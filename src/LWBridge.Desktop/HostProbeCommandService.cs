using System.Text.Json;

namespace LWBridge.Desktop;

// IMPLEMENTATION POLICY: deterministic, isolated commands used only by the
// --host-probe verification mode. They are not recovered LWBridge commands and
// are never installed into a normal production backend.
internal sealed class HostProbeCommandService : INativeAsyncCommandService
{
    public const string SlowSyncCommand = "diagnostic_host_slow_sync";
    public const string DelayedCommand = "diagnostic_host_delayed";
    public const string LateCommand = "diagnostic_host_late";
    public const string StateCommand = "diagnostic_host_state";
    public const string ErrorCommand = "diagnostic_host_error";

    private readonly object gate = new();
    private readonly Queue<(int DelayMs, bool Fail)> configSaveBehaviors = new();
    private readonly Queue<HostProbePickerOutcome> pickerOutcomes = new();
    private readonly TaskCompletionSource lateRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int slowSyncStarted;
    private int delayedStarted;
    private int delayedActive;
    private int delayedCancelled;
    private int lateStarted;
    private int lateCompleted;
    private int configSaveStarted;
    private int configSaveCompleted;
    private int configSaveFailed;
    private int configSaveActive;
    private int configSaveMaxActive;
    private int pickerStarted;
    private int pickerCancelled;
    private int pickerInvalid;
    private int forceMissingGameRoot;

    public int SlowSyncStarted => Volatile.Read(ref slowSyncStarted);
    public int DelayedStarted => Volatile.Read(ref delayedStarted);
    public int DelayedActive => Volatile.Read(ref delayedActive);
    public int DelayedCancelled => Volatile.Read(ref delayedCancelled);
    public int LateStarted => Volatile.Read(ref lateStarted);
    public int LateCompleted => Volatile.Read(ref lateCompleted);
    public int ConfigSaveStarted => Volatile.Read(ref configSaveStarted);
    public int ConfigSaveCompleted => Volatile.Read(ref configSaveCompleted);
    public int ConfigSaveFailed => Volatile.Read(ref configSaveFailed);
    public int ConfigSaveActive => Volatile.Read(ref configSaveActive);
    public int ConfigSaveMaxActive => Volatile.Read(ref configSaveMaxActive);
    public int PickerStarted => Volatile.Read(ref pickerStarted);
    public int PickerCancelled => Volatile.Read(ref pickerCancelled);
    public int PickerInvalid => Volatile.Read(ref pickerInvalid);
    public bool ForceMissingGameRoot => Volatile.Read(ref forceMissingGameRoot) != 0;

    public bool CanHandle(string command) => command is
        SlowSyncCommand or DelayedCommand or LateCommand or StateCommand or ErrorCommand;

    public void QueueConfigSave(int delayMs = 0, bool fail = false)
    {
        lock (gate) configSaveBehaviors.Enqueue((Math.Max(0, delayMs), fail));
    }

    public void QueuePicker(HostProbePickerOutcome outcome)
    {
        lock (gate) pickerOutcomes.Enqueue(outcome);
    }

    public void SetForceMissingGameRoot(bool value) =>
        Volatile.Write(ref forceMissingGameRoot, value ? 1 : 0);

    public bool TryTakePicker(out HostProbePickerOutcome outcome)
    {
        lock (gate)
        {
            if (pickerOutcomes.Count == 0)
            {
                outcome = default;
                return false;
            }
            outcome = pickerOutcomes.Dequeue();
            return true;
        }
    }

    public void RecordPickerStarted() => Interlocked.Increment(ref pickerStarted);
    public void RecordPickerCancelled() => Interlocked.Increment(ref pickerCancelled);
    public void RecordPickerInvalid() => Interlocked.Increment(ref pickerInvalid);

    public async Task BeforeProductionCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (!string.Equals(command, "local_config_set", StringComparison.Ordinal)) return;

        (int DelayMs, bool Fail) behavior;
        lock (gate)
            behavior = configSaveBehaviors.Count > 0 ? configSaveBehaviors.Dequeue() : default;

        Interlocked.Increment(ref configSaveStarted);
        int active = Interlocked.Increment(ref configSaveActive);
        while (true)
        {
            int observed = Volatile.Read(ref configSaveMaxActive);
            if (active <= observed || Interlocked.CompareExchange(ref configSaveMaxActive, active, observed) == observed)
                break;
        }
        try
        {
            if (behavior.DelayMs > 0)
                await Task.Delay(behavior.DelayMs, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (behavior.Fail)
            {
                Interlocked.Increment(ref configSaveFailed);
                throw new BridgeCommandException(
                    "HOST_PROBE_CONFIG_SAVE_FAILED",
                    "Expected isolated preference-save failure.");
            }
            Interlocked.Increment(ref configSaveCompleted);
        }
        finally
        {
            Interlocked.Decrement(ref configSaveActive);
        }
    }

    public void ReleaseLate() => lateRelease.TrySetResult();

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case SlowSyncCommand:
                Interlocked.Increment(ref slowSyncStarted);
                Thread.Sleep(800);
                cancellationToken.ThrowIfCancellationRequested();
                return new { completed = true };
            case DelayedCommand:
                Interlocked.Increment(ref delayedStarted);
                Interlocked.Increment(ref delayedActive);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    return null;
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref delayedCancelled);
                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref delayedActive);
                }
            case LateCommand:
                Interlocked.Increment(ref lateStarted);
                await lateRelease.Task.ConfigureAwait(false);
                Interlocked.Increment(ref lateCompleted);
                return new { completed = true };
            case StateCommand:
                return Snapshot();
            case ErrorCommand:
                throw new BridgeCommandException("DIAGNOSTIC_EXPECTED", "Expected isolated host-probe error.");
            default:
                throw new InvalidOperationException("Unexpected host-probe command: " + command);
        }
    }

    public object Snapshot() => new
    {
        slowSyncStarted = SlowSyncStarted,
        delayedStarted = DelayedStarted,
        delayedActive = DelayedActive,
        delayedCancelled = DelayedCancelled,
        lateStarted = LateStarted,
        lateCompleted = LateCompleted,
        configSaveStarted = ConfigSaveStarted,
        configSaveCompleted = ConfigSaveCompleted,
        configSaveFailed = ConfigSaveFailed,
        configSaveActive = ConfigSaveActive,
        configSaveMaxActive = ConfigSaveMaxActive,
        pickerStarted = PickerStarted,
        pickerCancelled = PickerCancelled,
        pickerInvalid = PickerInvalid,
    };
}

internal enum HostProbePickerOutcome
{
    Cancel,
    Invalid,
}
