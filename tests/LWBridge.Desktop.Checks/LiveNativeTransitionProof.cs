using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveNativeTransitionProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            "lwbridge-native-transition-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("native-transition-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        string? instanceId = null;
        Exception? operationError = null;

        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? started = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement startedJson = JsonSerializer.SerializeToElement(started, JsonOptions.Default);
            instanceId = startedJson.GetProperty("instanceId").GetString();

            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            try
            {
                TruckSnapshot first = await RunTruckScanAsync(
                    service, store, "A", operationCts.Token).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(2), operationCts.Token).ConfigureAwait(false);
                TruckSnapshot second = await RunTruckScanAsync(
                    service, store, "B", operationCts.Token).ConfigureAwait(false);
                TransitionDelta delta = Compare(first, second);
                TruckSnapshot? third = null;
                if (delta.TotalObservedTransitions == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), operationCts.Token).ConfigureAwait(false);
                    third = await RunTruckScanAsync(
                        service, store, "C", operationCts.Token).ConfigureAwait(false);
                    delta = Compare(second, third);
                }

                if (first.Rows.Count == 0 || second.Rows.Count == 0)
                    throw new InvalidDataException("Live B11 proof requires a positive Truck population.");
                if (delta.CommonCount == 0 && delta.AddedCount == 0 && delta.RemovedCount == 0)
                    throw new InvalidDataException("Live B11 proof could not correlate any Truck identities across snapshots.");

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "native_point_march_transition_live_observation",
                    serverId = delta.ServerId,
                    snapshots = new[]
                    {
                        SnapshotSummary(first),
                        SnapshotSummary(second),
                        third is null ? null : SnapshotSummary(third),
                    }.Where(item => item is not null).ToArray(),
                    compared = third is null ? "A->B" : "B->C",
                    transition = new
                    {
                        common = delta.CommonCount,
                        added = delta.AddedCount,
                        removed = delta.RemovedCount,
                        moved = delta.MovedCount,
                        metadataChanged = delta.MetadataChangedCount,
                        totalObserved = delta.TotalObservedTransitions,
                        addedSamples = delta.Added.Take(10).ToArray(),
                        removedSamples = delta.Removed.Take(10).ToArray(),
                        movedSamples = delta.Moved.Take(10).Select(item => new
                        {
                            marchUuid = item.Key,
                            before = new { x = item.Before.X, y = item.Before.Y, pointIndex = item.Before.PointIndex },
                            after = new { x = item.After.X, y = item.After.Y, pointIndex = item.After.PointIndex },
                        }).ToArray(),
                    },
                    identity = new
                    {
                        recordKeyEqualsNativeMarchUuid = true,
                        exact64BitTextPreserved = true,
                    },
                    safety = new
                    {
                        readOnlyMapScanning = true,
                        followInvoked = false,
                        attackOrPlunder = false,
                        claimOrCollect = false,
                        messaging = false,
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
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_NATIVE_TRANSITION_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }

            foreach (string candidate in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
                try { File.Delete(candidate); } catch { }
        }
    }

    private static async Task<TruckSnapshot> RunTruckScanAsync(
        ManualMapScanCommandService service,
        MapDataStore store,
        string label,
        CancellationToken cancellationToken)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            profileId = "native-transition-proof",
            selectedTypes = new[] { "truck" },
        }, JsonOptions.Default);

        Stopwatch stopwatch = Stopwatch.StartNew();
        object? start = await service.InvokeAsync(
            "map_scan_start", payload, cancellationToken).ConfigureAwait(false);
        JsonElement status = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
        string runId = status.GetProperty("scanRunId").GetString() ?? string.Empty;
        int serverId = status.GetProperty("serverId").GetInt32();
        if (string.IsNullOrWhiteSpace(runId) || serverId <= 0 ||
            status.GetProperty("totalBlocks").GetInt32() != 2500)
            throw new InvalidDataException("Live B11 scan did not expose full-world run identity.");

        int lastReported = 0;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
            string phase = status.GetProperty("phase").GetString() ?? string.Empty;
            int read = status.GetProperty("readBlocks").GetInt32();
            if (read >= lastReported + 500)
            {
                lastReported = (read / 500) * 500;
                Console.Error.WriteLine($"NATIVE_TRANSITION_{label}_PROGRESS blocks={read}/2500 phase={phase}");
            }
            if (phase == "completed") break;
            if (phase == "error")
                throw new InvalidDataException(
                    $"Live B11 Truck scan {label} failed: " +
                    (status.TryGetProperty("lastError", out JsonElement error) ? error.GetString() : "unknown"));
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        stopwatch.Stop();

        status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
        if (status.GetProperty("phase").GetString() != "completed" ||
            status.GetProperty("readBlocks").GetInt32() != 2500 ||
            status.GetProperty("failedBlocks").GetInt32() != 0 ||
            status.GetProperty("unreadBlocks").GetInt32() != 0)
            throw new InvalidDataException($"Live B11 Truck scan {label} did not complete exactly.");

        IReadOnlyList<MapStoredRecord> records = store.ReadRecords("truck", serverId);
        var rows = new Dictionary<string, TruckRow>(StringComparer.Ordinal);
        foreach (MapStoredRecord record in records)
        {
            if (string.IsNullOrWhiteSpace(record.RecordKey) ||
                !string.Equals(record.RecordKey, record.Uuid, StringComparison.Ordinal))
                throw new InvalidDataException("Live Truck row did not preserve native march UUID as record key.");
            using JsonDocument data = JsonDocument.Parse(record.DataJson);
            JsonElement root = data.RootElement;
            string marchUuid = root.GetProperty("marchUuid").GetString() ?? string.Empty;
            if (!string.Equals(marchUuid, record.RecordKey, StringComparison.Ordinal))
                throw new InvalidDataException("Live Truck data marchUuid diverged from indexed record identity.");
            rows.Add(record.RecordKey, new TruckRow(
                root.GetProperty("x").GetInt32(),
                root.GetProperty("y").GetInt32(),
                record.PointIndex,
                record.Quality,
                record.Power,
                record.Name));
        }

        return new TruckSnapshot(
            label,
            serverId,
            runId,
            status.GetProperty("scanMode").GetString() ?? string.Empty,
            status.GetProperty("scanStrategy").GetString() ?? string.Empty,
            status.GetProperty("concurrency").GetInt32(),
            stopwatch.Elapsed.TotalSeconds,
            rows);
    }

    private static TransitionDelta Compare(TruckSnapshot before, TruckSnapshot after)
    {
        if (before.ServerId != after.ServerId)
            throw new InvalidDataException("Live B11 snapshots were captured on different servers.");

        string[] added = after.Rows.Keys.Except(before.Rows.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        string[] removed = before.Rows.Keys.Except(after.Rows.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        string[] common = before.Rows.Keys.Intersect(after.Rows.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var moved = common
            .Where(key => before.Rows[key].X != after.Rows[key].X ||
                          before.Rows[key].Y != after.Rows[key].Y ||
                          before.Rows[key].PointIndex != after.Rows[key].PointIndex)
            .Select(key => new MovedRow(key, before.Rows[key], after.Rows[key]))
            .ToArray();
        int metadataChanged = common.Count(key =>
            before.Rows[key].Quality != after.Rows[key].Quality ||
            before.Rows[key].Power != after.Rows[key].Power ||
            !string.Equals(before.Rows[key].Name, after.Rows[key].Name, StringComparison.Ordinal));

        return new TransitionDelta(
            before.ServerId,
            common.Length,
            added,
            removed,
            moved,
            metadataChanged);
    }

    private static object SnapshotSummary(TruckSnapshot snapshot) => new
    {
        snapshot.Label,
        snapshot.ServerId,
        snapshot.RunId,
        snapshot.ScanMode,
        snapshot.ScanStrategy,
        snapshot.Concurrency,
        count = snapshot.Rows.Count,
        snapshot.WallSeconds,
    };

    private sealed record TruckSnapshot(
        string Label,
        int ServerId,
        string RunId,
        string ScanMode,
        string ScanStrategy,
        int Concurrency,
        double WallSeconds,
        IReadOnlyDictionary<string, TruckRow> Rows);

    private sealed record TruckRow(
        int X,
        int Y,
        int? PointIndex,
        int? Quality,
        long? Power,
        string? Name);

    private sealed record MovedRow(string Key, TruckRow Before, TruckRow After);

    private sealed record TransitionDelta(
        int ServerId,
        int CommonCount,
        IReadOnlyList<string> Added,
        IReadOnlyList<string> Removed,
        IReadOnlyList<MovedRow> Moved,
        int MetadataChangedCount)
    {
        public int AddedCount => Added.Count;
        public int RemovedCount => Removed.Count;
        public int MovedCount => Moved.Count;
        public int TotalObservedTransitions => AddedCount + RemovedCount + MovedCount + MetadataChangedCount;
    }
}
