using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapScanTraversalChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        IReadOnlyList<MapScanTargetBlock> single = MapScanTraversal.Build(20, 20);
        Require(single.Count == 1, "20x20 traversal should contain one block");
        Require(single[0] == new MapScanTargetBlock(0, 0, 0, 0, 0, 19, 19, 9, 9),
            "20x20 traversal block changed");

        IReadOnlyList<MapScanTargetBlock> partial = MapScanTraversal.Build(21, 41);
        Require(partial.Count == 6, "21x41 traversal should contain six blocks");
        Require(partial[0].BlockIndex == 0 && partial[0].Column == 0 && partial[0].Row == 0,
            "traversal no longer begins row-major");
        Require(partial[1].BlockIndex == 1 && partial[1].Column == 1 && partial[1].Row == 0,
            "traversal no longer advances columns first");
        Require(partial[2].BlockIndex == 2 && partial[2].Column == 0 && partial[2].Row == 1,
            "traversal no longer advances to the next row");

        MapScanTargetBlock last = partial[^1];
        Require(last == new MapScanTargetBlock(5, 1, 2, 20, 40, 20, 40, 20, 40),
            "partial edge block is not clamped to the map bounds");

        IReadOnlyList<MapScanTargetBlock> oneTile = MapScanTraversal.Build(1, 1);
        Require(oneTile.Count == 1 && oneTile[0].TargetX == 0 && oneTile[0].TargetY == 0,
            "single-tile traversal changed");

        try
        {
            MapScanTraversal.Build((long)int.MaxValue + 1, 1);
        }
        catch (BridgeCommandException error) when (
            error.Code == "MAP_SIZE_UNAVAILABLE" &&
            error.Message == "world map dimensions are unavailable")
        {
            return;
        }

        throw new InvalidOperationException("oversized traversal should fail closed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
