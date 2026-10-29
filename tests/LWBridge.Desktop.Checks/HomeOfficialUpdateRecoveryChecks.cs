using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Controlled lifecycle checks: real admission, helper-result classification and
// startup/reconnect paths; inert process, restoration and official-launch seams.
// These checks never start a launcher or touch the installed game.
internal static class HomeOfficialUpdateRecoveryChecks
{
    private const string DeltaError = "official_lua_update_failed: official launcher rejected the Lua delta output";

    internal static async Task RunAsync()
    {
        await AutomaticStartupRepairsInsideOneTransaction();
        await SecondDeltaFailsWithoutAnotherRepair();
        await UnrelatedFailuresDoNotRepair();
        await OfficialRestartRetainsOneRetry();
        await RecoveryRepairsDelta();
        await StopWaitsForRecoveryRepairCleanup();
        await CallerCancellationPreventsRetry();
        await ClosePreventsRetry();
        Console.WriteLine("HOME_OFFICIAL_UPDATE_RECOVERY_CONTROLLED_OK startup/reconnect/bounded-retry/stop/cancel/close; game launches=0");
    }

    private static void Require(bool valid, string reason)
    {
        if (!valid) throw new InvalidOperationException("HOME_OFFICIAL_UPDATE_RECOVERY: " + reason);
    }

    private enum Outcome { Success, Delta, Timeout, WrongType, WrappedCleanupFailure, OfficialRestart }

    private sealed class Case : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "home-official-update-" + Guid.NewGuid().ToString("N"));
        internal readonly LocalConfigStore Config;
        internal readonly OverviewLifecycleService Service;
        internal readonly List<string> Events = [];
        internal readonly List<OverviewHelperInvocation> Starts = [];
        internal readonly Queue<Outcome> Outcomes = [];
        internal readonly string Exe;
        internal readonly string Created = DateTimeOffset.UtcNow.AddMinutes(-1).UtcDateTime.ToString("O");
        internal readonly string Profile = "controlled-update-profile";
        internal long Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        internal bool Alive;
        internal int Pid = 39000;
        internal int Settles;
        internal string Session = "", Challenge = "";
        internal Func<int, CancellationToken, Task>? OnSettle;

        internal Case(params Outcome[] outcomes)
        {
            Directory.CreateDirectory(Path.Combine(Root, "Game"));
            Exe = Path.Combine(Root, "Game", "LastWar.exe");
            File.WriteAllBytes(Exe, []);
            foreach (Outcome outcome in outcomes) Outcomes.Enqueue(outcome);
            Config = new LocalConfigStore(persistent: false,
                initialValue: LWBridgeLocalConfig.CreateDefault() with
                { ProfileId = Profile, AutoReconnect = true, GameDesiredRunning = false });
            var hooks = new OverviewLifecycleTestHooks
            {
                UtcNow = () => DateTimeOffset.FromUnixTimeMilliseconds(Now),
                MonotonicMilliseconds = () => Now,
                SelectedGamePids = _ => [],
                GameRootAvailable = () => true,
                ProcessMatches = (pid, path, created) => Alive && pid == Pid &&
                    path == Exe && created == Created,
                ReadAllBytes = path => path.EndsWith("heartbeat.json", StringComparison.OrdinalIgnoreCase)
                    ? Heartbeat() : File.ReadAllBytes(path),
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
                DelayAsync = (_, token) => Task.Delay(1, token),
                UpdateProcessRunning = () => false,
                RunOfficialRecoverAsync = (_, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    Require(!Alive, "official restore-only preflight cannot overlap an owned game");
                    Events.Add("recover");
                    return Task.CompletedTask;
                },
                RunOfficialSettleAsync = async (_, token) =>
                {
                    Events.Add("settle");
                    Settles++;
                    if (OnSettle is not null) await OnSettle(Settles, token);
                    token.ThrowIfCancellationRequested();
                },
                RunHelperAsync = (inv, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    if (inv.Operation == "stop")
                    {
                        Require(inv.GamePid == Pid && inv.GamePath == Exe && inv.GameStartedAtUtc == Created,
                            "stop must retain the exact controlled owner");
                        Alive = false;
                        Events.Add("stop");
                        return Task.FromResult(JsonSerializer.SerializeToElement(new
                        {
                            mode = "overview_exact_pid_close_restore", bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = inv.SessionId, gamePid = Pid,
                            gamePath = Exe, gameStartedAtUtc = Created, gameRunning = false,
                            installedFilesChanged = false,
                            close = new { accepted = true, processExited = true, alreadyExited = false },
                            restore = new { restored = true },
                        }));
                    }
                    Require(inv.Operation == "start", "unexpected helper operation");
                    Starts.Add(inv);
                    Events.Add("start");
                    Outcome outcome = Outcomes.Count == 0 ? Outcome.Success : Outcomes.Dequeue();
                    if (outcome == Outcome.OfficialRestart)
                        throw new BridgeCommandException("OFFICIAL_LAUNCHER_RESTARTED", "OFFICIAL_LAUNCHER_RESTARTED");
                    if (outcome != Outcome.Success)
                    {
                        // Failure is the structured output of an already-exited helper.
                        // A cleanup failure is intentionally a different, unrepairable prefix.
                        return Task.FromResult(JsonSerializer.SerializeToElement(new
                        {
                            ok = false,
                            errorType = outcome == Outcome.WrongType ? "IOException" : "OverviewBridgeError",
                            error = outcome switch
                            {
                                Outcome.Timeout => "the selected launcher did not create a matching LastWar process before timeout",
                                Outcome.WrappedCleanupFailure => "Overview start failed: " + DeltaError + "; cleanup incomplete: exact-PID close failed",
                                _ => DeltaError,
                            },
                        }));
                    }
                    Alive = true;
                    Pid++;
                    Session = inv.SessionId!;
                    Challenge = inv.Challenge!;
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
                    {
                        ok = true, mode = "overview_install_launch_ready_deferred_restore",
                        bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId = Profile, sessionId = Session,
                        challengeSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Challenge))).ToLowerInvariant(),
                        gamePid = Pid, launcherPid = Pid + 1, gamePath = Exe, gameStartedAtUtc = Created,
                        gameRunning = true, installedFilesChanged = true,
                        restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                        ready = Ready(),
                    }));
                },
            };
            Service = new OverviewLifecycleService(Profile, Root, helperPath: "inert-not-a-helper.py",
                config: Config, testHooks: hooks, startRecoveryMonitor: false, requireCurrentClientEvidence: false,
                applicationDataRoot: Root, runtimeRoot: Path.Combine(Root, "runtime"),
                evidenceRoot: Path.Combine(Root, "evidence"), backupRoot: Path.Combine(Root, "backups"));
        }

        private object Ready() => new
        {
            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = Profile, sessionId = Session, challenge = Challenge, gamePid = Pid,
            ready = true, readyAt = Now / 1000, updatedAt = Now / 1000,
            messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
            gameStateObserved = true, gameReady = true, loggedIn = true, connected = true,
            connecting = false,
        };
        private byte[] Heartbeat() => JsonSerializer.SerializeToUtf8Bytes(Ready());
        internal JsonElement Status() => JsonSerializer.SerializeToElement(Service.CreateProfileInstanceStatus(), JsonOptions.Default);
        internal Task<object?> Start(CancellationToken token = default) => Service.InvokeAsync("profile_instance_start",
            JsonSerializer.SerializeToElement(new { }), token);
        internal Task<object?> Stop() => Service.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { }), CancellationToken.None);
        internal async Task BeginRecovery()
        {
            Alive = false;
            await Service.RunRecoveryObservationForTestAsync();
            Now += 2000;
            await Service.RunRecoveryObservationForTestAsync();
            Require(Service.CurrentRecoveryStatus.State == "waiting", "exited owner must enter recovery waiting");
        }
        public void Dispose()
        {
            Service.Dispose();
            Directory.Delete(Root, recursive: true);
        }
    }

    private static async Task AutomaticStartupRepairsInsideOneTransaction()
    {
        using var c = new Case(Outcome.Delta, Outcome.Success);
        string marker = Path.Combine(c.Root, "evidence", "official-settled-client.json");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, "stale settlement");
        c.OnSettle = async (number, _) =>
        {
            if (number != 2) return;
            Require(!File.Exists(marker), "delta failure must invalidate the stale official settlement");
            Require(c.Status().GetProperty("phase").GetString() == "starting" &&
                c.Status().GetProperty("instanceId").GetString() == c.Starts[0].SessionId,
                "repair must retain its admitted starting session");
            await ExpectCode(() => c.Start(), "PROFILE_ALREADY_RUNNING");
            c.Now += 9 * 60_000; // Official update gets its separate ten-minute budget.
        };
        JsonElement result = JsonSerializer.SerializeToElement(await c.Service.InvokeAsync(
            "profile_instances_reconcile", JsonSerializer.SerializeToElement(new { autoLaunchAll = true }),
            CancellationToken.None), JsonOptions.Default);
        Require(result.GetProperty("errors").GetArrayLength() == 0 && c.Service.IsReady,
            "automatic startup must repair the delta and connect");
        Require(c.Events.SequenceEqual(new[] { "recover", "settle", "start", "recover", "settle", "start" }),
            "repair must complete preflight and official settlement before one retry");
        Require(c.Starts.Count == 2 && c.Starts[0].SessionId == c.Starts[1].SessionId &&
            c.Starts[0].Challenge == c.Starts[1].Challenge &&
            c.Starts[1].TimeoutSeconds == 120 && c.Starts[1].SupervisionMilliseconds == 225_000,
            "long official update must retain session and receive a fresh acquisition/readiness budget");
    }

    private static async Task SecondDeltaFailsWithoutAnotherRepair()
    {
        using var c = new Case(Outcome.Delta, Outcome.Delta, Outcome.Success);
        string marker = Path.Combine(c.Root, "evidence", "official-settled-client.json");
        c.OnSettle = (number, _) =>
        {
            if (number == 2)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                File.WriteAllText(marker, "settlement must not survive second delta failure");
            }
            return Task.CompletedTask;
        };
        await ExpectCode(() => c.Start(), "LAUNCH_FAILED");
        Require(c.Starts.Count == 2 && c.Settles == 2 && !c.Alive && !File.Exists(marker),
            "second delta failure must invalidate marker and report without a third helper or repair");
    }

    private static async Task UnrelatedFailuresDoNotRepair()
    {
        foreach (Outcome outcome in new[] { Outcome.Timeout, Outcome.WrongType, Outcome.WrappedCleanupFailure })
        {
            using var c = new Case(outcome);
            await ExpectCode(() => c.Start(), "LAUNCH_FAILED");
            Require(c.Starts.Count == 1 && c.Settles == 1 && !c.Alive,
                "timeout, wrong helper error type or incomplete cleanup must not authorize repair");
        }
    }

    private static async Task OfficialRestartRetainsOneRetry()
    {
        using (var c = new Case(Outcome.OfficialRestart, Outcome.Success))
        {
            await c.Start();
            Require(c.Starts.Count == 2 && c.Service.IsReady, "exact official restart event must retain its retry");
        }
        using (var c = new Case(Outcome.OfficialRestart, Outcome.Delta, Outcome.Success))
        {
            await ExpectCode(() => c.Start(), "LAUNCH_FAILED");
            Require(c.Starts.Count == 2 && c.Settles == 1,
                "delta after official restart retry must not gain another attempt");
        }
    }

    private static async Task RecoveryRepairsDelta()
    {
        using var c = new Case(Outcome.Success, Outcome.Delta, Outcome.Success);
        await c.Start();
        await c.BeginRecovery();
        await c.Service.RunRecoveryRunTickForTestAsync();
        Require(c.Starts.Count == 3 && c.Settles == 3 && c.Service.IsReady,
            "automatic reconnect must share bounded delta repair");
    }

    private static async Task StopWaitsForRecoveryRepairCleanup()
    {
        using var c = new Case(Outcome.Success, Outcome.Delta, Outcome.Success);
        await c.Start();
        await c.BeginRecovery();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        c.OnSettle = async (number, token) =>
        {
            if (number != 3) return;
            reached.TrySetResult();
            await cleanup.Task;
            token.ThrowIfCancellationRequested();
        };
        Task pending = c.Service.RunRecoveryRunTickForTestAsync();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task stopped = c.Stop();
        Require(!stopped.IsCompleted && !c.Config.Snapshot.GameDesiredRunning,
            "Stop must cancel recovery and wait for repair's owned cleanup");
        cleanup.TrySetResult();
        await Task.WhenAll(pending, stopped).WaitAsync(TimeSpan.FromSeconds(5));
        Require(c.Starts.Count == 2 && !c.Alive && c.Service.CurrentRecoveryStatus.State == "idle",
            "cancelled repair cannot launch a replacement after Stop acknowledges cleanup");
    }

    private static async Task CallerCancellationPreventsRetry()
    {
        using var c = new Case(Outcome.Delta, Outcome.Success);
        using var cancelled = new CancellationTokenSource();
        c.OnSettle = (number, _) =>
        {
            if (number == 2) cancelled.Cancel();
            return Task.CompletedTask;
        };
        await ExpectCode(() => c.Start(cancelled.Token), "GAME_OPERATION_CANCELLED");
        Require(c.Starts.Count == 1 && !c.Alive, "caller cancellation during official repair must prevent retry");
    }

    private static async Task ClosePreventsRetry()
    {
        using var c = new Case(Outcome.Delta, Outcome.Success);
        c.OnSettle = (number, _) =>
        {
            if (number == 2) c.Service.Close();
            return Task.CompletedTask;
        };
        await ExpectCode(() => c.Start(), "GAME_OPERATION_CANCELLED");
        Require(c.Starts.Count == 1 && !c.Alive, "Close during official repair must prevent retry");
    }

    private static async Task ExpectCode(Func<Task<object?>> action, string expected)
    {
        try
        {
            await action();
            throw new InvalidOperationException("expected " + expected);
        }
        catch (BridgeCommandException error) when (error.Code == expected) { }
    }
}
