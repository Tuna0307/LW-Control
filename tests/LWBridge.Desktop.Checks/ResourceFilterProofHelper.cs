using System.Globalization;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ResourceFilterProofHelper
{
    internal sealed record Metrics(
        int RowCount,
        int CheckCount,
        string SampleResourceNameKey,
        int ResourceNameCount,
        int IdleCount,
        int FullCount,
        int NonBlackCount,
        int SampleLevel,
        int MinLevelCount,
        int MaxLevelCount,
        int ExactLevelCount,
        int DefaultCombinedCount);

    internal static Metrics Validate(
        MapDataStore store,
        int serverId,
        long sampledAt)
    {
        IReadOnlyList<JsonElement> rows = ReadAllRows(
            store, BuildQuery(serverId), sampledAt);
        if (rows.Count == 0)
            throw new InvalidDataException(
                "Resource filter proof requires a positive live population.");

        HashSet<string> allKeys = Keys(rows);
        if (rows.Any(row => !BoolKnown(row, "rebuildGatherOccupancyKnown")))
            throw new InvalidDataException(
                "Resource filter proof requires known occupancy on every live row.");
        if (rows.Any(row => !BoolKnown(row, "blackTileKnown")))
            throw new InvalidDataException(
                "Resource filter proof requires known black-tile classification on every live row.");

        string sampleName = rows
            .Select(row => OptionalString(row, "resourceNameKey"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "Resource filter proof has no resourceNameKey.");

        int checks = 0;
        HashSet<string> nameExpected = rows
            .Where(row => string.Equals(
                OptionalString(row, "resourceNameKey"),
                sampleName,
                StringComparison.Ordinal))
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("resourceNameKey", sampleName)),
            sampledAt,
            nameExpected,
            $"Resource resourceNameKey={sampleName}");
        checks++;

        HashSet<string> idleExpected = rows
            .Where(IsIdle)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("resourceIdleOnly", true)),
            sampledAt,
            idleExpected,
            "Resource resourceIdleOnly");
        checks++;

        HashSet<string> fullExpected = rows
            .Where(IsFull)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("resourceFullOnly", true)),
            sampledAt,
            fullExpected,
            "Resource resourceFullOnly");
        checks++;
        HashSet<string> nonBlackExpected = rows
            .Where(IsKnownNonBlack)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("excludeBlackTile", true)),
            sampledAt,
            nonBlackExpected,
            "Resource excludeBlackTile");
        checks++;

        int[] levels = rows
            .Select(row => OptionalInt(row, "level") ?? 0)
            .Where(level => level > 0)
            .OrderBy(level => level)
            .ToArray();
        if (levels.Length != rows.Count)
            throw new InvalidDataException(
                "Resource filter proof requires a positive level on every live row.");
        int sampleLevel = levels[levels.Length / 2];

        HashSet<string> minExpected = rows
            .Where(row => OptionalInt(row, "level") >= sampleLevel)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("minLevel", sampleLevel)),
            sampledAt,
            minExpected,
            $"Resource minLevel={sampleLevel}");
        checks++;

        HashSet<string> maxExpected = rows
            .Where(row => OptionalInt(row, "level") <= sampleLevel)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("maxLevel", sampleLevel)),
            sampledAt,
            maxExpected,
            $"Resource maxLevel={sampleLevel}");
        checks++;

        HashSet<string> exactExpected = rows
            .Where(row => OptionalInt(row, "level") == sampleLevel)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(
                serverId,
                ("minLevel", sampleLevel),
                ("maxLevel", sampleLevel)),
            sampledAt,
            exactExpected,
            $"Resource exact level={sampleLevel}");
        checks++;

        HashSet<string> defaultExpected = rows
            .Where(row => IsIdle(row) &&
                IsFull(row) &&
                IsKnownNonBlack(row))
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(
                serverId,
                ("resourceIdleOnly", true),
                ("resourceFullOnly", true),
                ("excludeBlackTile", true)),
            sampledAt,
            defaultExpected,
            "Resource default combined filters");
        checks++;

        HashSet<string> knownNames = rows
            .Where(row => !string.IsNullOrWhiteSpace(
                OptionalString(row, "resourceNameKey")))
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        if (!knownNames.SetEquals(allKeys))
            throw new InvalidDataException(
                "Resource live population has rows without resourceNameKey.");

        return new Metrics(
            rows.Count,
            checks,
            sampleName,
            nameExpected.Count,
            idleExpected.Count,
            fullExpected.Count,
            nonBlackExpected.Count,
            sampleLevel,
            minExpected.Count,
            maxExpected.Count,
            exactExpected.Count,
            defaultExpected.Count);
    }

    private static bool IsIdle(JsonElement row) =>
        ReadBool(row, "rebuildGatherOccupancyKnown") == true &&
        ReadBool(row, "rebuildGatherOccupied") == false;

    private static bool IsFull(JsonElement row) =>
        IsIdle(row) &&
        ReadBool(row, "resourceDetailKnown") == true &&
        ReadBool(row, "resourceFull") == true;

    private static bool IsKnownNonBlack(JsonElement row) =>
        ReadBool(row, "blackTileKnown") == true &&
        ReadBool(row, "isBlackTile") == false;
    private static bool BoolKnown(
        JsonElement row,
        string name) =>
        ReadBool(row, name).HasValue;

    private static bool? ReadBool(
        JsonElement row,
        string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value))
            return null;
        if (value.ValueKind == JsonValueKind.True) return true;
        if (value.ValueKind == JsonValueKind.False) return false;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out int number))
            return number != 0;
        return null;
    }

    private static int? OptionalInt(
        JsonElement row,
        string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value))
            return null;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out int numeric))
            return numeric;
        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int text))
            return text;
        return null;
    }

    private static string? OptionalString(
        JsonElement row,
        string name) =>
        row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static MapDataQueryOptions BuildQuery(
        int serverId,
        params (string Name, object Value)[] filters)
    {
        var query = new Dictionary<string, object?>
        {
            ["serverId"] = serverId,
            ["sorts"] = new[]
            {
                new { sortBy = "updatedAt", sortOrder = "desc" },
            },
        };
        foreach ((string name, object value) in filters)
            query[name] = value;

        JsonElement payload = JsonSerializer.SerializeToElement(
            new { kind = "resource", query },
            JsonOptions.Default);
        MapDataQueryOptions normalized =
            MapDataQueryContract.NormalizeSearch(payload);
        if (normalized.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                "Recovered Resource filter unexpectedly failed contract gate: " +
                string.Join(",", normalized.UnsupportedFeatures));
        return normalized;
    }

    private static IReadOnlyList<JsonElement> ReadAllRows(
        MapDataStore store,
        MapDataQueryOptions query,
        long sampledAt)
    {
        var rows = new List<JsonElement>();
        int page = 1;
        int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result =
                store.SearchIndexedAtForTest(
                    query with { Page = page },
                    sampledAt);
            if (expectedTotal < 0) expectedTotal = result.Total;
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal ||
                result.Rows.Count == 0)
                break;
            page++;
        }
        if (rows.Count != expectedTotal)
            throw new InvalidDataException(
                $"Resource filter proof observed " +
                $"{rows.Count}/{expectedTotal} rows.");
        return rows;
    }
    private static void AssertSet(
        MapDataStore store,
        MapDataQueryOptions query,
        long sampledAt,
        HashSet<string> expected,
        string label)
    {
        HashSet<string> actual = ReadAllRows(
                store, query, sampledAt)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            string missing = string.Join(
                ",", expected.Except(actual).Take(5));
            string extra = string.Join(
                ",", actual.Except(expected).Take(5));
            throw new InvalidDataException(
                $"{label} mismatch: actual={actual.Count}, " +
                $"expected={expected.Count}, missing=[{missing}], " +
                $"extra=[{extra}].");
        }
    }

    private static HashSet<string> Keys(
        IEnumerable<JsonElement> rows) =>
        rows.Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);

    private static string RecordKey(JsonElement row)
    {
        if (row.TryGetProperty(
                "recordKey",
                out JsonElement explicitKey) &&
            explicitKey.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(explicitKey.GetString()))
            return explicitKey.GetString()!;

        if (row.TryGetProperty(
                "pointId",
                out JsonElement pointId) &&
            pointId.TryGetInt64(out long parsedPointId))
            return parsedPointId.ToString(
                CultureInfo.InvariantCulture);

        if (row.TryGetProperty(
                "uuid",
                out JsonElement uuid) &&
            uuid.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(uuid.GetString()))
            return uuid.GetString()!;

        throw new InvalidDataException(
            "Resource filter proof row has no stable identity.");
    }
}
