namespace LWBridge.Desktop;

internal readonly record struct MapScanNativeCaptureMetrics(
    long PendingRecords,
    long DroppedRecords);

internal readonly record struct MapScanStoppedState(
    bool IsReading,
    string Phase,
    long InflightBlocks,
    bool ResumeAvailable);

internal static class MapScanCompletionSafety
{
    public static MapScanNativeCaptureMetrics DeriveNativeCaptureMetrics(
        long pendingPoints,
        long pendingMarches,
        long pendingPointRemovals,
        long pendingMarchRemovals,
        long pendingAcks,
        long droppedRecords)
    {
        long pending = pendingPoints + pendingMarches + pendingPointRemovals;
        pending += pendingMarchRemovals;
        pending += pendingAcks;
        return new MapScanNativeCaptureMetrics(pending, droppedRecords);
    }
    public static bool HasDroppedNativeCapture(long droppedRecords) => droppedRecords > 0;

    public static void ValidateDirectCompletion(
        long totalBlocks,
        long completedBlocks,
        long failedBlocks,
        long additionalFailedBatchCount = 0)
    {
        if (completedBlocks + failedBlocks != totalBlocks)
        {
            throw new BridgeCommandException(
                "INCOMPLETE_SCAN",
                "direct map scan is incomplete");
        }

        if (additionalFailedBatchCount != 0 || failedBlocks != 0)
        {
            throw new BridgeCommandException(
                "INCOMPLETE_SCAN",
                "direct map scan contains failed batches");
        }
    }

    public static MapScanStoppedState RecoveredStoppedState() =>
        new(false, "idle", 0, false);
}
