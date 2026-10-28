using System.Globalization;
using System.Text.Json;

namespace LWBridge.Map317;

public sealed record CoordinateJumpResult(int ServerId, int X, int Y);
public sealed record MarchFollowResult(int ServerId, string MarchUuid);
public sealed record ServerJumpResult(bool Changed, int PreviousServerId, int ServerId);

public sealed class MapActionControlPlane
{
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

    // Legacy scheduled account actions were retired. Map317 native commands
    // continue to return FEATURE_REMOVED before reaching the action provider.
    public IReadOnlyList<int> GetServerJumpHistory() => store.GetServerJumpHistory();

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

    private static string ReadString(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

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
