using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LWBridge.Desktop;

// Synthetic source-contract inverses. Call Run() from the main checks runner;
// there are deliberately no native/game actions in this test class.
internal static class Map014ResourceNativeSourceChecks
{
    internal static void Run()
    {
        FirstLivePreparedResource edge = ResourceAt(0, 999);
        FirstLivePreparedResource opposite = ResourceAt(999, 0);
        var batch = Batch(2, new Dictionary<string, int> { ["9900"] = 1, ["99"] = 1 });
        var footprint = new HashSet<int> { 9900, 99 };
        CurrentClientMapBlockSource.RequireResourceNativeSourceCohort(
            [edge, opposite], batch, footprint, 2212, 0);

        int nativeInverses = 0;
        void RejectNative(string label, IReadOnlyList<FirstLivePreparedResource>? rows = null,
            ResourceCompletenessBatchDiagnostics? counters = null,
            IReadOnlySet<int>? aoi = null, int server = 2212, int world = 0)
        {
            try
            {
                CurrentClientMapBlockSource.RequireResourceNativeSourceCohort(
                    rows ?? [edge, opposite], counters ?? batch, aoi ?? footprint, server, world);
            }
            catch (InvalidDataException) { nativeInverses++; return; }
            throw new Exception("Native Resource source wrongly accepted " + label);
        }
        RejectNative("AOI histogram drift with valid global count", counters:
            Batch(2, new Dictionary<string, int> { ["99"] = 2 }));
        RejectNative("skipped invalid native candidate", counters:
            Batch(2, new Dictionary<string, int> { ["9900"] = 1, ["99"] = 1 },
                rejected: 1, reason: "invalid_or_missing_point_id"));
        RejectNative("AOI outside returned source footprint", aoi: new HashSet<int> { 9900 });
        RejectNative("duplicate point within one native reply", rows: [edge, edge],
            counters: Batch(2, new Dictionary<string, int> { ["9900"] = 2 }));
        RejectNative("foreign imported server", rows: [edge with
        {
            Import = edge.Import with { ServerId = 2213 },
        }, opposite]);
        RejectNative("foreign native source server", rows: [Edit(edge, "srcServerId", 2213), opposite]);
        RejectNative("foreign world", rows: [Edit(edge, "worldId", 1), opposite]);
        RejectNative("wrong source spatial identity", rows: [Edit(edge, "pointId", 42), opposite]);
        RejectNative("forged source label", rows: [Edit(edge, "source", "guess"), opposite]);
        RejectNative("missing source label", rows: [Remove(edge, "source"), opposite]);
        RejectNative("raw observation timestamp changed", rows: [Edit(edge, "updatedAt", 1), opposite]);
        RejectNative("native and rebuilt occupancy disagree", rows: [Edit(edge, "gatherOccupied", true), opposite]);
        RejectNative("unidentified native runtime class", rows: [Remove(edge, "runtimeClass"), opposite]);
        RejectNative("out of range native location", rows: [edge with
        {
            Import = edge.Import with { X = 1000 },
        }, opposite]);

        FirstLivePreparedResource idle = ResourceAt(0, 999);
        var current = new Dictionary<string, FirstLivePreparedResource> { [idle.Record.RecordKey] = idle };
        var goodDetail = new CurrentClientMapBlockSource.ResourceScanDetail(
            true, 100000, 216000, 20106, 216000, "100317");
        var good = Details(goodDetail);
        CurrentClientMapBlockSource.RequireCompleteResourceHydration(current, good);
        // An occupied source has no defensible requirement for a current
        // unoccupied amount, so it can be retained without a detail request.
        var occupied = ResourceAt(0, 999, occupied: true);
        CurrentClientMapBlockSource.RequireCompleteResourceHydration(
            new Dictionary<string, FirstLivePreparedResource> { [occupied.Record.RecordKey] = occupied },
            CurrentClientMapBlockSource.ResourceScanDetailObservation.Empty);

        int hydrationInverses = 0;
        void RejectHydration(string label, CurrentClientMapBlockSource.ResourceScanDetailObservation details)
        {
            try { CurrentClientMapBlockSource.RequireCompleteResourceHydration(current, details); }
            catch (InvalidDataException) { hydrationInverses++; return; }
            throw new Exception("Native Resource hydration wrongly accepted " + label);
        }
        RejectHydration("timeout fail open", CurrentClientMapBlockSource.ResourceScanDetailObservation.Failed("timeout"));
        RejectHydration("partial native reply", good with { Error = "resource_scan_detail_partial_response" });
        RejectHydration("omitted current idle target", good with { TargetCount = 0, Details = new Dictionary<string, CurrentClientMapBlockSource.ResourceScanDetail>() });
        RejectHydration("unreceived current native detail", Details(goodDetail with { Received = false }, ready: 0));
        RejectHydration("fabricated ready counter", good with { ReadyCount = 0 });
        RejectHydration("reincarnated config mismatch", Details(goodDetail with { ResourceConfigId = 20107 }));
        RejectHydration("stale name key", Details(goodDetail with { SourceNameKey = "999999" }));
        RejectHydration("stale source capacity", Details(goodDetail with { SourceCapacity = 72000 }));
        RejectHydration("detail amount/config mismatch", Details(goodDetail with { FullAmount = 100001 }));

        if (nativeInverses != 14 || hydrationInverses != 9)
            throw new Exception("Incomplete Resource native source inverse coverage.");
        Console.WriteLine($"MAP014_RESOURCE_NATIVE_SOURCE_CHECKS_OK nativeInverses={nativeInverses} hydrationInverses={hydrationInverses} native=false");
    }

    private static CurrentClientMapBlockSource.ResourceScanDetailObservation Details(
        CurrentClientMapBlockSource.ResourceScanDetail detail, int ready = 1) => new(
        1, 1, 0, 0, ready, new Dictionary<string, CurrentClientMapBlockSource.ResourceScanDetail>
        { ["999001"] = detail }, null);

    private static ResourceCompletenessBatchDiagnostics Batch(int accepted,
        Dictionary<string, int> aoi, int rejected = 0, string? reason = null) => new(
            accepted + rejected, accepted + rejected, accepted + rejected,
            accepted, rejected, 0,
            new Dictionary<string, int> { ["ResPointInfo"] = accepted + rejected },
            new Dictionary<string, int> { ["7"] = accepted + rejected },
            reason is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [reason] = rejected },
            aoi, accepted, 0);

    private static FirstLivePreparedResource Edit(FirstLivePreparedResource resource, string name, object? value)
    {
        JsonObject data = JsonNode.Parse(resource.Record.DataJson)!.AsObject();
        data[name] = JsonSerializer.SerializeToNode(value);
        return resource with { Record = resource.Record with { DataJson = data.ToJsonString() } };
    }

    private static FirstLivePreparedResource Remove(FirstLivePreparedResource resource, string name)
    {
        JsonObject data = JsonNode.Parse(resource.Record.DataJson)!.AsObject();
        data.Remove(name);
        return resource with { Record = resource.Record with { DataJson = data.ToJsonString() } };
    }

    private static FirstLivePreparedResource ResourceAt(int x, int y, bool occupied = false)
    {
        int point = checked(y * 1000 + x + 1);
        const long observedAt = 1791917011000;
        string recordKey = point.ToString(CultureInfo.InvariantCulture);
        string json = JsonSerializer.Serialize(new
        {
            kind = "resource", sourceKind = "resource_point",
            source = "WorldPointManager._pointInfos+GatherResource+LuaEntry.Player.IsInBlackRange",
            resourceSourceType = "ResPointInfo", runtimeClass = "ResPointInfo",
            pointType = 7, pointId = point, pointIndex = point, recordKey,
            serverId = 2212, srcServerId = 2212, worldId = 0, x, y,
            resourceTypeId = 14, resourceNameKey = "100317", resourceConfigId = 20106,
            resourceMaxAmount = 216000, level = 6, updatedAt = observedAt,
            gatherOccupancyKnown = true, gatherOccupied = occupied,
            rebuildGatherOccupancyKnown = true, rebuildGatherOccupied = occupied,
            blackTileKnown = true, isBlackTile = false,
        });
        var record = new MapStoredRecord("resource", 2212, recordKey, point,
            null, "source", null, 6, null, null, null, null, observedAt, json);
        var import = new FirstLiveResultImport(2212, recordKey, point, x, y, 6,
            observedAt, "controlled-hash", "synthetic-not-native", null, null, json);
        return new FirstLivePreparedResource(import, record);
    }
}
