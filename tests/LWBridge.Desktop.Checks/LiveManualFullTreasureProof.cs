using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullTreasureProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-treasure");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "manual-full-treasure-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-treasure-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedTreasureCount = 0;
        int reopenedTreasureCount = 0;
        double scanWallSeconds = 0;
        string scanMode = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_MODE"),
            "fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "normal";
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        var metrics = new TreasureMetrics();
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
                        profileId = "manual-full-treasure-proof",
                        scanMode,
                        selectedTypes = new[] { "treasure" },
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
                        throw new InvalidDataException("Ordinary Manual Start did not expose expected Treasure scan identity/geometry.");
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
                                $"MANUAL_FULL_TREASURE_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException("Ordinary Manual Treasure scan failed: " +
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
                            $"Ordinary Manual Treasure scan incomplete: phase={status.GetProperty("phase").GetString()}, " +
                            $"read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, " +
                            $"unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, " +
                            $"secondAttempts={secondAttempts}.");
                    }

                    publishedTreasureCount = store.SearchIndexed(TreasureQuery(serverId)).Total;
                    if (publishedTreasureCount <= 0)
                        throw new InvalidDataException(
                            "Ordinary Manual Treasure scan published no Treasure records.");
                    metrics = CollectTreasureMetrics(store, serverId);
                    if (metrics.ValidIdentityCount != publishedTreasureCount ||
                        metrics.ValidSourceCount != publishedTreasureCount ||
                        metrics.OrdinaryCount + metrics.SuppliesCount != publishedTreasureCount)
                        throw new InvalidDataException(
                            "Published Treasure rows did not all preserve the recovered native identity/type/source contract.");
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
                reopenedTreasureCount = reopened.SearchIndexed(TreasureQuery(serverId)).Total;
            if (reopenedTreasureCount != publishedTreasureCount)
                throw new InvalidDataException("Ordinary Manual Treasure count changed after database reopen.");

            string proofJson = JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_treasure",
                totalBlocks = 2500,
                publishedTreasureCount,
                reopenedTreasureCount,
                scanMode,
                concurrency = expectedConcurrency,
                scanWallSeconds,
                metrics.OrdinaryCount,
                metrics.SuppliesCount,
                metrics.ValidIdentityCount,
                metrics.ValidSourceCount,
                metrics.OrdinaryRewardedCountRows,
                metrics.OrdinaryDiggingCountRows,
                metrics.OrdinaryRewardMaxRows,
                metrics.OrdinaryRemainingBoxesRows,
                metrics.SuppliesStateRows,
                metrics.SuppliesRewardedCountRows,
            }, JsonOptions.Default);
            File.WriteAllText(Path.Combine(proofRoot, "last-proof.json"), proofJson);
            Console.WriteLine(proofJson);
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_TREASURE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static TreasureMetrics CollectTreasureMetrics(MapDataStore store, int serverId)
    {
        int observed = 0, expectedTotal = -1, page = 1;
        var m = new TreasureMetrics();
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(TreasureQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                bool ordinary = row.TryGetProperty("pointType", out JsonElement pointType) && pointType.TryGetInt32(out int pt) && pt == 21 &&
                    row.TryGetProperty("runtimeClass", out JsonElement runtimeClass) && runtimeClass.ValueKind == JsonValueKind.String &&
                    (runtimeClass.GetString() ?? "").EndsWith("TreasurePointInfo", StringComparison.Ordinal) &&
                    PositiveInt(row, "treasureType") && ExactInt(row, "suppliesType", 0) &&
                    ExactString(row, "source", "WorldPointManager._pointInfos+TreasurePointInfo");
                bool supplies = row.TryGetProperty("pointType", out pointType) && pointType.TryGetInt32(out pt) && pt == 27 &&
                    row.TryGetProperty("runtimeClass", out runtimeClass) && runtimeClass.ValueKind == JsonValueKind.String &&
                    (runtimeClass.GetString() ?? "").EndsWith("WorldSuppliesPoint", StringComparison.Ordinal) &&
                    ExactInt(row, "treasureType", 0) && PositiveInt(row, "suppliesType") && PositiveInt(row, "configId") &&
                    ExactString(row, "source", "WorldPointManager._pointInfos+WorldSuppliesPoint+TableName.LWIceSupplies");
                if (!ordinary && !supplies)
                    throw new InvalidDataException("Published Treasure row did not match either recovered native Treasure shape.");
                if (!PositiveInt(row, "pointId") || !NonBlank(row, "uuid") || !ExactInt(row, "serverId", serverId))
                    throw new InvalidDataException("Published Treasure row lost required identity/server fields.");
                if (ordinary)
                {
                    m.OrdinaryCount++;
                    if (NonNegativeInt(row, "rewardedCount")) m.OrdinaryRewardedCountRows++;
                    if (NonNegativeInt(row, "diggingCount")) m.OrdinaryDiggingCountRows++;
                    if (NonNegativeInt(row, "rewardMax")) m.OrdinaryRewardMaxRows++;
                    if (NonNegativeInt(row, "remainingBoxes")) m.OrdinaryRemainingBoxesRows++;
                }
                else
                {
                    m.SuppliesCount++;
                    if (row.TryGetProperty("state", out JsonElement state) && state.TryGetInt32(out _)) m.SuppliesStateRows++;
                    if (NonNegativeInt(row, "rewardedCount")) m.SuppliesRewardedCountRows++;
                }
                m.ValidIdentityCount++;
                m.ValidSourceCount++;
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal)
            throw new InvalidDataException($"Treasure metric read observed {observed}/{expectedTotal} rows.");
        return m;
    }

    private static bool PositiveInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) && parsed > 0;

    private static bool NonNegativeInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) && parsed >= 0;

    private static bool NonBlank(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static bool ExactInt(JsonElement row, string name, int expected) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) && parsed == expected;

    private static bool ExactString(JsonElement row, string name, string expected) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static MapDataQueryOptions TreasureQuery(int serverId, int page = 1) => new(
        "treasure", serverId, page, MapDataQueryContract.RecoveredPageSize,
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

    private sealed class TreasureMetrics
    {
        internal int OrdinaryCount, SuppliesCount, ValidIdentityCount, ValidSourceCount;
        internal int OrdinaryRewardedCountRows, OrdinaryDiggingCountRows, OrdinaryRewardMaxRows, OrdinaryRemainingBoxesRows;
        internal int SuppliesStateRows, SuppliesRewardedCountRows;
    }
}
