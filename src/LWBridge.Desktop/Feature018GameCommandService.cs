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
        "monopoly_cell_open" or "mini_land_status" or "mini_sheep_status" or "mini_sheep_board" or
        "mini_chest_position" or "mini_chest_mark" or "mini_chest_unmark" or
        "automation_start" or "automation_stop" or "feature018_sheep_automation_status" or
        "feature018_input_focus" or
        "feature018_rapid_action" or
        "feature018_equipment_preflight" or
        "feature018_hotkey_action";

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!CanHandle(command))
            throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", "Unsupported FEATURE018 command.");
        Dictionary<string, string> fields = ReadFields(command, payload);
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
            "city_layout_apply_start" => TimeSpan.FromSeconds(30),
            "city_layout_validate" => TimeSpan.FromSeconds(10),
            "city_layout_snapshot_get" => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(8),
        };
        bool acceptsSubmission = command is "city_layout_apply_start" or "monopoly_cell_open" or
            "automation_start" or "automation_stop" or "feature018_hotkey_action" or
            "feature018_rapid_action";
        return await exchange.ExchangeAsync(command, fields, timeout, cancellationToken,
            allowAcceptedDispatch: acceptsSubmission).ConfigureAwait(false);
    }

    internal static Dictionary<string, string> ReadFields(string command, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new BridgeCommandException("INVALID_REQUEST", "FEATURE018 game payload must be an object.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (command)
        {
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
            {
                string job = ReadValue(payload, "jobId", 128);
                if (!job.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid game apply job token.");
                fields["jobId"] = job;
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
                // Explicit owned-march rapid workflow. Unlike Q/W/E/R this
                // command never invents a cursor target or spends diamonds.
                // The game bridge resolves its own authenticated UID and
                // current attack-march/owned-item state, never a UI-supplied UID.
                string mode = ReadValue(payload, "mode", 16);
                if (mode is not ("inventory" or "itemUse" or "status"))
                    throw new BridgeCommandException("INVALID_REQUEST", "Invalid rapid action mode.");
                if (!payload.TryGetProperty("slot", out JsonElement rawSlot) ||
                    !rawSlot.TryGetInt32(out int slot) || slot is < 1 or > 4)
                    throw new BridgeCommandException("INVALID_REQUEST", "Rapid squad slot must be 1–4.");
                fields["mode"] = mode;
                fields["slot"] = slot.ToString(CultureInfo.InvariantCulture);
                if (mode == "itemUse")
                {
                    if (!payload.TryGetProperty("itemOptIn", out JsonElement consent) ||
                        consent.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Owned speedup item use requires an explicit user confirmation.");
                    string item = ReadValue(payload, "itemId", 20);
                    if (!item.All(char.IsAsciiDigit) || item[0] == '0')
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Rapid item ID must be a positive native item identifier.");
                    fields["itemId"] = item;
                    fields["itemOptIn"] = "1";
                }
                else if (payload.TryGetProperty("itemOptIn", out _) ||
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
                if (action == "speedupItem")
                {
                    bool onlyStatus = payload.TryGetProperty("queryOnly", out JsonElement statusOnly) &&
                        statusOnly.ValueKind == JsonValueKind.True;
                    if (!onlyStatus)
                    {
                        if (!payload.TryGetProperty("itemOptIn", out JsonElement itemOptIn) ||
                            itemOptIn.ValueKind != JsonValueKind.True)
                            throw new BridgeCommandException("INVALID_REQUEST",
                                "Owned item-speedup requires explicit opt-in.");
                        string item = ReadValue(payload, "itemId", 20);
                        if (!item.All(char.IsAsciiDigit) || item[0] == '0')
                            throw new BridgeCommandException("INVALID_REQUEST",
                                "Speedup item must be an explicit native item ID.");
                        fields["itemId"] = item;
                        fields["itemOptIn"] = "1";
                    }
                }
                // Read-only follow-ups observe only already dispatched,
                // exact-owner native actions. The source-backed Recall
                // and ShieldUse modules have one-in-flight pending fences.
                if (payload.TryGetProperty("queryOnly", out JsonElement query))
                {
                    if (action is not ("recall" or "shieldUse" or "speedupItem") ||
                        query.ValueKind != JsonValueKind.True)
                        throw new BridgeCommandException("INVALID_REQUEST",
                            "Only native recall, shield-use and owned-item rapid support a true query-only status fence.");
                    fields["queryOnly"] = "1";
                }
                break;
            }
            case "city_layout_snapshot_get":
            case "city_layout_apply_status":
            case "monopoly_cell_open":
            case "mini_land_status":
            case "mini_sheep_status":
            case "mini_sheep_board":
            case "mini_chest_position":
            case "mini_chest_mark":
            case "mini_chest_unmark":
            case "feature018_input_focus":
            case "feature018_sheep_automation_status":
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
