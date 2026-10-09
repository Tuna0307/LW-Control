namespace LWBridge.Desktop;

internal static class MapScanPublicationOwnership
{
    public static void ValidateCompletionTransition(long affectedRows)
    {
        if (affectedRows == 0)
        {
            throw new BridgeCommandException(
                "INVALID_SCAN",
                "map scan is not running");
        }
    }

    public static void ValidatePostCommitRunPresent(bool present)
    {
        if (!present)
        {
            throw new BridgeCommandException(
                "INVALID_SCAN",
                "map scan disappeared");
        }
    }
}
