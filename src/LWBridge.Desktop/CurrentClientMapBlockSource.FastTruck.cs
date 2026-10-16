using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    // Each 625-rectangle sweep now fences sends and has up to two selective
    // single-outstanding repair passes. The native owner imposes its own
    // bounded deadline and must prove restoration on failure or cancellation.
    private static readonly TimeSpan FastTruckSweepTimeout = TimeSpan.FromSeconds(240);

    private async Task<IReadOnlyList<FastTrainPrepared>> CaptureFastTruckRowsAsync(
        OverviewMapScanSession session, MapScanExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorldId != 0 || request.LiveServerId is int live && live != request.ServerId)
            throw new InvalidDataException("Fast Truck requires the current normal-world server.");

        // One 20/s native sweep performs at most two repair passes over its
        // unverified rectangles. A terminal native loss is eligible for the
        // established camera fallback after exact owned cleanup.
        {
            const int rate = 20;
            JsonElement response = await ProbeFastTruckAsync(session, request, rate, cancellationToken)
                .ConfigureAwait(false);
            if (MatchesString(response, "state", "failed"))
            {
                string cause = ReadOptionalString(response, "error") ?? "native_source_failed";
                throw new InvalidDataException("Fast Truck native sweep failed: " + cause);
            }
            if (!MatchesString(response, "state", "proven") ||
                !MatchesInt(response, "initialRequestsSent", 625) ||
                !MatchesInt(response, "rectanglesCompleted", 625) ||
                !MatchesInt(response, "requestsPerSecond", rate) ||
                !MatchesInt(response, "unprovenRectangles", 0) ||
                !MatchesString(response, "responseAttribution", "bounded_sequential_fences") ||
                !MatchesInt(response, "responseFenceSize", 25) ||
                !response.TryGetProperty("repairPasses", out JsonElement passes) ||
                !passes.TryGetInt32(out int passCount) || passCount is < 0 or > 2 ||
                !response.TryGetProperty("requestsSent", out JsonElement sent) ||
                !sent.TryGetInt32(out int totalSent) || totalSent < 625 ||
                !response.TryGetProperty("pushesObserved", out JsonElement pushes) ||
                !pushes.TryGetInt32(out int observed) || observed < 625 || observed > totalSent ||
                !MatchesInt(response, "repairRequestsSent", totalSent - 625) ||
                !response.TryGetProperty("trainRecords", out JsonElement rows) ||
                rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0 ||
                !MatchesInt(response, "trainRecordCount", rows.GetArrayLength()))
                throw new InvalidDataException("Fast Truck native sweep did not prove 625 unique pushes and positive typed rows.");

            var currentRows = new List<JsonElement>(rows.GetArrayLength());
            long receiptTimeMs = Now().ToUnixTimeMilliseconds();
            foreach (JsonElement row in rows.EnumerateArray())
            {
                // Native checks at each capture frame. The receipt can arrive
                // tens of seconds later, so repeat the arrival check before
                // importing anything that could reach the published map.
                if (!FastTruckArrivalIsAfter(row, receiptTimeMs)) continue;
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
                currentRows.Add(row);
            }
            DateTimeOffset capturedAt = DateTimeOffset.Parse(
                RequiredString(response, "capturedAt"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            JsonElement wrapped = JsonSerializer.SerializeToElement(
                new { train_march_records = currentRows }, JsonOptions.Default);
            MapScanExecutionRequest truckRequest = request with { SelectedTypes = ["truck"] };
            IReadOnlyList<FastTrainPrepared> prepared = PrepareTrainRecords(
                wrapped, truckRequest, Enumerable.Range(0, 10000).ToHashSet(), capturedAt,
                receiptTimeMs);
            if (prepared.Count == 0 || prepared.Count != currentRows.Count ||
                prepared.Select(item => item.Record.RecordKey).Distinct(StringComparer.Ordinal).Count() != prepared.Count)
                throw new InvalidDataException("Fast Truck producer returned duplicate, empty or unclassifiable Truck rows.");
            RequireSameSession(session);
            return prepared;
        }
    }

    // Both native march.endTime and TrainData.arriveTs are possible arrival
    // deadlines. Source generations have used Unix seconds and milliseconds;
    // reject a Truck when either positive deadline has passed.
    internal static bool FastTruckArrivalIsAfter(JsonElement row, long nowUnixMs)
    {
        foreach (string field in new[] { "endTime", "arriveTs" })
        {
            if (!row.TryGetProperty(field, out JsonElement value)) continue;
            long? raw = value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number)
                ? number : value.ValueKind == JsonValueKind.String &&
                  long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                    ? parsed : null;
            if (raw is not > 0) continue;
            long arrivalMs = raw.Value < 100_000_000_000 ? checked(raw.Value * 1000) : raw.Value;
            if (arrivalMs <= nowUnixMs) return false;
        }
        return true;
    }

    private static bool FastTruckArrivalIsAfter(MapStoredRecord record, long nowUnixMs)
    {
        using JsonDocument document = JsonDocument.Parse(record.DataJson);
        return FastTruckArrivalIsAfter(document.RootElement, nowUnixMs);
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
            if (!FastTruckArrivalIsAfter(raw.Record, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) continue;
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
