using LWBridge.Desktop;

internal static class Map014BoundedLensChecks
{
    internal static void Run()
    {
        var covered = new HashSet<int>();
        int probes = 0;
        while (covered.Count != 10000)
        {
            int gap = Enumerable.Range(0,10000).First(i => !covered.Contains(i));
            int x = Math.Min(99,gap%100+7), y=Math.Min(99,gap/100+2);
            int[] footprint=Enumerable.Range(Math.Max(0,y-2),Math.Min(100,y+4)-Math.Max(0,y-2))
                .SelectMany(r => Enumerable.Range(Math.Max(0,x-7),Math.Min(100,x+8)-Math.Max(0,x-7))
                    .Select(c => r*100+c)).ToArray();
            CurrentClientMapBlockSource.ValidateBoundedLensFootprint(footprint,gap,x*10+5,y*10+5);
            covered.UnionWith(footprint);
            if (++probes>400) throw new Exception("Bounded lens planner made no finite coverage progress");
        }
        int[] valid=Enumerable.Range(40,6).SelectMany(r => Enumerable.Range(38,15).Select(c => r*100+c)).ToArray();
        Reject(valid.Concat([valid[0]]).ToArray(),4245,455,425);
        Reject(valid.Where(i=>i!=valid[0]).ToArray(),4245,455,425);
        Reject(valid,4954,545,495);
        Reject([4245],4245,455,425);
        Reject(valid.Concat([10000]).ToArray(),4245,455,425);
        var cities = new Dictionary<string,FirstLivePreparedResource>();
        var first = City(2212,"owner",174054,10);
        var current = City(2212,"owner",199052,11);
        CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,first);
        CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,current);
        CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,first);
        if(cities.Count!=1 || cities.Single().Value.Record.PointIndex!=199052)
            throw new Exception("City old location replaced or duplicated the latest native owner record");
        CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,City(2213,"owner",174054,10));
        if(cities.Count!=2) throw new Exception("City owner aggregation leaked across servers");
        CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,City(2212,"owner",199052,11));
        if(cities.Count!=2 || cities["2212:owner"].Record.PointIndex!=199052)
            throw new Exception("Duplicate City observation changed owner location");
        try
        {
            CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,City(2212,"owner",174054,11));
            throw new Exception("City accepted an ambiguous same-time relocation");
        }
        catch(InvalidDataException) { }
        try
        {
            CurrentClientMapBlockSource.MergeLatestCityByOwner(cities,City(2212,"0",999,12));
            throw new Exception("City accepted an invalid native owner identity");
        }
        catch(InvalidDataException) { }
        MapScanExecutionRequest choice = new("selection",2212,0,1000,1000,["city"],8);
        bool Uses(MapScanExecutionRequest req,CurrentClientMapBlockSourceHooks? hooks=null) =>
            CurrentClientMapBlockSource.ShouldUseBoundedFullWorldCameraLens(req,hooks);
        if (!Uses(choice) || !Uses(choice with { ScanMode = "fast" }) ||
            Uses(choice with { SelectedTypes = ["resource"] }) ||
            Uses(choice with { SelectedTypes = ["truck"] }) ||
            Uses(choice with { SelectedTypes = ["railway"] }) ||
            Uses(choice with { SelectedTypes = ["other"] }) ||
            Uses(choice with { SelectedTypes = ["treasure"] }) ||
            Uses(choice with { SelectedTypes = ["city","resource","truck"] }) ||
            Uses(choice, new CurrentClientMapBlockSourceHooks { ForceLegacyFullWorldLens = true }) ||
            !Uses(choice with { SelectedTypes = ["city","truck"] },
                new CurrentClientMapBlockSourceHooks { ExperimentalBoundedCameraLens = true }))
            throw new Exception("Scanner production policy enabled an unqualified selection or broke explicit baseline tests");
        var independentProfileCities=new Dictionary<string,FirstLivePreparedResource>();
        CurrentClientMapBlockSource.MergeLatestCityByOwner(independentProfileCities,City(2212,"owner",174054,10));
        if(independentProfileCities.Count!=1 || cities["2212:owner"].Record.PointIndex!=199052)
            throw new Exception("One profile's City aggregation mutated another scan");
        // Passive manager observations cannot be accepted on the strength of
        // point-ready flags. They must include source identities at two
        // ordered observation boundaries, with no null/capped/duplicate UUIDs.
        string MarchJson(string? before = null,string? after = null) =>
            "{\"marchSourceBefore\":" + (before ?? MarchSnapshot(10,2)) +
            ",\"marchSourceAtPointReady\":" + (after ?? MarchSnapshot(12,2)) + "}";
        static string MarchSnapshot(double seconds,int count,bool capped=false,string? uuids=null) =>
            System.Text.Json.JsonSerializer.Serialize(new
            {
                available=true, observedAtRuntimeSeconds=seconds,
                marchDictionaryCount=count,marchUuidSampleCapped=capped,
                marchUuids=uuids is null ? new[] { "uuid-A", "uuid-B" } : uuids.Split('|'),
            });
        void AcceptTelemetry(string json)
        {
            using var document=System.Text.Json.JsonDocument.Parse(json);
            CurrentClientMapBlockSource.ValidateNativeMarchTelemetry(document.RootElement);
        }
        void RejectTelemetry(string json)
        {
            try { AcceptTelemetry(json); }
            catch(InvalidDataException) { return; }
            throw new Exception("Native March telemetry accepted stale, invalid, or partial source membership");
        }
        AcceptTelemetry(MarchJson());
        RejectTelemetry(MarchJson("null"));
        RejectTelemetry(MarchJson(MarchSnapshot(10,2,true)));
        RejectTelemetry(MarchJson(MarchSnapshot(10,2,false,"uuid-A|uuid-A")));
        RejectTelemetry(MarchJson(MarchSnapshot(10,3)));
        RejectTelemetry(MarchJson(MarchSnapshot(10,2),MarchSnapshot(9,2)));
        RejectTelemetry(MarchJson(MarchSnapshot(10,2),MarchSnapshot(1000,2)));
        RejectTelemetry(MarchJson(MarchSnapshot(10,2),"{}"));
        // Exhaustively exercise every non-empty combination rather than only
        // the eight historical examples. The passive March diagnostic opt-in
        // must never silently select the faster point lens for mobile/mixed.
        string[] possible = ["city", "resource", "monster", "zombie_boss", "truck",
            "railway", "dispatch", "ghost", "treasure", "other"];
        int combinations = 0;
        for (int mask = 1; mask < 1 << possible.Length; mask++)
        {
            string[] selected = possible.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            var candidate = choice with { SelectedTypes = selected };
            bool qualified = selected.Length == 1 && selected[0] == "city";
            if (Uses(candidate) != qualified ||
                Uses(candidate, new CurrentClientMapBlockSourceHooks { ObserveMarchFreshness = true }) != qualified ||
                Uses(candidate, new CurrentClientMapBlockSourceHooks { ForceLegacyFullWorldLens = true }))
                throw new Exception("Scanner method was promoted for an unqualified kind or mixed selection");
            combinations++;
        }
        if (combinations != 1023 || Uses(choice with { SelectedTypes = ["city", "city"] }))
            throw new Exception("Scanner method selection allowed duplicate or incomplete case enumeration");
        // Unlike native AOI coverage, run-wide Resource source accounting is
        // a separate publication safety invariant. A source can visit all
        // 10k AOIs yet return irreconcilable Resource candidate identities.
        var resourceReport = new ResourceCompletenessAccumulator().Build(
            "controlled-resource-run", 2212, 0, 1000, 1000,
            Array.Empty<FirstLivePreparedResource>());
        resourceReport.RequireReconciledBeforePublication(0, 0);
        // Nonempty source-backed accounting shape at both inclusive map
        // corners; source no-object counts alone would not test the merge.
        static FirstLivePreparedResource ResourceAt(int x, int y)
        {
            int id = checked(y * 1000 + x + 1);
            string data = System.Text.Json.JsonSerializer.Serialize(new
            {
                x, y, pointType = 1, resourceTypeId = "5", resourceNameKey = "resource-5",
                blackTileKnown = true, isBlackTile = false,
                rebuildGatherOccupancyKnown = true, rebuildGatherOccupied = false,
                resourceDetailKnown = true, resourceRemainingAmount = 50,
                resourceFullAmount = 50, srcServerId = 2212, worldId = 0,
            });
            var stored = new MapStoredRecord("resource", 2212, id.ToString(), id,
                null, "source", null, 5, null, null, null, null, 100, data);
            var imported = new FirstLiveResultImport(2212, id.ToString(), id,
                x, y, 5, 100, "hash", "controlled", null, null, data);
            return new FirstLivePreparedResource(imported, stored);
        }
        var boundaryResources = new[] { ResourceAt(0, 0), ResourceAt(999, 999) };
        var resourceAccounting = new ResourceCompletenessAccumulator();
        resourceAccounting.AddBatch(new ResourceCompletenessBatchDiagnostics(
            2, 2, 2, 2, 0, 0,
            new Dictionary<string, int> { ["ResPointInfo"] = 2 },
            new Dictionary<string, int> { ["1"] = 2 },
            new Dictionary<string, int>(),
            new Dictionary<string, int> { ["0"] = 1, ["9999"] = 1 }, 2, 0));
        foreach (FirstLivePreparedResource resource in boundaryResources)
            resourceAccounting.RecordAcceptedMerge(resource, null);
        var boundaryReport = resourceAccounting.Build("resource-boundaries", 2212, 0,
            1000, 1000, boundaryResources);
        boundaryReport.RequireReconciledBeforePublication(1, 2);
        if (boundaryReport.AcceptedRows.Select(row => row.NativeAoiIndex)
                .Order().SequenceEqual([0, 9999]) == false ||
            boundaryReport.AcceptedRows.Select(row => row.LogicalBlockIndex)
                .Order().SequenceEqual([0, 2499]) == false)
            throw new Exception("Resource source accounting lost inclusive 0/999 seams");
        int resourcePublicationInverses = 0;
        void RejectResource(ResourceCompletenessReport incorrect, int responseCount = 0, int records = 0)
        {
            try { incorrect.RequireReconciledBeforePublication(responseCount, records); }
            catch (InvalidDataException) { resourcePublicationInverses++; return; }
            throw new Exception("Resource source mismatch silently reached Map publication");
        }
        RejectResource(resourceReport, responseCount: 1);
        RejectResource(resourceReport, records: 1);
        RejectResource(resourceReport with { RawObservationCountersReconcile = false });
        RejectResource(resourceReport with { FinalDeduplicationReconciles = false });
        RejectResource(resourceReport with { AcceptedDistributionsReconcile = false });
        RejectResource(resourceReport with { FinalAcceptedUniqueRecords = 1 });
        var alteredResource = new ResourceDuplicateObservation("7", 7, 2212, 7, 0, 3,
            123456789, 123456789, "ResPointInfo", 7, "5", 19, 2212, 0, false, false, "{}");
        RejectResource(resourceReport with { DuplicateComparisons =
            [new ResourceDuplicateComparison("7", "conflicting_same_record_key", false,
                alteredResource, alteredResource)] });
        if (resourcePublicationInverses != 7)
            throw new Exception("Incomplete Resource accounting inverse coverage");
        Console.WriteLine($"MAP014_BOUNDED_LENS_CONTROLLED_OK coverage={covered.Count} probes={probes} inverses=5 cityIdentityInverses=3 scannerSelectionInverses=8 scannerCombinations={combinations} marchTelemetryInverses=7 native=false");
    }

    private static FirstLivePreparedResource City(int server,string owner,int point,long updated)
    {
        string data=System.Text.Json.JsonSerializer.Serialize(new{ownerUid=owner});
        var record=new MapStoredRecord("city",server,point.ToString(),point,null,"native",null,30,null,null,null,null,updated,data);
        var import=new FirstLiveResultImport(server,point.ToString(),point,0,0,30,updated,"hash","controlled",null,null,data);
        return new FirstLivePreparedResource(import,record);
    }

    private static void Reject(int[] footprint,int gap,int x,int y)
    {
        try { CurrentClientMapBlockSource.ValidateBoundedLensFootprint(footprint,gap,x,y); }
        catch(InvalidDataException) { return; }
        throw new Exception("Bounded lens accepted stale/holed/duplicate/invalid native geometry");
    }
}
