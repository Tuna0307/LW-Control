using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveBridgeLossDuringScanProof
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
            proofRoot, "bridge-loss-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService("bridge-loss-scan-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string? instanceId = null;
        bool stopInvoked = false;
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
            try
            {
                var source = new CurrentClientMapBlockSource(lifecycle);
                CurrentClientMapContext context =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                if (context.ServerId <= 0)
                    throw new InvalidDataException("Live B07 proof did not resolve an authoritative server.");

                var baseline = new MapStoredRecord(
                    "monster", context.ServerId, "bridge-loss-baseline", 1, "bridge-loss-baseline-uuid",
                    "Baseline", null, 100, null, null, null, null,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    $"{{\"serverId\":{context.ServerId},\"name\":\"Baseline\"}}");
                store.UpsertRecord(baseline);

                JsonElement payload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "bridge-loss-scan-proof",
                    selectedTypes = new[] { "monster" },
                }, JsonOptions.Default);

                Stopwatch wall = Stopwatch.StartNew();
                object? started = await service.InvokeAsync(
                    "map_scan_start", payload, operationCts.Token).ConfigureAwait(false);
                JsonElement startStatus = JsonSerializer.SerializeToElement(started, JsonOptions.Default);
                string runId = startStatus.GetProperty("scanRunId").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(runId))
                    throw new InvalidDataException("Live B07 proof Start omitted scanRunId.");

                DateTimeOffset scanningDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
                JsonElement status = startStatus;
                while (DateTimeOffset.UtcNow < scanningDeadline)
                {
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    if (status.GetProperty("isReading").GetBoolean() &&
                        status.GetProperty("phase").GetString() == "scanning")
                        break;
                    await Task.Delay(50, operationCts.Token).ConfigureAwait(false);
                }
                if (!status.GetProperty("isReading").GetBoolean() ||
                    status.GetProperty("phase").GetString() != "scanning")
                    throw new InvalidDataException("Live B07 proof never reached active scanning phase.");

                int readBlocksAtLoss = status.GetProperty("readBlocks").GetInt32();
                int totalBlocksAtLoss = status.GetProperty("totalBlocks").GetInt32();
                int failedBlocksAtLoss = status.GetProperty("failedBlocks").GetInt32();

                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                await lifecycle.InvokeAsync(
                    "profile_instance_stop", stopPayload.RootElement, operationCts.Token).ConfigureAwait(false);
                stopInvoked = true;

                DateTimeOffset terminalDeadline = DateTimeOffset.UtcNow.AddSeconds(20);
                while (DateTimeOffset.UtcNow < terminalDeadline)
                {
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    if (!status.GetProperty("isReading").GetBoolean() &&
                        status.GetProperty("phase").GetString() == "error")
                        break;
                    await Task.Delay(50, operationCts.Token).ConfigureAwait(false);
                }
                wall.Stop();

                if (status.GetProperty("isReading").GetBoolean() ||
                    status.GetProperty("phase").GetString() != "error")
                    throw new InvalidDataException(
                        "Definitive owned bridge loss did not terminate the active scan as an error.");

                string lastError = status.TryGetProperty("lastError", out JsonElement last) &&
                                   last.ValueKind == JsonValueKind.String
                    ? last.GetString() ?? string.Empty
                    : string.Empty;
                if (!lastError.Contains("game connection unavailable", StringComparison.OrdinalIgnoreCase) &&
                    !lastError.Contains("owned game session changed", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Definitive bridge loss exposed an unexpected terminal error: " + lastError);

                MapOptionAggregates latest = store.ReadOptionAggregatesAt(
                    MapDataStore.SelectOptionSource(context.ServerId, false, context.ServerId, null),
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (latest.ScanProgress is not { Status: "failed" } progress ||
                    progress.Id != runId)
                    throw new InvalidDataException(
                        "Definitive bridge loss did not durably persist the active run as failed.");

                if (store.GetRecord("monster", context.ServerId, baseline.RecordKey) is null)
                    throw new InvalidDataException(
                        "Definitive bridge loss replaced the previously published Monster dataset.");

                int checkpoints = store.ReadScanBlockCheckpointsForTest(runId).Count;
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "bridge_loss_during_active_map_scan",
                    serverId = context.ServerId,
                    runId,
                    scanMode = status.GetProperty("scanMode").GetString(),
                    scanStrategy = status.GetProperty("scanStrategy").GetString(),
                    totalBlocksAtLoss,
                    readBlocksAtLoss,
                    failedBlocksAtLoss,
                    durableCheckpointCount = checkpoints,
                    finalPhase = status.GetProperty("phase").GetString(),
                    finalIsReading = status.GetProperty("isReading").GetBoolean(),
                    finalLastError = lastError,
                    durableRunStatus = latest.ScanProgress.Status,
                    priorPublishedDatasetPreserved = true,
                    resumeAvailable = status.GetProperty("resumeAvailable").GetBoolean(),
                    wallSeconds = wall.Elapsed.TotalSeconds,
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
            if (!stopInvoked && !string.IsNullOrWhiteSpace(instanceId))
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
                    Console.Error.WriteLine("LIVE_BRIDGE_LOSS_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }

            foreach (string candidate in new[]
            {
                databasePath,
                databasePath + "-wal",
                databasePath + "-shm",
                databasePath + ".scan-owner.lock",
            })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); }
                catch { }
            }
        }
    }
}
