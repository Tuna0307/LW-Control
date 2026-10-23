-- R20 current-client adapter. Native routes are RECOVERED from v26 bytecode;
-- scheduling/retry intervals are OWN_DESIGN. Never constructs a player target.
local M={VERSION='feature018-afk-native-1'}
local engine=require('DataCenter.Global.LWBridgeFeature018AfkEngine')
local attack=require('DataCenter.Global.LWBridgeFeature018AttackActions')
local potion=require('DataCenter.Global.LWBridgeFeature018StaminaEngine')
local drill=require('DataCenter.Global.LWBridgeFeature018DrillEngine')
local drill_acquired_at=0
local bridge,array,owner,search,last_pump
local target_scan_cache,target_scan_cache_active
local jobs,seen,join_wait,continuous_ready,joined,kicked={},{},{},{},{},{}
local function get(o,k) if o==nil then return nil end;local ok,v=pcall(function()return o[k]end);if ok then return v end;return nil end
local function call(o,k,...) local f=get(o,k);if type(f)~='function' then return false end;return pcall(f,o,...) end
local function id(v) if v==nil then return nil end;local s=tostring(v);if s:match('^[1-9]%d*$') then return s end end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function player()return get(rawget(_G,'LuaEntry'),'Player')end
local function world()return get(get(rawget(_G,'CS'),'SceneManager'),'World')end
local function now()return os.time()end
local function result(data)return {state='confirmed',data=data}end
local function failed(reason)return {state='failed',errorCode='GAME_PRECONDITION_FAILED',error=reason}end
local function account()
    local p=player();local uid=id(get(p,'uid'));local server
    for _,method in ipairs({'GetSourceServerId','GetSelfServerId','GetCurServerId'}) do
        local ok,v=call(p,method);v=id(v)
        if not ok or not v or (server and server~=v) then return nil end;server=v
    end
    if not uid then return nil end;return {uid=uid,server=server}
end
local function owned(o)
    local a=account()
    local token=bridge and get(bridge,'Feature018OwnerToken')
    return a and o and a.uid==o.uid and a.server==o.server and
        type(token)=='function' and token()==o.token
end
local function form(slot)
    local ok,f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
    if not ok or not id(get(f,'uuid')) then return nil end;return f
end
local function march(slot)
    local f=form(slot);local p=player()
    if not f then return false end
    return call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(p,'uid'),get(f,'uuid'),get(p,'allianceId'))
end
local function each(values,fn)
    if type(values)=='table' then for _,v in pairs(values)do fn(get(v,'Value') or v)end;return end
    local ok,e=call(values,'GetEnumerator');if not ok then return end
    for _=1,20000 do local next_ok,more=call(e,'MoveNext');if not next_ok or not more then break end
        local v=get(e,'Current');fn(get(v,'Value') or v)
    end
end
local function clean_target(t)
    if not t then return nil end
    local out={};for k,v in pairs(t)do if k~='native' then out[k]=v end end
    local ok,home=call(player(),'GetMainWorldPos')
    if ok and tonumber(home) then
        local a,b=tonumber(home),tonumber(t.pointIndex)
        out.distance=math.sqrt((a%1000-b%1000)^2+(math.floor(a/1000)-math.floor(b/1000))^2)
    end
    return out
end
local function available_monsters()
    local out={};local ok,list=call(center('WorldMarchDataManager'),'GetAllMarches')
    if ok then each(list,function(v)
        local uuid=id(get(v,'uuid'));local mid=tonumber(get(v,'monsterId'))
        if uuid and mid and mid>0 then
            local info=attack.AutomationMonsterInfo(uuid)
            if info then out[#out+1]=clean_target(info) end
        end
    end)end
    return out
end
local function fields(slot,captured)
    local out={};for k,v in pairs(captured.fields)do out[k]=v end;out.slot=tostring(slot);return out
end
local function clear_input()
    -- Native text-edit state only; no Windows key polling or input.
    local hotkey=require('DataCenter.Global.LWBridgeFeature018HotkeyGame')
    local f=get(hotkey,'TextInputClear')
    if type(f)=='function' then return f()==true end
    return false
end
local function install_search_observer()
    if search and search.wrapper then return true end
    local ok,class=pcall(require,'Net.Msgs.FindMonsterMessage')
    local previous=ok and get(class,'HandleMessage')
    if type(previous)~='function' then return false end
    search=search or {}
    local wrapper=function(self,response,...)
        local returned=table.pack(pcall(previous,self,response,...))
        if search.pending and owner and owned(owner) then
            search.pending=false;search.error=get(response,'errorCode')
            search.uuid=id(get(response,'uuid'));search.point=tonumber(get(response,'pointId'))
            search.received=now()
        end
        if not returned[1] then error(returned[2],0)end
        return table.unpack(returned,2,returned.n)
    end
    class.HandleMessage=wrapper;search.wrapper=wrapper;search.class=class;search.previous=previous
    return true
end
local function search_monster(p,level)
    if not install_search_observer() then return 'Native FindMonster reply unavailable' end
    if search.pending then return 'waiting_search' end
    if search.nextAt and now()<search.nextAt then return 'waiting_search' end
    if not owned(owner) then return 'account_changed' end
    local ok,home=call(player(),'GetMainWorldPos')
    local net=rawget(_G,'SFSNetwork');local route=get(rawget(_G,'MsgDefines'),'FindMonster')
    if not ok or not tonumber(home) or not route or type(get(net,'SendMessage'))~='function' then return 'Native monster search unavailable' end
    search.pending=true;search.nextAt=now()+10;search.level=level;search.profileId=p.id;search.uuid=nil
    search.moved=nil;search.received=nil;search.error=nil
    -- Original UISearchCtrl.OnSearchClick and FindMonsterMessage.OnCreate.
    local sent,err=pcall(net.SendMessage,route,home,level,false,nil,p.monsterType or 0)
    if not sent then search.pending=false;search.error=tostring(err)end
    return 'waiting_search'
end
local function targets(p,level)
    if p.kind=='join' then
        local out={};local ok,groups=call(center('AllianceWarDataManager'),'GetAllCanJoinAllianceWarData')
        if ok then each(groups,function(group)each(group,function(war)
            local uuid=id(get(war,'uuid'))
            local info=uuid and attack.AutomationRallyInfo(uuid,true)
            if info then
                if joined[uuid] and info.ownerIsMember==false and info.departureTime and info.departureTime>now()then kicked[uuid]=true end
                info.kicked=kicked[uuid]==true
                local key=p.id..':'..uuid;join_wait[key]=join_wait[key] or {}
                local ready=engine.JoinReady(p.joinRestrictions,info,join_wait[key],now())
                if ready then out[#out+1]=clean_target(info)end
            end
        end)end)end
        return out,'waiting_join_delay'
    end
    if search and search.pending and now()>search.nextAt+15 then search.pending=false;search.error='Native search timed out' end
    if search and search.uuid and search.profileId==p.id then
        local info=attack.AutomationMonsterInfo(search.uuid)
        if not info and not search.moved and owned(owner) then
            local goto_util=rawget(_G,'GoToUtil');local move=get(goto_util,'MoveToWorldMarchAndOpen')
            if type(move)=='function' then
                search.moved=true;pcall(move,search.point,tonumber(search.uuid),tonumber(owner.server),0)
            end
        end
    end
    local all
    if target_scan_cache_active then
        -- One native AFK scheduler pump can ask for targets once per eligible
        -- profile/squad. The underlying world-march snapshot is common to all
        -- of those read-only filters, while Send() independently revalidates
        -- account, squad state and exact target immediately before mutation.
        -- Cache only this raw snapshot for this engine.Pump invocation; Catalog,
        -- Drill and the next one-second pump continue to observe live state.
        if target_scan_cache==nil then target_scan_cache=available_monsters() end
        all=target_scan_cache
    else
        all=available_monsters()
    end
    local found={}
    for _,t in ipairs(all)do
        if (not seen[t.uuid] or seen[t.uuid]<=now()) and engine.Matches(p,t,level)then found[#found+1]=t end
    end
    table.sort(found,function(a,b)return (a.distance or math.huge)<(b.distance or math.huge)end)
    if #found>0 then return found end
    if p.searchable and p.kind=='farm' and not p.rally then
        return {},search_monster(p,level or p.minLevel or 1)
    end
    return {},'waiting_nearby_target'
end
local function free(slot,p)
    if jobs[slot] then return false end
    for _,pending in pairs(jobs)do if pending.phase=='chooser' then return false end end
    local ok,m=march(slot)
    if ok and m==nil then continuous_ready[slot]=nil;return form(slot)~=nil end
    if not ok or not p or not (p.continuousAttack or p.continuousJoin) or
       not continuous_ready[slot] or id(get(m,'uuid'))~=continuous_ready[slot] then return false end
    local type_ok,kind=call(m,'GetMarchTargetType')
    local kinds=rawget(_G,'MarchTargetType')
    return type_ok and (kind==get(kinds,'STATE') or kind==get(kinds,'BACK_HOME'))
end
local function stamina(slot,p)
    if potion.IsPending() then return nil,nil end
    local f=form(slot)
    local ok,v=call(center('ArmyFormationDataManager'),'GetCurStaminaByUuid',get(f,'uuid'))
    local util=rawget(_G,'MarchUtil');local kinds=rawget(_G,'MarchTargetType')
    local kind=get(kinds,p.kind=='join' and 'JOIN_RALLY' or p.rally and 'RALLY_FOR_BOSS' or 'ATTACK_MONSTER')
    local cost_ok,cost=false,nil
    if type(get(util,'GetCostStaminaByTargetType'))=='function' and kind~=nil then
        cost_ok,cost=pcall(util.GetCostStaminaByTargetType,kind,nil,get(f,'uuid'))
    end
    return ok and tonumber(v) or nil,cost_ok and tonumber(cost) or nil
end
local function stamina_snapshot()
    local energy
    for slot=1,4 do
        local f=form(slot)
        if f then
            local ok,value=call(center('ArmyFormationDataManager'),'GetCurStaminaByUuid',get(f,'uuid'))
            if ok then energy=tonumber(value);break end
        end
    end
    if not energy then return nil,'Native stamina is unavailable' end
    -- Current FormationStaminaSlider.OnAddClick uses the Energy resource-lack
    -- catalog. The old UIFormationAddStamina StaminaItem category is obsolete
    -- for today's bag items and can return empty despite owned energy stock.
    local energy_kind=get(rawget(_G,'ResLackContextType'),'Energy')
    local use_item=get(rawget(_G,'LWResourceLackGetWay'),'UseItem')
    local goods_util=rawget(_G,'GoodsUtil')
    if energy_kind==nil or use_item==nil or type(get(goods_util,'GetGoodsReturnItemCount'))~='function' then
        return nil,'Current native Energy item catalog unavailable'
    end
    local ok,list=call(center('LWResourceLackManager'),'GetSpecialResWay',energy_kind)
    if not ok or list==nil then return nil,'Current native Energy item list unavailable' end
    local items,rejected,raw_count,known={},{},0,{}
    each(list,function(route)
        raw_count=raw_count+1
        if get(route,'tips')~=use_item then return end -- Never buy/claim/shop.
        local route_id=tonumber(get(route,'para1'))
        if not route_id or known[route_id] then return end;known[route_id]=true
        local item_ok,item=call(center('ItemData'),'GetItemById',route_id)
        if not item_ok or not item then return end
        local item_id,count=tonumber(get(item,'itemId')),tonumber(get(item,'count'))
        local uuid=get(item,'uuid');uuid=uuid~=nil and tostring(uuid) or nil
        local recovery_ok,recovery=pcall(goods_util.GetGoodsReturnItemCount,route_id)
        recovery=recovery_ok and tonumber(recovery) or nil
        if item_id==route_id and item_id>0 and item_id%1==0 and count and count>=0 and count%1==0 and uuid and uuid~='0' and #uuid>0 and #uuid<=128 and
           recovery and recovery>0 and recovery<10000000 then
            items[#items+1]={id=item_id,uuid=uuid,count=count,recovery=recovery,staminaItem=true}
        elseif #rejected<20 then
            rejected[#rejected+1]={id=item_id,count=count,routeItemId=route_id,recovery=recovery,
                uuidType=type(get(item,'uuid')),itemType=type(item)}
        end
    end)
    return {stamina=energy,items=items,rawItemCount=raw_count,rejectedItems=rejected,
        source='LWResourceLackManager.GetSpecialResWay.Energy.UseItem',nativeListType=type(list)}
end
potion.Bind({Owned=owned,Now=now,Snapshot=stamina_snapshot,Use=function(o,item)
    if not owned(o) then return false,'Stamina item owner changed' end
    local fresh,error=stamina_snapshot()
    if not fresh then return false,error end
    local same=false
    for _,candidate in ipairs(fresh.items)do
        if candidate.id==item.id and candidate.uuid==item.uuid and candidate.count==item.count and
           candidate.recovery==item.recovery and candidate.count>0 then same=true end
    end
    if not same then return false,'Native stamina item stock changed before use' end
    local net=rawget(_G,'SFSNetwork');local route=get(rawget(_G,'MsgDefines'),'ItemUse')
    if type(get(net,'SendMessage'))~='function' or route==nil then return false,'Native ItemUse unavailable'end
    if not owned(o) then return false,'Stamina account changed at send boundary'end
    -- Current LWResourceLackCell.OnMultUseClick line 2808. No buy route.
    local ok,value=pcall(net.SendMessage,route,{uuid=item.uuid,num=1})
    return ok and value~=false,not ok and tostring(value) or nil
end})
-- Owner 2026-10-22: join a rally directly, without opening the team screen. Same call the
-- screen's Dispatch makes (UIFormationSelectListNewCtrl 1352): MarchUtil.OnJoinRally(formation,
-- rallyType, rallyUuid, rallyPoint, stamina) -> StartMarch(JOIN_RALLY, point, rallyUuid, -1, formation).
local function direct_join(slot,t)
    local f=form(slot);if not f then return nil,'Assigned rally squad absent' end
    local ok_busy,busy=march(slot);if not ok_busy or busy~=nil then return nil,'Assigned rally squad is busy' end
    local fresh=attack.AutomationRallyInfo(t.uuid,true)
    if not fresh or fresh.monsterId~=t.monsterId then return nil,'Native monster rally target changed' end
    call(f,'ConscriptSoldier')
    if not tonumber(get(f,'totalSoldierNum'))or tonumber(get(f,'totalSoldierNum'))<=0 then return nil,'Assigned rally squad lacks native troops' end
    local join=get(rawget(_G,'MarchUtil'),'OnJoinRally');local types=rawget(_G,'MarchTargetType')
    if type(join)~='function'or not get(types,'JOIN_RALLY')then return nil,'Native rally join unavailable' end
    local _,energy=call(center('ArmyFormationDataManager'),'GetCurStaminaByUuid',get(f,'uuid'))
    local formation=id(get(f,'uuid'))
    -- From here a send may have happened: an error is kept as an uncertain tombstone, never retried.
    local ok,err=pcall(join,get(f,'uuid'),get(types,'RALLY_FOR_BOSS'),tonumber(fresh.uuid),fresh.pointIndex,tonumber(energy)or 0)
    if not ok then
        jobs[slot]={activity='monsterSweep',uncertain=true,target=t,error=tostring(err)}
        error('Native rally join outcome uncertain: '..tostring(err))
    end
    jobs[slot]={activity='monsterSweep',target=t,sentAt=now(),phase='directAck',join=true,formation=formation}
    return tostring(slot)
end
local function send(o,slot,p,t)
    if not owned(o) or not free(slot,p) then return nil,'Assigned squad/account changed' end
    local f=fields(slot,o)
    local _,existing=march(slot)
    if p.kind=='join' or p.rally then
        if p.kind=='join' then
            local fresh=attack.AutomationRallyInfo(t.uuid,true)
            if fresh then fresh.kicked=kicked[t.uuid]==true end
            local ready,reason=engine.JoinReady(p.joinRestrictions,fresh,join_wait[p.id..':'..t.uuid] or {},now())
            if not ready then return nil,reason end
        end
        local receipt
        if existing~=nil and p.kind=='join' and p.continuousJoin then
            receipt=attack.AutomationContinue(f,clear_input,t,continuous_ready[slot],true)
            if receipt.state=='accepted' and get(receipt.data,'requestDispatched') then
                jobs[slot]={activity=p.activity or 'monsterSweep',fields=f,target=t,sentAt=now(),phase='ack',join=true,receipt=receipt}
                return tostring(slot)
            end
            return nil,receipt.error or 'Continuous rally join not dispatched'
        end
        if p.kind=='join'then
            local token,why=direct_join(slot,t)
            if token then jobs[slot].activity=p.activity or 'monsterSweep' end
            return token,why
        end
        receipt=attack.AutomationRallyOpen(f,clear_input,t,p.kind=='join')
        if receipt.state=='failed' or receipt.state=='unsupported' then return nil,receipt.error end
        jobs[slot]={activity=p.activity or 'monsterSweep',fields=f,target=t,sentAt=now(),phase='chooser',join=p.kind=='join',receipt=receipt}
        return tostring(slot)
    end
    local receipt
    if existing~=nil then receipt=attack.AutomationContinue(f,clear_input,t,continuous_ready[slot])
    else receipt=attack.AutomationSend(f,clear_input,t,p.continuousAttack==true)end
    if receipt.state~='accepted' or not get(receipt.data,'requestDispatched') then
        -- A producer can have sent even when its exception escapes. Retain a
        -- tombstone and stop; the scheduler must never retry an ambiguous send.
        jobs[slot]={activity=p.activity or 'monsterSweep',uncertain=true,fields=f,target=t,receipt=receipt}
        error(receipt.error or 'Uncertain native monster send')
    end
    jobs[slot]={activity=p.activity or 'monsterSweep',fields=f,target=t,sentAt=now(),phase='ack',receipt=receipt}
    seen[t.uuid]=now()+60
    return tostring(slot)
end
local function observe(job,allow_send)
    local native=jobs[job.slot]
    if not native then return {failed=true,error='Native pending march missing'} end
    if native.uncertain then return {failed=true,error='Native send outcome uncertain; restart suppressed'} end
    if native.phase=='directAck' then
        -- Confirmed when the squad's own march to the rally appears.
        local ok,m=march(job.slot)
        local type_ok,kind=call(m,'GetMarchTargetType')
        if ok and m~=nil and id(get(m,'uuid'))and (not type_ok or kind~=get(rawget(_G,'MarchTargetType'),'BACK_HOME'))then
            native.phase='battle';native.marchUuid=id(get(m,'uuid'))
            native.ack={serverAcknowledgementCorrelated=true,directJoin=true,marchUuid=native.marchUuid}
            joined[native.target.uuid]=true
            return {step='joining',acknowledged=true,receipt=native.ack}
        elseif now()-native.sentAt>30 then
            return {failed=true,error='Rally join was not confirmed by a native march'}
        end
        return {step='joining'}
    end
    if native.phase=='chooser' then
        if allow_send==false then return {step='waiting_event'}end
        if now()-native.sentAt>20 then return {failed=true,error='Native rally chooser timed out; no retry'}end
        local receipt=attack.AutomationRallyDispatch(native.fields,clear_input,native.target,native.join)
        if receipt.state=='failed' or receipt.state=='unsupported' then
            -- A dispatch exception may follow a real native send. Retain the
            -- reservation across Stop/reconfigure instead of calling it an
            -- unsent chooser and allowing an automatic replay.
            native.uncertain=true;native.phase='uncertain'
            return {failed=true,error=receipt.error}
        end
        if get(receipt.data,'requestDispatched')==true then native.phase='ack';native.receipt=receipt end
        return {step=native.join and 'joining' or 'attacking'}
    end
    if native.phase=='ack' then
        local reply=attack.AutomationObserve(native.fields,clear_input)
        if reply.state=='confirmed' and get(reply.data,'serverAcknowledgementCorrelated')==true then
            native.phase='battle';native.marchUuid=reply.data.marchUuid;native.ack=reply.data
            if native.join then joined[native.target.uuid]=true end
        elseif reply.state=='failed' or now()-native.sentAt>30 then
            return {failed=true,error=reply.error or 'Native march acknowledgement timed out'}
        end
        return {step='attacking',acknowledged=native.phase=='battle',receipt=native.ack}
    end
    local ok,m=march(job.slot)
    if not ok then return {step='waiting_squad'}end
    if m~=nil and id(get(m,'uuid'))~=native.marchUuid then
        return {failed=true,error='Assigned squad march replaced before battle/return'}
    end
    local fight_ok,fought=call(m,'HasFightMonster')
    local type_ok,kind=call(m,'GetMarchTargetType')
    local back=get(rawget(_G,'MarchTargetType'),'BACK_HOME')
    if fight_ok and fought==true then native.fought=true end
    if type_ok and kind==back then native.returning=true end
    if m==nil or native.returning or native.fought then
        local receipt={targetUuid=native.target.uuid,monsterId=native.target.monsterId,
            level=native.target.level,slot=job.slot,marchUuid=native.marchUuid,
            nativeMarchGuidMatchesRequest=native.ack.nativeMarchGuidMatchesRequest==true,serverAcknowledgementCorrelated=true,
            serverAppliedChangePostState=native.ack.serverAppliedChangePostState==true,
            battleFlagObserved=native.fought==true,returnObserved=native.returning==true,
            formationIdle=m==nil,completedAt=now(),ack=native.ack}
        jobs[job.slot]=nil
        if m~=nil then continuous_ready[job.slot]=native.marchUuid end
        return {completed=true,receipt=receipt,step='returning'}
    end
    return {step='attacking',acknowledged=true,receipt=native.ack}
end
engine.Bind({Owned=owned,Free=free,Stamina=stamina,Targets=targets,Send=send,Observe=observe,
    CancelUnsent=function(job)
        local pending=jobs[job.slot]
        if pending and pending.activity~='allianceDrill' and pending.phase=='chooser' then jobs[job.slot]=nil;return true end
        return false
    end,
    MaxLevel=function(p)local ok,v=call(center('MonsterTemplateManager'),'GetMonsterMaxLevelbyType',p.monsterType or 0);return ok and tonumber(v) or p.minLevel end,
    Stop=function()
        for slot,job in pairs(jobs)do if job.phase=='chooser' and job.activity~='allianceDrill' then jobs[slot]=nil end end
    end})
-- The native event manager supplies both stage and millisecond deadline.
-- This reader never starts/signs/donates to an event. Only an armed active
-- engine may use the manager's normal JumpToDrill to acquire its own target.
local function drill_snapshot(captured,acquire)
    local dm=center('AllyDrillDataManager')
    local ok,stage,deadline=call(dm,'GetCurStageAndCountDown')
    local attack_stage=get(rawget(_G,'AllyDrillStage'),'AttackStage')
    local time_ok,timer=call(rawget(_G,'UITimeManager'),'GetInstance')
    local clock_ok,server_time=call(time_ok and timer or nil,'GetServerTime')
    local info_ok,info=call(dm,'GetActInfo');local data=info_ok and get(info,'data')
    local point,server=tonumber(get(data,'bossPointId')),id(get(data,'bossServerId'))
    local active=ok and attack_stage~=nil and stage==attack_stage and tonumber(deadline) and
        clock_ok and tonumber(server_time) and tonumber(deadline)>tonumber(server_time)
    local out={active=active==true,stage=tostring(stage),deadline=tonumber(deadline),
        serverTime=tonumber(server_time),pointIndex=point,serverId=server,rallies={},ownRallyExists=false,readOnly=not acquire}
    if not out.active then return out end
    if not point or not captured or server~=captured.server then
        out.error='Native Drill target server/point unavailable or changed';return out
    end
    local special=rawget(_G,'WorldMonsterSpecialType') or {}
    local matches={}
    for _,target in ipairs(available_monsters())do
        local is_drill=false
        for _,key in ipairs({'AllyDrill','AllyDrillHugeSandWorm','AllyDrillRoadHog'})do
            if special[key]~=nil and target.special==special[key]then is_drill=true end
        end
        if is_drill and target.pointIndex==point then matches[#matches+1]=target end
    end
    if #matches==1 then out.target=matches[1]
    elseif acquire and owned(captured) and now()>=drill_acquired_at then
        drill_acquired_at=now()+30
        call(dm,'JumpToDrill')
    end
    if not out.target then out.error='Waiting for the unique native Drill monster';return out end
    for slot=1,4 do
        local present,m=march(slot)
        local type_ok,kind=call(m,'GetMarchTargetType')
        if present and type_ok and kind==get(rawget(_G,'MarchTargetType'),'RALLY_FOR_BOSS') and
           id(get(m,'targetUuid'))==out.target.uuid then out.ownRallyExists=true end
    end
    local rallies_ok,groups=call(center('AllianceWarDataManager'),'GetAllCanJoinAllianceWarData')
    if rallies_ok then each(groups,function(group)each(group,function(war)
        local uuid=id(get(war,'uuid'));local rally=uuid and attack.AutomationRallyInfo(uuid,true)
        if rally and rally.monsterUuid==out.target.uuid then
            if joined[uuid] and rally.ownerIsMember==false and rally.departureTime and rally.departureTime>now()then kicked[uuid]=true end
            rally.kicked=kicked[uuid]==true;out.rallies[#out.rallies+1]=clean_target(rally)
        end
    end)end)end
    return out
end
local function drill_join_ready(config,rally)
    local key='allianceDrill:'..rally.uuid;join_wait[key]=join_wait[key] or {}
    return engine.JoinReady(config.joinRestrictions,rally,join_wait[key],now())
end
drill.Bind({Owned=owned,Snapshot=drill_snapshot,Free=function(slot)return free(slot,{})end,
    Stamina=function(slot,role)return stamina(slot,{kind=role=='member' and 'join' or 'farm',rally=true})end,
    JoinReady=drill_join_ready,Observe=observe,
    CancelUnsent=function(job)
        local pending=jobs[job.slot]
        if pending and pending.activity=='allianceDrill' and pending.phase=='chooser' then
            jobs[job.slot]=nil;return true
        end
        return false
    end,
    Send=function(captured,slot,role,target,config)
        local fresh=drill_snapshot(captured,false)
        if not fresh.active or not fresh.target or
           (role=='leader' and fresh.target.uuid~=target.uuid) or
           (role=='member' and fresh.target.uuid~=target.monsterUuid)then return nil,'Drill ended/target changed before send'end
        local profile={id='allianceDrill',activity='allianceDrill',kind=role=='member' and 'join' or 'farm',
            rally=true,joinRestrictions=config.joinRestrictions}
        return send(captured,slot,profile,target)
    end})
function M.StartDrill(request)
    local a=account();local token=bridge and bridge.Feature018OwnerToken()
    if not a or request.authorizedUid~=a.uid or not token then return failed('Drill account admission absent')end
    local json=require('rapidjson');local ok,config=pcall(json.decode,request.confirmedAfkConfig or '{}')
    if not ok or type(config)~='table' then return failed('Confirmed Drill settings absent')end
    local captured={uid=a.uid,server=a.server,token=token,fields={}}
    for _,k in ipairs({'authorizedUid','profileId','launchSessionId','challenge','gamePid'})do captured.fields[k]=request[k]end
    local started,error=drill.Start(captured,config.allianceDrill)
    if not started then return failed(error)end
    return M.DrillStatus()
end
function M.StopDrill()drill.Stop();return M.DrillStatus()end
function M.DrillStatus()
    local status=drill.Status()
    local a=account();status.event=drill_snapshot(a,false)
    if array then array(status.rows);array(status.receipts);array(status.event.rallies)end
    status.observation='native_alliance_drill_runtime';status.readOnly=true
    return result(status)
end
function M.Start(request)
    local a=account();local token=bridge and bridge.Feature018OwnerToken()
    if not a or request.authorizedUid~=a.uid or not token then return failed('Fresh native account admission absent')end
    local json=require('rapidjson');local ok,config=pcall(json.decode,request.confirmedAfkConfig or '')
    if not ok or type(config)~='table' or type(config.strategies)~='table' then return failed('Confirmed profile config absent')end
    if config.enabled~=true then return failed('Monster AFK master switch is off')end
    if engine.IsRunning() then
        if not owned(owner) then return failed('Running AFK belongs to another account/session')end
        local status,error=engine.Configure(config)
        if not status then return failed(error)end
        return M.Status()
    end
    for _,job in pairs(jobs)do
        if job.activity~='allianceDrill' then return failed('A prior native march is still in flight; wait for reconciliation')end
    end
    owner={uid=a.uid,server=a.server,token=token,fields={}}
    for _,k in ipairs({'authorizedUid','profileId','launchSessionId','challenge','gamePid'})do owner.fields[k]=request[k]end
    seen={};join_wait={}
    local status,error=engine.Start(owner,config)
    if not status then return failed(error)end
    local potion_ok,potion_config=pcall(json.decode,request.confirmedStaminaConfig or '{}')
    if potion_ok and type(potion_config)=='table' and potion_config.enabled==true then
        local started,reason=potion.Start(owner,potion_config)
        if not started then engine.Stop(reason);return failed(reason)end
    end
    return M.Status()
end
function M.Stop()engine.Stop();return M.Status()end
function M.Configure(request)
    local status=engine.Status()
    if status.state=='idle' or status.state=='stopped' then return M.Start(request)end
    if not owner or not owned(owner) or request.authorizedUid~=owner.uid then return failed('AFK configuration account changed')end
    local json=require('rapidjson');local ok,config=pcall(json.decode,request.confirmedAfkConfig or '{}')
    if not ok then return failed('Confirmed AFK configuration unavailable')end
    if not config.enabled then return M.Stop()end
    local configured,error=engine.Configure(config)
    if not configured then return failed(error)end
    return M.Status()
end
function M.Status()
    local data=engine.Status();data.owner=nil;data.config=nil;data.pending=nil
    if array then array(data.rows);array(data.receipts)end
    data.observation='native_monster_afk_runtime';data.readOnly=true
    data.allianceDrill=M.DrillStatus().data
    data.staminaPotion=potion.Status()
    if array then array(data.staminaPotion.receipts)end
    return result(data)
end
function M.StartStamina(request)
    local a=account();local token=bridge and bridge.Feature018OwnerToken()
    if not a or request.authorizedUid~=a.uid or not token then return failed('Stamina account admission absent')end
    local json=require('rapidjson');local ok,config=pcall(json.decode,request.confirmedStaminaConfig or '{}')
    if not ok then return failed('Confirmed stamina settings unavailable')end
    local captured={uid=a.uid,server=a.server,token=token}
    local started,error=potion.Start(captured,config)
    if not started then return failed(error)end
    return M.StaminaStatus()
end
function M.StopStamina()potion.Stop();return M.StaminaStatus()end
function M.StaminaStatus()
    local status=potion.Status();local snapshot,error=stamina_snapshot()
    status.inventory=snapshot;status.inventoryError=error;status.readOnly=true
    status.observation='native_stamina_item_runtime'
    if array then array(status.receipts);if snapshot then array(snapshot.items);array(snapshot.rejectedItems)end end
    return result(status)
end
function M.Catalog()
    local options,groups={},{}
    local templates=center('MonsterTemplateManager')
    -- Native searchable ordinary types are supplied by the game enum. Do not
    -- offer player/city/resource options or infer targets from frontend fixtures.
    for _,kind in pairs(rawget(_G,'LWWorldMonsterType') or {})do
        if type(kind)=='number' then
            local ok,t=call(templates,'GetMonsterTemplatebyLevelType',1,kind)
            if ok and t and tonumber(get(t,'special'))==0 then
                local key=get(t,'name');local unique=tostring(kind)..':'..tostring(key)
                if not groups[unique]then
                    local name_ok,name=call(get(get(rawget(_G,'CS'),'GameEntry'),'Localization'),'GetString',key)
                    local max_ok,max=call(templates,'GetMonsterMaxLevelbyType',kind)
                    groups[unique]=true;options[#options+1]={key=unique,group='normal',name=name_ok and tostring(name) or tostring(key),
                        monsterNameKey=key,monsterType=kind,monsterSpecial=0,monsterIds={},
                        source='search',action='attack',rally=false,joinOnly=false,searchable=true,
                        minLevel=1,maxLevel=max_ok and tonumber(max) or 1,attackMinLevel=1,attackMaxLevel=max_ok and tonumber(max) or 1}
                end
            end
        end
    end
    -- Current visible event/world bosses add native-only rally options. A
    -- saved option may be undiscovered later; no fixture fallback is used.
    for _,t in ipairs(available_monsters())do
        if t.rallyOnly or t.worldBoss then
            local unique=tostring(t.monsterType)..':'..tostring(t.nameKey)..':'..tostring(t.special)..':rally'
            if not groups[unique]then groups[unique]=true;options[#options+1]={
                key=unique,group='leader',name=t.name,monsterNameKey=t.nameKey,monsterType=t.monsterType,
                monsterSpecial=t.special,monsterIds={},source='map',action='rally',rally=true,
                joinOnly=false,searchable=false,minLevel=t.level,maxLevel=t.level}
            end
        end
    end
    if array then
        for _,option in ipairs(options)do array(option.monsterIds)end
    end
    if array then array(options)end
    local unlocked,members={},{}
    for slot=1,4 do if form(slot)then unlocked[#unlocked+1]=slot end end
    local members_ok,all_members=call(center('AllianceMemberDataManager'),'GetAllMember')
    if members_ok then each(all_members,function(m)
        local uid=id(get(m,'uid'));local name=get(m,'name')
        if uid and type(name)=='string' then members[#members+1]={uid=uid,name=name}end
    end)end
    if array then array(unlocked);array(members)end
    return result({options=options,monsters=available_monsters(),unlockedSquads=unlocked,
        allianceMembers={ready=members_ok and all_members~=nil,members=members,selfUid=id(get(player(),'uid')),online=true},readOnly=true})
end
function M.Pump()
    if last_pump==now()then return end;last_pump=now()
    for uuid,expires in pairs(seen)do if expires<=now()then seen[uuid]=nil end end
    if owner and owned(owner) and not engine.IsRunning() then
        for slot in pairs(jobs) do
            local ok,m=march(slot)
            if ok and m==nil and not jobs[slot].uncertain and jobs[slot].activity~='allianceDrill' then jobs[slot]=nil end
        end
    end
    local potion_ok,potion_error=pcall(potion.Pump)
    if not potion_ok then potion.Fail(potion_error)end
    local drill_ok,drill_error=pcall(drill.Pump)
    if not drill_ok then drill.Fail(tostring(drill_error))end
    target_scan_cache=nil;target_scan_cache_active=true
    local ok,error=pcall(engine.Pump)
    target_scan_cache_active=false;target_scan_cache=nil
    if not ok then engine.Stop(tostring(error))end
end
function M.CleanupAfterLeaseLoss()
    local token=bridge and get(bridge,'Feature018SessionToken')
    if type(token)=='function' and token()~=nil then engine.Pause('account_changed')
    else engine.Stop('Session lease lost');potion.Stop();drill.Stop()end
end
function M.Register(feature,overview)
    bridge=overview;array=feature.Array
    rawset(_G,'LWBridgeFeature018AfkNative',M)
    feature.RegisterHandler('feature018_afk_status',M.Status)
    feature.RegisterHandler('feature018_afk_configure',M.Configure)
    feature.RegisterHandler('monster_catalog_options',M.Catalog)
    feature.RegisterHandler('feature018_stamina_status',M.StaminaStatus)
    feature.RegisterHandler('feature018_drill_status',M.DrillStatus)
end
return M
