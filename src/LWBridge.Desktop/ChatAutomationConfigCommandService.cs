using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

/// <summary>Recovered 0.3.17 Chat settings, persisted per profile. No game actions.</summary>
internal sealed class ChatAutomationConfigCommandService(ProfileRuntimeConfigStore store) : INativeAsyncCommandService
{
    internal static readonly string[] Kinds = ["redPacket", "fireworks", "treasure"];
    public bool CanHandle(string command) => command is "feature018_chat_config_get" or "feature018_chat_config_save";

    internal static JsonObject Default(string kind) => new()
    {
        ["enabled"] = false, ["claimDelaySeconds"] = new JsonArray(0d, 0d),
        ["replyEnabled"] = false, ["replyDelaySeconds"] = new JsonArray(2d, 5d),
        ["replies"] = new JsonArray(), ["searchEnabled"] = false,
        ["dispatchEnabled"] = false, ["dispatchDelaySeconds"] = new JsonArray(2d, 5d),
        ["dispatchSquadPriority"] = new JsonArray(1), ["dispatchRetrySeconds"] = 30,
    };

    internal static string ReadKind(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("kind", out JsonElement value) || value.ValueKind != JsonValueKind.String ||
            value.GetString() is not { } kind || !Kinds.Contains(kind, StringComparer.Ordinal))
            throw Invalid("Unknown Chat service.");
        return kind;
    }

    public Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string kind = ReadKind(payload);
        if (!CanHandle(command)) throw Invalid("Unknown Chat config command.");
        string[] permitted = command == "feature018_chat_config_save" ? ["kind", "config", "profileId"] : ["kind", "profileId"];
        if (payload.EnumerateObject().Any(p => !permitted.Contains(p.Name, StringComparer.Ordinal)))
            throw Invalid("Chat configuration contains unexpected fields.");
        if (command == "feature018_chat_config_get")
            return Task.FromResult<object?>(store.ReadTasksSnapshot()[kind]?.DeepClone() ?? Default(kind));
        if (!payload.TryGetProperty("config", out JsonElement input)) throw Invalid("Chat config is required.");
        JsonObject config = Validate(kind, input);
        return Task.FromResult<object?>(store.SaveResourceAutomationConfig(kind, config));
    }

    internal static JsonObject Validate(string kind, JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) throw Invalid("Chat config must be an object.");
        var allowed = new HashSet<string>(["enabled", "claimDelaySeconds", "replyEnabled", "replyDelaySeconds", "replies",
            "searchEnabled", "dispatchEnabled", "dispatchDelaySeconds", "dispatchSquadPriority", "dispatchRetrySeconds"], StringComparer.Ordinal);
        if (input.EnumerateObject().Any(p => !allowed.Contains(p.Name))) throw Invalid("Unexpected Chat setting.");
        var config = Default(kind);
        foreach (JsonProperty property in input.EnumerateObject()) config[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        foreach (string name in new[] { "enabled", "replyEnabled", "searchEnabled", "dispatchEnabled" })
            if (config[name] is not JsonValue boolean || !boolean.TryGetValue<bool>(out _)) throw Invalid($"Invalid {name}.");
        Range(config, "claimDelaySeconds", 0, kind == "treasure" ? 600 : 60);
        Range(config, "replyDelaySeconds", 0.1, 600);
        Range(config, "dispatchDelaySeconds", 0.1, 600);
        if (config["replies"] is not JsonArray replies || replies.Count > 100 ||
            replies.Any(p => p is not JsonValue value || !value.TryGetValue<string>(out string? phrase) || string.IsNullOrWhiteSpace(phrase) || phrase.Length > 500))
            throw Invalid("Reply phrases must be nonempty and at most 500 characters.");
        // Owner 2026-10-21: Auto Reply on with no phrases saves; the engine simply sends no reply until one is added.
        if (config["dispatchSquadPriority"] is not JsonArray squads || squads.Count > 4 ||
            squads.Any(s => s is not JsonValue number || !number.TryGetValue<int>(out int n) || n is < 1 or > 4) ||
            squads.Select(s => s!.GetValue<int>()).Distinct().Count() != squads.Count ||
            config["dispatchEnabled"]!.GetValue<bool>() && squads.Count == 0)
            throw Invalid("Treasure dispatch requires distinct squad numbers 1–4.");
        if (config["dispatchRetrySeconds"] is not JsonValue retry || !retry.TryGetValue<int>(out int seconds) || seconds is < 1 or > 300)
            throw Invalid("Treasure dispatch retry must be 1–300 seconds.");
        if (kind != "treasure")
            foreach (string name in new[] { "searchEnabled", "dispatchEnabled", "dispatchDelaySeconds", "dispatchSquadPriority", "dispatchRetrySeconds" }) config.Remove(name);
        return config;
    }

    private static void Range(JsonObject config, string key, double minimum, double maximum)
    {
        if (config[key] is not JsonArray { Count: 2 } range ||
            range[0] is not JsonValue first || !first.TryGetValue<double>(out double a) ||
            range[1] is not JsonValue second || !second.TryGetValue<double>(out double b) ||
            !double.IsFinite(a) || !double.IsFinite(b) || a < minimum || b > maximum || a > b)
            throw Invalid($"Invalid {key} range.");
    }
    private static BridgeCommandException Invalid(string message) => new("INVALID_REQUEST", message);
}
