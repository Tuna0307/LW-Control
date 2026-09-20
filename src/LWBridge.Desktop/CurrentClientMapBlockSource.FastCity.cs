using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

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
    private const int FastFullWorldAoiRows = 10;
    private const int FastFullWorldRowRequests = FastCityAoiBlockCount / FastFullWorldAoiRows;
    private const int FastFullWorldMaxRequestsPerRow = 50;
    private static readonly TimeSpan FastCityProbeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MonsterProtectionProbeTimeout = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan ResourceDetailProbeTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FastCityStartupSettleDelay = TimeSpan.FromSeconds(3);
    private string? fastCitySettledSessionId;
    private FastFullWorldResumeState? fastFullWorldResumeState;
    internal MonsterProtectionDetailMetrics? LastMonsterProtectionDetailMetrics { get; private set; }

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
        if (!string.Equals(fastCitySettledSessionId, session.SessionId, StringComparison.Ordinal))
        {
            await DelayAsync(FastCityStartupSettleDelay, cancellationToken).ConfigureAwait(false);
            RequireSameSession(session);
            fastCitySettledSessionId = session.SessionId;
        }

        if (pendingBlockIndices.Count == 2500 && seedBlock.BlockIndex == 0)
        {
            if (fastFullWorldResumeState is not { } resume || !resume.Matches(session, request))
                fastFullWorldResumeState = new FastFullWorldResumeState(session, request);

            if (IsMonsterOnly(request) && hooks?.DisableCoarseMonsterMap != true)
            {
                // Monster/Zombie Boss are fast-only. A failed whole-world LOD2 acquisition
                // must surface truthfully instead of silently falling back to the much slower
                // LOD0 adaptive sweep. The same-run session anchor remains available to an
                // outer retry when readiness itself was only transient.
                return await CaptureFullMonsterMapViaCoarseLodAsync(
                    session, request, pendingBlockIndices, progress, cancellationToken).ConfigureAwait(false);
            }
            return await CaptureFullCityMapAsync(session, request, pendingBlockIndices, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        int bandStartRow = (seedBlock.Row / FastCityGroupRows) * FastCityGroupRows;
        int firstGroupStartColumn = (seedBlock.Column / FastCityGroupColumns) * FastCityGroupColumns;
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        var captures = new List<MapScanBlockCapture>();

        for (int groupStartColumn = firstGroupStartColumn; groupStartColumn < 50; groupStartColumn += FastCityGroupColumns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MapScanTargetBlock[] groupBlocks = logicalBlocks
                .Where(block =>
                    block.Column >= groupStartColumn &&
                    block.Column < groupStartColumn + FastCityGroupColumns &&
                    block.Row >= bandStartRow &&
                    block.Row < bandStartRow + FastCityGroupRows &&
                    pendingBlockIndices.Contains(block.BlockIndex))
                .ToArray();
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
                protocol = "current_fast_monster_lod2_v1",
                blockIndex = block.BlockIndex,
                coverage = "native_lod2_whole_world_monster_snapshot_restored_to_original_lod",
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

        FastFullWorldResumeState state = fastFullWorldResumeState is { } existing && existing.Matches(session, request)
            ? existing
            : new FastFullWorldResumeState(session, request);
        fastFullWorldResumeState = state;
        HashSet<int> covered = state.Covered;
        Dictionary<string, FirstLivePreparedResource> cityRecords = state.CityRecords;
        Dictionary<string, FirstLivePreparedResource> resourceRecords = state.ResourceRecords;
        Dictionary<string, FastDispatchPrepared> dispatchRecords = state.DispatchRecords;
        Dictionary<string, FastGhostPrepared> ghostRecords = state.GhostRecords;
        Dictionary<string, FastTreasurePrepared> treasureRecords = state.TreasureRecords;
        Dictionary<string, FastMonsterPrepared> monsterRecords = state.MonsterRecords;
        Dictionary<string, FastTrainPrepared> trainRecords = state.TrainRecords;
        LastMonsterProtectionDetailMetrics = null;
        for (int row = 0; row < FastFullWorldRowRequests; row++)
        {
            int groupStartRow = row * FastCityGroupRows;
            int aoiRowStart = row * FastFullWorldAoiRows;
            int targetY = 75 + (row * 100);
            bool preferWideStep = false;
            int? previousGap = null;
            int requestsThisRow = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int gapColumn = FirstUncoveredAoiColumn(covered, aoiRowStart);
                if (gapColumn < 0) break;
                if (++requestsThisRow > FastFullWorldMaxRequestsPerRow)
                    throw new InvalidDataException($"Fast full-world acquisition made no bounded progress in AOI row band {row}.");

                // Wide footprints are centered around the target: width 4 has two columns
                // left / one right, while the observed width 5 has two left / two right.
                // Optimistically step two columns only after we have measured width >= 4 in this band.
                // If the footprint contracts, the unchanged first gap forces the next request back
                // to the conservative +1 target; exact coverage, not the prediction, remains truth.
                int targetOffset = preferWideStep ? 2 : 1;
                if (gapColumn == 0) targetOffset = 0;
                if (previousGap == gapColumn) targetOffset = gapColumn == 0 ? 0 : 1;
                int targetCellX = Math.Min(FastCityAoiBlockCount - 1, gapColumn + targetOffset);
                int targetX = checked((targetCellX * FastCityAoiBlockSize) + 5);

                FastCityBatchObservation? observation = null;
                Exception? lastError = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        observation = await ProbeFastCityBatchAsync(
                            session, request, -1, groupStartRow, targetX, targetY, cancellationToken)
                            .ConfigureAwait(false);
                        ValidateAdaptiveRowFootprint(
                            observation.RequestedIndices,
                            aoiRowStart,
                            request,
                            session,
                            targetX,
                            targetY,
                            attempt);
                        lastError = null;
                        break;
                    }
                    catch (Exception error) when (error is TimeoutException or InvalidDataException ||
                        error is BridgeCommandException bridge && bridge.Code == "GAME_CONNECTION_UNAVAILABLE")
                    {
                        lastError = error;
                        if (hooks is null)
                            Console.Error.WriteLine(
                                $"FAST_FULL_WORLD_RETRY server={request.ServerId} world={request.WorldId} " +
                                $"target=({targetX},{targetY}) rowStart={aoiRowStart} attempt={attempt}/3 error={error.Message}");
                        if (attempt < 3)
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
                if (lastError is not null || observation is null) throw lastError!;
                RequireSameSession(session);

                int before = covered.Count;
                foreach (int index in observation.RequestedIndices)
                    covered.Add(index);
                foreach (FirstLivePreparedResource prepared in observation.Prepared)
                {
                    if (!cityRecords.TryGetValue(prepared.Record.RecordKey, out FirstLivePreparedResource? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        cityRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FirstLivePreparedResource prepared in observation.Resources)
                {
                    if (!resourceRecords.TryGetValue(prepared.Record.RecordKey, out FirstLivePreparedResource? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        resourceRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FastDispatchPrepared prepared in observation.Dispatches)
                {
                    if (!dispatchRecords.TryGetValue(prepared.Record.RecordKey, out FastDispatchPrepared? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        dispatchRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FastGhostPrepared prepared in observation.Ghosts)
                {
                    if (!ghostRecords.TryGetValue(prepared.Record.RecordKey, out FastGhostPrepared? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        ghostRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FastTreasurePrepared prepared in observation.Treasures)
                {
                    if (!treasureRecords.TryGetValue(prepared.Record.RecordKey, out FastTreasurePrepared? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        treasureRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FastMonsterPrepared prepared in observation.Monsters)
                {
                    if (!monsterRecords.TryGetValue(prepared.Record.RecordKey, out FastMonsterPrepared? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        monsterRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FastTrainPrepared prepared in observation.Trains)
                {
                    if (!trainRecords.TryGetValue(prepared.Record.RecordKey, out FastTrainPrepared? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        trainRecords[prepared.Record.RecordKey] = prepared;
                }

                int afterGap = FirstUncoveredAoiColumn(covered, aoiRowStart);
                if (covered.Count == before)
                    throw new InvalidDataException("Fast full-world adaptive acquisition returned no new AOI coverage.");
                int measuredWidth = observation.RequestedIndices.Select(index => index % FastCityAoiBlockCount).Distinct().Count();
                preferWideStep = measuredWidth >= 4 && afterGap != gapColumn;
                previousGap = afterGap == gapColumn ? gapColumn : null;
                progress?.Invoke(new MapScanSourceProgress(covered.Count * 100d / 10000d));
            }
        }

        if (covered.Count != 10000)
            throw new InvalidDataException($"Fast full-world acquisition covered {covered.Count}/10000 AOIs.");

        // Truck reward/max-loot enrichment is deliberately post-acquisition. The AOI hot path
        // carries only lightweight game-owned TrainData fields/reward arrays; full TrainData JSON
        // is Railway-only because current-v19 Truck plunder history makes that blob very large.
        if (request.SelectedTypes.Contains("truck", StringComparer.Ordinal))
        {
            foreach ((string key, FastTrainPrepared prepared) in trainRecords.ToArray())
            {
                if (prepared.Record.Kind != "truck") continue;
                trainRecords[key] = ApplyFinalTruckMetadataEnrichment(prepared);
            }
        }

        ResourceScanDetailObservation resourceDetails = ResourceScanDetailObservation.Empty;
        int idleResourceCount = request.SelectedTypes.Contains("resource", StringComparer.Ordinal)
            ? resourceRecords.Values.Count(item => IsKnownIdleResource(item.Record))
            : 0;
        if (idleResourceCount > 0)
        {
            try
            {
                resourceDetails = await ProbeResourceScanDetailsAsync(
                    session, request, resourceRecords.Count, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                resourceDetails = ResourceScanDetailObservation.Failed(ex.Message);
            }
            foreach ((string key, FirstLivePreparedResource prepared) in resourceRecords.ToArray())
            {
                if (!IsKnownIdleResource(prepared.Record)) continue;
                resourceDetails.Details.TryGetValue(key, out ResourceScanDetail? detail);
                resourceRecords[key] = ApplyResourceScanDetail(prepared, detail);
            }
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
            monsterInvasionBossCount, protection.TargetCount,
            protection.RequestCount, protection.RetryCount, protection.ReadyCount, protection.Error);

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
        fastFullWorldResumeState = null;
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

    private static void ValidateAdaptiveRowFootprint(
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
        if (rows.Length != FastFullWorldAoiRows || rows.Length == 0 ||
            rows[0] != rowStart || rows[^1] != rowStart + FastFullWorldAoiRows - 1 ||
            columns.Length is < 2 or > 5 || columns.Zip(columns.Skip(1), (left, right) => right - left).Any(delta => delta != 1) ||
            indices.Length != rows.Length * columns.Length)
            throw new InvalidDataException(
                "Fast full-world adaptive acquisition returned a non-rectangular v18 AOI footprint: " + detail + ".");
        var expected = rows.SelectMany(row => columns.Select(column => checked(row * FastCityAoiBlockCount + column))).ToHashSet();
        if (expected.Count != indices.Length || indices.Any(index => !expected.Contains(index)))
            throw new InvalidDataException(
                "Fast full-world adaptive acquisition returned an inconsistent AOI footprint: " + detail + ".");
    }

    private static bool IncludesMonsterSource(MapScanExecutionRequest request) =>
        request.SelectedTypes.Contains("monster", StringComparer.Ordinal) ||
        request.SelectedTypes.Contains("zombie_boss", StringComparer.Ordinal);

    private static bool IsZombieBossOnly(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count == 1 &&
        request.SelectedTypes.Contains("zombie_boss", StringComparer.Ordinal);

    private static bool IsMonsterOnly(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count == 1 && IncludesMonsterSource(request);

    private static bool CanUseFastCityBatch(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count is >= 1 and <= 8 &&
        request.SelectedTypes.All(type => type is "city" or "resource" or "monster" or "zombie_boss" or "truck" or "railway" or "dispatch" or "ghost" or "treasure") &&
        request.WorldId == 0 && request.TileWidth == 1000 && request.TileHeight == 1000;
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
            $"targetTileX={targetX.ToString(CultureInfo.InvariantCulture)}",
            $"targetTileY={targetY.ToString(CultureInfo.InvariantCulture)}",
            "requestedCount=8",
            "holdMilliseconds=0",
            $"homeTileX={(request.PlayerTileX ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"homeTileY={(request.PlayerTileY ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"includeMonster={IncludesMonsterSource(request).ToString().ToLowerInvariant()}",
            $"includeMonsterProtection={IsZombieBossOnly(request).ToString().ToLowerInvariant()}",
            $"includeTrain={(request.SelectedTypes.Contains("truck", StringComparer.Ordinal) || request.SelectedTypes.Contains("railway", StringComparer.Ordinal)).ToString().ToLowerInvariant()}",
            $"includeDispatch={request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeGhost={request.SelectedTypes.Contains("ghost", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeTreasure={request.SelectedTypes.Contains("treasure", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
            $"includeResourceDetails={request.SelectedTypes.Contains("resource", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
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
                return ValidateFastCityBatchResult(
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
            }
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client fast City batch did not return a correlated result.");
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
        int bossCount = RequireNonNegativeInt(root, "monsterInvasionBossCount");
        int protectionTargets = RequireNonNegativeInt(root, "monsterProtectionDetailTargetCount");
        int protectionRequests = RequireNonNegativeInt(root, "monsterProtectionDetailRequestCount");
        int protectionReady = RequireNonNegativeInt(root, "monsterProtectionDetailReadyCount");
        int normalizedBossCount = monsters.Count(item => item.ProtectionEligible);
        bool zombieBossOnly = IsZombieBossOnly(request);
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
                return ValidateResourceScanDetailResult(
                    root.Value, requestId, startedAt, session, request, resourceRecordCount);
            await DelayAsync(PollDelay, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("The current-client Resource detail enrichment did not return a correlated result.");
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
            if (!details.TryAdd(recordKey, new ResourceScanDetail(received, remaining, fullAmount)))
                throw new InvalidDataException("Resource detail enrichment returned a duplicate record key.");
        }
        if (details.Count != targetCount)
            throw new InvalidDataException("Resource detail enrichment target/detail counts differ.");
        string? error = ReadOptionalString(root, "error");
        return new ResourceScanDetailObservation(
            targetCount, requestCount, cacheBeforeCount, sendFailureCount, readyCount, details, error);
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
            !MatchesBool(root, "includeMonster", IncludesMonsterSource(request)) ||
            !MatchesBool(root, "includeMonsterProtection", IsZombieBossOnly(request)) ||
            !MatchesBool(root, "includeTrain", request.SelectedTypes.Contains("truck", StringComparer.Ordinal) ||
                request.SelectedTypes.Contains("railway", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeDispatch", request.SelectedTypes.Contains("dispatch", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeGhost", request.SelectedTypes.Contains("ghost", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeTreasure", request.SelectedTypes.Contains("treasure", StringComparer.Ordinal)) ||
            !MatchesBool(root, "includeResourceDetails", request.SelectedTypes.Contains("resource", StringComparer.Ordinal)))
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
        if (!MatchesBool(root, "responseFlagsTransitioned", true) ||
            !MatchesBool(root, "cameraTileStable", true) ||
            !MatchesBool(root, "positionRestoredBeforeResponse", true) ||
            !MatchesString(root, "requestMethod", "WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore"))
            throw new InvalidDataException("Fast world batch did not prove the native response/restoration contract.");
        if (!MatchesInt(root, "serverLod", 0) ||
            !MatchesInt(root, "blockSize", FastCityAoiBlockSize) ||
            !MatchesInt(root, "blockCount", FastCityAoiBlockCount) ||
            !MatchesInt(root, "postServerLod", 0) ||
            !MatchesInt(root, "postBlockSize", FastCityAoiBlockSize) ||
            !MatchesInt(root, "postBlockCount", FastCityAoiBlockCount))
            throw new InvalidDataException("Fast world batch AOI geometry changed during acquisition.");
        int[] requestedIndices = RequireNonNegativeIntArray(root, "requestedIndices");
        if (requestedIndices.Length < FastCityExpectedAoiCount ||
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
        int matchedResourceCount = RequireNonNegativeInt(root, "matchedResourceCount");
        int matchedDispatchCount = RequireNonNegativeInt(root, "matchedDispatchCount");
        int matchedGhostCount = RequireNonNegativeInt(root, "matchedGhostCount");
        int matchedTreasureCount = RequireNonNegativeInt(root, "matchedTreasureCount");
        IReadOnlyList<FirstLivePreparedResource> prepared = request.SelectedTypes.Contains("city", StringComparer.Ordinal) && HasPointKind(pointRecords, "player_base")
            ? FirstLiveResultImporter.PrepareCitySnapshot(bytes, resultPath)
            : Array.Empty<FirstLivePreparedResource>();
        IReadOnlyList<FirstLivePreparedResource> resources = request.SelectedTypes.Contains("resource", StringComparer.Ordinal) && HasPointKind(pointRecords, "resource_point")
            ? FirstLiveResultImporter.PrepareResourceSnapshot(bytes, resultPath)
            : Array.Empty<FirstLivePreparedResource>();
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
        return new FastCityBatchObservation(requestedIndices, prepared, resources, dispatches, ghosts, treasures, monsters, trains,
            monsterInvasionBossCount, monsterProtectionDetailTargetCount, monsterProtectionDetailRequestCount, monsterProtectionDetailReadyCount);
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
            result.Add(new FastDispatchPrepared(x, y, new MapStoredRecord(
                "dispatch", serverId, recordKey, pointId, uuid, null, null,
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
        bool zombieBossOnly = IsZombieBossOnly(request);
        string outputKind = zombieBossOnly ? "zombie_boss" : "monster";
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
            if (zombieBossOnly ? !protectionEligible : protectionEligible) continue;
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
            result.Add(new FastMonsterPrepared(x, y, protectionEligible, new MapStoredRecord(outputKind, serverId, uuid, pointIndex, uuid, nameKey, null, level, null, null, distance, shieldEndTime, updatedAt, data.ToJsonString(JsonOptions.Default))));
        }
        return result;
    }

    private static IReadOnlyList<FastTrainPrepared> PrepareTrainRecords(
        JsonElement root,
        MapScanExecutionRequest request,
        HashSet<int> requestedSet,
        DateTimeOffset capturedAt)
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
            long updatedAt = capturedAt.ToUnixTimeMilliseconds();
            long? power = row.TryGetProperty("power", out JsonElement powerValue) && powerValue.TryGetInt64(out long parsedPower) && parsedPower >= 0 ? parsedPower : null;
            string? ownerName = OptionalStringValue(row, "ownerName");
            string? allianceName = OptionalStringValue(row, "allianceName");
            int? pointIndex = row.TryGetProperty("positionIndex", out JsonElement pi) && pi.TryGetInt32(out int piv) ? piv : null;
            long? arriveTs = OptionalPositiveInt64(row, "arriveTs");
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
                ["source"] = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
            };
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
        }

        internal OverviewMapScanSession Session { get; }
        internal string RunId { get; }
        internal int ServerId { get; }
        internal long WorldId { get; }
        internal string SelectedTypesKey { get; }
        internal HashSet<int> CompletedRequestOrdinals { get; } = new();
        internal HashSet<int> Covered { get; } = new();
        internal Dictionary<string, FirstLivePreparedResource> CityRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FirstLivePreparedResource> ResourceRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastDispatchPrepared> DispatchRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastGhostPrepared> GhostRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastTreasurePrepared> TreasureRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastMonsterPrepared> MonsterRecords { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, FastTrainPrepared> TrainRecords { get; } = new(StringComparer.Ordinal);

        internal bool Matches(OverviewMapScanSession session, MapScanExecutionRequest request) =>
            Session == session && RunId == request.RunId && ServerId == request.ServerId && WorldId == request.WorldId &&
            SelectedTypesKey == string.Join("\u001f", request.SelectedTypes.OrderBy(value => value, StringComparer.Ordinal));
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
    private sealed record ResourceScanDetail(bool Received, long RemainingAmount, long FullAmount);
    private sealed record ResourceScanDetailObservation(
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
    private sealed record FastCityBatchObservation(
        int[] RequestedIndices,
        IReadOnlyList<FirstLivePreparedResource> Prepared,
        IReadOnlyList<FirstLivePreparedResource> Resources,
        IReadOnlyList<FastDispatchPrepared> Dispatches,
        IReadOnlyList<FastGhostPrepared> Ghosts,
        IReadOnlyList<FastTreasurePrepared> Treasures,
        IReadOnlyList<FastMonsterPrepared> Monsters,
        IReadOnlyList<FastTrainPrepared> Trains,
        int MonsterInvasionBossCount,
        int MonsterProtectionDetailTargetCount,
        int MonsterProtectionDetailRequestCount,
        int MonsterProtectionDetailReadyCount);
}
