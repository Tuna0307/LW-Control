using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed record HotkeyConfig(
    bool Attack = true,
    bool Recall = true,
    bool ShieldUse = true,
    bool Equipment = true,
    bool RandomRelocate = false,
    bool AllianceRelocate = false,
    bool FrontlineReinforce = true,
    bool TreasureChestHint = false,
    int FrontlineReinforceCount = 5)
{
    internal const int MinFrontlineReinforceCount = 1;
    internal const int MaxFrontlineReinforceCount = 50;
    internal static HotkeyConfig Default { get; } = new();

    // OWN_DESIGN (owner request 2026-10-17): soldiers added per G press; the
    // original 0.3.17 used a fixed 5. Out-of-range stored values fall back to 5.
    internal HotkeyConfig Normalized() =>
        FrontlineReinforceCount is >= MinFrontlineReinforceCount and <= MaxFrontlineReinforceCount
            ? this : this with { FrontlineReinforceCount = 5 };
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

    // Audit 2026-10-23 W06: the in-process gate alone let two app processes read the same old file and
    // each replace it, losing one save. A named mutex per settings file extends the whole
    // read/modify/write across processes; the in-process gate stays for same-process callers.
    private ExclusiveScope Exclusive()
    {
        Monitor.Enter(storageGate);
        Mutex? mutex = null;
        try
        {
            string name = @"Local\LWBridgeRuntimeConfig-" + Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
            mutex = new Mutex(false, name);
            bool owned;
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { owned = true; /* the previous owner exited; we hold it now */ }
            if (!owned) throw StateUnavailable();
            return new ExclusiveScope(storageGate, mutex);
        }
        catch
        {
            mutex?.Dispose();
            Monitor.Exit(storageGate);
            throw;
        }
    }

    private readonly struct ExclusiveScope(object gate, Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            mutex.Dispose();
            Monitor.Exit(gate);
        }
    }

    internal HotkeyConfig ReadHotkeys()
    {
        using (Exclusive())
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

        using (Exclusive())
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
        using (Exclusive())
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

        using (Exclusive())
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
        using (Exclusive())
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

        using (Exclusive())
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

    internal JsonObject RenameEquipmentPreset(int slot, string expectedPresetId, string name)
    {
        if (slot is < 1 or > 4 || string.IsNullOrWhiteSpace(expectedPresetId) || string.IsNullOrWhiteSpace(name))
            throw new BridgeCommandException("INVALID_REQUEST", "Choose an existing preset and enter its name.");
        using (Exclusive())
        {
            try
            {
                JsonObject root = ReadRoot(allowMissing: false) ?? throw StateUnavailable();
                if (root["equipmentPresets"] is not JsonArray presets || presets.Count < slot ||
                    presets[slot - 1] is not JsonObject preset ||
                    preset["id"]?.GetValue<string>() != expectedPresetId)
                    throw new BridgeCommandException("INVALID_REQUEST", "The saved preset changed. Refresh before renaming it.");
                // Read, patch and write under the same cross-process gate. A UI
                // snapshot must not overwrite newer gear or restoration data.
                preset["name"] = name.Trim();
                WriteRoot(root);
                return ProjectEquipmentConfig(root);
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal JsonObject ReadTasksSnapshot()
    {
        using (Exclusive())
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

                return WithoutRetiredTasks((JsonObject)tasks.DeepClone());
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
        }
    }

    internal JsonObject SaveMonsterSweepConfig(JsonObject config) =>
        SaveTaskConfig("monsterSweep", config);

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

        using (Exclusive())
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

        using (Exclusive())
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
            RetryFileAccess(() =>
            {
                File.WriteAllText(temporary, root.ToJsonString(JsonOptions.Indented));
                File.Move(temporary, path, overwrite: true);
                return true;
            });
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

    private static T RetryFileAccess<T>(Func<T> operation)
    {
        // A reader/indexer can briefly deny replacement (or the initial read).
        // Retry local file access only; this never retries a game command.
        for (int attempt = 0; ; attempt++)
        {
            try { return operation(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt == 5)
                    throw new BridgeCommandException("CONFIG_FILE_BUSY",
                        "Could not save the plan file. Your edits are still here. Close any program using the file and try Save again.");
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    private JsonObject? ReadRoot(bool allowMissing)
    {
        if (!File.Exists(path))
            return allowMissing ? null : throw StateUnavailable();

        string json = RetryFileAccess(() => File.ReadAllText(path));
        JsonNode? node = JsonNode.Parse(json);
        if (node is not JsonObject root)
            throw StateUnavailable();
        if (root["tasks"] is JsonObject tasks) WithoutRetiredTasks(tasks);
        return root;
    }

    internal static JsonObject WithoutRetiredTasks(JsonObject tasks)
    {
        // OWNER_DIRECTION R18: legacy task settings remain loadable but never run.
        tasks.Remove("allianceGarrison");
        tasks.Remove("zombieBus");
        // Owner R30: retired Automation cards cannot reappear from an old profile.
        foreach (string name in new[] { "construction", "soldierTraining", "stamina", "railway",
                     "allianceTrainRide", "buildingResources", "armedTruckReward", "weekendShield", "attackShield" })
            tasks.Remove(name);
        return tasks;
    }

    private static HotkeyConfig DecodeHotkeys(JsonNode node)
    {
        try
        {
            HotkeyConfig? config = node.Deserialize<HotkeyConfig>(JsonOptions.Default);
            return config?.Normalized() ?? throw StateUnavailable();
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
        "recall",
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
            payload.GetProperty("recall").GetBoolean(),
            payload.GetProperty("shieldUse").GetBoolean(),
            payload.GetProperty("equipment").GetBoolean(),
            payload.GetProperty("randomRelocate").GetBoolean(),
            payload.GetProperty("allianceRelocate").GetBoolean(),
            payload.GetProperty("frontlineReinforce").GetBoolean(),
            ReadTreasureChestHint(payload),
            ReadFrontlineReinforceCount(payload));
    }

    private static int ReadFrontlineReinforceCount(JsonElement payload)
    {
        if (!payload.TryGetProperty("frontlineReinforceCount", out JsonElement value))
            return 5;
        if (!value.TryGetInt32(out int count) ||
            count < HotkeyConfig.MinFrontlineReinforceCount ||
            count > HotkeyConfig.MaxFrontlineReinforceCount)
            throw InvalidHotkeyConfig();
        return count;
    }

    // Added by the observed 0.3.17 Mini Games panel; older profile configs
    // written by 0.3.1 contained the original ten fields only.
    private static bool ReadTreasureChestHint(JsonElement payload)
    {
        if (!payload.TryGetProperty("treasureChestHint", out JsonElement value))
            return false;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw InvalidHotkeyConfig();
        return value.GetBoolean();
    }

    private static BridgeCommandException InvalidHotkeyConfig() =>
        new("INVALID_REQUEST", "invalid hotkey config");
}
