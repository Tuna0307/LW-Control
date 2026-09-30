using System.Text.Json;

namespace LWBridge.Map317.Checks;

internal static class PlunderWorkerChecks
{
    internal static void Run()
    {
        DispatchBatchArmIsNonTerminalUntilResult();
        DispatchDailyLimitFansOutOnlyOnResult();
        DispatchTimeoutDisconnectAndRestartAreExact();
        TruckUuidIsTrainUuidAndResultMerges();
        TruckStaleResultTimeoutClearAndMalformedTargetAreExact();
        TruckDisconnectAndRestartAreExact();
        ServerDayRefreshCachesAndPrunes();
    }

    private static void DispatchBatchArmIsNonTerminalUntilResult()
    {
        long now = 1_000;
        using MapStore store = MapStore.CreateInMemory();
        var provider = new WorkerProvider { CurrentServerId = 10 };
        var actions = new MapActionControlPlane(store, provider, () => now);
        actions.ScheduleDispatchPlunderAsync([
            Dispatch("101", "dispatch", 1_200),
            Dispatch("102", "dispatch", 1_300),
        ]).AsTask().GetAwaiter().GetResult();

        var worker = new MapPlunderWorker(store, provider, () => now);
        worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();

        TestAssert.Equal(1, provider.DispatchArmBatches.Count, "dispatch jobs must arm in one provider batch");
        IReadOnlyList<DispatchPlunderArmJob> batch = provider.DispatchArmBatches[0];
        TestAssert.Equal(2, batch.Count, "dispatch arm batch size mismatch");
        TestAssert.True(batch.All(job => job.Kind == "dispatch" && job.ServerId == 10 && job.OwnerServer == 10),
            "dispatch arm identity mismatch");
        TestAssert.True(batch.Select(job => job.TaskUuid).SequenceEqual(["101", "102"]),
            "dispatch arm UUID order mismatch");

        JsonElement firstRunning = DispatchRow(actions, "101");
        JsonElement secondRunning = DispatchRow(actions, "102");
        TestAssert.Equal("running", firstRunning.GetProperty("scheduleStatus").GetString(),
            "successful arm must remain non-terminal");
        TestAssert.Equal("running", secondRunning.GetProperty("scheduleStatus").GetString(),
            "second successful arm must remain non-terminal");
        TestAssert.Equal(1, firstRunning.GetProperty("attempts").GetInt32(),
            "dispatch arm must consume exactly one attempt");
        TestAssert.Equal(1, secondRunning.GetProperty("attempts").GetInt32(),
            "dispatch batch arm must consume one attempt per job");

        provider.DispatchResultBatches.Enqueue([
            new DispatchPlunderResultEvent(
                "dispatch", 10, "101", true, ServerDayStartAt: 900),
        ]);
        worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();

        JsonElement firstDone = DispatchRow(actions, "101");
        JsonElement secondPending = DispatchRow(actions, "102");
        TestAssert.Equal("succeeded", firstDone.GetProperty("scheduleStatus").GetString(),
            "dispatch result event must own terminal success");
        TestAssert.Equal("running", secondPending.GetProperty("scheduleStatus").GetString(),
            "unmatched dispatch must remain pending");
        TestAssert.Equal(1, firstDone.GetProperty("attempts").GetInt32(),
            "dispatch result must not consume another attempt");
        TestAssert.Equal(JsonSerializer.Serialize(900L), store.ReadSetting("map_plunder_server_day_start"),
            "dispatch result server day was not cached");
    }

    private static void DispatchDailyLimitFansOutOnlyOnResult()
    {
        long now = 5_000;
        using MapStore store = MapStore.CreateInMemory();
        var provider = new WorkerProvider { CurrentServerId = 10 };
        var actions = new MapActionControlPlane(store, provider, () => now);
        actions.ScheduleDispatchPlunderAsync([Dispatch("201", "dispatch", 5_200)])
            .AsTask().GetAwaiter().GetResult();
        var worker = new MapPlunderWorker(store, provider, () => now);
        worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();

        actions.ScheduleDispatchPlunderAsync([
            Dispatch("202", "dispatch", 5_300),
            Dispatch("203", "ghost", 5_400),
        ]).AsTask().GetAwaiter().GetResult();

        TestAssert.Equal("running", DispatchRow(actions, "201").GetProperty("scheduleStatus").GetString(),
            "daily-limit source must still be pending before its result event");
        TestAssert.Equal("scheduled", DispatchRow(actions, "202").GetProperty("scheduleStatus").GetString(),
            "daily-limit fanout must not happen at arm time");

        provider.DispatchResultBatches.Enqueue([
            new DispatchPlunderResultEvent(
                "dispatch", 10, "201", false, "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED"),
        ]);
        worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();

        JsonElement source = DispatchRow(actions, "201");
        JsonElement fanout = DispatchRow(actions, "202");
        JsonElement ghost = DispatchRow(actions, "203");
        TestAssert.Equal("failed", source.GetProperty("scheduleStatus").GetString(),
            "daily-limit result source mismatch");
        TestAssert.Equal("DISPATCH_PLUNDER_DAILY_LIMIT_REACHED", source.GetProperty("lastError").GetString(),
            "daily-limit source error mismatch");
        TestAssert.Equal("failed", fanout.GetProperty("scheduleStatus").GetString(),
            "daily-limit result must stop other active Dispatch rows");
        TestAssert.Equal(0, fanout.GetProperty("attempts").GetInt32(),
            "daily-limit fanout must not consume an unarmed row attempt");
        TestAssert.Equal("running", ghost.GetProperty("scheduleStatus").GetString(),
            "daily-limit fanout must exclude Ghost rows");
        TestAssert.Equal(1, ghost.GetProperty("attempts").GetInt32(),
            "Ghost row should arm normally after Dispatch daily-limit fanout");
    }

    private static void DispatchTimeoutDisconnectAndRestartAreExact()
    {
        long now = 10_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            actions.ScheduleDispatchPlunderAsync([Dispatch("301", "dispatch", 10_100)])
                .AsTask().GetAwaiter().GetResult();
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();

            now = 25_099;
            worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();
            TestAssert.Equal("running", DispatchRow(actions, "301").GetProperty("scheduleStatus").GetString(),
                "dispatch must remain pending before exact 15s horizon");
            now = 25_100;
            worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();
            JsonElement timedOut = DispatchRow(actions, "301");
            TestAssert.Equal("failed", timedOut.GetProperty("scheduleStatus").GetString(),
                "dispatch 15s pending timeout status mismatch");
            TestAssert.Equal("DISPATCH_PLUNDER_RESPONSE_TIMEOUT", timedOut.GetProperty("lastError").GetString(),
                "dispatch 15s pending timeout error mismatch");
            TestAssert.Equal(1, timedOut.GetProperty("attempts").GetInt32(),
                "dispatch timeout must preserve the single consumed attempt");
        }

        now = 30_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            actions.ScheduleDispatchPlunderAsync([Dispatch("302", "dispatch", 30_100)])
                .AsTask().GetAwaiter().GetResult();
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();
            provider.Offline = true;
            worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();
            JsonElement disconnected = DispatchRow(actions, "302");
            TestAssert.Equal("waiting_connection", disconnected.GetProperty("scheduleStatus").GetString(),
                "dispatch disconnect status mismatch");
            TestAssert.Equal("DISPATCH_PLUNDER_GAME_DISCONNECTED", disconnected.GetProperty("lastError").GetString(),
                "dispatch disconnect error mismatch");
            TestAssert.Equal(1, disconnected.GetProperty("attempts").GetInt32(),
                "dispatch disconnect must not consume a second attempt");
        }

        now = 40_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            actions.ScheduleDispatchPlunderAsync([Dispatch("303", "dispatch", 40_100)])
                .AsTask().GetAwaiter().GetResult();
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunDispatchOnceAsync().AsTask().GetAwaiter().GetResult();
            worker.RecoverAfterRestart();
            JsonElement restarted = DispatchRow(actions, "303");
            TestAssert.Equal("waiting_connection", restarted.GetProperty("scheduleStatus").GetString(),
                "dispatch restart status mismatch");
            TestAssert.Equal("DISPATCH_PLUNDER_CLIENT_RESTARTED", restarted.GetProperty("lastError").GetString(),
                "dispatch restart error mismatch");
            TestAssert.Equal(1, restarted.GetProperty("attempts").GetInt32(),
                "dispatch restart must preserve consumed attempt");
        }
    }

    private static void TruckUuidIsTrainUuidAndResultMerges()
    {
        long now = 50_000;
        using MapStore store = MapStore.CreateInMemory();
        var provider = new WorkerProvider { CurrentServerId = 10 };
        var actions = new MapActionControlPlane(store, provider, () => now);
        TruckPlunderScheduleResult scheduled = actions.ScheduleTruckPlunder([
            Truck("401", 50_100, extraTrainUuid: "999999"),
        ]).Single();
        var worker = new MapPlunderWorker(store, provider, () => now);

        worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();
        TestAssert.Equal(1, provider.TruckArmCalls.Count, "truck arm call count mismatch");
        TruckPlunderArmJob arm = provider.TruckArmCalls[0];
        TestAssert.Equal("401", arm.TrainUuid, "Truck row uuid must be the provider trainUuid");
        TestAssert.Equal(scheduled.JobId, arm.JobId, "truck arm jobId mismatch");
        TestAssert.Equal(50_100L, arm.ExecuteAt, "truck arm executeAt mismatch");
        TestAssert.Equal(0, arm.RobTimes, "truck arm robTimes mismatch");
        TestAssert.Equal(3, arm.MaxLootCount, "truck arm maxLootCount mismatch");

        JsonElement pending = TruckRow(actions, "401");
        TestAssert.Equal("running", pending.GetProperty("scheduleStatus").GetString(),
            "truck arm success must remain non-terminal");
        TestAssert.Equal(1, pending.GetProperty("attempts").GetInt32(),
            "truck arm must consume one attempt");

        JsonElement rewards = JsonSerializer.SerializeToElement(new[]
        {
            new { key = "item:7", name = "Item", iconPath = "icon", count = 2, rewardType = 1, itemId = 7 },
        });
        provider.TruckResultBatches.Enqueue([
            new TruckPlunderResultEvent(
                10, "401", scheduled.JobId, true,
                BattleWon: true,
                PlunderRewards: rewards,
                RobTimes: 1,
                RemainingLootCount: 2,
                DailyRobCount: 3),
        ]);
        worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();

        JsonElement merged = TruckRow(actions, "401");
        TestAssert.Equal("succeeded", merged.GetProperty("scheduleStatus").GetString(),
            "truck result event must own terminal success");
        TestAssert.Equal(1, merged.GetProperty("attempts").GetInt32(),
            "truck result must not consume another attempt");
        TestAssert.True(merged.GetProperty("battleWon").GetBoolean(), "truck battle outcome merge missing");
        TestAssert.Equal(1, merged.GetProperty("robTimes").GetInt32(), "truck robTimes merge missing");
        TestAssert.Equal(2, merged.GetProperty("remainingLootCount").GetInt32(),
            "truck remaining-loot merge missing");
        TestAssert.Equal(3, merged.GetProperty("dailyRobCount").GetInt32(),
            "truck daily count merge missing");
        TestAssert.Equal(1, merged.GetProperty("plunderRewards").GetArrayLength(),
            "truck reward merge missing");
    }

    private static void TruckStaleResultTimeoutClearAndMalformedTargetAreExact()
    {
        long now = 60_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            TruckPlunderScheduleResult scheduled = actions.ScheduleTruckPlunder([Truck("501", 60_100)]).Single();
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();

            provider.TruckResultBatches.Enqueue([
                new TruckPlunderResultEvent(10, "501", scheduled.JobId + "-stale", true, BattleWon: true),
            ]);
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();
            TestAssert.Equal("running", TruckRow(actions, "501").GetProperty("scheduleStatus").GetString(),
                "stale Truck result jobId must be ignored");

            now = 90_099;
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();
            TestAssert.Equal("running", TruckRow(actions, "501").GetProperty("scheduleStatus").GetString(),
                "Truck must remain pending before exact 30s horizon");
            TestAssert.Equal(0, provider.ClearTruckCalls.Count,
                "Truck pending clear must not run before timeout");

            now = 90_100;
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();
            JsonElement timedOut = TruckRow(actions, "501");
            TestAssert.Equal("failed", timedOut.GetProperty("scheduleStatus").GetString(),
                "Truck 30s timeout status mismatch");
            TestAssert.Equal("server response timeout", timedOut.GetProperty("lastError").GetString(),
                "Truck 30s timeout error mismatch");
            TestAssert.Equal(1, timedOut.GetProperty("attempts").GetInt32(),
                "Truck timeout must preserve one consumed attempt");
            TestAssert.Equal(1, provider.ClearTruckCalls.Count,
                "Truck result timeout must clear game-side pending state once");
            (int ServerId, string TrainUuid, string JobId) clear = provider.ClearTruckCalls[0];
            TestAssert.True(clear.ServerId == 10 && clear.TrainUuid == "501" && clear.JobId == scheduled.JobId,
                "Truck clear-pending identity mismatch");
        }

        now = 100_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            string malformedJson = JsonSerializer.Serialize(new
            {
                uuid = "not-decimal",
                serverId = 10,
                executeAt = now,
                expireAt = now + 50_000,
                robTimes = 0,
                maxLootCount = 3,
            });
            store.ScheduleTruckPlunder(
                10, "not-decimal", malformedJson, now, now + 50_000, now);
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();

            JsonElement malformed = TruckRow(actions, "not-decimal");
            TestAssert.Equal("failed", malformed.GetProperty("scheduleStatus").GetString(),
                "malformed Truck target must fail closed");
            TestAssert.Equal("invalid scheduled target", malformed.GetProperty("lastError").GetString(),
                "malformed Truck target error mismatch");
            TestAssert.Equal(0, malformed.GetProperty("attempts").GetInt32(),
                "malformed Truck target must not consume an attempt");
            TestAssert.Equal(0, provider.TruckArmCalls.Count,
                "malformed Truck target must never reach provider arm");
        }
    }

    private static void TruckDisconnectAndRestartAreExact()
    {
        long now = 120_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            actions.ScheduleTruckPlunder([Truck("601", 120_100)]);
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();
            provider.Offline = true;
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();

            JsonElement disconnected = TruckRow(actions, "601");
            TestAssert.Equal("waiting_connection", disconnected.GetProperty("scheduleStatus").GetString(),
                "Truck disconnect status mismatch");
            TestAssert.Equal("game disconnected", disconnected.GetProperty("lastError").GetString(),
                "Truck disconnect error mismatch");
            TestAssert.Equal(1, disconnected.GetProperty("attempts").GetInt32(),
                "Truck disconnect must preserve consumed attempt");
        }

        now = 130_000;
        using (MapStore store = MapStore.CreateInMemory())
        {
            var provider = new WorkerProvider { CurrentServerId = 10 };
            var actions = new MapActionControlPlane(store, provider, () => now);
            actions.ScheduleTruckPlunder([Truck("602", 130_100)]);
            var worker = new MapPlunderWorker(store, provider, () => now);
            worker.RunTruckOnceAsync().AsTask().GetAwaiter().GetResult();
            worker.RecoverAfterRestart();

            JsonElement restarted = TruckRow(actions, "602");
            TestAssert.Equal("waiting_connection", restarted.GetProperty("scheduleStatus").GetString(),
                "Truck restart status mismatch");
            TestAssert.Equal("client restarted", restarted.GetProperty("lastError").GetString(),
                "Truck restart error mismatch");
            TestAssert.Equal(1, restarted.GetProperty("attempts").GetInt32(),
                "Truck restart must preserve consumed attempt");
        }
    }

    private static void ServerDayRefreshCachesAndPrunes()
    {
        long now = 150_000;
        long dayStart = 140_000;
        using MapStore store = MapStore.CreateInMemory();
        var provider = new WorkerProvider
        {
            CurrentServerId = 10,
            ServerDay = new MapPlunderServerDayProviderResult(now, dayStart),
        };
        var actions = new MapActionControlPlane(store, provider, () => now);
        actions.ScheduleDispatchPlunderAsync([Dispatch("701", "dispatch", 141_000)])
            .AsTask().GetAwaiter().GetResult();
        actions.CancelDispatchPlunder(10, "701");
        MapPlunderJobsSnapshot snapshot = actions.ListPlunderJobsAsync().AsTask().GetAwaiter().GetResult();
        TestAssert.Equal(dayStart, snapshot.ServerDayStartAt, "server-day result mismatch");
        TestAssert.Equal(JsonSerializer.Serialize(dayStart), store.ReadSetting("map_plunder_server_day_start"),
            "server-day cache mismatch");
    }

    private static JsonElement Dispatch(string uuid, string kind, long plunderAt) =>
        JsonSerializer.SerializeToElement(new
        {
            taskKind = kind,
            uuid,
            serverId = 10,
            ownerServer = 10,
            completionTime = plunderAt - 100,
            plunderAt,
            taskExpireTime = plunderAt + 50_000,
            stolenCount = 0,
            maxStealCount = 2,
        });

    private static JsonElement Truck(string uuid, long executeAt, string? extraTrainUuid = null) =>
        JsonSerializer.SerializeToElement(new
        {
            uuid,
            trainUuid = extraTrainUuid,
            serverId = 10,
            executeAt,
            expireAt = executeAt + 50_000,
            robTimes = 0,
            maxLootCount = 3,
        });

    private static JsonElement DispatchRow(MapActionControlPlane actions, string uuid) =>
        actions.ListPlunderJobs().DispatchJobs.Single(row =>
            row.GetProperty("uuid").GetString() == uuid);

    private static JsonElement TruckRow(MapActionControlPlane actions, string uuid) =>
        actions.ListTruckPlunderJobs().Single(row =>
            row.GetProperty("uuid").GetString() == uuid);

    private sealed class WorkerProvider : IMapActionProvider
    {
        internal int CurrentServerId { get; set; }
        internal bool Offline { get; set; }
        internal MapPlunderServerDayProviderResult? ServerDay { get; set; }
        internal List<IReadOnlyList<DispatchPlunderArmJob>> DispatchArmBatches { get; } = [];
        internal List<TruckPlunderArmJob> TruckArmCalls { get; } = [];
        internal Queue<IReadOnlyList<DispatchPlunderResultEvent>> DispatchResultBatches { get; } = new();
        internal Queue<IReadOnlyList<TruckPlunderResultEvent>> TruckResultBatches { get; } = new();
        internal List<(int ServerId, string TrainUuid, string JobId)> ClearTruckCalls { get; } = [];

        private static BridgeCommandException Unavailable() =>
            new("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");

        public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default) =>
            Offline ? ValueTask.FromException<int>(Unavailable()) : ValueTask.FromResult(CurrentServerId);

        public ValueTask<IReadOnlyList<DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
            IReadOnlyList<DispatchPlunderArmJob> jobs,
            CancellationToken cancellationToken = default)
        {
            if (Offline) return ValueTask.FromException<IReadOnlyList<DispatchPlunderArmResult>>(Unavailable());
            DispatchPlunderArmJob[] captured = jobs.ToArray();
            DispatchArmBatches.Add(captured);
            return ValueTask.FromResult<IReadOnlyList<DispatchPlunderArmResult>>(
                captured.Select(job => new DispatchPlunderArmResult(
                    job.Kind, job.ServerId, job.TaskUuid, true)).ToArray());
        }

        public ValueTask<TruckPlunderArmResult> ArmTruckPlunderAsync(
            TruckPlunderArmJob job,
            CancellationToken cancellationToken = default)
        {
            if (Offline) return ValueTask.FromException<TruckPlunderArmResult>(Unavailable());
            TruckArmCalls.Add(job);
            return ValueTask.FromResult(new TruckPlunderArmResult(true));
        }

        public ValueTask<IReadOnlyList<DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
            CancellationToken cancellationToken = default)
        {
            if (Offline) return ValueTask.FromException<IReadOnlyList<DispatchPlunderResultEvent>>(Unavailable());
            return ValueTask.FromResult(
                DispatchResultBatches.Count > 0
                    ? DispatchResultBatches.Dequeue()
                    : (IReadOnlyList<DispatchPlunderResultEvent>)Array.Empty<DispatchPlunderResultEvent>());
        }

        public ValueTask<IReadOnlyList<TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
            CancellationToken cancellationToken = default)
        {
            if (Offline) return ValueTask.FromException<IReadOnlyList<TruckPlunderResultEvent>>(Unavailable());
            return ValueTask.FromResult(
                TruckResultBatches.Count > 0
                    ? TruckResultBatches.Dequeue()
                    : (IReadOnlyList<TruckPlunderResultEvent>)Array.Empty<TruckPlunderResultEvent>());
        }

        public ValueTask ClearTruckPlunderPendingAsync(
            int serverId,
            string trainUuid,
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (Offline) return ValueTask.FromException(Unavailable());
            ClearTruckCalls.Add((serverId, trainUuid, jobId));
            return ValueTask.CompletedTask;
        }

        public ValueTask<MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(
            CancellationToken cancellationToken = default) =>
            Offline
                ? ValueTask.FromException<MapPlunderServerDayProviderResult?>(Unavailable())
                : ValueTask.FromResult(ServerDay);

        public ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(Unavailable());
        public ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(Unavailable());
        public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(Unavailable());
        public ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(
            IReadOnlyList<JsonElement> records,
            bool refresh,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<TreasureInspectionResult>(Unavailable());
        public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromException<JsonElement>(Unavailable());
        public ValueTask<JsonElement> ClaimTreasuresAsync(
            TreasureClaimProviderRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<JsonElement>(Unavailable());
        public ValueTask<DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(
            JsonElement row,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<DispatchShareProviderResult>(Unavailable());
        public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(
            IReadOnlyList<JsonElement> rows,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(rows);
    }
}
