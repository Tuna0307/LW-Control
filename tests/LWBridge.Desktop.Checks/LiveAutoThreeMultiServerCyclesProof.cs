using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveAutoThreeMultiServerCyclesProof
{
    private sealed record ScanResult(
        int Cycle,
        int ServerId,
        string ScanRunId,
        string ScanMode,
        string ScanStrategy,
        int Concurrency,
        int TotalBlocks,
        int ReadBlocks,
        int FailedBlocks,
        int UnreadBlocks,
        int ZombieBossCount,
        double WallSeconds);

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
            proofRoot, "auto-three-cycle-" + Guid.NewGuid().ToString("N") + ".db");

        int requestedCycles = ResolvePositiveInt("LWBRIDGE_AUTO_CYCLE_COUNT", 3, 3, 10);
        int? configuredTarget = ResolveOptionalServer("LWBRIDGE_AUTO_CYCLE_TARGET");

        using var lifecycle = new OverviewLifecycleService(
            "auto-three-multiserver-cycle-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(12));
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
                int homeServerId = initial.ServerId;
                int targetServerId = configuredTarget ?? (homeServerId == 2213 ? 2212 : 2213);
                if (targetServerId == homeServerId)
                    throw new InvalidDataException("Three-cycle proof target must differ from the home server.");

                var scans = new List<ScanResult>();
                var cycleReturns = new List<object>();
                Stopwatch total = Stopwatch.StartNew();

                for (int cycle = 1; cycle <= requestedCycles; cycle++)
                {
                    scans.Add(await ScanServerAsync(
                        cycle, homeServerId, service, source, store, operationCts.Token).ConfigureAwait(false));

                    await JumpAndConfirmAsync(
                        targetServerId, service, source, operationCts.Token).ConfigureAwait(false);
                    scans.Add(await ScanServerAsync(
                        cycle, targetServerId, service, source, store, operationCts.Token).ConfigureAwait(false));

                    object returnResult = await JumpAndConfirmAsync(
                        homeServerId, service, source, operationCts.Token).ConfigureAwait(false);
                    cycleReturns.Add(new
                    {
                        cycle,
                        fromServerId = targetServerId,
                        serverId = homeServerId,
                        changed = JsonSerializer.SerializeToElement(returnResult, JsonOptions.Default)
                            .GetProperty("changed").GetBoolean(),
                    });
                }

                total.Stop();

                CurrentClientMapContext finalContext =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                if (finalContext.ServerId != homeServerId)
                    throw new InvalidDataException(
                        $"Three-cycle proof ended on server {finalContext.ServerId}, expected home {homeServerId}.");

                if (scans.Count != requestedCycles * 2 ||
                    scans.Any(scan => scan.TotalBlocks != 2500 ||
                                      scan.ReadBlocks != 2500 ||
                                      scan.FailedBlocks != 0 ||
                                      scan.UnreadBlocks != 0 ||
                                      scan.ScanMode != "fast" ||
                                      scan.ScanStrategy != MapScanStrategyPlanner.FastZombieBossStrategy ||
                                      scan.Concurrency != 20))
                    throw new InvalidDataException(
                        "One or more three-cycle scan legs failed exact Fast Zombie Boss coverage.");

                if (scans.Select(scan => scan.ScanRunId).Distinct(StringComparer.Ordinal).Count() != scans.Count)
                    throw new InvalidDataException("Three-cycle proof did not produce a unique run identity per scan leg.");

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "auto_scan_three_full_multiserver_cycles",
                    cycleCount = requestedCycles,
                    homeServerId,
                    targetServerId,
                    configuredServerOrder = new[] { homeServerId, targetServerId },
                    totalScanLegs = scans.Count,
                    allLegsCompleted = true,
                    allLegsReadBlocks = 2500,
                    allLegsFailedBlocks = 0,
                    allLegsUnreadBlocks = 0,
                    returnToOriginalAfterEveryCycle = true,
                    finalServerId = finalContext.ServerId,
                    callerScanModeProvided = false,
                    scans,
                    cycleReturns,
                    totalWallSeconds = total.Elapsed.TotalSeconds,
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
                    Console.Error.WriteLine("LIVE_AUTO_THREE_CYCLE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }

            TryDelete(databasePath);
        }
    }

    private static async Task<ScanResult> ScanServerAsync(
        int cycle,
        int expectedServerId,
        ManualMapScanCommandService service,
        CurrentClientMapBlockSource source,
        MapDataStore store,
        CancellationToken cancellationToken)
    {
        CurrentClientMapContext before =
            await source.GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
        if (before.ServerId != expectedServerId)
            throw new InvalidDataException(
                $"Cycle {cycle} scan expected server {expectedServerId}, found {before.ServerId}.");

        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            profileId = "auto-three-multiserver-cycle-proof",
            selectedTypes = new[] { "zombie_boss" },
        }, JsonOptions.Default);

        Stopwatch wall = Stopwatch.StartNew();
        object? start = await service.InvokeAsync(
            "map_scan_start", payload, cancellationToken).ConfigureAwait(false);
        JsonElement status = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
        string scanRunId = status.GetProperty("scanRunId").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(scanRunId))
            throw new InvalidDataException($"Cycle {cycle} server {expectedServerId} did not expose scanRunId.");

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
            string phase = status.GetProperty("phase").GetString() ?? string.Empty;
            if (phase == "completed") break;
            if (phase == "error")
                throw new InvalidDataException(
                    $"Cycle {cycle} server {expectedServerId} failed: " +
                    (status.TryGetProperty("lastError", out JsonElement last)
                        ? last.GetString()
                        : "unknown"));
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        wall.Stop();

        status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
        string phaseFinal = status.GetProperty("phase").GetString() ?? string.Empty;
        int totalBlocks = status.GetProperty("totalBlocks").GetInt32();
        int readBlocks = status.GetProperty("readBlocks").GetInt32();
        int failedBlocks = status.GetProperty("failedBlocks").GetInt32();
        int unreadBlocks = status.GetProperty("unreadBlocks").GetInt32();
        string scanMode = status.GetProperty("scanMode").GetString() ?? string.Empty;
        string scanStrategy = status.GetProperty("scanStrategy").GetString() ?? string.Empty;
        int concurrency = status.GetProperty("concurrency").GetInt32();

        if (phaseFinal != "completed" ||
            status.GetProperty("isReading").GetBoolean() ||
            status.GetProperty("serverId").GetInt32() != expectedServerId ||
            totalBlocks != 2500 || readBlocks != 2500 ||
            failedBlocks != 0 || unreadBlocks != 0 ||
            scanMode != "fast" ||
            scanStrategy != MapScanStrategyPlanner.FastZombieBossStrategy ||
            concurrency != 20)
            throw new InvalidDataException(
                $"Cycle {cycle} server {expectedServerId} did not complete exact backend-selected Fast coverage.");

        int count = store.SearchIndexed(ZombieBossQuery(expectedServerId)).Total;
        return new ScanResult(
            cycle,
            expectedServerId,
            scanRunId,
            scanMode,
            scanStrategy,
            concurrency,
            totalBlocks,
            readBlocks,
            failedBlocks,
            unreadBlocks,
            count,
            wall.Elapsed.TotalSeconds);
    }

    private static async Task<object> JumpAndConfirmAsync(
        int serverId,
        ManualMapScanCommandService service,
        CurrentClientMapBlockSource source,
        CancellationToken cancellationToken)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new { serverId }, JsonOptions.Default);
        object? result = await service.InvokeAsync(
            "server_jump", payload, cancellationToken).ConfigureAwait(false);
        JsonElement json = JsonSerializer.SerializeToElement(result, JsonOptions.Default);
        if (!json.TryGetProperty("previousServerId", out _) ||
            !json.TryGetProperty("changed", out _))
            throw new InvalidDataException(
                "server_jump did not return the recovered {previousServerId,changed} contract.");

        CurrentClientMapContext context =
            await source.GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
        if (context.ServerId != serverId)
            throw new InvalidDataException(
                $"authoritative context after server_jump is {context.ServerId}, expected {serverId}.");
        return result!;
    }

    private static int ResolvePositiveInt(string name, int fallback, int min, int max)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (!int.TryParse(value, out int parsed)) return fallback;
        return Math.Clamp(parsed, min, max);
    }

    private static int? ResolveOptionalServer(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (!int.TryParse(value, out int parsed)) return null;
        if (parsed is < 1 or > 99999)
            throw new InvalidDataException($"{name} must be in 1..99999.");
        return parsed;
    }

    private static MapDataQueryOptions ZombieBossQuery(int serverId) => new(
        "zombie_boss", serverId, 1, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, null, null, null, false, false, false,
        null, null, Array.Empty<string>());

    private static void TryDelete(string path)
    {
        foreach (string candidate in new[]
        {
            path, path + "-wal", path + "-shm", path + ".scan-owner.lock"
        })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { }
        }
    }
}
