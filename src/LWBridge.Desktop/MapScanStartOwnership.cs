namespace LWBridge.Desktop;

internal enum MapScanStartIntent
{
    Fresh,
    ResumeExisting
}

internal static class MapScanStartOwnership
{
    public const string ActiveScanErrorCode = "SCAN_RUNNING";
    public const string ActiveScanErrorMessage = "map scan already running";
    public const string MissingConnectionErrorCode = "GAME_CONNECTION_UNAVAILABLE";
    public const string MissingConnectionErrorMessage = "game connection unavailable";

    public static MapScanStartIntent ResolveIntent(bool requestedResume, bool currentResumeAvailable) =>
        requestedResume && currentResumeAvailable
            ? MapScanStartIntent.ResumeExisting
            : MapScanStartIntent.Fresh;

    public static void RequireConnection(bool available)
    {
        if (!available)
            throw new BridgeCommandException(MissingConnectionErrorCode, MissingConnectionErrorMessage);
    }

    public static void RejectAlreadyRunning(bool isReading)
    {
        if (isReading)
            throw new BridgeCommandException(ActiveScanErrorCode, ActiveScanErrorMessage);
    }
}
