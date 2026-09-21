using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveAutoNativeFailureContinuationProof
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
            proofRoot, "auto-native-failure-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService(
            "auto-native-failure-continuation-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
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
                int failingTargetServerId = ResolveFailingTarget(homeServerId);
                string? failureCode = null;
                string? failureMessage = null;

                try
                {
                    using var jumpCts = CancellationTokenSource.CreateLinkedTokenSource(operationCts.Token);
                    jumpCts.CancelAfter(TimeSpan.FromSeconds(45));
                    JsonElement failPayload = JsonSerializer.SerializeToElement(
                        new { serverId = failingTargetServerId }, JsonOptions.Default);
                    object? unexpected = await service.InvokeAsync(
                        "server_jump", failPayload, jumpCts.Token).ConfigureAwait(false);
                    JsonElement unexpectedJson =
                        JsonSerializer.SerializeToElement(unexpected, JsonOptions.Default);

                    if (unexpectedJson.TryGetProperty("changed", out JsonElement changed) &&
                        changed.GetBoolean())
                    {
                        JsonElement returnPayload = JsonSerializer.SerializeToElement(
                            new { serverId = homeServerId }, JsonOptions.Default);
                        await service.InvokeAsync(
                            "server_jump", returnPayload, jumpCts.Token).ConfigureAwait(false);
                    }

                    throw new InvalidDataException(
                        $"Expected native server_jump precheck failure for target {failingTargetServerId}, but travel was accepted.");
                }
                catch (BridgeCommandException error) when (
                    error.Code == "SERVER_JUMP_FAILED" &&
                    error.Message == "server_jump_precheck_failed")
                {
                    failureCode = error.Code;
                    failureMessage = error.Message;
                }

                CurrentClientMapContext afterFailure =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                if (afterFailure.ServerId != homeServerId)
                    throw new InvalidDataException(
                        $"Native jump failure changed server context {homeServerId} -> {afterFailure.ServerId}.");

                JsonElement homePayload = JsonSerializer.SerializeToElement(
                    new { serverId = homeServerId }, JsonOptions.Default);
                object? sameResult = await service.InvokeAsync(
                    "server_jump", homePayload, operationCts.Token).ConfigureAwait(false);
                JsonElement same = JsonSerializer.SerializeToElement(sameResult, JsonOptions.Default);
                if (same.GetProperty("previousServerId").GetInt32() != homeServerId ||
                    same.GetProperty("changed").GetBoolean())
                    throw new InvalidDataException(
                        "Post-failure same-server server_jump was not proven as a no-op.");

                JsonElement scanPayload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "auto-native-failure-continuation-proof",
                    selectedTypes = new[] { "zombie_boss" },
                    resume = false,
                }, JsonOptions.Default);

                Stopwatch stopwatch = Stopwatch.StartNew();
                object? startScan = await service.InvokeAsync(
                    "map_scan_start", scanPayload, operationCts.Token).ConfigureAwait(false);
                JsonElement status = JsonSerializer.SerializeToElement(startScan, JsonOptions.Default);
                if (status.GetProperty("serverId").GetInt32() != homeServerId ||
                    status.GetProperty("totalBlocks").GetInt32() != 2500 ||
                    status.GetProperty("scanMode").GetString() != "fast" ||
                    status.GetProperty("concurrency").GetInt32() != 20 ||
                    status.GetProperty("scanStrategy").GetString() !=
                        MapScanStrategyPlanner.FastZombieBossStrategy)
                    throw new InvalidDataException(
                        "Post-failure valid target did not start with the expected backend-selected Zombie Boss strategy.");

                DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(3);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    operationCts.Token.ThrowIfCancellationRequested();
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                    if (phase == "completed") break;
                    if (phase == "error")
                        throw new InvalidDataException(
                            "Post-failure Zombie Boss scan failed: " +
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
                        "Post-native-failure Zombie Boss continuation did not complete all 2500 logical blocks.");

                int zombieBossCount = store.SearchIndexed(ZombieBossQuery(homeServerId)).Total;

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "auto_native_travel_failure_continuation",
                    homeServerId,
                    failingTargetServerId,
                    nativeFailureCode = failureCode,
                    nativeFailureMessage = failureMessage,
                    postFailureServerId = afterFailure.ServerId,
                    postFailureSameServerNoOp = true,
                    nextValidTargetServerId = homeServerId,
                    scanMode = status.GetProperty("scanMode").GetString(),
                    scanStrategy = status.GetProperty("scanStrategy").GetString(),
                    concurrency = status.GetProperty("concurrency").GetInt32(),
                    totalBlocks = status.GetProperty("totalBlocks").GetInt32(),
                    readBlocks = status.GetProperty("readBlocks").GetInt32(),
                    failedBlocks = status.GetProperty("failedBlocks").GetInt32(),
                    unreadBlocks = status.GetProperty("unreadBlocks").GetInt32(),
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
                    Console.Error.WriteLine(
                        "LIVE_AUTO_NATIVE_FAILURE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }

            TryDelete(databasePath);
        }
    }

    private static int ResolveFailingTarget(int homeServerId)
    {
        string? configured =
            Environment.GetEnvironmentVariable("LWBRIDGE_AUTO_FAILURE_TARGET_SERVER");
        if (int.TryParse(configured, out int parsed) &&
            parsed is >= 1 and <= 99999 &&
            parsed != homeServerId)
            return parsed;
        return 2148;
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
            try
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
            catch
            {
            }
        }
    }
}
