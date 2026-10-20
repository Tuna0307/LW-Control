namespace LWBridge.Desktop;

/// <summary>
/// Proof-harness owner check that tolerates a brief heartbeat gap.
/// The exact approved account, owned process and owned session are hard
/// requirements and fail immediately. Only an UNAVAILABLE account read (a
/// heartbeat that is momentarily stale or mid-rewrite) is treated as
/// transient for <see cref="grace"/>; a concrete different account always
/// fails. Product action gates are unchanged: every native request still
/// re-reads the authenticated account itself.
/// </summary>
internal sealed class Feature018ProofOwnerGrace
{
    private readonly TimeSpan grace;
    private readonly Func<DateTimeOffset> clock;
    private DateTimeOffset? unavailableSince;

    internal Feature018ProofOwnerGrace(TimeSpan grace, Func<DateTimeOffset>? clock = null)
    {
        this.grace = grace;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    internal bool Check(bool approvedAccount, bool ownedProcessAndSession,
        Feature018HotkeySession? observed, Feature018HotkeySession expected)
    {
        if (!approvedAccount || !ownedProcessAndSession) return false;
        if (observed is null)
        {
            unavailableSince ??= clock();
            return clock() - unavailableSince.Value < grace;
        }
        unavailableSince = null;
        return observed == expected;
    }
}
