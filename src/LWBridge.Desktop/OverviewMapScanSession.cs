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
