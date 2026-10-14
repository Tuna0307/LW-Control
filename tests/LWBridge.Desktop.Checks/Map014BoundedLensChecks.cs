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
        Console.WriteLine($"MAP014_BOUNDED_LENS_CONTROLLED_OK coverage={covered.Count} probes={probes} inverses=5 cityIdentityInverses=3 scannerSelectionInverses=8 native=false");
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
