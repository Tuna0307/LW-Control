namespace LWControl.Desktop;

internal enum ReferenceFeatureGroup
{
    MapData,
    SquadsAfk,
    AutomationDaily,
    AutomationEvent,
    AutomationAlliance,
}

internal enum FeatureImplementationState
{
    Pending,
    Partial,
    Available,
}

internal sealed record ReferenceFeature(
    string Id,
    string Name,
    string Description,
    ReferenceFeatureGroup Group,
    IReadOnlyList<string> Actions,
    FeatureImplementationState State = FeatureImplementationState.Pending);

internal static class ReferenceFeatureCatalog
{
    public static readonly IReadOnlyList<ReferenceFeature> All =
    [
        F("continuous_gathering", "Continuous Gathering", "Cycle selected resources, reserve one idle squad, and strictly verify every gathering dispatch.", ReferenceFeatureGroup.SquadsAfk, "Status", "Gather Once", "Start Gathering", "Stop Gathering"),
        F("map_scan", "World Scan", "Scan players, resources, monsters, and alliance targets.", ReferenceFeatureGroup.MapData, FeatureImplementationState.Available, "Scan"),
        F("quick_attack", "Quick Attack / Recall", "Attack with Q/W/E/R and recall with A/S/D/F.", ReferenceFeatureGroup.SquadsAfk, "Read-only Probe", "Q Team 1", "W Team 2", "E Team 3", "R Team 4", "A Recall 1", "S Recall 2", "D Recall 3", "F Recall 4"),
        F("secret_mobile_squad", "Secret Mobile Squad", "Open the official dispatch screen, dispatch eligible heroes, and claim completed rewards.", ReferenceFeatureGroup.SquadsAfk, "Open Dispatch Tasks", "Dispatch", "Claim"),
        F("zombie_gold", "Zombie / Auto Gold", "Attack a zombie, gather gold once, or continuously dispatch idle squads to gold mines.", ReferenceFeatureGroup.SquadsAfk, "Start Auto Gold", "Gold Once", "Zombie", "Status", "Stop Auto Gold"),
        F("auto_join_rally", "Auto Join Rally", "Filter alliance rallies and join with an idle squad.", ReferenceFeatureGroup.SquadsAfk, "Start Auto Join", "Status", "Stop Auto Join"),
        F("region_jump", "Region Jump", "Read the current server and jump to another region or return home.", ReferenceFeatureGroup.SquadsAfk, "Refresh", "Jump", "Return Home"),
        F("team_swap_outfit", "Squad Equipment", "Swap squad gear and save or apply equipment schemes.", ReferenceFeatureGroup.SquadsAfk, "Swap Gear", "Save Scheme", "Apply Scheme"),
        F("random_teleport", "Random / Alliance Teleport", "F9 triggers random teleport; alliance teleport remains a menu action.", ReferenceFeatureGroup.SquadsAfk, "F9 Random", "Alliance Teleport"),
        F("hospital_heal", "Hospital Heal", "Treat wounded troops and verify queue changes.", ReferenceFeatureGroup.SquadsAfk, "Heal Now"),
        F("auto_reconnect", "Auto Reconnect", "Monitor exits or disconnects and recover the game.", ReferenceFeatureGroup.SquadsAfk, "Enable", "Stop"),

        F("auto_mail_claim", "Read & Claim Mail", "Read mail and claim valid attachments.", ReferenceFeatureGroup.AutomationDaily, "Read & Claim"),
        F("auto_radar", "Auto Radar", "Scan, filter, claim, dispatch, and fight radar tasks.", ReferenceFeatureGroup.AutomationDaily, "Read-only Probe", "Run Once", "Pause"),
        F("auto_truck", "Auto Truck", "Select a truck, cargo, and guards before departure.", ReferenceFeatureGroup.AutomationDaily, "Depart"),
        F("camp_armored_reward", "Camp / Armored Rewards", "Claim camp and armored vehicle rewards.", ReferenceFeatureGroup.AutomationDaily, "Collect"),
        F("alliance_train", "Alliance Train", "Queue, board, observe, and claim train rewards.", ReferenceFeatureGroup.AutomationDaily, "Queue", "Board", "Observe", "Claim"),
        F("auto_train", "Auto Train", "Claim completed queues and start the next training.", ReferenceFeatureGroup.AutomationDaily, "Train Once"),
        F("troop_promotion", "Troop Promotion", "Promote eligible troops to the allowed tier.", ReferenceFeatureGroup.AutomationDaily, "Run Once", "Pause"),
        F("apply_position", "Apply Position", "Apply for an available position and verify appointment.", ReferenceFeatureGroup.AutomationDaily, "Apply Now"),
        F("alliance_gift_claim", "Alliance Gifts", "Claim alliance gifts and reward chests.", ReferenceFeatureGroup.AutomationDaily, "Claim All"),
        F("use_stamina_item", "Use Stamina Item", "Safely use stamina recovery items at a threshold.", ReferenceFeatureGroup.AutomationDaily, "Use One", "Use at Threshold"),
        F("auto_reward_collect", "Reward Collector", "Collect currently available task and building rewards.", ReferenceFeatureGroup.AutomationDaily, "Collect All"),
        F("daily_free_claims", "Daily Free Claims", "Claim only rewards verified as free.", ReferenceFeatureGroup.AutomationDaily, FeatureImplementationState.Partial, "Run Once", "Pause"),
        F("auto_attack", "Auto Attack", "Search for a target and dispatch an attack squad.", ReferenceFeatureGroup.AutomationDaily, "Attack Once", "Start", "Stop"),
        F("auto_rally", "Auto Rally", "Select a target and create an alliance rally.", ReferenceFeatureGroup.AutomationDaily, "Create Once", "Start", "Stop"),
        F("auto_chat", "Auto Chat", "Send alliance notices or scheduled messages.", ReferenceFeatureGroup.AutomationDaily, "Send Once"),

        F("fireworks", "Fireworks", "Use one firework and verify the item, event queue, and alliance points.", ReferenceFeatureGroup.AutomationEvent, "Use One"),
        F("red_packet", "Red Packet", "Claim one valid red packet and verify the chat record and server grant.", ReferenceFeatureGroup.AutomationEvent, "Claim One"),
        F("golden_egg", "Golden Egg", "Open one claimable golden egg and verify its queue and reward.", ReferenceFeatureGroup.AutomationEvent, "Open One"),
        F("treasure_hunt", "Auto Excavator", "Scan official dig sites and excavate within configured spend limits.", ReferenceFeatureGroup.AutomationEvent, "Run One Cycle", "Start Auto Dig", "Refresh Status", "Claim Dig Reward", "Fragment Dig", "Stop Auto Dig"),
        F("plane_mission", "Trade Plane Takeoff", "Open the Business Center and observe official state before taking off once or on an interval. Independent claim, dispatch, and reward priority are unsupported.", ReferenceFeatureGroup.AutomationEvent, "Open Business Center", "Read-only Probe", "Take Off Once", "Start Auto Takeoff", "Refresh Status", "Stop Auto Takeoff"),
        F("double_reward_tracker", "Double Reward", "Read official multiplier windows; currently only verified matching excavation activities are scheduled automatically.", ReferenceFeatureGroup.AutomationEvent, "Check Multiplier", "Read-only Probe", "Start Tracker", "Stop Tracker"),
        F("arms_race_alliance_duel", "Arms Race + Alliance Duel", "Read official tasks and scores, run only exactly mapped training or promotion tasks, and verify both progress and score growth.", ReferenceFeatureGroup.AutomationEvent, "Check Events", "Read-only Contract Probe", "Run Smart Once", "Start Smart Mode", "Stop Smart Mode"),
        F("mining_dispatch", "Mining Dispatch", "Dispatch a gathering squad or recall one.", ReferenceFeatureGroup.AutomationEvent, "Dispatch", "Recall"),
        F("ghost_scout", "Ghost Scout", "Start a personal ghost task or claim one completed task reward.", ReferenceFeatureGroup.AutomationEvent, "Start Task", "Claim Reward"),

        F("alliance_help", "Alliance Help", "Help alliance building, technology, and healing requests.", ReferenceFeatureGroup.AutomationAlliance, "Help Once", "Start", "Stop"),
        F("alliance_tech_donate", "Alliance Tech Donate", "Donate to alliance technology and verify contribution.", ReferenceFeatureGroup.AutomationAlliance, "Donate Once", "Start", "Stop"),
        F("resource_grab", "Resource Grab", "Select a transport or resource target and attack.", ReferenceFeatureGroup.AutomationAlliance, "Grab Once"),
        F("shield_display", "Shield Display", "Display player shield status and remaining time.", ReferenceFeatureGroup.AutomationAlliance, "Show / Refresh"),
        F("performance_overlay", "FPS / PING Overlay", "Display live FPS and network latency in game.", ReferenceFeatureGroup.AutomationAlliance, "Toggle FPS", "Toggle PING"),
        F("alliance_ghost_scout", "Alliance Ghost Scout", "Select a recommended task and assist an ally once.", ReferenceFeatureGroup.AutomationAlliance, "Assist Once"),
        F("secret_task", "Secret Task", "Refresh for free, dispatch heroes, and claim secret-task rewards.", ReferenceFeatureGroup.AutomationAlliance, "Free Refresh", "Dispatch One", "Claim One"),
    ];

    static ReferenceFeatureCatalog()
    {
        if (All.Count != 42 || All.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 42)
            throw new InvalidOperationException("Recovered LW Control feature catalog must contain exactly 42 unique features.");
    }

    public static IReadOnlyList<ReferenceFeature> InGroup(ReferenceFeatureGroup group) =>
        All.Where(item => item.Group == group).ToArray();

    private static ReferenceFeature F(
        string id, string name, string description, ReferenceFeatureGroup group, params string[] actions) =>
        new(id, name, description, group, actions);

    private static ReferenceFeature F(
        string id, string name, string description, ReferenceFeatureGroup group,
        FeatureImplementationState state, params string[] actions) =>
        new(id, name, description, group, actions, state);
}
