-- Harness-only passive frame observation, appended to the isolated LuaEntry
-- wrapper. No network/game action, Time property assignment or global input.
-- Cumulative counters permit before/after subtraction. Frames are unique;
-- multiple probe.Pump calls in one frame never multiply the sample count.
local _m19_frame = {schemaVersion=1, nativeObservation=true, samples=0,
    deltaSecondsSum=0, over33ms=0, over100ms=0, maxDeltaSeconds=0,
    source="UnityEngine.Time.frameCount/unscaledDeltaTime; observed probe frames only"}
local _m19_last_frame, _m19_last_write = nil, -100
local function _m19_observe_frame()
    local frame = tonumber(CS.UnityEngine.Time.frameCount)
    if frame == nil or frame == _m19_last_frame then return end
    _m19_last_frame = frame
    local elapsed = tonumber(CS.UnityEngine.Time.realtimeSinceStartup)
    local delta = tonumber(CS.UnityEngine.Time.unscaledDeltaTime)
    if elapsed == nil or delta == nil or delta < 0 then return end
    _m19_frame.samples = _m19_frame.samples + 1
    _m19_frame.deltaSecondsSum = _m19_frame.deltaSecondsSum + delta
    if delta > 0.033333333 then _m19_frame.over33ms = _m19_frame.over33ms + 1 end
    if delta > 0.1 then _m19_frame.over100ms = _m19_frame.over100ms + 1 end
    _m19_frame.maxDeltaSeconds = math.max(_m19_frame.maxDeltaSeconds, delta)
    _m19_frame.frameCount, _m19_frame.elapsedSeconds = frame, elapsed
    if elapsed - _m19_last_write < 1 then return end
    -- Scope identity comes from this isolated owner's existing control file.
    local control = _trace_kv(_trace_root .. [[\overview-bridge\control.txt]])
    if not control then return end
    for _, key in ipairs({"profileId", "sessionId", "challenge", "gamePid"}) do
        if control[key] == nil then return end
        _m19_frame[key] = control[key]
    end
    local file = io.open(_trace_root .. [[\live-resource\map019-frame-telemetry.json]], "wb")
    if file == nil then return end
    file:write(_trace_json(_m19_frame)); file:close()
    _m19_last_write = elapsed
end
local _m19_original_pump = probe.Pump
probe.Pump = function(...)
    local values = {pcall(_m19_original_pump, ...)}
    local ok = table.remove(values, 1)
    pcall(_m19_observe_frame)
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
