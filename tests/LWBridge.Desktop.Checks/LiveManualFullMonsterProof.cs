using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullMonsterProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(proofRoot, "manual-full-monster-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-monster-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedMonsterCount = 0;
        int reopenedMonsterCount = 0;
        int distanceCount = 0;
        int zMBossInfoCount = 0;
        int shieldDeadlineCount = 0;
        int activeShieldDeadlineCount = 0;
        int monsterInvasionBossCount = 0;
        int monsterProtectionDetailTargetCount = 0;
        int monsterProtectionDetailRequestCount = 0;
        int monsterProtectionDetailRetryCount = 0;
        int monsterProtectionDetailReadyCount = 0;
        string? monsterProtectionDetailError = null;
        double? minDistance = null;
        double? maxDistance = null;
        long? minShieldDeadline = null;
        long? maxShieldDeadline = null;
        long filterSampledAt = 0;
        MonsterFilterProofHelper.Metrics? filterMetrics = null;
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
                var source = new CurrentClientMapBlockSource(lifecycle);
                var service = new ManualMapScanCommandService(store, source.GetCurrentContextAsync, source);
                try
                {
                JsonElement payload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "manual-full-monster-proof",
                    scanMode,
                    selectedTypes = new[] { "monster" },
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
                {
                    throw new InvalidDataException("Ordinary Manual Start did not expose the expected Monster scan identity/geometry.");
                }

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
                        Console.Error.WriteLine($"MANUAL_FULL_MONSTER_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                    }
                    if (phase == "completed") break;
                    if (phase == "error")
                        throw new InvalidDataException("Ordinary Manual Monster scan failed: " +
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
                    throw new InvalidDataException($"Ordinary Manual Monster scan incomplete: phase={status.GetProperty("phase").GetString()}, read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, secondAttempts={secondAttempts}.");
                }

                CurrentClientMapBlockSource.MonsterProtectionDetailMetrics metrics =
                    source.LastMonsterProtectionDetailMetrics ?? throw new InvalidDataException("Monster source did not retain full-world Monster Protection detail metrics.");
                monsterInvasionBossCount = metrics.BossCount;
                monsterProtectionDetailTargetCount = metrics.TargetCount;
                monsterProtectionDetailRequestCount = metrics.RequestCount;
                monsterProtectionDetailRetryCount = metrics.RetryCount;
                monsterProtectionDetailReadyCount = metrics.ReadyCount;
                monsterProtectionDetailError = metrics.Error;
                publishedMonsterCount = store.SearchIndexed(MonsterQuery(serverId)).Total;
                if (publishedMonsterCount <= 0)
                    throw new InvalidDataException("Ordinary Manual Monster scan published no Monster records.");
                CollectMonsterMetrics(store, serverId, out distanceCount, out zMBossInfoCount,
                    out shieldDeadlineCount, out activeShieldDeadlineCount,
                    out minDistance, out maxDistance, out minShieldDeadline, out maxShieldDeadline);
                filterSampledAt =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                filterMetrics = MonsterFilterProofHelper.Validate(
                    store,
                    serverId,
                    filterSampledAt);
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
            {
                reopenedMonsterCount = reopened.SearchIndexed(MonsterQuery(serverId)).Total;
                MonsterFilterProofHelper.Metrics reopenedFilterMetrics =
                    MonsterFilterProofHelper.Validate(
                        reopened,
                        serverId,
                        filterSampledAt);
                if (filterMetrics is null ||
                    reopenedFilterMetrics != filterMetrics)
                    throw new InvalidDataException(
                        "Monster filter proof changed after database reopen.");
            }
            if (reopenedMonsterCount != publishedMonsterCount)
                throw new InvalidDataException("Ordinary Manual Monster count changed after database reopen.");
            if (filterMetrics is null)
                throw new InvalidDataException(
                    "Monster filter proof did not produce metrics.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_monster",
                totalBlocks = 2500,
                publishedMonsterCount,
                reopenedMonsterCount,
                scanMode,
                concurrency = expectedConcurrency,
                scanWallSeconds,
                distanceCount,
                minDistance,
                maxDistance,
                zMBossInfoCount,
                shieldDeadlineCount,
                activeShieldDeadlineCount,
                monsterInvasionBossCount,
                monsterProtectionDetailTargetCount,
                monsterProtectionDetailRequestCount,
                monsterProtectionDetailRetryCount,
                monsterProtectionDetailReadyCount,
                monsterProtectionDetailError,
                minShieldDeadline,
                maxShieldDeadline,
                filterSampledAt,
                filterComparedRowCount = filterMetrics.RowCount,
                filterCheckCount = filterMetrics.CheckCount,
                filterMonsterNameCount = filterMetrics.MonsterNameCount,
                filterMaxLevel = filterMetrics.MaxLevel,
                filterMaxLevelCount = filterMetrics.MaxLevelCount,
                filterNameAndMaxCount = filterMetrics.NameAndMaxCount,
                filterKeywordIdentityCount =
                    filterMetrics.KeywordIdentityCount,
                filterKeywordPercentLiteralCount =
                    filterMetrics.KeywordPercentLiteralCount,
                filterKeywordUnderscoreLiteralCount =
                    filterMetrics.KeywordUnderscoreLiteralCount,
                filterKeywordBackslashLiteralCount =
                    filterMetrics.KeywordBackslashLiteralCount,
                filterResolvedNameKeyCount =
                    filterMetrics.ResolvedNameKeyCount,
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_MONSTER_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }


    private static void CollectMonsterMetrics(
        MapDataStore store, int serverId, out int distanceCount, out int zMBossInfoCount,
        out int shieldDeadlineCount, out int activeShieldDeadlineCount,
        out double? minDistance, out double? maxDistance, out long? minShieldDeadline, out long? maxShieldDeadline)
    {
        distanceCount = 0; zMBossInfoCount = 0; shieldDeadlineCount = 0; activeShieldDeadlineCount = 0;
        minDistance = null; maxDistance = null; minShieldDeadline = null; maxShieldDeadline = null;
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int page = 1; int observed = 0; int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(MonsterQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                if (row.TryGetProperty("distanceFromHome", out JsonElement distance) && distance.TryGetDouble(out double d) && double.IsFinite(d) && d >= 0)
                {
                    distanceCount++;
                    minDistance = minDistance is null ? d : Math.Min(minDistance.Value, d);
                    maxDistance = maxDistance is null ? d : Math.Max(maxDistance.Value, d);
                }
                if (row.TryGetProperty("zMBossId", out JsonElement zMBossId) && zMBossId.TryGetInt32(out int bossId) && bossId > 0)
                    zMBossInfoCount++;
                if (row.TryGetProperty("shieldEndTime", out JsonElement shield) && shield.TryGetInt64(out long deadline) && deadline > 0)
                {
                    shieldDeadlineCount++;
                    long deadlineMs = deadline < 1_000_000_000_000L ? checked(deadline * 1000L) : deadline;
                    if (deadlineMs > nowMs) activeShieldDeadlineCount++;
                    minShieldDeadline = minShieldDeadline is null ? deadline : Math.Min(minShieldDeadline.Value, deadline);
                    maxShieldDeadline = maxShieldDeadline is null ? deadline : Math.Max(maxShieldDeadline.Value, deadline);
                }
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal) throw new InvalidDataException($"Monster metric read observed {observed}/{expectedTotal} rows.");
    }

    private static MapDataQueryOptions MonsterQuery(int serverId, int page = 1) => new(
        "monster", serverId, page, MapDataQueryContract.RecoveredPageSize,
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
