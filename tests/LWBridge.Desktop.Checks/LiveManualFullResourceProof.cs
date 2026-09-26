using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullResourceProof
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
        string databasePath = Path.Combine(proofRoot, "manual-full-resource-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-resource-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedResourceCount = 0;
        int reopenedResourceCount = 0;
        int resourceTypeCount = 0;
        int occupancyKnownCount = 0;
        int occupiedCount = 0;
        int levelCount = 0;
        int resourceNameCount = 0;
        int resourceMaxAmountCount = 0;
        int blackTileKnownCount = 0;
        int blackTileCount = 0;
        int detailKnownCount = 0;
        int fullResourceCount = 0;
        int partialResourceCount = 0;
        int invalidAmountCount = 0;
        int defaultFilteredCount = 0;
        int sortCheckCount = 0;
        int reopenedSortCheckCount = 0;
        int sortComparedRowCount = 0;
        long filterSampledAt = 0;
        ResourceFilterProofHelper.Metrics? filterMetrics = null;
        double scanWallSeconds = 0;
        string scanMode = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_MANUAL_SCAN_MODE"),
            "fast", StringComparison.OrdinalIgnoreCase) ? "fast" : "normal";
        int expectedConcurrency = scanMode == "fast" ? 20 : 8;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson = JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();

            string runId;
            int serverId;
            using (var store = new MapDataStore(databasePath))
            {
                var service = new ManualMapScanCommandService(lifecycle, store);
                try
                {
                    JsonElement payload = JsonSerializer.SerializeToElement(new
                    {
                        profileId = "manual-full-resource-proof",
                        scanMode,
                        selectedTypes = new[] { "resource" },
                    }, JsonOptions.Default);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    object? start = await service.InvokeAsync(
                        "map_scan_start", payload, operationCts.Token).ConfigureAwait(false);
                    JsonElement startStatus = JsonSerializer.SerializeToElement(start, JsonOptions.Default);
                    runId = startStatus.GetProperty("scanRunId").GetString() ?? string.Empty;
                    serverId = startStatus.GetProperty("serverId").GetInt32();
                    if (string.IsNullOrWhiteSpace(runId) || serverId <= 0 ||
                        startStatus.GetProperty("totalBlocks").GetInt32() != 2500 ||
                        startStatus.GetProperty("concurrency").GetInt32() != expectedConcurrency)
                        throw new InvalidDataException("Ordinary Manual Start did not expose expected Resource scan identity/geometry.");
                    JsonElement status = startStatus;
                    int lastReportedBlocks = 0;
                    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(4);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        operationCts.Token.ThrowIfCancellationRequested();
                        status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                        string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                        int currentReadBlocks = status.GetProperty("readBlocks").GetInt32();
                        if (currentReadBlocks >= lastReportedBlocks + 250)
                        {
                            lastReportedBlocks = (currentReadBlocks / 250) * 250;
                            Console.Error.WriteLine($"MANUAL_FULL_RESOURCE_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                        }
                        if (phase == "completed") break;
                        if (phase == "error")
                            throw new InvalidDataException("Ordinary Manual Resource scan failed: " +
                                (status.TryGetProperty("lastError", out JsonElement last) ? last.GetString() : "unknown"));
                        await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                    }
                    stopwatch.Stop();
                    scanWallSeconds = stopwatch.Elapsed.TotalSeconds;
                    status = JsonSerializer.SerializeToElement(service.CreateStatus(), JsonOptions.Default);
                    if (status.GetProperty("phase").GetString() != "completed" ||
                        status.GetProperty("isReading").GetBoolean() ||
                        status.GetProperty("readBlocks").GetInt32() != 2500 ||
                        status.GetProperty("failedBlocks").GetInt32() != 0 ||
                        status.GetProperty("unreadBlocks").GetInt32() != 0)
                    {
                        IReadOnlyList<MapScanBlockCheckpoint> partial = store.ReadScanBlockCheckpointsForTest(runId);
                        int secondAttempts = partial.Count(item => item.Attempts > 1);
                        throw new InvalidDataException($"Ordinary Manual Resource scan incomplete: phase={status.GetProperty("phase").GetString()}, read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, secondAttempts={secondAttempts}.");
                    }
                    publishedResourceCount = store.SearchIndexed(ResourceQuery(serverId)).Total;
                    if (publishedResourceCount <= 0)
                        throw new InvalidDataException("Ordinary Manual Resource scan published no Resource records.");
                    CollectResourceMetrics(store, serverId, out resourceTypeCount, out occupancyKnownCount,
                        out occupiedCount, out levelCount, out resourceNameCount, out resourceMaxAmountCount,
                        out blackTileKnownCount, out blackTileCount, out detailKnownCount, out fullResourceCount,
                        out partialResourceCount, out invalidAmountCount);
                    defaultFilteredCount = store.SearchIndexed(ResourceDefaultFilteredQuery(serverId)).Total;
                    if (invalidAmountCount != 0)
                        throw new InvalidDataException("Resource detail enrichment published an invalid remaining/full amount pair.");
                    if (detailKnownCount <= 0 || fullResourceCount <= 0)
                        throw new InvalidDataException("Resource detail enrichment produced no authoritative full Resource rows.");
                    ResourceSortMetrics sortMetrics = ValidateResourceSorts(store, serverId);
                    sortCheckCount = sortMetrics.CheckCount;
                    sortComparedRowCount = sortMetrics.RowCount;
                    filterSampledAt =
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    filterMetrics = ResourceFilterProofHelper.Validate(
                        store,
                        serverId,
                        filterSampledAt);
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
            {
                reopenedResourceCount = reopened.SearchIndexed(ResourceQuery(serverId)).Total;
                ResourceSortMetrics reopenedSortMetrics = ValidateResourceSorts(reopened, serverId);
                reopenedSortCheckCount = reopenedSortMetrics.CheckCount;
                if (reopenedSortCheckCount != sortCheckCount ||
                    reopenedSortMetrics.RowCount != sortComparedRowCount)
                    throw new InvalidDataException("Resource sort proof changed after database reopen.");
                ResourceFilterProofHelper.Metrics reopenedFilterMetrics =
                    ResourceFilterProofHelper.Validate(
                        reopened,
                        serverId,
                        filterSampledAt);
                if (filterMetrics is null ||
                    reopenedFilterMetrics != filterMetrics)
                    throw new InvalidDataException(
                        "Resource filter proof changed after database reopen.");
            }
            if (reopenedResourceCount != publishedResourceCount)
                throw new InvalidDataException("Ordinary Manual Resource count changed after database reopen.");
            if (filterMetrics is null)
                throw new InvalidDataException(
                    "Resource filter proof did not produce metrics.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_resource",
                totalBlocks = 2500,
                publishedResourceCount,
                reopenedResourceCount,
                scanMode,
                concurrency = expectedConcurrency,
                scanWallSeconds,
                resourceTypeCount,
                occupancyKnownCount,
                occupiedCount,
                levelCount,
                resourceNameCount,
                resourceMaxAmountCount,
                blackTileKnownCount,
                blackTileCount,
                detailKnownCount,
                fullResourceCount,
                partialResourceCount,
                invalidAmountCount,
                defaultFilteredCount,
                sortComparedRowCount,
                sortCheckCount,
                reopenedSortCheckCount,
                filterSampledAt,
                filterComparedRowCount = filterMetrics.RowCount,
                filterCheckCount = filterMetrics.CheckCount,
                filterSampleResourceNameKey =
                    filterMetrics.SampleResourceNameKey,
                filterResourceNameCount =
                    filterMetrics.ResourceNameCount,
                filterIdleCount = filterMetrics.IdleCount,
                filterFullCount = filterMetrics.FullCount,
                filterNonBlackCount = filterMetrics.NonBlackCount,
                filterSampleLevel = filterMetrics.SampleLevel,
                filterMinLevelCount = filterMetrics.MinLevelCount,
                filterMaxLevelCount = filterMetrics.MaxLevelCount,
                filterExactLevelCount = filterMetrics.ExactLevelCount,
                filterDefaultCombinedCount =
                    filterMetrics.DefaultCombinedCount,
            }, JsonOptions.Default));
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_RESOURCE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private sealed record ResourceSortMetrics(int CheckCount, int RowCount);

    private static ResourceSortMetrics ValidateResourceSorts(MapDataStore store, int serverId)
    {
        IReadOnlyList<JsonElement> allRows = ReadAllResourceRows(
            store,
            ResourceSortQuery(serverId, [new MapDataSort("updatedAt", "desc")]));
        if (allRows.Count < 2)
            throw new InvalidDataException("Resource sort proof requires at least two Resource rows.");

        int checks = 0;
        foreach (string sortBy in new[] { "level", "updatedAt" })
        {
            foreach (string sortOrder in new[] { "asc", "desc" })
            {
                ValidateResourceSortOrder(
                    store,
                    serverId,
                    allRows,
                    [new MapDataSort(sortBy, sortOrder)]);
                checks++;
            }
        }

        ValidateResourceSortOrder(
            store,
            serverId,
            allRows,
            [
                new MapDataSort("level", "asc"),
                new MapDataSort("updatedAt", "desc"),
            ]);
        checks++;

        return new ResourceSortMetrics(checks, allRows.Count);
    }

    private static void ValidateResourceSortOrder(
        MapDataStore store,
        int serverId,
        IReadOnlyList<JsonElement> allRows,
        IReadOnlyList<MapDataSort> sorts)
    {
        var expected = allRows.ToList();
        expected.Sort((left, right) => CompareResourceRows(left, right, sorts));

        IReadOnlyList<JsonElement> actual = ReadAllResourceRows(
            store,
            ResourceSortQuery(serverId, sorts));
        if (actual.Count != expected.Count)
            throw new InvalidDataException(
                $"Resource sort row count mismatch for {DescribeResourceSorts(sorts)}: actual={actual.Count}, expected={expected.Count}.");

        for (int index = 0; index < expected.Count; index++)
        {
            string expectedKey = ResourceRecordKey(expected[index]);
            string actualKey = ResourceRecordKey(actual[index]);
            if (!string.Equals(actualKey, expectedKey, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Resource sort mismatch for {DescribeResourceSorts(sorts)} at {index}: actual={actualKey}, expected={expectedKey}.");
        }
    }

    private static int CompareResourceRows(
        JsonElement left,
        JsonElement right,
        IReadOnlyList<MapDataSort> sorts)
    {
        foreach (MapDataSort sort in sorts)
        {
            double? leftValue = ResourceSortValue(left, sort.SortBy);
            double? rightValue = ResourceSortValue(right, sort.SortBy);
            int comparison;
            if (!leftValue.HasValue && !rightValue.HasValue)
                comparison = 0;
            else if (!leftValue.HasValue)
                comparison = 1;
            else if (!rightValue.HasValue)
                comparison = -1;
            else
            {
                comparison = leftValue.Value.CompareTo(rightValue.Value);
                if (sort.SortOrder == "desc") comparison = -comparison;
            }

            if (comparison != 0) return comparison;
        }

        return StringComparer.Ordinal.Compare(
            ResourceRecordKey(left),
            ResourceRecordKey(right));
    }

    private static double? ResourceSortValue(JsonElement row, string sortBy) =>
        sortBy switch
        {
            "level" => OptionalResourceNumber(row, "level"),
            "updatedAt" => OptionalResourceNumber(row, "updatedAt"),
            _ => throw new InvalidDataException("Unexpected Resource sort key: " + sortBy),
        };

    private static double? OptionalResourceNumber(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static string ResourceRecordKey(JsonElement row) =>
        row.TryGetProperty("recordKey", out JsonElement key) &&
        key.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(key.GetString())
            ? key.GetString()!
            : throw new InvalidDataException("Resource sort proof row has no recordKey.");

    private static string DescribeResourceSorts(IReadOnlyList<MapDataSort> sorts) =>
        string.Join(",", sorts.Select(sort => $"{sort.SortBy}:{sort.SortOrder}"));

    private static MapDataQueryOptions ResourceSortQuery(
        int serverId,
        IReadOnlyList<MapDataSort> sorts)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind = "resource",
            query = new
            {
                serverId,
                sorts = sorts.Select(sort => new
                {
                    sortBy = sort.SortBy,
                    sortOrder = sort.SortOrder,
                }).ToArray(),
            },
        }, JsonOptions.Default);
        MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(payload);
        if (query.UnsupportedFeatures.Count != 0)
            throw new InvalidDataException(
                "Recovered Resource sort unexpectedly failed contract gate: " +
                string.Join(",", query.UnsupportedFeatures));
        return query;
    }

    private static IReadOnlyList<JsonElement> ReadAllResourceRows(
        MapDataStore store,
        MapDataQueryOptions query)
    {
        var rows = new List<JsonElement>();
        int page = 1;
        int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(query with { Page = page });
            if (expectedTotal < 0) expectedTotal = result.Total;
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (rows.Count != expectedTotal)
            throw new InvalidDataException($"Resource sort proof observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }

    private static void CollectResourceMetrics(
        MapDataStore store, int serverId, out int resourceTypeCount,
        out int occupancyKnownCount, out int occupiedCount, out int levelCount,
        out int resourceNameCount, out int resourceMaxAmountCount,
        out int blackTileKnownCount, out int blackTileCount, out int detailKnownCount,
        out int fullResourceCount, out int partialResourceCount, out int invalidAmountCount)
    {
        resourceTypeCount = occupancyKnownCount = occupiedCount = levelCount = 0;
        resourceNameCount = resourceMaxAmountCount = blackTileKnownCount = blackTileCount = 0;
        detailKnownCount = fullResourceCount = partialResourceCount = invalidAmountCount = 0;
        int page = 1; int observed = 0; int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexed(ResourceQuery(serverId, page));
            if (expectedTotal < 0) expectedTotal = result.Total;
            foreach (JsonElement row in result.Rows)
            {
                observed++;
                if (row.TryGetProperty("level", out JsonElement level) && level.TryGetInt32(out _)) levelCount++;
                if (row.TryGetProperty("resourceTypeId", out JsonElement resourceType) &&
                    ((resourceType.ValueKind == JsonValueKind.Number && resourceType.TryGetInt32(out _)) ||
                     (resourceType.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(resourceType.GetString()))))
                    resourceTypeCount++;
                if (row.TryGetProperty("rebuildGatherOccupancyKnown", out JsonElement known) && known.ValueKind == JsonValueKind.True)
                {
                    occupancyKnownCount++;
                    if (row.TryGetProperty("rebuildGatherOccupied", out JsonElement occupied) && occupied.ValueKind == JsonValueKind.True)
                        occupiedCount++;
                }
                if (row.TryGetProperty("resourceNameKey", out JsonElement resourceName) &&
                    resourceName.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(resourceName.GetString()))
                    resourceNameCount++;
                if (row.TryGetProperty("resourceMaxAmount", out JsonElement maxAmount) &&
                    maxAmount.ValueKind == JsonValueKind.Number && maxAmount.TryGetDouble(out double max) && max >= 0)
                    resourceMaxAmountCount++;
                if (row.TryGetProperty("blackTileKnown", out JsonElement blackKnown) && blackKnown.ValueKind == JsonValueKind.True)
                {
                    blackTileKnownCount++;
                    if (row.TryGetProperty("isBlackTile", out JsonElement black) && black.ValueKind == JsonValueKind.True)
                        blackTileCount++;
                }
                if (row.TryGetProperty("resourceDetailKnown", out JsonElement detailKnown) && detailKnown.ValueKind == JsonValueKind.True)
                {
                    detailKnownCount++;
                    long remaining = -1;
                    long full = -1;
                    bool hasRemaining = row.TryGetProperty("resourceRemainingAmount", out JsonElement remainingValue) &&
                        remainingValue.TryGetInt64(out remaining) && remaining >= 0;
                    bool hasFull = row.TryGetProperty("resourceFullAmount", out JsonElement fullValue) &&
                        fullValue.TryGetInt64(out full) && full >= 0;
                    if (!hasRemaining || !hasFull || remaining > full) invalidAmountCount++;
                    else if (remaining == full) fullResourceCount++;
                    else partialResourceCount++;
                }
            }
            if (observed >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (observed != expectedTotal)
            throw new InvalidDataException($"Resource metric read observed {observed}/{expectedTotal} rows.");
    }

    private static MapDataQueryOptions ResourceQuery(int serverId, int page = 1) => new(
        "resource", serverId, page, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, null, null, null, false, false, false,
        null, null, Array.Empty<string>());

    private static MapDataQueryOptions ResourceDefaultFilteredQuery(int serverId) => new(
        "resource", serverId, 1, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, null, null, null, false, false, false,
        null, null, Array.Empty<string>(),
        ResourceIdleOnly: true, ResourceFullOnly: true, ExcludeBlackTile: true);

    private static void TryDelete(string path)
    {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { }
        }
    }
}
