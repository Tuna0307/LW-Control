namespace LWBridge.Desktop;

// OVL-05 recovery policy. Thresholds, retry tables and caps are 0.3.17 facts re-proved from
// lwbridge-0.3.17.exe (HOME009 analysis; see the research archive tag
// archive/research-offline-controller).
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

}

internal sealed record OverviewRecoveryStatus(
    string State,
    string? Reason,
    bool UpdateDetected,
    bool Restarted,
    long StartedAt,
    long? CompletedAt,
    int Attempts,
    long? NextRetryAt,
    string? Error,
    ulong NoticeId,
    bool NoticeVisible);