-- MAP-014 HARNESS-ONLY camera-free full-world request experiment (decoded world.get.block path).
local _fs_cmd = _trace_root .. [[\live-resource\map014-fullscan-fixture.txt]]
local _fs_res = _trace_root .. [[\live-resource\map014-fullscan-fixture-result.json]]
local _fs = nil
local function _fs_now() local ok, t = pcall(function() return CS.UnityEngine.Time.realtimeSinceStartup end); return ok and tonumber(t) or 0 end
local function _fs_field(o, n)
    local ok, v = pcall(function() return o:GetType():GetField(n, 52):GetValue(o) end)
    return ok and v or nil
end
local function _fs_census(pm)
    local infos = _fs_field(pm, "_pointInfos")
    local total = infos and tonumber(_trace_safe(infos, "Count")) or -1
    local types = {}
    for i = 0, total - 1 do
        local item = _trace_safe(infos, i)
        local v = item and (_trace_safe(item, "Value") or item)
        local t = tostring(v and _trace_safe(v, "pointType") or "?")
        types[t] = (types[t] or 0) + 1
    end
    return total, types
end
local function _fs_write(out) local f = io.open(_fs_res, "wb"); if f then f:write(_trace_json(out)); f:close() end end
local function _fs_tick()
    local now = _fs_now()
    local s = _fs
    if s then
        local sent = 0
        local ready = true
        if s.gate == "reply" and s.next > 1 then
            ready = _fs_field(s.pm, "isRecvViewPoints") == true or now - s.lastSend >= 1.5
        elseif s.interval > 0 then ready = now - s.lastSend >= s.interval end
        if ready and s.next <= #s.chunks and s.gate == "reply" then
            pcall(function() s.pm:GetType():GetField("isRecvViewPoints", 52):SetValue(s.pm, CS.System.Boolean.Parse("False")) end)
        end
        while ready and s.next <= #s.chunks and sent < s.perTick do
            local c = s.chunks[s.next]
            local r = CS.WorldGetBlockMessage.Request()
            r.bigMap, r.x, r.y, r.serverId, r.worldId, r.type, r.lod = 0, c.x, c.y, s.server, 0, 0, 0
            r.blockSize, r.firstTime, r.leftBottom, r.rightTop, r.battleFieldFirst = 10, false, c.lb, c.rt, false
            local idx = CS.System.Array.CreateInstance(typeof(CS.System.Int32), #c.idx)
            for k, v in ipairs(c.idx) do idx:SetValue(s.i32.Parse(tostring(v)), k - 1); s.cur:Add(s.i32.Parse(tostring(v))) end
            r.index = idx
            CS.WorldGetBlockMessage.Instance:Send(r)
            s.next, sent, s.lastSend = s.next + 1, sent + 1, now
            if s.next > #s.chunks then s.out.lastSendAt = now - s.t0 end
        end
        if now >= s.sample then
            local infos = _fs_field(s.pm, "_pointInfos")
            local n = infos and tonumber(_trace_safe(infos, "Count")) or -1
            for i = 0, n - 1 do
                local item = _trace_safe(infos, i)
                local v = item and (_trace_safe(item, "Value") or item)
                local p = v and _trace_safe(v, "pointIndex")
                if p and not s.seen[p] then s.seen[p] = tostring(_trace_safe(v, "pointType")); s.seenN = s.seenN + 1 end
            end
            if now >= s.mSample then
                s.mSample = now + s.mEvery
                pcall(function()
                    local e = CS.SceneManager.World.MarchDataManager:GetAllMarchesByCS():GetEnumerator()
                    while e:MoveNext() do
                        local m = e.Current.Value or e.Current
                        local u = tostring(m.uuid)
                        if not s.marches[u] then
                            s.marches[u] = true; s.out.marchSeen = (s.out.marchSeen or 0) + 1
                            if m.train ~= nil then s.out.trainSeen = (s.out.trainSeen or 0) + 1 end
                            if (tonumber(m.monsterId) or 0) > 0 then s.out.monsterSeen = (s.out.monsterSeen or 0) + 1 end
                        end
                    end
                end)
            end
            s.out.timeline[#s.out.timeline + 1] = { t = now - s.t0, n = n, seen = s.seenN }
            s.sample = now + 0.2
        end
        if s.out.lastSendAt and now - s.t0 >= s.out.lastSendAt + s.settle then
            s.out.total, s.out.types = _fs_census(s.pm)
            s.out.seenTotal, s.out.seenTypes = s.seenN, {}
            for _, t in pairs(s.seen) do s.out.seenTypes[t] = (s.out.seenTypes[t] or 0) + 1 end
            s.out.doneAt = now - s.t0
            for _, v in ipairs(s.added) do pcall(function() s.cur:Remove(s.i32.Parse(tostring(v))) end) end
            _fs_write(s.out); _fs = nil
        end
        return
    end
    local req = _trace_kv(_fs_cmd)
    if not req then return end
    pcall(os.remove, _fs_cmd)
    local ctl = _trace_kv(_trace_root .. [[\overview-bridge\control.txt]])
    if not ctl or req.profileId ~= ctl.profileId or req.launchSessionId ~= ctl.sessionId or
        req.challenge ~= ctl.challenge or req.gamePid ~= ctl.gamePid then return end
    local out = { requestId = req.requestId, timeline = {} }
    local ok, err = pcall(function()
        local pm = CS.SceneManager.World.PointManager
        local cur = _fs_field(pm, "_curViewIndex")
        local before = {}
        local n = tonumber(_trace_safe(cur, "Count")) or 0
        local arr = CS.System.Array.CreateInstance(typeof(CS.System.Int32), n)
        cur:CopyTo(arr)
        for i = 0, n - 1 do before[tonumber(arr[i])] = true end
        local rows, cols = tonumber(req.rows or 10), tonumber(req.cols or 16)
        local chunks, added = {}, {}
        local linear = tonumber(req.linear or 0)
        for k0 = 0, linear > 0 and 9999 or -1, linear > 0 and linear or 1 do
            local k1 = math.min(9999, k0 + linear - 1)
            local r0, r1 = math.floor(k0 / 100), math.floor(k1 / 100)
            local c = { idx = {}, x = 500, y = math.floor((r0 + r1 + 1) * 5),
                lb = r0 * 10 * 1000 + 1, rt = 999 + math.min(999, (r1 + 1) * 10) * 1000 + 1 }
            for i = k0, k1 do c.idx[#c.idx + 1] = i; if not before[i] then added[#added + 1] = i end end
            chunks[#chunks + 1] = c
        end
        for r0 = 0, linear > 0 and -1 or 99, rows do
            for c0 = 0, 99, cols do
                local r1, c1 = math.min(99, r0 + rows - 1), math.min(99, c0 + cols - 1)
                local c = { idx = {}, x = math.floor((c0 + c1 + 1) * 5), y = math.floor((r0 + r1 + 1) * 5),
                    lb = c0 * 10 + r0 * 10 * 1000 + 1, rt = math.min(999, (c1 + 1) * 10) + math.min(999, (r1 + 1) * 10) * 1000 + 1 }
                for rr = r0, r1 do for cc = c0, c1 do
                    local i = rr * 100 + cc
                    c.idx[#c.idx + 1] = i
                    if not before[i] then added[#added + 1] = i end
                end end
                chunks[#chunks + 1] = c
            end
        end
        out.chunks, out.beforeTotal = #chunks, (select(1, _fs_census(pm)))
        _fs = { pm = pm, cur = cur, i32 = CS.System.Int32, chunks = chunks, added = added, next = 1,
            perTick = tonumber(req.perTick or 1), settle = tonumber(req.settle or 4), server = tonumber(req.server),
            gate = req.gate or "none", interval = tonumber(req.interval or 0), lastSend = -1, seen = {}, seenN = 0, marches = {}, mSample = 0, mEvery = tonumber(req.mEvery or 1),
            out = out, t0 = _fs_now(), sample = 0 }
    end)
    if not ok then out.error = tostring(err); _fs_write(out) end
end
local _fs_prior = probe.Pump
probe.Pump = function(...)
    local values = { pcall(_fs_prior, ...) }
    local ok = table.remove(values, 1)
    pcall(_fs_tick)
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
