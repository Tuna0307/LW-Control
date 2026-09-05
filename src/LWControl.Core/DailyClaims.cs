namespace LWControl.Core;

public enum ClaimKind
{
    VipDailyReward, StoreFreePack, DailyTaskChest, WeeklyTaskChest,
    LoginReward, TavernFreeRecruit, CampaignIdleReward
}

// Independent application models. These are not the original bridge protocol.
public sealed record DailyClaimSettings
{
    public bool Enabled { get; init; }
    public HashSet<ClaimKind> EnabledKinds { get; init; } = [];
    public int MaximumClaimsPerRun { get; init; } = 5;
    public int MaxSnapshotAgeSeconds { get; init; } = 60;
    public bool PreferExpiringRewards { get; init; } = true;
    public bool PreferTaskChests { get; init; } = true;

    public void Validate()
    {
        if (EnabledKinds is null || EnabledKinds.Any(k => !Enum.IsDefined(k)))
            throw new ArgumentException("Unknown reward category.");
        if (MaximumClaimsPerRun is < 1 or > 50)
            throw new ArgumentException("Claim limit must be between 1 and 50.");
        if (MaxSnapshotAgeSeconds is < 1 or > 300)
            throw new ArgumentException("Observation age limit must be between 1 and 300 seconds.");
    }
}

public sealed record ClaimObservation
{
    public required string SourceKey { get; init; }
    public required string RewardName { get; init; }
    public required ClaimKind Kind { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool? FreeConfirmed { get; init; }
    public decimal? CurrencyCost { get; init; }
    public int? RemainingFreeClaims { get; init; }
    public bool? ClaimButtonVisible { get; init; }
}

public sealed record ClaimDecision(ClaimObservation Observation, bool Selected, string Reason);
public sealed record ClaimPlan(DateTimeOffset CreatedAt, string Mode, IReadOnlyList<ClaimDecision> Decisions);

public static class DailyClaimPlanner
{
    public static ClaimPlan Build(DailyClaimSettings settings,
        IReadOnlyList<ClaimObservation> observations, DateTimeOffset now)
    {
        settings.Validate();
        if (observations.Count > 1000 || observations.Any(o => o is null))
            throw new ArgumentException("Provide at most 1,000 non-null observations.");
        var duplicates = observations.GroupBy(o => o.SourceKey, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var ordered = observations
            .OrderBy(o => settings.PreferExpiringRewards ? o.ExpiresAt ?? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue)
            .ThenBy(o => settings.PreferTaskChests && o.Kind is ClaimKind.DailyTaskChest or ClaimKind.WeeklyTaskChest ? 0 : 1)
            .ThenBy(o => o.SourceKey, StringComparer.Ordinal);
        var decisions = new List<ClaimDecision>();
        int selected = 0;
        foreach (var observation in ordered)
        {
            var reason = BlockReason(settings, observation, now, duplicates);
            if (reason is null && selected >= settings.MaximumClaimsPerRun)
                reason = "Per-run limit reached";
            bool include = reason is null;
            if (include) selected++;
            decisions.Add(new(observation, include, reason ?? "Eligible for preview; no action sent"));
        }
        return new(now, "preview-only", decisions);
    }

    private static string? BlockReason(DailyClaimSettings settings, ClaimObservation item,
        DateTimeOffset now, HashSet<string> duplicates)
    {
        if (!settings.Enabled) return "Daily claims disabled";
        if (string.IsNullOrWhiteSpace(item.SourceKey) || string.IsNullOrWhiteSpace(item.RewardName)
            || item.SourceKey.Length > 200 || item.RewardName.Length > 500 || !Enum.IsDefined(item.Kind))
            return "Invalid observation identity";
        if (duplicates.Contains(item.SourceKey)) return "Duplicate source identity";
        if (!settings.EnabledKinds.Contains(item.Kind)) return "Reward category disabled";
        var age = now - item.CapturedAt;
        if (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(settings.MaxSnapshotAgeSeconds))
            return "Observation stale or dated in the future";
        if (item.ExpiresAt <= now) return "Reward expired";
        if (item.FreeConfirmed is not true || item.CurrencyCost != 0m)
            return "Free eligibility or zero cost not confirmed";
        if (item.RemainingFreeClaims is null or <= 0) return "No confirmed free claims remaining";
        if (item.ClaimButtonVisible is not true) return "Claim availability not confirmed";
        return null;
    }
}
