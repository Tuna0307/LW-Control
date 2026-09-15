using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapScanEngineChecks
{
    [ModuleInitializer]
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        await SuccessPublishesOnlyAfterCheckpoint();
        await RetryThenSuccessRecordsAttemptCount();
        await ExhaustedRetriesFailWithoutPublication();
        await CancellationStopsWithoutPublication();
        await ForeignCaptureFailsClosed();
    }

    private static MapScanExecutionRequest Request(int width = 20, int height = 20) =>
        new("run-check", 2212, 7, width, height, ["city"], 8, 2);

    private static MapScanBlockCapture Capture(MapScanExecutionRequest request, MapScanTargetBlock block) =>
        new(request.ServerId, request.WorldId, block.BlockIndex, "{}", [Record(request.ServerId)]);

    private static MapStoredRecord Record(int serverId) =>
        new("city", serverId, "123", 123, "u", "P", "A", 30, null, null, null, null, 1, "{}");

    private static async Task SuccessPublishesOnlyAfterCheckpoint()
    {
        var sink = new RecordingSink();
        var source = new DelegateSource((request, block, _) => Task.FromResult(Capture(request, block)));
        var engine = new MapScanEngine(source, sink);
        await engine.ExecuteAsync(Request());

        Check(sink.Events.SequenceEqual(["begin", "success:0:1", "publish"]),
            "successful engine run checkpoints before publication");
    }

    private static async Task RetryThenSuccessRecordsAttemptCount()
    {
        int calls = 0;
        var sink = new RecordingSink();
        var source = new DelegateSource((request, block, _) =>
        {
            calls++;
            if (block.BlockIndex == 0 && calls == 1)
                throw new IOException("transient");
            return Task.FromResult(Capture(request, block));
        });
        await new MapScanEngine(source, sink).ExecuteAsync(Request(21, 20));

        Check(calls == 3 && sink.Events.Contains("success:0:2") && sink.Events.Contains("success:1:1"),
            "engine retries a failed block and preserves the successful attempt count");
    }

    private static async Task ExhaustedRetriesFailWithoutPublication()
    {
        var sink = new RecordingSink();
        var source = new DelegateSource((_, _, _) => throw new IOException("permanent"));
        try
        {
            await new MapScanEngine(source, sink).ExecuteAsync(Request());
            throw new InvalidOperationException("expected terminal scan failure");
        }
        catch (BridgeCommandException error) when (
            error.Code == "INCOMPLETE_SCAN" && error.Message == "direct map scan contains failed batches")
        {
        }

        Check(sink.Events.SequenceEqual(["begin", "failure:0:2", "fail"]),
            "exhausted block retries fail the run without publication");
    }

    private static async Task CancellationStopsWithoutPublication()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sink = new RecordingSink();
        var source = new DelegateSource((request, block, _) => Task.FromResult(Capture(request, block)));
        try
        {
            await new MapScanEngine(source, sink).ExecuteAsync(Request(), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }

        Check(sink.Events.SequenceEqual(["begin", "stop"]),
            "cancellation stops the owned run without publication");
    }

    private static async Task ForeignCaptureFailsClosed()
    {
        var sink = new RecordingSink();
        var source = new DelegateSource((request, block, _) => Task.FromResult(
            new MapScanBlockCapture(request.ServerId + 1, request.WorldId, block.BlockIndex, "{}", [])));
        try
        {
            await new MapScanEngine(source, sink).ExecuteAsync(Request());
            throw new InvalidOperationException("expected foreign capture rejection");
        }
        catch (InvalidDataException error) when (
            error.Message == "map block capture did not match the active server")
        {
        }

        Check(sink.Events.SequenceEqual(["begin", "fail"]),
            "foreign-server captures fail immediately without checkpoint or publication");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class DelegateSource(
        Func<MapScanExecutionRequest, MapScanTargetBlock, CancellationToken, Task<MapScanBlockCapture>> capture)
        : IMapScanBlockSource
    {
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken) => capture(request, block, cancellationToken);
    }

    private sealed class RecordingSink : IMapScanRunSink
    {
        public List<string> Events { get; } = [];

        public void Begin(MapScanExecutionRequest request, int totalBlocks, long updatedAt) => Events.Add("begin");
        public void CheckpointSuccess(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            MapScanBlockCapture capture,
            int attempts,
            long updatedAt) => Events.Add($"success:{block.BlockIndex}:{attempts}");
        public void CheckpointFailure(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            int attempts,
            string error,
            long updatedAt) => Events.Add($"failure:{block.BlockIndex}:{attempts}");

        public void Publish(MapScanExecutionRequest request, long updatedAt) => Events.Add("publish");
        public void Fail(MapScanExecutionRequest request, string error, long updatedAt) => Events.Add("fail");
        public void Stop(MapScanExecutionRequest request, long updatedAt) => Events.Add("stop");
    }
}
