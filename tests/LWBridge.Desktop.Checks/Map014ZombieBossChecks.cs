using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// CONTROLLED / INERT checks for the passive Zombie Boss (MonsterInvasionBoss, special 10)
// category. Every native row below is a synthetic snapshot in the exact shape the probe
// emits; none of it is genuine game evidence and no game/account action is performed.
internal static class Map014ZombieBossChecks
{
    private const int Server = 2212;
    private const long Now = 1_800_000_000_000L;

    private static void Require(bool assertion, string reason)
    {
        if (!assertion) throw new InvalidOperationException("MAP014_ZOMBIE_BOSS: " + reason);
    }

    private static JsonObject Row(string uuid, int x, int y, bool eligible, int level, Action<JsonObject>? edit = null)
    {
        var row = new JsonObject
        {
            ["uuid"] = uuid, ["serverId"] = Server, ["worldId"] = 0, ["x"] = x, ["y"] = y,
            ["positionIndex"] = y * 1000 + x, ["monsterId"] = eligible ? 1030019 : 2022001, ["configId"] = eligible ? 1030019 : 2022001,
            ["monsterLevel"] = level, ["monsterNameKey"] = eligible ? "2901012" : "300601",
            ["isMonster"] = true, ["isBoss"] = eligible, ["distanceFromHome"] = 12.5,
            ["monsterProtectionEligible"] = eligible, ["createTime"] = Now - 60_000,
        };
        edit?.Invoke(row);
        return row;
    }

    private static JsonElement Snapshot(params JsonObject[] rows)
    {
        var array = new JsonArray();
        foreach (JsonObject row in rows) array.Add(row);
        return JsonSerializer.SerializeToElement(new JsonObject { ["monster_march_records"] = array });
    }

    private static MapScanExecutionRequest Request(params string[] kinds) =>
        new("zombie-run", Server, 0, 1000, 1000, kinds, 8);

    private static IReadOnlyList<MapStoredRecord> Normalize(JsonElement snapshot, params string[] kinds) =>
        CurrentClientMapBlockSource.NormalizeMonsterSnapshotForCheck(
            snapshot, Request(kinds), Enumerable.Range(0, 10_000).ToHashSet(), DateTimeOffset.FromUnixTimeMilliseconds(Now));

    internal static void Run()
    {
        // ---- contract: selectable, but never part of the implicit/default selection
        Require(MapScanContract.AllTypes.Contains("zombie_boss") &&
            !MapScanContract.RecoveredDefaultTypes.Contains("zombie_boss") &&
            MapScanContract.RecoveredDefaultTypes.Length == 8,
            "zombie_boss is public but not a recovered default kind");
        Require(MapScanContract.NormalizeStart(JsonSerializer.SerializeToElement(new { }))
                .SelectedTypes.SequenceEqual(MapScanContract.RecoveredDefaultTypes),
            "implicit scan selection is unchanged (eight kinds, no Zombie Boss)");
        Require(MapScanContract.NormalizeStart(JsonSerializer.SerializeToElement(
                new { selectedTypes = new[] { "monster", "zombie_boss", "zombie_boss", "bogus" } }))
                .SelectedTypes.SequenceEqual(["monster", "zombie_boss"]),
            "explicit selection keeps Zombie Boss once and drops unknown kinds");
        Require(MapAutoScanCommandService.NormalizeConfig(MapAutoScanConfig.Default with
                { SelectedTypes = ["zombie_boss", "truck"] }).SelectedTypes!.SequenceEqual(["zombie_boss", "truck"]) &&
            !MapAutoScanCommandService.DefaultSelectedTypes.Contains("zombie_boss"),
            "Auto may select Zombie Boss explicitly but never by default");

        // ---- routing of ONE shared snapshot
        JsonElement snapshot = Snapshot(
            Row("m1", 15, 25, false, 12),
            Row("z-active", 16, 26, true, 85, row =>
            {
                row["recommendPower"] = 34_500_000; row["refreshTime"] = Now + 3_600_000; row["belongUid"] = "9001";
                row["ownerName"] = "Finder"; row["allianceAbbr"] = "TST";
                row["monsterProtectionKnown"] = true; row["monsterProtectionActive"] = true;
                row["monsterProtectionEndTime"] = Now + 120_000;
            }),
            Row("z-unknown", 17, 27, true, 40, row =>
            {
                row["recommendPower"] = "16339000";
                // Zone Mobilization shield must never be adopted as Zombie Boss protection.
                row["zMBossShieldEndTime"] = Now + 999_000;
            }),
            Row("z-bare", 18, 28, true, 5),
            Row("doom", 19, 29, false, 90, row => row["source"] = "DataCenter.LWDoomsdayManager.theaterBosses"));

        IReadOnlyList<MapStoredRecord> monsterOnly = Normalize(snapshot, "monster");
        Require(monsterOnly.Select(row => row.RecordKey).OrderBy(key => key).SequenceEqual(["doom", "m1"]) &&
            monsterOnly.All(row => row.Kind == "monster"),
            "Monster-only scan keeps Doom Walker/ordinary rows and never receives special-10 rows");
        IReadOnlyList<MapStoredRecord> zombieOnly = Normalize(snapshot, "zombie_boss");
        Require(zombieOnly.Select(row => row.RecordKey).OrderBy(key => key).SequenceEqual(["z-active", "z-bare", "z-unknown"]) &&
            zombieOnly.All(row => row.Kind == "zombie_boss"),
            "Zombie-Boss-only scan stores exactly the special-10 rows as zombie_boss");
        IReadOnlyList<MapStoredRecord> both = Normalize(snapshot, "monster", "zombie_boss");
        Require(both.Count == 5 && both.Count(row => row.Kind == "zombie_boss") == 3 && both.Count(row => row.Kind == "monster") == 2 &&
            both.Select(row => row.RecordKey).Distinct().Count() == 5,
            "mixed scan partitions one snapshot with no duplicate and no second pass");
        Require(Normalize(snapshot, "city", "resource").Count == 0, "unselected kinds receive no monster rows");

        MapStoredRecord active = zombieOnly.Single(row => row.RecordKey == "z-active");
        Require(active.Level == 85 && active.Power == 34_500_000 && active.Name == "2901012" && active.ShieldEndTime == Now + 120_000 &&
            active.Distance == 12.5 && active.PointIndex == 26 * 1000 + 16,
            "level, template power, game-confirmed protection end, distance and coordinates are stored");
        using (JsonDocument data = JsonDocument.Parse(active.DataJson))
        {
            JsonElement root = data.RootElement;
            Require(root.GetProperty("kind").GetString() == "zombie_boss" && root.GetProperty("refreshTime").GetInt64() == Now + 3_600_000 &&
                root.GetProperty("recommendPower").GetInt64() == 34_500_000 && root.GetProperty("ownerName").GetString() == "Finder" &&
                root.GetProperty("monsterProtectionKnown").GetBoolean() && root.GetProperty("monsterProtectionActive").GetBoolean() &&
                root.GetProperty("x").GetInt32() == 16 && root.GetProperty("y").GetInt32() == 26,
                "remaining lifetime source (refreshTime), discoverer and protection facts are retained verbatim");
        }
        MapStoredRecord unknown = zombieOnly.Single(row => row.RecordKey == "z-unknown");
        Require(unknown.Power == 16_339_000 && unknown.ShieldEndTime is null,
            "numeric-string template power is accepted; ZM boss shield is never reused as protection");
        MapStoredRecord bare = zombieOnly.Single(row => row.RecordKey == "z-bare");
        using (JsonDocument bareData = JsonDocument.Parse(bare.DataJson))
            Require(bare.Power is null && bare.ShieldEndTime is null && !bareData.RootElement.TryGetProperty("refreshTime", out _) &&
                !bareData.RootElement.TryGetProperty("recommendPower", out _) && !bareData.RootElement.TryGetProperty("shieldEndTime", out _),
                "missing power/lifetime/protection stay unknown: no zero, no invented expiry");

        // ---- canonical store: counts, filters, sorts, protection state
        using var store = Map317.MapStore.CreateInMemory();
        foreach (MapStoredRecord row in zombieOnly.Concat(monsterOnly))
            store.UpsertRecord(new Map317.MapRecord(row.Kind, row.ServerId, row.RecordKey, row.PointIndex, row.Uuid, row.Name,
                row.AllianceName, row.Level, row.Quality, row.Power, row.Distance, row.ShieldEndTime, row.UpdatedAt, row.DataJson));
        IReadOnlyDictionary<string, int> counts = store.SummaryCounts(Server);
        Require(counts["zombie_boss"] == 3 && counts["monster"] == 2 && counts.Count == Map317.MapKinds.All.Length,
            "summary counts carry a dedicated zombie_boss entry without changing monster");
        Require(store.Search(new Map317.MapQuery("monster", Server), Now).Rows
                .All(row => row.GetProperty("kind").GetString() == "monster"),
            "Monster tab never lists Zombie Boss rows");
        IReadOnlyList<string> Keys(Map317.MapQuery query, long? at = null) =>
            store.Search(query, at ?? Now).Rows.Select(row => row.GetProperty("uuid").GetString()!).ToArray();
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, MinLevel: 40, MaxLevel: 85)).OrderBy(key => key)
                .SequenceEqual(["z-active", "z-unknown"]), "level range is inclusive");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, ProtectionState: "active")).SequenceEqual(["z-active"]),
            "active protection filter uses the game-confirmed future end");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, ProtectionState: "active"))
                .Count == 1 && store.Search(new Map317.MapQuery("zombie_boss", Server, ProtectionState: "active"), Now + 200_000).Total == 0 &&
                store.Search(new Map317.MapQuery("zombie_boss", Server, ProtectionState: "inactive"), Now + 200_000).Total == 1,
            "an expired confirmed protection becomes inactive over time");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, ProtectionState: "unknown")).OrderBy(key => key)
                .SequenceEqual(["z-bare", "z-unknown"]) &&
            Keys(new Map317.MapQuery("zombie_boss", Server, ProtectionState: "inactive")).Count == 0,
            "unconfirmed protection is unknown, never inactive");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, Sorts: [new Map317.MapSort("remaining", "asc")]))
                .SequenceEqual(["z-active", "z-bare", "z-unknown"]),
            "remaining-lifetime sort puts known deadlines first and unknown last");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, Sorts: [new Map317.MapSort("power", "desc")]))
                .SequenceEqual(["z-active", "z-unknown", "z-bare"]),
            "recommended-power sort keeps unknown power last");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, Sorts: [new Map317.MapSort("shield", "desc")]), Now)[0] == "z-active",
            "active protection end is sortable");
        Require(Keys(new Map317.MapQuery("zombie_boss", Server, Sorts: [new Map317.MapSort("level", "desc")])).First() == "z-active" &&
            Keys(new Map317.MapQuery("zombie_boss", Server, Sorts: [new Map317.MapSort("updatedAt", "desc")])).Count == 3,
            "level and updatedAt sorts are accepted");
        foreach (Map317.MapQuery bad in new[]
        {
            new Map317.MapQuery("monster", Server, ProtectionState: "active"),
            new Map317.MapQuery("zombie_boss", Server, ProtectionState: "bogus"),
            new Map317.MapQuery("zombie_boss", Server, Sorts: [new Map317.MapSort("quality", "asc")]),
        })
        {
            try { store.Search(bad, Now); throw new InvalidOperationException("MAP014_ZOMBIE_BOSS: unsupported query was accepted"); }
            catch (Map317.BridgeCommandException) { }
        }

        // ---- publication semantics: a Monster-only publication cannot erase Zombie Boss rows
        Require(store.SummaryCounts(Server)["zombie_boss"] == 3, "zombie rows persist independently of Monster rows");
        Console.WriteLine("MAP014_ZOMBIE_BOSS_CHECKS_OK");
    }
}
