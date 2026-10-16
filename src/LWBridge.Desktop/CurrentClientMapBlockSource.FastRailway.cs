using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    private async Task<IReadOnlyList<FastTrainPrepared>> CaptureFastRailwayRowsAsync(
        OverviewMapScanSession session, MapScanExecutionRequest request, CancellationToken cancellationToken)
    {
        if (request.WorldId != 0 || request.LiveServerId is int live && live != request.ServerId)
            throw new InvalidDataException("Fast Railway requires the current normal-world server.");
        JsonElement response = await ProbeFastTruckAsync(
            session, request, 20, cancellationToken, railwayOnly: true).ConfigureAwait(false);
        if (MatchesString(response, "state", "failed"))
            throw new InvalidDataException("Fast Railway native sweep failed: " +
                (ReadOptionalString(response, "error") ?? "native_source_failed"));
        DateTimeOffset capturedAt = DateTimeOffset.Parse(
            RequiredString(response, "capturedAt"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        IReadOnlyList<FastTrainPrepared> rows = PrepareFastRailwayRows(
            response, request, capturedAt, railwayOnly: true);
        RequireSameSession(session);
        return rows;
    }

    private static IReadOnlyList<FastTrainPrepared> PrepareFastRailwayRows(
        JsonElement response, MapScanExecutionRequest request, DateTimeOffset capturedAt, bool railwayOnly)
    {
        // The official enemy-train recommendation has no requested server
        // argument and is not a census. Only this complete native rectangle
        // receipt may establish a positive or genuinely empty Railway result.
        if (!MatchesString(response, "state", "proven") ||
            !MatchesBool(response, "railwayIncluded", true) ||
            !MatchesBool(response, "railwayOnly", railwayOnly) ||
            !MatchesInt(response, "initialRequestsSent", 625) ||
            !MatchesInt(response, "rectanglesCompleted", 625) ||
            !MatchesInt(response, "unprovenRectangles", 0) ||
            !MatchesInt(response, "requestsPerSecond", 20) ||
            !MatchesInt(response, "responseFenceSize", 25) ||
            !MatchesString(response, "responseAttribution", "bounded_sequential_fences") ||
            !response.TryGetProperty("repairPasses", out JsonElement passes) ||
            !passes.TryGetInt32(out int passCount) || passCount is < 0 or > 2 ||
            !response.TryGetProperty("requestsSent", out JsonElement sent) ||
            !sent.TryGetInt32(out int totalSent) || totalSent < 625 ||
            !MatchesInt(response, "repairRequestsSent", totalSent - 625) ||
            !response.TryGetProperty("pushesObserved", out JsonElement pushes) ||
            !pushes.TryGetInt32(out int observed) || observed < 625 || observed > totalSent ||
            !response.TryGetProperty("railwayRecords", out JsonElement nativeRows) ||
            nativeRows.ValueKind != JsonValueKind.Array ||
            !MatchesInt(response, "railwayRecordCount", nativeRows.GetArrayLength()))
            throw new InvalidDataException("Fast Railway did not prove all 625 rectangles and its typed Railway snapshot.");

        foreach (JsonElement row in nativeRows.EnumerateArray())
        {
            if (!MatchesInt(row, "trainType", 2) ||
                !row.TryGetProperty("capturedAtMs", out JsonElement time) ||
                !time.TryGetInt64(out long observedAt) || observedAt <= 0 ||
                observedAt < capturedAt.ToUnixTimeMilliseconds() - 240_000 ||
                observedAt > capturedAt.ToUnixTimeMilliseconds() + 2_000 ||
                !MatchesString(row, "source", "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train"))
                throw new InvalidDataException("Fast Railway row has missing type, source or observation time.");
        }
        JsonElement wrapped = JsonSerializer.SerializeToElement(
            new { train_march_records = nativeRows }, JsonOptions.Default);
        MapScanExecutionRequest railwayRequest = request with { SelectedTypes = ["railway"] };
        IReadOnlyList<FastTrainPrepared> prepared = PrepareTrainRecords(
            wrapped, railwayRequest, Enumerable.Range(0, 10000).ToHashSet(), capturedAt);
        if (prepared.Count != nativeRows.GetArrayLength() ||
            prepared.Select(item => item.Record.RecordKey).Distinct(StringComparer.Ordinal).Count() != prepared.Count)
            throw new InvalidDataException("Fast Railway returned duplicate or unclassifiable Railway identities.");
        return prepared.Select(item =>
        {
            JsonObject data = JsonNode.Parse(item.Record.DataJson)!.AsObject();
            data["captureProtocol"] = "current_fast_railway_625_v1";
            return item with { Record = item.Record with { DataJson = data.ToJsonString(JsonOptions.Default) } };
        }).ToArray();
    }

    private static IReadOnlyList<MapScanBlockCapture> ProjectFastMobileTrains(
        MapScanExecutionRequest request, IReadOnlySet<int> pendingBlockIndices,
        IReadOnlyList<FastTrainPrepared> trains, Action<MapScanSourceProgress>? progress)
    {
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(request.TileWidth, request.TileHeight);
        if (blocks.Count != 2500 || pendingBlockIndices.Count != 2500 ||
            blocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("Fast mobile Train projection requires the complete 2,500-block map.");
        var byBlock = new Dictionary<int, List<MapStoredRecord>>();
        foreach (FastTrainPrepared raw in trains)
        {
            if (raw.Record.Kind is not ("truck" or "railway") ||
                !request.SelectedTypes.Contains(raw.Record.Kind, StringComparer.Ordinal) ||
                raw.X is < 0 or >= 1000 || raw.Y is < 0 or >= 1000)
                throw new InvalidDataException("Fast mobile Train projection encountered an invalid kind or position.");
            if (raw.Record.Kind == "truck" &&
                !FastTruckArrivalIsAfter(raw.Record, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) continue;
            FastTrainPrepared item = raw.Record.Kind == "truck" ? ApplyFinalTruckMetadataEnrichment(raw) : raw;
            int blockIndex = (raw.Y / MapScanGeometry.RecoveredBlockSpan) * 50 +
                             raw.X / MapScanGeometry.RecoveredBlockSpan;
            if (!byBlock.TryGetValue(blockIndex, out List<MapStoredRecord>? bucket))
                byBlock[blockIndex] = bucket = [];
            bucket.Add(item.Record);
        }
        progress?.Invoke(new MapScanSourceProgress(90));
        var captures = new List<MapScanBlockCapture>(2500);
        foreach (MapScanTargetBlock block in blocks)
        {
            IReadOnlyList<MapStoredRecord> records = byBlock.TryGetValue(block.BlockIndex, out var bucket)
                ? bucket : Array.Empty<MapStoredRecord>();
            string payload = JsonSerializer.Serialize(new
            {
                protocol = "current_fast_mobile_trains_625_v1", blockIndex = block.BlockIndex,
                recordsInBlock = records.Count, rectangles = 625,
                railwayCoverage = "live_40x40_rectangle_march_pushes",
            }, JsonOptions.Default);
            captures.Add(new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, payload, records));
        }
        progress?.Invoke(new MapScanSourceProgress(100));
        return captures;
    }
}
