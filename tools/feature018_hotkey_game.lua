-- FEATURE-RECOVERY-018: native game-window Hotkeys action adapter.
--
-- Source-verified read-only shield-status path in ORIGINAL installed v26 Lua:
-- DataCenter/ItemManager/StatusManager.luac SHA-256
-- 8546a11d87b66719c3f05917312d353119701cd78bbe55b2b87123df3722631c
--   GetShieldStatusIds (lines 26-36): iterates StatusTab and retains IDs whose
--      effect == EffectDefine.MAIN_SHIELD, indexed [statusId] = true.
--   GetAllStatusItem (lines 38-48): LuaEntry.Effect:GetStatusMap().
--   GetBuffTimeInfo (lines 154-165): {endTime,totalTime,intKey} for active buff.
--   CheckHasStatusByType2 (lines 374-388): UITimeManager:GetInstance()
--      :GetServerTime() and active status end times share the same time base.
-- Source-verified shield-item request (NOT enabled until duration/eligibility
-- is proven): UI/UILWCityShield/Component/CityShieldPage.luac ConfirmUse
-- lines 243-249 sends SFSNetwork.SendMessage(MsgDefines.ItemUse,
-- {uuid=item.uuid,num=1}); BuyOrUse lines 202-240 performs additional
-- meteorite-warning, inventory and diamond-purchase checks.
-- Offline v26 3/26 original-entry SHA
-- 50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137:
-- CityShieldPage.CellsCallBack 153-200 resolves each template.para1 through
-- StatusTab and explicitly compares status.time * 1000 with shield remaining;
-- its confirmation formats status.time / 3600 hours. The adapter supports
-- owned duration-matched items only when every UI warning/confirmation gate
-- can be satisfied without a user dialog or paid purchase.
--
-- Only Feature018GameBridge.Pump may invoke this registered module after the
-- exact game PID/profile/session/challenge/lease admission. Never register a
-- global keyboard hook, modify a game file or claim a network send succeeded.

local M = { VERSION = "feature018-hotkey-game-1" }
local mark_array = nil
local pending_shield = nil
local SHIELD_PENDING_SECONDS = 45

local function member(value, key)
    if value == nil then return nil end
    local ok, item = pcall(function() return value[key] end)
    if ok then return item end
    return nil
end

local function invoke(value, key, ...)
    local method = member(value, key)
    if type(method) ~= "function" then return false, nil end
    local ok, result = pcall(method, value, ...)
    -- A native eligibility gate can legitimately return false. Converting
    -- that value to nil would make safe, negative checks unverifiable.
    return ok, result
end

local function unavailable(reason)
    return {state = "unsupported", errorCode = "GAME_METHOD_UNAVAILABLE",
            error = reason}
end

local function invalid(reason)
    return {state = "failed", errorCode = "GAME_PRECONDITION_FAILED",
            error = reason}
end

local function number(value)
    local result = tonumber(value)
    if result == nil or result ~= result or
       result == math.huge or result == -math.huge then return nil end
    return result
end

local function shield_snapshot()
    local center = rawget(_G, "DataCenter")
    local status = member(center, "StatusManager")
    if status == nil then return unavailable("Current-game StatusManager unavailable") end
    local ok_ids, shield_ids = invoke(status, "GetShieldStatusIds")
    local ok_map, all_statuses = invoke(status, "GetAllStatusItem")
    if not ok_ids or type(shield_ids) ~= "table" or
       not ok_map or type(all_statuses) ~= "table" then
        return unavailable("Native shield status IDs or authoritative Effect status map unavailable")
    end
    local time_type = rawget(_G, "UITimeManager")
    local ok_clock, clock = invoke(time_type, "GetInstance")
    local ok_time, server_time = invoke(clock, "GetServerTime")
    local now = number(server_time)
    if not ok_clock or not ok_time or now == nil or now < 0 then
        return unavailable("Native game server clock unavailable")
    end

    local count_ids = 0
    local statuses = {}
    for key, is_shield in pairs(shield_ids) do
        if is_shield == true then
            local id = number(key)
            if id ~= nil and id > 0 and id % 1 == 0 then
                count_ids = count_ids + 1
                local end_value = member(all_statuses, key)
                if end_value == nil then end_value = member(all_statuses, id) end
                if end_value == nil then end_value = member(all_statuses, tostring(id)) end
                local ended = number(end_value)
                if ended ~= nil and ended > now then
                    local ok_info, info = invoke(status, "GetBuffTimeInfo", id)
                    local total = ok_info and number(member(info, "totalTime")) or nil
                    local end_time = ok_info and number(member(info, "endTime")) or nil
                    -- The independent StatusManager's own lookup must agree
                    -- with the native Effect status map before a time is shown.
                    if end_time ~= nil and end_time == ended and
                       total ~= nil and total >= 0 then
                        statuses[#statuses + 1] = {
                            statusId = id,
                            endTime = ended,
                            totalTime = total,
                            remainingMilliseconds = ended - now,
                        }
                    else
                        return unavailable("Active shield status could not be independently checked")
                    end
                end
            end
        end
    end
    if count_ids == 0 then
        return unavailable("Current game has no source-classified MAIN_SHIELD status IDs")
    end
    if #statuses > 1 then
        table.sort(statuses, function(a, b) return a.endTime < b.endTime end)
    end
    -- FEATURE018's JSON encoder treats an empty Lua table as an object.
    -- A shield-free game must still return statuses=[] to the frontend.
    if type(mark_array) == "function" then
        statuses = mark_array(statuses)
    end
    local result = {
        active = #statuses > 0,
        shieldStatusCount = #statuses,
        serverTimeMilliseconds = now,
        statuses = statuses,
        observation = "native_MAIN_SHIELD_effect_status_map",
    }
    if #statuses == 0 then
        result.remainingMilliseconds = 0
    elseif #statuses == 1 then
        result.remainingMilliseconds = statuses[1].remainingMilliseconds
        result.endTime = statuses[1].endTime
    end
    return {state = "confirmed", data = result}
end

function M.ShieldStatusSnapshot()
    return shield_snapshot()
end

-- Native game-side input focus gate for the window-only key sampler.
-- The v26 `Framework/UI/Component/UIInput.luac` (SHA-256
-- ed42a0d5ca46d2712d67a078a7f8fb597bbc36b00acd82366ec9ef4fbd15bbce)
-- OnCreate lines 19-29 and Select lines 184-192 prove this game uses
-- UnityEngine.UI.InputField, TMPro.TMP_InputField and TMP_InputFieldEx to edit
-- text, with Select()/ActivateInputField(). A Win32 HWND focus check alone
-- cannot distinguish these objects, which share the main Unity game HWND.
--
-- This is a pure game-thread observation, NOT an assertion that the desktop
-- has a fresh authenticated input-focus heartbeat. The caller must bind a
-- periodic result to the current game owner and must fail closed if stale.
function M.TextInputClear()
    local cs = rawget(_G, "CS")
    local unity = member(cs, "UnityEngine")
    local ui = member(unity, "UI")
    local events = member(unity, "EventSystems")
    local tmpro = member(cs, "TMPro")
    local event_type = member(events, "EventSystem")
    local field_type = member(ui, "InputField")
    local tmp_type = member(tmpro, "TMP_InputField")
    local extra_type = member(tmpro, "TMP_InputFieldEx")
    local type_of = rawget(_G, "typeof")
    local is_null = rawget(_G, "IsNull")
    if event_type == nil or field_type == nil or tmp_type == nil or
       extra_type == nil or type(type_of) ~= "function" or
       type(is_null) ~= "function" then
        return false, "game_input_focus_types_unavailable"
    end

    local current = member(event_type, "current")
    if current == nil then return false, "unity_event_system_unavailable" end
    local ok_selected, selected = pcall(function()
        return current.currentSelectedGameObject
    end)
    if not ok_selected then return false, "game_selected_object_unreadable" end
    if selected == nil or is_null(selected) then return true, "no_selected_text_input" end

    -- A focused child of an input component is still an edit target. Query
    -- the selected object's ancestors as well, never its siblings or scene.
    local get_component = member(selected, "GetComponentInParent")
    if type(get_component) ~= "function" then
        return false, "game_selection_component_reader_unavailable"
    end
    for _, native_type in ipairs({field_type, tmp_type, extra_type}) do
        local ok_kind, component_type = pcall(type_of, native_type)
        if not ok_kind or component_type == nil then
            return false, "game_input_component_type_unavailable"
        end
        local ok_field, field = pcall(get_component, selected, component_type)
        if not ok_field then return false, "game_input_component_lookup_failed" end
        if field ~= nil and not is_null(field) then
            return false, "native_game_text_input_selected"
        end
    end
    return true, "native_selected_object_has_no_text_input_component"
end

-- Called only through the authenticated game-bridge request/response gate.
-- The bridge itself stamps the request identity and in-game observation time
-- into a correlated envelope; the desktop must also bound receipt freshness.
function M.InputFocusSnapshot()
    local clear, reason = M.TextInputClear()
    if reason == "native_game_text_input_selected" then
        return {state = "confirmed", data = {
            inputClear = false, observation = "native_Unity_UI_input_focus",
            focusReason = reason,
        }}
    end
    if not clear then return unavailable("Native game text-input focus cannot be verified: " .. reason) end
    return {state = "confirmed", data = {
        inputClear = true, observation = "native_Unity_UI_input_focus",
        focusReason = reason,
    }}
end

local function digit_id(value)
    if value == nil or type(value) == "boolean" then return nil end
    local str = tostring(value)
    if #str < 1 or #str > 20 or str == "0" or
       str:find("[^0-9]") then return nil end
    return str
end

local function shield_owner(fields)
    local tokens = {}
    for _, key in ipairs({"profileId", "launchSessionId", "challenge", "gamePid"}) do
        local value = fields[key]
        if type(value) ~= "string" or #value == 0 or #value > 128 or
           value:find("[^%w_-]") then return nil end
        tokens[#tokens + 1] = value
    end
    return table.concat(tokens, "/")
end

local function owned_item_count(uuid, item_id)
    local center = rawget(_G, "DataCenter")
    local items = member(center, "ItemData")
    local ok, list = invoke(items, "GetItemList", 4, 1)
    if not ok or type(list) ~= "table" then return nil end
    local count = 0
    for _, item in pairs(list) do
        if digit_id(member(item, "uuid")) == uuid then
            if digit_id(member(item, "itemId")) ~= item_id then return nil end
            local available = number(member(item, "count"))
            if available == nil or available < 0 or available % 1 ~= 0 then
                return nil
            end
            count = count + available
        end
    end
    return count
end

local function shield_item_for_hours(hours)
    local center = rawget(_G, "DataCenter")
    local item_data = member(center, "ItemData")
    local templates = member(center, "ItemTemplateManager")
    local status = member(center, "StatusManager")
    local controller = rawget(_G, "LocalController")
    local tables = rawget(_G, "TableName")
    local status_table = member(tables, "StatusTab")
    local ok_owned, owned = invoke(item_data, "GetItemList", 4, 1)
    local ok_templates, available = invoke(templates, "GetTypeListByTypes", 4, 1)
    local ok_ids, ids = invoke(status, "GetShieldStatusIds")
    local ok_controller, local_instance = invoke(controller, "instance")
    if not ok_owned or type(owned) ~= "table" or
       not ok_templates or type(available) ~= "table" or
       not ok_ids or type(ids) ~= "table" or
       not ok_controller or local_instance == nil or status_table == nil then
        return nil, unavailable("Native owned items, status IDs, or Goods/Status templates unavailable")
    end
    local chosen = nil
    for _, template in pairs(available) do
        local status_id = digit_id(member(template, "para1"))
        local template_id = digit_id(member(template, "id"))
        if template_id ~= nil and status_id ~= nil and
           (ids[tonumber(status_id)] == true or ids[status_id] == true) then
            local ok_row, row = invoke(local_instance, "getLine",
                status_table, member(template, "para1"))
            if not ok_row or row == nil then
                return nil, unavailable("Native StatusTab shield duration is unreadable")
            end
            local seconds = number(member(row, "time"))
            if seconds == nil or seconds <= 0 or seconds % 1 ~= 0 then
                return nil, unavailable("Native StatusTab shield duration is invalid")
            end
            if seconds == hours * 3600 then
                for _, item in pairs(owned) do
                    local count = number(member(item, "count"))
                    local uuid = digit_id(member(item, "uuid"))
                    if digit_id(member(item, "itemId")) == template_id and
                       uuid ~= nil and count ~= nil and count >= 1 and count % 1 == 0 then
                        if chosen ~= nil then
                            return nil, unavailable("Multiple owned stacks map to this shield duration")
                        end
                        chosen = {uuid=uuid, itemId=template_id,
                            statusId=tonumber(status_id), count=count, durationMs=seconds*1000}
                    end
                end
            end
        end
    end
    if chosen == nil then
        return nil, unavailable("No uniquely owned 8/12/24h shield item with source-matched StatusTab duration")
    end
    return chosen
end

local function shield_safe_to_consume()
    local center = rawget(_G, "DataCenter")
    local status = member(center, "StatusManager")
    local ok_cd, cooling = invoke(status, "IsUseShieldCD")
    local ok_war, war_fever = invoke(status, "WarFeverStatu")
    if not ok_cd or type(cooling) ~= "boolean" or not ok_war then
        return unavailable("Native shield cooldown/war-fever eligibility cannot be verified")
    end
    if cooling then return invalid("Game shield-use cooldown remains active") end
    -- CityShieldPage.CellsCallBack 159–161 explicitly warns and returns when
    -- WarFeverStatu is NON-nil; nil is the game's safe state.
    if war_fever ~= nil then
        return invalid("Native war-fever shield warning blocks ItemUse")
    end
    local season = rawget(_G, "SeasonUtil")
    local season_reader = member(season, "IsInSeason")
    if type(season_reader) ~= "function" then
        return unavailable("Current season source reader unavailable")
    end
    -- The original CityShieldPage invokes SeasonUtil.IsInSeason() statically.
    -- Calling with an injected receiver can make a native C# static delegate
    -- fail even if an inert Lua table ignores the extra argument.
    local ok_season, seasonal = pcall(season_reader)
    if not ok_season or type(seasonal) ~= "boolean" then
        return unavailable("Current season and alliance-field shield eligibility unavailable")
    end
    -- The current UI needs seasonal alliance-field, danger-area and missile
    -- checks (CellsCallBack 163-181). Do not infer these from city coordinates.
    if seasonal then return invalid("Native seasonal shield-placement checks require game UI") end
    local wall = member(center, "DefenceWallDataManager")
    local ok_wall, defence = invoke(wall, "GetDefenceWallData")
    local clock_type = rawget(_G, "UITimeManager")
    local ok_clock, clock = invoke(clock_type, "GetInstance")
    local ok_time, current_time = invoke(clock, "GetServerTime")
    local now = number(current_time)
    local ends = number(member(defence, "protectEndTime"))
    if not ok_wall or not ok_clock or not ok_time or now == nil or ends == nil then
        return unavailable("Current native wall protection or game server time unavailable")
    end
    if ends > now then
        return invalid("Active shield replacement requires native user confirmation")
    end
    local meteorite = member(center, "ActMeteoriteBattleManager")
    local ok_notice, warn = invoke(meteorite, "NeedNoticeShield")
    if not ok_notice or type(warn) ~= "boolean" then
        return unavailable("Native meteorite shield warning eligibility unavailable")
    end
    if warn then return invalid("Native meteorite-drop warning requires user confirmation") end
    return nil
end

local function shield_use(fields)
    local owner = shield_owner(fields)
    local hours = tonumber(fields.slot)
    if owner == nil then
        return unavailable("Exact authenticated shield session identity unavailable")
    end
    if not (hours == 8 or hours == 12 or hours == 24) then
        return invalid("Shield duration must be 8, 12 or 24 hours")
    end
    if pending_shield ~= nil and pending_shield.owner ~= owner then
        pending_shield = nil
    end
    if fields.queryOnly ~= nil and fields.queryOnly ~= "1" then
        return invalid("Only an exact read-only shield query is accepted")
    end
    local pending = pending_shield
    if fields.queryOnly == "1" then
        if pending == nil or pending.hours ~= hours then
            return invalid("No matching sent shield to verify")
        end
        if os.time() - pending.sentAt > SHIELD_PENDING_SECONDS then
            return invalid("Shield ItemUse remains unconfirmed; manual state refresh required")
        end
        local snapshot = shield_snapshot()
        if snapshot.state ~= "confirmed" then return snapshot end
        local count = owned_item_count(pending.uuid, pending.itemId)
        if count == nil then return unavailable("Owned shield count after ItemUse is unreadable") end
        local match = false
        for _, effect in pairs(snapshot.data.statuses) do
            if effect.statusId == pending.statusId and
               effect.remainingMilliseconds > 0 and
               effect.remainingMilliseconds <= pending.durationMs + 15000 and
               effect.remainingMilliseconds >= pending.durationMs - 120000 and
               snapshot.data.serverTimeMilliseconds >= pending.beforeClock then
                match = true
            end
        end
        if count == pending.count - 1 and match and
           snapshot.data.shieldStatusCount == 1 then
            pending_shield = nil
            return {state="confirmed",data={
                observation="native_owned_shield_consumption_and_MAIN_SHIELD",
                slot=hours, itemUuid=pending.uuid, itemId=pending.itemId,
                statusId=pending.statusId, durationMilliseconds=pending.durationMs,
                consumedCount=1, remainingMilliseconds=snapshot.data.remainingMilliseconds,
                serverTimeMilliseconds=snapshot.data.serverTimeMilliseconds
            }}
        end
        return {state="accepted",data={requestDispatched=false,
            awaitingGameConfirmation=true,slot=hours,itemUuid=pending.uuid}}
    end
    if pending ~= nil then
        return invalid("Prior ItemUse is unconfirmed; never consume again in this session")
    end
    local safety = shield_safe_to_consume()
    if safety ~= nil then return safety end
    local before = shield_snapshot()
    if before.state ~= "confirmed" then return before end
    if before.data.active then
        return invalid("Active MAIN_SHIELD requires native replacement confirmation")
    end
    local item, reason = shield_item_for_hours(hours)
    if item == nil then return reason end
    local count = owned_item_count(item.uuid, item.itemId)
    if count == nil or count ~= item.count then
        return invalid("Owned shield stack changed during native eligibility check")
    end
    -- The original ConfirmUse sends ItemUse only after user-facing warning
    -- routes have been passed. It sets the five-second cooldown before send.
    local network = rawget(_G, "SFSNetwork")
    local message = member(rawget(_G, "MsgDefines"), "ItemUse")
    local status = member(rawget(_G, "DataCenter"), "StatusManager")
    if type(member(network, "SendMessage")) ~= "function" or message == nil or
       type(member(status, "SetUseShieldCD")) ~= "function" then
        return unavailable("Native shield ItemUse sender/cooldown method unavailable")
    end
    -- Re-evaluate volatile warning and cooldown immediately before sending.
    local final_safety = shield_safe_to_consume()
    if final_safety ~= nil then return final_safety end
    local ok_cd = invoke(status, "SetUseShieldCD")
    if not ok_cd then return invalid("Native shield cooldown could not be set") end
    local ok_send = pcall(member(network, "SendMessage"), message,
        {uuid=item.uuid, num=1})
    if not ok_send then return invalid("Native ItemUse sender failed") end
    pending_shield = {
        owner=owner, hours=hours, uuid=item.uuid, itemId=item.itemId,
        statusId=item.statusId, durationMs=item.durationMs, count=item.count,
        beforeClock=before.data.serverTimeMilliseconds, sentAt=os.time()
    }
    return {state="accepted",data={
        requestDispatched=true,awaitingGameConfirmation=true,
        slot=hours,itemUuid=item.uuid,itemId=item.itemId
    }}
end

local slots = {
    attack = {"1", "2", "3", "4"},
    recall = {"1", "2", "3", "4"},
    equipment = {"1", "2", "3", "4"},
    shieldUse = {"8", "12", "24"},
    frontlineReinforce = {"5"},
    shieldOverlay = {"0"},
    randomRelocate = {"0"},
    allianceRelocate = {"0"},
}

function M.Handle(fields)
    if type(fields) ~= "table" or type(fields.action) ~= "string" or
       type(fields.slot) ~= "string" or slots[fields.action] == nil then
        return invalid("Invalid feature018 hotkey kind or slot")
    end
    local valid = false
    for _, slot in ipairs(slots[fields.action]) do
        if fields.slot == slot then valid = true; break end
    end
    if not valid then return invalid("Hotkey slot is not bound to this game action") end
    -- Always revalidate game-side text focus when dispatch happens, even if
    -- the WinForms sampler saw a fresh earlier input-clear observation.
    if not (fields.action == "shieldUse" and fields.queryOnly == "1") then
        local clear, reason = M.TextInputClear()
        if not clear then
            return invalid("Game text input is active or unverifiable: " .. reason)
        end
    end
    if fields.action == "shieldOverlay" then
        return shield_snapshot()
    end
    if fields.action == "shieldUse" then
        return shield_use(fields)
    end
    if fields.action == "equipment" then
        -- Original Alt+1–4 references per-profile saved *equipment* loadouts.
        -- The v26 PVPFormationPresetManager.SendPresetApplyMsg uses game
        -- formation preset UUIDs and is not an equivalent equipment-slot
        -- mapping. A source-backed gear-owner/slot-ACK producer is required.
        return unavailable("Saved equipment preset cannot be mapped to a native v26 equipped-gear apply action")
    end
    return unavailable("Current-client gameplay operation has no proven native action producer: " .. fields.action)
end

function M.Register(feature)
    if type(feature) ~= "table" or type(feature.RegisterHandler) ~= "function" then return false end
    local actions = feature.RegisterHandler("feature018_hotkey_action", M.Handle)
    local input = feature.RegisterHandler("feature018_input_focus", function(_)
        return M.InputFocusSnapshot()
    end)
    if actions ~= true or input ~= true then return false end
    mark_array = type(feature.Array) == "function" and feature.Array or nil
    return true
end

return M
