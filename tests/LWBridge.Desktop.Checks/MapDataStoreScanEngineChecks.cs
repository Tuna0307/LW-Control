using System.Runtime.CompilerServices;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

internal static class MapDataStoreScanEngineChecks
{
    [ModuleInitializer]
    internal static void Run()
    {
        SuccessfulScanReplacesPublishedRows();
        AllEightSelectionPublishesEveryKind();
        UnresolvedMonsterProtectionCarriesForwardKnownDeadline();
        KnownInactiveMonsterProtectionClearsPriorDeadline();
        FullRunIdentityRejectsStaleMutations();
        LegacyScanRunSchemaMigratesAndPersistsIdentity();
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
        Check(result.Rows[0].GetProperty("serverId").GetInt32() == 2212,
            "indexed search must attach the authoritative indexed server scope even when data_json omits serverId");
        Check(store.ReadScanBlockCheckpointsForTest(request.RunId).Count == 0,
            "completed publication removes block staging after commit");
    }

    private static void AllEightSelectionPublishesEveryKind()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        foreach (string kind in MapScanContract.RecoveredDefaultTypes)
            store.UpsertRecord(GenericRecord(kind, "old-" + kind, 1));

        var request = new MapScanExecutionRequest(
            "store-all-eight", 2212, 7, 20, 20,
            MapScanContract.RecoveredDefaultTypes, 8, 2);
        MapStoredRecord[] fresh = MapScanContract.RecoveredDefaultTypes
            .Select((kind, index) => GenericRecord(kind, "fresh-" + kind, 100 + index))
            .ToArray();
        var source = new MultiCaptureSource(fresh);
        new MapScanEngine(source, new MapDataStoreScanSink(store))
            .ExecuteAsync(request).GetAwaiter().GetResult();

        foreach (string kind in MapScanContract.RecoveredDefaultTypes)
        {
            Check(store.CountRecords(kind, 2212) == 1 &&
                  store.GetRecord(kind, 2212, "fresh-" + kind) is not null &&
                  store.GetRecord(kind, 2212, "old-" + kind) is null,
                $"all-eight publication should atomically replace the selected {kind} scope");
        }
        Check(store.ReadScanBlockCheckpointsForTest(request.RunId).Count == 0,
            "all-eight publication should clean shared block staging after commit");
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

    private static void UnresolvedZombieBossProtectionCarriesForwardKnownDeadline()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        const long knownDeadline = 2_000_000_000_000L;
        store.UpsertRecord(MonsterRecord(
            "boss", 100, known: true, active: true, knownDeadline, kind: "zombie_boss"));

        var request = MonsterRequest("store-zombie-boss-carry", "zombie_boss");
        var source = new SingleCaptureSource(
            MonsterRecord("boss", 200, known: false, active: false, deadline: null, kind: "zombie_boss"));
        new MapScanEngine(source, new MapDataStoreScanSink(store))
            .ExecuteAsync(request).GetAwaiter().GetResult();

        MapStoredRecord row = store.GetRecord("zombie_boss", 2212, "boss")
            ?? throw new InvalidOperationException("carried Zombie Boss row was not published");
        Check(row.ShieldEndTime == knownDeadline &&
              row.DataJson.Contains("\"monsterProtectionKnown\":true", StringComparison.Ordinal) &&
              row.DataJson.Contains("\"monsterProtectionActive\":true", StringComparison.Ordinal),
            "unresolved dedicated Zombie Boss publication must preserve a still-live prior authoritative deadline");
        Check(store.CountRecords("monster", 2212) == 0,
            "Zombie Boss publication must stay isolated from the generic Monster dataset");
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

    private static void FullRunIdentityRejectsStaleMutations()
    {
        using MapDataStore store = MapDataStore.CreateInMemory();
        var request = new MapScanExecutionRequest(
            "identity-run", 2212, 7, 20, 20, ["city"], 8, 2,
            PlayerTileX: 3, PlayerTileY: 4,
            ScanMode: "normal", LaunchSessionId: "launch-a");
        var sink = new MapDataStoreScanSink(store);
        sink.Begin(request, 1, 100);
        MapScanTargetBlock block = MapScanTraversal.Build(20, 20)[0];
        var capture = new MapScanBlockCapture(
            request.ServerId, request.WorldId, block.BlockIndex, "{}", [Record("identity-row", 2)]);

        MapScanExecutionRequest[] stale =
        [
            request with { ServerId = 2213 },
            request with { WorldId = 8 },
            request with { TileWidth = 21 },
            request with { TileHeight = 21 },
            request with { SelectedTypes = ["resource"] },
            request with { RequestedConcurrency = 20 },
            request with { MaxAttemptsPerBlock = 3 },
            request with { PlayerTileX = 4 },
            request with { PlayerTileY = 5 },
            request with { ScanMode = "fast" },
            request with { LaunchSessionId = "launch-b" },
        ];
        foreach (MapScanExecutionRequest foreign in stale)
            ExpectIdentityMismatch(() => sink.CheckpointSuccess(foreign, block, capture, 1, 101));
        ExpectIdentityMismatch(() =>
            sink.CheckpointFailure(request with { WorldId = 8 }, block, 1, "foreign", 101));
        ExpectIdentityMismatch(() =>
            sink.CheckpointSuccessBatch(
                request with { LaunchSessionId = "launch-b" },
                [new MapScanBlockSuccess(block, capture)],
                1,
                101));

        Check(store.ReadScanBlockCheckpointsForTest(request.RunId).Count == 0,
            "stale or foreign run identity must be rejected before checkpoint mutation");

        sink.CheckpointSuccess(request, block, capture, 1, 102);
        Check(store.ReadScanBlockCheckpointsForTest(request.RunId).Count == 1,
            "the exact persisted run identity should admit its owned block checkpoint");

        ExpectIdentityMismatch(() => sink.Publish(request with { ScanMode = "fast" }, 103));
        Check(store.GetRecord("city", 2212, "identity-row") is null,
            "foreign final publication must not expose staged rows");
        ExpectIdentityMismatch(() => sink.Stop(request with { LaunchSessionId = "launch-b" }, 104));
        Check(store.ReadScanBlockCheckpointsForTest(request.RunId).Count == 1,
            "foreign terminal ownership must not alter the active run");
        sink.Stop(request, 105);
    }

    private static void LegacyScanRunSchemaMigratesAndPersistsIdentity()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-map-run-identity-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var legacy = new SqliteConnection("Data Source=" + path))
            {
                legacy.Open();
                using SqliteCommand create = legacy.CreateCommand();
                create.CommandText = """
                    CREATE TABLE scan_runs (
                      id TEXT PRIMARY KEY, server_id INTEGER NOT NULL, selected_types TEXT NOT NULL,
                      status TEXT NOT NULL, total_blocks INTEGER NOT NULL,
                      completed_blocks INTEGER NOT NULL DEFAULT 0,
                      failed_blocks INTEGER NOT NULL DEFAULT 0,
                      created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL, error TEXT
                    );
                    """;
                create.ExecuteNonQuery();
            }

            var request = new MapScanExecutionRequest(
                "migrated-run", 2212, 9, 20, 20, ["city"], 8, 2,
                PlayerTileX: 6, PlayerTileY: 7,
                ScanMode: "normal", LaunchSessionId: "launch-migrated");
            using (var store = new MapDataStore(path))
            {
                new MapDataStoreScanSink(store).Begin(request, 1, 100);
            }

            using (var reopened = new MapDataStore(path))
            {
                var sink = new MapDataStoreScanSink(reopened);
                MapScanTargetBlock block = MapScanTraversal.Build(20, 20)[0];
                sink.CheckpointSuccess(
                    request,
                    block,
                    new MapScanBlockCapture(
                        request.ServerId, request.WorldId, block.BlockIndex, "{}",
                        [Record("migrated-row", 2)]),
                    1,
                    101);
                Check(reopened.ReadScanBlockCheckpointsForTest(request.RunId).Count == 1,
                    "legacy scan_runs schema migration must preserve the full run identity across reopen");
                ExpectIdentityMismatch(() =>
                    sink.Stop(request with { LaunchSessionId = "different-launch" }, 102));
                sink.Stop(request, 103);
            }
        }
        finally
        {
            foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
                try { File.Delete(candidate); } catch { }
        }
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

    private static MapScanExecutionRequest MonsterRequest(string runId, string kind = "monster") =>
        new(runId, 2212, 7, 20, 20, [kind], 1, 2);

    private static MapStoredRecord GenericRecord(string kind, string key, long updatedAt) =>
        new(
            kind, 2212, key, 1, key, key, null,
            null, null, null, null, null, updatedAt,
            $"{{\"recordKey\":\"{key}\",\"kind\":\"{kind}\",\"serverId\":2212,\"updatedAt\":{updatedAt}}}");

    private static MapStoredRecord MonsterRecord(
        string key,
        long updatedAt,
        bool known,
        bool active,
        long? deadline,
        string kind = "monster")
    {
        string deadlineJson = deadline is null ? "null" : deadline.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string data =
            $"{{\"recordKey\":\"{key}\",\"kind\":\"{kind}\",\"serverId\":2212," +
            $"\"x\":1,\"y\":1,\"monsterProtectionEligible\":true," +
            $"\"monsterProtectionKnown\":{known.ToString().ToLowerInvariant()}," +
            $"\"monsterProtectionActive\":{active.ToString().ToLowerInvariant()}," +
            $"\"monsterProtectionEndTime\":{(active && deadline is not null ? deadlineJson : "0")}," +
            $"\"shieldEndTime\":{deadlineJson},\"updatedAt\":{updatedAt}}}";
        return new MapStoredRecord(
            kind, 2212, key, 1, key, "Zombie Boss", null,
            55, null, null, 1.0, deadline, updatedAt, data);
    }

    private static MapStoredRecord Record(string key, long updatedAt) =>
        new(
            "city", 2212, key, 1, key, key, null,
            30, null, null, null, null, updatedAt,
            $"{{\"recordKey\":\"{key}\",\"kind\":\"city\",\"updatedAt\":{updatedAt}}}");

    private static MapDataQueryOptions Query() =>
        new(
            "city", 2212, 1, 50,
            [new MapDataSort("updatedAt", "desc")],
            false, null, null, false, null, null, null, null, null, null, null,
            false, false, false, null, null, []);

    private static void ExpectIdentityMismatch(Action action)
    {
        try
        {
            action();
        }
        catch (BridgeCommandException error) when (
            error.Code == "INVALID_SCAN" &&
            error.Message == "map scan identity does not match the active run")
        {
            return;
        }
        throw new InvalidOperationException("expected stale/foreign scan identity rejection");
    }

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

    private sealed class MultiCaptureSource(IReadOnlyList<MapStoredRecord> records) : IMapScanBlockSource
    {
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request,
            MapScanTargetBlock block,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new MapScanBlockCapture(
                request.ServerId, request.WorldId, block.BlockIndex, "{}", records));
        }
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
