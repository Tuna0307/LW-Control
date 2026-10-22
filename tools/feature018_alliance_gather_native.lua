-- R30 Alliance Gathering Dispatch. Owner 2026-10-22: this collects the alliance resource drops left
-- when the alliance defeats a Doom Elite (末日精英). RECOVERED original settings: off, squads [1,2] in
-- priority order. RECOVERED v27 drop: world point type 26, class WorldAllianceCollectResource
-- (configId -> TableName.AllianceMine guard_num). The game's own button (UIWorldPointBtn 2289-2314)
-- refuses when you already have an ALLIANCE_RESOURCE_COLLECT march at the point (season_tips212) or
-- when #detail.playerInfoLiset >= guard_num (season_alliance_resource_maxtips), then
-- MarchUtil.OnClickStartMarch(ALLIANCE_RESOURCE_COLLECT, pointId, uuid) -> UIFormationSelectListNewCtrl
-- 1356 StartMarch(type, point, uuid, timeIndex, 0, formation, autoBackHome, nil, 0, server).
-- Detail: WorldGetAllianceCollectResDetail(uuid, serverId, pointId) -> GetAllianceResourceData(uuid).
-- Discovery (OWN_DESIGN): drops in the world area the game has loaded (WorldPointManager._pointInfos),
-- the same area the game's own pickaxe shortcut uses. No work on require.
local M={}
local ctx
local DROP_TYPE=26
local DETAIL_FRESH_MS=15000
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function static(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(...)end
local function enum(n,k)local v=get(rawget(_G,n),k);if v==nil then error('Native '..n..'.'..k..' is unavailable')end;return v end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function player()return get(rawget(_G,'LuaEntry'),'Player')end
local function id(v)local s=tostring(v);if s:match('^[1-9]%d*$')then return s end end
local function world()return get(get(rawget(_G,'CS'),'SceneManager'),'World')end
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
        local event=enum('EventId','WorldGetAllianceCollectResDetailUpdate')
        ctx={key=key,uid=uid,server=server,manager=manager,event=event,version=0,read={}}
        ctx.fn=function()if ctx then ctx.version=ctx.version+1 end end
        call(manager,'AddListener',event,ctx.fn)
    end
    return ctx
end
-- C# private field read, as the Map scanner does for WorldPointManager._pointInfos.
local function reflected(o,key)
    local ok,t=pcall(function()return o:GetType()end);if not ok or not t then return nil end
    local flags=52;pcall(function()local B=CS.System.Reflection.BindingFlags;flags=B.Instance+B.Public+B.NonPublic end)
    local okf,f=pcall(function()return t:GetField(key,flags)end)
    if okf and f then local okv,v=pcall(function()return f:GetValue(o)end);if okv then return v end end
end
local function loaded_points()
    local w=world();local pm=get(w,'PointManager')or reflected(w,'PointManager')
    local list=get(pm,'_pointInfos')or reflected(pm,'_pointInfos')
    local n=tonumber(get(list,'Count'))
    if not n then error('Loaded world points are unavailable (open the world map)')end
    local out={}
    for i=0,math.min(n,200000)-1 do
        local p=get(list,i);if p==nil then local ok,v=pcall(function()return list:get_Item(i)end);if ok then p=v end end
        if p and tonumber(get(p,'PointType')or get(p,'pointType'))==DROP_TYPE then out[#out+1]=p end
    end
    return out
end
local function typed(p)
    local cast,typeof=rawget(_G,'cast'),rawget(_G,'typeof');local cls=get(rawget(_G,'CS'),'WorldAllianceCollectResource')
    if get(p,'configId')==nil and type(cast)=='function'and type(typeof)=='function'and cls then
        local ok,v=pcall(cast,p,typeof(cls));if ok and v then return v end
    end
    return p
end
local function own_march_at(pointId,uuid)
    local wanted=enum('MarchTargetType','ALLIANCE_RESOURCE_COLLECT')
    for _,m in pairs(call(center('WorldMarchDataManager'),'GetOwnerMarches',get(player(),'uid'),get(player(),'allianceId'))or {})do
        if call(m,'GetMarchTargetType')==wanted and (tonumber(get(m,'targetPos'))==pointId or tostring(get(m,'targetUuid'))==uuid)then return m end
    end
end
local function idle_squad(c,slot)
    local f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
    if not id(get(f,'uuid'))or tonumber(get(f,'index'))~=slot or tostring(get(f,'ownerUid'))~=c.uid then return nil end
    if call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(player(),'uid'),get(f,'uuid'),get(player(),'allianceId'))~=nil then return nil end
    return f
end
local function count(list)local n=0;for _ in pairs(list or {})do n=n+1 end;return n end
-- Drops of our alliance in the loaded world area, with the game's own button checks.
local function drops(c,at)
    local alliance=tostring(get(player(),'allianceId')or '')
    local found,open,needRead={},{},nil
    for _,raw in ipairs(loaded_points())do
        local p=typed(raw);local pointId=tonumber(get(p,'pointIndex')or get(p,'PointIndex')or get(p,'pointId'))
        local uuid=id(get(p,'uuid'));local owner=get(p,'allianceId')
        if pointId and uuid and (owner==nil or tostring(owner)==alliance)then
            local drop={pointId=pointId,uuid=uuid,rawUuid=get(p,'uuid'),configId=tonumber(get(p,'configId')),server=tonumber(get(p,'serverId'))or tonumber(c.server)}
            found[#found+1]=drop
            if not own_march_at(pointId,uuid)then
                local read=c.read[uuid]
                -- The detail list is keyed by the reply's own uuid value (the point's long uuid), not text.
                local manager=center('WorldPointDetailManager')
                local detail=call(manager,'GetAllianceResourceData',drop.rawUuid)or call(manager,'GetAllianceResourceData',uuid)
                if not read or not detail or at-read>DETAIL_FRESH_MS then needRead=needRead or drop
                else
                    local players=get(detail,'playerInfoLiset');local mine=false
                    for _,v in pairs(players or {})do if tostring(get(v,'uid'))==c.uid then mine=true end end
                    local line=drop.configId and call(call(rawget(_G,'LocalController'),'instance'),'getLine',enum('TableName','AllianceMine'),drop.configId)
                    local cap=tonumber(get(line,'guard_num'))
                    local remain=tonumber(select(2,pcall(call,detail,'GetCurResourceValue')))or tonumber(get(detail,'remainValue'))
                    drop.remain=remain;drop.gatherers=count(players);drop.cap=cap
                    if not mine and remain and remain>0 and (not cap or drop.gatherers<cap)then open[#open+1]=drop end
                end
            end
        end
    end
    table.sort(open,function(a,b)return a.uuid<b.uuid end)
    return found,open,needRead
end
function M.Supports(kind)return kind=='allianceGather'end
function M.Interval()return 10000 end
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
    local ok,found,open,needRead=pcall(drops,c,at)
    if not ok then return nil,{available=0,reason=tostring(found)}end
    if needRead then
        return {readOnly=true,phase='detail',route=enum('MsgDefines','WorldGetAllianceCollectResDetail'),
            args={needRead.rawUuid,needRead.server,needRead.pointId},uuid=needRead.uuid,version=c.version,key=c.key},{available=#open,drops=#found}
    end
    local before={available=#open,drops=#found}
    if #open==0 then
        before.reason=#found==0 and 'No Doom Elite resource drop in the loaded world area'or 'Every nearby drop is full, empty or already has your squad'
        return nil,before
    end
    for _,slot in ipairs(squads)do
        local f=idle_squad(c,slot)
        if f then
            local drop=open[1];local formation=tostring(get(f,'uuid'))
            local target=enum('MarchTargetType','ALLIANCE_RESOURCE_COLLECT')
            local job={phase='dispatch',route='MarchUtil.StartMarch',squadIndex=slot,formation=formation,
                mineUuid=drop.uuid,pointId=drop.pointId,key=c.key,continue=true,wireCount=0}
            before.squadIndex=slot;before.mineUuid=drop.uuid;before.pointId=drop.pointId;before.remainNum=drop.remain
            job.execute=function()
                if job.executionStarted then error('Alliance gathering request was already attempted; no replay')end
                -- Re-check immediately before the single native call.
                local now=context();if now.key~=job.key then error('The game account changed')end
                local fresh=idle_squad(now,slot);if not fresh or tostring(get(fresh,'uuid'))~=formation then error('Configured squad is busy or changed')end
                if own_march_at(drop.pointId,drop.uuid)then error('A squad is already gathering at this drop')end
                call(fresh,'ConscriptSoldier')
                if not tonumber(get(fresh,'totalSoldierNum'))or tonumber(get(fresh,'totalSoldierNum'))<=0 then error('Configured squad has no soldiers')end
                local back=static(rawget(_G,'MarchUtil'),'GetAutoBackHomeState',target)
                -- From here a send may have happened: an error is uncertain, never retried.
                job.executionStarted=true;job.wireCount=1
                static(rawget(_G,'MarchUtil'),'StartMarch',target,drop.pointId,drop.rawUuid,-1,0,get(fresh,'uuid'),back,nil,0,drop.server)
            end
            return job,before
        end
    end
    before.reason='No selected squad is idle';return nil,before
end
function M.Verify(kind,job)
    local c=ctx;if not c or c.key~=job.key then return false end
    if job.phase=='detail'then
        if c.version>job.version then c.read[job.uuid]=job.sentAt;return true,{fresh=true}end
        return false
    end
    local f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',job.squadIndex)
    if tostring(get(f,'uuid'))~=job.formation then return false end
    local m=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(player(),'uid'),get(f,'uuid'),get(player(),'allianceId'))
    if not m or call(m,'GetMarchTargetType')~=enum('MarchTargetType','ALLIANCE_RESOURCE_COLLECT')or
       (tostring(get(m,'targetUuid'))~=job.mineUuid and tonumber(get(m,'targetPos'))~=job.pointId)or tostring(get(m,'ownerUid'))~=c.uid then return false end
    c.read[job.mineUuid]=nil
    return true,{squadIndex=job.squadIndex,marchUuid=tostring(get(m,'uuid')),mineUuid=job.mineUuid,pointId=job.pointId,dispatched=1}
end
function M.Stop(kind)
    if kind~='allianceGather'then return end
    if ctx then pcall(call,ctx.manager,'RemoveListener',ctx.event,ctx.fn)end
    ctx=nil
end
return M
