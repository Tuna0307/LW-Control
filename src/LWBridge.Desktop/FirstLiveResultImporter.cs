using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed record FirstLiveResultImport(
    int ServerId,
    string RecordKey,
    int PointIndex,
    int X,
    int Y,
    int? Level,
    long CapturedAtUnixMilliseconds,
    string CaptureSha256,
    string SourcePath,
    string DataJson);

internal static class FirstLiveResultImporter
{
    public static FirstLiveResultImport ImportOneResource(MapDataStore store, string diagnosticsPath)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (string.IsNullOrWhiteSpace(diagnosticsPath))
            throw new ArgumentException("First-live diagnostics path is required.", nameof(diagnosticsPath));

        string sourcePath = Path.GetFullPath(diagnosticsPath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("First-live diagnostics file was not found.", sourcePath);

        string captureSha256;
        using (FileStream hashStream = File.OpenRead(sourcePath))
            captureSha256 = Convert.ToHexString(SHA256.HashData(hashStream)).ToLowerInvariant();

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(sourcePath));
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("First-live diagnostics root must be a JSON object.");

        if (!root.TryGetProperty("capturedAt", out JsonElement capturedAtValue) ||
            capturedAtValue.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                capturedAtValue.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset capturedAt))
        {
            throw new InvalidDataException("First-live diagnostics are missing a valid capturedAt timestamp.");
        }

        if (!root.TryGetProperty("point_records", out JsonElement records) || records.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("First-live diagnostics are missing point_records.");

        JsonElement? selected = null;
        foreach (JsonElement candidate in records.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object ||
                !candidate.TryGetProperty("kind", out JsonElement kind) ||
                kind.ValueKind != JsonValueKind.String ||
                !string.Equals(kind.GetString(), "resource_point", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryReadPositiveInt(candidate, "serverId", out _) ||
                !TryReadPositiveInt(candidate, "pointId", out _) ||
                !TryReadPositiveInt(candidate, "x", out _) ||
                !TryReadPositiveInt(candidate, "y", out _))
            {
                continue;
            }

            selected = candidate.Clone();
            break;
        }

        if (selected is null)
            throw new InvalidDataException("First-live diagnostics contain no resource_point with a usable server/point/coordinate identity.");

        JsonElement point = selected.Value;
        TryReadPositiveInt(point, "serverId", out int serverId);
        TryReadPositiveInt(point, "pointId", out int pointIndex);
        TryReadPositiveInt(point, "x", out int x);
        TryReadPositiveInt(point, "y", out int y);
        string recordKey = pointIndex.ToString(CultureInfo.InvariantCulture);
        long updatedAt = capturedAt.ToUnixTimeMilliseconds();

        JsonObject normalized = JsonNode.Parse(point.GetRawText())?.AsObject()
            ?? throw new InvalidDataException("Selected resource point could not be normalized as a JSON object.");
        normalized["sourceKind"] = "resource_point";
        // IMPLEMENTATION POLICY: this bounded first-live adapter maps the prior
        // read-only probe's resource_point category to LWBridge's recovered public
        // resource kind. Game-derived fields stay source-backed; unsupported fields
        // such as resourceNameKey remain absent rather than guessed.
        normalized["kind"] = "resource";
        normalized["recordKey"] = recordKey;
        normalized["pointIndex"] = pointIndex;
        normalized["updatedAt"] = updatedAt;
        string dataJson = normalized.ToJsonString(JsonOptions.Default);

        int? level = TryReadInt(point, "level", out int parsedLevel) ? parsedLevel : null;
        string? name = ReadNonEmptyString(point, "name");
        store.UpsertRecord(new MapStoredRecord(
            "resource",
            serverId,
            recordKey,
            pointIndex,
            Uuid: null,
            name,
            AllianceName: null,
            level,
            Quality: null,
            Power: null,
            Distance: null,
            ShieldEndTime: null,
            updatedAt,
            dataJson));

        return new FirstLiveResultImport(
            serverId,
            recordKey,
            pointIndex,
            x,
            y,
            level,
            updatedAt,
            captureSha256,
            sourcePath,
            dataJson);
    }

    private static bool TryReadPositiveInt(JsonElement value, string propertyName, out int result) =>
        TryReadInt(value, propertyName, out result) && result > 0;

    private static bool TryReadInt(JsonElement value, string propertyName, out int result)
    {
        result = 0;
        return value.TryGetProperty(propertyName, out JsonElement property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out result);
    }

    private static string? ReadNonEmptyString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            return null;
        string? result = property.GetString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
