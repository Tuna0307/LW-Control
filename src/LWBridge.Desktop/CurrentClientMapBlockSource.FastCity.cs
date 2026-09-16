using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed partial class CurrentClientMapBlockSource
{
    private const int FastCityAoiBlockSize = 10;
    private const int FastCityAoiBlockCount = 100;
    private const int FastCityGroupColumns = 2;
    private const int FastCityGroupRows = 5;
    private const int FastCityExpectedAoiCount = 40;
    // LWB-R7-012 live-measured current-client full-world footprint: 5 AOI columns x 10 AOI rows.
    // The ordinary partial-band path intentionally keeps its recovered 2x5 logical-block grouping.
    private const int FastFullWorldAoiColumns = 5;
    private const int FastFullWorldAoiRows = 10;
    private const int FastFullWorldColumnRequests = FastCityAoiBlockCount / FastFullWorldAoiColumns;
    private const int FastFullWorldRowRequests = FastCityAoiBlockCount / FastFullWorldAoiRows;
    private static readonly TimeSpan FastCityProbeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FastCityStartupSettleDelay = TimeSpan.FromSeconds(3);
    private string? fastCitySettledSessionId;

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
            return await CaptureFullCityMapAsync(session, request, pendingBlockIndices, progress, cancellationToken)
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

        var covered = new HashSet<int>();
        var cityRecords = new Dictionary<string, FirstLivePreparedResource>(StringComparer.Ordinal);
        var resourceRecords = new Dictionary<string, FirstLivePreparedResource>(StringComparer.Ordinal);
        var monsterRecords = new Dictionary<string, FastMonsterPrepared>(StringComparer.Ordinal);
        for (int row = 0; row < FastFullWorldRowRequests; row++)
        {
            int groupStartRow = row * FastCityGroupRows;
            int targetY = 75 + (row * 100);
            for (int column = 0; column < FastFullWorldColumnRequests; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Current v17 returns five consecutive LOD0 AOI columns around these targets.
                // Keep a four-column validator subset inside that measured footprint; the final
                // covered.Count == 10000 invariant independently rejects any missing fifth column.
                int footprintStartCellX = column * FastFullWorldAoiColumns;
                int groupStartColumn = (footprintStartCellX + 1) / 2;
                int targetX = 25 + (column * 50);
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
                foreach (FirstLivePreparedResource prepared in observation.Resources)
                {
                    if (!resourceRecords.TryGetValue(prepared.Record.RecordKey, out FirstLivePreparedResource? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        resourceRecords[prepared.Record.RecordKey] = prepared;
                }
                foreach (FastMonsterPrepared prepared in observation.Monsters)
                {
                    if (!monsterRecords.TryGetValue(prepared.Record.RecordKey, out FastMonsterPrepared? prior) ||
                        prepared.Record.UpdatedAt >= prior.Record.UpdatedAt)
                        monsterRecords[prepared.Record.RecordKey] = prepared;
                }
                progress?.Invoke(new MapScanSourceProgress(covered.Count * 100d / 10000d));
            }
        }

        if (covered.Count != 10000)
            throw new InvalidDataException($"Fast full-world acquisition covered {covered.Count}/10000 AOIs.");

        var buckets = new Dictionary<int, List<MapStoredRecord>>();
        if (request.SelectedTypes.Contains("city", StringComparer.Ordinal))
            foreach (FirstLivePreparedResource item in cityRecords.Values)
                AddRecordToBlock(buckets, item.Import.X, item.Import.Y, item.Record);
        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal))
            foreach (FirstLivePreparedResource item in resourceRecords.Values)
                AddRecordToBlock(buckets, item.Import.X, item.Import.Y, item.Record);
        if (request.SelectedTypes.Contains("monster", StringComparer.Ordinal))
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
                protocol = "current_fast_full_world_v2",
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
        request.SelectedTypes.Count is >= 1 and <= 3 &&
        request.SelectedTypes.All(type => type is "city" or "resource" or "monster") &&
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
            $"homeTileX={(request.PlayerTileX ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"homeTileY={(request.PlayerTileY ?? -1).ToString(CultureInfo.InvariantCulture)}",
            $"includeMonster={request.SelectedTypes.Contains("monster", StringComparer.Ordinal).ToString().ToLowerInvariant()}",
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
            !MatchesString(root, "requestMode", "coverage") ||
            !MatchesBool(root, "includeMonster", request.SelectedTypes.Contains("monster", StringComparer.Ordinal)))
            throw new InvalidDataException("Fast world batch result did not match the active owned game session.");
        if (!MatchesInt(root, "requestedCount", 8) ||
            !MatchesInt(root, "holdMilliseconds", 0) ||
            !MatchesInt(root, "homeTileX", request.PlayerTileX ?? -1) ||
            !MatchesInt(root, "homeTileY", request.PlayerTileY ?? -1) ||
            !MatchesInt(root, "viewLevel", -1) ||
            !MatchesInt(root, "targetTileX", targetX) ||
            !MatchesInt(root, "targetTileY", targetY))
            throw new InvalidDataException("Fast world batch result did not match the requested acquisition parameters.");
        if (!MatchesString(root, "state", "proven"))
        {
            string error = ReadOptionalString(root, "error") ?? "unknown fast City batch failure";
            RequireFreshCaptureTime(root, startedAt);
            throw new InvalidDataException("Fast world batch failed: " + error);
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
        int[] expectedIndices = ExpectedGroupAoiIndices(groupStartColumn, groupStartRow);
        var requestedSet = requestedIndices.ToHashSet();
        if (expectedIndices.Any(index => !requestedSet.Contains(index)))
            throw new InvalidDataException("Fast world batch did not cover all AOIs required by its logical block group.");

        if (!root.TryGetProperty("point_records", out JsonElement pointRecords) ||
            pointRecords.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Fast world batch is missing its point record snapshot.");
        int matchedCityCount = RequireNonNegativeInt(root, "matchedCityCount");
        int matchedResourceCount = RequireNonNegativeInt(root, "matchedResourceCount");
        IReadOnlyList<FirstLivePreparedResource> prepared = request.SelectedTypes.Contains("city", StringComparer.Ordinal) && HasPointKind(pointRecords, "player_base")
            ? FirstLiveResultImporter.PrepareCitySnapshot(bytes, resultPath)
            : Array.Empty<FirstLivePreparedResource>();
        IReadOnlyList<FirstLivePreparedResource> resources = request.SelectedTypes.Contains("resource", StringComparer.Ordinal) && HasPointKind(pointRecords, "resource_point")
            ? FirstLiveResultImporter.PrepareResourceSnapshot(bytes, resultPath)
            : Array.Empty<FirstLivePreparedResource>();
        if (request.SelectedTypes.Contains("city", StringComparer.Ordinal) && prepared.Count != matchedCityCount)
            throw new InvalidDataException("Fast world batch City snapshot count changed during normalization.");
        if (request.SelectedTypes.Contains("resource", StringComparer.Ordinal) && resources.Count != matchedResourceCount)
            throw new InvalidDataException("Fast world batch Resource snapshot count changed during normalization.");
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
        IReadOnlyList<FastMonsterPrepared> monsters = request.SelectedTypes.Contains("monster", StringComparer.Ordinal)
            ? PrepareMonsterRecords(root, request, requestedSet, startedAt)
            : Array.Empty<FastMonsterPrepared>();
        return new FastCityBatchObservation(requestedIndices, prepared, resources, monsters);
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
        if (request.SelectedTypes.Contains("monster", StringComparer.Ordinal))
            foreach (FastMonsterPrepared item in observation.Monsters)
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

    private static IReadOnlyList<FastMonsterPrepared> PrepareMonsterRecords(JsonElement root, MapScanExecutionRequest request, HashSet<int> requestedSet, DateTimeOffset capturedAt)
    {
        if (!root.TryGetProperty("monster_march_records", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Fast world batch is missing its Monster march snapshot.");
        var result = new List<FastMonsterPrepared>(rows.GetArrayLength());
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
            long? shieldEndTime = row.TryGetProperty("zMBossShieldEndTime", out JsonElement shieldValue) &&
                shieldValue.TryGetInt64(out long parsedShield) && parsedShield > 0 ? parsedShield : null;
            data["level"] = level; data["updatedAt"] = updatedAt;
            if (distance is not null) data["distanceFromHome"] = distance.Value;
            if (shieldEndTime is not null) data["shieldEndTime"] = shieldEndTime.Value;
            int? pointIndex = row.TryGetProperty("positionIndex", out JsonElement pi) && pi.TryGetInt32(out int piv) ? piv : null;
            result.Add(new FastMonsterPrepared(x, y, new MapStoredRecord("monster", serverId, uuid, pointIndex, uuid, nameKey, null, level, null, null, distance, shieldEndTime, updatedAt, data.ToJsonString(JsonOptions.Default))));
        }
        return result;
    }

    private static int RequiredInt(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : throw new InvalidDataException("Monster field missing: " + name);
    private static string RequiredString(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new InvalidDataException("Monster field missing: " + name);

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

    private sealed record FastMonsterPrepared(int X, int Y, MapStoredRecord Record);
    private sealed record FastCityBatchObservation(
        int[] RequestedIndices,
        IReadOnlyList<FirstLivePreparedResource> Prepared,
        IReadOnlyList<FirstLivePreparedResource> Resources,
        IReadOnlyList<FastMonsterPrepared> Monsters);
}
