using System.Text.Json;

namespace LWBridge.Desktop;

// Evidence-only discriminator for MAP-014's isolated Windows WebView proof.
// Production search deliberately hides native Trucks after their arriveTs;
// zero visible records must not be reported as a PNG-decoder failure.
internal sealed record Map014TruckImageWitness(
    string State,
    int Total,
    int PageRows,
    int IconSources,
    int DistinctIconSources)
{
    public bool HasPositiveSource => State == "positive_source";
}

internal static class Map014TruckImageWitnessClassifier
{
    internal static Map014TruckImageWitness Classify(JsonElement searchResult)
    {
        int total = searchResult.GetProperty("total").GetInt32();
        JsonElement rows = searchResult.GetProperty("rows");
        if (total < 0 || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Truck witness query returned invalid total/rows.");
        int count = rows.GetArrayLength();
        if (count > total)
            throw new InvalidDataException("Truck witness query returned more page rows than its total.");
        var iconPaths = new List<string>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("currentGoods", out JsonElement goods) ||
                goods.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement item in goods.EnumerateArray())
            {
                if (item.TryGetProperty("iconPath", out JsonElement icon) &&
                    icon.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(icon.GetString()))
                    iconPaths.Add(icon.GetString()!);
            }
        }
        string state = total == 0 && count == 0 ? "no_unexpired_truck_rows" :
            count == 0 ? "no_page_rows" :
            iconPaths.Count == 0 ? "no_native_reward_icon_sources" :
            "positive_source";
        return new Map014TruckImageWitness(state, total, count, iconPaths.Count,
            iconPaths.Distinct(StringComparer.Ordinal).Count());
    }
}
