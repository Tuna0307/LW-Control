using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class CityExportWorkbookChecks
{
    private static readonly XNamespace SheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    internal static void Run()
    {
        FullFilteredSnapshotExceedsFrontendPageAndWorkbookRoundTrips();
        RecoveredRowLimitAndCoercionRules();
        InvalidWorkbookOptionsFailClosed();
    }

    private static void FullFilteredSnapshotExceedsFrontendPageAndWorkbookRoundTrips()
    {
        const int serverId = 2212;
        const int rowCount = 235;
        const long updatedBase = 1_780_000_000_000L;
        const string largeUid = "900719925474099312345678901234567890";
        const string formulaLikeUuid = "=1+1";
        const string formulaLikeName = "=HYPERLINK(\"https://example.invalid\",\"not a formula\")";
        const long authoritativeShieldMs = 1_800_000_000_000L;

        using MapDataStore store = MapDataStore.CreateInMemory();
        for (int i = 0; i < rowCount; i++)
        {
            bool newest = i == rowCount - 1;
            string uid = newest ? largeUid : $"uid-{i:D3}";
            string uuid = newest ? formulaLikeUuid : $"uuid-{i:D3}";
            string ownerName = newest ? formulaLikeName : $"Player {i:D3}";
            long? shield = newest ? authoritativeShieldMs : null;
            string dataJson = JsonSerializer.Serialize(new
            {
                serverId = 9999,
                x = 100 + i,
                y = 200 + i,
                ownerName,
                ownerUid = uid,
                uuid,
                allianceName = "ALPHA",
                level = 30,
                health = 123456 + i,
                protectEndTime = 1_900_000_000L + i,
                updatedAt = 1,
            });

            store.UpsertRecord(new MapStoredRecord(
                "city",
                serverId,
                $"city-{i:D3}",
                i,
                uuid,
                ownerName,
                "ALPHA",
                30,
                null,
                null,
                null,
                shield,
                updatedBase + i,
                dataJson));
        }

        store.UpsertPlayerMark(new MapPlayerMark(
            serverId,
            largeUid,
            "marked",
            updatedBase + rowCount,
            null,
            "{}"));

        var query = new MapDataQueryOptions(
            Kind: "city",
            ServerId: serverId,
            Page: 1,
            PageSize: 200,
            Sorts: [new MapDataSort("updatedAt", "desc")],
            MarkedOnly: false,
            Keyword: null,
            Alliance: "ALPHA",
            WithoutAlliance: false,
            ResourceNameKey: null,
            MonsterNameKey: null,
            TreasureType: null,
            SuppliesType: null,
            Quality: null,
            ItemKey: null,
            CompletionStatus: null,
            PlunderableOnly: false,
            SpecialOnly: false,
            ReindeerOnly: false,
            MinLevel: null,
            MaxLevel: null,
            UnsupportedFeatures: Array.Empty<string>());

        var exportRows = new List<JsonElement>();
        int exportTotal = 0;
        for (int page = 1; page <= 1000; page++)
        {
            MapSearchResult pageResult = store.SearchCityPageForExport(query with { Page = page, PageSize = 200 });
            exportTotal = pageResult.Total;
            if (pageResult.Rows.Count == 0) break;
            exportRows.AddRange(pageResult.Rows);
            if (exportRows.Count >= exportTotal) break;
        }
        var snapshot = new MapSearchResult(exportRows, exportTotal);
        Check(snapshot.Total == rowCount && snapshot.Rows.Count == rowCount,
            "city export pagination must include the full filtered set beyond frontend pageSize=200");

        JsonElement first = snapshot.Rows[0];
        Check(first.GetProperty("serverId").GetInt32() == serverId,
            "city export snapshot must overlay authoritative indexed serverId");
        Check(first.GetProperty("updatedAt").GetInt64() == updatedBase + rowCount - 1,
            "city export snapshot must overlay authoritative indexed updatedAt");
        Check(first.GetProperty("shieldEndTime").GetInt64() == authoritativeShieldMs,
            "city export snapshot must overlay authoritative indexed shieldEndTime");
        Check(first.GetProperty("marked").GetBoolean(),
            "city export snapshot must carry current player-mark state");

        string[] headers =
        [
            "Server", "X", "Y", "Player", "UID", "UUID",
            "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At",
        ];
        var options = new CityExportWorkbookOptions(headers, "Cities", "Yes", "No");

        using var workbook = new MemoryStream();
        CityExportWorkbookWriteResult result =
            CityExportWorkbookWriter.Write(workbook, snapshot.Rows, options);
        Check(result.RowCount == rowCount, "writer rowCount must equal the full snapshot count");

        workbook.Position = 0;
        using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);
        string[] expectedParts =
        [
            "[Content_Types].xml",
            "_rels/.rels",
            "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels",
            "xl/styles.xml",
            "xl/worksheets/sheet1.xml",
        ];
        Check(archive.Entries.Select(entry => entry.FullName).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(expectedParts.OrderBy(x => x, StringComparer.Ordinal)),
            "workbook package must contain the six recovered original parts");

        XDocument sheet = LoadXml(archive, "xl/worksheets/sheet1.xml");
        XElement root = sheet.Root ?? throw new InvalidOperationException("worksheet root missing");
        Check((string?)root.Element(SheetNs + "dimension")?.Attribute("ref") == "A1:L236",
            "worksheet dimension must cover header plus all 235 data rows");
        Check((string?)root.Element(SheetNs + "autoFilter")?.Attribute("ref") == "A1:L236",
            "worksheet autofilter must cover the entire exported table");
        XElement? pane = root.Descendants(SheetNs + "pane").SingleOrDefault();
        Check(
            (string?)pane?.Attribute("ySplit") == "1" &&
            (string?)pane?.Attribute("topLeftCell") == "A2" &&
            (string?)pane?.Attribute("state") == "frozen",
            "worksheet must preserve the recovered frozen header");
        Check(root.Descendants(SheetNs + "row").Count() == rowCount + 1,
            "worksheet must reopen with exactly one header plus every exported row");
        Check(!root.Descendants(SheetNs + "f").Any(),
            "formula-like game strings must never be emitted as spreadsheet formulas");

        Check(InlineText(Cell(root, "E2")) == largeUid,
            "large UID must round-trip exactly as inline text");
        Check(InlineText(Cell(root, "F2")) == formulaLikeUuid,
            "formula-like UUID must round-trip exactly as inline text");
        Check(InlineText(Cell(root, "D2")) == formulaLikeName,
            "formula-like player name must round-trip exactly as inline text");
        Check((string?)Cell(root, "E2").Attribute("t") == "inlineStr" &&
              (string?)Cell(root, "F2").Attribute("t") == "inlineStr",
            "UID and UUID must use text cells, not spreadsheet numeric coercion");

        Check(CellValue(Cell(root, "A2")) == serverId.ToString(CultureInfo.InvariantCulture),
            "server column must use authoritative serverId");
        Check(CellValue(Cell(root, "B2")) == (100 + rowCount - 1).ToString(CultureInfo.InvariantCulture) &&
              CellValue(Cell(root, "C2")) == (200 + rowCount - 1).ToString(CultureInfo.InvariantCulture),
            "X/Y columns must preserve the sorted newest City row");
        Check((string?)Cell(root, "A2").Attribute("s") == "2" &&
              (string?)Cell(root, "H2").Attribute("s") == "2" &&
              (string?)Cell(root, "I2").Attribute("s") == "2",
            "numeric display columns must use the recovered style-2 slot");

        long originalProtectSeconds = 1_900_000_000L + rowCount - 1;
        double expectedProtectSerial =
            originalProtectSeconds * 1000d / 86_400_000d + 25_569d;
        double actualProtectSerial = double.Parse(
            CellValue(Cell(root, "J2")),
            CultureInfo.InvariantCulture);
        Check(Math.Abs(actualProtectSerial - expectedProtectSerial) < 1e-9 &&
              (string?)Cell(root, "J2").Attribute("s") == "3",
            "column J must prefer present protectEndTime over authoritative shieldEndTime");

        double expectedUpdatedSerial =
            DateTimeOffset.FromUnixTimeMilliseconds(updatedBase + rowCount - 1).UtcDateTime.ToOADate();
        double actualUpdatedSerial = double.Parse(
            CellValue(Cell(root, "L2")),
            CultureInfo.InvariantCulture);
        Check(Math.Abs(actualUpdatedSerial - expectedUpdatedSerial) < 1e-9 &&
              (string?)Cell(root, "L2").Attribute("s") == "3",
            "updatedAt must reopen as a style-3 Excel datetime");
        Check(InlineText(Cell(root, "K2")) == "Yes",
            "marked player must use the caller-provided yes label");

        XDocument styles = LoadXml(archive, "xl/styles.xml");
        XElement? cellXfs = styles.Root?.Element(SheetNs + "cellXfs");
        Check((string?)cellXfs?.Attribute("count") == "4" &&
              cellXfs?.Elements(SheetNs + "xf").Count() == 4,
            "styles.xml must preserve the recovered four-XF layout");
        XElement? customFormat = styles.Root?
            .Element(SheetNs + "numFmts")?
            .Element(SheetNs + "numFmt");
        Check(
            (string?)customFormat?.Attribute("numFmtId") == "164" &&
            (string?)customFormat?.Attribute("formatCode") == "yyyy-mm-dd hh:mm:ss",
            "styles.xml must preserve the recovered datetime number format");

        XDocument workbookXml = LoadXml(archive, "xl/workbook.xml");
        Check((string?)workbookXml.Root?
                .Descendants(SheetNs + "sheet")
                .Single()
                .Attribute("name") == "Cities",
            "workbook must preserve the caller-provided localized sheet name");
    }

    private static void RecoveredRowLimitAndCoercionRules()
    {
        Check(MapDataStore.MaxCityExportRows == 200_000,
            "City export row ceiling must remain the recovered 1000 pages x 200 rows");
        MapDataStore.RequireCityExportRowLimit(200_000);
        bool rejected = false;
        try
        {
            MapDataStore.RequireCityExportRowLimit(200_001);
        }
        catch (BridgeCommandException error)
        {
            rejected = error.Code == "MAP_EXPORT_FAILED" &&
                error.Message == "city export exceeded the row limit";
        }
        Check(rejected,
            "City export must fail with the recovered code/message above 200,000 rows");

        using JsonDocument rowDocument = JsonDocument.Parse("""
            [
              {
                "serverId": 1,
                "x": 1e3,
                "y": 20,
                "ownerName": 123,
                "ownerUid": 9007199254740993,
                "uuid": true,
                "allianceName": false,
                "level": 30,
                "health": 100,
                "protectEndTime": "1800000000",
                "shieldEndTime": 1800000000000,
                "marked": "true",
                "updatedAt": 99999999999
              },
              {
                "serverId": 1,
                "x": 11,
                "y": 21,
                "ownerName": "Player",
                "ownerUid": "900719925474099312345678901234567890",
                "uuid": "uuid-text",
                "allianceName": "A",
                "level": 31,
                "health": 101,
                "shieldEndTime": 100000000000,
                "marked": true,
                "updatedAt": 100000000000
              }
            ]
            """);
        JsonElement[] rows = rowDocument.RootElement
            .EnumerateArray()
            .Select(row => row.Clone())
            .ToArray();
        string[] headers =
        [
            "Server", "X", "Y", "Player", "UID", "UUID",
            "Alliance", "Level", "HP", "Shield Ends", "Marked", "Updated At",
        ];

        using var workbook = new MemoryStream();
        CityExportWorkbookWriter.Write(
            workbook,
            rows,
            new CityExportWorkbookOptions(headers, "Cities", "Yes", "No"));
        workbook.Position = 0;
        using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);
        XDocument sheet = LoadXml(archive, "xl/worksheets/sheet1.xml");
        XElement root = sheet.Root ?? throw new InvalidOperationException("worksheet root missing");

        Check(CellValue(Cell(root, "B2")) == "1000",
            "numeric cells must format the parsed JSON number rather than preserving exponent token text");
        Check(InlineText(Cell(root, "D2")) == string.Empty &&
              InlineText(Cell(root, "E2")) == string.Empty &&
              InlineText(Cell(root, "F2")) == string.Empty &&
              InlineText(Cell(root, "G2")) == string.Empty,
            "original City text cells must not coerce JSON numbers/booleans to text");
        Check(CellValue(Cell(root, "J2")) == string.Empty,
            "present non-number protectEndTime must blank J instead of falling back to shieldEndTime");
        Check(InlineText(Cell(root, "K2")) == "No",
            "marked must use Yes only for JSON true, not string truthiness");

        double belowThresholdSerial =
            99_999_999_999d * 1000d / 86_400_000d + 25_569d;
        double actualBelowThresholdSerial = double.Parse(
            CellValue(Cell(root, "L2")),
            CultureInfo.InvariantCulture);
        Check(Math.Abs(actualBelowThresholdSerial - belowThresholdSerial) < 1e-9,
            "timestamps below 1e11 must be interpreted as Unix seconds");

        double thresholdSerial = 100_000_000_000d / 86_400_000d + 25_569d;
        double actualFallbackSerial = double.Parse(
            CellValue(Cell(root, "J3")),
            CultureInfo.InvariantCulture);
        double actualThresholdSerial = double.Parse(
            CellValue(Cell(root, "L3")),
            CultureInfo.InvariantCulture);
        Check(Math.Abs(actualFallbackSerial - thresholdSerial) < 1e-9 &&
              Math.Abs(actualThresholdSerial - thresholdSerial) < 1e-9,
            "absent protectEndTime must fall back to shieldEndTime and 1e11 must be milliseconds");
        Check(InlineText(Cell(root, "K3")) == "Yes" &&
              InlineText(Cell(root, "E3")) == "900719925474099312345678901234567890",
            "JSON true must use Yes and large UID strings must remain lossless inline text");
    }

    private static void InvalidWorkbookOptionsFailClosed()
    {
        using var stream = new MemoryStream();
        try
        {
            CityExportWorkbookWriter.Write(
                stream,
                Array.Empty<JsonElement>(),
                new CityExportWorkbookOptions(["only-one"], "Cities", "Yes", "No"));
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException("city export writer must reject invalid header envelopes");
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = archive.GetEntry(path) ??
            throw new InvalidOperationException($"workbook part missing: {path}");
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XElement Cell(XElement worksheet, string reference) =>
        worksheet.Descendants(SheetNs + "c")
            .Single(cell => string.Equals(
                (string?)cell.Attribute("r"),
                reference,
                StringComparison.Ordinal));

    private static string InlineText(XElement cell) =>
        cell.Element(SheetNs + "is")?.Element(SheetNs + "t")?.Value ?? string.Empty;

    private static string CellValue(XElement cell) =>
        cell.Element(SheetNs + "v")?.Value ?? string.Empty;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
