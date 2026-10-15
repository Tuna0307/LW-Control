namespace LWBridge.Desktop;

/// <summary>
/// Serial current-client LOD0 placement policy. A wider step is permitted only
/// after the native footprint has demonstrated sufficient columns in this band.
/// The caller must still validate the fresh response and the exact AOI union.
/// </summary>
internal static class MapFullWorldAoiPlacement
{
    // The current v26 camera can return an eight-row LOD0 footprint while
    // preserving the genuine native reply and exact home restoration. A band
    // has ten rows, so the already-observed rows (not a guessed native width)
    // must drive subsequent vertical placement to recover both seams.
    internal static int TargetY(int bandAoiStartRow, IReadOnlyList<int> missingAoiRows,
        bool nativeEightRowWindowObserved = false, int? calibratedFirstRowOffset = null)
    {
        if (bandAoiStartRow < 0 || bandAoiStartRow > 90 ||
            bandAoiStartRow % 10 != 0 ||
            missingAoiRows is null || missingAoiRows.Count == 0 ||
            missingAoiRows.Any(row => row < bandAoiStartRow || row >= bandAoiStartRow + 10) ||
            missingAoiRows.Distinct().Count() != missingAoiRows.Count)
            throw new ArgumentOutOfRangeException(nameof(missingAoiRows));
        int center = bandAoiStartRow * 10 + 75;
        // Measured on a real reply: window top = target row + offset. Put the window's top on the first
        // missing row so a window as tall as the band covers it in one request (lens/zoom independent).
        if (calibratedFirstRowOffset is int offset)
            return Math.Clamp(missingAoiRows.Min() - offset, 0, 99) * 10 + 5;
        // The first probe still tests the native default; move only after
        // real source coverage has demonstrated that one seam is omitted.
        if (missingAoiRows.Count == 10)
            return nativeEightRowWindowObserved ? center - 10 : center;
        if (missingAoiRows.Contains(bandAoiStartRow)) return center - 10;
        if (missingAoiRows.Contains(bandAoiStartRow + 9)) return center + 10;
        return center;
    }

    internal static int TargetColumn(
        string scanMode, int firstUncoveredColumn, bool measuredWide,
        int? previousGapColumn, int columnCount = 100)
    {
        if (scanMode is not ("normal" or "fast"))
            throw new ArgumentOutOfRangeException(nameof(scanMode));
        if (columnCount < 1 || firstUncoveredColumn < 0 || firstUncoveredColumn >= columnCount)
            throw new ArgumentOutOfRangeException(nameof(firstUncoveredColumn));

        int offset = scanMode == "fast" && measuredWide ? 2 : 1;
        if (firstUncoveredColumn == 0 || previousGapColumn == firstUncoveredColumn)
            offset = firstUncoveredColumn == 0 ? 0 : 1;
        return Math.Min(columnCount - 1, firstUncoveredColumn + offset);
    }
}
