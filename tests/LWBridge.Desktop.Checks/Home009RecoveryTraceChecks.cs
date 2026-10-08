using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// HOME 009 C/D trace harness. Runs the ACTUAL OverviewLifecycleService (inert hooks, virtual clock) through
/// scenario worlds shared verbatim with the reconstructed 0.3.17 oracle
/// (tools/lwbridge317/home009_recovery_scenarios.py) and writes the effect/status trace as JSON.
/// No process is created, terminated or inspected; no desktop or live access.
/// </summary>
internal static class Home009RecoveryTraceChecks
{
    private const long EpochBase = 1_700_000_000_000;
    private const int BasePid = 41000;

    internal static async Task RunAsync(string scenariosPath, string outputPath)
    {
        JsonArray scenarios = JsonNode.Parse(File.ReadAllText(scenariosPath))!.AsArray();
        var result = new JsonObject();
        foreach (JsonNode? node in scenarios)
        {
            JsonObject scenario = node!.AsObject();
            string name = scenario["name"]!.GetValue<string>();
            result[name] = await RunScenarioAsync(scenario).ConfigureAwait(false);
        }
        File.WriteAllText(outputPath, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static T Piece<T>(JsonNode? signal, long t, T fallback)
    {
        if (signal is not JsonArray array) return fallback;
        T value = fallback;
        foreach (JsonNode? entry in array)
        {
            JsonArray pair = entry!.AsArray();
            if (pair[0]!.GetValue<long>() <= t) value = pair[1]!.GetValue<T>();
            else break;
        }
        return value;
    }

    private static JsonNode? PieceNode(JsonNode? signal, long t)
    {
        if (signal is not JsonArray array) return null;
        JsonNode? value = null;
        foreach (JsonNode? entry in array)
        {
            JsonArray pair = entry!.AsArray();
            if (pair[0]!.GetValue<long>() <= t) value = pair[1];
            else break;
        }
        return value;
    }

    private sealed class Instance
    {
        public int Pid;
        public long Start;
        public bool Alive = true;
        public bool Relative;
        public long? CrashAt;
        public string Session = "";
        public string Challenge = "";
        public string StartedAtUtc = "";
    }

    private sealed class Sim
    {
        public JsonObject Scenario = null!;
        public long Clock;
        public readonly List<Instance> Instances = new();
        public readonly JsonArray Events = new();
        public int Launches;
        public int StartCalls;
        public bool UpdatersKilled;
        public LocalConfigStore Config = null!;
        public string ProfileId = "";
        public string GamePath = "";
        public Instance Current => Instances[^1];
        public long UnixMs => EpochBase + Clock;

        public void SetTime(long t)
        {
            Clock = t;
            foreach (Instance inst in Instances)
                if (inst.Alive && inst.CrashAt is long crash && t >= crash) inst.Alive = false;
            bool reconnect = Piece(Scenario["autoReconnect"], t, true);
            if (Config.Snapshot.AutoReconnect != reconnect)
                Config.Update(c => c with { AutoReconnect = reconnect });
        }

        public T Signal<T>(string name, T fallback)
        {
            Instance inst = Current;
            if (!inst.Relative)
                return Piece(Scenario["initial"]?[name], Clock, fallback);
            return Piece(Scenario["launch"]?[name], Clock - inst.Start, fallback);
        }

        public bool Online => Current.Alive && Signal("online", true);
        public bool Healthy => Current.Alive && Signal("healthy", true);
        public bool Hung => Current.Alive && Signal("hung", false);
    }

    private static async Task<JsonNode> RunScenarioAsync(JsonObject scenario)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-home009-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        try
        {
            var sim = new Sim { Scenario = scenario };
            string profileId = "profile-home009";
            sim.ProfileId = profileId;
            sim.GamePath = Path.Combine(root, "Game", "LastWar.exe");
            sim.Config = new LocalConfigStore(Path.Combine(root, "config"));
            sim.Config.Update(c => c with
            {
                ProfileId = profileId,
                GameRoot = root,
                AutoLaunchGame = false,
                AutoReconnect = Piece(scenario["autoReconnect"], 0, true),
            });
            JsonArray fails = scenario["launch"]?["fails"]?.AsArray() ?? new JsonArray();
            JsonArray logs = scenario["logs"]?.AsArray() ?? new JsonArray();
            int logCursor = 0;
            var logTexts = new Dictionary<string, StringBuilder>
            {
                ["player"] = new(), ["launcher"] = new(), ["updater"] = new(),
            };

            void Record(string kind, Action<JsonObject>? fill = null)
            {
                var e = new JsonObject { ["t"] = sim.Clock, ["kind"] = kind };
                fill?.Invoke(e);
                sim.Events.Add(e);
            }

            Instance NewInstance(bool relative, long? crashAt, string session, string challenge, int index)
            {
                var inst = new Instance
                {
                    Pid = BasePid + index, Start = sim.Clock, Relative = relative, CrashAt = crashAt,
                    Session = session, Challenge = challenge,
                    StartedAtUtc = $"2026-09-20T09:{index:00}:00.0000000Z",
                };
                sim.Instances.Add(inst);
                return inst;
            }

            byte[] Heartbeat()
            {
                Instance inst = sim.Current;
                if (!sim.Online) throw new IOException("synthetic bridge disconnect");
                bool healthy = sim.Healthy;
                JsonNode? ev = PieceNode(scenario["event"], sim.Clock);
                return JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1,
                    bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId,
                    sessionId = inst.Session,
                    challenge = inst.Challenge,
                    gamePid = inst.Pid,
                    updatedAt = sim.UnixMs / 1000,
                    ready = true,
                    messageVisible = true,
                    messageText = OverviewLifecycleService.ReadyMessage,
                    gameStateObserved = true,
                    gameReady = healthy,
                    loggedIn = healthy,
                    connected = healthy,
                    connecting = false,
                    gameUid = healthy ? "home009" : "",
                    serverId = healthy ? 2212 : 0,
                    worldPos = healthy ? 12345 : 0,
                    recoveryObserved = ev?["observed"]?.GetValue<bool>() ?? false,
                    recoveryConfirmed = ev?["confirmed"]?.GetValue<bool>() ?? false,
                    recoveryAmbiguous = ev?["ambiguous"]?.GetValue<bool>() ?? false,
                    recoveryReason = ev?["reason"]?.GetValue<string?>(),
                    recoveryUpdateDetected = ev?["update"]?.GetValue<bool>() ?? false,
                });
            }

            Task<JsonElement> Helper(OverviewHelperInvocation invocation, CancellationToken _)
            {
                if (invocation.Operation == "start")
                {
                    sim.StartCalls++;
                    bool initial = sim.StartCalls == 1;
                    if (!initial)
                    {
                        sim.Launches++;
                        int launchNumber = sim.Launches;
                        Record("launch", e => e["n"] = launchNumber);
                        if (launchNumber <= fails.Count && fails[launchNumber - 1]!.GetValue<bool>())
                            throw new BridgeCommandException("LAUNCH_FAILED", "synthetic launch failure");
                    }
                    long? crash = initial
                        ? scenario["initial"]?["crashAt"]?.GetValue<long?>()
                        : (scenario["launch"]?["crashAfterMs"]?.GetValue<long?>() is long after ? sim.Clock + after : null);
                    Instance inst = NewInstance(!initial, crash, invocation.SessionId!, invocation.Challenge!, initial ? 0 : sim.Launches);
                    string challengeHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant();
                    long now = sim.UnixMs / 1000;
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
                    {
                        ok = true,
                        mode = "overview_install_launch_ready_deferred_restore",
                        bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId,
                        sessionId = invocation.SessionId,
                        challengeSha256 = challengeHash,
                        gamePid = inst.Pid,
                        launcherPid = inst.Pid + 10000,
                        gamePath = sim.GamePath,
                        gameStartedAtUtc = inst.StartedAtUtc,
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
                            gamePid = inst.Pid,
                            ready = true,
                            messageVisible = true,
                            messageText = OverviewLifecycleService.ReadyMessage,
                            readyAt = now,
                            updatedAt = now,
                        },
                    }));
                }

                Record("cleanup");
                sim.Current.Alive = false;
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
                    close = new { method = "synthetic", accepted = true, processExited = true, alreadyExited = false },
                    restore = new { restored = true },
                    gameRunning = false,
                    installedFilesChanged = false,
                }));
            }

            var hooks = new OverviewLifecycleTestHooks
            {
                ProcessMatches = (pid, path, startedAt) =>
                    sim.Current.Alive && pid == sim.Current.Pid && startedAt == sim.Current.StartedAtUtc &&
                    string.Equals(Path.GetFullPath(path), Path.GetFullPath(sim.GamePath), StringComparison.OrdinalIgnoreCase),
                ReadAllBytes = _ => Heartbeat(),
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
                UtcNow = () => DateTimeOffset.FromUnixTimeMilliseconds(sim.UnixMs),
                MonotonicMilliseconds = () => sim.Clock,
                UpdateProcessRunning = () => !sim.UpdatersKilled && Piece(scenario["updating"], sim.Clock, false),
                UpdateActivityFingerprint = () => "fp:" + Piece(scenario["fingerprint"], sim.Clock, 0),
                ProcessHung = (_, _) => sim.Hung,
                TerminateOwnedProcessAsync = (pid, _, _, _) =>
                {
                    Record("terminate", e => e["pid"] = pid);
                    if (Piece(scenario["terminateFails"], sim.Clock, false))
                        throw new BridgeCommandException("GAME_RECOVERY_TERMINATE_FAILED", "synthetic terminate failure");
                    foreach (Instance inst in sim.Instances)
                        if (inst.Pid == pid) inst.Alive = false;
                    return Task.CompletedTask;
                },
                TerminateUpdateProcessesAsync = _ =>
                {
                    Record("killUpdaters");
                    if (scenario["killUpdatersStopsUpdating"]?.GetValue<bool>() ?? true) sim.UpdatersKilled = true;
                    return Task.CompletedTask;
                },
                DelayAsync = (delay, token) =>
                {
                    // inline (pre-port) recovery consumes virtual time; end the scenario at its horizon
                    if (sim.Clock + (long)delay.TotalMilliseconds > scenario["durationMs"]!.GetValue<long>() + 600000)
                        throw new OperationCanceledException("scenario horizon");
                    sim.SetTime(sim.Clock + (long)delay.TotalMilliseconds);
                    token.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
                RunHelperAsync = Helper,
                CreateRecoveryLogReader = name => new HarnessLogReader(logTexts[name switch
                {
                    "Player.log" => "player",
                    "Launcher.log" => "launcher",
                    _ => "updater",
                }]),
            };

            using var lifecycle = new OverviewLifecycleService(
                profileId, root,
                helperPath: Path.Combine(root, "fake-overview-helper.py"),
                requireCurrentClientEvidence: false,
                config: sim.Config,
                testHooks: hooks,
                startRecoveryMonitor: false);
            bool recording = true;
            lifecycle.RecoveryStatusChanged += status => { if (recording) Record("status", e =>
            {
                e["state"] = status.State;
                e["reason"] = status.Reason;
                e["attempts"] = status.Attempts;
                e["nextRetryAt"] = status.NextRetryAt is long next ? next - EpochBase : null;
                e["error"] = status.Error;
                e["updateDetected"] = status.UpdateDetected;
                e["restarted"] = status.Restarted;
                e["completedAt"] = status.CompletedAt is long done ? done - EpochBase : null;
                e["noticeId"] = (long)status.NoticeId;
            }); };

            sim.SetTime(0);
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { profileId }),
                CancellationToken.None).ConfigureAwait(false);
            // initial Start produced instance 0 (alive at t=0); discard its start-time events
            sim.Events.Clear();

            MethodInfo? runTick = typeof(OverviewLifecycleService).GetMethod(
                "RunRecoveryRunTickForTestAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            long duration = scenario["durationMs"]!.GetValue<long>();
            long? nextRun = null;
            for (long t = 0; t <= duration;)
            {
                if (sim.Clock < t) sim.SetTime(t);
                PumpLogs(logTexts, logs, ref logCursor, sim);
                await lifecycle.RunRecoveryObservationForTestAsync().ConfigureAwait(false);
                if (runTick is not null)
                {
                    bool active = lifecycle.CurrentRecoveryStatus.State is "waiting" or "repairing" or "launching"
                        or "verifying" or "updating" or "maintenance";
                    if (active)
                    {
                        nextRun ??= t + 2000;
                        while (nextRun <= sim.Clock && active)
                        {
                            await ((Task)runTick.Invoke(lifecycle, null)!).ConfigureAwait(false);
                            nextRun += 2000;
                            active = lifecycle.CurrentRecoveryStatus.State is "waiting" or "repairing" or "launching"
                                or "verifying" or "updating" or "maintenance";
                        }
                        if (!active) nextRun = null;
                    }
                }
                long after = Math.Max(t, sim.Clock);
                t = (after / 2000 + 1) * 2000;
            }
            recording = false;     // disposal finishes any active run idle; that is not part of the scenario
            return sim.Events;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private sealed class HarnessLogReader(StringBuilder text) : IRecoveryLogReader
    {
        private int offset;
        public void Begin() => offset = text.Length;
        public string ReadNew()
        {
            string value = text.ToString(offset, text.Length - offset);
            offset = text.Length;
            return value;
        }
    }

    private static void PumpLogs(Dictionary<string, StringBuilder> texts, JsonArray logs, ref int cursor, Sim sim)
    {
        while (cursor < logs.Count && logs[cursor]![0]!.GetValue<long>() <= sim.Clock)
        {
            JsonArray entry = logs[cursor]!.AsArray();
            texts[entry[1]!.GetValue<string>()].Append(entry[2]!.GetValue<string>());
            cursor++;
        }
    }
}
