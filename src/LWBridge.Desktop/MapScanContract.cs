using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record MapScanStartOptions(
    IReadOnlyList<string> SelectedTypes,
    int? TargetServerId = null);

internal sealed record MapScanStrategyPlan(
    string ScanMode,
    int Concurrency,
    string StrategyId);

internal static class MapScanContract
{
    public static readonly string[] RecoveredDefaultTypes =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    public static readonly string[] AllTypes =
        ["city", "resource", "monster", "zombie_boss", "truck", "railway", "dispatch", "ghost", "treasure"];

    private static readonly HashSet<string> AllowedTypes = new(AllTypes, StringComparer.Ordinal);

    public static MapScanStartOptions NormalizeStart(JsonElement payload)
    {
        // OWNER OVERRIDE LWB-R7-067: scanMode is no longer a public/user-owned
        // input. Older callers may still send it, but backend planning ignores it.
        // This prevents stale saved Normal/Fast preferences from steering acquisition.
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

        int? targetServerId = null;
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("targetServerId", out JsonElement targetValue))
        {
            if (!targetValue.TryGetInt32(out int parsedTarget) || parsedTarget is < 1 or > 99999)
                throw new BridgeCommandException(
                    "INVALID_SERVER_ID",
                    "target server ID must be an integer from 1 to 99999");
            targetServerId = parsedTarget;
        }

        return new MapScanStartOptions(selected, targetServerId);
    }
}

internal static class MapScanStrategyPlanner
{
    internal static bool IsDirectTrainListSelection(IReadOnlyList<string> selectedTypes) =>
        selectedTypes.Count >= 1 && selectedTypes.All(type => type is "truck" or "railway");

    internal const string FastFullWorldStrategy = "current_fast_full_world_v2";
    internal const string FastTrainListStrategy = "current_fast_train_list_v1";
    internal const string FastZombieBossStrategy = "current_fast_zombie_boss_lod2_v1";
    internal const string NormalBlockStrategy = "current_lod0_block_v1";

    internal static MapScanStrategyPlan Plan(
        CurrentClientMapContext context,
        IReadOnlyList<string> selectedTypes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selectedTypes);
        if (selectedTypes.Count == 0)
            throw new BridgeCommandException("INVALID_SCAN_TYPES", "no valid map scan types selected");

        bool standardCurrentWorld =
            context.WorldId == 0 &&
            context.TileWidth == 1000 &&
            context.TileHeight == 1000;

        if (standardCurrentWorld)
        {
            bool zombieBossOnly =
                selectedTypes.Count == 1 &&
                selectedTypes[0] == "zombie_boss";
            bool trainListOnly = IsDirectTrainListSelection(selectedTypes);
            string strategy = zombieBossOnly
                ? FastZombieBossStrategy
                : trainListOnly
                    ? FastTrainListStrategy
                    : FastFullWorldStrategy;
            return new MapScanStrategyPlan(
                "fast",
                20,
                strategy);
        }

        if (selectedTypes.Count == 1 && selectedTypes[0] is "city" or "resource")
        {
            return new MapScanStrategyPlan(
                "normal",
                8,
                NormalBlockStrategy);
        }

        throw new BridgeCommandException(
            "LIVE_BLOCK_TYPES_UNSUPPORTED",
            "The selected Map Data kinds do not have a proven acquisition strategy for the current world geometry.");
    }
}
