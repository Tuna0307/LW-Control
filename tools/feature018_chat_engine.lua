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
    local kind
    if post(msg,'RedPackge_New')then kind='redPacket'
    elseif post(msg,'FIREWORK_GIFT_REWARD')then kind='fireworks'
    elseif post(msg,'Detect_Treasure_Fin_Info')or post(msg,'Detect_Treasure_Reward_Fin_Info')or
        post(msg,'DetectEventGetDoubleTreasure')then kind='treasure'end
    if not kind then return nil end
    local uuid=data.uuid
    if uuid==nil or tostring(uuid)==''or #tostring(uuid)>128 then return nil end
    if kind~='redPacket'and not id(uuid)then return nil end
    local server=int(data.targetServer or data.serverId)
    if server and server<0 then return nil end
    local key=kind..'/'..tostring(server or 0)..'/'..tostring(uuid)
    return {kind=kind,uuid=tostring(uuid),data=data,message=msg,key=key,expires=expire(msg,data,kind),
        roomId=get(msg,'roomId'),seqId=get(msg,'seqId'),server=server}
end
local function delay(range)
    range=type(range)=='table'and range or {0,0}
    local a,b=tonumber(range[1])or 0,tonumber(range[2])or 0
    return (a+math.random()*(b-a))*1000
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
    local job=describe(msg);if not job then return end
    local config=configs[job.kind]or {}
    if not force and config.enabled~=true then return end
    if job.expires and job.expires<=clock()then return end
    if queue[job.key]then queue[job.key].message=msg;queue[job.key].data=job.data;return end
    local count=0;for _ in pairs(queue)do count=count+1 end
    if count>=512 then service(job.kind).error='Chat claim queue is full';return end
    job.due=clock()+delay(config.claimDelaySeconds);job.state='queued';job.force=force
    queue[job.key]=job
end
local function chat_manager()
    local ok,mgr=call(rawget(_G,'ChatManager2'),'GetInstance');return ok and mgr
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
    for _,job in pairs(queue)do
        if job.kind==kind and job.uuid==tostring(uuid)and job.state=='sent'then
            job.state='success';job.confirmedAt=clock()
            local s=service(kind);s.claimed=s.claimed+1;s.lastResult='success';s.state='success';s.lastRunAt=clock()
            s.receipts[#s.receipts+1]={uuid=job.uuid,state='success',at=clock(),nativePostState=true}
            if #s.receipts>30 then table.remove(s.receipts,1)end
            local config=configs[kind]or {}
            if config.replyEnabled and job.roomId and type(config.replies)=='table'and #config.replies>0 then
                replies[#replies+1]={job=job,at=clock()+delay(config.replyDelaySeconds),
                    phrase=config.replies[math.random(1,#config.replies)]}
            end
        end
    end
end
local function subscribe()
    local ok,manager=call(rawget(_G,'EventManager'),'GetInstance')
    if not ok or not manager then return end
    if events and events.manager==manager then return end
    M.Detach()
    events={manager=manager,rows={}}
    local function add(event,callback)
        if event==nil then return end
        local yes=call(manager,'AddListener',event,callback)
        if yes then events.rows[#events.rows+1]={event,callback}end
    end
    add(get(rawget(_G,'ChatEventEnum'),'CHAT_RECIEVE_ROOM_MSG'),function(msg)observe(msg,true)end)
    add(get(rawget(_G,'ChatEventEnum'),'CHAT_ROOM_ONEMSG_UPDATA'),function(msg)observe(msg,true)end)
    add(get(rawget(_G,'EventId'),'RedPacketOpen'),function(data)
        local packet=get(data,'redPacket');ack('redPacket',get(packet,'uuid'),data)
    end)
    add(get(rawget(_G,'EventId'),'FireworkGiftGotUpdate'),function(data)ack('fireworks',get(data,'uuid'),data)end)
    listener.eventListenerCount=#events.rows
end
function M.Detach()
    if events then for _,r in ipairs(events.rows)do call(events.manager,'RemoveListener',r[1],r[2])end end
    events=nil;listener.eventListenerCount=0
end
local function claimed(job)
    local p=get(rawget(_G,'LuaEntry'),'Player');local uid=id(get(p,'uid'))
    local data=decode(job.message)or job.data
    for _,u in ipairs(data.uids or {})do if tostring(u)==uid then return true end end
    if job.kind=='fireworks'then
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
    local mgr=chat_manager()
    local route=rawget(_G,'MsgDefines');local network=rawget(_G,'SFSNetwork')
    if type(get(network,'SendMessage'))~='function'then return false,'Native game message sender unavailable'end
    if job.kind=='redPacket'then
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
    elseif job.kind=='fireworks'then
        local config=int(data.configId);local yes,kind=invoke({get=rawget(_G,'GetTableData')},'get',get(rawget(_G,'TableName'),'Firework'),config,'type',0)
        if not config or not yes or not int(kind)or not id(data.ownerUid)or not get(route,'GetFireworksGift')then return false,'Native Fireworks / Egg gift type unavailable'end
        if not owned()then return false,'Chat owner lease changed'end
        job.state='sent';job.sentAt=clock();job.sendCount=1
        return invoke(network,'SendMessage',get(route,'GetFireworksGift'),{uuid=tonumber(job.uuid),ownerUid=tostring(data.ownerUid),type=kind})
    elseif job.kind=='treasure'then
        return false,'Treasure automatic claim is unavailable: v26 has no source-proven request correlation'
    end
    return false,'Unsupported Chat claim'
end
function M.Pump()
    if not owned()then return end
    subscribe()
    if clock()-last_scan>=1000 then last_scan=clock();scan()end
    for _,job in pairs(queue)do
        local s=service(job.kind);local config=configs[job.kind]or {}
        if job.state=='sent'and claimed(job)then ack(job.kind,job.uuid)end
        if job.state=='queued'and (config.enabled or job.force)and job.due<=clock()then
            if job.expires and job.expires<=clock()then job.state='expired'
            elseif claimed(job)then job.state='already_claimed'
            else
                local yes,error=claim(job)
                if job.sendCount==1 then s.submitted=s.submitted+1;s.lastResult='submitted';s.state='waiting_ack'
                else job.state='refused';s.failed=s.failed+1;s.lastResult='failed';s.error=error end
                if not yes and job.sendCount==1 then job.uncertain=true;s.error='Claim submission is uncertain; this target will not be replayed'end
            end
        elseif job.state=='sent'and clock()-job.sentAt>15000 then
            s.state='waiting_ack';s.error='Waiting for native claim confirmation; this target will not be replayed'
        end
    end
    for i=#replies,1,-1 do
        local reply=replies[i]
        if reply.at<=clock()then
            table.remove(replies,i)
            local config=configs[reply.job.kind]or {}
            if config.replyEnabled and owned()then
                local mgr=chat_manager();local ok,allowed=call(mgr,'CheckRoomSend',reply.job.roomId)
                if ok and allowed then call(mgr,'SendChatMsg',{roomId=reply.job.roomId,msg=reply.phrase,
                    post=get(rawget(_G,'PostType'),'Text_Normal')})end
            end
        end
    end
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
    if ledger_owner~=ledger_signature then M.CleanupAfterLeaseLoss();queue={};seen={}end
    ledger_owner=ledger_signature
    owner={account=signature,token=token};configs=settings
    subscribe();scan();return M.Status()
end
function M.Run(request)
    local configured=M.Configure(request);if configured.state~='confirmed'then return configured end
    if not configs[request.kind]then return fail('Unknown Chat service')end
    scan(request.kind);M.Pump();return M.Status()
end
function M.Status()
    for _,kind in ipairs({'redPacket','fireworks','treasure'})do
        local s=service(kind);local config=configs[kind]or {}
        s.enabled=config.enabled==true;s.running=owned()and s.enabled;s.pendingCount=0
        s.replyPendingCount=0
        for _,r in ipairs(replies)do if r.job.kind==kind then s.replyPendingCount=s.replyPendingCount+1 end end
        for _,job in pairs(queue)do if job.kind==kind and (job.state=='queued'or job.state=='sent')then s.pendingCount=s.pendingCount+1 end end
        if not owned()then s.state='disconnected'elseif not s.enabled and s.pendingCount==0 then s.state='disabled'end
        s.capabilities={claims=kind~='treasure',reply=true,search=false,dispatch=false,noSpending=true}
        if kind=='treasure'then
            s.warning='Treasure claim/search/dispatch are unavailable: current v26 has no source-proven claim correlation'
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
function M.CleanupAfterLeaseLoss()
    M.Detach();configs={};replies={};owner=nil;listener.ready=false
end
function M.Register(feature,overview)
    bridge=overview;array=feature.Array
    feature.RegisterHandler('feature018_chat_configure',M.Configure)
    feature.RegisterHandler('feature018_chat_run',M.Run)
    feature.RegisterHandler('feature018_chat_status',M.Read)
end
return M
