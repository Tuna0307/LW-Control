-- MAP-014 HARNESS-ONLY camera zoom fixture. NEVER shipped: run_owned.py appends it to the
-- task-private copy of the wrapper trace only when --zoom-fixture is requested. It lets a
-- test game start a scan from the same zoomed-in camera an owner may use (reproduction) and
-- restore the exact prior zoom afterwards. Requests must match the owned session identity.
local _zf_cmd = _trace_root .. [[\live-resource\map014-zoom-fixture.txt]]
local _zf_result = _trace_root .. [[\live-resource\map014-zoom-fixture-result.json]]
local _zf_original = nil
local function _zf_camera()
    local cs = rawget(_G, "CS")
    local manager = cs and _trace_safe(cs, "SceneManager")
    local world = manager and _trace_safe(manager, "World")
    return world and _trace_safe(world, "Camera") or nil
end
local function _zf_tick()
    local req = _trace_kv(_zf_cmd)
    if not req then return end
    pcall(os.remove, _zf_cmd)
    local ctl = _trace_kv(_trace_root .. [[\overview-bridge\control.txt]])
    if not ctl or req.profileId ~= ctl.profileId or req.launchSessionId ~= ctl.sessionId or
        req.challenge ~= ctl.challenge or req.gamePid ~= ctl.gamePid then return end
    local out = { requestId = req.requestId, mode = req.mode }
    local camera = _zf_camera()
    if camera == nil then
        out.ok = false
        out.error = "camera_unavailable"
    else
        out.before = tonumber(_trace_safe(camera, "Zoom"))
        local target = nil
        if req.mode == "set" then
            target = tonumber(req.zoom)
            if target == nil or target < 30 or target > 200 then
                out.ok = false
                out.error = "zoom_out_of_range"
            elseif _zf_original == nil then
                _zf_original = out.before
            end
        elseif req.mode == "restore" then
            target = _zf_original
            if target == nil then
                out.ok = false
                out.error = "nothing_to_restore"
            end
        else
            out.ok = false
            out.error = "unknown_mode"
        end
        if out.ok == nil then
            local set_ok = pcall(function() camera.Zoom = target end)
            local refresh_ok = pcall(function() camera:RefreshCameraAnchor() end)
            out.after = tonumber(_trace_safe(camera, "Zoom"))
            out.ok = set_ok and refresh_ok and out.after ~= nil and math.abs(out.after - target) < 0.01
            if req.mode == "restore" and out.ok then _zf_original = nil end
        end
    end
    local file = io.open(_zf_result, "wb")
    if file then file:write(_trace_json(out)); file:close() end
end
local _zf_prior_probe_pump = probe.Pump
probe.Pump = function(...)
    local values = { pcall(_zf_prior_probe_pump, ...) }
    local ok = table.remove(values, 1)
    pcall(_zf_tick)
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
