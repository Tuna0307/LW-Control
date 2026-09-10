-- Bounded LWBridge rebuild live-resource acquisition probe.
--
-- Game-facing members in this file are restricted to the current-client
-- WorldPointManager/WorldScene surface already exercised by LWB-R7-001 and the
-- build-1078 R6 findings.  The command/result files and time bounds are rebuild
-- IMPLEMENTATION POLICY and are not claimed as original LWBridge protocol.

local M = { VERSION = "lwbridge-live-resource-probe-1" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWBridgeRebuild\live-resource]]
local heartbeat_path = root .. [[\heartbeat.json]]
local command_path = root .. [[\command.txt]]
local result_path = root .. [[\result.json]]
local phase = "idle"
local active_request_id = nil
local active_launch_session_id = nil
local active_profile_id = nil
local active_game_pid = nil
local request_started_at = nil
local response_started_at = nil
local transition_requested = false
local original_manager_flag = nil
local original_world_flag = nil
local flags_touched = false
local acquisition_ordinal = 0
local registration_method = nil
local timer_handle = nil
local update_callback = nil

-- IMPLEMENTATION POLICY: this guards a bounded current-view response wait.  It
-- is not an original LWBridge timeout.
local RESPONSE_TIMEOUT_SECONDS = 8
local MAX_POINTS = 50000

local function safe_get(target, key)
    if target == nil then return nil end
    local ok, value = pcall(function() return target[key] end)
    return ok and value or nil
end

local function call(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, target, ...)
    if ok then return true, value end
    ok, value = pcall(fn, ...)
    return ok, ok and value or nil
end

local function each(collection, limit, consume)
    if collection == nil then return 0 end
    local count = 0
    local length = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if length ~= nil then
        for index = 0, math.min(length - 1, limit - 1) do
            local item = safe_get(collection, index)
            if item == nil then item = safe_get(collection, index + 1) end
            if item ~= nil then
                count = count + 1
                if consume(item) == false then break end
            end
        end
        return count
    end
    local ok_enum, enumerator = call(collection, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        while count < limit do
            local ok_move, moved = call(enumerator, "MoveNext")
            if not ok_move or moved ~= true then break end
            count = count + 1
            if consume(safe_get(enumerator, "Current")) == false then break end
        end
        return count
    end
    if type(collection) == "table" then
        for _, item in pairs(collection) do
            if count >= limit then break end
            count = count + 1
            if consume(item) == false then break end
        end
    end
    return count
end

local function reflection_flags()
    local cs = rawget(_G, "CS")
    local binding = cs and cs.System and cs.System.Reflection and cs.System.Reflection.BindingFlags
    if binding ~= nil then
        local ok, value = pcall(function()
            return binding.Instance + binding.Public + binding.NonPublic
        end)
        if ok then return value end
    end
    return 52
end

local function reflected_value(component, key)
    if component == nil then return nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local flags = reflection_flags()
    local ok_field, field = pcall(function() return reflected_type:GetField(tostring(key), flags) end)
    if ok_field and field ~= nil then
        local ok_value, value = pcall(function() return field:GetValue(component) end)
        if ok_value then return value end
    end
    local ok_property, property = pcall(function() return reflected_type:GetProperty(tostring(key), flags) end)
    if ok_property and property ~= nil then
        local ok_value, value = pcall(function() return property:GetValue(component, nil) end)
        if ok_value then return value end
    end
    return nil
end

local function reflected_set_value(component, key, value)
    if component == nil then return false end
    local direct = pcall(function() component[key] = value end)
    if direct then return true end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false end
    local flags = reflection_flags()
    local ok_field, field = pcall(function() return reflected_type:GetField(tostring(key), flags) end)
    if ok_field and field ~= nil then
        return pcall(function() field:SetValue(component, value) end)
    end
    local ok_property, property = pcall(function() return reflected_type:GetProperty(tostring(key), flags) end)
    if ok_property and property ~= nil and safe_get(property, "CanWrite") == true then
        return pcall(function() property:SetValue(component, value, nil) end)
    end
    return false
end

local function integer_field(target, names)
    for _, name in ipairs(names) do
        local value = safe_get(target, name)
        if value == nil then value = reflected_value(target, name) end
        local numeric = tonumber(value)
        if numeric ~= nil and numeric == math.floor(numeric) then return numeric end
    end
    return nil
end

local function scalar_field(target, names)
    for _, name in ipairs(names) do
        local value = safe_get(target, name)
        if value == nil then value = reflected_value(target, name) end
        if value ~= nil then
            if type(value) == "string" or type(value) == "boolean" or type(value) == "number" then return value end
            local numeric = tonumber(value)
            if numeric ~= nil then return numeric end
            local text = tostring(value)
            if text ~= "" then return text end
        end
    end
    return nil
end

local function runtime_clock()
    local cs = rawget(_G, "CS")
    local time = cs and cs.UnityEngine and cs.UnityEngine.Time
    return tonumber(time and safe_get(time, "realtimeSinceStartup")) or tonumber(os.clock()) or 0
end

local function runtime_world()
    local cs = rawget(_G, "CS")
    local manager = cs and safe_get(cs, "SceneManager")
    local world = manager and safe_get(manager, "World")
    if world == nil then return nil, nil, "CS.SceneManager.World unavailable" end
    local ok_type, reflected_type = pcall(function() return world:GetType() end)
    local name = ok_type and tostring(safe_get(reflected_type, "Name") or "") or ""
    if name ~= "WorldScene" then return nil, nil, "CS.SceneManager.World is not WorldScene" end
    local point_manager = safe_get(world, "PointManager") or reflected_value(world, "PointManager")
    if point_manager == nil or reflected_value(point_manager, "_pointInfos") == nil then
        return world, nil, "WorldScene.PointManager unavailable"
    end
    return world, point_manager, nil
end

local function scene_identity()
    local cs = rawget(_G, "CS")
    local manager = cs and safe_get(cs, "SceneManager")
    local ids = rawget(_G, "SceneManagerSceneID") or (cs and safe_get(cs, "SceneManagerSceneID"))
    local current = manager and safe_get(manager, "CurrSceneID")
    local city = ids and safe_get(ids, "City")
    local world = ids and safe_get(ids, "World")
    if current ~= nil and world ~= nil and current == world then return "world" end
    if current ~= nil and city ~= nil and current == city then return "city" end
    return "unknown"
end

local function current_server_id()
    local entry = rawget(_G, "GameEntry")
    local data = entry and safe_get(entry, "Data")
    local player = data and safe_get(data, "Player")
    if player == nil then
        local lua_entry = rawget(_G, "LuaEntry")
        player = lua_entry and safe_get(lua_entry, "Player") or nil
    end
    if player == nil then return nil end
    local ok, value = call(player, "GetCurServerId")
    local numeric = ok and tonumber(value) or nil
    return numeric and numeric > 0 and math.floor(numeric) or nil
end

local function world_size(world)
    local value = tonumber(safe_get(world, "WorldSize"))
    if value == nil then
        local ok, observed = call(world, "get_WorldSize")
        value = ok and tonumber(observed) or nil
    end
    return value and math.floor(value) or nil
end

local function index_to_tile(world, index)
    local numeric = tonumber(index)
    if numeric == nil or numeric <= 0 then return nil end
    local ok, tile = call(world, "IndexToTilePos", math.floor(numeric))
    local x = ok and tonumber(safe_get(tile, "x") or safe_get(tile, "X")) or nil
    local y = ok and tonumber(safe_get(tile, "y") or safe_get(tile, "Y")) or nil
    if x ~= nil and y ~= nil then return { x = math.floor(x), y = math.floor(y) } end
    local size = world_size(world)
    if size == nil or size < 1 then return nil end
    local zero = math.floor(numeric) - 1
    return { x = zero % size, y = math.floor(zero / size) }
end

local function manager_response_flag(point_manager)
    local value = safe_get(point_manager, "isRecvViewPoints")
    if value == nil then value = reflected_value(point_manager, "isRecvViewPoints") end
    return value == nil and nil or value == true
end

local function world_response_flag(world)
    local value = safe_get(world, "hasReceiveViewPointsReply")
    if value == nil then value = reflected_value(world, "hasReceiveViewPointsReply") end
    return value == nil and nil or value == true
end

local function json_escape(value)
    local text = tostring(value or "")
    return string.gsub(text, '[%z\1-\31\\"]', function(ch)
        if ch == '"' then return '\\"' end
        if ch == '\\' then return '\\\\' end
        if ch == '\b' then return '\\b' end
        if ch == '\f' then return '\\f' end
        if ch == '\n' then return '\\n' end
        if ch == '\r' then return '\\r' end
        if ch == '\t' then return '\\t' end
        return string.format('\\u%04x', string.byte(ch))
    end)
end

local function json_encode(value)
    local kind = type(value)
    if value == nil then return "null" end
    if kind == "boolean" then return value and "true" or "false" end
    if kind == "number" then return tostring(value) end
    if kind == "string" then return '"' .. json_escape(value) .. '"' end
    if kind ~= "table" then return '"' .. json_escape(tostring(value)) .. '"' end
    local array, maximum, count = true, 0, 0
    for key in pairs(value) do
        if type(key) ~= "number" or key < 1 or key ~= math.floor(key) then array = false; break end
        maximum = math.max(maximum, key); count = count + 1
    end
    if array and maximum == count then
        local parts = {}
        for index = 1, maximum do parts[#parts + 1] = json_encode(value[index]) end
        return "[" .. table.concat(parts, ",") .. "]"
    end
    local keys = {}
    for key in pairs(value) do keys[#keys + 1] = key end
    table.sort(keys, function(left, right) return tostring(left) < tostring(right) end)
    local parts = {}
    for _, key in ipairs(keys) do
        parts[#parts + 1] = '"' .. json_escape(tostring(key)) .. '":' .. json_encode(value[key])
    end
    return "{" .. table.concat(parts, ",") .. "}"
end

local function write_json(path, value)
    local file = io.open(path, "wb")
    if not file then return false end
    file:write(json_encode(value)); file:close(); return true
end

local function write_heartbeat(now)
    return write_json(heartbeat_path, {
        probeVersion = M.VERSION,
        updatedAt = now,
        phase = phase,
        requestId = active_request_id,
        launchSessionId = active_launch_session_id,
        profileId = active_profile_id,
        gamePid = active_game_pid,
        acquisitionOrdinal = acquisition_ordinal,
    })
end

local function restore_flags(world, point_manager)
    if not flags_touched then return true end
    local manager_ok = reflected_set_value(point_manager, "isRecvViewPoints", original_manager_flag == true)
    local world_ok = reflected_set_value(world, "hasReceiveViewPointsReply", original_world_flag == true)
    flags_touched = false
    return manager_ok and world_ok
end

local function fail_request(now, message, world, point_manager)
    if world ~= nil and point_manager ~= nil then restore_flags(world, point_manager) end
    write_json(result_path, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = active_request_id,
        launchSessionId = active_launch_session_id,
        profileId = active_profile_id,
        gamePid = active_game_pid,
        acquisitionOrdinal = acquisition_ordinal,
        state = "failed",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
        error = tostring(message or "live_resource_failed"),
    })
    active_request_id = nil; active_launch_session_id = nil; active_profile_id = nil; active_game_pid = nil
    phase = "idle"; response_started_at = nil
end

local function resource_record(world, point_manager)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if expected == nil or expected < 0 or expected > MAX_POINTS then
        return nil, "loaded point count is outside bounded limit"
    end
    local values = safe_get(collection, "Values")
    local enumerable = values ~= nil and values or collection
    local candidates = {}
    local scanned = each(enumerable, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local point_type = integer_field(info, { "pointType", "PointType" })
        if point_type ~= 1 and point_type ~= 7 and point_type ~= 26 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex", "pointId", "PointId" })
        if id == nil or id <= 0 then return true end
        local tile = index_to_tile(world, id)
        if tile == nil then return true end
        local server_id = integer_field(info, { "serverId", "ServerId" }) or current_server_id()
        if server_id == nil or server_id <= 0 then return true end
        local resource_info = safe_get(info, "collectResourceInfo") or safe_get(info, "CollectResourceInfo")
        local resource_source = info
        local ok_resource, loaded_resource = call(point_manager, "GetResourcePointInfoByIndex", id)
        if ok_resource and loaded_resource ~= nil then resource_source = loaded_resource end
        local level = resource_info and scalar_field(resource_info, { "level", "Level" }) or nil
        if level == nil then
            local ok_level, observed_level = call(resource_source, "GetResLevel")
            level = ok_level and tonumber(observed_level) or scalar_field(resource_source, { "level", "Level" })
        end
        local resource_type = resource_info and scalar_field(resource_info, { "resourceType", "ResourceType" }) or nil
        if resource_type == nil then
            local ok_type, observed_type = call(resource_source, "GetResType")
            resource_type = ok_type and (tonumber(observed_type) or tostring(observed_type)) or nil
        end
        candidates[#candidates + 1] = {
            id = id,
            pointId = id,
            pointType = point_type,
            kind = "resource_point",
            serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
            x = tile.x,
            y = tile.y,
            level = level,
            resourceTypeId = resource_type,
            source = "WorldPointManager._pointInfos",
        }
        return true
    end)
    if scanned ~= expected then return nil, "loaded point enumeration did not match _pointInfos.Count" end
    if #candidates == 0 then return nil, "no resource point is loaded after the fresh view response" end
    -- IMPLEMENTATION POLICY: rotate the selected live row so consecutive proof
    -- acquisitions are visibly distinguishable when at least two resources are
    -- present. This does not assert an original LWBridge row-selection rule.
    local selected_index = ((acquisition_ordinal - 1) % #candidates) + 1
    return candidates[selected_index], nil, #candidates, expected, selected_index
end

local function read_command()
    local file = io.open(command_path, "rb")
    if file == nil then return nil end
    local text = file:read("*a") or ""; file:close(); os.remove(command_path)
    if #text > 2048 then return false, "command_too_large" end
    local values = {}
    for line in string.gmatch(text, "[^\r\n]+") do
        local key, value = string.match(line, "^([%w_]+)=(.*)$")
        if key ~= nil then values[key] = value end
    end
    local request_id = tostring(values.requestId or "")
    local launch_session_id = tostring(values.launchSessionId or "")
    local profile_id = tostring(values.profileId or "")
    local game_pid = tonumber(values.gamePid)
    local valid_token = function(value)
        return #value > 0 and #value <= 128 and string.match(value, "^[%w_-]+$") ~= nil
    end
    if values.schema ~= "1" or not valid_token(request_id) or
       not valid_token(launch_session_id) or not valid_token(profile_id) or
       game_pid == nil or game_pid <= 0 or game_pid ~= math.floor(game_pid) then
        return false, "invalid_command"
    end
    return {
        requestId = request_id,
        launchSessionId = launch_session_id,
        profileId = profile_id,
        gamePid = game_pid,
    }, nil
end

local function begin_refresh(world, point_manager)
    original_manager_flag = manager_response_flag(point_manager)
    original_world_flag = world_response_flag(world)
    if original_manager_flag == nil or original_world_flag == nil then
        return false, "current view response flags unavailable"
    end
    if not reflected_set_value(point_manager, "isRecvViewPoints", false) then
        return false, "isRecvViewPoints reset failed"
    end
    if not reflected_set_value(world, "hasReceiveViewPointsReply", false) then
        reflected_set_value(point_manager, "isRecvViewPoints", original_manager_flag == true)
        return false, "hasReceiveViewPointsReply reset failed"
    end
    flags_touched = true
    local started = select(1, call(point_manager, "StartViewRequest"))
    local updated = select(1, call(point_manager, "UpdateViewRequest", true))
    if not started or not updated then
        restore_flags(world, point_manager)
        return false, not started and "StartViewRequest failed" or "UpdateViewRequest(true) failed"
    end
    response_started_at = runtime_clock(); phase = "waiting_response"
    return true, nil
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    if active_request_id == nil then
        local command, command_error = read_command()
        if command == false then
            write_json(result_path, {
                schemaVersion = 1, probeVersion = M.VERSION, requestId = "", state = "failed",
                capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now), error = command_error,
            })
        elseif command ~= nil then
            active_request_id = command.requestId
            active_launch_session_id = command.launchSessionId
            active_profile_id = command.profileId
            active_game_pid = command.gamePid
            request_started_at = runtime_clock()
            acquisition_ordinal = acquisition_ordinal + 1
            transition_requested = false
            phase = "waiting_world"
        end
        write_heartbeat(now)
        if active_request_id == nil then return true end
    end

    local scene = scene_identity()
    if scene ~= "world" then
        if scene == "city" and transition_requested ~= true then
            local scene_utils = rawget(_G, "SceneUtils")
            local change = safe_get(scene_utils, "ChangeToWorld")
            if type(change) == "function" then
                local ok = pcall(change)
                if not ok then ok = pcall(change, scene_utils) end
                transition_requested = ok == true
            end
        end
        phase = "waiting_world"; write_heartbeat(now); return true
    end

    local world, point_manager = runtime_world()
    if world == nil or point_manager == nil then phase = "waiting_world"; write_heartbeat(now); return true end
    if phase == "waiting_world" then
        local started, start_error = begin_refresh(world, point_manager)
        if not started then fail_request(now, start_error, world, point_manager) end
        write_heartbeat(now); return true
    end
    if phase == "waiting_response" then
        local manager_received = manager_response_flag(point_manager)
        local world_received = world_response_flag(world)
        if manager_received == true and world_received == true then
            local point, point_error, resource_count, loaded_count, selected_index = resource_record(world, point_manager)
            local restored = restore_flags(world, point_manager)
            if point == nil then fail_request(now, point_error, world, point_manager); write_heartbeat(now); return true end
            if not restored then fail_request(now, "response flags did not restore", world, point_manager); write_heartbeat(now); return true end
            write_json(result_path, {
                schemaVersion = 1,
                probeVersion = M.VERSION,
                requestId = active_request_id,
                launchSessionId = active_launch_session_id,
                profileId = active_profile_id,
                gamePid = active_game_pid,
                acquisitionOrdinal = acquisition_ordinal,
                state = "proven",
                capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
                requestRoute = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)",
                responseEvidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true",
                source = "WorldPointManager._pointInfos",
                loadedPointCount = loaded_count,
                resourcePointCount = resource_count,
                selectedResourceIndex = selected_index,
                point_records = { point },
            })
            active_request_id = nil; active_launch_session_id = nil; active_profile_id = nil; active_game_pid = nil
            phase = "idle"; response_started_at = nil
        elseif response_started_at ~= nil and runtime_clock() - response_started_at >= RESPONSE_TIMEOUT_SECONDS then
            fail_request(now, "fresh view response timeout", world, point_manager)
        end
        write_heartbeat(now); return true
    end
    write_heartbeat(now); return true
end

function M.Register()
    if registration_method ~= nil then return true end
    -- Match the live-proven R7 helper: keep the xLua delegate rooted for the
    -- lifetime of the module so the managed update/timer registration cannot
    -- outlive its Lua callback reference.
    update_callback = function() M.Pump() end
    local manager = rawget(_G, "UpdateManager")
    local instance = manager
    if manager ~= nil and type(safe_get(manager, "GetInstance")) == "function" then
        local ok_instance, value = call(manager, "GetInstance")
        if ok_instance and value ~= nil then instance = value end
    end
    local add_update = safe_get(instance, "AddUpdate")
    if type(add_update) == "function" then
        local ok_update = pcall(add_update, instance, update_callback)
        if not ok_update then ok_update = pcall(add_update, update_callback) end
        if ok_update then registration_method = "UpdateManager.AddUpdate" end
    end
    if registration_method == nil then
        local cs = rawget(_G, "CS")
        local entries = {}
        local global_entry = rawget(_G, "GameEntry")
        local cs_entry = cs and safe_get(cs, "GameEntry") or nil
        if global_entry ~= nil then entries[#entries + 1] = global_entry end
        if cs_entry ~= nil and cs_entry ~= global_entry then entries[#entries + 1] = cs_entry end
        for _, entry in ipairs(entries) do
            local timer = safe_get(entry, "Timer")
            local register_repeat = timer and safe_get(timer, "RegisterTimerRepeat")
            if type(register_repeat) == "function" then
                local ok_timer, handle = pcall(register_repeat, timer, 0.25, 0.25, update_callback)
                if not ok_timer then ok_timer, handle = pcall(register_repeat, 0.25, 0.25, update_callback) end
                if ok_timer then
                    timer_handle = handle
                    registration_method = "GameEntry.Timer.RegisterTimerRepeat"
                    break
                end
            end
        end
    end
    M.Pump()
    return registration_method ~= nil
end

M.Register()
return M
