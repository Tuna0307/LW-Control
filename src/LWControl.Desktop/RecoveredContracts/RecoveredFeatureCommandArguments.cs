using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWControl.Desktop;

internal static class RecoveredFeatureCommandArguments
{
    public static void Apply(string featureId, JsonObject? config, bool enabled,
        IDictionary<string, string?> arguments)
    {
        if (featureId is "auto_radar" or "auto_join_rally" or "daily_free_claims" or "troop_promotion")
            arguments["enabled"] = Text(enabled);
        if (featureId == "use_stamina_item")
            arguments["auto_stamina_enabled"] = Text(enabled);
        if (config is null) return;

        switch (featureId)
        {
            case "auto_attack":
                PutInt(config, "searchIncrement", arguments, "search_increment", 1, 200);
                PutJson(config, "squadAfkProfiles", arguments, "team_afk_profiles_json");
                PutJson(config, "squadAutoEventPolicies", arguments, "squad_auto_event_policy_json");
                PutInt(config, "autoEventIdleThresholdSeconds", arguments, "auto_event_idle_threshold_seconds", 0, 3600);
                break;

            case "use_stamina_item":
                arguments["auto_stamina_enabled"] = Text(enabled);
                PutInt(config, "minimumStamina", arguments, "auto_stamina_threshold", 0, int.MaxValue);
                bool preferFifty = Bool(config, "preferFiftyPoint");
                arguments["auto_stamina_prefer_fifty"] = Text(preferFifty);
                arguments["preferred_recovery"] = preferFifty ? "50" : "10";
                break;

            case "auto_radar":
                arguments["enabled"] = Text(enabled);
                PutInt(config, "intervalSeconds", arguments, "interval_seconds", 1, 3600);
                PutInt(config, "idleRetrySeconds", arguments, "idle_retry_seconds", 1, 3600);
                PutJoined(config, "allowedTaskTypes", arguments, "allowed_task_types");
                PutJoined(config, "blockedTaskTypes", arguments, "blocked_task_types");
                PutBool(config, "claimCompletedFirst", arguments, "claim_completed_first");
                PutBool(config, "priorityByRarity", arguments, "priority_by_rarity");
                PutBool(config, "priorityByExpiration", arguments, "priority_by_expiration");
                PutBool(config, "priorityByDistance", arguments, "priority_by_distance");
                PutInt(config, "minimumRarity", arguments, "minimum_rarity", 0, int.MaxValue);
                PutInt(config, "maximumRarity", arguments, "maximum_rarity", 0, int.MaxValue);
                PutInt(config, "minimumRemainingSeconds", arguments, "minimum_remaining_seconds", 0, int.MaxValue);
                PutInt(config, "minimumStamina", arguments, "minimum_stamina", 0, int.MaxValue);
                PutInt(config, "reservedSquadCount", arguments, "reserved_squad_count", 0, 4);
                PutNullableInt(config, "preferredSquad", arguments, "preferred_squad", 1, 4);
                PutNumber(config, "maximumDistance", arguments, "maximum_distance", 0, double.MaxValue);
                PutInt(config, "maximumTasksPerRun", arguments, "maximum_tasks_per_run", 1, int.MaxValue);
                PutBool(config, "keepUncompletedTasks", arguments, "keep_uncompleted_tasks");
                PutInt(config, "minimumStoredTasks", arguments, "minimum_stored_tasks", 0, int.MaxValue);
                PutBool(config, "onlyRunDuringEvent", arguments, "only_run_during_event");
                PutBool(config, "allowCombatTasks", arguments, "allow_combat_tasks");
                PutBool(config, "allowDispatchTasks", arguments, "allow_dispatch_tasks");
                PutBool(config, "allowStaminaItems", arguments, "allow_stamina_items");
                arguments["maximum_retries"] = "1";
                arguments["operation_timeout_seconds"] = "30";
                break;

            case "auto_join_rally":
                arguments["enabled"] = Text(enabled);
                PutInt(config, "intervalSeconds", arguments, "interval_seconds", 1, 3600);
                PutInt(config, "listenerTimeoutSeconds", arguments, "timeout_seconds", 1, 86400);
                PutInt(config, "maximumJoinsPerSession", arguments, "max_joins", 0, int.MaxValue);
                PutJoined(config, "allowedTargetTypes", arguments, "allowed_target_types");
                PutJoined(config, "blockedTargetTypes", arguments, "blocked_target_types");
                PutJoined(config, "preferredLeaderIds", arguments, "preferred_leader_ids");
                PutJoined(config, "allowedLeaderIds", arguments, "allowed_leader_ids");
                PutJoined(config, "blockedLeaderIds", arguments, "blocked_leader_ids");
                PutBool(config, "memberWhitelistEnabled", arguments, "member_whitelist_enabled");
                PutBool(config, "memberBlacklistEnabled", arguments, "member_blacklist_enabled");
                PutJoined(config, "whitelistedMemberNames", arguments, "whitelisted_member_names");
                PutJoined(config, "blacklistedMemberNames", arguments, "blacklisted_member_names");
                PutBool(config, "whitelistBypassesTargetLevel", arguments, "whitelist_bypasses_target_level");
                PutNullableInt(config, "minimumTargetLevel", arguments, "minimum_target_level", 0, int.MaxValue);
                PutNullableInt(config, "maximumTargetLevel", arguments, "maximum_target_level", 0, int.MaxValue);
                PutDuration(config, "minimumRemainingTime", arguments, "minimum_remaining_seconds");
                PutDuration(config, "maximumRemainingTime", arguments, "maximum_remaining_seconds");
                PutDuration(config, "joinSafetyBuffer", arguments, "join_safety_buffer_seconds");
                PutNumber(config, "maximumDistance", arguments, "maximum_distance", 0, double.MaxValue);
                PutDuration(config, "maximumMarchTime", arguments, "maximum_march_seconds");
                PutInt(config, "minimumRemainingStamina", arguments, "minimum_remaining_stamina", 0, int.MaxValue);
                PutInt(config, "reservedIdleSquadCount", arguments, "reserved_idle_squad_count", 0, 4);
                PutJoined(config, "preferredSquadOrder", arguments, "preferred_squad_order");
                PutJoined(config, "allowedSquads", arguments, "allowed_squads");
                PutJson(config, "teamJoinProfiles", arguments, "team_join_profiles_json");
                PutInt(config, "minimumMemberCount", arguments, "minimum_member_count", 1, 5);
                PutInt(config, "maximumJoinsPerRun", arguments, "maximum_joins_per_run", 1, 1);
                PutBool(config, "preferPreferredLeaders", arguments, "prefer_preferred_leaders");
                PutBool(config, "preferNearlyFullRallies", arguments, "prefer_nearly_full_rallies");
                PutBool(config, "preferShortMarchTime", arguments, "prefer_short_march_time");
                PutBool(config, "preferHigherTargetLevel", arguments, "prefer_higher_target_level");
                PutPairs(config, "targetTypePriorities", arguments, "target_type_priorities");
                PutBool(config, "skipUnknownLeader", arguments, "skip_unknown_leader");
                PutBool(config, "skipUnknownTargetType", arguments, "skip_unknown_target_type");
                PutBool(config, "skipUnknownMarchTime", arguments, "skip_unknown_march_time");
                PutBool(config, "retryWhenRallyExpiresOrFills", arguments, "retry_when_rally_expires_or_fills");
                arguments["allow_stamina_items"] = "false";
                arguments["maximum_retries"] = "1";
                arguments["operation_timeout_seconds"] = "30";
                break;

            case "daily_free_claims":
                arguments["enabled"] = Text(enabled);
                PutJoined(config, "enabledAdapterIds", arguments, "enabled_adapter_ids");
                PutJoined(config, "blockedAdapterIds", arguments, "blocked_adapter_ids");
                PutInt(config, "maximumClaimsPerRun", arguments, "maximum_claims_per_run", 1, 20);
                PutBool(config, "claimDailyTaskChests", arguments, "claim_daily_task_chests");
                PutBool(config, "claimWeeklyTaskChests", arguments, "claim_weekly_task_chests");
                PutBool(config, "claimStoreFreePacks", arguments, "claim_store_free_packs");
                PutBool(config, "claimVipRewards", arguments, "claim_vip_rewards");
                PutBool(config, "claimLoginRewards", arguments, "claim_login_rewards");
                PutBool(config, "claimTavernFreeRecruit", arguments, "claim_tavern_free_recruit");
                PutBool(config, "claimIdleRewards", arguments, "claim_idle_rewards");
                PutBool(config, "preferExpiringRewards", arguments, "prefer_expiring_rewards");
                PutBool(config, "preferTaskChests", arguments, "prefer_task_chests");
                PutBool(config, "stopOnUnknownCost", arguments, "stop_on_unknown_cost");
                arguments["free_only"] = "true";
                arguments["allow_advertisement_claims"] = "false";
                arguments["allow_tickets"] = "false";
                arguments["allow_premium_currency"] = "false";
                arguments["background"] = "false";
                break;

            case "troop_promotion":
                arguments["enabled"] = Text(enabled);
                PutJoined(config, "allowedBarracksIds", arguments, "allowed_barracks_ids");
                PutJoined(config, "preferredBarracksOrder", arguments, "preferred_barracks_order");
                PutJoined(config, "allowedSourceTiers", arguments, "allowed_source_tiers");
                PutScalar(config, "targetMode", arguments, "target_mode");
                PutNullableInt(config, "fixedTargetTier", arguments, "fixed_target_tier", 1, int.MaxValue);
                PutScalar(config, "sourceOrder", arguments, "source_order");
                PutJoined(config, "preferredSourceTierOrder", arguments, "preferred_source_tier_order");
                PutPairs(config, "minimumTroopsToKeepByTier", arguments, "minimum_troops_to_keep_by_tier");
                arguments["maximum_actions_per_run"] = "1";
                PutLong(config, "maximumUnitsPerAction", arguments, "maximum_units_per_action", 1, long.MaxValue);
                PutLong(config, "maximumUnitsPerRun", arguments, "maximum_units_per_run", 1, long.MaxValue);
                PutPairs(config, "resourceReserveByType", arguments, "resource_reserve_by_type");
                PutBool(config, "allowDirectPromotion", arguments, "allow_direct_promotion");
                PutBool(config, "allowOneTierStepPromotion", arguments, "allow_one_tier_step_promotion");
                PutBool(config, "onlyDuringTrainingScoringWindow", arguments, "only_during_training_scoring_window");
                PutBool(config, "requireConfirmedScoringWindow", arguments, "require_confirmed_scoring_window");
                arguments["stop_on_unknown_cost"] = "true";
                arguments["allow_use_all_button"] = "false";
                arguments["allow_speedups"] = "false";
                arguments["allow_premium_currency"] = "false";
                arguments["allow_resource_purchase"] = "false";
                arguments["background"] = "false";
                arguments["maximum_retries"] = "1";
                break;

            case "alliance_train":
                PutInt(config, "intervalSeconds", arguments, "interval_seconds", 10, 3600);
                PutBool(config, "prioritizeRewardQuantity", arguments, "prioritize_reward_quantity");
                PutJson(config, "rewardPriority", arguments, "reward_priority_json");
                break;

            case "hospital_heal":
                PutInt(config, "maximumSoldiersPerRun", arguments, "max_soldiers", 1, 100000);
                break;

            case "apply_position":
                PutScalar(config, "positionId", arguments, "position_id");
                break;
        }
    }

    private static void PutScalar(JsonObject config, string property, IDictionary<string, string?> arguments, string key)
    {
        string? value = Scalar(config[property]);
        if (!string.IsNullOrWhiteSpace(value)) arguments[key] = value.Trim();
    }

    private static void PutBool(JsonObject config, string property, IDictionary<string, string?> arguments, string key)
    {
        JsonNode? node = config[property];
        if (node is not null && bool.TryParse(Scalar(node), out bool value)) arguments[key] = Text(value);
    }

    private static void PutInt(JsonObject config, string property, IDictionary<string, string?> arguments, string key, int min, int max)
    {
        if (int.TryParse(Scalar(config[property]), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            arguments[key] = Math.Clamp(value, min, max).ToString(CultureInfo.InvariantCulture);
    }

    private static void PutNullableInt(JsonObject config, string property, IDictionary<string, string?> arguments, string key, int min, int max)
    {
        JsonNode? node = config[property];
        if (node is null) return;
        PutInt(config, property, arguments, key, min, max);
    }

    private static void PutLong(JsonObject config, string property, IDictionary<string, string?> arguments, string key, long min, long max)
    {
        if (long.TryParse(Scalar(config[property]), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            arguments[key] = Math.Clamp(value, min, max).ToString(CultureInfo.InvariantCulture);
    }

    private static void PutNumber(JsonObject config, string property, IDictionary<string, string?> arguments, string key, double min, double max)
    {
        if (double.TryParse(Scalar(config[property]), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            arguments[key] = Math.Clamp(value, min, max).ToString(CultureInfo.InvariantCulture);
    }

    private static void PutDuration(JsonObject config, string property, IDictionary<string, string?> arguments, string key)
    {
        string? text = Scalar(config[property]);
        if (string.IsNullOrWhiteSpace(text)) return;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan value))
            arguments[key] = Math.Max(0, (long)value.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        else if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds))
            arguments[key] = Math.Max(0, seconds).ToString(CultureInfo.InvariantCulture);
    }

    private static void PutJoined(JsonObject config, string property, IDictionary<string, string?> arguments, string key)
    {
        if (config[property] is not JsonArray values) return;
        arguments[key] = string.Join(',', values.Select(Scalar).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void PutPairs(JsonObject config, string property, IDictionary<string, string?> arguments, string key)
    {
        if (config[property] is not JsonObject values) return;
        arguments[key] = string.Join(',', values
            .Where(pair => !string.IsNullOrWhiteSpace(Scalar(pair.Value)))
            .Select(pair => pair.Key + ":" + Scalar(pair.Value)));
    }

    private static void PutJson(JsonObject config, string property, IDictionary<string, string?> arguments, string key)
    {
        JsonNode? node = config[property];
        if (node is not null) arguments[key] = node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static bool Bool(JsonObject config, string property) =>
        bool.TryParse(Scalar(config[property]), out bool value) && value;

    private static string? Scalar(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out string? text)) return text;
            if (value.TryGetValue<bool>(out bool boolean)) return Text(boolean);
            if (value.TryGetValue<int>(out int integer)) return integer.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<long>(out long longInteger)) return longInteger.ToString(CultureInfo.InvariantCulture);
            if (value.TryGetValue<double>(out double number)) return number.ToString(CultureInfo.InvariantCulture);
        }
        return node.ToJsonString().Trim('"');
    }

    private static string Text(bool value) => value ? "true" : "false";
}
