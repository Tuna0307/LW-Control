using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

/// <summary>RECOVERED original mission settings; validation has no game access.</summary>
internal static class AutomationMissionConfig
{
    internal static JsonObject? Default(string kind) => kind switch
    {
        "dispatch" => new()
        {
            ["enabled"] = false, ["autoExecute"] = false, ["collectRewards"] = false,
            ["delayMinutes"] = 3, ["weeklyQualities"] = new JsonArray("none", "ur", "none", "none", "none", "ur", "none"),
        },
        "dispatchAssist" => new()
        {
            ["enabled"] = false, ["autoHelp"] = false, ["qualities"] = new JsonArray(),
            ["delaySeconds"] = new JsonArray(0, 0), ["intervalSeconds"] = 30,
        },
        _ => null,
    };

    internal static JsonObject Validate(string kind, JsonObject input)
    {
        var config = Default(kind) ?? throw Invalid("Unknown mission service.");
        if (input.Any(property => !config.ContainsKey(property.Key))) throw Invalid("Unexpected mission setting.");
        foreach (var property in input) config[property.Key] = property.Value?.DeepClone();
        _ = Boolean(config, "enabled");
        if (kind == "dispatch")
        {
            bool execute = Boolean(config, "autoExecute"), collect = Boolean(config, "collectRewards");
            Integer(config["delayMinutes"], 0, 1440, "Secret Task delay must be 0–1440 whole minutes.");
            Strings(config["weeklyQualities"], ["none", "ssr", "ur"], 7, 7, false, "Choose one valid quality for each of the seven weekdays.");
            config["enabled"] = execute || collect;
        }
        else
        {
            bool help = Boolean(config, "autoHelp");
            var qualities = Strings(config["qualities"], ["n", "r", "sr", "ssr", "ur", "special"], help ? 1 : 0, 6, true, "Select the Secret Task qualities to assist.");
            if (config["delaySeconds"] is not JsonArray delay || delay.Count != 2) throw Invalid("Dispatch Assist needs a minimum and maximum delay.");
            int min = Integer(delay[0], 0, 86400, "Dispatch Assist delays must be 0–86400 whole seconds.");
            int max = Integer(delay[1], 0, 86400, "Dispatch Assist delays must be 0–86400 whole seconds.");
            if (min > max) throw Invalid("Dispatch Assist minimum delay must not exceed maximum delay.");
            Integer(config["intervalSeconds"], 5, 300, "Dispatch Assist interval must be 5–300 whole seconds.");
            config["qualities"] = qualities;
            config["enabled"] = help;
        }
        return config;
    }

    private static bool Boolean(JsonObject input, string key) => input[key] is JsonValue value && value.TryGetValue<bool>(out bool result)
        ? result : throw Invalid($"Mission setting {key} must be a boolean.");

    private static int Integer(JsonNode? node, int min, int max, string error) => node is JsonValue value && value.TryGetValue<int>(out int number) && number >= min && number <= max
        ? number : throw Invalid(error);

    private static JsonArray Strings(JsonNode? node, string[] allowed, int min, int max, bool unique, string error)
    {
        if (node is not JsonArray values || values.Count < min || values.Count > max) throw Invalid(error);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in values)
        {
            if (entry is not JsonValue value || !value.TryGetValue<string>(out var text) || !allowed.Contains(text, StringComparer.Ordinal) || unique && !seen.Add(text)) throw Invalid(error);
        }
        return (JsonArray)values.DeepClone();
    }

    private static BridgeCommandException Invalid(string message) => new("INVALID_REQUEST", message);
}
