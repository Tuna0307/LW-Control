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
                    publishedRailwayCount = store.SearchIndexed(RailwayQuery(serverId)).Total;
                    if (publishedRailwayCount <= 0)
                        throw new InvalidDataException("Ordinary Manual Railway scan published no Railway/Train records.");
                    CollectRailwayMetrics(store, serverId, out trainTypeCount, out qualityCount,
                        out powerCount, out trainCfgCount, out trainDataCount, out arriveTsCount, out robTimesCount,
                        out marchUuidCount, out maxLootCountCount, out protectTimeCount,
                        out currentGoodsRowCount, out currentGoodsItemCount, out currentGoodsAuditableCount);
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
                reopenedRailwayCount = reopened.SearchIndexed(RailwayQuery(serverId)).Total;
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
