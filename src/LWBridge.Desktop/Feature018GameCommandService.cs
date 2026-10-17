using System.Globalization;
using System.Text.Json;

namespace LWBridge.Desktop;

/// <summary>
/// Admission and exact 0.3.17 front-end schemas for the FEATURE-018 game
/// module. Client-class action semantics belong to source-verified Lua
/// handlers, not to these serialized requests.
/// </summary>
internal sealed class Feature018GameCommandService : INativeAsyncCommandService
{
    private readonly Feature018OwnedGameExchange exchange;
    internal Feature018GameCommandService(OverviewLifecycleService lifecycle)
        : this(new Feature018OwnedGameExchange(lifecycle)) { }
    internal Feature018GameCommandService(Feature018OwnedGameExchange exchange) =>
        this.exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));

    public bool CanHandle(string command) => command is
        "city_layout_snapshot_get" or "city_layout_validate" or "city_layout_apply_start" or
        "city_layout_apply_status" or "city_layout_apply_cancel" or
        "monopoly_cell_open" or "mini_land_status" or "mini_sheep_status" or "mini_sheep_board" or
        "mini_chest_position" or "mini_chest_mark" or "mini_chest_unmark" or
        "automation_start" or "automation_stop" or "feature018_sheep_automation_status" or
        "feature018_input_focus" or
        "feature018_hotkey_action";

    public async Task<object?> InvokeAsync(string command, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!CanHandle(command))
            throw new BridgeCommandException("COMMAND_NOT_IMPLEMENTED", "Unsupported FEATURE018 command.");
        Dictionary<string, string> fields = ReadFields(command, payload);
        TimeSpan timeout = command switch
        {
            "city_layout_apply_start" => TimeSpan.FromSeconds(30),
            "city_layout_validate" => TimeSpan.FromSeconds(10),
            "city_layout_snapshot_get" => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(8),
        };
        bool acceptsSubmission = command is "city_layout_apply_start" or "monopoly_cell_open" or
            "automation_start" or "automation_stop" or "feature018_hotkey_action";
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
