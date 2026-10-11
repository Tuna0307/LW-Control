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

    private static int VisibleArrivalsAt(string database, string kind, int server, long now)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = database, Pooling = false, Mode = SqliteOpenMode.ReadOnly,
        }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM map_records WHERE kind=$kind AND server_id=$server
              AND (json_extract(data_json,'$.arriveTs') IS NULL
                OR CAST(json_extract(data_json,'$.arriveTs') AS INTEGER)>$now)
            """;
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$server", server);
        command.Parameters.AddWithValue("$now", now);
        return Convert.ToInt32(command.ExecuteScalar());
    }

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
        var actualFilterChecks = new Dictionary<string, object>(StringComparer.Ordinal);
        var expiringSearchCountRanges = new Dictionary<string, object>(StringComparer.Ordinal);
        using (var native = new Map317CommandService(
            database, Map317.UnavailableMapProvider.Instance,
            Map317.UnavailableMapActionProvider.Instance, startPlunderWorkers: false))
        {
            JsonElement options = await Invoke(native, "map_data_options", new { serverId });
            foreach (string kind in Kinds)
            {
                bool expiring = kind is "truck" or "railway";
                long beforeFirst = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                JsonElement first = await Search(native, kind, serverId);
                long afterFirst = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long beforeSecond = afterFirst;
                JsonElement second = await Search(native, kind, serverId, 2);
                long afterSecond = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                JsonElement firstRows = first.GetProperty("rows");
                JsonElement secondRows = second.GetProperty("rows");
                int firstTotal = first.GetProperty("total").GetInt32();
                int secondTotal = second.GetProperty("total").GetInt32();
                bool verifiedVisibleCount = !expiring
                    ? firstTotal == visibleCounts[kind] && secondTotal == firstTotal
                    : firstTotal >= VisibleArrivalsAt(database, kind, serverId, afterFirst) &&
                      firstTotal <= VisibleArrivalsAt(database, kind, serverId, beforeFirst) &&
                      secondTotal >= VisibleArrivalsAt(database, kind, serverId, afterSecond) &&
                      secondTotal <= VisibleArrivalsAt(database, kind, serverId, beforeSecond) &&
                      secondTotal <= firstTotal;
                if (expiring)
                    expiringSearchCountRanges[kind] = new { firstTotal, secondTotal,
                        firstStartedAt = beforeFirst, firstFinishedAt = afterFirst,
                        secondStartedAt = beforeSecond, secondFinishedAt = afterSecond };
                Require(verifiedVisibleCount &&
                    options.GetProperty("counts").GetProperty(kind).GetInt32() == counts[kind] &&
                    firstRows.GetArrayLength() == Math.Min(firstTotal, 50) &&
                    secondRows.GetArrayLength() == Math.Clamp(secondTotal - 50, 0, 50),
                    kind + ": native stored search/pagination/options differ from fresh SQLite" +
                    $" (sql={counts[kind]}, sqlVisible={visibleCounts[kind]}, native={firstTotal}/{secondTotal}," +
                    $" options={options.GetProperty("counts").GetProperty(kind).GetInt32()}," +
                    $" page1={firstRows.GetArrayLength()}, page2={secondRows.GetArrayLength()})");
                string firstHash = Digest(firstRows);
                string secondHash = Digest(secondRows);
                Require(secondRows.GetArrayLength() == 0 || firstHash != secondHash,
                    kind + ": page 2 duplicates page 1");
                pageDigests[kind] = [firstHash, secondHash];
            }

            // Exercise genuine option-driven filters with the actual native
            // command parser/query path. Preserve only keys/counts in receipts.
            foreach (string kind in new[] { "resource", "monster" })
            {
                JsonElement names = options.GetProperty("names").GetProperty(kind);
                if (names.GetArrayLength() == 0) continue;
                JsonElement candidate = names[0];
                string key = candidate.GetProperty("key").GetString()!;
                string parameter = kind == "resource" ? "resourceNameKey" : "monsterNameKey";
                JsonElement selected = await Invoke(native, "map_search", new
                {
                    kind,
                    query = new Dictionary<string, object>
                    {
                        ["serverId"] = serverId, ["page"] = 1, ["pageSize"] = 50,
                        [parameter] = key,
                    },
                });
                int expected = candidate.GetProperty("count").GetInt32();
                int actual = selected.GetProperty("total").GetInt32();
                Require(actual == expected && actual > 0 &&
                    selected.GetProperty("rows").GetArrayLength() == Math.Min(actual, 50) &&
                    selected.GetProperty("rows").EnumerateArray().All(row =>
                        row.TryGetProperty(parameter, out JsonElement value) &&
                        value.ToString() == key), kind + ": real native option filter mismatched its fresh source rows");
                actualFilterChecks[kind + "Name"] = new { key, rows = actual };
            }
            JsonElement levels = options.GetProperty("dispatchLevels");
            if (levels.GetArrayLength() > 0)
            {
                int level = levels[0].GetInt32();
                JsonElement selected = await Invoke(native, "map_search", new
                {
                    kind = "dispatch", query = new { serverId, minLevel = level, maxLevel = level,
                        page = 1, pageSize = 50 },
                });
                Require(selected.GetProperty("total").GetInt32() > 0 &&
                    selected.GetProperty("rows").EnumerateArray().All(row =>
                        row.TryGetProperty("level", out JsonElement value) && value.GetInt32() == level),
                    "real native Dispatch exact-level filter produced wrong rows");
                actualFilterChecks["dispatchLevel"] = new { level, rows = selected.GetProperty("total").GetInt32() };
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
                pageDigests, actualFilterChecks, expiringSearchCountRanges,
                stagedRecords = 0, actionableJobs = 0,
                offlineSummaryHasNoAuthenticatedServer = true,
                nativeCityExcelRows = counts["city"], nativeCityExcelColumns = 12,
            }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("MAP_006_FRESH_NATIVE_SQLITE_ALL_KINDS_AUDIT_OK");
    }
}
