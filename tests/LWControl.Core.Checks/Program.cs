using System.Text.Json;
using LWControl.Core;

var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
var settings = new DailyClaimSettings { Enabled = true, EnabledKinds = [ClaimKind.VipDailyReward, ClaimKind.DailyTaskChest] };
var observation = new ClaimObservation
{
    SourceKey = "test-vip", RewardName = "Test reward", Kind = ClaimKind.VipDailyReward,
    CapturedAt = now, Status = ClaimSourceStatus.Claimable, FreeConfirmed = true,
    CurrencyCost = 0m, RemainingFreeClaims = 1, ClaimButtonVisible = true, ClaimButtonSemantic = "free"
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
cases.Add(("Recovered defaults are disabled with all seven categories and limit twenty", () =>
{
    var defaults = new DailyClaimSettings();
    Check(!defaults.Enabled);
    Check(defaults.EnabledKinds.SetEquals(Enum.GetValues<ClaimKind>()));
    Check(defaults.MaximumClaimsPerRun == 20);
    Check(!Selected(observation, defaults));
}));
cases.Add(("Category selection is respected", () => Check(!Selected(observation, settings with { EnabledKinds = [] }))));
cases.Add(("Unknown, nonzero, and flagged costs are blocked", () =>
{
    Check(!Selected(observation with { CurrencyCost = null }));
    Check(!Selected(observation with { CurrencyCost = 1 }));
    Check(!Selected(observation with { CurrencyCost = -1 }));
    Check(!Selected(observation with { HasCurrencyCost = true }));
}));
cases.Add(("Stale and future observations are blocked", () =>
{
    Check(!Selected(observation with { CapturedAt = now.AddSeconds(-61) }));
    Check(!Selected(observation with { CapturedAt = now.AddSeconds(1) }));
    Check(Selected(observation with { CapturedAt = now.AddSeconds(-60) }));
}));
cases.Add(("Expiry is enforced", () => Check(!Selected(observation with { ExpiresAt = now }))));
cases.Add(("Recovered runtime free evidence paths are matched", () =>
{
    Check(Selected(observation with { RemainingFreeClaims = 1, FreeConfirmed = null, ClaimButtonSemantic = null }));
    Check(Selected(observation with { RemainingFreeClaims = null, FreeConfirmed = true, ClaimButtonSemantic = "free" }));
    Check(Selected(observation with { RemainingFreeClaims = null, FreeConfirmed = true, ClaimButtonSemantic = "claim" }));
    Check(!Selected(observation with
    {
        RemainingFreeClaims = 0, FreeConfirmed = false, ClaimButtonSemantic = null
    }));
    Check(!Selected(observation with { RemainingFreeClaims = null, ClaimButtonSemantic = "免费" }));
    Check(Selected(observation with { ClaimButtonVisible = false }));
}));
cases.Add(("Only runtime Claimable status is eligible", () =>
{
    Check(!Selected(observation with { Status = ClaimSourceStatus.AlreadyClaimed }));
    Check(!Selected(observation with { Status = ClaimSourceStatus.RouteUnconfirmed }));
    Check(!Selected(observation with { Status = ClaimSourceStatus.BlockedByPaidRequirement }));
}));
cases.Add(("Duplicate source keys are rejected", () =>
    Check(DailyClaimPlanner.Build(settings, [observation, observation], now).Decisions.All(d => !d.Selected))));
cases.Add(("Invalid observation identity is rejected", () =>
{
    Check(!Selected(observation with { SourceKey = " " }));
    Check(!Selected(observation with { Kind = (ClaimKind)999 }));
}));
cases.Add(("Runtime ordering ignores expiry and uses stable identity", () =>
{
    var normal = observation with { SourceKey = "a-normal" };
    var expiring = observation with { SourceKey = "z-expires", ExpiresAt = now.AddMinutes(5) };
    var plan = DailyClaimPlanner.Build(settings with { MaximumClaimsPerRun = 1 }, [expiring, normal], now);
    Check(plan.Decisions.Count(d => d.Selected) == 1);
    Check(plan.Decisions.Single(d => d.Selected).Observation.SourceKey == "a-normal");
    Check(plan.Mode == "preview-only/recovered-runtime-policy");
}));
cases.Add(("Recovered adapter priority favors task chests", () =>
{
    var chest = observation with { SourceKey = "z-chest", Kind = ClaimKind.DailyTaskChest };
    var plan = DailyClaimPlanner.Build(settings with { MaximumClaimsPerRun = 1 }, [observation, chest], now);
    Check(plan.Decisions.Single(d => d.Selected).Observation.Kind == ClaimKind.DailyTaskChest);
    Check(RecoveredDailyClaimPolicy.Priority(ClaimKind.DailyTaskChest) == 900);
    Check(RecoveredDailyClaimPolicy.Priority(ClaimKind.CampaignIdleReward) == 500);
}));
cases.Add(("Invalid settings fail validation", () =>
{
    Throws<ArgumentException>(() => (settings with { MaximumClaimsPerRun = 0 }).Validate());
    Throws<ArgumentException>(() => (settings with { MaximumClaimsPerRun = 21 }).Validate());
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
cases.Add(("Recovered bridge heartbeat health contract is enforced", () =>
{
    var checkedAt = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    var startedAt = checkedAt.AddMinutes(-2);
    Check(LocalBridgeInspector.IsBridgeHealthy(true, startedAt, checkedAt.AddSeconds(-15), checkedAt));
    Check(!LocalBridgeInspector.IsBridgeHealthy(true, startedAt, checkedAt.AddSeconds(-16), checkedAt));
    Check(!LocalBridgeInspector.IsBridgeHealthy(true, startedAt, checkedAt.AddSeconds(6), checkedAt));
    Check(!LocalBridgeInspector.IsBridgeHealthy(false, startedAt, checkedAt, checkedAt));
    Check(!LocalBridgeInspector.IsBridgeHealthy(true, checkedAt, checkedAt.AddSeconds(-3), checkedAt));
}));

int failed = 0;
foreach (var test in cases)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{cases.Count - failed}/{cases.Count} checks passed.");
return failed == 0 ? 0 : 1;
