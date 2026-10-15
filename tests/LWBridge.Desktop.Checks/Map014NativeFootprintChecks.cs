using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

// Controlled approximation of the genuine eight-row source rectangles saved
// during the owner's failed Oct15 Truck/Dispatch runs. These probes are NOT a
// game-success receipt; they distinguish the historical rejection and prove
// finite exact 10,000-cell union under the corrected vertical placement.
internal static class Map014NativeFootprintChecks
{
    internal static void Run()
    {
        var request = new MapScanExecutionRequest(
            "eight-native-rows", 2212, 0, 1000, 1000, ["truck"], 20,
            PlayerTileX: 500, PlayerTileY: 500, ScanMode: "fast");
        var session = new OverviewMapScanSession("owner", "session", "challenge", 1234,
            @"C:\Game\LastWar.exe", "2026-10-15T00:00:00Z");
        var covered = new HashSet<int>();
        int probes = 0;
        bool observedNativeEightRows = false;
        for (int band = 0; band < 10; band++)
        {
            int firstRow = band * 10;
            int rowProbes = 0;
            while (true)
            {
                int gap = -1;
                for (int col = 0; col < 100; col++)
                {
                    if (Enumerable.Range(firstRow, 10).Any(row => !covered.Contains(row * 100 + col)))
                    {
                        gap = col;
                        break;
                    }
                }
                if (gap < 0) break;
                int[] missing = Enumerable.Range(firstRow, 10)
                    .Where(row => !covered.Contains(row * 100 + gap)).ToArray();
                int y = MapFullWorldAoiPlacement.TargetY(firstRow, missing, observedNativeEightRows);
                int targetColumn = MapFullWorldAoiPlacement.TargetColumn("fast", gap, false, null);
                int targetX = targetColumn * 10 + 5;
                // Proven source shape: center Y=75 -> row indices 1..8,
                // X=5 -> columns 0..1, X=35 -> columns 2..3 or 2..4.
                int nativeRowStart = Math.Max(0, Math.Min(92, y / 10 - 6));
                int nativeColumnStart = Math.Clamp(targetColumn - 1, 0, 98);
                int[] footprint = Enumerable.Range(nativeRowStart, 8)
                    .SelectMany(row => Enumerable.Range(nativeColumnStart, 2)
                        .Select(col => row * 100 + col)).ToArray();
                CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(
                    footprint, firstRow, request, session, targetX, y, 1);
                observedNativeEightRows = true;
                int before = covered.Count;
                covered.UnionWith(footprint);
                if (covered.Count == before || ++rowProbes > 200)
                    throw new InvalidOperationException("Eight-row native source made no finite AOI progress.");
                probes++;
            }
        }
        if (covered.Count != 10_000 || probes > 1050)
            throw new InvalidOperationException($"Eight-row native source omitted {10_000 - covered.Count} AOIs.");

        int[] genuineShape = Enumerable.Range(1, 8).SelectMany(row =>
            Enumerable.Range(0, 2).Select(col => row * 100 + col)).ToArray();
        void Reject(int[] source)
        {
            try { CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(
                source, 0, request, session, 5, 75, 1); }
            catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Malformed native footprint reached coverage.");
        }
        CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(
            genuineShape, 0, request, session, 5, 75, 1);
        CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(
            Enumerable.Range(1, 8).SelectMany(row => Enumerable.Range(0, 3)
                .Select(col => row * 100 + col)).ToArray(), 0, request, session, 15, 75, 1);
        Reject(genuineShape[..^1]); // hole
        Reject([.. genuineShape, genuineShape[0]]); // duplicate count / bad geometry
        Reject(genuineShape.Select(i => i + 2000).ToArray()); // wrong band
        Reject(genuineShape.Select(i => i + 3).ToArray()); // misses the requested native target
        try { _ = MapFullWorldAoiPlacement.TargetY(0, [0, 0, 9]); throw new Exception("Duplicate AOI rows passed"); }
        catch (ArgumentOutOfRangeException) { }
        Console.WriteLine($"MAP014_NATIVE_EIGHT_ROW_FOOTPRINT_OK covered={covered.Count} simulatedProbes={probes} negatives=5 native=false");
    }
}
