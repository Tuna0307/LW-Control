-- Bounded LWBridge rebuild live-resource acquisition probe.
--
-- Game-facing members in this file are restricted to the current-client
-- WorldPointManager/WorldScene surface already exercised by LWB-R7-001 and the
-- build-1078 R6 findings.  The command/result files and time bounds are rebuild
-- IMPLEMENTATION POLICY and are not claimed as original LWBridge protocol.

local M = { VERSION = "lwbridge-live-resource-probe-2" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWBridgeRebuild\live-resource]]
local heartbeat_path = root .. [[\heartbeat.json]]
local command_path = root .. [[\command.txt]]
local result_path = root .. [[\result.json]]
local aoi_diagnostic_path = root .. [[\aoi-diagnostic.txt]]
local aoi_diagnostic_result_path = root .. [[\aoi-diagnostic-result.json]]
local runtime_diagnostic_path = root .. [[\runtime-diagnostic.txt]]
local runtime_diagnostic_result_path = root .. [[\runtime-diagnostic-result.json]]
local overview_root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWBridgeRebuild\overview-bridge]]
local overview_control_path = overview_root .. [[\control.txt]]
local overview_lease_path = overview_root .. [[\lease.txt]]
local phase = "idle"
local active_request_id = nil
local active_launch_session_id = nil
local active_profile_id = nil
local active_game_pid = nil
local active_map_kind = nil
local active_allow_city_targeted_fallback = true
local city_targeted_requested = false
local city_target_point_id = nil
local city_target_lod = nil
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

local function reflection_static_flags()
    local cs = rawget(_G, "CS")
    local binding = cs and cs.System and cs.System.Reflection and cs.System.Reflection.BindingFlags
    if binding ~= nil then
        local ok, value = pcall(function()
            return binding.Static + binding.Public + binding.NonPublic
        end)
        if ok then return value end
    end
    return 56
end

local function reflected_static_value(component, key)
    if component == nil then return nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local flags = reflection_static_flags()
    local ok_property, property = pcall(function() return reflected_type:GetProperty(tostring(key), flags) end)
    if ok_property and property ~= nil then
        local ok_value, value = pcall(function() return property:GetValue(nil, nil) end)
        if ok_value and value ~= nil then return value end
    end
    local ok_field, field = pcall(function() return reflected_type:GetField(tostring(key), flags) end)
    if ok_field and field ~= nil then
        local ok_value, value = pcall(function() return field:GetValue(nil) end)
        if ok_value and value ~= nil then return value end
    end
    return nil
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

local function reflected_call(component, key, ...)
    if component == nil then return false, nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false, nil end
    local flags = reflection_flags()
    local ok_method, method = pcall(function() return reflected_type:GetMethod(tostring(key), flags) end)
    if not ok_method or method == nil then return false, nil end
    local raw_args = { ... }
    local arguments = nil
    if #raw_args > 0 then
        local cs = rawget(_G, "CS")
        local typeof_fn = rawget(_G, "typeof")
        local array_type = cs and cs.System and cs.System.Array or nil
        local object_type = cs and cs.System and cs.System.Object or nil
        local int32_type = cs and cs.System and cs.System.Int32 or nil
        if array_type == nil or object_type == nil or type(typeof_fn) ~= "function" then return false, nil end
        local ok_args, values = pcall(function()
            local result = array_type.CreateInstance(typeof_fn(object_type), #raw_args)
            for index, raw in ipairs(raw_args) do
                local value = raw
                if type(raw) == "number" and int32_type ~= nil then
                    value = int32_type.Parse(tostring(math.floor(raw)))
                end
                result:SetValue(value, index - 1)
            end
            return result
        end)
        if not ok_args or values == nil then return false, nil end
        arguments = values
    end
    local ok_value, value = pcall(function() return method:Invoke(component, arguments) end)
    if not ok_value then return false, nil end
    return true, value
end

local function reflected_field_value(component, key)
    if component == nil then return false, nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false, nil end
    local flags = reflection_flags()
    local ok_field, field = pcall(function() return reflected_type:GetField(tostring(key), flags) end)
    if not ok_field or field == nil then return false, nil end
    local ok_value, value = pcall(function() return field:GetValue(component) end)
    if not ok_value then return false, nil end
    return true, value
end

local function reflected_type_name(component)
    if component == nil then return nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local full_name = safe_get(reflected_type, "FullName") or safe_get(reflected_type, "Name")
    local text = full_name ~= nil and tostring(full_name) or ""
    return text ~= "" and text or nil
end

local function occupancy_value_present(value)
    if value == nil then return false end
    local text = tostring(value):match("^%s*(.-)%s*$")
    return text ~= "" and text ~= "0"
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

local function read_kv_file(path, maximum_bytes)
    local file = io.open(path, "rb")
    if file == nil then return nil end
    local text = file:read("*a") or ""; file:close()
    if #text > (maximum_bytes or 4096) then return nil end
    local values = {}
    for line in string.gmatch(text, "[^\r\n]+") do
        local key, value = string.match(line, "^([%w_]+)=(.*)$")
        if key ~= nil then values[key] = value end
    end
    return values
end

local function valid_token(value)
    return type(value) == "string" and #value > 0 and #value <= 128 and
        string.match(value, "^[%w_-]+$") ~= nil
end

local function collection_count(value)
    if value == nil then return nil end
    return tonumber(safe_get(value, "Count") or safe_get(value, "Length"))
end

local function vector_components(value)
    if value == nil then return nil, nil, nil end
    return tonumber(safe_get(value, "x") or safe_get(value, "X")),
        tonumber(safe_get(value, "y") or safe_get(value, "Y")),
        tonumber(safe_get(value, "z") or safe_get(value, "Z"))
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
        mapKind = active_map_kind,
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
        mapKind = active_map_kind,
        acquisitionOrdinal = acquisition_ordinal,
        state = "failed",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
        error = tostring(message or "live_resource_failed"),
    })
    active_request_id = nil; active_launch_session_id = nil; active_profile_id = nil; active_game_pid = nil; active_map_kind = nil
    active_allow_city_targeted_fallback = true
    city_targeted_requested = false; city_target_point_id = nil; city_target_lod = nil
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
        local gather_march_found, gather_march_uuid = reflected_field_value(resource_source, "gatherMarchUuid")
        local gather_uid_found, gather_uid = reflected_field_value(resource_source, "gatherUid")
        local gather_occupancy_known = gather_march_found and gather_uid_found
        local gather_occupied = nil
        if gather_occupancy_known then
            gather_occupied = occupancy_value_present(gather_march_uuid) or occupancy_value_present(gather_uid)
        end
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
            resourceSourceType = reflected_type_name(resource_source),
            gatherOccupancyKnown = gather_occupancy_known,
            gatherOccupied = gather_occupied,
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
    local ordered = { candidates[selected_index] }
    for index = 1, #candidates do
        if index ~= selected_index then ordered[#ordered + 1] = candidates[index] end
    end
    return candidates[selected_index], nil, #candidates, expected, selected_index, ordered
end

local function city_record(world, point_manager)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "loaded point count is outside bounded limit" end
    local values = safe_get(collection, "Values")
    local enumerable = values ~= nil and values or collection
    local candidates = {}
    local scanned = each(enumerable, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        if integer_field(info, { "pointType", "PointType" }) ~= 6 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex" })
        if id == nil or id <= 0 then return true end
        local tile = index_to_tile(world, id)
        if tile == nil then return true end
        local server_id = integer_field(info, { "serverId", "ServerId" }) or current_server_id()
        if server_id == nil or server_id <= 0 then return true end
        -- Current build-1078 _pointInfos is List<PointInfo>. Player bases are
        -- BuildPointInfo runtime objects, whose city fields are direct members.
        local owner_uid = scalar_field(info, { "ownerUid", "OwnerUid" })
        local owner_name = scalar_field(info, { "playerName", "PlayerName" })
        if owner_uid == nil or tostring(owner_uid) == "" or tostring(owner_uid) == "0" or
           owner_name == nil or tostring(owner_name) == "" then return true end
        local uuid = scalar_field(info, { "uuid", "Uuid" })
        local alliance_id = scalar_field(info, { "allianceId", "AllianceId" })
        local alliance_name = scalar_field(info, { "alAbbr", "AlAbbr" })
        candidates[#candidates + 1] = {
            id = id,
            pointId = id,
            pointType = 6,
            kind = "player_base",
            runtimeClass = reflected_type_name(info),
            serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
            x = tile.x,
            y = tile.y,
            uuid = uuid ~= nil and tostring(uuid) or nil,
            ownerUid = tostring(owner_uid),
            ownerName = tostring(owner_name),
            allianceId = alliance_id ~= nil and tostring(alliance_id) or nil,
            allianceName = alliance_name ~= nil and tostring(alliance_name) or nil,
            level = scalar_field(info, { "level", "Level" }),
            health = scalar_field(info, { "curHp", "CurHp" }),
            protectEndTime = scalar_field(info, { "protectEndTime", "ProtectEndTime" }),
            source = "WorldPointManager._pointInfos",
        }
        return true
    end)
    if scanned ~= expected then return nil, "loaded point enumeration did not match _pointInfos.Count" end
    if #candidates == 0 then
        return nil, "no Player City point is loaded after the fresh view response", 0, expected, nil, {}
    end
    local selected_index = ((acquisition_ordinal - 1) % #candidates) + 1
    local ordered = { candidates[selected_index] }
    for index = 1, #candidates do
        if index ~= selected_index then ordered[#ordered + 1] = candidates[index] end
    end
    return candidates[selected_index], nil, #candidates, expected, selected_index, ordered
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
    local map_kind = tostring(values.mapKind or "")
    local allow_city_targeted_fallback = true
    if values.allowCityTargetedFallback == "false" then
        allow_city_targeted_fallback = false
    elseif values.allowCityTargetedFallback ~= nil and values.allowCityTargetedFallback ~= "" and
           values.allowCityTargetedFallback ~= "true" then
        return false, "invalid_command"
    end
    local valid_token = function(value)
        return #value > 0 and #value <= 128 and string.match(value, "^[%w_-]+$") ~= nil
    end
    if values.schema ~= "1" or not valid_token(request_id) or
       not valid_token(launch_session_id) or not valid_token(profile_id) or
       (map_kind ~= "resource" and map_kind ~= "city") or
       game_pid == nil or game_pid <= 0 or game_pid ~= math.floor(game_pid) then
        return false, "invalid_command"
    end
    return {
        requestId = request_id,
        launchSessionId = launch_session_id,
        profileId = profile_id,
        gamePid = game_pid,
        mapKind = map_kind,
        allowCityTargetedFallback = allow_city_targeted_fallback,
    }, nil
end

local function active_overview_identity(now)
    local control = read_kv_file(overview_control_path, 4096)
    local lease = read_kv_file(overview_lease_path, 4096)
    if control == nil or lease == nil then return nil, "overview_session_unavailable" end
    if control.schema ~= "1" or control.bridgeVersion ~= "lwbridge-overview-bridge-1" or
       not valid_token(control.profileId) or not valid_token(control.sessionId) or
       not valid_token(control.challenge) then
        return nil, "overview_control_invalid"
    end
    local game_pid = tonumber(control.gamePid)
    if game_pid == nil or game_pid <= 0 or game_pid ~= math.floor(game_pid) then
        return nil, "overview_game_pid_invalid"
    end
    if lease.schema ~= "1" or lease.bridgeVersion ~= "lwbridge-overview-bridge-1" or
       lease.sessionId ~= control.sessionId or lease.challenge ~= control.challenge then
        return nil, "overview_lease_identity_mismatch"
    end
    local updated = tonumber(lease.updatedAt)
    if updated == nil or updated > now + 5 or now - updated > 5 then
        return nil, "overview_lease_stale"
    end
    return {
        profileId = control.profileId,
        sessionId = control.sessionId,
        challenge = control.challenge,
        gamePid = math.floor(game_pid),
    }, nil
end

local function read_aoi_diagnostic(now)
    local values = read_kv_file(aoi_diagnostic_path, 4096)
    if values == nil then return nil, nil end
    pcall(os.remove, aoi_diagnostic_path)
    local request_id = tostring(values.requestId or "")
    local request = { requestId = request_id }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request_id) then
        request.error = "aoi_diagnostic_invalid"
        return request, nil
    end
    local profile_id = tostring(values.profileId or "")
    local launch_session_id = tostring(values.launchSessionId or "")
    local challenge = tostring(values.challenge or "")
    local game_pid = tonumber(values.gamePid)
    local cell_x = tonumber(values.cellX)
    local cell_y = tonumber(values.cellY)
    local tile_count = tonumber(values.tileCount)
    if not valid_token(profile_id) or not valid_token(launch_session_id) or not valid_token(challenge) or
       game_pid == nil or game_pid <= 0 or game_pid ~= math.floor(game_pid) or
       cell_x == nil or cell_y == nil or tile_count == nil or
       cell_x < 0 or cell_y < 0 or tile_count <= 0 or
       cell_x ~= math.floor(cell_x) or cell_y ~= math.floor(cell_y) or tile_count ~= math.floor(tile_count) then
        request.error = "aoi_diagnostic_invalid"
        return request, nil
    end
    request.profileId = profile_id
    request.launchSessionId = launch_session_id
    request.challenge = challenge
    request.gamePid = math.floor(game_pid)
    request.cellX = math.floor(cell_x)
    request.cellY = math.floor(cell_y)
    request.tileCount = math.floor(tile_count)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request, nil end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "aoi_diagnostic_identity_mismatch"
    end
    return request, nil
end

local function read_runtime_diagnostic(now)
    local values = read_kv_file(runtime_diagnostic_path, 4096)
    if values == nil then return nil end
    pcall(os.remove, runtime_diagnostic_path)
    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "runtime_diagnostic_invalid"
        return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) then
        request.error = "runtime_diagnostic_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "runtime_diagnostic_identity_mismatch"
    end
    return request
end

local function object_shape(value)
    if value == nil then return { exists = false } end
    return {
        exists = true,
        luaType = type(value),
        reflectedType = reflected_type_name(value),
    }
end

local function write_runtime_diagnostic_result(request, state, error_text, details)
    details = details or {}
    write_json(runtime_diagnostic_result_path, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = request.requestId,
        launchSessionId = request.launchSessionId,
        profileId = request.profileId,
        challenge = request.challenge,
        gamePid = request.gamePid,
        state = state,
        error = error_text,
        globalGameEntry = details.globalGameEntry,
        csGameEntry = details.csGameEntry,
        luaEntry = details.luaEntry,
        gameMain = details.gameMain,
        dataCenter = details.dataCenter,
        globalEntryNetwork = details.globalEntryNetwork,
        globalEntryData = details.globalEntryData,
        globalEntryPlayer = details.globalEntryPlayer,
        csEntryNetwork = details.csEntryNetwork,
        csEntryData = details.csEntryData,
        csEntryPlayer = details.csEntryPlayer,
        luaEntryPlayer = details.luaEntryPlayer,
        luaEntryNetwork = details.luaEntryNetwork,
        luaEntryData = details.luaEntryData,
        luaEntryGameEntry = details.luaEntryGameEntry,
        gameMainGameEntry = details.gameMainGameEntry,
        dataCenterPlayer = details.dataCenterPlayer,
        globalNetworkManager = details.globalNetworkManager,
        globalCustomNetworkManager = details.globalCustomNetworkManager,
        networkLoginedType = details.networkLoginedType,
        networkLogined = details.networkLogined,
        networkConnectedType = details.networkConnectedType,
        networkConnected = details.networkConnected,
        networkConnectingType = details.networkConnectingType,
        networkConnecting = details.networkConnecting,
        reflectedLoginedType = details.reflectedLoginedType,
        reflectedLogined = details.reflectedLogined,
        reflectedConnectedType = details.reflectedConnectedType,
        reflectedConnected = details.reflectedConnected,
        reflectedConnectingType = details.reflectedConnectingType,
        reflectedConnecting = details.reflectedConnecting,
        reflectedLoginedMethodOk = details.reflectedLoginedMethodOk,
        reflectedLoginedMethodType = details.reflectedLoginedMethodType,
        reflectedLoginedMethodValue = details.reflectedLoginedMethodValue,
        reflectedConnectedMethodOk = details.reflectedConnectedMethodOk,
        reflectedConnectedMethodType = details.reflectedConnectedMethodType,
        reflectedConnectedMethodValue = details.reflectedConnectedMethodValue,
        reflectedConnectingMethodOk = details.reflectedConnectingMethodOk,
        reflectedConnectingMethodType = details.reflectedConnectingMethodType,
        reflectedConnectingMethodValue = details.reflectedConnectingMethodValue,
        loginedGetterOk = details.loginedGetterOk,
        loginedGetterType = details.loginedGetterType,
        loginedGetterValue = details.loginedGetterValue,
        connectedGetterOk = details.connectedGetterOk,
        connectedGetterType = details.connectedGetterType,
        connectedGetterValue = details.connectedGetterValue,
        connectingGetterOk = details.connectingGetterOk,
        connectingGetterType = details.connectingGetterType,
        connectingGetterValue = details.connectingGetterValue,
        uidCallOk = details.uidCallOk,
        uidType = details.uidType,
        uidNonEmpty = details.uidNonEmpty,
        serverCallOk = details.serverCallOk,
        serverNumeric = details.serverNumeric,
        serverPositive = details.serverPositive,
        worldPosNumeric = details.worldPosNumeric,
        worldPosPositive = details.worldPosPositive,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function pump_runtime_diagnostic(now)
    local request = read_runtime_diagnostic(now)
    if request == nil then return false end
    if request.error ~= nil then
        write_runtime_diagnostic_result(request, "failed", request.error, nil)
        return true
    end
    local cs = rawget(_G, "CS")
    local global_entry = rawget(_G, "GameEntry")
    local cs_entry = cs and safe_get(cs, "GameEntry") or nil
    local lua_entry = rawget(_G, "LuaEntry")
    local game_main = rawget(_G, "GameMain")
    local data_center = rawget(_G, "DataCenter")
    local global_data = global_entry and safe_get(global_entry, "Data") or nil
    local cs_data = cs_entry and safe_get(cs_entry, "Data") or nil
    local cs_network = cs_entry and safe_get(cs_entry, "Network") or nil
    local cs_player = cs_data and safe_get(cs_data, "Player") or nil
    local logged_in = cs_network and safe_get(cs_network, "Logined") or nil
    local connected = cs_network and safe_get(cs_network, "IsConnected") or nil
    local connecting = cs_network and safe_get(cs_network, "IsConnecting") or nil
    local reflected_logined = cs_network and reflected_value(cs_network, "Logined") or nil
    local reflected_connected = cs_network and reflected_value(cs_network, "IsConnected") or nil
    local reflected_connecting = cs_network and reflected_value(cs_network, "IsConnecting") or nil
    local reflected_logined_ok, reflected_logined_method = reflected_call(cs_network, "get_Logined")
    local reflected_connected_ok, reflected_connected_method = reflected_call(cs_network, "get_IsConnected")
    local reflected_connecting_ok, reflected_connecting_method = reflected_call(cs_network, "get_IsConnecting")
    local logined_getter_ok, logined_getter = call(cs_network, "get_Logined")
    local connected_getter_ok, connected_getter = call(cs_network, "get_IsConnected")
    local connecting_getter_ok, connecting_getter = call(cs_network, "get_IsConnecting")
    local uid_ok, uid = call(cs_player, "GetUid")
    local server_ok, server_id = call(cs_player, "GetCurServerId")
    local world_pos = cs_player and safe_get(cs_player, "PlayerWorldPointId") or nil
    local server_numeric = tonumber(server_id)
    local world_pos_numeric = tonumber(world_pos)
    write_runtime_diagnostic_result(request, "proven", nil, {
        globalGameEntry = object_shape(global_entry),
        csGameEntry = object_shape(cs_entry),
        luaEntry = object_shape(lua_entry),
        gameMain = object_shape(game_main),
        dataCenter = object_shape(data_center),
        globalEntryNetwork = object_shape(global_entry and safe_get(global_entry, "Network") or nil),
        globalEntryData = object_shape(global_data),
        globalEntryPlayer = object_shape(global_data and safe_get(global_data, "Player") or nil),
        csEntryNetwork = object_shape(cs_entry and safe_get(cs_entry, "Network") or nil),
        csEntryData = object_shape(cs_data),
        csEntryPlayer = object_shape(cs_data and safe_get(cs_data, "Player") or nil),
        luaEntryPlayer = object_shape(lua_entry and safe_get(lua_entry, "Player") or nil),
        luaEntryNetwork = object_shape(lua_entry and safe_get(lua_entry, "Network") or nil),
        luaEntryData = object_shape(lua_entry and safe_get(lua_entry, "Data") or nil),
        luaEntryGameEntry = object_shape(lua_entry and safe_get(lua_entry, "GameEntry") or nil),
        gameMainGameEntry = object_shape(game_main and safe_get(game_main, "GameEntry") or nil),
        dataCenterPlayer = object_shape(data_center and safe_get(data_center, "Player") or nil),
        globalNetworkManager = object_shape(rawget(_G, "NetworkManager")),
        globalCustomNetworkManager = object_shape(rawget(_G, "CustomNetworkManager")),
        networkLoginedType = type(logged_in),
        networkLogined = type(logged_in) == "boolean" and logged_in or nil,
        networkConnectedType = type(connected),
        networkConnected = type(connected) == "boolean" and connected or nil,
        networkConnectingType = type(connecting),
        networkConnecting = type(connecting) == "boolean" and connecting or nil,
        reflectedLoginedType = type(reflected_logined),
        reflectedLogined = type(reflected_logined) == "boolean" and reflected_logined or nil,
        reflectedConnectedType = type(reflected_connected),
        reflectedConnected = type(reflected_connected) == "boolean" and reflected_connected or nil,
        reflectedConnectingType = type(reflected_connecting),
        reflectedConnecting = type(reflected_connecting) == "boolean" and reflected_connecting or nil,
        reflectedLoginedMethodOk = reflected_logined_ok == true,
        reflectedLoginedMethodType = type(reflected_logined_method),
        reflectedLoginedMethodValue = type(reflected_logined_method) == "boolean" and reflected_logined_method or nil,
        reflectedConnectedMethodOk = reflected_connected_ok == true,
        reflectedConnectedMethodType = type(reflected_connected_method),
        reflectedConnectedMethodValue = type(reflected_connected_method) == "boolean" and reflected_connected_method or nil,
        reflectedConnectingMethodOk = reflected_connecting_ok == true,
        reflectedConnectingMethodType = type(reflected_connecting_method),
        reflectedConnectingMethodValue = type(reflected_connecting_method) == "boolean" and reflected_connecting_method or nil,
        loginedGetterOk = logined_getter_ok == true,
        loginedGetterType = type(logined_getter),
        loginedGetterValue = type(logined_getter) == "boolean" and logined_getter or nil,
        connectedGetterOk = connected_getter_ok == true,
        connectedGetterType = type(connected_getter),
        connectedGetterValue = type(connected_getter) == "boolean" and connected_getter or nil,
        connectingGetterOk = connecting_getter_ok == true,
        connectingGetterType = type(connecting_getter),
        connectingGetterValue = type(connecting_getter) == "boolean" and connecting_getter or nil,
        uidCallOk = uid_ok == true,
        uidType = type(uid),
        uidNonEmpty = uid ~= nil and tostring(uid) ~= "",
        serverCallOk = server_ok == true,
        serverNumeric = server_numeric ~= nil,
        serverPositive = server_numeric ~= nil and server_numeric > 0,
        worldPosNumeric = world_pos_numeric ~= nil,
        worldPosPositive = world_pos_numeric ~= nil and world_pos_numeric > 0,
    })
    return true
end

local function read_aoi_size_array(point_manager)
    local raw = safe_get(point_manager, "lwAoiBlockSizeArray") or
        reflected_value(point_manager, "lwAoiBlockSizeArray")
    if raw == nil then
        raw = reflected_static_value(point_manager, "lwAoiBlockSizeArray") or
            reflected_static_value(point_manager, "_lwAoiBlockSizeArray")
    end
    if raw == nil then
        local cs = rawget(_G, "CS")
        local point_manager_type = cs and safe_get(cs, "WorldPointManager") or nil
        raw = point_manager_type and safe_get(point_manager_type, "lwAoiBlockSizeArray") or nil
    end
    if raw == nil then return nil end
    local length = tonumber(safe_get(raw, "Length") or safe_get(raw, "Count"))
    if length == nil or length < 1 or length > 32 then return nil end
    local result = {}
    for index = 0, math.floor(length) - 1 do
        local value = tonumber(safe_get(raw, index))
        if value == nil then value = tonumber(safe_get(raw, index + 1)) end
        if value == nil then return nil end
        result[#result + 1] = math.floor(value)
    end
    return result
end

local function write_aoi_diagnostic_result(request, state, error_text, details)
    details = details or {}
    write_json(aoi_diagnostic_result_path, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = request.requestId,
        launchSessionId = request.launchSessionId,
        profileId = request.profileId,
        challenge = request.challenge,
        gamePid = request.gamePid,
        state = state,
        error = error_text,
        cellX = request.cellX,
        cellY = request.cellY,
        tileCount = request.tileCount,
        currentLod = details.currentLod,
        serverLod = details.serverLod,
        lwAoiBlockSizeArray = details.aoiSizes,
        lwAoiBlockSize = details.blockSize,
        lwAoiBlockCount = details.blockCount,
        msgViewIndexCount = details.msgCount,
        addViewIndexCount = details.addCount,
        curViewIndexCount = details.curCount,
        aoiIndex = details.aoiIndex,
        centerX = details.centerX,
        centerY = details.centerY,
        centerZ = details.centerZ,
        method = details.method or "WorldPointManager.AoiBlockToIndex+GetAoiIndexCenter(read-only)",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function pump_aoi_diagnostic(now)
    local request = select(1, read_aoi_diagnostic(now))
    if request == nil then return false end
    if request.error ~= nil then
        write_aoi_diagnostic_result(request, "failed", request.error, nil)
        return true
    end
    local world, point_manager, world_error = runtime_world()
    if world == nil or point_manager == nil then
        write_aoi_diagnostic_result(request, "failed", world_error or "world_unavailable", nil)
        return true
    end
    local lod = integer_field(point_manager, { "LOD" })
    local ok_server_lod, raw_server_lod = false, nil
    if lod ~= nil then ok_server_lod, raw_server_lod = call(point_manager, "GetServerLod", lod) end
    local server_lod = ok_server_lod and tonumber(raw_server_lod) or nil
    local aoi_sizes = read_aoi_size_array(point_manager)
    if lod == nil or server_lod == nil or aoi_sizes == nil then
        write_aoi_diagnostic_result(request, "failed", "aoi_geometry_unavailable", nil)
        return true
    end
    local block_size = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
    local block_count = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
    local ok_index, raw_index = reflected_call(point_manager, "AoiBlockToIndex", request.cellX, request.cellY)
    local aoi_index = ok_index and tonumber(raw_index) or nil
    local method = "WorldPointManager.AoiBlockToIndex+GetAoiIndexCenter(read-only)"
    if aoi_index == nil then
        if block_count == nil or block_count <= 0 then
            write_aoi_diagnostic_result(request, "failed", "aoi_index_unavailable", nil)
            return true
        end
        -- Exact current-v16 IL: AoiBlockToIndex(x,y) = y * _lwAoiBlockCount + x.
        aoi_index = request.cellY * block_count + request.cellX
        method = "WorldPointManager.AoiBlockToIndex+GetAoiIndexCenter(recovered-v16-IL,read-only)"
    end
    local ok_center, center = reflected_call(point_manager, "GetAoiIndexCenter", math.floor(aoi_index))
    local center_x, center_y, center_z = nil, nil, nil
    if ok_center then center_x, center_y, center_z = vector_components(center) end
    if center_x == nil or center_z == nil then
        if block_size == nil or block_size <= 0 then
            write_aoi_diagnostic_result(request, "failed", "aoi_center_unavailable", nil)
            return true
        end
        -- Exact current-v16 IL after IndexToAoiBlock: center=(2*size*x+size,0,2*size*y+size).
        center_x = block_size * request.cellX * 2 + block_size
        center_y = 0
        center_z = block_size * request.cellY * 2 + block_size
        method = "WorldPointManager.AoiBlockToIndex+GetAoiIndexCenter(recovered-v16-IL,read-only)"
    end
    write_aoi_diagnostic_result(request, "proven", nil, {
        currentLod = lod,
        serverLod = math.floor(server_lod),
        aoiSizes = aoi_sizes,
        blockSize = block_size,
        blockCount = block_count,
        msgCount = collection_count(reflected_value(point_manager, "_msgViewIndex")),
        addCount = collection_count(reflected_value(point_manager, "_addViewIndex")),
        curCount = collection_count(reflected_value(point_manager, "_curViewIndex")),
        aoiIndex = math.floor(aoi_index),
        centerX = center_x,
        centerY = center_y,
        centerZ = center_z,
        method = method,
    })
    return true
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

local function begin_targeted_city_refresh(world, point_manager)
    local cs = rawget(_G, "CS")
    local entry = rawget(_G, "GameEntry")
    if entry == nil and cs ~= nil then entry = safe_get(cs, "GameEntry") end
    local data = entry and safe_get(entry, "Data") or nil
    local player = data and safe_get(data, "Player") or nil
    if player == nil then return false, "current player unavailable for city-targeted view request" end
    local point_id = integer_field(player, { "PlayerWorldPointId" })
    if point_id == nil or point_id <= 0 then return false, "PlayerWorldPointId unavailable for city-targeted view request" end
    local tile = index_to_tile(world, point_id)
    if tile == nil then return false, "PlayerWorldPointId could not be converted to a world tile" end
    local server_id = current_server_id()
    if server_id == nil or server_id <= 0 then return false, "current server unavailable for city-targeted view request" end
    local lod = integer_field(point_manager, { "LOD" })
    if lod == nil or lod < 0 then return false, "current WorldPointManager.LOD unavailable for city-targeted view request" end
    local vector_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
    if vector_type == nil then return false, "UnityEngine.Vector2Int unavailable for city-targeted view request" end
    local ok_vector, tile_pos = pcall(function() return vector_type(tile.x, tile.y) end)
    if not ok_vector or tile_pos == nil then return false, "could not construct current city target tile" end
    if not reflected_set_value(point_manager, "isRecvViewPoints", false) then
        return false, "city-targeted isRecvViewPoints reset failed"
    end
    if not reflected_set_value(world, "hasReceiveViewPointsReply", false) then
        restore_flags(world, point_manager)
        return false, "city-targeted hasReceiveViewPointsReply reset failed"
    end
    local sent = select(1, call(point_manager, "SendViewRequest", tile_pos, math.floor(lod), math.floor(server_id)))
    if not sent then
        restore_flags(world, point_manager)
        return false, "SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId) failed"
    end
    city_targeted_requested = true
    city_target_point_id = math.floor(point_id)
    city_target_lod = math.floor(lod)
    response_started_at = runtime_clock()
    phase = "waiting_city_target_response"
    return true, nil
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    if active_request_id == nil then
        pump_runtime_diagnostic(now)
        pump_aoi_diagnostic(now)
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
            active_map_kind = command.mapKind
            active_allow_city_targeted_fallback = command.allowCityTargetedFallback ~= false
            request_started_at = runtime_clock()
            acquisition_ordinal = acquisition_ordinal + 1
            transition_requested = false
            city_targeted_requested = false
            city_target_point_id = nil
            city_target_lod = nil
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
    if phase == "waiting_response" or phase == "waiting_city_target_response" then
        local manager_received = manager_response_flag(point_manager)
        local world_received = world_response_flag(world)
        if manager_received == true and world_received == true then
            local response_phase = phase
            local point, point_error, matched_count, loaded_count, selected_index, point_records
            if active_map_kind == "city" then
                point, point_error, matched_count, loaded_count, selected_index, point_records = city_record(world, point_manager)
            else
                point, point_error, matched_count, loaded_count, selected_index, point_records = resource_record(world, point_manager)
            end
            if point == nil and active_map_kind == "city" and response_phase == "waiting_response" and
               not city_targeted_requested and active_allow_city_targeted_fallback then
                local targeted, target_error = begin_targeted_city_refresh(world, point_manager)
                if targeted then write_heartbeat(now); return true end
                restore_flags(world, point_manager)
                fail_request(now, tostring(point_error) .. "; targeted city request unavailable: " .. tostring(target_error), world, point_manager)
                write_heartbeat(now); return true
            end
            local proven_empty_city = point == nil and active_map_kind == "city" and
                response_phase == "waiting_response" and not active_allow_city_targeted_fallback and
                point_error == "no Player City point is loaded after the fresh view response" and
                matched_count == 0 and type(point_records) == "table" and #point_records == 0
            local restored = restore_flags(world, point_manager)
            if point == nil and not proven_empty_city then
                fail_request(now, point_error, world, point_manager); write_heartbeat(now); return true
            end
            if not restored then fail_request(now, "response flags did not restore", world, point_manager); write_heartbeat(now); return true end
            local route = "WorldPointManager.StartViewRequest+UpdateViewRequest(true)"
            local response_evidence = "isRecvViewPoints=false->true;hasReceiveViewPointsReply=false->true"
            if city_targeted_requested then
                route = route .. "+SendViewRequest(PlayerWorldPointId,currentLOD,currentServerId)"
                response_evidence = response_evidence .. ";targetedSameServerView=false->true"
            end
            write_json(result_path, {
                schemaVersion = 1,
                probeVersion = M.VERSION,
                requestId = active_request_id,
                launchSessionId = active_launch_session_id,
                profileId = active_profile_id,
                gamePid = active_game_pid,
                mapKind = active_map_kind,
                acquisitionOrdinal = acquisition_ordinal,
                state = "proven",
                capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
                requestRoute = route,
                responseEvidence = response_evidence,
                source = "WorldPointManager._pointInfos",
                loadedPointCount = loaded_count,
                resourcePointCount = active_map_kind == "resource" and matched_count or nil,
                cityPointCount = active_map_kind == "city" and matched_count or nil,
                selectedResourceIndex = active_map_kind == "resource" and selected_index or nil,
                selectedCityIndex = active_map_kind == "city" and selected_index or nil,
                cityTargetedView = active_map_kind == "city" and city_targeted_requested or nil,
                cityTargetPointId = active_map_kind == "city" and city_target_point_id or nil,
                cityTargetLod = active_map_kind == "city" and city_target_lod or nil,
                -- IMPLEMENTATION POLICY: expose the complete candidate snapshot from
                -- this one proven fresh response. Keep the rotating selected row first
                -- so the existing bounded one-row importer remains backward compatible.
                point_records = point_records or { point },
            })
            active_request_id = nil; active_launch_session_id = nil; active_profile_id = nil; active_game_pid = nil; active_map_kind = nil
            active_allow_city_targeted_fallback = true
            city_targeted_requested = false; city_target_point_id = nil; city_target_lod = nil
            phase = "idle"; response_started_at = nil
        elseif response_started_at ~= nil and runtime_clock() - response_started_at >= RESPONSE_TIMEOUT_SECONDS then
            local timeout_label = phase == "waiting_city_target_response" and "fresh targeted city view response timeout" or "fresh view response timeout"
            fail_request(now, timeout_label, world, point_manager)
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
