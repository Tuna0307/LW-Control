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

                publishedMonsterCount = store.SearchIndexed(MonsterQuery(serverId)).Total;
                if (publishedMonsterCount <= 0)
                    throw new InvalidDataException("Ordinary Manual Monster scan published no Monster records.");
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
                reopenedMonsterCount = reopened.SearchIndexed(MonsterQuery(serverId)).Total;
            if (reopenedMonsterCount != publishedMonsterCount)
                throw new InvalidDataException("Ordinary Manual Monster count changed after database reopen.");
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

    private static MapDataQueryOptions MonsterQuery(int serverId) => new(
        "monster", serverId, 1, MapDataQueryContract.RecoveredPageSize,
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
