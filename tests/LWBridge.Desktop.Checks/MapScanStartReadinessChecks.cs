using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapScanStartReadinessChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (MapScanStartOwnership.EnterWorldMapRequestTimeoutMs != 5_000 ||
            MapScanStartOwnership.WorldMapReadyDeadlineMs != 10_000 ||
            MapScanStartOwnership.WorldMapReadyPollIntervalMs != 500)
            throw new InvalidOperationException("recovered map-start readiness timing changed");

        if (!MapScanStartOwnership.RequiresEnterWorldMap(false) ||
            MapScanStartOwnership.RequiresEnterWorldMap(true))
            throw new InvalidOperationException("recovered enter-world-map predicate changed");

        MapScanStartOwnership.RequireWorldMapReady(true);
        MapScanStartOwnership.RequireLiveServer(2212, "live");
        Expect("WORLD_MAP_FAILED", "failed to enter world map", () => MapScanStartOwnership.RequireWorldMapReady(false));
        Expect("SERVER_UNAVAILABLE", "current server id unavailable", () => MapScanStartOwnership.RequireLiveServer(0, "live"));
        Expect("SERVER_UNAVAILABLE", "current server id unavailable", () => MapScanStartOwnership.RequireLiveServer(2212, "saved_capture_replay"));
    }

    private static void Expect(string code, string message, Action action)
    {
        try
        {
            action();
        }
        catch (BridgeCommandException error) when (error.Code == code && error.Message == message)
        {
            return;
        }

        throw new InvalidOperationException($"expected {code} / {message}");
    }
}
