using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveTrainListNoJumpProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-train-no-jump");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "train-no-jump-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService("train-no-jump-proof", gameRoot);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        string? instanceId = null;
        Exception? operationError = null;
        int originalLiveServerId = 0;
        int targetServerId = 0;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(
                await lifecycle.InvokeAsync(
                    "profile_instance_start", empty.RootElement, cts.Token).ConfigureAwait(false),
                JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();

            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initial =
                    await source.GetCurrentContextAsync(cts.Token).ConfigureAwait(false);
                originalLiveServerId = initial.ServerId;
                if (originalLiveServerId <= 0)
                    throw new InvalidDataException("No live server was available for the Train-list no-jump proof.");

                Stopwatch coverageWatch = Stopwatch.StartNew();
                JsonElement coverage = JsonSerializer.SerializeToElement(
                    await service.InvokeAsync(
                        "map_train_list_coverage",
                        empty.RootElement.Clone(),
                        cts.Token).ConfigureAwait(false),
                    JsonOptions.Default);
                coverageWatch.Stop();

                int coverageLiveServerId = coverage.GetProperty("liveServerId").GetInt32();
                if (coverageLiveServerId != originalLiveServerId)
                    throw new InvalidDataException(
                        $"Coverage live server changed from {originalLiveServerId} to {coverageLiveServerId}.");
                int[] matchServerIds = coverage.GetProperty("matchServerIds")
                    .EnumerateArray().Select(value => value.GetInt32()).ToArray();
                targetServerId = matchServerIds.FirstOrDefault(serverId => serverId != originalLiveServerId);
                if (targetServerId <= 0)
                    throw new InvalidDataException(
                        $"Official Train-list coverage from server {originalLiveServerId} exposed no remote match server.");

                int physicalBeforeScan = lifecycle.GetLiveServerId() ?? 0;
                if (physicalBeforeScan != originalLiveServerId)
                    throw new InvalidDataException("Physical server changed after coverage refresh.");

                JsonElement scanPayload = JsonSerializer.SerializeToElement(new
                {
                    selectedTypes = new[] { "truck", "railway" },
                    targetServerId,
                }, JsonOptions.Default);
                Stopwatch scanWatch = Stopwatch.StartNew();
                JsonElement status = JsonSerializer.SerializeToElement(
                    await service.InvokeAsync(
                        "map_scan_start", scanPayload, cts.Token).ConfigureAwait(false),
                    JsonOptions.Default);
                string runId = status.GetProperty("scanRunId").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(runId))
                    throw new InvalidDataException("Remote Train-list scan did not expose a run ID.");

                DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(2);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                    if (phase == "completed") break;
                    if (phase == "error")
                        throw new InvalidDataException(
                            "Remote Train-list scan failed: " +
                            (status.TryGetProperty("lastError", out JsonElement errorValue)
                                ? errorValue.GetString()
                                : "unknown"));
                    await Task.Delay(50, cts.Token).ConfigureAwait(false);
                }
                scanWatch.Stop();
                status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);

                int physicalAfterScan = lifecycle.GetLiveServerId() ?? 0;
                if (status.GetProperty("phase").GetString() != "completed" ||
                    status.GetProperty("isReading").GetBoolean() ||
                    status.GetProperty("serverId").GetInt32() != targetServerId ||
                    status.GetProperty("liveServerId").GetInt32() != originalLiveServerId ||
                    !string.Equals(
                        status.GetProperty("serverIdSource").GetString(),
                        "remote_train_list", StringComparison.Ordinal) ||
                    status.GetProperty("totalBlocks").GetInt32() != 2500 ||
                    status.GetProperty("readBlocks").GetInt32() != 2500 ||
                    status.GetProperty("failedBlocks").GetInt32() != 0 ||
                    status.GetProperty("unreadBlocks").GetInt32() != 0)
                    throw new InvalidDataException("Remote Train-list scan did not publish a complete remote dataset status.");
                if (physicalAfterScan != originalLiveServerId)
                    throw new InvalidDataException(
                        $"No-jump scan changed physical server {originalLiveServerId} -> {physicalAfterScan}.");

                int truckCount = store.CountRecords("truck", targetServerId);
                int railwayCount = store.CountRecords("railway", targetServerId);
                int liveTruckCount = store.CountRecords("truck", originalLiveServerId);
                int liveRailwayCount = store.CountRecords("railway", originalLiveServerId);

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "current_v20_train_list_remote_no_jump",
                    originalLiveServerId,
                    targetServerId,
                    coverage = new
                    {
                        matchServerIds,
                        truckServerIds = coverage.GetProperty("truckServerIds")
                            .EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                        railwayServerIds = coverage.GetProperty("railwayServerIds")
                            .EnumerateArray().Select(value => value.GetInt32()).ToArray(),
                        wallSeconds = coverageWatch.Elapsed.TotalSeconds,
                    },
                    scan = new
                    {
                        runId,
                        wallSeconds = scanWatch.Elapsed.TotalSeconds,
                        serverId = status.GetProperty("serverId").GetInt32(),
                        liveServerId = status.GetProperty("liveServerId").GetInt32(),
                        serverIdSource = status.GetProperty("serverIdSource").GetString(),
                        totalBlocks = status.GetProperty("totalBlocks").GetInt32(),
                        readBlocks = status.GetProperty("readBlocks").GetInt32(),
                        failedBlocks = status.GetProperty("failedBlocks").GetInt32(),
                        unreadBlocks = status.GetProperty("unreadBlocks").GetInt32(),
                        truckCount,
                        railwayCount,
                    },
                    physicalServer = new
                    {
                        beforeScan = physicalBeforeScan,
                        afterScan = physicalAfterScan,
                        unchanged = physicalBeforeScan == physicalAfterScan &&
                            physicalAfterScan == originalLiveServerId,
                    },
                    publishedDatasetScope = new
                    {
                        targetTruckCount = truckCount,
                        targetRailwayCount = railwayCount,
                        originalServerTruckCount = liveTruckCount,
                        originalServerRailwayCount = liveRailwayCount,
                    },
                    safety = new
                    {
                        serverJumpInvoked = false,
                        stateChangingGameplayAction = false,
                    },
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
                catch (Exception error)
                {
                    Console.Error.WriteLine("TRAIN_NO_JUMP_STOP_FAILED: " + error.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }
}
