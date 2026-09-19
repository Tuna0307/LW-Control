using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullRailwayProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-railway");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(proofRoot, "manual-full-railway-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-railway-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedRailwayCount = 0;
        int reopenedRailwayCount = 0;
        int trainTypeCount = 0;
        int qualityCount = 0;
        int powerCount = 0;
        int trainCfgCount = 0;
        int trainDataCount = 0;
        int arriveTsCount = 0;
        int robTimesCount = 0;
        int marchUuidCount = 0;
        int maxLootCountCount = 0;
        int protectTimeCount = 0;
        int currentGoodsRowCount = 0;
        int currentGoodsItemCount = 0;
        int currentGoodsAuditableCount = 0;
        int sortCheckCount = 0;
        int reopenedSortCheckCount = 0;
        int sortComparedRowCount = 0;
        string? sampleItemKey = null;
        long sortSampledAt = 0;
        double scanWallSeconds = 0;
        int originalServerId = 0;
        int targetServerId = 0;
        bool targetServerJumped = false;
        bool returnedToOriginalServer = false;
        bool followProven = false;
        string scanMode = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_MODE"),
            "fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "normal";
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        string? targetServerText = Environment.GetEnvironmentVariable("LWBRIDGE_RAILWAY_TARGET_SERVER");
        if (!string.IsNullOrWhiteSpace(targetServerText) &&
            (!int.TryParse(targetServerText, out targetServerId) || targetServerId is < 1 or > 99_999))
            throw new InvalidDataException("LWBRIDGE_RAILWAY_TARGET_SERVER must be an integer from 1 to 99999.");
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson = JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();

            string runId;
            int serverId;
            using (var store = new MapDataStore(databasePath))
            {
                var service = new ManualMapScanCommandService(lifecycle, store);
                var source = new CurrentClientMapBlockSource(lifecycle);
                try
                {
                    CurrentClientMapContext initialContext =
                        await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                    originalServerId = initialContext.ServerId;
                    if (targetServerId == 0) targetServerId = originalServerId;
                    if (targetServerId != originalServerId)
                    {
                        JsonElement jumpPayload = JsonSerializer.SerializeToElement(
                            new { serverId = targetServerId }, JsonOptions.Default);
                        JsonElement jumpResult = JsonSerializer.SerializeToElement(
                            await service.InvokeAsync(
                                "server_jump",
                                jumpPayload,
                                operationCts.Token).ConfigureAwait(false),
                            JsonOptions.Default);
                        if (!jumpResult.GetProperty("changed").GetBoolean())
                            throw new InvalidDataException("Railway target server jump did not change server.");
                        targetServerJumped = true;
                    }

                    JsonElement payload = JsonSerializer.SerializeToElement(new
                    {
                        profileId = "manual-full-railway-proof",
                        scanMode,
                        selectedTypes = new[] { "railway" },
                    }, JsonOptions.Default);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    object? start = await service.InvokeAsync(
                        "map_scan_start", payload, operationCts.Token).ConfigureAwait(false);
                    JsonElement startStatus = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
                    runId = startStatus.GetProperty("scanRunId").GetString() ?? string.Empty;
                    serverId = startStatus.GetProperty("serverId").GetInt32();
                    if (string.IsNullOrWhiteSpace(runId) || serverId <= 0 ||
                        startStatus.GetProperty("totalBlocks").GetInt32() != 2500 ||
                        startStatus.GetProperty("concurrency").GetInt32() != expectedConcurrency)
                        throw new InvalidDataException("Ordinary Manual Start did not expose expected Railway/Train scan identity/geometry.");
                    JsonElement status = startStatus;
                    int lastReportedBlocks = 0;
                    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        operationCts.Token.ThrowIfCancellationRequested();
                        status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                        string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                        int currentReadBlocks = status.GetProperty("readBlocks").GetInt32();
                        if (currentReadBlocks >= lastReportedBlocks + 250)
                        {
                            lastReportedBlocks = (currentReadBlocks / 250) * 250;
                            Console.Error.WriteLine($"MANUAL_FULL_RAILWAY_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException("Ordinary Manual Railway scan failed: " +
                                (status.TryGetProperty("lastError", out JsonElement last) ? last.GetString() : "unknown"));
                        await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                    }
                    stopwatch.Stop();
                    scanWallSeconds = stopwatch.Elapsed.TotalSeconds;
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    if (status.GetProperty("phase").GetString() != "completed" ||
                        status.GetProperty("isReading").GetBoolean() ||
                        status.GetProperty("readBlocks").GetInt32() != 2500 ||
                        status.GetProperty("failedBlocks").GetInt32() != 0 ||
                        status.GetProperty("unreadBlocks").GetInt32() != 0)
                    {
                        IReadOnlyList<MapScanBlockCheckpoint> partial = store.ReadScanBlockCheckpointsForTest(runId);
                        int secondAttempts = partial.Count(item => item.Attempts > 1);
                        throw new InvalidDataException($"Ordinary Manual Railway scan incomplete: phase={status.GetProperty("phase").GetString()}, read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, secondAttempts={secondAttempts}.");
                    }
                    MapSearchResult liveRailway = store.SearchIndexed(RailwayQuery(serverId));
                    publishedRailwayCount = liveRailway.Total;
                    if (publishedRailwayCount <= 0)
                        throw new InvalidDataException("Ordinary Manual Railway scan published no Railway/Train records.");

                    JsonElement followRow = liveRailway.Rows.FirstOrDefault();
                    string? followMarchUuid =
                        followRow.ValueKind == JsonValueKind.Object &&
                        followRow.TryGetProperty("marchUuid", out JsonElement marchValue) &&
                        marchValue.ValueKind == JsonValueKind.String
                            ? marchValue.GetString()
                            : null;
                    if (string.IsNullOrWhiteSpace(followMarchUuid))
                        throw new InvalidDataException("Positive Railway row omitted marchUuid required for Follow.");
                    JsonElement followPayload = JsonSerializer.SerializeToElement(
                        new { serverId, marchUuid = followMarchUuid }, JsonOptions.Default);
                    JsonElement followResult = JsonSerializer.SerializeToElement(
                        await service.InvokeAsync(
                            "map_march_follow",
                            followPayload,
                            operationCts.Token).ConfigureAwait(false),
                        JsonOptions.Default);
                    if (followResult.GetProperty("serverId").GetInt32() != serverId ||
                        !string.Equals(
                            followResult.GetProperty("marchUuid").GetString(),
                            followMarchUuid,
                            StringComparison.Ordinal))
                        throw new InvalidDataException("Railway Follow result did not preserve live row identity.");
                    followProven = true;

                    CollectRailwayMetrics(store, serverId, out trainTypeCount, out qualityCount,
                        out powerCount, out trainCfgCount, out trainDataCount, out arriveTsCount, out robTimesCount,
                        out marchUuidCount, out maxLootCountCount, out protectTimeCount,
                        out currentGoodsRowCount, out currentGoodsItemCount, out currentGoodsAuditableCount);
                    sortSampledAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    RailwaySortMetrics sortMetrics = ValidateRailwaySorts(store, serverId, sortSampledAt);
                    sortCheckCount = sortMetrics.CheckCount;
                    sortComparedRowCount = sortMetrics.RowCount;
                    sampleItemKey = sortMetrics.SampleItemKey;
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
            {
                reopenedRailwayCount = reopened.SearchIndexed(RailwayQuery(serverId)).Total;
                RailwaySortMetrics reopenedSortMetrics = ValidateRailwaySorts(reopened, serverId, sortSampledAt);
                reopenedSortCheckCount = reopenedSortMetrics.CheckCount;
                if (reopenedSortCheckCount != sortCheckCount ||
                    reopenedSortMetrics.RowCount != sortComparedRowCount ||
                    !string.Equals(reopenedSortMetrics.SampleItemKey, sampleItemKey, StringComparison.Ordinal))
                    throw new InvalidDataException("Railway sort proof changed after database reopen.");
            }
            if (reopenedRailwayCount != publishedRailwayCount)
                throw new InvalidDataException("Ordinary Manual Railway count changed after database reopen.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_railway",
                totalBlocks = 2500,
                publishedRailwayCount,
                reopenedRailwayCount,
                scanMode,
                concurrency = expectedConcurrency,
                scanWallSeconds,
                trainTypeCount,
                qualityCount,
                powerCount,
                trainCfgCount,
                trainDataCount,
                arriveTsCount,
                robTimesCount,
                marchUuidCount,
                maxLootCountCount,
                protectTimeCount,
                currentGoodsRowCount,
                currentGoodsItemCount,
                currentGoodsAuditableCount,
                sortSampledAt,
                sortComparedRowCount,
                sortCheckCount,
                reopenedSortCheckCount,
                sampleItemKey,
                originalServerId,
                targetServerId = serverId,
                targetServerJumped,
                followProven,
                relativeSortProven = sortComparedRowCount >= 2,
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            if (targetServerJumped && originalServerId > 0 &&
                lifecycle.GetReadyMapScanSession() is not null &&
                lifecycle.GetLiveServerId() is int liveServerId &&
                liveServerId != originalServerId)
            {
                try
                {
                    using MapDataStore returnStore = MapDataStore.CreateInMemory();
                    var returnService = new ManualMapScanCommandService(lifecycle, returnStore);
                    try
                    {
                        JsonElement returnPayload = JsonSerializer.SerializeToElement(
                            new { serverId = originalServerId }, JsonOptions.Default);
                        _ = await returnService.InvokeAsync(
                            "server_jump",
                            returnPayload,
                            CancellationToken.None).ConfigureAwait(false);
                        returnedToOriginalServer = lifecycle.GetLiveServerId() == originalServerId;
                    }
                    finally
                    {
                        returnService.Close();
                    }
                }
                catch (Exception returnError)
                {
                    Console.Error.WriteLine(
                        "LIVE_MANUAL_FULL_RAILWAY_RETURN_FAILED: " + returnError.Message);
                    if (operationError is null) throw;
                }
            }

            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_RAILWAY_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    internal static async Task RunSourceOnlyAsync()
    {
        string gameRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("railway-source-live-proof", gameRoot);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string? instanceId = null; Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(await lifecycle.InvokeAsync("profile_instance_start", empty.RootElement, cts.Token), JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(cts.Token).ConfigureAwait(false);
            IReadOnlyList<MapScanTargetBlock> blocks = MapScanTraversal.Build(context.TileWidth, context.TileHeight);
            var request = new MapScanExecutionRequest("railway-source-live", context.ServerId, context.WorldId, context.TileWidth, context.TileHeight, ["railway"], 8, 2, context.PlayerTileX, context.PlayerTileY);
            Stopwatch sw = Stopwatch.StartNew();
            IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(request, blocks[0], blocks.Select(b => b.BlockIndex).ToHashSet(), p => Console.Error.WriteLine($"RAILWAY_SOURCE_PROGRESS {p.Percent:F1}%"), cts.Token).ConfigureAwait(false);
            sw.Stop();
            MapStoredRecord[] records = captures.SelectMany(c => c.Records).GroupBy(r => r.RecordKey).Select(g => g.First()).ToArray();
            Console.WriteLine(JsonSerializer.Serialize(new { ok=true, proof="current_client_full_railway_source", captures=captures.Count, records=records.Length, elapsedSeconds=sw.Elapsed.TotalSeconds }, JsonOptions.Default));
        }
        catch (Exception error) { operationError = error; Console.Error.WriteLine("RAILWAY_SOURCE_ERROR: " + error); throw; }
        finally
        {
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stop = JsonDocument.Parse(JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try { await lifecycle.InvokeAsync("profile_instance_stop", stop.RootElement, stopCts.Token).ConfigureAwait(false); }
                catch when (operationError is not null) { }
            }
        }
    }

    private sealed record RailwaySortMetrics(
        int CheckCount,
        int RowCount,
        string? SampleItemKey);

    private static RailwaySortMetrics ValidateRailwaySorts(
        MapDataStore store,
        int serverId,
        long sampledAt)
    {
        IReadOnlyList<JsonElement> allRows = ReadAllRailwayRows(
            store,
            RailwaySortQuery(serverId, [new MapDataSort("updatedAt", "desc")], itemKey: null),
            sampledAt);
        if (allRows.Count <= 0)
            throw new InvalidDataException("Railway sort proof requires at least one active Railway row.");

        string? itemKey = FirstRailwayItemKey(allRows);
        int checks = 0;
        foreach (string sortBy in new[] { "quality", "power", "protectTime", "updatedAt" })
        {
            foreach (string sortOrder in new[] { "asc", "desc" })
            {
                ValidateRailwaySortOrder(
                    store,
                    serverId,
                    sampledAt,
                    allRows,
                    [new MapDataSort(sortBy, sortOrder)],
                    itemKey: null);
                checks++;
            }
        }

        ValidateRailwaySortOrder(
            store,
            serverId,
            sampledAt,
            allRows,
            [
                new MapDataSort("quality", "asc"),
                new MapDataSort("power", "desc"),
                new MapDataSort("protectTime", "asc"),
                new MapDataSort("updatedAt", "desc"),
            ],
            itemKey: null);
        checks++;

        if (!string.IsNullOrWhiteSpace(itemKey))
        {
            foreach (string sortOrder in new[] { "asc", "desc" })
            {
                ValidateRailwaySortOrder(
                    store,
                    serverId,
                    sampledAt,
                    allRows,
                    [new MapDataSort("itemCount", sortOrder)],
                    itemKey);
                checks++;
            }
            ValidateRailwaySortOrder(
                store,
                serverId,
                sampledAt,
                allRows,
                [
                    new MapDataSort("itemCount", "desc"),
                    new MapDataSort("quality", "desc"),
                    new MapDataSort("power", "asc"),
                    new MapDataSort("updatedAt", "desc"),
                ],
                itemKey);
            checks++;
        }

        return new RailwaySortMetrics(checks, allRows.Count, itemKey);
    }

    private static void ValidateRailwaySortOrder(
        MapDataStore store,
        int serverId,
        long sampledAt,
        IReadOnlyList<JsonElement> allRows,
        IReadOnlyList<MapDataSort> sorts,
        string? itemKey)
    {
        IReadOnlyList<JsonElement> expectedSource = string.IsNullOrWhiteSpace(itemKey)
            ? allRows
            : allRows.Where(row => RailwayContainsItem(row, itemKey!)).ToArray();
        var expected = expectedSource.ToList();
        expected.Sort((left, right) => CompareRailwayRows(left, right, sorts, itemKey));

        MapDataQueryOptions query = RailwaySortQuery(serverId, sorts, itemKey);
        IReadOnlyList<JsonElement> actual = ReadAllRailwayRows(store, query, sampledAt);
        if (actual.Count != expected.Count)
            throw new InvalidDataException(
                $"Railway sort row count mismatch for {DescribeRailwaySorts(sorts)}: actual={actual.Count}, expected={expected.Count}.");

        for (int index = 0; index < expected.Count; index++)
        {
            string expectedUuid = RailwayUuid(expected[index]);
            string actualUuid = RailwayUuid(actual[index]);
            if (!string.Equals(actualUuid, expectedUuid, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Railway sort mismatch for {DescribeRailwaySorts(sorts)} at {index}: actual={actualUuid}, expected={expectedUuid}.");
        }
    }

    private static int CompareRailwayRows(
        JsonElement left,
        JsonElement right,
        IReadOnlyList<MapDataSort> sorts,
        string? itemKey)
    {
        foreach (MapDataSort sort in sorts)
        {
            double? leftValue = RailwaySortValue(left, sort.SortBy, itemKey);
            double? rightValue = RailwaySortValue(right, sort.SortBy, itemKey);
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

        return StringComparer.Ordinal.Compare(RailwayUuid(left), RailwayUuid(right));
    }

    private static double? RailwaySortValue(JsonElement row, string sortBy, string? itemKey) =>
        sortBy switch
        {
            "quality" => OptionalRailwayNumber(row, "quality"),
            "power" => OptionalRailwayNumber(row, "power"),
            "itemCount" => RailwayItemCount(row, itemKey),
            "protectTime" => PositiveRailwayNumberOrNull(row, "protectTime"),
            "updatedAt" => OptionalRailwayNumber(row, "updatedAt"),
            _ => throw new InvalidDataException("Unexpected Railway sort key: " + sortBy),
        };

    private static double RailwayItemCount(JsonElement row, string? itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey) ||
            !row.TryGetProperty("currentGoods", out JsonElement goods) ||
            goods.ValueKind != JsonValueKind.Array)
            return 0d;

        double total = 0d;
        foreach (JsonElement good in goods.EnumerateArray())
        {
            if (!good.TryGetProperty("key", out JsonElement key) ||
                key.ValueKind != JsonValueKind.String ||
                !string.Equals(key.GetString(), itemKey, StringComparison.Ordinal))
                continue;
            if (good.TryGetProperty("count", out JsonElement count) &&
                count.ValueKind == JsonValueKind.Number &&
                count.TryGetDouble(out double quantity))
                total += quantity;
        }
        return total;
    }

    private static bool RailwayContainsItem(JsonElement row, string itemKey) =>
        row.TryGetProperty("currentGoods", out JsonElement goods) &&
        goods.ValueKind == JsonValueKind.Array &&
        goods.EnumerateArray().Any(good =>
            good.TryGetProperty("key", out JsonElement key) &&
            key.ValueKind == JsonValueKind.String &&
            string.Equals(key.GetString(), itemKey, StringComparison.Ordinal));

    private static string? FirstRailwayItemKey(IEnumerable<JsonElement> rows)
    {
        foreach (JsonElement row in rows)
        {
            if (!row.TryGetProperty("currentGoods", out JsonElement goods) ||
                goods.ValueKind != JsonValueKind.Array)
                continue;
            foreach (JsonElement good in goods.EnumerateArray())
            {
                if (good.TryGetProperty("key", out JsonElement key) &&
                    key.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(key.GetString()))
                    return key.GetString();
            }
        }
        return null;
    }

    private static double? OptionalRailwayNumber(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static double? PositiveRailwayNumberOrNull(JsonElement row, string name)
    {
        double? value = OptionalRailwayNumber(row, name);
        return value is > 0d ? value : null;
    }

    private static string RailwayUuid(JsonElement row) =>
        row.TryGetProperty("uuid", out JsonElement uuid) &&
        uuid.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(uuid.GetString())
            ? uuid.GetString()!
            : throw new InvalidDataException("Railway sort proof row has no UUID.");

    private static string DescribeRailwaySorts(IReadOnlyList<MapDataSort> sorts) =>
        string.Join(",", sorts.Select(sort => $"{sort.SortBy}:{sort.SortOrder}"));

    private static MapDataQueryOptions RailwaySortQuery(
        int serverId,
        IReadOnlyList<MapDataSort> sorts,
        string? itemKey)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind = "railway",
            query = new
            {
                serverId,
                itemKey,
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
                "Recovered Railway sort unexpectedly failed contract gate: " +
                string.Join(",", query.UnsupportedFeatures));
        return query;
    }

    private static IReadOnlyList<JsonElement> ReadAllRailwayRows(
        MapDataStore store,
        MapDataQueryOptions query,
        long sampledAt)
    {
        var rows = new List<JsonElement>();
        int page = 1;
        int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexedAtForTest(query with { Page = page }, sampledAt);
            if (expectedTotal < 0) expectedTotal = result.Total;
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (rows.Count != expectedTotal)
            throw new InvalidDataException($"Railway sort proof observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }

    private static void CollectRailwayMetrics(
        MapDataStore store, int serverId, out int trainTypeCount, out int qualityCount,
        out int powerCount, out int trainCfgCount, out int trainDataCount,
        out int arriveTsCount, out int robTimesCount, out int marchUuidCount, out int maxLootCountCount,
        out int protectTimeCount, out int currentGoodsRowCount, out int currentGoodsItemCount, out int currentGoodsAuditableCount)
    {
        trainTypeCount = qualityCount = powerCount = trainCfgCount = trainDataCount = arriveTsCount = robTimesCount = 0;
        marchUuidCount = maxLootCountCount = protectTimeCount = currentGoodsRowCount = currentGoodsItemCount = currentGoodsAuditableCount = 0;
        int page = 1; int observed = 0; int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(RailwayQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                if (row.TryGetProperty("trainType", out JsonElement trainType) && trainType.TryGetInt32(out int type) && type == 2) trainTypeCount++;
                if (row.TryGetProperty("quality", out JsonElement quality) && quality.TryGetInt32(out int q) && q > 0) qualityCount++;
                if (row.TryGetProperty("power", out JsonElement power) && power.TryGetInt64(out long pwr) && pwr >= 0) powerCount++;
                if (row.TryGetProperty("trainCfgId", out JsonElement cfg) && cfg.TryGetInt32(out int cfgId) && cfgId > 0) trainCfgCount++;
                if (row.TryGetProperty("trainDataJson", out JsonElement raw) && raw.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(raw.GetString())) trainDataCount++;
                if (row.TryGetProperty("arriveTs", out JsonElement arriveTs) && arriveTs.TryGetInt64(out long arrival) && arrival > 0) arriveTsCount++;
                if (row.TryGetProperty("robTimes", out JsonElement robTimes) && robTimes.TryGetInt32(out int robberies) && robberies >= 0) robTimesCount++;
                if (row.TryGetProperty("marchUuid", out JsonElement marchUuid) && marchUuid.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(marchUuid.GetString())) marchUuidCount++;
                if (row.TryGetProperty("maxLootCount", out JsonElement maxLootCount) &&
                    maxLootCount.TryGetInt32(out int maxLoot) && maxLoot >= 0) maxLootCountCount++;
                if (row.TryGetProperty("protectTime", out JsonElement protectTime) &&
                    protectTime.TryGetInt64(out long protection) && protection > 0) protectTimeCount++;
                if (row.TryGetProperty("currentGoods", out JsonElement goods) && goods.ValueKind == JsonValueKind.Array && goods.GetArrayLength() > 0)
                {
                    currentGoodsRowCount++;
                    foreach (JsonElement good in goods.EnumerateArray())
                    {
                        currentGoodsItemCount++;
                        bool auditable = good.ValueKind == JsonValueKind.Object &&
                            good.TryGetProperty("key", out JsonElement key) && key.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(key.GetString()) &&
                            good.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(name.GetString()) &&
                            good.TryGetProperty("iconPath", out JsonElement icon) && icon.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(icon.GetString()) &&
                            good.TryGetProperty("count", out JsonElement count) && count.TryGetDouble(out double amount) && amount > 0 &&
                            good.TryGetProperty("rewardType", out JsonElement rewardType) && rewardType.TryGetInt32(out _) &&
                            good.TryGetProperty("itemId", out JsonElement itemId) && itemId.TryGetInt64(out _);
                        if (auditable) currentGoodsAuditableCount++;
                    }
                }
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal)
            throw new InvalidDataException($"Railway metric read observed {observed}/{expectedTotal} rows.");
    }

    private static MapDataQueryOptions RailwayQuery(int serverId, int page = 1) => new(
        "railway", serverId, page, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, null, null, null, false, false, false,
        null, null, Array.Empty<string>());

    private static void TryDelete(string path)
    {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { }
        }
    }
}
