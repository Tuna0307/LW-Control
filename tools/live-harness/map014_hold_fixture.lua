local _hf_cmd = _trace_root .. [[\live-resource\map014-hold-fixture.txt]]
local _hf_res = _trace_root .. [[\live-resource\map014-hold-fixture-result.json]]
local _hf = nil
local function _hf_now() local ok, t = pcall(function() return CS.UnityEngine.Time.realtimeSinceStartup end); return ok and tonumber(t) or os.clock() end
local function _hf_field(o, n)
    local ok, v = pcall(function() return o:GetType():GetField(n, 52):GetValue(o) end)
    return ok and v or nil
end
local function _hf_count(o) return o and tonumber(_trace_safe(o, "Count")) or -1 end
local function _hf_write(out) local f = io.open(_hf_res, "wb"); if f then f:write(_trace_json(out)); f:close() end end
local function _hf_restore(h)
    pcall(function() h.cam.Zoom = h.zoom end)
    pcall(function() h.cam:SetFOV(h.fov) end)
    pcall(function() h.unity.aspect = h.aspect end)
    pcall(function() h.touch:SetCameraPos(h.pos) end)
    pcall(function() h.cam:RefreshCameraAnchor() end)
    pcall(function() h.pm:UpdateViewRequest(true) end)
end
local function _hf_tick()
    local now = _hf_now()
    if _hf then
        local h = _hf
        if now >= h.next then
            local cur = _hf_field(h.pm, "_curViewIndex")
            local s = { t = now - h.t0, cur = _hf_count(cur), points = _hf_count(_hf_field(h.pm, "_pointInfos")) }
            if cur then
                local mnr, mxr, mnc, mxc = 999, -1, 999, -1
                local arr = CS.System.Array.CreateInstance(typeof(CS.System.Int32), s.cur)
                cur:CopyTo(arr)
                for i = 0, s.cur - 1 do
                    local v = tonumber(arr[i])
                    if v then local r, c = math.floor(v / 100), v % 100
                        if r < mnr then mnr = r end; if r > mxr then mxr = r end; if c < mnc then mnc = c end; if c > mxc then mxc = c end end
                end
                s.rows, s.cols = { mnr, mxr }, { mnc, mxc }
            end
            h.out.samples[#h.out.samples + 1] = s
            h.next = now + 0.1
        end
        if now - h.t0 >= h.hold then _hf_restore(h); _hf_write(h.out); _hf = nil end
        return
    end
    local req = _trace_kv(_hf_cmd)
    if not req then return end
    pcall(os.remove, _hf_cmd)
    local ctl = _trace_kv(_trace_root .. [[\overview-bridge\control.txt]])
    if not ctl or req.profileId ~= ctl.profileId or req.launchSessionId ~= ctl.sessionId or
        req.challenge ~= ctl.challenge or req.gamePid ~= ctl.gamePid then return end
    local out = { requestId = req.requestId, samples = {} }
    local ok, err = pcall(function()
        local world = CS.SceneManager.World
        local cam = world.Camera
        local touch = cam.touchCamera or _hf_field(cam, "touchCamera")
        local unity = cam.__camera or _hf_field(cam, "camera")
        local pm = world.PointManager
        local pos = touch:GetCameraPos()
        local tile = world.CurTilePosClamped
        local h = { cam = cam, touch = touch, unity = unity, pm = pm, pos = pos, zoom = cam.Zoom, fov = unity.fieldOfView,
            aspect = unity.aspect, out = out, t0 = _hf_now(), next = 0, hold = tonumber(req.hold) or 3 }
        out.from = { x = tile.x, y = tile.y, zoom = h.zoom, fov = h.fov }
        local target = CS.UnityEngine.Vector3(pos.x + (tonumber(req.x) - tile.x) * 2, pos.y, pos.z + (tonumber(req.y) - tile.y) * 2)
        touch:SetCameraPos(target)
        cam.Zoom = tonumber(req.zoom)
        cam:SetFOV(tonumber(req.fov))
        unity.aspect = tonumber(req.aspect or 4)
        cam:RefreshCameraAnchor()
        pm:UpdateViewRequest(true)
        _hf = h
    end)
    out.started = ok
    if not ok then out.error = tostring(err); _hf_write(out) end
end
local _hf_prior = probe.Pump
probe.Pump = function(...)
    local values = { pcall(_hf_prior, ...) }
    local ok = table.remove(values, 1)
    pcall(_hf_tick)
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
