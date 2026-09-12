using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class OverviewLifecycleService
{
    // IMPLEMENTATION POLICY: the original worker cadence is not yet pinned.
    // Recovery decisions use recovered absolute thresholds/tables; this cadence
    // only drives observation and cancellation responsiveness.
    private static readonly TimeSpan RecoveryMonitorCadence = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim recoverySerial = new(1, 1);
    private readonly CancellationTokenSource recoveryLifetime = new();
    private System.Threading.Timer? recoveryTimer;
    private CancellationTokenSource? activeRecoveryCancellation;
    private int missingProcessObservations;
    private long? bridgeOfflineSinceMilliseconds;
    private long? gameUnhealthySinceMilliseconds;
    private long? hungSinceMilliseconds;
    private string? lastUpdateActivityFingerprint;
    private long? updateActivitySinceMilliseconds;
    private PendingRecoveryRequest? pendingRecoveryRequest;
    private long? pendingRecoveryVerifySinceMilliseconds;
    private OverviewRecoveryStatus recoveryStatus = IdleRecoveryStatus();

    internal event Action<OverviewRecoveryStatus>? RecoveryStatusChanged;

    internal OverviewRecoveryStatus CurrentRecoveryStatus
    {
        get { lock (stateGate) return recoveryStatus; }
    }

    internal Task RunRecoveryObservationForTestAsync() => RecoveryObservationAsync();

    private static OverviewRecoveryStatus IdleRecoveryStatus(
        string? reason = null,
        bool updateDetected = false,
        bool restarted = false,
        long? startedAt = null,
        long? completedAt = null,
        int attempts = 0) => new(
            "idle", reason, updateDetected, restarted, startedAt, completedAt,
            attempts, null, null, null, false);

    private DateTimeOffset RecoveryNow() => testHooks?.UtcNow?.Invoke() ?? DateTimeOffset.UtcNow;

    private long RecoveryClockMilliseconds() =>
        testHooks?.MonotonicMilliseconds?.Invoke() ?? Environment.TickCount64;

    private Task RecoveryDelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        testHooks?.DelayAsync is { } delayAsync
            ? delayAsync(delay, cancellationToken)
            : Task.Delay(delay, cancellationToken);

    private void StartRecoveryMonitor()
    {
        if (config is null || gameRoot is null) return;
        recoveryTimer = new System.Threading.Timer(
            _ => _ = RecoveryObservationAsync(), null,
            RecoveryMonitorCadence, RecoveryMonitorCadence);
    }

    private void StopRecoveryMonitor()
    {
        Interlocked.Exchange(ref recoveryTimer, null)?.Dispose();
        try { recoveryLifetime.Cancel(); } catch { }
        CancellationTokenSource? active = Interlocked.Exchange(ref activeRecoveryCancellation, null);
        try { active?.Cancel(); } catch { }
        active?.Dispose();
    }

    internal void NotifyAutomationChanged(bool enabled)
    {
        if (enabled) return;
        CancellationTokenSource? active = Volatile.Read(ref activeRecoveryCancellation);
        try { active?.Cancel(); } catch { }
        ResetPendingRecoveryRequest();
        ResetRunningFailureObservations();
        SetRecoveryStatus(IdleRecoveryStatus());
    }

    private void SetDesiredRunning(bool desired)
    {
        if (config is null) return;
        config.Update(current => current.GameDesiredRunning == desired
            ? current
            : current with { GameDesiredRunning = desired });
        if (!desired) NotifyAutomationChanged(enabled: false);
    }

    private bool RecoveryEnabledAndDesired()
    {
        LWBridgeLocalConfig? snapshot = config?.Snapshot;
        return snapshot is not null && snapshot.AutoReconnect && snapshot.GameDesiredRunning;
    }

    private async Task RecoveryObservationAsync()
    {
        if (!await recoverySerial.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            lock (stateGate)
            {
                if (closed) return;
            }

            OwnedSnapshot? snapshot = GetOwnedSnapshot();
            if (snapshot is null || snapshot.Phase is "starting" or "stopping")
            {
                ResetFailureObservations();
                return;
            }

            if (!ProcessMatches(snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc))
            {
                ResetRunningFailureObservations();
                // A confirmed reload/quit action can exit the game before the next
                // monitor tick. Preserve its fresh exact-session heartbeat reason.
                if (RecoveryEnabledAndDesired())
                {
                    RecoveryHeartbeatObservation lastHeartbeat = ReadRecoveryHeartbeat(snapshot);
                    if (lastHeartbeat.RecoveryConfirmed) LatchRecoveryRequest(lastHeartbeat, RecoveryClockMilliseconds());
                }
                int missing = Interlocked.Increment(ref missingProcessObservations);
                if (missing <= 1) return;
                MarkUnexpectedExit(snapshot);
                if (RecoveryEnabledAndDesired())
                {
                    PendingRecoveryRequest? pending = pendingRecoveryRequest;
                    ResetPendingRecoveryRequest();
                    await RecoverOwnedSessionAsync(snapshot, pending?.Reason ?? "processExit", terminateFirst: false,
                        initialUpdateDetected: pending?.UpdateDetected ?? false).ConfigureAwait(false);
                }
                return;
            }

            Interlocked.Exchange(ref missingProcessObservations, 0);
            if (!RecoveryEnabledAndDesired())
            {
                ResetRunningFailureObservations();
                return;
            }

            long now = RecoveryClockMilliseconds();
            RecoveryHeartbeatObservation heartbeat = ReadRecoveryHeartbeat(snapshot);
            if (heartbeat.RecoveryConfirmed)
                LatchRecoveryRequest(heartbeat, now);
            if (pendingRecoveryRequest is not null)
            {
                await ObservePendingRecoveryRequestAsync(snapshot, heartbeat, now).ConfigureAwait(false);
                return;
            }

            // The original monitor suppresses disconnect/hang classification while
            // the official launcher/updater/sync process family is active.
            if (IsUpdateProcessRunning())
            {
                ResetRunningFailureObservations();
                return;
            }
            if (!heartbeat.BridgeOnline)
            {
                bridgeOfflineSinceMilliseconds ??= now;
                bool hung = IsOwnedProcessHung(snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc);
                if (hung) hungSinceMilliseconds ??= now;
                else hungSinceMilliseconds = null;
                gameUnhealthySinceMilliseconds = null;

                if (hungSinceMilliseconds is long hungSince &&
                    now - hungSince >= OverviewRecoveryPolicy.HangThreshold.TotalMilliseconds &&
                    now - bridgeOfflineSinceMilliseconds.Value >= OverviewRecoveryPolicy.HangThreshold.TotalMilliseconds)
                {
                    await RecoverOwnedSessionAsync(snapshot, "hang", terminateFirst: true).ConfigureAwait(false);
                    return;
                }

                if (now - bridgeOfflineSinceMilliseconds.Value >= OverviewRecoveryPolicy.DisconnectThreshold.TotalMilliseconds)
                    await RecoverOwnedSessionAsync(snapshot, "disconnect", terminateFirst: true).ConfigureAwait(false);
                return;
            }

            bridgeOfflineSinceMilliseconds = null;
            hungSinceMilliseconds = null;
            if (!heartbeat.GameStateObserved)
            {
                gameUnhealthySinceMilliseconds = null;
                return;
            }
            if (heartbeat.GameHealthy)
            {
                gameUnhealthySinceMilliseconds = null;
                return;
            }

            gameUnhealthySinceMilliseconds ??= now;
            if (now - gameUnhealthySinceMilliseconds.Value >= OverviewRecoveryPolicy.LoginUnavailableThreshold.TotalMilliseconds)
                await RecoverOwnedSessionAsync(snapshot, "disconnect", terminateFirst: true).ConfigureAwait(false);
        }
        finally { recoverySerial.Release(); }
    }

    private void LatchRecoveryRequest(RecoveryHeartbeatObservation heartbeat, long now)
    {
        if (heartbeat.RecoveryReason is null) return;
        PendingRecoveryRequest? current = pendingRecoveryRequest;
        if (current is not null) return;
        pendingRecoveryRequest = new PendingRecoveryRequest(
            heartbeat.RecoveryReason,
            heartbeat.RecoveryUpdateDetected,
            now,
            RecoveryNow().ToUnixTimeMilliseconds());
        pendingRecoveryVerifySinceMilliseconds = null;
        SetRecoveryStatus(new("waiting", heartbeat.RecoveryReason, heartbeat.RecoveryUpdateDetected, false,
            pendingRecoveryRequest.StartedAtUnixMilliseconds, null, 0, null, null, null, false));
    }

    private async Task ObservePendingRecoveryRequestAsync(
        OwnedSnapshot snapshot, RecoveryHeartbeatObservation heartbeat, long now)
    {
        PendingRecoveryRequest pending = pendingRecoveryRequest!;
        if (!RecoveryEnabledAndDesired()) { ResetPendingRecoveryRequest(); SetRecoveryStatus(IdleRecoveryStatus()); return; }
        if (heartbeat.GameStateObserved && heartbeat.GameHealthy)
        {
            pendingRecoveryVerifySinceMilliseconds ??= now;
            SetRecoveryStatus(new("verifying", pending.Reason, pending.UpdateDetected, false,
                pending.StartedAtUnixMilliseconds, null, 0, null, null, null, false));
            if (now - pendingRecoveryVerifySinceMilliseconds.Value >= OverviewRecoveryPolicy.StableVerification.TotalMilliseconds)
            {
                SetRecoveryStatus(IdleRecoveryStatus(pending.Reason, pending.UpdateDetected, false,
                    pending.StartedAtUnixMilliseconds, RecoveryNow().ToUnixTimeMilliseconds(), 0));
                ResetPendingRecoveryRequest();
            }
            return;
        }
        pendingRecoveryVerifySinceMilliseconds = null;
        if (IsUpdateProcessRunning())
        {
            if (UpdateActivityStalled(now))
            {
                await TerminateUpdateProcessesAsync(CancellationToken.None).ConfigureAwait(false);
                TimeSpan delay = OverviewRecoveryPolicy.NormalRetryDelays[0];
                SetRecoveryStatus(new("waiting", pending.Reason, true, false,
                    pending.StartedAtUnixMilliseconds, null, 1,
                    RecoveryNow().Add(delay).ToUnixTimeMilliseconds(),
                    "game update had no activity for 15 minutes", null, false));
                await RecoveryDelayAsync(delay, recoveryLifetime.Token).ConfigureAwait(false);
                ResetPendingRecoveryRequest();
                await RecoverOwnedSessionAsync(snapshot, pending.Reason, terminateFirst: true,
                    initialUpdateDetected: true).ConfigureAwait(false);
                return;
            }
            SetRecoveryStatus(new("updating", pending.Reason, true, false,
                pending.StartedAtUnixMilliseconds, null, 0, null, null, null, false));
            return;
        }
        ResetUpdateActivity();
        SetRecoveryStatus(new("waiting", pending.Reason, pending.UpdateDetected, false,
            pending.StartedAtUnixMilliseconds, null, 0, null, null, null, false));
        if (now - pending.StartClockMilliseconds < OverviewRecoveryPolicy.DisconnectWaitBeforeTerminate.TotalMilliseconds) return;
        ResetPendingRecoveryRequest();
        await RecoverOwnedSessionAsync(snapshot, pending.Reason, terminateFirst: true,
            initialUpdateDetected: pending.UpdateDetected).ConfigureAwait(false);
    }

    private void ResetPendingRecoveryRequest()
    {
        pendingRecoveryRequest = null;
        pendingRecoveryVerifySinceMilliseconds = null;
    }

    private void ResetFailureObservations()
    {
        Interlocked.Exchange(ref missingProcessObservations, 0);
        ResetRunningFailureObservations();
    }

    private void ResetRunningFailureObservations()
    {
        bridgeOfflineSinceMilliseconds = null;
        gameUnhealthySinceMilliseconds = null;
        hungSinceMilliseconds = null;
    }

    private void MarkUnexpectedExit(OwnedSnapshot snapshot)
    {
        StopLeaseTimer(deleteLease: true);
        lock (stateGate)
        {
            if (gamePid != snapshot.GamePid || instanceId != snapshot.InstanceId) return;
            phase = "error";
            connectionState = "recovering";
            lastError = "GAME_EXITED_RESTORE_REQUIRED";
        }
    }

    private async Task RecoverOwnedSessionAsync(OwnedSnapshot snapshot, string reason, bool terminateFirst, bool initialUpdateDetected = false)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(recoveryLifetime.Token);
        CancellationTokenSource? previous = Interlocked.Exchange(ref activeRecoveryCancellation, linked);
        previous?.Dispose();
        CancellationToken token = linked.Token;
        long startedAt = RecoveryNow().ToUnixTimeMilliseconds();
        bool updateDetected = initialUpdateDetected;
        int attempts = 0;

        try
        {
            SetRecoveryStatus(new("repairing", reason, false, false,
                startedAt, null, attempts, null, null, null, false));
            token.ThrowIfCancellationRequested();
            // Once an eligible recovery has terminated/lost the owned process, exact
            // journal restoration is cleanup, not a retry, and must finish even if
            // Automatic Reconnection is disabled concurrently.
            if (terminateFirst)
                await TerminateOwnedProcessAsync(snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc, CancellationToken.None).ConfigureAwait(false);
            await CleanupExitedOwnedSessionAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

            while (RecoveryEnabledAndDesired())
            {
                token.ThrowIfCancellationRequested();
                bool updateRunning = IsUpdateProcessRunning();
                if (updateRunning)
                {
                    updateDetected = true;
                    if (UpdateActivityStalled(RecoveryClockMilliseconds()))
                    {
                        await TerminateUpdateProcessesAsync(CancellationToken.None).ConfigureAwait(false);
                        attempts++;
                        TimeSpan stalledDelay = OverviewRecoveryPolicy.RetryDelay(maintenance: false, attempts);
                        SetRecoveryStatus(new("waiting", reason, true, false,
                            startedAt, null, attempts,
                            RecoveryNow().Add(stalledDelay).ToUnixTimeMilliseconds(),
                            "game update had no activity for 15 minutes", null, false));
                        await RecoveryDelayAsync(stalledDelay, token).ConfigureAwait(false);
                        continue;
                    }
                    attempts++;
                    TimeSpan delay = OverviewRecoveryPolicy.RetryDelay(maintenance: true, attempts);
                    SetRecoveryStatus(new("updating", reason, true, false,
                        startedAt, null, attempts,
                        RecoveryNow().Add(delay).ToUnixTimeMilliseconds(), null, null, false));
                    await RecoveryDelayAsync(delay, token).ConfigureAwait(false);
                    continue;
                }
                ResetUpdateActivity();

                attempts++;
                SetRecoveryStatus(new("launching", reason, updateDetected, false,
                    startedAt, null, attempts, null, null, null, false));
                try
                {
                    await StartAsync(token).ConfigureAwait(false);
                    SetRecoveryStatus(new("verifying", reason, updateDetected, true,
                        startedAt, null, attempts, null, null, null, false));
                    await RecoveryDelayAsync(OverviewRecoveryPolicy.StableVerification, token).ConfigureAwait(false);
                    OwnedSnapshot? current = GetOwnedSnapshot();
                    if (current is not null && ProcessMatches(current.GamePid, current.GamePath, current.GameStartedAtUtc) && IsReady)
                    {
                        SetRecoveryStatus(IdleRecoveryStatus(reason, updateDetected, true,
                            startedAt, RecoveryNow().ToUnixTimeMilliseconds(), attempts));
                        ResetFailureObservations();
                        return;
                    }
                    throw new BridgeCommandException("BRIDGE_START_TIMEOUT",
                        "The recovered game did not remain bridge-ready for the verification window.");
                }

                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (!RecoveryEnabledAndDesired()) break;
                    bool maintenance = IsUpdateProcessRunning();
                    updateDetected |= maintenance;
                    TimeSpan delay = OverviewRecoveryPolicy.RetryDelay(maintenance, attempts);
                    string state = maintenance ? "maintenance" : "waiting";
                    SetRecoveryStatus(new(state, reason, updateDetected, false,
                        startedAt, null, attempts,
                        RecoveryNow().Add(delay).ToUnixTimeMilliseconds(),
                        RecoveryErrorCode(ex), null, false));
                    await RecoveryDelayAsync(delay, token).ConfigureAwait(false);
                }
            }

            SetRecoveryStatus(IdleRecoveryStatus(reason, updateDetected, false,
                startedAt, RecoveryNow().ToUnixTimeMilliseconds(), attempts));
        }
        catch (OperationCanceledException)
        {
            SetRecoveryStatus(IdleRecoveryStatus(reason, updateDetected, false,
                startedAt, RecoveryNow().ToUnixTimeMilliseconds(), attempts));
        }
        catch (Exception ex)
        {
            SetRecoveryStatus(new("failed", reason, updateDetected, false,
                startedAt, RecoveryNow().ToUnixTimeMilliseconds(), attempts,
                null, RecoveryErrorCode(ex), null, false));
        }
        finally
        {
            Interlocked.CompareExchange(ref activeRecoveryCancellation, null, linked);
        }
    }

    private async Task CleanupExitedOwnedSessionAsync(OwnedSnapshot snapshot, CancellationToken token)
    {
        JsonElement result = await RunHelperAsync(
            new OverviewHelperInvocation("stop", profileId, snapshot.InstanceId,
                snapshot.Challenge, snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc), token).ConfigureAwait(false);
        ValidateStopResult(result, profileId, snapshot.InstanceId,
            snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc, requireCurrentClientEvidence);
        if (testHooks is null) WriteHostStopEvidence(snapshot.InstanceId, result);
        StopLeaseTimer(deleteLease: true);
        ClearRuntimeSessionFiles();
        lock (stateGate)
        {
            if (instanceId != snapshot.InstanceId || gamePid != snapshot.GamePid) return;
            phase = "stopped";
            connectionState = "offline";
            instanceId = null;
            challenge = null;
            gamePid = null;
            launcherPid = null;
            gamePath = null;
            gameStartedAtUtc = null;
            lastError = null;
            readyAtUnix = null;
        }
    }

    private RecoveryHeartbeatObservation ReadRecoveryHeartbeat(OwnedSnapshot snapshot)
    {
        try
        {
            byte[] bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(Path.Combine(runtimeRoot, "heartbeat.json"));
            using JsonDocument heartbeat = JsonDocument.Parse(bytes);
            JsonElement root = heartbeat.RootElement;
            long now = RecoveryNow().ToUnixTimeSeconds();
            if (!MatchesInt(root, "schemaVersion", 1) ||
                !MatchesString(root, "bridgeVersion", BridgeVersion) ||
                !MatchesString(root, "profileId", profileId) ||
                !MatchesString(root, "sessionId", snapshot.InstanceId) ||
                !MatchesString(root, "challenge", snapshot.Challenge) ||
                !MatchesInt(root, "gamePid", snapshot.GamePid) ||
                !root.TryGetProperty("updatedAt", out JsonElement updated) || !updated.TryGetInt64(out long timestamp) ||
                timestamp > now + 5 || now - timestamp > 5)
                return new(false, false, false, false, null, false);

            bool recoveryObserved = MatchesBool(root, "recoveryObserved", true);
            bool recoveryAmbiguous = MatchesBool(root, "recoveryAmbiguous", true);
            bool recoveryConfirmed = recoveryObserved && !recoveryAmbiguous && MatchesBool(root, "recoveryConfirmed", true);
            string? recoveryReason = null;
            bool recoveryUpdateDetected = false;
            if (recoveryConfirmed && root.TryGetProperty("recoveryReason", out JsonElement reasonElement) &&
                reasonElement.ValueKind == JsonValueKind.String)
            {
                string? candidate = reasonElement.GetString();
                if (candidate is "disconnect" or "crossDisconnect" or "forceUpdate" or "exitPrompt")
                {
                    recoveryReason = candidate;
                    recoveryUpdateDetected = candidate == "forceUpdate" || MatchesBool(root, "recoveryUpdateDetected", true);
                }
            }
            recoveryConfirmed = recoveryReason is not null;

            bool observed = MatchesBool(root, "gameStateObserved", true);
            if (!observed) return new(true, false, false, recoveryConfirmed, recoveryReason, recoveryUpdateDetected);
            bool healthy = MatchesBool(root, "gameReady", true) &&
                MatchesBool(root, "loggedIn", true) &&
                MatchesBool(root, "connected", true) &&
                MatchesBool(root, "connecting", false) &&
                root.TryGetProperty("gameUid", out JsonElement uid) && uid.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(uid.GetString()) &&
                root.TryGetProperty("serverId", out JsonElement server) && server.TryGetInt32(out int serverId) && serverId > 0 &&
                root.TryGetProperty("worldPos", out JsonElement world) && world.TryGetInt64(out long worldPos) && worldPos > 0;
            return new(true, true, healthy, recoveryConfirmed, recoveryReason, recoveryUpdateDetected);
        }
        catch
        {
            return new(false, false, false, false, null, false);
        }
    }

    private bool IsOwnedProcessHung(int pid, string expectedPath, string expectedStartedAtUtc)
    {
        if (testHooks?.ProcessHung is { } test) return test(pid, expectedPath);
        if (!ValidateExactProcessIdentity(pid, expectedPath, expectedStartedAtUtc, out Process? verified) || verified is null) return false;
        verified.Dispose();
        bool hung = false;
        EnumWindows((window, parameter) =>
        {
            _ = parameter;
            _ = GetWindowThreadProcessId(window, out uint ownerPid);
            if (ownerPid == (uint)pid && IsHungAppWindow(window)) hung = true;
            return !hung;
        }, IntPtr.Zero);
        return hung;
    }

    private bool UpdateActivityStalled(long now)
    {
        string? fingerprint = ReadUpdateActivityFingerprint();
        if (fingerprint is null)
        {
            ResetUpdateActivity();
            return false;
        }
        if (!string.Equals(lastUpdateActivityFingerprint, fingerprint, StringComparison.Ordinal))
        {
            lastUpdateActivityFingerprint = fingerprint;
            updateActivitySinceMilliseconds = now;
            return false;
        }
        updateActivitySinceMilliseconds ??= now;
        return now - updateActivitySinceMilliseconds.Value >= OverviewRecoveryPolicy.UpdateNoActivityTimeout.TotalMilliseconds;
    }

    private void ResetUpdateActivity()
    {
        lastUpdateActivityFingerprint = null;
        updateActivitySinceMilliseconds = null;
    }

    private string? ReadUpdateActivityFingerprint()
    {
        if (testHooks?.UpdateActivityFingerprint is { } test) return test();
        if (gameRoot is null) return null;
        string[] paths =
        [
            Path.Combine(gameRoot, "manifest.json"),
            Path.Combine(gameRoot, "Temp"),
            Path.Combine(gameRoot, "Game", "LastWar_Data", "Plugins", "x86_64", "xlua.dll"),
        ];
        var parts = new List<string>(paths.Length);
        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    info.Refresh();
                    parts.Add($"F:{info.Length}:{info.LastWriteTimeUtc.Ticks}");
                }
                else if (Directory.Exists(path))
                {
                    var info = new DirectoryInfo(path);
                    info.Refresh();
                    parts.Add($"D:{info.LastWriteTimeUtc.Ticks}");
                }
                else parts.Add("M");
            }
            catch { return null; }
        }
        return string.Join("|", parts);
    }

    private bool IsUpdateProcessRunning()
    {
        if (testHooks?.UpdateProcessRunning is { } test) return test();
        if (gameRoot is null) return false;
        foreach (string fileName in new[] { "LastWarLauncher.exe", "LastWarUpdater.exe", "LastWarSync.exe" })
        {
            string expectedPath = Path.Combine(gameRoot, fileName);
            foreach (Process candidate in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fileName)))
            {
                using (candidate)
                {
                    if (!ValidateExactProcessPath(candidate.Id, expectedPath, out Process? verified) || verified is null) continue;
                    verified.Dispose();
                    return true;
                }
            }
        }
        return false;
    }

    private async Task TerminateUpdateProcessesAsync(CancellationToken cancellationToken)
    {
        if (testHooks?.TerminateUpdateProcessesAsync is { } test)
        {
            await test(cancellationToken).ConfigureAwait(false);
            ResetUpdateActivity();
            return;
        }
        if (gameRoot is null) return;
        foreach (string fileName in new[] { "LastWarLauncher.exe", "LastWarUpdater.exe", "LastWarSync.exe" })
        {
            string expectedPath = Path.Combine(gameRoot, fileName);
            foreach (Process candidate in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fileName)))
            {
                using (candidate)
                {
                    if (!ValidateExactProcessPath(candidate.Id, expectedPath, out Process? verified) || verified is null) continue;
                    using (verified)
                    {
                        IntPtr handle = OpenProcess(0x0001, false, verified.Id);
                        if (handle == IntPtr.Zero)
                            throw new BridgeCommandException("UPDATE_RECOVERY_TERMINATE_FAILED", $"Unable to open exact updater process {verified.Id}.");
                        try
                        {
                            if (!TerminateProcess(handle, 1))
                                throw new BridgeCommandException("UPDATE_RECOVERY_TERMINATE_FAILED", $"Unable to terminate exact updater process {verified.Id}.");
                        }
                        finally { _ = CloseHandle(handle); }
                        await verified.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        ResetUpdateActivity();
    }

    private async Task TerminateOwnedProcessAsync(int pid, string expectedPath, string expectedStartedAtUtc, CancellationToken cancellationToken)
    {
        if (testHooks?.TerminateOwnedProcessAsync is { } test)
        {
            await test(pid, expectedPath, expectedStartedAtUtc, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!ValidateExactProcessIdentity(pid, expectedPath, expectedStartedAtUtc, out Process? process) || process is null)
            throw new BridgeCommandException("PROCESS_IDENTITY_CHANGED", "Unable to verify the exact owned game process incarnation.");
        using (process)
        {
            IntPtr handle = OpenProcess(0x0001, false, pid); // PROCESS_TERMINATE
            if (handle == IntPtr.Zero)
                throw new BridgeCommandException("GAME_RECOVERY_TERMINATE_FAILED", "Unable to open the exact owned game process for recovery termination.");
            try
            {
                if (!TerminateProcess(handle, 1))
                    throw new BridgeCommandException("GAME_RECOVERY_TERMINATE_FAILED", "Unable to terminate the exact owned game process for recovery.");
            }
            finally { _ = CloseHandle(handle); }
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
    }


    private static bool ValidateExactProcessIdentity(int pid, string expectedPath, string expectedStartedAtUtc, out Process? process)
    {
        process = null;
        Process? candidate = null;
        try
        {
            candidate = Process.GetProcessById(pid);
            if (candidate.HasExited) return false;
            string? actual = candidate.MainModule?.FileName;
            string actualStartedAtUtc = candidate.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            if (actual is null || !PathEquals(actual, expectedPath) ||
                !string.Equals(actualStartedAtUtc, expectedStartedAtUtc, StringComparison.Ordinal)) return false;
            process = candidate;
            candidate = null;
            return true;
        }
        catch { return false; }
        finally { candidate?.Dispose(); }
    }

    private static bool ValidateExactProcessPath(int pid, string expectedPath, out Process? process)
    {
        process = null;
        Process? candidate = null;
        try
        {
            candidate = Process.GetProcessById(pid);
            if (candidate.HasExited) return false;
            string? actual = candidate.MainModule?.FileName;
            if (actual is null || !PathEquals(actual, expectedPath)) return false;
            process = candidate;
            candidate = null;
            return true;
        }
        catch { return false; }
        finally { candidate?.Dispose(); }
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsHungAppWindow(IntPtr window);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private void SetRecoveryStatus(OverviewRecoveryStatus next)
    {
        bool changed;
        lock (stateGate)
        {
            changed = recoveryStatus != next;
            recoveryStatus = next;
        }
        if (changed)
        {
            try { RecoveryStatusChanged?.Invoke(next); }
            catch { }
        }
    }

    private static string RecoveryErrorCode(Exception ex) => ex switch
    {
        BridgeCommandException bridge => bridge.Code,
        LocalConfigStoreException configError => configError.Code,
        _ => "GAME_RECOVERY_FAILED",
    };

    private sealed record RecoveryHeartbeatObservation(
        bool BridgeOnline,
        bool GameStateObserved,
        bool GameHealthy,
        bool RecoveryConfirmed,
        string? RecoveryReason,
        bool RecoveryUpdateDetected);

    private sealed record PendingRecoveryRequest(
        string Reason,
        bool UpdateDetected,
        long StartClockMilliseconds,
        long StartedAtUnixMilliseconds);
}
