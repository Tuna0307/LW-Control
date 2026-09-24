using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapSummaryParityChecks
{
    internal static async Task RunAsync()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        const int publishedServerId = 91;
        const string runId = "summary-run";

        store.UpsertRecord(new MapStoredRecord(
            "resource", publishedServerId, "published-resource", 1, null, null, null,
            20, null, null, null, null, 1000,
            "{\"serverId\":91,\"pointIndex\":1}"));
        store.UpsertRecord(new MapStoredRecord(
            "zombie_boss", publishedServerId, "rebuild-only-zombie", 2, null, null, null,
            20, null, null, null, null, 1001,
            "{\"serverId\":91,\"pointIndex\":2}"));
        store.InsertScanRun(new MapScanRunSeed(
            runId, publishedServerId, "[\"city\"]", "running",
            2, 0, 0, 1000, 1001, null));
        store.StageRecordForPublishTest(runId, new MapStoredRecord(
            "city", publishedServerId, "staged-city-a", 3, "uuid-a", "City A", null,
            30, null, null, null, null, 1002,
            "{\"serverId\":91,\"ownerUid\":\"a\"}"));
        store.StageRecordForPublishTest(runId, new MapStoredRecord(
            "city", publishedServerId, "staged-city-b", 4, "uuid-b", "City B", null,
            29, null, null, null, null, 1003,
            "{\"serverId\":91,\"ownerUid\":\"b\"}"));

        object scanState = new
        {
            serverId = publishedServerId,
            isReading = false,
            scanRunId = runId,
            phase = "idle",
        };
        var backend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            mapData: store,
            mapScanStatusProvider: () => scanState);
        JsonElement payload = JsonSerializer.SerializeToElement(
            new { profileId = backend.ProfileId },
            JsonOptions.Default);
        JsonElement published = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("map_summary", payload, CancellationToken.None),
            JsonOptions.Default);
        RequireExactEnvelope(published);
        RequireExactCountKeys(published.GetProperty("counts"));
        Require(published.GetProperty("serverId").GetInt32() == publishedServerId,
            "published summary did not use shared-state serverId");
        Require(published.GetProperty("counts").GetProperty("resource").GetInt32() == 1 &&
                published.GetProperty("counts").GetProperty("city").GetInt32() == 0,
            "idle summary did not read published map_records");
        Require(!published.GetProperty("counts").TryGetProperty("zombie_boss", out _),
            "map_summary exposed rebuild-only zombie_boss count");

        scanState = new
        {
            serverId = publishedServerId,
            isReading = true,
            scanRunId = runId,
            phase = "scanning",
            marker = "staging-state",
        };
        JsonElement staging = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("map_summary", payload, CancellationToken.None),
            JsonOptions.Default);
        RequireExactEnvelope(staging);
        RequireExactCountKeys(staging.GetProperty("counts"));
        Require(staging.GetProperty("counts").GetProperty("city").GetInt32() == 2 &&
                staging.GetProperty("counts").GetProperty("resource").GetInt32() == 0,
            "active summary did not switch to exact run-scoped scan_records");
        Require(staging.GetProperty("scanState").GetProperty("marker").GetString() == "staging-state",
            "map_summary did not preserve the shared scanState object");

        scanState = new
        {
            serverId = 92,
            isReading = false,
            scanRunId = string.Empty,
            phase = "idle",
        };
        JsonElement noFallback = JsonSerializer.SerializeToElement(
            await backend.InvokeAsync("map_summary", payload, CancellationToken.None),
            JsonOptions.Default);
        Require(noFallback.GetProperty("serverId").GetInt32() == 92 &&
                noFallback.GetProperty("counts").GetProperty("resource").GetInt32() == 0,
            "map_summary fell back to saved/published server data instead of shared state");

        var failingBackend = new LWBridgeBackend(
            new LocalConfigStore(persistent: false),
            mapData: store,
            mapScanStatusProvider: () =>
                throw new BridgeCommandException("STATE_FAIL", "synthetic shared-state failure"));
        JsonElement failingPayload = JsonSerializer.SerializeToElement(
            new { profileId = failingBackend.ProfileId },
            JsonOptions.Default);
        try
        {
            _ = await failingBackend.InvokeAsync(
                "map_summary", failingPayload, CancellationToken.None);
            throw new InvalidOperationException("expected shared-state failure to propagate");
        }
        catch (BridgeCommandException error) when (
            error.Code == "STATE_FAIL" &&
            error.Message == "synthetic shared-state failure")
        {
        }
    }

    private static void RequireExactEnvelope(JsonElement summary)
    {
        string[] actual = summary.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = ["counts", "scanState", "serverId"];
        Require(actual.SequenceEqual(expected),
            "map_summary did not preserve exact {serverId,counts,scanState} envelope");
    }

    private static void RequireExactCountKeys(JsonElement counts)
    {
        string[] actual = counts.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = MapScanContract.RecoveredDefaultTypes
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Require(actual.SequenceEqual(expected),
            "map_summary counts did not contain exactly the eight original kinds");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
