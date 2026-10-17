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
local AUTHORIZED_SUBACCOUNT_UID = "3133546747002289"
local AUTHORIZED_SUBACCOUNT_SERVER = 2289

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

-- Only the owner's R3 sub-account can read spending preflights or dispatch
-- speedup/equipment work. Every command rechecks the live game Player, not a
-- claimed browser UID or a cached connection/session identity. All three
-- native server getters must agree: the 2212 main account is read-only.
local function authorized_subaccount()
    local player, uid = current_account()
    if uid ~= AUTHORIZED_SUBACCOUNT_UID then
        return nil, blocked("Native game UID is not the authorized server-2289 sub-account")
    end
    for _, source in ipairs({"GetSourceServerId", "GetSelfServerId", "GetCurServerId"}) do
        local ok, server = invoke(player, source)
        if not ok or tonumber(server) ~= AUTHORIZED_SUBACCOUNT_SERVER then
            return nil, blocked("Native game server is not the authorized server-2289 sub-account")
        end
    end
    return uid, nil
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
    local query_only = fields.queryOnly == "1"
    if fields.queryOnly ~= nil and not query_only then
        return blocked("Unknown native recall status mode")
    end
    -- A query-only status observation cannot start or retry a march. It may
    -- run after the physical hotkey was released and game focus left the
    -- window, so it never creates a new game action or requires text focus.
    if query_only and outstanding[slot] == nil then
        return blocked("No previously dispatched owned recall to observe")
    end
    if not query_only and (type(game_input_clear) ~= "function" or
       game_input_clear() ~= true) then
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
    if query_only then
        return blocked("No matching in-flight owned recall is available")
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
        if fields.action == "frontlineReinforce" then
            if fields.slot ~= "5" then
                return blocked("Frontline shortcut requires original G slot")
            end
            if type(input) ~= "function" or input() ~= true then
                return blocked("Native game text input is active or unverified")
            end
            local mini = rawget(_G, "LWBridgeFeature018MiniGame")
            local fn = member(mini, "FrontlineReinforce")
            if type(fn) ~= "function" then
                return unavailable("Authenticated native Frontline battle producer is unavailable")
            end
            -- The game-side mini action performs its own *current* Parkour
            -- battle identity/hero/level and exact five-step count fencing.
            -- Never synthesize a client-only +5 receipt in this wrapper.
            local ok, result = pcall(fn, fields)
            if not ok or type(result) ~= "table" then
                return blocked("Native Frontline action returned no reliable game receipt")
            end
            return result
        end
        if fields.action == "attack" or fields.action == "speedupItem" or
           fields.action == "speedupDiamond" then
            if fields.action == "speedupItem" or fields.action == "speedupDiamond" then
                local uid, denial = authorized_subaccount()
                if not uid then return denial end
                local authenticated = {}
                for key, value in pairs(fields) do authenticated[key] = value end
                authenticated.authorizedUid = uid
                fields = authenticated
            end
            local action_module = rawget(_G, "LWBridgeFeature018AttackActions")
            if type(member(action_module, "Handle")) ~= "function" then
                return unavailable("Current v26 attack/rapid producer is unavailable")
            end
            local ok, result = pcall(action_module.Handle, fields, input)
            if not ok or type(result) ~= "table" then
                return blocked("Attack/rapid adapter did not provide native game evidence")
            end
            return result
        end
        if fields.action == "shieldUse" then
            if fields.slot ~= "8" and fields.slot ~= "12" and fields.slot ~= "24" then
                return blocked("Unrecognized shield duration")
            end
            -- The frozen Hotkey game module now joins owned inventory to the
            -- native StatusTab duration and protects every ItemUse with UI
            -- cooldown/warning and later independent shield+stock poststate.
            return previous(fields)
        end
        if fields.action == "randomRelocate" or fields.action == "allianceRelocate" then
            local relocation = rawget(_G, "LWBridgeFeature018RelocationActions")
            local handler = member(relocation,
                fields.action == "randomRelocate" and "RandomRelocate" or "AllianceRelocate")
            if type(handler) ~= "function" then
                return unavailable("Current relocation source preflight is unavailable")
            end
            local ok, result = pcall(handler, fields, input)
            if not ok or type(result) ~= "table" then
                return blocked("Relocation source-preflight did not return valid game evidence")
            end
            return result
        end
        return previous(fields)
    end
end

-- An explicit item-only march-speedup command for the mounted Hotkeys panel.
-- The real UID is obtained inside the already owner-admitted game, never from
-- a browser-provided identifier. Only exact R3 sub-account UID+server passes.
-- The existing Q/W/E/R path remains independent and cannot implicitly spend.
function M.RegisterRapid(feature, frozen_hotkeys)
    if type(feature) ~= "table" or type(feature.RegisterHandler) ~= "function" or
       type(frozen_hotkeys) ~= "table" then return false end
    local input = frozen_hotkeys.TextInputClear
    return feature.RegisterHandler("feature018_rapid_action", function(fields)
        if type(fields) ~= "table" or
           (fields.mode ~= "inventory" and fields.mode ~= "itemUse" and fields.mode ~= "status") or
           type(fields.slot) ~= "string" or not fields.slot:match("^[1-4]$") then
            return blocked("Invalid owned march rapid mode or squad slot")
        end
        local uid, denial = authorized_subaccount()
        if not uid then return denial end
        local adapter = rawget(_G, "LWBridgeFeature018AttackActions")
        if type(member(adapter, "Handle")) ~= "function" or
           type(member(adapter, "OwnedSpeedupInventory")) ~= "function" then
            return unavailable("Current v26 owned march speedup adapter missing")
        end
        local copy = {
            action = "speedupItem", slot = fields.slot, authorizedUid = uid,
            profileId = fields.profileId, launchSessionId = fields.launchSessionId,
            challenge = fields.challenge, gamePid = fields.gamePid,
        }
        if fields.mode == "itemUse" then
            if fields.itemOptIn ~= "1" or type(fields.itemId) ~= "string" or
               not fields.itemId:match("^[1-9]%d*$") then
                return blocked("No explicit owned-item speedup consent and valid item ID")
            end
            copy.itemOptIn, copy.itemId = "1", fields.itemId
        elseif fields.mode == "status" then
            copy.queryOnly = "1"
        elseif fields.itemId ~= nil or fields.itemOptIn ~= nil then
            return blocked("Read-only rapid inventory may not specify an item spend")
        end
        local ok, result
        if fields.mode == "inventory" then
            ok, result = pcall(adapter.OwnedSpeedupInventory, copy, input)
            if ok and type(result) == "table" and result.state == "confirmed" and
               type(member(result, "data")) == "table" and
               type(result.data.items) == "table" and #result.data.items == 0 and
               type(feature.Array) == "function" then
                feature.Array(result.data.items)
            end
        else
            ok, result = pcall(adapter.Handle, copy, input)
        end
        if not ok or type(result) ~= "table" then
            return blocked("Original march rapid producer gave no verifiable result")
        end
        if fields.mode == "status" and result.state == "accepted" then
            -- This confirms only the *observation of an unconfirmed pending
            -- request*, not that item use succeeded.
            return {state = "confirmed", data = {
                state = "pending", requestDispatched = false,
                observation = "native_rapid_item_request_unconfirmed",
                marchUuid = member(result.data, "marchUuid"),
                slot = tonumber(fields.slot),
            }}
        end
        if fields.mode == "status" and result.state == "confirmed" then
            result.data.state = "confirmed"
        end
        return result
    end)
end

-- Only the persisted equipment configuration from the currently admitted
-- profile may be examined. This is intentionally NOT an Alt+1..4 apply
-- producer: current v26 replies cannot correlate per-slot equipment changes.
-- No caller-provided preset/hero/equipment UUIDs may enter the game sender.
function M.RegisterEquipment(feature, frozen_hotkeys)
    if type(feature) ~= "table" or type(feature.RegisterHandler) ~= "function" or
       type(frozen_hotkeys) ~= "table" then return false end
    local input = frozen_hotkeys.TextInputClear
    return feature.RegisterHandler("feature018_equipment_preflight", function(fields)
        if type(fields) ~= "table" or type(fields.slot) ~= "string" or
           not fields.slot:match("^[1-4]$") then
            return blocked("Native equipment preflight requires preset ordinal 1–4")
        end
        local uid, denial = authorized_subaccount()
        if not uid then return denial end
        local serialized = fields.confirmedEquipmentPresets
        if type(serialized) ~= "string" or #serialized == 0 or #serialized > 14000 then
            return blocked("Verified profile equipment preset payload unavailable")
        end
        local ok_module, json = pcall(require,"rapidjson")
        if not ok_module or type(member(json,"decode")) ~= "function" then
            return unavailable("Original rapidjson equipment preset decoder unavailable")
        end
        local decoded_ok, presets = pcall(json.decode,serialized)
        if not decoded_ok or type(presets) ~= "table" or #presets > 4 then
            return blocked("Native persisted equipment preset data could not be decoded safely")
        end
        local selected = presets[tonumber(fields.slot)]
        if type(selected) ~= "table" or type(selected.id) ~= "string" then
            return unavailable("Requested equipment slot has no confirmed saved preset")
        end
        local module = rawget(_G,"LWBridgeFeature018EquipmentActions")
        if type(member(module,"Preflight")) ~= "function" then
            return unavailable("Original v26 hero equipment preflight module unavailable")
        end
        local native = {
            action="equipment", slot=fields.slot, presetId=selected.id,
            authorizedUid=uid, profileId=fields.profileId,
            launchSessionId=fields.launchSessionId, challenge=fields.challenge,
            gamePid=fields.gamePid,
        }
        local ok, result = pcall(module.Preflight,native,presets,input)
        if not ok or type(result) ~= "table" then
            return blocked("Native equipment preflight returned no valid observation")
        end
        if result.state == "confirmed" then
            local data = result.data
            if type(data) ~= "table" or
               data.observation ~= "native_HeroEquip_slot_inventory_preflight" or
               data.readOnly ~= true or data.readyToApply ~= false or
               data.requestDispatched ~= false then
                return blocked("Native equipment preflight may not report an apply or send")
            end
            if type(data.slots) == "table" and #data.slots == 0 and
               type(feature.Array) == "function" then
                feature.Array(data.slots)
            end
        end
        return result
    end)
end

return M
