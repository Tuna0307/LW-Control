using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Production scheduler/config persistence; only the native game boundary is controlled.
internal static class Map017ScanControlChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP017_SCAN_CONTROLS: " + message);
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map017-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            long now = 1_800_000_000_000;
            int currentServer = 2212, returns = 0;
            string? currentRun = null;
            var starts = new List<int>();
            string statePath = Path.Combine(root, "auto.json");
            MapAutoScanCommandService Create() => new(statePath, new MapAutoScanExecutionBoundary
            {
                IsOnline = () => true,
                IsMapScanActive = () => false,
                ReadStatusAsync = (run, _) => Task.FromResult(new MapAutoScanRuntimeStatus(
                    currentServer, false, null, run ?? currentRun, run is null ? "idle" : "completed")),
                StartTargetScanAsync = (server, kinds, mode, _) =>
                {
                    Require(kinds.SequenceEqual(["railway"]) && mode == "fast", "one-off changed its selected kinds/mode");
                    starts.Add(server);
                    currentServer = server;
                    currentRun = "map017-" + starts.Count;
                    return Task.FromResult(currentRun!);
                },
                ReturnServerAsync = (server, _) =>
                {
                    Require(server == 2212, "one-off returned to the wrong home");
                    currentServer = server;
                    returns++;
                    return Task.CompletedTask;
                },
                StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("completed one-off should not Stop"),
            }, new MapAutoScanSchedulerHooks
            {
                UtcNowMilliseconds = () => now,
                DelayAsync = (_, _) => Task.CompletedTask,
            }, startScheduler: false);

            await using (var auto = Create())
            {
                var empty = await auto.UpdateConfigAsync(new(false, 20, [2211, 2213], [], "fast", true, 0));
                Require(empty.Config.SelectedTypes?.Count == 0, "native config replaced an explicit empty choice with defaults");
                bool rejected = false;
                try { await auto.RunNowAsync(); }
                catch (BridgeCommandException error) when (error.Message.Contains("Select at least one")) { rejected = true; }
                Require(rejected && starts.Count == 0, "empty Run now started a native scan");
                await auto.UpdateConfigAsync(empty.Config with { Enabled = true });
                await auto.CheckDueAsync(); // observe first connection
                now += 30 * 60_000L;
                Require(!await auto.CheckDueAsync() && starts.Count == 0, "empty enabled schedule started a scan");
                await auto.UpdateConfigAsync(empty.Config with { SelectedTypes = ["railway"] });
                await auto.RunNowAsync();
                await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
                var result = await auto.GetSnapshotAsync();
                Require(starts.SequenceEqual([2211, 2213]) && returns == 1 && currentServer == 2212,
                    "schedule-off Run now did not complete both targets and return home");
                Require(result.LastCycle?.Outcome == "completed" && result.LastCycle.CompletedServerIds.SequenceEqual([2211, 2213]),
                    "one-off did not record both completed targets");
                Require(!result.Config.Enabled && result.Config.NextRunAt == 0, "one-off enabled a schedule or retained a deadline");
                now += 24 * 60 * 60_000L;
                Require(!await auto.CheckDueAsync() && starts.Count == 2, "one-off repeated later with schedule off");
                var enabled = await auto.UpdateConfigAsync(result.Config with { Enabled = true });
                Require(starts.Count == 2 && enabled.Config.NextRunAt == now + 20 * 60_000L,
                    "enabling after a one-off started immediately");
                await auto.UpdateConfigAsync(enabled.Config with { Enabled = false, SelectedTypes = [] });
            }
            await using (var reopened = Create())
            {
                var result = await reopened.GetSnapshotAsync();
                Require(result.Config.SelectedTypes?.Count == 0 && !result.Config.Enabled && result.Config.NextRunAt == 0,
                    "reopening lost the empty selection or disabled schedule");
                Require(!await reopened.CheckDueAsync() && starts.Count == 2, "reopening repeated a one-off cycle");
            }
            Require(MapAutoScanCommandService.NormalizeConfig(MapAutoScanConfig.Default with { SelectedTypes = null })
                .SelectedTypes!.SequenceEqual(MapAutoScanCommandService.DefaultSelectedTypes), "legacy missing kinds lost their defaults");
            await CheckDisableDuringCycleAsync(root, explicitRunNow: false);
            await CheckDisableDuringCycleAsync(root, explicitRunNow: true);
        }
        finally { Directory.Delete(root, recursive: true); }
        Console.WriteLine("MAP017_SCAN_CONTROLS_OK");
    }

    private static async Task CheckDisableDuringCycleAsync(string root, bool explicitRunNow)
    {
        long now = 1_800_000_000_000;
        int server = 2212, starts = 0, returns = 0;
        string? currentRun = null;
        var firstPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var auto = new MapAutoScanCommandService(Path.Combine(root, $"mid-cycle-{explicitRunNow}.json"),
            new MapAutoScanExecutionBoundary
            {
                IsOnline = () => true,
                IsMapScanActive = () => false,
                ReadStatusAsync = async (run, token) =>
                {
                    if (run is not null && starts == 1)
                    {
                        firstPoll.TrySetResult();
                        await releasePoll.Task.WaitAsync(token);
                    }
                    return new MapAutoScanRuntimeStatus(server, false, null, run ?? currentRun, run is null ? "idle" : "completed");
                },
                StartTargetScanAsync = (target, _, _, _) =>
                {
                    server = target;
                    currentRun = "mid-cycle-" + ++starts;
                    return Task.FromResult(currentRun!);
                },
                ReturnServerAsync = (target, _) => { server = target; returns++; return Task.CompletedTask; },
                StopScanIfOwnedAsync = (_, _) => throw new InvalidOperationException("completed target unexpectedly stopped"),
            }, new MapAutoScanSchedulerHooks
            {
                UtcNowMilliseconds = () => now,
                DelayAsync = (_, _) => Task.CompletedTask,
            }, startScheduler: false);
        await auto.UpdateConfigAsync(new(!explicitRunNow, 20, [2211, 2213], ["railway"], "fast", true, 0));
        if (explicitRunNow) await auto.RunNowAsync();
        else
        {
            now += 20 * 60_000L;
            Require(await auto.CheckDueAsync(), "scheduled interval did not admit its due cycle");
        }
        await firstPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var snapshot = await auto.GetSnapshotAsync();
            if (explicitRunNow)
                await auto.UpdateConfigAsync(snapshot.Config with { Enabled = true });
            await auto.UpdateConfigAsync(snapshot.Config with { Enabled = false });
        }
        finally { releasePoll.TrySetResult(); }
        await auto.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var result = await auto.GetSnapshotAsync();
        Require(starts == (explicitRunNow ? 2 : 1) && returns == 1 && server == 2212,
            "schedule disable changed the explicit/scheduled target boundary");
        Require(result.LastCycle?.Outcome == (explicitRunNow ? "completed" : "disabled") &&
            !result.Config.Enabled && result.Config.NextRunAt == 0,
            "schedule disable lost cycle outcome/deadline semantics");
    }
}
