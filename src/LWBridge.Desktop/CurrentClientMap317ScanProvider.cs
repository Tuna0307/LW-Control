using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

/// <summary>
/// Current-client acquisition adapter for the exact 0.3.17 host plane. The
/// recovered host semantics remain in Map317; traversal/capture are explicitly
/// current-client provider implementation details.
/// </summary>
internal sealed class CurrentClientMap317ScanProvider : IMap317RunScopedProvider
{
    private readonly object gate = new();
    private readonly IMapScanBlockSource source;
    private readonly Func<CancellationToken, Task<CurrentClientMapStatusContext>> getStatus;
    private readonly Func<CancellationToken, Task<CurrentClientMapContext>> getContext;
    private readonly Func<MapScanExecutionRequest, CurrentClientMapContext, IMapScanBlockSource>?
        createSourceForAcceptedRun;
    private Map317.MapProviderStartRequest? pending;
    private CurrentClientMapContext? pendingContext;
    private CancellationTokenSource? activeCancellation;
    private Task? activeTask;

    public event Action<string>? RunTerminated;

    internal CurrentClientMap317ScanProvider(CurrentClientMapBlockSource source)
        : this(source, source.GetMapStatusContextAsync, source.GetCurrentContextAsync)
    {
    }

    // Production adapter with the external game boundary replaced for controlled
    // scan/SQLite lifetime tests. The real engine and Map317 host still run.
    internal CurrentClientMap317ScanProvider(
        IMapScanBlockSource source,
        Func<CancellationToken, Task<CurrentClientMapStatusContext>> getStatus,
        Func<CancellationToken, Task<CurrentClientMapContext>> getContext,
        Func<MapScanExecutionRequest, CurrentClientMapContext, IMapScanBlockSource>?
            createSourceForAcceptedRun = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.getStatus = getStatus ?? throw new ArgumentNullException(nameof(getStatus));
        this.getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
        // Optional per-accepted-run factory. The engine source is normally
        // long lived; a source with an immutable native run/group owner must
        // instead be CREATED only after Map317 has issued the exact run ID.
        // This callback constructs a managed adapter ONLY and must never
        // initiate camera, network or game side effects. No production caller
        // currently supplies it: native A/B/C selector remains disabled.
        this.createSourceForAcceptedRun = createSourceForAcceptedRun;
    }

    public async ValueTask<Map317.MapProviderContext> GetContextAsync(
        CancellationToken cancellationToken = default)
    {
        CurrentClientMapStatusContext context;
        try
        {
            context = await getStatus(cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException error)
        {
            throw Translate(error);
        }
        if (context.ServerId <= 0)
            throw new Map317.BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
        int worldId = ToWorldId(context.WorldId);
        int? total = context.TileWidth > 0 && context.TileHeight > 0
            ? MapScanTraversal.Build(context.TileWidth, context.TileHeight).Count
            : null;
        return new Map317.MapProviderContext(
            true,
            context.IsInWorld,
            context.ServerId,
            "live",
            worldId,
            context.TileWidth,
            context.TileHeight,
            total);
    }

    public async ValueTask<Map317.MapProviderContext> EnterWorldMapAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            CurrentClientMapContext context = await getContext(cancellationToken).ConfigureAwait(false);
            int total = MapScanTraversal.Build(context.TileWidth, context.TileHeight).Count;
            return new Map317.MapProviderContext(
                true, true, context.ServerId, "live", ToWorldId(context.WorldId),
                context.TileWidth, context.TileHeight, total);
        }
        catch (BridgeCommandException error)
        {
            throw Translate(error);
        }
    }

    public async ValueTask<Map317.MapProviderStartResult> StartMapScanAsync(
        Map317.MapProviderStartRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int total = MapScanTraversal.Build(request.TileWidth, request.TileHeight).Count;
        CurrentClientMapContext liveContext;
        try
        {
            liveContext = await getContext(cancellationToken).ConfigureAwait(false);
        }
        catch (BridgeCommandException error)
        {
            throw Translate(error);
        }
        if (liveContext.ServerId != request.ServerId ||
            ToWorldId(liveContext.WorldId) != request.WorldId ||
            liveContext.TileWidth != request.TileWidth ||
            liveContext.TileHeight != request.TileHeight)
        {
            throw new Map317.BridgeCommandException(
                "MAP_SCAN_START_FAILED",
                "current map context changed before the scan started");
        }
        lock (gate)
        {
            if (pending is not null || activeTask is { IsCompleted: false })
                throw new Map317.BridgeCommandException("SCAN_RUNNING", "map scan is already running");
            pending = request;
            pendingContext = liveContext;
        }
        return new Map317.MapProviderStartResult(
            true,
            total,
            NativeCaptureReady: true);
    }

    public void ActivateAcceptedRun(Map317.MapControlPlane control, string scanRunId)
    {
        Map317.MapProviderStartRequest request;
        CurrentClientMapContext liveContext;
        CancellationTokenSource cancellation;
        lock (gate)
        {
            request = pending ?? throw new InvalidOperationException("No accepted Map317 scan is pending activation.");
            liveContext = pendingContext ?? throw new InvalidOperationException("No accepted current-client scan context is pending activation.");
            if (!string.Equals(scanRunId, request.ScanRunId, StringComparison.Ordinal) ||
                !string.Equals(control.ScanState.ScanRunId, request.ScanRunId, StringComparison.Ordinal))
                throw new InvalidOperationException("Map317 scan activation does not match the durable scan run.");
            pending = null;
            pendingContext = null;
            cancellation = new CancellationTokenSource();
            activeCancellation = cancellation;
        }

        var sink = new Map317ScanRunSink(control);
        var execution = new MapScanExecutionRequest(
            request.ScanRunId,
            request.ServerId,
            request.WorldId,
            request.TileWidth,
            request.TileHeight,
            request.SelectedTypes,
            request.Concurrency,
            // Current-v21 provider mechanics already use two bounded capture
            // attempts and the live player/home tile. Keep these beneath the
            // exact 0.3.17 host boundary rather than exposing retryCount as a
            // host persistence/state contract.
            MaxAttemptsPerBlock: 2,
            PlayerTileX: liveContext.PlayerTileX,
            PlayerTileY: liveContext.PlayerTileY,
            ScanMode: request.ScanMode,
            LaunchSessionId: liveContext.LaunchSessionId,
            LiveServerId: request.ServerId);
        // A legacy Other/Treasure adapter has no retained-camera cleanup
        // capability. Never place it around an opt-in native shared source:
        // doing so silently removes IMapScanSharedAcquisitionSource and its
        // required single final-home cleanup from the actual Auto/Map317 path.
        IMapScanBlockSource acceptedSource;
        try
        {
            acceptedSource = createSourceForAcceptedRun is null ? source :
                createSourceForAcceptedRun(execution, liveContext) ??
                throw new InvalidDataException("Accepted native scan source factory returned no source.");
        }
        catch (Exception error)
        {
            RetireUnactivatedAcceptedRun(control, request, cancellation, error);
            return;
        }
        IMapScanBlockSource acquisition;
        MapScanEngine engine;
        try
        {
        acquisition = acceptedSource is IMapScanSharedAcquisitionSource shared
            ? (execution.SelectedTypes.Contains("treasure", StringComparer.Ordinal) ||
                execution.SelectedTypes.Contains("other", StringComparer.Ordinal)
                    ? new MapOtherEventSharedScanSource(shared, execution)
                    : acceptedSource)
            : acceptedSource is IMapScanAcquisitionCleanup nativeCleanup
                ? new MapOtherEventCleanupScanSource(acceptedSource, nativeCleanup)
                : new MapOtherEventScanSource(acceptedSource);
        engine = new MapScanEngine(
            acquisition,
            sink,
            progress =>
            {
                try
                {
                    control.ReportProgressForRun(execution.RunId, new Map317.MapScanProgressUpdate(
                        progress.CompletedBlocks,
                        progress.CompletedBlocks,
                        progress.FailedBlocks,
                        progress.InflightBlocks,
                        progress.ScanRate,
                        NativeCaptureReady: true,
                        NativePendingRecords: 0,
                        NativeDroppedRecords: 0,
                        Phase: progress.Phase,
                        AcquisitionProgressPercent: progress.AcquisitionProgressPercent));
                }
                catch
                {
                    cancellation.Cancel();
                    throw;
                }
            },
            acceptedSource is CurrentClientMapBlockSource nativeSource ? metrics =>
            {
                // This is the mounted Map317 production adapter. Emit one
                // source-owned diagnostic after actual durable publication,
                // using exact run identity and no private game records.
                Directory.CreateDirectory(nativeSource.ProbeRuntimeRoot);
                File.WriteAllText(
                    Path.Combine(nativeSource.ProbeRuntimeRoot, "map-engine-stage-metrics.json"),
                    JsonSerializer.Serialize(metrics, JsonOptions.Default));
            } : null,
            beforePublicationOwnerCheck: acquisition is IMapScanAcquisitionCleanup
                ? token => VerifyCurrentNativeOwnerAsync(liveContext, execution, control, token)
                : null);
        }
        catch (Exception error)
        {
            // The native source factory can return a malformed shared owner
            // even though factory invocation itself succeeded. Adapter owner
            // projection and engine setup are still pre-capture; retire their
            // exact accepted Map317 run instead of leaving it stuck Reading.
            RetireUnactivatedAcceptedRun(control, request, cancellation, error);
            return;
        }
        // Even an immediate provider response must not finish before the active
        // task has been registered. Otherwise terminal cleanup can precede
        // registration and leave a completed task appearing active.
        var registered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task task = RunRegisteredAsync(registered.Task, engine, execution, cancellation,
            control, liveContext, acquisition is IMapScanAcquisitionCleanup);
        lock (gate) activeTask = task;
        registered.SetResult(true);
    }

    private async Task RunRegisteredAsync(
        Task registered, MapScanEngine engine, MapScanExecutionRequest request,
        CancellationTokenSource cancellation, Map317.MapControlPlane control,
        CurrentClientMapContext admittedContext, bool requiresFreshSourceOwner)
    {
        await registered.ConfigureAwait(false);
        await RunEngineAsync(engine, request, cancellation, control,
            admittedContext, requiresFreshSourceOwner).ConfigureAwait(false);
    }

    public async ValueTask StopMapScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task? task;
        CancellationTokenSource? cancellation;
        lock (gate)
        {
            pending = null;
            pendingContext = null;
            task = activeTask;
            cancellation = activeCancellation;
        }
        cancellation?.Cancel();
        if (task is not null)
        {
            // Once cancellation has been issued this method owns terminalization of
            // the accepted run. Do not let caller cancellation return early and
            // release the process lease while native capture is still unwinding.
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true) { }
        }
    }

    public void Dispose()
    {
        Task? task;
        CancellationTokenSource? cancellation;
        lock (gate)
        {
            task = activeTask;
            cancellation = activeCancellation;
            pending = null;
            pendingContext = null;
            cancellation?.Cancel();
        }

        if (task is not null)
        {
            try { task.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
        }

        lock (gate)
        {
            if (ReferenceEquals(activeCancellation, cancellation))
            {
                activeCancellation?.Dispose();
                activeCancellation = null;
                activeTask = null;
            }
        }
    }

    private async Task RunEngineAsync(
        MapScanEngine engine,
        MapScanExecutionRequest request,
        CancellationTokenSource cancellation,
        Map317.MapControlPlane control,
        CurrentClientMapContext admittedContext,
        bool requiresFreshSourceOwner)
    {
        try
        {
            if (requiresFreshSourceOwner)
            {
                // Between Map317 Start and accepted-run activation, Auto may
                // switch target servers or the Overview owner may retire.
                // Existing legacy per-probe sources enforce their own source
                // admission. An opt-in retained camera must be refused
                // *before any first native shared/acquisition command* when
                // the passive current-client context no longer matches.
                await VerifyCurrentNativeOwnerAsync(
                    admittedContext, request, control, cancellation.Token).ConfigureAwait(false);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            await engine.ExecuteAsync(request, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The normal Map317 Stop path already commits this exact run's
            // SQLite cancellation before it signals the provider. However a
            // provider-owned cancellation (for example shutdown/retirement)
            // can reach MapScanEngine's cancellation handler without ever
            // passing Map317 Stop. The engine sink.Stop intentionally avoids
            // recursing into provider.Stop, so the accepted run would remain
            // SQLite 'running' / Map317 Reading indefinitely. Attempt to
            // retire ONLY the exact admitted run; a previously canceled or
            // published run cannot be changed by FailScanForRun. Stop's
            // durable 'cancelled' status is never converted to 'failed'.
            try
            {
                control.FailScanForRun(request.RunId,
                    "native map acquisition was cancelled without a completed Map317 Stop");
            }
            catch
            {
                // The regular Stop won and already retired durable staging,
                // or the old owner no longer exists. Do not touch a successor.
            }
        }
        catch (Exception error)
        {
            // Engine request validation, shared-owner pinning, geometry checks
            // and sink.Begin currently happen BEFORE ExecuteAsync's protected
            // capture/failure region. Any of them can throw without writing a
            // terminal state, even though we have already entered the method.
            // A later engine failure normally retired itself. FailScanForRun is
            // intentionally idempotent on terminal/foreign runs and therefore
            // also safely closes these early exceptions without mutating a
            // manual or Auto successor's SQLite rows or progress.
            try
            {
                control.FailScanForRun(request.RunId, error.Message);
            }
            catch
            {
                // Preserve the original engine/source exception, not a second
                // persistence failure or unrelated successor status.
            }
        }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, cancellation))
                {
                    activeCancellation.Dispose();
                    activeCancellation = null;
                    activeTask = null;
                }
            }
            RunTerminated?.Invoke(request.RunId);
        }
    }

    private void RetireUnactivatedAcceptedRun(
        Map317.MapControlPlane control,
        Map317.MapProviderStartRequest request,
        CancellationTokenSource cancellation,
        Exception error)
    {
        // No native ingress or capture was started. A later scan must not
        // inherit this failed factory/adapter lease or its completion event.
        lock (gate)
        {
            if (ReferenceEquals(activeCancellation, cancellation))
            {
                activeCancellation = null;
                activeTask = null;
            }
        }
        cancellation.Dispose();
        try
        {
            control.FailScanForRun(request.ScanRunId, error.Message);
        }
        finally { RunTerminated?.Invoke(request.ScanRunId); }
    }

    private async Task VerifyCurrentNativeOwnerAsync(
        CurrentClientMapContext admittedContext,
        MapScanExecutionRequest request,
        Map317.MapControlPlane control,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CurrentClientMapContext fresh = await getContext(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        Map317.MapScanState current = control.ScanState;
        if (!current.IsReading ||
            !string.Equals(current.ScanRunId, request.RunId, StringComparison.Ordinal) ||
            fresh.ServerId != admittedContext.ServerId ||
            ToWorldId(fresh.WorldId) != ToWorldId(admittedContext.WorldId) ||
            fresh.TileWidth != admittedContext.TileWidth ||
            fresh.TileHeight != admittedContext.TileHeight ||
            fresh.PlayerTileX != admittedContext.PlayerTileX ||
            fresh.PlayerTileY != admittedContext.PlayerTileY ||
            string.IsNullOrWhiteSpace(fresh.LaunchSessionId) ||
            !string.Equals(fresh.LaunchSessionId,
                admittedContext.LaunchSessionId, StringComparison.Ordinal) ||
            fresh.ServerId != request.ServerId ||
            ToWorldId(fresh.WorldId) != request.WorldId ||
            !string.Equals(fresh.LaunchSessionId,
                request.LaunchSessionId, StringComparison.Ordinal))
            throw new BridgeCommandException("MAP_SCAN_OWNER_CHANGED",
                "current game server, world, home tile, accepted scan run or owned session changed during native acquisition");
    }

    private static int ToWorldId(long value)
    {
        if (value is < 0 or > int.MaxValue)
            throw new Map317.BridgeCommandException("GAME_PROVIDER_ERROR", "current world id is outside the recovered Map host range");
        return (int)value;
    }

    private static Map317.BridgeCommandException Translate(BridgeCommandException error) =>
        new(error.Code, error.Message, error.Details);
}

internal sealed class Map317ScanRunSink : IMapScanRunSink
{
    private readonly Map317.MapControlPlane control;
    private readonly object checkpointGate = new();
    private readonly HashSet<int> checkpointedBlocks = new();
    private string? begunRunId;
    private int expectedBlocks;

    internal Map317ScanRunSink(Map317.MapControlPlane control) => this.control = control;

    public void Begin(MapScanExecutionRequest request, int totalBlocks, long updatedAt)
    {
        Map317.MapScanState current = control.ScanState;
        if (!current.IsReading ||
            !string.Equals(current.ScanRunId, request.RunId, StringComparison.Ordinal) ||
            current.TotalBlocks != totalBlocks ||
            current.ServerId != request.ServerId ||
            !current.SelectedTypes.SequenceEqual(request.SelectedTypes, StringComparer.Ordinal))
            throw new InvalidDataException("Map317 scan engine run does not match the accepted durable run.");
        lock (checkpointGate)
        {
            if (begunRunId is not null || totalBlocks <= 0)
                throw new InvalidDataException("Map317 scan sink received a duplicate/invalid native Begin.");
            begunRunId = request.RunId;
            expectedBlocks = totalBlocks;
        }
    }

    public void CheckpointSuccess(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        MapScanBlockCapture capture,
        int attempts,
        long updatedAt)
    {
        lock (checkpointGate)
        {
            // MapScanEngine's progress is an observation, not independently
            // durable native block-completion evidence. Without this ledger,
            // a same-run premature Publish could claim TotalBlocks in SQLite
            // and replace last-good rows despite having staged one block.
            if (!string.Equals(begunRunId, request.RunId, StringComparison.Ordinal) ||
                block.BlockIndex is < 0 || block.BlockIndex >= expectedBlocks ||
                capture.BlockIndex != block.BlockIndex ||
                checkpointedBlocks.Contains(block.BlockIndex))
                throw new InvalidDataException("Map317 rejected a missing, foreign or duplicate native checkpoint.");
            // A checkpoint is certified ONLY after all of its records have
            // been staged durably under the exact Map317 owner. Holding this
            // separate ledger gate prevents early Publish from observing the
            // final logical block while its SQLite writes are in flight.
            foreach (MapStoredRecord record in capture.Records)
                control.StageRecordForRun(request.RunId, ToMap317Record(record));
            checkpointedBlocks.Add(block.BlockIndex);
        }
    }

    public void CheckpointFailure(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int attempts,
        string error,
        long updatedAt)
    {
    }

    public void Publish(MapScanExecutionRequest request, long updatedAt)
    {
        // Check active owner first so a retired sink continues to fail with
        // INVALID_SCAN, not a misleading incomplete-run error.
        Map317.MapScanState current = control.ScanState;
        if (!current.IsReading ||
            !string.Equals(current.ScanRunId, request.RunId, StringComparison.Ordinal))
            throw new Map317.BridgeCommandException("INVALID_SCAN",
                "obsolete map scan cannot publish over an independent run");
        lock (checkpointGate)
        {
            if (!string.Equals(begunRunId, request.RunId, StringComparison.Ordinal) ||
                checkpointedBlocks.Count != expectedBlocks || expectedBlocks <= 0)
                throw new Map317.BridgeCommandException("INCOMPLETE_SCAN",
                    "native scan lacks a successful checkpoint for every owned logical block");
        }
        control.CompleteScanForRun(request.RunId);
    }

    public void Fail(MapScanExecutionRequest request, string error, long updatedAt)
    {
        // An old engine may fail after Map317 has already retired that run
        // and accepted a successor. Never convert its failure into a failure
        // of the current independent run.
        control.FailScanForRun(request.RunId, error);
    }

    public void Stop(MapScanExecutionRequest request, long updatedAt)
    {
        // Stop ownership originates in MapControlPlane -> IMapProvider.StopMapScanAsync.
        // Re-entering that path from the engine cancellation sink would recurse.
    }

    private static Map317.MapRecord ToMap317Record(MapStoredRecord record) => new(
        record.Kind,
        record.ServerId,
        record.RecordKey,
        record.PointIndex,
        record.Uuid,
        record.Name,
        record.AllianceName,
        record.Level,
        record.Quality,
        record.Power,
        record.Distance,
        record.ShieldEndTime,
        record.UpdatedAt,
        record.DataJson);
}
