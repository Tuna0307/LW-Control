using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class OverviewLifecycleService
{
    internal object? CreateProfileInstanceStatus()
    {
        RefreshExitedOwnership();

        string currentInstanceId;
        string? currentChallenge;
        string currentPhase;
        int? currentPid;
        long currentStartedAt;
        string? currentLastError;
        bool identityConfirmed;

        lock (stateGate)
        {
            if (instanceId is null)
                return null;
            if (instanceStartedAtUnixMilliseconds is not long startedAt)
                throw new InvalidOperationException(
                    "An active Overview instance is missing its native startedAt timestamp.");

            currentInstanceId = instanceId;
            currentChallenge = challenge;
            currentPhase = phase == "stopping" ? "running" : phase;
            currentPid = gamePid;
            currentStartedAt = startedAt;
            currentLastError = lastError;
            identityConfirmed = gamePid is not null;
        }

        bool bridgeConnected =
            bridgeHostState?.IsRouteConnected(currentInstanceId) == true;
        long? lastHeartbeatAt = TryReadProfileInstanceHeartbeatAt(
            currentInstanceId,
            currentChallenge,
            currentPid);
        bool heartbeatFresh =
            lastHeartbeatAt is long heartbeat &&
            heartbeat > 0 &&
            RecoveryNow().ToUnixTimeMilliseconds() - heartbeat < 15_001;

        // Native leaseRequired/leaseActive are multi-entitlement lease state.
        // Retained single-profile mode does not manufacture excluded lease state.
        const bool leaseRequired = false;
        const bool leaseActive = false;

        string nativeConnectionState;
        if (currentLastError is not null)
        {
            nativeConnectionState = "error";
        }
        else if (currentPhase == "starting")
        {
            nativeConnectionState = "starting";
        }
        else if (currentPhase == "recovering")
        {
            nativeConnectionState = "recovering";
        }
        else if (currentPhase == "awaitingIdentity")
        {
            nativeConnectionState = "awaitingLogin";
        }
        else
        {
            nativeConnectionState =
                bridgeConnected && heartbeatFresh && identityConfirmed
                    ? "connected"
                    : "reconnecting";
        }

        return new
        {
            profileId,
            instanceId = currentInstanceId,
            phase = currentPhase,
            pid = currentPid,
            startedAt = currentStartedAt,
            lastError = currentLastError,
            identityConfirmed,
            leaseRequired,
            connectionState = nativeConnectionState,
            bridgeConnected,
            lastHeartbeatAt,
            leaseActive,
        };
    }

    private long? TryReadProfileInstanceHeartbeatAt(
        string currentInstanceId,
        string? currentChallenge,
        int? currentPid)
    {
        if (currentChallenge is null || currentPid is null)
            return null;

        try
        {
            byte[] bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(
                Path.Combine(runtimeRoot, "heartbeat.json"));
            using JsonDocument heartbeat = JsonDocument.Parse(bytes);
            JsonElement root = heartbeat.RootElement;
            if (!MatchesInt(root, "schemaVersion", 1) ||
                !MatchesString(root, "bridgeVersion", BridgeVersion) ||
                !MatchesString(root, "profileId", profileId) ||
                !MatchesString(root, "sessionId", currentInstanceId) ||
                !MatchesString(root, "challenge", currentChallenge) ||
                !MatchesInt(root, "gamePid", currentPid.Value) ||
                !root.TryGetProperty("updatedAt", out JsonElement updated) ||
                !updated.TryGetInt64(out long timestamp) ||
                timestamp <= 0)
            {
                return null;
            }

            return checked(timestamp * 1_000);
        }
        catch
        {
            return null;
        }
    }
}
