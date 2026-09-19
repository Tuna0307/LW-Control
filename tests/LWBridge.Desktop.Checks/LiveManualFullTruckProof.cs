using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullTruckProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-truck");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(proofRoot, "manual-full-truck-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-truck-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedTruckCount = 0;
        int reopenedTruckCount = 0;
        int trainTypeCount = 0;
        int qualityCount = 0;
        int powerCount = 0;
        int trainCfgCount = 0;
        int trainDataCount = 0;
        int arriveTsCount = 0;
        int robTimesCount = 0;
        int maxLootCountCount = 0;
        int protectTimeCount = 0;
        int specialUrCount = 0;
        int currentGoodsRowCount = 0;
        int currentGoodsItemCount = 0;
        int rawBaseGoodsRows = 0;
        int rawExtraGoodsRows = 0;
        int rawBaseCurRows = 0;
        int rawExtraCurRows = 0;
        int rawRewardEntries = 0;
        int ordinaryUrFilterCount = 0;
        int reindeerFilterCount = 0;
        int plunderableFilterCount = 0;
        int rewardOptionCount = 0;
        int itemFilterCount = 0;
        int remainingLootCountCount = 0;
        int sortCheckCount = 0;
        int reopenedSortCheckCount = 0;
        string? sampleItemKey = null;
        long filterSampledAt = 0;
        double scanWallSeconds = 0;
        string scanMode = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_MODE"),
            "fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "normal";
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
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
                try
                {
                    string? targetServerRaw =
                        Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_SERVER");
                    if (int.TryParse(targetServerRaw, out int targetServerId) &&
                        targetServerId is >= 1 and <= 99999)
                    {
                        JsonElement jumpPayload = JsonSerializer.SerializeToElement(
                            new { serverId = targetServerId },
                            JsonOptions.Default);
                        await service.InvokeAsync(
                            "server_jump",
                            jumpPayload,
                            operationCts.Token).ConfigureAwait(false);
                    }

                    JsonElement payload = JsonSerializer.SerializeToElement(new
                    {
                        profileId = "manual-full-truck-proof",
                        scanMode,
                        selectedTypes = new[] { "truck" },
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
                        throw new InvalidDataException("Ordinary Manual Start did not expose expected Truck scan identity/geometry.");
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
                            Console.Error.WriteLine($"MANUAL_FULL_TRUCK_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException("Ordinary Manual Truck scan failed: " +
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
                        throw new InvalidDataException($"Ordinary Manual Truck scan incomplete: phase={status.GetProperty("phase").GetString()}, read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, secondAttempts={secondAttempts}.");
                    }
                    publishedTruckCount = store.SearchIndexed(TruckQuery(serverId)).Total;
                    if (publishedTruckCount <= 0)
                        throw new InvalidDataException("Ordinary Manual Truck scan published no Truck records.");
                    CollectTruckMetrics(store, serverId, out trainTypeCount, out qualityCount,
                        out powerCount, out trainCfgCount, out trainDataCount, out arriveTsCount, out robTimesCount,
                        out maxLootCountCount, out protectTimeCount, out specialUrCount,
                        out currentGoodsRowCount, out currentGoodsItemCount,
                        out rawBaseGoodsRows, out rawExtraGoodsRows, out rawBaseCurRows,
                        out rawExtraCurRows, out rawRewardEntries);
                    if (currentGoodsRowCount <= 0 || currentGoodsItemCount <= 0)
                        throw new InvalidDataException("Ordinary Manual Truck scan reconstructed no authoritative currentGoods.");
                    if (maxLootCountCount <= 0)
                        throw new InvalidDataException("Ordinary Manual Truck scan reconstructed no source-safe maxLootCount.");
                    if (trainDataCount != 0)
                        throw new InvalidDataException($"Ordinary Manual Truck scan retained {trainDataCount} full trainDataJson payloads on the hot path.");

                    filterSampledAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    TruckFilterMetrics filters = ValidateTruckFilters(store, serverId, filterSampledAt);
                    ordinaryUrFilterCount = filters.OrdinaryUrCount;
                    reindeerFilterCount = filters.ReindeerCount;
                    plunderableFilterCount = filters.PlunderableCount;
                    rewardOptionCount = filters.RewardOptionCount;
                    itemFilterCount = filters.ItemFilterCount;
                    remainingLootCountCount = filters.RemainingLootCountCount;
                    sampleItemKey = filters.SampleItemKey;
                    sortCheckCount = ValidateTruckSorts(
                        store, serverId, filterSampledAt, filters.SampleItemKey);
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
            {
                reopenedTruckCount = reopened.SearchIndexed(TruckQuery(serverId)).Total;
                TruckFilterMetrics reopenedFilters = ValidateTruckFilters(reopened, serverId, filterSampledAt);
                if (reopenedFilters.OrdinaryUrCount != ordinaryUrFilterCount ||
                    reopenedFilters.ReindeerCount != reindeerFilterCount ||
                    reopenedFilters.PlunderableCount != plunderableFilterCount ||
                    reopenedFilters.RewardOptionCount != rewardOptionCount ||
                    reopenedFilters.ItemFilterCount != itemFilterCount ||
                    reopenedFilters.RemainingLootCountCount != remainingLootCountCount ||
                    !string.Equals(reopenedFilters.SampleItemKey, sampleItemKey, StringComparison.Ordinal))
                    throw new InvalidDataException("Truck filter/options results changed after database reopen.");
                reopenedSortCheckCount = ValidateTruckSorts(
                    reopened, serverId, filterSampledAt, reopenedFilters.SampleItemKey);
                if (reopenedSortCheckCount != sortCheckCount)
                    throw new InvalidDataException("Truck sort proof count changed after database reopen.");
            }
            if (reopenedTruckCount != publishedTruckCount)
                throw new InvalidDataException("Ordinary Manual Truck count changed after database reopen.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_truck",
                totalBlocks = 2500,
                publishedTruckCount,
                reopenedTruckCount,
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
                maxLootCountCount,
                protectTimeCount,
                specialUrCount,
                currentGoodsRowCount,
                currentGoodsItemCount,
                filterSampledAt,
                ordinaryUrFilterCount,
                reindeerFilterCount,
                plunderableFilterCount,
                rewardOptionCount,
                itemFilterCount,
                remainingLootCountCount,
                sortCheckCount,
                reopenedSortCheckCount,
                sampleItemKey,
                rawBaseGoodsRows,
                rawExtraGoodsRows,
                rawBaseCurRows,
                rawExtraCurRows,
                rawRewardEntries,
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_TRUCK_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    internal static async Task RunSourceOnlyAsync()
    {
        string gameRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("truck-source-live-proof", gameRoot);
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
            var request = new MapScanExecutionRequest("truck-source-live", context.ServerId, context.WorldId, context.TileWidth, context.TileHeight, ["truck"], 8, 2, context.PlayerTileX, context.PlayerTileY);
            Stopwatch sw = Stopwatch.StartNew();
            IReadOnlyList<MapScanBlockCapture> captures = await source.CaptureBatchAsync(request, blocks[0], blocks.Select(b => b.BlockIndex).ToHashSet(), p => Console.Error.WriteLine($"TRUCK_SOURCE_PROGRESS {p.Percent:F1}%"), cts.Token).ConfigureAwait(false);
            sw.Stop();
            MapStoredRecord[] records = captures.SelectMany(c => c.Records).GroupBy(r => r.RecordKey).Select(g => g.First()).ToArray();
            Console.WriteLine(JsonSerializer.Serialize(new { ok=true, proof="current_client_full_truck_source", captures=captures.Count, records=records.Length, elapsedSeconds=sw.Elapsed.TotalSeconds }, JsonOptions.Default));
        }
        catch (Exception error) { operationError = error; Console.Error.WriteLine("TRUCK_SOURCE_ERROR: " + error); throw; }
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

    private static void CollectTruckMetrics(
        MapDataStore store, int serverId, out int trainTypeCount, out int qualityCount,
        out int powerCount, out int trainCfgCount, out int trainDataCount,
        out int arriveTsCount, out int robTimesCount, out int maxLootCountCount,
        out int protectTimeCount, out int specialUrCount, out int currentGoodsRowCount,
        out int currentGoodsItemCount, out int rawBaseGoodsRows, out int rawExtraGoodsRows,
        out int rawBaseCurRows, out int rawExtraCurRows, out int rawRewardEntries)
    {
        trainTypeCount = qualityCount = powerCount = trainCfgCount = trainDataCount = arriveTsCount = robTimesCount = 0;
        maxLootCountCount = protectTimeCount = specialUrCount = currentGoodsRowCount = currentGoodsItemCount = 0;
        rawBaseGoodsRows = rawExtraGoodsRows = rawBaseCurRows = rawExtraCurRows = rawRewardEntries = 0;
        int page = 1; int observed = 0; int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(TruckQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                if (row.TryGetProperty("trainType", out JsonElement trainType) && trainType.TryGetInt32(out int type) && type == 1) trainTypeCount++;
                if (row.TryGetProperty("quality", out JsonElement quality) && quality.TryGetInt32(out int q) && q > 0) qualityCount++;
                if (row.TryGetProperty("power", out JsonElement power) && power.TryGetInt64(out long pwr) && pwr >= 0) powerCount++;
                if (row.TryGetProperty("trainCfgId", out JsonElement cfg) && cfg.TryGetInt32(out int cfgId) && cfgId > 0) trainCfgCount++;
                if (row.TryGetProperty("trainDataJson", out JsonElement raw) && raw.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(raw.GetString()))
                {
                    trainDataCount++;
                    try
                    {
                        using JsonDocument rawDoc = JsonDocument.Parse(raw.GetString()!);
                        JsonElement root = rawDoc.RootElement;
                        if (root.TryGetProperty("baseGoods", out JsonElement baseGoods) && baseGoods.ValueKind == JsonValueKind.Object)
                        {
                            rawBaseGoodsRows++;
                            if (baseGoods.TryGetProperty("cur", out JsonElement cur) && cur.ValueKind == JsonValueKind.Array && cur.GetArrayLength() > 0)
                            {
                                rawBaseCurRows++;
                                rawRewardEntries += cur.GetArrayLength();
                            }
                        }
                        if (root.TryGetProperty("extraGoods", out JsonElement extraGoods) && extraGoods.ValueKind == JsonValueKind.Object)
                        {
                            rawExtraGoodsRows++;
                            if (extraGoods.TryGetProperty("cur", out JsonElement cur) && cur.ValueKind == JsonValueKind.Array && cur.GetArrayLength() > 0)
                            {
                                rawExtraCurRows++;
                                rawRewardEntries += cur.GetArrayLength();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
                if (row.TryGetProperty("arriveTs", out JsonElement arriveTs) && arriveTs.TryGetInt64(out long arrival) && arrival > 0) arriveTsCount++;
                if (row.TryGetProperty("robTimes", out JsonElement robTimes) && robTimes.TryGetInt32(out int robberies) && robberies >= 0) robTimesCount++;
                if (row.TryGetProperty("maxLootCount", out JsonElement maxLootCount) && maxLootCount.TryGetInt32(out int maxLoot) && maxLoot >= 0) maxLootCountCount++;
                if (row.TryGetProperty("protectTime", out JsonElement protectTime) && protectTime.TryGetInt64(out long protection) && protection > 0) protectTimeCount++;
                if (row.TryGetProperty("isSpecialURQuality", out JsonElement specialUr) && specialUr.ValueKind == JsonValueKind.True) specialUrCount++;
                if (row.TryGetProperty("currentGoods", out JsonElement goods) && goods.ValueKind == JsonValueKind.Array && goods.GetArrayLength() > 0)
                {
                    currentGoodsRowCount++;
                    currentGoodsItemCount += goods.GetArrayLength();
                }
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal)
            throw new InvalidDataException($"Truck metric read observed {observed}/{expectedTotal} rows.");
    }

    private sealed record TruckFilterMetrics(
        int OrdinaryUrCount,
        int ReindeerCount,
        int PlunderableCount,
        int RewardOptionCount,
        int ItemFilterCount,
        int RemainingLootCountCount,
        string SampleItemKey);

    private static TruckFilterMetrics ValidateTruckFilters(MapDataStore store, int serverId, long sampledAt)
    {
        IReadOnlyList<JsonElement> rows = ReadAllTruckRows(store, TruckQuery(serverId), sampledAt);
        if (rows.Count == 0)
            throw new InvalidDataException("Truck filter proof has no active Truck rows.");

        int remainingLootCountCount = rows.Count(row =>
            row.TryGetProperty("remainingLootCount", out JsonElement remaining) &&
            remaining.TryGetInt32(out int value) && value >= 0);
        if (remainingLootCountCount != rows.Count)
            throw new InvalidDataException($"Truck remainingLootCount coverage is {remainingLootCountCount}/{rows.Count}.");

        static string Uuid(JsonElement row) =>
            row.TryGetProperty("uuid", out JsonElement uuid) && uuid.ValueKind == JsonValueKind.String
                ? uuid.GetString() ?? string.Empty
                : string.Empty;

        var ordinaryUrExpected = rows
            .Where(row =>
                row.TryGetProperty("quality", out JsonElement quality) && quality.TryGetInt32(out int q) && q >= 5 &&
                !(row.TryGetProperty("isSpecialURQuality", out JsonElement special) && special.ValueKind == JsonValueKind.True))
            .Select(Uuid).Where(uuid => uuid.Length > 0).ToHashSet(StringComparer.Ordinal);
        var reindeerExpected = rows
            .Where(row => row.TryGetProperty("isSpecialURQuality", out JsonElement special) && special.ValueKind == JsonValueKind.True)
            .Select(Uuid).Where(uuid => uuid.Length > 0).ToHashSet(StringComparer.Ordinal);
        var plunderableExpected = rows
            .Where(row => IsFrontendPlunderableTruck(row, sampledAt))
            .Select(Uuid).Where(uuid => uuid.Length > 0).ToHashSet(StringComparer.Ordinal);

        HashSet<string> ordinaryUrActual = ReadAllTruckRows(
                store, TruckQuery(serverId, quality: "ur"), sampledAt)
            .Select(Uuid).ToHashSet(StringComparer.Ordinal);
        HashSet<string> reindeerActual = ReadAllTruckRows(
                store, TruckQuery(serverId, reindeerOnly: true), sampledAt)
            .Select(Uuid).ToHashSet(StringComparer.Ordinal);
        HashSet<string> plunderableActual = ReadAllTruckRows(
                store, TruckQuery(serverId, plunderableOnly: true), sampledAt)
            .Select(Uuid).ToHashSet(StringComparer.Ordinal);

        if (!ordinaryUrActual.SetEquals(ordinaryUrExpected))
            throw new InvalidDataException($"Truck ordinary-UR filter mismatch: actual={ordinaryUrActual.Count}, expected={ordinaryUrExpected.Count}.");
        if (!reindeerActual.SetEquals(reindeerExpected))
            throw new InvalidDataException($"Truck reindeer-only filter mismatch: actual={reindeerActual.Count}, expected={reindeerExpected.Count}.");
        if (!plunderableActual.SetEquals(plunderableExpected))
            throw new InvalidDataException($"Truck plunderable-only filter mismatch: actual={plunderableActual.Count}, expected={plunderableExpected.Count}.");

        if (ordinaryUrExpected.Count == 0)
            throw new InvalidDataException("Truck filter proof population contains no ordinary UR Truck.");
        if (reindeerExpected.Count == 0)
            throw new InvalidDataException("Truck filter proof population contains no reindeer/special-UR Truck.");
        if (plunderableExpected.Count == 0)
            throw new InvalidDataException("Truck filter proof population contains no plunderable Truck.");

        MapOptionAggregates options = store.ReadOptionAggregatesAt(
            new MapOptionSourceSelection(serverId, null), sampledAt);
        MapRewardItemOptionAggregate[] truckItems = options.RewardItems
            .Where(item => item.Kind == "truck" && !string.IsNullOrWhiteSpace(item.Key))
            .ToArray();
        if (truckItems.Length == 0)
            throw new InvalidDataException("Truck map_data_options produced no retained-item options.");

        (string Key, HashSet<string> Uuids)[] itemCandidates = truckItems
            .Select(item => (
                item.Key,
                rows.Where(row => TruckContainsItem(row, item.Key))
                    .Select(Uuid).Where(uuid => uuid.Length > 0).ToHashSet(StringComparer.Ordinal)))
            .Where(item => item.Item2.Count > 0)
            .ToArray();
        (string Key, HashSet<string> Uuids) selected = itemCandidates
            .Where(item => item.Uuids.Count < rows.Count)
            .OrderByDescending(item => item.Uuids.Count)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected.Uuids is null)
            selected = itemCandidates
                .OrderByDescending(item => item.Uuids.Count)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .First();
        if (selected.Uuids.Count == 0)
            throw new InvalidDataException("Truck retained-item option does not match any active Truck row.");

        HashSet<string> itemActual = ReadAllTruckRows(
                store, TruckQuery(serverId, itemKey: selected.Key), sampledAt)
            .Select(Uuid).ToHashSet(StringComparer.Ordinal);
        if (!itemActual.SetEquals(selected.Uuids))
            throw new InvalidDataException($"Truck retained-item filter mismatch for {selected.Key}: actual={itemActual.Count}, expected={selected.Uuids.Count}.");

        return new TruckFilterMetrics(
            ordinaryUrActual.Count,
            reindeerActual.Count,
            plunderableActual.Count,
            truckItems.Length,
            itemActual.Count,
            remainingLootCountCount,
            selected.Key);
    }

    private static int ValidateTruckSorts(
        MapDataStore store,
        int serverId,
        long sampledAt,
        string itemKey)
    {
        IReadOnlyList<JsonElement> allRows = ReadAllTruckRows(
            store, TruckQuery(serverId), sampledAt);
        if (allRows.Count < 2)
            throw new InvalidDataException("Truck sort proof requires at least two active Truck rows.");

        int checks = 0;
        foreach (string sortBy in new[]
        {
            "quality",
            "power",
            "itemCount",
            "remainingLootCount",
            "arriveTime",
            "updatedAt",
        })
        {
            foreach (string sortOrder in new[] { "asc", "desc" })
            {
                ValidateTruckSortOrder(
                    store,
                    serverId,
                    sampledAt,
                    allRows,
                    [new MapDataSort(sortBy, sortOrder)],
                    sortBy == "itemCount" ? itemKey : null);
                checks++;
            }
        }

        ValidateTruckSortOrder(
            store,
            serverId,
            sampledAt,
            allRows,
            [
                new MapDataSort("quality", "desc"),
                new MapDataSort("power", "asc"),
                new MapDataSort("remainingLootCount", "desc"),
                new MapDataSort("arriveTime", "asc"),
                new MapDataSort("updatedAt", "desc"),
            ],
            itemKey: null);
        checks++;

        ValidateTruckSortOrder(
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

        ValidateTruckSortOrder(
            store,
            serverId,
            sampledAt,
            allRows,
            [
                new MapDataSort("quality", "asc"),
                new MapDataSort("power", "desc"),
                new MapDataSort("itemCount", "desc"),
                new MapDataSort("remainingLootCount", "asc"),
                new MapDataSort("arriveTime", "desc"),
                new MapDataSort("updatedAt", "desc"),
            ],
            itemKey);
        checks++;

        return checks;
    }

    private static void ValidateTruckSortOrder(
        MapDataStore store,
        int serverId,
        long sampledAt,
        IReadOnlyList<JsonElement> allRows,
        IReadOnlyList<MapDataSort> sorts,
        string? itemKey)
    {
        IReadOnlyList<JsonElement> expectedSource = itemKey is null
            ? allRows
            : allRows.Where(row => TruckContainsItem(row, itemKey)).ToArray();
        var expected = expectedSource.ToList();
        expected.Sort((left, right) => CompareTruckRows(left, right, sorts, itemKey));

        MapDataQueryOptions query = TruckSortQuery(serverId, sorts, itemKey);
        IReadOnlyList<JsonElement> actual = ReadAllTruckRows(store, query, sampledAt);
        if (actual.Count != expected.Count)
            throw new InvalidDataException(
                $"Truck sort row count mismatch for {DescribeSorts(sorts)}: actual={actual.Count}, expected={expected.Count}.");

        for (int index = 0; index < expected.Count; index++)
        {
            string expectedUuid = TruckUuid(expected[index]);
            string actualUuid = TruckUuid(actual[index]);
            if (!string.Equals(actualUuid, expectedUuid, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Truck sort mismatch for {DescribeSorts(sorts)} at {index}: actual={actualUuid}, expected={expectedUuid}.");
        }
    }

    private static int CompareTruckRows(
        JsonElement left,
        JsonElement right,
        IReadOnlyList<MapDataSort> sorts,
        string? itemKey)
    {
        foreach (MapDataSort sort in sorts)
        {
            double? leftValue = TruckSortValue(left, sort.SortBy, itemKey);
            double? rightValue = TruckSortValue(right, sort.SortBy, itemKey);
            int comparison;
            if (!leftValue.HasValue && !rightValue.HasValue)
            {
                comparison = 0;
            }
            else if (!leftValue.HasValue)
            {
                comparison = 1;
            }
            else if (!rightValue.HasValue)
            {
                comparison = -1;
            }
            else
            {
                comparison = leftValue.Value.CompareTo(rightValue.Value);
                if (sort.SortOrder == "desc") comparison = -comparison;
            }

            if (comparison != 0) return comparison;
        }

        // Current Truck record identity is the march/train UUID, so this independently
        // mirrors the recovered final record_key ASC tie-breaker without using SQL order.
        return StringComparer.Ordinal.Compare(TruckUuid(left), TruckUuid(right));
    }

    private static double? TruckSortValue(JsonElement row, string sortBy, string? itemKey)
    {
        if (sortBy == "quality")
        {
            if (row.TryGetProperty("isSpecialURQuality", out JsonElement special) &&
                special.ValueKind == JsonValueKind.True)
                return 100d;
            return OptionalJsonNumber(row, "quality");
        }
        if (sortBy == "power")
            return OptionalJsonNumber(row, "power");
        if (sortBy == "itemCount")
            return TruckItemCount(row, itemKey);
        if (sortBy == "remainingLootCount")
            return OptionalJsonNumber(row, "remainingLootCount") ?? 0d;
        if (sortBy == "arriveTime")
        {
            double? value = OptionalJsonNumber(row, "arriveTs");
            return value is > 0d ? value : null;
        }
        if (sortBy == "updatedAt")
            return OptionalJsonNumber(row, "updatedAt");
        throw new InvalidDataException("Unexpected Truck sort key: " + sortBy);
    }

    private static double TruckItemCount(JsonElement row, string? itemKey)
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

    private static double? OptionalJsonNumber(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static string TruckUuid(JsonElement row) =>
        row.TryGetProperty("uuid", out JsonElement uuid) &&
        uuid.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(uuid.GetString())
            ? uuid.GetString()!
            : throw new InvalidDataException("Truck sort proof row has no UUID.");

    private static string DescribeSorts(IReadOnlyList<MapDataSort> sorts) =>
        string.Join(",", sorts.Select(sort => $"{sort.SortBy}:{sort.SortOrder}"));

    private static MapDataQueryOptions TruckSortQuery(
        int serverId,
        IReadOnlyList<MapDataSort> sorts,
        string? itemKey)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind = "truck",
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
                "Recovered Truck sort unexpectedly failed contract gate: " +
                string.Join(",", query.UnsupportedFeatures));
        return query;
    }

    private static IReadOnlyList<JsonElement> ReadAllTruckRows(
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
            else if (result.Total != expectedTotal)
                throw new InvalidDataException("Truck filter query total changed across pages.");
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (rows.Count != expectedTotal)
            throw new InvalidDataException($"Truck filter query observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }

    private static bool IsFrontendPlunderableTruck(JsonElement row, long sampledAt)
    {
        if (!row.TryGetProperty("arriveTs", out JsonElement arrivalValue) ||
            !arrivalValue.TryGetInt64(out long arrival) || arrival <= sampledAt)
            return false;

        bool special = row.TryGetProperty("isSpecialURQuality", out JsonElement specialValue) &&
            specialValue.ValueKind == JsonValueKind.True;
        int maxLoot = 0;
        if (special)
        {
            maxLoot = 1;
        }
        else if (row.TryGetProperty("maxLootCount", out JsonElement maxValue) &&
                 maxValue.TryGetInt32(out int parsedMax) && parsedMax > 0)
        {
            maxLoot = parsedMax;
        }

        int robTimes = row.TryGetProperty("robTimes", out JsonElement robValue) &&
                       robValue.TryGetInt32(out int parsedRob)
            ? Math.Max(0, parsedRob)
            : 0;
        return maxLoot > 0 && robTimes < maxLoot;
    }

    private static bool TruckContainsItem(JsonElement row, string key)
    {
        if (!row.TryGetProperty("currentGoods", out JsonElement goods) || goods.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement item in goods.EnumerateArray())
            if (item.TryGetProperty("key", out JsonElement itemKey) &&
                itemKey.ValueKind == JsonValueKind.String &&
                string.Equals(itemKey.GetString(), key, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static MapDataQueryOptions TruckQuery(
        int serverId,
        int page = 1,
        string? quality = null,
        string? itemKey = null,
        bool plunderableOnly = false,
        bool reindeerOnly = false) => new(
        "truck", serverId, page, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, quality, itemKey, null, plunderableOnly, false, reindeerOnly,
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
