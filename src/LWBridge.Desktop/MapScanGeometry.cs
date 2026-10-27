namespace LWBridge.Desktop;

internal readonly record struct MapScanBlockGrid(long Columns, long Rows, long TotalBlocks);

internal static class MapScanGeometry
{
    public const int RecoveredBlockSpan = 20;

    public static MapScanBlockGrid FromTileDimensions(long tileWidth, long tileHeight)
    {
        if (tileWidth <= 0 || tileHeight <= 0)
            throw new BridgeCommandException("MAP_SIZE_UNAVAILABLE", "world map dimensions are unavailable");

        long columns = 1 + ((tileWidth - 1) / RecoveredBlockSpan);
        long rows = 1 + ((tileHeight - 1) / RecoveredBlockSpan);
        return new MapScanBlockGrid(columns, rows, checked(columns * rows));
    }
}
