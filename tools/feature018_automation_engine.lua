-- R30 OWN_DESIGN scheduler. RECOVERED settings and v26 routes live in the adapter.
-- No game reads, subscriptions or sends on require, or while every service is off.
local M={}
local bridge,native,array,owner
local configs,services,pending,uncertain,force={},{},{},{},{}
local stopped={}
local active=false
local next_tick=0
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then return false end;return pcall(f,o,...)end
local function clock()
    local ok,m=call(rawget(_G,'UITimeManager'),'GetInstance')
    local yes,n=call(ok and m,'GetServerTime');return yes and tonumber(n)or os.time()*1000
end
local function account()
    local p=get(rawget(_G,'LuaEntry'),'Player');local u=get(p,'uid')
    if not u or not tostring(u):match('^[1-9]%d*$')then return nil end
    local a,b=call(p,'GetSourceServerId');local c,d=call(p,'GetSelfServerId');local e,f=call(p,'GetCurServerId')
    if not a or not c or not e or not tonumber(b)or tonumber(b)<=0 or tostring(b)~=tostring(d)or tostring(b)~=tostring(f)then return nil end
    return tostring(u)..'/'..tostring(b)
end
local function owned()return owner and bridge and bridge.Feature018OwnerToken()==owner.token and account()==owner.account end
local function recalc()
    active=false
    for kind,c in pairs(configs)do if c.enabled==true or force[kind]then active=true;return end end
end
local function row(kind)
    if not services[kind]then services[kind]={name=kind,state='disabled',submitted=0,confirmed=0,receipts={}}end
    return services[kind]
end
local function ok(data)return {state='confirmed',data=data}end
local function fail(message)return {state='failed',error=message,errorCode='AUTOMATION_UNAVAILABLE'}end
local function measured(kind,fn,...)
    local start=bridge and type(bridge.PerformanceBegin)=='function'and bridge.PerformanceBegin()or nil
    local values=table.pack(pcall(fn,...))
    if start then bridge.PerformanceEnd('automation_'..kind,start)end
    return table.unpack(values,1,values.n)
end
local function apply(request,explicit)
    local a=account();local t=bridge and bridge.Feature018OwnerToken()
    if not a or not t or tostring(get(get(rawget(_G,'LuaEntry'),'Player'),'uid'))~=tostring(request.authorizedUid)then
        return fail('The authenticated game account changed; Automation stopped')
    end
    local yes,json=pcall(require,'rapidjson');local decoded,data=false,nil
    if yes then decoded,data=pcall(json.decode,request.confirmedAutomationConfig or '')end
    if not decoded or type(data)~='table'then return fail('Confirmed profile Automation settings are unavailable')end
    if owner and (owner.account~=a or owner.token~=t)then
        configs={};services={};pending={};force={};stopped={};active=false
    end
    owner={account=a,token=t}
    for kind,config in pairs(data)do
        if native.Supports(kind)then
            if explicit and request.kind==kind then stopped[kind]=nil end
            if stopped[kind]then
                local copy={};for k,v in pairs(config)do copy[k]=v end;copy.enabled=false;config=copy
            end
            local previous=configs[kind]
            configs[kind]=config
            local r=row(kind);r.enabled=config.enabled==true
            if config.enabled~=true and ((previous and previous.enabled==true)or explicit and request.kind==kind)then
                -- Retain the send tombstone; off never creates a retry path.
                if pending[kind]then if not pending[kind].readOnly then uncertain[kind]=pending[kind]end;pending[kind]=nil end
                force[kind]=nil;r.state='disabled';r.nextRunAt=nil
                if native.Stop then native.Stop(kind)end
            elseif config.enabled==true and not(previous and previous.enabled==true)then
                r.state='waiting';r.nextRunAt=0
            end
        end
    end
    recalc();return ok({configured=true})
end
function M.Configure(request)return apply(request,true)end
function M.Observe(request)
    local a=account();local token=bridge and bridge.Feature018OwnerToken()
    if not a or not token or tostring(get(get(rawget(_G,'LuaEntry'),'Player'),'uid'))~=tostring(request.authorizedUid)then return fail('Game account admission changed')end
    local yes,json=pcall(require,'rapidjson');if not yes then return fail('Native JSON decoder unavailable')end
    local decoded,data=pcall(json.decode,request.confirmedAutomationConfig or '')
    if not decoded or type(data)~='table'or not data[request.kind]then return fail('Confirmed Automation settings unavailable')end
    return native.Observe(request.kind,data[request.kind],clock(),a..'/'..tostring(token))
end
function M.IsActive()return active end
function M.Stop(request)
    if owner and not owned()then M.CleanupAfterLeaseLoss();return fail('The game account changed; Automation stopped')end
    local kind=request.kind
    if not native.Supports(kind)then return fail('This Automation service is unavailable')end
    if pending[kind]then if not pending[kind].readOnly then uncertain[kind]=pending[kind]end;pending[kind]=nil end
    configs[kind]={enabled=false};force[kind]=nil
    if native.Stop then native.Stop(kind)end
    stopped[kind]=true
    local r=row(kind);r.enabled=false;r.state='disabled';r.nextRunAt=nil
    recalc();return ok({stopped=true})
end
function M.Status(request)
    if request then local r=apply(request,false);if r.state~='confirmed'then return r end end
    if array then for _,r in pairs(services)do array(r.receipts)end end
    return ok({services=services,readOnly=true,observation='native_automation_runtime'})
end
function M.Run(request)
    local r=apply(request,false);if r.state~='confirmed'then return r end
    local kind=request.kind
    if not native.Supports(kind)then return fail('This Automation service is unavailable')end
    if uncertain[kind]then return fail('An earlier action is unconfirmed. Check the game; it will not be sent again')end
    if not pending[kind]then force[kind]=true;row(kind).nextRunAt=0;active=true;next_tick=0 end
    return {state='accepted',data={accepted=true,queued=true,requestDispatched=false,kind=kind}}
end
local function finish(kind,r,at,state,message)
    r.state=state;r.error=message;r.lastRunAt=at;r.nextRunAt=at+native.Interval(kind,configs[kind])
    r.cycleStart=nil
    force[kind]=nil
    if configs[kind]and configs[kind].enabled~=true and native.Stop then native.Stop(kind)end
end
function M.Pump()
    if not active then return end
    local at=clock();if at<next_tick then return end;next_tick=at+1000
    if not owned()then M.CleanupAfterLeaseLoss();return end
    for kind,c in pairs(configs)do
        if c.enabled==true or force[kind]then
            local r=row(kind);local job=pending[kind]
            if uncertain[kind]then r.state='unconfirmed';r.error='An earlier action is unconfirmed; it will not be sent again'
            elseif job then
                local yes,confirmed,after=measured(kind,native.Verify,kind,job)
                if yes and confirmed then
                    pending[kind]=nil;r.after=after
                    if job.readOnly then
                        r.readRequests=(r.readRequests or 0)+(job.followupReads or 0)
                        r.state='waiting';r.nextRunAt=0
                    else
                        r.confirmed=r.confirmed+1;r.receipts[#r.receipts+1]={at=at,before=job.before,after=after,route=job.route,nativePostState=true}
                        r.readRequests=(r.readRequests or 0)+(job.followupReads or 0)
                        if kind=='allianceGift'and type(after)=='table'then
                            local key=after.category==1 and 'normalClaimed'or 'advancedClaimed'
                            r[key]=(r[key]or 0)+(tonumber(after.claimed)or 0)
                        elseif kind=='allianceDonate'and type(after)=='table'then
                            r.donated=(r.donated or 0)+1;r.remaining=after.remaining;r.scienceId=after.scienceId
                        elseif kind=='officialPosition'and type(after)=='table'then
                            r.positionId=after.positionId;r.applyQueueLength=after.applyQueueLength
                        end
                        if #r.receipts>30 then table.remove(r.receipts,1)end
                        if job.continue and r.confirmed-(r.cycleStart or 0)<30 then r.state='waiting';r.nextRunAt=0
                        else finish(kind,r,at,'success')end
                    end
                elseif at-job.sentAt>=15000 then
                    if not job.readOnly then uncertain[kind]=job end
                    pending[kind]=nil;finish(kind,r,at,job.readOnly and 'error'or 'unconfirmed','No confirmed game result. No gameplay request is repeated')
                end
            elseif at>=(r.nextRunAt or 0)then
                r.cycleStart=r.cycleStart or r.confirmed
                local yes,plan,observation=measured(kind,native.Plan,kind,c,at)
                if not yes then finish(kind,r,at,'error','Game data could not be read: '..tostring(plan))
                elseif not plan then r.before=observation;finish(kind,r,at,r.confirmed>(r.cycleStart or r.confirmed)and 'success'or 'nothing_available')
                elseif plan.error then r.before=observation;finish(kind,r,at,'waiting',plan.error)
                elseif owned()then
                    local sender=get(rawget(_G,'SFSNetwork'),'SendMessage')
                    if type(sender)~='function'or plan.route==nil then finish(kind,r,at,'error','The native game route is unavailable')
                    else
                        plan.sentAt=at;plan.before=observation;pending[kind]=plan
                        -- Record before invoking. An exception can follow a successful wire send.
                        if plan.readOnly then r.readRequests=(r.readRequests or 0)+1 else r.submitted=r.submitted+1 end
                        r.state='awaiting_confirmation';r.before=observation;r.error=nil
                        local sent,err=pcall(sender,plan.route,table.unpack(plan.args or {}))
                        if not sent then plan.sendError=tostring(err)end
                    end
                end
            end
        end
    end
    recalc()
end
function M.CleanupAfterLeaseLoss()
    if native.Stop then for kind in pairs(configs)do native.Stop(kind)end end
    for kind,job in pairs(pending)do if not job.readOnly then uncertain[kind]=job end end
    configs={};pending={};force={};stopped={};active=false;owner=nil
    for _,r in pairs(services)do r.enabled=false;r.state='disconnected';r.nextRunAt=nil end
end
function M.Register(feature,overview,adapter)
    bridge=overview;native=adapter;array=feature.Array
    feature.RegisterHandler('feature018_automation_configure',M.Configure)
    feature.RegisterHandler('feature018_automation_run',M.Run)
    feature.RegisterHandler('feature018_automation_stop',M.Stop)
    feature.RegisterHandler('feature018_automation_status',M.Status)
    feature.RegisterHandler('feature018_automation_observe',M.Observe)
end
return M
