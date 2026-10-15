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
        CheckBoundedCityBandTransition();
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
        // Genuine lens-matrix shapes (default zoom lens 130 -> 11x3; zoomed-in player lens 130 -> 10x3; no lens at zoom 60 -> 7x3).
        foreach (var (firstRow, rowCount) in new[] { (1, 11), (1, 10), (1, 7) })
            CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(
                Enumerable.Range(firstRow, rowCount).SelectMany(row => Enumerable.Range(2, 3).Select(col => row * 100 + col)).ToArray(),
                0, request, session, 35, 75, 1);
        try { CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(Enumerable.Range(1, 70).SelectMany(row => Enumerable.Range(2, 3)
            .Select(col => row * 100 + col)).ToArray(), 0, request, session, 35, 75, 1); throw new Exception("too tall window passed"); }
        catch (InvalidDataException) { }
        try { CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(Enumerable.Range(1, 5).SelectMany(row => Enumerable.Range(2, 3)
            .Select(col => row * 100 + col)).ToArray(), 0, request, session, 35, 75, 1); throw new Exception("too short window passed"); }
        catch (InvalidDataException) { }
        // Genuine failure 2026-10-15 20:18 (calibrated window starts on the band's last missing row 19, rowStart 10).
        CurrentClientMapBlockSource.ValidateAdaptiveRowFootprint(
            Enumerable.Range(19, 10).SelectMany(row => Enumerable.Range(0, 2).Select(col => row * 100 + col)).ToArray(), 10, request, session, 5, 275, 3);
        // Calibrated vertical placement: window top = target row + measured offset (-6 on genuine replies).
        if (MapFullWorldAoiPlacement.TargetY(20, Enumerable.Range(20, 10).ToArray(), false, -6) != 265 ||
            MapFullWorldAoiPlacement.TargetY(20, [27, 28, 29], false, -6) != 335 ||
            MapFullWorldAoiPlacement.TargetY(90, Enumerable.Range(90, 10).ToArray(), false, -6) != 965 ||
            MapFullWorldAoiPlacement.TargetY(0, Enumerable.Range(0, 10).ToArray(), false, -6) != 65 ||
            MapFullWorldAoiPlacement.TargetY(20, Enumerable.Range(20, 10).ToArray()) != 275)
            throw new Exception("Calibrated vertical placement did not follow the measured window offset");
        Console.WriteLine($"MAP014_NATIVE_EIGHT_ROW_FOOTPRINT_OK covered={covered.Count} simulatedProbes={probes} negatives=5 native=false");
    }

    private static void CheckBoundedCityBandTransition()
    {
        var cityRequest = new MapScanExecutionRequest(
            "bounded-city-band-transition", 2212, 0, 1000, 1000, ["city"], 8);
        bool bounded = CurrentClientMapBlockSource.ShouldUseBoundedFullWorldCameraLens(cityRequest, null);
        if (!bounded) throw new Exception("City production selection did not choose the bounded lens.");
        // Reproduce the real failure's control-flow state: the bounded route's
        // outer band stays zero while its global gap advances beyond row nine.
        var covered = Enumerable.Range(0, 1000).ToHashSet();
        int firstGap = Enumerable.Range(0, 10000).First(index => !covered.Contains(index));
        int gapColumn = firstGap % 100;
        int[] oldMissingRows = Enumerable.Range(0, 10)
            .Where(row => !covered.Contains(row * 100 + gapColumn)).ToArray();
        bool oldFailed = false;
        try { _ = MapFullWorldAoiPlacement.TargetY(0, oldMissingRows); }
        catch (ArgumentOutOfRangeException) { oldFailed = true; }
        if (!oldFailed || oldMissingRows.Length != 0)
            throw new Exception("The historical bounded City placement failure was not reproduced.");

        var selected = CurrentClientMapBlockSource.SelectFullWorldProbeY(
            bounded, covered, firstGap, 0, gapColumn, false, null);
        if (selected.TargetY != 125 || selected.MissingRows.Length != 0)
            throw new Exception("Bounded City placement evaluated serial-band missing rows.");
        // Source coverage and target membership still go through the actual
        // bounded footprint guard; this is a controlled rectangle, not native evidence.
        int[] freshFootprint = Enumerable.Range(10, 8).SelectMany(row =>
            Enumerable.Range(0, 15).Select(col => row * 100 + col)).ToArray();
        CurrentClientMapBlockSource.ValidateBoundedLensFootprint(
            freshFootprint, firstGap, 75, selected.TargetY);
        // Do not weaken the serial route: an empty band remains invalid there.
        bool serialRejected = false;
        try { _ = CurrentClientMapBlockSource.SelectFullWorldProbeY(
            false, covered, -1, 0, gapColumn, false, null); }
        catch (ArgumentOutOfRangeException) { serialRejected = true; }
        if (!serialRejected) throw new Exception("Serial empty-band validation was weakened.");
        Console.WriteLine("MAP014_BOUNDED_CITY_BAND_TRANSITION_OK before=ArgumentOutOfRange afterTargetY=125 native=false");
    }
}
