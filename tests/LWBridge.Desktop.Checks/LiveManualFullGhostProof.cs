using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullGhostProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-ghost");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "manual-full-ghost-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-ghost-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedGhostCount = 0;
        int reopenedGhostCount = 0;
        double scanWallSeconds = 0;
        string scanMode = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_MODE"),
            "fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "normal";
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        var metrics = new GhostMetrics();
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
                        profileId = "manual-full-ghost-proof",
                        scanMode,
                        selectedTypes = new[] { "ghost" },
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
                        throw new InvalidDataException("Ordinary Manual Start did not expose expected Ghost scan identity/geometry.");
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
                                $"MANUAL_FULL_GHOST_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException("Ordinary Manual Ghost scan failed: " +
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
                            $"Ordinary Manual Ghost scan incomplete: phase={status.GetProperty("phase").GetString()}, " +
                            $"read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, " +
                            $"unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, " +
                            $"secondAttempts={secondAttempts}.");
                    }

                    publishedGhostCount = store.SearchIndexed(GhostQuery(serverId)).Total;
                    if (publishedGhostCount <= 0)
                        throw new InvalidDataException(
                            "Ordinary Manual Ghost scan published no Ghost Ops records.");
                    metrics = CollectGhostMetrics(store, serverId);
                    if (metrics.PointTypeCount != publishedGhostCount ||
                        metrics.RuntimeClassCount != publishedGhostCount ||
                        metrics.ConfigCount != publishedGhostCount ||
                        metrics.LevelCount != publishedGhostCount ||
                        metrics.QualityCount != publishedGhostCount ||
                        metrics.SpecialFlagCount != publishedGhostCount ||
                        metrics.SourceCount != publishedGhostCount)
                        throw new InvalidDataException(
                            "Published Ghost rows did not all preserve required point/config source fields.");
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
                reopenedGhostCount = reopened.SearchIndexed(GhostQuery(serverId)).Total;
            if (reopenedGhostCount != publishedGhostCount)
                throw new InvalidDataException("Ordinary Manual Ghost count changed after database reopen.");

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_ghost",
                totalBlocks = 2500,
                publishedGhostCount,
                reopenedGhostCount,
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
                metrics.TaskExpireTimeCount,
                metrics.ActEndTimeCount,
                metrics.TeamStartTimeCount,
                metrics.OwnerUidCount,
                metrics.OwnerServerCount,
                metrics.AllianceIdCount,
                metrics.SizeCount,
                metrics.StealListCountRows,
                metrics.MemberListCountRows,
                metrics.RewardConfigCount,
                metrics.WorldOpenCount,
                metrics.ProtectTimeCount,
                metrics.StealMaxTimesCount,
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_GHOST_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static GhostMetrics CollectGhostMetrics(MapDataStore store, int serverId)
    {
        int observed = 0, expectedTotal = -1, page = 1;
        var m = new GhostMetrics();
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(GhostQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                if (row.TryGetProperty("pointType", out JsonElement pointType) &&
                    pointType.TryGetInt32(out int pt) && pt == 29) m.PointTypeCount++;
                if (row.TryGetProperty("runtimeClass", out JsonElement runtimeClass) &&
                    runtimeClass.ValueKind == JsonValueKind.String &&
                    (runtimeClass.GetString() ?? "").EndsWith("GhostreconPointInfo", StringComparison.Ordinal))
                    m.RuntimeClassCount++;
                if (PositiveInt(row, "cfgId")) m.ConfigCount++;
                if (PositiveInt(row, "level")) m.LevelCount++;
                if (PositiveInt(row, "quality")) m.QualityCount++;
                if (row.TryGetProperty("isSpecial", out JsonElement special) &&
                    special.ValueKind is JsonValueKind.True or JsonValueKind.False) m.SpecialFlagCount++;
                if (PositiveLong(row, "completionTime")) m.CompletionTimeCount++;
                if (PositiveLong(row, "taskExpireTime")) m.TaskExpireTimeCount++;
                if (PositiveLong(row, "actEndTime")) m.ActEndTimeCount++;
                if (PositiveLong(row, "teamStartTime")) m.TeamStartTimeCount++;
                if (NonBlank(row, "ownerUid")) m.OwnerUidCount++;
                if (PositiveInt(row, "ownerServer")) m.OwnerServerCount++;
                if (NonBlank(row, "allianceId")) m.AllianceIdCount++;
                if (PositiveInt(row, "size")) m.SizeCount++;
                if (NonNegativeInt(row, "stealListCount")) m.StealListCountRows++;
                if (NonNegativeInt(row, "memberListCount")) m.MemberListCountRows++;
                if (row.TryGetProperty("rewardConfig", out JsonElement rewardConfig) &&
                    rewardConfig.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) m.RewardConfigCount++;
                if (row.TryGetProperty("worldOpen", out JsonElement worldOpen) &&
                    worldOpen.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) m.WorldOpenCount++;
                if (row.TryGetProperty("protectTime", out JsonElement protectTime) &&
                    protectTime.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) m.ProtectTimeCount++;
                if (row.TryGetProperty("stealMaxTimes", out JsonElement stealMaxTimes) &&
                    stealMaxTimes.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) m.StealMaxTimesCount++;
                if (row.TryGetProperty("source", out JsonElement source) &&
                    source.ValueKind == JsonValueKind.String &&
                    source.GetString() == "WorldPointManager._pointInfos+GhostreconPointInfo+TableName.LwGhostreconTask")
                    m.SourceCount++;
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal)
            throw new InvalidDataException($"Ghost metric read observed {observed}/{expectedTotal} rows.");
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

    private static MapDataQueryOptions GhostQuery(int serverId, int page = 1) => new(
        "ghost", serverId, page, MapDataQueryContract.RecoveredPageSize,
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

    private sealed class GhostMetrics
    {
        internal int PointTypeCount, RuntimeClassCount, ConfigCount, LevelCount, QualityCount;
        internal int SpecialFlagCount, CompletionTimeCount, TaskExpireTimeCount, ActEndTimeCount, TeamStartTimeCount;
        internal int OwnerUidCount, OwnerServerCount, AllianceIdCount, SizeCount, StealListCountRows, MemberListCountRows;
        internal int RewardConfigCount, WorldOpenCount, ProtectTimeCount, StealMaxTimesCount, SourceCount;
    }
}
