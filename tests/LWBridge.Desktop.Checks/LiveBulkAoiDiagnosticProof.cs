using System.Diagnostics;
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
            int homeServerId = context.ServerId;
            string? requestedServer = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_SERVER_ID");
            if (int.TryParse(requestedServer, out int targetServerId) &&
                targetServerId is >= 1 and <= 99999 &&
                targetServerId != context.ServerId)
            {
                await source.JumpToServerAsync(targetServerId, operationCts.Token).ConfigureAwait(false);
                context = await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
                if (context.ServerId != targetServerId)
                    throw new InvalidDataException(
                        $"Bulk AOI proof did not settle on requested server {targetServerId}.");
            }
            if (context.TileWidth != 1000 || context.TileHeight != 1000 || context.WorldId != 0)
                throw new InvalidDataException(
                    $"Bulk AOI proof expected normal 1000x1000 world, got world={context.WorldId}, size={context.TileWidth}x{context.TileHeight}.");
            string requestMode = ReadRequestMode();
            bool explicitTarget = HasCoverageTarget();
            int diagnosticServerId = context.ServerId;
            bool targetProvenUnloaded = false;
            (int targetTileX, int targetTileY) target;
            bool primeAtPlayerTile = requestMode == "messagebulk" &&
                string.Equals(
                    Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_PRIME_PLAYER_TILE"),
                    "true", StringComparison.OrdinalIgnoreCase);
            if (primeAtPlayerTile && context.PlayerTileX is int playerTileX && context.PlayerTileY is int playerTileY)
            {
                target = (playerTileX, playerTileY);
            }
            else if (requestMode is "message" or "messagebulk" && ReadIncludeDispatch())
            {
                CurrentClientDispatchNearestResult nearest =
                    await source.FindNearestDispatchAsync(operationCts.Token).ConfigureAwait(false);
                diagnosticServerId = nearest.ServerId;
                target = (nearest.X, nearest.Y);
                targetProvenUnloaded = !nearest.PointDataResolved;
            }
            else
            {
                target =
                    requestMode is "coverage" or "anchor" or "browser" or "zoom" or "expanded" or "edge" ||
                    requestMode is "native" or "direct" or "message" or "messagebulk" && explicitTarget
                        ? ReadCoverageTarget()
                        : await AcquireLiveCityTargetAsync(source, context, operationCts.Token).ConfigureAwait(false);
            }
            (int targetTileX, int targetTileY) = target;
            if (!targetProvenUnloaded && requestMode != "messagebulk")
            {
                await ParkCameraAwayFromTargetAsync(
                    source, session, context, targetTileX, targetTileY, operationCts.Token).ConfigureAwait(false);
            }
            int holdMilliseconds = ReadHoldMilliseconds();
            int viewLevel = ReadViewLevel();
            int requestedCount = ReadRequestedCount();
            IReadOnlyList<int>? explicitIndices = ReadExplicitIndices(requestedCount);
            int repeatCount = ReadRepeatCount();
            int repeatDelayMilliseconds = ReadRepeatDelayMilliseconds();
            bool reusePrimeContextAfterFirst = ReadReusePrimeContextAfterFirst();
            var results = new List<JsonElement>(repeatCount);
            var chainedRequestedIndices = new HashSet<int>();
            for (int iteration = 0; iteration < repeatCount; iteration++)
            {
                if (iteration > 0)
                    if (repeatDelayMilliseconds > 0)
                        await Task.Delay(TimeSpan.FromMilliseconds(repeatDelayMilliseconds), operationCts.Token).ConfigureAwait(false);

                int iterationTargetX = targetTileX;
                int iterationTargetY = targetTileY;
                bool reusePrimeContext = reusePrimeContextAfterFirst && iteration > 0;
                if (reusePrimeContextAfterFirst && explicitIndices is null && requestMode == "messagebulk")
                {
                    iterationTargetX = 50 + ((iteration % 5) * 200);
                    iterationTargetY = 50 + (((iteration / 5) % 4) * 200);
                }

                JsonElement run = await RunDiagnosticAsync(
                    session, diagnosticServerId, iterationTargetX, iterationTargetY,
                    holdMilliseconds, viewLevel, requestMode, requestedCount,
                    operationCts.Token, explicitIndices,
                    reusePrimeContext: reusePrimeContext).ConfigureAwait(false);

                if (reusePrimeContextAfterFirst && explicitIndices is null && requestMode == "messagebulk")
                {
                    if (!run.TryGetProperty("requestedIndices", out JsonElement runIndices) ||
                        runIndices.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException(
                            $"Chained message-bulk run {iteration + 1} did not expose requested AOI indices.");
                    foreach (JsonElement item in runIndices.EnumerateArray())
                    {
                        if (!item.TryGetInt32(out int index) || index < 0 || index >= 10000)
                            throw new InvalidDataException(
                                $"Chained message-bulk run {iteration + 1} returned an invalid AOI index.");
                        if (!chainedRequestedIndices.Add(index))
                            throw new InvalidDataException(
                                $"Chained message-bulk run {iteration + 1} re-requested AOI {index}.");
                    }
                }

                results.Add(run);
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
                homeServerId,
                observedServerId = context.ServerId,
                chainedUniqueRequestedAoiCount = chainedRequestedIndices.Count,
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

    internal static void RunMessageBulkSweepPlanningChecks()
    {
        foreach (int batchSize in new[] { 32, 64, 96, 128, 144, 160 })
        {
            int[] retained = Enumerable.Range(5000, 16).ToArray();
            int[] bootstrap = Enumerable.Range(0, batchSize).ToArray();
            var covered = new HashSet<int>(retained);
            foreach (int index in bootstrap)
                if (!covered.Add(index))
                    throw new InvalidDataException(
                        $"Message-bulk {batchSize} planning bootstrap overlapped retained context.");

            int remainingCount = 10000 - covered.Count;
            int expectedBatchCount = (remainingCount + batchSize - 1) / batchSize;
            IReadOnlyList<int[]> batches = BuildMessageBulkSweepBatches(covered, batchSize);
            if (batches.Count != expectedBatchCount ||
                batches.Any(batch => batch.Length < 1 || batch.Length > batchSize) ||
                batches.Take(Math.Max(0, batches.Count - 1)).Any(batch => batch.Length != batchSize))
            {
                throw new InvalidDataException(
                    $"Message-bulk {batchSize} planner expected {expectedBatchCount} bounded remaining batches, got {batches.Count}.");
            }

            foreach (int[] batch in batches)
                foreach (int index in batch)
                    if (!covered.Add(index))
                        throw new InvalidDataException(
                            $"Message-bulk {batchSize} planner duplicated AOI {index}.");

            if (covered.Count != 10000 || !covered.SetEquals(Enumerable.Range(0, 10000)))
                throw new InvalidDataException(
                    $"Message-bulk {batchSize} planner covered {covered.Count}/10000 AOIs.");
        }

        int[] nativeXStarts =
            NativeBrowserWindowStartsForProof(100, 14);
        int[] nativeYStarts =
            NativeBrowserWindowStartsForProof(100, 8);
        if (nativeXStarts.Length != 8 || nativeYStarts.Length != 13)
            throw new InvalidDataException(
                $"Native browser planner expected 8x13 starts, got {nativeXStarts.Length}x{nativeYStarts.Length}.");

        var nativeCovered = new HashSet<int>();
        int nativeWindowCount = 0;
        foreach (int startY in nativeYStarts)
        {
            foreach (int startX in nativeXStarts)
            {
                int[] core =
                    NativeBrowserCoreIndicesForProof(startX, startY);
                if (core.Length != 112 ||
                    core.Any(index => index < 0 || index >= 10000))
                    throw new InvalidDataException(
                        $"Native browser core ({startX},{startY}) was invalid.");
                nativeCovered.UnionWith(core);
                nativeWindowCount++;
            }
        }

        if (nativeWindowCount != 104 ||
            nativeCovered.Count != 10000 ||
            !nativeCovered.SetEquals(Enumerable.Range(0, 10000)))
            throw new InvalidDataException(
                $"Native browser planner covered {nativeCovered.Count}/10000 AOIs across {nativeWindowCount} windows.");
    }

    internal static async Task RunMessageBulkBatchBenchmarkAsync()
    {
        int[] candidateSizes = [64, 96, 128, 144, 160];
        const int repeatsPerSize = 3;
        const int bootstrapSize = 32;
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService(
            "messagebulk-batch-benchmark-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        string? instanceId = null;
        Exception? operationError = null;
        try
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(
                await lifecycle.InvokeAsync(
                    "profile_instance_start", empty.RootElement, operationCts.Token)
                    .ConfigureAwait(false),
                JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException(
                    "Production lifecycle did not expose a ready message-bulk benchmark session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context =
                await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            if (context.TileWidth != 1000 || context.TileHeight != 1000 ||
                context.WorldId != 0)
                throw new InvalidDataException(
                    $"Message-bulk benchmark expected normal 1000x1000 world, got world={context.WorldId}, size={context.TileWidth}x{context.TileHeight}.");

            int physicalServerBefore = lifecycle.GetLiveServerId() ?? 0;
            int primeX = context.PlayerTileX ?? 500;
            int primeY = context.PlayerTileY ?? 500;
            JsonElement bootstrap = await RunDiagnosticAsync(
                session, context.ServerId, primeX, primeY,
                holdMilliseconds: 0, viewLevel: 0, requestMode: "messagebulk",
                requestedCount: bootstrapSize, cancellationToken: operationCts.Token,
                explicitIndices: null, reusePrimeContext: false,
                allowCachedExplicitIndices: false,
                primeNearCameraOverride: true,
                primeCurrentTileOverride: false,
                targetTotalViewCountOverride: 0,
                includeCityOverride: true,
                includeMonsterOverride: false,
                includeDispatchOverride: true).ConfigureAwait(false);

            int[] bootstrapCoverage =
                ReadSweepIndexArray(bootstrap, "coverageIndices", 16 + bootstrapSize);
            var unavailable = bootstrapCoverage.ToHashSet();
            var measurements = new List<object>();
            int stableMax = 0;

            foreach (int batchSize in candidateSizes)
            {
                int successes = 0;
                for (int repeat = 1; repeat <= repeatsPerSize; repeat++)
                {
                    int[] batch = Enumerable.Range(0, 10000)
                        .Where(index => !unavailable.Contains(index))
                        .Take(batchSize)
                        .ToArray();
                    if (batch.Length != batchSize)
                        throw new InvalidDataException(
                            $"Message-bulk benchmark ran out of disjoint AOIs for batch size {batchSize}.");
                    foreach (int index in batch) unavailable.Add(index);

                    (int targetX, int targetY) = SweepBatchCenter(batch);
                    Stopwatch wall = Stopwatch.StartNew();
                    try
                    {
                        JsonElement result = await RunDiagnosticAsync(
                            session, context.ServerId, targetX, targetY,
                            holdMilliseconds: 0, viewLevel: 0, requestMode: "messagebulk",
                            requestedCount: batchSize, cancellationToken: operationCts.Token,
                            explicitIndices: batch, reusePrimeContext: true,
                            allowCachedExplicitIndices: true,
                            primeNearCameraOverride: false,
                            primeCurrentTileOverride: true,
                            targetTotalViewCountOverride: 0,
                            includeCityOverride: true,
                            includeMonsterOverride: false,
                            includeDispatchOverride: true).ConfigureAwait(false);
                        wall.Stop();

                        int[] coverage = ReadSweepIndexArray(
                            result, "coverageIndices", batchSize);
                        if (!coverage.ToHashSet().SetEquals(batch))
                            throw new InvalidDataException(
                                $"Message-bulk benchmark {batchSize} coverage differed from its explicit request.");

                        successes++;
                        measurements.Add(new
                        {
                            batchSize,
                            repeat,
                            ok = true,
                            wallSeconds = wall.Elapsed.TotalSeconds,
                            sourceElapsedSeconds =
                                result.TryGetProperty("elapsedSeconds", out JsonElement elapsed) &&
                                elapsed.TryGetDouble(out double sourceElapsed)
                                    ? sourceElapsed : (double?)null,
                            matchedCount =
                                result.TryGetProperty("matchedCount", out JsonElement matched) &&
                                matched.TryGetInt32(out int matchedCount)
                                    ? matchedCount : (int?)null,
                            requestMethod = ReadString(result, "requestMethod"),
                            responseFlagsTransitioned =
                                result.TryGetProperty("responseFlagsTransitioned", out JsonElement transitioned) &&
                                transitioned.ValueKind == JsonValueKind.True,
                            visualCameraPositionStable =
                                result.TryGetProperty("visualCameraPositionStable", out JsonElement visualStable) &&
                                visualStable.ValueKind == JsonValueKind.True,
                        });
                    }
                    catch (Exception error) when (
                        error is not OperationCanceledException)
                    {
                        wall.Stop();
                        measurements.Add(new
                        {
                            batchSize,
                            repeat,
                            ok = false,
                            wallSeconds = wall.Elapsed.TotalSeconds,
                            error = error.Message,
                        });
                        break;
                    }
                }

                if (successes == repeatsPerSize)
                    stableMax = batchSize;
            }

            int physicalServerAfter = lifecycle.GetLiveServerId() ?? 0;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_messagebulk_batch_ceiling",
                sessionId = session.SessionId,
                serverId = context.ServerId,
                candidateSizes,
                repeatsPerSize,
                bootstrapSize,
                bootstrapCoverageCount = bootstrapCoverage.Length,
                stableMaxBatchSize = stableMax,
                measurements,
                physicalServerBefore,
                physicalServerAfter,
                physicalServerUnchanged =
                    physicalServerBefore == context.ServerId &&
                    physicalServerAfter == context.ServerId,
                cameraPolicy = "one local prime; reused-prime disjoint explicit AOI batches",
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
                using var stopCts =
                    new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine(
                        "LIVE_MESSAGEBULK_BENCHMARK_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    internal static async Task RunCoverageGeometrySweepAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("coverage-geometry-sweep-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        string? instanceId = null;
        Exception? operationError = null;
        string? oldFov = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV");
        string? oldAspect = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT");
        string? oldCameraY = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y");
        try
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV", "175");
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT", "12");
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y", "200");
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(
                await lifecycle.InvokeAsync("profile_instance_start", empty.RootElement, operationCts.Token)
                    .ConfigureAwait(false), JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready coverage geometry session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context =
                await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            await ParkCameraAwayFromTargetAsync(
                source, session, context, 500, 500, operationCts.Token).ConfigureAwait(false);
            (int X, int Y)[] samples = [];
            var measurements = new List<object>();
            foreach ((int x, int y) in samples)
            {
                JsonElement result = await RunDiagnosticAsync(
                    session, context.ServerId, x, y,
                    holdMilliseconds: 0, viewLevel: -1, requestMode: "coverage",
                    requestedCount: 8, cancellationToken: operationCts.Token,
                    includeCityOverride: true, includeMonsterOverride: false,
                    includeDispatchOverride: true).ConfigureAwait(false);
                int[] indices = result.GetProperty("coverageIndices")
                    .EnumerateArray().Select(value => value.GetInt32()).ToArray();
                int minCellX = indices.Min(index => index % 100);
                int maxCellX = indices.Max(index => index % 100);
                int minCellY = indices.Min(index => index / 100);
                int maxCellY = indices.Max(index => index / 100);
                measurements.Add(new
                {
                    targetX = x,
                    targetY = y,
                    count = indices.Length,
                    minCellX,
                    maxCellX,
                    minCellY,
                    maxCellY,
                    width = maxCellX - minCellX + 1,
                    height = maxCellY - minCellY + 1,
                    elapsedSeconds = result.GetProperty("elapsedSeconds").GetDouble(),
                    nativeRemoteTileX = RequireInt(result, "nativeRemoteTileX"),
                    nativeRemoteTileY = RequireInt(result, "nativeRemoteTileY"),
                    cameraTileStable =
                        result.TryGetProperty("cameraTileStable", out JsonElement stable) &&
                        stable.ValueKind == JsonValueKind.True,
                    responseFlagsTransitioned =
                        result.TryGetProperty("responseFlagsTransitioned", out JsonElement transitioned) &&
                        transitioned.ValueKind == JsonValueKind.True,
                });
                await Task.Delay(1000, operationCts.Token).ConfigureAwait(false);
            }
            var cameraMeasurements = new List<object>();
            (int Aspect, int X, int Y)[] aspectSamples =
            [
                (4, 300, 500),
                (6, 360, 500),
                (8, 420, 500),
                (10, 480, 500),
                (12, 540, 500),
                (14, 600, 500),
                (16, 660, 500),
            ];
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y", "220");
            foreach ((int aspect, int x, int y) in aspectSamples)
            {
                Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT", aspect.ToString());
                try
                {
                    JsonElement result = await RunDiagnosticAsync(
                        session, context.ServerId, x, y,
                        holdMilliseconds: 0, viewLevel: -1, requestMode: "coverage",
                        requestedCount: 8, cancellationToken: operationCts.Token,
                        includeCityOverride: true, includeMonsterOverride: false,
                        includeDispatchOverride: true).ConfigureAwait(false);
                    int[] indices = result.GetProperty("coverageIndices")
                        .EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    int minCellX = indices.Min(index => index % 100);
                    int maxCellX = indices.Max(index => index % 100);
                    int minCellY = indices.Min(index => index / 100);
                    int maxCellY = indices.Max(index => index / 100);
                    int width = maxCellX - minCellX + 1;
                    int height = maxCellY - minCellY + 1;
                    cameraMeasurements.Add(new
                    {
                        aspect,
                        count = indices.Length,
                        minCellX,
                        maxCellX,
                        minCellY,
                        maxCellY,
                        width,
                        height,
                        tiledRequestCount = ((100 + width - 1) / width) * ((100 + height - 1) / height),
                        elapsedSeconds = result.GetProperty("elapsedSeconds").GetDouble(),
                        nativeRemoteTileX = RequireInt(result, "nativeRemoteTileX"),
                        nativeRemoteTileY = RequireInt(result, "nativeRemoteTileY"),
                        error = (string?)null,
                    });
                }
                catch (Exception error) when (error is InvalidDataException or TimeoutException)
                {
                    cameraMeasurements.Add(new
                    {
                        aspect,
                        count = 0,
                        minCellX = -1,
                        maxCellX = -1,
                        minCellY = -1,
                        maxCellY = -1,
                        width = 0,
                        height = 0,
                        tiledRequestCount = int.MaxValue,
                        elapsedSeconds = -1d,
                        nativeRemoteTileX = -1,
                        nativeRemoteTileY = -1,
                        error = error.Message,
                    });
                }
            }
            var cameraHeightMeasurements = new List<object>();
            (int CameraY, int X, int Y)[] cameraHeightSamples =
            [
                (120, 80, 500),
                (140, 180, 500),
                (160, 280, 500),
                (180, 380, 500),
                (200, 480, 500),
                (220, 580, 500),
                (240, 680, 500),
                (260, 780, 500),
                (280, 880, 500),
            ];
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT", "12");
            foreach ((int cameraY, int x, int y) in cameraHeightSamples)
            {
                Environment.SetEnvironmentVariable(
                    "LWBRIDGE_BULK_AOI_TEST_CAMERA_Y",
                    cameraY.ToString(System.Globalization.CultureInfo.InvariantCulture));
                try
                {
                    JsonElement result = await RunDiagnosticAsync(
                        session, context.ServerId, x, y,
                        holdMilliseconds: 0, viewLevel: -1, requestMode: "coverage",
                        requestedCount: 8, cancellationToken: operationCts.Token,
                        includeCityOverride: true, includeMonsterOverride: false,
                        includeDispatchOverride: true).ConfigureAwait(false);
                    int[] indices = result.GetProperty("coverageIndices")
                        .EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    int minCellX = indices.Min(index => index % 100);
                    int maxCellX = indices.Max(index => index % 100);
                    int minCellY = indices.Min(index => index / 100);
                    int maxCellY = indices.Max(index => index / 100);
                    int width = maxCellX - minCellX + 1;
                    int height = maxCellY - minCellY + 1;
                    cameraHeightMeasurements.Add(new
                    {
                        cameraY,
                        count = indices.Length,
                        width,
                        height,
                        tiledRequestCount =
                            ((100 + width - 1) / width) *
                            ((100 + height - 1) / height),
                        elapsedSeconds = result.GetProperty("elapsedSeconds").GetDouble(),
                        error = (string?)null,
                    });
                }
                catch (Exception error) when (error is InvalidDataException or TimeoutException)
                {
                    cameraHeightMeasurements.Add(new
                    {
                        cameraY,
                        count = 0,
                        width = 0,
                        height = 0,
                        tiledRequestCount = int.MaxValue,
                        elapsedSeconds = -1d,
                        error = error.Message,
                    });
                }
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_coverage_geometry_sweep",
                sessionId = session.SessionId,
                serverId = context.ServerId,
                fov = 175,
                aspect = 12,
                measurements,
                cameraMeasurements,
                cameraHeightMeasurements,
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV", oldFov);
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT", oldAspect);
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y", oldCameraY);
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_COVERAGE_GEOMETRY_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    internal static async Task RunCoverageCadenceBenchmarkAsync()
    {
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("coverage-cadence-benchmark-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        string? instanceId = null;
        Exception? operationError = null;
        string? oldFov = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV");
        string? oldAspect = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT");
        string? oldCameraY = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y");
        try
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV", "175");
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT", "12");
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y", "220");
            using JsonDocument empty = JsonDocument.Parse("{}");
            JsonElement started = JsonSerializer.SerializeToElement(
                await lifecycle.InvokeAsync("profile_instance_start", empty.RootElement, operationCts.Token)
                    .ConfigureAwait(false), JsonOptions.Default);
            instanceId = started.GetProperty("instanceId").GetString();
            OverviewMapScanSession session = lifecycle.GetReadyMapScanSession() ??
                throw new InvalidOperationException("Production lifecycle did not expose a ready coverage cadence session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context =
                await source.GetCurrentContextAsync(operationCts.Token).ConfigureAwait(false);
            await ParkCameraAwayFromTargetAsync(
                source, session, context, 500, 500, operationCts.Token).ConfigureAwait(false);
            (int X, int Y)[] targets =
            [
                (500, 500),
                (30, 160),
                (990, 910),
                (150, 160),
                (990, 160),
                (30, 910),
            ];
            var measurements = new List<object>();
            var coverageCounts = new List<int>();
            int successes = 0;
            string? error = null;
            Stopwatch wall = Stopwatch.StartNew();
            for (int i = 0; i < targets.Length; i++)
            {
                try
                {
                    JsonElement result = await RunDiagnosticAsync(
                        session, context.ServerId, targets[i].X, targets[i].Y,
                        holdMilliseconds: 0, viewLevel: -1, requestMode: "coverage",
                        requestedCount: 8, cancellationToken: operationCts.Token,
                        includeCityOverride: true, includeMonsterOverride: false,
                        includeDispatchOverride: true).ConfigureAwait(false);
                    int coverageCount = result.GetProperty("coverageIndices").GetArrayLength();
                    if (coverageCount is < 1 or > 160)
                        throw new InvalidDataException($"Coverage cadence request returned invalid AOI count {coverageCount}.");
                    if (!result.TryGetProperty("postInvokeSplitPending", out JsonElement split) ||
                        split.ValueKind != JsonValueKind.False ||
                        !result.TryGetProperty("cameraTileStable", out JsonElement stable) ||
                        stable.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException("Coverage cadence request did not remain bounded and camera-stable.");
                    coverageCounts.Add(coverageCount);
                    successes++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    error = ex.Message;
                    break;
                }
            }
            wall.Stop();
            measurements.Add(new
            {
                delayMs = 0,
                successes,
                total = targets.Length,
                ok = successes == targets.Length,
                wallSeconds = wall.Elapsed.TotalSeconds,
                averageWallSecondsPerRequest = wall.Elapsed.TotalSeconds / Math.Max(1, successes),
                coverageCounts,
                error,
            });
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_coverage_cadence_benchmark",
                sessionId = session.SessionId,
                serverId = context.ServerId,
                measurements,
            }, JsonOptions.Default));
        }
        catch (Exception error)
        {
            operationError = error;
            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV", oldFov);
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT", oldAspect);
            Environment.SetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y", oldCameraY);
            instanceId ??= lifecycle.GetReadyMapScanSession()?.SessionId;
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                using JsonDocument stopPayload = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { instanceId }, JsonOptions.Default));
                try
                {
                    await lifecycle.InvokeAsync(
                        "profile_instance_stop", stopPayload.RootElement, stopCts.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception stopError)
                {
                    Console.Error.WriteLine("LIVE_COVERAGE_CADENCE_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    internal static async Task RunMessageBulkSweepAsync()
    {
        int batchSize = ReadSweepBatchSize();
        string gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FunFly", "Last War-Survival Game");
        using var lifecycle = new OverviewLifecycleService("messagebulk-sweep-live-proof", gameRoot);
        using var operationCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
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
                throw new InvalidOperationException(
                    "Production lifecycle did not expose a ready message-bulk sweep session.");
            var source = new CurrentClientMapBlockSource(lifecycle);
            CurrentClientMapContext context = await source.GetCurrentContextAsync(operationCts.Token)
                .ConfigureAwait(false);
            if (context.TileWidth != 1000 || context.TileHeight != 1000 || context.WorldId != 0)
                throw new InvalidDataException(
                    $"Message-bulk sweep expected normal 1000x1000 world, got world={context.WorldId}, size={context.TileWidth}x{context.TileHeight}.");

            int primeX = context.PlayerTileX ?? 500;
            int primeY = context.PlayerTileY ?? 500;
            long wallStarted = Environment.TickCount64;
            var responseSeconds = new List<double>();
            var capturedRows = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            JsonElement bootstrap = await RunDiagnosticAsync(
                session, context.ServerId, primeX, primeY,
                holdMilliseconds: 0, viewLevel: 0, requestMode: "messagebulk",
                requestedCount: batchSize, cancellationToken: operationCts.Token,
                explicitIndices: null, reusePrimeContext: false,
                allowCachedExplicitIndices: false,
                primeNearCameraOverride: true,
                primeCurrentTileOverride: false,
                targetTotalViewCountOverride: 0,
                includeCityOverride: true,
                includeMonsterOverride: false,
                includeDispatchOverride: true).ConfigureAwait(false);

            int[] retained = ReadSweepIndexArray(bootstrap, "batchPrimeViewIndices", expectedCount: 16);
            int[] bootstrapBatch = ReadSweepIndexArray(bootstrap, "requestedIndices", expectedCount: batchSize);
            var covered = new HashSet<int>(retained);
            foreach (int index in bootstrapBatch)
                if (!covered.Add(index))
                    throw new InvalidDataException(
                        $"Message-bulk bootstrap re-requested retained AOI {index}.");
            int expectedBootstrapCoverage = retained.Length + bootstrapBatch.Length;
            if (covered.Count != expectedBootstrapCoverage)
                throw new InvalidDataException(
                    $"Message-bulk bootstrap covered {covered.Count} AOIs; expected {expectedBootstrapCoverage}.");
            int[] bootstrapCoverage = ReadSweepIndexArray(
                bootstrap, "coverageIndices", expectedCount: expectedBootstrapCoverage);
            if (!bootstrapCoverage.ToHashSet().SetEquals(covered))
                throw new InvalidDataException(
                    "Message-bulk bootstrap coverageIndices did not equal prime + requested AOIs.");

            CaptureSweepRows(bootstrap, capturedRows);
            AddResponseTime(bootstrap, responseSeconds);

            IReadOnlyList<int[]> remainingBatches = BuildMessageBulkSweepBatches(covered, batchSize);
            int expectedRemainingBatches =
                (10000 - covered.Count + batchSize - 1) / batchSize;
            if (remainingBatches.Count != expectedRemainingBatches)
                throw new InvalidDataException(
                    $"Message-bulk sweep expected {expectedRemainingBatches} remaining batches, got {remainingBatches.Count}.");
            int totalNetworkBatches = 1 + remainingBatches.Count;
            int networkBatchLimit = ReadSweepMaxBatches(totalNetworkBatches);
            int explicitBatchesToRun = networkBatchLimit - 1;

            for (int batchNumber = 0; batchNumber < explicitBatchesToRun; batchNumber++)
            {
                int[] batch = remainingBatches[batchNumber];
                (int targetX, int targetY) = SweepBatchCenter(batch);
                JsonElement result = await RunDiagnosticAsync(
                    session, context.ServerId, targetX, targetY,
                    holdMilliseconds: 0, viewLevel: 0, requestMode: "messagebulk",
                    requestedCount: batch.Length, cancellationToken: operationCts.Token,
                    explicitIndices: batch, reusePrimeContext: true,
                    allowCachedExplicitIndices: true,
                    primeNearCameraOverride: false,
                    primeCurrentTileOverride: true,
                    targetTotalViewCountOverride: 0,
                    includeCityOverride: true,
                    includeMonsterOverride: false,
                    includeDispatchOverride: true).ConfigureAwait(false);

                int[] batchCoverage = ReadSweepIndexArray(
                    result, "coverageIndices", expectedCount: batch.Length);
                if (!batchCoverage.ToHashSet().SetEquals(batch))
                    throw new InvalidDataException(
                        $"Message-bulk sweep batch {batchNumber + 2} coverageIndices differed from its requested AOIs.");

                foreach (int index in batch)
                    if (!covered.Add(index))
                        throw new InvalidDataException(
                            $"Message-bulk sweep batch {batchNumber + 2} duplicated AOI {index}.");

                CaptureSweepRows(result, capturedRows);
                AddResponseTime(result, responseSeconds);

                int completedNetworkBatches = batchNumber + 2;
                if (completedNetworkBatches % 10 == 0 ||
                    completedNetworkBatches == networkBatchLimit)
                {
                    Console.Error.WriteLine(
                        $"MESSAGEBULK_SWEEP_PROGRESS batches={completedNetworkBatches}/{networkBatchLimit} covered={covered.Count}/10000 rows={capturedRows.Count}");
                }
            }

            bool requestedFullSweep = networkBatchLimit == totalNetworkBatches;
            if (requestedFullSweep &&
                (covered.Count != 10000 || !covered.SetEquals(Enumerable.Range(0, 10000))))
            {
                throw new InvalidDataException(
                    $"Message-bulk full sweep finished with {covered.Count}/10000 unique AOIs.");
            }

            double wallSeconds = (Environment.TickCount64 - wallStarted) / 1000d;
            var kindCounts = capturedRows.Values
                .GroupBy(row => ReadString(row, "kind") ?? "unknown")
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = true,
                proof = "current_client_messagebulk_server_data_sweep",
                sessionId = session.SessionId,
                serverId = context.ServerId,
                batchSize,
                retainedContextAoiCount = retained.Length,
                bootstrapRequestedAoiCount = bootstrapBatch.Length,
                plannedNetworkBatches = totalNetworkBatches,
                completedNetworkBatches = networkBatchLimit,
                coveredAoiCells = covered.Count,
                totalAoiCells = 10000,
                fullCoverage = requestedFullSweep && covered.Count == 10000,
                capturedUniqueRows = capturedRows.Count,
                capturedKinds = kindCounts,
                wallSeconds,
                meanResponseSeconds = responseSeconds.Count == 0 ? 0 : responseSeconds.Average(),
                maxResponseSeconds = responseSeconds.Count == 0 ? 0 : responseSeconds.Max(),
                cameraPolicy = "visible-camera-stable; first local prime then reused prime context",
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
                    Console.Error.WriteLine("LIVE_MESSAGEBULK_SWEEP_STOP_FAILED: " + stopError.Message);
                    if (operationError is null) throw;
                }
            }
        }
    }

    private static IReadOnlyList<int[]> BuildMessageBulkSweepBatches(
        IEnumerable<int> alreadyCovered,
        int batchSize)
    {
        if (batchSize < 1 || batchSize > 160)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var covered = new HashSet<int>();
        foreach (int index in alreadyCovered)
        {
            if (index < 0 || index >= 10000 || !covered.Add(index))
                throw new InvalidDataException(
                    $"Message-bulk sweep seed contains invalid/duplicate AOI {index}.");
        }

        int[] remaining = Enumerable.Range(0, 10000)
            .Where(index => !covered.Contains(index))
            .ToArray();

        var result = new List<int[]>(
            (remaining.Length + batchSize - 1) / batchSize);
        for (int offset = 0; offset < remaining.Length; offset += batchSize)
        {
            int count = Math.Min(batchSize, remaining.Length - offset);
            result.Add(remaining.AsSpan(offset, count).ToArray());
        }
        return result;
    }

    private static int[] NativeBrowserWindowStartsForProof(int totalCells, int windowCells)
    {
        if (totalCells <= 0 || windowCells <= 0 || windowCells > totalCells)
            throw new ArgumentOutOfRangeException(nameof(windowCells));
        var starts = new List<int>();
        for (int start = 0; start <= totalCells - windowCells; start += windowCells)
            starts.Add(start);
        int finalStart = totalCells - windowCells;
        if (starts.Count == 0 || starts[^1] != finalStart)
            starts.Add(finalStart);
        return starts.ToArray();
    }

    private static int[] NativeBrowserCoreIndicesForProof(int startX, int startY)
    {
        if (startX is < 0 or > 86 || startY is < 0 or > 92)
            throw new ArgumentOutOfRangeException();
        return Enumerable.Range(0, 8)
            .SelectMany(y => Enumerable.Range(0, 14)
                .Select(x => checked((startY + y) * 100 + startX + x)))
            .ToArray();
    }

    private static int[] ReadSweepIndexArray(JsonElement root, string property, int expectedCount)
    {
        if (!root.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Message-bulk sweep result is missing {property}.");
        int[] result = value.EnumerateArray()
            .Select(item => item.TryGetInt32(out int parsed) ? parsed : -1)
            .ToArray();
        if (result.Length != expectedCount ||
            result.Any(index => index < 0 || index >= 10000) ||
            result.Distinct().Count() != result.Length)
            throw new InvalidDataException(
                $"Message-bulk sweep {property} is not {expectedCount} unique AOIs.");
        return result;
    }

    private static (int X, int Y) SweepBatchCenter(IReadOnlyList<int> batch)
    {
        int minX = 99, minY = 99, maxX = 0, maxY = 0;
        foreach (int index in batch)
        {
            int x = index % 100;
            int y = index / 100;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        return (
            Math.Min(999, ((minX + maxX + 1) * 10) / 2),
            Math.Min(999, ((minY + maxY + 1) * 10) / 2));
    }

    private static void CaptureSweepRows(
        JsonElement result,
        IDictionary<string, JsonElement> rows)
    {
        if (!result.TryGetProperty("point_records", out JsonElement pointRecords) ||
            pointRecords.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement row in pointRecords.EnumerateArray())
        {
            string kind = ReadString(row, "kind") ?? "unknown";
            string identity = string.Empty;
            foreach (string property in new[] { "uuid", "pointId", "id", "pointIndex" })
            {
                if (!row.TryGetProperty(property, out JsonElement value))
                    continue;
                identity = value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : value.GetRawText();
                if (!string.IsNullOrWhiteSpace(identity))
                    break;
            }
            if (string.IsNullOrWhiteSpace(identity))
            {
                string x = row.TryGetProperty("x", out JsonElement xValue) ? xValue.GetRawText() : "?";
                string y = row.TryGetProperty("y", out JsonElement yValue) ? yValue.GetRawText() : "?";
                string cfg = row.TryGetProperty("cfgId", out JsonElement cfgValue) ? cfgValue.GetRawText() : "?";
                identity = $"{x},{y},{cfg}";
            }
            rows[kind + ":" + identity] = row.Clone();
        }
    }

    private static void AddResponseTime(JsonElement result, ICollection<double> responseSeconds)
    {
        if (result.TryGetProperty("elapsedSeconds", out JsonElement elapsed) &&
            elapsed.TryGetDouble(out double seconds))
            responseSeconds.Add(seconds);
    }

    private static int ReadSweepBatchSize()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_SWEEP_BATCH_SIZE");
        if (string.IsNullOrWhiteSpace(raw))
            return 64;
        if (!int.TryParse(raw, out int value) || value < 1 || value > 160)
            throw new InvalidDataException(
                "LWBRIDGE_BULK_AOI_SWEEP_BATCH_SIZE must be 1..160.");
        return value;
    }

    private static int ReadSweepMaxBatches(int totalNetworkBatches)
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_SWEEP_MAX_BATCHES");
        if (string.IsNullOrWhiteSpace(raw))
            return totalNetworkBatches;
        if (!int.TryParse(raw, out int value) || value < 1 || value > totalNetworkBatches)
            throw new InvalidDataException(
                $"LWBRIDGE_BULK_AOI_SWEEP_MAX_BATCHES must be 1..{totalNetworkBatches}.");
        return value;
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
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken,
        IReadOnlyList<int>? explicitIndices = null,
        bool reusePrimeContext = false,
        bool allowCachedExplicitIndices = false,
        bool? primeNearCameraOverride = null,
        bool? primeCurrentTileOverride = null,
        int? targetTotalViewCountOverride = null,
        bool? includeCityOverride = null,
        bool? includeMonsterOverride = null,
        bool? includeDispatchOverride = null)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LWBridgeRebuild", "live-resource");
        Directory.CreateDirectory(root);
        string requestId = "bulk" + Guid.NewGuid().ToString("N");
        string commandPath = Path.Combine(root, "bulk-aoi-diagnostic.txt");
        string resultPath = Path.Combine(root, "bulk-aoi-diagnostic-result.json");
        string temporaryPath = commandPath + ".tmp-" + Guid.NewGuid().ToString("N");
        bool primeNearCamera = primeNearCameraOverride ?? string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_PRIME_NEAR_CAMERA"),
            "true", StringComparison.OrdinalIgnoreCase);
        bool primeCurrentTile = primeCurrentTileOverride ?? string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_PRIME_CURRENT_TILE"),
            "true", StringComparison.OrdinalIgnoreCase);
        bool dropPrimeView = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_DROP_PRIME_VIEW"),
            "true", StringComparison.OrdinalIgnoreCase);
        bool relocatePrimeView = string.Equals(
            Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_RELOCATE_PRIME_VIEW"),
            "true", StringComparison.OrdinalIgnoreCase);
        int targetTotalViewCount = targetTotalViewCountOverride ?? ReadTargetTotalViewCount();
        if (primeNearCamera && primeCurrentTile)
            throw new InvalidDataException(
                "LWBRIDGE_BULK_AOI_PRIME_NEAR_CAMERA and LWBRIDGE_BULK_AOI_PRIME_CURRENT_TILE cannot both be true.");
        bool includeCity = includeCityOverride ?? ReadIncludeCity();
        bool includeMonster = includeMonsterOverride ?? ReadIncludeMonster();
        bool includeDispatch = includeDispatchOverride ?? ReadIncludeDispatch();
        string testFov = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_FOV") ?? string.Empty;
        string testAspect = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_ASPECT") ?? string.Empty;
        string testCameraY = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TEST_CAMERA_Y") ?? string.Empty;
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
            $"targetTotalViewCount={targetTotalViewCount}",
            $"primeNearCamera={primeNearCamera.ToString().ToLowerInvariant()}",
            $"primeCurrentTile={primeCurrentTile.ToString().ToLowerInvariant()}",
            $"dropPrimeView={dropPrimeView.ToString().ToLowerInvariant()}",
            $"reusePrimeContext={reusePrimeContext.ToString().ToLowerInvariant()}",
            $"relocatePrimeView={relocatePrimeView.ToString().ToLowerInvariant()}",
            $"allowCachedExplicitIndices={allowCachedExplicitIndices.ToString().ToLowerInvariant()}",
            $"testFov={testFov}",
            $"testAspect={testAspect}",
            $"testCameraY={testCameraY}",
            $"explicitIndices={(explicitIndices is null ? string.Empty : string.Join(',', explicitIndices))}",
            $"holdMilliseconds={holdMilliseconds}",
            $"includeCity={includeCity.ToString().ToLowerInvariant()}",
            $"includeMonster={includeMonster.ToString().ToLowerInvariant()}",
            $"includeDispatch={includeDispatch.ToString().ToLowerInvariant()}",
            string.Empty,
        });
        try
        {
            try { if (File.Exists(commandPath)) File.Delete(commandPath); } catch { }
            try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
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
                    RequireInt(value, "gamePid") != session.GamePid)
                {
                    throw new InvalidDataException("Bulk AOI diagnostic did not match the owned game session.");
                }
                if (ReadString(value, "state") != "proven")
                    throw new InvalidDataException("Bulk AOI diagnostic failed: " + value.GetRawText());
                if (RequireInt(value, "requestedCount") != requestedCount ||
                    RequireInt(value, "targetTotalViewCount") != targetTotalViewCount ||
                    ReadString(value, "requestMode") != requestMode)
                {
                    throw new InvalidDataException("Bulk AOI diagnostic result parameters did not match the request.");
                }
                if (RequireInt(value, "holdMilliseconds") != holdMilliseconds ||
                    RequireInt(value, "viewLevel") != viewLevel)
                    throw new InvalidDataException("Bulk AOI diagnostic result parameters did not match the request.");
                if (requestMode == "browser")
                {
                    int browserIndexCount =
                        value.TryGetProperty("requestedIndices", out JsonElement browserIndices) &&
                        browserIndices.ValueKind == JsonValueKind.Array
                            ? browserIndices.GetArrayLength()
                            : -1;
                    if (browserIndexCount is < 1 or > 160)
                        throw new InvalidDataException(
                            $"Browser AOI diagnostic returned invalid native batch size {browserIndexCount}.");
                    if (ReadString(value, "requestMethod") !=
                        "WorldPointManager.UpdateLWAoi_Normal(true)+synthetic-anchor-no-camera-shift")
                        throw new InvalidDataException(
                            "Browser AOI diagnostic did not use the native synthetic-anchor path.");
                    if (!value.TryGetProperty("responseFlagsTransitioned", out JsonElement browserFlags) ||
                        browserFlags.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Browser AOI diagnostic completed without the native response flags.");
                    if (value.TryGetProperty("postInvokeSplitPending", out JsonElement browserSplit) &&
                        browserSplit.ValueKind == JsonValueKind.True)
                        throw new InvalidDataException(
                            "Browser AOI diagnostic unexpectedly entered native split-request state.");
                    if (!value.TryGetProperty("cameraTileStable", out JsonElement browserCameraStable) ||
                        browserCameraStable.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Browser AOI diagnostic changed the visible camera tile.");
                }
                if (requestMode == "messagebulk")
                {
                    string expectedMethod = reusePrimeContext
                        ? "WorldPointManager.SendAoiRequest(reused-prime-context,multi-index)"
                        : "WorldPointManager.SendViewRequest+SendAoiRequest(message-primed,multi-index)";
                    if (ReadString(value, "requestMethod") != expectedMethod)
                        throw new InvalidDataException(
                            $"Message-bulk diagnostic did not use expected path '{expectedMethod}'.");
                    if (!value.TryGetProperty("reusePrimeContext", out JsonElement reuseFlag) ||
                        reuseFlag.ValueKind != (reusePrimeContext ? JsonValueKind.True : JsonValueKind.False))
                        throw new InvalidDataException(
                            "Message-bulk diagnostic did not preserve the reuse-prime-context request flag.");
                    if (!value.TryGetProperty("allowCachedExplicitIndices", out JsonElement allowCachedFlag) ||
                        allowCachedFlag.ValueKind != (allowCachedExplicitIndices ? JsonValueKind.True : JsonValueKind.False))
                        throw new InvalidDataException(
                            "Message-bulk diagnostic did not preserve the allow-cached-explicit request flag.");
                    if (reusePrimeContext)
                    {
                        if (!value.TryGetProperty("primeContextReused", out JsonElement reused) ||
                            reused.ValueKind != JsonValueKind.True)
                            throw new InvalidDataException(
                                "Message-bulk diagnostic did not prove the retained prime context was reused.");
                        int contextViewCount = RequireInt(value, "reusePrimeContextViewCount");
                        if (contextViewCount is < 1 or > 64)
                            throw new InvalidDataException(
                                $"Message-bulk retained prime context had invalid view count {contextViewCount}.");
                    }
                    if (!value.TryGetProperty("primeResponseFlagsTransitioned", out JsonElement primeFlags) ||
                        primeFlags.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Message-bulk diagnostic did not prove the priming response.");
                    if (!value.TryGetProperty("responseFlagsTransitioned", out JsonElement batchFlags) ||
                        batchFlags.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Message-bulk diagnostic did not prove the direct batch response.");
                    if (!value.TryGetProperty("visualCameraPositionStable", out JsonElement messageBulkCameraStable) ||
                        messageBulkCameraStable.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Message-bulk diagnostic changed the visible camera transform.");
                    int batchIndexCount =
                        value.TryGetProperty("requestedIndices", out JsonElement batchIndices) &&
                        batchIndices.ValueKind == JsonValueKind.Array
                            ? batchIndices.GetArrayLength()
                            : -1;
                    int effectiveRequestedCount = targetTotalViewCount > 0
                        ? RequireInt(value, "effectiveRequestedCount")
                        : requestedCount;
                    if (batchIndexCount != effectiveRequestedCount)
                        throw new InvalidDataException(
                            $"Message-bulk diagnostic returned {batchIndexCount} indices; expected {effectiveRequestedCount}.");
                    if (explicitIndices is not null)
                    {
                        int[] observedIndices = batchIndices.EnumerateArray()
                            .Select(item => item.TryGetInt32(out int parsed) ? parsed : -1)
                            .ToArray();
                        if (!observedIndices.SequenceEqual(explicitIndices))
                            throw new InvalidDataException(
                                "Message-bulk diagnostic did not preserve the explicit AOI index batch.");
                    }
                    if (targetTotalViewCount > 0)
                    {
                        int retainedContextViewCount = RequireInt(value, "retainedContextViewCount");
                        int preparedTotal = RequireInt(value, "batchPreparedMsgViewCount");
                        if (effectiveRequestedCount != targetTotalViewCount - retainedContextViewCount ||
                            effectiveRequestedCount < 1 || effectiveRequestedCount > requestedCount)
                            throw new InvalidDataException(
                                $"Adaptive message-bulk count is invalid: retained={retainedContextViewCount}, effective={effectiveRequestedCount}, target={targetTotalViewCount}.");
                        if (preparedTotal != targetTotalViewCount)
                            throw new InvalidDataException(
                                $"Adaptive message-bulk prepared {preparedTotal} AOIs; expected exactly {targetTotalViewCount}.");
                    }
                    if (dropPrimeView)
                    {
                        if (!value.TryGetProperty("dropPrimeView", out JsonElement dropFlag) ||
                            dropFlag.ValueKind != JsonValueKind.True)
                            throw new InvalidDataException(
                                "Message-bulk diagnostic did not preserve the drop-prime-view request flag.");
                        int primeViewCount = RequireInt(value, "batchPrimeViewCount");
                        int postDropCount = RequireInt(value, "batchPostPrimeDropCurrentViewCount");
                        int preparedCount = RequireInt(value, "batchPreparedMsgViewCount");
                        if (primeViewCount < 1)
                            throw new InvalidDataException(
                                "Message-bulk drop-prime proof observed no prime-view AOIs to remove.");
                        if (postDropCount != 0)
                            throw new InvalidDataException(
                                $"Message-bulk drop-prime proof left {postDropCount} AOIs in the current set.");
                        if (preparedCount != requestedCount)
                            throw new InvalidDataException(
                                $"Message-bulk drop-prime proof prepared {preparedCount} message AOIs; expected exactly {requestedCount}.");
                    }
                }
                if (requestMode == "message")
                {
                    if (ReadString(value, "requestMethod") !=
                        "WorldPointManager.SendViewRequest(target-tile,no-camera-shift)")
                        throw new InvalidDataException(
                            "Message AOI diagnostic did not use targeted SendViewRequest.");
                    if (!value.TryGetProperty("visualCameraPositionStable", out JsonElement messageCameraStable) ||
                        messageCameraStable.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Message AOI diagnostic changed the visible camera transform.");
                    int baselineTargetCount = RequireInt(value, "baselineTargetPointCount");
                    int targetCount = RequireInt(value, "targetPointCount");
                    if (targetCount <= baselineTargetCount)
                        throw new InvalidDataException(
                            "Message AOI diagnostic did not load the known target point.");
                    if (ReadIncludeDispatch())
                    {
                        bool foundDispatchTarget =
                            value.TryGetProperty("point_records", out JsonElement pointRecords) &&
                            pointRecords.ValueKind == JsonValueKind.Array &&
                            pointRecords.EnumerateArray().Any(row =>
                                ReadString(row, "kind") == "dispatch_task" &&
                                row.TryGetProperty("x", out JsonElement xValue) &&
                                xValue.TryGetInt32(out int x) && x == targetTileX &&
                                row.TryGetProperty("y", out JsonElement yValue) &&
                                yValue.TryGetInt32(out int y) && y == targetTileY);
                        if (!foundDispatchTarget)
                            throw new InvalidDataException(
                                "Message AOI diagnostic loaded the target tile but not its Secret Task row.");
                    }
                }
                if (requestMode == "direct")
                {
                    int directIndexCount =
                        value.TryGetProperty("requestedIndices", out JsonElement directIndices) &&
                        directIndices.ValueKind == JsonValueKind.Array
                            ? directIndices.GetArrayLength()
                            : -1;
                    if (directIndexCount != requestedCount)
                        throw new InvalidDataException(
                            $"Direct AOI diagnostic returned {directIndexCount} indices; expected {requestedCount}.");
                    if (explicitIndices is not null)
                    {
                        int[] observedIndices = directIndices.EnumerateArray()
                            .Select(item => item.TryGetInt32(out int parsed) ? parsed : -1)
                            .ToArray();
                        if (!observedIndices.SequenceEqual(explicitIndices))
                            throw new InvalidDataException(
                                "Direct AOI diagnostic did not preserve the explicit AOI index batch.");
                    }
                    if (ReadString(value, "requestMethod") !=
                        "WorldPointManager.SendAoiRequest(private-reflection)-multi-index")
                        throw new InvalidDataException(
                            "Direct AOI diagnostic did not use the camera-free multi-index SendAoiRequest path.");
                    if (!value.TryGetProperty("responseFlagsTransitioned", out JsonElement directFlags) ||
                        directFlags.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Direct AOI diagnostic completed without the native response flags.");
                    if (!value.TryGetProperty("cameraTileStable", out JsonElement cameraStable) ||
                        cameraStable.ValueKind != JsonValueKind.True)
                        throw new InvalidDataException(
                            "Direct AOI diagnostic changed the visible camera tile.");
                }
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

    private static bool ReadIncludeCity() =>
        !string.Equals(Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_INCLUDE_CITY"), "false", StringComparison.OrdinalIgnoreCase);

    private static bool ReadIncludeMonster() =>
        !string.Equals(Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_INCLUDE_MONSTER"), "false", StringComparison.OrdinalIgnoreCase);

    private static bool ReadIncludeDispatch() =>
        string.Equals(Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_INCLUDE_DISPATCH"), "true", StringComparison.OrdinalIgnoreCase);

    private static string ReadRequestMode()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_REQUEST_MODE");
        if (string.IsNullOrWhiteSpace(raw)) return "native";
        string value = raw.Trim().ToLowerInvariant();
        if (value is not ("native" or "direct" or "message" or "messagebulk" or "browser" or "expanded" or "coverage" or "anchor" or "edge" or "zoom"))
            throw new InvalidDataException("LWBRIDGE_BULK_AOI_REQUEST_MODE must be native, direct, message, messagebulk, browser, expanded, coverage, anchor, edge, or zoom.");
        return value;
    }

    private static bool HasCoverageTarget() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TARGET_X")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TARGET_Y"));

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

    private static IReadOnlyList<int>? ReadExplicitIndices(int requestedCount)
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_EXPLICIT_INDICES");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        int[] values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out int value) ? value : -1)
            .ToArray();
        if (values.Length != requestedCount || values.Any(value => value < 0 || value >= 10000) ||
            values.Distinct().Count() != values.Length)
            throw new InvalidDataException(
                "LWBRIDGE_BULK_AOI_EXPLICIT_INDICES must contain exactly REQUEST_COUNT unique AOI indices in 0..9999.");
        return values;
    }

    private static int ReadTargetTotalViewCount()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_TARGET_TOTAL_VIEW_COUNT");
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        if (!int.TryParse(raw, out int value) || value < 0 || value > 160)
            throw new InvalidDataException(
                "LWBRIDGE_BULK_AOI_TARGET_TOTAL_VIEW_COUNT must be an integer from 0 through 160.");
        return value;
    }

    private static bool ReadReusePrimeContextAfterFirst()
    {
        string? raw = Environment.GetEnvironmentVariable("LWBRIDGE_BULK_AOI_REUSE_PRIME_CONTEXT_AFTER_FIRST");
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return false;
        throw new InvalidDataException(
            "LWBRIDGE_BULK_AOI_REUSE_PRIME_CONTEXT_AFTER_FIRST must be true or false.");
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
