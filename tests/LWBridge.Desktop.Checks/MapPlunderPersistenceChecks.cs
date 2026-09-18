using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapPlunderPersistenceChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-plunder-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string databasePath = Path.Combine(root, "map-data.db");
        var config = new LocalConfigStore(persistent: false);

        try
        {
            using (var store = new MapDataStore(databasePath))
            {
                store.UpsertTruckPlunderJobForTest(
                    88, "truck-later",
                    """{"uuid":"truck-later","ownerName":"Later","robTimes":0,"maxLootCount":2}""",
                    executeAt: 2_000, expireAt: 9_000, status: "scheduled", attempts: 1,
                    lastError: null, createdAt: 100, updatedAt: 110);
                store.UpsertTruckPlunderJobForTest(
                    88, "truck-waiting",
                    """{"uuid":"truck-waiting","ownerName":"Waiting","robTimes":1,"maxLootCount":2}""",
                    executeAt: 1_000, expireAt: 9_000, status: "waiting_connection", attempts: 2,
                    lastError: "client restarted", createdAt: 90, updatedAt: 120);
                store.InsertTruckPlunderHistoryForTest(
                    "truck-history", 88, "truck-history",
                    """{"uuid":"truck-history","jobId":"truck-history","battleWon":true,"plunderRewards":[]}""",
                    executeAt: 500, status: "succeeded", attempts: 1,
                    lastError: null, createdAt: 80, updatedAt: 130);
                store.UpsertDispatchPlunderJobForTest(
                    88, "dispatch-running",
                    """{"uuid":"dispatch-running","ownerName":"Dispatch","quality":4,"rewards":[]}""",
                    completionTime: 400, plunderAt: 900, expireAt: 9_000,
                    status: "running", attempts: 3, lastError: null, createdAt: 70, updatedAt: 140);

                MapPlunderJobsSnapshot snapshot = store.ReadPlunderJobs();
                Check(snapshot.TruckJobs.Count == 3, "truck plunder list includes live jobs plus history");
                Check(snapshot.DispatchJobs.Count == 1, "combined plunder list includes persisted dispatch jobs");

                JsonElement waiting = snapshot.TruckJobs[0];
                JsonElement later = snapshot.TruckJobs[1];
                JsonElement history = snapshot.TruckJobs[2];
                Check(waiting.GetProperty("uuid").GetString() == "truck-waiting" &&
                      waiting.GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
                      waiting.GetProperty("attempts").GetInt32() == 2 &&
                      waiting.GetProperty("lastError").GetString() == "client restarted" &&
                      waiting.GetProperty("scheduledAt").GetInt64() == 90 &&
                      waiting.GetProperty("scheduleUpdatedAt").GetInt64() == 120 &&
                      waiting.GetProperty("executeAt").GetInt64() == 1_000,
                    "truck list overlays recovered scheduler metadata onto stored truck JSON");
                Check(later.GetProperty("uuid").GetString() == "truck-later",
                    "active truck jobs are ordered by execute_at ascending");
                Check(history.GetProperty("uuid").GetString() == "truck-history" &&
                      history.GetProperty("scheduleStatus").GetString() == "succeeded",
                    "terminal truck history sorts after active jobs");

                JsonElement dispatch = snapshot.DispatchJobs[0];
                Check(dispatch.GetProperty("uuid").GetString() == "dispatch-running" &&
                      dispatch.GetProperty("scheduleStatus").GetString() == "running" &&
                      dispatch.GetProperty("attempts").GetInt32() == 3 &&
                      dispatch.GetProperty("plunderAt").GetInt64() == 900 &&
                      dispatch.GetProperty("scheduledAt").GetInt64() == 70 &&
                      dispatch.GetProperty("scheduleUpdatedAt").GetInt64() == 140,
                    "combined plunder list overlays the same recovered scheduler metadata on dispatch jobs");

                var backend = new LWBridgeBackend(config, mapData: store);
                JsonElement listPayload = Payload(new { profileId = config.Snapshot.ProfileId });
                object? listed = backend.InvokeAsync("map_plunder_jobs_list", listPayload, CancellationToken.None)
                    .GetAwaiter().GetResult();
                JsonElement listedJson = JsonSerializer.SerializeToElement(listed, JsonOptions.Default);
                Check(listedJson.GetProperty("truckJobs").GetArrayLength() == 3 &&
                      listedJson.GetProperty("dispatchJobs").GetArrayLength() == 1,
                    "production map_plunder_jobs_list returns the recovered combined envelope");

                JsonElement schedule = Payload(new
                {
                    profileId = config.Snapshot.ProfileId,
                    rows = Array.Empty<object>(),
                });
                ExpectBridgeError(
                    "COMMAND_NOT_IMPLEMENTED",
                    "Command 'map_truck_plunder_schedule' is not implemented by the production backend yet.",
                    () => backend.InvokeAsync("map_truck_plunder_schedule", schedule, CancellationToken.None)
                        .GetAwaiter().GetResult(),
                    "truck plunder schedule stays fail-closed until a current-v19 execution primitive is recovered");

                JsonElement cancel = Payload(new
                {
                    profileId = config.Snapshot.ProfileId,
                    serverId = 88,
                    trainUuid = "truck-later",
                });
                object? cancelResult = backend.InvokeAsync("map_truck_plunder_cancel", cancel, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(cancelResult is null, "truck cancel success does not invent an unused result payload");

                MapPlunderJobsSnapshot afterCancel = store.ReadPlunderJobs();
                JsonElement cancelled = afterCancel.TruckJobs.Single(
                    row => row.GetProperty("uuid").GetString() == "truck-later");
                Check(cancelled.GetProperty("scheduleStatus").GetString() == "cancelled" &&
                      cancelled.GetProperty("lastError").ValueKind == JsonValueKind.Null,
                    "truck cancel changes only the eligible persisted job to cancelled and clears last_error");

                ExpectBridgeError(
                    "NOT_FOUND", "scheduled truck job not found",
                    () => backend.InvokeAsync("map_truck_plunder_cancel", cancel, CancellationToken.None)
                        .GetAwaiter().GetResult(),
                    "a second cancel cannot mutate a terminal truck job");

                JsonElement invalid = Payload(new
                {
                    profileId = config.Snapshot.ProfileId,
                    serverId = 88,
                    trainUuid = "",
                });
                ExpectBridgeError(
                    "INVALID_TARGET", "truck target is required",
                    () => backend.InvokeAsync("map_truck_plunder_cancel", invalid, CancellationToken.None)
                        .GetAwaiter().GetResult(),
                    "truck cancel preserves recovered invalid-target text");
            }

            using (var reopened = new MapDataStore(databasePath))
            {
                MapPlunderJobsSnapshot snapshot = reopened.ReadPlunderJobs();
                JsonElement cancelled = snapshot.TruckJobs.Single(
                    row => row.GetProperty("uuid").GetString() == "truck-later");
                Check(cancelled.GetProperty("scheduleStatus").GetString() == "cancelled",
                    "truck cancel persists across database reopen");
                Check(snapshot.DispatchJobs.Count == 1 &&
                      snapshot.DispatchJobs[0].GetProperty("scheduleStatus").GetString() == "running",
                    "combined list preserves unrelated dispatch job state across reopen");
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static JsonElement Payload(object value)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions.Default));
        return document.RootElement.Clone();
    }

    private static void ExpectBridgeError(
        string code,
        string message,
        Action action,
        string name)
    {
        try
        {
            action();
        }
        catch (BridgeCommandException error)
        {
            Check(error.Code == code && error.Message == message, name);
            return;
        }

        throw new InvalidOperationException("check failed: " + name);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("check failed: " + name);
    }
}
