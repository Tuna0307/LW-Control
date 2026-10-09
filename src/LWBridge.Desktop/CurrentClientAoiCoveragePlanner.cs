namespace LWBridge.Desktop;

internal readonly record struct CurrentClientAoiCell(
    int ServerLod,
    int BlockSize,
    int CellX,
    int CellY,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    int TargetX,
    int TargetY);

internal sealed record CurrentClientAoiCoveragePlan(
    int ServerLod,
    int BlockSize,
    IReadOnlyList<CurrentClientAoiCell> Cells);

internal static class CurrentClientAoiCoveragePlanner
{
    private static readonly int[] RecoveredCurrentClientBlockSizes = [10, 20, 1000];

    public static CurrentClientAoiCoveragePlan Build(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int serverLod,
        IReadOnlyList<int> runtimeBlockSizes)
    {        if (serverLod < 0 || serverLod >= RecoveredCurrentClientBlockSizes.Length)
            throw new InvalidDataException("Current-client server LOD is outside the recovered AOI range.");
        if (runtimeBlockSizes is null || runtimeBlockSizes.Count < RecoveredCurrentClientBlockSizes.Length)
            throw new InvalidDataException("Current-client AOI block-size array is unavailable or incomplete.");

        for (int index = 0; index < RecoveredCurrentClientBlockSizes.Length; index++)
        {
            if (runtimeBlockSizes[index] != RecoveredCurrentClientBlockSizes[index])
                throw new InvalidDataException("Current-client AOI block-size array does not match the recovered supported client.");
        }

        if (request.TileWidth <= 0 || request.TileHeight <= 0 ||
            request.TileWidth > int.MaxValue || request.TileHeight > int.MaxValue)
            throw new BridgeCommandException("MAP_SIZE_UNAVAILABLE", "world map dimensions are unavailable");

        int width = checked((int)request.TileWidth);
        int height = checked((int)request.TileHeight);
        if (block.MinX < 0 || block.MinY < 0 || block.MaxX < block.MinX || block.MaxY < block.MinY ||
            block.MaxX >= width || block.MaxY >= height)
            throw new InvalidDataException("Requested map scan block is outside the declared world dimensions.");

        int blockSize = RecoveredCurrentClientBlockSizes[serverLod];
        int firstCellX = block.MinX / blockSize;
        int lastCellX = block.MaxX / blockSize;
        int firstCellY = block.MinY / blockSize;
        int lastCellY = block.MaxY / blockSize;
        var cells = new List<CurrentClientAoiCell>();
        for (int cellY = firstCellY; cellY <= lastCellY; cellY++)
        {
            int minY = checked(cellY * blockSize);
            int maxY = Math.Min(height - 1, checked(minY + blockSize - 1));
            int targetY = minY + ((maxY - minY) / 2);
            for (int cellX = firstCellX; cellX <= lastCellX; cellX++)
            {
                int minX = checked(cellX * blockSize);
                int maxX = Math.Min(width - 1, checked(minX + blockSize - 1));
                int targetX = minX + ((maxX - minX) / 2);
                cells.Add(new CurrentClientAoiCell(
                    serverLod,
                    blockSize,
                    cellX,
                    cellY,
                    minX,
                    minY,
                    maxX,
                    maxY,
                    targetX,
                    targetY));
            }
        }

        if (cells.Count == 0)
            throw new InvalidDataException("Current-client AOI coverage plan did not contain any cells.");
        return new CurrentClientAoiCoveragePlan(serverLod, blockSize, cells);
    }
}