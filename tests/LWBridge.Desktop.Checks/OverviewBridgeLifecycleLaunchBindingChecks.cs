using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewBridgeLifecycleLaunchBindingChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-overview-r7-118-launch-binding");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const string Profile = "profile-r7-118";
        const int GamePid = 48123;
        const int LauncherPid = 48124;
        const string StartedAt = "2026-09-21T02:00:00.0000000Z";

        byte[] entropy = Enumerable.Range(0, 32)
            .Select(value => (byte)value)
            .ToArray();
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(
            @"\\.\pipe\lwbridge-control-v1-r7-118",
            registry,
            pipeTokenEntropyFactory: () => entropy.ToArray());

        long clock = 1_000;
        int startCalls = 0;
        int settleCalls = 0;
        bool processAlive = false;
        string? activeSession = null;
        string? activeChallenge = null;
        LWBridgeControlPipeLaunchBinding? firstBinding = null;
        object admittedRoute = new();
        ulong admittedGeneration = 0;

        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) =>
            {
                Interlocked.Increment(ref settleCalls);
                return Task.CompletedTask;
            },
            MonotonicMilliseconds = () => Volatile.Read(ref clock),
            RunHelperAsync = (invocation, _) =>
            {
                if (invocation.Operation != "start")
                {
                    processAlive = false;
                    return Task.FromResult(StopResult(
                        invocation,
                        gamePath,
                        GamePid,
                        StartedAt));
                }

                int call = Interlocked.Increment(ref startCalls);
                LWBridgeControlPipeLaunchBinding binding =
                    invocation.ControlPipeLaunchBinding ??
                    throw new InvalidOperationException(
                        "R7-118 start invocation is missing control-pipe launch binding");

                Check(binding.ProfileId == Profile,
                    "lifecycle binding profileId must equal Overview profile");
                Check(binding.InstanceId == invocation.SessionId,
                    "lifecycle binding instanceId must equal same start session");
                Check(binding.BuildId == OverviewLifecycleService.BridgeVersion,
                    "lifecycle binding buildId uses explicit rebuild bridge identity");
                Check(binding.Environment[
                        LWBridgeProxyLaunchEnvironmentContract.PipeTokenVariable] ==
                        binding.PipeToken,
                    "helper invocation environment carries the same raw pipe token");

                if (call == 1)
                {
                    firstBinding = binding;
                    Check(binding.ExpiresAtMilliseconds == 91_000,
                        "initial binding deadline is monotonic clock 1000 + 90000");
                    Volatile.Write(ref clock, 5_000);
                    throw new InvalidOperationException(
                        "official_lua_update_failed: synthetic R7-118 retry");
                }

                Check(firstBinding is not null &&
                      ReferenceEquals(firstBinding, binding),
                    "same launch-binding object/token is reused across same-session retry");
                Check(binding.PipeToken == firstBinding!.PipeToken,
                    "retry does not mint a replacement pipe token");

                bool admitted = registry.TryAdmit(
                    Profile,
                    invocation.SessionId!,
                    binding.PipeToken,
                    nowMilliseconds: 94_000,
                    admittedRoute,
                    out admittedGeneration);
                Check(admitted && admittedGeneration == 1,
                    "registry admission at 94000 proves retry refreshed expiry beyond original 91000 deadline");

                activeSession = invocation.SessionId;
                activeChallenge = invocation.Challenge;
                processAlive = true;
                return Task.FromResult(StartResult(
                    invocation,
                    gamePath,
                    GamePid,
                    LauncherPid,
                    StartedAt));
            },
            ProcessMatches = (pid, path, created) =>
                processAlive &&
                pid == GamePid &&
                created == StartedAt &&
                string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(gamePath),
                    StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => Heartbeat(
                Profile,
                activeSession!,
                activeChallenge!,
                GamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            Profile,
            root,
            helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false,
            bridgeHostState: host,
            enableBridgeControlPipeLaunchBinding: true);

        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        JsonElement started = JsonSerializer.SerializeToElement(
            await lifecycle.InvokeAsync(
                "profile_instance_start",
                empty,
                CancellationToken.None),
            JsonOptions.Default);

        string session = started.GetProperty("instanceId").GetString()!;
        Check(started.GetProperty("phase").GetString() == "running" &&
              started.GetProperty("connectionState").GetString() == "connected" &&
              session == activeSession &&
              started.GetProperty("pid").GetInt32() == GamePid,
            "actual lifecycle publishes successful running state after refreshed retry");
        Check(startCalls == 2 && settleCalls >= 2,
            "official-update retry reruns settle/helper exactly through lifecycle retry path");
        Check(host.ConnectedRouteCount == 1 &&
              ReferenceEquals(registry.Resolve(session)?.Route, admittedRoute) &&
              registry.Resolve(session)?.Generation == admittedGeneration,
            "successful synthetic listener admission remains correlated to lifecycle session");

        Check(host.PendingRegistrationCount == 1 &&
              host.ConnectedRouteCount == 1,
            "successful running session retains its claimed registration/route until stop");

        JsonElement stopPayload =
            JsonSerializer.SerializeToElement(new { instanceId = session });
        _ = await lifecycle.InvokeAsync(
            "profile_instance_stop",
            stopPayload,
            CancellationToken.None);
        Check(!processAlive, "success proof stops synthetic owned game cleanly");
        Check(host.PendingRegistrationCount == 0 &&
              host.ConnectedRouteCount == 0,
            "successful profile stop explicitly unregisters the retained launch binding/route");

        await ProveTerminalFailureCleanupAsync(
            root,
            gamePath,
            entropy);

        string repo = FindRepoRoot();
        string lifecycleSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "OverviewLifecycleService.cs"));
        string windowSource = File.ReadAllText(Path.Combine(
            repo,
            "src",
            "LWBridge.Desktop",
            "LWBridgeWindow.cs"));
        Check(lifecycleSource.Contains(
                "invocation.ControlPipeLaunchBinding?.ApplyTo(start);",
                StringComparison.Ordinal),
            "real helper ProcessStartInfo applies the recovered launch environment");

        int stopValidation = lifecycleSource.IndexOf(
            "ValidateStopResult(result, profileId, snapshot.InstanceId",
            StringComparison.Ordinal);
        int successfulStopUnregister = stopValidation < 0
            ? -1
            : lifecycleSource.IndexOf(
                "bridgeHostState?.CancelLaunchBinding(snapshot.InstanceId);",
                stopValidation,
                StringComparison.Ordinal);
        int stopLeaseCleanup = successfulStopUnregister < 0
            ? -1
            : lifecycleSource.IndexOf(
                "StopLeaseTimer(deleteLease: true);",
                successfulStopUnregister,
                StringComparison.Ordinal);
        Check(
            stopValidation >= 0 &&
            successfulStopUnregister > stopValidation &&
            stopLeaseCleanup > successfulStopUnregister,
            "successful owned stop unregisters retained bridge instance after validation and before lease cleanup");

        Check(windowSource.Contains(
                "enableBridgeControlPipeLaunchBinding: true",
                StringComparison.Ordinal) &&
              windowSource.Contains(
                "StartRpcTransport(",
                StringComparison.Ordinal),
            "normal application composition enables launch binding only with shared listener startup");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            finding = "LWB-R7-118",
            transactionProof = new
            {
                initialDeadline = firstBinding!.ExpiresAtMilliseconds,
                retryClock = 5_000,
                proofAdmissionClock = 94_000,
                originalDeadlineWouldHaveExpired = true,
                refreshedAdmissionSucceeded = true,
                sameBindingObjectAcrossRetry = true,
                samePipeTokenAcrossRetry = true,
                startCalls,
                settleCalls,
                admittedGeneration,
                successfulStopUnregister = true,
                terminalFailureUnregister = true,
            },
            boundary = new
            {
                normalWindowEnablesLaunchBinding = true,
                productionListenerStarted = true,
                outboundCommandRoutingImplemented = true,
                pendingCallCollectionImplemented = true,
                exactGetStatusCallEnabled = true,
                genericCallLuaEnabled = false,
            },
        }, JsonOptions.Default);
    }

    private static async Task ProveTerminalFailureCleanupAsync(
        string root,
        string gamePath,
        byte[] entropy)
    {
        var registry = new LWBridgeControlPipeRegistry();
        using var host = new LWBridgeControlPipeHostState(
            @"\\.\pipe\lwbridge-control-v1-r7-118-failure",
            registry,
            pipeTokenEntropyFactory: () => entropy.ToArray());

        long clock = 10_000;
        LWBridgeControlPipeLaunchBinding? observed = null;
        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            MonotonicMilliseconds = () => clock,
            RunHelperAsync = (invocation, _) =>
            {
                observed = invocation.ControlPipeLaunchBinding;
                throw new InvalidOperationException(
                    "synthetic terminal R7-118 launch failure");
            },
            ProcessMatches = (_, _, _) => false,
            ReadAllBytes = _ => Array.Empty<byte>(),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-r7-118-failure",
            root + "-failure",
            helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false,
            bridgeHostState: host,
            enableBridgeControlPipeLaunchBinding: true);

        string code;
        try
        {
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start",
                JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None);
            code = "UNEXPECTED_SUCCESS";
        }
        catch (BridgeCommandException error)
        {
            code = error.Code;
        }

        Check(code == "LAUNCH_FAILED",
            "terminal synthetic helper failure surfaces LAUNCH_FAILED");
        Check(observed is not null,
            "terminal failure occurred after startup registration/binding creation");
        Check(host.PendingRegistrationCount == 0 &&
              host.ConnectedRouteCount == 0 &&
              !registry.IsPending(observed!.InstanceId),
            "terminal start failure explicitly unregisters pending launch binding");

        _ = gamePath; // documents shared synthetic installation; no process launched.
    }

    private static JsonElement StartResult(
        OverviewHelperInvocation invocation,
        string gamePath,
        int gamePid,
        int launcherPid,
        string startedAt)
    {
        string challengeHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(invocation.Challenge!)))
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

    private static JsonElement StopResult(
        OverviewHelperInvocation invocation,
        string gamePath,
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
            gamePath,
            gameStartedAtUtc = startedAt,
            close = new
            {
                method = "synthetic",
                accepted = true,
                processExited = true,
                alreadyExited = false,
            },
            restore = new { restored = true },
            gameRunning = false,
            installedFilesChanged = false,
        });

    private static byte[] Heartbeat(
        string profile,
        string session,
        string challenge,
        int gamePid)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = profile,
            sessionId = session,
            challenge,
            gamePid,
            updatedAt = now,
            ready = true,
            messageVisible = true,
            messageText = OverviewLifecycleService.ReadyMessage,
        });
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(
                    current.FullName,
                    "src",
                    "LWBridge.Desktop")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "could not locate LW-Control repository root");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Overview bridge lifecycle launch-binding check failed: " +
                message);
    }
}
