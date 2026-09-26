using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewFaultAdmissionChecks
{
    private const string PackageSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static async Task<JsonElement> RunAsync(bool includeTimeoutRegression)
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-a08-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            JsonElement installation = RunInstallationMatrix(root);
            JsonElement admission = await RunLifecycleAdmissionMatrixAsync(root);
            JsonElement handshake = await RunHandshakeAndCompatibilityRetryAsync(root);
            if (includeTimeoutRegression)
                await OverviewOfficialSettleChecks.RunAsync().ConfigureAwait(false);

            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                acceptanceCase = "A08",
                scope = "path/permission/ABI + lifecycle admission + handshake/current-client compatibility; existing bounded timeout/retry regression optionally executed",
                installation,
                admission,
                handshake,
                boundedTimeoutAndRetryRegression = includeTimeoutRegression ? "PASS" : "covered separately by OverviewOfficialSettleChecks in default suite",
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static JsonElement RunInstallationMatrix(string root)
    {
        string systemCmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        string systemKernel = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        string validRoot = CreateValidRoot(Path.Combine(root, "valid"), systemCmd, systemKernel);
        var service = new GameInstallationService(new LocalConfigStore(persistent: false));

        GameRootStatus blank = service.Validate("   ", "a08");
        Check(!blank.Valid && blank.Error == "GAME_ROOT_INVALID", "blank root must fail with GAME_ROOT_INVALID");

        string missingRoot = Path.Combine(root, "missing-required");
        Directory.CreateDirectory(missingRoot);
        GameRootStatus missing = service.Validate(missingRoot, "a08");
        Check(!missing.Valid && missing.Error == "GAME_ROOT_REQUIRED_FILES_MISSING",
            "missing required files must fail closed");

        var deniedService = new GameInstallationService(new LocalConfigStore(persistent: false),
            new GameInstallationTestHooks { OpenRead = _ => throw new UnauthorizedAccessException("synthetic denied") });
        GameRootStatus denied = deniedService.Validate(validRoot, "a08");
        Check(!denied.Valid && denied.Error == "GAME_ROOT_PERMISSION_DENIED",
            "permission failure must return concrete GAME_ROOT_PERMISSION_DENIED");

        var unreadableService = new GameInstallationService(new LocalConfigStore(persistent: false),
            new GameInstallationTestHooks { OpenRead = _ => throw new IOException("synthetic unreadable") });
        GameRootStatus unreadable = unreadableService.Validate(validRoot, "a08");
        Check(!unreadable.Valid && unreadable.Error == "GAME_ROOT_UNREADABLE",
            "I/O failure must return concrete GAME_ROOT_UNREADABLE");

        string badPeRoot = CreateValidRoot(Path.Combine(root, "bad-pe"), systemCmd, systemKernel);
        File.WriteAllText(Path.Combine(badPeRoot, "Game", "LastWar.exe"), "not-a-pe");
        GameRootStatus badPe = service.Validate(badPeRoot, "a08");
        Check(!badPe.Valid && badPe.Error == "GAME_ROOT_PE_INVALID",
            "malformed game image must return concrete GAME_ROOT_PE_INVALID");

        string sysWowCmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "cmd.exe");
        Check(File.Exists(sysWowCmd), "32-bit Windows fixture must exist for ABI rejection proof");
        string unsupportedRoot = CreateValidRoot(Path.Combine(root, "unsupported-abi"), systemCmd, systemKernel);
        File.Copy(sysWowCmd, Path.Combine(unsupportedRoot, "Game", "LastWar.exe"), overwrite: true);
        GameRootStatus unsupported = service.Validate(unsupportedRoot, "a08");
        Check(!unsupported.Valid && unsupported.Error == "GAME_ROOT_ARCH_UNSUPPORTED" && unsupported.Is64Bit == false,
            "non-AMD64 game image must fail with GAME_ROOT_ARCH_UNSUPPORTED");

        GameRootStatus valid = service.Validate(validRoot, "a08");
        Check(valid.Valid && valid.Is64Bit == true, "valid AMD64 root must still pass after fault injection");

        return JsonSerializer.SerializeToElement(new
        {
            blank = blank.Error,
            missingRequiredFiles = missing.Error,
            permissionDenied = denied.Error,
            unreadable = unreadable.Error,
            invalidPe = badPe.Error,
            unsupportedAbi = unsupported.Error,
            validRetry = valid.Valid,
        });
    }

    private static async Task<JsonElement> RunLifecycleAdmissionMatrixAsync(string root)
    {
        JsonElement empty = JsonSerializer.SerializeToElement(new { });

        int missingRootHelperCalls = 0;
        using (var noRoot = new OverviewLifecycleService(
            "profile-a08-missing-root", null,
            helperPath: Path.Combine(root, "missing-root-fake.py"),
            requireCurrentClientEvidence: false,
            testHooks: new OverviewLifecycleTestHooks
            {
                RunHelperAsync = (_, _) => { missingRootHelperCalls++; throw new InvalidOperationException("must not run"); },
                RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
            },
            startRecoveryMonitor: false))
        {
            string code = await CaptureStartErrorAsync(noRoot, empty);
            JsonElement status = Status(noRoot.CreateInstanceStatus());
            Check(code == "GAME_ROOT_NOT_FOUND" && missingRootHelperCalls == 0,
                "missing root must reject before helper execution");
            Check(status.GetProperty("phase").GetString() == "stopped" &&
                  status.GetProperty("connectionState").GetString() == "offline" &&
                  status.GetProperty("pid").ValueKind == JsonValueKind.Null,
                "missing root must not fabricate a connected/owned state");
        }

        string helperMissingRoot = Path.Combine(root, "helper-missing-root");
        Directory.CreateDirectory(helperMissingRoot);
        using (var noHelper = new OverviewLifecycleService(
            "profile-a08-missing-helper", helperMissingRoot,
            helperPath: Path.Combine(root, "definitely-missing-overview-helper.py"),
            requireCurrentClientEvidence: false,
            startRecoveryMonitor: false))
        {
            string code = await CaptureStartErrorAsync(noHelper, empty);
            JsonElement status = Status(noHelper.CreateInstanceStatus());
            Check(code == "OVERVIEW_HELPER_MISSING", "missing helper must return OVERVIEW_HELPER_MISSING");
            Check(status.GetProperty("phase").GetString() == "stopped" &&
                  status.GetProperty("connectionState").GetString() == "offline" &&
                  status.GetProperty("pid").ValueKind == JsonValueKind.Null,
                "missing helper must not fabricate a connected/owned state");
        }

        int closedHelperCalls = 0;
        var closedLifecycle = new OverviewLifecycleService(
            "profile-a08-closed", Path.Combine(root, "closed-root"),
            helperPath: Path.Combine(root, "closed-fake.py"),
            requireCurrentClientEvidence: false,
            testHooks: new OverviewLifecycleTestHooks
            {
                RunHelperAsync = (_, _) => { closedHelperCalls++; throw new InvalidOperationException("must not run"); },
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
            },
            startRecoveryMonitor: false);
        closedLifecycle.Close();
        string closedCode = await CaptureStartErrorAsync(closedLifecycle, empty);
        Check(closedCode == "GAME_OPERATION_CANCELLED" && closedHelperCalls == 0,
            "closed lifecycle must reject before helper execution");
        closedLifecycle.Dispose();

        return JsonSerializer.SerializeToElement(new
        {
            missingRoot = "GAME_ROOT_NOT_FOUND",
            missingRootHelperCalls,
            missingHelper = "OVERVIEW_HELPER_MISSING",
            closedLifecycle = closedCode,
            closedHelperCalls,
            allRejectedBeforeConnected = true,
        });
    }

    private static async Task<JsonElement> RunHandshakeAndCompatibilityRetryAsync(string root)
    {
        string selectedRoot = Path.Combine(root, "handshake-root");
        Directory.CreateDirectory(Path.Combine(selectedRoot, "Game"));
        string gamePath = Path.Combine(selectedRoot, "Game", "LastWar.exe");
        const int gamePid = 48123;
        const int launcherPid = 48124;
        const string startedAt = "2026-09-20T10:00:00.0000000Z";
        int startCalls = 0;
        int stopCalls = 0;
        string? session = null;
        string? challenge = null;
        bool heartbeatFresh = true;
        bool processAlive = false;

        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            ProcessMatches = (pid, path, created) => processAlive && pid == gamePid &&
                created == startedAt && string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            RunHelperAsync = (invocation, _) =>
            {
                if (invocation.Operation == "stop")
                {
                    stopCalls++;
                    processAlive = false;
                    return Task.FromResult(StopResult(invocation, gamePath, gamePid, startedAt));
                }

                startCalls++;
                session = invocation.SessionId;
                challenge = invocation.Challenge;
                if (startCalls == 1)
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt,
                        corruptChallengeHash: true, corruptPath: false, corruptCompatibility: false));
                if (startCalls == 2)
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt,
                        corruptChallengeHash: false, corruptPath: true, corruptCompatibility: false));
                if (startCalls == 3)
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt,
                        corruptChallengeHash: false, corruptPath: false, corruptCompatibility: true));

                processAlive = true;
                heartbeatFresh = true;
                return Task.FromResult(StartResult(invocation, gamePath, gamePid, launcherPid, startedAt,
                    corruptChallengeHash: false, corruptPath: false, corruptCompatibility: false));
            },
            ReadAllBytes = _ => Heartbeat(session!, challenge!, gamePid, heartbeatFresh),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-a08-handshake", selectedRoot,
            helperPath: Path.Combine(root, "handshake-fake.py"),
            requireCurrentClientEvidence: true,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement empty = JsonSerializer.SerializeToElement(new { });

        var errors = new List<string>();
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            string code = await CaptureStartErrorAsync(lifecycle, empty);
            errors.Add(code);
            Check(code == "LAUNCH_FAILED", $"corrupt helper result #{attempt} must fail as LAUNCH_FAILED");
            JsonElement failedStatus = Status(lifecycle.CreateInstanceStatus());
            Check(failedStatus.GetProperty("phase").GetString() == "error" &&
                  failedStatus.GetProperty("connectionState").GetString() == "error" &&
                  failedStatus.GetProperty("pid").ValueKind == JsonValueKind.Null,
                $"corrupt helper result #{attempt} must never fabricate connected ownership");
        }

        JsonElement success = Status(await lifecycle.InvokeAsync("profile_instance_start", empty, CancellationToken.None));
        Check(startCalls == 4 && success.GetProperty("phase").GetString() == "running" &&
              success.GetProperty("connectionState").GetString() == "connected" &&
              success.GetProperty("pid").GetInt32() == gamePid,
            "same lifecycle must succeed after handshake/path/compatibility faults are corrected");

        heartbeatFresh = false;
        JsonElement disconnected = Status(lifecycle.CreateInstanceStatus());
        Check(disconnected.GetProperty("phase").GetString() == "error" &&
              disconnected.GetProperty("connectionState").GetString() == "error" &&
              disconnected.GetProperty("error").GetString() == "BRIDGE_DISCONNECTED" &&
              disconnected.GetProperty("pid").GetInt32() == gamePid,
            "stale heartbeat must retain exact cleanup identity but never expose connected state");

        string ownedSession = success.GetProperty("instanceId").GetString()!;
        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = ownedSession }), CancellationToken.None);
        Check(stopCalls == 1 && !processAlive, "owned stale-heartbeat session must cleanly stop exactly once");

        heartbeatFresh = true;
        JsonElement retry = Status(await lifecycle.InvokeAsync("profile_instance_start", empty, CancellationToken.None));
        string retrySession = retry.GetProperty("instanceId").GetString()!;
        Check(startCalls == 5 && retry.GetProperty("phase").GetString() == "running" &&
              retry.GetProperty("connectionState").GetString() == "connected",
            "post-cleanup retry must succeed normally");
        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = retrySession }), CancellationToken.None);
        Check(stopCalls == 2 && !processAlive, "final retry must stop cleanly");

        return JsonSerializer.SerializeToElement(new
        {
            corruptAttempts = new[]
            {
                new { fault = "challenge-hash", error = errors[0] },
                new { fault = "game-path", error = errors[1] },
                new { fault = "critical-current-client-anchor", error = errors[2] },
            },
            successfulRetryAttempt = 4,
            staleHeartbeatExternalStatus = "BRIDGE_DISCONNECTED",
            exactCleanupStopCalls = stopCalls,
            finalRetryConnected = true,
            finalStopped = !processAlive,
        });
    }

    private static string CreateValidRoot(string root, string systemCmd, string systemKernel)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game", "LastWar_Data", "Plugins", "x86_64"));
        File.Copy(systemCmd, Path.Combine(root, "LastWarLauncher.exe"), overwrite: true);
        File.Copy(systemCmd, Path.Combine(root, "Game", "LastWar.exe"), overwrite: true);
        File.Copy(systemKernel, Path.Combine(root, "Game", "LastWar_Data", "Plugins", "x86_64", "xlua.dll"), overwrite: true);
        return root;
    }

    private static async Task<string> CaptureStartErrorAsync(OverviewLifecycleService lifecycle, JsonElement payload)
    {
        try
        {
            await lifecycle.InvokeAsync("profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
            return "UNEXPECTED_SUCCESS";
        }
        catch (BridgeCommandException error)
        {
            return error.Code;
        }
    }

    private static JsonElement Status(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static JsonElement StartResult(
        OverviewHelperInvocation invocation,
        string gamePath,
        int gamePid,
        int launcherPid,
        string startedAt,
        bool corruptChallengeHash,
        bool corruptPath,
        bool corruptCompatibility)
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
            challengeSha256 = corruptChallengeHash ? new string('0', 64) : challengeHash,
            gamePid,
            launcherPid,
            gamePath = corruptPath ? Path.Combine(Path.GetDirectoryName(gamePath)!, "Foreign.exe") : gamePath,
            gameStartedAtUtc = startedAt,
            gameRunning = true,
            installedFilesChanged = true,
            restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
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
            currentClient = CurrentClient(corruptCompatibility),
        });
    }

    private static JsonElement StopResult(OverviewHelperInvocation invocation, string gamePath, int gamePid, string startedAt) =>
        JsonSerializer.SerializeToElement(new
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
            restore = new
            {
                restored = true,
                packageSha256 = PackageSha,
                originalFiles = new { data = new { sha256 = PackageSha } },
            },
            gameRunning = false,
            installedFilesChanged = false,
        });

    private static object CurrentClient(bool corrupt) => new
    {
        compatibilityPolicy = CurrentClientCompatibility.Policy,
        packageSha256 = PackageSha,
        packageSize = 41296373L,
        packageCrc32 = 1913170558u,
        fileVersion = 3,
        contentVersion = 19,
        entryCount = 18734,
        gameSha256 = CurrentClientCompatibility.ExpectedGameSha256,
        xluaSha256 = CurrentClientCompatibility.ExpectedXluaSha256,
        assemblyCSharpSha256 = CurrentClientCompatibility.ExpectedAssemblyCSharpSha256,
        luaEntrySha256 = corrupt ? new string('b', 64) : CurrentClientCompatibility.ExpectedLuaEntrySha256,
        criticalEntries = new Dictionary<string, string>
        {
            ["DataCenter/Global/LuaEntry.luac"] = corrupt ? new string('b', 64) : CurrentClientCompatibility.ExpectedLuaEntrySha256,
            ["Global/ConstDefine.luac"] = "95e6c733b98dc641c330f36efdef044845033b37ea6703068f7c7ed5fb0048fd",
            ["Util/CSharpCallLuaInterface.luac"] = "af1559723afba0fa5773bb815c486f3233fecb3928768abd082a96b51960229e",
            ["UI/LWMainUI/Component/UIMainBottom/UIMainChangeScene.luac"] = "3843dc02869330f060a199b946ef3cd30aa335504914c2f4c60f8a824e871c5b",
        },
    };

    private static byte[] Heartbeat(string session, string challenge, int gamePid, bool fresh)
    {
        long updatedAt = (fresh ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = "profile-a08-handshake",
            sessionId = session,
            challenge,
            gamePid,
            updatedAt,
            ready = true,
            messageVisible = true,
            messageText = OverviewLifecycleService.ReadyMessage,
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Overview A08 fault admission check failed: " + message);
    }
}
