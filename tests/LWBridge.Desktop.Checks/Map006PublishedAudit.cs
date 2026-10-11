using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

// Read-only independent native + reopened SQLite audit of a genuine completed
// MAP-006 task profile. The actual current game never runs in this audit host.
// Store only counts/hashes and a task-local XLSX; never publish player rows.
internal static class Map006PublishedAudit
{
    private static readonly string[] Kinds =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("MAP_006_AUDIT: " + message);
    }

    private static async Task<JsonElement> Invoke(
        Map317CommandService service, string command, object payload) =>
        JsonSerializer.SerializeToElement(await service.InvokeAsync(command,
            JsonSerializer.SerializeToElement(payload, JsonOptions.Default), CancellationToken.None),
            JsonOptions.Default);

    private static async Task<JsonElement> Search(
        Map317CommandService service, string kind, int server, int page = 1) =>
        await Invoke(service, "map_search", new
        {
            kind,
            query = new
            {
                serverId = server,
                page,
                pageSize = 50,
                sorts = new[] { new { sortBy = "updatedAt", sortOrder = "desc" } },
            },
        });

    private static string Digest(JsonElement rows) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", rows.EnumerateArray().Select(row => row.GetRawText())))));

    internal static async Task RunAsync(string database, string outputDirectory)
    {
        if (!File.Exists(database))
            throw new FileNotFoundException("MAP-006 genuine task database missing", database);
        if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new InvalidOperationException("MAP_006_AUDIT: do not overwrite existing native audit evidence");
        Directory.CreateDirectory(outputDirectory);

        int serverId;
        string runId;
        string status;
        string[] selectedKinds;
        int totalBlocks;
        int completedBlocks;
        int failedBlocks;
        var counts = Kinds.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);
        var visibleCounts = Kinds.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);
        using (var sql = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Pooling = false,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString()))
        {
            sql.Open();
            using (SqliteCommand command = sql.CreateCommand())
            {
                command.CommandText = "SELECT DISTINCT server_id FROM map_records";
                using SqliteDataReader reader = command.ExecuteReader();
                Require(reader.Read(), "Genuine result contains no published records");
                serverId = reader.GetInt32(0);
                Require(serverId > 0 && !reader.Read(), "Genuine task database spans multiple servers");
            }
            using (SqliteCommand command = sql.CreateCommand())
            {
                command.CommandText = """
                    SELECT kind,COUNT(*),COUNT(DISTINCT record_key)
                    FROM map_records WHERE server_id=$server GROUP BY kind
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string kind = reader.GetString(0);
                    Require(counts.ContainsKey(kind), "Unexpected newly captured kind " + kind);
                    int count = reader.GetInt32(1);
                    Require(count > 0 && count == reader.GetInt32(2),
                        kind + ": duplicate real record keys or zero row group");
                    counts[kind] = count;
                }
            }
            foreach (string kind in Kinds)
            {
                // Normal browsing is intentionally narrower than persisted
                // publication for expired Truck/Railway arrivals and foreign
                // radar Treasure rows. Count both independently; never erase
                // legitimate stored records to satisfy a UI count assertion.
                using SqliteCommand visible = sql.CreateCommand();
                string predicate = kind switch
                {
                    "truck" or "railway" => """
                        AND (json_extract(data_json,'$.arriveTs') IS NULL OR
                             CAST(json_extract(data_json,'$.arriveTs') AS INTEGER)>$now)
                        """,
                    "treasure" => """
                        AND (COALESCE(CAST(json_extract(data_json,'$.treasureType') AS INTEGER),0)<>1
                             OR (COALESCE(CAST(json_extract(data_json,'$.allianceId') AS TEXT),'')<>''
                                 AND CAST(json_extract(data_json,'$.allianceId') AS TEXT)=
                                   COALESCE(CAST(json_extract(data_json,'$.viewerAllianceId') AS TEXT),'')))
                        """,
                    _ => string.Empty,
                };
                visible.CommandText = "SELECT COUNT(*) FROM map_records WHERE server_id=$server AND kind=$kind " + predicate;
                visible.Parameters.AddWithValue("$server", serverId);
                visible.Parameters.AddWithValue("$kind", kind);
                if (kind is "truck" or "railway")
                    visible.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                visibleCounts[kind] = Convert.ToInt32(visible.ExecuteScalar());
            }
            using (SqliteCommand command = sql.CreateCommand())
            {
                command.CommandText = """
                    SELECT id,status,selected_types,total_blocks,completed_blocks,failed_blocks
                    FROM scan_runs WHERE server_id=$server ORDER BY created_at DESC LIMIT 1
                    """;
                command.Parameters.AddWithValue("$server", serverId);
                using SqliteDataReader reader = command.ExecuteReader();
                Require(reader.Read(), "Genuine scan history missing");
                runId = reader.GetString(0);
                status = reader.GetString(1);
                selectedKinds = JsonSerializer.Deserialize<string[]>(reader.GetString(2)) ?? [];
                totalBlocks = reader.GetInt32(3);
                completedBlocks = reader.GetInt32(4);
                failedBlocks = reader.GetInt32(5);
            }
            Require(status == "completed" && totalBlocks == 2500 && completedBlocks == 2500 &&
                failedBlocks == 0 && selectedKinds.Length is >= 1 and <= 8 &&
                selectedKinds.All(kind => Kinds.Contains(kind, StringComparer.Ordinal)) &&
                selectedKinds.Distinct(StringComparer.Ordinal).Count() == selectedKinds.Length,
                "Latest task run is not a completed, fully selected official 2500-block scan");
            foreach (string table in new[] { "scan_records", "dispatch_plunder_jobs", "truck_plunder_jobs" })
            {
                using SqliteCommand command = sql.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM " + table;
                Require(Convert.ToInt32(command.ExecuteScalar()) == 0,
                    "Task database contains staging or actionable jobs: " + table);
            }
        }

        var pageDigests = new Dictionary<string, string[]>(StringComparer.Ordinal);
        using (var native = new Map317CommandService(
            database, Map317.UnavailableMapProvider.Instance,
            Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false))
        {
            JsonElement options = await Invoke(native, "map_data_options", new { serverId });
            foreach (string kind in Kinds)
            {
                JsonElement first = await Search(native, kind, serverId);
                JsonElement second = await Search(native, kind, serverId, 2);
                JsonElement firstRows = first.GetProperty("rows");
                JsonElement secondRows = second.GetProperty("rows");
                Require(first.GetProperty("total").GetInt32() == visibleCounts[kind] &&
                    options.GetProperty("counts").GetProperty(kind).GetInt32() == counts[kind] &&
                    firstRows.GetArrayLength() == Math.Min(visibleCounts[kind], 50) &&
                    secondRows.GetArrayLength() == Math.Clamp(visibleCounts[kind] - 50, 0, 50),
                    kind + ": native stored search/pagination/options differ from fresh SQLite" +
                    $" (sql={counts[kind]}, sqlVisible={visibleCounts[kind]}, native={first.GetProperty("total").GetInt32()}," +
                    $" options={options.GetProperty("counts").GetProperty(kind).GetInt32()}," +
                    $" page1={firstRows.GetArrayLength()}, page2={secondRows.GetArrayLength()})");
                string firstHash = Digest(firstRows);
                string secondHash = Digest(secondRows);
                Require(visibleCounts[kind] <= 50 || firstHash != secondHash,
                    kind + ": page 2 duplicates page 1");
                pageDigests[kind] = [firstHash, secondHash];
            }

            JsonElement disconnectedSummary = await Invoke(native, "map_summary", new { });
            Require(disconnectedSummary.GetProperty("serverId").GetInt32() == 0,
                "Disconnected read-only host misreported an authenticated current server");

            string workbook = Path.Combine(outputDirectory, "map006-city-native-audit.xlsx");
            var request = native.PrepareCityExport(JsonSerializer.SerializeToElement(new
            {
                headers = new[] { "Server", "X", "Y", "Player", "UID", "UUID",
                    "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At" },
                sheetName = "Cities", yesLabel = "Yes", noLabel = "No",
                query = new { serverId, page = 1, pageSize = 50 },
            }, JsonOptions.Default));
            JsonElement exported = JsonSerializer.SerializeToElement(
                native.WriteCityExport(request, workbook), JsonOptions.Default);
            Require(exported.GetProperty("rowCount").GetInt32() == counts["city"],
                "Native Excel export omitted genuine City rows");
            using ZipArchive archive = ZipFile.OpenRead(workbook);
            ZipArchiveEntry? sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
            Require(sheet is not null, "Native City workbook missing worksheet");
            using Stream sheetStream = sheet!.Open();
            XDocument worksheet = XDocument.Load(sheetStream);
            XNamespace excel = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XElement[] xlsxRows = worksheet.Descendants(excel + "row").ToArray();
            Require(xlsxRows.Length == counts["city"] + 1 &&
                xlsxRows[0].Elements(excel + "c").Count() == 12,
                "Reopened City XLSX has incorrect genuine row/column shape");
        }

        File.WriteAllText(Path.Combine(outputDirectory, "published-audit.json"),
            JsonSerializer.Serialize(new
            {
                task = "LWB317-MAP-COMPLETE-DELIVERY-006",
                evidenceType = "fresh-current-client-data-independent-sqlite-native",
                serverId, runId, status, selectedKinds, totalBlocks, completedBlocks, failedBlocks,
                sqliteAndNativeCounts = counts,
                queryVisibleCounts = visibleCounts,
                positiveKinds = Kinds.Where(kind => counts[kind] > 0).ToArray(),
                zeroPopulationKinds = Kinds.Where(kind => counts[kind] == 0).ToArray(),
                pageDigests,
                stagedRecords = 0, actionableJobs = 0,
                offlineSummaryHasNoAuthenticatedServer = true,
                nativeCityExcelRows = counts["city"], nativeCityExcelColumns = 12,
            }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("MAP_006_FRESH_NATIVE_SQLITE_ALL_KINDS_AUDIT_OK");
    }
}
