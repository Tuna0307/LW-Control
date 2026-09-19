using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace LWBridge.Desktop;

internal sealed record CityExportWorkbookOptions(
    IReadOnlyList<string> Headers,
    string SheetName,
    string YesLabel,
    string NoLabel);

internal sealed record CityExportWorkbookWriteResult(int RowCount);

internal static class CityExportWorkbookWriter
{
    private const int ColumnCount = 12;
    private const string SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // RECOVERED LWB-R6-018: original writer package has these six parts,
    // A:L widths, frozen row 1, a four-XF style table whose style 1 is the
    // bold white/blue header format, and an explicit style-3 datetime template.
    // IMPLEMENTATION POLICY R7: until original per-column coercion is recovered,
    // this writer applies style 1 to headers, style 2 to integer-like display
    // columns, inlineStr to identifiers/text, and UTC Excel serials with style 3.
    internal static CityExportWorkbookWriteResult Write(
        Stream destination,
        IReadOnlyList<JsonElement> rows,
        CityExportWorkbookOptions options)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);
        if (!destination.CanWrite)
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        ValidateOptions(options);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        WriteUtf8Entry(archive, "[Content_Types].xml", ContentTypesXml);
        WriteUtf8Entry(archive, "_rels/.rels", RootRelationshipsXml);
        WriteWorkbook(archive, options.SheetName);
        WriteUtf8Entry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
        WriteUtf8Entry(archive, "xl/styles.xml", StylesXml);
        WriteWorksheet(archive, rows, options);
        return new CityExportWorkbookWriteResult(rows.Count);
    }

    private static void ValidateOptions(CityExportWorkbookOptions options)
    {
        if (options.Headers.Count != ColumnCount ||
            options.Headers.Any(header => string.IsNullOrWhiteSpace(header)))
            throw new ArgumentException("City export requires exactly 12 non-empty headers.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.SheetName) ||
            options.SheetName.Length > 31 ||
            options.SheetName.IndexOfAny([':', '\\', '/', '?', '*', '[', ']']) >= 0)
            throw new ArgumentException("City export sheet name is invalid.", nameof(options));
        if (string.IsNullOrEmpty(options.YesLabel) || string.IsNullOrEmpty(options.NoLabel))
            throw new ArgumentException("City export yes/no labels are required.", nameof(options));
    }

    private static void WriteWorkbook(ZipArchive archive, string sheetName)
    {
        using XmlWriter writer = CreateXmlWriter(archive, "xl/workbook.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
        writer.WriteStartElement("sheets");
        writer.WriteStartElement("sheet");
        writer.WriteAttributeString("name", sheetName);
        writer.WriteAttributeString("sheetId", "1");
        writer.WriteAttributeString("r", "id", RelationshipNamespace, "rId1");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorksheet(
        ZipArchive archive,
        IReadOnlyList<JsonElement> rows,
        CityExportWorkbookOptions options)
    {
        int lastRow = Math.Max(1, rows.Count + 1);
        using XmlWriter writer = CreateXmlWriter(archive, "xl/worksheets/sheet1.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);

        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", $"A1:L{lastRow}");
        writer.WriteEndElement();

        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("cols");
        WriteColumn(writer, 1, 3, 11);
        WriteColumn(writer, 4, 4, 22);
        WriteColumn(writer, 5, 6, 24);
        WriteColumn(writer, 7, 7, 18);
        WriteColumn(writer, 8, 9, 12);
        WriteColumn(writer, 10, 10, 21);
        WriteColumn(writer, 11, 11, 10);
        WriteColumn(writer, 12, 12, 21);
        writer.WriteEndElement();

        writer.WriteStartElement("sheetData");
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", "1");
        writer.WriteAttributeString("ht", "20");
        writer.WriteAttributeString("customHeight", "1");
        for (int i = 0; i < ColumnCount; i++)
            WriteInlineStringCell(writer, CellReference(i, 1), options.Headers[i], styleIndex: 1);
        writer.WriteEndElement();

        for (int i = 0; i < rows.Count; i++)
            WriteDataRow(writer, rows[i], i + 2, options);
        writer.WriteEndElement();

        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A1:L{lastRow}");
        writer.WriteEndElement();

        writer.WriteStartElement("pageMargins");
        writer.WriteAttributeString("left", "0.7");
        writer.WriteAttributeString("right", "0.7");
        writer.WriteAttributeString("top", "0.75");
        writer.WriteAttributeString("bottom", "0.75");
        writer.WriteAttributeString("header", "0.3");
        writer.WriteAttributeString("footer", "0.3");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteDataRow(
        XmlWriter writer,
        JsonElement row,
        int rowNumber,
        CityExportWorkbookOptions options)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

        WriteJsonNumberCell(writer, $"A{rowNumber}", row, "serverId", styleIndex: 2);
        WriteJsonNumberCell(writer, $"B{rowNumber}", row, "x", styleIndex: 2);
        WriteJsonNumberCell(writer, $"C{rowNumber}", row, "y", styleIndex: 2);
        WriteInlineStringCell(writer, $"D{rowNumber}", ReadText(row, "ownerName"));
        WriteInlineStringCell(writer, $"E{rowNumber}", ReadText(row, "ownerUid"));
        WriteInlineStringCell(writer, $"F{rowNumber}", ReadText(row, "uuid"));
        WriteInlineStringCell(writer, $"G{rowNumber}", ReadText(row, "allianceName"));
        WriteJsonNumberCell(writer, $"H{rowNumber}", row, "level", styleIndex: 2);
        WriteJsonNumberCell(writer, $"I{rowNumber}", row, "health", styleIndex: 2);

        long? shield = ReadPositiveTimestamp(row, "shieldEndTime") ??
            ReadPositiveTimestamp(row, "protectEndTime");
        WriteTimestampCell(writer, $"J{rowNumber}", shield);

        bool marked = ReadBoolean(row, "marked");
        WriteInlineStringCell(writer, $"K{rowNumber}", marked ? options.YesLabel : options.NoLabel);

        WriteTimestampCell(writer, $"L{rowNumber}", ReadPositiveTimestamp(row, "updatedAt"));
        writer.WriteEndElement();
    }

    private static void WriteColumn(XmlWriter writer, int min, int max, int width)
    {
        writer.WriteStartElement("col");
        writer.WriteAttributeString("min", min.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", max.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void WriteJsonNumberCell(
        XmlWriter writer,
        string reference,
        JsonElement row,
        string property,
        int styleIndex)
    {
        if (!row.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number)
        {
            WriteEmptyCell(writer, reference, styleIndex);
            return;
        }
        WriteValueCell(writer, reference, value.GetRawText(), styleIndex);
    }

    private static void WriteTimestampCell(XmlWriter writer, string reference, long? unixTimestamp)
    {
        if (!unixTimestamp.HasValue)
        {
            WriteEmptyCell(writer, reference, 3);
            return;
        }

        DateTimeOffset instant = unixTimestamp.Value >= 1_000_000_000_000L
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp.Value)
            : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp.Value);
        double serial = instant.UtcDateTime.ToOADate();
        WriteValueCell(writer, reference, serial.ToString("G17", CultureInfo.InvariantCulture), 3);
    }

    private static void WriteValueCell(
        XmlWriter writer,
        string reference,
        string value,
        int styleIndex)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        if (styleIndex != 0)
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("v");
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteEmptyCell(XmlWriter writer, string reference, int styleIndex = 0)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        if (styleIndex != 0)
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteInlineStringCell(
        XmlWriter writer,
        string reference,
        string? value,
        int styleIndex = 0)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("t", "inlineStr");
        if (styleIndex != 0)
            writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        writer.WriteString(value ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string? ReadText(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out JsonElement value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static long? ReadPositiveTimestamp(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out JsonElement value))
            return null;
        long parsed;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out parsed))
            return parsed > 0 ? parsed : null;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            return parsed > 0 ? parsed : null;
        return null;
    }

    private static bool ReadBoolean(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out JsonElement value))
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt64(out long n) && n != 0,
            JsonValueKind.String => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(value.GetString(), "1", StringComparison.Ordinal),
            _ => false,
        };
    }

    private static string CellReference(int zeroBasedColumn, int row) =>
        $"{(char)('A' + zeroBasedColumn)}{row}";

    private static XmlWriter CreateXmlWriter(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        return XmlWriter.Create(entry.Open(), new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            CloseOutput = true,
            Indent = false,
            OmitXmlDeclaration = false,
        });
    }

    private static void WriteUtf8Entry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: false);
        writer.Write(content);
    }

    private const string ContentTypesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    private const string RootRelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private const string WorkbookRelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    private const string StylesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd hh:mm:ss\"/></numFmts>" +
        "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><color rgb=\"00FFFFFF\"/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
        "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"004F81BD\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
        "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";
}
