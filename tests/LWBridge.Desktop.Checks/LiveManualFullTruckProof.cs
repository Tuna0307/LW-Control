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
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
                reopenedTruckCount = reopened.SearchIndexed(TruckQuery(serverId)).Total;
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

    private static MapDataQueryOptions TruckQuery(int serverId, int page = 1) => new(
        "truck", serverId, page, MapDataQueryContract.RecoveredPageSize,
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
