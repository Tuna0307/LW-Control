using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// CONTROLLED / INERT: a synthetic native ghost_task snapshot (shape produced by
// ghost_aoi_records in current_live_resource_probe.lua) is normalized by the real
// PrepareGhostRecords and stored/queried through the real MapStore. NOT genuine
// game evidence; no game, Lua runtime or process is touched.
internal static class Map014GhostRewardFlowChecks
{
    private static void Require(bool valid, string why)
    {
        if (!valid) throw new InvalidOperationException("MAP014_GHOST_REWARD_FLOW: " + why);
    }

    internal static void Run()
    {
        const string rewardsJson = """
            [{"key":"reward:7:710005","name":"Controlled Item","iconPath":"icon/a","count":20,"rewardType":7,"itemId":710005},
             {"key":"reward:27:5001","name":"Controlled Resource Item","count":200,"rewardType":27,"itemId":5001}]
            """;
        string Row(string uuid, int pointId, int x, int y, bool withRewards) => $$"""
            {"kind":"ghost_task","pointType":29,"runtimeClass":"Game.GhostreconPointInfo","pointId":{{pointId}},"serverId":2213,"worldId":7,
             "x":{{x}},"y":{{y}},"uuid":"{{uuid}}","cfgId":40101,"level":3,"quality":4,"isSpecial":false,"ownerUid":"9001","ownerServer":2210,"srcServerId":2210,
             "completionTime":1700000000000,"protectTime":0,"stealListCount":0,"stealMaxTimes":3,
             "rewardConfig":"710005;7;20|5001;27;200"{{(withRewards ? ",\"rewards\":" + rewardsJson : string.Empty)}}}
            """;
        using JsonDocument rows = JsonDocument.Parse("[" + Row("g-known", 1001, 32, 21, true) + "," + Row("g-unknown", 1002, 33, 22, false) + "]");
        var request = new MapScanExecutionRequest("controlled", 2213, 7, 1000, 1000, ["ghost"], 1);
        var requested = new HashSet<int> { (21 / 10) * 100 + (32 / 10), (22 / 10) * 100 + (33 / 10) };
        MethodInfo prepare = typeof(CurrentClientMapBlockSource).GetMethod("PrepareGhostRecords",
            BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException("PrepareGhostRecords not found");
        var prepared = ((IEnumerable)prepare.Invoke(null, [rows.RootElement.Clone(), request, requested, DateTimeOffset.UtcNow])!).Cast<object>().ToList();
        Require(prepared.Count == 2, "both ghost rows prepared");
        MapStoredRecord Record(object item) => (MapStoredRecord)item.GetType().GetProperty("Record")!.GetValue(item)!;
        JsonObject known = JsonNode.Parse(Record(prepared[0]).DataJson)!.AsObject();
        JsonObject unknown = JsonNode.Parse(Record(prepared[1]).DataJson)!.AsObject();
        Require(known["kind"]!.GetValue<string>() == "ghost" && known["rewards"] is JsonArray { Count: 2 },
            "authoritative native rewards flow unchanged into data_json");
        Require(known["rewards"]![0]!["key"]!.GetValue<string>() == "reward:7:710005" && known["rewards"]![0]!["count"]!.GetValue<long>() == 20 &&
                known["rewards"]![1]!["name"]!.GetValue<string>() == "Controlled Resource Item" &&
                known["rewardConfig"]!.GetValue<string>() == "710005;7;20|5001;27;200", "reward fields and raw config preserved");
        Require(!unknown.ContainsKey("rewards") && unknown["rewardConfig"] is not null,
            "a row without a native reward list must stay unknown (no synthesized rewards)");

        string directory = Path.Combine(Path.GetTempPath(), "lwbridge-map014-ghost-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var store = new Map317.MapStore(Path.Combine(directory, "data.db")))
            {
                foreach (object item in prepared)
                {
                    MapStoredRecord record = Record(item);
                    store.UpsertRecord(new Map317.MapRecord(record.Kind, record.ServerId, record.RecordKey, record.PointIndex,
                        record.Uuid, record.Name, record.AllianceName, record.Level, record.Quality, record.Power,
                        record.Distance, record.ShieldEndTime, record.UpdatedAt, record.DataJson));
                }
                Map317.MapQuery Query(params Map317.MapRewardFilter[] filters) =>
                    new("ghost", 2213, RewardFilters: filters);
                var hit = store.Search(Query(new Map317.MapRewardFilter("reward:7:710005", 20)));
                Require(hit.Total == 1 && hit.Rows[0].GetProperty("uuid").GetString() == "g-known",
                    "stored native rewards satisfy a reward filter; unknown row excluded");
                Require(store.Search(Query(new Map317.MapRewardFilter("reward:7:710005", 21))).Total == 0, "threshold boundary after storage");
                Require(store.Search(Query(new Map317.MapRewardFilter("reward:27:5001"), new Map317.MapRewardFilter("reward:7:710005"))).Total == 1, "AND across native reward types");
                var options = store.ReadOptions(2213);
                Require(options.RewardItems["ghost"].Count == 2, "options list the two native identities");
            }
            Console.WriteLine("MAP014_GHOST_REWARD_NATIVE_ROW_FLOW_CONTROLLED_INERT_OK");
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
