using System.Text.Json;
using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Genuine worker/storage, deterministic external provider. No live truck attack.
internal static class Map006TruckCorrelationChecks
{
    private static void Need(bool condition, string why)
    {
        if (!condition) throw new InvalidOperationException("MAP_006_TRUCK: " + why);
    }

    private static string Status(MapStore store) =>
        store.ReadTruckPlunderJobs().Single().GetProperty("scheduleStatus").GetString()!;

    private static async Task CaseAsync(bool missingIdentity)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-truck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        const long now = 1_800_000_000_000L;
        var results = new Queue<TruckPlunderResultEvent>();
        int arms = 0;
        try
        {
            using var store = new MapStore(Path.Combine(root, "truck.db"));
            var scheduled = store.ScheduleTruckPlunder(
                317, "123456", "{\"trainUuid\":\"123456\",\"robTimes\":0,\"maxLootCount\":2}",
                now - 1000, now + 60000, now);
            var provider = new MapActionProviderAdapter
            {
                GotoWorldCoordinate = (_, _, _, _) => ValueTask.CompletedTask,
                GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
                GetCurrentServerId = _ => ValueTask.FromResult(317),
                GotoServer = (_, _) => ValueTask.CompletedTask,
                InspectTreasureStates = (_, _, _) => ValueTask.FromResult(new TreasureInspectionResult("", "", [])),
                GetTreasureClaimStatus = _ => throw new InvalidOperationException("No claim permitted"),
                ClaimTreasures = (_, _) => throw new InvalidOperationException("No claim permitted"),
                ShareDispatchTaskToAlliance = (_, _) => throw new InvalidOperationException("No sharing permitted"),
                PrepareGhostPlunderTasks = (_, _) => throw new InvalidOperationException("No plunder permitted"),
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (_, _) => throw new InvalidOperationException("No dispatch permitted"),
                ArmTruckPlunder = (job, _) =>
                {
                    Need(job.ServerId == 317 && job.TrainUuid == "123456" &&
                         job.JobId == scheduled.JobId, "worker changed job identity");
                    arms++;
                    return ValueTask.FromResult(new TruckPlunderArmResult(true,
                        AttemptId: missingIdentity ? null : "attempt-truck-006",
                        LaunchSessionId: missingIdentity ? null : "session-truck-006"));
                },
                DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<DispatchPlunderResultEvent>>([]),
                DrainTruckPlunderResults = _ =>
                {
                    TruckPlunderResultEvent[] drained = results.ToArray();
                    results.Clear();
                    return ValueTask.FromResult<IReadOnlyList<TruckPlunderResultEvent>>(drained);
                },
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
            var worker = new MapPlunderWorker(store, provider, () => now);
            await worker.RunTruckOnceAsync();
            Need(arms == 1, "scheduled provider not called exactly once");
            if (missingIdentity)
            {
                Need(Status(store) == "failed" &&
                     store.ReadTruckPlunderJobs()[0].GetProperty("lastError").GetString() ==
                         "TRUCK_PLUNDER_CORRELATION_UNAVAILABLE",
                     "unknown arm identity was counted as terminal success");
                return;
            }
            Need(Status(store) == "running", "arm acknowledgment counted as completed plunder");
            foreach (TruckPlunderResultEvent invalid in new[]
            {
                new TruckPlunderResultEvent(317, "123456", scheduled.JobId, true,
                    BattleWon: true, AttemptId: "old-attempt", LaunchSessionId: "session-truck-006"),
                new TruckPlunderResultEvent(317, "123456", scheduled.JobId, true,
                    BattleWon: true, AttemptId: "attempt-truck-006", LaunchSessionId: "old-session"),
                new TruckPlunderResultEvent(318, "123456", scheduled.JobId, true,
                    BattleWon: true, AttemptId: "attempt-truck-006", LaunchSessionId: "session-truck-006"),
                new TruckPlunderResultEvent(317, "123456", scheduled.JobId, true, BattleWon: true),
                new TruckPlunderResultEvent(317, "123456", "reused-future-id", true,
                    BattleWon: true, AttemptId: "attempt-truck-006", LaunchSessionId: "session-truck-006"),
            })
            {
                results.Enqueue(invalid);
                await worker.RunTruckOnceAsync();
                Need(Status(store) == "running", "stale or absent identity terminalized a truck");
            }
            results.Enqueue(new TruckPlunderResultEvent(317, "123456", scheduled.JobId, true,
                BattleWon: true, AttemptId: "attempt-truck-006", LaunchSessionId: "session-truck-006"));
            await worker.RunTruckOnceAsync();
            Need(Status(store) == "succeeded", "exact owned terminal battle not accepted");
            using var reopened = new MapStore(Path.Combine(root, "truck.db"));
            Need(Status(reopened) == "succeeded", "positive result was not persisted");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    internal static async Task RunAsync()
    {
        await CaseAsync(false);
        await CaseAsync(true);
        Console.WriteLine("MAP_006_TRUCK_EXACT_SESSION_ATTEMPT_RESULT_GATE_OK");
    }
}
