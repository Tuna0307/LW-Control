-- R30 current v26 adapter. Native routes are recovered from admitted 3/26 bytecode.
-- Original 0.3.17 engines are UNKNOWN; policy/confirmation is OWN_DESIGN.
local M={}
local MISSIONS,ALLIANCE='DataCenter.Global.LWBridgeFeature018MissionsNative','DataCenter.Global.LWBridgeFeature018AllianceResourcesNative'
local moduleNames={treatment='DataCenter.Global.LWBridgeFeature018TreatmentNative',
    dispatch=MISSIONS,dispatchAssist=MISSIONS,
    resourceGather='DataCenter.Global.LWBridgeFeature018GatherNative',
    strongholdResource=ALLIANCE,allianceCenterResource=ALLIANCE,
    tradeStation='DataCenter.Global.LWBridgeFeature018TradeNative',
    allianceGather='DataCenter.Global.LWBridgeFeature018AllianceGatherNative'}
local modules={}
local overview
local function adapter(kind)
    if not moduleNames[kind]then return nil end
    if not modules[kind]then
        modules[kind]=require(moduleNames[kind])
        if modules[kind].SetBridge then modules[kind].SetBridge(overview)end
    end
    return modules[kind]
end
function M.SetBridge(value)
    overview=value
    for _,module in pairs(modules)do if module.SetBridge then module.SetBridge(value)end end
end
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function route(k)return get(rawget(_G,'MsgDefines'),k)end
local giftEvents,giftVersion,giftFresh=nil,0,nil
local scienceEvents,scienceVersion,scienceFresh=nil,0,nil
local scienceDetailVersion,scienceDetailsFresh=0,{}
local donateActive=false
local officialEvents,officialVersions,officialFresh=nil,{},{}
local officialAppointmentVersions,officialObservation={},nil
-- Owner direction 2026-10-21: while the chosen list is full, keep checking and
-- apply as soon as a place opens. Only the apply list is re-read while full.
local OFFICIAL_FULL_RETRY_MS,OFFICIAL_REFUSED_RETRY_MS=3000,5000
local officialApplyFresh,officialFull,officialLastReason={},{},{}
-- KingdomPositionApplyMessage.HandleMessage 17-25 only shows errorCode as a tip.
-- Pass-through observer (same pattern as FindMonster in the AFK adapter).
local officialReply=nil
local function official_reply_observer()
    if officialReply then return true end
    local ok,class=pcall(require,'Net.Msgs.Government.OfficialApply.KingdomPositionApplyMessage')
    local previous=ok and get(class,'HandleMessage')
    if type(previous)~='function'then return false end
    local o={class=class,previous=previous,count=0}
    o.wrapper=function(self,response,...)
        o.count=o.count+1;o.errorCode=get(response,'errorCode')
        return previous(self,response,...)
    end
    class.HandleMessage=o.wrapper;officialReply=o;return true
end
local function official_reply_release()
    if officialReply and get(officialReply.class,'HandleMessage')==officialReply.wrapper then
        officialReply.class.HandleMessage=officialReply.previous
    end
    officialReply=nil
end
local function official_full(position,count)
    local manager=center('OfficialApplyManager')
    local ok,limit=pcall(function()return call(rawget(_G,'DataConfig'),'TryGetNum','auto_wonder_config','k3')end)
    limit=ok and tonumber(limit)or nil
    local flagged=false;pcall(function()flagged=call(manager,'GetApplyListFull')==true end)
    return limit~=nil and limit>0 and count>=limit or flagged
end
local function official_listener()
    if officialEvents then return end
    local event=get(rawget(_G,'EventId'),'OfficialApplyListInit')
    if not event then error('Native official application list event is unavailable')end
    local manager=call(rawget(_G,'EventManager'),'GetInstance')
    local fn=function(position)local id=tonumber(position);if id then officialVersions[id]=(officialVersions[id]or 0)+1 end end
    local appointmentEvent=get(rawget(_G,'EventId'),'OfficialAppointmentListRefresh')
    if not appointmentEvent then error('Native appointment queue event is unavailable')end
    local appointmentFn=function(position)local id=tonumber(position);if id then officialAppointmentVersions[id]=(officialAppointmentVersions[id]or 0)+1 end end
    call(manager,'AddListener',event,fn);call(manager,'AddListener',appointmentEvent,appointmentFn)
    officialEvents={manager=manager,rows={{event,fn},{appointmentEvent,appointmentFn}}}
end
local function official_state(position)
    local manager=center('OfficialApplyManager')
    local list=call(manager,'GetApplyList',tostring(position))
    if type(list)~='table'then error('The selected official application list is not loaded')end
    local uid=tostring(get(get(rawget(_G,'LuaEntry'),'Player'),'uid'))
    local queued=false;local count=0
    for _,applicant in pairs(list)do count=count+1;if tostring(get(applicant,'uid'))==uid then queued=true end end
    local appointments=call(manager,'GetAppointmentList',tostring(position));local appointed=false;local appointmentCount=0
    if type(appointments)=='table'then for _,applicant in pairs(appointments)do
        appointmentCount=appointmentCount+1;if tostring(get(applicant,'uid'))==uid then appointed=true end
    end end
    local held=call(manager,'GetOwnAppointIdName')
    return {positionId=position,currentPositionId=tonumber(held)or 0,applyQueueLength=count,
        ownApplicationPosition=tonumber(call(manager,'GetOwnApplyPositionId'))or 0,queued=queued,uid=uid,
        appointed=appointed,appointmentQueueLength=appointmentCount}
end
local function science_listener()
    if scienceEvents then return end
    local event=get(rawget(_G,'EventId'),'OnGetAllianceTechMessage')
    if not event then error('Native alliance research update event is unavailable')end
    local manager=call(rawget(_G,'EventManager'),'GetInstance')
    local fn=function()scienceVersion=scienceVersion+1 end
    local detailEvent=get(rawget(_G,'EventId'),'AllianceTechnology')
    if not detailEvent then error('Native research detail event is unavailable')end
    local detailFn=function()scienceDetailVersion=scienceDetailVersion+1 end
    call(manager,'AddListener',event,fn);call(manager,'AddListener',detailEvent,detailFn)
    scienceEvents={manager=manager,rows={{event,fn},{detailEvent,detailFn}}}
end
local function donation_candidate(at)
    local manager=center('AllianceScienceDataManager')
    local remaining=tonumber(call(manager,'GetResDonateRestCount'))
    if not remaining then error('Ordinary donation attempts are unavailable')end
    local recommended=call(manager,'GetCurRecommendScience')
    local candidates={};if recommended then candidates[1]=recommended end
    local list=get(manager,'alScienceList');if type(list)~='table'then error('Alliance research is not loaded')end
    local others={};for _,science in pairs(list)do if science~=recommended then others[#others+1]=science end end
    table.sort(others,function(a,b)return (tonumber(get(a,'scienceId'))or 0)<(tonumber(get(b,'scienceId'))or 0)end)
    for _,science in ipairs(others)do candidates[#candidates+1]=science end
    local ctrl=require('UI.UIAlliance.UIAllianceScienceInfo.Controller.UIAllianceScienceInfoCtrl')
    local counts={total=#candidates,donatable=0,ordinary=0,examples={}}
    for _,science in ipairs(candidates)do
        local id=tonumber(get(science,'scienceId'))
        if id and id>0 then
            -- Native UI reader checks dependencies, level, progress and research state.
            local details=call(ctrl,'GetScienceDataById',id)
            local resource,cost=tonumber(get(details,'resType')),tonumber(get(details,'resNum'))
            if get(details,'canDonate')==true and (not scienceDetailsFresh[id]or at-scienceDetailsFresh[id]>30000)then
                return {scienceId=id,remaining=remaining,needsDetail=true}
            end
            local types=rawget(_G,'ResourceType');local premium=tonumber(get(types,'Gold'))
            local ordinary=premium and resource and resource>0 and resource~=premium
            for _,name in ipairs({'Diamond','Diamonds','Gem','Gems'})do
                if tonumber(get(types,name))==resource then ordinary=false end
            end
            if get(details,'canDonate')==true then counts.donatable=counts.donatable+1 end
            if ordinary then counts.ordinary=counts.ordinary+1 end
            if #counts.examples<4 then counts.examples[#counts.examples+1]={scienceId=id,canDonate=get(details,'canDonate'),resourceType=resource,cost=cost}end
            if get(details,'canDonate')==true and ordinary and cost and cost>=0 then
                local balance=tonumber(call(get(rawget(_G,'LuaEntry'),'Resource'),'GetCntByResType',resource))
                if not balance then error('Ordinary resource balance is unavailable')end
                return {scienceId=id,remaining=remaining,resourceType=resource,cost=cost,balance=balance,
                    progress=tonumber(get(science,'currentPro'))or 0,level=tonumber(get(science,'curLevel'))or 0}
            end
        end
    end
    return {remaining=remaining,reason='No eligible ordinary-resource alliance research',candidates=counts}
end
local function gift_listener()
    if giftEvents then return end
    local event=get(rawget(_G,'EventId'),'RefreshAllianceGift')
    if not event then error('Native Alliance Gift update event is unavailable')end
    local manager=call(rawget(_G,'EventManager'),'GetInstance')
    local fn=function()giftVersion=giftVersion+1 end
    call(manager,'AddListener',event,fn);giftEvents={manager=manager,event=event,fn=fn}
end
local function gifts(at)
    local manager=center('AllianceGiftDataManager');local lists=get(manager,'giftInfoList')
    if type(lists)~='table'then error('Native gift lists are unavailable')end
    local data={normalAvailable=0,advancedAvailable=0,ids={}}
    for _,kind in ipairs({1,2})do
        local ids={};local list=get(lists,kind)
        if type(list)~='table'then error('Native gift category is unavailable')end
        for _,gift in pairs(list)do
            if tonumber(get(gift,'receiveState'))==0 and (tonumber(get(gift,'expirationTime'))or 0)>at+30000 then
                local uuid=get(gift,'uuid');if not uuid or tostring(uuid)==''then error('Native gift identity unavailable')end
                ids[#ids+1]=tostring(uuid)
            end
        end
        table.sort(ids);data.ids[kind]=ids
        data[kind==1 and 'normalAvailable'or 'advancedAvailable']=#ids
    end
    return data
end
local function help()
    local manager=center('AllianceHelpDataManager')
    local list=call(manager,'GetOtherHelpList')
    if type(list)~='table'then error('Alliance Help list is not ready')end
    local ids={};for k,v in pairs(list)do
        ids[#ids+1]=tostring(get(v,'uuid')or get(v,'id')or k)
    end
    -- Live owner main 2026-10-21: after a help-all the game empties this list and refills it only when the
    -- Help screen opens; new requests arrive via PushAlHelpNewMessage -> SetHelpNum (the hand bubble count).
    local count=tonumber(select(2,pcall(call,manager,'GetHelpNum')))or 0
    table.sort(ids);return {available=math.max(#ids,count),helpNum=count,identity=table.concat(ids,','),ids=ids}
end
function M.Supports(kind)return moduleNames[kind]~=nil or kind=='allianceHelp'or kind=='allianceGift'or kind=='allianceDonate'or kind=='officialPosition'end
function M.Interval(kind,c)
    local a=adapter(kind);if a then return a.Interval(kind,c)end
    -- Owner direction 2026-10-21: Help checks every 3 s (it only reads the local help list until there is work).
    if kind=='allianceHelp'then return 3000 end
    return kind=='allianceGift'and (tonumber(c.intervalMinutes)or 120)*60000 or 60000
end
function M.Plan(kind,c,at)
    local a=adapter(kind);if a then return a.Plan(kind,c,at)end
    if kind=='allianceHelp'then
        local before=help()
        if before.available==0 then return nil,before end
        -- UIAllianceHelpCtrl.OnHelpAllClick 126-129: server milliseconds, UI effect position.
        -- The optional effect position is omitted for background operation.
        return {route=route('AlHelpAll'),args={math.floor(at)},key=before.identity},before
    elseif kind=='allianceGift'then
        gift_listener()
        if not giftFresh or at-giftFresh>30000 then
            return {readOnly=true,phase='giftList',route=route('AllianceGiftList'),args={0,1000},version=giftVersion},{fresh=false}
        end
        local before=gifts(at)
        for _,category in ipairs({1,2})do
            if #before.ids[category]>0 then
                return {route=route('AllianceReceiveAllGift'),args={category},category=category,ids=before.ids[category],continue=true},before
            end
        end
        return nil,before
    elseif kind=='allianceDonate'then
        science_listener()
        if not scienceFresh or at-scienceFresh>30000 then
            return {readOnly=true,phase='scienceList',route=route('AllScienceRefresh'),args={},version=scienceVersion},{fresh=false}
        end
        local before=donation_candidate(at)
        if before.needsDetail then
            return {readOnly=true,phase='scienceDetail',route=route('AlScienceFresh'),args={before.scienceId},
                scienceId=before.scienceId,version=scienceDetailVersion},before
        end
        -- "Start donating at N": once attempts reach N, keep donating until none are left, then wait for N again.
        local threshold=tonumber(c.threshold)or 15
        if before.remaining>=threshold then donateActive=true elseif before.remaining<=0 then donateActive=false end
        if not donateActive then before.reason='Waiting until '..threshold..' donation attempts are available ('..tostring(before.remaining)..' now)';return nil,before end
        if not before.scienceId or before.balance<before.cost then return nil,before end
        -- AlScienceDonate option 1 is the ordinary-resource branch. Never option 2 / GoldDonate.
        return {route=route('AlScienceDonate'),args={before.scienceId,1},scienceId=before.scienceId,continue=true},before
    elseif kind=='officialPosition'then
        local position=tonumber(c.positionId)
        if not position or position==0 then return nil,{reason='Choose an official position first'}end
        official_listener()
        if not officialFresh[position]or at-officialFresh[position]>30000 then
            return {readOnly=true,phase='officialList',route=route('KingdomPositionApplyList'),args={tostring(position)},
                positionId=position,version=officialVersions[position]or 0},{positionId=position,fresh=false,reason=officialLastReason[position]}
        end
        if officialFull[position]and at-(officialApplyFresh[position]or 0)>=OFFICIAL_FULL_RETRY_MS-500 then
            return {readOnly=true,phase='officialApplyList',route=route('KingdomPositionApplyList'),args={tostring(position)},
                -- Keep the last explanation visible while the list is re-read (owner 2026-10-22: "Waiting" alone was unclear).
                positionId=position,version=officialVersions[position]or 0},{positionId=position,full=true,reason=officialLastReason[position]}
        end
        local before=official_state(position);local manager=center('OfficialApplyManager')
        if before.queued or before.appointed or before.currentPositionId==position or before.ownApplicationPosition>0 then
            officialFull[position]=nil
            -- Live 2026-10-22: appointed=true with currentPositionId 0 means accepted and waiting in the
            -- appointment queue, not holding the position yet.
            before.reason=before.currentPositionId==position and 'You hold this position now'
                or before.appointed and ('Accepted; waiting in the appointment queue ('..tostring(before.appointmentQueueLength or '?')..' in line)')
                or 'Applied; waiting for the President to approve'
            before.done=true
            officialLastReason[position]=before.reason;return nil,before
        end
        if not call(manager,'CheckCanApply',tostring(position))then before.reason='This position is not available for applications';return nil,before end
        before.full=official_full(position,before.applyQueueLength)
        officialFull[position]=before.full or nil
        local reason,cd=call(manager,'GetOwnApplicantData',tostring(position),false)
        if before.full then
            before.reason=reason and tostring(reason)~=''and tostring(reason)or 'The application list is full; checking again every 3 seconds'
            officialLastReason[position]=before.reason
            -- Re-plan each second; the list itself is re-read at most every 3 s (gate above).
            before.retryMs=1000;return nil,before
        end
        if reason and tostring(reason)~=''then
            before.reason=tostring(reason);officialLastReason[position]=before.reason
            -- A personal re-apply cooldown is temporary: check again when it ends.
            if tonumber(cd)and tonumber(cd)>0 then before.retryMs=math.min(math.max(tonumber(cd)+1000,OFFICIAL_FULL_RETRY_MS),3600000)end
            return nil,before
        end
        local observed=official_reply_observer()
        return {route=route('KingdomPositionApply'),args={tostring(position)},positionId=position,uid=before.uid,
            replyObserved=observed,replyCount=observed and officialReply.count or nil},before
    end
    return {error='This native Automation route is unavailable'}
end
function M.Verify(kind,job)
    local a=adapter(kind);if a then return a.Verify(kind,job)end
    if kind=='allianceHelp'then
        -- Busy alliances (owner main, 2026-10-21): new requests can arrive at once. Confirm that every
        -- request that was helped has left the list, not that the list is empty.
        local after=help();local now={}
        -- Help-all clears the game's own count (OnHelpAll -> SetHelpNum(0)); a lower count is the post-state.
        if (job.before.helpNum or 0)>0 and after.helpNum<job.before.helpNum then return true,after end
        if #(job.before.ids or {})==0 then return false,after end
        for _,id in ipairs(after.ids or {})do now[id]=true end
        for _,id in ipairs(job.before.ids or {})do if now[id]then return false,after end end
        return true,after
    elseif kind=='allianceGift'then
        if job.phase=='giftList'then
            if giftVersion>job.version then giftFresh=job.sentAt;return true,gifts(job.sentAt)end
            return false
        end
        local manager=center('AllianceGiftDataManager');local list=get(get(manager,'giftInfoList'),job.category)
        local claimed=0
        for _,uuid in ipairs(job.ids)do
            local gift=get(list,uuid)
            if gift and tonumber(get(gift,'receiveState'))==1 and (tonumber(get(gift,'receiveTime'))or 0)>=job.sentAt-5000 then claimed=claimed+1 end
        end
        return claimed==#job.ids,{category=job.category,claimed=claimed,expected=#job.ids}
    elseif kind=='allianceDonate'then
        if job.phase=='scienceList'then
            if scienceVersion>job.version then scienceFresh=job.sentAt;return true,{fresh=true}end
            return false
        end
        if job.phase=='scienceDetail'then
            local science=call(center('AllianceScienceDataManager'),'GetOneAllianceScienceById',job.scienceId)
            local resource,cost=tonumber(get(science,'res')),tonumber(get(science,'resNum'))
            if scienceDetailVersion>job.version and resource and resource>0 and cost and cost>=0 then
                scienceDetailsFresh[job.scienceId]=job.sentAt
                return true,{scienceId=job.scienceId,resourceType=resource,cost=cost,fresh=true}
            end
            return false
        end
        local manager=center('AllianceScienceDataManager')
        local science=call(manager,'GetOneAllianceScienceById',job.scienceId)
        local after={scienceId=job.scienceId,remaining=tonumber(call(manager,'GetResDonateRestCount')),
            progress=tonumber(get(science,'currentPro')),level=tonumber(get(science,'curLevel')),
            balance=tonumber(call(get(rawget(_G,'LuaEntry'),'Resource'),'GetCntByResType',job.before.resourceType))}
        return after.remaining==job.before.remaining-1 and after.progress and after.level and
            (after.progress>job.before.progress or after.level>job.before.level),after
    elseif kind=='officialPosition'then
        local version=officialVersions[job.positionId]or 0
        if job.phase=='officialList'then
            if version>job.version then
                if not job.appointmentReadSent then
                    job.appointmentReadSent=true;job.appointmentVersion=officialAppointmentVersions[job.positionId]or 0;job.followupReads=1
                    get(rawget(_G,'SFSNetwork'),'SendMessage')(route('KingdomPositionAppointmentList'),tostring(job.positionId));return false
                end
                if (officialAppointmentVersions[job.positionId]or 0)>job.appointmentVersion then
                    officialFresh[job.positionId]=job.sentAt;officialApplyFresh[job.positionId]=job.sentAt
                    return true,official_state(job.positionId)
                end
            end
            return false
        end
        if job.phase=='officialApplyList'then
            if version>job.version then officialApplyFresh[job.positionId]=job.sentAt;return true,{positionId=job.positionId,fresh=true}end
            return false
        end
        if job.replyObserved and officialReply and officialReply.count>job.replyCount and officialReply.errorCode~=nil then
            -- The server answered this application with a refusal: nothing was queued,
            -- so a later application is not a duplicate. Read the list again first.
            officialFresh[job.positionId]=nil;officialApplyFresh[job.positionId]=nil;officialFull[job.positionId]=nil
            return false,{positionId=job.positionId,errorCode=tostring(officialReply.errorCode),
                reason='The game refused the application (code '..tostring(officialReply.errorCode)..'); trying again'},OFFICIAL_REFUSED_RETRY_MS
        end
        if not job.postQuerySent then
            job.postQuerySent=true;job.postQueryVersion=version;job.postAppointmentVersion=officialAppointmentVersions[job.positionId]or 0;job.followupReads=2
            local sender=get(rawget(_G,'SFSNetwork'),'SendMessage')
            if type(sender)~='function'then return false end
            -- Only a read is sent here. The application is never repeated.
            sender(route('KingdomPositionApplyList'),tostring(job.positionId))
            sender(route('KingdomPositionAppointmentList'),tostring(job.positionId))
            return false
        end
        if version<=job.postQueryVersion or (officialAppointmentVersions[job.positionId]or 0)<=job.postAppointmentVersion then return false end
        local after=official_state(job.positionId)
        if (after.queued or after.appointed or after.currentPositionId==job.positionId)and after.uid==job.uid then return true,after end
        -- The game answered without an error code (its own success path) but the
        -- refreshed lists do not show us: record the server acceptance, never resend.
        if job.replyObserved and officialReply and officialReply.count>job.replyCount and officialReply.errorCode==nil then
            after.serverAccepted=true;return true,after
        end
        return false
    end
    return false
end
function M.FinishStop(kind)
    local a=adapter(kind);if a and a.FinishStop then return a.FinishStop(kind)end
end
function M.Stop(kind)
    local a=adapter(kind);if a then return a.Stop(kind)end
    if kind=='allianceGift'and giftEvents then
        pcall(call,giftEvents.manager,'RemoveListener',giftEvents.event,giftEvents.fn);giftEvents=nil;giftFresh=nil
    end
    if kind=='allianceDonate'and scienceEvents then
        for _,r in ipairs(scienceEvents.rows)do pcall(call,scienceEvents.manager,'RemoveListener',r[1],r[2])end
        scienceEvents=nil;scienceFresh=nil;scienceDetailsFresh={};donateActive=false
    end
    if kind=='officialPosition'and officialEvents then
        for _,r in ipairs(officialEvents.rows)do pcall(call,officialEvents.manager,'RemoveListener',r[1],r[2])end
        officialEvents=nil;officialFresh={};officialObservation=nil
    end
    if kind=='officialPosition'then official_reply_release();officialApplyFresh={};officialFull={};officialLastReason={}end
end
function M.Observe(kind,c,at,scope)
    local a=adapter(kind);if a and a.Observe then return a.Observe(kind,c,at,scope)end
    if kind~='officialPosition'then return {state='failed',error='Read-only observation is unavailable for this service'}end
    local position=tonumber(c.positionId)
    if not position or position==0 then return {state='failed',error='Choose an official position before observing'}end
    official_listener()
    local key=scope..'/'..tostring(position)
    if not officialObservation or officialObservation.key~=key then
        officialObservation={key=key,at=at,version=officialVersions[position]or 0,appointmentVersion=officialAppointmentVersions[position]or 0}
        local sender=get(rawget(_G,'SFSNetwork'),'SendMessage')
        sender(route('KingdomPositionApplyList'),tostring(position));sender(route('KingdomPositionAppointmentList'),tostring(position))
        return {state='accepted',data={accepted=true,readOnly=true,awaitingFreshLists=true}}
    end
    local o=officialObservation
    if (officialVersions[position]or 0)>o.version and (officialAppointmentVersions[position]or 0)>o.appointmentVersion then
        local data=official_state(position);data.readOnly=true;M.Stop(kind)
        return {state='confirmed',data=data}
    end
    if at-o.at>15000 then M.Stop(kind);return {state='failed',error='Fresh official lists were not available; no application was sent'}end
    return {state='accepted',data={accepted=true,readOnly=true,awaitingFreshLists=true}}
end
return M
