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
local bulk_aoi_diagnostic_path = root .. [[\bulk-aoi-diagnostic.txt]]
local bulk_aoi_diagnostic_result_path = root .. [[\bulk-aoi-diagnostic-result.json]]
local monster_protection_detail_path = root .. [[\monster-protection-detail.txt]]
local monster_protection_detail_result_path = root .. [[\monster-protection-detail-result.json]]
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
local bulk_aoi_request = nil
local bulk_aoi_started_at = nil
local monster_protection_request = nil
local monster_protection_started_at = nil
local monster_protection_scan = nil
local bulk_aoi_original_start_view_request = nil
local bulk_aoi_original_block_count = nil
local bulk_aoi_block_count_touched = false
local bulk_aoi_added_indices = {}
local bulk_aoi_native_camera = nil
local bulk_aoi_native_touch_camera = nil
local bulk_aoi_native_original_pos = nil
local bulk_aoi_native_original_fov = nil
local bulk_aoi_native_hold_seconds = nil
local bulk_aoi_native_position_restored = false
local bulk_aoi_restore_current_view = false
local bulk_aoi_original_manager_response_flag = nil
local bulk_aoi_original_world_response_flag = nil
local bulk_aoi_response_flags_reset = false
local bulk_aoi_skip_restore_update = false
local bulk_aoi_original_manager_lod = nil
local bulk_aoi_original_camera_lod = nil

-- IMPLEMENTATION POLICY: this guards a bounded current-view response wait.  It
-- is not an original LWBridge timeout.
local RESPONSE_TIMEOUT_SECONDS = 8
local BULK_AOI_TIMEOUT_SECONDS = 8
local MONSTER_INVASION_PROTECTION_TIMEOUT_SECONDS = 3
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

local function reflected_method(component, key, parameter_count)
    if component == nil then return nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local flags = reflection_flags()
    local methods = nil
    local ok_methods, value = pcall(function() return reflected_type:GetMethods(flags) end)
    if ok_methods then methods = value end
    if methods == nil then
        ok_methods, value = pcall(function() return reflected_type:GetMethods() end)
        if ok_methods then methods = value end
    end
    local length = methods and tonumber(safe_get(methods, "Length")) or nil
    if length == nil then return nil end
    for index = 0, length - 1 do
        local ok_method, method = pcall(function() return methods:GetValue(index) end)
        if ok_method and method ~= nil and tostring(safe_get(method, "Name") or "") == tostring(key) then
            local ok_params, parameters = pcall(function() return method:GetParameters() end)
            local count = ok_params and parameters and tonumber(safe_get(parameters, "Length")) or nil
            if parameter_count == nil or count == parameter_count then return method end
        end
    end
    return nil
end

local function reflected_call(component, key, ...)
    if component == nil then return false, nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false, nil end
    local method = reflected_method(component, key, select("#", ...))
    if method == nil then return false, nil end
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

local function reflected_call_bool(component, key, raw_value)
    if component == nil then return false, nil, "component_unavailable" end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false, nil, "type_unavailable" end
    local method = reflected_method(component, key, 1)
    if method == nil then return false, nil, "method_unavailable" end
    local cs = rawget(_G, "CS")
    local typeof_fn = rawget(_G, "typeof")
    local array_type = cs and cs.System and cs.System.Array or nil
    local object_type = cs and cs.System and cs.System.Object or nil
    local boolean_type = cs and cs.System and cs.System.Boolean or nil
    if array_type == nil or object_type == nil or boolean_type == nil or type(typeof_fn) ~= "function" then
        return false, nil, "reflection_types_unavailable"
    end
    local ok_args, arguments = pcall(function()
        local result = array_type.CreateInstance(typeof_fn(object_type), 1)
        result:SetValue(boolean_type.Parse(raw_value and "True" or "False"), 0)
        return result
    end)
    if not ok_args or arguments == nil then return false, nil, "argument_boxing_failed" end
    local ok_value, value = pcall(function() return method:Invoke(component, arguments) end)
    if not ok_value then return false, nil, tostring(value) end
    return true, value, nil
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

local function reflected_set_int_field(component, key, value)
    if component == nil or type(value) ~= "number" then return false end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false end
    local flags = reflection_flags()
    local ok_field, field = pcall(function() return reflected_type:GetField(tostring(key), flags) end)
    if not ok_field or field == nil then return false end
    local cs = rawget(_G, "CS")
    local int32_type = cs and cs.System and cs.System.Int32 or nil
    if int32_type == nil then return false end
    local ok_boxed, boxed = pcall(function() return int32_type.Parse(tostring(math.floor(value))) end)
    if not ok_boxed or boxed == nil then return false end
    local ok_set = pcall(function() field:SetValue(component, boxed) end)
    if not ok_set then return false end
    local ok_read, observed = pcall(function() return field:GetValue(component) end)
    local numeric = ok_read and tonumber(observed) or nil
    if numeric == nil and ok_read and observed ~= nil then numeric = tonumber(tostring(observed)) end
    return numeric == math.floor(value)
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

local function tile_distance(world, a, b)
    if a == nil or b == nil then return nil end
    local cs = rawget(_G, "CS")
    local vector_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
    if vector_type == nil then return nil end
    local ok_a, av = pcall(function() return vector_type(a.x, a.y) end)
    local ok_b, bv = pcall(function() return vector_type(b.x, b.y) end)
    if not ok_a or not ok_b or av == nil or bv == nil then return nil end
    local ok, value = call(world, "TileDistance", av, bv)
    local numeric = ok and tonumber(value) or nil
    return numeric and numeric >= 0 and numeric or nil
end

local function current_home_tile(world)
    local entry = rawget(_G, "GameEntry")
    local data = entry and safe_get(entry, "Data") or nil
    local player = data and safe_get(data, "Player") or nil
    local point_id = player and integer_field(player, { "PlayerWorldPointId" }) or nil
    return point_id and point_id > 0 and index_to_tile(world, point_id) or nil
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

local function collection_int_values(value, limit)
    local expected = collection_count(value)
    if expected == nil or expected < 0 or expected > limit then return nil, "count_invalid" end
    local cs = rawget(_G, "CS")
    local typeof_fn = rawget(_G, "typeof")
    local array_type = cs and cs.System and cs.System.Array or nil
    local int32_type = cs and cs.System and cs.System.Int32 or nil
    local object_type = cs and cs.System and cs.System.Object or nil
    if array_type == nil or int32_type == nil or object_type == nil or type(typeof_fn) ~= "function" then
        return nil, "array_type_unavailable"
    end
    local ok_array, values = pcall(function()
        return array_type.CreateInstance(typeof_fn(int32_type), expected)
    end)
    if not ok_array or values == nil then return nil, "array_create_failed" end
    local copied = select(1, call(value, "CopyTo", values))
    local method = copied and "HashSet.CopyTo(int[])" or nil
    if not copied then
        local ok_type, reflected_type = pcall(function() return value:GetType() end)
        local flags = reflection_flags()
        local methods = ok_type and reflected_type and reflected_type:GetMethods(flags) or nil
        each(methods, 128, function(candidate)
            if copied or tostring(safe_get(candidate, "Name") or "") ~= "CopyTo" then return true end
            local parameters = candidate:GetParameters()
            local length = tonumber(safe_get(parameters, "Length") or safe_get(parameters, "Count"))
            if length ~= 1 then return true end
            local parameter = safe_get(parameters, 0) or safe_get(parameters, 1)
            local parameter_type = parameter and safe_get(parameter, "ParameterType") or nil
            local is_array = parameter_type and safe_get(parameter_type, "IsArray") == true
            local element_type = is_array and parameter_type:GetElementType() or nil
            if element_type == nil or tostring(element_type) ~= "System.Int32" then return true end
            local args = array_type.CreateInstance(typeof_fn(object_type), 1)
            args:SetValue(values, 0)
            local ok_invoke = pcall(function() candidate:Invoke(value, args) end)
            if ok_invoke then copied = true; method = "HashSet.CopyTo(int[])-reflection" end
            return not copied
        end)
    end
    if not copied then return nil, "copyto_failed" end
    local result = {}
    for index = 0, expected - 1 do
        local raw = safe_get(values, index)
        if raw == nil then
            local ok_get, observed = call(values, "GetValue", index)
            raw = ok_get and observed or nil
        end
        local numeric = tonumber(safe_get(raw, "Value") or raw)
        if numeric == nil or numeric < 0 or numeric ~= math.floor(numeric) then
            return nil, "copied_value_invalid"
        end
        result[#result + 1] = math.floor(numeric)
    end
    table.sort(result)
    return result, method
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

local function read_bulk_aoi_diagnostic(now)
    local values = read_kv_file(bulk_aoi_diagnostic_path, 4096)
    if values == nil then return nil end
    pcall(os.remove, bulk_aoi_diagnostic_path)
    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "bulk_aoi_diagnostic_invalid"
        return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    request.serverId = tonumber(values.serverId)
    request.scanRunId = tostring(values.scanRunId or "")
    request.viewLevel = tonumber(values.viewLevel or "-1")
    request.requestMode = tostring(values.requestMode or "native")
    request.targetTileX = tonumber(values.targetTileX)
    request.targetTileY = tonumber(values.targetTileY)
    request.requestedCount = tonumber(values.requestedCount or "8")
    request.holdMilliseconds = tonumber(values.holdMilliseconds or "1000")
    request.homeTileX = tonumber(values.homeTileX or "-1")
    request.homeTileY = tonumber(values.homeTileY or "-1")
    local include_monster_raw = tostring(values.includeMonster or "false")
    request.includeMonster = include_monster_raw == "true"
    local include_train_raw = tostring(values.includeTrain or "false")
    request.includeTrain = include_train_raw == "true"
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or
       request.serverId == nil or request.serverId <= 0 or request.serverId ~= math.floor(request.serverId) or
       not valid_token(request.scanRunId) or
       request.viewLevel == nil or request.viewLevel < -1 or request.viewLevel > 2 or request.viewLevel ~= math.floor(request.viewLevel) or
       (request.requestMode ~= "native" and request.requestMode ~= "expanded" and request.requestMode ~= "direct" and request.requestMode ~= "coverage") or
       request.targetTileX == nil or request.targetTileY == nil or
       request.targetTileX < 0 or request.targetTileX >= 1000 or request.targetTileY < 0 or request.targetTileY >= 1000 or
       request.targetTileX ~= math.floor(request.targetTileX) or request.targetTileY ~= math.floor(request.targetTileY) or
       request.requestedCount == nil or
       request.requestedCount < 1 or request.requestedCount > 160 or
       request.requestedCount ~= math.floor(request.requestedCount) or
       request.holdMilliseconds == nil or request.holdMilliseconds < 0 or
       request.holdMilliseconds > 2000 or request.holdMilliseconds ~= math.floor(request.holdMilliseconds) or
       request.homeTileX == nil or request.homeTileY == nil or
       request.homeTileX < -1 or request.homeTileX >= 1000 or request.homeTileY < -1 or request.homeTileY >= 1000 or
       request.homeTileX ~= math.floor(request.homeTileX) or request.homeTileY ~= math.floor(request.homeTileY) or
       ((request.homeTileX == -1) ~= (request.homeTileY == -1)) or
       (include_monster_raw ~= "true" and include_monster_raw ~= "false") or
       (include_train_raw ~= "true" and include_train_raw ~= "false") then
        request.error = "bulk_aoi_diagnostic_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    request.serverId = math.floor(request.serverId)
    request.viewLevel = math.floor(request.viewLevel)
    request.targetTileX = math.floor(request.targetTileX)
    request.targetTileY = math.floor(request.targetTileY)
    request.requestedCount = math.floor(request.requestedCount)
    request.holdMilliseconds = math.floor(request.holdMilliseconds)
    request.homeTileX = math.floor(request.homeTileX)
    request.homeTileY = math.floor(request.homeTileY)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "bulk_aoi_diagnostic_identity_mismatch"
    end
    return request
end

local function collection_contains_int(collection, value)
    local ok, result = call(collection, "Contains", math.floor(value))
    if ok and type(result) == "boolean" then return result end
    local reflected_ok, reflected_result = reflected_call(collection, "Contains", math.floor(value))
    return reflected_ok and reflected_result == true
end

local function collection_add_int(collection, value)
    local ok = select(1, call(collection, "Add", math.floor(value)))
    if ok then return true end
    return select(1, reflected_call(collection, "Add", math.floor(value)))
end

local function collection_remove_int(collection, value)
    local ok = select(1, call(collection, "Remove", math.floor(value)))
    if ok then return true end
    return select(1, reflected_call(collection, "Remove", math.floor(value)))
end

local function collection_clear(collection)
    local ok = select(1, call(collection, "Clear"))
    if ok then return true end
    return select(1, reflected_call(collection, "Clear"))
end

local function point_aoi_counts(world, point_manager, block_size, block_count, selected_lookup)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end
    local matched, cities, resources = 0, 0, 0
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex" })
        if id == nil or id <= 0 then return true end
        local tile = index_to_tile(world, id)
        if tile == nil then return true end
        local cell_x = math.floor(tile.x / block_size)
        local cell_y = math.floor(tile.y / block_size)
        if cell_x < 0 or cell_y < 0 or cell_x >= block_count or cell_y >= block_count then return true end
        local aoi_index = cell_y * block_count + cell_x
        if selected_lookup[aoi_index] == true then
            matched = matched + 1
            local point_type = integer_field(info, { "pointType", "PointType" })
            if point_type == 6 then cities = cities + 1 end
            if point_type == 1 or point_type == 7 or point_type == 26 then resources = resources + 1 end
        end
        return true
    end)
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return { total = matched, cities = cities, resources = resources, loadedPointCount = expected }, nil
end

local function city_aoi_records(world, point_manager, block_size, block_count, selected_lookup)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end
    local records = {}
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        if integer_field(info, { "pointType", "PointType" }) ~= 6 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex" })
        if id == nil or id <= 0 then return true end
        local tile = index_to_tile(world, id)
        if tile == nil then return true end
        local cell_x = math.floor(tile.x / block_size)
        local cell_y = math.floor(tile.y / block_size)
        if cell_x < 0 or cell_y < 0 or cell_x >= block_count or cell_y >= block_count then return true end
        local aoi_index = cell_y * block_count + cell_x
        if selected_lookup[aoi_index] ~= true then return true end
        local server_id = integer_field(info, { "serverId", "ServerId" }) or current_server_id()
        if server_id == nil or server_id <= 0 then return true end
        local owner_uid = scalar_field(info, { "ownerUid", "OwnerUid" })
        local owner_name = scalar_field(info, { "playerName", "PlayerName" })
        if owner_uid == nil or tostring(owner_uid) == "" or tostring(owner_uid) == "0" or
           owner_name == nil or tostring(owner_name) == "" then return true end
        local uuid = scalar_field(info, { "uuid", "Uuid" })
        local alliance_id = scalar_field(info, { "allianceId", "AllianceId" })
        local alliance_name = scalar_field(info, { "alAbbr", "AlAbbr" })
        records[#records + 1] = {
            id = id, pointId = id, pointType = 6, kind = "player_base",
            runtimeClass = reflected_type_name(info), serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
            x = tile.x, y = tile.y,
            uuid = uuid ~= nil and tostring(uuid) or nil,
            ownerUid = tostring(owner_uid), ownerName = tostring(owner_name),
            allianceId = alliance_id ~= nil and tostring(alliance_id) or nil,
            allianceName = alliance_name ~= nil and tostring(alliance_name) or nil,
            level = scalar_field(info, { "level", "Level" }),
            health = scalar_field(info, { "curHp", "CurHp" }),
            protectEndTime = scalar_field(info, { "protectEndTime", "ProtectEndTime" }),
            source = "WorldPointManager._pointInfos",
        }
        return true
    end)
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return records, nil
end

local function resource_aoi_records(world, point_manager, block_size, block_count, selected_lookup)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end
    local records = {}
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local point_type = integer_field(info, { "pointType", "PointType" })
        if point_type ~= 1 and point_type ~= 7 and point_type ~= 26 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex", "pointId", "PointId" })
        if id == nil or id <= 0 then return true end
        local tile = index_to_tile(world, id)
        if tile == nil then return true end
        local cell_x = math.floor(tile.x / block_size)
        local cell_y = math.floor(tile.y / block_size)
        if cell_x < 0 or cell_y < 0 or cell_x >= block_count or cell_y >= block_count then return true end
        local aoi_index = cell_y * block_count + cell_x
        if selected_lookup[aoi_index] ~= true then return true end
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
        if gather_occupancy_known then gather_occupied = occupancy_value_present(gather_march_uuid) or occupancy_value_present(gather_uid) end
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
        records[#records + 1] = {
            id = id, pointId = id, pointType = point_type, kind = "resource_point",
            runtimeClass = reflected_type_name(info), serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
            x = tile.x, y = tile.y, level = level, resourceTypeId = resource_type,
            resourceSourceType = reflected_type_name(resource_source),
            gatherOccupancyKnown = gather_occupancy_known, gatherOccupied = gather_occupied,
            source = "WorldPointManager._pointInfos",
        }
        return true
    end)
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return records, nil
end

local function train_march_aoi_records(world, block_size, block_count, selected_lookup)
    local march_manager = safe_get(world, "MarchDataManager")
    if march_manager == nil then
        local ok_manager, value = call(world, "get_MarchDataManager")
        if ok_manager then march_manager = value end
    end
    if march_manager == nil then return nil, "WorldScene.MarchDataManager unavailable" end
    local ok_all, collection = call(march_manager, "GetAllMarchesByCS")
    if not ok_all or collection == nil then collection = reflected_value(march_manager, "allMarches") end
    if collection == nil then return nil, "WorldMarchDataManager.allMarches unavailable" end
    local ok_enum, enumerator = call(collection, "GetEnumerator")
    if not ok_enum or enumerator == nil then return nil, "march_enumerator_unavailable" end
    local records, scanned = {}, 0
    while scanned < MAX_POINTS do
        local ok_move, moved = call(enumerator, "MoveNext")
        if not ok_move then return nil, "march_enumerator_failed" end
        if moved ~= true then break end
        scanned = scanned + 1
        local pair = safe_get(enumerator, "Current")
        local march = pair and (safe_get(pair, "Value") or pair) or nil
        local train = march and safe_get(march, "train") or nil
        if train ~= nil then
            local ok_index, position_index = call(march, "GetMarchCurPosIndex")
            local index = ok_index and tonumber(position_index) or nil
            if index == nil or index <= 0 then index = integer_field(march, { "targetPos", "TargetPos" }) end
            local tile = index and index > 0 and index_to_tile(world, index) or nil
            if tile ~= nil then
                local cell_x = math.floor(tile.x / block_size)
                local cell_y = math.floor(tile.y / block_size)
                local aoi_index = cell_y * block_count + cell_x
                if cell_x >= 0 and cell_y >= 0 and cell_x < block_count and cell_y < block_count and selected_lookup[aoi_index] == true then
                    local config = safe_get(train, "config")
                    local train_data = safe_get(train, "trainData")
                    local train_data_json = nil
                    if train_data ~= nil then
                        local ok_json, value = call(train_data, "ToJson")
                        if ok_json and value ~= nil then train_data_json = tostring(value) end
                        if train_data_json == nil then
                            local ok_dump, dump = call(train_data, "GetDump")
                            if ok_dump and dump ~= nil then train_data_json = tostring(dump) end
                        end
                    end
                    records[#records + 1] = {
                        uuid = tostring(scalar_field(march, { "uuid", "Uuid", "_uuid" }) or ""),
                        runtimeClass = reflected_type_name(march),
                        serverId = integer_field(march, { "serverId", "ServerId" }) or current_server_id(),
                        worldId = integer_field(march, { "worldId", "WorldId" }) or 0,
                        x = tile.x, y = tile.y, positionIndex = math.floor(index),
                        ownerUid = scalar_field(march, { "ownerUid", "OwnerUid" }),
                        ownerName = scalar_field(march, { "ownerName", "OwnerName" }),
                        allianceUid = scalar_field(march, { "allianceUid", "AllianceUid" }),
                        allianceName = scalar_field(march, { "allianceName", "AllianceName" }),
                        ownerServer = integer_field(march, { "ownerServer", "OwnerServer" }),
                        targetServer = integer_field(march, { "targetServer", "TargetServer" }),
                        srcServer = integer_field(march, { "srcServer", "SrcServer" }),
                        power = scalar_field(march, { "power", "Power" }),
                        startTime = scalar_field(march, { "startTime", "StartTime" }),
                        endTime = scalar_field(march, { "endTime", "EndTime" }),
                        trainUuid = scalar_field(train, { "uuid", "Uuid" }),
                        trainCfgId = integer_field(train, { "cfgId", "CfgId" }),
                        trainType = integer_field(train, { "type", "Type" }),
                        trainQuality = config and integer_field(config, { "quality", "Quality" }) or nil,
                        carriageNum = config and integer_field(config, { "carriageNum", "CarriageNum" }) or nil,
                        trainDataJson = train_data_json,
                        source = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
                    }
                end
            end
        end
    end
    return records, nil
end

local function same_enum_value(left, right)
    if left == nil or right == nil then return false end
    local ok, equal = pcall(function() return left == right end)
    if ok and equal then return true end
    local lnum, rnum = tonumber(left), tonumber(right)
    return lnum ~= nil and rnum ~= nil and lnum == rnum
end

local function is_monster_invasion_template(template)
    if template == nil then return false end
    local specials = rawget(_G, "WorldMonsterSpecialType")
    local invasion_boss = specials and safe_get(specials, "MonsterInvasionBoss") or nil
    local special = scalar_field(template, { "special", "Special" })
    -- Current-v18 WorldMonsterDes.RefreshData issues MonsterInvasionBossDetail
    -- only for this exact game-owned special type. Names and other boss types
    -- are not substituted.
    return same_enum_value(special, invasion_boss)
end

local function monster_invasion_protection_deadline(march)
    local create_time = tonumber(scalar_field(march, { "createTime", "CreateTime" })) or 0
    local lua_entry = rawget(_G, "LuaEntry")
    local data_config = lua_entry and safe_get(lua_entry, "DataConfig") or nil
    local ok_cfg, cfg_time = call(data_config, "TryGetNum", "monster_invasion", "k12")
    cfg_time = ok_cfg and tonumber(cfg_time) or 0
    if create_time > 0 and cfg_time > 0 then return create_time + (cfg_time * 1000) end
    return 0
end

local function ensure_monster_protection_capture()
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "MonsterProtectionManager") or nil
    if manager == nil then return nil, "monster_protection_manager_unavailable" end
    local current = safe_get(manager, "OnGetDetail")
    if type(current) ~= "function" then return nil, "monster_protection_detail_handler_unavailable" end
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    if type(state) == "table" and state.manager == manager and state.wrapper == current then
        return state, nil
    end
    state = { manager = manager, original = current, pending = {}, responses = {} }
    state.wrapper = function(self, msg)
        local ok_original, result = pcall(state.original, self, msg)
        local uuid = msg and (safe_get(msg, "uuid") or reflected_value(msg, "uuid")) or nil
        local key = uuid ~= nil and tostring(uuid) or nil
        local pending = key ~= nil and state.pending[key] or nil
        if key ~= nil and pending ~= nil then
            local active_value = scalar_field(msg, { "isProtected", "IsProtected" })
            local active = active_value == true or tonumber(active_value) == 1
            local ok_end, end_time = call(manager, "GetMonsterProtectionEndTime", uuid)
            end_time = ok_end and tonumber(end_time) or 0
            if active and end_time <= 0 and type(pending) == "table" then
                end_time = tonumber(pending.sourceProtectionEndTime) or 0
            end
            if not active then end_time = 0 end
            state.responses[key] = { received = true, isProtected = active_value, protectionEndTime = end_time }
            state.pending[key] = nil
        end
        if not ok_original then error(result) end
        return result
    end
    local ok_set = pcall(function() manager.OnGetDetail = state.wrapper end)
    if not ok_set or safe_get(manager, "OnGetDetail") ~= state.wrapper then
        return nil, "monster_protection_detail_capture_install_failed"
    end
    rawset(_G, "__lwbridgeMonsterProtectionCapture", state)
    return state, nil
end

local function monster_invasion_protection_targets(world, block_size, block_count, selected_lookup, requested_server_id)
    local march_manager = safe_get(world, "MarchDataManager") or reflected_value(world, "MarchDataManager")
    if march_manager == nil then return nil, nil, "WorldScene.MarchDataManager unavailable" end
    local ok_all, collection = call(march_manager, "GetAllMarchesByCS")
    if not ok_all or collection == nil then collection = reflected_value(march_manager, "allMarches") end
    if collection == nil then return nil, nil, "WorldMarchDataManager.allMarches unavailable" end
    local data_center = rawget(_G, "DataCenter")
    local template_manager = data_center and safe_get(data_center, "MonsterTemplateManager") or nil
    if template_manager == nil then return nil, nil, "monster_template_manager_unavailable" end
    local ok_enum, enumerator = call(collection, "GetEnumerator")
    if not ok_enum or enumerator == nil then return nil, nil, "march_enumerator_unavailable" end
    local targets, total, scanned = {}, 0, 0
    while scanned < MAX_POINTS do
        local ok_move, moved = call(enumerator, "MoveNext")
        if not ok_move then return nil, nil, "march_enumerator_failed" end
        if moved ~= true then break end
        scanned = scanned + 1
        local pair = safe_get(enumerator, "Current")
        local march = pair and (safe_get(pair, "Value") or pair) or nil
        if march ~= nil then
            local ok_boss, boss_flag = call(march, "IsBoss")
            local monster_id = integer_field(march, { "monsterId", "MonsterId" })
            local template = nil
            if ok_boss and boss_flag == true and monster_id ~= nil and monster_id > 0 then
                local ok_template, value = call(template_manager, "TryGetMonsterTemplate", monster_id)
                if ok_template then template = value end
            end
            local is_invasion = is_monster_invasion_template(template)
            if is_invasion then
                local ok_index, cur_index = call(march, "GetMarchCurPosIndex")
                local index = ok_index and tonumber(cur_index) or nil
                if index == nil or index <= 0 then index = integer_field(march, { "targetPos", "TargetPos" }) end
                local tile = index and index > 0 and index_to_tile(world, index) or nil
                if tile ~= nil then
                    local cell_x, cell_y = math.floor(tile.x / block_size), math.floor(tile.y / block_size)
                    local aoi_index = cell_y * block_count + cell_x
                    if selected_lookup == nil or selected_lookup[aoi_index] == true then
                        total = total + 1
                        local wire_uuid = safe_get(march, "uuid") or reflected_value(march, "uuid")
                        local uuid = wire_uuid ~= nil and tostring(wire_uuid) or ""
                        -- The original Zombie Boss panel sends view.ctrl.serverId,
                        -- not a march source-server field. The owned scan server is
                        -- the rebuild equivalent of that controller value.
                        local server_id = tonumber(requested_server_id) or current_server_id()
                        if uuid == "" then return nil, nil, "monster_invasion_boss_uuid_unavailable" end
                        if server_id == nil then return nil, nil, "monster_invasion_boss_server_unavailable" end
                        targets[#targets + 1] = {
                            uuid = uuid, wireUuid = wire_uuid, serverId = server_id,
                            sourceProtectionEndTime = monster_invasion_protection_deadline(march),
                        }
                    end
                end
            end
        end
    end
    return targets, total, nil
end

local function send_monster_invasion_protection_requests(targets)
    local state, capture_error = ensure_monster_protection_capture()
    if state == nil then return nil, capture_error end
    local sfs, defs = rawget(_G, "SFSNetwork"), rawget(_G, "MsgDefines")
    local message = defs and safe_get(defs, "MonsterInvasionBossDetail") or nil
    local send = sfs and safe_get(sfs, "SendMessage") or nil
    if message == nil or type(send) ~= "function" then
        return nil, "monster_invasion_protection_transport_unavailable"
    end
    for i = 1, #targets do
        local t = targets[i]
        state.pending[t.uuid] = t
        local prior = state.responses[t.uuid]
        if type(prior) ~= "table" or prior.received ~= true then state.responses[t.uuid] = nil end
        local ok = pcall(send, message, t.serverId, t.wireUuid)
        if not ok then ok = pcall(send, sfs, message, t.serverId, t.wireUuid) end
        if not ok then
            state.pending[t.uuid] = nil
            return nil, "monster_invasion_protection_send_failed"
        end
    end
    return #targets, nil
end

local function same_monster_protection_scan(scan, request)
    return type(scan) == "table" and
        scan.profileId == request.profileId and
        scan.launchSessionId == request.launchSessionId and
        scan.challenge == request.challenge and
        scan.gamePid == request.gamePid and
        scan.scanRunId == request.scanRunId and
        scan.serverId == request.serverId
end

local function queue_monster_invasion_protection_requests(request, targets)
    if not same_monster_protection_scan(monster_protection_scan, request) then
        if type(monster_protection_scan) == "table" and type(monster_protection_scan.targets) == "table" then
            local capture = rawget(_G, "__lwbridgeMonsterProtectionCapture")
            if type(capture) == "table" and type(capture.pending) == "table" then
                for index = 1, #monster_protection_scan.targets do
                    capture.pending[monster_protection_scan.targets[index].uuid] = nil
                end
            end
        end
        monster_protection_scan = {
            profileId = request.profileId,
            launchSessionId = request.launchSessionId,
            challenge = request.challenge,
            gamePid = request.gamePid,
            scanRunId = request.scanRunId,
            serverId = request.serverId,
            targets = {},
            targetByUuid = {},
            requestCount = 0,
            retryCount = 0,
            error = nil,
        }
    end
    local fresh = {}
    for index = 1, #targets do
        local target = targets[index]
        if monster_protection_scan.targetByUuid[target.uuid] == nil then
            monster_protection_scan.targetByUuid[target.uuid] = target
            monster_protection_scan.targets[#monster_protection_scan.targets + 1] = target
            fresh[#fresh + 1] = target
        end
    end
    if #fresh == 0 then return 0, nil end
    local sent, send_error = send_monster_invasion_protection_requests(fresh)
    if sent == nil then
        monster_protection_scan.error = send_error
        return 0, send_error
    end
    monster_protection_scan.requestCount = monster_protection_scan.requestCount + sent
    return sent, nil
end

local function count_ready_monster_invasion_protection_details(targets)
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    if type(state) ~= "table" then return 0 end
    local ready = 0
    for i = 1, #targets do
        local response = state.responses[targets[i].uuid]
        if type(response) == "table" and response.received == true and response.isProtected ~= nil then
            ready = ready + 1
        end
    end
    return ready
end

local function abandon_monster_invasion_protection_requests(targets)
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    if type(state) ~= "table" or type(state.pending) ~= "table" then return end
    for i = 1, #targets do
        state.pending[targets[i].uuid] = nil
    end
end

local function monster_invasion_protection_snapshot(uuid)
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    local response = type(state) == "table" and state.responses[tostring(uuid)] or nil
    if type(response) ~= "table" or response.received ~= true or response.isProtected == nil then
        return false, false, 0
    end
    local active = response.isProtected == true or tonumber(response.isProtected) == 1
    local end_time = tonumber(response.protectionEndTime) or 0
    if not active then end_time = 0 end
    return true, active, end_time
end

local function unanswered_monster_protection_targets(targets)
    local capture = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    local unresolved = {}
    for index = 1, #targets do
        local target = targets[index]
        local response = type(capture) == "table" and capture.responses[target.uuid] or nil
        if type(response) ~= "table" or response.received ~= true or response.isProtected == nil then
            unresolved[#unresolved + 1] = target
        end
    end
    return unresolved
end

local function read_monster_protection_detail(now)
    local values = read_kv_file(monster_protection_detail_path, 4096)
    if values == nil then return nil end
    pcall(os.remove, monster_protection_detail_path)
    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "monster_protection_detail_invalid"
        return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    request.scanRunId = tostring(values.scanRunId or "")
    request.serverId = tonumber(values.serverId)
    request.expectedTargetCount = tonumber(values.expectedTargetCount or "0")
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.scanRunId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or
       request.serverId == nil or request.serverId <= 0 or request.serverId ~= math.floor(request.serverId) or
       request.expectedTargetCount == nil or request.expectedTargetCount < 0 or
       request.expectedTargetCount ~= math.floor(request.expectedTargetCount) then
        request.error = "monster_protection_detail_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    request.serverId = math.floor(request.serverId)
    request.expectedTargetCount = math.floor(request.expectedTargetCount)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "monster_protection_detail_identity_mismatch"
    end
    return request
end

local function monster_protection_details(targets)
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    local details = {}
    for index = 1, #targets do
        local target = targets[index]
        local response = type(state) == "table" and state.responses[target.uuid] or nil
        local received = type(response) == "table" and response.received == true and response.isProtected ~= nil
        local active = received and (response.isProtected == true or tonumber(response.isProtected) == 1) or false
        local end_time = active and (tonumber(response.protectionEndTime) or 0) or 0
        details[#details + 1] = {
            uuid = target.uuid,
            received = received,
            isProtected = active,
            protectionEndTime = end_time,
        }
    end
    return details
end

local function write_monster_protection_detail_result(request, state, error_text, targets, request_count, ready_count)
    write_json(monster_protection_detail_result_path, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = request.requestId,
        launchSessionId = request.launchSessionId,
        profileId = request.profileId,
        challenge = request.challenge,
        gamePid = request.gamePid,
        scanRunId = request.scanRunId,
        serverId = request.serverId,
        expectedTargetCount = request.expectedTargetCount,
        state = state,
        error = error_text,
        targetCount = targets and #targets or 0,
        requestCount = request_count or 0,
        retryCount = request.retryCount or 0,
        readyCount = ready_count or 0,
        timedOut = error_text == "monster_invasion_protection_response_timeout",
        elapsedSeconds = monster_protection_started_at and (runtime_clock() - monster_protection_started_at) or 0,
        details = targets and monster_protection_details(targets) or {},
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function pump_monster_protection_detail(now)
    if monster_protection_request == nil then
        local request = read_monster_protection_detail(now)
        if request == nil then return false end
        if request.error ~= nil then
            write_monster_protection_detail_result(request, "completed", request.error, nil, 0, 0)
            return true
        end
        if not same_monster_protection_scan(monster_protection_scan, request) then
            write_monster_protection_detail_result(
                request, "completed", "monster_protection_scan_unavailable", nil, 0, 0)
            return true
        end
        request.targets = monster_protection_scan.targets or {}
        request.requestCount = monster_protection_scan.requestCount or 0
        request.retryCount = monster_protection_scan.retryCount or 0
        request.scanError = monster_protection_scan.error
        local unresolved = unanswered_monster_protection_targets(request.targets)
        if #unresolved > 0 then
            local sent, retry_error = send_monster_invasion_protection_requests(unresolved)
            if sent == nil then
                request.scanError = request.scanError or retry_error
            else
                request.retryCount = request.retryCount + sent
                monster_protection_scan.retryCount = request.retryCount
            end
        end
        if #request.targets ~= request.expectedTargetCount then
            write_monster_protection_detail_result(
                request, "completed", "monster_protection_target_count_mismatch",
                request.targets, request.requestCount,
                count_ready_monster_invasion_protection_details(request.targets))
            abandon_monster_invasion_protection_requests(request.targets)
            monster_protection_scan = nil
            return true
        end
        if #request.targets == 0 then
            write_monster_protection_detail_result(request, "completed", nil, request.targets, 0, 0)
            monster_protection_scan = nil
            return true
        end
        monster_protection_request = request
        monster_protection_started_at = runtime_clock()
        return true
    end

    local request = monster_protection_request
    local targets = request.targets or {}
    local ready = count_ready_monster_invasion_protection_details(targets)
    local elapsed = runtime_clock() - (monster_protection_started_at or runtime_clock())
    if ready < #targets and elapsed < MONSTER_INVASION_PROTECTION_TIMEOUT_SECONDS then return true end
    local error_text = request.scanError or
        (ready < #targets and "monster_invasion_protection_response_timeout" or nil)
    if error_text ~= nil then abandon_monster_invasion_protection_requests(targets) end
    write_monster_protection_detail_result(request, "completed", error_text, targets, request.requestCount, ready)
    monster_protection_request = nil
    monster_protection_started_at = nil
    monster_protection_scan = nil
    return true
end

local function monster_march_aoi_records(world, block_size, block_count, selected_lookup, home_tile)
    local march_manager = safe_get(world, "MarchDataManager")
    if march_manager == nil then
        local ok_manager, value = call(world, "get_MarchDataManager")
        if ok_manager then march_manager = value end
    end
    if march_manager == nil then return nil, "WorldScene.MarchDataManager unavailable" end
    local data_center = rawget(_G, "DataCenter")
    local template_manager = data_center and safe_get(data_center, "MonsterTemplateManager") or nil
    local ok_all, collection = call(march_manager, "GetAllMarchesByCS")
    if not ok_all or collection == nil then collection = reflected_value(march_manager, "allMarches") end
    if collection == nil then return nil, "WorldMarchDataManager.allMarches unavailable" end
    local ok_enum, enumerator = call(collection, "GetEnumerator")
    if not ok_enum or enumerator == nil then return nil, "march_enumerator_unavailable" end
    local records, scanned = {}, 0
    while scanned < MAX_POINTS do
        local ok_move, moved = call(enumerator, "MoveNext")
        if not ok_move then return nil, "march_enumerator_failed" end
        if moved ~= true then break end
        scanned = scanned + 1
        local pair = safe_get(enumerator, "Current")
        local march = pair and (safe_get(pair, "Value") or pair) or nil
        if march ~= nil then
            local ok_kind, is_monster_or_boss = call(march, "IsMonsterOrBoss")
            if ok_kind and is_monster_or_boss == true then
                local ok_index, position_index = call(march, "GetMarchCurPosIndex")
                local index = ok_index and tonumber(position_index) or nil
                if index == nil or index <= 0 then index = integer_field(march, { "targetPos", "TargetPos" }) end
                local tile = index and index > 0 and index_to_tile(world, index) or nil
                if tile ~= nil then
                    local cell_x = math.floor(tile.x / block_size)
                    local cell_y = math.floor(tile.y / block_size)
                    local aoi_index = cell_y * block_count + cell_x
                    if cell_x >= 0 and cell_y >= 0 and cell_x < block_count and cell_y < block_count and
                       selected_lookup[aoi_index] == true then
                        local function flag(name)
                            local ok, value = call(march, name)
                            return ok and value == true
                        end
                        local ok_hp, hp = call(march, "GetHP")
                        local ok_max_hp, max_hp = call(march, "GetMaxHP")
                        local monster_id = integer_field(march, { "monsterId", "MonsterId" }) or 0
                        local template = nil
                        if template_manager ~= nil and monster_id > 0 then
                            local ok_template, value = call(template_manager, "TryGetMonsterTemplate", monster_id)
                            if ok_template then template = value end
                        end
                        -- Current-v17 Assembly-CSharp.rdl type 1700 proves WorldMarch.zMBossInfo
                        -- is a ZMBossInfo carrying the Zombie Boss stage/shield fields. Prefer the
                        -- normal xLua field getter, then the reflected field, and never substitute
                        -- generic march endTime/nextStageTime for shieldEndTime.
                        local zm_boss_info = safe_get(march, "zMBossInfo")
                        if zm_boss_info == nil then zm_boss_info = reflected_value(march, "zMBossInfo") end
                        local raw_record_uuid = safe_get(march, "uuid") or reflected_value(march, "uuid")
                        local record_uuid = raw_record_uuid ~= nil and tostring(raw_record_uuid) or ""
                        local protection_eligible = is_monster_invasion_template(template)
                        local protection_known, protection_active, protection_end_time =
                            monster_invasion_protection_snapshot(record_uuid)
                        records[#records + 1] = {
                            uuid = record_uuid,
                            kind = "monster", runtimeClass = reflected_type_name(march),
                            serverId = integer_field(march, { "serverId", "ServerId" }) or current_server_id(),
                            worldId = integer_field(march, { "worldId", "WorldId" }) or 0,
                            x = tile.x, y = tile.y, positionIndex = math.floor(index),
                            distanceFromHome = tile_distance(world, home_tile, tile),
                            monsterId = monster_id,
                            monsterType = integer_field(march, { "monsterType", "MonsterType" }) or 0,
                            monsterSpecialType = integer_field(march, { "monsterSpecialType", "MonsterSpecialType" }) or 0,
                            monsterRallyNum = integer_field(march, { "monsterRallyNum", "MonsterRallyNum" }) or 0,
                            monsterHpRatio = scalar_field(march, { "monsterHpRatio", "MonsterHpRatio" }),
                            configId = template and integer_field(template, { "id", "Id" }) or nil,
                            monsterNameKey = template and scalar_field(template, { "name", "Name" }) or nil,
                            monsterLevel = template and integer_field(template, { "level", "Level" }) or nil,
                            configType = template and integer_field(template, { "type", "Type" }) or nil,
                            configSpecial = template and integer_field(template, { "special", "Special" }) or nil,
                            configBoss = template and integer_field(template, { "boss", "Boss" }) or nil,
                            modelName = template and scalar_field(template, { "model_name", "modelName" }) or nil,
                            picture = template and scalar_field(template, { "pic", "Pic" }) or nil,
                            hp = ok_hp and hp or nil, maxHp = ok_max_hp and max_hp or nil,
                            refreshTime = scalar_field(march, { "refreshTime", "RefreshTime" }),
                            createTime = scalar_field(march, { "createTime", "CreateTime" }),
                            expireTime = scalar_field(march, { "expireTime", "ExpireTime" }),
                            actStartTime = scalar_field(march, { "actStartTime", "ActStartTime" }),
                            actEndTime = scalar_field(march, { "actEndTime", "ActEndTime" }),
                            startTime = scalar_field(march, { "startTime", "StartTime" }),
                            endTime = scalar_field(march, { "endTime", "EndTime" }),
                            zMBossId = integer_field(zm_boss_info, { "zMBossId", "ZMBossId" }),
                            zMBossStage = scalar_field(zm_boss_info, { "stage", "Stage" }),
                            zMBossNextStageTime = scalar_field(zm_boss_info, { "nextStageTime", "NextStageTime" }),
                            zMBossTransferEndTime = scalar_field(zm_boss_info, { "transferEndTime", "TransferEndTime" }),
                            zMBossShieldHp = scalar_field(zm_boss_info, { "shieldHp", "ShieldHp" }),
                            zMBossShieldMaxHp = scalar_field(zm_boss_info, { "shieldMaxHp", "ShieldMaxHp" }),
                            zMBossShieldEndTime = scalar_field(zm_boss_info, { "shieldEndTime", "ShieldEndTime" }),
                            zMBossFrenzyEndTime = scalar_field(zm_boss_info, { "frenzyEndTime", "FrenzyEndTime" }),
                            monsterProtectionEligible = protection_eligible,
                            monsterProtectionKnown = protection_known,
                            monsterProtectionActive = protection_active,
                            monsterProtectionEndTime = protection_end_time,
                            eventId = scalar_field(march, { "eventId", "EventId" }),
                            zombieRushId = integer_field(march, { "zombieRushId", "ZombieRushId" }) or 0,
                            zombieRushRound = integer_field(march, { "zombieRushRound", "ZombieRushRound" }) or 0,
                            isMonster = flag("IsMonster"), isBoss = flag("IsBoss"),
                            isActBerserkBoss = flag("IsActBerserkBoss"),
                            isOrdinaryBoss = flag("IsOrdinaryBoss"), isWanderMonster = flag("IsWanderMonster"),
                            isWanderBoss = flag("IsWanderBoss"), isZombieRushAltered = flag("IsZombieRushAltered"),
                            source = "WorldScene.MarchDataManager.GetAllMarchesByCS",
                        }
                    end
                end
            end
        end
    end
    return records, nil
end

local function point_tile_count(world, point_manager, target_x, target_y)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end
    local matched = 0
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex" })
        local tile = id and index_to_tile(world, id) or nil
        if tile ~= nil and tile.x == target_x and tile.y == target_y then matched = matched + 1 end
        return true
    end)
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return matched, nil
end

local function loaded_aoi_lookup(world, point_manager, block_size, block_count)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil end
    local result = {}
    each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex" })
        local tile = id and index_to_tile(world, id) or nil
        if tile ~= nil then
            local cell_x = math.floor(tile.x / block_size)
            local cell_y = math.floor(tile.y / block_size)
            if cell_x >= 0 and cell_y >= 0 and cell_x < block_count and cell_y < block_count then
                result[cell_y * block_count + cell_x] = true
            end
        end
        return true
    end)
    return result
end

local function choose_bulk_aoi_indices(world, point_manager, block_size, block_count, requested_count, target_tile_x, target_tile_y)
    local current_set = reflected_value(point_manager, "_curViewIndex")
    if current_set == nil then return nil, "cur_view_index_unavailable" end
    local current_tile = safe_get(world, "CurTilePosClamped")
    local tile_x = tonumber(current_tile and (safe_get(current_tile, "x") or safe_get(current_tile, "X")))
    local tile_y = tonumber(current_tile and (safe_get(current_tile, "y") or safe_get(current_tile, "Y")))
    if tile_x == nil or tile_y == nil then return nil, "current_tile_unavailable" end
    tile_x = math.floor(tile_x); tile_y = math.floor(tile_y)
    local target_x = math.max(0, math.min(block_count - 1, math.floor(target_tile_x / block_size)))
    local target_y = math.max(0, math.min(block_count - 1, math.floor(target_tile_y / block_size)))
    local target_index = target_y * block_count + target_x
    local loaded = loaded_aoi_lookup(world, point_manager, block_size, block_count) or {}
    if collection_contains_int(current_set, target_index) then return nil, "target_aoi_still_current" end
    if loaded[target_index] == true then return nil, "target_aoi_already_loaded" end
    local selected = { target_index }
    local selected_lookup = { [target_index] = true }
    for radius = 1, block_count do
        local min_x = math.max(0, target_x - radius)
        local max_x = math.min(block_count - 1, target_x + radius)
        local min_y = math.max(0, target_y - radius)
        local max_y = math.min(block_count - 1, target_y + radius)
        for y = min_y, max_y do
            for x = min_x, max_x do
                if math.max(math.abs(x - target_x), math.abs(y - target_y)) == radius then
                    local index = y * block_count + x
                    if selected_lookup[index] ~= true and not collection_contains_int(current_set, index) and loaded[index] ~= true then
                        selected[#selected + 1] = index
                        selected_lookup[index] = true
                        if #selected >= requested_count then
                            return selected, nil, tile_x, tile_y, target_index
                        end
                    end
                end
            end
        end
    end
    return nil, "insufficient_unloaded_aoi_indices"
end

local function point_store_state(world, point_manager, target_x, target_y)
    local vector_type = rawget(_G, "CS") and CS.UnityEngine and CS.UnityEngine.Vector2Int or nil
    if vector_type == nil then return nil end
    local ok_index, raw_index = call(world, "TilePosToIndex", vector_type(target_x, target_y))
    local point_index = ok_index and tonumber(raw_index) or nil
    if point_index == nil then return nil end
    point_index = math.floor(point_index)
    local function inspect(name)
        local collection = reflected_value(point_manager, name)
        if collection == nil then return nil, nil end
        local count = collection_count(collection)
        local ok_contains, contains = call(collection, "ContainsKey", point_index)
        if not ok_contains then ok_contains, contains = reflected_call(collection, "ContainsKey", point_index) end
        return count, ok_contains and contains == true or false
    end
    local all_count, all_has = inspect("allViewPoints")
    local out_count, out_has = inspect("outOfViewPoints")
    local out_obj_count, out_obj_has = inspect("outOfViewPointsObj")
    return {
        targetPointIndex = point_index, allViewPointsCount = all_count, allViewPointsHasTarget = all_has,
        outOfViewPointsCount = out_count, outOfViewPointsHasTarget = out_has,
        outOfViewPointsObjCount = out_obj_count, outOfViewPointsObjHasTarget = out_obj_has,
    }
end

local function bulk_manager_flags(point_manager)
    local function read(name)
        local value = reflected_value(point_manager, name)
        if type(value) == "boolean" then return value end
        local numeric = tonumber(value)
        if numeric ~= nil then return numeric end
        return nil
    end
    return {
        isRecvViewPoints = read("isRecvViewPoints"),
        isPointUpdate = read("_isPointUpdate"),
        isCityPointUpdate = read("_isCityPointUpdate"),
        myPointDirty = read("_myPointDirty"),
        littleSmartDirty = read("littleSmartDirty"),
        firstTimeReqAoi = read("firstTimeReqAoi"),
        splitLastAoiRequest = read("_splitLastAOIRequest"),
    }
end

local function profiler_packet_state(dictionary)
    if dictionary == nil then return { available = false } end
    local total = collection_count(dictionary) or 0
    local commands, counts = {}, {}
    local ok_enum, enumerator = call(dictionary, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        local seen = 0
        while seen < 4096 do
            local ok_move, moved = call(enumerator, "MoveNext")
            if not ok_move or moved ~= true then break end
            seen = seen + 1
            local pair = safe_get(enumerator, "Current")
            local packet = pair and safe_get(pair, "Key") or nil
            local info = packet and (safe_get(packet, "info") or reflected_value(packet, "<info>k__BackingField")) or nil
            local ok_cmd, cmd = info and call(info, "GetUtfString", "c") or false, nil
            if info ~= nil then ok_cmd, cmd = call(info, "GetUtfString", "c") end
            if ok_cmd and type(cmd) == "string" and cmd ~= "" then
                counts[cmd] = (counts[cmd] or 0) + 1
                if #commands < 32 and counts[cmd] == 1 then commands[#commands + 1] = cmd end
            end
        end
    end
    table.sort(commands)
    local rendered = {}
    for _, cmd in ipairs(commands) do rendered[#rendered + 1] = cmd .. ":" .. tostring(counts[cmd] or 0) end
    return { available = true, total = total, commands = table.concat(rendered, ";") }
end

local function bulk_network_receive_state()
    local cs = rawget(_G, "CS")
    local entry = rawget(_G, "GameEntry")
    if entry == nil and cs ~= nil then entry = safe_get(cs, "GameEntry") end
    local network = entry and safe_get(entry, "Network") or nil
    local proxy = network and reflected_value(network, "m_proxy") or nil
    local profiler = proxy and (safe_get(proxy, "profiler") or reflected_value(proxy, "<profiler>k__BackingField")) or nil
    if profiler == nil then return { available = false, sendAvailable = false } end
    local receive = profiler_packet_state(reflected_value(profiler, "_receiveData"))
    local send = profiler_packet_state(reflected_value(profiler, "_sendData"))
    receive.sendAvailable = send.available
    receive.sendTotal = send.total
    receive.sendCommands = send.commands
    return receive
end

local function bulk_selected_lookup(indices)
    local lookup = {}
    for _, index in ipairs(indices or {}) do lookup[index] = true end
    return lookup
end

local function restore_bulk_aoi_state(point_manager)
    local ok = true
    local current_set = point_manager and reflected_value(point_manager, "_curViewIndex") or nil
    if current_set ~= nil then
        for _, index in ipairs(bulk_aoi_added_indices or {}) do
            if not collection_remove_int(current_set, index) then ok = false end
        end
    elseif #(bulk_aoi_added_indices or {}) > 0 then
        ok = false
    end
    if point_manager ~= nil and bulk_aoi_original_start_view_request ~= nil then
        if not reflected_set_value(point_manager, "startViewRequest", bulk_aoi_original_start_view_request == true) then ok = false end
    end
    if point_manager ~= nil and bulk_aoi_block_count_touched then
        if not reflected_set_int_field(point_manager, "_lwAoiBlockCount", bulk_aoi_original_block_count or 0) then ok = false end
    end
    if bulk_aoi_restore_current_view and point_manager ~= nil then
        call(point_manager, "StartViewRequest")
        call(point_manager, "UpdateViewRequest", true)
    end
    bulk_aoi_restore_current_view = false
    if bulk_aoi_response_flags_reset and point_manager ~= nil then
        local world = runtime_world()
        if bulk_aoi_original_manager_response_flag ~= nil and
           not reflected_set_value(point_manager, "isRecvViewPoints", bulk_aoi_original_manager_response_flag == true) then ok = false end
        if world ~= nil and bulk_aoi_original_world_response_flag ~= nil and
           not reflected_set_value(world, "hasReceiveViewPointsReply", bulk_aoi_original_world_response_flag == true) then ok = false end
    end
    bulk_aoi_response_flags_reset = false
    bulk_aoi_original_manager_response_flag = nil
    bulk_aoi_original_world_response_flag = nil
    if bulk_aoi_native_touch_camera ~= nil and bulk_aoi_native_original_pos ~= nil then
        if not select(1, call(bulk_aoi_native_touch_camera, "SetCameraPos", bulk_aoi_native_original_pos)) then ok = false end
    end
    if bulk_aoi_native_camera ~= nil and bulk_aoi_native_original_fov ~= nil then
        if not select(1, call(bulk_aoi_native_camera, "SetFOV", bulk_aoi_native_original_fov)) then ok = false end
    end
    if bulk_aoi_native_camera ~= nil then
        if not select(1, call(bulk_aoi_native_camera, "RefreshCameraAnchor")) then ok = false end
        if point_manager ~= nil and not bulk_aoi_skip_restore_update then call(point_manager, "UpdateViewRequest", true) end
    end
    bulk_aoi_skip_restore_update = false
    bulk_aoi_native_camera = nil
    bulk_aoi_native_touch_camera = nil
    bulk_aoi_native_original_pos = nil
    bulk_aoi_native_original_fov = nil
    bulk_aoi_native_hold_seconds = nil
    bulk_aoi_native_position_restored = false
    bulk_aoi_added_indices = {}
    bulk_aoi_original_start_view_request = nil
    bulk_aoi_original_block_count = nil
    bulk_aoi_block_count_touched = false
    return ok
end

local function write_bulk_aoi_result(request, state, error_text, details)
    details = details or {}
    write_json(bulk_aoi_diagnostic_result_path, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = request.requestId,
        launchSessionId = request.launchSessionId,
        profileId = request.profileId,
        challenge = request.challenge,
        gamePid = request.gamePid,
        requestedCount = request.requestedCount,
        homeTileX = request.homeTileX,
        homeTileY = request.homeTileY,
        requestMode = request.requestMode,
        state = state,
        error = error_text,
        requestedIndices = details.requestedIndices,
        bigMap = details.bigMap,
        serverLod = details.serverLod,
        blockSize = details.blockSize,
        blockCount = details.blockCount,
        anchorTileX = details.anchorTileX,
        anchorTileY = details.anchorTileY,
        lbTileIndex = details.lbTileIndex,
        rtTileIndex = details.rtTileIndex,
        baselineMatchedCount = details.baselineMatchedCount,
        matchedCount = details.matchedCount,
        matchedCityCount = details.matchedCityCount,
        matchedResourceCount = details.matchedResourceCount,
        beforeLoadedPointCount = details.beforeLoadedPointCount,
        afterLoadedPointCount = details.afterLoadedPointCount,
        targetTileX = details.targetTileX,
        targetTileY = details.targetTileY,
        targetAoiIndex = details.targetAoiIndex,
        baselineTargetPointCount = details.baselineTargetPointCount,
        targetPointCount = details.targetPointCount,
        targetPointIndex = details.targetPointIndex,
        baselineAllViewPointsCount = details.baselineAllViewPointsCount,
        allViewPointsCount = details.allViewPointsCount,
        baselineAllViewPointsHasTarget = details.baselineAllViewPointsHasTarget,
        allViewPointsHasTarget = details.allViewPointsHasTarget,
        baselineOutOfViewPointsCount = details.baselineOutOfViewPointsCount,
        outOfViewPointsCount = details.outOfViewPointsCount,
        baselineOutOfViewPointsHasTarget = details.baselineOutOfViewPointsHasTarget,
        outOfViewPointsHasTarget = details.outOfViewPointsHasTarget,
        baselineOutOfViewPointsObjCount = details.baselineOutOfViewPointsObjCount,
        outOfViewPointsObjCount = details.outOfViewPointsObjCount,
        baselineOutOfViewPointsObjHasTarget = details.baselineOutOfViewPointsObjHasTarget,
        outOfViewPointsObjHasTarget = details.outOfViewPointsObjHasTarget,
        preTileX = details.preTileX,
        preTileY = details.preTileY,
        postTileX = details.postTileX,
        postTileY = details.postTileY,
        elapsedSeconds = details.elapsedSeconds,
        cameraTileStable = details.cameraTileStable,
        addListExists = details.addListExists,
        currentSetExists = details.currentSetExists,
        splitPending = details.splitPending,
        startViewType = details.startViewType,
        startViewValue = details.startViewValue,
        initializedViaNormalUpdate = details.initializedViaNormalUpdate,
        initializedViaStartViewRequest = details.initializedViaStartViewRequest,
        initInvokeError = details.initInvokeError,
        baselineIsRecvViewPoints = details.baselineIsRecvViewPoints,
        isRecvViewPoints = details.isRecvViewPoints,
        hasReceiveViewPointsReply = details.hasReceiveViewPointsReply,
        responseFlagsTransitioned = details.responseFlagsTransitioned,
        baselineIsPointUpdate = details.baselineIsPointUpdate,
        isPointUpdate = details.isPointUpdate,
        baselineIsCityPointUpdate = details.baselineIsCityPointUpdate,
        isCityPointUpdate = details.isCityPointUpdate,
        baselineMyPointDirty = details.baselineMyPointDirty,
        myPointDirty = details.myPointDirty,
        baselineLittleSmartDirty = details.baselineLittleSmartDirty,
        littleSmartDirty = details.littleSmartDirty,
        baselineFirstTimeReqAoi = details.baselineFirstTimeReqAoi,
        firstTimeReqAoi = details.firstTimeReqAoi,
        baselineNetReceiveAvailable = details.baselineNetReceiveAvailable,
        netReceiveAvailable = details.netReceiveAvailable,
        baselineNetReceiveCount = details.baselineNetReceiveCount,
        netReceiveCount = details.netReceiveCount,
        baselineNetReceiveCommands = details.baselineNetReceiveCommands,
        netReceiveCommands = details.netReceiveCommands,
        baselineNetSendAvailable = details.baselineNetSendAvailable,
        netSendAvailable = details.netSendAvailable,
        baselineNetSendCount = details.baselineNetSendCount,
        netSendCount = details.netSendCount,
        baselineNetSendCommands = details.baselineNetSendCommands,
        netSendCommands = details.netSendCommands,
        nativeRemoteTileX = details.nativeRemoteTileX,
        nativeRemoteTileY = details.nativeRemoteTileY,
        nativeCurrentSetCount = details.nativeCurrentSetCount,
        holdMilliseconds = details.holdMilliseconds,
        viewLevel = details.viewLevel,
        postServerLod = details.postServerLod,
        postBlockSize = details.postBlockSize,
        postBlockCount = details.postBlockCount,
        positionRestoredBeforeResponse = details.positionRestoredBeforeResponse,
        positionRestoreElapsedSeconds = details.positionRestoreElapsedSeconds,
        point_records = details.pointRecords,
        monster_march_records = details.monsterMarchRecords,
        train_march_records = details.trainMarchRecords,
        monsterInvasionBossCount = details.monsterInvasionBossCount or 0,
        monsterProtectionDetailTargetCount = details.monsterProtectionDetailTargetCount or 0,
        monsterProtectionDetailRequestCount = details.monsterProtectionDetailRequestCount or 0,
        monsterProtectionDetailReadyCount = details.monsterProtectionDetailReadyCount or 0,
        includeMonster = request.includeMonster == true,
        includeTrain = request.includeTrain == true,
        requestMethod = details.requestMethod or "WorldPointManager.SendAoiRequest(private-reflection)",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function fail_bulk_aoi(request, error_text, details, point_manager)
    local restored = restore_bulk_aoi_state(point_manager)
    if not restored then error_text = tostring(error_text) .. "; bulk_aoi_state_restore_failed" end
    write_bulk_aoi_result(request, "failed", error_text, details)
    bulk_aoi_request = nil
    bulk_aoi_started_at = nil
end

local function pump_bulk_aoi_diagnostic(now)
    if bulk_aoi_request == nil then
        local request = read_bulk_aoi_diagnostic(now)
        if request == nil then return false end
        if request.error ~= nil then
            write_bulk_aoi_result(request, "failed", request.error, nil)
            return true
        end
        local world, point_manager, world_error = runtime_world()
        if world == nil or point_manager == nil then
            write_bulk_aoi_result(request, "failed", world_error or "world_unavailable", nil)
            return true
        end
        local block_size = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
        local block_count = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
        local server_lod = integer_field(point_manager, { "svLod" })
        local start_view_before_init = reflected_value(point_manager, "startViewRequest")
        local initialized_via_start_view = false
        local initialized_via_normal_update = false
        if block_size ~= nil and block_size > 0 and block_count ~= nil and block_count <= 0 and
           server_lod ~= nil and server_lod >= 0 then
            local start_ok = select(1, call(point_manager, "StartViewRequest"))
            local update_ok = select(1, call(point_manager, "UpdateViewRequest", true))
            if not start_ok or not update_ok then
                write_bulk_aoi_result(request, "failed", "native_aoi_initialization_failed", {
                    blockSize = block_size, blockCount = block_count, serverLod = server_lod,
                    initInvokeError = not start_ok and "StartViewRequest failed" or "UpdateViewRequest(true) failed",
                })
                return true
            end
            initialized_via_start_view = true
            initialized_via_normal_update = true
            block_size = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
            block_count = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
            server_lod = integer_field(point_manager, { "svLod" })
        end
        if false and request.viewLevel >= 0 then
            local camera_manager = safe_get(world, "Camera") or reflected_value(world, "<Camera>k__BackingField")
            local original_manager_lod = integer_field(point_manager, { "LOD" })
            local original_camera_lod = camera_manager and tonumber(safe_get(camera_manager, "CurrentLodLevel")) or nil
            local desired_camera_lod = request.viewLevel == 0 and 1 or (request.viewLevel == 1 and 5 or 6)
            if camera_manager == nil or original_manager_lod == nil or original_camera_lod == nil then
                write_bulk_aoi_result(request, "failed", "native_lod_state_unavailable", nil)
                return true
            end
            bulk_aoi_original_manager_lod = original_manager_lod
            bulk_aoi_original_camera_lod = math.floor(original_camera_lod)
            local camera_lod_ok = select(1, reflected_call(camera_manager, "set_CurrentLodLevel", desired_camera_lod))
            local manager_lod_ok = select(1, reflected_call(point_manager, "OnUpdateLod", desired_camera_lod))
            local update_ok = select(1, call(point_manager, "UpdateViewRequest", true))
            if not camera_lod_ok or not manager_lod_ok or not update_ok then
                write_bulk_aoi_result(request, "failed", "native_lod_transition_failed", nil)
                return true
            end
            block_size = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
            block_count = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
            server_lod = integer_field(point_manager, { "svLod" })
        end
        local add_list = reflected_value(point_manager, "_addViewIndex")
        local current_set = reflected_value(point_manager, "_curViewIndex")
        local split_pending = reflected_value(point_manager, "_splitLastAOIRequest")
        local start_view = reflected_value(point_manager, "startViewRequest")
        if block_size == nil or block_size <= 0 or block_count == nil or block_count <= 0 or
           server_lod == nil or server_lod < 0 or add_list == nil or current_set == nil or
           type(start_view) ~= "boolean" then
            write_bulk_aoi_result(request, "failed", "bulk_aoi_geometry_or_state_unavailable", {
                blockSize = block_size, blockCount = block_count, serverLod = server_lod,
                addListExists = add_list ~= nil, currentSetExists = current_set ~= nil,
                splitPending = split_pending, startViewType = type(start_view), startViewValue = start_view,
            })
            return true
        end
        local add_count = collection_count(add_list)
        if split_pending == true then
            write_bulk_aoi_result(request, "failed", "bulk_aoi_manager_has_pending_split_request", {
                blockSize = block_size, blockCount = block_count, serverLod = server_lod,
            })
            return true
        end
        local indices, choose_error, pre_tile_x, pre_tile_y, target_aoi_index
        if request.requestMode == "coverage" then
            local current_tile = safe_get(world, "CurTilePosClamped")
            pre_tile_x = tonumber(current_tile and (safe_get(current_tile, "x") or safe_get(current_tile, "X")))
            pre_tile_y = tonumber(current_tile and (safe_get(current_tile, "y") or safe_get(current_tile, "Y")))
            if pre_tile_x == nil or pre_tile_y == nil then
                write_bulk_aoi_result(request, "failed", "current_tile_unavailable", nil)
                return true
            end
            pre_tile_x = math.floor(pre_tile_x); pre_tile_y = math.floor(pre_tile_y)
            local target_cell_x = math.max(0, math.min(block_count - 1, math.floor(request.targetTileX / block_size)))
            local target_cell_y = math.max(0, math.min(block_count - 1, math.floor(request.targetTileY / block_size)))
            target_aoi_index = target_cell_y * block_count + target_cell_x
            indices = { target_aoi_index }
        else
            indices, choose_error, pre_tile_x, pre_tile_y, target_aoi_index = choose_bulk_aoi_indices(
                world, point_manager, block_size, block_count, request.requestedCount,
                request.targetTileX, request.targetTileY)
            if indices == nil then
                write_bulk_aoi_result(request, "failed", choose_error, nil)
                return true
            end
        end
        local lookup = bulk_selected_lookup(indices)
        local baseline, baseline_error = point_aoi_counts(world, point_manager, block_size, block_count, lookup)
        if baseline == nil then
            write_bulk_aoi_result(request, "failed", baseline_error, nil)
            return true
        end
        local baseline_target, target_error = point_tile_count(
            world, point_manager, request.targetTileX, request.targetTileY)
        local baseline_stores = point_store_state(world, point_manager, request.targetTileX, request.targetTileY) or {}
        if baseline_target == nil then
            write_bulk_aoi_result(request, "failed", target_error, nil)
            return true
        end
        if baseline_target ~= 0 then
            write_bulk_aoi_result(request, "failed", "target_point_already_loaded", {
                blockSize = block_size, blockCount = block_count, targetAoiIndex = target_aoi_index,
                targetTileX = request.targetTileX, targetTileY = request.targetTileY,
                baselineTargetPointCount = baseline_target,
            targetPointIndex = baseline_stores.targetPointIndex,
            baselineAllViewPointsCount = baseline_stores.allViewPointsCount,
            baselineAllViewPointsHasTarget = baseline_stores.allViewPointsHasTarget,
            baselineOutOfViewPointsCount = baseline_stores.outOfViewPointsCount,
            baselineOutOfViewPointsHasTarget = baseline_stores.outOfViewPointsHasTarget,
            baselineOutOfViewPointsObjCount = baseline_stores.outOfViewPointsObjCount,
            baselineOutOfViewPointsObjHasTarget = baseline_stores.outOfViewPointsObjHasTarget,
            })
            return true
        end
        bulk_aoi_original_start_view_request = start_view_before_init
        bulk_aoi_added_indices = {}
        if request.viewLevel >= 90 then
            local cs = rawget(_G, "CS")
            local vector2_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
            if vector2_type == nil then
                fail_bulk_aoi(request, "direct_view_vector2int_unavailable", nil, point_manager)
                return true
            end
            local tile_pos = vector2_type(request.targetTileX, request.targetTileY)
            local baseline_flags = bulk_manager_flags(point_manager)
            local baseline_net = bulk_network_receive_state()
            local invoked = select(1, reflected_call(point_manager, "SendViewRequest",
                tile_pos, request.viewLevel, request.serverId))
            if not invoked then
                fail_bulk_aoi(request, "direct_send_view_request_reflection_failed", nil, point_manager)
                return true
            end
            bulk_aoi_restore_current_view = true
            request.details = {
                requestedIndices = { target_aoi_index },
                bigMap = 0, serverLod = request.viewLevel,
                blockSize = block_size, blockCount = block_count,
                anchorTileX = request.targetTileX, anchorTileY = request.targetTileY,
                baselineMatchedCount = baseline.total,
                beforeLoadedPointCount = baseline.loadedPointCount,
                targetTileX = request.targetTileX, targetTileY = request.targetTileY,
                targetAoiIndex = target_aoi_index,
                baselineTargetPointCount = baseline_target,
                targetPointIndex = baseline_stores.targetPointIndex,
                baselineAllViewPointsCount = baseline_stores.allViewPointsCount,
                baselineAllViewPointsHasTarget = baseline_stores.allViewPointsHasTarget,
                baselineOutOfViewPointsCount = baseline_stores.outOfViewPointsCount,
                baselineOutOfViewPointsHasTarget = baseline_stores.outOfViewPointsHasTarget,
                baselineOutOfViewPointsObjCount = baseline_stores.outOfViewPointsObjCount,
                baselineOutOfViewPointsObjHasTarget = baseline_stores.outOfViewPointsObjHasTarget,
                baselineIsRecvViewPoints = baseline_flags.isRecvViewPoints,
                baselineIsPointUpdate = baseline_flags.isPointUpdate,
                baselineIsCityPointUpdate = baseline_flags.isCityPointUpdate,
                baselineMyPointDirty = baseline_flags.myPointDirty,
                baselineLittleSmartDirty = baseline_flags.littleSmartDirty,
                baselineFirstTimeReqAoi = baseline_flags.firstTimeReqAoi,
                baselineNetReceiveAvailable = baseline_net.available,
                baselineNetReceiveCount = baseline_net.total,
                baselineNetReceiveCommands = baseline_net.commands,
                baselineNetSendAvailable = baseline_net.sendAvailable,
                baselineNetSendCount = baseline_net.sendTotal,
                baselineNetSendCommands = baseline_net.sendCommands,
                preTileX = pre_tile_x, preTileY = pre_tile_y,
                nativeRemoteTileX = request.targetTileX,
                nativeRemoteTileY = request.targetTileY,
                nativeCurrentSetCount = collection_count(current_set) or 0,
                holdMilliseconds = request.holdMilliseconds,
                viewLevel = request.viewLevel,
                initializedViaNormalUpdate = initialized_via_normal_update,
                initializedViaStartViewRequest = initialized_via_start_view,
                requestMethod = "WorldPointManager.SendViewRequest",
            }
            bulk_aoi_request = request
            bulk_aoi_started_at = runtime_clock()
            return true
        end
        local camera_manager = safe_get(world, "Camera") or reflected_value(world, "<Camera>k__BackingField")
        local touch_camera = camera_manager and (safe_get(camera_manager, "touchCamera") or reflected_value(camera_manager, "touchCamera")) or nil
        local ok_pos, original_pos = touch_camera and call(touch_camera, "GetCameraPos") or false, nil
        if touch_camera ~= nil then ok_pos, original_pos = call(touch_camera, "GetCameraPos") end
        local ox = tonumber(original_pos and safe_get(original_pos, "x"))
        local oy = tonumber(original_pos and safe_get(original_pos, "y"))
        local oz = tonumber(original_pos and safe_get(original_pos, "z"))
        local vector3_type = rawget(_G, "CS") and CS.UnityEngine and CS.UnityEngine.Vector3 or nil
        if camera_manager == nil or touch_camera == nil or not ok_pos or ox == nil or oy == nil or oz == nil or vector3_type == nil then
            fail_bulk_aoi(request, "native_camera_state_unavailable", nil, point_manager)
            return true
        end
        local shift_x = (request.targetTileX - pre_tile_x) * 2
        local shift_z = (request.targetTileY - pre_tile_y) * 2
        local remote_pos = vector3_type(ox + shift_x, oy, oz + shift_z)
        local baseline_flags = bulk_manager_flags(point_manager)
        local baseline_net = bulk_network_receive_state()
        if not select(1, call(touch_camera, "SetCameraPos", remote_pos)) or
           not select(1, call(camera_manager, "RefreshCameraAnchor")) then
            call(touch_camera, "SetCameraPos", original_pos)
            fail_bulk_aoi(request, "native_camera_shift_failed", nil, point_manager)
            return true
        end
        local remote_tile = safe_get(world, "CurTilePosClamped")
        local remote_tile_x = tonumber(remote_tile and (safe_get(remote_tile, "x") or safe_get(remote_tile, "X")))
        local remote_tile_y = tonumber(remote_tile and (safe_get(remote_tile, "y") or safe_get(remote_tile, "Y")))
        bulk_aoi_native_camera = camera_manager
        bulk_aoi_native_touch_camera = touch_camera
        bulk_aoi_native_original_pos = original_pos
        bulk_aoi_native_hold_seconds = request.holdMilliseconds / 1000.0
        bulk_aoi_native_position_restored = false
        bulk_aoi_skip_restore_update = request.requestMode == "coverage"
        local expanded_anchor_cells = nil
        if request.requestMode == "expanded" or request.requestMode == "coverage" then
            local unity_camera = safe_get(camera_manager, "__camera") or reflected_value(camera_manager, "camera")
            local original_fov = tonumber(unity_camera and safe_get(unity_camera, "fieldOfView"))
            if original_fov == nil or original_fov <= 0 then
                fail_bulk_aoi(request, "expanded_camera_fov_unavailable", nil, point_manager)
                return true
            end
            bulk_aoi_native_original_fov = original_fov
            if not select(1, call(camera_manager, "SetFOV", 120.0)) then
                fail_bulk_aoi(request, "expanded_camera_fov_set_failed", nil, point_manager)
                return true
            end
        end
        local manager_reply_before = manager_response_flag(point_manager)
        local world_reply_before = world_response_flag(world)
        if manager_reply_before == nil or world_reply_before == nil then
            fail_bulk_aoi(request, "bulk_response_flags_unavailable", nil, point_manager)
            return true
        end
        bulk_aoi_original_manager_response_flag = manager_reply_before
        bulk_aoi_original_world_response_flag = world_reply_before
        if not reflected_set_value(point_manager, "isRecvViewPoints", false) or
           not reflected_set_value(world, "hasReceiveViewPointsReply", false) then
            fail_bulk_aoi(request, "bulk_response_flag_reset_failed", nil, point_manager)
            return true
        end
        bulk_aoi_response_flags_reset = true
        local request_method = "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift"
        local invoked = false
        local requested_indices = indices
        local direct_lb_index, direct_rt_index = nil, nil
        if request.viewLevel >= 0 then
            local cs = rawget(_G, "CS")
            local vector2_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
            local tile_pos = vector2_type and vector2_type(request.targetTileX, request.targetTileY) or nil
            invoked = tile_pos ~= nil and select(1, reflected_call(point_manager, "SendViewRequest", tile_pos, request.viewLevel, request.serverId))
            request_method = "WorldPointManager.SendViewRequest+held-internal-camera-shift"
        elseif request.requestMode == "direct" then
            bulk_aoi_added_indices = {}
            if not collection_clear(add_list) then
                fail_bulk_aoi(request, "direct_add_list_clear_failed", nil, point_manager)
                return true
            end
            local min_x, min_y, max_x, max_y = block_count, block_count, 0, 0
            for _, index in ipairs(indices) do
                if not collection_add_int(add_list, index) then
                    fail_bulk_aoi(request, "direct_add_list_population_failed", nil, point_manager)
                    return true
                end
                if not collection_contains_int(current_set, index) then
                    if not collection_add_int(current_set, index) then
                        fail_bulk_aoi(request, "direct_current_set_population_failed", nil, point_manager)
                        return true
                    end
                    bulk_aoi_added_indices[#bulk_aoi_added_indices + 1] = index
                end
                local cell_x = index % block_count
                local cell_y = math.floor(index / block_count)
                min_x = math.min(min_x, cell_x); min_y = math.min(min_y, cell_y)
                max_x = math.max(max_x, cell_x); max_y = math.max(max_y, cell_y)
            end
            local cs = rawget(_G, "CS")
            local vector2_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
            local lb = vector2_type and vector2_type(min_x * block_size, min_y * block_size) or nil
            local rt = vector2_type and vector2_type((max_x + 1) * block_size, (max_y + 1) * block_size) or nil
            local ok_lb, lb_index = lb and call(world, "TilePosToIndex", lb) or false, nil
            if lb ~= nil then ok_lb, lb_index = call(world, "TilePosToIndex", lb) end
            local ok_rt, rt_index = rt and call(world, "TilePosToIndex", rt) or false, nil
            if rt ~= nil then ok_rt, rt_index = call(world, "TilePosToIndex", rt) end
            if not ok_lb or not ok_rt or tonumber(lb_index) == nil or tonumber(rt_index) == nil then
                fail_bulk_aoi(request, "direct_tile_bounds_unavailable", nil, point_manager)
                return true
            end
            direct_lb_index = math.floor(tonumber(lb_index)); direct_rt_index = math.floor(tonumber(rt_index))
            invoked = select(1, reflected_call(point_manager, "SendAoiRequest", 0, server_lod,
                request.targetTileX, request.targetTileY, add_list, block_size, direct_lb_index, direct_rt_index))
            request_method = "WorldPointManager.SendAoiRequest(private-reflection)+held-internal-camera-shift"
        else
            invoked = select(1, call(point_manager, "UpdateViewRequest", true))
        end
        if not invoked then
            call(camera_manager, "RefreshCameraAnchor")
            fail_bulk_aoi(request, "native_remote_request_failed", nil, point_manager)
            return true
        end
        -- LWB-R7-014 IMPLEMENTATION POLICY: production coverage uses a zero hold.
        -- Restore the visible camera transform in the same Lua callback that queues the
        -- remote request, instead of waiting for the next 250 ms pump/frame.
        if request.holdMilliseconds == 0 and bulk_aoi_native_touch_camera ~= nil and
           bulk_aoi_native_original_pos ~= nil then
            if not select(1, call(bulk_aoi_native_touch_camera, "SetCameraPos", bulk_aoi_native_original_pos)) then
                fail_bulk_aoi(request, "native_camera_same_tick_restore_failed", nil, point_manager)
                return true
            end
            if bulk_aoi_native_camera ~= nil and bulk_aoi_native_original_fov ~= nil and
               not select(1, call(bulk_aoi_native_camera, "SetFOV", bulk_aoi_native_original_fov)) then
                fail_bulk_aoi(request, "native_camera_fov_same_tick_restore_failed", nil, point_manager)
                return true
            end
            bulk_aoi_native_position_restored = true
            bulk_aoi_native_hold_seconds = nil
            if request_method == "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift" then
                request_method = "WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore"
            elseif request_method == "WorldPointManager.SendViewRequest+held-internal-camera-shift" then
                request_method = "WorldPointManager.SendViewRequest+same-tick-camera-restore"
            elseif request_method == "WorldPointManager.SendAoiRequest(private-reflection)+held-internal-camera-shift" then
                request_method = "WorldPointManager.SendAoiRequest(private-reflection)+same-tick-camera-restore"
            end
        end
        local native_current = reflected_value(point_manager, "_curViewIndex")
        local native_indices = native_current and select(1, collection_int_values(native_current, 512)) or nil
        if request.requestMode ~= "direct" then
            if native_indices == nil or #native_indices == 0 then native_indices = { target_aoi_index } end
            requested_indices = native_indices
        end
        request.details = {
            requestedIndices = requested_indices,
            bigMap = 0, serverLod = server_lod, blockSize = block_size, blockCount = block_count,
            anchorTileX = request.targetTileX, anchorTileY = request.targetTileY,
            lbTileIndex = direct_lb_index, rtTileIndex = direct_rt_index,
            baselineMatchedCount = baseline.total, beforeLoadedPointCount = baseline.loadedPointCount,
            targetTileX = request.targetTileX, targetTileY = request.targetTileY,
            targetAoiIndex = target_aoi_index, baselineTargetPointCount = baseline_target,
            targetPointIndex = baseline_stores.targetPointIndex,
            baselineAllViewPointsCount = baseline_stores.allViewPointsCount,
            baselineAllViewPointsHasTarget = baseline_stores.allViewPointsHasTarget,
            baselineOutOfViewPointsCount = baseline_stores.outOfViewPointsCount,
            baselineOutOfViewPointsHasTarget = baseline_stores.outOfViewPointsHasTarget,
            baselineOutOfViewPointsObjCount = baseline_stores.outOfViewPointsObjCount,
            baselineOutOfViewPointsObjHasTarget = baseline_stores.outOfViewPointsObjHasTarget,
            baselineIsRecvViewPoints = baseline_flags.isRecvViewPoints,
            baselineIsPointUpdate = baseline_flags.isPointUpdate,
            baselineIsCityPointUpdate = baseline_flags.isCityPointUpdate,
            baselineMyPointDirty = baseline_flags.myPointDirty,
            baselineLittleSmartDirty = baseline_flags.littleSmartDirty,
            baselineFirstTimeReqAoi = baseline_flags.firstTimeReqAoi,
            baselineNetReceiveAvailable = baseline_net.available,
            baselineNetReceiveCount = baseline_net.total,
            baselineNetReceiveCommands = baseline_net.commands,
            baselineNetSendAvailable = baseline_net.sendAvailable,
            baselineNetSendCount = baseline_net.sendTotal,
            baselineNetSendCommands = baseline_net.sendCommands,
            preTileX = pre_tile_x, preTileY = pre_tile_y,
            nativeRemoteTileX = remote_tile_x, nativeRemoteTileY = remote_tile_y,
            nativeCurrentSetCount = native_indices and #native_indices or (collection_count(current_set) or 0),
            holdMilliseconds = request.holdMilliseconds,
            viewLevel = request.viewLevel,
            initializedViaNormalUpdate = initialized_via_normal_update,
            initializedViaStartViewRequest = initialized_via_start_view,
            requestMethod = request_method,
        }
        bulk_aoi_request = request
        bulk_aoi_started_at = runtime_clock()
        return true
    end

    local world, point_manager, world_error = runtime_world()
    if world == nil or point_manager == nil then
        fail_bulk_aoi(bulk_aoi_request, world_error or "world_unavailable", bulk_aoi_request.details, point_manager)
        return true
    end
    local details = bulk_aoi_request.details or {}
    local elapsed_now = bulk_aoi_started_at and (runtime_clock() - bulk_aoi_started_at) or 0
    if not bulk_aoi_native_position_restored and bulk_aoi_native_hold_seconds ~= nil and
       elapsed_now >= bulk_aoi_native_hold_seconds and bulk_aoi_native_touch_camera ~= nil and
       bulk_aoi_native_original_pos ~= nil then
        if not select(1, call(bulk_aoi_native_touch_camera, "SetCameraPos", bulk_aoi_native_original_pos)) then
            fail_bulk_aoi(bulk_aoi_request, "native_camera_timed_restore_failed", details, point_manager)
            return true
        end
        if bulk_aoi_native_camera ~= nil and bulk_aoi_native_original_fov ~= nil then
            if not select(1, call(bulk_aoi_native_camera, "SetFOV", bulk_aoi_native_original_fov)) then
                fail_bulk_aoi(bulk_aoi_request, "native_camera_fov_timed_restore_failed", details, point_manager)
                return true
            end
        end
        bulk_aoi_native_position_restored = true
        details.positionRestoreElapsedSeconds = elapsed_now
    end
    details.positionRestoredBeforeResponse = bulk_aoi_native_position_restored == true
    local lookup = bulk_selected_lookup(details.requestedIndices)
    local observed, observe_error = point_aoi_counts(
        world, point_manager, details.blockSize, details.blockCount, lookup)
    if observed == nil then
        fail_bulk_aoi(bulk_aoi_request, observe_error, details, point_manager)
        return true
    end
    local post_tile = safe_get(world, "CurTilePosClamped")
    details.postTileX = tonumber(post_tile and (safe_get(post_tile, "x") or safe_get(post_tile, "X")))
    details.postTileY = tonumber(post_tile and (safe_get(post_tile, "y") or safe_get(post_tile, "Y")))
    details.matchedCount = observed.total
    details.matchedCityCount = observed.cities
    details.matchedResourceCount = observed.resources
    details.afterLoadedPointCount = observed.loadedPointCount
    local point_records, point_records_error = city_aoi_records(
        world, point_manager, details.blockSize, details.blockCount, lookup)
    if point_records == nil then
        fail_bulk_aoi(bulk_aoi_request, point_records_error, details, point_manager)
        return true
    end
    local resource_records, resource_records_error = resource_aoi_records(
        world, point_manager, details.blockSize, details.blockCount, lookup)
    if resource_records == nil then
        fail_bulk_aoi(bulk_aoi_request, resource_records_error, details, point_manager)
        return true
    end
    for index = 1, #resource_records do point_records[#point_records + 1] = resource_records[index] end
    details.pointRecords = point_records
    details.trainMarchRecords = {}
    if bulk_aoi_request.includeTrain == true then
        local train_records, train_records_error = train_march_aoi_records(
            world, details.blockSize, details.blockCount, lookup)
        if train_records == nil then
            fail_bulk_aoi(bulk_aoi_request, train_records_error, details, point_manager)
            return true
        end
        details.trainMarchRecords = train_records
    end
    details.monsterMarchRecords = {}
    if bulk_aoi_request.includeMonster == true then
        if bulk_aoi_request.requestMode == "coverage" then
            local response_flags = bulk_manager_flags(point_manager)
            if response_flags.isRecvViewPoints ~= true or world_response_flag(world) ~= true then return true end
        end
        -- Reproduce the original panel request while each boss is still loaded,
        -- without blocking the Lua acquisition pump.
        local targets, total, target_error = monster_invasion_protection_targets(
            world, details.blockSize, details.blockCount, lookup, bulk_aoi_request.serverId)
        if targets == nil then fail_bulk_aoi(bulk_aoi_request, target_error, details, point_manager); return true end
        local queued, queue_error = queue_monster_invasion_protection_requests(bulk_aoi_request, targets)
        details.monsterInvasionBossCount = total
        details.monsterProtectionDetailTargetCount = #targets
        details.monsterProtectionDetailRequestCount = queued or 0
        details.monsterProtectionDetailReadyCount = count_ready_monster_invasion_protection_details(targets)
        details.monsterProtectionDetailError = queue_error
        local home_tile = bulk_aoi_request.homeTileX >= 0 and
            { x = bulk_aoi_request.homeTileX, y = bulk_aoi_request.homeTileY } or nil
        local monster_march_records, monster_march_records_error = monster_march_aoi_records(
            world, details.blockSize, details.blockCount, lookup, home_tile)
        if monster_march_records == nil then
            fail_bulk_aoi(bulk_aoi_request, monster_march_records_error, details, point_manager)
            return true
        end
        details.monsterMarchRecords = monster_march_records
    end
    local target_count, target_count_error = point_tile_count(
        world, point_manager, details.targetTileX, details.targetTileY)
    if target_count == nil then
        fail_bulk_aoi(bulk_aoi_request, target_count_error, details, point_manager)
        return true
    end
    details.targetPointCount = target_count
    local post_stores = point_store_state(world, point_manager, details.targetTileX, details.targetTileY) or {}
    details.allViewPointsCount = post_stores.allViewPointsCount
    details.allViewPointsHasTarget = post_stores.allViewPointsHasTarget
    details.outOfViewPointsCount = post_stores.outOfViewPointsCount
    details.outOfViewPointsHasTarget = post_stores.outOfViewPointsHasTarget
    details.outOfViewPointsObjCount = post_stores.outOfViewPointsObjCount
    details.outOfViewPointsObjHasTarget = post_stores.outOfViewPointsObjHasTarget
    local post_flags = bulk_manager_flags(point_manager)
    details.isRecvViewPoints = post_flags.isRecvViewPoints
    details.hasReceiveViewPointsReply = world_response_flag(world)
    details.responseFlagsTransitioned = details.isRecvViewPoints == true and details.hasReceiveViewPointsReply == true
    details.isPointUpdate = post_flags.isPointUpdate
    details.isCityPointUpdate = post_flags.isCityPointUpdate
    details.myPointDirty = post_flags.myPointDirty
    details.littleSmartDirty = post_flags.littleSmartDirty
    details.firstTimeReqAoi = post_flags.firstTimeReqAoi
    details.postServerLod = integer_field(point_manager, { "svLod" })
    details.postBlockSize = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
    details.postBlockCount = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
    local post_net = bulk_network_receive_state()
    details.netReceiveAvailable = post_net.available
    details.netReceiveCount = post_net.total
    details.netReceiveCommands = post_net.commands
    details.netSendAvailable = post_net.sendAvailable
    details.netSendCount = post_net.sendTotal
    details.netSendCommands = post_net.sendCommands
    details.elapsedSeconds = bulk_aoi_started_at and (runtime_clock() - bulk_aoi_started_at) or nil
    details.cameraTileStable = details.postTileX == details.preTileX and details.postTileY == details.preTileY
    local request_completed = details.targetPointCount > (details.baselineTargetPointCount or 0)
    if bulk_aoi_request.requestMode == "coverage" then
        request_completed = details.responseFlagsTransitioned == true
    end
    if request_completed then
        local request = bulk_aoi_request
        local restored = restore_bulk_aoi_state(point_manager)
        if not restored then
            write_bulk_aoi_result(request, "failed", "bulk_aoi_state_restore_failed", details)
        elseif details.cameraTileStable ~= true and details.requestMethod ~= "WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore" then
            write_bulk_aoi_result(request, "failed", "camera_tile_changed_during_bulk_aoi_request", details)
        else
            write_bulk_aoi_result(request, "proven", nil, details)
        end
        bulk_aoi_request = nil
        bulk_aoi_started_at = nil
        return true
    end
    if bulk_aoi_started_at ~= nil and runtime_clock() - bulk_aoi_started_at >= BULK_AOI_TIMEOUT_SECONDS then
        fail_bulk_aoi(bulk_aoi_request, "bulk_aoi_response_timeout", details, point_manager)
    end
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
        if pump_monster_protection_detail(now) then
            write_heartbeat(now)
            return true
        end
        if pump_monster_protection_batch_retry() then
            write_heartbeat(now)
            return true
        end
        if pump_bulk_aoi_diagnostic(now) then
            write_heartbeat(now)
            return true
        end
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
            local current_view_set = reflected_value(point_manager, "_curViewIndex")
            local current_view_indices, current_view_method = collection_int_values(current_view_set, 20000)
            local current_view_count = collection_count(current_view_set)
            local live_aoi_block_size = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
            local live_aoi_block_count = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
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
                lwAoiBlockSize = live_aoi_block_size,
                lwAoiBlockCount = live_aoi_block_count,
                curViewIndexCount = current_view_count,
                curViewIndices = current_view_indices,
                curViewIndexMethod = current_view_method,
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
