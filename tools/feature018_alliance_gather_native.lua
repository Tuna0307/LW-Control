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
        ctx={key=key,uid=uid,server=server,manager=manager,event=event,version=0,read={},known={}}
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
local function world_loaded()
    local w=world();local pm=get(w,'PointManager')or reflected(w,'PointManager')
    return tonumber(get(get(pm,'_pointInfos')or reflected(pm,'_pointInfos'),'Count'))~=nil
end
local function typed(p)
    local cast,typeof=rawget(_G,'cast'),rawget(_G,'typeof');local cls=get(rawget(_G,'CS'),'WorldAllianceCollectResource')
    if get(p,'configId')==nil and type(cast)=='function'and type(typeof)=='function'and cls then
        local ok,v=pcall(cast,p,typeof(cls));if ok and v then return v end
    end
    return p
end
-- Owner 2026-10-22: the game's pickaxe shows drops tens of tiles away that are not in the loaded map
-- area. When none is in view, ask the server for the map around your base piece by piece (3x3 map
-- blocks, 30x30 tiles, within SWEEP_RADIUS tiles) without moving the camera, and remember drops found.
local SWEEP_RADIUS,SWEEP_STEP,SWEEP_PAUSE_MS,KNOWN_TTL_MS=60,30,30000,15*60000
local function set_field(o,k,v)
    local ok,t=pcall(function()return o:GetType()end);if not ok or not t then return false end
    local flags=52;pcall(function()local B=CS.System.Reflection.BindingFlags;flags=B.Instance+B.Public+B.NonPublic end)
    local okf,f=pcall(function()return t:GetField(k,flags)end)
    if okf and f then return pcall(function()f:SetValue(o,v)end)end
    return false
end
local function remember(c,at)
    local alliance=tostring(get(player(),'allianceId')or '')
    for _,raw in ipairs(loaded_points())do
        local p=typed(raw);local pointId=tonumber(get(p,'pointIndex')or get(p,'PointIndex')or get(p,'pointId'))
        local uuid=id(get(p,'uuid'));local owner=get(p,'allianceId')
        if pointId and uuid and owner~=nil and tostring(owner)~=alliance then c.known[uuid]=nil end
        if pointId and uuid and (owner==nil or tostring(owner)==alliance)then
            c.known[uuid]={pointId=pointId,uuid=uuid,rawUuid=get(p,'uuid'),configId=tonumber(get(p,'configId')),
                server=tonumber(get(p,'serverId'))or tonumber(c.server),seenAt=at}
        end
    end
end
local function aoi_cleanup(c)
    local a=c.aoi;if not a then return end
    for _,n in ipairs(a.added)do pcall(function()a.current:Remove(a.int32.Parse(tostring(n)))end)end
    if a.replyFlag~=nil then set_field(a.manager,'isRecvViewPoints',a.replyFlag)end
    if rawget(_G,'__lwbridgeAoiOwner')==a then rawset(_G,'__lwbridgeAoiOwner',nil)end
    c.aoi=nil
end
local function aoi_request(c,cx,cy)
    if rawget(_G,'__lwbridgeAoiOwner')~=nil then return false end
    local w=world();local manager=get(w,'PointManager')or reflected(w,'PointManager')
    local current=get(manager,'_curViewIndex')or reflected(manager,'_curViewIndex')
    local cs=rawget(_G,'CS');if not w or not manager or not current or not cs then return false end
    local a={manager=manager,current=current,added={},int32=get(get(cs,'System'),'Int32'),
        replyFlag=get(manager,'isRecvViewPoints'),started=nil}
    if a.replyFlag==nil then a.replyFlag=reflected(manager,'isRecvViewPoints')end
    c.aoi=a;rawset(_G,'__lwbridgeAoiOwner',a)
    local ok=pcall(function()
        local bx,by=math.floor(cx/10),math.floor(cy/10);local indices={}
        for ay=math.max(0,by-1),math.min(99,by+1)do for ax=math.max(0,bx-1),math.min(99,bx+1)do indices[#indices+1]=ay*100+ax end end
        local r=cs.WorldGetBlockMessage.Request()
        r.bigMap,r.x,r.y,r.serverId,r.worldId,r.type,r.lod=0,cx,cy,tonumber(c.server),0,0,0
        r.blockSize,r.firstTime,r.battleFieldFirst=10,false,false
        r.leftBottom=math.max(0,(by-1)*10)*1000+math.max(0,(bx-1)*10)+1
        r.rightTop=math.min(999,(by+2)*10)*1000+math.min(999,(bx+2)*10)+1
        local arr=cs.System.Array.CreateInstance(typeof(cs.System.Int32),#indices)
        for k,n in ipairs(indices)do
            local number=a.int32.Parse(tostring(n));arr:SetValue(number,k-1)
            if not current:Contains(number)and current:Add(number)then a.added[#a.added+1]=n end
        end
        r.index=arr
        set_field(manager,'isRecvViewPoints',false)
        cs.WorldGetBlockMessage.Instance:Send(r)
    end)
    if not ok then aoi_cleanup(c);return false end
    return true
end
-- One sweep step per call; true while a step is in flight.
-- Doom Elites are killed by alliance rallies, and the game keeps every alliance rally (with its
-- target tile) in AllianceWarList wherever it is on the map. Remember each monster rally's target;
-- once the rally is gone (fought), look at that spot twice for the drop it may have left.
local RALLY_SCAN_DELAYS_MS={3000,30000}
local function watch_rallies(c,at)
    c.rallies=c.rallies or {};c.rallyScans=c.rallyScans or {}
    local alliance=tostring(get(player(),'allianceId')or '')
    local boss=get(rawget(_G,'AllianceTeamType'),'ATTACK_BOSS')
    local list=get(center('AllianceWarDataManager'),'AllianceWarList')
    if type(list)~='table'then return end
    local seen={}
    for uuid,war in pairs(list)do
        local pointId=tonumber(get(war,'targetPointId'))
        if pointId and pointId>0 and tostring(get(war,'attackAllianceId'))==alliance and (boss==nil or get(war,'type')==boss)then
            local key=tostring(uuid);seen[key]=true
            c.rallies[key]={pointId=pointId,server=tonumber(get(war,'server'))or tonumber(c.server)}
        end
    end
    for key,r in pairs(c.rallies)do
        if not seen[key]then
            c.rallies[key]=nil
            for _,delay in ipairs(RALLY_SCAN_DELAYS_MS)do c.rallyScans[#c.rallyScans+1]={pointId=r.pointId,due=at+delay}end
            c.rallyFought=(c.rallyFought or 0)+1
        end
    end
end
local function due_rally_scan(c,at)
    for i,scan in ipairs(c.rallyScans or {})do
        if at>=scan.due then table.remove(c.rallyScans,i);return scan end
    end
end
local function sweep(c,at)
    if c.aoi then
        if c.aoi.started==nil then c.aoi.started=at end
        local replied=(get(c.aoi.manager,'isRecvViewPoints')or reflected(c.aoi.manager,'isRecvViewPoints'))==true
        if replied or at-c.aoi.started>3000 then pcall(remember,c,at);aoi_cleanup(c)end
        return true
    end
    local scan=due_rally_scan(c,at)
    if scan then
        c.scanningRally=true
        return aoi_request(c,(scan.pointId-1)%1000,math.floor((scan.pointId-1)/1000))
    end
    c.scanningRally=nil
    if c.sweepPauseUntil and at<c.sweepPauseUntil then return false end
    if not c.sweepQueue then
        local home=tonumber(call(player(),'GetMainWorldPos'));if not home or home<1 then return false end
        local hx,hy=(home-1)%1000,math.floor((home-1)/1000);c.sweepQueue={}
        for dy=-SWEEP_RADIUS,SWEEP_RADIUS,SWEEP_STEP do for dx=-SWEEP_RADIUS,SWEEP_RADIUS,SWEEP_STEP do
            local x,y=hx+dx,hy+dy
            if x>=0 and x<1000 and y>=0 and y<1000 then c.sweepQueue[#c.sweepQueue+1]={x,y}end
        end end
        table.sort(c.sweepQueue,function(a,b)return (a[1]-hx)^2+(a[2]-hy)^2<(b[1]-hx)^2+(b[2]-hy)^2 end)
        c.sweepTotal=#c.sweepQueue
    end
    local nxt=table.remove(c.sweepQueue,1)
    if not nxt then c.sweepQueue=nil;c.sweepPauseUntil=at+SWEEP_PAUSE_MS;return false end
    c.sweepDone=(c.sweepTotal or 0)-#c.sweepQueue
    return aoi_request(c,nxt[1],nxt[2])
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
    pcall(remember,c,at)
    for uuid,k in pairs(c.known)do if at-k.seenAt>KNOWN_TTL_MS then c.known[uuid]=nil end end
    local candidates={}
    for _,k in pairs(c.known)do candidates[#candidates+1]=k end
    for _,k in ipairs(candidates)do
        local pointId,uuid,owner=k.pointId,k.uuid,nil
        local p={configId=k.configId,serverId=k.server,uuid=k.rawUuid}
        if pointId and uuid and owner~=nil and tostring(owner)~=alliance then
            c.lastSeen={at=at,text='Drop at '..tostring((pointId-1)%1000)..','..tostring(math.floor((pointId-1)/1000))..' belongs to another alliance'}
        end
        if pointId and uuid and (owner==nil or tostring(owner)==alliance)then
            local drop={pointId=pointId,uuid=uuid,rawUuid=get(p,'uuid'),configId=tonumber(get(p,'configId')),server=tonumber(get(p,'serverId'))or tonumber(c.server)}
            found[#found+1]=drop
            drop.where=tostring((pointId-1)%1000)..','..tostring(math.floor((pointId-1)/1000))
            if own_march_at(pointId,uuid)then drop.skip='your squad is already there'
            else
                local read=c.read[uuid]
                -- The detail list is keyed by the reply's own uuid value (the point's long uuid), not text.
                local manager=center('WorldPointDetailManager')
                local detail=call(manager,'GetAllianceResourceData',drop.rawUuid)or call(manager,'GetAllianceResourceData',uuid)
                if not read or not detail or at-read>DETAIL_FRESH_MS then needRead=needRead or drop;drop.skip=read and not detail and 'the game returned no details' or 'reading details'
                else
                    local players=get(detail,'playerInfoLiset');local mine=false
                    for _,v in pairs(players or {})do if tostring(get(v,'uid'))==c.uid then mine=true end end
                    local line=drop.configId and call(call(rawget(_G,'LocalController'),'instance'),'getLine',enum('TableName','AllianceMine'),drop.configId)
                    local cap=tonumber(get(line,'guard_num'))
                    local remain=tonumber(select(2,pcall(call,detail,'GetCurResourceValue')))or tonumber(get(detail,'remainValue'))
                    drop.remain=remain;drop.gatherers=count(players);drop.cap=cap
                    if mine then drop.skip='you are already gathering it'
                    elseif not remain or remain<=0 then drop.skip='no resources left';c.known[uuid]=nil
                    elseif cap and drop.gatherers>=cap then drop.skip='full ('..drop.gatherers..'/'..cap..')'
                    else open[#open+1]=drop end
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
    pcall(watch_rallies,c,at)
    -- The game only keeps world points (and shows the pickaxe) while the world map is open.
    if not world_loaded()then return nil,{available=0,reason='Waiting for the world map: open it in the game so drops can be seen'}end
    local ok,found,open,needRead=pcall(drops,c,at)
    if not ok then return nil,{available=0,reason=tostring(found)}end
    if needRead then
        return {readOnly=true,phase='detail',route=enum('MsgDefines','WorldGetAllianceCollectResDetail'),
            args={needRead.rawUuid,needRead.server,needRead.pointId},uuid=needRead.uuid,version=c.version,key=c.key},{available=#open,drops=#found}
    end
    local before={available=#open,drops=#found}
    for _,d in ipairs(found)do if d.skip then c.lastSeen={at=at,text='Drop at '..d.where..': '..d.skip}end end
    local swept,busy=pcall(sweep,c,at)
    if #open==0 and swept and busy then
        before.reason=c.scanningRally and 'Checking where an alliance rally just fought for a Doom Elite drop'
            or ('Searching the map around your base for Doom Elite drops ('..tostring(c.sweepDone or 0)..'/'..tostring(c.sweepTotal or 0)..')')
        before.retryMs=1000;return nil,before
    end
    if #open==0 then
        local last=c.lastSeen and (' · last seen '..(select(2,pcall(os.date,'%H:%M',math.floor(c.lastSeen.at/1000)))or'?')..': '..c.lastSeen.text)or''
        before.reason=(#found==0 and 'No Doom Elite resource drop in the loaded world area'or 'Every nearby drop is full, empty or already has your squad')..last
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
    if ctx then pcall(call,ctx.manager,'RemoveListener',ctx.event,ctx.fn);aoi_cleanup(ctx)end
    ctx=nil
end
return M
