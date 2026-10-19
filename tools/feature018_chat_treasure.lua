-- R23 OWN_DESIGN optional Treasure neighborhood acquisition and dispatch.
-- Uses accepted native LOD0 AOIs without moving the camera. The probe owns
-- lane exclusion; only exact additions made by this lease are removed.
local M={}
local active,last_search,cursor=nil,0,0
local dispatches={}
local status={searchRequests=0,searchReplies=0,dispatched=0,dispatchPendingCount=0}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then return false end;return pcall(f,o,...)end
local function invoke(o,k,...)local f=get(o,k);if type(f)~='function'then return false end;return pcall(f,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function int(v)local n=tonumber(v);if n and n==math.floor(n)then return n end end
local function uid(v)if v~=nil then local s=tostring(v);if s:match('^[1-9]%d*$')then return s end end end
local function reflect(o,k)
    local direct=get(o,k);if direct~=nil then return direct end
    local ok,t=call(o,'GetType');if not ok then return nil end
    local yes,field=call(t,'GetField',k,52);local valid,value=call(yes and field,'GetValue',o)
    if valid then return value end
end
local function set_field(o,k,v)
    local ok,t=call(o,'GetType');local yes,f=call(ok and t,'GetField',k,52)
    if yes and f then return call(f,'SetValue',o,v)end
    if type(o)=='table'and rawget(o,k)~=nil then o[k]=v;return true end
    return false
end
local function each(list,fn)
    if type(list)=='table'then for _,v in pairs(list)do fn(v)end;return true end
    local n=int(get(list,'Count'));if not n or n>10000 or n<0 then return false end
    for i=0,n-1 do fn(get(list,i))end;return true
end
local function server_now()
    local ok,t=call(rawget(_G,'UITimeManager'),'GetInstance');local yes,n=call(ok and t,'GetServerTime')
    return yes and tonumber(n)or os.time()*1000
end
local function world()return get(get(rawget(_G,'CS'),'SceneManager'),'World')end
local function native_treasure(info)
    if not info or int(get(info,'pointType'))~=21 then return nil end
    local ok,t=call(info,'GetType');local name=ok and tostring(get(t,'FullName')or get(t,'Name')or '')or ''
    if not name:find('TreasurePointInfo',1,true)then return nil end
    local uuid=uid(get(info,'uuid'));local point=int(get(info,'mainIndex'));local server=int(get(info,'serverId'))
    if not uuid or not point or point<1 or point>1000000 or not server or server<=0 then return nil end
    local yes,kind=call(info,'GetWorldTreasureType');local types=rawget(_G,'WorldTreasureType')
    if not yes or not types or (kind~=get(types,'RadarTreasure')and kind~=get(types,'ActivityRadarTreasure')and kind~=get(types,'OffSeasonDetect'))then return nil end
    local exists,back=call(world(),'GetPointInfoByUuid',tonumber(uuid))
    if not exists or not back or uid(get(back,'uuid'))~=uuid or int(get(back,'mainIndex'))~=point then return nil end
    local player=get(rawget(_G,'LuaEntry'),'Player')
    local alliance=get(player,'allianceId');local target_alliance=get(info,'allianceId')
    if alliance==nil or tostring(alliance)==''or tostring(alliance)=='0'or tostring(target_alliance)~=tostring(alliance)then return nil end
    local auth,object=call(world(),'GetObjectByPoint',point)
    local allowed,yes_auth=call(auth and object,'CheckInteractAuthority',info)
    if not allowed or yes_auth~=true then return nil end
    local complete_ok,complete=call(info,'IsComplete')
    local got_ok,got=call(info,'IsHaveGetReward',uid(get(player,'uid')))
    local working_ok,working=call(info,'IsHaveWorking',uid(get(player,'uid')))
    local full_ok,full=call(info,'IsReceiveAllReward')
    if not complete_ok or not got_ok or not working_ok or not full_ok then return nil end
    local expires=tonumber(get(info,'expireTime'))
    if expires and expires>0 and expires<=server_now()then return nil end
    return {uuid=uuid,pointIndex=point,server=server,complete=complete==true,claimed=got==true,
        working=working==true,full=full==true,expires=expires,native=info}
end
local function points(callback,selected)
    local manager=reflect(world(),'PointManager');local list=reflect(manager,'_pointInfos')
    if not list then status.searchError='Native world point cache unavailable';return end
    each(list,function(raw)
        local info=get(raw,'Value')or raw;local t=native_treasure(info)
        if not t then return end
        local index=t.pointIndex-1;local x,y=index%1000,math.floor(index/1000)
        if selected and not selected[math.floor(y/10)*100+math.floor(x/10)]then return end
        callback(t)
    end)
end
local function cleanup_search(reason)
    local s=active;if not s then return end
    local restored=true
    for _,n in ipairs(s.added)do if not select(1,call(s.current,'Remove',s.int32.Parse(tostring(n))))then restored=false end end
    if s.replyFlag~=nil and not set_field(s.manager,'isRecvViewPoints',s.replyFlag)then restored=false end
    if s.worldFlag~=nil and not set_field(s.world,'hasReceiveViewPointsReply',s.worldFlag)then restored=false end
    local tile=get(s.world,'CurTilePosClamped')
    status.cameraStable=tonumber(get(tile,'x'))==s.x and tonumber(get(tile,'y'))==s.y
    status.subscriptionRestored=restored
    status.searchError=reason or (not restored and 'Chat search AOI cleanup failed')or
        (not status.cameraStable and 'Game camera changed during Chat search')or nil
    if s.probe then call(s.probe,'ReleaseChatSearchLease',s.token)end
    active=nil
end
function M.Cleanup()cleanup_search('Chat search stopped');dispatches={}end
function M.SearchPump(owned,callback)
    if active then
        if not owned()then cleanup_search('Chat search owner changed');return end
        if reflect(active.manager,'isRecvViewPoints')==true then
            points(callback,active.selected);status.searchReplies=status.searchReplies+1;cleanup_search()
        elseif os.time()-active.started>=2 then cleanup_search('Chat neighborhood reply timed out')end
        return
    end
    if not owned()or os.time()-last_search<10 then return end
    last_search=os.time()
    local ok,homes=call(center('AllianceMemberDataManager'),'GetAllianceMembersHomePos')
    if not ok or type(homes)~='table'then status.searchError='Alliance city positions unavailable';return end
    local positions={};for point,member in pairs(homes)do
        local n=int(point);if n and n>=1 and n<=1000000 and uid(member)then positions[#positions+1]=n end
    end
    table.sort(positions);if #positions==0 then status.searchError='No native alliance cities available';return end
    cursor=cursor%#positions+1;local point=positions[cursor]-1
    local x,y=point%1000,math.floor(point/1000)
    local w=world();local manager=reflect(w,'PointManager');local current=reflect(manager,'_curViewIndex')
    local probe=rawget(_G,'LWBridgeLiveResourceProbe');local token={}
    if not current or type(get(probe,'TryAcquireChatSearchLease'))~='function'then status.searchError='Native Chat search lane unavailable';return end
    local lease,acquired=call(probe,'TryAcquireChatSearchLease',token)
    if not lease or not acquired then status.searchError='Waiting for active map acquisition';return end
    local cs=rawget(_G,'CS');local tile=get(w,'CurTilePosClamped')
    active={world=w,manager=manager,current=current,probe=probe,token=token,added={},selected={},
        replyFlag=reflect(manager,'isRecvViewPoints'),worldFlag=reflect(w,'hasReceiveViewPointsReply'),
        int32=get(get(cs,'System'),'Int32'),x=tonumber(get(tile,'x')),y=tonumber(get(tile,'y')),started=os.time()}
    local s=active;local indices={};local cx,cy=math.floor(x/10),math.floor(y/10)
    for ay=math.max(0,cy-1),math.min(99,cy+1)do for ax=math.max(0,cx-1),math.min(99,cx+1)do
        local index=ay*100+ax;indices[#indices+1]=index;s.selected[index]=true
    end end
    local yes,error=pcall(function()
        if s.replyFlag==nil or s.worldFlag==nil or not s.x or not s.y then error('Native AOI restoration baseline unavailable')end
        local r=cs.WorldGetBlockMessage.Request()
        local server=int(get(get(rawget(_G,'LuaEntry'),'Player'),'GetCurServerId')(get(rawget(_G,'LuaEntry'),'Player')))
        r.bigMap,r.x,r.y,r.serverId,r.worldId,r.type,r.lod=0,x,y,server,0,0,0
        r.blockSize,r.firstTime,r.battleFieldFirst=10,false,false
        r.leftBottom=math.max(0,(cy-1)*10)*1000+math.max(0,(cx-1)*10)+1
        r.rightTop=math.min(999,(cy+2)*10)*1000+math.min(999,(cx+2)*10)+1
        local arr=cs.System.Array.CreateInstance(typeof(cs.System.Int32),#indices)
        for i,n in ipairs(indices)do
            local number=s.int32.Parse(tostring(n));arr:SetValue(number,i-1)
            local has,value=call(s.current,'Contains',number);if not has then error('Native AOI set lookup failed')end
            if not value then
                local added=select(1,call(s.current,'Add',number));if not added then error('Native AOI set addition failed')end
                s.added[#s.added+1]=n
            end
        end
        r.index=arr
        if not set_field(s.manager,'isRecvViewPoints',false)then error('Native AOI reply gate unavailable')end
        if not owned()then error('Chat search owner changed')end
        cs.WorldGetBlockMessage.Instance:Send(r)
        status.searchRequests=status.searchRequests+1;status.lastSearchPoint=positions[cursor]
    end)
    if not yes then cleanup_search(tostring(error))end
end
function M.QueueDispatch(info,config)
    if not info or info.complete or info.claimed or info.working or info.full or dispatches[info.uuid]then return end
    local range=config.dispatchDelaySeconds or {2,5}
    dispatches[info.uuid]={uuid=info.uuid,pointIndex=info.pointIndex,server=info.server,
        due=server_now()+((tonumber(range[1])or 2)+math.random()*((tonumber(range[2])or 5)-(tonumber(range[1])or 2)))*1000,state='queued'}
end
function M.DispatchChat(msg,config)
    local post=get(rawget(_G,'PostType'),'Text_PointShare_Alliance')
    if post==nil or get(msg,'post')~=post then return end
    local raw=get(msg,'attachmentId');local json_ok,json=pcall(require,'rapidjson');local ok,data
    if type(raw)=='table'then ok,data=true,raw elseif json_ok and type(raw)=='string'and #raw<20000 then ok,data=pcall(json.decode,raw)end
    if not ok or type(data)~='table'or get(rawget(_G,'WorldPointUIType'),'Treasure')~=data.shareType then return end
    local index=int(data.pos);local uuid=uid(data.uuid)
    if not index or not uuid then return end
    local yes,info=call(world(),'GetPointInfo',index)
    local t=yes and native_treasure(info)
    if t and t.uuid==uuid then M.QueueDispatch(t,config)end
end
function M.DispatchPump(owned,config)
    if config.dispatchEnabled~=true or not owned()then return end
    local p=get(rawget(_G,'LuaEntry'),'Player');local player_uid=uid(get(p,'uid'));local alliance=get(p,'allianceId')
    for _,job in pairs(dispatches)do
        if job.state=='queued'and job.due<=server_now()then
            local yes,raw=call(world(),'GetPointInfo',job.pointIndex);local info=yes and native_treasure(raw)
            if not info or info.uuid~=job.uuid or info.server~=job.server or info.complete or info.working or info.full then
                job.state='retired'
            else
                local selected
                for _,slot in ipairs(config.dispatchSquadPriority or {1})do
                    local ok,f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',slot)
                    if ok and f and uid(get(f,'ownerUid'))==player_uid then
                        local valid,march=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(p,'uid'),get(f,'uuid'),alliance)
                        if valid and march==nil then selected=f;break end
                    end
                end
                if not selected then job.due=server_now()+(tonumber(config.dispatchRetrySeconds)or 30)*1000
                else
                    local kind=get(rawget(_G,'MarchTargetType'),'DETECT_TREASURE');local util=rawget(_G,'MarchUtil')
                    local ok,back=invoke(util,'GetAutoBackHomeState',kind)
                    local ready,valid=invoke(util,'CheckFormation',get(selected,'uuid'),kind,tonumber(info.uuid),info.pointIndex,back)
                    if not kind or not ok or not ready or valid~=true then job.due=server_now()+(tonumber(config.dispatchRetrySeconds)or 30)*1000
                    elseif owned()then
                        -- Set a tombstone BEFORE the original route. Exceptions
                        -- are uncertain and must never launch a second march.
                        job.state='sent';job.slot=get(selected,'index');job.sentAt=server_now()
                        local sent,error=invoke(util,'SendCreateMarchMessage',get(selected,'uuid'),kind,info.pointIndex,tonumber(info.uuid),-1,back,true,info.server)
                        status.dispatchSubmitted=(status.dispatchSubmitted or 0)+1
                        if not sent then job.error=tostring(error);status.dispatchError='Treasure march submission uncertain; no replay'end
                    end
                end
            end
        elseif job.state=='sent'then
            local yes,f=call(center('ArmyFormationDataManager'),'GetOneArmyInfoByIndex',job.slot)
            local ok,march=call(center('WorldMarchDataManager'),'GetOwnerFormationMarch',get(p,'uid'),get(yes and f,'uuid'),alliance)
            local target_ok,target=call(march,'GetMarchTargetType')
            if ok and march and uid(get(march,'ownerUid'))==player_uid and uid(get(march,'targetUuid'))==job.uuid and
                target_ok and target==get(rawget(_G,'MarchTargetType'),'DETECT_TREASURE')and tonumber(get(march,'startTime'))and
                tonumber(get(march,'startTime'))>=job.sentAt then
                job.state='confirmed';status.dispatched=status.dispatched+1
            end
        end
    end
end
function M.Status()
    status.dispatchPendingCount=0
    for _,job in pairs(dispatches)do if job.state=='queued'or job.state=='sent'then status.dispatchPendingCount=status.dispatchPendingCount+1 end end
    status.searchActive=active~=nil;return status
end
M.NativeTreasure=native_treasure
return M
