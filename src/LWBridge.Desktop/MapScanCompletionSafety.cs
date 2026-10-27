namespace LWBridge.Desktop;

internal static class MapScanCompletionSafety
{
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
}
