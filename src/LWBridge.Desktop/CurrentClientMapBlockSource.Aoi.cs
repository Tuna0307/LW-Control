using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    private async Task<AoiCellCaptureSummary> CaptureCurrentViewAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        string mapKind,
        MapScanTargetBlock block,
        CurrentClientAoiCell cell,
        IDictionary<string, MapStoredRecord> records,
        CancellationToken cancellationToken)
    {
        var selectedIndices = new HashSet<int>();
        ProbeObservation snapshot = await ProbeOnceAsync(
            session, request, mapKind, cancellationToken).ConfigureAwait(false);

        if (snapshot.IsEmptyView)
            return new AoiCellCaptureSummary(cell, 0, Array.Empty<int>());

        AddObservation(snapshot, mapKind, block, records, selectedIndices);
        return new AoiCellCaptureSummary(
            cell,
            snapshot.MatchedCount,
            selectedIndices.OrderBy(value => value).ToArray());
    }

    private static MapScanBlockCapture BuildCapture(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        NavigationObservation initialNavigation,
        CurrentClientAoiCoveragePlan plan,
        IReadOnlyList<AoiCellCaptureSummary> cellSummaries,
        IEnumerable<MapStoredRecord> records)
    {
        MapStoredRecord[] accepted = records.ToArray();
        string payload = JsonSerializer.Serialize(new
        {
            protocol = "current_overview_probe_aoi_block_v2",
            blockIndex = block.BlockIndex,
            minX = block.MinX,
            minY = block.MinY,
            maxX = block.MaxX,
            maxY = block.MaxY,
            currentLod = initialNavigation.CurrentLod,
            serverLod = plan.ServerLod,
            aoiBlockSize = plan.BlockSize,
            aoiBlockSizes = initialNavigation.AoiBlockSizes,
            cells = cellSummaries.Select(summary => new
            {
                summary.Cell.CellX,
                summary.Cell.CellY,
                summary.Cell.MinX,
                summary.Cell.MinY,
                summary.Cell.MaxX,
                summary.Cell.MaxY,
                summary.Cell.TargetX,
                summary.Cell.TargetY,
                summary.MatchedCount,
                summary.SelectedIndices,
            }).ToArray(),
            recordsInBlock = accepted.Length,
            coverage = "planned_aoi_cells_pending_live_footprint_proof",
        }, JsonOptions.Default);

        return new MapScanBlockCapture(
            request.ServerId,
            request.WorldId,
            block.BlockIndex,
            payload,
            accepted);
    }

}

internal sealed record NavigationObservation(
    int CurrentLod,
    int ServerLod,
    int[] AoiBlockSizes);

internal sealed record AoiCellCaptureSummary(
    CurrentClientAoiCell Cell,
    int MatchedCount,
    int[] SelectedIndices);

internal sealed partial class CurrentClientMapBlockSource
{
    private static int RequireNonNegativeInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) ||
            !value.TryGetInt32(out int result) || result < 0)
        {
            throw new InvalidDataException($"Map navigation field '{name}' must be a non-negative integer.");
        }
        return result;
    }

    private static int[] RequirePositiveIntArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Map navigation field '{name}' must be an integer array.");
        }

        var values = new List<int>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (!item.TryGetInt32(out int parsed) || parsed <= 0)
                throw new InvalidDataException($"Map navigation field '{name}' contains an invalid AOI block size.");
            values.Add(parsed);
        }
        if (values.Count == 0)
            throw new InvalidDataException($"Map navigation field '{name}' must not be empty.");
        return values.ToArray();
    }
}
