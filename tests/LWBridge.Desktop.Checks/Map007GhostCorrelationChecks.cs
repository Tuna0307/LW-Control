using System.Text.Json;
using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Controlled production job worker + SQLite + dedicated Ghost contract.
// All game transports are inert; no official client request can be issued.
internal static class Map007GhostCorrelationChecks
{
    private static void Require(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("MAP007_GHOST_CORRELATION: " + reason);
    }

    internal static async Task RunAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), "lw-map007-ghost-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        const long now = 1_800_000_000_000;
        try
        {
            using var store = new MapStore(Path.Combine(path, "map.db"));
            var directGhost = new Queue<DispatchPlunderResultEvent>();
            var forgedDispatch = new Queue<DispatchPlunderResultEvent>();
            int ghostArms = 0, dispatchArms = 0;
            var provider = new MapActionProviderAdapter
            {
                GotoWorldCoordinate = (_, _, _, _) => ValueTask.CompletedTask,
                GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
                GetCurrentServerId = _ => ValueTask.FromResult(317),
                GotoServer = (_, _) => ValueTask.CompletedTask,
                InspectTreasureStates = (_, _, _) => throw new InvalidOperationException("no inspection"),
                GetTreasureClaimStatus = _ => throw new InvalidOperationException("no claim"),
                ClaimTreasures = (_, _) => throw new InvalidOperationException("no claim"),
                ShareDispatchTaskToAlliance = (_, _) => throw new InvalidOperationException("no share"),
                PrepareGhostPlunderTasks = (_, _) => throw new InvalidOperationException("no other preparation"),
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (jobs, _) =>
                {
                    dispatchArms++;
                    Require(jobs.All(job => job.Kind == "dispatch"), "Ghost entered Dispatch transport");
                    return ValueTask.FromResult<IReadOnlyList<DispatchPlunderArmResult>>(
                        jobs.Select(job => new DispatchPlunderArmResult("dispatch", job.ServerId, job.TaskUuid,
                            true, AttemptId: "dispatch-attempt", LaunchSessionId: "dispatch-session")).ToArray());
                },
                ArmGhostPlunder = (jobs, _) =>
                {
                    ghostArms++;
                    Require(jobs.Count == 1 && jobs[0].Kind == "ghost" && jobs[0].ServerId == 317 &&
                        jobs[0].OwnerServer == 318 && jobs[0].TaskUuid == "12345", "Ghost arm lost exact job identity");
                    return ValueTask.FromResult<IReadOnlyList<DispatchPlunderArmResult>>(
                        [new DispatchPlunderArmResult("ghost", 317, "12345", true,
                            AttemptId: "ghost-attempt-1", LaunchSessionId: "ghost-session-1")]);
                },
                ArmTruckPlunder = (_, _) => throw new InvalidOperationException("no truck"),
                DrainDispatchPlunderResults = _ =>
                {
                    var taken = forgedDispatch.ToArray(); forgedDispatch.Clear();
                    return ValueTask.FromResult<IReadOnlyList<DispatchPlunderResultEvent>>(taken);
                },
                DrainGhostPlunderResults = _ =>
                {
                    var taken = directGhost.ToArray(); directGhost.Clear();
                    return ValueTask.FromResult<IReadOnlyList<DispatchPlunderResultEvent>>(taken);
                },
                DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<TruckPlunderResultEvent>>([]),
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
            JsonElement? queued = store.ScheduleDispatchPlunderRow(317, "ghost:12345",
                "{\"uuid\":\"12345\",\"ownerServer\":318}", now - 5_000, now + 5_000, now + 60_000, now);
            Require(queued.HasValue, "ghost fixture could not be scheduled");
            var worker = new MapPlunderWorker(store, provider, () => now);
            await worker.RunDispatchOnceAsync();
            Require(ghostArms == 1 && dispatchArms == 0 && Status(store) == "running",
                "Ghost arm was not isolated from Dispatch");

            DispatchPlunderResultEvent[] wrong =
            [
                new("ghost", 317, "12345", true, OwnerServer: 318,
                    AttemptId: "ghost-attempt-1", LaunchSessionId: "ghost-session-1"),
                new("ghost", 317, "12345", true, OwnerServer: 318,
                    AttemptId: "old-attempt", LaunchSessionId: "ghost-session-1"),
                new("ghost", 317, "12345", true, OwnerServer: 318,
                    AttemptId: "ghost-attempt-1", LaunchSessionId: "old-session"),
                new("ghost", 319, "12345", true, OwnerServer: 318,
                    AttemptId: "ghost-attempt-1", LaunchSessionId: "ghost-session-1"),
                new("ghost", 317, "12345", true, OwnerServer: 999,
                    AttemptId: "ghost-attempt-1", LaunchSessionId: "ghost-session-1"),
            ];
            // Even perfectly matched Ghost-looking output from the Dispatch
            // command may not terminalize the dedicated Ghost pending handle.
            forgedDispatch.Enqueue(wrong[0]);
            await worker.RunDispatchOnceAsync();
            Require(Status(store) == "running", "Dispatch transport forged Ghost success");
            foreach (DispatchPlunderResultEvent negative in wrong.Skip(1))
            {
                directGhost.Enqueue(negative);
                await worker.RunDispatchOnceAsync();
                Require(Status(store) == "running", "stale/wrong Ghost result was accepted");
            }
            directGhost.Enqueue(wrong[0]);
            await worker.RunDispatchOnceAsync();
            Require(Status(store) == "succeeded", "exact dedicated Ghost terminal result was not accepted");
            using (var reopened = new MapStore(Path.Combine(path, "map.db")))
                Require(Status(reopened) == "succeeded", "verified Ghost result not persisted");
            Require(dispatchArms == 0, "Dispatch transport was touched by Ghost");

            // On subsequent attempt after cancellation, late old results must
            // not mutate cancelled durable storage or a newer owner.
            store.ScheduleDispatchPlunderRow(317, "ghost:54321",
                "{\"uuid\":\"54321\",\"ownerServer\":318}", now - 1_000, now + 8_000, now + 60_000, now);
            Require(store.CancelDispatchPlunder(317, "ghost:54321", now), "Ghost cancel failed");
            directGhost.Enqueue(new DispatchPlunderResultEvent("ghost", 317, "54321", true,
                OwnerServer: 318, AttemptId: "ghost-attempt-1", LaunchSessionId: "ghost-session-1"));
            await worker.RunDispatchOnceAsync();
            Require(store.ReadDispatchPlunderJobs().Single(row => row.GetProperty("uuid").GetString() == "54321")
                .GetProperty("scheduleStatus").GetString() == "cancelled", "late result revived canceled Ghost");
            Console.WriteLine("MAP_007_DEDICATED_GHOST_TRANSPORT_SESSION_ATTEMPT_INVERSES_OK");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    private static string Status(MapStore store) => store.ReadDispatchPlunderJobs().Single(
        row => row.GetProperty("uuid").GetString() == "12345")
        .GetProperty("scheduleStatus").GetString()!;
}
