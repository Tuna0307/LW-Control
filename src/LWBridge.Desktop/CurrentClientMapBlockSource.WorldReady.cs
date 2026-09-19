using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record CurrentClientMapContext(
    int ServerId,
    long WorldId,
    int TileWidth,
    int TileHeight,
    int? PlayerTileX = null,
    int? PlayerTileY = null,
    string? LaunchSessionId = null);


internal sealed record CurrentClientCoordinateJumpResult(int ServerId, int X, int Y);

internal sealed record CurrentClientMarchFollowResult(int ServerId, long MarchUuid);

internal sealed record CurrentClientServerJumpResult(int PreviousServerId, bool Changed);

internal sealed record CurrentClientTruckQuickRobResult(
    int ServerId,
    long MarchUuid,
    long TrainUuid,
    bool BattleWon,
    int RewardCount,
    JsonElement PlunderRewards,
    bool RewardNormalizationComplete,
    int? DailyRobCount);

internal sealed partial class CurrentClientMapBlockSource
{
    internal async Task<CurrentClientMapContext> GetCurrentContextAsync(CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }
        CurrentClientMapContext context = await EnsureWorldReadyAsync(session, cancellationToken).ConfigureAwait(false);
        RequireSameSession(session);
        return context;
    }

    internal async Task<CurrentClientCoordinateJumpResult> JumpToCoordinateAsync(
        int requestedServerId, int x, int y, CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }
        CurrentClientMapContext context = await EnsureWorldReadyAsync(session, cancellationToken).ConfigureAwait(false);
        RequireSameSession(session);
        if (context.ServerId != requestedServerId)
            throw new BridgeCommandException("STALE_MAP_SERVER", "Map row server does not match the current live server.");
        if (x < 0 || x >= context.TileWidth || y < 0 || y >= context.TileHeight)
            throw new BridgeCommandException("INVALID_MAP_COORDINATE", "Map coordinates are outside the current live world.");
        _ = await NavigateCoreAsync(session, context.ServerId, context.WorldId, x, y, cancellationToken).ConfigureAwait(false);
        RequireSameSession(session);
        return new CurrentClientCoordinateJumpResult(context.ServerId, x, y);
    }

    internal async Task<CurrentClientMarchFollowResult> FollowMarchAsync(
        int requestedServerId,
        long marchUuid,
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "follow" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "march-follow.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "march-follow-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={requestedServerId.ToString(CultureInfo.InvariantCulture)}",
            $"marchUuid={marchUuid.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        DateTimeOffset deadline = Now() + NavigationTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                CurrentClientMarchFollowResult result =
                    ValidateMarchFollowResult(root.Value, session, requestedServerId, marchUuid);
                RequireSameSession(session);
                return result;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The current-client march Follow did not return a correlated result.");
    }

    private static CurrentClientMarchFollowResult ValidateMarchFollowResult(
        JsonElement root,
        OverviewMapScanSession session,
        int requestedServerId,
        long marchUuid)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", requestedServerId) ||
            !MatchesLong(root, "marchUuid", marchUuid))
        {
            throw new InvalidDataException(
                "March Follow result did not match the active owned game session or requested march.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        if (state == "proven")
        {
            int currentServerId = RequirePositiveInt(root, "currentServerId");
            if (currentServerId != requestedServerId)
                throw new InvalidDataException("March Follow did not prove the requested live server.");
            if (!MatchesString(
                    root,
                    "method",
                    "GoToUtil.JumpToMarchByUuid"))
                throw new InvalidDataException("March Follow did not prove the supported current-client route.");
            return new CurrentClientMarchFollowResult(currentServerId, marchUuid);
        }

        if (state == "failed")
        {
            string error = ReadOptionalString(root, "error") ?? "march_follow_failed";
            if (string.Equals(error, "current_server_id_unavailable", StringComparison.Ordinal))
                throw new BridgeCommandException("SERVER_UNAVAILABLE", "current server id unavailable");
            throw new BridgeCommandException("MARCH_FOLLOW_FAILED", error);
        }

        throw new InvalidDataException("March Follow result did not contain a supported terminal state.");
    }


    internal async Task<CurrentClientTruckQuickRobResult> ExecuteTruckQuickRobAsync(
        int requestedServerId,
        long marchUuid,
        long trainUuid,
        CancellationToken cancellationToken)
    {
        if (requestedServerId is < 1 or > 99999 || marchUuid <= 0 || trainUuid <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedServerId), "Truck quick-rob target identity is invalid.");

        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "truckrob" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "truck-quick-rob-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={requestedServerId.ToString(CultureInfo.InvariantCulture)}",
            $"marchUuid={marchUuid.ToString(CultureInfo.InvariantCulture)}",
            $"trainUuid={trainUuid.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        // IMPLEMENTATION POLICY LWB-R7-042: the v19 bridge has a 15 s pre-send
        // setup window and 30 s post-send response window. Keep those inside a
        // host envelope. A host-envelope timeout is state-unknown and MUST NOT
        // be treated as retryable because train.attack may already have reached
        // the server.
        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(50);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                CurrentClientTruckQuickRobResult result = ValidateTruckQuickRobResult(
                    root.Value, session, requestedServerId, marchUuid, trainUuid);
                RequireSameSession(session);
                return result;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new BridgeCommandException(
            "TRUCK_PLUNDER_STATE_UNKNOWN",
            "truck plunder execution state is unknown",
            new
            {
                ambiguous = true,
                serverId = requestedServerId,
                marchUuid = marchUuid.ToString(CultureInfo.InvariantCulture),
                trainUuid = trainUuid.ToString(CultureInfo.InvariantCulture),
            });
    }

    private static CurrentClientTruckQuickRobResult ValidateTruckQuickRobResult(
        JsonElement root,
        OverviewMapScanSession session,
        int requestedServerId,
        long marchUuid,
        long trainUuid)
    {
        string marchText = marchUuid.ToString(CultureInfo.InvariantCulture);
        string trainText = trainUuid.ToString(CultureInfo.InvariantCulture);
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", requestedServerId) ||
            !MatchesString(root, "marchUuid", marchText) ||
            !MatchesString(root, "trainUuid", trainText))
        {
            throw new InvalidDataException(
                "Truck quick-rob result did not match the active owned game session or requested target.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        bool requestSent = MatchesBool(root, "requestSent", true);
        if (state == "proven")
        {
            int currentServerId = RequirePositiveInt(root, "currentServerId");
            if (currentServerId != requestedServerId || !requestSent)
                throw new InvalidDataException("Truck quick-rob did not prove the requested live server and sent request.");
            if (!MatchesString(
                    root,
                    "method",
                    "RailwayUtil.ClickAttackTrain+LWMyStationDataManager.TryAttackTrain"))
            {
                throw new InvalidDataException("Truck quick-rob did not prove the supported current-v19 route.");
            }

            if (!root.TryGetProperty("battleWon", out JsonElement battleWonValue) ||
                battleWonValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("Truck quick-rob result did not contain an authoritative battle outcome.");
            }
            if (!root.TryGetProperty("rewardCount", out JsonElement rewardCountValue) ||
                !rewardCountValue.TryGetInt32(out int rewardCount) || rewardCount < 0)
            {
                throw new InvalidDataException("Truck quick-rob result reward count is invalid.");
            }
            if (!root.TryGetProperty("plunderRewards", out JsonElement plunderRewards) ||
                plunderRewards.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Truck quick-rob result plunderRewards must be an array.");
            }
            var rewardKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement reward in plunderRewards.EnumerateArray())
            {
                if (reward.ValueKind != JsonValueKind.Object ||
                    !reward.TryGetProperty("key", out JsonElement keyValue) ||
                    keyValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(keyValue.GetString()) ||
                    !rewardKeys.Add(keyValue.GetString()!) ||
                    !reward.TryGetProperty("name", out JsonElement nameValue) ||
                    nameValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(nameValue.GetString()) ||
                    !reward.TryGetProperty("iconPath", out JsonElement iconValue) ||
                    iconValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(iconValue.GetString()) ||
                    !reward.TryGetProperty("count", out JsonElement countValue) ||
                    !countValue.TryGetDouble(out double count) || !double.IsFinite(count) || count <= 0 ||
                    !reward.TryGetProperty("rewardType", out JsonElement typeValue) ||
                    !typeValue.TryGetInt32(out int rewardType) || rewardType <= 0 ||
                    !reward.TryGetProperty("itemId", out JsonElement itemValue) ||
                    !itemValue.TryGetInt64(out long itemId) || itemId <= 0)
                {
                    throw new InvalidDataException("Truck quick-rob result contains an invalid normalized plunder reward.");
                }
            }
            if (!root.TryGetProperty("rewardNormalizationComplete", out JsonElement normalizationValue) ||
                normalizationValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidDataException("Truck quick-rob result rewardNormalizationComplete must be a boolean.");
            }
            int? dailyRobCount = null;
            if (root.TryGetProperty("dailyRobCount", out JsonElement dailyRobValue) &&
                dailyRobValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                if (!dailyRobValue.TryGetInt32(out int parsedDailyRobCount) || parsedDailyRobCount < 0)
                    throw new InvalidDataException("Truck quick-rob result dailyRobCount is invalid.");
                dailyRobCount = parsedDailyRobCount;
            }

            return new CurrentClientTruckQuickRobResult(
                currentServerId,
                marchUuid,
                trainUuid,
                battleWonValue.GetBoolean(),
                rewardCount,
                plunderRewards.Clone(),
                normalizationValue.GetBoolean(),
                dailyRobCount);
        }

        string error = ReadOptionalString(root, "error") ?? "truck_quick_rob_failed";
        if (state == "ambiguous")
        {
            if (!requestSent)
                throw new InvalidDataException("Ambiguous Truck quick-rob result did not prove that train.attack was sent.");

            object details = new
            {
                ambiguous = true,
                requestSent = true,
                serverId = requestedServerId,
                marchUuid = marchText,
                trainUuid = trainText,
                error,
            };
            if (string.Equals(error, "server_response_timeout", StringComparison.Ordinal))
                throw new BridgeCommandException(
                    "TRUCK_PLUNDER_RESPONSE_TIMEOUT",
                    "server response timeout",
                    details);
            throw new BridgeCommandException(
                "TRUCK_PLUNDER_RESULT_AMBIGUOUS",
                "truck plunder result is ambiguous",
                details);
        }

        if (state == "failed")
        {
            if (requestSent && string.Equals(error, "train_attack_rejected", StringComparison.Ordinal))
            {
                throw new BridgeCommandException(
                    "TRUCK_PLUNDER_SERVER_REJECTED",
                    "train attack rejected by the game",
                    new
                    {
                        requestSent = true,
                        serverId = requestedServerId,
                        marchUuid = marchText,
                        trainUuid = trainText,
                    });
            }

            throw new BridgeCommandException(
                "TRUCK_QUICK_ROB_FAILED",
                error,
                new
                {
                    requestSent,
                    serverId = requestedServerId,
                    marchUuid = marchText,
                    trainUuid = trainText,
                });
        }

        throw new InvalidDataException("Truck quick-rob result did not contain a supported terminal state.");
    }

    internal async Task<CurrentClientServerJumpResult> JumpToServerAsync(
        int targetServerId,
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "server" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "server-jump.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "server-jump-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            $"serverId={targetServerId.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);

        // IMPLEMENTATION POLICY: the original public timeout code/message are recovered,
        // but the exact original duration is not yet proven. Keep the bridge-side 15 s
        // deadline inside an 18 s host envelope.
        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(18);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                CurrentClientServerJumpResult result =
                    ValidateServerJumpResult(root.Value, session, targetServerId);
                RequireSameSession(session);
                return result;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new BridgeCommandException(
            "SERVER_JUMP_TIMEOUT",
            "the game did not switch to the target server");
    }

    private static CurrentClientServerJumpResult ValidateServerJumpResult(
        JsonElement root,
        OverviewMapScanSession session,
        int targetServerId)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", targetServerId))
        {
            throw new InvalidDataException(
                "Server-jump result did not match the active owned game session or target server.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        if (state == "proven")
        {
            int previousServerId = RequirePositiveInt(root, "previousServerId");
            int currentServerId = RequirePositiveInt(root, "currentServerId");
            if (currentServerId != targetServerId)
                throw new InvalidDataException("Server-jump result did not prove the requested target server.");
            bool expectedChanged = previousServerId != targetServerId;
            if (!MatchesBool(root, "changed", expectedChanged))
                throw new InvalidDataException("Server-jump result changed flag did not match the proven server transition.");
            return new CurrentClientServerJumpResult(previousServerId, expectedChanged);
        }

        if (state == "failed")
        {
            string error = ReadOptionalString(root, "error") ?? "server_jump_failed";
            if (string.Equals(error, "server_jump_timeout", StringComparison.Ordinal))
                throw new BridgeCommandException(
                    "SERVER_JUMP_TIMEOUT",
                    "the game did not switch to the target server");
            throw new BridgeCommandException("SERVER_JUMP_FAILED", error);
        }

        throw new InvalidDataException("Server-jump result did not contain a supported terminal state.");
    }

    private async Task<CurrentClientMapContext> EnsureWorldReadyAsync(
        OverviewMapScanSession session,
        CancellationToken cancellationToken)
    {
        string requestId = "world" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "world-ready.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "world-ready-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"bridgeVersion={OverviewBridgeVersion}",
            $"profileId={session.ProfileId}",
            $"sessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"requestId={requestId}",
            string.Empty,
        });
        await WriteCommandAsync(requestPath, command, cancellationToken).ConfigureAwait(false);
        TimeSpan allowance = TimeSpan.FromMilliseconds(
            MapScanStartOwnership.EnterWorldMapRequestTimeoutMs +
            MapScanStartOwnership.WorldMapReadyDeadlineMs);
        DateTimeOffset deadline = Now() + allowance;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                return ValidateWorldReadyResult(root.Value, session);
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }

        throw new BridgeCommandException(
            MapScanStartOwnership.WorldMapFailureCode,
            MapScanStartOwnership.WorldMapFailureMessage);
    }

    private static CurrentClientMapContext ValidateWorldReadyResult(
        JsonElement root,
        OverviewMapScanSession session)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", OverviewBridgeVersion) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "sessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid))
        {
            throw new InvalidDataException(
                "World-readiness result did not match the active owned game session.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        if (state == "proven")
        {
            int serverId = RequirePositiveInt(root, "serverId");
            if (!root.TryGetProperty("worldId", out JsonElement worldValue) ||
                !worldValue.TryGetInt64(out long worldId) || worldId < 0)
                throw new InvalidDataException("World-readiness result worldId is missing or invalid.");
            int tileWidth = RequirePositiveInt(root, "tileWidth");
            int tileHeight = RequirePositiveInt(root, "tileHeight");
            int? playerTileX = null;
            int? playerTileY = null;
            bool hasPlayerTileX = root.TryGetProperty("playerTileX", out JsonElement playerXValue);
            bool hasPlayerTileY = root.TryGetProperty("playerTileY", out JsonElement playerYValue);
            if (hasPlayerTileX != hasPlayerTileY)
                throw new InvalidDataException("World-readiness result player tile is incomplete.");
            if (hasPlayerTileX)
            {
                if (!playerXValue.TryGetInt32(out int parsedX) || parsedX < 0 || parsedX >= tileWidth ||
                    !playerYValue.TryGetInt32(out int parsedY) || parsedY < 0 || parsedY >= tileHeight)
                    throw new InvalidDataException("World-readiness result player tile is outside the live map dimensions.");
                playerTileX = parsedX;
                playerTileY = parsedY;
            }
            return new CurrentClientMapContext(serverId, worldId, tileWidth, tileHeight, playerTileX, playerTileY, session.SessionId);
        }
        if (state == "failed")
            throw new BridgeCommandException(
                MapScanStartOwnership.WorldMapFailureCode,
                MapScanStartOwnership.WorldMapFailureMessage);
        throw new InvalidDataException(
            "World-readiness result did not contain a supported terminal state.");
    }

    private async Task<NavigationObservation> NavigateAsync(
        OverviewMapScanSession session,
        int serverId,
        long worldId,
        int targetX,
        int targetY,
        CancellationToken cancellationToken)
    {
        await EnsureWorldReadyAsync(session, cancellationToken).ConfigureAwait(false);
        RequireSameSession(session);
        return await NavigateCoreAsync(session, serverId, worldId, targetX, targetY, cancellationToken)
            .ConfigureAwait(false);
    }
}
