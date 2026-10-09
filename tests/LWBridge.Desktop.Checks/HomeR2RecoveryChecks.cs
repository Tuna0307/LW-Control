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
        internal string Session = "", Challenge = "";
        internal readonly string Exe;
        internal readonly string Created;
        internal TaskCompletionSource? HeldTerminate = null;
        internal TaskCompletionSource? TerminationReached = null;
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
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
                DelayAsync = (_, token) => Task.Delay(1, token),
                CreateRecoveryLogReader = _ => new EmptyLog(),
                UpdateProcessRunning = () => Updating,
                UpdateActivityFingerprint = () => "constant",
                ProcessHung = (_, _) => Hung,
                TerminateOwnedProcessAsync = async (pid, path, created, _) =>
                {
                    TerminateCalls++;
                    Require(pid == Pid && path == Exe && created == Created, "terminate exact process identity");
                    TerminationReached?.TrySetResult();
                    if (HeldTerminate is not null) await HeldTerminate.Task;
                    if (Pid == pid) Alive = false;
                },
                RunHelperAsync = (inv, _) =>
                {
                    if (inv.Operation == "stop")
                    {
                        StopCalls++;
                        Require(inv.ProfileId == Profile && inv.GamePath == Exe &&
                                inv.GameStartedAtUtc == Created && inv.SessionId == Session &&
                                inv.Challenge == Challenge, "stop exact issued session");
                        Alive = false;
                        return Task.FromResult(JsonSerializer.SerializeToElement(new
                        {
                            mode = "overview_exact_pid_close_restore",
                            bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = inv.SessionId, gamePid = inv.GamePid,
                            gamePath = Exe, gameStartedAtUtc = Created, gameRunning = false,
                            installedFilesChanged = false,
                            close = new { accepted = true, processExited = true, alreadyExited = false },
                            restore = new { restored = true }
                        }));
                    }
                    StartCalls++;
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
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
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
                    }));
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
            Service.Dispose();
            Host?.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    internal static async Task RunAsync()
    {
        await ProcessExitOffAndOn();
        await RealAdoptionRecordRecovery();
        await FailedLaunchAndRetry();
        await DisableAndStop();
        await HungAndDisconnectedEdges();
        await HeldOldEffectVsStopSuccessor();
        await LoginUnavailableThreshold();
        Require(OverviewRecoveryPolicy.NormalRetryDelays.Select(d => d.TotalMilliseconds)
            .SequenceEqual(new double[] { 15000, 30000, 60000, 120000, 300000 }),
            "original normal counter table (clamped on attempts >=5)");
        Require(OverviewRecoveryPolicy.MaintenanceRetryDelays.Select(d => d.TotalMilliseconds)
            .SequenceEqual(new double[] { 120000, 300000, 600000 }),
            "original maintenance retry table");
        Console.WriteLine("HOME004_R2_NATIVE_MONITOR_OK off/on exit, exact hang/disconnect/login boundaries, failed retry/stable, disable, Stop, held stale cleanup/successor; game launches=0");
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
        c.HeldTerminate.TrySetResult();
        await heldObservation.WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.Pid == successorPid && c.Session == successorSession && c.Alive &&
                c.StopCalls == 1 && c.StartCalls == 2 &&
                c.Service.CurrentRecoveryStatus.State == "idle",
            "late old termination must not clear/stop new owner or publish stale recovery status");
        await c.Stop();
    }
}
