-- R30 Alliance Gathering Dispatch. RECOVERED original settings: off, squads [1,2] in
-- priority order. RECOVERED v27 route (WorldAlMinePanel / UIWorldPointBtn 1044-1056 ->
-- UIFormationSelectListNewCtrl 1356): MarchUtil.StartMarch(COLLECT_ALLIANCE_BUILD_RESOURCE,
-- minePoint, mineUuid, -1, 0, formationUuid, autoBackHome, nil, 0, serverId).
-- The game allows one of a player's squads per alliance mine (tip 129004); kept here.
-- Selection, cadence and confirmation are OWN_DESIGN. No work on require.
local M={}
local ctx
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function static(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(...)end
local function enum(n,k)local v=get(rawget(_G,n),k);if v==nil then error('Native '..n..'.'..k..' is unavailable')end;return v end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function player()return get(rawget(_G,'LuaEntry'),'Player')end
local function id(v)local s=tostring(v);if s:match('^[1-9]%d*$')then return s end end
local function account()
    local p=player();local uid=id(get(p,'uid'));if not uid then error('Native game UID unavailable')end
    local server=id(call(p,'GetSourceServerId'))
    if not server or id(call(p,'GetCurServerId'))~=server then error('Alliance gathering requires the home server')end
    return uid,server
end
local function context()
    local uid,server=account();local key=uid..'/'..server
    if ctx and ctx.key~=key then M.Stop('allianceGather');error('The game account changed')end
    if not ctx then
        local manager=call(rawget(_G,'EventManager'),'GetInstance')
        local event=enum('EventId','UpdateAllAllianceMineList')
        ctx={key=key,uid=uid,server=server,manager=manager,event=event,version=0}
        ctx.fn=function()if ctx then ctx.version=ctx.version+1 end end
        call(manager,'AddListener',event,ctx.fn)
    end
    return ctx
end
local function own_march(c,mine)
    local marches=call(center('WorldMarchDataManager'),'GetOwnerMarches',get(player(),'uid'),get(player(),'allianceId'))
    local wanted=enum('MarchTargetType','COLLECT_ALLIANCE_BUILD_RESOURCE')
    for _,m in pairs(marches or {})do
        if call(m,'GetMarchTargetType')==wanted and tostring(get(m,'targetUuid'))==tostring(get(mine,'uuid'))then return m end
    end
end
local function mines(c)
    local alliance=tostring(get(player(),'allianceId')or '')
    local normal=enum('AllianceMineStatus','Normal')
    local list={}
    for _,m in pairs(call(center('AllianceMineManager'),'GetMyAlMines')or {})do
        local remain,count,max=tonumber(get(m,'remainNum')),tonumber(get(m,'soldierNum')),tonumber(get(m,'soldierMax'))
        if id(get(m,'uuid'))and tonumber(get(m,'pointId'))and get(m,'status')==normal and tostring(get(m,'allianceId'))==alliance and
           tostring(get(m,'serverId')or c.server)==c.server and remain and remain>0 and count and max and count<max then
            list[#list+1]=m
        end
    end
    table.sort(list,function(a,b)return tostring(get(a,'uuid'))<tostring(get(b,'uuid'))end)
    return list
end
local function idle_squad(c,slot)
    local f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
    if not id(get(f,'uuid'))or tonumber(get(f,'index'))~=slot or tostring(get(f,'ownerUid'))~=c.uid then return nil end
    if call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(player(),'uid'),get(f,'uuid'),get(player(),'allianceId'))~=nil then return nil end
    return f
end
function M.Supports(kind)return kind=='allianceGather'end
function M.Interval()return 60000 end
function M.Plan(kind,config,at)
    if kind~='allianceGather'then error('Unsupported alliance gathering service')end
    local squads={};local seen={}
    for _,v in ipairs(config.allianceGatherSquads or {})do
        local slot=tonumber(v);if not slot or slot%1~=0 or slot<1 or slot>4 or seen[slot]then error('Invalid gathering squad priority')end
        seen[slot]=true;squads[#squads+1]=slot
    end
    if #squads==0 then return nil,{available=0,reason='Choose at least one squad'}end
    if call(player(),'IsInAlliance')~=true then return nil,{available=0,reason='Join an alliance first'}end
    if tonumber(call(player(),'GetCurWorldId'))~=0 then return nil,{available=0,reason='Alliance gathering requires the normal world'}end
    local c=context()
    if not c.fresh or at-c.fresh>30000 then
        return {readOnly=true,phase='mines',route=enum('MsgDefines','GetAllAllianceMineList'),args={},version=c.version,key=c.key},{fresh=false}
    end
    local candidates=mines(c);local open={}
    for _,m in ipairs(candidates)do if not own_march(c,m)then open[#open+1]=m end end
    local before={available=#open,mines=#candidates}
    if #open==0 then before.reason=#candidates==0 and 'No alliance mine has resources and room'or 'Your squads are already gathering at every open alliance mine';return nil,before end
    for _,slot in ipairs(squads)do
        local f=idle_squad(c,slot)
        if f then
            local mine=open[1];local formation=tostring(get(f,'uuid'))
            local target=enum('MarchTargetType','COLLECT_ALLIANCE_BUILD_RESOURCE')
            local job={phase='dispatch',route='MarchUtil.StartMarch',squadIndex=slot,formation=formation,
                mineUuid=tostring(get(mine,'uuid')),pointId=tonumber(get(mine,'pointId')),key=c.key,continue=true,wireCount=0}
            before.squadIndex=slot;before.mineUuid=job.mineUuid;before.pointId=job.pointId;before.remainNum=tonumber(get(mine,'remainNum'))
            job.execute=function()
                if job.executionStarted then error('Alliance gathering request was already attempted; no replay')end
                -- Re-check immediately before the single native call.
                local now=context();if now.key~=job.key then error('The game account changed')end
                local fresh=idle_squad(now,slot);if not fresh or tostring(get(fresh,'uuid'))~=formation then error('Configured squad is busy or changed')end
                if own_march(now,mine)then error('A squad is already gathering at this alliance mine')end
                call(fresh,'ConscriptSoldier')
                if not tonumber(get(fresh,'totalSoldierNum'))or tonumber(get(fresh,'totalSoldierNum'))<=0 then error('Configured squad has no soldiers')end
                local back=static(rawget(_G,'MarchUtil'),'GetAutoBackHomeState',target)
                -- From here a send may have happened: an error is uncertain, never retried.
                job.executionStarted=true;job.wireCount=1
                static(rawget(_G,'MarchUtil'),'StartMarch',target,job.pointId,get(mine,'uuid'),-1,0,get(fresh,'uuid'),back,nil,0,tonumber(now.server))
            end
            return job,before
        end
    end
    before.reason='No selected squad is idle';return nil,before
end
function M.Verify(kind,job)
    local c=ctx;if not c or c.key~=job.key then return false end
    if job.phase=='mines'then
        if c.version>job.version then c.fresh=job.sentAt;return true,{fresh=true}end
        return false
    end
    local f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',job.squadIndex)
    if tostring(get(f,'uuid'))~=job.formation then return false end
    local m=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(player(),'uid'),get(f,'uuid'),get(player(),'allianceId'))
    if not m or call(m,'GetMarchTargetType')~=enum('MarchTargetType','COLLECT_ALLIANCE_BUILD_RESOURCE')or
       tostring(get(m,'targetUuid'))~=job.mineUuid or tostring(get(m,'ownerUid'))~=c.uid then return false end
    c.fresh=nil
    return true,{squadIndex=job.squadIndex,marchUuid=tostring(get(m,'uuid')),mineUuid=job.mineUuid,pointId=job.pointId,dispatched=1}
end
function M.Stop(kind)
    if kind~='allianceGather'then return end
    if ctx then pcall(call,ctx.manager,'RemoveListener',ctx.event,ctx.fn)end
    ctx=nil
end
return M
