namespace LWControl.Core;

// Symbolic names recovered from the current content-version-12 Lua runtime.
// Numeric TaskState enum values are intentionally not assumed here.
public enum CurrentTaskState
{
    NoComplete,
    CanReceive,
    Received
}

public sealed record CurrentDailyTaskPointState(CurrentTaskState State, int? TemplatePoint);

public static class CurrentDailyTaskState
{
    public const int DailyBoxCount = 5;

    public static int GetCurValue(IEnumerable<CurrentDailyTaskPointState> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        int result = 0;
        foreach (var task in tasks)
        {
            if (task is null)
                throw new ArgumentException("Task state list contains null.", nameof(tasks));
            if (task.State == CurrentTaskState.Received && task.TemplatePoint.HasValue)
                result += task.TemplatePoint.Value;
        }
        return result;
    }

    public static CurrentTaskState GetBoxState(
        int index,
        int curPoint,
        IReadOnlyList<int> curReward,
        IReadOnlyDictionary<int, int> dailyBoxActive)
    {
        ArgumentNullException.ThrowIfNull(curReward);
        ArgumentNullException.ThrowIfNull(dailyBoxActive);

        if (curReward.Contains(index))
            return CurrentTaskState.Received;

        if (dailyBoxActive.TryGetValue(index, out int boxValue) && boxValue <= curPoint)
            return CurrentTaskState.CanReceive;

        return CurrentTaskState.NoComplete;
    }

    public static bool IsAllBoxRewardReceived(
        int curPoint,
        IReadOnlyList<int> curReward,
        IReadOnlyDictionary<int, int> dailyBoxActive)
    {
        for (int index = 1; index <= DailyBoxCount; index++)
        {
            if (GetBoxState(index, curPoint, curReward, dailyBoxActive) != CurrentTaskState.Received)
                return false;
        }

        return true;
    }
}
