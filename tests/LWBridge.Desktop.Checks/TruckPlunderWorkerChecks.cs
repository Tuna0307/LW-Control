using System.Runtime.CompilerServices;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class TruckPlunderWorkerChecks
{
    private const string MarchUuid = "7654321090123";
    private const long TrainUuid = 1_417_409_824_803_038_247L;

    [ModuleInitializer]
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-truck-worker-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SuccessfulOneShotPersists(root);
            TimeoutIsTerminalAndNeverRetried(root);
            BusyAndWrongServerDoNotConsumeAttempt(root);
            OfflineAndInvalidTargetTransitions(root);
            StaleRunningIsConservativelyTerminalized(root);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void SuccessfulOneShotPersists(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "success.db"));
        InsertScheduled(store, 88, MarchUuid, "truck-success", TrainUuid.ToString(), attempts: 2);

        int calls = 0;
        int enters = 0;
        int leaves = 0;
        int changed = 0;
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1_000);
        using var worker = new TruckPlunderWorker(
            store,
            () => 88,
            (serverId, marchUuid, trainUuid, _) =>
            {
                calls++;
                Check(serverId == 88 && marchUuid == long.Parse(MarchUuid) && trainUuid == TrainUuid,
                    "Truck worker preserves march UUID / real Train UUID identity split");
                return Task.FromResult(new CurrentClientTruckQuickRobResult(
                    88,
                    marchUuid,
                    trainUuid,
                    BattleWon: true,
                    RewardCount: 1,
                    PlunderRewards: JsonSerializer.SerializeToElement(new[]
                    {
                        new { key = "reward:1:1001", count = 25, rewardType = 1, itemId = 1001L },
                    }, JsonOptions.Default),
                    RewardNormalizationComplete: true,
                    DailyRobCount: 7));
            },
            () => { enters++; return true; },
            () => leaves++,
            () => now,
            startLoop: false);
        worker.Changed += () => changed++;

        worker.RunOnceForTestAsync().GetAwaiter().GetResult();

        JsonElement row = Active(store, MarchUuid);
        Check(calls == 1 && enters == 1 && leaves == 1,
            "Truck worker executes the selected job exactly once under one operation owner");
        Check(row.GetProperty("scheduleStatus").GetString() == "succeeded" &&
              row.GetProperty("attempts").GetInt32() == 3 &&
              row.GetProperty("battleWon").GetBoolean() &&
              row.GetProperty("dailyRobCount").GetInt32() == 7 &&
              row.GetProperty("plunderRewards").GetArrayLength() == 1 &&
              changed >= 2,
            "Truck worker marks running once then persists the authoritative success result and change notifications");
    }

    private static void TimeoutIsTerminalAndNeverRetried(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "timeout.db"));
        InsertScheduled(store, 88, MarchUuid, "truck-timeout", TrainUuid.ToString(), attempts: 0);

        int calls = 0;
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(1_000);
        using var worker = new TruckPlunderWorker(
            store,
            () => 88,
            (_, _, _, _) =>
            {
                calls++;
                throw new BridgeCommandException(
                    "TRUCK_PLUNDER_RESPONSE_TIMEOUT",
                    "server response timeout",
                    new { requestSent = true, ambiguous = true });
            },
            () => true,
            () => { },
            () => now,
            startLoop: false);

        worker.RunOnceForTestAsync().GetAwaiter().GetResult();
        worker.RunOnceForTestAsync().GetAwaiter().GetResult();

        JsonElement row = Active(store, MarchUuid);
        Check(calls == 1 &&
              row.GetProperty("scheduleStatus").GetString() == "failed" &&
              row.GetProperty("attempts").GetInt32() == 1 &&
              row.GetProperty("lastError").GetString() == "server response timeout",
            "sent Truck response timeout is terminal failed and is never retried on a later worker tick");
    }

    private static void BusyAndWrongServerDoNotConsumeAttempt(string root)
    {
        using (var store = new MapDataStore(Path.Combine(root, "busy.db")))
        {
            InsertScheduled(store, 88, MarchUuid, "truck-busy", TrainUuid.ToString(), attempts: 4);
            int calls = 0;
            using var worker = new TruckPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () => false,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, MarchUuid);
            Check(calls == 0 &&
                  row.GetProperty("scheduleStatus").GetString() == "scheduled" &&
                  row.GetProperty("attempts").GetInt32() == 4,
                "busy game-operation ownership defers Truck execution without consuming an attempt");
        }

        using (var store = new MapDataStore(Path.Combine(root, "wrong-server.db")))
        {
            InsertScheduled(store, 88, MarchUuid, "truck-wrong-server", TrainUuid.ToString(), attempts: 1);
            int calls = 0;
            using var worker = new TruckPlunderWorker(
                store,
                () => 89,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, MarchUuid);
            Check(calls == 0 &&
                  row.GetProperty("scheduleStatus").GetString() == "scheduled" &&
                  row.GetProperty("attempts").GetInt32() == 1,
                "Truck worker never performs an implicit server jump or attacks a job from a different live server");
        }
    }

    private static void OfflineAndInvalidTargetTransitions(string root)
    {
        using (var store = new MapDataStore(Path.Combine(root, "offline.db")))
        {
            InsertScheduled(store, 88, MarchUuid, "truck-offline", TrainUuid.ToString(), attempts: 2);
            using var worker = new TruckPlunderWorker(
                store,
                () => null,
                (_, _, _, _) => throw new InvalidOperationException(),
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, MarchUuid);
            Check(row.GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
                  row.GetProperty("lastError").GetString() == "game disconnected" &&
                  row.GetProperty("attempts").GetInt32() == 2,
                "offline due Truck job moves to recovered waiting_connection state without consuming an attempt");
        }

        using (var store = new MapDataStore(Path.Combine(root, "invalid.db")))
        {
            store.UpsertTruckPlunderJobForTest(
                88,
                MarchUuid,
                $$"""{"uuid":"{{MarchUuid}}","serverId":88,"jobId":"truck-invalid"}""",
                executeAt: 1_000,
                expireAt: 20_000,
                status: "scheduled",
                attempts: 5,
                lastError: null,
                createdAt: 500,
                updatedAt: 500);
            int calls = 0;
            using var worker = new TruckPlunderWorker(
                store,
                () => 88,
                (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
                () => true,
                () => { },
                () => DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                startLoop: false);

            worker.RunOnceForTestAsync().GetAwaiter().GetResult();
            JsonElement row = Active(store, MarchUuid);
            Check(calls == 0 &&
                  row.GetProperty("scheduleStatus").GetString() == "failed" &&
                  row.GetProperty("lastError").GetString() == "invalid scheduled target" &&
                  row.GetProperty("attempts").GetInt32() == 5,
                "invalid persisted Truck target fails before running and before any attempt increment");
        }
    }

    private static void StaleRunningIsConservativelyTerminalized(string root)
    {
        using var store = new MapDataStore(Path.Combine(root, "restart.db"));
        store.UpsertTruckPlunderJobForTest(
            88,
            MarchUuid,
            $$"""{"uuid":"{{MarchUuid}}","serverId":88,"jobId":"truck-running","trainUuid":"{{TrainUuid}}"}""",
            executeAt: 1_000,
            expireAt: 20_000,
            status: "running",
            attempts: 3,
            lastError: null,
            createdAt: 500,
            updatedAt: 900);

        int calls = 0;
        using var worker = new TruckPlunderWorker(
            store,
            () => 88,
            (_, _, _, _) => { calls++; throw new InvalidOperationException(); },
            () => true,
            () => { },
            () => DateTimeOffset.FromUnixTimeMilliseconds(1_100),
            startLoop: false);

        JsonElement row = Active(store, MarchUuid);
        Check(calls == 0 &&
              row.GetProperty("scheduleStatus").GetString() == "failed" &&
              row.GetProperty("attempts").GetInt32() == 3 &&
              row.GetProperty("lastError").GetString() ==
                  "truck plunder execution state is unknown after client restart",
            "stale running Truck job is terminalized on restart instead of being made retryable");
    }

    private static void InsertScheduled(
        MapDataStore store,
        int serverId,
        string marchUuid,
        string jobId,
        string trainUuid,
        int attempts)
    {
        store.UpsertTruckPlunderJobForTest(
            serverId,
            marchUuid,
            $$"""{"uuid":"{{marchUuid}}","marchUuid":"{{marchUuid}}","trainUuid":"{{trainUuid}}","serverId":{{serverId}},"jobId":"{{jobId}}","robTimes":0,"maxLootCount":2}""",
            executeAt: 1_000,
            expireAt: 20_000,
            status: "scheduled",
            attempts,
            lastError: null,
            createdAt: 500,
            updatedAt: 500);
    }

    private static JsonElement Active(MapDataStore store, string uuid) =>
        store.ReadPlunderJobs().TruckJobs.Single(row =>
            row.GetProperty("uuid").GetString() == uuid &&
            row.GetProperty("scheduleStatus").GetString() is "scheduled" or "waiting_connection" or "running" or "succeeded" or "failed");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
