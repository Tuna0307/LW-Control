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
    string? ScanRunId);

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
        "city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure",
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
                if (Volatile.Read(ref retired) != 0 ||
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
        string? cycleError = null;
        string outcome = "completed";

        await BeginCyclePersistenceAsync(startedAt).ConfigureAwait(false);
        try
        {
            MapAutoScanRuntimeStatus admissionState =
                await execution.ReadStatusAsync(null, cancellationToken).ConfigureAwait(false);
            originalServerId = admissionState.ServerId;

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

                int serverId = targets[index];
                string scanRunId = await execution.StartTargetScanAsync(
                    serverId,
                    admittedConfig.SelectedTypes ?? DefaultSelectedTypes,
                    admittedConfig.ScanMode,
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(scanRunId))
                    throw new InvalidOperationException("Auto scan start did not return a scan owner token");
                Volatile.Write(ref ownedScanRunId, scanRunId);

                if (cancellationToken.IsCancellationRequested)
                {
                    await StopOwnedScanAsync().ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }

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
                    if (status.IsReading)
                        continue;

                    Interlocked.CompareExchange(ref ownedScanRunId, null, scanRunId);
                    completedServers.Add(serverId);
                    if (!string.IsNullOrWhiteSpace(status.LastError))
                    {
                        cycleError = status.LastError;
                        outcome = "completed_with_error";
                        await RecordErrorAsync(status.LastError).ConfigureAwait(false);
                    }
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
            await RecordErrorAsync(error.Message).ConfigureAwait(false);
        }
        finally
        {
            bool liveOwner = Volatile.Read(ref retired) == 0;
            if (liveOwner && admittedConfig.ReturnToOriginalServer && originalServerId > 0)
            {
                try
                {
                    await execution.ReturnServerAsync(originalServerId, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // The recovered frontend treats return-to-origin as best effort and
                    // does not replace the cycle's terminal error with a return failure.
                }
            }

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
                LastError = cycleError ?? state.LastError,
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
        await StopOwnedScanAsync().ConfigureAwait(false);
    }

    private async Task<bool> StopOwnedScanAsync()
    {
        await stopGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string? scanRunId = Volatile.Read(ref ownedScanRunId);
            if (string.IsNullOrWhiteSpace(scanRunId))
                return false;
            bool stopped = await execution.StopScanIfOwnedAsync(scanRunId, CancellationToken.None)
                .ConfigureAwait(false);
            Interlocked.CompareExchange(ref ownedScanRunId, null, scanRunId);
            return stopped;
        }
        finally
        {
            stopGate.Release();
        }
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
