using System.Text.Json;
using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class ResourceCompletenessDiagnosticsChecks
{
    internal static void Run()
    {
        ResourceCompletenessBatchDiagnostics luaEmptyMaps = ResourceCompletenessBatchDiagnostics.Parse(
            JsonDocument.Parse("""
            {
              "observedPointInfos":0,
              "enumeratedPointInfos":0,
              "resourceCandidateCount":0,
              "acceptedResourceCount":0,
              "rejectedResourceCandidateCount":0,
              "nonResourcePointCount":0,
              "runtimeClassCounts":[],
              "rawPointTypeCounts":[],
              "rejectionReasonCounts":[],
              "acceptedAoiCounts":[],
              "resourceSourceLookupHitCount":0,
              "resourceSourceLookupFallbackCount":0
            }
            """).RootElement.Clone());
        Check(luaEmptyMaps.Reconciles && luaEmptyMaps.AcceptedAoiCounts.Count == 0,
            "Lua empty-table count maps must deserialize as empty diagnostic maps");

        ResourceCompletenessBatchDiagnostics batch1 = ParseBatch(new
        {
            observedPointInfos = 8,
            enumeratedPointInfos = 8,
            resourceCandidateCount = 5,
            acceptedResourceCount = 4,
            rejectedResourceCandidateCount = 1,
            nonResourcePointCount = 3,
            runtimeClassCounts = new Dictionary<string, int> { ["OtherPointInfo"] = 4, ["ResPointInfo"] = 4 },
            rawPointTypeCounts = new Dictionary<string, int> { ["1"] = 1, ["26"] = 1, ["6"] = 3, ["7"] = 3 },
            rejectionReasonCounts = new Dictionary<string, int> { ["invalid_or_missing_point_id"] = 1 },
            acceptedAoiCounts = new Dictionary<string, int> { ["0"] = 2, ["101"] = 2 },
            resourceSourceLookupHitCount = 3,
            resourceSourceLookupFallbackCount = 1,
        });
        ResourceCompletenessBatchDiagnostics batch2 = ParseBatch(new
        {
            observedPointInfos = 4,
            enumeratedPointInfos = 4,
            resourceCandidateCount = 3,
            acceptedResourceCount = 2,
            rejectedResourceCandidateCount = 1,
            nonResourcePointCount = 1,
            runtimeClassCounts = new Dictionary<string, int> { ["OtherPointInfo"] = 1, ["ResPointInfo"] = 3 },
            rawPointTypeCounts = new Dictionary<string, int> { ["17"] = 1, ["7"] = 3 },
            rejectionReasonCounts = new Dictionary<string, int> { ["outside_selected_aoi"] = 1 },
            acceptedAoiCounts = new Dictionary<string, int> { ["9999"] = 2 },
            resourceSourceLookupHitCount = 1,
            resourceSourceLookupFallbackCount = 1,
        });
        Check(batch1.Reconciles && batch2.Reconciles, "synthetic Resource completeness batches must reconcile");

        var accumulator = new ResourceCompletenessAccumulator();
        accumulator.AddBatch(batch1);
        accumulator.AddBatch(batch2);

        FirstLivePreparedResource[] accepted =
        [
            Resource(1, 0, 0, level: 1, pointType: 7, resourceTypeId: 2, resourceNameKey: "wood",
                blackKnown: true, black: true, occupancyKnown: true, occupied: true),
            Resource(2, 10, 10, level: 2, pointType: 1, resourceTypeId: 14, resourceNameKey: "food",
                blackKnown: true, black: false, occupancyKnown: true, occupied: false,
                remaining: 100, full: 100),
            Resource(3, 11, 10, level: 2, pointType: 26, resourceTypeId: 14, resourceNameKey: null,
                blackKnown: false, black: null, occupancyKnown: false, occupied: null,
                remaining: 60, full: 100),
            Resource(4, 990, 999, level: 3, pointType: 7, resourceTypeId: 2, resourceNameKey: "wood",
                blackKnown: true, black: false, occupancyKnown: true, occupied: false,
                remaining: 0, full: 100),
            Resource(5, 999, 999, level: null, pointType: 7, resourceTypeId: 99, resourceNameKey: "iron",
                blackKnown: true, black: true, occupancyKnown: false, occupied: null,
                remaining: 80, full: 80),
        ];
        for (int index = 0; index < 5; index++) accumulator.RecordAcceptedMerge(false);
        accumulator.RecordAcceptedMerge(accepted[0], accepted[0]);

        ResourceCompletenessReport report = accumulator.Build(
            "run-completeness", 2212, 0, 1000, 1000, accepted,
            resourceDetailTargetCount: 4,
            resourceDetailRequestCount: 3,
            resourceDetailCacheBeforeCount: 1,
            resourceDetailSendFailureCount: 0,
            resourceDetailReadyCount: 4,
            resourceDetailError: null);
        Check(report.RawObservationCountersReconcile, "raw Resource counters must reconcile");
        Check(report.FinalDeduplicationReconciles, "accepted Resource deduplication must reconcile");
        Check(report.AcceptedDistributionsReconcile, "accepted Resource distributions must reconcile to final unique rows");
        Check(report.RawObservedPointInfoOccurrences == 12 && report.ResourceCandidateOccurrences == 8 &&
              report.AcceptedResourceOccurrencesBeforeDeduplication == 6 && report.RejectedResourceCandidateOccurrences == 2 &&
              report.NonResourcePointOccurrences == 4,
            "raw Resource completeness totals changed");
        Check(report.DuplicateAcceptedRecordKeyOccurrences == 1 && report.FinalAcceptedUniqueRecords == 5,
            "Resource duplicate accounting changed");
        Check(report.DuplicateComparisons.Count == 1 &&
              report.DuplicateComparisons[0].SameSemanticIdentity &&
              report.DuplicateComparisons[0].Classification == "repeated_same_resource_identity",
            "Resource duplicate diagnostics must preserve and classify same-identity collisions");
        Check(report.ResourceDetailTargetCount == 4 && report.ResourceDetailRequestCount == 3 &&
              report.ResourceDetailCacheBeforeCount == 1 && report.ResourceDetailSendFailureCount == 0 &&
              report.ResourceDetailReadyCount == 4 && report.ResourceDetailError is null,
            "Resource detail accounting changed");
        Check(Count(report.RejectionReasonOccurrences, "invalid_or_missing_point_id") == 1 &&
              Count(report.RejectionReasonOccurrences, "outside_selected_aoi") == 1 &&
              report.RejectionReasonOccurrences.Values.Sum() == 2,
            "Resource rejection reasons must not disappear");
        Check(Count(report.BlackTileDistribution, "true") == 2 && Count(report.BlackTileDistribution, "false") == 2 &&
              Count(report.BlackTileDistribution, "unknown") == 1,
            "Resource black-tile tri-state distribution changed");
        Check(Count(report.OccupancyDistribution, "occupied") == 1 && Count(report.OccupancyDistribution, "unoccupied") == 2 &&
              Count(report.OccupancyDistribution, "unknown") == 2,
            "Resource occupancy tri-state distribution changed");
        Check(Count(report.ResourceDetailDistribution, "full") == 2 && Count(report.ResourceDetailDistribution, "partial") == 1 &&
              Count(report.ResourceDetailDistribution, "empty") == 1 && Count(report.ResourceDetailDistribution, "unknown") == 1,
            "Resource detail distribution changed");
        Check(Count(report.LevelDistribution, "1") == 1 && Count(report.LevelDistribution, "2") == 2 &&
              Count(report.LevelDistribution, "3") == 1 && Count(report.LevelDistribution, "unknown") == 1,
            "Resource level distribution changed");
        Check(report.PopulatedNativeAoiBlocks == 3 && report.ZeroResourceNativeAoiBlocks == 9997 &&
              report.MinResourcesPerPopulatedNativeAoiBlock == 1 && report.MaxResourcesPerPopulatedNativeAoiBlock == 2,
            "Resource native-AOI spatial distribution changed");
        Check(Count(report.ResourcesByNativeAoiIndex, "0") == 1 && Count(report.ResourcesByNativeAoiIndex, "101") == 2 &&
              Count(report.ResourcesByNativeAoiIndex, "9999") == 2,
            "Resource native-AOI count grid changed");
        Check(report.MinX == 0 && report.MinY == 0 && report.MaxX == 999 && report.MaxY == 999,
            "Resource coordinate bounding box changed");
        Check(report.AcceptedRows.Count == 5 && report.AcceptedRows.Select(row => row.PointIndex).SequenceEqual(new int?[] { 1, 2, 3, 4, 5 }),
            "lossless accepted Resource rows must use stable point order");

        string first = report.ToStableJson();
        string second = report.ToStableJson();
        Check(string.Equals(first, second, StringComparison.Ordinal), "Resource completeness serialization must be stable");
        using JsonDocument roundTrip = JsonDocument.Parse(first);
        Check(roundTrip.RootElement.GetProperty("acceptedRows").GetArrayLength() == 5 &&
              roundTrip.RootElement.GetProperty("finalAcceptedUniqueRecords").GetInt32() == 5,
            "Resource completeness stable JSON lost accepted rows");

        bool rejectedInvalid = false;
        try
        {
            _ = ParseBatch(new
            {
                observedPointInfos = 1,
                enumeratedPointInfos = 1,
                resourceCandidateCount = 1,
                acceptedResourceCount = 0,
                rejectedResourceCandidateCount = 1,
                nonResourcePointCount = 0,
                runtimeClassCounts = new Dictionary<string, int> { ["ResPointInfo"] = 1 },
                rawPointTypeCounts = new Dictionary<string, int> { ["7"] = 1 },
                rejectionReasonCounts = new Dictionary<string, int>(),
                acceptedAoiCounts = new Dictionary<string, int>(),
                resourceSourceLookupHitCount = 0,
                resourceSourceLookupFallbackCount = 0,
            });
        }
        catch (InvalidDataException)
        {
            rejectedInvalid = true;
        }
        Check(rejectedInvalid, "unattributed Resource candidate rejection must fail deterministic accounting");
    }

    private static ResourceCompletenessBatchDiagnostics ParseBatch(object value)
    {
        JsonElement json = JsonSerializer.SerializeToElement(value, JsonOptions.Default);
        return ResourceCompletenessBatchDiagnostics.Parse(json);
    }

    private static FirstLivePreparedResource Resource(
        int pointId,
        int x,
        int y,
        int? level,
        int pointType,
        int resourceTypeId,
        string? resourceNameKey,
        bool blackKnown,
        bool? black,
        bool occupancyKnown,
        bool? occupied,
        long? remaining = null,
        long? full = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["kind"] = "resource",
            ["sourceKind"] = "resource_point",
            ["recordKey"] = pointId.ToString(),
            ["pointIndex"] = pointId,
            ["pointId"] = pointId,
            ["pointType"] = pointType,
            ["serverId"] = 2212,
            ["srcServerId"] = 2212,
            ["worldId"] = 0,
            ["x"] = x,
            ["y"] = y,
            ["level"] = level,
            ["resourceTypeId"] = resourceTypeId,
            ["resourceNameKey"] = resourceNameKey,
            ["blackTileKnown"] = blackKnown,
            ["isBlackTile"] = black,
            ["rebuildGatherOccupancyKnown"] = occupancyKnown,
            ["rebuildGatherOccupied"] = occupied,
        };
        if (remaining.HasValue && full.HasValue)
        {
            data["resourceDetailKnown"] = true;
            data["resourceRemainingAmount"] = remaining.Value;
            data["resourceFullAmount"] = full.Value;
            data["resourceFull"] = remaining.Value == full.Value;
        }
        string dataJson = JsonSerializer.Serialize(data, JsonOptions.Default);
        long updatedAt = 1_790_000_000_000L + pointId;
        var record = new MapStoredRecord(
            "resource", 2212, pointId.ToString(), pointId, null, null, null, level,
            null, null, null, null, updatedAt, dataJson);
        var import = new FirstLiveResultImport(
            2212, pointId.ToString(), pointId, x, y, level, updatedAt,
            "synthetic", "synthetic.json", "lwbridge-live-resource-probe-2", null, dataJson);
        return new FirstLivePreparedResource(import, record);
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out int value) ? value : 0;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
