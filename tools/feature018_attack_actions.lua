-- FEATURE-RECOVERY-018 isolated v26 attack / item-march-rapid adapter.
--
-- Original, installed current 3/26 bytecode (offline, game package untouched):
--   Util/MarchUtil.luac SHA256 d3c3bb47b11e555ccd646510628c2e428000436ffa8bc8bac290a1320094e245
--     OnAttackOtherCity root/24 lines 767-789 uses world GetPointInfo
--     and TryStartMarch; OnAttackArmy root/13 lines 595-608 uses GetTroop.
--     Neither supplies a keyboard cursor-selection or slot-specific target.
--   UI/UIFormation/UIFormationSelectListV2/Controller/UIFormationSelectListV2Ctrl.luac
--     SHA256 25db92ba9a39791ff8fb500e2b00034f894dc5cf1d98200454e582bb24daa84c
--     OnAtkClick root/22 lines 715-779 consumes the controller's targetType
--     and targetPoint and performs special target-specific checks; SetTargetPoint
--     root/33 lines 1860-1862 simply stores an already selected target.
--     These do not prove where the game cursor points or which target is there.
--   UI/UILWRapid/View/UILWRapidView.luac SHA256
--     19bd32d6cedc24586089e19e12c070b3b73fac7e170dd4917ca8041a11720da3
--     ReInit root/11 lines 307-331 reads WorldMarchDataManager.GetMarch(uuid)
--     and its endTime; GetAllItems root/27 lines 641-667 enumerates
--     ItemData.GetItemsByType(GOODS_TYPE_108), ItemTemplateManager.GetTypeListByType.
--     CellsCallBack root/20 lines 469-489 selects a possessed item.
--     ConfirmUse root/21 lines 493-527 sends
--     SFSNetwork.SendMessage(MsgDefines.WorldMarchRapid, uuid, itemId, false),
--     then OPTIMISTICALLY reduces UI time/inventory without server success.
--     ConfirmBuy root/22 lines 529-546 instead sends (uuid, template.id, true);
--     CellsCallBack root/20 lines 441-459 demands native diamond confirmation.
--   Net/Msgs/WorldMarchRapidMessage.luac SHA256
--     1ba6497b62d041729d2e7b5a7dd907e5067fec8b92893e5e63ecd9cc2e00a8f5
--     HandleMessage root/1 lines 17-28 checks errorCode and remainGold,
--     but supplies no per-march success receipt usable by this adapter.
--
-- Registration: prime's authenticated Hotkey handler should call
-- M.Handle(fields, frozenHotkey.TextInputClear), for actions "attack",
-- "speedupItem" and "speedupDiamond". "speedupItem" must be a *separate*
-- opt-in request for an already-existing current user's slot march, not
-- assumed part of any source-unsupported cursor attack. No action on require.

local M = { VERSION = "feature018-attack-actions-1" }
local pending, current_session = {}, nil
local PENDING_LIMIT = 45

local function member(object, key)
    if object == nil then return nil end
    local ok, result = pcall(function() return object[key] end)
    return ok and result or nil
end

local function invoke(object, method, ...)
    local fn = member(object, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, result = pcall(fn, object, ...)
    return ok, result
end

local function failed(message)
    return {state="failed",errorCode="GAME_PRECONDITION_FAILED",error=message}
end

local function unsupported(message)
    return {state="unsupported",errorCode="GAME_METHOD_UNAVAILABLE",error=message}
end

local function integer(value)
    if type(value) == "boolean" or value == nil then return nil end
    local n = tonumber(value)
    if not n or n ~= n or n == math.huge or n == -math.huge or
       n < 1 or n % 1 ~= 0 then return nil end
    -- Lua 5.3 integers can represent full signed 64-bit game UUIDs. Float
    -- numbers cannot safely identify UUIDs above 2^53.
    if math.type and math.type(n) == "float" and n > 9007199254740991 then
        return nil
    end
    return n
end

local function ident(value)
    local n = integer(value)
    if not n then return nil, nil end
    return tostring(n), n
end

local function number(value)
    local n = tonumber(value)
    if n ~= nil and n == n and n ~= math.huge and n ~= -math.huge then return n end
    return nil
end

local function exact_session(fields)
    if type(fields) ~= "table" then return nil end
    local parts = {}
    for _, key in ipairs({"profileId","launchSessionId","challenge","gamePid"}) do
        local part = fields[key]
        if type(part) ~= "string" or part == "" or #part > 128 or
           part:find("[^%w_-]") then return nil end
        parts[#parts+1] = part
    end
    return table.concat(parts,"/")
end

local function owner_and_focus(fields, input_clear, read_only)
    local session = exact_session(fields)
    if not session then return nil, failed("Exact bridge game-session identity absent") end
    local player = member(rawget(_G,"LuaEntry"),"Player")
    local uid = ident(member(player,"uid"))
    if not uid or type(fields.authorizedUid) ~= "string" or
       fields.authorizedUid ~= uid then
        return nil, failed("Fresh game UID must match admitted owner-session UID")
    end
    if not read_only and
       (type(input_clear) ~= "function" or input_clear() ~= true) then
        return nil, failed("Current-game text-input focus unavailable or selected")
    end
    -- A reconnect must not silently discard an unacknowledged spend. A new
    -- session can observe its previous poststate, but cannot immediately
    -- double-send the same slot while that outcome remains uncertain.
    current_session = session
    return uid, nil
end

local function slot_march(slot, uid)
    if type(slot) ~= "string" or not slot:match("^[1-4]$") then
        return nil, failed("Expected current squad 1 through 4")
    end
    local center = rawget(_G,"DataCenter")
    local formations = member(center,"ArmyFormationDataManager")
    local marches = member(center,"WorldMarchDataManager")
    if not formations or not marches then
        return nil, unsupported("Current native formation/march data unavailable")
    end
    local ok_form, form = invoke(formations,"GetOneArmyInfoByIndex",tonumber(slot))
    local form_id, form_number = ident(member(form,"uuid"))
    if not ok_form or not form_id then
        return nil, unsupported("Current squad slot lacks authoritative formation UUID")
    end
    -- Exact v26 FormationSelectListV2Ctrl.OnAtkClick call includes the
    -- authenticated player's allianceId as its third native parameter.
    local player = member(rawget(_G,"LuaEntry"),"Player")
    local alliance_id = member(player,"allianceId")
    if alliance_id == nil then
        return nil, unsupported("Native current-account alliance identity unavailable")
    end
    local ok_match, candidate = invoke(marches,"GetOwnerFormationMarch",
        uid,form_number,alliance_id)
    local march_id, march_number = ident(member(candidate,"uuid"))
    if not ok_match or not march_id or
       ident(member(candidate,"ownerUid")) ~= uid or
       ident(member(candidate,"ownerFormationUuid")) ~= form_id then
        return nil, unsupported("Native selected march owner/formation UUID mismatch")
    end
    local ok_fresh, fresh = invoke(marches,"GetMarch",march_number)
    if not ok_fresh or not fresh or
       ident(member(fresh,"uuid")) ~= march_id or
       ident(member(fresh,"ownerUid")) ~= uid or
       ident(member(fresh,"ownerFormationUuid")) ~= form_id then
        return nil, unsupported("Fresh march UUID/owner/formation mismatch")
    end
    local end_time = number(member(fresh,"endTime"))
    local ok_target, target = invoke(fresh,"GetMarchTargetType")
    local kinds = rawget(_G,"MarchTargetType")
    local city_attack = member(kinds,"ATTACK_CITY")
    local army_attack = member(kinds,"ATTACK_ARMY")
    if not ok_target or target == nil or
       not ((city_attack ~= nil and target == city_attack) or
            (army_attack ~= nil and target == army_attack)) then
        return nil, unsupported("Fresh owned march is not a source-classified city/army attack")
    end
    local clock_type = rawget(_G,"UITimeManager")
    local ok_clock, clock = invoke(clock_type,"GetInstance")
    local ok_now, now_value = invoke(clock,"GetServerTime")
    local now = number(now_value)
    if not ok_clock or not ok_now or not now or not end_time or
       end_time <= now then
        return nil, failed("Source march end-time is absent or already reached")
    end
    return {uuid=march_id,uuidNumber=march_number,formation=form_id,
            uid=uid,slot=slot,endTime=end_time,now=now,
            targetType=target}, nil
end

-- Only original rapid-screen inventory/template producers. No guessed item
-- IDs, localized names, or diamond purchases. The caller must select one
-- specific existing item ID from the returned inventory.
local function owned_speedup_items()
    local center = rawget(_G,"DataCenter")
    local category = member(rawget(_G,"GOODS_TYPE"),"GOODS_TYPE_108")
    local items = member(center,"ItemData")
    local template_manager = member(center,"ItemTemplateManager")
    if category == nil or items == nil or template_manager == nil then
        return nil, unsupported("Native rapid-item category, inventory or templates absent")
    end
    local ok_owned, owned = invoke(items,"GetItemsByType",category)
    local ok_templates, templates = invoke(template_manager,"GetTypeListByType",category)
    if not ok_owned or type(owned) ~= "table" or
       not ok_templates or type(templates) ~= "table" then
        return nil, unsupported("Native category-108 inventory/template snapshot unavailable")
    end
    local by_id, options = {}, {}
    for _, template in pairs(templates) do
        local key, n = ident(member(template,"id"))
        if key then by_id[key] = {id=n,template=template} end
    end
    for _, item in pairs(owned) do
        local key = ident(member(item,"itemId"))
        local count = integer(member(item,"count"))
        local match = key and by_id[key]
        if match and count then
            local _, check = invoke(items,"GetItemById",match.id)
            local check_key = ident(member(check,"itemId"))
            local check_count = integer(member(check,"count"))
            if check_key == key and check_count == count then
                options[key] = {itemId=key,itemIdNumber=match.id,count=count,
                                template=match.template}
            end
        end
    end
    return options, nil
end

function M.OwnedSpeedupInventory(fields, input_clear)
    local uid, error = owner_and_focus(fields,input_clear,true)
    if not uid then return error end
    local march, march_error = slot_march(fields.slot,uid)
    if not march then return march_error end
    local options, items_error = owned_speedup_items()
    if not options then return items_error end
    local view = {}
    for _, item in pairs(options) do
        view[#view+1] = {itemId=item.itemId,count=item.count}
    end
    table.sort(view,function(a,b) return tonumber(a.itemId)<tonumber(b.itemId) end)
    return {state="confirmed",data={observation="native_owned_rapid_item_candidates",
        marchUuid=march.uuid,slot=march.slot,items=view,dispatchPerformed=false}}
end

local function speedup_item(fields, input_clear, uid)
    local march, march_error = slot_march(fields.slot,uid)
    if not march then return march_error end
    local options, options_error = owned_speedup_items()
    if not options then return options_error end
    local previous = pending[fields.slot]
    if previous then
        if previous.uuid ~= march.uuid or previous.formation ~= march.formation or
           previous.uid ~= uid or previous.targetType ~= march.targetType then
            return failed("Earlier speedup march/owner identity changed")
        end
        -- Unlike rapid UI's speculative ReduceTime/UseTool, this adapter
        -- only observes fresh native poststate and never mutates game caches.
        local before_item = options[previous.itemId]
        local current_count = before_item and before_item.count or 0
        if march.endTime < previous.endTime and current_count < previous.count then
            pending[fields.slot] = nil
            return {state="confirmed",data={
                observation="native_march_endTime_and_item_count_transition",
                slot=fields.slot,marchUuid=march.uuid,itemId=previous.itemId,
                priorEndTime=previous.endTime,currentEndTime=march.endTime,
                priorCount=previous.count,currentCount=current_count}}
        end
        if os.time() - previous.sentAt >= PENDING_LIMIT then
            return failed("Speedup poststate unconfirmed; do not retry in this session")
        end
        return {state="accepted",data={requestDispatched=false,
            awaitingGameConfirmation=true,slot=fields.slot,marchUuid=march.uuid}}
    end
    if fields.queryOnly == "1" then
        return failed("No pending item-speedup request to observe")
    end
    if fields.itemOptIn ~= "1" then
        return failed("Item speedup opt-in is absent")
    end
    if type(fields.itemId) ~= "string" or not fields.itemId:match("^%d+$") or
       not options[fields.itemId] then
        return failed("Requested item is not a verified owned rapid-speedup item")
    end
    local item = options[fields.itemId]
    if item.count < 1 then return failed("Speedup item is depleted") end
    local send = member(rawget(_G,"SFSNetwork"),"SendMessage")
    local route = member(rawget(_G,"MsgDefines"),"WorldMarchRapid")
    if type(send) ~= "function" or route == nil then
        return unsupported("Native WorldMarchRapid request route unavailable")
    end
    if input_clear() ~= true then
        return failed("Text focus changed before rapid-item dispatch")
    end
    -- TOCTOU: verify fresh owner/formation/march/time and item count again.
    local final, final_error = slot_march(fields.slot,uid)
    if not final then return final_error end
    local latest, latest_error = owned_speedup_items()
    if not latest then return latest_error end
    local fresh_item = latest[fields.itemId]
    if final.uuid ~= march.uuid or final.formation ~= march.formation or
       final.endTime ~= march.endTime or final.targetType ~= march.targetType or
       not fresh_item or
       fresh_item.count ~= item.count then
        return failed("Owned march/item changed immediately before speedup dispatch")
    end
    -- The v26 method is a static function, NOT a colon call.
    local ok, result = pcall(send,route,final.uuidNumber,fresh_item.itemIdNumber,false)
    if not ok or result == false then
        return failed("Original WorldMarchRapid sender rejected item request")
    end
    pending[fields.slot] = {uid=uid,formation=final.formation,uuid=final.uuid,
        targetType=final.targetType,
        itemId=fields.itemId,count=fresh_item.count,endTime=final.endTime,
        sentAt=os.time()}
    return {state="accepted",data={requestDispatched=true,
        awaitingGameConfirmation=true,slot=fields.slot,marchUuid=final.uuid,
        itemId=fields.itemId}}
end

function M.Handle(fields, input_clear)
    if type(fields) ~= "table" then return failed("Invalid rapid/attack command") end
    if fields.action ~= "attack" and fields.action ~= "speedupItem" and
       fields.action ~= "speedupDiamond" then
        return unsupported("Unknown attack/rapid action")
    end
    -- A status query is observation only. Never let Unity text editing hide
    -- a legitimate poststate receipt or force a second spend to retrieve it.
    local uid, error = owner_and_focus(fields,input_clear,fields.queryOnly == "1")
    if not uid then return error end
    if fields.action == "attack" then
        -- An idle formation may have no world march, which is normal when
        -- attempting a first attack. Never use march absence as a target.
        if type(fields.slot) ~= "string" or
           not fields.slot:match("^[1-4]$") then
            return failed("Attack requires fixed formation slot 1 to 4")
        end
        return unsupported("v26 has target-specific attack delegates, but no proven game cursor target-to-world object and fixed-slot UI selection contract")
    end
    if fields.action == "speedupDiamond" then
        return unsupported("Native rapid diamond purchase requires its own price/currency and ShowUseDiamondConfirm transaction; no safe direct shortcut")
    end
    return speedup_item(fields,input_clear,uid)
end

return M
