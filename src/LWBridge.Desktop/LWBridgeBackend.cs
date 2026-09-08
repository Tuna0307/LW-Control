using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class LWBridgeBackend
{
    private static readonly string[] MapKinds = MapScanContract.AllTypes;

    private readonly LocalConfigStore config = new();
    private readonly GameInstallationService installation;

    public LWBridgeBackend() => installation = new(config);

    public string ProfileId => config.Snapshot.ProfileId;

    public object GetBootstrap(bool fixture, string sessionId)
    {
        var profile = CreateProfile();
        return new
        {
            mode = fixture ? "fixture" : "live",
            sessionId,
            autoLaunchGame = fixture ? false : config.Snapshot.AutoLaunchGame,
            profiles = new
            {
                selectedProfileId = profile.id,
                profiles = new[] { profile },
            },
        };
    }

    public GameRootStatus GetGameRootStatus() => installation.GetStatus();

    public GameRootStatus SaveGameRoot(string path) => installation.SaveSelectedRoot(path);

    public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Invoke(command, payload));
    }

    private object? Invoke(string command, JsonElement payload)
    {
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
                return new { state = "idle", error = (string?)null };
            case "server_jump_history_import":
            case "server_jump_history_set":
                RequireOptionalProfile(payload);
                return payload.TryGetProperty("history", out JsonElement history)
                    ? JsonSerializer.Deserialize<object>(history.GetRawText(), JsonOptions.Default)
                    : Array.Empty<object>();
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
                return CreateMapScanStatus();
            case "map_scan_start":
                {
                    MapScanStartOptions options = MapScanContract.NormalizeStart(payload);
                    throw new BridgeCommandException(
                        "BRIDGE_NOT_READY",
                        "Map scan requires a verified bridge-ready game session.",
                        options);
                }
            case "map_scan_stop":
                return CreateMapScanStatus();
            case "map_scan_clear":
                throw new BridgeCommandException(
                    "MAP_INDEX_UNAVAILABLE",
                    "Map data cannot be cleared before the production map index is initialized.");
            case "map_summary":
                RequireOptionalProfile(payload);
                return new
                {
                    serverId = 0,
                    counts = EmptyMapCounts(),
                    scanState = CreateMapScanStatus(),
                    available = false,
                    unavailableReason = "MAP_BACKEND_NOT_IMPLEMENTED",
                };
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
        config.Update(c => c with { AutoReconnect = enabled });
        return new { enabled };
    }

    private object SetLocalConfig(JsonElement payload)
    {
        LWBridgeLocalConfig next = config.Snapshot;
        if (payload.TryGetProperty("autoLaunchGame", out JsonElement autoLaunch))
        {
            if (autoLaunch.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new BridgeCommandException("INVALID_PAYLOAD", "autoLaunchGame must be a boolean.");
            next = next with { AutoLaunchGame = autoLaunch.GetBoolean() };
        }
        config.Update(_ => next);
        return new { autoLaunchGame = next.AutoLaunchGame, autoReconnect = next.AutoReconnect };
    }

    private object CreateStatus()
    {
        return new
        {
            xluaOnline = false,
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
            repairRequired = false,
            bridgeOnline = false,
            gamePid = process.GamePid,
            launcherPid = process.LauncherPid,
        };
    }

    private object CreateInstanceStatus()
    {
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
            connectionState = process.GameRunning ? "offline" : "offline",
        };
    }

    private static object CreateMapScanStatus() => new
    {
        serverId = 0,
        serverIdSource = "none",
        scanRunId = "",
        isReading = false,
        phase = "unavailable",
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
        lastError = "MAP_BACKEND_NOT_IMPLEMENTED",
    };

    private static Dictionary<string, int> EmptyMapCounts() =>
        MapKinds.ToDictionary(kind => kind, _ => 0, StringComparer.Ordinal);

    private void RequireProfile(JsonElement payload)
    {
        string value = GetRequiredString(payload, "profileId");
        if (!string.Equals(value, ProfileId, StringComparison.Ordinal))
            throw new BridgeCommandException("PROFILE_SCOPE_MISMATCH", "The command targets a different local profile.");
    }

    private void RequireOptionalProfile(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("profileId", out JsonElement property))
            return;
        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;
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
