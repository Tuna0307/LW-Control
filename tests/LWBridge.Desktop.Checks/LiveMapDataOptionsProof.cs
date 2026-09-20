using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveMapDataOptionsProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-map-options");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot,
            "map-options-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService(
            "map-data-options-proof",
            gameRoot);
        using var operationCts =
            new CancellationTokenSource(TimeSpan.FromMinutes(6));
        string? instanceId = null;
        Exception? operationError = null;

        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start",
                empty.RootElement,
                operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson =
                JsonSerializer.SerializeToElement(
                    startInstance,
                    JsonOptions.Default);
            instanceId =
                instanceJson.GetProperty("instanceId").GetString();

            int serverId;
            string runId;
            double scanWallSeconds;
            MapDataOptionsLiveVerifier.Metrics persistedMetrics;
            using (var store = new MapDataStore(databasePath))
            {
                var service =
                    new ManualMapScanCommandService(lifecycle, store);
                var backend = new LWBridgeBackend(
                    new LocalConfigStore(persistent: false),
                    mapData: store,
                    mapScanStatusProvider: service.CreateStatus);
                try
                {
                    JsonElement scanPayload =
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                profileId = "map-data-options-proof",
                                selectedTypes =
                                    MapScanContract.RecoveredDefaultTypes,
                            },
                            JsonOptions.Default);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    object? start = await service.InvokeAsync(
                        "map_scan_start",
                        scanPayload,
                        operationCts.Token).ConfigureAwait(false);
                    JsonElement status =
                        JsonSerializer.SerializeToElement(
                            start,
                            JsonOptions.Default);
                    serverId =
                        status.GetProperty("serverId").GetInt32();
                    runId =
                        status.GetProperty("scanRunId").GetString()
                        ?? string.Empty;
                    string[] selectedTypes =
                        status.GetProperty("selectedTypes")
                            .EnumerateArray()
                            .Select(item =>
                                item.GetString() ?? string.Empty)
                            .ToArray();
                    if (serverId <= 0 ||
                        string.IsNullOrWhiteSpace(runId) ||
                        status.GetProperty("totalBlocks").GetInt32() !=
                            2500 ||
                        status.GetProperty("scanMode").GetString() !=
                            "fast" ||
                        status.GetProperty("concurrency").GetInt32() !=
                            20 ||
                        status.GetProperty("scanStrategy").GetString() !=
                            MapScanStrategyPlanner.FastFullWorldStrategy ||
                        !selectedTypes.SequenceEqual(
                            MapScanContract.RecoveredDefaultTypes))
                    {
                        throw new InvalidDataException(
                            "Map options proof did not start with the expected automatic all-eight strategy.");
                    }

                    int lastReportedBlocks = 0;
                    DateTimeOffset deadline =
                        DateTimeOffset.UtcNow.AddMinutes(4);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        operationCts.Token.ThrowIfCancellationRequested();
                        status = JsonSerializer.SerializeToElement(
                            service.CreateStatus(),
                            JsonOptions.Default);
                        string phase =
                            status.GetProperty("phase").GetString()
                            ?? string.Empty;
                        int readBlocks =
                            status.GetProperty("readBlocks").GetInt32();
                        if (readBlocks >= lastReportedBlocks + 500)
                        {
                            lastReportedBlocks =
                                (readBlocks / 500) * 500;
                            Console.Error.WriteLine(
                                $"MAP_OPTIONS_PROGRESS blocks={readBlocks}/2500 phase={phase}");
                        }

                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException(
                                "Map options proof scan failed: " +
                                (status.TryGetProperty(
                                    "lastError",
                                    out JsonElement last)
                                    ? last.GetString()
                                    : "unknown"));
                        await Task.Delay(
                            100,
                            operationCts.Token).ConfigureAwait(false);
                    }
                    stopwatch.Stop();
                    scanWallSeconds =
                        stopwatch.Elapsed.TotalSeconds;

                    status = JsonSerializer.SerializeToElement(
                        service.CreateStatus(),
                        JsonOptions.Default);
                    if (status.GetProperty("phase").GetString() !=
                            "completed" ||
                        status.GetProperty("isReading").GetBoolean() ||
                        status.GetProperty("readBlocks").GetInt32() !=
                            2500 ||
                        status.GetProperty("failedBlocks").GetInt32() !=
                            0 ||
                        status.GetProperty("unreadBlocks").GetInt32() !=
                            0)
                    {
                        throw new InvalidDataException(
                            "Map options proof scan did not complete all 2500 blocks.");
                    }

                    long before =
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    JsonElement persistedOptions =
                        await ReadPublicOptionsAsync(
                            backend,
                            serverId,
                            operationCts.Token).ConfigureAwait(false);
                    long after =
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    persistedMetrics =
                        MapDataOptionsLiveVerifier.ValidatePersisted(
                            persistedOptions,
                            store,
                            serverId,
                            runId,
                            before,
                            after);
                    if (persistedMetrics.PositiveKindCount < 4)
                        throw new InvalidDataException(
                            "Map options proof did not publish enough positive kinds for broad option acceptance.");
                }
                finally
                {
                    service.Close();
                }
            }

            MapDataOptionsLiveVerifier.Metrics reopenedMetrics;
            using (var reopened = new MapDataStore(databasePath))
            {
                var reopenedBackend = new LWBridgeBackend(
                    new LocalConfigStore(persistent: false),
                    mapData: reopened);
                long before =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                JsonElement reopenedOptions =
                    await ReadPublicOptionsAsync(
                        reopenedBackend,
                        serverId,
                        operationCts.Token).ConfigureAwait(false);
                long after =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                reopenedMetrics =
                    MapDataOptionsLiveVerifier.ValidatePersisted(
                        reopenedOptions,
                        reopened,
                        serverId,
                        runId,
                        before,
                        after);
            }

            if (reopenedMetrics != persistedMetrics)
                throw new InvalidDataException(
                    "Persisted map_data_options metrics changed after database reopen.");

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "public_map_data_options_live",
                totalBlocks = 2500,
                scanMode = "fast",
                concurrency = 20,
                scanWallSeconds,
                serverId,
                persistedMetrics.TotalRows,
                persistedMetrics.PositiveKindCount,
                persistedMetrics.AllianceOptionCount,
                persistedMetrics.NoAllianceCount,
                persistedMetrics.ResourceNameOptionCount,
                persistedMetrics.MonsterNameOptionCount,
                persistedMetrics.ZombieBossNameOptionCount,
                persistedMetrics.DispatchLevelCount,
                persistedMetrics.MonsterLevelCount,
                persistedMetrics.TreasureTypeOptionCount,
                persistedMetrics.TruckRewardItemCount,
                persistedMetrics.RailwayRewardItemCount,
                reopenedExactMetrics = true,
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            instanceId ??=
                lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts =
                    new CancellationTokenSource(
                        TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload =
                    JsonDocument.Parse(
                        JsonSerializer.Serialize(
                            new { instanceId },
                            JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop",
                        stopPayload.RootElement,
                        stopCts.Token).ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine(
                        "LIVE_MAP_OPTIONS_STOP_FAILED: " +
                        stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static async Task<JsonElement> ReadPublicOptionsAsync(
        LWBridgeBackend backend,
        int serverId,
        CancellationToken cancellationToken)
    {
        JsonElement payload =
            JsonSerializer.SerializeToElement(
                new
                {
                    profileId = backend.ProfileId,
                    serverId,
                },
                JsonOptions.Default);
        object? result = await backend.InvokeAsync(
            "map_data_options",
            payload,
            cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static void TryDelete(string path)
    {
        foreach (string candidate in
            new[] { path, path + "-wal", path + "-shm" })
        {
            try
            {
                if (File.Exists(candidate))
                    File.Delete(candidate);
            }
            catch
            {
            }
        }
    }
}
