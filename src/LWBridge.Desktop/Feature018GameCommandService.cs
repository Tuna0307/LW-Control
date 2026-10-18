using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LWBridge.Desktop;

/// <summary>
/// Admission and exact 0.3.17 front-end schemas for the FEATURE-018 game
/// module. Client-class action semantics belong to source-verified Lua
/// handlers, not to these serialized requests.
/// </summary>
internal sealed class Feature018GameCommandService : INativeAsyncCommandService
{
    private const int EquipmentPresetWireLimit = 14000;
    private readonly Feature018OwnedGameExchange exchange;
    private readonly EquipmentConfigCommandService? equipmentConfig;
    internal Feature018GameCommandService(OverviewLifecycleService lifecycle)
        : this(new Feature018OwnedGameExchange(lifecycle)) { }
    internal Feature018GameCommandService(OverviewLifecycleService lifecycle,
        EquipmentConfigCommandService equipmentConfig)
        : this(new Feature018OwnedGameExchange(lifecycle), equipmentConfig) { }
    internal Feature018GameCommandService(Feature018OwnedGameExchange exchange)
        : this(exchange, null) { }
    internal Feature018GameCommandService(Feature018OwnedGameExchange exchange,
        EquipmentConfigCommandService? equipmentConfig)
    {
        this.exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        this.equipmentConfig = equipmentConfig;
    }

    public bool CanHandle(string command) => command is
        "city_layout_snapshot_get" or "city_layout_validate" or "city_layout_apply_start" or
        "city_layout_apply_status" or "city_layout_apply_cancel" or
        "city_layout_restore_preflight" or "city_layout_restore_start" or
        "monopoly_cell_open" or "mini_land_status" or "mini_sheep_status" or "mini_sheep_board" or
        "mini_chest_position" or "mini_chest_mark" or "mini_chest_unmark" or
        "mini_frontline_status" or
        "automation_start" or "automation_stop" or "feature018_sheep_automation_status" or
        "feature018_input_focus" or
        "feature018_rapid_action" or
        "feature018_relocation_preflight" or
        "feature018_attack_prepare" or
        "feature018_shield_inventory_get" or
        "feature018_shield_shop" or
        "feature018_frontline_action" or
        "feature018_equipment_preflight" or
        "feature018_equipment_snapshot" or
        "feature018_hotkey_action";

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!CanHandle(command))
            throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", "Unsupported FEATURE018 command.");
        Dictionary<string, string> fields = ReadFields(command, payload);
        Feature018AuthenticatedGameAccount? capturedHotkeyAccount = null;
        if (command == "feature018_hotkey_action")
        {
            bool hasUid = payload.TryGetProperty("expectedGameUid", out JsonElement uid);
            bool hasServer = payload.TryGetProperty("expectedServerId", out JsonElement server);
            if (hasUid != hasServer)
                throw new BridgeCommandException("INVALID_REQUEST",
                    "A captured hotkey identity requires both UID and server.");
            if (hasUid)
            {
                if (uid.ValueKind != JsonValueKind.String ||
                    uid.GetString() is not { Length: > 0 and <= 20 } gameUid ||
                    gameUid[0] == '0' || !gameUid.All(char.IsAsciiDigit) ||
                    server.ValueKind != JsonValueKind.Number ||
                    !server.TryGetInt32(out int serverId) || serverId <= 0)
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Invalid captured hotkey UID/server.");
                capturedHotkeyAccount = new Feature018AuthenticatedGameAccount(gameUid, serverId);
            }
        }
        if (command == "feature018_hotkey_action" && fields["action"] == "equipment")
        {
            JsonElement ordinal = JsonSerializer.SerializeToElement(new
            { slot = int.Parse(fields["slot"], CultureInfo.InvariantCulture) });
            Dictionary<string, string> trusted = await ReadConfirmedEquipmentPreflightFieldsAsync(
                ordinal, cancellationToken).ConfigureAwait(false);
            fields["confirmedEquipmentPresets"] = trusted["confirmedEquipmentPresets"];
            fields["presetId"] = trusted["presetId"];
        }
        if (command == "feature018_equipment_preflight")
        {
            // Owner-scoped confirmed config comes from the profile runtime's
            // persistent server-side store. Neither UI gear maps nor a UID
            // supplied in this request may influence the game mailbox.
            fields = await ReadConfirmedEquipmentPreflightFieldsAsync(
                payload, cancellationToken).ConfigureAwait(false);
            string savedSnapshot = fields["confirmedEquipmentPresets"];
            JsonElement observed = await exchange.ExchangeAsync(command, fields,
                TimeSpan.FromSeconds(8), cancellationToken,
                allowAcceptedDispatch: false).ConfigureAwait(false);
            Dictionary<string, string> latest =
                await ReadConfirmedEquipmentPreflightFieldsAsync(
                    payload, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(savedSnapshot, latest["confirmedEquipmentPresets"],
                    StringComparison.Ordinal) ||
                !string.Equals(fields["presetId"], latest["presetId"],
                    StringComparison.Ordinal))
                throw new BridgeCommandException("GAME_ACTION_UNCONFIRMED",
                    "Equipment preset changed during read-only native inventory observation.");
            RequireReadOnlyEquipmentPreflight(observed, fields);
            return observed;
        }
        TimeSpan timeout = command switch
        {
            "city_layout_apply_start" or "city_layout_restore_start" => TimeSpan.FromSeconds(30),
            "city_layout_validate" => TimeSpan.FromSeconds(10),
            "city_layout_snapshot_get" => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(8),
        };
        bool acceptsSubmission = command is "city_layout_apply_start" or "city_layout_restore_start" or "monopoly_cell_open" or
            "automation_start" or "automation_stop" or "feature018_hotkey_action" or
            "feature018_rapid_action" ||
            // cursorOpen only enters the source-native formation chooser;
            // the Lua receipt explicitly reports accepted:true and
            // requestDispatched:false. Never construe it as a march send.
            (command == "feature018_attack_prepare" && fields["mode"] == "cursorOpen") ||
            (command == "feature018_shield_shop" && fields["mode"] is "buy" or "status");
        return await exchange.ExchangeAsync(command, fields, timeout, cancellationToken,
            allowAcceptedDispatch: acceptsSubmission,
            expectedAccount: capturedHotkeyAccount).ConfigureAwait(false);
    }

    internal static Dictionary<string, string> ReadFields(string command, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_REQUEST", "FEATURE018 game payload must be an object.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (command)
        {
            case "feature018_attack_prepare":
            {
                string mode = ReadValue(payload,"mode",16);
                if (mode is not ("inventory" or "open" or "staffingEditor" or
                    "selectorTarget" or "attackReadiness" or "cursorPoint" or "cursorOpen"))
                    throw new BridgeCommandException("INVALID_REQUEST","Invalid attack prepare mode.");
                var attackPrepareKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty entry in payload.EnumerateObject())
                {
                    bool allowed = entry.Name is "mode" or "profileId" ||
                        (mode == "open" && entry.Name == "targetUuid") ||
                        (mode == "attackReadiness" && entry.Name == "slot") ||
                        (mode == "cursorPoint" && entry.Name is "samplePixelX" or "samplePixelY") ||
                        (mode == "cursorOpen" && entry.Name == "targetKind") ||
                        (mode == "staffingEditor" && entry.Name is "slot" or "editorOptIn");
                    if (!allowed || !attackPrepareKeys.Add(entry.Name))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Native attack readiness accepts only distinct fields for its selected mode.");
                }
                fields["mode"] = mode;
                if (mode == "cursorOpen" && payload.TryGetProperty("targetKind", out JsonElement cursorKind))
                {
                    // The named kind is only an eligibility constraint. The
                    // game resolves the actual fresh Unity cursor, target
                    // identity and current server itself; no target UUID,
                    // point or screen coordinate may be supplied for a send.
                    if (cursorKind.ValueKind != JsonValueKind.String ||
                        cursorKind.GetString() is not ("monster" or "resource" or "city" or "ruin"))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Cursor selection requires a recognized native target kind.");
                    fields["targetKind"] = cursorKind.GetString()!;
                }
                if (mode == "cursorPoint")
                {
                    bool hasX = payload.TryGetProperty("samplePixelX", out JsonElement pixelX);
                    bool hasY = payload.TryGetProperty("samplePixelY", out JsonElement pixelY);
                    if (hasX != hasY)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Native cursor diagnostic requires both sample pixel coordinates.");
                    if (hasX)
                    {
                        if (pixelX.ValueKind != JsonValueKind.Number || pixelY.ValueKind != JsonValueKind.Number ||
                            !pixelX.TryGetInt32(out int x) || !pixelY.TryGetInt32(out int y) ||
                            x is < 0 or > 32767 || y is < 0 or > 32767)
                            throw new BridgeCommandException("INVALID_REQUEST",
                                "Native cursor diagnostic requires bounded integer screen pixels.");
                        // Diagnostic only. Never use client pixels as an
                        // authenticated attack target or mutating command.
                        fields["samplePixelX"] = x.ToString(CultureInfo.InvariantCulture);
                        fields["samplePixelY"] = y.ToString(CultureInfo.InvariantCulture);
                    }
                }
                if (mode == "attackReadiness")
                {
                    if (!payload.TryGetProperty("slot", out JsonElement readinessSlot) ||
                        readinessSlot.ValueKind != JsonValueKind.Number ||
                        !readinessSlot.TryGetInt32(out int chosen) || chosen is < 1 or > 4)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Read-only native attack prerequisites require squad slot 1–4.");
                    fields["slot"] = chosen.ToString(CultureInfo.InvariantCulture);
                }
                if (mode == "open")
                {
                    string uuid = ReadValue(payload,"targetUuid",20);
                    if (!ValidGameUuid(uuid)) throw new BridgeCommandException("INVALID_REQUEST","Invalid native monster UUID.");
                    fields["targetUuid"] = uuid;
                }
                if (mode == "staffingEditor")
                {
                    if (!payload.TryGetProperty("slot", out JsonElement editorSlot) ||
                        editorSlot.ValueKind != JsonValueKind.Number ||
                        !editorSlot.TryGetInt32(out int requested) || requested is < 1 or > 4 ||
                        !payload.TryGetProperty("editorOptIn", out JsonElement editorConsent) ||
                        editorConsent.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Current approved formation editor requires squad 1–4 and explicit manual-editor consent.");
                    fields["slot"] = requested.ToString(CultureInfo.InvariantCulture);
                    fields["editorOptIn"] = "1";
                }
                break;
            }
            case "feature018_relocation_preflight":
            {
                // Exactly one currently admitted native F9/F10 action name.
                // A browser cannot supply a destination, UID, item UUID,
                // rally target, spending authorization or game identity.
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty field in payload.EnumerateObject())
                {
                    if (field.Name is not ("action" or "profileId") ||
                        !unique.Add(field.Name))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Relocation preflight accepts only unique action and routing fields.");
                }
                string action = ReadValue(payload, "action", 32);
                if (action is not ("randomRelocate" or "allianceRelocate"))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Relocation preflight requires original F9 or F10.");
                if (payload.TryGetProperty("profileId", out JsonElement route) &&
                    (route.ValueKind != JsonValueKind.String ||
                     string.IsNullOrWhiteSpace(route.GetString()) ||
                     route.GetString()!.Length > 128))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Invalid relocation observation profile.");
                fields["action"] = action;
                break;
            }
            case "feature018_equipment_snapshot":
                if (payload.EnumerateObject().Any())
                    throw new BridgeCommandException("INVALID_REQUEST", "Equipment snapshot accepts an empty authenticated request only.");
                break;
            case "feature018_shield_shop":
            {
                string mode = ReadValue(payload, "mode", 16);
                if (mode is not ("inspect" or "buy" or "status" or "armUse") ||
                    !payload.TryGetProperty("slot", out JsonElement shieldSlot) ||
                    shieldSlot.ValueKind != JsonValueKind.Number ||
                    !shieldSlot.TryGetInt32(out int hours) || hours is not (8 or 12 or 24))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid native shield shop mode or duration.");
                // The profile ID is routing metadata and is never authority.
                // The only spend arguments are mode-specific *true* consent.
                // Unused/forged fields must not be silently accepted on a
                // read-only shop inspection or a different spending mode.
                var shieldShopKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in payload.EnumerateObject())
                {
                    bool known = property.Name is "mode" or "slot" or "profileId" ||
                        (mode == "buy" && property.Name == "gemOptIn") ||
                        (mode == "armUse" && property.Name == "shieldReplaceOptIn");
                    if (!known || !shieldShopKeys.Add(property.Name))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Native shield shop requires unique fields for the selected action and its exact confirmation.");
                }
                if (payload.TryGetProperty("profileId", out JsonElement shieldRoutingProfile) &&
                    (shieldRoutingProfile.ValueKind != JsonValueKind.String ||
                     string.IsNullOrWhiteSpace(shieldRoutingProfile.GetString()) ||
                     shieldRoutingProfile.GetString()!.Length > 128))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Invalid native shield shop routing profile.");
                fields["mode"] = mode;
                fields["slot"] = hours.ToString(CultureInfo.InvariantCulture);
                if (mode == "buy")
                {
                    if (!payload.TryGetProperty("gemOptIn", out JsonElement optIn) || optIn.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST", "Shield purchase requires explicit gem consent.");
                    fields["gemOptIn"] = "1";
                }
                if (mode == "armUse")
                {
                    if (!payload.TryGetProperty("shieldReplaceOptIn", out JsonElement replace) ||
                        replace.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST", "Next shield replacement requires explicit consent.");
                    fields["shieldReplaceOptIn"] = "1";
                }
                break;
            }
            case "city_layout_validate":
            case "city_layout_apply_start":
            {
                string revision = ReadValue(payload, "baseRevision", 128);
                if (!revision.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid city revision token.");
                if (!payload.TryGetProperty("placements", out JsonElement placements) ||
                    placements.ValueKind != JsonValueKind.Array || placements.GetArrayLength() is < 1 or > 1000)
                    throw new BridgeCommandException("INVALID_REQUEST", "City placements must contain 1–1000 changes.");
                var parsed = new List<string>();
                var uuids = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement placement in placements.EnumerateArray())
                {
                    if (placement.ValueKind != JsonValueKind.Object)
                        throw new BridgeCommandException("INVALID_REQUEST", "City placement must be an object.");
                    string uuid = ReadValue(placement, "uuid", 28);
                    if (!uuid.All(char.IsAsciiDigit) || uuid.Length == 0 || !uuids.Add(uuid))
                        throw new BridgeCommandException("INVALID_REQUEST", "Duplicate or invalid city building UUID.");
                    if (!placement.TryGetProperty("targetPointId", out JsonElement target) ||
                        !target.TryGetInt32(out int pointId) || pointId <= 0)
                        throw new BridgeCommandException("INVALID_REQUEST", "Invalid City target cell ID.");
                    parsed.Add(uuid + ":" + pointId.ToString(CultureInfo.InvariantCulture));
                }
                fields["baseRevision"] = revision;
                fields["placements"] = string.Join(';', parsed);
                break;
            }
            case "city_layout_apply_cancel":
            case "city_layout_restore_preflight":
            {
                string job = ReadValue(payload, "jobId", 128);
                if (!job.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid game apply job token.");
                fields["jobId"] = job;
                break;
            }
            case "city_layout_restore_start":
            {
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty entry in payload.EnumerateObject())
                {
                    if (entry.Name is not ("jobId" or "restoreOptIn" or "profileId") ||
                        !unique.Add(entry.Name))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Original City restore accepts only unique owner job and fresh consent.");
                }
                string job = ReadValue(payload, "jobId", 128);
                if (!job.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Invalid original City restoration job identifier.");
                if (!payload.TryGetProperty("restoreOptIn", out JsonElement optIn) ||
                    optIn.ValueKind != JsonValueKind.True)
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "A fresh explicit confirmation of original City restoration is required.");
                if (payload.TryGetProperty("profileId", out JsonElement route) &&
                    (route.ValueKind != JsonValueKind.String ||
                     string.IsNullOrWhiteSpace(route.GetString()) || route.GetString()!.Length > 128))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Invalid authenticated restoration routing profile.");
                fields["jobId"] = job;
                fields["restoreOptIn"] = "1";
                // The game exchange owns and generates a unique native requestId;
                // browser-supplied UUIDs, coordinates and request IDs are banned.
                break;
            }
            case "automation_start":
            case "automation_stop":
                if (ReadValue(payload, "taskName", 32) != "sheepGame")
                    throw new BridgeCommandException("INVALID_REQUEST", "FEATURE018 only supports the original sheepGame task.");
                fields["taskName"] = "sheepGame";
                break;
            case "feature018_rapid_action":
            {
                // Explicit owned-march rapid workflow. Paid modes admit only
                // the exact native buy item plus explicit consent; Lua still
                // requires the original UILWRapid confirmation dialog and
                // independently verifies the native owner/poststate.
                string mode = ReadValue(payload, "mode", 16);
                if (mode is not ("inventory" or "itemUse" or "status" or
                    "diamondInventory" or "diamondUse" or "diamondStatus"))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid rapid action mode.");
                if (!payload.TryGetProperty("slot", out JsonElement rawSlot) ||
                    !rawSlot.TryGetInt32(out int slot) || slot is < 1 or > 4)
                    throw new BridgeCommandException("INVALID_REQUEST", "Rapid squad slot must be 1–4.");
                fields["mode"] = mode;
                fields["slot"] = slot.ToString(CultureInfo.InvariantCulture);
                if (mode is "itemUse" or "diamondUse")
                {
                    string consentName = mode == "itemUse" ? "itemOptIn" : "diamondOptIn";
                    string oppositeConsentName = mode == "itemUse" ? "diamondOptIn" : "itemOptIn";
                    if (payload.TryGetProperty(oppositeConsentName, out _))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Rapid spending cannot mix item and diamond consent fields.");
                    if (!payload.TryGetProperty(consentName, out JsonElement consent) ||
                        consent.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Rapid spending requires an explicit user confirmation.");
                    string item = ReadValue(payload, "itemId", 20);
                    if (!item.All(char.IsAsciiDigit) || item[0] == '0')
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Rapid item ID must be a positive native item identifier.");
                    fields["itemId"] = item;
                    fields[consentName] = "1";
                }
                else if (payload.TryGetProperty("itemOptIn", out _) ||
                         payload.TryGetProperty("diamondOptIn", out _) ||
                         payload.TryGetProperty("itemId", out _))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Rapid inventory/status must never carry spending arguments.");
                break;
            }
            case "feature018_equipment_preflight":
            {
                // Only a slot ordinal is admitted. Reject even apparently
                // benign extra fields; a forged equipmentPresets, owner UID,
                // item use or stale client snapshot must not enter Lua.
                if (payload.EnumerateObject().Count() != 1 ||
                    !payload.TryGetProperty("slot", out JsonElement requestedSlot) ||
                    requestedSlot.ValueKind != JsonValueKind.Number ||
                    !requestedSlot.TryGetInt32(out int slot) || slot is < 1 or > 4)
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Equipment preflight accepts only the preset slot number 1–4.");
                fields["slot"] = slot.ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "feature018_frontline_action":
            {
                // A deliberate UI tap differs from the already admitted
                // game-owned physical G event. No client-supplied soldier
                // identity, battle ID, UID, or server can enter the producer.
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty field in payload.EnumerateObject())
                {
                    if (field.Name is not ("frontlineOptIn" or "profileId") ||
                        !unique.Add(field.Name))
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Original Frontline G requires unique UI opt-in and routing fields only.");
                }
                if (!payload.TryGetProperty("frontlineOptIn", out JsonElement consent) ||
                    consent.ValueKind != JsonValueKind.True)
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "The owner must explicitly confirm this five-soldier native battle action.");
                if (payload.TryGetProperty("profileId", out JsonElement route) &&
                    (route.ValueKind != JsonValueKind.String ||
                     string.IsNullOrWhiteSpace(route.GetString()) || route.GetString()!.Length > 128))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Invalid Frontline owner routing profile.");
                fields["action"] = "frontlineReinforce";
                fields["slot"] = "5";
                fields["frontlineOptIn"] = "1";
                break;
            }
            case "feature018_hotkey_action":
            {
                string action = ReadValue(payload, "action", 32);
                if (action is not ("attack" or "speedupItem" or "speedupDiamond" or
                    "recall" or "shieldOverlay" or
                    "shieldUse" or "equipment" or "randomRelocate" or "allianceRelocate" or "frontlineReinforce"))
                    throw new BridgeCommandException("INVALID_REQUEST", "Unknown game hotkey action.");
                if (!payload.TryGetProperty("slot", out JsonElement slotValue) ||
                    !slotValue.TryGetInt32(out int slot) || !ValidHotkeySlot(action, slot))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid game hotkey slot.");
                fields["action"] = action;
                fields["slot"] = slot.ToString(CultureInfo.InvariantCulture);
                if (action == "frontlineReinforce" && payload.TryGetProperty("count", out JsonElement countValue))
                {
                    if (!countValue.TryGetInt32(out int reinforce) ||
                        reinforce < HotkeyConfig.MinFrontlineReinforceCount ||
                        reinforce > HotkeyConfig.MaxFrontlineReinforceCount)
                        throw new BridgeCommandException("INVALID_REQUEST", "Invalid soldiers-per-G count.");
                    fields["count"] = reinforce.ToString(CultureInfo.InvariantCulture);
                }
                if (action == "frontlineReinforce" && payload.TryGetProperty("expectedStageRunId", out _))
                {
                    string run = ReadValue(payload, "expectedStageRunId", 20);
                    if (!run.All(char.IsAsciiDigit) || run[0] == '0')
                        throw new BridgeCommandException("INVALID_REQUEST", "Invalid queued stage identity.");
                    fields["expectedStageRunId"] = run;
                }
                if (action == "attack")
                {
                    foreach (string option in new[] { "allowRapidItem", "allowRapidDiamond" })
                        if (payload.TryGetProperty(option, out JsonElement allowed))
                        {
                            if (allowed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                                throw new BridgeCommandException("INVALID_REQUEST", "Attack rapid preference must be boolean.");
                            if (allowed.GetBoolean()) fields[option] = "1";
                        }
                }
                if (action is "attack" or "speedupItem" or "speedupDiamond")
                {
                    // The game-side rapid/attack adapter independently checks
                    // this UID against LuaEntry.Player.uid, the native owner
                    // formation and fresh march. A generic request must not
                    // invent a target or another account UID.
                    string uid = ReadValue(payload, "authorizedUid", 20);
                    if (!uid.All(char.IsAsciiDigit) || uid[0] == '0')
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Attack/rapid action requires authenticated numeric UID.");
                    fields["authorizedUid"] = uid;
                }
                if (action is "speedupItem" or "speedupDiamond")
                {
                    bool onlyStatus = payload.TryGetProperty("queryOnly", out JsonElement statusOnly) &&
                        statusOnly.ValueKind == JsonValueKind.True;
                    if (!onlyStatus)
                    {
                        string consentName = action == "speedupItem" ? "itemOptIn" : "diamondOptIn";
                        if (!payload.TryGetProperty(consentName, out JsonElement itemOptIn) ||
                            itemOptIn.ValueKind != JsonValueKind.True)
                            throw new BridgeCommandException("INVALID_REQUEST",
                                "Owned item-speedup requires explicit opt-in.");
                        string item = ReadValue(payload, "itemId", 20);
                        if (!item.All(char.IsAsciiDigit) || item[0] == '0')
                            throw new BridgeCommandException("INVALID_REQUEST",
                                "Speedup item must be an explicit native item ID.");
                        fields["itemId"] = item;
                        fields[consentName] = "1";
                    }
                }
                if (action == "shieldUse" && payload.TryGetProperty("shieldReplaceOptIn", out JsonElement replacement))
                {
                    if (replacement.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST", "Shield replacement consent must be true.");
                    fields["shieldReplaceOptIn"] = "1";
                }
                // Read-only follow-ups observe only already dispatched,
                // exact-owner native actions. The source-backed Recall
                // and ShieldUse modules have one-in-flight pending fences.
                if (payload.TryGetProperty("queryOnly", out JsonElement query))
                {
                    if (action is not ("attack" or "recall" or "shieldUse" or "speedupItem" or "speedupDiamond" or "equipment" or "randomRelocate" or "allianceRelocate") ||
                        query.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Only native recall, shield-use and owned-item rapid support a true query-only status fence.");
                    fields["queryOnly"] = "1";
                }
                break;
            }
            case "city_layout_snapshot_get":
            case "city_layout_apply_status":
            case "mini_sheep_status":
            case "mini_sheep_board":
            case "mini_frontline_status":
            case "mini_chest_position":
            case "mini_chest_mark":
            case "mini_chest_unmark":
            case "feature018_input_focus":
            case "feature018_sheep_automation_status":
                if (command == "mini_frontline_status" &&
                    (payload.EnumerateObject().Any(entry => entry.Name != "profileId") ||
                     (payload.TryGetProperty("profileId", out JsonElement frontlineProfile) &&
                      (frontlineProfile.ValueKind != JsonValueKind.String ||
                       string.IsNullOrWhiteSpace(frontlineProfile.GetString()) ||
                       frontlineProfile.GetString()!.Length > 128))))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Native Frontline status is read-only and accepts routing profile only.");
                break;
            case "feature018_shield_inventory_get":
                // Exact native owner, duration and inventory status only.
                // No client UID, shield hours, item UUID or consent fields
                // may enter this read. ShieldUse remains a separate original
                // F6/F7/F8 action with its own server/poststate fence.
                // The profile-scoped UI adapter adds profileId as routing
                // metadata. The command exchange uses its own independently
                // verified session/UID from the runtime; never forward or
                // trust this caller-provided routing identifier as authority.
                if (payload.EnumerateObject().Any(p => p.Name != "profileId") ||
                    (payload.TryGetProperty("profileId", out var profile) &&
                     (profile.ValueKind != JsonValueKind.String ||
                      string.IsNullOrWhiteSpace(profile.GetString()) ||
                      profile.GetString()!.Length > 128)))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Shield inventory inspection accepts no spend arguments.");
                break;
            case "monopoly_cell_open":
            case "mini_land_status":
                // An accepted UnlockUserLand packet is not a successful
                // land transition. The native producer can inspect the exact
                // previously targeted cell, even after the current-next cell
                // changes. No second UnlockUserLand is ever sent by polling.
                if (payload.EnumerateObject().Count() > 2 ||
                    payload.EnumerateObject().Any(property =>
                        property.Name is not ("expectedCellId" or "profileId")) ||
                    (payload.TryGetProperty("profileId", out var landProfile) &&
                     (landProfile.ValueKind != JsonValueKind.String ||
                      string.IsNullOrWhiteSpace(landProfile.GetString()) ||
                      landProfile.GetString()!.Length > 128)))
                    throw new BridgeCommandException("INVALID_REQUEST",
                        "Land operation accepts only its routing profile and native target cell ID.");
                if (payload.TryGetProperty("expectedCellId", out JsonElement cell))
                {
                    if (cell.ValueKind != JsonValueKind.Number ||
                        !cell.TryGetInt32(out int cellId) || cellId is < 1 or > 10000)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Expected land cell ID must be 1–10000.");
                    fields["expectedCellId"] = cellId.ToString(CultureInfo.InvariantCulture);
                }
                break;
        }
        return fields;
    }

    /// <summary>
    /// Read the owner profile's latest COMMITTED equipment config on every
    /// invocation, never from a supplied UI JSON blob. The complete saved
    /// ordered preset list is bounded to the native mailbox's safe JSON size.
    /// No equipment apply/spend field can be emitted from this method.
    /// </summary>
    internal async Task<Dictionary<string, string>> ReadConfirmedEquipmentPreflightFieldsAsync(
        JsonElement payload, CancellationToken cancellationToken)
    {
        Dictionary<string, string> fields =
            ReadFields("feature018_equipment_preflight", payload);
        if (equipmentConfig is null)
            throw new BridgeCommandException("GAME_METHOD_UNAVAILABLE",
                "Owner-scoped persisted equipment config is unavailable.");
        using JsonDocument empty = JsonDocument.Parse("{}");
        object? saved = await equipmentConfig.InvokeAsync(
            "equipment_config_get", empty.RootElement, cancellationToken)
            .ConfigureAwait(false);
        if (saved is not JsonObject root ||
            root["equipmentPresets"] is not JsonArray presets ||
            presets.Count is < 1 or > 4)
            throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                "No confirmed owner equipment preset is saved.");

        // Pre-check size before walking potentially arbitrary nested JSON
        // written by older desktop versions. There is no partial truncation.
        string json = presets.ToJsonString();
        if (json.Length > EquipmentPresetWireLimit ||
            json.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                "Confirmed equipment presets exceed the bounded native preflight schema.");

        var distinctIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < presets.Count; i++)
        {
            if (presets[i] is not JsonObject preset ||
                preset["id"] is not JsonValue idNode ||
                !idNode.TryGetValue<string>(out string? id) ||
                string.IsNullOrWhiteSpace(id) || id.Length > 128 ||
                !distinctIds.Add(id) ||
                preset["name"] is not JsonValue nameNode ||
                !nameNode.TryGetValue<string>(out string? name) ||
                string.IsNullOrWhiteSpace(name))
                throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                    "Confirmed owner equipment preset identities are invalid.");
        }
        int presetIndex = int.Parse(fields["slot"], CultureInfo.InvariantCulture) - 1;
        if (presetIndex >= presets.Count)
            throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                "Requested equipment slot has no saved owner preset.");
        JsonObject selected = (JsonObject)presets[presetIndex]!;
        string presetId = selected["id"]!.GetValue<string>();
        if (selected["squads"] is not JsonArray squads || squads.Count is < 1 or > 4)
            throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                "Selected owner equipment preset has no supported squad loadouts.");
        var usedSquads = new HashSet<int>();
        var usedEquipment = new HashSet<string>(StringComparer.Ordinal);
        int requestedPositions = 0;
        foreach (JsonNode? rawSquad in squads)
        {
            if (rawSquad is not JsonObject squad ||
                !ReadBoundedIndex(squad, "squadIndex", 4, out int squadIndex) ||
                !usedSquads.Add(squadIndex) ||
                squad["positions"] is not JsonArray positions || positions.Count > 5)
                throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                    "Selected saved equipment squad has invalid formation positions.");
            var usedPositions = new HashSet<int>();
            foreach (JsonNode? rawPosition in positions)
            {
                if (rawPosition is not JsonObject position ||
                    !ReadBoundedIndex(position, "position", 5, out int pos) ||
                    !usedPositions.Add(pos) ||
                    position["equips"] is not JsonArray items || items.Count > 4)
                    throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                        "Selected saved equipment hero position is invalid.");
                requestedPositions++;
                var usedSlots = new HashSet<int>();
                foreach (JsonNode? rawItem in items)
                {
                    if (rawItem is not JsonObject item ||
                        !ReadBoundedIndex(item, "slot", 4, out int itemSlot) ||
                        !usedSlots.Add(itemSlot) ||
                        item["equipUuid"] is not JsonValue uuidNode ||
                        !uuidNode.TryGetValue<string>(out string? uuid) ||
                        !ValidGameUuid(uuid) || !usedEquipment.Add(uuid!))
                        throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                            "Saved equipment UUID/slot is missing, duplicated or malformed.");
                }
            }
        }
        if (requestedPositions == 0)
            throw new BridgeCommandException("GAME_PRECONDITION_FAILED",
                "Saved equipment preset contains no hero positions.");
        fields["confirmedEquipmentPresets"] = json;
        fields["presetId"] = presetId;
        fields["action"] = "equipment";
        return fields;
    }

    private static bool ReadBoundedIndex(JsonObject row, string name,
        int max, out int result)
    {
        result = 0;
        return row[name] is JsonValue value &&
            value.TryGetValue<int>(out result) && result >= 1 && result <= max;
    }

    private static bool ValidGameUuid(string? uuid) =>
        !string.IsNullOrEmpty(uuid) && uuid.Length <= 20 &&
        uuid[0] is >= '1' and <= '9' &&
        uuid.All(char.IsAsciiDigit) &&
        long.TryParse(uuid, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    private static void RequireReadOnlyEquipmentPreflight(
        JsonElement data, IReadOnlyDictionary<string, string> sent)
    {
        if (data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("observation", out JsonElement kind) ||
            kind.ValueKind != JsonValueKind.String ||
            kind.GetString() != "native_HeroEquip_slot_inventory_preflight" ||
            !data.TryGetProperty("readOnly", out JsonElement readOnly) ||
            readOnly.ValueKind != JsonValueKind.True ||
            !data.TryGetProperty("readyToApply", out JsonElement ready) ||
            ready.ValueKind != JsonValueKind.False ||
            !data.TryGetProperty("requestDispatched", out JsonElement sentAction) ||
            sentAction.ValueKind != JsonValueKind.False ||
            !data.TryGetProperty("action", out JsonElement action) ||
            action.ValueKind != JsonValueKind.String ||
            action.GetString() != "equipment" ||
            !data.TryGetProperty("slot", out JsonElement slot) ||
            slot.ValueKind != JsonValueKind.Number ||
            !slot.TryGetInt32(out int actualSlot) ||
            actualSlot.ToString(CultureInfo.InvariantCulture) != sent["slot"] ||
            !data.TryGetProperty("presetId", out JsonElement id) ||
            id.ValueKind != JsonValueKind.String ||
            id.GetString() != sent["presetId"] ||
            !data.TryGetProperty("slots", out JsonElement observedSlots) ||
            observedSlots.ValueKind != JsonValueKind.Array ||
            observedSlots.GetArrayLength() is < 1 or > 80 ||
            data.TryGetProperty("applied", out _) ||
            data.TryGetProperty("appliedCount", out _) ||
            data.TryGetProperty("equipmentApplied", out _) ||
            data.TryGetProperty("accepted", out _))
            throw new BridgeCommandException("GAME_ACTION_UNCONFIRMED",
                "Native equipment response is not an independently read-only inventory preflight.");

        // An inventory observation must never smuggle an applied/committed
        // outcome under an alias. Neither the response nor any per-gear row
        // may contain unrecognized status/counter fields.
        var permitted = new HashSet<string>(StringComparer.Ordinal)
        {
            "observation", "readOnly", "readyToApply", "requestDispatched",
            "action", "slot", "presetId", "slots", "sourceVersion",
            "plannedPositions", "plannedItems", "changedSlots",
            "wornByOtherHero", "uninstallSlots",
        };
        foreach (JsonProperty property in data.EnumerateObject())
            if (!permitted.Contains(property.Name))
                throw new BridgeCommandException("GAME_ACTION_UNCONFIRMED",
                    "Equipment inventory preflight included an unsupported result field.");
        var legalStates = new HashSet<string>(StringComparer.Ordinal)
        {
            "alreadyEmpty", "alreadyEquipped", "installRequired",
            "replaceRequired", "wornByOtherHero", "uninstallRequired",
        };
        foreach (JsonElement row in observedSlots.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("squadIndex", out JsonElement squadIndex) ||
                squadIndex.ValueKind != JsonValueKind.Number ||
                !squadIndex.TryGetInt32(out int squad) || squad is < 1 or > 4 ||
                !row.TryGetProperty("position", out JsonElement position) ||
                position.ValueKind != JsonValueKind.Number ||
                !position.TryGetInt32(out int pos) || pos is < 1 or > 5 ||
                !row.TryGetProperty("slot", out JsonElement nativeSlot) ||
                nativeSlot.ValueKind != JsonValueKind.Number ||
                !nativeSlot.TryGetInt32(out int gearSlot) || gearSlot is < 1 or > 4 ||
                !row.TryGetProperty("heroUuid", out JsonElement heroUuid) ||
                heroUuid.ValueKind != JsonValueKind.String ||
                !ValidGameUuid(heroUuid.GetString()) ||
                !row.TryGetProperty("currentEquipUuid", out JsonElement currentUuid) ||
                currentUuid.ValueKind != JsonValueKind.String ||
                !(currentUuid.GetString() is "" || ValidGameUuid(currentUuid.GetString())) ||
                !row.TryGetProperty("targetEquipUuid", out JsonElement targetUuid) ||
                targetUuid.ValueKind != JsonValueKind.String ||
                !(targetUuid.GetString() is "" || ValidGameUuid(targetUuid.GetString())) ||
                !row.TryGetProperty("state", out JsonElement gearStatus) ||
                gearStatus.ValueKind != JsonValueKind.String ||
                !legalStates.Contains(gearStatus.GetString() ?? ""))
                throw new BridgeCommandException("GAME_ACTION_UNCONFIRMED",
                    "Native equipment slot observation does not meet the read-only gear schema.");
            foreach (JsonProperty property in row.EnumerateObject())
                if (property.Name is not ("squadIndex" or "position" or "slot" or
                    "heroUuid" or "currentEquipUuid" or "targetEquipUuid" or "state"))
                    throw new BridgeCommandException("GAME_ACTION_UNCONFIRMED",
                        "Native gear row included an unexpected applied-result property.");
        }
    }

    private static bool ValidHotkeySlot(string action, int slot) => action switch
    {
        "attack" or "speedupItem" or "speedupDiamond" or "recall" or "equipment" => slot is >= 1 and <= 4,
        "shieldUse" => slot is 8 or 12 or 24,
        "frontlineReinforce" => slot == 5,
        _ => slot == 0,
    };

    private static string ReadValue(JsonElement root, string key, int maxLength)
    {
        if (!root.TryGetProperty(key, out JsonElement item) ||
            item.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            throw new BridgeCommandException("INVALID_REQUEST", "Missing FEATURE018 field: " + key);
        string value = item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.GetRawText();
        if (value.Length is < 1 || value.Length > maxLength)
            throw new BridgeCommandException("INVALID_REQUEST", "Invalid FEATURE018 field length: " + key);
        return value;
    }
}
