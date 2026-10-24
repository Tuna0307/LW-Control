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
        // The native producer categorizes types 1, 7 and 26 as Resource.
        // Keep support for all three, not just the type-7 archived example.
        var mixedResourceTypes = batch with
        {
            RawPointTypeCounts = new Dictionary<string, int> { ["1"] = 1, ["26"] = 1 },
        };
        CurrentClientMapBlockSource.RequireResourceNativeSourceCohort(
            [Edit(edge, "pointType", 1), Edit(opposite, "pointType", 26)],
            mixedResourceTypes, footprint, 2212, 0);
        // The reflected native runtime class comes from the same enumerated
        // `info` object in both the source histogram and accepted row. Allow
        // heterogeneous classes only when the histogram actually names each;
        // resourceSourceType comes from a different optional lookup.
        CurrentClientMapBlockSource.RequireResourceNativeSourceCohort(
            [Edit(edge, "runtimeClass", "GatherPointInfo"), opposite],
            batch with { RuntimeClassCounts = new Dictionary<string, int>
                { ["GatherPointInfo"] = 1, ["ResPointInfo"] = 1 } }, footprint, 2212, 0);

        // The native Lua producer takes an independent AOI-filtered
        // point_aoi_counts snapshot before resource_aoi_records. Its later
        // Resource count replaces the first count in the reply, so a source
        // mutation between enumerations used to be admitted as "proven".
        void CheckFirstAoiTypes(string json, IReadOnlyList<FirstLivePreparedResource> rows,
            int firstNativeMatchedCount)
        {
            using JsonDocument parsed = JsonDocument.Parse(json);
            CurrentClientMapBlockSource.RequireResourceFirstAoiTypeSnapshot(
                parsed.RootElement, rows, firstNativeMatchedCount);
        }
        CheckFirstAoiTypes("{\"7\":2,\"6\":12}", [edge, opposite], 14);
        CheckFirstAoiTypes("{\"1\":1,\"26\":1,\"6\":12}",
            [Edit(edge, "pointType", 1), Edit(opposite, "pointType", 26)], 14);
        CheckFirstAoiTypes("[]", [], 0);
        CheckFirstAoiTypes("{\"7\":2,\"unknown\":1}", [edge, opposite], 3);

        int snapshotInverses = 0;
        void RejectFirstSnapshot(string label, string histogram,
            IReadOnlyList<FirstLivePreparedResource>? rows = null, int firstNativeMatchedCount = 14)
        {
            try { CheckFirstAoiTypes(histogram, rows ?? [edge, opposite], firstNativeMatchedCount); }
            catch (InvalidDataException) { snapshotInverses++; return; }
            throw new Exception("Native Resource first-AOI snapshot wrongly accepted " + label);
        }
        RejectFirstSnapshot("first type-7 census short by one", "{\"7\":1,\"6\":12}",
            firstNativeMatchedCount: 13);
        RejectFirstSnapshot("first type-7 census had extra Resource", "{\"7\":3,\"6\":12}",
            firstNativeMatchedCount: 15);
        RejectFirstSnapshot("first AOI was a non-Resource cohort", "{\"6\":14}");
        RejectFirstSnapshot("first census same total but different Resource type", "{\"1\":1,\"7\":1,\"6\":12}");
        RejectFirstSnapshot("missing first AOI native histogram", "null");
        RejectFirstSnapshot("non-integer first native count", "{\"7\":\"2\"}");
        RejectFirstSnapshot("ambiguous duplicate native point-type key", "{\"7\":2,\"7\":2}");
        RejectFirstSnapshot("invalid non-empty native array", "[1,2]");
        RejectFirstSnapshot("negative raw count", "{\"7\":2,\"6\":-1}");

        // NEW invariant independent of the nine first/second Resource-type
        // inverses: point_aoi_counts increments matchedCount and precisely one
        // raw type histogram bucket for each AOI-filtered native point.
        int censusTotalInverses = 0;
        void RejectFirstCensusTotal(string label, string histogram, int firstNativeMatchedCount,
            IReadOnlyList<FirstLivePreparedResource>? rows = null)
        {
            try { CheckFirstAoiTypes(histogram, rows ?? [edge, opposite], firstNativeMatchedCount); }
            catch (InvalidDataException) { censusTotalInverses++; return; }
            throw new Exception("Native Resource first AOI total wrongly accepted " + label);
        }
        RejectFirstCensusTotal("understated first census header", "{\"7\":2,\"6\":12}", 13);
        RejectFirstCensusTotal("overstated first census header", "{\"7\":2,\"6\":12}", 15);
        RejectFirstCensusTotal("invented first-census non-Resource type with valid Resource subset",
            "{\"7\":2,\"6\":12,\"29\":1}", 14);
        RejectFirstCensusTotal("empty histogram with phantom first native AOI count", "[]", 1, []);

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
        RejectNative("raw manager reports no Resource types despite accepted native Resource rows",
            counters: batch with { RawPointTypeCounts = new Dictionary<string, int> { ["6"] = 2 } });
        RejectNative("raw manager reports only one Resource candidate for two accepted rows",
            counters: batch with { RawPointTypeCounts = new Dictionary<string, int> { ["7"] = 1, ["6"] = 1 } });
        RejectNative("accepted type 1 missing from raw native point-type histogram",
            rows: [Edit(edge, "pointType", 1), opposite]);
        RejectNative("accepted type 26 missing from raw native point-type histogram",
            rows: [Edit(edge, "pointType", 26), opposite]);
        RejectNative("native source class histogram reports only non-Resource classes",
            counters: batch with { RuntimeClassCounts = new Dictionary<string, int>
                { ["BuildPointInfo"] = 2 } });
        RejectNative("native runtime-class histogram undercounts accepted Resource class",
            counters: batch with { RuntimeClassCounts = new Dictionary<string, int>
                { ["ResPointInfo"] = 1, ["BuildPointInfo"] = 1 } });
        RejectNative("accepted native runtime class is absent from raw same-pass census",
            rows: [Edit(edge, "runtimeClass", "GatherPointInfo"), opposite]);
        RejectNative("raw native runtime-class identity differs only by case",
            counters: batch with { RuntimeClassCounts = new Dictionary<string, int>
                { ["respointinfo"] = 2 } });

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
        // Two different servers can share point IDs, *and* their Resource
        // capacity/config/name may coincidentally match. The old comparison
        // accepted that cached row despite no current-run reply. Real v27
        // WorldPointDetailManager stores only pointId, with no reply nonce or
        // connection generation, so even an exact metadata match must fail.
        RejectHydration("cross-server stale native cache with identical source metadata",
            good with { RequestCount = 0, CacheBeforeCount = 1 });

        // The real round-5 Lua receipt has 7,494 cached, received targets,
        // each with requestIssued=false/cacheBefore=true/sendFailed=false.
        // Independent per-row flags must reconcile their same-source totals;
        // spoofed aggregate header counters cannot establish received detail.
        JsonObject BaseDetailEnvelope() => JsonNode.Parse("""
            {"targetCount":1,"requestCount":1,"cacheBeforeCount":0,
             "sendFailureCount":0,"readyCount":1,"details":[
               {"recordKey":"999001","received":true,"requestIssued":true,
                "cacheBefore":false,"sendFailed":false,"resourceConfigId":20106,
                "expectedReplyId":123,"replyId":123,"replyScopeMatched":true,
                "resourceNameKey":"100317","resourceMaxAmount":216000,
                "detail":{"remainRes":100000,"initRes":216000}}
             ]}
            """)!.AsObject();
        void CheckProvenance(JsonObject envelope)
        {
            using JsonDocument parsed = JsonDocument.Parse(envelope.ToJsonString());
            JsonElement root = parsed.RootElement;
            CurrentClientMapBlockSource.RequireResourceDetailRowProvenance(
                root.GetProperty("details"), root.GetProperty("targetCount").GetInt32(),
                root.GetProperty("requestCount").GetInt32(),
                root.GetProperty("cacheBeforeCount").GetInt32(),
                root.GetProperty("sendFailureCount").GetInt32(),
                root.GetProperty("readyCount").GetInt32());
        }
        CheckProvenance(BaseDetailEnvelope());
        JsonObject requested = BaseDetailEnvelope();
        requested["requestCount"] = 1;
        requested["cacheBeforeCount"] = 0;
        requested["details"]![0]!["requestIssued"] = true;
        requested["details"]![0]!["cacheBefore"] = false;
        CheckProvenance(requested);

        int provenanceInverses = 0;
        void RejectProvenance(string label, Action<JsonObject> mutate)
        {
            JsonObject broken = BaseDetailEnvelope();
            mutate(broken);
            try { CheckProvenance(broken); }
            catch (InvalidDataException) { provenanceInverses++; return; }
            throw new Exception("Resource provenance wrongly accepted " + label);
        }
        RejectProvenance("header says request issued when row says cached", doc =>
        {
            doc["requestCount"] = 0; doc["cacheBeforeCount"] = 1;
        });
        RejectProvenance("row claims no receipt origin", doc => doc["details"]![0]!["requestIssued"] = false);
        RejectProvenance("double-marked request and cache", doc => doc["details"]![0]!["cacheBefore"] = true);
        RejectProvenance("send failure contradicts successful cache", doc => doc["details"]![0]!["sendFailed"] = true);
        RejectProvenance("ready count overstates received rows", doc => doc["details"]![0]!["received"] = false);
        RejectProvenance("missing native origin flag", doc => doc["details"]![0]!.AsObject().Remove("cacheBefore"));
        RejectProvenance("non-boolean origin flag", doc => doc["details"]![0]!["cacheBefore"] = 1);
        RejectProvenance("send failed but cached detail later appeared", doc =>
        {
            doc["sendFailureCount"] = 1; doc["cacheBeforeCount"] = 0;
            doc["details"]![0]!["cacheBefore"] = false;
            doc["details"]![0]!["sendFailed"] = true;
        });
        RejectProvenance("header claims phantom target", doc => doc["targetCount"] = 2);
        RejectProvenance("unreceived row still contains asserted amount", doc =>
        {
            doc["readyCount"] = 0;
            doc["details"]![0]!["received"] = false;
        });
        RejectProvenance("old cache has no correlated native response", doc =>
        {
            doc["requestCount"] = 0; doc["cacheBeforeCount"] = 1;
            doc["details"]![0]!["requestIssued"] = false;
            doc["details"]![0]!["cacheBefore"] = true;
        });
        RejectProvenance("foreign request response for same point", doc => doc["details"]![0]!["replyId"] = 124);
        RejectProvenance("connection epoch changed", doc => doc["details"]![0]!["replyScopeMatched"] = false);
        RejectProvenance("native reply ID omitted", doc => doc["details"]![0]!.AsObject().Remove("replyId"));
        RejectProvenance("two targets reuse one native reply", doc =>
        {
            doc["targetCount"] = 2; doc["requestCount"] = 2; doc["readyCount"] = 2;
            var duplicate = doc["details"]![0]!.DeepClone(); duplicate["recordKey"] = "999002";
            doc["details"]!.AsArray().Add(duplicate);
        });

        if (nativeInverses != 22 || hydrationInverses != 10 || provenanceInverses != 15 ||
            snapshotInverses != 9 || censusTotalInverses != 4)
            throw new Exception("Incomplete Resource native source inverse coverage.");
        JsonObject cancelAck = JsonNode.Parse("""
            {"requestId":"request","scanRunId":"run","profileId":"profile",
             "launchSessionId":"session","challenge":"challenge","gamePid":123,
             "serverId":2212,"state":"cancelled","nativeLaneStopped":true}
            """)!.AsObject();
        bool CancelMatches(JsonObject node)
        {
            using JsonDocument doc = JsonDocument.Parse(node.ToJsonString());
            return CurrentClientMapBlockSource.ResourceDetailCancellationMatches(
                doc.RootElement, "request", "run", "profile", "session", "challenge", 123, 2212);
        }
        if (!CancelMatches(cancelAck)) throw new Exception("Exact Resource native cancellation rejected.");
        foreach (string field in new[] { "requestId", "scanRunId", "profileId", "launchSessionId", "challenge", "state" })
        {
            JsonObject foreign = (JsonObject)cancelAck.DeepClone(); foreign[field] = "foreign";
            if (CancelMatches(foreign)) throw new Exception("Foreign Resource cancellation admitted: " + field);
        }
        foreach (string field in new[] { "gamePid", "serverId" })
        {
            JsonObject foreign = (JsonObject)cancelAck.DeepClone(); foreign[field] = 999;
            if (CancelMatches(foreign)) throw new Exception("Foreign Resource cancellation identity admitted.");
        }
        cancelAck["nativeLaneStopped"] = false;
        if (CancelMatches(cancelAck)) throw new Exception("Unretired Resource lane acknowledged cancellation.");
        Console.WriteLine($"MAP014_RESOURCE_NATIVE_SOURCE_CHECKS_OK nativeInverses={nativeInverses} hydrationInverses={hydrationInverses} provenanceInverses={provenanceInverses} firstSnapshotInverses={snapshotInverses} firstCensusTotalInverses={censusTotalInverses} native=false");
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
            source = "WorldPointManager._pointInfos+GatherResource+SceneUtils.IsInBlackRange",
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
