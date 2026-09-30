using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

internal sealed record ResourceCompletenessBatchDiagnostics(
    int ObservedPointInfos,
    int EnumeratedPointInfos,
    int ResourceCandidateCount,
    int AcceptedResourceCount,
    int RejectedResourceCandidateCount,
    int NonResourcePointCount,
    IReadOnlyDictionary<string, int> RuntimeClassCounts,
    IReadOnlyDictionary<string, int> RawPointTypeCounts,
    IReadOnlyDictionary<string, int> RejectionReasonCounts,
    IReadOnlyDictionary<string, int> AcceptedAoiCounts,
    int ResourceSourceLookupHitCount,
    int ResourceSourceLookupFallbackCount)
{
    internal bool Reconciles =>
        ObservedPointInfos == EnumeratedPointInfos &&
        ObservedPointInfos == ResourceCandidateCount + NonResourcePointCount &&
        ResourceCandidateCount == AcceptedResourceCount + RejectedResourceCandidateCount &&
        RejectedResourceCandidateCount == RejectionReasonCounts.Values.Sum() &&
        AcceptedResourceCount == AcceptedAoiCounts.Values.Sum() &&
        AcceptedResourceCount == ResourceSourceLookupHitCount + ResourceSourceLookupFallbackCount;

    internal static ResourceCompletenessBatchDiagnostics Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Resource completeness diagnostic must be an object.");
        var result = new ResourceCompletenessBatchDiagnostics(
            ReadNonNegativeInt(root, "observedPointInfos"),
            ReadNonNegativeInt(root, "enumeratedPointInfos"),
            ReadNonNegativeInt(root, "resourceCandidateCount"),
            ReadNonNegativeInt(root, "acceptedResourceCount"),
            ReadNonNegativeInt(root, "rejectedResourceCandidateCount"),
            ReadNonNegativeInt(root, "nonResourcePointCount"),
            ReadCounts(root, "runtimeClassCounts"),
            ReadCounts(root, "rawPointTypeCounts"),
            ReadCounts(root, "rejectionReasonCounts"),
            ReadCounts(root, "acceptedAoiCounts"),
            ReadNonNegativeInt(root, "resourceSourceLookupHitCount"),
            ReadNonNegativeInt(root, "resourceSourceLookupFallbackCount"));
        if (!result.Reconciles)
            throw new InvalidDataException("Resource completeness diagnostic counters do not reconcile.");
        return result;
    }

    private static int ReadNonNegativeInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed) || parsed < 0)
            throw new InvalidDataException($"Resource completeness diagnostic field '{name}' is invalid.");
        return parsed;
    }

    private static IReadOnlyDictionary<string, int> ReadCounts(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Resource completeness diagnostic counts '{name}' are missing.");
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!property.Value.TryGetInt32(out int count) || count < 0)
                throw new InvalidDataException($"Resource completeness diagnostic count '{name}.{property.Name}' is invalid.");
            result[property.Name] = count;
        }
        return result;
    }
}

internal sealed record ResourceCompletenessAcceptedRow(
    string RecordKey,
    int ServerId,
    int? PointIndex,
    string? Uuid,
    string? Name,
    string? AllianceName,
    int? Level,
    int? Quality,
    long? Power,
    double? Distance,
    long? ShieldEndTime,
    long UpdatedAt,
    int X,
    int Y,
    int NativeAoiIndex,
    int LogicalBlockIndex,
    int? RawPointType,
    string? ResourceTypeId,
    string? ResourceNameKey,
    bool? IsBlackTile,
    bool? GatherOccupied,
    string ResourceDetailState,
    long? ResourceRemainingAmount,
    long? ResourceFullAmount,
    int? SourceServerId,
    int? WorldId,
    string DataJson);

internal sealed record ResourceCompletenessReport(
    string ScanRunId,
    int ServerId,
    long WorldId,
    int TileWidth,
    int TileHeight,
    int NativeAoiBlockSize,
    int NativeAoiBlockCount,
    int ResponseCount,
    int RawObservedPointInfoOccurrences,
    int RawEnumeratedPointInfoOccurrences,
    int NonResourcePointOccurrences,
    int ResourceCandidateOccurrences,
    int AcceptedResourceOccurrencesBeforeDeduplication,
    int RejectedResourceCandidateOccurrences,
    int DuplicateAcceptedRecordKeyOccurrences,
    int FinalAcceptedUniqueRecords,
    int ResourceSourceLookupHitOccurrences,
    int ResourceSourceLookupFallbackOccurrences,
    int ResourceDetailTargetCount,
    int ResourceDetailRequestCount,
    int ResourceDetailCacheBeforeCount,
    int ResourceDetailSendFailureCount,
    int ResourceDetailReadyCount,
    string? ResourceDetailError,
    IReadOnlyDictionary<string, int> RawRuntimeClassOccurrences,
    IReadOnlyDictionary<string, int> RawPointTypeOccurrences,
    IReadOnlyDictionary<string, int> RejectionReasonOccurrences,
    IReadOnlyDictionary<string, int> LevelDistribution,
    IReadOnlyDictionary<string, int> ResourceNameKeyDistribution,
    IReadOnlyDictionary<string, int> ResourceTypeDistribution,
    IReadOnlyDictionary<string, int> AcceptedRawPointTypeDistribution,
    IReadOnlyDictionary<string, int> BlackTileDistribution,
    IReadOnlyDictionary<string, int> OccupancyDistribution,
    IReadOnlyDictionary<string, int> ResourceDetailDistribution,
    IReadOnlyDictionary<string, int> ServerDistribution,
    IReadOnlyDictionary<string, int> SourceServerDistribution,
    IReadOnlyDictionary<string, int> WorldDistribution,
    int PopulatedNativeAoiBlocks,
    int ZeroResourceNativeAoiBlocks,
    int MinResourcesPerPopulatedNativeAoiBlock,
    int MaxResourcesPerPopulatedNativeAoiBlock,
    IReadOnlyDictionary<string, int> ResourcesByNativeAoiIndex,
    IReadOnlyDictionary<string, int> ResourcesByLogicalBlockIndex,
    int? MinX,
    int? MinY,
    int? MaxX,
    int? MaxY,
    bool RawObservationCountersReconcile,
    bool FinalDeduplicationReconciles,
    bool AcceptedDistributionsReconcile,
    IReadOnlyList<ResourceCompletenessBatchDiagnostics> Batches,
    IReadOnlyList<ResourceCompletenessAcceptedRow> AcceptedRows)
{
    internal string ToStableJson(bool indented = true) => JsonSerializer.Serialize(
        this,
        new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = indented });
}

internal sealed class ResourceCompletenessAccumulator
{
    private readonly List<ResourceCompletenessBatchDiagnostics> batches = [];
    private int duplicateAcceptedRecordKeyOccurrences;

    internal void AddBatch(ResourceCompletenessBatchDiagnostics diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (!diagnostic.Reconciles)
            throw new InvalidDataException("Resource completeness batch does not reconcile.");
        batches.Add(diagnostic);
    }

    internal void RecordAcceptedMerge(bool duplicateRecordKey)
    {
        if (duplicateRecordKey) duplicateAcceptedRecordKeyOccurrences++;
    }

    internal ResourceCompletenessReport Build(
        string scanRunId,
        int serverId,
        long worldId,
        long tileWidth,
        long tileHeight,
        IEnumerable<FirstLivePreparedResource> accepted,
        int resourceDetailTargetCount = 0,
        int resourceDetailRequestCount = 0,
        int resourceDetailCacheBeforeCount = 0,
        int resourceDetailSendFailureCount = 0,
        int resourceDetailReadyCount = 0,
        string? resourceDetailError = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanRunId);
        int width = checked((int)tileWidth);
        int height = checked((int)tileHeight);
        FirstLivePreparedResource[] final = accepted
            .OrderBy(item => item.Import.PointIndex)
            .ThenBy(item => item.Record.RecordKey, StringComparer.Ordinal)
            .ToArray();
        ResourceCompletenessAcceptedRow[] rows = final.Select(item => ToAcceptedRow(item, width)).ToArray();

        int observed = batches.Sum(batch => batch.ObservedPointInfos);
        int enumerated = batches.Sum(batch => batch.EnumeratedPointInfos);
        int candidates = batches.Sum(batch => batch.ResourceCandidateCount);
        int acceptedOccurrences = batches.Sum(batch => batch.AcceptedResourceCount);
        int rejected = batches.Sum(batch => batch.RejectedResourceCandidateCount);
        int nonResource = batches.Sum(batch => batch.NonResourcePointCount);
        int lookupHits = batches.Sum(batch => batch.ResourceSourceLookupHitCount);
        int lookupFallbacks = batches.Sum(batch => batch.ResourceSourceLookupFallbackCount);
        bool rawReconciles = batches.All(batch => batch.Reconciles) &&
            observed == enumerated && observed == candidates + nonResource && candidates == acceptedOccurrences + rejected;
        bool finalReconciles = acceptedOccurrences - duplicateAcceptedRecordKeyOccurrences == rows.Length;

        SortedDictionary<string, int> nativeAoi = CountBy(rows, row => row.NativeAoiIndex.ToString(CultureInfo.InvariantCulture));
        SortedDictionary<string, int> logicalBlocks = CountBy(rows, row => row.LogicalBlockIndex.ToString(CultureInfo.InvariantCulture));
        SortedDictionary<string, int> levels = CountBy(rows, row => Key(row.Level));
        SortedDictionary<string, int> resourceNames = CountBy(rows, row => Key(row.ResourceNameKey));
        SortedDictionary<string, int> resourceTypes = CountBy(rows, row => Key(row.ResourceTypeId));
        SortedDictionary<string, int> acceptedPointTypes = CountBy(rows, row => Key(row.RawPointType));
        SortedDictionary<string, int> blackTiles = CountBy(rows, row => row.IsBlackTile switch { true => "true", false => "false", null => "unknown" });
        SortedDictionary<string, int> occupancy = CountBy(rows, row => row.GatherOccupied switch { true => "occupied", false => "unoccupied", null => "unknown" });
        SortedDictionary<string, int> detail = CountBy(rows, row => row.ResourceDetailState);
        SortedDictionary<string, int> servers = CountBy(rows, row => row.ServerId.ToString(CultureInfo.InvariantCulture));
        SortedDictionary<string, int> sourceServers = CountBy(rows, row => Key(row.SourceServerId));
        SortedDictionary<string, int> worlds = CountBy(rows, row => Key(row.WorldId));
        bool distributionsReconcile = new IReadOnlyDictionary<string, int>[]
        {
            levels, resourceNames, resourceTypes, acceptedPointTypes, blackTiles, occupancy,
            detail, servers, sourceServers, worlds, nativeAoi, logicalBlocks,
        }.All(counts => counts.Values.Sum() == rows.Length);
        int nativeTotal = checked(FastNativeAoiBlockCount(width) * FastNativeAoiBlockCount(height));
        int minPerPopulated = nativeAoi.Count == 0 ? 0 : nativeAoi.Values.Min();
        int maxPerPopulated = nativeAoi.Count == 0 ? 0 : nativeAoi.Values.Max();

        return new ResourceCompletenessReport(
            scanRunId,
            serverId,
            worldId,
            width,
            height,
            10,
            FastNativeAoiBlockCount(width),
            batches.Count,
            observed,
            enumerated,
            nonResource,
            candidates,
            acceptedOccurrences,
            rejected,
            duplicateAcceptedRecordKeyOccurrences,
            rows.Length,
            lookupHits,
            lookupFallbacks,
            resourceDetailTargetCount,
            resourceDetailRequestCount,
            resourceDetailCacheBeforeCount,
            resourceDetailSendFailureCount,
            resourceDetailReadyCount,
            resourceDetailError,
            MergeBatches(batch => batch.RuntimeClassCounts),
            MergeBatches(batch => batch.RawPointTypeCounts),
            MergeBatches(batch => batch.RejectionReasonCounts),
            levels,
            resourceNames,
            resourceTypes,
            acceptedPointTypes,
            blackTiles,
            occupancy,
            detail,
            servers,
            sourceServers,
            worlds,
            nativeAoi.Count,
            Math.Max(nativeTotal - nativeAoi.Count, 0),
            minPerPopulated,
            maxPerPopulated,
            nativeAoi,
            logicalBlocks,
            rows.Length == 0 ? null : rows.Min(row => row.X),
            rows.Length == 0 ? null : rows.Min(row => row.Y),
            rows.Length == 0 ? null : rows.Max(row => row.X),
            rows.Length == 0 ? null : rows.Max(row => row.Y),
            rawReconciles,
            finalReconciles,
            distributionsReconcile,
            batches.ToArray(),
            rows);
    }

    private static ResourceCompletenessAcceptedRow ToAcceptedRow(FirstLivePreparedResource prepared, int tileWidth)
    {
        using JsonDocument document = JsonDocument.Parse(prepared.Record.DataJson);
        JsonElement data = document.RootElement;
        int x = ReadRequiredInt(data, "x");
        int y = ReadRequiredInt(data, "y");
        bool? black = ReadKnownBool(data, "blackTileKnown", "isBlackTile");
        bool? occupied = ReadKnownBool(data, "rebuildGatherOccupancyKnown", "rebuildGatherOccupied");
        long? remaining = ReadOptionalLong(data, "resourceRemainingAmount");
        long? full = ReadOptionalLong(data, "resourceFullAmount");
        bool detailKnown = data.TryGetProperty("resourceDetailKnown", out JsonElement known) && known.ValueKind == JsonValueKind.True;
        string detailState = !detailKnown || remaining is null || full is null
            ? "unknown"
            : remaining.Value == 0
                ? "empty"
                : remaining.Value == full.Value
                    ? "full"
                    : "partial";
        return new ResourceCompletenessAcceptedRow(
            prepared.Record.RecordKey,
            prepared.Record.ServerId,
            prepared.Record.PointIndex,
            prepared.Record.Uuid,
            prepared.Record.Name,
            prepared.Record.AllianceName,
            prepared.Record.Level,
            prepared.Record.Quality,
            prepared.Record.Power,
            prepared.Record.Distance,
            prepared.Record.ShieldEndTime,
            prepared.Record.UpdatedAt,
            x,
            y,
            checked((y / 10) * FastNativeAoiBlockCount(tileWidth) + (x / 10)),
            checked((y / MapScanGeometry.RecoveredBlockSpan) * LogicalBlockCount(tileWidth) + (x / MapScanGeometry.RecoveredBlockSpan)),
            ReadOptionalInt(data, "pointType"),
            ReadScalarText(data, "resourceTypeId"),
            ReadScalarText(data, "resourceNameKey"),
            black,
            occupied,
            detailState,
            remaining,
            full,
            ReadOptionalInt(data, "srcServerId"),
            ReadOptionalInt(data, "worldId"),
            prepared.Record.DataJson);
    }

    private static int FastNativeAoiBlockCount(int dimension) => checked((dimension + 9) / 10);
    private static int LogicalBlockCount(int dimension) => checked((dimension + MapScanGeometry.RecoveredBlockSpan - 1) / MapScanGeometry.RecoveredBlockSpan);

    private SortedDictionary<string, int> MergeBatches(
        Func<ResourceCompletenessBatchDiagnostics, IReadOnlyDictionary<string, int>> selector)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (ResourceCompletenessBatchDiagnostics batch in batches)
            foreach ((string key, int count) in selector(batch)) Add(result, key, count);
        return result;
    }

    private static SortedDictionary<string, int> CountBy<T>(IEnumerable<T> rows, Func<T, string> selector)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (T row in rows) Add(result, selector(row), 1);
        return result;
    }

    private static void Add(IDictionary<string, int> counts, string key, int amount)
    {
        counts[key] = counts.TryGetValue(key, out int current) ? checked(current + amount) : amount;
    }

    private static string Key(object? value) => value switch
    {
        null => "unknown",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "unknown",
        _ => value.ToString() ?? "unknown",
    };

    private static int ReadRequiredInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : throw new InvalidDataException($"Accepted Resource row is missing '{name}'.");

    private static int? ReadOptionalInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : null;

    private static long? ReadOptionalLong(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : null;

    private static string? ReadScalarText(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static bool? ReadKnownBool(JsonElement root, string knownName, string valueName)
    {
        if (!root.TryGetProperty(knownName, out JsonElement known) || known.ValueKind != JsonValueKind.True)
            return null;
        if (!root.TryGetProperty(valueName, out JsonElement value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return null;
        return value.GetBoolean();
    }
}
