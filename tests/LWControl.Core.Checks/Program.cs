using System.Text.Json;
using LWControl.Core;

var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
var settings = new DailyClaimSettings { Enabled = true, EnabledKinds = [ClaimKind.VipDailyReward, ClaimKind.DailyTaskChest] };
var observation = new ClaimObservation
{
    SourceKey = "test-vip", RewardName = "Test reward", Kind = ClaimKind.VipDailyReward,
    CapturedAt = now, FreeConfirmed = true, CurrencyCost = 0m, RemainingFreeClaims = 1, ClaimButtonVisible = true
};
var cases = new List<(string Name, Action Run)>();
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
bool Selected(ClaimObservation item, DailyClaimSettings? config = null) =>
    DailyClaimPlanner.Build(config ?? settings, [item], now).Decisions.Single().Selected;

cases.Add(("Confirmed free reward is selected", () => Check(Selected(observation))));
cases.Add(("Disabled by default", () => Check(!Selected(observation, new()))));
cases.Add(("Category selection is respected", () => Check(!Selected(observation, settings with { EnabledKinds = [] }))));
cases.Add(("Unknown and positive costs are blocked", () =>
{
    Check(!Selected(observation with { CurrencyCost = null }));
    Check(!Selected(observation with { CurrencyCost = 1 }));
    Check(!Selected(observation with { CurrencyCost = -1 }));
    Check(!Selected(observation with { FreeConfirmed = null }));
}));
cases.Add(("Stale and future observations are blocked", () =>
{
    Check(!Selected(observation with { CapturedAt = now.AddSeconds(-61) }));
    Check(!Selected(observation with { CapturedAt = now.AddSeconds(1) }));
    Check(Selected(observation with { CapturedAt = now.AddSeconds(-60) }));
}));
cases.Add(("Expiry is enforced", () => Check(!Selected(observation with { ExpiresAt = now }))));
cases.Add(("Availability must be confirmed", () =>
{
    Check(!Selected(observation with { RemainingFreeClaims = null }));
    Check(!Selected(observation with { RemainingFreeClaims = 0 }));
    Check(!Selected(observation with { ClaimButtonVisible = false }));
}));
cases.Add(("Duplicate source keys are rejected", () =>
    Check(DailyClaimPlanner.Build(settings, [observation, observation], now).Decisions.All(d => !d.Selected))));
cases.Add(("Invalid observation identity is rejected", () =>
{
    Check(!Selected(observation with { SourceKey = " " }));
    Check(!Selected(observation with { Kind = (ClaimKind)999 }));
}));
cases.Add(("Limit and priority select the expiring reward", () =>
{
    var expiring = observation with { SourceKey = "expires", ExpiresAt = now.AddMinutes(5) };
    var plan = DailyClaimPlanner.Build(settings with { MaximumClaimsPerRun = 1 }, [observation, expiring], now);
    Check(plan.Decisions.Count(d => d.Selected) == 1);
    Check(plan.Decisions.Single(d => d.Selected).Observation.SourceKey == "expires");
    Check(plan.Mode == "preview-only");
}));
cases.Add(("Chest priority breaks equal expiry ties", () =>
{
    var chest = observation with { SourceKey = "z-chest", Kind = ClaimKind.DailyTaskChest };
    var plan = DailyClaimPlanner.Build(settings with { MaximumClaimsPerRun = 1 }, [observation, chest], now);
    Check(plan.Decisions.Single(d => d.Selected).Observation.Kind == ClaimKind.DailyTaskChest);
}));
cases.Add(("Invalid settings fail validation", () =>
{
    Throws<ArgumentException>(() => (settings with { MaximumClaimsPerRun = 0 }).Validate());
    Throws<ArgumentException>(() => (settings with { MaxSnapshotAgeSeconds = 301 }).Validate());
    Throws<ArgumentException>(() => (settings with { EnabledKinds = [(ClaimKind)999] }).Validate());
}));
cases.Add(("Settings and observations round-trip; malformed JSON is rejected", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"lwcontrol-check-{Guid.NewGuid():N}");
    try
    {
        var path = Path.Combine(directory, "settings.json");
        JsonFiles.Write(path, settings);
        var loaded = JsonFiles.Read<DailyClaimSettings>(path);
        loaded.Validate();
        Check(loaded.EnabledKinds.SetEquals(settings.EnabledKinds));
        JsonFiles.Write(path, new[] { observation });
        Check(JsonFiles.Read<ClaimObservation[]>(path).Single() == observation);
        File.WriteAllText(path, "{\"unknownOption\":true}");
        Throws<JsonException>(() => JsonFiles.Read<DailyClaimSettings>(path));
        File.WriteAllText(path, "[{}]");
        Throws<JsonException>(() => JsonFiles.Read<ClaimObservation[]>(path));
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}));

int failed = 0;
foreach (var test in cases)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{cases.Count - failed}/{cases.Count} checks passed.");
return failed == 0 ? 0 : 1;
