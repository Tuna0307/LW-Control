using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

// HOME 009 C/D. EXACT_CONTRACT_RECONSTRUCTED from lwbridge-0.3.17.exe (SHA-256 4E9C3113...D6783):
//   monitor tick         0x41a8a0 (interval 2 s, 0xe5650)      recovery start    0x41b03a
//   recovery run         0xe5884  (2 s loop, 0xe69e3)           log classifier    0x419e2a
//   retry tables         0xd67b08 (normal) / 0xd681b0 (maintenance)
// Decision logic below mirrors tools/lwbridge317/home009_recovery_oracle.py line by line; the cited
// RVAs and the constants are asserted against the image by home009_contract.py. Effects (terminate,
// cleanup/restoration, relaunch, log files, bridge heartbeat) are current-client adaptations.
internal sealed partial class OverviewLifecycleService
{
    private static readonly TimeSpan RecoveryMonitorCadence = TimeSpan.FromSeconds(2);   // 0xe5660 interval(2 s)
    private static readonly TimeSpan RecoveryRunCadence = TimeSpan.FromSeconds(2);       // 0xe69e3 timer(2 s)
    private readonly SemaphoreSlim recoverySerial = new(1, 1);
    private readonly CancellationTokenSource recoveryLifetime = new();
    private System.Threading.Timer? recoveryTimer;
    private CancellationTokenSource? activeRecoveryCancellation;
    private RecoveryRunState? activeRecoveryRun;
    private Task? activeRecoveryRunTask;
    private long recoveryRunSequence;
    private int missingProcessObservations;                  // adaptation: UI exit tracking (see RefreshExitedOwnership)
    // 0x41b451 / ctx+0x120: the original keeps tracking the launched game's PID after it is terminated; only
    // Stop (0x23e158 / 0x129622) clears it. The current client's restoration cleanup clears the owned-session
    // fields, so the tracked identity is kept separately.
    private RecoveryTrackedGame? recoveryTracked;
    // 0x41ba64: the game-state record ([+0x88] pid, [+0x8c] valid) is produced by game-state events while the
    // bridge is connected and is retained while it is not; the current client delivers it through the heartbeat.
    private int healthRecordPid;
    private bool healthRecordValid;
    private readonly RecoveryMonitorObservation monitorObservation = new();
    private ulong recoveryNoticeId;
    private OverviewRecoveryStatus recoveryStatus = IdleRecoveryStatus();

    internal event Action<OverviewRecoveryStatus>? RecoveryStatusChanged;

    internal OverviewRecoveryStatus CurrentRecoveryStatus
    {
        get { lock (stateGate) return recoveryStatus; }
    }

    internal Task RunRecoveryObservationForTestAsync() => RecoveryObservationAsync();

    internal async Task RunRecoveryRunTickForTestAsync()
    {
        RecoveryRunState? run;
        lock (stateGate) run = activeRecoveryRun;
        if (run is null || run.Finished) return;
        await RecoveryRunTickAsync(run).ConfigureAwait(false);
    }

    private static OverviewRecoveryStatus IdleRecoveryStatus(
        string? reason = null,
        bool updateDetected = false,
        bool restarted = false,
        long startedAt = 0,
        long? completedAt = null,
        int attempts = 0,
        ulong noticeId = 0) => new(
            "idle", reason, updateDetected, restarted, startedAt, completedAt,
            attempts, null, null, noticeId, false);

    private ulong NextRecoveryNoticeId()
    {
        lock (stateGate)
        {
            recoveryNoticeId = unchecked(recoveryNoticeId + 1);
            return recoveryNoticeId;
        }
    }

    private ulong CurrentRecoveryNoticeId()
    {
        lock (stateGate) return recoveryNoticeId;
    }

    private DateTimeOffset RecoveryNow() => testHooks?.UtcNow?.Invoke() ?? DateTimeOffset.UtcNow;

    private long RecoveryClockMilliseconds() =>
        testHooks?.MonotonicMilliseconds?.Invoke() ?? Environment.TickCount64;

    // 0x2c9034: Unix epoch milliseconds from GetSystemTimePreciseAsFileTime. The monitor and the run compare
    // wall-clock instants, so the recovery engine uses the wall clock (test hook: MonotonicMilliseconds).
    private long RecoveryEngineNowMilliseconds() =>
        testHooks?.MonotonicMilliseconds?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private Task RecoveryDelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        testHooks?.DelayAsync is { } delayAsync
            ? delayAsync(delay, cancellationToken)
            : Task.Delay(delay, cancellationToken);

    private void StartRecoveryMonitor()
    {
        if (!recoveryMonitorEnabled || config is null || gameRoot is null || recoveryTimer is not null) return;
        recoveryTimer = new System.Threading.Timer(
            _ => _ = RecoveryObservationAsync(), null,
            RecoveryMonitorCadence, RecoveryMonitorCadence);
    }

    private void StopRecoveryMonitor()
    {
        Interlocked.Exchange(ref recoveryTimer, null)?.Dispose();
        try { recoveryLifetime.Cancel(); } catch { }
        RecoveryRunState? run;
        lock (stateGate) run = activeRecoveryRun;
        CancellationTokenSource? active = run?.Cancellation;
        try { active?.Cancel(); } catch { }
        // Shutdown / profile replacement: the active run ends idle immediately instead of waiting for its next tick.
        if (run is not null) FinishRecoveryRunStopped(run);
    }

    internal void NotifyAutomationChanged(bool enabled)
    {
        // 0xe69c3-0xe69cc: the original run re-reads the persisted auto_force_update_reload setting at the end of
        // every iteration and finishes idle (0x41c201) when it is off; disabling does not interrupt the current
        // iteration (including an in-flight relaunch). The monitor tick also re-reads it (0x41a8c5).
        _ = enabled;
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

    // game_lifecycle_start (0x11157c) sets desired-running and resets the recovery status to idle (0x41ad16)
    // before launching. Recovery relaunches call StartAsync directly and never reset their own status.
    private async Task<object?> StartCommandAsync(bool closeUnmanaged, CancellationToken cancellationToken)
    {
        ResetRecoveryStatusToIdle(invalidateRun: false);
        return await StartAsync(cancellationToken, closeUnmanaged).ConfigureAwait(false);
    }

    // HOME 009 R1 C. EXACT_CONTRACT_RECONSTRUCTED from lwbridge-0.3.17.exe (SHA-256 4E9C3113...D6783), profile launch
    // 0x1d5009: `closeUnmanaged` (0x207784) gates 0x1d6548. Unmanaged = LastWar processes of <root>\Game\LastWar.exe
    // (0x41dea4) whose PID is not an LWBridge-managed one (hash-set filter 0x30a354 via 0x39d7de), PIDs sorted
    // (0x2a2887). When any exist and closeUnmanaged: each PID is terminated in order by the path-verified 0x41e543
    // (an error aborts the launch at once, remaining PIDs untouched); a deadline of now+5 s is then set (0x1d693f-0x1d6955)
    // and the loop 0x1d723b lists again, finishes when empty, fails GAME_CLOSE_TIMEOUT (0x1d74e5) when
    // now >= deadline (secs,nanos lexicographic compare 0x1d72d0-0x1d72eb), otherwise waits 100 ms (0x1d7305) and repeats.
    private static readonly TimeSpan UnmanagedCloseWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UnmanagedClosePoll = TimeSpan.FromMilliseconds(100);

    private IReadOnlyList<int> SelectedGamePids(string selectedRoot)
    {
        if (testHooks?.SelectedGamePids is { } hook) return hook(selectedRoot);
        string expected = Path.GetFullPath(Path.Combine(selectedRoot, "Game", "LastWar.exe"));
        var pids = new SortedSet<int>();
        foreach (Process process in Process.GetProcessesByName("LastWar"))
        {
            try
            {
                string? actual = process.MainModule?.FileName;
                if (actual is not null && PathEquals(actual, expected)) pids.Add(process.Id);
            }
            catch { }
            finally { process.Dispose(); }
        }
        return pids.ToArray();
    }

    private async Task CloseUnmanagedSelectedGamesAsync(string selectedRoot, CancellationToken cancellationToken)
    {
        IReadOnlyList<int> pids = SelectedGamePids(selectedRoot);
        if (pids.Count == 0) return;
        string expectedPath = Path.GetFullPath(Path.Combine(selectedRoot, "Game", "LastWar.exe"));
        IOwnedProcessApi api = OwnedProcessApi;
        foreach (int pid in pids)
        {
            OwnedProcessTerminationResult result = await OwnedProcessTermination.TerminateAsync(
                api, pid, expectedPath, null, RecoveryDelayAsync, cancellationToken, awaitExit: false).ConfigureAwait(false);
            switch (result)
            {
                case OwnedProcessTerminationResult.QueryFailed:
                    throw new BridgeCommandException("PROCESS_QUERY_FAILED", "Unable to verify the target process path.");
                case OwnedProcessTerminationResult.OpenDenied:
                    throw new BridgeCommandException("IO_ERROR", "open target process: " + OsErrorText(api, 5));
                case OwnedProcessTerminationResult.TerminateFailed:
                    throw new BridgeCommandException("IO_ERROR", "terminate target process: " + OsErrorText(api, 5));
            }
        }
        long deadline = checked(RecoveryClockMilliseconds() + (long)UnmanagedCloseWindow.TotalMilliseconds);
        while (true)
        {
            if (SelectedGamePids(selectedRoot).Count == 0) return;
            if (RecoveryClockMilliseconds() >= deadline)
                throw new BridgeCommandException("GAME_CLOSE_TIMEOUT", "GAME_CLOSE_TIMEOUT");
            await RecoveryDelayAsync(UnmanagedClosePoll, cancellationToken).ConfigureAwait(false);
        }
    }

    // Rust std::io::Error Display: "<text> (os error N)"; the exact OS code of a failed open is not retained here,
    // so only the IO_ERROR code and the operation label are source-backed (0x41da31/0x41da6d, 0x2a1998).
    private static string OsErrorText(IOwnedProcessApi api, int fallback)
    {
        int code = api.LastErrorCode(fallback);
        return $"{new System.ComponentModel.Win32Exception(code).Message} (os error {code})";
    }

    internal void InvalidateRecoveryForUserStop() => ResetRecoveryStatusToIdle(invalidateRun: true);

    // 0x41ad16: fresh idle record preserving the notice id, game-state record and M.offline_pid cleared.
    private void ResetRecoveryStatusToIdle(bool invalidateRun)
    {
        OverviewRecoveryStatus? published = null;
        RecoveryRunState? run = null;
        lock (stateGate)
        {
            if (invalidateRun)
            {
                run = activeRecoveryRun;
                if (run is not null)
                {
                    run.Finished = true;               // 0x41bc47 run id increment: the stale run does nothing more
                    activeRecoveryRun = null;
                }
                recoveryTracked = null;                // 0x41bc41 xchg [ctx+0x120], 0
            }
            healthRecordPid = 0;
            healthRecordValid = false;
            monitorObservation.OfflinePid = 0;
            OverviewRecoveryStatus idle = IdleRecoveryStatus(noticeId: recoveryStatus.NoticeId);
            if (idle != recoveryStatus) published = recoveryStatus = idle;
        }
        if (run is not null)
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref activeRecoveryCancellation, null, run.Cancellation),
                    run.Cancellation))
            {
                try { run.Cancellation.Cancel(); } catch { }
                run.Cancellation.Dispose();
            }
        }
        if (published is not null) PublishRecoveryStatus(published);
    }

    // 0x4199ba
    private static bool IsActiveRecoveryState(string state) => state is
        "waiting" or "repairing" or "launching" or "verifying" or "updating" or "maintenance";

    // 0x41d2ce: <root>\Game\LastWar.exe must be a file.
    private bool RecoveryRootAvailable()
    {
        if (testHooks?.GameRootAvailable is { } hook) return hook();
        string? root = gameRoot;
        if (root is null) return false;
        if (testHooks is not null) return true;
        try { return File.Exists(Path.Combine(root, "Game", "LastWar.exe")); }
        catch { return false; }
    }

    // ---------------------------------------------------------------------------------------------
    // Monitor tick (0x41a8a0)
    // ---------------------------------------------------------------------------------------------
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
            bool lifecycleBusy = snapshot is not null && snapshot.Phase is "starting" or "stopping";
            string currentPhase;
            lock (stateGate) currentPhase = phase;
            if (currentPhase is "starting" or "stopping") lifecycleBusy = true;
            if (snapshot is null || lifecycleBusy)
                Interlocked.Exchange(ref missingProcessObservations, 0);   // adaptation: UI exit tracking
            else if (!ProcessMatches(snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc))
            {
                // Adaptation (UI exit state): keeps the previous consecutive-miss classification that publishes
                // GAME_EXITED_RESTORE_REQUIRED independently of the recovery setting.
                if (Interlocked.Increment(ref missingProcessObservations) > 1) MarkUnexpectedExit(snapshot);
            }
            else Interlocked.Exchange(ref missingProcessObservations, 0);

            if (lifecycleBusy) return;                                       // adaptation: no decisions mid start/stop

            // Current-client adaptation of the game.recovery_requested event (0x41ec72): a fresh, exact-session,
            // confirmed heartbeat request starts the same recovery (0x41b03a) with its reason.
            RecoveryHeartbeatObservation heartbeat = snapshot is null
                ? new(false, false, false, false, null, false)
                : ReadRecoveryHeartbeat(snapshot);
            if (snapshot is not null) UpdateHealthRecord(snapshot.GamePid, heartbeat);
            if (heartbeat.RecoveryConfirmed && heartbeat.RecoveryReason is not null)
                await StartRecoveryAsync(heartbeat.RecoveryReason, heartbeat.RecoveryUpdateDetected).ConfigureAwait(false);

            if (!RecoveryEnabledAndDesired()) return;                        // 0x41a8c0-0x41a8e5 (state M untouched)
            if (IsActiveRecoveryState(CurrentRecoveryStatus.State)) return;  // 0x41a932-0x41a951
            if (!RecoveryRootAvailable()) return;                            // 0x41a974-0x41a97c
            RecoveryTrackedGame? tracked = recoveryTracked;
            if (tracked is null) return;                                     // 0x41a99c ctx+0x120 == 0

            long now = RecoveryEngineNowMilliseconds();
            RecoveryMonitorObservation m = monitorObservation;
            bool processPresent = ProcessMatches(tracked.Pid, tracked.Path, tracked.StartedAtUtc);   // 0x41b451
            if (!processPresent)                                             // 0x41aa30-0x41ab07
            {
                m.OfflinePid = 0;
                m.Missing = m.Missing == uint.MaxValue ? m.Missing : m.Missing + 1;
                if (m.Missing <= 1) return;
                await StartRecoveryAsync("processExit", updateDetected: false).ConfigureAwait(false);
                return;
            }

            int pid = tracked.Pid;
            bool online = heartbeat.BridgeOnline;                            // 0x2d47a6 (current-client heartbeat)
            bool healthy = IsGameHealthy(pid);                              // 0x41ba64
            bool updating = IsUpdateProcessRunning();                        // 0x41dfb1
            bool either = updating || online;
            bool hung = !either && IsOwnedProcessHung(pid, tracked.Path, tracked.StartedAtUtc);   // 0x41a472
            m.Missing = 0;                                                   // 0x41ab51
            if (either) { m.OfflinePid = 0; m.OfflineSince = 0; }            // 0x41ab5b
            else if (m.OfflinePid != pid) { m.OfflinePid = pid; m.OfflineSince = now; }
            if (healthy) { m.UnhealthyPid = 0; m.UnhealthySince = 0; }       // 0x41ab8c
            else if (m.UnhealthyPid != pid) { m.UnhealthyPid = pid; m.UnhealthySince = now; }
            string? reason = null;
            if (hung)                                                        // 0x41abb3
            {
                long hungSince;
                if (m.HungPid == pid) hungSince = m.HungSince;
                else { m.HungPid = pid; m.HungSince = now; hungSince = now; }
                if (now - hungSince >= OverviewRecoveryPolicy.HangThreshold.TotalMilliseconds &&
                    now - m.OfflineSince >= OverviewRecoveryPolicy.HangThreshold.TotalMilliseconds) reason = "hang";   // 0x41abf8/0x41ac0b
            }
            else { m.HungPid = 0; m.HungSince = 0; }
            if (reason is null && !either && now - m.OfflineSince >= OverviewRecoveryPolicy.DisconnectThreshold.TotalMilliseconds) reason = "disconnect";   // 0x41ac34 (>59999)
            if (reason is null && m.UnhealthySince > 0 && !healthy && online &&
                now - m.UnhealthySince >= OverviewRecoveryPolicy.LoginUnavailableThreshold.TotalMilliseconds) reason = "disconnect";                              // 0x41ac53
            if (reason is not null)
                await StartRecoveryAsync(reason, updateDetected: false).ConfigureAwait(false);
        }
        finally { recoverySerial.Release(); }
    }

    // ---------------------------------------------------------------------------------------------
    // start_recovery (0x41b03a) + run spawn
    // ---------------------------------------------------------------------------------------------
    private async Task StartRecoveryAsync(string reason, bool updateDetected)
    {
        if (!RecoveryEnabledAndDesired()) return;                            // same three preconditions as the tick
        RecoveryRunState? run = null;
        OverviewRecoveryStatus? published = null;
        lock (stateGate)
        {
            if (closed) return;
            if (IsActiveRecoveryState(recoveryStatus.State))                 // 0x41b121-0x41b131: only merge the flag
            {
                if (updateDetected && !recoveryStatus.UpdateDetected)
                    published = recoveryStatus = recoveryStatus with { UpdateDetected = true };
            }
            else
            {
                ulong noticeId = unchecked(recoveryNoticeId + 1);
                recoveryNoticeId = noticeId;
                long startedAt = RecoveryNow().ToUnixTimeMilliseconds();
                run = new RecoveryRunState(++recoveryRunSequence, reason, startedAt, noticeId)
                {
                    Cancellation = CancellationTokenSource.CreateLinkedTokenSource(recoveryLifetime.Token),
                };
                activeRecoveryRun = run;
                CancellationTokenSource? previous = Interlocked.Exchange(ref activeRecoveryCancellation, run.Cancellation);
                previous?.Dispose();
                published = recoveryStatus = new("waiting", reason, updateDetected, false, startedAt, null, 0, null,
                    null, noticeId, true);
            }
        }
        if (published is not null) PublishRecoveryStatus(published);
        if (run is null) return;

        await InitRecoveryRunAsync(run).ConfigureAwait(false);
        if (!run.Finished && recoveryMonitorEnabled)
            activeRecoveryRunTask = Task.Run(() => RecoveryRunLoopAsync(run));
    }

    private async Task RecoveryRunLoopAsync(RecoveryRunState run)
    {
        try
        {
            while (!run.Finished)
            {
                await RecoveryDelayAsync(RecoveryRunCadence, run.Cancellation.Token).ConfigureAwait(false);
                await RecoveryRunTickAsync(run).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            FinishRecoveryRunStopped(run);
        }
        catch (Exception ex)
        {
            FinishRecoveryRunFailed(run, RecoveryErrorCode(ex));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Run init (0xe58e1-0xe5bf8)
    // ---------------------------------------------------------------------------------------------
    private async Task InitRecoveryRunAsync(RecoveryRunState run)
    {
        try
        {
            long now = RecoveryEngineNowMilliseconds();
            if (!RecoveryRootAvailable())
            {
                FinishRecoveryRun(run, success: false, "game root is unavailable");   // 0xe5976
                return;
            }
            int pid = FindRecoveryTrackedPid();
            run.RunPid = run.LastPid = pid;
            run.StableSince = 0;
            run.LastActivity = run.LoginSince = run.Deadline = now;
            run.NormalAttempts = run.MaintenanceAttempts = 0;
            run.Maintenance = false;
            run.LogReaders = CreateRecoveryLogReaders();
            foreach (IRecoveryLogReader reader in run.LogReaders) reader.Begin();          // 0x41a18d / 0x41e950
            run.LastFingerprint = ReadUpdateActivityFingerprint();                          // 0x41dad8
            if (run.Reason == "hang" && pid != 0)                                           // 0xe5b73-0xe5bf3
            {
                (bool ok, string? error) = await TerminateTrackedGameAsync().ConfigureAwait(false);
                if (!ok)
                {
                    uint n = run.NormalAttempts;
                    run.NormalAttempts = n + 1;
                    run.Deadline = now + RetryDelayMilliseconds(normal: true, n);
                    RecoveryRetryScheduled(run, checked((int)run.NormalAttempts), run.Deadline, error);
                }
            }
        }
        catch (OperationCanceledException) { FinishRecoveryRunStopped(run); }
        catch (Exception ex) { FinishRecoveryRunFailed(run, RecoveryErrorCode(ex)); }
    }

    // ---------------------------------------------------------------------------------------------
    // Run tick (0xe5ca7-0xe6987)
    // ---------------------------------------------------------------------------------------------
    private async Task RecoveryRunTickAsync(RecoveryRunState run)
    {
        if (run.Finished) return;
        try
        {
            await RecoveryRunTickCoreAsync(run).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { FinishRecoveryRunStopped(run); }
        catch (Exception ex) { FinishRecoveryRunFailed(run, RecoveryErrorCode(ex)); }
    }

    private async Task RecoveryRunTickCoreAsync(RecoveryRunState run)
    {
        long now = RecoveryEngineNowMilliseconds();
        OverviewRecoveryStatus st = CurrentRecoveryStatus;
        if (st.NextRetryAt is long nextUnix)                                             // 0xe5cfc-0xe5d1c
        {
            long candidate = now + (nextUnix - RecoveryNow().ToUnixTimeMilliseconds());
            if (candidate < run.Deadline) run.Deadline = candidate;
        }
        RecoveryLogClassification logs = ClassifyRecoveryLogs(run.LogReaders);              // 0x419e2a
        if (logs.Activity) run.LastActivity = now;                                         // 0xe5d4c
        if (logs.UpdateDetected) RecoveryMarkUpdateDetected(run);                          // 0xe5d57-0xe5d6c
        string? fingerprint = ReadUpdateActivityFingerprint();                             // 0xe5d71-0xe5db0
        if (!string.Equals(fingerprint, run.LastFingerprint, StringComparison.Ordinal))
        {
            run.LastFingerprint = fingerprint;
            run.LastActivity = now;
            RecoveryMarkUpdateDetected(run);
        }
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        int pid = FindRecoveryTrackedPid(snapshot);                                        // 0xe5dd0
        if (pid != 0)                                                                      // 0xe5deb
        {
            if (pid != run.LastPid)
            {
                run.LastPid = pid;
                run.LoginSince = now;
                run.StableSince = 0;
            }
            if (run.RunPid != 0 && pid != run.RunPid) RecoveryMarkRestarted(run);           // 0xe5e0e-0xe5e2e
        }
        bool upd = IsUpdateProcessRunning();                                                // 0xe5e3b
        if (upd)                                                                            // 0xe5e44-0xe5e85
        {
            run.LoginSince = now;
            RecoveryMarkUpdateDetected(run);
            RecoverySetState(run, "updating", checked((int)run.NormalAttempts));
        }
        if (logs.Error is not null)                                                         // 0xe5fa3
        {
            if (pid != 0) await TerminateTrackedGameAsync().ConfigureAwait(false);
            await KillUpdateProcessesIgnoringErrorsAsync().ConfigureAwait(false);   // 0x41e613 (result dropped)
            uint n = run.NormalAttempts;
            run.NormalAttempts = n + 1;
            run.Deadline = now + RetryDelayMilliseconds(normal: true, n);
            RecoveryRetryScheduled(run, checked((int)run.NormalAttempts), run.Deadline, logs.Error);
            run.LastActivity = now;
            EndRecoveryIteration(run);
            return;
        }
        if (logs.Maintenance && !upd)                                                       // 0xe5e95-0xe5f9e
        {
            if (pid != 0) await TerminateTrackedGameAsync().ConfigureAwait(false);
            run.Maintenance = true;
            run.Deadline = now + RetryDelayMilliseconds(normal: false, run.MaintenanceAttempts);
            RecoverySetMaintenance(run, checked((int)run.MaintenanceAttempts), run.Deadline);
            run.LastActivity = now;
            EndRecoveryIteration(run);
            return;
        }
        if (upd)                                                                            // 0xe608b-0xe6444
        {
            if (now - run.LastActivity < OverviewRecoveryPolicy.UpdateNoActivityTimeout.TotalMilliseconds)
            {
                EndRecoveryIteration(run);
                return;
            }
            await KillUpdateProcessesIgnoringErrorsAsync().ConfigureAwait(false);
            uint n = run.NormalAttempts;
            run.NormalAttempts = n + 1;
            run.Deadline = now + RetryDelayMilliseconds(normal: true, n);
            RecoveryRetryScheduled(run, checked((int)run.NormalAttempts), run.Deadline,
                "game update had no activity for 15 minutes");
            run.LastActivity = now;
            EndRecoveryIteration(run);
            return;
        }

        RecoveryHeartbeatObservation heartbeat = snapshot is null || pid == 0 || snapshot.GamePid != pid
            ? new(false, false, false, false, null, false)
            : ReadRecoveryHeartbeat(snapshot);
        bool online = heartbeat.BridgeOnline;                                               // 0x2d47a6
        if (snapshot is not null && pid != 0 && snapshot.GamePid == pid) UpdateHealthRecord(pid, heartbeat);
        bool healthy = pid != 0 && IsGameHealthy(pid);                                       // 0x41ba64
        if (online && pid != 0 && healthy && pid == run.LastPid)                            // 0xe6223-0xe6233
        {
            RecoverySetState(run, "verifying", checked((int)run.ActiveAttempts));
            if (run.StableSince == 0) run.StableSince = now;
            if (now - run.StableSince >= OverviewRecoveryPolicy.StableVerification.TotalMilliseconds)   // 0xe6285
            {
                FinishRecoveryRun(run, success: true, null);
                return;
            }
            EndRecoveryIteration(run);
            return;
        }
        run.StableSince = 0;                                                                // 0xe62b9
        if (pid == 0)                                                                       // 0xe64e0
        {
            if (now < run.Deadline)
            {
                EndRecoveryIteration(run);
                return;
            }
            RecoverySetState(run, "repairing", checked((int)run.ActiveAttempts));
            uint attempt;
            if (run.Maintenance) attempt = ++run.MaintenanceAttempts;
            else attempt = ++run.NormalAttempts;
            run.LaunchAttempt = attempt;
            RecoverySetState(run, "launching", checked((int)attempt));
            string? launchError = await RecoveryLaunchAsync(run).ConfigureAwait(false);     // 0xe6bf7 (+0x2deee9)
            if (launchError is null)                                                        // 0xe6751
            {
                RecoveryMarkRestarted(run);
                run.LastActivity = now;
                run.Deadline = now + RetryDelayMilliseconds(!run.Maintenance, attempt - 1);
            }
            else if (run.Maintenance)                                                       // 0xe67a4
            {
                run.Deadline = now + RetryDelayMilliseconds(normal: false, attempt - 1);
                RecoverySetMaintenance(run, checked((int)attempt), run.Deadline);
            }
            else
            {
                run.Deadline = now + RetryDelayMilliseconds(normal: true, attempt - 1);
                RecoveryRetryScheduled(run, checked((int)attempt), run.Deadline, launchError);
            }
            EndRecoveryIteration(run);
            return;
        }
        if (!online)                                                                        // 0xe6544
        {
            if (now - run.LastActivity < OverviewRecoveryPolicy.DisconnectWaitBeforeTerminate.TotalMilliseconds)
            {
                RecoverySetState(run, "waiting", checked((int)run.NormalAttempts));
                EndRecoveryIteration(run);
                return;
            }
            (bool ok, string? error) = await TerminateTrackedGameAsync().ConfigureAwait(false);
            if (!ok)                                                                        // 0xe6ab3
            {
                uint n = run.NormalAttempts;
                run.NormalAttempts = n + 1;
                run.Deadline = now + RetryDelayMilliseconds(normal: true, n);
                RecoveryRetryScheduled(run, checked((int)run.NormalAttempts), run.Deadline, error);
            }
            else if (run.Maintenance)                                                       // 0xe6619
            {
                run.Deadline = now + RetryDelayMilliseconds(normal: false, run.MaintenanceAttempts);
                RecoverySetMaintenance(run, checked((int)run.MaintenanceAttempts), run.Deadline);
            }
            else run.Deadline = now;                                                        // 0xe6bb2
            run.LastActivity = now;
            EndRecoveryIteration(run);
            return;
        }
        RecoverySetState(run, "verifying", checked((int)run.ActiveAttempts));              // 0xe62d2
        if (now - run.LoginSince < OverviewRecoveryPolicy.LoginUnavailableThreshold.TotalMilliseconds)
        {
            EndRecoveryIteration(run);
            return;
        }
        await TerminateTrackedGameAsync().ConfigureAwait(false);                            // result ignored (0xe632c)
        run.Maintenance = true;                                                             // 0xe6342
        run.Deadline = now + RetryDelayMilliseconds(normal: false, run.MaintenanceAttempts);
        RecoverySetMaintenance(run, checked((int)run.MaintenanceAttempts), run.Deadline);
        run.LastActivity = run.LoginSince = now;
        EndRecoveryIteration(run);
    }

    // 0xe6987-0xe69cc end-of-iteration gate: armed (not closed), same run, desired running, auto reconnect.
    private void EndRecoveryIteration(RecoveryRunState run)
    {
        bool allowed;
        lock (stateGate) allowed = !closed && ReferenceEquals(activeRecoveryRun, run);
        if (!allowed || !RecoveryEnabledAndDesired()) FinishRecoveryRunStopped(run);
    }

    // ---------------------------------------------------------------------------------------------
    // Effects (current-client adaptations of the original helper calls)
    // ---------------------------------------------------------------------------------------------
    private int FindRecoveryTrackedPid(OwnedSnapshot? unused = null)
    {
        _ = unused;
        RecoveryTrackedGame? tracked = recoveryTracked;
        if (tracked is null) return 0;
        return ProcessMatches(tracked.Pid, tracked.Path, tracked.StartedAtUtc) ? tracked.Pid : 0;
    }

    private async Task KillUpdateProcessesIgnoringErrorsAsync()
    {
        try { await TerminateUpdateProcessesAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    // 0x41e543: terminate the tracked game; the lifecycle restoration that the current client needs after
    // the game exits (journal/script triplet) is part of this effect.
    private async Task<(bool Ok, string? Error)> TerminateTrackedGameAsync()
    {
        RecoveryTrackedGame? tracked = recoveryTracked;
        if (tracked is null) return (true, null);
        try
        {
            await TerminateOwnedProcessAsync(tracked.Pid, tracked.Path, tracked.StartedAtUtc,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, RecoveryErrorCode(ex));
        }
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        if (snapshot is not null)
            await CleanupExitedOwnedSessionAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
        return (true, null);
    }

    // 0xe6bf7 relaunch (+ fallback 0x2deee9): returns null on success, otherwise the error code.
    private async Task<string?> RecoveryLaunchAsync(RecoveryRunState run)
    {
        // Safety fence (adaptation): never relaunch after the user cleared desired-running (Stop).
        if (config?.Snapshot.GameDesiredRunning != true) throw new OperationCanceledException(run.Cancellation.Token);
        OwnedSnapshot? stale = GetOwnedSnapshot();
        if (stale is not null)
            await CleanupExitedOwnedSessionAsync(stale, CancellationToken.None).ConfigureAwait(false);
        try
        {
            await StartAsync(run.Cancellation.Token).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (run.Cancellation.IsCancellationRequested) { throw; }
        catch (Exception ex) { return RecoveryErrorCode(ex); }
    }

    // ---------------------------------------------------------------------------------------------
    // Status record helpers (0x41ce45 / 0x41cacb / 0x41b59e / 0x41b89f / 0x41ae75 / 0x41c448 / 0x41c201)
    // ---------------------------------------------------------------------------------------------
    // Read-modify-write of the shared status record for the active run; the change event is delivered
    // after the lock is released (handlers may call back into the lifecycle).
    private void MutateRecoveryStatus(RecoveryRunState run,
        Func<OverviewRecoveryStatus, OverviewRecoveryStatus?> mutate)
    {
        OverviewRecoveryStatus? published = null;
        lock (stateGate)
        {
            if (!ReferenceEquals(activeRecoveryRun, run)) return;
            OverviewRecoveryStatus? next = mutate(recoveryStatus);
            if (next is null || next == recoveryStatus) return;
            recoveryStatus = next;
            published = next;
        }
        PublishRecoveryStatus(published);
    }

    private void PublishRecoveryStatus(OverviewRecoveryStatus status)
    {
        try { RecoveryStatusChanged?.Invoke(status); }
        catch { }
    }

    private void RecoverySetState(RecoveryRunState run, string label, int attempts) =>
        MutateRecoveryStatus(run, s =>
            s.State == label && s.Attempts == attempts                         // dedupe on (state, attempts)
                ? null
                : s with { State = label, Attempts = attempts, NextRetryAt = null, Error = null });

    private long ToUnixDeadline(long engineDeadline) =>
        RecoveryNow().ToUnixTimeMilliseconds() + (engineDeadline - RecoveryEngineNowMilliseconds());

    private void RecoveryRetryScheduled(RecoveryRunState run, int attempts, long engineDeadline, string? error)
    {
        long unixDeadline = ToUnixDeadline(engineDeadline);
        MutateRecoveryStatus(run, s => s with
        {
            State = "waiting", Attempts = attempts, NextRetryAt = unixDeadline, Error = error,
        });
    }

    private void RecoverySetMaintenance(RecoveryRunState run, int attempts, long engineDeadline)
    {
        long unixDeadline = ToUnixDeadline(engineDeadline);
        MutateRecoveryStatus(run, s => s with
        {
            State = "maintenance", Attempts = attempts, NextRetryAt = unixDeadline, NoticeVisible = true,
        });
    }

    private void RecoveryMarkUpdateDetected(RecoveryRunState run) =>
        MutateRecoveryStatus(run, s => s.UpdateDetected ? null : s with { UpdateDetected = true });

    private void RecoveryMarkRestarted(RecoveryRunState run) =>
        MutateRecoveryStatus(run, s => s.Restarted ? null : s with { Restarted = true });

    private void FinishRecoveryRun(RecoveryRunState run, bool success, string? message)
    {
        long completedAt = RecoveryNow().ToUnixTimeMilliseconds();
        OverviewRecoveryStatus? published = null;
        lock (stateGate)
        {
            if (run.Finished || !ReferenceEquals(activeRecoveryRun, run)) return;
            run.Finished = true;
            published = recoveryStatus = recoveryStatus with
            {
                State = success ? "succeeded" : "failed",
                CompletedAt = completedAt,
                NextRetryAt = null,
                Error = success ? null : message,
                NoticeVisible = true,
            };
            ReleaseRecoveryRunLocked(run);
        }
        PublishRecoveryStatus(published);
    }

    private void FinishRecoveryRunFailed(RecoveryRunState run, string code) => FinishRecoveryRun(run, success: false, code);

    private void UpdateHealthRecord(int pid, RecoveryHeartbeatObservation heartbeat)
    {
        if (!heartbeat.BridgeOnline) return;                       // retained while the bridge is offline
        lock (stateGate)
        {
            healthRecordPid = pid;
            healthRecordValid = heartbeat.GameStateObserved && heartbeat.GameHealthy;
        }
    }

    private bool IsGameHealthy(int pid)
    {
        lock (stateGate) return pid != 0 && healthRecordPid == pid && healthRecordValid;
    }

    private void FinishRecoveryRunStopped(RecoveryRunState run)
    {
        OverviewRecoveryStatus? published = null;
        lock (stateGate)
        {
            if (run.Finished || !ReferenceEquals(activeRecoveryRun, run)) return;
            run.Finished = true;
            healthRecordPid = 0;                                   // 0x41c201 resets the game-state record
            healthRecordValid = false;
            published = recoveryStatus = IdleRecoveryStatus(noticeId: recoveryStatus.NoticeId);   // 0x41c201 keeps the notice id
            ReleaseRecoveryRunLocked(run);
        }
        PublishRecoveryStatus(published);
    }

    private void ReleaseRecoveryRunLocked(RecoveryRunState run)
    {
        activeRecoveryRun = null;
        if (ReferenceEquals(Interlocked.CompareExchange(ref activeRecoveryCancellation, null, run.Cancellation),
                run.Cancellation))
            run.Cancellation.Dispose();
    }

    private long RetryDelayMilliseconds(bool normal, uint counterBefore)
    {
        TimeSpan[] table = normal
            ? OverviewRecoveryPolicy.NormalRetryDelays
            : OverviewRecoveryPolicy.MaintenanceRetryDelays;
        return (long)table[Math.Min((int)Math.Min(counterBefore, (uint)int.MaxValue), table.Length - 1)].TotalMilliseconds;
    }

    private void MarkUnexpectedExit(OwnedSnapshot snapshot)
    {
        StopLeaseTimer(deleteLease: true, snapshot.InstanceId, snapshot.Challenge);
        lock (stateGate)
        {
            if (gamePid != snapshot.GamePid || instanceId != snapshot.InstanceId) return;
            phase = "error";
            connectionState = "recovering";
            lastError = "GAME_EXITED_RESTORE_REQUIRED";
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
        StopLeaseTimer(deleteLease: true, snapshot.InstanceId, snapshot.Challenge);
        ClearRuntimeSessionFiles(snapshot.InstanceId, snapshot.Challenge);
        lock (stateGate)
        {
            if (instanceId != snapshot.InstanceId || gamePid != snapshot.GamePid) return;
            phase = "stopped";
            connectionState = "offline";
            instanceId = null;
            instanceStartedAtUnixMilliseconds = null;
            challenge = null;
            gamePid = null;
            launcherPid = null;
            gamePath = null;
            gameStartedAtUtc = null;
            lastError = null;
            readyAtUnix = null;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Launcher/updater/player log classification (0x419e2a), readers 0x41a18d / 0x41e950 / 0x41e9bb
    // ---------------------------------------------------------------------------------------------
    internal static readonly string[] PlayerUpdateMarkers =
        ["downloadupdatestate", "download_start", "download_finish", "dll version changed"];
    internal static readonly string[] LauncherUpdateMarkers =
        ["updating from path", "installing from path", "copying launcher"];
    internal static readonly string[] PlayerErrorMarkers = ["downloadupdate error", "disk full"];
    internal static readonly string[] PlayerErrorTokens = ["e115", "e123", "e900"];
    internal static readonly string[] LauncherErrorMarkers = ["download failed", "update failed", "disk full"];
    internal static readonly string[] UpdaterErrorMarkers = ["update failed", "install failed", "disk full"];
    internal const string RecoveryLogErrorText = "game update reported an error";

    internal readonly record struct RecoveryLogClassification(bool Activity, bool UpdateDetected, bool Maintenance, string? Error);

    // 0x41d0df: case-sensitive occurrence whose neighbours are not ASCII alphanumeric.
    internal static bool ContainsToken(string haystack, string needle)
    {
        int start = 0;
        while (true)
        {
            int i = haystack.IndexOf(needle, start, StringComparison.Ordinal);
            if (i < 0) return false;
            bool beforeAlnum = i > 0 && char.IsAsciiLetterOrDigit(haystack[i - 1]);
            int end = i + needle.Length;
            bool afterAlnum = end < haystack.Length && char.IsAsciiLetterOrDigit(haystack[end]);
            if (!beforeAlnum && !afterAlnum) return true;
            start = i + 1;
        }
    }

    private static bool ContainsAny(string haystack, string[] needles)
    {
        foreach (string needle in needles)
            if (haystack.Contains(needle, StringComparison.Ordinal)) return true;
        return false;
    }

    internal static RecoveryLogClassification ClassifyRecoveryLogTexts(string player, string launcher, string updater)
    {
        bool update = ContainsAny(player, PlayerUpdateMarkers) || ContainsToken(player, "e999") ||
                      ContainsAny(launcher, LauncherUpdateMarkers);
        bool maintenance = player.Contains("connect to server failed", StringComparison.Ordinal) ||
                           (player.Contains("loading error", StringComparison.Ordinal) && ContainsToken(player, "e109"));
        bool error = ContainsAny(player, PlayerErrorMarkers) ||
                     Array.Exists(PlayerErrorTokens, token => ContainsToken(player, token)) ||
                     ContainsAny(launcher, LauncherErrorMarkers) || ContainsAny(updater, UpdaterErrorMarkers);
        bool activity = update || launcher.Length != 0 || updater.Length != 0;
        return new(activity, update, maintenance, error ? RecoveryLogErrorText : null);
    }

    private RecoveryLogClassification ClassifyRecoveryLogs(IRecoveryLogReader[] readers)
    {
        string player = readers[0].ReadNew();
        string launcher = readers[1].ReadNew();
        string updater = readers[2].ReadNew();
        return ClassifyRecoveryLogTexts(player, launcher, updater);
    }

    private IRecoveryLogReader[] CreateRecoveryLogReaders()
    {
        string directory = Path.Combine(
            Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty,
            "AppData", "LocalLow", "FunFly", "Last War-Survival Game");
        string[] names = ["Player.log", "Launcher.log", "Updater.log"];
        var readers = new IRecoveryLogReader[names.Length];
        for (int i = 0; i < names.Length; i++)
            readers[i] = testHooks?.CreateRecoveryLogReader is { } factory
                ? factory(names[i])
                : new FileRecoveryLogReader(Path.Combine(directory, names[i]));
        return readers;
    }

    private sealed record RecoveryTrackedGame(int Pid, string Path, string StartedAtUtc);

    private sealed class RecoveryMonitorObservation
    {
        internal uint Missing;          // [M+0xac]
        internal int OfflinePid;        // [M+0xb0]
        internal long OfflineSince;     // [M+0x98]
        internal int UnhealthyPid;      // [M+0xb4]
        internal long UnhealthySince;   // [M+0xa0]
        internal int HungPid;           // [M+0xa8]
        internal long HungSince;        // [M+0x90]
    }

    private sealed class RecoveryRunState
    {
        internal RecoveryRunState(long runId, string reason, long startedAtUnixMilliseconds, ulong noticeId)
        {
            RunId = runId;
            Reason = reason;
            StartedAtUnixMilliseconds = startedAtUnixMilliseconds;
            NoticeId = noticeId;
        }

        internal long RunId { get; }
        internal string Reason { get; }
        internal long StartedAtUnixMilliseconds { get; }
        internal ulong NoticeId { get; }
        internal CancellationTokenSource Cancellation { get; init; } = null!;
        internal bool Finished;
        internal long StableSince;         // [+0x68]
        internal long LastActivity;        // [+0x70]
        internal long LoginSince;          // [+0x78]
        internal long Deadline;            // [+0x80]
        internal int RunPid;               // [+0x108]
        internal int LastPid;              // [+0x10c]
        internal uint NormalAttempts;      // [+0x110]
        internal uint MaintenanceAttempts; // [+0x114]
        internal bool Maintenance;         // [+0x118]
        internal uint LaunchAttempt;       // [+0x120]
        internal string? LastFingerprint;
        internal IRecoveryLogReader[] LogReaders = [];
        internal uint ActiveAttempts => Maintenance ? MaintenanceAttempts : NormalAttempts;
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
                    // HOME 009 R1 B: the image path is verified on the handle that is terminated (no PID reopen).
                    // 0x41e613 does not wait for exit; bound the wait so an unkillable process cannot stall recovery.
                    using var exitBound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    exitBound.CancelAfter(TimeSpan.FromSeconds(10));
                    OwnedProcessTerminationResult result;
                    try
                    {
                        result = await OwnedProcessTermination.TerminateAsync(
                            OwnedProcessApi, candidate.Id, expectedPath, null, RecoveryDelayAsync, exitBound.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { continue; }
                    if (result is OwnedProcessTerminationResult.OpenDenied or OwnedProcessTerminationResult.TerminateFailed)
                        throw new BridgeCommandException("UPDATE_RECOVERY_TERMINATE_FAILED", $"Unable to terminate exact updater process {candidate.Id}.");
                }
            }
        }
    }

    private IOwnedProcessApi OwnedProcessApi => testHooks?.OwnedProcessApi ?? Win32OwnedProcessApi.Instance;

    internal async Task TerminateOwnedProcessAsync(int pid, string expectedPath, string expectedStartedAtUtc, CancellationToken cancellationToken)
    {
        if (testHooks?.TerminateOwnedProcessAsync is { } test)
        {
            await test(pid, expectedPath, expectedStartedAtUtc, cancellationToken).ConfigureAwait(false);
            return;
        }
        // HOME 009 R1 B: identity (image path + creation incarnation) is verified on the very handle that is
        // terminated and awaited; the PID is never reopened after validation.
        OwnedProcessTerminationResult result = await OwnedProcessTermination.TerminateAsync(
            OwnedProcessApi, pid, expectedPath, expectedStartedAtUtc, RecoveryDelayAsync, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case OwnedProcessTerminationResult.Terminated:
                return;
            case OwnedProcessTerminationResult.NotVerified:
            case OwnedProcessTerminationResult.QueryFailed:
                throw new BridgeCommandException("PROCESS_IDENTITY_CHANGED", "Unable to verify the exact owned game process incarnation.");
            case OwnedProcessTerminationResult.OpenDenied:
                throw new BridgeCommandException("GAME_RECOVERY_TERMINATE_FAILED", "Unable to open the exact owned game process for recovery termination.");
            default:
                throw new BridgeCommandException("GAME_RECOVERY_TERMINATE_FAILED", "Unable to terminate the exact owned game process for recovery.");
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
}
