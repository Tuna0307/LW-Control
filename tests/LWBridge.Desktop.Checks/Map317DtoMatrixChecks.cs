using System.Security.Cryptography;
using System.Text.Json;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

internal static class Map317DtoMatrixChecks
{
    private const long Now = 1_800_000_400_000;
    private const string FixtureName = "map317-recovery-dto-matrix.json";
    private const string FixtureSha256 = "700E465605275122A66888435A1814F496245BC3F84221F19D8325579A17B314";
    private const string ReferenceExeSha256 = "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783";
    private const string MapPanelSha256 = "CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089";

    internal static void Run()
    {
        string fixturePath = Path.Combine(FindRepoRoot(), "tests", "LWBridge.Desktop.Checks", "Fixtures", FixtureName);
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        string fixtureHash = Convert.ToHexString(SHA256.HashData(fixtureBytes));
        Require(string.Equals(fixtureHash, FixtureSha256, StringComparison.OrdinalIgnoreCase),
            $"fixture hash must stay pinned at {FixtureSha256}, got {fixtureHash}");

        using JsonDocument fixture = JsonDocument.Parse(fixtureBytes);
        JsonElement rootFixture = fixture.RootElement;
        ValidateAuthority(rootFixture.GetProperty("authority"));
        JsonElement kindRules = rootFixture.GetProperty("kindRules");
        JsonElement fixtureRows = rootFixture.GetProperty("rows");
        ValidateKindSourceLocators(kindRules);

        string root = Path.Combine(Path.GetTempPath(), "lwb317-map-dto-matrix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var a = new Map317.MapStore(Path.Combine(root, "profile-A.db"));
            using var b = new Map317.MapStore(Path.Combine(root, "profile-B.db"));
            SeedFixtureRows(fixtureRows, a, b);

            foreach (string kind in Map317.MapKinds.All)
            {
                Require(kindRules.TryGetProperty(kind, out JsonElement rule),
                    $"fixture metadata must cover recovered kind {kind}");
                Require(!string.IsNullOrWhiteSpace(rule.GetProperty("defaultRule").GetString()),
                    $"fixture metadata must name the recovered default rule for {kind}");

                Map317.MapSearchResult normal = a.Search(new Map317.MapQuery(kind, 317), Now);
                Map317.MapSearchResult normalizedDefaults = a.Search(new Map317.MapQuery(kind, 317, Page: 0, PageSize: 0), Now);
                string[] expected = ExpectedDefaultKeys(fixtureRows, kind);
                Require(normal.Rows.Select(RowKey).SequenceEqual(expected),
                    $"{kind} default query must preserve recovered updatedAt-desc/record-key tie ordering");
                Require(normalizedDefaults.Total == normal.Total &&
                        normalizedDefaults.Rows.Select(RowKey).SequenceEqual(expected),
                    $"{kind} page/pageSize zero must normalize to recovered page 1 / size 50 defaults");

                string probeField = rule.GetProperty("probeField").GetString()!;
                JsonElement missing = FindRow(normal, rule.GetProperty("missingKey").GetString()!);
                JsonElement explicitNull = FindRow(normal, rule.GetProperty("nullKey").GetString()!);
                Require(!missing.TryGetProperty(probeField, out _),
                    $"{kind} missing {probeField} must remain absent after persistence/query");
                Require(explicitNull.TryGetProperty(probeField, out JsonElement nullValue) && nullValue.ValueKind == JsonValueKind.Null,
                    $"{kind} explicit-null {probeField} must remain JSON null after persistence/query");

                string isolationKey = "isolation-" + kind;
                JsonElement a317 = FindRow(a.Search(new Map317.MapQuery(kind, 317), Now), isolationKey);
                JsonElement a318 = FindRow(a.Search(new Map317.MapQuery(kind, 318), Now), isolationKey);
                JsonElement b317 = FindRow(b.Search(new Map317.MapQuery(kind, 317), Now), isolationKey);
                Require(a317.GetProperty("scopeTag").GetString() == "A317" && a317.GetProperty("serverId").GetInt32() == 317 &&
                        a318.GetProperty("scopeTag").GetString() == "A318" && a318.GetProperty("serverId").GetInt32() == 318 &&
                        b317.GetProperty("scopeTag").GetString() == "B317" && b317.GetProperty("serverId").GetInt32() == 317,
                    $"{kind} same-key rows must stay isolated by server and profile database");
            }

            VerifyCity(a);
            VerifyResourceAndMonster(a);
            VerifyTruckAndRailway(a);
            VerifyDispatchAndGhost(a);
            VerifyTreasure(a);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void ValidateAuthority(JsonElement authority)
    {
        Require(authority.GetProperty("referenceExecutableSha256").GetString() == ReferenceExeSha256,
            "fixture authority must pin the required 0.3.17 executable hash");

        JsonElement storage = authority.GetProperty("storageEvidence");
        Require(storage.GetProperty("path").GetString() == "evidence/lwbridge-0.3.17/map/storage-static-contract.json" &&
                storage.GetProperty("findingId").GetString() == "LWB317-RE-MAP-001-STORAGE-STATIC" &&
                storage.GetProperty("evidenceState").GetString() == "EXACT_BYTES_STATIC_CONTRACT" &&
                storage.GetProperty("schemaRaw").GetString() == "0xD56547" &&
                storage.GetProperty("schemaRva").GetString() == "0xD57747",
            "fixture storage authority must remain tied to the recovered schema locator");

        JsonElement review = authority.GetProperty("storageReview");
        Require(review.GetProperty("path").GetString() == "docs/reviews/2026-09-30-LWB317-RE-MAP-003-storage-query-export.md" &&
                review.GetProperty("searchService").GetString() == "0x3E10E7-0x3E6952" &&
                review.GetProperty("contractState").GetString() == "EXACT_CONTRACT",
            "fixture query authority must remain tied to the recovered search-service locator");

        JsonElement frontend = authority.GetProperty("frontendEvidence");
        Require(frontend.GetProperty("path").GetString() == "evidence/lwbridge-0.3.17/map/frontend-host-contract.json" &&
                frontend.GetProperty("mapPanelPath").GetString() == "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js" &&
                string.Equals(frontend.GetProperty("mapPanelSha256").GetString(), MapPanelSha256, StringComparison.OrdinalIgnoreCase) &&
                frontend.GetProperty("staticInventoryPath").GetString() == "evidence/lwbridge-0.3.17/ui/pages/map-data/static-inventory.json" &&
                frontend.GetProperty("scanTypesByte").GetString() == "0x160F",
            "fixture kind authority must remain tied to the recovered eight-type frontend source");
    }

    private static void ValidateKindSourceLocators(JsonElement kindRules)
    {
        const string contractPath = "evidence/lwbridge-0.3.17/map/frontend-host-contract.json";
        const string reviewPath = "docs/reviews/2026-09-30-LWB317-RE-MAP-003-storage-query-export.md";
        foreach (string kind in Map317.MapKinds.All)
        {
            if (!kindRules.TryGetProperty(kind, out JsonElement rule) ||
                !rule.TryGetProperty("sourceLocator", out JsonElement locator))
                throw new InvalidOperationException(
                    $"Map317 DTO matrix check failed: {kind} fixture rule must name recovered source-locator metadata");
            Require(locator.GetProperty("path").GetString() == contractPath &&
                    locator.GetProperty("reviewPath").GetString() == reviewPath &&
                    locator.GetProperty("function").GetString() == "0x3E10E7-0x3E6952" &&
                    !string.IsNullOrWhiteSpace(locator.GetProperty("anchor").GetString()) &&
                    !string.IsNullOrWhiteSpace(locator.GetProperty("reviewLines").GetString()) &&
                    !string.IsNullOrWhiteSpace(locator.GetProperty("claim").GetString()),
                $"{kind} source locator must stay tied to the recovered frontend-host/search review evidence");
        }
    }

    private static void VerifyCity(Map317.MapStore store)
    {
        Map317.MapSearchResult noAlliance = store.Search(new Map317.MapQuery("city", 317, WithoutAlliance: true), Now);
        Require(HasRow(noAlliance, "city-missing") && HasRow(noAlliance, "city-null") && !HasRow(noAlliance, "city-value"),
            "City missing/null alliance defaults must match recovered no-alliance SQL semantics");

        Map317.MapSearchResult cityTie = store.Search(new Map317.MapQuery(
            "city", 317, Sorts: [new Map317.MapSort("updatedAt", "desc")]), Now);
        Require(cityTie.Rows.Take(2).Select(RowKey).SequenceEqual(new[] { "city-missing", "city-null" }),
            "City equal updatedAt values must retain recovered record-key ASC tie ordering");

        Map317.MapOptionSet options = store.ReadOptions(317, nowUnixMilliseconds: Now);
        Require(options.Alliances.Count == 1 &&
                options.Alliances[0].Name == "QA" && options.Alliances[0].Count == 1 &&
                options.NoAllianceCount == 3,
            "City option grouping must omit missing/null alliance names while retaining their recovered no-alliance count");
    }

    private static void VerifyResourceAndMonster(Map317.MapStore store)
    {
        Map317.MapOptionSet options = store.ReadOptions(317, nowUnixMilliseconds: Now);
        Require(options.Names["resource"].Count == 1 &&
                options.Names["resource"][0].Key == "resource.iron" && options.Names["resource"][0].Count == 1,
            "Resource missing/null resourceNameKey values must be omitted from recovered options grouping");
        Require(options.Names["monster"].Count == 1 &&
                options.Names["monster"][0].Key == "monster.doom" && options.Names["monster"][0].Count == 1,
            "Monster missing/null monsterNameKey values must be omitted from recovered options grouping");

        Map317.MapSearchResult resource = store.Search(new Map317.MapQuery(
            "resource", 317, ResourceNameKey: "resource.iron"), Now);
        Require(resource.Rows.Select(RowKey).SequenceEqual(new[] { "resource-value" }),
            "Resource name filter must not match missing/null JSON values");

        Map317.MapSearchResult monster = store.Search(new Map317.MapQuery(
            "monster", 317, MonsterNameKey: "monster.doom"), Now);
        Require(monster.Rows.Select(RowKey).SequenceEqual(new[] { "monster-value" }),
            "Monster name filter must not match missing/null JSON values");

        Map317.MapSearchResult monsterDistance = store.Search(new Map317.MapQuery(
            "monster", 317, Sorts: [new Map317.MapSort("distance", "desc")]), Now);
        Require(monsterDistance.Rows.Take(3).Select(RowKey).SequenceEqual(
                new[] { "monster-value", "monster-null", "monster-missing" }),
            "Monster distance must retain recovered forced-ascending ordering even for desc requests");
    }

    private static void VerifyTruckAndRailway(Map317.MapStore store)
    {
        Map317.MapSearchResult plunderableTruck = store.Search(new Map317.MapQuery(
            "truck", 317, PlunderableOnly: true), Now);
        Require(HasRow(plunderableTruck, "truck-missing") && HasRow(plunderableTruck, "truck-null"),
            "Truck missing/null remainingLootCount must use recovered maxLootCount-robTimes fallback");

        Map317.MapSearchResult ordinaryUr = store.Search(new Map317.MapQuery(
            "truck", 317, Quality: "ur"), Now);
        Require(HasRow(ordinaryUr, "truck-missing") && HasRow(ordinaryUr, "truck-null") && !HasRow(ordinaryUr, "truck-special"),
            "Truck missing/null isSpecialURQuality must default to ordinary UR while special UR stays excluded");

        Map317.MapSearchResult reindeer = store.Search(new Map317.MapQuery(
            "truck", 317, ReindeerOnly: true), Now);
        Require(HasRow(reindeer, "truck-special") && !HasRow(reindeer, "truck-missing") && !HasRow(reindeer, "truck-null"),
            "Truck special/reindeer filter must require the explicit special indicator");

        Map317.MapSearchResult truckOrder = store.Search(new Map317.MapQuery(
            "truck", 317, Sorts: [new Map317.MapSort("quality", "desc")]), Now);
        Require(RowKey(truckOrder.Rows[0]) == "truck-special",
            "special UR Truck rows must retain recovered quality priority ordering");

        Map317.MapOptionSet options = store.ReadOptions(317, nowUnixMilliseconds: Now);
        Require(options.RewardItems["truck"].Count(item => item.GetProperty("key").GetString() == "item:iron") == 1,
            "Truck duplicate reward items must retain recovered grouped option behavior");
        Require(options.RewardItems["railway"].Count == 1 &&
                options.RewardItems["railway"][0].GetProperty("key").GetString() == "item:stone",
            "Railway missing/null currentGoods must contribute no reward item while populated rows remain grouped");

        Map317.MapSearchResult railwayStone = store.Search(new Map317.MapQuery(
            "railway", 317, ItemKey: "item:stone"), Now);
        Require(railwayStone.Rows.Select(RowKey).SequenceEqual(new[] { "railway-value" }),
            "Railway item filter must ignore missing/null currentGoods arrays");
        Require(HasRow(store.Search(new Map317.MapQuery("railway", 317), Now), "railway-missing") &&
                HasRow(store.Search(new Map317.MapQuery("railway", 317), Now), "railway-null"),
            "Railway missing/null arriveTs must retain recovered visible-by-default handling");
    }

    private static void VerifyDispatchAndGhost(Map317.MapStore store)
    {
        Map317.MapSearchResult dispatchPending = store.Search(new Map317.MapQuery(
            "dispatch", 317, CompletionStatus: "pending"), Now);
        Require(HasRow(dispatchPending, "dispatch-missing") && HasRow(dispatchPending, "dispatch-null") &&
                !HasRow(dispatchPending, "dispatch-default"),
            "Dispatch missing/null completionTime must retain recovered pending default");

        Map317.MapSearchResult dispatchCompleted = store.Search(new Map317.MapQuery(
            "dispatch", 317, CompletionStatus: "completed"), Now);
        Require(HasRow(dispatchCompleted, "dispatch-default"),
            "Dispatch positive past completionTime must retain recovered completed classification");

        Map317.MapSearchResult dispatchPlunderable = store.Search(new Map317.MapQuery(
            "dispatch", 317, PlunderableOnly: true), Now);
        Require(HasRow(dispatchPlunderable, "dispatch-default"),
            "Dispatch plunderability must retain recovered completionTime fallback and missing limit defaults");

        Map317.MapOptionSet options = store.ReadOptions(317, nowUnixMilliseconds: Now);
        Require(options.DispatchLevels.SequenceEqual(new[] { 5, 6, 7 }),
            "Dispatch option grouping must retain distinct non-null levels in recovered ascending order");

        Map317.MapSearchResult ghostPending = store.Search(new Map317.MapQuery(
            "ghost", 317, CompletionStatus: "pending"), Now);
        Require(HasRow(ghostPending, "ghost-missing") && HasRow(ghostPending, "ghost-null") &&
                !HasRow(ghostPending, "ghost-completed"),
            "Ghost missing/null completionTime must retain recovered pending default");

        Map317.MapSearchResult ghostCompleted = store.Search(new Map317.MapQuery(
            "ghost", 317, CompletionStatus: "completed"), Now);
        Require(HasRow(ghostCompleted, "ghost-completed"),
            "Ghost positive past completionTime must retain recovered completed classification");
    }

    private static void VerifyTreasure(Map317.MapStore store)
    {
        Map317.MapSearchResult allianceTreasure = store.Search(new Map317.MapQuery(
            "treasure", 317, ViewerAllianceId: "QA"), Now);
        Require(HasRow(allianceTreasure, "treasure-missing") && HasRow(allianceTreasure, "treasure-null") &&
                HasRow(allianceTreasure, "treasure-local") && !HasRow(allianceTreasure, "treasure-foreign"),
            "Treasure missing/null type fields must default to non-radar while foreign radar rows remain filtered");

        Map317.MapSearchResult allTreasure = store.Search(new Map317.MapQuery(
            "treasure", 317, IncludeForeignRadarTreasures: true, ViewerAllianceId: "QA"), Now);
        Require(HasRow(allTreasure, "treasure-foreign"),
            "explicit foreign-radar inclusion must restore the otherwise filtered Treasure DTO");

        IReadOnlyList<JsonElement> treasureTypes = store.ReadOptions(317, nowUnixMilliseconds: Now).TreasureTypes;
        JsonElement radar = treasureTypes.Single(item => item.GetProperty("key").GetString() == "treasure:1");
        JsonElement supplies = treasureTypes.Single(item => item.GetProperty("key").GetString() == "supplies:2");
        Require(radar.GetProperty("count").GetInt32() == 2 &&
                supplies.GetProperty("count").GetInt32() == 1 &&
                supplies.GetProperty("treasureType").GetInt32() == 0 &&
                !treasureTypes.Any(item => item.GetProperty("key").GetString() is "treasure:0" or "supplies:0"),
            "Treasure missing/null type fields must coalesce to zero and stay out of option groups; supplies must override treasure type");
    }

    private static void SeedFixtureRows(JsonElement rows, Map317.MapStore profileA, Map317.MapStore profileB)
    {
        foreach (JsonElement row in rows.EnumerateArray())
        {
            var record = new Map317.MapRecord(
                row.GetProperty("kind").GetString()!,
                row.GetProperty("serverId").GetInt32(),
                row.GetProperty("recordKey").GetString()!,
                OptionalInt32(row, "pointIndex"),
                OptionalString(row, "uuid"),
                OptionalString(row, "name"),
                OptionalString(row, "allianceName"),
                OptionalInt32(row, "level"),
                OptionalInt32(row, "quality"),
                OptionalInt64(row, "power"),
                OptionalDouble(row, "distance"),
                OptionalInt64(row, "shieldEndTime"),
                row.GetProperty("updatedAt").GetInt64(),
                row.GetProperty("data").GetRawText());
            (row.GetProperty("profile").GetString() == "A" ? profileA : profileB).UpsertRecord(record);
        }
    }

    private static string[] ExpectedDefaultKeys(JsonElement rows, string kind)
    {
        var expected = new List<(long UpdatedAt, string Key)>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.GetProperty("profile").GetString() != "A" ||
                row.GetProperty("serverId").GetInt32() != 317 ||
                row.GetProperty("kind").GetString() != kind)
                continue;
            string key = row.GetProperty("recordKey").GetString()!;
            if (kind == "treasure" && key == "treasure-foreign") continue;
            expected.Add((row.GetProperty("updatedAt").GetInt64(), key));
        }
        return expected.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Key).ToArray();
    }

    private static JsonElement FindRow(Map317.MapSearchResult result, string recordKey) =>
        result.Rows.Single(row => RowKey(row) == recordKey);

    private static bool HasRow(Map317.MapSearchResult result, string recordKey) =>
        result.Rows.Any(row => RowKey(row) == recordKey);

    private static string RowKey(JsonElement row) => row.GetProperty("recordKey").GetString()!;

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static int? OptionalInt32(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : null;

    private static long? OptionalInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt64()
            : null;

    private static double? OptionalDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : null;

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "tests", "LWBridge.Desktop.Checks", "Map317DtoMatrixChecks.cs")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Map317 DTO matrix check could not locate repository root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Map317 DTO matrix check failed: " + message);
    }
}
