using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// Read-only inspection of an *already completed* genuine acquisition. The
// isolated database is supplied explicitly: no archived dataset is selected.
internal static class MapManualScanPublishedAudit
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MAP_005_AUDIT: " + message);
    }

    private static async Task<JsonElement> Query(
        Map317CommandService service, string kind, int serverId, int page = 1)
    {
        object? value = await service.InvokeAsync("map_search",
            JsonSerializer.SerializeToElement(new
            {
                kind,
                query = new
                {
                    serverId, page, pageSize = 50,
                    sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } }
                }
            }, JsonOptions.Default), CancellationToken.None);
        return JsonSerializer.SerializeToElement(value, JsonOptions.Default);
    }

    private static string HashKeys(JsonElement rows) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", rows.EnumerateArray()
                .Select(row => row.GetRawText()).Order(StringComparer.Ordinal)))));

    internal static async Task RunAsync(
        string database, string outputDirectory, string? complementaryReceipt = null)
    {
        if (!File.Exists(database))
            throw new FileNotFoundException("Fresh scan SQLite database not found", database);
        Directory.CreateDirectory(outputDirectory);

        int serverId;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string runId;
        string status;
        string types;
        var sourceRunIds = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalBlocks;
        int completedBlocks;
        long createdAt;
        long updatedAt;
        using (var sql = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = database, Pooling = false, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        {
            sql.Open();
            using (var command = sql.CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT server_id FROM map_records";
                using var result = command.ExecuteReader();
                Require(result.Read(), "No fresh published Map rows");
                serverId = result.GetInt32(0);
                Require(!result.Read(), "Isolated database has more than one server");
            }
            using (var command = sql.CreateCommand())
            {
                command.CommandText = """
                    SELECT kind, COUNT(*) FROM map_records
                    WHERE server_id=$server GROUP BY kind
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using var result = command.ExecuteReader();
                while (result.Read()) counts.Add(result.GetString(0), result.GetInt32(1));
            }
            Require(counts.GetValueOrDefault("city") > 0 &&
                    counts.GetValueOrDefault("resource") > 0,
                "City and Resource must each contain fresh positive data");
            using (var command = sql.CreateCommand())
            {
                command.CommandText = """
                    SELECT id,status,selected_types,total_blocks,completed_blocks,created_at,updated_at
                    FROM scan_runs WHERE server_id=$server
                    ORDER BY created_at DESC LIMIT 1
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using var result = command.ExecuteReader();
                Require(result.Read(), "Completed live scan run missing");
                runId = result.GetString(0);
                status = result.GetString(1);
                types = result.GetString(2);
                totalBlocks = result.GetInt32(3);
                completedBlocks = result.GetInt32(4);
                createdAt = result.GetInt64(5);
                updatedAt = result.GetInt64(6);
            }
            Require(status == "completed" && totalBlocks > 0 && totalBlocks == completedBlocks,
                "Latest scan must be fully completed, not failed or partial");
            string[] latestKinds = JsonSerializer.Deserialize<string[]>(types) ?? [];
            Require(latestKinds.Length is 1 or 2 &&
                    latestKinds.All(kind => kind is "city" or "resource") &&
                    latestKinds.Distinct(StringComparer.Ordinal).Count() == latestKinds.Length,
                "Latest completed scan must select City, Resource or both");
            foreach (string kind in latestKinds) sourceRunIds.Add(kind, runId);
            if (latestKinds.Length == 1)
            {
                // MapStore.CompleteScan deliberately prunes older run history
                // for the server. Check the separately saved *genuine* City
                // completion receipt against its surviving published rows.
                Require(complementaryReceipt is { Length: > 0 } &&
                        File.Exists(complementaryReceipt),
                    "Missing independently captured complementary live-run receipt");
                using JsonDocument companionDocument = JsonDocument.Parse(
                    File.ReadAllText(complementaryReceipt!));
                JsonElement earlier = companionDocument.RootElement;
                string earlierKind = earlier.GetProperty("kind").GetString() ?? "";
                string earlierRun = earlier.GetProperty("runId").GetString() ?? "";
                int earlierCount = earlier.GetProperty("freshPublishedCount").GetInt32();
                Require(earlier.GetProperty("evidenceType").GetString() ==
                            "genuine-official-current-server-packaged-standalone" &&
                        earlier.GetProperty("serverId").GetInt32() == serverId &&
                        earlier.GetProperty("status").GetString() == "completed" &&
                        earlierKind is "city" or "resource" &&
                        earlierKind != latestKinds[0] &&
                        JsonSerializer.Deserialize<string[]>(
                            earlier.GetProperty("selectedTypesJson").GetString() ?? "[]")
                            ?.SequenceEqual([earlierKind]) == true &&
                        earlier.GetProperty("totalBlocks").GetInt32() > 0 &&
                        earlier.GetProperty("totalBlocks").GetInt32() ==
                            earlier.GetProperty("completedBlocks").GetInt32() &&
                        earlier.GetProperty("failedBlocks").GetInt32() == 0 &&
                        earlier.GetProperty("stagedRecords").GetInt32() == 0 &&
                        earlier.GetProperty("updatedAtMilliseconds").GetInt64() < createdAt &&
                        earlierCount > 0 && earlierCount == counts[earlierKind] &&
                        earlier.GetProperty("uniquePublishedKeys").GetInt32() == earlierCount &&
                        !string.IsNullOrWhiteSpace(earlierRun),
                    "Complementary receipt/retained genuinely published rows disagree");
                sourceRunIds.Add(earlierKind, earlierRun);
            }
            Require(sourceRunIds.ContainsKey("city") && sourceRunIds.ContainsKey("resource"),
                "No completed City and Resource acquisition runs");
            using (var command = sql.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM scan_records";
                Require(Convert.ToInt32(command.ExecuteScalar()) == 0,
                    "Completed live scan left staged records");
            }
            using (var command = sql.CreateCommand())
            {
                command.CommandText = """
                    SELECT COUNT(*) FROM dispatch_plunder_jobs
                    UNION ALL SELECT COUNT(*) FROM truck_plunder_jobs
                    """;
                using var result = command.ExecuteReader();
                while (result.Read())
                    Require(result.GetInt32(0) == 0, "Pilot must not contain actionable plunder jobs");
            }
        }

        using var native = new Map317CommandService(
            database, Map317.UnavailableMapProvider.Instance,
            Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false);
        JsonElement cities = await Query(native, "city", serverId);
        JsonElement resources = await Query(native, "resource", serverId);
        Require(cities.GetProperty("total").GetInt32() == counts["city"] &&
                resources.GetProperty("total").GetInt32() == counts["resource"],
            "Production native query counts differ from freshly reopened SQLite");
        JsonElement cityPage2 = await Query(native, "city", serverId, 2);
        JsonElement resourcePage2 = await Query(native, "resource", serverId, 2);
        Require(cityPage2.GetProperty("rows").GetArrayLength() > 0 &&
                resourcePage2.GetProperty("rows").GetArrayLength() > 0,
            "Both genuine kinds require independently queryable page 2");
        JsonElement options = JsonSerializer.SerializeToElement(
            await native.InvokeAsync("map_data_options",
                JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default),
                CancellationToken.None), JsonOptions.Default);
        Require(options.GetProperty("counts").GetProperty("city").GetInt32() == counts["city"] &&
                options.GetProperty("counts").GetProperty("resource").GetInt32() == counts["resource"],
            "Native options must agree with published SQL rows");
        JsonElement summary = JsonSerializer.SerializeToElement(
            await native.InvokeAsync("map_summary",
                JsonSerializer.SerializeToElement(new { }, JsonOptions.Default),
                CancellationToken.None), JsonOptions.Default);
        // A read-only audit host deliberately has an UnavailableMapProvider:
        // without an authenticated current-server context map_summary returns
        // zero/empty by design. The actual connected packaged Map UI validated
        // this live summary; here, explicit-server native search and options
        // must match the newly acquired persisted rows.
        Require(summary.GetProperty("serverId").GetInt32() == 0 &&
                summary.GetProperty("counts").GetProperty("city").GetInt32() == 0 &&
                summary.GetProperty("counts").GetProperty("resource").GetInt32() == 0,
            "Offline native summary must not pretend to have a live server");

        var firstCity = cities.GetProperty("rows");
        var firstResource = resources.GetProperty("rows");
        string cityFirstHash = HashKeys(firstCity);
        string citySecondHash = HashKeys(cityPage2.GetProperty("rows"));
        string resourceFirstHash = HashKeys(firstResource);
        string resourceSecondHash = HashKeys(resourcePage2.GetProperty("rows"));
        Require(cityFirstHash != citySecondHash && resourceFirstHash != resourceSecondHash,
            "Native page 2 duplicates page 1");

        string workbook = Path.Combine(outputDirectory, "city-native-audit.xlsx");
        var exportRequest = native.PrepareCityExport(
            JsonSerializer.SerializeToElement(new
            {
                headers = new[] { "Server", "X", "Y", "Player", "UID", "UUID",
                    "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At" },
                sheetName = "Cities", yesLabel = "Yes", noLabel = "No",
                query = new { serverId, page = 1, pageSize = 50 }
            }, JsonOptions.Default));
        JsonElement exported = JsonSerializer.SerializeToElement(
            native.WriteCityExport(exportRequest, workbook), JsonOptions.Default);
        Require(exported.GetProperty("rowCount").GetInt32() == counts["city"],
            "Production City Excel writer did not export all freshly acquired records");
        int reopenedWorksheetRows;
        int reopenedHeaderColumns;
        using (var archive = ZipFile.OpenRead(workbook))
        {
            ZipArchiveEntry? sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
            Require(sheet is not null, "Fresh Excel workbook missing worksheet");
            using Stream sheetStream = sheet!.Open();
            XDocument worksheet = XDocument.Load(sheetStream);
            XNamespace excel = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = worksheet.Descendants(excel + "row").ToArray();
            Require(rows.Length > 0, "Excel worksheet missing headers");
            reopenedWorksheetRows = rows.Length - 1;
            reopenedHeaderColumns = rows[0].Elements(excel + "c").Count();
            Require(reopenedWorksheetRows == counts["city"] &&
                    reopenedHeaderColumns == 12,
                "Reopened XLSX row/column count differs from fresh native City query");
        }

        var resultReceipt = new
        {
            evidenceType = "genuine-official-current-client-data",
            serverId,
            scanRunId = runId,
            runStatus = status,
            selectedTypesJson = types,
            completedSourceRunsByKind = sourceRunIds,
            totalBlocks,
            completedBlocks,
            createdAt,
            updatedAt,
            sqliteCounts = counts,
            nativeCityCount = cities.GetProperty("total").GetInt32(),
            nativeResourceCount = resources.GetProperty("total").GetInt32(),
            offlineNativeSummaryExplicitlyUnavailable = true,
            cityPage1Length = firstCity.GetArrayLength(),
            cityPage2Length = cityPage2.GetProperty("rows").GetArrayLength(),
            resourcePage1Length = firstResource.GetArrayLength(),
            resourcePage2Length = resourcePage2.GetProperty("rows").GetArrayLength(),
            cityPage1Digest = cityFirstHash,
            cityPage2Digest = citySecondHash,
            resourcePage1Digest = resourceFirstHash,
            resourcePage2Digest = resourceSecondHash,
            stagedCount = 0,
            nativeCityWorkbookRows = exported.GetProperty("rowCount").GetInt32(),
            reopenedXlsxRows = reopenedWorksheetRows,
            reopenedXlsxHeaders = reopenedHeaderColumns,
            nativeCityWorkbookSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workbook))),
            nativeQueriesOnFreshDatabase = true,
        };
        string receipt = Path.Combine(outputDirectory, "published-audit.json");
        File.WriteAllText(receipt, JsonSerializer.Serialize(resultReceipt,
            new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = true }));
        Console.WriteLine("MAP_005_PUBLISHED_AUDIT_OK " + receipt);
        Console.WriteLine(JsonSerializer.Serialize(resultReceipt, new JsonSerializerOptions(JsonOptions.Default)));
    }
}
