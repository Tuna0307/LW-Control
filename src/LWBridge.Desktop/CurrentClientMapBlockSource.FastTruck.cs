using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    // One exact 625-rectangle send pass at 15/s plus the bounded native
    // post-send drain remains below 90 s in observed live trials. A missing
    // terminal receipt must cancel and prove native restoration before any
    // camera fallback can begin.
    private static readonly TimeSpan FastTruckSweepTimeout = TimeSpan.FromSeconds(90);

    private async Task<IReadOnlyList<FastTrainPrepared>> CaptureFastTruckRowsAsync(
        OverviewMapScanSession session, MapScanExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorldId != 0 || request.LiveServerId is int live && live != request.ServerId)
            throw new InvalidDataException("Fast Truck requires the current normal-world server.");

        // The native owner runs at 20/s. A lost push means the entire sweep
        // was rejected; attempt exactly one fresh 15/s sweep, then let the
        // caller use the existing camera route.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int rate = attempt == 0 ? 20 : 15;
            JsonElement response = await ProbeFastTruckAsync(session, request, rate, cancellationToken)
                .ConfigureAwait(false);
            if (MatchesString(response, "state", "failed"))
            {
                string cause = ReadOptionalString(response, "error") ?? "native_source_failed";
                if (cause == "lost_march_push" && attempt == 0)
                {
                    RequireSameSession(session);
                    Console.Error.WriteLine($"FAST_TRUCK_PUSH_RETRY server={request.ServerId} run={request.RunId} rate=15");
                    continue;
                }
                throw new InvalidDataException("Fast Truck native sweep failed: " + cause);
            }
            if (!MatchesString(response, "state", "proven") ||
                !MatchesInt(response, "requestsSent", 625) ||
                !MatchesInt(response, "pushesObserved", 625) ||
                !MatchesInt(response, "rectanglesCompleted", 625) ||
                !MatchesInt(response, "requestsPerSecond", rate) ||
                !response.TryGetProperty("trainRecords", out JsonElement rows) ||
                rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0 ||
                !MatchesInt(response, "trainRecordCount", rows.GetArrayLength()))
                throw new InvalidDataException("Fast Truck native sweep did not prove 625 unique pushes and positive typed rows.");

            foreach (JsonElement row in rows.EnumerateArray())
            {
                // The authoritative builder is train_march_aoi_records, exactly
                // as for a camera sweep. Unknown/reclassified trains cannot
                // enter Truck publication, and incomplete native metadata
                // must force a camera scan rather than changing row fields.
                if (!MatchesInt(row, "trainType", 1) ||
                    !MatchesBool(row, "truckMetadataKnown", true) ||
                    !row.TryGetProperty("trainQuality", out JsonElement quality) ||
                    !quality.TryGetInt32(out int qualityValue) || qualityValue < 1 ||
                    !row.TryGetProperty("carriageNum", out JsonElement carriage) ||
                    !carriage.TryGetInt32(out int carriageValue) || carriageValue < 0 ||
                    string.IsNullOrWhiteSpace(OptionalStringValue(row, "ownerName")) ||
                    OptionalPositiveInt64(row, "arriveTs") is null ||
                    !row.TryGetProperty("power", out JsonElement power) ||
                    !power.TryGetInt64(out long powerValue) || powerValue < 0 ||
                    !row.TryGetProperty("truckCurrentGoodsRaw", out JsonElement rewards) ||
                    rewards.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Fast Truck row lacks production owner, reward, power or arrival metadata.");
            }
            DateTimeOffset capturedAt = DateTimeOffset.Parse(
                RequiredString(response, "capturedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            JsonElement wrapped = JsonSerializer.SerializeToElement(
                new { train_march_records = rows }, JsonOptions.Default);
            MapScanExecutionRequest truckRequest = request with { SelectedTypes = ["truck"] };
            IReadOnlyList<FastTrainPrepared> prepared = PrepareTrainRecords(
                wrapped, truckRequest, Enumerable.Range(0, 10000).ToHashSet(), capturedAt);
            if (prepared.Count == 0 || prepared.Count != rows.GetArrayLength() ||
                prepared.Select(item => item.Record.RecordKey).Distinct(StringComparer.Ordinal).Count() != prepared.Count)
                throw new InvalidDataException("Fast Truck producer returned duplicate, empty or unclassifiable Truck rows.");
            RequireSameSession(session);
            return prepared;
        }
        throw new InvalidDataException("Fast Truck 15/s retry did not complete.");
    }

    private async Task<JsonElement> ProbeFastTruckAsync(
        OverviewMapScanSession session, MapScanExecutionRequest request,
        int rate, CancellationToken cancellationToken)
    {
        string requestId = "truck" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "fast-truck-scan.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "fast-truck-scan-result.json");
        string cancelPath = Path.Combine(probeRuntimeRoot, "fast-truck-scan-cancel.txt");
        string command = string.Join('\n', new[]
        {
            "schema=1", $"probeVersion={ProbeVersion}", $"requestId={requestId}",
            $"profileId={session.ProfileId}", $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"rate={rate.ToString(CultureInfo.InvariantCulture)}", string.Empty,
        });
        DateTimeOffset started = Now();
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        bool terminal = false;
        try
        {
            while (Now() < started + FastTruckSweepTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JsonElement? received = TryReadJson(resultPath);
                if (received is JsonElement root && MatchesString(root, "requestId", requestId))
                {
                    // A native failure is eligible for camera fallback only
                    // after this exact request has restored AOI flags/memberships
                    // and the original physical camera tile.
                    if (!MatchesBool(root, "cameraStable", true) ||
                        !MatchesBool(root, "curViewRestored", true))
                        throw new MapScanSourceValidationException(
                            "Fast Truck producer did not restore camera/AOI state; camera fallback withheld.");
                    ValidateCameraFreeScanOwner(root, session, request, requestId, started);
                    terminal = true;
                    return root;
                }
                await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException("Fast Truck sweep did not deliver a correlated native terminal receipt.");
        }
        finally
        {
            if (!terminal)
            {
                await WriteCommandAsync(cancelPath, "requestId=" + requestId + "\n", CancellationToken.None)
                    .ConfigureAwait(false);
                await RequireCameraFreeCancelReceiptAsync(resultPath, requestId, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    private static IReadOnlyList<MapScanBlockCapture> ProjectFastTruckOnly(
        MapScanExecutionRequest request, IReadOnlySet<int> pendingBlockIndices,
        IReadOnlyList<FastTrainPrepared> trucks, Action<MapScanSourceProgress>? progress)
    {
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(request.TileWidth, request.TileHeight);
        if (blocks.Count != 2500 || pendingBlockIndices.Count != 2500 ||
            blocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("Fast Truck projection requires the complete 2,500-block map.");
        var byBlock = new Dictionary<int, List<MapStoredRecord>>();
        foreach (FastTrainPrepared raw in trucks)
        {
            if (raw.Record.Kind != "truck" || raw.X is < 0 or >= 1000 || raw.Y is < 0 or >= 1000)
                throw new InvalidDataException("Fast Truck projection encountered an invalid typed position.");
            FastTrainPrepared item = ApplyFinalTruckMetadataEnrichment(raw);
            int blockIndex = (raw.Y / MapScanGeometry.RecoveredBlockSpan) * 50 +
                             raw.X / MapScanGeometry.RecoveredBlockSpan;
            if (!byBlock.TryGetValue(blockIndex, out List<MapStoredRecord>? bucket))
                byBlock[blockIndex] = bucket = [];
            bucket.Add(item.Record);
        }
        if (byBlock.Count == 0) throw new InvalidDataException("Fast Truck had zero usable records.");
        progress?.Invoke(new MapScanSourceProgress(90));
        var captures = new List<MapScanBlockCapture>(2500);
        foreach (MapScanTargetBlock block in blocks)
        {
            IReadOnlyList<MapStoredRecord> records = byBlock.TryGetValue(block.BlockIndex, out var bucket)
                ? bucket : Array.Empty<MapStoredRecord>();
            string payload = JsonSerializer.Serialize(new
            {
                protocol = "current_fast_truck_625_v1", blockIndex = block.BlockIndex,
                recordsInBlock = records.Count, source = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
                rectangles = 625, nativePushes = 625, coverage = "live_40x40_rectangle_march_pushes",
            }, JsonOptions.Default);
            captures.Add(new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, payload, records));
        }
        progress?.Invoke(new MapScanSourceProgress(100));
        return captures;
    }
}
