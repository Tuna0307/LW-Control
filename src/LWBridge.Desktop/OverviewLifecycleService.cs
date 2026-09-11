using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

// OVL-02/03/04 IMPLEMENTATION POLICY: the Overview lifecycle deliberately uses
// the independently proven current-v14 LuaEntry execution route.  It does not
// claim to reproduce the still-unrecovered original launch-proof/ticket or
// hello.ack protocol. READY requires a fresh, exact-session game-side response.
internal sealed class OverviewLifecycleTestHooks
{
    public Func<OverviewHelperInvocation, CancellationToken, Task<JsonElement>>? RunHelperAsync { get; init; }
    public Func<int, string, bool>? ProcessMatches { get; init; }
    public Func<string, byte[]>? ReadAllBytes { get; init; }
    public Action<string, string, string>? WriteLease { get; init; }
    public Action<string>? DeleteFile { get; init; }
}

internal sealed record OverviewHelperInvocation(
    string Operation,
    string ProfileId,
    string? SessionId,
    string? Challenge,
    int? GamePid,
    string? GamePath);

internal sealed class OverviewLifecycleService : INativeAsyncCommandService, IDisposable
{
    internal const string BridgeVersion = "lwbridge-overview-bridge-1";
    internal const string ReadyMessage = "LWbridge is running";
    private const string ExpectedPackageSha256 = "09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace";
    private const string ExpectedXluaSha256 = "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f";
    private const string ExpectedAssemblyCSharpSha256 = "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd";
    private static readonly TimeSpan HeartbeatFreshness = TimeSpan.FromSeconds(5);

    private readonly object stateGate = new();
    private readonly string helperPath;
    private readonly string? gameRoot;
    private readonly string profileId;
    private readonly string runtimeRoot;
    private readonly TimeSpan helperSupervisionTimeout;
    private readonly OverviewLifecycleTestHooks? testHooks;
    private readonly bool requireCurrentClientEvidence;
    private System.Threading.Timer? leaseTimer;
    private Process? activeHelperProcess;
    private bool closed;
    private string phase = "stopped";
    private string connectionState = "offline";
    private string? instanceId;
    private string? challenge;
    private int? gamePid;
    private int? launcherPid;
    private string? gamePath;
    private string? lastError;
    private long? readyAtUnix;

    public OverviewLifecycleService(
        string profileId,
        string? gameRoot,
        string? helperPath = null,
        TimeSpan? helperSupervisionTimeout = null,
        bool? requireCurrentClientEvidence = null,
        OverviewLifecycleTestHooks? testHooks = null)
    {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("profileId is required", nameof(profileId));
        this.profileId = profileId;
        this.gameRoot = string.IsNullOrWhiteSpace(gameRoot) ? null : Path.GetFullPath(gameRoot);
        this.helperPath = helperPath ?? Path.Combine(AppContext.BaseDirectory, "OverviewBridge", "run_overview_bridge.py");
        this.helperSupervisionTimeout = helperSupervisionTimeout ?? TimeSpan.FromSeconds(190);
        this.requireCurrentClientEvidence = requireCurrentClientEvidence ?? helperPath is null;
        this.testHooks = testHooks;
        runtimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "overview-bridge");
    }

    public bool CanHandle(string command) =>
        command is "profile_instance_start" or "profile_instance_stop" or "profile_instance_status";

    public bool IsReady
    {
        get
        {
            OwnedSnapshot? snapshot = GetOwnedSnapshot();
            return snapshot is not null && snapshot.Phase == "running" && IsSnapshotReady(snapshot);
        }
    }

    public string CurrentConnectionState
    {
        get
        {
            RefreshExitedOwnership();
            lock (stateGate)
            {
                if (phase == "running" && instanceId is not null)
                    return IsReady ? "connected" : "error";
                return connectionState;
            }
        }
    }

    public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken) => command switch
    {
        "profile_instance_start" => StartAsync(cancellationToken),
        "profile_instance_stop" => StopAsync(payload, cancellationToken),
        "profile_instance_status" => Task.FromResult<object?>(CreateInstanceStatus()),
        _ => throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", $"Overview lifecycle cannot handle '{command}'."),
    };

    public object CreateInstanceStatus()
    {
        RefreshExitedOwnership();
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        if (snapshot is null)
        {
            GameProcessIdentity? unmanaged = FindSelectedGameProcess();
            if (unmanaged is not null)
            {
                return new
                {
                    phase = "error",
                    pid = (int?)unmanaged.Pid,
                    instanceId = (string?)null,
                    connectionState = "error",
                    error = "UNMANAGED_GAME_RUNNING",
                };
            }
            lock (stateGate)
            {
                if (phase == "starting")
                    return new { phase, pid = (int?)null, instanceId, connectionState, error = lastError };
                if (phase == "error" && gamePid is null)
                    return new { phase, pid = (int?)null, instanceId, connectionState, error = lastError };
            }
            return new
            {
                phase = "stopped",
                pid = (int?)null,
                instanceId = (string?)null,
                connectionState = "offline",
                error = (string?)null,
            };
        }

        if (snapshot.Phase == "running" && !IsSnapshotReady(snapshot))
        {
            return new
            {
                phase = "error",
                pid = (int?)snapshot.GamePid,
                instanceId = snapshot.InstanceId,
                connectionState = "error",
                error = "BRIDGE_DISCONNECTED",
            };
        }
        return new
        {
            phase = snapshot.Phase,
            pid = (int?)snapshot.GamePid,
            instanceId = snapshot.InstanceId,
            connectionState = snapshot.ConnectionState,
            error = snapshot.Error,
        };
    }

    public void Close()
    {
        lock (stateGate) closed = true;
        StopLeaseTimer(deleteLease: true);
    }

    public void Dispose() => Close();

    private async Task<object?> StartAsync(CancellationToken cancellationToken)
    {
        if (gameRoot is null)
            throw new BridgeCommandException("GAME_ROOT_NOT_FOUND", "No validated Last War installation is selected.");
        if (!File.Exists(helperPath) && testHooks?.RunHelperAsync is null)
            throw new BridgeCommandException("OVERVIEW_HELPER_MISSING", "The Overview bridge helper was not deployed with LWBridge.Desktop.");

        lock (stateGate)
        {
            if (closed) throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            if (phase is "starting" or "stopping")
                throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS", "A game lifecycle operation is already in progress.");
            if (gamePid is not null)
                throw new BridgeCommandException("GAME_RUNNING", "The LWBridge-owned game is already running.");
        }
        if (FindSelectedGameProcess() is not null)
            throw new BridgeCommandException("UNMANAGED_GAME_RUNNING", "Close the game started outside this application first.");

        string newSession = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string newChallenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        lock (stateGate)
        {
            phase = "starting";
            connectionState = "starting";
            instanceId = newSession;
            challenge = newChallenge;
            lastError = null;
            readyAtUnix = null;
        }

        try
        {
            JsonElement helper = await RunHelperAsync(
                new OverviewHelperInvocation("start", profileId, newSession, newChallenge, null, null),
                cancellationToken).ConfigureAwait(false);
            OverviewStartResult start = ValidateStartResult(helper, profileId, newSession, newChallenge, gameRoot, requireCurrentClientEvidence);
            lock (stateGate)
            {
                if (closed)
                    throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge closed while the game was starting.");
                phase = "running";
                connectionState = "connected";
                instanceId = newSession;
                challenge = newChallenge;
                gamePid = start.GamePid;
                launcherPid = start.LauncherPid;
                gamePath = start.GamePath;
                readyAtUnix = start.ReadyAtUnix;
                lastError = null;
            }
            StartLeaseTimer();
            if (!IsReady)
                throw new BridgeCommandException("BRIDGE_START_TIMEOUT", "The game started, but the current Overview bridge response is not fresh.");
            return CreateInstanceStatus();
        }
        catch (BridgeCommandException)
        {
            lock (stateGate)
            {
                if (gamePid is null)
                {
                    phase = "error";
                    connectionState = "error";
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            lock (stateGate)
            {
                if (gamePid is null)
                {
                    phase = "error";
                    connectionState = "error";
                    lastError = ex.Message;
                }
            }
            string message = ex.Message;
            if (message.Contains("already running", StringComparison.OrdinalIgnoreCase))
                throw new BridgeCommandException("UNMANAGED_GAME_RUNNING", "Close the game started outside this application first.");
            throw new BridgeCommandException("LAUNCH_FAILED", "The Overview bridge launch failed.", new { error = message });
        }
    }

    private async Task<object?> StopAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        OwnedSnapshot snapshot;
        lock (stateGate)
        {
            if (closed) throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            if (phase is "starting" or "stopping")
                throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS", "A game lifecycle operation is already in progress.");
            if (gamePid is null || instanceId is null || challenge is null || gamePath is null)
                throw new BridgeCommandException("INSTANCE_NOT_OWNED", "No LWBridge-owned game instance is active.");
            if (!payload.TryGetProperty("instanceId", out JsonElement supplied) ||
                supplied.ValueKind != JsonValueKind.String ||
                !string.Equals(supplied.GetString(), instanceId, StringComparison.Ordinal))
                throw new BridgeCommandException("INSTANCE_NOT_OWNED", "The requested game instance is not owned by this LWBridge session.");
            phase = "stopping";
            connectionState = "recovering";
            snapshot = SnapshotLocked();
        }

        try
        {
            JsonElement result = await RunHelperAsync(
                new OverviewHelperInvocation("stop", profileId, snapshot.InstanceId, snapshot.Challenge, snapshot.GamePid, snapshot.GamePath),
                cancellationToken).ConfigureAwait(false);
            ValidateStopResult(result, snapshot.GamePid, snapshot.GamePath);
            StopLeaseTimer(deleteLease: true);
            ClearRuntimeSessionFiles();
            lock (stateGate)
            {
                phase = "stopped";
                connectionState = "offline";
                instanceId = null;
                challenge = null;
                gamePid = null;
                launcherPid = null;
                gamePath = null;
                lastError = null;
                readyAtUnix = null;
            }
            return CreateInstanceStatus();
        }
        catch (BridgeCommandException)
        {
            lock (stateGate)
            {
                phase = "error";
                connectionState = "error";
            }
            throw;
        }
        catch (Exception ex)
        {
            lock (stateGate)
            {
                phase = "error";
                connectionState = "error";
                lastError = ex.Message;
            }
            throw new BridgeCommandException("GAME_CLOSE_FAILED", "The LWBridge-owned game did not close cleanly.", new { error = ex.Message });
        }
    }

    private async Task<JsonElement> RunHelperAsync(OverviewHelperInvocation invocation, CancellationToken cancellationToken)
    {
        if (testHooks?.RunHelperAsync is { } testRunner)
            return await testRunner(invocation, cancellationToken).ConfigureAwait(false);

        var start = new ProcessStartInfo
        {
            FileName = "python",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(helperPath) ?? AppContext.BaseDirectory,
        };
        start.ArgumentList.Add(helperPath);
        start.ArgumentList.Add(invocation.Operation);
        if (gameRoot is not null)
        {
            start.ArgumentList.Add("--game-root");
            start.ArgumentList.Add(gameRoot);
        }
        if (invocation.Operation == "start")
        {
            start.ArgumentList.Add("--profile-id"); start.ArgumentList.Add(invocation.ProfileId);
            start.ArgumentList.Add("--session-id"); start.ArgumentList.Add(invocation.SessionId!);
            start.ArgumentList.Add("--challenge"); start.ArgumentList.Add(invocation.Challenge!);
            start.ArgumentList.Add("--timeout-seconds"); start.ArgumentList.Add("120");
        }
        else
        {
            start.ArgumentList.Add("--game-pid"); start.ArgumentList.Add(invocation.GamePid!.Value.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add("--game-path"); start.ArgumentList.Add(invocation.GamePath!);
        }

        Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Python Overview bridge helper could not be started.");
        lock (stateGate) activeHelperProcess = process;
        try
        {
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            Task completed = await Task.WhenAny(exitTask, Task.Delay(helperSupervisionTimeout, CancellationToken.None)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, exitTask))
                throw new TimeoutException($"Overview bridge helper exceeded {helperSupervisionTimeout.TotalSeconds:0.#} seconds; the helper retains cleanup ownership.");
            await exitTask.ConfigureAwait(false);
            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);
            JsonDocument document;
            try { document = JsonDocument.Parse(stdout.Trim()); }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Overview helper returned non-JSON output (exit {process.ExitCode}): {stderr.Trim()}", ex);
            }
            using (document)
            {
                JsonElement root = document.RootElement;
                if (process.ExitCode != 0 || !root.TryGetProperty("ok", out JsonElement ok) || ok.ValueKind != JsonValueKind.True)
                {
                    string error = root.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind == JsonValueKind.String
                        ? errorElement.GetString() ?? "unknown Overview helper error"
                        : stderr.Trim();
                    throw new InvalidOperationException(error);
                }
                return root.Clone();
            }
        }
        finally
        {
            lock (stateGate)
            {
                if (ReferenceEquals(activeHelperProcess, process)) activeHelperProcess = null;
            }
            process.Dispose();
        }
    }

    internal static OverviewStartResult ValidateStartResult(
        JsonElement root,
        string expectedProfileId,
        string expectedSessionId,
        string expectedChallenge,
        string expectedGameRoot,
        bool requireCurrentClientEvidence)
    {
        RequireString(root, "mode", "overview_install_launch_ready_restore");
        RequireString(root, "bridgeVersion", BridgeVersion);
        RequireString(root, "profileId", expectedProfileId);
        RequireString(root, "sessionId", expectedSessionId);
        string challengeHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expectedChallenge))).ToLowerInvariant();
        RequireString(root, "challengeSha256", challengeHash);
        int pid = RequirePositiveInt(root, "gamePid");
        int launcher = RequirePositiveInt(root, "launcherPid");
        string gamePath = RequiredString(root, "gamePath");
        string expectedPath = Path.GetFullPath(Path.Combine(expectedGameRoot, "Game", "LastWar.exe"));
        if (!PathEquals(gamePath, expectedPath))
            throw new InvalidDataException("Overview helper returned a different game executable path.");
        if (!root.TryGetProperty("gameRunning", out JsonElement running) || running.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Overview helper did not leave the owned game running.");
        if (!root.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Overview helper did not prove exact installed-file restoration.");
        if (!root.TryGetProperty("restore", out JsonElement restore) || restore.ValueKind != JsonValueKind.Object ||
            !restore.TryGetProperty("restored", out JsonElement restored) || restored.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Overview helper did not prove exact script restoration.");
        if (!root.TryGetProperty("ready", out JsonElement ready) || ready.ValueKind != JsonValueKind.Object ||
            !HeartbeatMatches(ready, expectedProfileId, expectedSessionId, expectedChallenge, pid, long.MaxValue, requireFreshness: false))
            throw new InvalidDataException("Overview helper did not return the exact same-session game-side readiness response.");
        if (!ready.TryGetProperty("readyAt", out JsonElement readyAtElement) || !readyAtElement.TryGetInt64(out long readyAt) || readyAt <= 0)
            throw new InvalidDataException("Overview readiness response is missing readyAt.");
        if (requireCurrentClientEvidence)
        {
            if (!root.TryGetProperty("currentClient", out JsonElement current) || current.ValueKind != JsonValueKind.Object ||
                !MatchesString(current, "packageSha256", ExpectedPackageSha256) ||
                !MatchesString(current, "xluaSha256", ExpectedXluaSha256) ||
                !MatchesString(current, "assemblyCSharpSha256", ExpectedAssemblyCSharpSha256))
                throw new InvalidDataException("Overview helper did not prove the supported current-client identity.");
        }
        return new(pid, launcher, Path.GetFullPath(gamePath), readyAt);
    }

    internal static void ValidateStopResult(JsonElement root, int expectedGamePid, string expectedGamePath)
    {
        RequireString(root, "mode", "overview_exact_pid_normal_close");
        RequireString(root, "bridgeVersion", BridgeVersion);
        if (RequirePositiveInt(root, "gamePid") != expectedGamePid)
            throw new InvalidDataException("Overview stop helper returned a different game PID.");
        string path = RequiredString(root, "gamePath");
        if (!PathEquals(path, expectedGamePath))
            throw new InvalidDataException("Overview stop helper returned a different game path.");
        if (!root.TryGetProperty("gameRunning", out JsonElement running) || running.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Overview stop helper did not prove owned game exit.");
        if (!root.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Overview stop helper reported changed installed files.");
        if (!root.TryGetProperty("close", out JsonElement close) || close.ValueKind != JsonValueKind.Object ||
            !close.TryGetProperty("accepted", out JsonElement accepted) || accepted.ValueKind != JsonValueKind.True ||
            !close.TryGetProperty("processExited", out JsonElement exited) || exited.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Overview stop helper did not prove accepted normal close and process exit.");
    }

    internal static bool HeartbeatMatches(
        JsonElement root,
        string expectedProfileId,
        string expectedSessionId,
        string expectedChallenge,
        int expectedGamePid,
        long nowUnix,
        bool requireFreshness = true)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "bridgeVersion", BridgeVersion) ||
            !MatchesString(root, "profileId", expectedProfileId) ||
            !MatchesString(root, "sessionId", expectedSessionId) ||
            !MatchesString(root, "challenge", expectedChallenge) ||
            !MatchesInt(root, "gamePid", expectedGamePid) ||
            !MatchesBool(root, "ready", true) ||
            !MatchesBool(root, "messageVisible", true) ||
            !MatchesString(root, "messageText", ReadyMessage))
            return false;
        if (!requireFreshness) return true;
        if (!root.TryGetProperty("updatedAt", out JsonElement updated) || !updated.TryGetInt64(out long timestamp))
            return false;
        long tolerance = (long)HeartbeatFreshness.TotalSeconds;
        return timestamp <= nowUnix + tolerance && nowUnix - timestamp <= tolerance;
    }

    private bool IsSnapshotReady(OwnedSnapshot snapshot)
    {
        if (!ProcessMatches(snapshot.GamePid, snapshot.GamePath)) return false;
        string heartbeatPath = Path.Combine(runtimeRoot, "heartbeat.json");
        try
        {
            byte[] bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(heartbeatPath);
            using JsonDocument heartbeat = JsonDocument.Parse(bytes);
            return HeartbeatMatches(
                heartbeat.RootElement,
                profileId,
                snapshot.InstanceId,
                snapshot.Challenge,
                snapshot.GamePid,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        catch
        {
            return false;
        }
    }

    private bool ProcessMatches(int pid, string expectedPath)
    {
        if (testHooks?.ProcessMatches is { } test) return test(pid, expectedPath);
        try
        {
            using Process process = Process.GetProcessById(pid);
            if (process.HasExited) return false;
            string? actual = process.MainModule?.FileName;
            return actual is not null && PathEquals(actual, expectedPath);
        }
        catch { return false; }
    }

    private GameProcessIdentity? FindSelectedGameProcess()
    {
        if (gameRoot is null) return null;
        string expected = Path.GetFullPath(Path.Combine(gameRoot, "Game", "LastWar.exe"));
        foreach (Process process in Process.GetProcessesByName("LastWar"))
        {
            try
            {
                string? actual = process.MainModule?.FileName;
                if (actual is not null && PathEquals(actual, expected))
                    return new(process.Id, Path.GetFullPath(actual));
            }
            catch { }
            finally { process.Dispose(); }
        }
        return null;
    }

    private void RefreshExitedOwnership()
    {
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        if (snapshot is null || snapshot.Phase is "starting" or "stopping") return;
        if (ProcessMatches(snapshot.GamePid, snapshot.GamePath)) return;
        StopLeaseTimer(deleteLease: true);
        lock (stateGate)
        {
            if (gamePid == snapshot.GamePid && instanceId == snapshot.InstanceId)
            {
                phase = "stopped";
                connectionState = "offline";
                instanceId = null;
                challenge = null;
                gamePid = null;
                launcherPid = null;
                gamePath = null;
                lastError = null;
                readyAtUnix = null;
            }
        }
    }

    private void StartLeaseTimer()
    {
        StopLeaseTimer(deleteLease: false);
        leaseTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                OwnedSnapshot? snapshot = GetOwnedSnapshot();
                if (snapshot is null || snapshot.Phase != "running") return;
                WriteLease(snapshot.InstanceId, snapshot.Challenge);
            }
            catch { }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void StopLeaseTimer(bool deleteLease)
    {
        System.Threading.Timer? timer = Interlocked.Exchange(ref leaseTimer, null);
        timer?.Dispose();
        if (deleteLease)
        {
            try { DeleteFile(Path.Combine(runtimeRoot, "lease.txt")); }
            catch { }
        }
    }

    private void WriteLease(string session, string nonce)
    {
        if (testHooks?.WriteLease is { } test)
        {
            test(session, nonce, runtimeRoot);
            return;
        }
        Directory.CreateDirectory(runtimeRoot);
        string path = Path.Combine(runtimeRoot, "lease.txt");
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        File.WriteAllText(temp,
            "schema=1\n" +
            $"bridgeVersion={BridgeVersion}\n" +
            $"sessionId={session}\n" +
            $"challenge={nonce}\n" +
            $"updatedAt={DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}\n");
        File.Move(temp, path, overwrite: true);
    }

    private void ClearRuntimeSessionFiles()
    {
        foreach (string name in new[] { "lease.txt", "control.txt", "ready.json", "heartbeat.json" })
        {
            try { DeleteFile(Path.Combine(runtimeRoot, name)); }
            catch { }
        }
    }

    private void DeleteFile(string path)
    {
        if (testHooks?.DeleteFile is { } test) { test(path); return; }
        File.Delete(path);
    }

    private OwnedSnapshot? GetOwnedSnapshot()
    {
        lock (stateGate)
        {
            if (gamePid is null || instanceId is null || challenge is null || gamePath is null) return null;
            return SnapshotLocked();
        }
    }

    private OwnedSnapshot SnapshotLocked() => new(
        phase, connectionState, instanceId!, challenge!, gamePid!.Value,
        launcherPid, gamePath!, lastError, readyAtUnix);

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Overview helper field '{name}' is missing or invalid.");
        return value.GetString()!;
    }

    private static int RequirePositiveInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int result) || result <= 0)
            throw new InvalidDataException($"Overview helper field '{name}' must be a positive integer.");
        return result;
    }

    private static void RequireString(JsonElement root, string name, string expected)
    {
        if (!MatchesString(root, name, expected))
            throw new InvalidDataException($"Overview helper field '{name}' did not match the expected value.");
    }

    private static bool MatchesString(JsonElement root, string name, string expected) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool MatchesInt(JsonElement root, string name, int expected) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) && result == expected;

    private static bool MatchesBool(JsonElement root, string name, bool expected) =>
        root.TryGetProperty(name, out JsonElement value) &&
        (expected ? value.ValueKind == JsonValueKind.True : value.ValueKind == JsonValueKind.False);

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed record OwnedSnapshot(
        string Phase,
        string ConnectionState,
        string InstanceId,
        string Challenge,
        int GamePid,
        int? LauncherPid,
        string GamePath,
        string? Error,
        long? ReadyAtUnix);

    private sealed record GameProcessIdentity(int Pid, string Path);
}

internal sealed record OverviewStartResult(int GamePid, int LauncherPid, string GamePath, long ReadyAtUnix);
