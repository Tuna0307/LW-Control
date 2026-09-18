using System.Runtime.CompilerServices;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapDataStoreScanEngineChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        SuccessfulScanReplacesPublishedRows();
        UnresolvedMonsterProtectionCarriesForwardKnownDeadline();
        KnownInactiveMonsterProtectionClearsPriorDeadline();
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

    private static void UnresolvedMonsterProtectionCarriesForwardKnownDeadline()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        const long knownDeadline = 2_000_000_000_000L;
        store.UpsertRecord(MonsterRecord("boss", 100, known: true, active: true, knownDeadline));

        var request = MonsterRequest("store-monster-carry");
        var source = new SingleCaptureSource(
            MonsterRecord("boss", 200, known: false, active: false, deadline: null));
        new MapScanEngine(source, new MapDataStoreScanSink(store))
            .ExecuteAsync(request).GetAwaiter().GetResult();

        MapStoredRecord row = store.GetRecord("monster", 2212, "boss")
            ?? throw new InvalidOperationException("carried Monster row was not published");
        Check(row.ShieldEndTime == knownDeadline &&
              row.DataJson.Contains("\"monsterProtectionKnown\":true", StringComparison.Ordinal) &&
              row.DataJson.Contains("\"monsterProtectionActive\":true", StringComparison.Ordinal) &&
              row.DataJson.Contains("\"monsterProtectionEndTime\":2000000000000", StringComparison.Ordinal) &&
              row.DataJson.Contains("\"shieldEndTime\":2000000000000", StringComparison.Ordinal),
            "unresolved fresh Monster protection must preserve a still-live prior authoritative deadline");
    }

    private static void KnownInactiveMonsterProtectionClearsPriorDeadline()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        const long knownDeadline = 2_000_000_000_000L;
        store.UpsertRecord(MonsterRecord("boss", 100, known: true, active: true, knownDeadline));

        var request = MonsterRequest("store-monster-clear");
        var source = new SingleCaptureSource(
            MonsterRecord("boss", 200, known: true, active: false, deadline: null));
        new MapScanEngine(source, new MapDataStoreScanSink(store))
            .ExecuteAsync(request).GetAwaiter().GetResult();

        MapStoredRecord row = store.GetRecord("monster", 2212, "boss")
            ?? throw new InvalidOperationException("inactive Monster row was not published");
        Check(row.ShieldEndTime is null &&
              row.DataJson.Contains("\"monsterProtectionKnown\":true", StringComparison.Ordinal) &&
              row.DataJson.Contains("\"monsterProtectionActive\":false", StringComparison.Ordinal),
            "fresh authoritative inactive Monster protection must clear a prior countdown");
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

    private static MapScanExecutionRequest MonsterRequest(string runId) =>
        new(runId, 2212, 7, 20, 20, ["monster"], 1, 2);

    private static MapStoredRecord MonsterRecord(
        string key,
        long updatedAt,
        bool known,
        bool active,
        long? deadline)
    {
        string deadlineJson = deadline is null ? "null" : deadline.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string data =
            $"{{\"recordKey\":\"{key}\",\"kind\":\"monster\",\"serverId\":2212," +
            $"\"x\":1,\"y\":1,\"monsterProtectionEligible\":true," +
            $"\"monsterProtectionKnown\":{known.ToString().ToLowerInvariant()}," +
            $"\"monsterProtectionActive\":{active.ToString().ToLowerInvariant()}," +
            $"\"monsterProtectionEndTime\":{(active && deadline is not null ? deadlineJson : "0")}," +
            $"\"shieldEndTime\":{deadlineJson},\"updatedAt\":{updatedAt}}}";
        return new MapStoredRecord(
            "monster", 2212, key, 1, key, "Zombie Boss", null,
            55, null, null, 1.0, deadline, updatedAt, data);
    }

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
