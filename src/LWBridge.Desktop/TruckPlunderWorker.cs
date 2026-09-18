using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class TruckPlunderWorker : IDisposable
{
    // RECOVERED original armable-row call argument: 0x2710 = 10,000 ms.
    internal const long ArmLeadMilliseconds = 10_000;

    // IMPLEMENTATION POLICY: original tick cadence is not pinned. Eligibility is
    // governed by the recovered 10-second lead; one second only drives observation.
    private static readonly TimeSpan TickCadence = TimeSpan.FromSeconds(1);

    private const string UnknownAfterRestart =
        "truck plunder execution state is unknown after client restart";
    private const string UnknownAfterExecution =
        "truck plunder execution state is unknown";

    private readonly MapDataStore store;
    private readonly Func<int?> getLiveServerId;
    private readonly Func<int, long, long, CancellationToken, Task<CurrentClientTruckQuickRobResult>> execute;
    private readonly Func<bool> tryEnterGameOperation;
    private readonly Action leaveGameOperation;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task? loopTask;
    private int runningPass;
    private bool disposed;

    internal TruckPlunderWorker(
        MapDataStore store,
        Func<int?> getLiveServerId,
        Func<int, long, long, CancellationToken, Task<CurrentClientTruckQuickRobResult>> execute,
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

        int recovered = store.FailStaleRunningTruckPlunderConservatively(NowMilliseconds());
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
                // Keep the durable worker alive. Per-job execution failures are
                // persisted inside RunOnceAsync; outer faults are retried next tick.
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

            if (store.ExpireTruckPlunder(now) > 0)
                PublishChanged();

            int? liveServerId = getLiveServerId();
            if (liveServerId is null)
            {
                if (store.MarkDueTruckPlunderWaitingConnection(now, ArmLeadMilliseconds) > 0)
                    PublishChanged();
                return;
            }

            TruckPlunderWorkItem? item = store.ReadArmableTruckPlunder(now, ArmLeadMilliseconds);
            if (item is null) return;

            if (!TryReadExecutionIdentity(item, out long marchUuid, out long trainUuid, out string? jobId))
            {
                if (store.UpdateTruckPlunderStatus(
                        item.ServerId,
                        item.TrainUuid,
                        "failed",
                        "invalid scheduled target",
                        incrementAttempts: false,
                        updatedAt: now))
                {
                    PublishChanged();
                }
                return;
            }

            // The recovered original injected armMapPlunder may have had additional
            // travel behavior, but current-v19 execution is only source-proven on the
            // current server. Never jump or attack a different server implicitly.
            if (liveServerId.Value != item.ServerId) return;
            if (!tryEnterGameOperation()) return;

            bool markedRunning = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                now = NowMilliseconds();
                markedRunning = store.TryMarkTruckPlunderRunning(
                    item.ServerId,
                    item.TrainUuid,
                    jobId!,
                    now);
                if (!markedRunning) return;
                PublishChanged();

                try
                {
                    CurrentClientTruckQuickRobResult result = await execute(
                        item.ServerId,
                        marchUuid,
                        trainUuid,
                        cancellationToken).ConfigureAwait(false);

                    bool persisted = store.RecordTruckPlunderSuccess(
                        item.ServerId,
                        item.TrainUuid,
                        result.BattleWon,
                        result.PlunderRewards,
                        result.RewardNormalizationComplete,
                        updatedAt: NowMilliseconds(),
                        dailyRobCount: result.DailyRobCount);
                    if (!persisted)
                        throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled truck job is missing");
                    PublishChanged();
                }
                catch (BridgeCommandException ex)
                {
                    PersistExecutionFailure(item, ex.Code, ex.Message);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation may race train.attack. Once the row is running we
                    // cannot prove send state, so terminalize unknown and never retry.
                    PersistTerminalFailure(item, UnknownAfterExecution);
                    throw;
                }
                catch (Exception)
                {
                    // Unknown executor exceptions after entering running are likewise
                    // non-retryable because request-send state cannot be reconstructed.
                    PersistTerminalFailure(item, UnknownAfterExecution);
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

    private void PersistExecutionFailure(
        TruckPlunderWorkItem item,
        string code,
        string message)
    {
        string error = code switch
        {
            "TRUCK_PLUNDER_RESPONSE_TIMEOUT" => "server response timeout",
            "TRUCK_PLUNDER_RESULT_AMBIGUOUS" => UnknownAfterExecution,
            "TRUCK_PLUNDER_STATE_UNKNOWN" => UnknownAfterExecution,
            "TRUCK_PLUNDER_SERVER_REJECTED" => message,
            _ => message,
        };
        PersistTerminalFailure(item, error);
    }

    private void PersistTerminalFailure(TruckPlunderWorkItem item, string error)
    {
        if (store.UpdateTruckPlunderStatus(
                item.ServerId,
                item.TrainUuid,
                "failed",
                error,
                incrementAttempts: false,
                updatedAt: NowMilliseconds()))
        {
            PublishChanged();
        }
    }

    private static bool TryReadExecutionIdentity(
        TruckPlunderWorkItem item,
        out long marchUuid,
        out long trainUuid,
        out string? jobId)
    {
        marchUuid = 0;
        trainUuid = 0;
        jobId = null;
        if (!TryReadPositiveInt64(item.TrainUuid, out marchUuid))
            return false;

        JsonElement truck = item.Truck;
        if (truck.ValueKind != JsonValueKind.Object)
            return false;
        if (!TryReadPositiveInt64Property(truck, "trainUuid", out trainUuid))
            return false;
        if (!truck.TryGetProperty("jobId", out JsonElement jobValue) ||
            jobValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(jobId = jobValue.GetString()))
            return false;

        if (truck.TryGetProperty("uuid", out JsonElement uuidValue) &&
            !TryReadPositiveInt64(uuidValue, out long rowUuid))
            return false;
        if (truck.TryGetProperty("uuid", out uuidValue) &&
            TryReadPositiveInt64(uuidValue, out long exactRowUuid) &&
            exactRowUuid != marchUuid)
            return false;

        if (truck.TryGetProperty("serverId", out JsonElement serverValue))
        {
            if (!serverValue.TryGetInt32(out int rowServerId) || rowServerId != item.ServerId)
                return false;
        }
        return true;
    }

    private static bool TryReadPositiveInt64Property(
        JsonElement row,
        string name,
        out long value)
    {
        if (!row.TryGetProperty(name, out JsonElement element))
        {
            value = 0;
            return false;
        }
        return TryReadPositiveInt64(element, out value);
    }

    private static bool TryReadPositiveInt64(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value))
            return value > 0;
        if (element.ValueKind == JsonValueKind.String)
            return TryReadPositiveInt64(element.GetString(), out value);
        value = 0;
        return false;
    }

    private static bool TryReadPositiveInt64(string? text, out long value) =>
        long.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value) && value > 0;

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
