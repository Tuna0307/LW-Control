namespace LWBridge.Desktop;

/// <summary>
/// Legacy batch capture with source-owned final cleanup. Unlike the shared
/// stream adapter this keeps the existing per-block Treasure/Other partition
/// behavior, but it must NOT conceal the source's opt-in cleanup interface.
/// </summary>
internal sealed class MapOtherEventCleanupScanSource : IMapScanProgressBatchSource, IMapScanAcquisitionCleanup
{
    private readonly MapOtherEventScanSource partition;
    private readonly IMapScanAcquisitionCleanup owner;

    internal MapOtherEventCleanupScanSource(IMapScanBlockSource source,
        IMapScanAcquisitionCleanup owner)
    {
        partition = new MapOtherEventScanSource(source);
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request,
        MapScanTargetBlock block, CancellationToken cancellationToken) =>
        partition.CaptureAsync(request, block, cancellationToken);

    public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices, CancellationToken cancellationToken) =>
        partition.CaptureBatchAsync(request, seedBlock, pendingBlockIndices, cancellationToken);

    public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices, Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken) =>
        partition.CaptureBatchAsync(request, seedBlock, pendingBlockIndices, progress, cancellationToken);

    public async Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
        MapScanExecutionRequest request, MapScanAcquisitionExitReason reason,
        CancellationToken token)
    {
        MapScanExecutionRequest native = MapOtherEventScanSource.NativeRequest(request);
        MapScanAcquisitionCleanupReceipt actual = await owner.CleanupAsync(native, reason, token)
            .ConfigureAwait(false);
        if (actual is null || actual.Request is null ||
            !MapOtherEventSharedScanSource.SameRequest(actual.Request, native))
            throw new InvalidDataException("Legacy native Treasure/Other cleanup did not echo the exact native scan owner.");
        // The engine separately validates affirmative source restoration and
        // matched fresh-home reply; never change either native outcome here.
        return new MapScanAcquisitionCleanupReceipt(request,
            actual.OriginalCameraRestored, actual.FreshHomeViewReplyMatched);
    }
}
