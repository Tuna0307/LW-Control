using System.Text.Json;

namespace LWBridge.Map317.Checks;

internal static class ActionChecks
{
    internal static void Run()
    {
        NavigationAndServerJump();
        TreasureStateAndClaim();
        ShareAndPlunderPersistence();
        ProtectedCallsTimeout();
        UnavailableProviderFailsClosed();
    }

    private static void NavigationAndServerJump()
    {
        long now = 1_000;
        var provider = new FakeActionProvider { CurrentServerId = 100 };
        using MapStore store = MapStore.CreateInMemory();
        var actions = new MapActionControlPlane(
            store,
            provider,
            () => now,
            (duration, _) => { now += (long)duration.TotalMilliseconds; provider.PollCount++; if (provider.PendingServerId > 0 && provider.PollCount >= 2) provider.CurrentServerId = provider.PendingServerId; return Task.CompletedTask; });

        CoordinateJumpResult coordinate = actions.CoordinateJumpAsync(100, 12, 34).AsTask().GetAwaiter().GetResult();
        TestAssert.Equal(new CoordinateJumpResult(100, 12, 34), coordinate, "coordinate result mismatch");
        TestAssert.Throws<BridgeCommandException>(() => actions.CoordinateJumpAsync(0, 1, 1).AsTask().GetAwaiter().GetResult(), "INVALID_COORDINATE");

        MarchFollowResult follow = actions.MarchFollowAsync(100, "123456").AsTask().GetAwaiter().GetResult();
        TestAssert.Equal("123456", follow.MarchUuid, "march follow result mismatch");
        TestAssert.Throws<BridgeCommandException>(() => actions.MarchFollowAsync(100, "").AsTask().GetAwaiter().GetResult(), "INVALID_MARCH");

        ServerJumpResult jump = actions.ServerJumpAsync(101).AsTask().GetAwaiter().GetResult();
        TestAssert.True(jump.Changed && jump.PreviousServerId == 100 && jump.ServerId == 101, "server jump result mismatch");
        ServerJumpResult same = actions.ServerJumpAsync(101).AsTask().GetAwaiter().GetResult();
        TestAssert.True(!same.Changed && same.PreviousServerId == 101 && same.ServerId == 101, "same-server jump mismatch");
    }

    private static void TreasureStateAndClaim()
    {
        long now = 2_000;
        var provider = new FakeActionProvider { CurrentServerId = 2212 };
        using MapStore store = MapStore.CreateInMemory();
        store.UpsertRecord(new MapRecord(
            "treasure", 2212, "treasure:77", 77, "777", null, null, null, null, null, null, null, now,
            "{\"uuid\":\"777\",\"pointIndex\":77,\"suppliesType\":1,\"treasureType\":0,\"complete\":true,\"expireTime\":9999999}"));
        provider.TreasureStates = [JsonSerializer.SerializeToElement(new
        {
            uuid = "777",
            expireTime = 9999999L,
            playerClaimState = "unclaimed",
            worldClaimState = "claimable",
            claimPriority = 0,
        })];
        var actions = new MapActionControlPlane(store, provider, () => now);

        TreasureInspectionResult refreshed = actions.RefreshAllTreasureStatesAsync(2212).AsTask().GetAwaiter().GetResult();
        TestAssert.Equal("player-1", refreshed.PlayerUid, "treasure player identity mismatch");
        MapSearchResult search = store.Search(new MapQuery("treasure", 2212, ViewerUid: "player-1", LuckyFirst: true));
        TestAssert.Equal("unclaimed", search.Rows[0].GetProperty("playerClaimState").GetString(), "treasure cache overlay missing");

        JsonElement claim = actions.ClaimTreasuresAsync(2212, "season").AsTask().GetAwaiter().GetResult();
        TestAssert.Equal(1, claim.GetProperty("eligible").GetInt32(), "claim result mismatch");
        TestAssert.Equal(1, provider.LastClaimRequest!.Records.Count, "claim candidate query mismatch");
        TestAssert.Throws<BridgeCommandException>(() => actions.ClaimTreasuresAsync(2212, "bad").AsTask().GetAwaiter().GetResult(), "INVALID_TREASURE_CLAIM_SCOPE");

        provider.CurrentServerId = 5;
        TestAssert.Throws<BridgeCommandException>(() => actions.RefreshAllTreasureStatesAsync(2212).AsTask().GetAwaiter().GetResult(), "SERVER_MISMATCH");
    }

    private static void ShareAndPlunderPersistence()
    {
        long now = 10_000;
        var provider = new FakeActionProvider { CurrentServerId = 10 };
        using MapStore store = MapStore.CreateInMemory();
        var actions = new MapActionControlPlane(store, provider, () => now);
        int dispatchEvents = 0, truckEvents = 0;
        actions.DispatchPlunderChanged += (_, _) => dispatchEvents++;
        actions.TruckPlunderChanged += (_, _) => truckEvents++;

        JsonElement share1 = JsonSerializer.SerializeToElement(new { uuid = "11", serverId = 10, x = 1, y = 2, cfgId = 3, ownerName = "A" });
        JsonElement share2 = JsonSerializer.SerializeToElement(new { uuid = "12", serverId = 10, x = 3, y = 4, cfgId = 5, ownerName = "B" });
        JsonElement share3 = JsonSerializer.SerializeToElement(new { uuid = "13", serverId = 10, x = 5, y = 6, cfgId = 7, ownerName = "C" });
        provider.ShareFailures.Add("12");
        provider.ShareErrors.Add("13");
        DispatchShareResult share = actions.ShareDispatchToAllianceAsync([share1, share2, share3]).AsTask().GetAwaiter().GetResult();
        TestAssert.True(share.Shared == 1 && share.Failed == 2 && share.SharedUuids.SequenceEqual(["11"]),
            "dispatch share result/error aggregation mismatch");

        JsonElement dispatch = JsonSerializer.SerializeToElement(new
        {
            taskKind = "dispatch", uuid = "21", serverId = 10, completionTime = 100L, plunderAt = 150L,
            taskExpireTime = 1000L, stolenCount = 0, maxStealCount = 2,
        });
        JsonElement ghost = JsonSerializer.SerializeToElement(new
        {
            taskKind = "ghost", uuid = "22", serverId = 10, completionTime = 100L, plunderAt = 160L,
            taskExpireTime = 1000L, stolenCount = 0, maxStealCount = 2,
        });
        IReadOnlyList<JsonElement> scheduled = actions.ScheduleDispatchPlunderAsync([dispatch, ghost]).AsTask().GetAwaiter().GetResult();
        TestAssert.Equal(2, scheduled.Count, "dispatch schedule count mismatch");
        MapPlunderJobsSnapshot jobs = actions.ListPlunderJobs();
        TestAssert.Equal(2, jobs.DispatchJobs.Count, "dispatch job list mismatch");
        TestAssert.True(jobs.DispatchJobs.Any(row => row.TryGetProperty("taskKind", out JsonElement value) && value.GetString() == "ghost"),
            "ghost identity missing from scheduled jobs");
        actions.CancelDispatchPlunder(10, "21");
        TestAssert.True(dispatchEvents >= 2, "dispatch change event missing");

        JsonElement truck = JsonSerializer.SerializeToElement(new
        {
            uuid = "31", serverId = 10, executeAt = 200L, expireAt = 2000L, robTimes = 0, maxLootCount = 3,
            battleWon = true, plunderRewards = new[] { new { key = "x" } },
        });
        IReadOnlyList<TruckPlunderScheduleResult> trucks = actions.ScheduleTruckPlunder([truck]);
        TestAssert.Equal(1, trucks.Count, "truck schedule count mismatch");
        TestAssert.Equal(1, actions.ListTruckPlunderJobs().Count, "truck list mismatch");
        actions.CancelTruckPlunder(10, "31");
        TestAssert.True(truckEvents >= 2, "truck change event missing");
    }

    private static void UnavailableProviderFailsClosed()
    {
        using MapStore store = MapStore.CreateInMemory();
        var actions = new MapActionControlPlane(store, UnavailableMapActionProvider.Instance);
        TestAssert.Throws<BridgeCommandException>(() => actions.CoordinateJumpAsync(1, 1, 1).AsTask().GetAwaiter().GetResult(), "GAME_CONNECTION_UNAVAILABLE");
    }

    private static void ProtectedCallsTimeout()
    {
        TestAssert.Equal(5_000, MapActionControlPlane.ProtectedCallTimeoutMilliseconds,
            "protected Map action timeout must remain exact 5 seconds");
        var provider = new FakeActionProvider { CurrentServerId = 1, BlockCoordinate = true };
        using MapStore store = MapStore.CreateInMemory();
        var actions = new MapActionControlPlane(
            store,
            provider,
            protectedCallTimeout: TimeSpan.FromMilliseconds(5));
        TestAssert.Throws<BridgeCommandException>(
            () => actions.CoordinateJumpAsync(1, 1, 1).AsTask().GetAwaiter().GetResult(),
            "GAME_CONNECTION_UNAVAILABLE");
    }

    private sealed class FakeActionProvider : IMapActionProvider
    {
        internal int CurrentServerId { get; set; }
        internal int PendingServerId { get; set; }
        internal int PollCount { get; set; }
        internal IReadOnlyList<JsonElement> TreasureStates { get; set; } = Array.Empty<JsonElement>();
        internal TreasureClaimProviderRequest? LastClaimRequest { get; private set; }
        internal HashSet<string> ShareFailures { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> ShareErrors { get; } = new(StringComparer.Ordinal);
        internal bool BlockCoordinate { get; set; }

        public ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default) =>
            BlockCoordinate
                ? new ValueTask(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task)
                : ValueTask.CompletedTask;
        public ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(CurrentServerId);
        public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) { PendingServerId = serverId; PollCount = 0; return ValueTask.CompletedTask; }
        public ValueTask<TreasureInspectionResult> InspectTreasureStatesAsync(IReadOnlyList<JsonElement> records, bool refresh, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new TreasureInspectionResult("player-1", "alliance-1", TreasureStates));
        public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(JsonSerializer.SerializeToElement(new { states = Array.Empty<object>() }));
        public ValueTask<JsonElement> ClaimTreasuresAsync(TreasureClaimProviderRequest request, CancellationToken cancellationToken = default)
        {
            LastClaimRequest = request;
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { eligible = request.Records.Count, queued = request.Records.Count, skipped = 0 }));
        }
        public ValueTask<DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(JsonElement row, CancellationToken cancellationToken = default)
        {
            string uuid = row.GetProperty("uuid").GetString()!;
            if (ShareErrors.Contains(uuid))
                return ValueTask.FromException<DispatchShareProviderResult>(
                    new BridgeCommandException("GAME_PROVIDER_ERROR", "share failed"));
            return ValueTask.FromResult(new DispatchShareProviderResult(!ShareFailures.Contains(uuid)));
        }
        public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(rows);
        public ValueTask<MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<MapPlunderServerDayProviderResult?>(null);
        public ValueTask<IReadOnlyList<DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
            IReadOnlyList<DispatchPlunderArmJob> jobs,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<DispatchPlunderArmResult>>(
                jobs.Select(job => new DispatchPlunderArmResult(
                    job.Kind, job.ServerId, job.TaskUuid, true)).ToArray());
        public ValueTask<TruckPlunderArmResult> ArmTruckPlunderAsync(
            TruckPlunderArmJob job,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new TruckPlunderArmResult(true));
        public ValueTask<IReadOnlyList<DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<DispatchPlunderResultEvent>>(Array.Empty<DispatchPlunderResultEvent>());
        public ValueTask<IReadOnlyList<TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<TruckPlunderResultEvent>>(Array.Empty<TruckPlunderResultEvent>());
        public ValueTask ClearTruckPlunderPendingAsync(
            int serverId,
            string trainUuid,
            string jobId,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
