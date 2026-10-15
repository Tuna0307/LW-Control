namespace LWBridge.Desktop;

/// <summary>
/// Preserve the explicit native shared-run/cleanup protocol when Treasure and
/// Other share one native Treasure census. An ordinary MapOtherEventScanSource
/// deliberately does NOT implement shared cleanup; wrapping a retained-camera
/// source in that legacy adapter would silently erase its safety obligations.
/// The inner source owns the *native* kind selection (Other maps to Treasure),
/// while all durable records and engine receipts use the original selection.
/// </summary>
internal sealed class MapOtherEventSharedScanSource : IMapScanSharedAcquisitionSource
{
    private readonly IMapScanSharedAcquisitionSource native;
    private readonly MapScanExecutionRequest publicRequest;
    private readonly MapScanExecutionRequest nativeRequest;
    private readonly MapScanSharedAcquisitionOwner nativeOwner;
    private readonly MapScanSharedAcquisitionOwner publicOwner;

    internal MapOtherEventSharedScanSource(IMapScanSharedAcquisitionSource native,
        MapScanExecutionRequest request)
    {
        this.native = native ?? throw new ArgumentNullException(nameof(native));
        publicRequest = request with { SelectedTypes = request.SelectedTypes.ToArray() };
        nativeRequest = MapOtherEventScanSource.NativeRequest(publicRequest);
        MapScanSharedAcquisitionOwner owner = native.AcquisitionOwner
            ?? throw new InvalidDataException("Shared native Treasure source has no pinned owner.");
        nativeOwner = owner with { SelectedTypes = owner.SelectedTypes?.ToArray() ?? [] };
        RequireNativeOwner(nativeOwner);
        publicOwner = nativeOwner with { SelectedTypes = publicRequest.SelectedTypes.ToArray() };
    }

    public MapScanSharedAcquisitionOwner AcquisitionOwner => publicOwner;

    public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request,
        MapScanTargetBlock block, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("An owned shared Treasure/Other scan cannot use legacy per-block acquisition.");

    public async IAsyncEnumerable<MapScanSharedAcquisitionBatch> CaptureSharedAsync(
        MapScanExecutionRequest request, IReadOnlyList<MapScanTargetBlock> orderedBlocks,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RequirePublicRequest(request);
        RequireNativeOwner(native.AcquisitionOwner);
        await foreach (MapScanSharedAcquisitionBatch batch in native.CaptureSharedAsync(
            nativeRequest, orderedBlocks, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (batch is null) throw new InvalidDataException("Native shared Treasure batch is null.");
            RequireNativeOwner(batch.Owner);
            RequireNativeOwner(native.AcquisitionOwner);
            if (batch.Captures is null)
                throw new InvalidDataException("Native shared Treasure capture rows are missing.");
            MapScanBlockCapture[] projected = batch.Captures
                .Select(capture => MapOtherEventScanSource.Partition(capture, publicRequest))
                .ToArray();
            yield return new MapScanSharedAcquisitionBatch(publicOwner, projected,
                batch.BlockSourceProofs);
        }
    }

    public async Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
        MapScanExecutionRequest request, MapScanAcquisitionExitReason reason,
        CancellationToken cleanupToken)
    {
        RequirePublicRequest(request);
        RequireNativeOwner(native.AcquisitionOwner);
        MapScanAcquisitionCleanupReceipt receipt = await native.CleanupAsync(
            nativeRequest, reason, cleanupToken).ConfigureAwait(false);
        if (receipt is null || !MatchesNativeRequest(receipt.Request) ||
            !MatchesNativeOwner(receipt.SharedOwner))
            throw new InvalidDataException(
                "Native shared Treasure/Other cleanup did not echo the exact native run and group.");
        RequireNativeOwner(native.AcquisitionOwner);
        // Never synthesize an affirmative camera or fresh-home ACK. The native
        // source must supply both, and MapScanEngine checks them again before
        // publication of the public selection.
        return new MapScanAcquisitionCleanupReceipt(publicRequest,
            receipt.OriginalCameraRestored, receipt.FreshHomeViewReplyMatched, publicOwner);
    }

    private void RequirePublicRequest(MapScanExecutionRequest request)
    {
        if (!SameRequest(request, publicRequest))
            throw new InvalidDataException("Shared Treasure/Other public selection or scan owner changed.");
    }

    private bool MatchesNativeRequest(MapScanExecutionRequest? request) =>
        request is not null && SameRequest(request, nativeRequest);

    private void RequireNativeOwner(MapScanSharedAcquisitionOwner? owner)
    {
        if (!MatchesNativeOwner(owner))
            throw new InvalidDataException("Shared Treasure/Other native source changed server, profile, process, group or native selected types.");
    }

    private bool MatchesNativeOwner(MapScanSharedAcquisitionOwner? owner) =>
        owner is not null && owner.GameProcessId > 0 &&
        !string.IsNullOrWhiteSpace(owner.ProfileId) &&
        !string.IsNullOrWhiteSpace(owner.GroupGeneration) &&
        owner.RunId == nativeRequest.RunId && owner.ServerId == nativeRequest.ServerId &&
        owner.WorldId == nativeRequest.WorldId &&
        owner.LaunchSessionId == nativeRequest.LaunchSessionId &&
        owner.ProfileId == nativeOwner.ProfileId &&
        owner.GameProcessId == nativeOwner.GameProcessId &&
        owner.GroupGeneration == nativeOwner.GroupGeneration &&
        owner.SelectedTypes is not null &&
        owner.SelectedTypes.SequenceEqual(nativeRequest.SelectedTypes, StringComparer.Ordinal);

    internal static bool SameRequest(MapScanExecutionRequest a, MapScanExecutionRequest b) =>
        a.RunId == b.RunId && a.ServerId == b.ServerId &&
        a.WorldId == b.WorldId && a.TileWidth == b.TileWidth &&
        a.TileHeight == b.TileHeight && a.ScanMode == b.ScanMode &&
        a.LaunchSessionId == b.LaunchSessionId &&
        a.LiveServerId == b.LiveServerId &&
        a.RequestedConcurrency == b.RequestedConcurrency &&
        a.MaxAttemptsPerBlock == b.MaxAttemptsPerBlock &&
        a.PlayerTileX == b.PlayerTileX && a.PlayerTileY == b.PlayerTileY &&
        a.SelectedTypes.SequenceEqual(b.SelectedTypes, StringComparer.Ordinal);
}
