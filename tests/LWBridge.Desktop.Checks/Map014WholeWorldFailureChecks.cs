using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Map014WholeWorldFailureChecks
{
    private sealed class NativeBatch : IMapScanProgressBatchSource, IMapScanAcquisitionCleanup
    {
        internal int Calls;
        internal int CleanupCount;
        internal MapScanAcquisitionExitReason? ExitReason;
        public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest _, MapScanTargetBlock __,
            CancellationToken ___) => throw new InvalidOperationException("Source switched to legacy fallback.");
        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(MapScanExecutionRequest request,
            MapScanTargetBlock block, IReadOnlySet<int> pending, CancellationToken token) =>
            CaptureBatchAsync(request, block, pending, null, token);
        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(MapScanExecutionRequest request,
            MapScanTargetBlock block, IReadOnlySet<int> pending, Action<MapScanSourceProgress>? progress,
            CancellationToken token)
        {
            Calls++;
            if (pending.Count != 2500 || block.BlockIndex != 0)
                throw new InvalidOperationException("Whole-world negative entered a partial legacy scan.");
            throw new MapScanSourceValidationException(
                "Full-world native source failed before durable block publication: " +
                "Fast full-world adaptive acquisition returned a non-rectangular v18 AOI footprint.");
        }
        public Task<MapScanAcquisitionCleanupReceipt> CleanupAsync(MapScanExecutionRequest request,
            MapScanAcquisitionExitReason reason, CancellationToken token)
        {
            CleanupCount++;
            ExitReason = reason;
            return Task.FromResult(new MapScanAcquisitionCleanupReceipt(request,
                OriginalCameraRestored: true, FreshHomeViewReplyMatched: true));
        }
    }
    private sealed class Sink : IMapScanRunSink
    {
        internal int FailedBlocks, Staged, Published, FailedRuns;
        internal string? Error;
        public void Begin(MapScanExecutionRequest request, int totalBlocks, long timestamp)
        {
            if (totalBlocks != 2500) throw new Exception("Negative whole-world test lacks 2,500 blocks.");
        }
        public void CheckpointSuccess(MapScanExecutionRequest request, MapScanTargetBlock block,
            MapScanBlockCapture capture, int attempts, long timestamp) => Staged++;
        public void CheckpointFailure(MapScanExecutionRequest request, MapScanTargetBlock block,
            int attempts, string error, long timestamp) => FailedBlocks++;
        public void Publish(MapScanExecutionRequest request, long timestamp) => Published++;
        public void Fail(MapScanExecutionRequest request, string error, long timestamp)
        {
            FailedRuns++;
            Error = error;
        }
        public void Stop(MapScanExecutionRequest request, long timestamp) =>
            throw new Exception("Fatal native source failure must not masquerade as an owner Stop.");
    }
    internal static async Task RunAsync()
    {
        var source = new NativeBatch();
        var sink = new Sink();
        var request = new MapScanExecutionRequest("run-negative-8-rows", 2212, 0, 1000, 1000,
            ["truck", "dispatch"], 20, LaunchSessionId: "owned-session", ScanMode: "fast");
        try
        {
            await new MapScanEngine(source, sink).ExecuteAsync(request).ConfigureAwait(false);
            throw new Exception("Bad native whole-world source was allowed to publish.");
        }
        catch (MapScanSourceValidationException error)
        {
            if (!error.Message.Contains("non-rectangular v18 AOI footprint", StringComparison.Ordinal))
                throw;
        }
        if (source.Calls != 1 || source.CleanupCount != 1 ||
            source.ExitReason != MapScanAcquisitionExitReason.Failed ||
            sink.Staged != 0 || sink.FailedBlocks != 0 ||
            sink.Published != 0 || sink.FailedRuns != 1 ||
            !sink.Error!.Contains("non-rectangular v18 AOI footprint", StringComparison.Ordinal))
            throw new Exception("Full-world source error fanned out into false block progress or bypassed cleanup.");
        Console.WriteLine("MAP014_WHOLE_WORLD_TERMINAL_NEGATIVE_OK calls=1 falseFailedBlocks=0 stage=0 publication=0 exactCleanup=1");
    }
}
