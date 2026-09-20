using System.Text.Json;

namespace LWBridge.Desktop;

// LWB-R7-119 + R7-122: host-global outstanding bridge-to-Lua call/result
// collection. R7-122 closes the original process-start command-counter seed:
// store+0x180 starts at zero, the builder increments before formatting, and
// therefore the first original command id is cmd_1.
internal sealed class LWBridgeControlPipeCallRegistry : IDisposable
{
    public const ulong InitialCommandCounter = 0;
    public const int StoreMutexOffset = 0x100;
    public const int MutexToCommandCounterOffset = 0x80;
    public const int StoreCommandCounterOffset = 0x180;
    public const int StoreCounterSeedConstantLoadRva = 0x3C3926;
    public const int StoreCounterSeedWriteRva = 0x3C3AC3;
    public const int CommandCounterIncrementRva = 0x3C4BE5;
    public const int CommandIdLiteralRefRva = 0x3C4C16;
    public const string CommandIdPrefix = "cmd_";
    public const string FirstCommandId = "cmd_1";

    public const int CallTimeoutMilliseconds = 200;
    public const int CallTimeoutSeconds = 0;
    public const int CallTimeoutNanoseconds = 200_000_000;

    public const int CallTimeoutFutureRva = 0x0E537E;
    public const int CallTimeoutInitRva = 0x0E53A1;
    public const int CallTimeoutTimerCallRva = 0x0E53B4;
    public const int CallTimeoutSourceRecordRva = 0x825418;
    public const int RustNanosecondsPerSecond = 1_000_000_000;
    public const int TimerNanosecondInvariantRva = 0x63B4B2;

    public static readonly TimeSpan CallTimeout =
        TimeSpan.FromMilliseconds(CallTimeoutMilliseconds);

    private readonly object gate = new();
    private readonly Dictionary<string, PendingEntry> pending =
        new(StringComparer.Ordinal);
    private ulong commandCounter;
    private bool stopped;

    internal LWBridgeControlPipeCallRegistry(
        ulong initialCommandCounter = InitialCommandCounter)
    {
        commandCounter = initialCommandCounter;
    }

    public int PendingCount
    {
        get
        {
            lock (gate)
                return pending.Count;
        }
    }

    internal LWBridgePendingCall BeginCall(
        string profileId,
        string instanceId,
        string functionName,
        JsonElement args,
        long createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        PendingEntry entry;
        lock (gate)
        {
            if (stopped)
            {
                throw new BridgeCommandException(
                    "BRIDGE_STOPPED",
                    "The bridge call registry is stopped.");
            }

            ulong number = checked(commandCounter + 1);
            commandCounter = number;
            string id = CommandIdPrefix + number.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

            var completion =
                new TaskCompletionSource<JsonElement?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var timeout = new CancellationTokenSource();
            entry = new PendingEntry(
                id,
                profileId,
                instanceId,
                functionName,
                args.Clone(),
                createdAt,
                completion,
                timeout);
            pending.Add(id, entry);
        }

        _ = ExpireAsync(entry);

        return new LWBridgePendingCall(
            entry.Id,
            entry.ProfileId,
            entry.InstanceId,
            entry.FunctionName,
            entry.Args,
            entry.CreatedAt,
            entry.Completion.Task);
    }

    internal bool TryCompleteResult(
        string authenticatedProfileId,
        string authenticatedInstanceId,
        LWBridgeCallResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedInstanceId);
        ArgumentNullException.ThrowIfNull(result);

        PendingEntry? entry;
        lock (gate)
        {
            if (!pending.TryGetValue(result.Id, out entry))
                return false;

            // Route identity is already authenticated by the hello handshake.
            // Bind a result to the same authenticated route that owns the call;
            // outer requestId equality remains deliberately unasserted because
            // R7-097 did not recover that validation.
            if (!string.Equals(
                    entry.ProfileId,
                    authenticatedProfileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entry.InstanceId,
                    authenticatedInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            pending.Remove(result.Id);
            entry.Timeout.Cancel();
        }

        if (result.Ok)
        {
            entry.Completion.TrySetResult(
                result.Result?.Clone());
        }
        else
        {
            string message = result.Error is { Length: > 0 } error
                ? "lua call failed: " + error
                : "lua call failed";
            entry.Completion.TrySetException(
                new BridgeCommandException(
                    "LUA_CALL_FAILED",
                    message));
        }

        entry.Timeout.Dispose();
        return true;
    }

    internal bool TryFailPending(string id, Exception error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(error);

        PendingEntry? entry;
        lock (gate)
        {
            if (!pending.TryGetValue(id, out entry))
                return false;

            pending.Remove(id);
            entry.Timeout.Cancel();
        }

        entry.Completion.TrySetException(error);
        entry.Timeout.Dispose();
        return true;
    }

    internal void Stop()
    {
        PendingEntry[] drained;
        lock (gate)
        {
            if (stopped)
                return;

            stopped = true;
            drained = pending.Values.ToArray();
            pending.Clear();
        }

        foreach (PendingEntry entry in drained)
        {
            entry.Timeout.Cancel();
            entry.Completion.TrySetException(
                new BridgeCommandException(
                    "APP_SHUTTING_DOWN",
                    "application is shutting down"));
            entry.Timeout.Dispose();
        }
    }

    private async Task ExpireAsync(PendingEntry entry)
    {
        try
        {
            await Task.Delay(CallTimeout, entry.Timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        bool removed;
        lock (gate)
        {
            removed =
                pending.TryGetValue(entry.Id, out PendingEntry? current) &&
                ReferenceEquals(current, entry);
            if (removed)
                pending.Remove(entry.Id);
        }

        if (!removed)
            return;

        entry.Completion.TrySetException(
            new BridgeCommandException(
                "LUA_CALL_TIMEOUT",
                "Lua call result was not received before the recovered timeout."));
        entry.Timeout.Dispose();
    }

    public void Dispose() => Stop();

    private sealed record PendingEntry(
        string Id,
        string ProfileId,
        string InstanceId,
        string FunctionName,
        JsonElement Args,
        long CreatedAt,
        TaskCompletionSource<JsonElement?> Completion,
        CancellationTokenSource Timeout);
}

internal sealed record LWBridgePendingCall(
    string Id,
    string ProfileId,
    string InstanceId,
    string FunctionName,
    JsonElement Args,
    long CreatedAt,
    Task<JsonElement?> Completion);
