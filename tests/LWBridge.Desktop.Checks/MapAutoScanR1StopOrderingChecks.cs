using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Held native Stop/Start are production scheduler boundaries, not game actions.
internal static class MapAutoScanR1StopOrderingChecks
{
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("MAP006_R1_STOP_ORDER: " + message);
    }

    private static async Task CheckAsync(string scenario, bool later = false,
        bool heldStart = false, bool retire = false, bool returnEnabled = true,
        bool stopThrows = false, bool returnThrows = false, bool successor = false,
        bool alreadyTerminal = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "map006-r1-order-" + Guid.NewGuid().ToString("N"));
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int server = 317, stops = 0, returns = 0;
        bool nativeLeaseHeld = false, returnBeforeStop = false, exactRunTouched = false;
        var starts = new List<int>();
        try
        {
            await using var auto = new MapAutoScanCommandService(Path.Combine(root, "auto.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = async (run, token) =>
                    {
                        if (run is null) return new(317, false, null, null, "idle");
                        if (run == "first-local") { nativeLeaseHeld = false; return new(317, false, null, run, "completed"); }
                        if (successor && !token.CanBeCanceled)
                            return new(server, true, null, "independent-successor", "running");
                        if (alreadyTerminal && !token.CanBeCanceled)
                            return new(server, false, null, run, "cancelled");
                        pollEntered.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        throw new InvalidOperationException("held poll must cancel");
                    },
                    StartTargetScanAsync = async (target, _, _, _) =>
                    {
                        starts.Add(target);
                        server = target;
                        nativeLeaseHeld = true;
                        if (heldStart) { startEntered.TrySetResult(); await startRelease.Task; }
                        return later && target == 317 ? "first-local" : "exact-foreign";
                    },
                    StopScanIfOwnedAsync = async (run, _) =>
                    {
                        Require(run == "exact-foreign", scenario + ": Stop targeted successor");
                        exactRunTouched = true;
                        Interlocked.Increment(ref stops);
                        stopEntered.TrySetResult();
                        await stopRelease.Task;
                        if (stopThrows) throw new InvalidOperationException("controlled native Stop rejected");
                        if (successor || alreadyTerminal) return false;
                        nativeLeaseHeld = false;
                        return true;
                    },
                    ReturnServerAsync = (target, _) =>
                    {
                        Require(target == 317, scenario + ": return targeted wrong server");
                        Interlocked.Increment(ref returns);
                        returnBeforeStop = nativeLeaseHeld;
                        if (nativeLeaseHeld) throw new InvalidOperationException("SCAN_RUNNING: lease retained");
                        if (returnThrows) throw new InvalidOperationException("controlled return rejected");
                        server = target;
                        return Task.CompletedTask;
                    },
                }, new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);

            int[] targets = later ? [317, 318, 319] : [318, 319];
            await auto.UpdateConfigAsync(new(true, 20, targets, ["truck"], "fast", returnEnabled, 0));
            await auto.RunNowAsync();
            if (heldStart) await startEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            else await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task ending = retire ? auto.RetireAsync() : auto.CancelAsync();
            startRelease.TrySetResult();
            await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(70);
            Require(returns == 0 && !ending.IsCompleted,
                scenario + ": return/terminalization escaped held native Stop");
            if (alreadyTerminal) nativeLeaseHeld = false;
            stopRelease.TrySetResult();
            await ending.WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot state = await auto.GetSnapshotAsync();
            MapAutoScanCycleRecord cycle = state.LastCycle ?? throw new InvalidOperationException(
                scenario + ": missing terminal cycle");
            bool needsReturn = returnEnabled && !retire && !stopThrows && !successor;
            Require(exactRunTouched && stops == 1 && starts.SequenceEqual(later ? [317, 318] : [318]) &&
                returns == (needsReturn ? 1 : 0) && !returnBeforeStop &&
                (server == 317) == (needsReturn && !returnThrows) &&
                !state.Running && !state.OwnsActiveScan &&
                cycle.CompletedServerIds.SequenceEqual(later ? [317] : []) &&
                cycle.TargetServerIds.SequenceEqual(targets),
                scenario + ": lost admission/cancel/return/ownership order");
            string? expectedError = stopThrows ? "MAP_AUTO_SCAN_STOP_FAILED"
                : successor ? "MAP_AUTO_SCAN_STOP_OWNER_MISMATCH"
                : returnThrows ? "MAP_AUTO_SCAN_RETURN_FAILED" : null;
            Require((expectedError is null && cycle.LastError is null) ||
                (expectedError is not null && cycle.LastError?.Contains(expectedError, StringComparison.Ordinal) == true),
                scenario + ": missing or spurious terminal error");
            Require(cycle.Outcome == (retire ? "retired"
                : expectedError is not null ? "failed" : "canceled"),
                scenario + ": untruthful terminal outcome");
            if (!retire) await auto.UpdateConfigAsync(state.Config with { Enabled = false });
        }
        finally
        {
            startRelease.TrySetResult();
            stopRelease.TrySetResult();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    internal static async Task RunAsync()
    {
        await CheckAsync("foreign-first held Stop");
        await CheckAsync("foreign-later held Stop", later: true);
        await CheckAsync("cancel deferred Start", heldStart: true);
        await CheckAsync("no requested return", returnEnabled: false);
        await CheckAsync("native Stop throws", stopThrows: true);
        await CheckAsync("native return throws", returnThrows: true);
        await CheckAsync("foreign successor refused", successor: true);
        await CheckAsync("already-terminal exact run", alreadyTerminal: true);
        await CheckAsync("retired owner no return", retire: true);
        Console.WriteLine("MAP_006_R1_HELD_CROSS_SERVER_STOP_RETURN_OWNERSHIP_OK");
    }
}
