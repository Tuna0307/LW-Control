using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// The actual production command/adapter/engine/control/SQLite pipeline; only
// external game block capture/context are deterministic. This is intentionally
// NOT a substitute for positive official-client acquisition of these kinds.
internal static class Map006AllKindScanChecks
{
    private static readonly string[] Kinds =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    private static void Require(bool passed, string why)
    {
        if (!passed) throw new InvalidOperationException("MAP_006_ALL_KIND: " + why);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private sealed class Source : IMapScanBlockSource
    {
        public string Prefix { get; set; } = "single";
        public Task<MapScanBlockCapture> CaptureAsync(
            MapScanExecutionRequest request, MapScanTargetBlock block, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            MapStoredRecord[] records = request.SelectedTypes.Select(kind =>
            {
                string key = $"{Prefix}-{kind}-{block.BlockIndex}";
                return new MapStoredRecord(kind, 317, key, block.BlockIndex, key, key, null,
                    19, null, null, null, null, now, JsonSerializer.Serialize(new
                    {
                        serverId = 317, kind, uuid = key, name = key, ownerName = key,
                        ownerUid = key, level = 19, x = 1, y = 1, updatedAt = now,
                    }));
            }).ToArray();
            return Task.FromResult(new MapScanBlockCapture(
                request.ServerId, request.WorldId, block.BlockIndex, "{}", records));
        }
    }

    private static async Task<JsonElement> Invoke(Map317CommandService service, string command, object payload) =>
        JsonSerializer.SerializeToElement(await service.InvokeAsync(command, Json(payload), CancellationToken.None)
            ?? throw new InvalidOperationException("Native Map command returned no result"), JsonOptions.Default);

    private static async Task<JsonElement> Terminal(Map317CommandService service)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            JsonElement state = await Invoke(service, "map_scan_status", new { });
            if (!state.GetProperty("isReading").GetBoolean()) return state;
            await Task.Delay(20);
        }
        throw new TimeoutException("Controlled all-kind scan failed to terminalize");
    }

    private static async Task<JsonElement> Search(Map317CommandService service, string kind) =>
        await Invoke(service, "map_search", new
        {
            kind,
            query = new { serverId = 317, page = 1, pageSize = 50 },
        });

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwb317-map-006-allkind-" + Guid.NewGuid().ToString("N"));
        string db = Path.Combine(root, "map-data.db");
        Directory.CreateDirectory(root);
        try
        {
            var source = new Source();
            using (var service = new Map317CommandService(db,
                new CurrentClientMap317ScanProvider(
                    source,
                    _ => Task.FromResult(new CurrentClientMapStatusContext(true, 317, 317, [], [], 1, 20, 20, 1, 1)),
                    _ => Task.FromResult(new CurrentClientMapContext(317, 1, 20, 20, 1, 1, "map-006-controlled"))),
                Map317.UnavailableMapActionProvider.Instance))
            {
                foreach (string kind in Kinds)
                {
                    _ = await Invoke(service, "map_scan_start", new { selectedTypes = new[] { kind }, scanMode = "normal" });
                    JsonElement terminal = await Terminal(service);
                    Require(terminal.GetProperty("phase").GetString() == "completed" &&
                            terminal.GetProperty("completedBlocks").GetInt32() == 1,
                        kind + ": single-kind publication did not complete");
                    JsonElement rows = await Search(service, kind);
                    Require(rows.GetProperty("total").GetInt32() == 1 &&
                            rows.GetProperty("rows")[0].GetProperty("uuid").GetString() == $"single-{kind}-0",
                        kind + ": production search did not return newly published kind");
                }

                source.Prefix = "mixed";
                string[] selected = ["monster", "railway", "treasure"];
                _ = await Invoke(service, "map_scan_start", new { selectedTypes = selected, scanMode = "fast" });
                JsonElement combined = await Terminal(service);
                Require(combined.GetProperty("phase").GetString() == "completed" &&
                        combined.GetProperty("concurrency").GetInt32() == 20,
                    "mixed fast scan did not complete");
                JsonElement options = await Invoke(service, "map_data_options", new { serverId = 317 });
                foreach (string kind in Kinds)
                {
                    JsonElement rows = await Search(service, kind);
                    string prefix = selected.Contains(kind, StringComparer.Ordinal) ? "mixed" : "single";
                    Require(rows.GetProperty("total").GetInt32() == 1 &&
                            rows.GetProperty("rows")[0].GetProperty("uuid").GetString() == $"{prefix}-{kind}-0" &&
                            options.GetProperty("counts").GetProperty(kind).GetInt32() == 1,
                        kind + ": mixed publication damaged selected or previously stored kind");
                }
            }

            using (var reopened = new Map317CommandService(db,
                Map317.UnavailableMapProvider.Instance,
                Map317.UnavailableMapActionProvider.Instance))
            {
                foreach (string kind in Kinds)
                    Require((await Search(reopened, kind)).GetProperty("total").GetInt32() == 1,
                        kind + ": reopened native query lost published record");
            }

            using (var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = db, Pooling = false }.ToString()))
            {
                sqlite.Open();
                using var command = sqlite.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM map_records";
                Require(Convert.ToInt32(command.ExecuteScalar()) == 8,
                    "SQLite did not preserve eight kind publications");
                command.CommandText = "SELECT COUNT(*) FROM scan_records";
                Require(Convert.ToInt32(command.ExecuteScalar()) == 0,
                    "completed mixed scan left stage rows");
            }
            Console.WriteLine("MAP_006_ALL_EIGHT_KINDS_MIXED_PUBLISH_REOPEN_OK");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
