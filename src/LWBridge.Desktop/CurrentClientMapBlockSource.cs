using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class CurrentClientMapBlockSourceHooks
{
    public Func<string, byte[]>? ReadAllBytes { get; init; }
    public Action<string, string>? WriteTextAtomic { get; init; }
    public Func<DateTimeOffset>? UtcNow { get; init; }
    public Func<TimeSpan, CancellationToken, Task>? DelayAsync { get; init; }
    public bool DisableCoarseMonsterMap { get; init; }
}

internal sealed partial class CurrentClientMapBlockSource : IMapScanProgressBatchSource
{
    private const string ProbeVersion = "lwbridge-live-resource-probe-2";
    private const string OverviewBridgeVersion = "lwbridge-overview-bridge-1";
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(50);

    private readonly Func<OverviewMapScanSession?> getSession;
    private readonly Func<OverviewMapScanSession, CancellationToken, Task>? waitForHealthySession;
    private readonly Func<OverviewMapScanSession, bool>? matchesOwnedSession;
    private readonly CurrentClientMapBlockSourceHooks? hooks;
    private readonly string overviewRuntimeRoot;
    private readonly string probeRuntimeRoot;
    private readonly string? assetCacheRoot;

    public CurrentClientMapBlockSource(
        OverviewLifecycleService lifecycle,
        CurrentClientMapBlockSourceHooks? hooks = null)
        : this(
            lifecycle is null ? throw new ArgumentNullException(nameof(lifecycle)) : lifecycle.GetReadyMapScanSession,
            null,
            null,
            hooks,
            lifecycle.WaitForHealthyMapScanSessionAsync,
            lifecycle.MatchesOwnedMapScanSession,
            lifecycle is null ? null : Path.Combine(lifecycle.ProfileRuntimeRoot, "asset-cache"))
    {
    }

    internal CurrentClientMapBlockSource(
        Func<OverviewMapScanSession?> getSession,
        string? overviewRuntimeRoot,
        string? probeRuntimeRoot,
        CurrentClientMapBlockSourceHooks? hooks = null,
        Func<OverviewMapScanSession, CancellationToken, Task>? waitForHealthySession = null,
        Func<OverviewMapScanSession, bool>? matchesOwnedSession = null,
        string? assetCacheRoot = null)
    {
        this.getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        this.waitForHealthySession = waitForHealthySession;
        this.matchesOwnedSession = matchesOwnedSession;
        this.hooks = hooks;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        this.overviewRuntimeRoot = overviewRuntimeRoot ??
            Path.Combine(local, "LWBridgeRebuild", "overview-bridge");
        this.probeRuntimeRoot = probeRuntimeRoot ??
            Path.Combine(local, "LWBridgeRebuild", "live-resource");
        this.assetCacheRoot = string.IsNullOrWhiteSpace(assetCacheRoot)
            ? null
            : Path.GetFullPath(assetCacheRoot);
    }

    public async Task<MapScanBlockCapture> CaptureAsync(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        CancellationToken cancellationToken)
    {
        if (request.SelectedTypes.Count != 1 || request.SelectedTypes[0] is not ("resource" or "city"))
            throw new BridgeCommandException(
                "LIVE_BLOCK_TYPES_UNSUPPORTED",
                "The current-client block source currently supports one Resource or Player City kind.");

        string mapKind = request.SelectedTypes[0];
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }
        NavigationObservation initialNavigation = await NavigateAsync(
                session, request.ServerId, request.WorldId, block.TargetX, block.TargetY, cancellationToken)
            .ConfigureAwait(false);
        RequireSameSession(session);
        CurrentClientAoiCoveragePlan plan = CurrentClientAoiCoveragePlanner.Build(
            request,
            block,
            initialNavigation.ServerLod,
            initialNavigation.AoiBlockSizes);

        var records = new Dictionary<string, MapStoredRecord>(StringComparer.Ordinal);
        var cellSummaries = new List<AoiCellCaptureSummary>(plan.Cells.Count);
        int? responseCount = null;
        if (mapKind == "city")
        {
            responseCount = await CaptureCityFootprintAsync(session, request, block, initialNavigation, plan,
                records, cellSummaries, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (CurrentClientAoiCell cell in plan.Cells)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NavigationObservation navigation = await NavigateAsync(
                        session, request.ServerId, request.WorldId, cell.TargetX, cell.TargetY, cancellationToken)
                    .ConfigureAwait(false);
                ValidateStableAoiGeometry(navigation, initialNavigation, plan);
                RequireSameSession(session);
                AoiCellCaptureSummary summary = await CaptureCurrentViewAsync(
                    session, request, mapKind, block, cell, records, cancellationToken).ConfigureAwait(false);
                cellSummaries.Add(summary);
            }
        }

        RequireSameSession(session);
        return BuildCapture(request, block, initialNavigation, plan, cellSummaries, records.Values, responseCount);
    }

    private OverviewMapScanSession RequireReadySession() =>
        getSession() ?? throw new BridgeCommandException(
            "GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");

    private void RequireSameSession(OverviewMapScanSession expected)
    {
        bool sameOwnedSession = matchesOwnedSession is not null
            ? matchesOwnedSession(expected)
            : getSession() == expected;
        if (!sameOwnedSession)
            throw new BridgeCommandException(
                "GAME_CONNECTION_UNAVAILABLE",
                "the owned game session changed during map acquisition");
    }

    private static void AddObservation(
        ProbeObservation observation,
        string mapKind,
        MapScanTargetBlock block,
        IDictionary<string, MapStoredRecord> records,
        ISet<int> selectedIndices)
    {
        if (mapKind == "city" && observation.CityTargetedView)
            throw new InvalidDataException("Player City block acquisition left the requested current view.");
        if (observation.SelectedIndex < 1 || observation.SelectedIndex > observation.MatchedCount)
            throw new InvalidDataException("The live map selected candidate index is outside its declared candidate count.");
        if (observation.Prepared.Count != observation.MatchedCount)
            throw new InvalidDataException("The live map candidate snapshot count did not match its declared candidate count.");

        for (int index = 0; index < observation.Prepared.Count; index++)
        {
            int candidateIndex = index + 1;
            if (!selectedIndices.Add(candidateIndex))
                throw new InvalidDataException("The live map candidate snapshot contained a duplicate index.");
            FirstLivePreparedResource prepared = observation.Prepared[index];
            if (prepared.Import.X >= block.MinX && prepared.Import.X <= block.MaxX &&
                prepared.Import.Y >= block.MinY && prepared.Import.Y <= block.MaxY)
                records[prepared.Record.RecordKey] = prepared.Record;
        }
    }

    private static MapScanBlockCapture BuildCapture(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int matchedCount,
        IEnumerable<int> selectedIndices,
        IEnumerable<MapStoredRecord> records)
    {
        MapStoredRecord[] accepted = records.ToArray();
        string payload = JsonSerializer.Serialize(new
        {
            protocol = "current_overview_probe_block_v1",
            blockIndex = block.BlockIndex,
            targetX = block.TargetX,
            targetY = block.TargetY,
            minX = block.MinX,
            minY = block.MinY,
            maxX = block.MaxX,
            maxY = block.MaxY,
            matchedCount,
            selectedIndices = selectedIndices.OrderBy(value => value).ToArray(),
            recordsInBlock = accepted.Length,
            coverage = "unproven_current_view_footprint",
        }, JsonOptions.Default);
        return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, payload, accepted);
    }

    private async Task<NavigationObservation> NavigateCoreAsync(
        OverviewMapScanSession session,
        int serverId,
        long worldId,
        int targetX,
        int targetY,
        CancellationToken cancellationToken)
    {
        string requestId = "nav" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "map-navigation.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "map-navigation-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={serverId.ToString(CultureInfo.InvariantCulture)}",
            $"worldId={worldId.ToString(CultureInfo.InvariantCulture)}",
            $"targetX={targetX.ToString(CultureInfo.InvariantCulture)}",
            $"targetY={targetY.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = Now() + NavigationTimeout;

        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? value = TryReadJson(resultPath);
            if (value is not null && MatchesString(value.Value, "requestId", requestId))
                return ValidateNavigationResult(value.Value, session, serverId, worldId, targetX, targetY);
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client map camera did not return a correlated navigation result.");
    }

    private static NavigationObservation ValidateNavigationResult(
        JsonElement root,
        OverviewMapScanSession session,
        int serverId,
        long worldId,
        int targetX,
        int targetY)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid))
            throw new InvalidDataException("Map navigation result did not match the active owned game session.");
        if (!MatchesInt(root, "serverId", serverId) || !MatchesLong(root, "worldId", worldId))
            throw new InvalidDataException("Map navigation result did not match the requested server/world.");
        int liveCurServerId = RequirePositiveInt(root, "liveCurServerId");
        if (liveCurServerId != serverId)
            throw new InvalidDataException("Map navigation did not prove the requested live server.");
        _ = RequirePositiveInt(root, "liveSelfServerId");
        if (!MatchesInt(root, "targetX", targetX) || !MatchesInt(root, "targetY", targetY))
            throw new InvalidDataException("Map navigation result did not match the requested target tile.");
        if (!MatchesString(root, "state", "proven"))
        {
            string error = ReadOptionalString(root, "error") ?? "unknown navigation failure";
            throw new InvalidDataException("Map navigation failed: " + error);
        }
        int liveWorldId = RequireNonNegativeInt(root, "liveWorldId");
        int? preTargetTileX = null;
        int? preTargetTileY = null;
        string expectedMethod;
        if (liveWorldId > 0)
        {
            int expectedUniqueTileX = RequireNonNegativeInt(root, "expectedUniqueTileX");
            int expectedUniqueTileY = RequireNonNegativeInt(root, "expectedUniqueTileY");
            int verifiedUniqueTileX = RequireNonNegativeInt(root, "verifiedUniqueTileX");
            int verifiedUniqueTileY = RequireNonNegativeInt(root, "verifiedUniqueTileY");
            if (verifiedUniqueTileX != expectedUniqueTileX || verifiedUniqueTileY != expectedUniqueTileY)
                throw new InvalidDataException("Special-world navigation did not prove the requested camera target in unique-tile space.");
            if (worldId != liveWorldId)
                throw new InvalidDataException("Special-world navigation did not remain in the requested live world.");
            _ = RequireNonNegativeInt(root, "liveWorldType");
            expectedMethod = "SceneUtils.TileToWorld(ForceChangeScene.World,serverId)+GoToUtil.GotoDragonPos(serverId,worldId,worldType)+TileToUniqueTile/WorldToUniqueTile";
        }
        else
        {
            _ = RequireFiniteDouble(root, "targetWorldX");
            _ = RequireFiniteDouble(root, "targetWorldY");
            _ = RequireFiniteDouble(root, "targetWorldZ");
            _ = RequireFiniteDouble(root, "postCurTargetX");
            _ = RequireFiniteDouble(root, "postCurTargetY");
            _ = RequireFiniteDouble(root, "postCurTargetZ");
            preTargetTileX = RequireNonNegativeInt(root, "preTargetTileX");
            preTargetTileY = RequireNonNegativeInt(root, "preTargetTileY");
            int callbackTileX = RequireNonNegativeInt(root, "postTargetTileX");
            int callbackTileY = RequireNonNegativeInt(root, "postTargetTileY");
            if (callbackTileX != targetX || callbackTileY != targetY)
                throw new InvalidDataException("Normal-world navigation did not prove the requested tile at the GotoWorldPos completion callback.");
            expectedMethod = "SceneUtils.TileToWorld(ForceChangeScene.World,serverId)+GoToUtil.GotoWorldPos(serverId,worldId)+completionCallbackCurTarget";
        }
        if (!MatchesString(root, "method", expectedMethod))
            throw new InvalidDataException("Map navigation did not prove the supported current-client route.");
        int currentLod = RequireNonNegativeInt(root, "currentLod");
        int serverLod = RequireNonNegativeInt(root, "serverLod");
        int[] aoiBlockSizes = RequirePositiveIntArray(root, "lwAoiBlockSizeArray");
        return new NavigationObservation(currentLod, serverLod, aoiBlockSizes, preTargetTileX, preTargetTileY);
    }

    private async Task<ProbeObservation> ProbeOnceAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        string mapKind,
        CancellationToken cancellationToken)
    {
        string requestId = "scan" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "command.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"requestId={requestId}",
            $"launchSessionId={session.SessionId}",
            $"profileId={session.ProfileId}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"mapKind={mapKind}",
            $"allowCityTargetedFallback={(mapKind == "city" ? "false" : "true")}",
            string.Empty,
        });
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + ProbeTimeout;

        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                byte[] bytes = ReadAllBytes(resultPath);
                return ValidateProbeResult(
                    root.Value,
                    bytes,
                    resultPath,
                    requestId,
                    startedAt,
                    session,
                    request,
                    mapKind);
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The current-client map probe did not return a correlated fresh result.");
    }

    private ProbeObservation ValidateProbeResult(
        JsonElement root,
        byte[] bytes,
        string resultPath,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        string mapKind)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesString(root, "mapKind", mapKind))
        {
            throw new InvalidDataException("Live map probe result did not match the active owned game session.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        if (state == "failed")
        {
            string error = ReadOptionalString(root, "error") ?? "unknown live map probe failure";
            RequireFreshCaptureTime(root, startedAt);
            if (mapKind == "resource" &&
                string.Equals(error, "no resource point is loaded after the fresh view response", StringComparison.Ordinal))
            {
                return new ProbeObservation(0, 0, false, true,
                    0, 0, Array.Empty<int>(), Array.Empty<FirstLivePreparedResource>());
            }
            throw new InvalidDataException($"Live {mapKind} probe failed: {error}");
        }
        if (state != "proven")
            throw new InvalidDataException("Live map probe result did not contain a supported terminal state.");

        const string currentViewRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)";
        const string currentViewEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true";
        int liveAoiBlockSize = 0;
        int liveAoiBlockCount = 0;
        int[] currentViewIndices = Array.Empty<int>();
        if (mapKind == "city")
        {
            liveAoiBlockSize = RequirePositiveInt(root, "lwAoiBlockSize");
            liveAoiBlockCount = RequirePositiveInt(root, "lwAoiBlockCount");
            int currentViewIndexCount = RequirePositiveInt(root, "curViewIndexCount");
            currentViewIndices = RequireNonNegativeIntArray(root, "curViewIndices");
            long maxIndexExclusive = checked((long)liveAoiBlockCount * liveAoiBlockCount);
            if (currentViewIndices.Length != currentViewIndexCount ||
                currentViewIndices.Any(index => index >= maxIndexExclusive))
                throw new InvalidDataException("Player City current-view AOI footprint is incomplete or out of range.");
        }
        if (mapKind == "city" &&
            root.TryGetProperty("cityPointCount", out JsonElement emptyCityCount) &&
            emptyCityCount.TryGetInt32(out int cityCount) && cityCount == 0)
        {
            RequireFreshCaptureTime(root, startedAt);
            if (!MatchesString(root, "requestRoute", currentViewRoute) ||
                !MatchesString(root, "responseEvidence", currentViewEvidence) ||
                !MatchesString(root, "source", "WorldPointManager._pointInfos") ||
                MatchesBool(root, "cityTargetedView", true) ||
                !root.TryGetProperty("point_records", out JsonElement emptyRecords) ||
                emptyRecords.ValueKind != JsonValueKind.Array || emptyRecords.GetArrayLength() != 0)
            {
                throw new InvalidDataException("Empty Player City block result did not prove a fresh current-view zero-row response.");
            }
            return new ProbeObservation(0, 0, false, true,
                liveAoiBlockSize, liveAoiBlockCount, currentViewIndices, Array.Empty<FirstLivePreparedResource>());
        }

        string countName = mapKind == "resource" ? "resourcePointCount" : "cityPointCount";
        string indexName = mapKind == "resource" ? "selectedResourceIndex" : "selectedCityIndex";
        int matchedCount = RequirePositiveInt(root, countName);
        int selectedIndex = RequirePositiveInt(root, indexName);
        bool cityTargetedView = mapKind == "city" && MatchesBool(root, "cityTargetedView", true);

        _ = LiveResourceProbeCommandService.PrepareCorrelatedResult(
            resultPath,
            requestId,
            out _,
            readAllBytes: _ => bytes,
            expectedServerId: request.ServerId,
            operationStartedAtUtc: startedAt,
            nowUtc: Now(),
            expectedProfileId: session.ProfileId,
            expectedLaunchSessionId: session.SessionId,
            expectedGamePid: session.GamePid,
            mapKind: mapKind);

        IReadOnlyList<FirstLivePreparedResource> prepared = mapKind == "city"
            ? FirstLiveResultImporter.PrepareCitySnapshot(bytes, resultPath)
            : FirstLiveResultImporter.PrepareResourceSnapshot(bytes, resultPath);
        if (prepared.Count != matchedCount)
            throw new InvalidDataException("The live map candidate snapshot count did not match its declared candidate count.");
        if (prepared.Any(item => item.Import.ServerId != request.ServerId))
            throw new InvalidDataException("The live map candidate snapshot contained a different server than the active bounded session.");
        return new ProbeObservation(matchedCount, selectedIndex, cityTargetedView, false,
            liveAoiBlockSize, liveAoiBlockCount, currentViewIndices, prepared);
    }

    private void RequireFreshCaptureTime(JsonElement root, DateTimeOffset startedAt)
    {
        if (!root.TryGetProperty("capturedAt", out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset capturedAt))
        {
            throw new InvalidDataException("Live map probe failure did not include a valid capture timestamp.");
        }
        DateTimeOffset now = Now();
        if (capturedAt < startedAt.AddSeconds(-2) || capturedAt > now.AddSeconds(5))
            throw new InvalidDataException("Live map probe failure timestamp is outside the active acquisition window.");
    }

    private async Task WriteCommandAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        if (hooks?.WriteTextAtomic is { } writer)
        {
            writer(path, content);
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private byte[] ReadAllBytes(string path)
    {
        if (hooks?.ReadAllBytes is { } reader)
            return reader(path);

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private JsonElement? TryReadJson(string path)
    {
        try
        {
            byte[] bytes = ReadAllBytes(path);
            using JsonDocument document = JsonDocument.Parse(bytes);
            return document.RootElement.Clone();
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    private DateTimeOffset Now() => hooks?.UtcNow?.Invoke() ?? DateTimeOffset.UtcNow;

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        hooks?.DelayAsync is { } testDelay
            ? testDelay(delay, cancellationToken)
            : Task.Delay(delay, cancellationToken);

    private static int RequirePositiveInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int result) || result <= 0)
            throw new InvalidDataException($"Live map probe field '{name}' must be a positive integer.");
        return result;
    }

    private static double RequireFiniteDouble(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetDouble(out double result) || !double.IsFinite(result))
            throw new InvalidDataException($"Live map probe field '{name}' must be a finite number.");
        return result;
    }

    private static string? ReadOptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool MatchesString(JsonElement root, string name, string expected) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool MatchesInt(JsonElement root, string name, int expected) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.TryGetInt32(out int parsed) && parsed == expected;

    private static bool MatchesLong(JsonElement root, string name, long expected) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.TryGetInt64(out long parsed) && parsed == expected;

    private static bool MatchesBool(JsonElement root, string name, bool expected) =>
        root.TryGetProperty(name, out JsonElement value) &&
        (expected ? value.ValueKind == JsonValueKind.True : value.ValueKind == JsonValueKind.False);

    private sealed record ProbeObservation(
        int MatchedCount,
        int SelectedIndex,
        bool CityTargetedView,
        bool IsEmptyView,
        int AoiBlockSize,
        int AoiBlockCount,
        int[] CurrentViewIndices,
        IReadOnlyList<FirstLivePreparedResource> Prepared);
}
