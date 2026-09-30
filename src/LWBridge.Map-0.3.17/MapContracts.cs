using System.Text.Json;
using System.Text.Json.Serialization;

namespace LWBridge.Map317;

public static class MapKinds
{
    public static readonly string[] All =
        ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"];

    private static readonly HashSet<string> Allowed = new(All, StringComparer.Ordinal);

    public static bool IsValid(string value) => Allowed.Contains(value);
}

public sealed record MapRecord(
    string Kind,
    int ServerId,
    string RecordKey,
    int? PointIndex,
    string? Uuid,
    string? Name,
    string? AllianceName,
    int? Level,
    int? Quality,
    long? Power,
    double? Distance,
    long? ShieldEndTime,
    long UpdatedAt,
    string DataJson);

public sealed record MapSort(string SortBy, string SortOrder);

public sealed record MapQuery(
    string Kind,
    int ServerId,
    int Page = 1,
    int PageSize = 50,
    IReadOnlyList<MapSort>? Sorts = null,
    string? Keyword = null,
    string? ResourceNameKey = null,
    string? MonsterNameKey = null,
    int? TreasureType = null,
    int? SuppliesType = null,
    string? Alliance = null,
    bool WithoutAlliance = false,
    bool MarkedOnly = false,
    string? Quality = null,
    bool SpecialOnly = false,
    bool ReindeerOnly = false,
    string? ItemKey = null,
    string? CompletionStatus = null,
    bool PlunderableOnly = false,
    bool IncludeForeignRadarTreasures = false,
    bool LuckyFirst = false,
    string? ViewerUid = null,
    string? ViewerAllianceId = null,
    int? MinLevel = null,
    int? MaxLevel = null,
    string? ScanRunId = null);

public sealed record MapSearchResult(IReadOnlyList<JsonElement> Rows, int Total);

public sealed record MapScanState(
    int ServerId,
    string ServerIdSource,
    string ScanRunId,
    bool IsReading,
    string Phase,
    IReadOnlyList<string> SelectedTypes,
    int TotalBlocks,
    int CompletedBlocks,
    int ReadBlocks,
    int FailedBlocks,
    int UnreadBlocks,
    int InflightBlocks,
    string ScanMode,
    int Concurrency,
    double ScanRate,
    double ProgressPercent,
    bool NativeCaptureReady,
    int NativePendingRecords,
    int NativeDroppedRecords,
    bool ResumeAvailable,
    [property: JsonPropertyName("lastError")]
    string? Error = null,
    long StartedAt = 0);

public sealed record MapPlayerMark(
    int ServerId,
    string OwnerUid,
    string State,
    long MarkedAt,
    long? CheckedAt,
    string PlayerJson);

public sealed record MapScanRun(
    string Id,
    int ServerId,
    IReadOnlyList<string> SelectedTypes,
    string Status,
    int TotalBlocks,
    int CompletedBlocks,
    int FailedBlocks,
    long CreatedAt,
    long UpdatedAt,
    string? Error);

public sealed record MapOptionSet(
    int ServerId,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<MapAllianceOption> Alliances,
    IReadOnlyDictionary<string, IReadOnlyList<MapNameOption>> Names,
    IReadOnlyList<int> DispatchLevels,
    int NoAllianceCount,
    IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> RewardItems,
    IReadOnlyList<JsonElement> TreasureTypes,
    MapScanRun? ScanProgress);

public sealed record MapAllianceOption(string Name, int Count);

public sealed record MapNameOption(string Key, int Count);

public sealed record CityExportOptions(
    IReadOnlyList<string> Headers,
    string SheetName,
    string YesLabel,
    string NoLabel);

public sealed record CityExportResult(bool Canceled, int RowCount, string? Path);
