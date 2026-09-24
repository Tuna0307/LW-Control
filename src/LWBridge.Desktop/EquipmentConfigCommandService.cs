using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed class EquipmentConfigCommandService : INativeAsyncCommandService
{
    private readonly ProfileRuntimeConfigStore store;

    internal EquipmentConfigCommandService(string runtimeConfigPath) =>
        store = new ProfileRuntimeConfigStore(runtimeConfigPath);

    internal EquipmentConfigCommandService(ProfileRuntimeConfigStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public bool CanHandle(string command) =>
        command is "equipment_config_get" or "equipment_config_save";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        object result = command switch
        {
            "equipment_config_get" => store.ReadEquipmentConfig(),
            "equipment_config_save" =>
                store.SaveEquipmentConfig(ParseSavePayload(payload)),
            _ => throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Equipment config service does not handle '{command}'."),
        };

        return Task.FromResult<object?>(result);
    }

    private static JsonObject ParseSavePayload(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(
                "equipmentPresets",
                out JsonElement equipmentPresets) ||
            equipmentPresets.ValueKind != JsonValueKind.Array)
        {
            throw InvalidEquipmentPresets();
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement preset in equipmentPresets.EnumerateArray())
        {
            if (preset.ValueKind != JsonValueKind.Object ||
                !preset.TryGetProperty("id", out JsonElement idElement) ||
                idElement.ValueKind != JsonValueKind.String ||
                !preset.TryGetProperty("name", out JsonElement nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                throw InvalidEquipmentPreset();
            }

            string id = (idElement.GetString() ?? string.Empty).Trim();
            string name = (nameElement.GetString() ?? string.Empty).Trim();
            if (id.Length == 0 || name.Length == 0 || !ids.Add(id))
                throw InvalidEquipmentPreset();
        }

        var config = new JsonObject
        {
            ["equipmentPresets"] = JsonNode.Parse(equipmentPresets.GetRawText()),
        };

        if (payload.TryGetProperty(
            "initialEquipmentConfig",
            out JsonElement initialEquipmentConfig))
        {
            config["initialEquipmentConfig"] =
                JsonNode.Parse(initialEquipmentConfig.GetRawText());
        }

        return config;
    }

    private static BridgeCommandException InvalidEquipmentPresets() =>
        new("INVALID_REQUEST", "invalid equipment presets");

    private static BridgeCommandException InvalidEquipmentPreset() =>
        new("INVALID_REQUEST", "invalid equipment preset");
}
