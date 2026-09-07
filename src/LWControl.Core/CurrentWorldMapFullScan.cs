using System.Globalization;
using System.Text.Json;

namespace LWControl.Core;

public sealed record CurrentWorldMapScanShield
{
    public bool Known { get; init; }
    public bool Active { get; init; }
    public long? ExpiresAt { get; init; }
    public long RemainingSeconds { get; init; }
    public string? Source { get; init; }
}

public sealed record CurrentWorldMapScanRecord
{
    public required int Id { get; init; }
    public required int PointId { get; init; }
    public int? PointType { get; init; }
    public required string Kind { get; init; }
    public string? Uuid { get; init; }
    public required int ServerId { get; init; }
    public required int SrcServerId { get; init; }
    public required int WorldId { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public string? Name { get; init; }
    public string? PlayerName { get; init; }
    public string? PlayerId { get; init; }
    public string? Alliance { get; init; }
    public string? AllianceId { get; init; }
    public long? Level { get; init; }
    public long? Power { get; init; }
    public string? PowerSource { get; init; }
    public string? MonsterId { get; init; }
    public string? MonsterSpecialType { get; init; }
    public long? RecommendedPower { get; init; }
    public string? RecommendedPowerSource { get; init; }
    public string? ResourceTypeId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourcePointType { get; init; }
    public bool ResourceGettersObserved { get; init; }
    public long? ResourceRemaining { get; init; }
    public long? ResourceCapacity { get; init; }
    public string? ResourceAmountSource { get; init; }
    public long? GatherSeconds { get; init; }
    public string? GatherTimeStatus { get; init; }
    public CurrentWorldMapScanShield? Shield { get; init; }
    public required string Source { get; init; }

    public string DisplayName => FirstNonEmpty(
        PlayerName,
        Name,
        Kind == "monster" && !string.IsNullOrWhiteSpace(MonsterId) ? $"Monster {MonsterId}" : null,
        Kind == "resource_point" && !string.IsNullOrWhiteSpace(ResourceType) ? ResourceType : null,
        $"Point {PointId}");

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!.Trim();
}

public sealed record CurrentWorldMapFullScanResult
{
    public const int SupportedSchemaVersion = 2;
    public const string SupportedState = "proven";
    public const int ExpectedLogicalBlocks = 10_000;
    public const int ExpectedBatchCount = 65;
    public const int ExpectedMaximumConcurrency = 8;
    public const int ExpectedMonsterCameraViews = 500;
    public const string ExpectedMonsterDiscoverySource = "PushWorldMarchWorldGet.serverMarchArr.marchInfos";
    public const int MaximumRecords = 30_000;
    public const int MaximumResultBytes = 64 * 1024 * 1024;

    private static readonly HashSet<string> SupportedKinds =
    [
        "player_base", "resource_point", "monster", "alliance_building", "world_point"
    ];

    public required int SchemaVersion { get; init; }
    public required string ProbeVersion { get; init; }
    public required string State { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required int RequestedBlockCount { get; init; }
    public required int CoveredBlockCount { get; init; }
    public required int RequestSentCount { get; init; }
    public required int CompletedBatchCount { get; init; }
    public required int MaximumConcurrency { get; init; }
    public required int FullScanPeakInflight { get; init; }
    public required int AccumulatedRecordCount { get; init; }
    public required int DuplicateRecordCount { get; init; }
    public required string MonsterDiscoverySource { get; init; }
    public required int DirectMonsterRecordCount { get; init; }
    public required int CameraMoveCount { get; init; }
    public required bool CameraRestored { get; init; }
    public required int MonsterCameraViewCount { get; init; }
    public required int MonsterOfficialRequestCount { get; init; }
    public required int MonsterMarchRequestCount { get; init; }
    public required int MonsterMarchResponseCount { get; init; }
    public required int MonsterMarchForeignSendCount { get; init; }
    public required int MonsterMarchDuplicatePushCount { get; init; }
    public required bool MonsterMarchHookRestored { get; init; }
    public required int MonsterCaptureCount { get; init; }
    public required int MonsterViewsWithMarches { get; init; }
    public required int MonsterViewsWithBosses { get; init; }
    public required int MonsterMaxMarchCount { get; init; }
    public required int RetryCount { get; init; }
    public required bool ResponseHookRestored { get; init; }
    public required bool ManagerFlagRestored { get; init; }
    public required bool WorldResponseFlagRestored { get; init; }
    public required IReadOnlyList<CurrentWorldMapScanRecord> PointRecords { get; init; }

    public void Validate()
    {
        if (SchemaVersion != SupportedSchemaVersion || State != SupportedState)
            throw new InvalidDataException("World Scan result schema/state is unsupported.");
        if (string.IsNullOrWhiteSpace(ProbeVersion) || ProbeVersion.Length > 128)
            throw new InvalidDataException("World Scan probe version is invalid.");
        if (RequestedBlockCount != ExpectedLogicalBlocks || CoveredBlockCount != ExpectedLogicalBlocks)
            throw new InvalidDataException("World Scan result does not prove complete 100x100 coverage.");
        if (RequestSentCount != ExpectedBatchCount || CompletedBatchCount != ExpectedBatchCount)
            throw new InvalidDataException("World Scan result does not contain the recovered 65-batch completion.");
        if (MaximumConcurrency != ExpectedMaximumConcurrency
            || FullScanPeakInflight is < 1 or > ExpectedMaximumConcurrency)
            throw new InvalidDataException("World Scan concurrency evidence is invalid.");
        if (MonsterDiscoverySource != ExpectedMonsterDiscoverySource || DirectMonsterRecordCount < 0
            || CameraMoveCount != ExpectedMonsterCameraViews
            || MonsterCameraViewCount != ExpectedMonsterCameraViews
            || MonsterOfficialRequestCount != ExpectedMonsterCameraViews
            || MonsterMarchRequestCount != 0
            || MonsterMarchResponseCount != ExpectedMonsterCameraViews
            || MonsterMarchForeignSendCount != 0 || MonsterMarchDuplicatePushCount < 0
            || !MonsterMarchHookRestored
            || MonsterCaptureCount != ExpectedMonsterCameraViews
            || MonsterViewsWithMarches < 1
            || MonsterViewsWithBosses < 1
            || MonsterMaxMarchCount < 1
            || RetryCount != 0 || !CameraRestored || !ResponseHookRestored
            || !ManagerFlagRestored || !WorldResponseFlagRestored)
            throw new InvalidDataException("World Scan result failed its bounded cleanup contract.");
        if (AccumulatedRecordCount < 0 || AccumulatedRecordCount > MaximumRecords
            || PointRecords.Count != AccumulatedRecordCount || DuplicateRecordCount < 0)
            throw new InvalidDataException("World Scan point-record counters are invalid.");

        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in PointRecords)
        {
            if (!SupportedKinds.Contains(record.Kind))
                throw new InvalidDataException($"World Scan record kind '{record.Kind}' is unsupported.");
            if (record.Id < 0 || record.PointId < 0 || record.ServerId < 0 || record.SrcServerId < 0
                || record.WorldId < 0 || record.X is < 0 or > 999 || record.Y is < 0 or > 999)
                throw new InvalidDataException("World Scan record identity/routing/coordinate is outside the normal-map contract.");
            if (record.PointType < 0 || record.Level < 0 || record.Power < 0 || record.RecommendedPower < 0
                || record.ResourceRemaining < 0 || record.ResourceCapacity < 0 || record.GatherSeconds < 0)
                throw new InvalidDataException("World Scan record contains a negative recovered value.");
            if (string.IsNullOrWhiteSpace(record.Source) || record.Source.Length > 256)
                throw new InvalidDataException("World Scan record source is invalid.");
            string identity = record.Kind == "monster"
                && record.Source != "WorldPointManager._pointInfos"
                && IsMeaningfulIdentity(record.Uuid)
                ? $"monster:{record.Uuid}"
                : $"{record.WorldId}:{record.ServerId}:{record.Id}";
            if (!identities.Add(identity))
                throw new InvalidDataException("World Scan result contains a duplicate accumulated identity.");
        }
        int directMonsters = PointRecords.Count(record =>
            record.Kind == "monster" && record.Source == "WorldPointManager._pointInfos");
        if (directMonsters != DirectMonsterRecordCount)
            throw new InvalidDataException("World Scan direct monster count is inconsistent.");
        string[] requiredKinds = ["player_base", "resource_point", "alliance_building", "monster"];
        if (requiredKinds.Any(kind => !PointRecords.Any(record => record.Kind == kind)))
            throw new InvalidDataException("World Scan result is missing a core record category.");
    }

    public static CurrentWorldMapFullScanResult Read(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        if (!file.Exists) throw new FileNotFoundException("World Scan result file is missing.", file.FullName);
        if (file.Length <= 0 || file.Length > MaximumResultBytes)
            throw new InvalidDataException("World Scan result file is outside the bounded size limit.");
        using var stream = file.OpenRead();
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        var recordsElement = Required(root, "point_records", JsonValueKind.Array);
        if (recordsElement.GetArrayLength() > MaximumRecords)
            throw new InvalidDataException("World Scan result exceeds the point-record limit.");
        var records = new List<CurrentWorldMapScanRecord>(recordsElement.GetArrayLength());
        foreach (var row in recordsElement.EnumerateArray()) records.Add(ReadRecord(row));
        if (!DateTimeOffset.TryParse(RequiredString(root, "capturedAt"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var capturedAt))
            throw new InvalidDataException("World Scan capturedAt is invalid.");
        var result = new CurrentWorldMapFullScanResult
        {
            SchemaVersion = RequiredInt32(root, "schemaVersion"),
            ProbeVersion = RequiredString(root, "probeVersion"),
            State = RequiredString(root, "state"),
            CapturedAt = capturedAt,
            RequestedBlockCount = RequiredInt32(root, "requested_block_count"),
            CoveredBlockCount = RequiredInt32(root, "covered_block_count"),
            RequestSentCount = RequiredInt32(root, "request_sent_count"),
            CompletedBatchCount = RequiredInt32(root, "completed_batch_count"),
            MaximumConcurrency = RequiredInt32(root, "maximum_concurrency"),
            FullScanPeakInflight = RequiredInt32(root, "full_scan_peak_inflight"),
            AccumulatedRecordCount = RequiredInt32(root, "accumulated_record_count"),
            DuplicateRecordCount = OptionalInt64(root, "duplicate_record_count") is long duplicates
                ? checked((int)duplicates) : 0,
            MonsterDiscoverySource = RequiredString(root, "monster_discovery_source"),
            DirectMonsterRecordCount = RequiredInt32(root, "direct_monster_record_count"),
            CameraMoveCount = RequiredInt32(root, "camera_move_count"),
            CameraRestored = RequiredBool(root, "camera_restored"),
            MonsterCameraViewCount = RequiredInt32(root, "monster_camera_view_count"),
            MonsterOfficialRequestCount = RequiredInt32(root, "monster_official_request_count"),
            MonsterMarchRequestCount = RequiredInt32(root, "monster_march_request_count"),
            MonsterMarchResponseCount = RequiredInt32(root, "monster_march_response_count"),
            MonsterMarchForeignSendCount = RequiredInt32(root, "monster_march_foreign_send_count"),
            MonsterMarchDuplicatePushCount = RequiredInt32(root, "monster_march_duplicate_push_count"),
            MonsterMarchHookRestored = RequiredBool(root, "monster_march_hook_restored"),
            MonsterCaptureCount = RequiredInt32(root, "monster_capture_count"),
            MonsterViewsWithMarches = RequiredInt32(root, "monster_views_with_marches"),
            MonsterViewsWithBosses = RequiredInt32(root, "monster_views_with_bosses"),
            MonsterMaxMarchCount = RequiredInt32(root, "monster_max_march_count"),
            RetryCount = RequiredInt32(root, "retry_count"),
            ResponseHookRestored = RequiredBool(root, "response_hook_restored"),
            ManagerFlagRestored = RequiredBool(root, "manager_flag_restored"),
            WorldResponseFlagRestored = RequiredBool(root, "world_response_flag_restored"),
            PointRecords = records,
        };
        result.Validate();
        return result;
    }

    private static CurrentWorldMapScanRecord ReadRecord(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("World Scan point record is not an object.");
        var shield = row.TryGetProperty("shield", out var shieldElement)
            && shieldElement.ValueKind == JsonValueKind.Object
            ? new CurrentWorldMapScanShield
            {
                Known = OptionalBool(shieldElement, "known") ?? false,
                Active = OptionalBool(shieldElement, "active") ?? false,
                ExpiresAt = OptionalInt64(shieldElement, "expiresAt"),
                RemainingSeconds = OptionalInt64(shieldElement, "remainingSeconds") ?? 0,
                Source = OptionalScalarString(shieldElement, "source"),
            }
            : null;
        int id = RequiredInt32(row, "id");
        return new CurrentWorldMapScanRecord
        {
            Id = id,
            PointId = OptionalInt64(row, "pointId") is long pointId ? checked((int)pointId) : id,
            PointType = OptionalInt64(row, "pointType") is long pointType ? checked((int)pointType) : null,
            Kind = RequiredString(row, "kind"),
            Uuid = OptionalScalarString(row, "uuid"),
            ServerId = OptionalInt64(row, "serverId") is long serverId ? checked((int)serverId) : 0,
            SrcServerId = OptionalInt64(row, "srcServerId") is long srcServerId ? checked((int)srcServerId) : 0,
            WorldId = OptionalInt64(row, "worldId") is long worldId ? checked((int)worldId) : 0,
            X = RequiredInt32(row, "x"),
            Y = RequiredInt32(row, "y"),
            Name = OptionalScalarString(row, "name"),
            PlayerName = OptionalScalarString(row, "playerName"),
            PlayerId = OptionalScalarString(row, "playerId"),
            Alliance = OptionalScalarString(row, "alliance"),
            AllianceId = OptionalScalarString(row, "allianceId"),
            Level = OptionalInt64(row, "level"),
            Power = OptionalInt64(row, "power"),
            PowerSource = OptionalScalarString(row, "powerSource"),
            MonsterId = OptionalScalarString(row, "monsterId"),
            MonsterSpecialType = OptionalScalarString(row, "monsterSpecialType"),
            RecommendedPower = OptionalInt64(row, "recommendedPower"),
            RecommendedPowerSource = OptionalScalarString(row, "recommendedPowerSource"),
            ResourceTypeId = OptionalScalarString(row, "resourceTypeId"),
            ResourceType = OptionalScalarString(row, "resourceType"),
            ResourcePointType = OptionalScalarString(row, "resourcePointType"),
            ResourceGettersObserved = OptionalBool(row, "resourceGettersObserved") ?? false,
            ResourceRemaining = OptionalInt64(row, "resourceRemaining"),
            ResourceCapacity = OptionalInt64(row, "resourceCapacity"),
            ResourceAmountSource = OptionalScalarString(row, "resourceAmountSource"),
            GatherSeconds = OptionalInt64(row, "gatherSeconds"),
            GatherTimeStatus = OptionalScalarString(row, "gatherTimeStatus"),
            Shield = shield,
            Source = RequiredString(row, "source"),
        };
    }

    private static JsonElement Required(JsonElement parent, string name, JsonValueKind kind)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != kind)
            throw new InvalidDataException($"World Scan result is missing valid '{name}'.");
        return value;
    }

    private static string RequiredString(JsonElement parent, string name) =>
        Required(parent, name, JsonValueKind.String).GetString()
        ?? throw new InvalidDataException($"World Scan '{name}' is null.");

    private static int RequiredInt32(JsonElement parent, string name) =>
        OptionalInt64(parent, name) is long value
            ? checked((int)value)
            : throw new InvalidDataException($"World Scan result is missing integer '{name}'.");

    private static bool RequiredBool(JsonElement parent, string name) =>
        OptionalBool(parent, name)
        ?? throw new InvalidDataException($"World Scan result is missing boolean '{name}'.");

    private static long? OptionalInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long integer)) return integer;
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) return integer;
        return null;
    }

    private static bool? OptionalBool(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? OptionalScalarString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static bool IsMeaningfulIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value is not "0"
        && !value.Equals("nil", StringComparison.OrdinalIgnoreCase)
        && !value.Equals("null", StringComparison.OrdinalIgnoreCase);
}
