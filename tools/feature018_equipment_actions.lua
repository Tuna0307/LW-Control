-- FEATURE-RECOVERY-018: read-only, exact-owner native hero equipment preflight.
--
-- Inspected archived ORIGINAL current v26 LWLF 3/26, LuaEntry SHA256
-- 50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137.
-- UI/UILWHero/UIHeroEquipListPanel/View/UIHeroEquipListPanelView.luac
--   SHA256 071f83832ed6ec19a675055d7c02974b3a651e9a8aed04f2c8c4494e93c82793
--   root/1 29-34 sends MsgDefines.HeroEquipInstall(hero.uuid,
--     {[slotType] = equip.uuid}); root/2 37-41 sends HeroEquipUninstall.
-- Net/Msgs/HeroEquipInstallMessage.luac
--   SHA256 ba38a144c063867b81cd8c6490df3ee276c60f6a451e51da44d98ae74784abd8
--   OnCreate 13-29 serializes PutLong("heroUid"), followed by
--   PutSFSArray("slot2Uid") of PutInt("slot")/PutLong("uid") pairs.
--   HandleMessage 31-38 only broadcasts heroUid on errorCode=nil; no
--   request UUID, per-slot acknowledgement or equipment poststate.
-- Net/Msgs/PushHeroEquipChangeMessage.luac
--   SHA256 1c274d50719c30eceaacd0aef5974e7d45b939db669f8c74ecc280d4c046333d
--   HandleMessage 17-24 calls EquipDataManager:UpdateManyEquip(list) when
--   errorCode=nil, independently of the install request.
-- DataCenter/EquipData/EquipDataManager.luac
--   SHA256 20da01b79bc503eb467fdcec6d0e83fef08672157ff92446f7902ef8e0058cf9
--   GetEquipByUuid root/12 365-367, GetAllEquipList root/14 382-384,
--   GetAllUnusedEquipList root/19 465-477 (heroUuid nil/<=0 free).
-- DataCenter/ArmyFormationData/ArmyFormationInfo.luac
--   GetLocalHeroAtSlotIndex 237-239 from localIndexToHeroDic.
-- DataCenter/HeroData/HeroDataManager.luac GetHeroByUuid 292-297.
-- UI/UILWHero/UIHeroEquipListPanel/Controller/UIHeroEquipListPanelCtrl.luac
--   GetAllEquipDataList 17-78 uses GetAllEquipListBySlotTypeAndHeroType.
-- EquipDataManager.GetAllEquipListBySlotTypeAndHeroType 419-438 admits only
-- equip.config.heroType == hero.heroType or HeroType.All, using the same
-- native slot. GetHeroBetterEquip 522-547 respects IsUnlockEquipFunction.
--
-- Unlike the game's PVPFormationPreset (a formation UUID), the persisted
-- LWBridge 0.3.17 equipmentPresets[N] are {id,name,squads:[{
-- squadIndex,positions:[{position,equips:[{slot,equipUuid,...}]}]}]}.
-- See src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js 8-27.
--
-- Public API:
--   M.Preflight(fields, confirmedPresets, gameInputClear)
--       Returns state=confirmed + readOnly=true preflight observation ONLY.
--       confirmedPresets MUST originate from the owner profile's *confirmed*
--       native persisted equipment_config_get, never frontend draft/client
--       provided JSON. No mutation method is reachable from this function.
--   M.Handle(fields, confirmedPresets, gameInputClear)
--       Action path always unsupported, optionally including preflight hint.
-- Nothing is registered, sent, mutated or executed when this file loads.
--
-- C# bridge must pass action="equipment", slot=1..4 (ordinal of confirmed
-- equipmentPresets), authorizedUid=game-session UID, presetId=selected.id,
-- and authenticated profileId/launchSessionId/challenge/gamePid. The existing
-- game native command only forwards action+slot and currently has no
-- confirmed-preset provenance; the prime must provide an owner-bound
-- retrieval/marshalling seam before even read-only integration.

local M = { VERSION = "feature018-equipment-actions-1" }

local function member(object, key)
    if object == nil then return nil end
    local ok, value = pcall(function() return object[key] end)
    if ok then return value end
    return nil
end

local function invoke(object, name, ...)
    local fn = member(object, name)
    if type(fn) ~= "function" then return false, nil end
    return pcall(fn, object, ...)
end

local function blocked(message)
    return {state="failed",errorCode="GAME_PRECONDITION_FAILED",error=message}
end

local function unavailable(message)
    return {state="unsupported",errorCode="GAME_METHOD_UNAVAILABLE",error=message}
end

local function exact_id(value)
    if type(value) == "boolean" or value == nil then return nil end
    if type(value) == "number" then
        if value ~= value or value == math.huge or value == -math.huge or
           value < 1 or value % 1 ~= 0 or
           (math.type and math.type(value) == "float" and
            value > 9007199254740991) then return nil end
    elseif type(value) ~= "string" then
        return nil
    end
    local result = tostring(value)
    if #result < 1 or #result > 20 or result:sub(1,1) == "0" or
       result:find("[^0-9]") then return nil end
    return result
end

local function exact_session(fields)
    if type(fields) ~= "table" then return false end
    for _, key in ipairs({"profileId","launchSessionId","challenge","gamePid"}) do
        local value = fields[key]
        if type(value) ~= "string" or #value == 0 or #value > 128 or
           value:find("[^%w_-]") then return false end
    end
    local player = member(rawget(_G, "LuaEntry"), "Player")
    local native_uid = exact_id(member(player,"uid"))
    return native_uid ~= nil and type(fields.authorizedUid) == "string" and
           fields.authorizedUid == native_uid
end

local function number_in_range(value, low, high)
    if type(value) ~= "number" or value % 1 ~= 0 then return nil end
    if value < low or value > high then return nil end
    return value
end

local function selected_preset(fields, confirmed)
    if type(fields) ~= "table" or fields.action ~= "equipment" or
       type(fields.slot) ~= "string" or not fields.slot:match("^[1-4]$") then
        return nil, blocked("Equipment shortcut requires an exact Alt+1 through Alt+4 ordinal")
    end
    if type(confirmed) ~= "table" or #confirmed > 4 or #confirmed == 0 then
        return nil, unavailable("Confirmed per-profile equipment presets were not supplied")
    end
    for i = 1, #confirmed do
        if type(confirmed[i]) ~= "table" or
           type(confirmed[i].id) ~= "string" or
           confirmed[i].id == "" or type(confirmed[i].name) ~= "string" then
            return nil, blocked("Malformed native persisted preset selection")
        end
        for j = 1, i - 1 do
            if confirmed[j].id == confirmed[i].id then
                return nil, blocked("Duplicate persisted preset identity")
            end
        end
    end
    local preset = confirmed[tonumber(fields.slot)]
    if preset == nil then return nil, unavailable("Requested equipment preset slot is not saved") end
    if type(fields.presetId) ~= "string" or fields.presetId ~= preset.id then
        return nil, blocked("Preset UUID/id differs from the confirmed owner profile selection")
    end
    if type(preset.squads) ~= "table" then
        return nil, blocked("Confirmed equipment preset has no valid squad loadouts")
    end
    return preset
end

local function read_hero(squad_index, position)
    local center = rawget(_G,"DataCenter")
    local manager = member(center,"ArmyFormationDataManager")
    local heroes = member(center,"HeroDataManager")
    local ok_form, formation = invoke(manager,"GetOneArmyInfoByIndex",squad_index)
    if not ok_form or formation == nil then
        return nil, "Exact live squad formation slot is unavailable"
    end
    local ok_slot, hero_uuid = invoke(formation,"GetLocalHeroAtSlotIndex",position)
    local id = exact_id(hero_uuid)
    if not ok_slot or id == nil then
        return nil, "Saved equipment position has no verified current hero"
    end
    local ok_hero, hero = invoke(heroes,"GetHeroByUuid",tonumber(id))
    if not ok_hero or hero == nil or exact_id(member(hero,"uuid")) ~= id then
        return nil, "Hero slot UUID does not match fresh native hero manager"
    end
    if type(member(hero,"GetEquipBySlotType")) ~= "function" then
        return nil, "Native hero equipment-slot reader unavailable"
    end
    local ok_unlock, unlocked = invoke(hero,"IsUnlockEquipFunction")
    if not ok_unlock or type(unlocked) ~= "boolean" or not unlocked then
        return nil, "Native hero equipment slots are not verified unlocked"
    end
    if member(hero,"heroType") == nil then
        return nil, "Native hero equipment-category identity is unavailable"
    end
    return {uuid=id,object=hero}
end

local function validate_slot(equips)
    if type(equips) ~= "table" or #equips > 4 then
        return nil, "Saved equipment slot list is malformed"
    end
    local mapping = {}
    for i = 1, #equips do
        local entry = equips[i]
        local slot = type(entry) == "table" and number_in_range(entry.slot,1,4)
        local uuid = type(entry) == "table" and exact_id(entry.equipUuid)
        if slot == nil or uuid == nil or mapping[slot] ~= nil then
            return nil, "Saved equipment slot/UUID is invalid or repeated"
        end
        mapping[slot] = uuid
    end
    return mapping
end

local function validate_selected_loadout(preset)
    if #preset.squads > 4 then
        return nil, "Saved preset has too many squads"
    end
    local wanted = {}
    local position_count, item_count = 0, 0
    local claimed_equip = {}
    for _, saved_squad in ipairs(preset.squads) do
        local index = type(saved_squad) == "table" and
                      number_in_range(saved_squad.squadIndex,1,4)
        if index == nil or wanted[index] ~= nil or
           type(saved_squad.positions) ~= "table" or
           #saved_squad.positions > 5 then
            return nil, "Saved preset squad identity/positions are malformed"
        end
        wanted[index] = {}
        for _, entry in ipairs(saved_squad.positions) do
            local position = type(entry) == "table" and
                             number_in_range(entry.position,1,5)
            if position == nil or wanted[index][position] ~= nil then
                return nil, "Saved preset hero position is repeated or invalid"
            end
            local mapping, reason = validate_slot(entry.equips)
            if not mapping then return nil, reason end
            wanted[index][position] = mapping
            position_count = position_count + 1
            for _, uuid in pairs(mapping) do
                if claimed_equip[uuid] then
                    return nil, "One equipment UUID is assigned to multiple saved heroes"
                end
                claimed_equip[uuid] = true
                item_count = item_count + 1
            end
        end
    end
    if position_count == 0 then return nil, "Saved equipment preset has no hero positions" end
    return wanted, position_count, item_count
end

local function inventory_for_target(manager, uuid, hero, slot)
    local ok, item = invoke(manager,"GetEquipByUuid",tonumber(uuid))
    if not ok or item == nil or exact_id(member(item,"uuid")) ~= uuid then
        return nil, "Saved equipment UUID is not present in native inventory"
    end
    if number_in_range(member(item,"slot"),1,4) ~= slot then
        return nil, "Saved equipment UUID belongs to a different native slot type"
    end
    local config = member(item,"config")
    local item_type = member(config,"heroType")
    local hero_type = member(hero.object,"heroType")
    local unrestricted_type = member(rawget(_G,"HeroType"),"All")
    if item_type == nil or hero_type == nil or unrestricted_type == nil then
        return nil, "Native equipment/hero compatibility type unavailable"
    end
    if item_type ~= hero_type and item_type ~= unrestricted_type then
        return nil, "Native equipment hero-type eligibility rejects saved item"
    end
    local wearer = member(item,"heroUuid")
    local wearer_id = exact_id(wearer)
    if wearer ~= nil and wearer ~= 0 and wearer_id == nil then
        return nil, "Native equipment wearer identity cannot be verified"
    end
    return {item=item,wearer=wearer_id,alreadyWorn=wearer_id == hero.uuid}
end

function M.Preflight(fields, confirmed_presets, game_input_clear)
    local preset, error_receipt = selected_preset(fields,confirmed_presets)
    if preset == nil then return error_receipt end
    if not exact_session(fields) then
        return blocked("Game UID is not the authenticated owner of this profile session")
    end
    if type(game_input_clear) ~= "function" or game_input_clear() ~= true then
        return blocked("Game text-input focus is selected or unverifiable")
    end
    local wanted, first, second = validate_selected_loadout(preset)
    if wanted == nil then return blocked(first) end
    local center = rawget(_G,"DataCenter")
    local equips = member(center,"EquipDataManager")
    if type(member(equips,"GetEquipByUuid")) ~= "function" then
        return unavailable("Original native equipment inventory reader is unavailable")
    end
    local observations, changed, worn_other, removals = {}, 0, 0, 0
    for squad_index = 1, 4 do
        local group = wanted[squad_index]
        if group ~= nil then
            for position = 1, 5 do
                local requested = group[position]
                if requested ~= nil then
                    local hero, reason = read_hero(squad_index,position)
                    if hero == nil then return unavailable(
                        "Squad " .. squad_index .. " position " .. position .. ": " .. reason)
                    end
                    for slot = 1, 4 do
                        local target = requested[slot]
                        local ok_current, current_item = invoke(hero.object,"GetEquipBySlotType",slot)
                        if not ok_current then
                            return unavailable("Native current hero equipment-slot state unavailable")
                        end
                        local current_id = current_item and exact_id(member(current_item,"uuid"))
                        if current_item ~= nil and current_id == nil then
                            return blocked("Current hero slot equipment has invalid native UUID")
                        end
                        if current_id ~= nil then
                            local current_stock, current_reason =
                                inventory_for_target(equips,current_id,hero,slot)
                            if current_stock == nil or not current_stock.alreadyWorn then
                                return blocked("Current equipped item disagrees with native inventory: " ..
                                    (current_reason or "wearer mismatch"))
                            end
                        end
                        local status = "alreadyEmpty"
                        if target ~= nil then
                            local desired, target_reason = inventory_for_target(
                                equips,target,hero,slot)
                            if not desired then return blocked(target_reason) end
                            if current_id == target and desired.alreadyWorn then
                                status = "alreadyEquipped"
                            elseif desired.wearer ~= nil and not desired.alreadyWorn then
                                status = "wornByOtherHero"
                                worn_other = worn_other + 1
                            elseif desired.alreadyWorn then
                                return blocked("Saved item has current wearer but is absent from that hero slot")
                            else
                                status = current_id and "replaceRequired" or "installRequired"
                            end
                        elseif current_id ~= nil then
                            status = "uninstallRequired"
                            removals = removals + 1
                        end
                        if status ~= "alreadyEquipped" and status ~= "alreadyEmpty" then
                            changed = changed + 1
                        end
                        observations[#observations+1] = {
                            squadIndex=squad_index, position=position, heroUuid=hero.uuid,
                            slot=slot, currentEquipUuid=current_id or "",
                            targetEquipUuid=target or "", state=status
                        }
                    end
                end
            end
        end
    end
    return {state="confirmed",data={
        observation="native_HeroEquip_slot_inventory_preflight",
        readOnly=true, action="equipment", slot=tonumber(fields.slot),
        presetId=preset.id, readyToApply=false, requestDispatched=false,
        sourceVersion=26, plannedPositions=first, plannedItems=second,
        changedSlots=changed, wornByOtherHero=worn_other,
        uninstallSlots=removals, slots=observations,
    }}
end

function M.Handle(fields, confirmed_presets, game_input_clear)
    if type(fields) ~= "table" or fields.action ~= "equipment" then
        return blocked("Only original equipment hotkeys are supported by this adapter")
    end
    -- The original install reply only sends an uncorrelated heroUid/event,
    -- while later inventory pushes can arrive independently. Multi-hero
    -- swap, removal and busy-formation safety/ACK are not verified, so no
    -- network/mutation call is reachable even for a fully matching snapshot.
    local audit = M.Preflight(fields,confirmed_presets,game_input_clear)
    if audit.state ~= "confirmed" then return audit end
    return {state="unsupported",errorCode="GAME_METHOD_UNAVAILABLE",
        error="Native HeroEquipInstall has no request-correlated per-hero slot ACK or safe cross-hero preset transaction; no equipment changed",
        data=audit.data}
end

return M
