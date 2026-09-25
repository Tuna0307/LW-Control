using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record CityExportRequest(
    MapDataQueryOptions Query,
    CityExportWorkbookOptions WorkbookOptions,
    string DefaultFileName);

internal sealed class LWBridgeBackend
{
    private static readonly string[] MapKinds = MapScanContract.AllTypes;
    private static readonly HashSet<string> GlobalCommands = new(StringComparer.Ordinal)
    {
        "profile_list",
        "profile_select",
        "profile_note_set",
        "profile_reorder",
        "profile_primary_set",
        "profile_instances_reconcile",
        "profile_instances_update_and_restart",
        "profile_settings_save",
        "red_packet_delay_configure",
        "treasure_delay_configure",
        "game_root_status",
        "game_root_select",
        "update_status",
        "update_check",
        "update_download_and_open",
        "append_log",
        "set_window_theme",
        "lastwar_localize",
        "map_coordinate_jump",
        "local_config_get",
        "local_config_set",
    };

    private readonly LocalConfigStore config;
    private readonly GameInstallationService installation;
    private readonly INativeAsyncCommandService? asyncCommands;
    private readonly OverviewLifecycleService? overviewLifecycle;
    private readonly LWBridgeControlPipeHostState? bridgeHostState;
    private readonly MapDataStore? mapData;
    private readonly ServerJumpHistoryCommandService serverJumpHistory;
    private readonly AppendLogCommandService appendLog;
    private readonly ProxyStatusCommandService proxyStatus;
    private readonly GameRecoveryStatusCommandService gameRecoveryStatus;
    private readonly LastWarLocaleService lastWarLocales;
    private readonly int? firstLiveResultServerId;
    private readonly Func<object>? mapScanStatusProvider;
    private readonly Func<object?>? runtimeTasksProvider;

    public LWBridgeBackend(
        LocalConfigStore? config = null,
        INativeAsyncCommandService? asyncCommands = null,
        MapDataStore? mapData = null,
        int? firstLiveResultServerId = null,
        OverviewLifecycleService? overviewLifecycle = null,
        LastWarLocaleService? lastWarLocales = null,
        Func<object>? mapScanStatusProvider = null,
        LWBridgeControlPipeHostState? bridgeHostState = null,
        Func<object?>? runtimeTasksProvider = null,
        string? profileRuntimeDirectory = null,
        GameInstallationTestHooks? installationTestHooks = null,
        ProxyStatusTestHooks? proxyStatusTestHooks = null)
    {
        this.config = config ?? new LocalConfigStore();
        this.asyncCommands = asyncCommands;
        this.overviewLifecycle = overviewLifecycle;
        this.bridgeHostState = bridgeHostState;
        this.mapData = mapData;
        serverJumpHistory = new ServerJumpHistoryCommandService(
            this.config.Snapshot.ProfileId,
            mapData);
        appendLog = new AppendLogCommandService(
            this.config.Snapshot.ProfileId,
            profileRuntimeDirectory);
        this.lastWarLocales = lastWarLocales ?? new LastWarLocaleService();
        this.firstLiveResultServerId = firstLiveResultServerId;
        this.mapScanStatusProvider = mapScanStatusProvider;
        this.runtimeTasksProvider = runtimeTasksProvider;
        installation = new(this.config, installationTestHooks);
        proxyStatus = new ProxyStatusCommandService(
            this.config.Snapshot.ProfileId,
            profileRuntimeDirectory,
            installation,
            runtimeManagedProvider: () => overviewLifecycle?.RuntimeManaged ?? false,
            testHooks: proxyStatusTestHooks);
        gameRecoveryStatus = new GameRecoveryStatusCommandService(
            this.config.Snapshot.ProfileId,
            profileRuntimeDirectory,
            () => overviewLifecycle?.CurrentRecoveryStatus);
    }

    public string ProfileId => config.Snapshot.ProfileId;

    internal LWBridgeControlPipeHostState? BridgeHostState => bridgeHostState;

    public object GetBootstrap(bool fixture, string sessionId, bool suppressAutoLaunch = false)
    {
        var profile = CreateProfile();
        return new
        {
            mode = fixture ? "fixture" : "live",
            sessionId,
            autoLaunchGame = fixture || suppressAutoLaunch ? false : config.Snapshot.AutoLaunchGame,
            profiles = new
            {
                selectedProfileId = profile.id,
                profiles = new[] { profile },
            },
        };
    }

    public GameRootStatus GetGameRootStatus() => installation.GetStatus();

    public NativeGameRootSelectionResult CreateGameRootSelectionCanceled() =>
        installation.CreateNativeCanceledSelection();

    public NativeGameRootSelectionResult SaveNativeGameRootSelection(string path)
    {
        try
        {
            return installation.SaveNativeSelection(path);
        }
        catch (LocalConfigStoreException ex)
        {
            throw new BridgeCommandException(ex.Code, ex.Message);
        }
    }

    public GameRootStatus SaveGameRoot(string path)
    {
        try
        {
            GameRootStatus validated = installation.Validate(path, "selected");
            if (!validated.Valid) return validated;
            if (overviewLifecycle is null) return installation.SaveSelectedRoot(validated.Path);

            GameRootStatus? saved = null;
            overviewLifecycle.RebindGameRoot(validated.Path, () =>
            {
                saved = installation.SaveSelectedRoot(validated.Path);
                if (!saved.Valid)
                    throw new BridgeCommandException(saved.Error ?? "GAME_ROOT_INVALID",
                        "The selected Last War installation became invalid before it could be saved.");
            });
            return saved ?? throw new InvalidOperationException("Game Root selection was not persisted.");
        }
        catch (LocalConfigStoreException ex)
        {
            throw new BridgeCommandException(ex.Code, ex.Message);
        }
    }

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(command, "call_lua", StringComparison.Ordinal))
        {
            ValidateCommandScope(command, payload);
            return await CallLuaAsync(payload, cancellationToken)
                .ConfigureAwait(false);
        }

        if (asyncCommands?.CanHandle(command) == true)
        {
            ValidateCommandScope(command, payload);
            return await asyncCommands.InvokeAsync(command, payload, cancellationToken).ConfigureAwait(false);
        }
        return Invoke(command, payload);
    }

    private object? Invoke(string command, JsonElement payload)
    {
        ValidateCommandScope(command, payload);

        switch (command)
        {
            case "profile_list":
                {
                    var profile = CreateProfile();
                    return new
                    {
                        selectedProfileId = profile.id,
                        profiles = new[] { profile },
                    };
                }
            case "profile_instance_status":
                RequireProfile(payload);
                return CreateInstanceStatus();
            case "profile_instance_start":
                RequireProfile(payload);
                throw new BridgeCommandException(
                    "OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED",
                    "The recovered staged launcher descriptor/proof contract is not complete yet; launch is blocked rather than starting an unmanaged game.");
            case "profile_instance_stop":
                RequireProfile(payload);
                throw new BridgeCommandException(
                    "INSTANCE_NOT_OWNED",
                    "No LWBridge-owned game instance is active.");
            case "profile_instances_reconcile":
                return CreateInstanceStatus();
            case "get_status":
                RequireOptionalProfile(payload);
                return CreateStatus();
            case "proxy_status":
                return proxyStatus.Invoke(payload);
            case "game_root_status":
                return installation.GetNativeStatus();
            case "game_recovery_status":
                return gameRecoveryStatus.Invoke(payload);
            case "server_jump_history_get":
            case "server_jump_history_import":
            case "server_jump_history_set":
                return serverJumpHistory.Invoke(command, payload);
            case "update_status":
                return new
                {
                    phase = "idle",
                    currentVersion = "0.3.1",
                    latestVersion = (string?)null,
                    releaseNotes = "",
                    publishedAt = (string?)null,
                    progress = (double?)null,
                    message = (string?)null,
                    nextManualCheckAt = (string?)null,
                    downloadDirectory = "",
                };
            case "set_automation":
                RequireOptionalProfile(payload);
                return SetAutomation(payload);
            case "automation_status":
                RequireOptionalProfile(payload);
                return new { tasks = new Dictionary<string, object>() };
            case "resource_automation_status":
                RequireOptionalProfile(payload);
                return new
                {
                    tasks = new Dictionary<string, object>
                    {
                        ["buildingResources"] = new { enabled = false, intervalMinutes = 60 },
                        ["armedTruckReward"] = new { enabled = false, intervalMinutes = 60 },
                    },
                };
            case "map_scan_status":
                return CreateCurrentMapScanStatus();
            case "map_scan_start":
                {
                    MapScanStartOptions options = MapScanContract.NormalizeStart(payload);
                    throw new BridgeCommandException(
                        "BRIDGE_NOT_READY",
                        "Map scan requires a verified bridge-ready game session.",
                        options);
                }
            case "map_scan_stop":
                return CreateCurrentMapScanStatus();
            case "map_scan_clear":
                {
                    int serverId = MapDataQueryContract.RequiredServerIdOrAll(payload);
                    (bool isReading, int currentServerId, string? serverIdSource) = ReadMapScanOwnership();
                    MapScanClearOwnership.Validate(
                        serverId,
                        isReading,
                        currentServerId,
                        serverIdSource);
                    MapDataStore store = RequireMapDataStore();
                    store.ClearServer(serverId);
                    return CreateMapScanStatus(
                        currentServerId,
                        "idle",
                        null,
                        serverIdSource ?? "none");
                }
            case "map_data_options":
                {
                    int serverId = MapDataQueryContract.RequiredServerIdOrAll(payload);
                    MapDataStore store = RequireMapDataStore();
                    MapOptionSourceSelection source = serverId == 0
                        ? new MapOptionSourceSelection(0, null)
                        : SelectMapOptionSource(serverId);
                    MapOptionAggregates aggregates = store.ReadOptionAggregatesAt(
                        source, RecoveredWallClock.UnixTimeMilliseconds());
                    return CreateMapDataOptions(serverId, aggregates);
                }
            case "map_search":
                {
                    MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(payload);
                    MapSearchResult result = RequireMapDataStore().SearchIndexed(query);
                    return new { rows = result.Rows, total = result.Total };
                }
            case "map_city_export":
                throw new BridgeCommandException(
                    "NATIVE_DIALOG_REQUIRED",
                    "City export requires the desktop save dialog host.");
            case "map_player_mark_set":
                return SetPlayerMark(payload);
            case "map_summary":
                {
                    RequireOptionalProfile(payload);
                    if (mapScanStatusProvider is null)
                        throw new InvalidOperationException(
                            "Map summary requires the shared map scan state provider.");

                    object scanState = mapScanStatusProvider();
                    JsonElement scanJson =
                        JsonSerializer.SerializeToElement(scanState, JsonOptions.Default);
                    if (!scanJson.TryGetProperty("serverId", out JsonElement serverElement) ||
                        !serverElement.TryGetInt32(out int serverId))
                    {
                        throw new InvalidDataException(
                            "Map scan state did not contain an integer serverId.");
                    }

                    bool isReading =
                        scanJson.TryGetProperty("isReading", out JsonElement readingElement) &&
                        readingElement.ValueKind == JsonValueKind.True;
                    string? scanRunId =
                        scanJson.TryGetProperty("scanRunId", out JsonElement runElement) &&
                        runElement.ValueKind == JsonValueKind.String
                            ? runElement.GetString()
                            : null;

                    MapDataStore store = RequireMapDataStore();
                    MapOptionSourceSelection source = MapDataStore.SelectOptionSource(
                        serverId,
                        isReading,
                        serverId,
                        scanRunId);
                    MapOptionAggregates aggregates = store.ReadOptionAggregatesAt(
                        source,
                        RecoveredWallClock.UnixTimeMilliseconds());
                    var counts = MapScanContract.RecoveredDefaultTypes.ToDictionary(
                        kind => kind,
                        kind => aggregates.Counts.TryGetValue(kind, out int count) ? count : 0,
                        StringComparer.Ordinal);
                    return new
                    {
                        serverId,
                        counts,
                        scanState,
                    };
                }
            case "feedback_export":
                ValidateFeedbackExportPayload(payload);
                throw new BridgeCommandException(
                    "COMMAND_NOT_IMPLEMENTED",
                    "Feedback archive export remains fenced until the recovered redaction/cache/archive/save pipeline is implemented.");
            case "append_log":
                return appendLog.Invoke(payload);
            case "set_window_theme":
                return null;
            case "lastwar_localize":
                return lastWarLocales.Localize(payload);
            case "local_config_get":
                return new
                {
                    autoLaunchGame = config.Snapshot.AutoLaunchGame,
                    autoReconnect = config.Snapshot.AutoReconnect,
                };
            case "local_config_set":
                return SetLocalConfig(payload);
            default:
                throw new BridgeCommandException(
                    "COMMAND_NOT_IMPLEMENTED",
                    $"Command '{command}' is not implemented by the production backend yet.");
        }
    }

    private async Task<JsonElement?> CallLuaAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        string functionName = GetRequiredString(payload, "fnName");
        if (!string.Equals(
                functionName,
                "getStatus",
                StringComparison.Ordinal))
        {
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Lua function '{functionName}' is not enabled by the production backend.");
        }

        if (!payload.TryGetProperty("args", out JsonElement args) ||
            args.ValueKind != JsonValueKind.Object)
        {
            throw new BridgeCommandException(
                "INVALID_PAYLOAD",
                "call_lua getStatus requires args to be an object.");
        }

        using JsonElement.ObjectEnumerator properties =
            args.EnumerateObject();
        if (properties.MoveNext())
        {
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                "Only call_lua(getStatus,{}) is enabled by the production backend.");
        }

        if (bridgeHostState is null ||
            !bridgeHostState.IsRpcTransportStarted)
        {
            throw new BridgeCommandException(
                "LUA_CALL_FAILED",
                "lua call failed: shared bridge transport is unavailable");
        }

        long now = RecoveredWallClock.UnixTimeMilliseconds();
        try
        {
            return await bridgeHostState.CallLuaAsync(
                    LWBridgeControlPipeRegistry.DefaultRoute,
                    functionName,
                    args,
                    timestamp: now,
                    createdAt: now,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new BridgeCommandException(
                "LUA_CALL_FAILED",
                "lua call failed: " + error.Message);
        }
    }

    private object SetAutomation(JsonElement payload)
    {
        string name = GetRequiredString(payload, "name");
        bool enabled = GetRequiredBoolean(payload, "enabled");
        if (name != "autoForceUpdateReload")
            throw new BridgeCommandException("AUTOMATION_NOT_IMPLEMENTED", $"Automation '{name}' is not implemented yet.");
        UpdateConfig(c => c with { AutoReconnect = enabled });
        overviewLifecycle?.NotifyAutomationChanged(enabled);
        return new { enabled };
    }

    private object SetLocalConfig(JsonElement payload)
    {
        bool? autoLaunchGame = null;
        if (payload.TryGetProperty("autoLaunchGame", out JsonElement autoLaunch))
        {
            if (autoLaunch.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new BridgeCommandException("INVALID_PAYLOAD", "autoLaunchGame must be a boolean.");
            autoLaunchGame = autoLaunch.GetBoolean();
        }

        LWBridgeLocalConfig next = UpdateConfig(current => autoLaunchGame.HasValue
            ? current with { AutoLaunchGame = autoLaunchGame.Value }
            : current);
        return new { autoLaunchGame = next.AutoLaunchGame, autoReconnect = next.AutoReconnect };
    }

    private object CreateStatus()
    {
        return new
        {
            // OVL-02: only the current owned Overview session's fresh, exact
            // game-side heartbeat is authoritative bridge readiness.
            xluaOnline = overviewLifecycle?.IsReady ?? false,
            pending = bridgeHostState?.PendingCallCount,
            config = new
            {
                auto_weekend_shield = false,
                auto_attack_shield = false,
                auto_force_update_reload = config.Snapshot.AutoReconnect,
                auto_close_popup = false,
                tasks = runtimeTasksProvider?.Invoke() ?? new Dictionary<string, object>(),
            },
        };
    }

    private object CreateInstanceStatus()
    {
        if (overviewLifecycle is not null) return overviewLifecycle.CreateInstanceStatus();
        GameProcessStatus process = installation.GetProcessStatus();
        if (!process.GameRunning)
            return new { phase = "stopped", pid = (int?)null, instanceId = (string?)null, error = (string?)null };
        return new
        {
            phase = "error",
            pid = process.GamePid,
            instanceId = (string?)null,
            error = "UNMANAGED_GAME_RUNNING",
        };
    }

    private dynamic CreateProfile()
    {
        GameProcessStatus process = installation.GetProcessStatus();
        return new
        {
            id = ProfileId,
            displayName = "Local Game",
            roleName = "",
            serverId = 0,
            enabled = true,
            note = "",
            connectionState = overviewLifecycle?.CurrentConnectionState ?? "offline",
        };
    }

    private object CreateCurrentMapScanStatus() => firstLiveResultServerId is int serverId
        // IMPLEMENTATION POLICY: saved-capture replay has no active acquisition
        // session. Keep scan state unavailable and identify the source explicitly
        // so UI/status consumers cannot mistake the replay for a fresh idle scan.
        ? CreateMapScanStatus(serverId, "unavailable", null, "saved_capture_replay")
        : CreateMapScanStatus();

    private static object CreateMapScanStatus(
        int serverId = 0,
        string phase = "unavailable",
        string? lastError = "MAP_BACKEND_NOT_IMPLEMENTED",
        string serverIdSource = "none") => new
    {
        serverId,
        serverIdSource,
        scanRunId = "",
        isReading = false,
        phase,
        selectedTypes = MapKinds,
        totalBlocks = 0,
        readBlocks = 0,
        unreadBlocks = 0,
        failedBlocks = 0,
        inflightBlocks = 0,
        scanMode = "normal",
        scanStrategy = "none",
        concurrency = 8,
        retryCount = 2,
        scanRate = 0,
        progressPercent = 0,
        nativeCaptureReady = false,
        nativePendingRecords = 0,
        nativeDroppedRecords = 0,
        resumeAvailable = false,
        homeServerId = 0,
        seasonServerIds = Array.Empty<int>(),
        truckMatchServerIds = Array.Empty<int>(),
        lastError,
    };

    private MapDataStore RequireMapDataStore() => mapData ?? throw new BridgeCommandException(
        "MAP_INDEX_UNAVAILABLE",
        "Map data is unavailable before the profile map index is initialized.");

    private (bool IsReading, int ServerId, string? ServerIdSource) ReadMapScanOwnership()
    {
        if (mapScanStatusProvider is null) return (false, 0, null);

        JsonElement status = JsonSerializer.SerializeToElement(mapScanStatusProvider(), JsonOptions.Default);
        bool isReading = status.TryGetProperty("isReading", out JsonElement reading) &&
                         reading.ValueKind == JsonValueKind.True;
        int serverId = status.TryGetProperty("serverId", out JsonElement server) &&
                       server.TryGetInt32(out int parsedServerId)
            ? parsedServerId
            : 0;
        string? serverIdSource = status.TryGetProperty("serverIdSource", out JsonElement source) &&
                                 source.ValueKind == JsonValueKind.String
            ? source.GetString()
            : null;
        return (isReading, serverId, serverIdSource);
    }

    private MapOptionSourceSelection SelectMapOptionSource(int serverId)
    {
        if (mapScanStatusProvider is null)
            return MapDataStore.SelectOptionSource(serverId, false, 0, null);

        JsonElement status = JsonSerializer.SerializeToElement(mapScanStatusProvider(), JsonOptions.Default);
        int scanServerId = status.TryGetProperty("serverId", out JsonElement server) && server.TryGetInt32(out int parsed)
            ? parsed
            : 0;
        bool isReading = status.TryGetProperty("isReading", out JsonElement reading) && reading.ValueKind == JsonValueKind.True;
        string? scanRunId = status.TryGetProperty("scanRunId", out JsonElement run) && run.ValueKind == JsonValueKind.String
            ? run.GetString()
            : null;
        return MapDataStore.SelectOptionSource(serverId, isReading, scanServerId, scanRunId);
    }

    private static object CreateMapDataOptions(int serverId, MapOptionAggregates aggregates)
    {
        object? scanProgress = aggregates.ScanProgress is { } progress
            ? new
            {
                // RECOVERED frontend consumer surface (LWB-R6-004): only these
                // persisted scanProgress fields are required here. Do not serialize the
                // adjacent count/type columns whose exact original public mapping remains
                // unrecovered.
                id = progress.Id,
                serverId = progress.ServerId,
                status = progress.Status,
                createdAt = progress.CreatedAt,
                updatedAt = progress.UpdatedAt,
                error = progress.Error,
            }
            : null;
        return new
        {
            serverId,
            counts = aggregates.Counts,
            alliances = aggregates.Alliances.Select(item => new { name = item.Name ?? string.Empty, count = item.Count }).ToArray(),
            names = new
            {
                resource = aggregates.Names.Where(item => item.Kind == "resource").Select(item => new { key = item.Key, count = item.Count }).ToArray(),
                monster = aggregates.Names.Where(item => item.Kind == "monster").Select(item => new { key = item.Key, count = item.Count }).ToArray(),
            },
            dispatchLevels = aggregates.DispatchLevels,
            noAllianceCount = aggregates.NoAllianceCount,
            rewardItems = new
            {
                truck = aggregates.RewardItems.Where(item => item.Kind == "truck").Select(item => new { key = item.Key, name = item.Name, iconPath = item.IconPath }).ToArray(),
                railway = aggregates.RewardItems.Where(item => item.Kind == "railway").Select(item => new { key = item.Key, name = item.Name, iconPath = item.IconPath }).ToArray(),
            },
            treasureTypes = aggregates.TreasureTypes.Select(item => new
            {
                key = item.Key,
                count = item.Count,
                treasureType = item.TreasureType,
                suppliesType = item.SuppliesType,
                treasureNameKey = item.TreasureNameKey,
            }).ToArray(),
            scanProgress,
        };
    }

    internal CityExportRequest PrepareCityExport(
        JsonElement payload,
        DateTimeOffset? utcNow = null)
    {
        ValidateCommandScope("map_city_export", payload);
        if (!payload.TryGetProperty("query", out JsonElement exportQuery) ||
            exportQuery.ValueKind != JsonValueKind.Object)
        {
            throw new BridgeCommandException(
                "INVALID_MAP_QUERY",
                "map_city_export query must be an object.");
        }

        if (!exportQuery.TryGetProperty("serverId", out JsonElement serverElement) ||
            !serverElement.TryGetInt32(out int serverId) ||
            serverId <= 0)
        {
            throw new BridgeCommandException(
                "MAP_EXPORT_FAILED",
                "city export server is unavailable");
        }

        using JsonDocument envelope = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            profileId = ProfileId,
            kind = "city",
            query = JsonSerializer.Deserialize<object>(exportQuery.GetRawText(), JsonOptions.Default),
        }, JsonOptions.Default));
        MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(envelope.RootElement);

        string[] headers = ReadCityExportHeaders(payload);
        string sheetName = ReadOptionalExportString(payload, "sheetName", "Cities");
        string yesLabel = ReadOptionalExportString(payload, "yesLabel", "Yes");
        string noLabel = ReadOptionalExportString(payload, "noLabel", "No");
        var workbookOptions = new CityExportWorkbookOptions(
            headers,
            sheetName,
            yesLabel,
            noLabel);

        string fileName = BuildCityExportDefaultFileName(
            serverId,
            utcNow ?? DateTimeOffset.UtcNow);
        return new CityExportRequest(query, workbookOptions, fileName);
    }

    internal object WriteCityExport(CityExportRequest request, string path)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(path))
            throw new BridgeCommandException("MAP_EXPORT_FAILED", "city export path is invalid");

        MapDataStore store = RequireMapDataStore();
        var rows = new List<JsonElement>();
        int total = 0;
        for (int page = 1; page <= 1000; page++)
        {
            MapSearchResult result = store.SearchCityPageForExport(
                request.Query with { Page = page, PageSize = 200 });
            total = result.Total;
            if (result.Rows.Count == 0)
                break;
            rows.AddRange(result.Rows);
            if (rows.Count >= total)
                break;
        }
        if (rows.Count < total)
            MapDataStore.RequireCityExportRowLimit(total);

        CityExportWorkbookWriteResult writeResult;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            writeResult = CityExportWorkbookWriter.Write(
                stream,
                rows,
                request.WorkbookOptions);
            stream.Flush(flushToDisk: true);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            throw new BridgeCommandException(
                "MAP_EXPORT_FAILED",
                "city export headers are invalid",
                ex.Message);
        }
        catch (Exception ex)
        {
            throw new BridgeCommandException(
                "MAP_EXPORT_FAILED",
                ex.Message);
        }

        return new
        {
            canceled = false,
            path,
            rowCount = writeResult.RowCount,
        };
    }

    internal static object CreateCityExportCanceledResult() => new
    {
        canceled = true,
        path = string.Empty,
        rowCount = 0,
    };

    internal static string BuildCityExportDefaultFileName(
        int serverId,
        DateTimeOffset utcNow)
    {
        DateTimeOffset utc = utcNow.ToUniversalTime();
        return "map-cities-" + serverId.ToString(CultureInfo.InvariantCulture) + "-" +
            utc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".xlsx";
    }

    private static string[] ReadCityExportHeaders(JsonElement payload)
    {
        if (!payload.TryGetProperty("headers", out JsonElement headersElement) ||
            headersElement.ValueKind != JsonValueKind.Array ||
            headersElement.GetArrayLength() != 12)
        {
            throw new BridgeCommandException(
                "MAP_EXPORT_FAILED",
                "city export headers are invalid");
        }

        string[] headers = new string[12];
        int index = 0;
        foreach (JsonElement header in headersElement.EnumerateArray())
        {
            if (header.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(header.GetString()))
            {
                throw new BridgeCommandException(
                    "MAP_EXPORT_FAILED",
                    "city export headers are invalid");
            }
            headers[index++] = header.GetString()!;
        }
        return headers;
    }

    private static string ReadOptionalExportString(
        JsonElement payload,
        string property,
        string fallback)
    {
        if (!payload.TryGetProperty(property, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String)
            return fallback;
        return element.GetString() ?? fallback;
    }

    private object SetPlayerMark(JsonElement payload)
    {
        if (!payload.TryGetProperty("row", out JsonElement row) || row.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "player mark row must be an object.");
        if (!payload.TryGetProperty("marked", out JsonElement markedElement) ||
            markedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new BridgeCommandException("INVALID_PLAYER_MARK", "marked must be a boolean.");

        int serverId = MapDataQueryContract.RequiredServerId(row);
        string ownerUid = GetRequiredString(row, "ownerUid");
        bool marked = markedElement.GetBoolean();
        MapDataStore store = RequireMapDataStore();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (marked)
        {
            store.UpsertPlayerMark(new MapPlayerMark(
                serverId,
                ownerUid,
                "active",
                now,
                null,
                row.GetRawText()));
        }
        else
        {
            store.DeletePlayerMark(serverId, ownerUid);
        }

        return new
        {
            serverId,
            ownerUid,
            marked,
            state = marked ? "active" : null,
            markedAt = marked ? now : (long?)null,
            checkedAt = (long?)null,
        };
    }

    private void ValidateCommandScope(string command, JsonElement payload)
    {
        if (string.Equals(command, "proxy_status", StringComparison.Ordinal) ||
            string.Equals(command, "game_recovery_status", StringComparison.Ordinal))
            return;
        if (payload.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_PAYLOAD", "Command payload must be a JSON object.");
        if (ServerJumpHistoryCommandService.IsCommand(command))
            return;
        if (!GlobalCommands.Contains(command))
            RequireProfile(payload);
    }

    private static void ValidateFeedbackExportPayload(JsonElement payload)
    {
        if (!payload.TryGetProperty("exportId", out JsonElement exportId) ||
            exportId.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(exportId.GetString()))
        {
            throw new BridgeCommandException(
                "FEEDBACK_EXPORT_FAILED",
                "export ID is required");
        }
    }

    private LWBridgeLocalConfig UpdateConfig(Func<LWBridgeLocalConfig, LWBridgeLocalConfig> update)
    {
        try
        {
            return config.Update(update);
        }
        catch (LocalConfigStoreException ex)
        {
            throw new BridgeCommandException(ex.Code, ex.Message);
        }
    }

    private void RequireProfile(JsonElement payload)
    {
        if (!payload.TryGetProperty("profileId", out JsonElement property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            (property.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.GetString())))
            throw new BridgeCommandException("PROFILE_REQUIRED", "The command requires an active local profile.");
        if (property.ValueKind != JsonValueKind.String)
            throw new BridgeCommandException("INVALID_PAYLOAD", "profileId must be a string.");
        string value = property.GetString()!;
        if (!string.Equals(value, ProfileId, StringComparison.Ordinal))
            throw new BridgeCommandException("PROFILE_SCOPE_MISMATCH", "The command targets a different local profile.");
    }

    private void RequireOptionalProfile(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("profileId", out JsonElement property))
            return;
        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;
        if (property.ValueKind != JsonValueKind.String)
            throw new BridgeCommandException("INVALID_PAYLOAD", "profileId must be a string.");
        string value = property.GetString() ?? string.Empty;
        if (!string.Equals(value, ProfileId, StringComparison.Ordinal))
            throw new BridgeCommandException("PROFILE_SCOPE_MISMATCH", "The command targets a different local profile.");
    }

    private static string GetRequiredString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
            throw new BridgeCommandException("INVALID_PAYLOAD", $"{name} is required.");
        return property.GetString()!;
    }

    private static bool GetRequiredBoolean(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out JsonElement property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new BridgeCommandException("INVALID_PAYLOAD", $"{name} must be a boolean.");
        return property.GetBoolean();
    }
}
