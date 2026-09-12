namespace LWBridge.Desktop;

// OVL-05 build-specific policy recovered from lwbridge-0.3.1.exe.
// The preferred-VA/static proof is tools/inspect_lwbridge_game_recovery.py.
internal static class OverviewRecoveryPolicy
{
    internal static readonly TimeSpan HangThreshold = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DisconnectThreshold = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan LoginUnavailableThreshold = TimeSpan.FromSeconds(180);
    internal static readonly TimeSpan DisconnectWaitBeforeTerminate = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan StableVerification = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan UpdateNoActivityTimeout = TimeSpan.FromMinutes(15);

    internal static readonly TimeSpan[] NormalRetryDelays =
    [
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
    ];

    internal static readonly TimeSpan[] MaintenanceRetryDelays =
    [
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
    ];

    internal static TimeSpan RetryDelay(bool maintenance, int attempt)
    {
        TimeSpan[] table = maintenance ? MaintenanceRetryDelays : NormalRetryDelays;
        int index = Math.Clamp(Math.Max(attempt, 1) - 1, 0, table.Length - 1);
        return table[index];
    }
}

internal sealed record OverviewRecoveryStatus(
    string State,
    string? Reason,
    bool UpdateDetected,
    bool Restarted,
    long? StartedAt,
    long? CompletedAt,
    int Attempts,
    long? NextRetryAt,
    string? Error,
    string? NoticeId,
    bool NoticeVisible);