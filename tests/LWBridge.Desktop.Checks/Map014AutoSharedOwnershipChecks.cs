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
        await CheckArchivedRunWithoutSuccessorAllowsReturnAsync();
        await CheckPublishedRunCannotReturnOverSuccessorAsync();
        Console.WriteLine("MAP014_AUTO_SHARED_OWNER_OK");
    }

    private static async Task CheckArchivedRunCannotAuthorizeReturnAsync(bool managedFlagReflectsSuccessor)
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
                        return new MapAutoScanRuntimeStatus(318, true, null, "manual-successor", "running");
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

    private static async Task CheckArchivedRunWithoutSuccessorAllowsReturnAsync()
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
                        return new MapAutoScanRuntimeStatus(318, false, null, run, "completed");
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
            Require(stops == 1 && returns == 1 && result.LastCycle?.Outcome == "canceled" &&
                result.LastCycle.LastError is null && !result.OwnsActiveScan,
                "terminal owned run without successor incorrectly blocked requested return");
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

    private static MapAutoScanCommandService CreateAuto(
        string root,
        Func<bool> isMapScanActive,
        Func<string?, CancellationToken, Task<MapAutoScanRuntimeStatus>> readStatus,
        Func<string, CancellationToken, Task<bool>> stop,
        Func<int, CancellationToken, Task> returnServer) =>
        new(Path.Combine(root, "auto.json"),
            new MapAutoScanExecutionBoundary
            {
                IsOnline = () => true,
                IsMapScanActive = isMapScanActive,
                ReadStatusAsync = readStatus,
                StartTargetScanAsync = (server, _, _, _) =>
                {
                    Require(server == 318, "Auto targeted unexpected server");
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
