using LWBridge.Map317;

namespace LWBridge.Map317.Checks;

internal static class ScanStateChecks
{
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        DefaultIdleStateMatchesContract();
        await StartAndModeChecksAsync();
        await DuplicateStartIsRejectedAsync();
        await InvalidModeAndTypesAreRejectedAsync();
        await ProgressContractIsDerivedAsync();
        await StopRestartAndClearUseOwnedHooksAsync();
        await CancelFailureIsFailClosedAsync();
        await ResumeRemainsUnavailableWithoutTrueProducerAsync();
        await UnavailableProviderFailsExplicitlyAsync();

        Console.WriteLine("LWBridge.Map-0.3.17 scan-state checks passed.");
    }

    private static void DefaultIdleStateMatchesContract()
    {
        MapScanState state = MapScanStateMachine.CreateDefaultIdleState();
        Check(state.ServerId == 0, "idle serverId");
        Check(state.ServerIdSource == "none", "idle serverIdSource");
        Check(state.ScanRunId == string.Empty, "idle scanRunId");
        Check(!state.IsReading && state.Phase == "idle", "idle reading/phase");
        Check(state.SelectedTypes.SequenceEqual(MapKinds.All), "idle selected types");
        Check(state.TotalBlocks == 0 && state.CompletedBlocks == 0 && state.ReadBlocks == 0,
            "idle block counters");
        Check(state.FailedBlocks == 0 && state.UnreadBlocks == 0 && state.InflightBlocks == 0,
            "idle remaining counters");
        Check(state.ScanMode == "normal" && state.Concurrency == 8, "idle mode/concurrency");
        Check(state.ScanRate == 0 && state.ProgressPercent == 0, "idle rate/progress");
        Check(!state.NativeCaptureReady && state.NativePendingRecords == 0 && state.NativeDroppedRecords == 0,
            "idle native capture fields");
        Check(!state.ResumeAvailable && state.Error is null, "idle resume/error");
    }

    private static async Task StartAndModeChecksAsync()
    {
        var provider = new FakeMapProvider(totalBlocks: 12);
        provider.Context = provider.Context with { IsInWorld = false };
        var sink = new FakeLocalSink();
        var machine = new MapScanStateMachine(provider, sink);
        int events = 0;
        machine.StateChanged += (_, _) => events++;

        MapScanState normal = await machine.StartAsync(
            new MapScanStartRequest(["city", "resource", "city"], "normal"));
        Check(normal.IsReading && normal.Phase == "scanning", "normal start reading/phase");
        Check(normal.ServerId == provider.Context.ServerId && normal.ServerIdSource == "live",
            "normal start server ownership");
        Check(normal.SelectedTypes.SequenceEqual(new[] { "city", "resource" }),
            "start filters duplicates while retaining current valid types");
        Check(normal.ScanMode == "normal" && normal.Concurrency == 8, "normal concurrency");
        Check(normal.TotalBlocks == 12 && normal.UnreadBlocks == 12, "provider-owned block count");
        Check(provider.EnterCalls == 1, "start enters world map through high-level provider gate");
        Check(provider.LastStart is not null && provider.LastStart.Concurrency == 8,
            "normal provider request concurrency");
        Check(events == 1, "start state-changed event");

        await machine.StopAsync();
        MapScanState fast = await machine.StartAsync(new MapScanStartRequest(["treasure"], "fast"));
        Check(fast.ScanMode == "fast" && fast.Concurrency == 20, "fast concurrency");
        Check(provider.LastStart is not null && provider.LastStart.ScanMode == "fast" &&
              provider.LastStart.Concurrency == 20,
            "fast provider request");
    }

    private static async Task DuplicateStartIsRejectedAsync()
    {
        var provider = new FakeMapProvider();
        var machine = new MapScanStateMachine(provider, new FakeLocalSink());
        MapScanState first = await machine.StartAsync();
        int startCalls = provider.StartCalls;

        BridgeCommandException error = await ExpectBridgeErrorAsync(
            () => machine.StartAsync(new MapScanStartRequest(["city"], "fast")).AsTask());
        Check(error.Code == "SCAN_RUNNING" && error.Message == "map scan already running",
            "duplicate start current error");
        Check(provider.StartCalls == startCalls, "duplicate start does not issue another provider start");
        Check(machine.State.ScanRunId == first.ScanRunId && machine.State.IsReading,
            "duplicate start preserves active state");
    }

    private static async Task InvalidModeAndTypesAreRejectedAsync()
    {
        var modeProvider = new FakeMapProvider();
        var modeMachine = new MapScanStateMachine(modeProvider, new FakeLocalSink());
        BridgeCommandException modeError = await ExpectBridgeErrorAsync(
            () => modeMachine.StartAsync(new MapScanStartRequest(["city"], "turbo")).AsTask());
        Check(modeError.Code == "INVALID_SCAN_MODE" &&
              modeError.Message == "map scan mode must be normal or fast",
            "invalid mode current error");
        Check(modeProvider.StartCalls == 0 && !modeMachine.State.IsReading,
            "invalid mode cannot start provider scan");

        var typeProvider = new FakeMapProvider();
        var typeMachine = new MapScanStateMachine(typeProvider, new FakeLocalSink());
        BridgeCommandException typeError = await ExpectBridgeErrorAsync(
            () => typeMachine.StartAsync(new MapScanStartRequest(["zombie_boss"], "normal")).AsTask());
        Check(typeError.Code == "INVALID_SCAN_TYPES" &&
              typeError.Message == "no valid map scan types selected",
            "invalid type current error");
        Check(typeProvider.StartCalls == 0 && !typeMachine.State.IsReading,
            "invalid types cannot start provider scan");

        var allProvider = new FakeMapProvider();
        var allMachine = new MapScanStateMachine(allProvider, new FakeLocalSink());
        MapScanState all = await allMachine.StartAsync(new MapScanStartRequest(MapKinds.All, "normal"));
        Check(all.SelectedTypes.SequenceEqual(MapKinds.All), "all eight recovered types accepted");
    }

    private static async Task ProgressContractIsDerivedAsync()
    {
        var provider = new FakeMapProvider(totalBlocks: 10);
        long now = 1_000;
        var machine = new MapScanStateMachine(provider, new FakeLocalSink(), () => now);
        await machine.StartAsync(new MapScanStartRequest(["city"], "normal"));
        now = 1_002;
        int events = 0;
        machine.StateChanged += (_, _) => events++;

        MapScanState progress = machine.ReportProgress(new MapScanProgressUpdate(
            CompletedBlocks: 3,
            ReadBlocks: 3,
            FailedBlocks: 1,
            InflightBlocks: 2,
            ScanRate: 4.5,
            NativeCaptureReady: true,
            NativePendingRecords: 7,
            NativeDroppedRecords: 2));

        Check(progress.UnreadBlocks == 4, "unread excludes completed, failed and bounded inflight blocks");
        Check(progress.ProgressPercent == 40.0, "active progress uses completed+failed fraction");
        Check(progress.ReadBlocks == 3 && progress.InflightBlocks == 2 && progress.ScanRate == 1.5,
            "progress derives readBlocks and scanRate from current completed count/time");
        Check(progress.NativeCaptureReady && progress.NativePendingRecords == 7 &&
              progress.NativeDroppedRecords == 2,
            "progress native counters");
        Check(events == 1, "progress state-changed event");

        Check(MapScanStateMachine.UnreadBlocks(10, 8, 0, 2) == 0, "unread lower clamp");
        Check(MapScanStateMachine.ProgressPercent(0, 0, 0, "scanning") == 0.0,
            "zero-total progress");
        Check(MapScanStateMachine.ProgressPercent(10, 10, 0, "completed") == 100.0,
            "completed terminal progress");
        Check(MapScanStateMachine.ProgressPercent(100, 99, 0, "scanning") == 98.0,
            "active progress 98-percent clamp");

        BridgeCommandException invalid = ExpectBridgeError(() => machine.ReportProgress(
            new MapScanProgressUpdate(9, 9, 5, 0, 0, false, 0, 0)));
        Check(invalid.Code == "INVALID_SCAN_PROGRESS" &&
              invalid.Message == "completed and failed map blocks exceed the scan total",
            "invalid progress current error");
    }

    private static async Task StopRestartAndClearUseOwnedHooksAsync()
    {
        var provider = new FakeMapProvider(totalBlocks: 9) { ThrowOnStop = true };
        var sink = new FakeLocalSink();
        var machine = new MapScanStateMachine(provider, sink);
        MapScanState started = await machine.StartAsync(new MapScanStartRequest(["dispatch"], "normal"));
        machine.ReportProgress(new MapScanProgressUpdate(2, 2, 0, 1, 3.0, true, 1, 0));

        MapScanState stopped = await machine.StopAsync();
        Check(!stopped.IsReading && stopped.Phase == "idle" && stopped.InflightBlocks == 0,
            "stop returns non-reading idle state");
        Check(stopped.ScanRunId == started.ScanRunId && stopped.ServerId == started.ServerId,
            "stop preserves run/server identity for host-visible state and clear ownership");
        Check(stopped.CompletedBlocks == 2 && stopped.ReadBlocks == 2 && stopped.UnreadBlocks == 6,
            "stop preserves progress counters");
        Check(!stopped.ResumeAvailable, "stop resume false");
        Check(sink.CancelledRuns.SequenceEqual(new[] { started.ScanRunId }),
            "stop owns local cancellation hook");
        Check(provider.StopCalls == 1, "provider stop attempted best-effort");

        provider.ThrowOnStop = false;
        MapScanState restarted = await machine.StartAsync(new MapScanStartRequest(["dispatch"], "fast"));
        Check(restarted.IsReading && restarted.ScanRunId != started.ScanRunId,
            "restart creates a fresh run after stop");
        await machine.StopAsync();

        MapScanState cleared = await machine.ClearAsync(provider.Context.ServerId);
        Check(sink.ClearedServers.SequenceEqual(new[] { provider.Context.ServerId }),
            "clear uses injected server-owned sink");
        MapScanState expectedIdle = MapScanStateMachine.CreateDefaultIdleState();
        Check(cleared.ServerId == expectedIdle.ServerId &&
              cleared.ServerIdSource == expectedIdle.ServerIdSource &&
              cleared.ScanRunId == expectedIdle.ScanRunId &&
              cleared.IsReading == expectedIdle.IsReading &&
              cleared.Phase == expectedIdle.Phase &&
              cleared.SelectedTypes.SequenceEqual(expectedIdle.SelectedTypes) &&
              cleared.TotalBlocks == expectedIdle.TotalBlocks &&
              cleared.CompletedBlocks == expectedIdle.CompletedBlocks &&
              cleared.ReadBlocks == expectedIdle.ReadBlocks &&
              cleared.FailedBlocks == expectedIdle.FailedBlocks &&
              cleared.UnreadBlocks == expectedIdle.UnreadBlocks &&
              cleared.InflightBlocks == expectedIdle.InflightBlocks &&
              cleared.ScanMode == expectedIdle.ScanMode &&
              cleared.Concurrency == expectedIdle.Concurrency &&
              cleared.ScanRate == expectedIdle.ScanRate &&
              cleared.ProgressPercent == expectedIdle.ProgressPercent &&
              cleared.NativeCaptureReady == expectedIdle.NativeCaptureReady &&
              cleared.NativePendingRecords == expectedIdle.NativePendingRecords &&
              cleared.NativeDroppedRecords == expectedIdle.NativeDroppedRecords &&
              cleared.ResumeAvailable == expectedIdle.ResumeAvailable &&
              cleared.Error == expectedIdle.Error,
            "clear resets to exact default idle state");
    }

    private static async Task CancelFailureIsFailClosedAsync()
    {
        var provider = new FakeMapProvider();
        var sink = new FakeLocalSink { ThrowOnCancel = true };
        var machine = new MapScanStateMachine(provider, sink);
        MapScanState started = await machine.StartAsync();

        try
        {
            await machine.StopAsync();
            throw new InvalidOperationException("expected local cancellation failure");
        }
        catch (InvalidOperationException error) when (error.Message == FakeLocalSink.CancelFailureMessage)
        {
        }

        Check(machine.State.IsReading && machine.State.ScanRunId == started.ScanRunId,
            "failed local cancel cannot claim idle success");
        Check(provider.StopCalls == 0, "provider stop does not bypass failed local cancellation");
    }

    private static async Task ResumeRemainsUnavailableWithoutTrueProducerAsync()
    {
        var provider = new FakeMapProvider();
        var machine = new MapScanStateMachine(provider, new FakeLocalSink());
        MapScanState state = await machine.StartAsync(new MapScanStartRequest(["city"], "normal", Resume: true));

        Check(provider.LastStart is not null && !provider.LastStart.Resume,
            "request.resume true alone cannot enable resume");
        Check(!state.ResumeAvailable, "successful start does not fabricate resumeAvailable true");
    }

    private static async Task UnavailableProviderFailsExplicitlyAsync()
    {
        var machine = new MapScanStateMachine(UnavailableMapProvider.Instance, new FakeLocalSink());
        BridgeCommandException error = await ExpectBridgeErrorAsync(() => machine.StartAsync().AsTask());
        Check(error.Code == "GAME_CONNECTION_UNAVAILABLE" && error.Message == "game connection unavailable",
            "unavailable provider current game-connection error");
        Check(!machine.State.IsReading && machine.State.ScanRunId == string.Empty,
            "unavailable provider cannot synthesize a scan");
    }

    private static async Task<BridgeCommandException> ExpectBridgeErrorAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (BridgeCommandException error)
        {
            return error;
        }

        throw new InvalidOperationException("expected BridgeCommandException");
    }

    private static BridgeCommandException ExpectBridgeError(Action action)
    {
        try
        {
            action();
        }
        catch (BridgeCommandException error)
        {
            return error;
        }
        throw new InvalidOperationException("expected BridgeCommandException");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"scan-state check failed: {message}");
    }

    private sealed class FakeMapProvider : IMapProvider
    {
        private readonly int totalBlocks;

        public FakeMapProvider(int totalBlocks = 8)
        {
            this.totalBlocks = totalBlocks;
        }

        public MapProviderContext Context { get; set; } =
            new(true, true, 2212, "live", 0, 1000, 1000);

        public int ContextCalls { get; private set; }
        public int EnterCalls { get; private set; }
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public bool ThrowOnStop { get; set; }
        public MapProviderStartRequest? LastStart { get; private set; }

        public ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default)
        {
            ContextCalls++;
            return ValueTask.FromResult(Context);
        }

        public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default)
        {
            EnterCalls++;
            Context = Context with { IsInWorld = true };
            return ValueTask.FromResult(Context);
        }

        public ValueTask<MapProviderStartResult> StartMapScanAsync(
            MapProviderStartRequest request,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            LastStart = request;
            return ValueTask.FromResult(new MapProviderStartResult(
                Accepted: true,
                TotalBlocks: totalBlocks,
                NativeCaptureReady: false));
        }

        public ValueTask StopMapScanAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            if (ThrowOnStop)
                throw new InvalidOperationException("synthetic provider stop failure");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeLocalSink : IMapScanLocalSink
    {
        public const string CancelFailureMessage = "synthetic local cancel failure";

        public List<string> CancelledRuns { get; } = [];
        public List<int> ClearedServers { get; } = [];
        public bool ThrowOnCancel { get; set; }

        public ValueTask CancelScanAsync(string scanRunId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnCancel)
                throw new InvalidOperationException(CancelFailureMessage);
            CancelledRuns.Add(scanRunId);
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearServerAsync(int serverId, CancellationToken cancellationToken = default)
        {
            ClearedServers.Add(serverId);
            return ValueTask.CompletedTask;
        }
    }
}
