using System.Text.Json;
using LWBridge.Desktop;
using Map317 = LWBridge.Map317;

namespace LWBridge.Desktop.Checks;

// Distinguishing tests exercise actual Map317 command normalization, canonical
// SQLite predicates, paging, native action serialization and status ownership.
// The external game is replaced by an inert, explicitly controlled provider.
internal static class Map014OwnerFixChecks
{
    private static void Require(bool valid, string why)
    {
        if (!valid) throw new InvalidOperationException("MAP014_OWNER_FIX: " + why);
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, JsonOptions.Default);

    private static async Task<JsonElement> Invoke(Map317CommandService service, string command, object payload) =>
        Json(await service.InvokeAsync(command, Json(payload), CancellationToken.None)
             ?? throw new InvalidDataException("Null Map command result"));

    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); }
        catch (Map317.BridgeCommandException error) when (error.Code == code) { return; }
        catch (BridgeCommandException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("MAP014 expected rejection " + code);
    }

    private static void Insert(Map317.MapStore store, string kind, string key, int server,
        int? level, long? power, object fields)
    {
        var data = JsonSerializer.SerializeToNode(fields)!.AsObject();
        data["uuid"] = key;
        store.UpsertRecord(new Map317.MapRecord(kind, server, key, null,
            kind == "truck" ? key : null, null, null, level, 1, power, null, null,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), data.ToJsonString()));
    }

    internal static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "lwbridge-map014-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string db = Path.Combine(directory, "data.db");
        try
        {
            using (var store = new Map317.MapStore(db))
            {
                Insert(store, "resource", "fullBlack", 2213, 6, null,
                    new { resourceDetailKnown = true, resourceRemainingAmount = 8000, resourceFullAmount = 8000, blackTileKnown = true, isBlackTile = true });
                Insert(store, "resource", "partialNormal", 2213, 7, null,
                    new { resourceDetailKnown = true, resourceRemainingAmount = 200, resourceFullAmount = 8000, blackTileKnown = true, isBlackTile = false });
                Insert(store, "resource", "unknownBlack", 2213, 7, null,
                    new { gatherOccupied = false, blackTileKnown = true, isBlackTile = true });
                Insert(store, "resource", "unknownTerrain", 2213, 8, null,
                    new { gatherOccupied = true, resourceDetailKnown = true, resourceRemainingAmount = 400, resourceFullAmount = 8000 });
                Insert(store, "resource", "otherProfileLikeServer", 2212, 8, null,
                    new { resourceDetailKnown = true, resourceRemainingAmount = 8000, resourceFullAmount = 8000 });
                const string exactUid = "9912345678901234567";
                Insert(store, "city", "cityForDispatchOwner", 2213, 30, null,
                    new { ownerUid = exactUid, ownerName = "Real Stored City Name", allianceName = "ACTUAL" });
                Insert(store, "city", "wrongServerCityOwner", 2212, 30, null,
                    new { ownerUid = exactUid, ownerName = "Wrong Server Identity", allianceName = "OTHER" });
                Insert(store, "dispatch", "matchingTask", 2213, 6, null,
                    new { ownerUid = exactUid, ownerName = (string?)null, completionTime = 7000000000000L });
                Insert(store, "dispatch", "unknownOwnerTask", 2213, 6, null,
                    new { ownerUid = "990000000000000001", completionTime = 7000000000000L });
                // Scattered Supplies is not a speculative ninth scanner kind:
                // native WorldSuppliesPoint pointType=27 and LWIceSupplies
                // config type=1 are already stored as Treasure suppliesType=1.
                Insert(store, "treasure", "seasonIceSupply", 2213, null, null,
                    new { pointType = 27, runtimeClass = "WorldSuppliesPoint", suppliesType = 1,
                        treasureType = 0, configId = 301 });
                Insert(store, "treasure", "otherSeasonSupply", 2213, null, null,
                    new { pointType = 27, runtimeClass = "WorldSuppliesPoint", suppliesType = 3,
                        treasureType = 0, configId = 302 });
                Insert(store, "treasure", "ordinaryTreasure", 2213, null, null,
                    new { pointType = 21, runtimeClass = "TreasurePointInfo", suppliesType = 0,
                        treasureType = 19 });
                Insert(store, "treasure", "foreignServerIceSupply", 2212, null, null,
                    new { pointType = 27, runtimeClass = "WorldSuppliesPoint", suppliesType = 1,
                        treasureType = 0, configId = 301 });
                Insert(store, "truck", "bothAtLimit", 2213, null, 25_000_000,
                    new { currentGoods = new[] { new { key = "diamonds:100", count = 1 }, new { key = "speedup:5m", count = 3 } } });
                Insert(store, "truck", "diamondOnly", 2213, null, 24_999_999,
                    new { currentGoods = new[] { new { key = "diamonds:100", count = 8 } } });
                Insert(store, "truck", "aboveLimit", 2213, null, 25_000_001,
                    new { currentGoods = new[] { new { key = "diamonds:100", count = 1 }, new { key = "speedup:5m", count = 1 } } });
                Insert(store, "truck", "unknownPower", 2213, null, null,
                    new { currentGoods = new[] { new { key = "diamonds:100", count = 1 }, new { key = "speedup:5m", count = 1 } } });
                // 255 distinct source identities: test real SQL count, paging,
                // inclusive power and multi-item predicates under larger selection.
                for (int i = 0; i < 255; ++i)
                    Insert(store, "truck", "selection-" + i.ToString("D3"), 2214, null,
                        i % 2 == 0 ? 25_000_000 : 25_000_001,
                        new { currentGoods = new[] { new { key = "diamonds:100", count = 1 },
                                                      new { key = "speedup:5m", count = 5 } } });
            }

            int currentServer = 2212;
            var calls = new List<string>();
            bool rejectSwitch = false;
            bool wrongDestination = false;
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool holdNavigation = false;
            CurrentClientMapStatusContext? currentStatus = new(true, 2212, 2212, [], [2197, 2208], 0, 1000, 1000, 0, 0);
            var authoritativeWorld = new Map317.MapProviderContext(true, true, 2213, "live", 0, 1000, 1000);
            var source = new Map317.MapProviderAdapter(
                _ => ValueTask.FromResult(authoritativeWorld),
                _ => ValueTask.FromResult(authoritativeWorld),
                (_, _) => throw new NotSupportedException("controlled Map start is not authorized here"),
                _ => ValueTask.CompletedTask);
            var actions = new Map317.MapActionProviderAdapter
            {
                GetCurrentServerId = _ => ValueTask.FromResult(currentServer),
                GotoServer = (server, _) =>
                {
                    calls.Add("server:" + server);
                    if (rejectSwitch) return ValueTask.FromException(new Map317.BridgeCommandException("SERVER_JUMP_TIMEOUT", "controlled failure"));
                    currentServer = wrongDestination ? 2212 : server;
                    return ValueTask.CompletedTask;
                },
                GotoWorldCoordinate = async (server, x, y, cancel) =>
                {
                    calls.Add($"tile:{server}:{x}:{y}:live={currentServer}");
                    Require(server == currentServer, "navigation did not reach row's server first");
                    if (holdNavigation)
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(cancel);
                    }
                },
                GotoWorldMarch = (_, _, _) => ValueTask.CompletedTask,
                InspectTreasureStates = (_, _, _) => ValueTask.FromResult(new Map317.TreasureInspectionResult("", "", [])),
                GetTreasureClaimStatus = _ => throw new NotSupportedException(),
                ClaimTreasures = (_, _) => throw new NotSupportedException(),
                ShareDispatchTaskToAlliance = (_, _) => throw new NotSupportedException(),
                PrepareGhostPlunderTasks = (_, _) => throw new NotSupportedException(),
                GetMapPlunderServerDayStart = _ => ValueTask.FromResult<Map317.MapPlunderServerDayProviderResult?>(null),
                ArmDispatchPlunder = (_, _) => throw new NotSupportedException(),
                ArmTruckPlunder = (_, _) => throw new NotSupportedException(),
                DrainDispatchPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.DispatchPlunderResultEvent>>([]),
                DrainTruckPlunderResults = _ => ValueTask.FromResult<IReadOnlyList<Map317.TruckPlunderResultEvent>>([]),
                ClearTruckPlunderPending = (_, _, _, _) => ValueTask.CompletedTask,
            };
            using var service = new Map317CommandService(db, source,
                actions, startPlunderWorkers: false,
                statusReader: _ => currentStatus is null
                    ? Task.FromException<CurrentClientMapStatusContext>(new BridgeCommandException("GAME_CONNECTION_UNAVAILABLE", "controlled transient"))
                    : Task.FromResult(currentStatus));

            async Task<int> Count(string kind, object query) =>
                (await Invoke(service, "map_search", new { kind, query })).GetProperty("total").GetInt32();

            Require(await Count("resource", new { serverId = 2213 }) == 4, "unfiltered Resource data changed");
            Require(await Count("resource", new { serverId = 2213, resourceAmountState = "full" }) == 1, "full requires genuine amount");
            Require(await Count("resource", new { serverId = 2213, resourceAmountState = "partial" }) == 2, "partial requires confirmed < full");
            Require(await Count("resource", new { serverId = 2213, resourceAmountState = "unknown" }) == 1, "unknown amount includes idle with no detail");
            Require(await Count("resource", new { serverId = 2213, resourceTileState = "black" }) == 2, "black tile producer");
            Require(await Count("resource", new { serverId = 2213, resourceTileState = "normal" }) == 1, "non-black tile producer");
            Require(await Count("resource", new { serverId = 2213, resourceTileState = "unknown" }) == 1, "unknown terrain preserved");
            Require(await Count("resource", new { serverId = 2213, minLevel = 7, maxLevel = 8,
                resourceAmountState = "partial", resourceTileState = "unknown" }) == 1, "combined level/amount/terrain");
            Require(await Count("resource", new { serverId = 2213, minLevel = 7, maxLevel = 7,
                resourceAmountState = "unknown", resourceTileState = "black" }) == 1, "unknown + black independent");
            Require(await Count("treasure", new { serverId = 2213 }) == 3,
                "genuine Treasure and season supplies remain separate stored identities");
            Require(await Count("treasure", new { serverId = 2213, suppliesType = 1 }) == 1,
                "Scattered Supplies maps only to LWIceSupplies type 1");
            Require(await Count("treasure", new { serverId = 2213, suppliesType = 3 }) == 1,
                "different season supply must not be mislabeled Scattered Supplies");
            Require(await Count("treasure", new { serverId = 2213, treasureType = 19 }) == 1,
                "ordinary Treasure cannot be conflated with season supply");
            Require(await Count("treasure", new { serverId = 2213, suppliesType = 1, treasureType = 19 }) == 0,
                "conflicting source identities must fail closed");
            JsonElement supplyOptions = await Invoke(service, "map_data_options", new { serverId = 2213 });
            JsonElement iceOption = supplyOptions.GetProperty("treasureTypes").EnumerateArray()
                .Single(row => row.GetProperty("key").GetString() == "supplies:1");
            Require(iceOption.GetProperty("suppliesType").GetInt32() == 1 &&
                iceOption.GetProperty("treasureType").GetInt32() == 0 &&
                iceOption.GetProperty("count").GetInt32() == 1,
                "real Map options expose the already-supported Scattered Supplies discriminator");
            JsonElement ownerRows = await Invoke(service, "map_search", new { kind = "dispatch", query = new { serverId = 2213, pageSize = 10 } });
            JsonElement matchingTask = ownerRows.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("uuid").GetString() == "matchingTask");
            Require(matchingTask.GetProperty("ownerUid").GetString() == "9912345678901234567" &&
                matchingTask.GetProperty("ownerName").GetString() == "Real Stored City Name" &&
                matchingTask.GetProperty("allianceName").GetString() == "ACTUAL" &&
                matchingTask.GetProperty("ownerNameSource").GetString() == "same_server_city",
                "exact UID and genuine same-server City owner metadata enrich the Task");
            JsonElement unknownTask = ownerRows.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("uuid").GetString() == "unknownOwnerTask");
            Require(unknownTask.GetProperty("ownerUid").GetString() == "990000000000000001" &&
                !unknownTask.TryGetProperty("ownerName", out _), "unknown owner must not be invented");

            Require(await Count("truck", new { serverId = 2213,
                itemKeys = new[] { "diamonds:100", "speedup:5m" } }) == 3,
                "ALL selected reward identities filter before counts");
            Require(await Count("truck", new { serverId = 2213,
                itemKeys = new[] { "diamonds:100", "speedup:5m" }, escortPowerMaxMillions = 25m }) == 1,
                "25 million inclusive: unknown and higher power excluded");
            JsonElement page = await Invoke(service, "map_search", new { kind = "truck", query = new {
                serverId = 2213, page = 1, pageSize = 1,
                sorts = new[] { new { sortBy = "itemCount", sortOrder = "desc" } },
                itemKeys = new[] { "diamonds:100", "speedup:5m" }, escortPowerMaxMillions = 25m } });
            Require(page.GetProperty("total").GetInt32() == 1 && page.GetProperty("rows").GetArrayLength() == 1 &&
                page.GetProperty("rows")[0].GetProperty("uuid").GetString() == "bothAtLimit",
                "canonical filtering precedes SQL pagination");
            Require(await Count("truck", new { serverId = 2213, itemKey = "speedup:5m" }) == 3,
                "existing single-item caller preserved");
            Require(await Count("truck", new { serverId = 2214, itemKeys = new[] { "diamonds:100", "speedup:5m" },
                escortPowerMaxMillions = 25m }) == 128, "255-selection inclusive power AND count");
            JsonElement lastPage = await Invoke(service, "map_search", new { kind = "truck", query = new {
                serverId = 2214, page = 3, pageSize = 50,
                sorts = new[] { new { sortBy = "power", sortOrder = "asc" } },
                itemKeys = new[] { "diamonds:100", "speedup:5m" }, escortPowerMaxMillions = 25m } });
            Require(lastPage.GetProperty("total").GetInt32() == 128 && lastPage.GetProperty("rows").GetArrayLength() == 28,
                "255-selection filter was applied before paginated last page");
            await Reject(async () => _ = await Count("truck", new { serverId = 2213, escortPowerMaxMillions = -1 }), "INVALID_MAP_QUERY");
            await Reject(async () => _ = await Count("truck", new { serverId = 2213, itemKeys = new[] { "" } }), "INVALID_MAP_QUERY");
            await Reject(async () => _ = await Count("resource", new { serverId = 2213, resourceAmountState = "depleted" }), "INVALID_MAP_QUERY");

            JsonElement moved = await Invoke(service, "map_coordinate_jump", new { serverId = 2213, x = 80, y = 90 });
            Require(moved.GetProperty("serverId").GetInt32() == 2213 && currentServer == 2213 &&
                calls.SequenceEqual(new[] { "server:2213", "tile:2213:80:90:live=2213" }),
                "stored 2213 row at same coordinates as home 2212 must switch and verify first");
            currentServer = 2212;
            rejectSwitch = true;
            await Reject(async () => _ = await Invoke(service, "map_coordinate_jump", new { serverId = 2213, x = 80, y = 90 }), "SERVER_JUMP_TIMEOUT");
            Require(currentServer == 2212 && calls.Count == 3, "failed switching must not navigate on home world");
            rejectSwitch = false;
            wrongDestination = true;
            await Reject(async () => _ = await Invoke(service, "map_coordinate_jump", new { serverId = 2213, x = 80, y = 90 }), "SERVER_JUMP_TIMEOUT");
            Require(calls.Count == 4, "unverified server must not navigate");
            wrongDestination = false;
            holdNavigation = true;
            Task<JsonElement> held = Invoke(service, "map_coordinate_jump", new { serverId = 2213, x = 80, y = 90 });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Reject(async () => _ = await Invoke(service, "server_jump", new { serverId = 2212 }), "GAME_OPERATION_IN_PROGRESS");
            release.TrySetResult(true);
            await held;

            // Failures and absent producer metadata must keep the last option
            // list on exactly the same run; an authoritative empty read clears it.
            JsonElement status = await Invoke(service, "map_scan_status", new { });
            Require(status.GetProperty("truckMatchServerIds").GetArrayLength() == 2, "initial native warzone list");
            currentStatus = null;
            status = await Invoke(service, "map_scan_status", new { });
            Require(status.GetProperty("truckMatchServerIds").GetArrayLength() == 2, "failed poll flickered options");
            currentStatus = new(true, 2212, 2212, [], [], 0, 1000, 1000, 0, 0, false);
            status = await Invoke(service, "map_scan_status", new { });
            Require(status.GetProperty("truckMatchServerIds").GetArrayLength() == 2, "native producer unknown flickered options");
            currentStatus = new(true, 2212, 2212, [], [], 0, 1000, 1000, 0, 0, true);
            status = await Invoke(service, "map_scan_status", new { });
            Require(status.GetProperty("truckMatchServerIds").GetArrayLength() == 0, "genuine empty list must clear stale buttons");

            // Auto's admitted cycle can be busy before the underlying scan is
            // marked IsReading. Both UI modes use this one command and guard.
            bool autoBusy = true;
            service.BindAutoScanCycleBusy(() => autoBusy);
            await Reject(async () => _ = await Invoke(service, "map_scan_clear", new { serverId = 2213 }), "AUTO_SCAN_RUNNING");
            Require(await Count("resource", new { serverId = 2213 }) == 4, "Auto preflight must not delete rows");
            autoBusy = false;
            await Reject(async () => _ = await Invoke(service, "map_scan_clear", new { serverId = 2212 }), "SERVER_UNAVAILABLE");
            Require(await Count("resource", new { serverId = 2213 }) == 4, "mismatched stored server must not delete rows");
            _ = await Invoke(service, "map_scan_clear", new { serverId = 2213 });
            Require(await Count("resource", new { serverId = 2213 }) == 0 &&
                await Count("truck", new { serverId = 2213 }) == 0 &&
                await Count("resource", new { serverId = 2212 }) == 1,
                "Auto/Manual canonical Clear acknowledgment deletes only the live target server");

            Console.WriteLine("MAP014_CANONICAL_FILTERS_SCATTERED_SUPPLIES_REMOTE_ROW_WARZONE_CLEAR_CONTROLLED_OK");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
