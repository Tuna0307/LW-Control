using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record CurrentClientMapStatusContext(
    bool IsInWorld,
    int ServerId,
    int HomeServerId,
    int[] SeasonServerIds,
    int[] TruckMatchServerIds,
    long WorldId,
    int TileWidth,
    int TileHeight,
    int? TileX,
    int? TileY);

internal sealed partial class CurrentClientMapBlockSource
{
    internal async Task<CurrentClientMapStatusContext> GetMapStatusContextAsync(
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        string requestId = "state" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string requestPath = Path.Combine(overviewRuntimeRoot, "world-state.txt");
        string resultPath = Path.Combine(overviewRuntimeRoot, "world-state-result.json");
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

        DateTimeOffset deadline = Now() + TimeSpan.FromSeconds(5);
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireSameSession(session);
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                CurrentClientMapStatusContext result = ValidateStatusContext(root.Value, session);
                RequireSameSession(session);
                return result;
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client world-state probe did not return a correlated result.");
    }

    private static CurrentClientMapStatusContext ValidateStatusContext(
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
                "World-state result did not match the active owned game session.");
        }

        string state = ReadOptionalString(root, "state") ?? string.Empty;
        if (state != "proven")
        {
            string error = ReadOptionalString(root, "error") ?? "world_state_failed";
            throw new InvalidDataException("World-state probe failed: " + error);
        }

        bool isInWorld = root.TryGetProperty("isInWorld", out JsonElement inWorld) &&
                         inWorld.ValueKind == JsonValueKind.True;
        int serverId = ReadPositiveOrZero(root, "serverId");
        int homeServerId = ReadPositiveOrZero(root, "homeServerId");
        int[] seasonServerIds = ReadServerIdArray(root, "seasonServerIds");
        int[] truckMatchServerIds = ReadServerIdArray(root, "truckMatchServerIds");
        long worldId = 0;
        if (root.TryGetProperty("worldId", out JsonElement worldValue) &&
            worldValue.TryGetInt64(out long parsedWorld) && parsedWorld >= 0)
            worldId = parsedWorld;

        int tileWidth = ReadPositiveOrZero(root, "tileWidth");
        int tileHeight = ReadPositiveOrZero(root, "tileHeight");
        int? tileX = null;
        int? tileY = null;
        if (tileWidth > 0 && tileHeight > 0 &&
            root.TryGetProperty("tileX", out JsonElement tileXValue) &&
            root.TryGetProperty("tileY", out JsonElement tileYValue) &&
            tileXValue.TryGetInt32(out int parsedX) &&
            tileYValue.TryGetInt32(out int parsedY) &&
            parsedX >= 0 && parsedX < tileWidth &&
            parsedY >= 0 && parsedY < tileHeight)
        {
            tileX = parsedX;
            tileY = parsedY;
        }

        return new CurrentClientMapStatusContext(
            isInWorld, serverId, homeServerId, seasonServerIds, truckMatchServerIds,
            worldId, tileWidth, tileHeight, tileX, tileY);
    }

    private static int ReadPositiveOrZero(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) &&
               value.TryGetInt32(out int result) &&
               result > 0
            ? result
            : 0;
    }

    private static int[] ReadServerIdArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Select(item => item.TryGetInt32(out int serverId) ? serverId : 0)
            .Where(serverId => serverId is >= 1 and <= 99999)
            .Distinct()
            .OrderBy(serverId => serverId)
            .ToArray();
    }
}
