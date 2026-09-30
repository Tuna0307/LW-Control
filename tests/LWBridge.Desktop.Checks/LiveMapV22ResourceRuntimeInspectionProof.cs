using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Bounded live read-only Resource runtime inventory used only by
/// LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001.
/// It performs one normal Resource acquisition first so retained manager stores
/// can be compared with the exact accepted scan identity set.
/// </summary>
internal static class LiveMapV22ResourceRuntimeInspectionProof
{
    private const string TaskId = "LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001";
    private const string ProbeVersion = "lwbridge-live-resource-probe-2";
    private const string OfficialV22Sha256 =
        "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22";
    private const string GameSha256 =
        "905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d";

    internal static async Task RunAsync(string outputPath)
    {
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Directory.GetParent(localAppData)?.FullName
            ?? throw new InvalidOperationException("AppData root is unavailable.");
        string gameRoot = Path.Combine(localAppData, "FunFly", "Last War-Survival Game");
        string gamePath = Path.Combine(gameRoot, "Game", "LastWar.exe");
        string packagePath = Path.Combine(appData, "LocalLow", "FunFly", "Last War-Survival Game", "lwScripts", "LWScripts.data");
        string versionPath = Path.Combine(appData, "LocalLow", "FunFly", "Last War-Survival Game", "lwScripts", "version.txt");
        string runtimeRoot = Path.Combine(localAppData, "LWBridgeRebuild", "live-resource");
        string commandPath = Path.Combine(runtimeRoot, "runtime-diagnostic.txt");
        string resultPath = Path.Combine(runtimeRoot, "runtime-diagnostic-result.json");
        string bulkCommandPath = Path.Combine(runtimeRoot, "bulk-aoi-diagnostic.txt");
        string bulkResultPath = Path.Combine(runtimeRoot, "bulk-aoi-diagnostic-result.json");
        Directory.CreateDirectory(runtimeRoot);

        string profileId = "lwb317-resource-runtime-" + Guid.NewGuid().ToString("N")[..12];
        string databasePath = Path.Combine(runtimeRoot, "lwb317-resource-runtime-" + Guid.NewGuid().ToString("N") + ".db");
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        var evidence = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["taskId"] = TaskId,
            ["purpose"] = "read_only_resource_runtime_inventory_after_full_scan",
            ["startedAt"] = startedAt,
        };

        Exception? failure = null;
        OverviewLifecycleService? lifecycle = null;
        Map317CommandService? mapService = null;
        string? instanceId = null;
        int? gamePid = null;
        try
        {
            RequireNoPreexistingGameRuntime();
            string officialPackageHash = Sha256File(packagePath);
            string gameHash = Sha256File(gamePath);
            if (!string.Equals(officialPackageHash, OfficialV22Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Expected pristine official v22 package, observed {officialPackageHash}.");
            if (!string.Equals(gameHash, GameSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("LastWar.exe identity changed before Resource runtime inspection.");
            evidence["preflight"] = new
            {
                officialPackageSha256 = officialPackageHash,
                gameSha256 = gameHash,
                versionMarker = ReadText(versionPath),
                noPreexistingGameRuntime = true,
            };

            lifecycle = new OverviewLifecycleService(profileId, gameRoot);
            using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            using JsonDocument empty = JsonDocument.Parse("{}");
            _ = await lifecycle.InvokeAsync("profile_instance_start", empty.RootElement.Clone(), operationCts.Token).ConfigureAwait(false);
            OverviewMapScanSession session = await WaitForMapReadyAsync(lifecycle, operationCts.Token).ConfigureAwait(false);
            await lifecycle.WaitForHealthyMapScanSessionAsync(session, operationCts.Token).ConfigureAwait(false);
            instanceId = session.SessionId;
            gamePid = session.GamePid;

            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            mapService = new Map317CommandService(databasePath, lifecycle);
            evidence["session"] = new
            {
                profileId = session.ProfileId,
                instanceId = session.SessionId,
                gamePid = session.GamePid,
                gamePath = session.GamePath,
                gameStartedAtUtc = session.GameStartedAtUtc,
                serverId = context.ServerId,
                worldId = context.WorldId,
                tileWidth = context.TileWidth,
                tileHeight = context.TileHeight,
                playerTileX = context.PlayerTileX,
                playerTileY = context.PlayerTileY,
                runtimePackageSha256 = Sha256File(packagePath),
                runtimeVersionMarker = ReadText(versionPath),
            };

            JsonElement scanPayload = JsonSerializer.SerializeToElement(new
            {
                profileId,
                selectedTypes = new[] { "resource" },
                scanMode = "fast",
                resume = false,
            }, JsonOptions.Default);
            JsonElement scanStart = ToJson(await mapService.InvokeAsync(
                "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false));
            JsonElement scanFinal = await WaitForCompletedScanAsync(mapService, operationCts.Token).ConfigureAwait(false);
            ResourceCompletenessReport report = mapService.LastResourceCompletenessReport
                ?? throw new InvalidDataException("Runtime inspection scan did not produce Resource completeness diagnostics.");
            int[] knownResourceIds = report.AcceptedRows
                .Select(row => row.PointIndex)
                .Where(value => value is > 0)
                .Select(value => value!.Value)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (knownResourceIds.Length != report.FinalAcceptedUniqueRecords)
                throw new InvalidDataException("Runtime inspection scan accepted rows do not have one unique positive point ID each.");
            evidence["scan"] = new
            {
                start = scanStart,
                final = scanFinal,
                sourceUnique = report.FinalAcceptedUniqueRecords,
                knownResourceIds = knownResourceIds.Length,
                rawObservedPointInfoOccurrences = report.RawObservedPointInfoOccurrences,
                resourceCandidateOccurrences = report.ResourceCandidateOccurrences,
                rejectedResourceCandidateOccurrences = report.RejectedResourceCandidateOccurrences,
            };

            JsonElement inventory = await RequestInventoryAsync(
                commandPath, resultPath, session, knownResourceIds, null, null, operationCts.Token).ConfigureAwait(false);
            evidence["runtimeInspection"] = inventory.Clone();

            var spotChecks = new List<object>();
            ResourceCompletenessAcceptedRow[] knownRows = report.AcceptedRows
                .Where(row => row.PointIndex is > 0 && row.X > 0 && row.Y > 0)
                .OrderBy(row => row.X)
                .ThenBy(row => row.Y)
                .ToArray();
            if (knownRows.Length > 0)
            {
                ResourceCompletenessAcceptedRow[] selectedKnown = knownRows.Length == 1
                    ? [knownRows[0]]
                    : [knownRows[0], knownRows[^1]];
                foreach (ResourceCompletenessAcceptedRow row in selectedKnown)
                {
                    JsonElement jumpPayload = JsonSerializer.SerializeToElement(new
                    {
                        serverId = context.ServerId,
                        x = row.X,
                        y = row.Y,
                    }, JsonOptions.Default);
                    JsonElement jump = ToJson(await mapService.InvokeAsync(
                        "map_coordinate_jump", jumpPayload, operationCts.Token).ConfigureAwait(false));
                    await Task.Delay(1500, operationCts.Token).ConfigureAwait(false);
                    int aoiIndex = checked((row.Y / 10) * 100 + (row.X / 10));
                    JsonElement snapshot = await RequestInventoryAsync(
                        commandPath, resultPath, session, knownResourceIds, aoiIndex, row.PointIndex, operationCts.Token)
                        .ConfigureAwait(false);
                    JsonElement pointInfos = snapshot.GetProperty("resourceInventory").GetProperty("pointInfos");
                    JsonElement? targetPoint = pointInfos.TryGetProperty("targetPointRow", out JsonElement targetPointValue) &&
                                               targetPointValue.ValueKind == JsonValueKind.Object
                        ? targetPointValue.Clone()
                        : null;
                    JsonElement[] targetRows = ReadTargetRows(pointInfos);
                    spotChecks.Add(new
                    {
                        kind = "known_resource_coordinate",
                        requested = new { pointId = row.PointIndex, x = row.X, y = row.Y, aoiIndex },
                        jump,
                        targetPointPresent = targetPoint is not null,
                        targetPoint,
                        targetAoiResourceCount = targetRows.Length,
                        targetAoiRows = targetRows,
                    });
                }
            }

            HashSet<int> populatedAois = report.ResourcesByNativeAoiIndex.Keys
                .Select(value => int.TryParse(value, out int parsed) ? parsed : -1)
                .Where(value => value >= 0 && value < 10000)
                .ToHashSet();
            foreach (int aoiIndex in SelectZeroAois(populatedAois, 3))
            {
                int cellX = aoiIndex % 100;
                int cellY = aoiIndex / 100;
                int x = Math.Min(cellX * 10 + 5, 999);
                int y = Math.Min(cellY * 10 + 5, 999);
                JsonElement remoteCoverage = await RequestProductionEquivalentResourceCoverageAsync(
                    bulkCommandPath, bulkResultPath, session, context, x, y, 0, operationCts.Token)
                    .ConfigureAwait(false);
                JsonElement[] remoteCoverageRows = ReadResourcePointRows(remoteCoverage);
                int[] remoteCoverageResourceIds = ReadPointIds(remoteCoverageRows);
                await Task.Delay(1500, operationCts.Token).ConfigureAwait(false);
                JsonElement postRemoteNoMoveSnapshot = await RequestInventoryAsync(
                    commandPath, resultPath, session, knownResourceIds, aoiIndex, null, operationCts.Token)
                    .ConfigureAwait(false);
                JsonElement[] postRemoteNoMoveRows = ReadTargetRows(
                    postRemoteNoMoveSnapshot.GetProperty("resourceInventory").GetProperty("pointInfos"));
                int[] postRemoteNoMoveResourceIds = ReadPointIds(postRemoteNoMoveRows);
                JsonElement jumpPayload = JsonSerializer.SerializeToElement(new
                {
                    serverId = context.ServerId,
                    x,
                    y,
                }, JsonOptions.Default);
                JsonElement jump = ToJson(await mapService.InvokeAsync(
                    "map_coordinate_jump", jumpPayload, operationCts.Token).ConfigureAwait(false));
                await Task.Delay(1500, operationCts.Token).ConfigureAwait(false);
                JsonElement snapshot = await RequestInventoryAsync(
                    commandPath, resultPath, session, knownResourceIds, aoiIndex, null, operationCts.Token)
                    .ConfigureAwait(false);
                JsonElement[] targetRows = ReadTargetRows(
                    snapshot.GetProperty("resourceInventory").GetProperty("pointInfos"));
                int[] targetResourceIds = ReadPointIds(targetRows);
                int[] rowsAbsentFromScan = targetRows
                    .Select(row => row.TryGetProperty("pointId", out JsonElement pointId) && pointId.TryGetInt32(out int parsed) ? parsed : 0)
                    .Where(value => value > 0 && Array.BinarySearch(knownResourceIds, value) < 0)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
                int[] heldViewIdsAbsentFromImmediateRemoteCoverage = targetResourceIds
                    .Where(value => Array.BinarySearch(remoteCoverageResourceIds, value) < 0)
                    .ToArray();
                spotChecks.Add(new
                {
                    kind = "scan_zero_aoi",
                    requested = new { x, y, aoiIndex },
                    productionEquivalentRemoteCoverage = remoteCoverage,
                    productionEquivalentRemoteResourceCount = remoteCoverageRows.Length,
                    productionEquivalentRemoteResourceIds = remoteCoverageResourceIds,
                    postRemoteNoMoveResourceCount = postRemoteNoMoveRows.Length,
                    postRemoteNoMoveResourceIds = postRemoteNoMoveResourceIds,
                    jump,
                    targetAoiResourceCount = targetRows.Length,
                    targetAoiRows = targetRows,
                    resourceIdsAbsentFromCompletedScan = rowsAbsentFromScan,
                    heldViewResourceIdsAbsentFromImmediateRemoteCoverage = heldViewIdsAbsentFromImmediateRemoteCoverage,
                });
            }
            evidence["spotChecks"] = spotChecks;
            evidence["state"] = "proven";
        }
        catch (Exception error)
        {
            failure = error;
            evidence["state"] = "failed";
            evidence["failure"] = new
            {
                type = error.GetType().FullName,
                message = error.Message,
                code = error is BridgeCommandException bridge ? bridge.Code : null,
            };
        }
        finally
        {
            try { mapService?.Dispose(); }
            catch (Exception error) { evidence["mapServiceDisposeError"] = error.Message; }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (lifecycle is not null && !string.IsNullOrWhiteSpace(instanceId))
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    JsonElement stopPayload = JsonSerializer.SerializeToElement(new { instanceId }, JsonOptions.Default);
                    await lifecycle.InvokeAsync("profile_instance_stop", stopPayload, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    evidence["cleanupStopError"] = stopError.Message;
                    failure ??= stopError;
                    evidence["state"] = "failed";
                }
            }
            lifecycle?.Dispose();
            evidence["cleanup"] = new
            {
                ownedGamePid = gamePid,
                ownedGamePidAlive = gamePid is int pid && IsProcessAlive(pid),
                packageSha256 = File.Exists(packagePath) ? Sha256File(packagePath) : null,
                packageRestoredToOfficialV22 = File.Exists(packagePath) &&
                    string.Equals(Sha256File(packagePath), OfficialV22Sha256, StringComparison.OrdinalIgnoreCase),
                versionMarker = ReadText(versionPath),
                tempDatabaseDeleted = TryDeleteDatabase(databasePath),
            };
            evidence["finishedAt"] = DateTimeOffset.UtcNow;
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ok = failure is null,
            taskId = TaskId,
            outputPath,
            state = evidence["state"],
        }, JsonOptions.Default));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static async Task<JsonElement> RequestInventoryAsync(
        string commandPath,
        string resultPath,
        OverviewMapScanSession session,
        IReadOnlyList<int> knownResourceIds,
        int? targetAoiIndex,
        int? targetPointId,
        CancellationToken cancellationToken)
    {
        string requestId = "resourceinventory" + Guid.NewGuid().ToString("N");
        var lines = new List<string>
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid}",
            "mode=resource_inventory",
            "knownResourceIds=" + string.Join(',', knownResourceIds),
        };
        if (targetAoiIndex.HasValue) lines.Add($"targetAoiIndex={targetAoiIndex.Value}");
        if (targetPointId.HasValue) lines.Add($"targetPointId={targetPointId.Value}");
        lines.Add(string.Empty);
        await WriteCommandAtomicallyAsync(commandPath, string.Join('\n', lines), cancellationToken).ConfigureAwait(false);
        return await WaitForInventoryAsync(resultPath, requestId, session, cancellationToken).ConfigureAwait(false);
    }

    private static JsonElement[] ReadTargetRows(JsonElement pointInfos) =>
        pointInfos.TryGetProperty("targetResourceRows", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Select(row => row.Clone()).ToArray()
            : Array.Empty<JsonElement>();

    private static JsonElement[] ReadResourcePointRows(JsonElement bulkResult) =>
        bulkResult.TryGetProperty("point_records", out JsonElement rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                              row.TryGetProperty("kind", out JsonElement kind) &&
                              string.Equals(kind.GetString(), "resource_point", StringComparison.Ordinal))
                .Select(row => row.Clone())
                .ToArray()
            : Array.Empty<JsonElement>();

    private static int[] ReadPointIds(IEnumerable<JsonElement> rows) =>
        rows.Select(row =>
                row.TryGetProperty("pointId", out JsonElement pointId) && pointId.TryGetInt32(out int parsed)
                    ? parsed
                    : 0)
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

    private static async Task<JsonElement> RequestProductionEquivalentResourceCoverageAsync(
        string commandPath,
        string resultPath,
        OverviewMapScanSession session,
        CurrentClientMapContext context,
        int targetX,
        int targetY,
        int holdMilliseconds,
        CancellationToken cancellationToken)
    {
        string requestId = "resourcecoverage" + Guid.NewGuid().ToString("N");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid}",
            $"serverId={context.ServerId}",
            "scanRunId=resource-completeness-immediate-coverage",
            "viewLevel=-1",
            "requestMode=coverage",
            $"targetTileX={targetX}",
            $"targetTileY={targetY}",
            "requestedCount=8",
            $"holdMilliseconds={holdMilliseconds}",
            $"homeTileX={context.PlayerTileX ?? -1}",
            $"homeTileY={context.PlayerTileY ?? -1}",
            "includeCity=false",
            "includeResource=true",
            "includeMonster=false",
            "includeMonsterProtection=false",
            "includeTrain=false",
            "includeDispatch=false",
            "includeGhost=false",
            "includeTreasure=false",
            "includeResourceDetails=true",
            "deferRestoreUntilResponse=true",
            string.Empty,
        });
        await WriteCommandAtomicallyAsync(commandPath, command, cancellationToken).ConfigureAwait(false);

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(resultPath))
                {
                    using JsonDocument document = JsonDocument.Parse(
                        await File.ReadAllBytesAsync(resultPath, cancellationToken).ConfigureAwait(false));
                    JsonElement root = document.RootElement;
                    if (ReadString(root, "requestId") == requestId)
                    {
                        if (ReadString(root, "probeVersion") != ProbeVersion ||
                            ReadString(root, "profileId") != session.ProfileId ||
                            ReadString(root, "launchSessionId") != session.SessionId ||
                            ReadString(root, "challenge") != session.Challenge ||
                            !root.TryGetProperty("gamePid", out JsonElement pid) || pid.GetInt32() != session.GamePid ||
                            ReadString(root, "requestMode") != "coverage")
                            throw new InvalidDataException("Production-equivalent Resource coverage identity mismatch.");
                        if (ReadString(root, "state") != "proven")
                            throw new InvalidDataException(
                                "Production-equivalent Resource coverage failed: " +
                                (ReadString(root, "error") ?? "unknown"));
                        if (!root.TryGetProperty("holdMilliseconds", out JsonElement hold) || hold.GetInt32() != holdMilliseconds ||
                            !root.TryGetProperty("includeResource", out JsonElement includeResource) ||
                            includeResource.ValueKind != JsonValueKind.True ||
                            !root.TryGetProperty("deferRestoreUntilResponse", out JsonElement deferRestore) ||
                            deferRestore.ValueKind != JsonValueKind.True)
                            throw new InvalidDataException("Production-equivalent Resource coverage parameters mismatch.");
                        return root.Clone();
                    }
                }
            }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Production-equivalent Resource coverage did not return a correlated result.");
    }

    private static int[] SelectZeroAois(IReadOnlySet<int> populated, int count)
    {
        int[] preferred = [505, 5050, 9090, 950, 9050, 2500, 7525];
        var result = new List<int>(count);
        foreach (int preferredIndex in preferred)
        {
            int selected = FindNearestZeroAoi(preferredIndex, populated, result);
            if (selected >= 0) result.Add(selected);
            if (result.Count == count) break;
        }
        return result.ToArray();
    }

    private static int FindNearestZeroAoi(int preferred, IReadOnlySet<int> populated, IReadOnlyCollection<int> used)
    {
        int preferredX = preferred % 100;
        int preferredY = preferred / 100;
        return Enumerable.Range(0, 10000)
            .Where(index => !populated.Contains(index) && !used.Contains(index))
            .OrderBy(index => Math.Abs((index % 100) - preferredX) + Math.Abs((index / 100) - preferredY))
            .ThenBy(index => index)
            .FirstOrDefault(-1);
    }

    private static async Task<JsonElement> WaitForInventoryAsync(
        string resultPath,
        string requestId,
        OverviewMapScanSession session,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(resultPath))
                {
                    using JsonDocument document = JsonDocument.Parse(await File.ReadAllBytesAsync(resultPath, cancellationToken));
                    JsonElement root = document.RootElement;
                    if (ReadString(root, "requestId") == requestId)
                    {
                        if (ReadString(root, "probeVersion") != ProbeVersion ||
                            ReadString(root, "profileId") != session.ProfileId ||
                            ReadString(root, "launchSessionId") != session.SessionId ||
                            !root.TryGetProperty("gamePid", out JsonElement pid) || pid.GetInt32() != session.GamePid ||
                            ReadString(root, "mode") != "resource_inventory")
                            throw new InvalidDataException("Resource runtime inventory identity mismatch.");
                        if (ReadString(root, "state") != "proven")
                            throw new InvalidDataException("Resource runtime inventory failed: " + (ReadString(root, "error") ?? "unknown"));
                        if (!root.TryGetProperty("resourceInventory", out JsonElement inventory) || inventory.ValueKind != JsonValueKind.Object)
                            throw new InvalidDataException("Resource runtime inventory payload is missing.");
                        return JsonSerializer.SerializeToElement(new
                        {
                            schemaVersion = root.GetProperty("schemaVersion").GetInt32(),
                            probeVersion = ReadString(root, "probeVersion"),
                            requestId,
                            profileId = ReadString(root, "profileId"),
                            launchSessionId = ReadString(root, "launchSessionId"),
                            gamePid = session.GamePid,
                            mode = "resource_inventory",
                            state = "proven",
                            resourceInventory = inventory.Clone(),
                            capturedAt = ReadString(root, "capturedAt"),
                        }, JsonOptions.Default).Clone();
                    }
                }
            }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Resource runtime inventory did not return a correlated result.");
    }

    private static async Task WriteCommandAtomicallyAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, true);
    }

    private static async Task<OverviewMapScanSession> WaitForMapReadyAsync(
        OverviewLifecycleService lifecycle,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OverviewMapScanSession? session = lifecycle.GetReadyMapScanSession();
            if (session is not null && lifecycle.GetLiveServerId() is > 0) return session;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Owned Last War lifecycle did not reach Map-ready state.");
    }

    private static async Task<JsonElement> WaitForCompletedScanAsync(
        Map317CommandService service,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(7);
        JsonElement last = default;
        using JsonDocument empty = JsonDocument.Parse("{}");
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = ToJson(await service.InvokeAsync("map_scan_status", empty.RootElement.Clone(), cancellationToken).ConfigureAwait(false));
            if (!last.GetProperty("isReading").GetBoolean()) break;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        if (last.ValueKind == JsonValueKind.Undefined ||
            last.GetProperty("isReading").GetBoolean() ||
            !string.Equals(last.GetProperty("phase").GetString(), "completed", StringComparison.Ordinal) ||
            last.GetProperty("failedBlocks").GetInt32() != 0 ||
            last.GetProperty("unreadBlocks").GetInt32() != 0 ||
            last.GetProperty("readBlocks").GetInt32() != last.GetProperty("totalBlocks").GetInt32())
            throw new InvalidDataException("Resource runtime inspection scan did not reach clean completion.");
        return last.Clone();
    }

    private static void RequireNoPreexistingGameRuntime()
    {
        foreach (string name in new[] { "LastWar", "LastWarLauncher", "LWBridge", "LWBridge.Desktop" })
        {
            Process[] processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Length > 0)
                    throw new InvalidOperationException($"Refusing Resource runtime inspection because pre-existing {name} ownership is ambiguous.");
            }
            finally
            {
                foreach (Process process in processes) process.Dispose();
            }
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement ToJson(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default).Clone();

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path).Trim() : null;

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool TryDeleteDatabase(string path)
    {
        bool ok = true;
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { ok = false; }
        }
        return ok && !File.Exists(path) && !File.Exists(path + "-wal") && !File.Exists(path + "-shm");
    }
}
