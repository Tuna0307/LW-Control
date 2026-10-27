namespace LWBridge.Desktop;

internal static class MapScanStartOwnership
{
    public const string MissingConnectionErrorCode = "GAME_CONNECTION_UNAVAILABLE";
    public const string MissingConnectionErrorMessage = "game connection unavailable";
    public const int EnterWorldMapRequestTimeoutMs = 5_000;
    public const int WorldMapReadyDeadlineMs = 10_000;
    public const string WorldMapFailureCode = "WORLD_MAP_FAILED";
    public const string WorldMapFailureMessage = "failed to enter world map";
}
