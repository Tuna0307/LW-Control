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
--     OnCreate root/0 lines 46-47 serializes the generated
--     World.SaveCreateMarchRecordTime() clientCreateUuid with PutUtfString.
--     GetTestData root/2 contains clientCreateUuid, ownerUid, uuid. The
--     native correlation gate below needs this exact reply token AND fresh
--     formation-target march state; a test fixture is not a live ACK.
--   Net/Msgs/WorldMarchChangeMessage.luac SHA256
--     71453f2d7287ddb1f012e05cea71755f6bc704971a59425b6c54834c96fe8160
--     OnCreate root/0 sends uuid/target/targetUid, no request nonce.
--     HandleMessage root/1 line 90 applies HandleFormationMarchChange.
--     No safe exact same-march request/reply correlation for retargeting
--     or A/S/D/F recall is proved inside this attack adapter.
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
    if ok then return result end
    return nil
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

-- Current v26 ArmyFormationInfo.luac SHA256
-- 869783338b53ea39f7fbeda36884dec97d359fc2b7b18e664891f746aba74d32:
-- ConscriptSoldier root/19 lines454-566 builds soldiers[heroSlot] with
-- numeric soldier-id counts and supply/heroMaxCapacity, then totalSoldierNum.
-- RefreshFormationSoldier root/17 lines332-361 consumes server canMarch and
-- serverSoldiers. V2 BestSelect root/53 line2257 (New root/52) conscripts
-- the current SoldierDataManager inventory inside the original chooser.
-- Empty pre-chooser staffing must not prevent opening that original UI.
-- This reader NEVER calls ConscriptSoldier or changes any formation.
local function formation_staffing(form)
    local result={soldierCount=nil,heroCount=nil,staffedForAttack=false,
        staffingShape="unavailable",canMarch=member(form,"canMarch"),
        nativeTotalSoldierNum=number(member(form,"totalSoldierNum")),
        heroTotalSoldierCapacity=number(member(form,"heroTotalSoldierCapacity")),
        soldierSlots={}}
    local soldiers=member(form,"soldiers")
    local current=member(form,"totalSoldierNum")~=nil or
        member(form,"heroTotalSoldierCapacity")~=nil or member(form,"canMarch")~=nil
    if type(soldiers)=="table" then
        local scalar,nested=false,false
        for _,v in pairs(soldiers) do
            if type(v)=="table" then nested=true else scalar=true end
        end
        current=current or nested
        local total,valid,slots=0,true,0
        result.staffingShape=current and "current_nested_hero_slots" or "legacy_scalar_counts"
        if current and scalar then valid=false end
        for slot,value in pairs(soldiers) do
            slots=slots+1
            if slots>16 then valid=false;break end
            if current then
                local supply=number(member(value,"supply"))
                local capacity=number(member(value,"heroMaxCapacity"))
                local sum,entries=0,0
                if type(value)~="table" or not integer(slot) or supply==nil or
                   supply<0 or supply%1~=0 then valid=false;break end
                for key,count in pairs(value) do
                    if key~="supply" and key~="heroMaxCapacity" then
                        local n=number(count)
                        if not integer(key) or n==nil or n<0 or n%1~=0 then valid=false;break end
                        entries=entries+1
                        if entries>256 then valid=false;break end
                        sum=sum+n
                    end
                end
                if sum~=supply or (capacity~=nil and
                    (capacity<0 or capacity%1~=0 or supply>capacity)) then valid=false end
                result.soldierSlots[tostring(slot)]={supply=supply,
                    heroMaxCapacity=capacity,soldierTypeCount=entries}
                total=total+supply
            else
                local n=number(value)
                if n==nil or n<0 or n%1~=0 then valid=false;break end
                total=total+n
            end
        end
        if current and result.nativeTotalSoldierNum~=nil and
           result.nativeTotalSoldierNum~=total then valid=false end
        if valid then result.soldierCount=total end
    end
    local heroes=member(form,"heroes")
    if type(heroes)=="table" then
        local count=0
        for _,hero in pairs(heroes) do
            if ident(hero) then count=count+1 end
        end
        result.heroCount=count
    end
    local ok_local,local_count=invoke(form,"GetLocalHeroesCount")
    local n_local=ok_local and number(local_count) or nil
    if n_local~=nil and n_local>=0 and n_local%1==0 then result.localHeroCount=n_local end
    result.heroesReady=result.heroCount~=nil and result.heroCount>0 and
        (result.localHeroCount==nil or result.localHeroCount>0)
    result.staffedForAttack=result.soldierCount~=nil and result.soldierCount>0 and
        result.heroesReady and (not current or (result.canMarch==true and
        result.heroTotalSoldierCapacity~=nil and result.heroTotalSoldierCapacity>0))
    return result
end

-- Current inventory is separate from legacy ArmyManager.free projections.
local function current_soldier_inventory(center)
    local manager=member(center,"SoldierDataManager")
    local ok,list=invoke(manager,"GetInsideSoldiers")
    local result={observed=false,source="SoldierDataManager.GetInsideSoldiers",total=nil,byId={}}
    if not ok or type(list)~="table" then return result end
    local total,entries=0,0
    for _,item in pairs(list) do
        entries=entries+1
        local id=ident(member(item,"id"))
        local count=number(member(item,"count"))
        if entries>256 or not id or count==nil or count<0 or count%1~=0 then
            result.reason="malformed_current_soldier_inventory";return result
        end
        result.byId[id]=(result.byId[id] or 0)+count
        total=total+count
    end
    result.observed,result.total=true,total
    local ok_all,all=invoke(manager,"GetPlayerSoldiersTotalNum")
    result.nativePlayerTotal=ok_all and number(all) or nil
    return result
end

-- R5: v26 ArmyFormationDataManager.GetArmyUnFormationList (root/24,
-- lines 552-564) is the game's read-only projection of
-- ArmyManager.GetArmyFreeList (root/13, lines 234-240). The latter reads
-- allArmy[k].free; the former clamps negative counts to zero. In contrast,
-- AutoAddSoldierByForm (root/43, 917-959) CLEARS formation.soldiers and
-- mutates the formation, so must never be called by an inventory/preflight.
local function unassigned_soldier_inventory(center)
    local formation_manager=member(center,"ArmyFormationDataManager")
    local ok_free,free=invoke(formation_manager,"GetArmyUnFormationList")
    if not ok_free or type(free)~="table" then
        return {observed=false,reason="native_unassigned_soldier_list_unavailable",
            total=nil,countByType={},source="ArmyFormationDataManager.GetArmyUnFormationList"}
    end
    local summary={observed=false,total=nil,countByType={},
        source="ArmyFormationDataManager.GetArmyUnFormationList"}
    local total,n=0,0
    for soldier_id,count in pairs(free) do
        n=n+1
        if n>256 then summary.reason="native_unassigned_list_exceeds_bounded_types"; return summary end
        local key=ident(soldier_id)
        local amount=number(count)
        if not key or amount==nil or amount<0 or amount%1~=0 or amount>1000000000 then
            summary.reason="invalid_native_unassigned_soldier_record"
            return summary
        end
        summary.countByType[key]=amount
        total=total+amount
    end
    summary.observed=true
    summary.total=total
    return summary
end

-- v26 MarchUtil.GetMaxCanAddSoldierNum (root/45, 1073-1101) reads
-- building_base k5, APS_FORMATION_SIZE/ENHANCE and
-- GetFormationMaxNumByFormationIndex. It does not assign troops. This
-- capacity is advisory and never authorizes an attack by itself.
local function formation_capacity(form)
    local index=number(member(form,"index"))
    if not index or index<1 or index%1~=0 then return nil end
    local method=member(rawget(_G,"MarchUtil"),"GetMaxCanAddSoldierNum")
    if type(method)~="function" then return nil end
    local ok,capacity=pcall(method,member(form,"heroes"),index)
    local n=ok and number(capacity) or nil
    if n and n>=0 and n%1==0 and n<=1000000000 then return n end
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

local function selected_world_point()
    local ok_ui,ui=invoke(rawget(_G,"UIManager"),"GetInstance")
    if not ok_ui then return nil end
    local name=member(rawget(_G,"UIWindowNames"),"UIWorldPoint")
    if not name then return nil end
    local ok_open,opened=invoke(ui,"IsWindowOpen",name)
    if not ok_open or opened~=true then return nil end
    local ok_window,window=invoke(ui,"GetWindow",name)
    local ctrl=ok_window and member(window,"Ctrl") or nil
    if not ctrl then return nil end
    return {ui=ui,window=window,ctrl=ctrl,name=name}
end

-- R7: the v26 V2 formation controller's InitData (root/2 lines 31-46)
-- receives targetType, targetPoint, targetUuid and targetServerId as
-- SEPARATE fields. It does not obtain the CURRENT physical mouse position.
-- Keep a source-native typed-target witness; never convert a remembered
-- World.curTouchPoint or caller-supplied cursor coordinates into an attack.
local function native_account()
    local player=member(rawget(_G,"LuaEntry"),"Player")
    local uid=ident(member(player,"uid"))
    if not uid then return nil end
    local server
    for _,method in ipairs({"GetSourceServerId","GetSelfServerId","GetCurServerId"}) do
        local ok,value=invoke(player,method)
        local id=ident(value)
        if not ok or not id or (server and server~=id) then return nil end
        server=id
    end
    return {uid=uid,server=server,player=player}
end

-- Native city points expose allianceId; WorldMarchData exposes allianceUid.
-- An explicit zero is unaffiliated. Missing/malformed membership is unknown,
-- never an enemy. A resource is neutral only with an explicit empty gather UUID.
local function alliance_identity(value)
    if value==0 or value=="0" then return "0" end
    return ident(value)
end

-- Original DataCenter/Global/PlayerInfo.luac SHA256
-- 7b676b165212689d1213258464cfd7d23fa3ae0332a3f0ff565c8ec8c484a7b1:
-- IsInAlliance root/57 line1217 returns NOT string.IsNullOrEmpty(allianceId);
-- GetAllianceUid root/64 line1298 returns allianceId. Empty current player
-- membership is therefore source-known unaffiliated, not a positive alliance.
local function player_alliance_observation(player)
    local ok_in,in_alliance=invoke(player,"IsInAlliance")
    local ok_uid,native_uid=invoke(player,"GetAllianceUid")
    local raw=member(player,"allianceId")
    local data={membershipReadSucceeded=ok_in,
        allianceUidReadSucceeded=ok_uid,allianceUidRaw=native_uid~=nil and tostring(native_uid) or nil,
        allianceIdRaw=raw~=nil and tostring(raw) or nil}
    if type(in_alliance)=="boolean" then data.isInAlliance=in_alliance end
    return data,ok_in,in_alliance,ok_uid,native_uid,raw
end

local function target_alliance_guard(account,target,requires_player_alliance,is_march)
    -- Ordinary monsters/resources are not player-alliance targets in the
    -- original flow. Their own native type/occupancy/server checks decide
    -- eligibility; do not turn a missing alliance field into a false denial.
    if requires_player_alliance~=true then
        return {ownerAllianceUid=nil,targetAllianceUid=nil,
            targetAllianceVerified=true,sameAlliance=false,
            allianceCheckRequired=false}
    end
    local _,ok_in,in_alliance,ok_uid,native_uid,raw=
        player_alliance_observation(account and account.player)
    local field=alliance_identity(raw)
    local own
    if ok_in and in_alliance==false then
        -- Original explicit membership false is a known unaffiliated owner.
        -- Positive contradictory identity is unknown, never silently ignored.
        if (field and field~="0") or (alliance_identity(native_uid) and
            alliance_identity(native_uid)~="0") then
            return nil,failed("Own alliance membership contradicts identity; attack suppressed")
        end
        own="0"
    elseif ok_in and in_alliance==true and ok_uid then
        own=alliance_identity(native_uid)
        if own=="0" or (field and field~=own) then own=nil end
    end
    if not own then return nil,failed("Own alliance identity unknown; attack suppressed") end
    local target_alliance=alliance_identity(member(target,is_march and "allianceUid" or "allianceId"))
    -- If the current player is source-proven unaffiliated, no other player
    -- can be in the same alliance. Target alliance identity need not exist.
    if own=="0" then
        return {ownerAllianceUid=own,targetAllianceUid=target_alliance,
            targetAllianceVerified=target_alliance~=nil,sameAlliance=false,
            allianceCheckRequired=true}
    end
    if not target_alliance then
        return nil,failed("Target alliance identity unknown; attack suppressed")
    end
    if own~="0" and target_alliance==own then
        return nil,failed("Target belongs to own alliance; attack suppressed")
    end
    return {ownerAllianceUid=own,targetAllianceUid=target_alliance,
        targetAllianceVerified=true,sameAlliance=false,
        allianceCheckRequired=true}
end

local function native_selector_target(ctrl,uid)
    local kind=member(ctrl,"targetType")
    local kinds=rawget(_G,"MarchTargetType")
    local is_city=kind~=nil and kind==member(kinds,"ATTACK_CITY")
    local is_army=kind~=nil and kind==member(kinds,"ATTACK_ARMY")
    local is_monster=kind~=nil and kind==member(kinds,"ATTACK_MONSTER")
    local is_resource=kind~=nil and kind==member(kinds,"COLLECT")
    local is_ruin=kind~=nil and kind==member(kinds,"ATTACK_PLAYER_RUIN_BUILDING")
    local uuid,native_uuid=ident(member(ctrl,"targetUuid"))
    if (not uuid and not is_resource) or
       not (is_city or is_army or is_monster or is_resource or is_ruin) then
        return nil, failed("Open a native formation selector on a typed city/army/monster/resource/ruin target")
    end
    local point=member(ctrl,"targetPoint")
    local point_id=ident(point)
    if (is_city or is_resource or is_ruin) and not point_id then
        return nil, failed("Native point selector has no original targetPoint")
    end
    local account=native_account()
    if not account or account.uid~=uid then
        return nil,failed("Native selector account changed")
    end
    local selector_server=member(ctrl,"targetServerId")
    local server=number(selector_server)
    if selector_server~=nil and (server==nil or
       (server>0 and tostring(server)~=account.server)) then
        return nil, failed("Native target selector server differs from admitted owner server")
    end
    local world=member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local ok,target
    if is_resource then ok,target=invoke(world,"GetResourcePointInfoByIndex",tonumber(point_id))
    elseif is_city or is_ruin then ok,target=invoke(world,"GetPointInfo",tonumber(point_id))
    else ok,target=invoke(world,"GetMarch",native_uuid) end
    if not ok or target==nil or (not is_resource and ident(member(target,"uuid"))~=uuid) then
        return nil, failed("Native typed selector target is absent or changed")
    end
    if is_resource and (not integer(member(target,"id")) or
        not integer(member(target,"mainIndex")) or
        member(target,"gatherMarchUuid")==nil or
        tostring(member(target,"gatherMarchUuid"))~="0") then
        return nil,failed("Native resource selector is occupied or lacks resource identity")
    end
    if is_resource and member(ctrl,"targetUuid")~=nil and
       tostring(member(ctrl,"targetUuid"))~="0" then
        return nil,failed("Original COLLECT selector unexpectedly contains a UUID")
    end
    if is_resource then uuid=ident(member(target,"mainIndex")) end
    local owner=ident(member(target,"ownerUid"))
    if owner==uid then
        return nil, failed("Native typed selector target belongs to approved own account")
    end
    local target_server=member(target,"serverId")
    if target_server~=nil and tostring(target_server)~=account.server then
        return nil, failed("Native typed selector target belongs to another server")
    end
    local alliance,alliance_error=target_alliance_guard(
        account,target,is_city or is_army or is_ruin,is_army)
    if not alliance then return nil,alliance_error end
    return {kind=kind,uuid=uuid,point=point,selectorServer=selector_server,
        targetOwnerUid=owner,targetServerId=target_server,
        ownerAllianceUid=alliance.ownerAllianceUid,targetAllianceUid=alliance.targetAllianceUid,
        targetAllianceVerified=true,
        observation="open_native_formation_selector_only",
        currentCursorVerified=false,physicalCursorTargetVerified=false,
        requestDispatched=false}
end

-- Observation only. Preserve the original static sender, exact arguments,
-- return values and exceptions. Track ONLY exact pending formation/march,
-- target kind and target UUID on the two source-native attack routes.
local observed_send, observed_send_owner, active_create_send, active_attack_invocation
local function observe_attack_sender()
    local net=rawget(_G,"SFSNetwork")
    local original=member(net,"SendMessage")
    if observed_send then
        -- A new/replaced native sender could bypass the observation fence.
        -- Never wrap a different sender as though it were the exact original
        -- previously captured for an outstanding nonce transaction.
        return net==observed_send_owner and original==observed_send
    end
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
                    if active_attack_invocation==transaction then
                        transaction.sendCount=(transaction.sendCount or 0)+1
                        transaction.outboundRoute=created and "WorldMarchFormationNew" or "WorldMarchChange"
                        transaction.outboundFormationOrMarch=ident(args[1])
                        transaction.outboundTargetType=args[2]
                        transaction.outboundTargetUuid=ident(args[3])
                        transaction.outboundPath=type(args[4])=="string" and args[4] or nil
                        matched[#matched+1]=transaction
                    else
                        -- The original OnCheckTime may show a confirmation
                        -- dialog and RETURN before the user accepts it. A
                        -- later same-target native send could originate from
                        -- that dialog OR an unrelated manual game action.
                        -- Without an exact native callback/intent token we
                        -- must never attribute its nonce to this shortcut.
                        transaction.unattributedMatchingSendObserved=true
                        transaction.unattributedSendCount=
                            (transaction.unattributedSendCount or 0)+1
                    end
                end
            end
        end
        -- OnCreate serializes the source-generated clientCreateUuid while
        -- SendMessage handles this exact native request. An unrelated packet
        -- serializer must never donate its nonce to a pending attack.
        local previous_active=active_create_send
        active_create_send=created and #matched==1 and matched[1] or nil
        local returned=table.pack(pcall(original,route,table.unpack(args,1,args.n)))
        active_create_send=previous_active
        for _,transaction in ipairs(matched) do
            transaction.senderReturned=returned[1]
            transaction.senderReturnedFalse=returned[1] and returned[2]==false
            transaction.senderError=not returned[1] and tostring(returned[2]) or nil
        end
        if not returned[1] then error(returned[2],0) end
        return table.unpack(returned,2,returned.n)
    end
    net.SendMessage=observed_send
    observed_send_owner=net
    return member(net,"SendMessage")==observed_send
end

-- v26 WorldMarchFormationNewMessage.OnCreate lines 46-47 serializes a
-- clientCreateUuid returned by World.SaveCreateMarchRecordTime. The original
-- response fixture also carries clientCreateUuid, ownerUid and march uuid.
-- Inspect the serialized SFSObject after its original OnCreate, without
-- generating, replacing, altering or replaying the native request token.
-- Installed Assembly-CSharp.rdl WorldMarchDataManager.SaveCreateMarchRecordTime
-- (0x060032de) uses System.Guid.NewGuid().ToString() and records the GUID
-- against server time. Both shipped SmartFox2X.rdl and Smartfox2xLw.rdl
-- expose SFSObject.GetUtfString(string), but Lua/native runtime interop and
-- LIVE server echo remain unverified. Never treat a synthetic arbitrary
-- string as a witnessed original request nonce.
local function original_create_guid(token)
    if type(token)~="string" or #token~=36 then return false end
    local a,b,c,d,e=token:match("^(%x+)%-(%x+)%-(%x+)%-(%x+)%-(%x+)$")
    return a~=nil and #a==8 and #b==4 and #c==4 and #d==4 and #e==12
end

local observed_create_serializer, observed_serializer_owner
local function observe_create_serializer()
    local ok,class=pcall(require,"Net.Msgs.WorldMarchFormationNewMessage")
    local original=ok and member(class,"OnCreate") or nil
    if observed_create_serializer then
        return class==observed_serializer_owner and
            original==observed_create_serializer
    end
    if type(original)~="function" then return false end
    local wrapper=function(self,formation,kind,target,...)
        local returned=table.pack(pcall(original,self,formation,kind,target,...))
        local transaction=active_create_send
        if returned[1] and transaction and
           ident(formation)==transaction.formation and
           kind==transaction.kind and ident(target)==transaction.targetUuid then
            local packet=member(self,"sfsObj")
            local ok_token,token=invoke(packet,"GetUtfString","clientCreateUuid")
            transaction.serializerObserved=true
            if ok_token and original_create_guid(token) then
                transaction.clientCreateUuid=token
            end
        end
        if not returned[1] then error(returned[2],0) end
        return table.unpack(returned,2,returned.n)
    end
    class.OnCreate=wrapper
    observed_create_serializer=wrapper
    observed_serializer_owner=class
    return member(class,"OnCreate")==wrapper
end

local observed_replies={}
local function observe_attack_replies()
    local create_reply_observer_intact=false
    for _,name in ipairs({"WorldMarchFormationNewMessage","WorldMarchChangeMessage"}) do
        local ok,class=pcall(require,"Net.Msgs."..name)
        local current=ok and member(class,"HandleMessage") or nil
        if observed_replies[name] and current~=observed_replies[name] then
            -- Another owner replaced or reloaded the original reply handler.
            -- Never silently recapture it: the original callback chain and
            -- exact native response attribution are no longer demonstrated.
            if name=="WorldMarchFormationNewMessage" then return false end
        elseif not observed_replies[name] and type(current)=="function" then
                local original=current
                local source_route=name=="WorldMarchFormationNewMessage" and
                    "WorldMarchFormationNew" or "WorldMarchChange"
                local wrapper=function(self,response,...)
                    local create_token=member(response,"clientCreateUuid")
                    local owner=ident(member(response,"ownerUid"))
                    local response_uuid=ident(member(response,"uuid"))
                    local response_error=member(response,"errorCode")
                    local matches={}
                    for _,transaction in pairs(attacks) do
                        -- The same session may contain a separate squad recall
                        -- or another attack. A change ACK is not a create ACK.
                        if (transaction.sendCount or 0)>0 and
                           transaction.outboundRoute==source_route then
                            transaction.routeReplyCount=(transaction.routeReplyCount or 0)+1
                            if source_route=="WorldMarchFormationNew" and
                               type(create_token)=="string" and
                               transaction.clientCreateUuid==create_token then
                                matches[#matches+1]=transaction
                            end
                        end
                    end
                    local returned=table.pack(pcall(original,self,response,...))
                    if #matches==1 then
                        local transaction=matches[1]
                        transaction.replyObserved=true
                        transaction.replyMessage=name
                        transaction.replyErrorCode=response_error and tostring(response_error) or nil
                        transaction.replyUuid=response_uuid
                        transaction.replyOwnerUid=owner
                        transaction.replyClientCreateUuid=create_token
                        transaction.replyCount=(transaction.replyCount or 0)+1
                        transaction.replyApplied=returned[1]
                        transaction.replyCorrelation=(returned[1] and response_error==nil and
                            owner==transaction.uid and response_uuid~=nil and
                            transaction.replyCount==1) and
                            "native_clientCreateUuid_owner_march_reply" or "incomplete_native_reply"
                    end
                    if not returned[1] then error(returned[2],0) end
                    return table.unpack(returned,2,returned.n)
                end
                class.HandleMessage=wrapper
                observed_replies[name]=wrapper
        end
        if name=="WorldMarchFormationNewMessage" then
            create_reply_observer_intact=ok and
                observed_replies[name]~=nil and
                member(class,"HandleMessage")==observed_replies[name]
        end
    end
    return create_reply_observer_intact
end

local function transaction_view(transaction)
    if not transaction then return nil end
    local view={}
    for _,key in ipairs({"formation","kind","targetUuid","sendCount","outboundRoute",
        "outboundFormationOrMarch","outboundTargetType","outboundTargetUuid","outboundPath",
        "senderReturned","senderReturnedFalse","senderError","serializerObserved",
        "unattributedMatchingSendObserved","unattributedSendCount",
        "clientCreateUuid","routeReplyCount","replyObserved","replyMessage","replyCount",
        "replyErrorCode","replyUuid","replyOwnerUid","replyClientCreateUuid",
        "replyApplied","replyCorrelation",
        "nativeMarchGuidObserved","nativeMarchGuidMatchesRequest"}) do
        view[key]=transaction[key]
    end
    if transaction.unattributedMatchingSendObserved then
        view.nonceStatus="matching_send_outside_original_controller_causal_scope"
    elseif (transaction.sendCount or 0)~=1 then
        view.nonceStatus="no_unique_observed_create_send"
    elseif transaction.senderError or transaction.senderReturnedFalse then
        view.nonceStatus="sender_outcome_uncertain"
    elseif transaction.serializerObserved~=true then
        view.nonceStatus="original_serializer_not_observed"
    elseif not transaction.clientCreateUuid then
        view.nonceStatus="serialized_create_nonce_unreadable"
    elseif transaction.replyObserved~=true then
        view.nonceStatus="awaiting_same_nonce_reply"
    elseif transaction.replyErrorCode then
        view.nonceStatus="same_nonce_server_error"
    elseif transaction.replyCorrelation=="native_clientCreateUuid_owner_march_reply" then
        view.nonceStatus="same_nonce_owner_march_reply_observed_requires_fresh_poststate"
    else
        view.nonceStatus="same_nonce_reply_incomplete"
    end
    -- A transport/serializer observation alone never licenses a duplicate
    -- request, proves a physical pointer, or substitutes for march poststate.
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
    local account=native_account()
    local uid=account and account.uid
    if not uid or type(fields.authorizedUid) ~= "string" or fields.authorizedUid ~= uid then
        return nil, failed("Fresh game UID must match admitted owner-session UID")
    end
    if not read_only and
       (type(input_clear) ~= "function" or input_clear() ~= true) then
        return nil, failed("Current-game text-input focus unavailable or selected")
    end
    -- A reconnect must not silently discard an unacknowledged spend. A new
    -- session can observe its previous poststate, but cannot immediately
    -- double-send the same slot while that outcome remains uncertain.
    current_session = session .. "/" .. uid .. "/" .. account.server
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
    local staffing=formation_staffing(formation)
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
        formationStaffing=staffing,
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

-- v26 both UIFormationSelectListV2Ctrl.OnEditClick (root/20, 595-621)
-- and UIFormationSelectListNewCtrl.OnEditClick (root/20, 600-626) open
-- UIHeroPVPFormation with EnterHeroSquadPanelWay.ToMarch and the native
-- formation UUID. WARNING: the ORIGINAL OnEditClick passes a completion
-- callback that invokes OnCheckTime, which can automatically SEND a march.
-- This explicit, owner-gated UI-only route replaces that dangerous callback
-- with an inert one. Only the PERSON in the original editor can subsequently
-- choose to save troops/heroes; no automatic formation assignment or attack.
local function open_manual_staffing_editor(fields,uid)
    if fields.editorOptIn~="1" or type(fields.slot)~="string" or
       not fields.slot:match("^[1-4]$") then
        return failed("Explicit native formation editor consent and slot 1 to 4 required")
    end
    if attacks[fields.slot] or pending[fields.slot] then
        return failed("Native slot has an unresolved attack or rapid transaction; editor shortcut fenced")
    end
    local selector=selected_window()
    if not selector then
        return failed("A single original formation selector must already be open")
    end
    local ui=selector.ui
    local player=member(rawget(_G,"LuaEntry"),"Player")
    local center=rawget(_G,"DataCenter")
    local forms=member(center,"ArmyFormationDataManager")
    local marches=member(center,"WorldMarchDataManager")
    local ok_form,form=invoke(forms,"GetOneArmyInfoByIndex",tonumber(fields.slot))
    local uuid,native_uuid=ident(member(form,"uuid"))
    if not ok_form or not uuid then return failed("Native formation slot is locked") end
    local owner=member(form,"ownerUid")
    if owner~=nil and ident(owner)~=uid then
        return failed("Native formation owner mismatch")
    end
    local ok_march,march=invoke(marches,"GetOwnerFormationMarch",
        member(player,"uid"),native_uuid,member(player,"allianceId"))
    if not ok_march or march~=nil then
        return failed("Native squad has an existing or unreadable march; no manual editor shortcut")
    end
    local name=member(rawget(_G,"UIWindowNames"),"UIHeroPVPFormation")
    local way=member(rawget(_G,"EnterHeroSquadPanelWay"),"ToMarch")
    local open=member(ui,"OpenWindow")
    if not name or way==nil or type(open)~="function" then
        return unsupported("Original formation editor window/mode unavailable")
    end
    local ok_before,already_open=invoke(ui,"IsWindowOpen",name)
    if not ok_before or already_open~=false then
        return failed("Native formation editor is already open or cannot be checked")
    end
    local again=selected_window()
    if not again or again.window~=selector.window or again.ctrl~=selector.ctrl then
        return failed("Original formation selector changed before editing")
    end
    local refreshed,refreshed_form=invoke(forms,"GetOneArmyInfoByIndex",tonumber(fields.slot))
    if not refreshed or ident(member(refreshed_form,"uuid"))~=uuid or
       (member(refreshed_form,"ownerUid")~=nil and
        ident(member(refreshed_form,"ownerUid"))~=uid) then
        return failed("Native formation identity changed before editor open")
    end
    local ok_open=pcall(open,ui,name,{anim=true},way,native_uuid,function() end)
    if not ok_open then
        return failed("Original native formation editor opening failed")
    end
    local ok_after,is_open=invoke(ui,"IsWindowOpen",name)
    if not ok_after or is_open~=true then
        return failed("Native formation editor opening not independently observed")
    end
    return {state="confirmed",data={
        observation="native_manual_formation_editor_open",
        slot=tonumber(fields.slot),formationUuid=uuid,
        windowName=name,editWay="ToMarch",
        completionCallback="no_op_prevents_automatic_OnCheckTime_attack",
        staffingPreparationPerformed=false,formationSaveSent=false,
        attackDispatched=false,requestDispatched=false,
        operatorMustSaveInOriginalGame=true}}
end

-- R8: one consolidated, strictly read-only game-native prerequisite view.
-- v26 selector InitData owns the typed target, ArmyFormationDataManager
-- owns the saved squad, and GetOwnerFormationMarch owns current availability.
-- This deliberately does not call MarchUtil/OnCheckTime, install packet
-- observers, synthesize pointer hits, allocate soldiers, or assert an ACK.
local function attack_readiness(fields,uid)
    if type(fields.slot)~="string" or not fields.slot:match("^[1-4]$") then
        return failed("Read-only attack readiness requires slot 1 to 4")
    end
    local center=rawget(_G,"DataCenter")
    local forms=member(center,"ArmyFormationDataManager")
    local marches=member(center,"WorldMarchDataManager")
    local player=member(rawget(_G,"LuaEntry"),"Player")
    local ok_form,form=invoke(forms,"GetOneArmyInfoByIndex",tonumber(fields.slot))
    local formation,formation_number=ident(member(form,"uuid"))
    local form_owner=member(form,"ownerUid")
    local owner_ok=form_owner==nil or ident(form_owner)==uid
    local staffing=formation_staffing(form)
    local ok_march,existing=false,nil
    if formation and owner_ok then
        ok_march,existing=invoke(marches,"GetOwnerFormationMarch",
            member(player,"uid"),formation_number,member(player,"allianceId"))
    end
    local selection=selected_window()
    local target,denial=nil,nil
    if selection then target,denial=native_selector_target(selection.ctrl,uid) end
    local unassigned=unassigned_soldier_inventory(center)
    local capacity=formation_capacity(form)
    local reasons={}
    if not ok_form or not formation then
        reasons[#reasons+1]="native_formation_locked_or_unavailable"
    elseif not owner_ok then
        reasons[#reasons+1]="native_formation_owner_mismatch"
    end
    if not staffing.staffedForAttack then
        reasons[#reasons+1]="native_formation_soldiers_or_heroes_unstaffed"
    end
    if not ok_march then
        reasons[#reasons+1]="native_formation_march_lookup_unavailable"
    elseif existing~=nil then
        reasons[#reasons+1]="native_formation_has_existing_march"
    end
    if attacks[fields.slot] or pending[fields.slot] then
        reasons[#reasons+1]="native_attack_or_speedup_transaction_pending"
    end
    if not target then
        reasons[#reasons+1]=selection and "native_typed_selector_target_invalid"
            or "native_typed_selector_not_open"
    end
    local can_editor_help=not staffing.staffedForAttack and
        unassigned.observed and unassigned.total>0 and capacity~=nil and
        staffing.soldierCount~=nil and capacity>staffing.soldierCount
    return {state="confirmed",data={
        observation="native_attack_readonly_prerequisite_inventory",
        slot=tonumber(fields.slot),formationUuid=formation,
        nativeFormationOwnerVerified=owner_ok and formation~=nil,
        nativeMarchLookupSucceeded=ok_march,
        nativeExistingMarchUuid=ident(member(existing,"uuid")),
        formationStaffing=staffing,
        nativeFormationCapacity=capacity,
        unassignedSoldiers=unassigned,currentSoldiers=current_soldier_inventory(center),
        originalManualEditorMayHelp=can_editor_help==true,
        target=target,
        targetDenial=denial and denial.error or nil,
        sourcePrerequisitesObserved=#reasons==0,
        outstandingReasons=reasons,
        physicalCursorTargetVerified=false,
        keyboardInputFocusAtActionVerified=false,
        serverRequestTokenEchoVerified=false,
        serverAcknowledgementCorrelated=false,
        nativeMarchPostStateVerified=false,
        safeToDispatchFromPhysicalCursor=false,
        attackDispatched=false,requestDispatched=false,
        staffingMutationPerformed=false
    }}
end

-- Actual main-world source: installed Assembly-CSharp.rdl WorldScene
-- GetTouchPoint() token 0x060039e6 reads WorldCamera.GetRaycastGroundPoint(
-- UnityEngine.Input.mousePosition); GetTouchPoint(Vector3) token 0x060039e7
-- uses a supplied screen position; WorldToTile + TilePosToIndex are the
-- original world-tile projection. WorldPointObject.OnClickWorldPointObject
-- token 0x060035e2 calls Lua UIUtil.OnClickWorld(pointIndex,1), whose
-- category-specific target resolution still needs independent evidence.
-- This only observes the native world tile; it never clicks/sends.
local function exact_march_id(value)
    if value==nil or type(value)=="boolean" then return nil end
    if type(value)=="number" and
       (not math.type or math.type(value)~="integer") then return nil end
    local decimal=tostring(value)
    if not decimal:match("^[1-9]%d*$") or #decimal>19 then return nil end
    return decimal
end

local function native_cursor_point(fields, uid)
    local account=native_account()
    if not account or account.uid~=uid then
        return nil,failed("Native world cursor owner changed")
    end
    local scene=member(member(rawget(_G,"CS"),"SceneManager"),"World")
    if not scene or type(member(scene,"GetTouchPoint"))~="function" or
       type(member(scene,"WorldToTile"))~="function" or
       type(member(scene,"TilePosToIndex"))~="function" then
        return nil,unsupported("Native world scene is not available for cursor projection")
    end
    local unity=member(rawget(_G,"CS"),"UnityEngine")
    local screen=member(unity,"Screen")
    local input=member(unity,"Input")
    local width=number(member(screen,"width"))
    local height=number(member(screen,"height"))
    if not width or not height or width<1 or height<1 then
        return nil,unsupported("Native Unity viewport dimensions unavailable")
    end
    local sampled=fields.samplePixelX~=nil or fields.samplePixelY~=nil
    local pos
    if sampled then
        local x,y=number(fields.samplePixelX),number(fields.samplePixelY)
        if not x or not y then return nil,failed("Both finite cursor sample coordinates are required") end
        pos={x=x,y=y}
    else
        pos=member(input,"mousePosition")
    end
    local x,y=number(member(pos,"x")),number(member(pos,"y"))
    if not x or not y or x<0 or y<0 or x>=width or y>=height then
        return nil,failed("Current cursor is outside the native Unity game viewport")
    end
    local intersection,ok_hit
    if sampled then
        local vector=rawget(_G,"Vector3") or member(unity,"Vector3")
        local create=member(vector,"New")
        if type(create)~="function" then
            return nil,unsupported("Native Unity vector constructor unavailable")
        end
        local ok_v,point=pcall(create,x,y,0)
        if not ok_v or not point then return nil,unsupported("Native sampled cursor position invalid") end
        ok_hit,intersection=invoke(scene,"GetTouchPoint",point)
    else
        -- The installed v26 WorldScene.GetTouchPoint overload takes a
        -- UnityEngine.Vector3 screen point (managed token 0x060039e7).
        -- Input.mousePosition is already that precise native Vector3;
        -- calling the method with no argument would never project a real
        -- physical cursor, even if sampled diagnostics happened to pass.
        ok_hit,intersection=invoke(scene,"GetTouchPoint",pos)
    end
    if not ok_hit or intersection==nil then
        return nil,unsupported("Native WorldScene.GetTouchPoint raycast unavailable")
    end
    local ok_tile_pos,tile_pos=invoke(scene,"WorldToTile",intersection)
    if not ok_tile_pos or tile_pos==nil then
        return nil,unsupported("Native WorldScene.WorldToTile failed")
    end
    local ok_tile,raw_tile=invoke(scene,"TilePosToIndex",tile_pos)
    local tile=ok_tile and ident(raw_tile) or nil
    if not tile then return nil,unsupported("Cursor world intersection has no native world tile") end
    local point_class={
        [1]="collect_resource",[4]="ordinary_monster",[5]="world_boss_excluded",
        [6]="player_city",[7]="resource_node",[9]="sample_point",
        [11]="alliance_city",[13]="new_sample_point",
        [15]="alliance_build",[25]="city_stronghold",
        [26]="alliance_collect_resource",
        [38]="destroyed_city_ruin",[1003]="ordinary_simple_monster"}
    -- UIUtil.OnClickWorld first reads World:GetPointInfo(tileIndex). For
    -- secondary/occupied tiles, it resolves the authoritative mainIndex.
    -- The pointType values are source-pinned managed WorldPointType constants.
    local ok_point,point=invoke(scene,"GetPointInfo",tonumber(tile))
    local main=ok_point and ident(member(point,"mainIndex")) or nil
    local is_main=member(point,"isMainPoint")
    if is_main==false and main then
        local ok_canonical,canonical=invoke(scene,"GetPointInfo",tonumber(main))
        if not ok_canonical or canonical==nil then
            return nil,unsupported("Native world object's main tile is unavailable")
        end
        point=canonical
    end
    local point_type=number(member(point,"PointType"))
        or number(member(point,"pointType"))
    local kind=point_type and point_class[point_type] or nil
    local target={
        pointIndex=tile,mainIndex=main,worldPointType=point_type,
        category=kind or (point==nil and "empty_world_tile" or "unknown_world_point_type"),
        uuid=ident(member(point,"uuid")),
        ownerUid=ident(member(point,"ownerUid")),
        source="UIUtil.OnClickWorld:GetPointInfo and original managed WorldPointType",
        targetMethodSelected=false,nativeMarchTargetValidated=false,
        safeToDispatchFromPhysicalCursor=false}
    local last_touch=member(scene,"curTouchPoint")
    local last_tile=nil
    if last_touch~=nil then
        local ok_last,old_pos=invoke(scene,"WorldToTile",last_touch)
        if ok_last and old_pos~=nil then
            local ok_old,old=invoke(scene,"TilePosToIndex",old_pos)
            if ok_old then last_tile=ident(old) end
        end
    end
    if not sampled then
        local current=member(input,"mousePosition")
        if number(member(current,"x"))~=x or number(member(current,"y"))~=y then
            return nil,failed("Unity mouse moved during native world cursor projection")
        end
    end
    -- The original moving-monster pointer identity is independent of the
    -- ground PointInfo category. Expose an *observation only* raycast in the
    -- existing cursorPoint diagnostic; an empty terrain tile does not prove
    -- absence of a hovered WorldArmy March_<Int64> collider.
    local army={raycastSupported=false,raycastSucceeded=false,
        hitFound=false,monsterVerified=false,worldBoss=false}
    if type(member(scene,"GetRaycastHitMarch"))=="function" then
        army.raycastSupported=true
        local raycast_point=pos
        if sampled then
            local vector=rawget(_G,"Vector3") or member(unity,"Vector3")
            local create=member(vector,"New")
            local ok_vector,point=false,nil
            if type(create)=="function" then
                ok_vector,point=pcall(create,x,y,0)
            end
            if ok_vector then raycast_point=point end
        end
        if raycast_point~=nil then
            local ok_army,hit=invoke(scene,"GetRaycastHitMarch",raycast_point)
            army.raycastSucceeded=ok_army
            local uuid=ok_army and exact_march_id(hit) or nil
            army.hitFound=uuid~=nil
            army.marchUuid=uuid
            if uuid then
                local _,native_march=invoke(scene,"GetMarch",hit)
                local _,managed_march=invoke(member(rawget(_G,"DataCenter"),"WorldMarchDataManager"),"GetMarch",hit)
                local native_alliance=member(native_march,"allianceUid")
                local managed_alliance=member(managed_march,"allianceUid")
                army.nativeAllianceUidRaw=native_alliance~=nil and tostring(native_alliance) or nil
                army.managerAllianceUidRaw=managed_alliance~=nil and tostring(managed_alliance) or nil
                local guard,denial=target_alliance_guard(account,native_march,false,true)
                army.targetAllianceVerified=guard~=nil
                army.targetAllianceUid=guard and guard.targetAllianceUid or nil
                army.allianceDenial=denial and denial.error or nil
                local ok_troop,troop=invoke(scene,"GetTroop",hit)
                local ok_troop_id,troop_id=invoke(troop,"GetMarchUUID")
                local ok_monster,is_monster=invoke(troop,"IsMonsterTroop")
                local ok_boss,is_boss=invoke(troop,"IsWorldBossTroop")
                army.troopUuidMatches=ok_troop and ok_troop_id and
                    exact_march_id(troop_id)==uuid or false
                army.worldBoss=ok_boss and is_boss==true or false
                army.monsterVerified=army.troopUuidMatches and
                    ok_monster and is_monster==true and ok_boss and is_boss==false
            end
        end
    end
    if not sampled then
        local current=member(input,"mousePosition")
        if number(member(current,"x"))~=x or number(member(current,"y"))~=y then
            return nil,failed("Unity mouse moved during native WorldArmy cursor observation")
        end
    end
    local server_check=native_account()
    if not server_check or server_check.uid~=account.uid or
       server_check.server~=account.server or
       member(member(rawget(_G,"CS"),"SceneManager"),"World")~=scene then
        return nil,failed("Native account or world scene changed during cursor projection")
    end
    return {pixelX=x,pixelY=y,screenWidth=width,screenHeight=height,
        worldX=number(member(intersection,"x")),worldY=number(member(intersection,"y")),
        worldZ=number(member(intersection,"z")),pointIndex=tile,
        lastTouchTileIndex=last_tile,serverId=tonumber(account.server),
        ownerUid=account.uid,readOnly=sampled,physicalCursorRead=not sampled,
        source="WorldScene.GetTouchPoint+WorldToTile+TilePosToIndex",
        nativeMainWorldTileProjected=true,targetTypeVerified=kind~=nil,
        target=target,worldArmy=army,
        playerAlliance=player_alliance_observation(account.player),
        safeToDispatchFromPhysicalCursor=false,
        requestDispatched=false},nil
end

-- Managed v26 WorldScene.GetRaycastHitMarch(Vector3), token 0x06003ad1,
-- raycasts WorldArmy and reads a March_<Int64> collider name. The xLua
-- wrapper at 0x06008d32 returns the Int64 to Lua. Never use tonumber() on
-- this value: a floating point conversion corrupts march UUIDs above 2^53.

local cursor_open_pending
local function native_hover_target(fields,uid)
    if fields.samplePixelX~=nil or fields.samplePixelY~=nil then
        return nil,failed("Native hover action requires the physical game cursor")
    end
    -- Moving WorldArmy troops have their own native pointer collider. The
    -- ground raycast used for stationary resource points may MISS even when
    -- the mouse is physically over a moving troop; it is not a prerequisite
    -- to WorldScene.GetRaycastHitMarch(Vector3).
    local account=native_account()
    if not account or account.uid~=uid then
        return nil,failed("Native world hover account changed")
    end
    local scene=member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local unity=member(rawget(_G,"CS"),"UnityEngine")
    local width=number(member(member(unity,"Screen"),"width"))
    local height=number(member(member(unity,"Screen"),"height"))
    local pointer=member(member(unity,"Input"),"mousePosition")
    local x,y=number(member(pointer,"x")),number(member(pointer,"y"))
    if not width or not height or width<1 or height<1 or not x or not y or
       x<0 or y<0 or x>=width or y>=height then
        return nil,failed("Physical hover cursor is outside the native Unity viewport")
    end
    if pointer==nil or type(member(scene,"GetRaycastHitMarch"))~="function" then
        return nil,unsupported("Native WorldArmy march cursor raycast unavailable")
    end
    -- Unity Input.mousePosition is already the exact native Vector3 consumed
    -- by WorldInputManager.GetRaycastHitMarch -> WorldScene.ScreenPointToRay.
    -- Reconstructing it through the Lua Vector3.New wrapper is unnecessary and
    -- can change the marshalled object seen by the current xLua method.
    local ok_hit,hit=invoke(scene,"GetRaycastHitMarch",pointer)
    if not ok_hit then return nil,unsupported("Native WorldArmy march raycast failed") end
    local latest_pointer=member(member(unity,"Input"),"mousePosition")
    if number(member(latest_pointer,"x"))~=x or
       number(member(latest_pointer,"y"))~=y then
        return nil,failed("Physical mouse moved during native WorldArmy raycast")
    end
    local hit_id=exact_march_id(hit)
    if hit~=nil and tostring(hit)~="0" and hit_id==nil then
        return nil,failed("Native WorldArmy raycast returned an invalid Int64 march identity")
    end
    local hover_source="WorldScene.GetRaycastHitMarch+WorldTroop.GetMarchUUID+GetMarch"
    local popup_point,popup_cursor_point
    -- v26 UIUtil.OnClickWorldTroop resolves the clicked march first, then
    -- opens/refreshes UIWorldPoint with that exact marchUuid and target tile.
    -- Some rendered monsters do not expose their WorldArmy collider to the
    -- subsequent xLua raycast. In that case only reuse the game's *currently
    -- open* native monster popup when the same physical cursor freshly
    -- projects onto the popup's native target tile. A stale/moved popup cannot
    -- provide a target, and the live march/troop is fully revalidated below.
    if not hit_id and (fields.targetKind==nil or fields.targetKind=="monster") then
        local selected=selected_world_point()
        local ctrl=selected and selected.ctrl or nil
        local popup_uuid=ctrl and member(ctrl,"uuid") or nil
        local popup_id=exact_march_id(popup_uuid)
        local point=ctrl and integer(member(ctrl,"pointId")) or nil
        local popup_server=ctrl and member(ctrl,"serverId") or nil
        if popup_id and point and
           (popup_server==nil or number(popup_server)==0 or
            tostring(popup_server)==account.server) then
            local ok_world,popup_world=invoke(scene,"GetMarch",popup_uuid)
            local ok_troop,popup_troop=invoke(scene,"GetTroop",popup_uuid)
            local ok_monster,is_monster=invoke(popup_troop,"IsMonsterTroop")
            local ok_boss,is_boss=invoke(popup_troop,"IsWorldBossTroop")
            if ok_world and popup_world~=nil and ok_troop and popup_troop~=nil and
               ok_monster and is_monster==true and ok_boss and is_boss==false then
                local cursor=select(1,native_cursor_point(fields,uid))
                local cursor_tile=cursor and integer(tonumber(cursor.pointIndex)) or nil
                if cursor_tile and cursor_tile==point then
                    hit,hit_id=popup_uuid,popup_id
                    popup_point,popup_cursor_point=point,cursor_tile
                    hover_source="UIUtil.OnClickWorldTroop+UIWorldPoint.Ctrl+physical_cursor_tile"
                end
            end
        end
    end
    local intent=fields.targetKind
    if intent==nil then intent=hit_id and "monster" or "resource" end
    local fresh_account=native_account()
    if not fresh_account or fresh_account.uid~=uid or
       fresh_account.server~=account.server or
       member(member(rawget(_G,"CS"),"SceneManager"),"World")~=scene then
        return nil,failed("Native world/account changed during hover acquisition")
    end
    if intent=="monster" then
        if not hit_id then
            return nil,failed("Physical cursor is not over a native WorldArmy monster march")
        end
        local center=rawget(_G,"DataCenter")
        local manager=member(center,"WorldMarchDataManager")
        local ok_march,march=invoke(manager,"GetMarch",hit)
        local ok_scene_march,world_march=invoke(scene,"GetMarch",hit)
        local ok_troop,troop=invoke(scene,"GetTroop",hit)
        local ok_troop_id,troop_uuid=invoke(troop,"GetMarchUUID")
        local ok_monster,is_monster=invoke(troop,"IsMonsterTroop")
        local ok_boss,is_boss=invoke(troop,"IsWorldBossTroop")
        if not ok_march or not ok_scene_march or not ok_troop or
           not ok_troop_id or not ok_monster or not ok_boss or
           exact_march_id(member(march,"uuid"))~=hit_id or
           exact_march_id(member(world_march,"uuid"))~=hit_id or
           exact_march_id(troop_uuid)~=hit_id or
           is_monster~=true or is_boss~=false then
            return nil,failed("Hovered march identity/type is absent, changed, or a world boss")
        end
        local ok_type,march_type=invoke(march,"GetMarchType")
        local ordinary_type=member(rawget(_G,"NewMarchType"),"MONSTER")
        local monster_id=integer(member(march,"monsterId"))
        local ok_template,template=invoke(member(center,"MonsterTemplateManager"),
            "GetMonsterTemplate",monster_id)
        if not ok_type or ordinary_type==nil or march_type~=ordinary_type or
           not monster_id or not ok_template or
           number(member(template,"special"))~=0 then
            return nil,failed("Hovered monster is special, unsupported, or lacks ordinary template proof")
        end
        for _,source in ipairs({march,world_march}) do
            if exact_march_id(member(source,"ownerUid"))==uid or
               (member(source,"serverId")~=nil and
                tostring(member(source,"serverId"))~=account.server) then
                return nil,failed("Hovered monster is owned or on another server")
            end
        end
        local alliance,alliance_error=target_alliance_guard(account,world_march,false,true)
        if not alliance then return nil,alliance_error end
        local ok_pos,pos=invoke(troop,"GetPosition")
        local transform=member(rawget(_G,"SceneUtils"),"WorldToTileIndex")
        local world_scene=member(rawget(_G,"ForceChangeScene"),"World")
        if not ok_pos or pos==nil or type(transform)~="function" or
           world_scene==nil then
            return nil,unsupported("Original hovered monster WorldToTileIndex unavailable")
        end
        local ok_tile,point=pcall(transform,pos,world_scene)
        if not ok_tile or not integer(point) then
            return nil,failed("Hovered monster has no fresh native troop point")
        end
        if popup_point and
           (integer(point)~=popup_point or popup_cursor_point~=popup_point) then
            return nil,failed("Native popup monster no longer matches the physical cursor tile")
        end
        return {category="ordinary_monster",targetType="ATTACK_MONSTER",
            point=integer(point),uuid=hit,uuidText=hit_id,
            server=account.server,uid=account.uid,scene=scene,
            ownerAllianceUid=alliance.ownerAllianceUid,targetAllianceUid=alliance.targetAllianceUid,
            pixelX=x,pixelY=y,
            source=hover_source,nativeCursorRaycastVerified=popup_point==nil,
            nativePopupTargetVerified=popup_point~=nil},nil
    end
    if intent~="resource" then
        return nil,failed("Unsupported native hover intent")
    end
    if hit_id then
        return nil,failed("Physical cursor overlaps a troop; resource click target is ambiguous")
    end
    local cursor,reason=native_cursor_point(fields,uid)
    if not cursor then return nil,reason end
    if cursor.pixelX~=x or cursor.pixelY~=y or
       tostring(cursor.serverId)~=account.server or
       member(member(rawget(_G,"CS"),"SceneManager"),"World")~=scene then
        return nil,failed("Native resource ground cursor changed after WorldArmy raycast")
    end
    -- Original UIUtil.OnClickWorld uses the clicked PointInfo.pointIndex,
    -- normalizing non-main occupied tiles through PointInfo.mainIndex.
    local tile=integer(cursor.pointIndex)
    local ok_point,point=invoke(scene,"GetPointInfo",tile)
    if not ok_point or point==nil then
        return nil,failed("Cursor has no native resource world point")
    end
    if member(point,"isMainPoint")==false then
        local main=integer(member(point,"mainIndex"))
        local ok_main,canonical=invoke(scene,"GetPointInfo",main)
        if not main or not ok_main or canonical==nil then
            return nil,failed("Cursor resource has no original main point")
        end
        point=canonical
    end
    if number(member(point,"PointType"))~=7 then
        return nil,failed("Native hovered point is not an ordinary WorldResource")
    end
    local point_id=integer(member(point,"pointIndex"))
    if not point_id then
        return nil,failed("Native resource pointIndex is missing")
    end
    local ok_resource,resource=invoke(scene,"GetResourcePointInfoByIndex",point_id)
    if not ok_resource or resource==nil or
       not integer(member(resource,"id")) or
       not integer(member(resource,"mainIndex")) then
        return nil,failed("Native hovered resource record is missing")
    end
    local gathering=member(resource,"gatherMarchUuid")
    if gathering==nil or tostring(gathering)~="0" then
        return nil,failed("Native hovered resource occupancy is unknown or occupied")
    end
    if (member(point,"serverId")~=nil and
        tostring(member(point,"serverId"))~=account.server) or
       (member(resource,"serverId")~=nil and
        tostring(member(resource,"serverId"))~=account.server) then
        return nil,failed("Hovered resource belongs to another server")
    end
    local alliance,alliance_error=target_alliance_guard(account,resource,false,false)
    if not alliance then return nil,alliance_error end
    -- Original MarchUtil.OnGotoCollect -> TryStartMarch(COLLECT,nil,
    -- forcedPoint,pointIndex,resource.mainIndex,...) sends mainIndex as the
    -- correlated targetUid while the formation chooser targetUuid is nil.
    return {category="ordinary_resource",targetType="COLLECT",point=point_id,
        uuid=nil,uuidText=tostring(integer(member(resource,"mainIndex"))),
        server=account.server,uid=account.uid,
        ownerAllianceUid=alliance.ownerAllianceUid,targetAllianceUid=alliance.targetAllianceUid,
        scene=scene,pixelX=cursor.pixelX,pixelY=cursor.pixelY,
        source="UIUtil.OnClickWorld+World.GetResourcePointInfoByIndex"},nil
end

local function native_hover_open(fields,input_clear,uid)
    if fields.targetKind~=nil and fields.targetKind~="monster" and
       fields.targetKind~="resource" then
        return failed("Original hover targetKind must be monster or resource")
    end
    if cursor_open_pending and cursor_open_pending.session==current_session then
        return failed("Earlier original cursor formation opening may still be pending")
    end
    local target,reason=native_hover_target(fields,uid)
    if not target then return reason end
    local slot=fields.slot
    local formation
    if slot~=nil then
        if type(slot)~="string" or not slot:match("^[1-4]$") then
            return failed("Hover shortcut requires original squad slot 1 to 4")
        end
        local center=rawget(_G,"DataCenter")
        local ok_form,form=invoke(member(center,"ArmyFormationDataManager"),
            "GetOneArmyInfoByIndex",tonumber(slot))
        formation=ident(member(form,"uuid"))
        if not ok_form or not formation or
           (member(form,"ownerUid")~=nil and ident(member(form,"ownerUid"))~=uid) then
            return failed("Native hover squad identity/owner unavailable")
        end
        -- Original BestSelect previews current global soldiers inside the
        -- chooser. Require saved heroes here; validate nested supply/canMarch
        -- only after that original preview and again at the send boundary.
        local staffing=formation_staffing(form)
        if not staffing.heroCount or staffing.heroCount<1 then
            return failed("Selected squad has no verified assigned heroes")
        end
        local account=native_account()
        local ok_idle,active=invoke(member(center,"WorldMarchDataManager"),
            "GetOwnerFormationMarch",member(account and account.player,"uid"),
            integer(member(form,"uuid")),member(account and account.player,"allianceId"))
        if not ok_idle or active~=nil then
            return failed("Native hover squad is occupied or cannot prove idle")
        end
    end
    local opener=member(rawget(_G,"MarchUtil"),"OnClickStartMarch")
    local kind=member(rawget(_G,"MarchTargetType"),target.targetType)
    if type(opener)~="function" or kind==nil then
        return unsupported("Original MarchUtil.OnClickStartMarch route unavailable")
    end
    if selected_window() then
        return failed("An original formation selector is already open")
    end
    -- Raycast a SECOND time directly before the original UI entrypoint.
    -- Neither a caller supplied position nor a remembered touch point is
    -- allowed to substitute for the physical cursor's typed native object.
    local fresh,changed=native_hover_target(fields,uid)
    if not fresh then return changed end
    if fresh.uid~=target.uid or fresh.server~=target.server or
       fresh.scene~=target.scene or fresh.category~=target.category or
       fresh.point~=target.point or fresh.uuidText~=target.uuidText or
       fresh.source~=target.source or
       fresh.ownerAllianceUid~=target.ownerAllianceUid or
       fresh.targetAllianceUid~=target.targetAllianceUid or
       fresh.pixelX~=target.pixelX or fresh.pixelY~=target.pixelY or
       input_clear()~=true then
        return failed("Native hover target or text focus changed before original selector")
    end
    local account=native_account()
    if not account or account.uid~=uid or account.server~=target.server then
        return failed("Original hover selector account changed before dispatch")
    end
    cursor_open_pending={session=current_session,uid=uid,server=account.server,
        kind=fresh.targetType,point=fresh.point,uuid=fresh.uuidText,
        slot=slot,formation=formation,consumed=false,sentAt=os.time(),
        source=fresh.source,
        nativeCursorRaycastVerified=fresh.nativeCursorRaycastVerified==true,
        nativePopupTargetVerified=fresh.nativePopupTargetVerified==true}
    local ok,result
    if fresh.targetType=="ATTACK_MONSTER" then
        -- UIWorldPointBtn root/13 line1491 uses (kind,point,uuid,-1,1).
        ok,result=pcall(opener,kind,fresh.point,fresh.uuid,-1,1)
    else
        -- UIWorldPointBtn root/13/43 line2265 passes the server in arg7.
        ok,result=pcall(opener,kind,fresh.point,nil,nil,nil,nil,
            tonumber(account.server))
    end
    if not ok or result==false then
        return failed("Original hover selector invocation outcome uncertain; do not retry")
    end
    return {state="accepted",data={observation="original_native_hover_formation_entrypoint",
        accepted=true,
        targetCategory=fresh.category,targetPoint=tostring(fresh.point),
        targetUuid=fresh.uuidText,targetServerId=tonumber(account.server),
        ownerUid=uid,sourceServerId=tonumber(account.server),slot=slot and tonumber(slot) or nil,
        formationUuid=formation,awaitingNativeChooser=true,chooserRequestMade=true,
        nativeCursorRaycastVerified=true,originalUIInvoked=true,
        nativeSelectorConfirmed=false,awaitingOriginalUI=true,
        requestDispatched=false,worldMarchSent=false}}
end

function M.Prepare(fields, input_clear)
    local uid, denial = owner_and_focus(fields,input_clear,
        fields.mode == "inventory" or fields.mode=="attackReadiness" or
        fields.mode=="cursorPoint")
    if not uid then return denial end
    if fields.mode=="staffingEditor" then return open_manual_staffing_editor(fields,uid) end
    if fields.mode=="attackReadiness" then return attack_readiness(fields,uid) end
    if fields.mode=="cursorOpen" then return native_hover_open(fields,input_clear,uid) end
    if fields.mode=="cursorPoint" then
        local point,reason=native_cursor_point(fields,uid)
        if not point then return reason end
        return {state="confirmed",data={observation="native_world_cursor_tile_projection",
            cursor=point,requestDispatched=false,readOnly=true}}
    end
    if fields.mode=="selectorTarget" then
        local selection=selected_window()
        if not selection then return failed("Original formation selector not uniquely open") end
        local witness,denial=native_selector_target(selection.ctrl,uid)
        if not witness then return denial end
        return {state="confirmed",data={
            observation="native_typed_formation_selector_readonly",
            target=witness,safeToDispatchFromPhysicalCursor=false,
            requestDispatched=false}}
    end
    if fields.mode ~= "inventory" and fields.mode ~= "open" then return failed("Invalid native attack prepare mode") end
    local center = rawget(_G,"DataCenter")
    local manager = member(center,"WorldMarchDataManager")
    local world = member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local ok_all, all = invoke(manager,"GetAllMarches")
    local candidates, squads, owned_marches = {}, {}, {}
    local unassigned=unassigned_soldier_inventory(center)
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
        local staffing=formation_staffing(form)
        local local_heroes=staffing.localHeroCount
        local _,stamina=invoke(member(center,"ArmyFormationDataManager"),"GetCurStaminaByUuid",n)
        local capacity=formation_capacity(form)
        local has_heroes=staffing.heroCount~=nil and staffing.heroCount>0 and
            (local_heroes==nil or local_heroes>0)
        local can_prepare=ok_form and uuid~=nil and staffing.soldierCount~=nil and
            unassigned.observed and unassigned.total>0 and capacity~=nil and
            capacity>staffing.soldierCount and has_heroes
        squads[tostring(slot)]={unlocked=ok_form and uuid ~= nil,formationUuid=uuid,
            marchUuid=ident(member(march,"uuid")),targetType=member(march,"target"),
            endTime=number(member(march,"endTime")),lookupSucceeded=lookup_ok,
            formationState=number(member(form,"state")),formationIndex=number(member(form,"index")),
            heroCount=staffing.heroCount,localHeroCount=number(local_heroes),
            soldierCount=staffing.soldierCount,staffedForAttack=staffing.staffedForAttack,
            formationStaffing=staffing,
            formationCapacity=capacity,
            canPrepareWithUnassignedSoldiers=can_prepare==true,
            preparationMethod="native_formation_assignment_ui_required_no_mutation",
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
        local account=native_account()
        if not account or account.uid~=uid then return failed("Native account changed before selector") end
        local ok_open=pcall(open,1,kind,selected.pointIndex,tonumber(selected.uuid),nil,nil,nil,tonumber(account.server),0)
        if not ok_open then return failed("Native selector opening failed") end
        return {state="confirmed",data={observation="native_attack_selector_open_invoked",
            targetUuid=selected.uuid,targetPoint=selected.pointIndex,targetServerId=tonumber(account.server),
            source="UIUtil.OpenFormationSelectUI",marchDispatched=false}}
    end
    return {state="confirmed",data={observation="native_ordinary_monsters_and_owned_squads",
        candidates=candidates,squads=squads,ownedMarches=owned_marches,
        unassignedSoldiers=unassigned,currentSoldiers=current_soldier_inventory(center),
        staffingPreparationPerformed=false,
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
    pending[fields.slot] = {uid=uid,formation=final.formation,uuid=final.uuid,
        targetType=final.targetType,
        itemId=fields.itemId,count=fresh_item.count,endTime=final.endTime,
        sentAt=os.time()}
    -- A send may reach the server before returning false/raising locally.
    -- Install the one-shot tombstone before calling the actual native sender.
    local ok, result = pcall(send,route,final.uuidNumber,fresh_item.itemIdNumber,false)
    if not ok or result == false then
        return failed("Native rapid item send outcome uncertain; no retry until authoritative poststate")
    end
    return {state="accepted",data={requestDispatched=true,
        awaitingGameConfirmation=true,slot=fields.slot,marchUuid=final.uuid,
        itemId=fields.itemId}}
end

-- UILWRapidView.CellsCallBack must show the original
-- ShowUseDiamondConfirm(BuyUseDialog) and run its confirmation callback
-- before ConfirmBuy. A caller-supplied opt-in is not that native callback.
-- No direct WorldMarchRapid(..., true) route is exposed in this module.
local function speedup_diamond(fields, input_clear, uid)
    return unsupported("Native ShowUseDiamondConfirm callback and purchase ACK not verified; diamond rapid disabled")
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
    local form_owner=member(form,"ownerUid")
    if form_owner~=nil and ident(form_owner)~=uid then
        return failed("Native selected squad belongs to another owner")
    end
    local form_index=member(form,"index")
    if form_index~=nil and number(form_index)~=tonumber(fields.slot) then
        return failed("Native selected squad index changed")
    end
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
           prior.unattributedMatchingSendObserved or
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
                    awaitingGameConfirmation=true,requestDispatched=false,
                    ownerUid=uid,sourceServerId=tonumber(prior.server)}}
            end
            -- The attack happened, but requested speedup did not confirm.
            -- Retain the fence so another key edge cannot double-spend.
            return failed("Attack confirmed; requested rapid remains unconfirmed: "..tostring(result.error))
        end
        local fresh, uuid = fresh_owned(existing)
        local ok_type, kind = invoke(fresh,"GetMarchTargetType")
        local start_time = number(member(fresh,"startTime"))
        -- Installed WorldMarch::get_clientCreateGuid 0x060031bc and
        -- XLua.CSObjectWrap.WorldMarchWrap::_g_get_clientCreateGuid 0x06008a4b
        -- expose the native typed march's OWN request GUID. Original
        -- WorldMarch::UpdateFromSFS 0x060031e6 sources it directly from
        -- response key clientCreateUuid via MessageExtension.TryGetString.
        -- The original
        -- WorldMarchDataManager::RemoveCreateMarchRecordTime 0x060032df
        -- reconciles this property with the GUID saved before the send.
        -- A matching reply field and target alone could otherwise come
        -- from an unrelated already-cached march, so require this
        -- independent original native field on the fresh owner march too.
        local native_guid=member(fresh,"clientCreateGuid")
        local native_guid_match=original_create_guid(native_guid) and
            original_create_guid(prior.clientCreateUuid) and
            native_guid:lower()==prior.clientCreateUuid:lower()
        prior.nativeMarchGuidObserved=type(native_guid)=="string" and
            original_create_guid(native_guid) or false
        prior.nativeMarchGuidMatchesRequest=native_guid_match==true
        local ack_correlated=prior.replyCorrelation=="native_clientCreateUuid_owner_march_reply" and
            prior.replyUuid==uuid and prior.replyOwnerUid==uid and prior.replyCount==1
        if fresh and ok_type and kind == prior.kind and prior.sendCount==1 and
           prior.senderReturned==true and prior.senderReturnedFalse~=true and
           ack_correlated and native_guid_match and
           ident(member(fresh,"targetUuid")) == prior.targetUuid and
           (not prior.oldUuid or uuid ~= prior.oldUuid or
            start_time ~= prior.oldStartTime or kind ~= prior.oldKind) then
            local receipt={
                observation="native_owned_formation_attack_target_transition",
                slot=tonumber(fields.slot),marchUuid=uuid,formationUuid=formation,
                targetUuid=prior.targetUuid,targetType=kind,
                targetSource=prior.hoverOrigin and "native_physical_hover_original_selector" or
                    "open_native_formation_selector",
                currentCursorVerified=prior.hoverOrigin==true,
                ownerUid=uid,sourceServerId=tonumber(prior.server),
                priorMarchUuid=prior.oldUuid,priorStartTime=prior.oldStartTime,
                currentStartTime=start_time,requestDispatched=false,
                nativeMarchGuidMatchesRequest=true,
                serverAcknowledgementCorrelated=true,nativeMarchPostStateVerified=true,
                transaction=transaction_view(prior),
                nativeTransaction=transaction_view(prior)}
            if prior.allowItem or prior.allowDiamond then
                local options=owned_speedup_items()
                local chosen
                if prior.allowItem and options then
                    for id in pairs(options) do
                        if not chosen or tonumber(id)<tonumber(chosen) then chosen=id end
                    end
                end
                if chosen then prior.rapidAction,prior.rapidItem="speedupItem",chosen end
                if not prior.rapidAction then return failed("Attack confirmed; no permitted native rapid item or purchase template") end
                prior.receipt=receipt
                return attack_selected(fields,input_clear,uid)
            end
            attacks[fields.slot] = nil
            return {state="confirmed",data=receipt}
        end
        return {state="accepted",data={slot=tonumber(fields.slot),formationUuid=formation,
            awaitingGameConfirmation=true,requestDispatched=false,
            ownerUid=uid,sourceServerId=tonumber(prior.server),
            nativeMarchPostStateVerified=fresh~=nil and kind==prior.kind and
                ident(member(fresh,"targetUuid"))==prior.targetUuid and
                native_guid_match==true,
            serverAcknowledgementCorrelated=ack_correlated,
            nativeTransaction=transaction_view(prior)}}
    end
    if fields.queryOnly == "1" then return failed("No pending native attack to observe") end
    if fields.allowRapidDiamond=="1" then
        return unsupported("Attack-linked diamond rapid requires original native purchase confirmation")
    end
    local selector=selected_window()
    local window=selector and selector.window
    local ctrl=selector and selector.ctrl
    if not selector or not ctrl then
        return failed("Open the native attack formation selector on a current city/army/monster target first")
    end
    local target,denial=native_selector_target(ctrl,uid)
    if not target then return denial end
    local target_uuid,kind=target.uuid,target.kind
    local staffing=formation_staffing(form)
    if staffing.soldierCount==nil or staffing.heroCount==nil then
        return unsupported("Native formation soldier/hero staffing cannot be verified")
    end
    if not staffing.staffedForAttack then
        return failed("Selected native formation must contain soldiers and assigned heroes before attack")
    end
    local old, old_uuid
    if existing ~= nil then
        old,old_uuid = fresh_owned(existing)
        if not old then return failed("Existing squad march owner/formation changed") end
        -- WorldMarchChange carries no source-verifiable request nonce.
        -- This shortcut must not replace an active march when its own ACK
        -- cannot be distinguished from another change of the same march.
        return unsupported("Existing squad march requires a separately correlated native retarget ACK")
    end
    local _,old_kind = invoke(old,"GetMarchTargetType")
    local old_start = number(member(old,"startTime"))
    local method = existing ~= nil and "OnAtkClick" or "OnCheckTime"
    if type(member(ctrl,method)) ~= "function" then return unsupported("Original native attack controller missing") end
    local ok_fresh_form,fresh_form=invoke(forms,"GetOneArmyInfoByIndex",tonumber(fields.slot))
    local fresh_staffing=formation_staffing(fresh_form)
    local fresh_owner=member(fresh_form,"ownerUid")
    local fresh_index=member(fresh_form,"index")
    if not ok_fresh_form or ident(member(fresh_form,"uuid"))~=formation or
       (fresh_owner~=nil and ident(fresh_owner)~=uid) or
       (fresh_index~=nil and number(fresh_index)~=tonumber(fields.slot)) or
       not fresh_staffing.staffedForAttack or
       fresh_staffing.soldierCount~=staffing.soldierCount or
       fresh_staffing.heroCount~=staffing.heroCount then
        return failed("Native formation staffing or identity changed before attack")
    end
    if not observe_create_serializer() then
        return unsupported("Original create-march request nonce observer unavailable")
    end
    if not observe_attack_sender() then return unsupported("Native attack sender observation unavailable") end
    if not observe_attack_replies() then
        return unsupported("Exact original native create-march reply observer missing or displaced; attack suppressed")
    end
    -- Native formation reads or observer initialization can change selection.
    -- Re-resolve the typed target AFTER them, at the last safe boundary
    -- immediately preceding the native controller call.
    local fresh_selector=selected_window()
    local fresh_target=fresh_selector and native_selector_target(fresh_selector.ctrl,uid)
    if input_clear() ~= true or not fresh_selector or
       fresh_selector.window~=window or fresh_selector.ctrl~=ctrl or
       member(window,"Ctrl")~=ctrl or not fresh_target or
       fresh_target.kind~=kind or fresh_target.uuid~=target_uuid or
       fresh_target.point~=target.point or
       fresh_target.selectorServer~=target.selectorServer or
       fresh_target.targetOwnerUid~=target.targetOwnerUid or
       fresh_target.targetServerId~=target.targetServerId or
       fresh_target.ownerAllianceUid~=target.ownerAllianceUid or
       fresh_target.targetAllianceUid~=target.targetAllianceUid then
        return failed("Native selector changed immediately before attack")
    end
    -- The original controller branches on GetOwnerFormationMarch. A separate
    -- action may have started a squad march while the selector/nonce hooks
    -- were being resolved; sending then could take the uncorrelated CHANGE
    -- route instead of the create-march route we can acknowledge by GUID.
    -- Re-read immediately before invocation and retain the one-shot fence
    -- if the current native formation is no longer idle.
    local ok_final_march,final_march=invoke(marches,"GetOwnerFormationMarch",
        member(player,"uid"),form_number,member(player,"allianceId"))
    if not ok_final_march or final_march~=nil then
        return failed("Native squad march appeared or became unreadable before attack")
    end
    -- Authentication and server admission are mutable game state. A native
    -- selector/formation callback may have switched the current account or
    -- server since the beginning of this command; recheck at the send edge.
    local final_uid,final_owner_error=owner_and_focus(fields,input_clear,false)
    if final_uid~=uid then return final_owner_error end
    local final_account=native_account()
    if not final_account or final_account.uid~=uid then
        return failed("Original attack owner changed before sender observation")
    end
    -- Final owner/march lookups may run native callbacks. Check membership
    -- again at the send edge, including a changed own alliance.
    local send_target,send_target_error=native_selector_target(ctrl,uid)
    if not send_target then return send_target_error end
    if send_target.kind~=kind or send_target.uuid~=target_uuid or
       send_target.point~=target.point or
       send_target.ownerAllianceUid~=target.ownerAllianceUid or
       send_target.targetAllianceUid~=target.targetAllianceUid then
        return failed("Target or alliance changed at native attack send edge")
    end
    attacks[fields.slot] = {session=current_session,uid=uid,formation=formation,
        kind=kind,targetUuid=target_uuid,oldUuid=old_uuid,oldKind=old_kind,
        oldStartTime=old_start,sentAt=os.time(),allowItem=fields.allowRapidItem=="1",
        allowDiamond=fields.allowRapidDiamond=="1",sendCount=0,
        hoverOrigin=cursor_open_pending~=nil and
            cursor_open_pending.session==current_session and
            cursor_open_pending.consumed==true and cursor_open_pending.slot==fields.slot,
        server=final_account.server}
    local previous_invocation=active_attack_invocation
    active_attack_invocation=attacks[fields.slot]
    local ok, invocation_result = invoke(ctrl,method,form_number)
    active_attack_invocation=previous_invocation
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
    -- This is an original network *send* witness, not a server ACK. The
    -- desktop hotkey provider must now poll queryOnly to check the exact
    -- original GUID/owner/target poststate; controller-only actions (native
    -- deferred dialogs) cannot claim a packet was sent.
    local dispatched=transaction.sendCount==1 and
        transaction.outboundRoute=="WorldMarchFormationNew" and
        transaction.senderReturned==true and transaction.senderReturnedFalse~=true
    return {state="accepted",data={slot=tonumber(fields.slot),formationUuid=formation,
        targetUuid=target_uuid,targetType=kind,requestDispatched=dispatched,
        ownerUid=uid,sourceServerId=tonumber(attacks[fields.slot].server),
        accepted=true,
        nativeControllerInvoked=true,
        nativeTransaction=transaction_view(attacks[fields.slot]),
        awaitingGameConfirmation=true,targetSource="open_native_formation_selector",
        currentCursorVerified=false}}
end

-- One physical Q/W edge may open a native formation chooser asynchronously.
-- Persist its *original raycast* identity in this VM until the exact native
-- chooser is available. A queryOnly poll may consume that intent ONCE; it
-- must never raycast again, launch a second chooser, or retry a prior send.
local function attack_from_native_hover(fields,input_clear,uid)
    local prior=cursor_open_pending
    if prior and prior.session==current_session then
        if fields.slot~=prior.slot or prior.uid~=uid then
            return failed("Pending hovered chooser belongs to another squad or account")
        end
        local account=native_account()
        if not account or account.uid~=prior.uid or account.server~=prior.server then
            return failed("Native hover chooser changed account or server")
        end
        if prior.consumed then
            if fields.queryOnly=="1" and attacks[fields.slot] then
                return attack_selected(fields,input_clear,uid)
            end
            return failed("Native hover chooser was already consumed once")
        end
        if os.time()-prior.sentAt>20 then
            return failed("Original hover chooser expired; no automatic retry")
        end
        local center=rawget(_G,"DataCenter")
        local ok_form,form=invoke(member(center,"ArmyFormationDataManager"),
            "GetOneArmyInfoByIndex",tonumber(fields.slot))
        if not ok_form or ident(member(form,"uuid"))~=prior.formation or
           (member(form,"ownerUid")~=nil and ident(member(form,"ownerUid"))~=uid) then
            return failed("Pending hover chooser's original squad changed")
        end
        local selector=selected_window()
        if not selector then
            return {state="accepted",data={
                observation="original_hover_waiting_for_native_chooser",
                accepted=true,
                awaitingNativeChooser=true,awaitingGameConfirmation=false,
                slot=tonumber(fields.slot),formationUuid=prior.formation,
                targetPoint=tostring(prior.point),targetUuid=prior.uuid,
                ownerUid=uid,sourceServerId=tonumber(account.server),
                hoverSource=prior.source,
                nativeCursorRaycastVerified=prior.nativeCursorRaycastVerified,
                nativePopupTargetVerified=prior.nativePopupTargetVerified,
                chooserRequestMade=true,
                requestDispatched=false,nativeSelectorConfirmed=false}}
        end
        local target,reason=native_selector_target(selector.ctrl,uid)
        if not target or target.kind~=member(rawget(_G,"MarchTargetType"),prior.kind) or
           target.uuid~=prior.uuid or
           not target.point or tonumber(target.point)~=prior.point or
           (target.selectorServer~=nil and
            tonumber(target.selectorServer)~=tonumber(account.server)) then
            return reason or failed("Original hovered chooser target changed")
        end
        -- A second fresh owned formation check occurs inside attack_selected.
        -- Set the one-shot latch BEFORE calling any original native callback.
        prior.consumed=true
        if input_clear()~=true then
            return failed("Native game input focus changed before pending hovered attack")
        end
        local direct={}
        for key,value in pairs(fields) do direct[key]=value end
        direct.queryOnly=nil
        local result=attack_selected(direct,input_clear,uid)
        if type(result)=="table" and type(result.data)=="table" then
            result.data.ownerUid=uid
            result.data.sourceServerId=tonumber(account.server)
            result.data.nativeCursorRaycastVerified=prior.nativeCursorRaycastVerified
            result.data.nativePopupTargetVerified=prior.nativePopupTargetVerified
            result.data.hoverSource=prior.source
            result.data.awaitingNativeChooser=false
        end
        return result
    end
    if fields.queryOnly=="1" then
        return attack_selected(fields,input_clear,uid)
    end
    if selected_window() then return attack_selected(fields,input_clear,uid) end
    local opened=native_hover_open(fields,input_clear,uid)
    if opened.state~="accepted" then return opened end
    local immediate=attack_from_native_hover(fields,input_clear,uid)
    if immediate.state=="accepted" and immediate.data and
       immediate.data.awaitingNativeChooser then
        return immediate
    end
    return immediate
end

function M.Handle(fields, input_clear)
    if type(fields) ~= "table" then return failed("Invalid rapid/attack command") end
    if fields.action ~= "attack" and fields.action ~= "speedupItem" and
       fields.action ~= "speedupDiamond" then
        return unsupported("Unknown attack/rapid action")
    end
    -- Never accept caller-derived hovered IDs/tiles/pixels for a mutating
    -- action, even if a native chooser is already open. Only the native
    -- cursor and actual original selector may provide those target fields.
    if fields.action=="attack" and
       (fields.targetUuid~=nil or fields.targetType~=nil or
        fields.targetPoint~=nil or fields.samplePixelX~=nil or
        fields.samplePixelY~=nil) then
        return failed("Physical attack target must come from native world hover")
    end
    -- queryOnly normally reads the prior send receipt. Exception: an exact
    -- pending, one-shot original physical-key chooser intent may consume its
    -- matching native Ctrl ONCE after a deferred opening. No new raycast/UI
    -- opening or automatic second send is possible from a bare queryOnly.
    local uid, error = owner_and_focus(fields,input_clear,fields.queryOnly == "1")
    if not uid then return error end
    if fields.action == "attack" then
        return attack_from_native_hover(fields,input_clear,uid)
    end
    if fields.action == "speedupDiamond" then
        return speedup_diamond(fields,input_clear,uid)
    end
    return speedup_item(fields,input_clear,uid)
end

return M
