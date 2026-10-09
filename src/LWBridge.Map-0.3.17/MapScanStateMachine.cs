namespace LWBridge.Map317;

public sealed record MapScanStartRequest(
    IReadOnlyList<string>? SelectedTypes = null,
    string? ScanMode = null,
    bool Resume = false);

public sealed record MapScanProgressUpdate(
    int CompletedBlocks,
    int ReadBlocks,
    int FailedBlocks,
    int InflightBlocks,
    double ScanRate,
    bool NativeCaptureReady,
    int NativePendingRecords,
    int NativeDroppedRecords,
    string? Phase = null,
    string? Error = null);

/// <summary>
/// Host-owned persistence boundary. The state machine owns when a run is cancelled
/// or a server is cleared, while the sink owns how those current 0.3.17 storage
/// transactions are implemented.
/// </summary>
public interface IMapScanLocalSink
{
    ValueTask CancelScanAsync(string scanRunId, CancellationToken cancellationToken = default);

    ValueTask ClearServerAsync(int serverId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Current 0.3.17 host-visible Map scan control/state contract. Deliberately does
/// not implement traversal geometry, retries, provider transport, or record capture.
/// </summary>
public sealed class MapScanStateMachine
{
    private const string GameUnavailableCode = "GAME_CONNECTION_UNAVAILABLE";
    private const string GameUnavailableMessage = "game connection unavailable";
    private const string ScanRunningCode = "SCAN_RUNNING";
    private const string ScanRunningMessage = "map scan already running";
    private const string InvalidModeCode = "INVALID_SCAN_MODE";
    private const string InvalidModeMessage = "map scan mode must be normal or fast";
    private const string InvalidTypesCode = "INVALID_SCAN_TYPES";
    private const string InvalidTypesMessage = "no valid map scan types selected";
    private const string ServerUnavailableCode = "SERVER_UNAVAILABLE";
    private const string ServerUnavailableMessage = "current server id unavailable";
    private const string MapSizeUnavailableCode = "MAP_SIZE_UNAVAILABLE";
    private const string MapSizeUnavailableMessage = "world map dimensions are unavailable";
    private const string WorldMapFailedCode = "WORLD_MAP_FAILED";
    private const string WorldMapFailedMessage = "failed to enter world map";
    private const string StartFailedCode = "MAP_SCAN_START_FAILED";
    private const string StartFailedMessage = "map scanner is not ready; please try again";
    private const string RejectedCode = "MAP_SCAN_REJECTED";
    private const string RejectedMessage = "map scan was not accepted";
    private const string BlockCountMismatchMessage = "map scan block count mismatch";

    private readonly IMapProvider provider;
    private readonly IMapScanLocalSink localSink;
    private readonly Func<long> nowUnixSeconds;
    private readonly object stateGate = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private MapScanState state = CreateDefaultIdleState();

    public MapScanStateMachine(
        IMapProvider provider,
        IMapScanLocalSink localSink,
        Func<long>? nowUnixSeconds = null)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.localSink = localSink ?? throw new ArgumentNullException(nameof(localSink));
        this.nowUnixSeconds = nowUnixSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public event EventHandler<MapScanStateChangedEventArgs>? StateChanged;

    public MapScanState State
    {
        get
        {
            lock (stateGate)
                return state;
        }
    }

    public static MapScanState CreateDefaultIdleState() =>
        new(
            ServerId: 0,
            ServerIdSource: "none",
            ScanRunId: string.Empty,
            IsReading: false,
            Phase: "idle",
            SelectedTypes: MapKinds.All.ToArray(),
            TotalBlocks: 0,
            CompletedBlocks: 0,
            ReadBlocks: 0,
            FailedBlocks: 0,
            UnreadBlocks: 0,
            InflightBlocks: 0,
            ScanMode: "normal",
            Concurrency: 8,
            ScanRate: 0,
            ProgressPercent: 0,
            NativeCaptureReady: false,
            NativePendingRecords: 0,
            NativeDroppedRecords: 0,
            ResumeAvailable: false,
            Error: null,
            StartedAt: 0);

    /// <summary>
    /// Refresh the idle server identity from the real provider without starting a
    /// scan. This lets a fresh host reopen durable Map data for the currently owned
    /// live server before another scan has established in-memory state.
    /// </summary>
    public async ValueTask<MapScanState> RefreshContextAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MapScanState before = State;
            if (before.IsReading)
                return before;

            MapProviderContext context = await GetContextAsync(cancellationToken).ConfigureAwait(false);
            RequireGameAvailable(context);
            RequireOwnedServer(context);

            MapScanState next = before.ServerId == context.ServerId
                ? before with
                {
                    ServerId = context.ServerId,
                    ServerIdSource = context.ServerIdSource,
                    IsInWorld = context.IsInWorld,
                }
                : CreateDefaultIdleState() with
                {
                    ServerId = context.ServerId,
                    ServerIdSource = context.ServerIdSource,
                    IsInWorld = context.IsInWorld,
                };
            return SetState(next);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async ValueTask<MapScanState> StartAsync(
        MapScanStartRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new MapScanStartRequest();
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MapProviderContext context = await GetContextAsync(cancellationToken).ConfigureAwait(false);
            RequireGameAvailable(context);

            MapScanState before = State;
            if (before.IsReading)
                throw new BridgeCommandException(ScanRunningCode, ScanRunningMessage);

            if (!context.IsInWorld)
                context = await EnterWorldMapAsync(cancellationToken).ConfigureAwait(false);

            RequireGameAvailable(context);
            RequireOwnedServer(context);

            string[] selectedTypes = NormalizeTypes(request.SelectedTypes);
            string scanMode = NormalizeMode(request.ScanMode);
            int concurrency = ConcurrencyForMode(scanMode);

            if (context.TileWidth <= 0 || context.TileHeight <= 0)
                throw new BridgeCommandException(MapSizeUnavailableCode, MapSizeUnavailableMessage);

            string scanRunId = Guid.NewGuid().ToString("N");
            bool resume = request.Resume && before.ResumeAvailable;
            var providerRequest = new MapProviderStartRequest(
                scanRunId,
                context.ServerId,
                context.WorldId,
                context.TileWidth,
                context.TileHeight,
                selectedTypes,
                scanMode,
                concurrency,
                resume);

            MapProviderStartResult result =
                await StartProviderAsync(providerRequest, cancellationToken).ConfigureAwait(false);

            if (!result.Accepted)
                throw new BridgeCommandException(
                    StartFailedCode,
                    string.IsNullOrWhiteSpace(result.Error) ? RejectedMessage : result.Error);

            if (result.TotalBlocks <= 0 ||
                (context.ExpectedTotalBlocks is int expected && expected != result.TotalBlocks))
            {
                throw new BridgeCommandException(RejectedCode, BlockCountMismatchMessage);
            }

            MapScanState started = new(
                ServerId: context.ServerId,
                ServerIdSource: context.ServerIdSource,
                ScanRunId: scanRunId,
                IsReading: true,
                Phase: "scanning",
                SelectedTypes: selectedTypes,
                TotalBlocks: result.TotalBlocks,
                CompletedBlocks: 0,
                ReadBlocks: 0,
                FailedBlocks: 0,
                UnreadBlocks: result.TotalBlocks,
                InflightBlocks: 0,
                ScanMode: scanMode,
                Concurrency: concurrency,
                ScanRate: 0,
                ProgressPercent: 0,
                NativeCaptureReady: result.NativeCaptureReady,
                NativePendingRecords: Math.Max(result.NativePendingRecords, 0),
                NativeDroppedRecords: Math.Max(result.NativeDroppedRecords, 0),
                ResumeAvailable: false,
                Error: null,
                StartedAt: nowUnixSeconds());

            return SetState(started);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public MapScanState ReportProgress(MapScanProgressUpdate update)
    {
        MapScanState changed = PreviewProgress(update);
        return ApplyProgress(changed);
    }

    public MapScanState PreviewProgress(MapScanProgressUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (stateGate)
        {
            if (!state.IsReading)
                return state;

            int completed = Math.Clamp(update.CompletedBlocks, 0, state.TotalBlocks);
            int failed = Math.Clamp(update.FailedBlocks, 0, state.TotalBlocks);
            if ((long)completed + failed > state.TotalBlocks)
                throw new BridgeCommandException(
                    "INVALID_SCAN_PROGRESS",
                    "completed and failed map blocks exceed the scan total");
            int remaining = state.TotalBlocks - completed - failed;
            int inflight = Math.Clamp(update.InflightBlocks, 0, Math.Min(state.Concurrency, remaining));
            int unread = Math.Max(remaining - inflight, 0);
            string phase = string.IsNullOrWhiteSpace(update.Phase) ? state.Phase : update.Phase;
            double progress = ProgressPercent(
                state.TotalBlocks,
                completed,
                failed,
                phase);
            long elapsed = Math.Max(nowUnixSeconds() - state.StartedAt, 1L);
            double scanRate = Math.Round(completed / (double)elapsed * 100.0, MidpointRounding.AwayFromZero) / 100.0;

            return state with
            {
                CompletedBlocks = completed,
                ReadBlocks = completed,
                FailedBlocks = failed,
                UnreadBlocks = unread,
                InflightBlocks = inflight,
                ScanRate = scanRate,
                ProgressPercent = progress,
                NativeCaptureReady = update.NativeCaptureReady,
                NativePendingRecords = update.NativePendingRecords,
                NativeDroppedRecords = update.NativeDroppedRecords,
                Phase = phase,
                ResumeAvailable = false,
                Error = update.Error,
            };
        }
    }

    public MapScanState ApplyProgress(MapScanState changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (stateGate)
        {
            if (!state.IsReading)
                return state;
            if (!string.Equals(state.ScanRunId, changed.ScanRunId, StringComparison.Ordinal) ||
                state.ServerId != changed.ServerId)
            {
                throw new BridgeCommandException("INVALID_SCAN_PROGRESS", "map scan progress does not match the active scan");
            }
            state = changed;
        }
        RaiseStateChanged(changed);
        return changed;
    }

    public MapScanState Complete(string? error = null)
    {
        MapScanState completed;
        lock (stateGate)
        {
            if (!state.IsReading)
                return state;
            state = state with
            {
                IsReading = false,
                Phase = "completed",
                CompletedBlocks = state.TotalBlocks,
                ReadBlocks = state.TotalBlocks,
                FailedBlocks = 0,
                UnreadBlocks = 0,
                InflightBlocks = 0,
                ProgressPercent = state.TotalBlocks > 0 ? 100.0 : 0.0,
                ResumeAvailable = false,
                Error = error,
            };
            completed = state;
        }
        RaiseStateChanged(completed);
        return completed;
    }

    public MapScanState Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        MapScanState failed;
        lock (stateGate)
        {
            if (!state.IsReading)
                return state;
            state = state with
            {
                IsReading = false,
                Phase = "failed",
                InflightBlocks = 0,
                ResumeAvailable = false,
                Error = error,
            };
            failed = state;
        }
        RaiseStateChanged(failed);
        return failed;
    }

    public async ValueTask<MapScanState> FailAndStopAsync(
        string error, string expectedScanRunId, int expectedServerId,
        MapStore store, Func<long> nowMilliseconds, CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MapScanState before = State;
            // Check the captured owner *under* the same operation gate as Start/Stop.
            // An old status request must never fail, stop or publish into its successor.
            if (!before.IsReading || string.IsNullOrEmpty(expectedScanRunId) ||
                !string.Equals(before.ScanRunId, expectedScanRunId, StringComparison.Ordinal) ||
                before.ServerId != expectedServerId)
                return before;
            store.FailScan(before.ScanRunId, error, nowMilliseconds(), preserveRecords: false);
            try { await provider.StopMapScanAsync(CancellationToken.None).ConfigureAwait(false); }
            catch { /* best effort, like the original stopMapScan after the local boundary */ }
            MapScanState failed;
            lock (stateGate)
            {
                state = state with
                {
                    IsReading = false,
                    Phase = "idle",
                    InflightBlocks = 0,
                    ResumeAvailable = false,
                    Error = error,
                };
                failed = state;
            }
            RaiseStateChanged(failed);
            return failed;
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async ValueTask<MapScanState> StopAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MapScanState before = State;
            if (before.IsReading && !string.IsNullOrEmpty(before.ScanRunId))
            {
                // Local cancellation is authoritative. If it fails, preserve the active
                // state and fail closed rather than claiming the scan was stopped.
                await localSink.CancelScanAsync(before.ScanRunId, cancellationToken).ConfigureAwait(false);
            }

            // 0.3.17 reaches stopMapScan only when the provider is usable. Provider stop
            // is therefore best-effort after local cancellation; failure must not undo
            // the local cancelled/idle boundary or fabricate provider success.
            if (before.IsReading)
            {
                try
                {
                    // Local cancellation has committed. The caller may now retire,
                    // but the accepted provider run still owns capture and its lease
                    // until Stop signals it and observes terminalization. Preserve
                    // cancelable admission/local persistence above; this independent
                    // terminal wait is a native lifetime adaptation, not provider
                    // success or a change to the recovered best-effort error policy.
                    await provider.StopMapScanAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Intentionally best-effort. No records or provider state are synthesized.
                }
            }

            MapScanState stopped;
            lock (stateGate)
            {
                state = state with
                {
                    IsReading = false,
                    Phase = "idle",
                    InflightBlocks = 0,
                    ResumeAvailable = false,
                    Error = null,
                };
                stopped = state;
            }

            RaiseStateChanged(stopped);
            return stopped;
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async ValueTask<MapScanState> ClearAsync(
        int serverId,
        CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MapScanState before = State;
            if (before.IsReading)
                throw new BridgeCommandException(ScanRunningCode, "stop the map scan first");

            if (serverId <= 0 || before.ServerId != serverId ||
                !string.Equals(before.ServerIdSource, "live", StringComparison.Ordinal))
            {
                throw new BridgeCommandException(ServerUnavailableCode, ServerUnavailableMessage);
            }

            await localSink.ClearServerAsync(serverId, cancellationToken).ConfigureAwait(false);
            return SetState(CreateDefaultIdleState());
        }
        finally
        {
            operationGate.Release();
        }
    }

    public static int UnreadBlocks(int totalBlocks, int completedBlocks, int failedBlocks, int inflightBlocks = 0)
    {
        long unread = (long)totalBlocks - completedBlocks - failedBlocks - inflightBlocks;
        return unread <= 0 ? 0 : unread > int.MaxValue ? int.MaxValue : (int)unread;
    }

    public static double ProgressPercent(
        int totalBlocks,
        int completedBlocks,
        int failedBlocks,
        string? phase)
    {
        if (totalBlocks <= 0)
            return 0.0;

        if (string.Equals(phase, "completed", StringComparison.Ordinal))
            return 100.0;

        long covered = (long)completedBlocks + failedBlocks;
        covered = Math.Clamp(covered, 0L, totalBlocks);
        double tenths = Math.Round(
            covered / (double)totalBlocks * 1000.0,
            MidpointRounding.AwayFromZero) / 10.0;
        return Math.Min(tenths, 98.0);
    }

    private async ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetContextAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The recovered public error contract is code/message. Do not expose
            // an Exception instance as JSON `details`: System.Text.Json walks
            // Exception.TargetSite (MethodBase), which is unsupported and can
            // crash the WinForms host while it is trying to report the provider
            // failure. Provider diagnostics stay beneath the host boundary.
            _ = error;
            throw new BridgeCommandException(GameUnavailableCode, GameUnavailableMessage);
        }
    }

    private async ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken)
    {
        try
        {
            MapProviderContext context =
                await provider.EnterWorldMapAsync(cancellationToken).ConfigureAwait(false);
            if (!context.IsInWorld)
                throw new BridgeCommandException(WorldMapFailedCode, WorldMapFailedMessage);
            return context;
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _ = error;
            throw new BridgeCommandException(WorldMapFailedCode, WorldMapFailedMessage);
        }
    }

    private async ValueTask<MapProviderStartResult> StartProviderAsync(
        MapProviderStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.StartMapScanAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _ = error;
            throw new BridgeCommandException(StartFailedCode, StartFailedMessage);
        }
    }

    private static void RequireGameAvailable(MapProviderContext context)
    {
        if (!context.IsAvailable)
            throw new BridgeCommandException(GameUnavailableCode, GameUnavailableMessage);
    }

    private static void RequireOwnedServer(MapProviderContext context)
    {
        if (context.ServerId <= 0 ||
            !string.Equals(context.ServerIdSource, "live", StringComparison.Ordinal))
        {
            throw new BridgeCommandException(ServerUnavailableCode, ServerUnavailableMessage);
        }
    }

    private static string[] NormalizeTypes(IReadOnlyList<string>? requested)
    {
        if (requested is null)
            return MapKinds.All.ToArray();

        var selected = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? type in requested)
        {
            if (type is null || !MapKinds.IsValid(type) || !seen.Add(type))
                continue;
            selected.Add(type);
        }

        if (selected.Count == 0)
            throw new BridgeCommandException(InvalidTypesCode, InvalidTypesMessage);

        return selected.ToArray();
    }

    private static string NormalizeMode(string? requested) =>
        requested switch
        {
            null => "normal",
            "normal" => "normal",
            "fast" => "fast",
            _ => throw new BridgeCommandException(InvalidModeCode, InvalidModeMessage),
        };

    private static int ConcurrencyForMode(string scanMode) =>
        scanMode == "fast" ? 20 : 8;

    private MapScanState SetState(MapScanState next)
    {
        lock (stateGate)
            state = next;
        RaiseStateChanged(next);
        return next;
    }

    private void RaiseStateChanged(MapScanState changed) =>
        StateChanged?.Invoke(this, new MapScanStateChangedEventArgs(changed));
}

public sealed class MapScanStateChangedEventArgs : EventArgs
{
    public MapScanStateChangedEventArgs(MapScanState state) => State = state;

    public MapScanState State { get; }
}
