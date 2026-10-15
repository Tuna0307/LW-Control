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
        ObservedPointInfos == RuntimeClassCounts.Values.Sum() &&
        ObservedPointInfos == RawPointTypeCounts.Values.Sum() &&
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
        if (!root.TryGetProperty(name, out JsonElement value))
            throw new InvalidDataException($"Resource completeness diagnostic counts '{name}' are missing.");
        if (value.ValueKind == JsonValueKind.Array)
        {
            if (value.GetArrayLength() == 0)
                return new SortedDictionary<string, int>(StringComparer.Ordinal);
            throw new InvalidDataException($"Resource completeness diagnostic counts '{name}' used a non-empty array.");
        }
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Resource completeness diagnostic counts '{name}' are invalid.");
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

internal sealed record ResourceDuplicateObservation(
    string RecordKey,
    int PointIndex,
    int ServerId,
    int X,
    int Y,
    int? Level,
    long CapturedAtUnixMilliseconds,
    long UpdatedAt,
    string? RuntimeClass,
    int? RawPointType,
    string? ResourceTypeId,
    int? ResourceConfigId,
    int? SourceServerId,
    int? WorldId,
    bool? IsBlackTile,
    bool? GatherOccupied,
    string DataJson);

internal sealed record ResourceDuplicateComparison(
    string RecordKey,
    string Classification,
    bool SameSemanticIdentity,
    ResourceDuplicateObservation Previous,
    ResourceDuplicateObservation Current);

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
    IReadOnlyList<ResourceDuplicateComparison> DuplicateComparisons,
    IReadOnlyList<ResourceCompletenessBatchDiagnostics> Batches,
    IReadOnlyList<ResourceCompletenessAcceptedRow> AcceptedRows)
{
    internal string ToStableJson(bool indented = true) => JsonSerializer.Serialize(
        this,
        new JsonSerializerOptions(JsonOptions.Default) { WriteIndented = indented });

    // Source accounting is independent of completed AOI geometry. Finishing
    // all point AOIs cannot turn a duplicate physical Resource identity, an
    // incomplete candidate classification, or a partial merge into a safe
    // published map. This gate does not claim same-time population recall.
    internal void RequireReconciledBeforePublication(long acquiredResponseCount, int stagedResourceCount)
    {
        if (ResponseCount != acquiredResponseCount ||
            !RawObservationCountersReconcile || !FinalDeduplicationReconciles ||
            !AcceptedDistributionsReconcile ||
            FinalAcceptedUniqueRecords != stagedResourceCount ||
            FinalAcceptedUniqueRecords != AcceptedRows.Count ||
            Batches.Count != ResponseCount ||
            Batches.Any(batch => !batch.Reconciles) ||
            RawObservedPointInfoOccurrences != Batches.Sum(batch => batch.ObservedPointInfos) ||
            RawEnumeratedPointInfoOccurrences != Batches.Sum(batch => batch.EnumeratedPointInfos) ||
            ResourceCandidateOccurrences != Batches.Sum(batch => batch.ResourceCandidateCount) ||
            AcceptedResourceOccurrencesBeforeDeduplication != Batches.Sum(batch => batch.AcceptedResourceCount) ||
            RejectedResourceCandidateOccurrences != Batches.Sum(batch => batch.RejectedResourceCandidateCount) ||
            NonResourcePointOccurrences != Batches.Sum(batch => batch.NonResourcePointCount) ||
            RawRuntimeClassOccurrences.Values.Sum() != RawEnumeratedPointInfoOccurrences ||
            RawPointTypeOccurrences.Values.Sum() != RawEnumeratedPointInfoOccurrences ||
            RejectionReasonOccurrences.Values.Sum() != RejectedResourceCandidateOccurrences ||
            ResourceSourceLookupHitOccurrences + ResourceSourceLookupFallbackOccurrences != AcceptedResourceOccurrencesBeforeDeduplication ||
            RejectionReasonOccurrences.Any(reason => reason.Value > 0 && reason.Key != "outside_selected_aoi") ||
            DuplicateAcceptedRecordKeyOccurrences != DuplicateComparisons.Count ||
            AcceptedResourceOccurrencesBeforeDeduplication - DuplicateAcceptedRecordKeyOccurrences != FinalAcceptedUniqueRecords ||
            DuplicateComparisons.Any(duplicate => !ValidDuplicate(duplicate)) ||
            TileWidth <= 0 || TileHeight <= 0 || NativeAoiBlockSize != 10 ||
            NativeAoiBlockCount != (TileWidth + 9L) / 10 ||
            AcceptedRows.Any(row => !ValidSourceRow(row)) ||
            AcceptedRows.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count() != AcceptedRows.Count ||
            AcceptedRows.Select(row => row.PointIndex).Distinct().Count() != AcceptedRows.Count ||
            ResourcesByNativeAoiIndex.Keys.Any(key => !int.TryParse(key, NumberStyles.None,
                CultureInfo.InvariantCulture, out int index) || index < 0 ||
                index >= ((TileWidth + 9L) / 10) * ((TileHeight + 9L) / 10)))
            throw new InvalidDataException(
                "Resource native source accounting, provenance, stable identity or staging did not reconcile.");
    }

    private bool ValidDuplicate(ResourceDuplicateComparison duplicate)
    {
        ResourceDuplicateObservation previous = duplicate.Previous;
        ResourceDuplicateObservation current = duplicate.Current;
        return duplicate.SameSemanticIdentity &&
            duplicate.Classification == "repeated_same_resource_identity" &&
            duplicate.RecordKey == previous.RecordKey &&
            duplicate.RecordKey == current.RecordKey &&
            previous.RecordKey == previous.PointIndex.ToString(CultureInfo.InvariantCulture) &&
            current.RecordKey == current.PointIndex.ToString(CultureInfo.InvariantCulture) &&
            previous.ServerId == ServerId && current.ServerId == ServerId &&
            previous.SourceServerId == ServerId && current.SourceServerId == ServerId &&
            previous.WorldId == WorldId && current.WorldId == WorldId &&
            previous.PointIndex == current.PointIndex && previous.X == current.X && previous.Y == current.Y &&
            previous.RuntimeClass == current.RuntimeClass &&
            previous.RawPointType == current.RawPointType &&
            previous.ResourceTypeId == current.ResourceTypeId &&
            previous.ResourceConfigId == current.ResourceConfigId &&
            previous.CapturedAtUnixMilliseconds > 0 &&
            previous.UpdatedAt == previous.CapturedAtUnixMilliseconds &&
            current.UpdatedAt == current.CapturedAtUnixMilliseconds &&
            current.CapturedAtUnixMilliseconds >= previous.CapturedAtUnixMilliseconds;
    }

    private bool ValidSourceRow(ResourceCompletenessAcceptedRow row)
    {
        if (row.ServerId != ServerId || row.SourceServerId != ServerId || row.WorldId != WorldId ||
            row.PointIndex is not int point || point < 1 || row.UpdatedAt <= 0 ||
            row.X < 0 || row.X >= TileWidth || row.Y < 0 || row.Y >= TileHeight ||
            row.RecordKey != point.ToString(CultureInfo.InvariantCulture) ||
            point != (long)row.Y * TileWidth + row.X + 1 ||
            row.NativeAoiIndex != (row.Y / 10) * NativeAoiBlockCount + (row.X / 10) ||
            row.LogicalBlockIndex != (row.Y / MapScanGeometry.RecoveredBlockSpan) *
                ((TileWidth + MapScanGeometry.RecoveredBlockSpan - 1) / MapScanGeometry.RecoveredBlockSpan) +
                row.X / MapScanGeometry.RecoveredBlockSpan ||
            row.ResourceDetailState is not ("unknown" or "empty" or "partial" or "full") ||
            (row.ResourceRemainingAmount.HasValue != row.ResourceFullAmount.HasValue) ||
            (row.ResourceRemainingAmount is long remaining && row.ResourceFullAmount is long full &&
                (full <= 0 || remaining < 0 || remaining > full ||
                 row.ResourceDetailState != (remaining == 0 ? "empty" : remaining == full ? "full" : "partial"))))
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(row.DataJson);
            JsonElement raw = document.RootElement;
            if (raw.ValueKind != JsonValueKind.Object ||
                !SameInt(raw, "x", row.X) || !SameInt(raw, "y", row.Y) ||
                !SameInt(raw, "srcServerId", ServerId) || !SameInt(raw, "worldId", WorldId) ||
                !SameOptionalInt(raw, "serverId", ServerId) ||
                !SameOptionalInt(raw, "pointId", point) ||
                !SameOptionalInt(raw, "pointIndex", point) ||
                !SameOptionalString(raw, "recordKey", row.RecordKey) ||
                !SameOptionalInt(raw, "updatedAt", row.UpdatedAt) ||
                !SameOptionalString(raw, "kind", "resource") ||
                !SameOptionalString(raw, "sourceKind", "resource_point") ||
                !SameOptionalString(raw, "source",
                    "WorldPointManager._pointInfos+GatherResource+SceneUtils.IsInBlackRange") ||
                !SameOptionalString(raw, "resourceSourceType", "ResPointInfo") ||
                !SameOptionalInt(raw, "pointType", row.RawPointType) ||
                !SameOptionalBoolean(raw, "rebuildGatherOccupied", row.GatherOccupied) ||
                !SameOptionalBoolean(raw, "isBlackTile", row.IsBlackTile) ||
                !SameOptionalInt(raw, "resourceRemainingAmount", row.ResourceRemainingAmount) ||
                !SameOptionalInt(raw, "resourceFullAmount", row.ResourceFullAmount))
                return false;

            if (raw.TryGetProperty("gatherOccupancyKnown", out JsonElement occupancyKnown) &&
                occupancyKnown.ValueKind == JsonValueKind.True &&
                !SameOptionalBoolean(raw, "gatherOccupied", row.GatherOccupied))
                return false;
            if (raw.TryGetProperty("resourceDetailKnown", out JsonElement detailKnown) &&
                detailKnown.ValueKind == JsonValueKind.True &&
                (!row.ResourceRemainingAmount.HasValue || !row.ResourceFullAmount.HasValue))
                return false;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool SameInt(JsonElement raw, string name, long value) =>
        raw.TryGetProperty(name, out JsonElement item) && item.TryGetInt64(out long actual) && actual == value;

    // Older controlled boundary fixtures omit fields subsequently added by the
    // native importer. Every field present in a real normalized point is checked.
    private static bool SameOptionalInt(JsonElement raw, string name, long? value) =>
        !raw.TryGetProperty(name, out _) || value.HasValue && SameInt(raw, name, value.Value);

    private static bool SameOptionalString(JsonElement raw, string name, string expected) =>
        !raw.TryGetProperty(name, out JsonElement item) ||
        item.ValueKind == JsonValueKind.String && item.GetString() == expected;

    private static bool SameOptionalBoolean(JsonElement raw, string name, bool? expected) =>
        !raw.TryGetProperty(name, out JsonElement item) ||
        expected.HasValue && item.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        item.GetBoolean() == expected.Value;
}

internal sealed class ResourceCompletenessAccumulator
{
    private readonly List<ResourceCompletenessBatchDiagnostics> batches = [];
    private readonly List<ResourceDuplicateComparison> duplicateComparisons = [];
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

    internal void RecordAcceptedMerge(
        FirstLivePreparedResource current,
        FirstLivePreparedResource? previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        bool duplicateRecordKey = previous is not null;
        RecordAcceptedMerge(duplicateRecordKey);
        if (!duplicateRecordKey) return;

        ResourceDuplicateObservation prior = ToDuplicateObservation(previous!);
        ResourceDuplicateObservation next = ToDuplicateObservation(current);
        bool sameSemanticIdentity =
            prior.PointIndex == next.PointIndex &&
            prior.ServerId == next.ServerId &&
            prior.X == next.X &&
            prior.Y == next.Y &&
            prior.RuntimeClass == next.RuntimeClass &&
            prior.RawPointType == next.RawPointType &&
            prior.ResourceTypeId == next.ResourceTypeId &&
            prior.ResourceConfigId == next.ResourceConfigId &&
            prior.SourceServerId == next.SourceServerId &&
            prior.WorldId == next.WorldId;
        duplicateComparisons.Add(new ResourceDuplicateComparison(
            current.Record.RecordKey,
            sameSemanticIdentity ? "repeated_same_resource_identity" : "conflicting_same_record_key",
            sameSemanticIdentity,
            prior,
            next));
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
            duplicateComparisons.ToArray(),
            batches.ToArray(),
            rows);
    }

    private static ResourceDuplicateObservation ToDuplicateObservation(FirstLivePreparedResource item)
    {
        string? runtimeClass = null;
        int? rawPointType = null;
        string? resourceTypeId = null;
        int? resourceConfigId = null;
        int? sourceServerId = null;
        int? worldId = null;
        bool? isBlackTile = null;
        bool? gatherOccupied = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(item.Record.DataJson);
            JsonElement root = document.RootElement;
            runtimeClass = ReadOptionalString(root, "runtimeClass");
            rawPointType = ReadOptionalInt(root, "pointType");
            resourceTypeId = ReadOptionalScalar(root, "resourceTypeId");
            resourceConfigId = ReadOptionalInt(root, "resourceConfigId");
            sourceServerId = ReadOptionalInt(root, "srcServerId");
            worldId = ReadOptionalInt(root, "worldId");
            isBlackTile = ReadOptionalBool(root, "isBlackTile");
            gatherOccupied = ReadOptionalBool(root, "gatherOccupied");
        }
        catch (JsonException)
        {
            // Keep the original DataJson below so malformed diagnostic detail is
            // preserved rather than silently invented.
        }
        return new ResourceDuplicateObservation(
            item.Record.RecordKey,
            item.Import.PointIndex,
            item.Import.ServerId,
            item.Import.X,
            item.Import.Y,
            item.Import.Level,
            item.Import.CapturedAtUnixMilliseconds,
            item.Record.UpdatedAt,
            runtimeClass,
            rawPointType,
            resourceTypeId,
            resourceConfigId,
            sourceServerId,
            worldId,
            isBlackTile,
            gatherOccupied,
            item.Record.DataJson);
    }

    private static string? ReadOptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadOptionalBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    private static string? ReadOptionalScalar(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
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
