using System.Globalization;
using System.Text.Json;

namespace LWBridge.Map317;

public sealed record CoordinateJumpResult(int ServerId, int X, int Y);
public sealed record MarchFollowResult(int ServerId, string MarchUuid);
public sealed record ServerJumpResult(bool Changed, int PreviousServerId, int ServerId);
public sealed record DispatchShareResult(int Shared, int Failed, IReadOnlyList<string> SharedUuids);

public sealed class MapActionControlPlane
{
    private const string PlunderServerDaySettingKey = "map_plunder_server_day_start";
    private const long ServerDayMilliseconds = 86_400_000;
    public const int ProtectedCallTimeoutMilliseconds = 5_000;
    private readonly MapStore store;
    private readonly IMapActionProvider provider;
    private readonly Func<long> nowMilliseconds;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly TimeSpan protectedCallTimeout;
    private int operationOwned;

    public MapActionControlPlane(
        MapStore store,
        IMapActionProvider provider,
        Func<long>? nowMilliseconds = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeSpan? protectedCallTimeout = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.nowMilliseconds = nowMilliseconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        this.delay = delay ?? Task.Delay;
        this.protectedCallTimeout = protectedCallTimeout ?? TimeSpan.FromMilliseconds(ProtectedCallTimeoutMilliseconds);
    }

    public event EventHandler? DispatchPlunderChanged;
    public event EventHandler? TruckPlunderChanged;

    public async ValueTask<CoordinateJumpResult> CoordinateJumpAsync(
        int serverId, int x, int y, CancellationToken cancellationToken = default)
    {
        if (serverId <= 0 || x <= 0 || y <= 0)
            throw new BridgeCommandException("INVALID_COORDINATE", "server ID and coordinates must be positive integers");
        using OperationLease lease = EnterGameOperation();
        await ProtectedAsync(
            provider.GotoWorldCoordinateAsync(serverId, x, y, cancellationToken), cancellationToken).ConfigureAwait(false);
        return new CoordinateJumpResult(serverId, x, y);
    }

    public async ValueTask<MarchFollowResult> MarchFollowAsync(
        int serverId, string marchUuid, CancellationToken cancellationToken = default)
    {
        if (serverId <= 0 || string.IsNullOrWhiteSpace(marchUuid))
            throw new BridgeCommandException("INVALID_MARCH", "server ID and march UUID are required");
        using OperationLease lease = EnterGameOperation();
        await ProtectedAsync(
            provider.GotoWorldMarchAsync(serverId, marchUuid, cancellationToken), cancellationToken).ConfigureAwait(false);
        return new MarchFollowResult(serverId, marchUuid);
    }

    public async ValueTask<ServerJumpResult> ServerJumpAsync(
        int serverId, CancellationToken cancellationToken = default)
    {
        MapStore.ValidateServerId(serverId);
        using OperationLease lease = EnterGameOperation();
        int previous = await ProtectedAsync(
            provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (previous == serverId) return new ServerJumpResult(false, previous, serverId);

        await ProtectedAsync(provider.GotoServerAsync(serverId, cancellationToken), cancellationToken).ConfigureAwait(false);
        long deadline = checked(nowMilliseconds() + 10_000L);
        while (nowMilliseconds() < deadline)
        {
            await delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            int current = await ProtectedAsync(
                provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
            if (current == serverId) return new ServerJumpResult(true, previous, serverId);
        }
        throw new BridgeCommandException("SERVER_JUMP_TIMEOUT", "the game did not switch to the target server");
    }

    public async ValueTask<TreasureInspectionResult> RefreshTreasureStatesAsync(
        int serverId,
        IReadOnlyList<JsonElement> records,
        CancellationToken cancellationToken = default)
    {
        MapStore.ValidateServerId(serverId);
        ArgumentNullException.ThrowIfNull(records);
        using OperationLease lease = EnterGameOperation();
        await RequireLiveServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        TreasureInspectionResult result = await ProtectedAsync(
            provider.InspectTreasureStatesAsync(records, refresh: true, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        PersistTreasureStates(serverId, result);
        return result;
    }

    public ValueTask<TreasureInspectionResult> RefreshAllTreasureStatesAsync(
        int serverId,
        CancellationToken cancellationToken = default) =>
        RefreshTreasureStatesAsync(serverId, store.ReadSeasonSupplyTreasureRows(serverId), cancellationToken);

    public async ValueTask<JsonElement> TreasureClaimStatusAsync(CancellationToken cancellationToken = default)
    {
        using OperationLease lease = EnterGameOperation();
        return await ProtectedAsync(
            provider.GetTreasureClaimStatusAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<JsonElement> ClaimTreasuresAsync(
        int serverId,
        string claimScope,
        bool prioritizeLuckySlots = true,
        string targetUuid = "",
        CancellationToken cancellationToken = default)
    {
        MapStore.ValidateServerId(serverId);
        if (claimScope is not ("single" or "boxes" or "season") ||
            (claimScope == "single" && string.IsNullOrEmpty(targetUuid)))
        {
            throw new BridgeCommandException("INVALID_TREASURE_CLAIM_SCOPE", "treasure claim scope is invalid");
        }
        using OperationLease lease = EnterGameOperation();
        await RequireLiveServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<JsonElement> records = store.ReadTreasureClaimCandidates(serverId, nowMilliseconds());
        return await ProtectedAsync(
            provider.ClaimTreasuresAsync(
                new TreasureClaimProviderRequest(serverId, records, claimScope, targetUuid, prioritizeLuckySlots),
                cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DispatchShareResult> ShareDispatchToAllianceAsync(
        IReadOnlyList<JsonElement> rows,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JsonElement> normalized = ValidateShareRows(rows);
        using OperationLease lease = EnterGameOperation();
        var sharedUuids = new List<string>(normalized.Count);
        int failed = 0;
        foreach (JsonElement row in normalized)
        {
            string uuid = row.GetProperty("uuid").GetString()!;
            DispatchShareProviderResult result;
            try
            {
                result = await ProtectedAsync(
                    provider.ShareDispatchTaskToAllianceAsync(row, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (BridgeCommandException)
            {
                // Exact 0.3.17 shares rows sequentially and aggregates protected
                // call failures into the failed count rather than aborting the
                // remaining batch.
                failed++;
                continue;
            }
            if (result.Shared) sharedUuids.Add(uuid); else failed++;
        }
        return new DispatchShareResult(sharedUuids.Count, failed, sharedUuids);
    }

    public async ValueTask<IReadOnlyList<JsonElement>> ScheduleDispatchPlunderAsync(
        IReadOnlyList<JsonElement> rows,
        CancellationToken cancellationToken = default)
    {
        DispatchScheduleRow[] normalized = ValidateDispatchScheduleRows(rows);
        JsonElement[] ghostInput = normalized.Where(row => row.TaskKind == "ghost").Select(row => row.Row).ToArray();
        Dictionary<string, JsonElement>? preparedGhost = null;
        if (ghostInput.Length > 0)
        {
            using OperationLease lease = EnterGameOperation();
            IReadOnlyList<JsonElement> prepared = await ProtectedAsync(
                provider.PrepareGhostPlunderTasksAsync(ghostInput, cancellationToken), cancellationToken).ConfigureAwait(false);
            preparedGhost = prepared
                .Where(row => row.ValueKind == JsonValueKind.Object && row.TryGetProperty("uuid", out _))
                .ToDictionary(row => row.GetProperty("uuid").ToString(), row => row, StringComparer.Ordinal);
            if (preparedGhost.Count != ghostInput.Length)
                throw new BridgeCommandException("INVALID_REQUEST", "ghost scheduling data unavailable");
        }

        var scheduled = new List<JsonElement>(normalized.Length);
        long now = nowMilliseconds();
        foreach (DispatchScheduleRow item in normalized)
        {
            JsonElement source = item.Row;
            if (item.TaskKind == "ghost") source = preparedGhost![item.Uuid];
            DispatchScheduleRow current = NormalizeDispatchScheduleRow(source, item.TaskKind);
            string taskUuid = current.TaskKind == "ghost" ? "ghost:" + current.Uuid : current.Uuid;
            JsonElement? stored = store.ScheduleDispatchPlunderRow(
                current.ServerId, taskUuid, current.Row.GetRawText(), current.CompletionTime,
                current.PlunderAt, current.ExpireAt, now);
            if (stored is null)
                throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled plunder job is missing");
            scheduled.Add(stored.Value);
        }
        DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
        return scheduled;
    }

    public void CancelDispatchPlunder(long serverId, string taskUuid)
    {
        if (serverId <= 0 || string.IsNullOrEmpty(taskUuid))
            throw new BridgeCommandException("INVALID_REQUEST", "server ID and secret task UUID are required");
        if (!store.CancelDispatchPlunder(serverId, taskUuid, nowMilliseconds()))
            throw new BridgeCommandException("NOT_FOUND", "scheduled plunder job not found");
        DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
    }

    public int ClearDispatchPlunder(long before, string? taskKind)
    {
        int count = store.ClearDispatchPlunderHistory(before, taskKind);
        DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
        return count;
    }

    public IReadOnlyList<TruckPlunderScheduleResult> ScheduleTruckPlunder(IReadOnlyList<JsonElement> rows)
    {
        TruckScheduleRow[] normalized = ValidateTruckRows(rows);
        long now = nowMilliseconds();
        var result = new List<TruckPlunderScheduleResult>(normalized.Length);
        foreach (TruckScheduleRow row in normalized)
            result.Add(store.ScheduleTruckPlunder(row.ServerId, row.Uuid, row.Row.GetRawText(), row.ExecuteAt, row.ExpireAt, now));
        TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void CancelTruckPlunder(long serverId, string trainUuid)
    {
        if (serverId <= 0 || string.IsNullOrEmpty(trainUuid))
            throw new BridgeCommandException("INVALID_REQUEST", "truck target is required");
        if (!store.CancelTruckPlunder(serverId, trainUuid, nowMilliseconds()))
            throw new BridgeCommandException("NOT_FOUND", "scheduled truck job not found");
        TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
    }

    public int ClearTruckPlunder(long before)
    {
        int count = store.ClearTruckPlunderHistory(before);
        TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
        return count;
    }

    public MapPlunderJobsSnapshot ListPlunderJobs() => store.ReadPlunderJobs();

    public async ValueTask<MapPlunderJobsSnapshot> ListPlunderJobsAsync(
        CancellationToken cancellationToken = default)
    {
        long? serverDayStartAt = ReadCachedPlunderServerDayStart();
        try
        {
            MapPlunderServerDayProviderResult? live = await ProtectedAsync(
                provider.GetMapPlunderServerDayStartAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
            if (live is not null &&
                live.ServerDayStartAt > 0 &&
                live.ServerTime >= live.ServerDayStartAt &&
                live.ServerTime - live.ServerDayStartAt < ServerDayMilliseconds)
            {
                serverDayStartAt = live.ServerDayStartAt;
                store.WriteSetting(
                    PlunderServerDaySettingKey,
                    JsonSerializer.Serialize(live.ServerDayStartAt),
                    nowMilliseconds());
            }
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
            // Exact 0.3.17 skips the provider refresh when the game provider is
            // unavailable and continues from a previously persisted usable day.
        }

        if (serverDayStartAt is > 0)
        {
            store.ClearDispatchPlunderHistory(serverDayStartAt.Value, null);
            store.ClearTruckPlunderHistory(serverDayStartAt.Value);
        }
        MapPlunderJobsSnapshot snapshot = store.ReadPlunderJobs();
        return snapshot with { ServerDayStartAt = serverDayStartAt };
    }
    public IReadOnlyList<JsonElement> ListDispatchPlunderJobs() => store.ReadDispatchPlunderJobs();
    public IReadOnlyList<JsonElement> ListTruckPlunderJobs() => store.ReadTruckPlunderJobs();
    public IReadOnlyList<int> GetServerJumpHistory() => store.GetServerJumpHistory();

    public void RetryDispatchPlunder(long serverId, string taskUuid)
    {
        if (!store.RetryDispatchPlunder(serverId, taskUuid, nowMilliseconds()))
            throw new BridgeCommandException("NOT_FOUND", "scheduled plunder job not found");
        DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
    }

    public TruckPlunderScheduleResult RetryTruckPlunder(long serverId, string trainUuid)
    {
        TruckPlunderScheduleResult result = store.RetryTruckPlunder(serverId, trainUuid, nowMilliseconds());
        TruckPlunderChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    private long? ReadCachedPlunderServerDayStart()
    {
        string? json = store.ReadSetting(PlunderServerDaySettingKey);
        if (json is null) return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Number ||
                !document.RootElement.TryGetInt64(out long value))
            {
                throw new BridgeCommandException("INVALID_SETTING", "invalid map plunder server day setting");
            }
            return value > 0 ? value : null;
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException("INVALID_SETTING", error.Message);
        }
    }

    private async ValueTask RequireLiveServerAsync(int serverId, CancellationToken cancellationToken)
    {
        int current = await ProtectedAsync(
            provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (current != serverId)
            throw new BridgeCommandException("SERVER_MISMATCH", "current game server does not match map data server");
    }

    private async ValueTask<T> ProtectedAsync<T>(
        ValueTask<T> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation.AsTask().WaitAsync(protectedCallTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
        }
    }

    private async ValueTask ProtectedAsync(
        ValueTask operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await operation.AsTask().WaitAsync(protectedCallTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
        }
    }

    private void PersistTreasureStates(int serverId, TreasureInspectionResult result)
    {
        long now = nowMilliseconds();
        foreach (JsonElement state in result.States)
        {
            if (state.ValueKind != JsonValueKind.Object ||
                !state.TryGetProperty("uuid", out JsonElement uuidValue)) continue;
            string uuid = uuidValue.ToString();
            if (string.IsNullOrEmpty(uuid)) continue;
            long? expire = ReadIntegerLike(state, "expireTime") is long value && value > 0 ? value : null;
            store.UpsertTreasureClaimState(serverId, result.PlayerUid, uuid, expire, now, state.GetRawText(), now);
        }
    }

    private OperationLease EnterGameOperation()
    {
        if (Interlocked.CompareExchange(ref operationOwned, 1, 0) != 0)
            throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS", "another game operation is already in progress");
        return new OperationLease(this);
    }

    private sealed class OperationLease : IDisposable
    {
        private MapActionControlPlane? owner;
        internal OperationLease(MapActionControlPlane owner) => this.owner = owner;
        public void Dispose()
        {
            MapActionControlPlane? value = Interlocked.Exchange(ref owner, null);
            if (value is not null) Volatile.Write(ref value.operationOwned, 0);
        }
    }

    private sealed record DispatchScheduleRow(JsonElement Row, string TaskKind, long ServerId, string Uuid,
        long CompletionTime, long PlunderAt, long? ExpireAt);
    private sealed record TruckScheduleRow(JsonElement Row, long ServerId, string Uuid, long ExecuteAt, long? ExpireAt);

    private static DispatchScheduleRow[] ValidateDispatchScheduleRows(IReadOnlyList<JsonElement> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count is < 1 or > 200)
            throw new BridgeCommandException("INVALID_REQUEST", "select between 1 and 200 secret tasks");
        return rows.Select(row => NormalizeDispatchScheduleRow(row, ReadTaskKind(row))).ToArray();
    }

    private static DispatchScheduleRow NormalizeDispatchScheduleRow(JsonElement row, string taskKind)
    {
        if (row.ValueKind != JsonValueKind.Object || taskKind is not ("dispatch" or "ghost"))
            throw new BridgeCommandException("INVALID_REQUEST", "secret task scheduling data is invalid");
        long server = ReadIntegerLike(row, "serverId");
        string uuid = ReadString(row, "uuid");
        long completion = ReadIntegerLike(row, "completionTime");
        long plunder = ReadIntegerLike(row, "plunderAt");
        long expire = ReadIntegerLike(row, "taskExpireTime");
        long stolen = ReadIntegerLike(row, "stolenCount");
        long maxSteal = ReadIntegerLike(row, "maxStealCount");
        if (server <= 0 || !DecimalString(uuid) || completion <= 0 || plunder < completion ||
            (expire > 0 && expire <= plunder) || (maxSteal > 0 && stolen >= maxSteal))
            throw new BridgeCommandException("INVALID_REQUEST", "secret task scheduling data is invalid");
        return new DispatchScheduleRow(row.Clone(), taskKind, server, uuid, completion, plunder, expire > 0 ? expire : null);
    }

    private static TruckScheduleRow[] ValidateTruckRows(IReadOnlyList<JsonElement> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count is < 1 or > 200)
            throw new BridgeCommandException("INVALID_REQUEST", "select between 1 and 200 trucks");
        return rows.Select(row =>
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new BridgeCommandException("INVALID_REQUEST", "truck scheduling data is invalid");
            long server = ReadIntegerLike(row, "serverId");
            string uuid = ReadString(row, "uuid");
            long execute = ReadIntegerLike(row, "executeAt");
            if (execute <= 0) execute = ReadIntegerLike(row, "protectTime");
            long expire = ReadIntegerLike(row, "expireAt");
            long maxLoot = ReadIntegerLike(row, "maxLootCount");
            long robTimes = ReadIntegerLike(row, "robTimes");
            if (server <= 0 || !DecimalString(uuid) || execute <= 0 || maxLoot <= 0 || robTimes >= maxLoot)
                throw new BridgeCommandException("INVALID_REQUEST", "truck scheduling data is invalid");
            return new TruckScheduleRow(row.Clone(), server, uuid, execute, expire > 0 ? expire : null);
        }).ToArray();
    }

    private static IReadOnlyList<JsonElement> ValidateShareRows(IReadOnlyList<JsonElement> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count is < 1 or > 200)
            throw new BridgeCommandException("INVALID_REQUEST", "select between 1 and 200 dispatch tasks");
        foreach (JsonElement row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object || !DecimalString(ReadString(row, "uuid")) ||
                ReadIntegerLike(row, "serverId") <= 0 || ReadIntegerLike(row, "x") <= 0 ||
                ReadIntegerLike(row, "y") <= 0 || ReadIntegerLike(row, "cfgId") <= 0)
                throw new BridgeCommandException("INVALID_REQUEST", "selected dispatch task cannot be shared");
        }
        return rows.Select(row => row.Clone()).ToArray();
    }

    private static string ReadTaskKind(JsonElement row) => row.TryGetProperty("taskKind", out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : "dispatch";
    private static string ReadString(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static bool DecimalString(string value) => value.Length > 0 && value.All(ch => ch is >= '0' and <= '9');

    private static long ReadIntegerLike(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return 0;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt64(out long integer)) return integer;
            if (value.TryGetDouble(out double floating) && double.IsFinite(floating))
                return floating >= long.MaxValue ? long.MaxValue : floating <= long.MinValue ? long.MinValue : (long)floating;
        }
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out long parsed)) return parsed;
        return 0;
    }
}
