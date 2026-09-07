namespace LWControl.Core;

public sealed record RecoveredRallyJoinScheme
{
    public required string Id { get; init; }
    public required string MonsterName { get; init; }
    public required int MinimumLevel { get; init; }
    public required int MaximumLevel { get; init; }
}

public sealed record RecoveredRallyTeamProfile
{
    public required int SquadId { get; init; }
    public required bool AllowJoin { get; init; }
    public required IReadOnlyList<RecoveredRallyJoinScheme> JoinSchemes { get; init; }
}

public sealed record RecoveredAutoJoinRallyOptions
{
    public IReadOnlySet<string> AllowedTargetTypes { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> BlockedTargetTypes { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> AllowedLeaderIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> BlockedLeaderIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public bool MemberWhitelistEnabled { get; init; }
    public bool MemberBlacklistEnabled { get; init; }
    public IReadOnlySet<string> WhitelistedMemberNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> BlacklistedMemberNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool WhitelistBypassesTargetLevel { get; init; } = true;
    public int? MinimumTargetLevel { get; init; }
    public int? MaximumTargetLevel { get; init; }
    public int MinimumRemainingSeconds { get; init; } = 30;
    public int? MaximumRemainingSeconds { get; init; }
    public int JoinSafetyBufferSeconds { get; init; } = 15;
    public int? MaximumMarchSeconds { get; init; }
    public int MinimumRemainingStamina { get; init; }
    public int ReservedIdleSquadCount { get; init; } = 1;
    public int MinimumMemberCount { get; init; } = 1;
    public bool SkipUnknownLeader { get; init; }
    public bool SkipUnknownTargetType { get; init; }
    public bool SkipUnknownMarchTime { get; init; }
    public IReadOnlyList<int> PreferredSquadOrder { get; init; } = [];
    public IReadOnlySet<int> AllowedSquads { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int, RecoveredRallyTeamProfile> TeamJoinProfiles { get; init; }
        = new Dictionary<int, RecoveredRallyTeamProfile>();

    public void Validate()
    {
        if (MinimumMemberCount is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(MinimumMemberCount));
        if (MinimumTargetLevel is < 1 || MaximumTargetLevel is < 1
            || (MinimumTargetLevel is not null && MaximumTargetLevel is not null
                && MinimumTargetLevel > MaximumTargetLevel))
            throw new ArgumentException("Target-level bounds are invalid.");
        if (MinimumRemainingSeconds < 0 || MaximumRemainingSeconds is < 0
            || (MaximumRemainingSeconds is not null && MinimumRemainingSeconds > MaximumRemainingSeconds))
            throw new ArgumentException("Remaining-time bounds are invalid.");
        if (JoinSafetyBufferSeconds < 0 || MaximumMarchSeconds is < 0
            || MinimumRemainingStamina < 0 || ReservedIdleSquadCount < 0)
            throw new ArgumentException("Rally timing, stamina, and reserve values must be non-negative.");
        if (PreferredSquadOrder.Any(id => id is < 1 or > 4)
            || PreferredSquadOrder.Distinct().Count() != PreferredSquadOrder.Count)
            throw new ArgumentException("Preferred squad order must contain unique squad IDs 1-4.");
        if (AllowedSquads.Any(id => id is < 1 or > 4))
            throw new ArgumentException("Allowed squad IDs must be 1-4.");
        if (TeamJoinProfiles.Count > 0 && !Enumerable.Range(1, 4).All(TeamJoinProfiles.ContainsKey))
            throw new ArgumentException("Recovered explicit team profiles require all four squad IDs.");
        foreach (var (id, profile) in TeamJoinProfiles)
        {
            if (profile.SquadId != id || id is < 1 or > 4)
                throw new ArgumentException("Team profile squad ID is invalid.");
            foreach (var scheme in profile.JoinSchemes)
            {
                if (string.IsNullOrWhiteSpace(scheme.Id) || string.IsNullOrWhiteSpace(scheme.MonsterName)
                    || scheme.MinimumLevel < 1 || scheme.MaximumLevel > 999
                    || scheme.MinimumLevel > scheme.MaximumLevel)
                    throw new ArgumentException("Team profile join scheme is invalid.");
            }
        }
    }
}

public sealed record RecoveredRallyCandidate
{
    public required string RallyId { get; init; }
    public required string LeaderId { get; init; }
    public required string LeaderName { get; init; }
    public required IReadOnlyList<string> MemberNames { get; init; }
    public required string TargetType { get; init; }
    public required string TargetName { get; init; }
    public required int TargetLevel { get; init; }
    public required bool MemberCountKnown { get; init; }
    public required int MemberCount { get; init; }
    public required int RemainingSeconds { get; init; }
    public int? MarchSeconds { get; init; }
}

public sealed record RecoveredRallySquad
{
    public required int SquadId { get; init; }
    public required bool IsFree { get; init; }
    public required int Stamina { get; init; }
}

public sealed record RecoveredRallySelection
{
    public RecoveredRallyCandidate? Candidate { get; init; }
    public RecoveredRallySquad? Squad { get; init; }
    public required string Reason { get; init; }
    public bool Selected => Candidate is not null && Squad is not null;
}

/// <summary>
/// Clean-room reconstruction of the candidate/squad gates used by the recovered
/// LWC2AutoJoinRally.lua v49 background path. It performs no game or network I/O.
/// </summary>
public static class RecoveredAutoJoinRallyPlanner
{
    public static string? RejectionReason(
        RecoveredAutoJoinRallyOptions options,
        RecoveredRallyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(candidate);
        options.Validate();

        if (!candidate.MemberCountKnown) return "UnknownMemberCount";
        if (candidate.MemberCount < options.MinimumMemberCount) return "MemberCountBelowMinimum";
        if (candidate.TargetType == "Unknown" && options.SkipUnknownTargetType) return "UnknownTargetType";
        if (options.AllowedTargetTypes.Count > 0 && !options.AllowedTargetTypes.Contains(candidate.TargetType))
            return "TargetTypeNotAllowed";
        if (options.BlockedTargetTypes.Contains(candidate.TargetType)) return "TargetTypeBlocked";
        if (string.IsNullOrEmpty(candidate.LeaderId) && options.SkipUnknownLeader) return "UnknownLeader";
        if (options.BlockedLeaderIds.Contains(candidate.LeaderId)) return "LeaderBlocked";
        if (options.AllowedLeaderIds.Count > 0 && !options.AllowedLeaderIds.Contains(candidate.LeaderId))
            return "LeaderNotAllowed";

        bool whitelistMatch = ContainsNamedMember(candidate, options.WhitelistedMemberNames);
        bool blacklistMatch = ContainsNamedMember(candidate, options.BlacklistedMemberNames);
        if (options.MemberBlacklistEnabled && blacklistMatch) return "MemberNameBlocked";
        if (options.MemberWhitelistEnabled && !whitelistMatch) return "MemberNameNotAllowed";

        bool bypassLevel = options.MemberWhitelistEnabled && whitelistMatch
            && options.WhitelistBypassesTargetLevel;
        if (!bypassLevel && options.MinimumTargetLevel is int minimum
            && (candidate.TargetLevel <= 0 || candidate.TargetLevel < minimum))
            return "TargetLevelBelowMinimum";
        if (!bypassLevel && options.MaximumTargetLevel is int maximum
            && (candidate.TargetLevel <= 0 || candidate.TargetLevel > maximum))
            return "TargetLevelAboveMaximum";

        if (candidate.RemainingSeconds < options.MinimumRemainingSeconds)
            return "InsufficientRemainingTime";
        if (options.MaximumRemainingSeconds is int maxRemaining
            && candidate.RemainingSeconds > maxRemaining)
            return "RemainingTimeAboveMaximum";

        int? marchSeconds = candidate.MarchSeconds is > 0 ? candidate.MarchSeconds : null;
        if (marchSeconds is null && options.SkipUnknownMarchTime) return "UnknownMarchTime";
        if (marchSeconds is int march
            && candidate.RemainingSeconds < march + options.JoinSafetyBufferSeconds)
            return "InsufficientRemainingTime";
        if (options.MaximumMarchSeconds is int maxMarch
            && (marchSeconds is null || marchSeconds > maxMarch))
            return "MarchTimeAboveMaximum";
        return null;
    }

    public static RecoveredRallySelection Select(
        RecoveredAutoJoinRallyOptions options,
        IReadOnlyList<RecoveredRallyCandidate> candidates,
        IReadOnlyList<RecoveredRallySquad> squads,
        IReadOnlySet<string>? seenRallyIds = null,
        string? requestedRallyId = null,
        int? requestedSquadId = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(squads);
        options.Validate();

        var free = squads.Where(item => item.IsFree)
            .OrderBy(item => PreferredSquadRank(options, item.SquadId))
            .ThenBy(item => item.SquadId)
            .ToArray();
        if (free.Length <= options.ReservedIdleSquadCount)
            return new() { Reason = "auto_join_rally_no_available_squad" };

        bool candidateSeen = false;
        bool squadSeen = false;
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(requestedRallyId)
                && !string.Equals(candidate.RallyId, requestedRallyId, StringComparison.Ordinal))
                continue;
            if (seenRallyIds?.Contains(candidate.RallyId) == true) continue;
            candidateSeen = true;
            if (RejectionReason(options, candidate) is not null) continue;

            foreach (var squad in free)
            {
                bool globallyAllowed = options.AllowedSquads.Count == 0
                    || options.AllowedSquads.Contains(squad.SquadId);
                if (!globallyAllowed || (requestedSquadId is int requested && requested > 0
                    && requested != squad.SquadId))
                    continue;
                squadSeen = true;
                if (!TeamProfileMatches(options, squad.SquadId, candidate)) continue;
                return new() { Candidate = candidate, Squad = squad, Reason = "selected" };
            }
        }

        if (!candidateSeen) return new() { Reason = "auto_join_rally_no_new_joinable_rally" };
        if (!squadSeen) return new() { Reason = "auto_join_rally_no_available_squad" };
        return new() { Reason = "auto_join_rally_no_eligible_rally" };
    }

    private static bool ContainsNamedMember(
        RecoveredRallyCandidate candidate,
        IReadOnlySet<string> names)
    {
        if (names.Count == 0) return false;
        if (names.Contains(candidate.LeaderName)) return true;
        return candidate.MemberNames.Any(names.Contains);
    }

    private static int PreferredSquadRank(RecoveredAutoJoinRallyOptions options, int squadId)
    {
        for (int index = 0; index < options.PreferredSquadOrder.Count; index++)
            if (options.PreferredSquadOrder[index] == squadId) return index + 1;
        return 1000 + squadId;
    }

    private static bool TeamProfileMatches(
        RecoveredAutoJoinRallyOptions options,
        int squadId,
        RecoveredRallyCandidate candidate)
    {
        if (options.TeamJoinProfiles.Count == 0) return true;
        if (!options.TeamJoinProfiles.TryGetValue(squadId, out var profile)
            || !profile.AllowJoin || profile.JoinSchemes.Count == 0)
            return false;
        return profile.JoinSchemes.Any(scheme =>
            string.Equals(candidate.TargetName.Trim(), scheme.MonsterName.Trim(), StringComparison.OrdinalIgnoreCase)
            && candidate.TargetLevel >= scheme.MinimumLevel
            && candidate.TargetLevel <= scheme.MaximumLevel);
    }
}
