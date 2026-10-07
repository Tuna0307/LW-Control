using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;
using Microsoft.Data.Sqlite;

namespace LWBridge.Desktop.Checks;

internal static class PilotLocalMapVerificationChecks
{
    private static readonly XNamespace SheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    internal static JsonElement Run()
    {
        string sourcePath = Environment.GetEnvironmentVariable("LWB317_PILOT_MAP_DB")
            ?? throw new InvalidOperationException("LWB317_PILOT_MAP_DB is required.");
        string outputPath = Environment.GetEnvironmentVariable("LWB317_PILOT_LOCAL_OUTPUT")
            ?? throw new InvalidOperationException("LWB317_PILOT_LOCAL_OUTPUT is required.");
        int serverId = int.Parse(
            Environment.GetEnvironmentVariable("LWB317_PILOT_SERVER_ID") ?? "2212",
            System.Globalization.CultureInfo.InvariantCulture);

        sourcePath = Path.GetFullPath(sourcePath);
        outputPath = Path.GetFullPath(outputPath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Pilot Map database is missing.", sourcePath);

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "lwb317-pilot-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string snapshotPath = Path.Combine(tempRoot, "map-data.db");

        try
        {
            using (var source = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = sourcePath,
                    Mode = SqliteOpenMode.ReadOnly,
                }.ToString()))
            using (var destination = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = snapshotPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            int cityCount;
            int resourceCount;
            CityFilterProofHelper.Metrics filters;
            int pageOneCount;
            int pageTwoCount;
            int exportCount;

            using (var store = new MapDataStore(snapshotPath))
            {
                cityCount = store.CountRecords("city", serverId);
                resourceCount = store.CountRecords("resource", serverId);
                if (cityCount <= 0)
                    throw new InvalidDataException(
                        "Pilot local Map verification requires persisted City rows.");

                long sampledAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                CityFilterProofHelper.SeedTransientLiveMark(store, serverId, sampledAt);
                filters = CityFilterProofHelper.Validate(store, serverId, sampledAt);

                MapDataQueryOptions paged = NormalizeCityQuery(
                    serverId,
                    page: 1,
                    pageSize: 50);
                MapSearchResult pageOne = store.SearchIndexedAtForTest(paged, sampledAt);
                MapSearchResult pageTwo = store.SearchIndexedAtForTest(
                    paged with { Page = 2 },
                    sampledAt);
                pageOneCount = pageOne.Rows.Count;
                pageTwoCount = pageTwo.Rows.Count;
                if (pageOne.Total != cityCount ||
                    pageTwo.Total != cityCount ||
                    pageOneCount == 0 ||
                    pageTwoCount == 0)
                {
                    throw new InvalidDataException(
                        "Persisted City pagination did not expose multiple pages.");
                }

                HashSet<string> firstKeys = Keys(pageOne.Rows);
                if (firstKeys.Overlaps(Keys(pageTwo.Rows)))
                    throw new InvalidDataException(
                        "Persisted City pagination repeated rows across page 1/page 2.");

                var exportRows = new List<JsonElement>();
                int exportTotal = 0;
                for (int page = 1; page <= 1000; page++)
                {
                    MapSearchResult result = store.SearchCityPageForExport(
                        paged with { Page = page, PageSize = 200 });
                    exportTotal = result.Total;
                    if (result.Rows.Count == 0) break;
                    exportRows.AddRange(result.Rows);
                    if (exportRows.Count >= exportTotal) break;
                }
                MapDataStore.RequireCityExportRowLimit(exportTotal);
                if (exportRows.Count != exportTotal || exportTotal != cityCount)
                    throw new InvalidDataException(
                        $"City export paging mismatch: rows={exportRows.Count}, total={exportTotal}, cityCount={cityCount}.");

                Directory.CreateDirectory(
                    Path.GetDirectoryName(outputPath)
                    ?? throw new InvalidOperationException("Output directory missing."));
                using (FileStream workbook = new(
                    outputPath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None))
                {
                    CityExportWorkbookWriteResult written =
                        CityExportWorkbookWriter.Write(
                            workbook,
                            exportRows,
                            new CityExportWorkbookOptions(
                                [
                                    "Server", "X", "Y", "Player", "UID", "UUID",
                                    "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At",
                                ],
                                "Cities",
                                "Yes",
                                "No"));
                    exportCount = written.RowCount;
                }
            }

            using (var reopened = new MapDataStore(snapshotPath))
            {
                if (reopened.CountRecords("city", serverId) != cityCount ||
                    reopened.CountRecords("resource", serverId) != resourceCount)
                {
                    throw new InvalidDataException(
                        "Persisted Map counts changed after database reopen.");
                }
            }

            int workbookDataRows;
            using (FileStream stream = File.OpenRead(outputPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry entry =
                    archive.GetEntry("xl/worksheets/sheet1.xml")
                    ?? throw new InvalidDataException("Exported workbook has no sheet1.xml.");
                using Stream sheetStream = entry.Open();
                XDocument sheet = XDocument.Load(sheetStream);
                workbookDataRows =
                    sheet.Descendants(SheetNs + "row").Count() - 1;
            }
            if (workbookDataRows != exportCount || exportCount != cityCount)
                throw new InvalidDataException(
                    "Exported workbook did not reopen with every persisted City row.");

            return JsonSerializer.SerializeToElement(new
            {
                ok = true,
                sourceDatabase = sourcePath,
                sourceWasSnapshottedReadOnly = true,
                snapshotDatabase = snapshotPath,
                serverId,
                cityCount,
                resourceCount,
                pagination = new
                {
                    pageSize = 50,
                    pageOneCount,
                    pageTwoCount,
                    distinctAcrossFirstTwoPages = true,
                },
                filters,
                export = new
                {
                    path = outputPath,
                    rowCount = exportCount,
                    reopenedWorkbookRows = workbookDataRows,
                },
                databaseReopen = true,
                gameCommandsIssued = false,
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static MapDataQueryOptions NormalizeCityQuery(
        int serverId,
        int page,
        int pageSize)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(
            new
            {
                kind = "city",
                query = new
                {
                    serverId,
                    page,
                    pageSize,
                    sorts = new[]
                    {
                        new { sortBy = "updatedAt", sortOrder = "desc" },
                    },
                },
            },
            JsonOptions.Default);
        MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(payload);
        if (query.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                "Pilot local City query failed contract gate.");
        return query;
    }

    private static HashSet<string> Keys(IEnumerable<JsonElement> rows) =>
        rows.Select(row =>
                row.TryGetProperty("recordKey", out JsonElement key)
                    ? key.GetString()
                    : null)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
}
