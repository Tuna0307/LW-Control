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
    public const int EnterWorldMapRequestTimeoutMs = 5_000;
    public const int WorldMapReadyDeadlineMs = 10_000;
    public const int WorldMapReadyPollIntervalMs = 500;
    public const string WorldMapFailureCode = "WORLD_MAP_FAILED";
    public const string WorldMapFailureMessage = "failed to enter world map";
    public const string ServerUnavailableCode = "SERVER_UNAVAILABLE";
    public const string ServerUnavailableMessage = "current server id unavailable";

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

    public static bool RequiresEnterWorldMap(bool isInWorld) => !isInWorld;

    public static void RequireWorldMapReady(bool isInWorld)
    {
        if (!isInWorld)
            throw new BridgeCommandException(WorldMapFailureCode, WorldMapFailureMessage);
    }

    public static void RequireLiveServer(long serverId, string? serverIdSource)
    {
        if (serverId <= 0 || !string.Equals(serverIdSource, "live", StringComparison.Ordinal))
            throw new BridgeCommandException(ServerUnavailableCode, ServerUnavailableMessage);
    }
}
