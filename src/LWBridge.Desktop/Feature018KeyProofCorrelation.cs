namespace LWBridge.Desktop;

/// <summary>
/// Identifies an OS-timed *candidate* that may have followed one task-owned
/// injected key press. The game-intent producer currently supplies only
/// Kind/Slot, not a unique originating Windows event or request nonce; this
/// classifier NEVER establishes that injected input caused the callback.
/// </summary>
internal static class Feature018KeyProofCorrelation
{
    internal static bool TemporalCandidate(
        Feature018OwnedKeyReceipt receipt, string actualKind, int actualSlot,
        string expectedKind, int expectedSlot, DateTimeOffset completion)
    {
        if (!receipt.Pressed || receipt.KeyDownStateSamples < 3 ||
            receipt.InputBaselineObservedAtUtc is not { } baseline ||
            receipt.InputInjectionStartedAtUtc is not { } injected ||
            receipt.KeyDownObservedAtUtc is not { } down ||
            receipt.KeyDownLastObservedAtUtc is not { } lastDown ||
            receipt.KeyReleaseObservedAtUtc is not { } released ||
            actualKind != expectedKind || actualSlot != expectedSlot)
            return false;
        // A stale previously enqueued outcome, a bogus OS chronology, or an
        // unrelated outcome too far after release is not even a candidate.
        // The game action may have its own 8-second owned-mailbox timeout.
        return baseline <= injected && injected <= down && down <= lastDown &&
            lastDown <= released && down <= completion &&
            completion <= released.AddSeconds(12);
    }
}
