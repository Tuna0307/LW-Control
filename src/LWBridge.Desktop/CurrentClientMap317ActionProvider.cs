using System.Globalization;
using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

/// <summary>
/// Production current-client adapter for the exact 0.3.17 Map action plane.
/// Only source-backed current-client actions are implemented here; unrecovered
/// protected actions fail explicitly instead of fabricating game behavior.
/// </summary>
internal sealed class CurrentClientMap317ActionProvider : Map317.IMapActionProvider
{
    private readonly CurrentClientMapBlockSource source;
    private readonly object plunderGate = new();
    private readonly Dictionary<string, CurrentClientDispatchPlunderPendingHandle> dispatchPending =
        new(StringComparer.Ordinal);
    private readonly Queue<Map317.DispatchPlunderResultEvent> dispatchImmediate = new();
    private CurrentClientTruckPlunderPendingHandle? truckPending;
    private readonly Queue<Map317.TruckPlunderResultEvent> truckImmediate = new();

    internal CurrentClientMap317ActionProvider(CurrentClientMapBlockSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async ValueTask GotoWorldCoordinateAsync(
        int serverId, int x, int y, CancellationToken cancellationToken = default)
    {
        await TranslateAsync(() => source.JumpToCoordinateAsync(serverId, x, y, cancellationToken))
            .ConfigureAwait(false);
    }

    public async ValueTask GotoWorldMarchAsync(
        int serverId, string marchUuid, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(marchUuid, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed <= 0)
            throw new Map317.BridgeCommandException("INVALID_MARCH", "server ID and march UUID are required");
        await TranslateAsync(() => source.FollowMarchAsync(serverId, parsed, cancellationToken)).ConfigureAwait(false);
    }

    public async ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default)
    {
        CurrentClientMapStatusContext context = await TranslateAsync(
            () => source.GetMapStatusContextAsync(cancellationToken)).ConfigureAwait(false);
        if (context.ServerId <= 0)
            throw ProviderUnavailable("current server id unavailable");
        return context.ServerId;
    }

    public async ValueTask GotoServerAsync(int serverId, CancellationToken cancellationToken = default)
    {
        await TranslateAsync(() => source.JumpToServerAsync(serverId, cancellationToken)).ConfigureAwait(false);
    }

    public async ValueTask<Map317.TreasureInspectionResult> InspectTreasureStatesAsync(
        IReadOnlyList<JsonElement> records,
        bool refresh,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
            return new Map317.TreasureInspectionResult(string.Empty, string.Empty, Array.Empty<JsonElement>());

        int serverId = checked((int)ReadInteger(records[0], "serverId"));
        var normalized = records.Select(row => ToTreasureRecord(serverId, row)).ToArray();
        var states = new List<JsonElement>(normalized.Length);
        string playerUid = string.Empty;
        string allianceId = string.Empty;
        foreach (CurrentClientTreasureInspectionRecord[] batch in normalized.Chunk(100))
        {
            CurrentClientTreasureInspectionResult result = await TranslateAsync(
                () => source.InspectTreasureStatesAsync(serverId, batch, refresh, cancellationToken))
                .ConfigureAwait(false);
            if (playerUid.Length == 0)
            {
                playerUid = result.PlayerUid;
                allianceId = result.AllianceId;
            }
            else if (!string.Equals(playerUid, result.PlayerUid, StringComparison.Ordinal) ||
                     !string.Equals(allianceId, result.AllianceId, StringComparison.Ordinal))
            {
                throw new Map317.BridgeCommandException(
                    "GAME_PROVIDER_ERROR", "treasure inspection identity changed during batching");
            }
            states.AddRange(result.States.Select(item => item.Clone()));
        }
        return new Map317.TreasureInspectionResult(playerUid, allianceId, states);
    }

    public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromException<JsonElement>(ProviderUnavailable("current-client Treasure claim status provider is unavailable"));

    public ValueTask<JsonElement> ClaimTreasuresAsync(
        Map317.TreasureClaimProviderRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<JsonElement>(ProviderUnavailable("current-client Treasure claim provider is unavailable"));

    public async ValueTask<Map317.DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(
        JsonElement row,
        CancellationToken cancellationToken = default)
    {
        try
        {
            bool shared = await TranslateAsync(
                () => source.ShareDispatchTaskToAllianceAsync(row, cancellationToken)).ConfigureAwait(false);
            return new Map317.DispatchShareProviderResult(shared);
        }
        catch (Map317.BridgeCommandException)
        {
            // Exact 0.3.17 aggregates a protected-call error/timeout as a failed
            // row. The provider only returns Shared=true after the current client
            // observes ChatHeroDispatchShare.HandleMessage without errorCode.
            return new Map317.DispatchShareProviderResult(false);
        }
    }

    public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(
        IReadOnlyList<JsonElement> rows,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<IReadOnlyList<JsonElement>>(
            ProviderUnavailable(
                "current-client Ghost plunder preparation remains unavailable because " +
                "GhostReconSteal terminal response identity is not source-proven"));


    internal static IReadOnlyList<JsonElement> PrepareGhostPlunderRows(IReadOnlyList<JsonElement> rows)
    {
        var prepared = new JsonElement[rows.Count];
        for (int index = 0; index < rows.Count; index++)
        {
            JsonElement row = rows[index];
            string uuid = ReadText(row, "uuid");
            if (!IsPositiveDecimal(uuid))
                throw new InvalidDataException("Ghost plunder row contains invalid task UUID.");

            long ownerServer = ReadInteger(row, "ownerServer");
            long completionTime = ReadInteger(row, "completionTime");
            long protectSeconds = ReadInteger(row, "protectTime");
            long plunderAt = ReadInteger(row, "plunderAt");
            long taskExpireTime = ReadInteger(row, "taskExpireTime");
            long stealListCount = ReadInteger(row, "stealListCount");
            long stealMaxTimes = ReadInteger(row, "stealMaxTimes");
            long stolenCount = ReadInteger(row, "stolenCount");
            long maxStealCount = ReadInteger(row, "maxStealCount");

            if (ownerServer is < 1 or > 99999 || completionTime <= 0 || protectSeconds < 0 ||
                taskExpireTime <= 0 || stealListCount < 0 || stealMaxTimes < 0 ||
                stolenCount != stealListCount || maxStealCount != stealMaxTimes)
            {
                throw new InvalidDataException("Ghost plunder row is not a source-backed current-v22 task projection.");
            }

            long earliestPlunderAt = checked(completionTime + protectSeconds * 1000L);
            if (plunderAt < earliestPlunderAt || plunderAt >= taskExpireTime)
                throw new InvalidDataException("Ghost plunder row contains invalid protection/expiry timing.");

            prepared[index] = row.Clone();
        }
        return prepared;
    }

    public async ValueTask<Map317.MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(
        CancellationToken cancellationToken = default)
    {
        CurrentClientMapPlunderServerDayResult result = await TranslateAsync(
            () => source.GetMapPlunderServerDayStartAsync(cancellationToken)).ConfigureAwait(false);
        return new Map317.MapPlunderServerDayProviderResult(result.ServerTime, result.ServerDayStartAt);
    }

    public async ValueTask<IReadOnlyList<Map317.DispatchPlunderArmResult>> ArmDispatchPlunderAsync(
        IReadOnlyList<Map317.DispatchPlunderArmJob> jobs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        var results = new Map317.DispatchPlunderArmResult[jobs.Count];
        var toArm = new List<(int Index, Map317.DispatchPlunderArmJob Job)>();
        lock (plunderGate)
        {
            for (int index = 0; index < jobs.Count; index++)
            {
                Map317.DispatchPlunderArmJob job = jobs[index];
                if (dispatchPending.ContainsKey(DispatchKey(job.Kind, job.ServerId, job.TaskUuid)))
                    results[index] = new Map317.DispatchPlunderArmResult(job.Kind, job.ServerId, job.TaskUuid, true);
                else
                    toArm.Add((index, job));
            }
        }

        if (toArm.Count > 0)
        {
            IReadOnlyList<CurrentClientDispatchPlunderArmOutcome> outcomes = await TranslateAsync(
                () => source.ArmDispatchPlunderBatchAsync(
                    toArm.Select(item => item.Job).ToArray(), cancellationToken)).ConfigureAwait(false);
            if (outcomes.Count != toArm.Count)
                throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", "dispatch arm result count mismatch");

            lock (plunderGate)
            {
                for (int offset = 0; offset < outcomes.Count; offset++)
                {
                    (int index, Map317.DispatchPlunderArmJob job) = toArm[offset];
                    CurrentClientDispatchPlunderArmOutcome outcome = outcomes[offset];
                    results[index] = outcome.Result;
                    if (outcome.Pending is not null)
                        dispatchPending[DispatchKey(job.Kind, job.ServerId, job.TaskUuid)] = outcome.Pending;
                    if (outcome.ImmediateResult is not null)
                        dispatchImmediate.Enqueue(outcome.ImmediateResult);
                }
            }
        }
        return results;
    }

    public async ValueTask<Map317.TruckPlunderArmResult> ArmTruckPlunderAsync(
        Map317.TruckPlunderArmJob job,
        CancellationToken cancellationToken = default)
    {
        lock (plunderGate)
        {
            if (truckPending is not null &&
                truckPending.Job.ServerId == job.ServerId &&
                string.Equals(truckPending.Job.TrainUuid, job.TrainUuid, StringComparison.Ordinal) &&
                string.Equals(truckPending.Job.JobId, job.JobId, StringComparison.Ordinal))
                return new Map317.TruckPlunderArmResult(true);
        }

        CurrentClientTruckPlunderArmOutcome outcome = await TranslateAsync(
            () => source.ArmTruckPlunderAsync(job, cancellationToken)).ConfigureAwait(false);
        lock (plunderGate)
        {
            if (outcome.Pending is not null) truckPending = outcome.Pending;
            if (outcome.ImmediateResult is not null) truckImmediate.Enqueue(outcome.ImmediateResult);
        }
        return outcome.Result;
    }

    public ValueTask<IReadOnlyList<Map317.DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<Map317.DispatchPlunderResultEvent>();
        CurrentClientDispatchPlunderPendingHandle[] snapshot;
        lock (plunderGate)
        {
            while (dispatchImmediate.Count > 0) results.Add(dispatchImmediate.Dequeue());
            snapshot = dispatchPending.Values.ToArray();
        }

        foreach (CurrentClientDispatchPlunderPendingHandle handle in snapshot)
        {
            Map317.DispatchPlunderResultEvent? terminal = Translate(
                () => source.TryDrainDispatchPlunderResult(handle));
            if (terminal is null) continue;
            results.Add(terminal);
            lock (plunderGate)
            {
                string key = DispatchKey(handle.Job.Kind, handle.Job.ServerId, handle.Job.TaskUuid);
                if (dispatchPending.TryGetValue(key, out CurrentClientDispatchPlunderPendingHandle? current) &&
                    current == handle)
                    dispatchPending.Remove(key);
            }
        }
        return ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>(results);
    }

    public ValueTask<IReadOnlyList<Map317.TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<Map317.TruckPlunderResultEvent>();
        CurrentClientTruckPlunderPendingHandle? handle;
        lock (plunderGate)
        {
            while (truckImmediate.Count > 0) results.Add(truckImmediate.Dequeue());
            handle = truckPending;
        }
        if (handle is not null)
        {
            Map317.TruckPlunderResultEvent? terminal = Translate(
                () => source.TryDrainTruckPlunderResult(handle));
            if (terminal is not null)
            {
                results.Add(terminal);
                lock (plunderGate)
                {
                    if (truckPending == handle) truckPending = null;
                }
            }
        }
        return ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>(results);
    }

    public async ValueTask ClearTruckPlunderPendingAsync(
        int serverId,
        string trainUuid,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        CurrentClientTruckPlunderPendingHandle? handle;
        lock (plunderGate)
        {
            handle = truckPending;
            if (handle is null || handle.Job.ServerId != serverId ||
                !string.Equals(handle.Job.TrainUuid, trainUuid, StringComparison.Ordinal) ||
                !string.Equals(handle.Job.JobId, jobId, StringComparison.Ordinal))
                return;
        }
        await TranslateAsync(() => source.ClearTruckPlunderPendingAsync(handle, cancellationToken)).ConfigureAwait(false);
        lock (plunderGate)
        {
            if (truckPending == handle) truckPending = null;
        }
    }

    private static string DispatchKey(string kind, int serverId, string taskUuid) =>
        kind + ":" + serverId.ToString(CultureInfo.InvariantCulture) + ":" + taskUuid;

    private static CurrentClientTreasureInspectionRecord ToTreasureRecord(int expectedServerId, JsonElement row)
    {
        int serverId = checked((int)ReadInteger(row, "serverId"));
        if (serverId != expectedServerId)
            throw new Map317.BridgeCommandException("INVALID_REQUEST", "Treasure inspection spans multiple servers");
        return new CurrentClientTreasureInspectionRecord(
            serverId,
            checked((int)ReadInteger(row, "pointIndex")),
            ReadText(row, "uuid"),
            checked((int)ReadInteger(row, "treasureType")),
            checked((int)ReadInteger(row, "suppliesType")),
            ReadText(row, "allianceId"),
            ReadText(row, "viewerUid"),
            ReadText(row, "viewerAllianceId"),
            ReadNullableBoolean(row, "viewerHasReward"),
            ReadNullableBoolean(row, "viewerIsWorking"),
            ReadNullableBoolean(row, "complete"),
            ReadNullableInteger(row, "expireTime"),
            ReadNullableInteger(row, "startTime"),
            ReadNullableInteger(row, "completionTime"),
            ReadNullableInt(row, "rewardedCount"),
            ReadNullableInt(row, "diggingCount"),
            ReadNullableInt(row, "rewardMax"),
            ReadNullableInt(row, "remainingBoxes"),
            ReadNullableInteger(row, "createTime"),
            ReadText(row, "discovererAllianceId"),
            ReadText(row, "discovererUid"),
            ReadNullableInt(row, "workState"),
            ReadNullableInt(row, "userCount"));
    }

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (BridgeCommandException error)
        {
            throw new Map317.BridgeCommandException(error.Code, error.Message, error.Details);
        }
        catch (TimeoutException error)
        {
            throw new Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", error.Message);
        }
        catch (InvalidDataException error)
        {
            throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", error.Message);
        }
    }

    private static async Task TranslateAsync(Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (BridgeCommandException error)
        {
            throw new Map317.BridgeCommandException(error.Code, error.Message, error.Details);
        }
        catch (TimeoutException error)
        {
            throw new Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", error.Message);
        }
        catch (InvalidDataException error)
        {
            throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", error.Message);
        }
    }

    private static T Translate<T>(Func<T> action)
    {
        try { return action(); }
        catch (BridgeCommandException error)
        {
            throw new Map317.BridgeCommandException(error.Code, error.Message, error.Details);
        }
        catch (TimeoutException error)
        {
            throw new Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", error.Message);
        }
        catch (InvalidDataException error)
        {
            throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", error.Message);
        }
    }

    private static Map317.BridgeCommandException ProviderUnavailable(string message) =>
        new("GAME_PROVIDER_UNAVAILABLE", message);

    private static string ReadText(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static long ReadInteger(JsonElement row, string name) => ReadNullableInteger(row, name) ?? 0;

    private static bool IsPositiveDecimal(string value) =>
        value.Length > 0 && value.Any(ch => ch is >= '1' and <= '9') && value.All(ch => ch is >= '0' and <= '9');

    private static long? ReadNullableInteger(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.TryGetInt64(out long integer)) return integer;
        return value.ValueKind == JsonValueKind.String &&
               long.TryParse(value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : null;
    }

    private static int? ReadNullableInt(JsonElement row, string name)
    {
        long? value = ReadNullableInteger(row, name);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static bool? ReadNullableBoolean(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null;
    }
}
