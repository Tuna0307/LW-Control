using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MonsterFilterProofHelper
{
    internal sealed record Metrics(
        int RowCount,
        int CheckCount,
        int MonsterNameCount,
        int MaxLevel,
        int MaxLevelCount,
        int NameAndMaxCount,
        int KeywordIdentityCount,
        int KeywordPercentLiteralCount,
        int KeywordUnderscoreLiteralCount,
        int KeywordBackslashLiteralCount,
        int ResolvedNameKeyCount);

    internal static Metrics Validate(
        MapDataStore store,
        int serverId,
        long sampledAt)
    {
        MapStoredRecord[] records =
            store.ReadRecords("monster", serverId).ToArray();
        if (records.Length == 0)
            throw new InvalidDataException(
                "Monster filter proof requires a positive live population.");

        IReadOnlyList<JsonElement> baseRows =
            ReadAllRows(store, BuildQuery(serverId), sampledAt);
        if (baseRows.Count != records.Length ||
            !Keys(baseRows).SetEquals(
                records.Select(RawIdentity)))
            throw new InvalidDataException(
                "Monster raw-record/query populations differ before filter proof.");

        string sampleNameKey = records
            .Select(record => MonsterNameKey(record.DataJson))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "Monster filter proof has no monsterNameKey.");

        int checks = 0;
        HashSet<string> nameExpected = records
            .Where(record => string.Equals(
                MonsterNameKey(record.DataJson),
                sampleNameKey,
                StringComparison.Ordinal))
            .Select(RawIdentity)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("monsterNameKey", sampleNameKey)),
            sampledAt,
            nameExpected,
            "Monster monsterNameKey");
        checks++;

        int[] levels = records
            .Select(record => record.Level ?? 0)
            .Where(level => level > 0)
            .OrderBy(level => level)
            .ToArray();
        if (levels.Length != records.Length)
            throw new InvalidDataException(
                "Monster filter proof requires a positive level on every live row.");
        int medianLevel = levels[levels.Length / 2];
        int maxLevel = ((medianLevel + 4) / 5) * 5;
        if (maxLevel <= 0)
            throw new InvalidDataException(
                "Monster filter proof could not choose a visible maximum-level bucket.");

        HashSet<string> maxExpected = records
            .Where(record => record.Level is > 0 &&
                record.Level <= maxLevel)
            .Select(RawIdentity)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(serverId, ("maxLevel", maxLevel)),
            sampledAt,
            maxExpected,
            $"Monster maxLevel={maxLevel}");
        checks++;
        HashSet<string> nameAndMaxExpected = records
            .Where(record =>
                string.Equals(
                    MonsterNameKey(record.DataJson),
                    sampleNameKey,
                    StringComparison.Ordinal) &&
                record.Level is > 0 &&
                record.Level <= maxLevel)
            .Select(RawIdentity)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(
                serverId,
                ("monsterNameKey", sampleNameKey),
                ("maxLevel", maxLevel)),
            sampledAt,
            nameAndMaxExpected,
            "Monster name+maxLevel");
        checks++;

        string identityKeyword = records
            .Select(record => record.Uuid)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "Monster filter proof has no live UUID for keyword verification.");
        HashSet<string> identityExpected =
            DirectKeywordExpected(records, identityKeyword);
        if (identityExpected.Count == 0)
            throw new InvalidDataException(
                "Monster identity keyword did not match a live record.");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", identityKeyword)),
            sampledAt,
            identityExpected,
            "Monster keyword identity");
        checks++;

        HashSet<string> percentExpected =
            DirectKeywordExpected(records, "%");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", "%")),
            sampledAt,
            percentExpected,
            "Monster keyword literal percent");
        checks++;

        HashSet<string> underscoreExpected =
            DirectKeywordExpected(records, "_");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", "_")),
            sampledAt,
            underscoreExpected,
            "Monster keyword literal underscore");
        checks++;

        HashSet<string> backslashExpected =
            DirectKeywordExpected(records, "\\");
        AssertSet(
            store,
            BuildQuery(serverId, ("keyword", "\\")),
            sampledAt,
            backslashExpected,
            "Monster keyword literal backslash");
        checks++;

        string[] resolvedKeys = records
            .Select(record => MonsterNameKey(record.DataJson))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(2)
            .Select(group => group.Key)
            .ToArray();
        if (resolvedKeys.Length == 0)
            throw new InvalidDataException(
                "Monster filter proof has no resolved name-key candidates.");

        const string localizedProbe = "lwbridge-localized-name-probe-zzzz";
        HashSet<string> directProbe =
            DirectKeywordExpected(records, localizedProbe);
        if (directProbe.Count != 0)
            throw new InvalidDataException(
                "Monster localized-name probe unexpectedly matched direct fields.");
        HashSet<string> resolvedExpected = records
            .Where(record => resolvedKeys.Contains(
                MonsterNameKey(record.DataJson),
                StringComparer.Ordinal))
            .Select(RawIdentity)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> resolvedActual = Keys(
            ReadAllRowsWithResolvedNames(
                store,
                BuildQuery(
                    serverId,
                    ("keyword", localizedProbe)),
                resolvedKeys));
        if (!resolvedActual.SetEquals(resolvedExpected))
            throw new InvalidDataException(
                "Monster resolved-name-key keyword mismatch.");
        checks++;

        return new Metrics(
            records.Length,
            checks,
            nameExpected.Count,
            maxLevel,
            maxExpected.Count,
            nameAndMaxExpected.Count,
            identityExpected.Count,
            percentExpected.Count,
            underscoreExpected.Count,
            backslashExpected.Count,
            resolvedExpected.Count);
    }

    private static HashSet<string> DirectKeywordExpected(
        IEnumerable<MapStoredRecord> records,
        string keyword) =>
        records
            .Where(record =>
                ContainsLiteral(record.Name, keyword) ||
                ContainsLiteral(record.Uuid, keyword) ||
                ContainsLiteral(
                    MonsterNameKey(record.DataJson),
                    keyword))
            .Select(RawIdentity)
            .ToHashSet(StringComparer.Ordinal);

    private static bool ContainsLiteral(
        string? value,
        string keyword) =>
        value?.IndexOf(
            keyword,
            StringComparison.OrdinalIgnoreCase) >= 0;

    private static string? MonsterNameKey(string dataJson)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        return document.RootElement.TryGetProperty(
                "monsterNameKey",
                out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
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
            new { kind = "monster", query },
            JsonOptions.Default);
        MapDataQueryOptions normalized =
            MapDataQueryContract.NormalizeSearch(payload);
        if (normalized.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                "Monster filter unexpectedly failed contract gate: " +
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
                $"Monster filter proof observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }

    private static IReadOnlyList<JsonElement> ReadAllRowsWithResolvedNames(
        MapDataStore store,
        MapDataQueryOptions query,
        IReadOnlyList<string> resolvedNameKeys)
    {
        var rows = new List<JsonElement>();
        int page = 1;
        int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result =
                store.SearchIndexedWithMonsterNameKeys(
                    query with { Page = page },
                    resolvedNameKeys);
            if (expectedTotal < 0) expectedTotal = result.Total;
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal ||
                result.Rows.Count == 0)
                break;
            page++;
        }
        if (rows.Count != expectedTotal)
            throw new InvalidDataException(
                $"Monster resolved-name proof observed {rows.Count}/{expectedTotal} rows.");
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
        rows.Select(RowIdentity)
            .ToHashSet(StringComparer.Ordinal);

    private static string RawIdentity(MapStoredRecord record) =>
        !string.IsNullOrWhiteSpace(record.Uuid)
            ? record.Uuid!
            : throw new InvalidDataException(
                "Monster filter proof raw row has no UUID.");

    private static string RowIdentity(JsonElement row)
    {
        if (row.TryGetProperty(
                "uuid",
                out JsonElement uuid) &&
            uuid.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(uuid.GetString()))
            return uuid.GetString()!;

        if (row.TryGetProperty(
                "recordKey",
                out JsonElement key) &&
            key.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(key.GetString()))
            return key.GetString()!;

        throw new InvalidDataException(
            "Monster filter proof row has no UUID/recordKey.");
    }
}
