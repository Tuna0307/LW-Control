using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveMapSummaryProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-map-summary");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot,
            "map-summary-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService(
            "map-summary-proof",
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
            int activeMonsterCount;
            int completedMonsterCount;
            IReadOnlyDictionary<string, int> completedCounts;
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
                                profileId = "map-summary-proof",
                                selectedTypes = new[] { "monster" },
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
                    if (serverId <= 0 ||
                        string.IsNullOrWhiteSpace(runId) ||
                        status.GetProperty("totalBlocks").GetInt32() !=
                            2500 ||
                        status.GetProperty("scanMode").GetString() !=
                            "fast" ||
                        status.GetProperty("concurrency").GetInt32() !=
                            20 ||
                        status.GetProperty("scanStrategy").GetString() !=
                            MapScanStrategyPlanner
                                .FastFullWorldStrategy)
                    {
                        throw new InvalidDataException(
                            "Map summary proof did not start with the expected automatic Monster strategy.");
                    }

                    JsonElement activeSummary =
                        await ReadSummaryAsync(
                            backend,
                            operationCts.Token).ConfigureAwait(false);
                    ValidateEnvelope(activeSummary);
                    JsonElement activeState =
                        activeSummary.GetProperty("scanState");
                    if (activeSummary.GetProperty("serverId").GetInt32() !=
                            serverId ||
                        !activeState.GetProperty(
                            "isReading").GetBoolean() ||
                        !string.Equals(
                            activeState.GetProperty(
                                "phase").GetString(),
                            "scanning",
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            activeState.GetProperty(
                                "scanRunId").GetString(),
                            runId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Active map_summary did not expose the exact owned Monster run.");
                    }
                    activeMonsterCount =
                        activeSummary.GetProperty("counts")
                            .GetProperty("monster")
                            .GetInt32();

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
                                $"MAP_SUMMARY_PROGRESS blocks={readBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException(
                                "Map summary proof scan failed: " +
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
                            "Map summary proof scan did not complete all 2500 blocks.");
                    }

                    completedCounts = CountKinds(store, serverId);
                    completedMonsterCount =
                        completedCounts["monster"];
                    if (completedMonsterCount <= 0)
                        throw new InvalidDataException(
                            "Map summary proof requires a positive completed Monster population.");

                    JsonElement completedSummary =
                        await ReadSummaryAsync(
                            backend,
                            operationCts.Token).ConfigureAwait(false);
                    ValidateEnvelope(completedSummary);
                    ValidateCounts(
                        completedSummary.GetProperty("counts"),
                        completedCounts,
                        "completed");
                    JsonElement completedState =
                        completedSummary.GetProperty("scanState");
                    if (completedSummary.GetProperty(
                            "serverId").GetInt32() != serverId ||
                        completedState.GetProperty(
                            "isReading").GetBoolean() ||
                        !string.Equals(
                            completedState.GetProperty(
                                "phase").GetString(),
                            "completed",
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            completedState.GetProperty(
                                "scanRunId").GetString(),
                            runId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Completed map_summary did not preserve the terminal owned run.");
                    }
                }
                finally
                {
                    service.Close();
                }
            }

            IReadOnlyDictionary<string, int> reopenedCounts;
            using (var reopened = new MapDataStore(databasePath))
            {
                var reopenedBackend = new LWBridgeBackend(
                    new LocalConfigStore(persistent: false),
                    mapData: reopened,
                    mapScanStatusProvider: () => new
                    {
                        serverId,
                        isReading = false,
                        scanRunId = string.Empty,
                        phase = "completed",
                        serverIdSource = "reopen_proof_state",
                    });
                JsonElement reopenedSummary =
                    await ReadSummaryAsync(
                        reopenedBackend,
                        operationCts.Token).ConfigureAwait(false);
                ValidateEnvelope(reopenedSummary);
                reopenedCounts = CountKinds(reopened, serverId);
                ValidateCounts(
                    reopenedSummary.GetProperty("counts"),
                    reopenedCounts,
                    "reopened");
                JsonElement reopenedState =
                    reopenedSummary.GetProperty("scanState");
                if (reopenedSummary.GetProperty(
                        "serverId").GetInt32() != serverId ||
                    reopenedState.GetProperty(
                        "isReading").GetBoolean() ||
                    !string.Equals(
                        reopenedState.GetProperty(
                            "phase").GetString(),
                        "completed",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        reopenedState.GetProperty(
                            "serverIdSource").GetString(),
                        "reopen_proof_state",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Reopened map_summary did not preserve the supplied shared scan state.");
                }
            }

            if (!reopenedCounts.OrderBy(item => item.Key)
                    .SequenceEqual(
                        completedCounts.OrderBy(item => item.Key)))
            {
                throw new InvalidDataException(
                    "map_summary counts changed after database reopen.");
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "public_map_summary_live",
                totalBlocks = 2500,
                scanMode = "fast",
                concurrency = 20,
                scanWallSeconds,
                serverId,
                activePhase = "scanning",
                activeMonsterCount,
                completedPhase = "completed",
                completedMonsterCount,
                completedPositiveKindCount =
                    completedCounts.Count(
                        item => item.Value > 0),
                reopenedPhase = "completed",
                reopenedServerIdSource =
                    "reopen_proof_state",
                reopenedCountsExact = true,
                envelopeFields =
                    new[] { "serverId", "counts", "scanState" },
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
                        "LIVE_MAP_SUMMARY_STOP_FAILED: " +
                        stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static async Task<JsonElement> ReadSummaryAsync(
        LWBridgeBackend backend,
        CancellationToken cancellationToken)
    {
        JsonElement payload =
            JsonSerializer.SerializeToElement(
                new { profileId = backend.ProfileId },
                JsonOptions.Default);
        object? result = await backend.InvokeAsync(
            "map_summary",
            payload,
            cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(
            result,
            JsonOptions.Default);
    }

    private static void ValidateEnvelope(JsonElement summary)
    {
        string[] names = summary
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected =
            new[] { "counts", "scanState", "serverId" };
        if (!names.SequenceEqual(expected))
            throw new InvalidDataException(
                "map_summary did not preserve the three-field public envelope.");
    }

    private static IReadOnlyDictionary<string, int> CountKinds(
        MapDataStore store,
        int serverId) =>
        MapScanContract.RecoveredDefaultTypes.ToDictionary(
            kind => kind,
            kind => store.CountRecords(kind, serverId),
            StringComparer.Ordinal);

    private static void ValidateCounts(
        JsonElement counts,
        IReadOnlyDictionary<string, int> expected,
        string label)
    {
        foreach (string kind in MapScanContract.RecoveredDefaultTypes)
        {
            int actual =
                counts.GetProperty(kind).GetInt32();
            if (actual != expected[kind])
            {
                throw new InvalidDataException(
                    $"{label} map_summary count mismatch for {kind}: actual={actual}, expected={expected[kind]}.");
            }
        }
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
