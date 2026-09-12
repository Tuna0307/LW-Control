using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record MapScanStartOptions(
    string ScanMode,
    int Concurrency,
    IReadOnlyList<string> SelectedTypes);

internal static class MapScanContract
{
    public static readonly string[] AllTypes =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    private static readonly HashSet<string> AllowedTypes = new(AllTypes, StringComparer.Ordinal);

    public static MapScanStartOptions NormalizeStart(JsonElement payload)
    {
        string mode = "normal";
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("scanMode", out JsonElement modeValue))
        {
            if (modeValue.ValueKind != JsonValueKind.String)
                throw new BridgeCommandException("INVALID_SCAN_MODE", "scanMode must be normal or fast.");
            mode = modeValue.GetString() ?? string.Empty;
        }
        if (mode is not ("normal" or "fast"))
            throw new BridgeCommandException("INVALID_SCAN_MODE", "scanMode must be normal or fast.");

        IReadOnlyList<string> selected = AllTypes;
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

        return new MapScanStartOptions(mode, mode == "fast" ? 20 : 8, selected);
    }
}
