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
CurrentDailyTaskSnapshot ValidDailyTaskSnapshot(DateTimeOffset capturedAt) => new()
{
    SchemaVersion = CurrentDailyTaskSnapshot.SupportedSchemaVersion,
    Mode = CurrentDailyTaskSnapshot.SupportedMode,
    CaptureId = "capture-test-1",
    CapturedAt = capturedAt,
    Heartbeat = new()
    {
        ProbeVersion = "offline-test-probe-1",
        ObservedAt = capturedAt
    },
    Tasks =
    [
        new() { TaskId = "task-a", State = CurrentTaskState.Received, TemplatePoint = 30 },
        new() { TaskId = "task-b", State = CurrentTaskState.CanReceive, TemplatePoint = 40 },
        new() { TaskId = "task-c", State = CurrentTaskState.NoComplete, TemplatePoint = null }
    ],
    CurrentPoint = 30,
    ReceivedStages = [1],
    Boxes =
    [
        new() { Index = 1, ActivationPoint = 10, State = CurrentTaskState.Received },
        new() { Index = 2, ActivationPoint = 20, State = CurrentTaskState.CanReceive },
        new() { Index = 3, ActivationPoint = 30, State = CurrentTaskState.CanReceive },
        new() { Index = 4, ActivationPoint = 40, State = CurrentTaskState.NoComplete },
        new() { Index = 5, ActivationPoint = 50, State = CurrentTaskState.NoComplete }
    ]
};

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
cases.Add(("Current-game daily chest state contract is mirrored symbolically", () =>
{
    var thresholds = new Dictionary<int, int>
    {
        [1] = 20,
        [2] = 40,
        [3] = 60,
        [4] = 80,
        [5] = 100
    };
    int[] received = [1, 3];

    Check(CurrentDailyTaskState.GetBoxState(1, 50, received, thresholds) == CurrentTaskState.Received);
    Check(CurrentDailyTaskState.GetBoxState(2, 50, received, thresholds) == CurrentTaskState.CanReceive);
    Check(CurrentDailyTaskState.GetBoxState(4, 50, received, thresholds) == CurrentTaskState.NoComplete);
    Check(CurrentDailyTaskState.GetBoxState(9, 999, received, thresholds) == CurrentTaskState.NoComplete);
    Check(!CurrentDailyTaskState.IsAllBoxRewardReceived(999, received, thresholds));
    Check(CurrentDailyTaskState.IsAllBoxRewardReceived(0, [1, 2, 3, 4, 5], thresholds));
}));
cases.Add(("Current-game daily point total uses only received task templates", () =>
{
    CurrentDailyTaskPointState[] tasks =
    [
        new(CurrentTaskState.Received, 10),
        new(CurrentTaskState.CanReceive, 99),
        new(CurrentTaskState.NoComplete, 88),
        new(CurrentTaskState.Received, 20),
        new(CurrentTaskState.Received, null)
    ];
    Check(CurrentDailyTaskState.GetCurValue(tasks) == 30);
}));
cases.Add(("Read-only daily-task snapshot validates derived point and box state", () =>
{
    var snapshot = ValidDailyTaskSnapshot(now);
    snapshot.Validate(now);
    Check(snapshot.CurrentPoint == CurrentDailyTaskState.GetCurValue(
        snapshot.Tasks.Select(task => new CurrentDailyTaskPointState(task.State, task.TemplatePoint))));
}));
cases.Add(("Daily-task snapshot rejects stale, future, unsupported, and action-shaped captures", () =>
{
    Throws<InvalidDataException>(() => ValidDailyTaskSnapshot(now.AddSeconds(-16)).Validate(now));
    Throws<InvalidDataException>(() => ValidDailyTaskSnapshot(now.AddSeconds(6)).Validate(now));
    Throws<InvalidDataException>(() => (ValidDailyTaskSnapshot(now) with { SchemaVersion = 2 }).Validate(now));
    Throws<InvalidDataException>(() => (ValidDailyTaskSnapshot(now) with { Mode = "run_once" }).Validate(now));
    Throws<InvalidDataException>(() => (ValidDailyTaskSnapshot(now) with
    {
        Heartbeat = new() { ProbeVersion = "offline-test-probe-1", ObservedAt = now.AddSeconds(-16) }
    }).Validate(now));
}));
cases.Add(("Daily-task snapshot rejects identity, threshold, and derivation corruption", () =>
{
    var snapshot = ValidDailyTaskSnapshot(now);
    Throws<InvalidDataException>(() => (snapshot with
    {
        Tasks = [.. snapshot.Tasks, snapshot.Tasks[0]]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with { CurrentPoint = 31 }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with { ReceivedStages = [1, 1] }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Boxes = snapshot.Boxes.Select(box => box.Index == 5
            ? box with { ActivationPoint = -1 }
            : box).ToArray()
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Boxes = snapshot.Boxes.Select(box => box.Index == 2
            ? box with { State = CurrentTaskState.NoComplete }
            : box).ToArray()
    }).Validate(now));
}));
cases.Add(("Daily-task snapshot JSON is strict and uses symbolic task states", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"lwcontrol-snapshot-check-{Guid.NewGuid():N}");
    try
    {
        var path = Path.Combine(directory, "snapshot.json");
        var snapshot = ValidDailyTaskSnapshot(now);
        JsonFiles.Write(path, snapshot);
        var text = File.ReadAllText(path);
        Check(text.Contains("\"state\": \"Received\"", StringComparison.Ordinal));
        var loaded = JsonFiles.Read<CurrentDailyTaskSnapshot>(path);
        loaded.Validate(now);

        File.WriteAllText(path, text.Replace(
            "\"captureId\": \"capture-test-1\"",
            "\"captureId\": \"capture-test-1\",\n  \"unexpected\": true",
            StringComparison.Ordinal));
        Throws<JsonException>(() => JsonFiles.Read<CurrentDailyTaskSnapshot>(path));

        JsonFiles.Write(path, snapshot);
        text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"Received\"", "999", StringComparison.Ordinal));
        Throws<JsonException>(() => JsonFiles.Read<CurrentDailyTaskSnapshot>(path));
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}));
cases.Add(("Daily-task claim proof selects only explicit CanReceive targets", () =>
{
    var snapshot = ValidDailyTaskSnapshot(now);
    var candidates = CurrentDailyTaskClaimProof.EligibleCandidates(snapshot, now);
    Check(candidates.Count == 3);
    Check(candidates[0].Kind == CurrentDailyTaskClaimTargetKind.DailyQuestStage && candidates[0].Stage == 2);
    Check(candidates[1].Kind == CurrentDailyTaskClaimTargetKind.DailyQuestStage && candidates[1].Stage == 3);
    Check(candidates[2].Kind == CurrentDailyTaskClaimTargetKind.DailyTask && candidates[2].TaskId == "task-b");
    Check(CurrentDailyTaskClaimProof.SelectOne(snapshot, now) == candidates[0]);
}));
cases.Add(("Daily-task claim proof requires a correlated post-claim state transition", () =>
{
    var before = ValidDailyTaskSnapshot(now.AddSeconds(-1));
    var candidate = CurrentDailyTaskClaimProof.SelectOne(before, now)!;
    var after = before with
    {
        CaptureId = "capture-test-2",
        CapturedAt = now,
        Heartbeat = new() { ProbeVersion = "offline-test-probe-1", ObservedAt = now },
        ReceivedStages = [1, 2],
        Boxes = before.Boxes.Select(box => box.Index == 2
            ? box with { State = CurrentTaskState.Received }
            : box).ToArray()
    };
    Check(CurrentDailyTaskClaimProof.EffectConfirmed(candidate, before, after, now));
    Check(!CurrentDailyTaskClaimProof.EffectConfirmed(
        candidate,
        before,
        after with { ReceivedStages = [1], Boxes = before.Boxes },
        now));
}));

int failed = 0;
foreach (var test in cases)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{cases.Count - failed}/{cases.Count} checks passed.");
return failed == 0 ? 0 : 1;
