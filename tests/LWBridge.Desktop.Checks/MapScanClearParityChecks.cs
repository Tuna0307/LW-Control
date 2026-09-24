using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapScanClearParityChecks
{
    internal static async Task RunAsync()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        SeedServer(store, 91, "live");
        SeedServer(store, 92, "saved");

        int liveServerId = 91;
        var service = new ManualMapScanCommandService(
            store,
            _ => Task.FromException<CurrentClientMapContext>(
                new InvalidOperationException("map context must not be requested by Clear")),
            new UnusedBlockSource(),
            getLiveServerId: () => liveServerId);

        await service.InvokeAsync(
            "map_scan_status",
            JsonSerializer.SerializeToElement(new { }),
            CancellationToken.None);
        await ExpectErrorAsync(service, 0, "SERVER_UNAVAILABLE");
        Require(store.CountRecords("city", 91) == 1 &&
                store.CountRecords("city", 92) == 1 &&
                store.CountScanRuns(91) == 1 &&
                store.CountScanRuns(92) == 1,
            "serverId=0 Clear mutated persisted scan data");

        await ExpectErrorAsync(service, 92, "SERVER_UNAVAILABLE");
        Require(store.CountRecords("city", 91) == 1 &&
                store.CountRecords("city", 92) == 1,
            "non-current-server Clear mutated persisted scan data");

        object? result = await service.InvokeAsync(
            "map_scan_clear",
            JsonSerializer.SerializeToElement(new { serverId = 91 }),
            CancellationToken.None);
        JsonElement status = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
        Require(status.GetProperty("serverId").GetInt32() == 91 &&
                status.GetProperty("serverIdSource").GetString() == "live" &&
                status.GetProperty("phase").GetString() == "idle",
            "valid live-server Clear did not return refreshed live idle state");
        Require(store.CountRecords("city", 91) == 0 &&
                store.CountScanRuns(91) == 0 &&
                store.CountRecords("city", 92) == 1 &&
                store.CountScanRuns(92) == 1,
            "valid Clear did not remain strictly server-scoped");

        service.Close();
    }

    private static void SeedServer(MapDataStore store, int serverId, string suffix)
    {
        store.UpsertRecord(new MapStoredRecord(
            "city", serverId, $"clear-{suffix}", 1, $"uuid-{suffix}", $"City {suffix}", null,
            20, null, null, null, null, 1000 + serverId,
            JsonSerializer.Serialize(new { serverId, ownerUid = $"owner-{suffix}" })));
        store.InsertScanRun(new MapScanRunSeed(
            $"run-{suffix}", serverId, "[\"city\"]", "completed",
            1, 1, 0, 1000, 1000 + serverId, null));
    }

    private static async Task ExpectErrorAsync(
        ManualMapScanCommandService service,
        int serverId,
        string expectedCode)
    {
        try
        {
            await service.InvokeAsync(
                "map_scan_clear",
                JsonSerializer.SerializeToElement(new { serverId }),
                CancellationToken.None);
        }
        catch (BridgeCommandException error)
        {
            Require(error.Code == expectedCode &&
                    error.Message == MapScanClearOwnership.ServerUnavailableErrorMessage,
                $"Clear error mismatch for server {serverId}: {error.Code} / {error.Message}");
            return;
        }

        throw new InvalidOperationException(
            $"map_scan_clear unexpectedly accepted server {serverId}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class UnusedBlockSource : IMapScanBlockSource
    {
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken) =>
            Task.FromException<MapScanBlockCapture>(
                new InvalidOperationException("block capture must not run during Clear"));
    }
}
