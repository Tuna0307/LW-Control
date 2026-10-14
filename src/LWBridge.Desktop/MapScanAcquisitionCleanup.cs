namespace LWBridge.Desktop;

/// <summary>
/// Optional opt-in capability for a source that has retained a native camera
/// across consecutive acquisitions. Sources without this interface retain the
/// existing scanner behavior; a source choosing this contract must fail closed.
/// </summary>
internal interface IMapScanAcquisitionCleanup
{
    Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(
        MapScanExecutionRequest request,
        MapScanAcquisitionExitReason reason,
        CancellationToken cleanupToken);
}

internal enum MapScanAcquisitionExitReason
{
    Completed,
    Stopped,
    Failed,
}

/// <summary>
/// The source must return this only after the exact owned native camera and
/// projection are restored AND a fresh native home-view reply is matched.
/// Echo all request identity fields. The managed request has no native PID or
/// group-generation fields: the eventual native provider must validate those
/// independently before any cleanup side effects. This contract alone is not
/// evidence of actual native restoration.
/// </summary>
internal sealed record MapScanAcquisitionCleanupReceipt(
    MapScanExecutionRequest Request,
    bool OriginalCameraRestored,
    bool FreshHomeViewReplyMatched,
    MapScanSharedAcquisitionOwner? SharedOwner = null);

/// <summary>
/// Claimed immutable owner of an experimental shared world-map acquisition.
/// Profile/PID/generation are not present in MapScanExecutionRequest: a future
/// native source must independently verify these against the live owner.
/// </summary>
internal sealed record MapScanSharedAcquisitionOwner(
    string RunId,
    int ServerId,
    long WorldId,
    string LaunchSessionId,
    IReadOnlyList<string> SelectedTypes,
    string ProfileId,
    int GameProcessId,
    string GroupGeneration);

/// <summary>
/// Each ordered batch represents final type-specific record enrichment for
/// these logical blocks; the same native discovery may be shared across kinds.
/// Empty record sets are valid, but missing logical blocks are not.
/// </summary>
internal sealed record MapScanSharedAcquisitionBatch(
    MapScanSharedAcquisitionOwner Owner,
    IReadOnlyList<MapScanBlockCapture> Captures);

/// <summary>
/// Explicit opt-in, not selected by existing production source adapters.
/// One call supplies a strictly ordered stream for one pinned run, and the
/// inherited cleanup must acknowledge that SAME group once at final home.
/// </summary>
internal interface IMapScanSharedAcquisitionSource : IMapScanBlockSource, IMapScanAcquisitionCleanup
{
    MapScanSharedAcquisitionOwner AcquisitionOwner { get; }

    IAsyncEnumerable<MapScanSharedAcquisitionBatch> CaptureSharedAsync(
        MapScanExecutionRequest request,
        IReadOnlyList<MapScanTargetBlock> orderedBlocks,
        CancellationToken cancellationToken);
}
