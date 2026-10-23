using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

/// <summary>R30 recovered per-profile settings. This service never sends game requests.</summary>
internal sealed class AutomationTaskConfigCommandService(ProfileRuntimeConfigStore store) : INativeAsyncCommandService
{
    internal static readonly string[] Kinds = ["allianceHelp", "allianceGift", "allianceDonate", "officialPosition", "treatment",
        "dispatch", "dispatchAssist", "resourceGather", "strongholdResource", "allianceCenterResource", "tradeStation", "allianceGather"];
    private static readonly int[] GatherRadii = [50, 100, 150, 200, 250, 300, 400, 500];
    public bool CanHandle(string command) => command is "feature018_automation_config_get" or "feature018_automation_config_save";

    internal static string ReadKind(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty("kind", out var value) ||
            value.ValueKind != JsonValueKind.String || value.GetString() is not { } kind || !Kinds.Contains(kind, StringComparer.Ordinal))
            throw Invalid("This Automation service is unavailable.");
        return kind;
    }
    internal static JsonObject Default(string kind) => kind switch
    {
        "allianceGift" => new() { ["enabled"] = false, ["intervalMinutes"] = 120 },
        "allianceDonate" => new() { ["enabled"] = false, ["threshold"] = 15 },
        "officialPosition" => new() { ["enabled"] = false, ["positionId"] = 0, ["targetTime"] = "" }, // targetTime "" = as soon as possible
        "treatment" => new() { ["enabled"] = false, ["amountPerArmy"] = 1 },
        // RECOVERED original UI defaults: off; squads off/metal/10; radius 200; manual delay 120 s; no recall.
        "resourceGather" => new()
        {
            ["enabled"] = false, ["scanRadius"] = 200, ["manualResumeDelaySeconds"] = 120, ["recallOnDisable"] = false,
            ["squads"] = new JsonArray(Enumerable.Range(1, 4).Select(i => (JsonNode)new JsonObject
                { ["squadIndex"] = i, ["enabled"] = false, ["resource"] = "metal", ["level"] = 10 }).ToArray()),
        },
        "strongholdResource" or "allianceCenterResource" => new() { ["enabled"] = false, ["intervalMinutes"] = 60 },
        // RECOVERED original: off, squads 1 and 2 in priority order.
        "allianceGather" => new() { ["enabled"] = false, ["allianceGatherSquads"] = new JsonArray(1, 2) },
        "tradeStation" => new()
        {
            ["enabled"] = false, ["crossServerEnabled"] = false, ["selectedItemIds"] = new JsonArray(),
            ["selectedCurrencyIds"] = new JsonArray(15, 650053),
        },
        _ => AutomationMissionConfig.Default(kind) ?? new() { ["enabled"] = false },
    };
    internal static JsonObject Validate(string kind, JsonElement input)
    {
        var config = Default(kind);
        if (!Kinds.Contains(kind, StringComparer.Ordinal) || input.ValueKind != JsonValueKind.Object ||
            input.EnumerateObject().Any(p => !config.ContainsKey(p.Name))) throw Invalid("Unexpected Automation setting.");
        if (kind is "dispatch" or "dispatchAssist")
            return AutomationMissionConfig.Validate(kind, JsonNode.Parse(input.GetRawText())!.AsObject());
        foreach (var property in input.EnumerateObject()) config[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        if (config["enabled"] is not JsonValue boolean || !boolean.TryGetValue<bool>(out _))
            throw Invalid("Automation enabled must be a boolean.");
        if (kind == "allianceGift" && (config["intervalMinutes"] is not JsonValue interval || !interval.TryGetValue<int>(out int minutes) || minutes is < 1 or > 1440))
            throw Invalid("Gift interval must be 1–1440 whole minutes.");
        if (kind == "allianceDonate" && (config["threshold"] is not JsonValue threshold || !threshold.TryGetValue<int>(out int count) || count is < 1 or > 30))
            throw Invalid("Donation threshold must be 1–30 whole attempts.");
        if (kind == "treatment" && (config["amountPerArmy"] is not JsonValue amount || !amount.TryGetValue<int>(out int armyCount) || armyCount is < 1 or > 1000000))
            throw Invalid("Treatment amount must be 1–1000000 whole soldiers per type.");
        if (kind == "officialPosition" && (config["positionId"] is not JsonValue position || !position.TryGetValue<int>(out int id) ||
            id is not (0 or 10002 or 10003 or 10004 or 10005 or 10006 or 10007) || id == 0 && config["enabled"]!.GetValue<bool>()))
            throw Invalid("Select one available official position before enabling.");
        if (kind == "officialPosition" && (config["targetTime"] is not JsonValue target || !target.TryGetValue<string>(out string? at) ||
            at.Length != 0 && !(at.Length == 5 && at[2] == ':' && int.TryParse(at[..2], out int h) && int.TryParse(at[3..], out int m) && h is >= 0 and < 24 && m is >= 0 and < 60)))
            throw Invalid("The target time must be empty or HH:MM.");
        if (kind is "strongholdResource" or "allianceCenterResource" &&
            (config["intervalMinutes"] is not JsonValue every || !every.TryGetValue<int>(out int interval2) || interval2 is < 1 or > 1440))
            throw Invalid("Collection interval must be 1–1440 whole minutes.");
        if (kind == "resourceGather") ValidateGather(config);
        if (kind == "tradeStation") ValidateTrade(config);
        if (kind == "allianceGather")
        {
            if (config["allianceGatherSquads"] is not JsonArray order || order.Count > 4) throw Invalid("Choose up to four gathering squads.");
            var seen = new HashSet<int>();
            foreach (var slot in order)
                if (Int(slot, "Gathering squads must be 1–4.") is int value && (value is < 1 or > 4 || !seen.Add(value)))
                    throw Invalid("Gathering squads must be 1–4 and listed once.");
            if (config["enabled"]!.GetValue<bool>() && order.Count == 0) throw Invalid("Choose at least one squad before enabling.");
        }
        return config;
    }
    private static int Int(JsonNode? node, string error) => node is JsonValue value && value.TryGetValue<int>(out int number) ? number : throw Invalid(error);
    private static bool Bool(JsonNode? node, string error) => node is JsonValue value && value.TryGetValue<bool>(out bool flag) ? flag : throw Invalid(error);
    private static void ValidateGather(JsonObject config)
    {
        if (!GatherRadii.Contains(Int(config["scanRadius"], "Choose a listed gathering scan radius.")))
            throw Invalid("Choose a listed gathering scan radius.");
        int delay = Int(config["manualResumeDelaySeconds"], "Manual resume delay must be whole minutes from 1 to 30.");
        if (delay is < 60 or > 1800 || delay % 60 != 0) throw Invalid("Manual resume delay must be whole minutes from 1 to 30.");
        Bool(config["recallOnDisable"], "Recall on disable must be on or off.");
        if (config["squads"] is not JsonArray squads || squads.Count > 4) throw Invalid("Gathering needs up to four squads.");
        var seen = new HashSet<int>();
        bool any = false;
        foreach (var node in squads)
        {
            if (node is not JsonObject squad || squad.Any(p => p.Key is not ("squadIndex" or "enabled" or "resource" or "level")))
                throw Invalid("Unexpected gathering squad setting.");
            int index = Int(squad["squadIndex"], "Gathering squad must be 1–4.");
            if (index is < 1 or > 4 || !seen.Add(index)) throw Invalid("Gathering squad must be 1–4 and listed once.");
            any |= Bool(squad["enabled"], "Gathering squad switch must be on or off.");
            if (squad["resource"] is not JsonValue resource || !resource.TryGetValue<string>(out var name) || name is not ("metal" or "food" or "gold"))
                throw Invalid("Gathering resource must be metal, food or gold.");
            if (Int(squad["level"], "Gathering level must be 1–10.") is < 1 or > 10) throw Invalid("Gathering level must be 1–10.");
        }
        if (config["enabled"]!.GetValue<bool>() && !any) throw Invalid("Turn on at least one gathering squad first.");
    }
    private static void ValidateTrade(JsonObject config)
    {
        Bool(config["crossServerEnabled"], "Cross-server trading must be on or off.");
        foreach (var (key, min, max) in new[] { ("selectedItemIds", 0, 500), ("selectedCurrencyIds", 1, 20) })
        {
            if (config[key] is not JsonArray ids || ids.Count < min || ids.Count > max) throw Invalid("Select the trade goods and in-game currencies.");
            var seen = new HashSet<int>();
            foreach (var id in ids)
                if (Int(id, "Trade selections must be game IDs.") is int value && (value <= 0 || !seen.Add(value)))
                    throw Invalid("Trade selections must be unique game IDs.");
        }
        if (config["enabled"]!.GetValue<bool>() && ((JsonArray)config["selectedItemIds"]!).Count == 0)
            throw Invalid("Select at least one trade item before enabling.");
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
