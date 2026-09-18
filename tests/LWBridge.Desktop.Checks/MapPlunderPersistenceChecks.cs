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

                JsonElement plunderRewards = Payload(new[]
                {
                    new
                    {
                        key = "reward:1:1001",
                        name = "Iron",
                        iconPath = "items/iron.png",
                        count = 25,
                        rewardType = 1,
                        itemId = 1001L,
                    },
                });
                Check(store.RecordTruckPlunderSuccess(
                        88,
                        "truck-waiting",
                        battleWon: true,
                        plunderRewards,
                        rewardNormalizationComplete: true,
                        updatedAt: 150,
                        dailyRobCount: 7),
                    "authoritative Truck result updates the active persisted attempt");
                Check(!store.RecordTruckPlunderSuccess(
                        88,
                        "missing-truck",
                        battleWon: true,
                        plunderRewards,
                        rewardNormalizationComplete: true,
                        updatedAt: 151),
                    "Truck result update does not fabricate a missing scheduled attempt");

                JsonElement succeeded = store.ReadPlunderJobs().TruckJobs.Single(
                    row => row.GetProperty("uuid").GetString() == "truck-waiting");
                Check(succeeded.GetProperty("scheduleStatus").GetString() == "succeeded" &&
                      succeeded.GetProperty("battleWon").GetBoolean() &&
                      succeeded.GetProperty("plunderRewards").GetArrayLength() == 1 &&
                      succeeded.GetProperty("plunderRewards")[0].GetProperty("key").GetString() == "reward:1:1001" &&
                      succeeded.GetProperty("plunderRewards")[0].GetProperty("count").GetInt32() == 25 &&
                      succeeded.GetProperty("plunderRewardsComplete").GetBoolean() &&
                      succeeded.GetProperty("robTimes").GetInt32() == 1 &&
                      !succeeded.TryGetProperty("remainingLootCount", out _) &&
                      succeeded.GetProperty("dailyRobCount").GetInt32() == 7 &&
                      succeeded.GetProperty("attempts").GetInt32() == 2 &&
                      succeeded.GetProperty("lastError").ValueKind == JsonValueKind.Null &&
                      succeeded.GetProperty("scheduleUpdatedAt").GetInt64() == 150,
                    "Truck success merges only supplied authoritative counters, preserves absent counters and clears the prior connection error");

                store.UpsertTruckPlunderJobForTest(
                    88,
                    "truck-result-counts",
                    """{"uuid":"truck-result-counts","ownerName":"Counts"}""",
                    executeAt: 1_500, expireAt: 9_000, status: "running", attempts: 1,
                    lastError: null, createdAt: 95, updatedAt: 145);
                Check(store.RecordTruckPlunderSuccess(
                        88,
                        "truck-result-counts",
                        battleWon: false,
                        plunderRewards,
                        rewardNormalizationComplete: true,
                        updatedAt: 152,
                        robTimes: 2,
                        remainingLootCount: 0,
                        dailyRobCount: 8),
                    "original Truck result merger accepts all three recovered optional counters when supplied");
                JsonElement counted = store.ReadPlunderJobs().TruckJobs.Single(
                    row => row.GetProperty("uuid").GetString() == "truck-result-counts");
                Check(counted.GetProperty("robTimes").GetInt32() == 2 &&
                      counted.GetProperty("remainingLootCount").GetInt32() == 0 &&
                      counted.GetProperty("dailyRobCount").GetInt32() == 8 &&
                      counted.GetProperty("scheduleStatus").GetString() == "succeeded" &&
                      counted.GetProperty("attempts").GetInt32() == 1,
                    "all authoritative Truck result counters persist without changing attempts");

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
                JsonElement succeeded = snapshot.TruckJobs.Single(
                    row => row.GetProperty("uuid").GetString() == "truck-waiting");
                Check(succeeded.GetProperty("scheduleStatus").GetString() == "succeeded" &&
                      succeeded.GetProperty("battleWon").GetBoolean() &&
                      succeeded.GetProperty("plunderRewards")[0].GetProperty("count").GetInt32() == 25 &&
                      succeeded.GetProperty("plunderRewardsComplete").GetBoolean() &&
                      succeeded.GetProperty("attempts").GetInt32() == 2,
                    "authoritative Truck battle/reward result persists across database reopen without changing attempts");
                Check(snapshot.DispatchJobs.Count == 1 &&
                      snapshot.DispatchJobs[0].GetProperty("scheduleStatus").GetString() == "running",
                    "combined list preserves unrelated dispatch job state across reopen");
            }

            RunTruckScheduleTransaction(Path.Combine(root, "schedule-map-data.db"));
            RunTruckWorkerPersistence(Path.Combine(root, "worker-map-data.db"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void RunTruckScheduleTransaction(string databasePath)
    {
        Check(MapDataStore.CreateTruckPlunderJobId(1_700_000_000_123L, 0xabcdUL) ==
              "truck-1700000000123-abcd",
            "fresh Truck job id uses recovered truck-{unixMs}-{randomU64LowerHex} shape");
        Check(MapDataStore.CreateTruckPlunderJobId(1_700_000_000_123L, 0UL) ==
              "truck-1700000000123-0",
            "fresh Truck job id does not invent hex zero padding");
        Check(MapDataStore.CreateLegacyTruckPlunderJobId(88, "train-legacy", 600) ==
              "legacy-88-train-legacy-600",
            "legacy Truck archive id uses recovered server/train/created_at tuple");

        using var store = new MapDataStore(databasePath);

        TruckPlunderScheduleResult fresh = store.ScheduleTruckPlunderForTest(
            88,
            "truck-fresh",
            """{"uuid":"truck-fresh","ownerName":"Fresh","battleWon":true,"plunderRewards":[{"key":"old"}],"plunderRewardsComplete":true,"jobId":"stale"}""",
            executeAt: 2_000,
            expireAt: 9_000,
            now: 1_000,
            randomValue: 0x10UL);
        Check(fresh.JobId == "truck-1000-10" && fresh.Attempts == 0 && !fresh.ArchivedPreviousAttempt,
            "fresh Truck schedule returns recovered new identity with zero attempts and no archive");
        JsonElement freshRow = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "truck-fresh");
        Check(freshRow.GetProperty("jobId").GetString() == "truck-1000-10" &&
              freshRow.GetProperty("scheduleStatus").GetString() == "scheduled" &&
              freshRow.GetProperty("attempts").GetInt32() == 0 &&
              freshRow.GetProperty("scheduledAt").GetInt64() == 1_000 &&
              freshRow.GetProperty("scheduleUpdatedAt").GetInt64() == 1_000 &&
              freshRow.GetProperty("executeAt").GetInt64() == 2_000 &&
              !freshRow.TryGetProperty("battleWon", out _) &&
              !freshRow.TryGetProperty("plunderRewards", out _) &&
              !freshRow.TryGetProperty("plunderRewardsComplete", out _),
            "fresh Truck schedule strips stale result state and persists the fresh job id");

        store.UpsertTruckPlunderJobForTest(
            88,
            "truck-wait",
            """{"uuid":"truck-wait","ownerName":"Waiting","jobId":"truck-old-wait","battleWon":false,"plunderRewards":[]}""",
            executeAt: 2_100,
            expireAt: 9_000,
            status: "waiting_connection",
            attempts: 4,
            lastError: "game disconnected",
            createdAt: 700,
            updatedAt: 710);
        TruckPlunderScheduleResult waiting = store.ScheduleTruckPlunderForTest(
            88,
            "truck-wait",
            """{"uuid":"truck-wait","ownerName":"Waiting New","battleWon":true,"plunderRewards":[{"key":"stale"}]}""",
            executeAt: 3_000,
            expireAt: 9_500,
            now: 1_100,
            randomValue: 0x20UL);
        Check(waiting.JobId == "truck-1100-20" &&
              waiting.Attempts == 4 &&
              !waiting.ArchivedPreviousAttempt,
            "waiting_connection reschedule preserves attempts and does not archive a nonterminal attempt");
        JsonElement waitingRow = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "truck-wait");
        Check(waitingRow.GetProperty("jobId").GetString() == "truck-1100-20" &&
              waitingRow.GetProperty("scheduleStatus").GetString() == "scheduled" &&
              waitingRow.GetProperty("attempts").GetInt32() == 4 &&
              waitingRow.GetProperty("scheduledAt").GetInt64() == 700 &&
              waitingRow.GetProperty("scheduleUpdatedAt").GetInt64() == 1_100 &&
              waitingRow.GetProperty("executeAt").GetInt64() == 3_000 &&
              waitingRow.GetProperty("ownerName").GetString() == "Waiting New",
            "rescheduling a waiting row preserves original created_at while replacing current target JSON/time");

        store.UpsertTruckPlunderJobForTest(
            88,
            "truck-terminal",
            """{"uuid":"truck-terminal","ownerName":"Terminal","jobId":"truck-old-terminal","battleWon":true,"plunderRewards":[{"key":"reward:1:1001","count":25}]}""",
            executeAt: 2_200,
            expireAt: 9_000,
            status: "succeeded",
            attempts: 2,
            lastError: null,
            createdAt: 500,
            updatedAt: 550);
        TruckPlunderScheduleResult terminal = store.ScheduleTruckPlunderForTest(
            88,
            "truck-terminal",
            """{"uuid":"truck-terminal","ownerName":"Terminal Again","battleWon":false,"plunderRewards":[{"key":"stale"}]}""",
            executeAt: 3_100,
            expireAt: 9_600,
            now: 1_200,
            randomValue: 0x30UL);
        Check(terminal.JobId == "truck-1200-30" &&
              terminal.Attempts == 0 &&
              terminal.ArchivedPreviousAttempt,
            "terminal reschedule archives the previous attempt and resets attempts");
        JsonElement[] terminalRows = store.ReadPlunderJobs().TruckJobs
            .Where(row => row.GetProperty("uuid").GetString() == "truck-terminal")
            .ToArray();
        Check(terminalRows.Length == 2,
            "terminal reschedule exposes one fresh active row plus one archived previous attempt");
        JsonElement terminalActive = terminalRows.Single(
            row => row.GetProperty("scheduleStatus").GetString() == "scheduled");
        JsonElement terminalHistory = terminalRows.Single(
            row => row.GetProperty("scheduleStatus").GetString() == "succeeded");
        Check(terminalActive.GetProperty("jobId").GetString() == "truck-1200-30" &&
              terminalActive.GetProperty("attempts").GetInt32() == 0 &&
              terminalActive.GetProperty("scheduledAt").GetInt64() == 500 &&
              terminalActive.GetProperty("scheduleUpdatedAt").GetInt64() == 1_200 &&
              !terminalActive.TryGetProperty("battleWon", out _) &&
              !terminalActive.TryGetProperty("plunderRewards", out _),
            "terminal replacement keeps original active-row created_at but removes prior battle result state");
        Check(terminalHistory.GetProperty("jobId").GetString() == "truck-old-terminal" &&
              terminalHistory.GetProperty("battleWon").GetBoolean() &&
              terminalHistory.GetProperty("attempts").GetInt32() == 2 &&
              terminalHistory.GetProperty("scheduledAt").GetInt64() == 500 &&
              terminalHistory.GetProperty("scheduleUpdatedAt").GetInt64() == 550,
            "terminal archive preserves previous job identity, result metadata, attempts and timestamps");

        store.UpsertTruckPlunderJobForTest(
            88,
            "truck-legacy",
            """{"uuid":"truck-legacy","ownerName":"Legacy","battleWon":false,"plunderRewards":[]}""",
            executeAt: 2_300,
            expireAt: 9_000,
            status: "failed",
            attempts: 5,
            lastError: "previous failure",
            createdAt: 600,
            updatedAt: 650);
        TruckPlunderScheduleResult legacy = store.ScheduleTruckPlunderForTest(
            88,
            "truck-legacy",
            """{"uuid":"truck-legacy","ownerName":"Legacy Again"}""",
            executeAt: 3_200,
            expireAt: null,
            now: 1_300,
            randomValue: 0x40UL);
        Check(legacy.JobId == "truck-1300-40" && legacy.Attempts == 0 && legacy.ArchivedPreviousAttempt,
            "legacy terminal reschedule still produces a fresh active job");
        JsonElement[] legacyRows = store.ReadPlunderJobs().TruckJobs
            .Where(row => row.GetProperty("uuid").GetString() == "truck-legacy")
            .ToArray();
        JsonElement legacyHistory = legacyRows.Single(
            row => row.GetProperty("scheduleStatus").GetString() == "failed");
        JsonElement legacyActive = legacyRows.Single(
            row => row.GetProperty("scheduleStatus").GetString() == "scheduled");
        Check(legacyHistory.GetProperty("jobId").GetString() == "legacy-88-truck-legacy-600" &&
              legacyHistory.GetProperty("attempts").GetInt32() == 5 &&
              legacyHistory.GetProperty("lastError").GetString() == "previous failure",
            "missing legacy jobId is synthesized from recovered server/train/created_at identity and persisted in history JSON");
        Check(legacyActive.GetProperty("jobId").GetString() == "truck-1300-40" &&
              legacyActive.GetProperty("attempts").GetInt32() == 0 &&
              legacyActive.GetProperty("scheduledAt").GetInt64() == 600,
            "legacy terminal replacement resets attempts while preserving the conflict-row created_at");

        store.UpsertTruckPlunderJobForTest(
            88,
            "truck-running",
            """{"uuid":"truck-running","ownerName":"Running","jobId":"truck-running-old"}""",
            executeAt: 2_400,
            expireAt: 9_000,
            status: "running",
            attempts: 6,
            lastError: null,
            createdAt: 650,
            updatedAt: 660);
        ExpectBridgeError(
            "MAP_DATA_ERROR",
            "scheduled truck job is missing",
            () => store.ScheduleTruckPlunderForTest(
                88,
                "truck-running",
                """{"uuid":"truck-running","ownerName":"Replacement"}""",
                executeAt: 3_300,
                expireAt: 9_700,
                now: 1_400,
                randomValue: 0x50UL),
            "running Truck row cannot be replaced by the recovered schedule upsert");
        JsonElement[] runningRows = store.ReadPlunderJobs().TruckJobs
            .Where(row => row.GetProperty("uuid").GetString() == "truck-running")
            .ToArray();
        Check(runningRows.Length == 1 &&
              runningRows[0].GetProperty("jobId").GetString() == "truck-running-old" &&
              runningRows[0].GetProperty("scheduleStatus").GetString() == "running" &&
              runningRows[0].GetProperty("attempts").GetInt32() == 6 &&
              runningRows[0].GetProperty("scheduledAt").GetInt64() == 650 &&
              runningRows[0].GetProperty("scheduleUpdatedAt").GetInt64() == 660 &&
              runningRows[0].GetProperty("executeAt").GetInt64() == 2_400 &&
              runningRows[0].GetProperty("ownerName").GetString() == "Running",
            "failed running-row replacement rolls the scheduling transaction back without fabricating history or mutating the active job");
    }

    private static void RunTruckWorkerPersistence(string databasePath)
    {
        using var store = new MapDataStore(databasePath);
        store.UpsertTruckPlunderJobForTest(
            88, "worker-waiting",
            """{"uuid":"worker-waiting","jobId":"truck-waiting","marchUuid":"101","robTimes":0,"maxLootCount":2}""",
            executeAt: 900, expireAt: 2_000, status: "waiting_connection", attempts: 2,
            lastError: "game disconnected", createdAt: 100, updatedAt: 110);
        store.UpsertTruckPlunderJobForTest(
            88, "worker-scheduled",
            """{"uuid":"worker-scheduled","jobId":"truck-scheduled","marchUuid":"102","robTimes":0,"maxLootCount":2}""",
            executeAt: 1_050, expireAt: 2_000, status: "scheduled", attempts: 3,
            lastError: null, createdAt: 120, updatedAt: 130);
        store.UpsertTruckPlunderJobForTest(
            88, "worker-far",
            """{"uuid":"worker-far","jobId":"truck-far","marchUuid":"103"}""",
            executeAt: 1_500, expireAt: 2_000, status: "scheduled", attempts: 0,
            lastError: null, createdAt: 140, updatedAt: 150);
        store.UpsertTruckPlunderJobForTest(
            88, "worker-expired",
            """{"uuid":"worker-expired","jobId":"truck-expired","marchUuid":"104"}""",
            executeAt: 800, expireAt: 1_000, status: "scheduled", attempts: 1,
            lastError: null, createdAt: 160, updatedAt: 170);

        Check(store.ExpireTruckPlunder(1_000) == 1,
            "Truck worker expires only scheduled/waiting jobs whose expire_at has elapsed");
        JsonElement expired = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "worker-expired");
        Check(expired.GetProperty("scheduleStatus").GetString() == "expired" &&
              expired.GetProperty("lastError").GetString() == "truck expired" &&
              expired.GetProperty("scheduleUpdatedAt").GetInt64() == 1_000,
            "Truck expiry uses the recovered terminal status/error and update time");

        TruckPlunderWorkItem? first = store.ReadArmableTruckPlunder(1_000, 100);
        Check(first is not null &&
              first.TrainUuid == "worker-waiting" &&
              first.Status == "waiting_connection" &&
              first.Attempts == 2 &&
              first.ExecuteAt == 900 &&
              first.ExpireAt == 2_000 &&
              first.Truck.GetProperty("jobId").GetString() == "truck-waiting",
            "armable Truck read accepts scheduled/waiting rows, excludes expired rows, and orders by execute_at");

        Check(store.UpdateTruckPlunderStatus(
                88, "worker-waiting", "running", null, incrementAttempts: true, updatedAt: 1_010),
            "Truck worker can atomically mark the selected job running");
        JsonElement running = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "worker-waiting");
        Check(running.GetProperty("scheduleStatus").GetString() == "running" &&
              running.GetProperty("attempts").GetInt32() == 3 &&
              running.GetProperty("lastError").ValueKind == JsonValueKind.Null &&
              running.GetProperty("scheduleUpdatedAt").GetInt64() == 1_010,
            "running transition increments attempts exactly once and clears the prior connection error");

        Check(store.UpdateTruckPlunderStatus(
                88, "worker-waiting", "failed", "server response timeout", incrementAttempts: false, updatedAt: 1_020),
            "post-arm response timeout can be terminalized without a second attempt increment");
        JsonElement timedOut = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "worker-waiting");
        Check(timedOut.GetProperty("scheduleStatus").GetString() == "failed" &&
              timedOut.GetProperty("attempts").GetInt32() == 3 &&
              timedOut.GetProperty("lastError").GetString() == "server response timeout",
            "server response timeout is terminal failed and preserves the one running-transition attempt increment");

        TruckPlunderWorkItem? second = store.ReadArmableTruckPlunder(1_000, 100);
        Check(second is not null && second.TrainUuid == "worker-scheduled" && second.Status == "scheduled",
            "after the first terminal result the next due scheduled Truck becomes armable");
        Check(store.MarkDueTruckPlunderWaitingConnection(1_000, 100) == 1,
            "offline worker marks only due scheduled jobs waiting_connection");
        JsonElement disconnected = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "worker-scheduled");
        JsonElement far = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "worker-far");
        Check(disconnected.GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
              disconnected.GetProperty("lastError").GetString() == "game disconnected" &&
              disconnected.GetProperty("attempts").GetInt32() == 3 &&
              far.GetProperty("scheduleStatus").GetString() == "scheduled",
            "offline deferral preserves attempts and does not touch jobs outside the arm lead window");

        Check(store.UpdateTruckPlunderStatus(
                88, "worker-scheduled", "running", null, incrementAttempts: true, updatedAt: 1_030),
            "recovered restart fixture can create a stale running row");
        Check(store.RecoverTruckPlunderJobsOriginal(1_040) == 1,
            "original restart recovery helper finds the stale running Truck row");
        JsonElement recovered = store.ReadPlunderJobs().TruckJobs.Single(
            row => row.GetProperty("uuid").GetString() == "worker-scheduled");
        Check(recovered.GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
              recovered.GetProperty("lastError").GetString() == "client restarted" &&
              recovered.GetProperty("attempts").GetInt32() == 4,
            "original restart SQL is preserved as a reference helper without hiding the prior running attempt");
        Check(!store.UpdateTruckPlunderStatus(
                88, "worker-missing", "failed", "missing", incrementAttempts: false, updatedAt: 1_050),
            "Truck worker status update never fabricates a missing job");
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
