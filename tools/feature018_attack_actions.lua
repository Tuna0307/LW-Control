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
--     InitData root/2 lines 31-46 receives targetType, targetPoint,
--     targetUuid as arguments; ChangeMarchByType root/26 lines 1199-1355
--     dispatches target-specific MarchUtil.OnAttackArmy/OnAttackOtherCity etc.
--     These do not prove where the CURRENT keyboard cursor points.
--   UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac SHA256
--     7298256e4a5181a335cfd2ce8c89c9454452211b2feab68eac57827e0465af6d
--     InitData root/2 lines 30-145 receives clicked uuid, pointId, type,
--     and uses SceneManager.World.curTouchPoint in a special fallback.
--     UI/UIWorldPoint/View/UIWorldPointView.luac SHA256
--     e49f0a80b37ae2ecc3abf808c4fa2d2ed5eb146f4224cdf1cf348a1f461636ff
--     ReInit root/17 lines 874-1332 reads ctrl.GetPointData/ctrl.pointId.
--     World.curTouchPoint is therefore LAST WORLD TOUCH, not a fresh
--     desktop mouse raycast, and cannot authorise an attack shortcut.
--   Util/MarchUtil.luac TryStartMarch root/12 lines 574-593
--     enforces stamina/CheckFormation, then forwards to SendCreateMarchMessage
--     or StartMarch. No safe way to reconstruct the full UI selection
--     and subsequent authoritative attack march ACK from a hotkey is proven.
--   Net/Msgs/WorldMarchFormationNewMessage.luac SHA256
--     69dec0f3e14286770c728a2b42d8542f8f3b9e0f493bc2bcb75c9cf4f69a8dd9
--     HandleMessage root/1 lines 92-128 forwards a successful response to
--     CS.SceneManager.World:HandleFormationMarch(response). The handler alone
--     does not correlate a hotkey, target, owned formation and poststate.
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
local attacks = {}
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

local function selected_window()
    local ok_ui,ui=invoke(rawget(_G,"UIManager"),"GetInstance")
    if not ok_ui then return nil end
    local found
    for _,key in ipairs({"UIFormationSelectListV2","UIFormationSelectListNew"}) do
        local name=member(rawget(_G,"UIWindowNames"),key)
        local ok_open,opened=invoke(ui,"IsWindowOpen",name)
        local ok_window,window=invoke(ui,"GetWindow",name)
        if name and ok_open and opened==true and ok_window and member(window,"Ctrl") then
            if found then return nil end -- ambiguous simultaneously open targets
            found={ui=ui,window=window,ctrl=member(window,"Ctrl"),name=name}
        end
    end
    return found
end

-- Observation only. Preserve the original static sender, exact arguments,
-- return values and exceptions. Track ONLY exact pending formation/march,
-- target kind and target UUID on the two source-native attack routes.
local observed_send
local function observe_attack_sender()
    local net=rawget(_G,"SFSNetwork")
    local original=member(net,"SendMessage")
    if original==observed_send then return true end
    if type(original)~="function" then return false end
    observed_send=function(route,...)
        local args=table.pack(...)
        local defs=rawget(_G,"MsgDefines")
        local created=route==member(defs,"WorldMarchFormationNew")
        local changed=route==member(defs,"WorldMarchChange")
        local matched={}
        if created or changed then
            for _,transaction in pairs(attacks) do
                local identity=created and transaction.formation or transaction.oldUuid
                if identity and ident(args[1])==identity and args[2]==transaction.kind and
                   ident(args[3])==transaction.targetUuid then
                    transaction.sendCount=(transaction.sendCount or 0)+1
                    transaction.outboundRoute=created and "WorldMarchFormationNew" or "WorldMarchChange"
                    transaction.outboundFormationOrMarch=ident(args[1])
                    transaction.outboundTargetType=args[2]
                    transaction.outboundTargetUuid=ident(args[3])
                    transaction.outboundPath=type(args[4])=="string" and args[4] or nil
                    matched[#matched+1]=transaction
                end
            end
        end
        local returned=table.pack(pcall(original,route,table.unpack(args,1,args.n)))
        for _,transaction in ipairs(matched) do
            transaction.senderReturned=returned[1]
            transaction.senderReturnedFalse=returned[1] and returned[2]==false
            transaction.senderError=not returned[1] and tostring(returned[2]) or nil
        end
        if not returned[1] then error(returned[2],0) end
        return table.unpack(returned,2,returned.n)
    end
    net.SendMessage=observed_send
    return net.SendMessage==observed_send
end

local observed_replies={}
local function observe_attack_replies()
    for _,name in ipairs({"WorldMarchFormationNewMessage","WorldMarchChangeMessage"}) do
        if not observed_replies[name] then
            local ok,class=pcall(require,"Net.Msgs."..name)
            local original=ok and member(class,"HandleMessage") or nil
            if type(original)=="function" then
                local source_route=name=="WorldMarchFormationNewMessage" and
                    "WorldMarchFormationNew" or "WorldMarchChange"
                local wrapper=function(self,response,...)
                    for _,transaction in pairs(attacks) do
                        -- The same session may contain a separate squad recall
                        -- or another attack. A change ACK is not a create ACK.
                        if (transaction.sendCount or 0)>0 and
                           transaction.outboundRoute==source_route then
                            transaction.replyObserved=true
                            transaction.replyMessage=name
                            transaction.replyErrorCode=member(response,"errorCode") and tostring(member(response,"errorCode")) or nil
                            transaction.replyUuid=ident(member(response,"uuid"))
                            transaction.replyOwnerUid=ident(member(response,"ownerUid"))
                            -- Errors may omit owner/formation IDs. Route-only
                            -- observations are diagnostic, never success ACKs.
                            transaction.replyCorrelation="message_route_only"
                        end
                    end
                    return original(self,response,...)
                end
                class.HandleMessage=wrapper
                observed_replies[name]=wrapper
            end
        end
    end
end

local function transaction_view(transaction)
    if not transaction then return nil end
    local view={}
    for _,key in ipairs({"formation","kind","targetUuid","sendCount","outboundRoute",
        "outboundFormationOrMarch","outboundTargetType","outboundTargetUuid","outboundPath",
        "senderReturned","senderReturnedFalse","senderError","replyObserved","replyMessage",
        "replyErrorCode","replyUuid","replyOwnerUid","replyCorrelation"}) do view[key]=transaction[key] end
    return view
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
    if uid ~= "3133546747002289" then
        return nil, failed("Only the authorized server-2289 sub-account may act")
    end
    for _, method in ipairs({"GetSourceServerId","GetSelfServerId","GetCurServerId"}) do
        local ok, server = invoke(player, method)
        if not ok or not (server == 2289 or server == "2289") then
            return nil, failed("Only the authorized server-2289 sub-account may act")
        end
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
        member(player,"uid"),form_number,alliance_id)
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
    local monster_attack = member(kinds,"ATTACK_MONSTER")
    if not ok_target or target == nil or
       not ((city_attack ~= nil and target == city_attack) or
            (army_attack ~= nil and target == army_attack) or
            (monster_attack ~= nil and target == monster_attack)) then
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
            targetType=target,native=fresh}, nil
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

-- Native read-only attack preflight. This intentionally observes an existing
-- game world last TOUCH and one owned formation, without treating an earlier
-- touch as the current physical cursor or resolving target-specific battle
-- permissions. It never sends or opens windows. The preflight can be used to
-- diagnose the unresolved Q/W/E/R target contract.
local function attack_preflight(fields, input_clear, uid)
    local slot = fields.slot
    if type(slot) ~= "string" or not slot:match("^[1-4]$") then
        return failed("Attack requires fixed formation slot 1 to 4")
    end
    local player = member(rawget(_G, "LuaEntry"), "Player")
    local ok_server, current_server = invoke(player, "GetCurServerId")
    local server_id = ident(current_server)
    if not ok_server or server_id == nil then
        return unsupported("Current native player server identity unavailable")
    end
    local center = rawget(_G, "DataCenter")
    local formation_manager = member(center, "ArmyFormationDataManager")
    local ok_formation, formation = invoke(formation_manager,
        "GetOneArmyInfoByIndex", tonumber(slot))
    local formation_id = ident(member(formation, "uuid"))
    if not ok_formation or not formation_id then
        return unsupported("Current formation slot has no native UUID")
    end
    local formation_owner = member(formation, "ownerUid")
    if formation_owner ~= nil and ident(formation_owner) ~= uid then
        return failed("Native formation reports a different owner")
    end
    -- FormationSelectListV2Ctrl.OnAtkClick/ChangeMarchByType explicitly check
    -- the existing march for this owner/formation with the player's allianceId.
    -- Record that status; it cannot imply the formation is launch-ready.
    local marches = member(center, "WorldMarchDataManager")
    local alliance_id = member(player, "allianceId")
    if marches == nil or alliance_id == nil then
        return unsupported("Native owner/alliance formation march reader unavailable")
    end
    local ok_existing, existing = invoke(marches, "GetOwnerFormationMarch",
        member(player,"uid"), tonumber(formation_id), alliance_id)
    if not ok_existing then
        return unsupported("Native fixed-formation current march lookup failed")
    end
    local existing_march_id = nil
    local march_broken = nil
    if existing ~= nil then
        local native_march_number
        existing_march_id, native_march_number = ident(member(existing, "uuid"))
        if existing_march_id == nil or
           ident(member(existing, "ownerUid")) ~= uid or
           ident(member(existing, "ownerFormationUuid")) ~= formation_id then
            return failed("Existing native squad march belongs to another owner/formation")
        end
        -- A selected-march lookup may be stale. Independently obtain the
        -- authoritative UUID and reject drift before advertising ANY owned
        -- march in the read-only target diagnostic. This is not an attack ACK.
        local ok_fresh, fresh = invoke(marches, "GetMarch", native_march_number)
        if not ok_fresh or fresh == nil or
           ident(member(fresh, "uuid")) ~= existing_march_id or
           ident(member(fresh, "ownerUid")) ~= uid or
           ident(member(fresh, "ownerFormationUuid")) ~= formation_id then
            return unsupported("Existing squad march fails fresh native UUID/owner/formation lookup")
        end
        local ok_broken, is_broken = invoke(fresh, "GetIsBroken")
        if ok_broken and type(is_broken) == "boolean" then
            march_broken = is_broken
        end
    end
    local cs = rawget(_G, "CS")
    local scene_manager = member(cs, "SceneManager")
    local world = member(scene_manager, "World")
    local last_touch = member(world, "curTouchPoint")
    local x, z = number(member(last_touch, "x")), number(member(last_touch, "z"))
    if not world or not x or not z then
        return unsupported("No original native world last-touch location available")
    end
    local scene_utils = rawget(_G, "SceneUtils")
    local tile_from_world = member(scene_utils, "WorldToTileIndex")
    local world_kind = member(rawget(_G, "ForceChangeScene"), "World")
    if type(tile_from_world) ~= "function" or world_kind == nil then
        return unsupported("Original world position-to-tile transform unavailable")
    end
    local ok_tile, tile_value = pcall(tile_from_world, last_touch, world_kind)
    local touched_id = ok_tile and ident(tile_value) or nil
    if not touched_id then
        return unsupported("Native world last-touch location has no valid tile index")
    end
    if fields.pointIndex ~= nil then
        if type(fields.pointIndex) ~= "string" or
           fields.pointIndex ~= touched_id then
            return failed("Requested world point disagrees with native last-touch tile")
        end
    end
    local ok_point, point = invoke(world, "GetPointInfo", tonumber(touched_id))
    if not ok_point or point == nil then
        return unsupported("Last-touched world tile has no authoritative point object")
    end
    local point_uuid = ident(member(point, "uuid"))
    local point_main = ident(member(point, "mainIndex"))
    local point_server = ident(member(point, "serverId"))
    if point_server ~= nil and point_server ~= server_id then
        return failed("Native last-touched point belongs to another server")
    end
    local point_owner = ident(member(point, "ownerUid"))
    if point_owner ~= nil and point_owner == uid then
        return failed("Native last-touched point belongs to own account")
    end
    local verified_lookup = false
    if point_uuid then
        local ok_by_uuid, by_uuid = invoke(world, "GetPointInfoByUuid",
            tonumber(point_uuid))
        if not ok_by_uuid or by_uuid == nil or
           ident(member(by_uuid, "uuid")) ~= point_uuid then
            return unsupported("Last-touched object UUID fails native reverse lookup")
        end
        local reverse_main = ident(member(by_uuid, "mainIndex"))
        local reverse_owner = ident(member(by_uuid, "ownerUid"))
        local reverse_server = ident(member(by_uuid, "serverId"))
        if (point_main and reverse_main and point_main ~= reverse_main) or
           (point_owner and reverse_owner and point_owner ~= reverse_owner) or
           (point_server and reverse_server and point_server ~= reverse_server) then
            return unsupported("Last-touched point disagrees with fresh UUID reverse lookup")
        end
        verified_lookup = true
    end
    return {state="confirmed", data={
        observation="native_world_last_touch_and_formation_preflight",
        slot=slot, formationUuid=formation_id, ownerUid=uid,
        existingOwnedMarchUuid=existing_march_id,
        existingOwnedMarchFreshlyVerified=existing_march_id ~= nil,
        existingOwnedMarchBroken=march_broken,
        currentServerId=server_id, touchedTileIndex=touched_id,
        worldPointUuid=point_uuid, worldPointMainIndex=point_main,
        worldPointOwnerUid=point_owner, worldPointServerId=point_server,
        worldObjectReverseLookupVerified=verified_lookup,
        targetSource="SceneManager.World.curTouchPoint_last_touch",
        currentCursorVerified=false, freshTouchVerified=false,
        attackTargetKindVerified=false, attackAllowedVerified=false,
        safeToDispatch=false, requestDispatched=false
    }}
end

function M.AttackTargetPreflight(fields, input_clear)
    local uid, error = owner_and_focus(fields, input_clear, true)
    if not uid then return error end
    return attack_preflight(fields, input_clear, uid)
end

function M.Prepare(fields, input_clear)
    local uid, denial = owner_and_focus(fields,input_clear,fields.mode == "inventory")
    if not uid then return denial end
    if fields.mode ~= "inventory" and fields.mode ~= "open" then return failed("Invalid native attack prepare mode") end
    local center = rawget(_G,"DataCenter")
    local manager = member(center,"WorldMarchDataManager")
    local world = member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local ok_all, all = invoke(manager,"GetAllMarches")
    local candidates, squads, owned_marches = {}, {}, {}
    local function candidate(raw)
        local value = member(raw,"Value") or raw
        local uuid, n = ident(member(value,"uuid"))
        if uuid and ident(member(value,"ownerUid"))==uid then
            owned_marches[uuid]={uuid=uuid,ownerUid=uid,
                formationUuid=ident(member(value,"ownerFormationUuid")),
                targetUuid=ident(member(value,"targetUuid")),targetType=member(value,"target"),
                status=member(value,"status"),startTime=number(member(value,"startTime")),
                endTime=number(member(value,"endTime"))}
        end
        local monster_id = integer(member(value,"monsterId"))
        if not uuid or not monster_id then return end
        local ok_template, template = invoke(member(center,"MonsterTemplateManager"),"GetMonsterTemplate",monster_id)
        local level,special = number(member(template,"level")),number(member(template,"special"))
        if not ok_template or not level or special ~= 0 then return end
        local ok_fresh,fresh = invoke(world,"GetMarch",n)
        local ok_troop,troop = invoke(world,"GetTroop",n)
        local ok_pos,pos = invoke(troop,"GetPosition")
        local transform = member(rawget(_G,"SceneUtils"),"WorldToTileIndex")
        local ok_tile,tile = false,nil
        -- v26 WorldToTileIndex takes the world position AND the target scene.
        -- Omitting ForceChangeScene.World silently drops otherwise valid targets.
        local world_scene=member(rawget(_G,"ForceChangeScene"),"World")
        if ok_pos and type(transform)=="function" and world_scene~=nil then
            ok_tile,tile=pcall(transform,pos,world_scene)
        end
        if not ok_fresh or not ok_troop or ident(member(fresh,"uuid")) ~= uuid or
           not ok_tile or not integer(tile) then return end
        candidates[#candidates+1]={uuid=uuid,monsterId=monster_id,level=level,
            pointIndex=tile,ownerUid=ident(member(fresh,"ownerUid"))}
    end
    if ok_all and type(all) == "table" then
        for _,value in pairs(all) do if #candidates < 100 then candidate(value) end end
    elseif ok_all and all ~= nil then
        local ok_enum,enumerator=invoke(all,"GetEnumerator")
        if ok_enum then
            for _=1,10000 do
                local ok_next,next_=invoke(enumerator,"MoveNext")
                if not ok_next or next_ ~= true or #candidates >= 100 then break end
                candidate(member(enumerator,"Current"))
            end
        end
    end
    for slot=1,4 do
        local ok_form,form=invoke(member(center,"ArmyFormationDataManager"),"GetOneArmyInfoByIndex",slot)
        local uuid,n=ident(member(form,"uuid"))
        local lookup_ok,march=invoke(manager,"GetOwnerFormationMarch",member(member(rawget(_G,"LuaEntry"),"Player"),"uid"),n,
            member(member(rawget(_G,"LuaEntry"),"Player"),"allianceId"))
        local heroes=member(form,"heroes")
        local hero_count=type(heroes)=="table" and 0 or nil
        if hero_count then for _ in pairs(heroes) do hero_count=hero_count+1 end end
        local soldiers=member(form,"soldiers")
        local soldier_count=type(soldiers)=="table" and 0 or nil
        if soldier_count then for _,value in pairs(soldiers) do
            local amount=number(value)
            if amount and amount>0 then soldier_count=soldier_count+amount end
        end end
        local _,local_heroes=invoke(form,"GetLocalHeroesCount")
        local _,stamina=invoke(member(center,"ArmyFormationDataManager"),"GetCurStaminaByUuid",n)
        squads[tostring(slot)]={unlocked=ok_form and uuid ~= nil,formationUuid=uuid,
            marchUuid=ident(member(march,"uuid")),targetType=member(march,"target"),
            endTime=number(member(march,"endTime")),lookupSucceeded=lookup_ok,
            formationState=number(member(form,"state")),formationIndex=number(member(form,"index")),
            heroCount=hero_count,localHeroCount=number(local_heroes),soldierCount=soldier_count,
            stamina=number(stamina),maxNum=number(member(form,"maxNum")),
            pendingTransaction=transaction_view(attacks[tostring(slot)])}
    end
    local selector=selected_window()
    local selector_data
    if selector then
        selector_data={name=selector.name}
        for _,key in ipairs({"formationType","targetType","targetPoint","targetUuid","timeIndex",
            "autoBackHome","selectFormationUuid","currentFormationUuid","targetServerId","defaultMarchIndex"}) do
            local value=member(selector.ctrl,key)
            if type(value)=="number" or type(value)=="string" or type(value)=="boolean" then selector_data[key]=value end
        end
    end
    table.sort(candidates,function(a,b) if a.level~=b.level then return a.level<b.level end return a.uuid<b.uuid end)
    if fields.mode == "open" then
        local selected
        for _,item in ipairs(candidates) do if item.uuid == fields.targetUuid then selected=item end end
        if not selected then return failed("Requested monster is absent from fresh native ordinary-monster candidates") end
        local open=member(rawget(_G,"UIUtil"),"OpenFormationSelectUI")
        local kind=member(rawget(_G,"MarchTargetType"),"ATTACK_MONSTER")
        if type(open)~="function" or not kind then return unsupported("Native formation selector helper unavailable") end
        local ok_open=pcall(open,1,kind,selected.pointIndex,tonumber(selected.uuid),nil,nil,nil,2289,0)
        if not ok_open then return failed("Native selector opening failed") end
        return {state="confirmed",data={observation="native_attack_selector_open_invoked",
            targetUuid=selected.uuid,targetPoint=selected.pointIndex,targetServerId=2289,
            source="UIUtil.OpenFormationSelectUI",marchDispatched=false}}
    end
    return {state="confirmed",data={observation="native_ordinary_monsters_and_owned_squads",
        candidates=candidates,squads=squads,ownedMarches=owned_marches,
        currentSelector=selector_data,playerUidNativeType=type(member(member(rawget(_G,"LuaEntry"),"Player"),"uid")),
        requestDispatched=false}}
end

-- Diamond template metadata is also read-only: the original CellsCallBack
-- uses GetTokenIdAndType, CurrencyUtil.GetCurrencyCount and the native
-- ShowUseDiamondConfirm dialog. A persisted diamond preference or typed
-- opt-in cannot replace the game's interactive confirmation callback.
function M.DiamondSpeedupPreflight(fields, input_clear)
    local uid, error = owner_and_focus(fields, input_clear, true)
    if not uid then return error end
    local march, march_error = slot_march(fields.slot, uid)
    if not march then return march_error end
    local center = rawget(_G, "DataCenter")
    local template_manager = member(center, "ItemTemplateManager")
    local category = member(rawget(_G, "GOODS_TYPE"), "GOODS_TYPE_108")
    local ok_templates, templates = invoke(template_manager, "GetTypeListByType", category)
    if category == nil or not ok_templates or type(templates) ~= "table" then
        return unsupported("Native rapid-speedup purchase templates unavailable")
    end
    local options = {}
    for _, template in pairs(templates) do
        local key = ident(member(template, "id"))
        local price = number(member(template, "price"))
        if key and price and price > 0 then
            options[#options+1] = {itemId=key, listedPrice=price}
        end
    end
    table.sort(options, function(a,b) return tonumber(a.itemId) < tonumber(b.itemId) end)
    return {state="confirmed", data={
        observation="native_rapid_purchase_template_metadata",
        marchUuid=march.uuid, slot=march.slot, options=options,
        priceCurrencyVerified=false, nativeDiamondDialogCompleted=false,
        safeToDispatch=false, requestDispatched=false
    }}
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

-- Native ordinary-world rapid purchase: UILWRapidView.GetTokenIdAndType
-- returns token=0 outside battlefields; CellsCallBack uses template.price and
-- Player.gold, ConfirmBuy sends WorldMarchRapid(uuid,template.id,true).
-- App consent replaces the native purchase prompt, but never its currency
-- or balance checks. Unlike native ConfirmBuy this does NOT UpdateGold or
-- ReduceTime optimistically: BOTH independent game fields must change.
local diamond_pending = {}
local function speedup_diamond(fields, input_clear, uid)
    local march, reason = slot_march(fields.slot,uid)
    if not march then return reason end
    local ok_world, world_type = invoke(march.native,"GetWorldType")
    if not ok_world or world_type ~= 0 then return failed("Diamond rapid supports ordinary world marches only") end
    local gold = number(member(member(rawget(_G,"LuaEntry"),"Player"),"gold"))
    if not gold or gold < 0 then return failed("Current native gem balance unavailable") end
    local old = diamond_pending[fields.slot]
    if old then
        if old.uid ~= uid or old.uuid ~= march.uuid or old.formation ~= march.formation then
            return failed("Pending diamond rapid march identity changed")
        end
        if march.endTime < old.endTime and gold == old.gold-old.price then
            diamond_pending[fields.slot] = nil
            return {state="confirmed",data={observation="native_march_endTime_and_gem_balance_transition",
                slot=tonumber(fields.slot),marchUuid=march.uuid,itemId=old.itemId,
                priorEndTime=old.endTime,currentEndTime=march.endTime,
                priorGold=old.gold,currentGold=gold,price=old.price}}
        end
        return {state="accepted",data={slot=tonumber(fields.slot),marchUuid=march.uuid,
            requestDispatched=false,awaitingGameConfirmation=true}}
    end
    if fields.queryOnly == "1" then return failed("No pending diamond rapid purchase to observe") end
    if fields.diamondOptIn ~= "1" then return failed("Explicit gem purchase consent absent") end
    local template_manager = member(rawget(_G,"DataCenter"),"ItemTemplateManager")
    local category = member(rawget(_G,"GOODS_TYPE"),"GOODS_TYPE_108")
    local ok_templates, templates = invoke(template_manager,"GetTypeListByType",category)
    local selected
    if ok_templates and type(templates) == "table" and type(fields.itemId) == "string" then
        for _,template in pairs(templates) do
            if ident(member(template,"id")) == fields.itemId then selected=template end
        end
    end
    local price = number(member(selected,"price"))
    if not price or price <= 0 or price % 1 ~= 0 or gold < price then
        return failed("Exact native category-108 purchase template/price or sufficient gems absent")
    end
    local route = member(rawget(_G,"MsgDefines"),"WorldMarchRapid")
    local send = member(rawget(_G,"SFSNetwork"),"SendMessage")
    if not route or type(send) ~= "function" then return unsupported("Original WorldMarchRapid sender unavailable") end
    if input_clear() ~= true then return failed("Text focus changed before gem purchase") end
    diamond_pending[fields.slot] = {uid=uid,uuid=march.uuid,formation=march.formation,
        itemId=fields.itemId,price=price,gold=gold,endTime=march.endTime}
    local ok,result = pcall(send,route,march.uuidNumber,tonumber(fields.itemId),true)
    if not ok or result == false then return failed("Native gem rapid sender rejected request; observe before retry") end
    return {state="accepted",data={slot=tonumber(fields.slot),marchUuid=march.uuid,
        itemId=fields.itemId,price=price,priorGold=gold,requestDispatched=true,
        awaitingGameConfirmation=true}}
end

-- Adaptation: the CURRENT open native formation selector owns the typed
-- target. This is deliberately distinct from the original physical-pointer
-- shortcut. No last-touch or caller-supplied target can dispatch a march.
-- v26 UIManager.GetWindow -> UIWindow.Ctrl; OnCreateClick(root/24) and
-- OnAtkClick(root/22) preserve all original battle/warning/formation checks.
local function attack_selected(fields, input_clear, uid)
    if type(fields.slot) ~= "string" or not fields.slot:match("^[1-4]$") then
        return failed("Attack requires fixed formation slot 1 to 4")
    end
    local player = member(rawget(_G,"LuaEntry"),"Player")
    local center = rawget(_G,"DataCenter")
    local forms = member(center,"ArmyFormationDataManager")
    local marches = member(center,"WorldMarchDataManager")
    local ok_form, form = invoke(forms,"GetOneArmyInfoByIndex",tonumber(fields.slot))
    local formation, form_number = ident(member(form,"uuid"))
    if not ok_form or not formation then return failed("Selected squad is not unlocked") end
    local ok_existing, existing = invoke(marches,"GetOwnerFormationMarch",
        member(player,"uid"),form_number,member(player,"allianceId"))
    if not ok_existing then return failed("Native formation march lookup failed") end
    local function fresh_owned(candidate)
        local uuid, uuid_number = ident(member(candidate,"uuid"))
        if not uuid then return nil end
        local ok, fresh = invoke(marches,"GetMarch",uuid_number)
        if not ok or ident(member(fresh,"uuid")) ~= uuid or
           ident(member(fresh,"ownerUid")) ~= uid or
           ident(member(fresh,"ownerFormationUuid")) ~= formation then return nil end
        return fresh, uuid
    end
    local prior = attacks[fields.slot]
    if prior then
        if prior.uid ~= uid or prior.formation ~= formation or
           prior.session ~= current_session then return failed("Pending attack session/formation changed") end
        if prior.senderReturnedFalse or prior.senderError or
           prior.replyErrorCode or (prior.sendCount or 0)>1 then
            local result=failed("Native attack sender or route may have failed; observe retained transaction before retry")
            result.data={nativeTransaction=transaction_view(prior)}
            return result
        end
        if prior.receipt then
            local copy={}
            for k,v in pairs(fields) do copy[k]=v end
            copy.action,copy.itemId=prior.rapidAction,prior.rapidItem
            copy.itemOptIn,copy.diamondOptIn="1","1"
            copy.queryOnly=prior.rapidSent and "1" or nil
            local result
            if prior.rapidAction == "speedupItem" then result=speedup_item(copy,input_clear,uid)
            else result=speedup_diamond(copy,input_clear,uid) end
            if result.state == "confirmed" then
                prior.receipt.speedup=result.data
                attacks[fields.slot]=nil
                return {state="confirmed",data=prior.receipt}
            end
            if result.state == "accepted" then
                prior.rapidSent=true
                return {state="accepted",data={slot=tonumber(fields.slot),formationUuid=formation,
                    awaitingGameConfirmation=true,requestDispatched=false}}
            end
            -- The attack happened, but requested speedup did not confirm.
            -- Retain the fence so another key edge cannot double-spend.
            return failed("Attack confirmed; requested rapid remains unconfirmed: "..tostring(result.error))
        end
        local fresh, uuid = fresh_owned(existing)
        local ok_type, kind = invoke(fresh,"GetMarchTargetType")
        local start_time = number(member(fresh,"startTime"))
        if fresh and ok_type and kind == prior.kind and prior.sendCount==1 and
           prior.senderReturned==true and prior.senderReturnedFalse~=true and
           ident(member(fresh,"targetUuid")) == prior.targetUuid and
           (not prior.oldUuid or uuid ~= prior.oldUuid or
            start_time ~= prior.oldStartTime or kind ~= prior.oldKind) then
            local receipt={
                observation="native_owned_formation_attack_target_transition",
                slot=tonumber(fields.slot),marchUuid=uuid,formationUuid=formation,
                targetUuid=prior.targetUuid,targetType=kind,
                targetSource="open_native_formation_selector",currentCursorVerified=false,
                priorMarchUuid=prior.oldUuid,priorStartTime=prior.oldStartTime,
                currentStartTime=start_time,requestDispatched=false,
                transaction=transaction_view(prior)}
            if prior.allowItem or prior.allowDiamond then
                local options=owned_speedup_items()
                local chosen
                if prior.allowItem and options then
                    for id in pairs(options) do
                        if not chosen or tonumber(id)<tonumber(chosen) then chosen=id end
                    end
                end
                if chosen then prior.rapidAction,prior.rapidItem="speedupItem",chosen
                elseif prior.allowDiamond then
                    local _,templates=invoke(member(center,"ItemTemplateManager"),"GetTypeListByType",
                        member(rawget(_G,"GOODS_TYPE"),"GOODS_TYPE_108"))
                    local price
                    if type(templates)=="table" then
                        for _,template in pairs(templates) do
                            local id=ident(member(template,"id"))
                            local cost=number(member(template,"price"))
                            if id and cost and cost>0 and (not price or cost<price) then
                                chosen,price=id,cost
                            end
                        end
                    end
                    if chosen then prior.rapidAction,prior.rapidItem="speedupDiamond",chosen end
                end
                if not prior.rapidAction then return failed("Attack confirmed; no permitted native rapid item or purchase template") end
                prior.receipt=receipt
                return attack_selected(fields,input_clear,uid)
            end
            attacks[fields.slot] = nil
            return {state="confirmed",data=receipt}
        end
        return {state="accepted",data={slot=tonumber(fields.slot),formationUuid=formation,
            awaitingGameConfirmation=true,requestDispatched=false,
            nativeTransaction=transaction_view(prior)}}
    end
    if fields.queryOnly == "1" then return failed("No pending native attack to observe") end
    local selector=selected_window()
    local window=selector and selector.window
    local ctrl=selector and selector.ctrl
    local target_uuid, target_number = ident(member(ctrl,"targetUuid"))
    local kind = member(ctrl,"targetType")
    local kinds = rawget(_G,"MarchTargetType")
    local is_city = kind ~= nil and kind == member(kinds,"ATTACK_CITY")
    local is_army = kind ~= nil and kind == member(kinds,"ATTACK_ARMY")
    local is_monster = kind ~= nil and kind == member(kinds,"ATTACK_MONSTER")
    if not selector or not ctrl or not target_uuid or
       not (is_city or is_army or is_monster) then
        return failed("Open the native attack formation selector on a current city/army/monster target first")
    end
    local world = member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local ok_target, target
    if is_city then
        ok_target,target = invoke(world,"GetPointInfo",tonumber(member(ctrl,"targetPoint")))
    else
        ok_target,target = invoke(world,"GetMarch",target_number)
    end
    if not ok_target or ident(member(target,"uuid")) ~= target_uuid or
       ident(member(target,"ownerUid")) == uid then
        return failed("Current selector target is absent, changed or owned by this account")
    end
    local old, old_uuid
    if existing ~= nil then
        old,old_uuid = fresh_owned(existing)
        if not old then return failed("Existing squad march owner/formation changed") end
    end
    local _,old_kind = invoke(old,"GetMarchTargetType")
    local old_start = number(member(old,"startTime"))
    local method = existing ~= nil and "OnAtkClick" or "OnCheckTime"
    if type(member(ctrl,method)) ~= "function" then return unsupported("Original native attack controller missing") end
    if input_clear() ~= true or member(window,"Ctrl") ~= ctrl or
       member(ctrl,"targetType") ~= kind or ident(member(ctrl,"targetUuid")) ~= target_uuid then
        return failed("Native selector changed immediately before attack")
    end
    if not observe_attack_sender() then return unsupported("Native attack sender observation unavailable") end
    observe_attack_replies()
    attacks[fields.slot] = {session=current_session,uid=uid,formation=formation,
        kind=kind,targetUuid=target_uuid,oldUuid=old_uuid,oldKind=old_kind,
        oldStartTime=old_start,sentAt=os.time(),allowItem=fields.allowRapidItem=="1",
        allowDiamond=fields.allowRapidDiamond=="1",sendCount=0}
    local ok, invocation_result = invoke(ctrl,method,form_number)
    local transaction=attacks[fields.slot]
    if not ok or invocation_result==false or transaction.senderError or
       transaction.senderReturnedFalse or transaction.sendCount>1 then
        -- A thrown controller, a false sender result or duplicate observation
        -- may still have changed native state. Keep the fence and allow only
        -- independent observation; never authorize an automatic second send.
        local result=failed("Original native attack controller or sender failed; observe retained transaction before retry")
        result.data={nativeTransaction=transaction_view(transaction)}
        return result
    end
    return {state="accepted",data={slot=tonumber(fields.slot),formationUuid=formation,
        targetUuid=target_uuid,targetType=kind,requestDispatched=false,accepted=true,
        nativeControllerInvoked=true,
        nativeTransaction=transaction_view(attacks[fields.slot]),
        awaitingGameConfirmation=true,targetSource="open_native_formation_selector",
        currentCursorVerified=false}}
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
        return attack_selected(fields,input_clear,uid)
    end
    if fields.action == "speedupDiamond" then
        return speedup_diamond(fields,input_clear,uid)
    end
    return speedup_item(fields,input_clear,uid)
end

return M
