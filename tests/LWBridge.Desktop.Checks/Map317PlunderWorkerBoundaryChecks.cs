using System.Collections.Concurrent;
using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class Map317PlunderWorkerBoundaryChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwb317-plunder-worker-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string controllerPath = Path.Combine(root, "controller.db");
            using var registryStore = new ProfileRegistryStore(controllerPath);
            registryStore.EnsureLocalProfile("plunder-A", "Plunder A", 1_800_000_300_000);
            using var registry = new ProfileRegistryCommandService(registryStore, maxProfiles: 3);
            GameInstallationTestHooks installationHooks = MissingInstallation(root);
            string profileRoot = Path.Combine(root, "profiles", "plunder-A");
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var unavailable = new InstrumentedActionProvider(serverId: 317, connected: false);
            using (ProfileRuntimeOwner owner = CreateOwner(
                "plunder-A", profileRoot, registry, installationHooks, unavailable, startWorkers: true))
            {
                int dispatchEvents = 0;
                int truckEvents = 0;
                owner.Map317.DispatchPlunderChanged += () => Interlocked.Increment(ref dispatchEvents);
                owner.Map317.TruckPlunderChanged += () => Interlocked.Increment(ref truckEvents);
                await ScheduleDueJobsAsync(owner.Backend, "plunder-A", 317, now).ConfigureAwait(false);

                JsonElement waiting = await WaitForStatusesAsync(
                    owner.Backend,
                    "plunder-A",
                    dispatchStatus: "waiting_connection",
                    truckStatus: "waiting_connection",
                    timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(waiting.GetProperty("dispatchJobs").GetArrayLength() == 1 &&
                        waiting.GetProperty("truckJobs").GetArrayLength() == 1,
                    "unavailable worker must retain both due durable jobs");
                Require(unavailable.DispatchArmCalls == 0 && unavailable.TruckArmCalls == 0,
                    "unavailable execution must never cross the Dispatch/Truck arm boundary");
                Require(unavailable.CurrentServerCalls > 0 && dispatchEvents > 0 && truckEvents > 0,
                    "real native workers must poll inert connectivity and publish waiting-connection events");
            }

            var positive = new InstrumentedActionProvider(serverId: 317, connected: true);
            using (ProfileRuntimeOwner reopened = CreateOwner(
                "plunder-A", profileRoot, registry, installationHooks, positive, startWorkers: true))
            {
                int dispatchEvents = 0;
                int truckEvents = 0;
                reopened.Map317.DispatchPlunderChanged += () => Interlocked.Increment(ref dispatchEvents);
                reopened.Map317.TruckPlunderChanged += () => Interlocked.Increment(ref truckEvents);
                JsonElement succeeded = await WaitForStatusesAsync(
                    reopened.Backend,
                    "plunder-A",
                    dispatchStatus: "succeeded",
                    truckStatus: "succeeded",
                    timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Require(succeeded.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == "succeeded" &&
                        succeeded.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == "succeeded",
                    "positive inert workers must persist successful terminal results for both job types");
                Require(positive.DispatchArmCalls == 1 && positive.TruckArmCalls == 1,
                    "positive inert workers must arm each recovered due job exactly once");
                Require(positive.DispatchDrainCalls > 0 && positive.TruckDrainCalls > 0 &&
                        dispatchEvents > 0 && truckEvents > 0,
                    "positive inert workers must drain provider results and publish native change events");
            }

            using (ProfileRuntimeOwner persisted = CreateOwner(
                "plunder-A", profileRoot, registry, installationHooks,
                new InstrumentedActionProvider(317, connected: false), startWorkers: false))
            {
                JsonElement jobs = await ListJobsAsync(persisted.Backend, "plunder-A").ConfigureAwait(false);
                Require(jobs.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == "succeeded" &&
                        jobs.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == "succeeded",
                    "worker shutdown/reopen must preserve terminal durable job state without rerunning work");
            }

            string profileBRoot = Path.Combine(root, "profiles", "plunder-B");
            using (ProfileRuntimeOwner profileB = CreateOwner(
                "plunder-B", profileBRoot, registry, installationHooks,
                new InstrumentedActionProvider(318, connected: false), startWorkers: false))
            {
                JsonElement jobs = await ListJobsAsync(profileB.Backend, "plunder-B").ConfigureAwait(false);
                Require(jobs.GetProperty("dispatchJobs").GetArrayLength() == 0 &&
                        jobs.GetProperty("truckJobs").GetArrayLength() == 0,
                    "due-worker persistence must stay isolated from a second profile runtime");
            }

            string retiringProfileRoot = Path.Combine(root, "profiles", "retiring-A");
            string isolatedProfileRoot = Path.Combine(root, "profiles", "retiring-B");
            long retirementNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using (ProfileRuntimeOwner isolated = CreateOwner(
                "retiring-B", isolatedProfileRoot, registry, installationHooks,
                new InstrumentedActionProvider(318, connected: false), startWorkers: false))
            {
                await ScheduleDueJobsAsync(isolated.Backend, "retiring-B", 318, retirementNow).ConfigureAwait(false);
                JsonElement queued = await ListJobsAsync(isolated.Backend, "retiring-B").ConfigureAwait(false);
                Require(queued.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == "scheduled" &&
                        queued.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == "scheduled",
                    "profile B due jobs must remain queued while its worker owner is disabled");
            }

            var blocked = new InstrumentedActionProvider(serverId: 317, connected: true, blockArms: true);
            ProfileRuntimeOwner retiring = CreateOwner(
                "retiring-A", retiringProfileRoot, registry, installationHooks, blocked, startWorkers: true);
            int retiredDispatchEvents = 0;
            int retiredTruckEvents = 0;
            retiring.Map317.DispatchPlunderChanged += () => Interlocked.Increment(ref retiredDispatchEvents);
            retiring.Map317.TruckPlunderChanged += () => Interlocked.Increment(ref retiredTruckEvents);
            await ScheduleDueJobsAsync(retiring.Backend, "retiring-A", 317, retirementNow).ConfigureAwait(false);
            await blocked.WaitForBlockedArmsAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            JsonElement inFlight = await ListJobsAsync(retiring.Backend, "retiring-A").ConfigureAwait(false);
            Require(inFlight.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == "running" &&
                    inFlight.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == "running",
                "both due workers must persist running state before the owner-retirement barrier");
            Require(blocked.DispatchArmCalls == 1 && blocked.TruckArmCalls == 1,
                "retirement boundary must hold exactly one in-flight arm for each due worker");

            retiring.Dispose();
            Require(blocked.DispatchArmCancellationSignals > 0 && blocked.TruckArmCancellationSignals > 0,
                "ProfileRuntimeOwner disposal must cancel both in-flight plunder provider tokens");
            int dispatchDrainsAfterRetirement = blocked.DispatchDrainCalls;
            int truckDrainsAfterRetirement = blocked.TruckDrainCalls;
            int dispatchEventsAfterRetirement = retiredDispatchEvents;
            int truckEventsAfterRetirement = retiredTruckEvents;

            await blocked.ReleaseBlockedArmsAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Require(blocked.PendingDispatchResults == 1 && blocked.PendingTruckResults == 1,
                "late provider success must exist after retirement so the inverse proves it is not consumed");
            Require(blocked.DispatchArmCalls == 1 && blocked.TruckArmCalls == 1,
                "retired workers must never re-arm after their late provider completions");
            Require(blocked.DispatchDrainCalls == dispatchDrainsAfterRetirement &&
                    blocked.TruckDrainCalls == truckDrainsAfterRetirement,
                "retired workers must never drain late provider results");
            Require(retiredDispatchEvents == dispatchEventsAfterRetirement &&
                    retiredTruckEvents == truckEventsAfterRetirement,
                "late provider completions must not publish retired owner events");

            using (ProfileRuntimeOwner restarted = CreateOwner(
                "retiring-A", retiringProfileRoot, registry, installationHooks,
                new InstrumentedActionProvider(317, connected: false), startWorkers: false))
            {
                JsonElement recovered = await ListJobsAsync(restarted.Backend, "retiring-A").ConfigureAwait(false);
                Require(recovered.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == "waiting_connection" &&
                        recovered.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == "waiting_connection",
                    "restart must recover retired in-flight durable jobs instead of accepting late success");
            }

            using (ProfileRuntimeOwner isolatedRestart = CreateOwner(
                "retiring-B", isolatedProfileRoot, registry, installationHooks,
                new InstrumentedActionProvider(318, connected: false), startWorkers: false))
            {
                JsonElement isolatedJobs = await ListJobsAsync(isolatedRestart.Backend, "retiring-B").ConfigureAwait(false);
                Require(isolatedJobs.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == "scheduled" &&
                        isolatedJobs.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == "scheduled",
                    "profile A retirement/restart must not mutate profile B durable due jobs");
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static ProfileRuntimeOwner CreateOwner(
        string profileId,
        string profileRoot,
        ProfileRegistryCommandService registry,
        GameInstallationTestHooks installationHooks,
        Map317.IMapActionProvider actionProvider,
        bool startWorkers)
    {
        var config = new LocalConfigStore(
            Path.Combine(profileRoot, "local-config"),
            initialValue: LWBridgeLocalConfig.CreateDefault() with { ProfileId = profileId });
        var mapProvider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(Context(actionProvider is InstrumentedActionProvider provider ? provider.ServerId : 317)),
            _ => ValueTask.FromResult(Context(actionProvider is InstrumentedActionProvider provider ? provider.ServerId : 317)),
            (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(
                Accepted: false,
                TotalBlocks: 1,
                NativeCaptureReady: false,
                Error: "plunder worker boundary does not execute map scans")),
            _ => ValueTask.CompletedTask);
        return ProfileRuntimeOwner.Create(
            profileId,
            config,
            profileRoot,
            registry,
            mapProvider: mapProvider,
            mapActionProvider: actionProvider,
            startPlunderWorkers: startWorkers,
            startAutoScheduler: false,
            startRecoveryMonitor: false,
            startBridgeTransport: false,
            installationTestHooks: installationHooks,
            overviewRuntimeRoot: Path.Combine(profileRoot, "isolated-overview-runtime"),
            overviewEvidenceRoot: Path.Combine(profileRoot, "isolated-overview-evidence"),
            overviewBackupRoot: Path.Combine(profileRoot, "isolated-overview-backups"));
    }

    private static Map317.MapProviderContext Context(int serverId) => new(
        IsAvailable: true,
        IsInWorld: true,
        ServerId: serverId,
        ServerIdSource: "live",
        WorldId: 1,
        TileWidth: 100,
        TileHeight: 100,
        ExpectedTotalBlocks: 1);

    private static GameInstallationTestHooks MissingInstallation(string root) => new()
    {
        DefaultRoot = Path.Combine(root, "missing-default"),
        GetEnvironmentVariable = _ => null,
        NearbyRoot = Path.Combine(root, "missing-nearby"),
        LocalAppData = Path.Combine(root, "missing-local"),
        DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
    };

    private static async Task ScheduleDueJobsAsync(
        LWBridgeBackend backend,
        string profileId,
        int serverId,
        long now)
    {
        _ = await backend.InvokeAsync(
            "map_dispatch_plunder_schedule",
            JsonSerializer.SerializeToElement(new
            {
                profileId,
                rows = new[]
                {
                    new
                    {
                        taskKind = "dispatch",
                        serverId,
                        ownerServer = serverId,
                        uuid = "7001",
                        ownerName = "Due Dispatch",
                        completionTime = now - 5_000,
                        plunderAt = now - 1_000,
                        taskExpireTime = now + 60_000,
                        stolenCount = 0,
                        maxStealCount = 2,
                        rewards = Array.Empty<object>(),
                    },
                },
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
        _ = await backend.InvokeAsync(
            "map_truck_plunder_schedule",
            JsonSerializer.SerializeToElement(new
            {
                profileId,
                rows = new[]
                {
                    new
                    {
                        serverId,
                        uuid = "8001",
                        ownerName = "Due Truck",
                        executeAt = now - 1_000,
                        maxLootCount = 2,
                        robTimes = 0,
                        expireAt = now + 60_000,
                    },
                },
            }, JsonOptions.Default),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<JsonElement> WaitForStatusesAsync(
        LWBridgeBackend backend,
        string profileId,
        string dispatchStatus,
        string truckStatus,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        JsonElement last = default;
        do
        {
            last = await ListJobsAsync(backend, profileId).ConfigureAwait(false);
            if (last.GetProperty("dispatchJobs").GetArrayLength() == 1 &&
                last.GetProperty("truckJobs").GetArrayLength() == 1 &&
                last.GetProperty("dispatchJobs")[0].GetProperty("scheduleStatus").GetString() == dispatchStatus &&
                last.GetProperty("truckJobs")[0].GetProperty("scheduleStatus").GetString() == truckStatus)
                return last;
            await Task.Delay(25).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        throw new InvalidOperationException(
            $"Map317 plunder worker boundary did not reach {dispatchStatus}/{truckStatus}: {last.GetRawText()}");
    }

    private static async Task<JsonElement> ListJobsAsync(LWBridgeBackend backend, string profileId) =>
        JsonSerializer.SerializeToElement(
            await backend.InvokeAsync(
                "map_plunder_jobs_list",
                JsonSerializer.SerializeToElement(new { profileId }, JsonOptions.Default),
                CancellationToken.None).ConfigureAwait(false),
            JsonOptions.Default);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Map317 plunder worker boundary check failed: " + message);
    }

    private sealed class InstrumentedActionProvider(int serverId, bool connected, bool blockArms = false) : Map317.IMapActionProvider
    {
        private readonly ConcurrentQueue<Map317.DispatchPlunderResultEvent> dispatchResults = new();
        private readonly ConcurrentQueue<Map317.TruckPlunderResultEvent> truckResults = new();
        private readonly TaskCompletionSource<bool> dispatchArmEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> truckArmEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> armRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> dispatchArmCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> truckArmCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int ServerId { get; } = serverId;
        internal int CurrentServerCalls;
        internal int DispatchArmCalls;
        internal int TruckArmCalls;
        internal int DispatchDrainCalls;
        internal int TruckDrainCalls;
        internal int DispatchArmCancellationSignals;
        internal int TruckArmCancellationSignals;
        internal int PendingDispatchResults => dispatchResults.Count;
        internal int PendingTruckResults => truckResults.Count;

        internal Task WaitForBlockedArmsAsync(TimeSpan timeout) =>
            Task.WhenAll(dispatchArmEntered.Task, truckArmEntered.Task).WaitAsync(timeout);

        internal async Task ReleaseBlockedArmsAsync(TimeSpan timeout)
        {
            armRelease.TrySetResult(true);
            await Task.WhenAll(dispatchArmCompleted.Task, truckArmCompleted.Task).WaitAsync(timeout).ConfigureAwait(false);
        }

        public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref CurrentServerCalls);
            return connected
                ? ValueTask.FromResult(ServerId)
                : ValueTask.FromException<int>(Unavailable());
        }

        public async ValueTask<IReadOnlyList<Map317.DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
            IReadOnlyList<Map317.DispatchPlunderArmJob> jobs,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref DispatchArmCalls);
            if (!connected) throw Unavailable();
            using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
                () => Interlocked.Increment(ref DispatchArmCancellationSignals));
            if (blockArms)
            {
                dispatchArmEntered.TrySetResult(true);
                await armRelease.Task.ConfigureAwait(false);
            }
            foreach (Map317.DispatchPlunderArmJob job in jobs)
                dispatchResults.Enqueue(new Map317.DispatchPlunderResultEvent(
                    job.Kind, job.ServerId, job.TaskUuid, Success: true));
            dispatchArmCompleted.TrySetResult(true);
            return jobs.Select(job => new Map317.DispatchPlunderArmResult(
                job.Kind, job.ServerId, job.TaskUuid, Armed: true)).ToArray();
        }

        public async ValueTask<Map317.TruckPlunderArmResult> ArmTruckPlunderAsync(
            Map317.TruckPlunderArmJob job,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref TruckArmCalls);
            if (!connected) throw Unavailable();
            using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
                () => Interlocked.Increment(ref TruckArmCancellationSignals));
            if (blockArms)
            {
                truckArmEntered.TrySetResult(true);
                await armRelease.Task.ConfigureAwait(false);
            }
            truckResults.Enqueue(new Map317.TruckPlunderResultEvent(
                job.ServerId,
                job.TrainUuid,
                job.JobId,
                Success: true,
                BattleWon: true,
                RobTimes: 1,
                RemainingLootCount: 1,
                DailyRobCount: 1));
            truckArmCompleted.TrySetResult(true);
            return new Map317.TruckPlunderArmResult(Armed: true);
        }

        public ValueTask<IReadOnlyList<Map317.DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref DispatchDrainCalls);
            return ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>(Drain(dispatchResults));
        }

        public ValueTask<IReadOnlyList<Map317.TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref TruckDrainCalls);
            return ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>(Drain(truckResults));
        }

        public ValueTask GotoWorldCoordinateAsync(int serverId, int x, int y, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask GotoWorldMarchAsync(int serverId, string marchUuid, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<Map317.TreasureInspectionResult> InspectTreasureStatesAsync(IReadOnlyList<JsonElement> records, bool refresh, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new Map317.TreasureInspectionResult(string.Empty, string.Empty, Array.Empty<JsonElement>()));
        public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(JsonSerializer.SerializeToElement(new { }));
        public ValueTask<JsonElement> ClaimTreasuresAsync(Map317.TreasureClaimProviderRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult(JsonSerializer.SerializeToElement(new { }));
        public ValueTask<Map317.DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(JsonElement row, CancellationToken cancellationToken = default) => ValueTask.FromResult(new Map317.DispatchShareProviderResult(false));
        public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default) => ValueTask.FromResult(rows);
        public ValueTask<Map317.MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<Map317.MapPlunderServerDayProviderResult?>(null);
        public ValueTask ClearTruckPlunderPendingAsync(int serverId, string trainUuid, string jobId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        private static Map317.BridgeCommandException Unavailable() =>
            new("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");

        private static T[] Drain<T>(ConcurrentQueue<T> queue)
        {
            var items = new List<T>();
            while (queue.TryDequeue(out T? item)) items.Add(item);
            return items.ToArray();
        }
    }
}
