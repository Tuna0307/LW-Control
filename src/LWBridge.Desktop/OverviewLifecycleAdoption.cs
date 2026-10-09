using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

// Supported primary-profile adoption in the verified current client. Restores
// the exact prior pipe token after DPAPI integrity protection and exact
// selected-path/PID/creation/session/journal validation. This is not an original
// 0.3.17 on-disk format or original native proof-ticket replacement.
internal sealed partial class OverviewLifecycleService
{
    private string AdoptionRecordPath => Path.Combine(runtimeRoot, OverviewAdoptionRecord.FileName);

    private bool TryReadAdoptionRecord(
        out OverviewAdoptionSnapshot? record, out string failure)
    {
        record = null;
        failure = "RECOVERY_RECORD_NOT_FOUND";
        byte[] payload;
        try
        {
            payload = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(AdoptionRecordPath);
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch { failure = "RECOVERY_RECORD_INVALID"; return false; }

        if (!OverviewAdoptionRecord.TryDeserialize(payload, out record) || record is null)
        {
            failure = "RECOVERY_RECORD_INVALID";
            return false;
        }

        string? selectedRoot;
        lock (stateGate) selectedRoot = gameRoot;
        if (selectedRoot is null ||
            !string.Equals(record.ProfileId, profileId, StringComparison.Ordinal) ||
            !PathEquals(record.GameExecutable,
                Path.GetFullPath(Path.Combine(selectedRoot, "Game", "LastWar.exe"))) ||
            !ProcessMatches(record.Pid, record.GameExecutable, record.ProcessCreatedAt))
        {
            record = null;
            failure = "RECOVERY_PROCESS_MISMATCH";
            return false;
        }
        return true;
    }

    private void CommitAdoptionRecord(
        OverviewStartResult start, string session, string nonce,
        LWBridgeControlPipeLaunchBinding binding)
    {
        var identity = new OverviewAdoptionSnapshot(
            profileId, session, nonce, start.GamePid, start.GamePath,
            start.GameStartedAtUtc, BridgeVersion, binding.PipeToken,
            instanceStartedAtUnixMilliseconds ?? RecoveryNow().ToUnixTimeMilliseconds(),
            LeaseRequired: true);
        byte[] bytes = OverviewAdoptionRecord.Serialize(identity);
        Directory.CreateDirectory(runtimeRoot);
        if (!OverviewRuntimeFileOwnership.TryWrite(
            AdoptionRecordPath, bytes,
            existing => JsonRuntimeFileMatches(existing, session, nonce),
            testHooks?.RuntimeFileBeforeMutation))
            throw new BridgeCommandException(
                "RECOVERY_RECORD_COMMIT_FAILED",
                "The exact-session protected game adoption record could not be committed.");
    }

    private void RemoveAdoptionRecord(string session, string nonce) =>
        TryDeleteOwnedRuntimeFile(AdoptionRecordPath, session, nonce, json: true);

    private async Task<OverviewStartupError?> AdoptOrRepairAsync(
        OverviewRepairSnapshot repair, CancellationToken cancellationToken)
    {
        if (!bridgeControlPipeLaunchBindingEnabled || bridgeHostState is null)
        {
            // Native tests without the production host and legacy recovery
            // mounts are deliberately unaffected.
            return null;
        }
        if (!TryReadAdoptionRecord(out OverviewAdoptionSnapshot? record,
                out string failure) || record is null)
        {
            return new OverviewStartupError(profileId, failure, failure);
        }
        if (repair.Stage != "active_ready_deferred_restore" ||
            !string.Equals(record.InstanceId, repair.SessionId, StringComparison.Ordinal) ||
            record.Pid != repair.GamePid ||
            !PathEquals(record.GameExecutable, repair.GamePath) ||
            !string.Equals(record.ProcessCreatedAt, repair.GameStartedAtUtc, StringComparison.Ordinal))
        {
            return new OverviewStartupError(profileId,
                "RECOVERY_PROCESS_MISMATCH", "RECOVERY_PROCESS_MISMATCH");
        }
        if (!string.Equals(record.BuildId, BridgeVersion, StringComparison.Ordinal))
        {
            // The original restoration classifier at 0x2A0CB7 identifies a
            // restartRequired build mismatch. Only a validated same-identity
            // repair journal can authorize the stop-and-restore, independent
            // of a new-game autoLaunchAll preference.
            var repairResult = (OverviewRestartResult)(await UpdateAndRestartAsync(cancellationToken)
                .ConfigureAwait(false))!;
            return repairResult.Errors.FirstOrDefault();
        }

        lock (stateGate)
        {
            if (closed || profileReplacementPending || phase is "running" or "starting" or "stopping" ||
                gamePid is not null || instanceId is not null)
                return new OverviewStartupError(profileId, "GAME_OPERATION_IN_PROGRESS",
                    "The exact-session adoption is no longer owned by this profile.");
        }

        // Attempt identity: the exact registration this adoption creates. Every
        // retirement side effect is scoped to (session, challenge, registration).
        ulong registrationSerial = 0;
        try
        {
            await EnsureControlPipeHostStartedAsync(gameRoot!).ConfigureAwait(false);
            // Restores the exact token, never a fresh token with which the game
            // could not authenticate. The host's normal HMAC/hello path is
            // unchanged and must actually admit the game before it is ready.
            long admissionStarted = ControlPipeClockMilliseconds();
            long adoptionDeadline = checked(admissionStarted +
                LWBridgeControlPipeRegistry.StartupRegistrationLifetimeMilliseconds);
            registrationSerial = bridgeHostState.RestoreLaunchBinding(record, admissionStarted);
            lock (stateGate)
            {
                if (closed || profileReplacementPending ||
                    phase is "running" or "starting" or "stopping" ||
                    gamePid is not null || instanceId is not null)
                    throw new BridgeCommandException("GAME_OPERATION_CANCELLED",
                        "The profile changed during exact-session adoption.");
                phase = "running";
                connectionState = "connecting";
                instanceId = record.InstanceId;
                challenge = record.Challenge;
                instanceStartedAtUnixMilliseconds = record.StartedAt;
                gamePid = record.Pid;
                gamePath = record.GameExecutable;
                gameStartedAtUtc = record.ProcessCreatedAt;
                readyAtUnix = null;
                lastError = null;
                launcherPid = null;
                recoveryTracked = new RecoveryTrackedGame(
                    record.Pid, record.GameExecutable, record.ProcessCreatedAt);
            }
            if (!StartLeaseTimer(record.InstanceId, record.Challenge))
                throw new BridgeCommandException("GAME_OPERATION_CANCELLED",
                    "The exact-session adoption was retired before its lease timer began.");
            // Do not equate ready.json with the host's authenticated hello.
            // The restored registration's 90s window owns the wait budget.
            bool authenticated = await WaitAuthenticatedRouteAsync(
                record.InstanceId, cancellationToken).ConfigureAwait(false);
            if (!authenticated)
                throw new BridgeCommandException("BRIDGE_DISCONNECTED",
                    "The same-build game was retained, but its authenticated reconnect was not observed.");
            lock (stateGate)
            {
                if (closed || profileReplacementPending ||
                    !string.Equals(instanceId, record.InstanceId, StringComparison.Ordinal))
                    throw new BridgeCommandException("GAME_OPERATION_CANCELLED",
                        "The exact-session adoption was retired before publication.");
                connectionState = "connected";
                readyAtUnix = RecoveryNow().ToUnixTimeSeconds();
            }
            // Authentication can complete before the game has observed this
            // host's new lease. Do not delete that lease based on the first stale
            // heartbeat: wait within the SAME original 90s registration budget.
            if (!await WaitFreshAdoptionHeartbeatAsync(
                    record, adoptionDeadline, cancellationToken).ConfigureAwait(false))
                throw new BridgeCommandException("BRIDGE_DISCONNECTED",
                    "The authenticated route returned, but no fresh exact-session game heartbeat arrived within the bounded registration window.");
            return null;
        }
        catch (OperationCanceledException)
        {
            RetireIncompleteAdoption(record.InstanceId, record.Challenge, registrationSerial);
            return new OverviewStartupError(profileId, "GAME_OPERATION_CANCELLED",
                "The same-build reconnect was cancelled without terminating its game.");
        }
        catch (BridgeCommandException ex)
        {
            RetireIncompleteAdoption(record.InstanceId, record.Challenge, registrationSerial);
            return new OverviewStartupError(profileId, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            RetireIncompleteAdoption(record.InstanceId, record.Challenge, registrationSerial);
            return new OverviewStartupError(profileId, "BRIDGE_HOST_UNAVAILABLE", ex.Message);
        }
    }

    private async Task<bool> WaitFreshAdoptionHeartbeatAsync(
        OverviewAdoptionSnapshot record, long deadlineMilliseconds,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (stateGate)
            {
                if (closed || profileReplacementPending ||
                    !string.Equals(instanceId, record.InstanceId, StringComparison.Ordinal))
                    throw new OperationCanceledException(cancellationToken);
            }
            if (!ProcessMatches(record.Pid, record.GameExecutable, record.ProcessCreatedAt))
                return false;
            if (IsReady) return true;
            if (ControlPipeClockMilliseconds() >= deadlineMilliseconds)
                return false;
            await RecoveryDelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // Retires exactly one adoption attempt. Every side effect is scoped to that
    // attempt's (session, challenge, registration): a late completion after Stop
    // and a successor Start must not touch the successor's timer, route, lease,
    // record or published state (LEAD009R3-01).
    private void RetireIncompleteAdoption(string session, string nonce, ulong registrationSerial)
    {
        StopLeaseTimer(deleteLease: true, session, nonce);
        if (registrationSerial != 0)
            bridgeHostState?.CancelLaunchBinding(session, registrationSerial);
        lock (stateGate)
        {
            if (string.Equals(instanceId, session, StringComparison.Ordinal) &&
                string.Equals(challenge, nonce, StringComparison.Ordinal))
            {
                phase = "error";
                connectionState = "error";
                lastError = "BRIDGE_DISCONNECTED";
                instanceId = null;
                challenge = null;
                gamePid = null;
                gamePath = null;
                gameStartedAtUtc = null;
                readyAtUnix = null;
            }
        }
        // Intentionally leave the same-build game and its protected record
        // untouched. An unproven reconnect must never become an unsafe kill.
    }

    private async Task<bool> WaitAuthenticatedRouteAsync(
        string session, CancellationToken cancellationToken)
    {
        if (bridgeHostState is null) return false;
        // Synthetic lifecycle seams can validate registry admission without
        // waiting on an actual Windows named pipe; production cannot.
        if (testHooks is not null) return true;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (stateGate)
            {
                if (closed || profileReplacementPending ||
                    !string.Equals(instanceId, session, StringComparison.Ordinal))
                    throw new OperationCanceledException(cancellationToken);
            }
            if (bridgeHostState.IsRouteConnected(session)) return true;
            long? expires = bridgeHostState.GetPendingExpiration(session);
            if (!expires.HasValue || ControlPipeClockMilliseconds() >= expires.Value)
                return false;
            await RecoveryDelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
