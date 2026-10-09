namespace LWBridge.Desktop;

internal static class MapScanTerminalPublicationGate
{
    public static bool HasTerminalScanFailure(
        long failedBlocks,
        string? lastError)
    {
        return failedBlocks > 0 || !string.IsNullOrEmpty(lastError);
    }

    public static bool CanEnterPublishing(
        long failedBlocks,
        string? lastError)
    {
        return !HasTerminalScanFailure(failedBlocks, lastError);
    }
}
