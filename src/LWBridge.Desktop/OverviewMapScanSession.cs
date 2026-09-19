using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record OverviewMapScanSession(
    string ProfileId,
    string SessionId,
    string Challenge,
    int GamePid,
    string GamePath,
    string GameStartedAtUtc);

internal sealed partial class OverviewLifecycleService
{
    internal OverviewMapScanSession? GetReadyMapScanSession()
    {
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        if (snapshot is null || snapshot.Phase != "running" || !IsSnapshotReady(snapshot))
            return null;

        return new OverviewMapScanSession(
            profileId,
            snapshot.InstanceId,
            snapshot.Challenge,
            snapshot.GamePid,
            snapshot.GamePath,
            snapshot.GameStartedAtUtc);
    }

    internal int? GetLiveServerId()
    {
        OverviewMapScanSession? session = GetReadyMapScanSession();
        if (session is null) return null;

        try
        {
            byte[] bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(
                Path.Combine(runtimeRoot, "heartbeat.json"));
            using JsonDocument heartbeat = JsonDocument.Parse(bytes);
            JsonElement root = heartbeat.RootElement;
            if (!HeartbeatMatches(
                    root,
                    session.ProfileId,
                    session.SessionId,
                    session.Challenge,
                    session.GamePid,
                    RecoveryNow().ToUnixTimeSeconds()) ||
                !MatchesBool(root, "gameStateObserved", true) ||
                !MatchesBool(root, "gameReady", true) ||
                !MatchesBool(root, "loggedIn", true) ||
                !MatchesBool(root, "connected", true) ||
                !root.TryGetProperty("serverId", out JsonElement server) ||
                !server.TryGetInt32(out int serverId) ||
                serverId <= 0)
            {
                return null;
            }
            return serverId;
        }
        catch
        {
            return null;
        }
    }

    internal bool MatchesOwnedMapScanSession(OverviewMapScanSession expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        return snapshot is not null &&
            snapshot.InstanceId == expected.SessionId &&
            snapshot.Challenge == expected.Challenge &&
            snapshot.GamePid == expected.GamePid &&
            PathEquals(snapshot.GamePath, expected.GamePath) &&
            snapshot.GameStartedAtUtc == expected.GameStartedAtUtc;
    }

    internal async Task WaitForHealthyMapScanSessionAsync(
        OverviewMapScanSession expected,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        long deadline = checked(
            RecoveryClockMilliseconds() +
            (long)OverviewRecoveryPolicy.LoginUnavailableThreshold.TotalMilliseconds);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OwnedSnapshot? snapshot = GetOwnedSnapshot();
            if (snapshot is null ||
                snapshot.InstanceId != expected.SessionId || snapshot.Challenge != expected.Challenge ||
                snapshot.GamePid != expected.GamePid || !PathEquals(snapshot.GamePath, expected.GamePath) ||
                snapshot.GameStartedAtUtc != expected.GameStartedAtUtc)
                throw new BridgeCommandException(
                    MapScanStartOwnership.MissingConnectionErrorCode,
                    MapScanStartOwnership.MissingConnectionErrorMessage);

            OverviewMapScanSession? current = GetReadyMapScanSession();
            if (current == expected)
            {
                RecoveryHeartbeatObservation heartbeat = ReadRecoveryHeartbeat(snapshot);
                if (heartbeat.BridgeOnline && heartbeat.GameStateObserved && heartbeat.GameHealthy)
                    return;
            }
            if (RecoveryClockMilliseconds() >= deadline)
                throw new BridgeCommandException(
                    MapScanStartOwnership.MissingConnectionErrorCode,
                    MapScanStartOwnership.MissingConnectionErrorMessage);

            await RecoveryDelayAsync(RecoveryMonitorCadence, cancellationToken).ConfigureAwait(false);
        }
    }
}
