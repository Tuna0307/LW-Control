using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed record HotkeyConfig(
    bool Attack = true,
    bool AttackMarchSpeedupItem = false,
    bool AttackMarchSpeedupDiamond = false,
    bool Recall = true,
    bool ShieldOverlay = true,
    bool ShieldUse = true,
    bool Equipment = true,
    bool RandomRelocate = false,
    bool AllianceRelocate = false,
    bool FrontlineReinforce = true)
{
    internal static HotkeyConfig Default { get; } = new();
}

internal sealed record VisualMetricsConfig(
    bool ShowFps = false,
    bool ShowPing = false)
{
    internal static VisualMetricsConfig Default { get; } = new();
}

internal sealed class ProfileRuntimeConfigStore
{
    private static readonly ConcurrentDictionary<string, object> StorageGates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string path;
    private readonly object storageGate;

    internal ProfileRuntimeConfigStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Runtime config path is required.", nameof(path));

        this.path = Path.GetFullPath(path);
        storageGate = StorageGates.GetOrAdd(this.path, static _ => new object());
    }

    internal HotkeyConfig ReadHotkeys()
    {
        lock (storageGate)
        {
            try
            {
                JsonObject? root = ReadRoot(allowMissing: true);
                if (root is null || !root.TryGetPropertyValue("hotkeys", out JsonNode? node) || node is null)
                    return HotkeyConfig.Default;

                return DecodeHotkeys(node);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal HotkeyConfig SaveHotkeys(HotkeyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (storageGate)
        {
            try
            {
                JsonObject root = ReadRoot(allowMissing: true) ?? new JsonObject();
                root["hotkeys"] = JsonSerializer.SerializeToNode(config, JsonOptions.Default);

                WriteRoot(root);
                return config;
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal VisualMetricsConfig ReadVisualMetrics()
    {
        lock (storageGate)
        {
            try
            {
                JsonObject? root = ReadRoot(allowMissing: true);
                if (root is null ||
                    !root.TryGetPropertyValue("visualMetrics", out JsonNode? node) ||
                    node is null)
                {
                    return VisualMetricsConfig.Default;
                }

                return DecodeVisualMetrics(node);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal VisualMetricsConfig SaveVisualMetrics(VisualMetricsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (storageGate)
        {
            try
            {
                JsonObject root = ReadRoot(allowMissing: true) ?? new JsonObject();
                root["visualMetrics"] =
                    JsonSerializer.SerializeToNode(config, JsonOptions.Default);

                WriteRoot(root);
                return config;
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal JsonObject ReadEquipmentConfig()
    {
        lock (storageGate)
        {
            try
            {
                JsonObject? root = ReadRoot(allowMissing: true);
                return ProjectEquipmentConfig(root);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal JsonObject SaveEquipmentConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (storageGate)
        {
            try
            {
                JsonObject root = ReadRoot(allowMissing: true) ?? new JsonObject();

                root["equipmentPresets"] =
                    config["equipmentPresets"]?.DeepClone();

                if (config.TryGetPropertyValue(
                    "initialEquipmentConfig",
                    out JsonNode? initialEquipmentConfig))
                {
                    root["initialEquipmentConfig"] =
                        initialEquipmentConfig?.DeepClone();
                }
                else
                {
                    root.Remove("initialEquipmentConfig");
                }

                root.Remove("equipmentSchemes");
                root.Remove("squadEquipmentBindings");

                WriteRoot(root);
                return ProjectEquipmentConfig(root);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal JsonObject ReadTasksSnapshot()
    {
        lock (storageGate)
        {
            try
            {
                JsonObject? root = ReadRoot(allowMissing: true);
                if (root is null ||
                    !root.TryGetPropertyValue("tasks", out JsonNode? tasksNode) ||
                    tasksNode is null)
                {
                    return new JsonObject();
                }

                if (tasksNode is not JsonObject tasks)
                    throw StateUnavailable();

                return (JsonObject)tasks.DeepClone();
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal JsonObject SaveMonsterSweepConfig(JsonObject config) =>
        SaveTaskConfig("monsterSweep", config);

    internal JsonObject SaveAllianceGarrisonConfig(JsonObject config) =>
        SaveTaskConfig("allianceGarrison", config);

    internal JsonObject SaveResourceAutomationConfig(
        string taskName,
        JsonObject config) =>
        SaveTaskConfig(taskName, config);

    internal JsonArray SaveClaimDelayRange(
        string chatKind,
        string schedulerKey,
        double minSeconds,
        double maxSeconds)
    {
        if (string.IsNullOrWhiteSpace(chatKind))
            throw new ArgumentException(
                "Chat automation kind is required.",
                nameof(chatKind));
        if (string.IsNullOrWhiteSpace(schedulerKey))
            throw new ArgumentException(
                "Scheduler key is required.",
                nameof(schedulerKey));

        lock (storageGate)
        {
            try
            {
                JsonObject root =
                    ReadRoot(allowMissing: true) ?? new JsonObject();

                JsonObject scheduler =
                    GetOrCreateObject(root, "scheduler");
                scheduler[schedulerKey] =
                    CreateRange(minSeconds, maxSeconds);

                JsonObject chatAutomation =
                    GetOrCreateObject(root, "chat_automation");
                JsonObject chatConfig =
                    GetOrCreateObject(chatAutomation, chatKind);
                chatConfig["claimDelaySeconds"] =
                    CreateRange(minSeconds, maxSeconds);

                WriteRoot(root);
                return CreateRange(minSeconds, maxSeconds);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    private static JsonObject GetOrCreateObject(
        JsonObject parent,
        string name)
    {
        if (!parent.TryGetPropertyValue(name, out JsonNode? node) ||
            node is null)
        {
            var created = new JsonObject();
            parent[name] = created;
            return created;
        }

        return node as JsonObject ?? throw StateUnavailable();
    }

    private static JsonArray CreateRange(
        double minSeconds,
        double maxSeconds) =>
        new(
            JsonValue.Create(minSeconds),
            JsonValue.Create(maxSeconds));

    private JsonObject SaveTaskConfig(string taskName, JsonObject config)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Task name is required.", nameof(taskName));
        ArgumentNullException.ThrowIfNull(config);

        lock (storageGate)
        {
            try
            {
                JsonObject root = ReadRoot(allowMissing: true) ?? new JsonObject();
                JsonObject tasks;
                if (!root.TryGetPropertyValue("tasks", out JsonNode? tasksNode) ||
                    tasksNode is null)
                {
                    tasks = new JsonObject();
                    root["tasks"] = tasks;
                }
                else if (tasksNode is JsonObject existingTasks)
                {
                    tasks = existingTasks;
                }
                else
                {
                    throw StateUnavailable();
                }

                tasks[taskName] = config.DeepClone();
                WriteRoot(root);
                return (JsonObject)config.DeepClone();
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    private static JsonObject ProjectEquipmentConfig(JsonObject? root)
    {
        var result = new JsonObject();

        if (root is not null &&
            root.TryGetPropertyValue("equipmentPresets", out JsonNode? presets))
        {
            result["equipmentPresets"] = presets?.DeepClone();
        }
        else
        {
            result["equipmentPresets"] = new JsonArray();
        }

        if (root is not null &&
            root.TryGetPropertyValue(
                "initialEquipmentConfig",
                out JsonNode? initialEquipmentConfig))
        {
            result["initialEquipmentConfig"] =
                initialEquipmentConfig?.DeepClone();
        }

        return result;
    }

    private void WriteRoot(JsonObject root)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw StateUnavailable();
        Directory.CreateDirectory(directory);

        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporary,
                root.ToJsonString(JsonOptions.Indented));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch { }
        }
    }

    private JsonObject? ReadRoot(bool allowMissing)
    {
        if (!File.Exists(path))
            return allowMissing ? null : throw StateUnavailable();

        string json = File.ReadAllText(path);
        JsonNode? node = JsonNode.Parse(json);
        if (node is not JsonObject root)
            throw StateUnavailable();
        return root;
    }

    private static HotkeyConfig DecodeHotkeys(JsonNode node)
    {
        try
        {
            HotkeyConfig? config = node.Deserialize<HotkeyConfig>(JsonOptions.Default);
            return config ?? throw StateUnavailable();
        }
        catch (JsonException)
        {
            throw StateUnavailable();
        }
    }

    private static VisualMetricsConfig DecodeVisualMetrics(JsonNode node)
    {
        try
        {
            VisualMetricsConfig? config =
                node.Deserialize<VisualMetricsConfig>(JsonOptions.Default);
            return config ?? throw StateUnavailable();
        }
        catch (JsonException)
        {
            throw StateUnavailable();
        }
    }

    private static BridgeCommandException StateUnavailable() =>
        new("STATE_UNAVAILABLE", "config state is unavailable");
}

internal sealed class HotkeyConfigCommandService : INativeAsyncCommandService
{
    private static readonly string[] BooleanFields =
    [
        "attack",
        "attackMarchSpeedupItem",
        "attackMarchSpeedupDiamond",
        "recall",
        "shieldOverlay",
        "shieldUse",
        "equipment",
        "randomRelocate",
        "allianceRelocate",
        "frontlineReinforce",
    ];

    private readonly ProfileRuntimeConfigStore store;

    internal HotkeyConfigCommandService(string runtimeConfigPath) =>
        store = new ProfileRuntimeConfigStore(runtimeConfigPath);

    internal HotkeyConfigCommandService(ProfileRuntimeConfigStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public bool CanHandle(string command) =>
        command is "hotkey_config_get" or "hotkey_config_save";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object result = command switch
        {
            "hotkey_config_get" => store.ReadHotkeys(),
            "hotkey_config_save" => store.SaveHotkeys(ParseSavePayload(payload)),
            _ => throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Hotkey config service does not handle '{command}'."),
        };

        return Task.FromResult<object?>(result);
    }

    private static HotkeyConfig ParseSavePayload(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw InvalidHotkeyConfig();

        foreach (string field in BooleanFields)
        {
            if (!payload.TryGetProperty(field, out JsonElement value) ||
                value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw InvalidHotkeyConfig();
            }
        }

        return new HotkeyConfig(
            payload.GetProperty("attack").GetBoolean(),
            payload.GetProperty("attackMarchSpeedupItem").GetBoolean(),
            payload.GetProperty("attackMarchSpeedupDiamond").GetBoolean(),
            payload.GetProperty("recall").GetBoolean(),
            payload.GetProperty("shieldOverlay").GetBoolean(),
            payload.GetProperty("shieldUse").GetBoolean(),
            payload.GetProperty("equipment").GetBoolean(),
            payload.GetProperty("randomRelocate").GetBoolean(),
            payload.GetProperty("allianceRelocate").GetBoolean(),
            payload.GetProperty("frontlineReinforce").GetBoolean());
    }

    private static BridgeCommandException InvalidHotkeyConfig() =>
        new("INVALID_REQUEST", "invalid hotkey config");
}
