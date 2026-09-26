using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class CurrentClientAoiCoveragePlannerChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        LodZeroSplitsTwentyTileBlockIntoFourCells();
        LodOneMatchesTwentyTileBlock();
        LodTwoProducesOneSupersetCell();
        PartialWorldEdgeIsClamped();
        UnexpectedRuntimeGeometryFailsClosed();
    }

    private static MapScanExecutionRequest Request(long width = 40, long height = 40) =>
        new("run_aoi", 2212, 7, width, height, ["resource"], 8, 2);

    private static MapScanTargetBlock Block(int minX = 0, int minY = 0, int maxX = 19, int maxY = 19) =>
        new(0, 0, 0, minX, minY, maxX, maxY,
            minX + ((maxX - minX) / 2),
            minY + ((maxY - minY) / 2));

    private static void LodZeroSplitsTwentyTileBlockIntoFourCells()
    {
        CurrentClientAoiCoveragePlan plan = CurrentClientAoiCoveragePlanner.Build(
            Request(), Block(), 0, [10, 20, 1000]);        Check(plan.BlockSize == 10 && plan.Cells.Count == 4,
            "server LOD 0 should cover one recovered scan block with four 10-tile AOI cells");
        Check(plan.Cells.Select(cell => (cell.MinX, cell.MinY, cell.MaxX, cell.MaxY)).SequenceEqual(new[]
        {
            (0, 0, 9, 9),
            (10, 0, 19, 9),
            (0, 10, 9, 19),
            (10, 10, 19, 19),
        }), "server LOD 0 AOI cell bounds changed");
    }

    private static void LodOneMatchesTwentyTileBlock()
    {
        CurrentClientAoiCoveragePlan plan = CurrentClientAoiCoveragePlanner.Build(
            Request(), Block(), 1, [10, 20, 1000]);
        Check(plan.BlockSize == 20 && plan.Cells.Count == 1,
            "server LOD 1 should map one recovered scan block to one 20-tile AOI cell");
        CurrentClientAoiCell cell = plan.Cells[0];
        Check((cell.MinX, cell.MinY, cell.MaxX, cell.MaxY, cell.TargetX, cell.TargetY) == (0, 0, 19, 19, 9, 9),
            "server LOD 1 AOI cell should exactly match the recovered scan block");
    }

    private static void LodTwoProducesOneSupersetCell()
    {
        CurrentClientAoiCoveragePlan plan = CurrentClientAoiCoveragePlanner.Build(
            Request(), Block(), 2, [10, 20, 1000]);
        Check(plan.BlockSize == 1000 && plan.Cells.Count == 1,
            "server LOD 2 should place the recovered scan block inside one larger AOI cell");        CurrentClientAoiCell cell = plan.Cells[0];
        Check((cell.MinX, cell.MinY, cell.MaxX, cell.MaxY) == (0, 0, 39, 39),
            "server LOD 2 AOI cell should be clamped to the declared world dimensions");
    }

    private static void PartialWorldEdgeIsClamped()
    {
        CurrentClientAoiCoveragePlan plan = CurrentClientAoiCoveragePlanner.Build(
            Request(21, 21), Block(20, 20, 20, 20), 1, [10, 20, 1000]);
        Check(plan.Cells.Count == 1,
            "partial edge scan block should still map to one intersecting AOI cell");
        CurrentClientAoiCell cell = plan.Cells[0];
        Check((cell.MinX, cell.MinY, cell.MaxX, cell.MaxY, cell.TargetX, cell.TargetY) == (20, 20, 20, 20, 20, 20),
            "partial edge AOI cell must be clamped to the world edge");
    }

    private static void UnexpectedRuntimeGeometryFailsClosed()
    {
        try
        {
            _ = CurrentClientAoiCoveragePlanner.Build(Request(), Block(), 1, [10, 21, 1000]);
            throw new InvalidOperationException("unexpected current-client AOI geometry should fail closed");
        }
        catch (InvalidDataException error) when (
            error.Message == "Current-client AOI block-size array does not match the recovered supported client.")
        {
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}