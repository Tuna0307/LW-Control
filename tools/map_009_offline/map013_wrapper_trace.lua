-- Opt-in owner-only passive EventComponent listener. Inserted in LuaEntry
-- wrapper AFTER original unchanged LiveResourceProbe module has loaded.
-- No original subscriber or receiver is replaced. Raw output stays ignored.
local _trace_root = os.getenv("LWBRIDGE_REBUILD_DATA_ROOT") or ""
local _trace_cmd = _trace_root .. [[\live-resource\map013-native-trace.txt]]
local _trace_result = _trace_root .. [[\live-resource\map013-native-trace-result.json]]
-- Wall seconds, not CPU ticks: a paused/low-CPU scene must not leave a
-- temporary listener armed indefinitely.
local _trace_time = os.time
local _trace_active = nil
local _trace_bridge_pump = {calls=0, failures=0, lastError=nil, lastResult=nil}
local function _trace_kv(path)
    local f = io.open(path, "rb")
    if f == nil then return nil end
    local data = f:read("*a"); f:close()
    if #data > 4096 then return nil end
    local result = {}
    for k, v in string.gmatch(data, "([^=\r\n]+)=([^\r\n]*)") do
        if result[k] ~= nil then return nil end
        result[k] = v
    end
    return result
end
local function _trace_safe(object, key)
    if object == nil then return nil end
    local ok, value = pcall(function() return object[key] end)
    return ok and value or nil
end
local function _trace_shape(value)
    if value == nil then return {present=false} end
    local out = {present=true, luaType=type(value)}
    local ok, typ = pcall(function() return value:GetType() end)
    if ok and typ ~= nil then out.nativeType = tostring(_trace_safe(typ, "FullName") or typ) end
    for _,name in ipairs({"Id","id","ServerId","serverId","worldId","WorldId"}) do
        local v = _trace_safe(value,name)
        if type(v) == "number" or type(v) == "string" or type(v) == "boolean" then out[name]=v end
    end
    local ok_dump, dump = pcall(function() return value:GetDump() end)
    if ok_dump and type(dump)=="string" then
        out.dumpPrefix = string.sub(dump,1,1000)
        out.dumpLength = #dump
    end
    return out
end
local function _trace_json(v)
    if v == nil then return "null" end
    if type(v) == "boolean" then return v and "true" or "false" end
    if type(v) == "number" then return tostring(v) end
    if type(v) == "string" then
        return '"' .. (string.gsub(v,"[%z\1-\31\\\"]",function(ch)
            if ch == "\\" then return "\\\\" end
            if ch == '"' then return '\\"' end
            return string.format("\\u%04x",string.byte(ch))
        end)) .. '"'
    end
    if type(v)~="table" then return _trace_json(tostring(v)) end
    local keys,n={},0
    for k in pairs(v) do n=n+1;keys[#keys+1]=k end
    local array=true
    for k in pairs(v) do if type(k)~="number" or k<1 or k>n or k~=math.floor(k) then array=false end end
    if array then
        local parts={}
        for i=1,n do parts[#parts+1]=_trace_json(v[i]) end
        return "["..table.concat(parts,",").."]"
    end
    local parts={}
    for _,k in ipairs(keys) do parts[#parts+1]=_trace_json(tostring(k))..":".._trace_json(v[k]) end
    return "{"..table.concat(parts,",").."}"
end
local function _trace_view()
    local cs=rawget(_G,"CS")
    local sm=cs and _trace_safe(cs,"SceneManager")
    local world=sm and _trace_safe(sm,"World")
    local manager=world and _trace_safe(world,"PointManager")
    local tile=world and _trace_safe(world,"CurTilePosClamped")
    local result={ worldPresent=world~=nil, pointManagerPresent=manager~=nil,
        tileX=tile and tonumber(_trace_safe(tile,"x") or _trace_safe(tile,"X")),
        tileY=tile and tonumber(_trace_safe(tile,"y") or _trace_safe(tile,"Y")) }
    if world~=nil then
        local ok, typ=pcall(function() return world:GetType() end)
        if ok and typ~=nil then result.worldType=tostring(_trace_safe(typ,"Name") or typ) end
    end
    local ok_world,is_world=pcall(function() return sm:IsInWorld() end)
    result.sceneIsInWorld=ok_world and is_world or false
    local scene_utils=rawget(_G,"SceneUtils")
    result.globalSceneUtilsPresent=scene_utils~=nil
    result.globalChangeToWorldPresent=type(_trace_safe(scene_utils,"ChangeToWorld"))=="function"
    if manager~=nil then
        local rt=manager:GetType()
        local b=cs and cs.System and cs.System.Reflection and cs.System.Reflection.BindingFlags
        local flags=52
        if b~=nil then
            local ok,combined=pcall(function() return b.Instance+b.NonPublic+b.Public end)
            if ok then flags=combined end
        end
        for _,name in ipairs({"_curViewIndex","_addViewIndex","_msgViewIndex","_pointInfos","svLod","_lwAoiBlockSize"}) do
            local ok,value=pcall(function()
                local f=rt:GetField(name,flags)
                return f and f:GetValue(manager)
            end)
            if ok and value~=nil then
                local count=_trace_safe(value,"Count")
                result[name]=tonumber(count) or (tonumber(value) or tostring(value))
            end
        end
    end
    return result
end
local function _trace_finish(status)
    local tr=_trace_active
    if not tr then return end
    for _,sub in ipairs(tr.listeners) do
        local ok,err=pcall(function() sub.event:Unsubscribe(sub.id,sub.cb) end)
        if not ok then tr.errors[#tr.errors+1]="Unsubscribe "..sub.id..":"..tostring(err) end
    end
    tr.listeners={}
    local obj={schemaVersion=1, probeVersion=tr.request.probeVersion,
        requestId=tr.request.requestId, profileId=tr.request.profileId,
        launchSessionId=tr.request.launchSessionId, gamePid=tonumber(tr.request.gamePid),
        challenge=tr.request.challenge,
        capturedAt=os.date("!%Y-%m-%dT%H:%M:%SZ"),
        state=status, events=tr.events, registrations=tr.registrations,
        samples=tr.samples, errors=tr.errors,
        eventType=tr.eventType, entryGlobal=tr.entryGlobal, entryStatic=tr.entryStatic,
        bridgePump=_trace_bridge_pump,
        note="Passive additive listeners only; callbacks never replace native handlers"}
    local f=io.open(_trace_result,"wb")
    if f then f:write(_trace_json(obj));f:close() end
    _trace_active=nil
end
local function _trace_tick()
    local now=_trace_time()
    if not _trace_active then
        local req=_trace_kv(_trace_cmd)
        if not req then return end
        pcall(os.remove,_trace_cmd)
        local ctl=_trace_kv(_trace_root..[[\overview-bridge\control.txt]])
        if not ctl or req.profileId~=ctl.profileId or req.launchSessionId~=ctl.sessionId or
            req.challenge~=ctl.challenge or req.gamePid~=ctl.gamePid then return end
        local tr={request=req,started=now,ends=now+18,events={},registrations={},
            listeners={},samples={},errors={}}
        _trace_active=tr
        -- WorldPointManager.Init invokes the static GameEntry.get_Event;
        -- the Lua global GameEntry need not expose that static accessor.
        local entry=rawget(_G,"GameEntry")
        local cs=rawget(_G,"CS")
        local static_entry=cs and _trace_safe(cs,"GameEntry")
        local event=static_entry and _trace_safe(static_entry,"Event")
        if event==nil then event=entry and _trace_safe(entry,"Event") end
        tr.entryGlobal=_trace_shape(entry)
        tr.entryStatic=_trace_shape(static_entry)
        tr.eventType=_trace_shape(event)
        if event == nil then tr.errors[#tr.errors+1]="CS.GameEntry.Event and global GameEntry.Event unavailable"
        else
            for _,id in ipairs({1433,661,2821011,2821014}) do
                local cb=function(args)
                    if #tr.events<60 then tr.events[#tr.events+1]={
                        elapsed=_trace_time()-tr.started, subscribedId=id,
                        payload=_trace_shape(args),
                        userData=_trace_shape(_trace_safe(args,"UserData"))}
                    end
                end
                local ok,err=pcall(function() event:Subscribe(id,cb) end)
                tr.registrations[#tr.registrations+1]={id=id,ok=ok,error=not ok and tostring(err) or nil}
                if ok then tr.listeners[#tr.listeners+1]={event=event,id=id,cb=cb} end
            end
        end
    else
        local tr=_trace_active
        if (#tr.samples==0 or tr.lastSampleAt~=now) and #tr.samples<24 then
            tr.lastSampleAt=now
            local root=_trace_root..[[\overview-bridge\]]
            local lease=_trace_kv(root.."lease.txt")
            local control=_trace_kv(root.."control.txt")
            local ready=_trace_kv(root.."world-ready.txt")
            local before=io.open(root.."world-ready-result.json","rb")
            local resultPresent=before~=nil
            if before then before:close() end
            tr.samples[#tr.samples+1]={
                elapsed=now-tr.started,view=_trace_view(),
                leaseAgeSeconds=lease and (now-tonumber(lease.updatedAt or "0")),
                controlOwned=control and control.sessionId==tr.request.launchSessionId,
                queuedWorldRequest=ready~=nil,
                queuedWorldRequestId=ready and ready.requestId or nil,
                worldReplyPresent=resultPresent,
                bridgeCalls=_trace_bridge_pump.calls,
                bridgeFailures=_trace_bridge_pump.failures,
                bridgeLastError=_trace_bridge_pump.lastError}
        end
        if now>=tr.ends then _trace_finish("observed") end
    end
end
-- A read-only observation of the existing bridge's synchronous execution.
-- Always relay the original return/error; never skip or reorder its callbacks.
local _trace_original_bridge_pump=bridge.Pump
bridge.Pump=function(...)
    local values={pcall(_trace_original_bridge_pump,...)}
    local ok=table.remove(values,1)
    _trace_bridge_pump.calls=_trace_bridge_pump.calls+1
    _trace_bridge_pump.lastResult=ok and tostring(values[1]) or nil
    if not ok then
        _trace_bridge_pump.failures=_trace_bridge_pump.failures+1
        _trace_bridge_pump.lastError=string.sub(tostring(values[1]),1,400)
        error(values[1])
    end
    return (table.unpack or unpack)(values)
end
local _trace_prior_probe_pump=probe.Pump
probe.Pump=function(...)
    local values={pcall(_trace_prior_probe_pump,...)}
    local ok=table.remove(values,1)
    local trace_ok,trace_err=pcall(_trace_tick)
    if not trace_ok and _trace_active then
        _trace_active.errors[#_trace_active.errors+1]=tostring(trace_err)
        _trace_finish("observer_error")
    end
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
