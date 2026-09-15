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
    string? Keyword,
    string? Alliance,
    bool WithoutAlliance,
    string? ResourceNameKey,
    string? MonsterNameKey,
    int? TreasureType,
    int? SuppliesType,
    string? Quality,
    string? ItemKey,
    string? CompletionStatus,
    bool PlunderableOnly,
    bool SpecialOnly,
    bool ReindeerOnly,
    int? MinLevel,
    int? MaxLevel,
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
        string? keyword = OptionalString(query, "keyword");
        string? alliance = OptionalString(query, "alliance");
        bool withoutAlliance = OptionalTrue(query, "withoutAlliance");
        string? resourceNameKey = OptionalString(query, "resourceNameKey");
        string? monsterNameKey = OptionalString(query, "monsterNameKey");
        int? treasureType = OptionalNonNegativeInt(query, "treasureType");
        int? suppliesType = OptionalNonNegativeInt(query, "suppliesType");
        string? quality = OptionalString(query, "quality");
        string? itemKey = OptionalString(query, "itemKey");
        string? completionStatus = OptionalString(query, "completionStatus");
        bool plunderableOnly = OptionalTrue(query, "plunderableOnly");
        bool specialOnly = OptionalTrue(query, "specialOnly");
        bool reindeerOnly = OptionalTrue(query, "reindeerOnly");
        int? minLevel = OptionalNonNegativeInt(query, "minLevel");
        int? maxLevel = OptionalNonNegativeInt(query, "maxLevel");
        IReadOnlyList<string> unsupported = CollectUnsupportedFeatures(
            kind,
            query,
            sorts,
            markedOnly,
            treasureType,
            suppliesType);
        return new MapDataQueryOptions(
            kind,
            serverId,
            page,
            pageSize,
            sorts,
            markedOnly,
            keyword,
            alliance,
            withoutAlliance,
            resourceNameKey,
            monsterNameKey,
            treasureType,
            suppliesType,
            quality,
            itemKey,
            completionStatus,
            plunderableOnly,
            specialOnly,
            reindeerOnly,
            minLevel,
            maxLevel,
            unsupported);
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
        bool markedOnly,
        int? treasureType,
        int? suppliesType)
    {
        var unsupported = new List<string>();
        foreach (string name in FilterFields)
        {
            if (!query.TryGetProperty(name, out JsonElement value) || IsNeutral(value)) continue;
            if (IsRecoveredFilter(kind, name, value)) continue;
            unsupported.Add(name);
        }

        // LWB-R6-009: a selected recovered treasure option emits both numeric fields,
        // with exactly one positive dimension and a zero sentinel for the other.
        bool hasTreasureType = query.TryGetProperty("treasureType", out JsonElement treasureValue) && !IsNeutral(treasureValue);
        bool hasSuppliesType = query.TryGetProperty("suppliesType", out JsonElement suppliesValue) && !IsNeutral(suppliesValue);
        bool validTreasureSelection =
            hasTreasureType && hasSuppliesType &&
            ((treasureType > 0 && suppliesType == 0) || (treasureType == 0 && suppliesType > 0));
        if (kind == "treasure" && (hasTreasureType || hasSuppliesType) && !validTreasureSelection)
        {
            unsupported.Add("treasureType");
            unsupported.Add("suppliesType");
        }

        if (markedOnly && kind != "city") unsupported.Add("markedOnly");
        if (sorts.Count != 1 || !string.Equals(sorts[0].SortBy, "updatedAt", StringComparison.Ordinal))
            unsupported.Add("sorts");
        return unsupported.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsRecoveredFilter(string kind, string name, JsonElement value) => name switch
    {
        // LWB-R6-005/006/007: only frontend-emitted forms backed by verified original predicate strings are accepted.
        "keyword" => value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString()),
        "alliance" => kind == "city" && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString()),
        "withoutAlliance" => kind == "city" && value.ValueKind == JsonValueKind.True,
        "resourceNameKey" => kind == "resource" && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString()),
        "monsterNameKey" => kind == "monster" && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString()),
        "treasureType" => kind == "treasure" && IsNonNegativeInteger(value),
        "suppliesType" => kind == "treasure" && IsNonNegativeInteger(value),
        // LWB-R6-013: recovered frontend emits only these five string forms.
        "quality" => kind is "truck" or "railway" or "dispatch" or "ghost" &&
                     value.ValueKind == JsonValueKind.String &&
                     value.GetString() is "n" or "r" or "sr" or "ssr" or "ur",
        "itemKey" => kind is "truck" or "railway" && value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString()),
        // LWB-R6-014: original map_search compares completionTime to one sampled Unix-ms wall clock.
        "completionStatus" => kind is "dispatch" or "ghost" &&
                              value.ValueKind == JsonValueKind.String &&
                              value.GetString() is "pending" or "completed",
        // LWB-R6-004 + R6-014: native has a wider internal branch, but the recovered frontend emits true only for these kinds.
        "plunderableOnly" => kind is "truck" or "railway" or "dispatch" && value.ValueKind == JsonValueKind.True,
        "specialOnly" => kind is "dispatch" or "ghost" && value.ValueKind == JsonValueKind.True,
        "reindeerOnly" => kind == "truck" && value.ValueKind == JsonValueKind.True,
        "minLevel" => kind == "dispatch" && IsPositiveInteger(value),
        "maxLevel" => kind == "dispatch" && IsPositiveInteger(value),
        _ => false,
    };

    private static bool IsNonNegativeInteger(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number >= 0;

    private static bool IsPositiveInteger(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number >= 1;

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

    private static bool OptionalTrue(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static string? OptionalString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        string? result = value.GetString();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    private static int OptionalPositiveInt(JsonElement payload, string name, int fallback)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result < 1)
            throw new BridgeCommandException("INVALID_MAP_QUERY", $"{name} must be a positive integer.");
        return result;
    }

    private static int? OptionalNonNegativeInt(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result < 0)
            throw new BridgeCommandException("INVALID_MAP_QUERY", $"{name} must be a non-negative integer.");
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
