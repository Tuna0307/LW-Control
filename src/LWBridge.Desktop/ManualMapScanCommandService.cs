using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class ManualMapScanCommandService : INativeAsyncCommandService
{
    private readonly object gate = new();
    private readonly MapDataStore store;
    private readonly CurrentClientMapBlockSource currentClientSource;
    private readonly Func<CancellationToken, Task<CurrentClientMapContext>> getContext;
    private readonly IMapScanBlockSource blockSource;
    private CancellationTokenSource? activeCancellation;
    private Task? activeTask;
    private TaskCompletionSource<object?>? activeTerminal;
    private bool closed;
    private bool isReading;
    private string phase = "idle";
    private string scanRunId = string.Empty;
    private string scanMode = "normal";
    private IReadOnlyList<string> selectedTypes = ["city"];
    private int serverId;
    private long worldId;
    private int concurrency;
    private int totalBlocks;
    private int completedBlocks;
    private int failedBlocks;
    private int inflightBlocks;
    private int unreadBlocks;
    private double scanRate;
    private double? acquisitionProgressPercent;
    private bool coordinateJumping;
    private string? lastError;

    public ManualMapScanCommandService(
        OverviewLifecycleService lifecycle,
        MapDataStore store)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        currentClientSource = new CurrentClientMapBlockSource(lifecycle);
        getContext = currentClientSource.GetCurrentContextAsync;
        blockSource = currentClientSource;
    }

    internal ManualMapScanCommandService(
        MapDataStore store,
        Func<CancellationToken, Task<CurrentClientMapContext>> getContext,
        IMapScanBlockSource blockSource)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
        this.blockSource = blockSource ?? throw new ArgumentNullException(nameof(blockSource));
        currentClientSource = null!;
    }

    public event Action<object>? StatusChanged;

    public bool CanHandle(string command) =>
        command is "map_scan_start" or "map_scan_stop" or "map_scan_status" or "map_coordinate_jump";

    public async Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (command == "map_scan_status") return CreateStatus();
        if (command == "map_coordinate_jump")
            return await JumpToCoordinateAsync(payload, cancellationToken).ConfigureAwait(false);
        if (command == "map_scan_stop")
        {
            await StopAsync(cancellationToken).ConfigureAwait(false);
            return CreateStatus();
        }
        if (command != "map_scan_start")
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Manual Map Scan cannot handle '{command}'.");

        MapScanStartOptions options = MapScanContract.NormalizeStart(payload);
        if (options.SelectedTypes.Count != 1 ||
            options.SelectedTypes[0] is not ("city" or "resource" or "monster"))
        {
            throw new BridgeCommandException(
                "LIVE_BLOCK_TYPES_UNSUPPORTED",
                "The shared current-client scanner currently supports one Player City, Resource, or Monster kind.");
        }

        return await StartAsync(options, cancellationToken).ConfigureAwait(false);
    }


    private async Task<object> JumpToCoordinateAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        int requestedServerId = RequirePayloadInt(payload, "serverId", positive: true);
        int x = RequirePayloadInt(payload, "x", positive: false);
        int y = RequirePayloadInt(payload, "y", positive: false);
        lock (gate)
        {
            if (closed) throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing.");
            MapScanStartOwnership.RejectAlreadyRunning(isReading);
            if (coordinateJumping) throw new BridgeCommandException("MAP_NAVIGATION_RUNNING", "A map coordinate jump is already in progress.");
            if (currentClientSource is null) throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", "Map coordinate jump requires the live current-client source.");
            coordinateJumping = true;
        }
        try
        {
            CurrentClientCoordinateJumpResult result = await currentClientSource.JumpToCoordinateAsync(
                requestedServerId, x, y, cancellationToken).ConfigureAwait(false);
            return new { serverId = result.ServerId, x = result.X, y = result.Y };
        }
        finally
        {
            lock (gate) coordinateJumping = false;
        }
    }

    private static int RequirePayloadInt(JsonElement payload, string name, bool positive)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed) ||
            (positive ? parsed <= 0 : parsed < 0))
            throw new BridgeCommandException("INVALID_PAYLOAD", $"{name} is invalid.");
        return parsed;
    }

    private async Task<object> StartAsync(
        MapScanStartOptions options,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource scanCancellation;
        string runId;
        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException(
                    "MAP_SCAN_CLOSED",
                    "The Map Data window is closing and cannot start another scan.");
            MapScanStartOwnership.RejectAlreadyRunning(isReading);
            if (coordinateJumping)
                throw new BridgeCommandException("MAP_NAVIGATION_RUNNING", "A map coordinate jump is already in progress.");
            isReading = true;
            phase = "starting";
            scanMode = options.ScanMode;
            selectedTypes = options.SelectedTypes.ToArray();
            concurrency = options.Concurrency;
            lastError = null;
            serverId = 0;
            worldId = 0;
            totalBlocks = completedBlocks = failedBlocks = inflightBlocks = 0;
            unreadBlocks = 0;
            scanRate = 0;
            acquisitionProgressPercent = null;
            runId = Guid.NewGuid().ToString("N");
            scanRunId = runId;
            scanCancellation = new CancellationTokenSource();
            activeCancellation = scanCancellation;
            activeTerminal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        PublishStatusChanged();

        try
        {
            CurrentClientMapContext context;
            using (CancellationTokenSource linked =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken, scanCancellation.Token))
            {
                context = await getContext(linked.Token).ConfigureAwait(false);
            }
            MapScanStartOwnership.RequireLiveServer(context.ServerId, "live");
            MapScanBlockGrid grid = MapScanGeometry.FromTileDimensions(
                context.TileWidth,
                context.TileHeight);
            if (grid.TotalBlocks > int.MaxValue)
                throw new BridgeCommandException(
                    "MAP_SIZE_UNAVAILABLE",
                    "world map dimensions are unavailable");

            var request = new MapScanExecutionRequest(
                runId,
                context.ServerId,
                context.WorldId,
                context.TileWidth,
                context.TileHeight,
                selectedTypes,
                options.Concurrency,
                MaxAttemptsPerBlock: 2,
                PlayerTileX: context.PlayerTileX,
                PlayerTileY: context.PlayerTileY);

            lock (gate)
            {
                if (closed || !ReferenceEquals(activeCancellation, scanCancellation) ||
                    scanCancellation.IsCancellationRequested)
                    throw new OperationCanceledException(scanCancellation.Token);
                serverId = context.ServerId;
                worldId = context.WorldId;
                totalBlocks = checked((int)grid.TotalBlocks);
                unreadBlocks = totalBlocks;
                phase = "scanning";
            }
            PublishStatusChanged();

            var engine = new MapScanEngine(
                blockSource,
                new MapDataStoreScanSink(store),
                UpdateProgress);
            Task task = RunEngineAsync(engine, request, scanCancellation);
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                    activeTask = task;
            }
            return CreateStatus();
        }
        catch (Exception error)
        {
            CleanupFailedStart(scanCancellation, error);
            throw;
        }
    }

    private async Task RunEngineAsync(
        MapScanEngine engine,
        MapScanExecutionRequest request,
        CancellationTokenSource scanCancellation)
    {
        try
        {
            await engine.ExecuteAsync(request, scanCancellation.Token).ConfigureAwait(false);
            bool changed = false;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    isReading = false;
                    phase = "completed";
                    inflightBlocks = 0;
                    unreadBlocks = 0;
                    lastError = null;
                    acquisitionProgressPercent = null;
                    changed = true;
                }
            }
            if (changed) PublishStatusChanged();
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            bool changed = false;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    isReading = false;
                    phase = "idle";
                    inflightBlocks = 0;
                    lastError = null;
                    acquisitionProgressPercent = null;
                    changed = true;
                }
            }
            if (changed) PublishStatusChanged();
        }
        catch (Exception error)
        {
            bool changed = false;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    isReading = false;
                    phase = "error";
                    inflightBlocks = 0;
                    lastError = error.Message;
                    acquisitionProgressPercent = null;
                    changed = true;
                }
            }
            if (changed) PublishStatusChanged();
        }
        finally
        {
            TaskCompletionSource<object?>? terminal = null;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    activeCancellation = null;
                    activeTask = null;
                    terminal = activeTerminal;
                    activeTerminal = null;
                }
            }
            terminal?.TrySetResult(null);
            scanCancellation.Dispose();
        }
    }

    private void UpdateProgress(MapScanEngineProgress progress)
    {
        lock (gate)
        {
            if (!isReading) return;
            phase = progress.Phase;
            totalBlocks = progress.TotalBlocks;
            completedBlocks = progress.CompletedBlocks;
            failedBlocks = progress.FailedBlocks;
            inflightBlocks = progress.InflightBlocks;
            unreadBlocks = progress.UnreadBlocks;
            scanRate = progress.ScanRate;
            if (progress.AcquisitionProgressPercent.HasValue)
                acquisitionProgressPercent = Math.Clamp(progress.AcquisitionProgressPercent.Value, 0d, 100d);
            else if (!string.Equals(progress.Phase, "scanning", StringComparison.Ordinal))
                acquisitionProgressPercent = null;
        }
        PublishStatusChanged();
    }

    private void CleanupFailedStart(
        CancellationTokenSource scanCancellation,
        Exception error)
    {
        TaskCompletionSource<object?>? terminal = null;
        bool changed = false;
        lock (gate)
        {
            if (!ReferenceEquals(activeCancellation, scanCancellation)) return;
            isReading = false;
            bool cancelled = scanCancellation.IsCancellationRequested || error is OperationCanceledException;
            phase = cancelled ? "idle" : "error";
            inflightBlocks = 0;
            activeCancellation = null;
            activeTask = null;
            terminal = activeTerminal;
            activeTerminal = null;
            lastError = cancelled ? null : error.Message;
            acquisitionProgressPercent = null;
            changed = true;
        }
        terminal?.TrySetResult(null);
        if (changed) PublishStatusChanged();
        scanCancellation.Dispose();
    }

    private async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cancellation;
        Task terminalTask;
        bool changed = false;
        lock (gate)
        {
            cancellation = activeCancellation;
            terminalTask = activeTerminal?.Task ?? Task.CompletedTask;
            if (isReading && phase is not "completed" and not "cancelling")
            {
                phase = "cancelling";
                changed = true;
            }
        }
        if (changed) PublishStatusChanged();
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
        if (!terminalTask.IsCompleted)
            await terminalTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void PublishStatusChanged()
    {
        Action<object>? handler = StatusChanged;
        if (handler is null) return;
        handler(CreateStatus());
    }

    public object CreateStatus()
    {
        lock (gate)
        {
            MapScanDerivedProgress derived = MapScanProgress.Derive(
                totalBlocks,
                completedBlocks,
                failedBlocks,
                phase);
            double visibleProgress = derived.ProgressPercent;
            if (isReading && string.Equals(phase, "scanning", StringComparison.Ordinal) && acquisitionProgressPercent.HasValue)
                visibleProgress = Math.Max(visibleProgress, Math.Min(98d, acquisitionProgressPercent.Value));
            return new
            {
                serverId,
                serverIdSource = serverId > 0 ? "live" : "none",
                scanRunId,
                isReading,
                phase,
                selectedTypes,
                totalBlocks,
                readBlocks = completedBlocks,
                unreadBlocks,
                failedBlocks,
                inflightBlocks,
                scanMode,
                concurrency,
                retryCount = (int?)null,
                scanRate,
                progressPercent = visibleProgress,
                acquisitionProgressPercent,
                nativeCaptureReady = (bool?)null,
                nativePendingRecords = (int?)null,
                nativeDroppedRecords = (int?)null,
                homeServerId = (int?)null,
                seasonServerIds = (int[]?)null,
                truckMatchServerIds = (int[]?)null,
                lastError,
                worldId,
            };
        }
    }

    public void Close()
    {
        Task? terminalTask;
        CancellationTokenSource? cancellation;
        lock (gate)
        {
            closed = true;
            cancellation = activeCancellation;
            terminalTask = activeTerminal?.Task ?? activeTask;
            if (isReading && phase is not "completed") phase = "cancelling";
        }
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
        if (terminalTask is null) return;
        try { terminalTask.GetAwaiter().GetResult(); }
        catch { }
    }
}
