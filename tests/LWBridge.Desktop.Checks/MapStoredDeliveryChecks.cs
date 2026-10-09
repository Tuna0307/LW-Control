using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// Inert production Map317 command handler, real SQLite store and actual exporter.
// Never calls scan_start or any game action.
internal static class MapStoredDeliveryChecks
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP_DATA_003: " + message);
    }

    private static JsonElement Json(object value) =>
        JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static async Task<JsonElement> Search(
        Map317CommandService service, object query, string kind = "city") =>
        Json(await service.InvokeAsync("map_search", Json(new { kind, query }), CancellationToken.None)
            ?? throw new InvalidDataException("Null map_search response"));

    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "lwb317-map-data-003-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string database = Path.Combine(root, "map-data.db");
        try
        {
            using var lifecycle = new OverviewLifecycleService(
                "map-data-003-inert", gameRoot: null, startRecoveryMonitor: false);
            using (var service = new Map317CommandService(database, lifecycle))
            {
                using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = database, Pooling = false }.ToString()))
                {
                    connection.Open();
                    foreach (int? level in new int?[] { null, 0, 1, 3 })
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = """
                            INSERT INTO map_records
                            (kind,server_id,record_key,level,updated_at,data_json)
                            VALUES ('city',317,$key,$level,$updated,$json)
                            """;
                        string key = level?.ToString() ?? "null";
                        command.Parameters.AddWithValue("$key", key);
                        command.Parameters.AddWithValue("$level", (object?)level ?? DBNull.Value);
                        command.Parameters.AddWithValue("$updated", 1000 + (level ?? -1));
                        command.Parameters.AddWithValue("$json",
                            JsonSerializer.Serialize(new { ownerName = "Original boundary " + key, level, x = 101, y = 102,
                                ownerUid = "owner-" + key, serverId = 317, updatedAt = 1000 + (level ?? -1) }));
                        command.ExecuteNonQuery();
                    }
                }
                var baseline = await Search(service, new { serverId = 317, page = 1, pageSize = 2 });
                Require(baseline.GetProperty("total").GetInt32() == 4 &&
                        baseline.GetProperty("rows").GetArrayLength() == 2, "published rows and first page");
                var second = await Search(service, new { serverId = 317, page = 2, pageSize = 2 });
                Require(second.GetProperty("page").GetInt64() == 2 &&
                        second.GetProperty("rows").GetArrayLength() == 2, "second page");
                var negative = await Search(service, new { serverId = 317, maxLevel = -1 });
                Require(negative.GetProperty("total").GetInt32() == 0,
                    "original f64 maxLevel:-1 must exclude level zero");
                var lower = await Search(service, new { serverId = 317, minLevel = 0 });
                Require(lower.GetProperty("total").GetInt32() == 3,
                    "original minLevel:0 excludes NULL but includes level zero");
                var fractional = await Search(service, new { serverId = 317, minLevel = 0.5, maxLevel = 2.5 });
                Require(fractional.GetProperty("total").GetInt32() == 1,
                    "original fractional boundaries compare SQLite numeric values exactly");
                var wide = await Search(service, new { serverId = 317, page = 3000000000L, pageSize = 200 });
                Require(wide.GetProperty("page").GetInt64() == 3000000000L &&
                        wide.GetProperty("total").GetInt32() == 4 &&
                        wide.GetProperty("rows").GetArrayLength() == 0,
                    "original signed i64 page envelope must not be int32-capped");
                var huge = await Search(service, new { serverId = 317, page = long.MaxValue, pageSize = 200 });
                Require(huge.GetProperty("page").GetInt64() == long.MaxValue &&
                        huge.GetProperty("rows").GetArrayLength() == 4,
                    "original unchecked offset wraps negative; SQLite returns the first rows");
                // Around 2^64 / 200: the unchecked product can wrap to either
                // negative or small positive offsets, not only long.MaxValue.
                foreach (var (page, count) in new (long, int)[]
                {
                    (long.MaxValue / 200 + 1, 0), // largest non-overflowing product
                    (long.MaxValue / 200 + 2, 4), // negative wrapped offset
                    (92233720368547759L, 4),      // -16 offset
                    (92233720368547760L, 0),      // 184 offset
                })
                {
                    var boundary = await Search(service, new { serverId = 317, page, pageSize = 200 });
                    Require(boundary.GetProperty("page").GetInt64() == page &&
                            boundary.GetProperty("rows").GetArrayLength() == count,
                        "original signed-i64 offset boundary " + page);
                }
                foreach (var (query, count) in new (object, int)[]
                {
                    (new { serverId = 317, minLevel = 3, maxLevel = 1 }, 2),
                    (new { serverId = 317, minLevel = -1, maxLevel = -1 }, 0),
                    (new { serverId = 317, minLevel = "0.5", maxLevel = "2.5" }, 1),
                    (new { serverId = 317, minLevel = "NaN", maxLevel = "Infinity" }, 4),
                    (new { serverId = 317, minLevel = double.MaxValue }, 0),
                })
                {
                    var bounds = await Search(service, query);
                    Require(bounds.GetProperty("total").GetInt32() == count,
                        "original finite f64 inversion/string/nonfinite/extreme bounds");
                }
                var sort = await Search(service, new { serverId = 317,
                    sorts = new[] { new { sortBy = "level", sortOrder = "desc" } } });
                Require(sort.GetProperty("total").GetInt32() == 4, "original admitted City level sort");
                var unknown = await Search(service, new { serverId = 317, page = "3", pageSize = 900 }, "unknown");
                Require(unknown.GetProperty("total").GetInt32() == 0 &&
                        unknown.GetProperty("page").GetInt64() == 3 &&
                        unknown.GetProperty("pageSize").GetInt32() == 200,
                    "invalid Map kind returns empty and loose page coerce/size clamp");
                string workbook = Path.Combine(root, "city.xlsx");
                var request = service.PrepareCityExport(Json(new
                {
                    headers = new[] { "Server", "X", "Y", "Player", "UID", "UUID",
                        "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At" },
                    sheetName = "Cities", yesLabel = "Yes", noLabel = "No",
                    query = new { serverId = 317, page = 1, pageSize = 50,
                        sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } } }
                }));
                var export = Json(service.WriteCityExport(request, workbook));
                Require(export.GetProperty("rowCount").GetInt32() == 4 &&
                        File.Exists(workbook), "actual City XLSX writer");
                using var archive = ZipFile.OpenRead(workbook);
                Require(archive.GetEntry("xl/worksheets/sheet1.xml") is not null,
                    "XLSX must have actual spreadsheet cells");
                Console.WriteLine("MAP_DATA_003_NATIVE_CONTRACTS_OK levels/loose/i64/page/sort/export");
            }

            // Real newly opened Map command/store instance must see identical published rows.
            using (var reopened = new Map317CommandService(database, lifecycle))
            {
                JsonElement again = await Search(reopened, new { serverId = 317 });
                Require(again.GetProperty("total").GetInt32() == 4, "published records persisted after reopen");
            }
            Console.WriteLine("MAP_DATA_003_REOPEN_OK");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
