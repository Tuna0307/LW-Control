using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LWBridge.Map317.Checks;

internal static class StoreChecks
{
    internal static void Run()
    {
        using MapStore store = MapStore.CreateInMemory();
        IReadOnlyDictionary<string, string> schema = store.ReadSchemaDefinitions();
        foreach (string table in new[]
                 {
                     "metadata", "map_records", "scan_runs", "scan_blocks", "scan_records",
                     "player_marks", "app_settings", "treasure_claim_states", "dispatch_plunder_jobs",
                     "truck_plunder_jobs", "truck_plunder_history", "dispatch_assist_jobs",
                 })
            TestAssert.True(schema.ContainsKey(table), $"missing exact base table {table}");
        TestAssert.True(!schema["scan_runs"].Contains("world_id", StringComparison.OrdinalIgnoreCase),
            "0.3.17 base scan_runs must not inherit legacy current-client columns");

        MapRecord old = Record("city", 10, "city-1", 1000, "u1", "Alice", "AAA", 20, 100, 1000,
            "{\"ownerUid\":\"u1\",\"ownerName\":\"Alice\",\"x\":1,\"y\":2,\"level\":20,\"health\":100}");
        store.UpsertRecord(old);
        store.SetPlayerMark(new MapPlayerMark(10, "u1", "marked", 1001, null, old.DataJson), true);
        TestAssert.True(store.ReadPlayerMark(10, "u1") is not null, "mark upsert failed");

        MapScanRun run = new("run-1", 10, ["city"], "running", 1, 1, 0, 1100, 1101, null);
        store.InsertScanRun(run);
        MapRecord replacement = Record("city", 10, "city-2", 1102, "u2", "Bob", null, 22, 80, 900,
            "{\"ownerUid\":\"u2\",\"ownerName\":\"Bob\",\"x\":3,\"y\":4,\"level\":22,\"health\":80}");
        store.StageRecord(run.Id, replacement);
        store.CompleteScan(run.Id, 1200);
        MapSearchResult after = store.Search(new MapQuery("city", 10));
        TestAssert.Equal(1, after.Total, "completed publication must replace selected kind");
        TestAssert.Equal("u2", after.Rows[0].GetProperty("ownerUid").GetString(), "wrong published row");
        TestAssert.True(store.ReadPlayerMark(10, "u1") is not null, "server scan publication must preserve marks");

        MapScanRun failed = new("run-f", 10, ["city"], "running", 2, 1, 1, 1300, 1301, null);
        store.InsertScanRun(failed);
        store.StageRecord(failed.Id, Record("city", 10, "city-3", 1302, "u3", "Carol", null, 30, 90, 800,
            "{\"ownerUid\":\"u3\",\"ownerName\":\"Carol\",\"x\":5,\"y\":6}"));
        store.FailScan(failed.Id, "map scan failed", 1400);
        MapSearchResult preserved = store.Search(new MapQuery("city", 10));
        TestAssert.Equal(2, preserved.Total, "failed scan must preserve captured rows without replacing old published rows");

        store.InsertScanRun(new MapScanRun("run-c", 10, ["city"], "running", 5, 1, 0, 1500, 1500, null));
        store.StageRecord("run-c", Record("city", 10, "stage", 1501, "stage", "Stage", null, 1, 1, 1,
            "{\"ownerUid\":\"stage\"}"));
        store.CancelScan("run-c", null, 1502);
        TestAssert.Equal(0, store.Search(new MapQuery("city", 10, ScanRunId: "run-c")).Total,
            "cancel must clear staging");

        MapScanRun interrupted = new("run-restart", 11, ["city"], "running", 5, 1, 0, 1510, 1510, null);
        store.InsertScanRun(interrupted);
        store.StageRecord(interrupted.Id, Record("city", 11, "restart-stage", 1511, "restart-stage", "Restart Stage", null, 1, 1, 1,
            "{\"ownerUid\":\"restart-stage\"}"));
        TestAssert.Equal(1, store.ReconcileInterruptedScans("map scan interrupted by application restart", 1512),
            "restart reconciliation must terminalize every orphaned running row");
        MapScanRun reconciled = store.ReadScanRun(interrupted.Id)!;
        TestAssert.True(reconciled.Status == "failed" && reconciled.Error == "map scan interrupted by application restart",
            "restart reconciliation must retain the recovered interruption reason");
        TestAssert.Equal(0, store.Search(new MapQuery("city", 11)).Total,
            "restart reconciliation must not publish stale staging rows");
        TestAssert.Throws<BridgeCommandException>(() => store.CompleteScan(interrupted.Id, 1513), "INVALID_SCAN");

        store.SetServerJumpHistory([5, 5, 0, 100000, 7, 8, 9, 10, 11], 1600);
        TestAssert.True(store.GetServerJumpHistory().SequenceEqual([5, 7, 8, 9, 10]), "history normalization mismatch");
        TestAssert.True(store.ImportServerJumpHistory([99], 1601).SequenceEqual([5, 7, 8, 9, 10]),
            "existing server history must win import");

        (int deletedRuns, int deletedRecords) = store.ClearServer(10);
        TestAssert.True(deletedRuns >= 1 && deletedRecords >= 1, "clear server counts missing");
        TestAssert.True(store.ReadPlayerMark(10, "u1") is not null, "clear must preserve player marks");

        SchemaVersionAndSettingErrors();
    }

    private static void SchemaVersionAndSettingErrors()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge317-map-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string database = MapStore.DatabasePathForRuntimeRoot(root, "profile-a");
        try
        {
            using (MapStore physical = new(database))
            {
                TestAssert.True(database.EndsWith(Path.Combine("profiles", "profile-a", "map-data", "map-data.db"), StringComparison.OrdinalIgnoreCase),
                    "per-profile database path mismatch");
                physical.UpsertRecord(Record("city", 12, "persisted", 2000, "persisted", "Persisted", null, 12, 12, 12,
                    "{\"ownerUid\":\"persisted\"}"));
                physical.InsertScanRun(new MapScanRun("physical-orphan", 12, ["city"], "running", 2, 1, 0, 2001, 2001, null));
                physical.StageRecord("physical-orphan", Record("city", 12, "staged-orphan", 2002, "staged-orphan", "Staged", null, 1, 1, 1,
                    "{\"ownerUid\":\"staged-orphan\"}"));
            }

            using (MapStore reopened = new(database))
            {
                TestAssert.Equal(1, reopened.Search(new MapQuery("city", 12)).Total,
                    "published Map rows must survive a physical database reopen");
                TestAssert.Equal(1, reopened.ReconcileInterruptedScans("map scan interrupted by application restart", 2003),
                    "physical reopen must reconcile the orphaned running row");
                TestAssert.Equal(1, reopened.Search(new MapQuery("city", 12)).Total,
                    "restart reconciliation must not publish stale physical staging rows");
                TestAssert.True(reopened.ReadScanRun("physical-orphan") is { Status: "failed" },
                    "physical orphan must be terminal after reconciliation");
                reopened.WriteSetting("serverJumpHistory", "not-json", 1);
                TestAssert.Throws<BridgeCommandException>(() => reopened.GetServerJumpHistory(), "INVALID_SETTING");
            }

            using (var connection = new SqliteConnection($"Data Source={database}"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "UPDATE metadata SET value='5' WHERE key='schema_version'";
                command.ExecuteNonQuery();
            }

            BridgeCommandException tooNew = TestAssert.Throws<BridgeCommandException>(() =>
            {
                using MapStore _ = new(database);
            }, "MAP_SCHEMA_TOO_NEW");
            TestAssert.True(tooNew.Message.Contains("schema 5", StringComparison.Ordinal) &&
                            tooNew.Message.Contains("supported 4", StringComparison.Ordinal),
                "too-new schema message mismatch");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    internal static MapRecord Record(string kind, int server, string key, long updated, string? uuid, string? name,
        string? alliance, int? level, int? quality, long? power, string json) =>
        new(kind, server, key, null, uuid, name, alliance, level, quality, power, null, null, updated, json);
}
