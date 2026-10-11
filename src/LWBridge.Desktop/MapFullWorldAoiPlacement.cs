namespace LWBridge.Desktop;

/// <summary>
/// Serial current-client LOD0 placement policy. A wider step is permitted only
/// after the native footprint has demonstrated sufficient columns in this band.
/// The caller must still validate the fresh response and the exact AOI union.
/// </summary>
internal static class MapFullWorldAoiPlacement
{
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
