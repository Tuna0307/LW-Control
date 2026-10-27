-- MAP-014 HARNESS-ONLY read-only terrain probe. NEVER shipped (appended to the private wrapper-trace copy
-- only when LWBRIDGE_MAP014_TERRAIN_FIXTURE is set). No network, camera or UI mutation. Identity-checked.
local _tf_cmd = _trace_root .. [[\live-resource\map014-terrain-fixture.txt]]
local _tf_result = _trace_root .. [[\live-resource\map014-terrain-fixture-result.json]]
local function _tf_get(object, key)
    if object == nil then return nil end
    local ok, value = pcall(function() return object[key] end)
    return ok and value or nil
end
local function _tf_call(object, method, ...)
    local fn = _tf_get(object, method)
    if type(fn) ~= "function" then return nil end
    local args = { ... }
    local ok, value = pcall(function() return fn(object, (table.unpack or unpack)(args)) end)
    if ok then return value end
    ok, value = pcall(function() return fn((table.unpack or unpack)(args)) end)
    if ok then return value end
    return nil
end
local function _tf_fn(object, key, ...)
    local fn = _tf_get(object, key)
    if type(fn) ~= "function" then return nil end
    local ok, value = pcall(fn, ...)
    return ok and value or nil
end
local function _tf_tick()
    local req = _trace_kv(_tf_cmd)
    if not req then return end
    pcall(os.remove, _tf_cmd)
    local ctl = _trace_kv(_trace_root .. [[\overview-bridge\control.txt]])
    if not ctl or req.profileId ~= ctl.profileId or req.launchSessionId ~= ctl.sessionId or
        req.challenge ~= ctl.challenge or req.gamePid ~= ctl.gamePid then return end
    local server = tonumber(req.serverId)
    local scene_utils = rawget(_G, "SceneUtils")
    local season_util = rawget(_G, "SeasonUtil")
    local data_center = rawget(_G, "DataCenter")
    local lua_entry = rawget(_G, "LuaEntry")
    local cs = rawget(_G, "CS")
    local manager = cs and _trace_safe(cs, "SceneManager")
    local world = manager and _trace_safe(manager, "World")
    local zone = world and _trace_safe(world, "WorldMapZoneManager")
    local temperature = data_center and _tf_get(data_center, "TemperatureManager")
    local player = lua_entry and _tf_get(lua_entry, "Player")
    local out = { requestId = req.requestId, serverId = server, tiles = {} }
    out.snowMode = season_util and _tf_fn(season_util, "IsCurWorldInSeasonSnowModeWithoutGroup") or nil
    out.playerBlackNoArg = player and _tf_call(player, "IsInBlackRange") or nil
    out.hasSceneUtils = scene_utils ~= nil
    out.hasZone = zone ~= nil
    out.hasTemperature = temperature ~= nil
    local count = 0
    for token in string.gmatch(req.tiles or "", "[^,]+") do
        local x, y = string.match(token, "^(%d+):(%d+)$")
        x, y = tonumber(x), tonumber(y)
        if x and y and count < 400 then
            count = count + 1
            local idx = x + y * 1000 + 1
            out.tiles[#out.tiles + 1] = {
                x = x, y = y, idx = idx,
                blackRange = scene_utils and _tf_fn(scene_utils, "IsInBlackRange", idx, server) or nil,
                blackLand = scene_utils and _tf_fn(scene_utils, "IsInBlackLand", idx, server) or nil,
                yellowLand = scene_utils and _tf_fn(scene_utils, "IsInYellowLand", idx, server) or nil,
                zoneBlack = zone and _tf_call(zone, "IsInBlackArea", idx) or nil,
                temperature = temperature and _tf_call(temperature, "GetTemperatureByIndex", idx) or nil,
            }
        end
    end
    local file = io.open(_tf_result, "wb")
    if file then file:write(_trace_json(out)); file:close() end
end
local _tf_prior_probe_pump = probe.Pump
probe.Pump = function(...)
    local values = { pcall(_tf_prior_probe_pump, ...) }
    local ok = table.remove(values, 1)
    pcall(_tf_tick)
    if not ok then error(values[1]) end
    return (table.unpack or unpack)(values)
end
