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
        if (serverId <= 0 || x is < 0 or > 999 || y is < 0 or > 999)
            throw new BridgeCommandException("INVALID_COORDINATE",
                "server ID must be positive and map coordinates must be between 0 and 999");
        using OperationLease lease = EnterGameOperation();
        // Stored rows retain their server identity across Auto's return home.
        // Switch while holding the SAME action lease so a competing navigation
        // cannot interleave between switching and navigating to the tile.
        await RequireTargetServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        await ProtectedAsync(
            provider.GotoWorldCoordinateAsync(serverId, x, y, cancellationToken), cancellationToken).ConfigureAwait(false);
        int reachedServer = await ProtectedAsync(
            provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (reachedServer != serverId)
            throw new BridgeCommandException("SERVER_MISMATCH", "map navigation ended on a different server");
        return new CoordinateJumpResult(serverId, x, y);
    }

    public async ValueTask<MarchFollowResult> MarchFollowAsync(
        int serverId, string marchUuid, CancellationToken cancellationToken = default)
    {
        if (serverId <= 0 || string.IsNullOrWhiteSpace(marchUuid))
            throw new BridgeCommandException("INVALID_MARCH", "server ID and march UUID are required");
        using OperationLease lease = EnterGameOperation();
        // March identity is stable even when its coordinates change. Resolve a
        // remote row's server before native JumpToMarchByUuid; never reuse the
        // scan-time coordinates, which are already stale for moving trucks.
        await RequireTargetServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        await ProtectedAsync(
            provider.GotoWorldMarchAsync(serverId, marchUuid, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (await ProtectedAsync(provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken)
            .ConfigureAwait(false) != serverId)
            throw new BridgeCommandException("SERVER_MISMATCH", "follow changed to a different server");
        return new MarchFollowResult(serverId, marchUuid);
    }

    // Original server_jump handler 0x182112-0x182C0D: any server id outside the
    // integer range 1..99999 (including a missing/non-integer payload value) is
    // INVALID_SERVER_ID with exactly this text.
    internal const string ServerJumpInvalidServerText = "server ID must be an integer from 1 to 99999";

    public static void RequireServerJumpServerId(long serverId)
    {
        if (serverId is < 1 or > 99_999)
            throw new BridgeCommandException("INVALID_SERVER_ID", ServerJumpInvalidServerText);
    }

    public static int RequireServerJumpServerId(System.Text.Json.JsonElement payload)
    {
        if (payload.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !payload.TryGetProperty("serverId", out System.Text.Json.JsonElement value) ||
            value.ValueKind != System.Text.Json.JsonValueKind.Number ||
            !value.TryGetInt32(out int serverId))
            throw new BridgeCommandException("INVALID_SERVER_ID", ServerJumpInvalidServerText);
        RequireServerJumpServerId(serverId);
        return serverId;
    }

    public async ValueTask<ServerJumpResult> ServerJumpAsync(
        int serverId, CancellationToken cancellationToken = default)
    {
        RequireServerJumpServerId(serverId);
        using OperationLease lease = EnterGameOperation();
        int previous = await ProtectedAsync(
            provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (previous == serverId) return new ServerJumpResult(false, previous, serverId);

        await AwaitServerTransitionAsync(serverId, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask RequireTargetServerAsync(int serverId, CancellationToken cancellationToken)
    {
        int current = await ProtectedAsync(
            provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (current == serverId) return;
        await AwaitServerTransitionAsync(serverId, cancellationToken).ConfigureAwait(false);
        // Require a fresh source observation; the remote coordinate itself is
        // also identity-gated in the mounted current-client navigation provider.
        int reached = await ProtectedAsync(
            provider.GetCurrentServerIdAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        if (reached != serverId)
            throw new BridgeCommandException("SERVER_JUMP_TIMEOUT", "the target map server was not reached");
    }

    private async ValueTask AwaitServerTransitionAsync(int serverId, CancellationToken cancellationToken)
    {
        // The owned source allows up to 18 seconds for native, exact-session
        // switching; the generic protected-call 5-second budget was shorter
        // than this legitimate command. Preserve cancellation and a hard bound.
        try
        {
            await provider.GotoServerAsync(serverId, cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new BridgeCommandException("SERVER_JUMP_TIMEOUT", "the target server switch timed out");
        }
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
        int serverId = await ProtectedAsync(provider.GetCurrentServerIdAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
        MapStore.ValidateServerId(serverId);
        IReadOnlyList<JsonElement> candidates = store.ReadTreasureClaimCandidates(serverId, nowMilliseconds());
        // Native state inspection is authoritative for the current live owner.
        // Never synthesize a claim/reward, quota or cooldown from the cached rows.
        var states = new List<JsonElement>(candidates.Count);
        string? playerUid = null;
        string? allianceId = null;
        foreach (JsonElement[] batch in candidates.Chunk(100))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TreasureInspectionResult read = await ProtectedAsync(
                provider.InspectTreasureStatesAsync(batch, true, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(read.PlayerUid) || read.AllianceId is null ||
                (playerUid is not null && (playerUid != read.PlayerUid || allianceId != read.AllianceId)))
                throw new BridgeCommandException("TREASURE_STATE_UNAVAILABLE", "Treasure inspection identity changed");
            playerUid = read.PlayerUid;
            allianceId = read.AllianceId;
            var expected = batch.Select(row => row.GetProperty("uuid").ToString()).ToHashSet(StringComparer.Ordinal);
            if (read.States.Count != batch.Length || expected.Count != batch.Length)
                throw new BridgeCommandException("TREASURE_STATE_UNAVAILABLE", "Treasure inspection batch is incomplete");
            foreach (JsonElement state in read.States)
            {
                if (state.ValueKind != JsonValueKind.Object ||
                    !state.TryGetProperty("uuid", out JsonElement uuid) || !expected.Remove(uuid.ToString()))
                    throw new BridgeCommandException("TREASURE_STATE_UNAVAILABLE", "Treasure inspection returned a foreign or duplicate target");
                states.Add(state.Clone());
            }
            if (expected.Count != 0)
                throw new BridgeCommandException("TREASURE_STATE_UNAVAILABLE", "Treasure inspection omitted a target");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (playerUid is not null)
            PersistTreasureStates(serverId, new TreasureInspectionResult(playerUid, allianceId!, states));
        return JsonSerializer.SerializeToElement(new
        {
            serverId, playerUid, allianceId, states,
            inspectedCount = states.Count, candidateCount = candidates.Count,
            mode = "inspection_only", claimExecutionAvailable = false,
            dailyRemaining = (int?)null, nextResetAt = (long?)null,
            cooldownUntil = (long?)null,
        });
    }

    /// <summary>OWN_DESIGN: dry-run claim planning, with zero game claim requests.</summary>
    public async ValueTask<JsonElement> PreviewTreasureClaimAsync(
        int serverId, string claimScope, bool prioritizeLuckySlots = true,
        string targetUuid = "", CancellationToken cancellationToken = default)
    {
        MapStore.ValidateServerId(serverId);
        if (claimScope is not ("single" or "boxes" or "season") ||
            (claimScope == "single" && string.IsNullOrWhiteSpace(targetUuid)))
            throw new BridgeCommandException("INVALID_TREASURE_CLAIM_SCOPE", "Treasure claim scope is invalid");
        JsonElement snapshot = await TreasureClaimStatusAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.GetProperty("serverId").GetInt32() != serverId)
            throw new BridgeCommandException("SERVER_MISMATCH", "Treasure claim preview server changed");
        IReadOnlyList<JsonElement> records = store.ReadTreasureClaimCandidates(serverId, nowMilliseconds());
        var states = snapshot.GetProperty("states").EnumerateArray()
            .ToDictionary(row => row.GetProperty("uuid").ToString(), row => row, StringComparer.Ordinal);
        JsonElement[] selected = records.Where(row => claimScope switch
        {
            "single" => string.Equals(row.GetProperty("uuid").ToString(), targetUuid, StringComparison.Ordinal),
            "boxes" => ReadIntegerLike(row, "suppliesType") is 1 or 3 or 4,
            _ => ReadIntegerLike(row, "suppliesType") == 0,
        }).ToArray();
        if (claimScope == "single" && selected.Length != 1)
            throw new BridgeCommandException("NOT_FOUND", "Treasure is missing or expired");
        var ordered = selected.OrderBy(row => prioritizeLuckySlots &&
            ReadIntegerLike(states[row.GetProperty("uuid").ToString()], "claimPriority") == 0 &&
            states[row.GetProperty("uuid").ToString()].TryGetProperty("claimPriority", out _) ? 0 : 1)
            .ThenBy(row => ReadIntegerLike(row, "expireTime") is > 0 ? ReadIntegerLike(row, "expireTime") : long.MaxValue)
            .ThenBy(row => row.GetProperty("uuid").ToString(), StringComparer.Ordinal)
            .Select(row =>
            {
                string uuid = row.GetProperty("uuid").ToString();
                JsonElement status = states[uuid];
                string player = ReadString(status, "playerClaimState");
                string world = ReadString(status, "worldClaimState");
                string block = ReadString(status, "claimBlockReason");
                bool eligible = player == "unclaimed" && world == "claimable" && string.IsNullOrWhiteSpace(block);
                return new { uuid, eligible, playerClaimState = player, worldClaimState = world,
                    claimBlockReason = block, luckyPriorityKnown = status.TryGetProperty("claimPriority", out _),
                    execute = false };
            }).ToArray();
        return JsonSerializer.SerializeToElement(new
        {
            serverId, claimScope, prioritizeLuckySlots,
            inspectedCount = snapshot.GetProperty("inspectedCount").GetInt32(),
            candidateCount = ordered.Length, eligibleCount = ordered.Count(row => row.eligible),
            pendingCount = ordered.Count(row => !row.eligible),
            mode = "preview_only", claimExecutionAvailable = false, rows = ordered,
        });
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

    public ValueTask<IReadOnlyList<JsonElement>> ScheduleDispatchPlunderAsync(
        IReadOnlyList<JsonElement> rows,
        CancellationToken cancellationToken = default)
    {
        DispatchScheduleRow[] normalized = ValidateDispatchScheduleRows(rows);
        if (normalized.Any(row => row.TaskKind == "ghost"))
            throw new BridgeCommandException("GHOST_PLUNDER_PROVIDER_UNAVAILABLE",
                "Ghost tasks can be prepared, but execution requires a verified v24 Ghost request and terminal result");
        var scheduled = new List<JsonElement>(normalized.Length);
        long now = nowMilliseconds();
        foreach (DispatchScheduleRow item in normalized)
        {
            JsonElement? stored = store.ScheduleDispatchPlunderRow(
                item.ServerId, item.Uuid, item.Row.GetRawText(), item.CompletionTime,
                item.PlunderAt, item.ExpireAt, now);
            if (stored is null)
                throw new BridgeCommandException("MAP_DATA_ERROR", "scheduled plunder job is missing");
            scheduled.Add(stored.Value);
        }
        DispatchPlunderChanged?.Invoke(this, EventArgs.Empty);
        return ValueTask.FromResult<IReadOnlyList<JsonElement>>(scheduled);
    }

    /// <summary>OWN_DESIGN: validate and hydrate Ghost plans without queuing an action.</summary>
    public async ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderAsync(
        IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default)
    {
        DispatchScheduleRow[] selected = ValidateDispatchScheduleRows(rows);
        if (selected.Any(row => row.TaskKind != "ghost"))
            throw new BridgeCommandException("INVALID_REQUEST", "Ghost preparation requires only Ghost rows");
        if (selected.Select(row => (row.ServerId, row.Uuid)).Distinct().Count() != selected.Length)
            throw new BridgeCommandException("INVALID_REQUEST", "Ghost preparation contains duplicate tasks");
        using OperationLease lease = EnterGameOperation();
        IReadOnlyList<JsonElement> prepared = await ProtectedAsync(
            provider.PrepareGhostPlunderTasksAsync(selected.Select(row => row.Row).ToArray(), cancellationToken),
            cancellationToken).ConfigureAwait(false);
        if (prepared.Count != selected.Length)
            throw new BridgeCommandException("GAME_PROVIDER_ERROR", "Ghost preparation returned an incomplete set");
        for (int index = 0; index < prepared.Count; index++)
        {
            DispatchScheduleRow normalized = NormalizeDispatchScheduleRow(prepared[index], "ghost");
            if (normalized.ServerId != selected[index].ServerId ||
                !string.Equals(normalized.Uuid, selected[index].Uuid, StringComparison.Ordinal))
                throw new BridgeCommandException("GAME_PROVIDER_ERROR", "Ghost preparation changed the selected task identity");
        }
        return prepared.Select(row => row.Clone()).ToArray();
    }

    public void CancelDispatchPlunder(long serverId, string taskUuid)
    {
        if (serverId <= 0 || string.IsNullOrEmpty(taskUuid))
            throw new BridgeCommandException("INVALID_REQUEST", "server ID and task UUID are required");
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
