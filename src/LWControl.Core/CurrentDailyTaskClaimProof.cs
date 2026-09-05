namespace LWControl.Core;

public enum CurrentDailyTaskClaimTargetKind
{
    DailyQuestStage,
    DailyTask
}

public sealed record CurrentDailyTaskClaimCandidate
{
    public required CurrentDailyTaskClaimTargetKind Kind { get; init; }
    public required string CaptureId { get; init; }
    public int? Stage { get; init; }
    public string? TaskId { get; init; }
}

/// <summary>
/// Offline, fail-closed contract for choosing and verifying a future bounded
/// current-game daily-task reward proof. This type does not send game messages.
/// </summary>
public static class CurrentDailyTaskClaimProof
{
    public static IReadOnlyList<CurrentDailyTaskClaimCandidate> EligibleCandidates(
        CurrentDailyTaskSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate(now);

        var output = new List<CurrentDailyTaskClaimCandidate>();
        foreach (var box in snapshot.Boxes
                     .Where(box => box.State == CurrentTaskState.CanReceive)
                     .OrderBy(box => box.Index))
        {
            output.Add(new CurrentDailyTaskClaimCandidate
            {
                Kind = CurrentDailyTaskClaimTargetKind.DailyQuestStage,
                CaptureId = snapshot.CaptureId,
                Stage = box.Index
            });
        }

        foreach (var task in snapshot.Tasks
                     .Where(task => task.State == CurrentTaskState.CanReceive)
                     .OrderBy(task => task.TaskId, StringComparer.Ordinal))
        {
            output.Add(new CurrentDailyTaskClaimCandidate
            {
                Kind = CurrentDailyTaskClaimTargetKind.DailyTask,
                CaptureId = snapshot.CaptureId,
                TaskId = task.TaskId
            });
        }

        return output;
    }

    public static CurrentDailyTaskClaimCandidate? SelectOne(
        CurrentDailyTaskSnapshot snapshot,
        DateTimeOffset now) => EligibleCandidates(snapshot, now).FirstOrDefault();

    public static bool EffectConfirmed(
        CurrentDailyTaskClaimCandidate candidate,
        CurrentDailyTaskSnapshot before,
        CurrentDailyTaskSnapshot after,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        before.Validate(now);
        after.Validate(now);

        if (!string.Equals(candidate.CaptureId, before.CaptureId, StringComparison.Ordinal))
            return false;
        if (after.CapturedAt < before.CapturedAt)
            return false;

        return candidate.Kind switch
        {
            CurrentDailyTaskClaimTargetKind.DailyQuestStage =>
                StageEffectConfirmed(candidate.Stage, before, after),
            CurrentDailyTaskClaimTargetKind.DailyTask =>
                TaskEffectConfirmed(candidate.TaskId, before, after),
            _ => false
        };
    }

    private static bool StageEffectConfirmed(
        int? stage,
        CurrentDailyTaskSnapshot before,
        CurrentDailyTaskSnapshot after)
    {
        if (!stage.HasValue || stage.Value is < 1 or > CurrentDailyTaskState.DailyBoxCount)
            return false;

        int stageValue = stage.Value;
        var beforeBox = before.Boxes.SingleOrDefault(box => box.Index == stageValue);
        var afterBox = after.Boxes.SingleOrDefault(box => box.Index == stageValue);
        return beforeBox?.State == CurrentTaskState.CanReceive
            && !before.ReceivedStages.Contains(stageValue)
            && afterBox?.State == CurrentTaskState.Received
            && after.ReceivedStages.Contains(stageValue);
    }

    private static bool TaskEffectConfirmed(
        string? taskId,
        CurrentDailyTaskSnapshot before,
        CurrentDailyTaskSnapshot after)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return false;

        var beforeTask = before.Tasks.SingleOrDefault(task =>
            string.Equals(task.TaskId, taskId, StringComparison.Ordinal));
        var afterTask = after.Tasks.SingleOrDefault(task =>
            string.Equals(task.TaskId, taskId, StringComparison.Ordinal));
        return beforeTask?.State == CurrentTaskState.CanReceive
            && afterTask?.State == CurrentTaskState.Received;
    }
}
