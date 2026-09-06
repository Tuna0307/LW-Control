namespace LWControl.Core;

public sealed record CurrentRallyPlayerSnapshot
{
    public required string Uid { get; init; }
    public bool? InAlliance { get; init; }
    public required string AllianceUid { get; init; }
    public double? Stamina { get; init; }
}

public sealed record CurrentRallyLeaderSnapshot
{
    public required string Uuid { get; init; }
    public required string OwnerUid { get; init; }
    public required string OwnerName { get; init; }
    public required string Status { get; init; }
    public double? StartId { get; init; }
    public double? StartTime { get; init; }
    public double? EndTime { get; init; }
    public required string TeamUuid { get; init; }
    public double? Power { get; init; }
    public double? CurHp { get; init; }
    public double? MaxHp { get; init; }
}

public sealed record CurrentRallyObservedSnapshot
{
    public required string Uuid { get; init; }
    public required int RawWarType { get; init; }
    public required string WarType { get; init; }
    public required string WarTypeSource { get; init; }
    public double? Server { get; init; }
    public required string ServerSource { get; init; }
    public double? WorldId { get; init; }
    public required string WorldIdSource { get; init; }
    public required string WorldType { get; init; }
    public double? AttackPointId { get; init; }
    public required string AttackUid { get; init; }
    public required string AttackName { get; init; }
    public double? TargetPointId { get; init; }
    public required string TargetUuid { get; init; }
    public required string TargetUid { get; init; }
    public required string TargetName { get; init; }
    public required string TargetContentId { get; init; }
    public double? TargetBaseSkinId { get; init; }
    public required string TargetBaseSkinIdSource { get; init; }
    public double? TargetLevel { get; init; }
    public required string TargetLevelSource { get; init; }
    public required string JoinRallyType { get; init; }
    public required string JoinRallyTypeSource { get; init; }
    public required string JoinTargetUuid { get; init; }
    public double? JoinTargetPointId { get; init; }
    public double? JoinTargetServerId { get; init; }
    public double? JoinTargetWorldId { get; init; }
    public double? JoinMonsterSpecialType { get; init; }
    public required string JoinMonsterSpecialTypeSource { get; init; }
    public required string JoinTargetSource { get; init; }
    public double? CreateTime { get; init; }
    public double? WaitTime { get; init; }
    public double? MarchTime { get; init; }
    public double? RemainingSeconds { get; init; }
    public required string RemainingSecondsSource { get; init; }
    public double? ServerTimeMs { get; init; }
    public double? CurrentSoldiers { get; init; }
    public double? MaxSoldiers { get; init; }
    public double? AssemblyMarchMax { get; init; }
    public double? BossHp { get; init; }
    public double? UpdateTime { get; init; }
    public required int MemberCount { get; init; }
    public required string MemberCountSource { get; init; }
    public required IReadOnlyList<string> MemberNames { get; init; }
    public required bool CanJoin { get; init; }
    public required bool IsLeader { get; init; }
    public required bool InTeam { get; init; }
    public required string JoinState { get; init; }
    public required CurrentRallyLeaderSnapshot Leader { get; init; }
}

public sealed record CurrentRallyFormationSnapshot
{
    public required string Uuid { get; init; }
    public int? Index { get; init; }
    public required string State { get; init; }
    public required bool IsFree { get; init; }
    public required double Stamina { get; init; }
    public double? Power { get; init; }
    public double? TotalSoldierNum { get; init; }
    public required string CurrentRallyId { get; init; }
    public required bool OwnerMarchChecked { get; init; }
}

public sealed record CurrentRallySyncEvidence
{
    public required string Protocol { get; init; }
    public required int TargetServer { get; init; }
    public required int CurrentWorldId { get; init; }
    public required int OwnedSendCount { get; init; }
    public required int ForeignSyncSendCount { get; init; }
    public required int HandlerCount { get; init; }
    public object? ResponseErrorCode { get; init; }
    public required bool ResponseTeamsPresent { get; init; }
    public double? ResponseObservedAt { get; init; }
    public required bool ExactlyOneOwnedSend { get; init; }
    public required bool NoForeignSameProtocolSend { get; init; }
    public required bool NoRetry { get; init; }
}

/// <summary>
/// Strict interchange contract for the recovered current-build Rally probes.
/// It performs no game or network I/O.
/// </summary>
public sealed record CurrentRallySnapshot
{
    public const int SupportedSchemaVersion = 4;
    public const string StateMode = "state";
    public const string SyncStateMode = "sync_state";
    public const string SupportedCandidateSource = "DataCenter.AllianceWarDataManager.GetAllianceWarIdList";
    public const string SupportedFormationSource = "DataCenter.ArmyFormationDataManager.GetCurFormationList";
    public const string CurrentRefreshProtocol = "alliance.team.ls";
    public const string CurrentWarTypeSource = "Global.EnumType.AllianceTeamType";
    public const string CurrentServerSource = "AllianceWarInfo.ParseData: message.server";
    public const string CurrentWorldIdSource = "AllianceWarInfo.ParseData: message.worldId";
    public const string CurrentTargetBaseSkinIdSource = "AllianceWarInfo.ParseData: message.targetBaseSkinId";
    public const string CurrentTargetLevelSource = "AllianceWarInfo.ParseData: message.targetLevel";
    public const string CurrentJoinRallyTypeSource = "UIAllianceWarMainTableCtrl.OnJoinClick";
    public const string CurrentJoinTargetSource = "UIAllianceWarMainTableCtrl.OnJoinClick: leaderMarch.startId + rally uuid + data.server + data.worldId";
    public const string CurrentBossMonsterSpecialTypeSource = "UIAllianceWarMainTableCtrl.GetWarItemData: MonsterTemplateManager.GetMonsterTemplate(targetUid).special";
    public const string CurrentNoMonsterSpecialTypeSource = "UIAllianceWarMainTableCtrl.GetWarItemData: non-boss branch unset";
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DefaultFutureTolerance = TimeSpan.FromSeconds(5);

    public required int SchemaVersion { get; init; }
    public required string Mode { get; init; }
    public required bool ReadOnly { get; init; }
    public required string CaptureId { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required string CandidateSource { get; init; }
    public required string FormationSource { get; init; }
    public required string WorldMarchSource { get; init; }
    public required CurrentRallyPlayerSnapshot Player { get; init; }
    public required int ObservedRallyCount { get; init; }
    public required int JoinableRallyCount { get; init; }
    public required int JoinedRallyCount { get; init; }
    public required int FormationCount { get; init; }
    public required int FreeFormationCount { get; init; }
    public required IReadOnlyList<CurrentRallyObservedSnapshot> Rallies { get; init; }
    public required IReadOnlyList<CurrentRallyFormationSnapshot> Formations { get; init; }
    public CurrentRallySyncEvidence? Sync { get; init; }
    public int? PreSyncObservedRallyCount { get; init; }
    public int? PreSyncJoinableRallyCount { get; init; }
    public bool? ListRefreshCorrelated { get; init; }

    public void Validate(
        DateTimeOffset now,
        TimeSpan? maxAge = null,
        TimeSpan? futureTolerance = null)
    {
        TimeSpan ageLimit = maxAge ?? DefaultMaxAge;
        TimeSpan futureLimit = futureTolerance ?? DefaultFutureTolerance;
        if (ageLimit <= TimeSpan.Zero || ageLimit > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(maxAge));
        if (futureLimit < TimeSpan.Zero || futureLimit > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(futureTolerance));

        if (SchemaVersion != SupportedSchemaVersion)
            throw new InvalidDataException($"Unsupported Rally snapshot schema version {SchemaVersion}.");
        if (!string.Equals(Mode, StateMode, StringComparison.Ordinal)
            && !string.Equals(Mode, SyncStateMode, StringComparison.Ordinal))
            throw new InvalidDataException("Rally snapshot mode is unsupported.");
        if (!ReadOnly)
            throw new InvalidDataException("Rally snapshot is not marked read-only.");
        if (string.IsNullOrWhiteSpace(CaptureId) || CaptureId.Length > 128)
            throw new InvalidDataException("Rally snapshot capture ID is invalid.");
        if (CapturedAt < now - ageLimit || CapturedAt > now + futureLimit)
            throw new InvalidDataException("Rally snapshot timestamp is outside the accepted live window.");
        if (!string.Equals(CandidateSource, SupportedCandidateSource, StringComparison.Ordinal)
            || !string.Equals(FormationSource, SupportedFormationSource, StringComparison.Ordinal))
            throw new InvalidDataException("Rally snapshot source does not match the recovered current managers.");
        if (Player is null || string.IsNullOrWhiteSpace(Player.Uid)
            || Player.Uid is "0" or "nil" || Player.AllianceUid is null)
            throw new InvalidDataException("Rally snapshot player identity is incomplete.");
        if (Player.Stamina is < 0)
            throw new InvalidDataException("Rally snapshot player stamina is negative.");

        if (Rallies is null || Rallies.Count > 256 || Rallies.Any(item => item is null))
            throw new InvalidDataException("Rally snapshot candidate list is invalid.");
        if (Formations is null || Formations.Count is < 1 or > 32 || Formations.Any(item => item is null))
            throw new InvalidDataException("Rally snapshot formation list is invalid.");
        if (ObservedRallyCount != Rallies.Count
            || JoinableRallyCount != Rallies.Count(item => item.CanJoin && !item.IsLeader && !item.InTeam)
            || JoinedRallyCount != Rallies.Count(item => item.InTeam)
            || FormationCount != Formations.Count
            || FreeFormationCount != Formations.Count(item => item.IsFree))
            throw new InvalidDataException("Rally snapshot derived counts do not match its records.");

        var rallyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rally in Rallies)
        {
            if (string.IsNullOrWhiteSpace(rally.Uuid) || !rallyIds.Add(rally.Uuid)
                || rally.MemberCount < 0 || rally.MemberNames is null || rally.MemberNames.Any(name => name is null)
                || rally.Leader is null)
                throw new InvalidDataException("Rally snapshot contains an invalid or duplicate Rally record.");
            if (rally.RemainingSeconds is null
                || double.IsNaN(rally.RemainingSeconds.Value) || double.IsInfinity(rally.RemainingSeconds.Value)
                || !string.Equals(rally.RemainingSecondsSource,
                    "AllianceWarDataManager.GetAllianceWarDurationSec", StringComparison.Ordinal)
                || rally.ServerTimeMs is null || rally.ServerTimeMs < 0)
                throw new InvalidDataException("Rally snapshot lacks the recovered current duration evidence.");
            ValidateCurrentTargetContract(rally);
            ValidateCurrentJoinEligibility(rally);
        }

        var formationIds = new HashSet<string>(StringComparer.Ordinal);
        var formationIndexes = new HashSet<int>();
        foreach (var formation in Formations)
        {
            if (string.IsNullOrWhiteSpace(formation.Uuid) || !formationIds.Add(formation.Uuid)
                || formation.Stamina < 0 || double.IsNaN(formation.Stamina) || double.IsInfinity(formation.Stamina)
                || formation.CurrentRallyId is null || formation.State is null)
                throw new InvalidDataException("Rally snapshot contains an invalid or duplicate formation record.");
            if (formation.Index is int index && (index is < 1 or > 32 || !formationIndexes.Add(index)))
                throw new InvalidDataException("Rally snapshot contains an invalid or duplicate formation index.");
        }

        if (string.Equals(Mode, SyncStateMode, StringComparison.Ordinal)) ValidateSyncEvidence();
        else if (Sync is not null || PreSyncObservedRallyCount is not null
            || PreSyncJoinableRallyCount is not null || ListRefreshCorrelated is not null)
            throw new InvalidDataException("Read-only state snapshot unexpectedly contains sync evidence.");
    }

    public bool IsAuthoritativeEmptyAfterRefresh =>
        string.Equals(Mode, SyncStateMode, StringComparison.Ordinal)
        && ObservedRallyCount == 0
        && PreSyncObservedRallyCount == 0
        && ListRefreshCorrelated == true
        && Sync is { ExactlyOneOwnedSend: true, NoForeignSameProtocolSend: true, NoRetry: true,
            OwnedSendCount: 1, ForeignSyncSendCount: 0, HandlerCount: 1, ResponseTeamsPresent: true };

    public IReadOnlyList<RecoveredRallySquad> ToRecoveredSquads(DateTimeOffset now)
    {
        Validate(now);
        var result = new List<RecoveredRallySquad>(Formations.Count);
        foreach (var formation in Formations)
        {
            if (formation.Index is not int squadId || squadId is < 1 or > 4)
                throw new InvalidDataException("Current formation index cannot be mapped to the recovered squad IDs 1-4.");
            if (formation.Stamina != Math.Truncate(formation.Stamina) || formation.Stamina > int.MaxValue)
                throw new InvalidDataException("Current formation stamina cannot be represented by the recovered planner.");
            result.Add(new RecoveredRallySquad
            {
                SquadId = squadId,
                IsFree = formation.IsFree,
                Stamina = checked((int)formation.Stamina)
            });
        }
        return result;
    }

    private void ValidateSyncEvidence()
    {
        if (Sync is null || PreSyncObservedRallyCount is null || PreSyncJoinableRallyCount is null
            || ListRefreshCorrelated is null)
            throw new InvalidDataException("Rally sync snapshot is missing refresh evidence.");
        if (!string.Equals(Sync.Protocol, CurrentRefreshProtocol, StringComparison.Ordinal)
            || Sync.TargetServer < 0 || Sync.CurrentWorldId < 0
            || Sync.OwnedSendCount != 1 || Sync.ForeignSyncSendCount != 0 || Sync.HandlerCount != 1
            || Sync.ResponseErrorCode is not null || !Sync.ResponseTeamsPresent
            || !Sync.ExactlyOneOwnedSend || !Sync.NoForeignSameProtocolSend || !Sync.NoRetry
            || ListRefreshCorrelated != true)
            throw new InvalidDataException("Rally sync evidence is not an exact one-request correlated refresh.");
        if (PreSyncObservedRallyCount < 0 || PreSyncJoinableRallyCount < 0)
            throw new InvalidDataException("Rally pre-sync counts are invalid.");
    }

    private static void ValidateCurrentJoinEligibility(CurrentRallyObservedSnapshot rally)
    {
        // Current content-v12 AllianceWarDataManager.CheckJoinAllianceWarByWarData
        // returns (canJoin, isLeader, inTeam, state) with state codes 1..9.
        // Formation travel time is deliberately absent here: current formation-select
        // bytecode treats the JOIN_RALLY + RALLY_FOR_BOSS travel threshold as a
        // confirmable UI warning, not as an AllianceWarDataManager eligibility gate.
        if (!int.TryParse(rally.JoinState, out int state) || state is < 1 or > 9)
            throw new InvalidDataException("Rally snapshot contains an unknown current join-state code.");

        bool tupleMatches = state switch
        {
            1 or 3 or 5 or 6 or 7 or 8 => !rally.CanJoin && !rally.IsLeader && !rally.InTeam,
            2 => !rally.CanJoin && rally.IsLeader && !rally.InTeam,
            4 => !rally.CanJoin && !rally.IsLeader && rally.InTeam,
            9 => !rally.IsLeader && rally.CanJoin == !rally.InTeam,
            _ => false
        };
        if (!tupleMatches)
            throw new InvalidDataException("Rally snapshot join tuple does not match the recovered current manager state machine.");

        if (!rally.CanJoin) return;

        // CheckAllianceWarData requires GetAllianceWarDurationSec(data, curTime) > 0.
        if (rally.RemainingSeconds is not > 0)
            throw new InvalidDataException("Joinable Rally does not have positive current-manager duration.");

        // CheckRallyWaitStateTimeoutValid accepts legacy/sentinel waitTime values below
        // 9527; otherwise the current server time must not have passed waitTime.
        if (rally.WaitTime is double waitTime && waitTime >= 9527
            && rally.ServerTimeMs is double serverTimeMs && serverTimeMs > waitTime)
            throw new InvalidDataException("Joinable Rally is already past the recovered current wait deadline.");
    }

    private static void ValidateCurrentTargetContract(CurrentRallyObservedSnapshot rally)
    {
        string? expectedWarType = rally.RawWarType switch
        {
            0 => "ATTACK_BOSS",
            1 => "ATTACK_BUILDING",
            2 => "ATTACK_CITY",
            3 => "ATTACK_AL_CITY",
            4 => "ATTACK_ALLIANCE_THRONE",
            5 => "ATTACK_DRAGON_BUILDING",
            6 => "ATTACK_SERVER_THRONE",
            7 => "ATTACK_AL_CENTER",
            8 => "ATTACK_CITY_STRONGHOLD",
            10 => "ATTACK_EPIDEMIC_BUILDING",
            11 => "ATTACK_EPIDEMIC_CITY",
            12 => "ATTACK_OUTPOST",
            13 => "ATTACK_ZWL",
            _ => null
        };
        if (expectedWarType is null
            || !string.Equals(rally.WarType, expectedWarType, StringComparison.Ordinal)
            || !string.Equals(rally.WarTypeSource, CurrentWarTypeSource, StringComparison.Ordinal))
            throw new InvalidDataException("Rally snapshot AllianceTeamType does not match the recovered current enum.");

        string expectedJoinRallyType = rally.WarType switch
        {
            "ATTACK_BOSS" => "RALLY_FOR_BOSS",
            "ATTACK_BUILDING" => "RALLY_FOR_BUILDING",
            "ATTACK_AL_CITY" => "RALLY_FOR_ALLIANCE_CITY",
            "ATTACK_CITY" => "RALLY_FOR_CITY",
            "ATTACK_EPIDEMIC_CITY" => "RALLY_EPIDEMIC_CITY",
            "ATTACK_CITY_STRONGHOLD" => "RALLY_CITY_STRONGHOLD",
            _ => ""
        };
        if (!string.Equals(rally.JoinRallyType, expectedJoinRallyType, StringComparison.Ordinal)
            || !string.Equals(rally.JoinRallyTypeSource, CurrentJoinRallyTypeSource, StringComparison.Ordinal)
            || !string.Equals(rally.JoinTargetUuid, rally.Uuid, StringComparison.Ordinal)
            || !string.Equals(rally.JoinTargetSource, CurrentJoinTargetSource, StringComparison.Ordinal)
            || rally.JoinTargetPointId != rally.Leader.StartId
            || rally.JoinTargetServerId != rally.Server
            || rally.JoinTargetWorldId != rally.WorldId)
            throw new InvalidDataException("Rally snapshot normal JOIN_RALLY routing does not match current UI bytecode.");
        if (rally.CanJoin && expectedJoinRallyType.Length > 0
            && rally.JoinTargetPointId is not > 0)
            throw new InvalidDataException("Joinable Rally lacks the positive leaderMarch.startId used by the current JOIN_RALLY UI path.");

        if (rally.Server is null or < 0 || rally.WorldId is null or < 0
            || !string.Equals(rally.ServerSource, CurrentServerSource, StringComparison.Ordinal)
            || !string.Equals(rally.WorldIdSource, CurrentWorldIdSource, StringComparison.Ordinal))
            throw new InvalidDataException("Rally snapshot server/world routing does not match AllianceWarInfo.ParseData.");

        if (string.Equals(rally.WarType, "ATTACK_BOSS", StringComparison.Ordinal))
        {
            if (!string.Equals(rally.JoinMonsterSpecialTypeSource, CurrentBossMonsterSpecialTypeSource, StringComparison.Ordinal)
                || rally.JoinMonsterSpecialType is double special
                    && (double.IsNaN(special) || double.IsInfinity(special) || special < 0 || special != Math.Truncate(special)))
                throw new InvalidDataException("Boss Rally monsterSpecialType does not match current GetWarItemData derivation.");
        }
        else if (rally.JoinMonsterSpecialType is not null
            || !string.Equals(rally.JoinMonsterSpecialTypeSource, CurrentNoMonsterSpecialTypeSource, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Non-boss Rally unexpectedly carries a monsterSpecialType.");
        }

        // Current AllianceWarInfo keeps targetUuid/targetUid distinct and also owns
        // targetBaseSkinId/targetLevel, populated directly from the Rally message.
        if (rally.TargetUuid is null || rally.TargetUid is null || rally.TargetName is null
            || rally.TargetContentId is null)
            throw new InvalidDataException("Rally snapshot current target fields are incomplete.");
        if (rally.TargetBaseSkinId is null or < 0 || rally.TargetLevel is null or < 0
            || !string.Equals(rally.TargetBaseSkinIdSource, CurrentTargetBaseSkinIdSource, StringComparison.Ordinal)
            || !string.Equals(rally.TargetLevelSource, CurrentTargetLevelSource, StringComparison.Ordinal))
            throw new InvalidDataException("Rally snapshot current target level/skin fields do not match AllianceWarInfo.ParseData.");
    }
}

public static class CurrentRallyPlannerPreview
{
    /// <summary>
    /// Drives the recovered selector only for the evidence-backed empty-after-refresh case.
    /// A non-empty current snapshot is deliberately refused until its target-field/category
    /// semantics have been verified against a real current-build Rally.
    /// </summary>
    public static RecoveredRallySelection PreviewAuthoritativeEmpty(
        RecoveredAutoJoinRallyOptions options,
        CurrentRallySnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate(now);
        if (!snapshot.IsAuthoritativeEmptyAfterRefresh)
            throw new InvalidDataException("Rally snapshot does not prove an authoritative empty list after refresh.");
        if (snapshot.Rallies.Count != 0)
            throw new InvalidDataException("Non-empty current Rally candidate mapping is not yet recovered.");
        return RecoveredAutoJoinRallyPlanner.Select(options, [], snapshot.ToRecoveredSquads(now));
    }
}
