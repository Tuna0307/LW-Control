namespace LWBridge.Desktop;

internal sealed record MapScanExecutionRequest(
    string RunId,
    int ServerId,
    long WorldId,
    long TileWidth,
    long TileHeight,
    IReadOnlyList<string> SelectedTypes,
    int RequestedConcurrency,
    int MaxAttemptsPerBlock = 2);

internal sealed record MapScanBlockCapture(
    int ServerId,
    long WorldId,
    int BlockIndex,
    string PayloadJson,
    IReadOnlyList<MapStoredRecord> Records);

internal readonly record struct MapScanEngineProgress(
    string Phase,
    int TotalBlocks,
    int CompletedBlocks,
    int FailedBlocks,
    int InflightBlocks,
    int UnreadBlocks,
    double ScanRate);

internal interface IMapScanBlockSource
{
    Task<MapScanBlockCapture> CaptureAsync(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        CancellationToken cancellationToken);
}

internal interface IMapScanRunSink
{
    void Begin(MapScanExecutionRequest request, int totalBlocks, long updatedAt);
    void CheckpointSuccess(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        MapScanBlockCapture capture,
        int attempts,
        long updatedAt);
    void CheckpointFailure(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int attempts,
        string error,
        long updatedAt);
    void Publish(MapScanExecutionRequest request, long updatedAt);
    void Fail(MapScanExecutionRequest request, string error, long updatedAt);
    void Stop(MapScanExecutionRequest request, long updatedAt);
}
