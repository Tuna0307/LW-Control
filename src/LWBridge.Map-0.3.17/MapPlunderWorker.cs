using System.Globalization;
using System.Text.Json;

namespace LWBridge.Map317;

/// <summary>
/// Exact 0.3.17 durable scheduled-plunder worker semantics. The provider owns
/// current-client mechanics; this worker owns due selection, arming, pending
/// deadlines, restart/disconnect recovery, terminal result persistence and
/// server-day pruning.
/// </summary>
public sealed class MapPlunderWorker
{
    public const int TickMilliseconds = 100;
    public const int DispatchBatchLimit = 200;
    public const long ArmLeadMilliseconds = 10_000;
    public const int ProtectedCallTimeoutMilliseconds = 5_000;
    public const long DispatchResultHorizonMilliseconds = 15_000;
    public const long TruckResultHorizonMilliseconds = 30_000;

    private sealed record DispatchPending(
        long ServerId,
        string DurableTaskUuid,
        string ProviderTaskUuid,
        string Kind,
        long Deadline);

    private sealed record TruckPending(
        long ServerId,
        string TrainUuid,
        string JobId,
        long Deadline);

    private readonly MapStore store;
    private readonly IMapActionProvider provider;
    private readonly Func<long> nowMilliseconds;
    private readonly TimeSpan protectedCallTimeout;
    private readonly Dictionary<(long ServerId, string DurableTaskUuid), DispatchPending> dispatchPending = new();
    private TruckPending? truckPending;

    public MapPlunderWorker(
        MapStore store,
        IMapActionProvider provider,
        Func<long>? nowMilliseconds = null,
        TimeSpan? protectedCallTimeout = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.nowMilliseconds = nowMilliseconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        this.protectedCallTimeout = protectedCallTimeout ?? TimeSpan.FromMilliseconds(ProtectedCallTimeoutMilliseconds);
        if (this.protectedCallTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(protectedCallTimeout));
    }

    public event EventHandler? DispatchPlunderChanged;
    public event EventHandler? TruckPlunderChanged;

    public void RecoverAfterRestart()
    {
        dispatchPending.Clear();
        truckPending = null;
        long now = nowMilliseconds();
        if (store.RecoverDispatchPlunderJobs(now) > 0)
            DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
        if (store.RecoverTruckPlunderJobs(now) > 0)
            TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Test/convenience entry point. Production owns independent 100 ms loops.</summary>
    public async ValueTask RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await RunDispatchOnceAsync(cancellationToken).ConfigureAwait(false);
        await RunTruckOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RunDispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        long now = nowMilliseconds();
        bool changed = store.ExpireDispatchPlunder(now) > 0;
        changed |= await DrainDispatchResultsAsync(now, cancellationToken).ConfigureAwait(false);

        bool connected = await IsConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (!connected)
        {
            foreach (DispatchPending pending in dispatchPending.Values.ToArray())
            {
                changed |= store.UpdateDispatchPlunderStatus(
                    pending.ServerId,
                    pending.DurableTaskUuid,
                    "waiting_connection",
                    "DISPATCH_PLUNDER_GAME_DISCONNECTED",
                    false,
                    now);
            }
            dispatchPending.Clear();
            changed |= store.MarkDueDispatchPlunderWaitingConnection(now) > 0;
            if (changed) DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        foreach (DispatchPending pending in dispatchPending.Values.Where(item => item.Deadline <= now).ToArray())
        {
            changed |= store.UpdateDispatchPlunderStatus(
                pending.ServerId,
                pending.DurableTaskUuid,
                "failed",
                "DISPATCH_PLUNDER_RESPONSE_TIMEOUT",
                false,
                now);
            dispatchPending.Remove((pending.ServerId, pending.DurableTaskUuid));
        }

        IReadOnlyList<DispatchPlunderWorkItem> jobs =
            store.ReadArmableDispatchPlunderBatch(now, ArmLeadMilliseconds, DispatchBatchLimit);
        if (jobs.Count > 0)
            changed |= await ArmDispatchBatchAsync(jobs, now, cancellationToken).ConfigureAwait(false);

        if (changed) DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask RunTruckOnceAsync(CancellationToken cancellationToken = default)
    {
        long now = nowMilliseconds();
        bool changed = store.ExpireTruckPlunder(now) > 0;
        changed |= await DrainTruckResultsAsync(now, cancellationToken).ConfigureAwait(false);

        bool connected = await IsConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (!connected)
        {
            if (truckPending is not null)
            {
                changed |= store.UpdateTruckPlunderStatus(
                    truckPending.ServerId,
                    truckPending.TrainUuid,
                    "waiting_connection",
                    "game disconnected",
                    false,
                    now);
                truckPending = null;
            }
            changed |= store.MarkDueTruckPlunderWaitingConnection(now, ArmLeadMilliseconds) > 0;
            if (changed) TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (truckPending is not null && truckPending.Deadline <= now)
        {
            TruckPending timedOut = truckPending;
            changed |= store.UpdateTruckPlunderStatus(
                timedOut.ServerId,
                timedOut.TrainUuid,
                "failed",
                "server response timeout",
                false,
                now);
            truckPending = null;
            try
            {
                await WithProtectedTimeoutAsync(
                    provider.ClearTruckPlunderPendingAsync(
                        checked((int)timedOut.ServerId), timedOut.TrainUuid, timedOut.JobId, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is BridgeCommandException or TimeoutException)
            {
                // Exact worker cleanup is best-effort; the durable timeout result wins.
            }
        }

        if (truckPending is null)
        {
            TruckPlunderWorkItem? job = store.ReadArmableTruckPlunder(now, ArmLeadMilliseconds);
            if (job is not null)
                changed |= await ArmTruckAsync(job, now, cancellationToken).ConfigureAwait(false);
        }

        if (changed) TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
    }

    private async ValueTask<bool> ArmDispatchBatchAsync(
        IReadOnlyList<DispatchPlunderWorkItem> jobs,
        long now,
        CancellationToken cancellationToken)
    {
        var armedCandidates = new List<(DispatchPlunderWorkItem Job, DispatchPlunderArmJob Request)>();
        bool changed = false;

        foreach (DispatchPlunderWorkItem job in jobs)
        {
            string kind = job.TaskUuid.StartsWith("ghost:", StringComparison.Ordinal) ? "ghost" : "dispatch";
            string providerUuid = ReadString(job.Task, "uuid");
            int? ownerServer = ReadNullableInt(job.Task, "ownerServer");
            if (!IsPositiveDecimal(providerUuid) ||
                ownerServer is null or < 1 or > 99999 ||
                job.ServerId is < 1 or > 99999)
            {
                // Corrupt persisted rows cannot be sent. The public scheduler already
                // validates these fields, so this is a fail-closed storage guard.
                changed |= store.UpdateDispatchPlunderStatus(
                    job.ServerId, job.TaskUuid, "failed", "invalid scheduled target", false, now);
                continue;
            }

            if (!store.UpdateDispatchPlunderStatus(job.ServerId, job.TaskUuid, "running", null, true, now))
                continue;

            changed = true;
            armedCandidates.Add((job, new DispatchPlunderArmJob(
                kind,
                checked((int)job.ServerId),
                ownerServer.Value,
                providerUuid,
                job.PlunderAt)));
        }

        if (armedCandidates.Count == 0) return changed;

        IReadOnlyList<DispatchPlunderArmResult> results;
        try
        {
            results = await WithProtectedTimeoutAsync(
                provider.ArmDispatchPlunderAsync(
                    armedCandidates.Select(item => item.Request).ToArray(), cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            foreach ((DispatchPlunderWorkItem job, _) in armedCandidates)
                store.UpdateDispatchPlunderStatus(
                    job.ServerId, job.TaskUuid, "waiting_connection",
                    "DISPATCH_PLUNDER_GAME_DISCONNECTED", false, nowMilliseconds());
            return true;
        }
        catch (BridgeCommandException error)
        {
            string failure = string.IsNullOrWhiteSpace(error.Code)
                ? "DISPATCH_PLUNDER_SEND_FAILED"
                : error.Code;
            foreach ((DispatchPlunderWorkItem job, _) in armedCandidates)
                store.UpdateDispatchPlunderStatus(job.ServerId, job.TaskUuid, "failed", failure, false, nowMilliseconds());
            return true;
        }
        catch (TimeoutException)
        {
            foreach ((DispatchPlunderWorkItem job, _) in armedCandidates)
                store.UpdateDispatchPlunderStatus(
                    job.ServerId, job.TaskUuid, "failed", "DISPATCH_PLUNDER_RESPONSE_TIMEOUT", false, nowMilliseconds());
            return true;
        }

        foreach ((DispatchPlunderWorkItem job, DispatchPlunderArmJob request) in armedCandidates)
        {
            DispatchPlunderArmResult? result = results.FirstOrDefault(candidate =>
                candidate.ServerId == request.ServerId &&
                string.Equals(candidate.Kind, request.Kind, StringComparison.Ordinal) &&
                string.Equals(candidate.TaskUuid, request.TaskUuid, StringComparison.Ordinal));
            if (result?.Armed == true)
            {
                dispatchPending[(job.ServerId, job.TaskUuid)] = new DispatchPending(
                    job.ServerId,
                    job.TaskUuid,
                    request.TaskUuid,
                    request.Kind,
                    Math.Max(nowMilliseconds(), job.PlunderAt) + DispatchResultHorizonMilliseconds);
                continue;
            }

            string error = result?.ErrorCode ?? "DISPATCH_PLUNDER_SEND_FAILED";
            store.UpdateDispatchPlunderStatus(job.ServerId, job.TaskUuid, "failed", error, false, nowMilliseconds());
        }
        return true;
    }

    private async ValueTask<bool> ArmTruckAsync(
        TruckPlunderWorkItem job,
        long now,
        CancellationToken cancellationToken)
    {
        string jobId = ReadString(job.Truck, "jobId");
        int? robTimes = ReadNullableInt(job.Truck, "robTimes");
        int? maxLootCount = ReadNullableInt(job.Truck, "maxLootCount");
        if (!IsPositiveDecimal(job.TrainUuid) || string.IsNullOrWhiteSpace(jobId) || job.ServerId is < 1 or > 99999)
        {
            return store.UpdateTruckPlunderStatus(
                job.ServerId, job.TrainUuid, "failed", "invalid scheduled target", false, now);
        }

        if (!store.UpdateTruckPlunderStatus(job.ServerId, job.TrainUuid, "running", null, true, now))
            return false;

        try
        {
            TruckPlunderArmResult result = await WithProtectedTimeoutAsync(
                provider.ArmTruckPlunderAsync(
                    new TruckPlunderArmJob(
                        checked((int)job.ServerId), job.TrainUuid, jobId, job.ExecuteAt, robTimes, maxLootCount),
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (result.Armed)
            {
                truckPending = new TruckPending(
                    job.ServerId,
                    job.TrainUuid,
                    jobId,
                    Math.Max(nowMilliseconds(), job.ExecuteAt) + TruckResultHorizonMilliseconds);
            }
            else
            {
                store.UpdateTruckPlunderStatus(
                    job.ServerId,
                    job.TrainUuid,
                    "failed",
                    result.ErrorCode ?? "server response timeout",
                    false,
                    nowMilliseconds());
            }
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            store.UpdateTruckPlunderStatus(
                job.ServerId, job.TrainUuid, "waiting_connection", "game disconnected", false, nowMilliseconds());
        }
        catch (BridgeCommandException error)
        {
            bool connected = await IsConnectedAsync(cancellationToken).ConfigureAwait(false);
            store.UpdateTruckPlunderStatus(
                job.ServerId,
                job.TrainUuid,
                connected ? "failed" : "waiting_connection",
                error.Code.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)
                    ? "server response timeout"
                    : (string.IsNullOrWhiteSpace(error.Message) ? error.Code : error.Message),
                false,
                nowMilliseconds());
        }
        catch (TimeoutException)
        {
            bool connected = await IsConnectedAsync(cancellationToken).ConfigureAwait(false);
            store.UpdateTruckPlunderStatus(
                job.ServerId,
                job.TrainUuid,
                connected ? "failed" : "waiting_connection",
                "server response timeout",
                false,
                nowMilliseconds());
        }
        return true;
    }

    private async ValueTask<bool> DrainDispatchResultsAsync(long now, CancellationToken cancellationToken)
    {
        IReadOnlyList<DispatchPlunderResultEvent> events;
        try
        {
            events = await provider.DrainDispatchPlunderResultsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            return false;
        }

        bool changed = false;
        foreach (DispatchPlunderResultEvent result in events)
        {
            DispatchPending? pending = dispatchPending.Values.FirstOrDefault(candidate =>
                candidate.ServerId == result.ServerId &&
                string.Equals(candidate.ProviderTaskUuid, result.TaskUuid, StringComparison.Ordinal) &&
                string.Equals(candidate.Kind, result.Kind, StringComparison.Ordinal));
            if (pending is null) continue;

            string? error = result.Success ? null : result.ErrorCode ?? "DISPATCH_PLUNDER_SEND_FAILED";
            if (!result.Success && string.Equals(
                    error, "DISPATCH_PLUNDER_GAME_DISCONNECTED", StringComparison.Ordinal))
            {
                changed |= store.UpdateDispatchPlunderStatus(
                    pending.ServerId,
                    pending.DurableTaskUuid,
                    "waiting_connection",
                    "DISPATCH_PLUNDER_GAME_DISCONNECTED",
                    false,
                    now);
                dispatchPending.Remove((pending.ServerId, pending.DurableTaskUuid));
                changed |= ApplyServerDay(result.ServerDayStartAt, now);
                continue;
            }

            changed |= store.UpdateDispatchPlunderStatus(
                pending.ServerId,
                pending.DurableTaskUuid,
                result.Success ? "succeeded" : "failed",
                error,
                false,
                now);
            dispatchPending.Remove((pending.ServerId, pending.DurableTaskUuid));
            if (error == "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED")
                changed |= store.StopActiveDispatchPlunderAtDailyLimit(now) > 0;
            changed |= ApplyServerDay(result.ServerDayStartAt, now);
        }
        return changed;
    }

    private async ValueTask<bool> DrainTruckResultsAsync(long now, CancellationToken cancellationToken)
    {
        IReadOnlyList<TruckPlunderResultEvent> events;
        try
        {
            events = await provider.DrainTruckPlunderResultsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            return false;
        }

        bool changed = false;
        foreach (TruckPlunderResultEvent result in events)
        {
            if (truckPending is not { } pending ||
                pending.ServerId != result.ServerId ||
                !string.Equals(pending.TrainUuid, result.TrainUuid, StringComparison.Ordinal) ||
                !string.Equals(pending.JobId, result.JobId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!result.Success && string.Equals(
                    result.ErrorCode, "game disconnected", StringComparison.Ordinal))
            {
                changed |= store.UpdateTruckPlunderStatus(
                    pending.ServerId,
                    pending.TrainUuid,
                    "waiting_connection",
                    "game disconnected",
                    false,
                    now);
                truckPending = null;
                changed |= ApplyServerDay(result.ServerDayStartAt, now);
                continue;
            }

            changed |= store.RecordTruckPlunderResult(
                pending.ServerId,
                pending.TrainUuid,
                pending.JobId,
                result.Success,
                result.Success ? null : result.ErrorCode,
                now,
                result.BattleWon,
                result.PlunderRewards,
                result.RobTimes,
                result.RemainingLootCount,
                result.DailyRobCount);
            truckPending = null;
            changed |= ApplyServerDay(result.ServerDayStartAt, now);
        }
        return changed;
    }

    private bool ApplyServerDay(long? serverDayStartAt, long updatedAt)
    {
        if (serverDayStartAt is not > 0) return false;
        store.WriteSetting("map_plunder_server_day_start", JsonSerializer.Serialize(serverDayStartAt.Value), updatedAt);
        int changed = store.ClearDispatchPlunderHistory(serverDayStartAt.Value, null);
        changed += store.ClearTruckPlunderHistory(serverDayStartAt.Value);
        return changed > 0;
    }

    private async ValueTask<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetCurrentServerIdAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            return false;
        }
    }

    private async ValueTask<T> WithProtectedTimeoutAsync<T>(
        ValueTask<T> operation,
        CancellationToken cancellationToken)
    {
        return await operation.AsTask().WaitAsync(protectedCallTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WithProtectedTimeoutAsync(
        ValueTask operation,
        CancellationToken cancellationToken)
    {
        await operation.AsTask().WaitAsync(protectedCallTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static string ReadString(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ValueKind == JsonValueKind.Number
                ? value.GetRawText()
                : string.Empty;
    }

    private static int? ReadNullableInt(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.TryGetInt32(out int integer)) return integer;
        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static bool IsPositiveDecimal(string value) =>
        value.Length > 0 && value.All(ch => ch is >= '0' and <= '9') &&
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed) && parsed > 0;
}
