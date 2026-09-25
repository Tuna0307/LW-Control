using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewReconnectPolicyChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-a07-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        try
        {
            JsonElement enabled = await RunEnabledDisconnectAsync(Path.Combine(root, "enabled"));
            JsonElement disabled = await RunDisabledDisconnectAsync(Path.Combine(root, "disabled"));
            JsonElement cancelled = await RunDisableDuringRetryAsync(Path.Combine(root, "cancelled"));
            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                acceptanceCase = "A07",
                scope = "isolated recovered Overview reconnect policy; no real game/process operation",
                recoveredPolicy = new
                {
                    disconnectThresholdSeconds = OverviewRecoveryPolicy.DisconnectThreshold.TotalSeconds,
                    stableVerificationSeconds = OverviewRecoveryPolicy.StableVerification.TotalSeconds,
                    normalRetrySeconds = OverviewRecoveryPolicy.NormalRetryDelays.Select(x => x.TotalSeconds).ToArray(),
                },
                enabled,
                disabled,
                disabledDuringRetry = cancelled,
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static async Task<JsonElement> RunEnabledDisconnectAsync(string caseRoot)
    {
        Harness h = CreateHarness(caseRoot, autoReconnect: true);
        using (h.Lifecycle)
        {
            await h.StartAsync();
            Check(h.Config.Snapshot.GameDesiredRunning, "enabled: successful Start must arm desired-running");
            int startCountBeforeDisconnect = h.StartCalls;
            int stopCountBeforeDisconnect = h.StopCalls;

            h.HeartbeatAvailable = false;
            h.ClockMilliseconds = 0;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            h.ClockMilliseconds = (long)OverviewRecoveryPolicy.DisconnectThreshold.TotalMilliseconds - 1;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(h.Terminations.Count == 0 && h.StartCalls == startCountBeforeDisconnect && h.StopCalls == stopCountBeforeDisconnect,
                "enabled: disconnect must not terminate/restart before recovered 60-second threshold");

            h.ClockMilliseconds = (long)OverviewRecoveryPolicy.DisconnectThreshold.TotalMilliseconds;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(h.Terminations.Count == 1,
                "enabled: threshold disconnect must terminate exactly one owned process");
            Check(h.StopCalls == stopCountBeforeDisconnect + 1,
                "enabled: recovery must restore/close the old owned session exactly once");
            Check(h.StartCalls == startCountBeforeDisconnect + 1,
                "enabled: recovery must relaunch once through the normal lifecycle");
            OverviewRecoveryStatus terminal = h.Lifecycle.CurrentRecoveryStatus;
            Check(h.RecoveryEvents.Any(x => x.State == "waiting" && x.Reason == "disconnect") &&
                  h.RecoveryEvents.Any(x => x.State == "repairing" && x.Reason == "disconnect") &&
                  h.RecoveryEvents.Any(x => x.State == "launching" && x.Reason == "disconnect") &&
                  h.RecoveryEvents.Any(x => x.State == "verifying" && x.Reason == "disconnect") &&
                  terminal.State == "succeeded" && terminal.Restarted,
                "enabled: recovery must publish waiting/repairing/launching/verifying then succeeded restarted");
            Check(terminal.StartedAt > 0 && terminal.CompletedAt is > 0 &&
                  terminal.NoticeId == 1 && terminal.NoticeVisible &&
                  h.RecoveryEvents.Where(x => x.State != "idle").All(x => x.NoticeId == 1 && x.NoticeVisible),
                "enabled: native recovery notice id remains stable and visible through terminal success");
            Check(h.Delays.Contains(OverviewRecoveryPolicy.StableVerification),
                "enabled: recovered session must pass the 15-second stable verification gate");
            Check(h.Config.Snapshot.AutoReconnect && h.Config.Snapshot.GameDesiredRunning,
                "enabled: successful recovery preserves reconnect and desired-running intent");

            string recoveredSession = h.Session!;
            await h.StopAsync(recoveredSession);
            Check(!h.Config.Snapshot.GameDesiredRunning,
                "enabled: intentional Stop must clear desired-running after the recovery proof");
            int startsAfterStop = h.StartCalls;
            h.HeartbeatAvailable = false;
            h.ProcessAlive = false;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(h.StartCalls == startsAfterStop,
                "enabled: intentional Stop must suppress later automatic resurrection");

            return JsonSerializer.SerializeToElement(new
            {
                preThresholdTerminationCount = 0,
                thresholdTerminations = 1,
                recoveryStartDelta = 1,
                recoveryStopDelta = 1,
                states = h.RecoveryEvents.Select(x => x.State).Distinct().ToArray(),
                stableVerificationObserved = h.Delays.Contains(OverviewRecoveryPolicy.StableVerification),
                finalDesiredRunning = h.Config.Snapshot.GameDesiredRunning,
                automaticRestartAfterIntentionalStop = h.StartCalls != startsAfterStop,
            });
        }
    }

    private static async Task<JsonElement> RunDisabledDisconnectAsync(string caseRoot)
    {
        Harness h = CreateHarness(caseRoot, autoReconnect: false);
        using (h.Lifecycle)
        {
            await h.StartAsync();
            Check(!h.Config.Snapshot.AutoReconnect && h.Config.Snapshot.GameDesiredRunning,
                "disabled: manual Start may arm desired-running while automatic reconnect remains off");
            int startsBefore = h.StartCalls;
            int stopsBefore = h.StopCalls;

            h.HeartbeatAvailable = false;
            h.ClockMilliseconds = 0;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            h.ClockMilliseconds = (long)OverviewRecoveryPolicy.DisconnectThreshold.TotalMilliseconds;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            h.ClockMilliseconds += (long)OverviewRecoveryPolicy.LoginUnavailableThreshold.TotalMilliseconds;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            Check(h.Terminations.Count == 0 && h.StartCalls == startsBefore && h.StopCalls == stopsBefore,
                "disabled: observed disconnect must not terminate, restore or relaunch the owned process");
            Check(h.Lifecycle.CurrentRecoveryStatus.State == "idle" &&
                  h.Lifecycle.CurrentRecoveryStatus.StartedAt == 0 &&
                  h.Lifecycle.CurrentRecoveryStatus.NoticeId == 0 &&
                  !h.Lifecycle.CurrentRecoveryStatus.NoticeVisible,
                "disabled: recovery status must remain the native idle baseline");

            h.HeartbeatAvailable = true;
            await h.StopAsync(h.Session!);
            return JsonSerializer.SerializeToElement(new
            {
                autoReconnect = h.Config.Snapshot.AutoReconnect,
                recoveryTerminations = h.Terminations.Count,
                recoveryStartDelta = h.StartCalls - startsBefore,
                recoveryStopDelta = h.StopCalls - stopsBefore - 1,
                recoveryState = h.Lifecycle.CurrentRecoveryStatus.State,
                finalDesiredRunning = h.Config.Snapshot.GameDesiredRunning,
            });
        }
    }

    private static async Task<JsonElement> RunDisableDuringRetryAsync(string caseRoot)
    {
        Harness h = CreateHarness(caseRoot, autoReconnect: true);
        using (h.Lifecycle)
        {
            await h.StartAsync();
            h.FailNextStart = true;
            h.DisableReconnectOnNormalRetry = true;
            int startsBefore = h.StartCalls;
            int stopsBefore = h.StopCalls;

            h.HeartbeatAvailable = false;
            h.ClockMilliseconds = 0;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();
            h.ClockMilliseconds = (long)OverviewRecoveryPolicy.DisconnectThreshold.TotalMilliseconds;
            await h.Lifecycle.RunRecoveryObservationForTestAsync();

            Check(!h.Config.Snapshot.AutoReconnect,
                "disable-during-retry: public automation command must persist reconnect=false");
            Check(h.Config.Snapshot.GameDesiredRunning,
                "disable-during-retry: disabling reconnect must not erase desired-running intent");
            Check(h.Terminations.Count == 1 && h.StopCalls == stopsBefore + 1,
                "disable-during-retry: already-eligible recovery still performs exact cleanup once");
            Check(h.StartCalls == startsBefore + 1,
                "disable-during-retry: exactly one failed recovery launch is attempted before cancellation");
            Check(h.Delays.Contains(OverviewRecoveryPolicy.NormalRetryDelays[0]),
                "disable-during-retry: failed recovery must enter recovered 15-second retry delay");
            Check(h.Lifecycle.CurrentRecoveryStatus.State == "idle" &&
                  h.Lifecycle.CurrentRecoveryStatus.StartedAt == 0 &&
                  h.Lifecycle.CurrentRecoveryStatus.NoticeId == 1 &&
                  !h.Lifecycle.CurrentRecoveryStatus.NoticeVisible,
                "disable-during-retry: cancellation must restore native idle defaults while preserving noticeId");

            int startsAfterDisable = h.StartCalls;
            for (int i = 0; i < 4; i++)
            {
                h.ClockMilliseconds += 300_000;
                await h.Lifecycle.RunRecoveryObservationForTestAsync();
            }
            Check(h.StartCalls == startsAfterDisable,
                "disable-during-retry: future observations must never launch another recovery");

            return JsonSerializer.SerializeToElement(new
            {
                firstRetryDelaySeconds = OverviewRecoveryPolicy.NormalRetryDelays[0].TotalSeconds,
                exactCleanupCount = h.StopCalls - stopsBefore,
                attemptedRecoveryStarts = h.StartCalls - startsBefore,
                autoReconnectAfterDisable = h.Config.Snapshot.AutoReconnect,
                desiredRunningAfterDisable = h.Config.Snapshot.GameDesiredRunning,
                recoveryState = h.Lifecycle.CurrentRecoveryStatus.State,
                futureRecoveryStarts = h.StartCalls - startsAfterDisable,
            });
        }
    }

    private static Harness CreateHarness(string caseRoot, bool autoReconnect)
    {
        Directory.CreateDirectory(Path.Combine(caseRoot, "Game"));
        var config = new LocalConfigStore(Path.Combine(caseRoot, "config"));
        string profileId = "profile-a07-" + Path.GetFileName(caseRoot);
        config.Update(c => c with
        {
            ProfileId = profileId,
            GameRoot = caseRoot,
            AutoLaunchGame = false,
            AutoReconnect = autoReconnect,
        });
        return new Harness(caseRoot, profileId, config);
    }

    private sealed class Harness
    {
        private readonly string gamePath;
        private readonly string profileId;
        private int pidSequence;
        private string startedAtUtc = "2026-09-20T09:00:00.0000000Z";
        private LWBridgeBackend? backend;

        internal Harness(string root, string profileId, LocalConfigStore config)
        {
            this.profileId = profileId;
            Config = config;
            gamePath = Path.Combine(root, "Game", "LastWar.exe");
            var hooks = new OverviewLifecycleTestHooks
            {
                ProcessMatches = (pid, path, startedAt) => ProcessAlive && pid == Pid && startedAt == startedAtUtc &&
                    string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
                ReadAllBytes = _ => HeartbeatAvailable
                    ? HeartbeatBytes()
                    : throw new IOException("synthetic bridge disconnect"),
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
                UtcNow = () => DateTimeOffset.UtcNow,
                MonotonicMilliseconds = () => ClockMilliseconds,
                UpdateProcessRunning = () => false,
                UpdateActivityFingerprint = () => null,
                ProcessHung = (_, _) => false,
                TerminateOwnedProcessAsync = (pid, path, startedAt, _) =>
                {
                    Check(ProcessAlive && pid == Pid && startedAt == startedAtUtc &&
                          string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
                        "termination hook must receive the exact owned PID/path/creation identity");
                    Terminations.Add((pid, Path.GetFullPath(path), startedAt));
                    ProcessAlive = false;
                    HeartbeatAvailable = false;
                    return Task.CompletedTask;
                },
                DelayAsync = async (delay, token) =>
                {
                    Delays.Add(delay);
                    if (DisableReconnectOnNormalRetry && delay == OverviewRecoveryPolicy.NormalRetryDelays[0])
                    {
                        DisableReconnectOnNormalRetry = false;
                        JsonElement disable = JsonSerializer.SerializeToElement(new
                        {
                            profileId,
                            name = "autoForceUpdateReload",
                            enabled = false,
                        });
                        await backend!.InvokeAsync("set_automation", disable, CancellationToken.None).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                },
                RunHelperAsync = RunHelperAsync,
            };
            Lifecycle = new OverviewLifecycleService(
                profileId,
                root,
                helperPath: Path.Combine(root, "fake-overview-helper.py"),
                requireCurrentClientEvidence: false,
                config: config,
                testHooks: hooks,
                startRecoveryMonitor: false);
            Lifecycle.RecoveryStatusChanged += RecoveryEvents.Add;
            backend = new LWBridgeBackend(config, asyncCommands: Lifecycle, overviewLifecycle: Lifecycle);
        }

        internal LocalConfigStore Config { get; }
        internal OverviewLifecycleService Lifecycle { get; }
        internal bool ProcessAlive { get; set; }
        internal bool HeartbeatAvailable { get; set; } = true;
        internal long ClockMilliseconds { get; set; }
        internal bool FailNextStart { get; set; }
        internal bool DisableReconnectOnNormalRetry { get; set; }
        internal int Pid { get; private set; }
        internal string? Session { get; private set; }
        internal string? Challenge { get; private set; }
        internal int StartCalls { get; private set; }
        internal int StopCalls { get; private set; }
        internal List<TimeSpan> Delays { get; } = new();
        internal List<OverviewRecoveryStatus> RecoveryEvents { get; } = new();
        internal List<(int Pid, string Path, string StartedAt)> Terminations { get; } = new();

        internal async Task StartAsync()
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { profileId });
            await Lifecycle.InvokeAsync("profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
        }

        internal async Task StopAsync(string session)
        {
            JsonElement payload = JsonSerializer.SerializeToElement(new { profileId, instanceId = session });
            await Lifecycle.InvokeAsync("profile_instance_stop", payload, CancellationToken.None).ConfigureAwait(false);
        }

        private Task<JsonElement> RunHelperAsync(OverviewHelperInvocation invocation, CancellationToken _)
        {
            if (invocation.Operation == "start")
            {
                StartCalls++;
                if (FailNextStart)
                {
                    FailNextStart = false;
                    throw new BridgeCommandException("LAUNCH_FAILED", "synthetic recovery launch failure");
                }
                Session = invocation.SessionId;
                Challenge = invocation.Challenge;
                Pid = 47000 + ++pidSequence;
                startedAtUtc = $"2026-09-20T09:{pidSequence:00}:00.0000000Z";
                ProcessAlive = true;
                HeartbeatAvailable = true;
                string challengeHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant();
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    ok = true,
                    mode = "overview_install_launch_ready_deferred_restore",
                    bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId,
                    sessionId = invocation.SessionId,
                    challengeSha256 = challengeHash,
                    gamePid = Pid,
                    launcherPid = Pid + 10000,
                    gamePath,
                    gameStartedAtUtc = startedAtUtc,
                    gameRunning = true,
                    installedFilesChanged = true,
                    restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                    ready = new
                    {
                        schemaVersion = 1,
                        bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId,
                        sessionId = invocation.SessionId,
                        challenge = invocation.Challenge,
                        gamePid = Pid,
                        ready = true,
                        messageVisible = true,
                        messageText = OverviewLifecycleService.ReadyMessage,
                        readyAt = now,
                        updatedAt = now,
                    },
                }));
            }

            StopCalls++;
            bool wasAlive = ProcessAlive;
            ProcessAlive = false;
            HeartbeatAvailable = false;
            return Task.FromResult(JsonSerializer.SerializeToElement(new
            {
                ok = true,
                mode = "overview_exact_pid_close_restore",
                bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId,
                sessionId = invocation.SessionId,
                gamePid = invocation.GamePid,
                gamePath = invocation.GamePath,
                gameStartedAtUtc = invocation.GameStartedAtUtc,
                close = wasAlive
                    ? new { method = "synthetic", accepted = true, processExited = true, alreadyExited = false }
                    : new { method = "already_exited", accepted = false, processExited = true, alreadyExited = true },
                restore = new { restored = true },
                gameRunning = false,
                installedFilesChanged = false,
            }));
        }

        private byte[] HeartbeatBytes()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId,
                sessionId = Session,
                challenge = Challenge,
                gamePid = Pid,
                updatedAt = now,
                ready = true,
                messageVisible = true,
                messageText = OverviewLifecycleService.ReadyMessage,
                gameStateObserved = true,
                gameReady = true,
                loggedIn = true,
                connected = true,
                connecting = false,
                gameUid = "a07-test",
                serverId = 2212,
                worldPos = 12345,
                recoveryObserved = false,
                recoveryConfirmed = false,
                recoveryAmbiguous = false,
            });
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview A07 reconnect policy check failed: " + message);
    }
}
