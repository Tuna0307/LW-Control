using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewLaunchSpamChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-a04-launch-spam");
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int gamePid = 37123;
        const int launcherPid = 37124;
        const string startedAt = "2026-09-20T08:05:00.0000000Z";
        const int spamCount = 48;
        const int refreshCount = 120;

        var helperEntered = new TaskCompletionSource<OverviewHelperInvocation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var helperRelease = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        int helperStartCalls = 0;
        int helperStopCalls = 0;
        bool processAlive = false;
        string? session = null;
        string? challenge = null;

        var hooks = new OverviewLifecycleTestHooks
        {
            RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
            RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
            RunHelperAsync = async (invocation, cancellationToken) =>
            {
                if (invocation.Operation == "start")
                {
                    Interlocked.Increment(ref helperStartCalls);
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    helperEntered.TrySetResult(invocation);
                    JsonElement result = await helperRelease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    processAlive = true;
                    return result;
                }

                Interlocked.Increment(ref helperStopCalls);
                processAlive = false;
                return StopResult(invocation, gamePath, gamePid, startedAt);
            },
            ProcessMatches = (pid, path, created) => processAlive && pid == gamePid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            ReadAllBytes = _ => Heartbeat(session!, challenge!, gamePid),
            WriteLease = (_, _, _) => { },
            DeleteFile = _ => { },
        };

        using var lifecycle = new OverviewLifecycleService(
            "profile-a04-launch-spam",
            root,
            helperPath: Path.Combine(root, "fake-helper.py"),
            requireCurrentClientEvidence: false,
            testHooks: hooks,
            startRecoveryMonitor: false);
        JsonElement empty = JsonSerializer.SerializeToElement(new { });

        Task<object?> primaryStart = lifecycle.InvokeAsync("profile_instance_start", empty, CancellationToken.None);
        OverviewHelperInvocation primaryInvocation = await helperEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        string primarySession = primaryInvocation.SessionId!;
        string primaryChallenge = primaryInvocation.Challenge!;

        JsonElement initialStarting = StatusElement(
            await lifecycle.InvokeAsync("profile_instance_status", empty, CancellationToken.None));
        RequireStartingStatus(initialStarting, primarySession, "initial status while helper is blocked");

        var stopwatch = Stopwatch.StartNew();
        Task<string>[] launchSpam = Enumerable.Range(0, spamCount)
            .Select(_ => CaptureStartErrorAsync(lifecycle, empty))
            .ToArray();
        Task<JsonElement>[] refreshSpam = Enumerable.Range(0, refreshCount)
            .Select(_ => RefreshAsync(lifecycle, empty))
            .ToArray();

        await Task.WhenAll(launchSpam.Cast<Task>().Concat(refreshSpam)).WaitAsync(TimeSpan.FromSeconds(5));
        stopwatch.Stop();

        string[] launchCodes = launchSpam.Select(task => task.Result).ToArray();
        JsonElement[] refreshes = refreshSpam.Select(task => task.Result).ToArray();
        Check(launchCodes.All(code => code == "GAME_OPERATION_IN_PROGRESS"),
            "every Launch during starting must reject as GAME_OPERATION_IN_PROGRESS");
        Check(refreshes.All(status =>
            status.GetProperty("phase").GetString() == "starting" &&
            status.GetProperty("connectionState").GetString() == "starting" &&
            status.GetProperty("instanceId").GetString() == primarySession &&
            status.GetProperty("pid").ValueKind == JsonValueKind.Null),
            "every refresh during launch must return the same in-flight session without a fabricated PID/success");
        Check(Volatile.Read(ref helperStartCalls) == 1,
            "Launch spam must not invoke a second helper start");

        helperRelease.SetResult(StartResult(
            primaryInvocation, gamePath, gamePid, launcherPid, startedAt));
        JsonElement primaryResult = StatusElement(await primaryStart.WaitAsync(TimeSpan.FromSeconds(5)));
        Check(primaryResult.GetProperty("phase").GetString() == "running" &&
              primaryResult.GetProperty("connectionState").GetString() == "connected" &&
              primaryResult.GetProperty("instanceId").GetString() == primarySession &&
              primaryResult.GetProperty("pid").GetInt32() == gamePid,
            "only the original Start may publish running/connected success");
        Check(session == primarySession && challenge == primaryChallenge,
            "helper completion must remain correlated to the original session/challenge");

        JsonElement postStartRefresh = StatusElement(
            await lifecycle.InvokeAsync("profile_instance_status", empty, CancellationToken.None));
        Check(postStartRefresh.GetProperty("phase").GetString() == "running" &&
              postStartRefresh.GetProperty("instanceId").GetString() == primarySession &&
              postStartRefresh.GetProperty("pid").GetInt32() == gamePid,
            "post-start refresh must report the original successful session, not a spam request");

        JsonElement stopPayload = JsonSerializer.SerializeToElement(new { instanceId = primarySession });
        JsonElement stopped = StatusElement(
            await lifecycle.InvokeAsync("profile_instance_stop", stopPayload, CancellationToken.None));
        Check(stopped.GetProperty("phase").GetString() == "stopped" &&
              stopped.GetProperty("connectionState").GetString() == "offline" &&
              stopped.GetProperty("instanceId").ValueKind == JsonValueKind.Null &&
              !processAlive,
            "A04 proof must end with a clean owned stop");
        Check(helperStopCalls == 1, "A04 proof must issue exactly one helper stop");

        return JsonSerializer.SerializeToElement(new
        {
            ok = true,
            acceptanceCase = "A04",
            scope = "isolated OverviewLifecycleService Launch-spam/status-refresh concurrency; no real game process",
            launchSpamAttempts = spamCount,
            launchSpamRejected = launchCodes.Count(code => code == "GAME_OPERATION_IN_PROGRESS"),
            refreshDuringLaunch = refreshCount,
            refreshAllSameStartingSession = true,
            refreshBatchElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            helperStartCalls,
            helperStopCalls,
            uniqueSuccessfulSession = primarySession,
            staleSuccessObserved = false,
            duplicateHelperStartObserved = helperStartCalls != 1,
            finalPhase = stopped.GetProperty("phase").GetString(),
            finalProcessAlive = processAlive,
        });
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

    private static async Task<JsonElement> RefreshAsync(OverviewLifecycleService lifecycle, JsonElement payload)
    {
        object? status = await lifecycle.InvokeAsync("profile_instance_status", payload, CancellationToken.None)
            .ConfigureAwait(false);
        return StatusElement(status);
    }

    private static JsonElement StatusElement(object? value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static void RequireStartingStatus(JsonElement status, string session, string label)
    {
        Check(status.GetProperty("phase").GetString() == "starting" &&
              status.GetProperty("connectionState").GetString() == "starting" &&
              status.GetProperty("instanceId").GetString() == session &&
              status.GetProperty("pid").ValueKind == JsonValueKind.Null,
            label);
    }

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

    private static byte[] Heartbeat(string session, string challenge, int gamePid)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = "profile-a04-launch-spam",
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
            throw new InvalidOperationException("Overview A04 launch-spam check failed: " + message);
    }
}
