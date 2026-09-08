using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record MapDataSort(string SortBy, string SortOrder);

internal sealed record MapDataQueryOptions(
    string Kind,
    int ServerId,
    int Page,
    int PageSize,
    IReadOnlyList<MapDataSort> Sorts,
    bool MarkedOnly,
    IReadOnlyList<string> UnsupportedFeatures);

internal static class MapDataQueryContract
{
    public const int RecoveredPageSize = 50;

    private static readonly string[] FilterFields =
    [
        "keyword",
        "resourceNameKey",
        "monsterNameKey",
        "treasureType",
        "suppliesType",
        "alliance",
        "withoutAlliance",
        "quality",
        "specialOnly",
        "reindeerOnly",
        "itemKey",
        "completionStatus",
        "plunderableOnly",
        "includeForeignRadarTreasures",
        "luckyFirst",
        "viewerUid",
        "viewerAllianceId",
        "minLevel",
        "maxLevel",
    ];

    private static readonly HashSet<string> AllowedKinds =
        new(MapScanContract.AllTypes, StringComparer.Ordinal);

    public static MapDataQueryOptions NormalizeSearch(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_PAYLOAD", "map_search payload must be an object.");

        string kind = RequiredString(payload, "kind", "INVALID_MAP_KIND");
        if (!AllowedKinds.Contains(kind))
            throw new BridgeCommandException("INVALID_MAP_KIND", $"Unknown map data kind '{kind}'.");

        if (!payload.TryGetProperty("query", out JsonElement query) || query.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_MAP_QUERY", "map_search query must be an object.");

        int serverId = RequiredServerId(query);
        int page = OptionalPositiveInt(query, "page", 1);
        int pageSize = OptionalPositiveInt(query, "pageSize", RecoveredPageSize);
        IReadOnlyList<MapDataSort> sorts = NormalizeSorts(query);
        bool markedOnly = OptionalBoolean(query, "markedOnly", false);
        IReadOnlyList<string> unsupported = CollectUnsupportedFeatures(kind, query, sorts, markedOnly);
        return new MapDataQueryOptions(kind, serverId, page, pageSize, sorts, markedOnly, unsupported);
    }

    public static void RequireRecoveredIndexedSearch(MapDataQueryOptions options)
    {
        if (options.UnsupportedFeatures.Count == 0) return;
        throw new BridgeCommandException(
            "MAP_QUERY_UNRECOVERED",
            "The requested Map Data filter/sort has not been recovered sufficiently for production use.",
            new { options.Kind, unsupported = options.UnsupportedFeatures });
    }

    public static int RequiredServerId(JsonElement payload)
    {
        if (!payload.TryGetProperty("serverId", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int serverId) ||
            serverId < 1 || serverId > 99999)
            throw new BridgeCommandException("INVALID_SERVER_ID", "serverId must be an integer from 1 through 99999.");
        return serverId;
    }

    private static IReadOnlyList<MapDataSort> NormalizeSorts(JsonElement query)
    {
        if (!query.TryGetProperty("sorts", out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new[] { new MapDataSort("updatedAt", "desc") };
        if (value.ValueKind != JsonValueKind.Array)
            throw new BridgeCommandException("INVALID_MAP_QUERY", "sorts must be an array.");

        var sorts = new List<MapDataSort>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new BridgeCommandException("INVALID_MAP_QUERY", "Each sort must be an object.");
            string sortBy = RequiredString(item, "sortBy", "INVALID_MAP_QUERY");
            string sortOrder = RequiredString(item, "sortOrder", "INVALID_MAP_QUERY");
            if (sortOrder is not ("asc" or "desc"))
                throw new BridgeCommandException("INVALID_MAP_QUERY", "sortOrder must be asc or desc.");
            sorts.Add(new MapDataSort(sortBy, sortOrder));
        }

        return sorts.Count == 0
            ? new[] { new MapDataSort("updatedAt", "desc") }
            : sorts;
    }

    private static IReadOnlyList<string> CollectUnsupportedFeatures(
        string kind,
        JsonElement query,
        IReadOnlyList<MapDataSort> sorts,
        bool markedOnly)
    {
        var unsupported = new List<string>();
        foreach (string name in FilterFields)
        {
            if (!query.TryGetProperty(name, out JsonElement value) || IsNeutral(value)) continue;
            unsupported.Add(name);
        }

        if (markedOnly && kind != "city") unsupported.Add("markedOnly");
        if (sorts.Count != 1 || !string.Equals(sorts[0].SortBy, "updatedAt", StringComparison.Ordinal))
            unsupported.Add("sorts");
        return unsupported.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsNeutral(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => true,
        JsonValueKind.String => string.IsNullOrEmpty(value.GetString()),
        _ => false,
    };

    private static bool OptionalBoolean(JsonElement payload, string name, bool fallback)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new BridgeCommandException("INVALID_MAP_QUERY", $"{name} must be a boolean.");
        return value.GetBoolean();
    }

    private static int OptionalPositiveInt(JsonElement payload, string name, int fallback)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result < 1)
            throw new BridgeCommandException("INVALID_MAP_QUERY", $"{name} must be a positive integer.");
        return result;
    }

    private static string RequiredString(JsonElement payload, string name, string code)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new BridgeCommandException(code, $"{name} must be a non-empty string.");
        return value.GetString()!;
    }
}
