using LWBridge.Desktop;

namespace LWBridge.Desktop.Checks;

internal static class Feature018ProofOwnerGraceChecks
{
    internal static void Run()
    {
        DateTimeOffset now = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
        var grace = new Feature018ProofOwnerGrace(TimeSpan.FromSeconds(15), () => now);
        var expected = new Feature018HotkeySession("p", "s", "1001", 2212);
        static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("FEATURE018_PROOF_OWNER_GRACE_FAIL " + reason);
        }

        Check(grace.Check(true, true, expected, expected), "matching account accepted");
        Check(grace.Check(true, true, null, expected), "first unavailable read tolerated");
        now = now.AddSeconds(14);
        Check(grace.Check(true, true, null, expected), "unavailable within grace tolerated");
        Check(grace.Check(true, true, expected, expected), "recovered account resets grace");
        now = now.AddSeconds(30);
        Check(grace.Check(true, true, null, expected), "new gap starts a fresh grace window");
        now = now.AddSeconds(15);
        Check(!grace.Check(true, true, null, expected), "unavailable beyond grace fails");
        Check(!grace.Check(true, true, expected with { GameUid = "2002" }, expected), "different account fails at once");
        Check(!grace.Check(true, false, expected, expected), "lost process/session fails at once");
        Check(!grace.Check(false, true, expected, expected), "unapproved account fails at once");
        Console.WriteLine("FEATURE018_PROOF_OWNER_GRACE_OK");
    }
}
