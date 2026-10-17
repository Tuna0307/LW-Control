-- FEATURE-RECOVERY-018: exact-owner native equipment snapshot and sequential apply.
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
--       Exact 2289-only sequential original RPC plan; queryOnly resumes its
--       authorized next step after native state confirms the prior step.
-- Nothing is registered, sent, mutated or executed when this file loads.
--
-- C# bridge must pass action="equipment", slot=1..4 (ordinal of confirmed
-- equipmentPresets), authorizedUid=game-session UID, presetId=selected.id,
-- and authenticated profileId/launchSessionId/challenge/gamePid. The existing
-- game native command only forwards action+slot and currently has no
-- confirmed-preset provenance; the prime must provide an owner-bound
-- retrieval/marshalling seam before even read-only integration.

local M = { VERSION = "feature018-equipment-actions-2" }

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
    if native_uid ~= "3133546747002289" or
       type(fields.authorizedUid) ~= "string" or
       fields.authorizedUid ~= native_uid then return false end
    -- The public read-only Preflight also runs with native Lua authority:
    -- it cannot depend solely on the C# owner check preceding a mailbox
    -- request. Every original native server getter must agree on home 2289.
    for _,getter in ipairs({"GetSourceServerId","GetSelfServerId","GetCurServerId"}) do
        local ok,server=invoke(player,getter)
        if not ok or (server~=2289 and server~="2289") then return false end
    end
    return true
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
    local claimed_equip,expected_heroes = {},{}
    for _, saved_squad in ipairs(preset.squads) do
        local index = type(saved_squad) == "table" and
                      number_in_range(saved_squad.squadIndex,1,4)
        if index == nil or wanted[index] ~= nil or
           type(saved_squad.positions) ~= "table" or
           #saved_squad.positions > 5 then
            return nil, "Saved preset squad identity/positions are malformed"
        end
        wanted[index] = {}
        expected_heroes[index] = {}
        for _, entry in ipairs(saved_squad.positions) do
            local position = type(entry) == "table" and
                             number_in_range(entry.position,1,5)
            if position == nil or wanted[index][position] ~= nil then
                return nil, "Saved preset hero position is repeated or invalid"
            end
            local mapping, reason = validate_slot(entry.equips)
            if not mapping then return nil, reason end
            -- Native v26 M.Snapshot persists the actual hero UUID alongside
            -- each squad/position. Older UI-created presets are position-only;
            -- honor those, but NEVER ignore a saved native hero identity
            -- when it is supplied by the authenticated owner profile.
            if entry.heroUuid ~= nil then
                local saved_hero=exact_id(entry.heroUuid)
                if not saved_hero then
                    return nil,"Saved equipment hero UUID is malformed"
                end
                expected_heroes[index][position]=saved_hero
            end
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
    return wanted, position_count, item_count, expected_heroes
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
    if wearer ~= nil and not (type(wearer)=="number" and wearer<=0 and wearer%1==0) and wearer_id == nil then
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
    local focus_ok,focus_clear=false,false
    if type(game_input_clear)=="function" then
        focus_ok,focus_clear=pcall(game_input_clear)
    end
    if not focus_ok or focus_clear~=true then
        return blocked("Game text-input focus is selected or unverifiable")
    end
    local wanted, first, second, expected_heroes = validate_selected_loadout(preset)
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
                    local saved_hero=expected_heroes[squad_index][position]
                    if saved_hero and hero.uuid~=saved_hero then
                        return blocked("Saved equipment hero UUID no longer matches its native formation position")
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

-- A single sequential transaction. Network return is never proof of a slot change.
-- HeroEquipInstall/Uninstall replies carry no slot payload; only the original
-- PushHeroEquipChange -> EquipDataManager and hero slot readers advance a step.
local transaction = nil
local function action_owner(fields)
    if not exact_session(fields) then return false end
    local player = member(rawget(_G,"LuaEntry"),"Player")
    if exact_id(member(player,"uid")) ~= "3133546747002289" then return false end
    for _, getter in ipairs({"GetSourceServerId","GetSelfServerId","GetCurServerId"}) do
        local ok, server = invoke(player,getter)
        if not ok or (server ~= 2289 and server ~= "2289") then return false end
    end
    return true
end
local function owner_key(fields)
    return table.concat({fields.profileId,fields.launchSessionId,fields.challenge,
        fields.gamePid,fields.authorizedUid,fields.slot,fields.presetId},"/")
end
local function fingerprint(slots)
    local parts = {}
    for _, slot in ipairs(slots) do
        parts[#parts+1] = table.concat({slot.squadIndex,slot.position,slot.heroUuid,
            slot.slot,slot.targetEquipUuid},":")
    end
    return table.concat(parts,"/")
end
local function receipt(tx, confirmed)
    return {state=confirmed and "confirmed" or "accepted",data={
        observation=confirmed and "native_HeroEquip_preset_slots_applied" or
            "native_HeroEquip_preset_pending",
        action="equipment",slot=tonumber(tx.slot),presetId=tx.presetId,
        requestDispatched=not confirmed and tx.sent>0,
        awaitingGameConfirmation=not confirmed,equipmentApplied=confirmed,
        nativeSlotPoststateVerified=confirmed,
        perSlotReplyCorrelated=false,
        readOnly=false,sourceVersion=26,completedSteps=tx.completed,
        dispatchedSteps=tx.sent,plannedSteps=#tx.steps,
        beforeSlots=tx.before,slots=tx.latest,
    }}
end
local function observe_step(step)
    local hero, why = read_hero(step.squadIndex,step.position)
    if not hero or hero.uuid ~= step.heroUuid then return false,why or "Native hero identity changed" end
    local ok, item = invoke(hero.object,"GetEquipBySlotType",step.slot)
    if not ok then return false,"Native slot reader failed" end
    local current = item and exact_id(member(item,"uuid")) or ""
    if current ~= step.after then
        if current ~= step.before then
            -- The native push/slot has changed to a THIRD item, not this
            -- original HeroEquipInstall/Uninstall step's expected result.
            -- A different player's or manual update is not a pending ACK.
            return false,"Native gear slot changed to an unexpected equipment UUID"
        end
        return nil
    end
    if step.after ~= "" then
        local inv, reason = inventory_for_target(
            member(rawget(_G,"DataCenter"),"EquipDataManager"),step.after,hero,step.slot)
        if not inv or not inv.alreadyWorn then return false,reason or "Equipment wearer push pending" end
    elseif step.before ~= "" then
        local ok_item, old = invoke(member(rawget(_G,"DataCenter"),"EquipDataManager"),
            "GetEquipByUuid",tonumber(step.before))
        if not ok_item or not old then return false,"Removed equipment inventory missing" end
        local wearer=member(old,"heroUuid")
        local wearer_id=exact_id(wearer)
        if wearer_id == step.heroUuid then return nil end
        -- The original Uninstall step must free this inventory UUID. Another
        -- hero acquiring it concurrently is not proof that our uninstall
        -- completed; do not advance the preset to the next native RPC.
        if wearer_id~=nil then
            return false,"Removed equipment was reassigned to another hero"
        end
        if wearer~=nil and not (type(wearer)=="number" and
           wearer<=0 and wearer%1==0) then
            return false,"Removed equipment wearer state is unverified"
        end
    end
    return true
end
-- Source read-only owner snapshot for saving ordinary app equipment presets.
-- Returned gear identifiers are native UUID strings, never template IDs.
function M.Snapshot(fields)
    if not action_owner(fields) then return blocked("Equipment snapshot requires the exact authorized 2289 session") end
    local squads, candidates={},{}
    local center=rawget(_G,"DataCenter")
    local manager=member(center,"ArmyFormationDataManager")
    for index=1,4 do
        local ok,formation=invoke(manager,"GetOneArmyInfoByIndex",index)
        if not ok then return unavailable("Native squad lookup unavailable") end
        if formation then
            local group={squadIndex=index,positions={}}
            for position=1,5 do
                local ok_slot,hero_uuid=invoke(formation,"GetLocalHeroAtSlotIndex",position)
                if not ok_slot then return unavailable("Native formation hero lookup unavailable") end
                if exact_id(hero_uuid) then
                    local hero,reason=read_hero(index,position)
                    if not hero then return unavailable(reason) end
                    local entry={position=position,heroUuid=hero.uuid,equips={}}
                    for slot=1,4 do
                        local ok_item,item=invoke(hero.object,"GetEquipBySlotType",slot)
                        if not ok_item then return unavailable("Native gear slot lookup unavailable") end
                        if item then
                            local id=exact_id(member(item,"uuid"))
                            local stock,reason=id and inventory_for_target(
                                member(center,"EquipDataManager"),id,hero,slot)
                            if not stock or not stock.alreadyWorn then return blocked(reason or "Native slot inventory mismatch") end
                            entry.equips[#entry.equips+1]={slot=slot,equipUuid=id}
                        end
                    end
                    group.positions[#group.positions+1]=entry
                end
            end
            if #group.positions>0 then squads[#squads+1]=group end
        end
    end
    local ok,inventory=invoke(member(center,"EquipDataManager"),"GetAllEquipList")
    if not ok or type(inventory)~="table" then return unavailable("Native equipment inventory unavailable") end
    for _,item in pairs(inventory)do
        local id=exact_id(member(item,"uuid"))
        local slot=number_in_range(member(item,"slot"),1,4)
        if id and slot then
            candidates[#candidates+1]={equipUuid=id,slot=slot,
                itemId=tostring(member(item,"itemId") or ""),
                heroType=member(member(item,"config"),"heroType"),
                heroUuid=exact_id(member(item,"heroUuid")) or "",
                unused=exact_id(member(item,"heroUuid"))==nil}
        end
    end
    table.sort(candidates,function(a,b)return a.equipUuid<b.equipUuid end)
    return {state="confirmed",data={observation="native_HeroEquip_owner_snapshot",
        readOnly=true,requestDispatched=false,ownerUid=fields.authorizedUid,
        serverId=2289,squads=squads,items=candidates}}
end

function M.Handle(fields, confirmed_presets, game_input_clear)
    if not action_owner(fields) then return blocked("Equipment actions require exact authorized UID on server 2289") end
    if fields.queryOnly ~= nil and fields.queryOnly ~= "1" then return blocked("Unknown equipment query mode") end
    local audit = M.Preflight(fields,confirmed_presets,
        fields.queryOnly == "1" and function() return true end or game_input_clear)
    if audit.state ~= "confirmed" then return audit end
    local slots = audit.data.slots
    local key, revision = owner_key(fields),fingerprint(slots)
    if transaction and transaction.key ~= key then
        return blocked("Previous equipment transaction is unresolved; owner or preset changed")
    end
    if transaction and transaction.fingerprint ~= revision then
        return blocked("Confirmed preset or native hero assignment changed during equipment transaction")
    end
    if fields.queryOnly == "1" and not transaction then return blocked("No pending equipment transaction") end
    if not transaction then
        local steps, heroes = {},{}
        local hero_positions={}
        for _, slot in ipairs(slots) do
            local position_key=tostring(slot.squadIndex)..":"..tostring(slot.position)
            if hero_positions[slot.heroUuid] and hero_positions[slot.heroUuid]~=position_key then
                return blocked("One native hero is present in multiple saved formation positions")
            end
            hero_positions[slot.heroUuid]=position_key
            heroes[slot.heroUuid]=true
        end
        -- Remove changed equipped slots before installing. This resolves cycles
        -- without silently taking gear from heroes outside this saved preset.
        if audit.data.wornByOtherHero > 0 then
            local manager = member(rawget(_G,"DataCenter"),"EquipDataManager")
            for _, slot in ipairs(slots) do
                if slot.targetEquipUuid ~= "" then
                    local _, gear = invoke(manager,"GetEquipByUuid",tonumber(slot.targetEquipUuid))
                    local wearer = exact_id(member(gear,"heroUuid"))
                    if wearer and not heroes[wearer] then
                        return blocked("Saved equipment is worn by a hero outside this preset")
                    end
                end
            end
        end
        for _, slot in ipairs(slots) do
            if slot.currentEquipUuid ~= slot.targetEquipUuid and slot.currentEquipUuid ~= "" then
                steps[#steps+1]={squadIndex=slot.squadIndex,position=slot.position,
                    heroUuid=slot.heroUuid,slot=slot.slot,before=slot.currentEquipUuid,after=""}
            end
        end
        for _, slot in ipairs(slots) do
            if slot.currentEquipUuid ~= slot.targetEquipUuid and slot.targetEquipUuid ~= "" then
                steps[#steps+1]={squadIndex=slot.squadIndex,position=slot.position,
                    heroUuid=slot.heroUuid,slot=slot.slot,before="",after=slot.targetEquipUuid}
            end
        end
        transaction={key=key,fingerprint=revision,steps=steps,index=1,completed=0,
            sent=0,before=slots,latest=slots,slot=fields.slot,presetId=fields.presetId}
    end
    local tx=transaction
    tx.latest=slots
    if tx.waiting then
        local observed, reason=observe_step(tx.steps[tx.index])
        if observed==false then return blocked(reason) end
        if observed==nil then return receipt(tx,false) end
        tx.waiting=false;tx.completed=tx.completed+1;tx.index=tx.index+1
    end
    local step=tx.steps[tx.index]
    if not step then
        if audit.data.changedSlots ~= 0 then return blocked("Final equipment slots disagree with saved preset") end
        local result=receipt(tx,true);transaction=nil;return result
    end
    local hero, reason=read_hero(step.squadIndex,step.position)
    if not hero or hero.uuid ~= step.heroUuid then return blocked(reason or "Native hero changed") end
    local ok_current,current=invoke(hero.object,"GetEquipBySlotType",step.slot)
    if not ok_current or (current and exact_id(member(current,"uuid")) or "") ~= step.before then
        return blocked("Native equipment slot changed before dispatch")
    end
    if step.after ~= "" then
        local item, why=inventory_for_target(member(rawget(_G,"DataCenter"),"EquipDataManager"),
            step.after,hero,step.slot)
        if not item or item.wearer then return blocked(why or "Equipment has not been released by its previous hero") end
    end
    -- queryOnly polls are allowed to observe a prior native push even while
    -- the user types. However, when polling advances to the NEXT install or
    -- uninstall, that is a fresh send and must pass live input focus again.
    -- Preserve the transaction at this step so a later safe poll can resume
    -- without retransmitting any already-observed operation.
    local focus_ok,focus_clear=false,false
    if type(game_input_clear)=="function" then
        focus_ok,focus_clear=pcall(game_input_clear)
    end
    if not focus_ok or focus_clear~=true then
        local pending=receipt(tx,false)
        pending.data.awaitingInputClear=true
        pending.data.nextStepNotDispatched=true
        return pending
    end
    if not action_owner(fields) then
        return blocked("Native equipment owner/server changed before next step dispatch")
    end
    -- A confirmed owner preset may be edited/replaced between the initial
    -- preflight and the native focus callback. Revalidate the *saved* complete
    -- target for every planned hero/slot before sending this next step, not
    -- just the already-read gear UUID in an old audit table.
    local fresh_preset,preset_error=selected_preset(fields,confirmed_presets)
    if not fresh_preset then return preset_error end
    local refreshed,count,_,saved_heroes=validate_selected_loadout(fresh_preset)
    if not refreshed or count~=#tx.before/4 then
        return blocked("Confirmed equipment preset shape changed before dispatch")
    end
    for _,wanted_slot in ipairs(tx.before) do
        local group=refreshed[wanted_slot.squadIndex]
        local target=group and group[wanted_slot.position]
        local desired=target and (target[wanted_slot.slot] or "") or nil
        if desired~=wanted_slot.targetEquipUuid then
            return blocked("Confirmed equipment preset targets changed before dispatch")
        end
        local native_hero=saved_heroes[wanted_slot.squadIndex][wanted_slot.position]
        if native_hero and native_hero~=wanted_slot.heroUuid then
            return blocked("Confirmed equipment saved hero changed before dispatch")
        end
    end
    -- The focus callback and prior reads are not a lock. Rebind the exact
    -- saved hero, current slot and intended gear *after* focus evaluation,
    -- using the native instance actually passed to the v26 serializer.
    local fresh_hero,fresh_reason=read_hero(step.squadIndex,step.position)
    if not fresh_hero or fresh_hero.uuid~=step.heroUuid then
        return blocked(fresh_reason or "Native equipment hero changed immediately before dispatch")
    end
    local fresh_ok,fresh_current=invoke(fresh_hero.object,"GetEquipBySlotType",step.slot)
    local fresh_uuid=fresh_current and exact_id(member(fresh_current,"uuid")) or ""
    if not fresh_ok or fresh_uuid~=step.before then
        return blocked("Native equipment slot changed immediately before dispatch")
    end
    local next_item
    if step.after~="" then
        local checked,why=inventory_for_target(
            member(rawget(_G,"DataCenter"),"EquipDataManager"),step.after,
            fresh_hero,step.slot)
        if not checked or checked.wearer then
            return blocked("Native equipment target changed immediately before dispatch: " ..
                (why or "gear already has a wearer"))
        end
        next_item=checked.item
    end
    local message=member(rawget(_G,"MsgDefines"),step.after=="" and "HeroEquipUninstall" or "HeroEquipInstall")
    local send=member(rawget(_G,"SFSNetwork"),"SendMessage")
    if message==nil or type(send)~="function" then return unavailable("Original HeroEquip message unavailable") end
    -- Tombstone before the call; an exception can occur after a native send.
    tx.waiting=true;tx.sent=tx.sent+1
    local ok
    if step.after=="" then ok=pcall(send,message,member(fresh_hero.object,"uuid"),{step.slot})
    else
        ok=pcall(send,message,member(fresh_hero.object,"uuid"),
            {[step.slot]=member(next_item,"uuid")})
    end
    if not ok then return {state="failed",errorCode="GAME_ACTION_UNCONFIRMED",
        error="Equipment send outcome unknown; never retransmit this step",data=receipt(tx,false).data} end
    return receipt(tx,false)
end

return M
