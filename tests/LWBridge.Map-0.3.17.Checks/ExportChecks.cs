using System.IO.Compression;

namespace LWBridge.Map317.Checks;

internal static class ExportChecks
{
    internal static void Run()
    {
        using MapStore store = MapStore.CreateInMemory();
        store.UpsertRecord(new MapRecord("city", 42, "c", null, "uuid-1", "Player", "ALLY", 25, null, null,
            null, 1_700_000_000_000, 1_700_000_100_000,
            "{\"ownerUid\":\"12345678901234567890\",\"ownerName\":\"Player\",\"uuid\":\"uuid-1\",\"allianceName\":\"ALLY\",\"level\":25,\"health\":99,\"x\":100,\"y\":200,\"protectEndTime\":1700000000000}"));
        store.SetPlayerMark(new MapPlayerMark(42, "12345678901234567890", "marked", 1, null,
            "{\"ownerUid\":\"12345678901234567890\"}"), true);
        string directory = Path.Combine(Path.GetTempPath(), "lwbridge317-map-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "cities.xlsx");
            var exporter = new MapExporter(store);
            string[] headers = ["Server", "X", "Y", "Player", "UID", "UUID", "Alliance", "Level", "HP", "Shield", "Marked", "Updated"];
            CityExportResult result = exporter.ExportCitiesToPath(new MapQuery("city", 42),
                new CityExportOptions(headers, "Cities", "Yes", "No"), path);
            TestAssert.True(!result.Canceled && result.RowCount == 1 && File.Exists(path), "city export result mismatch");
            using ZipArchive zip = ZipFile.OpenRead(path);
            TestAssert.True(zip.GetEntry("xl/worksheets/sheet1.xml") is not null, "xlsx sheet missing");
            using StreamReader reader = new(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            string sheet = reader.ReadToEnd();
            TestAssert.True(sheet.Contains("12345678901234567890", StringComparison.Ordinal), "UID text missing from export");
            TestAssert.True(sheet.Contains(">Yes<", StringComparison.Ordinal), "marked label missing from export");
            TestAssert.Equal("map-cities-42-20260930-010203.xlsx",
                MapExporter.DefaultCityFileName(42, new DateTimeOffset(2026, 9, 30, 1, 2, 3, TimeSpan.Zero)),
                "default filename mismatch");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
