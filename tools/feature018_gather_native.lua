-- R30 OWN_DESIGN scheduler adapter over RECOVERED original v26 gather routes.
-- Original UI: off; squads off/metal/10; radius200; manual delay120s; recall false.
-- No work on require. Requires scheduler execute/BeforeSend/cleanupJobs support.
local M={}
-- Live owner 2026-10-22: the game's own Search tab (WorldSearchItem 104-110) uses ResourceType Metal,
-- Food and Wood for the three gatherable resources; the gold mine (金礦) is internally ResourceType.Wood.
local GATHER_TYPES={metal='Metal',food='Food',gold='Wood'}
local bridge,owner,settings,search,active,stopping,detailJob
local cursor=1
local hooks,slots,known={},{},{}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function id(v)local s=tostring(v);if s:match('^[1-9]%d*$')then return s end end
local function enum(n,k)local v=get(rawget(_G,n),k);if v==nil then error('Native '..n..'.'..k..' is unavailable')end;return v end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function player()return get(rawget(_G,'LuaEntry'),'Player')end
local function world()return get(get(rawget(_G,'CS'),'SceneManager'),'World')end
local function clock()return call(call(rawget(_G,'UITimeManager'),'GetInstance'),'GetServerTime')end
local function field(o,k,getter)local v=get(o,k);if v~=nil then return v end;local f=get(o,getter);if type(f)=='function'then local ok,r=pcall(f,o,k);if ok then return r end end end
local function guid(v)return type(v)=='string'and v:match('^%x%x%x%x%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%x%x%x%x%x%x%x%x$')~=nil end
local function account()
    local p=player();local uid=id(get(p,'uid'));if not uid then error('Native game UID unavailable')end
    local server;for _,k in ipairs({'GetSourceServerId','GetSelfServerId','GetCurServerId'})do
        local s=id(call(p,k));if not s or server and s~=server then error('Gathering requires the home server')end;server=s
    end
    if tonumber(call(p,'GetCurWorldId'))~=0 then error('Ordinary gathering requires the normal world')end
    if not bridge or type(bridge.Feature018OwnerToken)~='function'then error('Gathering owner lease unavailable')end
    local token=bridge.Feature018OwnerToken();if not token then error('Gathering owner lease unavailable')end
    return {uid=uid,server=server,player=p,world=world(),token=token}
end
-- Live v27: CS.SceneManager.World is a C# object whose Lua wrapper is not guaranteed to be
-- identical between reads, so identity is the exact UID + server + Lua player + launch lease.
local function same_owner(a,b)return a and b and a.uid==b.uid and a.server==b.server and a.player==b.player and a.token==b.token and a.world~=nil and b.world~=nil end
local function owned()local ok,a=pcall(account);return ok and same_owner(owner,a)end
local function form(slot)
    local f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
    if not id(get(f,'uuid'))or tonumber(get(f,'index'))~=slot or tostring(get(f,'ownerUid'))~=owner.uid then return nil end
    return f
end
local function march(f)return call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(player(),'uid'),get(f,'uuid'),get(player(),'allianceId'))end
local function detach()
    for i=#hooks,1,-1 do local h=hooks[i];if get(h.object,h.key)==h.wrapper then h.object[h.key]=h.previous end end
    hooks={};search=nil;active=nil;detailJob=nil
end
local function hook(object,key,make)
    local previous=get(object,key);if type(previous)~='function'then error('Native '..key..' observer unavailable')end
    local wrapper=make(previous);object[key]=wrapper;hooks[#hooks+1]={object=object,key=key,wrapper=wrapper,previous=previous}
end
local function intact()for _,h in ipairs(hooks)do if get(h.object,h.key)~=h.wrapper then return false end end;return #hooks>0 end
local function safe_point(pointId,squad,radius)
    local w=world();local p=call(w,'GetPointInfo',pointId);local r=call(w,'GetResourcePointInfoByIndex',pointId)
    if not p or not r then return nil,'Resource point is not loaded' end
    local kind=tonumber(get(p,'PointType')or get(p,'pointType'))
    if kind~=1 and kind~=7 then return nil,'Tile is not a public resource node (type '..tostring(kind)..')' end
    local main=tonumber(get(p,'mainIndex'));local index=tonumber(get(p,'pointIndex'))
    if main~=pointId or index~=pointId or tonumber(get(r,'mainIndex'))~=pointId then return nil,'Resource tile identity changed (tile '..tostring(pointId)..', main '..tostring(main)..', index '..tostring(index)..', resource main '..tostring(get(r,'mainIndex'))..')' end
    if tostring(get(p,'serverId'))~=owner.server or tostring(get(r,'serverId'))~=owner.server then return nil,'Resource server is unknown or changed ('..tostring(get(p,'serverId'))..'/'..tostring(get(r,'serverId'))..' vs '..owner.server..')' end
    -- Strict absence: never interpret missing/native nil occupancy as empty.
    -- Live v27: an empty tile reports gatherMarchUuid 0 and gatherUid "" (empty text). A missing
    -- (nil) value is still unknown and refused.
    local gatherer=get(r,'gatherUid')
    if tostring(get(r,'gatherMarchUuid'))~='0'or gatherer==nil or (tostring(gatherer)~='0'and tostring(gatherer)~='')then
        return nil,'Resource tile is occupied or occupancy is unknown (march '..tostring(get(r,'gatherMarchUuid'))..', player '..tostring(get(r,'gatherUid'))..')'
    end
    for _,v in ipairs({p,r})do local u=get(v,'ownerUid');if u~=nil and tostring(u)~='0'and tostring(u)~=''then return nil,'Resource tile has a player owner ('..tostring(u)..')' end end
    local cfg=tonumber(get(r,'id'));if not cfg or cfg<=0 then return nil,'Resource identity unavailable'end
    local lookup=rawget(_G,'GetTableData');if type(lookup)~='function'then error('Native resource config unavailable')end
    local tab=enum('TableName','GatherResource')
    local resource=tonumber(lookup(tab,cfg,'resource_type'));local level=tonumber(lookup(tab,cfg,'level'))
    local wanted=enum('ResourceType',GATHER_TYPES[squad.resource])
    -- Owner 2026-10-21: the saved level is a maximum (10 = level 10 and below).
    if resource~=wanted or not level or level>squad.level then return nil,'Resource type or level differs (tile type '..tostring(resource)..' level '..tostring(level)..', wanted '..tostring(wanted)..' up to '..tostring(squad.level)..')' end
    local home=tonumber(call(player(),'GetMainWorldPos'));if not home or home<1 or home>1000000 or pointId<1 or pointId>1000000 then return nil,'Native world coordinates unavailable'end
    -- Normal world point indexes are 1 + x + 1000*y.
    local a,b=home-1,pointId-1;local distance=math.sqrt((a%1000-b%1000)^2+(math.floor(a/1000)-math.floor(b/1000))^2)
    if distance>radius then return nil,'Nearest matching node lies outside saved scan radius ('..math.floor(distance)..' > '..radius..')'end
    local black=get(rawget(_G,'SceneUtils'),'IsInBlackRange')
    if type(black)~='function'or black(pointId,tonumber(owner.server))~=false then return nil,'Resource safety in contaminated land is unverified'end
    return {pointId=pointId,configId=cfg,resourceType=resource,level=level,pointType=kind,distance=distance,native=r}
end
local function fresh_record(rec)
    if not owned()then return nil end
    local f=form(rec.slot);if not f or tostring(get(f,'uuid'))~=rec.formation then return nil end
    -- Returning marches disappear from GetOwnerFormationMarch before arrival.
    -- Re-read the exact captured UUID; retain every ownership/GUID fence.
    local m=call(center('WorldMarchDataManager'),'GetMarch',tonumber(rec.uuid))
    -- Arrival/collecting updates clear the creation GUID too. This record was
    -- admitted with that GUID at creation; the exact UUID/owner/formation/server
    -- stays required throughout its life. A different nonempty GUID is refused.
    if not m or tostring(get(m,'uuid'))~=rec.uuid or tostring(get(m,'ownerUid'))~=owner.uid or
       tostring(get(m,'ownerFormationUuid'))~=rec.formation or tostring(get(m,'serverId'))~=owner.server or
       (get(m,'clientCreateGuid')~=rec.guid and get(m,'clientCreateGuid')~='')then return nil end
    return m
end
local function guard(job)
    if not owned()or not intact()then return false,'Gathering game account/session or native observer changed'end
    if stopping and job.phase~='recall'then return false,'Gathering has been stopped'end
    if job.phase=='recall'then
        if known[job.record.slot]~=job.record or job.record.recallAttempted and job.record.recallJob~=job then return false,'Gathering recall was already attempted or ownership record retired'end
        local m=fresh_record(job.record)
        if not m or call(m,'GetMarchTargetType')~=enum('MarchTargetType','COLLECT')or tonumber(get(m,'targetPos'))~=job.record.pointId then return false,'Engine-owned gathering march changed before recall'end
        if call(get(rawget(_G,'LuaEntry'),'DataConfig'),'CheckSwitch','slide_left_return')~=true then return false,'Game requires return-home confirmation'end
        return true
    end
    local f=form(job.slot);if not f or tostring(get(f,'uuid'))~=job.formation or march(f)~=nil then return false,'Configured squad is busy or ownership changed'end
    if job.phase=='gather'or job.phase=='detail'then
        local t,reason=safe_point(job.pointId,job.squad,job.radius)
        if not t or t.configId~=job.configId then return false,reason or 'Resource identity changed before send'end
        if job.phase=='gather'then
            local d=call(center('WorldPointDetailManager'),'GetDetailByPointId',job.pointId)
            if d~=job.detail or tonumber(get(d,'remainRes'))==nil or tonumber(get(d,'remainRes'))<=0 then return false,'Resource detail changed before gathering'end
        end
    end
    return true
end
local function recall_job(rec,slot,reason)
    local job={phase='recall',route='MarchUtil.OnBackHome',record=rec,slot=slot,stopping=reason==nil,wireCount=0,reason=reason}
    job.execute=function()
        if job.executionStarted then error('Gathering recall was already attempted; no replay')end
        local ok,why=guard(job);if not ok then error(why)end
        job.executionStarted=true;rec.recallAttempted=true;rec.recallJob=job;active=job;job.executing=true
        local result=table.pack(pcall(enum('MarchUtil','OnBackHome'),tonumber(rec.uuid)))
        job.executing=false
        if not result[1]then error(result[2],0)end
        if job.wireCount~=1 then error('Native gathering recall has no exact send witness')end
    end
    return job
end
-- Owner 2026-10-21: if another player starts gathering on our target tile before our squad
-- arrives, recall it. Unloaded/unknown tile data never triggers anything.
local function taken_by_other(rec)
    local r=select(2,pcall(function()return call(world(),'GetResourcePointInfoByIndex',rec.pointId)end))
    if not r then return false end
    local marchId,gatherer=tostring(get(r,'gatherMarchUuid')or''),tostring(get(r,'gatherUid')or'')
    local otherMarch=marchId~=''and marchId~='0'and marchId~=rec.uuid
    local otherPlayer=gatherer~=''and gatherer~='0'and gatherer~=owner.uid
    return otherMarch or otherPlayer,marchId,gatherer
end
local function install()
    if #hooks>0 then if not intact()then error('Gathering native observers were replaced')end;return end
    local find=require('Net.Msgs.FindResourcePointMessage')
    hook(find,'HandleMessage',function(previous)return function(self,response,...)
        -- Live owner 2026-10-22: a found tile makes the game's handler change scene and move the camera,
        -- after which the reply was never recorded. Record it first, then let the game handle it.
        local q=search
        pcall(function()if q and q.pending and owned()then
            q.pending=false;q.pointId=tonumber(field(response,'pointId','GetInt'))or 0
            q.error=get(response,'errorCode');q.receivedAt=clock()
        end end)
        local result=table.pack(pcall(previous,self,response,...))
        if q then q.applied=result[1]end
        if not result[1]then error(result[2],0)end;return table.unpack(result,2,result.n)
    end end)
    -- The found tile's detail reply (WorldGetDetailMessage -> WorldPointDetailManager:UpdateDetail).
    -- Record it first so a refused read reports its game code instead of waiting silently.
    local detail=require('Net.Msgs.WorldGetDetailMessage')
    hook(detail,'HandleMessage',function(previous)return function(self,response,...)
        local j=detailJob
        pcall(function()if j and not j.replied and owned()and (tonumber(field(response,'pointId','GetInt'))==j.pointId or get(response,'errorCode')~=nil)then
            j.replied=true;j.replyError=get(response,'errorCode')
        end end)
        local result=table.pack(pcall(previous,self,response,...))
        if not result[1]then error(result[2],0)end;return table.unpack(result,2,result.n)
    end end)
    local create=require('Net.Msgs.WorldMarchFormationNewMessage')
    hook(create,'OnCreate',function(previous)return function(self,formation,kind,target,...)
        local result=table.pack(pcall(previous,self,formation,kind,target,...))
        pcall(function()if result[1]and active and active.phase=='gather'and active.inWire and tostring(formation)==active.formation and kind==enum('MarchTargetType','COLLECT')and tonumber(target)==0 then
            local token=call(get(self,'sfsObj'),'GetUtfString','clientCreateUuid')
            if guid(token)then active.guid=token end
        end end)
        if not result[1]then error(result[2],0)end;return table.unpack(result,2,result.n)
    end end)
    hook(create,'HandleMessage',function(previous)return function(self,response,...)
        local result=table.pack(pcall(previous,self,response,...))
        pcall(function()if active and active.guid and field(response,'clientCreateUuid','GetUtfString')==active.guid then
            active.reply={applied=result[1],uuid=id(field(response,'uuid','GetLong')),uid=id(field(response,'ownerUid','GetUtfString')),error=get(response,'errorCode')}
        end end)
        if not result[1]then error(result[2],0)end;return table.unpack(result,2,result.n)
    end end)
    local change=require('Net.Msgs.WorldMarchChangeMessage')
    hook(change,'HandleMessage',function(previous)return function(self,response,...)
        local result=table.pack(pcall(previous,self,response,...))
        pcall(function()if active and active.phase=='recall'and owned()then
            active.replyFields={uuid=tostring(field(response,'uuid','GetLong')),target=tonumber(field(response,'target','GetInt')),
                uid=tostring(field(response,'ownerUid','GetUtfString')),formation=tostring(field(response,'ownerFormationUuid','GetLong')),
                error=get(response,'errorCode'),applied=result[1]}
            if active.replyFields.uuid==active.record.uuid and active.replyFields.target==enum('MarchTargetType','BACK_HOME')and get(response,'errorCode')==nil then active.replyApplied=result[1]end
        end end)
        if not result[1]then error(result[2],0)end;return table.unpack(result,2,result.n)
    end end)
    hook(rawget(_G,'SFSNetwork'),'SendMessage',function(previous)return function(route,...)
        local args=table.pack(...);local createRoute=route==enum('MsgDefines','WorldMarchFormationNew');local changeRoute=route==enum('MsgDefines','WorldMarchChange')
        -- Native StartMarch has fallback branches (including MoveCityToWorld
        -- for an invalid start position). This bounded invocation authorises
        -- exactly the expected gathering/recall wire request and no fallback.
        if active and active.executing and not(createRoute or changeRoute)then error('Unexpected native route during gathering or recall; refused before wire send',0)end
        if active and active.executing and (createRoute or changeRoute)then
            local ok,reason=guard(active);if not ok then error(reason,0)end
            if active.phase=='gather'then
                local endpoint=type(args[4])=='string'and tonumber(args[4]:match(';(%d+)$'))or nil
                if not createRoute or tostring(args[1])~=active.formation or args[2]~=enum('MarchTargetType','COLLECT')or tonumber(args[3])~=0 or endpoint~=active.pointId then error('Gather route/formation/target changed before wire send',0)end
            elseif not changeRoute or tostring(args[1])~=active.record.uuid or args[2]~=enum('MarchTargetType','BACK_HOME')then error('Recall route/march/target changed before wire send',0)end
            if active.wireCount~=0 then error('A second gathering gameplay send is refused',0)end
            active.wireCount=active.wireCount+1;active.inWire=true
        elseif (createRoute or changeRoute)and owner and owned()then
            -- Conservative manual interference pause: only configured formation/march IDs.
            for slot,v in pairs(slots)do if tostring(args[1])==v.formation or known[slot]and tostring(args[1])==known[slot].uuid then
                v.manualResumeAt=clock()+(settings.manualResumeDelaySeconds or 120)*1000;v.search=nil
            end end
        end
        local result=table.pack(pcall(previous,route,table.unpack(args,1,args.n)))
        if active then active.inWire=false end
        if not result[1]then error(result[2],0)end;return table.unpack(result,2,result.n)
    end end)
end
function M.SetBridge(v)bridge=v end
function M.Supports(kind)return kind=='resourceGather'end
function M.Interval()return 15000 end
function M.BeforeSend(kind,job)if not M.Supports(kind)then return true end;return guard(job)end
function M.Plan(kind,c,at)
    if not M.Supports(kind)then return nil end
    local current=account();if owner and not same_owner(owner,current)then detach();slots={};known={}end;owner=current;settings=c;stopping=false
    local radius=tonumber(c.scanRadius)or 200;local validRadius=false
    for _,v in ipairs({50,100,150,200,250,300,400,500})do if v==radius then validRadius=true end end
    local delay=tonumber(c.manualResumeDelaySeconds)or 120
    if not validRadius or delay<60 or delay>1800 or delay%60~=0 then error('Invalid gathering radius or manual resume delay')end
    local before={gatherSquads={},worldTileCount=1000};local configured={}
    for _,s in ipairs(c.squads or {})do
        if s.enabled==true then
            local slot=tonumber(s.squadIndex);local level=tonumber(s.level)
            if not slot or slot%1~=0 or slot<1 or slot>4 or configured[slot]or not level or level%1~=0 or level<1 or level>10 or not ({metal=true,food=true,gold=true})[s.resource]then error('Invalid gathering squad settings')end
            configured[slot]={squadIndex=slot,resource=s.resource,level=level}
        end
    end
    for offset=0,3 do local slot=(cursor+offset-1)%4+1;local s=configured[slot];if s then
        local f=form(slot);local row={squadIndex=slot,step='unavailable'};before.gatherSquads[#before.gatherSquads+1]=row
        if f then
            local v=slots[slot]or {};slots[slot]=v;v.formation=tostring(get(f,'uuid'));row.step='idle'
            local m=march(f)
            if m then
                local rec=known[slot];local mine=rec and fresh_record(rec);local ours=mine~=nil
                if not ours then v.manualResumeAt=at+delay*1000;v.search=nil;row.pauseReason='manual_wait'end
                row.step='busy'
                if ours and not rec.recallAttempted and call(mine,'GetMarchTargetType')==enum('MarchTargetType','COLLECT')and
                   tonumber(get(mine,'targetPos'))==rec.pointId then
                    local taken,marchId,gatherer=taken_by_other(rec)
                    if taken then
                        row.step='recalling';row.reason='Another player took the tile first; recalling (march '..marchId..', player '..gatherer..')'
                        return recall_job(rec,slot,row.reason),before
                    end
                end
            elseif v.manualResumeAt and at<v.manualResumeAt then row.step='paused';row.pauseReason='manual_wait';row.manualResumeAt=v.manualResumeAt
            else
                known[slot]=nil;row.step='searching';install()
                local q=v.search
                row.reason=v.lastReason
                local function search_at(level)
                    q={pending=true,slot=slot,resource=s.resource,maxLevel=s.level,level=level,startedAt=at};v.search=q;search=q
                    row.reason=(v.lastReason and v.lastReason..' · 'or'')..'Searching level '..tostring(level)
                    return {readOnly=true,phase='search',route=enum('MsgDefines','FindResourcePoint'),args={enum('ResourceType',GATHER_TYPES[s.resource]),level},slot=slot,formation=v.formation,query=q},before
                end
                -- Saved level is a maximum: try it first, then each lower level, before giving up.
                local function lower_or_none(reason)
                    v.lastReason='Level '..tostring(q.level)..': '..tostring(reason)
                    if q.level>1 then return search_at(q.level-1)end
                    row.step='nothing_available';row.reason=reason;v.search=nil;cursor=slot%4+1;return nil,before
                end
                if not q or q.resource~=s.resource or q.maxLevel~=s.level or at-(q.receivedAt or q.startedAt)>30000 then
                    return search_at(s.level)
                elseif q.pending then row.reason=(v.lastReason and v.lastReason..' · 'or'')..'Searching level '..tostring(q.level);return {error='Waiting for native resource search',retryMs=1000},before
                elseif q.detailError then return lower_or_none('Tile detail refused (game code '..tostring(q.detailError)..')')
                -- Live owner 2026-10-22: the game's own reply handler moves the camera and can then raise an
                -- unrelated UI error; a server reply with a pointId and no errorCode is still a valid result.
                elseif q.error or not q.pointId or q.pointId==0 then return lower_or_none('No matching resource at level '..tostring(q.level)..(q.error and (' (game code '..tostring(q.error)..')')or''))
                else
                    local t,reason=safe_point(q.pointId,s,radius)
                    -- Live owner 2026-10-22: the game's own search reply moves the camera to the found tile, but the
                    -- tile's details arrive a moment later. Wait for them instead of rejecting the tile at once.
                    if not t and reason=='Resource point is not loaded'and at-(q.receivedAt or at)<25000 then
                        row.step='searching';row.reason='Waiting for the found tile to load';return {error='Waiting for the found tile to load',retryMs=2000},before
                    end
                    if not t then return lower_or_none(reason)end
                    local d=call(center('WorldPointDetailManager'),'GetDetailByPointId',t.pointId)
                    if not q.detail or d~=q.detail or at-(q.detailAt or 0)>10000 then
                        row.reason='Reading level '..tostring(t.level)..' tile at '..tostring((t.pointId-1)%1000)..','..tostring(math.floor((t.pointId-1)/1000))
                        local job={readOnly=true,phase='detail',route=enum('MsgDefines','WorldGetDetail'),
                            args={t.pointId,tonumber(owner.server),0,t.pointType,'0'},slot=slot,formation=v.formation,
                            pointId=t.pointId,configId=t.configId,squad=s,radius=radius,previous=d,query=q}
                        detailJob=job;return job,before
                    elseif tonumber(get(d,'remainRes'))==nil or tonumber(get(d,'remainRes'))<=0 then
                        return lower_or_none('Native resource detail has no remaining resources')
                    end
                    local job={phase='gather',route='MarchUtil.OnGotoCollect',slot=slot,formation=v.formation,squad=s,radius=radius,
                        pointId=t.pointId,configId=t.configId,detail=d,wireCount=0,continue=true}
                    job.execute=function()
                        if job.executionStarted then error('Gathering request was already attempted; no replay')end
                        local ok,why=guard(job);if not ok then error(why)end
                        local assigned=form(slot);call(assigned,'ConscriptSoldier')
                        if not tonumber(get(assigned,'totalSoldierNum'))or tonumber(get(assigned,'totalSoldierNum'))<=0 or type(get(assigned,'heroes'))~='table'or next(assigned.heroes)==nil then error('Configured squad has no native soldiers/heroes')end
                        ok,why=guard(job);if not ok then error(why)end
                        job.executionStarted=true;active=job;job.executing=true
                        -- Source-causal signature: OnGotoCollect(formation,
                        -- resource,targetUuid,isFormation). Its R2 flows into
                        -- TryStartMarch.R4 -> SendCreateMarchMessage.R3 ->
                        -- StartMarch.R2 -> SendCreateMarchToServer.R3 ->
                        -- WorldMarchFormationNew argument3 (targetUid).
                        -- Public empty resources use targetUid=0; mainIndex
                        -- separately flows into the path destination. This
                        -- parameter is NOT a server ID or destination tile.
                        local result=table.pack(pcall(enum('MarchUtil','OnGotoCollect'),get(assigned,'uuid'),t.native,0,true))
                        job.executing=false
                        if not result[1]then error(result[2],0)end
                        if job.wireCount~=1 or not job.guid then error('Native gathering request has no exact GUID witness')end
                    end
                    return job,before
                end
            end
        end
    end end
    before.reason='No configured gathering squad is ready';return nil,before
end
function M.Verify(kind,job)
    if not M.Supports(kind)or not owned()or not intact()then return false end
    if job.phase=='search'then return not job.query.pending,{readOnly=true,pointId=job.query.pointId or 0,nativeResponseApplied=job.query.applied==true}end
    if job.phase=='detail'then
        local d=call(center('WorldPointDetailManager'),'GetDetailByPointId',job.pointId)
        if job.replied and job.replyError~=nil then job.query.detailError=job.replyError;detailJob=nil;return true,{readOnly=true,pointId=job.pointId,gameCode=job.replyError}end
        if not d or (d==job.previous and not job.replied)or tonumber(get(d,'pointId'))~=job.pointId or tonumber(get(d,'resourceId'))~=job.configId then return false end
        detailJob=nil
        job.query.detail=d;job.query.detailAt=clock();return true,{readOnly=true,pointId=job.pointId,remainRes=tonumber(get(d,'remainRes'))}
    elseif job.phase=='gather'then
        local f=form(job.slot);local m=f and march(f)
        if not m or job.wireCount~=1 or not guid(job.guid)or get(m,'clientCreateGuid')~=job.guid or
           tostring(get(m,'ownerUid'))~=owner.uid or tostring(get(m,'ownerFormationUuid'))~=job.formation or
           tostring(get(m,'serverId'))~=owner.server or call(m,'GetMarchTargetType')~=enum('MarchTargetType','COLLECT')or tonumber(get(m,'targetPos'))~=job.pointId then return false end
        local uuid=id(get(m,'uuid'));if not uuid then return false end
        known[job.slot]={slot=job.slot,formation=job.formation,uuid=uuid,guid=job.guid,pointId=job.pointId}
        slots[job.slot].search=nil;active=nil;cursor=job.slot%4+1
        return true,{squadIndex=job.slot,marchUuid=uuid,clientCreateGuid=job.guid,pointId=job.pointId,targetType='COLLECT',nativeGuidPostState=true,
            directReplyObserved=job.reply and job.reply.applied and job.reply.uuid==uuid and job.reply.uid==owner.uid and job.reply.error==nil or false}
    elseif job.phase=='recall'then
        -- UpdateFromSFS overwrites clientCreateGuid from clientCreateUuid, which
        -- the return update omits (live v27: empty text). The exact owned UUID /
        -- formation / server BACK_HOME transition after our guarded single send
        -- is authoritative poststate, as in the established hotkey recall path.
        -- A dispatcher's cached callback can bypass this late Lua reply observer;
        -- report that honestly instead of requiring or inventing a reply witness.
        local m=fresh_record(job.record)
        if not job.executionStarted or job.wireCount~=1 or not m or call(m,'GetMarchTargetType')~=enum('MarchTargetType','BACK_HOME')then
            local raw=call(center('WorldMarchDataManager'),'GetMarch',tonumber(job.record.uuid))
            return false,{reply=job.replyFields,replyApplied=job.replyApplied==true,marchPresent=raw~=nil,
                marchUuid=tostring(get(raw,'uuid')),ownerUid=tostring(get(raw,'ownerUid')),formation=tostring(get(raw,'ownerFormationUuid')),
                serverId=tostring(get(raw,'serverId')),clientCreateGuid=get(raw,'clientCreateGuid'),target=raw and call(raw,'GetMarchTargetType')}
        end
        known[job.record.slot]=nil;active=nil
        return true,{squadIndex=job.record.slot,marchUuid=job.record.uuid,targetType='BACK_HOME',nativePostState=true,
            replyObserved=job.replyApplied==true,exactRequestCausality=false,
            observation='authoritative_same_owned_march_back_home_after_single_send'}
    end
    return false
end
function M.Stop(kind)
    if not M.Supports(kind)then return end
    stopping=true;search=nil
    for _,v in pairs(slots)do v.search=nil end
    local out={cleanupJobs={}}
    if not owned()then M.FinishStop(kind);known={};slots={};owner=nil;return out end
    if settings and settings.recallOnDisable==true then
        for slot,rec in pairs(known)do
            local m=fresh_record(rec)
            if not rec.recallAttempted and m and call(m,'GetMarchTargetType')==enum('MarchTargetType','COLLECT')and tonumber(get(m,'targetPos'))==rec.pointId then
                out.cleanupJobs[#out.cleanupJobs+1]=recall_job(rec,slot)
            end
        end
    end
    -- The UI stops first, then saves disabled settings. That second Stop must
    -- neither resend nor detach the observer of the first in-flight recall.
    if #out.cleanupJobs==0 and not(active and active.phase=='recall'and active.executionStarted)then M.FinishStop(kind)end
    return out
end
function M.FinishStop(kind)if M.Supports(kind)then detach();stopping=true end end
function M.Observe(kind)
    if not M.Supports(kind)then return {state='unsupported'}end
    local a=account();local out={readOnly=true,serverTime=clock(),gatherSquadIndexes={},gatherSquads={},gatherResources={},worldTileCount=1000}
    for slot=1,4 do
        local f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
        if id(get(f,'uuid'))and tostring(get(f,'ownerUid'))==a.uid then
            out.gatherSquadIndexes[#out.gatherSquadIndexes+1]=slot
            local m=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(a.player,'uid'),get(f,'uuid'),get(a.player,'allianceId'))
            out.gatherSquads[#out.gatherSquads+1]={squadIndex=slot,nativeFormationUuid=tostring(get(f,'uuid')),
                marchUuid=m and tostring(get(m,'uuid')),ownerUid=m and tostring(get(m,'ownerUid')),
                ownerFormationUuid=m and tostring(get(m,'ownerFormationUuid')),serverId=m and tostring(get(m,'serverId')),
                targetPos=m and tonumber(get(m,'targetPos')),targetType=m and call(m,'GetMarchTargetType'),
                collecting=m and call(m,'GetMarchTargetType')==enum('MarchTargetType','COLLECT'),clientCreateGuid=m and get(m,'clientCreateGuid'),
                marchEndTime=m and tonumber(get(m,'endTime')),marchStartTime=m and tonumber(get(m,'startTime'))}
        end
    end
    for _,key in ipairs({'metal','food','gold'})do out.gatherResources[#out.gatherResources+1]={resource=key,resourceType=enum('ResourceType',GATHER_TYPES[key]),maxLevel=10,levelLimitObservation='Original UI maximum; current search may refuse locked levels'}end
    return {state='confirmed',data=out}
end
return M
