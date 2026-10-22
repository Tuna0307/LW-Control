-- R30 mission adapter. v26 routes/settings are RECOVERED; scheduling and conservative
-- selection are OWN_DESIGN. Static bytecode receipts: artifacts/feature018-r30/research-missions.
-- No subscriptions, data reads, timers or requests happen when this file is loaded.
local M={}
local states={}
local generation=0
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function player()return get(rawget(_G,'LuaEntry'),'Player')end
local function uid()return tostring(get(player(),'uid')or '')end
local function route(k)local r=get(rawget(_G,'MsgDefines'),k);if not r then error('Native '..k..' route unavailable')end;return r end
local function num(o,k)return tonumber(get(o,k))end
local function rows(list)
    if type(list)~='table'then error('Native mission list is not loaded')end
    local out={};for _,v in pairs(list)do out[#out+1]=v end
    table.sort(out,function(a,b)return tostring(get(a,'uuid')or '')<tostring(get(b,'uuid')or '')end);return out
end
-- Ghost action -> the game's reply message for it.
local GHOST_REPLY={ghostCreate='GhostReconJoinTeamMessage',ghostJoin='GhostReconJoinTeamMessage',ghostShare='GhostReconHandleSendChatMessage',
    ghostStart='GhostReconStartTeamMessage',ghostClaim='GhostReconRewardMessage'}
local GHOST_REPLIES={'GhostReconJoinTeamMessage','GhostReconHandleSendChatMessage','GhostReconStartTeamMessage','GhostReconRewardMessage'}
local specs={dispatch={event='DispatchTaskUpdateAll',query='DispatchGetTasks'},
    dispatchAssist={event='DispatchTaskUpdateAlliance',query='DispatchGetAllianceTasks'},
    ghostRecon={event='GhostreconTaskRefreshAll',query='GhostreconGetTaskList'}}
local function enabled(kind,c)
    if c.enabled==false and not c.runNow then return false end
    if c.runNow then return true end
    if kind=='dispatch'then return c.autoExecute==true or c.collectRewards==true or c.enabled==true end
    if kind=='dispatchAssist'then return c.autoHelp==true or c.enabled==true end
    return c.autoStartOwn==true or c.autoJoinAlliance==true or c.autoClaimRewards==true
end
local function ensure(kind)
    local s=states[kind]
    if s and s.uid~=uid()then M.Stop(kind);s=nil end
    if s then return s end
    generation=generation+1
    s={uid=uid(),generation=generation,version=0,allianceVersion=0,listeners={},delays={}}
    if not s.uid:match('^[1-9]%d*$')then error('Authenticated mission account unavailable')end
    local events=rawget(_G,'EventId');local em=call(rawget(_G,'EventManager'),'GetInstance')
    local function listen(name,fn)
        local event=get(events,name);if not event then error('Native '..name..' event unavailable')end
        call(em,'AddListener',event,fn);s.listeners[#s.listeners+1]={event,fn}
    end
    s.manager=em;states[kind]=s
    local ok,err=pcall(function()
        listen(specs[kind].event,function()if states[kind]==s then s.version=s.version+1 end end)
        if kind=='ghostRecon'then
            listen('GhostreconAllianceTaskRefresh',function()if states[kind]==s then s.allianceVersion=s.allianceVersion+1 end end)
            -- Live owner 2026-10-22: Ghost Ops locked on "unconfirmed". Watch the game's reply to each
            -- Ghost action: a refusal (errorCode) retries later, a clean reply is the server's acceptance.
            s.replies={};s.hooks={}
            for _,name in ipairs(GHOST_REPLIES)do
                local module=require('Net.Msgs.Ghostrecon.'..name);local previous=get(module,'HandleMessage')
                if type(previous)~='function'then error('Native '..name..' reply handler unavailable')end
                local wrapper=function(self,response,...)
                    pcall(function()if states[kind]==s then
                        local r=s.replies[name]or {seq=0};s.replies[name]={seq=r.seq+1,error=get(response,'errorCode')}
                    end end)
                    return previous(self,response,...)
                end
                module.HandleMessage=wrapper;s.hooks[#s.hooks+1]={module=module,wrapper=wrapper,previous=previous}
            end
        end
    end)
    if not ok then M.Stop(kind);error(err)end
    return s
end
local function plan(s,action,args,before,phase)
    local name=GHOST_REPLY[phase];local seen=name and s.replies and s.replies[name]
    return {route=route(action),args=args,phase=phase,uid=s.uid,generation=s.generation,
        version=s.version,continue=true,replyName=name,replySeq=seen and seen.seq or 0},before
end
local function query(s,kind,phase)
    local p,b=plan(s,specs[kind].query,{}, {fresh=false},phase or 'missionList');p.readOnly=true;return p,b
end
local function fresh(s,kind,at)
    if not s.freshAt or at-s.freshAt>30000 then return query(s,kind)end
end
local function task_by_uuid(list,id)
    for _,t in pairs(list or {})do if tostring(get(t,'uuid'))==tostring(id)then return t end end
end
local function identity(t)
    local id=get(t,'uuid');if not id or tostring(id)==''then error('Mission UUID unavailable')end;return id
end
local function own_dispatch(t)
    local owner=get(t,'ownerInfo');local u=get(owner,'uid')or get(t,'ownerId')
    return not u or tostring(u)==uid()
end
local function member(t)
    for _,v in pairs(get(t,'memberList')or {})do if tostring(get(v,'uid'))==uid()then return v end end
end
local function current_state(kind)
    if kind=='dispatch'then return call(center('ActDispatchTaskDataManager'),'GetAllSingleTasks')end
    if kind=='dispatchAssist'then return call(center('ActDispatchTaskDataManager'),'GetAllAllianceTasks')end
    return get(center('ActGhostreconManager'),'taskList')
end
local function mission_state(t)
    local m=member(t)
    return {uuid=tostring(identity(t)),cfgId=num(t,'cfgId'),completionTime=num(t,'completionTime'),
        rewarded=num(t,'rewarded'),teamStartTime=num(t,'teamStartTime'),ownerId=tostring(get(t,'ownerId')or ''),
        sendChatTime=num(t,'sendChatTime'),joined=m~=nil,ownRewarded=m and num(m,'rewarded'),
        ownCanReward=m and num(m,'canReward')}
end
local function recommend(kind,task,cfg,teamCounts)
    local path=kind=='ghostRecon'and 'UI.UIDispatchTask.Ghostrecon.Formation.Controller.UIGhostreconFormationCtrl'
        or 'UI.UIFormation.UIFormationDispatchTask.Controller.UIFormationDispatchTaskCtrl'
    local ctrl=require(path)
    local heroes,met=call(ctrl,'GetRecommendHeroList',kind=='ghostRecon'and cfg or task,teamCounts)
    if type(heroes)~='table'or #heroes==0 or #heroes>3 or met~=true then return nil end
    local seen={};for _,h in ipairs(heroes)do
        if type(h)~='string'and type(h)~='number'or tostring(h)==''or seen[tostring(h)]then return nil end
        seen[tostring(h)]=true
    end
    return heroes
end
local function dispatch_quality(c,at)
    local clock=call(rawget(_G,'UITimeManager'),'GetInstance')
    local day=tonumber(call(clock,'GetNowWeekdayIndex'))
    if not day or day<1 or day>7 then error('Server weekday unavailable')end
    local defaults={'none','ur','none','none','none','ur','none'}
    local value=(c.weeklyQualities or defaults)[day]
    if value~='none'and value~='ssr'and value~='ur'then error('Invalid weekly mission quality')end
    -- GetTodayZeroServerTime accepts seconds and returns seconds (v26 lines 584-586).
    local zero=tonumber(call(clock,'GetTodayZeroServerTime',at/1000))
    if not zero then error('Server reset time unavailable')end
    return value,at>=zero*1000+((tonumber(c.delayMinutes)or 3)%1440)*60000
end
local function dispatch_plan(c,at,s)
    local p,b=fresh(s,'dispatch',at);if p then return p,b end
    local manager=center('ActDispatchTaskDataManager');local tasks=rows(current_state('dispatch'))
    if c.collectRewards==true or c.runNow then
        for _,t in ipairs(tasks)do
            if own_dispatch(t)and (num(t,'completionTime')or 0)>0 and num(t,'completionTime')<=at and num(t,'rewarded')==0 then
                local before=mission_state(t);local p,b=plan(s,'DispatchReward',{identity(t)},before,'dispatchClaim');p.uuid=before.uuid;return p,b
            end
        end
    end
    if c.autoExecute~=true and not c.runNow then return nil,{available=#tasks,reason='Automatic dispatch is off'}end
    local wanted,ready=dispatch_quality(c,at)
    if not ready and not c.runNow then return nil,{reason='Waiting for the saved delay after server reset'}end
    if call(rawget(_G,'SeasonUtil'),'IsInLandlordActAndOnCenterServer')then return nil,{reason='Secret Task dispatch is unavailable on this event server'}end
    local max=tonumber(call(manager,'GetMaxMarch'));local running=tonumber(call(manager,'GetSingleTaskIngCount'))
    if not max or not running then error('Mission dispatch capacity unavailable')end
    if running>=max then return nil,{available=#tasks,reason='All Secret Task dispatch slots are in use'}end
    local bad,blockedGood=false,false
    for _,t in ipairs(tasks)do
        if own_dispatch(t)and num(t,'completionTime')==0 then
            local cfg=get(t,'cfg');local color=num(cfg,'color')
            if not color then error('Mission quality unavailable')end
            local good=wanted=='none'or color>=(wanted=='ur'and 5 or 4)
            if good then
                local heroes=recommend('dispatch',t)
                if heroes then
                    -- Exact native OnStartClick payload, not WorldMarch or an attack route.
                    local view=require('UI.UIFormation.UIFormationDispatchTask.View.UIFormationDispatchTaskView')
                    local duration=tonumber(call(view,'CalculateMarchTime',num(t,'pointId')))
                    if not duration or duration<0 then error('Native mission march duration unavailable')end
                    local before=mission_state(t);before.heroCount=#heroes
                    local p,b=plan(s,'DispatchStart',{identity(t),heroes,duration},before,'dispatchStart');p.uuid=before.uuid;return p,b
                end
                blockedGood=true
            else bad=true end
        end
    end
    if bad and not blockedGood then
        -- Native normal refresh rerolls ALL unstarted tasks. Keep qualified tasks if
        -- their heroes are unavailable. Use owned tokens; original gem policy UNKNOWN.
        local _,itemId,cost=call(manager,'GetTaskRefreshSetting');itemId=tonumber(itemId);cost=tonumber(cost)
        local item=itemId and call(center('ItemData'),'GetItemById',itemId)
        local count=num(item,'count')or 0
        if itemId and cost and cost>0 and count>=cost then
            local before={itemId=itemId,itemCount=count,cost=cost,quality=wanted}
            local p,b=plan(s,'DispatchTaskRefresh',{0,0},before,'dispatchRefresh');return p,b
        end
        return {error='Chosen quality needs refresh tokens; automatic gem refresh is unavailable because the original spending policy is unverified'},
            {available=#tasks,quality=wanted,refreshTokens=count}
    end
    return nil,{available=#tasks,quality=wanted,reason=blockedGood and 'Eligible task is waiting for available heroes'or 'No pending Secret Task'}
end
local qualities={[1]='n',[2]='r',[3]='sr',[4]='ssr',[5]='ur'}
local function assist_plan(c,at,s)
    if c.autoHelp~=true and not c.runNow then return nil,{reason='Dispatch Assist is off'}end
    local alliance=tostring(get(player(),'allianceId')or '')
    if alliance==''or alliance=='0'then return nil,{reason='Join an alliance to assist Secret Tasks'}end
    local p,b=fresh(s,'dispatchAssist',at);if p then return p,b end
    local manager=center('ActDispatchTaskDataManager');local tasks=rows(current_state('dispatchAssist'))
    local count=tonumber(call(manager,'GetTodayAssistNum'));local limit=tonumber(call(manager,'GetDispatchSetting','aid_count'))
    if not count or not limit then error('Native Dispatch Assist daily limit unavailable')end
    if count>=limit then return nil,{available=#tasks,remaining=0,reason='Daily Dispatch Assist limit reached'}end
    local allowed={};for _,q in ipairs(c.qualities or {})do allowed[q]=true end
    local min=tonumber(get(c.delaySeconds,1))or 0;local max=tonumber(get(c.delaySeconds,2))or 0
    if min<0 or max<min or max>86400 then error('Invalid Dispatch Assist delay')end
    for _,t in ipairs(tasks)do
        local owner=get(get(t,'ownerInfo'),'uid')or get(t,'ownerId')
        local aid=tostring(get(t,'allianceId')or alliance)
        local quality=(num(get(t,'cfg'),'is_special')==1)and 'special'or qualities[num(get(t,'cfg'),'color')]
        if owner and tostring(owner)~=uid()and aid==alliance and allowed[quality]and
            (num(t,'completionTime')or 0)>0 and num(t,'completionTime')<=at and num(t,'rewarded')==0 then
            local id=identity(t);local key=tostring(id)
            if not s.delays[key]then s.delays[key]=num(t,'completionTime')+math.random(math.floor(min),math.floor(max))*1000 end
            if at>=s.delays[key]then
                local server=num(t,'targetServer');if not server or server<=0 then error('Alliance task target server unavailable')end
                local before={uuid=key,assistCount=count,ownerUid=tostring(owner),allianceId=alliance,quality=quality}
                local p,b=plan(s,'DispatchAssist',{id,server},before,'dispatchAssist');p.uuid=key;return p,b
            end
        end
    end
    return nil,{available=#tasks,remaining=limit-count,reason='No eligible completed alliance Secret Task matches the saved qualities and delay'}
end
local function super_counts(cfg,list)
    if type(get(cfg,'superCondions'))~='table'or #get(cfg,'superCondions')==0 then return {}end
    return call(cfg,'GetSuperCondionNumsByMemberList',list or {})
end
local function full_conditions(cfg,t)
    local conditions=get(cfg,'superCondions')or {};local counts=super_counts(cfg,get(t,'memberList'))
    for i,v in ipairs(conditions)do if (tonumber(counts[i])or 0)<(num(v,'num')or math.huge)then return false end end
    return true
end
local function ghost_plan(c,at,s)
    local p,b=fresh(s,'ghostRecon',at);if p then return p,b end
    local manager=center('ActGhostreconManager');local tasks=rows(current_state('ghostRecon'))
    if c.autoClaimRewards==true then
        for _,t in ipairs(tasks)do
            local m=member(t)
            if m and (num(t,'completionTime')or 0)>0 and num(t,'completionTime')<=at and num(m,'rewarded')==0 and
                (num(m,'canReward')or 0)>0 and (num(t,'actEndTime')or math.huge)>at then
                local server=num(t,'ownerServer');if not server or server<=0 then error('Ghost mission owner server unavailable')end
                local before=mission_state(t);local p,b=plan(s,'GhostReconReward',{identity(t),server},before,'ghostClaim');p.uuid=before.uuid;return p,b
            end
        end
    end
    if not c.autoStartOwn and not c.autoJoinAlliance then return nil,{available=#tasks,reason='No Ghost Ops reward is ready'}end
    local beginAt,endAt=num(manager,'dispatchBeginTime'),num(manager,'dispatchEndTime')
    if not beginAt or not endAt or not call(manager,'IsOpenDay')or at<beginAt or at>=endAt then
        return nil,{available=#tasks,reason='Ghost Ops is outside its current dispatch window'}
    end
    local alliance=tostring(get(player(),'allianceId')or '')
    if c.autoStartOwn then
        for _,t in ipairs(tasks)do
            if tostring(get(t,'ownerId'))==uid()and (num(t,'completionTime')or 0)==0 and (num(t,'taskExpireTime')or math.huge)>at then
                local cfg=call(manager,'GetTaskTemplate',get(t,'cfgId'))
                if (num(t,'teamStartTime')or 0)==0 then
                    local heroes=recommend('ghostRecon',t,cfg,{})
                    if heroes then local before=mission_state(t);local p,b=plan(s,'GhostReconJoinTeam',{identity(t),heroes,1},before,'ghostCreate');p.uuid=before.uuid;return p,b end
                else
                    if alliance~=''and alliance~='0'and (num(t,'sendChatTime')or 0)<num(t,'teamStartTime')then
                        local cooldown=tonumber(call(get(rawget(_G,'LuaEntry'),'DataConfig'),'TryGetNum','ghostrecon_config','k1',5))
                        if cooldown and at-(num(t,'sendChatTime')or 0)>=cooldown*1000 then
                            local before=mission_state(t);local p,b=plan(s,'GhostReconHandleSendChat',{identity(t),1,'0'},before,'ghostShare');p.uuid=before.uuid;return p,b
                        end
                    end
                    if call(t,'MemberIsFull')and full_conditions(cfg,t)then
                        local before=mission_state(t);local p,b=plan(s,'GhostReconStartTeam',{identity(t)},before,'ghostStart');p.uuid=before.uuid;return p,b
                    end
                end
            end
        end
    end
    if c.autoJoinAlliance and alliance~=''and alliance~='0'then
        if call(manager,'GetTeamworkRewardTimesFull')then return nil,{reason='Daily Ghost Ops alliance reward limit reached'}end
        if not s.allianceFreshAt or at-s.allianceFreshAt>30000 then
            local p,b=plan(s,'GhostReconGetAllianceTaskList',{}, {fresh=false},'ghostAllianceList');p.readOnly=true;p.version=s.allianceVersion;return p,b
        end
        local candidates=rows(get(center('ActGhostreconAllianceManager'),'allianceTaskList'))
        for _,t in ipairs(candidates)do
            local cfg=call(manager,'GetTaskTemplate',get(t,'cfgId'));local special=get(cfg,'special')==true
            local filter=c.allianceFilter or 'special';local color=num(cfg,'color')
            local selected=filter=='special'and special or filter=='ur'and color and color>=5 or filter=='sr'and color and color>=3
            local own=member(t);local total=0;for _ in pairs(get(t,'memberList')or {})do total=total+1 end
            local expiry=(num(t,'teamStartTime')or 0)+(num(cfg,'showTime')or 0)
            if selected and tostring(get(t,'ownerId'))~=uid()and not own and (num(t,'completionTime')or 0)==0 and expiry>at and
                total<(tonumber(call(manager,'GetTeamMaxMemberNum'))or 0)then
                local heroes=recommend('ghostRecon',t,cfg,super_counts(cfg,get(t,'memberList')))
                if heroes then local before=mission_state(t);before.allianceId=alliance
                    local p,b=plan(s,'GhostReconJoinTeam',{identity(t),heroes,2},before,'ghostJoin');p.uuid=before.uuid;return p,b end
            end
        end
        return nil,{available=#tasks,allianceCandidates=#candidates,reason='No eligible alliance Ghost Ops mission matches the saved filter'}
    end
    return nil,{available=#tasks,reason='Own Ghost Ops missions are waiting for heroes or a full team'}
end
function M.Supports(kind)return specs[kind]~=nil end
function M.Interval(kind,c)return kind=='dispatchAssist'and math.max(5,math.min(300,tonumber(c.intervalSeconds)or 30))*1000 or 60000 end
function M.Plan(kind,c,at)
    if not M.Supports(kind)then return {error='Unknown mission service'}end
    if not enabled(kind,c)then return nil,{reason='Mission service is off'}end
    local s=ensure(kind)
    if kind=='dispatch'then return dispatch_plan(c,at,s)end
    if kind=='dispatchAssist'then return assist_plan(c,at,s)end
    return ghost_plan(c,at,s)
end
function M.Verify(kind,job)
    local s=states[kind]
    -- A stale continuation must not read or send anything after Stop/account change.
    if not s or s.uid~=uid()or job.uid~=s.uid or job.generation~=s.generation then return false end
    if job.phase=='missionList'then
        if s.version>job.version then s.freshAt=job.sentAt;return true,{fresh=true,available=#rows(current_state(kind))}end
        return false
    end
    if job.phase=='ghostAllianceList'then
        if s.allianceVersion>job.version then s.allianceFreshAt=job.sentAt;return true,{fresh=true}end
        return false
    end
    -- Every write is reconciled with a separate fresh own/alliance list request.
    if not job.postQuerySent then
        job.postQuerySent=true;job.postQueryVersion=s.version;job.followupReads=1
        get(rawget(_G,'SFSNetwork'),'SendMessage')(route(specs[kind].query));return false
    end
    local reply=job.replyName and s.replies and s.replies[job.replyName]
    reply=reply and reply.seq>job.replySeq and reply or nil
    if reply and reply.error~=nil then
        -- The game refused the action (nothing happened): retry later, never an uncertain lock.
        s.freshAt=nil;return false,{uuid=job.uuid,phase=job.phase,gameCode=tostring(reply.error)},60000
    end
    if s.version<=job.postQueryVersion then return false end
    local t=task_by_uuid(current_state(kind),job.uuid);local after=t and mission_state(t)or {uuid=job.uuid,missing=true}
    local confirmed=false
    -- Live v27 (2026-10-21): a claimed own task leaves the fresh list (13 -> 12 -> 11), it is not kept as rewarded.
    if job.phase=='dispatchClaim'then confirmed=(t==nil and s.version>job.postQueryVersion)or t and own_dispatch(t)and num(t,'rewarded')==1
    elseif job.phase=='dispatchStart'then confirmed=t and own_dispatch(t)and (num(t,'completionTime')or 0)>job.sentAt
    elseif job.phase=='dispatchRefresh'then
        local item=call(center('ItemData'),'GetItemById',job.before.itemId)
        after={itemId=job.before.itemId,itemCount=num(item,'count')or 0,quality=job.before.quality}
        confirmed=after.itemCount==job.before.itemCount-job.before.cost
    elseif job.phase=='dispatchAssist'then
        after.assistCount=tonumber(call(center('ActDispatchTaskDataManager'),'GetTodayAssistNum'))
        confirmed=not t and after.assistCount==job.before.assistCount+1
    elseif job.phase=='ghostCreate'then confirmed=t and tostring(get(t,'ownerId'))==s.uid and member(t)~=nil and (num(t,'teamStartTime')or 0)>0
    elseif job.phase=='ghostJoin'then confirmed=t and member(t)~=nil
    elseif job.phase=='ghostShare'then confirmed=t and tostring(get(t,'ownerId'))==s.uid and (num(t,'sendChatTime')or 0)>=job.sentAt-1000 and num(t,'sendChatTime')>(job.before.sendChatTime or 0)
    elseif job.phase=='ghostStart'then confirmed=t and tostring(get(t,'ownerId'))==s.uid and (num(t,'completionTime')or 0)>job.sentAt
    elseif job.phase=='ghostClaim'then confirmed=(t==nil and s.version>job.postQueryVersion)or t and member(t)~=nil and num(member(t),'rewarded')==1 end
    -- A clean game reply to our own Ghost action is the server's acceptance, even when the refreshed
    -- list does not show the exact field we expected.
    if not confirmed and reply and reply.error==nil then confirmed=true;after.acceptedByReply=true end
    if confirmed then s.freshAt=job.sentAt;s.allianceFreshAt=nil end
    return confirmed,after
end
function M.Stop(kind)
    local s=states[kind];states[kind]=nil
    if s then for _,r in ipairs(s.listeners)do pcall(call,s.manager,'RemoveListener',r[1],r[2])end end
    if s and s.hooks then for i=#s.hooks,1,-1 do local h=s.hooks[i];if h.module.HandleMessage==h.wrapper then h.module.HandleMessage=h.previous end end end
end
return M
