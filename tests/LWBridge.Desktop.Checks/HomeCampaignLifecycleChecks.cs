using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class HomeCampaignLifecycleChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-home-campaign-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            JsonElement pendingRepeated = await RunPendingRepeatedStartAsync(
                Path.Combine(root, "pending-repeated")).WaitAsync(TimeSpan.FromSeconds(10));
            JsonElement closeDuringStart = await RunCloseDuringStartAsync(
                Path.Combine(root, "close-during-start")).WaitAsync(TimeSpan.FromSeconds(10));
            JsonElement staleIdentityAndStop = await RunStaleIdentityAndExactStopAsync(
                Path.Combine(root, "stale-identity-stop")).WaitAsync(TimeSpan.FromSeconds(10));
            JsonElement nativeRejectionRetry = await RunNativeRejectionRetryAsync(
                Path.Combine(root, "native-rejection-retry")).WaitAsync(TimeSpan.FromSeconds(10));
            JsonElement profileMismatch = await RunProfileMismatchAsync(
                Path.Combine(root, "profile-mismatch")).WaitAsync(TimeSpan.FromSeconds(10));
            JsonElement recoveryAndShutdown = await RunRecoveryAndShutdownAsync(
                Path.Combine(root, "recovery-shutdown")).WaitAsync(TimeSpan.FromSeconds(10));

            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                acceptanceCase = "LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-home-lifecycle-bcd",
                scope = "isolated OverviewLifecycleService/LWBridgeBackend hooks only; no real Last War, updater, original service, or live provider action",
                pendingRepeated,
                closeDuringStart,
                staleIdentityAndStop,
                nativeRejectionRetry,
                profileMismatch,
                recoveryAndShutdown,
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { }
        }
    }

    private static async Task<JsonElement> RunPendingRepeatedStartAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        const string profileId = "home-campaign-pending";
        const int gamePid = 53101;
        const string startedAtUtc = "2026-10-06T01:00:00.0000000Z";
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        int startCalls = 0;
        int stopCalls = 0;
        var helperEntered = new TaskCompletionSource<OverviewHelperInvocation>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var helperRelease = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = async (invocation, cancellationToken) =>
            {
                if (invocation.Operation == "start")
                {
                    Interlocked.Increment(ref startCalls);
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    helperEntered.TrySetResult(invocation);
                    await helperRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    processAlive = true;
                    return StartResult(invocation, gamePath, gamePid, startedAtUtc);
                }

                Interlocked.Increment(ref stopCalls);
                bool wasAlive = processAlive;
                processAlive = false;
                return StopResult(invocation, wasAlive);
            },
            ProcessMatches = (pid, path, startedAt) =>
                processAlive &&
                pid == gamePid &&
                startedAt == startedAtUtc &&
                PathEquals(path, gamePath),
            ReadAllBytes = _ => Heartbeat(profileId, session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement payload = JsonSerializer.SerializeToElement(new { profileId });

        Task<object?> primary = lifecycle.InvokeAsync(
            "profile_instance_start", payload, CancellationToken.None);
        OverviewHelperInvocation admitted = await helperEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string admittedSession = admitted.SessionId!;

        JsonElement pending = Status(await lifecycle.InvokeAsync(
            "profile_instance_status", payload, CancellationToken.None));
        string repeatedCode = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
        });

        Check(repeatedCode == "GAME_OPERATION_IN_PROGRESS",
            "a repeated Start must reject while the admitted Start is still pending");
        Check(Volatile.Read(ref startCalls) == 1,
            "a repeated pending Start must not invoke a second helper launch");
        Check(pending.GetProperty("phase").GetString() == "starting" &&
              pending.GetProperty("connectionState").GetString() == "starting" &&
              pending.GetProperty("instanceId").GetString() == admittedSession &&
              pending.GetProperty("pid").ValueKind == JsonValueKind.Null,
            "pending status must retain one in-flight instance without fabricating PID ownership");

        helperRelease.TrySetResult();
        JsonElement running = Status(await primary.WaitAsync(TimeSpan.FromSeconds(5)));
        Check(running.GetProperty("phase").GetString() == "running" &&
              running.GetProperty("instanceId").GetString() == admittedSession &&
              running.GetProperty("pid").GetInt32() == gamePid,
            "the admitted Start must be the only operation allowed to publish running ownership");

        string runningDuplicateCode = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
        });
        Check(runningDuplicateCode == "GAME_RUNNING",
            "a duplicate Start after ownership publication must fail as GAME_RUNNING");

        JsonElement stopped = Status(await lifecycle.InvokeAsync(
            "profile_instance_stop",
            JsonSerializer.SerializeToElement(new { profileId, instanceId = admittedSession }),
            CancellationToken.None));
        Check(stopped.GetProperty("phase").GetString() == "stopped" &&
              !processAlive && stopCalls == 1,
            "pending/repeated acceptance must finish with one exact owned Stop");

        return JsonSerializer.SerializeToElement(new
        {
            pendingPhase = pending.GetProperty("phase").GetString(),
            pendingInstanceId = admittedSession,
            repeatedPendingError = repeatedCode,
            duplicateRunningError = runningDuplicateCode,
            helperStartCalls = startCalls,
            helperStopCalls = stopCalls,
            finalPhase = stopped.GetProperty("phase").GetString(),
        });
    }

    private static async Task<JsonElement> RunCloseDuringStartAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        const string profileId = "home-campaign-close-start";
        var helperEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string? startSession = null;
        string? startChallenge = null;
        string? markerSession = null;
        string? markerChallenge = null;
        int startCalls = 0;
        int stopCalls = 0;

        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = async (invocation, _) =>
            {
                if (invocation.Operation != "start")
                {
                    Interlocked.Increment(ref stopCalls);
                    throw new InvalidOperationException(
                        "pre-publication Close cancellation must not invent a compensating Stop");
                }

                Interlocked.Increment(ref startCalls);
                startSession = invocation.SessionId;
                startChallenge = invocation.Challenge;
                helperEntered.TrySetResult();
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                throw new BridgeCommandException(
                    "GAME_OPERATION_CANCELLED",
                    "synthetic helper observed the correlated Close cancellation marker");
            },
            WriteStartCancellation = (session, challenge, _) =>
            {
                markerSession = session;
                markerChallenge = challenge;
                cancellationObserved.TrySetResult();
            },
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);

        Task<object?> start = lifecycle.InvokeAsync(
            "profile_instance_start",
            JsonSerializer.SerializeToElement(new { profileId }),
            CancellationToken.None);
        await helperEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        lifecycle.Close();

        string code = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await start.ConfigureAwait(false);
        });
        JsonElement status = Status(lifecycle.CreateInstanceStatus());

        Check(code == "GAME_OPERATION_CANCELLED",
            "Close during pending Start must surface GAME_OPERATION_CANCELLED");
        Check(startCalls == 1 && stopCalls == 0,
            "pre-publication Close cancellation must use the start cancellation marker without a synthetic Stop");
        Check(startSession is not null && startChallenge is not null &&
              markerSession == startSession && markerChallenge == startChallenge,
            "Close cancellation must be correlated to the exact admitted session/challenge");
        Check(status.GetProperty("phase").GetString() == "stopped" &&
              status.GetProperty("instanceId").ValueKind == JsonValueKind.Null &&
              status.GetProperty("pid").ValueKind == JsonValueKind.Null,
            "Close-cancelled startup must leave no owned instance or PID");

        return JsonSerializer.SerializeToElement(new
        {
            error = code,
            correlatedCancellationMarker = true,
            helperStartCalls = startCalls,
            compensatingStopCalls = stopCalls,
            finalPhase = status.GetProperty("phase").GetString(),
            ownsInstance = status.GetProperty("instanceId").ValueKind != JsonValueKind.Null,
        });
    }

    private static async Task<JsonElement> RunStaleIdentityAndExactStopAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        const string profileId = "home-campaign-stale-stop";
        const int gamePid = 53301;
        const string startedAtUtc = "2026-10-06T01:20:00.0000000Z";
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        int startCalls = 0;
        int stopCalls = 0;
        OverviewHelperInvocation? lastStop = null;

        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(current => current with
        {
            ProfileId = profileId,
            GameRoot = root,
            AutoReconnect = false,
        });

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
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, startedAtUtc));
                }

                stopCalls++;
                lastStop = invocation;
                bool wasAlive = processAlive;
                processAlive = false;
                return Task.FromResult(StopResult(invocation, wasAlive));
            },
            ProcessMatches = (pid, path, startedAt) =>
                processAlive && pid == gamePid && startedAt == startedAtUtc && PathEquals(path, gamePath),
            ReadAllBytes = _ => Heartbeat(profileId, session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
            UpdateProcessRunning = () => false,
            UpdateActivityFingerprint = () => null,
            ProcessHung = (_, _) => false,
            DelayAsync = (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            config: config,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement profilePayload = JsonSerializer.SerializeToElement(new { profileId });
        JsonElement running = Status(await lifecycle.InvokeAsync(
            "profile_instance_start", profilePayload, CancellationToken.None));
        string ownedSession = running.GetProperty("instanceId").GetString()!;

        string foreignStopCode = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await lifecycle.InvokeAsync(
                "profile_instance_stop",
                JsonSerializer.SerializeToElement(new { profileId, instanceId = "foreign-instance" }),
                CancellationToken.None).ConfigureAwait(false);
        });
        Check(foreignStopCode == "INSTANCE_NOT_OWNED" && stopCalls == 0,
            "foreign instance Stop must fail closed before helper cleanup");

        processAlive = false;
        await lifecycle.RunRecoveryObservationForTestAsync();
        await lifecycle.RunRecoveryObservationForTestAsync();
        JsonElement stale = Status(lifecycle.CreateInstanceStatus());
        Check(stale.GetProperty("phase").GetString() == "error" &&
              stale.GetProperty("connectionState").GetString() == "recovering" &&
              stale.GetProperty("instanceId").GetString() == ownedSession &&
              stale.GetProperty("error").GetString() == "GAME_EXITED_RESTORE_REQUIRED",
            "two missing exact-process observations must preserve the owned session for restoration and mark stale identity");
        Check(startCalls == 1,
            "AutoReconnect=false must prevent stale identity from creating a replacement Start");

        JsonElement stopped = Status(await lifecycle.InvokeAsync(
            "profile_instance_stop",
            JsonSerializer.SerializeToElement(new { profileId, instanceId = ownedSession }),
            CancellationToken.None));
        Check(stopCalls == 1 && lastStop is not null &&
              lastStop.SessionId == ownedSession &&
              lastStop.GamePid == gamePid &&
              lastStop.GamePath is not null && PathEquals(lastStop.GamePath, gamePath) &&
              lastStop.GameStartedAtUtc == startedAtUtc,
            "exact Stop after stale identity must retain the original session/PID/path/creation identity");
        Check(stopped.GetProperty("phase").GetString() == "stopped" &&
              !config.Snapshot.GameDesiredRunning,
            "exact stale-instance cleanup must end stopped and clear desired-running intent");

        return JsonSerializer.SerializeToElement(new
        {
            foreignStopError = foreignStopCode,
            stalePhase = stale.GetProperty("phase").GetString(),
            staleError = stale.GetProperty("error").GetString(),
            staleInstancePreserved = stale.GetProperty("instanceId").GetString() == ownedSession,
            exactStopCalls = stopCalls,
            exactStopIdentityMatched = true,
            finalPhase = stopped.GetProperty("phase").GetString(),
        });
    }

    private static async Task<JsonElement> RunNativeRejectionRetryAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        const string profileId = "home-campaign-native-error";
        const int gamePid = 53401;
        const string startedAtUtc = "2026-10-06T01:30:00.0000000Z";
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        string? rejectedSession = null;
        string? rejectedChallenge = null;
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
                    if (startCalls == 1)
                    {
                        rejectedSession = invocation.SessionId;
                        rejectedChallenge = invocation.Challenge;
                        throw new BridgeCommandException(
                            "BRIDGE_START_TIMEOUT",
                            "synthetic native start rejection");
                    }

                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    processAlive = true;
                    return Task.FromResult(StartResult(invocation, gamePath, gamePid, startedAtUtc));
                }

                stopCalls++;
                bool wasAlive = processAlive;
                processAlive = false;
                return Task.FromResult(StopResult(invocation, wasAlive));
            },
            ProcessMatches = (pid, path, startedAt) =>
                processAlive && pid == gamePid && startedAt == startedAtUtc && PathEquals(path, gamePath),
            ReadAllBytes = _ => Heartbeat(profileId, session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement payload = JsonSerializer.SerializeToElement(new { profileId });

        string rejection = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await lifecycle.InvokeAsync(
                "profile_instance_start", payload, CancellationToken.None).ConfigureAwait(false);
        });
        JsonElement failed = Status(await lifecycle.InvokeAsync(
            "profile_instance_status", payload, CancellationToken.None));
        Check(rejection == "BRIDGE_START_TIMEOUT" &&
              failed.GetProperty("phase").GetString() == "error" &&
              failed.GetProperty("lastError").GetString() == "BRIDGE_START_TIMEOUT" &&
              failed.GetProperty("pid").ValueKind == JsonValueKind.Null,
            "native start rejection must remain an error without fabricated PID ownership");

        JsonElement retry = Status(await lifecycle.InvokeAsync(
            "profile_instance_start", payload, CancellationToken.None));
        string retrySession = retry.GetProperty("instanceId").GetString()!;
        Check(startCalls == 2 && retry.GetProperty("phase").GetString() == "running" &&
              rejectedSession is not null && rejectedChallenge is not null &&
              retrySession != rejectedSession && challenge != rejectedChallenge,
            "retry after native rejection must use a fresh session/challenge and may publish only the new ownership");

        _ = await lifecycle.InvokeAsync(
            "profile_instance_stop",
            JsonSerializer.SerializeToElement(new { profileId, instanceId = retrySession }),
            CancellationToken.None);
        Check(stopCalls == 1 && !processAlive,
            "successful retry must remain exactly stoppable after native rejection");

        return JsonSerializer.SerializeToElement(new
        {
            firstError = rejection,
            failedPhase = failed.GetProperty("phase").GetString(),
            failedOwnsPid = failed.GetProperty("pid").ValueKind != JsonValueKind.Null,
            retryUsedFreshIdentity = retrySession != rejectedSession && challenge != rejectedChallenge,
            helperStartCalls = startCalls,
            helperStopCalls = stopCalls,
        });
    }

    private static async Task<JsonElement> RunProfileMismatchAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        const string profileId = "home-campaign-profile-a";
        int helperCalls = 0;
        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(current => current with
        {
            ProfileId = profileId,
            GameRoot = root,
        });
        var hooks = new OverviewLifecycleTestHooks
        {
            RunHelperAsync = (_, _) =>
            {
                helperCalls++;
                throw new InvalidOperationException("profile-scope rejection must happen before lifecycle helper execution");
            },
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            config: config,
            testHooks: hooks,
            startRecoveryMonitor: false);
        var backend = new LWBridgeBackend(
            config,
            asyncCommands: lifecycle,
            overviewLifecycle: lifecycle);

        string foreignStart = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await backend.InvokeAsync(
                "profile_instance_start",
                JsonSerializer.SerializeToElement(new { profileId = "home-campaign-profile-b" }),
                CancellationToken.None).ConfigureAwait(false);
        });
        string missingStart = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await backend.InvokeAsync(
                "profile_instance_start",
                JsonSerializer.SerializeToElement(new { }),
                CancellationToken.None).ConfigureAwait(false);
        });
        string foreignStatus = await CaptureBridgeErrorAsync(async () =>
        {
            _ = await backend.InvokeAsync(
                "profile_instance_status",
                JsonSerializer.SerializeToElement(new { profileId = "home-campaign-profile-b" }),
                CancellationToken.None).ConfigureAwait(false);
        });

        Check(foreignStart == "PROFILE_SCOPE_MISMATCH" &&
              missingStart == "PROFILE_REQUIRED" &&
              foreignStatus == "PROFILE_RUNTIME_UNAVAILABLE" &&
              helperCalls == 0,
            "backend profile scoping must retire foreign/missing lifecycle work before helper execution");

        return JsonSerializer.SerializeToElement(new
        {
            foreignStart,
            missingStart,
            foreignStatus,
            lifecycleHelperCalls = helperCalls,
        });
    }

    private static async Task<JsonElement> RunRecoveryAndShutdownAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        const string profileId = "home-campaign-recovery";
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(current => current with
        {
            ProfileId = profileId,
            GameRoot = root,
            AutoReconnect = true,
        });

        bool processAlive = false;
        bool failNextStart = false;
        bool blockRetryDelay = false;
        int startCalls = 0;
        int stopCalls = 0;
        int currentPid = 0;
        string currentStartedAtUtc = string.Empty;
        string? session = null;
        string? challenge = null;
        var retryDelayEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = (invocation, _) =>
            {
                if (invocation.Operation == "start")
                {
                    startCalls++;
                    if (failNextStart)
                    {
                        failNextStart = false;
                        throw new BridgeCommandException(
                            "BRIDGE_START_TIMEOUT",
                            "synthetic recovery relaunch rejection");
                    }

                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    currentPid = 53500 + startCalls;
                    currentStartedAtUtc = $"2026-10-06T01:{40 + startCalls:00}:00.0000000Z";
                    processAlive = true;
                    return Task.FromResult(StartResult(
                        invocation, gamePath, currentPid, currentStartedAtUtc));
                }

                stopCalls++;
                bool wasAlive = processAlive;
                processAlive = false;
                return Task.FromResult(StopResult(invocation, wasAlive));
            },
            ProcessMatches = (pid, path, startedAt) =>
                processAlive &&
                pid == currentPid &&
                startedAt == currentStartedAtUtc &&
                PathEquals(path, gamePath),
            ReadAllBytes = _ => Heartbeat(profileId, session!, challenge!, currentPid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
            UpdateProcessRunning = () => false,
            UpdateActivityFingerprint = () => null,
            ProcessHung = (_, _) => false,
            TerminateOwnedProcessAsync = (_, _, _, _) =>
            {
                processAlive = false;
                return Task.CompletedTask;
            },
            DelayAsync = async (_, token) =>
            {
                if (!blockRetryDelay)
                {
                    token.ThrowIfCancellationRequested();
                    return;
                }

                retryDelayEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            },
        };

        using var lifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            config: config,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement payload = JsonSerializer.SerializeToElement(new { profileId });

        JsonElement first = Status(await lifecycle.InvokeAsync(
            "profile_instance_start", payload, CancellationToken.None));
        string firstSession = first.GetProperty("instanceId").GetString()!;
        Check(config.Snapshot.GameDesiredRunning,
            "successful Start must arm desired-running before recovery acceptance");

        processAlive = false;
        await lifecycle.RunRecoveryObservationForTestAsync();
        await lifecycle.RunRecoveryObservationForTestAsync();
        JsonElement recovered = Status(lifecycle.CreateInstanceStatus());
        string recoveredSession = recovered.GetProperty("instanceId").GetString()!;
        Check(startCalls == 2 && stopCalls == 1 &&
              recoveredSession != firstSession &&
              recovered.GetProperty("phase").GetString() == "running" &&
              lifecycle.CurrentRecoveryStatus.State == "succeeded",
            "unexpected owned-process loss with reconnect enabled must perform exact cleanup and one fresh-session recovery Start");

        processAlive = false;
        failNextStart = true;
        blockRetryDelay = true;
        await lifecycle.RunRecoveryObservationForTestAsync();
        Task closingRecovery = lifecycle.RunRecoveryObservationForTestAsync();
        await retryDelayEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        int startsAtClose = startCalls;
        lifecycle.Close();
        await closingRecovery.WaitAsync(TimeSpan.FromSeconds(5));

        Check(lifecycle.CurrentRecoveryStatus.State == "idle" &&
              !lifecycle.RuntimeManaged &&
              !processAlive,
            "Close during an active recovery retry must cancel the retry owner and leave no managed process");
        await lifecycle.RunRecoveryObservationForTestAsync();
        Check(startCalls == startsAtClose,
            "closed lifecycle must not launch another recovery attempt after shutdown cancellation");

        var reopenedConfig = new LocalConfigStore(Path.Combine(root, "config"));
        Check(reopenedConfig.Snapshot.AutoReconnect && reopenedConfig.Snapshot.GameDesiredRunning,
            "fresh config ownership must preserve reconnect and desired-running state across host restart");
        blockRetryDelay = false;
        int startsBeforeFreshHost = startCalls;
        using var freshLifecycle = new OverviewLifecycleService(
            profileId,
            root,
            helperPath: Path.Combine(root, "fake-overview-helper.py"),
            requireCurrentClientEvidence: false,
            config: reopenedConfig,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement reconcile = Status(await freshLifecycle.InvokeAsync(
            "profile_instances_reconcile",
            JsonSerializer.SerializeToElement(new { autoLaunchAll = true }),
            CancellationToken.None));
        JsonElement freshRunning = Status(freshLifecycle.CreateInstanceStatus());
        Check(reconcile.GetProperty("errors").GetArrayLength() == 0 &&
              startCalls == startsBeforeFreshHost + 1 &&
              freshRunning.GetProperty("phase").GetString() == "running",
            "a fresh lifecycle owner must consume persisted Auto Launch/reconnect state through startup reconcile");
        string freshSession = freshRunning.GetProperty("instanceId").GetString()!;
        _ = await freshLifecycle.InvokeAsync(
            "profile_instance_stop",
            JsonSerializer.SerializeToElement(new { profileId, instanceId = freshSession }),
            CancellationToken.None);
        Check(!reopenedConfig.Snapshot.GameDesiredRunning && !processAlive,
            "fresh-host exact Stop must clear persisted desired-running state and leave no owned process");

        return JsonSerializer.SerializeToElement(new
        {
            firstSession,
            recoveredSession,
            successfulRecoveryUsedFreshSession = recoveredSession != firstSession,
            successfulRecoveryState = "succeeded",
            startsBeforeShutdown = startsAtClose,
            cleanupStopCalls = stopCalls,
            shutdownRecoveryState = lifecycle.CurrentRecoveryStatus.State,
            runtimeManagedAfterShutdown = lifecycle.RuntimeManaged,
            startsAfterShutdownBeforeFreshHost = startsBeforeFreshHost,
            persistedDesiredRunningObservedByFreshHost = true,
            freshHostStartupReconcileStartDelta = startCalls - startsBeforeFreshHost,
            freshHostFinalDesiredRunning = reopenedConfig.Snapshot.GameDesiredRunning,
        });
    }

    private static async Task<string> CaptureBridgeErrorAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return "UNEXPECTED_SUCCESS";
        }
        catch (BridgeCommandException error)
        {
            return error.Code;
        }
    }

    private static JsonElement Status(object? value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static JsonElement StartResult(
        OverviewHelperInvocation invocation,
        string gamePath,
        int gamePid,
        string startedAtUtc)
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
            launcherPid = gamePid + 10_000,
            gamePath,
            gameStartedAtUtc = startedAtUtc,
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
        bool wasAlive) =>
        JsonSerializer.SerializeToElement(new
        {
            ok = true,
            mode = "overview_exact_pid_close_restore",
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = invocation.ProfileId,
            sessionId = invocation.SessionId,
            gamePid = invocation.GamePid,
            gamePath = invocation.GamePath,
            gameStartedAtUtc = invocation.GameStartedAtUtc,
            close = wasAlive
                ? new
                {
                    method = "synthetic",
                    accepted = true,
                    processExited = true,
                    alreadyExited = false,
                }
                : new
                {
                    method = "already_exited",
                    accepted = false,
                    processExited = true,
                    alreadyExited = true,
                },
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
            gameStateObserved = true,
            gameReady = true,
            loggedIn = true,
            connected = true,
            connecting = false,
            gameUid = "home-campaign-test",
            serverId = 2212,
            worldPos = 12345,
            recoveryObserved = false,
            recoveryConfirmed = false,
            recoveryAmbiguous = false,
        });
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Home campaign lifecycle acceptance failed: " + message);
    }
}
