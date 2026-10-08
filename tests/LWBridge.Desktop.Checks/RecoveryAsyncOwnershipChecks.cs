using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// HOME 009 R1 E. Held-asynchronous-effect ownership checks for the recovery engine through the REAL
// OverviewLifecycleService: the old run's terminate is parked on a gate while the user Stops / Starts / closes the
// profile, then the gate is released with success or rejection. These are production scheduling overlaps, not serial
// immediate hooks. They verify current-client ownership safety (exact captured identity); the original
// 0.3.17 contract does not describe these overlaps (its task/handle model differs) and they are NOT parity claims.
internal static class RecoveryAsyncOwnershipChecks
{
    private const string Profile = "r1-async-ownership";

    internal sealed class Instance
    {
        public int Pid; public string Session = ""; public string Challenge = ""; public string StartedAt = ""; public bool Alive = true;
    }

    internal sealed class Env : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "lwbridge-r1-async-" + Guid.NewGuid().ToString("N"));
        public readonly List<Instance> Instances = new();
        public readonly List<string> Calls = new();
        public readonly List<string> StatusStates = new();
        public long Clock = 1_000_000;
        public bool BridgeOnline = true;
        public bool Hung;
        public bool UpdateRunning;
        public bool GameHealthy = true;
        public bool GameStateObserved = true;
        public int KillUpdaterCalls;
        public int FailStarts;
        public int FailStops;
        public TaskCompletionSource? StartGate;
        public readonly TaskCompletionSource StartEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Fingerprint = "fp";
        public TaskCompletionSource? TerminateGate;
        public readonly TaskCompletionSource TerminateEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? TerminateFailure;
        public OverviewLifecycleService Lifecycle = null!;
        public string GamePath = "";
        private int starts;

        public Instance Current => Instances[^1];
        public int StartCalls => Calls.Count(c => c == "helper-start");
        public int StopCallsFor(int pid) => Calls.Count(c => c == "helper-stop:" + pid);

        public Env()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Game"));
            GamePath = Path.Combine(Root, "Game", "LastWar.exe");
            var config = new LocalConfigStore(Path.Combine(Root, "config"));
            config.Update(c => c with { ProfileId = Profile, GameRoot = Root, AutoLaunchGame = false, AutoReconnect = true });
            var hooks = new OverviewLifecycleTestHooks
            {
                RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                ProcessMatches = (pid, path, startedAt) =>
                    Instances.Any(i => i.Alive && i.Pid == pid && i.StartedAt == startedAt) &&
                    string.Equals(Path.GetFullPath(path), Path.GetFullPath(GamePath), StringComparison.OrdinalIgnoreCase),
                ReadAllBytes = _ => Heartbeat(),
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
                UtcNow = () => DateTimeOffset.FromUnixTimeMilliseconds(Clock),
                MonotonicMilliseconds = () => Clock,
                UpdateProcessRunning = () => UpdateRunning,
                UpdateActivityFingerprint = () => Fingerprint,
                ProcessHung = (_, _) => Hung,
                SelectedGamePids = _ => Array.Empty<int>(),
                TerminateUpdateProcessesAsync = _ => { KillUpdaterCalls++; UpdateRunning = false; return Task.CompletedTask; },
                DelayAsync = (_, token) => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; },
                RunHelperAsync = Helper,
                TerminateOwnedProcessAsync = async (pid, _, _, token) =>
                {
                    Calls.Add("terminate:" + pid);
                    TerminateEntered.TrySetResult();
                    if (TerminateGate is { } gate) await gate.Task.ConfigureAwait(false);
                    if (TerminateFailure is { } failure) throw failure;
                    foreach (Instance i in Instances) if (i.Pid == pid) i.Alive = false;
                },
                CreateRecoveryLogReader = _ => new EmptyReader(),
            };
            Lifecycle = new OverviewLifecycleService(Profile, Root, helperPath: Path.Combine(Root, "fake-helper.py"),
                requireCurrentClientEvidence: false, config: config, testHooks: hooks, startRecoveryMonitor: false);
            Lifecycle.RecoveryStatusChanged += status => StatusStates.Add(status.State);
        }

        private sealed class EmptyReader : IRecoveryLogReader
        {
            public void Begin() { }
            public string ReadNew() => "";
        }

        private byte[] Heartbeat()
        {
            Instance inst = Current;
            if (!BridgeOnline) throw new IOException("synthetic bridge disconnect");
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion, profileId = Profile,
                sessionId = inst.Session, challenge = inst.Challenge, gamePid = inst.Pid,
                updatedAt = Clock / 1000, ready = true, messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
                gameStateObserved = GameStateObserved, gameReady = GameHealthy, loggedIn = GameHealthy, connected = GameHealthy, connecting = false,
                gameUid = GameHealthy ? "r1" : "", serverId = GameHealthy ? 2212 : 0, worldPos = GameHealthy ? 12345 : 0,
                recoveryObserved = false, recoveryConfirmed = false, recoveryAmbiguous = false,
            });
        }

        private async Task<JsonElement> Helper(OverviewHelperInvocation invocation, CancellationToken _)
        {
            if (invocation.Operation == "start")
            {
                Calls.Add("helper-start");
                if (Instances.Count > 0 && StartGate is { } startGate)
                {
                    StartEntered.TrySetResult();
                    await startGate.Task.ConfigureAwait(false);
                }
                if (Instances.Count > 0 && FailStarts > 0)
                {
                    FailStarts--;
                    throw new BridgeCommandException("LAUNCH_FAILED", "synthetic launch failure");
                }
                int index = ++starts;
                var inst = new Instance
                {
                    Pid = 50_000 + index * 100, Session = invocation.SessionId!, Challenge = invocation.Challenge!,
                    StartedAt = $"2026-10-08T10:{index:00}:00.0000000Z",
                };
                Instances.Add(inst);
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant();
                long now = Clock / 1000;
                return (JsonSerializer.SerializeToElement(new
                {
                    ok = true, mode = "overview_install_launch_ready_deferred_restore",
                    bridgeVersion = OverviewLifecycleService.BridgeVersion, profileId = Profile, sessionId = invocation.SessionId,
                    challengeSha256 = hash, gamePid = inst.Pid, launcherPid = inst.Pid + 10000, gamePath = GamePath,
                    gameStartedAtUtc = inst.StartedAt, gameRunning = true, installedFilesChanged = true,
                    restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                    ready = new
                    {
                        schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion, profileId = Profile,
                        sessionId = invocation.SessionId, challenge = invocation.Challenge, gamePid = inst.Pid, ready = true,
                        messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage, readyAt = now, updatedAt = now,
                    },
                }));
            }
            Calls.Add("helper-stop:" + invocation.GamePid);
            if (FailStops > 0)
            {
                FailStops--;
                throw new InvalidOperationException("synthetic restoration failure");
            }
            foreach (Instance i in Instances) if (i.Pid == invocation.GamePid) i.Alive = false;
            return (JsonSerializer.SerializeToElement(new
            {
                ok = true, mode = "overview_exact_pid_close_restore", bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId = Profile, sessionId = invocation.SessionId, gamePid = invocation.GamePid, gamePath = invocation.GamePath,
                gameStartedAtUtc = invocation.GameStartedAtUtc,
                close = new { method = "synthetic", accepted = true, processExited = true, alreadyExited = false },
                restore = new { restored = true }, gameRunning = false, installedFilesChanged = false,
            }));
        }

        public async Task<string> StartAsync()
        {
            using JsonDocument payload = JsonDocument.Parse("{}");
            object? status = await Lifecycle.InvokeAsync("profile_instance_start", payload.RootElement.Clone(), CancellationToken.None);
            return JsonSerializer.SerializeToElement(status, JsonOptions.Default).GetProperty("instanceId").GetString()!;
        }

        public Task StopAsync(string instanceId)
        {
            using JsonDocument payload = JsonDocument.Parse(JsonSerializer.Serialize(new { profileId = Profile, instanceId }));
            return Lifecycle.InvokeAsync("profile_instance_stop", payload.RootElement.Clone(), CancellationToken.None);
        }

        /// <summary>Make the current game hung and offline, then tick the monitor until the hang recovery's terminate is entered.</summary>
        public async Task<Task> DriveToHeldHangTerminateAsync()
        {
            BridgeOnline = false;
            Hung = true;
            TerminateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? held = null;
            for (int i = 0; i < 200 && !TerminateEntered.Task.IsCompleted; i++)
            {
                Clock += 2000;
                held = Lifecycle.RunRecoveryObservationForTestAsync();
                if (!TerminateEntered.Task.IsCompleted) await held.ConfigureAwait(false);
                else break;
            }
            Check(TerminateEntered.Task.IsCompleted && held is not null, "hang recovery must reach the held terminate");
            return held!;
        }

        public void Dispose()
        {
            try { Lifecycle.Dispose(); } catch { }
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }

    internal static async Task<JsonElement> RunAsync()
    {
        var done = new List<string>();

        // 1. baseline, no overlap: the terminated game's own owned session is cleaned up exactly once
        using (var env = new Env())
        {
            string a = await env.StartAsync();
            int pidA = env.Current.Pid;
            env.TerminateGate = null;
            Task held = await env.DriveToHeldHangTerminateAsync();
            env.TerminateGate!.SetResult();
            await held;
            Check(env.StopCallsFor(pidA) == 1, "same-instance cleanup exactly once: " + string.Join(",", env.Calls));
            done.Add("no-overlap-cleans-the-terminated-instance-once");
        }

        // 2. user Stop then a NEW Start while the old terminate is parked; late success must not clean the new instance
        using (var env = new Env())
        {
            string a = await env.StartAsync();
            int pidA = env.Current.Pid;
            Task held = await env.DriveToHeldHangTerminateAsync();
            await env.StopAsync(a);
            env.BridgeOnline = true; env.Hung = false;
            string b = await env.StartAsync();
            int pidB = env.Current.Pid;
            Check(pidB != pidA && env.Current.Alive, "second instance running");
            env.TerminateGate!.SetResult();                                  // late success of the OLD effect
            await held;
            Check(env.StopCallsFor(pidB) == 0, "late old success must not stop/clean the NEW instance: " + string.Join(",", env.Calls));
            Check(env.Current.Alive && env.Instances.Count == 2, "new instance must stay running");
            done.Add("late-success-after-stop-start-does-not-touch-new-instance");
        }

        // 3. same overlap, late REJECTION of the old terminate
        using (var env = new Env())
        {
            string a = await env.StartAsync();
            Task held = await env.DriveToHeldHangTerminateAsync();
            await env.StopAsync(a);
            env.BridgeOnline = true; env.Hung = false;
            string b = await env.StartAsync();
            int pidB = env.Current.Pid;
            int startsBefore = env.StartCalls;
            env.TerminateFailure = new BridgeCommandException("GAME_RECOVERY_TERMINATE_FAILED", "late rejection");
            env.TerminateGate!.SetResult();
            await held;
            Check(env.StartCalls == startsBefore && env.StopCallsFor(pidB) == 0 && env.Current.Alive, "late rejection must not relaunch or clean");
            Check(env.Lifecycle.CurrentRecoveryStatus.State is "idle", "stale run must not publish a failure after Stop: " + env.Lifecycle.CurrentRecoveryStatus.State);
            done.Add("late-rejection-after-stop-start-publishes-nothing");
        }

        // 4. profile replacement (Close/Dispose) while the old terminate is parked
        using (var env = new Env())
        {
            string a = await env.StartAsync();
            Task held = await env.DriveToHeldHangTerminateAsync();
            Task dispose = Task.Run(env.Lifecycle.Dispose);
            Check(await Task.WhenAny(dispose, Task.Delay(5000)) == dispose, "Dispose must not block on a parked terminate");
            int statusBefore = env.StatusStates.Count;          // anything published by the close itself is already in
            int callsBefore = env.Calls.Count;
            env.TerminateGate!.SetResult();
            try { await held; } catch (OperationCanceledException) { }
            await Task.Delay(50);
            Check(env.StartCalls == 1 && env.Calls.Count == callsBefore, "no relaunch/cleanup after the owner was closed: " + string.Join(",", env.Calls.Skip(callsBefore)));
            Check(env.StatusStates.Count == statusBefore, "no recovery status published after close: " + string.Join(",", env.StatusStates.Skip(statusBefore)));
            done.Add("close-while-terminate-parked-is-quiet");
        }
        // 5. restoration (helper stop) failure during an intentional Stop: stays retryable, nothing resurrects it
        using (var env = new Env())
        {
            string a = await env.StartAsync();
            int pidA = env.Current.Pid;
            env.FailStops = 1;
            bool failed = false;
            try { await env.StopAsync(a); } catch (BridgeCommandException) { failed = true; } catch (InvalidOperationException) { failed = true; }
            Check(failed, "scripted restoration failure must surface");
            Check(env.Lifecycle.CurrentRecoveryStatus.State == "idle", "no recovery after an intentional Stop with a failed restoration");
            env.Current.Alive = false;
            for (int i = 0; i < 4; i++) { env.Clock += 2000; await env.Lifecycle.RunRecoveryObservationForTestAsync(); }
            Check(env.StartCalls == 1, "desired-running was cleared by the Stop: recovery must not relaunch: " + string.Join(",", env.Calls));
            await env.StopAsync(a);                                           // retry restores
            Check(env.StopCallsFor(pidA) == 2, "retry reaches the helper again: " + string.Join(",", env.Calls));
            done.Add("stop-restoration-failure-is-retryable-and-not-resurrected");
        }

        // 6. Close (profile replacement) while a recovery RELAUNCH is parked in the helper
        using (var env = new Env())
        {
            await env.StartAsync();
            env.Current.Alive = false;
            env.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var background = new List<Task>();
            for (int i = 0; i < 60 && !env.StartEntered.Task.IsCompleted; i++)
            {
                env.Clock += 2000;
                Task observe = env.Lifecycle.RunRecoveryObservationForTestAsync();
                background.Add(observe);
                await Task.WhenAny(observe, env.StartEntered.Task, Task.Delay(1000));
                if (env.StartEntered.Task.IsCompleted) break;
                Task tick = env.Lifecycle.RunRecoveryRunTickForTestAsync();
                background.Add(tick);
                await Task.WhenAny(tick, env.StartEntered.Task, Task.Delay(1000));
            }
            Check(env.StartEntered.Task.IsCompleted, "relaunch must reach the helper");
            Task dispose = Task.Run(env.Lifecycle.Dispose);
            Check(await Task.WhenAny(dispose, Task.Delay(5000)) == dispose, "Dispose must not block on a parked relaunch");
            int startsParked = env.StartCalls;
            env.StartGate.SetResult();
            await Task.Delay(200);
            Check(env.StartCalls == startsParked, "no further launch after the owner was closed");
            int newGamePid = env.Current.Pid;
            Check(env.StopCallsFor(newGamePid) == 1 || !env.Current.Alive,
                "a game started by a cancelled start must be stopped/cleaned: " + string.Join(",", env.Calls));
            done.Add("close-while-relaunch-parked-cleans-the-cancelled-start");
        }

        // 6b. intentional Stop while a recovery RELAUNCH is parked in the helper: CURRENT-CLIENT characterisation
        // (the original Stop handler 0x199627 has no counterpart for an in-flight relaunch; not a parity claim): the lifecycle
        // refuses the Stop as in progress, keeps desired-running, and the parked relaunch still completes into a running game.
        using (var env = new Env())
        {
            string a = await env.StartAsync();
            env.Current.Alive = false;
            env.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var background = new List<Task>();
            for (int i = 0; i < 60 && !env.StartEntered.Task.IsCompleted; i++)
            {
                env.Clock += 2000;
                Task observe = env.Lifecycle.RunRecoveryObservationForTestAsync();
                background.Add(observe);
                await Task.WhenAny(observe, env.StartEntered.Task, Task.Delay(1000));
                if (env.StartEntered.Task.IsCompleted) break;
                Task tick = env.Lifecycle.RunRecoveryRunTickForTestAsync();
                background.Add(tick);
                await Task.WhenAny(tick, env.StartEntered.Task, Task.Delay(1000));
            }
            Check(env.StartEntered.Task.IsCompleted, "relaunch parked");
            string? code = null;
            try { await env.StopAsync(a); } catch (BridgeCommandException ex) { code = ex.Code; }
            Check(code is "GAME_OPERATION_IN_PROGRESS" or "INSTANCE_NOT_OWNED", "Stop during a parked relaunch is refused, not interleaved: " + code);
            env.StartGate.SetResult();
            await Task.WhenAll(background.Select(t => t.ContinueWith(_ => { }))).WaitAsync(TimeSpan.FromSeconds(5));
            Check(env.Instances.Count == 2 && env.Current.Alive, "the parked relaunch completes into a running game");
            done.Add("stop-during-parked-relaunch-is-refused-characterisation");
        }

        // 7. timer teardown: a closed owner ignores later monitor ticks and publishes nothing
        using (var env = new Env())
        {
            await env.StartAsync();
            env.Lifecycle.Dispose();
            int calls = env.Calls.Count, statuses = env.StatusStates.Count;
            env.Current.Alive = false;
            for (int i = 0; i < 6; i++) { env.Clock += 2000; await env.Lifecycle.RunRecoveryObservationForTestAsync(); }
            Check(env.Calls.Count == calls && env.StatusStates.Count == statuses, "closed owner must be inert");
            done.Add("closed-owner-ignores-monitor-ticks");
        }
        return JsonSerializer.SerializeToElement(new { ok = true, cases = done });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("recovery async ownership check failed: " + message);
    }
}
