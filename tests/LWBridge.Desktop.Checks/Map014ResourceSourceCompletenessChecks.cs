using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

internal static class Map014ResourceSourceCompletenessChecks
{
    internal static void Run()
    {
        FirstLivePreparedResource resource = ResourceAt(0, 999);
        ResourceCompletenessReport good = Build(resource);
        good.RequireReconciledBeforePublication(1, 1);

        int inverses = 0;
        void Reject(ResourceCompletenessReport candidate, string label)
        {
            try { candidate.RequireReconciledBeforePublication(candidate.ResponseCount, candidate.FinalAcceptedUniqueRecords); }
            catch (InvalidDataException) { inverses++; return; }
            throw new Exception($"Resource publication incorrectly accepted {label}.");
        }
        ResourceCompletenessReport Rewrite(Func<ResourceCompletenessAcceptedRow, ResourceCompletenessAcceptedRow> update) =>
            good with { AcceptedRows = good.AcceptedRows.Select(update).ToArray() };
        ResourceCompletenessReport RewriteJson(string key, object? replacement) => Rewrite(row =>
        {
            JsonObject data = JsonNode.Parse(row.DataJson)!.AsObject();
            data[key] = JsonSerializer.SerializeToNode(replacement);
            return row with { DataJson = data.ToJsonString() };
        });

        Reject(Rewrite(row => row with { SourceServerId = 2213 }), "foreign native source server");
        Reject(Rewrite(row => row with { WorldId = 1 }), "foreign world");
        Reject(Rewrite(row => row with { ServerId = 2213 }), "foreign stored server");
        Reject(Rewrite(row => row with { RecordKey = "not-the-native-index" }), "fabricated point key");
        Reject(Rewrite(row => row with { PointIndex = 1 }), "colliding source point identity");
        Reject(Rewrite(row => row with { X = 1000 }), "coordinate seam overflow");
        Reject(Rewrite(row => row with { NativeAoiIndex = 0 }), "stale source AOI index");
        Reject(Rewrite(row => row with { UpdatedAt = 5 }), "stored stale observation clock");
        Reject(RewriteJson("updatedAt", 5), "raw stale observation clock");
        Reject(RewriteJson("pointId", 1234), "raw mismatched point identity");
        Reject(RewriteJson("kind", "city"), "mislabelled raw row kind");
        Reject(RewriteJson("source", "invented native provenance"), "fabricated raw source");
        Reject(RewriteJson("gatherOccupied", true), "contradictory occupied state");
        Reject(RewriteJson("resourceRemainingAmount", 300), "impossible hydrated amount");
        Reject(good with { RawRuntimeClassOccurrences = new Dictionary<string, int> { ["ResPointInfo"] = 0 } },
            "raw enumeration histogram mismatch");

        ResourceCompletenessBatchDiagnostics droppedCandidate = Batch(
            observed: 2, accepted: 1, rejected: 1, rejectionReason: "invalid_or_missing_point_id");
        Reject(Build(resource, droppedCandidate), "silently rejected invalid Resource identity");

        // The raw manager normally holds points outside the current AOI. They
        // count in source enumeration but are correctly excluded from this reply.
        Build(resource, Batch(observed: 2, accepted: 1, rejected: 1,
            rejectionReason: "outside_selected_aoi")).RequireReconciledBeforePublication(1, 1);

        // A live alliance WorldAllianceCollectResource (type 26) is not a ResPointInfo: it is a counted candidate
        // that is rejected with a NAMED reason (never silently skipped), and may not abort the scan.
        Build(resource, Batch(observed: 2, accepted: 1, rejected: 1,
            rejectionReason: "not_res_point_info")).RequireReconciledBeforePublication(1, 1);

        var duplicates = new ResourceCompletenessAccumulator();
        duplicates.AddBatch(Batch(observed: 2, accepted: 2));
        duplicates.RecordAcceptedMerge(resource, null);
        duplicates.RecordAcceptedMerge(resource, resource);
        ResourceCompletenessReport repeated = duplicates.Build("controlled-resource-repeat", 2212, 0, 1000, 1000,
            [resource]);
        repeated.RequireReconciledBeforePublication(1, 1);
        Reject(repeated with { DuplicateComparisons = [] }, "unaccounted duplicate identity comparison");

        if (inverses != 17)
            throw new Exception($"Unexpected Resource inverse count {inverses}.");
        Console.WriteLine($"MAP014_RESOURCE_SOURCE_COMPLETENESS_CHECKS_OK inverses={inverses} native=false");
    }

    private static ResourceCompletenessBatchDiagnostics Batch(int observed = 1, int accepted = 1,
        int rejected = 0, string? rejectionReason = null) => new(
        observed, observed, observed, accepted, rejected, 0,
        new Dictionary<string, int> { ["ResPointInfo"] = observed },
        new Dictionary<string, int> { ["7"] = observed },
        rejectionReason is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [rejectionReason] = rejected },
        new Dictionary<string, int> { ["9900"] = accepted }, accepted, 0);

    private static ResourceCompletenessReport Build(FirstLivePreparedResource resource,
        ResourceCompletenessBatchDiagnostics? diagnostic = null)
    {
        var accumulator = new ResourceCompletenessAccumulator();
        accumulator.AddBatch(diagnostic ?? Batch());
        accumulator.RecordAcceptedMerge(resource, null);
        return accumulator.Build("controlled-resource-source", 2212, 0, 1000, 1000, [resource]);
    }

    private static FirstLivePreparedResource ResourceAt(int x, int y)
    {
        int id = checked(y * 1000 + x + 1);
        const long now = 1791917011000;
        string json = JsonSerializer.Serialize(new
        {
            kind = "resource", sourceKind = "resource_point",
            source = "WorldPointManager._pointInfos+GatherResource+SceneUtils.IsInBlackRange",
            resourceSourceType = "ResPointInfo", runtimeClass = "ResPointInfo",
            pointType = 7, pointId = id, pointIndex = id, recordKey = id.ToString(),
            serverId = 2212, srcServerId = 2212, worldId = 0, x, y,
            resourceTypeId = 14, resourceNameKey = "100317", resourceConfigId = 20106,
            resourceMaxAmount = 216000, level = 6, updatedAt = now,
            gatherOccupancyKnown = true, gatherOccupied = false,
            rebuildGatherOccupancyKnown = true, rebuildGatherOccupied = false,
            blackTileKnown = true, isBlackTile = false,
            resourceDetailKnown = true, resourceRemainingAmount = 216000,
            resourceFullAmount = 216000, resourceFull = true,
            resourceCapacityMatchesConfig = true,
        });
        var record = new MapStoredRecord("resource", 2212, id.ToString(), id, null,
            "resource", null, 6, null, null, null, null, now, json);
        var import = new FirstLiveResultImport(2212, id.ToString(), id, x, y, 6, now,
            "controlled-hash", "synthetic-not-native", null, null, json);
        return new FirstLivePreparedResource(import, record);
    }
}
