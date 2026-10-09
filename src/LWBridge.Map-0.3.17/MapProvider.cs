namespace LWBridge.Map317;

/// <summary>
/// High-level current-client Map provider boundary. 0.3.17 proves the host calls
/// enterWorldMap/startMapScan/stopMapScan, but not the transport/traversal grammar
/// beneath them, so this interface intentionally carries only host-visible inputs.
/// </summary>
public interface IMapProvider
{
    ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default);

    ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default);

    ValueTask<MapProviderStartResult> StartMapScanAsync(
        MapProviderStartRequest request,
        CancellationToken cancellationToken = default);

    ValueTask StopMapScanAsync(CancellationToken cancellationToken = default);
}

public sealed record MapProviderContext(
    bool IsAvailable,
    bool IsInWorld,
    int ServerId,
    string ServerIdSource,
    int WorldId,
    int TileWidth,
    int TileHeight,
    int? ExpectedTotalBlocks = null);

public sealed record MapProviderStartRequest(
    string ScanRunId,
    int ServerId,
    int WorldId,
    int TileWidth,
    int TileHeight,
    IReadOnlyList<string> SelectedTypes,
    string ScanMode,
    int Concurrency,
    bool Resume);

public sealed record MapProviderStartResult(
    bool Accepted,
    int TotalBlocks,
    bool NativeCaptureReady = false,
    int NativePendingRecords = 0,
    int NativeDroppedRecords = 0,
    string? Error = null);

/// <summary>
/// Delegate adapter for the eventual current-client provider integration. It keeps
/// transport details out of the recovered Map host implementation.
/// </summary>
public sealed class MapProviderAdapter : IMapProvider
{
    private readonly Func<CancellationToken, ValueTask<MapProviderContext>> getContext;
    private readonly Func<CancellationToken, ValueTask<MapProviderContext>> enterWorldMap;
    private readonly Func<MapProviderStartRequest, CancellationToken, ValueTask<MapProviderStartResult>> startMapScan;
    private readonly Func<CancellationToken, ValueTask> stopMapScan;

    public MapProviderAdapter(
        Func<CancellationToken, ValueTask<MapProviderContext>> getContext,
        Func<CancellationToken, ValueTask<MapProviderContext>> enterWorldMap,
        Func<MapProviderStartRequest, CancellationToken, ValueTask<MapProviderStartResult>> startMapScan,
        Func<CancellationToken, ValueTask> stopMapScan)
    {
        this.getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
        this.enterWorldMap = enterWorldMap ?? throw new ArgumentNullException(nameof(enterWorldMap));
        this.startMapScan = startMapScan ?? throw new ArgumentNullException(nameof(startMapScan));
        this.stopMapScan = stopMapScan ?? throw new ArgumentNullException(nameof(stopMapScan));
    }

    public ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default) =>
        getContext(cancellationToken);

    public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default) =>
        enterWorldMap(cancellationToken);

    public ValueTask<MapProviderStartResult> StartMapScanAsync(
        MapProviderStartRequest request,
        CancellationToken cancellationToken = default) =>
        startMapScan(request, cancellationToken);

    public ValueTask StopMapScanAsync(CancellationToken cancellationToken = default) =>
        stopMapScan(cancellationToken);
}

/// <summary>
/// Explicit unavailable implementation for compositions that do not yet have the
/// current game/provider connection. It never fabricates scan data.
/// </summary>
public sealed class UnavailableMapProvider : IMapProvider
{
    public static UnavailableMapProvider Instance { get; } = new();

    private UnavailableMapProvider()
    {
    }

    public ValueTask<MapProviderContext> GetContextAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromException<MapProviderContext>(Unavailable());

    public ValueTask<MapProviderContext> EnterWorldMapAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromException<MapProviderContext>(Unavailable());

    public ValueTask<MapProviderStartResult> StartMapScanAsync(
        MapProviderStartRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<MapProviderStartResult>(Unavailable());

    public ValueTask StopMapScanAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromException(Unavailable());

    private static BridgeCommandException Unavailable() =>
        new("GAME_CONNECTION_UNAVAILABLE", "game connection unavailable");
}
