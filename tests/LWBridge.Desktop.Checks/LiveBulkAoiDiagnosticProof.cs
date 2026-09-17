using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class LiveBulkAoiDiagnosticProof
{
    internal static async Task RunAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("bulk-aoi-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startResult = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(startResult, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready bulk-AOI session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token)
                .ConfigureAwait(false);
            if (context.TileWidth != 1000 || context.TileHeight != 1000 || context.WorldId != 0)
                throw new InvalidDataException(
                    $"Bulk AOI proof expected normal 1000x1000 world, got world={context.WorldId}, size={context.TileWidth}x{context.TileHeight}.");
            string requestMode = ReadRequestMode();
            (int targetTileX, int targetTileY) = requestMode is "coverage" or "anchor" or "zoom"
                ? ReadCoverageTarget()
                : await AcquireLiveCityTargetAsync(source, context, operationCts.Token).ConfigureAwait(false);
            await ParkCameraAwayFromTargetAsync(
                source, session, context, targetTileX, targetTileY, operationCts.Token).ConfigureAwait(false);
            int holdMilliseconds = ReadHoldMilliseconds();
            int viewLevel = ReadViewLevel();
            int requestedCount = ReadRequestedCount();
            int repeatCount = ReadRepeatCount();
            int repeatDelayMilliseconds = ReadRepeatDelayMilliseconds();
            var results = new List<JsonElement>(repeatCount);
            for (int iteration = 0; iteration < repeatCount; iteration++)
            {
                if (iteration > 0)
                    if (repeatDelayMilliseconds > 0)
                        await Task.Delay(TimeSpan.FromMilliseconds(repeatDelayMilliseconds), operationCts.Token).ConfigureAwait(false);
                results.Add(await RunDiagnosticAsync(
                    session, context.ServerId, targetTileX, targetTileY,
                    holdMilliseconds, viewLevel, requestMode, requestedCount,
                    operationCts.Token).ConfigureAwait(false));
            }
            JsonElement result = results[^1];
            JsonElement geometry = requestMode == "anchor" ? JsonSerializer.SerializeToElement(new { skipped = true }, JsonOptions.Default) : await RunAoiGeometryDiagnosticAsync(
                session, operationCts.Token).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_private_bulk_aoi_request",
                sessionId = session.SessionId,
                gamePid = session.GamePid,
                bulkAoi = result,
                bulkAoiRuns = results,
                aoiGeometry = geometry,
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
                    Console.Error.WriteLine("LIVE_BULK_AOI_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    internal static async Task RunFullCoverageAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("full-map-coverage-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            object? startResult = await lifecycle.InvokeAsync(
                "profile_instance_start", empty.RootElement, operationCts.Token).ConfigureAwait(false);
            JsonElement startJson = JsonSerializer.SerializeToElement(startResult, JsonOptions.Default);
            instanceId = startJson.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready full-map session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token)
                .ConfigureAwait(false);
            if (context.TileWidth != 1000 || context.TileHeight != 1000 || context.WorldId != 0)
                throw new InvalidDataException(
                    $"Full-map coverage proof expected normal 1000x1000 world, got world={context.WorldId}, size={context.TileWidth}x{context.TileHeight}.");

            await ParkCameraAwayFromTargetAsync(
                source, session, context, 15, 75, operationCts.Token).ConfigureAwait(false);

            var covered = new HashSet<int>();
            var responseSeconds = new List<double>();
            var cityRecords = new Dictionary<string, FirstLivePreparedResource>(StringComparer.Ordinal);
            int primaryRequests = 0;
            int cleanupRequests = 0;
            long startedAt = Environment.TickCount64;

            async Task CaptureCoverageAsync(int targetX, int targetY, bool cleanup)
            {
                JsonElement result = default;
                Exception? lastError = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        result = await RunDiagnosticAsync(
                            session, context.ServerId, targetX, targetY, 0, -1,
                            "coverage", 8, operationCts.Token).ConfigureAwait(false);
                        lastError = null;
                        break;
                    }
                    catch (Exception error) when (error is TimeoutException or InvalidDataException)
                    {
                        lastError = error;
                        Console.Error.WriteLine(
                            $"FULL_MAP_COVERAGE_RETRY target={targetX},{targetY} attempt={attempt} reason={error.GetType().Name}");
                        if (attempt < 3)
                            await Task.Delay(TimeSpan.FromMilliseconds(150), operationCts.Token).ConfigureAwait(false);
                    }
                }
                if (lastError is not null) throw lastError;
                if (!result.TryGetProperty("responseFlagsTransitioned", out JsonElement flags) || flags.ValueKind != JsonValueKind.True)
                    throw new InvalidDataException("Coverage request completed without native response-flag transition.");
                if (!result.TryGetProperty("requestedIndices", out JsonElement indices) || indices.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Coverage request did not expose native requested AOI indices.");
                int before = covered.Count;
                foreach (JsonElement item in indices.EnumerateArray())
                {
                    if (!item.TryGetInt32(out int index) || index < 0 || index >= 10000)
                        throw new InvalidDataException("Coverage request returned an AOI index outside 0..9999.");
                    covered.Add(index);
                }
                if (cleanup && covered.Count == before)
                    throw new InvalidDataException($"Cleanup target ({targetX},{targetY}) added no new AOI coverage.");
                if (result.TryGetProperty("point_records", out JsonElement pointRecords) &&
                    pointRecords.ValueKind == JsonValueKind.Array && pointRecords.GetArrayLength() > 0)
                {
                    byte[] snapshotBytes = System.Text.Encoding.UTF8.GetBytes(result.GetRawText());
                    string sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "LWBridgeRebuild", "live-resource", "bulk-aoi-diagnostic-result.json");
                    foreach (FirstLivePreparedResource prepared in FirstLiveResultImporter.PrepareCitySnapshot(snapshotBytes, sourcePath))
                    {
                        if (prepared.Import.ServerId != context.ServerId)
                            throw new InvalidDataException("Full-map City snapshot contained a different server.");
                        if (!cityRecords.TryGetValue(prepared.Record.RecordKey, out FirstLivePreparedResource? prior) ||
                            prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                            cityRecords[prepared.Record.RecordKey] = prepared;
                    }
                }
                if (result.TryGetProperty("elapsedSeconds", out JsonElement elapsed) && elapsed.TryGetDouble(out double seconds))
                    responseSeconds.Add(seconds);
                if (cleanup) cleanupRequests++; else primaryRequests++;
                int completed = primaryRequests + cleanupRequests;
                if (completed % 25 == 0)
                    Console.Error.WriteLine($"FULL_MAP_COVERAGE_PROGRESS requests={completed} covered={covered.Count}/10000");
            }

            for (int row = 0; row < 10; row++)
            {
                int targetY = 75 + (row * 100);
                for (int column = 0; column < 25; column++)
                {
                    int targetX = 15 + (column * 40);
                    await CaptureCoverageAsync(targetX, targetY, cleanup: false).ConfigureAwait(false);
                }
            }

            int cleanupGuard = 0;
            while (covered.Count < 10000)
            {
                int missing = Enumerable.Range(0, 10000).First(index => !covered.Contains(index));
                int cellX = missing % 100;
                int cellY = missing / 100;
                await CaptureCoverageAsync((cellX * 10) + 5, (cellY * 10) + 5, cleanup: true)
                    .ConfigureAwait(false);
                cleanupGuard++;
                if (cleanupGuard > 500)
                    throw new InvalidDataException($"Full-map coverage stalled at {covered.Count}/10000 AOI cells.");
            }

            double wallSeconds = (Environment.TickCount64 - startedAt) / 1000d;
            double meanResponseSeconds = responseSeconds.Count == 0 ? 0 : responseSeconds.Average();
            double maxResponseSeconds = responseSeconds.Count == 0 ? 0 : responseSeconds.Max();

            IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(context.TileWidth, context.TileHeight);
            string scanRunId = "full_city_" + Guid.NewGuid().ToString("N");
            var scanRequest = new MapScanExecutionRequest(scanRunId, context.ServerId, context.WorldId,
                context.TileWidth, context.TileHeight, ["city"], 8, 3);
            string proofRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LWBridgeRebuild", "live-resource");
            Directory.CreateDirectory(proofRoot);
            string proofDbPath = Path.Combine(proofRoot, scanRunId + ".db");
            int publishedCityCount;
            int reopenedCityCount;
            long publishStartedAt = Environment.TickCount64;
            try
            {
                using (var store = new MapDataStore(proofDbPath))
                {
                    var sink = new MapDataStoreScanSink(store);
                    long now = RecoveredWallClock.UnixTimeMilliseconds();
                    sink.Begin(scanRequest, logicalBlocks.Count, now);
                    var recordsByBlock = cityRecords.Values
                        .GroupBy(item => (item.Import.Y / 20) * 50 + (item.Import.X / 20))
                        .ToDictionary(group => group.Key, group => (IReadOnlyList<MapStoredRecord>)group.Select(item => item.Record).ToArray());
                    foreach (MapScanTargetBlock block in logicalBlocks)
                    {
                        IReadOnlyList<MapStoredRecord> records = recordsByBlock.TryGetValue(block.BlockIndex, out IReadOnlyList<MapStoredRecord>? value)
                            ? value : Array.Empty<MapStoredRecord>();
                        string payload = JsonSerializer.Serialize(new
                        {
                            protocol = "current_fast_full_city_block_v1",
                            blockIndex = block.BlockIndex,
                            coverage = "all_four_lod0_aoi_cells_proven_by_full_map_union",
                            recordsInBlock = records.Count,
                        }, JsonOptions.Default);
                        sink.CheckpointSuccess(scanRequest, block,
                            new MapScanBlockCapture(context.ServerId, context.WorldId, block.BlockIndex, payload, records),
                            1, RecoveredWallClock.UnixTimeMilliseconds());
                    }
                    sink.Publish(scanRequest, RecoveredWallClock.UnixTimeMilliseconds());
                    publishedCityCount = store.SearchIndexed(DefaultCityQuery(context.ServerId)).Total;
                }
                using (var reopened = new MapDataStore(proofDbPath))
                    reopenedCityCount = reopened.SearchIndexed(DefaultCityQuery(context.ServerId)).Total;
                if (reopenedCityCount != publishedCityCount || publishedCityCount != cityRecords.Count)
                    throw new InvalidDataException("Published/reopened City count did not match the deduplicated full-map capture.");
            }
            finally
            {
                TryDeleteProofDatabase(proofDbPath);
            }
            double publicationSeconds = (Environment.TickCount64 - publishStartedAt) / 1000d;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_full_map_aoi_coverage",
                coveredAoiCells = covered.Count,
                totalAoiCells = 10000,
                primaryRequests,
                cleanupRequests,
                totalRequests = primaryRequests + cleanupRequests,
                capturedCityCount = cityRecords.Count,
                publishedCityCount,
                reopenedCityCount,
                logicalBlocks = logicalBlocks.Count,
                wallSeconds,
                publicationSeconds,
                meanResponseSeconds,
                maxResponseSeconds,
                cameraRestoration = "per-request-next-tick",
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
                    Console.Error.WriteLine("LIVE_FULL_MAP_COVERAGE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    private static MapDataQueryOptions DefaultCityQuery(int serverId) => new(
        "city", serverId, 1, MapDataQueryContract.RecoveredPageSize,
        [new MapDataSort("updatedAt", "desc")], false, null, null, false,
        null, null, null, null, null, null, null, false, false, false,
        null, null, Array.Empty<string>());

    private static void TryDeleteProofDatabase(string path)
    {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { }
        }
    }

    private static async Task<(int X, int Y)> AcquireLiveCityTargetAsync(
        CurrentClientMapBlockSource source,
        CurrentClientMapContext context,
        CancellationToken cancellationToken)
    {
        int seedX = context.PlayerTileX ?? 495;
        int seedY = context.PlayerTileY ?? 40;
        MapScanTargetBlock block = MapScanTraversal.Build(context.TileWidth, context.TileHeight)
            .Single(candidate => candidate.MinX <= seedX && candidate.MaxX >= seedX &&
                                 candidate.MinY <= seedY && candidate.MaxY >= seedY);
        var request = new MapScanExecutionRequest(
            "bulk_aoi_target_city_probe", context.ServerId, context.WorldId,
            context.TileWidth, context.TileHeight, ["city"], 1, 1);
        MapScanBlockCapture capture = await source.CaptureAsync(request, block, cancellationToken)
            .ConfigureAwait(false);
        foreach (MapStoredRecord record in capture.Records)
        {
            using JsonDocument document = JsonDocument.Parse(record.DataJson);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("x", out JsonElement xValue) && xValue.TryGetInt32(out int x) &&
                root.TryGetProperty("y", out JsonElement yValue) && yValue.TryGetInt32(out int y) &&
                x >= 0 && x < context.TileWidth && y >= 0 && y < context.TileHeight)
                return (x, y);
        }
        throw new InvalidDataException("Bounded live City capture returned no usable current city coordinate.");
    }

    private static async Task ParkCameraAwayFromTargetAsync(
        CurrentClientMapBlockSource source,
        OverviewMapScanSession session,
        CurrentClientMapContext context,
        int cityX,
        int cityY,
        CancellationToken cancellationToken)
    {
        int targetX = cityX < 500 ? 850 : 150;
        int targetY = cityY < 500 ? 850 : 150;
        var method = typeof(CurrentClientMapBlockSource).GetMethod(
            "NavigateAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException("CurrentClientMapBlockSource.NavigateAsync");
        object? invocation = method.Invoke(source, [
            session, context.ServerId, context.WorldId, targetX, targetY, cancellationToken]);
        if (invocation is not Task task)
            throw new InvalidOperationException("Private navigation proof did not return a Task.");
        await task.ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonElement> RunDiagnosticAsync(
        OverviewMapScanSession session,
        int serverId,
        int targetTileX,
        int targetTileY,
        int holdMilliseconds,
        int viewLevel,
        string requestMode,
        int requestedCount,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(root);
        string requestId = "bulk" + Guid.NewGuid().ToString("N");
        string commandPath = Path.Combine(root, "bulk-aoi-diagnostic.txt");
        string resultPath = Path.Combine(root, "bulk-aoi-diagnostic-result.json");
        string temporaryPath = commandPath + ".tmp-" + Guid.NewGuid().ToString("N");
        string command = string.Join('\n', new[]
        {
            "schema=1",
            "probeVersion=lwbridge-live-resource-probe-2",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid}",
            $"serverId={serverId}",
            "scanRunId=bulk-aoi-live-proof",
            $"viewLevel={viewLevel}",
            $"requestMode={requestMode}",
            $"targetTileX={targetTileX}",
            $"targetTileY={targetTileY}",
            $"requestedCount={requestedCount}",
            $"holdMilliseconds={holdMilliseconds}",
            "includeMonster=true",
            string.Empty,
        });
        try
        {
            await File.WriteAllTextAsync(temporaryPath, command, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, commandPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(resultPath, cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(bytes);
                JsonElement value = document.RootElement;
                if (ReadString(value, "requestId") != requestId)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (RequireInt(value, "schemaVersion") != 1 ||
                    ReadString(value, "probeVersion") != "lwbridge-live-resource-probe-2" ||
                    ReadString(value, "profileId") != session.ProfileId ||
                    ReadString(value, "launchSessionId") != session.SessionId ||
                    ReadString(value, "challenge") != session.Challenge ||
                    RequireInt(value, "gamePid") != session.GamePid ||
                    RequireInt(value, "requestedCount") != requestedCount ||
                    ReadString(value, "requestMode") != requestMode)
                {
                    throw new InvalidDataException("Bulk AOI diagnostic did not match the owned game session.");
                }
                if (ReadString(value, "state") != "proven")
                    throw new InvalidDataException("Bulk AOI diagnostic failed: " + value.GetRawText());
                if (RequireInt(value, "holdMilliseconds") != holdMilliseconds ||
                    RequireInt(value, "viewLevel") != viewLevel)
                    throw new InvalidDataException("Bulk AOI diagnostic result parameters did not match the request.");
                return value.Clone();
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
            catch (JsonException) { }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Bulk AOI diagnostic did not return a correlated result.");
    }

    private static async Task<JsonElement> RunAoiGeometryDiagnosticAsync(
        OverviewMapScanSession session, CancellationToken cancellationToken)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-resource");
        string requestId = "aoi" + Guid.NewGuid().ToString("N");
        string commandPath = Path.Combine(root, "aoi-diagnostic.txt");
        string resultPath = Path.Combine(root, "aoi-diagnostic-result.json");
        string command = string.Join('\n', new[]
        {
            "schema=1", "probeVersion=lwbridge-live-resource-probe-2",
            $"requestId={requestId}", $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}", $"challenge={session.Challenge}",
            $"gamePid={session.GamePid}", "cellX=0", "cellY=0", "tileCount=1000", string.Empty,
        });
        await File.WriteAllTextAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    await File.ReadAllBytesAsync(resultPath, cancellationToken).ConfigureAwait(false));
                JsonElement value = document.RootElement;
                if (ReadString(value, "requestId") == requestId)
                {
                    if (ReadString(value, "state") != "proven")
                        throw new InvalidDataException("AOI geometry diagnostic failed: " + value.GetRawText());
                    return value.Clone();
                }
            }
            catch (FileNotFoundException) { } catch (IOException) { } catch (JsonException) { }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("AOI geometry diagnostic did not return a correlated result.");
    }

    private static string ReadRequestMode()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_REQUEST_MODE");
        if (string.IsNullOrWhiteSpace(raw)) return "native";
        string value = raw.Trim().ToLowerInvariant();
        if (value is not ("native" or "direct" or "expanded" or "coverage" or "anchor" or "edge" or "zoom"))
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_REQUEST_MODE must be native, direct, expanded, coverage, anchor, edge, or zoom.");
        return value;
    }

    private static (int X, int Y) ReadCoverageTarget()
    {
        string? rawX = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TARGET_X");
        string? rawY = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TARGET_Y");
        if (!int.TryParse(rawX, out int x) || !int.TryParse(rawY, out int y) ||
            x < 0 || x >= 1000 || y < 0 || y >= 1000)
            throw new InvalidDataException("Coverage mode requires LWBRIDGE_BULK_AOI_TARGET_X/Y in 0..999.");
        return (x, y);
    }

    private static int ReadRequestedCount()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_REQUEST_COUNT");
        if (string.IsNullOrWhiteSpace(raw)) return 8;
        if (!int.TryParse(raw, out int value) || value < 1 || value > 160)
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_REQUEST_COUNT must be an integer from 1 through 160.");
        return value;
    }

    private static int ReadRepeatDelayMilliseconds()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_REPEAT_DELAY_MS");
        if (string.IsNullOrWhiteSpace(raw)) return 350;
        if (!int.TryParse(raw, out int value) || value < 0 || value > 2000)
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_REPEAT_DELAY_MS must be an integer from 0 through 2000.");
        return value;
    }

    private static int ReadRepeatCount()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_REPEAT");
        if (string.IsNullOrWhiteSpace(raw)) return 1;
        if (!int.TryParse(raw, out int value) || value < 1 || value > 20)
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_REPEAT must be an integer from 1 through 20.");
        return value;
    }

    private static int ReadViewLevel()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_VIEW_LEVEL");
        if (string.IsNullOrWhiteSpace(raw)) return -1;
        if (!int.TryParse(raw, out int value) || value < -1 || value > 2)
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_VIEW_LEVEL must be -1, 0, 1, or 2.");
        return value;
    }

    private static int ReadHoldMilliseconds()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_HOLD_MS");
        if (string.IsNullOrWhiteSpace(raw)) return 1000;
        if (!int.TryParse(raw, out int value) || value < 0 || value > 2000)
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_HOLD_MS must be an integer from 0 through 2000.");
        return value;
    }

    private static int RequireInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : throw new InvalidDataException($"Bulk AOI diagnostic field '{name}' is missing or invalid.");

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
