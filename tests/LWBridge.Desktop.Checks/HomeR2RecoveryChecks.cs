using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Real OverviewLifecycleService command/monitor/run/effect boundaries. No game
// processes or installed game data; the process and helper effects are inert.
internal static class HomeR2RecoveryChecks
{
    private static void Require(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("HOME004_R2_RECOVERY: " + what);
    }

    private sealed class EmptyLog : IRecoveryLogReader
    {
        public void Begin() { }
        public string ReadNew() => string.Empty;
    }
    private sealed class HookLog(Func<string> read) : IRecoveryLogReader
    {
        public void Begin() { }
        public string ReadNew() => read();
    }

    private sealed class Case : IDisposable
    {
        internal readonly string Root;
        internal readonly string Profile;
        internal readonly LocalConfigStore Config;
        internal readonly OverviewLifecycleService Service;
        internal readonly LWBridgeControlPipeHostState? Host;
        internal long Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        internal int StartCalls, StopCalls, TerminateCalls, Pid = 31000;
        internal bool Alive;
        internal bool Hung, Updating = false, StateObserved = true, Healthy = true, BridgeOnline = true;
        internal bool FailNextLaunch;
        internal string PlayerLog = "", LauncherLog = "", UpdaterLog = "";
        internal int UpdateTerminationCalls;
        internal string Session = "", Challenge = "";
        internal readonly string Exe;
        internal readonly string Created;
        internal TaskCompletionSource? HeldTerminate = null;
        internal TaskCompletionSource? TerminationReached = null;
        internal TaskCompletionSource? HeldOldStopAcknowledgement = null;
        internal TaskCompletionSource? OldStopAcknowledgementReached = null;
        internal TaskCompletionSource? HeldRecoveryStart = null;
        internal TaskCompletionSource? RecoveryStartReached = null;
        internal byte[]? GameReport;

        internal Case(string name, bool enabled, bool withActualAdoptionRecord = false)
        {
            Root = Path.Combine(Path.GetTempPath(), "home004-r2-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "Game"));
            Exe = Path.Combine(Root, "Game", "LastWar.exe");
            File.WriteAllBytes(Exe, []);
            Profile = "r2-" + name;
            Created = DateTimeOffset.UtcNow.AddMinutes(-3).UtcDateTime.ToString("O");
            Config = new LocalConfigStore(persistent: false,
                initialValue: LWBridgeLocalConfig.CreateDefault() with
                { ProfileId = Profile, AutoReconnect = enabled, GameDesiredRunning = false });
            var hooks = new OverviewLifecycleTestHooks
            {
                UtcNow = () => DateTimeOffset.FromUnixTimeMilliseconds(Now),
                MonotonicMilliseconds = () => Now,
                SelectedGamePids = _ => [],
                GameRootAvailable = () => true,
                ProcessMatches = (pid, path, creation) => Alive && pid == Pid &&
                    string.Equals(path, Exe, StringComparison.OrdinalIgnoreCase) &&
                    creation == Created,
                ReadAllBytes = path => BridgeOnline && path.EndsWith("heartbeat.json", StringComparison.OrdinalIgnoreCase)
                    ? MakeHeartbeat() : path.EndsWith("game-reported.txt", StringComparison.OrdinalIgnoreCase)
                    && GameReport is not null ? GameReport
                    : File.ReadAllBytes(path),
                RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                WriteLease = withActualAdoptionRecord ? null : (_, _, _) => { },
                DeleteFile = _ => { },
                DelayAsync = (_, token) => Task.Delay(1, token),
                CreateRecoveryLogReader = name => new HookLog(() => name switch
                {
                    "Player.log" => ConsumeLog(ref PlayerLog),
                    "Launcher.log" => ConsumeLog(ref LauncherLog),
                    "Updater.log" => ConsumeLog(ref UpdaterLog),
                    _ => string.Empty,
                }),
                UpdateProcessRunning = () => Updating,
                UpdateActivityFingerprint = () => "constant",
                TerminateUpdateProcessesAsync = _ =>
                {
                    UpdateTerminationCalls++;
                    Updating = false;
                    return Task.CompletedTask;
                },
                ProcessHung = (_, _) => Hung,
                TerminateOwnedProcessAsync = async (pid, path, created, _) =>
                {
                    TerminateCalls++;
                    Require(pid == Pid && path == Exe && created == Created, "terminate exact process identity");
                    TerminationReached?.TrySetResult();
                    if (HeldTerminate is not null) await HeldTerminate.Task;
                    if (Pid == pid) Alive = false;
                },
                RunHelperAsync = async (inv, _) =>
                {
                    if (inv.Operation == "stop")
                    {
                        StopCalls++;
                        Require(inv.ProfileId == Profile && inv.GamePath == Exe &&
                                inv.GameStartedAtUtc == Created, "stop exact issued game path");
                        if (HeldOldStopAcknowledgement is not null &&
                            OldStopAcknowledgementReached is not null &&
                            !OldStopAcknowledgementReached.Task.IsCompleted)
                        {
                            OldStopAcknowledgementReached.TrySetResult();
                            await HeldOldStopAcknowledgement.Task;
                        }
                        // An obsolete helper acknowledgement may arrive AFTER a
                        // successor was published. Only an exact owner may affect Alive.
                        if (inv.SessionId == Session && inv.Challenge == Challenge &&
                            inv.GamePid == Pid) Alive = false;
                        return JsonSerializer.SerializeToElement(new
                        {
                            mode = "overview_exact_pid_close_restore",
                            bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = inv.SessionId, gamePid = inv.GamePid,
                            gamePath = Exe, gameStartedAtUtc = Created, gameRunning = false,
                            installedFilesChanged = false,
                            close = new { accepted = true, processExited = true, alreadyExited = false },
                            restore = new { restored = true }
                        });
                    }
                    StartCalls++;
                    if (StartCalls > 1 && HeldRecoveryStart is not null)
                    {
                        RecoveryStartReached?.TrySetResult();
                        // Simulates the helper completing successfully after a
                        // user Stop/cancellation; the lifecycle must restore
                        // this exact late result, without publishing a successor.
                        await HeldRecoveryStart.Task;
                    }
                    if (FailNextLaunch)
                    {
                        FailNextLaunch = false;
                        throw new BridgeCommandException("LAUNCH_FAILED", "inert launch failure");
                    }
                    Pid++;
                    Session = inv.SessionId!;
                    Challenge = inv.Challenge!;
                    Alive = true;
                    GameReport = Encoding.UTF8.GetBytes(
                        "schema=1\nsessionId=" + Session + "\nchallenge=" + Challenge +
                        "\ndeadlineMilliseconds=" + (Now + 90000) +
                        "\ngamePid=" + Pid + "\n");
                    long unix = Now / 1000;
                    return JsonSerializer.SerializeToElement(new
                    {
                        mode = "overview_install_launch_ready_deferred_restore",
                        bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId = Profile, sessionId = Session,
                        challengeSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Challenge))).ToLowerInvariant(),
                        gamePid = Pid, launcherPid = Pid + 1000, gamePath = Exe, gameStartedAtUtc = Created,
                        gameRunning = true, installedFilesChanged = true,
                        restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                        ready = new
                        {
                            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = Session, challenge = Challenge,
                            gamePid = Pid, ready = true, messageVisible = true,
                            messageText = OverviewLifecycleService.ReadyMessage,
                            readyAt = unix, updatedAt = unix
                        }
                    });
                }
            };
            if (withActualAdoptionRecord)
                Host = new LWBridgeControlPipeHostState(
                    pipePath: @"\\.\pipe\home-r2-adoption-" + Guid.NewGuid().ToString("N"));
            Service = new OverviewLifecycleService(Profile, Root, config: Config, testHooks: hooks,
                startRecoveryMonitor: false, requireCurrentClientEvidence: false,
                runtimeRoot: Path.Combine(Root, "runtime"),
                applicationDataRoot: Root, backupRoot: Path.Combine(Root, "backup"),
                bridgeHostState: Host, enableBridgeControlPipeLaunchBinding: withActualAdoptionRecord);
        }
        private static string ConsumeLog(ref string line)
        {
            string value = line;
            line = "";
            return value;
        }

        private byte[] MakeHeartbeat() => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = Profile, sessionId = Session, challenge = Challenge, gamePid = Pid,
            updatedAt = Now / 1000, ready = true, messageVisible = true,
            messageText = OverviewLifecycleService.ReadyMessage,
            gameStateObserved = StateObserved, gameReady = Healthy, loggedIn = Healthy,
            connected = Healthy, connecting = false, gameUid = "test-id", serverId = 2212,
            worldPos = 123456789L
        });
        internal void Advance(long milliseconds) => Now += milliseconds;
        internal Task Observe() => Service.RunRecoveryObservationForTestAsync();
        internal Task Tick() => Service.RunRecoveryRunTickForTestAsync();
        internal async Task Start()
        {
            await Service.InvokeAsync("profile_instance_start",
                JsonSerializer.SerializeToElement(new { closeUnmanaged = false }), CancellationToken.None);
            Require(Alive && Config.Snapshot.GameDesiredRunning, "native Start desired and owner");
        }
        internal async Task Stop()
        {
            await Service.InvokeAsync("profile_instance_stop", JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None);
        }
        internal void Toggle(bool enabled)
        {
            Config.Update(c => c with { AutoReconnect = enabled });
            Service.NotifyAutomationChanged(enabled);
        }
        public void Dispose()
        {
            HeldTerminate?.TrySetResult();
            HeldOldStopAcknowledgement?.TrySetResult();
            HeldRecoveryStart?.TrySetResult();
            Service.Dispose();
            Host?.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    internal static async Task RunAsync()
    {
        await ProcessExitOffAndOn();
        await RecoveryInstanceStatusProjection();
        await RealAdoptionRecordRecovery();
        await FailedLaunchAndRetry();
        await OriginalMaintenanceRetryLadder();
        await OriginalUpdaterStallDeadline();
        await DisableAndStop();
        await HungAndDisconnectedEdges();
        await StillAliveOfflineRecoveryRunEdges();
        await HeldOldEffectVsStopSuccessor();
        await HeldOldStopAckVsProtectedSuccessor();
        await StopWhileRecoveryLaunchIsPending();
        await ExplicitPendingRecoveryStopTargets();
        await PendingRecoveryFaultAndHostClose();
        await LoginUnavailableThreshold();
        Require(OverviewRecoveryPolicy.NormalRetryDelays.Select(d => d.TotalMilliseconds)
            .SequenceEqual(new double[] { 15000, 30000, 60000, 120000, 300000 }),
            "original normal counter table (clamped on attempts >=5)");
        Require(OverviewRecoveryPolicy.MaintenanceRetryDelays.Select(d => d.TotalMilliseconds)
            .SequenceEqual(new double[] { 120000, 300000, 600000 }),
            "original maintenance retry table");
        Console.WriteLine("HOME004_R2_NATIVE_MONITOR_OK off/on exit, exact hang/disconnect/login boundaries, failed retry/stable, disable, Stop, held stale cleanup/successor; game launches=0");
    }

    private static async Task StopWhileRecoveryLaunchIsPending()
    {
        using var c = new Case("pending-recovery-stop", true,
            withActualAdoptionRecord: true);
        await c.Start();
        string original = c.Session;
        string adoption = Path.Combine(c.Root, "runtime", "adoption.json");
        c.Alive = false;
        await c.Observe();
        c.Advance(2000);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.State == "waiting",
            "process exit schedules pending recovery before relaunch");
        c.HeldRecoveryStart = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        c.RecoveryStartReached = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task pending = c.Tick();
        await c.RecoveryStartReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.StartCalls == 2 && c.Session == original && !c.Alive,
            "recovery helper is pending after old exact owner cleanup");
        await c.Stop();
        Require(!c.Config.Snapshot.GameDesiredRunning &&
                c.Service.CurrentRecoveryStatus.State == "idle",
            "user Stop cancels pending recovery helper without requiring an active PID");
        c.HeldRecoveryStart.TrySetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Require(!c.Alive && c.StopCalls == 2 &&
                !c.Config.Snapshot.GameDesiredRunning &&
                !File.Exists(adoption) &&
                c.Service.CurrentRecoveryStatus.State == "idle",
            "late helper success is restored, never publishes or adopts a successor after Stop");
        c.Advance(120_000);
        await c.Tick();
        Require(c.StartCalls == 2 && !c.Alive,
            "late run cannot relaunch after the user's Stop");
    }

    private static async Task ExplicitPendingRecoveryStopTargets()
    {
        foreach (string variant in new[] { "current", "absent", "nonstring" })
        {
            using var c = new Case("pending-target-" + variant, true, withActualAdoptionRecord: true);
            await c.Start();
            string oldId = c.Session;
            c.Alive = false;
            await c.Observe(); c.Advance(2000); await c.Observe();
            c.HeldRecoveryStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            c.RecoveryStartReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task pending = c.Tick();
            await c.RecoveryStartReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            JsonElement status = JsonSerializer.SerializeToElement(c.Service.CreateProfileInstanceStatus());
            string currentId = status.GetProperty("instanceId").GetString()!;
            Require(currentId != oldId && c.Config.Snapshot.GameDesiredRunning,
                "pending recovery publishes a different exact owner before a PID");
            try
            {
                await c.Service.InvokeAsync("profile_instance_stop",
                    JsonSerializer.SerializeToElement(new { instanceId = oldId }), CancellationToken.None);
                throw new InvalidOperationException("stale explicit Close accepted during pending recovery");
            }
            catch (BridgeCommandException error) when (error.Code == "INSTANCE_MISMATCH") { }
            Require(c.Config.Snapshot.GameDesiredRunning &&
                    c.Service.CurrentRecoveryStatus.State != "idle" && c.StartCalls == 2,
                "stale explicit Close cannot cancel the replacement or clear desired-running");
            try
            {
                await c.Service.InvokeAsync("profile_instance_start",
                    JsonSerializer.SerializeToElement(new { closeUnmanaged = true }), CancellationToken.None);
                throw new InvalidOperationException("manual Start was admitted during pending automatic recovery");
            }
            catch (BridgeCommandException error) when (error.Code == "PROFILE_ALREADY_RUNNING") { }
            Require(c.Service.CurrentRecoveryStatus.State != "idle" && c.Config.Snapshot.GameDesiredRunning,
                "rejected manual Start cannot reset the active pending recovery status");
            JsonElement stopPayload = variant switch
            {
                "current" => JsonSerializer.SerializeToElement(new { instanceId = currentId }),
                "nonstring" => JsonSerializer.SerializeToElement(new { instanceId = 99 }),
                _ => JsonSerializer.SerializeToElement(new { }),
            };
            await c.Service.InvokeAsync("profile_instance_stop", stopPayload, CancellationToken.None);
            Require(!c.Config.Snapshot.GameDesiredRunning &&
                    c.Service.CurrentRecoveryStatus.State == "idle",
                "valid optional-ID Stop cancels the matching pending recovery: " + variant);
            c.HeldRecoveryStart.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Require(!c.Alive && c.StopCalls == 2 &&
                    c.Service.CurrentRecoveryStatus.State == "idle",
                "late helper cannot publish a cancelled pending owner: " + variant);
        }
    }

    private static async Task PendingRecoveryFaultAndHostClose()
    {
        foreach (bool closingHost in new[] { false, true })
        {
            using var c = new Case(closingHost ? "pending-host-close" : "pending-helper-fault",
                true, withActualAdoptionRecord: true);
            await c.Start();
            string oldId = c.Session;
            c.Alive = false;
            await c.Observe(); c.Advance(2000); await c.Observe();
            c.HeldRecoveryStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            c.RecoveryStartReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task pending = c.Tick();
            await c.RecoveryStartReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (closingHost)
                c.Service.Close();
            else
            {
                await c.Service.InvokeAsync("profile_instance_stop",
                    JsonSerializer.SerializeToElement(new { instanceId = JsonSerializer.SerializeToElement(
                        c.Service.CreateProfileInstanceStatus(), JsonOptions.Default)
                        .GetProperty("instanceId").GetString() }),
                    CancellationToken.None);
                c.FailNextLaunch = true;
            }
            c.HeldRecoveryStart.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Require(!c.Alive && c.StartCalls == 2 &&
                    c.Service.CurrentRecoveryStatus.State == "idle",
                closingHost ? "closed host cannot adopt its pending recovery helper" :
                    "late helper failure cannot re-arm recovery after explicit user Stop");
            if (!closingHost)
                Require(!c.Config.Snapshot.GameDesiredRunning,
                    "helper fault after user Stop cannot restore desired-running");
            if (closingHost)
                Require(c.Session != oldId && c.StopCalls == 2,
                    "late helper really spawned a new owner and the closed host restored it exactly");
            else
                Require(c.Session == oldId,
                    "faulted helper never spawned a successor after explicit user Stop");
        }
    }

    private static async Task ProcessExitOffAndOn()
    {
        using (var disabled = new Case("disabled", false))
        {
            await disabled.Start();
            disabled.Alive = false;
            await disabled.Observe();
            disabled.Advance(2000);
            await disabled.Observe();
            Require(disabled.Service.CurrentRecoveryStatus.State == "idle" && disabled.StartCalls == 1,
                "OFF two missing observations must not schedule/relaunch");
            await disabled.Tick();
            Require(disabled.StartCalls == 1, "OFF run tick cannot relaunch");
            await disabled.Stop();
            Require(!disabled.Config.Snapshot.GameDesiredRunning && disabled.StopCalls == 1,
                "OFF user Stop must restore exact exited session");
        }
        using var enabled = new Case("enabled", true);
        await enabled.Start();
        enabled.Alive = false;
        await enabled.Observe();
        Require(enabled.Service.CurrentRecoveryStatus.State == "idle", "first missing tick insufficient");
        enabled.Advance(2000);
        await enabled.Observe();
        Require(enabled.Service.CurrentRecoveryStatus.State == "waiting" &&
                enabled.Service.CurrentRecoveryStatus.Reason == "processExit", "second missing tick starts processExit");
        await enabled.Tick();
        Require(enabled.StartCalls == 2 && enabled.StopCalls == 1 && enabled.Alive,
            "ON first due tick restores old journal and starts new owned process");
        Require(enabled.Service.CurrentRecoveryStatus.Restarted, "successful recovery marked restarted");
        enabled.Advance(2000);
        await enabled.Tick();
        Require(enabled.Service.CurrentRecoveryStatus.State == "verifying", "new exact heartbeat enters verifying");
        enabled.Advance(14999);
        await enabled.Tick();
        Require(enabled.Service.CurrentRecoveryStatus.State == "verifying", "stable <15 seconds cannot succeed");
        enabled.Advance(1);
        await enabled.Tick();
        Require(enabled.Service.CurrentRecoveryStatus.State == "succeeded", "stable >=15 sec succeeds");
        await enabled.Stop();
        Require(!enabled.Config.Snapshot.GameDesiredRunning, "user Stop clears desire");
    }

    private static async Task RecoveryInstanceStatusProjection()
    {
        using var c = new Case("recovery-instance-projection", true);
        await c.Start();
        c.Alive = false;
        await c.Observe(); c.Advance(2000); await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.State == "waiting" &&
                c.Service.CurrentRecoveryStatus.Reason == "processExit",
            "native recovery is active for a missing exact game process");
        JsonElement instance = JsonSerializer.SerializeToElement(c.Service.CreateProfileInstanceStatus(), JsonOptions.Default);
        Require(instance.GetProperty("connectionState").GetString() == "recovering" &&
                instance.GetProperty("lastError").GetString() == "GAME_EXITED_RESTORE_REQUIRED",
            "H-42 original recovering projection wins over the stale exited-owner failure during automatic recovery");
        await c.Stop();
    }

    private static async Task RealAdoptionRecordRecovery()
    {
        using var c = new Case("protected-adoption-commit", true, withActualAdoptionRecord: true);
        await c.Start();
        string originalSession = c.Session;
        string path = Path.Combine(c.Root, "runtime", "adoption.json");
        Require(File.Exists(path), "first start committed an actual protected adoption record");
        c.Alive = false;
        await c.Observe();
        c.Advance(2000);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.Reason == "processExit", "actual protected owner triggers recovery");
        await c.Tick();
        Require(c.Alive && c.StartCalls == 2 && c.StopCalls == 1 &&
                c.Service.CurrentRecoveryStatus.Restarted && File.Exists(path),
            "native successor must launch, old session must restore exactly once and persist replacement record: " +
            "starts=" + c.StartCalls + " stops=" + c.StopCalls +
            " recovery=" + c.Service.CurrentRecoveryStatus);
        Require(OverviewAdoptionRecord.TryDeserialize(File.ReadAllBytes(path), out var stored) &&
                stored is not null && stored.InstanceId == c.Session &&
                stored.InstanceId != originalSession,
            "protected adoption file belongs solely to replacement session");
        await c.Stop();
        Require(!File.Exists(path), "native user Stop removes exact successor adoption record");
    }

    private static async Task FailedLaunchAndRetry()
    {
        using var c = new Case("failed-retry", true);
        await c.Start();
        c.Alive = false;
        await c.Observe(); c.Advance(2000); await c.Observe();
        c.FailNextLaunch = true;
        await c.Tick();
        var st = c.Service.CurrentRecoveryStatus;
        Require(st.State == "waiting" && st.Attempts == 1 && st.Error == "LAUNCH_FAILED" &&
                st.NextRetryAt == c.Now + 15000 && c.StartCalls == 2,
            "first native failed helper schedules original 15s retry");
        c.Advance(14999);
        await c.Tick();
        Require(c.StartCalls == 2, "retry cannot start 1ms early");
        c.Advance(1);
        await c.Tick();
        Require(c.StartCalls == 3 && c.Alive, "retry at exact threshold launches");
        await c.Stop();
    }

    private static async Task OriginalMaintenanceRetryLadder()
    {
        using var c = new Case("maintenance-retries", true);
        await c.Start();
        c.Alive = false;
        await c.Observe(); c.Advance(2000); await c.Observe();
        c.PlayerLog = "connect to server failed";
        await c.Tick();
        Require(c.Service.CurrentRecoveryStatus.State == "maintenance" &&
                c.Service.CurrentRecoveryStatus.NextRetryAt == c.Now + 120000 &&
                c.StartCalls == 1,
            "real log classifier enters original first 120-second maintenance wait");
        foreach ((long wait, long nextWait, int attempt) in new[]
        {
            (120000L, 120000L, 1),
            (120000L, 300000L, 2),
            (300000L, 600000L, 3),
            (600000L, 600000L, 4),
        })
        {
            c.FailNextLaunch = true;
            c.Advance(wait - 1); await c.Tick();
            Require(c.StartCalls == attempt,
                "maintenance cannot retry 1ms early, attempt " + attempt);
            c.Advance(1); await c.Tick();
            Require(c.StartCalls == attempt + 1 &&
                    c.Service.CurrentRecoveryStatus.State == "maintenance" &&
                    c.Service.CurrentRecoveryStatus.NextRetryAt == c.Now + nextWait &&
                    c.Service.CurrentRecoveryStatus.Attempts == attempt,
                "maintenance bounded 120/300/600/600-second retry ladder, attempt " + attempt);
        }
        await c.Stop();
    }

    private static async Task OriginalUpdaterStallDeadline()
    {
        using var c = new Case("updater-stall", true);
        await c.Start();
        c.Alive = false;
        await c.Observe(); c.Advance(2000); await c.Observe();
        c.Updating = true;
        await c.Tick();
        Require(c.Service.CurrentRecoveryStatus.State == "updating" &&
                c.UpdateTerminationCalls == 0,
            "active updater enters original updating state without premature termination");
        c.Advance(900000 - 1); await c.Tick();
        Require(c.UpdateTerminationCalls == 0 && c.StartCalls == 1,
            "updater remains active below 15-minute no-activity threshold");
        c.Advance(1); await c.Tick();
        Require(c.UpdateTerminationCalls == 1 && !c.Updating &&
                c.Service.CurrentRecoveryStatus.State == "waiting" &&
                c.Service.CurrentRecoveryStatus.Error == "game update had no activity for 15 minutes" &&
                c.Service.CurrentRecoveryStatus.NextRetryAt == c.Now + 15000 &&
                c.StartCalls == 1,
            "original 15-minute update stall schedules first normal 15-second retry");
        await c.Stop();
    }

    private static async Task DisableAndStop()
    {
        using var c = new Case("disable-stop", true);
        await c.Start();
        c.Alive = false;
        await c.Observe(); c.Advance(2000); await c.Observe();
        c.Toggle(false);
        await c.Tick();
        // Original 0xe69c3 reads persisted auto-force-update-reload at the
        // END of an already-running iteration. One in-flight effect can
        // finish after OFF; it must then go idle, not start a new run.
        Require(c.StartCalls == 2 && c.Service.CurrentRecoveryStatus.State == "idle",
            "disabling retains one in-flight iteration then goes idle; starts=" +
            c.StartCalls + " state=" + c.Service.CurrentRecoveryStatus.State);
        await c.Stop();
        c.Toggle(true);
        c.Advance(2000);
        await c.Observe();
        await c.Tick();
        Require(c.StartCalls == 2 && !c.Config.Snapshot.GameDesiredRunning,
            "Stop clears desired and prevents a late relaunch even after enabling");
    }

    private static async Task HungAndDisconnectedEdges()
    {
        using var c = new Case("threshold", true);
        await c.Start();
        c.BridgeOnline = false;
        c.Hung = true;
        await c.Observe();
        c.Advance(29999);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.State == "idle", "hang 29999ms below threshold");
        c.Advance(1);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.Reason == "hang",
            "original hang and offline 30s must start run");
        Require(c.TerminateCalls == 1 && c.StopCalls == 1,
            "hang recovery init immediately terminates and restores captured owner");

        using var d = new Case("disconnect", true);
        await d.Start();
        d.BridgeOnline = false;
        await d.Observe();
        d.Advance(59999);
        await d.Observe();
        Require(d.Service.CurrentRecoveryStatus.State == "idle", "disconnect 59999ms below threshold");
        d.Advance(1);
        await d.Observe();
        Require(d.Service.CurrentRecoveryStatus.Reason == "disconnect",
            "disconnected 60000ms starts run");
        await d.Stop();
    }

    private static async Task LoginUnavailableThreshold()
    {
        using var c = new Case("login-unavailable", true);
        await c.Start();
        c.Healthy = false; // fresh authenticated transport but no valid logged-in game state
        await c.Observe();
        c.Advance(179999);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.State == "idle",
            "online but unhealthy at 179999ms cannot recover");
        c.Advance(1);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.Reason == "disconnect",
            "online unhealthy at >=180000ms starts disconnect recovery");
        await c.Stop();
    }

    // Real monitor/run methods: original 0x41ac34 disconnect classification
    // and 0xe6544 terminate check each use their own exact 60000ms clock.
    private static async Task StillAliveOfflineRecoveryRunEdges()
    {
        using (var off = new Case("still-alive-off", false))
        {
            await off.Start();
            int ownedPid = off.Pid;
            off.BridgeOnline = false;
            off.Hung = false;
            await off.Observe();
            off.Advance(59999); await off.Observe();
            off.Advance(1); await off.Observe();
            Require(off.Alive && off.Pid == ownedPid && off.StartCalls == 1 &&
                    off.TerminateCalls == 0 && off.Service.CurrentRecoveryStatus.State == "idle",
                "OFF at threshold leaves exact tracked process alive and no recovery");
            await off.Stop();
        }
        using var on = new Case("still-alive-on", true);
        await on.Start();
        int originalPid = on.Pid;
        on.BridgeOnline = false;
        on.Hung = false;
        await on.Observe();
        on.Advance(59999); await on.Observe();
        Require(on.Alive && on.TerminateCalls == 0 && on.Service.CurrentRecoveryStatus.State == "idle",
            "ON monitor at 59999ms leaves offline process alive");
        on.Advance(1); await on.Observe();
        Require(on.Alive && on.Pid == originalPid && on.TerminateCalls == 0 &&
                on.Service.CurrentRecoveryStatus.Reason == "disconnect" &&
                on.Service.CurrentRecoveryStatus.State == "waiting",
            "ON monitor at 60000ms classifies disconnect while process still alive");
        await on.Tick();
        on.Advance(59999); await on.Tick();
        Require(on.Alive && on.TerminateCalls == 0 && on.StopCalls == 0,
            "run waits separate 59999ms before terminating disconnected process");
        on.Advance(1); await on.Tick();
        Require(!on.Alive && on.TerminateCalls == 1 && on.StopCalls == 1 && on.StartCalls == 1,
            "run terminates and restores exact owner at independent 60000ms threshold");
        on.BridgeOnline = true;
        await on.Tick();
        Require(on.Alive && on.Pid != originalPid && on.StartCalls == 2,
            "next native run tick starts a successor after disconnect cleanup");
        await on.Stop();
    }

    private static async Task HeldOldEffectVsStopSuccessor()
    {
        using var c = new Case("held-effect", true);
        await c.Start();
        int oldPid = c.Pid;
        string oldSession = c.Session;
        c.BridgeOnline = false;
        c.Hung = true;
        c.HeldTerminate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        c.TerminationReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await c.Observe();
        c.Advance(30000);
        Task heldObservation = c.Observe(); // actual hang init awaits termination effect
        await c.TerminationReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.TerminateCalls == 1, "stale termination reached after exact process verification");
        // A user Stop retires the old lifecycle while its effect is parked.
        await c.Stop();
        Require(!c.Config.Snapshot.GameDesiredRunning &&
                c.Service.CurrentRecoveryStatus.State == "idle", "user Stop retires active run");
        c.BridgeOnline = true;
        c.Hung = false;
        await c.Start();
        int successorPid = c.Pid;
        string successorSession = c.Session;
        Require(successorPid != oldPid && successorSession != oldSession && c.Alive,
            "new Start must be independent owner");
        c.HeldTerminate!.TrySetResult();
        await heldObservation.WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.Pid == successorPid && c.Session == successorSession && c.Alive &&
                c.StopCalls == 1 && c.StartCalls == 2 &&
                c.Service.CurrentRecoveryStatus.State == "idle",
            "late old termination must not clear/stop new owner or publish stale recovery status");
        await c.Stop();
    }

    private static async Task HeldOldStopAckVsProtectedSuccessor()
    {
        using var c = new Case("delayed-stop-ack", true, withActualAdoptionRecord: true);
        await c.Start();
        string oldSession = c.Session;
        string adoption = Path.Combine(c.Root, "runtime", "adoption.json");
        string lease = Path.Combine(c.Root, "runtime", "lease.txt");
        Require(File.Exists(adoption) && c.Host!.GetPendingExpiration(oldSession) is not null,
            "old native launch has protected adoption and actual pipe registration");
        c.BridgeOnline = false;
        c.Hung = true;
        c.HeldOldStopAcknowledgement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        c.OldStopAcknowledgementReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await c.Observe();
        c.Advance(29999);
        await c.Observe();
        Require(c.Service.CurrentRecoveryStatus.State == "idle", "held cleanup not admitted at hang 29999ms");
        c.Advance(1);
        Task heldObservation = c.Observe();
        await c.OldStopAcknowledgementReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.StopCalls == 1 && c.Service.CurrentRecoveryStatus.Reason == "hang",
            "old exact helper Stop acknowledgement held after production hang detector");

        // User Stop retires the run while its OLD helper completion is pending.
        // Its own helper acknowledgement remains unblocked.
        await c.Stop();
        c.BridgeOnline = true;
        c.Hung = false;
        await c.Start();
        string successorSession = c.Session;
        string successorChallenge = c.Challenge;
        int successorPid = c.Pid;
        Require(successorSession != oldSession &&
                OverviewAdoptionRecord.TryDeserialize(File.ReadAllBytes(adoption), out var before) &&
                before is not null && before.InstanceId == successorSession &&
                c.Host!.GetPendingExpiration(successorSession) is not null,
            "successor has new protected adoption record and real pipe registration");
        // Timer callback is asynchronous; observe the real current-owner lease producer.
        for (int i = 0; i < 100 && !File.Exists(lease); i++)
            await Task.Delay(20);
        Require(File.Exists(lease) &&
                File.ReadAllText(lease).Contains("sessionId=" + successorSession + "\n", StringComparison.Ordinal) &&
                File.ReadAllText(lease).Contains("challenge=" + successorChallenge + "\n", StringComparison.Ordinal),
            "successor lease is written by the production renewal timer");

        c.HeldOldStopAcknowledgement!.TrySetResult();
        await heldObservation.WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.Alive && c.Pid == successorPid && c.Session == successorSession &&
                c.Service.CurrentRecoveryStatus.State == "idle" &&
                OverviewAdoptionRecord.TryDeserialize(File.ReadAllBytes(adoption), out var after) &&
                after is not null && after.InstanceId == successorSession &&
                c.Host!.GetPendingExpiration(successorSession) is not null &&
                File.Exists(lease) &&
                File.ReadAllText(lease).Contains("sessionId=" + successorSession + "\n", StringComparison.Ordinal),
            "obsolete acknowledged cleanup cannot retire successor adoption, registration, lease or state");
        await c.Stop();
        Require(!File.Exists(adoption) && !File.Exists(lease) &&
                c.Host!.GetPendingExpiration(successorSession) is null,
            "new owner Stop retires its protected record, registration and lease");
    }
}
