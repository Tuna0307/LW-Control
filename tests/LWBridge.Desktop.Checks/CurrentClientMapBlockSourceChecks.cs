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
        await FastCityBatchReturnsFiveLogicalCaptures();
        await CoordinateJumpUsesOwnedNavigation();
        await MarchFollowUsesOwnedNavigation();
        await MarchFollowMapsUnavailableServerAndRejectsForeignSession();
        await ServerJumpProvesNoOpAndChangedTransition();
        await ServerJumpMapsRecoveredTimeoutContract();
        await ServerJumpRejectsForeignSessionResult();
        await TruckQuickRobPreservesExactIdentityAndOutcome();
        await TruckQuickRobMapsRejectedAndAmbiguousWithoutRetry();
        await TruckQuickRobRejectsForeignSessionResult();
        await DispatchPlunderPreservesExactIdentityAndOutcome();
        await DispatchPlunderMapsRejectedAndAmbiguousWithoutRetry();
        await DispatchPlunderRejectsForeignSessionResult();
        await AssetImageValidatesCachesAndRetriesSessionGap();
        await AssetImageRejectsInvalidPng();
        await FastCityBandReturnsTwoHundredFiftyLogicalCaptures();
        await FastCityFullMapReturnsAllLogicalCaptures();
        await FastResourceFullMapReturnsAllLogicalCaptures();
        await FastFullWorldResumesAfterOuterRetry();
        await FastMonsterResumesAfterEarlyReadyLoss();
        await FastMonsterFullMapReturnsAllLogicalCaptures();
        await FastMonsterCoarseLodReturnsAllLogicalCaptures();
        await FastMonsterCoarseIncludesDoomsdayBoss();
        await FastZombieBossCoarseLodPublishesOnlyZombieBosses();
        await FastMonsterCoarseFailureFailsFastWithoutCoverageFallback();
        await FastZombieBossCoarseFailureFailsFastWithoutCoverageFallback();
        await FastMonsterAcceptsAccumulatedProtectionTargets();
        await FastMonsterKnownInactiveProtectionSuppressesRemaining();
        await FastMonsterAllowsIncompleteMonsterProtectionDetail();
        await FastMonsterUnresolvedDetailPreservesGameOwnedCache();
        await FastMonsterAllowsDeduplicatedReadyCarryover();
        await FastTruckFullMapReturnsAllLogicalCaptures();
        await FastRailwayFullMapReturnsAllLogicalCaptures();
        await FastDispatchFullMapReturnsAllLogicalCaptures();
        await FastGhostFullMapReturnsAllLogicalCaptures();
        await FastTreasureFullMapReturnsAllLogicalCaptures();
        await FastAllEightFullMapReturnsAllLogicalCaptures();
        await FastFullMapFillsMeasuredCoverageHole();
        await FastFullMapRetriesTransientNonRectangularFootprint();
        await FastFailedBatchReportsNativeErrorBeforeSuccessFields();
        await FastFullMapAdaptsToMeasuredWideFootprints();
        await FastFullMapAcceptsFiveColumnFootprint();
        await HealthyGateRunsBeforeWorldReadyProtocol();
        await TransientReadyLossKeepsOwnedSessionIdentity();
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

    private static async Task FastCityBatchReturnsFiveLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                return ProvenFastCityBatch(fields, (200, 9, 9), (201, 9, 45));
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        int[] groupIndices = [0, 50, 100, 150, 200];
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], groupIndices.ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 1 && captures.Count == 5,
            "fast City source should satisfy one 1x5 logical-block group from one native response");
        Check(captures.Select(capture => capture.BlockIndex).SequenceEqual(groupIndices),
            "fast City batch logical-block ordering changed");
        Check(captures.Single(capture => capture.BlockIndex == 0).Records.Single().RecordKey == "200" &&
              captures.Single(capture => capture.BlockIndex == 100).Records.Single().RecordKey == "201" &&
              captures.Where(capture => capture.BlockIndex is not (0 or 100)).All(capture => capture.Records.Count == 0),
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
        Check(bulkCalls == 50 && captures.Count == 250,
            "fast City band should satisfy 250 logical blocks from 50 conservative native responses");
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
                if (x == 5 && y == 75) return ProvenFastCityBatch(fields, (200, 9, 9));
                if (x == 995 && y == 975) return ProvenFastCityBatch(fields, (300, 985, 985));
                return ProvenFastCityBatch(fields);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 270 && captures.Count == 2500,
            "fast full-City source should adapt to measured four-column interior footprints without weakening exact coverage");
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
                if (x == 5 && y == 75) return ProvenFastResourceBatch(fields, (400, 9, 9, 3, 2, true, false));
                if (x == 995 && y == 975) return ProvenFastResourceBatch(fields, (500, 985, 985, 10, 4, true, true));
                return ProvenFastResourceBatch(fields);
            },
            resourceDetailResult: fields => JsonSerializer.Serialize(new
            {
                schemaVersion = 1, probeVersion = "lwbridge-live-resource-probe-2",
                requestId = fields["requestId"], profileId = fields["profileId"],
                launchSessionId = fields["launchSessionId"], challenge = fields["challenge"],
                gamePid = int.Parse(fields["gamePid"]), serverId = int.Parse(fields["serverId"]),
                scanRunId = fields["scanRunId"], state = "completed", error = (string?)null,
                targetCount = 1, requestCount = 1, cacheBeforeCount = 0, sendFailureCount = 0, readyCount = 1,
                details = new[]
                {
                    new
                    {
                        recordKey = "400", received = true, resourceConfigId = 203, resourceNameKey = "iron",
                        resourceMaxAmount = 216000, requestIssued = true, cacheBefore = false, sendFailed = false,
                        detail = new { remainRes = 216000, initRes = 216000, speed = 0 },
                    },
                },
                capturedAt = Timestamp(),
            }, JsonOptions.Default));
        MapScanExecutionRequest request = Request("resource", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 270 && captures.Count == 2500, "fast full-Resource source should adapt to measured four-column interior footprints");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "resource" && first.RecordKey == "400" && first.Level == 3 &&
              last.Kind == "resource" && last.RecordKey == "500" && last.Level == 10,
            "fast full-Resource source did not preserve normalized resources at map extremes");
        Check(first.DataJson.Contains("\"resourceTypeId\":2", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"rebuildGatherOccupancyKnown\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"rebuildGatherOccupied\":false", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"resourceDetailKnown\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"resourceRemainingAmount\":216000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"resourceFullAmount\":216000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"resourceFull\":true", StringComparison.Ordinal) &&
              last.DataJson.Contains("\"rebuildGatherOccupied\":true", StringComparison.Ordinal) &&
              !last.DataJson.Contains("\"resourceDetailKnown\":true", StringComparison.Ordinal),
            "fast full-Resource source did not preserve occupancy or apply authoritative detail only to the idle target");
    }

    private static async Task FastFullWorldResumesAfterOuterRetry()
    {
        int bulkCalls = 0;
        int failedTargetAttempts = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                if (x == 35 && y == 75 && failedTargetAttempts++ < 3)
                {
                    JsonObject failed = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
                    failed["state"] = "failed";
                    failed["error"] = "synthetic_transient_batch_failure";
                    return failed.ToJsonString(JsonOptions.Default);
                }
                return ProvenFastCityBatch(fields);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlySet<int> pending = blocks.Select(block => block.BlockIndex).ToHashSet();
        try
        {
            _ = await source.CaptureBatchAsync(request, blocks[0], pending, CancellationToken.None);
            throw new InvalidOperationException("synthetic transient should exhaust the source-local retry budget once");
        }
        catch (InvalidDataException error) when (error.Message.Contains("synthetic_transient_batch_failure", StringComparison.Ordinal))
        {
        }
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], pending, CancellationToken.None);
        Check(captures.Count == 2500 && bulkCalls == 273,
            "outer retry should resume the adaptive full-world acquisition instead of repeating successful footprints instead of restarting successful footprints");
    }

    private static async Task FastMonsterResumesAfterEarlyReadyLoss()
    {
        bool ready = true;
        int bulkCalls = 0;
        int healthWaits = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            waitForHealthySession: (session, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                healthWaits++;
                Check(session == Session, "transient Monster readiness wait changed the owned session");
                ready = true;
                return Task.CompletedTask;
            },
            bulkResult: fields =>
            {
                bulkCalls++;
                if (bulkCalls == 1)
                {
                    ready = false;
                    throw new BridgeCommandException(
                        "GAME_CONNECTION_UNAVAILABLE",
                        "synthetic transient readiness loss before coarse Monster completion");
                }
                return ProvenCoarseMonsterBatch(fields);
            },
            useCoarseMonsterMap: true,
            sessionProvider: () => ready ? Session : null,
            matchesOwnedSession: session => session == Session);

        MapScanExecutionRequest request = new(
            "run_transient_monster", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlySet<int> pending = blocks.Select(block => block.BlockIndex).ToHashSet();

        try
        {
            _ = await source.CaptureBatchAsync(request, blocks[0], pending, CancellationToken.None);
            throw new InvalidOperationException("synthetic transient readiness loss should escape the first coarse attempt");
        }
        catch (BridgeCommandException error) when (error.Code == "GAME_CONNECTION_UNAVAILABLE")
        {
        }

        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], pending, CancellationToken.None);
        Check(captures.Count == 2500 && bulkCalls == 2 && healthWaits >= 3,
            "same-run Monster retry must retain its owned-session anchor while instantaneous readiness is absent");
    }

    private static async Task FastMonsterFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        int protectionCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                Check(fields["homeTileX"] == "230" && fields["homeTileY"] == "257",
                    "fast full-Monster source must pass the authoritative player home/base tile into every live distance capture");
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x == 5 && y == 75)
                {
                    return ProvenFastMonsterBatch(fields, ("m-first", 9, 9, 1002009, 9, "2000005", false));
                }
                if (x == 995 && y == 975) return ProvenFastMonsterBatch(fields, ("m-last", 985, 985, 1004029, 29, "2000005", false));
                return ProvenFastMonsterBatch(fields);
            },
            monsterProtectionResult: fields =>
            {
                protectionCalls++;
                return ProvenMonsterProtectionResult(fields, ("m-first", true, true, 2_000_000_000_000L));
            });
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 270 && protectionCalls == 0 && captures.Count == 2500,
            "generic Monster source should use exact AOI coverage without any Zombie Boss protection network phase");
        Check(source.LastMonsterProtectionDetailMetrics is { BossCount: 0, TargetCount: 0, RequestCount: 0, ReadyCount: 0 },
            "generic Monster source must not retain Zombie Boss protection work after the category split");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "monster" && first.RecordKey == "m-first" && first.Level == 9 &&
              last.Kind == "monster" && last.RecordKey == "m-last" && last.Level == 29,
            "fast full-Monster source did not preserve normalized monsters at map extremes");
        Check(first.DataJson.Contains("\"monsterNameKey\":\"2000005\"", StringComparison.Ordinal),
            "fast full-Monster source did not preserve the authoritative ordinary Monster name key");
        Check(first.Distance == 12.5 && first.ShieldEndTime is null &&
              first.DataJson.Contains("\"distanceFromHome\":12.5", StringComparison.Ordinal),
            "generic Monster source should persist game-derived distance without Zombie Boss Remaining state");
    }


    private static async Task FastMonsterCoarseLodReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        int protectionCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                Check(fields["requestMode"] == "zoom",
                    "coarse Monster path must use the native zoom/LOD2 whole-world request");
                return ProvenCoarseMonsterBatch(fields,
                    ("m-coarse", 500, 500, 1031015, 55, "2901012", true),
                    ("m-coarse-ordinary", 10, 990, 1002009, 9, "2000005", false));
            },
            monsterProtectionResult: fields =>
            {
                protectionCalls++;
                return ProvenMonsterProtectionResult(fields,
                    ("m-coarse", true, true, 2_000_000_000_000L));
            },
            useCoarseMonsterMap: true);
        MapScanExecutionRequest request = new("run_coarse", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 1 && protectionCalls == 0 && captures.Count == 2500,
            "coarse generic Monster path should use one whole-world snapshot and skip Zombie Boss detail requests");
        MapStoredRecord[] rows = captures.SelectMany(capture => capture.Records).ToArray();
        Check(rows.Length == 1 && rows[0].RecordKey == "m-coarse-ordinary" &&
              rows[0].Kind == "monster" && rows[0].ShieldEndTime is null,
            "generic Monster path must exclude Zombie Boss rows after the dedicated category split");
    }

    private static async Task FastMonsterCoarseIncludesDoomsdayBoss()
    {
        int genericCalls = 0;
        CurrentClientMapBlockSource generic = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                genericCalls++;
                return ProvenCoarseDoomsdayMonsterBatch(fields);
            },
            useCoarseMonsterMap: true);
        MapScanExecutionRequest genericRequest = new(
            "run_doomsday_monster", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> genericCaptures = await generic.CaptureBatchAsync(
            genericRequest, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        MapStoredRecord doom = genericCaptures.SelectMany(capture => capture.Records).Single();
        Check(genericCalls == 1 && genericCaptures.Count == 2500 &&
              doom.Kind == "monster" && doom.RecordKey == "doom-manager-1" && doom.Level == 60 &&
              doom.DataJson.Contains("\"monsterSpecialType\":32", StringComparison.Ordinal) &&
              doom.DataJson.Contains("\"configSpecial\":32", StringComparison.Ordinal) &&
              doom.DataJson.Contains("\"runtimeClass\":\"LWDoomsdayManager.BossVO\"", StringComparison.Ordinal) &&
              doom.DataJson.Contains("\"source\":\"DataCenter.LWDoomsdayManager.theaterBosses\"", StringComparison.Ordinal),
            "generic Monster coarse scan must preserve current-v20 SuperRunningBoss/Doom Walker manager rows");

        CurrentClientMapBlockSource zombie = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: ProvenCoarseDoomsdayMonsterBatch,
            useCoarseMonsterMap: true);
        MapScanExecutionRequest zombieRequest = new(
            "run_doomsday_zombie", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
        IReadOnlyList<MapScanBlockCapture> zombieCaptures = await zombie.CaptureBatchAsync(
            zombieRequest, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(zombieCaptures.SelectMany(capture => capture.Records).Count() == 0,
            "Doom Walker must remain excluded from the dedicated Zombie Boss category");
    }

    private static async Task FastZombieBossCoarseLodPublishesOnlyZombieBosses()
    {
        int bulkCalls = 0;
        int protectionCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                Check(fields["requestMode"] == "zoom",
                    "Zombie Boss path must use the native LOD2 whole-world Monster snapshot");
                return ProvenCoarseMonsterBatch(fields,
                    ("z-boss", 500, 500, 1031015, 65, "2901012", true),
                    ("z-ordinary", 510, 510, 1002009, 9, "2000005", false));
            },
            monsterProtectionResult: fields =>
            {
                protectionCalls++;
                return ProvenMonsterProtectionResult(fields,
                    ("z-boss", true, true, 2_000_000_000_000L));
            },
            useCoarseMonsterMap: true);

        MapScanExecutionRequest request = new(
            "run_zombie_boss", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);

        MapStoredRecord[] rows = captures.SelectMany(capture => capture.Records).ToArray();
        Check(bulkCalls == 1 && protectionCalls == 1 && captures.Count == 2500,
            "Zombie Boss path should use one LOD2 snapshot plus one serialized detail pass");
        Check(rows.Length == 1 && rows[0].Kind == "zombie_boss" && rows[0].RecordKey == "z-boss",
            "Zombie Boss scan must publish only authentic protection-eligible Zombie Boss rows into its separate kind");
        Check(rows[0].Level == 65 && rows[0].Distance is not null &&
              rows[0].ShieldEndTime == 2_000_000_000_000L &&
              rows[0].DataJson.Contains("\"kind\":\"zombie_boss\"", StringComparison.Ordinal),
            "Zombie Boss rows must preserve level, Distance, Remaining, and dedicated result identity");
    }

    private static async Task FastMonsterCoarseFailureFailsFastWithoutCoverageFallback()
    {
        int zoomCalls = 0;
        int coverageCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                if (fields["requestMode"] == "zoom")
                {
                    zoomCalls++;
                    JsonObject failed = JsonNode.Parse(ProvenCoarseMonsterBatch(fields))!.AsObject();
                    failed["state"] = "failed";
                    failed["error"] = "synthetic_coarse_failure";
                    return failed.ToJsonString(JsonOptions.Default);
                }
                coverageCalls++;
                return ProvenFastMonsterBatch(fields);
            },
            useCoarseMonsterMap: true);
        MapScanExecutionRequest request = new("run_fast_only", 2212, 0, 1000, 1000, ["monster"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        try
        {
            _ = await source.CaptureBatchAsync(
                request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
            throw new InvalidOperationException("synthetic coarse failure should fail the fast-only Monster scan");
        }
        catch (InvalidDataException error) when (error.Message.Contains("synthetic_coarse_failure", StringComparison.Ordinal))
        {
        }
        Check(zoomCalls == 1 && coverageCalls == 0,
            "Monster coarse failure must fail fast without entering the conservative LOD0 coverage scanner");
    }

    private static async Task FastZombieBossCoarseFailureFailsFastWithoutCoverageFallback()
    {
        int zoomCalls = 0;
        int coverageCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                if (fields["requestMode"] == "zoom")
                {
                    zoomCalls++;
                    JsonObject failed = JsonNode.Parse(ProvenCoarseMonsterBatch(fields))!.AsObject();
                    failed["state"] = "failed";
                    failed["error"] = "synthetic_zombie_coarse_failure";
                    return failed.ToJsonString(JsonOptions.Default);
                }
                coverageCalls++;
                return ProvenFastMonsterBatch(fields);
            },
            useCoarseMonsterMap: true);
        MapScanExecutionRequest request = new(
            "run_zombie_fast_only", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        try
        {
            _ = await source.CaptureBatchAsync(
                request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
            throw new InvalidOperationException("synthetic Zombie Boss coarse failure should fail the fast-only scan");
        }
        catch (InvalidDataException error) when (error.Message.Contains("synthetic_zombie_coarse_failure", StringComparison.Ordinal))
        {
        }
        Check(zoomCalls == 1 && coverageCalls == 0,
            "Zombie Boss coarse failure must fail fast without entering the conservative LOD0 coverage scanner");
    }

    private static async Task FastMonsterAcceptsAccumulatedProtectionTargets()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x != 5 || y != 75) return ProvenFastMonsterBatch(fields);
                return ProvenFastMonsterBatch(fields, ("m-final", 9, 9, 1031015, 60, "2901012", true));
            },
            monsterProtectionResult: fields => ProvenMonsterProtectionResult(fields,
                ("m-final", true, true, 2_000_000_000_000L),
                ("m-stale", true, false, 0L)));
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        MapStoredRecord row = captures.Single(c => c.BlockIndex == 0).Records.Single();
        Check(row.ShieldEndTime == 2_000_000_000_000L,
            "final Zombie Boss must keep its authoritative protection deadline when scan target history is a superset");
        Check(source.LastMonsterProtectionDetailMetrics is { BossCount: 1, TargetCount: 2, ReadyCount: 2 },
            "accumulated moving/unloaded Zombie Boss targets must remain observable without invalidating final rows");
    }

    private static async Task FastMonsterKnownInactiveProtectionSuppressesRemaining()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x != 5 || y != 75) return ProvenFastMonsterBatch(fields);
                JsonObject root = JsonNode.Parse(ProvenFastMonsterBatch(fields, ("m-inactive", 9, 9, 1002009, 9, "2901012", true)))!.AsObject();
                JsonObject row = root["monster_march_records"]!.AsArray()[0]!.AsObject();
                row["monsterProtectionKnown"] = true; row["monsterProtectionActive"] = false;
                row["monsterProtectionEndTime"] = 0L; row["zMBossShieldEndTime"] = 2_000_000_000_000L;
                root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                root["monsterProtectionDetailRequestCount"] = 1; root["monsterProtectionDetailReadyCount"] = 1;
                return root.ToJsonString(JsonOptions.Default);
            },
            monsterProtectionResult: fields =>
                ProvenMonsterProtectionResult(fields, ("m-inactive", true, false, 0L)));
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
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
                JsonObject root = JsonNode.Parse(x == 5 && y == 75
                    ? ProvenFastMonsterBatch(fields, ("m-timeout", 9, 9, 1031015, 20, "2901012", true))
                    : ProvenFastMonsterBatch(fields))!.AsObject();
                if (x == 5 && y == 75)
                {
                    root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                    root["monsterProtectionDetailRequestCount"] = 1; root["monsterProtectionDetailReadyCount"] = 0;
                    JsonObject row = root["monster_march_records"]!.AsArray()[0]!.AsObject();
                    row["monsterProtectionKnown"] = false; row["monsterProtectionActive"] = false;
                    row["monsterProtectionEndTime"] = 0; row["zMBossShieldEndTime"] = 2_000_000_000_000L;
                }
                return root.ToJsonString(JsonOptions.Default);
            },
            monsterProtectionResult: fields =>
                ProvenMonsterProtectionResult(fields, ("m-timeout", false, false, 0L)));
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
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

    private static async Task FastMonsterUnresolvedDetailPreservesGameOwnedCache()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                JsonObject root = JsonNode.Parse(x == 5 && y == 75
                    ? ProvenFastMonsterBatch(fields, ("m-first", 9, 9, 1031015, 55, "2901012", true))
                    : ProvenFastMonsterBatch(fields))!.AsObject();
                if (x == 5 && y == 75)
                {
                    root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                    root["monsterProtectionDetailRequestCount"] = 1; root["monsterProtectionDetailReadyCount"] = 0;
                }
                return root.ToJsonString(JsonOptions.Default);
            },
            monsterProtectionResult: fields =>
                ProvenMonsterProtectionResult(fields, ("m-first", false, false, 0L)));
        MapScanExecutionRequest request = new("run_cache", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);

        MapStoredRecord row = captures.Single(c => c.BlockIndex == 0).Records.Single();
        Check(row.ShieldEndTime == 2_000_000_000_000L &&
              row.DataJson.Contains("\"monsterProtectionKnown\":true", StringComparison.Ordinal) &&
              row.DataJson.Contains("\"monsterProtectionActive\":true", StringComparison.Ordinal),
            "an unresolved optional detail reply must not erase game-owned Monster Protection state captured before enrichment");
    }

    private static async Task FastMonsterAllowsDeduplicatedReadyCarryover()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x != 5 || y != 75) return ProvenFastMonsterBatch(fields);
                JsonObject root = JsonNode.Parse(ProvenFastMonsterBatch(fields, ("m-carry", 9, 9, 1031015, 20, "2901012", true)))!.AsObject();
                root["monsterInvasionBossCount"] = 1; root["monsterProtectionDetailTargetCount"] = 1;
                root["monsterProtectionDetailRequestCount"] = 0; root["monsterProtectionDetailReadyCount"] = 1;
                return root.ToJsonString(JsonOptions.Default);
            },
            monsterProtectionResult: fields =>
                ProvenMonsterProtectionResult(fields, ("m-carry", true, false, 0L)));
        MapScanExecutionRequest request = new("run_1", 2212, 0, 1000, 1000, ["zombie_boss"], 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(captures.Count == 2500,
            "a ready protection response carried from an overlapping AOI must not invalidate the full Monster scan");
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
                if (x == 5 && y == 75)
                {
                    JsonObject root = JsonNode.Parse(ProvenFastTruckBatch(fields, ("t-first", 9, 9, 86, 3, 1, "Driver A", 4_152_318L)))!.AsObject();
                    JsonObject row = root["train_march_records"]!.AsArray()[0]!.AsObject();
                    row["arriveTs"] = 1_789_615_774_078L;
                    row["robTimes"] = 2;
                    row["protectTime"] = 1_789_616_000_000L;
                    row["truckMetadataKnown"] = true;
                    row["truckVipOn"] = false;
                    row["truckMaxLootCount"] = 3;
                    row["truckCurrentGoodsRaw"] = JsonNode.Parse("[{\"type\":7,\"itemId\":\"200364\",\"count\":1,\"name\":\"Recovered Item A\",\"iconPath\":\"Assets/Item/a.png\"},{\"type\":7,\"itemId\":\"2270000\",\"count\":1,\"name\":\"Recovered Item B\",\"iconPath\":\"Assets/Item/b.png\"},{\"type\":7,\"itemId\":\"2270000\",\"count\":1,\"name\":\"Recovered Item B\",\"iconPath\":\"Assets/Item/b.png\"},{\"type\":1,\"itemId\":1,\"count\":3807500,\"name\":\"Recovered Resource\",\"iconPath\":\"Assets/Reward/resource.png\"}]");
                    // Retain split arrays too; the direct game GetCurRewardData result above must win
                    // rather than being double-counted with these fallback fields.
                    row["truckExtraGoodsCur"] = JsonNode.Parse("[{\"type\":7,\"value\":{\"id\":\"200364\",\"num\":1}},{\"type\":7,\"value\":{\"id\":\"2270000\",\"num\":1}}]");
                    row["truckBaseGoodsCur"] = JsonNode.Parse("[{\"type\":7,\"value\":{\"id\":\"2270000\",\"num\":1}},{\"type\":1,\"value\":3807500}]");
                    return root.ToJsonString(JsonOptions.Default);
                }
                if (x == 995 && y == 975)
                {
                    JsonObject root = JsonNode.Parse(ProvenFastTruckBatch(fields, ("t-last", 985, 985, 87, 10, 2, "Driver B", 9_000_000L)))!.AsObject();
                    JsonObject row = root["train_march_records"]!.AsArray()[0]!.AsObject();
                    row["robTimes"] = 1;
                    row["truckMetadataKnown"] = true;
                    row["truckVipOn"] = true;
                    row["truckMaxLootCount"] = 2;
                    return root.ToJsonString(JsonOptions.Default);
                }
                return ProvenFastTruckBatch(fields);
            });
        MapScanExecutionRequest request = Request("truck", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 270 && captures.Count == 2500, "fast full-Truck source should adapt to measured four-column interior footprints");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "truck" && first.RecordKey == "t-first" && first.Quality == 3 && first.Power == 4_152_318L &&
              last.Kind == "truck" && last.RecordKey == "t-last" && last.Quality == 10 && last.Power == 9_000_000L &&
              last.DataJson.Contains("\"isSpecialURQuality\":true", StringComparison.Ordinal),
            "fast full-Truck source did not preserve live train identity/quality/power/special-UR state at map extremes");
        Check(first.Name == "Driver A" && first.DataJson.Contains("\"trainType\":1", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"trainCfgId\":86", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"arriveTs\":1789615774078", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"robTimes\":2", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"protectTime\":1789616000000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"maxLootCount\":3", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"remainingLootCount\":1", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"currentGoods\":[", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"reward:7:200364\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"reward:7:2270000\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"count\":2", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"reward:1:1\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"count\":3807500", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"name\":\"Recovered Item B\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"iconPath\":\"Assets/Item/b.png\"", StringComparison.Ordinal) &&
              !first.DataJson.Contains("\"trainDataJson\"", StringComparison.Ordinal) &&
              !last.DataJson.Contains("\"trainDataJson\"", StringComparison.Ordinal) &&
              last.DataJson.Contains("\"maxLootCount\":2", StringComparison.Ordinal) &&
              last.DataJson.Contains("\"remainingLootCount\":0", StringComparison.Ordinal),
            "fast full-Truck source did not reconstruct current-v19 GetCurRewardData/maxLootPerTrain metadata, derive frontend-compatible remaining loot, aggregate string-ID rewards, or preserve exact VIP-adjusted max loot");
    }

    private static async Task FastRailwayFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]); int y = int.Parse(fields["targetTileY"]);
                if (x == 5 && y == 75)
                {
                    JsonObject root = JsonNode.Parse(ProvenFastRailwayBatch(fields, ("r-first", 9, 9, 201, 4, 3, "Conductor A", 5_500_000L)))!.AsObject();
                    JsonObject row = root["train_march_records"]!.AsArray()[0]!.AsObject();
                    row.Remove("trainType");
                    row["trainDataJson"] = "{\"type\":2,\"arriveTime\":1789616774078,\"marchInfo\":{\"robTimes\":1,\"protectTime\":1789616000000}}";
                    row["maxLootCount"] = 3;
                    row["currentGoods"] = new JsonArray(
                        new JsonObject
                        {
                            ["key"] = "reward:7:650053",
                            ["name"] = "Recovered Rail Reward",
                            ["iconPath"] = "Assets/Main/Sprites/UI/Item/recovered.png",
                            ["count"] = 1234,
                            ["rewardType"] = 7,
                            ["itemId"] = 650053,
                        });
                    return root.ToJsonString(JsonOptions.Default);
                }
                if (x == 995 && y == 975) return ProvenFastRailwayBatch(fields, ("r-last", 985, 985, 202, 5, 4, "Conductor B", 11_000_000L));
                return ProvenFastRailwayBatch(fields);
            });
        MapScanExecutionRequest request = Request("railway", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(bulkCalls == 270 && captures.Count == 2500, "fast full-Railway source should adapt to measured four-column interior footprints");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "railway" && first.RecordKey == "r-first" && first.Quality == 4 && first.Power == 5_500_000L &&
              last.Kind == "railway" && last.RecordKey == "r-last" && last.Quality == 5 && last.Power == 11_000_000L,
            "fast full-Railway source did not preserve game TrainType.Train identity/quality/power at map extremes");
        Check(first.Name == "Conductor A" && first.DataJson.Contains("\"trainType\":2", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"trainCfgId\":201", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"marchUuid\":\"r-first\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"arriveTs\":1789616774078", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"robTimes\":1", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"protectTime\":1789616000000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"maxLootCount\":3", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"key\":\"reward:7:650053\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"name\":\"Recovered Rail Reward\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"count\":1234", StringComparison.Ordinal),
            "fast full-Railway source did not preserve source-backed train metadata including protectTime/currentGoods");
    }

    private static async Task FastDispatchFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                if (x == 5 && y == 75)
                    return ProvenFastDispatchBatch(fields,
                        ("90010", 90010, 9, 9, 3101, 5, 4, true, 1_789_616_000_000L, "dispatch-owner-a"));
                if (x == 995 && y == 975)
                    return ProvenFastDispatchBatch(fields,
                        ("985986", 985986, 985, 985, 3102, 7, 5, false, 1_789_617_000_000L, "dispatch-owner-b"));
                return ProvenFastDispatchBatch(fields);
            });

        MapScanExecutionRequest request = Request("dispatch", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);

        Check(bulkCalls == 270 && captures.Count == 2500,
            "fast full-Dispatch source should adapt to measured four-column interior footprints");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "dispatch" && first.RecordKey == "90010" && first.PointIndex == 90010 &&
              first.Uuid == "90010" && first.Level == 5 && first.Quality == 4 &&
              last.Kind == "dispatch" && last.RecordKey == "985986" && last.PointIndex == 985986 &&
              last.Level == 7 && last.Quality == 5,
            "fast full-Dispatch source did not preserve point identity/config metadata at map extremes");
        Check(first.DataJson.Contains("\"pointType\":17", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"runtimeClass\":\"HeroDispatchMissionPointInfo\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"cfgId\":3101", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"isSpecial\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"completionTime\":1789616000000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"plunderAt\":1789616300000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"stolenCount\":1", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"maxStealCount\":3", StringComparison.Ordinal) &&
              !first.DataJson.Contains("\"taskExpireTime\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"ownerUid\":\"dispatch-owner-a\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"kind\":\"dispatch\"", StringComparison.Ordinal),
            "fast full-Dispatch source did not preserve authoritative HeroDispatchMissionPointInfo fields");
    }

    private static async Task FastGhostFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                if (x == 5 && y == 75)
                    return ProvenFastGhostBatch(fields,
                        ("ghost-90010", 90010, 9, 9, 4101, 6, 5, true, 1_789_616_000_000L, "ghost-owner-a"));
                if (x == 995 && y == 975)
                    return ProvenFastGhostBatch(fields,
                        ("ghost-985986", 985986, 985, 985, 4102, 8, 4, false, 1_789_617_000_000L, "ghost-owner-b"));
                return ProvenFastGhostBatch(fields);
            });

        MapScanExecutionRequest request = Request("ghost", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);

        Check(bulkCalls == 270 && captures.Count == 2500,
            "fast full-Ghost source should adapt to measured four-column interior footprints");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "ghost" && first.RecordKey == "90010" && first.PointIndex == 90010 &&
              first.Uuid == "ghost-90010" && first.Level == 6 && first.Quality == 5 &&
              last.Kind == "ghost" && last.RecordKey == "985986" && last.PointIndex == 985986 &&
              last.Level == 8 && last.Quality == 4,
            "fast full-Ghost source did not preserve point identity/config metadata at map extremes");
        Check(first.DataJson.Contains("\"pointType\":29", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"runtimeClass\":\"GhostreconPointInfo\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"cfgId\":4101", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"isSpecial\":true", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"completionTime\":1789616000000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"taskExpireTime\":1789623200000", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"ownerUid\":\"ghost-owner-a\"", StringComparison.Ordinal) &&
              first.DataJson.Contains("\"kind\":\"ghost\"", StringComparison.Ordinal),
            "fast full-Ghost source did not preserve authoritative GhostreconPointInfo fields");
    }

    private static async Task FastTreasureFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                if (x == 5 && y == 75)
                    return ProvenFastTreasureBatch(fields,
                        (false, "treasure-u1", 90010, 9, 9, 5));
                if (x == 995 && y == 975)
                    return ProvenFastTreasureBatch(fields,
                        (true, "supplies-u2", 985986, 985, 985, 3));
                return ProvenFastTreasureBatch(fields);
            });

        MapScanExecutionRequest request = Request("treasure", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);

        Check(bulkCalls == 270 && captures.Count == 2500,
            "fast full-Treasure source should adapt to measured four-column interior footprints");
        MapStoredRecord first = captures.Single(c => c.BlockIndex == 0).Records.Single();
        MapStoredRecord last = captures.Single(c => c.BlockIndex == 2499).Records.Single();
        Check(first.Kind == "treasure" && first.RecordKey == "90010" && first.PointIndex == 90010 &&
              first.Uuid == "treasure-u1" &&
              last.Kind == "treasure" && last.RecordKey == "985986" && last.PointIndex == 985986 &&
              last.Uuid == "supplies-u2",
            "fast full-Treasure source did not preserve original point-index record identity at map extremes");
        using JsonDocument firstTreasure = JsonDocument.Parse(first.DataJson);
        using JsonDocument lastTreasure = JsonDocument.Parse(last.DataJson);
        JsonElement firstData = firstTreasure.RootElement;
        JsonElement lastData = lastTreasure.RootElement;
        Check(firstData.GetProperty("pointType").GetInt32() == 21 &&
              firstData.GetProperty("runtimeClass").GetString() == "TreasurePointInfo" &&
              firstData.GetProperty("treasureType").GetInt32() == 5 &&
              firstData.GetProperty("suppliesType").GetInt32() == 0 &&
              firstData.GetProperty("remainingBoxes").GetInt32() == 3 &&
              firstData.GetProperty("source").GetString() == "WorldPointManager._pointInfos+TreasurePointInfo" &&
              lastData.GetProperty("pointType").GetInt32() == 27 &&
              lastData.GetProperty("runtimeClass").GetString() == "WorldSuppliesPoint" &&
              lastData.GetProperty("treasureType").GetInt32() == 0 &&
              lastData.GetProperty("suppliesType").GetInt32() == 3 &&
              lastData.GetProperty("configId").GetInt32() == 7003 &&
              lastData.GetProperty("source").GetString() == "WorldPointManager._pointInfos+WorldSuppliesPoint+TableName.LWIceSupplies",
            "fast full-Treasure source did not preserve the recovered ordinary-treasure/supplies split");
    }

    private static async Task FastAllEightFullMapReturnsAllLogicalCaptures()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                return ProvenFastAllEightBatch(fields, x == 5 && y == 75);
            });

        var request = new MapScanExecutionRequest(
            "run_all_eight", 2212, 0, 1000, 1000,
            MapScanContract.RecoveredDefaultTypes, 8, 2, 230, 257);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);

        MapStoredRecord[] records = captures.SelectMany(capture => capture.Records).ToArray();
        string[] kinds = records.Select(record => record.Kind).OrderBy(kind => kind, StringComparer.Ordinal).ToArray();
        string[] expectedKinds = MapScanContract.RecoveredDefaultTypes.OrderBy(kind => kind, StringComparer.Ordinal).ToArray();
        Check(bulkCalls == 270 && captures.Count == 2500 && kinds.SequenceEqual(expectedKinds),
            "full-world all-eight source should cover 2,500 logical blocks once and preserve one row of every recovered kind");
        Check(records.All(record => record.ServerId == 2212),
            "full-world all-eight source must retain one server scope across every selected kind");
    }

    private static async Task FastFullMapFillsMeasuredCoverageHole()
    {
        int bulkCalls = 0;
        bool narrowedOnce = false;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                int x = int.Parse(fields["targetTileX"]);
                int y = int.Parse(fields["targetTileY"]);
                string result = ProvenFastCityBatch(fields);
                if (narrowedOnce || x != 35 || y != 75) return result;
                narrowedOnce = true;
                JsonObject root = JsonNode.Parse(result)!.AsObject();
                JsonArray indices = root["requestedIndices"]!.AsArray();
                int maxColumn = indices.Select(node => node!.GetValue<int>() % 100).Max();
                JsonArray reduced = new(indices
                    .Select(node => node!.GetValue<int>())
                    .Where(index => index % 100 != maxColumn)
                    .Select(index => (JsonNode?)JsonValue.Create(index))
                    .ToArray());
                root["requestedIndices"] = reduced;
                root["nativeCurrentSetCount"] = reduced.Count;
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(captures.Count == 2500 && narrowedOnce && bulkCalls >= 270 && bulkCalls < 340,
            "adaptive full-world acquisition should fill a measured narrow-footprint hole instead of publishing incomplete coverage");
    }

    private static async Task FastFullMapRetriesTransientNonRectangularFootprint()
    {
        int bulkCalls = 0;
        bool malformedOnce = false;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                string result = ProvenFastCityBatch(fields);
                if (malformedOnce) return result;
                malformedOnce = true;
                JsonObject root = JsonNode.Parse(result)!.AsObject();
                JsonArray indices = root["requestedIndices"]!.AsArray();
                indices.RemoveAt(indices.Count - 1);
                root["nativeCurrentSetCount"] = indices.Count;
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(captures.Count == 2500 && malformedOnce && bulkCalls >= 271 && bulkCalls < 341,
            "transient non-rectangular fast AOI footprint should retry inside the bounded probe loop instead of failing the full scan");
    }

    private static async Task FastFailedBatchReportsNativeErrorBeforeSuccessFields()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
                root["state"] = "failed";
                root["error"] = "synthetic_pending_native_state";
                root.Remove("holdMilliseconds");
                root.Remove("viewLevel");
                root.Remove("targetTileX");
                root.Remove("targetTileY");
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        try
        {
            _ = await source.CaptureBatchAsync(
                request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
            throw new InvalidOperationException("failed Fast batch should exhaust the bounded source-local retry");
        }
        catch (InvalidDataException error)
        {
            Check(error.Message == "Fast world batch failed: synthetic_pending_native_state" && bulkCalls == 3,
                "failed Fast batch must surface the native failure before requiring success-only acquisition fields");
        }
    }

    private static async Task FastFullMapAdaptsToMeasuredWideFootprints()
    {
        int bulkCalls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                return ProvenWideFastCityBatch(fields);
            });
        MapScanExecutionRequest request = Request("city", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(captures.Count == 2500 && bulkCalls > 270 && bulkCalls < 340,
            "adaptive full-world acquisition should use measured mixed three/four-column footprints to reduce request count without weakening exact coverage");
    }

    private static async Task FastFullMapAcceptsFiveColumnFootprint()
    {
        int bulkCalls = 0;
        bool fiveColumnObserved = false;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            bulkResult: fields =>
            {
                bulkCalls++;
                JsonObject root = JsonNode.Parse(ProvenFastDispatchBatch(fields))!.AsObject();
                int targetX = int.Parse(fields["targetTileX"]);
                int targetY = int.Parse(fields["targetTileY"]);
                if (!fiveColumnObserved && targetX == 35 && targetY == 75)
                {
                    fiveColumnObserved = true;
                    int targetCellX = targetX / 10;
                    int startCellX = targetCellX - 2;
                    int startCellY = Math.Clamp((targetY / 10) - 7, 0, 90);
                    int[] requested = Enumerable.Range(startCellY, 10)
                        .SelectMany(row => Enumerable.Range(startCellX, 5)
                            .Select(column => row * 100 + column))
                        .ToArray();
                    root["requestedIndices"] = JsonSerializer.SerializeToNode(requested, JsonOptions.Default);
                    root["nativeCurrentSetCount"] = requested.Length;
                }
                return root.ToJsonString(JsonOptions.Default);
            });
        MapScanExecutionRequest request = Request("dispatch", 1000, 1000, worldId: 0);
        IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(1000, 1000);
        IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(
            request, blocks[0], blocks.Select(block => block.BlockIndex).ToHashSet(), CancellationToken.None);
        Check(captures.Count == 2500 && fiveColumnObserved && bulkCalls < 340,
            "current-v19 five-column native AOI footprints must remain rectangular, accepted, and complete the full-world Dispatch sweep");
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

    private static async Task MarchFollowUsesOwnedNavigation()
    {
        const long SyntheticMarchUuid = 7654321090123;
        int followWrites = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("march-follow.txt", StringComparison.OrdinalIgnoreCase)) followWrites++;
            },
            marchFollowResult: fields => MarchFollowResult(
                fields,
                "proven",
                null,
                int.Parse(fields["serverId"])));

        CurrentClientMarchFollowResult result =
            await source.FollowMarchAsync(2212, SyntheticMarchUuid, CancellationToken.None);

        Check(
            followWrites == 1 &&
            result.ServerId == 2212 &&
            result.MarchUuid == SyntheticMarchUuid,
            "march Follow must use one correlated owned-session request and preserve the 64-bit march UUID");
    }

    private static async Task MarchFollowMapsUnavailableServerAndRejectsForeignSession()
    {
        const long SyntheticMarchUuid = 7654321090123;
        CurrentClientMapBlockSource unavailable = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            marchFollowResult: fields => MarchFollowResult(
                fields,
                "failed",
                "current_server_id_unavailable",
                0));
        try
        {
            _ = await unavailable.FollowMarchAsync(2212, SyntheticMarchUuid, CancellationToken.None);
            throw new InvalidOperationException("unavailable current server should fail with the recovered public contract");
        }
        catch (BridgeCommandException error) when (
            error.Code == "SERVER_UNAVAILABLE" &&
            error.Message == "current server id unavailable")
        {
        }

        CurrentClientMapBlockSource foreign = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            marchFollowResult: fields =>
            {
                using JsonDocument document = JsonDocument.Parse(MarchFollowResult(
                    fields,
                    "proven",
                    null,
                    int.Parse(fields["serverId"])));
                JsonObject root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
                root["sessionId"] = "foreign-session";
                return root.ToJsonString(JsonOptions.Default);
            });
        try
        {
            _ = await foreign.FollowMarchAsync(2212, SyntheticMarchUuid, CancellationToken.None);
            throw new InvalidOperationException("foreign-session Follow result should fail closed");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static async Task ServerJumpProvesNoOpAndChangedTransition()
    {
        int jumpWrites = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            serverJumpResult: fields =>
            {
                jumpWrites++;
                int target = int.Parse(fields["serverId"]);
                return ServerJumpResult(fields, 2212, target, "proven", null);
            });

        CurrentClientServerJumpResult same =
            await source.JumpToServerAsync(2212, CancellationToken.None);
        CurrentClientServerJumpResult changed =
            await source.JumpToServerAsync(2213, CancellationToken.None);

        Check(jumpWrites == 2 &&
              same.PreviousServerId == 2212 && !same.Changed &&
              changed.PreviousServerId == 2212 && changed.Changed,
            "server Jump must accept a proven same-server no-op and a proven changed transition");
    }

    private static async Task ServerJumpMapsRecoveredTimeoutContract()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            serverJumpResult: fields =>
                ServerJumpResult(fields, 2212, 2212, "failed", "server_jump_timeout"));

        try
        {
            _ = await source.JumpToServerAsync(2213, CancellationToken.None);
            throw new InvalidOperationException("server-jump timeout should fail with the recovered public contract");
        }
        catch (BridgeCommandException error) when (
            error.Code == "SERVER_JUMP_TIMEOUT" &&
            error.Message == "the game did not switch to the target server")
        {
        }
    }

    private static async Task ServerJumpRejectsForeignSessionResult()
    {
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            serverJumpResult: fields =>
            {
                JsonObject root = JsonNode.Parse(
                    ServerJumpResult(fields, 2212, 2213, "proven", null))!.AsObject();
                root["sessionId"] = "foreign_session";
                return root.ToJsonString(JsonOptions.Default);
            });

        try
        {
            _ = await source.JumpToServerAsync(2213, CancellationToken.None);
            throw new InvalidOperationException("foreign server-jump result should fail closed");
        }
        catch (InvalidDataException)
        {
        }
    }


    private static async Task TruckQuickRobPreservesExactIdentityAndOutcome()
    {
        const long MarchUuid = 1_417_409_824_803_038_247L;
        const long TrainUuid = 1_417_409_824_803_038_999L;
        int protocolWrites = 0;
        int calls = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("truck-quick-rob.txt", StringComparison.OrdinalIgnoreCase))
                    protocolWrites++;
            },
            truckQuickRobResult: fields =>
            {
                Check(fields["marchUuid"] == MarchUuid.ToString() &&
                      fields["trainUuid"] == TrainUuid.ToString(),
                    "Truck quick-rob request must preserve both 64-bit target IDs as exact decimal text");
                bool won = calls++ == 0;
                return TruckQuickRobResult(
                    fields,
                    state: "proven",
                    error: null,
                    requestSent: true,
                    battleWon: won,
                    rewardCount: won ? 3 : 0,
                    dailyRobCount: won ? 7 : null);
            });

        CurrentClientTruckQuickRobResult win = await source.ExecuteTruckQuickRobAsync(
            2212, MarchUuid, TrainUuid, CancellationToken.None);
        CurrentClientTruckQuickRobResult loss = await source.ExecuteTruckQuickRobAsync(
            2212, MarchUuid, TrainUuid, CancellationToken.None);

        Check(protocolWrites == 2 &&
              win.ServerId == 2212 &&
              win.MarchUuid == MarchUuid &&
              win.TrainUuid == TrainUuid &&
              win.BattleWon &&
              win.RewardCount == 3 &&
              win.RewardNormalizationComplete &&
              win.PlunderRewards.GetArrayLength() == 1 &&
              win.PlunderRewards[0].GetProperty("key").GetString() == "reward:1:1001" &&
              win.PlunderRewards[0].GetProperty("count").GetInt32() == 25 &&
              win.DailyRobCount == 7 &&
              !loss.BattleWon &&
              loss.RewardCount == 0 &&
              loss.RewardNormalizationComplete &&
              loss.PlunderRewards.GetArrayLength() == 0 &&
              loss.DailyRobCount is null,
            "Truck quick-rob host protocol must preserve exact identity, normalized rewards and authoritative win/loss orientation");

        CurrentClientMapBlockSource incompleteRewards = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            truckQuickRobResult: fields => TruckQuickRobResult(
                fields,
                state: "proven",
                error: null,
                requestSent: true,
                battleWon: true,
                rewardCount: 1,
                rewardNormalizationComplete: false));
        CurrentClientTruckQuickRobResult incomplete = await incompleteRewards.ExecuteTruckQuickRobAsync(
            2212, MarchUuid, TrainUuid, CancellationToken.None);
        Check(incomplete.BattleWon &&
              !incomplete.RewardNormalizationComplete &&
              incomplete.PlunderRewards.GetArrayLength() == 1,
            "incomplete reward display normalization must remain a proven one-shot battle result rather than becoming retryable");
    }

    private static async Task TruckQuickRobMapsRejectedAndAmbiguousWithoutRetry()
    {
        const long MarchUuid = 1_417_409_824_803_038_247L;
        const long TrainUuid = 1_417_409_824_803_038_999L;

        int rejectedWrites = 0;
        CurrentClientMapBlockSource rejected = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("truck-quick-rob.txt", StringComparison.OrdinalIgnoreCase))
                    rejectedWrites++;
            },
            truckQuickRobResult: fields => TruckQuickRobResult(
                fields,
                state: "failed",
                error: "train_attack_rejected",
                requestSent: true,
                battleWon: null,
                rewardCount: null));
        try
        {
            _ = await rejected.ExecuteTruckQuickRobAsync(2212, MarchUuid, TrainUuid, CancellationToken.None);
            throw new InvalidOperationException("rejected train.attack should fail with a terminal Truck error");
        }
        catch (BridgeCommandException error) when (
            error.Code == "TRUCK_PLUNDER_SERVER_REJECTED" &&
            error.Message == "train attack rejected by the game")
        {
        }
        Check(rejectedWrites == 1,
            "terminal Truck rejection must use one train.attack protocol request without a hidden retry");

        int ambiguousWrites = 0;
        CurrentClientMapBlockSource ambiguous = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("truck-quick-rob.txt", StringComparison.OrdinalIgnoreCase))
                    ambiguousWrites++;
            },
            truckQuickRobResult: fields => TruckQuickRobResult(
                fields,
                state: "ambiguous",
                error: "server_response_timeout",
                requestSent: true,
                battleWon: null,
                rewardCount: null));
        try
        {
            _ = await ambiguous.ExecuteTruckQuickRobAsync(2212, MarchUuid, TrainUuid, CancellationToken.None);
            throw new InvalidOperationException("post-send Truck timeout must remain ambiguous");
        }
        catch (BridgeCommandException error) when (
            error.Code == "TRUCK_PLUNDER_RESPONSE_TIMEOUT" &&
            error.Message == "server response timeout")
        {
            JsonElement details = JsonSerializer.SerializeToElement(error.Details, JsonOptions.Default);
            Check(details.GetProperty("ambiguous").GetBoolean() &&
                  details.GetProperty("requestSent").GetBoolean() &&
                  details.GetProperty("marchUuid").GetString() == MarchUuid.ToString() &&
                  details.GetProperty("trainUuid").GetString() == TrainUuid.ToString(),
                "post-send Truck timeout must retain non-retryable ambiguity and exact target identity");
        }
        Check(ambiguousWrites == 1,
            "ambiguous post-send Truck timeout must never auto-retry the robbery request");
    }

    private static async Task TruckQuickRobRejectsForeignSessionResult()
    {
        const long MarchUuid = 1_417_409_824_803_038_247L;
        const long TrainUuid = 1_417_409_824_803_038_999L;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            truckQuickRobResult: fields =>
            {
                JsonObject root = JsonNode.Parse(TruckQuickRobResult(
                    fields,
                    state: "proven",
                    error: null,
                    requestSent: true,
                    battleWon: true,
                    rewardCount: 1))!.AsObject();
                root["sessionId"] = "foreign_session";
                return root.ToJsonString(JsonOptions.Default);
            });

        try
        {
            _ = await source.ExecuteTruckQuickRobAsync(2212, MarchUuid, TrainUuid, CancellationToken.None);
            throw new InvalidOperationException("foreign Truck quick-rob result should fail closed");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static async Task DispatchPlunderPreservesExactIdentityAndOutcome()
    {
        const string TaskUuid = "1417409824803038247";
        const long ExecuteAt = 1_789_616_300_000L;
        int writes = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("dispatch-plunder.txt", StringComparison.OrdinalIgnoreCase))
                    writes++;
            },
            dispatchPlunderResult: fields =>
            {
                Check(fields["taskUuid"] == TaskUuid &&
                      fields["executeAt"] == ExecuteAt.ToString() &&
                      fields["serverId"] == "2212",
                    "Dispatch plunder request must preserve exact decimal task UUID, executeAt and target server");
                return DispatchPlunderResult(
                    fields,
                    state: "proven",
                    requestSent: true,
                    success: true,
                    errorCode: null);
            });

        CurrentClientDispatchPlunderResult result =
            await source.ExecuteDispatchPlunderAsync(
                2212, TaskUuid, ExecuteAt, CancellationToken.None);

        Check(writes == 1 &&
              result.ServerId == 2212 &&
              result.TaskUuid == TaskUuid &&
              result.Succeeded &&
              result.ErrorCode is null &&
              result.RequestSent,
            "Dispatch plunder host protocol must return one authoritative matched success without retry");

        Check(CurrentClientMapBlockSource.NormalizeDispatchPlunderError("dispatch_des040") ==
                  "DISPATCH_PLUNDER_TASK_COMPLETED" &&
              CurrentClientMapBlockSource.NormalizeDispatchPlunderError("dispatch_des043") ==
                  "DISPATCH_PLUNDER_TASK_DISAPPEARED" &&
              CurrentClientMapBlockSource.NormalizeDispatchPlunderError("123456") ==
                  "DISPATCH_PLUNDER_SERVER_REJECTED: 123456",
            "Dispatch error normalization must preserve recovered named outcomes and generic numeric server rejection");
    }

    private static async Task DispatchPlunderMapsRejectedAndAmbiguousWithoutRetry()
    {
        const string TaskUuid = "1417409824803038247";
        const long ExecuteAt = 1_789_616_300_000L;

        int rejectedWrites = 0;
        CurrentClientMapBlockSource rejected = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("dispatch-plunder.txt", StringComparison.OrdinalIgnoreCase))
                    rejectedWrites++;
            },
            dispatchPlunderResult: fields => DispatchPlunderResult(
                fields,
                state: "rejected",
                requestSent: true,
                success: false,
                errorCode: "457001"));

        CurrentClientDispatchPlunderResult rejection =
            await rejected.ExecuteDispatchPlunderAsync(
                2212, TaskUuid, ExecuteAt, CancellationToken.None);
        Check(rejectedWrites == 1 &&
              !rejection.Succeeded &&
              rejection.RequestSent &&
              rejection.ErrorCode == "DISPATCH_PLUNDER_SERVER_REJECTED: 457001",
            "explicit Dispatch server rejection must be terminal, normalized and never retried");

        int ambiguousWrites = 0;
        CurrentClientMapBlockSource ambiguous = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            onProtocolWrite: path =>
            {
                if (path.EndsWith("dispatch-plunder.txt", StringComparison.OrdinalIgnoreCase))
                    ambiguousWrites++;
            },
            dispatchPlunderResult: fields => DispatchPlunderResult(
                fields,
                state: "ambiguous",
                requestSent: true,
                success: null,
                errorCode: "DISPATCH_PLUNDER_RESPONSE_TIMEOUT"));
        try
        {
            _ = await ambiguous.ExecuteDispatchPlunderAsync(
                2212, TaskUuid, ExecuteAt, CancellationToken.None);
            throw new InvalidOperationException(
                "post-send Dispatch timeout must remain ambiguous");
        }
        catch (BridgeCommandException error) when (
            error.Code == "DISPATCH_PLUNDER_RESPONSE_TIMEOUT" &&
            error.Message == "server response timeout")
        {
            JsonElement details =
                JsonSerializer.SerializeToElement(error.Details, JsonOptions.Default);
            Check(details.GetProperty("ambiguous").GetBoolean() &&
                  details.GetProperty("requestSent").GetBoolean() &&
                  details.GetProperty("taskUuid").GetString() == TaskUuid,
                "post-send Dispatch timeout must retain non-retryable ambiguity and exact task identity");
        }
        Check(ambiguousWrites == 1,
            "ambiguous post-send Dispatch timeout must never auto-retry DispatchSteal");
    }

    private static async Task DispatchPlunderRejectsForeignSessionResult()
    {
        const string TaskUuid = "1417409824803038247";
        const long ExecuteAt = 1_789_616_300_000L;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            dispatchPlunderResult: fields =>
            {
                JsonObject root = JsonNode.Parse(DispatchPlunderResult(
                    fields,
                    state: "proven",
                    requestSent: true,
                    success: true,
                    errorCode: null))!.AsObject();
                root["sessionId"] = "foreign_session";
                return root.ToJsonString(JsonOptions.Default);
            });

        try
        {
            _ = await source.ExecuteDispatchPlunderAsync(
                2212, TaskUuid, ExecuteAt, CancellationToken.None);
            throw new InvalidOperationException(
                "foreign Dispatch plunder result should fail closed");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static async Task AssetImageValidatesCachesAndRetriesSessionGap()
    {
        const string AssetPath = "Assets/Main/Sprites/ItemIcons/item406";
        const string PngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        int writes = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            assetImageResult: fields =>
            {
                writes++;
                Check(fields["sourceMode"] == "assetPath" && fields["assetPath"] == AssetPath,
                    "asset image protocol must preserve the authoritative game asset path");
                return AssetImageResult(
                    fields,
                    writes == 1 ? "failed" : "proven",
                    writes == 1 ? "overview_session_unavailable" : null,
                    writes == 1 ? null : PngBase64,
                    writes == 1 ? null : 1,
                    writes == 1 ? null : 1);
            });

        CurrentClientAssetImageResult first =
            await source.GetAssetImageAsync(AssetPath, null, CancellationToken.None);
        CurrentClientAssetImageResult second =
            await source.GetAssetImageAsync(AssetPath, null, CancellationToken.None);

        Check(writes == 2,
            "asset image should retry one recoverable Overview admission gap and then serve the second request from host cache");
        Check(first.DataUrl == "data:image/png;base64," + PngBase64 &&
              second.DataUrl == first.DataUrl &&
              first.Width == 1 && first.Height == 1 &&
              first.SourceMode == "assetPath" && first.SourceValue == AssetPath,
            "asset image result must preserve validated PNG bytes and exact source identity");
    }

    private static async Task AssetImageRejectsInvalidPng()
    {
        const string AssetPath = "Assets/Main/Sprites/ItemIcons/item230006";
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => ProvenEmptyCityCurrentView(fields),
            assetImageResult: fields => AssetImageResult(
                fields,
                "proven",
                null,
                Convert.ToBase64String([1, 2, 3, 4, 5, 6]),
                1,
                1));

        try
        {
            _ = await source.GetAssetImageAsync(AssetPath, null, CancellationToken.None);
            throw new InvalidOperationException("malformed PNG asset should fail closed");
        }
        catch (BridgeCommandException error) when (
            error.Code == "INVALID_ASSET" &&
            error.Message == "invalid PNG asset")
        {
        }

        try
        {
            _ = await source.GetAssetImageAsync(AssetPath, "frame_sprite", CancellationToken.None);
            throw new InvalidOperationException("asset image request with both source forms should fail closed");
        }
        catch (BridgeCommandException error) when (error.Code == "INVALID_ASSET")
        {
        }
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

    private static async Task TransientReadyLossKeepsOwnedSessionIdentity()
    {
        int readyReads = 0;
        CurrentClientMapBlockSource source = CreateSource(
            (fields, _) => FailedEmptyResource(fields),
            waitForHealthySession: (session, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Check(session == Session, "transient health gate received a different owned session");
                return Task.CompletedTask;
            },
            sessionProvider: () => ++readyReads == 1 ? Session : null,
            matchesOwnedSession: session => session == Session);

        _ = await source.CaptureAsync(Request("resource"), Block(), CancellationToken.None);
        Check(readyReads == 1,
            "same-session verification must use owned identity instead of re-requiring an instantaneously ready heartbeat");
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
        Func<IReadOnlyDictionary<string, string>, string>? bulkResult = null,
        Func<IReadOnlyDictionary<string, string>, string>? monsterProtectionResult = null,
        bool useCoarseMonsterMap = false,
        Func<OverviewMapScanSession?>? sessionProvider = null,
        Func<OverviewMapScanSession, bool>? matchesOwnedSession = null,
        Func<IReadOnlyDictionary<string, string>, string>? resourceDetailResult = null,
        Func<IReadOnlyDictionary<string, string>, string>? serverJumpResult = null,
        Func<IReadOnlyDictionary<string, string>, string>? marchFollowResult = null,
        Func<IReadOnlyDictionary<string, string>, string>? truckQuickRobResult = null,
        Func<IReadOnlyDictionary<string, string>, string>? dispatchPlunderResult = null,
        Func<IReadOnlyDictionary<string, string>, string>? assetImageResult = null)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        string overviewRoot = @"C:\overview";
        string probeRoot = @"C:\probe";
        var hooks = new CurrentClientMapBlockSourceHooks
        {
            DisableCoarseMonsterMap = !useCoarseMonsterMap,
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
                if (string.Equals(path, Path.Combine(overviewRoot, "server-jump.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (serverJumpResult is null) throw new InvalidOperationException("unexpected server Jump request");
                    string result = serverJumpResult(fields);
                    files[Path.Combine(overviewRoot, "server-jump-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(overviewRoot, "march-follow.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (marchFollowResult is null) throw new InvalidOperationException("unexpected march Follow request");
                    string result = marchFollowResult(fields);
                    files[Path.Combine(overviewRoot, "march-follow-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(overviewRoot, "truck-quick-rob.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (truckQuickRobResult is null) throw new InvalidOperationException("unexpected Truck quick-rob request");
                    string result = truckQuickRobResult(fields);
                    files[Path.Combine(overviewRoot, "truck-quick-rob-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(overviewRoot, "dispatch-plunder.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (dispatchPlunderResult is null) throw new InvalidOperationException("unexpected Dispatch plunder request");
                    string result = dispatchPlunderResult(fields);
                    files[Path.Combine(overviewRoot, "dispatch-plunder-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(probeRoot, "asset-image.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (assetImageResult is null) throw new InvalidOperationException("unexpected asset image request");
                    string result = assetImageResult(fields);
                    files[Path.Combine(probeRoot, "asset-image-result.json")] = Encoding.UTF8.GetBytes(result);
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
                if (string.Equals(path, Path.Combine(probeRoot, "resource-scan-detail.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    string result = resourceDetailResult?.Invoke(fields) ?? ProvenEmptyResourceScanDetail(fields);
                    files[Path.Combine(probeRoot, "resource-scan-detail-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                if (string.Equals(path, Path.Combine(probeRoot, "monster-protection-detail.txt"), StringComparison.OrdinalIgnoreCase))
                {
                    if (monsterProtectionResult is null) throw new InvalidOperationException("unexpected Monster Protection request");
                    string result = monsterProtectionResult(fields);
                    files[Path.Combine(probeRoot, "monster-protection-detail-result.json")] = Encoding.UTF8.GetBytes(result);
                    return;
                }
                throw new InvalidOperationException("unexpected protocol write: " + path);
            },
        };
        return new CurrentClientMapBlockSource(
            sessionProvider ?? (() => Session),
            overviewRoot,
            probeRoot,
            hooks,
            waitForHealthySession,
            matchesOwnedSession);
    }

    private static IReadOnlyDictionary<string, string> ParseKv(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);


    private static string AssetImageResult(
        IReadOnlyDictionary<string, string> fields,
        string state,
        string? error,
        string? base64,
        int? width,
        int? height)
    {
        string sourceMode = fields["sourceMode"];
        string sourceValue = sourceMode == "assetPath"
            ? fields["assetPath"]
            : fields["spriteName"];
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            sourceMode,
            sourceValue,
            state,
            error,
            base64,
            width,
            height,
            capturedAt = Timestamp(),
        }, JsonOptions.Default);
    }

    private static string DispatchPlunderResult(
        IReadOnlyDictionary<string, string> fields,
        string state,
        bool requestSent,
        bool? success,
        string? errorCode) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state,
            serverId = int.Parse(fields["serverId"]),
            currentServerId = int.Parse(fields["serverId"]),
            taskUuid = fields["taskUuid"],
            executeAt = long.Parse(fields["executeAt"]),
            requestSent,
            success,
            errorCode,
            error = errorCode,
            method = "SFSNetwork.SendMessage(MsgDefines.DispatchSteal)+DispatchStealMessage.HandleMessage",
        }, JsonOptions.Default);

    private static string TruckQuickRobResult(
        IReadOnlyDictionary<string, string> fields,
        string state,
        string? error,
        bool requestSent,
        bool? battleWon,
        int? rewardCount,
        bool rewardNormalizationComplete = true,
        int? dailyRobCount = null) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state,
            serverId = int.Parse(fields["serverId"]),
            currentServerId = int.Parse(fields["serverId"]),
            marchUuid = fields["marchUuid"],
            trainUuid = fields["trainUuid"],
            requestSent,
            battleWon,
            rewardCount,
            plunderRewards = rewardCount is > 0
                ? new object[]
                {
                    new
                    {
                        key = "reward:1:1001",
                        name = "Synthetic Reward",
                        iconPath = "synthetic/reward.png",
                        count = 25,
                        rewardType = 1,
                        itemId = 1001L,
                    },
                }
                : Array.Empty<object>(),
            rewardNormalizationComplete,
            dailyRobCount,
            method = "RailwayUtil.ClickAttackTrain+LWMyStationDataManager.TryAttackTrain",
            error,
        }, JsonOptions.Default);

    private static string MarchFollowResult(
        IReadOnlyDictionary<string, string> fields,
        string state,
        string? error,
        int currentServerId) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state,
            serverId = int.Parse(fields["serverId"]),
            marchUuid = long.Parse(fields["marchUuid"]),
            currentServerId,
            worldX = 123.5,
            worldY = 0.0,
            worldZ = 456.5,
            method = "GoToUtil.JumpToMarchByUuid",
            error,
        }, JsonOptions.Default);

    private static string ServerJumpResult(
        IReadOnlyDictionary<string, string> fields,
        int previousServerId,
        int currentServerId,
        string state,
        string? error) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            bridgeVersion = "lwbridge-overview-bridge-1",
            profileId = fields["profileId"],
            sessionId = fields["sessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            requestId = fields["requestId"],
            state,
            serverId = int.Parse(fields["serverId"]),
            previousServerId,
            currentServerId,
            changed = previousServerId != int.Parse(fields["serverId"]),
            method = "test-server-jump",
            error,
        }, JsonOptions.Default);

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

    private static string ProvenEmptyResourceScanDetail(IReadOnlyDictionary<string, string> fields) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            profileId = fields["profileId"],
            launchSessionId = fields["launchSessionId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            serverId = int.Parse(fields["serverId"]),
            scanRunId = fields["scanRunId"],
            state = "completed",
            error = (string?)null,
            targetCount = 0,
            requestCount = 0,
            cacheBeforeCount = 0,
            sendFailureCount = 0,
            readyCount = 0,
            details = Array.Empty<object>(),
            capturedAt = Timestamp(),
        }, JsonOptions.Default);

    private static string ProvenFastCityBatch(
        IReadOnlyDictionary<string, string> fields,
        params (int PointId, int X, int Y)[] points)
    {
        int targetX = int.Parse(fields["targetTileX"]);
        int targetY = int.Parse(fields["targetTileY"]);
        int targetCellX = targetX / 10;
        int startCellX;
        int columnCount;
        if (targetX <= 5)
        {
            startCellX = 0;
            columnCount = 2;
        }
        else if (targetX >= 995)
        {
            startCellX = 98;
            columnCount = 2;
        }
        else
        {
            startCellX = Math.Clamp(targetCellX - 2, 0, 96);
            columnCount = 4;
        }
        int startCellY = Math.Clamp((targetY / 10) - 7, 0, 90);
        int[] requested = Enumerable.Range(startCellY, 10)
            .SelectMany(row => Enumerable.Range(startCellX, columnCount).Select(column => row * 100 + column))
            .ToArray();
        bool includeMonster = fields.TryGetValue("includeMonster", out string? includeMonsterText) && includeMonsterText == "true";
        bool includeMonsterProtection = fields.TryGetValue("includeMonsterProtection", out string? includeMonsterProtectionText) &&
            includeMonsterProtectionText == "true";
        bool includeTrain = fields.TryGetValue("includeTrain", out string? includeTrainText) && includeTrainText == "true";
        bool includeDispatch = fields.TryGetValue("includeDispatch", out string? includeDispatchText) && includeDispatchText == "true";
        bool includeGhost = fields.TryGetValue("includeGhost", out string? includeGhostText) && includeGhostText == "true";
        bool includeTreasure = fields.TryGetValue("includeTreasure", out string? includeTreasureText) && includeTreasureText == "true";
        bool includeResourceDetails = fields.TryGetValue("includeResourceDetails", out string? includeResourceDetailsText) && includeResourceDetailsText == "true";
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1, probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"], launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"], challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]), requestedCount = 8, requestMode = "coverage",
            includeMonster, includeMonsterProtection, includeTrain, includeDispatch, includeGhost, includeTreasure, includeResourceDetails,
            state = "proven", error = (string?)null, requestedIndices = requested,
            matchedCityCount = points.Length, matchedResourceCount = 0, matchedDispatchCount = 0, matchedGhostCount = 0, matchedTreasureCount = 0,
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
                level = 30, source = "WorldPointManager.GetAllMainBaseList",
            }).ToArray(),
            monster_march_records = Array.Empty<object>(),
            train_march_records = Array.Empty<object>(),
        }, JsonOptions.Default);
    }



    private static string ProvenWideFastCityBatch(IReadOnlyDictionary<string, string> fields)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        int targetX = int.Parse(fields["targetTileX"]);
        int targetY = int.Parse(fields["targetTileY"]);
        int targetCell = Math.Clamp(targetX / 10, 0, 99);
        int startCellY = Math.Clamp((targetY / 10) - 7, 0, 90);
        bool narrowInterior = targetCell >= 30 && targetCell <= 60;
        int width = targetCell is 0 or 99 ? 2 : narrowInterior ? 3 : 4;
        int startCellX = targetCell switch
        {
            0 => 0,
            99 => 98,
            _ when narrowInterior => Math.Clamp(targetCell - 1, 0, 97),
            _ => Math.Clamp(targetCell - 2, 0, 96),
        };
        int[] requested = Enumerable.Range(startCellY, 10)
            .SelectMany(row => Enumerable.Range(startCellX, width).Select(column => row * 100 + column))
            .ToArray();
        root["requestedIndices"] = JsonSerializer.SerializeToNode(requested, JsonOptions.Default);
        root["nativeCurrentSetCount"] = requested.Length;
        return root.ToJsonString(JsonOptions.Default);
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

    private static string ProvenFastDispatchBatch(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, int PointId, int X, int Y, int CfgId, int Level, int Quality, bool IsSpecial, long CompletionTime, string OwnerUid)[] tasks)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["includeDispatch"] = true;
        root["matchedCityCount"] = 0;
        root["matchedResourceCount"] = 0;
        root["matchedDispatchCount"] = tasks.Length;
        root["point_records"] = JsonSerializer.SerializeToNode(tasks.Select(task => new
        {
            id = task.PointId,
            pointId = task.PointId,
            pointType = 17,
            kind = "dispatch_task",
            runtimeClass = "HeroDispatchMissionPointInfo",
            serverId = 2212,
            srcServerId = 0,
            worldId = 0,
            x = task.X,
            y = task.Y,
            uuid = task.Uuid,
            ownerUid = task.OwnerUid,
            cfgId = task.CfgId,
            level = task.Level,
            quality = task.Quality,
            isSpecial = task.IsSpecial,
            completionTime = task.CompletionTime,
            rewarded = 0,
            actEndTime = task.CompletionTime + 3_600_000L,
            expiredTime = task.CompletionTime + 7_200_000L,
            allianceId = "dispatch-alliance",
            stealListCount = 1,
            accListCount = 0,
            protectTimeMinutes = 5,
            stealMaxTimes = 3,
            dispatchNameKey = "dispatch_name_key",
            source = "WorldPointManager._pointInfos+HeroDispatchMissionPointInfo",
        }).ToArray(), JsonOptions.Default);
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenFastGhostBatch(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, int PointId, int X, int Y, int CfgId, int Level, int Quality, bool IsSpecial, long CompletionTime, string OwnerUid)[] tasks)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["includeGhost"] = true;
        root["matchedCityCount"] = 0;
        root["matchedResourceCount"] = 0;
        root["matchedDispatchCount"] = 0;
        root["matchedGhostCount"] = tasks.Length;
        root["point_records"] = JsonSerializer.SerializeToNode(tasks.Select(task => new
        {
            id = task.PointId,
            pointId = task.PointId,
            pointType = 29,
            kind = "ghost_task",
            runtimeClass = "GhostreconPointInfo",
            serverId = 2212,
            srcServerId = 0,
            worldId = 0,
            x = task.X,
            y = task.Y,
            uuid = task.Uuid,
            ownerUid = task.OwnerUid,
            cfgId = task.CfgId,
            level = task.Level,
            quality = task.Quality,
            isSpecial = task.IsSpecial,
            completionTime = task.CompletionTime,
            taskExpireTime = task.CompletionTime + 7_200_000L,
            actEndTime = task.CompletionTime + 3_600_000L,
            teamStartTime = task.CompletionTime - 60_000L,
            ownerServer = 2212,
            allianceId = "ghost-alliance",
            size = 1,
            stealListCount = 1,
            memberListCount = 2,
            rewardConfig = "1:100",
            worldOpen = 1,
            protectTime = 300,
            stealMaxTimes = 3,
            source = "WorldPointManager._pointInfos+GhostreconPointInfo+TableName.LwGhostreconTask",
        }).ToArray(), JsonOptions.Default);
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenFastTreasureBatch(
        IReadOnlyDictionary<string, string> fields,
        params (bool Supplies, string Uuid, int PointId, int X, int Y, int Type)[] points)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["includeTreasure"] = true;
        root["matchedCityCount"] = 0;
        root["matchedResourceCount"] = 0;
        root["matchedDispatchCount"] = 0;
        root["matchedGhostCount"] = 0;
        root["matchedTreasureCount"] = points.Length;
        var pointRows = new JsonArray();
        foreach (var point in points)
        {
            JsonObject row = point.Supplies
                ? new JsonObject
                {
                    ["id"] = point.PointId, ["pointId"] = point.PointId, ["pointType"] = 27,
                    ["kind"] = "supplies_point", ["runtimeClass"] = "WorldSuppliesPoint",
                    ["serverId"] = 2212, ["srcServerId"] = 0, ["worldId"] = 0,
                    ["x"] = point.X, ["y"] = point.Y, ["uuid"] = point.Uuid,
                    ["treasureType"] = 0, ["suppliesType"] = point.Type, ["configId"] = 7000 + point.Type,
                    ["state"] = 1, ["userCount"] = 2, ["rewardedCount"] = 1,
                    ["createTime"] = 1_789_616_000_000L,
                    ["discovererAllianceId"] = "supply-alliance", ["discovererUid"] = "supply-owner",
                    ["workEndTime"] = 1_789_620_000_000L, ["workState"] = 1,
                    ["source"] = "WorldPointManager._pointInfos+WorldSuppliesPoint+TableName.LWIceSupplies",
                }
                : new JsonObject
                {
                    ["id"] = point.PointId, ["pointId"] = point.PointId, ["pointType"] = 21,
                    ["kind"] = "treasure_point", ["runtimeClass"] = "TreasurePointInfo",
                    ["serverId"] = 2212, ["srcServerId"] = 0, ["worldId"] = 0,
                    ["x"] = point.X, ["y"] = point.Y, ["uuid"] = point.Uuid,
                    ["ownerUid"] = "treasure-owner", ["ownerName"] = "Owner", ["eventId"] = "event",
                    ["treasureType"] = point.Type, ["suppliesType"] = 0,
                    ["startTime"] = 1_789_615_000_000L, ["completionTime"] = 1_789_616_000_000L,
                    ["expireTime"] = 1_789_630_000_000L, ["createTime"] = 1_789_614_000_000L,
                    ["complete"] = false, ["speed"] = 1.0,
                    ["allianceId"] = "treasure-alliance", ["allianceAbbr"] = "TAG",
                    ["rewardedCount"] = 2, ["diggingCount"] = 1, ["rewardMax"] = 5, ["remainingBoxes"] = 3,
                    ["fromPoint"] = 0, ["multiple"] = 1, ["killerId"] = "", ["customInfo"] = "",
                    ["source"] = "WorldPointManager._pointInfos+TreasurePointInfo",
                };
            pointRows.Add(row);
        }
        root["point_records"] = pointRows;
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenFastAllEightBatch(
        IReadOnlyDictionary<string, string> fields,
        bool includeRecords)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(
            fields, includeRecords ? new[] { (90001, 9, 9) } : Array.Empty<(int, int, int)>()))!.AsObject();
        JsonObject resource = JsonNode.Parse(ProvenFastResourceBatch(
            fields, includeRecords ? new[] { (90002, 9, 9, 3, 2, true, true) } : Array.Empty<(int, int, int, int, int, bool, bool)>()))!.AsObject();
        JsonObject dispatch = JsonNode.Parse(ProvenFastDispatchBatch(
            fields, includeRecords ? new[] { ("dispatch-all", 90003, 9, 9, 7001, 5, 4, false, 1_789_616_000_000L, "owner-dispatch") } : Array.Empty<(string, int, int, int, int, int, int, bool, long, string)>()))!.AsObject();
        JsonObject ghost = JsonNode.Parse(ProvenFastGhostBatch(
            fields, includeRecords ? new[] { ("ghost-all", 90004, 9, 9, 7101, 6, 5, true, 1_789_616_000_000L, "owner-ghost") } : Array.Empty<(string, int, int, int, int, int, int, bool, long, string)>()))!.AsObject();
        JsonObject treasure = JsonNode.Parse(ProvenFastTreasureBatch(
            fields, includeRecords ? new[] { (false, "treasure-all", 90005, 9, 9, 5) } : Array.Empty<(bool, string, int, int, int, int)>()))!.AsObject();
        JsonObject monster = JsonNode.Parse(ProvenFastMonsterBatch(
            fields, includeRecords ? new[] { ("monster-all", 9, 9, 1002009, 9, "2000005", false) } : Array.Empty<(string, int, int, int, int, string, bool)>()))!.AsObject();
        JsonObject truck = JsonNode.Parse(ProvenFastTruckBatch(
            fields, includeRecords ? new[] { ("truck-all", 9, 9, 86, 3, 4, "Truck Owner", 12345L) } : Array.Empty<(string, int, int, int, int, int, string, long)>()))!.AsObject();
        JsonObject railway = JsonNode.Parse(ProvenFastRailwayBatch(
            fields, includeRecords ? new[] { ("rail-all", 9, 9, 201, 4, 5, "Rail Owner", 54321L) } : Array.Empty<(string, int, int, int, int, int, string, long)>()))!.AsObject();

        var pointRows = new JsonArray();
        foreach (JsonObject part in new[] { root, resource, dispatch, ghost, treasure })
            foreach (JsonNode? row in part["point_records"]!.AsArray())
                pointRows.Add(row!.DeepClone());
        root["point_records"] = pointRows;
        root["matchedCityCount"] = includeRecords ? 1 : 0;
        root["matchedResourceCount"] = includeRecords ? 1 : 0;
        root["matchedDispatchCount"] = includeRecords ? 1 : 0;
        root["matchedGhostCount"] = includeRecords ? 1 : 0;
        root["matchedTreasureCount"] = includeRecords ? 1 : 0;
        root["monster_march_records"] = monster["monster_march_records"]!.DeepClone();
        var trainRows = new JsonArray();
        foreach (JsonObject part in new[] { truck, railway })
            foreach (JsonNode? row in part["train_march_records"]!.AsArray())
                trainRows.Add(row!.DeepClone());
        root["train_march_records"] = trainRows;
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
            trainQuality = truck.Quality, carriageNum = truck.CarriageNum,
            source = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
        }).ToArray(), JsonOptions.Default);
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenFastRailwayBatch(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, int X, int Y, int TrainCfgId, int Quality, int CarriageNum, string OwnerName, long Power)[] trains)
    {
        JsonObject root = JsonNode.Parse(ProvenFastCityBatch(fields))!.AsObject();
        root["includeTrain"] = true;
        root["train_march_records"] = JsonSerializer.SerializeToNode(trains.Select(train => new
        {
            uuid = train.Uuid, runtimeClass = "WorldMarch", serverId = 2212, worldId = 0,
            x = train.X, y = train.Y, positionIndex = train.Y * 1000 + train.X + 1,
            ownerUid = "owner-" + train.Uuid, ownerName = train.OwnerName, allianceName = "Alliance", ownerServer = 2212,
            power = train.Power, startTime = 1_789_588_788_959L, endTime = 1_789_589_236_172L,
            trainUuid = 1_417_409_824_803_038_247L, trainCfgId = train.TrainCfgId, trainType = 2,
            trainQuality = train.Quality, carriageNum = train.CarriageNum, trainDataJson = "{}",
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


    private static string ProvenCoarseDoomsdayMonsterBatch(
        IReadOnlyDictionary<string, string> fields)
    {
        JsonObject root = JsonNode.Parse(ProvenCoarseMonsterBatch(
            fields, ("doom-manager-1", 500, 500, 320032, 60, "doom_walker_name_key", false)))!.AsObject();
        JsonObject row = root["monster_march_records"]!.AsArray()[0]!.AsObject();
        row["runtimeClass"] = "LWDoomsdayManager.BossVO";
        row["monsterSpecialType"] = 32;
        row["configSpecial"] = 32;
        row["monsterRallyNum"] = 1;
        row["source"] = "DataCenter.LWDoomsdayManager.theaterBosses";
        bool zombieBossOnly = fields.TryGetValue("includeMonsterProtection", out string? includeProtection) &&
            includeProtection == "true";
        root["doomsdayBossCount"] = zombieBossOnly ? 0 : 1;
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenCoarseMonsterBatch(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, int X, int Y, int MonsterId, int Level, string NameKey, bool Boss)[] monsters)
    {
        JsonObject root = JsonNode.Parse(ProvenFastMonsterBatch(fields, monsters))!.AsObject();
        int bossCount = monsters.Count(monster => monster.NameKey == "2901012");
        bool includeMonsterProtection = fields.TryGetValue("includeMonsterProtection", out string? includeProtectionText) &&
            includeProtectionText == "true";
        root["requestMode"] = "zoom";
        root["requestedCount"] = 160;
        root["holdMilliseconds"] = 0;
        root["viewLevel"] = -1;
        root["includeMonster"] = true;
        root["includeMonsterProtection"] = includeMonsterProtection;
        root["includeTrain"] = false;
        root["responseFlagsTransitioned"] = true;
        root["cameraTileStable"] = true;
        root["positionRestoredBeforeResponse"] = true;
        root["requestMethod"] = "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift";
        root["zoomWholeWorldCoarse"] = true;
        root["serverLod"] = 0; root["blockSize"] = 10; root["blockCount"] = 100;
        root["postServerLod"] = 2; root["postBlockSize"] = 1000; root["postBlockCount"] = 1;
        root["zoomFinalServerLod"] = 2; root["zoomFinalBlockSize"] = 1000; root["zoomFinalBlockCount"] = 1;
        root["restoredServerLod"] = 0; root["restoredBlockSize"] = 10; root["restoredBlockCount"] = 100;
        root["preTileX"] = 230; root["preTileY"] = 257;
        root["restoredTileX"] = 230; root["restoredTileY"] = 257;
        root["doomsdayBossCount"] = 0;
        root["monsterInvasionBossCount"] = bossCount;
        root["monsterProtectionDetailTargetCount"] = includeMonsterProtection ? bossCount : 0;
        root["monsterProtectionDetailRequestCount"] = 0;
        root["monsterProtectionDetailReadyCount"] = 0;
        return root.ToJsonString(JsonOptions.Default);
    }

    private static string ProvenMonsterProtectionResult(
        IReadOnlyDictionary<string, string> fields,
        params (string Uuid, bool Received, bool Active, long EndTime)[] details) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            probeVersion = "lwbridge-live-resource-probe-2",
            requestId = fields["requestId"],
            launchSessionId = fields["launchSessionId"],
            profileId = fields["profileId"],
            challenge = fields["challenge"],
            gamePid = int.Parse(fields["gamePid"]),
            scanRunId = fields["scanRunId"],
            serverId = int.Parse(fields["serverId"]),
            expectedTargetCount = int.Parse(fields["expectedTargetCount"]),
            state = "completed",
            error = details.Any(detail => !detail.Received) ? "monster_invasion_protection_response_timeout" : null,
            targetCount = details.Length,
            requestCount = details.Length,
            retryCount = 0,
            readyCount = details.Count(detail => detail.Received),
            timedOut = details.Any(detail => !detail.Received),
            elapsedSeconds = 3.0,
            details = details.Select(detail => new
            {
                uuid = detail.Uuid,
                received = detail.Received,
                isProtected = detail.Active,
                protectionEndTime = detail.EndTime,
            }).ToArray(),
            capturedAt = Timestamp(),
        }, JsonOptions.Default);

    private static string Timestamp() => Now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
