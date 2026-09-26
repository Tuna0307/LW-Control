using System.Globalization;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class MapDataOptionsLiveVerifier
{
    internal sealed record Metrics(
        int TotalRows,
        int PositiveKindCount,
        int AllianceOptionCount,
        int NoAllianceCount,
        int ResourceNameOptionCount,
        int MonsterNameOptionCount,
        int DispatchLevelCount,
        int TreasureTypeOptionCount,
        int TruckRewardItemCount,
        int RailwayRewardItemCount);

    private sealed record TreasureOption(
        string Key,
        int SuppliesType,
        int TreasureType,
        string TreasureNameKey,
        int Count);

    private sealed record RewardOption(
        string Kind,
        string Key,
        string Name,
        string? IconPath);

    internal static Metrics ValidatePersisted(
        JsonElement options,
        MapDataStore store,
        int serverId,
        string expectedRunId,
        long beforeUnixMs,
        long afterUnixMs)
    {
        if (beforeUnixMs > afterUnixMs)
            throw new ArgumentOutOfRangeException(nameof(beforeUnixMs));
        if (options.GetProperty("serverId").GetInt32() != serverId)
            throw new InvalidDataException(
                "Persisted map_data_options changed server identity.");

        Dictionary<string, MapStoredRecord[]> byKind =
            MapScanContract.RecoveredDefaultTypes.ToDictionary(
                kind => kind,
                kind => store.ReadRecords(kind, serverId).ToArray(),
                StringComparer.Ordinal);
        int totalRows = byKind.Values.Sum(rows => rows.Length);

        JsonElement countsJson = options.GetProperty("counts");
        string[] countKeys = countsJson.EnumerateObject().Select(property => property.Name).ToArray();
        if (!countKeys.SequenceEqual(MapScanContract.RecoveredDefaultTypes))
            throw new InvalidDataException(
                "map_data_options counts must contain exactly the original eight keys in recovered order.");
        int positiveKindCount = 0;
        foreach (string kind in MapScanContract.RecoveredDefaultTypes)
        {
            int expected = byKind[kind].Length;
            int actual = countsJson.GetProperty(kind).GetInt32();
            if (actual != expected)
                throw new InvalidDataException(
                    $"map_data_options count mismatch for {kind}: actual={actual}, expected={expected}.");
            if (expected > 0) positiveKindCount++;
        }

        MapStoredRecord[] cities = byKind["city"];
        var expectedAlliances = cities
            .Where(row => !string.IsNullOrEmpty(row.AllianceName))
            .GroupBy(row => row.AllianceName!, StringComparer.Ordinal)
            .Select(group => (Name: group.Key, Count: group.Count()))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        int noAllianceCount = cities.Count(
            row => string.IsNullOrEmpty(row.AllianceName));
        JsonElement allianceJson = options.GetProperty("alliances");
        if (allianceJson.GetArrayLength() != expectedAlliances.Length)
            throw new InvalidDataException(
                "map_data_options alliance option count mismatch.");
        for (int index = 0; index < expectedAlliances.Length; index++)
        {
            JsonElement actual = allianceJson[index];
            if (!string.Equals(
                    actual.GetProperty("name").GetString(),
                    expectedAlliances[index].Name,
                    StringComparison.Ordinal) ||
                actual.GetProperty("count").GetInt32() !=
                    expectedAlliances[index].Count)
            {
                throw new InvalidDataException(
                    $"map_data_options alliance option mismatch at {index}.");
            }
        }
        if (options.GetProperty("noAllianceCount").GetInt32() != noAllianceCount)
            throw new InvalidDataException(
                "map_data_options noAllianceCount mismatch.");

        JsonElement names = options.GetProperty("names");
        int resourceNameCount = ValidateNameOptions(
            names.GetProperty("resource"),
            byKind["resource"],
            "resourceNameKey");
        int monsterNameCount = ValidateNameOptions(
            names.GetProperty("monster"),
            byKind["monster"],
            "monsterNameKey");
        if (names.TryGetProperty("zombie_boss", out _))
            throw new InvalidDataException(
                "map_data_options must not expose rebuild-only names.zombie_boss.");

        int[] expectedDispatchLevels = byKind["dispatch"]
            .Select(row => row.Level ?? 0)
            .Where(level => level >= 1)
            .Distinct()
            .OrderBy(level => level)
            .ToArray();
        ValidateIntArray(
            options.GetProperty("dispatchLevels"),
            expectedDispatchLevels,
            "dispatchLevels");

        if (options.TryGetProperty("monsterLevels", out _))
            throw new InvalidDataException(
                "map_data_options must not expose rebuild-only monsterLevels.");

        TreasureOption[] expectedTreasure =
            BuildTreasureOptions(byKind["treasure"]);
        JsonElement treasureJson = options.GetProperty("treasureTypes");
        if (treasureJson.GetArrayLength() != expectedTreasure.Length)
            throw new InvalidDataException(
                "map_data_options treasureTypes length mismatch.");
        for (int index = 0; index < expectedTreasure.Length; index++)
        {
            TreasureOption expected = expectedTreasure[index];
            JsonElement actual = treasureJson[index];
            if (!string.Equals(
                    actual.GetProperty("key").GetString(),
                    expected.Key,
                    StringComparison.Ordinal) ||
                actual.GetProperty("suppliesType").GetInt32() !=
                    expected.SuppliesType ||
                actual.GetProperty("treasureType").GetInt32() !=
                    expected.TreasureType ||
                !string.Equals(
                    actual.GetProperty("treasureNameKey").GetString(),
                    expected.TreasureNameKey,
                    StringComparison.Ordinal) ||
                actual.GetProperty("count").GetInt32() != expected.Count)
            {
                throw new InvalidDataException(
                    $"map_data_options treasureTypes mismatch at {index}.");
            }
        }

        RewardOption[] expectedRewardsBefore = BuildRewardOptions(
            byKind["truck"],
            byKind["railway"],
            beforeUnixMs);
        RewardOption[] expectedRewardsAfter = BuildRewardOptions(
            byKind["truck"],
            byKind["railway"],
            afterUnixMs);
        if (!expectedRewardsBefore.SequenceEqual(expectedRewardsAfter))
            throw new InvalidDataException(
                "Reward option eligibility crossed an arrival-time boundary during verification.");
        RewardOption[] expectedRewards = expectedRewardsAfter;

        JsonElement rewardItems = options.GetProperty("rewardItems");
        int truckRewardCount = ValidateRewardOptions(
            rewardItems.GetProperty("truck"),
            expectedRewards.Where(item => item.Kind == "truck").ToArray(),
            "truck");
        int railwayRewardCount = ValidateRewardOptions(
            rewardItems.GetProperty("railway"),
            expectedRewards.Where(item => item.Kind == "railway").ToArray(),
            "railway");

        JsonElement progress = options.GetProperty("scanProgress");
        if (progress.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                progress.GetProperty("id").GetString(),
                expectedRunId,
                StringComparison.Ordinal) ||
            progress.GetProperty("serverId").GetInt32() != serverId ||
            !string.Equals(
                progress.GetProperty("status").GetString(),
                "completed",
                StringComparison.OrdinalIgnoreCase) ||
            progress.GetProperty("createdAt").GetInt64() <= 0 ||
            progress.GetProperty("updatedAt").GetInt64() <= 0 ||
            progress.GetProperty("error").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException(
                "map_data_options persisted scanProgress did not preserve the completed run.");
        }

        return new Metrics(
            totalRows,
            positiveKindCount,
            expectedAlliances.Length,
            noAllianceCount,
            resourceNameCount,
            monsterNameCount,
            expectedDispatchLevels.Length,
            expectedTreasure.Length,
            truckRewardCount,
            railwayRewardCount);
    }

    private static int ValidateNameOptions(
        JsonElement actual,
        IReadOnlyList<MapStoredRecord> rows,
        string field)
    {
        var expected = rows
            .Select(row => StringField(row.DataJson, field))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .Select(group => (Key: group.Key, Count: group.Count()))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        if (actual.GetArrayLength() != expected.Length)
            throw new InvalidDataException(
                $"map_data_options {field} option count mismatch.");
        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    actual[index].GetProperty("key").GetString(),
                    expected[index].Key,
                    StringComparison.Ordinal) ||
                actual[index].GetProperty("count").GetInt32() !=
                    expected[index].Count)
            {
                throw new InvalidDataException(
                    $"map_data_options {field} option mismatch at {index}.");
            }
        }
        return expected.Length;
    }

    private static TreasureOption[] BuildTreasureOptions(
        IReadOnlyList<MapStoredRecord> rows) =>
        rows
            .Select(row =>
            {
                int supplies = IntField(row.DataJson, "suppliesType");
                int treasure = supplies > 0
                    ? 0
                    : IntField(row.DataJson, "treasureType");
                return new
                {
                    Supplies = supplies > 0 ? supplies : 0,
                    Treasure = treasure,
                    Name = StringField(row.DataJson, "treasureNameKey") ?? string.Empty,
                };
            })
            .Where(item => item.Supplies > 0 || item.Treasure > 0)
            .GroupBy(item => (item.Supplies, item.Treasure))
            .Select(group =>
            {
                string name = group
                    .Select(item => item.Name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .LastOrDefault() ?? string.Empty;
                int keyValue = group.Key.Supplies > 0
                    ? group.Key.Supplies
                    : group.Key.Treasure;
                string prefix = group.Key.Supplies > 0
                    ? "supplies:"
                    : "treasure:";
                return new TreasureOption(
                    prefix + keyValue.ToString(CultureInfo.InvariantCulture),
                    group.Key.Supplies,
                    group.Key.Treasure,
                    name,
                    group.Count());
            })
            .OrderBy(item => item.SuppliesType > 0 ? 1 : 0)
            .ThenBy(item => item.TreasureType)
            .ThenBy(item => item.SuppliesType)
            .ToArray();

    private static RewardOption[] BuildRewardOptions(
        IEnumerable<MapStoredRecord> trucks,
        IEnumerable<MapStoredRecord> railways,
        long nowUnixMs)
    {
        var result = new List<RewardOption>();
        foreach ((string kind, IEnumerable<MapStoredRecord> rows) in
            new[]
            {
                ("truck", trucks),
                ("railway", railways),
            })
        {
            foreach (MapStoredRecord row in rows)
            {
                long? arriveTs = LongField(row.DataJson, "arriveTs");
                if (arriveTs is not null && arriveTs <= nowUnixMs)
                    continue;
                using JsonDocument document = JsonDocument.Parse(row.DataJson);
                if (!document.RootElement.TryGetProperty(
                        "currentGoods",
                        out JsonElement goods) ||
                    goods.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (JsonElement good in goods.EnumerateArray())
                {
                    string? key = JsonString(good, "key");
                    string? name = JsonString(good, "name");
                    if (string.IsNullOrWhiteSpace(key) ||
                        string.IsNullOrWhiteSpace(name))
                        continue;
                    result.Add(new RewardOption(
                        kind,
                        key,
                        name,
                        JsonString(good, "iconPath")));
                }
            }
        }
        return result
            .Distinct()
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.IconPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static int ValidateRewardOptions(
        JsonElement actual,
        RewardOption[] expected,
        string kind)
    {
        RewardOption[] observed = actual
            .EnumerateArray()
            .Select(item => new RewardOption(
                kind,
                item.GetProperty("key").GetString() ?? string.Empty,
                item.GetProperty("name").GetString() ?? string.Empty,
                item.TryGetProperty("iconPath", out JsonElement icon) &&
                icon.ValueKind == JsonValueKind.String
                    ? icon.GetString()
                    : null))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.IconPath, StringComparer.Ordinal)
            .ToArray();
        RewardOption[] expectedForKind = expected
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.IconPath, StringComparer.Ordinal)
            .ToArray();
        if (!observed.SequenceEqual(expectedForKind))
            throw new InvalidDataException(
                $"map_data_options {kind} rewardItems mismatch.");
        return expectedForKind.Length;
    }

    private static void ValidateIntArray(
        JsonElement actual,
        IReadOnlyList<int> expected,
        string label)
    {
        int[] observed = actual
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        if (!observed.SequenceEqual(expected))
            throw new InvalidDataException(
                $"map_data_options {label} mismatch.");
    }

    private static string? StringField(
        string dataJson,
        string name)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        return JsonString(document.RootElement, name);
    }

    private static string? JsonString(
        JsonElement root,
        string name) =>
        root.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int IntField(
        string dataJson,
        string name)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        return document.RootElement.TryGetProperty(
                name,
                out JsonElement value) &&
            value.TryGetInt32(out int parsed)
                ? parsed
                : 0;
    }

    private static long? LongField(
        string dataJson,
        string name)
    {
        using JsonDocument document = JsonDocument.Parse(dataJson);
        if (!document.RootElement.TryGetProperty(
                name,
                out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.TryGetInt64(out long parsed))
            return parsed;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
            return parsed;
        return null;
    }
}
