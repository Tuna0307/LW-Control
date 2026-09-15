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
