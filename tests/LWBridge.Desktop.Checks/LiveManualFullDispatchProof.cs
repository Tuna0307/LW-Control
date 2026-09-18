using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullDispatchProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-dispatch");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "manual-full-dispatch-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-dispatch-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedDispatchCount = 0;
        int reopenedDispatchCount = 0;
        double scanWallSeconds = 0;
        string scanMode = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_MODE"),
            "fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "normal";
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        var metrics = new DispatchMetrics();
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
                        profileId = "manual-full-dispatch-proof",
                        scanMode,
                        selectedTypes = new[] { "dispatch" },
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
                        throw new InvalidDataException("Ordinary Manual Start did not expose expected Dispatch scan identity/geometry.");
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
                            Console.Error.WriteLine(
                                $"MANUAL_FULL_DISPATCH_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException("Ordinary Manual Dispatch scan failed: " +
                                (status.TryGetProperty("lastError", out JsonElement last)
                                    ? last.GetString() : "unknown"));
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
                        IReadOnlyList<MapScanBlockCheckpoint> partial =
                            store.ReadScanBlockCheckpointsForTest(runId);
                        int secondAttempts = partial.Count(item => item.Attempts > 1);
                        throw new InvalidDataException(
                            $"Ordinary Manual Dispatch scan incomplete: phase={status.GetProperty("phase").GetString()}, " +
                            $"read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, " +
                            $"unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, " +
                            $"secondAttempts={secondAttempts}.");
                    }

                    publishedDispatchCount = store.SearchIndexed(DispatchQuery(serverId)).Total;
                    if (publishedDispatchCount <= 0)
                        throw new InvalidDataException(
                            "Ordinary Manual Dispatch scan published no Secret Task records.");
                    metrics = CollectDispatchMetrics(store, serverId);
                    if (metrics.PointTypeCount != publishedDispatchCount ||
                        metrics.RuntimeClassCount != publishedDispatchCount ||
                        metrics.ConfigCount != publishedDispatchCount ||
                        metrics.LevelCount != publishedDispatchCount ||
                        metrics.QualityCount != publishedDispatchCount ||
                        metrics.SpecialFlagCount != publishedDispatchCount ||
                        metrics.SourceCount != publishedDispatchCount)
                        throw new InvalidDataException(
                            "Published Dispatch rows did not all preserve required point/config source fields.");
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
                reopenedDispatchCount = reopened.SearchIndexed(DispatchQuery(serverId)).Total;
            if (reopenedDispatchCount != publishedDispatchCount)
                throw new InvalidDataException("Ordinary Manual Dispatch count changed after database reopen.");

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_dispatch",
                totalBlocks = 2500,
                publishedDispatchCount,
                reopenedDispatchCount,
                scanMode,
                concurrency = expectedConcurrency,
                scanWallSeconds,
                metrics.PointTypeCount,
                metrics.RuntimeClassCount,
                metrics.ConfigCount,
                metrics.LevelCount,
                metrics.QualityCount,
                metrics.SpecialFlagCount,
                metrics.CompletionTimeCount,
                metrics.RewardedCount,
                metrics.ActEndTimeCount,
                metrics.ExpiredTimeCount,
                metrics.OwnerUidCount,
                metrics.AllianceIdCount,
                metrics.StealListCountRows,
                metrics.AccListCountRows,
                metrics.NameKeyCount,
                metrics.SourceCount,
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_DISPATCH_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static DispatchMetrics CollectDispatchMetrics(MapDataStore store, int serverId)
    {
        int observed = 0, expectedTotal = -1, page = 1;
        var m = new DispatchMetrics();
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(DispatchQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                if (row.TryGetProperty("pointType", out JsonElement pointType) &&
                    pointType.TryGetInt32(out int pt) && pt == 17) m.PointTypeCount++;
                if (row.TryGetProperty("runtimeClass", out JsonElement runtimeClass) &&
                    runtimeClass.ValueKind == JsonValueKind.String &&
                    (runtimeClass.GetString() ?? "").EndsWith("HeroDispatchMissionPointInfo", StringComparison.Ordinal))
                    m.RuntimeClassCount++;
                if (PositiveInt(row, "cfgId")) m.ConfigCount++;
                if (PositiveInt(row, "level")) m.LevelCount++;
                if (PositiveInt(row, "quality")) m.QualityCount++;
                if (row.TryGetProperty("isSpecial", out JsonElement special) &&
                    special.ValueKind is JsonValueKind.True or JsonValueKind.False) m.SpecialFlagCount++;
                if (PositiveLong(row, "completionTime")) m.CompletionTimeCount++;
                if (row.TryGetProperty("rewarded", out JsonElement rewarded) && rewarded.TryGetInt32(out _)) m.RewardedCount++;
                if (PositiveLong(row, "actEndTime")) m.ActEndTimeCount++;
                if (PositiveLong(row, "expiredTime")) m.ExpiredTimeCount++;
                if (NonBlank(row, "ownerUid")) m.OwnerUidCount++;
                if (NonBlank(row, "allianceId")) m.AllianceIdCount++;
                if (NonNegativeInt(row, "stealListCount")) m.StealListCountRows++;
                if (NonNegativeInt(row, "accListCount")) m.AccListCountRows++;
                if (NonBlank(row, "dispatchNameKey")) m.NameKeyCount++;
                if (row.TryGetProperty("source", out JsonElement source) &&
                    source.ValueKind == JsonValueKind.String &&
                    source.GetString() == "WorldPointManager._pointInfos+HeroDispatchMissionPointInfo")
                    m.SourceCount++;
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal)
            throw new InvalidDataException($"Dispatch metric read observed {observed}/{expectedTotal} rows.");
        return m;
    }
    private static bool PositiveInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) && parsed > 0;

    private static bool NonNegativeInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) && parsed >= 0;

    private static bool PositiveLong(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) && parsed > 0;

    private static bool NonBlank(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static MapDataQueryOptions DispatchQuery(int serverId, int page = 1) => new(
        "dispatch", serverId, page, MapDataQueryContract.RecoveredPageSize,
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

    private sealed class DispatchMetrics
    {
        internal int PointTypeCount, RuntimeClassCount, ConfigCount, LevelCount, QualityCount;
        internal int SpecialFlagCount, CompletionTimeCount, RewardedCount, ActEndTimeCount, ExpiredTimeCount;
        internal int OwnerUidCount, AllianceIdCount, StealListCountRows, AccListCountRows, NameKeyCount, SourceCount;
    }
}
