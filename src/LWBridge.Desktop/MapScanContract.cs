using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record MapScanStartOptions(
    IReadOnlyList<string> SelectedTypes,
    string ScanMode);

internal static class MapScanContract
{
    public static readonly string[] RecoveredDefaultTypes =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    // Zombie Boss (game text 2901012; MonsterInvasionBoss special 10) is a selectable
    // passive kind but, like Other, is not part of the implicit eight-kind default.
    public const string ZombieBossKind = "zombie_boss";

    public static readonly string[] AllTypes = [..RecoveredDefaultTypes, "other", ZombieBossKind];

    private static readonly HashSet<string> AllowedTypes = new(AllTypes, StringComparer.Ordinal);

    public static MapScanStartOptions NormalizeStart(JsonElement payload)
    {
        string scanMode = "normal";
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("scanMode", out JsonElement modeValue) &&
            modeValue.ValueKind == JsonValueKind.String &&
            modeValue.GetString() is string requestedMode)
        {
            // Original 0.3.1 defaults missing/null/non-string values to normal.
            // Preserve invalid strings here so validation happens after the
            // recovered game/world admission work inside StartAsync.
            scanMode = requestedMode;
        }

        IReadOnlyList<string> selected = RecoveredDefaultTypes;
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("selectedTypes", out JsonElement typesValue) &&
            typesValue.ValueKind == JsonValueKind.Array)
        {
            var filtered = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement entry in typesValue.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String) continue;
                string? type = entry.GetString();
                if (type is null || !AllowedTypes.Contains(type) || !seen.Add(type)) continue;
                filtered.Add(type);
            }
            if (filtered.Count == 0)
                throw new BridgeCommandException("INVALID_SCAN_TYPES", "no valid map scan types selected");
            selected = filtered;
        }

        return new MapScanStartOptions(selected, scanMode);
    }
}
