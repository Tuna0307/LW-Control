namespace LWBridge.Desktop;

/// <summary>
/// Identifies an OS-timed *candidate* that may have followed one task-owned
/// injected key press. The game-intent producer currently supplies only
/// Kind/Slot, not a unique originating Windows event or request nonce; this
/// classifier NEVER establishes that injected input caused the callback.
/// </summary>
internal static class Feature018KeyProofCorrelation
{
    internal static bool CapturedIntentCandidate(
        Feature018OwnedKeyReceipt receipt, Feature018CapturedIntent eventReceipt,
        string expectedCaptureId, long priorSequence, Feature018HotkeyIntent expected)
    {
        if (string.IsNullOrEmpty(expectedCaptureId) ||
            eventReceipt.CaptureId != expectedCaptureId ||
            eventReceipt.Sequence <= priorSequence ||
            eventReceipt.Kind != expected.Kind || eventReceipt.Slot != expected.Slot ||
            eventReceipt.GamePid != receipt.GamePid ||
            eventReceipt.ForegroundHwnd != receipt.Hwnd || !receipt.Pressed ||
            receipt.InputInjectionStartedAtUtc is not { } injection ||
            receipt.KeyDownObservedAtUtc is not { } down ||
            receipt.KeyReleaseObservedAtUtc is not { } release ||
            receipt.KeyDownStateSamples < 3)
            return false;
        // A timer recognition is independently captured only when its poll
        // happened during the OS-observed held-key interval. An overlapping
        // human press remains theoretically indistinguishable, so this result
        // cannot establish the originating device or any native gameplay ACK.
        return eventReceipt.ObservedAtUtc >= injection &&
               eventReceipt.ObservedAtUtc <= release &&
               down <= release;
    }

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
