using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HomeR1AdoptionChecks
{
    internal static async Task RunAsync()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "home004-r1-adoption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            await RunCaseAsync(sandbox, "failed-helper", "GAME_CLOSE_TIMEOUT", "GAME_CLOSE_TIMEOUT", expectedStopCalls: 1);
            await RunCaseAsync(sandbox, "cancelled-helper", "GAME_OPERATION_CANCELLED",
                "The repair/relaunch operation was cancelled.", expectedStopCalls: 1);
            await RunCaseAsync(sandbox, "success", null, null, expectedStopCalls: 1);
            await RunCaseAsync(sandbox, "wrong-profile", "RECOVERY_PROCESS_MISMATCH",
                "RECOVERY_PROCESS_MISMATCH", expectedStopCalls: 0);
            await RunCaseAsync(sandbox, "obsolete-process", "RECOVERY_PROCESS_MISMATCH",
                "RECOVERY_PROCESS_MISMATCH", expectedStopCalls: 0);
            Console.WriteLine("HOME004_R1_ACTUAL_REPAIR_PRODUCER_ADOPTION_OK failed/cancel/success/wrong-owner/obsolete-process; game launches=0");
        }
        finally
        {
            // No installation or game data exists in this disposable test root.
            if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
        }
    }

    private static async Task RunCaseAsync(
        string sandbox, string scenario, string? expectedError, string? expectedMessage, int expectedStopCalls)
    {
        string root = Path.Combine(sandbox, scenario);
        string install = Path.Combine(root, "inert-install");
        Directory.CreateDirectory(Path.Combine(install, "Game"));
        string exe = Path.Combine(install, "Game", "LastWar.exe");
        File.WriteAllBytes(exe, []);
        string profile = "home-r1-" + scenario;
        string session = "session-" + scenario;
        string challenge = new string('a', 64);
        string date = DateTimeOffset.UtcNow.AddMinutes(-2).UtcDateTime.ToString("O");
        int pid = 22000;
        string runtime = Path.Combine(root, "runtime");
        string backups = Path.Combine(root, "backups");
        string backup = Path.Combine(backups, "exact-session");
        Directory.CreateDirectory(backup);
        var identity = new OverviewAdoptionSnapshot(
            scenario == "wrong-profile" ? "another-profile" : profile,
            session, challenge, scenario == "obsolete-process" ? pid + 8 : pid,
            exe, date, "obsolete-build", new string('A', 43),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LeaseRequired: true);
        byte[] adopted = OverviewAdoptionRecord.Serialize(identity);
        byte[] journal = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, profileId = profile, requestId = session, sessionId = session,
            stage = "active_ready_deferred_restore", gamePid = pid, gamePath = exe,
            gameStartedAtUtc = date, backupPath = backup, originalFiles = new { startup = "inert" }
        });
        int stopCalls = 0;
        int starts = 0;
        byte[]? report = null;
        byte[]? heartbeat = null;
        var hooks = new OverviewLifecycleTestHooks
        {
            GameRootAvailable = () => true,
            ProcessMatches = (candidate, path, created) =>
                (candidate == pid || candidate == pid + 1) &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(created, date, StringComparison.Ordinal),
            ReadAllBytes = path => path.EndsWith("adoption.json", StringComparison.OrdinalIgnoreCase) ? adopted
                : path.EndsWith("recovery.json", StringComparison.OrdinalIgnoreCase) ? journal
                : path.EndsWith("game-reported.txt", StringComparison.OrdinalIgnoreCase) && report is not null ? report
                : path.EndsWith("heartbeat.json", StringComparison.OrdinalIgnoreCase) && heartbeat is not null ? heartbeat
                : throw new FileNotFoundException(path),
            SelectedGamePids = _ => Array.Empty<int>(),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = (invocation, _) =>
            {
                if (invocation.Operation == "stop")
                {
                    stopCalls++;
                    if (scenario == "failed-helper")
                        throw new BridgeCommandException("GAME_CLOSE_TIMEOUT", "GAME_CLOSE_TIMEOUT");
                    if (scenario == "cancelled-helper")
                        throw new OperationCanceledException("inert cancelled helper");
                    return Task.FromResult(JsonSerializer.SerializeToElement(new
                    {
                        mode = "overview_exact_pid_close_restore", bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId = profile, sessionId = session, gamePid = pid, gamePath = exe,
                        gameStartedAtUtc = date, gameRunning = false, installedFilesChanged = false,
                        close = new { accepted = true, processExited = true, alreadyExited = false },
                        restore = new { restored = true }
                    }));
                }
                starts++;
                report = System.Text.Encoding.UTF8.GetBytes(
                    "schema=1\nsessionId=" + invocation.SessionId + "\nchallenge=" + invocation.Challenge +
                    "\ndeadlineMilliseconds=" + (Environment.TickCount64 + 90_000) +
                    "\ngamePid=" + (pid + 1) + "\n");
                heartbeat = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId = profile, sessionId = invocation.SessionId, challenge = invocation.Challenge,
                    gamePid = pid + 1, ready = true, messageVisible = true,
                    messageText = OverviewLifecycleService.ReadyMessage,
                    updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    mode = "overview_install_launch_ready_deferred_restore",
                    bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId = profile, sessionId = invocation.SessionId,
                    challengeSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant(),
                    gamePid = pid + 1, launcherPid = pid + 2, gamePath = exe,
                    gameStartedAtUtc = date, gameRunning = true, installedFilesChanged = true,
                    restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
                    ready = new {
                        schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                        profileId = profile, sessionId = invocation.SessionId, challenge = invocation.Challenge,
                        gamePid = pid + 1, ready = true, messageVisible = true,
                        messageText = OverviewLifecycleService.ReadyMessage,
                        readyAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                }));
            }
        };
        using var bridgeState = new LWBridgeControlPipeHostState(
            pipePath: @"\\.\pipe\lwbridge-home-r1-" + Guid.NewGuid().ToString("N"));
        using var lifecycle = new OverviewLifecycleService(
            profile, install, helperPath: Path.Combine(root, "inert-helper.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false,
            bridgeHostState: bridgeState, enableBridgeControlPipeLaunchBinding: true,
            runtimeRoot: runtime, backupRoot: backups,
            evidenceRoot: Path.Combine(root, "evidence"), applicationDataRoot: root);
        object? response = await lifecycle.InvokeAsync("profile_instances_reconcile",
            JsonSerializer.SerializeToElement(new { autoLaunchAll = false }), CancellationToken.None);
        // This is the actual service -> adoption -> UpdateAndRestart -> helper
        // producer. Only the outer native wire response uses JsonOptions.Web.
        string internalJson = JsonSerializer.Serialize(response);
        JsonElement native = JsonSerializer.SerializeToElement(response, JsonOptions.Default);
        JsonElement errors = native.GetProperty("errors");
        if (expectedError is null)
            Check(errors.GetArrayLength() == 0 && stopCalls == expectedStopCalls && starts == 1,
                scenario + " should have restarted through actual helper producer: " +
                internalJson + " stopCalls=" + stopCalls + " starts=" + starts);
        else
            Check(errors.GetArrayLength() == 1 &&
                  errors[0].GetProperty("error").GetString() == expectedError &&
                  errors[0].GetProperty("message").GetString() == expectedMessage &&
                  errors[0].GetProperty("profileId").GetString() == profile &&
                  stopCalls == expectedStopCalls && starts == 0,
                scenario + " actual repair error/identity mismatch: " + internalJson);
        Check(JsonSerializer.SerializeToElement(response).GetProperty("errors").ValueKind == JsonValueKind.Array,
            "default internal serialization should remain distinct from external wire serializer");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("HOME004_R1_REPAIR: " + message);
    }
}
