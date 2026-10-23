-- R20 OWN_DESIGN event scheduler for recovered Auto Alliance Drill settings.
-- Native event/target/rally/squad permissions are supplied by the adapter.
local M={}
local api,run
local function copy(v)
    if type(v)~='table'then return v end
    local out={};for k,x in pairs(v)do out[k]=copy(x)end;return out
end
local function valid(c)
    if type(c)~='table' or type(c.enabled)~='boolean' or type(c.activeRally)~='boolean' or
       type(c.squadIndexes)~='table' or c.enabled and #c.squadIndexes==0 then return false end
    local seen={}
    for _,slot in ipairs(c.squadIndexes)do
        if type(slot)~='number' or slot%1~=0 or slot<1 or slot>4 or seen[slot] then return false end
        seen[slot]=true
    end
    if c.squadRoles~=nil then
        if type(c.squadRoles)~='table'then return false end
        for k,v in pairs(c.squadRoles)do
            local slot=tonumber(k)
            if not slot or slot%1~=0 or slot<1 or slot>4 or (v~='launch'and v~='join'and v~='both')then return false end
        end
    end
    return true
end
-- Owner 2026-10-22: each squad can be "launch" only, "join" only, or "both" (default: launch when
-- Launch rallies is on and no own rally exists, otherwise join).
-- The Launch rallies switch was removed (owner 2026-10-22): a squad without a saved role keeps the
-- old meaning of that switch (on = both, off = join only); an explicit "both" always may launch.
local function squad_role(c,slot)
    local roles=type(c.squadRoles)=='table'and c.squadRoles or {}
    local r=roles[tostring(slot)]or roles[slot]
    if r=='launch'or r=='join'or r=='both'then return r end
    return c.activeRally and 'both' or 'join'
end
function M.Bind(adapter)api=assert(adapter);return M end
function M.Start(owner,config)
    if not valid(config)then return nil,'Invalid Alliance Drill settings' end
    if not api.Owned(owner)then return nil,'Alliance Drill account changed'end
    if run and next(run.pending)~=nil and not api.Owned(run.owner) then return nil,'Prior Drill belongs to another account'end
    if run and run.owner.token==owner.token then
        run.config=copy(config);run.running=config.enabled
        if not config.enabled then M.Stop()end
        return true
    end
    run={owner=copy(owner),config=copy(config),running=config.enabled,pending={},rows={},
        state=config.enabled and 'waiting' or 'stopped',step='waiting_event',active=false,confirmedSends=0,receipts={}}
    return true
end
function M.Stop()
    if not run then return end
    run.running=false;run.state='stopped';run.step='stopped'
    for slot,job in pairs(run.pending)do if api.CancelUnsent(job)then run.pending[slot]=nil end end
end
function M.Fail(error)
    M.Stop();if run then run.state='failed';run.lastError=tostring(error)end
end
function M.Status()
    if not run then return {running=false,state='idle',active=false,rows={},receipts={},confirmedSends=0}end
    local rows=copy(run.rows);for _,row in ipairs(rows)do row.running=run.running end
    return {running=run.running,state=run.state,step=run.step,active=run.active,
        confirmedSends=run.confirmedSends,rows=rows,receipts=copy(run.receipts),lastError=run.lastError}
end
function M.Pump()
    if not run or not (run.running or next(run.pending)~=nil)then return end
    if not api.Owned(run.owner)then run.state='waiting';run.step='account_changed';return end
    local event=api.Snapshot(run.owner,run.running)
    local active=event and event.active==true
    run.active=active;run.rows={}
    for slot,job in pairs(run.pending)do
        local same_target=active and event.target and
            event.target.uuid==(job.role=='leader' and job.target.uuid or job.target.monsterUuid)
        if (not same_target or not run.running) and api.CancelUnsent(job)then run.pending[slot]=nil
        else
            local observation=api.Observe(job,same_target and run.running)
            if observation.failed then run.running=false;run.state='failed';run.lastError=observation.error;return end
            if (observation.acknowledged or observation.completed) and not job.acknowledged then
                job.acknowledged=true;run.confirmedSends=run.confirmedSends+1
                run.receipts[#run.receipts+1]=copy(observation.receipt)
                if #run.receipts>20 then table.remove(run.receipts,1)end
            end
            run.rows[#run.rows+1]={squadIndex=slot,activity='allianceDrill',activityRole=job.role,
                step=observation.step or 'joining',running=run.running,currentTarget=copy(job.target)}
            if observation.completed then run.pending[slot]=nil end
        end
    end
    if not run.running then return end
    if not active then run.state='waiting';run.step='waiting_event';run.lastError=event and event.error;return end
    local target=event.target
    if not target or target.monster~=true or target.player==true then
        run.state='waiting';run.step='waiting_nearby_target';run.lastError=event.error;return
    end
    run.state='running';run.lastError=nil
    local leader_exists=event.ownRallyExists==true
    for _,job in pairs(run.pending)do if job.role=='leader'then leader_exists=true end end
    for _,slot in ipairs(run.config.squadIndexes)do
        if not run.pending[slot] and api.Free(slot)then
            local mode=squad_role(run.config,slot)
            local role=mode=='launch'and 'leader' or mode=='join'and 'member'
                or (not leader_exists and 'leader' or 'member')
            local chosen
            if role=='leader'then
                -- A launch-only squad waits while your own rally is already running; it never joins.
                if not leader_exists then chosen=target end
            else
                for _,rally in ipairs(event.rallies or {})do
                    if rally.monster==true and rally.player~=true and rally.joinable==true and
                       rally.monsterUuid==target.uuid and api.JoinReady(run.config,rally)then chosen=rally;break end
                end
            end
            local stamina,cost=api.Stamina(slot,role)
            local step=(not stamina or not cost or stamina<cost) and 'waiting_stamina' or 'waiting_drill_rally'
            if chosen and stamina and cost and stamina>=cost and api.Owned(run.owner) and run.running then
                local sent,error=api.Send(run.owner,slot,role,chosen,run.config)
                if sent then
                    run.pending[slot]={slot=slot,role=role,target=copy(chosen),native=sent}
                    if role=='leader'then leader_exists=true end
                    step=role=='leader' and 'attacking' or 'joining'
                    run.rows[#run.rows+1]={squadIndex=slot,activity='allianceDrill',activityRole=role,step=step,running=true,currentTarget=copy(chosen)}
                    break
                else run.lastError=error end
            end
            run.rows[#run.rows+1]={squadIndex=slot,activity='allianceDrill',activityRole=role,
                step=step,running=true,currentTarget=copy(chosen)}
        end
    end
    run.step=run.rows[1] and run.rows[1].step or 'waiting_squad'
end
return M
