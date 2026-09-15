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

    private async Task<int> CaptureCityFootprintAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        NavigationObservation initialNavigation,
        CurrentClientAoiCoveragePlan plan,
        IDictionary<string, MapStoredRecord> records,
        List<AoiCellCaptureSummary> cellSummaries,
        CancellationToken cancellationToken)
    {
        var covered = new HashSet<(int X, int Y)>();
        int? expectedBlockCount = null;
        int responseCount = 0;
        NavigationObservation navigation = initialNavigation;
        while (covered.Count < plan.Cells.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStableAoiGeometry(navigation, initialNavigation, plan);
            RequireSameSession(session);
            ProbeObservation snapshot = await ProbeOnceAsync(
                session, request, "city", cancellationToken).ConfigureAwait(false);
            responseCount++;
            if (snapshot.CityTargetedView)
                throw new InvalidDataException("Player City block acquisition left the requested current view.");
            if (snapshot.AoiBlockSize != plan.BlockSize || snapshot.AoiBlockCount <= 0)
                throw new InvalidDataException("Player City response did not preserve the requested AOI geometry.");
            expectedBlockCount ??= snapshot.AoiBlockCount;
            if (snapshot.AoiBlockCount != expectedBlockCount.Value)
                throw new InvalidDataException("Player City AOI block count changed during acquisition.");

            var footprint = snapshot.CurrentViewIndices.ToHashSet();
            CurrentClientAoiCell[] newlyCovered = plan.Cells
                .Where(cell => !covered.Contains((cell.CellX, cell.CellY)) &&
                    footprint.Contains(checked((cell.CellY * snapshot.AoiBlockCount) + cell.CellX)))
                .ToArray();
            if (newlyCovered.Length == 0)
                throw new InvalidDataException("Player City fresh response did not cover any pending AOI cell.");

            int[] selectedIndices = Array.Empty<int>();
            if (!snapshot.IsEmptyView)
            {
                var selected = new HashSet<int>();
                AddObservation(snapshot, "city", block, records, selected);
                selectedIndices = selected.OrderBy(value => value).ToArray();
            }
            foreach (CurrentClientAoiCell cell in newlyCovered)
            {
                covered.Add((cell.CellX, cell.CellY));
                cellSummaries.Add(new AoiCellCaptureSummary(
                    cell, snapshot.MatchedCount, selectedIndices));
            }
            if (covered.Count == plan.Cells.Count) break;

            CurrentClientAoiCell next = plan.Cells.First(cell => !covered.Contains((cell.CellX, cell.CellY)));
            navigation = await NavigateAsync(
                session, request.ServerId, request.WorldId, next.TargetX, next.TargetY, cancellationToken)
                .ConfigureAwait(false);
        }

        var order = plan.Cells.Select((cell, index) => ((cell.CellX, cell.CellY), index))
            .ToDictionary(item => item.Item1, item => item.index);
        cellSummaries.Sort((left, right) =>
            order[(left.Cell.CellX, left.Cell.CellY)].CompareTo(order[(right.Cell.CellX, right.Cell.CellY)]));
        return responseCount;
    }

    private static void ValidateStableAoiGeometry(
        NavigationObservation navigation,
        NavigationObservation initialNavigation,
        CurrentClientAoiCoveragePlan plan)
    {
        if (navigation.ServerLod != plan.ServerLod ||
            !navigation.AoiBlockSizes.SequenceEqual(initialNavigation.AoiBlockSizes))
            throw new InvalidDataException("Current-client AOI geometry changed during map acquisition.");
    }

    private static MapScanBlockCapture BuildCapture(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        NavigationObservation initialNavigation,
        CurrentClientAoiCoveragePlan plan,
        IReadOnlyList<AoiCellCaptureSummary> cellSummaries,
        IEnumerable<MapStoredRecord> records,
        int? responseCount)
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
            responseCount,
            coverage = request.SelectedTypes.Count == 1 && request.SelectedTypes[0] == "city"
                ? "live_cur_view_index_covered"
                : "planned_aoi_cells_pending_live_footprint_proof",
            footprintSource = request.SelectedTypes.Count == 1 && request.SelectedTypes[0] == "city"
                ? "WorldPointManager._curViewIndex"
                : null,
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

    private static int[] RequireNonNegativeIntArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Map probe field '{name}' must be an integer array.");
        var values = new List<int>();
        var seen = new HashSet<int>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (!item.TryGetInt32(out int parsed) || parsed < 0 || !seen.Add(parsed))
                throw new InvalidDataException($"Map probe field '{name}' contains an invalid or duplicate AOI index.");
            values.Add(parsed);
        }
        return values.ToArray();
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
