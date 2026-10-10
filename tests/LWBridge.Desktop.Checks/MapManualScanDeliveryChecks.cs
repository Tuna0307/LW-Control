using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// Real production command -> Map317 control -> current-client adapter -> engine
// -> SQLite. Only the external game capture/context is controlled.
internal static class MapManualScanDeliveryChecks
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("MAP_005: " + message);
    }

    private static JsonElement Json(object value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static JsonElement Response(object? value) =>
        JsonSerializer.SerializeToElement(value ?? throw new InvalidOperationException("Empty native response"), JsonOptions.Default);

    private static async Task<JsonElement> Invoke(
        Map317CommandService service, string command, object payload) =>
        Response(await service.InvokeAsync(command, Json(payload), CancellationToken.None));

    private static async Task<JsonElement> Search(
        Map317CommandService service, string kind) =>
        await Invoke(service, "map_search", new { kind, query = new { serverId = 317, page = 1 } });

    private static MapStoredRecord Record(string kind, string key, int block = 0)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new MapStoredRecord(kind, 317, key, block, key, key, null, 17, null,
            null, null, null, now, JsonSerializer.Serialize(new
            {
                serverId = 317, kind, uuid = key, ownerUid = key,
                ownerName = key, name = key, level = 17, x = block + 1, y = 1, updatedAt = now,
            }));
    }

    private static MapScanBlockCapture Capture(
        MapScanExecutionRequest request, MapScanTargetBlock block, string kind, string key) =>
        new(request.ServerId, request.WorldId, block.BlockIndex, "{}", [Record(kind, key, block.BlockIndex)]);

    private sealed class ControlledSource : IMapScanBlockSource
    {
        public Func<MapScanExecutionRequest, MapScanTargetBlock, CancellationToken, Task<MapScanBlockCapture>> CaptureBlock { get; set; } =
            (request, block, _) => Task.FromResult(Capture(request, block, "city", "initial"));

        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken cancellationToken) =>
            CaptureBlock(request, block, cancellationToken);
    }

    private sealed class ProgressSource : IMapScanProgressBatchSource
    {
        internal readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<MapScanBlockCapture> CaptureAsync(MapScanExecutionRequest request, MapScanTargetBlock block,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Batch route required");

        public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request, MapScanTargetBlock seedBlock, IReadOnlySet<int> pending,
            CancellationToken cancellationToken) =>
            CaptureBatchAsync(request, seedBlock, pending, null, cancellationToken);

        public async Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
            MapScanExecutionRequest request, MapScanTargetBlock seedBlock, IReadOnlySet<int> pending,
            Action<MapScanSourceProgress>? progress, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => Cancelled.TrySetResult(true));
            progress?.Invoke(new MapScanSourceProgress(45));
            Entered.TrySetResult(true);
            await Release.Task.ConfigureAwait(false); // Deliberately ignores cancellation: late game reply.
            return [Capture(request, seedBlock, "city", "late-response")];
        }
    }

    private static CurrentClientMapStatusContext Status(int width = 40) =>
        new(true, 317, 317, [], [], 1, width, 20, 1, 1);

    private static CurrentClientMapContext Context(int width = 40) =>
        new(317, 1, width, 20, 1, 1, "map-005-inert-session");

    private static Map317CommandService Service(
        string path, IMapScanBlockSource source, int width = 40) =>
        new(path, new CurrentClientMap317ScanProvider(
                source, _ => Task.FromResult(Status(width)), _ => Task.FromResult(Context(width))),
            Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false);

    private static async Task<JsonElement> WaitTerminal(Map317CommandService service)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            JsonElement state = await Invoke(service, "map_scan_status", new { });
            if (!state.GetProperty("isReading").GetBoolean()) return state;
            await Task.Delay(20);
        }
        throw new TimeoutException("MAP_005: production scan did not become terminal");
    }

    private static int SqlCount(string database, string table, string condition = "1=1")
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = database, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {condition}";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Seed(string database, string kind, string key)
    {
        MapStoredRecord row = Record(kind, key);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = database, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO map_records(kind,server_id,record_key,uuid,name,level,updated_at,data_json)
            VALUES ($kind,317,$key,$uuid,$name,$level,$updated,$json)
            """;
        command.Parameters.AddWithValue("$kind", row.Kind);
        command.Parameters.AddWithValue("$key", row.RecordKey);
        command.Parameters.AddWithValue("$uuid", row.Uuid!);
        command.Parameters.AddWithValue("$name", row.Name!);
        command.Parameters.AddWithValue("$level", row.Level!.Value);
        command.Parameters.AddWithValue("$updated", row.UpdatedAt);
        command.Parameters.AddWithValue("$json", row.DataJson);
        command.ExecuteNonQuery();
    }

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwb317-map-005-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string database = Path.Combine(root, "map-data.db");
        try
        {
            var source = new ControlledSource();
            using (var service = Service(database, source))
            {
                Seed(database, "city", "previous-city");
                Seed(database, "resource", "previous-resource");
                var reachedSecond = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var observedCancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var delayedReply = new TaskCompletionSource<MapScanBlockCapture>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                source.CaptureBlock = (request, block, token) =>
                {
                    if (block.BlockIndex == 0)
                        return Task.FromResult(Capture(request, block, "city", "staged-city"));
                    token.Register(() => observedCancellation.TrySetResult(true));
                    reachedSecond.TrySetResult(true);
                    return delayedReply.Task;
                };

                JsonElement start = await Invoke(service, "map_scan_start",
                    new { selectedTypes = new[] { "city" }, scanMode = "normal" });
                Require(start.GetProperty("isReading").GetBoolean() &&
                        start.GetProperty("concurrency").GetInt32() == 8, "normal start");
                await reachedSecond.Task.WaitAsync(TimeSpan.FromSeconds(4));
                Require((await Search(service, "city")).GetProperty("total").GetInt32() == 1,
                    "live search observes genuine engine staging");
                Require(SqlCount(database, "map_records", "kind='city'") == 1 &&
                        SqlCount(database, "scan_records", "kind='city'") == 1,
                    "staged rows must not replace previous publication");
                try
                {
                    await Invoke(service, "map_scan_start",
                        new { selectedTypes = new[] { "resource" }, scanMode = "fast" });
                    throw new InvalidOperationException("MAP_005: duplicate start accepted");
                }
                catch (BridgeCommandException error)
                {
                    Require(error.Code == "SCAN_RUNNING", "duplicate start fenced by owned lease");
                }

                Task<JsonElement> stopping = Invoke(service, "map_scan_stop", new { });
                await observedCancellation.Task.WaitAsync(TimeSpan.FromSeconds(4));
                delayedReply.TrySetResult(Capture(new MapScanExecutionRequest(
                    start.GetProperty("scanRunId").GetString()!, 317, 1, 40, 20, ["city"], 8),
                    MapScanTraversal.Build(40, 20)[1], "city", "late-city"));
                JsonElement stopped = await stopping.WaitAsync(TimeSpan.FromSeconds(4));
                Require(!stopped.GetProperty("isReading").GetBoolean(), "Stop terminalizes real adapter engine");
                Require(SqlCount(database, "scan_records") == 0 &&
                        SqlCount(database, "map_records", "kind='city'") == 1 &&
                        SqlCount(database, "map_records", "kind='resource'") == 1 &&
                        SqlCount(database, "scan_runs", "status='cancelled'") == 1,
                    "Stop discards positive staging and late response, retains both publications");
                Require((await Search(service, "city")).GetProperty("rows")[0]
                        .GetProperty("ownerName").GetString() == "previous-city",
                    "post-Stop native search returns previously published City");

                source.CaptureBlock = async (request, block, _) =>
                {
                    await Task.Yield();
                    return Capture(request, block, "resource", "fresh-resource-" + block.BlockIndex);
                };
                JsonElement fast = await Invoke(service, "map_scan_start",
                    new { selectedTypes = new[] { "resource" }, scanMode = "fast" });
                Require(fast.GetProperty("concurrency").GetInt32() == 20, "fast starts with 20 concurrency");
                JsonElement completed = await WaitTerminal(service);
                Require(completed.GetProperty("phase").GetString() == "completed" &&
                        completed.GetProperty("progressPercent").GetDouble() == 100, "successful completion");
                Require((await Search(service, "resource")).GetProperty("total").GetInt32() == 2 &&
                        (await Search(service, "city")).GetProperty("total").GetInt32() == 1,
                    "Resource publication replaces only Resource, retains City");
                JsonElement summary = await Invoke(service, "map_summary", new { });
                JsonElement options = await Invoke(service, "map_data_options", new { serverId = 317 });
                Require(summary.GetProperty("counts").GetProperty("resource").GetInt32() == 2 &&
                        options.GetProperty("counts").GetProperty("resource").GetInt32() == 2,
                    "summary/options agree on freshly published records");
                Require(SqlCount(database, "scan_records") == 0 &&
                        SqlCount(database, "map_records", "kind='resource'") == 2,
                    "fully published Resource durable and staging empty");

                source.CaptureBlock = (request, block, _) =>
                    block.BlockIndex == 0
                        ? Task.FromResult(Capture(request, block, "resource", "failure-staged"))
                        : Task.FromException<MapScanBlockCapture>(new InvalidDataException("controlled lost batch"));
                await Invoke(service, "map_scan_start",
                    new { selectedTypes = new[] { "resource" }, scanMode = "normal" });
                JsonElement failed = await WaitTerminal(service);
                Require(failed.GetProperty("phase").GetString() == "failed" &&
                        failed.GetProperty("lastError").GetString() is { Length: > 0 },
                    "failure visibly reports an error");
                Require(SqlCount(database, "map_records", "kind='resource'") == 2 &&
                        SqlCount(database, "scan_records") == 0,
                    "failure discards partial staged new data, keeps last complete Resource");

                // A lost owned backend/game session is terminal immediately,
                // even if one earlier block staged successfully. It must not
                // retry the disconnected world to completion or replace data.
                source.CaptureBlock = (request, block, _) =>
                    block.BlockIndex == 0
                        ? Task.FromResult(Capture(request, block, "city", "disconnected-staged"))
                        : Task.FromException<MapScanBlockCapture>(new BridgeCommandException(
                            MapScanStartOwnership.MissingConnectionErrorCode,
                            MapScanStartOwnership.MissingConnectionErrorMessage));
                await Invoke(service, "map_scan_start",
                    new { selectedTypes = new[] { "city" }, scanMode = "normal" });
                JsonElement disconnected = await WaitTerminal(service);
                Require(disconnected.GetProperty("phase").GetString() == "failed" &&
                        disconnected.GetProperty("lastError").GetString() is string reason &&
                        reason.Contains("game connection unavailable", StringComparison.OrdinalIgnoreCase) &&
                        SqlCount(database, "scan_records") == 0 &&
                        SqlCount(database, "map_records", "kind='city'") == 1,
                    "owned backend loss retired partial City and kept published City");
                Console.WriteLine("MAP_005_NATIVE_STOP_STAGE_LATE_FAIL_PUBLISH_OK");
            }

            // A separate adapter with a held native progressive batch tests that
            // acquired percentage is visible without pretending blocks are committed.
            var progressive = new ProgressSource();
            using (var service = Service(database, progressive, width: 20))
            {
                await Invoke(service, "map_scan_start",
                    new { selectedTypes = new[] { "city" }, scanMode = "normal" });
                await progressive.Entered.Task.WaitAsync(TimeSpan.FromSeconds(4));
                JsonElement state = await Invoke(service, "map_scan_status", new { });
                Require(state.GetProperty("completedBlocks").GetInt32() == 0 &&
                        state.GetProperty("acquisitionProgressPercent").GetDouble() == 45 &&
                        state.GetProperty("progressPercent").GetDouble() == 0,
                    "native acquisition visible separately from staged blocks");
                Task<JsonElement> stop = Invoke(service, "map_scan_stop", new { });
                await progressive.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(4));
                progressive.Release.TrySetResult(true);
                Require(!(await stop.WaitAsync(TimeSpan.FromSeconds(4)))
                        .GetProperty("isReading").GetBoolean(), "held progressive batch Stop");
                Require(SqlCount(database, "map_records", "kind='city'") == 1,
                    "late progressive reply cannot publish City");
                Console.WriteLine("MAP_005_NATIVE_ACQUISITION_PROGRESS_OK");
            }

            using (var reopened = new Map317CommandService(
                database, Map317.UnavailableMapProvider.Instance,
                Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false))
            {
                Require((await Search(reopened, "city")).GetProperty("total").GetInt32() == 1 &&
                        (await Search(reopened, "resource")).GetProperty("total").GetInt32() == 2,
                    "fresh production handler reopens last successful SQLite data");
            }
            Console.WriteLine("MAP_005_NATIVE_REOPEN_OK");

            // Deterministic publication race through the actual production
            // SQLite sink: cancellation during the terminal progress callback
            // must prevent a fully staged new run from entering Publish.
            using (var store = new MapDataStore(database))
            using (var cancellation = new CancellationTokenSource())
            {
                var raceSource = new ControlledSource
                {
                    CaptureBlock = (request, block, _) =>
                        Task.FromResult(Capture(request, block, "resource", "race-replacement")),
                };
                bool reachedPublishing = false;
                var engine = new MapScanEngine(raceSource, new MapDataStoreScanSink(store),
                    progress =>
                    {
                        if (progress.Phase != "publishing") return;
                        reachedPublishing = true;
                        cancellation.Cancel();
                    });
                try
                {
                    await engine.ExecuteAsync(new MapScanExecutionRequest(
                        Guid.NewGuid().ToString("N"), 317, 1, 20, 20, ["resource"], 8),
                        cancellation.Token);
                    throw new InvalidOperationException("MAP_005: cancelled fully staged run published");
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Require(reachedPublishing, "cancellation reached actual publishing boundary");
                }
                Require(SqlCount(database, "scan_runs", "status='discarded'") == 1 &&
                        SqlCount(database, "scan_records") == 0 &&
                        SqlCount(database, "map_records", "kind='resource'") == 2,
                    "terminal publication race stopped and kept previous Resource intact");
            }
            Console.WriteLine("MAP_005_NATIVE_PUBLICATION_RACE_OK");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
