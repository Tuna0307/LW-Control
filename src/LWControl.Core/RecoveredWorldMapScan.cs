namespace LWControl.Core;

public sealed record RecoveredWorldMapBlock
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Index { get; init; }
}

public sealed record RecoveredWorldMapCoverage
{
    public required int Left { get; init; }
    public required int Bottom { get; init; }
    public required int Right { get; init; }
    public required int Top { get; init; }

    public int Width => Right - Left + 1;
    public int Height => Top - Bottom + 1;
    public int BlockCount => Width * Height;

    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Bottom && y <= Top;

    public bool Overlaps(RecoveredWorldMapCoverage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Right >= other.Left && Top >= other.Bottom && Left <= other.Right && Bottom <= other.Top;
    }
}

public sealed record RecoveredWorldMapTilePoint
{
    public required int X { get; init; }
    public required int Y { get; init; }
}

public sealed record RecoveredWorldMapBatch
{
    public required int Sequence { get; init; }
    public required IReadOnlyList<RecoveredWorldMapBlock> RequestedBlocks { get; init; }
    public required RecoveredWorldMapCoverage RequestedCoverage { get; init; }
    public required RecoveredWorldMapCoverage TransportCoverage { get; init; }
    public required IReadOnlyList<int> TransportIndexes { get; init; }
    public required RecoveredWorldMapTilePoint LeftBottomTile { get; init; }
    public required RecoveredWorldMapTilePoint RightTopTile { get; init; }
    public required RecoveredWorldMapTilePoint RequestTile { get; init; }
}

public sealed record RecoveredWorldMapScanPlan
{
    public required int BlockCount { get; init; }
    public required int BlockSize { get; init; }
    public required int RequestedEdge { get; init; }
    public required RecoveredWorldMapCoverage RequestedCoverage { get; init; }
    public required IReadOnlyList<RecoveredWorldMapBlock> RequestedBlocks { get; init; }
    public required IReadOnlyList<RecoveredWorldMapBatch> Batches { get; init; }
}

public sealed record RecoveredWorldMapResponseEnvelope
{
    public int? ServerId { get; init; }
    public int? WorldId { get; init; }
    public required RecoveredWorldMapCoverage Coverage { get; init; }
}

public sealed record RecoveredWorldMapCoverageResult
{
    public required int CoveredBlocks { get; init; }
    public required int ExpectedBlocks { get; init; }
    public required int AcceptedEnvelopes { get; init; }
    public required int RejectedEnvelopes { get; init; }
    public required IReadOnlyList<RecoveredWorldMapBlock> Covered { get; init; }
    public bool Complete => ExpectedBlocks > 0 && CoveredBlocks == ExpectedBlocks;
}

/// <summary>
/// Clean-room reconstruction of the recovered LWC2MapScanner native block planner.
/// It plans logical coverage and transport envelopes only; it performs no game or network I/O.
/// </summary>
public static class RecoveredWorldMapScanPlanner
{
    public const int MaxNativeBatchIndexes = 160;
    public const int MinimumTransportWidth = 5;
    public const int MinimumTransportHeight = 4;

    public static RecoveredWorldMapScanPlan Build(
        int blockCount,
        int blockSize,
        int centerX,
        int centerY,
        int requestedEdge = 5)
    {
        ValidateGeometry(blockCount, blockSize, centerX, centerY);
        requestedEdge = Math.Clamp(requestedEdge, 1, 99);
        if (requestedEdge % 2 == 0) requestedEdge--;

        int selectedEdge = Math.Min(requestedEdge, blockCount);
        int startX;
        int startY;
        int endX;
        int endY;
        if (requestedEdge >= blockCount - 1)
        {
            startX = 0;
            startY = 0;
            endX = blockCount - 1;
            endY = blockCount - 1;
            selectedEdge = blockCount;
        }
        else
        {
            int radius = selectedEdge / 2;
            startX = Math.Clamp(centerX - radius, 0, blockCount - selectedEdge);
            startY = Math.Clamp(centerY - radius, 0, blockCount - selectedEdge);
            endX = startX + selectedEdge - 1;
            endY = startY + selectedEdge - 1;
        }

        var coverage = new RecoveredWorldMapCoverage
        {
            Left = startX,
            Bottom = startY,
            Right = endX,
            Top = endY
        };
        var requested = EnumerateBlocks(coverage, blockCount)
            .OrderBy(block => Math.Max(Math.Abs(block.X - centerX), Math.Abs(block.Y - centerY)))
            .ThenBy(block => block.Y)
            .ThenBy(block => block.X)
            .ToArray();
        var batches = BuildBatchQueue(blockCount, blockSize, coverage);

        return new()
        {
            BlockCount = blockCount,
            BlockSize = blockSize,
            RequestedEdge = selectedEdge,
            RequestedCoverage = coverage,
            RequestedBlocks = requested,
            Batches = batches
        };
    }

    public static RecoveredWorldMapCoverageResult EvaluateCoverage(
        RecoveredWorldMapBatch batch,
        int? expectedServerId,
        int? expectedWorldId,
        IReadOnlyList<RecoveredWorldMapResponseEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(envelopes);
        var covered = new Dictionary<(int X, int Y), RecoveredWorldMapBlock>();
        int accepted = 0;
        int rejected = 0;

        foreach (var envelope in envelopes)
        {
            if (envelope is null)
            {
                rejected++;
                continue;
            }
            bool identityMatches = (expectedServerId is null || envelope.ServerId is null
                    || expectedServerId == envelope.ServerId)
                && (expectedWorldId is null || envelope.WorldId is null
                    || expectedWorldId == envelope.WorldId);
            if (!identityMatches || !envelope.Coverage.Overlaps(batch.RequestedCoverage))
            {
                rejected++;
                continue;
            }

            accepted++;
            foreach (var block in batch.RequestedBlocks)
            {
                if (envelope.Coverage.Contains(block.X, block.Y))
                    covered.TryAdd((block.X, block.Y), block);
            }
        }

        return new()
        {
            CoveredBlocks = covered.Count,
            ExpectedBlocks = batch.RequestedBlocks.Count,
            AcceptedEnvelopes = accepted,
            RejectedEnvelopes = rejected,
            Covered = covered.Values.OrderBy(block => block.Y).ThenBy(block => block.X).ToArray()
        };
    }

    private static IReadOnlyList<RecoveredWorldMapBatch> BuildBatchQueue(
        int blockCount,
        int blockSize,
        RecoveredWorldMapCoverage coverage)
    {
        int width = coverage.Width;
        int height = coverage.Height;
        int bestWidth = 1;
        int bestHeight = 1;
        int bestCount = int.MaxValue;
        int bestArea = 0;
        for (int batchWidth = 1; batchWidth <= Math.Min(width, MaxNativeBatchIndexes); batchWidth++)
        {
            int batchHeight = Math.Min(height, MaxNativeBatchIndexes / batchWidth);
            if (batchHeight < 1) continue;
            int requestCount = CeilingDivide(width, batchWidth) * CeilingDivide(height, batchHeight);
            int area = batchWidth * batchHeight;
            if (requestCount < bestCount || (requestCount == bestCount && area > bestArea))
            {
                bestWidth = batchWidth;
                bestHeight = batchHeight;
                bestCount = requestCount;
                bestArea = area;
            }
        }

        var batches = new List<RecoveredWorldMapBatch>();
        int y = coverage.Bottom;
        while (y <= coverage.Top)
        {
            int batchTop = Math.Min(coverage.Top, y + bestHeight - 1);
            var row = new List<RecoveredWorldMapBatch>();
            int x = coverage.Left;
            while (x <= coverage.Right)
            {
                int batchRight = Math.Min(coverage.Right, x + bestWidth - 1);
                var requestedCoverage = new RecoveredWorldMapCoverage
                {
                    Left = x,
                    Bottom = y,
                    Right = batchRight,
                    Top = batchTop
                };
                row.Add(BuildBatch(blockCount, blockSize, requestedCoverage, 0));
                x = batchRight + 1;
            }
            if (batches.Count % 2 == 1) row.Reverse();
            foreach (var batch in row)
                batches.Add(batch with { Sequence = batches.Count + 1 });
            y = batchTop + 1;
        }
        return batches;
    }

    private static RecoveredWorldMapBatch BuildBatch(
        int blockCount,
        int blockSize,
        RecoveredWorldMapCoverage requestedCoverage,
        int sequence)
    {
        var transport = PadTransportCoverage(requestedCoverage, blockCount);
        var indexes = EnumerateBlocks(transport, blockCount).Select(block => block.Index).ToArray();
        if (indexes.Length is < 1 or > MaxNativeBatchIndexes)
            throw new InvalidOperationException("Recovered native transport coverage exceeds the 160-index limit.");

        int worldSize = checked(blockCount * blockSize);
        var leftBottom = new RecoveredWorldMapTilePoint
        {
            X = transport.Left * blockSize,
            Y = transport.Bottom * blockSize
        };
        var rightTop = new RecoveredWorldMapTilePoint
        {
            X = Math.Min(worldSize - 1, (transport.Right + 1) * blockSize),
            Y = Math.Min(worldSize - 1, (transport.Top + 1) * blockSize)
        };
        var requestTile = new RecoveredWorldMapTilePoint
        {
            X = (leftBottom.X + rightTop.X) / 2,
            Y = (leftBottom.Y + rightTop.Y) / 2
        };

        return new()
        {
            Sequence = sequence,
            RequestedBlocks = EnumerateBlocks(requestedCoverage, blockCount).ToArray(),
            RequestedCoverage = requestedCoverage,
            TransportCoverage = transport,
            TransportIndexes = indexes,
            LeftBottomTile = leftBottom,
            RightTopTile = rightTop,
            RequestTile = requestTile
        };
    }

    private static RecoveredWorldMapCoverage PadTransportCoverage(
        RecoveredWorldMapCoverage coverage,
        int blockCount)
    {
        (int left, int right) = PadAxis(coverage.Left, coverage.Right, MinimumTransportWidth, blockCount);
        (int bottom, int top) = PadAxis(coverage.Bottom, coverage.Top, MinimumTransportHeight, blockCount);
        return new() { Left = left, Bottom = bottom, Right = right, Top = top };
    }

    private static (int Low, int High) PadAxis(int low, int high, int minimum, int blockCount)
    {
        int missing = Math.Max(0, minimum - (high - low + 1));
        low -= missing / 2;
        high += (missing + 1) / 2;
        if (low < 0)
        {
            high -= low;
            low = 0;
        }
        if (high >= blockCount)
        {
            low -= high - blockCount + 1;
            high = blockCount - 1;
        }
        return (Math.Max(0, low), Math.Min(blockCount - 1, high));
    }

    private static IEnumerable<RecoveredWorldMapBlock> EnumerateBlocks(
        RecoveredWorldMapCoverage coverage,
        int blockCount)
    {
        for (int y = coverage.Bottom; y <= coverage.Top; y++)
        for (int x = coverage.Left; x <= coverage.Right; x++)
            yield return new() { X = x, Y = y, Index = checked(y * blockCount + x) };
    }

    private static int CeilingDivide(int value, int divisor) => (value + divisor - 1) / divisor;

    private static void ValidateGeometry(int blockCount, int blockSize, int centerX, int centerY)
    {
        if (blockCount < 1 || blockCount > 10_000)
            throw new ArgumentOutOfRangeException(nameof(blockCount));
        if (blockSize < 1 || blockSize > 10_000)
            throw new ArgumentOutOfRangeException(nameof(blockSize));
        if (centerX < 0 || centerX >= blockCount)
            throw new ArgumentOutOfRangeException(nameof(centerX));
        if (centerY < 0 || centerY >= blockCount)
            throw new ArgumentOutOfRangeException(nameof(centerY));
        _ = checked(blockCount * blockSize);
    }
}
