using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record MapScanStartOptions(
    IReadOnlyList<string> SelectedTypes,
    string ScanMode);

internal sealed record MapScanStrategyPlan(
    string ScanMode,
    int Concurrency,
    string StrategyId);

internal static class MapScanContract
{
    public static readonly string[] RecoveredDefaultTypes =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    public static readonly string[] AllTypes = RecoveredDefaultTypes;

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

internal static class MapScanStrategyPlanner
{
    internal static bool IsDirectTrainListSelection(IReadOnlyList<string> selectedTypes) =>
        selectedTypes.Count >= 1 && selectedTypes.All(type => type is "truck" or "railway");

    internal const string FastFullWorldStrategy = "current_fast_full_world_v2";
    internal const string FastTrainListStrategy = "current_fast_train_list_v1";
    // Historical current-client experiment identifier retained only so archived/live
    // proof helpers compile. The original eight-kind public allowlist cannot select it.
    internal const string FastZombieBossStrategy = "current_fast_zombie_boss_lod2_v1";
    internal const string NormalBlockStrategy = "current_lod0_block_v1";

    internal static int ConcurrencyForMode(string scanMode) => scanMode switch
    {
        "normal" => 8,
        "fast" => 20,
        _ => throw new BridgeCommandException(
            "INVALID_SCAN_MODE",
            "map scan mode must be normal or fast"),
    };

    internal static MapScanStrategyPlan Plan(
        CurrentClientMapContext context,
        IReadOnlyList<string> selectedTypes,
        string scanMode)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selectedTypes);
        if (selectedTypes.Count == 0)
            throw new BridgeCommandException("INVALID_SCAN_TYPES", "no valid map scan types selected");

        int concurrency = ConcurrencyForMode(scanMode);
        bool standardCurrentWorld =
            context.WorldId == 0 &&
            context.TileWidth == 1000 &&
            context.TileHeight == 1000;

        if (standardCurrentWorld)
        {
            string strategy = IsDirectTrainListSelection(selectedTypes)
                ? FastTrainListStrategy
                : FastFullWorldStrategy;
            return new MapScanStrategyPlan(
                scanMode,
                concurrency,
                strategy);
        }

        if (selectedTypes.Count == 1 && selectedTypes[0] is "city" or "resource")
        {
            return new MapScanStrategyPlan(
                scanMode,
                concurrency,
                NormalBlockStrategy);
        }

        throw new BridgeCommandException(
            "LIVE_BLOCK_TYPES_UNSUPPORTED",
            "The selected Map Data kinds do not have a proven acquisition strategy for the current world geometry.");
    }
}
