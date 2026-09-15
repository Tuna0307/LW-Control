using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record CurrentClientMapContext(
    int ServerId,
    long WorldId,
    int TileWidth,
    int TileHeight,
    int? PlayerTileX = null,
    int? PlayerTileY = null);

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
            return new CurrentClientMapContext(serverId, worldId, tileWidth, tileHeight, playerTileX, playerTileY);
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
