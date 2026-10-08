using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Native deterministic R3 checkpoint. All processes are synthetic; no game,
// launcher, updater, protected service or original binary is executed.
internal static class OverviewAdoptionChecks
{
    private const int GamePid = 4242;
    private const string Profile = "primary";
    private const string Session = "r3-original-session";
    private const string StartedAt = "2026-10-08T11:00:00.0000000Z";
    private static readonly string Challenge = new('a', 64);
    private static readonly string Token =
        Convert.ToBase64String(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("HOME009 R3 adoption: " + label);
    }

    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "home009-r3-adoption-" +
            Guid.NewGuid().ToString("N"));
        var passed = new List<string>();
        try
        {
            Directory.CreateDirectory(root);
            string gameRoot = Path.Combine(root, "selected-game");
            string game = Path.Combine(gameRoot, "Game", "LastWar.exe");
            string runtime = Path.Combine(root, "runtime");
            string backup = Path.Combine(root, "backups");
            Directory.CreateDirectory(runtime);
            Directory.CreateDirectory(backup);
            var record = new OverviewAdoptionSnapshot(Profile, Session, Challenge, GamePid,
                game, StartedAt, OverviewLifecycleService.BridgeVersion,
                Token, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), true);
            byte[] encoded = OverviewAdoptionRecord.Serialize(record);
            Check(OverviewAdoptionRecord.TryDeserialize(encoded, out var decoded) &&
                decoded == record, "Windows DPAPI roundtrip including session identity");
            Check(!System.Text.Encoding.UTF8.GetString(encoded).Contains(Token,
                StringComparison.Ordinal), "plaintext pipe token leaked in journal");
            passed.Add("dpapi-roundtrip-token-not-plaintext");
            // Production WebView IPC uses Web camelCase, unlike default
            // JsonSerializer.SerializeToElement used by native test snapshots.
            // Original 0x2055db projects the FIRST code member as a string,
            // and App.jsx reads result.errors[].profileId/error/message.
            JsonElement wireError = JsonSerializer.SerializeToElement(
                new OverviewStartupError(Profile, "RECOVERY_RECORD_INVALID",
                    "A separate human-readable detail"), JsonOptions.Default);
            Check(wireError.GetProperty("profileId").GetString() == Profile &&
                wireError.GetProperty("error").GetString() == "RECOVERY_RECORD_INVALID" &&
                wireError.GetProperty("message").GetString() == "A separate human-readable detail",
                "production WebView error projected message instead of code or used PascalCase");
            passed.Add("original-first-code-string-through-production-webview-casing");
            var original = JsonDocument.Parse(encoded).RootElement;
            string serialized = System.Text.Encoding.UTF8.GetString(encoded);
            Check(!OverviewAdoptionRecord.TryDeserialize(System.Text.Encoding.UTF8.GetBytes(
                serialized.Replace($"\"pid\":{GamePid}", "\"pid\":4243")), out _),
                "process PID tamper was admitted");
            Check(!OverviewAdoptionRecord.TryDeserialize(System.Text.Encoding.UTF8.GetBytes(
                serialized.Replace($"\"buildId\":\"{record.BuildId}\"",
                    "\"buildId\":\"older-build\"")), out _),
                "build identity tamper was admitted");
            Check(!OverviewAdoptionRecord.TryDeserialize(System.Text.Encoding.UTF8.GetBytes(
                serialized.Replace("\"sessionId\":\"r3-original-session\"",
                    "\"sessionId\":\"obsolete\"")), out _),
                "session alias tamper was admitted");
            Check(!OverviewAdoptionRecord.TryDeserialize(System.Text.Encoding.UTF8.GetBytes(
                serialized.Replace("{", "{\"pid\":1,", StringComparison.Ordinal)
                    ), out _), "duplicate JSON key was admitted");
            passed.Add("dpapi-tamper-and-duplicate-keys-fail-closed");

            using (var registry = new RegistryHolder())
            {
                registry.Host.RestoreLaunchBinding(record, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Check(!registry.Registry.TryAdmit(Profile, Session, new string('A', 43),
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), new object(), out _),
                    "wrong token admitted");
                Check(registry.Registry.TryAdmit(Profile, Session, Token,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), new object(), out ulong generation) &&
                    generation != 0 && registry.Host.IsRouteConnected(Session),
                    "retained exact token rejected");
                Check(registry.Registry.RemoveConnected(Session, generation) &&
                      !registry.Host.IsRouteConnected(Session),
                    "generation-scoped teardown failed");
                passed.Add("restored-credential-through-actual-host-registry");
                CheckThrows<BridgeCommandException>(() =>
                    registry.Host.RestoreLaunchBinding(record with { BuildId = "outdated" },
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                    "outdated build must not bind as current");
                passed.Add("outdated-build-rejected-from-host-registry");
            }

            WriteRepair(runtime, backup, record);
            WriteHeartbeat(runtime, record);
            File.WriteAllBytes(Path.Combine(runtime, OverviewAdoptionRecord.FileName), encoded);
            for (int restart = 0; restart < 2; restart++)
            {
                using var holder = new RegistryHolder();
                int helperCalls = 0;
                var hooks = new OverviewLifecycleTestHooks
                {
                    ProcessMatches = (pid, path, started) =>
                        pid == GamePid && string.Equals(path, game, StringComparison.OrdinalIgnoreCase) &&
                        started == StartedAt,
                    ReadAllBytes = File.ReadAllBytes,
                    SelectedGamePids = _ => new[] { GamePid },
                    RunHelperAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref helperCalls);
                        throw new InvalidOperationException("Adoption must not stop or launch a game");
                    }
                };
                using var lifecycle = Create(Profile, gameRoot, runtime, backup, root,
                    holder.Host, hooks);
                object? result = await lifecycle.InvokeAsync("profile_instances_reconcile",
                    JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                    CancellationToken.None).ConfigureAwait(false);
                JsonElement errors = JsonSerializer.SerializeToElement(result).GetProperty("errors");
                Check(errors.GetArrayLength() == 0 && lifecycle.RuntimeManaged &&
                    lifecycle.IsReady && helperCalls == 0, "same-build restart adopted without launch");
                Check(holder.Registry.PendingCount == 1 &&
                    holder.Registry.TryAdmit(Profile, Session, Token,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        new object(), out _),
                    "new host registration did not retain exact token");
                Check(lifecycle.CurrentConnectionState == "connected",
                    "synthetic heartbeat did not agree with restored state");
                passed.Add(restart == 0 ? "same-build-inert-first-adoption" :
                    "same-build-second-host-restart-no-kill");
            }
            Check(File.Exists(Path.Combine(runtime, OverviewAdoptionRecord.FileName)),
                "host close removed a valid adoption record");
            passed.Add("host-close-preserves-owned-record");

            foreach (var (scenario, bytes, profile, expected) in new[]
            {
                ("missing", (byte[]?)null, Profile, "RECOVERY_RECORD_NOT_FOUND"),
                ("invalid", System.Text.Encoding.UTF8.GetBytes("{broken json"), Profile, "RECOVERY_RECORD_INVALID"),
                ("process-mismatch", OverviewAdoptionRecord.Serialize(
                    record with { Pid = 4444 }), Profile, "RECOVERY_PROCESS_MISMATCH"),
                ("profile-replacement", encoded, "secondary", "RECOVERY_PROCESS_MISMATCH")
            })
            {
                string basePath = Path.Combine(root, scenario);
                string r = Path.Combine(basePath, "runtime");
                string b = Path.Combine(basePath, "backup");
                Directory.CreateDirectory(r);
                Directory.CreateDirectory(b);
                var syntheticRecord = record with { ProfileId = profile };
                WriteRepair(r, b, syntheticRecord);
                WriteHeartbeat(r, syntheticRecord);
                if (bytes is not null)
                    File.WriteAllBytes(Path.Combine(r, OverviewAdoptionRecord.FileName), bytes);
                int helperCalls = 0;
                var hooks = new OverviewLifecycleTestHooks
                {
                    ProcessMatches = (pid, path, started) =>
                        pid == GamePid && path.Equals(game, StringComparison.OrdinalIgnoreCase) &&
                        started == StartedAt,
                    ReadAllBytes = File.ReadAllBytes,
                    RunHelperAsync = (_, _) =>
                    {
                        Interlocked.Increment(ref helperCalls);
                        throw new InvalidOperationException("Unexpected start/stop");
                    },
                };
                using var holder = new RegistryHolder();
                using var lifecycle = Create(profile, gameRoot, r, b, basePath, holder.Host, hooks);
                JsonElement errors = JsonSerializer.SerializeToElement(
                    await lifecycle.InvokeAsync("profile_instances_reconcile",
                        JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                        CancellationToken.None))
                    .GetProperty("errors");
                Check(errors.GetArrayLength() == 1 &&
                    errors[0].GetProperty("Error").GetString() == expected &&
                    helperCalls == 0 && !lifecycle.RuntimeManaged,
                    scenario + " did not fail closed without touching a process");
                passed.Add(scenario + "-fails-closed");
            }
            await TestProfileAbaAndCancelledAdoptionAsync(root, gameRoot, game, record, encoded, passed);
            await TestDeferredAdoptionReadinessAsync(root, gameRoot, game, record, encoded, passed);
            await TestExplicitStopAndOutdatedRepairAsync(root, gameRoot, game, record, encoded, passed);
            await TestRetiredAdoptionCannotDisableSuccessorAsync(root, passed);
            TestRegistrationScopedRetirement(record, passed);
            return JsonSerializer.SerializeToElement(new
            {
                ok = true, tests = passed, count = passed.Count, gameLaunches = 0,
                actualGameProcessesTerminated = 0, retainedOriginalToken = true
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task TestProfileAbaAndCancelledAdoptionAsync(
        string root, string gameRoot, string game, OverviewAdoptionSnapshot source,
        byte[] encoded, List<string> passed)
    {
        string folder = Path.Combine(root, "profile-aba");
        string runtime = Path.Combine(folder, "runtime");
        string backup = Path.Combine(folder, "backup");
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(backup);
        WriteRepair(runtime, backup, source);
        WriteHeartbeat(runtime, source);
        string recordPath = Path.Combine(runtime, OverviewAdoptionRecord.FileName);
        File.WriteAllBytes(recordPath, encoded);
        int helperCount = 0;
        OverviewLifecycleTestHooks Hooks() => new()
        {
            ProcessMatches = (pid, path, created) =>
                pid == GamePid && path.Equals(game, StringComparison.OrdinalIgnoreCase) &&
                created == StartedAt,
            ReadAllBytes = File.ReadAllBytes,
            RunHelperAsync = (_, _) =>
            {
                helperCount++;
                throw new InvalidOperationException("Profile replacement must not touch game");
            },
        };

        // The exact same running game belongs to A. A switch to B must not
        // consume A's protected journal; returning to A must adopt without
        // a new process. The source multi-profile lease rule is NOT inferred.
        using (var hostA = new RegistryHolder())
        using (var lifecycleA = Create(Profile, gameRoot, runtime, backup, folder,
            hostA.Host, Hooks()))
        {
            JsonElement first = JsonSerializer.SerializeToElement(
                await lifecycleA.InvokeAsync("profile_instances_reconcile",
                    JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                    CancellationToken.None));
            Check(first.GetProperty("errors").GetArrayLength() == 0 &&
                lifecycleA.IsReady, "first A did not adopt game");
            JsonElement duplicate = JsonSerializer.SerializeToElement(
                await lifecycleA.InvokeAsync("profile_instances_reconcile",
                    JsonSerializer.SerializeToElement(new { autoLaunchAll = true }),
                    CancellationToken.None));
            Check(duplicate.GetProperty("errors").GetArrayLength() == 0 &&
                lifecycleA.IsReady && helperCount == 0,
                "consumed reconcile launched game twice");
            passed.Add("same-host-reconcile-consumed-exactly-once");
        }
        using (var hostB = new RegistryHolder())
        using (var lifecycleB = Create("secondary", gameRoot, runtime, backup, folder,
            hostB.Host, Hooks()))
        {
            JsonElement answer = JsonSerializer.SerializeToElement(
                await lifecycleB.InvokeAsync("profile_instances_reconcile",
                    JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                    CancellationToken.None));
            Check(answer.GetProperty("errors").GetArrayLength() <= 1 &&
                !lifecycleB.RuntimeManaged && helperCount == 0 &&
                File.ReadAllBytes(recordPath).SequenceEqual(encoded),
                "B claimed or damaged A's retained protected journal");
        }
        using (var returnHost = new RegistryHolder())
        using (var lifecycleA = Create(Profile, gameRoot, runtime, backup, folder,
            returnHost.Host, Hooks()))
        {
            JsonElement returned = JsonSerializer.SerializeToElement(
                await lifecycleA.InvokeAsync("profile_instances_reconcile",
                    JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                    CancellationToken.None));
            Check(returned.GetProperty("errors").GetArrayLength() == 0 &&
                lifecycleA.IsReady && helperCount == 0,
                "A was lost after an unrelated B selection");
        }
        passed.Add("same-process-profile-A-B-A-retains-A-journal");

        // A completed command-independent Close while adoption is parked must
        // retire only the new host lease; it must never call a helper, terminate
        // the game, delete the exact prior journal, or publish stale success.
        string cancelFolder = Path.Combine(root, "close-while-adopting");
        string cancelRuntime = Path.Combine(cancelFolder, "runtime");
        string cancelBackup = Path.Combine(cancelFolder, "backup");
        Directory.CreateDirectory(cancelRuntime);
        Directory.CreateDirectory(cancelBackup);
        WriteRepair(cancelRuntime, cancelBackup, source);
        WriteHeartbeat(cancelRuntime, source);
        string cancelRecord = Path.Combine(cancelRuntime, OverviewAdoptionRecord.FileName);
        File.WriteAllBytes(cancelRecord, encoded);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new OverviewLifecycleTestHooks
        {
            ProcessMatches = (pid, path, created) =>
                pid == GamePid && created == StartedAt &&
                path.Equals(game, StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = path => path.EndsWith("heartbeat.json", StringComparison.Ordinal)
                ? System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1}")
                : File.ReadAllBytes(path),
            DelayAsync = (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                entered.TrySetResult();
                return release.Task;
            },
            RunHelperAsync = (_, _) =>
            {
                helperCount++;
                throw new InvalidOperationException("Game must not be touched");
            },
        };
        using var holder = new RegistryHolder();
        using var lifecycle = Create(Profile, gameRoot, cancelRuntime, cancelBackup,
            cancelFolder, holder.Host, hooks);
        Task<object?> pending = lifecycle.InvokeAsync("profile_instances_reconcile",
            JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
            CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        lifecycle.Close();
        release.TrySetResult();
        JsonElement result = JsonSerializer.SerializeToElement(await pending);
        JsonElement errors = result.GetProperty("errors");
        Check(errors.GetArrayLength() == 1 &&
            errors[0].GetProperty("Error").GetString() == "GAME_OPERATION_CANCELLED" &&
            !lifecycle.RuntimeManaged && helperCount == 0 &&
            File.ReadAllBytes(cancelRecord).SequenceEqual(encoded) &&
            holder.Registry.PendingCount == 0,
            "closed owner published stale adoption or removed retained game journal");
        passed.Add("closed-owner-held-adoption-no-late-ready-or-kill");
    }

    private static async Task TestDeferredAdoptionReadinessAsync(
        string taskRoot, string gameRoot, string game, OverviewAdoptionSnapshot source,
        byte[] encoded, List<string> passed)
    {
        foreach (bool timeout in new[] { false, true })
        {
            string folder = Path.Combine(taskRoot, timeout ? "heartbeat-expired" : "heartbeat-delayed");
            string runtime = Path.Combine(folder, "runtime");
            string backup = Path.Combine(folder, "backup");
            Directory.CreateDirectory(runtime);
            Directory.CreateDirectory(backup);
            WriteRepair(runtime, backup, source);
            WriteHeartbeat(runtime, source);
            File.WriteAllBytes(Path.Combine(runtime, OverviewAdoptionRecord.FileName), encoded);

            bool heartbeatReady = false;
            int waited = 0;
            long clock = 1000;
            int touchedGame = 0;
            var hooks = new OverviewLifecycleTestHooks
            {
                ProcessMatches = (pid, path, created) =>
                    pid == GamePid && created == StartedAt &&
                    string.Equals(path, game, StringComparison.OrdinalIgnoreCase),
                MonotonicMilliseconds = () => clock,
                ReadAllBytes = path => path.EndsWith("heartbeat.json", StringComparison.Ordinal) &&
                    !heartbeatReady
                    ? System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1}")
                    : File.ReadAllBytes(path),
                DelayAsync = (_, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    waited++;
                    if (timeout) clock = 100_000;
                    else heartbeatReady = true;
                    return Task.CompletedTask;
                },
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
                RunHelperAsync = (_, _) =>
                {
                    touchedGame++;
                    throw new InvalidOperationException("Reconnect must never start/stop the game");
                },
            };
            using var holder = new RegistryHolder();
            using var lifecycle = Create(Profile, gameRoot, runtime, backup, folder, holder.Host, hooks);
            JsonElement errors = JsonSerializer.SerializeToElement(
                await lifecycle.InvokeAsync("profile_instances_reconcile",
                    JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                    CancellationToken.None)).GetProperty("errors");
            if (!timeout)
            {
                Check(errors.GetArrayLength() == 0 && lifecycle.IsReady && waited > 0 &&
                    touchedGame == 0, "authenticated route must await delayed heartbeat");
                passed.Add("authenticated-reconnect-waits-for-fresh-heartbeat");
            }
            else
            {
                Check(errors.GetArrayLength() == 1 &&
                    errors[0].GetProperty("Error").GetString() == "BRIDGE_DISCONNECTED" &&
                    waited > 0 && !lifecycle.RuntimeManaged && touchedGame == 0 &&
                    File.Exists(Path.Combine(runtime, OverviewAdoptionRecord.FileName)),
                    "stale heartbeat exhaustion must retain game and journal without launching or killing");
                passed.Add("heartbeat-timeout-retains-exact-game-journal");
            }
        }
    }

    private static async Task TestExplicitStopAndOutdatedRepairAsync(
        string taskRoot, string gameRoot, string game, OverviewAdoptionSnapshot source,
        byte[] encoded, List<string> passed)
    {
        foreach (bool outdated in new[] { false, true })
        {
            string path = Path.Combine(taskRoot, outdated ? "outdated-repair" : "explicit-stop");
            string runtime = Path.Combine(path, "runtime");
            string backup = Path.Combine(path, "backup");
            Directory.CreateDirectory(runtime);
            Directory.CreateDirectory(backup);
            OverviewAdoptionSnapshot record = outdated
                ? source with { BuildId = "legacy-build-not-current" } : source;
            WriteRepair(runtime, backup, record);
            WriteHeartbeat(runtime, record);
            File.WriteAllBytes(Path.Combine(runtime, OverviewAdoptionRecord.FileName),
                outdated ? OverviewAdoptionRecord.Serialize(record) : encoded);
            bool alive = true;
            int stopCalls = 0;
            int startCalls = 0;
            var hooks = new OverviewLifecycleTestHooks
            {
                ProcessMatches = (pid, executable, created) =>
                    alive && pid == GamePid && created == StartedAt &&
                    string.Equals(executable, game, StringComparison.OrdinalIgnoreCase),
                ReadAllBytes = File.ReadAllBytes,
                SelectedGamePids = _ => alive ? new[] { GamePid } : Array.Empty<int>(),
                RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                RunHelperAsync = (invocation, _) =>
                {
                    if (invocation.Operation == "stop")
                    {
                        Check(invocation.GamePid == GamePid &&
                              invocation.GameStartedAtUtc == StartedAt &&
                              invocation.SessionId == Session, "exact Stop identity changed");
                        stopCalls++;
                        alive = false; // controlled seam: only the recorded process exits
                        return Task.FromResult(JsonSerializer.SerializeToElement(new
                        {
                            ok = true, mode = "overview_exact_pid_close_restore",
                            bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = Session,
                            gamePid = GamePid, gamePath = game, gameStartedAtUtc = StartedAt,
                            close = new { method = "inert_exact_identity", accepted = true,
                                processExited = true, alreadyExited = false },
                            restore = new { restored = true },
                            gameRunning = false, installedFilesChanged = false
                        }));
                    }
                    startCalls++;
                    throw new BridgeCommandException("CONTROLLED_RELAUNCH_NOT_AUTHORIZED",
                        "Inert proof records the relaunch transition but launches no process");
                },
            };
            using var holder = new RegistryHolder();
            using var lifecycle = Create(Profile, gameRoot, runtime, backup, path, holder.Host, hooks);
            if (outdated)
            {
                JsonElement reconciled = JsonSerializer.SerializeToElement(
                    await lifecycle.InvokeAsync("profile_instances_reconcile",
                        JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                        CancellationToken.None));
                JsonElement errors = reconciled.GetProperty("errors");
                Check(errors.GetArrayLength() == 1 &&
                    errors[0].GetProperty("Error").GetString() ==
                    "CONTROLLED_RELAUNCH_NOT_AUTHORIZED",
                    "outdated build did not take repair/relaunch despite autoLaunchAll=false");
                Check(stopCalls == 1 && startCalls == 1 &&
                      !alive && !File.Exists(Path.Combine(runtime, OverviewAdoptionRecord.FileName)),
                    "outdated build did not clean its exact old record before bounded relaunch");
                passed.Add("outdated-build-repair-then-one-inert-relaunch");
            }
            else
            {
                var start = JsonSerializer.SerializeToElement(
                    await lifecycle.InvokeAsync("profile_instances_reconcile",
                        JsonSerializer.SerializeToElement(new { autoLaunchAll = false }),
                        CancellationToken.None));
                Check(start.GetProperty("errors").GetArrayLength() == 0 &&
                    lifecycle.RuntimeManaged, "same-build stop proof could not adopt");
                await lifecycle.InvokeAsync("profile_instance_stop",
                    JsonSerializer.SerializeToElement(new { instanceId = Session }),
                    CancellationToken.None);
                Check(stopCalls == 1 && startCalls == 0 && !alive &&
                    holder.Registry.PendingCount == 0 && !lifecycle.RuntimeManaged &&
                    !File.Exists(Path.Combine(runtime, OverviewAdoptionRecord.FileName)),
                    "explicit Stop did not preserve same-session exact cleanup");
                passed.Add("adopted-session-explicit-stop-record-cleanup");
            }
        }
    }


    // LEAD009R3-01: held adoption -> Stop -> successful new Start -> late old
    // completion (success-delay, fault, cancellation; before and after the new
    // Start). The successor must keep its lease timer, keep RENEWING its lease,
    // keep its registration and published state; the old attempt must still be
    // retired with its own exact-owner file cleanup.
    private static async Task TestRetiredAdoptionCannotDisableSuccessorAsync(
        string taskRoot, List<string> passed)
    {
        foreach (string outcome in new[] { "released", "faulted", "cancelled" })
        foreach (bool afterNewStart in new[] { false, true })
        {
            string root = Path.Combine(taskRoot, $"successor-{outcome}-{(afterNewStart ? "late" : "early")}");
            string gameRoot = Path.Combine(root, "selected");
            string runtime = Path.Combine(root, "runtime");
            string backups = Path.Combine(root, "backups");
            string game = Path.Combine(gameRoot, "Game", "LastWar.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(game)!);
            File.WriteAllBytes(game, new byte[] { 77, 90 });
            Directory.CreateDirectory(runtime);
            Directory.CreateDirectory(backups);
            var old = new OverviewAdoptionSnapshot(Profile, Session, Challenge, GamePid, game,
                StartedAt, OverviewLifecycleService.BridgeVersion, Token,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), true);
            File.WriteAllBytes(Path.Combine(runtime, OverviewAdoptionRecord.FileName),
                OverviewAdoptionRecord.Serialize(old));
            WriteRepair(runtime, backups, old);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool oldAlive = true, newAlive = false;
            int stage = 0;
            int helperLaunches = 0, helperStops = 0, successorLeaseWrites = 0, oldLeaseWritesAfterStop = 0;
            string? newSession = null;
            OverviewHelperInvocation? current = null;
            var hooks = new OverviewLifecycleTestHooks
            {
                ProcessMatches = (pid, path, started) =>
                    string.Equals(path, game, StringComparison.OrdinalIgnoreCase) &&
                    started == StartedAt && ((pid == GamePid && oldAlive) || (pid == 50000 && newAlive)),
                SelectedGamePids = _ => oldAlive ? new[] { GamePid } : Array.Empty<int>(),
                RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                WriteLease = (session, _, _) =>
                {
                    if (session == newSession) Interlocked.Increment(ref successorLeaseWrites);
                    else if (stage == 1) Interlocked.Increment(ref oldLeaseWritesAfterStop);
                },
                DelayAsync = (_, token) =>
                {
                    if (stage == 0) { entered.TrySetResult(); return release.Task; }
                    return Task.Delay(1, token);
                },
                ReadAllBytes = path =>
                {
                    if (path.EndsWith("heartbeat.json", StringComparison.Ordinal))
                    {
                        if (current is null)
                            return System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1}");
                        return JsonSerializer.SerializeToUtf8Bytes(new
                        {
                            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = current.SessionId,
                            challenge = current.Challenge, gamePid = 50000,
                            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ready = true,
                            messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage
                        });
                    }
                    if (path.EndsWith("game-reported.txt", StringComparison.Ordinal) && current is not null)
                        return System.Text.Encoding.UTF8.GetBytes(
                            $"schema=1\nbridgeVersion={OverviewLifecycleService.BridgeVersion}\n" +
                            $"sessionId={current.SessionId}\nchallenge={current.Challenge}\ngamePid=50000\n" +
                            $"deadlineMilliseconds={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 90000}\n");
                    return File.ReadAllBytes(path);
                },
                RunHelperAsync = (inv, _) =>
                {
                    if (inv.Operation == "stop")
                    {
                        Interlocked.Increment(ref helperStops);
                        oldAlive = false;
                        File.Delete(Path.Combine(runtime, "recovery.json"));
                        return Task.FromResult(JsonSerializer.SerializeToElement(new
                        {
                            ok = true, mode = "overview_exact_pid_close_restore",
                            bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = inv.SessionId, gamePid = inv.GamePid,
                            gamePath = inv.GamePath, gameStartedAtUtc = inv.GameStartedAtUtc,
                            close = new { method = "inert", accepted = true, processExited = true, alreadyExited = false },
                            restore = new { restored = true }, gameRunning = false, installedFilesChanged = false
                        }));
                    }
                    Interlocked.Increment(ref helperLaunches);
                    current = inv; newSession = inv.SessionId; newAlive = true;
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
                    {
                        ok = true, mode = "overview_install_launch_ready_deferred_restore",
                        bridgeVersion = OverviewLifecycleService.BridgeVersion, profileId = Profile,
                        sessionId = inv.SessionId,
                        challengeSha256 = Convert.ToHexString(SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(inv.Challenge!))).ToLowerInvariant(),
                        gamePid = 50000, launcherPid = 60000, gamePath = game, gameStartedAtUtc = StartedAt,
                        gameRunning = true, installedFilesChanged = true,
                        restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                        ready = new
                        {
                            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                            profileId = Profile, sessionId = inv.SessionId, challenge = inv.Challenge,
                            gamePid = 50000, ready = true, messageVisible = true,
                            messageText = OverviewLifecycleService.ReadyMessage, readyAt = now, updatedAt = now
                        }
                    }));
                },
            };
            using var holder = new RegistryHolder();
            using var life = Create(Profile, gameRoot, runtime, backups, root, holder.Host, hooks);
            string label = $"{outcome}/{(afterNewStart ? "after" : "before")}-new-start";
            Task<object?> pending = life.InvokeAsync("profile_instances_reconcile",
                JsonSerializer.SerializeToElement(new { autoLaunchAll = false }), CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await life.InvokeAsync("profile_instance_stop",
                JsonSerializer.SerializeToElement(new { profileId = Profile, instanceId = Session }),
                CancellationToken.None);
            stage = 1;
            void Release()
            {
                if (outcome == "released") release.TrySetResult();
                else if (outcome == "faulted") release.TrySetException(new InvalidOperationException("late helper fault"));
                else release.TrySetCanceled();
            }
            if (!afterNewStart) { Release(); await pending; }
            await life.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None);
            Check(newSession is not null && life.RuntimeManaged && life.IsReady, label + ": successor not running");
            var timerField = typeof(OverviewLifecycleService).GetField("leaseTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Check(timerField.GetValue(life) is not null, label + ": successor timer missing before late completion");
            if (afterNewStart) { Release(); await pending; }
            Check(timerField.GetValue(life) is not null,
                label + ": late retirement of the old attempt disabled the successor lease timer");
            int before = Volatile.Read(ref successorLeaseWrites);
            DateTime until = DateTime.UtcNow.AddSeconds(6);
            while (Volatile.Read(ref successorLeaseWrites) <= before && DateTime.UtcNow < until)
                await Task.Delay(100);
            Check(Volatile.Read(ref successorLeaseWrites) > before,
                label + ": successor lease renewal stopped after late old completion");
            Check(life.RuntimeManaged && life.IsReady && life.CurrentConnectionState == "connected" &&
                holder.Registry.PendingCount == 1 && holder.Registry.IsPending(newSession!),
                label + ": successor state or registration was removed by the old attempt");
            Check(helperLaunches == 1 && helperStops == 1 && oldLeaseWritesAfterStop == 0,
                label + ": unexpected launch/stop/old-owner lease write");
            life.Close();
            passed.Add("late-old-adoption-completion-keeps-successor-lease-" + outcome +
                (afterNewStart ? "-after-start" : "-before-start"));
        }
    }

    private static void TestRegistrationScopedRetirement(
        OverviewAdoptionSnapshot record, List<string> passed)
    {
        using var holder = new RegistryHolder();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ulong first = holder.Host.RestoreLaunchBinding(record, now);
        Check(first != 0, "restored registration has no attempt identity");
        Check(holder.Host.CancelLaunchBinding(Session, first) &&
              holder.Registry.PendingCount == 0, "own registration not retired");
        ulong second = holder.Host.RestoreLaunchBinding(record, now);
        Check(second != first, "successor registration reused attempt identity");
        Check(!holder.Host.CancelLaunchBinding(Session, first) &&
              holder.Registry.PendingCount == 1,
              "stale attempt removed the same-session successor registration");
        Check(holder.Registry.TryAdmit(Profile, Session, Token, now, new object(), out _) &&
              !holder.Host.CancelLaunchBinding(Session, first) && holder.Registry.ConnectedCount == 1,
              "stale attempt removed the successor's admitted route");
        Check(holder.Host.CancelLaunchBinding(Session, second) &&
              holder.Registry.PendingCount == 0 && holder.Registry.ConnectedCount == 0,
              "successor registration not retired by its own identity");
        passed.Add("registration-scoped-retirement-protects-same-session-successor");
    }

    private static OverviewLifecycleService Create(
        string profile, string gameRoot, string runtime, string backup,
        string appRoot, LWBridgeControlPipeHostState host, OverviewLifecycleTestHooks hooks) =>
        new(profile, gameRoot, helperPath: Path.Combine(appRoot, "nonexistent.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false,
            bridgeHostState: host, enableBridgeControlPipeLaunchBinding: true,
            runtimeRoot: runtime, backupRoot: backup,
            evidenceRoot: Path.Combine(appRoot, "evidence"), applicationDataRoot: appRoot);

    private static void WriteRepair(
        string runtime, string backup, OverviewAdoptionSnapshot record)
    {
        File.WriteAllBytes(Path.Combine(runtime, "recovery.json"),
            JsonSerializer.SerializeToUtf8Bytes(new {
                schemaVersion = 1, profileId = record.ProfileId,
                requestId = record.InstanceId, sessionId = record.InstanceId,
                stage = "active_ready_deferred_restore",
                gamePid = GamePid, gamePath = record.GameExecutable,
                gameStartedAtUtc = record.ProcessCreatedAt,
                backupPath = Path.Combine(backup, "owned"), originalFiles = new { }
            }));
    }

    private static void WriteHeartbeat(string runtime, OverviewAdoptionSnapshot record)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        File.WriteAllBytes(Path.Combine(runtime, "heartbeat.json"),
            JsonSerializer.SerializeToUtf8Bytes(new {
                schemaVersion = 1,
                bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId = record.ProfileId, sessionId = record.InstanceId,
                challenge = record.Challenge, gamePid = GamePid,
                updatedAt = now, ready = true, messageVisible = true,
                messageText = OverviewLifecycleService.ReadyMessage
            }));
    }

    private static void CheckThrows<T>(Action action, string label) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("HOME009 R3 adoption: " + label);
    }

    private sealed class RegistryHolder : IDisposable
    {
        internal LWBridgeControlPipeRegistry Registry { get; } = new();
        internal LWBridgeControlPipeHostState Host { get; }
        internal RegistryHolder()
        {
            Host = new LWBridgeControlPipeHostState(
                @"\\.\pipe\lwbridge-r3-sole-inert-adoption", Registry);
        }
        public void Dispose() => Host.Dispose();
    }
}
