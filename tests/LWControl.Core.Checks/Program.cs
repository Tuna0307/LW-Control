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
CurrentWorldMapSnapshot ValidWorldMapSnapshot(DateTimeOffset capturedAt) => new()
{
    SchemaVersion = CurrentWorldMapSnapshot.SupportedSchemaVersion,
    Mode = CurrentWorldMapSnapshot.SupportedMode,
    Source = CurrentWorldMapSnapshot.SupportedSource,
    CaptureId = "world-capture-test-1",
    CapturedAt = capturedAt,
    Heartbeat = new()
    {
        ProbeVersion = "offline-world-probe-1",
        ObservedAt = capturedAt
    },
    Points =
    [
        new()
        {
            Id = 123,
            PointType = 4,
            Uuid = 456,
            ServerId = 1,
            SrcServerId = 1,
            WorldId = 2,
            CollectResourceInfo = new()
            {
                ResourceType = 3,
                Level = 8,
                Type = 0,
                AttachId = 0
            }
        },
        new()
        {
            Id = 124,
            PointType = 5,
            Uuid = 457,
            ServerId = 1,
            SrcServerId = 1,
            WorldId = 2,
            ResourceInfo = new()
            {
                ResourceId = 99,
                State = 1,
                GatherUuid = 0
            }
        }
    ]
};
CurrentRallySnapshot ValidRallySyncSnapshot(DateTimeOffset capturedAt) => new()
{
    SchemaVersion = CurrentRallySnapshot.SupportedSchemaVersion,
    Mode = CurrentRallySnapshot.SyncStateMode,
    ReadOnly = true,
    CaptureId = "live-sync-test-post",
    CapturedAt = capturedAt,
    CandidateSource = CurrentRallySnapshot.SupportedCandidateSource,
    FormationSource = CurrentRallySnapshot.SupportedFormationSource,
    WorldMarchSource = "SceneManager.World.MarchDataManager",
    Player = new()
    {
        Uid = "player-1",
        InAlliance = true,
        AllianceUid = "alliance-1",
        Stamina = 26
    },
    ObservedRallyCount = 0,
    JoinableRallyCount = 0,
    JoinedRallyCount = 0,
    FormationCount = 3,
    FreeFormationCount = 3,
    Rallies = [],
    Formations =
    [
        new() { Uuid = "1349056539444945940", Index = 1, State = "0", IsFree = true, Stamina = 26,
            CurrentRallyId = "", OwnerMarchChecked = true },
        new() { Uuid = "1349056695082984539", Index = 2, State = "0", IsFree = true, Stamina = 26,
            CurrentRallyId = "", OwnerMarchChecked = true },
        new() { Uuid = "1356530504375510135", Index = 3, State = "0", IsFree = true, Stamina = 26,
            CurrentRallyId = "", OwnerMarchChecked = true }
    ],
    Sync = new()
    {
        Protocol = CurrentRallySnapshot.CurrentRefreshProtocol,
        TargetServer = 2212,
        CurrentWorldId = 0,
        OwnedSendCount = 1,
        ForeignSyncSendCount = 0,
        HandlerCount = 1,
        ResponseErrorCode = null,
        ResponseTeamsPresent = true,
        ResponseObservedAt = 1_788_646_379,
        ExactlyOneOwnedSend = true,
        NoForeignSameProtocolSend = true,
        NoRetry = true
    },
    PreSyncObservedRallyCount = 0,
    PreSyncJoinableRallyCount = 0,
    ListRefreshCorrelated = true
};

CurrentRallyObservedSnapshot ValidCurrentJoinableRally() => new()
{
    Uuid = "rally-current-1",
    RawWarType = 0,
    WarType = "ATTACK_BOSS",
    WarTypeSource = CurrentRallySnapshot.CurrentWarTypeSource,
    Server = 2212,
    ServerSource = CurrentRallySnapshot.CurrentServerSource,
    WorldId = 0,
    WorldIdSource = CurrentRallySnapshot.CurrentWorldIdSource,
    WorldType = "0",
    AttackPointId = 12345,
    AttackUid = "leader-1",
    AttackName = "Leader",
    TargetPointId = 67890,
    TargetUuid = "boss-target-object-1",
    TargetUid = "38",
    TargetName = "",
    TargetContentId = "boss-content-1",
    TargetBaseSkinId = 0,
    TargetBaseSkinIdSource = CurrentRallySnapshot.CurrentTargetBaseSkinIdSource,
    TargetLevel = 0,
    TargetLevelSource = CurrentRallySnapshot.CurrentTargetLevelSource,
    ResolvedTargetName = "300602",
    ResolvedTargetLevel = 30,
    ResolvedTargetMetadataSource = CurrentRallySnapshot.CurrentBossResolvedTargetMetadataSource,
    ResolvedTargetDisplayName = "",
    ResolvedTargetDisplayNameSource = CurrentRallySnapshot.CurrentBossResolvedTargetDisplayNameSource,
    JoinRallyType = "RALLY_FOR_BOSS",
    JoinRallyTypeSource = CurrentRallySnapshot.CurrentJoinRallyTypeSource,
    JoinTargetUuid = "rally-current-1",
    JoinTargetPointId = 77777,
    JoinTargetServerId = 2212,
    JoinTargetWorldId = 0,
    JoinMonsterSpecialType = 0,
    JoinMonsterSpecialTypeSource = CurrentRallySnapshot.CurrentBossMonsterSpecialTypeSource,
    JoinTargetSource = CurrentRallySnapshot.CurrentJoinTargetSource,
    CreateTime = 900_000,
    WaitTime = 1_100_000,
    MarchTime = 1_200_000,
    RemainingSeconds = 100,
    RemainingSecondsSource = "AllianceWarDataManager.GetAllianceWarDurationSec",
    ServerTimeMs = 1_000_000,
    CurrentSoldiers = 100,
    MaxSoldiers = 500,
    AssemblyMarchMax = 5,
    BossHp = 1_000,
    UpdateTime = 1_000_000,
    MemberCount = 1,
    MemberCountSource = CurrentRallySnapshot.CurrentLeaderInclusiveMemberCountSource,
    MemberNames = [],
    CanJoin = true,
    IsLeader = false,
    InTeam = false,
    JoinState = "9",
    Leader = new()
    {
        Uuid = "leader-march-1",
        OwnerUid = "leader-1",
        OwnerName = "Leader",
        Status = "WAIT_RALLY",
        StartId = 77777,
        TeamUuid = "rally-current-1"
    }
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
cases.Add(("Read-only world-map snapshot accepts recovered structured point fields", () =>
{
    var snapshot = ValidWorldMapSnapshot(now);
    snapshot.Validate(now);
    Check(snapshot.Source == "WorldPointManager");
    Check(snapshot.Points.Count == 2);
    Check(snapshot.Points[0].CollectResourceInfo?.Level == 8);
}));
cases.Add(("World-map snapshot rejects stale, action-shaped, and duplicate point captures", () =>
{
    Throws<InvalidDataException>(() => ValidWorldMapSnapshot(now.AddSeconds(-16)).Validate(now));
    Throws<InvalidDataException>(() => (ValidWorldMapSnapshot(now) with { Mode = "scan" }).Validate(now));
    Throws<InvalidDataException>(() => (ValidWorldMapSnapshot(now) with { Source = "UIIcons" }).Validate(now));

    var snapshot = ValidWorldMapSnapshot(now);
    Throws<InvalidDataException>(() => (snapshot with
    {
        Points = [.. snapshot.Points, snapshot.Points[0] with { Uuid = 999 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Points = [snapshot.Points[0] with { WorldId = -1 }]
    }).Validate(now));
}));
cases.Add(("World-map snapshot JSON is strict", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), $"lwcontrol-world-snapshot-check-{Guid.NewGuid():N}");
    try
    {
        var path = Path.Combine(directory, "world-snapshot.json");
        var snapshot = ValidWorldMapSnapshot(now);
        JsonFiles.Write(path, snapshot);
        var loaded = JsonFiles.Read<CurrentWorldMapSnapshot>(path);
        loaded.Validate(now);

        var text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace(
            "\"captureId\": \"world-capture-test-1\"",
            "\"captureId\": \"world-capture-test-1\",\n  \"unexpected\": true",
            StringComparison.Ordinal));
        Throws<JsonException>(() => JsonFiles.Read<CurrentWorldMapSnapshot>(path));
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}));
cases.Add(("Recovered Auto Join Rally gates match the original Lua v49 order", () =>
{
    var options = new RecoveredAutoJoinRallyOptions
    {
        AllowedTargetTypes = new HashSet<string>(["WorldBoss"], StringComparer.Ordinal),
        MinimumTargetLevel = 20,
        MaximumTargetLevel = 30,
        MinimumRemainingSeconds = 30,
        JoinSafetyBufferSeconds = 15,
        MinimumMemberCount = 2,
        SkipUnknownLeader = true,
        SkipUnknownTargetType = true,
        SkipUnknownMarchTime = true
    };
    var candidate = new RecoveredRallyCandidate
    {
        RallyId = "rally-1",
        LeaderId = "leader-1",
        LeaderName = "Leader",
        MemberNames = ["Member"],
        TargetType = "WorldBoss",
        TargetName = "Doom Elite",
        TargetLevel = 25,
        MemberCountKnown = true,
        MemberCount = 2,
        RemainingSeconds = 90,
        MarchSeconds = 20
    };
    Check(RecoveredAutoJoinRallyPlanner.RejectionReason(options, candidate) is null);
    Check(RecoveredAutoJoinRallyPlanner.RejectionReason(
        options,
        candidate with { MemberCountKnown = false }) == "UnknownMemberCount");
    var unknownTargetReason = RecoveredAutoJoinRallyPlanner.RejectionReason(
        options,
        candidate with { TargetType = "Unknown" });
    if (unknownTargetReason != "UnknownTargetType")
        throw new Exception($"Expected UnknownTargetType, got {unknownTargetReason ?? "<none>"}");
    Check(RecoveredAutoJoinRallyPlanner.RejectionReason(
        options,
        candidate with { RemainingSeconds = 34, MarchSeconds = 20 }) == "InsufficientRemainingTime");
}));
cases.Add(("Recovered Auto Join Rally preserves idle squad and explicit team-profile gates", () =>
{
    var schemes = new[]
    {
        new RecoveredRallyJoinScheme
        {
            Id = "doom-20-30", MonsterName = "Doom Elite", MinimumLevel = 20, MaximumLevel = 30
        }
    };
    var profiles = Enumerable.Range(1, 4).ToDictionary(
        id => id,
        id => new RecoveredRallyTeamProfile
        {
            SquadId = id,
            AllowJoin = id == 2,
            JoinSchemes = id == 2 ? schemes : []
        });
    var options = new RecoveredAutoJoinRallyOptions
    {
        ReservedIdleSquadCount = 1,
        PreferredSquadOrder = [2, 1, 3, 4],
        TeamJoinProfiles = profiles
    };
    var candidate = new RecoveredRallyCandidate
    {
        RallyId = "rally-2",
        LeaderId = "leader-2",
        LeaderName = "Leader",
        MemberNames = [],
        TargetType = "WorldBoss",
        TargetName = "Doom Elite",
        TargetLevel = 25,
        MemberCountKnown = true,
        MemberCount = 1,
        RemainingSeconds = 90,
        MarchSeconds = 20
    };
    var selected = RecoveredAutoJoinRallyPlanner.Select(
        options,
        [candidate],
        [
            new() { SquadId = 1, IsFree = true, Stamina = 100 },
            new() { SquadId = 2, IsFree = true, Stamina = 100 }
        ]);
    Check(selected.Selected);
    Check(selected.Squad?.SquadId == 2);

    var reserved = RecoveredAutoJoinRallyPlanner.Select(
        options,
        [candidate],
        [new() { SquadId = 2, IsFree = true, Stamina = 100 }]);
    Check(!reserved.Selected && reserved.Reason == "auto_join_rally_no_available_squad");
}));
cases.Add(("Current Rally sync snapshot proves an authoritative refreshed empty list", () =>
{
    var snapshot = ValidRallySyncSnapshot(now);
    snapshot.Validate(now);
    Check(snapshot.IsAuthoritativeEmptyAfterRefresh);
    var squads = snapshot.ToRecoveredSquads(now);
    Check(squads.Count == 3 && squads.All(squad => squad.IsFree && squad.Stamina == 26));

    var selection = CurrentRallyPlannerPreview.PreviewAuthoritativeEmpty(
        new RecoveredAutoJoinRallyOptions(), snapshot, now);
    Check(!selection.Selected);
    Check(selection.Reason == "auto_join_rally_no_new_joinable_rally");
}));
cases.Add(("Current Rally importer refuses uncorrelated empty state and corrupted formation counts", () =>
{
    var snapshot = ValidRallySyncSnapshot(now);
    Throws<InvalidDataException>(() => (snapshot with { ListRefreshCorrelated = false }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with { FormationCount = 2 }).Validate(now));
    Throws<InvalidDataException>(() => CurrentRallyPlannerPreview.PreviewAuthoritativeEmpty(
        new RecoveredAutoJoinRallyOptions(), snapshot with
        {
            Mode = CurrentRallySnapshot.StateMode,
            Sync = null,
            PreSyncObservedRallyCount = null,
            PreSyncJoinableRallyCount = null,
            ListRefreshCorrelated = null
        }, now));
}));
cases.Add(("Current Rally importer enforces recovered content-v12 join eligibility", () =>
{
    var rally = ValidCurrentJoinableRally();
    var snapshot = ValidRallySyncSnapshot(now) with
    {
        Mode = CurrentRallySnapshot.StateMode,
        ObservedRallyCount = 1,
        JoinableRallyCount = 1,
        JoinedRallyCount = 0,
        Rallies = [rally],
        Sync = null,
        PreSyncObservedRallyCount = null,
        PreSyncJoinableRallyCount = null,
        ListRefreshCorrelated = null
    };
    snapshot.Validate(now);

    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinState = "8" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { RemainingSeconds = 0 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { WaitTime = 999_999 }]
    }).Validate(now));

    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { RawWarType = 1 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { RawWarType = 9, WarType = "UNKNOWN" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinRallyType = "RALLY_FOR_CITY" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinTargetUuid = "boss-1" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinTargetPointId = 88888 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinTargetServerId = 9999 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinTargetWorldId = 9 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinTargetPointId = 0, Leader = rally.Leader with { StartId = 0 } }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { ServerSource = "unproven" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { WorldId = -1, JoinTargetWorldId = -1 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { JoinMonsterSpecialType = -1 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { TargetLevelSource = "unproven" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { TargetLevel = -1 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { MemberCount = 0 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { MemberCountSource = "memberList" }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { ResolvedTargetLevel = 0 }]
    }).Validate(now));
    Throws<InvalidDataException>(() => (snapshot with
    {
        Rallies = [rally with { ResolvedTargetMetadataSource = "unproven" }]
    }).Validate(now));

    var ownTarget = rally with { CanJoin = false, InTeam = true, JoinState = "4" };
    (snapshot with
    {
        JoinableRallyCount = 0,
        JoinedRallyCount = 1,
        Rallies = [ownTarget]
    }).Validate(now);

    var alreadyJoined = rally with { CanJoin = false, InTeam = true, JoinState = "9" };
    (snapshot with
    {
        JoinableRallyCount = 0,
        JoinedRallyCount = 1,
        Rallies = [alreadyJoined]
    }).Validate(now);

    var allianceCity = rally with
    {
        RawWarType = 3,
        WarType = "ATTACK_AL_CITY",
        JoinRallyType = "RALLY_FOR_ALLIANCE_CITY",
        JoinMonsterSpecialType = null,
        JoinMonsterSpecialTypeSource = CurrentRallySnapshot.CurrentNoMonsterSpecialTypeSource,
        TargetName = "Alliance City",
        TargetLevel = 5,
        ResolvedTargetName = "Alliance City",
        ResolvedTargetLevel = 5,
        ResolvedTargetMetadataSource = CurrentRallySnapshot.CurrentMessageResolvedTargetMetadataSource,
        ResolvedTargetDisplayName = "Alliance City",
        ResolvedTargetDisplayNameSource = CurrentRallySnapshot.CurrentMessageResolvedTargetDisplayNameSource
    };
    (snapshot with { Rallies = [allianceCity] }).Validate(now);
}));
cases.Add(("Current Rally schema-v5 maps the live boss shape without inventing taxonomy or march time", () =>
{
    var rally = ValidCurrentJoinableRally();
    var snapshot = ValidRallySyncSnapshot(now) with
    {
        Mode = CurrentRallySnapshot.StateMode,
        ObservedRallyCount = 1,
        JoinableRallyCount = 1,
        JoinedRallyCount = 0,
        Rallies = [rally],
        Sync = null,
        PreSyncObservedRallyCount = null,
        PreSyncJoinableRallyCount = null,
        ListRefreshCorrelated = null
    };

    var candidate = snapshot.ToRecoveredCandidates(now).Single();
    Check(candidate.RallyId == rally.Uuid);
    Check(candidate.LeaderId == "leader-1" && candidate.LeaderName == "Leader");
    Check(candidate.MemberCountKnown && candidate.MemberCount == 1 && candidate.MemberNames.Count == 0);
    Check(candidate.TargetType == "Unknown");
    Check(candidate.TargetName == "300602" && candidate.TargetLevel == 30);
    Check(candidate.RemainingSeconds == 100);
    Check(candidate.MarchSeconds is null);

    var failClosedOptions = new RecoveredAutoJoinRallyOptions { SkipUnknownTargetType = true };
    Check(RecoveredAutoJoinRallyPlanner.RejectionReason(failClosedOptions, candidate) == "UnknownTargetType");
    var failClosed = CurrentRallyPlannerPreview.Preview(failClosedOptions, snapshot, now);
    Check(!failClosed.Selected);

    var explicitUnknownCompatibility = CurrentRallyPlannerPreview.Preview(
        new RecoveredAutoJoinRallyOptions { SkipUnknownTargetType = false }, snapshot, now);
    Check(explicitUnknownCompatibility.Selected);
    Check(explicitUnknownCompatibility.Candidate?.TargetType == "Unknown");

    var localized = snapshot with
    {
        Rallies = [rally with { ResolvedTargetDisplayName = "localized-name" }]
    };
    Check(localized.ToRecoveredCandidates(now).Single().TargetName == "localized-name");
}));
cases.Add(("Recovered World Map default plan matches the original five-by-five logical policy", () =>
{
    var plan = RecoveredWorldMapScanPlanner.Build(100, 10, 55, 47);
    Check(plan.RequestedEdge == 5);
    Check(plan.RequestedCoverage == new RecoveredWorldMapCoverage
    {
        Left = 53, Bottom = 45, Right = 57, Top = 49
    });
    Check(plan.RequestedBlocks.Count == 25);
    Check(plan.RequestedBlocks[0].X == 55 && plan.RequestedBlocks[0].Y == 47);
    Check(plan.Batches.Count == 1);
    Check(plan.Batches[0].RequestedBlocks.Count == 25);
    Check(plan.Batches[0].TransportIndexes.Count == 25);
    Check(plan.Batches[0].TransportIndexes[0] == 4553);
    Check(plan.Batches[0].TransportIndexes[^1] == 4957);
}));
cases.Add(("Recovered World Map small batch uses the proven minimum five-by-four transport", () =>
{
    var plan = RecoveredWorldMapScanPlanner.Build(100, 10, 58, 47, requestedEdge: 3);
    var batch = plan.Batches.Single();
    Check(batch.RequestedCoverage == new RecoveredWorldMapCoverage
    {
        Left = 57, Bottom = 46, Right = 59, Top = 48
    });
    Check(batch.TransportCoverage == new RecoveredWorldMapCoverage
    {
        Left = 56, Bottom = 46, Right = 60, Top = 49
    });
    Check(batch.TransportIndexes.Count == 20);
    Check(batch.TransportIndexes[0] == 4656);
    Check(batch.TransportIndexes[^1] == 4960);
    Check(batch.LeftBottomTile == new RecoveredWorldMapTilePoint { X = 560, Y = 460 });
    Check(batch.RightTopTile == new RecoveredWorldMapTilePoint { X = 610, Y = 500 });
    Check(batch.RequestTile == new RecoveredWorldMapTilePoint { X = 585, Y = 480 });
}));
cases.Add(("Recovered World Map thirteen-by-thirteen plan splits into 156 plus 13 logical blocks", () =>
{
    var plan = RecoveredWorldMapScanPlanner.Build(100, 10, 58, 47, requestedEdge: 13);
    Check(plan.RequestedEdge == 13);
    Check(plan.RequestedBlocks.Count == 169);
    Check(plan.Batches.Count == 2);
    Check(plan.Batches[0].RequestedBlocks.Count == 156);
    Check(plan.Batches[1].RequestedBlocks.Count == 13);
    Check(plan.Batches[0].TransportIndexes.Count == 156);
    Check(plan.Batches[1].TransportIndexes.Count == 65);
    Check(plan.Batches.All(batch => batch.TransportIndexes.Count is > 0 and <= RecoveredWorldMapScanPlanner.MaxNativeBatchIndexes));
    Check(plan.Batches.Sum(batch => batch.RequestedBlocks.Count) == 169);
}));
cases.Add(("Recovered World Map nineteen-by-nineteen plan exposes two-batch concurrent wave after probe", () =>
{
    var plan = RecoveredWorldMapScanPlanner.Build(100, 10, 58, 47, requestedEdge: 19);
    Check(plan.RequestedEdge == 19);
    Check(plan.RequestedBlocks.Count == 361);
    Check(plan.Batches.Count == 3);
    Check(plan.Batches[0].RequestedBlocks.Count == 152);
    Check(plan.Batches[1].RequestedBlocks.Count == 152);
    Check(plan.Batches[2].RequestedBlocks.Count == 57);
    Check(plan.Batches[0].TransportIndexes.Count == 152);
    Check(plan.Batches[1].TransportIndexes.Count == 152);
    Check(plan.Batches[2].TransportIndexes.Count == 95);
    Check(plan.Batches.All(batch => batch.TransportIndexes.Count is > 0 and <= RecoveredWorldMapScanPlanner.MaxNativeBatchIndexes));
    Check(plan.Batches.Sum(batch => batch.RequestedBlocks.Count) == 361);
}));
cases.Add(("Recovered World Map response coverage accumulates and fails closed on missing blocks", () =>
{
    var batch = RecoveredWorldMapScanPlanner.Build(100, 10, 58, 47, requestedEdge: 3).Batches.Single();
    var first = new RecoveredWorldMapResponseEnvelope
    {
        ServerId = 2212,
        WorldId = 0,
        Coverage = new() { Left = 57, Bottom = 46, Right = 58, Top = 48 }
    };
    var partial = RecoveredWorldMapScanPlanner.EvaluateCoverage(batch, 2212, 0, [first]);
    Check(partial.CoveredBlocks == 6 && partial.ExpectedBlocks == 9 && !partial.Complete);

    var second = new RecoveredWorldMapResponseEnvelope
    {
        ServerId = null,
        WorldId = 0,
        Coverage = new() { Left = 59, Bottom = 46, Right = 59, Top = 48 }
    };
    var wrongServer = new RecoveredWorldMapResponseEnvelope
    {
        ServerId = 9999,
        WorldId = 0,
        Coverage = batch.RequestedCoverage
    };
    var complete = RecoveredWorldMapScanPlanner.EvaluateCoverage(
        batch, 2212, 0, [first, second, wrongServer]);
    Check(complete.Complete);
    Check(complete.CoveredBlocks == 9);
    Check(complete.AcceptedEnvelopes == 2);
    Check(complete.RejectedEnvelopes == 1);
}));
cases.Add(("Recovered World Map full-grid plan batches every logical block within native limits", () =>
{
    var plan = RecoveredWorldMapScanPlanner.Build(100, 10, 50, 50, requestedEdge: 99);
    Check(plan.RequestedEdge == 100);
    Check(plan.RequestedBlocks.Count == 10_000);
    Check(plan.Batches.Count == 65);
    Check(plan.Batches.Count(batch => batch.RequestedBlocks.Count == 160) == 60);
    Check(plan.Batches.Count(batch => batch.RequestedBlocks.Count == 80) == 5);
    Check(plan.Batches.All(batch => batch.TransportIndexes.Count is > 0 and <= RecoveredWorldMapScanPlanner.MaxNativeBatchIndexes));
    Check(plan.Batches.SelectMany(batch => batch.RequestedBlocks)
        .Select(block => (block.X, block.Y)).Distinct().Count() == 10_000);
    Check(plan.Batches.Sum(batch => batch.RequestedBlocks.Count) == 10_000);
    Check(plan.Batches.Select((batch, index) => batch.Sequence == index + 1).All(value => value));
    Check(plan.Batches.All(batch => batch.LeftBottomTile.X is >= 0 and <= 999
        && batch.LeftBottomTile.Y is >= 0 and <= 999
        && batch.RightTopTile.X is >= 0 and <= 999
        && batch.RightTopTile.Y is >= 0 and <= 999));
    Check(plan.Batches.Any(batch => batch.RightTopTile.X == 999 || batch.RightTopTile.Y == 999));
}));
cases.Add(("Daily Task live command protocol is bounded and heartbeat-gated", () =>
{
    string command = CurrentDailyTaskRuntimeClient.BuildCommandText("daily-test_1", 20);
    Check(command == "schema=1\ncommandId=daily-test_1\nmode=run_once\nmaximumClaims=20\n");
    Throws<ArgumentException>(() => CurrentDailyTaskRuntimeClient.BuildCommandText("bad id", 1));
    Throws<ArgumentOutOfRangeException>(() => CurrentDailyTaskRuntimeClient.BuildCommandText("daily-test", 21));

    string directory = Path.Combine(Path.GetTempPath(), $"lw-daily-runtime-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var client = new CurrentDailyTaskRuntimeClient(directory);
        Check(client.Inspect(now).StatusCode == "heartbeat_missing");
        File.WriteAllText(Path.Combine(directory, "daily-task-runtime-heartbeat.json"),
            JsonSerializer.Serialize(new
            {
                version = CurrentDailyTaskRuntimeClient.ExpectedRuntimeVersion,
                loaded = true,
                updatedAt = now.ToUnixTimeSeconds(),
                registrationMethod = "UpdateManager.AddUpdate"
            }));
        var inspection = client.Inspect(now);
        Check(inspection.StatusCode == "ready");
        Check(inspection.HeartbeatFresh);
        Check(inspection.RegistrationMethod == "UpdateManager.AddUpdate");
        Check(client.Inspect(now + TimeSpan.FromSeconds(16)).StatusCode == "heartbeat_stale");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}));
cases.Add(("Daily Task live result requires final authoritative Received state", () =>
{
    var final = ValidDailyTaskSnapshot(now) with
    {
        Tasks =
        [
            new() { TaskId = "task-a", State = CurrentTaskState.Received, TemplatePoint = 30 },
            new() { TaskId = "task-b", State = CurrentTaskState.Received, TemplatePoint = 40 },
            new() { TaskId = "task-c", State = CurrentTaskState.NoComplete, TemplatePoint = null }
        ],
        CurrentPoint = 70,
        Boxes =
        [
            new() { Index = 1, ActivationPoint = 10, State = CurrentTaskState.Received },
            new() { Index = 2, ActivationPoint = 20, State = CurrentTaskState.CanReceive },
            new() { Index = 3, ActivationPoint = 30, State = CurrentTaskState.CanReceive },
            new() { Index = 4, ActivationPoint = 40, State = CurrentTaskState.CanReceive },
            new() { Index = 5, ActivationPoint = 50, State = CurrentTaskState.CanReceive }
        ]
    };
    var result = new CurrentDailyTaskRuntimeResult
    {
        SchemaVersion = 1,
        RuntimeVersion = CurrentDailyTaskRuntimeClient.ExpectedRuntimeVersion,
        CommandId = "daily-result-1",
        State = "completed",
        Message = "no_more_eligible_targets",
        ConfirmedClaims = 1,
        RewardSendCount = 1,
        RefreshSendCount = 2,
        ClaimedTargets = [new() { Kind = "DailyTask", TaskId = "task-b" }],
        FinalSnapshot = final,
        CompletedAt = now
    };
    result.Validate("daily-result-1");
    Throws<InvalidDataException>(() => (result with
    {
        ClaimedTargets = [new() { Kind = "DailyTask", TaskId = "task-c" }]
    }).Validate("daily-result-1"));
}));

int failed = 0;
foreach (var test in cases)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{cases.Count - failed}/{cases.Count} checks passed.");
return failed == 0 ? 0 : 1;
