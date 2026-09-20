using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class OverviewProcessOwnershipChecks
{
    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-a05-" + Guid.NewGuid().ToString("N"));
        string selectedRoot = Path.Combine(root, "selected");
        string foreignRoot = Path.Combine(root, "foreign");
        Directory.CreateDirectory(Path.Combine(selectedRoot, "Game"));
        Directory.CreateDirectory(Path.Combine(foreignRoot, "Game"));
        string systemCmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        string selectedGame = Path.Combine(selectedRoot, "Game", "LastWar.exe");
        string foreignGame = Path.Combine(foreignRoot, "Game", "LastWar.exe");
        File.Copy(systemCmd, selectedGame, overwrite: true);
        File.Copy(systemCmd, foreignGame, overwrite: true);

        Process? foreignProcess = null;
        Process? managedProcess = null;
        Process? unmanagedSelectedProcess = null;
        int helperStartCalls = 0;
        int helperStopCalls = 0;
        string? activeSession = null;
        string? activeChallenge = null;

        try
        {
            foreignProcess = StartDummyLastWar(foreignGame);
            int foreignPid = foreignProcess.Id;
            string foreignStartedAt = ProcessStartedAtUtc(foreignProcess);
            Check(ProcessPathEquals(foreignProcess, foreignGame), "foreign dummy process path must match fixture executable");

            var hooks = new OverviewLifecycleTestHooks
            {
                RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                RunHelperAsync = async (invocation, cancellationToken) =>
                {
                    if (invocation.Operation == "start")
                    {
                        Interlocked.Increment(ref helperStartCalls);
                        Check(managedProcess is null || managedProcess.HasExited,
                            "helper must not create a second managed process while one is active");
                        managedProcess = StartDummyLastWar(selectedGame);
                        activeSession = invocation.SessionId;
                        activeChallenge = invocation.Challenge;
                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                        return StartResult(invocation, managedProcess, selectedGame);
                    }

                    Interlocked.Increment(ref helperStopCalls);
                    Process stopTarget = managedProcess ?? throw new InvalidOperationException(
                        "stop requires the managed fixture process");
                    Check(invocation.GamePid == stopTarget.Id,
                        "stop must target the exact managed PID returned by start");
                    Check(string.Equals(Path.GetFullPath(invocation.GamePath!), Path.GetFullPath(selectedGame), StringComparison.OrdinalIgnoreCase),
                        "stop must target the exact selected-root executable path");
                    Check(invocation.GameStartedAtUtc == ProcessStartedAtUtc(stopTarget),
                        "stop must retain the exact managed process creation identity");
                    int stoppedPid = stopTarget.Id;
                    string stoppedStartedAt = ProcessStartedAtUtc(stopTarget);
                    KillProcessTree(stopTarget);
                    return StopResult(invocation, selectedGame, stoppedPid, stoppedStartedAt);
                },
                ReadAllBytes = _ => Heartbeat(activeSession!, activeChallenge!, managedProcess!.Id),
                WriteLease = (_, _, _) => { },
                DeleteFile = _ => { },
            };

            using var managedLifecycle = new OverviewLifecycleService(
                "profile-a05-process-ownership",
                selectedRoot,
                helperPath: Path.Combine(root, "fake-helper.py"),
                requireCurrentClientEvidence: false,
                testHooks: hooks,
                startRecoveryMonitor: false);
            JsonElement empty = JsonSerializer.SerializeToElement(new { });

            JsonElement stoppedWithForeign = StatusElement(
                await managedLifecycle.InvokeAsync("profile_instance_status", empty, CancellationToken.None));
            Check(stoppedWithForeign.GetProperty("phase").GetString() == "stopped" &&
                  stoppedWithForeign.GetProperty("pid").ValueKind == JsonValueKind.Null,
                "foreign LastWar process must not be classified as selected-root unmanaged ownership");
            Check(IsAlive(foreignProcess), "foreign process must remain alive after selected-root status discovery");

            JsonElement started = StatusElement(
                await managedLifecycle.InvokeAsync("profile_instance_start", empty, CancellationToken.None));
            string managedSession = started.GetProperty("instanceId").GetString()!;
            int managedPid = started.GetProperty("pid").GetInt32();
            Process ownedProcess = managedProcess ?? throw new InvalidOperationException(
                "managed Start returned without a fixture process");
            Check(started.GetProperty("phase").GetString() == "running" &&
                  started.GetProperty("connectionState").GetString() == "connected" &&
                  managedPid == ownedProcess.Id,
                "managed selected-root process must reach running/connected with exact real PID identity");
            Check(ProcessPathEquals(ownedProcess, selectedGame),
                "managed process must execute from the selected installation path");
            Check(IsAlive(foreignProcess),
                "foreign process must remain alive while the selected-root managed session is running");

            string duplicateCode = await CaptureStartErrorAsync(managedLifecycle, empty);
            Check(duplicateCode == "GAME_RUNNING", "duplicate start with owned process must report GAME_RUNNING");
            Check(helperStartCalls == 1, "duplicate managed Start must not invoke another helper");
            Check(IsAlive(ownedProcess) && IsAlive(foreignProcess),
                "duplicate Start rejection must not terminate managed or unrelated processes");

            JsonElement stopPayload = JsonSerializer.SerializeToElement(new { instanceId = managedSession });
            JsonElement stopped = StatusElement(
                await managedLifecycle.InvokeAsync("profile_instance_stop", stopPayload, CancellationToken.None));
            Check(stopped.GetProperty("phase").GetString() == "stopped" &&
                  stopped.GetProperty("connectionState").GetString() == "offline",
                "managed Stop must return lifecycle to stopped/offline");
            Check(!IsAlive(ownedProcess), "managed Stop must terminate the exact owned selected-root process");
            Check(IsAlive(foreignProcess), "managed Stop must preserve unrelated LastWar process");
            Check(helperStartCalls == 1 && helperStopCalls == 1,
                "managed lifecycle must perform exactly one helper Start and Stop");

            unmanagedSelectedProcess = StartDummyLastWar(selectedGame);
            int unmanagedPid = unmanagedSelectedProcess.Id;
            Check(IsAlive(unmanagedSelectedProcess) && IsAlive(foreignProcess),
                "selected-root unmanaged and foreign fixtures must both be alive before conflict test");

            using var unmanagedLifecycle = new OverviewLifecycleService(
                "profile-a05-unmanaged-conflict",
                selectedRoot,
                helperPath: Path.Combine(root, "fake-helper.py"),
                requireCurrentClientEvidence: false,
                testHooks: new OverviewLifecycleTestHooks
                {
                    RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
                    RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
                    RunHelperAsync = (_, _) => throw new InvalidOperationException(
                        "helper must never run while selected-root unmanaged process exists"),
                    ReadAllBytes = _ => throw new FileNotFoundException(),
                    WriteLease = (_, _, _) => { },
                    DeleteFile = _ => { },
                },
                startRecoveryMonitor: false);

            JsonElement unmanagedStatus = StatusElement(
                await unmanagedLifecycle.InvokeAsync("profile_instance_status", empty, CancellationToken.None));
            Check(unmanagedStatus.GetProperty("phase").GetString() == "error" &&
                  unmanagedStatus.GetProperty("pid").GetInt32() == unmanagedPid &&
                  unmanagedStatus.GetProperty("instanceId").ValueKind == JsonValueKind.Null &&
                  unmanagedStatus.GetProperty("connectionState").GetString() == "error" &&
                  unmanagedStatus.GetProperty("error").GetString() == "UNMANAGED_GAME_RUNNING",
                "selected-root unmanaged process must be reported with concrete conflict status and exact PID");

            string unmanagedStartCode = await CaptureStartErrorAsync(unmanagedLifecycle, empty);
            Check(unmanagedStartCode == "UNMANAGED_GAME_RUNNING",
                "selected-root unmanaged process must block Start with UNMANAGED_GAME_RUNNING");
            Check(IsAlive(unmanagedSelectedProcess),
                "unmanaged selected-root process must be preserved after rejected Start");
            Check(IsAlive(foreignProcess),
                "unrelated foreign process must also be preserved during selected-root conflict");

            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                acceptanceCase = "A05",
                scope = "real Windows LastWar process-name/path/creation-identity ownership matrix using temporary dummy executables; official game never launched",
                managed = new
                {
                    foreignPid,
                    foreignStartedAt,
                    selectedPid = managedPid,
                    helperStartCalls,
                    helperStopCalls,
                    duplicateStartCode = duplicateCode,
                    foreignPreservedBeforeStart = true,
                    foreignPreservedWhileManagedRunning = true,
                    foreignPreservedAfterManagedStop = true,
                    selectedOwnedStopped = true,
                },
                unmanaged = new
                {
                    selectedPid = unmanagedPid,
                    statusError = unmanagedStatus.GetProperty("error").GetString(),
                    startError = unmanagedStartCode,
                    selectedProcessPreserved = IsAlive(unmanagedSelectedProcess),
                    unrelatedProcessPreserved = IsAlive(foreignProcess),
                },
            });
        }
        finally
        {
            KillProcessTree(managedProcess);
            KillProcessTree(unmanagedSelectedProcess);
            KillProcessTree(foreignProcess);
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static Process StartDummyLastWar(string path)
    {
        var start = new ProcessStartInfo
        {
            FileName = path,
            Arguments = "/c ping 127.0.0.1 -n 60 >nul",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(path)!,
        };
        Process process = Process.Start(start) ?? throw new InvalidOperationException("could not start dummy LastWar fixture");
        for (int i = 0; i < 20; i++)
        {
            if (!process.HasExited && ProcessPathEquals(process, path)) return process;
            Thread.Sleep(25);
        }
        KillProcessTree(process);
        throw new InvalidOperationException("dummy LastWar fixture did not become visible with the expected path");
    }

    private static string ProcessStartedAtUtc(Process process) =>
        process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static bool ProcessPathEquals(Process process, string expected)
    {
        try
        {
            if (process.HasExited) return false;
            string? actual = process.MainModule?.FileName;
            return actual is not null && string.Equals(
                Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAlive(Process? process)
    {
        if (process is null) return false;
        try { return !process.HasExited; }
        catch { return false; }
    }

    private static void KillProcessTree(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch { }
        finally { process.Dispose(); }
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

    private static JsonElement StatusElement(object? value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static JsonElement StartResult(
        OverviewHelperInvocation invocation,
        Process process,
        string gamePath)
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
            gamePid = process.Id,
            launcherPid = process.Id + 100000,
            gamePath,
            gameStartedAtUtc = ProcessStartedAtUtc(process),
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
                gamePid = process.Id,
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
            close = new { method = "A05.real-dummy.exact-owned-close", accepted = true, processExited = true, alreadyExited = false },
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
            profileId = "profile-a05-process-ownership",
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
            throw new InvalidOperationException("Overview A05 process ownership check failed: " + message);
    }
}
