using System.Globalization;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class DispatchGhostSortProofHelper
{
    internal sealed record Metrics(
        int CheckCount,
        int RowCount,
        int SpecialCount,
        int CompletionTimeValueCount);

    internal static Metrics Validate(
        MapDataStore store,
        int serverId,
        string kind)
    {
        if (kind is not ("dispatch" or "ghost"))
            throw new ArgumentOutOfRangeException(nameof(kind));

        IReadOnlyList<JsonElement> allRows = ReadAllRows(
            store,
            BuildQuery(kind, serverId, [new MapDataSort("updatedAt", "desc")]));
        if (allRows.Count < 2)
            throw new InvalidDataException(
                $"{kind} sort proof requires at least two live rows.");

        int specialCount = allRows.Count(IsSpecial);
        int completionTimeValueCount = allRows.Count(row =>
            PositiveNumber(row, "completionTime").HasValue);

        int checks = 0;
        foreach (string sortBy in new[] { "level", "quality", "completionTime", "updatedAt" })
        {
            foreach (string sortOrder in new[] { "asc", "desc" })
            {
                ValidateOrder(
                    store,
                    serverId,
                    kind,
                    allRows,
                    [new MapDataSort(sortBy, sortOrder)]);
                checks++;
            }
        }

        ValidateOrder(
            store,
            serverId,
            kind,
            allRows,
            [
                new MapDataSort("quality", "desc"),
                new MapDataSort("completionTime", "asc"),
                new MapDataSort("level", "desc"),
                new MapDataSort("updatedAt", "desc"),
            ]);
        checks++;

        return new Metrics(
            checks,
            allRows.Count,
            specialCount,
            completionTimeValueCount);
    }

    private static void ValidateOrder(
        MapDataStore store,
        int serverId,
        string kind,
        IReadOnlyList<JsonElement> allRows,
        IReadOnlyList<MapDataSort> sorts)
    {
        var expected = allRows.ToList();
        expected.Sort((left, right) => CompareRows(left, right, sorts));

        IReadOnlyList<JsonElement> actual = ReadAllRows(
            store,
            BuildQuery(kind, serverId, sorts));
        if (actual.Count != expected.Count)
            throw new InvalidDataException(
                $"{kind} sort row count mismatch for {DescribeSorts(sorts)}: " +
                $"actual={actual.Count}, expected={expected.Count}.");

        for (int index = 0; index < expected.Count; index++)
        {
            string expectedKey = RecordKey(expected[index]);
            string actualKey = RecordKey(actual[index]);
            if (!string.Equals(actualKey, expectedKey, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"{kind} sort mismatch for {DescribeSorts(sorts)} at {index}: " +
                    $"actual={actualKey}, expected={expectedKey}.");
        }
    }

    private static int CompareRows(
        JsonElement left,
        JsonElement right,
        IReadOnlyList<MapDataSort> sorts)
    {
        foreach (MapDataSort sort in sorts)
        {
            double? leftValue = SortValue(left, sort.SortBy);
            double? rightValue = SortValue(right, sort.SortBy);
            int comparison;
            if (!leftValue.HasValue && !rightValue.HasValue)
                comparison = 0;
            else if (!leftValue.HasValue)
                comparison = 1;
            else if (!rightValue.HasValue)
                comparison = -1;
            else
            {
                comparison = leftValue.Value.CompareTo(rightValue.Value);
                if (sort.SortOrder == "desc") comparison = -comparison;
            }

            if (comparison != 0) return comparison;
        }

        return StringComparer.Ordinal.Compare(RecordKey(left), RecordKey(right));
    }

    private static double? SortValue(JsonElement row, string sortBy) =>
        sortBy switch
        {
            "level" => OptionalNumber(row, "level"),
            "quality" => IsSpecial(row) ? 100d : OptionalNumber(row, "quality"),
            "completionTime" => PositiveNumber(row, "completionTime"),
            "updatedAt" => OptionalNumber(row, "updatedAt"),
            _ => throw new InvalidDataException(
                "Unexpected Dispatch/Ghost sort key: " + sortBy),
        };

    private static bool IsSpecial(JsonElement row) =>
        row.TryGetProperty("isSpecial", out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private static double? PositiveNumber(JsonElement row, string name)
    {
        double? value = OptionalNumber(row, name);
        return value is > 0d ? value : null;
    }

    private static double? OptionalNumber(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static string RecordKey(JsonElement row)
    {
        if (row.TryGetProperty("recordKey", out JsonElement explicitKey) &&
            explicitKey.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(explicitKey.GetString()))
            return explicitKey.GetString()!;

        if (row.TryGetProperty("pointId", out JsonElement pointId) &&
            pointId.TryGetInt64(out long parsedPointId))
            return parsedPointId.ToString(CultureInfo.InvariantCulture);

        throw new InvalidDataException(
            "Dispatch/Ghost sort proof row has neither recordKey nor pointId.");
    }

    private static string DescribeSorts(IReadOnlyList<MapDataSort> sorts) =>
        string.Join(",", sorts.Select(sort => $"{sort.SortBy}:{sort.SortOrder}"));

    private static MapDataQueryOptions BuildQuery(
        string kind,
        int serverId,
        IReadOnlyList<MapDataSort> sorts)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind,
            query = new
            {
                serverId,
                sorts = sorts.Select(sort => new
                {
                    sortBy = sort.SortBy,
                    sortOrder = sort.SortOrder,
                }).ToArray(),
            },
        }, JsonOptions.Default);
        MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(payload);
        if (query.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                $"Recovered {kind} sort unexpectedly failed contract gate: " +
                string.Join(",", query.UnsupportedFeatures));
        return query;
    }

    private static IReadOnlyList<JsonElement> ReadAllRows(
        MapDataStore store,
        MapDataQueryOptions query)
    {
        var rows = new List<JsonElement>();
        int page = 1;
        int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(query with { Page = page });
            if (expectedTotal < 0) expectedTotal = result.Total;
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }

        if (rows.Count != expectedTotal)
            throw new InvalidDataException(
                $"{query.Kind} sort proof observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }
}
