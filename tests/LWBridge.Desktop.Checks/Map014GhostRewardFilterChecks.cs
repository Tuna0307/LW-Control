using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// CONTROLLED / INERT CHECKS ONLY. These rows are synthetic fixtures written to a
// temporary SQLite file; they are NOT genuine game evidence. They exercise the
// real Map317 command normalization and the canonical MapStore SQL for the Ghost
// reward filter: AND (default) vs explicit "any", minimum-amount boundaries,
// unknown-reward rows, per-server isolation, combination with the existing Ghost
// filters, and pagination totals. No game, launcher or Lua is touched, and no
// Ghost plunder is prepared or executed.
internal static class Map014GhostRewardFilterChecks
{
    private const string Diamond = "reward:1:100";
    private const string Speedup = "reward:1:200";
    private const string Gem = "reward:1:300";
    private const long Past = 1_700_000_000_000L;
    private const long Future = 7_000_000_000_000L;

    private static void Require(bool valid, string why)
    {
        if (!valid) throw new InvalidOperationException("MAP014_GHOST_REWARD: " + why);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static object Reward(string key, string name, long count) =>
        new { key, name, iconPath = "game/icon/" + name, count, rewardType = 1, itemId = int.Parse(key.Split(':')[2]) };

    private static void Insert(Map317.MapStore store, string uuid, int server, int quality, object fields)
    {
        var data = System.Text.Json.JsonSerializer.SerializeToNode(fields)!.AsObject();
        data["uuid"] = uuid;
        data["kind"] = "ghost";
        store.UpsertRecord(new Map317.MapRecord("ghost", server, uuid, null, uuid, null, null, 5, quality, null, null, null,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), data.ToJsonString()));
    }

    internal static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "lwbridge-map014-ghost-reward-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string db = Path.Combine(directory, "data.db");
        try
        {
            using (var store = new Map317.MapStore(db))
            {
                Insert(store, "g-a", 2213, 4, new { ownerUid = "1001", ownerName = "Alice", completionTime = Past, taskExpireTime = Future,
                    serverId = 2213, ownerServer = 2210, srcServerId = 2210,
                    rewards = new[] { Reward(Diamond, "Diamond", 500), Reward(Speedup, "Speedup", 10) } });
                Insert(store, "g-b", 2213, 4, new { ownerUid = "1002", ownerName = "Bob", completionTime = Future, taskExpireTime = Future,
                    rewards = new[] { Reward(Diamond, "Diamond", 499) } });
                Insert(store, "g-c", 2213, 3, new { ownerUid = "1003", ownerName = "Cyd", completionTime = Future, taskExpireTime = Future,
                    rewards = new[] { Reward(Speedup, "Speedup", 10), Reward(Gem, "Gem", 1) } });
                // Native config scalar only (the probe's raw base_reward_show): NOT an authoritative reward list.
                Insert(store, "g-unknown-config", 2213, 4, new { ownerUid = "1004", completionTime = Past, rewardConfig = "raw-config-id-77" });
                Insert(store, "g-unknown-none", 2213, 4, new { ownerUid = "1005", completionTime = Past });
                // An empty array is also not an authoritative statement about rewards.
                Insert(store, "g-unknown-empty", 2213, 4, new { ownerUid = "1006", completionTime = Past, rewards = Array.Empty<object>() });
                // Corrupt reward entries must never match nor crash the query.
                Insert(store, "g-corrupt", 2213, 4, new { ownerUid = "1007", completionTime = Past,
                    rewards = new object[] { "abc", 5, new { key = Diamond }, new { key = Diamond, count = "500" },
                        new { key = Diamond, count = 0 }, new { key = Diamond, count = -9 }, new { count = 900 } } });
                // The same identity listed twice is summed (600 >= 500).
                Insert(store, "g-dup", 2213, 2, new { ownerUid = "1008", ownerName = "Dee", completionTime = Past,
                    rewards = new[] { Reward(Diamond, "Diamond", 300), Reward(Diamond, "Diamond", 300) } });
                // Other server with an enormous reward: must never leak into 2213.
                Insert(store, "g-other-server", 2212, 4, new { ownerUid = "1009", completionTime = Past,
                    rewards = new[] { Reward(Diamond, "Diamond", 9999), Reward("reward:1:999", "OtherServerOnly", 5) } });
                for (int i = 1; i <= 130; ++i)
                    Insert(store, "page-" + i.ToString("D3"), 2216, 4, new { ownerUid = "2" + i, completionTime = Past,
                        rewards = new[] { Reward(Diamond, "Diamond", i) } });
            }

            int currentServer = 2213;
            var world = new Map317.MapProviderContext(true, true, 2213, "live", 0, 1000, 1000);
            var source = new Map317.MapProviderAdapter(
                _ => ValueTask.FromResult(world), _ => ValueTask.FromResult(world),
                (_, _) => throw new NotSupportedException("controlled: no scan"), _ => ValueTask.CompletedTask);
            var actions = new Map317.MapActionProviderAdapter
            {
                GetCurrentServerId = _ => ValueTask.FromResult(currentServer),
                GotoServer = (_, _) => throw new NotSupportedException(),
                GotoWorldCoordinate = (_, _, _, _) => throw new NotSupportedException(),
                GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
                InspectTreasureStates = (_, _, _) => ValueTask.FromResult(new Map317.TreasureInspectionResult("", "", [])),
                GetTreasureClaimStatus = _ => throw new NotSupportedException(),
                ClaimTreasures = (_, _) => throw new NotSupportedException(),
            };
            using var service = new Map317CommandService(db, source, actions,
                statusReader: _ => Task.FromResult(new CurrentClientMapStatusContext(true, 2213, 2213, [], [], 0, 1000, 1000, 0, 0)));

            async Task<JsonElement> Search(object query, string kind = "ghost") =>
                Json(await service.InvokeAsync("map_search", Json(new { kind, query }), CancellationToken.None)
                     ?? throw new InvalidDataException("null search"));
            async Task<int> Count(object query, string kind = "ghost") => (await Search(query, kind)).GetProperty("total").GetInt32();
            async Task<HashSet<string>> Uuids(object query)
            {
                JsonElement result = await Search(query);
                return result.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("uuid").GetString()!).ToHashSet(StringComparer.Ordinal);
            }
            async Task Reject(object query, string why, string kind = "ghost")
            {
                try { await Search(query, kind); }
                catch (Map317.BridgeCommandException error) when (error.Code == "INVALID_MAP_QUERY") { return; }
                catch (BridgeCommandException error) when (error.Code == "INVALID_MAP_QUERY") { return; }
                throw new InvalidOperationException("MAP014_GHOST_REWARD: expected INVALID_MAP_QUERY: " + why);
            }
            object F(string key, long? min = null) => min is null ? new { key } : new { key, minCount = min.Value };
            bool SameSet(HashSet<string> actual, params string[] expected) => actual.SetEquals(expected);

            // Baseline: every stored Ghost row on the selected server only.
            Require(await Count(new { serverId = 2213 }) == 8, "unfiltered 2213 Ghost baseline");
            Require(await Count(new { serverId = 2212 }) == 1, "unfiltered 2212 Ghost baseline");

            // Single reward identity; the default match is AND and needs a positive amount.
            var diamond = await Uuids(new { serverId = 2213, rewardFilters = new[] { F(Diamond) } });
            Require(SameSet(diamond, "g-a", "g-b", "g-dup"),
                "diamond selection must match only rows with an authoritative positive diamond amount");

            // Quantity threshold boundary (inclusive).
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 499) } }), "g-a", "g-b", "g-dup"), "499 includes exactly 499");
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 500) } }), "g-a", "g-dup"), "500 inclusive; 499 excluded");
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 501) } }), "g-dup"), "501 excludes the exact-500 task");
            Require(await Count(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 601) } }) == 0, "above every amount");

            // AND is the default; explicit "all" is identical.
            var both = new[] { F(Diamond), F(Speedup) };
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = both }), "g-a"), "default AND requires every selected reward");
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = both, rewardMatch = "all" }), "g-a"), "explicit all == default");
            Require(await Count(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 500), F(Speedup, 11) } }) == 0, "AND with one unmet threshold");
            Require(await Count(new { serverId = 2213, rewardFilters = new[] { F(Diamond), F("reward:1:404") } }) == 0, "AND with a reward no task has");
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = new[] { F(Speedup), F(Gem) } }), "g-c"), "AND speedup+gem");

            // Explicit, labelled OR ("any of"). Thresholds stay per reward.
            Require(SameSet(await Uuids(new { serverId = 2213, rewardFilters = both, rewardMatch = "any" }), "g-a", "g-b", "g-c", "g-dup"),
                "any of diamond/speedup");
            Require(SameSet(await Uuids(new { serverId = 2213, rewardMatch = "any",
                    rewardFilters = new[] { F(Diamond, 500), F(Gem, 1) } }), "g-a", "g-c", "g-dup"),
                "any with per-reward thresholds: diamond>=500 or gem>=1");
            Require(SameSet(await Uuids(new { serverId = 2213, rewardMatch = "any",
                    rewardFilters = new[] { F(Diamond, 501), F(Gem, 2) } }), "g-dup"), "any threshold boundary");

            // Unknown / corrupt rows never satisfy any reward filter (AND or any), and
            // a raw config scalar is not treated as an authoritative reward list.
            foreach (string mode in new[] { "all", "any" })
            {
                var everything = await Uuids(new { serverId = 2213, rewardMatch = mode,
                    rewardFilters = new[] { F(Diamond), F(Speedup), F(Gem), F("reward:1:404") } });
                Require(!everything.Overlaps(new[] { "g-unknown-config", "g-unknown-none", "g-unknown-empty", "g-corrupt" }),
                    "unknown/corrupt Ghost rows leaked into a reward filter in mode " + mode);
            }
            Require(await Count(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 1) } }) == 3, "corrupt entries (no count / string count / zero / negative / no key) add nothing");

            // Server isolation.
            var otherServer = await Uuids(new { serverId = 2212, rewardFilters = new[] { F(Diamond, 9999) } });
            Require(SameSet(otherServer, "g-other-server"), "selected server 2212 owns its own huge reward");
            Require(!(await Uuids(new { serverId = 2213, rewardMatch = "any", rewardFilters = new[] { F(Diamond), F("reward:1:999") } })).Contains("g-other-server"),
                "another saved server's task leaked into 2213");
            Require(await Count(new { serverId = 2212, rewardFilters = new[] { F("reward:1:999") } }) == 1 &&
                    await Count(new { serverId = 2213, rewardFilters = new[] { F("reward:1:999") } }) == 0, "reward identity existing only on 2212");

            // Combination with the existing Ghost filters.
            Require(SameSet(await Uuids(new { serverId = 2213, completionStatus = "completed", rewardFilters = new[] { F(Diamond) } }), "g-a", "g-dup"), "completed + diamond");
            Require(SameSet(await Uuids(new { serverId = 2213, completionStatus = "pending", rewardFilters = new[] { F(Diamond) } }), "g-b"), "pending + diamond");
            Require(SameSet(await Uuids(new { serverId = 2213, quality = "ssr", rewardFilters = new[] { F(Diamond) } }), "g-a", "g-b"), "quality + diamond");
            Require(SameSet(await Uuids(new { serverId = 2213, keyword = "alice", rewardFilters = new[] { F(Diamond) } }), "g-a"), "owner keyword + diamond");
            Require(SameSet(await Uuids(new { serverId = 2213, keyword = "alice", rewardMatch = "any", rewardFilters = both }), "g-a"), "owner keyword + any");
            Require(await Count(new { serverId = 2213, keyword = "bob", rewardFilters = both }) == 0, "owner keyword + AND");

            // Returned rows retain the exact authoritative reward objects.
            JsonElement aRows = (await Search(new { serverId = 2213, rewardFilters = new[] { F(Speedup, 10) }, keyword = "alice" })).GetProperty("rows");
            JsonElement aRewards = aRows[0].GetProperty("rewards");
            Require(aRewards.GetArrayLength() == 2 && aRewards[0].GetProperty("key").GetString() == Diamond &&
                    aRewards[0].GetProperty("name").GetString() == "Diamond" && aRewards[0].GetProperty("count").GetInt64() == 500 &&
                    aRows[0].GetProperty("serverId").GetInt32() == 2213 && aRows[0].GetProperty("ownerServer").GetInt32() == 2210,
                "row presentation fields (rewards, current server, owner server) must survive the query");

            // Pagination: totals are counted before the page boundary.
            var page1 = await Search(new { serverId = 2216, page = 1, pageSize = 50, rewardFilters = new[] { F(Diamond, 31) } });
            var page2 = await Search(new { serverId = 2216, page = 2, pageSize = 50, rewardFilters = new[] { F(Diamond, 31) } });
            var page3 = await Search(new { serverId = 2216, page = 3, pageSize = 50, rewardFilters = new[] { F(Diamond, 31) } });
            Require(page1.GetProperty("total").GetInt32() == 100 && page2.GetProperty("total").GetInt32() == 100 &&
                    page3.GetProperty("total").GetInt32() == 100, "100 tasks have diamond >= 31 across every page");
            Require(page1.GetProperty("rows").GetArrayLength() == 50 && page2.GetProperty("rows").GetArrayLength() == 50 &&
                    page3.GetProperty("rows").GetArrayLength() == 0, "page sizes 50/50/0");
            var paged = page1.GetProperty("rows").EnumerateArray().Concat(page2.GetProperty("rows").EnumerateArray())
                .Select(row => row.GetProperty("uuid").GetString()!).ToHashSet(StringComparer.Ordinal);
            Require(paged.Count == 100 && paged.All(uuid => int.Parse(uuid["page-".Length..]) >= 31), "pages are disjoint and filtered before LIMIT");
            Require(await Count(new { serverId = 2216 }) == 130, "unfiltered total unaffected");
            Require(await Count(new { serverId = 2216, rewardMatch = "any", rewardFilters = new[] { F(Diamond, 130), F("reward:1:404") } }) == 1, "single boundary row paged");

            // Option list: only authoritative identities of THIS server, with names and tasks.
            JsonElement options = Json((await service.InvokeAsync("map_data_options", Json(new { serverId = 2213 }), CancellationToken.None))!);
            JsonElement[] ghostItems = options.GetProperty("rewardItems").GetProperty("ghost").EnumerateArray().ToArray();
            Require(ghostItems.Select(item => item.GetProperty("key").GetString()).OrderBy(key => key, StringComparer.Ordinal)
                    .SequenceEqual(new[] { Diamond, Speedup, Gem }.Order(StringComparer.Ordinal)),
                "ghost reward options expose exactly the authoritative identities on the selected server");
            JsonElement diamondOption = ghostItems.Single(item => item.GetProperty("key").GetString() == Diamond);
            Require(diamondOption.GetProperty("name").GetString() == "Diamond" &&
                    diamondOption.GetProperty("iconPath").GetString() == "game/icon/Diamond" &&
                    diamondOption.GetProperty("taskCount").GetInt32() == 3 && diamondOption.GetProperty("maxCount").GetInt64() == 500,
                "option carries the stored real name/icon/task count/max amount");
            JsonElement otherOptions = Json((await service.InvokeAsync("map_data_options", Json(new { serverId = 2212 }), CancellationToken.None))!);
            Require(otherOptions.GetProperty("rewardItems").GetProperty("ghost").EnumerateArray()
                        .Select(item => item.GetProperty("key").GetString()).Order(StringComparer.Ordinal).SequenceEqual(new[] { "reward:1:999", Diamond }.Order(StringComparer.Ordinal)),
                "server 2212 options are isolated from 2213");
            Require(options.GetProperty("rewardItems").GetProperty("truck").GetArrayLength() == 0 &&
                    options.GetProperty("rewardItems").GetProperty("railway").GetArrayLength() == 0, "Truck/Railway options unchanged");

            // Validation negatives (never widened into an unfiltered query).
            await Reject(new { serverId = 2213, rewardFilters = new[] { F(Diamond) } }, "reward filter on Truck", "truck");
            await Reject(new { serverId = 2213, rewardFilters = new[] { F(Diamond) } }, "reward filter on Dispatch", "dispatch");
            await Reject(new { serverId = 2213, rewardMatch = "any" }, "match mode on City", "city");
            await Reject(new { serverId = 2213, rewardFilters = Array.Empty<object>() }, "empty selection");
            await Reject(new { serverId = 2213, rewardFilters = "diamond" }, "non-array selection");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { new { key = "" } } }, "blank key");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { new { key = "   " } } }, "whitespace key");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { new { key = new string('k', 161) } } }, "overlong key");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { "reward:1:100" } }, "non-object item");
            await Reject(new { serverId = 2213, rewardFilters = new[] { F(Diamond), F(Diamond, 5) } }, "duplicate identity");
            await Reject(new { serverId = 2213, rewardFilters = new[] { F(Diamond, 0) } }, "minimum zero");
            await Reject(new { serverId = 2213, rewardFilters = new[] { F(Diamond, -3) } }, "negative minimum");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { new { key = Diamond, minCount = 1.5 } } }, "fractional minimum");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { new { key = Diamond, minCount = "5" } } }, "string minimum");
            await Reject(new { serverId = 2213, rewardFilters = new object[] { new { key = Diamond, minCount = 9_007_199_254_740_992L } } }, "unsafe minimum");
            await Reject(new { serverId = 2213, rewardFilters = Enumerable.Range(0, 25).Select(i => F("reward:1:" + i)).ToArray() }, "25 selections");
            await Reject(new { serverId = 2213, rewardMatch = "xor", rewardFilters = new[] { F(Diamond) } }, "unknown match mode");
            await Reject(new { serverId = 2213, rewardMatch = 7, rewardFilters = new[] { F(Diamond) } }, "numeric match mode");
            Require(await Count(new { serverId = 2213, rewardFilters = Enumerable.Range(0, 24).Select(i => F("reward:1:" + i)).ToArray(), rewardMatch = "any" }) == 0,
                "24 selections are permitted");
            // A neutral match mode with no selection is a harmless no-op; null selection is neutral.
            Require(await Count(new { serverId = 2213, rewardMatch = "any" }) == 8 &&
                    await Count(new { serverId = 2213, rewardFilters = (object?)null }) == 8, "no selection leaves the Ghost query unfiltered");

            // The older indexed store has no `$.rewards` predicate and must fail closed.
            MapDataQueryOptions legacy = MapDataQueryContract.NormalizeSearch(Json(new
            {
                kind = "ghost", query = new { serverId = 2213, rewardFilters = new[] { F(Diamond) } },
            }));
            Require(legacy.UnsupportedFeatures.Contains("rewardFilters"), "legacy indexed Map store must not silently ignore a reward filter");
            Console.WriteLine("MAP014_GHOST_REWARD_FILTER_CONTROLLED_INERT_OK");
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
