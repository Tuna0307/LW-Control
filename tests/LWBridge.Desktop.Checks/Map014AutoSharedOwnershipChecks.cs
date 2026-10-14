using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Exercises the real Auto cycle against controlled scan/status/return boundaries.
// A status lookup for a retired run can return its archived terminal row even
// while the current scan belongs to a different process/owner.
internal static class Map014AutoSharedOwnershipChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP014_AUTO_SHARED_OWNER: " + message);
    }

    internal static async Task RunAsync()
    {
        await CheckArchivedRunCannotAuthorizeReturnAsync(managedFlagReflectsSuccessor: true);
        await CheckArchivedRunCannotAuthorizeReturnAsync(managedFlagReflectsSuccessor: false);
        await CheckArchivedRunCannotAuthorizeReturnAsync(
            managedFlagReflectsSuccessor: false, successorFinished: true);
        await CheckArchivedRunWithoutSuccessorAllowsReturnAsync();
        await CheckArchivedRunWithoutSuccessorAllowsReturnAsync(exactRunMissing: true);
        await CheckPublishedRunCannotReturnOverSuccessorAsync();
        await CheckIdleServerSwitchCannotReturnAsync(319);
        await CheckIdleServerSwitchCannotReturnAsync(0);
        await CheckIdleServerSwitchAfterCanceledOwnedRunAsync();
        await CheckRetirementDuringHeldReturnOwnershipReadAsync();
        await CheckTwoRemoteTargetsCancelOnlyLatestOwnedRunAsync();
        await CheckPersistedEnabledDeadlineSurvivesStaleConfigAsync();
        await CheckCanceledRemoteStartWithoutRunTokenAsync(independentSwitch: true);
        await CheckCanceledRemoteStartWithoutRunTokenAsync(independentSwitch: false);
        await CheckSecondRemoteCanceledStartRejectsPreviousTargetAsync(secondJumpCompleted: true);
        await CheckSecondRemoteCanceledStartRejectsPreviousTargetAsync(secondJumpCompleted: false);
        await CheckIdleIndependentCompletedRunCannotAuthorizeReturnAsync(manualServer: 318);
        await CheckIdleIndependentCompletedRunCannotAuthorizeReturnAsync(manualServer: 317);
        Console.WriteLine("MAP014_AUTO_SHARED_OWNER_OK");
    }

    private static async Task CheckArchivedRunCannotAuthorizeReturnAsync(
        bool managedFlagReflectsSuccessor, bool successorFinished = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-owned-" + Guid.NewGuid().ToString("N"));
        var enteredPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int successorActive = 0, globals = 0, stops = 0, returns = 0;
        try
        {
            await using var auto = CreateAuto(root,
                () => managedFlagReflectsSuccessor && Volatile.Read(ref successorActive) != 0,
                async (run, token) =>
                {
                    if (run is null)
                    {
                        if (Interlocked.Increment(ref globals) == 1)
                            return new MapAutoScanRuntimeStatus(317, false, null, null, "idle");
                        return new MapAutoScanRuntimeStatus(318, !successorFinished, null,
                            "manual-successor", successorFinished ? "completed" : "running");
                    }
                    Require(run == "auto-owned", "looked up unrelated owned history");
                    if (!token.CanBeCanceled)
                        return new MapAutoScanRuntimeStatus(318, false, null, run, "completed");
                    enteredPoll.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    throw new InvalidOperationException("held status must cancel");
                },
                (run, _) =>
                {
                    Require(run == "auto-owned", "Stop targeted successor");
                    Interlocked.Increment(ref stops);
                    return Task.FromResult(false); // successor replaced exact scan; owned history is terminal
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref returns);
                    throw new InvalidOperationException("return tried to navigate over active successor");
                });
            await auto.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [318], ["city"], "fast", true, 0));
            await enteredPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Write(ref successorActive, 1);
            await auto.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(globals >= 2 && stops == 1 && returns == 0 &&
                result.LastCycle?.Outcome == "failed" &&
                result.LastError?.Contains("MAP_AUTO_SCAN_STOP_OWNER_MISMATCH", StringComparison.Ordinal) == true &&
                result.LastCycle.CompletedServerIds.Count == 0 && !result.Running && !result.OwnsActiveScan,
                "archived terminal run permitted return over live successor, or retired the successor");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task CheckArchivedRunWithoutSuccessorAllowsReturnAsync(bool exactRunMissing = false)
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-no-successor-" + Guid.NewGuid().ToString("N"));
        var enteredPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int stops = 0, returns = 0;
        try
        {
            await using var auto = CreateAuto(root, () => false,
                async (run, token) =>
                {
                    if (run is null) return new MapAutoScanRuntimeStatus(317, false, null, null, "idle");
                    if (!token.CanBeCanceled)
                        // Map317.ReadAutoScanStatusAsync(exactRunId) returns the
                        // requested ID with IsReading=false even when SQLite
                        // has NO such run (Phase="missing", ServerId=0).
                        return new MapAutoScanRuntimeStatus(
                            exactRunMissing ? 0 : 318, false,
                            exactRunMissing ? "Auto scan owner run is unavailable" : null,
                            run, exactRunMissing ? "missing" : "completed");
                    enteredPoll.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    throw new InvalidOperationException("held status must cancel");
                },
                (run, _) =>
                {
                    Require(run == "auto-owned", "unexpected Stop owner");
                    Interlocked.Increment(ref stops);
                    return Task.FromResult(false);
                },
                (server, _) =>
                {
                    Require(server == 317, "returned to a server other than original");
                    Interlocked.Increment(ref returns);
                    return Task.CompletedTask;
                });
            await auto.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [318], ["resource"], "fast", true, 0));
            await enteredPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await auto.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(stops == 1 && returns == (exactRunMissing ? 0 : 1) &&
                result.LastCycle?.Outcome == (exactRunMissing ? "failed" : "canceled") &&
                (exactRunMissing
                    ? result.LastError?.Contains("MAP_AUTO_SCAN_STOP_OWNER_MISMATCH", StringComparison.Ordinal) == true
                    : result.LastCycle.LastError is null) &&
                !result.OwnsActiveScan,
                "no-op Stop returned home without terminal ownership proof, or blocked a known terminal run");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task CheckPublishedRunCannotReturnOverSuccessorAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-complete-successor-" + Guid.NewGuid().ToString("N"));
        int successorActive = 0, returns = 0, stops = 0;
        try
        {
            await using var auto = CreateAuto(root,
                () => Volatile.Read(ref successorActive) != 0,
                (run, _) =>
                {
                    if (run is null)
                        return Task.FromResult(new MapAutoScanRuntimeStatus(
                            Volatile.Read(ref successorActive) != 0 ? 318 : 317,
                            Volatile.Read(ref successorActive) != 0, null,
                            Volatile.Read(ref successorActive) != 0 ? "manual-successor" : null,
                            Volatile.Read(ref successorActive) != 0 ? "running" : "idle"));
                    Require(run == "auto-owned", "unexpected run poll");
                    Volatile.Write(ref successorActive, 1); // manual scan starts immediately after owned publication
                    return Task.FromResult(new MapAutoScanRuntimeStatus(318, false, null, run, "completed"));
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref stops);
                    return Task.FromResult(true);
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref returns);
                    throw new InvalidOperationException("return over active successor");
                });
            await auto.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [318], ["city"], "fast", true, 0));
            await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(stops == 0 && returns == 0 &&
                result.LastCycle?.Outcome == "failed" &&
                result.LastError?.Contains("MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH", StringComparison.Ordinal) == true &&
                result.LastCycle.CompletedServerIds.SequenceEqual([318]) && !result.OwnsActiveScan,
                "completed Auto scan attempted navigation after another run acquired the process");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // The production ReadAutoScanStatusAsync(null) exposes the live server even
    // after the scan is idle. A user navigation can leave no active scan but
    // still make a previously scheduled Auto return belong to the old server.
    private static async Task CheckIdleServerSwitchCannotReturnAsync(int observedLiveServer)
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-idle-switch-" + Guid.NewGuid().ToString("N"));
        int currentServer = 317, starts = 0, stops = 0, returns = 0;
        try
        {
            await using var auto = CreateAuto(root, () => false,
                (run, _) =>
                {
                    if (run is null) return Task.FromResult(new MapAutoScanRuntimeStatus(
                        Volatile.Read(ref currentServer), false, null, null, "idle"));
                    Require(run == "auto-owned", "idle-server switch read wrong run");
                    Volatile.Write(ref currentServer, observedLiveServer); // new server or lost live server after publication
                    return Task.FromResult(new MapAutoScanRuntimeStatus(318, false, null, run, "completed"));
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref stops);
                    return Task.FromResult(true);
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref returns);
                    return Task.CompletedTask;
                },
                startTarget: server =>
                {
                    Interlocked.Increment(ref starts);
                    Volatile.Write(ref currentServer, server);
                });
            await auto.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [318], ["resource"], "fast", true, 0));
            await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(starts == 1 && stops == 0 && returns == 0 &&
                Volatile.Read(ref currentServer) == observedLiveServer &&
                result.LastCycle?.Outcome == "failed" &&
                result.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                result.LastError?.Contains("MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH", StringComparison.Ordinal) == true,
                "changed or unavailable live server was overwritten by old Auto return");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task CheckIdleServerSwitchAfterCanceledOwnedRunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-idle-cancel-" + Guid.NewGuid().ToString("N"));
        var enteredPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int currentServer = 317, stops = 0, returns = 0;
        try
        {
            await using var auto = CreateAuto(root, () => false,
                async (run, token) =>
                {
                    if (run is null) return new MapAutoScanRuntimeStatus(
                        Volatile.Read(ref currentServer), false, null, null, "idle");
                    Require(run == "auto-owned", "canceled exact run lookup changed owner");
                    if (!token.CanBeCanceled)
                        return new MapAutoScanRuntimeStatus(318, false, null, run, "completed");
                    enteredPoll.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    throw new InvalidOperationException("held poll must cancel");
                },
                (run, _) =>
                {
                    Require(run == "auto-owned", "Stop targeted manual successor");
                    Interlocked.Increment(ref stops);
                    return Task.FromResult(false); // native stop observed old run terminal
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref returns);
                    return Task.CompletedTask;
                },
                startTarget: server => Volatile.Write(ref currentServer, server));
            await auto.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [318], ["treasure"], "fast", true, 0));
            await enteredPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Write(ref currentServer, 319);
            await auto.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(stops == 1 && returns == 0 && Volatile.Read(ref currentServer) == 319 &&
                result.LastCycle?.Outcome == "failed" &&
                result.LastCycle.CompletedServerIds.Count == 0 &&
                result.LastError?.Contains("MAP_AUTO_SCAN_STOP_OWNER_MISMATCH", StringComparison.Ordinal) == true,
                "canceled archived run redirected idle manually selected server");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task CheckRetirementDuringHeldReturnOwnershipReadAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-retire-return-" + Guid.NewGuid().ToString("N"));
        var returnReadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReturnRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int globalReads = 0, returns = 0, stops = 0;
        try
        {
            await using var auto = CreateAuto(root, () => false,
                async (run, _) =>
                {
                    if (run is null)
                    {
                        if (Interlocked.Increment(ref globalReads) == 1)
                            return new MapAutoScanRuntimeStatus(317, false, null, null, "idle");
                        returnReadEntered.TrySetResult();
                        await releaseReturnRead.Task.ConfigureAwait(false);
                        return new MapAutoScanRuntimeStatus(318, false, null, null, "idle");
                    }
                    return new MapAutoScanRuntimeStatus(318, false, null, run, "completed");
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref stops);
                    return Task.FromResult(true);
                },
                (_, _) =>
                {
                    Interlocked.Increment(ref returns);
                    return Task.CompletedTask;
                });
            await auto.UpdateConfigAsync(new MapAutoScanConfig(true, 20, [318], ["city"], "fast", true, 0));
            await returnReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task retiring = auto.RetireAsync();
            try
            {
                Require(!retiring.IsCompleted, "retire should await held final read");
                releaseReturnRead.TrySetResult();
                await retiring.WaitAsync(TimeSpan.FromSeconds(5));
                MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
                Require(stops == 0 && returns == 0 &&
                    result.LastCycle?.Outcome == "retired" &&
                    result.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                    result.Config.Enabled && result.Config.NextRunAt == 1_800_000_000_000,
                    "retired old profile executed a return or modified enabled schedule");
            }
            finally { releaseReturnRead.TrySetResult(); }
        }
        finally
        {
            releaseReturnRead.TrySetResult();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task CheckTwoRemoteTargetsCancelOnlyLatestOwnedRunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-two-remote-" + Guid.NewGuid().ToString("N"));
        var secondPolling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = new List<int>();
        var stops = new List<string>();
        int currentServer = 317, returns = 0, stopSettled = 0;
        try
        {
            await using var auto = new MapAutoScanCommandService(Path.Combine(root, "auto.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = async (run, token) =>
                    {
                        if (run is null)
                            return new MapAutoScanRuntimeStatus(Volatile.Read(ref currentServer), false, null, null, "idle");
                        if (run == "remote-318")
                            return new MapAutoScanRuntimeStatus(318, false, null, run, "completed");
                        Require(run == "remote-319", "second target polled archived first target");
                        secondPolling.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        throw new InvalidOperationException("second target must cancel");
                    },
                    StartTargetScanAsync = (server, types, mode, _) =>
                    {
                        Require((server == 318 || server == 319) && types.SequenceEqual(["city", "resource"]) &&
                            mode == "fast", "unexpected remote target settings");
                        starts.Add(server);
                        Volatile.Write(ref currentServer, server);
                        return Task.FromResult("remote-" + server);
                    },
                    StopScanIfOwnedAsync = async (run, _) =>
                    {
                        stops.Add(run);
                        stopEntered.TrySetResult();
                        await releaseStop.Task;
                        Volatile.Write(ref stopSettled, 1);
                        return true;
                    },
                    ReturnServerAsync = (server, _) =>
                    {
                        Require(server == 317 && Volatile.Read(ref stopSettled) == 1,
                            "old cycle return ran while exact second-run Stop still held");
                        Interlocked.Increment(ref returns);
                        Volatile.Write(ref currentServer, server);
                        return Task.CompletedTask;
                    },
                }, new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            await auto.UpdateConfigAsync(new MapAutoScanConfig(
                true, 25, [318, 319], ["city", "resource"], "fast", true, 0));
            await secondPolling.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task cancel = auto.CancelAsync();
            await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                Require(!cancel.IsCompleted && returns == 0,
                    "return escaped held exact second-target Stop");
            }
            finally { releaseStop.TrySetResult(); }
            await cancel.WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(starts.SequenceEqual([318, 319]) && stops.SequenceEqual(["remote-319"]) &&
                returns == 1 && Volatile.Read(ref currentServer) == 317 &&
                result.LastCycle?.Outcome == "canceled" &&
                result.LastCycle.TargetServerIds.SequenceEqual([318, 319]) &&
                result.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                result.Config.NextRunAt == 1_800_001_500_000 &&
                !result.Running && !result.OwnsActiveScan,
                "second remote cancel lost completed first target or targeted stale Stop/return/deadline");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            releaseStop.TrySetResult();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task CheckPersistedEnabledDeadlineSurvivesStaleConfigAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-deadline-" + Guid.NewGuid().ToString("N"));
        string statePath = Path.Combine(root, "auto.json");
        long now = 1_800_000_000_000;
        int server = 317, starts = 0, returns = 0;
        try
        {
            await using (var auto = new MapAutoScanCommandService(statePath,
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) => Task.FromResult(run is null
                        ? new MapAutoScanRuntimeStatus(server, false, null, null, "idle")
                        : new MapAutoScanRuntimeStatus(318, false, null, run, "completed")),
                    StartTargetScanAsync = (target, _, _, _) =>
                    {
                        Require(target == 318, "deadline cycle target changed");
                        starts++;
                        server = target;
                        return Task.FromResult("deadline-run");
                    },
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("published run must not Stop"),
                    ReturnServerAsync = (target, _) =>
                    {
                        Require(target == 317, "deadline cycle returned to wrong origin");
                        returns++;
                        server = target;
                        return Task.CompletedTask;
                    },
                }, new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => Volatile.Read(ref now),
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false))
            {
                MapAutoScanSnapshot before = await auto.GetSnapshotAsync();
                Require(!before.Config.Enabled && before.Config.NextRunAt == 0,
                    "new temporary Auto owner unexpectedly enabled by default");
                await auto.UpdateConfigAsync(new MapAutoScanConfig(
                    true, 27, [318], ["city"], "fast", true, 0));
                await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
                MapAutoScanSnapshot after = await auto.GetSnapshotAsync();
                long deadline = now + 27 * 60_000L;
                Require(starts == 1 && returns == 1 && after.LastCycle?.Outcome == "completed" &&
                    after.Config.Enabled && after.Config.NextRunAt == deadline,
                    "successful remote Auto did not persist its enabled deadline");
                // Simulate stale browser payload: it carries nextRunAt=0 from
                // before the completed cycle and edits the selected types only.
                MapAutoScanSnapshot staleUpdate = await auto.UpdateConfigAsync(after.Config with
                {
                    NextRunAt = 0,
                    SelectedTypes = ["resource"],
                });
                Require(staleUpdate.Config.NextRunAt == deadline && starts == 1 &&
                    staleUpdate.Config.SelectedTypes?.SequenceEqual(["resource"]) == true,
                    "stale config reset owner deadline or launched duplicate Auto cycle");
                Volatile.Write(ref now, deadline - 1);
                Require(!await auto.CheckDueAsync() && starts == 1,
                    "Auto admitted a second cycle before persisted deadline");
            }
            await using var reopened = new MapAutoScanCommandService(statePath,
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => false,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (_, _) => throw new InvalidOperationException("reopen must not contact game"),
                    StartTargetScanAsync = (_, _, _, _) => throw new InvalidOperationException("reopen must not scan"),
                    ReturnServerAsync = (_, _) => throw new InvalidOperationException("reopen must not navigate"),
                    StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("reopen must not Stop"),
                }, startScheduler: false);
            MapAutoScanSnapshot persisted = await reopened.GetSnapshotAsync();
            Require(persisted.Config.Enabled && persisted.Config.NextRunAt == 1_800_001_620_000 &&
                persisted.Config.SelectedTypes?.SequenceEqual(["resource"]) == true &&
                persisted.LastCycle?.Outcome == "completed" &&
                persisted.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                starts == 1 && returns == 1,
                "reopened enabled Auto config lost fresh deadline or published target");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // Map317's Auto target start jumps to its server before it starts the
    // durable native scan and returns its run ID. Cancel can arrive between
    // those boundaries. A manual server switch after that jump must not be
    // undone by this old Auto cycle merely because no run ID was obtained.
    private static async Task CheckCanceledRemoteStartWithoutRunTokenAsync(bool independentSwitch)
    {
        string root = Path.Combine(Path.GetTempPath(), "map014-auto-start-cancel-" + Guid.NewGuid().ToString("N"));
        var jumped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int currentServer = 317, starts = 0, stops = 0, returns = 0;
        try
        {
            await using var auto = new MapAutoScanCommandService(Path.Combine(root, "auto.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) =>
                    {
                        Require(run is null, "Auto received a run ID from canceled pre-start");
                        return Task.FromResult(new MapAutoScanRuntimeStatus(
                            Volatile.Read(ref currentServer), false, null, null, "idle"));
                    },
                    StartTargetScanAsync = async (server, kinds, mode, token) =>
                    {
                        Require(server == 318 && kinds.SequenceEqual(["city"]) && mode == "fast",
                            "unexpected remote Start target");
                        Interlocked.Increment(ref starts);
                        Volatile.Write(ref currentServer, server); // Map317 ServerJumpAsync completed
                        jumped.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        throw new InvalidOperationException("native scan must not start after cancellation");
                    },
                    StopScanIfOwnedAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref stops);
                        throw new InvalidOperationException("pre-start cancellation has no owned scan to Stop");
                    },
                    ReturnServerAsync = (server, _) =>
                    {
                        Require(server == 317, "return must address the original server");
                        Interlocked.Increment(ref returns);
                        Volatile.Write(ref currentServer, server);
                        return Task.CompletedTask;
                    },
                }, new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);

            await auto.UpdateConfigAsync(new MapAutoScanConfig(
                true, 20, [318], ["city"], "fast", true, 0));
            await jumped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (independentSwitch)
                Volatile.Write(ref currentServer, 319); // another owner changes live server while no run exists
            await auto.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(starts == 1 && stops == 0 &&
                returns == (independentSwitch ? 0 : 1) &&
                Volatile.Read(ref currentServer) == (independentSwitch ? 319 : 317) &&
                result.LastCycle?.Outcome == (independentSwitch ? "failed" : "canceled") &&
                (independentSwitch
                    ? result.LastError?.Contains("MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH", StringComparison.Ordinal) == true
                    : result.LastCycle.LastError is null) &&
                result.LastCycle.CompletedServerIds.Count == 0 &&
                !result.Running && !result.OwnsActiveScan,
                "canceled pre-start remote jump lost return ownership or normal home restoration");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // Auto completed and published 318, then attempted 319. If the second
    // start is canceled before returning a run ID, idle 318 is NOT safe return
    // provenance: the user may have manually jumped from 319 back to 318.
    // If 319's jump failed before navigation, the same 318 observation is
    // indistinguishable; fail closed rather than overwrite independent intent.
    private static async Task CheckSecondRemoteCanceledStartRejectsPreviousTargetAsync(
        bool secondJumpCompleted)
    {
        string root = Path.Combine(Path.GetTempPath(),
            "map014-auto-previous-target-" + Guid.NewGuid().ToString("N"));
        var secondStartEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int liveServer = 317, firstStarts = 0, secondStarts = 0, stops = 0, returns = 0;
        try
        {
            await using var auto = new MapAutoScanCommandService(Path.Combine(root, "auto.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) =>
                    {
                        if (run is null)
                            return Task.FromResult(new MapAutoScanRuntimeStatus(
                                Volatile.Read(ref liveServer), false, null, null, "idle"));
                        Require(run == "first-318", "canceled second start fabricated a scan-run ID");
                        return Task.FromResult(new MapAutoScanRuntimeStatus(
                            318, false, null, run, "completed"));
                    },
                    StartTargetScanAsync = async (server, types, mode, token) =>
                    {
                        Require(types.SequenceEqual(["city", "resource"]) && mode == "fast",
                            "unexpected multi-target Auto scan settings");
                        if (server == 318)
                        {
                            Interlocked.Increment(ref firstStarts);
                            Volatile.Write(ref liveServer, 318);
                            return "first-318";
                        }
                        Require(server == 319, "unexpected second target");
                        Interlocked.Increment(ref secondStarts);
                        if (secondJumpCompleted)
                            Volatile.Write(ref liveServer, 319);
                        secondStartEntered.TrySetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        throw new InvalidOperationException("second target must cancel before run ID");
                    },
                    StopScanIfOwnedAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref stops);
                        throw new InvalidOperationException("completed first run must not be stopped by second start");
                    },
                    ReturnServerAsync = (server, _) =>
                    {
                        Require(server == 317, "old Auto attempted non-original return");
                        Interlocked.Increment(ref returns);
                        Volatile.Write(ref liveServer, server);
                        return Task.CompletedTask;
                    },
                }, new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);

            await auto.UpdateConfigAsync(new MapAutoScanConfig(
                true, 20, [318, 319], ["city", "resource"], "fast", true, 0));
            await secondStartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // When the 319 jump succeeded, this is an INDEPENDENT manual jump
            // back to already-published 318. If 319 never jumped, no extra
            // navigation occurs and the observation is indistinguishable.
            if (secondJumpCompleted)
                Volatile.Write(ref liveServer, 318);
            await auto.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(firstStarts == 1 && secondStarts == 1 && stops == 0 && returns == 0 &&
                Volatile.Read(ref liveServer) == 318 &&
                result.LastCycle?.Outcome == "failed" &&
                result.LastCycle.TargetServerIds.SequenceEqual([318, 319]) &&
                result.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                result.LastError?.Contains("MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH", StringComparison.Ordinal) == true &&
                !result.OwnsActiveScan && !result.Running,
                "older accepted target 318 falsely authorized canceled 319 return");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // `ReadAutoScanStatusAsync(null)` returns the CURRENT ScanRunId even after
    // that run becomes idle. A completed manual successor on the same server
    // can pass every existing server/IsReading check; the old Auto cycle still
    // must not jump away from the successor's current camera or home.
    private static async Task CheckIdleIndependentCompletedRunCannotAuthorizeReturnAsync(int manualServer)
    {
        string root = Path.Combine(Path.GetTempPath(),
            "map014-auto-idle-manual-run-" + Guid.NewGuid().ToString("N"));
        int currentServer = 317, returns = 0, stops = 0, starts = 0;
        string? currentRun = null;
        try
        {
            await using var auto = new MapAutoScanCommandService(Path.Combine(root, "auto.json"),
                new MapAutoScanExecutionBoundary
                {
                    IsOnline = () => true,
                    IsMapScanActive = () => false,
                    ReadStatusAsync = (run, _) =>
                    {
                        if (run is not null)
                        {
                            Require(run == "owned-318", "Auto polled unrelated manual run");
                            // The owned scan completed. Before Auto gets to its
                            // return, a distinct MANUAL scan on the same server
                            // (or already home) completes and becomes idle.
                            Volatile.Write(ref currentServer, manualServer);
                            Volatile.Write(ref currentRun, "manual-completed");
                            return Task.FromResult(new MapAutoScanRuntimeStatus(
                                318, false, null, run, "completed"));
                        }
                        return Task.FromResult(new MapAutoScanRuntimeStatus(
                            Volatile.Read(ref currentServer), false, null,
                            Volatile.Read(ref currentRun), "completed"));
                    },
                    StartTargetScanAsync = (server, types, mode, _) =>
                    {
                        Require(server == 318 && types.SequenceEqual(["city"]) && mode == "fast",
                            "Auto started unexpected remote run");
                        Interlocked.Increment(ref starts);
                        Volatile.Write(ref currentServer, server);
                        Volatile.Write(ref currentRun, "owned-318");
                        return Task.FromResult("owned-318");
                    },
                    StopScanIfOwnedAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref stops);
                        return Task.FromResult(false);
                    },
                    ReturnServerAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref returns);
                        return Task.CompletedTask;
                    },
                }, new MapAutoScanSchedulerHooks
                {
                    UtcNowMilliseconds = () => 1_800_000_000_000,
                    DelayAsync = (_, _) => Task.CompletedTask,
                }, startScheduler: false);
            await auto.UpdateConfigAsync(new MapAutoScanConfig(
                true, 20, [318], ["city"], "fast", true, 0));
            await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            MapAutoScanSnapshot result = await auto.GetSnapshotAsync();
            Require(starts == 1 && stops == 0 && returns == 0 &&
                Volatile.Read(ref currentServer) == manualServer &&
                Volatile.Read(ref currentRun) == "manual-completed" &&
                result.LastCycle?.Outcome == "failed" &&
                result.LastCycle.CompletedServerIds.SequenceEqual([318]) &&
                result.LastError?.Contains("MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH", StringComparison.Ordinal) == true &&
                !result.Running && !result.OwnsActiveScan,
                "idle foreign completed run on Auto target/original server authorized old return");
            await auto.UpdateConfigAsync(result.Config with { Enabled = false });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static MapAutoScanCommandService CreateAuto(
        string root,
        Func<bool> isMapScanActive,
        Func<string?, CancellationToken, Task<MapAutoScanRuntimeStatus>> readStatus,
        Func<string, CancellationToken, Task<bool>> stop,
        Func<int, CancellationToken, Task> returnServer,
        Action<int>? startTarget = null) =>
        new(Path.Combine(root, "auto.json"),
            new MapAutoScanExecutionBoundary
            {
                IsOnline = () => true,
                IsMapScanActive = isMapScanActive,
                ReadStatusAsync = readStatus,
                StartTargetScanAsync = (server, _, _, _) =>
                {
                    Require(server == 318, "Auto targeted unexpected server");
                    startTarget?.Invoke(server);
                    return Task.FromResult("auto-owned");
                },
                StopScanIfOwnedAsync = stop,
                ReturnServerAsync = returnServer,
            },
            new MapAutoScanSchedulerHooks
            {
                UtcNowMilliseconds = () => 1_800_000_000_000,
                DelayAsync = (_, _) => Task.CompletedTask,
            }, startScheduler: false);
}
