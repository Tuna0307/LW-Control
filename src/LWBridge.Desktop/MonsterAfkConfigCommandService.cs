using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

internal sealed class MonsterAfkConfigCommandService : INativeAsyncCommandService
{
    private readonly ProfileRuntimeConfigStore store;

    internal MonsterAfkConfigCommandService(string runtimeConfigPath) =>
        store = new ProfileRuntimeConfigStore(runtimeConfigPath);

    internal MonsterAfkConfigCommandService(ProfileRuntimeConfigStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public bool CanHandle(string command) =>
        command is "monster_afk_config_save";

    public Task<object?> InvokeAsync(
        string command,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command != "monster_afk_config_save")
        {
            throw new BridgeCommandException(
                "COMMAND_NOT_IMPLEMENTED",
                $"Monster AFK config service does not handle '{command}'.");
        }

        JsonObject config = ParseAndValidate(payload);
        return Task.FromResult<object?>(store.SaveMonsterSweepConfig(config));
    }

    private static JsonObject ParseAndValidate(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("strategies", out JsonElement strategies) ||
            strategies.ValueKind != JsonValueKind.Array ||
            !TryReadBoolean(payload, "enabled", out _))
        {
            throw InvalidMonsterAfkConfig();
        }

        if (!payload.TryGetProperty("allianceDrill", out JsonElement allianceDrill) ||
            allianceDrill.ValueKind != JsonValueKind.Object ||
            !TryReadBoolean(allianceDrill, "enabled", out bool drillEnabled) ||
            !TryReadBoolean(allianceDrill, "activeRally", out _) ||
            !allianceDrill.TryGetProperty("squadIndexes", out JsonElement drillSquads) ||
            drillSquads.ValueKind != JsonValueKind.Array)
        {
            throw InvalidAllianceDrillConfig();
        }

        int drillSquadCount = ValidateSquadIndexes(
            drillSquads,
            "duplicate alliance drill squad");
        if (drillEnabled && drillSquadCount == 0)
            throw Invalid("alliance drill squad required");

        var strategyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement strategy in strategies.EnumerateArray())
            ValidateStrategy(strategy, strategyIds);

        JsonNode? node = JsonNode.Parse(payload.GetRawText());
        if (node is not JsonObject config)
            throw InvalidMonsterAfkConfig();

        config.Remove("profileId");
        return config;
    }

    private static void ValidateStrategy(
        JsonElement strategy,
        HashSet<string> strategyIds)
    {
        if (strategy.ValueKind != JsonValueKind.Object ||
            !strategy.TryGetProperty("id", out JsonElement idElement) ||
            idElement.ValueKind != JsonValueKind.String)
        {
            throw Invalid("invalid monster AFK strategy id");
        }

        string id = (idElement.GetString() ?? string.Empty).Trim();
        if (id.Length == 0 || !strategyIds.Add(id))
            throw Invalid("invalid monster AFK strategy id");

        if (!strategy.TryGetProperty("kind", out JsonElement kindElement) ||
            kindElement.ValueKind != JsonValueKind.String)
        {
            throw Invalid("invalid monster AFK strategy kind");
        }

        string? kind = kindElement.GetString();
        bool farm = string.Equals(kind, "farm", StringComparison.Ordinal);
        bool join = string.Equals(kind, "join", StringComparison.Ordinal);
        if (!farm && !join)
            throw Invalid("invalid monster AFK strategy kind");

        if (!TryReadBoolean(strategy, "attackEnabled", out bool attackEnabled) ||
            !TryReadBoolean(strategy, "joinEnabled", out bool joinEnabled) ||
            attackEnabled != farm ||
            joinEnabled != join)
        {
            throw Invalid("invalid monster AFK strategy action");
        }

        if (join &&
            (!TryReadBoolean(strategy, "rally", out bool rally) || !rally))
        {
            throw Invalid("invalid monster AFK strategy action");
        }

        if (strategy.TryGetProperty("executionLimit", out JsonElement executionLimit) &&
            (!executionLimit.TryGetInt64(out long limit) || limit < 0))
        {
            throw Invalid("invalid monster AFK execution limit");
        }

        bool levelFilterEnabled =
            TryReadBoolean(strategy, "levelFilterEnabled", out bool levelFilter) &&
            levelFilter;
        if (levelFilterEnabled)
        {
            long minLevel = ReadPositiveIntegerOrZero(strategy, "minLevel");
            long maxLevel = ReadPositiveIntegerOrZero(strategy, "maxLevel");
            bool progressive =
                TryReadBoolean(strategy, "progressiveLevels", out bool progress) &&
                progress;

            if (minLevel <= 0 || (!progressive && maxLevel < minLevel))
                throw Invalid("invalid monster AFK level range");
        }

        if (!TryReadBoolean(strategy, "enabled", out bool enabled))
            throw Invalid("invalid monster AFK strategy state");

        if (!strategy.TryGetProperty("squadIndexes", out JsonElement squadIndexes) ||
            squadIndexes.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("monster AFK squad required");
        }

        int squadCount = ValidateSquadIndexes(
            squadIndexes,
            "duplicate monster AFK squad");
        if (enabled && squadCount == 0)
            throw Invalid("monster AFK squad required");
    }

    private static int ValidateSquadIndexes(
        JsonElement squadIndexes,
        string duplicateMessage)
    {
        var squads = new HashSet<long>();
        int count = 0;
        foreach (JsonElement item in squadIndexes.EnumerateArray())
        {
            if (!item.TryGetInt64(out long squadIndex) ||
                squadIndex < 1 ||
                squadIndex > 4)
            {
                throw Invalid("invalid squad index");
            }

            if (!squads.Add(squadIndex))
                throw Invalid(duplicateMessage);
            count++;
        }

        return count;
    }

    private static long ReadPositiveIntegerOrZero(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out JsonElement element) ||
            !element.TryGetInt64(out long integer) ||
            integer <= 0)
        {
            return 0;
        }

        return integer;
    }

    private static bool TryReadBoolean(
        JsonElement value,
        string property,
        out bool result)
    {
        result = false;
        if (!value.TryGetProperty(property, out JsonElement element))
            return false;

        if (element.ValueKind == JsonValueKind.True)
        {
            result = true;
            return true;
        }

        if (element.ValueKind == JsonValueKind.False)
            return true;

        return false;
    }

    private static BridgeCommandException InvalidMonsterAfkConfig() =>
        Invalid("invalid monster AFK config");

    private static BridgeCommandException InvalidAllianceDrillConfig() =>
        Invalid("invalid alliance drill config");

    private static BridgeCommandException Invalid(string message) =>
        new("INVALID_REQUEST", message);
}
