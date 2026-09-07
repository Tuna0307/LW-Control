namespace LWControl.Core;

public sealed record CurrentWorldMapSnapshotHeartbeat
{
    public required string ProbeVersion { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
}

public sealed record CurrentWorldMapBuildInfo
{
    public required string OwnerUid { get; init; }
    public required long Uuid { get; init; }
    public required int BuildId { get; init; }
    public required int Level { get; init; }
    public required int BuildState { get; init; }
    public required int QueueState { get; init; }
    public required string AllianceId { get; init; }
    public required int UpdateEndTime { get; init; }
    public required int UpdateStartTime { get; init; }
    public required int LastHpTime { get; init; }
    public required int ProtectEndTime { get; init; }
    public required int Inside { get; init; }
    public required int CurrentHp { get; init; }
    public required string Name { get; init; }
    public required string AllianceAbbreviation { get; init; }
    public required int LastCollectTime { get; init; }
    public required int UnavailableTime { get; init; }
    public required int MonthCardEndTime { get; init; }
    public required int QueueItemId { get; init; }
    public required int QueueStartTime { get; init; }
    public required int QueueUpdateTime { get; init; }
    public required int DestroyStartTime { get; init; }
    public required int AppearanceId { get; init; }
    public required int SpecialType { get; init; }
    public required string PositionId { get; init; }
}

public sealed record CurrentWorldMapRoadInfo
{
    public required string OwnerUid { get; init; }
    public required long Uuid { get; init; }
    public required int RoadState { get; init; }
    public required int Inside { get; init; }
    public required int CurrentHp { get; init; }
    public required string AllianceId { get; init; }
}

public sealed record CurrentWorldMapCollectResourceInfo
{
    public required int ResourceType { get; init; }
    public required int Level { get; init; }
    public required int Type { get; init; }
    public required int AttachId { get; init; }
}

public sealed record CurrentWorldMapResourceInfo
{
    public required int ResourceId { get; init; }
    public required int State { get; init; }
    public required long GatherUuid { get; init; }
}

public sealed record CurrentWorldMapEventPointInfo
{
    public required string OwnerUid { get; init; }
    public required long Uuid { get; init; }
    public required string EventId { get; init; }
}

public sealed record CurrentWorldMapGarbagePointInfo
{
    public required string OwnerUid { get; init; }
    public required long Uuid { get; init; }
    public required string EventId { get; init; }
    public required long EndTime { get; init; }
}

public sealed record CurrentWorldMapPointSnapshot
{
    public required int Id { get; init; }
    public required int PointType { get; init; }
    public required long Uuid { get; init; }
    public required int ServerId { get; init; }
    public required int SrcServerId { get; init; }
    public required int WorldId { get; init; }
    public CurrentWorldMapBuildInfo? BuildInfo { get; init; }
    public CurrentWorldMapRoadInfo? RoadInfo { get; init; }
    public CurrentWorldMapCollectResourceInfo? CollectResourceInfo { get; init; }
    public CurrentWorldMapResourceInfo? ResourceInfo { get; init; }
    public CurrentWorldMapEventPointInfo? ExplorePointInfo { get; init; }
    public CurrentWorldMapEventPointInfo? SamplePointInfo { get; init; }
    public CurrentWorldMapGarbagePointInfo? GarbagePointInfo { get; init; }
}

/// <summary>
/// Read-only interchange contract for a current-game WorldPointManager capture.
/// It mirrors only fields whose current-build source has been recovered.
/// </summary>
public sealed record CurrentWorldMapSnapshot
{
    public const int SupportedSchemaVersion = 1;
    public const string SupportedMode = "state";
    public const string SupportedSource = "WorldPointManager";
    public const int MaximumPointCount = 50_000;
    public const int MaximumStringLength = 512;
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DefaultFutureTolerance = TimeSpan.FromSeconds(5);

    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string Source { get; init; }
    public required string CaptureId { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required CurrentWorldMapSnapshotHeartbeat Heartbeat { get; init; }
    public required IReadOnlyList<CurrentWorldMapPointSnapshot> Points { get; init; }

    public void Validate(
        DateTimeOffset now,
        TimeSpan? maxAge = null,
        TimeSpan? futureTolerance = null)
    {
        TimeSpan ageLimit = maxAge ?? DefaultMaxAge;
        TimeSpan futureLimit = futureTolerance ?? DefaultFutureTolerance;
        if (ageLimit <= TimeSpan.Zero || ageLimit > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(maxAge), "Snapshot age limit must be positive and at most five minutes.");
        if (futureLimit < TimeSpan.Zero || futureLimit > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(futureTolerance), "Future tolerance must be between zero and thirty seconds.");

        if (SchemaVersion != SupportedSchemaVersion)
            throw new InvalidDataException($"Unsupported world-map snapshot schema version {SchemaVersion}.");
        if (!string.Equals(Mode, SupportedMode, StringComparison.Ordinal))
            throw new InvalidDataException("World-map snapshot is not a read-only state capture.");
        if (!string.Equals(Source, SupportedSource, StringComparison.Ordinal))
            throw new InvalidDataException("World-map snapshot source is not the recovered WorldPointManager boundary.");
        if (string.IsNullOrWhiteSpace(CaptureId) || CaptureId.Length > 128)
            throw new InvalidDataException("World-map snapshot capture ID is invalid.");
        ValidateFreshTimestamp(CapturedAt, now, ageLimit, futureLimit, "capture");

        if (Heartbeat is null || string.IsNullOrWhiteSpace(Heartbeat.ProbeVersion) || Heartbeat.ProbeVersion.Length > 128)
            throw new InvalidDataException("World-map snapshot heartbeat metadata is invalid.");
        ValidateFreshTimestamp(Heartbeat.ObservedAt, now, ageLimit, futureLimit, "heartbeat");

        if (Points is null || Points.Count > MaximumPointCount || Points.Any(point => point is null))
            throw new InvalidDataException($"World-map snapshot must contain at most {MaximumPointCount} non-null points.");

        var identities = new HashSet<(int WorldId, int ServerId, int Id)>();
        foreach (var point in Points)
        {
            if (point.Id < 0 || point.PointType < 0 || point.Uuid < 0
                || point.ServerId < 0 || point.SrcServerId < 0 || point.WorldId < 0)
                throw new InvalidDataException("World-map snapshot contains a negative identity or routing value.");
            if (!identities.Add((point.WorldId, point.ServerId, point.Id)))
                throw new InvalidDataException("World-map snapshot contains a duplicate world/server/point identity.");

            ValidateBuild(point.BuildInfo);
            ValidateRoad(point.RoadInfo);
            ValidateCollectResource(point.CollectResourceInfo);
            ValidateResource(point.ResourceInfo);
            ValidateEventPoint(point.ExplorePointInfo, "explore");
            ValidateEventPoint(point.SamplePointInfo, "sample");
            ValidateGarbage(point.GarbagePointInfo);
        }
    }

    private static void ValidateBuild(CurrentWorldMapBuildInfo? value)
    {
        if (value is null) return;
        ValidateString(value.OwnerUid, "build owner UID");
        ValidateString(value.AllianceId, "build alliance ID");
        ValidateString(value.Name, "build name");
        ValidateString(value.AllianceAbbreviation, "build alliance abbreviation");
        ValidateString(value.PositionId, "build position ID");
        if (value.Uuid < 0 || value.BuildId < 0 || value.Level < 0)
            throw new InvalidDataException("World-map build payload contains a negative identity or level.");
    }

    private static void ValidateRoad(CurrentWorldMapRoadInfo? value)
    {
        if (value is null) return;
        ValidateString(value.OwnerUid, "road owner UID");
        ValidateString(value.AllianceId, "road alliance ID");
        if (value.Uuid < 0)
            throw new InvalidDataException("World-map road payload contains a negative UUID.");
    }

    private static void ValidateCollectResource(CurrentWorldMapCollectResourceInfo? value)
    {
        if (value is null) return;
        if (value.ResourceType < 0 || value.Level < 0 || value.Type < 0 || value.AttachId < 0)
            throw new InvalidDataException("World-map collect-resource payload contains a negative identifier or level.");
    }

    private static void ValidateResource(CurrentWorldMapResourceInfo? value)
    {
        if (value is null) return;
        if (value.ResourceId < 0 || value.GatherUuid < 0)
            throw new InvalidDataException("World-map resource payload contains a negative identifier or UUID.");
    }

    private static void ValidateEventPoint(CurrentWorldMapEventPointInfo? value, string label)
    {
        if (value is null) return;
        ValidateString(value.OwnerUid, $"{label} owner UID");
        ValidateString(value.EventId, $"{label} event ID");
        if (value.Uuid < 0)
            throw new InvalidDataException($"World-map {label} payload contains a negative UUID.");
    }

    private static void ValidateGarbage(CurrentWorldMapGarbagePointInfo? value)
    {
        if (value is null) return;
        ValidateString(value.OwnerUid, "garbage owner UID");
        ValidateString(value.EventId, "garbage event ID");
        if (value.Uuid < 0 || value.EndTime < 0)
            throw new InvalidDataException("World-map garbage payload contains a negative UUID or end time.");
    }

    private static void ValidateString(string value, string label)
    {
        if (value is null || value.Length > MaximumStringLength)
            throw new InvalidDataException($"World-map {label} is invalid.");
    }

    private static void ValidateFreshTimestamp(
        DateTimeOffset value,
        DateTimeOffset now,
        TimeSpan maxAge,
        TimeSpan futureTolerance,
        string label)
    {
        if (value < now - maxAge)
            throw new InvalidDataException($"World-map snapshot {label} timestamp is stale.");
        if (value > now + futureTolerance)
            throw new InvalidDataException($"World-map snapshot {label} timestamp is too far in the future.");
    }
}
