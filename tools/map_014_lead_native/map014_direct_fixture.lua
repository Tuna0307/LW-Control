local _df_cmd = _trace_root .. [[\live-resource\map014-direct-fixture.txt]]
local _df_res = _trace_root .. [[\live-resource\map014-direct-fixture-result.json]]
local _df = nil
local function _df_now() local ok, t = pcall(function() return CS.UnityEngine.Time.realtimeSinceStartup end); return ok and tonumber(t) or 0 end
local function _df_flags() local b = CS.System.Reflection.BindingFlags; return b.Instance + b.Public + b.NonPublic end
local function _df_field(o, n)
    for _, f in ipairs({ 52, "enum" }) do
        local ok, v = pcall(function() return o:GetType():GetField(n, f == "enum" and _df_flags() or f):GetValue(o) end)
        if ok and v ~= nil then return v end
    end
end
local function _df_count(pm, areas)
    local infos = _df_field(pm, "_pointInfos")
    local total = infos and tonumber(_trace_safe(infos, "Count")) or nil
    if total == nil then return -1 end
    local n = 0
    for i = 0, total - 1 do
        local item = _trace_safe(infos, i)
        local v = item and (_trace_safe(item, "Value") or item)
        local p = tonumber(tostring(v and (_trace_safe(v, "pointIndex") or _trace_safe(v, "mainIndex")) or ""))
        if p then
            local x, y = (p - 1) % 1000, math.floor((p - 1) / 1000)
            if areas[math.floor(y / 10) * 100 + math.floor(x / 10)] then n = n + 1 end
        end
    end
    return n
end
local function _df_write(out) local f = io.open(_df_res, "wb"); if f then f:write(_trace_json(out)); f:close() end end
local function _df_tick()
    local now = _df_now()
    if _df then
        if now >= _df.next then
            _df.samples[#_df.samples + 1] = { t = now - _df.t0, n = _df_count(_df.pm, _df.areas) }
            _df.next = now + 0.1
        end
        if now - _df.t0 >= 3 then _df_write(_df.out); _df = nil end
        return
    end
    local req = _trace_kv(_df_cmd)
    if not req then return end
    pcall(os.remove, _df_cmd)
    local ctl = _trace_kv(_trace_root .. [[\overview-bridge\control.txt]])
    if not ctl or req.profileId ~= ctl.profileId or req.launchSessionId ~= ctl.sessionId or
        req.challenge ~= ctl.challenge or req.gamePid ~= ctl.gamePid then return end
    local out = { requestId = req.requestId, samples = {} }
    local cs = rawget(_G, "CS")
    local world = _trace_safe(_trace_safe(cs, "SceneManager"), "World")
    local pm = world and _trace_safe(world, "PointManager")
    local cur, add = _df_field(pm, "_curViewIndex"), _df_field(pm, "_addViewIndex")
    out.svLod, out.blockSize = tonumber(tostring(_df_field(pm, "svLod"))), tonumber(tostring(_df_field(pm, "_lwAoiBlockSize")))
    local areas, list = {}, {}
    for v in string.gmatch(req.indices or "", "%d+") do areas[tonumber(v)] = true; list[#list + 1] = tonumber(v) end
    out.areaCount = #list
    if pm == nil or cur == nil or add == nil then out.error = "no_manager"; _df_write(out); return end
    out.before = _df_count(pm, areas)
    local i32 = cs.System.Int32
    local ok, err = pcall(function()
        add:Clear()
        for _, v in ipairs(list) do add:Add(i32.Parse(tostring(v))); cur:Add(i32.Parse(tostring(v))) end
            local r = CS.WorldGetBlockMessage.Request()
            r.bigMap, r.x, r.y, r.serverId, r.worldId = 0, tonumber(req.x), tonumber(req.y), tonumber(req.server), 0
            r.type, r.lod, r.blockSize, r.firstTime = tonumber(req.type or 0), 0, out.blockSize or 10, req.first == "true"
            r.leftBottom, r.rightTop, r.battleFieldFirst = tonumber(req.lb), tonumber(req.rt), false
            local idx = cs.System.Array.CreateInstance(typeof(i32), #list)
            for k, v in ipairs(list) do idx:SetValue(i32.Parse(tostring(v)), k - 1) end
            r.index = idx
            -- Decoded IL: BaseMessage.Send is `params object[]`; pass the Request itself.
            CS.WorldGetBlockMessage.Instance:Send(r)
    end)
    out.sent = ok
    if not ok then out.error = tostring(err); _df_write(out); return end
    _df = { pm = pm, areas = areas, out = out, samples = out.samples, t0 = _df_now(), next = 0 }
end
local _df_prior = probe.Pump
probe.Pump = function(...)
    local values = { pcall(_df_prior, ...) }
    local ok = table.remove(values, 1)
    pcall(_df_tick)
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
