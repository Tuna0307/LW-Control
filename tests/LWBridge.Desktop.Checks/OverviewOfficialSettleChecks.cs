using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewOfficialSettleChecks
{
    internal static async Task RunAsync()
    {
        await OfficialSettleRunsBeforeHelper();
        await OfficialRecoveryFailureBlocksSettleAndHelper();
        await OfficialSettleFailureBlocksHelper();
        await OfficialLuaUpdateFailureForcesSettleAndRetriesOnce();
        await LauncherGameSpawnTimeoutRetriesOnceWithoutRepeatingSettle();
        await GenericHelperFailureAllowsExplicitSubsequentRetry();
        await PostHelperReadinessFailureCanCloseThenRetry();
    }

    private static async Task OfficialSettleRunsBeforeHelper()
    {
        var events = new List<string>();
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-settle-order");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int gamePid = 32123;
        const int launcherPid = 32124;
        const string startedAt = "2026-09-14T14:00:00.0000000Z";
        string? session = null;
        string? challenge = null;
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (selectedRoot, _) =>
            {
                Check(Path.GetFullPath(selectedRoot) == Path.GetFullPath(root), "official recovery receives the selected root");
                events.Add("recover");
                return Task.CompletedTask;
            },
            RunOfficialSettleAsync = (selectedRoot, _) =>
            {
                Check(events.SequenceEqual(["recover"]), "official settle must not run before pending recovery");
                Check(Path.GetFullPath(selectedRoot) == Path.GetFullPath(root), "official settle receives the selected root");
                events.Add("settle");
                return Task.CompletedTask;
            },
            RunHelperAsync = (invocation, _) =>
            {
                Check(events.SequenceEqual(["recover", "settle"]), "helper must not run before recovery and official settle");
                events.Add("helper");
                session = invocation.SessionId;
                challenge = invocation.Challenge;
                return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt));
            },
            ProcessMatches = (pid, path, created) => pid == gamePid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => Heartbeat(session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-settle-order",
            root,
            helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);
        await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Check(events.SequenceEqual(["recover", "settle", "helper"]), "pending recovery and official settle precede candidate/helper start");
    }

    private static async Task OfficialRecoveryFailureBlocksSettleAndHelper()
    {
        int settleCalls = 0;
        int helperCalls = 0;
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-recover-failure");
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => throw new BridgeCommandException(
                "OFFICIAL_RECOVERY_TEST_FAILURE",
                "synthetic official recovery failure"),
            RunOfficialSettleAsync = (_, _) =>
            {
                settleCalls++;
                return Task.CompletedTask;
            },
            RunHelperAsync = (_, _) =>
            {
                helperCalls++;
                throw new InvalidOperationException("helper must not run after recovery failure");
            },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-recover-failure",
            root,
            helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);
        try
        {
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            throw new InvalidOperationException("expected official recovery failure");
        }
        catch (BridgeCommandException error) when (error.Code == "OFFICIAL_RECOVERY_TEST_FAILURE")
        {
        }
        Check(settleCalls == 0 && helperCalls == 0, "official recovery failure prevents settle and candidate/helper start");
    }

    private static async Task OfficialSettleFailureBlocksHelper()
    {
        int helperCalls = 0;
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-settle-failure");
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialSettleAsync = (_, _) => throw new BridgeCommandException(
                "OFFICIAL_SETTLE_TEST_FAILURE",
                "synthetic official settle failure"),
            RunHelperAsync = (_, _) =>
            {
                helperCalls++;
                throw new InvalidOperationException("helper must not run after settle failure");
            },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-settle-failure",
            root,
            helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);
        try
        {
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            throw new InvalidOperationException("expected official settle failure");
        }
        catch (BridgeCommandException error) when (error.Code == "OFFICIAL_SETTLE_TEST_FAILURE")
        {
        }
        Check(helperCalls == 0, "official settle failure prevents candidate/helper start");
    }

    private static async Task OfficialLuaUpdateFailureForcesSettleAndRetriesOnce()
    {
        int recoverCalls = 0;
        int settleCalls = 0;
        int helperCalls = 0;
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-lua-update-retry");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int gamePid = 33123;
        const int launcherPid = 33124;
        const string startedAt = "2026-09-17T11:21:18.0000000Z";
        string? session = null;
        string? challenge = null;
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => { recoverCalls++; return Task.CompletedTask; },
            RunOfficialSettleAsync = (_, _) => { settleCalls++; return Task.CompletedTask; },
            RunHelperAsync = (invocation, _) =>
            {
                helperCalls++;
                if (helperCalls == 1)
                    throw new InvalidOperationException("official_lua_update_failed: synthetic CRC mismatch");
                session = invocation.SessionId;
                challenge = invocation.Challenge;
                return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt));
            },
            ProcessMatches = (pid, path, created) => pid == gamePid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => Heartbeat(session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };
        using var lifecycle = new OverviewLifecycleService(
            "profile-settle-order", root, helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false);
        await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Check(recoverCalls == 2 && settleCalls == 2 && helperCalls == 2,
            "Lua update CRC failure should force exactly one fresh official settle and one helper retry");
    }

    private static async Task LauncherGameSpawnTimeoutRetriesOnceWithoutRepeatingSettle()
    {
        int recoverCalls = 0;
        int settleCalls = 0;
        int helperCalls = 0;
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-launcher-spawn-retry");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int gamePid = 34123;
        const int launcherPid = 34124;
        const string startedAt = "2026-09-19T10:41:09.0000000Z";
        string? session = null;
        string? challenge = null;
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => { recoverCalls++; return Task.CompletedTask; },
            RunOfficialSettleAsync = (_, _) => { settleCalls++; return Task.CompletedTask; },
            RunHelperAsync = (invocation, _) =>
            {
                helperCalls++;
                if (helperCalls == 1)
                {
                    throw new InvalidOperationException(
                        "the selected launcher did not create a matching LastWar process before timeout");
                }
                session = invocation.SessionId;
                challenge = invocation.Challenge;
                return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt));
            },
            ProcessMatches = (pid, path, created) => pid == gamePid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => Heartbeat(session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-settle-order", root, helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false);
        await lifecycle.InvokeAsync(
                "profile_instance_start",
                JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Check(recoverCalls == 1 && settleCalls == 1 && helperCalls == 2,
            "launcher spawn timeout should retry exactly once without repeating official settle");
    }

    private static async Task GenericHelperFailureAllowsExplicitSubsequentRetry()
    {
        int startCalls = 0;
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-generic-helper-retry");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int gamePid = 35123;
        const int launcherPid = 35124;
        const string startedAt = "2026-09-20T07:20:00.0000000Z";
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = (invocation, _) =>
            {
                if (invocation.Operation == "start")
                {
                    startCalls++;
                    if (startCalls == 1)
                        throw new InvalidOperationException("A10 synthetic generic helper failure");
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    processAlive = true;
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt));
                }
                processAlive = false;
                return Task.FromResult(StopResult(invocation, gamePath, gamePid, startedAt));
            },
            ProcessMatches = (pid, path, created) => processAlive && pid == gamePid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => Heartbeat(session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };
        using var lifecycle = new OverviewLifecycleService(
            "profile-settle-order", root, helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false);

        try
        {
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            throw new InvalidOperationException("expected generic helper failure");
        }
        catch (BridgeCommandException error) when (error.Code == "LAUNCH_FAILED")
        {
        }
        JsonElement failed = JsonSerializer.SerializeToElement(lifecycle.CreateInstanceStatus(), JsonOptions.Default);
        Check(failed.GetProperty("phase").GetString() == "error" && failed.GetProperty("pid").ValueKind == JsonValueKind.Null,
            "generic helper failure must not fabricate running ownership");

        JsonElement retry = JsonSerializer.SerializeToElement(
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None),
            JsonOptions.Default);
        string retrySession = retry.GetProperty("instanceId").GetString()!;
        Check(startCalls == 2 && retry.GetProperty("phase").GetString() == "running" &&
              retry.GetProperty("connectionState").GetString() == "connected",
            "generic helper failure should allow an explicit subsequent start retry");
        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = retrySession }), CancellationToken.None);
        JsonElement stopped = JsonSerializer.SerializeToElement(lifecycle.CreateInstanceStatus(), JsonOptions.Default);
        Check(stopped.GetProperty("phase").GetString() == "stopped" && !processAlive,
            "generic helper retry must close and return to stopped");
    }

    private static async Task PostHelperReadinessFailureCanCloseThenRetry()
    {
        int startCalls = 0;
        bool processAlive = false;
        bool heartbeatFresh = false;
        string? session = null;
        string? challenge = null;
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-post-helper-readiness-retry");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int gamePid = 36123;
        const int launcherPid = 36124;
        const string startedAt = "2026-09-20T07:21:00.0000000Z";
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = (invocation, _) =>
            {
                if (invocation.Operation == "start")
                {
                    startCalls++;
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    processAlive = true;
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt));
                }
                processAlive = false;
                return Task.FromResult(StopResult(invocation, gamePath, gamePid, startedAt));
            },
            ProcessMatches = (pid, path, created) => processAlive && pid == gamePid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => heartbeatFresh
                ? Heartbeat(session!, challenge!, gamePid)
                : JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = 1,
                    bridgeVersion = OverviewLifecycleService.BridgeVersion,
                    profileId = "profile-settle-order",
                    sessionId = session,
                    challenge,
                    gamePid,
                    updatedAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
                    ready = true,
                    messageVisible = true,
                    messageText = OverviewLifecycleService.ReadyMessage,
                }),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };
        using var lifecycle = new OverviewLifecycleService(
            "profile-settle-order", root, helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false, testHooks: hooks, startRecoveryMonitor: false);

        try
        {
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            throw new InvalidOperationException("expected post-helper readiness failure");
        }
        catch (BridgeCommandException error) when (error.Code == "BRIDGE_START_TIMEOUT")
        {
        }
        JsonElement retained = JsonSerializer.SerializeToElement(lifecycle.CreateInstanceStatus(), JsonOptions.Default);
        string retainedSession = retained.GetProperty("instanceId").GetString()!;
        Check(retained.GetProperty("pid").GetInt32() == gamePid && processAlive,
            "post-helper readiness failure must retain exact owned process identity for safe cleanup");
        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = retainedSession }), CancellationToken.None);
        Check(!processAlive, "post-helper readiness failure cleanup must close the exact owned process");

        heartbeatFresh = true;
        JsonElement retry = JsonSerializer.SerializeToElement(
            await lifecycle.InvokeAsync("profile_instance_start", JsonSerializer.SerializeToElement(new { }), CancellationToken.None),
            JsonOptions.Default);
        string retrySession = retry.GetProperty("instanceId").GetString()!;
        Check(startCalls == 2 && retry.GetProperty("phase").GetString() == "running" &&
              retry.GetProperty("connectionState").GetString() == "connected",
            "post-helper readiness cleanup must permit a successful subsequent retry");
        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = retrySession }), CancellationToken.None);
        Check(!processAlive, "post-helper readiness retry must close cleanly");
    }

    private static JsonElement StopResult(
        OverviewHelperInvocation invocation,
        string gamePath,
        int gamePid,
        string startedAt) => JsonSerializer.SerializeToElement(new
        {
            ok = true,
            mode = "overview_exact_pid_close_restore",
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = invocation.ProfileId,
            sessionId = invocation.SessionId,
            gamePid,
            gamePath,
            gameStartedAtUtc = startedAt,
            close = new { method = "synthetic", accepted = true, processExited = true, alreadyExited = false },
            restore = new { restored = true },
            gameRunning = false,
            installedFilesChanged = false,
        });

    private static JsonElement StartResult(
        OverviewHelperInvocation invocation,
        string gamePath,
        int gamePid,
        int launcherPid,
        string startedAt)
    {
        string challengeHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            mode = "overview_install_launch_ready_deferred_restore",
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = invocation.ProfileId,
            sessionId = invocation.SessionId,
            challengeSha256 = challengeHash,
            gamePid,
            launcherPid,
            gamePath,
            gameStartedAtUtc = startedAt,
            gameRunning = true,
            installedFilesChanged = true,
            restore = new
            {
                restored = false,
                deferred = true,
                stage = "active_ready_deferred_restore",
            },
            ready = new
            {
                schemaVersion = 1,
                bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId = invocation.ProfileId,
                sessionId = invocation.SessionId,
                challenge = invocation.Challenge,
                gamePid,
                ready = true,
                messageVisible = true,
                messageText = OverviewLifecycleService.ReadyMessage,
                readyAt = now,
                updatedAt = now,
            },
        });
    }

    private static byte[] Heartbeat(string session, string challenge, int gamePid)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = "profile-settle-order",
            sessionId = session,
            challenge,
            gamePid,
            updatedAt = now,
            ready = true,
            messageVisible = true,
            messageText = OverviewLifecycleService.ReadyMessage,
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview official-settle check failed: " + message);
    }
}
