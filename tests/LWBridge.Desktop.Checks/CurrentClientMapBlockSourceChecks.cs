using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class CurrentClientMapBlockSourceChecks
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 2, 0, 0, TimeSpan.Zero);
    private static readonly OverviewMapScanSession Session = new(
        "default", "session_1", "challenge_1", 4242, @"C:\Game\LastWar.exe", "2026-09-14T01:00:00.0000000Z");

    [ModuleInitializer]
    internal static void Run()
    {
        RunAsync().GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        await ContextCarriesOptionalPlayerTile();
        await ResourceViewEnumeratesAndFilters();
        await EmptyResourceViewIsAZeroRowCapture();
        await LodZeroAcquiresAllFourCells();
        await CityLodZeroUsesOneLiveFootprint();
        await LodTwoFiltersSupersetToRequestedBlock();
        await EmptyCityCurrentViewIsAZeroRowCapture();
        await TargetedCityFallbackFailsClosed();
        await FastCityBatchReturnsTenLogicalCaptures();
        await CoordinateJumpUsesOwnedNavigation();
        await FastCityBandReturnsTwoHundredFiftyLogicalCaptures();
        await FastCityFullMapReturnsAllLogicalCaptures();
        await FastResourceFullMapReturnsAllLogicalCaptures();
        await FastMonsterFullMapReturnsAllLogicalCaptures();
        await FastMonsterKnownInactiveProtectionSuppressesRemaining();
        await FastMonsterAllowsIncompleteMonsterProtectionDetail();
        await FastTruckFullMapReturnsAllLogicalCaptures();
        await FastFullMapRejectsIncompleteMeasuredCoverage();
        await HealthyGateRunsBeforeWorldReadyProtocol();
        await MissingOwnedSessionFailsClosed();
    }

    private static MapScanExecutionRequest Request(string kind, long width = 20, long height = 20, long worldId = 7) =>
        new("run_1", 2212, worldId, width, height, [kind], 8, 2);

    private static MapScanTargetBlock Block() =>
        new(0, 0, 0, 0, 0, 19, 19, 9, 9);

    private static async Task ContextCarriesOptionalPlayerTile()
    {
        CurrentClientMapBlockSource source = CreateSource((fields, _) => FailedEmptyResource(fields));
        CurrentClientMapContext context = await source.GetCurrentContextAsync(CancellationToken.None);
        Check(context.PlayerTileX == 495 && context.PlayerTileY == 40,
            "world-ready context should preserve optional source-backed player tile metadata");
    }

    private static async Task ResourceViewEnumeratesAndFilters()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, mapKind) =>
        {
            probeCalls++;
            return ProvenResourceSnapshot(fields, (101, 4, 5), (102, 24, 5), (103, 8, 0));
        });

        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(probeCalls == 1, "resource block source should consume one complete fresh candidate snapshot");
        Check(capture.ServerId == 2212 && capture.WorldId == 7 && capture.BlockIndex == 0,
            "resource capture identity changed");
        Check(capture.Records.Count == 2 &&
              capture.Records.Select(record => record.RecordKey).Order().SequenceEqual(new[] { "101", "103" }),
            "resource capture should retain in-block records, including valid coordinate zero");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        Check(payload.RootElement.GetProperty("cells")[0].GetProperty("matchedCount").GetInt32() == 3 &&
              payload.RootElement.GetProperty("recordsInBlock").GetInt32() == 2 &&
              payload.RootElement.GetProperty("coverage").GetString() == "planned_aoi_cells_pending_live_footprint_proof" &&
              payload.RootElement.GetProperty("serverLod").GetInt32() == 1 &&
              payload.RootElement.GetProperty("aoiBlockSize").GetInt32() == 20 &&
              payload.RootElement.GetProperty("cells").GetArrayLength() == 1,
            "resource checkpoint must preserve truthful planned AOI coverage metadata");
    }

    private static async Task EmptyResourceViewIsAZeroRowCapture()
    {
        CurrentClientMapBlockSource source = CreateSource((fields, _) => FailedEmptyResource(fields));
        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(capture.Records.Count == 0, "fresh empty resource view should produce zero staged rows");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        Check(payload.RootElement.GetProperty("cells")[0].GetProperty("matchedCount").GetInt32() == 0,
            "fresh empty resource view should checkpoint zero candidates for its AOI cell");
    }

    private static async Task LodZeroAcquiresAllFourCells()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, _) =>
        {
            probeCalls++;
            return FailedEmptyResource(fields);
        }, currentLod: 4, serverLod: 0);

        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(probeCalls == 4, "server LOD 0 should acquire all four planned 10-tile AOI cells");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        JsonElement cells = payload.RootElement.GetProperty("cells");
        Check(payload.RootElement.GetProperty("serverLod").GetInt32() == 0 &&
              payload.RootElement.GetProperty("aoiBlockSize").GetInt32() == 10 &&
              cells.GetArrayLength() == 4,
            "server LOD 0 checkpoint should preserve its four-cell AOI plan");
        var actual = cells.EnumerateArray()
            .Select(cell => (cell.GetProperty("minX").GetInt32(), cell.GetProperty("minY").GetInt32(),
                             cell.GetProperty("maxX").GetInt32(), cell.GetProperty("maxY").GetInt32()))
            .ToArray();
        Check(actual.SequenceEqual(new[] { (0, 0, 9, 9), (10, 0, 19, 9), (0, 10, 9, 19), (10, 10, 19, 19) }),
            "server LOD 0 source cell bounds changed");
    }

    private static async Task CityLodZeroUsesOneLiveFootprint()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, mapKind) =>
        {
            probeCalls++;
            Check(mapKind == "city", "city footprint test received the wrong map kind");
            return ProvenCitySnapshot(fields, 10, 100, [0, 1, 100, 101], (200, 9, 9));
        }, currentLod: 4, serverLod: 0);

        MapScanBlockCapture capture = await source.CaptureAsync(Request("city"), Block(), CancellationToken.None);
        Check(probeCalls == 1,
            "one fresh City response should satisfy all four planned cells when _curViewIndex proves them covered");
        Check(capture.Records.Count == 1 && capture.Records[0].RecordKey == "200",
            "city footprint capture should retain the real in-block City row");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        Check(payload.RootElement.GetProperty("cells").GetArrayLength() == 4 &&
              payload.RootElement.GetProperty("responseCount").GetInt32() == 1 &&
              payload.RootElement.GetProperty("coverage").GetString() == "live_cur_view_index_covered" &&
              payload.RootElement.GetProperty("footprintSource").GetString() == "WorldPointManager._curViewIndex",
            "city checkpoint should expose source-backed current-view footprint coverage");
    }

    private static async Task LodTwoFiltersSupersetToRequestedBlock()
    {
        int probeCalls = 0;
        CurrentClientMapBlockSource source = CreateSource((fields, _) =>
        {
            probeCalls++;
            return ProvenResourceSnapshot(fields, (301, 5, 5), (302, 25, 5));
        }, currentLod: 6, serverLod: 2);

        MapScanBlockCapture capture = await source.CaptureAsync(Request("resource", 40, 40), Block(), CancellationToken.None);
        Check(probeCalls == 1, "server LOD 2 should consume one complete superset candidate snapshot");
        Check(capture.Records.Count == 1 && capture.Records[0].RecordKey == "301",
            "server LOD 2 should filter the larger AOI response back to the requested scan block");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        JsonElement cell = payload.RootElement.GetProperty("cells")[0];
        Check(payload.RootElement.GetProperty("serverLod").GetInt32() == 2 &&
              payload.RootElement.GetProperty("aoiBlockSize").GetInt32() == 1000 &&
              cell.GetProperty("minX").GetInt32() == 0 && cell.GetProperty("minY").GetInt32() == 0 &&
              cell.GetProperty("maxX").GetInt32() == 39 && cell.GetProperty("maxY").GetInt32() == 39 &&
              payload.RootElement.GetProperty("recordsInBlock").GetInt32() == 1,
            "server LOD 2 checkpoint should preserve the clamped superset AOI cell and filtered result count");
    }

    private static async Task EmptyCityCurrentViewIsAZeroRowCapture()
    {
        bool fallbackDisabled = false;
        CurrentClientMapBlockSource source = CreateSource((fields, mapKind) =>
        {
            Check(mapKind == "city", "empty-city block test received the wrong map kind");
            fallbackDisabled = fields.TryGetValue("allowCityTargetedFallback", out string? value) && value == "false";
            return ProvenEmptyCityCurrentView(fields);
        });

        MapScanBlockCapture capture = await source.CaptureAsync(Request("city"), Block(), CancellationToken.None);
        Check(fallbackDisabled, "shared Player City block source must disable the bounded home-city fallback");
        Check(capture.Records.Count == 0, "fresh empty Player City current view should produce zero staged rows");
        using JsonDocument payload = JsonDocument.Parse(capture.PayloadJson);
        Check(payload.RootElement.GetProperty("cells")[0].GetProperty("matchedCount").GetInt32() == 0,
            "fresh empty Player City current view should checkpoint zero candidates for its AOI cell");
    }

    private static async Task TargetedCityFallbackFailsClosed()
    {
        CurrentClientMapBlockSource source = CreateSource((fields, _) => ProvenCityTargeted(fields));
        try
        {
            _ = await source.CaptureAsync(Request("city"), Block(), CancellationToken.None);
            throw new InvalidOperationException("targeted Player City fallback should not be accepted as block coverage");
        }
        catch (InvalidDataException error) when (
            error.Message == "Player City block acquisition left the requested current view.")
        {
        }
    }

    private static async Task FastCityBatchReturnsTenLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                return ProvenFastCityBatch(fields, (200, 9, 9), (201, 25, 45));
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        int[] groupIndices = [0, 1, 50, 51, 100, 101, 150, 151, 200, 201];
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], groupIndices.ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 1 && captures.Count == 10,
            "fast City source should satisfy one 2x5 logical-block group from one native response");
        Check(captures.Select(capture => capture.BlockIndex).SequenceEqual(groupIndices),
            "fast City batch logical-block ordering changed");
        Check(captures.Single(capture => capture.BlockIndex == 0).Records.Single().RecordKey == "200" &&
              captures.Single(capture => capture.BlockIndex == 101).Records.Single().RecordKey == "201" &&
              captures.Where(capture => capture.BlockIndex is not (0 or 101)).All(capture => capture.Records.Count == 0),
            "fast City batch did not assign source-backed Cities to the correct logical blocks");
    }


    private static async Task FastCityBandReturnsTwoHundredFiftyLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                return ProvenFastCityBatch(fields);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        HashSet<int> band = blocks.Where(block => block.Row < 5).Select(block => block.BlockIndex).ToHashSet();
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], band, CancellationToken.None);
        Check(bulkCalls == 25 && captures.Count == 250,
            "fast City band should satisfy 250 logical blocks from 25 immediate native responses");
        Check(captures.Select(capture => capture.BlockIndex).Order().SequenceEqual(band.Order()),
            "fast City band did not return the exact pending 5-row logical-block band");
    }

    private static async Task FastCityFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                if (x == 25 && y == 75) return ProvenFastCityBatch(fields, (200, 9, 9));
                if (x == 975 && y == 975) return ProvenFastCityBatch(fields, (300, 985, 985));
                return ProvenFastCityBatch(fields);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 200 && captures.Count == 2500,
            "fast full-City source should use the live-measured 200 native responses for all 2,500 logical blocks");
        Check(captures.Single(capture => capture.BlockIndex == 0).Records.Single().RecordKey == "200" &&
              captures.Single(capture => capture.BlockIndex == 2499).Records.Single().RecordKey == "300",
            "fast full-City source should preserve globally accumulated Cities at both map extremes");
    }




    private static async Task FastResourceFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => FailedEmptyResource(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x == 25 && y == 75) return ProvenFastResourceBatch(fields, (400, 9, 9, 3, 2, true, false));
                if (x == 975 && y == 975) return ProvenFastResourceBatch(fields, (500, 985, 985, 10, 4, true, true));
                return ProvenFastResourceBatch(fields);
            });
        MapScanExecutionRequest request = Request("resource", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 200 && captures.Count == 2500, "fast full-Resource source should use 200 live-measured native responses");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "resource" && first.RecordKey == "400" && first.Level == 3 &&
              last.Kind == "resource" && last.RecordKey == "500" && last.Level == 10,
            "fast full-Resource source did not preserve normalized resources at map extremes");
        Check(first.DataJson.Contains("\"resourceTypeId\":2", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"rebuildGatherOccupancyKnown\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"rebuildGatherOccupied\":false", StringComparison.Ordinal) &&
              last.DataJson.Contains("\"rebuildGatherOccupied\":true", StringComparison.Ordinal),
            "fast full-Resource source did not preserve source-backed type and occupancy state");
    }

    private static async Task FastMonsterFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                Check(fields["homeTileX"] == "230" && fields["homeTileY"] == "257",
                    "fast full-Monster source must pass the authoritative player home/base tile into every live distance capture");
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x == 25 && y == 75)
                {
                    JsonObject root = JsonNode.Parse(ProvenFastMonsterBatch(fields, ("m-first", 9, 9, 1002009, 9, "2901012", true)))!.AsObject();
                    root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                    root["monsterProtectionDetailRequestCount"] = 1; root["monsterProtectionDetailReadyCount"] = 1;
                    return root.ToJsonString(JsonOptions.Default);
                }
                if (x == 975 && y == 975) return ProvenFastMonsterBatch(fields, ("m-last", 985, 985, 1004029, 29, "2000005", false));
                return ProvenFastMonsterBatch(fields);
            });
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 200 && captures.Count == 2500, "fast full-Monster source should use 200 live-measured native responses");
        Check(source.LastMonsterProtectionDetailMetrics is { BossCount: 1, TargetCount: 1, RequestCount: 1, ReadyCount: 1 },
            "fast full-Monster source should aggregate completed Monster Invasion protection detail refresh metrics");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "monster" && first.RecordKey == "m-first" && first.Level == 9 &&
              last.Kind == "monster" && last.RecordKey == "m-last" && last.Level == 29,
            "fast full-Monster source did not preserve normalized monsters at map extremes");
        Check(first.DataJson.Contains("\"monsterNameKey\":\"2901012\"", StringComparison.Ordinal),
            "fast full-Monster source did not preserve the authoritative Zombie Boss name key");
        Check(first.Distance == 12.5 && first.ShieldEndTime == 2_000_000_000_000L &&
              first.DataJson.Contains("\"distanceFromHome\":12.5", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"monsterProtectionKnown\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"monsterProtectionActive\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"shieldEndTime\":2000000000000", StringComparison.Ordinal),
            "fast full-Monster source should persist game-derived distance and Monster Invasion protection deadline");
    }

    private static async Task FastMonsterKnownInactiveProtectionSuppressesRemaining()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x != 25 || y != 75) return ProvenFastMonsterBatch(fields);
                JsonObject root = JsonNode.Parse(ProvenFastMonsterBatch(fields, ("m-inactive", 9, 9, 1002009, 9, "2901012", true)))!.AsObject();
                JsonObject row = root["monster_march_records"]!.AsArray()[0]!.AsObject();
                row["monsterProtectionKnown"] = true; row["monsterProtectionActive"] = false;
                row["monsterProtectionEndTime"] = 0L; row["zMBossShieldEndTime"] = 2_000_000_000_000L;
                root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                root["monsterProtectionDetailRequestCount"] = 1; root["monsterProtectionDetailReadyCount"] = 1;
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        MapStoredRecord row = captures.Single(c => c.BlockIndex == 0).Records.Single();
        Check(row.ShieldEndTime is null && !row.DataJson.Contains("\"shieldEndTime\":", StringComparison.Ordinal),
            "authoritative inactive Monster Protection must suppress Remaining and any stale zMBoss shield fallback");
    }

    private static async Task FastMonsterAllowsIncompleteMonsterProtectionDetail()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                JsonObject root = JsonNode.Parse(x == 25 && y == 75
                    ? ProvenFastMonsterBatch(fields, ("m-timeout", 9, 9, 1031015, 20, "2901012", true))
                    : ProvenFastMonsterBatch(fields))!.AsObject();
                if (x == 25 && y == 75)
                {
                    root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                    root["monsterProtectionDetailRequestCount"] = 1; root["monsterProtectionDetailReadyCount"] = 0;
                    JsonObject row = root["monster_march_records"]!.AsArray()[0]!.AsObject();
                    row["monsterProtectionKnown"] = false; row["monsterProtectionActive"] = false;
                    row["monsterProtectionEndTime"] = 0; row["zMBossShieldEndTime"] = 2_000_000_000_000L;
                }
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(captures.Count == 2500, "incomplete optional Monster Protection detail must not discard the base Monster scan");
        Check(source.LastMonsterProtectionDetailMetrics is { BossCount: 1, TargetCount: 1, RequestCount: 1, ReadyCount: 0 },
            "incomplete Monster Protection detail metrics must remain observable without failing acquisition");
        MapStoredRecord row = captures.Single(c => c.BlockIndex == 0).Records.Single();
        Check(row.ShieldEndTime is null && !row.DataJson.Contains("\"shieldEndTime\":", StringComparison.Ordinal),
            "unresolved Invasion Zombie Boss protection must not fall back to unrelated zMBoss shield data");
    }

    private static async Task FastTruckFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x == 25 && y == 75)
                {
                    JsonObject root = JsonNode.Parse(ProvenFastTruckBatch(fields, ("t-first", 9, 9, 86, 3, 1, "Driver A", 4_152_318L)))!.AsObject();
                    JsonObject row = root["train_march_records"]!.AsArray()[0]!.AsObject();
                    row.Remove("trainType");
                    row["trainDataJson"] = "{\"type\":1,\"arriveTime\":1789615774078,\"marchInfo\":{\"robTimes\":2}}";
                    return root.ToJsonString(JsonOptions.Default);
                }
                if (x == 975 && y == 975) return ProvenFastTruckBatch(fields, ("t-last", 985, 985, 87, 5, 2, "Driver B", 9_000_000L));
                return ProvenFastTruckBatch(fields);
            });
        MapScanExecutionRequest request = Request("truck", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 200 && captures.Count == 2500, "fast full-Truck source should use 200 live-measured native responses");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "truck" && first.RecordKey == "t-first" && first.Quality == 3 && first.Power == 4_152_318L &&
              last.Kind == "truck" && last.RecordKey == "t-last" && last.Quality == 5 && last.Power == 9_000_000L,
            "fast full-Truck source did not preserve live train identity/quality/power at map extremes");
        Check(first.Name == "Driver A" && first.DataJson.Contains("\"trainType\":1", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"trainCfgId\":86", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"arriveTs\":1789615774078", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"robTimes\":2", StringComparison.Ordinal),
            "fast full-Truck source did not preserve source-backed truck metadata");
    }

    private static async Task FastFullMapRejectsIncompleteMeasuredCoverage()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                string result = ProvenFastCityBatch(fields);
                if (x != 25 || y != 75) return result;
                JsonObject root = JsonNode.Parse(result)!.AsObject();
                JsonArray indices = root["requestedIndices"]!.AsArray();
                JsonArray reduced = new(indices
                    .Select(node => node!.GetValue<int>())
                    .Where(index => index % 100 != 4)
                    .Select(index => (JsonNode?)JsonValue.Create(index))
                    .ToArray());
                root["requestedIndices"] = reduced;
                root["nativeCurrentSetCount"] = reduced.Count;
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        try
        {
            _ = await source.CaptureBatchAsync(
                request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
            throw new InvalidOperationException("expected incomplete measured full-world coverage rejection");
        }
        catch (InvalidDataException error) when (error.Message.Contains("9990/10000", StringComparison.Ordinal))
        {
        }
        Check(bulkCalls == 200, "incomplete measured footprint should still fail only at the exact full-world union gate");
    }

    private static async Task CoordinateJumpUsesOwnedNavigation()
    {
        int navigationWrites = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("map-navigation.txt", StringComparison.OrdinalIgnoreCase)) navigationWrites++;
            });
        CurrentClientCoordinateJumpResult result = await source.JumpToCoordinateAsync(2212, 836, 601, CancellationToken.None);
        Check(navigationWrites == 1 && result.ServerId == 2212 && result.X == 836 && result.Y == 601,
            "coordinate Jump must use one correlated owned-session map navigation request");
        try
        {
            _ = await source.JumpToCoordinateAsync(2213, 836, 601, CancellationToken.None);
            throw new InvalidOperationException("stale-server Jump should fail closed");
        }
        catch (BridgeCommandException error) when (error.Code == "STALE_MAP_SERVER") { }
    }

    private static async Task HealthyGateRunsBeforeWorldReadyProtocol()
    {
        bool healthGatePassed = false;
        bool worldReadyObserved = false;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => FailedEmptyResource(fields),
            waitForHealthySession: (session, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Check(session == Session, "health gate received a different owned session");
                healthGatePassed = true;
                return Task.CompletedTask;
            },
            onProtocolWrite: path =>
            {
                if (!path.EndsWith("world-ready.txt", StringComparison.OrdinalIgnoreCase)) return;
                worldReadyObserved = true;
                Check(healthGatePassed, "world-ready protocol ran before the game-health gate");
            });

        _ = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(healthGatePassed && worldReadyObserved,
            "current-client block source must gate world entry on healthy game readiness");
    }

    private static async Task MissingOwnedSessionFailsClosed()
    {
        var source = new CurrentClientMapBlockSource(
            () => null,
            @"C:\overview",
            @"C:\probe",
            new CurrentClientMapBlockSourceHooks());
        try
        {
            _ = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
            throw new InvalidOperationException("missing owned Overview session should fail closed");
        }
        catch (BridgeCommandException error) when (
            error.Code == "GAME_CONNECTION_UNAVAILABLE" && error.Message == "game connection unavailable")
        {
        }
    }

    private static CurrentClientMapBlockSource CreateSource(
        Func<IReadOnlyDictionary<string, string>, string, string> probeResult,
        int currentLod = 5,
        int serverLod = 1,
        Func<OverviewMapScanSession, CancellationToken, Task>? waitForHealthySession = null,
        Action<string>? onProtocolWrite = null,
        Func<IReadOnlyDictionary<string, string>, string>? bulkResult = null)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        string overviewRoot = @"C:\overview";
        string probeRoot = @"C:\probe";        var hooks = new CurrentClientMapBlockSourceHooks
        {
            UtcNow = () => Now,
            DelayAsync = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            ReadAllBytes = path => files.TryGetValue(path, out byte[]? bytes)
                ? bytes
                : throw new FileNotFoundException("test protocol file unavailable", path),
            WriteTextAtomic = (path, text) =>
            {
                onProtocolWrite?.Invoke(path);
                IReadOnlyDictionary<string, string> fields = ParseKv(text);
                if (string.Equals(path, Path.Combine(overviewRoot, "world-ready.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string result = WorldReadyResult(fields);
                    files[Path.Combine(overviewRoot, "world-ready-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(overviewRoot, "map-navigation.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string result = NavigationResult(fields, currentLod, serverLod);
                    files[Path.Combine(overviewRoot, "map-navigation-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(probeRoot, "command.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string mapKind = fields["mapKind"];
                    string result = probeResult(fields, mapKind);
                    files[Path.Combine(probeRoot, "result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(probeRoot, "bulk-aoi-diagnostic.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (bulkResult is null) throw new InvalidOperationException("unexpected fast City bulk request");
                    string result = bulkResult(fields);
                    files[Path.Combine(probeRoot, "bulk-aoi-diagnostic-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                throw new InvalidOperationException("unexpected protocol write: " + path);
            },
        };
        return new CurrentClientMapBlockSource(
            () => Session,
            overviewRoot,
            probeRoot,
            hooks,
            waitForHealthySession);
    }

    private static IReadOnlyDictionary<string, string> ParseKv(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static string WorldReadyResult(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state = "proven",
            method = "already_world_scene",
            serverId = 2212,
            worldId = 0,
            tileWidth = 1000,
            tileHeight = 1000,
            playerTileX = 495,
            playerTileY = 40,
        }, JsonOptions.Default);

    private static string NavigationResult(
        IReadOnlyDictionary<string, string> fields,
        int currentLod,
        int serverLod) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state = "proven",
            serverId = int.Parse(fields["serverId"]),
            worldId = long.Parse(fields["worldId"]),
            liveCurServerId = int.Parse(fields["serverId"]),
            liveSelfServerId = int.Parse(fields["serverId"]),
            liveWorldId = 0,
            targetX = int.Parse(fields["targetX"]),
            targetY = int.Parse(fields["targetY"]),
            currentX = 137,
            currentY = 674,
            verifiedTargetX = int.Parse(fields["targetX"]),
            verifiedTargetY = int.Parse(fields["targetY"]),
            expectedUniqueTileX = int.Parse(fields["targetX"]),
            expectedUniqueTileY = int.Parse(fields["targetY"]),
            verifiedUniqueTileX = int.Parse(fields["targetX"]),
            verifiedUniqueTileY = int.Parse(fields["targetY"]),
            expectedGotoWorldX = 219.0,
            expectedGotoWorldY = 0.0,
            expectedGotoWorldZ = 219.0,
            targetWorldX = 219.0,
            targetWorldY = 0.0,
            targetWorldZ = 219.0,
            // Live block-0 evidence showed normal Unity float drift at the callback.
            // The requested tile is still exact after SceneUtils.WorldToTile.
            postCurTargetX = 219.0,
            postCurTargetY = 0.0,
            postCurTargetZ = 218.999992370605,
            postTargetTileX = int.Parse(fields["targetX"]),
            postTargetTileY = int.Parse(fields["targetY"]),
            verifyCurTargetX = 219.0,
            verifyCurTargetY = 0.0,
            verifyCurTargetZ = 219.0,
            currentLod,
            serverLod,
            lwAoiBlockSizeArray = new[] { 10, 20, 1000 },
            method = "SceneUtils.TileToWorld(ForceChangeScene.World,serverId)+GoToUtil.GotoWorldPos(serverId,worldId)+completionCallbackCurTarget",
        }, JsonOptions.Default);

    private static string ProvenResourceSnapshot(
        IReadOnlyDictionary<string, string> fields,
        params (int PointId, int X, int Y)[] points) => JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "resource",
            acquisitionOrdinal = 1,
            state = "proven",
            capturedAt = Timestamp(),
            requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
            responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true",
            source = "WorldPointManager._pointInfos",
            loadedPointCount = points.Length,
            resourcePointCount = points.Length,
            selectedResourceIndex = 1,
            point_records = points.Select(point => new
            {
                kind = "resource_point",
                serverId = 2212,
                pointId = point.PointId,
                x = point.X,
                y = point.Y,
                level = 3,
                name = "Resource",
                source = "WorldPointManager._pointInfos",
            }).ToArray(),
        }, JsonOptions.Default);

    private static string FailedEmptyResource(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "resource",            state = "failed",
            capturedAt = Timestamp(),
            error = "no resource point is loaded after the fresh view response",
        }, JsonOptions.Default);

    private static string ProvenCitySnapshot(
        IReadOnlyDictionary<string, string> fields,
        int blockSize,
        int blockCount,
        int[] currentViewIndices,
        params (int PointId, int X, int Y)[] points) => JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "city",
            acquisitionOrdinal = 1,
            state = "proven",
            capturedAt = Timestamp(),
            requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
            responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true",
            source = "WorldPointManager._pointInfos",
            lwAoiBlockSize = blockSize,
            lwAoiBlockCount = blockCount,
            curViewIndexCount = currentViewIndices.Length,
            curViewIndices = currentViewIndices,
            loadedPointCount = points.Length,
            cityPointCount = points.Length,
            selectedCityIndex = 1,
            point_records = points.Select(point => new
            {
                kind = "player_base", pointType = 6, serverId = 2212,
                pointId = point.PointId, x = point.X, y = point.Y,
                ownerUid = "u1", ownerName = "Player", level = 30,
                source = "WorldPointManager._pointInfos",
            }).ToArray(),
        }, JsonOptions.Default);

    private static string ProvenEmptyCityCurrentView(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "city",
            acquisitionOrdinal = 1,
            state = "proven",
            capturedAt = Timestamp(),
            requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
            responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true",
            source = "WorldPointManager._pointInfos",
            lwAoiBlockSize = 20,
            lwAoiBlockCount = 50,
            curViewIndexCount = 1,
            curViewIndices = new[] { 0 },
            loadedPointCount = 0,
            cityPointCount = 0,
            point_records = Array.Empty<object>(),
        }, JsonOptions.Default);

    private static string ProvenCityTargeted(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            gamePid = int.Parse(fields["gamePid"]),
            mapKind = "city",
            acquisitionOrdinal = 1,
            state = "proven",
            capturedAt = Timestamp(),
            requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)+SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId)",
            responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true;targetedSameServerView=false->true",
            source = "WorldPointManager._pointInfos",
            lwAoiBlockSize = 20,
            lwAoiBlockCount = 50,
            curViewIndexCount = 1,
            curViewIndices = new[] { 0 },
            loadedPointCount = 1,
            cityPointCount = 1,
            selectedCityIndex = 1,
            cityTargetedView = true,
            point_records = new[]
            {
                new
                {
                    kind = "player_base",
                    pointType = 6,
                    serverId = 2212,
                    pointId = 200,
                    x = 9,
                    y = 9,                    ownerUid = "u1",
                    ownerName = "Player",
                    level = 30,
                    source = "WorldPointManager._pointInfos",
                },
            },
        }, JsonOptions.Default);

    private static string ProvenFastCityBatch(
        IReadOnlyDictionary<string, string> fields,
        params (int PointId, int X, int Y)[] points)
    {
        int targetX = int.Parse(fields["targetTileX"]);
        int targetY = int.Parse(fields["targetTileY"]);
        int startCellX = Math.Clamp((targetX / 10) - 2, 0, 95);
        int startCellY = Math.Clamp((targetY / 10) - 7, 0, 90);
        int[] requested = Enumerable.Range(startCellY, 10)
            .SelectMany(row => Enumerable.Range(startCellX, 5).Select(column => row * 100 + column))
            .ToArray();
        bool includeMonster = fields.TryGetValue("includeMonster", out string? includeMonsterText) && includeMonsterText == "true";
        bool includeTrain = fields.TryGetValue("includeTrain", out string? includeTrainText) && includeTrainText == "true";
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1, probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"], launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"], challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]), requestedCount = 8, requestMode = "coverage", includeMonster, includeTrain,
            state = "proven", error = (string?)null, requestedIndices = requested,
            matchedCityCount = points.Length, matchedResourceCount = 0,
            monsterInvasionBossCount = 0, monsterProtectionDetailTargetCount = 0,
            monsterProtectionDetailRequestCount = 0, monsterProtectionDetailReadyCount = 0,
            serverLod = 0, blockSize = 10, blockCount = 100,
            targetTileX = int.Parse(fields["targetTileX"]), targetTileY = int.Parse(fields["targetTileY"]),
            responseFlagsTransitioned = true, cameraTileStable = true, positionRestoredBeforeResponse = true,
            requestMethod = "WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore",
            nativeCurrentSetCount = requested.Length, holdMilliseconds = 0,
            homeTileX = int.Parse(fields["homeTileX"]), homeTileY = int.Parse(fields["homeTileY"]), viewLevel = -1,
            postServerLod = 0, postBlockSize = 10, postBlockCount = 100, capturedAt = Timestamp(),
            point_records = points.Select(point => new
            {
                kind = "player_base", pointType = 6, serverId = 2212, pointId = point.PointId,
                x = point.X, y = point.Y, ownerUid = "u" + point.PointId, ownerName = "Player",
                level = 30, source = "WorldPointManager._pointInfos",
            }).ToArray(),
            monster_march_records = Array.Empty<object>(),
            train_march_records = Array.Empty<object>(),
        }, JsonOptions.Default);
    }



    private static string ProvenFastResourceBatch(
        IReadOnlyDictionary<string, string> fields,
        params (int PointId, int X, int Y, int Level, int ResourceTypeId, bool OccupancyKnown, bool Occupied)[] resources)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["matchedCityCount"] = 0;
        root["matchedResourceCount"] = resources.Length;
        root["point_records"] = JsonSerializer.SerializeToNode(resources.Select(resource => new
        {
            id = resource.PointId, pointId = resource.PointId, pointType = 1, kind = "resource_point",
            runtimeClass = "ResPointInfo", serverId = 2212, srcServerId = 0, worldId = 0,
            x = resource.X, y = resource.Y, level = resource.Level, resourceTypeId = resource.ResourceTypeId,
            resourceSourceType = "ResPointInfo", gatherOccupancyKnown = resource.OccupancyKnown,
            gatherOccupied = resource.Occupied, source = "WorldPointManager._pointInfos",
        }).ToArray(), JsonOptions.Default);
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenFastTruckBatch(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, int X, int Y, int TrainCfgId, int Quality, int CarriageNum, string OwnerName, long Power)[] trucks)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["includeTrain"] = true;
        root["train_march_records"] = JsonSerializer.SerializeToNode(trucks.Select(truck => new
        {
            uuid = truck.Uuid, runtimeClass = "WorldMarch", serverId = 2212, worldId = 0,
            x = truck.X, y = truck.Y, positionIndex = truck.Y * 1000 + truck.X + 1,
            ownerUid = "owner-" + truck.Uuid, ownerName = truck.OwnerName, allianceName = "Alliance", ownerServer = 2212,
            power = truck.Power, startTime = 1_789_588_788_959L, endTime = 1_789_589_236_172L,
            trainUuid = 1_417_409_824_803_038_247L, trainCfgId = truck.TrainCfgId, trainType = 1,
            trainQuality = truck.Quality, carriageNum = truck.CarriageNum, trainDataJson = "{}",
            source = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
        }).ToArray(), JsonOptions.Default);
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenFastMonsterBatch(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, int X, int Y, int MonsterId, int Level, string NameKey, bool Boss)[] monsters)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["includeMonster"] = true;
        root["monster_march_records"] = JsonSerializer.SerializeToNode(monsters.Select(monster => new
        {
            uuid = monster.Uuid, kind = "monster", runtimeClass = "WorldMarch", serverId = 2212, worldId = 0,
            x = monster.X, y = monster.Y, positionIndex = monster.Y * 1000 + monster.X + 1,
            monsterId = monster.MonsterId, monsterType = 0, monsterSpecialType = 0, monsterRallyNum = monster.Boss ? 1 : 0,
            configId = monster.MonsterId, monsterNameKey = monster.NameKey, monsterLevel = monster.Level,
            configType = monster.Boss ? 7 : 1, configSpecial = monster.NameKey == "2901012" ? 10 : 0, configBoss = monster.Boss ? 1 : 0,
            hp = 1, maxHp = 1, createTime = 1L, refreshTime = 2L, expireTime = 0L,
            distanceFromHome = monster.Uuid == "m-first" ? 12.5 : 34.5,
            zMBossId = monster.Boss ? 7001 : 0,
            zMBossStage = monster.Boss ? 2 : 0,
            zMBossShieldHp = monster.Boss ? 1_000L : 0L,
            zMBossShieldMaxHp = monster.Boss ? 2_000L : 0L,
            zMBossShieldEndTime = 0L,
            monsterProtectionEligible = monster.NameKey == "2901012",
            monsterProtectionKnown = monster.NameKey == "2901012",
            monsterProtectionActive = monster.NameKey == "2901012",
            monsterProtectionEndTime = monster.Uuid == "m-first" && monster.NameKey == "2901012" ? 2_000_000_000_000L : 0L,
            isMonster = !monster.Boss, isBoss = monster.Boss, isOrdinaryBoss = monster.Boss,
            isWanderMonster = false, isWanderBoss = false, isZombieRushAltered = false,
            source = "WorldScene.MarchDataManager.GetAllMarchesByCS",
        }).ToArray(), JsonOptions.Default);
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string Timestamp() => Now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
