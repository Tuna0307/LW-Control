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
    public Func<int, string, string?, bool>? ProcessMatches { get; init; }
    public Func<string, byte[]>? ReadAllBytes { get; init; }
    public Action<string, string, string>? WriteLease { get; init; }
    public Action<string>? DeleteFile { get; init; }
    public Func<DateTimeOffset>? UtcNow { get; init; }
    public Func<long>? MonotonicMilliseconds { get; init; }
    public Func<bool>? UpdateProcessRunning { get; init; }
    public Func<string?>? UpdateActivityFingerprint { get; init; }
    public Func<CancellationToken, Task>? TerminateUpdateProcessesAsync { get; init; }
    public Func<int, string, bool>? ProcessHung { get; init; }
    public Func<int, string, string, CancellationToken, Task>? TerminateOwnedProcessAsync { get; init; }
    public Func<TimeSpan, CancellationToken, Task>? DelayAsync { get; init; }
}

internal sealed record OverviewHelperInvocation(
    string Operation,
    string ProfileId,
    string? SessionId,
    string? Challenge,
    int? GamePid,
    string? GamePath,
    string? GameStartedAtUtc);

internal sealed partial class OverviewLifecycleService : INativeAsyncCommandService, IDisposable
{
    internal const string BridgeVersion = "lwbridge-overview-bridge-1";
    internal const string ReadyMessage = "LWbridge is running";
    private const string ExpectedPackageSha256 = "09ddc4d1727bc0676ef6320db79814852cacc5c82b53551c703722052ebdbace";
    private const string ExpectedXluaSha256 = "21eb704afdb7e528f4b90fa1b90bf414c221b06ba990d625aaaaed31b292740f";
    private const string ExpectedAssemblyCSharpSha256 = "871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd";
    private static readonly TimeSpan HeartbeatFreshness = TimeSpan.FromSeconds(5);

    private readonly object stateGate = new();
    private readonly object leaseWriteGate = new();
    private readonly string helperPath;
    private string? gameRoot;
    private readonly string profileId;
    private readonly string runtimeRoot;
    private readonly string evidenceRoot;
    private readonly TimeSpan helperSupervisionTimeout;
    private readonly LocalConfigStore? config;
    private readonly OverviewLifecycleTestHooks? testHooks;
    private readonly bool requireCurrentClientEvidence;
    private readonly bool recoveryMonitorEnabled;
    private System.Threading.Timer? leaseTimer;
    private long leaseGeneration;
    private Process? activeHelperProcess;
    private bool closed;
    private string phase = "stopped";
    private string connectionState = "offline";
    private string? instanceId;
    private string? challenge;
    private int? gamePid;
    private int? launcherPid;
    private string? gamePath;
    private string? gameStartedAtUtc;
    private string? lastError;
    private long? readyAtUnix;
    private bool startupReconcileConsumed;
    private IReadOnlyList<OverviewStartupError> startupReconcileErrors = Array.Empty<OverviewStartupError>();

    public OverviewLifecycleService(
        string profileId,
        string? gameRoot,
        string? helperPath = null,
        TimeSpan? helperSupervisionTimeout = null,
        bool? requireCurrentClientEvidence = null,
        LocalConfigStore? config = null,
        OverviewLifecycleTestHooks? testHooks = null,
        bool startRecoveryMonitor = true)
    {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("profileId is required", nameof(profileId));
        this.profileId = profileId;
        this.gameRoot = string.IsNullOrWhiteSpace(gameRoot) ? null : Path.GetFullPath(gameRoot);
        this.helperPath = helperPath ?? Path.Combine(AppContext.BaseDirectory, "OverviewBridge", "run_overview_bridge.py");
        this.helperSupervisionTimeout = helperSupervisionTimeout ?? TimeSpan.FromSeconds(190);
        this.requireCurrentClientEvidence = requireCurrentClientEvidence ?? helperPath is null;
        this.config = config;
        this.testHooks = testHooks;
        recoveryMonitorEnabled = startRecoveryMonitor;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        runtimeRoot = Path.Combine(localAppData, "LWBridgeRebuild", "overview-bridge");
        evidenceRoot = Path.Combine(localAppData, "LWBridgeRebuild", "overview-evidence");
        if (startRecoveryMonitor) StartRecoveryMonitor();
    }

    public bool CanHandle(string command) =>
        command is "profile_instance_start" or "profile_instance_stop" or "profile_instance_status" or
            "profile_instances_reconcile" or "profile_instances_update_and_restart";

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

    public bool RepairRequired => TryGetRepairSnapshot(out _);

    // PM16-01 IMPLEMENTATION POLICY: a validated installation may replace the bound
    // lifecycle root only while no owned launch/close/recovery work is active and no
    // same-profile recovery journal remains. Re-selecting the same root
    // is a harmless persistence-only no-op.
    internal void RebindGameRoot(string selectedRoot, Action persistSelection)
    {
        if (string.IsNullOrWhiteSpace(selectedRoot))
            throw new ArgumentException("A validated game root is required.", nameof(selectedRoot));
        ArgumentNullException.ThrowIfNull(persistSelection);
        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedRoot));
        bool startRecoveryMonitor = false;

        lock (stateGate)
        {
            if (closed)
                throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            if (gameRoot is not null && PathEquals(gameRoot, normalized))
            {
                persistSelection();
                return;
            }
            if (phase is "starting" or "stopping" or "running" ||
                gamePid is not null || activeHelperProcess is not null || activeRecoveryCancellation is not null)
                throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS",
                    "The selected installation cannot change while an owned game lifecycle operation is active.");
            RecoveryJournalState journalState = ClassifyRecoveryJournal();
            if (journalState is RecoveryJournalState.Pending or RecoveryJournalState.Unknown)
                throw new BridgeCommandException("GAME_REPAIR_REQUIRED",
                    "Finish restoring or resolve the existing LWBridge recovery journal before selecting another installation.");

            bool releaseAbandonedAttempt = false;
            if (instanceId is not null)
            {
                if (phase != "error" || gameRoot is null || FindSelectedGameProcess(gameRoot) is not null)
                    throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS",
                        "The selected installation cannot change while an owned or uncertain launch attempt remains active.");
                releaseAbandonedAttempt = true;
            }

            // Persist first. A failed config write keeps the previous root and launch identity.
            persistSelection();
            if (releaseAbandonedAttempt)
                ClearAbandonedLaunchIdentityLocked();
            gameRoot = normalized;
            if (phase == "error")
            {
                phase = "stopped";
                connectionState = "offline";
                lastError = null;
            }
            startRecoveryMonitor = recoveryMonitorEnabled && config is not null && recoveryTimer is null;
        }

        if (startRecoveryMonitor) StartRecoveryMonitor();
    }

    private void ClearAbandonedLaunchIdentityLocked()
    {
        instanceId = null;
        challenge = null;
        launcherPid = null;
        gamePath = null;
        gameStartedAtUtc = null;
        readyAtUnix = null;
    }

    private enum RecoveryJournalState
    {
        Absent,
        VerifiedCompleted,
        Pending,
        Unknown,
    }

    // PM17-02 IMPLEMENTATION POLICY: overview-bridge/recovery.json is a single
    // shared runtime journal, not profile-isolated storage. Therefore any known
    // unfinished stage blocks retargeting regardless of profile, and malformed,
    // unsupported, unreadable or future records are UNKNOWN and fail closed.
    // A leftover "restored" journal is accepted only when the backup manifest
    // independently confirms the same completed schema-1 restoration.
    private RecoveryJournalState ClassifyRecoveryJournal()
    {
        string journalPath = Path.Combine(runtimeRoot, "recovery.json");
        if (testHooks?.ReadAllBytes is null && !File.Exists(journalPath))
            return RecoveryJournalState.Absent;
        try
        {
            byte[] bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(journalPath);
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !MatchesInt(root, "schemaVersion", 1))
                return RecoveryJournalState.Unknown;
            if (!root.TryGetProperty("stage", out JsonElement stageElement) || stageElement.ValueKind != JsonValueKind.String)
                return RecoveryJournalState.Unknown;
            string? stage = stageElement.GetString();
            if (string.IsNullOrWhiteSpace(stage))
                return RecoveryJournalState.Unknown;
            if (string.Equals(stage, "restored", StringComparison.Ordinal))
                return IsVerifiedCompletedRecovery(root) ? RecoveryJournalState.VerifiedCompleted : RecoveryJournalState.Unknown;
            return IsKnownPendingRecoveryStage(stage) ? RecoveryJournalState.Pending : RecoveryJournalState.Unknown;
        }
        catch (FileNotFoundException) { return RecoveryJournalState.Absent; }
        catch (DirectoryNotFoundException) { return RecoveryJournalState.Absent; }
        catch { return RecoveryJournalState.Unknown; }
    }

    private bool IsVerifiedCompletedRecovery(JsonElement recovery)
    {
        try
        {
            if (!recovery.TryGetProperty("backupPath", out JsonElement backupElement) || backupElement.ValueKind != JsonValueKind.String)
                return false;
            string? backupPath = backupElement.GetString();
            if (string.IsNullOrWhiteSpace(backupPath) ||
                !recovery.TryGetProperty("originalFiles", out JsonElement originals) || originals.ValueKind != JsonValueKind.Object)
                return false;
            string manifestPath = Path.Combine(Path.GetFullPath(backupPath), "manifest.json");
            byte[] manifestBytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(manifestPath);
            using JsonDocument manifestDocument = JsonDocument.Parse(manifestBytes);
            JsonElement manifest = manifestDocument.RootElement;
            if (manifest.ValueKind != JsonValueKind.Object || !MatchesInt(manifest, "schemaVersion", 1) ||
                !MatchesString(manifest, "stage", "restored"))
                return false;
            if (!manifest.TryGetProperty("backupPath", out JsonElement manifestBackup) || manifestBackup.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(manifestBackup.GetString()) || !PathEquals(manifestBackup.GetString()!, backupPath))
                return false;
            return manifest.TryGetProperty("originalFiles", out JsonElement manifestOriginals) && manifestOriginals.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKnownPendingRecoveryStage(string stage) =>
        stage is "backup_ready" or
            "restoring_after_failure" or
            "closing_failed_owned_game" or
            "restoring_after_failed_owned_game_close" or
            "active_ready_deferred_restore" or
            "closing_owned_game_for_restore" or
            "restoring_after_owned_game_exit" or
            "restoring_interrupted_operation" or
            "restoring_while_running" or
            "restoring_after_owned_game_close" ||
        stage.StartsWith("installed_", StringComparison.Ordinal) ||
        stage.StartsWith("restored_", StringComparison.Ordinal);

    public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken) => command switch
    {
        "profile_instance_start" => StartAsync(cancellationToken),
        "profile_instance_stop" => StopAsync(payload, cancellationToken),
        "profile_instance_status" => Task.FromResult<object?>(CreateInstanceStatus()),
        "profile_instances_reconcile" => ReconcileStartupAsync(payload, cancellationToken),
        "profile_instances_update_and_restart" => UpdateAndRestartAsync(cancellationToken),
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
        StopRecoveryMonitor();
        StopLeaseTimer(deleteLease: true);
    }

    public void Dispose() => Close();

    private async Task<object?> ReconcileStartupAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        bool autoLaunchAll = !payload.TryGetProperty("autoLaunchAll", out JsonElement requested) ||
            requested.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || requested.GetBoolean();
        bool shouldAttempt;
        lock (stateGate)
        {
            if (startupReconcileConsumed)
                return new { errors = startupReconcileErrors };
            startupReconcileConsumed = true;
            shouldAttempt = autoLaunchAll && (config?.Snapshot.AutoLaunchGame ?? true);
            startupReconcileErrors = Array.Empty<OverviewStartupError>();
            if (!shouldAttempt || phase is "starting" or "running" || gamePid is not null)
                return new { errors = startupReconcileErrors };
        }

        // A correlated interrupted Overview session is intentionally left running for
        // the recovered repair/update-and-restart path. Startup auto-launch must not
        // reclassify that exact owned journal as a generic unmanaged-game error.
        if (TryGetRepairSnapshot(out _))
            return new { errors = startupReconcileErrors };

        try
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException ex)
        {
            lock (stateGate)
                startupReconcileErrors = [new OverviewStartupError(profileId, ex.Code, ex.Message)];
        }

        lock (stateGate)
            return new { errors = startupReconcileErrors };
    }

    private async Task<object?> UpdateAndRestartAsync(CancellationToken cancellationToken)
    {
        if (!TryGetRepairSnapshot(out OverviewRepairSnapshot? repair) || repair is null)
            return CreateUpdateRestartResult(restarted: false);

        lock (stateGate)
        {
            if (closed)
                return CreateUpdateRestartResult(false, "GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            if (phase is "starting" or "stopping" || gamePid is not null || instanceId is not null)
                return CreateUpdateRestartResult(false, "GAME_OPERATION_IN_PROGRESS", "A game lifecycle operation is already in progress.");
            phase = "stopping";
            connectionState = "recovering";
            lastError = null;
        }

        try
        {
            JsonElement result = await RunHelperAsync(
                new OverviewHelperInvocation("stop", profileId, repair.SessionId, null, repair.GamePid, repair.GamePath, repair.GameStartedAtUtc),
                cancellationToken).ConfigureAwait(false);
            ValidateStopResult(result, profileId, repair.SessionId, repair.GamePid, repair.GamePath, repair.GameStartedAtUtc, requireCurrentClientEvidence);
            if (testHooks is null) WriteHostStopEvidence(repair.SessionId, result);
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
                gameStartedAtUtc = null;
                lastError = null;
                readyAtUnix = null;
            }
            await StartAsync(cancellationToken).ConfigureAwait(false);
            return CreateUpdateRestartResult(restarted: true);
        }
        catch (OperationCanceledException)
        {
            SetRepairFailureState("GAME_OPERATION_CANCELLED");
            return CreateUpdateRestartResult(false, "GAME_OPERATION_CANCELLED", "The repair/relaunch operation was cancelled.");
        }
        catch (BridgeCommandException ex)
        {
            SetRepairFailureState(ex.Code);
            return CreateUpdateRestartResult(false, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            SetRepairFailureState("GAME_CLOSE_FAILED");
            return CreateUpdateRestartResult(false, "GAME_CLOSE_FAILED", ex.Message);
        }
    }

    private object CreateUpdateRestartResult(bool restarted, string? error = null, string? message = null) => new
    {
        // IMPLEMENTATION POLICY: the recovered frontend only consumes the successful array length.
        restarted = restarted ? new[] { profileId } : Array.Empty<string>(),
        errors = error is null ? Array.Empty<OverviewStartupError>() : new[] { new OverviewStartupError(profileId, error, message ?? error) },
    };

    private void SetRepairFailureState(string error)
    {
        lock (stateGate)
        {
            if (gamePid is null)
            {
                phase = "error";
                connectionState = "error";
                lastError = error;
            }
        }
    }

    private bool TryGetRepairSnapshot(out OverviewRepairSnapshot? repair)
    {
        repair = null;
        if (gameRoot is null) return false;
        lock (stateGate)
        {
            if (closed || phase is "starting" or "stopping" or "running" || gamePid is not null || instanceId is not null)
                return false;
        }

        try
        {
            string journalPath = Path.Combine(runtimeRoot, "recovery.json");
            byte[] bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(journalPath);
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !MatchesInt(root, "schemaVersion", 1)) return false;
            if (!MatchesString(root, "profileId", profileId)) return false;
            string requestId = RequiredString(root, "requestId");
            string sessionId = RequiredString(root, "sessionId");
            if (!string.Equals(requestId, sessionId, StringComparison.Ordinal)) return false;
            string stage = RequiredString(root, "stage");
            if (stage is not ("active_ready_deferred_restore" or "closing_owned_game_for_restore" or "restoring_after_owned_game_exit"))
                return false;
            int pid = RequirePositiveInt(root, "gamePid");
            string recordedPath = RequiredString(root, "gamePath");
            string expectedPath = Path.GetFullPath(Path.Combine(gameRoot, "Game", "LastWar.exe"));
            if (!PathEquals(recordedPath, expectedPath)) return false;
            string backupPath = RequiredString(root, "backupPath");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string backupRoot = Path.Combine(localAppData, "LWBridgeRebuild", "overview-bridge-backups");
            if (!PathIsWithin(backupPath, backupRoot)) return false;
            if (!root.TryGetProperty("originalFiles", out JsonElement originals) || originals.ValueKind != JsonValueKind.Object)
                return false;
            string startedAtUtc = RequiredProcessStartedAtUtc(root, "gameStartedAtUtc");
            if (!ProcessMatches(pid, expectedPath, startedAtUtc)) return false;
            repair = new OverviewRepairSnapshot(sessionId, pid, expectedPath, startedAtUtc, backupPath, stage);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PathIsWithin(string candidate, string root)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string prefix = fullRoot + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<object?> StartAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(helperPath) && testHooks?.RunHelperAsync is null)
            throw new BridgeCommandException("OVERVIEW_HELPER_MISSING", "The Overview bridge helper was not deployed with LWBridge.Desktop.");

        string newSession = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string newChallenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        string selectedRoot;
        lock (stateGate)
        {
            if (closed) throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            if (gameRoot is null)
                throw new BridgeCommandException("GAME_ROOT_NOT_FOUND", "No validated Last War installation is selected.");
            if (phase is "starting" or "stopping")
                throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS", "A game lifecycle operation is already in progress.");
            if (gamePid is not null)
                throw new BridgeCommandException("GAME_RUNNING", "The LWBridge-owned game is already running.");
            selectedRoot = gameRoot;
            if (FindSelectedGameProcess(selectedRoot) is not null)
                throw new BridgeCommandException("UNMANAGED_GAME_RUNNING", "Close the game started outside this application first.");
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
                new OverviewHelperInvocation("start", profileId, newSession, newChallenge, null, null, null),
                cancellationToken).ConfigureAwait(false);
            OverviewStartResult start = ValidateStartResult(helper, profileId, newSession, newChallenge, selectedRoot, requireCurrentClientEvidence);
            if (testHooks is null) WriteHostStartEvidence(newSession, start);
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
                gameStartedAtUtc = start.GameStartedAtUtc;
                readyAtUnix = start.ReadyAtUnix;
                lastError = null;
            }
            StartLeaseTimer();
            if (!IsReady)
                throw new BridgeCommandException("BRIDGE_START_TIMEOUT", "The game started, but the current Overview bridge response is not fresh.");
            SetDesiredRunning(true);
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
            SetDesiredRunning(false);
            phase = "stopping";
            connectionState = "recovering";
            snapshot = SnapshotLocked();
        }

        try
        {
            JsonElement result = await RunHelperAsync(
                new OverviewHelperInvocation("stop", profileId, snapshot.InstanceId, snapshot.Challenge, snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc),
                cancellationToken).ConfigureAwait(false);
            ValidateStopResult(result, profileId, snapshot.InstanceId, snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc, requireCurrentClientEvidence);
            if (testHooks is null) WriteHostStopEvidence(snapshot.InstanceId, result);
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
                gameStartedAtUtc = null;
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
            start.ArgumentList.Add("--profile-id"); start.ArgumentList.Add(invocation.ProfileId);
            start.ArgumentList.Add("--session-id"); start.ArgumentList.Add(invocation.SessionId!);
            start.ArgumentList.Add("--game-pid"); start.ArgumentList.Add(invocation.GamePid!.Value.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add("--game-path"); start.ArgumentList.Add(invocation.GamePath!);
            if (!string.IsNullOrWhiteSpace(invocation.GameStartedAtUtc))
            {
                start.ArgumentList.Add("--game-started-at-utc"); start.ArgumentList.Add(invocation.GameStartedAtUtc);
            }
        }

        Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Python Overview bridge helper could not be started.");
        lock (stateGate) activeHelperProcess = process;
        bool retainHelperOwnership = false;
        try
        {
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            Task completed = await Task.WhenAny(exitTask, Task.Delay(helperSupervisionTimeout, CancellationToken.None)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, exitTask))
            {
                retainHelperOwnership = true;
                _ = ReleaseRetainedHelperAfterExitAsync(process, exitTask, stdoutTask, stderrTask);
                throw new TimeoutException($"Overview bridge helper exceeded {helperSupervisionTimeout.TotalSeconds:0.#} seconds; the helper retains cleanup ownership.");
            }
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
            if (!retainHelperOwnership)
                ReleaseHelperProcess(process);
        }
    }

    private async Task ReleaseRetainedHelperAfterExitAsync(
        Process process,
        Task exitTask,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        try
        {
            await exitTask.ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch { }
        finally
        {
            ReleaseHelperProcess(process);
        }
    }

    private void ReleaseHelperProcess(Process process)
    {
        lock (stateGate)
        {
            if (ReferenceEquals(activeHelperProcess, process)) activeHelperProcess = null;
        }
        process.Dispose();
    }

    internal static OverviewStartResult ValidateStartResult(
        JsonElement root,
        string expectedProfileId,
        string expectedSessionId,
        string expectedChallenge,
        string expectedGameRoot,
        bool requireCurrentClientEvidence)
    {
        RequireString(root, "mode", "overview_install_launch_ready_deferred_restore");
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
        if (!root.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Overview helper did not preserve the active candidate package while the owned game is running.");
        if (!root.TryGetProperty("restore", out JsonElement restore) || restore.ValueKind != JsonValueKind.Object ||
            !restore.TryGetProperty("restored", out JsonElement restored) || restored.ValueKind != JsonValueKind.False ||
            !restore.TryGetProperty("deferred", out JsonElement deferred) || deferred.ValueKind != JsonValueKind.True ||
            !MatchesString(restore, "stage", "active_ready_deferred_restore"))
            throw new InvalidDataException("Overview helper did not return a durable deferred-restoration journal state.");
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
        string gameStartedAtUtc = RequiredProcessStartedAtUtc(root, "gameStartedAtUtc");
        return new(pid, launcher, Path.GetFullPath(gamePath), gameStartedAtUtc, readyAt);
    }

    internal static void ValidateStopResult(
        JsonElement root,
        string expectedProfileId,
        string expectedSessionId,
        int expectedGamePid,
        string expectedGamePath,
        string expectedGameStartedAtUtc,
        bool requireCurrentClientEvidence)
    {
        RequireString(root, "mode", "overview_exact_pid_close_restore");
        RequireString(root, "bridgeVersion", BridgeVersion);
        RequireString(root, "profileId", expectedProfileId);
        RequireString(root, "sessionId", expectedSessionId);
        if (RequirePositiveInt(root, "gamePid") != expectedGamePid)
            throw new InvalidDataException("Overview stop helper returned a different game PID.");
        string path = RequiredString(root, "gamePath");
        if (!PathEquals(path, expectedGamePath))
            throw new InvalidDataException("Overview stop helper returned a different game path.");
        string startedAtUtc = RequiredProcessStartedAtUtc(root, "gameStartedAtUtc");
        if (!string.Equals(startedAtUtc, expectedGameStartedAtUtc, StringComparison.Ordinal))
            throw new InvalidDataException("Overview stop helper returned a different game process creation identity.");
        if (!root.TryGetProperty("gameRunning", out JsonElement running) || running.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Overview stop helper did not prove owned game exit.");
        if (!root.TryGetProperty("installedFilesChanged", out JsonElement changed) || changed.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Overview stop helper reported changed installed files.");
        if (!root.TryGetProperty("close", out JsonElement close) || close.ValueKind != JsonValueKind.Object ||
            !close.TryGetProperty("processExited", out JsonElement exited) || exited.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Overview stop helper did not prove owned process exit.");
        bool accepted = close.TryGetProperty("accepted", out JsonElement acceptedElement) && acceptedElement.ValueKind == JsonValueKind.True;
        bool alreadyExited = close.TryGetProperty("alreadyExited", out JsonElement alreadyExitedElement) && alreadyExitedElement.ValueKind == JsonValueKind.True;
        if (!accepted && !alreadyExited)
            throw new InvalidDataException("Overview stop helper proved neither normal close acceptance nor an already-exited owned process.");
        if (!root.TryGetProperty("restore", out JsonElement restore) || restore.ValueKind != JsonValueKind.Object ||
            !restore.TryGetProperty("restored", out JsonElement restored) || restored.ValueKind != JsonValueKind.True)
            throw new InvalidDataException("Overview stop helper did not prove exact script restoration after game exit.");
        if (requireCurrentClientEvidence && !MatchesString(restore, "packageSha256", ExpectedPackageSha256))
            throw new InvalidDataException("Overview stop helper did not restore the supported current-client script package identity.");
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
        if (!ProcessMatches(snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc)) return false;
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

    private bool ProcessMatches(int pid, string expectedPath, string? expectedStartedAtUtc)
    {
        if (testHooks?.ProcessMatches is { } test) return test(pid, expectedPath, expectedStartedAtUtc);
        if (string.IsNullOrWhiteSpace(expectedStartedAtUtc)) return false;
        try
        {
            using Process process = Process.GetProcessById(pid);
            if (process.HasExited) return false;
            string? actual = process.MainModule?.FileName;
            string actualStartedAtUtc = process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            return actual is not null && PathEquals(actual, expectedPath) &&
                string.Equals(actualStartedAtUtc, expectedStartedAtUtc, StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private GameProcessIdentity? FindSelectedGameProcess()
    {
        string? selectedRoot;
        lock (stateGate) selectedRoot = gameRoot;
        return selectedRoot is null ? null : FindSelectedGameProcess(selectedRoot);
    }

    private static GameProcessIdentity? FindSelectedGameProcess(string selectedRoot)
    {
        string expected = Path.GetFullPath(Path.Combine(selectedRoot, "Game", "LastWar.exe"));
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
        if (ProcessMatches(snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc)) return;
        // OVL-05: the original classifier requires more than one consecutive
        // missing-process observation. The recovery monitor owns that counter;
        // synchronous status reads must not turn one miss into an immediate exit.
        if (Volatile.Read(ref missingProcessObservations) > 1)
            MarkUnexpectedExit(snapshot);
    }

    private void StartLeaseTimer()
    {
        StopLeaseTimer(deleteLease: false);
        long generation = Interlocked.Increment(ref leaseGeneration);
        leaseTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                OwnedSnapshot? snapshot = GetOwnedSnapshot();
                if (snapshot is null || snapshot.Phase != "running") return;
                WriteLease(snapshot.InstanceId, snapshot.Challenge, generation);
            }
            catch { }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void StopLeaseTimer(bool deleteLease)
    {
        Interlocked.Increment(ref leaseGeneration);
        System.Threading.Timer? timer = Interlocked.Exchange(ref leaseTimer, null);
        timer?.Dispose();
        if (!deleteLease) return;
        lock (leaseWriteGate)
        {
            try { DeleteFile(Path.Combine(runtimeRoot, "lease.txt")); } catch { }
            try
            {
                if (Directory.Exists(runtimeRoot))
                    foreach (string temp in Directory.EnumerateFiles(runtimeRoot, "lease.txt.tmp-*"))
                        try { DeleteFile(temp); } catch { }
            }
            catch { }
        }
    }

    private void WriteLease(string session, string nonce, long generation)
    {
        lock (leaseWriteGate)
        {
            if (generation != Volatile.Read(ref leaseGeneration)) return;
            if (testHooks?.WriteLease is { } test)
            {
                test(session, nonce, runtimeRoot);
                return;
            }
            Directory.CreateDirectory(runtimeRoot);
            string path = Path.Combine(runtimeRoot, "lease.txt");
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            try
            {
                File.WriteAllText(temp,
                    "schema=1\n" +
                    $"bridgeVersion={BridgeVersion}\n" +
                    $"sessionId={session}\n" +
                    $"challenge={nonce}\n" +
                    $"updatedAt={DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}\n");
                if (generation != Volatile.Read(ref leaseGeneration)) return;
                File.Move(temp, path, overwrite: true);
                if (generation != Volatile.Read(ref leaseGeneration))
                    try { DeleteFile(path); } catch { }
            }
            finally
            {
                try { if (File.Exists(temp)) DeleteFile(temp); } catch { }
            }
        }
    }

    private void WriteHostStartEvidence(string session, OverviewStartResult start)
    {
        try
        {
            string directory = Path.Combine(evidenceRoot, session);
            Directory.CreateDirectory(directory);
            string? processPath = Environment.ProcessPath;
            string assemblyPath = typeof(OverviewLifecycleService).Assembly.Location;
            File.WriteAllText(Path.Combine(directory, "host-start.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                recordedAtUtc = DateTimeOffset.UtcNow,
                profileId,
                sessionId = session,
                gamePid = start.GamePid,
                gamePath = start.GamePath,
                gameStartedAtUtc = start.GameStartedAtUtc,
                readyAt = start.ReadyAtUnix,
                hostProcess = FileIdentity(processPath),
                desktopAssembly = FileIdentity(assemblyPath),
                overviewHelper = FileIdentity(helperPath),
            }, JsonOptions.Default));
        }
        catch { }
    }

    private void WriteHostStopEvidence(string session, JsonElement helperResult)
    {
        try
        {
            string directory = Path.Combine(evidenceRoot, session);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "host-stop.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                recordedAtUtc = DateTimeOffset.UtcNow,
                profileId,
                sessionId = session,
                helper = helperResult,
            }, JsonOptions.Default));
        }
        catch { }
    }

    private static object? FileIdentity(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        using FileStream stream = File.OpenRead(path);
        string sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var info = new FileInfo(path);
        return new { path = info.FullName, size = info.Length, sha256 };
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
        launcherPid, gamePath!, gameStartedAtUtc!, lastError, readyAtUnix);

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Overview helper field '{name}' is missing or invalid.");
        return value.GetString()!;
    }

    private static string RequiredProcessStartedAtUtc(JsonElement root, string name)
    {
        string value = RequiredString(root, name);
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
            throw new InvalidDataException($"Overview helper field '{name}' is not a valid process creation timestamp.");
        return parsed.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
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
        string GameStartedAtUtc,
        string? Error,
        long? ReadyAtUnix);

    private sealed record GameProcessIdentity(int Pid, string Path);
    private sealed record OverviewRepairSnapshot(string SessionId, int GamePid, string GamePath, string GameStartedAtUtc, string BackupPath, string Stage);
}

internal sealed record OverviewStartResult(int GamePid, int LauncherPid, string GamePath, string GameStartedAtUtc, long ReadyAtUnix);
internal sealed record OverviewStartupError(string ProfileId, string Error, string Message);
