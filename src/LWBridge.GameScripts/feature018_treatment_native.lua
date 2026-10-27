-- R30 treatment adapter: RECOVERED v26 hospital message/data contracts.
-- Original 0.3.17 engine UNKNOWN; batching, cadence and confirmation OWN_DESIGN.
-- Scheduler owns account/lease admission, off gate and uncertain-send tombstones.
local M={}
local listeners,versions=nil,{update=0,finish=0,help=0}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' is unavailable')end;return f(o,...)end
local function center(k)return get(rawget(_G,'DataCenter'),k)end
local function enum(name,key)local v=get(rawget(_G,name),key);if v==nil then error('Native '..name..'.'..key..' is unavailable')end;return v end
local function num(v,name)local n=tonumber(v);if not n or n~=n or n<0 or n==math.huge then error('Native '..name..' is unavailable')end;return n end
local function listen()
    if listeners then return end
    local manager=call(rawget(_G,'EventManager'),'GetInstance');listeners={manager=manager,rows={}}
    for _,row in ipairs({{'HospitalUpdate','update'},{'HospitalFinish','finish'},{'AllianceQueueHelpNew','help'}})do
        local event=enum('EventId',row[1]);local key=row[2]
        local fn=function(qtype)if key~='help'or qtype==enum('NewQueueType','Hospital')then versions[key]=versions[key]+1 end end
        call(manager,'AddListener',event,fn);listeners.rows[#listeners.rows+1]={event,fn}
    end
end
local function snapshot()
    local queue=call(center('QueueDataManager'),'GetQueueByType',enum('NewQueueType','Hospital'))
    local data={wounded=0,treating=0,armies={}}
    local hospitals=call(center('HospitalManager'),'GetAllHospital')
    if type(hospitals)~='table'then error('Native hospital soldiers are unavailable')end
    for _,v in pairs(hospitals)do
        local id=tonumber(get(v,'armyId'));if not id or id<=0 then error('Native hospital army identity is unavailable')end
        local dead,heal=num(get(v,'dead'),'wounded count'),num(get(v,'heal'),'treating count')
        if dead%1~=0 or heal%1~=0 or data.armies[id]then error('Invalid native hospital soldier counts')end
        data.armies[id]={dead=dead,heal=heal};data.wounded=data.wounded+dead;data.treating=data.treating+heal
    end
    if queue then
        -- Validate normal hospital before calling GetQueueState: DragonHospital's
        -- implementation has an automatic collection side effect.
        if get(queue,'type')~=enum('NewQueueType','Hospital')then error('Unexpected hospital queue type')end
        local uuid=get(queue,'uuid');if not uuid or tostring(uuid)==''or tostring(uuid)=='0'then error('Native hospital queue identity unavailable')end
        data.queue={uuid=tostring(uuid),nativeUuid=uuid,state=call(queue,'GetQueueState'),
            startTime=num(get(queue,'startTime'),'queue start'),endTime=num(get(queue,'endTime'),'queue end'),
            isHelped=num(get(queue,'isHelped'),'queue help state'),itemId=get(queue,'itemId'),
            helpNum=num(get(queue,'helpNum')or 0,'help count'),lastHelpTime=num(get(queue,'lastHelpTime')or 0,'help timestamp')}
    end
    return data
end
local function plan(phase,route,args,before)
    local r=enum('MsgDefines',route)
    return {phase=phase,route=r,args=args,continue=true,version=versions[phase=='cure'and 'update'or phase=='collect'and 'finish'or 'help']},before
end
function M.Supports(kind)return kind=='treatment'end
function M.Interval()return 2000 end
function M.Plan(kind,c,at)
    if not M.Supports(kind)then return nil end
    local amount=tonumber(c.amountPerArmy)or 1
    if amount%1~=0 or amount<1 or amount>1000000 then error('Treatment amount must be an integer from 1 to 1000000')end
    local before=snapshot();local q=before.queue
    if not q then before.reason='No hospital queue is available';return nil,before end
    if call(center('CityRebuildDataManager'),'GetCureState')then before.reason='City recovery treatment is active';return nil,before end
    if q.state==enum('NewQueueState','Finish')and before.treating>0 then
        before.soldiers=num(call(center('SoldierDataManager'),'GetPlayerSoldiersTotalNum'),'soldier stock')
        local capacity=num(call(get(rawget(_G,'LuaEntry'),'Effect'),'GetGameEffect',enum('EffectDefine','LW_SOLDIER_MAX_STOCK')),'soldier stock capacity')
        if before.soldiers>=math.floor(capacity)then before.reason='Soldier stock is full';return nil,before end
        listen();return plan('collect','QueueFinish',{{uuid=q.nativeUuid}},before)
    elseif q.state==enum('NewQueueState','Work')then
        before.reason='Treatment is in progress'
        if q.isHelped~=0 then return nil,before end
        if call(get(rawget(_G,'LuaEntry'),'Player'),'IsInAlliance')~=true then before.reason='Join an alliance to ask for treatment help';return nil,before end
        local limit=num(call(get(rawget(_G,'LuaEntry'),'DataConfig'),'TryGetNum','medical_assist_daily_limit','k1'),'medical help daily limit')
        local sameDay=false
        if q.lastHelpTime>0 then sameDay=call(call(rawget(_G,'UITimeManager'),'GetInstance'),'IsSameDayForServer',math.floor(q.lastHelpTime/1000),math.floor(at/1000))end
        if sameDay and q.helpNum>=limit then before.reason='Daily medical help limit reached';return nil,before end
        if q.itemId==nil then error('Native hospital queue item identity unavailable')end
        listen();return plan('help','AllianceCallHelp',{q.nativeUuid,enum('AllianceHelpType','Queue'),enum('NewQueueType','Hospital'),q.itemId},before)
    elseif q.state~=enum('NewQueueState','Free')then before.reason='Hospital queue is not ready';return nil,before end
    if before.wounded==0 then before.reason='No wounded soldiers';return nil,before end
    local types=rawget(_G,'ResourceType');local metal,food=enum('ResourceType','Metal'),enum('ResourceType','Food')
    for _,key in ipairs({'Gold','Diamond','Diamonds','Gem','Gems'})do
        local premium=get(types,key);if premium~=nil and (premium==metal or premium==food)then error('Hospital ordinary resource type is invalid')end
    end
    local resource=get(rawget(_G,'LuaEntry'),'Resource')
    local balances={[metal]=num(call(resource,'GetCntByResType',metal),'metal balance'),[food]=num(call(resource,'GetCntByResType',food),'food balance')}
    local ids={};for id,v in pairs(before.armies)do if v.dead>0 then ids[#ids+1]=id end end;table.sort(ids)
    local arr,total={},0
    for _,id in ipairs(ids)do
        local template=call(center('SoldierDataManager'),'GetTemplate',id);local costs=get(template,'cureCost')
        if type(costs)~='table'then error('Native treatment resource costs unavailable')end
        local cm,cf=num(get(costs,metal)or 0,'metal cure cost'),num(get(costs,food)or 0,'food cure cost')
        for r,cost in pairs(costs)do if r~=metal and r~=food and num(cost,'cure cost')>0 then error('Treatment requires an unsupported resource')end end
        local count=math.min(amount,before.armies[id].dead)
        if cm>0 then count=math.min(count,math.floor(balances[metal]/cm))end
        if cf>0 then count=math.min(count,math.floor(balances[food]/cf))end
        if count>0 then arr[id]=count;total=total+count;balances[metal]=balances[metal]-cm*count;balances[food]=balances[food]-cf*count end
    end
    if total==0 then before.reason='Not enough ordinary resources for treatment';return nil,before end
    listen();local job=plan('cure','HospitalCure',{{arr=arr,gold=0}},before);job.armies=arr;job.count=total
    return job,before
end
function M.Verify(kind,job)
    if not M.Supports(kind)or not listeners then return false end
    local before=job.before;local after=snapshot();local b,q=before.queue,after.queue
    if not q or q.uuid~=b.uuid then return false,after end
    if job.phase=='cure'then
        if versions.update<=job.version or q.state==enum('NewQueueState','Free')or (q.startTime==b.startTime and q.endTime==b.endTime)then return false,after end
        for id,count in pairs(job.armies)do
            local old,v=before.armies[id],after.armies[id]
            if not v or v.dead~=old.dead-count or v.heal~=old.heal+count then return false,after end
        end
        after.batchesStarted=1;after.soldiersQueued=job.count;return true,after
    elseif job.phase=='help'then
        if versions.help<=job.version or b.isHelped~=0 or q.isHelped~=1 then return false,after end
        after.helpsRequested=1;return true,after
    elseif job.phase=='collect'then
        if versions.finish<=job.version or after.treating~=0 or q.state~=enum('NewQueueState','Free')then return false,after end
        after.soldiers=num(call(center('SoldierDataManager'),'GetPlayerSoldiersTotalNum'),'soldier stock')
        if after.soldiers~=before.soldiers+before.treating then return false,after end
        after.collected=before.treating;return true,after
    end
    return false,after
end
function M.Stop(kind)
    if not M.Supports(kind)or not listeners then return end
    local old=listeners;listeners=nil
    for _,row in ipairs(old.rows)do pcall(call,old.manager,'RemoveListener',row[1],row[2])end
    -- Monotonic versions also invalidate observations retained before stopping.
    versions={update=versions.update+1,finish=versions.finish+1,help=versions.help+1}
end
return M
