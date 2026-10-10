using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace LWBridge.Desktop;

// OVL-02/03/04 IMPLEMENTATION POLICY: the Overview lifecycle deliberately uses
// the independently proven current-client LuaEntry execution route. It does not
// claim to reproduce the still-unrecovered original launch-proof/ticket or
// hello.ack protocol. READY requires a fresh, exact-session game-side response.
internal sealed class OverviewLifecycleTestHooks
{
    public Func<OverviewHelperInvocation, CancellationToken, Task<JsonElement>>? RunHelperAsync { get; init; }
    public Func<string, CancellationToken, Task>? RunOfficialRecoverAsync { get; init; }
    public Func<string, CancellationToken, Task>? RunOfficialSettleAsync { get; init; }
    public Func<int, string, string?, bool>? ProcessMatches { get; init; }
    public Func<string, byte[]>? ReadAllBytes { get; init; }
    // Inert producer seam: fires only after the actual host registry accepted
    // the launch report. Tests wait on this signal instead of a timing sleep.
    public Action<OverviewHelperInvocation>? LaunchReportRegistered { get; init; }
    public Action<string, string, string>? WriteLease { get; init; }
    public Action<string, string, string>? WriteStartCancellation { get; init; }
    public Action<string>? DeleteFile { get; init; }
    // Runs only after the OS handle owns the validated identity. The test seam
    // cannot bypass sharing restrictions or substitute pathname deletion.
    public Action<string, OverviewRuntimeFileMutation>? RuntimeFileBeforeMutation { get; init; }
    public Func<DateTimeOffset>? UtcNow { get; init; }
    public Func<long>? MonotonicMilliseconds { get; init; }
    public Func<bool>? UpdateProcessRunning { get; init; }
    public Func<string?>? UpdateActivityFingerprint { get; init; }
    public Func<CancellationToken, Task>? TerminateUpdateProcessesAsync { get; init; }
    public Func<int, string, bool>? ProcessHung { get; init; }
    public Func<int, string, string, CancellationToken, Task>? TerminateOwnedProcessAsync { get; init; }
    // HOME 009 R1 B: Win32 process primitives for the real (non-overridden) handle-bound termination logic.
    public IOwnedProcessApi? OwnedProcessApi { get; init; }
    // HOME 009 R1 C: PIDs of LastWar processes at <root>\Game\LastWar.exe (0x41dea4); null = real process enumeration.
    public Func<string, IReadOnlyList<int>>? SelectedGamePids { get; init; }
    public Func<TimeSpan, CancellationToken, Task>? DelayAsync { get; init; }
    // HOME 009: <root>\Game\LastWar.exe presence (0x41d2ce) and the three recovery log readers (0x41a18d).
    public Func<bool>? GameRootAvailable { get; init; }
    public Func<string, IRecoveryLogReader>? CreateRecoveryLogReader { get; init; }
}

internal sealed record OverviewHelperInvocation(
    string Operation,
    string ProfileId,
    string? SessionId,
    string? Challenge,
    int? GamePid,
    string? GamePath,
    string? GameStartedAtUtc,
    int? TimeoutSeconds = null,
    int? SupervisionMilliseconds = null,
    LWBridgeControlPipeLaunchBinding? ControlPipeLaunchBinding = null);

internal sealed partial class OverviewLifecycleService : INativeAsyncCommandService, IDisposable
{
    internal const string BridgeVersion = "lwbridge-overview-bridge-1";
    internal const string ReadyMessage = "LWbridge is running";
    private const string StartCancellationFileName = "cancel-start.txt";
    private static readonly TimeSpan HeartbeatFreshness = TimeSpan.FromSeconds(5);
    // HOME 009 R2 D (see run_overview_bridge.py READY_WINDOW_MILLISECONDS, original 0x1DD119 add rax,0x15F90).
    internal const long BridgeReadyWindowMilliseconds = 90_000;
    private const long StartCleanupMarginMilliseconds = 15_000;
    private const long MinimumAcquisitionMilliseconds = 10_000;
    internal const long MinimumStartWindowMilliseconds =
        MinimumAcquisitionMilliseconds + BridgeReadyWindowMilliseconds + StartCleanupMarginMilliseconds;

    private readonly object stateGate = new();
    private readonly object leaseWriteGate = new();
    private readonly string helperPath;
    private string? gameRoot;
    // Picker selection is persistent configuration; gameRoot remains the active
    // launch/restore binding until its exact owned session retires. Never let a
    // new picker choice silently redirect an old game's Stop or recovery.
    private bool hasStagedConfiguredRoot;
    private string? stagedConfiguredRoot;
    private readonly string profileId;
    private readonly string applicationDataRoot;
    private readonly string runtimeRoot;
    private readonly string evidenceRoot;
    private readonly string backupRoot;
    private readonly string liveResourceRuntimeRoot;
    private readonly string profileRuntimeRoot;
    private readonly TimeSpan helperSupervisionTimeout;
    private readonly LocalConfigStore? config;
    private readonly OverviewLifecycleTestHooks? testHooks;
    private readonly LWBridgeControlPipeHostState? bridgeHostState;
    private readonly bool bridgeControlPipeLaunchBindingEnabled;
    private readonly bool requireCurrentClientEvidence;
    private readonly bool recoveryMonitorEnabled;
    private readonly Func<int, bool>? foreignOwnedProcess;
    private readonly Func<string, bool>? foreignOwnedInstallation;
    // Lease-timer ownership is (session, challenge): a timer started for one
    // owner is only ever stopped/renewed for that exact owner (LEAD009R3-01).
    private readonly object leaseTimerGate = new();
    private System.Threading.Timer? leaseTimer;
    private string? leaseTimerSession;
    private string? leaseTimerChallenge;
    private long leaseGeneration;
    private Process? activeHelperProcess;
    // A pending recovery Stop is acknowledged only after its in-flight
    // official launcher/helper has completed exact-owner cancellation cleanup.
    private TaskCompletionSource? activeStartCompletion;
    private bool closed;
    private string phase = "stopped";
    private string connectionState = "offline";
    private string? instanceId;
    private long? instanceStartedAtUnixMilliseconds;
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
        bool startRecoveryMonitor = true,
        LWBridgeControlPipeHostState? bridgeHostState = null,
        bool enableBridgeControlPipeLaunchBinding = false,
        string? runtimeRoot = null,
        string? evidenceRoot = null,
        string? backupRoot = null,
        string? applicationDataRoot = null,
        Func<int, bool>? foreignOwnedProcess = null,
        Func<string, bool>? foreignOwnedInstallation = null)
    {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("profileId is required", nameof(profileId));
        this.profileId = profileId;
        this.gameRoot = string.IsNullOrWhiteSpace(gameRoot) ? null : Path.GetFullPath(gameRoot);
        this.helperPath = helperPath ?? Path.Combine(AppContext.BaseDirectory, "OverviewBridge", "run_overview_bridge_current.py");
        this.helperSupervisionTimeout = helperSupervisionTimeout ?? TimeSpan.FromSeconds(120 + 90 + 15);
        this.requireCurrentClientEvidence = requireCurrentClientEvidence ?? helperPath is null;
        this.config = config;
        this.testHooks = testHooks;
        this.foreignOwnedProcess = foreignOwnedProcess;
        this.foreignOwnedInstallation = foreignOwnedInstallation;
        this.bridgeHostState = bridgeHostState;
        bridgeControlPipeLaunchBindingEnabled =
            enableBridgeControlPipeLaunchBinding;
        recoveryMonitorEnabled = startRecoveryMonitor;
        string defaultApplicationRoot = DesktopApplicationPaths.DefaultRoot;
        this.applicationDataRoot = Path.GetFullPath(applicationDataRoot ?? defaultApplicationRoot);
        this.runtimeRoot = Path.GetFullPath(runtimeRoot ?? Path.Combine(this.applicationDataRoot, "overview-bridge"));
        this.evidenceRoot = Path.GetFullPath(evidenceRoot ?? Path.Combine(this.applicationDataRoot, "overview-evidence"));
        this.backupRoot = Path.GetFullPath(backupRoot ?? Path.Combine(this.applicationDataRoot, "overview-bridge-backups"));
        liveResourceRuntimeRoot = Path.Combine(this.applicationDataRoot, "live-resource");
        profileRuntimeRoot = Path.Combine(this.applicationDataRoot, "profiles", profileId);
        if (startRecoveryMonitor) StartRecoveryMonitor();
    }

    public bool CanHandle(string command) =>
        command is "profile_instance_start" or "profile_instance_stop" or "profile_instance_status" or
            "profile_instances_reconcile" or "profile_instances_update_and_restart";

    internal LWBridgeControlPipeHostState? BridgeHostState => bridgeHostState;
    internal string ApplicationDataRoot => applicationDataRoot;
    internal string RuntimeRoot => runtimeRoot;
    internal string EvidenceRoot => evidenceRoot;
    internal string BackupRoot => backupRoot;
    internal string LiveResourceRuntimeRoot => liveResourceRuntimeRoot;
    internal string ProfileRuntimeRoot => profileRuntimeRoot;

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
                {
                    if (string.Equals(connectionState, "maintenance", StringComparison.Ordinal))
                        return "maintenance";
                    return IsReady ? "connected" : "error";
                }
                return connectionState;
            }
        }
    }

    public bool RepairRequired => TryGetRepairSnapshot(out _);

    // F-07 OWN_DESIGN: deleting a local registry identity is NOT authority
    // to abandon exact game ownership or an unreadable recovery journal.
    // Stop and restoration must finish before its metadata may be retired.
    internal bool CanRetireStoppedLocalProfile()
    {
        lock (stateGate)
        {
            if (closed || phase is "starting" or "stopping" or "running" ||
                gamePid is not null || instanceId is not null ||
                activeHelperProcess is not null || activeRecoveryCancellation is not null ||
                activeRecoveryRun is not null || activeStartCompletion is not null && !activeStartCompletion.Task.IsCompleted ||
                config?.Snapshot.GameDesiredRunning == true)
                return false;
            return ClassifyRecoveryJournal() is RecoveryJournalState.Absent or RecoveryJournalState.VerifiedCompleted;
        }
    }

    public bool RuntimeManaged
    {
        get
        {
            RefreshExitedOwnership();
            return GetOwnedSnapshot() is not null;
        }
    }

    internal bool OwnsExactProcess(int pid)
    {
        OwnedSnapshot? snapshot = GetOwnedSnapshot();
        return snapshot is not null && snapshot.GamePid == pid &&
            ProcessMatches(pid, snapshot.GamePath, snapshot.GameStartedAtUtc);
    }

    internal bool HasCapturedInstallation(string candidateRoot)
    {
        string? root;
        bool active;
        string? observedRoot = Volatile.Read(ref gameRoot);
        if (observedRoot is null || !PathEquals(observedRoot, candidateRoot))
            return false;
        // Start may ask about sibling owners while it holds its OWN stateGate.
        // Never wait on a sibling's lock here: simultaneous A/B starts would
        // otherwise form an A->B / B->A lock cycle. Only matching captured
        // installations get the conservative busy disposition.
        if (!Monitor.TryEnter(stateGate))
            return true;
        try
        {
            root = gameRoot;
            active = instanceId is not null || phase is "starting" or "stopping";
        }
        finally { Monitor.Exit(stateGate); }
        if (root is null || !PathEquals(root, candidateRoot))
            return false;
        // Even after the process exits, the journal still owns the installed
        // Lua triplet until the exact restoration is confirmed.
        return active || ClassifyRecoveryJournal() is
            RecoveryJournalState.Pending or RecoveryJournalState.Unknown;
    }

    // PM16-01 IMPLEMENTATION POLICY: a validated installation may replace the bound
    // lifecycle root only while no owned launch/close/recovery work is active and no
    // same-profile recovery journal remains. Re-selecting the same root
    // is a harmless persistence-only no-op.
    internal void RebindGameRoot(string selectedRoot, Action persistSelection)
    {
        if (string.IsNullOrWhiteSpace(selectedRoot))
            throw new ArgumentException("A validated game root is required.", nameof(selectedRoot));
        RebindGameRootSelection(selectedRoot, selectionChanged: false, persistSelection);
    }

    internal void RebindGameRootSelection(
        string? selectedRoot,
        bool selectionChanged,
        Action persistSelection)
    {
        ArgumentNullException.ThrowIfNull(persistSelection);
        string? normalized = string.IsNullOrWhiteSpace(selectedRoot)
            ? null
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedRoot));
        bool startRecoveryMonitor = false;

        lock (stateGate)
        {
            if (closed)
                throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            bool sameBoundRoot =
                gameRoot is null && normalized is null ||
                gameRoot is not null && normalized is not null && PathEquals(gameRoot, normalized);
            if (sameBoundRoot && !selectionChanged)
            {
                persistSelection();
                return;
            }
            if (phase is "starting" or "stopping" or "running" ||
                gamePid is not null || activeHelperProcess is not null || activeRecoveryCancellation is not null)
            {
                // Original game_root_select (0x188f12) persists the picker
                // choice even with a running game. Do not rebind the active
                // process: its exact path/creation and restoration own the old
                // root until the next eligible launch.
                persistSelection();
                stagedConfiguredRoot = normalized;
                hasStagedConfiguredRoot = true;
                return;
            }
            RecoveryJournalState journalState = ClassifyRecoveryJournal();
            if (journalState is RecoveryJournalState.Pending or RecoveryJournalState.Unknown)
            {
                // The original picker persists independently of the current repair
                // owner. A pending/unknown journal still owns the former installation;
                // staging a new selection must not retarget its restoration.
                persistSelection();
                stagedConfiguredRoot = normalized;
                hasStagedConfiguredRoot = true;
                return;
            }

            bool releaseAbandonedAttempt = false;
            if (instanceId is not null)
            {
                if (phase != "error" || gameRoot is null || FindSelectedGameProcess(gameRoot) is not null)
                {
                    persistSelection();
                    stagedConfiguredRoot = normalized;
                    hasStagedConfiguredRoot = true;
                    return;
                }
                releaseAbandonedAttempt = true;
            }

            // Persist first. A failed config write keeps the previous root and launch identity.
            persistSelection();
            if (releaseAbandonedAttempt)
                ClearAbandonedLaunchIdentityLocked();
            gameRoot = normalized;
            stagedConfiguredRoot = null;
            hasStagedConfiguredRoot = false;
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
        instanceStartedAtUnixMilliseconds = null;
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
        "profile_instance_start" => StartCommandAsync(ReadCloseUnmanaged(payload), cancellationToken),
        "profile_instance_stop" => StopAsync(payload, cancellationToken),
        "profile_instance_status" => Task.FromResult(CreateProfileInstanceStatus()),
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

        if (snapshot.Phase == "running" &&
            string.Equals(snapshot.ConnectionState, "maintenance", StringComparison.Ordinal))
        {
            return new
            {
                phase = "running",
                pid = (int?)snapshot.GamePid,
                instanceId = snapshot.InstanceId,
                connectionState = "maintenance",
                error = "SERVER_MAINTENANCE",
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
        string? cancellingSession = null;
        string? cancellingChallenge = null;
        string? cleanupSession = null;
        string? cleanupChallenge = null;
        lock (stateGate)
        {
            closed = true;
            cleanupSession = instanceId;
            cleanupChallenge = challenge;
            if (phase == "starting" && instanceId is not null && challenge is not null)
            {
                cancellingSession = instanceId;
                cancellingChallenge = challenge;
            }
        }
        if (cancellingSession is not null && cancellingChallenge is not null)
            TryWriteStartCancellationMarker(cancellingSession, cancellingChallenge);
        StopRecoveryMonitor();
        StopLeaseTimer(deleteLease: true, cleanupSession, cleanupChallenge);
    }

    public void Dispose() => Close();

    private async Task<object?> ReconcileStartupAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        bool autoLaunchAll = payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("autoLaunchAll", out JsonElement requested) ||
            requested.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || requested.GetBoolean();
        bool shouldAttempt;
        lock (stateGate)
        {
            if (startupReconcileConsumed)
                return new { errors = startupReconcileErrors };
            startupReconcileConsumed = true;
            // HOME 009 R2 C: the original reconcile (0x203021, 0x39E153/0x2EA391) reads no persisted launch preference; the
            // only gate is the payload `autoLaunchAll` (default true), which the UI fills from its local-storage preference.
            // The profile's native AutoLaunchGame value is a mirror and no longer participates in admission.
            shouldAttempt = autoLaunchAll;
            startupReconcileErrors = Array.Empty<OverviewStartupError>();
            if (phase is "starting" or "running" || gamePid is not null)
                return new { errors = startupReconcileErrors };
        }

        // Restoration/adoption precedes fresh-game autoLaunchAll admission.
        // The same-build game survives host restart, while the validated
        // outdated-build journal takes the original restartRequired path.
        if (TryGetRepairSnapshot(out OverviewRepairSnapshot? repair) && repair is not null)
        {
            OverviewStartupError? restored = await AdoptOrRepairAsync(
                repair, cancellationToken).ConfigureAwait(false);
            lock (stateGate)
                startupReconcileErrors = restored is null
                    ? Array.Empty<OverviewStartupError>() : [restored];
            return new { errors = startupReconcileErrors };
        }

        if (!shouldAttempt)
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
        // Capture before stopping: the protected adoption reader must only
        // accept a still-running exact process, never a recycled PID later.
        _ = TryReadAdoptionRecord(out OverviewAdoptionSnapshot? retained, out _);
        if (retained is not null &&
            !string.Equals(retained.InstanceId, repair.SessionId, StringComparison.Ordinal))
            retained = null;

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
            if (retained is not null)
            {
                RemoveAdoptionRecord(retained.InstanceId, retained.Challenge);
                ClearRuntimeSessionFiles(retained.InstanceId, retained.Challenge);
            }
            StopLeaseTimer(deleteLease: false);
            lock (stateGate)
            {
                phase = "stopped";
                connectionState = "offline";
                instanceId = null;
                instanceStartedAtUnixMilliseconds = null;
                challenge = null;
                gamePid = null;
                recoveryTracked = null;
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

    // Keep this result typed between native lifecycle commands. The external
    // command dispatcher serializes with JsonOptions.Default (camelCase);
    // internal adoption must never re-serialize it with different options.
    private sealed record OverviewRestartResult(string[] Restarted, OverviewStartupError[] Errors);

    private OverviewRestartResult CreateUpdateRestartResult(bool restarted, string? error = null, string? message = null) => new(
        // IMPLEMENTATION POLICY: the recovered frontend only consumes the successful array length.
        restarted ? new[] { profileId } : Array.Empty<string>(),
        error is null ? Array.Empty<OverviewStartupError>() : new[] { new OverviewStartupError(profileId, error, message ?? error) });

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

    // 0x207784-0x2077af: `closeUnmanaged` is read from the start payload object; a missing key or a non-boolean
    // value is false. Reconcile (0x2053fa) and update-and-restart (0x206963) store false in the same launch slot.
    internal static bool ReadCloseUnmanaged(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("closeUnmanaged", out JsonElement value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private async Task<object?> StartAsync(CancellationToken cancellationToken,
        bool closeUnmanaged = false, bool resetManualRecoveryStatus = false)
    {
        string newSession = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string newChallenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        string selectedRoot;
        var startCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (stateGate)
        {
            if (closed) throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            cancellationToken.ThrowIfCancellationRequested();
            // Consume configured-root choice only after the old owner is absent.
            // A running/uncertain session must continue to use its captured root.
            if (hasStagedConfiguredRoot && phase is not ("starting" or "stopping" or "running") &&
                gamePid is null && instanceId is null)
            {
                if (ClassifyRecoveryJournal() is RecoveryJournalState.Pending or RecoveryJournalState.Unknown)
                    throw new BridgeCommandException("GAME_REPAIR_REQUIRED",
                        "Restore the previous owned installation before launching from the newly selected folder.");
                gameRoot = stagedConfiguredRoot;
                stagedConfiguredRoot = null;
                hasStagedConfiguredRoot = false;
            }
            if (gameRoot is null)
                throw new BridgeCommandException("GAME_ROOT_NOT_FOUND", "No validated Last War installation is selected.");
            // Original profile_instance_start (0x23e9dc; COMPLETION-010 c-handlers 5.1): GAME_ROOT_NOT_FOUND, then
            // PROFILE_ALREADY_RUNNING for a profile that already has an instance record in any phase, then the
            // unmanaged-game gate. GAME_OPERATION_IN_PROGRESS / GAME_RUNNING are not start-path codes in 0.3.17
            // (closed / profile-replacement waits above stay clone-only lifetime fences).
            if (phase is "starting" or "stopping" || gamePid is not null)
                throw new BridgeCommandException("PROFILE_ALREADY_RUNNING", "PROFILE_ALREADY_RUNNING");
            selectedRoot = gameRoot;
            if (testHooks is null && foreignOwnedInstallation?.Invoke(selectedRoot) == true)
                throw new BridgeCommandException("BRIDGE_HOST_BUSY",
                    "Another profile still owns the selected installation and its restoration journal.");
            IReadOnlyList<int> unmanagedPids = SelectedGamePids(selectedRoot);
            if (unmanagedPids.Count > 0 && !closeUnmanaged)
                // 0x1d6781-0x1d6828: code == message == "UNMANAGED_GAME_RUNNING", details {pids:[ascending]}.
                throw new BridgeCommandException("UNMANAGED_GAME_RUNNING", "UNMANAGED_GAME_RUNNING", new { pids = unmanagedPids });
            // Packaging errors must not preempt the recovered original Start
            // preconditions. Root and running-profile errors are observable even
            // when a local helper is missing; do not publish a starting owner.
            if (!File.Exists(helperPath) && testHooks?.RunHelperAsync is null)
                throw new BridgeCommandException("OVERVIEW_HELPER_MISSING",
                    "The Overview bridge helper was not deployed with LWBridge.Desktop.");
            phase = "starting";
            connectionState = "starting";
            instanceId = newSession;
            instanceStartedAtUnixMilliseconds = RecoveryNow().ToUnixTimeMilliseconds();
            challenge = newChallenge;
            lastError = null;
            readyAtUnix = null;
            activeStartCompletion = startCompletion;
        }

        LWBridgeControlPipeLaunchBinding? controlPipeLaunchBinding = null;
        bool startTransactionSucceeded = false;
        try
        {
            // A rejected manual Start must not reset an active pending recovery
            // to idle. Only an admitted manual Start clears the old UI notice;
            // automatic recovery and repair Start retain their own run state.
            if (resetManualRecoveryStatus)
                ResetRecoveryStatusToIdle(invalidateRun: false);
            if (closeUnmanaged)
            {
                await CloseUnmanagedSelectedGamesAsync(selectedRoot, cancellationToken).ConfigureAwait(false);
                ThrowIfStartClosed(cancellationToken);
            }
            await EnsureControlPipeHostStartedAsync(selectedRoot).ConfigureAwait(false);
            OverviewHelperInvocation startInvocation;
            long? startDeadline = null;
            if (testHooks is null)
            {
                // Official updating and LWBridge candidate startup use separate
                // bounded budgets. A legitimate game/Lua update must not consume
                // most of the bridge-readiness window before the candidate even
                // starts.
                long officialSettleDeadline = checked(
                    RecoveryClockMilliseconds() +
                    (long)OfficialClientSettleTimeout.TotalMilliseconds);
                await EnsureOfficialClientSettledAsync(
                    selectedRoot, cancellationToken, officialSettleDeadline,
                    forceOfficialSettle: true).ConfigureAwait(false);

                startDeadline = checked(
                    RecoveryClockMilliseconds() +
                    (long)helperSupervisionTimeout.TotalMilliseconds);
                controlPipeLaunchBinding = PrepareControlPipeLaunchBinding(
                    newSession);
                startInvocation = CreateBoundedStartInvocation(
                    newSession,
                    newChallenge,
                    startDeadline.Value,
                    controlPipeLaunchBinding);
            }
            else
            {
                await EnsureOfficialClientSettledAsync(selectedRoot, cancellationToken).ConfigureAwait(false);
                controlPipeLaunchBinding = PrepareControlPipeLaunchBinding(
                    newSession);
                startInvocation = new OverviewHelperInvocation(
                    "start", profileId, newSession, newChallenge, null, null, null,
                    ControlPipeLaunchBinding: controlPipeLaunchBinding);
            }

            JsonElement helper;
            try
            {
                helper = await RunHelperAsync(startInvocation, cancellationToken).ConfigureAwait(false);
            }
            catch (BridgeCommandException error) when (
                string.Equals(error.Code, "OFFICIAL_LAUNCHER_RESTARTED", StringComparison.Ordinal))
            {
                // 0x1DC345..0x1DC3D0: only attempt 1's exact 27-byte
                // OFFICIAL_LAUNCHER_RESTARTED error permits attempt 2.
                // Official-Lua-update and spawn-timeout are NOT equivalent
                // events. The current helper has no verified producer of this
                // typed event; synthetic tests prove policy, not live mapping.
                cancellationToken.ThrowIfCancellationRequested();
                if (testHooks is null)
                {
                    long officialSettleDeadline = checked(
                        RecoveryClockMilliseconds() +
                        (long)OfficialClientSettleTimeout.TotalMilliseconds);
                    await EnsureOfficialClientSettledAsync(
                        selectedRoot, cancellationToken, officialSettleDeadline,
                        forceOfficialSettle: true).ConfigureAwait(false);
                    if (startDeadline!.Value - RecoveryClockMilliseconds() <
                        MinimumStartWindowMilliseconds + 1_000)
                        throw;
                    RefreshControlPipeLaunchBinding(controlPipeLaunchBinding);
                    startInvocation = CreateBoundedStartInvocation(
                        newSession, newChallenge, startDeadline.Value,
                        controlPipeLaunchBinding);
                }
                else
                {
                    RefreshControlPipeLaunchBinding(controlPipeLaunchBinding);
                    startInvocation = new OverviewHelperInvocation(
                        "start", profileId, newSession, newChallenge, null, null, null,
                        ControlPipeLaunchBinding: controlPipeLaunchBinding);
                }
                // Exactly one more invocation; no catch retries its failure.
                helper = await RunHelperAsync(startInvocation, cancellationToken).ConfigureAwait(false);
            }
            OverviewStartResult start = ValidateStartResult(helper, profileId, newSession, newChallenge, selectedRoot, requireCurrentClientEvidence);
            if (testHooks is null) WriteHostStartEvidence(newSession, start);
            bool cancelBeforePublication;
            lock (stateGate)
            {
                cancelBeforePublication = closed || cancellationToken.IsCancellationRequested;
                if (!cancelBeforePublication)
                {
                    phase = "running";
                    connectionState = "connected";
                    instanceId = newSession;
                    challenge = newChallenge;
                    gamePid = start.GamePid;
                    recoveryTracked = new RecoveryTrackedGame(start.GamePid, start.GamePath, start.GameStartedAtUtc);
                    launcherPid = start.LauncherPid;
                    gamePath = start.GamePath;
                    gameStartedAtUtc = start.GameStartedAtUtc;
                    readyAtUnix = start.ReadyAtUnix;
                    lastError = null;
                }
            }
            if (cancelBeforePublication)
            {
                await StopCancelledSuccessfulStartAsync(
                    start, newSession, newChallenge).ConfigureAwait(false);
                throw new BridgeCommandException(
                    "GAME_OPERATION_CANCELLED",
                    "LWBridge closed while the game was starting.");
            }

            if (!StartLeaseTimer(newSession, newChallenge))
            {
                bool closedNow;
                lock (stateGate) closedNow = closed;
                if (closedNow)
                {
                    await StopCancelledSuccessfulStartAsync(start, newSession, newChallenge)
                        .ConfigureAwait(false);
                    throw new BridgeCommandException(
                        "GAME_OPERATION_CANCELLED",
                        "LWBridge closed while the game was starting.");
                }
                throw new BridgeCommandException(
                    "GAME_OPERATION_CANCELLED",
                    "The started game session was retired before its lease timer began.");
            }
            if (controlPipeLaunchBinding is not null &&
                !await WaitAuthenticatedRouteAsync(newSession, cancellationToken).ConfigureAwait(false))
            {
                await StopCancelledSuccessfulStartAsync(start, newSession, newChallenge)
                    .ConfigureAwait(false);
                throw new BridgeCommandException("BRIDGE_START_TIMEOUT",
                    "The game was launched, but no authenticated bridge connection was accepted before registration expired.");
            }
            if (!IsReady)
            {
                // The game/session has already been published. Original 0.3.17
                // unregisters/unbinds on launch failure; retain our additional
                // captured-process exit and journal restoration requirements.
                await StopCancelledSuccessfulStartAsync(start, newSession, newChallenge)
                    .ConfigureAwait(false);
                throw new BridgeCommandException("BRIDGE_START_TIMEOUT",
                    "The game started, but the current Overview bridge response is not fresh.");
            }
            if (controlPipeLaunchBinding is not null)
            {
                try
                {
                    CommitAdoptionRecord(start, newSession, newChallenge, controlPipeLaunchBinding);
                }
                catch (Exception error)
                {
                    await StopCancelledSuccessfulStartAsync(start, newSession, newChallenge)
                        .ConfigureAwait(false);
                    throw new BridgeCommandException("RECOVERY_RECORD_COMMIT_FAILED",
                        "The protected adoption record could not be written; the exact owned game was stopped and restored.",
                        new { error = error.Message });
                }
            }
            SetDesiredRunning(true);

            bool cancelAfterPublication;
            lock (stateGate)
            {
                cancelAfterPublication = closed || cancellationToken.IsCancellationRequested;
                if (!cancelAfterPublication)
                    startTransactionSucceeded = true;
            }
            if (cancelAfterPublication)
            {
                await StopCancelledSuccessfulStartAsync(
                    start, newSession, newChallenge).ConfigureAwait(false);
                throw new BridgeCommandException(
                    "GAME_OPERATION_CANCELLED",
                    "LWBridge closed while the game was starting.");
            }
            return CreateInstanceStatus();
        }
        catch (BridgeCommandException ex)
        {
            if (ex.Code == "GAME_OPERATION_CANCELLED")
            {
                ResetCancelledStartState();
            }
            else
            {
                lock (stateGate)
                {
                    if (gamePid is null)
                    {
                        phase = "error";
                        connectionState = "error";
                        lastError = ex.Code;
                    }
                }
            }
            throw;
        }
        catch (OperationCanceledException)
        {
            ResetCancelledStartState();
            throw new BridgeCommandException(
                "GAME_OPERATION_CANCELLED",
                "LWBridge closed while the game was starting.");
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
        finally
        {
            try
            {
                if (!startTransactionSucceeded &&
                    controlPipeLaunchBinding is not null)
                    bridgeHostState?.CancelLaunchBinding(controlPipeLaunchBinding.InstanceId);
            }
            finally
            {
                lock (stateGate)
                {
                    if (ReferenceEquals(activeStartCompletion, startCompletion))
                        activeStartCompletion = null;
                }
                startCompletion.TrySetResult();
            }
        }
    }

    private async Task StopCancelledSuccessfulStartAsync(
        OverviewStartResult start,
        string session,
        string nonce)
    {
        try
        {
            JsonElement result = await RunHelperAsync(
                new OverviewHelperInvocation(
                    "stop",
                    profileId,
                    session,
                    nonce,
                    start.GamePid,
                    start.GamePath,
                    start.GameStartedAtUtc),
                CancellationToken.None).ConfigureAwait(false);
            ValidateStopResult(
                result,
                profileId,
                session,
                start.GamePid,
                start.GamePath,
                start.GameStartedAtUtc,
                requireCurrentClientEvidence);
            if (testHooks is null)
                WriteHostStopEvidence(session, result);
        }
        catch (Exception ex)
        {
            try { SetDesiredRunning(false); } catch { }
            throw new BridgeCommandException(
                "GAME_CLOSE_FAILED",
                "LWBridge closed while the game was starting, but the owned game could not be restored cleanly.",
                new { error = ex.Message });
        }

        if (bridgeControlPipeLaunchBindingEnabled)
            bridgeHostState?.CancelLaunchBinding(session);
        ResetCancelledStartState();
    }

    private void ResetCancelledStartState()
    {
        string? ownedSession;
        string? ownedChallenge;
        lock (stateGate)
        {
            ownedSession = instanceId;
            ownedChallenge = challenge;
        }
        StopLeaseTimer(deleteLease: true, ownedSession, ownedChallenge);
        ClearRuntimeSessionFiles(ownedSession, ownedChallenge);
        if (ownedSession is not null && ownedChallenge is not null)
            RemoveAdoptionRecord(ownedSession, ownedChallenge);
        lock (stateGate)
        {
            phase = "stopped";
            connectionState = "offline";
            instanceId = null;
            instanceStartedAtUnixMilliseconds = null;
            challenge = null;
            gamePid = null;
            recoveryTracked = null;
            launcherPid = null;
            gamePath = null;
            gameStartedAtUtc = null;
            lastError = "GAME_OPERATION_CANCELLED";
            readyAtUnix = null;
        }
        try { SetDesiredRunning(false); } catch { }
    }

    private async Task EnsureControlPipeHostStartedAsync(string selectedRoot)
    {
        if (!bridgeControlPipeLaunchBindingEnabled || testHooks is not null)
            return;
        if (bridgeHostState is null)
        {
            throw new BridgeCommandException(
                "BRIDGE_HOST_UNAVAILABLE",
                "The shared bridge host is required for control-pipe launch binding.");
        }

        string expectedClientPath =
            LWBridgeControlPipeClientPathContract
                .BuildExpectedGameExecutablePath(selectedRoot);
        try
        {
            await bridgeHostState.EnsureRpcTransportAsync(
                BridgeVersion,
                expectedClientPath,
                permitPerRegistrationClientPath: true).ConfigureAwait(false);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new BridgeCommandException(
                "BRIDGE_HOST_UNAVAILABLE",
                "The shared bridge control-pipe listener could not start.",
                new { error = error.Message });
        }
    }

    private LWBridgeControlPipeLaunchBinding? PrepareControlPipeLaunchBinding(
        string sessionId)
    {
        if (!bridgeControlPipeLaunchBindingEnabled)
            return null;
        if (bridgeHostState is null)
        {
            throw new BridgeCommandException(
                "BRIDGE_HOST_UNAVAILABLE",
                "The shared bridge host is required for control-pipe launch binding.");
        }

        return bridgeHostState.PrepareLaunchBinding(
            profileId,
            sessionId,
            BridgeVersion,
            ControlPipeClockMilliseconds(),
            gameRoot is null ? null :
                LWBridgeControlPipeClientPathContract
                    .BuildExpectedGameExecutablePath(gameRoot));
    }

    private void RefreshControlPipeLaunchBinding(
        LWBridgeControlPipeLaunchBinding? binding)
    {
        if (binding is null)
            return;
        bridgeHostState!.RefreshLaunchBinding(
            binding.InstanceId,
            ControlPipeClockMilliseconds());
    }

    private long ControlPipeClockMilliseconds() =>
        testHooks?.MonotonicMilliseconds?.Invoke() ??
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    internal OverviewHelperInvocation CreateBoundedStartInvocation(
        string sessionId,
        string sessionChallenge,
        long deadline,
        LWBridgeControlPipeLaunchBinding? controlPipeLaunchBinding)
    {
        long remainingMilliseconds = deadline - RecoveryClockMilliseconds();
        if (remainingMilliseconds < MinimumStartWindowMilliseconds)
            throw new BridgeCommandException("BRIDGE_START_TIMEOUT",
                "The official client settled, but no bounded start window remained for the Overview bridge.");
        // HOME 009 R2 D: `--timeout-seconds` is the launcher->game ACQUISITION budget only. The 90 s bridge-connect window
        // starts at the launcher's game report (helper-side) and is carved out of the outer supervision window here.
        int timeoutSeconds = (int)Math.Min(120, (remainingMilliseconds - BridgeReadyWindowMilliseconds - StartCleanupMarginMilliseconds) / 1000);
        int supervisionMilliseconds = (int)Math.Min(int.MaxValue, remainingMilliseconds);
        return new OverviewHelperInvocation(
            "start", profileId, sessionId, sessionChallenge, null, null, null,
            timeoutSeconds, supervisionMilliseconds,
            controlPipeLaunchBinding);
    }

    private async Task<object?> StopAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        OwnedSnapshot snapshot;
        bool cancelPendingRecovery = false;
        Task? pendingStart = null;
        lock (stateGate)
        {
            if (closed) throw new BridgeCommandException("GAME_OPERATION_CANCELLED", "LWBridge is closing.");
            if (phase == "stopping")
                throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS", "A game lifecycle operation is already in progress.");
            // An old exited PID may remain published while recovery is already
            // launching its successor. The ordinary owned Stop also waits for
            // that replacement's exact process/launcher cancellation cleanup.
            pendingStart = activeStartCompletion?.Task;
            if (phase == "starting" ||
                gamePid is null || instanceId is null || challenge is null || gamePath is null)
            {
                // User Stop can retire a pending automatic recovery launch before
                // it publishes a new exact process. The run's cancellation
                // token drives late successful helper cleanup; inventing a
                // process here would risk terminating an unrelated installation.
                if (activeRecoveryRun is null || config?.Snapshot.GameDesiredRunning != true)
                {
                    if (phase == "starting")
                        throw new BridgeCommandException("GAME_OPERATION_IN_PROGRESS",
                            "A game lifecycle operation is already in progress.");
                    throw new BridgeCommandException("INSTANCE_NOT_OWNED",
                        "No LWBridge-owned game instance is active.");
                }
                // A recovery helper may already be starting a replacement
                // instance, even though no PID has been reported yet. Compare
                // the explicit target with that pending owner before clearing
                // desired-running or cancelling the helper. Absent/non-string
                // IDs retain the original optional-ID Stop behavior.
                if (payload.TryGetProperty("instanceId", out JsonElement pendingTarget) &&
                    pendingTarget.ValueKind == JsonValueKind.String &&
                    !string.Equals(pendingTarget.GetString(), instanceId, StringComparison.Ordinal))
                    throw new BridgeCommandException("INSTANCE_MISMATCH", "INSTANCE_MISMATCH");
                SetDesiredRunning(false);
                cancelPendingRecovery = true;
                snapshot = default!;
            }
            else
            {
                // Original 0.3.17 profile_instance_stop: 0x199D4B-0x199D6F
                // distinguishes an explicitly different instance identity from
                // the absent/unusable owner. Do not terminate or clear the active
                // owner when a delayed Close targets a prior instance.
                // Original 0x199AC4-0x199B38 extracts an optional JSON string;
                // missing/non-string values skip the compare at 0x199D4B.
                // The captured active owner still supplies all process/session
                // identity to the helper and restoration path.
                if (payload.TryGetProperty("instanceId", out JsonElement supplied) &&
                    supplied.ValueKind == JsonValueKind.String &&
                    !string.Equals(supplied.GetString(), instanceId, StringComparison.Ordinal))
                    throw new BridgeCommandException("INSTANCE_MISMATCH", "INSTANCE_MISMATCH");
                SetDesiredRunning(false);
                phase = "stopping";
                connectionState = "recovering";
                snapshot = SnapshotLocked();
            }
        }
        if (cancelPendingRecovery)
        {
            InvalidateRecoveryForUserStop();
            await AwaitCancelledRecoveryStartCleanupAsync(pendingStart).ConfigureAwait(false);
            return CreateInstanceStatus();
        }
        // 0x41bbff: the original Stop turns desired-running off, clears the tracked PID, increments the run id
        // (invalidating any recovery run) and resets the recovery status to idle (0x41ad16) before terminating.
        InvalidateRecoveryForUserStop();

        try
        {
            JsonElement result = await RunHelperAsync(
                new OverviewHelperInvocation("stop", profileId, snapshot.InstanceId, snapshot.Challenge, snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc),
                cancellationToken).ConfigureAwait(false);
            ValidateStopResult(result, profileId, snapshot.InstanceId, snapshot.GamePid, snapshot.GamePath, snapshot.GameStartedAtUtc, requireCurrentClientEvidence);
            if (testHooks is null) WriteHostStopEvidence(snapshot.InstanceId, result);
            if (bridgeControlPipeLaunchBindingEnabled)
                bridgeHostState?.CancelLaunchBinding(snapshot.InstanceId);
            RemoveAdoptionRecord(snapshot.InstanceId, snapshot.Challenge);
            StopLeaseTimer(deleteLease: true, snapshot.InstanceId, snapshot.Challenge);
            ClearRuntimeSessionFiles(snapshot.InstanceId, snapshot.Challenge);
            lock (stateGate)
            {
                phase = "stopped";
                connectionState = "offline";
                instanceId = null;
                instanceStartedAtUnixMilliseconds = null;
                challenge = null;
                gamePid = null;
                recoveryTracked = null;
                launcherPid = null;
                gamePath = null;
                gameStartedAtUtc = null;
                lastError = null;
                readyAtUnix = null;
            }
            await AwaitCancelledRecoveryStartCleanupAsync(pendingStart).ConfigureAwait(false);
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

    private async Task AwaitCancelledRecoveryStartCleanupAsync(Task? pendingStart)
    {
        // A recovery observation can advance from the last pre-Stop status
        // read into StartAsync before run invalidation takes its stateGate.
        // Capture that just-admitted Start as well, without waiting on a
        // previously rejected manual launch.
        if (pendingStart is null)
        {
            lock (stateGate) pendingStart = activeStartCompletion?.Task;
        }
        if (pendingStart is not null)
        {
            // 0x41bbff invalidates the run first; this additional current-client
            // barrier ensures a cancelled official-launcher child cannot start
            // AFTER Stop reports success. It is bounded independently of a
            // cancelled Start and never assumes ownership of a foreign process.
            Task finished = await Task.WhenAny(pendingStart,
                Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, pendingStart))
                throw new BridgeCommandException("GAME_CLOSE_TIMEOUT",
                    "The cancelled recovery launcher has not finished its owned cleanup.");
        }
        if (pendingStart is not null && testHooks is null && gameRoot is not null &&
            (FindSelectedGameProcess(gameRoot) is not null || IsUpdateProcessRunning()))
            throw new BridgeCommandException("GAME_CLOSE_FAILED",
                "The cancelled recovery launcher or game is still running; Stop cannot be confirmed.");
    }

    private async Task<JsonElement> RunHelperAsync(OverviewHelperInvocation invocation, CancellationToken cancellationToken)
    {
        bool isStart = invocation.Operation == "start" &&
            invocation.SessionId is not null &&
            invocation.Challenge is not null;
        CancellationTokenRegistration startCancellationRegistration = isStart
            ? cancellationToken.Register(() =>
                TryWriteStartCancellationMarker(invocation.SessionId!, invocation.Challenge!))
            : default;
        CancellationTokenSource? observerCancellation = null;
        Task<BridgeCommandException?>? observer = null;
        if (isStart && invocation.ControlPipeLaunchBinding is not null && bridgeHostState is not null)
        {
            observerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            observer = ObserveGameReportAsync(invocation, observerCancellation.Token);
        }
        try
        {
            JsonElement helperResult = await RunHelperCoreAsync(invocation, cancellationToken).ConfigureAwait(false);
            if (observer is not null)
            {
                // The helper can fulfill while its independent report reader has not yet
                // completed (or has already failed). Success must own BOTH outcomes.
                // A real successful start writes game-reported.txt before ready.json.
                // The local five-second handoff cap is a conservative adapter fence,
                // not a claim about the original launcher's timing.
                Task completed = await Task.WhenAny(
                    observer, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)).ConfigureAwait(false);
                if (completed != observer)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new BridgeCommandException(
                        "LAUNCH_REPORT_FAILED", "The game launch report was not registered.");
                }
                BridgeCommandException? refreshError = await observer.ConfigureAwait(false);
                if (refreshError is not null) throw refreshError;
            }
            // StartAsync must receive a successful helper's owned game identity
            // even when Close/Stop raced it, so that its exact-identity rollback
            // can terminate and restore the just-started process.
            return helperResult;
        }
        catch when (observer is not null && observer.IsCompletedSuccessfully && observer.Result is not null)
        {
            // Original 0x1DD152-0x1DD198: a failed registry refresh precedes
            // the consequent helper cancellation, rejection OR late fulfillment.
            throw observer.Result;
        }
        finally
        {
            startCancellationRegistration.Dispose();
            if (observerCancellation is not null)
            {
                observerCancellation.Cancel();
                try { if (observer is not null) await observer.ConfigureAwait(false); } catch { }
                observerCancellation.Dispose();
            }
            if (isStart)
                ClearStartCancellationMarker(invocation.SessionId!, invocation.Challenge!);
        }
    }

    // HOME 009 R2 D. The helper reports the launcher's game PID and the wall-clock deadline of the 90 s bridge-connect window
    // in runtime/game-reported.txt (0x1DD114-0x1DD11F); the host then performs refresh_pending(key, deadline) (0x1DD152) so the
    // pending pipe registration lives exactly as long as the readiness wait. Returns the refresh error, if any (the start is
    // cancelled through the existing start-cancellation marker so the helper restores and closes its owned game).
    private async Task<BridgeCommandException?> ObserveGameReportAsync(OverviewHelperInvocation invocation, CancellationToken token)
    {
        LWBridgeControlPipeLaunchBinding binding = invocation.ControlPipeLaunchBinding!;
        string path = Path.Combine(runtimeRoot, "game-reported.txt");
        try
        {
            while (!token.IsCancellationRequested)
            {
                // The report may already exist when the observer starts; do not
                // force an initial sleep across the launch/registration handoff.
                token.ThrowIfCancellationRequested();
                byte[] bytes;
                try { bytes = (testHooks?.ReadAllBytes ?? File.ReadAllBytes)(path); }
                catch
                {
                    await RecoveryDelayAsync(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(false);
                    continue;
                }
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string line in Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) values[line[..eq]] = line[(eq + 1)..].TrimEnd('\r');
                }
                if (!values.TryGetValue("schema", out string? schema) || schema != "1" ||
                    !values.TryGetValue("sessionId", out string? session) || session != invocation.SessionId ||
                    !values.TryGetValue("challenge", out string? challenge) || challenge != invocation.Challenge ||
                    !values.TryGetValue("deadlineMilliseconds", out string? deadlineText) ||
                    !long.TryParse(deadlineText, NumberStyles.None, CultureInfo.InvariantCulture, out long deadline) ||
                    !values.TryGetValue("gamePid", out string? gamePidText) ||
                    !int.TryParse(gamePidText, NumberStyles.None, CultureInfo.InvariantCulture, out int reportedGamePid) ||
                    reportedGamePid <= 0)
                {
                    await RecoveryDelayAsync(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(false);
                    continue;
                }
                try
                {
                    token.ThrowIfCancellationRequested();
                    bridgeHostState!.RefreshLaunchBindingUntil(binding.InstanceId, deadline);
                    // The current-client helper cannot expose connect-capable
                    // control until refresh_pending has succeeded. This is an
                    // atomic, exact-session handoff (not a ready.json shortcut).
                    string ackPath = Path.Combine(runtimeRoot, "registration-confirmed.txt");
                    Directory.CreateDirectory(runtimeRoot);
                    byte[] ackContents = Encoding.UTF8.GetBytes(
                        "schema=1\n" +
                        "sessionId=" + invocation.SessionId + "\n" +
                        "challenge=" + invocation.Challenge + "\n" +
                        "instanceId=" + binding.InstanceId + "\n" +
                        "gamePid=" + reportedGamePid.ToString(CultureInfo.InvariantCulture) + "\n");
                    token.ThrowIfCancellationRequested();
                    if (!OverviewRuntimeFileOwnership.TryWrite(
                        ackPath, ackContents,
                        bytes => KeyValueRuntimeFileMatches(
                            bytes, invocation.SessionId!, invocation.Challenge!),
                        testHooks?.RuntimeFileBeforeMutation))
                    {
                        throw new BridgeCommandException(
                            "LAUNCH_REPORT_FAILED",
                            "The exact-session registration acknowledgement is unavailable.");
                    }
                    testHooks?.LaunchReportRegistered?.Invoke(invocation);
                    return null;
                }
                catch (BridgeCommandException error)
                {
                    TryWriteStartCancellationMarker(invocation.SessionId!, invocation.Challenge!);
                    return error;
                }
                catch (IOException error)
                {
                    TryWriteStartCancellationMarker(invocation.SessionId!, invocation.Challenge!);
                    return new BridgeCommandException("LAUNCH_REPORT_FAILED",
                        "The launch report could not be acknowledged.", new { error = error.Message });
                }
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    private async Task<JsonElement> RunHelperCoreAsync(OverviewHelperInvocation invocation, CancellationToken cancellationToken)
    {
        if (testHooks?.RunHelperAsync is { } testRunner)
            return await testRunner(invocation, cancellationToken).ConfigureAwait(false);

        string operationHelperPath = invocation.Operation == "preflight-recover"
            ? Path.Combine(Path.GetDirectoryName(helperPath) ?? AppContext.BaseDirectory,
                "recover_overview_pending_current.py")
            : helperPath;
        if (!File.Exists(operationHelperPath))
            throw new InvalidOperationException($"Overview helper is missing: {operationHelperPath}");

        var start = new ProcessStartInfo
        {
            FileName = "python",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(operationHelperPath) ?? AppContext.BaseDirectory,
        };
        start.Environment["LWBRIDGE_REBUILD_DATA_ROOT"] = applicationDataRoot;
        start.ArgumentList.Add(operationHelperPath);
        if (invocation.Operation != "preflight-recover")
            start.ArgumentList.Add(invocation.Operation);
        if (gameRoot is not null)
        {
            start.ArgumentList.Add("--game-root");
            start.ArgumentList.Add(gameRoot);
        }
        if (invocation.Operation == "preflight-recover")
        {
            // The recovery helper needs only the selected game root supplied above.
        }
        else if (invocation.Operation == "start")
        {
            start.ArgumentList.Add("--profile-id"); start.ArgumentList.Add(invocation.ProfileId);
            start.ArgumentList.Add("--session-id"); start.ArgumentList.Add(invocation.SessionId!);
            start.ArgumentList.Add("--challenge"); start.ArgumentList.Add(invocation.Challenge!);
            int timeoutSeconds = invocation.TimeoutSeconds ?? 120;
            start.ArgumentList.Add("--timeout-seconds");
            start.ArgumentList.Add(timeoutSeconds.ToString(CultureInfo.InvariantCulture));
            if (invocation.ControlPipeLaunchBinding is not null)
            {
                start.ArgumentList.Add("--control-pipe-path");
                start.ArgumentList.Add(LWBridgeControlPipeContract.GetCurrentUserFullPath());
            }
        }
        else
        {
            start.ArgumentList.Add("--profile-id"); start.ArgumentList.Add(invocation.ProfileId);
            start.ArgumentList.Add("--session-id"); start.ArgumentList.Add(invocation.SessionId!);
            start.ArgumentList.Add("--challenge"); start.ArgumentList.Add(invocation.Challenge!);
            start.ArgumentList.Add("--game-pid"); start.ArgumentList.Add(invocation.GamePid!.Value.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add("--game-path"); start.ArgumentList.Add(invocation.GamePath!);
            if (!string.IsNullOrWhiteSpace(invocation.GameStartedAtUtc))
            {
                start.ArgumentList.Add("--game-started-at-utc"); start.ArgumentList.Add(invocation.GameStartedAtUtc);
            }
        }

        invocation.ControlPipeLaunchBinding?.ApplyTo(start);

        Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Python Overview bridge helper could not be started.");
        lock (stateGate) activeHelperProcess = process;
        bool retainHelperOwnership = false;
        try
        {
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task exitTask = process.WaitForExitAsync(CancellationToken.None);
            TimeSpan supervision = invocation.SupervisionMilliseconds is int requestedSupervision
                ? TimeSpan.FromMilliseconds(requestedSupervision)
                : helperSupervisionTimeout;
            Task completed = await Task.WhenAny(exitTask, Task.Delay(supervision, CancellationToken.None)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, exitTask))
            {
                retainHelperOwnership = true;
                _ = ReleaseRetainedHelperAfterExitAsync(process, exitTask, stdoutTask, stderrTask);
                throw new TimeoutException($"Overview bridge helper exceeded {supervision.TotalSeconds:0.#} seconds; the helper retains cleanup ownership.");
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
                    string? errorType = root.TryGetProperty("errorType", out JsonElement errorTypeElement) && errorTypeElement.ValueKind == JsonValueKind.String
                        ? errorTypeElement.GetString()
                        : null;
                    if (invocation.Operation == "start" &&
                        error.StartsWith("Overview start cancelled by closing LWBridge", StringComparison.Ordinal))
                    {
                        throw new BridgeCommandException(
                            "GAME_OPERATION_CANCELLED",
                            "LWBridge closed while the game was starting.",
                            new { error });
                    }
                    if (string.Equals(errorType, "CurrentClientCompatibilityError", StringComparison.Ordinal))
                        throw new BridgeCommandException(
                            "GAME_UPDATE_UNSUPPORTED",
                            "Last War updated, but LWBridge could not verify this game version as automatically compatible.",
                            new { error });
                    if (string.Equals(errorType, "ServerMaintenanceError", StringComparison.Ordinal))
                        throw new BridgeCommandException(
                            "SERVER_MAINTENANCE",
                            "Last War servers are currently under maintenance. Scanning is temporarily unavailable.",
                            new { error, loginCode = "E005", loadingCode = "E109" });
                    if (invocation.Operation == "start" &&
                        string.Equals(errorType, "LauncherSpawnError", StringComparison.Ordinal))
                        throw new BridgeCommandException(
                            "LAUNCH_TASK_FAILED", "LAUNCH_TASK_FAILED",
                            new { error });
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
            if (!root.TryGetProperty("currentClient", out JsonElement current) || current.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Overview helper did not return current-client compatibility evidence.");
            _ = CurrentClientCompatibility.ValidateCurrentClient(current);
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
        if (requireCurrentClientEvidence)
            CurrentClientCompatibility.ValidateRestore(restore);
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
        if (bridgeControlPipeLaunchBindingEnabled && testHooks is null &&
            (bridgeHostState is null || !bridgeHostState.IsRouteConnected(snapshot.InstanceId)))
            return false;  // A current ready.json/heartbeat is NOT authenticated host registration.
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
                RecoveryNow().ToUnixTimeSeconds());
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

    // Starts the lease timer for the exact current owner. Returns false, without
    // touching any existing timer, when the caller no longer owns the service
    // (closed, replaced, stopped or superseded by a successor session).
    private bool StartLeaseTimer(string session, string nonce)
    {
        lock (stateGate)
        {
            if (closed ||
                !string.Equals(instanceId, session, StringComparison.Ordinal) ||
                !string.Equals(challenge, nonce, StringComparison.Ordinal))
                return false;
            StopLeaseTimer(deleteLease: false);
            lock (leaseTimerGate)
            {
                long generation = Interlocked.Increment(ref leaseGeneration);
                leaseTimerSession = session;
                leaseTimerChallenge = nonce;
                leaseTimer = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        OwnedSnapshot? snapshot = GetOwnedSnapshot();
                        if (snapshot is null ||
                            !string.Equals(snapshot.InstanceId, session, StringComparison.Ordinal) ||
                            !string.Equals(snapshot.Challenge, nonce, StringComparison.Ordinal))
                        {
                            // The owner this timer was created for is gone; a
                            // late-started orphan retires itself without touching
                            // any successor's timer.
                            StopLeaseTimer(deleteLease: false, session, nonce);
                            return;
                        }
                        if (snapshot.Phase != "running") return;
                        WriteLease(session, nonce, generation);
                    }
                    catch { }
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            }
            return true;
        }
    }

    // With an expected owner the timer is disposed only when it belongs to that
    // exact owner; a late retirement of an older owner can never disable a
    // successor's renewal. Without one the stop is unconditional (service Close,
    // profile replacement). The lease file is always deleted by exact ownership.
    private void StopLeaseTimer(
        bool deleteLease,
        string? expectedSession = null,
        string? expectedChallenge = null)
    {
        System.Threading.Timer? timer = null;
        lock (leaseTimerGate)
        {
            bool ownedByCaller = expectedSession is null || expectedChallenge is null ||
                leaseTimer is null ||
                (string.Equals(leaseTimerSession, expectedSession, StringComparison.Ordinal) &&
                 string.Equals(leaseTimerChallenge, expectedChallenge, StringComparison.Ordinal));
            if (ownedByCaller)
            {
                Interlocked.Increment(ref leaseGeneration);
                timer = Interlocked.Exchange(ref leaseTimer, null);
                leaseTimerSession = null;
                leaseTimerChallenge = null;
            }
        }
        timer?.Dispose();
        if (!deleteLease || expectedSession is null || expectedChallenge is null) return;
        lock (leaseWriteGate)
        {
            TryDeleteOwnedRuntimeFile(
                Path.Combine(runtimeRoot, "lease.txt"),
                expectedSession,
                expectedChallenge,
                json: false);
            try
            {
                if (Directory.Exists(runtimeRoot))
                    foreach (string temp in Directory.EnumerateFiles(runtimeRoot, "lease.txt.tmp-*"))
                        TryDeleteOwnedRuntimeFile(temp, expectedSession, expectedChallenge, json: false);
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
            byte[] contents = System.Text.Encoding.UTF8.GetBytes(
                "schema=1\n" +
                $"bridgeVersion={BridgeVersion}\n" +
                $"sessionId={session}\n" +
                $"challenge={nonce}\n" +
                $"updatedAt={DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}\n");
            if (generation != Volatile.Read(ref leaseGeneration)) return;
            _ = OverviewRuntimeFileOwnership.TryWrite(
                path, contents,
                bytes => KeyValueRuntimeFileMatches(bytes, session, nonce),
                testHooks?.RuntimeFileBeforeMutation);
            if (generation != Volatile.Read(ref leaseGeneration))
                TryDeleteOwnedRuntimeFile(path, session, nonce, json: false);
        }
    }

    private void TryWriteStartCancellationMarker(string session, string nonce)
    {
        try { WriteStartCancellationMarker(session, nonce); }
        catch { }
    }

    private void WriteStartCancellationMarker(string session, string nonce)
    {
        lock (leaseWriteGate)
        {
            if (testHooks?.WriteStartCancellation is { } test)
            {
                test(session, nonce, runtimeRoot);
                return;
            }
            Directory.CreateDirectory(runtimeRoot);
            string path = Path.Combine(runtimeRoot, StartCancellationFileName);
            byte[] contents = System.Text.Encoding.UTF8.GetBytes(
                "schema=1\n" +
                $"sessionId={session}\n" +
                $"challenge={nonce}\n");
            _ = OverviewRuntimeFileOwnership.TryWrite(
                path, contents,
                bytes => KeyValueRuntimeFileMatches(bytes, session, nonce),
                testHooks?.RuntimeFileBeforeMutation);
        }
    }

    private void ClearStartCancellationMarker(string expectedSession, string expectedChallenge)
    {
        lock (leaseWriteGate)
        {
            TryDeleteOwnedRuntimeFile(
                Path.Combine(runtimeRoot, StartCancellationFileName),
                expectedSession,
                expectedChallenge,
                json: false);
            try
            {
                if (Directory.Exists(runtimeRoot))
                    foreach (string temp in Directory.EnumerateFiles(runtimeRoot, StartCancellationFileName + ".tmp-*"))
                        TryDeleteOwnedRuntimeFile(temp, expectedSession, expectedChallenge, json: false);
            }
            catch { }
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

    private void ClearRuntimeSessionFiles(string? expectedSession, string? expectedChallenge)
    {
        if (expectedSession is null || expectedChallenge is null) return;
        TryDeleteOwnedRuntimeFile(Path.Combine(runtimeRoot, "lease.txt"), expectedSession, expectedChallenge, json: false);
        TryDeleteOwnedRuntimeFile(Path.Combine(runtimeRoot, "control.txt"), expectedSession, expectedChallenge, json: false);
        TryDeleteOwnedRuntimeFile(Path.Combine(runtimeRoot, "ready.json"), expectedSession, expectedChallenge, json: true);
        TryDeleteOwnedRuntimeFile(Path.Combine(runtimeRoot, "heartbeat.json"), expectedSession, expectedChallenge, json: true);
        TryDeleteOwnedRuntimeFile(Path.Combine(runtimeRoot, "registration-confirmed.txt"), expectedSession, expectedChallenge, json: false);
        ClearStartCancellationMarker(expectedSession, expectedChallenge);
    }

    private void TryDeleteOwnedRuntimeFile(
        string path,
        string expectedSession,
        string expectedChallenge,
        bool json)
    {
        try
        {
            _ = OverviewRuntimeFileOwnership.TryDelete(
                path,
                bytes => json
                    ? JsonRuntimeFileMatches(bytes, expectedSession, expectedChallenge)
                    : KeyValueRuntimeFileMatches(bytes, expectedSession, expectedChallenge),
                testHooks?.RuntimeFileBeforeMutation);
        }
        catch { }
    }

    private static bool JsonRuntimeFileMatches(
        byte[] bytes,
        string expectedSession,
        string expectedChallenge)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
                if (!keys.Add(property.Name)) return false;
            return MatchesString(root, "sessionId", expectedSession) &&
                   MatchesString(root, "challenge", expectedChallenge);
        }
        catch
        {
            return false;
        }
    }

    private static bool KeyValueRuntimeFileMatches(
        byte[] bytes,
        string expectedSession,
        string expectedChallenge)
    {
        try
        {
            string? session = null;
            string? challengeValue = null;
            string text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0) return false;
                string key = line[..separator];
                if (!keys.Add(key)) return false;
                string value = line[(separator + 1)..];
                if (key == "sessionId") session = value;
                else if (key == "challenge") challengeValue = value;
            }
            return string.Equals(session, expectedSession, StringComparison.Ordinal) &&
                   string.Equals(challengeValue, expectedChallenge, StringComparison.Ordinal);
        }
        catch
        {
            return false;
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
