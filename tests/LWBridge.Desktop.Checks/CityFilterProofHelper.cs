using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class CityFilterProofHelper
{
    internal sealed record Metrics(
        int RowCount,
        int CheckCount,
        int AllianceCount,
        int WithoutAllianceCount,
        int MarkedCount,
        int KeywordIdentityCount,
        int KeywordPercentLiteralCount,
        int KeywordUnderscoreLiteralCount,
        int KeywordBackslashLiteralCount);

    internal static void SeedTransientLiveMark(
        MapDataStore store,
        int serverId,
        long sampledAt)
    {
        MapStoredRecord[] records =
            store.ReadRecords("city", serverId).ToArray();
        string? ownerUid = records
            .Select(record => OwnerUid(record.DataJson))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ownerUid))
            throw new InvalidDataException(
                "City filter proof has no live ownerUid to seed a transient mark.");

        store.UpsertPlayerMark(new MapPlayerMark(
            serverId,
            ownerUid,
            "marked",
            sampledAt,
            null,
            JsonSerializer.Serialize(new { ownerUid }, JsonOptions.Default)));
    }

    internal static Metrics Validate(
        MapDataStore store,
        int serverId,
        long sampledAt)
    {
        MapStoredRecord[] records =
            store.ReadRecords("city", serverId).ToArray();
        if (records.Length == 0)
            throw new InvalidDataException(
                "City filter proof requires a positive live population.");

        IReadOnlyList<JsonElement> baseRows =
            ReadAllRows(store, BuildQuery(serverId), sampledAt);
        if (baseRows.Count != records.Length ||
            !Keys(baseRows).SetEquals(
                records.Select(record => record.RecordKey)))
            throw new InvalidDataException(
                "City raw-record/query populations differ before filter proof.");

        int checks = 0;
        string sampleAlliance = records
            .Select(record => record.AllianceName)
            .Where(value => !string.IsNullOrEmpty(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "City filter proof has no nonempty alliance.");

        HashSet<string> allianceExpected = records
            .Where(record => string.Equals(
                record.AllianceName,
                sampleAlliance,
                StringComparison.Ordinal))
            .Select(record => record.RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("alliance", sampleAlliance)),
            sampledAt,
            allianceExpected,
            "City alliance");
        checks++;

        HashSet<string> withoutAllianceExpected = records
            .Where(record => string.IsNullOrEmpty(record.AllianceName))
            .Select(record => record.RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("withoutAlliance", true)),
            sampledAt,
            withoutAllianceExpected,
            "City withoutAlliance");
        checks++;
        HashSet<string> markedExpected = records
            .Where(record =>
            {
                string? ownerUid = OwnerUid(record.DataJson);
                return !string.IsNullOrWhiteSpace(ownerUid) &&
                    store.GetPlayerMark(serverId, ownerUid) is not null;
            })
            .Select(record => record.RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        if (markedExpected.Count == 0)
            throw new InvalidDataException(
                "City filter proof transient mark did not match a live row.");
        AssertSet(
            store,
            BuildQuery(serverId, ("markedOnly", true)),
            sampledAt,
            markedExpected,
            "City markedOnly");
        checks++;

        string identityKeyword = records
            .Select(record => record.Uuid)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "City filter proof has no live UUID for keyword verification.");
        HashSet<string> identityKeywordExpected =
            KeywordExpected(records, identityKeyword);
        if (identityKeywordExpected.Count == 0)
            throw new InvalidDataException(
                "City identity keyword did not match any raw live record.");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", identityKeyword)),
            sampledAt,
            identityKeywordExpected,
            "City keyword identity");
        checks++;

        HashSet<string> percentExpected =
            KeywordExpected(records, "%");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", "%")),
            sampledAt,
            percentExpected,
            "City keyword literal percent");
        checks++;

        HashSet<string> underscoreExpected =
            KeywordExpected(records, "_");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", "_")),
            sampledAt,
            underscoreExpected,
            "City keyword literal underscore");
        checks++;

        HashSet<string> backslashExpected =
            KeywordExpected(records, "\\");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", "\\")),
            sampledAt,
            backslashExpected,
            "City keyword literal backslash");
        checks++;

        return new Metrics(
            records.Length,
            checks,
            allianceExpected.Count,
            withoutAllianceExpected.Count,
            markedExpected.Count,
            identityKeywordExpected.Count,
            percentExpected.Count,
            underscoreExpected.Count,
            backslashExpected.Count);
    }

    private static HashSet<string> KeywordExpected(
        IEnumerable<MapStoredRecord> records,
        string keyword) =>
        records
            .Where(record =>
                ContainsLiteral(record.Name, keyword) ||
                ContainsLiteral(record.AllianceName, keyword) ||
                ContainsLiteral(record.Uuid, keyword) ||
                ContainsLiteral(record.DataJson, keyword))
            .Select(record => record.RecordKey)
            .ToHashSet(StringComparer.Ordinal);

    private static bool ContainsLiteral(
        string? value,
        string keyword) =>
        value?.IndexOf(
            keyword,
            StringComparison.OrdinalIgnoreCase) >= 0;
    private static string? OwnerUid(string dataJson)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        return document.RootElement.TryGetProperty(
                "ownerUid",
                out JsonElement ownerUid) &&
            ownerUid.ValueKind == JsonValueKind.String
                ? ownerUid.GetString()
                : null;
    }

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
            new { kind = "city", query },
            JsonOptions.Default);
        MapDataQueryOptions normalized =
            MapDataQueryContract.NormalizeSearch(payload);
        if (normalized.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                "Recovered City filter unexpectedly failed contract gate: " +
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
                $"City filter proof observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }

    private static void AssertSet(
        MapDataStore store,
        MapDataQueryOptions query,
        long sampledAt,
        HashSet<string> expected,
        string label)
    {
        HashSet<string> actual = Keys(
            ReadAllRows(store, query, sampledAt));
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

    private static string RecordKey(JsonElement row) =>
        row.TryGetProperty(
                "recordKey",
                out JsonElement key) &&
        key.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(key.GetString())
            ? key.GetString()!
            : throw new InvalidDataException(
                "City filter proof row has no recordKey.");
}
