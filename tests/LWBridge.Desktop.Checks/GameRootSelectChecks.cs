using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class GameRootSelectChecks
{
    internal static async Task RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-game-root-select-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = new LocalConfigStore(
                Path.Combine(root, "config"));
            string previous = CreateNativeRoot(
                Path.Combine(root, "previous"));
            config.Update(c => c with { GameRoot = previous });

            var service = new GameInstallationService(
                config,
                new GameInstallationTestHooks
                {
                    GetEnvironmentVariable = _ => null,
                    NearbyRoot = Path.Combine(root, "missing-nearby"),
                    LocalAppData = Path.Combine(root, "missing-local"),
                    DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
                });

            NativeGameRootSelectionResult canceled =
                service.CreateNativeCanceledSelection();
            JsonElement canceledJson =
                JsonSerializer.SerializeToElement(
                    canceled,
                    JsonOptions.Default);
            Require(
                canceledJson.EnumerateObject()
                    .Select(property => property.Name)
                    .SequenceEqual(
                        new[] { "canceled", "path", "valid" }),
                "game_root_select result has exactly canceled/path/valid");
            Require(
                canceledJson.GetProperty("canceled").GetBoolean() &&
                canceledJson.GetProperty("path").ValueKind ==
                    JsonValueKind.Null &&
                !canceledJson.GetProperty("valid").GetBoolean(),
                "cancel returns true/null/false");
            string invalidPath =
                Path.Combine(root, "invalid-selected");
            Directory.CreateDirectory(invalidPath);
            NativeGameRootSelectionResult invalid =
                service.SaveNativeSelection(invalidPath);
            Require(
                !invalid.Canceled &&
                !invalid.Valid &&
                SamePath(invalid.Path!, invalidPath),
                "ordinary invalid selection returns false/path/false");
            Require(
                SamePath(config.Snapshot.GameRoot!, previous),
                "ordinary invalid selection does not replace saved root");

            string selected = CreateNativeRoot(
                Path.Combine(root, "selected"));
            NativeGameRootSelectionResult valid =
                service.SaveNativeSelection(selected);
            Require(
                !valid.Canceled &&
                valid.Valid &&
                SamePath(valid.Path!, selected),
                "native-minimal selected root returns valid result");
            Require(
                SamePath(config.Snapshot.GameRoot!, selected),
                "valid native selection persists the normalized root");

            GameRootStatus internalValidation =
                service.Validate(selected, "internal");
            Require(
                !internalValidation.Valid &&
                internalValidation.Error ==
                    "GAME_ROOT_REQUIRED_FILES_MISSING",
                "public selection predicate stays separate from launch admission");

            NativeGameRootSelectionResult fromGame =
                service.SaveNativeSelection(
                    Path.Combine(selected, "Game"));
            Require(
                fromGame.Valid &&
                SamePath(fromGame.Path!, selected),
                "selected Game directory normalizes to installation root");

            NativeGameRootSelectionResult fromExe =
                service.SaveNativeSelection(
                    Path.Combine(
                        selected,
                        "Game",
                        "LastWar.exe"));
            Require(
                fromExe.Valid &&
                SamePath(fromExe.Path!, selected),
                "selected LastWar.exe path normalizes to installation root");
            ExpectError(
                () => service.SaveNativeSelection("bad\0path"),
                "INVALID_GAME_ROOT",
                "select the folder containing Game\\LastWar.exe",
                "un-normalizable selected path");

            string unavailableRoot = CreateNativeRoot(
                Path.Combine(root, "unavailable"));
            var unavailableConfig = new LocalConfigStore(
                Path.Combine(root, "unavailable-config"));
            unavailableConfig.Update(
                c => c with { GameRoot = previous });
            var unavailable = new GameInstallationService(
                unavailableConfig,
                new GameInstallationTestHooks
                {
                    StateAvailable = false,
                    GetEnvironmentVariable = _ => null,
                });
            ExpectError(
                () => unavailable.SaveNativeSelection(
                    unavailableRoot),
                "STATE_UNAVAILABLE",
                "path state is unavailable",
                "unavailable path state");
            Require(
                SamePath(
                    unavailableConfig.Snapshot.GameRoot!,
                    previous),
                "state-unavailable selection does not persist");

            var backendConfig = new LocalConfigStore(
                Path.Combine(root, "backend-config"));
            backendConfig.Update(c => c with { GameRoot = previous });
            var backend = new LWBridgeBackend(backendConfig);

            NativeGameRootSelectionResult backendCanceled =
                backend.CreateGameRootSelectionCanceled();
            Require(
                backendCanceled.Canceled &&
                backendCanceled.Path is null &&
                !backendCanceled.Valid,
                "backend cancel helper preserves native envelope");

            NativeGameRootSelectionResult backendInvalid =
                backend.SaveNativeGameRootSelection(invalidPath);
            Require(
                !backendInvalid.Canceled &&
                !backendInvalid.Valid &&
                SamePath(backendInvalid.Path!, invalidPath) &&
                SamePath(
                    backendConfig.Snapshot.GameRoot!,
                    previous),
                "backend invalid selection is a normal no-save result");
            NativeGameRootSelectionResult backendValid =
                backend.SaveNativeGameRootSelection(selected);
            Require(
                backendValid.Valid &&
                SamePath(backendValid.Path!, selected) &&
                SamePath(
                    backendConfig.Snapshot.GameRoot!,
                    selected),
                "backend valid selection persists selected root");

            using JsonDocument emptyPayload = JsonDocument.Parse("{}");
            JsonElement weakLaunchStatus = JsonSerializer.SerializeToElement(
                await backend.InvokeAsync(
                    "local_game_launch_status",
                    emptyPayload.RootElement.Clone(),
                    CancellationToken.None),
                JsonOptions.Default);
            Require(
                !weakLaunchStatus.GetProperty("valid").GetBoolean() &&
                weakLaunchStatus.GetProperty("error").GetString() == "GAME_ROOT_REQUIRED_FILES_MISSING",
                "clone-internal launch admission rejects a public-valid native-minimal root without changing game_root_status");

            await VerifyLifecycleRebindAsync(root);
            await VerifyFreshHostSelectionOwnershipAsync(root);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static string CreateNativeRoot(string root)
    {
        Directory.CreateDirectory(
            Path.Combine(
                root,
                "Game",
                "LastWar_Data",
                "Plugins",
                "x86_64"));
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        File.WriteAllText(
            Path.Combine(root, "Game", "LastWar.exe"),
            "native-select-fixture");
        return Path.GetFullPath(root);
    }

    private static string CreateStrictRoot(string root)
    {
        string systemCmd = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "cmd.exe");
        string systemKernel = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "kernel32.dll");
        Directory.CreateDirectory(
            Path.Combine(root, "Game", "LastWar_Data", "Plugins", "x86_64"));
        File.Copy(systemCmd, Path.Combine(root, "LastWarLauncher.exe"), overwrite: true);
        File.Copy(systemCmd, Path.Combine(root, "Game", "LastWar.exe"), overwrite: true);
        File.Copy(
            systemKernel,
            Path.Combine(root, "Game", "LastWar_Data", "Plugins", "x86_64", "xlua.dll"),
            overwrite: true);
        return Path.GetFullPath(root);
    }

    private static async Task VerifyLifecycleRebindAsync(string root)
    {
        string rootA = CreateStrictRoot(Path.Combine(root, "strict-a"));
        string rootB = CreateStrictRoot(Path.Combine(root, "strict-b"));
        string weakRoot = CreateNativeRoot(Path.Combine(root, "native-valid-strict-invalid"));
        var config = new LocalConfigStore(Path.Combine(root, "lifecycle-config"));
        config.Update(current => current with { GameRoot = rootA });
        string profileId = config.Snapshot.ProfileId;
        const int gamePid = 48121;
        const int launcherPid = 48122;
        const string startedAt = "2026-10-05T18:30:00.0000000Z";
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        int startCalls = 0;
        int stopCalls = 0;

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
                    return Task.FromResult(StartResult(
                        invocation,
                        rootB,
                        gamePid,
                        launcherPid,
                        startedAt));
                }

                stopCalls++;
                processAlive = false;
                return Task.FromResult(StopResult(
                    invocation,
                    rootB,
                    gamePid,
                    startedAt));
            },
            ProcessMatches = (pid, path, created) =>
                processAlive &&
                pid == gamePid &&
                created == startedAt &&
                SamePath(path, Path.Combine(rootB, "Game", "LastWar.exe")),
            ReadAllBytes = path =>
            {
                if (path.EndsWith("recovery.json", StringComparison.OrdinalIgnoreCase))
                    throw new FileNotFoundException(path);
                return Heartbeat(profileId, session!, challenge!, gamePid);
            },
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            rootA,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            config: config,
            testHooks: hooks,
            startRecoveryMonitor: false,
            runtimeRoot: Path.Combine(root, "picker-overview-runtime"),
            evidenceRoot: Path.Combine(root, "picker-overview-evidence"),
            backupRoot: Path.Combine(root, "picker-overview-backups"));
        var backend = new LWBridgeBackend(
            config,
            lifecycle,
            overviewLifecycle: lifecycle,
            installationTestHooks: new GameInstallationTestHooks
            {
                DefaultRoot = rootA,
                GetEnvironmentVariable = _ => null,
                NearbyRoot = Path.Combine(root, "missing-nearby"),
                LocalAppData = Path.Combine(root, "missing-local"),
                DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
            });

        NativeGameRootSelectionResult weakSelected =
            backend.SaveNativeGameRootSelection(weakRoot);
        Require(
            weakSelected.Valid && SamePath(config.Snapshot.GameRoot!, weakRoot),
            "public-valid native-minimal picker selection still persists through lifecycle guard");
        string weakLaunchError = string.Empty;
        try
        {
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start",
                JsonSerializer.SerializeToElement(new { profileId }),
                CancellationToken.None);
        }
        catch (BridgeCommandException error)
        {
            weakLaunchError = error.Code;
        }
        Require(
            weakLaunchError == "GAME_ROOT_NOT_FOUND" && startCalls == 0,
            "strict-invalid picker selection must unbind launch instead of falling back to another detected root");

        NativeGameRootSelectionResult selected =
            backend.SaveNativeGameRootSelection(rootB);
        Require(
            selected.Valid &&
            SamePath(selected.Path!, rootB) &&
            SamePath(config.Snapshot.GameRoot!, rootB),
            "native picker selection updates persisted root through lifecycle guard");

        JsonElement profilePayload =
            JsonSerializer.SerializeToElement(new { profileId });
        JsonElement started = JsonSerializer.SerializeToElement(
            await lifecycle.InvokeAsync(
                "profile_instance_start",
                profilePayload,
                CancellationToken.None),
            JsonOptions.Default);
        Require(
            startCalls == 1 &&
            started.GetProperty("phase").GetString() == "running",
            "existing lifecycle launches from picker-selected strict root without host restart");

        bool activeRetargetRejected = false;
        try
        {
            backend.SaveNativeGameRootSelection(rootA);
        }
        catch (BridgeCommandException error)
        {
            activeRetargetRejected =
                error.Code == "GAME_OPERATION_IN_PROGRESS";
        }
        Require(
            activeRetargetRejected &&
            SamePath(config.Snapshot.GameRoot!, rootB),
            "active owned lifecycle rejects picker retarget and preserves persisted root");

        string instanceId =
            started.GetProperty("instanceId").GetString() ??
            throw new InvalidDataException("started instance ID missing");
        JsonElement stopPayload =
            JsonSerializer.SerializeToElement(new { profileId, instanceId });
        await lifecycle.InvokeAsync(
            "profile_instance_stop",
            stopPayload,
            CancellationToken.None);
        Require(
            stopCalls == 1 && !processAlive,
            "isolated picker/rebind proof stops only its synthetic owned process");
    }

    private static async Task VerifyFreshHostSelectionOwnershipAsync(string root)
    {
        string detectedA = CreateStrictRoot(Path.Combine(root, "fresh-detected-a"));
        string selectedWeakB = CreateNativeRoot(Path.Combine(root, "fresh-selected-weak-b"));
        string selectedStrictB = CreateStrictRoot(Path.Combine(root, "fresh-selected-strict-b"));
        string configRoot = Path.Combine(root, "fresh-host-config");

        var saved = new LocalConfigStore(configRoot);
        saved.Update(current => current with { GameRoot = selectedWeakB });
        string profileId = saved.Snapshot.ProfileId;
        var hooks = new GameInstallationTestHooks
        {
            DefaultRoot = detectedA,
            GetEnvironmentVariable = _ => null,
            NearbyRoot = Path.Combine(root, "fresh-missing-nearby"),
            LocalAppData = Path.Combine(root, "fresh-missing-local"),
            DiscoveredCandidates = Array.Empty<GameRootCandidate>(),
        };

        // Re-open the persisted config to model a fresh Desktop process. The legacy
        // strict resolver still demonstrates the dangerous fallback to A, while the
        // lifecycle admission resolver must remain owned by saved selection B.
        var restarted = new LocalConfigStore(configRoot);
        var installation = new GameInstallationService(restarted, hooks);
        GameRootStatus fallback = installation.GetStatus();
        NativeGameRootStatus publicSelection = installation.GetNativeStatus();
        GameRootStatus launchAdmission = installation.GetLaunchAdmissionStatus();
        Require(
            fallback.Valid && SamePath(fallback.Path, detectedA),
            "fresh-host counterexample must retain legacy strict fallback detection for diagnostics");
        Require(
            publicSelection.Valid && SamePath(publicSelection.Root, selectedWeakB),
            "fresh host must retain the persisted public picker selection before lifecycle composition");
        Require(
            !launchAdmission.Valid && SamePath(launchAdmission.Path, selectedWeakB),
            "fresh lifecycle admission must reject weak-valid selected B instead of falling back to detected A");

        int weakStartCalls = 0;
        using (var weakLifecycle = new OverviewLifecycleService(
            profileId,
            launchAdmission.Valid ? launchAdmission.Path : null,
            helperPath: Path.Combine(root, "fresh-fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            config: restarted,
            testHooks: new OverviewLifecycleTestHooks
            {
                RunHelperAsync = (_, _) =>
                {
                    weakStartCalls++;
                    throw new InvalidOperationException("fresh weak selection must not invoke launch helper");
                },
            },
            startRecoveryMonitor: false,
            runtimeRoot: Path.Combine(root, "fresh-overview-runtime"),
            evidenceRoot: Path.Combine(root, "fresh-overview-evidence"),
            backupRoot: Path.Combine(root, "fresh-overview-backups")))
        {
            string errorCode = string.Empty;
            try
            {
                _ = await weakLifecycle.InvokeAsync(
                    "profile_instance_start",
                    JsonSerializer.SerializeToElement(new { profileId }),
                    CancellationToken.None);
            }
            catch (BridgeCommandException error)
            {
                errorCode = error.Code;
            }
            Require(
                errorCode == "GAME_ROOT_NOT_FOUND" && weakStartCalls == 0,
                "fresh host with weak-valid B must fail closed without launching detected A");
        }

        installation.SaveNativeSelection(selectedStrictB);
        var restartedStrict = new LocalConfigStore(configRoot);
        GameRootStatus strictAdmission =
            new GameInstallationService(restartedStrict, hooks).GetLaunchAdmissionStatus();
        Require(
            strictAdmission.Valid && SamePath(strictAdmission.Path, selectedStrictB),
            "later strict-valid saved B must become the exact fresh-host lifecycle admission root");
    }

    private static JsonElement StartResult(
        OverviewHelperInvocation invocation,
        string root,
        int gamePid,
        int launcherPid,
        string startedAt)
    {
        string challengeHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(invocation.Challenge!)))
            .ToLowerInvariant();
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
            gamePath = Path.Combine(root, "Game", "LastWar.exe"),
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
        });
    }

    private static JsonElement StopResult(
        OverviewHelperInvocation invocation,
        string root,
        int gamePid,
        string startedAt) =>
        JsonSerializer.SerializeToElement(new
        {
            ok = true,
            mode = "overview_exact_pid_close_restore",
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = invocation.ProfileId,
            sessionId = invocation.SessionId,
            gamePid,
            gamePath = Path.Combine(root, "Game", "LastWar.exe"),
            gameStartedAtUtc = startedAt,
            close = new { method = "synthetic", accepted = true, processExited = true, alreadyExited = false },
            restore = new { restored = true },
            gameRunning = false,
            installedFilesChanged = false,
        });

    private static byte[] Heartbeat(
        string profileId,
        string session,
        string challenge,
        int gamePid)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId,
            sessionId = session,
            challenge,
            gamePid,
            updatedAt = now,
            ready = true,
            messageVisible = true,
            messageText = OverviewLifecycleService.ReadyMessage,
        });
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    private static void ExpectError(
        Action action,
        string expectedCode,
        string expectedMessage,
        string label)
    {
        try
        {
            action();
            throw new InvalidOperationException(
                $"{label}: expected {expectedCode}.");
        }
        catch (BridgeCommandException error)
        {
            Require(
                error.Code == expectedCode,
                $"{label}: expected {expectedCode}, got {error.Code}");
            Require(
                error.Message == expectedMessage,
                $"{label}: expected message '{expectedMessage}', got '{error.Message}'");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
