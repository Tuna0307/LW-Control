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

local function native_item_uuid(value)
    if type(value) ~= "string" and type(value) ~= "number" then return nil end
    local token = tostring(value)
    if #token < 1 or #token > 128 or token == "0" or
       token:find("[^%w_-]") then return nil end
    return token
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
        -- Reject decimal-looking or padded server aliases such as "02289"
        -- and "2289.0": admission must reflect the exact native server.
        local valid_server =
            (type(server) == "number" and server == AUTHORIZED_SUBACCOUNT_SERVER)
            or (type(server) == "string" and
                server == tostring(AUTHORIZED_SUBACCOUNT_SERVER))
        if not ok or not valid_server then
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
    local player, uid = current_account()
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
        member(player, "uid"), native_id, member(player, "allianceId"))
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
    local admitted, denial = authorized_subaccount()
    if not admitted then return denial end
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

-- Read-only original CityShieldPage inventory/status join. Its CellsCallBack
-- resolves item template.para1 through StatusTab.time (seconds), and the
-- StatusManager independently identifies MAIN_SHIELD status IDs. Never infer
-- duration from a localized name, item ID, or inventory ordering.
function M.ShieldOwnedInventory()
    local uid, denial = authorized_subaccount()
    if not uid then return denial end
    local center = rawget(_G,"DataCenter")
    local items = member(center,"ItemData")
    local templates = member(center,"ItemTemplateManager")
    local status = member(center,"StatusManager")
    local controller = rawget(_G,"LocalController")
    local table_name = member(rawget(_G,"TableName"),"StatusTab")
    local ok_items, owned = invoke(items,"GetItemList",4,1)
    local ok_templates, options = invoke(templates,"GetTypeListByTypes",4,1)
    local ok_ids, status_ids = invoke(status,"GetShieldStatusIds")
    local ok_controller, native_tables = invoke(controller,"instance")
    if not ok_items or not ok_templates or type(owned) ~= "table" or
       type(options) ~= "table" or not ok_ids or type(status_ids) ~= "table" or
       not ok_controller or native_tables == nil or table_name == nil then
        return unavailable("Authoritative shield inventory, MAIN_SHIELD status IDs or StatusTab unavailable")
    end
    local classified_ids = 0
    for _, match in pairs(status_ids) do
        if match == true then classified_ids = classified_ids + 1 end
    end
    if classified_ids == 0 then
        return unavailable("Native MAIN_SHIELD status classification contains no verified IDs")
    end
    local mapped = {}
    local used_hours = {}
    for _, template in pairs(options) do
        local item_id = id_string(member(template,"id"))
        local status_id = id_string(member(template,"para1"))
        if item_id ~= nil and status_id ~= nil and
           (status_ids[tonumber(status_id)] == true or status_ids[status_id] == true) then
            local ok_row, row = invoke(native_tables,"getLine",table_name,member(template,"para1"))
            local seconds = ok_row and tonumber(member(row,"time")) or nil
            if row == nil or seconds == nil or seconds <= 0 or
               seconds % 1 ~= 0 then
                return unavailable("Source-classified MAIN_SHIELD StatusTab duration is unreadable")
            end
            if mapped[item_id] ~= nil then
                return unavailable("Original shield item template identity is duplicated")
            end
            local hours = seconds / 3600
            mapped[item_id] = {statusId=status_id,durationHours=hours}
            if hours == 8 or hours == 12 or hours == 24 then
                used_hours[hours] = (used_hours[hours] or 0) + 1
            end
        end
    end
    local available = {}
    local seen_uuids = {}
    local stock_by_hours = {}
    for _, item in pairs(owned) do
        local item_id = id_string(member(item,"itemId"))
        local count = tonumber(member(item,"count"))
        local uuid = native_item_uuid(member(item,"uuid"))
        local entry = item_id and mapped[item_id] or nil
        if entry ~= nil then
            if uuid == nil or count == nil or count < 0 or count % 1 ~= 0 or
               seen_uuids[uuid] then
                return unavailable("Owned shield stack count or identity is inconsistent")
            end
            seen_uuids[uuid] = true
            if count > 0 then
                stock_by_hours[entry.durationHours] =
                    (stock_by_hours[entry.durationHours] or 0) + 1
                available[#available+1] = {
                    itemId=item_id, uuid=uuid, count=count,
                    statusId=entry.statusId, durationHours=entry.durationHours
                }
            end
        end
    end
    for _, entry in ipairs(available) do
        entry.uniqueDuration=(used_hours[entry.durationHours] == 1 and
                              stock_by_hours[entry.durationHours] == 1)
    end
    table.sort(available,function(a,b)
        if a.durationHours ~= b.durationHours then
            return a.durationHours < b.durationHours
        end
        return a.uuid < b.uuid
    end)
    return {state="confirmed",data={
        observation="native_MAIN_SHIELD_StatusTab_owned_item_duration_join",
        items=available,durationMappingVerified=true,
        readOnly=true,requestDispatched=false
    }}
end

-- Read-only native inventory endpoint for the Hotkeys panel. The FEATURE018
-- bridge authenticates the profile/lease and this producer independently
-- verifies the current game's exact UID and all three 2289 server getters.
-- No client-supplied item identity, duration or spend instruction is used.
function M.RegisterShieldInventory(feature)
    if type(feature) ~= "table" or
       type(feature.RegisterHandler) ~= "function" or
       type(feature.Array) ~= "function" then return false end
    return feature.RegisterHandler("feature018_shield_inventory_get", function(fields)
        if type(fields) ~= "table" then
            return blocked("Native shield inventory requires an authenticated read request")
        end
        local result = M.ShieldOwnedInventory()
        if type(result) ~= "table" or result.state ~= "confirmed" then
            return result
        end
        local data = result.data
        if type(data) ~= "table" or data.readOnly ~= true or
           data.requestDispatched ~= false or type(data.items) ~= "table" then
            return blocked("Shield inventory did not provide a read-only native observation")
        end
        if #data.items == 0 then
            -- The real FEATURE018 JSON encoder otherwise serializes {}.
            -- Array applies the __feature018_array marker to an empty table.
            local ok_array, marked = pcall(feature.Array, data.items)
            local marker = ok_array and type(marked) == "table" and
                           getmetatable(marked) or nil
            if not ok_array or type(marked) ~= "table" or
               type(marker) ~= "table" or marker.__feature018_array ~= true then
                return unavailable("Native shield inventory JSON array tagging failed")
            end
            data.items = marked
        end
        return result
    end)
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
        if fields.action == "equipment" then
            local uid, denial = authorized_subaccount()
            if not uid then return denial end
            local serialized=fields.confirmedEquipmentPresets
            if type(serialized)~="string" or #serialized<2 or #serialized>60000 then
                return blocked("Confirmed profile equipment presets were not supplied")
            end
            local ok_json,json=pcall(require,"rapidjson")
            if not ok_json or type(member(json,"decode"))~="function" then
                return unavailable("Original equipment preset decoder unavailable")
            end
            local ok_presets,presets=pcall(json.decode,serialized)
            local slot=type(fields.slot)=="string" and tonumber(fields.slot) or nil
            if not ok_presets or type(presets)~="table" or not slot or slot<1 or slot>4 or slot%1~=0 or
               type(presets[slot])~="table" then return blocked("Confirmed equipment ordinal is unavailable") end
            local native={}
            for key,value in pairs(fields)do native[key]=value end
            native.authorizedUid=uid;native.presetId=presets[slot].id
            local adapter=rawget(_G,"LWBridgeFeature018EquipmentActions")
            if type(member(adapter,"Handle"))~="function" then return unavailable("Native equipment adapter unavailable") end
            local ok,result=pcall(adapter.Handle,native,presets,input)
            if not ok or type(result)~="table" then return blocked("Equipment adapter returned no native receipt") end
            return result
        end
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
            -- Unlike a browser/session token, these values are read from the
            -- game owner on every use AND read-only pending-status query.
            -- The frozen shield sender does not itself check the 2289 server.
            local uid, denial = authorized_subaccount()
            if not uid then return denial end
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
    feature.RegisterHandler("feature018_attack_prepare",function(fields)
        local uid,denial=authorized_subaccount()
        if not uid then return denial end
        local adapter=rawget(_G,"LWBridgeFeature018AttackActions")
        if type(member(adapter,"Prepare")) ~= "function" then return unavailable("Native attack preparation unavailable") end
        local copy={}
        for k,v in pairs(fields) do copy[k]=v end
        copy.authorizedUid=uid
        local result=adapter.Prepare(copy,input)
        if result.state == "confirmed" and type(member(result.data,"candidates")) == "table" then
            feature.Array(result.data.candidates)
        end
        return result
    end)
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
-- Read-only original F9/F10 prerequisite join. A source home-world rally is
-- not proof of any F10 packet. This handler accepts no item selection, mark,
-- confirmation or destination from the browser and can NEVER send a move.
function M.RegisterRelocation(feature, frozen_hotkeys)
    if type(feature) ~= "table" or type(feature.RegisterHandler) ~= "function" or
       type(frozen_hotkeys) ~= "table" then return false end
    return feature.RegisterHandler("feature018_relocation_preflight", function(fields)
        if type(fields) ~= "table" or
           (fields.action ~= "randomRelocate" and fields.action ~= "allianceRelocate") then
            return blocked("Original F9/F10 native readonly action required")
        end
        local uid,denial=authorized_subaccount()
        if not uid then return denial end
        local adapter=rawget(_G,"LWBridgeFeature018RelocationActions")
        if type(member(adapter,"Readiness"))~="function" then
            return unavailable("Original native relocation readiness source unavailable")
        end
        local native={}
        for k,v in pairs(fields) do native[k]=v end
        native.authorizedUid=uid
        native.slot="0"
        local ok,result=pcall(adapter.Readiness,native,frozen_hotkeys.TextInputClear,fields.action)
        if not ok or type(result)~="table" then
            return blocked("Original native relocation readiness failed closed")
        end
        if result.state=="confirmed" then
            local data=result.data
            if type(data)~="table" or data.action~=fields.action or
               data.ownerUid~=uid or data.readOnly~=true or
               data.requestDispatched~=false or
               data.relocationConfirmed~=false then
                return blocked("Native relocation preflight returned an untrusted action claim")
            end
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
    feature.RegisterHandler("feature018_equipment_snapshot",function(fields)
        local uid, denial=authorized_subaccount()
        if not uid then return denial end
        local module=rawget(_G,"LWBridgeFeature018EquipmentActions")
        if type(member(module,"Snapshot"))~="function" then return unavailable("Native equipment snapshot unavailable") end
        local native={}
        for key,value in pairs(fields) do native[key]=value end
        native.authorizedUid=uid
        local result=module.Snapshot(native)
        if result.state=="confirmed" and type(result.data)=="table" then
            for _,key in ipairs({"squads","items"}) do
                if type(result.data[key])=="table" then feature.Array(result.data[key]) end
            end
            for _,squad in ipairs(result.data.squads or {}) do
                feature.Array(squad.positions)
                for _,position in ipairs(squad.positions or {}) do feature.Array(position.equips) end
            end
        end
        return result
    end)
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
