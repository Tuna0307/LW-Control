using System.Globalization;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class DispatchGhostFilterProofHelper
{
    internal sealed record Metrics(
        int RowCount,
        int CheckCount,
        int QualityNCount,
        int QualityRCount,
        int QualitySrCount,
        int QualitySsrCount,
        int QualityUrCount,
        int SpecialCount,
        int PendingCount,
        int CompletedCount,
        int SampleLevel,
        int MinLevelCount,
        int MaxLevelCount,
        int ExactLevelCount,
        int? PlunderableCount);

    internal static Metrics Validate(
        MapDataStore store,
        int serverId,
        string kind,
        long sampledAt)
    {
        if (kind is not ("dispatch" or "ghost"))
            throw new ArgumentOutOfRangeException(nameof(kind));

        MapDataQueryOptions baseQuery = BuildQuery(kind, serverId);
        IReadOnlyList<JsonElement> rows =
            ReadAllRows(store, baseQuery, sampledAt);
        if (rows.Count == 0)
            throw new InvalidDataException(
                $"{kind} filter proof requires a positive live population.");

        var allKeys = Keys(rows);
        int checks = 0;
        int QualityExpected(string selector, JsonElement row)
        {
            int? quality = OptionalInt(row, "quality");
            return selector switch
            {
                "n" => quality == 1 ? 1 : 0,
                "r" => quality == 2 ? 1 : 0,
                "sr" => quality == 3 ? 1 : 0,
                "ssr" => quality == 4 ? 1 : 0,
                "ur" => quality >= 5 ? 1 : 0,
                _ => 0,
            };
        }

        var qualityCounts = new Dictionary<string, int>(
            StringComparer.Ordinal);
        foreach (string selector in
            new[] { "n", "r", "sr", "ssr", "ur" })
        {
            HashSet<string> expected = rows
                .Where(row => QualityExpected(selector, row) != 0)
                .Select(RecordKey)
                .ToHashSet(StringComparer.Ordinal);
            AssertSet(
                store,
                BuildQuery(kind, serverId, ("quality", selector)),
                sampledAt,
                expected,
                $"{kind} quality={selector}");
            qualityCounts[selector] = expected.Count;
            checks++;
        }

        HashSet<string> qualityUnion = rows
            .Where(row => OptionalInt(row, "quality") is > 0)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        if (!qualityUnion.SetEquals(allKeys))
            throw new InvalidDataException(
                $"{kind} live rows are not fully classified by the recovered quality family.");
        HashSet<string> specialExpected = rows
            .Where(IsSpecial)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(kind, serverId, ("specialOnly", true)),
            sampledAt,
            specialExpected,
            $"{kind} specialOnly");
        checks++;

        HashSet<string> completedExpected = rows
            .Where(row => IsCompleted(row, sampledAt))
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> pendingExpected = rows
            .Where(row => !IsCompleted(row, sampledAt))
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        AssertSet(
            store,
            BuildQuery(
                kind, serverId, ("completionStatus", "completed")),
            sampledAt,
            completedExpected,
            $"{kind} completionStatus=completed");
        checks++;
        AssertSet(
            store,
            BuildQuery(
                kind, serverId, ("completionStatus", "pending")),
            sampledAt,
            pendingExpected,
            $"{kind} completionStatus=pending");
        checks++;
        HashSet<string> timeUnion =
            new(completedExpected, StringComparer.Ordinal);
        timeUnion.UnionWith(pendingExpected);
        if (!timeUnion.SetEquals(allKeys) ||
            completedExpected.Overlaps(pendingExpected))
            throw new InvalidDataException(
                $"{kind} completion filters did not partition the live rows.");

        int[] levels = rows
            .Select(row => OptionalInt(row, "level") ?? 0)
            .Where(level => level > 0)
            .OrderBy(level => level)
            .ToArray();
        if (levels.Length != rows.Count)
            throw new InvalidDataException(
                $"{kind} filter proof requires a positive level on every live row.");
        int sampleLevel = levels[levels.Length / 2];

        HashSet<string> minExpected = rows
            .Where(row => OptionalInt(row, "level") >= sampleLevel)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> maxExpected = rows
            .Where(row => OptionalInt(row, "level") <= sampleLevel)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> exactExpected = rows
            .Where(row => OptionalInt(row, "level") == sampleLevel)
            .Select(RecordKey)
            .ToHashSet(StringComparer.Ordinal);

        AssertSet(
            store,
            BuildQuery(kind, serverId, ("minLevel", sampleLevel)),
            sampledAt,
            minExpected,
            $"{kind} minLevel={sampleLevel}");
        checks++;
        AssertSet(
            store,
            BuildQuery(kind, serverId, ("maxLevel", sampleLevel)),
            sampledAt,
            maxExpected,
            $"{kind} maxLevel={sampleLevel}");
        checks++;
        AssertSet(
            store,
            BuildQuery(
                kind,
                serverId,
                ("minLevel", sampleLevel),
                ("maxLevel", sampleLevel)),
            sampledAt,
            exactExpected,
            $"{kind} exact level={sampleLevel}");
        checks++;
        int? plunderableCount = null;
        if (kind == "dispatch")
        {
            HashSet<string> plunderableExpected = rows
                .Where(row => IsDispatchPlunderable(row, sampledAt))
                .Select(RecordKey)
                .ToHashSet(StringComparer.Ordinal);
            AssertSet(
                store,
                BuildQuery(
                    kind, serverId, ("plunderableOnly", true)),
                sampledAt,
                plunderableExpected,
                "dispatch plunderableOnly");
            plunderableCount = plunderableExpected.Count;
            checks++;
        }

        return new Metrics(
            rows.Count,
            checks,
            qualityCounts["n"],
            qualityCounts["r"],
            qualityCounts["sr"],
            qualityCounts["ssr"],
            qualityCounts["ur"],
            specialExpected.Count,
            pendingExpected.Count,
            completedExpected.Count,
            sampleLevel,
            minExpected.Count,
            maxExpected.Count,
            exactExpected.Count,
            plunderableCount);
    }

    private static bool IsCompleted(JsonElement row, long sampledAt)
    {
        long completion = OptionalLong(row, "completionTime") ?? 0;
        return completion > 0 && completion <= sampledAt;
    }

    private static bool IsDispatchPlunderable(
        JsonElement row,
        long sampledAt)
    {
        long completion = OptionalLong(row, "completionTime") ?? 0;
        long plunderAt =
            OptionalLong(row, "plunderAt") ?? completion;
        long taskExpire =
            OptionalLong(row, "taskExpireTime") ?? 0;
        int maxSteal =
            OptionalInt(row, "maxStealCount") ?? 0;
        int stolen =
            OptionalInt(row, "stolenCount") ?? 0;
        return completion > 0 &&
            plunderAt > 0 &&
            (taskExpire <= 0 || taskExpire > sampledAt) &&
            (maxSteal <= 0 || stolen < maxSteal);
    }

    private static bool IsSpecial(JsonElement row) =>
        row.TryGetProperty("isSpecial", out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

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

    private static long? OptionalLong(
        JsonElement row,
        string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value))
            return null;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out long numeric))
            return numeric;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long text))
            return text;
        return null;
    }
    private static MapDataQueryOptions BuildQuery(
        string kind,
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
            new { kind, query },
            JsonOptions.Default);
        MapDataQueryOptions normalized =
            MapDataQueryContract.NormalizeSearch(payload);
        if (normalized.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                $"Recovered {kind} filter unexpectedly failed contract gate: " +
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
                $"{query.Kind} filter proof observed " +
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

        throw new InvalidDataException(
            "Dispatch/Ghost filter proof row has neither " +
            "recordKey nor pointId.");
    }
}
