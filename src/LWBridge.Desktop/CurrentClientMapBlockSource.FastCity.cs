using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed record CurrentClientTrainListCoverageResult(
    int LiveServerId,
    IReadOnlyList<int> MatchServerIds,
    IReadOnlyList<int> TruckServerIds,
    IReadOnlyList<int> RailwayServerIds);

internal sealed partial class CurrentClientMapBlockSource
{
    private const int FastCityAoiBlockSize = 10;
    private const int FastCityAoiBlockCount = 100;
    // Current-client live measurements are not fixed-width. Earlier v18 captures returned
    // 2-4 x 10 AOI footprints; a current-v19 server-2212 capture on 2026-09-20 proved a valid
    // contiguous 5 x 10 footprint as well. Never assume a fixed width: full-world acquisition
    // advances from the measured native footprint and still requires the exact 10,000-cell union.
    private const int FastCityGroupColumns = 1;
    private const int FastCityGroupRows = 5;
    private const int FastCityExpectedAoiCount = 20;
    // Genuine v26 target/home-correlated snapshots for Truck and Dispatch
    // have contracted to 8 native AOI rows x 2 columns (16 AOIs). This is a
    // lower admission bound, not an assertion that the 10,000-AOI world shrank.
    private const int FastCityMinimumLiveAoiCount = 16;
    private const int FastFullWorldAoiRows = 10;
    private const int FastFullWorldRowRequests = FastCityAoiBlockCount / FastFullWorldAoiRows;
    // A native 8-row viewport may need two vertically distinct fresh replies
    // for each horizontal footprint to cover a ten-row logical band. The old
    // 50-probe ceiling would reject a completely valid 10,000-AOI union.
    // This is a safety upper bound, not a fabricated progress or timeout.
    private const int FastFullWorldMaxRequestsPerRow = 200;
    private static readonly TimeSpan FastCityProbeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MonsterProtectionProbeTimeout = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan ResourceDetailProbeTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FastCityStartupSettleDelay = TimeSpan.FromSeconds(3);
    private string? fastCitySettledSessionId;
    private FastFullWorldResumeState? fastFullWorldResumeState;
    internal MonsterProtectionDetailMetrics? LastMonsterProtectionDetailMetrics { get; private set; }
    internal ResourceCompletenessReport? LastResourceCompletenessReport { get; private set; }

    public Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices, CancellationToken cancellationToken) =>
        CaptureBatchAsync(request, seedBlock, pendingBlockIndices, null, cancellationToken);

    public async Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request, MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices, Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingBlockIndices);
        if (!CanUseFastCityBatch(request))
            return [await CaptureAsync(request, seedBlock, cancellationToken).ConfigureAwait(false)];

        OverviewMapScanSession session = await RequireReadyOrResumeSessionAsync(request, cancellationToken).ConfigureAwait(false);
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }

        // Fast Truck and direct LOD0 AOI requests leave the physical camera
        // untouched. Truck begins after a startup settle to avoid an in-flight
        // scene transition while sampling ephemeral march pushes.
        bool directFirst = pendingBlockIndices.Count == 2500 && seedBlock.BlockIndex == 0 &&
            hooks?.ForceLegacyFullWorldLens != true &&
            hooks?.ExperimentalBoundedCameraLens != true &&
            !(IsZombieBossOnly(request) && hooks?.UseCoarseZombieBossMap == true &&
              hooks?.DisableCoarseMonsterMap != true);
        if (!directFirst)
            await EnsureFastCityStartupSettledAsync(session, cancellationToken).ConfigureAwait(false);

        if (pendingBlockIndices.Count == 2500 && seedBlock.BlockIndex == 0)
        {
            if (fastFullWorldResumeState is not { } resume || !resume.Matches(session, request))
            {
                fastFullWorldResumeState = new FastFullWorldResumeState(session, request);
                if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
                    LastResourceCompletenessReport = null;
            }

            try
            {
                // The LOD2 whole-world Zombie Boss snapshot was only proven on game v18/v19; on v26 the proven
                // shared LOD0 sweep is used unless a diagnostic explicitly opts in.
                if (IsZombieBossOnly(request) && hooks?.UseCoarseZombieBossMap == true && hooks?.DisableCoarseMonsterMap != true)
                {
                    // Zombie Boss keeps its existing read-only dedicated internal
                    // path; this is not admission to the public selection.
                    return await CaptureFullMonsterMapViaCoarseLodAsync(
                        session, request, pendingBlockIndices, progress, cancellationToken).ConfigureAwait(false);
                }
                // Truck uses an independently owned 625-rectangle sweep before
                // the static AOI capture; failures retry via the camera route.
                // Mixed scans merge typed Truck rows with the normal direct
                // point/march source only after BOTH native receipts qualify.
                if (directFirst)
                {
                    try
                    {
                        IReadOnlyList<FastTrainPrepared>? fastTrucks = null;
                        if (request.SelectedTypes.Contains("truck", StringComparer.Ordinal))
                        {
                            await EnsureFastCityStartupSettledAsync(session, cancellationToken).ConfigureAwait(false);
                            fastTrucks = await CaptureFastTruckRowsAsync(session, request, cancellationToken)
                                .ConfigureAwait(false);
                            if (request.SelectedTypes.Count == 1)
                                return ProjectFastTruckOnly(request, pendingBlockIndices, fastTrucks, progress);
                        }
                        else if (request.SelectedTypes.Contains("railway", StringComparer.Ordinal))
                        {
                            await EnsureFastCityStartupSettledAsync(session, cancellationToken).ConfigureAwait(false);
                            fastTrucks = await CaptureFastRailwayRowsAsync(session, request, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        if (fastTrucks is not null && request.SelectedTypes.All(kind => kind is "truck" or "railway"))
                            return ProjectFastMobileTrains(request, pendingBlockIndices, fastTrucks, progress);
                        return await CaptureCameraFreeMapAsync(
                            session, request, pendingBlockIndices, progress, cancellationToken, fastTrucks)
                            .ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is TimeoutException or InvalidDataException or IOException or UnauthorizedAccessException ||
                        error is BridgeCommandException bridge && bridge.Code == "GAME_CONNECTION_UNAVAILABLE")
                    {
                        Console.Error.WriteLine(
                            $"CAMERA_FREE_FALLBACK server={request.ServerId} run={request.RunId} reason={error.Message}");
                        // Keep the exact managed rejection reason in this
                        // owned scan's ignored runtime evidence. The packaged
                        // Windows UI has no attached stderr console, and a
                        // successful native 63-reply receipt alone cannot
                        // reveal which later parser/hydrator rejected it.
                        try
                        {
                            await File.WriteAllTextAsync(
                                Path.Combine(probeRuntimeRoot, "map015-direct-fallback.json"),
                                JsonSerializer.Serialize(new
                                {
                                    runId = request.RunId, serverId = request.ServerId,
                                    causeType = error.GetType().Name, reason = error.Message,
                                    recordedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                                }, JsonOptions.Default), CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (IOException) { /* diagnostic only */ }
                        catch (UnauthorizedAccessException) { /* diagnostic only */ }
                        // Neither a failed native AOI request nor its staged data
                        // may contaminate the camera scan's resume/coverage state.
                        fastFullWorldResumeState = null;
                        LastResourceCompletenessReport = null;
                        LastMonsterProtectionDetailMetrics = null;
                        RequireSameSession(session);
                    }
                }
                if (directFirst)
                    await EnsureFastCityStartupSettledAsync(session, cancellationToken).ConfigureAwait(false);
                return await CaptureFullCityMapAsync(session, request, pendingBlockIndices, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException &&
                error is not MapScanSourceValidationException)
            {
                // One full-world native camera owner is not 2,500 independent
                // probes. After its bounded source attempts are exhausted, a
                // retry of logical block 0 used to drop seed0, switch to the
                // legacy band route, and count 100+ failures as fake progress.
                // Retire this run with its actual first fatal source reason,
                // zero staged blocks and the regular exact-owned cleanup.
                throw new MapScanSourceValidationException(
                    "Full-world native source failed before durable block publication: " +
                    error.Message, error);
            }
        }

        int bandStartRow = (seedBlock.Row / FastCityGroupRows) * FastCityGroupRows;
        int firstGroupStartColumn = (seedBlock.Column / FastCityGroupColumns) * FastCityGroupColumns;
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        // Build pending groups once. A resumed band used to re-filter all 2,500
        // logical blocks for every AOI group, even when only one group remained.
        // Group membership and the ordered per-block projection stay unchanged.
        ILookup<int, MapScanTargetBlock> pendingGroups = logicalBlocks
            .Where(block =>
                block.Row >= bandStartRow &&
                block.Row < bandStartRow + FastCityGroupRows &&
                block.Column >= firstGroupStartColumn &&
                pendingBlockIndices.Contains(block.BlockIndex))
            .ToLookup(block => block.Column / FastCityGroupColumns);
        var captures = new List<MapScanBlockCapture>();

        for (int groupStartColumn = firstGroupStartColumn; groupStartColumn < 50; groupStartColumn += FastCityGroupColumns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MapScanTargetBlock[] groupBlocks = pendingGroups[
                groupStartColumn / FastCityGroupColumns].ToArray();
            if (groupBlocks.Length == 0) continue;

            int targetX = checked(groupStartColumn * MapScanGeometry.RecoveredBlockSpan + 15);
            int targetY = checked(bandStartRow * MapScanGeometry.RecoveredBlockSpan + 75);
            FastCityBatchObservation observation = await ProbeFastCityBatchAsync(
                session, request, groupStartColumn, bandStartRow, targetX, targetY, cancellationToken)
                .ConfigureAwait(false);
            RequireSameSession(session);

            Dictionary<int, MapStoredRecord[]> recordsByBlock = RecordsByBlock(observation, request);
            foreach (MapScanTargetBlock block in groupBlocks.OrderBy(block => block.BlockIndex))
            {
                IReadOnlyList<MapStoredRecord> records = recordsByBlock.TryGetValue(
                    block.BlockIndex, out MapStoredRecord[]? value)
                    ? value
                    : Array.Empty<MapStoredRecord>();
                int[] blockAoiIndices = ExpectedBlockAoiIndices(block);
                string payload = JsonSerializer.Serialize(new
                {
                    protocol = "current_fast_city_batch_v1",
                    blockIndex = block.BlockIndex,
                    targetX,
                    targetY,
                    minX = block.MinX,
                    minY = block.MinY,
                    maxX = block.MaxX,
                    maxY = block.MaxY,
                    recordsInBlock = records.Count,
                    aoiIndices = blockAoiIndices,
                    footprintAoiCount = observation.RequestedIndices.Length,
                    coverage = "live_cur_view_index_covered_fast_batch",
                    footprintSource = "WorldPointManager._curViewIndex",
                }, JsonOptions.Default);
                captures.Add(new MapScanBlockCapture(
                    request.ServerId,
                    request.WorldId,
                    block.BlockIndex,
                    payload,
                    records));
            }
        }

        if (!captures.Any(capture => capture.BlockIndex == seedBlock.BlockIndex))
            throw new InvalidDataException("Fast City batch did not contain the requested seed block.");
        return captures;
    }

    private async Task EnsureFastCityStartupSettledAsync(
        OverviewMapScanSession session, CancellationToken cancellationToken)
    {
        if (string.Equals(fastCitySettledSessionId, session.SessionId, StringComparison.Ordinal)) return;
        await DelayAsync(FastCityStartupSettleDelay, cancellationToken).ConfigureAwait(false);
        RequireSameSession(session);
        fastCitySettledSessionId = session.SessionId;
    }

    private async Task<IReadOnlyList<MapScanBlockCapture>> CaptureFullTrainListMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlySet<int> pendingBlockIndices,
        Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        if (logicalBlocks.Count != 2500 || pendingBlockIndices.Count != logicalBlocks.Count ||
            logicalBlocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("Direct Train-list acquisition requires the exact 2,500-block full-world pending set.");

        progress?.Invoke(new MapScanSourceProgress(20));
        FastTrainListSnapshot directSnapshot = await ProbeTrainListAsync(
            session, request, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FastTrainPrepared> directRows = directSnapshot.Rows;
        RequireSameSession(session);
        progress?.Invoke(new MapScanSourceProgress(90));

        var recordsByBlock = new Dictionary<int, List<MapStoredRecord>>();
        foreach (FastTrainPrepared raw in directRows)
        {
            FastTrainPrepared prepared = raw.Record.Kind == "truck"
                ? ApplyFinalTruckMetadataEnrichment(raw)
                : raw;
            int blockColumn = prepared.X / MapScanGeometry.RecoveredBlockSpan;
            int blockRow = prepared.Y / MapScanGeometry.RecoveredBlockSpan;
            int blocksPerAxis = checked((int)(request.TileWidth / MapScanGeometry.RecoveredBlockSpan));
            int blockIndex = checked(blockRow * blocksPerAxis + blockColumn);
            if (blockIndex < 0 || blockIndex >= logicalBlocks.Count)
                throw new InvalidDataException("Direct Train-list row fell outside the logical full-world block grid.");
            if (!recordsByBlock.TryGetValue(blockIndex, out List<MapStoredRecord>? bucket))
                recordsByBlock[blockIndex] = bucket = [];
            bucket.Add(prepared.Record);
        }

        string[] directKinds = request.SelectedTypes.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var captures = new List<MapScanBlockCapture>(logicalBlocks.Count);
        foreach (MapScanTargetBlock block in logicalBlocks)
        {
            IReadOnlyList<MapStoredRecord> records = recordsByBlock.TryGetValue(
                block.BlockIndex, out List<MapStoredRecord>? bucket)
                ? bucket
                : Array.Empty<MapStoredRecord>();
            string payload = JsonSerializer.Serialize(new
            {
                protocol = "current_fast_train_list_v1",
                blockIndex = block.BlockIndex,
                recordsInBlock = records.Count,
                selectedTypes = directKinds,
                coverage = "official_get_train_list_snapshot",
                source = "LWTrainDataManager.TryGetTrainList(true)",
            }, JsonOptions.Default);
            captures.Add(new MapScanBlockCapture(
                request.ServerId, request.WorldId, block.BlockIndex, payload, records));
        }
        progress?.Invoke(new MapScanSourceProgress(100));
        return captures;
    }

    private async Task<IReadOnlyList<MapScanBlockCapture>> CaptureFullMonsterMapViaCoarseLodAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlySet<int> pendingBlockIndices,
        Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        if (logicalBlocks.Count != 2500 || pendingBlockIndices.Count != logicalBlocks.Count ||
            logicalBlocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("LOD2 Monster acquisition requires all 2,500 logical blocks pending.");

        LastMonsterProtectionDetailMetrics = null;
        IReadOnlyList<FastMonsterPrepared> coarse = await ProbeCoarseMonsterMapAsync(
            session, request, cancellationToken).ConfigureAwait(false);
        RequireSameSession(session);
        progress?.Invoke(new MapScanSourceProgress(90));

        var monsterRecords = new Dictionary<string, FastMonsterPrepared>(StringComparer.Ordinal);
        foreach (FastMonsterPrepared prepared in coarse)
        {
            if (!monsterRecords.TryGetValue(prepared.Record.RecordKey, out FastMonsterPrepared? prior) ||
                prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                monsterRecords[prepared.Record.RecordKey] = prepared;
        }

        MonsterProtectionDetailObservation protection = MonsterProtectionDetailObservation.Empty;
        int monsterInvasionBossCount = monsterRecords.Values.Count(item => item.ProtectionEligible);
        if (monsterInvasionBossCount > 0)
        {
            try
            {
                protection = await ProbeMonsterProtectionDetailsAsync(
                    session, request, monsterInvasionBossCount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                protection = MonsterProtectionDetailObservation.Failed(ex.Message);
            }
            foreach ((string key, FastMonsterPrepared prepared) in monsterRecords.ToArray())
            {
                if (!prepared.ProtectionEligible) continue;
                protection.Details.TryGetValue(prepared.Record.Uuid ?? string.Empty, out MonsterProtectionDetail? detail);
                monsterRecords[key] = ApplyMonsterProtectionDetail(prepared, detail);
            }
        }
        LastMonsterProtectionDetailMetrics = new MonsterProtectionDetailMetrics(
            monsterInvasionBossCount, protection.TargetCount, protection.RequestCount,
            protection.RetryCount, protection.ReadyCount, protection.Error);
        progress?.Invoke(new MapScanSourceProgress(100));

        var buckets = new Dictionary<int, List<MapStoredRecord>>();
        foreach (FastMonsterPrepared item in monsterRecords.Values)
            AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        Dictionary<int, MapStoredRecord[]> recordsByBlock = buckets.ToDictionary(
            pair => pair.Key, pair => pair.Value.ToArray());
        var captures = new List<MapScanBlockCapture>(logicalBlocks.Count);
        foreach (MapScanTargetBlock block in logicalBlocks)
        {
            IReadOnlyList<MapStoredRecord> records = recordsByBlock.TryGetValue(
                block.BlockIndex, out MapStoredRecord[]? value) ? value : Array.Empty<MapStoredRecord>();
            string payload = JsonSerializer.Serialize(new
            {
                protocol = "current_fast_zombie_boss_lod2_v1",
                blockIndex = block.BlockIndex,
                coverage = "native_lod2_whole_world_zombie_boss_snapshot_restored_to_original_lod",
                recordsInBlock = records.Count,
                monsterInvasionBossCount,
                monsterProtectionDetailTargetCount = protection.TargetCount,
                monsterProtectionDetailRequestCount = protection.RequestCount,
                monsterProtectionDetailRetryCount = protection.RetryCount,
                monsterProtectionDetailReadyCount = protection.ReadyCount,
                monsterProtectionDetailError = protection.Error,
            }, JsonOptions.Default);
            captures.Add(new MapScanBlockCapture(
                request.ServerId, request.WorldId, block.BlockIndex, payload, records));
        }
        fastFullWorldResumeState = null;
        return captures;
    }

    private async Task<IReadOnlyList<MapScanBlockCapture>> CaptureFullCityMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlySet<int> pendingBlockIndices,
        Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        if (logicalBlocks.Count != 2500 || pendingBlockIndices.Count != logicalBlocks.Count ||
            logicalBlocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("Fast full-world acquisition requires all 2,500 logical blocks pending.");

        FastFullWorldResumeState? existingState = fastFullWorldResumeState;
        bool reuseState = existingState is not null && existingState.Matches(session, request);
        FastFullWorldResumeState state = reuseState
            ? existingState!
            : new FastFullWorldResumeState(session, request);
        if (!reuseState && request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
            LastResourceCompletenessReport = null;
        fastFullWorldResumeState = state;
        HashSet<int> covered = state.Covered;
        LastMonsterProtectionDetailMetrics = null;
        bool boundedLens = ShouldUseBoundedFullWorldCameraLens(request, hooks);
        bool nativeEightRowWindowObserved = false;
        // Where the native window really starts relative to the target AOI row (measured on unclipped
        // replies). Lens height / player zoom move it, so placement follows the observation, not a constant.
        int? windowFirstRowOffset = null;
        int stalledGapColumn = -1;
        int lastMissGapColumn = -1;
        int missesAtGap = 0;
        for (int row = 0; row < (boundedLens ? 1 : FastFullWorldRowRequests); row++)
        {
            int groupStartRow = row * FastCityGroupRows;
            int aoiRowStart = row * FastFullWorldAoiRows;
            int baseTargetY = 75 + (row * 100);
            bool preferWideStep = false;
            int? previousGap = null;
            int requestsThisRow = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int firstGap = boundedLens ? Enumerable.Range(0, 10000).FirstOrDefault(index => !covered.Contains(index), -1) : -1;
                int gapColumn = boundedLens ? (firstGap < 0 ? -1 : firstGap % 100) : FirstUncoveredAoiColumn(covered, aoiRowStart);
                if (gapColumn < 0) break;
                // Current-v21 live proof can contract the conservative request to 9 rows.
                // If that leaves only the band's bottom AOI missing, nudge one AOI row down and
                // union the second bounded footprint; exact 10,000-cell coverage remains authoritative.
                (int targetY, int[] missingRows) = SelectFullWorldProbeY(
                    boundedLens, covered, firstGap, aoiRowStart, gapColumn,
                    nativeEightRowWindowObserved, gapColumn == stalledGapColumn ? null : windowFirstRowOffset);
                if (++requestsThisRow > (boundedLens ? 400 : FastFullWorldMaxRequestsPerRow))
                    throw new InvalidDataException($"Fast full-world acquisition made no bounded progress in AOI row band {row}.");

                // Wide footprints are centered around the target: width 4 has two columns
                // left / one right, while the observed width 5 has two left / two right.
                // Normal deliberately places the camera one AOI ahead of the first gap,
                // maximizing margin when native footprints contract. Fast uses the
                // measured >=4-column footprint to advance two AOIs, gathering more
                // previously uncovered cells per serial request when supported.
                // Both modes validate fresh footprints and the same exact 10,000 AOIs;
                // neither issues concurrent mutations of the shared game camera.
                int targetCellX = MapFullWorldAoiPlacement.TargetColumn(
                    request.ScanMode, gapColumn, preferWideStep, previousGap, FastCityAoiBlockCount);
                if (boundedLens) targetCellX = Math.Min(99, gapColumn + 7);
                int targetX = checked((targetCellX * FastCityAoiBlockSize) + 5);

                FastCityBatchObservation? observation = null;
                Exception? lastError = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    DateTimeOffset probeStarted = Now();
                    state.ProbeAttempts++;
                    if (attempt > 1) state.ProbeRetries++;
                    try
                    {
                        int activeProbes = Interlocked.Increment(ref state.ActiveCameraProbes);
                        state.PeakCameraProbes = Math.Max(state.PeakCameraProbes, activeProbes);
                        observation = await ProbeFastCityBatchAsync(
                            session, request, -1, groupStartRow, targetX, targetY, cancellationToken)
                            .ConfigureAwait(false);
                        if (boundedLens) ValidateBoundedLensFootprint(observation.RequestedIndices, firstGap, targetX, targetY);
                        else ValidateAdaptiveRowFootprint(
                            observation.RequestedIndices,
                            aoiRowStart,
                            request,
                            session,
                            targetX,
                            targetY,
                            attempt);
                        state.ValidatedProbeResponses++;
                        if (observation.TargetResponseMilliseconds is double targetMilliseconds &&
                            observation.HomeRestorationMilliseconds is double homeMilliseconds &&
                            observation.TotalProbeMilliseconds is double totalMilliseconds)
                        {
                            state.NativePhaseSamples++;
                            state.NativeTargetResponseMilliseconds += targetMilliseconds;
                            state.NativeHomeRestorationMilliseconds += homeMilliseconds;
                            state.NativeTotalProbeMilliseconds += totalMilliseconds;
                        }
                        lastError = null;
                        break;
                    }
                    catch (Exception error) when (error is TimeoutException or InvalidDataException ||
                        error is BridgeCommandException bridge && bridge.Code == "GAME_CONNECTION_UNAVAILABLE")
                    {
                        lastError = error;
                        if (boundedLens || hooks is null)
                            Console.Error.WriteLine(
                                $"FAST_FULL_WORLD_RETRY server={request.ServerId} world={request.WorldId} " +
                                $"target=({targetX},{targetY}) rowStart={aoiRowStart} attempt={attempt}/3 error={error.Message}");
                        if (attempt < 3)
                        {
                            if (IsOverviewSessionAdmissionGap(error))
                            {
                                // A correlated Lua result can briefly observe the Overview lease/control
                                // handoff between ticks even while the exact owned game session is still
                                // intact. Do not route that one-response admission gap through the recovered
                                // 180-second login-unavailable Home recovery window.
                                RequireSameSession(session);
                                await DelayAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                if (waitForHealthySession is { } waitForHealthy)
                                {
                                    await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
                                    RequireSameSession(session);
                                }
                                await DelayAsync(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref state.ActiveCameraProbes);
                        state.ProbeWaitMilliseconds += Math.Max(0, (Now() - probeStarted).TotalMilliseconds);
                    }
                }
                if (lastError is not null || observation is null) throw lastError!;
                RequireSameSession(session);
                if (!boundedLens &&
                    observation.RequestedIndices.Select(index => index / FastCityAoiBlockCount)
                        .Distinct().Count() == FastFullWorldAoiRows - 2)
                    nativeEightRowWindowObserved = true;
                if (!boundedLens)
                {
                    int observedFirstRow = observation.RequestedIndices.Min() / FastCityAoiBlockCount;
                    int observedLastRow = observation.RequestedIndices.Max() / FastCityAoiBlockCount;
                    if (observedFirstRow > 0 && observedLastRow < FastCityAoiBlockCount - 1)
                        windowFirstRowOffset = observedFirstRow - targetY / FastCityAoiBlockSize;
                    // A calibrated placement that did not reach the first missing cell falls back to the
                    // proven centre placement for this column instead of repeating itself.
                    if (observation.RequestedIndices.Contains(missingRows.Min() * FastCityAoiBlockCount + gapColumn))
                        missesAtGap = 0;
                    else
                    {
                        // Re-calibrate from this reply and retry once; a second miss uses the centre placement.
                        missesAtGap = lastMissGapColumn == gapColumn ? missesAtGap + 1 : 1;
                        lastMissGapColumn = gapColumn;
                        if (missesAtGap >= 2) stalledGapColumn = gapColumn;
                    }
                }

                int before = covered.Count;
                MergeFullWorldObservation(state, observation);

                int afterGap = boundedLens ? Enumerable.Range(0, 10000).FirstOrDefault(index => !covered.Contains(index), -1) : FirstUncoveredAoiColumn(covered, aoiRowStart);
                if (covered.Count == before)
                    throw new InvalidDataException("Fast full-world adaptive acquisition returned no new AOI coverage.");
                int measuredWidth = observation.RequestedIndices.Select(index => index % FastCityAoiBlockCount).Distinct().Count();
                preferWideStep = measuredWidth >= 4 && afterGap != gapColumn;
                previousGap = afterGap == gapColumn ? gapColumn : null;
                progress?.Invoke(new MapScanSourceProgress(covered.Count * 100d / 10000d));
                if (boundedLens && state.ValidatedProbeResponses % 10 == 0) WriteFastFullWorldMetrics(state, "acquiring");
            }
            // Ten compact checkpoints (and one final) per full-world pass;
            // per-request I/O would itself distort the elapsed benchmark.
            WriteFastFullWorldMetrics(state, "acquiring");
        }

        if (covered.Count != 10000)
            throw new InvalidDataException($"Fast full-world acquisition covered {covered.Count}/10000 AOIs.");
        state.AcquisitionMilliseconds = (Now() - state.MetricsStartedAt).TotalMilliseconds;
        WriteFastFullWorldMetrics(state, "covered");

        return await CompleteFullWorldMapAsync(session, request, logicalBlocks, state, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    // Both acquisition sources share every downstream detail request, record
    // enrichment, resource completeness check and 2,500-block projection.
    private async Task<IReadOnlyList<MapScanBlockCapture>> CompleteFullWorldMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlyList<MapScanTargetBlock> logicalBlocks,
        FastFullWorldResumeState state,
        Action<MapScanSourceProgress>? progress,
        CancellationToken cancellationToken)
    {
        HashSet<int> covered = state.Covered;
        Dictionary<string, FirstLivePreparedResource> cityRecords = state.CityRecords;
        Dictionary<string, FirstLivePreparedResource> resourceRecords = state.ResourceRecords;
        Dictionary<string, FastDispatchPrepared> dispatchRecords = state.DispatchRecords;
        Dictionary<string, FastGhostPrepared> ghostRecords = state.GhostRecords;
        Dictionary<string, FastTreasurePrepared> treasureRecords = state.TreasureRecords;
        Dictionary<string, FastMonsterPrepared> monsterRecords = state.MonsterRecords;
        Dictionary<string, FastTrainPrepared> trainRecords = state.TrainRecords;

        // An empty Boss result is published only when the whole-map sweep was proven complete and the
        // game Monster list it rode on delivered rows (MAP-019). An unproven empty source still fails,
        // so positive unrelated kinds can never turn a broken Boss source into a misleading success.
        RequireZombieBossSourceRows(request, monsterRecords.Values.Any(item => item.ProtectionEligible),
            covered.Count == 10000 && state.MonsterSourceRows > 0);

        // OWNER WORKFLOW R7-147: current-v20 Railway UI does not rely solely on
        // WorldScene.MarchDataManager. It explicitly refreshes LWTrainDataManager
        // and consumes the response allianceTrainList. Reproduce that read-only
        // source once per completed full-world scan, then merge by exact march UUID.
        if (request.SelectedTypes.Contains("railway", StringComparer.Ordinal) && !state.RailwaySweepVerified)
        {
            DateTimeOffset railwayStarted = Now();
            // The official train-list endpoint returns both TrainType.Train and
            // TrainType.Truck when the request selects both. A complete mixed
            // scan already has an independently verified fast Truck sweep;
            // this final refresh must ask for Railway alone or a live Truck
            // would fail the Railway-only source check and force camera fallback.
            MapScanExecutionRequest railwayRequest = request with { SelectedTypes = ["railway"] };
            IReadOnlyList<FastTrainPrepared> refreshedRailway = (await ProbeTrainListAsync(
                session, railwayRequest, cancellationToken).ConfigureAwait(false)).Rows;
            state.RailwayRefreshMilliseconds = (Now() - railwayStarted).TotalMilliseconds;
            RequireSameSession(session);
            foreach (FastTrainPrepared prepared in refreshedRailway)
            {
                if (prepared.Record.Kind != "railway")
                    throw new InvalidDataException("The current-client Train-list refresh returned a non-Railway row.");
                trainRecords[prepared.Record.RecordKey] = prepared;
            }
        }

        // Truck reward/max-loot enrichment is deliberately post-acquisition. The AOI hot path
        // carries only lightweight game-owned TrainData fields/reward arrays; full TrainData JSON
        // is Railway-only because current-v19 Truck plunder history makes that blob very large.
        DateTimeOffset truckStarted = Now();
        if (request.SelectedTypes.Contains("truck", StringComparer.Ordinal))
        {
            foreach ((string key, FastTrainPrepared prepared) in trainRecords.ToArray())
            {
                if (prepared.Record.Kind != "truck") continue;
                trainRecords[key] = ApplyFinalTruckMetadataEnrichment(prepared);
            }
        }
        state.TruckHydrationMilliseconds = (Now() - truckStarted).TotalMilliseconds;

        DateTimeOffset resourceStarted = Now();
        ResourceScanDetailObservation resourceDetails = ResourceScanDetailObservation.Empty;
        int idleResourceCount = request.SelectedTypes.Contains("resource", StringComparer.Ordinal)
            ? resourceRecords.Values.Count(item => IsKnownIdleResource(item.Record))
            : 0;
        if (idleResourceCount > 0)
        {
            Exception? resourceHydrationCause = null;
            try
            {
                resourceDetails = await ProbeResourceScanDetailsAsync(
                    session, request, resourceRecords.Count, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                resourceHydrationCause = ex;
                resourceDetails = ResourceScanDetailObservation.Failed(ex.Message);
            }
            // A failed/partial native detail pass previously published every
            // known idle Resource with unknown capacity, despite reporting a
            // completed full-world scan. Require the exact current idle keys
            // before modifying any staged record; a source error must abort
            // this publication attempt, including mixed selections.
            RequireFullWorldResourceHydration(probeRuntimeRoot, request,
                resourceRecords, resourceDetails, resourceHydrationCause);
            foreach ((string key, FirstLivePreparedResource prepared) in resourceRecords.ToArray())
            {
                if (!IsKnownIdleResource(prepared.Record)) continue;
                resourceDetails.Details.TryGetValue(key, out ResourceScanDetail? detail);
                resourceRecords[key] = ApplyResourceScanDetail(prepared, detail);
            }
        }

        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
        {
            LastResourceCompletenessReport = state.ResourceCompleteness.Build(
                request.RunId,
                request.ServerId,
                request.WorldId,
                request.TileWidth,
                request.TileHeight,
                resourceRecords.Values,
                resourceDetails.TargetCount,
                resourceDetails.RequestCount,
                resourceDetails.CacheBeforeCount,
                resourceDetails.SendFailureCount,
                resourceDetails.ReadyCount,
                resourceDetails.Error);
            LastResourceCompletenessReport.RequireReconciledBeforePublication(
                state.ValidatedProbeResponses, resourceRecords.Count);
        }
        state.ResourceHydrationMilliseconds = (Now() - resourceStarted).TotalMilliseconds;
        state.ResourceDetailNativeRequests = resourceDetails.RequestCount;
        state.ResourceDetailNativeReady = resourceDetails.ReadyCount;

        DateTimeOffset monsterStarted = Now();
        MonsterProtectionDetailObservation protection = MonsterProtectionDetailObservation.Empty;
        int monsterInvasionBossCount = monsterRecords.Values.Count(item => item.ProtectionEligible);
        if (monsterInvasionBossCount > 0)
        {
            try
            {
                protection = await ProbeMonsterProtectionDetailsAsync(
                    session, request, monsterInvasionBossCount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                protection = MonsterProtectionDetailObservation.Failed(ex.Message);
            }
            foreach ((string key, FastMonsterPrepared prepared) in monsterRecords.ToArray())
            {
                if (!prepared.ProtectionEligible) continue;
                protection.Details.TryGetValue(prepared.Record.Uuid ?? string.Empty, out MonsterProtectionDetail? detail);
                monsterRecords[key] = ApplyMonsterProtectionDetail(prepared, detail);
            }
        }
        LastMonsterProtectionDetailMetrics = new MonsterProtectionDetailMetrics(
            monsterInvasionBossCount, protection.TargetCount,
            protection.RequestCount, protection.RetryCount, protection.ReadyCount, protection.Error);
        state.MonsterHydrationMilliseconds = (Now() - monsterStarted).TotalMilliseconds;
        state.MonsterDetailNativeRequests = protection.RequestCount;
        state.MonsterDetailNativeReady = protection.ReadyCount;

        DateTimeOffset projectionStarted = Now();
        var buckets = new Dictionary<int, List<MapStoredRecord>>();
        if (request.SelectedTypes.Contains("city", StringComparer.Ordinal))
            foreach (FirstLivePreparedResource item in cityRecords.Values)
                AddRecordToBlock(buckets, item.Import.X, item.Import.Y, item.Record);
        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
            foreach (FirstLivePreparedResource item in resourceRecords.Values)
                AddRecordToBlock(buckets, item.Import.X, item.Import.Y, item.Record);
        if (request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal))
            foreach (FastDispatchPrepared item in dispatchRecords.Values)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("ghost", StringComparer.Ordinal))
            foreach (FastGhostPrepared item in ghostRecords.Values)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("treasure", StringComparer.Ordinal))
            foreach (FastTreasurePrepared item in treasureRecords.Values)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (IncludesMonsterSource(request))
            foreach (FastMonsterPrepared item in monsterRecords.Values)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("truck", StringComparer.Ordinal))
            foreach (FastTrainPrepared item in trainRecords.Values.Where(item => item.Record.Kind == "truck"))
                if (FastTruckArrivalIsAfter(item.Record, Now().ToUnixTimeMilliseconds()))
                    AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("railway", StringComparer.Ordinal))
            foreach (FastTrainPrepared item in trainRecords.Values.Where(item => item.Record.Kind == "railway"))
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        Dictionary<int, MapStoredRecord[]> recordsByBlock = buckets.ToDictionary(
            pair => pair.Key, pair => pair.Value.ToArray());
        var captures = new List<MapScanBlockCapture>(logicalBlocks.Count);
        foreach (MapScanTargetBlock block in logicalBlocks)
        {
            IReadOnlyList<MapStoredRecord> records = recordsByBlock.TryGetValue(
                block.BlockIndex, out MapStoredRecord[]? value) ? value : Array.Empty<MapStoredRecord>();
            string payload = JsonSerializer.Serialize(new
            {
                protocol = "current_fast_full_world_v2",
                blockIndex = block.BlockIndex,
                coverage = "all_four_lod0_aoi_cells_proven_by_full_map_union",
                recordsInBlock = records.Count,
                coveredAoiCells = covered.Count,
                idleResourceCount,
                resourceDetailTargetCount = resourceDetails.TargetCount,
                resourceDetailRequestCount = resourceDetails.RequestCount,
                resourceDetailReadyCount = resourceDetails.ReadyCount,
                resourceDetailSendFailureCount = resourceDetails.SendFailureCount,
                resourceDetailError = resourceDetails.Error,
                monsterInvasionBossCount,
                monsterProtectionDetailTargetCount = protection.TargetCount,
                monsterProtectionDetailRequestCount = protection.RequestCount,
                monsterProtectionDetailRetryCount = protection.RetryCount,
                monsterProtectionDetailReadyCount = protection.ReadyCount,
                monsterProtectionDetailError = protection.Error,
            }, JsonOptions.Default);
            captures.Add(new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, payload, records));
        }
        state.ProjectionMilliseconds = (Now() - projectionStarted).TotalMilliseconds;
        state.OldestCapturedAgeMilliseconds = cityRecords.Values.Select(item => item.Record.UpdatedAt)
            .Concat(resourceRecords.Values.Select(item => item.Record.UpdatedAt))
            .Concat(monsterRecords.Values.Select(item => item.Record.UpdatedAt))
            .Concat(trainRecords.Values.Select(item => item.Record.UpdatedAt))
            .Concat(dispatchRecords.Values.Select(item => item.Record.UpdatedAt))
            .Concat(ghostRecords.Values.Select(item => item.Record.UpdatedAt))
            .Concat(treasureRecords.Values.Select(item => item.Record.UpdatedAt))
            .DefaultIfEmpty(Now().ToUnixTimeMilliseconds())
            .Min();
        state.OldestCapturedAgeMilliseconds = Math.Max(0, Now().ToUnixTimeMilliseconds() - state.OldestCapturedAgeMilliseconds);
        WriteFastFullWorldMetrics(state, "prepared");
        fastFullWorldResumeState = null;
        progress?.Invoke(new MapScanSourceProgress(100));
        return captures;
    }

    private async Task<OverviewMapScanSession> RequireReadyOrResumeSessionAsync(
        MapScanExecutionRequest request,
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession? current = getSession();
        if (current is not null) return current;
        FastFullWorldResumeState? resume = fastFullWorldResumeState;
        if (resume is null || resume.RunId != request.RunId || waitForHealthySession is null)
            return RequireReadySession();
        await waitForHealthySession(resume.Session, cancellationToken).ConfigureAwait(false);
        RequireSameSession(resume.Session);
        return resume.Session;
    }

    // MAP-019 (2026-10-23): Zombie Bosses arrive in the same game Monster list as ordinary monsters, and
    // same-session scans on 2175/2180/2198/2203 matched the camera scan. So "no Zombie Boss" is a real,
    // verified result when the sweep covered all 10,000 map areas AND that Monster list delivered monsters
    // (proving the source worked). Only an unproven source still fails (and falls back to the camera).
    internal static void RequireZombieBossSourceRows(MapScanExecutionRequest request, bool hasZombieBossRows,
        bool monsterSourceVerifiedComplete = false)
    {
        if (IncludesZombieBoss(request) && !hasZombieBossRows && !monsterSourceVerifiedComplete)
            throw new MapScanSourceValidationException(
                "Zombie Boss source returned no confirmed rows; empty March-source coverage could not be verified. Retry the scan.");
    }

    private static int FirstUncoveredAoiColumn(IReadOnlySet<int> covered, int rowStart)
    {
        for (int column = 0; column < FastCityAoiBlockCount; column++)
        {
            bool complete = true;
            for (int row = rowStart; row < rowStart + FastFullWorldAoiRows; row++)
            {
                if (covered.Contains(checked(row * FastCityAoiBlockCount + column))) continue;
                complete = false;
                break;
            }
            if (!complete) return column;
        }
        return -1;
    }

    // The bounded City lens walks the entire AOI grid in one outer iteration;
    // its first gap can move beyond the serial route's current ten-row band.
    // Do not evaluate serial-band placement before overriding its result: once
    // that band is covered its missing-row set is empty and correctly rejected
    // by TargetY. Keep that strict serial guard on the serial route only.
    internal static (int TargetY, int[] MissingRows) SelectFullWorldProbeY(
        bool boundedLens, IReadOnlySet<int> covered, int firstGap,
        int aoiRowStart, int gapColumn, bool nativeEightRowWindowObserved,
        int? windowFirstRowOffset)
    {
        if (boundedLens)
        {
            if (firstGap is < 0 or >= 10000)
                throw new ArgumentOutOfRangeException(nameof(firstGap));
            return (Math.Min(995, (firstGap / FastCityAoiBlockCount + 2) * FastCityAoiBlockSize + 5), []);
        }
        int[] missingRows = Enumerable.Range(aoiRowStart, FastFullWorldAoiRows)
            .Where(rowIndex => !covered.Contains(checked(rowIndex * FastCityAoiBlockCount + gapColumn)))
            .ToArray();
        return (MapFullWorldAoiPlacement.TargetY(aoiRowStart, missingRows,
            nativeEightRowWindowObserved, windowFirstRowOffset), missingRows);
    }

    internal static void ValidateAdaptiveRowFootprint(
        int[] indices,
        int rowStart,
        MapScanExecutionRequest request,
        OverviewMapScanSession session,
        int targetX,
        int targetY,
        int attempt)
    {
        int[] rows = indices.Select(index => index / FastCityAoiBlockCount).Distinct().Order().ToArray();
        int[] columns = indices.Select(index => index % FastCityAoiBlockCount).Distinct().Order().ToArray();
        string detail =
            $"server={request.ServerId},world={request.WorldId},session={session.SessionId},attempt={attempt}," +
            $"target=({targetX},{targetY}),requestedCount=8,nativeCurrentSetCount={indices.Length}," +
            $"rowStart={rowStart},rows=[{string.Join(',', rows)}],columns=[{string.Join(',', columns)}]," +
            $"indices=[{string.Join(',', indices.Order())}]";
        // v26 windows measured on a real client: 7-11 rows depending on camera height / lens.
        // Tall windows (lens/FOV study: up to ~54 rows, capped near 160 AOIs) are accepted when they overlap the band.
        if (rows.Length is < FastFullWorldAoiRows - 3 or > 60 ||
            rows.Length == 0 ||
            // The window may start anywhere that still overlaps the band: placement targets the first
            // missing row, and the exact 10,000-cell union (not this shape check) proves coverage.
            rows.Zip(rows.Skip(1), (top, bottom) => bottom - top).Any(delta => delta != 1) ||
            columns.Length is < 2 or > 5 ||
            columns.Zip(columns.Skip(1), (left, right) => right - left).Any(delta => delta != 1) ||
            indices.Length != rows.Length * columns.Length ||
            !indices.Contains((targetY / 10) * FastCityAoiBlockCount + targetX / 10) ||
            rows.All(row => row < rowStart || row >= rowStart + FastFullWorldAoiRows))
            throw new InvalidDataException(
                "Fast full-world adaptive acquisition returned a non-rectangular v18 AOI footprint: " + detail + ".");
        var expected = rows.SelectMany(row => columns.Select(column => checked(row * FastCityAoiBlockCount + column))).ToHashSet();
        if (expected.Count != indices.Length || indices.Any(index => !expected.Contains(index)))
            throw new InvalidDataException(
                "Fast full-world adaptive acquisition returned an inconsistent AOI footprint: " + detail + ".");
    }

    internal static void ValidateBoundedLensFootprint(int[] indices, int firstGap, int targetX, int targetY)
    {
        var actual = indices.ToHashSet();
        if (indices.Length is < 20 or > 160 || actual.Count != indices.Length ||
            actual.Any(index => index is < 0 or >= 10000) || !actual.Contains(firstGap) ||
            !actual.Contains(targetY / 10 * 100 + targetX / 10))
            throw new InvalidDataException("Bounded lens did not freshly cover the requested target and uncovered cell.");
        int[] rows = actual.Select(i => i / 100).Distinct().Order().ToArray();
        int[] columns = actual.Select(i => i % 100).Distinct().Order().ToArray();
        if (rows.Zip(rows.Skip(1), (a,b) => b-a).Any(d => d != 1) ||
            columns.Zip(columns.Skip(1), (a,b) => b-a).Any(d => d != 1) ||
            actual.Count != rows.Length * columns.Length)
            throw new InvalidDataException("Bounded lens native AOI rectangle has holes or stale geometry.");
    }

    internal static void MergeLatestCityByOwner(Dictionary<string, FirstLivePreparedResource> rows,
        FirstLivePreparedResource candidate)
    {
        using var data = JsonDocument.Parse(candidate.Record.DataJson);
        if (candidate.Record.Kind != "city" || candidate.Record.ServerId <= 0 ||
            candidate.Record.UpdatedAt <= 0 ||
            !data.RootElement.TryGetProperty("ownerUid", out var owner) ||
            owner.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(owner.GetString()) || owner.GetString() == "0")
            throw new InvalidDataException("City aggregation requires an actual native owner identity.");
        // The scan state itself is tied to an exact profile/session/run. Within
        // that state a player has one current observed location per server.
        string key = candidate.Record.ServerId.ToString(CultureInfo.InvariantCulture) + ":" + owner.GetString();
        if (!rows.TryGetValue(key, out var prior))
        {
            rows.Add(key, candidate);
            return;
        }
        if (candidate.Record.UpdatedAt > prior.Record.UpdatedAt)
        {
            rows[key] = candidate;
            return;
        }
        if (candidate.Record.UpdatedAt == prior.Record.UpdatedAt &&
            candidate.Record.PointIndex != prior.Record.PointIndex)
            throw new InvalidDataException("City owner has conflicting locations at the same observation time.");
    }

    private static bool IncludesMonsterSource(MapScanExecutionRequest request) =>
        request.SelectedTypes.Contains("monster", StringComparer.Ordinal) ||
        request.SelectedTypes.Contains("zombie_boss", StringComparer.Ordinal);

    // Zombie Boss (MonsterInvasionBoss special 10) rides the same shared AOI sweep as
    // Monster; the serialized game-native detail queue runs whenever it is selected.
    private static bool IncludesZombieBoss(MapScanExecutionRequest request) =>
        request.SelectedTypes.Contains("zombie_boss", StringComparer.Ordinal);

    private static bool IsZombieBossOnly(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count == 1 &&
        request.SelectedTypes.Contains("zombie_boss", StringComparer.Ordinal);

    private static bool IsTrainListOnly(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count >= 1 &&
        request.SelectedTypes.All(type => type is "truck" or "railway");

    private static bool IsMonsterOnly(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count == 1 && IncludesMonsterSource(request);

    private static bool IsOverviewSessionAdmissionGap(Exception error) =>
        error is InvalidDataException &&
        string.Equals(
            error.Message,
            "Fast world batch failed: overview_session_unavailable",
            StringComparison.Ordinal);

    private static bool CanUseFastCityBatch(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count is >= 1 and <= 9 &&
        request.SelectedTypes.All(type => type is "city" or "resource" or "monster" or "zombie_boss" or "truck" or "railway" or "dispatch" or "ghost" or "treasure") &&
        request.WorldId == 0 && request.TileWidth == 1000 && request.TileHeight == 1000;

    // Same-session Oct 14 baseline/FOV20 six-round native City comparison:
    // 4,114 current owners in every round, zero omitted/new owner identities,
    // one source-timestamped relocation, 10,000 AOIs and exact restoration.
    // City-only median 227.912s -> 86.179s. Mixed FOV20 missed unexpired
    // Trucks, and Resource snapshots retained unexplained temporal changes:
    // neither is admitted to this production policy.
    internal static int ExpectedCoverageFov(CurrentClientMapBlockSourceHooks? hooks) => hooks?.CoverageFovOverride ?? CanonicalCoverageFov;

    internal static double? ExpectedCoverageZoom(CurrentClientMapBlockSourceHooks? hooks) =>
        hooks?.DisableCoverageZoomNormalization == true ? null : hooks?.CoverageZoomOverride ?? CanonicalCoverageZoom;

    internal static bool ShouldUseBoundedFullWorldCameraLens(
        MapScanExecutionRequest request, CurrentClientMapBlockSourceHooks? hooks) =>
        hooks?.ForceLegacyFullWorldLens != true &&
        (hooks?.ExperimentalBoundedCameraLens == true ||
         request.SelectedTypes.Count == 1 && request.SelectedTypes[0] == "city");

    private async Task<FastCityBatchObservation> ProbeFastCityBatchAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int groupStartColumn,
        int groupStartRow,
        int targetX,
        int targetY,
        CancellationToken cancellationToken)
    {
        string requestId = "fastcity" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "bulk-aoi-diagnostic.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "bulk-aoi-diagnostic-result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            "viewLevel=-1",
            "requestMode=coverage",
            $"coverageFov={(ShouldUseBoundedFullWorldCameraLens(request, hooks) && groupStartColumn < 0 ? 20 : ExpectedCoverageFov(hooks))}",
            ExpectedCoverageZoom(hooks) is double coverageZoom
                ? $"coverageZoom={coverageZoom.ToString("0.###", CultureInfo.InvariantCulture)}"
                : "coverageZoomNormalization=disabled",
            $"targetTileX={targetX.ToString(CultureInfo.InvariantCulture)}",
            $"targetTileY={targetY.ToString(CultureInfo.InvariantCulture)}",
            "requestedCount=8",
            "holdMilliseconds=0",
            $"homeTileX={(request.PlayerTileX ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"homeTileY={(request.PlayerTileY ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"includeCity={request.SelectedTypes.Contains("city", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeResource={request.SelectedTypes.Contains("resource", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeMonster={IncludesMonsterSource(request).ToString().ToLowerInvariant()}",
            $"includeMonsterProtection={IncludesZombieBoss(request).ToString().ToLowerInvariant()}",
            $"includeTrain={(request.SelectedTypes.Contains("truck", StringComparer.Ordinal) || request.SelectedTypes.Contains("railway", StringComparer.Ordinal)).ToString().ToLowerInvariant()}",
            $"observeMarchFreshness={(hooks?.ObserveMarchFreshness == true).ToString().ToLowerInvariant()}",
            $"includeDispatch={request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeGhost={request.SelectedTypes.Contains("ghost", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeTreasure={request.SelectedTypes.Contains("treasure", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeResourceDetails={request.SelectedTypes.Contains("resource", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            // A native footprint can be fresh while same-tick camera restoration
            // drops known spatial rows. All selected kinds require the native
            // point/march snapshot before restoring the target view.
            "deferRestoreUntilResponse=true",
            string.Empty,
        });
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + FastCityProbeTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                byte[] bytes = ReadAllBytes(resultPath);
                try { var validated = ValidateFastCityBatchResult(
                    root.Value,
                    bytes,
                    resultPath,
                    requestId,
                    startedAt,
                    session,
                    request,
                    groupStartColumn,
                    groupStartRow,
                    targetX,
                    targetY);
                    if (hooks?.ObserveMarchFreshness == true)
                    {
                        // Diagnostic admission only: validating two native manager
                        // snapshots is NOT a March request/reply/queue readiness proof.
                        // Null, truncated or duplicate UUID receipts must fail closed.
                        ValidateNativeMarchTelemetry(root.Value);
                        JsonElement before = root.Value.GetProperty("marchSourceBefore");
                        JsonElement atPoint = root.Value.GetProperty("marchSourceAtPointReady");
                        string directory = Path.Combine(probeRuntimeRoot, "march-freshness-owned");
                        Directory.CreateDirectory(directory);
                        File.WriteAllText(Path.Combine(directory, requestId + ".json"),
                            JsonSerializer.Serialize(new { scanRunId = request.RunId, requestId, serverId = request.ServerId,
                                targetX, targetY, before, atPoint }));
                    }
                    return validated; }
                catch (InvalidDataException)
                {
                    if (nativeBatchNegativesRecorded++ < 8 &&
                        (hooks is null || ShouldUseBoundedFullWorldCameraLens(request, hooks)))
                    {
                        try
                        {
                            string negativePath = Path.Combine(probeRuntimeRoot,"batch-negatives");
                            Directory.CreateDirectory(negativePath);
                            File.WriteAllBytes(Path.Combine(negativePath,requestId+".json"),bytes);
                        }
                        catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                    throw;
                }
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client fast City batch did not return a correlated result.");
    }

    internal static void ValidateNativeMarchTelemetry(JsonElement response)
    {
        static InvalidDataException Bad(string reason) =>
            new("Passive native March telemetry invalid: " + reason);
        static (double Time, int Count) Check(JsonElement response, string field)
        {
            if (!response.TryGetProperty(field, out JsonElement snapshot) ||
                snapshot.ValueKind != JsonValueKind.Object ||
                !snapshot.TryGetProperty("available", out JsonElement available) ||
                available.ValueKind != JsonValueKind.True ||
                !snapshot.TryGetProperty("observedAtRuntimeSeconds", out JsonElement time) ||
                time.ValueKind != JsonValueKind.Number ||
                !time.TryGetDouble(out double seconds) ||
                !double.IsFinite(seconds) || seconds <= 0 ||
                !snapshot.TryGetProperty("marchDictionaryCount", out JsonElement count) ||
                count.ValueKind != JsonValueKind.Number ||
                !count.TryGetInt32(out int reportedCount) || reportedCount < 0 ||
                !snapshot.TryGetProperty("marchUuidSampleCapped", out JsonElement capped) ||
                capped.ValueKind != JsonValueKind.False ||
                !snapshot.TryGetProperty("marchUuids", out JsonElement uuids) ||
                uuids.ValueKind != JsonValueKind.Array)
                throw Bad(field + " missing, stale, truncated or unavailable");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            int observed = 0;
            foreach (JsonElement uuid in uuids.EnumerateArray())
            {
                if (uuid.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(uuid.GetString()) ||
                    !unique.Add(uuid.GetString()!))
                    throw Bad(field + " has a blank or duplicate March UUID");
                observed++;
            }
            if (observed != reportedCount)
                throw Bad(field + " March UUID count does not match native dictionary");
            return (seconds, observed);
        }
        (double start, _) = Check(response, "marchSourceBefore");
        (double ready, _) = Check(response, "marchSourceAtPointReady");
        if (ready < start || ready - start > FastCityProbeTimeout.TotalSeconds)
            throw Bad("native point-ready snapshot is out of the correlated probe time window");
    }

    internal async Task<IReadOnlyList<MapStoredRecord>> CaptureRailwayListSnapshotForTestAsync(
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }
        CurrentClientMapContext context = await GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
        var request = new MapScanExecutionRequest(
            "trainlist" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            context.ServerId, context.WorldId, context.TileWidth, context.TileHeight,
            ["railway"], 20, 2, context.PlayerTileX, context.PlayerTileY);
        FastTrainListSnapshot snapshot = await ProbeTrainListAsync(
            session, request, cancellationToken).ConfigureAwait(false);
        return snapshot.Rows.Select(row => row.Record).ToArray();
    }

    internal async Task<CurrentClientTrainListCoverageResult> GetTrainListCoverageAsync(
        CancellationToken cancellationToken)
    {
        OverviewMapScanSession session = RequireReadySession();
        if (waitForHealthySession is { } waitForHealthy)
        {
            await waitForHealthy(session, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
        }
        CurrentClientMapContext context = await GetCurrentContextAsync(cancellationToken).ConfigureAwait(false);
        var request = new MapScanExecutionRequest(
            "traincoverage" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            context.ServerId, context.WorldId, context.TileWidth, context.TileHeight,
            ["truck", "railway"], 20, 2, context.PlayerTileX, context.PlayerTileY,
            ScanMode: "fast", LaunchSessionId: context.LaunchSessionId, LiveServerId: context.ServerId);
        FastTrainListSnapshot snapshot = await ProbeTrainListAsync(
            session, request, cancellationToken).ConfigureAwait(false);
        return new CurrentClientTrainListCoverageResult(
            snapshot.LiveServerId, snapshot.MatchServerIds,
            snapshot.TruckServerIds, snapshot.RailwayServerIds);
    }

    private async Task<FastTrainListSnapshot> ProbeTrainListAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        CancellationToken cancellationToken)
    {
        string requestId = "trainlist" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "train-list-diagnostic.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "train-list-diagnostic-result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"liveServerId={(request.LiveServerId ?? request.ServerId).ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            string.Empty,
        });
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + ProbeTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                JsonElement value = root.Value;
                if (!MatchesInt(value, "schemaVersion", 1) ||
                    !MatchesString(value, "probeVersion", ProbeVersion) ||
                    !MatchesString(value, "profileId", session.ProfileId) ||
                    !MatchesString(value, "launchSessionId", session.SessionId) ||
                    !MatchesString(value, "challenge", session.Challenge) ||
                    !MatchesInt(value, "gamePid", session.GamePid) ||
                    !MatchesInt(value, "serverId", request.ServerId) ||
                    !MatchesString(value, "scanRunId", request.RunId))
                    throw new InvalidDataException("Train-list result did not match the active owned game session.");
                RequireFreshCaptureTime(value, startedAt);
                if (!MatchesString(value, "state", "proven"))
                {
                    string error = ReadOptionalString(value, "error") ?? "unknown Train-list refresh failure";
                    throw new InvalidDataException("Train-list refresh failed: " + error);
                }
                if (!MatchesBool(value, "refreshObserved", true))
                    throw new InvalidDataException("Train-list refresh did not observe the official RefreshTrainListData event.");

                int expectedLiveServerId = request.LiveServerId ?? request.ServerId;
                int liveServerId = RequirePositiveInt(value, "liveServerId");
                if (liveServerId != expectedLiveServerId)
                    throw new InvalidDataException("Train-list result live server changed during acquisition.");
                int[] matchServerIds = ReadTrainServerIds(value, "matchServerIds");
                int[] truckServerIds = ReadTrainServerIds(value, "truckServerIds");
                int[] railwayServerIds = ReadTrainServerIds(value, "railwayServerIds");
                if (request.ServerId != liveServerId && !matchServerIds.Contains(request.ServerId))
                    throw new InvalidDataException("Train-list target server is not covered by the refreshed matchServers snapshot.");

                // A Train-list snapshot is a whole-list source, not an AOI response.
                // Reuse exact Train normalization with every valid LOD0 AOI admitted.
                HashSet<int> allAoi = Enumerable.Range(0, FastCityAoiBlockCount * FastCityAoiBlockCount).ToHashSet();
                IReadOnlyList<FastTrainPrepared> rows = PrepareTrainRecords(value, request, allAoi, startedAt);
                return new FastTrainListSnapshot(rows, liveServerId, matchServerIds, truckServerIds, railwayServerIds);
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client Train-list refresh did not return a correlated result.");
    }

    private async Task<IReadOnlyList<FastMonsterPrepared>> ProbeCoarseMonsterMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        CancellationToken cancellationToken)
    {
        (int X, int Y)[] targets =
        [
            (500, 500), (100, 100), (900, 900), (100, 900), (900, 100),
            (250, 750), (750, 250), (250, 250), (750, 750),
        ];
        CoarseMonsterTargetUnavailableException? lastUnavailable = null;
        foreach ((int targetX, int targetY) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await ProbeCoarseMonsterMapAtTargetAsync(
                    session, request, targetX, targetY, cancellationToken).ConfigureAwait(false);
            }
            catch (CoarseMonsterTargetUnavailableException ex)
            {
                lastUnavailable = ex;
            }
        }
        if (lastUnavailable is not null)
            throw new InvalidDataException("LOD2 Monster acquisition had no usable remote target.", lastUnavailable);
        throw new InvalidDataException("LOD2 Monster acquisition had no usable remote target.");
    }

    private async Task<IReadOnlyList<FastMonsterPrepared>> ProbeCoarseMonsterMapAtTargetAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int targetX,
        int targetY,
        CancellationToken cancellationToken)
    {
        string requestId = "monsterlod2" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "bulk-aoi-diagnostic.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "bulk-aoi-diagnostic-result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            "viewLevel=-1",
            "requestMode=zoom",
            $"targetTileX={targetX.ToString(CultureInfo.InvariantCulture)}",
            $"targetTileY={targetY.ToString(CultureInfo.InvariantCulture)}",
            "requestedCount=160",
            "holdMilliseconds=0",
            $"homeTileX={(request.PlayerTileX ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"homeTileY={(request.PlayerTileY ?? -1).ToString(CultureInfo.InvariantCulture)}",
            "includeMonster=true",
            $"includeMonsterProtection={IsZombieBossOnly(request).ToString().ToLowerInvariant()}",
            "includeTrain=false",
            string.Empty,
        });
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + FastCityProbeTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
                return ValidateCoarseMonsterMapResult(
                    root.Value, requestId, startedAt, session, request, targetX, targetY);
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client LOD2 Monster acquisition did not return a correlated result.");
    }

    private IReadOnlyList<FastMonsterPrepared> ValidateCoarseMonsterMapResult(
        JsonElement root,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int targetX,
        int targetY)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesString(root, "requestMode", "zoom") ||
            !MatchesBool(root, "includeMonster", true) ||
            !MatchesBool(root, "includeMonsterProtection", IsZombieBossOnly(request)) ||
            !MatchesBool(root, "includeTrain", false))
            throw new InvalidDataException("LOD2 Monster result did not match the active owned game session.");
        if (!MatchesInt(root, "requestedCount", 160) ||
            !MatchesInt(root, "holdMilliseconds", 0) ||
            !MatchesInt(root, "homeTileX", request.PlayerTileX ?? -1) ||
            !MatchesInt(root, "homeTileY", request.PlayerTileY ?? -1) ||
            !MatchesInt(root, "viewLevel", -1) ||
            !MatchesInt(root, "targetTileX", targetX) ||
            !MatchesInt(root, "targetTileY", targetY))
            throw new InvalidDataException("LOD2 Monster result did not match the requested acquisition parameters.");
        RequireFreshCaptureTime(root, startedAt);
        if (!MatchesString(root, "state", "proven"))
        {
            string error = ReadOptionalString(root, "error") ?? "unknown LOD2 Monster acquisition failure";
            if (error is "target_aoi_still_current" or "target_aoi_already_loaded" or "target_point_already_loaded")
                throw new CoarseMonsterTargetUnavailableException(error);
            throw new InvalidDataException("LOD2 Monster acquisition failed: " + error);
        }
        if (!MatchesBool(root, "responseFlagsTransitioned", true) ||
            !MatchesBool(root, "cameraTileStable", true) ||
            !MatchesBool(root, "positionRestoredBeforeResponse", true) ||
            !MatchesString(root, "requestMethod", "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift") ||
            !MatchesBool(root, "zoomWholeWorldCoarse", true))
            throw new InvalidDataException("LOD2 Monster acquisition did not prove the native response/restoration contract.");
        if (!MatchesInt(root, "serverLod", 0) || !MatchesInt(root, "blockSize", 10) || !MatchesInt(root, "blockCount", 100) ||
            !MatchesInt(root, "postServerLod", 2) || !MatchesInt(root, "postBlockSize", 1000) || !MatchesInt(root, "postBlockCount", 1) ||
            !MatchesInt(root, "zoomFinalServerLod", 2) || !MatchesInt(root, "zoomFinalBlockSize", 1000) || !MatchesInt(root, "zoomFinalBlockCount", 1) ||
            !MatchesInt(root, "restoredServerLod", 0) || !MatchesInt(root, "restoredBlockSize", 10) || !MatchesInt(root, "restoredBlockCount", 100))
            throw new InvalidDataException("LOD2 Monster acquisition did not make and restore the proven v18 LOD transition.");
        if (!root.TryGetProperty("preTileX", out JsonElement preX) || !preX.TryGetInt32(out int px) ||
            !root.TryGetProperty("preTileY", out JsonElement preY) || !preY.TryGetInt32(out int py) ||
            !MatchesInt(root, "restoredTileX", px) || !MatchesInt(root, "restoredTileY", py))
            throw new InvalidDataException("LOD2 Monster acquisition did not restore the exact original camera tile.");

        var allAoi = Enumerable.Range(0, FastCityAoiBlockCount * FastCityAoiBlockCount).ToHashSet();
        IReadOnlyList<FastMonsterPrepared> monsters = PrepareMonsterRecords(root, request, allAoi, startedAt);
        int doomsdayBossCount = RequireNonNegativeInt(root, "doomsdayBossCount");
        int normalizedDoomsdayBossCount = monsters.Count(item =>
            item.Record.DataJson.Contains("\"source\":\"DataCenter.LWDoomsdayManager.", StringComparison.Ordinal));
        bool zombieBossOnly = IsZombieBossOnly(request);
        if (zombieBossOnly ? doomsdayBossCount != 0 : doomsdayBossCount != normalizedDoomsdayBossCount)
            throw new InvalidDataException("LOD2 Monster Doomsday boss count changed during normalization.");
        int bossCount = RequireNonNegativeInt(root, "monsterInvasionBossCount");
        int protectionTargets = RequireNonNegativeInt(root, "monsterProtectionDetailTargetCount");
        int protectionRequests = RequireNonNegativeInt(root, "monsterProtectionDetailRequestCount");
        int protectionReady = RequireNonNegativeInt(root, "monsterProtectionDetailReadyCount");
        int normalizedBossCount = monsters.Count(item => item.ProtectionEligible);
        bool countersValid = zombieBossOnly
            ? bossCount == normalizedBossCount &&
              protectionTargets == bossCount &&
              protectionRequests <= protectionTargets &&
              protectionReady <= protectionRequests
            : normalizedBossCount == 0 &&
              protectionTargets == 0 &&
              protectionRequests == 0 &&
              protectionReady == 0;
        if (!countersValid)
            throw new InvalidDataException("LOD2 Monster/Zombie Boss protection counters changed during normalization.");
        if (request.PlayerTileX is not null && request.PlayerTileY is not null && monsters.Any(item => item.Record.Distance is null))
            throw new InvalidDataException("LOD2 Monster acquisition omitted home-relative Distance.");
        return monsters;
    }

    private async Task<MonsterProtectionDetailObservation> ProbeMonsterProtectionDetailsAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int expectedTargetCount,
        CancellationToken cancellationToken)
    {
        string requestId = "monsterprotection" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "monster-protection-detail.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "monster-protection-detail-result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            $"expectedTargetCount={expectedTargetCount.ToString(CultureInfo.InvariantCulture)}",
            string.Empty,
        });
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + MonsterProtectionProbeTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
                return ValidateMonsterProtectionDetailResult(
                    root.Value, requestId, startedAt, session, request, expectedTargetCount);
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client Monster Protection enrichment did not return a correlated result.");
    }

    private MonsterProtectionDetailObservation ValidateMonsterProtectionDetailResult(
        JsonElement root,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int expectedTargetCount)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesString(root, "scanRunId", request.RunId) ||
            !MatchesInt(root, "serverId", request.ServerId) ||
            !MatchesInt(root, "expectedTargetCount", expectedTargetCount) ||
            !MatchesString(root, "state", "completed"))
            throw new InvalidDataException("Monster Protection enrichment result did not match the active owned scan.");
        RequireFreshCaptureTime(root, startedAt);
        int targetCount = RequireNonNegativeInt(root, "targetCount");
        int requestCount = RequireNonNegativeInt(root, "requestCount");
        int retryCount = RequireNonNegativeInt(root, "retryCount");
        int readyCount = RequireNonNegativeInt(root, "readyCount");
        if (targetCount < expectedTargetCount || requestCount > targetCount || readyCount > requestCount)
            throw new InvalidDataException("Monster Protection enrichment counters are inconsistent.");
        if (!root.TryGetProperty("details", out JsonElement detailsValue) || detailsValue.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Monster Protection enrichment details are missing.");
        var details = new Dictionary<string, MonsterProtectionDetail>(StringComparer.Ordinal);
        foreach (JsonElement item in detailsValue.EnumerateArray())
        {
            string uuid = RequiredString(item, "uuid");
            bool received = item.TryGetProperty("received", out JsonElement receivedValue) && receivedValue.ValueKind == JsonValueKind.True;
            bool active = item.TryGetProperty("isProtected", out JsonElement activeValue) && activeValue.ValueKind == JsonValueKind.True;
            long endTime = item.TryGetProperty("protectionEndTime", out JsonElement endValue) && endValue.TryGetInt64(out long parsedEnd)
                ? parsedEnd : 0;
            if (!received && (active || endTime != 0))
                throw new InvalidDataException("Unreceived Monster Protection detail carried authoritative state.");
            if (!details.TryAdd(uuid, new MonsterProtectionDetail(received, active, endTime)))
                throw new InvalidDataException("Monster Protection enrichment returned a duplicate UUID.");
        }
        if (details.Count != targetCount)
            throw new InvalidDataException("Monster Protection enrichment target/detail counts differ.");
        string? error = ReadOptionalString(root, "error");
        return new MonsterProtectionDetailObservation(targetCount, requestCount, retryCount, readyCount, details, error);
    }

    private static FastMonsterPrepared ApplyMonsterProtectionDetail(
        FastMonsterPrepared prepared,
        MonsterProtectionDetail? detail)
    {
        // An unresolved optional reply is not negative authority. Keep any
        // game-owned protection state already captured with the base Monster
        // row (for example from MonsterProtectionManager after a prior Jump).
        if (detail?.Received != true) return prepared;

        JsonObject data = JsonNode.Parse(prepared.Record.DataJson)?.AsObject()
            ?? throw new InvalidDataException("Monster record JSON is unavailable during protection enrichment.");
        bool known = true;
        bool active = detail.Active;
        long endTime = active && detail.EndTime > 0 ? detail.EndTime : 0;
        data["monsterProtectionKnown"] = known;
        data["monsterProtectionActive"] = active;
        data["monsterProtectionEndTime"] = endTime;
        data.Remove("shieldEndTime");
        long? shieldEndTime = endTime > 0 ? endTime : null;
        if (shieldEndTime is not null) data["shieldEndTime"] = shieldEndTime.Value;
        return prepared with
        {
            Record = prepared.Record with
            {
                ShieldEndTime = shieldEndTime,
                DataJson = data.ToJsonString(JsonOptions.Default),
            },
        };
    }

    private async Task<ResourceScanDetailObservation> ProbeResourceScanDetailsAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int resourceRecordCount,
        CancellationToken cancellationToken)
    {
        string requestId = "resourcedetail" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string commandPath = Path.Combine(probeRuntimeRoot, "resource-scan-detail.txt");
        string resultPath = Path.Combine(probeRuntimeRoot, "resource-scan-detail-result.json");
        DateTimeOffset startedAt = Now();
        string command = string.Join('\n', new[]
        {
            "schema=1",
            $"probeVersion={ProbeVersion}",
            $"requestId={requestId}",
            $"profileId={session.ProfileId}",
            $"launchSessionId={session.SessionId}",
            $"challenge={session.Challenge}",
            $"gamePid={session.GamePid.ToString(CultureInfo.InvariantCulture)}",
            $"serverId={request.ServerId.ToString(CultureInfo.InvariantCulture)}",
            $"scanRunId={request.RunId}",
            string.Empty,
        });
        await WriteCommandAsync(commandPath, command, cancellationToken).ConfigureAwait(false);
        DateTimeOffset deadline = startedAt + ResourceDetailProbeTimeout;
        while (Now() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement? root = TryReadJson(resultPath);
            if (root is not null && MatchesString(root.Value, "requestId", requestId))
            {
                // Keep the first request-correlated native response BEFORE
                // validation or the Lua lane clears its state. A retry must
                // never overwrite the evidence of the original failure.
                try { RetainFirstResourceHydrationReply(probeRuntimeRoot, request.RunId, requestId, root.Value); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
                    JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
                {
                    Console.Error.WriteLine("Resource hydration reply retention unavailable: " + error.Message);
                }
                return ValidateResourceScanDetailResult(
                    root.Value, requestId, startedAt, session, request, resourceRecordCount);
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client Resource detail enrichment did not return a correlated result.");
    }

    internal static string RetainFirstResourceHydrationReply(
        string probeRoot, string runId, string requestId, JsonElement reply)
    {
        RequireResourceHydrationArtifactToken(requestId);
        if (reply.ValueKind != JsonValueKind.Object || !MatchesString(reply, "requestId", requestId))
            throw new InvalidDataException("Cannot retain a foreign Resource hydration response.");
        string folder = ResourceHydrationArtifactFolder(probeRoot, runId);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "first-reply.json");
        WriteResourceHydrationArtifactOnce(path, reply.GetRawText());
        return path;
    }

    private static string ResourceHydrationArtifactFolder(string probeRoot, string runId)
    {
        RequireResourceHydrationArtifactToken(runId);
        return Path.Combine(Path.GetFullPath(probeRoot), "resource-hydration-receipts", runId);
    }

    private static void RequireResourceHydrationArtifactToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 ||
            token.Any(value => !char.IsAsciiLetterOrDigit(value) && value is not ('-' or '_')))
            throw new InvalidDataException("Unsafe Resource hydration artifact identity.");
    }

    private static void WriteResourceHydrationArtifactOnce(string path, string json)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, ".receipt-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(Encoding.UTF8.GetBytes(json));
                file.Flush(flushToDisk: true);
            }
            try { File.Move(temporary, path, overwrite: false); }
            catch (IOException conflict) when (File.Exists(path) &&
                (conflict.HResult & 0xFFFF) is 80 or 183 or 17)
            {
                // Only the completed-final-file conflict is tolerated. A
                // partial temporary write is never a retained first receipt.
            }
        }
        finally
        {
            // This exact unique temporary belongs to this call; never remove
            // the pre-existing final receipt or another attempt's temporary.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
            { Console.Error.WriteLine("Resource diagnostic temporary cleanup unavailable: " + cleanupError.Message); }
        }
    }

    internal static void RequireFullWorldResourceHydration(
        string probeRoot, MapScanExecutionRequest request,
        IReadOnlyDictionary<string, FirstLivePreparedResource> resources,
        ResourceScanDetailObservation observation, Exception? originalCause = null)
    {
        try { RequireCompleteResourceHydration(resources, observation); }
        catch (InvalidDataException validationError)
        {
            Exception cause = originalCause ?? validationError;
            string? failurePath = null;
            try
            {
                string folder = ResourceHydrationArtifactFolder(probeRoot, request.RunId);
                Directory.CreateDirectory(folder);
                string replyPath = Path.Combine(folder, "first-reply.json");
                JsonElement? raw = null;
                if (File.Exists(replyPath))
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(replyPath, Encoding.UTF8));
                    raw = document.RootElement.Clone();
                }
                int? ReadCount(string field) => raw is { } value &&
                    value.TryGetProperty(field, out var item) && item.ValueKind == JsonValueKind.Number &&
                    item.TryGetInt32(out int count) ? count : null;
                string failureJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, scanRunId = request.RunId,
                    serverId = request.ServerId, worldId = request.WorldId,
                    phase = "fullworld_resource_hydration_validation_failed",
                    originalMessage = cause.Message, originalType = cause.GetType().Name,
                    validationMessage = validationError.Message,
                    nativeError = raw is { } value ? ReadOptionalString(value, "error") : null,
                    nativeTargetCount = ReadCount("targetCount"), nativeReadyCount = ReadCount("readyCount"),
                    nativeDetailCount = raw is { } detailsRoot &&
                        detailsRoot.TryGetProperty("details", out var nativeDetails) && nativeDetails.ValueKind == JsonValueKind.Array
                            ? (int?)nativeDetails.GetArrayLength() : null,
                    currentIdleCount = resources.Values.Count(item => IsKnownIdleResource(item.Record)),
                    observationTargetCount = observation.TargetCount, observationReadyCount = observation.ReadyCount,
                    observationDetailCount = observation.Details.Count, observationError = observation.Error,
                    rawReceiptLocator = File.Exists(replyPath) ? replyPath : null,
                }, JsonOptions.Default);
                failurePath = Path.Combine(folder, "first-failure.json");
                WriteResourceHydrationArtifactOnce(failurePath, failureJson);
            }
            catch (Exception diagnosticError) when (diagnosticError is IOException or UnauthorizedAccessException or InvalidDataException or
                JsonException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine("Resource hydration failure retention unavailable: " + diagnosticError.Message);
            }
            var terminal = new MapScanSourceValidationException(
                "Full-world Resource hydration validation failed: " + cause.Message, cause);
            if (failurePath is not null && File.Exists(failurePath)) terminal.Data["resourceHydrationFailureReceipt"] = failurePath;
            throw terminal;
        }
    }

    private static bool IsKnownIdleResource(MapStoredRecord record)
    {
        if (!string.Equals(record.Kind, "resource", StringComparison.Ordinal)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(record.DataJson);
            JsonElement root = document.RootElement;
            return root.TryGetProperty("rebuildGatherOccupancyKnown", out JsonElement known) &&
                   known.ValueKind == JsonValueKind.True &&
                   root.TryGetProperty("rebuildGatherOccupied", out JsonElement occupied) &&
                   occupied.ValueKind == JsonValueKind.False;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static void RequireCompleteResourceHydration(
        IReadOnlyDictionary<string, FirstLivePreparedResource> resources,
        ResourceScanDetailObservation observation)
    {
        string[] idleKeys = resources.Where(pair => IsKnownIdleResource(pair.Value.Record))
            .Select(pair => pair.Key).ToArray();
        if (idleKeys.Length == 0) return;
        if (!string.IsNullOrWhiteSpace(observation.Error) ||
            observation.TargetCount < idleKeys.Length ||
            observation.TargetCount != observation.Details.Count ||
            observation.ReadyCount != observation.Details.Values.Count(detail => detail.Received))
            throw new InvalidDataException("Resource source hydration did not complete its current native targets.");

        foreach (string key in idleKeys)
        {
            if (!observation.Details.TryGetValue(key, out ResourceScanDetail? detail) ||
                !detail.Received || detail.FullAmount <= 0 || detail.RemainingAmount < 0 ||
                detail.RemainingAmount > detail.FullAmount)
                throw new InvalidDataException("Resource source hydration missed a currently idle Resource identity.");
            try
            {
                using JsonDocument document = JsonDocument.Parse(resources[key].Record.DataJson);
                JsonElement source = document.RootElement;
                if (!source.TryGetProperty("resourceConfigId", out JsonElement config) ||
                    !config.TryGetInt32(out int expectedConfig) || expectedConfig <= 0 ||
                    !source.TryGetProperty("resourceMaxAmount", out JsonElement capacity) ||
                    !capacity.TryGetInt64(out long expectedCapacity) || expectedCapacity <= 0 ||
                    !source.TryGetProperty("resourceNameKey", out JsonElement name) ||
                    name.ValueKind is not (JsonValueKind.String or JsonValueKind.Number) ||
                    detail.ResourceConfigId != expectedConfig ||
                    detail.SourceCapacity != expectedCapacity ||
                    detail.FullAmount != expectedCapacity ||
                    detail.SourceNameKey != (name.ValueKind == JsonValueKind.String
                        ? name.GetString() : name.GetRawText()))
                    throw new InvalidDataException("Resource source hydration identity/configuration changed during acquisition.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Resource source hydration metadata is malformed.", ex);
            }
        }
    }

    private ResourceScanDetailObservation ValidateResourceScanDetailResult(
        JsonElement root,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int resourceRecordCount)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesInt(root, "serverId", request.ServerId) ||
            !MatchesString(root, "scanRunId", request.RunId) ||
            !MatchesString(root, "state", "completed"))
            throw new InvalidDataException("Resource detail enrichment result did not match the active owned scan.");
        RequireFreshCaptureTime(root, startedAt);
        int targetCount = RequireNonNegativeInt(root, "targetCount");
        int requestCount = RequireNonNegativeInt(root, "requestCount");
        int cacheBeforeCount = RequireNonNegativeInt(root, "cacheBeforeCount");
        int sendFailureCount = RequireNonNegativeInt(root, "sendFailureCount");
        int readyCount = RequireNonNegativeInt(root, "readyCount");
        if (targetCount > resourceRecordCount || requestCount + cacheBeforeCount + sendFailureCount != targetCount ||
            readyCount > targetCount)
            throw new InvalidDataException("Resource detail enrichment counters are inconsistent.");
        if (!root.TryGetProperty("details", out JsonElement detailsValue) || detailsValue.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Resource detail enrichment details are missing.");
        var details = new Dictionary<string, ResourceScanDetail>(StringComparer.Ordinal);
        foreach (JsonElement item in detailsValue.EnumerateArray())
        {
            string recordKey = RequiredString(item, "recordKey");
            bool received = item.TryGetProperty("received", out JsonElement receivedValue) && receivedValue.ValueKind == JsonValueKind.True;
            int? configId = item.TryGetProperty("resourceConfigId", out JsonElement configValue) &&
                configValue.ValueKind == JsonValueKind.Number &&
                configValue.TryGetInt32(out int observedConfig) ? observedConfig : null;
            long? capacity = item.TryGetProperty("resourceMaxAmount", out JsonElement capacityValue) &&
                capacityValue.ValueKind == JsonValueKind.Number &&
                capacityValue.TryGetInt64(out long observedCapacity) ? observedCapacity : null;
            string? nameKey = item.TryGetProperty("resourceNameKey", out JsonElement nameValue) &&
                nameValue.ValueKind is JsonValueKind.String or JsonValueKind.Number
                    ? nameValue.ValueKind == JsonValueKind.String ? nameValue.GetString() : nameValue.GetRawText()
                    : null;
            long remaining = 0;
            long fullAmount = 0;
            if (received)
            {
                if (!item.TryGetProperty("detail", out JsonElement detailValue) || detailValue.ValueKind != JsonValueKind.Object ||
                    !detailValue.TryGetProperty("remainRes", out JsonElement remainValue) || !remainValue.TryGetInt64(out remaining) || remaining < 0 ||
                    !detailValue.TryGetProperty("initRes", out JsonElement initValue) || !initValue.TryGetInt64(out fullAmount) || fullAmount < 0 ||
                    remaining > fullAmount)
                    throw new InvalidDataException("Resource detail enrichment returned an invalid amount pair.");
            }
            if (!details.TryAdd(recordKey, new ResourceScanDetail(
                received, remaining, fullAmount, configId, capacity, nameKey)))
                throw new InvalidDataException("Resource detail enrichment returned a duplicate record key.");
        }
        if (details.Count != targetCount)
            throw new InvalidDataException("Resource detail enrichment target/detail counts differ.");
        RequireResourceDetailRowProvenance(detailsValue, targetCount, requestCount,
            cacheBeforeCount, sendFailureCount, readyCount);
        string? error = ReadOptionalString(root, "error");
        return new ResourceScanDetailObservation(
            targetCount, requestCount, cacheBeforeCount, sendFailureCount, readyCount, details, error);
    }

    // Native resource_scan_detail_runtime.rows attaches per-target origin
    // flags; aggregate header counts alone are insufficient to prove them.
    internal static void RequireResourceDetailRowProvenance(
        JsonElement rows, int targetCount, int requestCount, int cacheBeforeCount,
        int sendFailureCount, int readyCount)
    {
        if (rows.ValueKind != JsonValueKind.Array || targetCount < 0 ||
            requestCount < 0 || cacheBeforeCount < 0 || sendFailureCount < 0 ||
            readyCount < 0 || rows.GetArrayLength() != targetCount)
            throw new InvalidDataException("Resource detail native target rows are inconsistent.");

        int issued = 0, cached = 0, failed = 0, receivedCount = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("requestIssued", out JsonElement issuedValue) ||
                issuedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !row.TryGetProperty("cacheBefore", out JsonElement cachedValue) ||
                cachedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !row.TryGetProperty("sendFailed", out JsonElement failedValue) ||
                failedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !row.TryGetProperty("received", out JsonElement receivedValue) ||
                receivedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("Resource detail target omitted native request/cache/receive provenance.");
            bool wasIssued = issuedValue.GetBoolean();
            bool wasCached = cachedValue.GetBoolean();
            bool sendFailed = failedValue.GetBoolean();
            bool received = receivedValue.GetBoolean();
            if ((wasIssued ? 1 : 0) + (wasCached ? 1 : 0) + (sendFailed ? 1 : 0) != 1 ||
                sendFailed && received ||
                !received && row.TryGetProperty("detail", out JsonElement rawDetail) &&
                    rawDetail.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                throw new InvalidDataException("Resource detail target has contradictory native origin or result.");
            if (wasIssued) issued++;
            if (wasCached) cached++;
            if (sendFailed) failed++;
            if (received) receivedCount++;
        }
        if (issued != requestCount || cached != cacheBeforeCount ||
            failed != sendFailureCount || receivedCount != readyCount)
            throw new InvalidDataException("Resource detail target provenance does not reconcile with native counters.");
    }

    private static FirstLivePreparedResource ApplyResourceScanDetail(
        FirstLivePreparedResource prepared,
        ResourceScanDetail? detail)
    {
        if (detail?.Received != true) return prepared;
        JsonObject data = JsonNode.Parse(prepared.Record.DataJson)?.AsObject()
            ?? throw new InvalidDataException("Resource record JSON is unavailable during detail enrichment.");
        bool full = detail.RemainingAmount == detail.FullAmount;
        data["resourceDetailKnown"] = true;
        data["resourceRemainingAmount"] = detail.RemainingAmount;
        data["resourceFullAmount"] = detail.FullAmount;
        data["resourceFull"] = full;
        if (data["resourceMaxAmount"] is JsonValue configured && configured.TryGetValue<long>(out long configuredMax))
            data["resourceCapacityMatchesConfig"] = configuredMax == detail.FullAmount;
        return prepared with
        {
            Record = prepared.Record with { DataJson = data.ToJsonString(JsonOptions.Default) },
            Import = prepared.Import with { DataJson = data.ToJsonString(JsonOptions.Default) },
        };
    }

    private FastCityBatchObservation ValidateFastCityBatchResult(
        JsonElement root,
        byte[] bytes,
        string resultPath,
        string requestId,
        DateTimeOffset startedAt,
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        int groupStartColumn,
        int groupStartRow,
        int targetX,
        int targetY)
    {
        if (!MatchesInt(root, "schemaVersion", 1) ||
            !MatchesString(root, "probeVersion", ProbeVersion) ||
            !MatchesString(root, "requestId", requestId) ||
            !MatchesString(root, "profileId", session.ProfileId) ||
            !MatchesString(root, "launchSessionId", session.SessionId) ||
            !MatchesString(root, "challenge", session.Challenge) ||
            !MatchesInt(root, "gamePid", session.GamePid) ||
            !MatchesString(root, "requestMode", "coverage") ||
            !MatchesBool(root, "includeCity", request.SelectedTypes.Contains("city", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeResource", request.SelectedTypes.Contains("resource", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeMonster", IncludesMonsterSource(request)) ||
            !MatchesBool(root, "includeMonsterProtection", IncludesZombieBoss(request)) ||
            !MatchesBool(root, "includeTrain", request.SelectedTypes.Contains("truck", StringComparer.Ordinal) ||
                request.SelectedTypes.Contains("railway", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeDispatch", request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeGhost", request.SelectedTypes.Contains("ghost", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeTreasure", request.SelectedTypes.Contains("treasure", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeResourceDetails", request.SelectedTypes.Contains("resource", StringComparer.Ordinal)) ||
            !MatchesBool(root, "deferRestoreUntilResponse", true))
            throw new InvalidDataException("Fast world batch result did not match the active owned game session.");
        if (!MatchesString(root, "state", "proven"))
        {
            string error = ReadOptionalString(root, "error") ?? "unknown fast City batch failure";
            RequireFreshCaptureTime(root, startedAt);
            throw new InvalidDataException("Fast world batch failed: " + error);
        }
        if (!MatchesInt(root, "requestedCount", 8) ||
            !MatchesInt(root, "holdMilliseconds", 0) ||
            !MatchesInt(root, "homeTileX", request.PlayerTileX ?? -1) ||
            !MatchesInt(root, "homeTileY", request.PlayerTileY ?? -1) ||
            !MatchesInt(root, "viewLevel", -1) ||
            !MatchesInt(root, "targetTileX", targetX) ||
            !MatchesInt(root, "targetTileY", targetY))
        {
            string Actual(string name) =>
                root.TryGetProperty(name, out JsonElement value) ? value.GetRawText() : "<missing>";
            throw new InvalidDataException(
                "Fast world batch result did not match the requested acquisition parameters: " +
                $"expected requestedCount=8,holdMilliseconds=0,homeTile=({request.PlayerTileX ?? -1},{request.PlayerTileY ?? -1})," +
                $"viewLevel=-1,target=({targetX},{targetY}); actual requestedCount={Actual("requestedCount")}," +
                $"holdMilliseconds={Actual("holdMilliseconds")},homeTile=({Actual("homeTileX")},{Actual("homeTileY")})," +
                $"viewLevel={Actual("viewLevel")},target=({Actual("targetTileX")},{Actual("targetTileY")}).");
        }
        RequireFreshCaptureTime(root, startedAt);
        bool restorationContractMatches =
            MatchesBool(root, "positionRestoredBeforeResponse", false) &&
            MatchesBool(root, "positionRestoredAfterResponse", true) &&
            MatchesString(root, "requestMethod", "WorldPointManager.UpdateViewRequest(true)+deferred-restore-after-response");
        if (ExpectedCoverageZoom(hooks) is double expectedZoom &&
            (!root.TryGetProperty("coverageZoom", out JsonElement echoedZoom) || echoedZoom.ValueKind != JsonValueKind.Number ||
             Math.Abs(echoedZoom.GetDouble() - expectedZoom) > 0.001 ||
             !root.TryGetProperty("coverageZoomApplied", out JsonElement appliedZoom) || appliedZoom.ValueKind != JsonValueKind.Number ||
             Math.Abs(appliedZoom.GetDouble() - expectedZoom) > 0.01))
            throw new InvalidDataException("Fast world batch did not prove the canonical coverage camera height was applied.");
        if (ShouldUseBoundedFullWorldCameraLens(request, hooks) && groupStartColumn < 0 && !MatchesInt(root, "coverageFov", 20))
            throw new InvalidDataException("Bounded lens result did not match its admitted lens.");
        if (!MatchesBool(root, "responseFlagsTransitioned", true) ||
            !MatchesBool(root, "cameraTileStable", true) ||
            !MatchesBool(root, "cameraGeometryRestored", true) ||
            !MatchesBool(root, "cameraGeometryConfirmed", true) ||
            !MatchesBool(root, "cameraProjectionRestored", true) ||
            !restorationContractMatches)
            throw new InvalidDataException("Fast world batch did not prove the native response and full camera geometry restoration contract.");
        if (!MatchesInt(root, "serverLod", 0) ||
            !MatchesInt(root, "blockSize", FastCityAoiBlockSize) ||
            !MatchesInt(root, "blockCount", FastCityAoiBlockCount) ||
            !MatchesInt(root, "postServerLod", 0) ||
            !MatchesInt(root, "postBlockSize", FastCityAoiBlockSize) ||
            !MatchesInt(root, "postBlockCount", FastCityAoiBlockCount))
            throw new InvalidDataException("Fast world batch AOI geometry changed during acquisition.");
        int[] requestedIndices = RequireNonNegativeIntArray(root, "requestedIndices");
        if (requestedIndices.Length < FastCityMinimumLiveAoiCount ||
            requestedIndices.Any(index => index >= FastCityAoiBlockCount * FastCityAoiBlockCount))
            throw new InvalidDataException("Fast world batch returned an invalid native AOI footprint.");
        if (!MatchesInt(root, "nativeCurrentSetCount", requestedIndices.Length))
            throw new InvalidDataException("Fast world batch native AOI count did not match its copied footprint.");
        var requestedSet = requestedIndices.ToHashSet();
        if (groupStartColumn >= 0)
        {
            int[] expectedIndices = ExpectedGroupAoiIndices(groupStartColumn, groupStartRow);
            if (expectedIndices.Any(index => !requestedSet.Contains(index)))
                throw new InvalidDataException("Fast world batch did not cover all AOIs required by its logical block group.");
        }

        if (!root.TryGetProperty("point_records", out JsonElement pointRecords) ||
            pointRecords.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Fast world batch is missing its point record snapshot.");
        int matchedCityCount = RequireNonNegativeInt(root, "matchedCityCount");
        if (request.SelectedTypes.Contains("city", StringComparer.Ordinal))
        {
            if (!root.TryGetProperty("cityCompleteness", out JsonElement cityCompleteness) ||
                cityCompleteness.ValueKind != JsonValueKind.Object ||
                !MatchesInt(cityCompleteness, "accepted", matchedCityCount) ||
                !MatchesInt(cityCompleteness, "inRequestedAoi", matchedCityCount) ||
                !MatchesInt(cityCompleteness, "missingPointIndex", 0) ||
                !MatchesInt(cityCompleteness, "invalidPosition", 0) ||
                !MatchesInt(cityCompleteness, "missingServer", 0) ||
                !MatchesInt(cityCompleteness, "missingOwnerUid", 0) ||
                !MatchesInt(cityCompleteness, "missingOwnerName", 0) ||
                !MatchesInt(cityCompleteness, "duplicateRetainedPointIndices", 0) ||
                !MatchesInt(cityCompleteness, "missingNativeCurrentPointIndices", 0) ||
                !cityCompleteness.TryGetProperty("sourceCount", out JsonElement allRetained) ||
                allRetained.ValueKind != JsonValueKind.Number ||
                !allRetained.TryGetInt32(out int retainedCount) || retainedCount < matchedCityCount ||
                !root.TryGetProperty("nativePointInfoCityCount", out JsonElement currentCityValue) ||
                currentCityValue.ValueKind != JsonValueKind.Number ||
                !currentCityValue.TryGetInt32(out int currentCityCount) ||
                !cityCompleteness.TryGetProperty("nativeCurrentCityCount",out var canonicalCityValue) ||
                canonicalCityValue.ValueKind != JsonValueKind.Number ||
                !canonicalCityValue.TryGetInt32(out int canonicalCityCount) || canonicalCityCount < 0 ||
                canonicalCityCount > matchedCityCount || currentCityCount < 0)
                throw new InvalidDataException("Fast world City producer did not reconcile independent retained-base and current-point counts.");
            if (cityCompleteness.TryGetProperty("nativeOwnerConflicts", out _) &&
                !MatchesInt(cityCompleteness,"nativeOwnerConflicts",0))
                throw new InvalidDataException("Fresh native City owner conflicts with its retained point identity.");
            int superseded = cityCompleteness.TryGetProperty("supersededNativeCityCount",out var supersededValue)
                ? supersededValue.ValueKind == JsonValueKind.Number && supersededValue.TryGetInt32(out int n) && n >= 0
                    ? n : throw new InvalidDataException("Invalid superseded native City count") : 0;
            if ((long)canonicalCityCount + superseded != currentCityCount ||
                cityCompleteness.TryGetProperty("nativeCanonicalLookupFailures",out _) &&
                !MatchesInt(cityCompleteness,"nativeCanonicalLookupFailures",0))
                throw new InvalidDataException("Native City identities lack complete canonical-owner accounting.");
            if (superseded > 0)
            {
                if (!cityCompleteness.TryGetProperty("supersededNativePoints",out var oldPoints) ||
                    oldPoints.ValueKind != JsonValueKind.Array || oldPoints.GetArrayLength()!=superseded)
                    throw new InvalidDataException("Superseded native Cities lack exact identity references.");
                var oldIds = new HashSet<int>();
                foreach(var oldPoint in oldPoints.EnumerateArray())
                {
                    int oldId=RequirePositiveInt(oldPoint,"pointIndex");
                    int currentId=RequirePositiveInt(oldPoint,"canonicalPointIndex");
                    int x=RequireNonNegativeInt(oldPoint,"x"),y=RequireNonNegativeInt(oldPoint,"y");
                    if(!oldIds.Add(oldId) || oldId==currentId || currentId>1000000 || x>=1000 || y>=1000 ||
                        oldId!=y*1000+x+1 || !requestedSet.Contains(y/10*100+x/10) ||
                        !MatchesInt(oldPoint,"canonicalServerId",request.ServerId))
                        throw new InvalidDataException("Superseded native City identity/context was invalid.");
                }
            }
        }
        int matchedResourceCount = RequireNonNegativeInt(root, "matchedResourceCount");
        int matchedDispatchCount = RequireNonNegativeInt(root, "matchedDispatchCount");
        int matchedGhostCount = RequireNonNegativeInt(root, "matchedGhostCount");
        int matchedTreasureCount = RequireNonNegativeInt(root, "matchedTreasureCount");
        ResourceCompletenessBatchDiagnostics? resourceCompleteness = null;
        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
        {
            if (!root.TryGetProperty("resourceCompleteness", out JsonElement completenessValue) ||
                completenessValue.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Fast world Resource batch is missing completeness accounting.");
            resourceCompleteness = ResourceCompletenessBatchDiagnostics.Parse(completenessValue);
            if (resourceCompleteness.AcceptedResourceCount != matchedResourceCount)
                throw new InvalidDataException("Fast world Resource completeness accepted count changed during capture.");
        }
        IReadOnlyList<FirstLivePreparedResource> prepared = request.SelectedTypes.Contains("city", StringComparer.Ordinal) && HasPointKind(pointRecords, "player_base")
            ? FirstLiveResultImporter.PrepareCitySnapshot(bytes, resultPath)
            : Array.Empty<FirstLivePreparedResource>();
        IReadOnlyList<FirstLivePreparedResource> resources = request.SelectedTypes.Contains("resource", StringComparer.Ordinal) && HasPointKind(pointRecords, "resource_point")
            ? FirstLiveResultImporter.PrepareResourceSnapshot(bytes, resultPath)
            : Array.Empty<FirstLivePreparedResource>();
        if (resourceCompleteness is not null)
        {
            RequireResourceNativeSourceCohort(
                resources, resourceCompleteness, requestedSet, request.ServerId, request.WorldId);
            // The Lua producer first counts the native AOI snapshot, then
            // independently enumerates _pointInfos for Resource records. Its
            // matchedResourceCount is later overwritten by the second pass;
            // require that the earlier source evidence still agrees with the
            // published per-type Resource rows, not just their later totals.
            if (!root.TryGetProperty("nativePointTypeHistogram", out JsonElement firstNativeTypes))
                throw new InvalidDataException("Native Resource batch omitted its first AOI point-type snapshot.");
            RequireResourceFirstAoiTypeSnapshot(
                firstNativeTypes, resources, RequireNonNegativeInt(root, "matchedCount"));
        }
        IReadOnlyList<FastDispatchPrepared> dispatches = request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal)
            ? PrepareDispatchRecords(pointRecords, request, requestedSet, startedAt)
            : Array.Empty<FastDispatchPrepared>();
        IReadOnlyList<FastGhostPrepared> ghosts = request.SelectedTypes.Contains("ghost", StringComparer.Ordinal)
            ? PrepareGhostRecords(pointRecords, request, requestedSet, startedAt)
            : Array.Empty<FastGhostPrepared>();
        IReadOnlyList<FastTreasurePrepared> treasures = request.SelectedTypes.Contains("treasure", StringComparer.Ordinal)
            ? PrepareTreasureRecords(pointRecords, request, requestedSet, startedAt)
            : Array.Empty<FastTreasurePrepared>();
        if (request.SelectedTypes.Contains("city", StringComparer.Ordinal) && prepared.Count != matchedCityCount)
            throw new InvalidDataException("Fast world batch City snapshot count changed during normalization.");
        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal) && resources.Count != matchedResourceCount)
            throw new InvalidDataException("Fast world batch Resource snapshot count changed during normalization.");
        if (request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal) && dispatches.Count != matchedDispatchCount)
            throw new InvalidDataException("Fast world batch Dispatch snapshot count changed during normalization.");
        if (request.SelectedTypes.Contains("ghost", StringComparer.Ordinal) && ghosts.Count != matchedGhostCount)
            throw new InvalidDataException("Fast world batch Ghost snapshot count changed during normalization.");
        if (request.SelectedTypes.Contains("treasure", StringComparer.Ordinal) && treasures.Count != matchedTreasureCount)
            throw new InvalidDataException("Fast world batch Treasure snapshot count changed during normalization.");
        foreach (FirstLivePreparedResource item in prepared.Concat(resources))
        {
            if (item.Import.ServerId != request.ServerId)
                throw new InvalidDataException("Fast world batch contained a point from a different server.");
            int cellX = item.Import.X / FastCityAoiBlockSize;
            int cellY = item.Import.Y / FastCityAoiBlockSize;
            int aoiIndex = checked(cellY * FastCityAoiBlockCount + cellX);
            if (!requestedSet.Contains(aoiIndex))
                throw new InvalidDataException("Fast world batch point fell outside the native AOI footprint.");
        }
        IReadOnlyList<FastMonsterPrepared> monsters = IncludesMonsterSource(request)
            ? PrepareMonsterRecords(root, request, requestedSet, startedAt)
            : Array.Empty<FastMonsterPrepared>();
        bool includeTrain = request.SelectedTypes.Contains("truck", StringComparer.Ordinal) ||
            request.SelectedTypes.Contains("railway", StringComparer.Ordinal);
        IReadOnlyList<FastTrainPrepared> trains = includeTrain
            ? PrepareTrainRecords(root, request, requestedSet, startedAt)
            : Array.Empty<FastTrainPrepared>();
        int monsterInvasionBossCount = RequireNonNegativeInt(root, "monsterInvasionBossCount");
        int monsterProtectionDetailTargetCount = RequireNonNegativeInt(root, "monsterProtectionDetailTargetCount");
        int monsterProtectionDetailRequestCount = RequireNonNegativeInt(root, "monsterProtectionDetailRequestCount");
        int monsterProtectionDetailReadyCount = RequireNonNegativeInt(root, "monsterProtectionDetailReadyCount");
        if (monsterProtectionDetailRequestCount > monsterProtectionDetailTargetCount ||
            monsterProtectionDetailReadyCount > monsterProtectionDetailTargetCount)
            throw new InvalidDataException("Fast world Monster Invasion protection detail counters are inconsistent.");
        double? PhaseMilliseconds(string property) => root.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double seconds) &&
            double.IsFinite(seconds) && seconds >= 0 ? seconds * 1000 : null;
        return new FastCityBatchObservation(requestedIndices, prepared, resources, dispatches, ghosts, treasures, monsters, trains,
            resourceCompleteness,
            monsterInvasionBossCount, monsterProtectionDetailTargetCount, monsterProtectionDetailRequestCount, monsterProtectionDetailReadyCount,
            PhaseMilliseconds("targetResponseElapsedSeconds"), PhaseMilliseconds("homeRestorationElapsedSeconds"),
            PhaseMilliseconds("totalProbeElapsedSeconds"),
            IncludesMonsterSource(request) && root.TryGetProperty("monster_march_records", out JsonElement monsterSource) &&
                monsterSource.ValueKind == JsonValueKind.Array ? monsterSource.GetArrayLength() : 0);
    }

    // The first AOI count is taken before the Resource-specific producer
    // re-enumerates _pointInfos. Counts from the second pass can reconcile
    // perfectly even if the first native snapshot was a different cohort.
    // This bounded cross-enumeration check rejects such source races, though
    // matching counts do NOT prove equal identities or authoritative absence.
    internal static void RequireResourceFirstAoiTypeSnapshot(
        JsonElement firstNativeHistogram, IReadOnlyList<FirstLivePreparedResource> acceptedResources,
        int firstNativeMatchedCount)
    {
        if (firstNativeMatchedCount < 0)
            throw new InvalidDataException("First native AOI matched-point count is invalid.");
        var observed = new Dictionary<string, long>(StringComparer.Ordinal);
        long histogramTotal = 0;
        if (firstNativeHistogram.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty type in firstNativeHistogram.EnumerateObject())
            {
                if (type.Value.ValueKind != JsonValueKind.Number ||
                    !type.Value.TryGetInt64(out long count) || count < 0 ||
                    !observed.TryAdd(type.Name, count))
                    throw new InvalidDataException("First native AOI type histogram is malformed or ambiguous.");
                // point_aoi_counts increments matched and exactly one type key
                // in the SAME loop, including unknown/non-Resource types.
                // A valid Resource-only subset cannot certify a contradictory
                // total native AOI population. Bound before adding to avoid
                // overflow on malformed/untrusted histogram values.
                if (count > firstNativeMatchedCount - histogramTotal)
                    throw new InvalidDataException("First native AOI histogram exceeds its matched point count.");
                histogramTotal += count;
            }
        }
        else if (firstNativeHistogram.ValueKind != JsonValueKind.Array ||
                 firstNativeHistogram.GetArrayLength() != 0)
        {
            throw new InvalidDataException("First native AOI type histogram is unavailable.");
        }
        if (histogramTotal != firstNativeMatchedCount)
            throw new InvalidDataException("First native AOI histogram does not reconcile with its matched point count.");

        var acceptedByType = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (FirstLivePreparedResource item in acceptedResources)
        {
            try
            {
                using JsonDocument parsed = JsonDocument.Parse(item.Record.DataJson);
                if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                    !parsed.RootElement.TryGetProperty("pointType", out JsonElement type) ||
                    type.ValueKind != JsonValueKind.Number || !type.TryGetInt32(out int value) ||
                    value is not (1 or 7 or 26))
                    throw new InvalidDataException("Native Resource point has an invalid native source type.");
                string typeKey = value.ToString(CultureInfo.InvariantCulture);
                acceptedByType[typeKey] = acceptedByType.TryGetValue(typeKey, out long prior)
                    ? checked(prior + 1) : 1;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Native Resource type source JSON was invalid.", ex);
            }
        }

        foreach (string typeKey in new[] { "1", "7", "26" })
        {
            observed.TryGetValue(typeKey, out long firstCount);
            acceptedByType.TryGetValue(typeKey, out long acceptedCount);
            if (firstCount != acceptedCount)
                throw new InvalidDataException(
                    "Independent native AOI Resource types changed between first and accepted source snapshots.");
        }
    }

    // The native Resource producer enumerates _pointInfos once, then records
    // acceptedAoiCounts from the very same accepted resource_point entries.
    // Global totals and a reported footprint cannot prove which individual
    // AOI supplied each entry. Check the actual normalized source rows against
    // that exact per-response histogram before treating any AOI as acquired.
    internal static void RequireResourceNativeSourceCohort(
        IReadOnlyList<FirstLivePreparedResource> resources,
        ResourceCompletenessBatchDiagnostics source,
        IReadOnlySet<int> requestedAois,
        int serverId,
        long worldId)
    {
        static bool MatchesLong(JsonElement row, string field, long expected) =>
            row.TryGetProperty(field, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out long actual) && actual == expected;
        // resource_aoi_records increments rawPointTypeCounts for every native
        // _pointInfos entry, then classifies Resource candidates solely by the
        // same native pointType (1, 7, 26). Aggregate count equality alone
        // previously accepted a purported Resource snapshot whose native type
        // histogram contained only non-Resource points.
        long sourceTypedResources = source.RawPointTypeCounts
            .Where(entry => entry.Key is "1" or "7" or "26")
            .Sum(entry => (long)entry.Value);
        if (!source.Reconciles ||
            sourceTypedResources != source.ResourceCandidateCount ||
            source.RejectionReasonCounts.Any(reason =>
                reason.Value > 0 && reason.Key != "outside_selected_aoi") ||
            resources.Count != source.AcceptedResourceCount)
            throw new InvalidDataException("Native Resource source contained an unreconciled or skipped candidate.");

        var byAoi = new Dictionary<string, int>(StringComparer.Ordinal);
        var acceptedByNativeType = new Dictionary<string, int>(StringComparer.Ordinal);
        var acceptedByRuntimeClass = new Dictionary<string, int>(StringComparer.Ordinal);
        var identities = new HashSet<int>();
        foreach (FirstLivePreparedResource resource in resources)
        {
            FirstLiveResultImport origin = resource.Import;
            MapStoredRecord row = resource.Record;
            int aoi = checked((origin.Y / 10) * FastCityAoiBlockCount + origin.X / 10);
            if (origin.ServerId != serverId || row.ServerId != serverId ||
                row.Kind != "resource" || row.PointIndex != origin.PointIndex ||
                row.RecordKey != origin.RecordKey ||
                origin.RecordKey != origin.PointIndex.ToString(CultureInfo.InvariantCulture) ||
                origin.PointIndex != (long)origin.Y * 1000 + origin.X + 1 ||
                origin.X is < 0 or >= 1000 || origin.Y is < 0 or >= 1000 ||
                origin.CapturedAtUnixMilliseconds <= 0 ||
                row.UpdatedAt != origin.CapturedAtUnixMilliseconds ||
                !requestedAois.Contains(aoi) || !identities.Add(origin.PointIndex))
                throw new InvalidDataException("Native Resource point had a foreign, duplicate or stale AOI identity.");
            try
            {
                using JsonDocument document = JsonDocument.Parse(row.DataJson);
                JsonElement data = document.RootElement;
                bool sourceOccupiedKnown = data.TryGetProperty("gatherOccupancyKnown", out JsonElement known) &&
                    known.ValueKind == JsonValueKind.True;
                bool rebuiltKnown = data.TryGetProperty("rebuildGatherOccupancyKnown", out JsonElement rebuildKnown) &&
                    rebuildKnown.ValueKind == JsonValueKind.True;
                if (data.ValueKind != JsonValueKind.Object ||
                    !MatchesInt(data, "pointId", origin.PointIndex) ||
                    !MatchesInt(data, "pointIndex", origin.PointIndex) ||
                    !MatchesInt(data, "serverId", serverId) ||
                    !MatchesInt(data, "srcServerId", serverId) ||
                    !MatchesLong(data, "worldId", worldId) ||
                    !MatchesInt(data, "x", origin.X) || !MatchesInt(data, "y", origin.Y) ||
                    !MatchesString(data, "kind", "resource") ||
                    !MatchesString(data, "sourceKind", "resource_point") ||
                    !MatchesString(data, "source",
                        "WorldPointManager._pointInfos+GatherResource+SceneUtils.IsInBlackRange") ||
                    !MatchesString(data, "recordKey", origin.RecordKey) ||
                    !data.TryGetProperty("runtimeClass", out JsonElement runtimeClass) ||
                    runtimeClass.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(runtimeClass.GetString()) ||
                    !data.TryGetProperty("resourceSourceType", out JsonElement sourceClass) ||
                    sourceClass.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(sourceClass.GetString()) ||
                    !data.TryGetProperty("pointType", out JsonElement type) ||
                    type.ValueKind != JsonValueKind.Number ||
                    !type.TryGetInt32(out int nativePointType) ||
                    nativePointType is not (1 or 7 or 26) ||
                    !MatchesLong(data, "updatedAt", row.UpdatedAt) ||
                    sourceOccupiedKnown != rebuiltKnown ||
                    (sourceOccupiedKnown &&
                     (!data.TryGetProperty("gatherOccupied", out JsonElement occupied) ||
                      occupied.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                      !MatchesBool(data, "rebuildGatherOccupied", occupied.GetBoolean()))))
                    throw new InvalidDataException("Native Resource point lacked source identity or current occupancy provenance.");
                string nativeTypeKey = nativePointType.ToString(CultureInfo.InvariantCulture);
                int acceptedForType = acceptedByNativeType.TryGetValue(nativeTypeKey, out int earlier)
                    ? checked(earlier + 1) : 1;
                // The accepted AOI records are a subset of the same raw native
                // enumeration. The source cannot accept more of a type than
                // it reports observing, even when overall counts reconcile.
                if (!source.RawPointTypeCounts.TryGetValue(nativeTypeKey, out int enumeratedForType) ||
                    acceptedForType > enumeratedForType)
                    throw new InvalidDataException("Native Resource accepted type lacks raw source enumeration evidence.");
                acceptedByNativeType[nativeTypeKey] = acceptedForType;

                // The same native _pointInfos enumeration increments
                // runtimeClassCounts BEFORE filtering each candidate and
                // writes reflected_type_name(info) to each accepted row.
                // A nonblank row class is not enough: a contradictory raw
                // class histogram can otherwise certify a foreign source.
                string nativeClass = runtimeClass.GetString()!;
                int acceptedForClass = acceptedByRuntimeClass.TryGetValue(nativeClass, out int earlierClass)
                    ? checked(earlierClass + 1) : 1;
                if (!source.RuntimeClassCounts.TryGetValue(nativeClass, out int enumeratedForClass) ||
                    acceptedForClass > enumeratedForClass)
                    throw new InvalidDataException("Native Resource accepted class lacks raw source enumeration evidence.");
                acceptedByRuntimeClass[nativeClass] = acceptedForClass;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Native Resource source row has invalid JSON.", ex);
            }
            string key = aoi.ToString(CultureInfo.InvariantCulture);
            byAoi[key] = byAoi.TryGetValue(key, out int previous) ? checked(previous + 1) : 1;
        }
        if (source.AcceptedAoiCounts.Count != byAoi.Count ||
            source.AcceptedAoiCounts.Any(entry =>
                !byAoi.TryGetValue(entry.Key, out int actual) || actual != entry.Value))
            throw new InvalidDataException("Native Resource per-AOI source counts differ from accepted point records.");
    }
    private static Dictionary<int, MapStoredRecord[]> RecordsByBlock(FastCityBatchObservation observation, MapScanExecutionRequest request)
    {
        var buckets = new Dictionary<int, List<MapStoredRecord>>();
        if (request.SelectedTypes.Contains("city", StringComparer.Ordinal))
            foreach (FirstLivePreparedResource item in observation.Prepared)
                AddRecordToBlock(buckets, item.Import.X, item.Import.Y, item.Record);
        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
            foreach (FirstLivePreparedResource item in observation.Resources)
                AddRecordToBlock(buckets, item.Import.X, item.Import.Y, item.Record);
        if (request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal))
            foreach (FastDispatchPrepared item in observation.Dispatches)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("ghost", StringComparer.Ordinal))
            foreach (FastGhostPrepared item in observation.Ghosts)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("treasure", StringComparer.Ordinal))
            foreach (FastTreasurePrepared item in observation.Treasures)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (IncludesMonsterSource(request))
            foreach (FastMonsterPrepared item in observation.Monsters)
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("truck", StringComparer.Ordinal))
            foreach (FastTrainPrepared item in observation.Trains.Where(item => item.Record.Kind == "truck"))
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        if (request.SelectedTypes.Contains("railway", StringComparer.Ordinal))
            foreach (FastTrainPrepared item in observation.Trains.Where(item => item.Record.Kind == "railway"))
                AddRecordToBlock(buckets, item.X, item.Y, item.Record);
        return buckets.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    private static bool HasPointKind(JsonElement rows, string expectedKind) =>
        rows.EnumerateArray().Any(row =>
            row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("kind", out JsonElement kind) &&
            kind.ValueKind == JsonValueKind.String &&
            string.Equals(kind.GetString(), expectedKind, StringComparison.Ordinal));

    private static void AddRecordToBlock(Dictionary<int, List<MapStoredRecord>> buckets, int x, int y, MapStoredRecord record)
    {
        int blockIndex = checked((y / MapScanGeometry.RecoveredBlockSpan) * 50 + (x / MapScanGeometry.RecoveredBlockSpan));
        if (!buckets.TryGetValue(blockIndex, out List<MapStoredRecord>? list)) buckets[blockIndex] = list = [];
        list.Add(record);
    }

    private static IReadOnlyList<FastDispatchPrepared> PrepareDispatchRecords(
        JsonElement rows,
        MapScanExecutionRequest request,
        HashSet<int> requestedSet,
        DateTimeOffset capturedAt)
    {
        var result = new List<FastDispatchPrepared>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("kind", out JsonElement kindValue) ||
                kindValue.ValueKind != JsonValueKind.String ||
                !string.Equals(kindValue.GetString(), "dispatch_task", StringComparison.Ordinal))
                continue;

            int pointType = RequiredInt(row, "pointType");
            string runtimeClass = RequiredString(row, "runtimeClass");
            int pointId = RequiredInt(row, "pointId");
            int serverId = RequiredInt(row, "serverId");
            int worldId = RequiredInt(row, "worldId");
            int x = RequiredInt(row, "x");
            int y = RequiredInt(row, "y");
            string uuid = RequiredString(row, "uuid");
            int cfgId = RequiredInt(row, "cfgId");
            int level = RequiredInt(row, "level");
            int quality = RequiredInt(row, "quality");
            if (pointType != 17 ||
                !runtimeClass.EndsWith("HeroDispatchMissionPointInfo", StringComparison.Ordinal) ||
                pointId <= 0 || serverId != request.ServerId || worldId != request.WorldId ||
                x < 0 || x >= 1000 || y < 0 || y >= 1000 ||
                cfgId <= 0 || level < 1 || quality < 1)
                throw new InvalidDataException("Fast world Dispatch snapshot contained invalid identity/geometry/config data.");

            int aoiIndex = checked((y / FastCityAoiBlockSize) * FastCityAoiBlockCount + (x / FastCityAoiBlockSize));
            if (!requestedSet.Contains(aoiIndex))
                throw new InvalidDataException("Fast world Dispatch point fell outside the native AOI footprint.");

            var data = JsonNode.Parse(row.GetRawText())!.AsObject();
            long updatedAt = capturedAt.ToUnixTimeMilliseconds();
            data["kind"] = "dispatch";
            data["updatedAt"] = updatedAt;
            bool isSpecial = row.TryGetProperty("isSpecial", out JsonElement specialValue) &&
                specialValue.ValueKind == JsonValueKind.True;
            data["isSpecial"] = isSpecial;

            // Current-v19 UIWorldPointBtn Dispatch steal eligibility uses
            // completionTime + LwDispatchTask.protect_times * 60000 as the first
            // legal steal instant, stealList.Count as the current per-task steal
            // count, and LwDispatchTask.steal_maxtimes as the cap.
            long? completionTime = OptionalPositiveInt64(row, "completionTime");
            int? protectMinutes = OptionalNonNegativeInt(row, "protectTimeMinutes");
            int? stolenCount = OptionalNonNegativeInt(row, "stealListCount");
            int? maxStealCount = OptionalNonNegativeInt(row, "stealMaxTimes");
            if (completionTime is long completion &&
                protectMinutes is int protect &&
                protect <= (long.MaxValue - completion) / 60_000L)
            {
                data["plunderAt"] = completion + protect * 60_000L;
            }
            if (stolenCount.HasValue) data["stolenCount"] = stolenCount.Value;
            if (maxStealCount.HasValue) data["maxStealCount"] = maxStealCount.Value;
            // Do not map HeroDispatchMissionPointInfo.expiredTime to
            // taskExpireTime: current-v19 Dispatch Lua does not use that field
            // as the steal expiry contract.

            string recordKey = pointId.ToString(CultureInfo.InvariantCulture);
            string? ownerName = OptionalStringValue(row, "ownerName");
            string? allianceName = OptionalStringValue(row, "allianceName");
            result.Add(new FastDispatchPrepared(x, y, new MapStoredRecord(
                "dispatch", serverId, recordKey, pointId, uuid, ownerName, allianceName,
                level, quality, null, null, null, updatedAt,
                data.ToJsonString(JsonOptions.Default))));
        }
        return result;
    }

    private static IReadOnlyList<FastGhostPrepared> PrepareGhostRecords(
        JsonElement rows,
        MapScanExecutionRequest request,
        HashSet<int> requestedSet,
        DateTimeOffset capturedAt)
    {
        var result = new List<FastGhostPrepared>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("kind", out JsonElement kindValue) ||
                kindValue.ValueKind != JsonValueKind.String ||
                !string.Equals(kindValue.GetString(), "ghost_task", StringComparison.Ordinal))
                continue;

            int pointType = RequiredInt(row, "pointType");
            string runtimeClass = RequiredString(row, "runtimeClass");
            int pointId = RequiredInt(row, "pointId");
            int serverId = RequiredInt(row, "serverId");
            int worldId = RequiredInt(row, "worldId");
            int x = RequiredInt(row, "x");
            int y = RequiredInt(row, "y");
            string uuid = RequiredString(row, "uuid");
            int cfgId = RequiredInt(row, "cfgId");
            int level = RequiredInt(row, "level");
            int quality = RequiredInt(row, "quality");
            if (pointType != 29 ||
                !runtimeClass.EndsWith("GhostreconPointInfo", StringComparison.Ordinal) ||
                pointId <= 0 || serverId != request.ServerId || worldId != request.WorldId ||
                x < 0 || x >= 1000 || y < 0 || y >= 1000 ||
                cfgId <= 0 || level < 1 || quality < 1)
                throw new InvalidDataException("Fast world Ghost snapshot contained invalid identity/geometry/config data.");

            int aoiIndex = checked((y / FastCityAoiBlockSize) * FastCityAoiBlockCount + (x / FastCityAoiBlockSize));
            if (!requestedSet.Contains(aoiIndex))
                throw new InvalidDataException("Fast world Ghost point fell outside the native AOI footprint.");

            var data = JsonNode.Parse(row.GetRawText())!.AsObject();
            long updatedAt = capturedAt.ToUnixTimeMilliseconds();
            data["kind"] = "ghost";
            data["updatedAt"] = updatedAt;
            bool isSpecial = row.TryGetProperty("isSpecial", out JsonElement specialValue) &&
                specialValue.ValueKind == JsonValueKind.True;
            data["isSpecial"] = isSpecial;

            // Current-v22 ActGhostreconTaskTemplate.CheckCanSteal uses
            // (serverTime-completionTime)/1000 >= protectTime, while
            // GetPointStealType compares #stealList with stealMaxtimes. Project
            // those exact scheduler fields here so the 0.3.17 host can perform
            // its pre-provider validation before PrepareGhostPlunderTasksAsync.
            if (row.TryGetProperty("completionTime", out JsonElement completionValue) &&
                completionValue.TryGetInt64(out long completionTime) && completionTime > 0 &&
                row.TryGetProperty("protectTime", out JsonElement protectValue) &&
                protectValue.TryGetInt32(out int protectSeconds) && protectSeconds >= 0)
            {
                data["plunderAt"] = checked(completionTime + (long)protectSeconds * 1000L);
            }
            if (row.TryGetProperty("stealListCount", out JsonElement stolenValue) &&
                stolenValue.TryGetInt32(out int stolenCount) && stolenCount >= 0)
            {
                data["stolenCount"] = stolenCount;
            }
            if (row.TryGetProperty("stealMaxTimes", out JsonElement maxStealValue) &&
                maxStealValue.TryGetInt32(out int maxStealCount) && maxStealCount >= 0)
            {
                data["maxStealCount"] = maxStealCount;
            }

            string recordKey = pointId.ToString(CultureInfo.InvariantCulture);
            result.Add(new FastGhostPrepared(x, y, new MapStoredRecord(
                "ghost", serverId, recordKey, pointId, uuid, null, null,
                level, quality, null, null, null, updatedAt,
                data.ToJsonString(JsonOptions.Default))));
        }
        return result;
    }

    private static IReadOnlyList<FastTreasurePrepared> PrepareTreasureRecords(
        JsonElement rows,
        MapScanExecutionRequest request,
        HashSet<int> requestedSet,
        DateTimeOffset capturedAt)
    {
        var result = new List<FastTreasurePrepared>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("kind", out JsonElement kindValue) ||
                kindValue.ValueKind != JsonValueKind.String ||
                (kindValue.GetString() is not ("treasure_point" or "supplies_point")))
                continue;

            int pointType = RequiredInt(row, "pointType");
            string runtimeClass = RequiredString(row, "runtimeClass");
            int pointId = RequiredInt(row, "pointId");
            int serverId = RequiredInt(row, "serverId");
            int worldId = RequiredInt(row, "worldId");
            int x = RequiredInt(row, "x");
            int y = RequiredInt(row, "y");
            string uuid = RequiredString(row, "uuid");
            int treasureType = RequiredInt(row, "treasureType");
            int suppliesType = RequiredInt(row, "suppliesType");
            bool ordinary = pointType == 21 && runtimeClass.EndsWith("TreasurePointInfo", StringComparison.Ordinal) &&
                treasureType > 0 && suppliesType == 0;
            bool supplies = pointType == 27 && runtimeClass.EndsWith("WorldSuppliesPoint", StringComparison.Ordinal) &&
                treasureType == 0 && suppliesType > 0;
            if ((!ordinary && !supplies) ||
                pointId <= 0 || serverId != request.ServerId || worldId != request.WorldId ||
                x < 0 || x >= 1000 || y < 0 || y >= 1000)
                throw new InvalidDataException("Fast world Treasure snapshot contained invalid identity/geometry/type data.");

            int aoiIndex = checked((y / FastCityAoiBlockSize) * FastCityAoiBlockCount + (x / FastCityAoiBlockSize));
            if (!requestedSet.Contains(aoiIndex))
                throw new InvalidDataException("Fast world Treasure point fell outside the native AOI footprint.");

            var data = JsonNode.Parse(row.GetRawText())!.AsObject();
            long updatedAt = capturedAt.ToUnixTimeMilliseconds();
            data["kind"] = "treasure";
            data["updatedAt"] = updatedAt;

            string recordKey = pointId.ToString(CultureInfo.InvariantCulture);
            result.Add(new FastTreasurePrepared(x, y, new MapStoredRecord(
                "treasure", serverId, recordKey, pointId, uuid, null, null,
                null, null, null, null, null, updatedAt,
                data.ToJsonString(JsonOptions.Default))));
        }
        return result;
    }

    private static IReadOnlyList<FastMonsterPrepared> PrepareMonsterRecords(JsonElement root, MapScanExecutionRequest request, HashSet<int> requestedSet, DateTimeOffset capturedAt)
    {
        if (!root.TryGetProperty("monster_march_records", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Fast world batch is missing its Monster march snapshot.");
        var result = new List<FastMonsterPrepared>(rows.GetArrayLength());
        // Routing split of one shared sweep: game-identified MonsterInvasionBoss rows
        // (special 10, `monsterProtectionEligible`) belong to zombie_boss only; every
        // other row belongs to monster only. A kind that was not selected never
        // receives the other kind's rows (so Monster-only scans keep excluding them).
        bool wantsMonster = request.SelectedTypes.Contains("monster", StringComparer.Ordinal);
        bool wantsZombieBoss = IncludesZombieBoss(request);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string uuid = RequiredString(row, "uuid");
            int serverId = RequiredInt(row, "serverId"); int worldId = RequiredInt(row, "worldId");
            int x = RequiredInt(row, "x"); int y = RequiredInt(row, "y"); int monsterId = RequiredInt(row, "monsterId");
            int configId = RequiredInt(row, "configId"); int level = RequiredInt(row, "monsterLevel");
            string nameKey = RequiredString(row, "monsterNameKey");
            if (serverId != request.ServerId || worldId != request.WorldId || x < 0 || x >= 1000 || y < 0 || y >= 1000 || monsterId <= 0 || configId <= 0 || level < 0)
                throw new InvalidDataException("Fast world Monster snapshot contained invalid identity/geometry/config data.");
            bool isMonster = row.TryGetProperty("isMonster", out JsonElement im) && im.ValueKind == JsonValueKind.True;
            bool isBoss = row.TryGetProperty("isBoss", out JsonElement ib) && ib.ValueKind == JsonValueKind.True;
            if (!isMonster && !isBoss) throw new InvalidDataException("Fast world Monster snapshot contained a row not classified by the game as Monster/Boss.");
            int aoiIndex = checked((y / FastCityAoiBlockSize) * FastCityAoiBlockCount + (x / FastCityAoiBlockSize));
            if (!requestedSet.Contains(aoiIndex)) throw new InvalidDataException("Fast world Monster fell outside the native AOI footprint.");
            JsonObject data = JsonNode.Parse(row.GetRawText())!.AsObject(); long updatedAt = capturedAt.ToUnixTimeMilliseconds();
            double? distance = row.TryGetProperty("distanceFromHome", out JsonElement distanceValue) &&
                distanceValue.TryGetDouble(out double parsedDistance) && double.IsFinite(parsedDistance) && parsedDistance >= 0
                ? parsedDistance : null;
            bool protectionEligible = row.TryGetProperty("monsterProtectionEligible", out JsonElement protectionEligibleValue) &&
                protectionEligibleValue.ValueKind == JsonValueKind.True;
            if (protectionEligible ? !wantsZombieBoss : !wantsMonster) continue;
            string outputKind = protectionEligible ? MapScanContract.ZombieBossKind : "monster";
            // Static game template power (`recommend_power`); absent/unparseable stays unknown.
            long? recommendPower = null;
            if (protectionEligible && row.TryGetProperty("recommendPower", out JsonElement powerValue))
            {
                if (powerValue.ValueKind == JsonValueKind.Number && powerValue.TryGetInt64(out long numericPower) && numericPower > 0)
                    recommendPower = numericPower;
                else if (powerValue.ValueKind == JsonValueKind.String && long.TryParse(powerValue.GetString(),
                    NumberStyles.None, CultureInfo.InvariantCulture, out long textPower) && textPower > 0)
                    recommendPower = textPower;
                if (recommendPower is not null) data["recommendPower"] = recommendPower.Value;
            }
            bool protectionKnown = row.TryGetProperty("monsterProtectionKnown", out JsonElement protectionKnownValue) &&
                protectionKnownValue.ValueKind == JsonValueKind.True;
            bool protectionActive = row.TryGetProperty("monsterProtectionActive", out JsonElement protectionActiveValue) &&
                protectionActiveValue.ValueKind == JsonValueKind.True;
            long? shieldEndTime;
            if (protectionKnown)
                shieldEndTime = protectionActive && row.TryGetProperty("monsterProtectionEndTime", out JsonElement protectionEndValue) &&
                    protectionEndValue.TryGetInt64(out long protectionEnd) && protectionEnd > 0 ? protectionEnd : null;
            else if (protectionEligible)
                shieldEndTime = null;
            else
                shieldEndTime = row.TryGetProperty("zMBossShieldEndTime", out JsonElement shieldValue) &&
                    shieldValue.TryGetInt64(out long parsedShield) && parsedShield > 0 ? parsedShield : null;
            data["kind"] = outputKind; data["level"] = level; data["updatedAt"] = updatedAt;
            if (distance is not null) data["distanceFromHome"] = distance.Value;
            if (shieldEndTime is not null) data["shieldEndTime"] = shieldEndTime.Value;
            int? pointIndex = row.TryGetProperty("positionIndex", out JsonElement pi) && pi.TryGetInt32(out int piv) ? piv : null;
            result.Add(new FastMonsterPrepared(x, y, protectionEligible, new MapStoredRecord(outputKind, serverId, uuid, pointIndex, uuid, nameKey, null, level, null, recommendPower, distance, shieldEndTime, updatedAt, data.ToJsonString(JsonOptions.Default))));
        }
        return result;
    }

    // Controlled-check seam: exact production routing/normalization of one native Monster snapshot.
    internal static IReadOnlyList<MapStoredRecord> NormalizeMonsterSnapshotForCheck(
        JsonElement root, MapScanExecutionRequest request, HashSet<int> requestedSet, DateTimeOffset capturedAt) =>
        PrepareMonsterRecords(root, request, requestedSet, capturedAt).Select(item => item.Record).ToArray();

    private static IReadOnlyList<FastTrainPrepared> PrepareTrainRecords(
        JsonElement root,
        MapScanExecutionRequest request,
        HashSet<int> requestedSet,
        DateTimeOffset capturedAt,
        long? truckReceiptAdmissionUnixMs = null)
    {
        if (!root.TryGetProperty("train_march_records", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Fast world batch is missing its Train march snapshot.");
        var result = new List<FastTrainPrepared>(rows.GetArrayLength());
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string uuid = RequiredString(row, "uuid");
            TrainDataMetadata? parsedTrainData = null;
            int? trainType = OptionalInt(row, "trainType");
            if (trainType is null)
            {
                parsedTrainData = ReadTrainDataMetadata(row);
                trainType = parsedTrainData.Type;
            }
            if (trainType is null) continue; // unclassifiable train rows stay unknown.
            // Current-v18 Assembly-CSharp.rdl: TrainType.Truck = 1, TrainType.Train = 2.
            string? kind = trainType.Value switch { 1 => "truck", 2 => "railway", _ => null };
            if (kind is null || !request.SelectedTypes.Contains(kind, StringComparer.Ordinal)) continue;
            int serverId = RequiredInt(row, "serverId");
            int worldId = RequiredInt(row, "worldId");
            int x = RequiredInt(row, "x");
            int y = RequiredInt(row, "y");
            int quality = RequiredInt(row, "trainQuality");
            if (serverId != request.ServerId || worldId != request.WorldId ||
                x < 0 || x >= 1000 || y < 0 || y >= 1000 || quality < 1)
                throw new InvalidDataException("Fast world Truck snapshot contained invalid identity/geometry/quality data.");
            int aoiIndex = checked((y / FastCityAoiBlockSize) * FastCityAoiBlockCount + (x / FastCityAoiBlockSize));
            if (!requestedSet.Contains(aoiIndex))
                throw new InvalidDataException("Fast world Truck fell outside the native AOI footprint.");
            long updatedAt = OptionalPositiveInt64(row, "capturedAtMs") is long frameTime
                ? frameTime : capturedAt.ToUnixTimeMilliseconds();
            long? power = row.TryGetProperty("power", out JsonElement powerValue) && powerValue.TryGetInt64(out long parsedPower) && parsedPower >= 0 ? parsedPower : null;
            string? ownerName = OptionalStringValue(row, "ownerName");
            string? allianceName = OptionalStringValue(row, "allianceName");
            int? pointIndex = row.TryGetProperty("positionIndex", out JsonElement pi) && pi.TryGetInt32(out int piv) ? piv : null;
            long? arriveTs = OptionalPositiveInt64(row, "arriveTs");
            // The dedicated fast Truck importer already admitted its rows at
            // one exact receipt time. Rechecking against a later clock here
            // can drop a Truck arriving during parsing, spuriously failing the
            // strict row-count reconciliation. Its final block publisher still
            // independently drops anything that has arrived by publication.
            if (kind == "truck" && !FastTruckArrivalIsAfter(row,
                    truckReceiptAdmissionUnixMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                continue;
            int? robTimes = OptionalNonNegativeInt(row, "robTimes");
            long? protectTime = OptionalPositiveInt64(row, "protectTime");
            if (kind == "railway" && (arriveTs is null || robTimes is null || protectTime is null))
            {
                parsedTrainData ??= ReadTrainDataMetadata(row);
                arriveTs ??= parsedTrainData.ArriveTs;
                robTimes ??= parsedTrainData.RobTimes;
                protectTime ??= parsedTrainData.ProtectTime;
            }
            TruckSourceMetadata? truckMetadata = kind == "truck" ? ReadTruckSourceMetadata(row) : null;
            var data = new JsonObject
            {
                ["uuid"] = uuid, ["marchUuid"] = OptionalStringValue(row, "marchUuid") ?? uuid,
                ["ownerName"] = ownerName, ["allianceName"] = allianceName,
                ["quality"] = quality, ["power"] = power, ["x"] = x, ["y"] = y,
                // Current-v19 TrainData.Refresh: self.isSpecialURQuality = self.quality == 10.
                ["isSpecialURQuality"] = quality == 10,
                ["positionIndex"] = pointIndex, ["trainType"] = trainType.Value,
                ["trainCfgId"] = RequiredInt(row, "trainCfgId"),
                ["carriageNum"] = RequiredInt(row, "carriageNum"),
                ["updatedAt"] = updatedAt,
                ["source"] = OptionalStringValue(row, "source") ??
                    "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
            };
            // Persist the actual acquisition lane for independent packaged
            // publication audits. The same native march builder also serves
            // the camera route, so its ordinary source string is insufficient
            // evidence that this record came from the verified 625 rectangles.
            if (kind == "truck" && truckReceiptAdmissionUnixMs is not null)
                data["captureProtocol"] = "current_fast_truck_625_v1";
            if (row.TryGetProperty("trainUuid", out JsonElement trainUuidValue)) data["trainUuid"] = JsonNode.Parse(trainUuidValue.GetRawText());
            if (row.TryGetProperty("ownerUid", out JsonElement ownerUidValue) && ownerUidValue.ValueKind == JsonValueKind.String) data["ownerUid"] = ownerUidValue.GetString();
            if (row.TryGetProperty("allianceUid", out JsonElement allianceUidValue) && allianceUidValue.ValueKind == JsonValueKind.String) data["allianceUid"] = allianceUidValue.GetString();
            if (row.TryGetProperty("allianceAbbr", out JsonElement allianceAbbrValue) &&
                allianceAbbrValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(allianceAbbrValue.GetString()))
                data["allianceAbbr"] = allianceAbbrValue.GetString();
            if (row.TryGetProperty("ownerServer", out JsonElement ownerServerValue) && ownerServerValue.TryGetInt32(out int ownerServer)) data["ownerServer"] = ownerServer;
            if (row.TryGetProperty("targetServer", out JsonElement targetServerValue) && targetServerValue.TryGetInt32(out int targetServer)) data["targetServer"] = targetServer;
            if (row.TryGetProperty("srcServer", out JsonElement srcServerValue) && srcServerValue.TryGetInt32(out int srcServer)) data["srcServer"] = srcServer;
            if (row.TryGetProperty("startTime", out JsonElement startValue) && startValue.TryGetInt64(out long startTime)) data["startTime"] = startTime;
            if (row.TryGetProperty("endTime", out JsonElement endValue) && endValue.TryGetInt64(out long endTime)) data["endTime"] = endTime;
            if (arriveTs is long sourceArriveTs) data["arriveTs"] = sourceArriveTs;
            if (robTimes is int sourceRobTimes) data["robTimes"] = sourceRobTimes;
            if (protectTime is long sourceProtectTime) data["protectTime"] = sourceProtectTime;
            if (row.TryGetProperty("maxLootCount", out JsonElement maxLootValue) &&
                maxLootValue.TryGetInt32(out int maxLootCount) && maxLootCount >= 0)
                data["maxLootCount"] = maxLootCount;
            if (row.TryGetProperty("currentGoods", out JsonElement currentGoodsValue) &&
                currentGoodsValue.ValueKind == JsonValueKind.Array && currentGoodsValue.GetArrayLength() > 0)
                data["currentGoods"] = JsonNode.Parse(currentGoodsValue.GetRawText());
            if (kind == "railway" && row.TryGetProperty("trainDataJson", out JsonElement trainDataValue) &&
                trainDataValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(trainDataValue.GetString()))
                data["trainDataJson"] = trainDataValue.GetString();
            result.Add(new FastTrainPrepared(x, y, new MapStoredRecord(
                kind, serverId, uuid, pointIndex, uuid, ownerName, allianceName, null, quality, power, null, null, updatedAt,
                data.ToJsonString(JsonOptions.Default)), truckMetadata));
        }
        return result;
    }

    private static int? OptionalInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : null;

    private static int? OptionalNonNegativeInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) && parsed >= 0 ? parsed : null;

    private static long? OptionalPositiveInt64(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) && parsed > 0 ? parsed : null;

    private static TruckSourceMetadata? ReadTruckSourceMetadata(JsonElement row)
    {
        bool metadataKnown = row.TryGetProperty("truckMetadataKnown", out JsonElement knownValue) &&
            knownValue.ValueKind == JsonValueKind.True;
        if (!metadataKnown) return null;
        string? currentGoods = row.TryGetProperty("truckCurrentGoodsRaw", out JsonElement currentValue) &&
            currentValue.ValueKind == JsonValueKind.Array ? currentValue.GetRawText() : null;
        int? exactMaxLootCount = row.TryGetProperty("truckMaxLootCount", out JsonElement maxLootValue) &&
            maxLootValue.TryGetInt32(out int parsedMaxLoot) && parsedMaxLoot >= 0 ? parsedMaxLoot : null;
        string? extraGoodsCur = row.TryGetProperty("truckExtraGoodsCur", out JsonElement extraValue) &&
            extraValue.ValueKind == JsonValueKind.Array ? extraValue.GetRawText() : null;
        string? baseGoodsCur = row.TryGetProperty("truckBaseGoodsCur", out JsonElement baseValue) &&
            baseValue.ValueKind == JsonValueKind.Array ? baseValue.GetRawText() : null;
        bool vipOn = row.TryGetProperty("truckVipOn", out JsonElement vipValue) &&
            vipValue.ValueKind == JsonValueKind.True;
        return new TruckSourceMetadata(currentGoods, exactMaxLootCount, extraGoodsCur, baseGoodsCur, vipOn);
    }

    private static TrainDataMetadata ReadTrainDataMetadata(JsonElement row)
    {
        if (!row.TryGetProperty("trainDataJson", out JsonElement raw) || raw.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(raw.GetString()))
            return new TrainDataMetadata(null, null, null, null);
        try
        {
            using JsonDocument document = JsonDocument.Parse(raw.GetString()!);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new TrainDataMetadata(null, null, null, null);
            int? type = root.TryGetProperty("type", out JsonElement typeValue) && typeValue.TryGetInt32(out int parsedType) ? parsedType : null;
            long? arriveTs = root.TryGetProperty("arriveTime", out JsonElement arriveValue) && arriveValue.TryGetInt64(out long parsedArrive) && parsedArrive > 0 ? parsedArrive : null;
            int? robTimes = null;
            long? protectTime = null;
            if (root.TryGetProperty("marchInfo", out JsonElement marchInfo) && marchInfo.ValueKind == JsonValueKind.Object)
            {
                if (marchInfo.TryGetProperty("robTimes", out JsonElement robValue) &&
                    robValue.TryGetInt32(out int parsedRob) && parsedRob >= 0)
                    robTimes = parsedRob;
                if (marchInfo.TryGetProperty("protectTime", out JsonElement protectValue) &&
                    protectValue.TryGetInt64(out long parsedProtect) && parsedProtect > 0)
                    protectTime = parsedProtect;
            }
            return new TrainDataMetadata(type, arriveTs, robTimes, protectTime);
        }
        catch (JsonException) { return new TrainDataMetadata(null, null, null, null); }
    }

    private static FastTrainPrepared ApplyFinalTruckMetadataEnrichment(FastTrainPrepared prepared)
    {
        TruckSourceMetadata? source = prepared.TruckMetadata;
        if (source is null) return prepared;

        JsonObject? data;
        try { data = JsonNode.Parse(prepared.Record.DataJson)?.AsObject(); }
        catch (JsonException) { return prepared; }
        if (data is null) return prepared;

        if (data["maxLootCount"] is null && TryReadSourceSafeTruckMaxLootCount(source, out int maxLootCount))
            data["maxLootCount"] = maxLootCount;
        ApplyTruckRemainingLootCount(data);
        if (data["currentGoods"] is not JsonArray existingGoods || existingGoods.Count == 0)
        {
            JsonArray? goods = ReadTruckCurrentGoods(source);
            if (goods is not null && goods.Count > 0) data["currentGoods"] = goods;
        }

        return prepared with
        {
            Record = prepared.Record with { DataJson = data.ToJsonString(JsonOptions.Default) },
        };
    }

    private static void ApplyTruckRemainingLootCount(JsonObject data)
    {
        bool specialUr = data["isSpecialURQuality"] is JsonValue specialValue &&
            specialValue.TryGetValue<bool>(out bool specialFlag) && specialFlag;

        int effectiveMaxLootCount = 0;
        if (specialUr)
        {
            // Recovered shipped-frontend contract: reindeer/special-UR Trucks can be
            // robbed at most once regardless of the generic TrainData maxLootPerTrain.
            effectiveMaxLootCount = 1;
        }
        else if (data["maxLootCount"] is JsonValue maxValue &&
                 maxValue.TryGetValue<int>(out int parsedMax) && parsedMax > 0)
        {
            effectiveMaxLootCount = parsedMax;
        }

        if (effectiveMaxLootCount <= 0) return;

        int robTimes = 0;
        if (data["robTimes"] is JsonValue robValue &&
            robValue.TryGetValue<int>(out int parsedRob))
            robTimes = Math.Max(0, parsedRob);

        data["remainingLootCount"] = Math.Max(effectiveMaxLootCount - robTimes, 0);
    }

    private static bool TryReadSourceSafeTruckMaxLootCount(TruckSourceMetadata source, out int maxLootCount)
    {
        maxLootCount = 0;
        if (source.ExactMaxLootCount is int exact)
        {
            maxLootCount = exact;
            return true;
        }

        // Fallback for deterministic/older captures that do not expose the already-computed
        // TrainData.maxLootPerTrain field. Current-v19 TrainData.Refresh starts from
        // LWAllyStationDataManager.MAX_LOOT_PER_TRAIN (default 3) and only subtracts under
        // truthy vipOn. Never guess the VIP reduction when the exact runtime field is absent.
        if (source.VipOn) return false;
        maxLootCount = 3;
        return true;
    }

    private static JsonArray? ReadTruckCurrentGoods(TruckSourceMetadata source)
    {
        var totals = new Dictionary<(int RewardType, long ItemId), long>();
        var metadata = new Dictionary<(int RewardType, long ItemId), (string? Name, string? IconPath)>();
        AppendTruckGoods(source.CurrentGoodsJson, totals, metadata);
        if (totals.Count == 0)
        {
            AppendTruckGoods(source.ExtraGoodsCurJson, totals, metadata);
            AppendTruckGoods(source.BaseGoodsCurJson, totals, metadata);
        }
        if (totals.Count == 0) return null;

        var goods = new JsonArray();
        foreach (((int rewardType, long itemId), long count) in totals.OrderBy(item => item.Key.RewardType).ThenBy(item => item.Key.ItemId))
        {
            if (count <= 0) continue;
            var good = new JsonObject
            {
                // Internal rebuild identity only; original LWBridge key producer remains unrecovered.
                ["key"] = $"reward:{rewardType}:{itemId}",
                ["count"] = count,
                ["rewardType"] = rewardType,
                ["itemId"] = itemId,
            };
            if (metadata.TryGetValue((rewardType, itemId), out (string? Name, string? IconPath) display))
            {
                if (!string.IsNullOrWhiteSpace(display.Name)) good["name"] = display.Name;
                if (!string.IsNullOrWhiteSpace(display.IconPath)) good["iconPath"] = display.IconPath;
            }
            goods.Add(good);
        }
        return goods.Count > 0 ? goods : null;
    }

    private static void AppendTruckGoods(
        string? currentJson,
        Dictionary<(int RewardType, long ItemId), long> totals,
        Dictionary<(int RewardType, long ItemId), (string? Name, string? IconPath)> metadata)
    {
        if (string.IsNullOrWhiteSpace(currentJson)) return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(currentJson);
            JsonElement current = document.RootElement;
            if (current.ValueKind != JsonValueKind.Array) return;

            foreach (JsonElement reward in current.EnumerateArray())
            {
                if (reward.ValueKind != JsonValueKind.Object ||
                    !reward.TryGetProperty("type", out JsonElement typeValue) || !typeValue.TryGetInt32(out int rewardType))
                    continue;

                long? itemId = null;
                long? count = null;
                if (reward.TryGetProperty("itemId", out JsonElement flatItemId) &&
                    reward.TryGetProperty("count", out JsonElement flatCount) &&
                    TryReadInt64(flatItemId, out long parsedFlatId) &&
                    TryReadInt64(flatCount, out long parsedFlatCount))
                {
                    itemId = parsedFlatId;
                    count = parsedFlatCount;
                }
                else if (reward.TryGetProperty("value", out JsonElement value))
                {
                    if (value.ValueKind == JsonValueKind.Object)
                    {
                        if (value.TryGetProperty("id", out JsonElement idValue) && TryReadInt64(idValue, out long parsedId)) itemId = parsedId;
                        if (value.TryGetProperty("num", out JsonElement numValue) && TryReadInt64(numValue, out long parsedCount)) count = parsedCount;
                    }
                    else if (TryReadInt64(value, out long scalarCount))
                    {
                        // This is the existing game/rebuild reward shape used by TrainData normalization:
                        // scalar rewards carry their identity in reward.type and their amount in reward.value.
                        itemId = rewardType;
                        count = scalarCount;
                    }
                }

                if (itemId is not long id || count is not long quantity || id <= 0 || quantity <= 0) continue;
                var key = (rewardType, id);
                if (totals.TryGetValue(key, out long existing))
                {
                    if (existing > long.MaxValue - quantity) continue;
                    totals[key] = existing + quantity;
                }
                else totals[key] = quantity;

                string? name = reward.TryGetProperty("name", out JsonElement nameValue) &&
                    nameValue.ValueKind == JsonValueKind.String ? nameValue.GetString() : null;
                string? iconPath = reward.TryGetProperty("iconPath", out JsonElement iconValue) &&
                    iconValue.ValueKind == JsonValueKind.String ? iconValue.GetString() : null;
                if ((!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(iconPath)) &&
                    (!metadata.TryGetValue(key, out var existingMetadata) ||
                     string.IsNullOrWhiteSpace(existingMetadata.Name) ||
                     string.IsNullOrWhiteSpace(existingMetadata.IconPath)))
                {
                    metadata[key] = (
                        !string.IsNullOrWhiteSpace(name) ? name : existingMetadata.Name,
                        !string.IsNullOrWhiteSpace(iconPath) ? iconPath : existingMetadata.IconPath);
                }
            }
        }
        catch (JsonException)
        {
            // Optional source metadata must remain fail-closed; malformed rewards are not synthesized.
        }
    }

    private static bool TryReadInt64(JsonElement value, out long parsed)
    {
        parsed = 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out parsed)) return true;
        return value.ValueKind == JsonValueKind.String &&
            long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static int[] ReadTrainServerIds(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Train-list result is missing {name}.");
        var result = new List<int>(value.GetArrayLength());
        var seen = new HashSet<int>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (!item.TryGetInt32(out int serverId) || serverId is < 1 or > 99999 || !seen.Add(serverId))
                throw new InvalidDataException($"Train-list result contains an invalid {name} entry.");
            result.Add(serverId);
        }
        result.Sort();
        return result.ToArray();
    }

    private static string? OptionalStringValue(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString() : null;

    private static int RequiredInt(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : throw new InvalidDataException("Monster field missing: " + name);
    private static string RequiredString(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new InvalidDataException("Monster field missing: " + name);

    private static int[] ExpectedGroupAoiIndices(int groupStartColumn, int groupStartRow)
    {
        int startCellX = checked(groupStartColumn * 2);
        int startCellY = checked(groupStartRow * 2);
        var result = new List<int>(FastCityExpectedAoiCount);
        for (int cellY = startCellY; cellY < startCellY + 10; cellY++)
            for (int cellX = startCellX; cellX < startCellX + (FastCityGroupColumns * 2); cellX++)
                result.Add(checked(cellY * FastCityAoiBlockCount + cellX));
        return result.ToArray();
    }

    private static int[] ExpectedBlockAoiIndices(MapScanTargetBlock block)
    {
        int startCellX = checked(block.Column * 2);
        int startCellY = checked(block.Row * 2);
        return
        [
            checked(startCellY * FastCityAoiBlockCount + startCellX),
            checked(startCellY * FastCityAoiBlockCount + startCellX + 1),
            checked((startCellY + 1) * FastCityAoiBlockCount + startCellX),
            checked((startCellY + 1) * FastCityAoiBlockCount + startCellX + 1),
        ];
    }

    private sealed class FastFullWorldResumeState
    {
        internal FastFullWorldResumeState(OverviewMapScanSession session, MapScanExecutionRequest request)
        {
            Session = session;
            RunId = request.RunId;
            ServerId = request.ServerId;
            WorldId = request.WorldId;
            SelectedTypesKey = string.Join("\u001f", request.SelectedTypes.OrderBy(value => value, StringComparer.Ordinal));
            ScanMode = request.ScanMode;
            RequestedConcurrency = request.RequestedConcurrency;
        }

        internal OverviewMapScanSession Session { get; }
        internal string RunId { get; }
        internal int ServerId { get; }
        internal long WorldId { get; }
        internal string SelectedTypesKey { get; }
        internal string ScanMode { get; }
        internal int RequestedConcurrency { get; }
        internal bool CameraFreeAcquisition { get; set; }
        internal bool RailwaySweepVerified { get; set; }
        internal long MonsterSourceRows { get; set; }
        internal DateTimeOffset MetricsStartedAt { get; } = DateTimeOffset.UtcNow;
        internal long ProbeAttempts { get; set; }
        internal long ProbeRetries { get; set; }
        internal long ValidatedProbeResponses { get; set; }
        // rows x columns of every validated native AOI window -> count. Shows what a lens/zoom really buys.
        internal Dictionary<string, int> FootprintHistogram { get; } = new(StringComparer.Ordinal);
        internal long NewlyCoveredAois { get; set; }
        internal int ActiveCameraProbes;
        internal int PeakCameraProbes;
        internal double AcquisitionMilliseconds;
        internal double RailwayRefreshMilliseconds;
        internal double TruckHydrationMilliseconds;
        internal double ResourceHydrationMilliseconds;
        internal double MonsterHydrationMilliseconds;
        internal double ProjectionMilliseconds;
        internal long OldestCapturedAgeMilliseconds;
        internal int ResourceDetailNativeRequests;
        internal int ResourceDetailNativeReady;
        internal int MonsterDetailNativeRequests;
        internal int MonsterDetailNativeReady;
        internal double ProbeWaitMilliseconds { get; set; }
        internal long NativePhaseSamples { get; set; }
        internal double NativeTargetResponseMilliseconds { get; set; }
        internal double NativeHomeRestorationMilliseconds { get; set; }
        internal double NativeTotalProbeMilliseconds { get; set; }
        internal HashSet<int> CompletedRequestOrdinals { get; } = new();
        internal HashSet<int> Covered { get; } = new();
        internal Dictionary<string, FirstLivePreparedResource> CityRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FirstLivePreparedResource> ResourceRecords { get; } = new(StringComparer.Ordinal);
        internal ResourceCompletenessAccumulator ResourceCompleteness { get; } = new();
        internal Dictionary<string, FastDispatchPrepared> DispatchRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastGhostPrepared> GhostRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastTreasurePrepared> TreasureRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastMonsterPrepared> MonsterRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastTrainPrepared> TrainRecords { get; } = new(StringComparer.Ordinal);

        internal bool Matches(OverviewMapScanSession session, MapScanExecutionRequest request) =>
            Session == session && RunId == request.RunId && ServerId == request.ServerId && WorldId == request.WorldId &&
            ScanMode == request.ScanMode && RequestedConcurrency == request.RequestedConcurrency &&
            SelectedTypesKey == string.Join("\u001f", request.SelectedTypes.OrderBy(value => value, StringComparer.Ordinal));
    }

    private void WriteFastFullWorldMetrics(FastFullWorldResumeState state, string phase)
    {
        // A diagnostic ForceLegacy baseline still needs its own exact-run
        // coverage metrics; otherwise sequential benchmark rounds could
        // inadvertently reuse an earlier candidate's private metric file.
        if (!state.CameraFreeAcquisition && hooks is not null &&
            !hooks.ExperimentalBoundedCameraLens && !hooks.ForceLegacyFullWorldLens) return;
        // This is an attributable application->Lua AOI probe counter, NOT a
        // raw game-network request counter. Concurrent game traffic cannot be
        // separated from reflection-level global Net send/receive counters.
        var compact = new
        {
            schemaVersion = 1,
            source = state.CameraFreeAcquisition
                ? "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync"
                : "CurrentClientMapBlockSource.ProbeFastCityBatchAsync",
            identity = new { state.RunId, state.ServerId, state.WorldId, state.SelectedTypesKey, state.ScanMode },
            phase,
            acquisitionPolicy = state.CameraFreeAcquisition
                ? "direct"
                : state.ScanMode == "fast" ? "measured_wide_aoi_step" : "conservative_aoi_step",
            requestedConcurrency = state.RequestedConcurrency,
            actualPeakConcurrentCameraProbes = state.PeakCameraProbes,
            elapsedMilliseconds = (DateTimeOffset.UtcNow - state.MetricsStartedAt).TotalMilliseconds,
            acquisitionMilliseconds = state.AcquisitionMilliseconds,
            railwayRefreshMilliseconds = state.RailwayRefreshMilliseconds,
            truckHydrationMilliseconds = state.TruckHydrationMilliseconds,
            resourceHydrationMilliseconds = state.ResourceHydrationMilliseconds,
            monsterHydrationMilliseconds = state.MonsterHydrationMilliseconds,
            projectionMilliseconds = state.ProjectionMilliseconds,
            oldestCapturedAgeMilliseconds = state.OldestCapturedAgeMilliseconds,
            resourceDetailNativeRequests = state.ResourceDetailNativeRequests,
            resourceDetailNativeReady = state.ResourceDetailNativeReady,
            monsterDetailNativeRequests = state.MonsterDetailNativeRequests,
            monsterDetailNativeReady = state.MonsterDetailNativeReady,
            attributableLuaProbeAttempts = state.ProbeAttempts,
            attributableRetries = state.ProbeRetries,
            validatedProbeResponses = state.ValidatedProbeResponses,
            footprintHistogram = state.FootprintHistogram,
            newlyCoveredAois = state.NewlyCoveredAois,
            summedProbeWaitMilliseconds = state.ProbeWaitMilliseconds,
            nativePhaseSampleCount = state.NativePhaseSamples,
            summedNativeTargetResponseMilliseconds = state.NativeTargetResponseMilliseconds,
            summedNativeHomeRestorationMilliseconds = state.NativeHomeRestorationMilliseconds,
            summedNativeTotalProbeMilliseconds = state.NativeTotalProbeMilliseconds,
            uniqueCoveredAois = state.Covered.Count,
            requiredAois = 10000,
            uniqueRecordKeys = new
            {
                city = state.CityRecords.Count, resource = state.ResourceRecords.Count,
                monster = state.MonsterRecords.Count, train = state.TrainRecords.Count,
                dispatch = state.DispatchRecords.Count, ghost = state.GhostRecords.Count,
                treasure = state.TreasureRecords.Count,
            },
            rawNetworkRequestCount = (long?)null,
        };
        Directory.CreateDirectory(probeRuntimeRoot);
        string path = Path.Combine(probeRuntimeRoot, "map-fullworld-probe-metrics.json");
        File.WriteAllText(path, JsonSerializer.Serialize(compact, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class CoarseMonsterTargetUnavailableException(string message) : Exception(message);

    internal sealed record MonsterProtectionDetailMetrics(
        int BossCount, int TargetCount, int RequestCount, int RetryCount, int ReadyCount, string? Error);
    private sealed record TrainDataMetadata(int? Type, long? ArriveTs, int? RobTimes, long? ProtectTime);
    private sealed record TruckSourceMetadata(
        string? CurrentGoodsJson,
        int? ExactMaxLootCount,
        string? ExtraGoodsCurJson,
        string? BaseGoodsCurJson,
        bool VipOn);
    private sealed record FastDispatchPrepared(int X, int Y, MapStoredRecord Record);
    private sealed record FastGhostPrepared(int X, int Y, MapStoredRecord Record);
    private sealed record FastTreasurePrepared(int X, int Y, MapStoredRecord Record);
    private sealed record FastMonsterPrepared(int X, int Y, bool ProtectionEligible, MapStoredRecord Record);
    private sealed record MonsterProtectionDetail(bool Received, bool Active, long EndTime);
    internal sealed record ResourceScanDetail(bool Received, long RemainingAmount, long FullAmount,
        int? ResourceConfigId, long? SourceCapacity, string? SourceNameKey);
    internal sealed record ResourceScanDetailObservation(
        int TargetCount,
        int RequestCount,
        int CacheBeforeCount,
        int SendFailureCount,
        int ReadyCount,
        IReadOnlyDictionary<string, ResourceScanDetail> Details,
        string? Error)
    {
        internal static readonly ResourceScanDetailObservation Empty =
            new(0, 0, 0, 0, 0, new Dictionary<string, ResourceScanDetail>(StringComparer.Ordinal), null);
        internal static ResourceScanDetailObservation Failed(string error) =>
            new(0, 0, 0, 0, 0, new Dictionary<string, ResourceScanDetail>(StringComparer.Ordinal), error);
    }
    private sealed record MonsterProtectionDetailObservation(
        int TargetCount,
        int RequestCount,
        int RetryCount,
        int ReadyCount,
        IReadOnlyDictionary<string, MonsterProtectionDetail> Details,
        string? Error)
    {
        internal static readonly MonsterProtectionDetailObservation Empty =
            new(0, 0, 0, 0, new Dictionary<string, MonsterProtectionDetail>(StringComparer.Ordinal), null);

        internal static MonsterProtectionDetailObservation Failed(string error) =>
            new(0, 0, 0, 0, new Dictionary<string, MonsterProtectionDetail>(StringComparer.Ordinal), error);
    }
    private sealed record FastTrainPrepared(int X, int Y, MapStoredRecord Record, TruckSourceMetadata? TruckMetadata = null);
    private sealed record FastTrainListSnapshot(
        IReadOnlyList<FastTrainPrepared> Rows, int LiveServerId,
        int[] MatchServerIds, int[] TruckServerIds, int[] RailwayServerIds);
    private sealed record FastCityBatchObservation(
        int[] RequestedIndices,
        IReadOnlyList<FirstLivePreparedResource> Prepared,
        IReadOnlyList<FirstLivePreparedResource> Resources,
        IReadOnlyList<FastDispatchPrepared> Dispatches,
        IReadOnlyList<FastGhostPrepared> Ghosts,
        IReadOnlyList<FastTreasurePrepared> Treasures,
        IReadOnlyList<FastMonsterPrepared> Monsters,
        IReadOnlyList<FastTrainPrepared> Trains,
        ResourceCompletenessBatchDiagnostics? ResourceCompleteness,
        int MonsterInvasionBossCount,
        int MonsterProtectionDetailTargetCount,
        int MonsterProtectionDetailRequestCount,
        int MonsterProtectionDetailReadyCount,
        double? TargetResponseMilliseconds,
        double? HomeRestorationMilliseconds,
        double? TotalProbeMilliseconds,
        // Raw game Monster rows in this batch (bosses and ordinary monsters) before kind filtering.
        int MonsterSourceRows = 0);
}
