namespace LWBridge.Desktop;

internal readonly record struct MapScanSchedulerCounters(
    long CompletedBlocks,
    long ReadBlocks,
    long FailedBlocks,
    long UnreadBlocks,
    long InflightBlocks);

internal static class MapScanSchedulerProgress
{
    public static MapScanSchedulerCounters Normalize(
        long totalBlocks,
        long concurrency,
        long completedBlocks,
        long failedBlocks,
        long inflightBlocks)
    {
        if (totalBlocks < 0)
            throw new ArgumentOutOfRangeException(nameof(totalBlocks));
        if (concurrency < 0)
            throw new ArgumentOutOfRangeException(nameof(concurrency));

        long completed = Math.Clamp(completedBlocks, 0, totalBlocks);
        long failed = Math.Clamp(failedBlocks, 0, totalBlocks);
        if (completed > totalBlocks - failed)
        {
            throw new BridgeCommandException(
                "INVALID_SCAN_PROGRESS",
                "completed and failed map blocks exceed the scan total");
        }

        long accounted = completed + failed;
        long remaining = totalBlocks - accounted;
        long maxInflight = Math.Min(concurrency, remaining);
        long inflight = Math.Clamp(inflightBlocks, 0, maxInflight);
        long unread = remaining - inflight;
        return new MapScanSchedulerCounters(completed, completed, failed, unread, inflight);
    }

    public static double ComputeScanRate(long completedBlocks, long elapsedMilliseconds)
    {
        long boundedElapsed = Math.Max(elapsedMilliseconds, 1);
        double perSecond = completedBlocks / (boundedElapsed / 1000.0);
        return Math.Round(perSecond * 100.0, MidpointRounding.AwayFromZero) / 100.0;
    }
}
