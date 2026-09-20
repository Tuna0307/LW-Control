using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class DispatchPlunderWorkerChecks
{
    private const string TaskUuid = "1417409824803038247";

    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-dispatch-worker-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SuccessfulCrossServerOneShotPersists(root);
            DailyLimitStopsRemainingJobs(root);
            AmbiguousTimeoutIsTerminalAndNeverRetried(root);
            BusyAndCancelRaceDoNotConsumeAttempt(root);
            OfflineAndInvalidTargetTransitions(root);
            PreSendDisconnectAndConnectedTimeoutSplit(root);
            StaleRunningIsConservativelyTerminalized(root);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void SuccessfulCrossServerOneShotPersists(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "success.db"));
        InsertScheduled(store, 88, TaskUuid, attempts: 2, plunderAt: 1_000);

        int calls = 0;
        int enters = 0;
        int leaves = 0;
        int changed = 0;
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1_000);
        using var worker = new DispatchPlunderWorker(
            store,
            () => 99,
            (serverId, taskUuid, executeAt, _) =>
            {
                calls++;
                Check(
                    serverId == 88 &&
                    taskUuid == TaskUuid &&
                    executeAt == 1_000,
                    "Dispatch worker preserves target server/task UUID/executeAt and does not require live-server equality");
                return Task.FromResult(new CurrentClientDispatchPlunderResult(
                    serverId,
                    taskUuid,
                    Succeeded: true,
                    ErrorCode: null,
                    RequestSent: true));
            },
            () => { enters++; return true; },
            () => leaves++,
            () => now,
            startLoop: false);
        worker.Changed += () => changed++;

        worker.RunOnceForTestAsync().GetAwaiter().GetResult();

        JsonElement row = Active(store, TaskUuid);
        Check(calls == 1 && enters == 1 && leaves == 1,
            "Dispatch worker executes the selected cross-server-capable job exactly once under one operation owner");
        Check(row.GetProperty("scheduleStatus").GetString() == "succeeded" &&
              row.GetProperty("attempts").GetInt32() == 3 &&
              row.GetProperty("lastError").ValueKind == JsonValueKind.Null &&
              changed >= 2,
            "Dispatch worker enters running with one attempt increment then persists authoritative success");
    }

    private static void DailyLimitStopsRemainingJobs(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "daily.db"));
        InsertScheduled(store, 88, TaskUuid, attempts: 0, plunderAt: 1_000);
        InsertScheduled(store, 89, "1417409824803038248", attempts: 4, plunderAt: 1_100);

        int calls = 0;
        using var worker = new DispatchPlunderWorker(
            store,
            () => 88,
            (serverId, taskUuid, _, _) =>
            {
                calls++;
                return Task.FromResult(new CurrentClientDispatchPlunderResult(
                    serverId,
                    taskUuid,
                    Succeeded: false,
                    ErrorCode: "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED",
                    RequestSent: true));
            },
            () => true,
            () => { },
            () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
            startLoop: false);

        worker.RunOnceForTestAsync().GetAwaiter().GetResult();

        JsonElement first = Active(store, TaskUuid);
        JsonElement second = Active(store, "1417409824803038248");
        Check(calls == 1 &&
              first.GetProperty("scheduleStatus").GetString() == "failed" &&
              first.GetProperty("attempts").GetInt32() == 1 &&
              first.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED" &&
              second.GetProperty("scheduleStatus").GetString() == "failed" &&
              second.GetProperty("attempts").GetInt32() == 4 &&
              second.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED",
            "authoritative Dispatch daily-limit result fails the current attempt and terminalizes all remaining active jobs");
    }

    private static void AmbiguousTimeoutIsTerminalAndNeverRetried(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "ambiguous.db"));
        InsertScheduled(store, 88, TaskUuid, attempts: 0, plunderAt: 1_000);

        int calls = 0;
        using var worker = new DispatchPlunderWorker(
            store,
            () => 88,
            (_, _, _, _) =>
            {
                calls++;
                throw new BridgeCommandException(
                    "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
                    "server response timeout",
                    new { requestSent = true, ambiguous = true });
            },
            () => true,
            () => { },
            () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
            startLoop: false);

        worker.RunOnceForTestAsync().GetAwaiter().GetResult();
        worker.RunOnceForTestAsync().GetAwaiter().GetResult();

        JsonElement row = Active(store, TaskUuid);
        Check(calls == 1 &&
              row.GetProperty("scheduleStatus").GetString() == "failed" &&
              row.GetProperty("attempts").GetInt32() == 1 &&
              row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
            "post-send Dispatch timeout is terminal failed and can never be automatically retried");
    }

    private static void BusyAndCancelRaceDoNotConsumeAttempt(string root)
    {
        using (var store = new MapDataStore(Path.Combine(root, "busy.db")))
        {
            InsertScheduled(store, 88, TaskUuid, attempts: 4, plunderAt: 1_000);
            int calls = 0;
            using var worker = new DispatchPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () => false,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(calls == 0 &&
                  row.GetProperty("scheduleStatus").GetString() == "scheduled" &&
                  row.GetProperty("attempts").GetInt32() == 4,
                "busy game-operation ownership defers Dispatch without consuming an attempt");
        }

        using (var store = new MapDataStore(Path.Combine(root, "cancel-race.db")))
        {
            InsertScheduled(store, 88, TaskUuid, attempts: 2, plunderAt: 1_000);
            int calls = 0;
            int leaves = 0;
            using var worker = new DispatchPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () =>
                {
                    Check(store.CancelDispatchPlunder(88, TaskUuid, 1_001),
                        "synthetic cancel wins the race before running transition");
                    return true;
                },
                () => leaves++,
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(calls == 0 && leaves == 1 &&
                  row.GetProperty("scheduleStatus").GetString() == "cancelled" &&
                  row.GetProperty("attempts").GetInt32() == 2,
                "guarded Dispatch running transition cannot resurrect a concurrently cancelled job");
        }
    }

    private static void OfflineAndInvalidTargetTransitions(string root)
    {
        using (var store = new MapDataStore(Path.Combine(root, "offline.db")))
        {
            InsertScheduled(store, 88, TaskUuid, attempts: 2, plunderAt: 1_000);
            using var worker = new DispatchPlunderWorker(
                store,
                () => null,
                (_, _, _, _) => throw new InvalidOperationException(),
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(row.GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
                  row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_GAME_DISCONNECTED" &&
                  row.GetProperty("attempts").GetInt32() == 2,
                "offline due Dispatch job moves to waiting_connection without consuming an attempt");
        }

        using (var store = new MapDataStore(Path.Combine(root, "offline-lead.db")))
        {
            InsertScheduled(store, 88, TaskUuid, attempts: 1, plunderAt: 5_000);
            using var worker = new DispatchPlunderWorker(
                store,
                () => null,
                (_, _, _, _) => throw new InvalidOperationException(),
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(row.GetProperty("scheduleStatus").GetString() == "scheduled" &&
                  row.GetProperty("attempts").GetInt32() == 1,
                "offline Dispatch deferral uses due time only, not the recovered ten-second arm lead");
        }

        using (var store = new MapDataStore(Path.Combine(root, "invalid.db")))
        {
            const string InvalidUuid = "0012345";
            InsertScheduled(store, 88, InvalidUuid, attempts: 5, plunderAt: 1_000);
            int calls = 0;
            using var worker = new DispatchPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, InvalidUuid);
            Check(calls == 0 &&
                  row.GetProperty("scheduleStatus").GetString() == "failed" &&
                  row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_INVALID_TARGET" &&
                  row.GetProperty("attempts").GetInt32() == 5,
                "current-v19-incompatible persisted Dispatch identity fails before running and before attempt increment");
        }

        using (var store = new MapDataStore(Path.Combine(root, "wide-server.db")))
        {
            InsertScheduled(store, 100_000L, TaskUuid, attempts: 4, plunderAt: 1_000);
            int calls = 0;
            using var worker = new DispatchPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(calls == 0 &&
                  row.GetProperty("scheduleStatus").GetString() == "failed" &&
                  row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_INVALID_TARGET" &&
                  row.GetProperty("attempts").GetInt32() == 4,
                "original positive-i64 Dispatch server identity remains persistable but fails before a current-v19 execution attempt when outside the proven transport domain");
        }
    }

    private static void PreSendDisconnectAndConnectedTimeoutSplit(string root)
    {
        using (var store = new MapDataStore(Path.Combine(root, "disconnect-arm.db")))
        {
            InsertScheduled(store, 88, TaskUuid, attempts: 0, plunderAt: 1_000);
            int liveChecks = 0;
            using var worker = new DispatchPlunderWorker(
                store,
                () => ++liveChecks == 1 ? 88 : null,
                (_, _, _, _) => throw new BridgeCommandException(
                    "DISPATCH_PLUNDER_GAME_DISCONNECTED",
                    "game disconnected",
                    new { requestSent = false, ambiguous = false }),
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(row.GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
                  row.GetProperty("attempts").GetInt32() == 1 &&
                  row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_GAME_DISCONNECTED",
                "proven pre-send Dispatch arm failure becomes retryable only when the live connection is actually gone");
        }

        using (var store = new MapDataStore(Path.Combine(root, "connected-arm-timeout.db")))
        {
            InsertScheduled(store, 88, TaskUuid, attempts: 0, plunderAt: 1_000);
            using var worker = new DispatchPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => throw new BridgeCommandException(
                    "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
                    "server response timeout",
                    new { requestSent = false, ambiguous = false }),
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, TaskUuid);
            Check(row.GetProperty("scheduleStatus").GetString() == "failed" &&
                  row.GetProperty("attempts").GetInt32() == 1 &&
                  row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
                "pre-send Dispatch arm timeout while still connected is terminal failed like the recovered original");
        }
    }

    private static void StaleRunningIsConservativelyTerminalized(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "restart.db"));
        store.UpsertDispatchPlunderJobForTest(
            88,
            TaskUuid,
            TaskJson(88, TaskUuid, 1_000),
            completionTime: 500,
            plunderAt: 1_000,
            expireAt: 20_000,
            status: "running",
            attempts: 3,
            lastError: null,
            createdAt: 500,
            updatedAt: 900);

        int calls = 0;
        using var worker = new DispatchPlunderWorker(
            store,
            () => 88,
            (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
            () => true,
            () => { },
            () => DateTimeOffset.FromUnixTimeMilliseconds(1_100),
            startLoop: false);

        JsonElement row = Active(store, TaskUuid);
        Check(calls == 0 &&
              row.GetProperty("scheduleStatus").GetString() == "failed" &&
              row.GetProperty("attempts").GetInt32() == 3 &&
              row.GetProperty("lastError").GetString() == "DISPATCH_PLUNDER_CLIENT_RESTARTED",
            "stale running Dispatch job is conservatively terminalized after restart to prevent duplicate plunder");
    }

    private static void InsertScheduled(
        MapDataStore store,
        long serverId,
        string taskUuid,
        int attempts,
        long plunderAt)
    {
        store.UpsertDispatchPlunderJobForTest(
            serverId,
            taskUuid,
            TaskJson(serverId, taskUuid, plunderAt),
            completionTime: 500,
            plunderAt,
            expireAt: 20_000,
            status: "scheduled",
            attempts,
            lastError: null,
            createdAt: 500,
            updatedAt: 500);
    }

    private static string TaskJson(
        long serverId,
        string taskUuid,
        long plunderAt) =>
        $$"""{"uuid":"{{taskUuid}}","serverId":{{serverId}},"completionTime":500,"plunderAt":{{plunderAt}},"taskExpireTime":20000,"stolenCount":0,"maxStealCount":3}""";

    private static JsonElement Active(MapDataStore store, string uuid) =>
        store.ReadPlunderJobs().DispatchJobs.Single(row =>
            row.GetProperty("uuid").GetString() == uuid);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
