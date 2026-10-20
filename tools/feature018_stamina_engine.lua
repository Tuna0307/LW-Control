-- R20 OWN_DESIGN scheduler for the recovered stamina-item settings.
-- Native ItemUse route and inventory classification are adapter-owned.
local M={}
local api,run
function M.Bind(adapter)api=assert(adapter);return M end
function M.Start(owner,config)
    if not api.Owned(owner) then return nil,'Account/session changed' end
    if type(config)~='table' or type(config.enabled)~='boolean' or
       type(config.minStamina)~='number' or config.minStamina<0 or
       config.minStamina>9999 or config.minStamina%1~=0 or type(config.preferFifty)~='boolean' then
        return nil,'Invalid stamina-item settings'
    end
    if run and run.pending then
        if api.Owned(run.owner) and run.owner.token==owner.token and run.state~='failed' then
            run.config={enabled=config.enabled,minStamina=config.minStamina,preferFifty=config.preferFifty}
            run.running=config.enabled;return true
        end
        return nil,'A prior stamina item still needs reconciliation'
    end
    if run and run.running and api.Owned(run.owner) and run.owner.token==owner.token then
        run.config={enabled=config.enabled,minStamina=config.minStamina,preferFifty=config.preferFifty}
        return true
    end
    run={owner=owner,config={enabled=config.enabled,minStamina=config.minStamina,preferFifty=config.preferFifty},
        running=config.enabled,state=config.enabled and 'waiting' or 'stopped',used=0,receipts={}}
    return true
end
function M.Stop()
    if run then run.running=false;run.state='stopped' end
end
function M.Fail(reason)
    if run then run.running=false;run.state='failed';run.lastError=tostring(reason)end
end
function M.IsPending()return run and run.pending~=nil or false end
function M.Status()
    if not run then return {running=false,state='idle',used=0,receipts={}}end
    return {running=run.running,state=run.state,step=run.step,used=run.used,lastError=run.lastError,
        stamina=run.stamina,pending=run.pending~=nil,receipts=run.receipts}
end
function M.Pump()
    if not run or not (run.running or run.pending) then return end
    if not api.Owned(run.owner) then run.state='waiting';run.step='account_changed';return end
    local snapshot,error=api.Snapshot()
    if not snapshot or not snapshot.stamina then run.state='waiting';run.step='inventory_unavailable';run.lastError=error;return end
    run.stamina=snapshot.stamina;run.lastError=nil
    if run.pending then
        local pending=run.pending;local count=0
        for _,item in ipairs(snapshot.items or {})do if item.id==pending.item.id then count=item.count end end
        if count==pending.item.count-1 and snapshot.stamina>pending.stamina then
            run.used=run.used+1
            run.receipts[#run.receipts+1]={itemId=pending.item.id,itemUuid=pending.item.uuid,
                countBefore=pending.item.count,countAfter=count,staminaBefore=pending.stamina,
                staminaAfter=snapshot.stamina,confirmed=true}
            if #run.receipts>20 then table.remove(run.receipts,1)end
            run.pending=nil;run.state=run.running and 'waiting' or 'stopped';run.step='item_confirmed'
        elseif api.Now()-pending.at>30 then
            run.running=false;run.state='failed';run.lastError='Native item count and stamina change were not both confirmed; no retry'
        end
        return
    end
    if not run.running then return end
    if snapshot.stamina>=run.config.minStamina then run.state='waiting';run.step='above_limit';return end
    local items={}
    for _,item in ipairs(snapshot.items or {})do
        if item.staminaItem==true and item.count>0 and item.recovery>0 then items[#items+1]=item end
    end
    table.sort(items,function(a,b)
        -- Owned items all cost zero gems. The recovered explicit 50-point
        -- preference breaks that cost tie; otherwise choose the smallest.
        if run.config.preferFifty and (a.recovery==50)~=(b.recovery==50) then return a.recovery==50 end
        if a.recovery~=b.recovery then return a.recovery<b.recovery end
        return a.id<b.id
    end)
    local item=items[1]
    if not item then run.state='waiting';run.step='no_owned_items';return end
    if not run.running or not api.Owned(run.owner) then return end
    -- Arm before invoking the native producer. Exceptions retain this fence.
    run.pending={item={id=item.id,uuid=item.uuid,count=item.count,recovery=item.recovery,staminaItem=true},
        stamina=snapshot.stamina,at=api.Now()}
    local ok,err=api.Use(run.owner,run.pending.item)
    if not ok then run.running=false;run.state='failed';run.lastError=err or 'Native item send uncertain; no retry'
    else run.state='waiting';run.step='confirming_item' end
end
return M
