using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    private static readonly TimeSpan CameraFreeScanTimeout = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan CameraFreeCancelTimeout = TimeSpan.FromSeconds(5);
    private sealed class CameraFreeTerminalDriftException(string message) : Exception(message);

    private async Task<IReadOnlyList<MapScanBlockCapture>> CaptureCameraFreeMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlySet<int> pendingBlockIndices,
        Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken,
        IReadOnlyList<FastTrainPrepared>? fastTrucks = null)
    {
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        if (logicalBlocks.Count != 2500 || pendingBlockIndices.Count != logicalBlocks.Count ||
            logicalBlocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("Camera-free scan needs the complete 2,500-block map.");
        if (request.WorldId != 0 || request.LiveServerId is int liveServer && liveServer != request.ServerId)
            throw new InvalidDataException("Camera-free scan is available only for the game's current normal-world server.");

        // Mobile Train rows belong to the independently verified rectangle
        // source. Do not spend the point sweep reading a sparse second source
        // or allow it to overwrite the typed mobile snapshot.
        MapScanExecutionRequest pointRequest = fastTrucks is null ? request :
            request with { SelectedTypes = request.SelectedTypes.Where(kind => kind is not ("truck" or "railway")).ToArray() };

        // Start metrics before the native sweep, not after awaiting its result.
        // This state is private until the entire response passes admission.
        FastFullWorldResumeState state = new(session, request) { CameraFreeAcquisition = true };
        progress?.Invoke(new MapScanSourceProgress(1));
        JsonElement response = default;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                // Each attempt has a new requestId and an independent 35-second
                // deadline. No records from an unproven attempt are admitted.
                response = await ProbeCameraFreeMapAsync(session, pointRequest, cancellationToken)
                    .ConfigureAwait(false);
                break;
            }
            catch (CameraFreeTerminalDriftException error)
            {
                if (attempt != 0)
                    throw new MapScanSourceValidationException(
                        "Camera-free native camera tile changed again after its one bounded direct retry; " +
                        "camera fallback was withheld.", error);

                // The native owner has already terminated and restored its exact
                // AOI set/flags. A moving camera is NOT valid scan evidence and
                // cannot authorize camera fallback; give startup navigation a
                // bounded settle before a completely fresh direct attempt.
                RequireSameSession(session);
                fastFullWorldResumeState = null;
                LastResourceCompletenessReport = null;
                LastMonsterProtectionDetailMetrics = null;
                Console.Error.WriteLine(
                    $"CAMERA_FREE_CAMERA_DRIFT_RETRY server={request.ServerId} run={request.RunId} attempt=1/2 reason={error.Message}");
                await DelayAsync(FastCityStartupSettleDelay, cancellationToken).ConfigureAwait(false);
                RequireSameSession(session);
                fastCitySettledSessionId = session.SessionId;
            }
        }
        RequireSameSession(session);

        // Keep the direct source's staged rows out of the camera resume state.
        // They are admitted to publication only after complete AOI/record checks.
        // Native may retry up to two transient identity-incomplete chunks;
        // count all attempted requests, not only the 63 successful replies.
        state.ProbeAttempts = response.GetProperty("requestsSent").GetInt32();
        state.ProbeRetries = state.ProbeAttempts - 63;
        DateTimeOffset capturedAt = DateTimeOffset.Parse(
            RequiredString(response, "capturedAt"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        JsonElement batches = response.GetProperty("batches");
        string resultPath = Path.Combine(probeRuntimeRoot, "camera-free-scan-result.json");
        // In a mixed scan Truck rows belong exclusively to the independently
        // verified 625-rectangle producer. The 63-chunk point/Railway sweep
        // observes a small, transient Truck subset with different coverage;
        // accepting that subset as a second Truck source could replace a
        // qualified fast row or admit an unverified one.
        long lastBatchCapturedMs = 0;
        long terminalCapturedMs = capturedAt.ToUnixTimeMilliseconds();
        for (int i = 0; i < batches.GetArrayLength(); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement batch = batches[i];
            // City positions may change during the 20–35s sweep. Preserve the
            // actual per-reply observation order from native monotonic time;
            // never relabel all 63 replies with the terminal timestamp and
            // thereby manufacture simultaneous conflicting City locations.
            if (!batch.TryGetProperty("capturedAtUnixMs", out JsonElement captureValue) ||
                !captureValue.TryGetInt64(out long batchCapturedMs) ||
                batchCapturedMs <= lastBatchCapturedMs ||
                batchCapturedMs < terminalCapturedMs - 40_000 ||
                batchCapturedMs > terminalCapturedMs + 2_000)
                throw new InvalidDataException("Camera-free batch has missing, unordered or implausible native capture time.");
            lastBatchCapturedMs = batchCapturedMs;
            FastCityBatchObservation normalized = NormalizeCameraFreeBatch(
                batch, pointRequest, resultPath,
                DateTimeOffset.FromUnixTimeMilliseconds(batchCapturedMs),
                DateTimeOffset.FromUnixTimeMilliseconds(terminalCapturedMs + 2_000));
            int previousAois = state.Covered.Count;
            MergeFullWorldObservation(state, normalized);
            if (state.Covered.Count - previousAois != normalized.RequestedIndices.Length)
                throw new InvalidDataException("Camera-free AOI batches overlapped or missed unique cells.");
            state.ValidatedProbeResponses++;
        }
        if (state.Covered.Count != 10000 || state.ValidatedProbeResponses != 63)
            throw new InvalidDataException("Camera-free scan did not independently verify 10,000 AOIs in 63 replies.");
        if (fastTrucks is not null)
        {
            foreach (FastTrainPrepared truck in fastTrucks)
            {
                if (truck.Record.Kind is not ("truck" or "railway") ||
                    !request.SelectedTypes.Contains(truck.Record.Kind, StringComparer.Ordinal))
                    throw new InvalidDataException("Fast mobile Train result contained an unknown or unselected kind.");
                state.TrainRecords[truck.Record.RecordKey] = truck;
            }
            state.RailwaySweepVerified = request.SelectedTypes.Contains("railway", StringComparer.Ordinal);
        }
        if (state.CityRecords.Count + state.ResourceRecords.Count + state.DispatchRecords.Count +
            state.GhostRecords.Count + state.TreasureRecords.Count + state.MonsterRecords.Count +
            state.TrainRecords.Count == 0 &&
            !request.SelectedTypes.Contains("railway", StringComparer.Ordinal))
            throw new InvalidDataException("Camera-free scan returned zero selected rows.");

        state.AcquisitionMilliseconds = (Now() - state.MetricsStartedAt).TotalMilliseconds;
        progress?.Invoke(new MapScanSourceProgress(85));
        fastFullWorldResumeState = state;
        IReadOnlyList<MapScanBlockCapture> captures;
        try
        {
            captures = await CompleteFullWorldMapAsync(
                session, request, logicalBlocks, state, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (MapScanSourceValidationException error)
        {
            // The direct producer already acknowledged native restoration.
            // A later detail-enrichment failure can safely try the intact
            // camera route before ending this scan as a source validation error.
            throw new InvalidDataException("Camera-free detail hydration failed: " + error.Message, error);
        }
        if (captures.All(capture => capture.Records.Count == 0))
            throw new InvalidDataException("Camera-free scan returned zero rows after final record hydration.");
        return captures;
    }

    private async Task<JsonElement> ProbeCameraFreeMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = "direct" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "camera-free-scan.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "camera-free-scan-result.json");
        string cancelPath = Path.Combine(probeRuntimeRoot, "camera-free-scan-cancel.txt");
        static string Flag(bool value) => value ? "true" : "false";
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"worldId={request.WorldId.ToString(CultureInfo.InvariantCulture)}",
            $"homeTileX={(request.PlayerTileX ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"homeTileY={(request.PlayerTileY ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"includeCity={Flag(request.SelectedTypes.Contains("city", StringComparer.Ordinal))}",
            $"includeResource={Flag(request.SelectedTypes.Contains("resource", StringComparer.Ordinal))}",
            $"includeMonster={Flag(IncludesMonsterSource(request))}",
            $"includeMonsterProtection={Flag(IncludesZombieBoss(request))}",
            $"includeTrain={Flag(request.SelectedTypes.Contains("railway", StringComparer.Ordinal))}",
            $"includeDispatch={Flag(request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal))}",
            $"includeGhost={Flag(request.SelectedTypes.Contains("ghost", StringComparer.Ordinal))}",
            $"includeTreasure={Flag(request.SelectedTypes.Contains("treasure", StringComparer.Ordinal))}",
            $"includeResourceDetails={Flag(request.SelectedTypes.Contains("resource", StringComparer.Ordinal))}",
            string.Empty,
        });
        DateTimeOffset startedAt = Now();
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        bool nativeStopped = false;
        try
        {
            DateTimeOffset deadline = startedAt + CameraFreeScanTimeout;
            while (Now() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JsonElement? root = TryReadJson(resultPath);
                if (root is not null && MatchesString(root.Value, "requestId", requestId))
                {
                    JsonElement result = root.Value;
                    // Lua finish() first retires the exact native lane and
                    // restores its original AOI membership/response flags;
                    // cameraStable independently checks whether game navigation
                    // moved the physical camera while this lane was running.
                    // Only a complete, empty, owner/freshness-verified failed
                    // receipt may initiate a NEW direct retry without cancel.
                    if (IsCameraFreeTerminalDriftReceipt(result, session, request, requestId, startedAt))
                    {
                        nativeStopped = true;
                        throw new CameraFreeTerminalDriftException(
                            "Native AOI state was restored, but the camera tile moved during the direct sweep.");
                    }
                    // A terminal native reply is the only safe handoff to the
                    // camera route: it proves the original AOI set and camera
                    // tile are restored, even if the direct sweep itself failed.
                    if (!MatchesBool(result, "cameraStable", true) ||
                        !MatchesBool(result, "curViewRestored", true))
                        throw new MapScanSourceValidationException(
                            "Camera-free producer did not prove original AOI/camera state restoration.");
                    nativeStopped = true;
                    ValidateCameraFreeScanHeader(result, session, request, requestId, startedAt);
                    return result;
                }
                await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException("Camera-free scan did not finish a correlated native full-world sweep.");
        }
        finally
        {
            if (!nativeStopped)
            {
                // The cancel command is request-bound and the native lane owns
                // the exact cleanup. Do not start a camera fallback until the
                // reply acknowledges restoration, including when Stop was used.
                await WriteCommandAsync(cancelPath, "requestId=" + requestId + "\n", CancellationToken.None)
                    .ConfigureAwait(false);
                await RequireCameraFreeCancelReceiptAsync(
                    resultPath, requestId, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task RequireCameraFreeCancelReceiptAsync(
        string resultPath, string requestId, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = Now() + CameraFreeCancelTimeout;
        while (Now() < deadline)
        {
            JsonElement? result = TryReadJson(resultPath);
            if (result is not null && MatchesString(result.Value, "requestId", requestId))
            {
                if (MatchesBool(result.Value, "cameraStable", true) &&
                    MatchesBool(result.Value, "curViewRestored", true) &&
                    MatchesString(result.Value, "state", "failed"))
                    return;
                if (MatchesString(result.Value, "state", "failed")) break;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new MapScanSourceValidationException(
            "Camera-free cancellation did not prove native camera and AOI restoration; camera fallback was withheld.");
    }

    private void ValidateCameraFreeScanHeader(
        JsonElement root, OverviewMapScanSession session,
        MapScanExecutionRequest request, string requestId, DateTimeOffset startedAt)
    {
        ValidateCameraFreeScanOwner(root, session, request, requestId, startedAt);
        if (!MatchesString(root, "state", "proven"))
            throw new InvalidDataException(
                "Camera-free native acquisition failed: " + (ReadOptionalString(root, "error") ?? "unknown source failure"));
        if (!MatchesInt(root, "completedAois", 10000) ||
            !root.TryGetProperty("requestsSent", out JsonElement requestsSentValue) ||
            requestsSentValue.ValueKind != JsonValueKind.Number ||
            !requestsSentValue.TryGetInt32(out int requestsSent) ||
            requestsSent is < 63 or > 65 ||
            !MatchesInt(root, "repliesCaptured", 63) ||
            !root.TryGetProperty("batches", out JsonElement batches) ||
            batches.ValueKind != JsonValueKind.Array || batches.GetArrayLength() != 63 ||
            !root.TryGetProperty("elapsedSeconds", out JsonElement elapsedValue) ||
            !elapsedValue.TryGetDouble(out double elapsed) || !double.IsFinite(elapsed) || elapsed < 0)
            throw new InvalidDataException("Camera-free native result did not prove all 63 replies and 10,000 AOIs.");
    }

    internal bool IsCameraFreeTerminalDriftReceipt(
        JsonElement root, OverviewMapScanSession session,
        MapScanExecutionRequest request, string requestId, DateTimeOffset startedAt)
    {
        if (!MatchesString(root, "state", "failed") ||
            !MatchesString(root, "error", "camera_tile_changed") ||
            !MatchesBool(root, "curViewRestored", true) ||
            !MatchesBool(root, "cameraStable", false) ||
            !MatchesInt(root, "completedAois", 10000) ||
            !MatchesInt(root, "repliesCaptured", 63) ||
            !root.TryGetProperty("requestsSent", out JsonElement requestsValue) ||
            requestsValue.ValueKind != JsonValueKind.Number ||
            !requestsValue.TryGetInt32(out int requests) || requests is < 63 or > 65 ||
            !root.TryGetProperty("batches", out JsonElement batches) ||
            batches.ValueKind != JsonValueKind.Array || batches.GetArrayLength() != 0 ||
            !root.TryGetProperty("elapsedSeconds", out JsonElement elapsedValue) ||
            !elapsedValue.TryGetDouble(out double elapsed) || !double.IsFinite(elapsed) || elapsed < 0)
            return false;

        // A same requestId alone is insufficient. The *entire* native owner
        // snapshot must match before even considering a new direct attempt.
        ValidateCameraFreeScanOwner(root, session, request, requestId, startedAt);
        return true;
    }

    private void ValidateCameraFreeScanOwner(
        JsonElement root, OverviewMapScanSession session,
        MapScanExecutionRequest request, string requestId, DateTimeOffset startedAt)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesString(root, "scanRunId", request.RunId) ||
            !MatchesInt(root, "serverId", request.ServerId) ||
            !MatchesLong(root, "worldId", request.WorldId))
            throw new InvalidDataException("Camera-free native result did not match the owned scan/session/server.");
        RequireFreshCaptureTime(root, startedAt);
    }

    private FastCityBatchObservation NormalizeCameraFreeBatch(
        JsonElement batch, MapScanExecutionRequest request,
        string resultPath, DateTimeOffset capturedAt, DateTimeOffset terminalCaptureUpperBound)
    {
        if (batch.ValueKind != JsonValueKind.Object ||
            !batch.TryGetProperty("indices", out JsonElement indicesValue) ||
            indicesValue.ValueKind != JsonValueKind.Array ||
            !batch.TryGetProperty("point_records", out JsonElement points) ||
            points.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Camera-free batch omitted its native AOI/point snapshots.");
        int[] indices = RequireNonNegativeIntArray(batch, "indices");
        if (indices.Length is < 1 or > 160 || indices.Distinct().Count() != indices.Length ||
            indices.Any(index => index >= 10000))
            throw new InvalidDataException("Camera-free native AOI batch had duplicate or out-of-range indices.");
        HashSet<int> requestedSet = indices.ToHashSet();
        if (!batch.TryGetProperty("monster_march_records", out JsonElement monsterMarches) ||
            monsterMarches.ValueKind != JsonValueKind.Array ||
            !batch.TryGetProperty("train_march_records", out JsonElement trainMarches) ||
            trainMarches.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Camera-free batch is missing its native march snapshots.");

        bool wantsCity = request.SelectedTypes.Contains("city", StringComparer.Ordinal);
        bool wantsResource = request.SelectedTypes.Contains("resource", StringComparer.Ordinal);
        bool wantsDispatch = request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal);
        bool wantsGhost = request.SelectedTypes.Contains("ghost", StringComparer.Ordinal);
        bool wantsTreasure = request.SelectedTypes.Contains("treasure", StringComparer.Ordinal);
        int cityCount = 0, resourceCount = 0;
        foreach (JsonElement point in points.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Camera-free point snapshot included a non-object record.");
            if (!MatchesInt(point, "serverId", request.ServerId) ||
                !MatchesLong(point, "worldId", request.WorldId))
                throw new InvalidDataException("Camera-free point snapshot came from a different server or world.");
            string kind = RequiredString(point, "kind");
            switch (kind)
            {
                case "player_base" when wantsCity: cityCount++; break;
                case "resource_point" when wantsResource: resourceCount++; break;
                case "dispatch_task" when wantsDispatch: break;
                case "ghost_task" when wantsGhost: break;
                case "treasure_point" or "supplies_point" when wantsTreasure: break;
                default: throw new InvalidDataException("Camera-free batch included an unrequested point kind: " + kind);
            }
        }
        byte[] normalizedBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            probeVersion = ProbeVersion,
            capturedAt = capturedAt.ToString("O", CultureInfo.InvariantCulture),
            point_records = points,
        }, JsonOptions.Default);
        string captureSha = Convert.ToHexString(SHA256.HashData(normalizedBytes)).ToLowerInvariant();
        var cities = new List<FirstLivePreparedResource>(cityCount);
        var resources = new List<FirstLivePreparedResource>(resourceCount);
        foreach (JsonElement point in points.EnumerateArray())
        {
            string kind = RequiredString(point, "kind");
            if (kind is not ("player_base" or "resource_point")) continue;
            // The original snapshot importer clones the entire batch once per
            // item. At 160 AOIs that repeatedly reparses hundreds of points.
            // Its same per-record normalization accepts a one-point wrapper;
            // retain the batch hash as the original importer does.
            byte[] one = JsonSerializer.SerializeToUtf8Bytes(new
            {
                probeVersion = ProbeVersion,
                capturedAt = capturedAt.ToString("O", CultureInfo.InvariantCulture),
                point_records = new[] { point },
            }, JsonOptions.Default);
            FirstLivePreparedResource prepared = kind == "player_base"
                ? FirstLiveResultImporter.PrepareOneCity(one, resultPath)
                : FirstLiveResultImporter.PrepareOneResource(one, resultPath);
            prepared = prepared with { Import = prepared.Import with { CaptureSha256 = captureSha } };
            if (kind == "player_base") cities.Add(prepared);
            else resources.Add(prepared);
        }
        if (cities.Count != cityCount || resources.Count != resourceCount)
            throw new InvalidDataException("Camera-free City/Resource record count changed during import.");
        foreach (FirstLivePreparedResource point in cities.Concat(resources))
        {
            if (point.Import.ServerId != request.ServerId ||
                point.Import.X is < 0 or >= 1000 || point.Import.Y is < 0 or >= 1000 ||
                !requestedSet.Contains(point.Import.Y / 10 * 100 + point.Import.X / 10))
                throw new InvalidDataException("Camera-free City/Resource left its requested server or AOI footprint.");
        }
        ResourceCompletenessBatchDiagnostics? completeness = null;
        if (wantsResource)
        {
            if (!batch.TryGetProperty("resourceCompleteness", out JsonElement source) ||
                source.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Camera-free Resource batch omitted native completeness evidence.");
            completeness = ResourceCompletenessBatchDiagnostics.Parse(source);
            if (completeness.AcceptedResourceCount != resourceCount)
                throw new InvalidDataException("Camera-free Resource native accepted rows do not reconcile.");
            RequireResourceNativeSourceCohort(resources, completeness, requestedSet, request.ServerId, request.WorldId);
            if (!batch.TryGetProperty("nativePointTypeHistogram", out JsonElement histogram))
                throw new InvalidDataException("Camera-free Resource batch omitted first native type histogram.");
            RequireResourceFirstAoiTypeSnapshot(histogram, resources, RequireNonNegativeInt(batch, "matchedCount"));
        }
        IReadOnlyList<FastDispatchPrepared> dispatch = wantsDispatch
            ? PrepareDispatchRecords(points, request, requestedSet, capturedAt) : Array.Empty<FastDispatchPrepared>();
        IReadOnlyList<FastGhostPrepared> ghosts = wantsGhost
            ? PrepareGhostRecords(points, request, requestedSet, capturedAt) : Array.Empty<FastGhostPrepared>();
        IReadOnlyList<FastTreasurePrepared> treasures = wantsTreasure
            ? PrepareTreasureRecords(points, request, requestedSet, capturedAt) : Array.Empty<FastTreasurePrepared>();
        IReadOnlyList<FastMonsterPrepared> monsters = IncludesMonsterSource(request)
            ? PrepareMonsterRecords(batch, request, requestedSet,
                RequireCameraFreeMonsterReceipt(batch, capturedAt, terminalCaptureUpperBound)) : Array.Empty<FastMonsterPrepared>();
        IReadOnlyList<FastTrainPrepared> trains = request.SelectedTypes.Contains("railway", StringComparer.Ordinal)
            ? PrepareTrainRecords(batch, request, requestedSet, capturedAt) : Array.Empty<FastTrainPrepared>();
        if (!IncludesMonsterSource(request) && monsterMarches.GetArrayLength() > 0 ||
            !request.SelectedTypes.Contains("railway", StringComparer.Ordinal) && trainMarches.GetArrayLength() > 0)
            throw new InvalidDataException("Camera-free result included unrequested native marches.");

        return new FastCityBatchObservation(indices, cities, resources, dispatch, ghosts, treasures,
            monsters, trains, completeness, 0, 0, 0, 0, null, null, null);
    }

    internal static DateTimeOffset RequireCameraFreeMonsterReceipt(
        JsonElement batch, DateTimeOffset pointCapturedAt, DateTimeOffset terminalCaptureUpperBound)
    {
        if (!batch.TryGetProperty("marchSource", out JsonElement receipt) || receipt.ValueKind != JsonValueKind.Object ||
            !MatchesString(receipt, "source", "WorldMarchDataManager.HandleWorldMarchGetImpl+ParseDataWorldMarchGet") ||
            !MatchesString(receipt, "comparison", "System.DateTime.CompareTo") ||
            !receipt.TryGetProperty("receivedComparedToBeforeSend", out JsonElement receivedValue) ||
            receivedValue.ValueKind != JsonValueKind.Number ||
            !receivedValue.TryGetInt32(out int received) || received != 1 ||
            !receipt.TryGetProperty("handledComparedToReceived", out JsonElement handledValue) ||
            handledValue.ValueKind != JsonValueKind.Number ||
            !handledValue.TryGetInt32(out int handled) || handled is < 0 or > 1 ||
            !MatchesBool(receipt, "receivedAfterSend", true) ||
            !MatchesBool(receipt, "handledAtOrAfterReceived", true) ||
            !receipt.TryGetProperty("waitSeconds", out JsonElement waitValue) ||
            waitValue.ValueKind != JsonValueKind.Number ||
            !waitValue.TryGetDouble(out double waitSeconds) || !double.IsFinite(waitSeconds) || waitSeconds is < 0 or >= 2 ||
            !batch.TryGetProperty("marchCapturedAtUnixMs", out JsonElement capturedValue) ||
            capturedValue.ValueKind != JsonValueKind.Number ||
            !capturedValue.TryGetInt64(out long capturedMs) ||
            capturedMs < pointCapturedAt.ToUnixTimeMilliseconds() ||
            capturedMs > terminalCaptureUpperBound.ToUnixTimeMilliseconds() ||
            capturedMs > pointCapturedAt.ToUnixTimeMilliseconds() + (long)CameraFreeScanTimeout.TotalMilliseconds)
            throw new InvalidDataException(
                "Camera-free Monster source omitted a fresh processed companion March receipt.");
        return DateTimeOffset.FromUnixTimeMilliseconds(capturedMs);
    }

    private static void MergeFullWorldObservation(
        FastFullWorldResumeState state, FastCityBatchObservation observation)
    {
        int before = state.Covered.Count;
        foreach (int index in observation.RequestedIndices) state.Covered.Add(index);
        string shape = observation.RequestedIndices.Select(index => index / FastCityAoiBlockCount).Distinct().Count() +
            "x" + observation.RequestedIndices.Select(index => index % FastCityAoiBlockCount).Distinct().Count();
        state.FootprintHistogram[shape] = state.FootprintHistogram.GetValueOrDefault(shape) + 1;
        state.NewlyCoveredAois += state.Covered.Count - before;
        foreach (FirstLivePreparedResource prepared in observation.Prepared)
            MergeLatestCityByOwner(state.CityRecords, prepared);
        foreach (FirstLivePreparedResource prepared in observation.Resources)
        {
            bool duplicateKey = state.ResourceRecords.TryGetValue(
                prepared.Record.RecordKey, out FirstLivePreparedResource? prior);
            state.ResourceCompleteness.RecordAcceptedMerge(prepared, prior);
            if (!duplicateKey || prepared.Record.UpdatedAt >= prior!.Record.UpdatedAt)
                state.ResourceRecords[prepared.Record.RecordKey] = prepared;
        }
        if (observation.ResourceCompleteness is { } completeness)
            state.ResourceCompleteness.AddBatch(completeness);
        foreach (FastDispatchPrepared prepared in observation.Dispatches)
            if (!state.DispatchRecords.TryGetValue(prepared.Record.RecordKey, out FastDispatchPrepared? prior) ||
                prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                state.DispatchRecords[prepared.Record.RecordKey] = prepared;
        foreach (FastGhostPrepared prepared in observation.Ghosts)
            if (!state.GhostRecords.TryGetValue(prepared.Record.RecordKey, out FastGhostPrepared? prior) ||
                prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                state.GhostRecords[prepared.Record.RecordKey] = prepared;
        foreach (FastTreasurePrepared prepared in observation.Treasures)
            if (!state.TreasureRecords.TryGetValue(prepared.Record.RecordKey, out FastTreasurePrepared? prior) ||
                prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                state.TreasureRecords[prepared.Record.RecordKey] = prepared;
        foreach (FastMonsterPrepared prepared in observation.Monsters)
            if (!state.MonsterRecords.TryGetValue(prepared.Record.RecordKey, out FastMonsterPrepared? prior) ||
                prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                state.MonsterRecords[prepared.Record.RecordKey] = prepared;
        foreach (FastTrainPrepared prepared in observation.Trains)
            if (!state.TrainRecords.TryGetValue(prepared.Record.RecordKey, out FastTrainPrepared? prior) ||
                prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                state.TrainRecords[prepared.Record.RecordKey] = prepared;
    }
}
