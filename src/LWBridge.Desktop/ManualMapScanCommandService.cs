using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class ManualMapScanCommandService : INativeAsyncCommandService
{
    private readonly object gate = new();
    private readonly MapDataStore store;
    private readonly CurrentClientMapBlockSource currentClientSource;
    private readonly Func<CancellationToken, Task<CurrentClientMapContext>> getContext;
    private readonly Func<int, CancellationToken, Task<CurrentClientServerJumpResult>>? jumpToServer;
    private readonly Func<int, long, CancellationToken, Task<CurrentClientMarchFollowResult>>? followMarch;
    private readonly Func<string?, string?, CancellationToken, Task<CurrentClientAssetImageResult>>? getAssetImage;
    private readonly Func<int, IReadOnlyList<CurrentClientTreasureInspectionRecord>, bool, CancellationToken, Task<CurrentClientTreasureInspectionResult>>? inspectTreasureStates;
    private readonly Func<int?>? getLiveServerId;
    private readonly Func<CancellationToken, Task<CurrentClientTrainListCoverageResult>>? getTrainListCoverage;
    private readonly Func<CancellationToken, Task<CurrentClientDispatchNearestResult>>? findNearestDispatch;
    private readonly IMapScanBlockSource blockSource;
    private CancellationTokenSource? activeCancellation;
    private MapScanProcessLease? activeScanLease;
    private Task? activeTask;
    private TaskCompletionSource<object?>? activeTerminal;
    private bool closed;
    private bool isReading;
    private string phase = "idle";
    private string scanRunId = string.Empty;
    private string scanMode = "auto";
    private string scanStrategy = "none";
    private IReadOnlyList<string> selectedTypes = ["city"];
    private int serverId;
    private long worldId;
    private int concurrency;
    private int totalBlocks;
    private int completedBlocks;
    private int failedBlocks;
    private int inflightBlocks;
    private int unreadBlocks;
    private double scanRate;
    private double? acquisitionProgressPercent;
    private bool coordinateJumping;
    private bool serverJumping;
    private bool treasureInspecting;
    private bool trainCoverageReading;
    private bool dispatchQuickFinding;
    private int liveServerId;
    private int[] truckMatchServerIds = [];
    private string? lastError;

    public ManualMapScanCommandService(
        OverviewLifecycleService lifecycle,
        MapDataStore store)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        currentClientSource = new CurrentClientMapBlockSource(lifecycle);
        getContext = currentClientSource.GetCurrentContextAsync;
        jumpToServer = currentClientSource.JumpToServerAsync;
        followMarch = currentClientSource.FollowMarchAsync;
        getAssetImage = currentClientSource.GetAssetImageAsync;
        inspectTreasureStates = currentClientSource.InspectTreasureStatesAsync;
        getLiveServerId = lifecycle.GetLiveServerId;
        getTrainListCoverage = currentClientSource.GetTrainListCoverageAsync;
        findNearestDispatch = currentClientSource.FindNearestDispatchAsync;
        blockSource = currentClientSource;
        ReconcileInterruptedScansAtStartup();
    }

    internal ManualMapScanCommandService(
        MapDataStore store,
        Func<CancellationToken, Task<CurrentClientMapContext>> getContext,
        IMapScanBlockSource blockSource,
        Func<int, CancellationToken, Task<CurrentClientServerJumpResult>>? jumpToServer = null,
        Func<int?>? getLiveServerId = null,
        Func<int, long, CancellationToken, Task<CurrentClientMarchFollowResult>>? followMarch = null,
        Func<string?, string?, CancellationToken, Task<CurrentClientAssetImageResult>>? getAssetImage = null,
        Func<int, IReadOnlyList<CurrentClientTreasureInspectionRecord>, bool, CancellationToken, Task<CurrentClientTreasureInspectionResult>>? inspectTreasureStates = null,
        Func<CancellationToken, Task<CurrentClientTrainListCoverageResult>>? getTrainListCoverage = null,
        Func<CancellationToken, Task<CurrentClientDispatchNearestResult>>? findNearestDispatch = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
        this.blockSource = blockSource ?? throw new ArgumentNullException(nameof(blockSource));
        this.jumpToServer = jumpToServer;
        this.getLiveServerId = getLiveServerId;
        this.followMarch = followMarch;
        this.getAssetImage = getAssetImage;
        this.inspectTreasureStates = inspectTreasureStates;
        this.getTrainListCoverage = getTrainListCoverage;
        this.findNearestDispatch = findNearestDispatch;
        currentClientSource = null!;
        ReconcileInterruptedScansAtStartup();
    }

    public event Action<object>? StatusChanged;

    public bool CanHandle(string command) =>
        command is "map_scan_start" or "map_scan_stop" or "map_scan_status" or "map_scan_clear" or
            "map_coordinate_jump" or "map_march_follow" or "server_jump" or
            "game_asset_image" or "map_train_list_coverage" or "map_dispatch_find_nearest" or
            "map_treasure_state_refresh" or "map_treasure_state_refresh_all" or
            "map_treasure_claim_status";

    public async Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (command == "game_asset_image")
            return await GetAssetImageAsync(payload, cancellationToken).ConfigureAwait(false);
        if (command == "map_train_list_coverage")
            return await ReadTrainListCoverageAsync(cancellationToken).ConfigureAwait(false);
        if (command == "map_dispatch_find_nearest")
            return await FindNearestDispatchAsync(cancellationToken).ConfigureAwait(false);
        if (command is "map_treasure_state_refresh" or
            "map_treasure_state_refresh_all" or
            "map_treasure_claim_status")
        {
            return await InspectTreasureStateAsync(command, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        if (command == "map_scan_status") return CreateStatus();
        if (command == "map_scan_clear") return ClearMapScan(payload);
        if (command == "server_jump")
            return await JumpToServerAsync(payload, cancellationToken).ConfigureAwait(false);
        if (command == "map_coordinate_jump")
            return await JumpToCoordinateAsync(payload, cancellationToken).ConfigureAwait(false);
        if (command == "map_march_follow")
            return await FollowMarchAsync(payload, cancellationToken).ConfigureAwait(false);
        if (command == "map_scan_stop")
        {
            await StopAsync(cancellationToken).ConfigureAwait(false);
            return CreateStatus();
        }
        if (command != "map_scan_start")
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Manual Map Scan cannot handle '{command}'.");

        MapScanStartOptions options = MapScanContract.NormalizeStart(payload);
        bool dedicatedZombieBossOnly = options.SelectedTypes.Count == 1 &&
            options.SelectedTypes[0] == "zombie_boss";
        bool recoveredMixedSelection = options.SelectedTypes.Count is >= 1 and <= 8 &&
            options.SelectedTypes.All(type => MapScanContract.RecoveredDefaultTypes.Contains(type, StringComparer.Ordinal));
        if (!dedicatedZombieBossOnly && !recoveredMixedSelection)
        {
            throw new BridgeCommandException(
                "LIVE_BLOCK_TYPES_UNSUPPORTED",
                "The shared current-client scanner supports any mixture of the original eight Map Data kinds; dedicated Zombie Boss must be scanned by itself.");
        }

        return await StartAsync(options, cancellationToken).ConfigureAwait(false);
    }


    private async Task<object> ReadTrainListCoverageAsync(CancellationToken cancellationToken)
    {
        if (getTrainListCoverage is null)
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                "Train-list coverage requires the live current-client source.");

        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing.");
            if (isReading)
                throw new BridgeCommandException("SCAN_RUNNING", "stop the map scan first");
            if (coordinateJumping || serverJumping || treasureInspecting || trainCoverageReading || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            trainCoverageReading = true;
        }

        try
        {
            CurrentClientTrainListCoverageResult result =
                await getTrainListCoverage(cancellationToken).ConfigureAwait(false);
            int[] match = result.MatchServerIds.Distinct().OrderBy(id => id).ToArray();
            lock (gate)
            {
                liveServerId = result.LiveServerId;
                if (serverId <= 0) serverId = result.LiveServerId;
                truckMatchServerIds = match;
            }
            PublishStatusChanged();
            return new
            {
                liveServerId = result.LiveServerId,
                matchServerIds = match,
                truckServerIds = result.TruckServerIds.ToArray(),
                railwayServerIds = result.RailwayServerIds.ToArray(),
            };
        }
        finally
        {
            lock (gate) trainCoverageReading = false;
        }
    }

    private async Task<object> FindNearestDispatchAsync(CancellationToken cancellationToken)
    {
        if (findNearestDispatch is null)
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                "Dispatch Quick Find requires the live current-client source.");

        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException(
                    "MAP_SCAN_CLOSED",
                    "The Map Data window is closing.");
            if (isReading)
                throw new BridgeCommandException(
                    "SCAN_RUNNING",
                    "stop the map scan first");
            if (coordinateJumping || serverJumping || treasureInspecting ||
                trainCoverageReading || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            dispatchQuickFinding = true;
        }

        try
        {
            CurrentClientDispatchNearestResult result =
                await findNearestDispatch(cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                liveServerId = result.LiveServerId;
                if (serverId <= 0) serverId = result.LiveServerId;
                lastError = null;
            }
            PublishStatusChanged();
            return new
            {
                serverId = result.ServerId,
                pointId = result.PointId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                x = result.X,
                y = result.Y,
                liveServerId = result.LiveServerId,
                pointDataResolved = result.PointDataResolved,
                runtimeClass = result.RuntimeClass,
                pointType = result.PointType,
                cfgId = result.CfgId,
                elapsedSeconds = result.ElapsedSeconds,
                identitySource = result.IdentitySource,
            };
        }
        finally
        {
            lock (gate) dispatchQuickFinding = false;
        }
    }

    private async Task<object> InspectTreasureStateAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (inspectTreasureStates is null)
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                "Treasure state inspection requires the live current-client source.");

        int requestedServerId;
        if (command == "map_treasure_claim_status")
        {
            requestedServerId = getLiveServerId?.Invoke() ?? 0;
            if (requestedServerId <= 0)
                throw new BridgeCommandException("GAME_DISCONNECTED", "game disconnected");
        }
        else
        {
            requestedServerId = MapDataQueryContract.RequiredServerId(payload);
        }

        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException(
                    "MAP_SCAN_CLOSED",
                    "The Map Data window is closing.");
            if (isReading)
                throw new BridgeCommandException(
                    "SCAN_RUNNING",
                    "stop the map scan first");
            if (coordinateJumping || serverJumping || treasureInspecting || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            treasureInspecting = true;
        }

        try
        {
            int liveServerId = getLiveServerId?.Invoke() ?? 0;
            if (liveServerId <= 0)
                throw new BridgeCommandException("GAME_DISCONNECTED", "game disconnected");
            if (requestedServerId != liveServerId)
                throw new BridgeCommandException(
                    "SERVER_MISMATCH",
                    "current game server does not match map data server");

            IReadOnlyList<MapStoredRecord> published = store.ReadRecords("treasure", requestedServerId);
            IReadOnlyList<MapStoredRecord> selected;
            if (command == "map_treasure_claim_status")
            {
                selected = Array.Empty<MapStoredRecord>();
            }
            else if (command == "map_treasure_state_refresh_all")
            {
                selected = published;
            }
            else
            {
                selected = SelectRequestedTreasureRecords(payload, published);
            }

            CurrentClientTreasureInspectionRecord[] records =
                selected.Select(BuildTreasureInspectionRecord).ToArray();

            string? playerUid = null;
            string? allianceId = null;
            var states = new List<JsonElement>(records.Length);
            if (records.Length == 0)
            {
                CurrentClientTreasureInspectionResult empty = await inspectTreasureStates(
                        requestedServerId,
                        Array.Empty<CurrentClientTreasureInspectionRecord>(),
                        false,
                        cancellationToken)
                    .ConfigureAwait(false);
                playerUid = empty.PlayerUid;
                allianceId = empty.AllianceId;
                states.AddRange(empty.States.Select(state => state.Clone()));
            }
            else
            {
                for (int offset = 0; offset < records.Length; offset += 100)
                {
                    CurrentClientTreasureInspectionRecord[] batch =
                        records.Skip(offset).Take(Math.Min(100, records.Length - offset)).ToArray();
                    CurrentClientTreasureInspectionResult result = await inspectTreasureStates(
                            requestedServerId,
                            batch,
                            true,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (playerUid is null)
                    {
                        playerUid = result.PlayerUid;
                        allianceId = result.AllianceId;
                    }
                    else if (!string.Equals(playerUid, result.PlayerUid, StringComparison.Ordinal) ||
                             !string.Equals(allianceId, result.AllianceId, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Treasure inspection player identity changed between internal batches.");
                    }
                    states.AddRange(result.States.Select(state => state.Clone()));
                }
            }

            if (string.IsNullOrEmpty(playerUid) || allianceId is null)
                throw new InvalidDataException("Treasure inspection omitted player identity.");

            if (states.Count > 0)
            {
                var sourceByUuid = records.ToDictionary(record => record.Uuid, StringComparer.Ordinal);
                long updatedAt = RecoveredWallClock.UnixTimeMilliseconds();
                var cacheRows = new List<MapTreasureClaimState>(states.Count);
                foreach (JsonElement state in states)
                {
                    string uuid = state.GetProperty("uuid").GetString()!;
                    CurrentClientTreasureInspectionRecord source = sourceByUuid[uuid];
                    long? expireTime = ReadOptionalNonNegativeInt64(state, "expireTime") ??
                                       source.ExpireTime;
                    cacheRows.Add(new MapTreasureClaimState(
                        requestedServerId,
                        playerUid,
                        uuid,
                        expireTime,
                        updatedAt,
                        state.GetRawText()));
                }
                store.UpsertTreasureClaimStates(cacheRows, updatedAt);
            }

            return new
            {
                playerUid,
                allianceId,
                states = states.ToArray(),
            };
        }
        finally
        {
            lock (gate) treasureInspecting = false;
        }
    }

    private static IReadOnlyList<MapStoredRecord> SelectRequestedTreasureRecords(
        JsonElement payload,
        IReadOnlyList<MapStoredRecord> published)
    {
        if (!payload.TryGetProperty("records", out JsonElement rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "Treasure state records are required.");
        }
        if (rows.GetArrayLength() > 200)
            throw new BridgeCommandException(
                "INVALID_REQUEST",
                "Treasure page refresh cannot exceed 200 records.");

        var byUuid = published
            .Where(record => !string.IsNullOrEmpty(record.Uuid))
            .ToDictionary(record => record.Uuid!, StringComparer.Ordinal);
        var selected = new List<MapStoredRecord>(rows.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("uuid", out JsonElement uuidValue) ||
                uuidValue.ValueKind != JsonValueKind.String)
            {
                throw new BridgeCommandException(
                    "INVALID_REQUEST",
                    "Treasure state record UUID is required.");
            }
            string uuid = uuidValue.GetString() ?? string.Empty;
            if (uuid.Length == 0 || !seen.Add(uuid) || !byUuid.TryGetValue(uuid, out MapStoredRecord? record))
            {
                throw new BridgeCommandException(
                    "INVALID_REQUEST",
                    "Treasure state records must uniquely match published rows.");
            }
            selected.Add(record);
        }
        return selected;
    }

    private static CurrentClientTreasureInspectionRecord BuildTreasureInspectionRecord(
        MapStoredRecord record)
    {
        if (record.PointIndex is not > 0)
            throw new BridgeCommandException(
                "TREASURE_STATE_UNAVAILABLE",
                "Treasure state is unavailable.",
                "published Treasure row has no point index");

        try
        {
            using JsonDocument document = JsonDocument.Parse(record.DataJson);
            JsonElement row = document.RootElement;
            string uuid = record.Uuid ??
                (row.TryGetProperty("uuid", out JsonElement uuidValue) &&
                 uuidValue.ValueKind == JsonValueKind.String
                    ? uuidValue.GetString() ?? string.Empty
                    : string.Empty);
            int treasureType = checked((int)(ReadOptionalNonNegativeInt64(row, "treasureType") ?? 0));
            int suppliesType = checked((int)(ReadOptionalNonNegativeInt64(row, "suppliesType") ?? 0));
            if (uuid.Length == 0 ||
                !((treasureType > 0 && suppliesType == 0) ||
                  (treasureType == 0 && suppliesType > 0)))
            {
                throw new BridgeCommandException(
                    "TREASURE_STATE_UNAVAILABLE",
                    "Treasure state is unavailable.",
                    "published Treasure row has invalid type identity");
            }

            return new CurrentClientTreasureInspectionRecord(
                record.ServerId,
                record.PointIndex.Value,
                uuid,
                treasureType,
                suppliesType,
                ReadOptionalString(row, "allianceId") ?? string.Empty,
                ReadOptionalString(row, "viewerUid") ?? string.Empty,
                ReadOptionalString(row, "viewerAllianceId") ?? string.Empty,
                ReadOptionalBoolean(row, "viewerHasReward"),
                ReadOptionalBoolean(row, "viewerIsWorking"),
                ReadOptionalBoolean(row, "complete"),
                ReadOptionalNonNegativeInt64(row, "expireTime"),
                ReadOptionalNonNegativeInt64(row, "startTime"),
                ReadOptionalNonNegativeInt64(row, "completionTime"),
                ReadOptionalNonNegativeInt32(row, "rewardedCount"),
                ReadOptionalNonNegativeInt32(row, "diggingCount"),
                ReadOptionalNonNegativeInt32(row, "rewardMax"),
                ReadOptionalNonNegativeInt32(row, "remainingBoxes"),
                ReadOptionalNonNegativeInt64(row, "createTime"),
                ReadOptionalString(row, "discovererAllianceId") ?? string.Empty,
                ReadOptionalString(row, "discovererUid") ?? string.Empty,
                ReadOptionalNonNegativeInt32(row, "workState"),
                ReadOptionalNonNegativeInt32(row, "userCount"));
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException(
                "MAP_INDEX_CORRUPT",
                "Stored Treasure row contains invalid JSON.",
                error.Message);
        }
    }

    private static long? ReadOptionalNonNegativeInt64(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long numeric))
            return numeric >= 0 ? numeric : null;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out long parsed))
            return parsed >= 0 ? parsed : null;
        return null;
    }

    private static int? ReadOptionalNonNegativeInt32(JsonElement row, string name)
    {
        long? value = ReadOptionalNonNegativeInt64(row, name);
        return value is >= 0 and <= int.MaxValue ? checked((int)value.Value) : null;
    }

    private static string? ReadOptionalString(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool? ReadOptionalBoolean(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private async Task<object> GetAssetImageAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (getAssetImage is null)
            throw new BridgeCommandException(
                "GAME_CONNECTION_UNAVAILABLE",
                "game connection unavailable");

        string? assetPath = ReadOptionalAssetImageString(payload, "assetPath");
        string? spriteName = ReadOptionalAssetImageString(payload, "spriteName");
        CurrentClientAssetImageResult result = await getAssetImage(
                assetPath,
                spriteName,
                cancellationToken)
            .ConfigureAwait(false);
        return new
        {
            dataUrl = result.DataUrl,
        };
    }

    private static string? ReadOptionalAssetImageString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new BridgeCommandException(
                "INVALID_ASSET",
                "invalid PNG asset",
                $"{name} must be a string");
        return value.GetString();
    }

    private object ClearMapScan(JsonElement payload)
    {
        int requestedServerId = MapDataQueryContract.RequiredServerIdOrAll(payload);
        bool shouldResolveLiveServer;
        lock (gate)
            shouldResolveLiveServer = !closed && !isReading && serverId <= 0;

        int? resolvedServerId = shouldResolveLiveServer ? getLiveServerId?.Invoke() : null;
        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing.");
            if (isReading)
                throw new BridgeCommandException(
                    MapScanClearOwnership.ActiveScanErrorCode,
                    MapScanClearOwnership.ActiveScanErrorMessage);
            if (serverId <= 0 && resolvedServerId is > 0) serverId = resolvedServerId.Value;

            // OWNER WORKFLOW R7-147: Clear manages locally published scan data. It
            // must not require the currently viewed data server to equal the live
            // game server after an Auto Scan has returned to its origin. serverId=0
            // clears all scan rows from this LWBridge session; marks/settings survive.
            if (requestedServerId == 0) store.ClearAllScanData();
            else store.ClearServer(requestedServerId);
            scanRunId = string.Empty;
            phase = "idle";
            scanMode = "auto";
            scanStrategy = "none";
            worldId = 0;
            concurrency = 0;
            totalBlocks = 0;
            completedBlocks = 0;
            failedBlocks = 0;
            inflightBlocks = 0;
            unreadBlocks = 0;
            scanRate = 0;
            acquisitionProgressPercent = null;
            lastError = null;
        }

        PublishStatusChanged();
        return CreateStatus();
    }

    private async Task<object> JumpToServerAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (!payload.TryGetProperty("serverId", out JsonElement value) ||
            !value.TryGetInt32(out int targetServerId) ||
            targetServerId is < 1 or > 99999)
        {
            throw new BridgeCommandException(
                "INVALID_SERVER_ID",
                "server ID must be an integer from 1 to 99999");
        }

        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing.");
            if (isReading || coordinateJumping || serverJumping || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            if (jumpToServer is null)
                throw new BridgeCommandException(
                    "COMMAND_NOT_IMPLEMENTED",
                    "Server jump requires the live current-client source.");
            serverJumping = true;
        }

        try
        {
            CurrentClientServerJumpResult result = await jumpToServer(
                targetServerId, cancellationToken).ConfigureAwait(false);
            lock (gate)
            {
                serverId = targetServerId;
                liveServerId = targetServerId;
                truckMatchServerIds = [];
                lastError = null;
            }
            PublishStatusChanged();
            return new
            {
                previousServerId = result.PreviousServerId,
                changed = result.Changed,
            };
        }
        finally
        {
            lock (gate) serverJumping = false;
        }
    }


    private async Task<object> FollowMarchAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (!payload.TryGetProperty("serverId", out JsonElement serverValue) ||
            !serverValue.TryGetInt32(out int requestedServerId) ||
            requestedServerId <= 0 ||
            !payload.TryGetProperty("marchUuid", out JsonElement marchValue) ||
            !TryReadPositiveInt64(marchValue, out long marchUuid))
        {
            throw new BridgeCommandException(
                "INVALID_MARCH",
                "server ID and march UUID are required");
        }

        lock (gate)
        {
            if (closed) throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing.");
            MapScanStartOwnership.RejectAlreadyRunning(isReading);
            if (serverJumping || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            if (coordinateJumping)
                throw new BridgeCommandException("MAP_NAVIGATION_RUNNING", "A map coordinate jump is already in progress.");
            if (followMarch is null)
                throw new BridgeCommandException(
                    "COMMAND_NOT_IMPLEMENTED",
                    "March Follow requires the live current-client source.");
            coordinateJumping = true;
        }

        try
        {
            CurrentClientMarchFollowResult result = await followMarch(
                requestedServerId, marchUuid, cancellationToken).ConfigureAwait(false);
            return new
            {
                serverId = result.ServerId,
                marchUuid = result.MarchUuid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
        }
        finally
        {
            lock (gate) coordinateJumping = false;
        }
    }

    private static bool TryReadPositiveInt64(JsonElement value, out long parsed)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out parsed))
            return parsed > 0;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out parsed))
        {
            return parsed > 0;
        }
        parsed = 0;
        return false;
    }

    private async Task<object> JumpToCoordinateAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        int requestedServerId = RequirePayloadInt(payload, "serverId", positive: true);
        int x = RequirePayloadInt(payload, "x", positive: false);
        int y = RequirePayloadInt(payload, "y", positive: false);
        lock (gate)
        {
            if (closed) throw new BridgeCommandException("MAP_SCAN_CLOSED", "The Map Data window is closing.");
            MapScanStartOwnership.RejectAlreadyRunning(isReading);
            if (serverJumping || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            if (coordinateJumping) throw new BridgeCommandException("MAP_NAVIGATION_RUNNING", "A map coordinate jump is already in progress.");
            if (currentClientSource is null) throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", "Map coordinate jump requires the live current-client source.");
            coordinateJumping = true;
        }
        try
        {
            CurrentClientCoordinateJumpResult result = await currentClientSource.JumpToCoordinateAsync(
                requestedServerId, x, y, cancellationToken).ConfigureAwait(false);
            return new { serverId = result.ServerId, x = result.X, y = result.Y };
        }
        finally
        {
            lock (gate) coordinateJumping = false;
        }
    }

    private static int RequirePayloadInt(JsonElement payload, string name, bool positive)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed) ||
            (positive ? parsed <= 0 : parsed < 0))
            throw new BridgeCommandException("INVALID_PAYLOAD", $"{name} is invalid.");
        return parsed;
    }

    private void ReconcileInterruptedScansAtStartup()
    {
        using MapScanProcessLease? lease = MapScanProcessLease.TryAcquire(store);
        if (lease is null) return;
        store.ReconcileInterruptedEngineScans(RecoveredWallClock.UnixTimeMilliseconds());
    }

    private async Task<object> StartAsync(
        MapScanStartOptions options,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource scanCancellation;
        string runId;
        lock (gate)
        {
            if (closed)
                throw new BridgeCommandException(
                    "MAP_SCAN_CLOSED",
                    "The Map Data window is closing and cannot start another scan.");
            MapScanStartOwnership.RejectAlreadyRunning(isReading);
            if (serverJumping || dispatchQuickFinding)
                throw new BridgeCommandException(
                    "GAME_OPERATION_IN_PROGRESS",
                    "another game operation is already in progress");
            if (coordinateJumping)
                throw new BridgeCommandException("MAP_NAVIGATION_RUNNING", "A map coordinate jump is already in progress.");

            MapScanProcessLease? scanLease = MapScanProcessLease.TryAcquire(store);
            if (scanLease is null)
                throw new BridgeCommandException(
                    MapScanStartOwnership.ActiveScanErrorCode,
                    MapScanStartOwnership.ActiveScanErrorMessage);
            try
            {
                // IMPLEMENTATION POLICY R7-136: public resume remains unsupported. Once
                // this process owns the exclusive per-profile scan lease, any persisted
                // running row is necessarily orphaned by a prior owner and is failed
                // durably before a fresh run can start. Staging/checkpoints are retained
                // as interruption evidence and can never publish from the failed run.
                store.ReconcileInterruptedEngineScans(RecoveredWallClock.UnixTimeMilliseconds());
                isReading = true;
                phase = "starting";
                scanMode = "auto";
                scanStrategy = "pending";
                selectedTypes = options.SelectedTypes.ToArray();
                concurrency = 0;
                lastError = null;
                serverId = 0;
                liveServerId = 0;
                worldId = 0;
                totalBlocks = completedBlocks = failedBlocks = inflightBlocks = 0;
                unreadBlocks = 0;
                scanRate = 0;
                acquisitionProgressPercent = null;
                runId = Guid.NewGuid().ToString("N");
                scanRunId = runId;
                scanCancellation = new CancellationTokenSource();
                activeCancellation = scanCancellation;
                activeScanLease = scanLease;
                scanLease = null;
                activeTerminal = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            finally
            {
                scanLease?.Dispose();
            }
        }
        PublishStatusChanged();

        try
        {
            CurrentClientMapContext context;
            using (CancellationTokenSource linked =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken, scanCancellation.Token))
            {
                context = await getContext(linked.Token).ConfigureAwait(false);
            }
            MapScanStartOwnership.RequireLiveServer(context.ServerId, "live");
            MapScanBlockGrid grid = MapScanGeometry.FromTileDimensions(
                context.TileWidth,
                context.TileHeight);
            if (grid.TotalBlocks > int.MaxValue)
                throw new BridgeCommandException(
                    "MAP_SIZE_UNAVAILABLE",
                    "world map dimensions are unavailable");

            if (options.TargetServerId.HasValue &&
                !MapScanStrategyPlanner.IsDirectTrainListSelection(selectedTypes))
            {
                throw new BridgeCommandException(
                    "LIVE_REMOTE_SCAN_UNSUPPORTED",
                    "targetServerId is supported only for Truck/Railway direct-list scans");
            }
            int targetServerId = options.TargetServerId ?? context.ServerId;
            MapScanStrategyPlan strategy = MapScanStrategyPlanner.Plan(context, selectedTypes);
            var request = new MapScanExecutionRequest(
                runId,
                targetServerId,
                context.WorldId,
                context.TileWidth,
                context.TileHeight,
                selectedTypes,
                strategy.Concurrency,
                MaxAttemptsPerBlock: 2,
                PlayerTileX: context.PlayerTileX,
                PlayerTileY: context.PlayerTileY,
                ScanMode: strategy.ScanMode,
                LaunchSessionId: context.LaunchSessionId,
                LiveServerId: context.ServerId);

            lock (gate)
            {
                if (closed || !ReferenceEquals(activeCancellation, scanCancellation) ||
                    scanCancellation.IsCancellationRequested)
                    throw new OperationCanceledException(scanCancellation.Token);
                serverId = targetServerId;
                liveServerId = context.ServerId;
                worldId = context.WorldId;
                scanMode = strategy.ScanMode;
                scanStrategy = strategy.StrategyId;
                concurrency = strategy.Concurrency;
                totalBlocks = checked((int)grid.TotalBlocks);
                unreadBlocks = totalBlocks;
                phase = "scanning";
            }
            PublishStatusChanged();

            var engine = new MapScanEngine(
                blockSource,
                new MapDataStoreScanSink(store),
                UpdateProgress);
            Task task = RunEngineAsync(engine, request, scanCancellation);
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                    activeTask = task;
            }
            return CreateStatus();
        }
        catch (Exception error)
        {
            CleanupFailedStart(scanCancellation, error);
            throw;
        }
    }

    private async Task RunEngineAsync(
        MapScanEngine engine,
        MapScanExecutionRequest request,
        CancellationTokenSource scanCancellation)
    {
        try
        {
            await engine.ExecuteAsync(request, scanCancellation.Token).ConfigureAwait(false);
            bool changed = false;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    isReading = false;
                    phase = "completed";
                    inflightBlocks = 0;
                    unreadBlocks = 0;
                    lastError = null;
                    acquisitionProgressPercent = null;
                    changed = true;
                }
            }
            if (changed) PublishStatusChanged();
        }
        catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
        {
            bool changed = false;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    isReading = false;
                    phase = "idle";
                    inflightBlocks = 0;
                    lastError = null;
                    acquisitionProgressPercent = null;
                    changed = true;
                }
            }
            if (changed) PublishStatusChanged();
        }
        catch (Exception error)
        {
            bool changed = false;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    isReading = false;
                    phase = "error";
                    inflightBlocks = 0;
                    lastError = error.Message;
                    acquisitionProgressPercent = null;
                    changed = true;
                }
            }
            if (changed) PublishStatusChanged();
        }
        finally
        {
            TaskCompletionSource<object?>? terminal = null;
            MapScanProcessLease? scanLease = null;
            lock (gate)
            {
                if (ReferenceEquals(activeCancellation, scanCancellation))
                {
                    activeCancellation = null;
                    activeTask = null;
                    scanLease = activeScanLease;
                    activeScanLease = null;
                    terminal = activeTerminal;
                    activeTerminal = null;
                }
            }
            scanLease?.Dispose();
            terminal?.TrySetResult(null);
            scanCancellation.Dispose();
        }
    }

    private void UpdateProgress(MapScanEngineProgress progress)
    {
        lock (gate)
        {
            if (!isReading) return;
            phase = progress.Phase;
            totalBlocks = progress.TotalBlocks;
            completedBlocks = progress.CompletedBlocks;
            failedBlocks = progress.FailedBlocks;
            inflightBlocks = progress.InflightBlocks;
            unreadBlocks = progress.UnreadBlocks;
            scanRate = progress.ScanRate;
            if (progress.AcquisitionProgressPercent.HasValue)
                acquisitionProgressPercent = Math.Clamp(progress.AcquisitionProgressPercent.Value, 0d, 100d);
            else if (!string.Equals(progress.Phase, "scanning", StringComparison.Ordinal))
                acquisitionProgressPercent = null;
        }
        PublishStatusChanged();
    }

    private void CleanupFailedStart(
        CancellationTokenSource scanCancellation,
        Exception error)
    {
        TaskCompletionSource<object?>? terminal = null;
        MapScanProcessLease? scanLease = null;
        bool changed = false;
        lock (gate)
        {
            if (!ReferenceEquals(activeCancellation, scanCancellation)) return;
            isReading = false;
            bool cancelled = scanCancellation.IsCancellationRequested || error is OperationCanceledException;
            phase = cancelled ? "idle" : "error";
            inflightBlocks = 0;
            activeCancellation = null;
            activeTask = null;
            scanLease = activeScanLease;
            activeScanLease = null;
            terminal = activeTerminal;
            activeTerminal = null;
            lastError = cancelled ? null : error.Message;
            acquisitionProgressPercent = null;
            changed = true;
        }
        scanLease?.Dispose();
        terminal?.TrySetResult(null);
        if (changed) PublishStatusChanged();
        scanCancellation.Dispose();
    }

    private async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cancellation;
        Task terminalTask;
        bool changed = false;
        lock (gate)
        {
            cancellation = activeCancellation;
            terminalTask = activeTerminal?.Task ?? Task.CompletedTask;
            if (isReading && phase is not "completed" and not "cancelling")
            {
                phase = "cancelling";
                changed = true;
            }
        }
        if (changed) PublishStatusChanged();
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
        if (!terminalTask.IsCompleted)
            await terminalTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void PublishStatusChanged()
    {
        Action<object>? handler = StatusChanged;
        if (handler is null) return;
        handler(CreateStatus());
    }

    public object CreateStatus()
    {
        bool shouldResolveLiveServer;
        lock (gate)
            shouldResolveLiveServer = !closed && !isReading && !dispatchQuickFinding;
        if (shouldResolveLiveServer && getLiveServerId is not null)
        {
            int? resolvedServerId = getLiveServerId();
            if (resolvedServerId is > 0)
            {
                lock (gate)
                {
                    if (!closed && !isReading)
                    {
                        liveServerId = resolvedServerId.Value;
                        if (serverId <= 0) serverId = resolvedServerId.Value;
                    }
                }
            }
        }

        lock (gate)
        {
            MapScanDerivedProgress derived = MapScanProgress.Derive(
                totalBlocks,
                completedBlocks,
                failedBlocks,
                phase);
            double visibleProgress = derived.ProgressPercent;
            if (isReading && string.Equals(phase, "scanning", StringComparison.Ordinal) && acquisitionProgressPercent.HasValue)
                visibleProgress = Math.Max(visibleProgress, Math.Min(98d, acquisitionProgressPercent.Value));
            return new
            {
                serverId,
                liveServerId = liveServerId > 0 ? liveServerId : serverId,
                serverIdSource = serverId <= 0 ? "none" :
                    liveServerId > 0 && serverId != liveServerId ? "remote_train_list" : "live",
                scanRunId,
                isReading,
                dispatchQuickFinding,
                phase,
                selectedTypes,
                totalBlocks,
                readBlocks = completedBlocks,
                unreadBlocks,
                failedBlocks,
                inflightBlocks,
                scanMode,
                scanStrategy,
                concurrency,
                retryCount = (int?)null,
                scanRate,
                progressPercent = visibleProgress,
                acquisitionProgressPercent,
                nativeCaptureReady = (bool?)null,
                nativePendingRecords = (int?)null,
                nativeDroppedRecords = (int?)null,
                resumeAvailable = false,
                homeServerId = (int?)null,
                seasonServerIds = (int[]?)null,
                truckMatchServerIds,
                lastError,
                worldId,
            };
        }
    }

    public void Close()
    {
        Task? terminalTask;
        CancellationTokenSource? cancellation;
        lock (gate)
        {
            closed = true;
            cancellation = activeCancellation;
            terminalTask = activeTerminal?.Task ?? activeTask;
            if (isReading && phase is not "completed") phase = "cancelling";
        }
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
        if (terminalTask is null) return;
        try { terminalTask.GetAwaiter().GetResult(); }
        catch { }
    }
}
