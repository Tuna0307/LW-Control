using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class DispatchPlunderWorker : IDisposable
{
    internal const long ArmLeadMilliseconds = 10_000;
    private static readonly TimeSpan TickCadence = TimeSpan.FromSeconds(1);

    private readonly MapDataStore store;
    private readonly Func<int?> getLiveServerId;
    private readonly Func<int, string, long, CancellationToken, Task<CurrentClientDispatchPlunderResult>> execute;
    private readonly Func<bool> tryEnterGameOperation;
    private readonly Action leaveGameOperation;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task? loopTask;
    private int runningPass;
    private bool disposed;

    internal DispatchPlunderWorker(
        MapDataStore store,
        Func<int?> getLiveServerId,
        Func<int, string, long, CancellationToken, Task<CurrentClientDispatchPlunderResult>> execute,
        Func<bool> tryEnterGameOperation,
        Action leaveGameOperation,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        bool startLoop = true)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.getLiveServerId = getLiveServerId ?? throw new ArgumentNullException(nameof(getLiveServerId));
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.tryEnterGameOperation = tryEnterGameOperation ?? throw new ArgumentNullException(nameof(tryEnterGameOperation));
        this.leaveGameOperation = leaveGameOperation ?? throw new ArgumentNullException(nameof(leaveGameOperation));
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this.delay = delay ?? Task.Delay;

        // IMPLEMENTATION POLICY LWB-R7-083: the verified original made stale
        // running Dispatch jobs retryable after restart. The current-v19 executor
        // cannot reconstruct whether DispatchSteal was already sent before a host
        // restart, so retrying could duplicate a real plunder. Fail closed instead.
        int recovered = store.FailStaleRunningDispatchPlunderConservatively(NowMilliseconds());
        if (recovered > 0) PublishChanged();

        if (startLoop)
            loopTask = Task.Run(RunLoopAsync);
    }

    internal event Action? Changed;

    internal Task RunOnceForTestAsync(CancellationToken cancellationToken = default) =>
        RunOnceAsync(cancellationToken);

    private async Task RunLoopAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Keep the durable owner alive; per-job faults are persisted below.
            }

            try
            {
                await delay(TickCadence, lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref runningPass, 1) != 0) return;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            long now = NowMilliseconds();

            if (store.ExpireDispatchPlunder(now) > 0)
                PublishChanged();

            // A live session is required to arm. Unlike Truck, the live server does
            // not have to equal the target server: current-v19 DispatchSteal carries
            // targetServer directly and the executor rechecks IsOpenCrossSteal().
            if (getLiveServerId() is null)
            {
                if (store.MarkDueDispatchPlunderWaitingConnection(now) > 0)
                    PublishChanged();
                return;
            }

            DispatchPlunderWorkItem? item =
                store.ReadArmableDispatchPlunder(now, ArmLeadMilliseconds);
            if (item is null) return;

            if (!TryValidateExecutionIdentity(item))
            {
                PersistStatus(
                    item,
                    "failed",
                    "DISPATCH_PLUNDER_INVALID_TARGET");
                return;
            }

            if (!tryEnterGameOperation()) return;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                now = NowMilliseconds();

                // Safety hardening over the recovered generic updater: only an
                // active scheduled/waiting row may enter running. This prevents a
                // concurrent successful cancel from being resurrected.
                if (!store.TryMarkDispatchPlunderRunning(
                        item.ServerId,
                        item.TaskUuid,
                        now))
                {
                    return;
                }
                PublishChanged();

                try
                {
                    CurrentClientDispatchPlunderResult result = await execute(
                        item.ServerId,
                        item.TaskUuid,
                        item.PlunderAt,
                        cancellationToken).ConfigureAwait(false);

                    if (!result.RequestSent)
                        throw new InvalidDataException(
                            "Dispatch executor returned a terminal result without proving one sent request.");

                    if (result.Succeeded)
                    {
                        PersistStatus(item, "succeeded", null);
                        return;
                    }

                    string error = string.IsNullOrWhiteSpace(result.ErrorCode)
                        ? "DISPATCH_PLUNDER_SERVER_REJECTED: missing server error"
                        : result.ErrorCode!;
                    PersistStatus(item, "failed", error);
                    if (IsDailyLimit(error))
                        FailRemainingAtDailyLimit();
                }
                catch (BridgeCommandException ex)
                {
                    PersistBridgeFailure(item, ex);
                }
                catch (OperationCanceledException)
                {
                    // Once running, cancellation can race the one-shot send.
                    PersistStatus(
                        item,
                        "failed",
                        "DISPATCH_PLUNDER_RESPONSE_TIMEOUT");
                    throw;
                }
                catch (Exception)
                {
                    // Unknown executor failure after entering running has unknown
                    // send state. Terminalize rather than risk an automatic retry.
                    PersistStatus(
                        item,
                        "failed",
                        "DISPATCH_PLUNDER_RESPONSE_TIMEOUT");
                }
            }
            finally
            {
                leaveGameOperation();
            }
        }
        finally
        {
            Volatile.Write(ref runningPass, 0);
        }
    }

    private void PersistBridgeFailure(
        DispatchPlunderWorkItem item,
        BridgeCommandException error)
    {
        bool requestSent = ReadDetailBool(error.Details, "requestSent") == true;
        bool ambiguous = ReadDetailBool(error.Details, "ambiguous") == true;
        string normalized = NormalizeError(error);

        if (IsDailyLimit(normalized))
        {
            PersistStatus(item, "failed", "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED");
            FailRemainingAtDailyLimit();
            return;
        }

        bool provenPreSend =
            !requestSent &&
            !ambiguous &&
            (normalized == "DISPATCH_PLUNDER_GAME_DISCONNECTED" ||
             normalized == "DISPATCH_PLUNDER_RESPONSE_TIMEOUT");

        if (provenPreSend && getLiveServerId() is null)
        {
            PersistStatus(item, "waiting_connection", normalized);
            return;
        }

        if (requestSent || ambiguous ||
            error.Code == "DISPATCH_PLUNDER_STATE_UNKNOWN")
        {
            PersistStatus(
                item,
                "failed",
                "DISPATCH_PLUNDER_RESPONSE_TIMEOUT");
            return;
        }

        PersistStatus(item, "failed", normalized);
    }

    private static string NormalizeError(BridgeCommandException error)
    {
        if (error.Code.StartsWith("DISPATCH_PLUNDER_", StringComparison.Ordinal))
        {
            if (error.Code == "DISPATCH_PLUNDER_SERVER_REJECTED" &&
                !string.IsNullOrWhiteSpace(error.Message))
            {
                return "DISPATCH_PLUNDER_SERVER_REJECTED: " + error.Message.Trim();
            }
            return error.Code;
        }

        return CurrentClientMapBlockSource.NormalizeDispatchPlunderError(
            string.IsNullOrWhiteSpace(error.Message) ? error.Code : error.Message);
    }

    private static bool? ReadDetailBool(object? details, string name)
    {
        if (details is null) return null;
        try
        {
            JsonElement root = JsonSerializer.SerializeToElement(details, JsonOptions.Default);
            if (!root.TryGetProperty(name, out JsonElement value) ||
                value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }
            return value.GetBoolean();
        }
        catch
        {
            return null;
        }
    }

    private void FailRemainingAtDailyLimit()
    {
        if (store.FailActiveDispatchPlunderAtDailyLimit(NowMilliseconds()) > 0)
            PublishChanged();
    }

    private void PersistStatus(
        DispatchPlunderWorkItem item,
        string status,
        string? error)
    {
        if (store.UpdateDispatchPlunderStatus(
                item.ServerId,
                item.TaskUuid,
                status,
                error,
                incrementAttempts: false,
                updatedAt: NowMilliseconds()))
        {
            PublishChanged();
        }
    }

    private static bool IsDailyLimit(string error) =>
        error == "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED";

    private static bool TryValidateExecutionIdentity(
        DispatchPlunderWorkItem item)
    {
        if (!TryParseExactPositiveInt64(item.TaskUuid, out _))
            return false;
        if (item.PlunderAt <= 0 || item.CompletionTime <= 0 ||
            item.PlunderAt < item.CompletionTime)
        {
            return false;
        }

        JsonElement task = item.Task;
        if (task.ValueKind != JsonValueKind.Object ||
            !task.TryGetProperty("uuid", out JsonElement uuid) ||
            uuid.ValueKind != JsonValueKind.String ||
            !string.Equals(uuid.GetString(), item.TaskUuid, StringComparison.Ordinal))
        {
            return false;
        }

        if (task.TryGetProperty("serverId", out JsonElement server) &&
            (!server.TryGetInt32(out int rowServer) || rowServer != item.ServerId))
        {
            return false;
        }

        if (task.TryGetProperty("completionTime", out JsonElement completion) &&
            (!TryReadInt64(completion, out long rowCompletion) ||
             rowCompletion != item.CompletionTime))
        {
            return false;
        }

        if (task.TryGetProperty("plunderAt", out JsonElement plunder) &&
            (!TryReadInt64(plunder, out long rowPlunder) ||
             rowPlunder != item.PlunderAt))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadInt64(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetInt64(out value);
        if (element.ValueKind == JsonValueKind.String)
            return long.TryParse(
                element.GetString(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
        value = 0;
        return false;
    }

    private static bool TryParseExactPositiveInt64(string text, out long value) =>
        long.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value) &&
        value > 0 &&
        value.ToString(CultureInfo.InvariantCulture) == text;

    private long NowMilliseconds() => utcNow().ToUnixTimeMilliseconds();

    private void PublishChanged()
    {
        try { Changed?.Invoke(); }
        catch { }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        if (loopTask is not null)
        {
            try { loopTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch { }
        }
        lifetime.Dispose();
    }
}
