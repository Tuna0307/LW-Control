using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    private const int FastCityAoiBlockSize = 10;
    private const int FastCityAoiBlockCount = 100;
    private const int FastCityGroupColumns = 2;
    private const int FastCityGroupRows = 5;
    private const int FastCityExpectedAoiCount = 40;
    private static readonly TimeSpan FastCityProbeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FastCityStartupSettleDelay = TimeSpan.FromSeconds(3);
    private string? fastCitySettledSessionId;

    public async Task<IReadOnlyList<MapScanBlockCapture>> CaptureBatchAsync(
        MapScanExecutionRequest request,
        MapScanTargetBlock seedBlock,
        IReadOnlySet<int> pendingBlockIndices,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingBlockIndices);
        if (!CanUseFastCityBatch(request))
            return [await CaptureAsync(request, seedBlock, cancellationToken).ConfigureAwait(false)];

        OverviewMapScanSession session = RequireReadySession();
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
            return await CaptureFullCityMapAsync(session, request, pendingBlockIndices, cancellationToken)
                .ConfigureAwait(false);

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

            var recordsByBlock = observation.Prepared
                .GroupBy(item => checked((item.Import.Y / MapScanGeometry.RecoveredBlockSpan) * 50 +
                                         (item.Import.X / MapScanGeometry.RecoveredBlockSpan)))
                .ToDictionary(group => group.Key, group => group.Select(item => item.Record).ToArray());
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

    private async Task<IReadOnlyList<MapScanBlockCapture>> CaptureFullCityMapAsync(
        OverviewMapScanSession session,
        MapScanExecutionRequest request,
        IReadOnlySet<int> pendingBlockIndices,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MapScanTargetBlock> logicalBlocks = MapScanTraversal.Build(
            request.TileWidth, request.TileHeight);
        if (logicalBlocks.Count != 2500 || pendingBlockIndices.Count != logicalBlocks.Count ||
            logicalBlocks.Any(block => !pendingBlockIndices.Contains(block.BlockIndex)))
            throw new InvalidDataException("Fast full-City acquisition requires all 2,500 logical blocks pending.");

        var covered = new HashSet<int>();
        var cityRecords = new Dictionary<string, FirstLivePreparedResource>(StringComparer.Ordinal);
        for (int row = 0; row < 10; row++)
        {
            int groupStartRow = row * FastCityGroupRows;
            int targetY = 75 + (row * 100);
            for (int column = 0; column < 25; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int groupStartColumn = column * FastCityGroupColumns;
                int targetX = 15 + (column * 40);
                FastCityBatchObservation? observation = null;
                Exception? lastError = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        observation = await ProbeFastCityBatchAsync(
                            session, request, groupStartColumn, groupStartRow, targetX, targetY, cancellationToken)
                            .ConfigureAwait(false);
                        lastError = null;
                        break;
                    }
                    catch (Exception error) when (error is TimeoutException or InvalidDataException)
                    {
                        lastError = error;
                        if (attempt < 3)
                            await DelayAsync(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
                    }
                }
                if (lastError is not null || observation is null) throw lastError!;
                RequireSameSession(session);
                foreach (int index in observation.RequestedIndices)
                    covered.Add(index);
                foreach (FirstLivePreparedResource prepared in observation.Prepared)
                {
                    if (!cityRecords.TryGetValue(prepared.Record.RecordKey, out FirstLivePreparedResource? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        cityRecords[prepared.Record.RecordKey] = prepared;
                }
            }
        }

        if (covered.Count != 10000)
            throw new InvalidDataException($"Fast full-City acquisition covered {covered.Count}/10000 AOIs.");

        var recordsByBlock = cityRecords.Values
            .GroupBy(item => checked((item.Import.Y / MapScanGeometry.RecoveredBlockSpan) * 50 +
                                     (item.Import.X / MapScanGeometry.RecoveredBlockSpan)))
            .ToDictionary(group => group.Key, group => group.Select(item => item.Record).ToArray());
        var captures = new List<MapScanBlockCapture>(logicalBlocks.Count);
        foreach (MapScanTargetBlock block in logicalBlocks)
        {
            IReadOnlyList<MapStoredRecord> records = recordsByBlock.TryGetValue(
                block.BlockIndex, out MapStoredRecord[]? value) ? value : Array.Empty<MapStoredRecord>();
            string payload = JsonSerializer.Serialize(new
            {
                protocol = "current_fast_full_city_v1",
                blockIndex = block.BlockIndex,
                coverage = "all_four_lod0_aoi_cells_proven_by_full_map_union",
                recordsInBlock = records.Count,
                coveredAoiCells = covered.Count,
            }, JsonOptions.Default);
            captures.Add(new MapScanBlockCapture(request.ServerId, request.WorldId, block.BlockIndex, payload, records));
        }
        return captures;
    }

    private static bool CanUseFastCityBatch(MapScanExecutionRequest request) =>
        request.SelectedTypes.Count == 1 && request.SelectedTypes[0] == "city" &&
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
            "viewLevel=-1",
            "requestMode=coverage",
            $"targetTileX={targetX.ToString(CultureInfo.InvariantCulture)}",
            $"targetTileY={targetY.ToString(CultureInfo.InvariantCulture)}",
            "requestedCount=8",
            "holdMilliseconds=0",
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
            !MatchesString(root, "requestMode", "coverage"))
            throw new InvalidDataException("Fast City batch result did not match the active owned game session.");
        if (!MatchesInt(root, "requestedCount", 8) ||
            !MatchesInt(root, "holdMilliseconds", 0) ||
            !MatchesInt(root, "viewLevel", -1) ||
            !MatchesInt(root, "targetTileX", targetX) ||
            !MatchesInt(root, "targetTileY", targetY))
            throw new InvalidDataException("Fast City batch result did not match the requested acquisition parameters.");
        if (!MatchesString(root, "state", "proven"))
        {
            string error = ReadOptionalString(root, "error") ?? "unknown fast City batch failure";
            RequireFreshCaptureTime(root, startedAt);
            throw new InvalidDataException("Fast City batch failed: " + error);
        }
        RequireFreshCaptureTime(root, startedAt);
        if (!MatchesBool(root, "responseFlagsTransitioned", true) ||
            !MatchesBool(root, "cameraTileStable", true) ||
            !MatchesBool(root, "positionRestoredBeforeResponse", true) ||
            !MatchesString(root, "requestMethod", "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift"))
            throw new InvalidDataException("Fast City batch did not prove the native response/restoration contract.");
        if (!MatchesInt(root, "serverLod", 0) ||
            !MatchesInt(root, "blockSize", FastCityAoiBlockSize) ||
            !MatchesInt(root, "blockCount", FastCityAoiBlockCount) ||
            !MatchesInt(root, "postServerLod", 0) ||
            !MatchesInt(root, "postBlockSize", FastCityAoiBlockSize) ||
            !MatchesInt(root, "postBlockCount", FastCityAoiBlockCount))
            throw new InvalidDataException("Fast City batch AOI geometry changed during acquisition.");
        int[] requestedIndices = RequireNonNegativeIntArray(root, "requestedIndices");
        if (requestedIndices.Length < FastCityExpectedAoiCount ||
            requestedIndices.Any(index => index >= FastCityAoiBlockCount * FastCityAoiBlockCount))
            throw new InvalidDataException("Fast City batch returned an invalid native AOI footprint.");
        if (!MatchesInt(root, "nativeCurrentSetCount", requestedIndices.Length))
            throw new InvalidDataException("Fast City batch native AOI count did not match its copied footprint.");
        int[] expectedIndices = ExpectedGroupAoiIndices(groupStartColumn, groupStartRow);
        var requestedSet = requestedIndices.ToHashSet();
        if (expectedIndices.Any(index => !requestedSet.Contains(index)))
            throw new InvalidDataException("Fast City batch did not cover all AOIs required by its logical block group.");

        if (!root.TryGetProperty("point_records", out JsonElement pointRecords) ||
            pointRecords.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Fast City batch is missing its Player City record snapshot.");
        IReadOnlyList<FirstLivePreparedResource> prepared = pointRecords.GetArrayLength() == 0
            ? Array.Empty<FirstLivePreparedResource>()
            : FirstLiveResultImporter.PrepareCitySnapshot(bytes, resultPath);
        if (prepared.Count != pointRecords.GetArrayLength())
            throw new InvalidDataException("Fast City batch record snapshot count changed during normalization.");
        foreach (FirstLivePreparedResource item in prepared)
        {
            if (item.Import.ServerId != request.ServerId)
                throw new InvalidDataException("Fast City batch contained a Player City from a different server.");
            int cellX = item.Import.X / FastCityAoiBlockSize;
            int cellY = item.Import.Y / FastCityAoiBlockSize;
            int aoiIndex = checked(cellY * FastCityAoiBlockCount + cellX);
            if (!requestedSet.Contains(aoiIndex))
                throw new InvalidDataException("Fast City batch record fell outside the native AOI footprint.");
        }
        return new FastCityBatchObservation(requestedIndices, prepared);
    }
    private static int[] ExpectedGroupAoiIndices(int groupStartColumn, int groupStartRow)
    {
        int startCellX = checked(groupStartColumn * 2);
        int startCellY = checked(groupStartRow * 2);
        var result = new List<int>(FastCityExpectedAoiCount);
        for (int cellY = startCellY; cellY < startCellY + 10; cellY++)
            for (int cellX = startCellX; cellX < startCellX + 4; cellX++)
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

    private sealed record FastCityBatchObservation(
        int[] RequestedIndices,
        IReadOnlyList<FirstLivePreparedResource> Prepared);
}
