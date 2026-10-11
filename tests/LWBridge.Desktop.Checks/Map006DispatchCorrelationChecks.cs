using System.Text.Json;
using LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Actual scheduled-plunder worker and SQLite with only action-provider responses
// controlled. Does not execute any live plunder or claim action.
internal static class Map006DispatchCorrelationChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP_006_DISPATCH: " + message);
    }

    private sealed class FakeBoundary
    {
        internal string ArmMode = "valid";
        internal readonly Queue<DispatchPlunderResultEvent> Events = new();

        internal IMapActionProvider Provider() => new MapActionProviderAdapter
        {
            GotoWorldCoordinate = (_, _, _, _) => ValueTask.CompletedTask,
            GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
            GetCurrentServerId = _ => ValueTask.FromResult(317),
            GotoServer = (_, _) => ValueTask.CompletedTask,
            InspectTreasureStates = (_, _, _) => ValueTask.FromResult(
                new TreasureInspectionResult("", "", Array.Empty<JsonElement>())),
            GetTreasureClaimStatus = _ => ValueTask.FromException<JsonElement>(
                new InvalidOperationException("No Treasure claim action permitted")),
            ClaimTreasures = (_, _) => ValueTask.FromException<JsonElement>(
                new InvalidOperationException("No Treasure claim action permitted")),
            ShareDispatchTaskToAlliance = (_, _) => ValueTask.FromException<DispatchShareProviderResult>(
                new InvalidOperationException("No sharing permitted")),
            PrepareGhostPlunderTasks = (_, _) => ValueTask.FromException<IReadOnlyList<JsonElement>>(
                new InvalidOperationException("Ghost preparation not available")),
            GetMapPlunderServerDayStart = _ => ValueTask.FromResult<MapPlunderServerDayProviderResult?>(null),
            ArmDispatchPlunder = (jobs, _) =>
            {
                Require(jobs.Count == 1 && jobs[0].Kind == "dispatch" &&
                    jobs[0].ServerId == 317 && jobs[0].OwnerServer == 317 &&
                    jobs[0].TaskUuid == "12345", "invalid controlled dispatch job");
                var result = new DispatchPlunderArmResult("dispatch", 317, "12345", true,
                    AttemptId: ArmMode == "missing" ? null : "attempt-006",
                    LaunchSessionId: ArmMode == "missing" ? null : "launch-006");
                return ValueTask.FromResult<IReadOnlyList<DispatchPlunderArmResult>>(
                    ArmMode == "duplicate" ? [result, result] : [result]);
            },
            ArmTruckPlunder = (_, _) => ValueTask.FromException<TruckPlunderArmResult>(
                new InvalidOperationException("No truck action permitted")),
            DrainDispatchPlunderResults = _ =>
            {
                var drained = Events.ToArray();
                Events.Clear();
                return ValueTask.FromResult<IReadOnlyList<DispatchPlunderResultEvent>>(drained);
            },
            DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<TruckPlunderResultEvent>>(
                Array.Empty<TruckPlunderResultEvent>()),
            ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
        };
    }

    private static string Status(MapStore store) =>
        store.ReadDispatchPlunderJobs().Single().GetProperty("scheduleStatus").GetString()!;

    private static async Task RunCaseAsync(string armMode)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-dispatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        const long now = 1_800_000_000_000;
        try
        {
            using var store = new MapStore(Path.Combine(root, "data.db"));
            JsonElement? scheduled = store.ScheduleDispatchPlunderRow(317, "12345",
                "{\"uuid\":\"12345\",\"ownerServer\":317}",
                now - 2000, now + 5000, now + 60000, now);
            Require(scheduled is not null, "fixture failed to schedule exact task");
            var controlled = new FakeBoundary { ArmMode = armMode };
            var worker = new MapPlunderWorker(store, controlled.Provider(), () => now);
            await worker.RunDispatchOnceAsync();
            if (armMode != "valid")
            {
                Require(Status(store) == "failed" &&
                    store.ReadDispatchPlunderJobs()[0].GetProperty("lastError").GetString() ==
                    (armMode == "missing" ? "DISPATCH_PLUNDER_CORRELATION_UNAVAILABLE" :
                        "DISPATCH_PLUNDER_AMBIGUOUS_ARM_RESULT"),
                    armMode + ": ambiguous/missing arm was recorded as an executable success");
                return;
            }

            Require(Status(store) == "running", "real worker did not arm controlled target");
            var invalidEvents = new[]
            {
                new DispatchPlunderResultEvent("dispatch", 317, "12345", true,
                    OwnerServer: 317, AttemptId: "old-attempt", LaunchSessionId: "launch-006"),
                new DispatchPlunderResultEvent("dispatch", 317, "12345", true,
                    OwnerServer: 317, AttemptId: "attempt-006", LaunchSessionId: "old-launch"),
                new DispatchPlunderResultEvent("dispatch", 317, "12345", true,
                    OwnerServer: 318, AttemptId: "attempt-006", LaunchSessionId: "launch-006"),
                new DispatchPlunderResultEvent("dispatch", 317, "12345", true,
                    OwnerServer: 317),
                new DispatchPlunderResultEvent("ghost", 317, "12345", true,
                    OwnerServer: 317, AttemptId: "attempt-006", LaunchSessionId: "launch-006"),
            };
            foreach (DispatchPlunderResultEvent invalid in invalidEvents)
            {
                controlled.Events.Enqueue(invalid);
                await worker.RunDispatchOnceAsync();
                Require(Status(store) == "running",
                    "stale/missing/cross-kind/server identity falsely terminalized owned job");
            }

            controlled.Events.Enqueue(new DispatchPlunderResultEvent(
                "dispatch", 317, "12345", true, OwnerServer: 317,
                AttemptId: "attempt-006", LaunchSessionId: "launch-006"));
            await worker.RunDispatchOnceAsync();
            Require(Status(store) == "succeeded", "exact authoritative identity was not accepted");
            using var reopened = new MapStore(Path.Combine(root, "data.db"));
            Require(Status(reopened) == "succeeded", "verified terminal result not persisted on reopen");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    internal static async Task RunAsync()
    {
        await RunCaseAsync("valid");
        await RunCaseAsync("missing");
        await RunCaseAsync("duplicate");
        Console.WriteLine("MAP_006_DISPATCH_TERMINAL_ATTEMPT_SESSION_CORRELATION_OK");
    }
}
