using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

// Actual MapScanStrategyPlanner + MapScanEngine + record staging/publication
// boundaries, with controlled independent witness datasets. No live game action.
const int server = 2212;
string[] kinds = MapScanContract.AllTypes;
var runs = new List<object>();
int selectionCases = 0;
long totalElapsed = 0;
foreach (int mask in Enumerable.Range(1, (1 << kinds.Length) - 1))
{
    string[] selected = kinds.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
    string mode = mask % 2 == 0 ? "normal" : "fast";
    var plan = MapScanStrategyPlanner.Plan(new CurrentClientMapContext(server, 0, 1000, 1000, LaunchSessionId: "controlled-owned-session"), selected, mode);
    Check(plan.ScanMode == mode && plan.Concurrency == (mode == "normal" ? 8 : 20), "Incorrect planner mode or host requested limit");
    var witnesses = Witnesses(selected, server);
    var source = new CaptureSource(witnesses);
    var sink = new WitnessSink(witnesses);
    MapScanEngineStageMetrics? stageMetrics = null;
    var stopwatch = Stopwatch.StartNew();
    await new MapScanEngine(source, sink, completedStageMetrics: metrics =>
        stageMetrics = metrics).ExecuteAsync(Request(selected, mode));
    stopwatch.Stop();
    Check(sink.Published && sink.PublishedCount == selected.Length && source.ActivePeak == 1, "Population missing or camera concurrency incorrect");
    Check(stageMetrics is not null && stageMetrics.ServerId == server &&
          stageMetrics.ScanMode == mode && stageMetrics.SourceCaptureMilliseconds >= 0 &&
          stageMetrics.CheckpointMilliseconds >= 0 && stageMetrics.PublicationMilliseconds >= 0 &&
          stageMetrics.WholeEngineMilliseconds >= 0,
        "Completed scan omitted its exact engine staging/publication timing callback");
    selectionCases++;
    totalElapsed += stopwatch.ElapsedMilliseconds;
    if (mask == 255 || mask == 7 || mask == 56 || mask == 9 || mask == 1 || mask == 128)
        runs.Add(new { mask, selected, mode, plan.StrategyId, source.Calls, source.ActivePeak, sink.PublishedCount, elapsedMs = stopwatch.Elapsed.TotalMilliseconds });
}

// Diagnostic I/O can fail after an already committed durable publication.
// Such a failure must not convert that completed scan into a failed run or
// attempt to publish a second time.
{
    var witness = Witnesses(["city"], server);
    var sink = new WitnessSink(witness);
    await new MapScanEngine(new CaptureSource(witness), sink,
        completedStageMetrics: _ => throw new IOException("controlled diagnostic storage unavailable"))
        .ExecuteAsync(Request(["city"], "normal"));
    Check(sink.Published && sink.PublishedCount == 1,
        "A post-publication diagnostic failure changed a successful scan outcome");
}

// A source deliberately misbehaving at an actual production-engine boundary
// must fail before publication. These also demonstrate why a plausible count or
// covered-block counter is not an independent population/field correctness oracle.
var rejections = new List<string>();
foreach (string kind in new[] { "wrong-server", "wrong-world", "unselected-kind", "duplicate-block", "missing-seed", "empty-payload", "partial", "native-error" })
{
    var witness = Witnesses(["city", "truck"], server);
    var badSource = new CaptureSource(witness, fault: kind);
    var sink = new WitnessSink(witness);
    bool emittedCompletedStageMetrics = false;
    try
    {
        await new MapScanEngine(badSource, sink,
            completedStageMetrics: _ => emittedCompletedStageMetrics = true)
            .ExecuteAsync(Request(["city", "truck"], "fast"));
        throw new Exception(kind + " incorrectly published");
    }
    catch (Exception error) when (error is InvalidDataException or BridgeCommandException)
    {
        Check(!sink.Published && !emittedCompletedStageMetrics,
            kind + " published corrupted coverage or success diagnostics");
        rejections.Add(kind);
    }
}

// Independent record identity/value/recency/population checks must reject an
// omitted known row, one missing selected kind, an extra identity, wrong values,
// duplicate IDs, wrong-server rows and stale records even if all blocks completed.
var trueWitness = Witnesses(kinds, server);
var distortions = new[] { "omitted-known-record", "dropped-selected-kind", "extra-record", "wrong-field", "duplicate-identity", "wrong-server-record", "stale-record" };
foreach (string fault in distortions)
{
    MapStoredRecord[] rows = trueWitness.Values.ToArray();
    MapStoredRecord[] distorted = fault switch
    {
        "omitted-known-record" => rows.Skip(1).ToArray(),
        "dropped-selected-kind" => rows.Where(row => row.Kind != "ghost").ToArray(),
        "extra-record" => [..rows, rows[0] with { RecordKey = "extra-unobserved", Uuid = "extra-unobserved" }],
        "wrong-field" => [rows[0] with { Level = rows[0].Level + 1 }, ..rows.Skip(1)],
        "duplicate-identity" => [..rows, rows[0]],
        "wrong-server-record" => [rows[0] with { ServerId = 2213 }, ..rows.Skip(1)],
        "stale-record" => [rows[0] with { UpdatedAt = 1 }, ..rows.Skip(1)],
        _ => throw new Exception(fault),
    };
    try { CompareIndependentWitness(trueWitness, distorted, server, kinds); throw new Exception("Independent witness missed " + fault); }
    catch (InvalidDataException) { rejections.Add(fault); }
}

// Two distinct mode placement policies on bounded representative native-geometry
// simulations: exact union is a prerequisite, comparison counts are controlled
// *requests* and are NOT genuine live throughput observations.
var simulations = new List<object>();
foreach (int width in new[] { 2, 3, 4, 5 })
{
    var simulation = new Dictionary<string, int>();
    foreach (string mode in new[] { "normal", "fast" })
    {
        var covered = new HashSet<int>();
        int attempts = 0;
        bool preferWide = false;
        int? previousGap = null;
        while (covered.Count != 100)
        {
            int gap = Enumerable.Range(0, 100).First(index => !covered.Contains(index));
            int target = MapFullWorldAoiPlacement.TargetColumn(mode, gap, preferWide, previousGap);
            int first = Math.Clamp(target - Math.Min(2, width - 1), 0, 100 - width);
            int last = first + width - 1;
            for (int col = first; col <= last; col++) covered.Add(col);
            int nextGap = Enumerable.Range(0, 100).FirstOrDefault(index => !covered.Contains(index), -1);
            preferWide = width >= 4 && nextGap != gap;
            previousGap = nextGap == gap ? gap : null;
            if (++attempts > 300) throw new Exception("Unbounded coverage retry in simulation");
        }
        Check(covered.Count == 100, "AOI union incomplete");
        simulation.Add(mode, attempts);
    }
    simulations.Add(new { footprintWidth = width, coverage = 100, normalProbes = simulation["normal"], fastProbes = simulation["fast"] });
}

// Dense, repeated bounded publication across two independent servers and all
// eight kinds. Force slow captures and ensure no per-cycle records or pending
// rows leak into the next run. Memory is only a controlled .NET heap indicator.
const int denseRecordsPerKind = 375;
const int stressCycles = 12;
long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
long peakRetainedMemory = memoryBefore;
int stressRows = 0;
int maxInflight = 0;
var stressWatch = Stopwatch.StartNew();
for (int cycle = 0; cycle < stressCycles; cycle++)
{
    int targetServer = cycle % 2 == 0 ? 2212 : 2213;
    var population = DenseWitnesses(kinds, targetServer, denseRecordsPerKind);
    var source = new CaptureSource(population, delayMs: 5);
    var sink = new WitnessSink(population);
    var request = Request(kinds, cycle % 2 == 0 ? "normal" : "fast") with
    {
        ServerId = targetServer, LiveServerId = targetServer,
        RunId = "controlled-dense-" + cycle,
    };
    await new MapScanEngine(source, sink).ExecuteAsync(request);
    Check(sink.Published && sink.PublishedCount == denseRecordsPerKind * kinds.Length,
        "Dense producer lost a selected type or record");
    Check(source.Calls == 4 && source.ActivePeak == 1, "Dense capture queue expanded");
    stressRows += sink.PublishedCount;
    maxInflight = Math.Max(maxInflight, source.ActivePeak);
    peakRetainedMemory = Math.Max(peakRetainedMemory, GC.GetTotalMemory(forceFullCollection: true));
}
stressWatch.Stop();
Check(peakRetainedMemory - memoryBefore < 128L * 1024 * 1024,
    "Unbounded retained memory in controlled repeated dense scans");

// A held stale completion cannot publish after cancellation. Slow captures still
// keep actual inflight <=1; failure preserves previously published owner data.
var staleSource = new CaptureSource(Witnesses(["city"], server), hold: true);
var staleSink = new WitnessSink(staleSource.Expected);
using (var cts = new CancellationTokenSource())
{
    Task run = new MapScanEngine(staleSource, staleSink).ExecuteAsync(Request(["city"], "normal"), cts.Token);
    await staleSource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    cts.Cancel();
    staleSource.Release.SetResult(true);
    try { await run; throw new Exception("Cancelled run incorrectly completed"); }
    catch (OperationCanceledException) { Check(staleSink.Stopped && !staleSink.Published, "Cancelled run published stale result"); }
}

var report = new {
    schemaVersion = 1, evidence = "Production planner and engine with CONTROLLED independent records, no live timing or population proof",
    selectionCases, totalKinds = kinds.Length, representativeRuns = runs,
    rejectedAdversities = rejections, simulatedAoiModes = simulations,
    controlledEnginePeakInflight = 1, abortedStaleRunCannotPublish = true,
    denseStress = new { stressCycles, denseRecordsPerKind, totalPublishedRowsAcrossIsolatedCycles = stressRows,
        maxInflight, elapsedMs = stressWatch.Elapsed.TotalMilliseconds,
        memoryBeforeBytes = memoryBefore, peakRetainedMemoryBytes = peakRetainedMemory,
        memoryBoundBytes = 128L * 1024 * 1024 },
    rawNetworkPacketCounts = (int?)null, actualGameActions = 0,
};
string formatted = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (args.Length > 0) File.WriteAllText(args[0], formatted + Environment.NewLine);
Console.WriteLine("MAP008_SCAN_CONTROLLED_PASS selections=" + selectionCases + " adverse=" + rejections.Count + " elapsedMs=" + totalElapsed);

static MapScanExecutionRequest Request(string[] selected, string mode) =>
    new("controlled-map008-" + string.Join("-", selected), 2212, 0, 40, 40, selected,
        mode == "fast" ? 20 : 8, ScanMode: mode, LaunchSessionId: "controlled-owned-session", LiveServerId: 2212);

static Dictionary<string, MapStoredRecord> Witnesses(IEnumerable<string> selected, int server) =>
    selected.ToDictionary(kind => kind + ":" + server + ":native-id", kind =>
        new MapStoredRecord(kind, server, kind + ":" + server + ":native-id",
            101, kind + "-native-id", "native-name", "native-alliance", 100, 4,
            9999, 3.5, 0, 1791693000000,
            JsonSerializer.Serialize(new { nativeField = "known-value", kind, server })), StringComparer.Ordinal);

static Dictionary<string, MapStoredRecord> DenseWitnesses(IEnumerable<string> selected, int server, int each)
{
    var data = new Dictionary<string, MapStoredRecord>(StringComparer.Ordinal);
    foreach (string kind in selected)
        for (int index = 0; index < each; index++)
        {
            string key = kind + ":" + server + ":" + index;
            data.Add(key, new MapStoredRecord(kind, server, key, index,
                kind + "-" + index, "name", "alliance", 10 + index, 4, 100000,
                1.5, 0, 1791693000000, JsonSerializer.Serialize(new { kind, server, index })));
        }
    return data;
}

static void CompareIndependentWitness(
    IReadOnlyDictionary<string, MapStoredRecord> baseline, IReadOnlyList<MapStoredRecord> candidate,
    int server, IReadOnlyList<string> selected)
{
    if (candidate.Any(row => row.ServerId != server || row.UpdatedAt < 1791692000000))
        throw new InvalidDataException("wrong-server or stale native record");
    if (candidate.Count != candidate.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count())
        throw new InvalidDataException("duplicate native identity");
    if (selected.Any(kind => !candidate.Any(row => row.Kind == kind)))
        throw new InvalidDataException("selected population omitted");
    var actual = candidate.ToDictionary(row => row.RecordKey, StringComparer.Ordinal);
    if (!baseline.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actual.Keys))
        throw new InvalidDataException("omitted or extra native identity");
    foreach ((string key, MapStoredRecord expected) in baseline)
    {
        MapStoredRecord value = actual[key];
        if (value != expected) throw new InvalidDataException("native value differs: " + key);
    }
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

sealed class CaptureSource(IReadOnlyDictionary<string, MapStoredRecord> expected, string? fault = null, bool hold = false, int delayMs = 0) : IMapScanBlockSource
{
    internal IReadOnlyDictionary<string, MapStoredRecord> Expected => expected;
    internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int active;
    internal int ActivePeak { get; private set; }
    internal int Calls { get; private set; }
    public async Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request,
        MapScanTargetBlock block, CancellationToken cancellationToken)
    {
        Calls++;
        int activeNow = Interlocked.Increment(ref active);
        ActivePeak = Math.Max(ActivePeak, activeNow);
        try
        {
            Entered.TrySetResult(true);
            if (hold) await Release.Task.ConfigureAwait(false);
            else if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
            else await Task.Yield();
            if (fault == "native-error") throw new InvalidDataException("unsupported native response");
            var data = block.BlockIndex == 0 ? expected.Values.ToArray() : [];
            if (fault == "unselected-kind") data = [new MapStoredRecord("ghost", request.ServerId, "injected", null, null, null, null, null, null, null, null, null, 1791693000000, "{}")];
            if (fault == "wrong-server") return new MapScanBlockCapture(2213, request.WorldId, block.BlockIndex, "{}", data);
            if (fault == "wrong-world") return new MapScanBlockCapture(request.ServerId, 9, block.BlockIndex, "{}", data);
            if (fault == "duplicate-block") return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex + 1, "{}", data);
            if (fault == "missing-seed") return new MapScanBlockCapture(request.ServerId, request.WorldId, -1, "{}", data);
            if (fault == "empty-payload") return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, "", data);
            if (fault == "partial") throw new InvalidDataException("missing last native response");
            return new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, "{\"fresh\":true}", data);
        }
        finally { Interlocked.Decrement(ref active); }
    }
}

sealed class WitnessSink(IReadOnlyDictionary<string, MapStoredRecord> expected) : IMapScanRunSink
{
    private readonly Dictionary<string, MapStoredRecord> staged = new(StringComparer.Ordinal);
    internal bool Published { get; private set; }
    internal bool Stopped { get; private set; }
    internal int PublishedCount { get; private set; }
    public void Begin(MapScanExecutionRequest request, int count, long time) { }
    public void CheckpointSuccess(MapScanExecutionRequest request, MapScanTargetBlock block, MapScanBlockCapture capture, int attempts, long time)
    {
        foreach (MapStoredRecord row in capture.Records)
        {
            if (!staged.TryAdd(row.RecordKey, row)) throw new InvalidDataException("duplicate identity");
        }
    }
    public void CheckpointFailure(MapScanExecutionRequest request, MapScanTargetBlock block, int attempts, string error, long time) { }
    public void Publish(MapScanExecutionRequest request, long time)
    {
        ComparePublished(expected, staged);
        Published = true;
        PublishedCount = staged.Count;
    }
    public void Fail(MapScanExecutionRequest request, string error, long time) { }
    public void Stop(MapScanExecutionRequest request, long time) { Stopped = true; }
    private static void ComparePublished(IReadOnlyDictionary<string, MapStoredRecord> witness, IReadOnlyDictionary<string, MapStoredRecord> candidate)
    {
        if (witness.Count != candidate.Count || witness.Any(pair => !candidate.TryGetValue(pair.Key, out var row) || row != pair.Value))
            throw new InvalidDataException("controlled native witness did not match staged rows");
    }
}
