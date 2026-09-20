using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

internal static class OverviewCloseTimingChecks
{
    private const int ServerId = 2212;

    internal static async Task<JsonElement> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-overview-a06-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            JsonElement frontend = VerifyRecoveredFrontendContract();
            JsonElement idle = await RunIdleAsync(Path.Combine(root, "idle"));
            JsonElement launching = await RunLaunchingAsync(Path.Combine(root, "launching"));
            JsonElement scanning = await RunScanningAsync(Path.Combine(root, "scanning"));
            JsonElement recovering = await RunRecoveringAsync(Path.Combine(root, "recovering"));
            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                acceptanceCase = "A06",
                scope = "recovered Home Close reachability plus lifecycle/scan terminal outcomes; no real game/process",
                frontend,
                idle,
                launching,
                scanning,
                recovering,
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static JsonElement VerifyRecoveredFrontendContract()
    {
        string repo = FindRepoRoot();
        string assetRoot = Path.Combine(repo, "src", "LWBridge.Desktop", "WebUi", "assets");
        string indexPath = Directory.GetFiles(assetRoot, "index-*.js").Single();
        string source = File.ReadAllText(indexPath);
        const string gate = "canStop:e&&t&&(n||i)&&!a&&!o";
        Check(source.Contains(gate, StringComparison.Ordinal),
            "recovered Home gate must disable Close while idle/launching and enable it for owned/recovering states");

        int handlerStart = source.IndexOf("async function Ut(){", StringComparison.Ordinal);
        Check(handlerStart >= 0, "recovered Home Close handler must remain present");
        int handlerEnd = source.IndexOf("async function Wt(){", handlerStart, StringComparison.Ordinal);
        Check(handlerEnd > handlerStart, "recovered Home Close handler boundary must remain identifiable");
        string handler = source[handlerStart..handlerEnd];
        Check(handler.Contains("await i(u.selectedProfileId)", StringComparison.Ordinal) &&
              handler.Contains("await te(u.selectedProfileId,e.instanceId)", StringComparison.Ordinal),
            "Home Close must refresh exact instance ownership then invoke profile_instance_stop");
        Check(!handler.Contains("map_scan_stop", StringComparison.Ordinal) &&
              !handler.Contains("stopMapScan", StringComparison.Ordinal),
            "Home Close must not invent an explicit Map Stop command");

        return JsonSerializer.SerializeToElement(new
        {
            closeGate = gate,
            idleCloseReachable = false,
            launchingCloseReachable = false,
            ownedCloseReachable = true,
            recoveringCloseReachable = true,
            homeCloseCallsMapScanStop = false,
        });
    }

    private static async Task<JsonElement> RunIdleAsync(string root)
    {
        Directory.CreateDirectory(root);
        using var lifecycle = new OverviewLifecycleService(
            "profile-a06-idle", root,
            helperPath: Path.Combine(root, "fake.py"),
            requireCurrentClientEvidence: false,
            testHooks: BaseHooks((_, _) => throw new InvalidOperationException("idle helper must not run")),
            startRecoveryMonitor: false);
        string code = await CaptureStopErrorAsync(lifecycle, "idle-none");
        JsonElement status = Status(lifecycle.CreateInstanceStatus());
        Check(code == "INSTANCE_NOT_OWNED", "idle direct Stop must fail closed as INSTANCE_NOT_OWNED");
        Check(status.GetProperty("phase").GetString() == "stopped" &&
              status.GetProperty("connectionState").GetString() == "offline" &&
              status.GetProperty("pid").ValueKind == JsonValueKind.Null,
            "idle Stop rejection must preserve stopped/offline/no-PID state");
        return JsonSerializer.SerializeToElement(new
        {
            uiCloseReachable = false,
            directCommandError = code,
            finalPhase = status.GetProperty("phase").GetString(),
            finalConnection = status.GetProperty("connectionState").GetString(),
        });
    }

    private static async Task<JsonElement> RunLaunchingAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int pid = 49101;
        const string startedAt = "2026-09-20T10:30:00.0000000Z";
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int startCalls = 0;
        int stopCalls = 0;

        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(c => c with { ProfileId = "profile-a06-launching", AutoReconnect = true });
        var hooks = BaseHooks(
            async (invocation, token) =>
            {
                if (invocation.Operation == "start")
                {
                    startCalls++;
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    entered.TrySetResult();
                    await release.Task.WaitAsync(token).ConfigureAwait(false);
                    processAlive = true;
                    return StartResult(invocation, gamePath, pid, startedAt);
                }
                stopCalls++;
                processAlive = false;
                return StopResult(invocation, gamePath, pid, startedAt);
            },
            processMatches: (p, path, created) => processAlive && p == pid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            readAllBytes: _ => Heartbeat("profile-a06-launching", session!, challenge!, pid, true, true, false));

        using var lifecycle = new OverviewLifecycleService(
            "profile-a06-launching", root,
            helperPath: Path.Combine(root, "fake.py"), requireCurrentClientEvidence: false,
            config: config, testHooks: hooks, startRecoveryMonitor: false);
        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        Task<object?> startTask = lifecycle.InvokeAsync("profile_instance_start", empty, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        JsonElement during = Status(lifecycle.CreateInstanceStatus());
        string inProgressCode = await CaptureStopErrorAsync(lifecycle, during.GetProperty("instanceId").GetString()!);
        Check(inProgressCode == "GAME_OPERATION_IN_PROGRESS" && startCalls == 1 && stopCalls == 0,
            "direct Stop during admitted Start must reject without disturbing the one Start");
        release.TrySetResult();
        JsonElement started = Status(await startTask.ConfigureAwait(false));
        string ownedSession = started.GetProperty("instanceId").GetString()!;
        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = ownedSession }), CancellationToken.None);
        JsonElement stopped = Status(lifecycle.CreateInstanceStatus());
        Check(startCalls == 1 && stopCalls == 1 && !processAlive,
            "launching branch must permit exact Stop after original Start reaches ownership");
        Check(!config.Snapshot.GameDesiredRunning && stopped.GetProperty("phase").GetString() == "stopped",
            "post-launch intentional Stop must clear desired-running and end stopped");
        return JsonSerializer.SerializeToElement(new
        {
            uiCloseReachable = false,
            directCommandDuringStart = inProgressCode,
            originalStartCompleted = true,
            exactStopCalls = stopCalls,
            finalDesiredRunning = config.Snapshot.GameDesiredRunning,
            finalPhase = stopped.GetProperty("phase").GetString(),
        });
    }

    private static async Task<JsonElement> RunScanningAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        string dbPath = Path.Combine(root, "map.db");
        const int pid = 49201;
        const string startedAt = "2026-09-20T10:40:00.0000000Z";
        bool processAlive = false;
        string? session = null;
        string? challenge = null;
        int lifecycleStarts = 0;
        int lifecycleStops = 0;
        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(c => c with { ProfileId = "profile-a06-scanning", AutoReconnect = true });
        var hooks = BaseHooks(
            (invocation, _) =>
            {
                if (invocation.Operation == "start")
                {
                    lifecycleStarts++;
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    processAlive = true;
                    return Task.FromResult(StartResult(invocation, gamePath, pid, startedAt));
                }
                lifecycleStops++;
                processAlive = false;
                return Task.FromResult(StopResult(invocation, gamePath, pid, startedAt));
            },
            processMatches: (p, path, created) => processAlive && p == pid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            readAllBytes: _ => Heartbeat("profile-a06-scanning", session!, challenge!, pid, true, true, false));
        using var lifecycle = new OverviewLifecycleService(
            "profile-a06-scanning", root,
            helperPath: Path.Combine(root, "fake.py"), requireCurrentClientEvidence: false,
            config: config, testHooks: hooks, startRecoveryMonitor: false);
        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        JsonElement running = Status(await lifecycle.InvokeAsync("profile_instance_start", empty, CancellationToken.None));
        string ownedSession = running.GetProperty("instanceId").GetString()!;

        using var store = new MapDataStore(dbPath);
        var baseline = new MapStoredRecord(
            "city", ServerId, "baseline", 1, "baseline-uuid", "Baseline", null,
            1, null, 100, null, null, 1, "{\"serverId\":2212,\"ownerName\":\"Baseline\"}");
        store.UpsertRecord(baseline);
        var lossSource = new CloseLossSource(() => processAlive);
        var scan = new ManualMapScanCommandService(
            store,
            _ => Task.FromResult(new CurrentClientMapContext(ServerId, 1, 21, 1, 10, 0, ownedSession)),
            lossSource,
            getLiveServerId: () => processAlive ? ServerId : null);
        JsonElement scanPayload = JsonSerializer.SerializeToElement(new
        {
            profileId = "profile-a06-scanning",
            selectedTypes = new[] { "city" },
        });
        JsonElement scanStart = Status(await scan.InvokeAsync("map_scan_start", scanPayload, CancellationToken.None));
        string runId = scanStart.GetProperty("scanRunId").GetString()!;
        await lossSource.SecondBlockEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check(store.ReadScanBlockCheckpointsForTest(runId).Count == 1,
            "scan must checkpoint its first successful block before Home Close");

        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = ownedSession }), CancellationToken.None);
        lossSource.ReleaseAfterClose();
        JsonElement terminal = await WaitForScanTerminalAsync(scan);
        Check(terminal.GetProperty("phase").GetString() == "error" &&
              terminal.GetProperty("isReading").GetBoolean() == false &&
              terminal.GetProperty("failedBlocks").GetInt32() == 1,
            "Home Close during scan must terminate the dependent scan truthfully as incomplete/error");
        Check(terminal.GetProperty("lastError").GetString() == "direct map scan contains failed batches",
            "scan loss must use recovered incomplete-scan terminal error");

        IReadOnlyList<MapScanBlockCheckpoint> checkpoints = store.ReadScanBlockCheckpointsForTest(runId);
        Check(checkpoints.Count == 2 && checkpoints[0].Status == "completed" &&
              checkpoints[1].Status == "failed" && checkpoints[1].Attempts == 2,
            "successful checkpoint must survive and the lost-session block must record bounded failure attempts");
        IReadOnlyList<MapStoredRecord> published = store.ReadRecords("city", ServerId);
        Check(published.Count == 1 && published[0].RecordKey == "baseline",
            "incomplete scan must not replace the previously published dataset with staged partial data");
        (string runStatus, string? runError) = ReadRunTerminal(dbPath, runId);
        Check(runStatus == "failed" && runError == "direct map scan contains failed batches",
            "durable scan run must be terminal failed, not completed/published");
        Check(lifecycleStops == 1 && !processAlive && !config.Snapshot.GameDesiredRunning,
            "Home Close must exactly stop/restore the owned game and clear desired-running during scan loss");
        int startsAfterClose = lifecycleStarts;
        await lifecycle.RunRecoveryObservationForTestAsync();
        await lifecycle.RunRecoveryObservationForTestAsync();
        Check(lifecycleStarts == startsAfterClose,
            "intentional Home Close during scan must not automatically restart the game");

        scan.Close();
        return JsonSerializer.SerializeToElement(new
        {
            uiCloseReachable = true,
            homeCloseCallsMapScanStop = false,
            scanTerminalPhase = terminal.GetProperty("phase").GetString(),
            scanTerminalError = terminal.GetProperty("lastError").GetString(),
            durableRunStatus = runStatus,
            checkpointStatuses = checkpoints.Select(x => new { x.BlockIndex, x.Status, x.Attempts }).ToArray(),
            publishedRecordKeys = published.Select(x => x.RecordKey).ToArray(),
            lifecycleStopCalls = lifecycleStops,
            finalDesiredRunning = config.Snapshot.GameDesiredRunning,
            automaticRestartAfterClose = lifecycleStarts != startsAfterClose,
        });
    }

    private static async Task<JsonElement> RunRecoveringAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "Game"));
        string gamePath = Path.Combine(root, "Game", "LastWar.exe");
        const int pid = 49301;
        const string startedAt = "2026-09-20T10:50:00.0000000Z";
        bool processAlive = false;
        bool recoveryRequested = false;
        string? session = null;
        string? challenge = null;
        int startCalls = 0;
        int stopCalls = 0;
        long clock = 0;
        var config = new LocalConfigStore(Path.Combine(root, "config"));
        config.Update(c => c with { ProfileId = "profile-a06-recovering", AutoReconnect = true });
        var hooks = BaseHooks(
            (invocation, _) =>
            {
                if (invocation.Operation == "start")
                {
                    startCalls++;
                    session = invocation.SessionId;
                    challenge = invocation.Challenge;
                    processAlive = true;
                    return Task.FromResult(StartResult(invocation, gamePath, pid, startedAt));
                }
                stopCalls++;
                processAlive = false;
                return Task.FromResult(StopResult(invocation, gamePath, pid, startedAt));
            },
            processMatches: (p, path, created) => processAlive && p == pid && created == startedAt &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(gamePath), StringComparison.OrdinalIgnoreCase),
            readAllBytes: _ => Heartbeat("profile-a06-recovering", session!, challenge!, pid,
                ready: true, healthy: !recoveryRequested, recoveryConfirmed: recoveryRequested),
            monotonicMilliseconds: () => clock,
            updateProcessRunning: () => false,
            processHung: (_, _) => false);
        using var lifecycle = new OverviewLifecycleService(
            "profile-a06-recovering", root,
            helperPath: Path.Combine(root, "fake.py"), requireCurrentClientEvidence: false,
            config: config, testHooks: hooks, startRecoveryMonitor: false);
        JsonElement running = Status(await lifecycle.InvokeAsync("profile_instance_start",
            JsonSerializer.SerializeToElement(new { }), CancellationToken.None));
        string ownedSession = running.GetProperty("instanceId").GetString()!;
        recoveryRequested = true;
        clock = 1_000;
        await lifecycle.RunRecoveryObservationForTestAsync();
        Check(lifecycle.CurrentRecoveryStatus.State == "waiting" &&
              lifecycle.CurrentRecoveryStatus.Reason == "disconnect",
            "confirmed unhealthy recovery request must enter waiting while exact owned process remains active");

        await lifecycle.InvokeAsync("profile_instance_stop",
            JsonSerializer.SerializeToElement(new { instanceId = ownedSession }), CancellationToken.None);
        Check(lifecycle.CurrentRecoveryStatus.State == "idle" && !config.Snapshot.GameDesiredRunning &&
              stopCalls == 1 && !processAlive,
            "intentional Close while recovering must cancel recovery intent and restore/stop exact owned game");
        int startsAfterClose = startCalls;
        for (int i = 0; i < 4; i++)
        {
            clock += 300_000;
            await lifecycle.RunRecoveryObservationForTestAsync();
        }
        Check(startCalls == startsAfterClose,
            "intentional recovering Close must suppress every later automatic restart");
        JsonElement stopped = Status(lifecycle.CreateInstanceStatus());
        return JsonSerializer.SerializeToElement(new
        {
            uiCloseReachable = true,
            recoveryStateBeforeClose = "waiting",
            recoveryStateAfterClose = lifecycle.CurrentRecoveryStatus.State,
            exactStopCalls = stopCalls,
            finalDesiredRunning = config.Snapshot.GameDesiredRunning,
            futureRecoveryStarts = startCalls - startsAfterClose,
            finalPhase = stopped.GetProperty("phase").GetString(),
        });
    }

    private static OverviewLifecycleTestHooks BaseHooks(
        Func<OverviewHelperInvocation, CancellationToken, Task<JsonElement>> runHelper,
        Func<int, string, string?, bool>? processMatches = null,
        Func<string, byte[]>? readAllBytes = null,
        Func<long>? monotonicMilliseconds = null,
        Func<bool>? updateProcessRunning = null,
        Func<int, string, bool>? processHung = null) => new()
    {
        RunOfficialRecoverAsync = (_, _) => Task.CompletedTask,
        RunOfficialSettleAsync = (_, _) => Task.CompletedTask,
        RunHelperAsync = runHelper,
        ProcessMatches = processMatches,
        ReadAllBytes = readAllBytes,
        WriteLease = (_, _, _) => { },
        DeleteFile = _ => { },
        UtcNow = () => DateTimeOffset.UtcNow,
        MonotonicMilliseconds = monotonicMilliseconds,
        UpdateProcessRunning = updateProcessRunning,
        ProcessHung = processHung,
    };

    private static async Task<string> CaptureStopErrorAsync(OverviewLifecycleService lifecycle, string instanceId)
    {
        try
        {
            await lifecycle.InvokeAsync("profile_instance_stop",
                JsonSerializer.SerializeToElement(new { instanceId }), CancellationToken.None).ConfigureAwait(false);
            return "UNEXPECTED_SUCCESS";
        }
        catch (BridgeCommandException error) { return error.Code; }
    }

    private static async Task<JsonElement> WaitForScanTerminalAsync(ManualMapScanCommandService scan)
    {
        for (int i = 0; i < 500; i++)
        {
            JsonElement status = Status(scan.CreateStatus());
            if (!status.GetProperty("isReading").GetBoolean()) return status;
            await Task.Delay(10).ConfigureAwait(false);
        }
        throw new TimeoutException("A06 scan did not reach terminal state");
    }

    private static (string Status, string? Error) ReadRunTerminal(string dbPath, string runId)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Cache=Private");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT status,error FROM scan_runs WHERE id=$run";
        command.Parameters.AddWithValue("$run", runId);
        using SqliteDataReader reader = command.ExecuteReader();
        Check(reader.Read(), "scan run must remain durably present after incomplete scan");
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static JsonElement StartResult(OverviewHelperInvocation invocation, string path, int pid, string startedAt)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(invocation.Challenge!))).ToLowerInvariant();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToElement(new
        {
            ok = true, mode = "overview_install_launch_ready_deferred_restore",
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = invocation.ProfileId, sessionId = invocation.SessionId,
            challengeSha256 = hash, gamePid = pid, launcherPid = pid + 10000,
            gamePath = path, gameStartedAtUtc = startedAt, gameRunning = true,
            installedFilesChanged = true,
            restore = new { restored = false, deferred = true, stage = "active_ready_deferred_restore" },
            ready = new
            {
                schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
                profileId = invocation.ProfileId, sessionId = invocation.SessionId,
                challenge = invocation.Challenge, gamePid = pid, ready = true,
                messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
                readyAt = now, updatedAt = now,
            },
        });
    }

    private static JsonElement StopResult(OverviewHelperInvocation invocation, string path, int pid, string startedAt) =>
        JsonSerializer.SerializeToElement(new
        {
            ok = true, mode = "overview_exact_pid_close_restore",
            bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId = invocation.ProfileId, sessionId = invocation.SessionId,
            gamePid = pid, gamePath = path, gameStartedAtUtc = startedAt,
            close = new { method = "synthetic", accepted = true, processExited = true, alreadyExited = false },
            restore = new { restored = true }, gameRunning = false, installedFilesChanged = false,
        });

    private static byte[] Heartbeat(
        string profileId, string session, string challenge, int pid,
        bool ready, bool healthy, bool recoveryConfirmed)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, bridgeVersion = OverviewLifecycleService.BridgeVersion,
            profileId, sessionId = session, challenge, gamePid = pid, updatedAt = now,
            ready, messageVisible = true, messageText = OverviewLifecycleService.ReadyMessage,
            gameStateObserved = true, gameReady = healthy, loggedIn = healthy,
            connected = healthy, connecting = false,
            gameUid = healthy ? "a06-test" : "", serverId = healthy ? ServerId : 0, worldPos = healthy ? 12345 : 0,
            recoveryObserved = recoveryConfirmed, recoveryConfirmed,
            recoveryAmbiguous = false, recoveryReason = recoveryConfirmed ? "disconnect" : null,
            recoveryUpdateDetected = false,
        });
    }

    private sealed class CloseLossSource : IMapScanBlockSource
    {
        private readonly Func<bool> processAlive;
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        internal TaskCompletionSource SecondBlockEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CloseLossSource(Func<bool> processAlive) => this.processAlive = processAlive;
        internal void ReleaseAfterClose() => release.TrySetResult();

        public async Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                Check(processAlive(), "first scan checkpoint requires the owned game to be alive");
                var staged = new MapStoredRecord(
                    "city", request.ServerId, "staged-new", 2, "staged-uuid", "Staged", null,
                    2, null, 200, null, null, 2,
                    "{\"serverId\":2212,\"ownerName\":\"Staged\"}");
                return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, "{}", [staged]);
            }
            SecondBlockEntered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!processAlive())
                throw new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
            return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, "{}", []);
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "task.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LWBridge.Desktop", "WebUi")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("could not locate LW-Control repository root");
    }

    private static JsonElement Status(object? value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Overview A06 close timing check failed: " + message);
    }
}
