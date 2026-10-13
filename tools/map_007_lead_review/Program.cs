using System.Reflection;
using System.Text.Json;
using LWBridge.Desktop;

// Inert source + sink, actual production engine and actual batch-admission predicate.
// No game provider is constructed; this is execution behavior, not a live speed test.
var cases = new List<object>();
foreach ((string mode, int requested) in new[] { ("normal", 8), ("fast", 20) })
{
    var source = new HeldSource();
    var sink = new InertSink();
    var request = new MapScanExecutionRequest(mode, 317, 0, 40, 40, ["truck"], requested, ScanMode: mode);
    await new MapScanEngine(source, sink).ExecuteAsync(request);
    var fullWorld = request with { TileWidth = 1000, TileHeight = 1000 };
    MethodInfo admission = typeof(CurrentClientMapBlockSource).GetMethod("CanUseFastCityBatch",
        BindingFlags.NonPublic | BindingFlags.Static) ?? throw new Exception("Batch predicate missing");
    bool batch = (bool)admission.Invoke(null, [fullWorld])!;
    cases.Add(new { mode, requestedConcurrency = requested, observedConcurrentCaptures = source.Peak,
        captureCalls = source.Calls, checkpoints = sink.Checkpoints, published = sink.Published,
        sameFullWorldBatchAdmission = batch });
}
Console.WriteLine(JsonSerializer.Serialize(new { finding = "LEADSCAN008-01", cases,
    proof = "Actual production engine with controlled asynchronous source, not live timing",
    externalGameActions = 0 }, new JsonSerializerOptions { WriteIndented = true }));

sealed class HeldSource : IMapScanBlockSource
{
    public int Calls, Peak;
    private int active;
    public async Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request,
        MapScanTargetBlock block, CancellationToken cancellationToken)
    {
        Calls++;
        Peak = Math.Max(Peak, Interlocked.Increment(ref active));
        try
        {
            await Task.Delay(10, cancellationToken);
            return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, "{}", []);
        }
        finally { Interlocked.Decrement(ref active); }
    }
}
sealed class InertSink : IMapScanRunSink
{
    public int Checkpoints;
    public bool Published;
    public void Begin(MapScanExecutionRequest r, int total, long at) { }
    public void CheckpointSuccess(MapScanExecutionRequest r, MapScanTargetBlock b,
        MapScanBlockCapture c, int attempts, long at) { Checkpoints++; }
    public void CheckpointFailure(MapScanExecutionRequest r, MapScanTargetBlock b,
        int attempts, string error, long at) => throw new Exception(error);
    public void Publish(MapScanExecutionRequest r, long at) { Published = true; }
    public void Fail(MapScanExecutionRequest r, string error, long at) => throw new Exception(error);
    public void Stop(MapScanExecutionRequest r, long at) => throw new Exception("Unexpected Stop");
}
