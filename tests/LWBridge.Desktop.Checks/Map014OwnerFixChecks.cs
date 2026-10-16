using System.Text.Json;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
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
        // The packaged proof also has eight-kind, City-only and Other-only
        // modes. Its receipt must retain the actual native completion selection
        // even when that differs from the historical six-kind proof default.
        foreach (string[] kinds in new string[][]
        {
            ["city", "resource", "monster", "truck", "railway", "dispatch"],
            ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "zombie_boss"],
            ["city"], ["other"], ["resource"],
        })
        {
            string[] retained;
            using (JsonDocument status = JsonDocument.Parse(JsonSerializer.Serialize(new { selectedTypes = kinds })))
                retained = LWBridgeWindow.ReadNormalUiProofSelectedTypes(status.RootElement);
            Require(retained.SequenceEqual(kinds), "packaged proof receipt misreported its actual completed selection");
        }
        foreach (object malformed in new object[]
        {
            new { }, new { selectedTypes = Array.Empty<string>() },
            new { selectedTypes = "city" }, new { selectedTypes = new[] { "city", "city" } },
            new { selectedTypes = new[] { "not-a-map-kind" } },
            new { selectedTypes = new object[] { "city", 1 } },
        })
        {
            bool rejected = false;
            try { _ = LWBridgeWindow.ReadNormalUiProofSelectedTypes(Json(malformed)); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "packaged proof fabricated selection metadata from malformed native completion");
        }

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
                // The UI regards terrain as known only when BOTH source fields
                // are JSON booleans. Exercise every malformed/missing variant
                // against the actual MapStore SQL on an independent server.
                Insert(store, "resource", "strictBlack", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = true });
                Insert(store, "resource", "strictNormal", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = false });
                Insert(store, "resource", "missingKnown", 2217, 6, null,
                    new { isBlackTile = false });
                Insert(store, "resource", "falseKnown", 2217, 6, null,
                    new { blackTileKnown = false, isBlackTile = true });
                Insert(store, "resource", "nullKnown", 2217, 6, null,
                    new { blackTileKnown = (bool?)null, isBlackTile = false });
                Insert(store, "resource", "missingValue", 2217, 6, null,
                    new { blackTileKnown = true });
                Insert(store, "resource", "nullValue", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = (bool?)null });
                Insert(store, "resource", "stringValue", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = "false" });
                Insert(store, "resource", "numericValue", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = 2 });
                Insert(store, "resource", "stringKnown", 2217, 6, null,
                    new { blackTileKnown = "true", isBlackTile = false });
                Insert(store, "resource", "numericKnown", 2217, 6, null,
                    new { blackTileKnown = 1, isBlackTile = true });
                // Rows scanned with the per-account Player.IsInBlackRange(pos) call say nothing about the tile.
                Insert(store, "resource", "legacyAccountFalse", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = false, source = "WorldPointManager._pointInfos+GatherResource+LuaEntry.Player.IsInBlackRange" });
                Insert(store, "resource", "legacyAccountTrue", 2217, 6, null,
                    new { blackTileKnown = true, isBlackTile = true, source = "WorldPointManager._pointInfos+GatherResource+LuaEntry.Player.IsInBlackRange" });
                const string exactUid = "9912345678901234567";
                Insert(store, "city", "cityForDispatchOwner", 2213, 30, null,
                    new { ownerUid = exactUid, ownerName = "Real Stored City Name", allianceName = "ACTUAL" });
                Insert(store, "city", "wrongServerCityOwner", 2212, 30, null,
                    new { ownerUid = exactUid, ownerName = "Wrong Server Identity", allianceName = "OTHER" });
                Insert(store, "dispatch", "matchingTask", 2213, 6, null,
                    new { ownerUid = exactUid, ownerName = (string?)null, completionTime = 7000000000000L });
                Insert(store, "dispatch", "unknownOwnerTask", 2213, 6, null,
                    new { ownerUid = "990000000000000001", completionTime = 7000000000000L });
                // Legacy records from the previous revision were published as
                // Treasure. On reopen the real store must migrate only source-
                // confirmed Other events, retaining unrelated Treasure rows.
                Insert(store, "treasure", "seasonIceSupply", 2213, null, null,
                    new { pointType = 27, runtimeClass = "WorldSuppliesPoint", suppliesType = 1,
                        treasureType = 0, configId = 301 });
                Insert(store, "treasure", "otherSeasonSupply", 2213, null, null,
                    new { pointType = 27, runtimeClass = "WorldSuppliesPoint", suppliesType = 3,
                        treasureType = 0, configId = 302 });
                Insert(store, "treasure", "ordinaryTreasure", 2213, null, null,
                    new { pointType = 21, runtimeClass = "TreasurePointInfo", suppliesType = 0,
                        treasureType = 19 });
                Insert(store, "treasure", "celebrationParty", 2213, null, null,
                    new { pointType = 21, runtimeClass = "TreasurePointInfo", suppliesType = 0,
                        treasureType = 20, eventId = 27000 });
                Insert(store, "treasure", "differentEvent", 2213, null, null,
                    new { pointType = 21, runtimeClass = "TreasurePointInfo", suppliesType = 0,
                        treasureType = 20, eventId = 27015 });
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

            // A genuine 842-Dispatch/4,132-City population exposed a native
            // search timeout: SQLite ran both owner lookups before paginating.
            // Seed the same cardinalities transactionally to catch that plan
            // regression without shipping or opening the owner's real database.
            using (var connection = new SqliteConnection($"Data Source={db}"))
            {
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO map_records(kind,server_id,record_key,uuid,updated_at,data_json)
                    VALUES ($kind,2215,$key,$uuid,$updated,$json)
                    """;
                var kind = insert.Parameters.Add("$kind", SqliteType.Text);
                var key = insert.Parameters.Add("$key", SqliteType.Text);
                var uuid = insert.Parameters.Add("$uuid", SqliteType.Text);
                var updated = insert.Parameters.Add("$updated", SqliteType.Integer);
                var json = insert.Parameters.Add("$json", SqliteType.Text);
                for (int i = 0; i < 4_132 + 842; i++)
                {
                    bool isCity = i < 4_132;
                    int sequence = isCity ? i : i - 4_132;
                    string identity = "99000000" + sequence.ToString("D6");
                    string id = (isCity ? "load-city-" : "load-task-") + sequence.ToString("D4");
                    kind.Value = isCity ? "city" : "dispatch";
                    key.Value = id;
                    uuid.Value = id;
                    updated.Value = (isCity ? 1_000L : 10_000L) + sequence;
                    json.Value = isCity
                        ? JsonSerializer.Serialize(new { uuid = id, ownerUid = identity,
                            ownerName = "Native owner " + sequence, allianceName = "Alliance " + (sequence % 5) })
                        : JsonSerializer.Serialize(new { uuid = id, ownerUid = identity, completionTime = 7000000000000L });
                    insert.ExecuteNonQuery();
                }
                transaction.Commit();
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
            bool statusProbeTimesOut = false;
            using var service = new Map317CommandService(db, source,
                actions, startPlunderWorkers: false,
                statusReader: _ => statusProbeTimesOut
                    ? Task.FromException<CurrentClientMapStatusContext>(new TimeoutException("controlled fresh world-state poll unavailable"))
                    : currentStatus is null
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
            Require(await Count("resource", new { serverId = 2217 }) == 13,
                "malformed terrain fixture contains every source shape");
            Require(await Count("resource", new { serverId = 2217, resourceTileState = "black" }) == 1,
                "only a genuine JSON true with boolean known=true is black");
            Require(await Count("resource", new { serverId = 2217, resourceTileState = "normal" }) == 1,
                "only a genuine JSON false with boolean known=true is normal");
            JsonElement unknownTerrainRows = await Invoke(service, "map_search", new
            {
                kind = "resource", query = new { serverId = 2217, resourceTileState = "unknown" }
            });
            string[] expectedUnknownTerrainKeys =
            [
                "missingKnown", "falseKnown", "nullKnown", "missingValue", "legacyAccountFalse", "legacyAccountTrue",
                "nullValue", "stringValue", "numericValue", "stringKnown", "numericKnown",
            ];
            Require(unknownTerrainRows.GetProperty("total").GetInt32() == expectedUnknownTerrainKeys.Length &&
                unknownTerrainRows.GetProperty("rows").EnumerateArray()
                    .Select(row => row.GetProperty("uuid").GetString() ?? "")
                    .ToHashSet(StringComparer.Ordinal).SetEquals(expectedUnknownTerrainKeys),
                "unknown terrain must include ALL malformed/missing flags without dropping any rows");
            Require(await Count("treasure", new { serverId = 2213 }) == 2,
                "Other supplies and Celebration Party are excluded from Treasure");
            Require(await Count("other", new { serverId = 2213 }) == 3,
                "legacy supplies and exact event 27000 migrate to independent Other");
            Require(await Count("other", new { serverId = 2213, otherSubtype = "supplies:1" }) == 1,
                "Scattered Supplies maps only to LWIceSupplies type 1");
            Require(await Count("other", new { serverId = 2213, otherSubtype = "supplies:3" }) == 1,
                "different season supply has its own Other subtype");
            Require(await Count("other", new { serverId = 2213, otherSubtype = "event:27000" }) == 1,
                "event 27000 is uniquely Celebration Party; 27015 must remain Treasure");
            Require(await Count("other", new { serverId = 2212 }) == 1,
                "Other migration does not alter server identity");
            await Reject(async () => { await Count("other", new { serverId = 2213, otherSubtype = "event:27015" }); },
                "INVALID_MAP_QUERY");
            Require(await Count("treasure", new { serverId = 2213, treasureType = 19 }) == 1,
                "ordinary Treasure cannot be conflated with season supply");
            Require(await Count("treasure", new { serverId = 2213, suppliesType = 1, treasureType = 19 }) == 0,
                "conflicting source identities must fail closed");
            JsonElement supplyOptions = await Invoke(service, "map_data_options", new { serverId = 2213 });
            Require(!supplyOptions.GetProperty("treasureTypes").EnumerateArray()
                    .Any(row => row.GetProperty("key").GetString()?.StartsWith("supplies:", StringComparison.Ordinal) == true),
                "Treasure dropdown excludes migrated Other supplies");
            JsonElement iceOption = supplyOptions.GetProperty("otherTypes").EnumerateArray()
                .Single(row => row.GetProperty("key").GetString() == "supplies:1");
            Require(iceOption.GetProperty("count").GetInt32() == 1 &&
                supplyOptions.GetProperty("otherTypes").EnumerateArray()
                    .Single(row => row.GetProperty("key").GetString() == "event:27000")
                    .GetProperty("count").GetInt32() == 1,
                "Other subtype counts include source-confirmed supplies and Party");
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

            var dispatchTimer = Stopwatch.StartNew();
            JsonElement dispatchPage1 = await Invoke(service, "map_search", new { kind = "dispatch", query = new { serverId = 2215, page = 1 } });
            JsonElement dispatchPage2 = await Invoke(service, "map_search", new { kind = "dispatch", query = new { serverId = 2215, page = 2 } });
            dispatchTimer.Stop();
            Require(dispatchTimer.Elapsed < TimeSpan.FromSeconds(25),
                "genuine-scale two-page Dispatch owner resolution exceeded native WebView search deadline");
            Require(dispatchPage1.GetProperty("total").GetInt32() == 842 && dispatchPage2.GetProperty("total").GetInt32() == 842,
                "Dispatch total changed when name resolution was moved after pagination");
            for (int pageIndex = 0; pageIndex < 2; pageIndex++)
            {
                JsonElement result = pageIndex == 0 ? dispatchPage1 : dispatchPage2;
                JsonElement.ArrayEnumerator records = result.GetProperty("rows").EnumerateArray();
                int n = 0;
                foreach (JsonElement record in records)
                {
                    int identity = 841 - pageIndex * 50 - n++;
                    Require(record.GetProperty("uuid").GetString() == "load-task-" + identity.ToString("D4") &&
                        record.GetProperty("ownerName").GetString() == "Native owner " + identity &&
                        record.GetProperty("ownerNameSource").GetString() == "same_server_city",
                        "Dispatch page order or exact owner identity changed after early SQL pagination");
                }
                Require(n == 50, "Dispatch page returned fewer than 50 real rows");
            }

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
            Require(Json(service.CreateStatus()).GetProperty("truckMatchServerIds").GetArrayLength() == 2,
                "synchronous normal-window status erased native warzones");
            JsonElement summaryStatus = await Invoke(service,"map_summary",new{});
            Require(summaryStatus.GetProperty("scanState").GetProperty("truckMatchServerIds").GetArrayLength() == 2,
                "summary discarded its freshly observed native warzones");
            statusProbeTimesOut = true;
            status = await Invoke(service, "map_scan_status", new { });
            Require(status.GetProperty("truckMatchServerIds").GetArrayLength() == 2 &&
                Json(service.CreateStatus()).GetProperty("truckMatchServerIds").GetArrayLength() == 2,
                "timed-out world-state poll must retain exact-owned last accepted live metadata");
            statusProbeTimesOut = false;
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
            await Reject(async () => _ = await Invoke(service, "map_scan_clear", new { serverId = 2213, clearSavedServer = true }), "AUTO_SCAN_RUNNING");
            Require(await Count("resource", new { serverId = 2213 }) == 4, "Auto preflight must not delete rows");
            autoBusy = false;
            // Auto returned the game home. Legacy clear still rejects any
            // mismatch; explicit Auto saved-server clear is authorized only
            // for an actually persisted remote server with fresh world owner.
            authoritativeWorld = authoritativeWorld with { ServerId = 2212 };
            currentServer = 2212;
            // Owner fix: a plain (Manual tab) Clear of a saved server the game is not on is allowed;
            // a server with no published rows is still refused, with or without the flag.
            await Reject(async () => _ = await Invoke(service, "map_scan_clear", new { serverId = 2216 }), "SERVER_UNAVAILABLE");
            await Reject(async () => _ = await Invoke(service, "map_scan_clear", new { serverId = 2216, clearSavedServer = true }), "SERVER_UNAVAILABLE");
            Require(await Count("resource", new { serverId = 2213 }) == 4, "mismatched or absent stored server must not be deleted");
            _ = await Invoke(service, "map_scan_clear", new { serverId = 2213 });
            Require(await Count("resource", new { serverId = 2213 }) == 0 &&
                await Count("truck", new { serverId = 2213 }) == 0 &&
                await Count("resource", new { serverId = 2212 }) == 1 &&
                await Count("truck", new { serverId = 2214 }) == 255,
                "Explicit Auto saved-server Clear deletes selected remote and preserves home/other servers");

            Console.WriteLine("MAP014_CANONICAL_FILTERS_SCATTERED_SUPPLIES_REMOTE_ROW_WARZONE_CLEAR_CONTROLLED_OK");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
