using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;
using M = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Real native routing, SQLite and XLSX output, with an inert external provider.
internal static class Map016DataChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP016_DATA: " + message);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);
    private static async Task<JsonElement> Invoke(Map317CommandService service, string command, object payload) =>
        Json(await service.InvokeAsync(command, Json(payload), CancellationToken.None) ?? new { });
    private static Task<JsonElement> Search(Map317CommandService service, string kind, object query) =>
        Invoke(service, "map_search", new { kind, query });

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwbridge-map016-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await CheckSavedAggregateAndClearAsync(root);
            await CheckAllServersClearAsync(root);
            await CheckRemovedRoutesAndUpgradeAsync(root);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
        Console.WriteLine("MAP016_AGGREGATE_CLEAR_RETIREMENT_OK native=false sqlite=true xlsx=true");
    }

    private static void Seed(M.MapStore store)
    {
        foreach (int server in new[] { 2212, 2213 })
        {
            string name = server == 2212 ? "Alpha" : "Beta";
            string alliance = server == 2212 ? "A" : "B";
            void Put(string kind, int level, object data) => store.UpsertRecord(new M.MapRecord(
                kind, server, "duplicate-key", 20010, "duplicate-uuid", name, alliance,
                level, 5, 1_000_000, 20, null, server, Json(data).GetRawText()));
            // JSON carries an obsolete server: SQLite source metadata must win.
            Put("city", server == 2212 ? 20 : 30, new
            {
                serverId = 9999, uuid = "duplicate-uuid", ownerUid = "same-owner", ownerName = name,
                allianceName = alliance, x = 10, y = 20, level = server == 2212 ? 20 : 30,
            });
            Put("resource", server == 2212 ? 7 : 9, new
            {
                serverId = 9999, uuid = "duplicate-uuid", x = 10, y = 20,
                resourceNameKey = server == 2212 ? "wood" : "iron",
                resourceDetailKnown = true, resourceRemainingAmount = 100, resourceFullAmount = 100,
            });
            Put("dispatch", server == 2212 ? 4 : 6, new
            {
                serverId = 9999, uuid = "duplicate-uuid", ownerUid = "same-owner",
                completionTime = 7_000_000_000_000L, x = 10, y = 20,
            });
            Put("ghost", 5, new
            {
                serverId = 9999, uuid = "duplicate-uuid", ownerUid = "same-owner",
                completionTime = 7_000_000_000_000L, x = 10, y = 20,
                rewards = new[] { new { key = "reward:1:100", name = "Gold", count = 5 } },
            });
        }
        store.SetPlayerMark(new(2213, "same-owner", "active", 1, null, "{}"), true);
    }

    private static M.MapProviderAdapter Provider(Func<int> current) => new(
        _ => ValueTask.FromResult(new M.MapProviderContext(true, true, current(), "live", 0, 1000, 1000)),
        _ => ValueTask.FromResult(new M.MapProviderContext(true, true, current(), "live", 0, 1000, 1000)),
        (_, _) => throw new InvalidOperationException("inert test cannot scan"),
        _ => ValueTask.CompletedTask);

    private static async Task CheckSavedAggregateAndClearAsync(string root)
    {
        string db = Path.Combine(root, "aggregate.db");
        using (var store = new M.MapStore(db)) Seed(store);
        // Legacy malformed row is not a saved server and must not enter All servers.
        using (var connection = Open(db))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO map_records(kind,server_id,record_key,updated_at,data_json) VALUES('city',0,'unscoped',1,'{}')";
            command.ExecuteNonQuery();
        }
        int current = 2212;
        var calls = new List<string>();
        var actions = new InertActions(() => current, server => { current = server; calls.Add("server:" + server); },
            (server, x, y) => { Require(current == server, "Jump used coordinates before row server admission"); calls.Add($"tile:{server}:{x}:{y}"); });
        using var service = new Map317CommandService(db, Provider(() => current), actions, startPlunderWorkers: false);
        JsonElement unscoped = await Search(service, "city", new { });
        Require(unscoped.GetProperty("total").GetInt32() == 0,
            "missing server on an idle/unobserved owner broadened into All servers");
        _ = await Invoke(service, "map_scan_status", new { });
        JsonElement all = await Search(service, "city", new { serverId = 0,
            sorts = new[] { new { sortBy = "serverId", sortOrder = "desc" } } });
        var rows = all.GetProperty("rows").EnumerateArray().ToArray();
        Require(all.GetProperty("total").GetInt32() == 2 && rows.Length == 2,
            "All servers lost same-key rows or included unscoped records");
        Require(rows.Select(row => row.GetProperty("serverId").GetInt32()).SequenceEqual(new[] { 2213, 2212 }),
            "Server sort did not use actual SQLite metadata");
        Require(rows[0].GetProperty("marked").GetBoolean() && !rows[1].GetProperty("marked").GetBoolean(),
            "same owner UID leaked a player mark across servers");
        JsonElement marked = await Search(service, "city", new { serverId = 0, markedOnly = true });
        Require(marked.GetProperty("total").GetInt32() == 1 && marked.GetProperty("rows")[0].GetProperty("serverId").GetInt32() == 2213,
            "aggregate marked filter ignored server-scoped ownership");
        JsonElement filtered = await Search(service, "city", new { serverId = 0, keyword = "Beta", minLevel = 25, alliance = "B", pageSize = 1 });
        Require(filtered.GetProperty("total").GetInt32() == 1 && filtered.GetProperty("rows")[0].GetProperty("serverId").GetInt32() == 2213,
            "aggregate filters were applied after page count or on another server");
        JsonElement second = await Search(service, "city", new { serverId = 0, page = 2, pageSize = 1,
            sorts = new[] { new { sortBy = "serverId", sortOrder = "desc" } } });
        Require(second.GetProperty("rows")[0].GetProperty("serverId").GetInt32() == 2212,
            "aggregate pagination repeated a same-key row from another server");
        JsonElement malformed = await Search(service, "city", new { serverId = "broken" });
        Require(malformed.GetProperty("total").GetInt32() == 1,
            "malformed server input opted into All servers instead of the observed live server");

        JsonElement tasks = await Search(service, "dispatch", new { serverId = 0,
            sorts = new[] { new { sortBy = "serverId", sortOrder = "asc" } } });
        Require(tasks.GetProperty("total").GetInt32() == 2 &&
            tasks.GetProperty("rows")[0].GetProperty("ownerName").GetString() == "Alpha" &&
            tasks.GetProperty("rows")[1].GetProperty("ownerName").GetString() == "Beta",
            "aggregate Dispatch owner resolver crossed server identity");
        JsonElement options = await Invoke(service, "map_data_options", new { serverId = 0 });
        Require(options.GetProperty("counts").GetProperty("city").GetInt32() == 2 &&
            options.GetProperty("counts").GetProperty("resource").GetInt32() == 2 &&
            options.GetProperty("alliances").GetArrayLength() == 2 &&
            options.GetProperty("names").GetProperty("resource").GetArrayLength() == 2 &&
            options.GetProperty("dispatchLevels").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(new[] { 4, 6 }),
            "aggregate filter options/counts did not include exactly the saved positive servers");
        Require(options.GetProperty("rewardItems").GetProperty("ghost")[0].GetProperty("taskCount").GetInt32() == 2,
            "aggregate Ghost reward task count collapsed same-key tasks from different servers");
        foreach (long overflow in new[] { 2_147_483_648L, long.MaxValue })
        {
            try
            {
                await Invoke(service, "map_data_options", new { serverId = overflow });
                throw new InvalidOperationException("overflow server unexpectedly loaded options");
            }
            catch (BridgeCommandException error)
            {
                Require(error.Code == "INVALID_SERVER_ID", "overflow server did not fail before aggregate options");
            }
        }
        JsonElement remote = rows[0];
        await Invoke(service, "map_coordinate_jump", new { serverId = remote.GetProperty("serverId").GetInt32(),
            x = remote.GetProperty("x").GetInt32(), y = remote.GetProperty("y").GetInt32() });
        Require(calls.SequenceEqual(new[] { "server:2213", "tile:2213:10:20" }),
            "aggregate row Jump lost source server at identical coordinates");

        using (var store = new M.MapStore(db))
        {
            string path = Path.Combine(root, "all-cities.xlsx");
            var result = new M.MapExporter(store).ExportCitiesToPath(new("city", 0),
                new(Enumerable.Range(1, 12).Select(n => "Column " + n).ToArray(), "Cities", "Yes", "No"), path);
            using var archive = ZipFile.OpenRead(path);
            using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            var xml = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string[] servers = xml.Descendants(ns + "row").Skip(1)
                .Select(row => row.Elements(ns + "c").First().Element(ns + "v")!.Value).Order().ToArray();
            Require(result.RowCount == 2 && servers.SequenceEqual(new[] { "2212", "2213" }),
                "aggregate XLSX did not preserve each row source server");
        }

        await Invoke(service, "map_scan_clear", new { serverId = 2213, clearSavedServer = true });
        JsonElement saved = await Invoke(service, "map_saved_servers", new { });
        Require(saved.GetProperty("servers").EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(new[] { 2212 }),
            "Clear left an empty saved server in native discovery");
        JsonElement after = await Search(service, "city", new { serverId = 0 });
        Require(after.GetProperty("total").GetInt32() == 1 && after.GetProperty("rows")[0].GetProperty("serverId").GetInt32() == 2212,
            "Clear removed another server's same-key rows");
        Require((await Search(service, "resource", new { serverId = 2212 })).GetProperty("total").GetInt32() == 1,
            "Clear deleted another kind on another server");
    }

    private static async Task CheckAllServersClearAsync(string root)
    {
        string db = Path.Combine(root, "clear-all.db");
        string otherDb = Path.Combine(root, "clear-all-other-profile.db");
        using (var store = new M.MapStore(db))
        {
            Seed(store);
            store.WriteSetting("mapAutoScan", "{\"enabled\":false}", 1);
            store.UpsertTreasureClaimState(2212, "player", "treasure", null, 1, "{}", 1);
        }
        using var otherProfile = new M.MapStore(otherDb);
        Seed(otherProfile);
        bool connected = true;
        bool autoBusy = false;
        var provider = new CurrentClientMap317ScanProvider(new HeldMapSource(),
            _ => Task.FromResult(new CurrentClientMapStatusContext(true, connected ? 2212 : 0, 2212, [], [], 0, 20, 20, 1, 1)),
            _ => Task.FromResult(new CurrentClientMapContext(2212, 0, 20, 20, 1, 1, "clear-all-controlled")));
        using var service = new Map317CommandService(db, provider,
            M.UnavailableMapActionProvider.Instance, startPlunderWorkers: false);
        service.BindAutoScanCycleBusy(() => autoBusy);
        async Task Reject(object payload, string expectedCode)
        {
            try
            {
                await Invoke(service, "map_scan_clear", payload);
                throw new InvalidOperationException("Clear unexpectedly succeeded: " + expectedCode);
            }
            catch (BridgeCommandException error)
            {
                Require(error.Code == expectedCode, "All-server Clear rejected with " + error.Code);
            }
            Require((await Search(service, "city", new { serverId = 0 })).GetProperty("total").GetInt32() == 2,
                "rejected all-server Clear removed saved rows");
        }
        object all = new { serverId = 0, allServers = true };
        await Reject(new { serverId = 0 }, "SERVER_UNAVAILABLE");
        await Reject(new { serverId = 2212, allServers = true }, "INVALID_REQUEST");
        autoBusy = true;
        await Reject(all, "AUTO_SCAN_RUNNING");
        autoBusy = false;
        connected = false;
        await Reject(all, "SERVER_UNAVAILABLE");
        connected = true;
        JsonElement started = await Invoke(service, "map_scan_start", new { selectedTypes = new[] { "city" } });
        Require(started.GetProperty("isReading").GetBoolean(), "manual scan guard was not exercised");
        await Reject(all, "SCAN_RUNNING");
        await Invoke(service, "map_scan_stop", new { });
        string runId = started.GetProperty("scanRunId").GetString()!;
        // A failed delete must roll back earlier table/server deletions too.
        using (var connection = Open(db))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER reject_all_clear BEFORE DELETE ON map_records
                WHEN OLD.server_id=2213 BEGIN SELECT RAISE(ABORT, 'controlled clear failure'); END;
                """;
            command.ExecuteNonQuery();
            try
            {
                await Invoke(service, "map_scan_clear", all);
                throw new InvalidOperationException("all-server Clear ignored a database failure");
            }
            catch (SqliteException) { }
            using (var store = new M.MapStore(db))
                Require(store.Search(new("city", 0)).Total == 2 && store.ReadScanRun(runId) is not null,
                    "failed all-server transaction partially removed map data/history");
            command.CommandText = "DROP TRIGGER reject_all_clear";
            command.ExecuteNonQuery();
        }
        JsonElement cleared = await Invoke(service, "map_scan_clear", all);
        Require(!cleared.GetProperty("isReading").GetBoolean() &&
            cleared.GetProperty("scanRunId").GetString() == "" &&
            !cleared.GetProperty("resumeAvailable").GetBoolean(),
            "all-server Clear retained deleted scan/resume state");
        Require((await Invoke(service, "map_saved_servers", new { })).GetProperty("servers").GetArrayLength() == 0,
            "all-server Clear retained saved server choices");
        using (var store = new M.MapStore(db))
        {
            Require(M.MapKinds.All.All(kind => store.Search(new(kind, 0)).Total == 0) &&
                store.ReadScanRun(runId) is null, "all-server Clear left map records or scan history");
            Require(store.ReadSetting("mapAutoScan") == "{\"enabled\":false}" &&
                store.ReadPlayerMark(2213, "same-owner") is not null,
                "all-server Clear removed settings or player marks");
        }
        using (var connection = Open(db))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM treasure_claim_states";
            Require(Convert.ToInt32(command.ExecuteScalar()) == 0, "all-server Clear retained stale treasure states");
        }
        Require(otherProfile.Search(new("city", 0)).Total == 2,
            "all-server Clear crossed into another profile database");
        // Repeating the command on an empty store is harmless.
        await Invoke(service, "map_scan_clear", all);
        Console.WriteLine("MAP_ALL_SERVERS_CLEAR_OK sqlite=true guards=true profileIsolation=true rollback=true");
    }

    private static async Task CheckRemovedRoutesAndUpgradeAsync(string root)
    {
        string db = Path.Combine(root, "legacy.db");
        using (var store = new M.MapStore(db))
        {
            Seed(store);
            store.WriteSetting("mapAutoScan", "{\"enabled\":true}", 1);
            store.WriteSetting("randomDelaySeconds", "20", 1);
            store.WriteSetting("autoPlunderEnabled", "true", 1);
        }
        using (var connection = Open(db))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO dispatch_plunder_jobs(server_id,task_uuid,task_json,completion_time,plunder_at,status,created_at,updated_at)
                  VALUES(2212,'legacy-dispatch','{}',1,1,'scheduled',1,1);
                INSERT INTO truck_plunder_jobs(server_id,train_uuid,truck_json,execute_at,status,created_at,updated_at)
                  VALUES(2212,'legacy-truck','{}',1,'scheduled',1,1);
                INSERT INTO truck_plunder_history(job_id,server_id,train_uuid,truck_json,execute_at,status,created_at,updated_at)
                  VALUES('legacy-history',2212,'legacy-truck','{}',1,'completed',1,1);
                INSERT INTO dispatch_assist_jobs(task_uuid,target_server,task_json,assist_at,status,created_at,updated_at)
                  VALUES('legacy-share',2212,'{}',1,'scheduled',1,1);
                """;
            command.ExecuteNonQuery();
        }
        var actions = new InertActions(() => 2212, _ => { }, (_, _, _) => { });
        using (var service = new Map317CommandService(db, Provider(() => 2212), actions, startPlunderWorkers: true))
        {
            foreach (string command in new[] {
                "map_ghost_plunder_prepare", "map_dispatch_share_alliance", "map_plunder_jobs_list",
                "map_dispatch_plunder_schedule", "map_dispatch_plunder_cancel", "map_dispatch_plunder_clear",
                "map_dispatch_plunder_list", "map_dispatch_plunder_retry", "map_truck_plunder_schedule",
                "map_truck_plunder_cancel", "map_truck_plunder_clear", "map_truck_plunder_list", "map_truck_plunder_retry" })
            {
                try
                {
                    await Invoke(service, command, new { rows = new[] { new { serverId = 2212, uuid = "legacy" } } });
                    throw new InvalidOperationException(command + " succeeded after retirement");
                }
                catch (BridgeCommandException error)
                {
                    Require(error.Code == "FEATURE_REMOVED", command + " did not explain owner removal");
                }
            }
            await Task.Delay(3 * M.MapPlunderWorker.TickMilliseconds);
            Require(actions.ForbiddenCalls == 0, "removed route or legacy worker reached an account-action provider");
        }
        using var reopened = new M.MapStore(db);
        Require(reopened.ReadSetting("mapAutoScan") == "{\"enabled\":true}" &&
            reopened.ReadSetting("randomDelaySeconds") is null && reopened.ReadSetting("autoPlunderEnabled") is null,
            "upgrade removed unrelated settings or retained removed action settings");
        Require(reopened.Search(new("city", 0)).Total == 2 && reopened.Search(new("city", 0, MarkedOnly: true)).Total == 1,
            "legacy action retirement damaged saved rows or marks");
        using var check = Open(db);
        foreach (string table in new[] { "dispatch_plunder_jobs", "truck_plunder_jobs", "truck_plunder_history", "dispatch_assist_jobs" })
        {
            using var count = check.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM " + table;
            Require(Convert.ToInt32(count.ExecuteScalar()) == 0, "legacy " + table + " survived owner retirement");
        }
    }

    private static SqliteConnection Open(string db)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private sealed class HeldMapSource : IMapScanBlockSource
    {
        public async Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Controlled scan must stop before capture completes");
        }
    }

    private sealed class InertActions(Func<int> current, Action<int> server, Action<int, int, int> coordinate) : M.IMapActionProvider
    {
        internal int ForbiddenCalls;
        private Exception Forbidden() { Interlocked.Increment(ref ForbiddenCalls); return new InvalidOperationException("forbidden account action"); }
        public ValueTask<int> GetCurrentServerIdAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(current());
        public ValueTask GotoServerAsync(int value, CancellationToken cancellationToken = default) { server(value); return ValueTask.CompletedTask; }
        public ValueTask GotoWorldCoordinateAsync(int value, int x, int y, CancellationToken cancellationToken = default) { coordinate(value, x, y); return ValueTask.CompletedTask; }
        public ValueTask GotoWorldMarchAsync(int value, string uuid, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<M.TreasureInspectionResult> InspectTreasureStatesAsync(IReadOnlyList<JsonElement> rows, bool refresh, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<JsonElement> GetTreasureClaimStatusAsync(CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<JsonElement> ClaimTreasuresAsync(M.TreasureClaimProviderRequest request, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<M.DispatchShareProviderResult> ShareDispatchTaskToAllianceAsync(JsonElement row, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<IReadOnlyList<JsonElement>> PrepareGhostPlunderTasksAsync(IReadOnlyList<JsonElement> rows, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<M.MapPlunderServerDayProviderResult?> GetMapPlunderServerDayStartAsync(CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<IReadOnlyList<M.DispatchPlunderArmResult>> ArmDispatchPlunderAsync(IReadOnlyList<M.DispatchPlunderArmJob> jobs, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<M.TruckPlunderArmResult> ArmTruckPlunderAsync(M.TruckPlunderArmJob job, CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<IReadOnlyList<M.DispatchPlunderResultEvent>> DrainDispatchPlunderResultsAsync(CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask<IReadOnlyList<M.TruckPlunderResultEvent>> DrainTruckPlunderResultsAsync(CancellationToken cancellationToken = default) => throw Forbidden();
        public ValueTask ClearTruckPlunderPendingAsync(int value, string uuid, string job, CancellationToken cancellationToken = default) => throw Forbidden();
    }
}
