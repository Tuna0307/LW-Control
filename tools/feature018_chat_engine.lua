-- R23 OWN_DESIGN scheduling of RECOVERED 0.3.17 Chat settings.
-- Native v26 routes and message discrimination. DetectEventClaimTreasureMessage
-- (archived original SHA256 eb7ac9ba1e7b69779155138fbe87df176f0d65e3987b131839f8706f7d0b9b8c)
-- serializes uuid + targetServer, but its response handler exposes no source-proven
-- request uuid/nonce and native UI code can independently send the same route.
-- ActDetectTreasureDataManager only updates global/activity dig counters, so there
-- is no exact target poststate either. Treasure automatic claim therefore remains
-- unavailable rather than attributing an unrelated same-route reply to this engine.
-- No action on require. No purchases, item-use, gifts sent or currency routes.
local M={}
local bridge,array,owner,events,ledger_owner
local configs,queue,seen,replies={},{},{},{}
local listener={ready=false,observedMessages=0,eventMessages=0,retainedMessages=0}
local services={}
local response_hooks={}
local cross={nextAt=0,cursor=1,allowed={},dirty={},batches=0,requestedRooms=0,eligibleRooms=0}
local last_scan=0
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then return false end;return pcall(f,o,...)end
local function invoke(o,k,...)local f=get(o,k);if type(f)~='function'then return false end;return pcall(f,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function id(v)if v==nil then return nil end;local s=tostring(v);if s:match('^[1-9]%d*$')then return s end end
local function int(v)local n=tonumber(v);if n and n==math.floor(n)then return n end end
local function clock()
    local ok,t=call(rawget(_G,'UITimeManager'),'GetInstance')
    local yes,n=call(ok and t,'GetServerTime');return yes and tonumber(n) or os.time()*1000
end
local function account()
    local p=get(rawget(_G,'LuaEntry'),'Player');local uid=id(get(p,'uid'))
    local a,b,c=call(p,'GetSourceServerId');local d,e=call(p,'GetSelfServerId');local f,g=call(p,'GetCurServerId')
    if not uid or not a or not d or not f or not id(b)or tostring(b)~=tostring(e)or tostring(b)~=tostring(g)then return nil end
    return uid..'/'..tostring(b)
end
local function owned()
    return owner and account()==owner.account and bridge and bridge.Feature018OwnerToken()==owner.token
end
local function service(kind)
    if not services[kind]then services[kind]={name=kind,state='waiting',running=false,pendingCount=0,
        claimed=0,submitted=0,failed=0,dispatchPendingCount=0,receipts={}}end
    return services[kind]
end
local function result(data)return {state='confirmed',data=data}end
local function fail(message)return {state='failed',error=message}end
local function each(list,fn)
    if type(list)=='table'then for _,v in pairs(list)do fn(v)end;return true end
    local n=tonumber(get(list,'Count'));if not n or n>10000 then return false end
    for i=0,n-1 do fn(get(list,i))end;return true
end
local function decode(msg)
    local custom=get(get(msg,'extra'),'customJsonParam')
    if type(custom)=='table'then return custom end
    if type(custom)~='string'or #custom>24000 then return nil end
    local ok,json=pcall(require,'rapidjson');if not ok then return nil end
    local yes,data=pcall(json.decode,custom)
    if yes and type(data)=='table'then return data end
end
local function post(msg,key)
    local expected=get(rawget(_G,'PostType'),key)
    return expected~=nil and get(msg,'post')==expected
end
local function reward_config(job)
    local config=configs[job.kind]or {}
    if job.kind=='goldEgg'and config.legacyCombined==true then return configs[job.route]or {} end
    return config
end
local function packet_family(data)
    local packet=int(data.packetId)
    local ok,template=call(center('RedPacketTemplateManager'),'GetTemplate',packet)
    if not ok or not template then return nil end
    local special,kind,goods=int(get(template,'special_type')),int(get(template,'type')),int(get(template,'goodsId'))
    -- Native v27 packet catalog: government tax, trade/magatama, lucky
    -- packets and birthday gifts. Diamond/birthday goods use the same
    -- source-qualified OpenRedPacket route and its normal template checks.
    if goods==670001 or (special==5 and kind==5)then return 'birthday' end
    if goods==653052 then return 'diamond'end
    if special==2 and kind==1 then return 'tax'end
    if special==3 and kind==3 then return 'trade'end
    if special==4 and kind==2 then return 'lucky'end
end
local function firework_type(data)
    local config=int(data.configId)
    local ok,value=invoke({get=rawget(_G,'GetTableData')},'get',get(rawget(_G,'TableName'),'Firework'),config,'type',0)
    return config and ok and int(value)or nil
end
local function expire(msg,data,kind)
    local n=tonumber(data.expiredTime)
    if n and n>0 then return n end
    if kind=='fireworks'then
        local ok,minutes=call(get(rawget(_G,'LuaEntry'),'DataConfig'),'TryGetNum','fireworks','k2',120)
        local at=tonumber(get(msg,'serverTime'))
        if ok and tonumber(minutes)and at then return at+tonumber(minutes)*60000 end
    end
end
local function describe(msg)
    local data=decode(msg);if not data then return nil end
    local kind,route,family
    if post(msg,'RedPackge_New')then
        kind='redPacket';route=kind;family=packet_family(data)
        if family and configs.goldEgg then kind='goldEgg'end
    elseif post(msg,'FIREWORK_GIFT_REWARD')then
        kind='fireworks';route=kind
        if configs.goldEgg and firework_type(data)==1 then kind='goldEgg';family='boss'end
    elseif post(msg,'S0_ALLIANCE_BOSS_GIFT_REWARD')and configs.goldEgg and configs.goldEgg.legacyCombined~=true then
        kind='goldEgg';route='fireworks';family='boss'
    elseif post(msg,'Detect_Treasure_Fin_Info')or post(msg,'Detect_Treasure_Reward_Fin_Info')or
        post(msg,'DetectEventGetDoubleTreasure')then kind='treasure'end
    if not kind then return nil end
    local uuid=data.uuid
    if uuid==nil or tostring(uuid)==''or #tostring(uuid)>128 then return nil end
    route=route or kind
    if route~='redPacket'and not id(uuid)then return nil end
    local server=int(data.targetServer or data.serverId)
    if server and server<0 then return nil end
    -- The ledger follows the actual native route, not the visible card. A
    -- settings migration or a second announcement cannot send this reward twice.
    local key=route..'/'..tostring(server or 0)..'/'..tostring(uuid)
    return {kind=kind,route=route,family=family,uuid=tostring(uuid),data=data,message=msg,key=key,expires=expire(msg,data,route),
        roomId=get(msg,'roomId'),seqId=get(msg,'seqId'),server=server}
end
-- Owner 2026-10-22: optional active hours per Chat service (this computer's clock), e.g. 08:00-02:00
-- across midnight. Outside them no new claim, reply, share lookup or dig dispatch starts.
local function minutes(text)
    local h,m=tostring(text or''):match('^(%d%d):(%d%d)$');if h then return tonumber(h)*60+tonumber(m)end
end
local function in_hours(config)
    local w=type(config)=='table'and config.activeHours
    if type(w)~='table'or w.enabled~=true then return true end
    local a,b=minutes(w.start),minutes(w['end']);if not a or not b or a==b then return true end
    local ok,t=pcall(os.date,'*t');if not ok or type(t)~='table'then return true end
    local now=t.hour*60+t.min
    if a<b then return now>=a and now<b end
    return now>=a or now<b
end
local function active(config)return type(config)=='table'and config.enabled==true and in_hours(config)end
local function gold_allowed(job,config)
    if job.kind~='goldEgg'or configs.goldEgg.legacyCombined==true then return true end
    if job.family=='boss'then
        local ok,home=call(get(rawget(_G,'LuaEntry'),'Player'),'IsInSelfServer')
        if not ok or home~=true then return false,'Native boss gifts require the home server'end
    end
    if job.family=='birthday'and config.ignoreBirthday==true then return false,'Birthday gifts are excluded'end
    local home=owner.account:match('/(%d+)$')
    if job.server and job.server>0 and tostring(job.server)~=home then
        if config.crossServerEnabled~=true then return false,'Cross-server Gold Egg is disabled'end
        if job.route=='redPacket'then
            local ok,template=call(center('RedPacketTemplateManager'),'GetTemplate',int(job.data.packetId))
            local allowed,value=call(ok and template,'IsCanCrossServer')
            if not allowed or value~=true then return false,'Native packet cross-server conditions refused'end
        end
    end
    return true
end
M._InHours=in_hours
local function delay(range)
    range=type(range)=='table'and range or {0,0}
    local a,b=tonumber(range[1])or 0,tonumber(range[2])or 0
    return (a+math.random()*(b-a))*1000
end
-- Owner 2026-10-22 Treasure: watch alliance chat for shared locations, find the real dig within
-- 3 tiles (people often share an empty tile next to it), send an idle squad to help dig
-- (MarchUtil.SendCreateMarchMessage DETECT_TREASURE, the game's own route), and claim with the
-- game's own request (UIUtil 4467: DetectEventClaimTreasure(uuid, serverId)) only for digs our
-- squad joined, after the time-of-day delay. Reply-confirmed; a refused claim is retried briefly.
local T={lookups={},digs={},shareSeen={},aoi=nil,hook=nil,
    stats={sharesSeen=0,digsFound=0,dispatched=0,claimsSent=0,claimed=0,notJoined=0}}
local SEARCH_RADIUS=3
local function world()return get(get(rawget(_G,'CS'),'SceneManager'),'World')end
local function reflect(o,k)
    local direct=get(o,k);if direct~=nil then return direct end
    local ok,t=call(o,'GetType');if not ok then return nil end
    local yes,field=call(t,'GetField',k,52);local valid,value=call(yes and field,'GetValue',o)
    if valid then return value end
end
local function set_field(o,k,v)
    local ok,t=call(o,'GetType');local yes,f=call(ok and t,'GetField',k,52)
    if yes and f then return call(f,'SetValue',o,v)end
    return false
end
-- The game's own round-trip ping (GameKit.Base.NetRawProxy.GetPing, milliseconds; same source as Show ping).
function T.Ping()
    local cs=rawget(_G,'CS');local network=get(get(cs,'GameEntry'),'Network')
    local proxy=reflect(network,'m_proxy');if not proxy then return 0 end
    local ok,ping=call(proxy,'GetPing');ping=ok and tonumber(ping)or tonumber(reflect(proxy,'_ping'))
    return (ping and ping>0 and ping<5000)and ping or 0
end
function T.Delay(config)
    local rows=type(config.claimSchedule)=='table'and config.claimSchedule or {}
    local ok,t=pcall(os.date,'*t');local minute=ok and type(t)=='table'and t.hour*60+t.min
    for _,r in ipairs(rows)do
        local sh,sm=tostring(r.start or''):match('^(%d%d):(%d%d)$');local eh,em=tostring(r['end']or''):match('^(%d%d):(%d%d)$')
        if minute and sh and eh then
            local a,b=tonumber(sh)*60+tonumber(sm),tonumber(eh)*60+tonumber(em)
            if (a<=b and minute>=a and minute<b)or(a>b and (minute>=a or minute<b))then
                local lo=tonumber(r.min)or 0;local hi=math.max(lo,tonumber(r.max)or lo)
                -- Owner 2026-10-22: 耗時 is measured by the server, so the trip there and back (one ping) is
                -- already part of it. Wait the chosen time minus the ping, never below 0.
                local ping=T.Ping();T.lastPing=ping
                return math.max(0,(lo+math.random()*(hi-lo))*1000-ping)
            end
        end
    end
    return delay(config.claimDelaySeconds)
end
-- A dig of our alliance that we may interact with (the R23 chat_treasure.lua checks).
function T.Native(info)
    if not info or int(get(info,'pointType'))~=21 then return nil end
    local ok,ty=call(info,'GetType');local name=ok and tostring(get(ty,'FullName')or get(ty,'Name')or'')or''
    if not name:find('TreasurePointInfo',1,true)then return nil end
    local uuid=id(get(info,'uuid'));local point=int(get(info,'mainIndex'));local server=int(get(info,'serverId'))
    if not uuid or not point or point<1 or point>1000000 or not server or server<=0 then return nil end
    local p=get(rawget(_G,'LuaEntry'),'Player');local me=id(get(p,'uid'))
    local alliance=get(p,'allianceId')
    if alliance==nil or tostring(alliance)==''or tostring(alliance)=='0'or tostring(get(info,'allianceId'))~=tostring(alliance)then return nil end
    local c1,complete=call(info,'IsComplete');local c2,got=call(info,'IsHaveGetReward',me)
    local c3,working=call(info,'IsHaveWorking',me);local c4,full=call(info,'IsReceiveAllReward')
    return {uuid=uuid,pointIndex=point,server=server,complete=c1 and complete==true,claimed=c2 and got==true,
        working=c3 and working==true,full=c4 and full==true,expires=tonumber(get(info,'expireTime'))}
end
function T.Near(pos)
    local w=world();if not w then return nil end
    local i=pos-1;local x,y=i%1000,math.floor(i/1000);local best,bestDist
    for dy=-SEARCH_RADIUS,SEARCH_RADIUS do for dx=-SEARCH_RADIUS,SEARCH_RADIUS do
        local nx,ny=x+dx,y+dy
        if nx>=0 and nx<1000 and ny>=0 and ny<1000 then
            local ok,info=call(w,'GetPointInfo',ny*1000+nx+1);local t=ok and T.Native(info)
            local d=math.abs(dx)+math.abs(dy)
            if t and (not best or d<bestDist)then best,bestDist=t,d end
        end
    end end
    return best
end
function T.Share(msg)
    if get(msg,'post')~=get(rawget(_G,'PostType'),'Text_PointShare_Alliance')then return end
    local groups=rawget(_G,'ChatGroupType')
    if get(groups,'GROUP_ALLIANCE')==nil or get(msg,'group')~=get(groups,'GROUP_ALLIANCE')then return end
    local key=tostring(get(msg,'roomId'))..'/'..tostring(get(msg,'seqId'))
    if T.shareSeen[key]then return end;T.shareSeen[key]=clock()
    local raw=get(msg,'attachmentId');local data
    if type(raw)=='table'then data=raw
    elseif type(raw)=='string'and #raw<20000 then
        local ok,json=pcall(require,'rapidjson');if ok then local yes,v=pcall(json.decode,raw);if yes then data=v end end
    end
    if type(data)~='table'then data=decode(msg)end
    local pos=type(data)=='table'and int(data.pos)
    if not pos or pos<1 or pos>1000000 then return end
    local sid=int(data.sid);local p=get(rawget(_G,'LuaEntry'),'Player');local ok,cur=call(p,'GetCurServerId')
    if sid and ok and int(cur)and sid~=int(cur)then return end
    T.stats.sharesSeen=T.stats.sharesSeen+1
    T.lookups[#T.lookups+1]={pos=pos,at=clock(),state='new'}
end
function T.AoiCleanup()
    local a=T.aoi;if not a then return end
    for _,n in ipairs(a.added)do call(a.current,'Remove',a.int32.Parse(tostring(n)))end
    if a.replyFlag~=nil then set_field(a.manager,'isRecvViewPoints',a.replyFlag)end
    if rawget(_G,'__lwbridgeAoiOwner')==a then rawset(_G,'__lwbridgeAoiOwner',nil)end
    T.aoi=nil
end
-- Ask the server for the 3x3 map blocks around a shared spot without moving the camera.
function T.AoiRequest(pos)
    local w=world();local manager=reflect(w,'PointManager');local current=reflect(manager,'_curViewIndex')
    local cs=rawget(_G,'CS');if not w or not manager or not current or not cs then return false end
    -- One camera-free map request at a time across services (Alliance Gathering also uses this).
    if rawget(_G,'__lwbridgeAoiOwner')~=nil then return false end
    local i=pos-1;local x,y=i%1000,math.floor(i/1000);local cx,cy=math.floor(x/10),math.floor(y/10)
    local a={manager=manager,current=current,added={},int32=get(get(cs,'System'),'Int32'),
        replyFlag=reflect(manager,'isRecvViewPoints'),started=clock()}
    T.aoi=a;rawset(_G,'__lwbridgeAoiOwner',a)
    local ok=pcall(function()
        local indices={}
        for ay=math.max(0,cy-1),math.min(99,cy+1)do for ax=math.max(0,cx-1),math.min(99,cx+1)do indices[#indices+1]=ay*100+ax end end
        local r=cs.WorldGetBlockMessage.Request()
        local p=get(rawget(_G,'LuaEntry'),'Player');local server=int(select(2,call(p,'GetCurServerId')))
        r.bigMap,r.x,r.y,r.serverId,r.worldId,r.type,r.lod=0,x,y,server,0,0,0
        r.blockSize,r.firstTime,r.battleFieldFirst=10,false,false
        r.leftBottom=math.max(0,(cy-1)*10)*1000+math.max(0,(cx-1)*10)+1
        r.rightTop=math.min(999,(cy+2)*10)*1000+math.min(999,(cx+2)*10)+1
        local arr=cs.System.Array.CreateInstance(typeof(cs.System.Int32),#indices)
        for k,n in ipairs(indices)do
            local number=a.int32.Parse(tostring(n));arr:SetValue(number,k-1)
            local has,value=call(current,'Contains',number)
            if has and not value and select(1,call(current,'Add',number))then a.added[#a.added+1]=n end
        end
        r.index=arr
        set_field(manager,'isRecvViewPoints',false)
        cs.WorldGetBlockMessage.Instance:Send(r)
    end)
    if not ok then T.AoiCleanup()end
    return ok
end
function T.AddDig(t,config)
    if T.digs[t.uuid]then return end
    T.stats.digsFound=T.stats.digsFound+1
    if t.complete or t.claimed or t.full then T.digs[t.uuid]={uuid=t.uuid,state='closed',at=clock()};return end
    T.digs[t.uuid]={uuid=t.uuid,pointIndex=t.pointIndex,server=t.server,expires=t.expires,at=clock(),account=owner and owner.account,
        state=t.working and 'joined'or 'queued',due=clock()+delay(config.dispatchDelaySeconds or {2,5})}
end
local function own_dig_march(uuid)
    local p=get(rawget(_G,'LuaEntry'),'Player');local kind=get(rawget(_G,'MarchTargetType'),'DETECT_TREASURE')
    for slot=1,4 do
        local ok,f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
        local yes,m=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(p,'uid'),get(ok and f,'uuid'),get(p,'allianceId'))
        local tk,target=call(m,'GetMarchTargetType')
        if yes and m and id(get(m,'targetUuid'))==uuid and tk and target==kind then return m end
    end
end
function T.Joined(uuid)
    local d=T.digs[uuid];return (d and (d.state=='joined'or d.state=='claiming'))or own_dig_march(uuid)~=nil
end
function T.Dispatch(d,config)
    local p=get(rawget(_G,'LuaEntry'),'Player');local me=id(get(p,'uid'));local alliance=get(p,'allianceId')
    local selected
    for _,slot in ipairs(config.dispatchSquadPriority or {1})do
        local ok,f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
        if ok and f and id(get(f,'ownerUid'))==me then
            local valid,m=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(p,'uid'),get(f,'uuid'),alliance)
            if valid and m==nil then selected=f;break end
        end
    end
    local retry=(tonumber(config.dispatchRetrySeconds)or 30)*1000
    if not selected then d.due=clock()+retry;d.reason='No selected squad is idle';return end
    local kind=get(rawget(_G,'MarchTargetType'),'DETECT_TREASURE');local util=rawget(_G,'MarchUtil')
    local ok,back=invoke(util,'GetAutoBackHomeState',kind)
    local ready,valid=invoke(util,'CheckFormation',get(selected,'uuid'),kind,tonumber(d.uuid),d.pointIndex,back)
    if not kind or not ok or not ready or valid~=true then d.due=clock()+retry;d.reason='The game refused this squad for the dig';return end
    if not owned()then return end
    -- Audit 2026-10-23 AUDIT-01: only the account that found the dig may send to it, and the dig must
    -- still be our alliance's, unfinished, at the same tile right before the march.
    local w=world();local here=w and select(2,call(w,'GetPointInfo',d.pointIndex));local fresh=here and T.Native(here)
    if d.account~=owner.account or not fresh or fresh.uuid~=d.uuid or fresh.complete or fresh.full then
        d.state='closed';d.reason='The dig is no longer valid for this account';return
    end
    -- Tombstone before the game route: an exception is uncertain and never launches a second march.
    d.state='sent';d.sentAt=clock();d.slot=get(selected,'index')
    local sent,error=invoke(util,'SendCreateMarchMessage',get(selected,'uuid'),kind,d.pointIndex,tonumber(d.uuid),-1,back,true,d.server)
    if not sent then d.state='uncertain';d.reason='Dig march submission uncertain; no replay: '..tostring(error)end
end
function T.InstallHook()
    if T.hook then return end
    local ok,module=pcall(require,'Net.Msgs.RadarCenter.DetectEventClaimTreasureMessage')
    local previous=ok and get(module,'HandleMessage')
    if type(previous)~='function'then return end
    local wrapper=function(self,response,...)
        pcall(function()
            local job=T.claiming
            if job and job.state=='sent'and clock()-job.sentAt<15000 then
                local code=get(response,'errorCode')
                if code==nil then T.claiming=nil;T.stats.claimed=T.stats.claimed+1;if T.ack then T.ack('treasure',job.uuid)end
                else
                    job.lastCode=tostring(code);job.retries=(job.retries or 0)+1
                    if job.retries<20 and clock()-(job.firstSentAt or job.sentAt)<15000 then job.state='queued';job.due=clock()+300
                    else job.state='refused';T.claiming=nil end
                end
            end
        end)
        return previous(self,response,...)
    end
    module.HandleMessage=wrapper;T.hook={module=module,wrapper=wrapper,previous=previous}
end
function T.Release()
    T.AoiCleanup()
    if T.hook and T.hook.module.HandleMessage==T.hook.wrapper then T.hook.module.HandleMessage=T.hook.previous end
    T.hook=nil;T.claiming=nil
end
-- The claim reply names no dig, so only one engine claim may be waiting for it (audit 2026-10-23
-- AUDIT-02); the next one goes out right after that reply or after 15 s without one.
function T.Busy(job)
    local c=T.claiming
    if c and c.state=='sent'and clock()-c.sentAt>=15000 then T.claiming=nil;return false end
    return c~=nil and c~=job and c.state=='sent'
end
function T.Claim(job)
    local route=get(rawget(_G,'MsgDefines'),'DetectEventClaimTreasure');local network=rawget(_G,'SFSNetwork')
    if not route or type(get(network,'SendMessage'))~='function'then return false,'Native treasure claim unavailable'end
    if not owned()then return false,'Chat owner lease changed'end
    T.InstallHook()
    local d=T.digs[job.uuid];local server=job.server or (d and d.server)
    if not server then local p=get(rawget(_G,'LuaEntry'),'Player');server=int(select(2,call(p,'GetCurServerId')))end
    job.state='sent';job.sentAt=clock();job.firstSentAt=job.firstSentAt or job.sentAt;job.sendCount=1
    T.claiming=job;T.stats.claimsSent=T.stats.claimsSent+1
    if d then d.state='claiming'end
    return invoke(network,'SendMessage',route,tonumber(job.uuid),server)
end
function T.Pump(config)
    -- AUDIT-03: an in-flight map request is released as soon as Treasure is off or outside its hours,
    -- so the shared lock (also used by Alliance Gathering) and the added view blocks never linger.
    if not active(config)or not owned()then
        if T.aoi then T.AoiCleanup()end
        for _,l in ipairs(T.lookups)do if l.state=='loading'then l.state='new'end end
        return
    end
    -- 1. Shared spots: find the real dig within 3 tiles, loading the area once if needed.
    if T.aoi and (reflect(T.aoi.manager,'isRecvViewPoints')==true or clock()-T.aoi.started>3000)then
        local l=T.aoi.lookup;local t=l and T.Near(l.pos);T.AoiCleanup()
        if l then
            if t then T.AddDig(t,config);l.state='found'else l.state='no_dig';T.lastReason='No dig within 3 tiles of a shared spot'end
        end
    end
    for _,l in ipairs(T.lookups)do
        if l.state=='new'then
            local t=T.Near(l.pos)
            if t then T.AddDig(t,config);l.state='found'
            elseif not T.aoi then
                if T.AoiRequest(l.pos)then T.aoi.lookup=l;l.state='loading'
                elseif rawget(_G,'__lwbridgeAoiOwner')==nil or clock()-l.at>15000 then l.state='no_dig'end
            end
        end
    end
    for i=#T.lookups,1,-1 do local st=T.lookups[i].state;if st~='new'and st~='loading'then table.remove(T.lookups,i)end end
    -- 2. Dispatch, confirm, and trigger the claim when our squad leaves the finished dig.
    for _,d in pairs(T.digs)do
        if d.state=='queued'and d.due<=clock()and config.dispatchEnabled==true then T.Dispatch(d,config)
        elseif d.state=='sent'then
            if own_dig_march(d.uuid)then d.state='joined';T.stats.dispatched=T.stats.dispatched+1
            elseif clock()-d.sentAt>30000 then d.state='uncertain';d.reason='Dig march was not confirmed; no replay'end
        end
        if d.state=='joined'and not own_dig_march(d.uuid)then
            local key='treasure/'..tostring(d.server or 0)..'/'..d.uuid
            if not queue[key]then queue[key]={kind='treasure',uuid=d.uuid,server=d.server,data={},key=key,state='queued',due=clock()+T.Delay(config)}end
        end
    end
    for key,at in pairs(T.shareSeen)do if at<clock()-3600000 then T.shareSeen[key]=nil end end
    for uuid,d in pairs(T.digs)do if d.at<clock()-6*3600000 then T.digs[uuid]=nil end end
end
function T.Pending()
    if #T.lookups>0 or T.aoi then return true end
    for _,d in pairs(T.digs)do if d.state=='queued'or d.state=='sent'or d.state=='joined'or d.state=='claiming'then return true end end
    return false
end
local function observe(msg,from_event,force)
    if not owned()then return end
    local room,seq=get(msg,'roomId'),get(msg,'seqId')
    local message_key=room and seq and tostring(room)..'/'..tostring(seq)
    if message_key and not seen[message_key]then
        seen[message_key]=clock();listener.observedMessages=listener.observedMessages+1
        if from_event then listener.eventMessages=listener.eventMessages+1 else listener.retainedMessages=listener.retainedMessages+1 end
        listener.lastMessageAt=clock();listener.lastPost=get(msg,'post')
    end
    if active(configs.treasure)then T.Share(msg)end
    local job=describe(msg);if not job then return end
    local config=reward_config(job)
    if not gold_allowed(job,config)then return end
    if not force and not active(config)then return end
    if job.expires and job.expires<=clock()then return end
    if queue[job.key]then
        local old=queue[job.key];old.message=msg;old.data=job.data
        if old.state=='queued'and not old.sendCount and old.kind~=job.kind then
            old.kind=job.kind;old.family=job.family;old.due=clock()+delay(config.claimDelaySeconds)
        end
        return
    end
    local count=0;for _ in pairs(queue)do count=count+1 end
    if count>=512 then service(job.kind).error='Chat claim queue is full';return end
    job.createdAt=clock();job.due=clock()+(job.kind=='treasure'and T.Delay(config)or delay(config.claimDelaySeconds));job.state='queued';job.force=force
    queue[job.key]=job
end
local function chat_manager()
    local ok,mgr=call(rawget(_G,'ChatManager2'),'GetInstance');return ok and mgr
end
local function native_season_servers()
    local manager=center('SeasonDataManager')
    local ok,values=call(manager,'GetServerListInt')
    if not ok then
        local meta=manager and getmetatable(manager);local index=get(meta,'__index')
        local method=get(index,'GetServerListInt')
        if type(method)=='function'then ok,values=pcall(method,manager)end
    end
    local servers={}
    if ok then each(values,function(value)local n=int(value);if n and n>0 then servers[n]=true end end)end
    return servers
end
local function cross_rooms(home_server)
    local manager=chat_manager();local room_manager=get(manager,'Room')
    local ok,rooms=call(room_manager,'GetGameServerRoomList')
    local servers=native_season_servers();local home=tonumber(home_server)or(owner and tonumber(owner.account:match('/(%d+)$')))
    local selected,seen,budget={},{},0
    local function collect(value,depth)
        budget=budget+1;if budget>512 or depth>3 then return end
        if type(value)=='string'and #value<=128 then
            local server=tonumber(value:match('country_(%d+)'))
            if not server or server==home or not servers[server]or seen[value]then return end
            local available,room=call(room_manager,'GetRoomData',value)
            if available and room and tostring(get(room,'roomId'))==value and
                type(get(room,'GetUnblockedChatDatas'))=='function'and #selected<32 then
                selected[#selected+1]=value;seen[value]=true
            end
        elseif type(value)=='table'then
            for _,child in pairs(value)do if budget>=512 then break end;collect(child,depth+1)end
        elseif value~=nil then each(value,function(child)collect(child,depth+1)end)end
    end
    if ok then collect(rooms,0)end
    table.sort(selected)
    return selected,seen,room_manager
end
local function cross_enabled()
    local config=configs.goldEgg
    return active(config)and config.crossServerEnabled==true and config.legacyCombined~=true
end
local function mark_history(data)
    if not owned()or not cross_enabled()then return end
    local room=get(data,'roomId')
    if type(room)=='string'and cross.allowed[room]then cross.dirty[room]=true end
end
local function pump_cross()
    if not cross_enabled()then cross.allowed={};cross.dirty={};cross.eligibleRooms=0;return end
    if clock()>=cross.nextAt then
        cross.nextAt=clock()+15000
        local rooms,allowed,manager=cross_rooms()
        cross.eligibleRooms=#rooms;cross.allowed=allowed
        if #rooms>0 and type(get(manager,'requestMultiRoomLatestMsg'))=='function'and owned()then
            local batch={}
            for _=1,math.min(8,#rooms)do
                if cross.cursor>#rooms then cross.cursor=1 end
                batch[#batch+1]=rooms[cross.cursor];cross.cursor=cross.cursor+1
            end
            -- Fresh copies of native permission-supplied IDs go through the
            -- game's own pending-request fence. No guessed IDs, room joins,
            -- external feed or reward request are used for discovery.
            local ok,accepted=call(manager,'requestMultiRoomLatestMsg',batch)
            if ok and accepted==true and #batch>0 then
                cross.batches=cross.batches+1;cross.requestedRooms=cross.requestedRooms+#batch
                cross.lastRequestAt=clock()
            end
        end
    end
    -- Coalesce history callbacks and inspect one completed room per tick.
    local room_id=next(cross.dirty)
    if room_id then
        cross.dirty[room_id]=nil
        local _,allowed,manager=cross_rooms()
        if allowed[room_id]and owned()then
            local ok,room=call(manager,'GetRoomData',room_id)
            local valid,messages=call(ok and room,'GetUnblockedChatDatas')
            local count=0
            if valid then each(messages,function(msg)
                if count<200 and owned()then count=count+1;observe(msg,true)end
            end)end
            cross.lastReadAt=clock()
        end
    end
end
local function scan(force_kind)
    local mgr=chat_manager();local room=get(mgr,'Room')
    local ok,rooms=call(room,'GetRoomDatas')
    listener.ready=ok and rooms~=nil;listener.lastScanAt=clock()
    if not listener.ready then listener.error='Native chat rooms are not ready';return end
    listener.error=nil;listener.roomCount=0
    each(rooms,function(r)
        listener.roomCount=listener.roomCount+1
        local yes,messages=call(r,'GetUnblockedChatDatas')
        if yes then each(messages,function(msg)
            local d=force_kind and describe(msg)
            observe(msg,false,d and d.kind==force_kind)
        end)end
    end)
end
local function ack(kind,uuid,data)
    if not owned()or uuid==nil then return end
    local matches={}
    for _,job in pairs(queue)do
        if (job.route or job.kind)==kind and job.uuid==tostring(uuid)and job.state=='sent'then
            local server=type(data)=='table'and int(data.targetServer or data.serverId)
            if not server or not job.server or server==job.server then matches[#matches+1]=job end
        end
    end
    -- The protocol has no per-attempt nonce. Never credit two different
    -- server-qualified targets from one UUID-only observation.
    if #matches~=1 then return end
    for _,job in ipairs(matches)do
        if job.state=='sent'then
            job.state='success';job.confirmedAt=clock()
            local s=service(job.kind);s.claimed=s.claimed+1;s.lastResult='success';s.state='success';s.lastRunAt=clock();s.error=nil
            if job.unconfirmed then s.unconfirmed=math.max(0,(s.unconfirmed or 0)-1);job.unconfirmed=nil end
            s.receipts[#s.receipts+1]={uuid=job.uuid,state='success',at=clock(),nativePostState=true,newRewardConfirmed=true}
            if #s.receipts>30 then table.remove(s.receipts,1)end
            local config=reward_config(job)
            if config.replyEnabled and job.roomId and type(config.replies)=='table'and #config.replies>0 then
                replies[#replies+1]={job=job,at=clock()+delay(config.replyDelaySeconds),
                    phrase=config.replies[math.random(1,#config.replies)]}
            end
        end
    end
end
T.ack=ack
local function outcome(job,state,reason)
    if job.state~='sent'and job.state~='queued'then return end
    local s=service(job.kind)
    if state=='unconfirmed'then
        if not job.unconfirmed then s.unconfirmed=(s.unconfirmed or 0)+1;job.unconfirmed=true end
        -- Retain the original sent tombstone and accept a later genuine
        -- response. Unknown is never permission to retransmit.
    else
        if job.unconfirmed then s.unconfirmed=math.max(0,(s.unconfirmed or 0)-1);job.unconfirmed=nil end
        job.state=state
        local field=state=='already_claimed'and 'alreadyClaimed'or state
        s[field]=(s[field]or 0)+1
        if state=='refused'or state=='exhausted'or state=='expired'then s.failed=s.failed+1 end
    end
    s.lastResult=state;s.state=state;s.error=reason;s.lastRunAt=clock()
    if not job.recordedOutcome or job.recordedOutcome~=state then
        s.receipts[#s.receipts+1]={uuid=job.uuid,state=state,at=clock(),newRewardConfirmed=false}
        if #s.receipts>30 then table.remove(s.receipts,1)end
        job.recordedOutcome=state
    end
end
local function response(kind,data)
    if not owned()or type(data)~='table'then return end
    local packet=get(data,'redPacket')
    local uuid=kind=='redPacket'and get(packet,'uuid')or get(data,'uuid')
    uuid=uuid or get(data,'uuid')
    if uuid==nil then return end -- Unidentified errors cannot be assigned to a pending job.
    local error_code=get(data,'errorCode')
    if error_code==nil then ack(kind,uuid,data);return end
    local matches={}
    for _,job in pairs(queue)do
        if (job.route or job.kind)==kind and job.uuid==tostring(uuid)and job.state=='sent'then
            local server=int(data.targetServer or data.serverId)
            if not server or not job.server or server==job.server then matches[#matches+1]=job end
        end
    end
    if #matches~=1 then return end
    local states={firework_tips_1016='already_claimed',firework_tips_1015='exhausted',firework_tips_1014='expired'}
    outcome(matches[1],states[tostring(error_code)]or 'refused',tostring(error_code))
end
local function attach_response_hooks()
    for _,spec in ipairs({{'fireworks','Net.Msgs.GetFireworksGiftMessage'}, {'redPacket','Net.Msgs.OpenRedPacketMessage'}})do
        local ok,class=pcall(require,spec[2])
        local original=ok and get(class,'HandleMessage')
        if type(original)=='function'and not response_hooks[spec[2]]then
            local kind=spec[1]
            local wrapper=function(self,data,...)
                local values=table.pack(pcall(original,self,data,...))
                if values[1]then pcall(response,kind,data)end
                if not values[1]then error(values[2],0)end
                return table.unpack(values,2,values.n)
            end
            local installed=pcall(function()class.HandleMessage=wrapper end)
            if installed and get(class,'HandleMessage')==wrapper then
                response_hooks[spec[2]]={class=class,original=original,wrapper=wrapper}
            end
        end
    end
    local n=0;for _ in pairs(response_hooks)do n=n+1 end;listener.responseObserverCount=n
end
local function subscribe()
    local ok,manager=call(rawget(_G,'EventManager'),'GetInstance')
    if not ok or not manager then return end
    if events and events.manager==manager then attach_response_hooks();return end
    M.Detach()
    events={manager=manager,rows={}}
    local function add(event,callback)
        if event==nil then return end
        local yes=call(manager,'AddListener',event,callback)
        if yes then events.rows[#events.rows+1]={event,callback}end
    end
    add(get(rawget(_G,'ChatEventEnum'),'CHAT_RECIEVE_ROOM_MSG'),function(msg)observe(msg,true)end)
    add(get(rawget(_G,'ChatEventEnum'),'CHAT_ROOM_ONEMSG_UPDATA'),function(msg)observe(msg,true)end)
    add(get(rawget(_G,'ChatEventEnum'),'CHAT_ROOM_HISTORYMSG_UPDATA'),mark_history)
    add(get(rawget(_G,'EventId'),'RedPacketOpen'),function(data)
        local packet=get(data,'redPacket');ack('redPacket',get(packet,'uuid'),data)
    end)
    add(get(rawget(_G,'EventId'),'FireworkGiftGotUpdate'),function(data)ack('fireworks',get(data,'uuid'),data)end)
    listener.eventListenerCount=#events.rows
    attach_response_hooks()
end
function M.Detach()
    if events then for _,r in ipairs(events.rows)do call(events.manager,'RemoveListener',r[1],r[2])end end
    events=nil;listener.eventListenerCount=0
    for key,hook in pairs(response_hooks)do
        -- Never overwrite a newer game/extension wrapper during cleanup.
        if get(hook.class,'HandleMessage')==hook.wrapper then pcall(function()hook.class.HandleMessage=hook.original end)end
        response_hooks[key]=nil
    end
    listener.responseObserverCount=0
end
local function claimed(job)
    local p=get(rawget(_G,'LuaEntry'),'Player');local uid=id(get(p,'uid'))
    local data=decode(job.message)or job.data
    for _,u in ipairs(data.uids or {})do if tostring(u)==uid then return true end end
    if job.route=='fireworks'then
        local ok,v=call(center('LWFireworkGiftManager'),'IsThisGiftUuidGot',tonumber(job.uuid));if ok and v then return true end
    elseif job.kind=='treasure'then
        local world=get(get(rawget(_G,'CS'),'SceneManager'),'World')
        local ok,point=call(world,'GetPointInfoByUuid',tonumber(job.uuid))
        if ok and point and id(get(point,'uuid'))==job.uuid then
            local yes,v=call(point,'IsHaveGetReward',uid);if yes and v==true then return true end
        end
    end
    return false
end
local function claim(job)
    local data=decode(job.message)or job.data
    local permitted,reason=gold_allowed(job,reward_config(job))
    if not permitted then return false,reason end
    local mgr=chat_manager()
    local route=rawget(_G,'MsgDefines');local network=rawget(_G,'SFSNetwork')
    if type(get(network,'SendMessage'))~='function'then return false,'Native game message sender unavailable'end
    if job.route=='redPacket'then
        local ok,blocked=call(mgr,'CanNotPickRedPacket',data)
        if not ok or blocked then return false,'Native Red Packet eligibility refused'end
        local packet=int(data.packetId)
        local yes,template=call(center('RedPacketTemplateManager'),'GetTemplate',packet)
        local valid,condition=call(template,'Condition',false)
        if not packet or not yes or not template or not valid or condition~=true then return false,'Red Packet template conditions are not met'end
        local group=get(job.message,'group');local groups=rawget(_G,'ChatGroupType');local channels=get(center('RedPacketManager'),'ChannelType')
        local channel
        for _,pair in ipairs({{'GROUP_ALLIANCE','Alliance'},{'GROUP_SEASON_ROOM','Season'},
            {'GROUP_COUNTRY','World'},{'GROUP_ALLIANCE_FRIEND_ROOM','AliFriend'}})do
            if get(groups,pair[1])~=nil and group==get(groups,pair[1])then channel=get(channels,pair[2])end
        end
        if not channel or not get(route,'OpenRedPacket')then return false,'Native Red Packet channel unavailable'end
        if not owned()then return false,'Chat owner lease changed'end
        job.state='sent';job.sentAt=clock();job.sendCount=1
        return invoke(network,'SendMessage',get(route,'OpenRedPacket'),job.uuid,packet,channel,job.server or 0)
    elseif job.route=='fireworks'then
        local config=int(data.configId);local yes,kind=invoke({get=rawget(_G,'GetTableData')},'get',get(rawget(_G,'TableName'),'Firework'),config,'type',0)
        if not config or not yes or not int(kind)or not id(data.ownerUid)or not get(route,'GetFireworksGift')then return false,'Native Fireworks / Egg gift type unavailable'end
        if job.kind=='goldEgg'and configs.goldEgg.legacyCombined~=true then
            -- Boss announcements are location links in the native UI. Do not
            -- turn one into a claim until the game's own gift object exists
            -- for this exact UUID/owner/config and permits receiving it.
            local ok,q=call(center('LWFireworkGiftManager'),'GetGiftQueueByPlayerUid',tostring(data.ownerUid))
            local list,items=call(ok and q,'ToArray');local found
            if list then each(items,function(item)
                if tostring(get(item,'uuid'))==job.uuid and tostring(get(item,'ownerUid'))==tostring(data.ownerUid)and
                    int(get(item,'configId'))==config and int(get(item,'type'))==1 then found=item end
            end)end
            if not found then
                if clock()-(job.createdAt or clock())<2000 then return false,'Waiting for native gift eligibility',true end
                return false,'The announcement has no matching eligible native gift'
            end
            local checked=call(found,'CheckIsAvailable');local measured,full=call(found,'IsReachLimit')
            if not checked or get(found,'isAvailable')~=true or not measured or full~=false then
                return false,'Native gift ownership, alliance, expiry or remaining-count conditions refused'
            end
        end
        if not owned()then return false,'Chat owner lease changed'end
        job.state='sent';job.sentAt=clock();job.sendCount=1
        return invoke(network,'SendMessage',get(route,'GetFireworksGift'),{uuid=tonumber(job.uuid),ownerUid=tostring(data.ownerUid),type=kind})
    elseif job.kind=='treasure'then
        return T.Claim(job)
    end
    return false,'Unsupported Chat claim'
end
-- Live chat arrives through the game's own room events (subscribe/observe).
-- The full retained-room rescan is only a backup: it walks every room and
-- message and measured ~165 ms per pass, so it runs every 15 s and only while
-- a Chat service is enabled or work is pending. Idle owners do no rescan.
local RESCAN_MS=15000
local function has_chat_work()
    for _,config in pairs(configs)do if type(config)=='table'and config.enabled then return true end end
    if next(replies)~=nil or T.Pending()then return true end
    for _,job in pairs(queue)do if job.state=='queued'or job.state=='sent'then return true end end
    return false
end
M._HasChatWork=has_chat_work
M._Treasure=T -- inert checks only
function M.Pump()
    if not has_chat_work()then return end
    if not owned()then return end
    subscribe()
    pump_cross()
    if clock()-last_scan>=RESCAN_MS then last_scan=clock();scan()end
    for _,job in pairs(queue)do
        local s=service(job.kind);local config=reward_config(job)
        -- The got-cache also changes on an "already claimed" refusal. Only a
        -- genuine native success event/identified success response credits a
        -- new reward; a cache-only change after send remains unconfirmed.
        if job.state=='sent'and job.kind~='treasure'and claimed(job)then
            outcome(job,'unconfirmed','Native got-cache alone does not confirm a new reward')
        elseif job.state=='sent'and job.kind=='treasure'and claimed(job)then ack(job.kind,job.uuid)end
        if job.state=='queued'and job.kind=='treasure'and not T.Joined(job.uuid)then
            -- Owner 2026-10-22: never claim a dig our squad did not join.
            job.state='not_joined';T.stats.notJoined=T.stats.notJoined+1
        elseif job.state=='queued'and job.kind=='treasure'and T.Busy(job)then
            -- Wait for the reply to the claim already in flight.
        -- AUDIT-04: active hours are checked again at the send. Treasure claims are only queued for digs
        -- our squad joined, and those finish after the stop time (owner 2026-10-22).
        elseif job.state=='queued'and (job.force or (job.kind=='treasure'and config.enabled==true)or active(config))and job.due<=clock()then
            if job.expires and job.expires<=clock()then outcome(job,'expired')
            elseif claimed(job)then outcome(job,'already_claimed')
            else
                local yes,error,deferred=claim(job)
                if deferred and not job.sendCount then job.due=clock()+100
                elseif job.kind=='treasure'and (job.retries or 0)>0 then -- a brief retry after a refused claim
                elseif job.sendCount==1 then s.submitted=s.submitted+1;if job.state=='sent'then s.lastResult='submitted';s.state='waiting_ack'end
                else outcome(job,'refused',error)end
                if not yes and job.sendCount==1 and job.state=='sent'then job.uncertain=true;s.error='Claim submission is uncertain; this target will not be replayed'end
            end
        elseif job.state=='sent'and clock()-job.sentAt>15000 then
            outcome(job,'unconfirmed','Waiting for native claim confirmation; this target will not be replayed')
        end
    end
    for i=#replies,1,-1 do
        local reply=replies[i]
        if reply.at<=clock()then
            table.remove(replies,i)
            local config=reward_config(reply.job)
            if config.replyEnabled and active(config)and owned()then
                local mgr=chat_manager();local ok,allowed=call(mgr,'CheckRoomSend',reply.job.roomId)
                if ok and allowed then call(mgr,'SendChatMsg',{roomId=reply.job.roomId,msg=reply.phrase,
                    post=get(rawget(_G,'PostType'),'Text_Normal')})end
            end
        end
    end
    T.Pump(configs.treasure or {})
    for key,at in pairs(seen)do if at<clock()-3600000 then seen[key]=nil end end
    -- Keep sent/success tombstones for this owner session. Expired records may
    -- be forgotten only after their source deadline; scanning rejects expiry.
    for key,job in pairs(queue)do if job.expires and job.expires<clock()-60000 then queue[key]=nil end end
end
function M.Configure(request)
    local signature=account();local token=bridge and bridge.Feature018OwnerToken()
    local uid=signature and signature:match('^([^/]+)/')
    if not signature or not token or request.authorizedUid~=uid then return fail('Chat account admission absent')end
    local ok,json=pcall(require,'rapidjson');if not ok then return fail('Native JSON decoder unavailable')end
    local yes,settings=pcall(json.decode,request.confirmedChatConfig or '')
    if not yes or type(settings)~='table'then return fail('Confirmed Chat settings unavailable')end
    local ledger_signature=signature..'/'..tostring(token)
    if ledger_owner~=ledger_signature then
        M.CleanupAfterLeaseLoss();queue={};seen={}
        -- AUDIT-01: the previous account's shared spots and digs never carry over.
        T.lookups={};T.shareSeen={};T.digs={}
    end
    ledger_owner=ledger_signature
    owner={account=signature,token=token};configs=settings
    -- Status/configuration is polled by the host. Only Pump's 15 s fallback
    -- or an explicit Run now may walk retained rooms; off status never scans.
    subscribe();return M.Status()
end
function M.Run(request)
    local configured=M.Configure(request);if configured.state~='confirmed'then return configured end
    if not configs[request.kind]then return fail('Unknown Chat service')end
    scan(request.kind);M.Pump();return M.Status()
end
function M.Status()
    for _,kind in ipairs({'redPacket','fireworks','goldEgg','treasure'})do
        local s=service(kind);local config=configs[kind]or {}
        s.enabled=config.enabled==true;s.running=owned()and s.enabled;s.pendingCount=0
        s.legacyCombined=kind=='goldEgg'and config.legacyCombined==true or nil
        if kind=='goldEgg'then
            s.crossServer={enabled=cross_enabled(),eligibleRooms=cross.eligibleRooms,
                historyBatches=cross.batches,requestedRooms=cross.requestedRooms,lastReadAt=cross.lastReadAt}
            s.eventEggClaimsSupported=false
        end
        s.replyPendingCount=0
        for _,r in ipairs(replies)do if r.job.kind==kind then s.replyPendingCount=s.replyPendingCount+1 end end
        for _,job in pairs(queue)do if job.kind==kind and (job.state=='queued'or job.state=='sent')then s.pendingCount=s.pendingCount+1 end end
        if not owned()then s.state='disconnected'elseif not s.enabled and s.pendingCount==0 then s.state='disabled'end
        s.offHours=s.enabled and not in_hours(config)or nil
        s.activeFrom=s.offHours and config.activeHours.start or nil
        s.capabilities={claims=true,reply=true,search=kind=='treasure',dispatch=kind=='treasure',noSpending=true}
        if kind=='treasure'then
            for k,v in pairs(T.stats)do s[k]=v end
            s.dispatchPendingCount=0
            for _,d in pairs(T.digs)do if d.state=='queued'or d.state=='sent'then s.dispatchPendingCount=s.dispatchPendingCount+1 end
                if d.reason then s.lastDigReason=d.reason end end
            s.lastSearchReason=T.lastReason;s.pingMs=T.lastPing
        end
        if array then array(s.receipts)end
    end
    listener.ready=listener.ready and owned()or false
    return result({services=services,listener=listener,readOnly=true,observation='native_chat_listener_and_claim_runtime'})
end
function M.Read(request)
    local configured=M.Configure(request);if configured.state~='confirmed'then return configured end
    return M.Status()
end
-- Read-only native options for reward taxonomy and cross-server eligibility.
-- Configuration templates contain no player messages/rewards and this path
-- neither installs listeners nor joins a room or submits a claim.
function M.Catalog(request)
    local signature=account();local uid=signature and signature:match('^([^/]+)/')
    if not uid or request.authorizedUid~=uid or not bridge or not bridge.Feature018OwnerToken()then
        return fail('Chat account admission absent')
    end
    local packets,firework_rows={},{}
    local function scalar(value)
        if type(value)=='number'or type(value)=='boolean'then return value end
        if type(value)=='string'and #value<=250 then return value end
    end
    local ok,templates=call(center('RedPacketTemplateManager'),'GetAllRedPackTemplate')
    if ok then each(templates,function(template)
        if #packets>=512 then return end
        local row={}
        for _,key in ipairs({'id','type','goodsId','gift_type','special_type','para1','pic','item_pic','chat_icon'})do row[key]=scalar(get(template,key))end
        local named,name=call(template,'GetName');if named then row.name=scalar(name)end
        packets[#packets+1]=row
    end)end
    local yes,controller=call(rawget(_G,'LocalController'),'instance')
    if yes then call(controller,'visitTable',get(rawget(_G,'TableName'),'Firework'),function(key,row)
        row=row or key
        if type(get(row,'getValue'))~='function'then return end
        if #firework_rows>=512 then return end
        local out={}
        for _,key in ipairs({'id','type','goods','name','gift_type','reward'})do
            local valid,value=call(row,'getValue',key);if valid then out[key]=scalar(value)end
        end
        firework_rows[#firework_rows+1]=out
    end)end
    local home=signature:match('/(%d+)$')
    local rooms,_,room_manager=cross_rooms(home)
    local servers=native_season_servers();local server_count=0
    for _ in pairs(servers)do server_count=server_count+1 end
    if array then array(packets);array(firework_rows)end
    return result({readOnly=true,requestDispatched=false,observation='native_chat_reward_configuration',
        packets=packets,fireworks=firework_rows,boundedAt=512,
        crossServer={seasonServerCount=server_count,eligibleKnownRemoteRooms=#rooms,
            nativeRoomListAvailable=type(get(room_manager,'GetGameServerRoomList'))=='function',
            nativeHistoryAvailable=type(get(room_manager,'requestMultiRoomLatestMsg'))=='function',
            historyRequested=false,roomJoined=false}})
end
function M.CleanupAfterLeaseLoss()
    M.Detach();T.Release();configs={};replies={};owner=nil;listener.ready=false
    cross={nextAt=0,cursor=1,allowed={},dirty={},batches=0,requestedRooms=0,eligibleRooms=0}
end
-- Owner 2026-10-21: read-only game emoji list for the Auto Reply picker. No send, no subscription.
-- ChatInterface.GetOldEmojiStr 926-935: a game emoji travels in text as <lwEmoji:ID:>.
function M.EmojiList()
    local manager=rawget(_G,'DataCenter');manager=manager and manager.ChatEmojiTemplateManager
    local ok,list=pcall(function()return manager:GetShowEmojiList()end)
    if not ok or type(list)~='table'then return {state='failed',errorCode='GAME_METHOD_UNAVAILABLE',error='The game emoji list is unavailable'}end
    local out={}
    for _,emoji in ipairs(list)do
        if #out>=300 then break end
        local id=tonumber(select(2,pcall(function()return emoji.id end)))
        if id then
            local row={id=id,tag='<lwEmoji:'..tostring(math.floor(id))..':>'}
            for _,key in ipairs({'name','icon','desc','type','order'})do
                local fine,value=pcall(function()return emoji[key]end)
                if fine and (type(value)=='string'or type(value)=='number')then row[key]=value end
            end
            out[#out+1]=row
        end
    end
    return {state='confirmed',data={readOnly=true,emojis=array and array(out)or out}}
end
-- Owner 2026-10-21: read-only emoji picture for the picker. The chat screen's own TMP sprite sheet
-- (DataCenter.CacheData:LoadEmojiAsset, googleEmoji-0) holds every emoji as a named region; that region
-- is copied to a small PNG with the same Blit/ReadPixels/EncodeToPNG method as the Map icon reader.
local emojiPng={}
local function find_emoji(asset,name,depth)
    if not asset or depth>4 then return nil end
    -- Live owner 2026-10-21: names like e006 are Unicode code points (U+E006), not sprite names.
    local code=tonumber(name,16)
    local index=code and select(2,pcall(function()return asset:GetSpriteIndexFromUnicode(code)end))
    if type(index)~='number'or index<0 then index=select(2,pcall(function()return asset:GetSpriteIndexFromName(name)end))end
    if type(index)=='number'and index>=0 then return asset,index end
    local fallbacks=get(asset,'fallbackSpriteAssets');local count=tonumber(get(fallbacks,'Count'))or 0
    for i=0,math.min(count,16)-1 do
        local found,at=find_emoji(select(2,pcall(function()return fallbacks[i]end)),name,depth+1)
        if found then return found,at end
    end
end
local function emoji_region_png(root,name)
    local cs=rawget(_G,'CS');local u=cs and cs.UnityEngine
    local asset,index=find_emoji(root,name,0)
    local texture=get(asset,'spriteSheet')
    if not texture or type(index)~='number'or index<0 then return nil,'Emoji picture not found'end
    local character=select(2,pcall(function()return asset.spriteCharacterTable[index]end))
    local glyph=get(character,'glyph');local rect=get(glyph,'glyphRect')
    local x,y,w,h=tonumber(get(rect,'x')),tonumber(get(rect,'y')),tonumber(get(rect,'width')),tonumber(get(rect,'height'))
    if not(x and y and w and h)then
        -- Older TMP sprite assets keep x/y/width/height on spriteInfoList entries.
        local info=select(2,pcall(function()return asset.spriteInfoList[index]end))
        x,y,w,h=tonumber(get(info,'x')),tonumber(get(info,'y')),tonumber(get(info,'width')),tonumber(get(info,'height'))
    end
    local tw,th=tonumber(get(texture,'width')),tonumber(get(texture,'height'))
    if not(x and y and w and h and tw and th)or w<1 or h<1 or w>512 or h>512 then return nil,'Emoji picture region unavailable'end
    local rt,readable,previous
    local ok,result=pcall(function()
        rt=u.RenderTexture.GetTemporary(w,h,0,u.RenderTextureFormat.ARGB32)
        u.Graphics.Blit(texture,rt,u.Vector2(w/tw,h/th),u.Vector2(x/tw,y/th))
        previous=u.RenderTexture.active;u.RenderTexture.active=rt
        readable=u.Texture2D(w,h,u.TextureFormat.RGBA32,false)
        readable:ReadPixels(u.Rect(0,0,w,h),0,0,false);readable:Apply(false,false)
        return cs.System.Convert.ToBase64String(u.ImageConversion.EncodeToPNG(readable))
    end)
    pcall(function()u.RenderTexture.active=previous end)
    if rt then pcall(function()u.RenderTexture.ReleaseTemporary(rt)end)end
    if readable then pcall(function()u.Object.Destroy(readable)end)end
    if not ok or type(result)~='string'or result==''then return nil,'Emoji picture could not be drawn'end
    return result
end
function M.EmojiImage(request)
    local name=tostring(request and request.name or'')
    if not name:match('^[%w_%-]+$')or #name>40 then return {state='failed',errorCode='INVALID_REQUEST',error='Invalid emoji name'}end
    if emojiPng[name]then return {state='confirmed',data={readOnly=true,name=name,png=emojiPng[name]}}end
    local cache=get(rawget(_G,'DataCenter'),'CacheData')
    local asset
    -- LoadEmojiAsset answers at once when the sheet is already loaded; otherwise it starts the game's own load.
    local ok=pcall(function()cache:LoadEmojiAsset(function(loaded)asset=loaded end)end)
    if not ok then return {state='failed',errorCode='GAME_METHOD_UNAVAILABLE',error='The game emoji pictures are unavailable'}end
    if not asset then return {state='failed',errorCode='GAME_NOT_READY',error='Emoji pictures are still loading'}end
    local png,err=emoji_region_png(asset,name)
    if not png then return {state='failed',errorCode='GAME_METHOD_UNAVAILABLE',error=err}end
    emojiPng[name]=png
    return {state='confirmed',data={readOnly=true,name=name,png=png}}
end
function M.Register(feature,overview)
    bridge=overview;array=feature.Array
    feature.RegisterHandler('feature018_chat_configure',M.Configure)
    feature.RegisterHandler('feature018_chat_run',M.Run)
    feature.RegisterHandler('feature018_chat_status',M.Read)
    feature.RegisterHandler('feature018_chat_catalog',M.Catalog)
    feature.RegisterHandler('feature018_chat_emoji_list',M.EmojiList)
    feature.RegisterHandler('feature018_chat_emoji_image',M.EmojiImage)
end
return M
