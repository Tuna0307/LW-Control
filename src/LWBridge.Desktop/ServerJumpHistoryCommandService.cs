using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed class ServerJumpHistoryCommandService
{
    private const string SettingKey = "serverJumpHistory";

    private readonly string profileId;
    private readonly MapDataStore? store;

    internal ServerJumpHistoryCommandService(
        string profileId,
        MapDataStore? store)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("profileId is required.", nameof(profileId));
        this.profileId = profileId;
        this.store = store;
    }

    internal static bool IsCommand(string command) =>
        command is
            "server_jump_history_get" or
            "server_jump_history_set" or
            "server_jump_history_import";

    internal IReadOnlyList<int> Invoke(
        string command,
        JsonElement payload)
    {
        RequireRuntime(payload);
        return command switch
        {
            "server_jump_history_get" => Read(),
            "server_jump_history_set" => Set(payload),
            "server_jump_history_import" => Import(payload),
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command,
                "Unsupported server-jump-history command."),
        };
    }

    private IReadOnlyList<int> Read()
    {
        string? stored = store!.ReadAppSettingJson(SettingKey);
        return stored is null
            ? Array.Empty<int>()
            : ReadStoredHistory(stored);
    }

    private IReadOnlyList<int> Set(JsonElement payload)
    {
        IReadOnlyList<int> normalized = NormalizePayloadHistory(payload);
        Persist(normalized);
        return normalized;
    }

    private IReadOnlyList<int> Import(JsonElement payload)
    {
        string? stored = store!.ReadAppSettingJson(SettingKey);
        if (stored is not null)
            return ReadStoredHistory(stored);

        IReadOnlyList<int> normalized = NormalizePayloadHistory(payload);
        Persist(normalized);
        return normalized;
    }

    private void Persist(IReadOnlyList<int> history)
    {
        store!.UpsertAppSettingJson(
            SettingKey,
            JsonSerializer.Serialize(history),
            RecoveredWallClock.UnixTimeMilliseconds());
    }

    private static IReadOnlyList<int> NormalizePayloadHistory(
        JsonElement payload)
    {
        if (!payload.TryGetProperty("history", out JsonElement history))
            return Array.Empty<int>();
        return NormalizeHistory(history);
    }

    private static IReadOnlyList<int> ReadStoredHistory(string valueJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(valueJson);
            return NormalizeHistory(document.RootElement);
        }
        catch (JsonException error)
        {
            throw new BridgeCommandException(
                "INVALID_SETTING",
                error.Message);
        }
    }

    internal static IReadOnlyList<int> NormalizeHistory(JsonElement history)
    {
        if (history.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        var seen = new HashSet<int>();
        var normalized = new List<int>(5);
        foreach (JsonElement item in history.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number ||
                !item.TryGetInt32(out int serverId) ||
                serverId < 1 ||
                serverId > 99999 ||
                !seen.Add(serverId))
            {
                continue;
            }

            normalized.Add(serverId);
            if (normalized.Count == 5)
                break;
        }

        return normalized;
    }

    private void RequireRuntime(JsonElement payload)
    {
        if (!payload.TryGetProperty("profileId", out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new BridgeCommandException(
                "PROFILE_ID_REQUIRED",
                "profileId is required");
        }

        if (!string.Equals(
                property.GetString(),
                profileId,
                StringComparison.Ordinal) ||
            store is null)
        {
            throw new BridgeCommandException(
                "PROFILE_RUNTIME_UNAVAILABLE",
                "profile runtime unavailable");
        }
    }
}
