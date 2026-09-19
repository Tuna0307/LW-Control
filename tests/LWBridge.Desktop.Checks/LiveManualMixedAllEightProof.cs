using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualMixedAllEightProof
{
    private static readonly string[] MixedTypes = ["city", "truck", "dispatch", "treasure"];

    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-mixed-all-eight");
        Directory.CreateDirectory(proofRoot);
        string proofPath = Path.Combine(proofRoot, "last-proof.json");
        string databasePath = Path.Combine(
            proofRoot, "manual-mixed-all-eight-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-mixed-all-eight-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson = JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();

            ScanObservation mixed;
            ScanObservation allEight;
            Dictionary<string, int> mixedCounts;
            Dictionary<string, int> allEightCounts;
            using (var store = new MapDataStore(databasePath))
            {
                var service = new ManualMapScanCommandService(lifecycle, store);
                try
                {
                    mixed = await RunScanAsync(
                        service, store, "mixed-normal", "normal", MixedTypes, operationCts.Token).ConfigureAwait(false);
                    mixedCounts = Counts(store, mixed.ServerId, MixedTypes);
                    if (mixedCounts.Count(pair => pair.Value > 0) < 2)
                        throw new InvalidDataException(
                            "Mixed Manual scan did not publish positive rows for at least two selected kinds.");

                    allEight = await RunScanAsync(
                        service, store, "all-eight-normal", "normal",
                        MapScanContract.RecoveredDefaultTypes, operationCts.Token).ConfigureAwait(false);
                    if (allEight.ServerId != mixed.ServerId)
                        throw new InvalidDataException("Same-owned-session mixed/all-eight scans changed server identity.");
                    allEightCounts = Counts(store, allEight.ServerId, MapScanContract.RecoveredDefaultTypes);
                    if (allEightCounts.Count(pair => pair.Value > 0) < 4)
                        throw new InvalidDataException(
                            "All-eight Manual scan did not publish positive rows for enough independently recovered kinds.");
                }
                finally
                {
                    service.Close();
                }
            }

            Dictionary<string, int> reopenedCounts;
            using (var reopened = new MapDataStore(databasePath))
                reopenedCounts = Counts(reopened, allEight.ServerId, MapScanContract.RecoveredDefaultTypes);
            foreach (string kind in MapScanContract.RecoveredDefaultTypes)
                if (reopenedCounts[kind] != allEightCounts[kind])
                    throw new InvalidDataException($"All-eight {kind} count changed after database reopen.");

            string proofJson = JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_mixed_then_all_eight",
                originalEight = MapScanContract.RecoveredDefaultTypes,
                dedicatedZombieBossExcluded = true,
                mixed = new
                {
                    mixed.ScanMode,
                    mixed.Concurrency,
                    mixed.TotalBlocks,
                    mixed.ReadBlocks,
                    mixed.FailedBlocks,
                    mixed.UnreadBlocks,
                    mixed.ScanWallSeconds,
                    counts = mixedCounts,
                },
                allEight = new
                {
                    allEight.ScanMode,
                    allEight.Concurrency,
                    allEight.TotalBlocks,
                    allEight.ReadBlocks,
                    allEight.FailedBlocks,
                    allEight.UnreadBlocks,
                    allEight.ScanWallSeconds,
                    counts = allEightCounts,
                    reopenedCounts,
                },
            }, JsonOptions.Default);
            File.WriteAllText(proofPath, proofJson);
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
                    Console.Error.WriteLine("LIVE_MANUAL_MIXED_ALL_EIGHT_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static async Task<ScanObservation> RunScanAsync(
        ManualMapScanCommandService service,
        MapDataStore store,
        string label,
        string scanMode,
        IReadOnlyList<string> selectedTypes,
        CancellationToken cancellationToken)
    {
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            profileId = "manual-mixed-all-eight-proof",
            scanMode,
            selectedTypes,
        }, JsonOptions.Default);

        Stopwatch stopwatch = Stopwatch.StartNew();
        object? start = await service.InvokeAsync(
            "map_scan_start", payload, cancellationToken).ConfigureAwait(false);
        JsonElement startStatus = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
        string runId = startStatus.GetProperty("scanRunId").GetString() ?? string.Empty;
        int serverId = startStatus.GetProperty("serverId").GetInt32();
        string[] observedTypes = startStatus.GetProperty("selectedTypes")
            .EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        if (string.IsNullOrWhiteSpace(runId) || serverId <= 0 ||
            startStatus.GetProperty("totalBlocks").GetInt32() != 2500 ||
            startStatus.GetProperty("concurrency").GetInt32() != expectedConcurrency ||
            !observedTypes.SequenceEqual(selectedTypes))
            throw new InvalidDataException($"{label} Start did not preserve expected scan identity/types/geometry.");

        JsonElement status = startStatus;
        int lastReportedBlocks = 0;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
            string phase = status.GetProperty("phase").GetString() ?? string.Empty;
            int currentReadBlocks = status.GetProperty("readBlocks").GetInt32();
            if (currentReadBlocks >= lastReportedBlocks + 250)
            {
                lastReportedBlocks = (currentReadBlocks / 250) * 250;
                Console.Error.WriteLine(
                    $"MANUAL_{label.ToUpperInvariant().Replace('-', '_')}_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
            }
            if (phase == "completed") break;
            if (phase == "error")
                throw new InvalidDataException($"{label} scan failed: " +
                    (status.TryGetProperty("lastError", out JsonElement last) ? last.GetString() : "unknown"));
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        stopwatch.Stop();
        status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
        int read = status.GetProperty("readBlocks").GetInt32();
        int failed = status.GetProperty("failedBlocks").GetInt32();
        int unread = status.GetProperty("unreadBlocks").GetInt32();
        if (status.GetProperty("phase").GetString() != "completed" ||
            status.GetProperty("isReading").GetBoolean() || read != 2500 || failed != 0 || unread != 0)
        {
            IReadOnlyList<MapScanBlockCheckpoint> partial = store.ReadScanBlockCheckpointsForTest(runId);
            int secondAttempts = partial.Count(item => item.Attempts > 1);
            throw new InvalidDataException(
                $"{label} incomplete: phase={status.GetProperty("phase").GetString()}, read={read}, failed={failed}, " +
                $"unread={unread}, checkpoints={partial.Count}, secondAttempts={secondAttempts}.");
        }
        return new ScanObservation(
            serverId, scanMode, expectedConcurrency, 2500, read, failed, unread, stopwatch.Elapsed.TotalSeconds);
    }

    private static Dictionary<string, int> Counts(
        MapDataStore store, int serverId, IEnumerable<string> kinds) =>
        kinds.ToDictionary(kind => kind, kind => store.CountRecords(kind, serverId), StringComparer.Ordinal);

    private static void TryDelete(string path)
    {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { }
        }
    }

    private sealed record ScanObservation(
        int ServerId,
        string ScanMode,
        int Concurrency,
        int TotalBlocks,
        int ReadBlocks,
        int FailedBlocks,
        int UnreadBlocks,
        double ScanWallSeconds);
}
