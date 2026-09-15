namespace LWBridge.Desktop;

internal static class MapScanClearOwnership
{
    public const string ActiveScanErrorCode = "SCAN_RUNNING";
    public const string ActiveScanErrorMessage = "stop the map scan first";
    public const string ServerUnavailableErrorCode = "SERVER_UNAVAILABLE";
    public const string ServerUnavailableErrorMessage = "current server id unavailable";
    public const string LiveServerSource = "live";

    public static void Validate(
        int requestedServerId,
        bool isReading,
        int currentServerId,
        string? currentServerIdSource)
    {
        if (isReading)
            throw new BridgeCommandException(ActiveScanErrorCode, ActiveScanErrorMessage);

        if (requestedServerId <= 0 ||
            currentServerId != requestedServerId ||
            !string.Equals(currentServerIdSource, LiveServerSource, StringComparison.Ordinal))
        {
            throw new BridgeCommandException(ServerUnavailableErrorCode, ServerUnavailableErrorMessage);
        }
    }
}
