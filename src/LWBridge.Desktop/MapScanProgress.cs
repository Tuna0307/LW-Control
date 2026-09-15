namespace LWBridge.Desktop;

internal readonly record struct MapScanDerivedProgress(
    long UnreadBlocks,
    double ProgressPercent);

internal static class MapScanProgress
{
    public const double RecoveredIncompleteCap = 98.0;

    public static MapScanDerivedProgress Derive(
        long totalBlocks,
        long completedBlocks,
        long failedBlocks,
        string status)
    {
        long accountedBlocks = completedBlocks + failedBlocks;
        long unreadBlocks = Math.Max(totalBlocks - accountedBlocks, 0);
        return new MapScanDerivedProgress(
            unreadBlocks,
            DeriveProgressPercent(totalBlocks, accountedBlocks, status));
    }

    private static double DeriveProgressPercent(long totalBlocks, long accountedBlocks, string status)
    {
        if (totalBlocks <= 0)
            return 0.0;

        if (string.Equals(status, "completed", StringComparison.Ordinal))
            return 100.0;

        long bounded = Math.Clamp(accountedBlocks, 0, totalBlocks);
        double tenths = Math.Round(
            ((double)bounded / totalBlocks) * 1000.0,
            MidpointRounding.AwayFromZero);
        return Math.Min(tenths / 10.0, RecoveredIncompleteCap);
    }
}
