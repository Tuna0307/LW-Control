using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

/// <summary>R30 recovered per-profile settings. This service never sends game requests.</summary>
internal sealed class AutomationTaskConfigCommandService(ProfileRuntimeConfigStore store) : INativeAsyncCommandService
{
    internal static readonly string[] Kinds = ["allianceHelp"];
    public bool CanHandle(string command) => command is "feature018_automation_config_get" or "feature018_automation_config_save";

    internal static string ReadKind(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty("kind", out var value) ||
            value.ValueKind != JsonValueKind.String || value.GetString() is not { } kind || !Kinds.Contains(kind, StringComparer.Ordinal))
            throw Invalid("This Automation service is unavailable.");
        return kind;
    }
    internal static JsonObject Default(string kind) => new() { ["enabled"] = false };
    internal static JsonObject Validate(string kind, JsonElement input)
    {
        if (!Kinds.Contains(kind, StringComparer.Ordinal) || input.ValueKind != JsonValueKind.Object ||
            input.EnumerateObject().Any(p => p.Name != "enabled")) throw Invalid("Unexpected Automation setting.");
        var config = Default(kind);
        foreach (var property in input.EnumerateObject()) config[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        if (config["enabled"] is not JsonValue boolean || !boolean.TryGetValue<bool>(out _))
            throw Invalid("Automation enabled must be a boolean.");
        return config;
    }
    public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string kind = ReadKind(payload);
        if (!CanHandle(command) || payload.EnumerateObject().Any(p => p.Name is not ("kind" or "profileId") &&
                !(command == "feature018_automation_config_save" && p.Name == "config"))) throw Invalid("Unexpected Automation request field.");
        JsonObject config = Validate(kind, JsonSerializer.SerializeToElement(store.ReadTasksSnapshot()[kind] ?? Default(kind)));
        if (command == "feature018_automation_config_get") return Task.FromResult<object?>(config);
        if (!payload.TryGetProperty("config", out var input)) throw Invalid("Automation settings are required.");
        return Task.FromResult<object?>(store.SaveResourceAutomationConfig(kind, Validate(kind, input)));
    }
    private static BridgeCommandException Invalid(string message) => new("INVALID_REQUEST", message);
}
