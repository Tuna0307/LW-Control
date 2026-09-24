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

                return config;
            }
            catch (BridgeCommandException) { throw; }
            catch (Exception) { throw StateUnavailable(); }
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
