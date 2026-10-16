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
--
-- Only Feature018GameBridge.Pump may invoke this registered module after the
-- exact game PID/profile/session/challenge/lease admission. Never register a
-- global keyboard hook, modify a game file or claim a network send succeeded.

local M = { VERSION = "feature018-hotkey-game-1" }

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
    return ok, ok and result or nil
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
    local clear, reason = M.TextInputClear()
    if not clear then
        return invalid("Game text input is active or unverifiable: " .. reason)
    end
    if fields.action == "shieldOverlay" then
        return shield_snapshot()
    end
    if fields.action == "shieldUse" then
        -- Shield duration-to-owned UUID mapping and meteorite confirmation
        -- requirements are not established for this exact installed build.
        -- Sending MsgDefines.ItemUse without both would use an arbitrary item
        -- or skip game UI preconditions; report the real limitation.
        return unavailable("Shield duration item and warning checks are not source-verified")
    end
    return unavailable("Current-client gameplay operation has no proven native action producer: " .. fields.action)
end

function M.Register(feature)
    if type(feature) ~= "table" or type(feature.RegisterHandler) ~= "function" then return false end
    local actions = feature.RegisterHandler("feature018_hotkey_action", M.Handle)
    local input = feature.RegisterHandler("feature018_input_focus", function(_)
        return M.InputFocusSnapshot()
    end)
    return actions == true and input == true
end

return M
