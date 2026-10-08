using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// Deterministic driver for the original 2-second monitor/run cadence (0xe5650 / 0xe69e3) on a virtual clock.
/// One step = set the clock, run the monitor tick, then (when a recovery run was already active before this
/// step) one run tick. No real time passes.
/// </summary>
internal sealed class Home009RecoveryPump
{
    internal const long TickMilliseconds = 2000;
    private readonly OverviewLifecycleService lifecycle;
    private readonly Action<long> setClock;
    private long? runDue;

    internal Home009RecoveryPump(OverviewLifecycleService lifecycle, Action<long> setClock, long startMilliseconds = 0)
    {
        this.lifecycle = lifecycle;
        this.setClock = setClock;
        Now = startMilliseconds;
    }

    /// <summary>Time of the next tick to execute.</summary>
    internal long Now { get; private set; }

    internal static bool IsActive(string state) => state is "waiting" or "repairing" or "launching"
        or "verifying" or "updating" or "maintenance";

    internal async Task StepAsync()
    {
        setClock(Now);
        bool activeBefore = IsActive(lifecycle.CurrentRecoveryStatus.State);
        await lifecycle.RunRecoveryObservationForTestAsync().ConfigureAwait(false);
        bool activeAfterMonitor = IsActive(lifecycle.CurrentRecoveryStatus.State);
        if (activeAfterMonitor && !activeBefore) runDue = Now + TickMilliseconds;     // run started by this monitor tick
        if (activeAfterMonitor && activeBefore && runDue is long due && Now >= due)
        {
            await lifecycle.RunRecoveryRunTickAsyncForPump().ConfigureAwait(false);
            runDue = Now + TickMilliseconds;
        }
        if (!IsActive(lifecycle.CurrentRecoveryStatus.State)) runDue = null;
        Now += TickMilliseconds;
    }

    /// <summary>Execute ticks at Now, Now+2000, ... up to and including <paramref name="endInclusive"/>.</summary>
    internal async Task RunUntilAsync(long endInclusive)
    {
        while (Now <= endInclusive) await StepAsync().ConfigureAwait(false);
    }

    internal async Task<bool> RunUntilAsync(Func<bool> condition, long maximumMilliseconds)
    {
        long end = Now + maximumMilliseconds;
        while (Now <= end)
        {
            await StepAsync().ConfigureAwait(false);
            if (condition()) return true;
        }
        return condition();
    }
}

internal static class Home009LifecycleTestExtensions
{
    internal static Task RunRecoveryRunTickAsyncForPump(this OverviewLifecycleService lifecycle) =>
        lifecycle.RunRecoveryRunTickForTestAsync();
}
