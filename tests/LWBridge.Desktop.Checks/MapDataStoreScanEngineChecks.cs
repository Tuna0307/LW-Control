using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapDataStoreScanEngineChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        SuccessfulScanReplacesPublishedRows();
        StoppedRunCannotPublish();
        FailedRunCannotPublish();
    }

    private static void SuccessfulScanReplacesPublishedRows()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        store.UpsertRecord(Record("old", 1));
        var request = Request("store-success");
        var source = new SingleCaptureSource(Record("fresh", 2));
        var engine = new MapScanEngine(source, new MapDataStoreScanSink(store));

        engine.ExecuteAsync(request).GetAwaiter().GetResult();

        MapSearchResult result = store.SearchIndexed(Query());
        Check(result.Total == 1, "completed engine publication replaces the selected published kind");
        string key = result.Rows[0].GetProperty("recordKey").GetString() ?? string.Empty;
        Check(key == "fresh", "completed engine publication exposes only freshly staged row");
        Check(store.ReadScanBlockCheckpointsForTest(request.RunId).Count == 0,
            "completed publication removes block staging after commit");
    }

    private static void StoppedRunCannotPublish()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        store.UpsertRecord(Record("old", 1));
        var request = Request("store-stopped");
        var sink = new MapDataStoreScanSink(store);
        sink.Begin(request, 1, 100);
        MapScanTargetBlock block = MapScanTraversal.Build(20, 20)[0];
        sink.CheckpointSuccess(request, block,
            new MapScanBlockCapture(2212, 7, 0, "{}", [Record("staged", 2)]), 1, 101);
        sink.Stop(request, 102);

        ExpectInvalidScan(() => sink.Publish(request, 103));
        MapSearchResult result = store.SearchIndexed(Query());
        Check(result.Total == 1 && result.Rows[0].GetProperty("recordKey").GetString() == "old",
            "stopped run cannot replace previously published rows");
    }

    private static void FailedRunCannotPublish()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        store.UpsertRecord(Record("old", 1));
        var request = Request("store-failed");
        var sink = new MapDataStoreScanSink(store);
        sink.Begin(request, 1, 200);
        MapScanTargetBlock block = MapScanTraversal.Build(20, 20)[0];
        sink.CheckpointFailure(request, block, 2, "capture failed", 201);
        sink.Fail(request, "direct map scan contains failed batches", 202);

        ExpectInvalidScan(() => sink.Publish(request, 203));
        MapSearchResult result = store.SearchIndexed(Query());
        Check(result.Total == 1 && result.Rows[0].GetProperty("recordKey").GetString() == "old",
            "failed run cannot replace previously published rows");
    }

    private static MapScanExecutionRequest Request(string runId) =>
        new(runId, 2212, 7, 20, 20, ["city"], 1, 2);

    private static MapStoredRecord Record(string key, long updatedAt) =>
        new(
            "city", 2212, key, 1, key, key, null,
            30, null, null, null, null, updatedAt,
            $"{{\"recordKey\":\"{key}\",\"kind\":\"city\",\"serverId\":2212,\"updatedAt\":{updatedAt}}}");

    private static MapDataQueryOptions Query() =>
        new(
            "city", 2212, 1, 50,
            [new MapDataSort("updatedAt", "desc")],
            false, null, null, false, null, null, null, null, null, null, null,
            false, false, false, null, null, []);

    private static void ExpectInvalidScan(Action action)
    {
        try
        {
            action();
        }
        catch (BridgeCommandException error) when (
            error.Code == "INVALID_SCAN" && error.Message == "map scan is not running")
        {
            return;
        }
        throw new InvalidOperationException("expected stale/non-running scan publication rejection");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SingleCaptureSource : IMapScanBlockSource
    {
        private readonly MapStoredRecord record;

        public SingleCaptureSource(MapStoredRecord record)
        {
            this.record = record;
        }

        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new MapScanBlockCapture(
                request.ServerId,
                request.WorldId,
                block.BlockIndex,
                "{}",
                [record]));
        }
    }
}
