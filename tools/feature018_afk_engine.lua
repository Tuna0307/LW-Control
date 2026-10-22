-- R20 OWN_DESIGN scheduler. Recovered 0.3.17 settings are inputs, never targets.
-- The native adapter alone resolves typed monsters and owns all game sends.
-- No action on require. Every send is fenced by the captured account/session.
local M = {}
local api, run
local function copy(value)
    if type(value) ~= 'table' then return value end
    local out = {}; for k,v in pairs(value) do out[k] = copy(v) end; return out
end
local function enabled_profiles(config)
    local out={}
    for _,p in ipairs(config.strategies or {}) do
        if p.enabled then out[#out+1]=p end
    end
    return out
end
local function matches(p,t,level)
    if type(t)~='table' or t.monster~=true or t.player==true then return false end
    if p.kind=='join' then
        if t.joinable~=true or t.rally~=true then return false end
    elseif t.rallyOnly and p.rally~=true then return false end
    if p.rally~=true and t.worldBoss then return false end
    if p.monsterType~=nil and t.monsterType~=p.monsterType then return false end
    if p.monsterSpecial~=nil and t.special~=p.monsterSpecial then return false end
    if p.targetNameQuery and p.targetNameQuery~='' then
        if not string.find(string.lower(t.name or ''),string.lower(p.targetNameQuery),1,true) then return false end
    elseif p.monsterNameKey and p.monsterNameKey~='' and t.nameKey~=p.monsterNameKey then return false end
    if p.monsterIds and #p.monsterIds>0 then
        local found=false;for _,id in ipairs(p.monsterIds) do if id==t.monsterId then found=true end end
        if not found then return false end
    end
    if p.levelFilterEnabled and (not t.level or t.level<(level or p.minLevel) or
       t.level>(p.progressiveLevels and (level or p.minLevel) or p.maxLevel)) then return false end
    if p.distanceFilterEnabled and (not t.distance or t.distance>p.maxDistance) then return false end
    return true
end
M.Matches=matches
-- Owner 2026-10-22: a squad may have its own level range inside a profile (e.g. Squad 1 1-80,
-- Squad 2 1-40). Without one, the squad uses the profile's level settings.
local function slot_profile(p,slot)
    local r=type(p.squadLevels)=='table'and (p.squadLevels[tostring(slot)]or p.squadLevels[slot])
    if type(r)~='table'or type(r.min)~='number'or type(r.max)~='number'then return p end
    return setmetatable({levelFilterEnabled=true,progressiveLevels=false,minLevel=r.min,maxLevel=r.max},{__index=p})
end
M.SlotProfile=slot_profile
-- OWN_DESIGN interpretation of recovered join controls. The randomly chosen
-- position/delay is retained per rally, rather than rerolled on every frame.
function M.JoinReady(r,t,wait,clock,random)
    if not t or t.monster~=true or t.player==true or not t.joinable then return false,'not_monster_rally' end
    if not r or r.enabled==false then return true end
    local listed=false
    for _,leader in ipairs(r.leaders or {})do if leader.uid==t.leaderUid then listed=true end end
    if r.leaderListMode=='whitelist' and not listed or r.leaderListMode=='blacklist' and listed then return false,'leader_list' end
    if r.skipSoloLeader and (t.leaderHeroCount==nil or t.leaderHeroCount<=1)then return false,'solo_leader' end
    if r.skipKicked and t.kicked then return false,'removed_from_rally' end
    if (r.maxWaitMinutes or 0)>0 and (not t.departureTime or t.departureTime-clock>r.maxWaitMinutes*60)then return false,'departure_limit' end
    if (r.minMembers or 0)>0 and (not t.memberCount or t.memberCount<r.minMembers)then return false,'member_limit' end
    random=random or math.random
    if r.mode=='slot' then
        if not t.memberCount then return false,'member_count_unknown' end
        local range=r.slotRange or {2,2}
        wait.position=wait.position or random(range[1],range[2])
        if t.memberCount+1<wait.position then wait.at=nil;return false,'waiting_join_delay' end
        wait.at=wait.at or clock+(r.slotDelaySeconds or 0)
    else
        local range=r.delaySeconds or {0,0}
        if not wait.at then wait.at=clock+range[1]+random()*(range[2]-range[1])end
    end
    return clock>=wait.at,'waiting_join_delay'
end
function M.IsRunning()return run~=nil and run.running==true end
function M.Pause(reason)
    if run and run.running then run.state='waiting';run.step=reason or 'account_changed' end
end
function M.Bind(adapter) api=assert(adapter);return M end
local function validate_config(config)
    if type(config)~='table' or type(config.strategies)~='table' then return nil,'Profile settings are invalid' end
    for _,p in ipairs(config.strategies)do
        if p.enabled then
            if type(p.id)~='string' or (p.kind~='farm' and p.kind~='join') or
               type(p.monsterType)~='number' or p.monsterType<0 or p.monsterType%1~=0 or
               type(p.squadIndexes)~='table' or #p.squadIndexes==0 or
               type(p.executionLimit)~='number' or p.executionLimit<0 or p.executionLimit%1~=0 then
                return nil,'Enabled AFK profile has invalid target, squad or execution settings'
            end
            for _,slot in ipairs(p.squadIndexes)do if type(slot)~='number' or slot%1~=0 or slot<1 or slot>4 then return nil,'Invalid assigned squad' end end
            if p.levelFilterEnabled and (type(p.minLevel)~='number' or p.minLevel<1 or p.minLevel%1~=0 or
               not p.progressiveLevels and (type(p.maxLevel)~='number' or p.maxLevel<p.minLevel or p.maxLevel%1~=0)) then return nil,'Invalid AFK level range' end
            if p.distanceFilterEnabled and (type(p.maxDistance)~='number' or p.maxDistance<=0)then return nil,'Invalid distance limit'end
            if p.squadLevels~=nil then
                if type(p.squadLevels)~='table'then return nil,'Invalid squad level ranges'end
                for k,r in pairs(p.squadLevels)do
                    local slot=tonumber(k)
                    if not slot or slot<1 or slot>4 or slot%1~=0 or type(r)~='table'or type(r.min)~='number'or type(r.max)~='number'or
                       r.min<1 or r.min%1~=0 or r.max%1~=0 or r.max<r.min then return nil,'Invalid squad level ranges'end
                end
            end
        end
    end
    return true
end
function M.Start(owner,config)
    if run and run.running then return nil,'Already running; Stop before starting again' end
    if not api or not api.Owned(owner) then return nil,'Account/session is not current' end
    local valid,error=validate_config(config)
    if not valid then return nil,error end
    run={owner=copy(owner),config=copy(config),running=true,state='running',step='scanning_nearby',
        processed={},pending={},levels={},rows={},receipts={},lastError=nil}
    for _,p in ipairs(enabled_profiles(config)) do run.processed[p.id]=0;run.levels[p.id]=p.minLevel or 1 end
    return M.Status()
end
function M.Stop(reason)
    if run then run.running=false;run.state='stopped';run.step='stopped';run.lastError=reason end
    -- Do not recall or cancel an already sent march. In-flight sends remain
    -- observable and cannot be replayed by a subsequent Start.
    if api and api.Stop then api.Stop() end
    return M.Status()
end
function M.Configure(config)
    if not run then return nil,'No captured running account' end
    local valid,error=validate_config(config)
    if not valid then return nil,error end
    run.config=copy(config)
    local needs_run=false
    for _,p in ipairs(enabled_profiles(config)) do
        run.processed[p.id]=run.processed[p.id] or 0
        run.levels[p.id]=run.levels[p.id] or p.minLevel or 1
        if p.executionLimit==0 or run.processed[p.id]<p.executionLimit then needs_run=true end
    end
    if config.enabled and needs_run then run.running=true;run.state='running' end
    return M.Status()
end
function M.Status()
    local status=copy(run or {running=false,state='idle',step='idle',processed={},rows={},receipts={}})
    for _,row in ipairs(status.rows)do row.running=status.running and row.step~='completed' end
    return status
end
function M.Pump()
    if not run or not run.running then return end
    if not api.Owned(run.owner) then run.state='waiting';run.step='account_changed';return end
    run.state='running';run.rows={};run.lastError=nil
    local profiles=enabled_profiles(run.config)
    local configured={};for _,p in ipairs(profiles)do configured[p.id]=true end
    for _,job in pairs(run.pending)do
        if not configured[job.profileId] then
            local observer=copy(job.profile);observer.enabled=false
            profiles[#profiles+1]=observer;configured[job.profileId]=true
        end
    end
    local used={}
    local all_complete=#profiles>0
    for _,p in ipairs(profiles) do
        local count=run.processed[p.id] or 0
        local limit=tonumber(p.executionLimit) or 0
        local reserved=0
        for _,job in pairs(run.pending) do if job.profileId==p.id then reserved=reserved+1 end end
        if p.enabled and (limit==0 or count<limit) then all_complete=false end
        for _,slot in ipairs(p.squadIndexes or {}) do
            if used[slot] then goto next_slot end
            local row={squadIndex=slot,profileId=p.id,running=true,strategyProcessed=run.processed,
                step='idle',currentTarget=nil}
            run.rows[#run.rows+1]=row
            local job=run.pending[slot]
            if job then
                used[slot]=true
                row.profileId=job.profileId;row.currentTarget=copy(job.target)
                if not p.enabled and api.CancelUnsent and api.CancelUnsent(job)then
                    run.pending[slot]=nil;row.step='stopped';goto next_slot
                end
                local result=api.Observe(job,p.enabled==true)
                row.step=result.step or 'attacking'
                if result.failed then
                    run.running=false;run.state='failed';run.lastError=result.error or 'Native send outcome uncertain';return
                end
                if result.completed then
                    run.processed[job.profileId]=(run.processed[job.profileId] or 0)+1
                    run.receipts[#run.receipts+1]=copy(result.receipt)
                    if #run.receipts>20 then table.remove(run.receipts,1)end
                    run.pending[slot]=nil
                    local completed_profile=p
                    for _,candidate in ipairs(profiles)do if candidate.id==job.profileId then completed_profile=candidate end end
                    if completed_profile.progressiveLevels and result.won~=false then
                        local key=job.profileId
                        run.levels[key]=math.min((run.levels[key] or completed_profile.minLevel)+1,api.MaxLevel(completed_profile))
                    end
                end
            elseif p.enabled and (limit==0 or count+reserved<limit) and api.Free(slot,p) then
                local stamina,cost=api.Stamina(slot,p)
                if not stamina or not cost or stamina<cost then row.step='waiting_stamina';run.state='waiting'
                else
                    local sp=slot_profile(p,slot)
                    local level=sp==p and run.levels[p.id] or nil
                    local targets,reason=api.Targets(sp,level or (sp~=p and sp.minLevel or nil))
                    row.step=reason or 'waiting_nearby_target'
                    for _,target in ipairs(targets or {}) do
                        if matches(sp,target,level) then
                            row.currentTarget=copy(target)
                            -- Fresh mutable account and squad checks at the last
                            -- boundary; the adapter repeats them before native send.
                            if not run.running or not api.Owned(run.owner) then return end
                            if not api.Free(slot,p) then row.step='waiting_squad';break end
                            local sent,error=api.Send(run.owner,slot,p,target)
                            if sent then
                                used[slot]=true
                                run.pending[slot]={profileId=p.id,profile=copy(p),slot=slot,target=copy(target),native=sent}
                                reserved=reserved+1;row.step='attacking'
                            elseif error then
                                row.lastError=error;row.step='waiting';run.lastError=error
                            end
                            break
                        end
                    end
                end
            elseif limit>0 and count>=limit then row.step='completed'
            else row.step='waiting_squad' end
            ::next_slot::
        end
    end
    if all_complete and next(run.pending)==nil then run.running=false;run.state='completed';run.step='completed'
    elseif #profiles==0 then run.state='waiting';run.step='waiting_profiles'
    else run.step=run.rows[1] and run.rows[1].step or 'waiting' end
end
return M
