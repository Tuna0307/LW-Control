namespace LWBridge.Desktop;

internal readonly record struct MapScanTargetBlock(
    int BlockIndex,
    int Column,
    int Row,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int TargetX,
    int TargetY);

internal static class MapScanTraversal
{
    // IMPLEMENTATION POLICY: use the recovered 20-tile cardinality as a
    // deterministic covering grid and target the clamped center of each block.
    // Live proof must still establish the current client's response footprint.
    public static IReadOnlyList<MapScanTargetBlock> Build(long tileWidth, long tileHeight)
    {
        MapScanBlockGrid grid = MapScanGeometry.FromTileDimensions(tileWidth, tileHeight);
        if (tileWidth > int.MaxValue || tileHeight > int.MaxValue || grid.TotalBlocks > int.MaxValue)
            throw new BridgeCommandException("MAP_SIZE_UNAVAILABLE", "world map dimensions are unavailable");

        var result = new List<MapScanTargetBlock>(checked((int)grid.TotalBlocks));
        int width = checked((int)tileWidth);
        int height = checked((int)tileHeight);
        int columns = checked((int)grid.Columns);
        int rows = checked((int)grid.Rows);

        for (int row = 0; row < rows; row++)
        {
            int minY = checked(row * MapScanGeometry.RecoveredBlockSpan);
            int maxY = Math.Min(height - 1, minY + MapScanGeometry.RecoveredBlockSpan - 1);
            int targetY = minY + ((maxY - minY) / 2);
            for (int column = 0; column < columns; column++)
            {
                int minX = checked(column * MapScanGeometry.RecoveredBlockSpan);
                int maxX = Math.Min(width - 1, minX + MapScanGeometry.RecoveredBlockSpan - 1);
                int targetX = minX + ((maxX - minX) / 2);
                int blockIndex = checked(row * columns + column);
                result.Add(new MapScanTargetBlock(
                    blockIndex,
                    column,
                    row,
                    minX,
                    minY,
                    maxX,
                    maxY,
                    targetX,
                    targetY));
            }
        }

        return result;
    }
}
