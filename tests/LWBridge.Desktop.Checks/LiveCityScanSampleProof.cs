using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveCityScanSampleProof
{
    private const int TargetCompletedBlocks = 12;

    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService(
            "current-city-sample-proof",
            gameRoot);
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
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token)
                .ConfigureAwait(false);
            // Startup readiness policy for this bounded live sample: allow the just-entered
            // world scene to finish its initial population before block 0 begins.
            await Task.Delay(TimeSpan.FromSeconds(3), operationCts.Token).ConfigureAwait(false);
            using MapDataStore store = MapDataStore.CreateInMemory();
            var service = new ManualMapScanCommandService(
                store,
                source.GetCurrentContextAsync,
                source);
            try
            {
                JsonElement payload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "current-city-sample-proof",
                    scanMode = "normal",
                    selectedTypes = new[] { "city" },
                }, JsonOptions.Default);

                Stopwatch stopwatch = Stopwatch.StartNew();
                object? start = await service.InvokeAsync(
                    "map_scan_start", payload, operationCts.Token).ConfigureAwait(false);
                JsonElement startStatus = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
                int totalBlocks = startStatus.GetProperty("totalBlocks").GetInt32();
                string runId = startStatus.GetProperty("scanRunId").GetString() ?? string.Empty;
                if (totalBlocks != 2500 || startStatus.GetProperty("concurrency").GetInt32() != 8 ||
                    string.IsNullOrWhiteSpace(runId))
                    throw new InvalidDataException("Bounded City sample did not start with expected live geometry/mode.");
                JsonElement activeStatus = default;
                bool sampleFailed = false;
                DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(3);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    operationCts.Token.ThrowIfCancellationRequested();
                    activeStatus = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    string phase = activeStatus.GetProperty("phase").GetString() ?? string.Empty;
                    int failed = activeStatus.GetProperty("failedBlocks").GetInt32();
                    if (phase == "error" || failed > 0)
                    {
                        sampleFailed = true;
                        break;
                    }
                    if (activeStatus.GetProperty("readBlocks").GetInt32() >= TargetCompletedBlocks)
                        break;
                    await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                }

                int completed = activeStatus.GetProperty("readBlocks").GetInt32();
                int failedBlocks = activeStatus.GetProperty("failedBlocks").GetInt32();
                MapScanBlockCheckpoint[] failedCheckpoints = store.ReadScanBlockCheckpointsForTest(runId)
                    .Where(checkpoint => checkpoint.Status == "failed")
                    .ToArray();
                if (!sampleFailed && completed < TargetCompletedBlocks)
                    throw new TimeoutException(
                        $"Bounded City sample completed only {completed} blocks before its deadline.");
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                double measuredBlocksPerSecond = completed / Math.Max(elapsedSeconds, 0.001);
                double? estimatedFullRunSeconds = !sampleFailed && completed > 0
                    ? totalBlocks / measuredBlocksPerSecond
                    : null;
                _ = await service.InvokeAsync(
                    "map_scan_stop", payload, operationCts.Token).ConfigureAwait(false);
                JsonElement finalStatus = default;
                DateTimeOffset stopDeadline = DateTimeOffset.UtcNow.AddSeconds(20);
                while (DateTimeOffset.UtcNow < stopDeadline)
                {
                    finalStatus = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    bool reading = finalStatus.GetProperty("isReading").GetBoolean();
                    string phase = finalStatus.GetProperty("phase").GetString() ?? string.Empty;
                    if (!reading && phase is "idle" or "completed") break;
                    await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                }
                if (finalStatus.GetProperty("isReading").GetBoolean())
                    throw new TimeoutException("Bounded City sample Stop did not release scan ownership.");

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    diagnosticOk = true,
                    scanResult = sampleFailed ? "block_failure" : "target_reached",
                    proof = "production_manual_city_scan_bounded_sample",
                    serverId = context.ServerId,
                    worldId = context.WorldId,
                    tileWidth = context.TileWidth,
                    tileHeight = context.TileHeight,
                    totalBlocks,
                    targetCompletedBlocks = TargetCompletedBlocks,
                    completedBlocks = completed,
                    failedBlocks,
                    failureBlocks = failedCheckpoints.Select(checkpoint => new
                    {
                        checkpoint.BlockIndex,
                        checkpoint.Attempts,
                        checkpoint.Error,
                    }).ToArray(),
                    elapsedSeconds,
                    measuredBlocksPerSecond,
                    estimatedFullRunSeconds,
                    estimatedFullRunMinutes = estimatedFullRunSeconds.HasValue
                        ? estimatedFullRunSeconds.Value / 60.0
                        : (double?)null,
                    finalPhase = finalStatus.GetProperty("phase").GetString(),
                    stopped = !finalStatus.GetProperty("isReading").GetBoolean(),
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
                        "profile_instance_stop",
                        stopPayload.RootElement,
                        stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_CITY_SAMPLE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

}
