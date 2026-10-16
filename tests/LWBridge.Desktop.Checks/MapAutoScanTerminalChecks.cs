using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Production scheduler, persistence and ownership with only its game boundary
// controlled. A terminal native scan must have positively published to count.
internal static class MapAutoScanTerminalChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP_006_AUTO: " + message);
    }

    private static async Task CheckAsync(
        string label, string phase, string? terminalError,
        string resultRunId, int resultServer, bool expectedSuccess)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-auto-" + Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(root, "auto-state.json");
        int starts = 0;
        int returns = 0;
        int stops = 0;
        try
        {
            await using (var scheduler = new MapAutoScanCommandService(
                statePath,
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) => Task.FromResult(run is null
                        ? new MapAutoScanRuntimeStatus(317, false, null, null, "idle")
                        : new MapAutoScanRuntimeStatus(resultServer, false, terminalError, resultRunId, phase)),
                    StartTargetScanAsync = (server, kinds, speed, _) =>
                    {
                        Require(server == 317 && kinds.SequenceEqual(["monster"]) && speed == "fast",
                            label + ": wrong scan target/configuration");
                        Interlocked.Increment(ref starts);
                        return Task.FromResult("owned-006");
                    },
                    ReturnServerAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref returns);
                        return Task.CompletedTask;
                    },
                    StopScanIfOwnedAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref stops);
                        return Task.FromResult(true);
                    },
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false))
            {
                _ = await scheduler.UpdateConfigAsync(new MapAutoScanConfig(
                    true, 20, [317], ["monster"], "fast", false, 0));
                await scheduler.RunNowAsync();
                await scheduler.WaitForIdleAsync();
                MapAutoScanSnapshot result = await scheduler.GetSnapshotAsync();
                // A matching terminal failure already releases its owner token;
                // a mismatched response leaves that token owned and must invoke
                // an exact-run Stop rather than touch a successor.
                int expectedStops = resultRunId != "owned-006" || resultServer != 317 ? 1 : 0;
                Require(starts == 1 && returns == 0 && stops == expectedStops,
                    label + ": incorrect controlled action count");
                Require(result.LastCycle is not null && result.LastCycle.CompletedServerIds.Count ==
                    (expectedSuccess ? 1 : 0), label + ": terminal result counted without publication");
                Require(result.LastCycle!.Outcome == (expectedSuccess ? "completed" : "failed"),
                    label + ": incorrect terminal outcome");
                Require((result.LastError is null) == expectedSuccess,
                    label + ": missing or spurious terminal error");
                Require(!result.OwnsActiveScan,
                    label + ": retained a stale owned-run token after terminal failure");
                _ = await scheduler.UpdateConfigAsync(result.Config with { Enabled = false });
                Require(!await scheduler.CheckDueAsync() && starts == 1,
                    label + ": disabled scheduler admitted a later cycle");
            }
            Require(File.Exists(statePath), label + ": missing persisted cycle");
            await using var reopened = new MapAutoScanCommandService(
                statePath,
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => false,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (_, _) => throw new InvalidOperationException("No game expected"),
                    StartTargetScanAsync = (_, _, _, _) => throw new InvalidOperationException("No scan expected"),
                    ReturnServerAsync = (_, _) => throw new InvalidOperationException("No jump expected"),
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("No Stop expected"),
                }, startScheduler: false);
            MapAutoScanSnapshot persisted = await reopened.GetSnapshotAsync();
            Require(!persisted.Config.Enabled &&
                persisted.LastCycle?.Outcome == (expectedSuccess ? "completed" : "failed"),
                label + ": reopened outcome or disabled state changed");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    internal static async Task RunAsync()
    {
        await Map016LaunchScheduleChecks.RunAsync();
        await CheckAsync("published", "completed", null, "owned-006", 317, true);
        await CheckAsync("cancelled", "cancelled", null, "owned-006", 317, false);
        await CheckAsync("failed", "failed", "capture failure", "owned-006", 317, false);
        await CheckAsync("stale run", "completed", null, "stale-006", 317, false);
        await CheckAsync("wrong server", "completed", null, "owned-006", 318, false);
        await CheckAsync("error despite phase", "completed", "partial failure", "owned-006", 317, false);
        await CheckSuccessAfterFailureAsync();
        await CheckOrderedMultiServerAndReturnAsync();
        await CheckReturnFailureRemainsVisibleAsync();
        await CheckDisableWhileOwnedScanActiveAsync();
        await CheckCancelStopsOnlyOwnedRunAsync();
        await CheckConfigEditDoesNotMutateAdmittedCycleAsync();
        Console.WriteLine("MAP_006_AUTO_TERMINAL_PUBLICATION_OWNERSHIP_OK");
    }

    private static async Task CheckOrderedMultiServerAndReturnAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-order-" + Guid.NewGuid().ToString("N"));
        var starts = new List<int>();
        var returns = new List<int>();
        int currentServer = 317;
        try
        {
            await using var scheduler = new MapAutoScanCommandService(
                Path.Combine(root, "auto-state.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) => Task.FromResult(run is null
                        ? new MapAutoScanRuntimeStatus(317, false, null, null, "idle")
                        : new MapAutoScanRuntimeStatus(currentServer, false, null, run, "completed")),
                    StartTargetScanAsync = (server, kinds, speed, _) =>
                    {
                        Require(kinds.SequenceEqual(["truck", "ghost"]) && speed == "fast",
                            "legacy normal setting did not normalize to the best automatic scan method");
                        starts.Add(server);
                        currentServer = server;
                        return Task.FromResult("ordered-" + starts.Count);
                    },
                    ReturnServerAsync = (server, _) =>
                    {
                        returns.Add(server);
                        currentServer = server;
                        return Task.CompletedTask;
                    },
                    StopScanIfOwnedAsync = (_, _) => Task.FromResult(true),
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            await scheduler.UpdateConfigAsync(new MapAutoScanConfig(
                true, 1, [318, 319, 318, 0, 317],
                ["truck", "unknown", "ghost", "truck"], "normal", true, 0));
            await scheduler.RunNowAsync();
            await scheduler.WaitForIdleAsync();
            MapAutoScanSnapshot result = await scheduler.GetSnapshotAsync();
            Require(starts.SequenceEqual([318, 319, 317]) && returns.SequenceEqual([317]) &&
                result.LastCycle?.Outcome == "completed" &&
                result.LastCycle.TargetServerIds.SequenceEqual([318, 319, 317]) &&
                result.LastCycle.CompletedServerIds.SequenceEqual([318, 319, 317]) &&
                result.Config.ServerIds!.SequenceEqual([318, 319, 317]) &&
                result.Config.SelectedTypes!.SequenceEqual(["truck", "ghost"]) &&
                result.Config.IntervalMinutes == MapAutoScanCommandService.MinimumIntervalMinutes &&
                currentServer == 317,
                "controlled cross-server order/return and normalized config differed");
            await scheduler.UpdateConfigAsync(result.Config with { Enabled = false });
            Require(!await scheduler.CheckDueAsync() && starts.Count == 3,
                "disabled cross-server scheduler admitted extra cycle");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckDisableWhileOwnedScanActiveAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-disable-" + Guid.NewGuid().ToString("N"));
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = new List<int>();
        try
        {
            await using var scheduler = new MapAutoScanCommandService(
                Path.Combine(root, "auto-state.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = async (run, _) =>
                    {
                        if (run is null) return new MapAutoScanRuntimeStatus(317, false, null, null, "idle");
                        entered.TrySetResult(true);
                        await release.Task.ConfigureAwait(false);
                        return new MapAutoScanRuntimeStatus(317, false, null, run, "completed");
                    },
                    StartTargetScanAsync = (server, _, _, _) =>
                    {
                        starts.Add(server);
                        return Task.FromResult("disable-" + starts.Count);
                    },
                    ReturnServerAsync = (_, _) => Task.CompletedTask,
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException(
                        "Disabling configuration must not stop an in-flight scan"),
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            await scheduler.UpdateConfigAsync(new MapAutoScanConfig(
                true, 20, [317, 318], ["monster"], "fast", false, 0));
            await scheduler.RunNowAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot before = await scheduler.GetSnapshotAsync();
            Require(before.Running && before.OwnsActiveScan && starts.SequenceEqual([317]),
                "held controlled Auto run was not owned");
            await scheduler.UpdateConfigAsync(before.Config with { Enabled = false });
            release.TrySetResult(true);
            await scheduler.WaitForIdleAsync();
            MapAutoScanSnapshot final = await scheduler.GetSnapshotAsync();
            Require(!final.Config.Enabled && !final.OwnsActiveScan && starts.SequenceEqual([317]) &&
                final.LastCycle?.Outcome == "disabled" &&
                final.LastCycle.CompletedServerIds.SequenceEqual([317]) &&
                final.LastCycle.TargetServerIds.SequenceEqual([317, 318]),
                "disable did not finish owned scan and suppress next target");
        }
        finally
        {
            release.TrySetResult(true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckConfigEditDoesNotMutateAdmittedCycleAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-config-edit-" + Guid.NewGuid().ToString("N"));
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = new List<(int Server, string Kinds)>();
        int activeServer = 317;
        try
        {
            await using var scheduler = new MapAutoScanCommandService(
                Path.Combine(root, "auto-state.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = async (run, _) =>
                    {
                        if (run is null) return new MapAutoScanRuntimeStatus(activeServer, false, null, null, "idle");
                        if (run == "edit-1")
                        {
                            entered.TrySetResult(true);
                            await release.Task.ConfigureAwait(false);
                        }
                        return new MapAutoScanRuntimeStatus(activeServer, false, null, run, "completed");
                    },
                    StartTargetScanAsync = (server, types, _, _) =>
                    {
                        activeServer = server;
                        starts.Add((server, string.Join(",", types)));
                        return Task.FromResult("edit-" + starts.Count);
                    },
                    ReturnServerAsync = (_, _) => throw new InvalidOperationException("Return was not requested"),
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("No Stop expected"),
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            await scheduler.UpdateConfigAsync(new MapAutoScanConfig(
                true, 20, [317, 318], ["railway", "dispatch"], "fast", false, 0));
            await scheduler.RunNowAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot holding = await scheduler.GetSnapshotAsync();
            Require(holding.Running && starts.SequenceEqual([(317, "railway,dispatch")]),
                "controlled first target was not held for config edit");
            await scheduler.UpdateConfigAsync(holding.Config with
            {
                ServerIds = [317, 319],
                SelectedTypes = ["railway", "ghost"],
            });
            release.TrySetResult(true);
            await scheduler.WaitForIdleAsync();
            MapAutoScanSnapshot first = await scheduler.GetSnapshotAsync();
            Require(first.LastCycle?.Outcome == "completed" &&
                first.LastCycle.CompletedServerIds.SequenceEqual([317, 318]) &&
                starts.SequenceEqual([(317, "railway,dispatch"), (318, "railway,dispatch")]) &&
                first.Config.ServerIds?.SequenceEqual([317, 319]) == true &&
                first.Config.SelectedTypes?.SequenceEqual(["railway", "ghost"]) == true,
                "editing selected kinds/server list changed an already-admitted Auto cycle");
            await scheduler.RunNowAsync();
            await scheduler.WaitForIdleAsync();
            MapAutoScanSnapshot next = await scheduler.GetSnapshotAsync();
            Require(next.LastCycle?.Outcome == "completed" &&
                next.LastCycle.CompletedServerIds.SequenceEqual([317, 319]) &&
                starts.SequenceEqual([
                    (317, "railway,dispatch"), (318, "railway,dispatch"),
                    (317, "railway,ghost"), (319, "railway,ghost")]),
                "the subsequent Auto cycle ignored newly committed kinds/server list");
            await scheduler.UpdateConfigAsync(next.Config with { Enabled = false });
        }
        finally
        {
            release.TrySetResult(true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckCancelStopsOnlyOwnedRunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-auto-cancel-" + Guid.NewGuid().ToString("N"));
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new List<int>();
        var stopped = new List<string>();
        try
        {
            await using var scheduler = new MapAutoScanCommandService(
                Path.Combine(root, "auto-state.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = async (run, token) =>
                    {
                        if (run is null) return new MapAutoScanRuntimeStatus(317, false, null, null, "idle");
                        entered.TrySetResult(true);
                        await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                        throw new InvalidOperationException("cancelled read must never publish");
                    },
                    StartTargetScanAsync = (server, _, _, _) =>
                    {
                        started.Add(server);
                        return Task.FromResult("owned-cancel-006");
                    },
                    ReturnServerAsync = (_, _) => throw new InvalidOperationException("Return was not requested"),
                    StopScanIfOwnedAsync = (run, _) =>
                    {
                        stopped.Add(run);
                        return Task.FromResult(true);
                    },
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            // The second target would require a cross-server return, but
            // cancelling the first same-origin scan did not leave server 317.
            // Return here would hit SCAN_RUNNING during native Stop teardown.
            await scheduler.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [317, 318], ["railway"], "fast", true, 0));
            await scheduler.RunNowAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await scheduler.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot final = await scheduler.GetSnapshotAsync();
            Require(started.SequenceEqual([317]) && stopped.SequenceEqual(["owned-cancel-006"]) &&
                final.LastCycle?.Outcome == "canceled" && final.LastCycle.CompletedServerIds.Count == 0 &&
                final.LastCycle.TargetServerIds.SequenceEqual([317, 318]) &&
                !final.OwnsActiveScan && !final.Running && final.LastError is null,
                "Auto Stop failed to retire exactly its running scan or admitted the next target");
            await scheduler.UpdateConfigAsync(final.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckReturnFailureRemainsVisibleAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-return-" + Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(root, "auto-state.json");
        int starts = 0;
        int returns = 0;
        try
        {
            await using (var scheduler = new MapAutoScanCommandService(
                statePath,
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) => Task.FromResult(run is null
                        ? new MapAutoScanRuntimeStatus(317, false, null, null, "idle")
                        : new MapAutoScanRuntimeStatus(318, false, null, run, "completed")),
                    StartTargetScanAsync = (server, types, mode, _) =>
                    {
                        Require(server == 318 && types.SequenceEqual(["truck"]) && mode == "fast",
                            "return-failure test did not acquire requested target");
                        starts++;
                        return Task.FromResult("return-failure-run");
                    },
                    ReturnServerAsync = (server, _) =>
                    {
                        Require(server == 317, "return failure addressed the wrong original server");
                        returns++;
                        throw new InvalidOperationException("native jump rejected");
                    },
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException(
                        "completed target must not be stopped after return failure"),
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false))
            {
                await scheduler.UpdateConfigAsync(new MapAutoScanConfig(
                    true, 20, [318], ["truck"], "fast", true, 0));
                await scheduler.RunNowAsync();
                await scheduler.WaitForIdleAsync();
                MapAutoScanSnapshot failed = await scheduler.GetSnapshotAsync();
                Require(starts == 1 && returns == 1 && !failed.OwnsActiveScan &&
                    failed.LastCycle?.Outcome == "failed" &&
                    failed.LastCycle.OriginalServerId == 317 &&
                    failed.LastCycle.TargetServerIds.SequenceEqual([318]) &&
                    failed.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                    failed.LastError?.Contains("MAP_AUTO_SCAN_RETURN_FAILED: native jump rejected", StringComparison.Ordinal) == true,
                    "failed return was hidden as an entirely completed Auto cycle or lost the published target");
                await scheduler.UpdateConfigAsync(failed.Config with { Enabled = false });
            }
            // Read the committed file again while it still exists. A later
            // process/restart must not reinterpret a published target as success.
            await using var reopened = new MapAutoScanCommandService(
                statePath, new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => false,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (_, _) => throw new InvalidOperationException("No game expected"),
                    StartTargetScanAsync = (_, _, _, _) => throw new InvalidOperationException("No scan expected"),
                    ReturnServerAsync = (_, _) => throw new InvalidOperationException("No return expected"),
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("No Stop expected"),
                }, startScheduler: false);
            MapAutoScanSnapshot durable = await reopened.GetSnapshotAsync();
            Require(durable.LastCycle?.Outcome == "failed" &&
                durable.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                durable.LastError?.Contains("MAP_AUTO_SCAN_RETURN_FAILED", StringComparison.Ordinal) == true,
                "return failure not durable across scheduler reopening");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CheckSuccessAfterFailureAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map006-auto-recovery-" + Guid.NewGuid().ToString("N"));
        string phase = "failed";
        int starts = 0;
        try
        {
            await using var scheduler = new MapAutoScanCommandService(
                Path.Combine(root, "auto-state.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) => Task.FromResult(run is null
                        ? new MapAutoScanRuntimeStatus(317, false, null, null, "idle")
                        : new MapAutoScanRuntimeStatus(317, false,
                            phase == "failed" ? "controlled producer failed" : null, run, phase)),
                    StartTargetScanAsync = (_, _, _, _) => Task.FromResult("cycle-" + ++starts),
                    ReturnServerAsync = (_, _) => Task.CompletedTask,
                    StopScanIfOwnedAsync = (_, _) => Task.FromResult(true),
                },
                new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            await scheduler.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [317], ["monster"], "fast", false, 0));
            await scheduler.RunNowAsync();
            await scheduler.WaitForIdleAsync();
            Require((await scheduler.GetSnapshotAsync()).LastError is not null && starts == 1,
                "initial controlled failure was not persisted");
            phase = "completed";
            await scheduler.RunNowAsync();
            await scheduler.WaitForIdleAsync();
            MapAutoScanSnapshot recovered = await scheduler.GetSnapshotAsync();
            Require(starts == 2 && recovered.LastCycle?.Outcome == "completed" &&
                recovered.LastCycle.CompletedServerIds.SequenceEqual([317]) && recovered.LastError is null,
                "successful subsequent publication did not retire stale Auto error");
            await scheduler.UpdateConfigAsync(recovered.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
