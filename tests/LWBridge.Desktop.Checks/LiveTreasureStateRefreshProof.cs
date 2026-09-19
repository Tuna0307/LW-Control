using System.Diagnostics;
using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveTreasureStateRefreshProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        string proofRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-treasure-state");
        Directory.CreateDirectory(proofRoot);
        string databasePath = Path.Combine(
            proofRoot, "treasure-state-" + Guid.NewGuid().ToString("N") + ".db");

        using var lifecycle = new OverviewLifecycleService(
            "treasure-state-refresh-proof",
            gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        string? instanceId = null;
        Exception? operationError = null;
        int originalServerId = 0;
        int targetServerId = 0;
        bool targetServerJumped = false;
        bool requireSupplies = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_REQUIRE_SUPPLIES"),
            "1",
            StringComparison.Ordinal);
        string? targetServerText =
            Environment.GetEnvironmentVariable("LWBRIDGE_SUPPLIES_TARGET_SERVER");
        if (!string.IsNullOrWhiteSpace(targetServerText) &&
            (!int.TryParse(targetServerText, out targetServerId) ||
             targetServerId is < 1 or > 99_999))
        {
            throw new InvalidDataException(
                "LWBRIDGE_SUPPLIES_TARGET_SERVER must be an integer from 1 to 99999.");
        }

        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startInstance = await lifecycle.InvokeAsync(
                "profile_instance_start",
                empty.RootElement,
                operationCts.Token).ConfigureAwait(false);
            JsonElement instanceJson =
                JsonSerializer.SerializeToElement(startInstance, JsonOptions.Default);
            instanceId = instanceJson.GetProperty("instanceId").GetString();

            using var store = new MapDataStore(databasePath);
            var service = new ManualMapScanCommandService(lifecycle, store);
            var source = new CurrentClientMapBlockSource(lifecycle);
            try
            {
                CurrentClientMapContext initialContext =
                    await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                originalServerId = initialContext.ServerId;
                if (targetServerId == 0) targetServerId = originalServerId;
                if (targetServerId != originalServerId)
                {
                    JsonElement jumpPayload = JsonSerializer.SerializeToElement(
                        new { serverId = targetServerId },
                        JsonOptions.Default);
                    JsonElement jumpResult = JsonSerializer.SerializeToElement(
                        await service.InvokeAsync(
                            "server_jump",
                            jumpPayload,
                            operationCts.Token).ConfigureAwait(false),
                        JsonOptions.Default);
                    if (!jumpResult.GetProperty("changed").GetBoolean())
                    {
                        throw new InvalidDataException(
                            "Supplies proof target server jump did not report a changed transition.");
                    }
                    targetServerJumped = true;
                    CurrentClientMapContext targetContext =
                        await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                    if (targetContext.ServerId != targetServerId)
                    {
                        throw new InvalidDataException(
                            "Supplies proof live context did not settle on the requested target server.");
                    }
                }

                JsonElement scanPayload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "treasure-state-refresh-proof",
                    selectedTypes = new[] { "treasure" },
                }, JsonOptions.Default);

                Stopwatch scanStopwatch = Stopwatch.StartNew();
                object? start = await service.InvokeAsync(
                    "map_scan_start",
                    scanPayload,
                    operationCts.Token).ConfigureAwait(false);
                JsonElement status =
                    JsonSerializer.SerializeToElement(start, JsonOptions.Default);
                int serverId = status.GetProperty("serverId").GetInt32();
                if (serverId <= 0 ||
                    status.GetProperty("totalBlocks").GetInt32() != 2500 ||
                    status.GetProperty("scanMode").GetString() != "fast" ||
                    status.GetProperty("concurrency").GetInt32() != 20 ||
                    status.GetProperty("scanStrategy").GetString() !=
                        MapScanStrategyPlanner.FastFullWorldStrategy)
                {
                    throw new InvalidDataException(
                        "Treasure state proof did not start with the expected automatic full-world strategy.");
                }

                int lastReportedBlocks = 0;
                DateTimeOffset scanDeadline = DateTimeOffset.UtcNow.AddMinutes(4);
                while (DateTimeOffset.UtcNow < scanDeadline)
                {
                    operationCts.Token.ThrowIfCancellationRequested();
                    status = JsonSerializer.SerializeToElement(
                        service.CreateStatus(),
                        JsonOptions.Default);
                    string phase = status.GetProperty("phase").GetString() ?? string.Empty;
                    int readBlocks = status.GetProperty("readBlocks").GetInt32();
                    if (readBlocks >= lastReportedBlocks + 500)
                    {
                        lastReportedBlocks = (readBlocks / 500) * 500;
                        Console.Error.WriteLine(
                            $"TREASURE_STATE_SCAN_PROGRESS blocks={readBlocks}/2500 phase={phase}");
                    }
                    if (phase == "completed") break;
                    if (phase == "error")
                    {
                        throw new InvalidDataException(
                            "Treasure state proof scan failed: " +
                            (status.TryGetProperty("lastError", out JsonElement error)
                                ? error.GetString()
                                : "unknown"));
                    }
                    await Task.Delay(100, operationCts.Token).ConfigureAwait(false);
                }
                scanStopwatch.Stop();

                status = JsonSerializer.SerializeToElement(
                    service.CreateStatus(),
                    JsonOptions.Default);
                if (status.GetProperty("phase").GetString() != "completed" ||
                    status.GetProperty("isReading").GetBoolean() ||
                    status.GetProperty("readBlocks").GetInt32() != 2500 ||
                    status.GetProperty("failedBlocks").GetInt32() != 0 ||
                    status.GetProperty("unreadBlocks").GetInt32() != 0)
                {
                    throw new InvalidDataException(
                        "Treasure state proof scan did not complete all 2500 blocks.");
                }

                IReadOnlyList<MapStoredRecord> published =
                    store.ReadRecords("treasure", serverId);
                if (published.Count <= 0)
                    throw new InvalidDataException(
                        "Treasure state proof requires at least one live Treasure row.");
                var suppliesUuids = published
                    .Where(IsSuppliesRecord)
                    .Where(row => !string.IsNullOrWhiteSpace(row.Uuid))
                    .Select(row => row.Uuid!)
                    .ToHashSet(StringComparer.Ordinal);
                int publishedSuppliesCount = suppliesUuids.Count;
                int publishedOrdinaryTreasureCount =
                    published.Count - publishedSuppliesCount;
                if (requireSupplies && publishedSuppliesCount <= 0)
                {
                    throw new InvalidDataException(
                        $"Supplies proof requires a positive live Supplies row; " +
                        $"published ordinary={publishedOrdinaryTreasureCount}, supplies=0.");
                }

                JsonElement claimStatusPayload = JsonSerializer.SerializeToElement(
                    new { profileId = "treasure-state-refresh-proof" },
                    JsonOptions.Default);
                Stopwatch statusStopwatch = Stopwatch.StartNew();
                JsonElement claimStatus = JsonSerializer.SerializeToElement(
                    await service.InvokeAsync(
                        "map_treasure_claim_status",
                        claimStatusPayload,
                        operationCts.Token).ConfigureAwait(false),
                    JsonOptions.Default);
                statusStopwatch.Stop();
                string playerUid = claimStatus.GetProperty("playerUid").GetString() ?? string.Empty;
                string allianceId = claimStatus.GetProperty("allianceId").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerUid) ||
                    claimStatus.GetProperty("states").GetArrayLength() != 0)
                {
                    throw new InvalidDataException(
                        "Treasure claim status did not return identity-only state.");
                }

                JsonElement refreshPayload = JsonSerializer.SerializeToElement(new
                {
                    profileId = "treasure-state-refresh-proof",
                    serverId,
                }, JsonOptions.Default);
                Stopwatch refreshStopwatch = Stopwatch.StartNew();
                JsonElement refresh = JsonSerializer.SerializeToElement(
                    await service.InvokeAsync(
                        "map_treasure_state_refresh_all",
                        refreshPayload,
                        operationCts.Token).ConfigureAwait(false),
                    JsonOptions.Default);
                refreshStopwatch.Stop();

                if (refresh.GetProperty("playerUid").GetString() != playerUid ||
                    refresh.GetProperty("allianceId").GetString() != allianceId)
                {
                    throw new InvalidDataException(
                        "Treasure refresh viewer identity changed between status and refresh-all.");
                }

                JsonElement states = refresh.GetProperty("states");
                if (states.ValueKind != JsonValueKind.Array ||
                    states.GetArrayLength() != published.Count)
                {
                    throw new InvalidDataException(
                        $"Treasure refresh returned {states.GetArrayLength()}/{published.Count} states.");
                }

                var publishedByUuid = published
                    .Where(row => !string.IsNullOrWhiteSpace(row.Uuid))
                    .ToDictionary(row => row.Uuid!, StringComparer.Ordinal);
                var observed = new HashSet<string>(StringComparer.Ordinal);
                var worldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var playerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                int cacheCount = 0;
                int chargePercentRows = 0;
                int remainingBoxesRows = 0;
                int rewardedCountRows = 0;
                int diggingCountRows = 0;
                int returnedSuppliesStateCount = 0;
                int suppliesChargePercentRows = 0;
                int suppliesRewardedCountRows = 0;
                int suppliesRemainingBoxesRows = 0;

                foreach (JsonElement state in states.EnumerateArray())
                {
                    string uuid = state.GetProperty("uuid").GetString() ?? string.Empty;
                    if (!publishedByUuid.ContainsKey(uuid) || !observed.Add(uuid))
                        throw new InvalidDataException(
                            "Treasure refresh returned an unknown or duplicate UUID.");
                    if (state.TryGetProperty("claimPriority", out _))
                        throw new InvalidDataException(
                            "Treasure refresh synthesized unrecovered claimPriority.");

                    string world = state.GetProperty("worldClaimState").GetString() ?? string.Empty;
                    string player = state.GetProperty("playerClaimState").GetString() ?? string.Empty;
                    if (world is not ("charging" or "claimable" or "depleted" or "expired" or "unknown") ||
                        player is not ("unclaimed" or "digging" or "claimed" or "unknown"))
                    {
                        throw new InvalidDataException(
                            "Treasure refresh returned state outside the recovered vocabulary.");
                    }
                    worldCounts[world] = worldCounts.GetValueOrDefault(world) + 1;
                    playerCounts[player] = playerCounts.GetValueOrDefault(player) + 1;
                    bool suppliesState = suppliesUuids.Contains(uuid);
                    if (suppliesState) returnedSuppliesStateCount++;
                    if (state.TryGetProperty("chargePercent", out JsonElement charge) &&
                        charge.ValueKind == JsonValueKind.Number)
                    {
                        chargePercentRows++;
                        if (suppliesState) suppliesChargePercentRows++;
                    }
                    if (state.TryGetProperty("remainingBoxes", out JsonElement remaining) &&
                        remaining.ValueKind == JsonValueKind.Number)
                    {
                        remainingBoxesRows++;
                        if (suppliesState) suppliesRemainingBoxesRows++;
                    }
                    if (state.TryGetProperty("rewardedCount", out JsonElement rewarded) &&
                        rewarded.ValueKind == JsonValueKind.Number)
                    {
                        rewardedCountRows++;
                        if (suppliesState) suppliesRewardedCountRows++;
                    }
                    if (state.TryGetProperty("diggingCount", out JsonElement digging) &&
                        digging.ValueKind == JsonValueKind.Number)
                        diggingCountRows++;

                    MapTreasureClaimState? cached =
                        store.ReadTreasureClaimStateForTest(serverId, playerUid, uuid);
                    if (cached is null)
                        throw new InvalidDataException(
                            "Treasure refresh state was not persisted in the recovered cache.");
                    cacheCount++;
                }
                if (requireSupplies &&
                    returnedSuppliesStateCount != publishedSuppliesCount)
                {
                    throw new InvalidDataException(
                        $"Supplies refresh returned {returnedSuppliesStateCount}/" +
                        $"{publishedSuppliesCount} Supplies states.");
                }

                MapSearchResult overlay = store.SearchIndexed(new MapDataQueryOptions(
                    "treasure",
                    serverId,
                    1,
                    MapDataQueryContract.RecoveredPageSize,
                    [new MapDataSort("updatedAt", "desc")],
                    false,
                    null,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    false,
                    false,
                    null,
                    null,
                    Array.Empty<string>(),
                    IncludeForeignRadarTreasures: true,
                    LuckyFirst: false,
                    ViewerUid: playerUid,
                    ViewerAllianceId: allianceId));
                int overlayStateRows = overlay.Rows.Count(row =>
                    row.TryGetProperty("worldClaimState", out JsonElement value) &&
                    value.ValueKind == JsonValueKind.String);
                if (overlay.Rows.Count > 0 && overlayStateRows != overlay.Rows.Count)
                    throw new InvalidDataException(
                        "Treasure search did not overlay cached state onto every first-page row.");

                string proofJson = JsonSerializer.Serialize(new
                {
                    ok = true,
                    proof = "current_v19_treasure_state_refresh_read_only",
                    serverId,
                    originalServerId,
                    targetServerId,
                    targetServerJumped,
                    requireSupplies,
                    publishedTreasureCount = published.Count,
                    publishedOrdinaryTreasureCount,
                    publishedSuppliesCount,
                    returnedStateCount = states.GetArrayLength(),
                    returnedSuppliesStateCount,
                    cacheCount,
                    playerUidPresent = !string.IsNullOrWhiteSpace(playerUid),
                    allianceIdPresent = !string.IsNullOrWhiteSpace(allianceId),
                    callerClaimActionInvoked = false,
                    callerScoutActionInvoked = false,
                    claimPrioritySynthesized = false,
                    scanMode = status.GetProperty("scanMode").GetString(),
                    scanStrategy = status.GetProperty("scanStrategy").GetString(),
                    concurrency = status.GetProperty("concurrency").GetInt32(),
                    scanWallSeconds = scanStopwatch.Elapsed.TotalSeconds,
                    claimStatusWallSeconds = statusStopwatch.Elapsed.TotalSeconds,
                    refreshAllWallSeconds = refreshStopwatch.Elapsed.TotalSeconds,
                    worldClaimStates = worldCounts,
                    playerClaimStates = playerCounts,
                    chargePercentRows,
                    remainingBoxesRows,
                    rewardedCountRows,
                    diggingCountRows,
                    suppliesChargePercentRows,
                    suppliesRemainingBoxesRows,
                    suppliesRewardedCountRows,
                    overlayFirstPageRows = overlay.Rows.Count,
                    overlayStateRows,
                }, JsonOptions.Default);
                File.WriteAllText(
                    Path.Combine(proofRoot, "last-proof.json"),
                    proofJson);
                Console.WriteLine(proofJson);
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
            if (targetServerJumped &&
                originalServerId > 0 &&
                lifecycle.GetReadyMapScanSession() is not null &&
                lifecycle.GetLiveServerId() is int liveServerId &&
                liveServerId != originalServerId)
            {
                try
                {
                    using MapDataStore returnStore = MapDataStore.CreateInMemory();
                    var returnService =
                        new ManualMapScanCommandService(lifecycle, returnStore);
                    try
                    {
                        JsonElement returnPayload = JsonSerializer.SerializeToElement(
                            new { serverId = originalServerId },
                            JsonOptions.Default);
                        JsonElement returnResult = JsonSerializer.SerializeToElement(
                            await returnService.InvokeAsync(
                                "server_jump",
                                returnPayload,
                                CancellationToken.None).ConfigureAwait(false),
                            JsonOptions.Default);
                        if (!returnResult.GetProperty("changed").GetBoolean())
                        {
                            throw new InvalidDataException(
                                "Supplies proof return jump did not report a changed transition.");
                        }
                        CurrentClientMapContext returnContext =
                            await new CurrentClientMapBlockSource(lifecycle)
                                .GetCurrentContextAsync(CancellationToken.None)
                                .ConfigureAwait(false);
                        if (returnContext.ServerId != originalServerId)
                        {
                            throw new InvalidDataException(
                                "Supplies proof live context did not return to the original server.");
                        }
                    }
                    finally
                    {
                        returnService.Close();
                    }
                }
                catch (Exception returnError)
                {
                    Console.Error.WriteLine(
                        "LIVE_TREASURE_STATE_RETURN_FAILED: " +
                        returnError.Message);
                    if (operationError is null) throw;
                }
            }

            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts =
                    new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
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
                        "LIVE_TREASURE_STATE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
            TryDelete(databasePath);
        }
    }

    private static bool IsSuppliesRecord(MapStoredRecord row)
    {
        using JsonDocument document = JsonDocument.Parse(row.DataJson);
        JsonElement root = document.RootElement;
        return root.TryGetProperty("pointType", out JsonElement pointType) &&
            pointType.TryGetInt32(out int parsedPointType) &&
            parsedPointType == 27 &&
            root.TryGetProperty("suppliesType", out JsonElement suppliesType) &&
            suppliesType.TryGetInt32(out int parsedSuppliesType) &&
            parsedSuppliesType > 0 &&
            root.TryGetProperty("treasureType", out JsonElement treasureType) &&
            treasureType.TryGetInt32(out int parsedTreasureType) &&
            parsedTreasureType == 0 &&
            root.TryGetProperty("runtimeClass", out JsonElement runtimeClass) &&
            runtimeClass.ValueKind == JsonValueKind.String &&
            (runtimeClass.GetString() ?? string.Empty)
                .EndsWith("WorldSuppliesPoint", StringComparison.Ordinal);
    }

    private static void TryDelete(string path)
    {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
            catch
            {
            }
        }
    }
}
