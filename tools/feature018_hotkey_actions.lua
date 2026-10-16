-- FEATURE-RECOVERY-018 isolated CURRENT v26 Hotkeys actions.
-- Installed/original evidence (read-only Lua bytecode; do not edit game files):
-- Util/MarchUtil.luac SHA d3c3bb47b11e555ccd646510628c2e428000436ffa8bc8bac290a1320094e245
--   OnBackHome root/19 lines 697-715: resolve world march by UUID and pass
--   MarchTargetType.BACK_HOME to StartMarch with that UUID.
--   SendChangeMarchToServer root/65 lines 2016-2065: posts WorldMarchChange.
-- Scene/WorldMarchTileUI/WorldMarchTileUIManager.luac SHA
--   c580c2533f842bd6c1b5b093ccd6b1a51952ae4d44741de8b9d6e1e16e0587dc
--   OnBtnClick root/11 lines 277-293: native March_Callback UI route,
--   gated by DataConfig.CheckSwitch('slide_left_return') or confirmation UI.
-- Net/Msgs/WorldMarchChangeMessage.luac SHA
--   71453f2d7287ddb1f012e05cea71755f6bc704971a59425b6c54834c96fe8160
--   HandleMessage root/1 lines 64-92: after error-free server response,
--   SceneManager.World:HandleFormationMarchChange(response).
-- DataCenter/ArmyFormationData/ArmyFormationDataManager.luac SHA
--   0df480a74c338b9830f16b7b455ae0f1a77877a78896f0fedde381c28d7cbce7
--   GetOneArmyInfoByIndex root/17 lines 468-471: current slot -> formation.
-- DataCenter/WorldMarchDataManager/WorldMarchDataProxy.luac SHA
--   7c53a1dfaf141450bb06a8004b7f0fd29791ec614aab3ffca7c026d16dfad55e
--   GetOwnerFormationMarch root/12 lines 63-68; native C# method
--   0x060032A5. WARNING: native GetOwnerMarches 0x0600329F ignores owner
--   filter arguments. This module explicitly rechecks owner and formation.
-- UI/UILWCityShield/Component/CityShieldPage.luac SHA
--   f405905566075affbed4bd2dc26d5ab3323346bd0715539a8699ce18051ed50a
--   Refresh root/5 lines 66-95: GetItemList(4,1)/GetTypeListByTypes(4,1).
--   BuyOrUse root/11 lines 202-240: meteorite warning and diamond fallback.
--   ConfirmUse root/12 lines 243-249: ItemUse {uuid=item.uuid,num=1}.
-- Duration-to-item ID from F6/F7/F8 and post-response effects are NOT proven.
--
-- Nothing runs on load; prime must wrap the frozen Hotkey module's Handle
-- BEFORE calling frozenHotkey.Register(authenticatedFeature018Bridge).

local M = {VERSION = "feature018-hotkey-actions-1"}
local outstanding = {}
local owner_token = nil
local MAX_PENDING_SECONDS = 45

local function member(object, key)
    if object == nil then return nil end
    local ok, value = pcall(function() return object[key] end)
    if ok then return value end
    return nil
end

local function invoke(object, method, ...)
    local fn = member(object, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, result = pcall(fn, object, ...)
    return ok, result
end

local function unavailable(message)
    return {state="unsupported", errorCode="GAME_METHOD_UNAVAILABLE", error=message}
end

local function blocked(message)
    return {state="failed", errorCode="GAME_PRECONDITION_FAILED", error=message}
end

local function id_string(value)
    if value == nil or type(value) == "boolean" then return nil end
    local result = tostring(value)
    if result:match("^%d+$") and #result <= 20 and result ~= "0" then
        return result
    end
    return nil
end

local function current_account()
    local player = member(rawget(_G, "LuaEntry"), "Player")
    return player, id_string(member(player, "uid"))
end

local function same_march(march, uid, formation_id, uuid)
    if march == nil then return false end
    return id_string(member(march, "ownerUid")) == uid
       and id_string(member(march, "ownerFormationUuid")) == formation_id
       and id_string(member(march, "uuid")) == uuid
end

local function resolve_squad(slot)
    local _, uid = current_account()
    local center = rawget(_G, "DataCenter")
    local formations = member(center, "ArmyFormationDataManager")
    local marches = member(center, "WorldMarchDataManager")
    if uid == nil or formations == nil or marches == nil then
        return nil, "Native owner/formation/march data unavailable"
    end
    local ok_formation, formation = invoke(formations, "GetOneArmyInfoByIndex", slot)
    local formation_id = id_string(member(formation, "uuid"))
    if not ok_formation or formation_id == nil then
        return nil, "Formation slot is absent or not source-verified"
    end
    -- Return-homing an arbitrary march is unacceptable. Query the native
    -- current user's formation, then independently re-fetch its exact UUID.
    local native_id = tonumber(formation_id)
    if native_id == nil or native_id % 1 ~= 0 then
        return nil, "Formation UUID is not a native integer"
    end
    local ok_lookup, candidate = invoke(marches, "GetOwnerFormationMarch",
        uid, native_id, "")
    local march_uuid = id_string(member(candidate, "uuid"))
    if not ok_lookup or march_uuid == nil or
       not same_march(candidate, uid, formation_id, march_uuid) then
        return nil, "Native owner formation march identity is missing or mismatched"
    end
    local ok_fresh, fresh = invoke(marches, "GetMarch", native_id and tonumber(march_uuid))
    if not ok_fresh or not same_march(fresh, uid, formation_id, march_uuid) then
        return nil, "Fresh authoritative march owner no longer matches slot"
    end
    local ok_target, target = invoke(fresh, "GetMarchTargetType")
    if not ok_target or target == nil then
        return nil, "Authoritative march target type unavailable"
    end
    return {uuid=march_uuid, uuid_number=tonumber(march_uuid),
            uid=uid, formation=formation_id, target=target,
            slot=slot, marches=marches}, nil
end

local function session_id(fields)
    local keys = {"profileId", "launchSessionId", "challenge", "gamePid"}
    local result = {}
    for _, key in ipairs(keys) do
        if type(fields[key]) ~= "string" or fields[key] == "" or
           #fields[key] > 128 or fields[key]:match("[^%w_-]") then
            return nil
        end
        result[#result+1] = fields[key]
    end
    return table.concat(result, "/")
end

local function read_return_state(selected)
    local ok_march, fresh = invoke(selected.marches, "GetMarch", selected.uuid_number)
    if not ok_march or not same_march(fresh,
          selected.uid, selected.formation, selected.uuid) then
        return nil
    end
    local ok_target, target = invoke(fresh, "GetMarchTargetType")
    if not ok_target then return nil end
    return target
end

local function recall(fields, game_input_clear)
    local slot = tonumber(fields.slot)
    if type(fields.slot) ~= "string" or
       slot == nil or slot % 1 ~= 0 or slot < 1 or slot > 4 then
        return blocked("Recall hotkey requires squad slot 1 to 4")
    end
    local identity = session_id(fields)
    if identity == nil then return blocked("Exact authenticated game identity absent") end
    if owner_token ~= identity then
        outstanding = {}
        owner_token = identity
    end
    if type(game_input_clear) ~= "function" or game_input_clear() ~= true then
        return blocked("Current-game text input focus could not be cleared")
    end
    local selected, reason = resolve_squad(slot)
    if selected == nil then return unavailable(reason) end
    local target_enum = member(rawget(_G, "MarchTargetType"), "BACK_HOME")
    if target_enum == nil then return unavailable("Native BACK_HOME target enum unavailable") end
    local pending = outstanding[slot]
    if pending and (pending.uuid ~= selected.uuid or pending.formation ~= selected.formation) then
        return blocked("Squad changed while its earlier return was pending")
    end
    if pending then
        local current = read_return_state(selected)
        if current == nil then return blocked("Pending march state could not be revalidated") end
        if current == target_enum then
            outstanding[slot] = nil
            return {state="confirmed", data={
                observation="native_world_march_return_target", slot=slot,
                marchUuid=selected.uuid, formationUuid=selected.formation,
                target="BACK_HOME", returning=true
            }}
        end
        if os.time() - pending.sentAt >= MAX_PENDING_SECONDS then
            return blocked("Earlier return has no authoritative game update; refresh session before retry")
        end
        return {state="accepted",data={requestDispatched=false,
            awaitingGameConfirmation=true,slot=slot,marchUuid=selected.uuid}}
    end
    if selected.target == target_enum then
        return {state="confirmed", data={
            observation="native_world_march_return_target",
            slot=slot,marchUuid=selected.uuid,
            formationUuid=selected.formation,
            target="BACK_HOME", returning=true
        }}
    end
    local entry = rawget(_G,"LuaEntry")
    local config = member(entry,"DataConfig")
    local ok_switch, user_confirmed_ui = invoke(config, "CheckSwitch", "slide_left_return")
    -- Native TileUI shows a confirmation dialogue when this switch is off.
    -- With that mode disabled we cannot faithfully bypass that confirmation.
    if not ok_switch or user_confirmed_ui ~= true then
        return blocked("Game requires the native return-home confirmation UI")
    end
    if game_input_clear() ~= true then
        return blocked("Text focus changed before return-home dispatch")
    end
    local final_target = read_return_state(selected)
    if final_target == nil or final_target ~= selected.target then
        return blocked("March target changed immediately before dispatch")
    end
    local util = rawget(_G, "MarchUtil")
    if type(member(util, "OnBackHome")) ~= "function" then
        return unavailable("Current game MarchUtil.OnBackHome missing")
    end
    local sent = pcall(member(util, "OnBackHome"), selected.uuid_number)
    if not sent then return blocked("Native return-home method rejected request") end
    outstanding[slot] = {uuid=selected.uuid,formation=selected.formation,
                          sentAt=os.time()}
    -- Native method sends an RPC; only a later game-owned WorldMarch target
    -- of BACK_HOME confirms its effective state transition.
    return {state="accepted",data={
        slot=slot, marchUuid=selected.uuid,
        requestDispatched=true,awaitingGameConfirmation=true
    }}
end

-- Read-only original Shield UI inventory source; never guess a duration from
-- a localized name or an unrelated item template field.
function M.ShieldOwnedInventory()
    local center = rawget(_G,"DataCenter")
    local items = member(center,"ItemData")
    local templates = member(center,"ItemTemplateManager")
    local ok_items, owned = invoke(items,"GetItemList",4,1)
    local ok_templates, options = invoke(templates,"GetTypeListByTypes",4,1)
    if not ok_items or not ok_templates or type(owned) ~= "table" or
       type(options) ~= "table" then
        return unavailable("Authoritative owned shield inventory or item templates unavailable")
    end
    local available = {}
    for _, item in pairs(owned) do
        local item_id = id_string(member(item,"itemId"))
        local count = tonumber(member(item,"count"))
        local uuid = id_string(member(item,"uuid"))
        if item_id ~= nil and uuid ~= nil and count and count > 0
            and count % 1 == 0 then
            for _, template in pairs(options) do
                if id_string(member(template,"id")) == item_id then
                    available[#available+1] = {
                        itemId=item_id, uuid=uuid, count=count
                    }
                    break
                end
            end
        end
    end
    return {state="confirmed",data={
        observation="native_type4_subtype1_owned_shield_items",
        items=available,durationMappingVerified=false
    }}
end

function M.Wrap(frozen_hotkeys)
    if type(frozen_hotkeys) ~= "table" or
       type(frozen_hotkeys.Handle) ~= "function" then
        error("FEATURE018 frozen hotkey module is required")
    end
    local previous = frozen_hotkeys.Handle
    local input = frozen_hotkeys.TextInputClear
    return function(fields)
        if type(fields) ~= "table" then return blocked("Invalid Hotkeys request") end
        if fields.action == "recall" then return recall(fields, input) end
        if fields.action == "attack" then
            return unavailable("No source-verified cursor-selected target plus fixed-slot attack sender")
        end
        if fields.action == "shieldUse" then
            if fields.slot ~= "8" and fields.slot ~= "12" and fields.slot ~= "24" then
                return blocked("Unrecognized shield duration")
            end
            return unavailable("Cannot source-verify shield duration to owned item UUID and native warning/confirmation")
        end
        return previous(fields)
    end
end

return M
