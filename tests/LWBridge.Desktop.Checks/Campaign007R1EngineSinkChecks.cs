using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

/// <summary>
/// R1: actual MapScanEngine, Map317ScanRunSink and MapControlPlane/SQLite;
/// provider capture and provider Stop signal are controlled, NOT live Last War.
/// </summary>
internal static class Campaign007R1EngineSinkChecks
{
    private static long now = 1791440000000;
    private static readonly int Server = 2212;
    private static void Require(bool value, string why)
    {
        if (!value) throw new InvalidDataException("R1 actual-engine/sink: " + why);
    }
    private static MapStoredRecord Record(string key) => new(
        "resource", Server, key, 321, null, "r1-" + key, null, 1,
        null, null, null, null, ++now,
        JsonSerializer.Serialize(new { kind = "resource", serverId = Server, recordKey = key }));
    private static Map317.MapRecord PublishedRecord(string key) => new(
        "resource", Server, key, 321, null, "baseline-" + key, null, 1,
        null, null, null, null, ++now, "{}");
    private static MapScanExecutionRequest Request(string runId) =>
        new(runId, Server, 0, 40, 20, ["resource"], 8);
    private static int Count(Map317.MapStore store, string? runId = null) =>
        store.Search(new Map317.MapQuery("resource", Server, ScanRunId: runId)).Total;
    private static readonly Map317.MapProviderContext Context = new(
        true, true, Server, "live", 0, 40, 20, 2);

    private sealed class GatedSource(
        Func<MapScanExecutionRequest, MapScanTargetBlock, IReadOnlySet<int>,
            Action<MapScanSourceProgress>?, CancellationToken,
            Task<IReadOnlyList<MapScanBlockCapture>>> handler)
        : IMapScanProgressBatchSource
    {
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken token) =>
            throw new InvalidOperationException("progress batch path expected");
        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request, MapScanTargetBlock seed,
            IReadOnlySet<int> pending, CancellationToken token) =>
            handler(request, seed, pending, null, token);
        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request, MapScanTargetBlock seed,
            IReadOnlySet<int> pending, Action<MapScanSourceProgress>? progress, CancellationToken token) =>
            handler(request, seed, pending, progress, token);
    }
    private static TaskCompletionSource<bool> Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static IReadOnlyList<MapScanBlockCapture> Captures(
        MapScanExecutionRequest request, IReadOnlyList<int> indices)
    {
        return indices.Select(i => new MapScanBlockCapture(
            request.ServerId, request.WorldId, i, "{}", [Record("captured-" + i)])).ToArray();
    }

    internal static async Task RunAsync(string outputPath)
    {
        var results = new List<object>();
        // Capture still pending: provider may obey cancellation or return after it.
        foreach (bool honorCancellation in new[] { true, false })
        {
            using var store = Map317.MapStore.CreateInMemory();
            store.UpsertRecord(PublishedRecord("old"));
            using var cts = new CancellationTokenSource();
            Task? engineTask = null;
            int providerStops = 0;
            var provider = new Map317.MapProviderAdapter(
                _ => ValueTask.FromResult(Context),
                _ => throw new InvalidOperationException("already world"),
                (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(true, 2)),
                async _ =>
                {
                    providerStops++;
                    cts.Cancel();
                    if (engineTask is not null)
                        try { await engineTask.ConfigureAwait(false); } catch { }
                });
            using var plane = new Map317.MapControlPlane(store, provider, () => ++now);
            var started = await plane.StartScanAsync(
                new Map317.MapScanStartRequest(["resource"], "normal"));
            var entered = Signal();
            var release = Signal();
            var observations = new List<MapScanEngineProgress>();
            var source = new GatedSource(async (req, _, pending, report, token) =>
            {
                Require(pending.SetEquals([0, 1]), "batch pending set changed");
                report?.Invoke(new MapScanSourceProgress(47));
                entered.TrySetResult(true);
                if (honorCancellation)
                    await release.Task.WaitAsync(token).ConfigureAwait(false);
                else
                    await release.Task.ConfigureAwait(false);
                return Captures(req, [0, 1]);
            });
            var engine = new MapScanEngine(source, new Map317ScanRunSink(plane),
                item => {
                    observations.Add(item);
                    plane.ReportProgress(new Map317.MapScanProgressUpdate(
                        item.CompletedBlocks, item.CompletedBlocks, item.FailedBlocks,
                        item.InflightBlocks, item.ScanRate, NativeCaptureReady: true,
                        NativePendingRecords: 0, NativeDroppedRecords: 0,
                        Phase: item.Phase));
                });
            engineTask = Task.Run(() => engine.ExecuteAsync(Request(started.ScanRunId), cts.Token));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Require(Count(store, started.ScanRunId) == 0 && Count(store) == 1 &&
                    observations.Any(p => p.CompletedBlocks == 0 && p.AcquisitionProgressPercent == 47),
                "acquisition progress must not be misreported as staged record");
            Task<Map317.MapScanState> stopping = plane.StopScanAsync().AsTask();
            if (!honorCancellation)
            {
                await WaitForCancelled(store, started.ScanRunId);
                try
                {
                    store.StageRecord(started.ScanRunId, PublishedRecord("direct-late-at-cancel"));
                    throw new InvalidDataException("direct cancelled store staging was accepted");
                }
                catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_SCAN") { }
                release.SetResult(true);
            }
            else release.SetResult(true);
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            try { await engineTask.WaitAsync(TimeSpan.FromSeconds(10)); } catch (OperationCanceledException) { }
            catch (Map317.BridgeCommandException) { }
            Console.WriteLine($"R1_DEFERRED_BATCH_BOUNDARY honorCancellation={honorCancellation} terminal={store.ReadScanRun(started.ScanRunId)?.Status} staged={Count(store,started.ScanRunId)} published={Count(store)}");
            Require(providerStops == 1 && store.ReadScanRun(started.ScanRunId)?.Status == "cancelled" &&
                    Count(store, started.ScanRunId) == 0 && Count(store) == 1 && !plane.ScanState.IsReading,
                "late/full batch after Stop must not stage or publish");
            results.Add(new { scenario = honorCancellation ? "cancel-during-deferred-batch" : "late-batch-return-after-cancel",
                acquisitionPercent = 47, stagedBeforeStop = 0, stagedAfterStop = 0,
                publishedAfterStop = Count(store), terminal = "cancelled", providerStops });
        }
        // One logical block checkpoints positively; next block defers. Stop must
        // remove positive staged rows, not previously published rows.
        await PositiveThenStopAsync(results);
        // Hold production publishing callback. Stop commits durable cancel before
        // callback resumes; engine must not publish a retired run.
        await PublishOverlapAsync(results);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new {
            workItem = "LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007-R1",
            scope = "actual-engine/actual-Map317-sink/actual-Stop and SQLite; controlled provider, NO current-game or original-runtime proof",
            cases = results, livePositiveStageStop = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("R1_ACTUAL_ENGINE_SINK_STOP_BOUNDARIES_PASS " + results.Count);
    }

    private static async Task WaitForCancelled(Map317.MapStore store, string id)
    {
        for (int i = 0; i < 200; i++)
        {
            if (store.ReadScanRun(id)?.Status == "cancelled") return;
            await Task.Delay(5);
        }
        throw new InvalidDataException("R1 cancel did not reach durable store");
    }

    private static async Task PositiveThenStopAsync(List<object> results)
    {
        using var store = Map317.MapStore.CreateInMemory();
        store.UpsertRecord(PublishedRecord("old"));
        using var cts = new CancellationTokenSource();
        Task? task = null;
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(Context), _ => throw new InvalidOperationException("already-world"),
            (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(true, 2)),
            async _ => {
                cts.Cancel();
                if (task is not null) try { await task; } catch { }
            });
        using var plane = new Map317.MapControlPlane(store, provider, () => ++now);
        var started = await plane.StartScanAsync(new Map317.MapScanStartRequest(["resource"], "normal"));
        var secondEntered = Signal();
        var release = Signal();
        var source = new GatedSource(async (req, block, _, progress, token) => {
            if (block.BlockIndex == 0) return Captures(req, [0]);
            secondEntered.SetResult(true);
            await release.Task.WaitAsync(token);
            return Captures(req, [1]);
        });
        task = Task.Run(() => new MapScanEngine(source, new Map317ScanRunSink(plane),
            p => plane.ReportProgress(new Map317.MapScanProgressUpdate(
                p.CompletedBlocks, p.CompletedBlocks, p.FailedBlocks,
                p.InflightBlocks, p.ScanRate, NativeCaptureReady: true, NativePendingRecords: 0,
                NativeDroppedRecords: 0, Phase: p.Phase)))
            .ExecuteAsync(Request(started.ScanRunId), cts.Token));
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Require(Count(store, started.ScanRunId) == 1 && Count(store) == 1,
            "positive staging not present before Stop");
        await plane.StopScanAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        release.TrySetResult(true);
        try { await task.WaitAsync(TimeSpan.FromSeconds(10)); } catch (OperationCanceledException) { }
        Require(store.ReadScanRun(started.ScanRunId)?.Status == "cancelled" &&
                Count(store, started.ScanRunId) == 0 && Count(store) == 1,
            "positive staging not removed while publication retained");
        results.Add(new { scenario = "positive-stage-then-stop", stagedBeforeStop = 1,
            stagedAfterStop = 0, publishedAfterStop = 1, terminal = "cancelled" });
    }

    private static async Task PublishOverlapAsync(List<object> results)
    {
        using var store = Map317.MapStore.CreateInMemory();
        store.UpsertRecord(PublishedRecord("old"));
        using var cts = new CancellationTokenSource();
        Task? task = null;
        var atPublishing = Signal();
        var resumePublishing = Signal();
        var provider = new Map317.MapProviderAdapter(
            _ => ValueTask.FromResult(Context), _ => throw new InvalidOperationException("already-world"),
            (_, _) => ValueTask.FromResult(new Map317.MapProviderStartResult(true, 2)),
            async _ => {
                cts.Cancel();
                if (task is not null) try { await task; } catch { }
            });
        using var plane = new Map317.MapControlPlane(store, provider, () => ++now);
        var started = await plane.StartScanAsync(new Map317.MapScanStartRequest(["resource"], "normal"));
        var source = new GatedSource((req, _, _, _, _) =>
            Task.FromResult(Captures(req, [0, 1])));
        task = Task.Run(() => new MapScanEngine(source, new Map317ScanRunSink(plane), p => {
            plane.ReportProgress(new Map317.MapScanProgressUpdate(
                p.CompletedBlocks, p.CompletedBlocks, p.FailedBlocks,
                p.InflightBlocks, p.ScanRate, NativeCaptureReady: true, NativePendingRecords: 0,
                NativeDroppedRecords: 0, Phase: p.Phase));
            if (p.Phase == "publishing") {
                atPublishing.TrySetResult(true);
                resumePublishing.Task.GetAwaiter().GetResult();
            }
        }).ExecuteAsync(Request(started.ScanRunId), cts.Token));
        await atPublishing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Require(Count(store, started.ScanRunId) == 2 && Count(store) == 1,
            "publishing overlap must begin with positive staged rows");
        Task<Map317.MapScanState> stop = plane.StopScanAsync().AsTask();
        await WaitForCancelled(store, started.ScanRunId);
        resumePublishing.SetResult(true);
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        try { await task.WaitAsync(TimeSpan.FromSeconds(10)); } catch (Map317.BridgeCommandException) { }
        catch (InvalidDataException) { }
        Require(store.ReadScanRun(started.ScanRunId)?.Status == "cancelled" &&
                Count(store, started.ScanRunId) == 0 && Count(store) == 1,
            "cancelled run must not publish when publishing callback resumes");
        results.Add(new { scenario = "stop-during-publication-transition",
            stagedBeforeStop = 2, stagedAfterStop = 0,
            publishedAfterStop = 1, terminal = "cancelled" });
    }
}
