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
        Console.WriteLine($"MAP014_BOUNDED_LENS_CONTROLLED_OK coverage={covered.Count} probes={probes} inverses=5 native=false");
    }

    private static void Reject(int[] footprint,int gap,int x,int y)
    {
        try { CurrentClientMapBlockSource.ValidateBoundedLensFootprint(footprint,gap,x,y); }
        catch(InvalidDataException) { return; }
        throw new Exception("Bounded lens accepted stale/holed/duplicate/invalid native geometry");
    }
}
