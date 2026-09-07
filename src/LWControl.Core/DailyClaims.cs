namespace LWControl.Core;

public enum ClaimKind
{
    VipDailyReward, StoreFreePack, DailyTaskChest, WeeklyTaskChest,
    LoginReward, TavernFreeRecruit, CampaignIdleReward
}

public enum ClaimSourceStatus
{
    Unknown,
    Unavailable,
    NotUnlocked,
    AlreadyClaimed,
    Claimable,
    BlockedByPaidRequirement,
    TemporarilyUnavailable,
    RouteUnconfirmed
}

// Independent application models. These are not the original bridge protocol.
public sealed record DailyClaimSettings
{
    public bool Enabled { get; init; }
    public HashSet<ClaimKind> EnabledKinds { get; init; } = [.. Enum.GetValues<ClaimKind>()];
    public int MaximumClaimsPerRun { get; init; } = 20;
    public int MaxSnapshotAgeSeconds { get; init; } = 60;
    public bool PreferExpiringRewards { get; init; } = true;
    public bool PreferTaskChests { get; init; } = true;

    public void Validate()
    {
        if (EnabledKinds is null || EnabledKinds.Any(k => !Enum.IsDefined(k)))
            throw new ArgumentException("Unknown reward category.");
        if (MaximumClaimsPerRun is < 1 or > 20)
            throw new ArgumentException("Claim limit must be between 1 and 20.");
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
    public string? RewardId { get; init; }
    public ClaimSourceStatus Status { get; init; } = ClaimSourceStatus.Unknown;
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool? FreeConfirmed { get; init; }
    public decimal? CurrencyCost { get; init; }
    public bool HasCurrencyCost { get; init; }
    public string? CurrencyType { get; init; }
    public int? RemainingFreeClaims { get; init; }
    public bool? ClaimButtonVisible { get; init; }
    public string? ClaimButtonSemantic { get; init; }
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
            // The embedded daily-claim runtime receives PreferExpiringRewards but does not
            // use expiry in its candidate sort. Match the executing runtime here.
            .OrderBy(o => settings.PreferTaskChests && Enum.IsDefined(o.Kind)
                && RecoveredDailyClaimPolicy.IsTaskChest(o.Kind) ? 0 : 1)
            .ThenByDescending(o => Enum.IsDefined(o.Kind)
                ? RecoveredDailyClaimPolicy.Priority(o.Kind) : int.MinValue)
            .ThenBy(o => Enum.IsDefined(o.Kind)
                ? RecoveredDailyClaimPolicy.AdapterId(o.Kind) : "~invalid", StringComparer.Ordinal)
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
        return new(now, "preview-only/recovered-runtime-policy", decisions);
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
        return RecoveredDailyClaimPolicy.BlockReason(item);
    }
}

public static class RecoveredDailyClaimPolicy
{
    public static string AdapterId(ClaimKind kind) => kind switch
    {
        ClaimKind.VipDailyReward => "vip_daily_reward",
        ClaimKind.StoreFreePack => "store_daily_free_pack",
        ClaimKind.DailyTaskChest => "daily_task_chest",
        ClaimKind.WeeklyTaskChest => "weekly_task_chest",
        ClaimKind.LoginReward => "login_reward",
        ClaimKind.TavernFreeRecruit => "tavern_free_recruit",
        ClaimKind.CampaignIdleReward => "campaign_idle_reward",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static int Priority(ClaimKind kind) => kind switch
    {
        ClaimKind.DailyTaskChest => 900,
        ClaimKind.WeeklyTaskChest => 890,
        ClaimKind.VipDailyReward => 700,
        ClaimKind.StoreFreePack => 690,
        ClaimKind.LoginReward => 650,
        ClaimKind.TavernFreeRecruit => 600,
        ClaimKind.CampaignIdleReward => 500,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool IsTaskChest(ClaimKind kind) =>
        kind is ClaimKind.DailyTaskChest or ClaimKind.WeeklyTaskChest;

    public static string? BlockReason(ClaimObservation item)
    {
        // This mirrors free_confirmed() in the embedded LWC2DailyFreeClaims Lua
        // runtime. The local freshness/identity/expiry checks remain in the caller.
        if (item.Status != ClaimSourceStatus.Claimable)
            return $"Source is not claimable ({item.Status})";
        if (item.CurrencyCost != 0m || item.HasCurrencyCost)
            return "Zero-cost condition not confirmed";
        if (item.RemainingFreeClaims is > 0)
            return null;
        if (item.FreeConfirmed is true && item.ClaimButtonSemantic is "free" or "claim")
            return null;
        return "Free condition unconfirmed";
    }
}
