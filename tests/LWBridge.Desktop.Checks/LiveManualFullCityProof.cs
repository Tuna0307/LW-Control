using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveManualFullCityProof
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
        string databasePath = Path.Combine(proofRoot, "manual-full-city-" + Guid.NewGuid().ToString("N") + ".db");
        using var lifecycle = new OverviewLifecycleService("manual-full-city-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        string? instanceId = null;
        Exception? operationError = null;
        int publishedCityCount = 0;
        int reopenedCityCount = 0;
        int healthValueCount = 0;
        int shieldValueCount = 0;
        int distinctShieldValueCount = 0;
        int sortCheckCount = 0;
        int reopenedSortCheckCount = 0;
        int sortComparedRowCount = 0;
        long sortSampledAt = 0;
        CityFilterProofHelper.Metrics? filterMetrics = null;
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
                    profileId = "manual-full-city-proof",
                    scanMode,
                    selectedTypes = new[] { "city" },
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
                {
                    throw new InvalidDataException("Ordinary Manual Start did not expose the expected City scan identity/geometry.");
                }

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
                        Console.Error.WriteLine($"MANUAL_FULL_CITY_PROGRESS blocks={currentReadBlocks}/2500 phase={phase}");
                    }
                    if (phase == "completed") break;
                    if (phase == "error")
                        throw new InvalidDataException("Ordinary Manual City scan failed: " +
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
                    throw new InvalidDataException($"Ordinary Manual City scan incomplete: phase={status.GetProperty("phase").GetString()}, read={status.GetProperty("readBlocks").GetInt32()}, failed={status.GetProperty("failedBlocks").GetInt32()}, unread={status.GetProperty("unreadBlocks").GetInt32()}, checkpoints={partial.Count}, secondAttempts={secondAttempts}.");
                }

                publishedCityCount = store.SearchIndexed(CityQuery(serverId)).Total;
                if (publishedCityCount <= 0)
                    throw new InvalidDataException("Ordinary Manual City scan published no City records.");
                sortSampledAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                CitySortMetrics sortMetrics = ValidateCitySorts(store, serverId, sortSampledAt);
                sortCheckCount = sortMetrics.CheckCount;
                sortComparedRowCount = sortMetrics.RowCount;
                healthValueCount = sortMetrics.HealthValueCount;
                shieldValueCount = sortMetrics.ShieldValueCount;
                distinctShieldValueCount = sortMetrics.DistinctShieldValueCount;
                CityFilterProofHelper.SeedTransientLiveMark(
                    store,
                    serverId,
                    sortSampledAt);
                filterMetrics = CityFilterProofHelper.Validate(
                    store,
                    serverId,
                    sortSampledAt);
                }
                finally
                {
                    service.Close();
                }
            }

            using (var reopened = new MapDataStore(databasePath))
            {
                reopenedCityCount = reopened.SearchIndexed(CityQuery(serverId)).Total;
                CitySortMetrics reopenedSortMetrics = ValidateCitySorts(reopened, serverId, sortSampledAt);
                reopenedSortCheckCount = reopenedSortMetrics.CheckCount;
                if (reopenedSortCheckCount != sortCheckCount ||
                    reopenedSortMetrics.RowCount != sortComparedRowCount ||
                    reopenedSortMetrics.HealthValueCount != healthValueCount ||
                    reopenedSortMetrics.ShieldValueCount != shieldValueCount ||
                    reopenedSortMetrics.DistinctShieldValueCount != distinctShieldValueCount)
                    throw new InvalidDataException("City sort proof changed after database reopen.");
                CityFilterProofHelper.Metrics reopenedFilterMetrics =
                    CityFilterProofHelper.Validate(
                        reopened,
                        serverId,
                        sortSampledAt);
                if (filterMetrics is null ||
                    reopenedFilterMetrics != filterMetrics)
                    throw new InvalidDataException(
                        "City filter proof changed after database reopen.");
            }
            if (reopenedCityCount != publishedCityCount)
                throw new InvalidDataException("Ordinary Manual City count changed after database reopen.");
            if (filterMetrics is null)
                throw new InvalidDataException(
                    "City filter proof did not produce metrics.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "ordinary_manual_start_full_city",
                totalBlocks = 2500,
                publishedCityCount,
                reopenedCityCount,
                scanMode,
                concurrency = expectedConcurrency,
                scanWallSeconds,
                sortSampledAt,
                sortComparedRowCount,
                healthValueCount,
                shieldValueCount,
                distinctShieldValueCount,
                sortCheckCount,
                reopenedSortCheckCount,
                filterComparedRowCount = filterMetrics.RowCount,
                filterCheckCount = filterMetrics.CheckCount,
                filterAllianceCount = filterMetrics.AllianceCount,
                filterWithoutAllianceCount =
                    filterMetrics.WithoutAllianceCount,
                filterMarkedCount = filterMetrics.MarkedCount,
                filterKeywordIdentityCount =
                    filterMetrics.KeywordIdentityCount,
                filterKeywordPercentLiteralCount =
                    filterMetrics.KeywordPercentLiteralCount,
                filterKeywordUnderscoreLiteralCount =
                    filterMetrics.KeywordUnderscoreLiteralCount,
                filterKeywordBackslashLiteralCount =
                    filterMetrics.KeywordBackslashLiteralCount,
                transientLocalMarkSeeded = true,
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
                    Console.Error.WriteLine("LIVE_MANUAL_FULL_CITY_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private sealed record CitySortMetrics(
        int CheckCount,
        int RowCount,
        int HealthValueCount,
        int ShieldValueCount,
        int DistinctShieldValueCount);

    private static CitySortMetrics ValidateCitySorts(
        MapDataStore store,
        int serverId,
        long sampledAt)
    {
        IReadOnlyList<JsonElement> allRows = ReadAllCityRows(
            store,
            CitySortQuery(serverId, [new MapDataSort("updatedAt", "desc")]),
            sampledAt);
        if (allRows.Count < 2)
            throw new InvalidDataException("City sort proof requires at least two City rows.");

        int healthValues = allRows.Count(row => CityHealthValue(row).HasValue);
        long[] shieldValues = allRows
            .Select(row => CityShieldValue(row, sampledAt))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        int distinctShieldValues = shieldValues.Distinct().Count();

        int checks = 0;
        foreach (string sortBy in new[] { "level", "health", "shield", "updatedAt" })
        {
            foreach (string sortOrder in new[] { "asc", "desc" })
            {
                ValidateCitySortOrder(
                    store,
                    serverId,
                    sampledAt,
                    allRows,
                    [new MapDataSort(sortBy, sortOrder)]);
                checks++;
            }
        }

        ValidateCitySortOrder(
            store,
            serverId,
            sampledAt,
            allRows,
            [
                new MapDataSort("shield", "asc"),
                new MapDataSort("health", "desc"),
                new MapDataSort("level", "desc"),
                new MapDataSort("updatedAt", "desc"),
            ]);
        checks++;

        return new CitySortMetrics(
            checks,
            allRows.Count,
            healthValues,
            shieldValues.Length,
            distinctShieldValues);
    }

    private static void ValidateCitySortOrder(
        MapDataStore store,
        int serverId,
        long sampledAt,
        IReadOnlyList<JsonElement> allRows,
        IReadOnlyList<MapDataSort> sorts)
    {
        var expected = allRows.ToList();
        expected.Sort((left, right) => CompareCityRows(left, right, sorts, sampledAt));

        IReadOnlyList<JsonElement> actual = ReadAllCityRows(
            store,
            CitySortQuery(serverId, sorts),
            sampledAt);
        if (actual.Count != expected.Count)
            throw new InvalidDataException(
                $"City sort row count mismatch for {DescribeCitySorts(sorts)}: actual={actual.Count}, expected={expected.Count}.");

        for (int index = 0; index < expected.Count; index++)
        {
            string expectedKey = CityRecordKey(expected[index]);
            string actualKey = CityRecordKey(actual[index]);
            if (!string.Equals(actualKey, expectedKey, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"City sort mismatch for {DescribeCitySorts(sorts)} at {index}: actual={actualKey}, expected={expectedKey}.");
        }
    }

    private static int CompareCityRows(
        JsonElement left,
        JsonElement right,
        IReadOnlyList<MapDataSort> sorts,
        long sampledAt)
    {
        foreach (MapDataSort sort in sorts)
        {
            double? leftValue = CitySortValue(left, sort.SortBy, sampledAt);
            double? rightValue = CitySortValue(right, sort.SortBy, sampledAt);
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
            CityRecordKey(left),
            CityRecordKey(right));
    }

    private static double? CitySortValue(
        JsonElement row,
        string sortBy,
        long sampledAt) =>
        sortBy switch
        {
            "level" => OptionalCityNumber(row, "level"),
            "health" => CityHealthValue(row),
            "shield" => CityShieldValue(row, sampledAt),
            "updatedAt" => OptionalCityNumber(row, "updatedAt"),
            _ => throw new InvalidDataException("Unexpected City sort key: " + sortBy),
        };

    private static double? CityHealthValue(JsonElement row)
    {
        double? health = OptionalCityNumber(row, "health");
        return health is not null && health.Value != 0d ? health : null;
    }

    private static long? CityShieldValue(JsonElement row, long sampledAt)
    {
        if (!row.TryGetProperty("protectEndTime", out JsonElement protect) ||
            protect.ValueKind != JsonValueKind.Number ||
            !protect.TryGetInt64(out long expiry))
            return null;

        if (expiry >= 1_000_000_000_000L)
            return expiry > sampledAt ? expiry : null;

        long sampledSeconds = sampledAt / 1000;
        return expiry > sampledSeconds ? expiry : null;
    }

    private static double? OptionalCityNumber(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double parsed)
            ? parsed
            : null;

    private static string CityRecordKey(JsonElement row) =>
        row.TryGetProperty("recordKey", out JsonElement key) &&
        key.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(key.GetString())
            ? key.GetString()!
            : throw new InvalidDataException("City sort proof row has no recordKey.");

    private static string DescribeCitySorts(IReadOnlyList<MapDataSort> sorts) =>
        string.Join(",", sorts.Select(sort => $"{sort.SortBy}:{sort.SortOrder}"));

    private static MapDataQueryOptions CitySortQuery(
        int serverId,
        IReadOnlyList<MapDataSort> sorts)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            kind = "city",
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
                "Recovered City sort unexpectedly failed contract gate: " +
                string.Join(",", query.UnsupportedFeatures));
        return query;
    }

    private static IReadOnlyList<JsonElement> ReadAllCityRows(
        MapDataStore store,
        MapDataQueryOptions query,
        long sampledAt)
    {
        var rows = new List<JsonElement>();
        int page = 1;
        int expectedTotal = -1;
        while (true)
        {
            MapSearchResult result = store.SearchIndexedAtForTest(query with { Page = page }, sampledAt);
            if (expectedTotal < 0) expectedTotal = result.Total;
            rows.AddRange(result.Rows);
            if (rows.Count >= expectedTotal || result.Rows.Count == 0) break;
            page++;
        }
        if (rows.Count != expectedTotal)
            throw new InvalidDataException($"City sort proof observed {rows.Count}/{expectedTotal} rows.");
        return rows;
    }

    private static MapDataQueryOptions CityQuery(int serverId) => new(
        "city", serverId, 1, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, null, null, null, false, false, false,
        null, null, Array.Empty<string>());

    private static void TryDelete(string path)
    {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { }
        }
    }
}
