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
    bool FreshHomeViewReplyMatched);
