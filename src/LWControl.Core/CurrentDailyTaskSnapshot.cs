namespace LWControl.Core;

public sealed record CurrentDailyTaskSnapshotTask
{
    public required string TaskId { get; init; }
    public required CurrentTaskState State { get; init; }
    public int? TemplatePoint { get; init; }
}

public sealed record CurrentDailyTaskBoxSnapshot
{
    public required int Index { get; init; }
    public required int ActivationPoint { get; init; }
    public required CurrentTaskState State { get; init; }
}

public sealed record CurrentDailyTaskSnapshotHeartbeat
{
    public required string ProbeVersion { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
}

/// <summary>
/// Read-only interchange contract for the current-game daily-task probe.
/// The game-derived fields are validated against CurrentDailyTaskState before use.
/// </summary>
public sealed record CurrentDailyTaskSnapshot
{
    public const int SupportedSchemaVersion = 1;
    public const string SupportedMode = "state";
    public const int DailyTaskLimit = 1_000;
    public const int MaximumPointValue = 1_000_000;
    public const int MaximumCurrentPoint = DailyTaskLimit * MaximumPointValue;
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DefaultFutureTolerance = TimeSpan.FromSeconds(5);

    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required string CaptureId { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required CurrentDailyTaskSnapshotHeartbeat Heartbeat { get; init; }
    public required IReadOnlyList<CurrentDailyTaskSnapshotTask> Tasks { get; init; }
    public required int CurrentPoint { get; init; }
    public required IReadOnlyList<int> ReceivedStages { get; init; }
    public required IReadOnlyList<CurrentDailyTaskBoxSnapshot> Boxes { get; init; }

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
            throw new InvalidDataException($"Unsupported daily-task snapshot schema version {SchemaVersion}.");
        if (!string.Equals(Mode, SupportedMode, StringComparison.Ordinal))
            throw new InvalidDataException("Daily-task snapshot is not a read-only state capture.");
        if (string.IsNullOrWhiteSpace(CaptureId) || CaptureId.Length > 128)
            throw new InvalidDataException("Daily-task snapshot capture ID is invalid.");
        ValidateFreshTimestamp(CapturedAt, now, ageLimit, futureLimit, "capture");

        if (Heartbeat is null || string.IsNullOrWhiteSpace(Heartbeat.ProbeVersion) || Heartbeat.ProbeVersion.Length > 128)
            throw new InvalidDataException("Daily-task snapshot heartbeat metadata is invalid.");
        ValidateFreshTimestamp(Heartbeat.ObservedAt, now, ageLimit, futureLimit, "heartbeat");

        if (Tasks is null || Tasks.Count > DailyTaskLimit || Tasks.Any(task => task is null))
            throw new InvalidDataException($"Daily-task snapshot must contain at most {DailyTaskLimit} non-null tasks.");

        var taskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.TaskId) || task.TaskId.Length > 200 || !taskIds.Add(task.TaskId))
                throw new InvalidDataException("Daily-task snapshot contains a missing, oversized, or duplicate task ID.");
            if (!Enum.IsDefined(task.State))
                throw new InvalidDataException($"Daily-task snapshot contains an unknown task state for {task.TaskId}.");
            if (task.TemplatePoint is < 0 or > MaximumPointValue)
                throw new InvalidDataException($"Daily-task snapshot contains an invalid template point for {task.TaskId}.");
        }

        if (CurrentPoint is < 0 or > MaximumCurrentPoint)
            throw new InvalidDataException("Daily-task snapshot current point value is invalid.");
        int derivedCurrentPoint = CurrentDailyTaskState.GetCurValue(
            Tasks.Select(task => new CurrentDailyTaskPointState(task.State, task.TemplatePoint)));
        if (CurrentPoint != derivedCurrentPoint)
            throw new InvalidDataException(
                $"Daily-task snapshot current point mismatch: exported {CurrentPoint}, derived {derivedCurrentPoint}.");

        if (ReceivedStages is null || ReceivedStages.Count > CurrentDailyTaskState.DailyBoxCount
            || ReceivedStages.Any(index => index is < 1 or > CurrentDailyTaskState.DailyBoxCount)
            || ReceivedStages.Distinct().Count() != ReceivedStages.Count)
            throw new InvalidDataException("Daily-task snapshot received-stage list is invalid.");

        if (Boxes is null || Boxes.Count != CurrentDailyTaskState.DailyBoxCount || Boxes.Any(box => box is null))
            throw new InvalidDataException("Daily-task snapshot must contain exactly five non-null box states.");

        var thresholds = new Dictionary<int, int>();
        foreach (var box in Boxes)
        {
            if (box.Index is < 1 or > CurrentDailyTaskState.DailyBoxCount || !thresholds.TryAdd(box.Index, box.ActivationPoint))
                throw new InvalidDataException("Daily-task snapshot contains a missing, duplicate, or out-of-range box index.");
            if (box.ActivationPoint is < 0 or > MaximumPointValue)
                throw new InvalidDataException($"Daily-task snapshot contains an invalid threshold for box {box.Index}.");
            if (!Enum.IsDefined(box.State))
                throw new InvalidDataException($"Daily-task snapshot contains an unknown state for box {box.Index}.");
        }

        for (int index = 1; index <= CurrentDailyTaskState.DailyBoxCount; index++)
        {
            if (!thresholds.ContainsKey(index))
                throw new InvalidDataException($"Daily-task snapshot is missing box index {index}.");
        }

        foreach (var box in Boxes)
        {
            var derived = CurrentDailyTaskState.GetBoxState(box.Index, CurrentPoint, ReceivedStages, thresholds);
            if (box.State != derived)
                throw new InvalidDataException(
                    $"Daily-task snapshot box {box.Index} state mismatch: exported {box.State}, derived {derived}.");
        }
    }

    private static void ValidateFreshTimestamp(
        DateTimeOffset value,
        DateTimeOffset now,
        TimeSpan maxAge,
        TimeSpan futureTolerance,
        string label)
    {
        if (value < now - maxAge)
            throw new InvalidDataException($"Daily-task snapshot {label} timestamp is stale.");
        if (value > now + futureTolerance)
            throw new InvalidDataException($"Daily-task snapshot {label} timestamp is too far in the future.");
    }
}
