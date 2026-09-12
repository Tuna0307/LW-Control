using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class LWBridgeBackend
{
    private static readonly string[] MapKinds = MapScanContract.AllTypes;
    private static readonly HashSet<string> GlobalCommands = new(StringComparer.Ordinal)
    {
        "profile_list",
        "profile_instances_reconcile",
        "profile_instances_update_and_restart",
        "game_root_status",
        "game_root_select",
        "update_status",
        "update_check",
        "update_download_and_open",
        "append_log",
        "set_window_theme",
        "lastwar_localize",
        "local_config_get",
        "local_config_set",
    };

    private readonly LocalConfigStore config;
    private readonly GameInstallationService installation;
    private readonly INativeAsyncCommandService? asyncCommands;
    private readonly OverviewLifecycleService? overviewLifecycle;
    private readonly MapDataStore? mapData;
    private readonly int? firstLiveResultServerId;

    public LWBridgeBackend(
        LocalConfigStore? config = null,
        INativeAsyncCommandService? asyncCommands = null,
        MapDataStore? mapData = null,
        int? firstLiveResultServerId = null,
        OverviewLifecycleService? overviewLifecycle = null)
    {
        this.config = config ?? new LocalConfigStore();
        this.asyncCommands = asyncCommands;
        this.overviewLifecycle = overviewLifecycle;
        this.mapData = mapData;
        this.firstLiveResultServerId = firstLiveResultServerId;
        installation = new(this.config);
    }

    public string ProfileId => config.Snapshot.ProfileId;

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

    public GameRootStatus SaveGameRoot(string path)
    {
        try
        {
            return installation.SaveSelectedRoot(path);
        }
        catch (LocalConfigStoreException ex)
        {
            throw new BridgeCommandException(ex.Code, ex.Message);
        }
    }

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                RequireOptionalProfile(payload);
                return CreateProxyStatus();
            case "game_root_status":
                return installation.GetStatus();
            case "game_recovery_status":
                RequireOptionalProfile(payload);
                return overviewLifecycle?.CurrentRecoveryStatus ?? new OverviewRecoveryStatus(
                    "idle", null, false, false, null, null, 0, null, null, null, false);
            case "server_jump_history_import":
            case "server_jump_history_set":
                return SaveServerJumpHistory(payload);
            case "update_status":
                return new
                {
                    phase = "idle",
                    currentVersion = "0.3.1-rebuild",
                    latestVersion = (string?)null,
                    releaseNotes = "",
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
                    int serverId = MapDataQueryContract.RequiredServerId(payload);
                    MapDataStore store = RequireMapDataStore();
                    store.ClearServer(serverId);
                    return CreateMapScanStatus(serverId, "idle", null);
                }
            case "map_data_options":
                MapDataQueryContract.RequiredServerId(payload);
                throw new BridgeCommandException(
                    "MAP_INDEX_UNAVAILABLE",
                    "Map data options are unavailable before the production map index is initialized.");
            case "map_search":
                {
                    MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(payload);
                    MapSearchResult result = RequireMapDataStore().SearchIndexed(query);
                    return new { rows = result.Rows, total = result.Total };
                }
            case "map_city_export":
                {
                    if (!payload.TryGetProperty("query", out JsonElement exportQuery) || exportQuery.ValueKind != JsonValueKind.Object)
                        throw new BridgeCommandException("INVALID_MAP_QUERY", "map_city_export query must be an object.");
                    using JsonDocument envelope = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        profileId = ProfileId,
                        kind = "city",
                        query = JsonSerializer.Deserialize<object>(exportQuery.GetRawText(), JsonOptions.Default),
                    }, JsonOptions.Default));
                    MapDataQueryOptions query = MapDataQueryContract.NormalizeSearch(envelope.RootElement);
                    throw new BridgeCommandException(
                        "MAP_INDEX_UNAVAILABLE",
                        "Map export is unavailable before the production map index is initialized.",
                        query);
                }
            case "map_player_mark_set":
                return SetPlayerMark(payload);
            case "map_summary":
                RequireOptionalProfile(payload);
                if (asyncCommands is LiveResourceProbeCommandService liveResource &&
                    liveResource.CurrentServerId is int liveServerId)
                {
                    // IMPLEMENTATION POLICY: the bounded first-live adapter uses
                    // the recovered summary envelope over the same persisted
                    // MapDataStore that received the correlated current-game row.
                    MapDataStore store = RequireMapDataStore();
                    MapOptionSourceSelection source = MapDataStore.SelectOptionSource(
                        liveServerId,
                        isReading: false,
                        scanStateServerId: liveServerId,
                        scanRunId: null);
                    MapOptionAggregates aggregates = store.ReadOptionAggregatesAt(
                        source,
                        RecoveredWallClock.UnixTimeMilliseconds());
                    return new
                    {
                        serverId = liveServerId,
                        counts = aggregates.Counts,
                        scanState = liveResource.CreateStatus(),
                    };
                }
                if (firstLiveResultServerId is int firstLiveServerId)
                {
                    // IMPLEMENTATION POLICY: the bounded first-live mode exposes the
                    // recovered R6-025 {serverId,counts,scanState} envelope only for
                    // the source-backed imported server. Counts come from the same
                    // persisted/public map_records scope as map_search. Normal public
                    // summary remains fail-closed below.
                    MapDataStore store = RequireMapDataStore();
                    MapOptionSourceSelection source = MapDataStore.SelectOptionSource(
                        firstLiveServerId,
                        isReading: false,
                        scanStateServerId: firstLiveServerId,
                        scanRunId: null);
                    MapOptionAggregates aggregates = store.ReadOptionAggregatesAt(
                        source,
                        RecoveredWallClock.UnixTimeMilliseconds());
                    return new
                    {
                        serverId = firstLiveServerId,
                        counts = aggregates.Counts,
                        scanState = CreateCurrentMapScanStatus(),
                    };
                }
                MapDataStore savedStore = RequireMapDataStore();
                IReadOnlyList<int> savedServerIds = savedStore.ReadPublishedServerIds();
                if (savedServerIds.Count == 1)
                {
                    int savedServerId = savedServerIds[0];
                    // IMPLEMENTATION POLICY PM13-01: after process restart the bounded
                    // live adapter has no current-server observation. A profile-local
                    // index containing exactly one server is safe to expose as saved
                    // browsing context only. It is never promoted to live readiness.
                    MapOptionSourceSelection source = MapDataStore.SelectOptionSource(
                        savedServerId,
                        isReading: false,
                        scanStateServerId: savedServerId,
                        scanRunId: null);
                    MapOptionAggregates aggregates = savedStore.ReadOptionAggregatesAt(
                        source,
                        RecoveredWallClock.UnixTimeMilliseconds());
                    return new
                    {
                        serverId = savedServerId,
                        counts = aggregates.Counts,
                        scanState = CreateMapScanStatus(
                            savedServerId,
                            phase: "unavailable",
                            lastError: null,
                            serverIdSource: "saved_profile_index"),
                    };
                }
                if (savedServerIds.Count > 1)
                {
                    throw new BridgeCommandException(
                        "MAP_SAVED_CONTEXT_AMBIGUOUS",
                        "Saved map data spans multiple servers; choose or establish a server context before browsing it.",
                        new { serverIds = savedServerIds });
                }
                throw new BridgeCommandException(
                    "MAP_SAVED_CONTEXT_UNAVAILABLE",
                    "Map summary is unavailable because this profile has no saved map server and no current live server context.");
            case "append_log":
            case "set_window_theme":
                return null;
            case "lastwar_localize":
                return new Dictionary<string, string>();
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

    private IReadOnlyList<int> SaveServerJumpHistory(JsonElement payload)
    {
        if (!payload.TryGetProperty("history", out JsonElement history) || history.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("INVALID_PAYLOAD", "history must be an array of server IDs.");

        var seen = new HashSet<int>();
        var normalized = new List<int>(5);
        foreach (JsonElement item in history.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out int serverId))
                continue;
            if (serverId < 1 || serverId > 99999 || !seen.Add(serverId))
                continue;
            normalized.Add(serverId);
            if (normalized.Count == 5) break;
        }

        LWBridgeLocalConfig saved = UpdateConfig(c => c with { ServerJumpHistory = normalized });
        return saved.ServerJumpHistory;
    }

    private object CreateStatus()
    {
        return new
        {
            // OVL-02: only the current owned Overview session's fresh, exact
            // game-side heartbeat is authoritative bridge readiness.
            xluaOnline = overviewLifecycle?.IsReady ?? false,
            pending = (int?)null,
            config = new
            {
                auto_weekend_shield = false,
                auto_attack_shield = false,
                auto_force_update_reload = config.Snapshot.AutoReconnect,
                auto_close_popup = false,
                tasks = new Dictionary<string, object>(),
            },
        };
    }

    private object CreateProxyStatus()
    {
        GameProcessStatus process = installation.GetProcessStatus();
        return new
        {
            gameRunning = process.GameRunning,
            launcherRunning = process.LauncherRunning,
            repairRequired = overviewLifecycle?.RepairRequired ?? false,
            bridgeOnline = overviewLifecycle?.IsReady ?? false,
            gamePid = process.GamePid,
            launcherPid = process.LauncherPid,
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
        concurrency = 0,
        retryCount = 0,
        scanRate = 0,
        progressPercent = 0,
        nativeCaptureReady = false,
        nativePendingRecords = 0,
        nativeDroppedRecords = 0,
        homeServerId = 0,
        seasonServerIds = Array.Empty<int>(),
        truckMatchServerIds = Array.Empty<int>(),
        lastError,
    };

    private MapDataStore RequireMapDataStore() => mapData ?? throw new BridgeCommandException(
        "MAP_INDEX_UNAVAILABLE",
        "Map data is unavailable before the profile map index is initialized.");

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
        if (payload.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_PAYLOAD", "Command payload must be a JSON object.");
        if (!GlobalCommands.Contains(command))
            RequireProfile(payload);
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
