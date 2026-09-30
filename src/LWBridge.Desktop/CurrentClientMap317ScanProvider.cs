using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop;

/// <summary>
/// Current-client acquisition adapter for the exact 0.3.17 host plane. The
/// recovered host semantics remain in Map317; traversal/capture are explicitly
/// current-client provider implementation details.
/// </summary>
internal sealed class CurrentClientMap317ScanProvider : Map317.IMapProvider, IDisposable
{
    private readonly object gate = new();
    private readonly CurrentClientMapBlockSource source;
    private Map317.MapProviderStartRequest? pending;
    private CurrentClientMapContext? pendingContext;
    private CancellationTokenSource? activeCancellation;
    private Task? activeTask;

    internal event Action? RunTerminated;

    internal CurrentClientMap317ScanProvider(CurrentClientMapBlockSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async ValueTask<Map317.MapProviderContext> GetContextAsync(
        CancellationToken cancellationToken = default)
    {
        CurrentClientMapStatusContext context;
        try
        {
            context = await source.GetMapStatusContextAsync(cancellationToken).ConfigureAwait(false);
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
            CurrentClientMapContext context = await source.GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
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
            liveContext = await source.GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
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

    internal void ActivateAcceptedRun(Map317.MapControlPlane control)
    {
        Map317.MapProviderStartRequest request;
        CurrentClientMapContext liveContext;
        CancellationTokenSource cancellation;
        lock (gate)
        {
            request = pending ?? throw new InvalidOperationException("No accepted Map317 scan is pending activation.");
            liveContext = pendingContext ?? throw new InvalidOperationException("No accepted current-client scan context is pending activation.");
            if (!string.Equals(control.ScanState.ScanRunId, request.ScanRunId, StringComparison.Ordinal))
                throw new InvalidOperationException("Map317 scan activation does not match the durable scan run.");
            pending = null;
            pendingContext = null;
            cancellation = new CancellationTokenSource();
            activeCancellation = cancellation;
        }

        var sink = new Map317ScanRunSink(control);
        var engine = new MapScanEngine(
            source,
            sink,
            progress =>
            {
                try
                {
                    control.ReportProgress(new Map317.MapScanProgressUpdate(
                        progress.CompletedBlocks,
                        progress.CompletedBlocks,
                        progress.FailedBlocks,
                        progress.InflightBlocks,
                        progress.ScanRate,
                        NativeCaptureReady: true,
                        NativePendingRecords: 0,
                        NativeDroppedRecords: 0,
                        Phase: progress.Phase));
                }
                catch
                {
                    cancellation.Cancel();
                    throw;
                }
            });
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
        Task task = RunEngineAsync(engine, execution, cancellation);
        lock (gate) activeTask = task;
    }

    public async ValueTask StopMapScanAsync(CancellationToken cancellationToken = default)
    {
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
            try { await task.WaitAsync(cancellationToken).ConfigureAwait(false); }
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
        CancellationTokenSource cancellation)
    {
        try
        {
            await engine.ExecuteAsync(request, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch
        {
            // MapScanEngine writes the exact-plane terminal failure through the
            // sink before surfacing the provider exception.
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
            RunTerminated?.Invoke();
        }
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

    internal Map317ScanRunSink(Map317.MapControlPlane control) => this.control = control;

    public void Begin(MapScanExecutionRequest request, int totalBlocks, long updatedAt)
    {
        if (!string.Equals(control.ScanState.ScanRunId, request.RunId, StringComparison.Ordinal) ||
            control.ScanState.TotalBlocks != totalBlocks)
            throw new InvalidDataException("Map317 scan engine run does not match the accepted durable run.");
    }

    public void CheckpointSuccess(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        MapScanBlockCapture capture,
        int attempts,
        long updatedAt)
    {
        foreach (MapStoredRecord record in capture.Records)
            control.StageRecord(ToMap317Record(record));
    }

    public void CheckpointFailure(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int attempts,
        string error,
        long updatedAt)
    {
    }

    public void Publish(MapScanExecutionRequest request, long updatedAt) => control.CompleteScan();

    public void Fail(MapScanExecutionRequest request, string error, long updatedAt) => control.FailScan(error);

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
