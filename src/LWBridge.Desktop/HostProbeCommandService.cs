using System.Text.Json;

namespace LWBridge.Desktop;

// IMPLEMENTATION POLICY: deterministic, isolated commands used only by the
// --host-probe verification mode. They are not recovered LWBridge commands and
// are never installed into a normal production backend.
internal sealed class HostProbeCommandService : INativeAsyncCommandService
{
    public const string SlowSyncCommand = "diagnostic_host_slow_sync";
    public const string DelayedCommand = "diagnostic_host_delayed";
    public const string StateCommand = "diagnostic_host_state";
    public const string ErrorCommand = "diagnostic_host_error";

    private int slowSyncStarted;
    private int delayedStarted;
    private int delayedActive;
    private int delayedCancelled;

    public int SlowSyncStarted => Volatile.Read(ref slowSyncStarted);
    public int DelayedStarted => Volatile.Read(ref delayedStarted);
    public int DelayedActive => Volatile.Read(ref delayedActive);
    public int DelayedCancelled => Volatile.Read(ref delayedCancelled);

    public bool CanHandle(string command) => command is
        SlowSyncCommand or DelayedCommand or StateCommand or ErrorCommand;

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
    };
}
