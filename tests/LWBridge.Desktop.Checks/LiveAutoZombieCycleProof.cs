using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveAutoZombieCycleProof
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
        string databasePath = Path.Combine(
            proofRoot, "auto-zombie-cycle-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService("auto-zombie-cycle-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string? instanceId = null;
        Exception? operationError = null;

        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson = JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();
            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initial =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                int serverId = initial.ServerId;

                JsonElement jumpPayload = JsonSerializer.SerializeToElement(
                    new { serverId }, JsonOptions.Default);
                object? jumpResult = await service.InvokeAsync(
                    "server_jump", jumpPayload, operationCts.Token).ConfigureAwait(false);
                JsonElement jump = JsonSerializer.SerializeToElement(jumpResult, JsonOptions.Default);
                if (jump.GetProperty("previousServerId").GetInt32() != serverId ||
                    jump.GetProperty("changed").GetBoolean())
                    throw new InvalidDataException(
                        "Auto-cycle same-server server_jump was not proven as a no-op.");

                JsonElement scanPayload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "auto-zombie-cycle-proof",
                    selectedTypes = new[] { "zombie_boss" },
                }, JsonOptions.Default);

                Stopwatch stopwatch = Stopwatch.StartNew();
                object? startScan = await service.InvokeAsync(
                    "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false);
                JsonElement status = JsonSerializer.SerializeToElement(startScan, JsonOptions.Default);
                if (status.GetProperty("serverId").GetInt32() != serverId ||
                    status.GetProperty("totalBlocks").GetInt32() != 2500 ||
                    status.GetProperty("scanMode").GetString() != "fast" ||
                    status.GetProperty("concurrency").GetInt32() != 20 ||
                    status.GetProperty("scanStrategy").GetString() != MapScanStrategyPlanner.FastZombieBossStrategy)
                    throw new InvalidDataException(
                        "Auto-cycle Zombie Boss Start did not expose the expected backend-selected fast strategy and current-server geometry.");
                DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(3);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    operationCts.Token.ThrowIfCancellationRequested();
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                    if (phase == "completed") break;
                    if (phase == "error")
                        throw new InvalidDataException(
                            "Auto-cycle Zombie Boss scan failed: " +
                            (status.TryGetProperty("lastError", out JsonElement last)
                                ? last.GetString()
                                : "unknown"));
                    await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                }
                stopwatch.Stop();

                status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                if (status.GetProperty("phase").GetString() != "completed" ||
                    status.GetProperty("isReading").GetBoolean() ||
                    status.GetProperty("readBlocks").GetInt32() != 2500 ||
                    status.GetProperty("failedBlocks").GetInt32() != 0 ||
                    status.GetProperty("unreadBlocks").GetInt32() != 0)
                    throw new InvalidDataException(
                        "Auto-cycle Zombie Boss scan did not complete all 2500 logical blocks.");

                int zombieBossCount = store.SearchIndexed(ZombieBossQuery(serverId)).Total;

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "auto_scan_backend_cycle_zombie_boss",
                    serverId,
                    serverJumpNoOpProven = true,
                    callerScanModeProvided = false,
                    scanMode = status.GetProperty("scanMode").GetString(),
                    scanStrategy = status.GetProperty("scanStrategy").GetString(),
                    concurrency = status.GetProperty("concurrency").GetInt32(),
                    totalBlocks = 2500,
                    zombieBossCount,
                    scanWallSeconds = stopwatch.Elapsed.TotalSeconds,
                }, JsonOptions.Default));
            }
            finally
            {
                service.Close();
            }
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
                    Console.Error.WriteLine("LIVE_AUTO_ZOMBIE_CYCLE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static MapDataQueryOptions ZombieBossQuery(int serverId) => new(
        "zombie_boss", serverId, 1, MapDataQueryContract.RecoveredPageSize,
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
