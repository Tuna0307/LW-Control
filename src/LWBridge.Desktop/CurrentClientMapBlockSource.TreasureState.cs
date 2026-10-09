using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record CurrentClientTreasureInspectionRecord(
    int ServerId,
    int PointIndex,
    string Uuid,
    int TreasureType,
    int SuppliesType,
    string AllianceId,
    string ViewerUid,
    string ViewerAllianceId,
    bool? ViewerHasReward,
    bool? ViewerIsWorking,
    bool? Complete,
    long? ExpireTime,
    long? StartTime,
    long? CompletionTime,
    int? RewardedCount,
    int? DiggingCount,
    int? RewardMax,
    int? RemainingBoxes,
    long? CreateTime,
    string DiscovererAllianceId,
    string DiscovererUid,
    int? WorkState,
    int? UserCount);

internal sealed record CurrentClientTreasureInspectionResult(
    string PlayerUid,
    string AllianceId,
    IReadOnlyList<JsonElement> States);

internal sealed partial class CurrentClientMapBlockSource
{
    private const int TreasureInspectionBatchLimit = 100;
    private static readonly TimeSpan TreasureInspectionTimeout = TimeSpan.FromSeconds(8);
    private readonly SemaphoreSlim treasureInspectionGate = new(1, 1);

    public async Task<CurrentClientTreasureInspectionResult> InspectTreasureStatesAsync(
        int serverId,
        IReadOnlyList<CurrentClientTreasureInspectionRecord> records,
        bool refreshDetails,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (serverId is < 1 or > 99_999)
            throw new BridgeCommandException("INVALID_SERVER_ID", "server ID is required");
        if (records.Count > TreasureInspectionBatchLimit)
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                $"Treasure inspection is limited to {TreasureInspectionBatchLimit} records per internal batch.");
        foreach (CurrentClientTreasureInspectionRecord record in records)
            ValidateTreasureInspectionRecord(serverId, record);

        await treasureInspectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            OverviewMapScanSession session = RequireReadySession();
            if (waitForHealthySession is { } waitForHealthy)
            {
                await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
                RequireSameSession(session);
            }

            Exception? lastError = null;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    return await ProbeTreasureStatesAsync(
                            session,
                            serverId,
                            records,
                            refreshDetails,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception error) when (
                    error is TimeoutException ||
                    error is BridgeCommandException bridge &&
                    bridge.Code == "GAME_CONNECTION_UNAVAILABLE")
                {
                    lastError = error;
                    if (attempt >= 2) break;
                    if (waitForHealthySession is { } retryHealthy)
                    {
                        await retryHealthy(session, cancellationToken).ConfigureAwait(false);
                        RequireSameSession(session);
                    }
                    await DelayAsync(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
                }
            }

            throw lastError ?? new InvalidOperationException(
                "Treasure state inspection failed without an error.");
        }
        finally
        {
            treasureInspectionGate.Release();
        }
    }

    private async Task<CurrentClientTreasureInspectionResult> ProbeTreasureStatesAsync(
        OverviewMapScanSession session,
        int serverId,
        IReadOnlyList<CurrentClientTreasureInspectionRecord> records,
        bool refreshDetails,
        CancellationToken cancellationToken)
    {
        string requestId = "treasure" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "treasure-state.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "treasure-state-result.json");
        DateTimeOffset startedAt = Now();

        var lines = new List<string>(12 + records.Count * 19)
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"launchSessionId={session.SessionId}",
            $"profileId={session.ProfileId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"serverId={serverId.ToString(CultureInfo.InvariantCulture)}",
            $"refreshDetails={(refreshDetails ? "true" : "false")}",
            $"recordCount={records.Count.ToString(CultureInfo.InvariantCulture)}",
        };
        for (int index = 0; index < records.Count; index++)
        {
            int ordinal = index + 1;
            CurrentClientTreasureInspectionRecord record = records[index];
            string prefix = "record" + ordinal.ToString(CultureInfo.InvariantCulture);
            lines.Add($"{prefix}PointIndex={record.PointIndex.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}Uuid={record.Uuid}");
            lines.Add($"{prefix}TreasureType={record.TreasureType.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}SuppliesType={record.SuppliesType.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}AllianceId={record.AllianceId}");
            lines.Add($"{prefix}ViewerUid={record.ViewerUid}");
            lines.Add($"{prefix}ViewerAllianceId={record.ViewerAllianceId}");
            lines.Add($"{prefix}ViewerHasReward={FormatOptionalBoolean(record.ViewerHasReward)}");
            lines.Add($"{prefix}ViewerIsWorking={FormatOptionalBoolean(record.ViewerIsWorking)}");
            lines.Add($"{prefix}Complete={FormatOptionalBoolean(record.Complete)}");
            lines.Add($"{prefix}ExpireTime={FormatOptionalInteger(record.ExpireTime)}");
            lines.Add($"{prefix}StartTime={FormatOptionalInteger(record.StartTime)}");
            lines.Add($"{prefix}CompletionTime={FormatOptionalInteger(record.CompletionTime)}");
            lines.Add($"{prefix}RewardedCount={FormatOptionalInteger(record.RewardedCount)}");
            lines.Add($"{prefix}DiggingCount={FormatOptionalInteger(record.DiggingCount)}");
            lines.Add($"{prefix}RewardMax={FormatOptionalInteger(record.RewardMax)}");
            lines.Add($"{prefix}RemainingBoxes={FormatOptionalInteger(record.RemainingBoxes)}");
            lines.Add($"{prefix}CreateTime={FormatOptionalInteger(record.CreateTime)}");
            lines.Add($"{prefix}DiscovererAllianceId={record.DiscovererAllianceId}");
            lines.Add($"{prefix}DiscovererUid={record.DiscovererUid}");
            lines.Add($"{prefix}WorkState={FormatOptionalInteger(record.WorkState)}");
            lines.Add($"{prefix}UserCount={FormatOptionalInteger(record.UserCount)}");
        }
        lines.Add(string.Empty);

        await WriteCommandAsync(
                commandPath,
                string.Join('\n', lines),
                cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset deadline = startedAt + TreasureInspectionTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                return ValidateTreasureInspectionResult(
                    root.Value,
                    requestId,
                    startedAt,
                    session,
                    serverId,
                    records);
            }

            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The current-client Treasure state inspector did not return a correlated fresh result.");
    }

    private CurrentClientTreasureInspectionResult ValidateTreasureInspectionResult(
        JsonElement root,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        int serverId,
        IReadOnlyList<CurrentClientTreasureInspectionRecord> records)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", serverId))
        {
            throw new InvalidDataException(
                "Treasure state result did not match the active owned game session and request.");
        }

        RequireFreshCaptureTime(root, startedAt);
        string state = ReadOptionalString(root, "state") ?? string.Empty;
        if (state == "failed")
        {
            string error = ReadOptionalString(root, "error") ?? "treasure_state_inspection_failed";
            if (error.StartsWith("overview_", StringComparison.Ordinal) ||
                error.EndsWith("_identity_mismatch", StringComparison.Ordinal) ||
                error is "world_unavailable" or "player_identity_unavailable")
            {
                throw new BridgeCommandException(
                    "GAME_CONNECTION_UNAVAILABLE",
                    "game connection unavailable",
                    error);
            }
            if (error == "server_mismatch")
            {
                throw new BridgeCommandException(
                    "SERVER_MISMATCH",
                    "current game server does not match map data server");
            }
            throw new BridgeCommandException(
                "TREASURE_STATE_UNAVAILABLE",
                "Treasure state is unavailable.",
                error);
        }
        if (state != "proven")
            throw new InvalidDataException(
                "Treasure state result did not contain a supported terminal state.");

        string? playerUid = ReadOptionalString(root, "playerUid");
        string? allianceId = ReadOptionalString(root, "allianceId");
        if (string.IsNullOrEmpty(playerUid) || allianceId is null)
            throw new InvalidDataException("Treasure state result omitted player identity.");
        if (!root.TryGetProperty("states", out JsonElement statesValue) ||
            statesValue.ValueKind != JsonValueKind.Array ||
            statesValue.GetArrayLength() != records.Count)
        {
            throw new InvalidDataException(
                "Treasure state result did not return exactly one state per requested record.");
        }

        var expected = records.ToDictionary(record => record.Uuid, StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        var states = new List<JsonElement>(records.Count);
        foreach (JsonElement item in statesValue.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Treasure state item is not an object.");
            string? uuid = ReadOptionalString(item, "uuid");
            if (string.IsNullOrEmpty(uuid) || !expected.ContainsKey(uuid) || !observed.Add(uuid))
                throw new InvalidDataException(
                    "Treasure state item did not match a unique requested Treasure UUID.");

            ValidateRecoveredTreasureStateItem(item);
            states.Add(item.Clone());
        }

        RequireSameSession(session);
        return new CurrentClientTreasureInspectionResult(playerUid, allianceId, states);
    }

    private static void ValidateRecoveredTreasureStateItem(JsonElement item)
    {
        if (item.TryGetProperty("worldClaimState", out JsonElement world))
        {
            if (world.ValueKind != JsonValueKind.String ||
                world.GetString() is not ("charging" or "claimable" or "depleted" or "expired" or "unknown"))
                throw new InvalidDataException("Treasure worldClaimState is outside the recovered UI vocabulary.");
        }
        if (item.TryGetProperty("playerClaimState", out JsonElement player))
        {
            if (player.ValueKind != JsonValueKind.String ||
                player.GetString() is not ("unclaimed" or "digging" or "claimed" or "unknown"))
                throw new InvalidDataException("Treasure playerClaimState is outside the read-only recovered vocabulary.");
        }
        if (item.TryGetProperty("claimBlockReason", out JsonElement block) &&
            block.ValueKind is not JsonValueKind.Null)
        {
            if (block.ValueKind != JsonValueKind.String ||
                block.GetString() is not ("other_alliance" or "no_scout" or "no_squad" or "squad_reserved"))
                throw new InvalidDataException("Treasure claimBlockReason is outside the recovered UI vocabulary.");
        }

        ValidateOptionalNonNegativeInteger(item, "rewardedCount");
        ValidateOptionalNonNegativeInteger(item, "diggingCount");
        ValidateOptionalNonNegativeInteger(item, "remainingBoxes");
        ValidateOptionalNonNegativeInteger(item, "expireTime");
        if (item.TryGetProperty("chargePercent", out JsonElement charge) &&
            charge.ValueKind is not JsonValueKind.Null)
        {
            if (!charge.TryGetDouble(out double value) || !double.IsFinite(value) || value < 0 || value > 1.000001)
                throw new InvalidDataException("Treasure chargePercent is invalid.");
        }
        // claimPriority is deliberately not accepted from this reconstruction.
        // R7-068 keeps missing priority at the recovered default 1 until the
        // original/current client predictor is source-proven.
        if (item.TryGetProperty("claimPriority", out _))
            throw new InvalidDataException("Treasure inspector must not synthesize claimPriority.");
    }

    private static void ValidateOptionalNonNegativeInteger(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return;
        if (!value.TryGetInt64(out long parsed) || parsed < 0)
            throw new InvalidDataException($"Treasure {name} is invalid.");
    }

    private static void ValidateTreasureInspectionRecord(
        int serverId,
        CurrentClientTreasureInspectionRecord record)
    {
        if (record.ServerId != serverId ||
            record.PointIndex <= 0 ||
            string.IsNullOrWhiteSpace(record.Uuid) ||
            record.Uuid.Length > 128 ||
            record.Uuid.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            record.AllianceId.Length > 128 ||
            record.AllianceId.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            record.ViewerUid.Length > 128 ||
            record.ViewerUid.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            record.ViewerAllianceId.Length > 128 ||
            record.ViewerAllianceId.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            record.DiscovererAllianceId.Length > 128 ||
            record.DiscovererAllianceId.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            record.DiscovererUid.Length > 128 ||
            record.DiscovererUid.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            !((record.TreasureType > 0 && record.SuppliesType == 0) ||
              (record.TreasureType == 0 && record.SuppliesType > 0)))
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "Treasure state record identity is invalid.");
        }
    }

    private static string FormatOptionalBoolean(bool? value) =>
        value.HasValue ? (value.Value ? "true" : "false") : string.Empty;

    private static string FormatOptionalInteger(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatOptionalInteger(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
