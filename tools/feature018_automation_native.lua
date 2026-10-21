-- R30 current v26 adapter. Native routes are recovered from admitted 3/26 bytecode.
-- Original 0.3.17 engines are UNKNOWN; policy/confirmation is OWN_DESIGN.
local M={}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function route(k)return get(rawget(_G,'MsgDefines'),k)end
local giftEvents,giftVersion,giftFresh=nil,0,nil
local scienceEvents,scienceVersion,scienceFresh=nil,0,nil
local scienceDetailVersion,scienceDetailsFresh=0,{}
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
    local list=call(center('AllianceHelpDataManager'),'GetOtherHelpList')
    if type(list)~='table'then error('Alliance Help list is not ready')end
    local ids={};for k,v in pairs(list)do
        ids[#ids+1]=tostring(get(v,'uuid')or get(v,'id')or k)
    end
    table.sort(ids);return {available=#ids,identity=table.concat(ids,',')}
end
function M.Supports(kind)return kind=='allianceHelp'or kind=='allianceGift'or kind=='allianceDonate'end
function M.Interval(kind,c)return kind=='allianceGift'and (tonumber(c.intervalMinutes)or 120)*60000 or 60000 end
function M.Plan(kind,c,at)
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
        if not before.scienceId or before.remaining<(tonumber(c.threshold)or 15)or before.balance<before.cost then return nil,before end
        -- AlScienceDonate option 1 is the ordinary-resource branch. Never option 2 / GoldDonate.
        return {route=route('AlScienceDonate'),args={before.scienceId,1},scienceId=before.scienceId},before
    end
    return {error='This native Automation route is unavailable'}
end
function M.Verify(kind,job)
    if kind=='allianceHelp'then
        local after=help()
        return after.available==0 and job.before.available>0,after
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
    end
    return false
end
function M.Stop(kind)
    if kind=='allianceGift'and giftEvents then
        pcall(call,giftEvents.manager,'RemoveListener',giftEvents.event,giftEvents.fn);giftEvents=nil;giftFresh=nil
    end
    if kind=='allianceDonate'and scienceEvents then
        for _,r in ipairs(scienceEvents.rows)do pcall(call,scienceEvents.manager,'RemoveListener',r[1],r[2])end
        scienceEvents=nil;scienceFresh=nil;scienceDetailsFresh={}
    end
end
return M
