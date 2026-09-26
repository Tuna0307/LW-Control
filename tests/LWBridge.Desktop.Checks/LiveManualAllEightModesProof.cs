using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualAllEightModesProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-all-eight-modes");
        Directory.CreateDirectory(proofRoot);
        string proofPath = Path.Combine(proofRoot, "last-proof.json");
        string databasePath = Path.Combine(
            proofRoot, "manual-all-eight-modes-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-all-eight-modes-proof", gameRoot);
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

            ScanObservation normal;
            ScanObservation fast;
            Dictionary<string, int> normalCounts;
            Dictionary<string, int> fastCounts;
            using (var store = new MapDataStore(databasePath))
            {
                var service = new ManualMapScanCommandService(lifecycle, store);
                try
                {
                    normal = await RunScanAsync(service, store, "all-eight-normal", "normal", operationCts.Token)
                        .ConfigureAwait(false);
                    normalCounts = Counts(store, normal.ServerId);
                    if (normalCounts.Count(pair => pair.Value > 0) < 4)
                        throw new InvalidDataException("Normal all-eight scan did not publish enough independently recovered kinds.");

                    fast = await RunScanAsync(service, store, "all-eight-fast", "fast", operationCts.Token)
                        .ConfigureAwait(false);
                    if (fast.ServerId != normal.ServerId)
                        throw new InvalidDataException("Same-owned-session Normal/Fast scans changed server identity.");
                    fastCounts = Counts(store, fast.ServerId);
                    if (fastCounts.Count(pair => pair.Value > 0) < 4)
                        throw new InvalidDataException("Fast all-eight scan did not publish enough independently recovered kinds.");
                }
                finally
                {
                    service.Close();
                }
            }

            Dictionary<string, int> reopenedCounts;
            using (var reopened = new MapDataStore(databasePath))
                reopenedCounts = Counts(reopened, fast.ServerId);
            foreach (string kind in MapScanContract.RecoveredDefaultTypes)
                if (reopenedCounts[kind] != fastCounts[kind])
                    throw new InvalidDataException($"Fast all-eight {kind} count changed after database reopen.");

            string proofJson = JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "same_owned_session_all_eight_normal_then_fast",
                originalEight = MapScanContract.RecoveredDefaultTypes,
                dedicatedZombieBossExcluded = true,
                normal = Shape(normal, normalCounts),
                fast = new
                {
                    fast.ScanMode,
                    fast.Concurrency,
                    fast.TotalBlocks,
                    fast.ReadBlocks,
                    fast.FailedBlocks,
                    fast.UnreadBlocks,
                    fast.ScanWallSeconds,
                    counts = fastCounts,
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
                    Console.Error.WriteLine("LIVE_MANUAL_ALL_EIGHT_MODES_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static object Shape(ScanObservation item, IReadOnlyDictionary<string, int> counts) => new
    {
        item.ScanMode,
        item.Concurrency,
        item.TotalBlocks,
        item.ReadBlocks,
        item.FailedBlocks,
        item.UnreadBlocks,
        item.ScanWallSeconds,
        counts,
    };

    private static async Task<ScanObservation> RunScanAsync(
        ManualMapScanCommandService service,
        MapDataStore store,
        string label,
        string scanMode,
        CancellationToken cancellationToken)
    {
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            profileId = "manual-all-eight-modes-proof",
            scanMode,
            selectedTypes = MapScanContract.RecoveredDefaultTypes,
        }, JsonOptions.Default);

        Stopwatch stopwatch = Stopwatch.StartNew();
        object? start = await service.InvokeAsync("map_scan_start", payload, cancellationToken).ConfigureAwait(false);
        JsonElement startStatus = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
        string runId = startStatus.GetProperty("scanRunId").GetString() ?? string.Empty;
        int serverId = startStatus.GetProperty("serverId").GetInt32();
        string[] observedTypes = startStatus.GetProperty("selectedTypes")
            .EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        if (string.IsNullOrWhiteSpace(runId) || serverId <= 0 ||
            startStatus.GetProperty("totalBlocks").GetInt32() != 2500 ||
            startStatus.GetProperty("concurrency").GetInt32() != expectedConcurrency ||
            !observedTypes.SequenceEqual(MapScanContract.RecoveredDefaultTypes))
            throw new InvalidDataException($"{label} Start did not preserve all-eight identity/types/geometry.");

        JsonElement status = startStatus;
        int lastReportedBlocks = 0;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
            string phase = status.GetProperty("phase").GetString() ?? string.Empty;
            int readBlocks = status.GetProperty("readBlocks").GetInt32();
            if (readBlocks >= lastReportedBlocks + 250)
            {
                lastReportedBlocks = (readBlocks / 250) * 250;
                Console.Error.WriteLine(
                    $"MANUAL_{label.ToUpperInvariant().Replace('-', '_')}_PROGRESS blocks={readBlocks}/2500 phase={phase}");
            }
            if (phase == "idle" && readBlocks == 2500) break;
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
        if (status.GetProperty("phase").GetString() != "idle" ||
            status.GetProperty("isReading").GetBoolean() || read != 2500 || failed != 0 || unread != 0)
        {
            IReadOnlyList<MapScanBlockCheckpoint> partial = store.ReadScanBlockCheckpointsForTest(runId);
            throw new InvalidDataException(
                $"{label} incomplete: phase={status.GetProperty("phase").GetString()}, read={read}, failed={failed}, " +
                $"unread={unread}, checkpoints={partial.Count}, secondAttempts={partial.Count(item => item.Attempts > 1)}.");
        }
        return new ScanObservation(
            serverId, scanMode, expectedConcurrency, 2500, read, failed, unread, stopwatch.Elapsed.TotalSeconds);
    }

    private static Dictionary<string, int> Counts(MapDataStore store, int serverId) =>
        MapScanContract.RecoveredDefaultTypes.ToDictionary(
            kind => kind, kind => store.CountRecords(kind, serverId), StringComparer.Ordinal);

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
