using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record MapAutoScanConfig(
    bool Enabled,
    int IntervalMinutes,
    IReadOnlyList<int>? ServerIds,
    IReadOnlyList<string>? SelectedTypes,
    string ScanMode,
    bool ReturnToOriginalServer,
    long NextRunAt)
{
    internal static MapAutoScanConfig Default => new(
        false,
        60,
        Array.Empty<int>(),
        MapAutoScanCommandService.DefaultSelectedTypes,
        "fast",
        true,
        0);
}

internal sealed record MapAutoScanCycleRecord(
    string Outcome,
    long StartedAt,
    long CompletedAt,
    int OriginalServerId,
    IReadOnlyList<int> TargetServerIds,
    IReadOnlyList<int> CompletedServerIds,
    string? LastError);

internal sealed record MapAutoScanSnapshot(
    MapAutoScanConfig Config,
    bool Running,
    bool OwnsActiveScan,
    string? LastError,
    MapAutoScanCycleRecord? LastCycle,
    bool RecoveredInterruptedCycle,
    long Revision);

internal sealed record MapAutoScanRuntimeStatus(
    int ServerId,
    bool IsReading,
    string? LastError,
    string? ScanRunId,
    string Phase = "unknown");

internal sealed class MapAutoScanExecutionBoundary
{
    internal required Func<bool> IsOnline { get; init; }
    internal required Func<bool> IsMapScanActive { get; init; }
    internal required Func<string?, CancellationToken, Task<MapAutoScanRuntimeStatus>> ReadStatusAsync { get; init; }
    internal required Func<int, IReadOnlyList<string>, string, CancellationToken, Task<string>> StartTargetScanAsync { get; init; }
    internal required Func<int, CancellationToken, Task> ReturnServerAsync { get; init; }
    internal required Func<string, CancellationToken, Task<bool>> StopScanIfOwnedAsync { get; init; }
}

internal sealed class MapAutoScanSchedulerHooks
{
    internal Func<long>? UtcNowMilliseconds { get; init; }
    internal Func<TimeSpan, CancellationToken, Task>? DelayAsync { get; init; }
    // Deterministic test seam for the window between due read and admission.
    internal Func<Task>? BeforeDueAdmissionAsync { get; init; }
}

/// <summary>
/// Profile-owned Auto Scan scheduler/control-plane core. It intentionally consumes the
/// existing Map scan and server-jump boundaries instead of owning a second Map store.
/// </summary>
internal sealed class MapAutoScanCommandService : INativeAsyncCommandService, IDisposable, IAsyncDisposable
{
    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "local_map_auto_scan_status",
        "local_map_auto_scan_config_set",
        "local_map_auto_scan_run_now",
        "local_map_auto_scan_cancel",
    };
    internal const int MinimumIntervalMinutes = 20;
    internal const int MaximumIntervalMinutes = 1440;
    internal const int MaximumServerIds = 20;
    internal const int MinimumServerId = 1;
    internal const int MaximumServerId = 99999;
    internal const int DueTickMilliseconds = 5_000;
    internal const int ScanPollMilliseconds = 2_000;
    internal const int ScanTimeoutMilliseconds = 2_700_000;

    private const int StateSchemaVersion = 1;
    private const string RestartInterruptedError = "Auto scan interrupted by restart";

    internal static readonly string[] AllowedSelectedTypes =
    [
        "city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure", "other",
    ];

    internal static readonly string[] DefaultSelectedTypes =
    [
        "truck", "railway", "dispatch", "ghost", "treasure",
    ];

    private static readonly HashSet<string> AllowedSelectedTypeSet =
        new(AllowedSelectedTypes, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions StateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string statePath;
    private readonly MapAutoScanExecutionBoundary execution;
    private readonly Func<long> utcNowMilliseconds;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly Func<Task>? beforeDueAdmissionAsync;
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private readonly SemaphoreSlim stopGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly object cycleGate = new();
    private PersistedState state;
    private Task? schedulerTask;
    private Task? activeCycleTask;
    private CancellationTokenSource? activeCycleCancellation;
    private string? ownedScanRunId;
    private int retired;
    private int disposed;

    internal MapAutoScanCommandService(
        string statePath,
        MapAutoScanExecutionBoundary execution,
        MapAutoScanSchedulerHooks? hooks = null,
        bool startScheduler = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(execution.IsOnline);
        ArgumentNullException.ThrowIfNull(execution.IsMapScanActive);
        ArgumentNullException.ThrowIfNull(execution.ReadStatusAsync);
        ArgumentNullException.ThrowIfNull(execution.StartTargetScanAsync);
        ArgumentNullException.ThrowIfNull(execution.ReturnServerAsync);
        ArgumentNullException.ThrowIfNull(execution.StopScanIfOwnedAsync);

        this.statePath = Path.GetFullPath(statePath);
        this.execution = execution;
        utcNowMilliseconds = hooks?.UtcNowMilliseconds ??
            (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        delayAsync = hooks?.DelayAsync ?? Task.Delay;
        beforeDueAdmissionAsync = hooks?.BeforeDueAdmissionAsync;
        state = LoadState(this.statePath);
        ReconcileRestartState();
        if (startScheduler)
            schedulerTask = Task.Run(() => SchedulerLoopAsync(lifetimeCancellation.Token));
    }

    internal event Action<MapAutoScanSnapshot>? StateChanged;
    internal bool IsCycleBusy
    {
        get
        {
            lock (cycleGate) return activeCycleTask is { IsCompleted: false };
        }
    }
    private bool mapClearReserved;

    internal IDisposable AcquireMapClearLease()
    {
        lock (cycleGate)
        {
            if (mapClearReserved || activeCycleTask is { IsCompleted: false })
                throw new BridgeCommandException("AUTO_SCAN_RUNNING", "Stop the Auto scan cycle before clearing Map data.");
            mapClearReserved = true;
            return new MapClearReservation(this);
        }
    }

    private sealed class MapClearReservation(MapAutoScanCommandService owner) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.cycleGate) owner.mapClearReserved = false;
        }
    }

    public bool CanHandle(string command) => Commands.Contains(command);

    public async Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        return command switch
        {
            "local_map_auto_scan_status" => await GetSnapshotAsync(cancellationToken).ConfigureAwait(false),
            "local_map_auto_scan_config_set" => await UpdateConfigAsync(
                ReadConfig(payload), cancellationToken).ConfigureAwait(false),
            "local_map_auto_scan_run_now" => await RunNowAsync(cancellationToken).ConfigureAwait(false),
            "local_map_auto_scan_cancel" => await CancelAndReadAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED", $"Auto Scan cannot handle '{command}'."),
        };
    }

    internal static MapAutoScanConfig NormalizeConfig(MapAutoScanConfig? candidate)
    {
        MapAutoScanConfig source = candidate ?? MapAutoScanConfig.Default;
        int intervalMinutes = Math.Clamp(
            source.IntervalMinutes,
            MinimumIntervalMinutes,
            MaximumIntervalMinutes);

        var serverIds = new List<int>(MaximumServerIds);
        var seenServers = new HashSet<int>();
        foreach (int serverId in source.ServerIds ?? Array.Empty<int>())
        {
            if (serverId < MinimumServerId || serverId > MaximumServerId || !seenServers.Add(serverId))
                continue;
            serverIds.Add(serverId);
            if (serverIds.Count == MaximumServerIds)
                break;
        }

        var selectedTypes = new List<string>(AllowedSelectedTypes.Length);
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? kind in source.SelectedTypes ?? Array.Empty<string>())
        {
            if (kind is null || !AllowedSelectedTypeSet.Contains(kind) || !seenTypes.Add(kind))
                continue;
            selectedTypes.Add(kind);
        }
        if (selectedTypes.Count == 0)
            selectedTypes.AddRange(DefaultSelectedTypes);

        return new MapAutoScanConfig(
            source.Enabled,
            intervalMinutes,
            serverIds,
            selectedTypes,
            string.Equals(source.ScanMode, "normal", StringComparison.Ordinal) ? "normal" : "fast",
            source.ReturnToOriginalServer,
            Math.Max(0, source.NextRunAt));
    }

    internal async Task<MapAutoScanSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return CreateSnapshotLocked();
        }
        finally
        {
            stateGate.Release();
        }
    }

    internal async Task<MapAutoScanSnapshot> UpdateConfigAsync(
        MapAutoScanConfig candidate,
        CancellationToken cancellationToken = default)
    {
        ThrowIfRetired();
        ArgumentNullException.ThrowIfNull(candidate);

        bool disabling;
        MapAutoScanSnapshot snapshot;
        await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MapAutoScanConfig previous = state.Config;
            MapAutoScanConfig normalized = NormalizeConfig(candidate);
            // nextRunAt is scheduler-owned native state. Ordinary UI edits carry a
            // full config snapshot, so accepting their deadline would let a stale
            // browser snapshot undo a deadline advanced by a completed cycle.
            MapAutoScanConfig next = normalized with { NextRunAt = previous.NextRunAt };
            if (next.Enabled && !previous.Enabled)
                next = next with { NextRunAt = utcNowMilliseconds() };
            if (!next.Enabled)
                next = next with { NextRunAt = 0 };
            CommitLocked(state with { Config = next });
            disabling = !next.Enabled;
            snapshot = CreateSnapshotLocked();
        }
        finally
        {
            stateGate.Release();
        }

        Publish(snapshot);
        // Original: disabling only clears the persisted deadline; a running cycle
        // finishes its in-flight scan and observes `enabled` between targets (there
        // is no Auto-specific Stop; Manual Stop remains the scan Stop surface).
        if (!disabling)
            await TryAdmitDueCycleAsync().ConfigureAwait(false);

        return await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<MapAutoScanSnapshot> RunNowAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfRetired();
        MapAutoScanSnapshot snapshot;
        await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!state.Config.Enabled)
                throw new InvalidOperationException("Auto scan is disabled");
            CommitLocked(state with
            {
                Config = state.Config with { NextRunAt = utcNowMilliseconds() },
            });
            snapshot = CreateSnapshotLocked();
        }
        finally
        {
            stateGate.Release();
        }

        Publish(snapshot);
        await TryAdmitDueCycleAsync().ConfigureAwait(false);
        return await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> CheckDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfRetired();
        return await TryAdmitDueCycleAsync().ConfigureAwait(false);
    }

    internal async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfRetired();
        await CancelCycleAndStopOwnedScanAsync().ConfigureAwait(false);
        await WaitForIdleAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MapAutoScanSnapshot> CancelAndReadAsync(CancellationToken cancellationToken)
    {
        await CancelAsync(cancellationToken).ConfigureAwait(false);
        return await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        Task? active;
        lock (cycleGate)
            active = activeCycleTask;
        if (active is not null)
            await active.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task RetireAsync()
    {
        if (Interlocked.Exchange(ref retired, 1) != 0)
            return;

        lifetimeCancellation.Cancel();
        await CancelCycleAndStopOwnedScanAsync().ConfigureAwait(false);

        Task? active;
        lock (cycleGate)
            active = activeCycleTask;
        if (active is not null)
        {
            try { await active.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        Task? scheduler = schedulerTask;
        if (scheduler is not null)
        {
            try { await scheduler.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        try { RetireAsync().GetAwaiter().GetResult(); }
        finally
        {
            lock (cycleGate)
            {
                activeCycleCancellation?.Dispose();
                activeCycleCancellation = null;
            }
            lifetimeCancellation.Dispose();
            stateGate.Dispose();
            stopGate.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        try { await RetireAsync().ConfigureAwait(false); }
        finally
        {
            lock (cycleGate)
            {
                activeCycleCancellation?.Dispose();
                activeCycleCancellation = null;
            }
            lifetimeCancellation.Dispose();
            stateGate.Dispose();
            stopGate.Dispose();
        }
    }

    private async Task SchedulerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await TryAdmitDueCycleAsync().ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                await delayAsync(TimeSpan.FromMilliseconds(DueTickMilliseconds), cancellationToken)
                    .ConfigureAwait(false);
                await TryAdmitDueCycleAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task<bool> TryAdmitDueCycleAsync()
    {
        if (Volatile.Read(ref retired) != 0 || !execution.IsOnline() || execution.IsMapScanActive())
            return false;

        MapAutoScanConfig config;
        await stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            config = state.Config;
            if (!config.Enabled || utcNowMilliseconds() < config.NextRunAt)
                return false;
        }
        finally
        {
            stateGate.Release();
        }

        if (beforeDueAdmissionAsync is not null)
            await beforeDueAdmissionAsync().ConfigureAwait(false);

        // A config update (especially Disable) can commit after the first
        // due read. The final reservation must therefore re-read the durable
        // config under stateGate, and hold it until cycleGate publishes the
        // active owner. Keep the established lock order stateGate -> cycleGate.
        await stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            config = state.Config;
            if (!config.Enabled || utcNowMilliseconds() < config.NextRunAt)
                return false;
            lock (cycleGate)
            {
                if (mapClearReserved || Volatile.Read(ref retired) != 0 ||
                    activeCycleTask is { IsCompleted: false } ||
                    !execution.IsOnline() ||
                    execution.IsMapScanActive())
                    return false;

                activeCycleCancellation?.Dispose();
                activeCycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
                CancellationToken cycleToken = activeCycleCancellation.Token;
                activeCycleTask = Task.Run(() => RunCycleAsync(config, cycleToken));
                return true;
            }
        }
        finally
        {
            stateGate.Release();
        }
    }

    private async Task RunCycleAsync(MapAutoScanConfig admittedConfig, CancellationToken cancellationToken)
    {
        long startedAt = utcNowMilliseconds();
        int originalServerId = 0;
        int[] targets = Array.Empty<int>();
        var completedServers = new List<int>();
        bool attemptedOtherServer = false;
        int lastAttemptedServerId = 0;
        string? admissionScanRunId = null;
        string? lastOwnedScanRunId = null;
        string? cycleError = null;
        string outcome = "completed";

        await BeginCyclePersistenceAsync(startedAt).ConfigureAwait(false);
        try
        {
            MapAutoScanRuntimeStatus admissionState =
                await execution.ReadStatusAsync(null, cancellationToken).ConfigureAwait(false);
            originalServerId = admissionState.ServerId;
            admissionScanRunId = admissionState.ScanRunId;

            // Original cycle (index-BVfnK1wp.js Di()): an explicit server list is
            // used as supplied even when the current server is unknown (id 0); only
            // an EMPTY resolved list fails with MAP_AUTO_SCAN_SERVER_UNAVAILABLE.
            targets = admittedConfig.ServerIds is { Count: > 0 }
                ? admittedConfig.ServerIds.ToArray()
                : originalServerId > 0 ? [originalServerId] : Array.Empty<int>();
            if (targets.Length == 0)
                throw new InvalidOperationException("MAP_AUTO_SCAN_SERVER_UNAVAILABLE");
            await UpdateRestartProgressAsync(startedAt, originalServerId, targets, 0, null)
                .ConfigureAwait(false);

            for (int index = 0; index < targets.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await IsStillEnabledAsync().ConfigureAwait(false))
                {
                    outcome = "disabled";
                    break;
                }

                if (index > 0)
                {
                    // A completed first Auto run is still available through
                    // Map317's archived exact-run status after a separate
                    // manual scan starts and even FINISHES on that server.
                    // Do not navigate to the next Auto target using the old
                    // publication as authority over that manual successor.
                    await EnsureNoIndependentScanAsync(
                        "MAP_AUTO_SCAN_TARGET_OWNER_MISMATCH", lastAttemptedServerId,
                        originalServerId, admissionScanRunId, lastOwnedScanRunId,
                        cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                int serverId = targets[index];
                // A cancelled first target at the original server never left
                // home. Returning through the native scan lease while its
                // Stop is settling can fail as SCAN_RUNNING, despite no move.
                if (serverId != originalServerId) attemptedOtherServer = true;
                // The Map317 target boundary jumps to this server BEFORE it
                // starts the durable scan and returns a run ID. Cancellation
                // during that start leaves no run token, but the jump may have
                // happened; subsequent return still needs target provenance.
                lastAttemptedServerId = serverId;
                string scanRunId = await execution.StartTargetScanAsync(
                    serverId,
                    admittedConfig.SelectedTypes ?? DefaultSelectedTypes,
                    admittedConfig.ScanMode,
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(scanRunId))
                    throw new InvalidOperationException("Auto scan start did not return a scan owner token");
                lastOwnedScanRunId = scanRunId;
                Volatile.Write(ref ownedScanRunId, scanRunId);

                cancellationToken.ThrowIfCancellationRequested();

                long deadline = checked(utcNowMilliseconds() + ScanTimeoutMilliseconds);
                for (;;)
                {
                    await delayAsync(TimeSpan.FromMilliseconds(ScanPollMilliseconds), cancellationToken)
                        .ConfigureAwait(false);
                    if (utcNowMilliseconds() >= deadline)
                    {
                        // Original: the 45-minute wait throws MAP_AUTO_SCAN_TIMEOUT and
                        // issues NO map_scan_stop; the ordinary scan keeps running.
                        // Auto only releases its ownership marker.
                        Interlocked.CompareExchange(ref ownedScanRunId, null, scanRunId);
                        throw new TimeoutException("MAP_AUTO_SCAN_TIMEOUT");
                    }

                    MapAutoScanRuntimeStatus status =
                        await execution.ReadStatusAsync(scanRunId, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(status.ScanRunId, scanRunId, StringComparison.Ordinal) ||
                        status.ServerId != serverId)
                        throw new InvalidOperationException(
                            "MAP_AUTO_SCAN_OWNER_MISMATCH: scan result belongs to another run or server");
                    if (status.IsReading)
                        continue;

                    Interlocked.CompareExchange(ref ownedScanRunId, null, scanRunId);
                    // Not reading is not a publication acknowledgement. A stopped,
                    // failed or stale scan must never count as an Auto success.
                    if (!string.Equals(status.Phase, "completed", StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "MAP_AUTO_SCAN_NOT_PUBLISHED: " +
                            (status.LastError ?? "scan ended in phase " + status.Phase));
                    if (!string.IsNullOrWhiteSpace(status.LastError))
                        throw new InvalidOperationException("MAP_AUTO_SCAN_NOT_PUBLISHED: " + status.LastError);
                    completedServers.Add(serverId);
                    await UpdateRestartProgressAsync(
                        startedAt,
                        originalServerId,
                        targets,
                        index + 1,
                        cycleError).ConfigureAwait(false);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = Volatile.Read(ref retired) != 0
                ? "retired"
                : await IsStillEnabledAsync().ConfigureAwait(false) ? "canceled" : "disabled";
        }
        catch (Exception error)
        {
            outcome = "failed";
            cycleError = error.Message;
        }
        finally
        {
            // The cycle is the sole Stop owner. Cancel/retire only signal its
            // token, then await this task. Previously they launched a separate
            // Stop while this finally raced ahead to ReturnServerAsync, where
            // the exact native scan still held its process lease. Do not return
            // until the captured run's Stop has *settled*.
            bool stopSettled = true;
            try
            {
                await StopOwnedScanAsync(lastAttemptedServerId, originalServerId,
                    admissionScanRunId, lastOwnedScanRunId)
                    .ConfigureAwait(false);
            }
            catch (Exception stopError)
            {
                stopSettled = false;
                string stopFailure = "MAP_AUTO_SCAN_STOP_FAILED: " + stopError.Message;
                cycleError = cycleError is null ? stopFailure : cycleError + "; " + stopFailure;
                if (outcome != "retired") outcome = "failed";
            }
            if (Volatile.Read(ref retired) == 0 && stopSettled &&
                admittedConfig.ReturnToOriginalServer && originalServerId > 0 && attemptedOtherServer)
            {
                try
                {
                    // The last Auto run may have published and released its
                    // marker just before a manual scan became current. Its
                    // saved terminal status does not authorize Auto to move
                    // that successor's camera/server during return.
                    await EnsureNoIndependentScanAsync(
                        "MAP_AUTO_SCAN_RETURN_OWNER_MISMATCH", lastAttemptedServerId,
                        originalServerId, admissionScanRunId, lastOwnedScanRunId)
                        .ConfigureAwait(false);
                    // Profile replacement can retire Auto while the async
                    // current-status read is pending. A retired profile must
                    // not initiate or continue its old native navigation.
                    lifetimeCancellation.Token.ThrowIfCancellationRequested();
                    if (Volatile.Read(ref retired) != 0)
                        throw new OperationCanceledException(lifetimeCancellation.Token);
                    await execution.ReturnServerAsync(originalServerId, lifetimeCancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (Volatile.Read(ref retired) != 0)
                {
                    outcome = "retired";
                }
                catch (Exception returnError)
                {
                    // An explicitly requested return is part of the owner's Auto cycle.
                    // Keep the already-published targets, but do not describe the full
                    // cycle as completed if native navigation did not restore its origin.
                    string returnFailure = "MAP_AUTO_SCAN_RETURN_FAILED: " + returnError.Message;
                    cycleError = cycleError is null ? returnFailure : cycleError + "; " + returnFailure;
                    if (outcome != "retired") outcome = "failed";
                }
            }

            bool liveOwner = Volatile.Read(ref retired) == 0;
            if (!liveOwner) outcome = "retired";

            await FinishCyclePersistenceAsync(
                outcome,
                startedAt,
                originalServerId,
                targets,
                completedServers,
                cycleError,
                liveOwner).ConfigureAwait(false);
        }
    }

    private async Task BeginCyclePersistenceAsync(long startedAt)
    {
        MapAutoScanSnapshot snapshot;
        await stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CommitLocked(state with
            {
                LastError = null,
                RecoveredInterruptedCycle = false,
                Restart = new RestartState(true, startedAt, 0, Array.Empty<int>(), 0, null),
            });
            snapshot = CreateSnapshotLocked(runningOverride: true);
        }
        finally
        {
            stateGate.Release();
        }
        Publish(snapshot);
    }

    private async Task UpdateRestartProgressAsync(
        long startedAt,
        int originalServerId,
        IReadOnlyList<int> targets,
        int nextTargetIndex,
        string? error)
    {
        await stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CommitLocked(state with
            {
                Restart = new RestartState(
                    true,
                    startedAt,
                    originalServerId,
                    targets.ToArray(),
                    nextTargetIndex,
                    error),
            });
        }
        finally
        {
            stateGate.Release();
        }
    }

    private async Task FinishCyclePersistenceAsync(
        string outcome,
        long startedAt,
        int originalServerId,
        IReadOnlyList<int> targets,
        IReadOnlyList<int> completedServers,
        string? cycleError,
        bool advanceDeadline)
    {
        MapAutoScanSnapshot snapshot;
        await stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            long completedAt = utcNowMilliseconds();
            MapAutoScanConfig config = state.Config;
            if (advanceDeadline)
            {
                config = config.Enabled
                    ? config with
                    {
                        NextRunAt = checked(completedAt + config.IntervalMinutes * 60_000L),
                    }
                    : config with { NextRunAt = 0 };
            }

            CommitLocked(state with
            {
                Config = config,
                // A genuinely published new cycle resolves an earlier failure;
                // retain errors on failed/interrupted cycles instead of leaving
                // a stale failure banner next to a successful fresh result.
                LastError = outcome == "completed" ? null : cycleError ?? state.LastError,
                LastCycle = new MapAutoScanCycleRecord(
                    outcome,
                    startedAt,
                    completedAt,
                    originalServerId,
                    targets.ToArray(),
                    completedServers.ToArray(),
                    cycleError),
                Restart = RestartState.Inactive,
            });
            snapshot = CreateSnapshotLocked(runningOverride: false);
        }
        finally
        {
            stateGate.Release();
        }
        Publish(snapshot);
    }

    private async Task<bool> IsStillEnabledAsync()
    {
        await stateGate.WaitAsync().ConfigureAwait(false);
        try { return state.Config.Enabled; }
        finally { stateGate.Release(); }
    }

    private async Task CancelCycleAndStopOwnedScanAsync()
    {
        CancellationTokenSource? cycleCancellation;
        lock (cycleGate)
            cycleCancellation = activeCycleCancellation;
        cycleCancellation?.Cancel();
        // RunCycleAsync owns native Stop and the subsequent return ordering.
        // Its caller waits for the active task, never starts a racing Stop.
    }

    private async Task<bool> StopOwnedScanAsync(
        int lastAttemptedServerId, int originalServerId,
        string? admissionScanRunId, string? lastOwnedScanRunId)
    {
        await stopGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string? scanRunId = Volatile.Read(ref ownedScanRunId);
            if (string.IsNullOrWhiteSpace(scanRunId))
                return false;
            try
            {
                bool stopped = await execution.StopScanIfOwnedAsync(scanRunId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (!stopped)
                {
                    // A no-op Stop is safe only if this same run is already
                    // terminal. A successor or different server must not be
                    // redirected by this retired Auto cycle's return navigation.
                    MapAutoScanRuntimeStatus status = await execution.ReadStatusAsync(
                        scanRunId, CancellationToken.None).ConfigureAwait(false);
                    if (status.IsReading ||
                        !string.Equals(status.ScanRunId, scanRunId, StringComparison.Ordinal) ||
                        status.Phase is not ("completed" or "failed" or "canceled" or "cancelled" or "idle"))
                        throw new InvalidOperationException(
                            "MAP_AUTO_SCAN_STOP_OWNER_MISMATCH: exact run has no verified terminal state");

                    // ReadStatusAsync(exactRun) is allowed to return an old
                    // completed SQLite run while a different native run now
                    // owns the current process. A terminal old row alone
                    // cannot authorize the subsequent ReturnServerAsync.
                    await EnsureNoIndependentScanAsync(
                        "MAP_AUTO_SCAN_STOP_OWNER_MISMATCH", lastAttemptedServerId,
                        originalServerId, admissionScanRunId, lastOwnedScanRunId)
                        .ConfigureAwait(false);
                }
                return true;
            }
            finally
            {
                // Retire only our marker even if native Stop failed/rejected.
                // Leave any unrelated native successor completely untouched.
                Interlocked.CompareExchange(ref ownedScanRunId, null, scanRunId);
            }
        }
        finally
        {
            stopGate.Release();
        }
    }

    private async Task EnsureNoIndependentScanAsync(
        string failureCode, int lastAttemptedServerId, int originalServerId,
        string? admissionScanRunId, string? lastOwnedScanRunId,
        CancellationToken statusCancellation = default)
    {
        // The status boundary's null owner reads the CURRENT scan, rather
        // than falling back to an archived exact-run status. Also consult
        // the immediate active flag because an unrelated scan can enter
        // after that async read; native return retains its own lease check.
        MapAutoScanRuntimeStatus current = await execution.ReadStatusAsync(
            null, statusCancellation).ConfigureAwait(false);
        if (current.IsReading || execution.IsMapScanActive())
            throw new InvalidOperationException(failureCode + ": independent scan is active");
        // Server equality alone is not proof of an idle owner. Map317 keeps
        // the CURRENT ScanRunId even after a different manual scan completes
        // on the same server. Admit only this Auto cycle's last accepted run
        // or the pre-cycle idle run, which can remain after a canceled jump
        // that never issued its own run ID.
        if (!string.IsNullOrWhiteSpace(current.ScanRunId) &&
            !string.Equals(current.ScanRunId, lastOwnedScanRunId, StringComparison.Ordinal) &&
            !string.Equals(current.ScanRunId, admissionScanRunId, StringComparison.Ordinal))
            throw new InvalidOperationException(failureCode + ": independent terminal scan owner");
        // Prefer the LATEST attempted target, not an earlier accepted run:
        // Auto may have completed 318, attempted 319 and been canceled before
        // obtaining 319's run ID, then a manual owner navigated back to 318.
        // A failed 319 jump that never navigated is indistinguishable from that
        // manual return to 318. Reject both, conservatively; only the latest
        // attempted server and original home can authorize Auto's return.
        if (lastAttemptedServerId > 0 &&
            current.ServerId != lastAttemptedServerId &&
            current.ServerId != originalServerId)
            throw new InvalidOperationException(failureCode + ": live server moved outside Auto's cycle");
    }

    private async Task RecordErrorAsync(string error)
    {
        MapAutoScanSnapshot snapshot;
        await stateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CommitLocked(state with { LastError = error });
            snapshot = CreateSnapshotLocked();
        }
        finally
        {
            stateGate.Release();
        }
        Publish(snapshot);
    }

    private void ReconcileRestartState()
    {
        RestartState? restart = state.Restart;
        if (restart is null || !restart.Active)
            return;

        long completedAt = utcNowMilliseconds();
        CommitLocked(state with
        {
            LastError = RestartInterruptedError,
            LastCycle = new MapAutoScanCycleRecord(
                "interrupted",
                restart.StartedAt,
                completedAt,
                restart.OriginalServerId,
                restart.TargetServerIds,
                restart.TargetServerIds.Take(Math.Clamp(restart.NextTargetIndex, 0, restart.TargetServerIds.Count)).ToArray(),
                RestartInterruptedError),
            RecoveredInterruptedCycle = true,
            Restart = RestartState.Inactive,
        });
    }

    private static PersistedState LoadState(string path)
    {
        if (!File.Exists(path))
            return PersistedState.Default;
        try
        {
            PersistedState? loaded = JsonSerializer.Deserialize<PersistedState>(
                File.ReadAllText(path),
                StateJsonOptions);
            if (loaded is null || loaded.SchemaVersion != StateSchemaVersion)
                return PersistedState.Default;
            return loaded with
            {
                Config = NormalizeConfig(loaded.Config),
                Restart = loaded.Restart ?? RestartState.Inactive,
            };
        }
        catch (JsonException)
        {
            return PersistedState.Default;
        }
        catch (IOException)
        {
            return PersistedState.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return PersistedState.Default;
        }
    }

    private void CommitLocked(PersistedState candidate)
    {
        long revision = checked(state.Revision + 1);
        PersistedState committed = candidate with { Revision = revision };
        PersistState(committed);
        state = committed;
    }

    private void PersistState(PersistedState value)
    {
        string? directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = statePath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, StateJsonOptions));
            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); } catch { }
        }
    }

    private MapAutoScanSnapshot CreateSnapshotLocked(bool? runningOverride = null)
    {
        bool running;
        if (runningOverride.HasValue)
        {
            running = runningOverride.Value;
        }
        else
        {
            lock (cycleGate)
                running = activeCycleTask is { IsCompleted: false };
        }

        return new MapAutoScanSnapshot(
            state.Config,
            running,
            !string.IsNullOrWhiteSpace(Volatile.Read(ref ownedScanRunId)),
            state.LastError,
            state.LastCycle,
            state.RecoveredInterruptedCycle,
            state.Revision);
    }

    private void Publish(MapAutoScanSnapshot snapshot)
    {
        try { StateChanged?.Invoke(snapshot); }
        catch { }
    }

    private static MapAutoScanConfig ReadConfig(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("config", out JsonElement config) ||
            config.ValueKind != JsonValueKind.Object)
        {
            throw new BridgeCommandException("INVALID_PAYLOAD", "Auto Scan config object is required.");
        }

        bool enabled = config.TryGetProperty("enabled", out JsonElement enabledValue) &&
            enabledValue.ValueKind == JsonValueKind.True;
        int intervalMinutes = config.TryGetProperty("intervalMinutes", out JsonElement intervalValue) &&
            intervalValue.TryGetInt32(out int parsedInterval)
                ? parsedInterval
                : MapAutoScanConfig.Default.IntervalMinutes;
        var serverIds = new List<int>();
        if (config.TryGetProperty("serverIds", out JsonElement serversValue) &&
            serversValue.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in serversValue.EnumerateArray())
                if (item.TryGetInt32(out int serverId)) serverIds.Add(serverId);
        }

        var selectedTypes = new List<string>();
        if (config.TryGetProperty("selectedTypes", out JsonElement typesValue) &&
            typesValue.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in typesValue.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } kind)
                    selectedTypes.Add(kind);
        }

        string scanMode = config.TryGetProperty("scanMode", out JsonElement modeValue) &&
            modeValue.ValueKind == JsonValueKind.String
                ? modeValue.GetString() ?? "fast"
                : "fast";
        bool returnToOriginalServer = !config.TryGetProperty("returnToOriginalServer", out JsonElement returnValue) ||
            returnValue.ValueKind != JsonValueKind.False;
        long nextRunAt = config.TryGetProperty("nextRunAt", out JsonElement nextValue) &&
            nextValue.TryGetInt64(out long parsedNext)
                ? parsedNext
                : 0;

        return new MapAutoScanConfig(
            enabled,
            intervalMinutes,
            serverIds,
            selectedTypes,
            scanMode,
            returnToOriginalServer,
            nextRunAt);
    }

    private void ThrowIfRetired()
    {
        if (Volatile.Read(ref retired) != 0 || Volatile.Read(ref disposed) != 0)
            throw new ObjectDisposedException(nameof(MapAutoScanCommandService));
    }

    private sealed record PersistedState(
        int SchemaVersion,
        MapAutoScanConfig Config,
        string? LastError,
        MapAutoScanCycleRecord? LastCycle,
        bool RecoveredInterruptedCycle,
        RestartState? Restart,
        long Revision)
    {
        internal static PersistedState Default => new(
            StateSchemaVersion,
            MapAutoScanConfig.Default,
            null,
            null,
            false,
            RestartState.Inactive,
            0);
    }

    private sealed record RestartState(
        bool Active,
        long StartedAt,
        int OriginalServerId,
        IReadOnlyList<int> TargetServerIds,
        int NextTargetIndex,
        string? LastError)
    {
        internal static RestartState Inactive => new(false, 0, 0, Array.Empty<int>(), 0, null);
    }
}
