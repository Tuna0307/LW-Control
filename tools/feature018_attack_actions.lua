-- FEATURE-RECOVERY-018 isolated v26 attack adapter.
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
-- Registration: prime's authenticated Hotkey handler should call
-- M.Handle(fields, frozenHotkey.TextInputClear), for action "attack".
-- No action on require.

local M = { VERSION = "feature018-attack-actions-1" }
local current_session = nil
local attacks = {}

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
    -- V2 BestSelect invokes ArmyFormationInfo:ConscriptSoldier() with no
    -- local-hero mode. That original path gates on HasHero(), whose v26
    -- implementation is simply table.count(self.heroes) > 0, then fills the
    -- nested soldiers[heroSlot].supply values shown by the chooser. canMarch
    -- and heroTotalSoldierCapacity are useful diagnostics but are not the
    -- original chooser's final staffing predicate and can remain stale while
    -- the freshly conscripted chooser already displays a valid 170/170.
    result.heroesReady=result.heroCount~=nil and result.heroCount>0
    result.staffedForAttack=result.soldierCount~=nil and result.soldierCount>0 and
        result.heroesReady
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

local function native_target_alliance(target,is_march)
    local field=is_march and "allianceUid" or "allianceId"
    local value=member(target,field)
    if value~=nil then return value,"native_member_"..field,true end
    if is_march then return nil,"native_member_"..field,false end
    -- World.GetPointInfo may expose a base PointInfo userdata. allianceId is
    -- declared only by BuildPointInfo; Map's native city reader likewise uses
    -- reflection when the direct base userdata member is unavailable.
    local ok_type,native_type=invoke(target,"GetType")
    if not ok_type or tostring(member(native_type,"FullName"))~="BuildPointInfo" then
        return nil,"native_member_"..field,false
    end
    local ok_field,info=invoke(native_type,"GetField","allianceId")
    if not ok_field or info==nil then return nil,"BuildPointInfo.GetField",false end
    local ok_value,native_value=invoke(info,"GetValue",target)
    return native_value,"BuildPointInfo.allianceId_reflection",ok_value
end

local function opaque_alliance_identity(value)
    if value==0 or value=="0" or value=="" then return "0" end
    local id=ident(value)
    if id then return id end
    if type(value)=="string" and value~="" then return value end
    return nil
end

-- Ordinary player bases use the same typed BuildPointInfo for enemy and ally
-- clicks. The original UI decides between AttackCity and AssistanceCity from
-- the native alliance string. Preserve the empty-string guildless protocol
-- value while also accepting opaque nonnumeric alliance IDs for an exact
-- same-alliance comparison.
local function city_alliance_relation(account,target)
    local _,ok_in,in_alliance,ok_uid,native_uid,raw=
        player_alliance_observation(account and account.player)
    local own=opaque_alliance_identity(ok_uid and native_uid or raw)
    if ok_in and type(in_alliance)=="boolean" then
        if in_alliance==true and (own==nil or own=="0") then own=nil
        elseif in_alliance==false then
            if own~=nil and own~="0" then own=nil else own="0" end
        end
    end
    local raw_alliance,reader,read_ok=native_target_alliance(target,false)
    local target_alliance=read_ok and opaque_alliance_identity(raw_alliance) or nil
    local point_type=number(member(target,"PointType")) or number(member(target,"pointType"))
    if point_type~=6 or not read_ok or target_alliance==nil then
        local result=failed("Target alliance identity unknown; city action suppressed")
        result.data={targetAllianceReader=reader,targetAllianceReadSucceeded=read_ok,
            targetAllianceRaw=raw_alliance}
        return nil,result
    end
    local relation
    if target_alliance=="0" then relation="guildless"
    elseif own~=nil and own~="0" and target_alliance==own then relation="same_alliance"
    elseif own==nil then relation="owner_alliance_unknown"
    else relation="other_alliance" end
    return {relation=relation,ownerAllianceUid=own,targetAllianceUid=target_alliance,
        targetAllianceRaw=raw_alliance,targetAllianceReader=reader,
        targetAllianceVerified=true},nil
end

local function ruin_eligibility(account,target)
    local point_type=number(member(target,"PointType")) or number(member(target,"pointType"))
    if point_type~=38 then return nil,failed("Native hovered point is not a destroyed city ruin") end
    local types=rawget(_G,"BuildingTypes")
    local build_id=member(target,"buildId")
    local ruin_a=member(types,"LW_CITY_RUIN")
    local ruin_b=member(types,"LW_CITY_RUIN_1")
    if ruin_a==nil or ruin_b==nil or
       (build_id~=ruin_a and build_id~=ruin_b) then
        return nil,failed("Destroyed city ruin build identity is unsupported or changed")
    end
    local center=rawget(_G,"DataCenter")
    local mastery=member(center,"MasteryManager")
    local ok_show,show=invoke(mastery,"IsShowWorldMasteryBtn")
    if not ok_show or show~=true then
        return nil,failed("Destroyed city ruin is not eligible for the native mastery action")
    end
    local ok_in,in_alliance=invoke(account and account.player,"IsInAlliance")
    if not ok_in or in_alliance~=true then
        return nil,failed("Destroyed city ruin march requires current alliance membership")
    end
    local gather=member(rawget(_G,"MasteryHome"),"Gather")
    if gather==nil then return nil,unsupported("Native MasteryHome.Gather classification unavailable") end
    local ok_data,data=invoke(mastery,"GetData")
    local home=(ok_data and data and member(data,"home_id")) or 0
    if home==gather then
        return nil,failed("Destroyed city ruin march is unavailable from mastery Gather home")
    end
    return {buildId=build_id,homeId=home},nil
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
    local _,_,_,_,native_uid,raw=
        player_alliance_observation(account and account.player)
    -- R15 permits only an explicitly guildless PLAYER target. The owner's
    -- membership cannot change that target's eligibility; keep it diagnostic
    -- without making an unreadable/contradictory owner alliance a new gate.
    local own=alliance_identity(native_uid) or alliance_identity(raw)
    local raw_alliance,reader,read_ok=native_target_alliance(target,is_march)
    local target_alliance=alliance_identity(raw_alliance)
    -- Installed BuildPointInfo::.ctor(Protobuf.WorldPointInfo) RID13427
    -- copies Protobuf.BuildInfo.get_AllianceId() to its string allianceId.
    -- Protobuf.BuildInfo::.ctor RID43782 initializes that field to "" and
    -- set_AllianceId RID43798 rejects null. An explicitly READ empty string
    -- on a typed city is the protocol's no-alliance value; nil is unreadable.
    if not is_march and read_ok and raw_alliance=="" and
       (number(member(target,"PointType")) or number(member(target,"pointType")))==6 then
        target_alliance="0"
    end
    -- City alliance IDs are native strings and may be opaque GUID/hex IDs.
    -- A read nonempty string is membership, even when it is not numeric.
    -- This changes refusal classification only; it can never permit a send.
    if not target_alliance and not is_march and read_ok and
       type(raw_alliance)=="string" and raw_alliance~="" and
       (number(member(target,"PointType")) or number(member(target,"pointType")))==6 then
        target_alliance=raw_alliance
    end
    if not target_alliance then
        local result=failed("Target alliance identity unknown; attack suppressed")
        result.data={targetAllianceReader=reader,targetAllianceReadSucceeded=read_ok,
            targetAllianceRaw=raw_alliance}
        return nil,result
    end
    if target_alliance~="0" then
        local result=failed("Player target belongs to an alliance; only guildless players may be attacked")
        result.data={targetAllianceReader=reader,targetAllianceReadSucceeded=read_ok,
            targetAllianceRaw=raw_alliance}
        return nil,result
    end
    return {ownerAllianceUid=own,targetAllianceUid=target_alliance,
        targetAllianceRaw=raw_alliance,targetAllianceReader=reader,
        targetAllianceVerified=true,sameAlliance=false,
        allianceCheckRequired=true}
end

local function native_selector_target(ctrl,uid,automation_rally)
    local kind=member(ctrl,"targetType")
    local kinds=rawget(_G,"MarchTargetType")
    local is_city=kind~=nil and kind==member(kinds,"ATTACK_CITY")
    local is_assistance=kind~=nil and kind==member(kinds,"ASSISTANCE_CITY")
    local is_army=kind~=nil and kind==member(kinds,"ATTACK_ARMY")
    local is_monster=kind~=nil and kind==member(kinds,"ATTACK_MONSTER")
    local is_resource=kind~=nil and kind==member(kinds,"COLLECT")
    local is_ruin=kind~=nil and kind==member(kinds,"ATTACK_PLAYER_RUIN_BUILDING")
    local is_rally=automation_rally==true and kind~=nil and
        (kind==member(kinds,"RALLY_FOR_BOSS") or kind==member(kinds,"JOIN_RALLY"))
    local uuid,native_uuid=ident(member(ctrl,"targetUuid"))
    if (not uuid and not is_resource) or
       not (is_city or is_assistance or is_army or is_monster or is_resource or is_ruin or is_rally) then
        return nil, failed("Open a native formation selector on a typed city/army/monster/resource/ruin target")
    end
    local point=member(ctrl,"targetPoint")
    local point_id=ident(point)
    if (is_city or is_assistance or is_resource or is_ruin) and not point_id then
        return nil, failed("Native point selector has no original targetPoint")
    end
    local account=native_account()
    if not account or account.uid~=uid then
        return nil,failed("Native selector account changed")
    end
    if is_rally then
        local info=M.AutomationRallyInfo(uuid,kind==member(kinds,"JOIN_RALLY"))
        if not info or info.monster~=true or info.player==true then
            return nil,failed("AFK rally selector does not resolve to a current native monster")
        end
    end
    local selector_server=member(ctrl,"targetServerId")
    local server=number(selector_server)
    if selector_server~=nil and (server==nil or
       (server>0 and tostring(server)~=account.server)) then
        return nil, failed("Native target selector server differs from admitted owner server")
    end
    local world=member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local ok,target
    if is_rally and kind==member(kinds,"JOIN_RALLY") then
        local rally=M.AutomationRallyInfo(uuid,true)
        target=rally and rally.native;ok=target~=nil
    elseif is_resource then ok,target=invoke(world,"GetResourcePointInfoByIndex",tonumber(point_id))
    elseif is_city or is_assistance or is_ruin then ok,target=invoke(world,"GetPointInfo",tonumber(point_id))
    else ok,target=invoke(world,"GetMarch",native_uuid) end
    if not ok or target==nil or (not is_resource and
       not (is_rally and kind==member(kinds,"JOIN_RALLY")) and ident(member(target,"uuid"))~=uuid) then
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
    local alliance,alliance_error
    if is_city or is_assistance then
        alliance,alliance_error=city_alliance_relation(account,target)
        if not alliance then return nil,alliance_error end
        if is_city and alliance.relation~="guildless" then
            local result=failed("Attack-city selector target belongs to an alliance; only guildless player cities may be attacked")
            result.data={targetAllianceReader=alliance.targetAllianceReader,
                targetAllianceReadSucceeded=true,targetAllianceRaw=alliance.targetAllianceRaw}
            return nil,result
        end
        if is_assistance and alliance.relation~="same_alliance" then
            local result=failed("Reinforcement selector is not an exact same-alliance player city")
            result.data={targetAllianceReader=alliance.targetAllianceReader,
                targetAllianceReadSucceeded=true,targetAllianceRaw=alliance.targetAllianceRaw}
            return nil,result
        end
    elseif is_ruin then
        local eligible,ruin_error=ruin_eligibility(account,target)
        if not eligible then return nil,ruin_error end
        alliance={ownerAllianceUid=nil,targetAllianceUid=nil,
            targetAllianceVerified=true,sameAlliance=false,
            allianceCheckRequired=false}
    else
        alliance,alliance_error=target_alliance_guard(account,target,is_army,is_army)
        if not alliance then return nil,alliance_error end
    end
    return {kind=kind,uuid=uuid,point=point,selectorServer=selector_server,
        targetOwnerUid=owner,targetServerId=target_server,
        ownerAllianceUid=alliance.ownerAllianceUid,targetAllianceUid=alliance.targetAllianceUid,
        targetAllianceRaw=alliance.targetAllianceRaw,targetAllianceReader=alliance.targetAllianceReader,
        targetAllianceVerified=true,cityAllianceRelation=alliance.relation,
        observation="open_native_formation_selector_only",
        currentCursorVerified=false,physicalCursorTargetVerified=false,
        requestDispatched=false}
end

-- Observation only. Preserve the original static sender, exact arguments,
-- return values and exceptions. Track ONLY exact pending formation/march,
-- target kind and target UUID on the two source-native attack routes.
local observed_send, observed_send_owner, active_create_send, active_attack_invocation
local function native_create_send_matches(transaction,args,created)
    if not created or not transaction or ident(args[1])~=transaction.formation or
       args[2]~=transaction.kind then
        return false
    end
    local collect=transaction.kind==member(rawget(_G,"MarchTargetType"),"COLLECT")
    if not collect then
        return ident(args[3])==transaction.targetUuid
    end
    -- v26 UIFormationSelectListV2Ctrl.OnCreateClick line 1103 calls
    -- SendCreateMarchMessage(formation,targetType,targetPoint,targetUuid,...).
    -- Ordinary COLLECT initializes the selector UUID as numeric 0 when no
    -- source UUID exists. MarchUtil SendCreateMarchToServer line 2012 therefore
    -- sends 0 as argument 3 and encodes the source-validated destination in
    -- "startPoint;targetPoint".
    -- transaction.targetUuid remains the resource mainIndex for authoritative
    -- poststate matching; it is not the outbound packet's targetUuid field.
    if number(args[3])~=0 or type(args[4])~="string" or not transaction.targetPoint then
        return false
    end
    local start_text,target_text=args[4]:match("^([^;]+);([^;]+)$")
    local start_id=ident(start_text)
    local path_target=ident(target_text)
    local transaction_point=ident(transaction.targetPoint)
    return start_id~=nil and path_target==transaction_point
end
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
                local matches
                if created then
                    matches=native_create_send_matches(transaction,args,true)
                else
                    matches=identity and ident(args[1])==identity and
                        args[2]==transaction.kind and ident(args[3])==transaction.targetUuid
                end
                if matches then
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
        local extra=table.pack(...)
        local create_args=table.pack(formation,kind,target,table.unpack(extra,1,extra.n))
        local returned=table.pack(pcall(original,self,formation,kind,target,
            table.unpack(extra,1,extra.n)))
        local transaction=active_create_send
        if returned[1] and transaction and
           native_create_send_matches(transaction,create_args,true) then
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
    for _,key in ipairs({"formation","kind","targetPoint","targetUuid","sendCount","outboundRoute",
        "targetAllianceUid","targetAllianceRaw","targetAllianceReader",
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
    elseif transaction.nativeMarchGuidMatchesRequest==true then
        view.nonceStatus="matching_server_response_guid_poststate_observed"
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

local function native_attack_target_matches(transaction,march)
    if not transaction or not march then return false end
    if transaction.kind==member(rawget(_G,"MarchTargetType"),"COLLECT") then
        local observed=ident(member(march,"targetPos"))
        local expected=ident(transaction.targetPoint)
        return observed~=nil and expected~=nil and observed==expected
    end
    return ident(member(march,"targetUuid"))==transaction.targetUuid
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
    if attacks[fields.slot] then
        return failed("Native slot has an unresolved attack transaction; editor shortcut fenced")
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
    if attacks[fields.slot] then
        reasons[#reasons+1]="native_attack_transaction_pending"
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
local function release_confirmed_hover(slot)
    -- The chooser is one-shot only for its own send. A confirmed transaction
    -- must not leave a process-lifetime latch that blocks the next physical
    -- key, or a different squad. Late queryOnly polls still cannot open UI.
    if cursor_open_pending and cursor_open_pending.session==current_session and
       cursor_open_pending.slot==slot and cursor_open_pending.consumed==true then
        cursor_open_pending=nil
    end
end
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
    if intent~="resource" and intent~="city" then
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
    local point_type=number(member(point,"PointType"))
    if point_type==6 and (fields.targetKind==nil or intent=="city") then
        local point_id=integer(member(point,"pointIndex"))
        local uuid,native_uuid=ident(member(point,"uuid"))
        local owner,owner_native=ident(member(point,"ownerUid"))
        if not point_id or not uuid or not owner or owner==uid then
            return nil,failed("Hovered player city owner/identity is unreadable or belongs to this account")
        end
        if member(point,"serverId")~=nil and
           tostring(member(point,"serverId"))~=account.server then
            return nil,failed("Hovered player city belongs to another server")
        end
        -- Canonical point and reverse UUID lookup must describe the same
        -- current native city. Never authorize a remembered popup/touch ID.
        local ok_reverse,reverse=invoke(scene,"GetPointInfoByUuid",native_uuid)
        if not ok_reverse or reverse==nil or ident(member(reverse,"uuid"))~=uuid or
           integer(member(reverse,"mainIndex"))~=integer(member(point,"mainIndex")) or
           ident(member(reverse,"ownerUid"))~=owner or
           select(1,native_target_alliance(reverse,false))~=select(1,native_target_alliance(point,false)) or
           (member(reverse,"serverId")~=nil and
            tostring(member(reverse,"serverId"))~=account.server) then
            return nil,failed("Hovered city reverse identity or alliance changed")
        end
        local alliance,denial=city_alliance_relation(account,point)
        if not alliance then return nil,denial end
        local category,target_type
        if alliance.relation=="guildless" then
            category,target_type="guildless_player_city","ATTACK_CITY"
        elseif alliance.relation=="same_alliance" then
            category,target_type="same_alliance_player_city","ASSISTANCE_CITY"
        elseif alliance.relation=="owner_alliance_unknown" then
            return nil,failed("Own alliance identity is unknown; same-alliance reinforcement cannot be proven")
        else
            return nil,failed("Player target belongs to another alliance; only guildless players may be attacked")
        end
        return {category=category,targetType=target_type,point=point_id,
            uuid=native_uuid,uuidText=uuid,server=account.server,uid=account.uid,
            targetOwnerUid=owner,targetOwnerUidNative=owner_native,
            ownerAllianceUid=alliance.ownerAllianceUid,targetAllianceUid=alliance.targetAllianceUid,
            targetAllianceRaw=alliance.targetAllianceRaw,targetAllianceReader=alliance.targetAllianceReader,
            scene=scene,pixelX=cursor.pixelX,pixelY=cursor.pixelY,
            source="UIUtil.OnClickWorld+World.GetPointInfo+GetPointInfoByUuid",
            nativeCursorRaycastVerified=true},nil
    end
    if point_type==38 and (fields.targetKind==nil or intent=="ruin") then
        local point_id=integer(member(point,"pointIndex"))
        local uuid,native_uuid=ident(member(point,"uuid"))
        if not point_id or not uuid then
            return nil,failed("Hovered destroyed city ruin identity is unreadable")
        end
        if member(point,"serverId")~=nil and
           tostring(member(point,"serverId"))~=account.server then
            return nil,failed("Hovered destroyed city ruin belongs to another server")
        end
        local ok_reverse,reverse=invoke(scene,"GetPointInfoByUuid",native_uuid)
        if not ok_reverse or reverse==nil or ident(member(reverse,"uuid"))~=uuid or
           integer(member(reverse,"mainIndex"))~=integer(member(point,"mainIndex")) or
           member(reverse,"buildId")~=member(point,"buildId") or
           (member(reverse,"serverId")~=nil and
            tostring(member(reverse,"serverId"))~=account.server) then
            return nil,failed("Hovered destroyed city ruin reverse identity changed")
        end
        local eligible,denial=ruin_eligibility(account,point)
        if not eligible then return nil,denial end
        local eligible_reverse,reverse_denial=ruin_eligibility(account,reverse)
        if not eligible_reverse then return nil,reverse_denial end
        return {category="destroyed_city_ruin",targetType="ATTACK_PLAYER_RUIN_BUILDING",
            point=point_id,uuid=native_uuid,uuidText=uuid,server=account.server,uid=account.uid,
            targetOwnerUid=ident(member(point,"ownerUid")),
            scene=scene,pixelX=cursor.pixelX,pixelY=cursor.pixelY,
            source="UIUtil.OnClickWorld+World.GetPointInfo+GetPointInfoByUuid",
            nativeCursorRaycastVerified=true},nil
    end
    if intent=="city" or intent=="ruin" or point_type~=7 then
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
    -- Original V2 selector normalizes an absent COLLECT targetUuid to numeric
    -- zero. MarchUtil sends that zero targetUid and carries this physical
    -- resource point in the final path endpoint; retain mainIndex here as the
    -- independently validated resource identity for poststate.
    return {category="ordinary_resource",targetType="COLLECT",point=point_id,
        uuid=nil,uuidText=tostring(integer(member(resource,"mainIndex"))),
        server=account.server,uid=account.uid,
        ownerAllianceUid=alliance.ownerAllianceUid,targetAllianceUid=alliance.targetAllianceUid,
        scene=scene,pixelX=cursor.pixelX,pixelY=cursor.pixelY,
        source="UIUtil.OnClickWorld+World.GetResourcePointInfoByIndex"},nil
end

local function native_hover_open(fields,input_clear,uid)
    if fields.targetKind~=nil and fields.targetKind~="monster" and
       fields.targetKind~="resource" and fields.targetKind~="city" and
       fields.targetKind~="ruin" then
        return failed("Original hover targetKind must be monster, resource, city or ruin")
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
       fresh.targetOwnerUid~=target.targetOwnerUid or
       fresh.targetOwnerUidNative~=target.targetOwnerUidNative or
       fresh.source~=target.source or
       fresh.targetAllianceUid~=target.targetAllianceUid or
       fresh.pixelX~=target.pixelX or fresh.pixelY~=target.pixelY or
       input_clear()~=true then
        return failed("Native hover target or text focus changed before original selector")
    end
    local account=native_account()
    if not account or account.uid~=uid or account.server~=target.server then
        return failed("Original hover selector account changed before dispatch")
    end
    local pending={session=current_session,uid=uid,server=account.server,
        kind=fresh.targetType,point=fresh.point,uuid=fresh.uuidText,
        targetOwnerUid=fresh.targetOwnerUid,targetAllianceUid=fresh.targetAllianceUid,
        slot=slot,formation=formation,consumed=false,sentAt=os.time(),
        source=fresh.source,
        nativeCursorRaycastVerified=fresh.nativeCursorRaycastVerified==true,
        nativePopupTargetVerified=fresh.nativePopupTargetVerified==true}
    -- Prepare(mode=cursorOpen) is a no-send proof helper and intentionally has
    -- no fixed squad. It may open the original chooser for inspection, but it
    -- must not leave an unconsumable slot=nil latch that rejects the next real
    -- physical Q/W edge after that diagnostic chooser is closed.
    if fields.mode=="cursorOpen" then
        cursor_open_pending=nil
    else
        cursor_open_pending=pending
    end
    local ok,result
    if fresh.targetType=="ATTACK_MONSTER" then
        -- UIWorldPointBtn root/13 line1491 uses (kind,point,uuid,-1,1).
        ok,result=pcall(opener,kind,fresh.point,fresh.uuid,-1,1)
    elseif fresh.targetType=="ATTACK_CITY" then
        -- v26 UIWorldPointBtn.OnBtnClick root/13 sourceLine1678:
        -- (ATTACK_CITY, pointId, uuid, -1, 1, nil, targetServerId).
        ok,result=pcall(opener,kind,fresh.point,fresh.uuid,-1,1,nil,
            tonumber(account.server))
    elseif fresh.targetType=="ASSISTANCE_CITY" then
        -- UIWorldPointBtn.OnBtnClick AssistanceCity source1777-1784 calls
        -- WorldBattleUtil.TrySendAssistanceMarch with the exact MainCity
        -- assistance payload. Require that current UI mode explicitly exposes
        -- this native path; otherwise refuse instead of bypassing its legacy
        -- UIFormationAssistance fallback.
        local battle=rawget(_G,"WorldBattleUtil")
        local enabled=member(battle,"EnableShowWorldAssistanceInfo")
        local send=member(battle,"TrySendAssistanceMarch")
        local assistance=member(rawget(_G,"AssistanceType"),"MainCity")
        local ok_enabled,show=pcall(enabled)
        if type(enabled)~="function" or type(send)~="function" or
           assistance==nil or not ok_enabled or show~=true then
            return unsupported("Original same-alliance AssistanceCity march route is unavailable")
        end
        ok,result=pcall(send,{uuid=fresh.uuid,playerUid=fresh.targetOwnerUidNative,
            pointId=fresh.point,asType=assistance,isThroneCity=false,
            isCrossServerThrone=false})
    elseif fresh.targetType=="ATTACK_PLAYER_RUIN_BUILDING" then
        -- UIWorldPointBtn.OnBtnClick source3004 exact five-argument ruin tuple.
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
        targetAllianceUid=fresh.targetAllianceUid,
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
        local direct_reply_correlated=prior.replyCorrelation=="native_clientCreateUuid_owner_march_reply" and
            prior.replyUuid==uuid and prior.replyOwnerUid==uid and prior.replyCount==1
        -- The native WorldMarch.clientCreateGuid is not a locally generated
        -- preview field. v26 WorldMarch.UpdateFromSFS reads clientCreateUuid
        -- from the server response that WorldMarchFormationNewMessage passes
        -- into World.HandleFormationMarch. Some live message dispatchers cache
        -- the original HandleMessage delegate before our diagnostic wrapper is
        -- installed, so the wrapper can miss a real reply even though the
        -- authoritative native march carries the exact request GUID. Treat the
        -- fresh owned march's matching response-derived GUID plus target/state
        -- transition as the server correlation; retain the direct wrapper as
        -- additional evidence when it fires.
        local response_guid_correlated=native_guid_match==true
        if fresh and ok_type and kind == prior.kind and prior.sendCount==1 and
           prior.senderReturned==true and prior.senderReturnedFalse~=true and
           response_guid_correlated and
           native_attack_target_matches(prior,fresh) and
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
                targetAllianceUid=prior.targetAllianceUid,targetAllianceRaw=prior.targetAllianceRaw,
                targetAllianceReader=prior.targetAllianceReader,
                priorMarchUuid=prior.oldUuid,priorStartTime=prior.oldStartTime,
                currentStartTime=start_time,requestDispatched=false,
                nativeMarchGuidMatchesRequest=true,
                serverAcknowledgementCorrelated=true,nativeMarchPostStateVerified=true,
                serverReplyDerivedPostState=true,
                directReplyObserverCorrelated=direct_reply_correlated,
                transaction=transaction_view(prior),
                nativeTransaction=transaction_view(prior)}
            attacks[fields.slot] = nil
            release_confirmed_hover(fields.slot)
            return {state="confirmed",data=receipt}
        end
        return {state="accepted",data={slot=tonumber(fields.slot),formationUuid=formation,
            awaitingGameConfirmation=true,requestDispatched=false,
            ownerUid=uid,sourceServerId=tonumber(prior.server),
            nativeMarchPostStateVerified=fresh~=nil and kind==prior.kind and
                native_attack_target_matches(prior,fresh) and
                native_guid_match==true,
            serverAcknowledgementCorrelated=response_guid_correlated and fresh~=nil and
                kind==prior.kind and native_attack_target_matches(prior,fresh),
            directReplyObserverCorrelated=direct_reply_correlated,
            nativeTransaction=transaction_view(prior)}}
    end
    if fields.queryOnly == "1" then return failed("No pending native attack to observe") end
    local selector=selected_window()
    local window=selector and selector.window
    local ctrl=selector and selector.ctrl
    if not selector or not ctrl then
        return failed("Open the native attack formation selector on a current city/army/monster target first")
    end
    local target,denial=native_selector_target(ctrl,uid,fields.automationRally==true)
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
    local fresh_target=fresh_selector and native_selector_target(fresh_selector.ctrl,uid,fields.automationRally==true)
    if input_clear() ~= true or not fresh_selector or
       fresh_selector.window~=window or fresh_selector.ctrl~=ctrl or
       member(window,"Ctrl")~=ctrl or not fresh_target or
       fresh_target.kind~=kind or fresh_target.uuid~=target_uuid or
       fresh_target.point~=target.point or
       fresh_target.selectorServer~=target.selectorServer or
       fresh_target.targetOwnerUid~=target.targetOwnerUid or
       fresh_target.targetServerId~=target.targetServerId or
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
    local send_target,send_target_error=native_selector_target(ctrl,uid,fields.automationRally==true)
    if not send_target then return send_target_error end
    if send_target.kind~=kind or send_target.uuid~=target_uuid or
       send_target.point~=target.point or
       send_target.targetAllianceUid~=target.targetAllianceUid then
        return failed("Target or alliance changed at native attack send edge")
    end
    attacks[fields.slot] = {session=current_session,uid=uid,formation=formation,
        kind=kind,targetPoint=target.point,targetUuid=target_uuid,
        targetAllianceUid=send_target.targetAllianceUid,targetAllianceRaw=send_target.targetAllianceRaw,
        targetAllianceReader=send_target.targetAllianceReader,
        oldUuid=old_uuid,oldKind=old_kind,
        oldStartTime=old_start,sentAt=os.time(),sendCount=0,
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
        local function waiting_for_chooser()
            return {state="accepted",data={
                observation="original_hover_waiting_for_native_chooser",
                accepted=true,
                awaitingNativeChooser=true,awaitingGameConfirmation=false,
                slot=tonumber(fields.slot),formationUuid=prior.formation,
                targetPoint=tostring(prior.point),targetUuid=prior.uuid,
                targetOwnerUid=prior.targetOwnerUid,
                targetAllianceUid=prior.targetAllianceUid,
                ownerUid=uid,sourceServerId=tonumber(account.server),
                hoverSource=prior.source,
                nativeCursorRaycastVerified=prior.nativeCursorRaycastVerified,
                nativePopupTargetVerified=prior.nativePopupTargetVerified,
                chooserRequestMade=true,
                requestDispatched=false,nativeSelectorConfirmed=false}}
        end
        if not selector then return waiting_for_chooser() end
        -- OpenWindow can publish Ctrl before InitData assigns the target.
        -- Wait only for missing/zero native identity fields whose populated
        -- siblings still agree with the retained physical-hover intent. A
        -- concrete different kind/UUID/tile/server remains an immediate error.
        -- This path neither reacquires the cursor nor opens/sends anything;
        -- the original sentAt deadline above still applies on every poll.
        local kind=member(selector.ctrl,"targetType")
        local point=member(selector.ctrl,"targetPoint")
        local uuid=member(selector.ctrl,"targetUuid")
        local server=member(selector.ctrl,"targetServerId")
        local expected_kind=member(rawget(_G,"MarchTargetType"),prior.kind)
        local is_resource=prior.kind=="COLLECT"
        local missing_kind=kind==nil or kind==0
        local missing_point=point==nil or tostring(point)=="0"
        local missing_uuid=uuid==nil or tostring(uuid)=="0"
        local known_fields_match=(missing_kind or kind==expected_kind) and
            (missing_point or integer(point)==prior.point) and
            (missing_uuid or (not is_resource and ident(uuid)==prior.uuid)) and
            (server==nil or tostring(server)=="0" or tostring(server)==prior.server)
        if known_fields_match and (missing_kind or missing_point or
            (not is_resource and missing_uuid)) then
            return waiting_for_chooser()
        end
        local target,reason=native_selector_target(selector.ctrl,uid)
        if not target or target.kind~=member(rawget(_G,"MarchTargetType"),prior.kind) or
           target.uuid~=prior.uuid or
           not target.point or tonumber(target.point)~=prior.point or
           (prior.targetOwnerUid~=nil and target.targetOwnerUid~=prior.targetOwnerUid) or
           (prior.targetAllianceUid~=nil and target.targetAllianceUid~=prior.targetAllianceUid) or
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

-- R20: authenticated automation has a different source of intent from physical
-- hover. Only this private Lua API admits a freshly typed native MONSTER. It
-- shares the Q/W/E/R serializer, send fence and server-derived GUID receipt.
-- No public hotkey payload can select this path.
function M.AutomationMonsterInfo(uuid)
    local world=member(member(rawget(_G,"CS"),"SceneManager"),"World")
    local ok,target=invoke(world,"GetMarch",tonumber(uuid))
    local ok_type,kind=invoke(target,"GetMarchType")
    local monster_kind=member(rawget(_G,"NewMarchType"),"MONSTER")
    local ok_monster,is_monster=invoke(target,"IsMonsterOrBoss")
    local monster_id=integer(member(target,"monsterId"))
    local ok_template,template=invoke(member(rawget(_G,"DataCenter"),"MonsterTemplateManager"),
        "GetMonsterTemplate",monster_id)
    if not ok or ident(member(target,"uuid"))~=tostring(uuid) or not ok_type or
       not monster_id or not ok_template or template==nil or
       (kind~=monster_kind and not (ok_monster and is_monster==true)) then return nil end
    local ok_troop,troop=invoke(world,"GetTroop",tonumber(uuid))
    local ok_pos,pos=invoke(troop,"GetPosition")
    local transform=member(rawget(_G,"SceneUtils"),"WorldToTileIndex")
    local ok_point,point=false,nil
    if ok_troop and ok_pos and type(transform)=="function" then
        ok_point,point=pcall(transform,pos,member(rawget(_G,"ForceChangeScene"),"World"))
    end
    if not ok_point or not integer(point) then return nil end
    local special=number(member(template,"special"))
    if special==nil then return nil end
    local name_key=member(template,"name")
    local ok_name,name=invoke(template,"GetName")
    if not ok_name then
        ok_name,name=invoke(member(member(rawget(_G,"CS"),"GameEntry"),"Localization"),"GetString",name_key)
    end
    return {uuid=tostring(uuid),monster=true,player=false,monsterId=monster_id,
        monsterType=number(member(template,"type")),special=special,
        level=number(member(template,"level")),nameKey=name_key,
        name=ok_name and tostring(name) or tostring(name_key or ""),
        pointIndex=point,worldBoss=kind~=monster_kind,rallyOnly=special~=0,
        native=target}
end

function M.AutomationSend(fields, input_clear, target, continuous)
    local uid,denial=owner_and_focus(fields,input_clear,false)
    if not uid then return denial end
    if attacks[fields.slot] then return attack_selected(fields,input_clear,uid) end
    local fresh=target and M.AutomationMonsterInfo(target.uuid)
    if not fresh or fresh.player or fresh.worldBoss or fresh.special~=0 or
       fresh.monsterId~=target.monsterId or fresh.pointIndex~=target.pointIndex then
        return failed("AFK target is not the same fresh ordinary native monster")
    end
    local account=native_account()
    local center=rawget(_G,"DataCenter")
    local forms=member(center,"ArmyFormationDataManager")
    local manager=member(center,"WorldMarchDataManager")
    local ok_form,form=invoke(forms,"GetOneArmyInfoByIndex",tonumber(fields.slot))
    local formation,native_form=ident(member(form,"uuid"))
    local ok_march,march=invoke(manager,"GetOwnerFormationMarch",member(account.player,"uid"),
        native_form,member(account.player,"allianceId"))
    if not ok_form or not formation or not ok_march or march~=nil or
       (member(form,"ownerUid")~=nil and ident(member(form,"ownerUid"))~=uid) then
        return failed("AFK assigned formation is busy or has no verified owner")
    end
    -- Original BestSelect lines 2123-2125 uses the formation's own conscription
    -- method. It allocates the current owned soldiers; no invented troop count.
    local ok_conscript=invoke(form,"ConscriptSoldier")
    if not ok_conscript or not formation_staffing(form).staffedForAttack then
        return failed("AFK assigned formation has no staffed native soldiers/heroes")
    end
    local ok_energy,energy=invoke(forms,"GetCurStaminaByUuid",native_form)
    local march_util=rawget(_G,"MarchUtil")
    if not ok_energy or not number(energy) or type(member(march_util,"OnAttackMonster"))~="function" or
       not observe_create_serializer() or not observe_attack_sender() or not observe_attack_replies() then
        return unsupported("Native Monster AFK route/confirmation observer unavailable")
    end
    fresh=M.AutomationMonsterInfo(target.uuid)
    local final_uid=owner_and_focus(fields,input_clear,false)
    local final_ok,final_march=invoke(manager,"GetOwnerFormationMarch",member(account.player,"uid"),
        native_form,member(account.player,"allianceId"))
    if final_uid~=uid or not fresh or fresh.special~=0 or fresh.worldBoss or
       fresh.monsterId~=target.monsterId or not final_ok or final_march~=nil then
        return failed("AFK owner, monster or assigned squad changed before send")
    end
    local kind=member(rawget(_G,"MarchTargetType"),"ATTACK_MONSTER")
    local auto=member(rawget(_G,"MarchAutoBackType"),continuous and "NoBack" or "Back")
    if kind==nil or auto==nil then return unsupported("Native monster route enums absent") end
    local transaction={session=current_session,uid=uid,formation=formation,kind=kind,
        targetPoint=fresh.pointIndex,targetUuid=fresh.uuid,server=account.server,
        sentAt=os.time(),sendCount=0,automation=true}
    attacks[fields.slot]=transaction
    active_attack_invocation=transaction
    -- Original MarchUtil.OnAttackMonster(formation, target, stamina,
    -- isFormation, autoBackHome, withoutMessageTip), lines 430-539.
    local ok,error=pcall(march_util.OnAttackMonster,native_form,fresh.native,energy,true,auto,true)
    active_attack_invocation=nil
    if not ok or transaction.sendCount~=1 or transaction.senderReturned~=true then
        local result=failed("Native Monster AFK send was not confirmed; no automatic retry")
        result.data={nativeTransaction=transaction_view(transaction),detail=not ok and tostring(error) or nil}
        return result
    end
    return {state="accepted",data={requestDispatched=true,nativeTransaction=transaction_view(transaction)}}
end

function M.AutomationObserve(fields,input_clear)
    local uid,denial=owner_and_focus(fields,input_clear,true)
    if not uid then return denial end
    local prior=attacks[fields.slot]
    if prior and prior.automationRetarget then
        if prior.session~=current_session or prior.uid~=uid or prior.sendCount~=1 or
           prior.senderError or prior.senderReturnedFalse or prior.unattributedMatchingSendObserved then
            return failed("Native continuous-march transaction lost its single-send fence")
        end
        local ok,march=invoke(member(rawget(_G,"DataCenter"),"WorldMarchDataManager"),"GetMarch",tonumber(prior.oldUuid))
        local type_ok,kind=invoke(march,"GetMarchTargetType")
        if ok and ident(member(march,"uuid"))==prior.oldUuid and
           ident(member(march,"ownerUid"))==uid and ident(member(march,"ownerFormationUuid"))==prior.formation and
           type_ok and kind==prior.kind and native_attack_target_matches(prior,march) and
           number(member(march,"startTime"))~=prior.oldStartTime then
            attacks[fields.slot]=nil
            return {state="confirmed",data={marchUuid=prior.oldUuid,slot=tonumber(fields.slot),
                targetUuid=prior.targetUuid,serverAcknowledgementCorrelated=true,
                serverAppliedChangePostState=true,correlation="native_single_change_march_target_startTime",
                nativeTransaction=transaction_view(prior)}}
        end
        return {state="accepted",data={accepted=true,awaitingGameConfirmation=true}}
    end
    return attack_selected(fields,input_clear,uid)
end

function M.AutomationContinue(fields,input_clear,target,expected_march,join)
    local uid,denial=owner_and_focus(fields,input_clear,false)
    if not uid then return denial end
    if attacks[fields.slot]then return M.AutomationObserve(fields,input_clear)end
    local account=native_account();local center=rawget(_G,"DataCenter")
    local ok_form,form=invoke(member(center,"ArmyFormationDataManager"),"GetOneArmyInfoByIndex",tonumber(fields.slot))
    local ok,march=invoke(member(center,"WorldMarchDataManager"),"GetOwnerFormationMarch",member(account.player,"uid"),
        member(form,"uuid"),member(account.player,"allianceId"))
    local fresh=target and (join and M.AutomationRallyInfo(target.uuid,true) or M.AutomationMonsterInfo(target.uuid))
    local type_ok,old_kind=invoke(march,"GetMarchTargetType")
    local kinds=rawget(_G,"MarchTargetType")
    if not ok_form or not ok or ident(member(march,"uuid"))~=expected_march or
       ident(member(march,"ownerUid"))~=uid or not fresh or (not join and (fresh.special~=0 or fresh.worldBoss)) or
       fresh.monsterId~=target.monsterId or not type_ok or
       (old_kind~=member(kinds,"STATE") and old_kind~=member(kinds,"BACK_HOME")) then
        return failed("Continuous attack requires the exact completed owned monster march")
    end
    local energy_ok,energy=invoke(member(center,"ArmyFormationDataManager"),"GetCurStaminaByUuid",member(form,"uuid"))
    if not energy_ok or not observe_attack_sender() or not observe_attack_replies() then
        return failed("Native continuous march route unavailable")end
    local transaction={session=current_session,uid=uid,formation=ident(member(form,"uuid")),
        kind=member(kinds,join and "JOIN_RALLY" or "ATTACK_MONSTER"),targetPoint=fresh.pointIndex,targetUuid=fresh.uuid,
        server=account.server,oldUuid=expected_march,oldKind=old_kind,
        oldStartTime=number(member(march,"startTime")),sentAt=os.time(),sendCount=0,automationRetarget=true}
    if owner_and_focus(fields,input_clear,false)~=uid then return failed("Continuous attack account changed")end
    attacks[fields.slot]=transaction;active_attack_invocation=transaction
    local util=rawget(_G,"MarchUtil")
    local sent
    if join then
        sent=pcall(util.OnJoinRally,tonumber(expected_march),member(kinds,"RALLY_FOR_BOSS"),tonumber(fresh.uuid),fresh.pointIndex,energy)
    else
        sent=pcall(util.OnAttackMonster,tonumber(expected_march),fresh.native,energy,false,
            member(rawget(_G,"MarchAutoBackType"),"NoBack"),true)
    end
    active_attack_invocation=nil
    if not sent or transaction.sendCount~=1 or transaction.senderReturned~=true then
        return failed("Continuous march send uncertain; no automatic retry")end
    return {state="accepted",data={requestDispatched=true,nativeTransaction=transaction_view(transaction)}}
end

function M.AutomationRallyInfo(uuid,join)
    if not join then return M.AutomationMonsterInfo(uuid) end
    local manager=member(rawget(_G,"DataCenter"),"AllianceWarDataManager")
    local ok,war=invoke(manager,"GetAllianceWarDataByUuid",tonumber(uuid))
    local leader=member(war,"leaderMarch")
    local account=native_account()
    local alliance=account and member(account.player,"allianceId")
    local eligible,can_join=invoke(manager,"CheckJoinAllianceWarByWarData",war)
    if not ok or not war or not account or alliance==nil or tostring(alliance)=="0" or tostring(alliance)=="" or
       tostring(member(war,"attackAllianceId"))~=tostring(alliance) or
       ident(member(war,"uuid"))~=tostring(uuid) or not ident(member(leader,"uuid")) or ident(member(leader,"ownerUid"))==account.uid or
       not eligible or can_join~=true then return nil end
    if member(war,"type")~=member(rawget(_G,"AllianceTeamType"),"ATTACK_BOSS") then return nil end
    local moving=member(rawget(_G,"MarchStatus"),"MOVING")
    if moving~=nil and member(leader,"status")==moving then return nil end
    -- AllianceWarInfo.targetUid is the MONSTER TEMPLATE ID, not a march UUID.
    -- UIAllianceWarMainTableCtrl.GetWarItemData lines 310-318 reads exactly
    -- this template for ATTACK_BOSS. The targetUuid field is the world UUID.
    -- Rally data remains authoritative outside the camera's AOI.
    local monster_id=integer(member(war,"targetUid"))
    local template_ok,template=invoke(member(rawget(_G,"DataCenter"),"MonsterTemplateManager"),"GetMonsterTemplate",monster_id)
    if not template_ok or not template or not monster_id or not ident(member(war,"targetUuid")) then return nil end
    local name_key=member(template,"name")
    local name_ok,name=invoke(member(member(rawget(_G,"CS"),"GameEntry"),"Localization"),"GetString",name_key)
    local info={monster=true,player=false,monsterId=monster_id,monsterUuid=ident(member(war,"targetUuid")),
        uuid=tostring(uuid),rally=true,joinable=true,worldBoss=true,rallyOnly=true,
        monsterType=number(member(template,"type")),special=number(member(template,"special")),
        level=number(member(template,"level")),nameKey=name_key,name=name_ok and tostring(name) or name_key}
    info.pointIndex=number(member(leader,"startId"))
    if not info.pointIndex then return nil end
    info.leaderUid=ident(member(leader,"ownerUid"));info.leaderName=member(leader,"ownerName")
    local members=member(war,"memberList")
    if type(members)=="table" then
        info.memberCount=1;info.ownerIsMember=false
        for _,m in pairs(members)do
            if ident(member(m,"uuid"))~=ident(member(leader,"uuid")) then info.memberCount=info.memberCount+1 end
            if ident(member(m,"ownerUid"))==account.uid then info.ownerIsMember=true end
        end
    end
    local departure=number(member(leader,"endTime"))
    info.departureTime=departure and (departure>1000000000000 and departure/1000 or departure) or nil
    info.leaderHeroCount=type(member(leader,"heroes"))=="table" and #leader.heroes or nil
    info.native=leader
    return info
end

function M.AutomationRallyOpen(fields,input_clear,target,join)
    local uid,denial=owner_and_focus(fields,input_clear,false)
    if not uid then return denial end
    local fresh=target and M.AutomationRallyInfo(target.uuid,join)
    if not fresh or fresh.monsterId~=target.monsterId then return failed("Native monster rally target changed")end
    local ok_form,form=invoke(member(rawget(_G,"DataCenter"),"ArmyFormationDataManager"),
        "GetOneArmyInfoByIndex",tonumber(fields.slot))
    if not ok_form then return failed("Native assigned rally squad absent")end
    local account=native_account()
    local ok_busy,busy=invoke(member(rawget(_G,"DataCenter"),"WorldMarchDataManager"),
        "GetOwnerFormationMarch",member(account.player,"uid"),member(form,"uuid"),member(account.player,"allianceId"))
    if not ok_busy or busy~=nil then return failed("Assigned rally squad is busy")end
    local conscript=invoke(form,"ConscriptSoldier")
    if not conscript or not formation_staffing(form).staffedForAttack then return failed("Assigned rally squad lacks native troops")end
    local kind=member(rawget(_G,"MarchTargetType"),join and "JOIN_RALLY" or "RALLY_FOR_BOSS")
    local open=member(rawget(_G,"UIUtil"),"OpenFormationSelectUI")
    if kind==nil or type(open)~="function" then return unsupported("Native rally chooser unavailable")end
    if owner_and_focus(fields,input_clear,false)~=uid then return failed("Rally account changed before chooser")end
    -- Original UIUtil helper selects current V2/new native controller. Its own
    -- InitRallyTime selects the event-specific rally duration and checks.
    local rally_kind=join and member(rawget(_G,"MarchTargetType"),"RALLY_FOR_BOSS") or nil
    local ok=pcall(open,1,kind,fresh.pointIndex,tonumber(fresh.uuid),nil,
        member(rawget(_G,"MarchAutoBackType"),"Back"),rally_kind,tonumber(account.server),fresh.special,tonumber(fields.slot))
    if not ok then return failed("Native rally chooser did not open")end
    return {state="accepted",data={accepted=true,requestDispatched=false,awaitingNativeChooser=true}}
end

function M.AutomationRallyDispatch(fields,input_clear,target,join)
    local uid,denial=owner_and_focus(fields,input_clear,false)
    if not uid then return denial end
    if attacks[fields.slot] then return attack_selected(fields,input_clear,uid)end
    local fresh=target and M.AutomationRallyInfo(target.uuid,join)
    if not fresh or fresh.monsterId~=target.monsterId then return failed("Native monster rally no longer eligible")end
    local selected=selected_window()
    if not selected then return {state="accepted",data={accepted=true,awaitingNativeChooser=true}}end
    local kind=member(rawget(_G,"MarchTargetType"),join and "JOIN_RALLY" or "RALLY_FOR_BOSS")
    if ident(member(selected.ctrl,"targetUuid"))~=fresh.uuid or member(selected.ctrl,"targetType")~=kind then
        return failed("Native rally chooser changed target")
    end
    fields.automationRally=true
    return attack_selected(fields,input_clear,uid)
end

function M.Handle(fields, input_clear)
    if type(fields) ~= "table" then return failed("Invalid attack command") end
    if fields.action ~= "attack" then
        return unsupported("Unknown attack action")
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
    return attack_from_native_hover(fields,input_clear,uid)
end

return M
