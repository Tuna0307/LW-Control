using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018KeyProofCorrelationChecks
{
    internal static void Run()
    {
        DateTimeOffset t = new(2026, 10, 17, 8, 30, 0, TimeSpan.Zero);
        Feature018OwnedKeyReceipt receipt = new("F6", 4208, 9911, true,
            "win32_owned_window_key_state_seen")
        {
            InputBaselineObservedAtUtc = t,
            InputInjectionStartedAtUtc = t.AddMilliseconds(100),
            KeyDownObservedAtUtc = t.AddMilliseconds(145),
            KeyDownLastObservedAtUtc = t.AddMilliseconds(285),
            KeyReleaseObservedAtUtc = t.AddMilliseconds(360),
            KeyDownStateSamples = 3,
        };
        static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidDataException("FEATURE018_KEY_PROOF: " + reason);
        }
        bool Candidate(Feature018OwnedKeyReceipt r, string kind = "UseShield", int slot = 8,
            DateTimeOffset? completed = null) =>
            Feature018KeyProofCorrelation.TemporalCandidate(r, kind, slot, "UseShield", 8,
                completed ?? t.AddMilliseconds(400));
        Check(Candidate(receipt), "matching post-key result not retained as temporal candidate");
        Check(!Candidate(receipt with { Pressed = false }), "denied Windows insertion fabricated callback");
        Check(!Candidate(receipt with { KeyDownStateSamples = 2 }),
            "transient OS down sample fabricated stable physical key");
        Check(!Candidate(receipt with { KeyReleaseObservedAtUtc = null }),
            "unreleased physical key fabricated proof");
        Check(!Candidate(receipt with { InputBaselineObservedAtUtc = t.AddSeconds(2) }),
            "impossible OS timestamp order fabricated proof");
        Check(!Candidate(receipt, kind: "RecallSquad"), "wrong intent kind counted");
        Check(!Candidate(receipt, slot: 12), "wrong shield slot counted");
        Check(!Candidate(receipt, completed: t.AddMilliseconds(140)),
            "prior queued kind/slot callback counted as physical key result");
        Check(!Candidate(receipt, completed: t.AddSeconds(13)),
            "unrelated later callback counted as injected physical key result");
        Feature018HotkeyIntent key = new(Feature018HotkeyKind.UseShield, 8);
        var capture = new Feature018CapturedIntent("capture-A", 15, key.Kind, key.Slot,
            4208, 9911, t.AddMilliseconds(180));
        bool Captured(Feature018CapturedIntent current, string scope = "capture-A",
            long baseline = 14, Feature018OwnedKeyReceipt? os = null) =>
            Feature018KeyProofCorrelation.CapturedIntentCandidate(
                os ?? receipt, current, scope, baseline, key);
        Check(Captured(capture),
            "actual capture's independently sequenced event was hidden");
        Check(!Captured(capture, scope: "capture-foreign"),
            "capture from previous owner/session was misattributed");
        Check(!Captured(capture, baseline: 15),
            "preexisting recognizer event was misattributed");
        Check(!Captured(capture with { ForegroundHwnd = 9912 }),
            "foreign game window recognizer event was attributed");
        Check(!Captured(capture with { GamePid = 2212 }),
            "wrong process recognizer event was attributed");
        Check(!Captured(capture with { Kind = Feature018HotkeyKind.RecallSquad }),
            "wrong captured kind was attributed");
        Check(!Captured(capture with { Slot = 12 }),
            "wrong captured slot was attributed");
        Check(!Captured(capture with { ObservedAtUtc = t.AddMilliseconds(99) }),
            "capture event before injection was attributed");
        Check(!Captured(capture with { ObservedAtUtc = t.AddMilliseconds(370) }),
            "capture event after OS release was attributed");
        Check(!Captured(capture, os: receipt with { Pressed = false }),
            "denied OS injection counted a timer recognition");
        Console.WriteLine("FEATURE018_KEY_PROOF_TEMPORAL_ONLY_PASS nativeIntentCorrelation=false");
    }
}
