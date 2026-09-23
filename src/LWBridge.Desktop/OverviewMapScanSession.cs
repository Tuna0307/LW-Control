using System.Globalization;
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
            ThrowIfServerMaintenance(expected);
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

    private void ThrowIfServerMaintenance(OverviewMapScanSession expected)
    {
        if (testHooks is not null) return;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string? appDataRoot = Directory.GetParent(localAppData)?.FullName;
        if (string.IsNullOrWhiteSpace(appDataRoot)) return;
        string playerLog = Path.Combine(
            appDataRoot,
            "LocalLow",
            "FunFly",
            "Last War-Survival Game",
            "Player.log");

        try
        {
            var info = new FileInfo(playerLog);
            if (!info.Exists) return;
            if (!DateTimeOffset.TryParse(
                    expected.GameStartedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset startedAt))
            {
                return;
            }

            if (info.LastWriteTimeUtc < startedAt.UtcDateTime.AddSeconds(-5))
                return;

            string text;
            using (var stream = new FileStream(
                       playerLog,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                text = reader.ReadToEnd();
            bool hasLoginCode = text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => string.Equals(line.Trim(), "E005", StringComparison.Ordinal));
            if (!hasLoginCode ||
                !text.Contains("Loading error : E109", StringComparison.Ordinal) ||
                !text.Contains("OnLoginError", StringComparison.Ordinal))
            {
                return;
            }

            lock (stateGate)
            {
                if (instanceId == expected.SessionId &&
                    challenge == expected.Challenge &&
                    gamePid == expected.GamePid)
                {
                    connectionState = "maintenance";
                    lastError = "SERVER_MAINTENANCE";
                }
            }

            throw new BridgeCommandException(
                "SERVER_MAINTENANCE",
                "Last War servers are currently under maintenance. Scanning is temporarily unavailable.",
                new { loginCode = "E005", loadingCode = "E109" });
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch
        {
            // Log inspection is advisory; ordinary readiness handling remains the fallback.
        }
    }
}
