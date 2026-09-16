namespace LWBridge.Desktop;

internal sealed class MapDataStoreScanSink : IMapScanBatchRunSink
{
    private readonly MapDataStore store;

    public MapDataStoreScanSink(MapDataStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void Begin(MapScanExecutionRequest request, int totalBlocks, long updatedAt) =>
        store.BeginEngineScan(request, totalBlocks, updatedAt);

    public void CheckpointSuccess(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        MapScanBlockCapture capture,
        int attempts,
        long updatedAt) =>
        store.CommitEngineBlockSuccess(request, block, capture, attempts, updatedAt);

    public void CheckpointSuccessBatch(
        MapScanExecutionRequest request,
        IReadOnlyList<MapScanBlockSuccess> successes,
        int attempts,
        long updatedAt) =>
        store.CommitEngineBlockSuccessBatch(request, successes, attempts, updatedAt);

    public void CheckpointFailure(
        MapScanExecutionRequest request,
        MapScanTargetBlock block,
        int attempts,
        string error,
        long updatedAt) =>
        store.CommitEngineBlockFailure(request, block, attempts, error, updatedAt);

    public void Publish(MapScanExecutionRequest request, long updatedAt) =>
        store.PublishEngineScan(request, updatedAt);

    public void Fail(MapScanExecutionRequest request, string error, long updatedAt) =>
        store.FailEngineScan(request, error, updatedAt);

    public void Stop(MapScanExecutionRequest request, long updatedAt) =>
        store.StopEngineScan(request, updatedAt);
}
