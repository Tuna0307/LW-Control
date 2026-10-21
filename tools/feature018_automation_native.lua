-- R30 current v26 adapter. Native routes are recovered from admitted 3/26 bytecode.
-- Original 0.3.17 engines are UNKNOWN; policy/confirmation is OWN_DESIGN.
local M={}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function route(k)return get(rawget(_G,'MsgDefines'),k)end
local giftEvents,giftVersion,giftFresh=nil,0,nil
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
function M.Supports(kind)return kind=='allianceHelp'or kind=='allianceGift'end
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
    end
    return false
end
function M.Stop(kind)
    if kind=='allianceGift'and giftEvents then
        pcall(call,giftEvents.manager,'RemoveListener',giftEvents.event,giftEvents.fn);giftEvents=nil;giftFresh=nil
    end
end
return M
