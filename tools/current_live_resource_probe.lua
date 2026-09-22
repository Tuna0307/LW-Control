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
local resource_detail_diagnostic_path = root .. [[\resource-detail-diagnostic.txt]]
local resource_detail_diagnostic_result_path = root .. [[\resource-detail-diagnostic-result.json]]
local resource_scan_detail_path = root .. [[\resource-scan-detail.txt]]
local resource_scan_detail_result_path = root .. [[\resource-scan-detail-result.json]]
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
local resource_detail_request = nil
local resource_detail_started_at = nil
local resource_detail_transition_requested = false
local resource_detail_refresh_requested = false
local resource_scan_detail_runtime = { state = nil, request = nil, startedAt = nil }
local train_list_runtime = {
    path = root .. [[\train-list-diagnostic.txt]],
    resultPath = root .. [[\train-list-diagnostic-result.json]],
    request = nil, startedAt = nil, manager = nil, eventManager = nil, eventId = nil,
    listener = nil, refreshObserved = false, refreshArgument = nil,
}
local asset_image_runtime = {
    path = root .. [[\asset-image.txt]],
    resultPath = root .. [[\asset-image-result.json]],
    request = nil,
    startedAt = nil,
    timeoutSeconds = 12,
}
M._treasureStateRuntime = {
    path = root .. [[\treasure-state.txt]],
    resultPath = root .. [[\treasure-state-result.json]],
    request = nil,
    startedAt = nil,
    timeoutSeconds = 6,
    detailWaitSeconds = 2,
}
local bulk_aoi_original_start_view_request = nil
local bulk_aoi_original_block_count = nil
local bulk_aoi_block_count_touched = false
local bulk_aoi_added_indices = {}
local bulk_aoi_native_camera = nil
local bulk_aoi_native_touch_camera = nil
local bulk_aoi_native_original_pos = nil
local bulk_aoi_native_original_fov = nil
local bulk_aoi_native_unity_camera = nil
local bulk_aoi_native_original_aspect = nil
local bulk_aoi_native_original_zoom = nil
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
local MONSTER_INVASION_PROTECTION_MIN_TIMEOUT_SECONDS = 10
local MONSTER_INVASION_PROTECTION_MAX_TIMEOUT_SECONDS = 30
local MONSTER_INVASION_PROTECTION_PER_TARGET_SECONDS = 0.32
local MONSTER_INVASION_PROTECTION_TIMEOUT_PADDING_SECONDS = 4
-- RECOVERED current-v18 constraint: MonsterInvasionBossDetailMessge stores one
-- module-level request UUID, so protection detail requests are serialized.
-- One dropped reply must not pin the shared UUID slot and starve the tail of the
-- queue. Give each UUID a short bounded wait and one retry before advancing.
local MONSTER_INVASION_PROTECTION_ITEM_TIMEOUT_SECONDS = 0.45
local MONSTER_INVASION_PROTECTION_MAX_RETRIES = 1
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

local function bounded_enumerator_completion_error(collection, enumerator, scanned, limit, prefix)
    local expected = collection_count(collection)
    if expected ~= nil then
        if expected < 0 or expected > limit then return prefix .. "_count_outside_bounded_limit" end
        if scanned ~= expected then return prefix .. "_enumeration_mismatch" end
        return nil
    end
    if scanned < limit then return nil end
    local ok_move, moved = call(enumerator, "MoveNext")
    if not ok_move then return prefix .. "_enumerator_failed" end
    if moved == true then return prefix .. "_count_outside_bounded_limit" end
    return nil
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

local function player_city_health_snapshot(info)
    local function server_seconds()
        local manager_type = rawget(_G, "UITimeManager")
        local ok_instance, manager = call(manager_type, "GetInstance")
        if not ok_instance or manager == nil then return nil end
        local ok_seconds, seconds = call(manager, "GetServerSeconds")
        seconds = ok_seconds and tonumber(seconds) or nil
        if seconds ~= nil and seconds >= 0 then return seconds end
        local ok_time, millis = call(manager, "GetServerTime")
        millis = ok_time and tonumber(millis) or nil
        if millis ~= nil and millis >= 0 then return millis / 1000 end
        return nil
    end

    local function recover_speed(observed)
        local speed = tonumber(observed)
        if speed ~= nil and speed > 0 then return speed end
        local data_center = rawget(_G, "DataCenter")
        local manager = data_center and safe_get(data_center, "BuildTemplateManager") or nil
        local item_id = integer_field(info, { "itemId", "ItemId" })
        local level = integer_field(info, { "level", "Level" })
        if manager ~= nil and item_id ~= nil and level ~= nil then
            local ok_template, template = call(manager, "GetBuildingLevelTemplate", item_id, level)
            if ok_template and template ~= nil then
                local ok_cover, cover = call(template, "GetDefenceWallCoverSpeed")
                cover = ok_cover and tonumber(cover) or nil
                if cover ~= nil and cover > 0 then return cover end
            end
        end
        return speed
    end

    local function fire_speed(observed)
        local speed = tonumber(observed)
        if speed ~= nil and speed ~= 0 then return speed end
        local get_table_data = rawget(_G, "GetTableData")
        local table_name = rawget(_G, "TableName")
        local status_tab = table_name and safe_get(table_name, "StatusTab") or nil
        if type(get_table_data) == "function" and status_tab ~= nil then
            local ok, value = pcall(get_table_data, status_tab, 500300, "effect_num")
            value = ok and tonumber(value) or nil
            if value ~= nil then return value end
        end
        return speed
    end

    local raw_hp = tonumber(scalar_field(info, { "curHp", "CurHp" }))
    local last_hp_time = tonumber(scalar_field(info, { "lastHpTime", "LastHpTime" }))
    local observed_recover_speed = recover_speed(
        scalar_field(info, { "recoverSpeed", "RecoverSpeed" }))
    local unavailable_time = tonumber(scalar_field(info, { "unavailableTime", "UnavailableTime" }))
    local observed_fire_speed = tonumber(scalar_field(info, { "fireSpeed", "FireSpeed" }))
    local item_id = integer_field(info, { "itemId", "ItemId" })
    local result = {
        effectiveHp = raw_hp,
        rawCurHp = raw_hp,
        maxHp = nil,
        lastHpTime = last_hp_time,
        recoverSpeed = observed_recover_speed,
        unavailableTime = unavailable_time,
        fireSpeed = observed_fire_speed,
        calculationMode = "source_curHp",
    }
    if raw_hp == nil then return result end

    local ok_normal, is_normal = call(info, "IsNormalType")
    local building_types = rawget(_G, "BuildingTypes")
    local main_build_id = building_types and tonumber(safe_get(building_types, "FUN_BUILD_MAIN")) or nil
    if not ok_normal or is_normal ~= true or item_id == nil or main_build_id == nil or
       item_id ~= math.floor(main_build_id) then
        return result
    end

    local max_hp = 10000
    result.maxHp = max_hp
    local now = server_seconds()
    if now == nil or last_hp_time == nil then
        result.effectiveHp = math.floor(math.max(0, math.min(raw_hp, max_hp)))
        result.calculationMode = "normal_main_clamped_raw"
        return result
    end

    local hp = raw_hp
    local delta_time = now - last_hp_time
    if delta_time > 0 and observed_recover_speed ~= nil and observed_recover_speed > 0 then
        if unavailable_time ~= nil and unavailable_time ~= 0 then
            local fire_end = unavailable_time / 1000
            local max_fire_time = fire_end - last_hp_time
            local fire_time = math.min(math.max(max_fire_time, 0), delta_time)
            local normal_time = delta_time - fire_time
            if fire_time > 0 then
                local effective_fire_speed = fire_speed(observed_fire_speed)
                if effective_fire_speed == nil then
                    result.effectiveHp = math.floor(math.max(0, math.min(raw_hp, max_hp)))
                    result.calculationMode = "normal_main_fire_source_incomplete"
                    return result
                end
                result.fireSpeed = effective_fire_speed
                hp = math.max(hp - fire_time * effective_fire_speed, 1)
            end
            if normal_time > 0 then hp = hp + normal_time * observed_recover_speed end
            result.calculationMode = "normal_main_fire_then_recover"
        else
            hp = hp + delta_time * observed_recover_speed
            result.calculationMode = "normal_main_recover"
        end
    else
        result.calculationMode = "normal_main_no_elapsed_recovery"
    end
    result.effectiveHp = math.floor(math.max(0, math.min(hp, max_hp)))
    return result
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
        local health = player_city_health_snapshot(info)
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
            health = health.effectiveHp,
            healthRawCurHp = health.rawCurHp,
            healthMaxHp = health.maxHp,
            healthLastHpTime = health.lastHpTime,
            healthRecoverSpeed = health.recoverSpeed,
            healthUnavailableTime = health.unavailableTime,
            healthFireSpeed = health.fireSpeed,
            healthCalculationMode = health.calculationMode,
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

function asset_image_runtime.read_request(now)
    local values = read_kv_file(asset_image_runtime.path, 8192)
    if values == nil then return nil end
    pcall(os.remove, asset_image_runtime.path)
    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "asset_image_request_invalid"
        return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    request.sourceMode = tostring(values.sourceMode or "")
    request.assetPath = tostring(values.assetPath or "")
    request.spriteName = tostring(values.spriteName or "")
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or
       (request.sourceMode ~= "assetPath" and request.sourceMode ~= "spriteName") then
        request.error = "asset_image_request_invalid"
        return request
    end
    local source_value = request.sourceMode == "assetPath" and request.assetPath or request.spriteName
    local other_value = request.sourceMode == "assetPath" and request.spriteName or request.assetPath
    if #source_value < 1 or #source_value > 1024 or #other_value ~= 0 or
       string.find(source_value, "[%z\r\n]") ~= nil then
        request.error = "asset_image_request_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    request.sourceValue = source_value
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "asset_image_identity_mismatch"
    end
    return request
end

function asset_image_runtime.destroy_object(value)
    if value == nil then return end
    local cs = rawget(_G, "CS")
    local object_type = cs and cs.UnityEngine and cs.UnityEngine.Object or nil
    if object_type ~= nil then
        pcall(function() object_type.Destroy(value) end)
    end
end

function asset_image_runtime.render_sprite_png(sprite, sprite_renderer)
    local cs = rawget(_G, "CS")
    if cs == nil or cs.UnityEngine == nil or cs.System == nil then
        return nil, "asset_render_api_unavailable"
    end
    local render_texture_type = cs.UnityEngine.RenderTexture
    local render_texture_format = cs.UnityEngine.RenderTextureFormat
    local texture2d_type = cs.UnityEngine.Texture2D
    local texture_format = cs.UnityEngine.TextureFormat
    local image_conversion = cs.UnityEngine.ImageConversion
    local graphics = cs.UnityEngine.Graphics
    local vector2_type = cs.UnityEngine.Vector2
    local rect_type = cs.UnityEngine.Rect
    local convert_type = cs.System.Convert
    if render_texture_type == nil or render_texture_format == nil or
       texture2d_type == nil or texture_format == nil or image_conversion == nil or
       graphics == nil or vector2_type == nil or rect_type == nil or convert_type == nil then
        return nil, "asset_render_api_unavailable"
    end

    local texture = safe_get(sprite, "texture")
    local texture_rect = safe_get(sprite, "textureRect")
    local pixels_per_unit = tonumber(safe_get(sprite, "pixelsPerUnit"))
    local texture_width = tonumber(texture and safe_get(texture, "width"))
    local texture_height = tonumber(texture and safe_get(texture, "height"))
    local x = tonumber(texture_rect and (safe_get(texture_rect, "x") or safe_get(texture_rect, "X")))
    local y = tonumber(texture_rect and (safe_get(texture_rect, "y") or safe_get(texture_rect, "Y")))
    local width = tonumber(texture_rect and (safe_get(texture_rect, "width") or safe_get(texture_rect, "Width")))
    local height = tonumber(texture_rect and (safe_get(texture_rect, "height") or safe_get(texture_rect, "Height")))
    if texture == nil or texture_width == nil or texture_height == nil or
       x == nil or y == nil or width == nil or height == nil or
       texture_width <= 0 or texture_height <= 0 then
        return nil, "asset_sprite_texture_rect_unavailable"
    end
    width = math.floor(width + 0.5)
    height = math.floor(height + 0.5)
    if width < 1 or height < 1 or width > 4096 or height > 4096 then
        return nil, "asset_sprite_dimensions_invalid"
    end

    local packed = safe_get(sprite, "packed") == true
    local packing_rotation = tostring(safe_get(sprite, "packingRotation") or "")
    local scale_x = width / texture_width
    local scale_y = height / texture_height
    local offset_x = x / texture_width
    local offset_y = y / texture_height
    if string.find(packing_rotation, "FlipHorizontal", 1, true) ~= nil then
        scale_x = -scale_x
        offset_x = (x + width) / texture_width
    elseif string.find(packing_rotation, "FlipVertical", 1, true) ~= nil then
        scale_y = -scale_y
        offset_y = (y + height) / texture_height
    elseif string.find(packing_rotation, "Rotate180", 1, true) ~= nil then
        scale_x = -scale_x
        scale_y = -scale_y
        offset_x = (x + width) / texture_width
        offset_y = (y + height) / texture_height
    elseif packed and string.find(packing_rotation, "None", 1, true) == nil then
        return nil, "asset_sprite_packing_rotation_unsupported:" .. packing_rotation
    end

    local render_texture = nil
    local readable_texture = nil
    local previous_active = nil
    local ok, rendered = pcall(function()
        render_texture = render_texture_type.GetTemporary(
            width, height, 0, render_texture_format.ARGB32)
        if render_texture == nil then error("asset_render_texture_create_failed") end
        graphics.Blit(
            texture,
            render_texture,
            vector2_type(scale_x, scale_y),
            vector2_type(offset_x, offset_y))

        previous_active = render_texture_type.active
        render_texture_type.active = render_texture
        readable_texture = texture2d_type(width, height, texture_format.RGBA32, false)
        if readable_texture == nil then error("asset_readable_texture_create_failed") end
        readable_texture:ReadPixels(rect_type(0, 0, width, height), 0, 0, false)
        readable_texture:Apply(false, false)
        local png = image_conversion.EncodeToPNG(readable_texture)
        if png == nil then error("asset_png_encode_failed") end
        local base64 = convert_type.ToBase64String(png)
        if type(base64) ~= "string" or #base64 == 0 then error("asset_base64_encode_failed") end
        return {
            base64 = base64,
            width = width,
            height = height,
            packed = packed,
            packingRotation = packing_rotation,
            textureWidth = texture_width,
            textureHeight = texture_height,
            pixelsPerUnit = pixels_per_unit,
            textureRectX = x,
            textureRectY = y,
        }
    end)

    if render_texture_type ~= nil then
        pcall(function() render_texture_type.active = previous_active end)
    end
    if render_texture ~= nil then
        pcall(function() render_texture_type.ReleaseTemporary(render_texture) end)
    end
    asset_image_runtime.destroy_object(readable_texture)

    if not ok then
        return nil, "asset_png_render_failed:" .. tostring(rendered)
    end
    return rendered, nil
end

function asset_image_runtime.write_result(request, state, error_text, details)
    details = details or {}
    write_json(asset_image_runtime.resultPath, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = request.requestId,
        launchSessionId = request.launchSessionId,
        profileId = request.profileId,
        challenge = request.challenge,
        gamePid = request.gamePid,
        sourceMode = request.sourceMode,
        sourceValue = request.sourceValue or "",
        itemPathTemplate = request.itemPathTemplate,
        state = state,
        error = error_text,
        base64 = details.base64,
        width = details.width,
        height = details.height,
        packed = details.packed,
        packingRotation = details.packingRotation,
        textureWidth = details.textureWidth,
        textureHeight = details.textureHeight,
        pixelsPerUnit = details.pixelsPerUnit,
        textureRectX = details.textureRectX,
        textureRectY = details.textureRectY,
        renderMethod = state == "proven" and
            "SpriteRenderer.LoadSpriteAuto(extension)+Sprite.textureRect+Graphics.Blit+ImageConversion.EncodeToPNG" or nil,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

function asset_image_runtime.cleanup()
    if asset_image_runtime.request ~= nil then
        asset_image_runtime.destroy_object(asset_image_runtime.request.gameObject)
    end
    asset_image_runtime.request = nil
    asset_image_runtime.startedAt = nil
end

function asset_image_runtime.begin(request)
    if request.error ~= nil then
        request.done = true
        return
    end
    if request.sourceMode == "spriteName" then
        request.error = "asset_sprite_name_resolution_unrecovered"
        request.done = true
        return
    end

    local cs = rawget(_G, "CS")
    local typeof_fn = rawget(_G, "typeof")
    local game_object_type = cs and cs.UnityEngine and cs.UnityEngine.GameObject or nil
    local sprite_renderer_type = cs and cs.UnityEngine and cs.UnityEngine.SpriteRenderer or nil
    local load_path = rawget(_G, "LoadPath")
    local item_path = load_path and safe_get(load_path, "ItemPath") or nil
    request.itemPathTemplate = item_path ~= nil and tostring(item_path) or nil
    if game_object_type == nil or sprite_renderer_type == nil or type(typeof_fn) ~= "function" then
        request.error = "asset_sprite_loader_unavailable"
        request.done = true
        return
    end

    local ok_create, create_error = pcall(function()
        request.gameObject = game_object_type("LWBridgeAssetImage")
        request.renderer = request.gameObject:AddComponent(typeof_fn(sprite_renderer_type))
    end)
    if not ok_create or request.renderer == nil then
        request.error = "asset_sprite_renderer_create_failed:" .. tostring(create_error)
        request.done = true
        return
    end

    request.callback = function(sprite)
        if asset_image_runtime.request ~= request or request.done == true then return end
        if sprite == nil then
            request.error = "asset_sprite_load_failed"
            request.done = true
            return
        end
        local rendered, render_error = asset_image_runtime.render_sprite_png(sprite, request.renderer)
        if rendered == nil then
            request.error = render_error or "asset_png_render_failed"
        else
            request.rendered = rendered
        end
        request.done = true
    end

    local ok_load, load_error = pcall(function()
        request.renderer:LoadSpriteAuto(request.assetPath, request.callback, "")
    end)
    if not ok_load then
        request.error = "asset_sprite_extension_load_failed:" .. tostring(load_error)
        request.done = true
    end
end

function asset_image_runtime.pump(now)
    if asset_image_runtime.request == nil then
        local request = asset_image_runtime.read_request(now)
        if request ~= nil then
            asset_image_runtime.request = request
            asset_image_runtime.startedAt = runtime_clock()
            asset_image_runtime.begin(request)
        end
    end
    local request = asset_image_runtime.request
    if request == nil then return false end

    if request.done == true then
        if request.error ~= nil then
            asset_image_runtime.write_result(request, "failed", request.error, nil)
        else
            asset_image_runtime.write_result(request, "proven", nil, request.rendered)
        end
        asset_image_runtime.cleanup()
        return true
    end

    if asset_image_runtime.startedAt ~= nil and
       runtime_clock() - asset_image_runtime.startedAt >= asset_image_runtime.timeoutSeconds then
        request.error = "asset_sprite_load_timeout"
        asset_image_runtime.write_result(request, "failed", request.error, nil)
        asset_image_runtime.cleanup()
        return true
    end
    return true
end

function M._treasureStateRuntime.optional_integer(values, key)
    local text = values[key]
    if text == nil or text == "" then return nil end
    local value = tonumber(text)
    if value == nil or value < 0 or value ~= math.floor(value) then return false end
    return math.floor(value)
end

function M._treasureStateRuntime.optional_boolean(values, key)
    local text = values[key]
    if text == nil or text == "" then return nil end
    if text == "true" then return true end
    if text == "false" then return false end
    return "invalid"
end

function M._treasureStateRuntime.safe_text(values, key)
    local value = tostring(values[key] or "")
    if #value > 128 or string.find(value, "[%z\r\n]") ~= nil then return nil end
    return value
end

function M._treasureStateRuntime.read_request(now)
    local values = read_kv_file(M._treasureStateRuntime.path, 131072)
    if values == nil then return nil end
    pcall(os.remove, M._treasureStateRuntime.path)

    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "treasure_state_request_invalid"
        return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    request.serverId = tonumber(values.serverId)
    request.refreshDetails = values.refreshDetails == "true"
    local record_count = tonumber(values.recordCount)
    if values.refreshDetails ~= "true" and values.refreshDetails ~= "false" then
        request.error = "treasure_state_request_invalid"
        return request
    end
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or
       request.serverId == nil or request.serverId < 1 or request.serverId > 99999 or
       request.serverId ~= math.floor(request.serverId) or
       record_count == nil or record_count < 0 or record_count > 100 or
       record_count ~= math.floor(record_count) then
        request.error = "treasure_state_request_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    request.serverId = math.floor(request.serverId)
    request.records = {}
    local seen = {}
    for index = 1, math.floor(record_count) do
        local prefix = "record" .. tostring(index)
        local point_index = M._treasureStateRuntime.optional_integer(values, prefix .. "PointIndex")
        local uuid = M._treasureStateRuntime.safe_text(values, prefix .. "Uuid")
        local treasure_type = M._treasureStateRuntime.optional_integer(values, prefix .. "TreasureType")
        local supplies_type = M._treasureStateRuntime.optional_integer(values, prefix .. "SuppliesType")
        local alliance_id = M._treasureStateRuntime.safe_text(values, prefix .. "AllianceId")
        local viewer_uid = M._treasureStateRuntime.safe_text(values, prefix .. "ViewerUid")
        local viewer_alliance_id = M._treasureStateRuntime.safe_text(values, prefix .. "ViewerAllianceId")
        local viewer_has_reward = M._treasureStateRuntime.optional_boolean(values, prefix .. "ViewerHasReward")
        local viewer_is_working = M._treasureStateRuntime.optional_boolean(values, prefix .. "ViewerIsWorking")
        local complete = M._treasureStateRuntime.optional_boolean(values, prefix .. "Complete")
        local expire_time = M._treasureStateRuntime.optional_integer(values, prefix .. "ExpireTime")
        local start_time = M._treasureStateRuntime.optional_integer(values, prefix .. "StartTime")
        local completion_time = M._treasureStateRuntime.optional_integer(values, prefix .. "CompletionTime")
        local rewarded_count = M._treasureStateRuntime.optional_integer(values, prefix .. "RewardedCount")
        local digging_count = M._treasureStateRuntime.optional_integer(values, prefix .. "DiggingCount")
        local reward_max = M._treasureStateRuntime.optional_integer(values, prefix .. "RewardMax")
        local remaining_boxes = M._treasureStateRuntime.optional_integer(values, prefix .. "RemainingBoxes")
        local create_time = M._treasureStateRuntime.optional_integer(values, prefix .. "CreateTime")
        local discoverer_alliance_id = M._treasureStateRuntime.safe_text(values, prefix .. "DiscovererAllianceId")
        local discoverer_uid = M._treasureStateRuntime.safe_text(values, prefix .. "DiscovererUid")
        local work_state = M._treasureStateRuntime.optional_integer(values, prefix .. "WorkState")
        local user_count = M._treasureStateRuntime.optional_integer(values, prefix .. "UserCount")
        local ordinary = treasure_type ~= false and supplies_type ~= false and
            treasure_type ~= nil and treasure_type > 0 and supplies_type == 0
        local supplies = treasure_type ~= false and supplies_type ~= false and
            treasure_type == 0 and supplies_type ~= nil and supplies_type > 0
        if point_index == false or point_index == nil or point_index <= 0 or
           uuid == nil or #uuid < 1 or seen[uuid] or
           alliance_id == nil or viewer_uid == nil or viewer_alliance_id == nil or
           discoverer_alliance_id == nil or discoverer_uid == nil or
           viewer_has_reward == "invalid" or viewer_is_working == "invalid" or
           complete == "invalid" or expire_time == false or start_time == false or
           completion_time == false or rewarded_count == false or digging_count == false or
           reward_max == false or remaining_boxes == false or create_time == false or
           work_state == false or user_count == false or not (ordinary or supplies) then
            request.error = "treasure_state_request_invalid"
            return request
        end
        seen[uuid] = true
        request.records[#request.records + 1] = {
            pointIndex = point_index,
            uuid = uuid,
            treasureType = treasure_type,
            suppliesType = supplies_type,
            allianceId = alliance_id,
            viewerUid = viewer_uid,
            viewerAllianceId = viewer_alliance_id,
            viewerHasReward = viewer_has_reward,
            viewerIsWorking = viewer_is_working,
            complete = complete,
            expireTime = expire_time,
            startTime = start_time,
            completionTime = completion_time,
            rewardedCount = rewarded_count,
            diggingCount = digging_count,
            rewardMax = reward_max,
            remainingBoxes = remaining_boxes,
            createTime = create_time,
            discovererAllianceId = discoverer_alliance_id,
            discovererUid = discoverer_uid,
            workState = work_state,
            userCount = user_count,
        }
    end

    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "treasure_state_identity_mismatch"
    end
    return request
end

function M._treasureStateRuntime.player_identity()
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    if player == nil then return nil, nil end
    local uid = safe_get(player, "uid")
    if uid == nil then
        local ok_uid, observed_uid = call(player, "GetUid")
        uid = ok_uid and observed_uid or nil
    end
    local alliance_id = safe_get(player, "allianceId")
    if alliance_id == nil then
        local ok_alliance, observed_alliance = call(player, "GetAllianceUid")
        alliance_id = ok_alliance and observed_alliance or nil
    end
    local uid_text = uid ~= nil and tostring(uid) or ""
    if #uid_text < 1 then return nil, nil end
    return uid_text, alliance_id ~= nil and tostring(alliance_id) or ""
end

function M._treasureStateRuntime.server_time()
    local manager = rawget(_G, "UITimeManager")
    if manager == nil then return nil end
    local ok_instance, instance = call(manager, "GetInstance")
    if not ok_instance or instance == nil then return nil end
    local ok_time, value = call(instance, "GetServerTime")
    local numeric = ok_time and tonumber(value) or nil
    return numeric and numeric >= 0 and numeric or nil
end

function M._treasureStateRuntime.world_object()
    local cs = rawget(_G, "CS")
    local scene_manager = cs and safe_get(cs, "SceneManager") or rawget(_G, "SceneManager")
    return scene_manager and safe_get(scene_manager, "World") or nil
end

function M._treasureStateRuntime.resolve_point(record)
    local world = M._treasureStateRuntime.world_object()
    if world == nil then return nil end
    local ok_info, info = call(world, "GetPointInfo", record.pointIndex)
    if not ok_info or info == nil then return nil end
    local class_name = reflected_type_name(info) or ""
    if record.treasureType > 0 then
        if not string.find(class_name, "TreasurePointInfo", 1, true) then return nil end
        local ok_type, observed_type = call(info, "GetWorldTreasureType")
        local point_type = ok_type and tonumber(observed_type) or nil
        if point_type ~= nil and math.floor(point_type) ~= record.treasureType then return nil end
    else
        if not string.find(class_name, "WorldSuppliesPoint", 1, true) then return nil end
    end
    local point_uuid = safe_get(info, "uuid") or safe_get(info, "Uuid")
    if point_uuid ~= nil and tostring(point_uuid) ~= record.uuid then return nil end
    return info
end

function M._treasureStateRuntime.int64_uuid(text)
    local cs = rawget(_G, "CS")
    local int64 = cs and cs.System and cs.System.Int64 or nil
    if int64 == nil then return nil end
    local ok, value = pcall(function() return int64.Parse(text) end)
    return ok and value or nil
end

function M._treasureStateRuntime.get_supplies_detail(record)
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "WorldPointDetailManager") or nil
    if manager == nil then return nil end
    local uuid = M._treasureStateRuntime.int64_uuid(record.uuid)
    if uuid == nil then return nil end
    local ok, detail = call(manager, "GetWorldSuppliesPointDetailData", uuid)
    return ok and detail or nil
end

function M._treasureStateRuntime.send_supplies_detail(record, server_id)
    local detail = M._treasureStateRuntime.get_supplies_detail(record)
    if detail ~= nil then return false end
    local sfs = rawget(_G, "SFSNetwork")
    local msg_defines = rawget(_G, "MsgDefines")
    local message = msg_defines and safe_get(msg_defines, "WorldGetSuppliesPointDetail") or nil
    local uuid = M._treasureStateRuntime.int64_uuid(record.uuid)
    if sfs == nil or message == nil or uuid == nil then return false end
    local ok = select(1, call(sfs, "SendMessage", message, uuid, server_id, record.pointIndex))
    return ok == true
end

function M._treasureStateRuntime.clamp_percent(value)
    local numeric = tonumber(value)
    if numeric == nil then return nil end
    if numeric < 0 then return 0 end
    if numeric > 1 then return 1 end
    return numeric
end

function M._treasureStateRuntime.nonnegative(value)
    local numeric = tonumber(value)
    if numeric == nil or numeric < 0 then return nil end
    return numeric
end

function M._treasureStateRuntime.inspect_ordinary(record, player_uid, server_time)
    local state = { uuid = record.uuid }
    local info = M._treasureStateRuntime.resolve_point(record)
    local expire_time = record.expireTime
    local start_time = record.startTime
    local complete = record.complete
    local rewarded_count = record.rewardedCount
    local digging_count = record.diggingCount
    local reward_max = record.rewardMax
    local all_rewards = nil
    local has_reward = nil
    local has_working = nil
    if record.viewerUid == player_uid then
        has_reward = record.viewerHasReward
        has_working = record.viewerIsWorking
    end

    if info ~= nil then
        expire_time = M._treasureStateRuntime.nonnegative(safe_get(info, "expireTime") or safe_get(info, "ExpireTime")) or expire_time
        start_time = M._treasureStateRuntime.nonnegative(safe_get(info, "startTime") or safe_get(info, "StartTime")) or start_time
        local rewards = safe_get(info, "rewardUserList") or safe_get(info, "RewardUserList")
        local digging = safe_get(info, "diggingUserList") or safe_get(info, "DiggingUserList")
        rewarded_count = collection_count(rewards) or rewarded_count
        digging_count = collection_count(digging) or digging_count
        local ok_max, observed_max = call(info, "GetRewardMaxNum")
        reward_max = ok_max and M._treasureStateRuntime.nonnegative(observed_max) or reward_max
        local ok_all, observed_all = call(info, "IsReceiveAllReward")
        if ok_all then all_rewards = observed_all == true end
        local ok_complete, observed_complete = call(info, "IsComplete")
        if ok_complete then complete = observed_complete == true end
        local ok_reward, observed_reward = call(info, "IsHaveGetReward", player_uid)
        if ok_reward then has_reward = observed_reward == true end
        local ok_working, observed_working = call(info, "IsHaveWorking", player_uid)
        if ok_working then has_working = observed_working == true end
    end

    if all_rewards == nil and reward_max ~= nil and reward_max > 0 and
       rewarded_count ~= nil and rewarded_count >= reward_max then
        all_rewards = true
    end

    if expire_time ~= nil and expire_time > 0 and server_time ~= nil and server_time >= expire_time then
        state.worldClaimState = "expired"
    elseif all_rewards == true then
        state.worldClaimState = "depleted"
    elseif complete == true then
        state.worldClaimState = "claimable"
    elseif start_time ~= nil and start_time > 0 then
        state.worldClaimState = "charging"
    else
        state.worldClaimState = "unknown"
    end

    if has_working == true then
        state.playerClaimState = "digging"
    elseif has_reward == true then
        state.playerClaimState = "claimed"
    elseif has_working ~= nil and has_reward ~= nil then
        state.playerClaimState = "unclaimed"
    else
        state.playerClaimState = "unknown"
    end

    if rewarded_count ~= nil then state.rewardedCount = math.floor(rewarded_count) end
    if digging_count ~= nil then state.diggingCount = math.floor(digging_count) end
    if reward_max ~= nil and rewarded_count ~= nil then
        state.remainingBoxes = math.max(0, math.floor(reward_max - rewarded_count))
    elseif record.remainingBoxes ~= nil then
        state.remainingBoxes = math.floor(record.remainingBoxes)
    end
    if expire_time ~= nil and expire_time > 0 then state.expireTime = math.floor(expire_time) end
    return state
end

function M._treasureStateRuntime.charge_player_state(detail, player_uid)
    local charge_data = detail and safe_get(detail, "chargeData") or nil
    local index_dic = charge_data and safe_get(charge_data, "indexDic") or nil
    if type(index_dic) ~= "table" then return nil, nil, 0 end
    local has_player, got_reward, count = false, false, 0
    for _, value in pairs(index_dic) do
        count = count + 1
        if tostring(safe_get(value, "uid") or "") == player_uid then
            has_player = true
            got_reward = tonumber(safe_get(value, "getReward")) == 1
        end
    end
    return has_player, got_reward, count
end

function M._treasureStateRuntime.inspect_supplies(record, player_uid, server_time)
    local state = { uuid = record.uuid }
    local detail = M._treasureStateRuntime.get_supplies_detail(record)
    if detail == nil then
        state.worldClaimState = record.expireTime ~= nil and record.expireTime > 0 and
            server_time ~= nil and server_time >= record.expireTime and "expired" or "unknown"
        state.playerClaimState = "unknown"
        if record.rewardedCount ~= nil then state.rewardedCount = math.floor(record.rewardedCount) end
        if record.expireTime ~= nil and record.expireTime > 0 then state.expireTime = math.floor(record.expireTime) end
        return state
    end

    local expire_time = M._treasureStateRuntime.nonnegative(safe_get(detail, "expireTime"))
    local reward_count = M._treasureStateRuntime.nonnegative(safe_get(detail, "rewardCount"))
    local reward_max = M._treasureStateRuntime.nonnegative(safe_get(detail, "rewardMax"))
    local reward_left = M._treasureStateRuntime.nonnegative(safe_get(detail, "rewardLeftCount"))
    local btn_state = tonumber(safe_get(detail, "btnState"))
    local charge_data = safe_get(detail, "chargeData")
    local charge_percent = nil
    if charge_data ~= nil then
        local ok_percent, observed_percent = call(charge_data, "GetPercent")
        charge_percent = ok_percent and M._treasureStateRuntime.clamp_percent(observed_percent) or nil
    end
    local ok_has, observed_has = call(detail, "HasPlayer", player_uid)
    local has_player = ok_has and observed_has == true or nil
    local charge_has_player, charge_got_reward, charge_count =
        M._treasureStateRuntime.charge_player_state(detail, player_uid)
    if charge_has_player ~= nil then has_player = charge_has_player end
    local ok_can_get, observed_can_get = call(detail, "CheckBtnState")
    local can_get = ok_can_get and observed_can_get == true or false

    if expire_time ~= nil and expire_time > 0 and server_time ~= nil and server_time >= expire_time then
        state.worldClaimState = "expired"
    elseif charge_percent ~= nil and charge_percent < 1 then
        state.worldClaimState = "charging"
    elseif reward_left ~= nil and reward_left <= 0 then
        state.worldClaimState = "depleted"
    elseif can_get then
        state.worldClaimState = "claimable"
    else
        state.worldClaimState = "unknown"
    end

    if charge_percent ~= nil and charge_percent < 1 and has_player == true then
        state.playerClaimState = "digging"
    elseif charge_got_reward == true or btn_state == 1 or
           (charge_data == nil and has_player == true) then
        state.playerClaimState = "claimed"
    elseif has_player ~= nil or ok_can_get then
        state.playerClaimState = "unclaimed"
    else
        state.playerClaimState = "unknown"
    end

    if reward_count ~= nil then state.rewardedCount = math.floor(reward_count)
    elseif record.rewardedCount ~= nil then state.rewardedCount = math.floor(record.rewardedCount) end
    if charge_count > 0 and charge_percent ~= nil and charge_percent < 1 then
        state.diggingCount = charge_count
    end
    if reward_left ~= nil then
        state.remainingBoxes = math.floor(reward_left)
    elseif reward_max ~= nil and reward_count ~= nil then
        state.remainingBoxes = math.max(0, math.floor(reward_max - reward_count))
    end
    if expire_time ~= nil and expire_time > 0 then state.expireTime = math.floor(expire_time) end
    if charge_percent ~= nil then state.chargePercent = charge_percent end
    return state
end

function M._treasureStateRuntime.write_result(request, state, error_text, player_uid, alliance_id, states)
    write_json(M._treasureStateRuntime.resultPath, {
        schemaVersion = 1,
        probeVersion = M.VERSION,
        requestId = request.requestId,
        launchSessionId = request.launchSessionId,
        profileId = request.profileId,
        challenge = request.challenge,
        gamePid = request.gamePid,
        serverId = request.serverId,
        state = state,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
        error = error_text,
        playerUid = player_uid,
        allianceId = alliance_id,
        states = states or {},
        detailRequests = request.detailRequests or 0,
        source = "current-v19 TreasurePointInfo/WorldSuppliesPointData read-only reconstruction",
    })
end

function M._treasureStateRuntime.finish(request)
    local player_uid, alliance_id = M._treasureStateRuntime.player_identity()
    if player_uid == nil then
        M._treasureStateRuntime.write_result(
            request, "failed", "player_identity_unavailable", nil, nil, nil)
        return
    end
    local server_time = M._treasureStateRuntime.server_time()
    local states = {}
    for _, record in ipairs(request.records) do
        if record.treasureType > 0 then
            states[#states + 1] =
                M._treasureStateRuntime.inspect_ordinary(record, player_uid, server_time)
        else
            states[#states + 1] =
                M._treasureStateRuntime.inspect_supplies(record, player_uid, server_time)
        end
    end
    M._treasureStateRuntime.write_result(
        request, "proven", nil, player_uid, alliance_id, states)
end

function M._treasureStateRuntime.cleanup()
    M._treasureStateRuntime.request = nil
    M._treasureStateRuntime.startedAt = nil
end

function M._treasureStateRuntime.pump(now)
    if M._treasureStateRuntime.request == nil then
        local request = M._treasureStateRuntime.read_request(now)
        if request ~= nil then
            M._treasureStateRuntime.request = request
            M._treasureStateRuntime.startedAt = runtime_clock()
            request.detailRequests = 0
            if request.error == nil then
                local current_server = current_server_id()
                if current_server == nil then
                    request.error = "world_unavailable"
                elseif current_server ~= request.serverId then
                    request.error = "server_mismatch"
                elseif request.refreshDetails then
                    for _, record in ipairs(request.records) do
                        if record.suppliesType > 0 and
                           M._treasureStateRuntime.send_supplies_detail(record, request.serverId) then
                            request.detailRequests = request.detailRequests + 1
                        end
                    end
                end
            end
        end
    end

    local request = M._treasureStateRuntime.request
    if request == nil then return false end
    if request.error ~= nil then
        M._treasureStateRuntime.write_result(request, "failed", request.error, nil, nil, nil)
        M._treasureStateRuntime.cleanup()
        return true
    end

    if request.detailRequests > 0 and M._treasureStateRuntime.startedAt ~= nil and
       runtime_clock() - M._treasureStateRuntime.startedAt < M._treasureStateRuntime.detailWaitSeconds then
        local all_ready = true
        for _, record in ipairs(request.records) do
            if record.suppliesType > 0 and M._treasureStateRuntime.get_supplies_detail(record) == nil then
                all_ready = false
                break
            end
        end
        if not all_ready then return true end
    end

    if M._treasureStateRuntime.startedAt ~= nil and
       runtime_clock() - M._treasureStateRuntime.startedAt >= M._treasureStateRuntime.timeoutSeconds then
        M._treasureStateRuntime.write_result(
            request, "failed", "treasure_state_timeout", nil, nil, nil)
        M._treasureStateRuntime.cleanup()
        return true
    end

    M._treasureStateRuntime.finish(request)
    M._treasureStateRuntime.cleanup()
    return true
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
    local include_city_raw = tostring(values.includeCity or "false")
    request.includeCity = include_city_raw == "true"
    local include_resource_raw = tostring(values.includeResource or "false")
    request.includeResource = include_resource_raw == "true"
    local include_monster_raw = tostring(values.includeMonster or "false")
    request.includeMonster = include_monster_raw == "true"
    local include_monster_protection_raw = tostring(values.includeMonsterProtection or "false")
    request.includeMonsterProtection = include_monster_protection_raw == "true"
    local include_train_raw = tostring(values.includeTrain or "false")
    request.includeTrain = include_train_raw == "true"
    local include_dispatch_raw = tostring(values.includeDispatch or "false")
    request.includeDispatch = include_dispatch_raw == "true"
    local include_ghost_raw = tostring(values.includeGhost or "false")
    request.includeGhost = include_ghost_raw == "true"
    local include_treasure_raw = tostring(values.includeTreasure or "false")
    request.includeTreasure = include_treasure_raw == "true"
    local include_resource_details_raw = tostring(values.includeResourceDetails or "false")
    request.includeResourceDetails = include_resource_details_raw == "true"
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or
       request.serverId == nil or request.serverId <= 0 or request.serverId ~= math.floor(request.serverId) or
       not valid_token(request.scanRunId) or
       request.viewLevel == nil or request.viewLevel < -1 or request.viewLevel > 2 or request.viewLevel ~= math.floor(request.viewLevel) or
       (request.requestMode ~= "native" and request.requestMode ~= "expanded" and request.requestMode ~= "direct" and request.requestMode ~= "coverage" and request.requestMode ~= "anchor" and request.requestMode ~= "edge" and request.requestMode ~= "zoom") or
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
       (include_city_raw ~= "true" and include_city_raw ~= "false") or
       (include_resource_raw ~= "true" and include_resource_raw ~= "false") or
       (include_monster_raw ~= "true" and include_monster_raw ~= "false") or
       (include_monster_protection_raw ~= "true" and include_monster_protection_raw ~= "false") or
       (request.includeMonsterProtection == true and request.includeMonster ~= true) or
       (include_train_raw ~= "true" and include_train_raw ~= "false") or
       (include_dispatch_raw ~= "true" and include_dispatch_raw ~= "false") or
       (include_ghost_raw ~= "true" and include_ghost_raw ~= "false") or
       (include_treasure_raw ~= "true" and include_treasure_raw ~= "false") or
       (include_resource_details_raw ~= "true" and include_resource_details_raw ~= "false") then
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
    local matched, cities, resources, dispatches, ghosts, treasures = 0, 0, 0, 0, 0, 0
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
            if point_type == 17 then dispatches = dispatches + 1 end
            if point_type == 29 then ghosts = ghosts + 1 end
            if point_type == 21 or point_type == 27 then treasures = treasures + 1 end
        end
        return true
    end)
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return { total = matched, cities = cities, resources = resources, dispatches = dispatches, ghosts = ghosts, treasures = treasures, loadedPointCount = expected }, nil
end

local function city_aoi_records(world, point_manager, block_size, block_count, selected_lookup)
    -- Current-v19 responses can omit already-retained City rows from _pointInfos.
    -- The public/xLua-wrapped GetAllMainBaseList() returns the unique retained
    -- BuildPointInfo main bases for the current manager state; filter that list
    -- back to the exact native AOIs proven by the response.
    local ok_list, collection = call(point_manager, "GetAllMainBaseList")
    if not ok_list or collection == nil then
        ok_list, collection = reflected_call(point_manager, "GetAllMainBaseList")
    end
    if not ok_list or collection == nil then return nil, "WorldPointManager.GetAllMainBaseList unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "main_base_list_count_invalid" end
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
        local health = player_city_health_snapshot(info)
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
            health = health.effectiveHp,
            healthRawCurHp = health.rawCurHp,
            healthMaxHp = health.maxHp,
            healthLastHpTime = health.lastHpTime,
            healthRecoverSpeed = health.recoverSpeed,
            healthUnavailableTime = health.unavailableTime,
            healthFireSpeed = health.fireSpeed,
            healthCalculationMode = health.calculationMode,
            protectEndTime = scalar_field(info, { "protectEndTime", "ProtectEndTime" }),
            source = "WorldPointManager.GetAllMainBaseList",
        }
        return true
    end)
    if scanned ~= expected then return nil, "main_base_list_enumeration_mismatch" end
    return records, nil
end

local function resource_source_metadata(tile, server_id, resource_source)
    local config_id = integer_field(resource_source, { "id", "Id" })
    local name_key, reserve = nil, nil
    local controller_type = rawget(_G, "LocalController")
    local ok_controller, controller = call(controller_type, "instance")
    local table_name = rawget(_G, "TableName")
    local gather_table = table_name and safe_get(table_name, "GatherResource") or nil
    if config_id ~= nil and config_id > 0 and ok_controller and controller ~= nil and gather_table ~= nil then
        local ok_cfg, cfg = call(controller, "getLine", gather_table, config_id)
        if ok_cfg and cfg ~= nil then
            local observed_name = scalar_field(cfg, { "name", "Name" })
            if observed_name ~= nil and tostring(observed_name) ~= "" then name_key = tostring(observed_name) end
            local observed_reserve = tonumber(scalar_field(cfg, { "reserve", "Reserve" }))
            if observed_reserve ~= nil and observed_reserve >= 0 then reserve = observed_reserve end
        end
    end

    local black_known, black_tile = false, nil
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    local scene_utils = rawget(_G, "SceneUtils")
    local tile_to_world = scene_utils and safe_get(scene_utils, "TileToWorld") or nil
    local force_change_scene = rawget(_G, "ForceChangeScene")
    local force_world = force_change_scene and safe_get(force_change_scene, "World") or nil
    local cs = rawget(_G, "CS")
    local vector2_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
    if player ~= nil and type(tile_to_world) == "function" and force_world ~= nil and vector2_type ~= nil and
       tile ~= nil and tonumber(tile.x) ~= nil and tonumber(tile.y) ~= nil and tonumber(server_id) ~= nil then
        local ok_world, world_pos = pcall(tile_to_world, vector2_type(math.floor(tile.x), math.floor(tile.y)), force_world, math.floor(server_id))
        if ok_world and world_pos ~= nil then
            local ok_black, observed_black = call(player, "IsInBlackRange", world_pos)
            if ok_black and type(observed_black) == "boolean" then
                black_known = true
                black_tile = observed_black
            end
        end
    end
    return config_id, name_key, reserve, black_known, black_tile
end

local function resource_aoi_records(world, point_manager, block_size, block_count, selected_lookup, detail_request)
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
        local resource_config_id, resource_name_key, resource_reserve, black_tile_known, is_black_tile =
            resource_source_metadata(tile, server_id, resource_source)
        if detail_request ~= nil and detail_request.includeResourceDetails == true and
           gather_occupancy_known and gather_occupied == false then
            resource_scan_detail_runtime.queue(detail_request, {
                pointId = id, pointType = point_type, serverId = math.floor(server_id),
                worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
                uid = tostring(scalar_field(info, { "ownerUid", "OwnerUid", "uid", "Uid" }) or ""),
                resourceConfigId = resource_config_id, resourceNameKey = resource_name_key,
                resourceMaxAmount = resource_reserve,
            })
        end
        records[#records + 1] = {
            id = id, pointId = id, pointType = point_type, kind = "resource_point",
            runtimeClass = reflected_type_name(info), serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
            x = tile.x, y = tile.y, level = level, resourceTypeId = resource_type,
            resourceConfigId = resource_config_id, resourceNameKey = resource_name_key,
            resourceMaxAmount = resource_reserve,
            resourceSourceType = reflected_type_name(resource_source),
            gatherOccupancyKnown = gather_occupancy_known, gatherOccupied = gather_occupied,
            blackTileKnown = black_tile_known, isBlackTile = is_black_tile,
            source = "WorldPointManager._pointInfos+GatherResource+LuaEntry.Player.IsInBlackRange",
        }
        return true
    end)
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return records, nil
end

local function dispatch_aoi_records(world, point_manager, block_size, block_count, selected_lookup)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end

    local controller_type = rawget(_G, "LocalController")
    local ok_controller, controller = call(controller_type, "instance")
    local table_name = rawget(_G, "TableName")
    local dispatch_table = table_name and safe_get(table_name, "LwDispatchTask") or nil
    if not ok_controller or controller == nil or dispatch_table == nil then
        return nil, "dispatch_config_manager_unavailable"
    end

    local records = {}
    local record_error = nil
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        if integer_field(info, { "pointType", "PointType" }) ~= 17 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex" })
        if id == nil or id <= 0 then record_error = "dispatch_point_index_invalid"; return false end
        local tile = index_to_tile(world, id)
        if tile == nil then record_error = "dispatch_tile_unavailable"; return false end
        local cell_x = math.floor(tile.x / block_size)
        local cell_y = math.floor(tile.y / block_size)
        if cell_x < 0 or cell_y < 0 or cell_x >= block_count or cell_y >= block_count then return true end
        local aoi_index = cell_y * block_count + cell_x
        if selected_lookup[aoi_index] ~= true then return true end

        local server_id = integer_field(info, { "serverId", "ServerId" }) or current_server_id()
        local world_id = integer_field(info, { "worldId", "WorldId" }) or 0
        local uuid = scalar_field(info, { "uuid", "Uuid" })
        local cfg_id = integer_field(info, { "cfgId", "CfgId" })
        if server_id == nil or server_id <= 0 or uuid == nil or tostring(uuid) == "" or tostring(uuid) == "0" or
           cfg_id == nil or cfg_id <= 0 then
            record_error = "dispatch_identity_invalid"
            return false
        end

        local ok_cfg, cfg = call(controller, "getLine", dispatch_table, cfg_id)
        if not ok_cfg or cfg == nil then
            record_error = "dispatch_config_unavailable:" .. tostring(cfg_id)
            return false
        end
        local level = tonumber(scalar_field(cfg, { "level", "Level" }))
        local quality = tonumber(scalar_field(cfg, { "color", "Color" }))
        local is_special = tonumber(scalar_field(cfg, { "is_special", "isSpecial", "IsSpecial" }))
        local protect_time = tonumber(scalar_field(cfg, { "protect_times", "protectTime", "ProtectTime" }))
        local steal_max_times = tonumber(scalar_field(cfg, { "steal_maxtimes", "stealMaxTimes", "StealMaxTimes" }))
        if level == nil or level < 1 or quality == nil or quality < 1 or is_special == nil then
            record_error = "dispatch_config_shape_invalid:" .. tostring(cfg_id)
            return false
        end

        local owner_uid = scalar_field(info, { "ownerUid", "OwnerUid" })
        local steal_list = safe_get(info, "stealList") or safe_get(info, "StealList")
        local acc_list = safe_get(info, "accList") or safe_get(info, "AccList")
        records[#records + 1] = {
            id = id,
            pointId = id,
            pointType = 17,
            kind = "dispatch_task",
            runtimeClass = reflected_type_name(info),
            serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = world_id,
            x = tile.x,
            y = tile.y,
            uuid = tostring(uuid),
            ownerUid = owner_uid ~= nil and tostring(owner_uid) or nil,
            cfgId = cfg_id,
            level = math.floor(level),
            quality = math.floor(quality),
            isSpecial = is_special == 1,
            completionTime = scalar_field(info, { "completionTime", "CompletionTime" }),
            rewarded = integer_field(info, { "rewarded", "Rewarded" }),
            actEndTime = scalar_field(info, { "actEndTime", "ActEndTime" }),
            expiredTime = scalar_field(info, { "expiredTime", "ExpiredTime" }),
            allianceId = scalar_field(info, { "allianceId", "AllianceId" }),
            stealListCount = collection_count(steal_list),
            accListCount = collection_count(acc_list),
            protectTimeMinutes = protect_time,
            stealMaxTimes = steal_max_times,
            dispatchNameKey = scalar_field(cfg, { "name", "Name" }),
            source = "WorldPointManager._pointInfos+HeroDispatchMissionPointInfo",
        }
        return true
    end)
    if record_error ~= nil then return nil, record_error end
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return records, nil
end

local function ghost_aoi_records(world, point_manager, block_size, block_count, selected_lookup)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end

    local controller_type = rawget(_G, "LocalController")
    local ok_controller, controller = call(controller_type, "instance")
    local table_name = rawget(_G, "TableName")
    local ghost_table = table_name and safe_get(table_name, "LwGhostreconTask") or nil
    if not ok_controller or controller == nil or ghost_table == nil then
        return nil, "ghost_config_manager_unavailable"
    end

    local records = {}
    local record_error = nil
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        if integer_field(info, { "pointType", "PointType" }) ~= 29 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex" })
        if id == nil or id <= 0 then record_error = "ghost_point_index_invalid"; return false end
        local tile = index_to_tile(world, id)
        if tile == nil then record_error = "ghost_tile_unavailable"; return false end
        local cell_x = math.floor(tile.x / block_size)
        local cell_y = math.floor(tile.y / block_size)
        if cell_x < 0 or cell_y < 0 or cell_x >= block_count or cell_y >= block_count then return true end
        local aoi_index = cell_y * block_count + cell_x
        if selected_lookup[aoi_index] ~= true then return true end

        local ghost_source = info
        local ok_ghost, loaded_ghost = call(point_manager, "GetGhostreconPointInfoByIndex", id)
        if ok_ghost and loaded_ghost ~= nil then ghost_source = loaded_ghost end
        local runtime_class = reflected_type_name(ghost_source)
        local server_id = integer_field(ghost_source, { "serverId", "ServerId" }) or current_server_id()
        local world_id = integer_field(ghost_source, { "worldId", "WorldId" }) or 0
        local uuid = scalar_field(ghost_source, { "uuid", "Uuid" })
        local cfg_id = integer_field(ghost_source, { "cfgId", "CfgId" })
        if runtime_class == nil or not string.find(runtime_class, "GhostreconPointInfo", 1, true) or
           server_id == nil or server_id <= 0 or uuid == nil or tostring(uuid) == "" or tostring(uuid) == "0" or
           cfg_id == nil or cfg_id <= 0 then
            record_error = "ghost_identity_invalid"
            return false
        end

        local ok_cfg, cfg = call(controller, "getLine", ghost_table, cfg_id)
        if not ok_cfg or cfg == nil then
            record_error = "ghost_config_unavailable:" .. tostring(cfg_id)
            return false
        end
        local level = tonumber(scalar_field(cfg, { "level", "Level" }))
        local quality = tonumber(scalar_field(cfg, { "color", "Color" }))
        local is_special = tonumber(scalar_field(cfg, { "is_special", "isSpecial", "IsSpecial" }))
        if level == nil or level < 1 or quality == nil or quality < 1 or is_special == nil then
            record_error = "ghost_config_shape_invalid:" .. tostring(cfg_id)
            return false
        end

        local steal_list = safe_get(ghost_source, "stealList") or safe_get(ghost_source, "StealList")
        local member_list = safe_get(ghost_source, "memberList") or safe_get(ghost_source, "MemberList")
        records[#records + 1] = {
            id = id,
            pointId = id,
            pointType = 29,
            kind = "ghost_task",
            runtimeClass = runtime_class,
            serverId = math.floor(server_id),
            srcServerId = integer_field(ghost_source, { "srcServerId", "SrcServerId" }) or 0,
            worldId = world_id,
            x = tile.x,
            y = tile.y,
            uuid = tostring(uuid),
            ownerUid = tostring(scalar_field(ghost_source, { "ownerUid", "OwnerUid" }) or ""),
            cfgId = cfg_id,
            level = math.floor(level),
            quality = math.floor(quality),
            isSpecial = is_special == 1,
            completionTime = scalar_field(ghost_source, { "completionTime", "CompletionTime" }),
            taskExpireTime = scalar_field(ghost_source, { "taskExpireTime", "TaskExpireTime" }),
            actEndTime = scalar_field(ghost_source, { "actEndTime", "ActEndTime" }),
            teamStartTime = scalar_field(ghost_source, { "teamStartTime", "TeamStartTime" }),
            ownerServer = integer_field(ghost_source, { "ownerServer", "OwnerServer" }),
            allianceId = scalar_field(ghost_source, { "allianceId", "AllianceId" }),
            size = integer_field(ghost_source, { "size", "Size" }),
            stealListCount = collection_count(steal_list),
            memberListCount = collection_count(member_list),
            rewardConfig = scalar_field(cfg, { "base_reward_show", "reward", "Reward" }),
            worldOpen = scalar_field(cfg, { "world_open", "worldOpen", "WorldOpen" }),
            protectTime = scalar_field(cfg, { "protect_times", "protectTime", "ProtectTime" }),
            stealMaxTimes = scalar_field(cfg, { "steal_maxtimes", "stealMaxtimes", "StealMaxtimes" }),
            source = "WorldPointManager._pointInfos+GhostreconPointInfo+TableName.LwGhostreconTask",
        }
        return true
    end)
    if record_error ~= nil then return nil, record_error end
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return records, nil
end

local function treasure_aoi_records(world, point_manager, block_size, block_count, selected_lookup)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = collection_count(collection)
    if expected == nil or expected < 0 or expected > MAX_POINTS then return nil, "point_count_invalid" end

    local controller_type = rawget(_G, "LocalController")
    local ok_controller, controller = call(controller_type, "instance")
    local table_name = rawget(_G, "TableName")
    local supplies_table = table_name and safe_get(table_name, "LWIceSupplies") or nil
    local viewer_uid, viewer_alliance_id = M._treasureStateRuntime.player_identity()
    viewer_uid = viewer_uid or ""
    viewer_alliance_id = viewer_alliance_id or ""

    local records = {}
    local record_error = nil
    local scanned = each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local point_type = integer_field(info, { "pointType", "PointType" })
        if point_type ~= 21 and point_type ~= 27 then return true end
        local id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex" })
        if id == nil or id <= 0 then record_error = "treasure_point_index_invalid"; return false end
        local tile = index_to_tile(world, id)
        if tile == nil then record_error = "treasure_tile_unavailable"; return false end
        local cell_x = math.floor(tile.x / block_size)
        local cell_y = math.floor(tile.y / block_size)
        if cell_x < 0 or cell_y < 0 or cell_x >= block_count or cell_y >= block_count then return true end
        local aoi_index = cell_y * block_count + cell_x
        if selected_lookup[aoi_index] ~= true then return true end

        local runtime_class = reflected_type_name(info)
        local server_id = integer_field(info, { "serverId", "ServerId" }) or current_server_id()
        local world_id = integer_field(info, { "worldId", "WorldId" }) or 0
        local uuid = scalar_field(info, { "uuid", "Uuid" })
        if server_id == nil or server_id <= 0 or uuid == nil or tostring(uuid) == "" or tostring(uuid) == "0" then
            record_error = "treasure_identity_invalid"
            return false
        end

        if point_type == 21 then
            if runtime_class == nil or not string.find(runtime_class, "TreasurePointInfo", 1, true) then
                record_error = "treasure_runtime_class_invalid"
                return false
            end
            local ok_type, observed_type = call(info, "GetWorldTreasureType")
            local treasure_type = ok_type and tonumber(observed_type) or
                tonumber(scalar_field(info, { "type", "Type" }))
            if treasure_type == nil or treasure_type <= 0 then
                record_error = "treasure_type_invalid"
                return false
            end
            local reward_users = safe_get(info, "rewardUserList") or safe_get(info, "RewardUserList")
            local digging_users = safe_get(info, "diggingUserList") or safe_get(info, "DiggingUserList")
            local rewarded_count = collection_count(reward_users)
            local digging_count = collection_count(digging_users)
            local ok_reward_max, observed_reward_max = call(info, "GetRewardMaxNum")
            local reward_max = ok_reward_max and tonumber(observed_reward_max) or nil
            local viewer_has_reward, viewer_is_working = nil, nil
            if viewer_uid ~= "" then
                local ok_reward, observed_reward = call(info, "IsHaveGetReward", viewer_uid)
                if ok_reward then viewer_has_reward = observed_reward == true end
                local ok_working, observed_working = call(info, "IsHaveWorking", viewer_uid)
                if ok_working then viewer_is_working = observed_working == true end
            end
            local remaining_boxes = nil
            if reward_max ~= nil and reward_max >= 0 and rewarded_count ~= nil and rewarded_count >= 0 then
                remaining_boxes = math.max(0, math.floor(reward_max) - rewarded_count)
            end
            records[#records + 1] = {
                id = id, pointId = id, pointType = 21, kind = "treasure_point",
                runtimeClass = runtime_class, serverId = math.floor(server_id),
                srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
                worldId = world_id, x = tile.x, y = tile.y, uuid = tostring(uuid),
                ownerUid = tostring(scalar_field(info, { "ownerUid", "OwnerUid" }) or ""),
                ownerName = scalar_field(info, { "ownerName", "OwnerName" }),
                eventId = scalar_field(info, { "eventId", "EventId" }),
                treasureType = math.floor(treasure_type), suppliesType = 0,
                startTime = scalar_field(info, { "startTime", "StartTime" }),
                completionTime = scalar_field(info, { "completionTime", "CompletionTime" }),
                expireTime = scalar_field(info, { "expireTime", "ExpireTime" }),
                createTime = scalar_field(info, { "createTime", "CreateTime" }),
                complete = scalar_field(info, { "complete", "Complete" }),
                speed = scalar_field(info, { "speed", "Speed" }),
                allianceId = scalar_field(info, { "allianceId", "AllianceId" }),
                allianceAbbr = scalar_field(info, { "allianceAbbr", "AllianceAbbr" }),
                rewardedCount = rewarded_count, diggingCount = digging_count,
                rewardMax = reward_max, remainingBoxes = remaining_boxes,
                viewerUid = viewer_uid, viewerAllianceId = viewer_alliance_id,
                viewerHasReward = viewer_has_reward, viewerIsWorking = viewer_is_working,
                fromPoint = integer_field(info, { "fromPoint", "FromPoint" }),
                multiple = integer_field(info, { "multiple", "Multiple" }),
                killerId = scalar_field(info, { "killerId", "KillerId" }),
                customInfo = scalar_field(info, { "customInfoStr", "CustomInfoStr" }),
                source = "WorldPointManager._pointInfos+TreasurePointInfo",
            }
            return true
        end

        if runtime_class == nil or not string.find(runtime_class, "WorldSuppliesPoint", 1, true) then
            record_error = "supplies_runtime_class_invalid"
            return false
        end
        local cfg_id = integer_field(info, { "configId", "ConfigId" })
        if cfg_id == nil then
            local ok_cfg_id, observed_cfg_id = call(info, "get_configId")
            cfg_id = ok_cfg_id and tonumber(observed_cfg_id) or nil
        end
        if cfg_id == nil or cfg_id <= 0 or not ok_controller or controller == nil or supplies_table == nil then
            record_error = "supplies_config_identity_invalid"
            return false
        end
        local ok_cfg, cfg = call(controller, "getLine", supplies_table, math.floor(cfg_id))
        if not ok_cfg or cfg == nil then
            record_error = "supplies_config_unavailable:" .. tostring(cfg_id)
            return false
        end
        local supplies_type = tonumber(scalar_field(cfg, { "type", "Type" }))
        if supplies_type == nil or supplies_type <= 0 then
            record_error = "supplies_type_invalid:" .. tostring(cfg_id)
            return false
        end
        local uid_list = safe_get(info, "uidList") or safe_get(info, "_uidList") or safe_get(info, "UidList")
        records[#records + 1] = {
            id = id, pointId = id, pointType = 27, kind = "supplies_point",
            runtimeClass = runtime_class, serverId = math.floor(server_id),
            srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
            worldId = world_id, x = tile.x, y = tile.y, uuid = tostring(uuid),
            treasureType = 0, suppliesType = math.floor(supplies_type),
            configId = math.floor(cfg_id),
            state = integer_field(info, { "state", "State" }),
            userCount = integer_field(info, { "userCount", "UserCount" }),
            rewardedCount = collection_count(uid_list),
            viewerUid = viewer_uid, viewerAllianceId = viewer_alliance_id,
            createTime = scalar_field(info, { "createTime", "CreateTime" }),
            discovererAllianceId = scalar_field(info, { "discovererAllianceId", "DiscovererAllianceId" }),
            discovererUid = scalar_field(info, { "discovererUid", "DiscovererUid" }),
            workEndTime = scalar_field(info, { "workEndTime", "WorkEndTime" }),
            workState = integer_field(info, { "workState", "WorkState" }),
            source = "WorldPointManager._pointInfos+WorldSuppliesPoint+TableName.LWIceSupplies",
        }
        return true
    end)
    if record_error ~= nil then return nil, record_error end
    if scanned ~= expected then return nil, "point_enumeration_mismatch" end
    return records, nil
end

local function resolve_reward_display_metadata(data_center, reward_type, item_id)
    if data_center == nil or reward_type == nil or item_id == nil then return nil, nil end
    M._rewardMetadataCache = M._rewardMetadataCache or {}
    local cache_key = tostring(reward_type) .. ":" .. tostring(item_id)
    local cached = M._rewardMetadataCache[cache_key]
    if cached ~= nil then return cached.name, cached.iconPath end

    local name, icon_path = nil, nil
    local item_manager = safe_get(data_center, "ItemTemplateManager")
    local reward_manager = safe_get(data_center, "RewardManager")
    local reward_type_enum = rawget(_G, "RewardType")
    local goods_type = reward_type_enum and tonumber(safe_get(reward_type_enum, "GOODS")) or nil
    local load_path = rawget(_G, "LoadPath")
    local item_path = load_path and safe_get(load_path, "ItemPath") or nil

    if goods_type ~= nil and reward_type == goods_type and item_manager ~= nil then
        local ok_template, goods = call(item_manager, "GetItemTemplate", item_id)
        local ok_name, resolved_name = call(item_manager, "GetName", item_id)
        if ok_name and resolved_name ~= nil and tostring(resolved_name) ~= "" then name = tostring(resolved_name) end
        if ok_template and goods ~= nil then
            local join_method = tonumber(safe_get(goods, "join_method")) or -1
            local icon_join = safe_get(goods, "icon_join")
            if join_method > 0 and icon_join ~= nil and tostring(icon_join) ~= "" then
                local parts = {}
                for part in string.gmatch(tostring(icon_join), "([^;]+)") do parts[#parts + 1] = part end
                if #parts > 2 and parts[3] ~= "" then icon_path = parts[3] end
            end
            if icon_path == nil then
                local icon = safe_get(goods, "icon")
                if icon ~= nil and tostring(icon) ~= "" and item_path ~= nil then
                    local ok_format, formatted = pcall(string.format, tostring(item_path), tostring(icon))
                    if ok_format and formatted ~= nil and formatted ~= "" then icon_path = formatted end
                end
            end
        end
    elseif reward_manager ~= nil then
        local ok_name, resolved_name = call(reward_manager, "GetNameByType", reward_type, item_id)
        if (not ok_name or resolved_name == nil or tostring(resolved_name) == "") then
            ok_name, resolved_name = call(reward_manager, "GetNameByType", reward_type)
        end
        if ok_name and resolved_name ~= nil and tostring(resolved_name) ~= "" then name = tostring(resolved_name) end
        local ok_icon, resolved_icon = call(reward_manager, "GetPicByType", reward_type, item_id)
        if (not ok_icon or resolved_icon == nil or tostring(resolved_icon) == "") then
            ok_icon, resolved_icon = call(reward_manager, "GetPicByType", reward_type)
        end
        if ok_icon and resolved_icon ~= nil and tostring(resolved_icon) ~= "" then icon_path = tostring(resolved_icon) end
    end

    M._rewardMetadataCache[cache_key] = { name = name, iconPath = icon_path }
    return name, icon_path
end

local function normalize_train_current_goods(march, train, train_data_json, explicit_train_data)
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "LWTrainDataManager") or nil

    local lua_train = explicit_train_data
    if manager ~= nil then
        local train_uuid = train and scalar_field(train, { "uuid", "Uuid" }) or nil
        if train_uuid ~= nil then
            local ok_train, value = call(manager, "GetOneTrain", train_uuid)
            if ok_train and value ~= nil then lua_train = value end
        end
        if lua_train == nil then
            local march_uuid = march and scalar_field(march, { "uuid", "Uuid", "_uuid" }) or nil
            if march_uuid ~= nil then
                local ok_train, value = call(manager, "GetOneTrainByMarchUuid", march_uuid)
                if ok_train and value ~= nil then lua_train = value end
            end
        end
    end

    local rewards = nil
    local max_loot_count = lua_train and tonumber(safe_get(lua_train, "maxLootPerTrain")) or nil
    local decoded_train_data = nil
    if lua_train ~= nil then
        local ok_rewards, value = call(lua_train, "GetCurRewardData")
        if ok_rewards and type(value) == "table" then rewards = value end
    end
    if (rewards == nil or max_loot_count == nil) and train_data_json ~= nil and train_data_json ~= "" then
        -- Exact current-v18 TrainData:GetCurRewardData fallback for TrainType.Train:
        -- flatten marchInfo.carriageList[*].trainGoods.cur from the already captured
        -- SFS train payload. This is read-only and sends no additional game request.
        local ok_module, rapidjson = pcall(require, "rapidjson")
        if ok_module and rapidjson ~= nil then
            local ok_decode, decoded = pcall(rapidjson.decode, train_data_json)
            if ok_decode and type(decoded) == "table" then decoded_train_data = decoded end
            local march_info = decoded_train_data and decoded_train_data.marchInfo or nil
            local carriage_list = type(march_info) == "table" and march_info.carriageList or nil
            if rewards == nil and type(carriage_list) == "table" then
                rewards = {}
                for _, carriage in ipairs(carriage_list) do
                    local train_goods = type(carriage) == "table" and carriage.trainGoods or nil
                    local current = type(train_goods) == "table" and train_goods.cur or nil
                    if type(current) == "table" then
                        for _, reward in ipairs(current) do rewards[#rewards + 1] = reward end
                    end
                end
            end
        end
    end
    if max_loot_count == nil and decoded_train_data ~= nil then
        -- Exact current-v18 TrainData.Refresh maxLootPerTrain construction.
        local ally_manager = data_center and safe_get(data_center, "LWAllyStationDataManager") or nil
        local base_max = ally_manager and tonumber(safe_get(ally_manager, "MAX_LOOT_PER_TRAIN")) or nil
        if base_max ~= nil and base_max >= 0 then
            local reduction = 0
            local delete_info = safe_get(ally_manager, "Delete_Train_Times")
            local march_info = type(decoded_train_data.marchInfo) == "table" and decoded_train_data.marchInfo or nil
            local lua_entry = rawget(_G, "LuaEntry")
            local data_config = lua_entry and safe_get(lua_entry, "DataConfig") or nil
            local ok_switch, vip_switch = call(data_config, "CheckSwitch", "alliance_train_vip")
            local vip_on = march_info and march_info.vipOn ~= nil and march_info.vipOn ~= false
            local rights_open = ok_switch and vip_switch ~= nil and vip_switch ~= false
            local gift_lv = march_info and tonumber(march_info.giftLv) or nil
            local required_gift_lv = delete_info and tonumber(safe_get(delete_info, "gift_lv")) or nil
            local buy_flag = tonumber(decoded_train_data.buyFlag)
            if delete_info ~= nil and rights_open and vip_on and
               ((gift_lv ~= nil and required_gift_lv ~= nil and gift_lv >= required_gift_lv) or buy_flag == 1) then
                reduction = tonumber(safe_get(delete_info, "para1")) or 0
            end
            max_loot_count = math.max(base_max - reduction, 0)
        end
    end

    if type(rewards) ~= "table" then return nil, max_loot_count end

    local item_manager = safe_get(data_center, "ItemTemplateManager")
    local reward_manager = safe_get(data_center, "RewardManager")
    local reward_type_enum = rawget(_G, "RewardType")
    local goods_type = reward_type_enum and tonumber(safe_get(reward_type_enum, "GOODS")) or nil
    local load_path = rawget(_G, "LoadPath")
    local item_path = load_path and safe_get(load_path, "ItemPath") or nil

    local by_key, order = {}, {}
    for _, reward in ipairs(rewards) do
        local reward_type = tonumber(safe_get(reward, "type"))
        local reward_value = safe_get(reward, "value")
        local item_id, count = nil, nil
        if type(reward_value) == "table" then
            item_id = tonumber(safe_get(reward_value, "id"))
            count = tonumber(safe_get(reward_value, "num"))
        else
            item_id = reward_type
            count = tonumber(reward_value)
        end

        if reward_type ~= nil and item_id ~= nil and count ~= nil and count > 0 then
            local name, icon_path = nil, nil
            if goods_type ~= nil and reward_type == goods_type and item_manager ~= nil then
                local ok_template, goods = call(item_manager, "GetItemTemplate", item_id)
                local ok_name, resolved_name = call(item_manager, "GetName", item_id)
                if ok_name and resolved_name ~= nil and tostring(resolved_name) ~= "" then
                    name = tostring(resolved_name)
                end
                if ok_template and goods ~= nil then
                    local join_method = tonumber(safe_get(goods, "join_method")) or -1
                    local icon_join = safe_get(goods, "icon_join")
                    if join_method > 0 and icon_join ~= nil and tostring(icon_join) ~= "" then
                        local parts = {}
                        for part in string.gmatch(tostring(icon_join), "([^;]+)") do parts[#parts + 1] = part end
                        if #parts > 2 and parts[3] ~= "" then icon_path = parts[3] end
                    end
                    if icon_path == nil then
                        local icon = safe_get(goods, "icon")
                        if icon ~= nil and tostring(icon) ~= "" and item_path ~= nil then
                            local ok_format, formatted = pcall(string.format, tostring(item_path), tostring(icon))
                            if ok_format and formatted ~= nil and formatted ~= "" then icon_path = formatted end
                        end
                    end
                end
            elseif reward_manager ~= nil then
                local ok_name, resolved_name = call(reward_manager, "GetNameByType", reward_type, item_id)
                if (not ok_name or resolved_name == nil or tostring(resolved_name) == "") then
                    ok_name, resolved_name = call(reward_manager, "GetNameByType", reward_type)
                end
                if ok_name and resolved_name ~= nil and tostring(resolved_name) ~= "" then
                    name = tostring(resolved_name)
                end
                local ok_icon, resolved_icon = call(reward_manager, "GetPicByType", reward_type, item_id)
                if (not ok_icon or resolved_icon == nil or tostring(resolved_icon) == "") then
                    ok_icon, resolved_icon = call(reward_manager, "GetPicByType", reward_type)
                end
                if ok_icon and resolved_icon ~= nil and tostring(resolved_icon) ~= "" then
                    icon_path = tostring(resolved_icon)
                end
            end

            if name ~= nil and icon_path ~= nil then
                -- Internal rebuild identity only. Last War supplies rewardType/itemId;
                -- the original LWBridge currentGoods key producer remains unrecovered.
                local key = "reward:" .. tostring(reward_type) .. ":" .. tostring(item_id)
                local existing = by_key[key]
                if existing == nil then
                    existing = {
                        key = key,
                        name = name,
                        iconPath = icon_path,
                        count = 0,
                        rewardType = reward_type,
                        itemId = item_id,
                    }
                    by_key[key] = existing
                    order[#order + 1] = key
                end
                existing.count = existing.count + count
            end
        end
    end

    if #order == 0 then return nil, max_loot_count end
    local result = {}
    for _, key in ipairs(order) do result[#result + 1] = by_key[key] end
    return result, max_loot_count
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
                    local march_uuid = tostring(scalar_field(march, { "uuid", "Uuid", "_uuid" }) or "")
                    local raw_train_data = safe_get(train, "trainData")
                    local train_data = nil
                    local data_center = rawget(_G, "DataCenter")
                    local manager = data_center and safe_get(data_center, "LWTrainDataManager") or nil
                    if manager ~= nil then
                        local train_uuid = scalar_field(train, { "uuid", "Uuid" })
                        if train_uuid ~= nil then
                            local ok_train, value = call(manager, "GetOneTrain", train_uuid)
                            if ok_train and value ~= nil then train_data = value end
                        end
                        if train_data == nil and march_uuid ~= "" then
                            local ok_train, value = call(manager, "GetOneTrainByMarchUuid", march_uuid)
                            if ok_train and value ~= nil then train_data = value end
                        end
                    end
                    if train_data == nil and raw_train_data ~= nil then train_data = raw_train_data end
                    local train_type_raw = scalar_field(train, { "type", "Type" })
                    local train_data_type_raw = train_data and scalar_field(train_data, { "type", "Type" }) or nil
                    local train_type = integer_field(train, { "type", "Type" })
                    if train_type == nil and train_type_raw ~= nil then
                        train_type = tonumber(tostring(train_type_raw):match(":%s*(-?%d+)%s*$"))
                    end
                    if train_type == nil and train_data ~= nil then
                        train_type = integer_field(train_data, { "type", "Type" })
                        if train_type == nil and train_data_type_raw ~= nil then
                            train_type = tonumber(tostring(train_data_type_raw):match(":%s*(-?%d+)%s*$"))
                        end
                    end
                    local train_data_json = nil
                    -- TrainData:ToJson() is large on current-v19 Trucks because it includes
                    -- plunder history. Keep that blob only for Railway fallback; Truck uses
                    -- the lightweight game-owned TrainData fields below.
                    if train_type == 2 and train_data ~= nil then
                        local ok_json, value = call(train_data, "ToJson")
                        if ok_json and value ~= nil then train_data_json = tostring(value) end
                        if train_data_json == nil then
                            local ok_dump, dump = call(train_data, "GetDump")
                            if ok_dump and dump ~= nil then train_data_json = tostring(dump) end
                        end
                    end
                    local train_arrive_ts = train_data and scalar_field(train_data, { "arriveTs", "arriveTime", "ArriveTs", "ArriveTime" }) or nil
                    local train_march_info = train_data and safe_get(train_data, "marchInfo") or nil
                    local train_rob_times = train_march_info and integer_field(train_march_info, { "robTimes", "RobTimes" }) or nil
                    local train_protect_time = train_march_info and scalar_field(train_march_info, { "protectTime", "ProtectTime" }) or nil
                    local truck_base_goods_cur, truck_extra_goods_cur, truck_vip_on = nil, nil, nil
                    local truck_current_goods_raw, truck_max_loot_count = nil, nil
                    local truck_metadata_known = train_type == 1 and train_data ~= nil
                    if truck_metadata_known then
                        local base_goods = safe_get(train_data, "baseGoods")
                        local extra_goods = safe_get(train_data, "extraGoods")
                        truck_base_goods_cur = base_goods and safe_get(base_goods, "cur") or nil
                        truck_extra_goods_cur = extra_goods and safe_get(extra_goods, "cur") or nil
                        truck_vip_on = train_march_info and safe_get(train_march_info, "vipOn") or nil
                        local ok_rewards, rewards = call(train_data, "GetCurRewardData")
                        if ok_rewards and type(rewards) == "table" then
                            truck_current_goods_raw = {}
                            for _, reward in ipairs(rewards) do
                                local reward_type = tonumber(safe_get(reward, "type"))
                                local reward_value = safe_get(reward, "value")
                                local item_id, count = nil, nil
                                if type(reward_value) == "table" then
                                    item_id = tonumber(safe_get(reward_value, "id"))
                                    count = tonumber(safe_get(reward_value, "num"))
                                else
                                    item_id = reward_type
                                    count = tonumber(reward_value)
                                end
                                if reward_type ~= nil and item_id ~= nil and count ~= nil and count > 0 then
                                    local reward_name, reward_icon = resolve_reward_display_metadata(data_center, reward_type, item_id)
                                    truck_current_goods_raw[#truck_current_goods_raw + 1] = {
                                        type = reward_type,
                                        itemId = item_id,
                                        count = count,
                                        name = reward_name,
                                        iconPath = reward_icon,
                                    }
                                end
                            end
                        end
                        truck_max_loot_count = tonumber(safe_get(train_data, "maxLootPerTrain"))
                    end
                    local current_goods, max_loot_count = nil, nil
                    if train_type == 2 then
                        current_goods, max_loot_count = normalize_train_current_goods(march, train, train_data_json)
                    end
                    records[#records + 1] = {
                        uuid = march_uuid,
                        marchUuid = march_uuid,
                        runtimeClass = reflected_type_name(march),
                        serverId = integer_field(march, { "serverId", "ServerId" }) or current_server_id(),
                        worldId = integer_field(march, { "worldId", "WorldId" }) or 0,
                        x = tile.x, y = tile.y, positionIndex = math.floor(index),
                        ownerUid = scalar_field(march, { "ownerUid", "OwnerUid" }),
                        ownerName = scalar_field(march, { "ownerName", "OwnerName" }),
                        allianceUid = scalar_field(march, { "allianceUid", "AllianceUid" }),
                        allianceName = scalar_field(march, { "allianceName", "AllianceName" }),
                        allianceAbbr = scalar_field(march, { "allianceAbbr", "AllianceAbbr" }),
                        ownerServer = integer_field(march, { "ownerServer", "OwnerServer" }),
                        targetServer = integer_field(march, { "targetServer", "TargetServer" }),
                        srcServer = integer_field(march, { "srcServer", "SrcServer" }),
                        power = scalar_field(march, { "power", "Power" }),
                        startTime = scalar_field(march, { "startTime", "StartTime" }),
                        endTime = scalar_field(march, { "endTime", "EndTime" }),
                        trainUuid = scalar_field(train, { "uuid", "Uuid" }),
                        trainCfgId = integer_field(train, { "cfgId", "CfgId" }),
                        trainType = train_type,
                        trainQuality = config and integer_field(config, { "quality", "Quality" }) or nil,
                        carriageNum = config and integer_field(config, { "carriageNum", "CarriageNum" }) or nil,
                        arriveTs = train_arrive_ts,
                        robTimes = train_rob_times,
                        protectTime = train_protect_time,
                        truckMetadataKnown = truck_metadata_known,
                        truckCurrentGoodsRaw = truck_current_goods_raw,
                        truckMaxLootCount = truck_max_loot_count,
                        truckBaseGoodsCur = truck_base_goods_cur,
                        truckExtraGoodsCur = truck_extra_goods_cur,
                        truckVipOn = truck_vip_on,
                        trainDataJson = train_data_json,
                        currentGoods = current_goods,
                        maxLootCount = max_loot_count,
                        source = "WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train",
                    }
                end
            end
        end
    end
    local completion_error = bounded_enumerator_completion_error(
        collection, enumerator, scanned, MAX_POINTS, "march")
    if completion_error ~= nil then return nil, completion_error end
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

local function monster_protection_server_time_ms()
    local manager_type = rawget(_G, "UITimeManager")
    local ok_instance, manager = call(manager_type, "GetInstance")
    if not ok_instance or manager == nil then return nil end
    local ok, value = call(manager, "GetServerTime")
    value = ok and tonumber(value) or nil
    return value ~= nil and value > 0 and value or nil
end

local function monster_protection_same_identity(left, right)
    if left == nil or right == nil then return false end
    return tostring(left) == tostring(right)
end

local function monster_protection_should_show(msg, pending)
    local active_value = scalar_field(msg, { "isProtected", "IsProtected" })
    local active = active_value == true or tonumber(active_value) == 1
    if not active then return false end

    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    local player_alliance = player and scalar_field(player, { "allianceId", "AllianceId" }) or nil
    local player_uid = player and scalar_field(player, { "uid", "Uid" }) or nil
    local alliance_uid = scalar_field(msg, { "allianceUid", "AllianceUid" })
    local belong_uid = type(pending) == "table" and pending.sourceBelongUid or nil

    local alliance_text = alliance_uid ~= nil and tostring(alliance_uid) or ""
    local same_alliance = alliance_text ~= "" and
        monster_protection_same_identity(alliance_uid, player_alliance)
    local own_boss = monster_protection_same_identity(belong_uid, player_uid)
    return not same_alliance and not own_boss
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
            local should_show = monster_protection_should_show(msg, pending)
            local ok_end, end_time = call(manager, "GetMonsterProtectionEndTime", uuid)
            end_time = ok_end and tonumber(end_time) or 0
            if active and should_show and end_time <= 0 and type(pending) == "table" then
                -- The original MonsterProtection.Refresh computes this exact deadline
                -- from the live WorldMarch createTime plus monster_invasion.k12. During
                -- the coarse whole-world scan the manager can receive the authoritative
                -- detail reply while being unable to instantiate its visual protection
                -- object (GetTroop/position may be unavailable), leaving its getter at 0.
                -- Reuse the same game formula from the just-captured march only after
                -- the server has confirmed isProtected=true.
                local candidate = tonumber(pending.sourceProtectionEndTime) or 0
                local server_time = tonumber(monster_protection_server_time_ms()) or 0
                if candidate > 0 and (server_time <= 0 or candidate > server_time) then
                    end_time = candidate
                end
            end
            if not active or not should_show then end_time = 0 end
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
    local server_time_ms = monster_protection_server_time_ms()
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
                        local source_end_time = monster_invasion_protection_deadline(march)
                        targets[#targets + 1] = {
                            uuid = uuid, wireUuid = wire_uuid, serverId = server_id,
                            sourceProtectionEndTime = source_end_time,
                            sourceBelongUid = scalar_field(march, { "belongUid", "BelongUid", "ownerUid", "OwnerUid" }),
                            -- The reconstructed createTime + k12 deadline is useful diagnostic
                            -- context only. It is not authoritative enough to suppress the
                            -- game's MonsterInvasionBossDetail request.
                            requestProtectionDetail = true,
                        }
                    end
                end
            end
        end
    end
    local completion_error = bounded_enumerator_completion_error(
        collection, enumerator, scanned, MAX_POINTS, "march")
    if completion_error ~= nil then return nil, nil, completion_error end
    return targets, total, nil
end

local function send_monster_invasion_protection_request(target)
    local state, capture_error = ensure_monster_protection_capture()
    if state == nil then return nil, capture_error end
    local sfs, defs = rawget(_G, "SFSNetwork"), rawget(_G, "MsgDefines")
    local message = defs and safe_get(defs, "MonsterInvasionBossDetail") or nil
    local send = sfs and safe_get(sfs, "SendMessage") or nil
    if message == nil or type(send) ~= "function" then
        return nil, "monster_invasion_protection_transport_unavailable"
    end
    state.pending[target.uuid] = target
    state.responses[target.uuid] = nil
    local ok = pcall(send, message, target.serverId, target.wireUuid)
    if not ok then ok = pcall(send, sfs, message, target.serverId, target.wireUuid) end
    if not ok then
        state.pending[target.uuid] = nil
        return nil, "monster_invasion_protection_send_failed"
    end
    return 1, nil
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

local function ensure_monster_protection_message_capture()
    local require_module = rawget(_G, "require")
    if type(require_module) ~= "function" then return nil, "monster_protection_message_registry_unavailable" end
    -- Current-v18 Net.Config.MsgMap line 2435 maps MonsterInvasionBossDetail
    -- to this exact module path; SFSNetwork.GetMsgType requires and caches it.
    local ok_require, message_class = pcall(require_module, "Net.Msgs.MonsterInvasionBossDetailMessge")
    if not ok_require or type(message_class) ~= "table" then
        return nil, "monster_protection_message_handler_unavailable"
    end
    local current = safe_get(message_class, "HandleMessage")
    local state = rawget(_G, "__lwbridgeMonsterProtectionMessageCapture")
    if type(state) == "table" and state.messageClass == message_class and state.wrapper == current then return state, nil end
    if type(current) ~= "function" then return nil, "monster_protection_message_handler_unavailable" end
    state = { messageClass = message_class, original = current }
    state.wrapper = function(self, msg)
        local scan = monster_protection_scan
        local target = type(scan) == "table" and scan.inflightTarget or nil
        local ok_original, result = pcall(state.original, self, msg)
        if target ~= nil and type(scan) == "table" and scan.inflightTarget == target then
            local response_uuid = msg and (safe_get(msg, "uuid") or reflected_value(msg, "uuid")) or nil
            if response_uuid ~= nil and tostring(response_uuid) ~= target.uuid then
                -- A duplicate/late reply from the previous serialized request or
                -- previous scan must never complete/fail the current request.
                -- The game's message module has one shared UUID slot, so repeated
                -- scans can legitimately observe an old reply after a new inflight
                -- target has already been claimed. Ignore it for correlation; the
                -- original handler above is still allowed to refresh game-owned data.
                scan.staleResponseCount = (scan.staleResponseCount or 0) + 1
            else
                local capture = rawget(_G, "__lwbridgeMonsterProtectionCapture")
                local error_code = scalar_field(msg, { "errorCode", "ErrorCode" })
                local prior_response = type(capture) == "table" and
                    type(capture.responses) == "table" and capture.responses[target.uuid] or nil
                local prior_ready = type(prior_response) == "table" and
                    prior_response.received == true and prior_response.isProtected ~= nil
                if error_code == nil and type(capture) == "table" and
                   type(capture.responses) == "table" and not prior_ready then
                    -- Current-v18 MonsterInvasionBossDetailMessge passes the same
                    -- reply object to MonsterProtectionManager.OnGetDetail. If that
                    -- callback is missed/replaced, recover the exact reply directly
                    -- here instead of wasting a retry. The display deadline still
                    -- follows MonsterProtection.Refresh: createTime + k12.
                    local active_value = scalar_field(msg, { "isProtected", "IsProtected" })
                    if active_value ~= nil then
                        local active = active_value == true or tonumber(active_value) == 1
                        local should_show = monster_protection_should_show(msg, target)
                        local end_time = 0
                        if active and should_show then
                            local candidate = tonumber(target.sourceProtectionEndTime) or 0
                            local server_time = tonumber(monster_protection_server_time_ms()) or 0
                            if candidate > 0 and (server_time <= 0 or candidate > server_time) then
                                end_time = candidate
                            end
                        end
                        capture.responses[target.uuid] = {
                            received = true,
                            isProtected = active_value,
                            protectionEndTime = end_time,
                        }
                    end
                end
                if type(capture) == "table" and type(capture.pending) == "table" then
                    capture.pending[target.uuid] = nil
                end
                scan.inflightCompletedUuid = target.uuid
                scan.inflightErrorCode = error_code
            end
        end
        if not ok_original then error(result) end
        return result
    end
    local ok_set = pcall(function() message_class.HandleMessage = state.wrapper end)
    if not ok_set or safe_get(message_class, "HandleMessage") ~= state.wrapper then
        return nil, "monster_protection_message_capture_install_failed"
    end
    rawset(_G, "__lwbridgeMonsterProtectionMessageCapture", state)
    return state, nil
end

local function queue_monster_invasion_protection_requests(request, targets)
    if not same_monster_protection_scan(monster_protection_scan, request) then
        if type(monster_protection_scan) == "table" and type(monster_protection_scan.targets) == "table" then
            local capture = rawget(_G, "__lwbridgeMonsterProtectionCapture")
            if type(capture) == "table" and type(capture.pending) == "table" then
                for index = 1, #monster_protection_scan.targets do capture.pending[monster_protection_scan.targets[index].uuid] = nil end
            end
        end
        monster_protection_scan = {
            profileId = request.profileId, launchSessionId = request.launchSessionId,
            challenge = request.challenge, gamePid = request.gamePid,
            scanRunId = request.scanRunId, serverId = request.serverId,
            targets = {}, targetByUuid = {}, requestQueue = {}, queueIndex = 1,
            requestCount = 0, retryCount = 0, responseCount = 0, staleResponseCount = 0,
            timeoutCount = 0, unresolvedCount = 0, errorResponseCount = 0,
            inflightTarget = nil, inflightCompletedUuid = nil, inflightErrorCode = nil,
            inflightStartedAt = nil, inflightAttempt = 0,
            error = nil,
        }
    end
    local queued = 0
    for index = 1, #targets do
        local target = targets[index]
        if monster_protection_scan.targetByUuid[target.uuid] == nil then
            monster_protection_scan.targetByUuid[target.uuid] = target
            monster_protection_scan.targets[#monster_protection_scan.targets + 1] = target
            local capture = rawget(_G, "__lwbridgeMonsterProtectionCapture")
            if type(capture) == "table" and type(capture.responses) == "table" then
                -- A new scan owns a fresh authority decision for this UUID.
                capture.responses[target.uuid] = nil
            end
            if target.requestProtectionDetail == true then
                monster_protection_scan.requestQueue[#monster_protection_scan.requestQueue + 1] = target
                queued = queued + 1
            end
        end
    end
    return queued, nil
end

local function monster_protection_response_ready(uuid)
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    local response = type(state) == "table" and type(state.responses) == "table" and state.responses[uuid] or nil
    return type(response) == "table" and response.received == true and response.isProtected ~= nil
end

local function clear_monster_protection_inflight(scan)
    scan.inflightTarget = nil
    scan.inflightCompletedUuid = nil
    scan.inflightErrorCode = nil
    scan.inflightStartedAt = nil
    scan.inflightAttempt = 0
end

local function retry_monster_protection_inflight(scan, target)
    scan.inflightCompletedUuid = nil
    scan.inflightErrorCode = nil
    local sent, send_error = send_monster_invasion_protection_request(target)
    if sent == nil then
        scan.error = scan.error or send_error
        clear_monster_protection_inflight(scan)
        return false
    end
    scan.inflightAttempt = (scan.inflightAttempt or 0) + 1
    scan.inflightStartedAt = runtime_clock()
    scan.retryCount = (scan.retryCount or 0) + sent
    return true
end

local function pump_monster_protection_queue()
    local scan = monster_protection_scan
    if type(scan) ~= "table" then return end

    if scan.inflightTarget ~= nil then
        local target = scan.inflightTarget
        local completed = scan.inflightCompletedUuid == target.uuid
        local elapsed = runtime_clock() - (scan.inflightStartedAt or runtime_clock())
        if completed then
            if monster_protection_response_ready(target.uuid) then
                scan.responseCount = (scan.responseCount or 0) + 1
                clear_monster_protection_inflight(scan)
            elseif scan.inflightErrorCode ~= nil then
                -- Current-v18 MonsterInvasionBossDetailMessge explicitly skips
                -- MonsterProtectionManager.OnGetDetail when errorCode is present.
                -- That is a terminal server reply for this UUID, not a lost reply,
                -- so do not waste the shared module UUID slot retrying it.
                target.detailErrorCode = scan.inflightErrorCode
                scan.errorResponseCount = (scan.errorResponseCount or 0) + 1
                scan.unresolvedCount = (scan.unresolvedCount or 0) + 1
                clear_monster_protection_inflight(scan)
            elseif (scan.inflightAttempt or 0) < MONSTER_INVASION_PROTECTION_MAX_RETRIES then
                retry_monster_protection_inflight(scan, target)
                return
            else
                scan.unresolvedCount = (scan.unresolvedCount or 0) + 1
                clear_monster_protection_inflight(scan)
            end
        elseif elapsed >= MONSTER_INVASION_PROTECTION_ITEM_TIMEOUT_SECONDS then
            if (scan.inflightAttempt or 0) < MONSTER_INVASION_PROTECTION_MAX_RETRIES then
                retry_monster_protection_inflight(scan, target)
                return
            else
                local capture = rawget(_G, "__lwbridgeMonsterProtectionCapture")
                if type(capture) == "table" and type(capture.pending) == "table" then
                    capture.pending[target.uuid] = nil
                end
                scan.timeoutCount = (scan.timeoutCount or 0) + 1
                scan.unresolvedCount = (scan.unresolvedCount or 0) + 1
                clear_monster_protection_inflight(scan)
            end
        else
            return
        end
    end

    if scan.queueIndex > #scan.requestQueue then return end
    local _, capture_error = ensure_monster_protection_capture()
    local _, message_error = ensure_monster_protection_message_capture()
    if capture_error ~= nil or message_error ~= nil then
        scan.error = scan.error or capture_error or message_error
        scan.queueIndex = #scan.requestQueue + 1
        return
    end

    local target = scan.requestQueue[scan.queueIndex]
    scan.queueIndex = scan.queueIndex + 1
    -- Claim correlation before SendMessage so even an unexpectedly synchronous
    -- completion is attributed to this exact target.
    scan.inflightTarget = target
    scan.inflightCompletedUuid = nil
    scan.inflightErrorCode = nil
    scan.inflightStartedAt = runtime_clock()
    scan.inflightAttempt = 0
    local sent, send_error = send_monster_invasion_protection_request(target)
    if sent == nil then
        scan.error = scan.error or send_error
        clear_monster_protection_inflight(scan)
        return
    end
    scan.requestCount = scan.requestCount + sent
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

local function monster_protection_epoch_ms(value)
    local number = tonumber(value) or 0
    if number <= 0 then return 0 end
    if number < 100000000000 then return number * 1000 end
    return number
end

local function monster_protection_manager_end_time(uuid)
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "MonsterProtectionManager") or nil
    if manager == nil or uuid == nil then return 0 end
    local ok, value = call(manager, "GetMonsterProtectionEndTime", uuid)
    if not ok then return 0 end
    return monster_protection_epoch_ms(value)
end

local function monster_invasion_protection_snapshot(uuid, wire_uuid)
    local state = rawget(_G, "__lwbridgeMonsterProtectionCapture")
    local response = type(state) == "table" and state.responses[tostring(uuid)] or nil
    if type(response) == "table" and response.received == true and response.isProtected ~= nil then
        local active = response.isProtected == true or tonumber(response.isProtected) == 1
        local end_time = active and monster_protection_epoch_ms(response.protectionEndTime) or 0
        return true, active, end_time
    end

    -- The game's own WorldMonsterDes reads this manager after
    -- MonsterInvasionBossDetail. Reuse a still-live game-owned value (for
    -- example one populated after a user Jump) instead of treating the boss
    -- as unknown until our post-scan detail pass catches up.
    local cached_end_time = monster_protection_manager_end_time(wire_uuid or uuid)
    local server_time = monster_protection_epoch_ms(monster_protection_server_time_ms())
    if cached_end_time > 0 and (server_time <= 0 or cached_end_time > server_time) then
        return true, true, cached_end_time
    end
    return false, false, 0
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
            errorCode = target.detailErrorCode,
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
        staleResponseCount = request.staleResponseCount or 0,
        timeoutCount = request.timeoutCount or 0,
        unresolvedCount = request.unresolvedCount or 0,
        errorResponseCount = request.errorResponseCount or 0,
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
            write_monster_protection_detail_result(request, "completed", "monster_protection_scan_unavailable", nil, 0, 0)
            return true
        end
        request.targets = monster_protection_scan.targets or {}
        request.requestCount = monster_protection_scan.requestCount or 0
        request.retryCount = monster_protection_scan.retryCount or 0
        request.staleResponseCount = monster_protection_scan.staleResponseCount or 0
        request.timeoutCount = monster_protection_scan.timeoutCount or 0
        request.unresolvedCount = monster_protection_scan.unresolvedCount or 0
        request.errorResponseCount = monster_protection_scan.errorResponseCount or 0
        request.scanError = monster_protection_scan.error
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
    local scan = monster_protection_scan
    local targets = request.targets or {}
    local queue_done = type(scan) ~= "table" or
        (scan.inflightTarget == nil and scan.queueIndex > #scan.requestQueue)
    local elapsed = runtime_clock() - (monster_protection_started_at or runtime_clock())
    local allowed_seconds = math.max(
        MONSTER_INVASION_PROTECTION_MIN_TIMEOUT_SECONDS,
        math.min(
            MONSTER_INVASION_PROTECTION_MAX_TIMEOUT_SECONDS,
            #targets * MONSTER_INVASION_PROTECTION_PER_TARGET_SECONDS +
                MONSTER_INVASION_PROTECTION_TIMEOUT_PADDING_SECONDS))
    if not queue_done and elapsed < allowed_seconds then return true end
    if type(scan) == "table" then
        request.requestCount = scan.requestCount or request.requestCount
        request.retryCount = scan.retryCount or request.retryCount
        request.staleResponseCount = scan.staleResponseCount or request.staleResponseCount
        request.timeoutCount = scan.timeoutCount or request.timeoutCount
        request.unresolvedCount = scan.unresolvedCount or request.unresolvedCount
        request.errorResponseCount = scan.errorResponseCount or request.errorResponseCount
    end
    local ready = count_ready_monster_invasion_protection_details(targets)
    local incomplete = ready < #targets
    local error_text = request.scanError or
        (not queue_done and "monster_invasion_protection_response_timeout" or
         (incomplete and "monster_invasion_protection_partial_response" or nil))
    if error_text ~= nil then abandon_monster_invasion_protection_requests(targets) end
    write_monster_protection_detail_result(request, "completed", error_text, targets, request.requestCount, ready)
    monster_protection_request = nil
    monster_protection_started_at = nil
    monster_protection_scan = nil
    return true
end

local function read_resource_detail_diagnostic(now)
    local values = read_kv_file(resource_detail_diagnostic_path, 4096)
    if values == nil then return nil end
    pcall(os.remove, resource_detail_diagnostic_path)
    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "resource_detail_diagnostic_invalid"
        return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    request.serverId = tonumber(values.serverId)
    request.maxTargets = tonumber(values.maxTargets or "1")
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or
       request.serverId == nil or request.serverId <= 0 or request.serverId ~= math.floor(request.serverId) or
       request.maxTargets == nil or request.maxTargets < 1 or request.maxTargets > 32 or
       request.maxTargets ~= math.floor(request.maxTargets) then
        request.error = "resource_detail_diagnostic_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    request.serverId = math.floor(request.serverId)
    request.maxTargets = math.floor(request.maxTargets)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "resource_detail_diagnostic_identity_mismatch"
    end
    return request
end

local function resource_detail_manager()
    local data_center = rawget(_G, "DataCenter")
    return data_center and safe_get(data_center, "WorldPointDetailManager") or nil
end

local function resource_detail_snapshot(manager, point_id)
    if manager == nil or point_id == nil then return nil end
    local ok, detail = call(manager, "GetDetailByPointId", point_id)
    if not ok or detail == nil then return nil end
    return {
        resourceId = integer_field(detail, { "resourceId", "ResourceId" }),
        remainRes = tonumber(scalar_field(detail, { "remainRes", "RemainRes" })),
        initRes = tonumber(scalar_field(detail, { "initRes", "InitRes" })),
        reserve = tonumber(scalar_field(detail, { "reserve", "Reserve" })),
        initReserve = tonumber(scalar_field(detail, { "initReserve", "InitReserve" })),
        collectStartTime = tonumber(scalar_field(detail, { "collectStartTime", "CollectStartTime" })),
        speed = tonumber(scalar_field(detail, { "speed", "Speed" })),
    }
end

local function find_resource_detail_targets(world, point_manager, requested_server_id, max_targets)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "resource_point_collection_unavailable" end
    local manager = resource_detail_manager()
    local uncached, cached = {}, {}
    each(collection, MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local point_type = integer_field(info, { "pointType", "PointType" })
        if point_type ~= 1 and point_type ~= 7 and point_type ~= 26 then return true end
        local point_id = integer_field(info, { "pointIndex", "PointIndex", "mainIndex", "MainIndex", "pointId", "PointId" })
        if point_id == nil or point_id <= 0 then return true end
        local resource_source = info
        local ok_resource, loaded_resource = call(point_manager, "GetResourcePointInfoByIndex", point_id)
        if ok_resource and loaded_resource ~= nil then resource_source = loaded_resource end
        local gather_march_found, gather_march_uuid = reflected_field_value(resource_source, "gatherMarchUuid")
        local gather_uid_found, gather_uid = reflected_field_value(resource_source, "gatherUid")
        if not gather_march_found or not gather_uid_found then return true end
        if occupancy_value_present(gather_march_uuid) or occupancy_value_present(gather_uid) then return true end
        local server_id = integer_field(info, { "serverId", "ServerId" }) or current_server_id()
        if server_id == nil or math.floor(server_id) ~= math.floor(requested_server_id) then return true end
        local tile = index_to_tile(world, point_id)
        if tile == nil then return true end
        local config_id, name_key, max_amount = resource_source_metadata(tile, server_id, resource_source)
        local target = {
            pointId = point_id, pointType = point_type, serverId = math.floor(server_id),
            worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
            uid = tostring(scalar_field(info, { "ownerUid", "OwnerUid", "uid", "Uid" }) or ""),
            resourceConfigId = config_id, resourceNameKey = name_key, resourceMaxAmount = max_amount,
        }
        if resource_detail_snapshot(manager, point_id) == nil then
            uncached[#uncached + 1] = target
        else
            cached[#cached + 1] = target
        end
        return #uncached < max_targets
    end)
    local selected = {}
    for index = 1, math.min(#uncached, max_targets) do selected[#selected + 1] = uncached[index] end
    if #selected == 0 then
        for index = 1, math.min(#cached, max_targets) do selected[#selected + 1] = cached[index] end
    end
    if #selected == 0 then return nil, "no_idle_resource_point_loaded" end
    return selected, #uncached > 0 and nil or "resource_detail_targets_already_cached"
end

local function resource_detail_result_rows(manager, targets)
    local rows, ready = {}, 0
    for index = 1, #targets do
        local target = targets[index]
        local detail = resource_detail_snapshot(manager, target.pointId)
        if detail ~= nil then ready = ready + 1 end
        rows[#rows + 1] = {
            resourceConfigId = target.resourceConfigId, resourceNameKey = target.resourceNameKey,
            resourceMaxAmount = target.resourceMaxAmount, received = detail ~= nil, detail = detail,
        }
    end
    return rows, ready
end

function resource_scan_detail_runtime.same_state(state, request)
    return type(state) == "table" and state.profileId == request.profileId and
        state.launchSessionId == request.launchSessionId and state.challenge == request.challenge and
        state.gamePid == request.gamePid and state.serverId == request.serverId and
        state.scanRunId == request.scanRunId
end

function resource_scan_detail_runtime.send(target)
    local sfs, defs = rawget(_G, "SFSNetwork"), rawget(_G, "MsgDefines")
    local message = defs and safe_get(defs, "WorldGetDetail") or nil
    local send = sfs and safe_get(sfs, "SendMessage") or nil
    if message == nil or type(send) ~= "function" then return false end
    local ok = pcall(send, message, target.pointId, target.serverId, target.worldId, 0, target.pointType, target.uid)
    if not ok then ok = pcall(send, sfs, message, target.pointId, target.serverId, target.worldId, 0, target.pointType, target.uid) end
    return ok == true
end

function resource_scan_detail_runtime.queue(request, target)
    if request == nil or request.includeResourceDetails ~= true or target == nil or target.pointId == nil then return false end
    if not resource_scan_detail_runtime.same_state(resource_scan_detail_runtime.state, request) then
        resource_scan_detail_runtime.state = {
            profileId = request.profileId, launchSessionId = request.launchSessionId,
            challenge = request.challenge, gamePid = request.gamePid, serverId = request.serverId,
            scanRunId = request.scanRunId, targets = {}, targetByKey = {},
            requestCount = 0, cacheBeforeCount = 0, sendFailureCount = 0,
        }
    end
    local state = resource_scan_detail_runtime.state
    local key = tostring(math.floor(tonumber(target.pointId) or 0))
    if key == "0" or state.targetByKey[key] ~= nil then return false end
    target.recordKey = key
    state.targetByKey[key] = target
    state.targets[#state.targets + 1] = target
    local manager = resource_detail_manager()
    if resource_detail_snapshot(manager, target.pointId) ~= nil then
        target.cacheBefore = true
        state.cacheBeforeCount = state.cacheBeforeCount + 1
        return true
    end
    if resource_scan_detail_runtime.send(target) then
        target.requestIssued = true
        state.requestCount = state.requestCount + 1
    else
        target.sendFailed = true
        state.sendFailureCount = state.sendFailureCount + 1
    end
    return true
end

function resource_scan_detail_runtime.read(now)
    local values = read_kv_file(resource_scan_detail_path, 4096)
    if values == nil then return nil end
    pcall(os.remove, resource_scan_detail_path)
    local request = { requestId = tostring(values.requestId or "") }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or not valid_token(request.requestId) then
        request.error = "resource_scan_detail_invalid"; return request
    end
    request.profileId = tostring(values.profileId or "")
    request.launchSessionId = tostring(values.launchSessionId or "")
    request.challenge = tostring(values.challenge or "")
    request.gamePid = tonumber(values.gamePid)
    request.serverId = tonumber(values.serverId)
    request.scanRunId = tostring(values.scanRunId or "")
    if not valid_token(request.profileId) or not valid_token(request.launchSessionId) or
       not valid_token(request.challenge) or not valid_token(request.scanRunId) or
       request.gamePid == nil or request.gamePid <= 0 or request.gamePid ~= math.floor(request.gamePid) or
       request.serverId == nil or request.serverId <= 0 or request.serverId ~= math.floor(request.serverId) then
        request.error = "resource_scan_detail_invalid"; return request
    end
    request.gamePid = math.floor(request.gamePid); request.serverId = math.floor(request.serverId)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "resource_scan_detail_identity_mismatch"
    end
    return request
end

function resource_scan_detail_runtime.rows(state)
    local manager = resource_detail_manager()
    local rows, ready = {}, 0
    if type(state) ~= "table" then return rows, ready end
    for index = 1, #state.targets do
        local target = state.targets[index]
        local detail = resource_detail_snapshot(manager, target.pointId)
        if detail ~= nil then ready = ready + 1 end
        rows[#rows + 1] = {
            recordKey = target.recordKey, received = detail ~= nil,
            resourceConfigId = target.resourceConfigId, resourceNameKey = target.resourceNameKey,
            resourceMaxAmount = target.resourceMaxAmount, detail = detail,
            requestIssued = target.requestIssued == true, cacheBefore = target.cacheBefore == true,
            sendFailed = target.sendFailed == true,
        }
    end
    return rows, ready
end

function resource_scan_detail_runtime.write_result(request, error_text, state)
    local rows, ready = resource_scan_detail_runtime.rows(state)
    write_json(resource_scan_detail_result_path, {
        schemaVersion = 1, probeVersion = M.VERSION, requestId = request.requestId,
        profileId = request.profileId, launchSessionId = request.launchSessionId,
        challenge = request.challenge, gamePid = request.gamePid, serverId = request.serverId,
        scanRunId = request.scanRunId, state = "completed", error = error_text,
        targetCount = type(state) == "table" and #state.targets or 0,
        requestCount = type(state) == "table" and state.requestCount or 0,
        cacheBeforeCount = type(state) == "table" and state.cacheBeforeCount or 0,
        sendFailureCount = type(state) == "table" and state.sendFailureCount or 0,
        readyCount = ready, details = rows,
        elapsedSeconds = resource_scan_detail_runtime.startedAt and (runtime_clock() - resource_scan_detail_runtime.startedAt) or 0,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

function resource_scan_detail_runtime.pump(now)
    if resource_scan_detail_runtime.request == nil then
        local request = resource_scan_detail_runtime.read(now)
        if request == nil then return false end
        resource_scan_detail_runtime.startedAt = runtime_clock()
        if request.error ~= nil then
            resource_scan_detail_runtime.write_result(request, request.error, nil)
            resource_scan_detail_runtime.startedAt = nil
            return true
        end
        if not resource_scan_detail_runtime.same_state(resource_scan_detail_runtime.state, request) then
            resource_scan_detail_runtime.write_result(request, "resource_scan_detail_runtime.state_unavailable", nil)
            resource_scan_detail_runtime.startedAt = nil
            return true
        end
        resource_scan_detail_runtime.request = request
    end
    local request = resource_scan_detail_runtime.request
    local state = resource_scan_detail_runtime.state
    local _, ready = resource_scan_detail_runtime.rows(state)
    if ready == #state.targets then
        resource_scan_detail_runtime.write_result(request, nil, state)
        resource_scan_detail_runtime.request = nil; resource_scan_detail_runtime.startedAt = nil; resource_scan_detail_runtime.state = nil
        return true
    end
    if runtime_clock() - (resource_scan_detail_runtime.startedAt or runtime_clock()) >= 8 then
        resource_scan_detail_runtime.write_result(request, "resource_scan_detail_partial_response", state)
        resource_scan_detail_runtime.request = nil; resource_scan_detail_runtime.startedAt = nil; resource_scan_detail_runtime.state = nil
        return true
    end
    return true
end

local function write_resource_detail_diagnostic_result(request, state, error_text, rows, ready_count)
    write_json(resource_detail_diagnostic_result_path, {
        schemaVersion = 1, probeVersion = M.VERSION, requestId = request.requestId,
        launchSessionId = request.launchSessionId, profileId = request.profileId,
        challenge = request.challenge, gamePid = request.gamePid, serverId = request.serverId,
        state = state, error = error_text, maxTargets = request.maxTargets,
        targetCount = request.targets and #request.targets or 0,
        requestCount = request.requestCount or 0, readyCount = ready_count or 0,
        cacheBeforeCount = request.cacheBeforeCount or 0,
        elapsedSeconds = resource_detail_started_at and (runtime_clock() - resource_detail_started_at) or 0,
        details = rows or {},
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function finish_resource_detail_diagnostic(request, error_text)
    local manager = resource_detail_manager()
    local rows, ready = resource_detail_result_rows(manager, request.targets or {})
    write_resource_detail_diagnostic_result(request, "completed", error_text, rows, ready)
    resource_detail_request = nil
    resource_detail_started_at = nil
    resource_detail_transition_requested = false
    resource_detail_refresh_requested = false
end

local function pump_resource_detail_diagnostic(now)
    if resource_detail_request == nil then
        local request = read_resource_detail_diagnostic(now)
        if request == nil then return false end
        resource_detail_started_at = runtime_clock()
        if request.error ~= nil then
            write_resource_detail_diagnostic_result(request, "completed", request.error, nil, 0)
            resource_detail_started_at = nil
            return true
        end
        resource_detail_request = request
    end

    local request = resource_detail_request
    if runtime_clock() - (resource_detail_started_at or runtime_clock()) > 15 then
        finish_resource_detail_diagnostic(request, "resource_detail_diagnostic_timeout")
        return true
    end

    local scene = scene_identity()
    if scene ~= "world" then
        if scene == "city" and not resource_detail_transition_requested then
            local scene_utils = rawget(_G, "SceneUtils")
            local change = scene_utils and safe_get(scene_utils, "ChangeToWorld") or nil
            if type(change) == "function" then
                local ok = pcall(change)
                if not ok then ok = pcall(change, scene_utils) end
                resource_detail_transition_requested = ok == true
            end
        end
        return true
    end

    local world, point_manager = runtime_world()
    if world == nil or point_manager == nil then return true end
    if request.targets == nil then
        local targets, target_note = find_resource_detail_targets(world, point_manager, request.serverId, request.maxTargets)
        if targets == nil then
            if not resource_detail_refresh_requested then
                local ok = call(point_manager, "UpdateViewRequest", true)
                resource_detail_refresh_requested = ok == true
                return true
            end
            if runtime_clock() - (resource_detail_started_at or runtime_clock()) < 8 then return true end
            finish_resource_detail_diagnostic(request, target_note)
            return true
        end
        request.targets = targets
        request.targetNote = target_note
        local manager = resource_detail_manager()
        request.cacheBeforeCount = select(2, resource_detail_result_rows(manager, targets))
        request.requestCount = 0
    end

    local manager = resource_detail_manager()
    if manager == nil then finish_resource_detail_diagnostic(request, "world_point_detail_manager_unavailable"); return true end
    if request.requestsIssued ~= true then
        local sfs, defs = rawget(_G, "SFSNetwork"), rawget(_G, "MsgDefines")
        local message = defs and safe_get(defs, "WorldGetDetail") or nil
        local send = sfs and safe_get(sfs, "SendMessage") or nil
        if message == nil or type(send) ~= "function" then
            finish_resource_detail_diagnostic(request, "world_get_detail_transport_unavailable"); return true
        end
        for index = 1, #request.targets do
            local target = request.targets[index]
            if resource_detail_snapshot(manager, target.pointId) == nil then
                local ok = pcall(send, message, target.pointId, target.serverId, target.worldId, 0, target.pointType, target.uid)
                if not ok then ok = pcall(send, sfs, message, target.pointId, target.serverId, target.worldId, 0, target.pointType, target.uid) end
                if ok then request.requestCount = request.requestCount + 1 end
            end
        end
        request.requestsIssued = true
        request.requestsIssuedAt = runtime_clock()
        return true
    end

    local _, ready = resource_detail_result_rows(manager, request.targets)
    if ready == #request.targets then finish_resource_detail_diagnostic(request, nil); return true end
    if runtime_clock() - (request.requestsIssuedAt or runtime_clock()) > 6 then
        finish_resource_detail_diagnostic(request, "world_get_detail_partial_response")
        return true
    end
    return true
end

local function doomsday_boss_records(world, block_size, block_count, selected_lookup, home_tile, existing_records)
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "LWDoomsdayManager") or nil
    local template_manager = data_center and safe_get(data_center, "MonsterTemplateManager") or nil
    if manager == nil or template_manager == nil then return {}, nil end
    local specials = rawget(_G, "WorldMonsterSpecialType")
    local super_running_boss = specials and safe_get(specials, "SuperRunningBoss") or nil
    if super_running_boss == nil then return nil, "WorldMonsterSpecialType.SuperRunningBoss unavailable" end

    local records, seen = {}, {}
    for index = 1, #(existing_records or {}) do
        local uuid = existing_records[index].uuid
        if uuid ~= nil and tostring(uuid) ~= "" then seen[tostring(uuid)] = true end
    end

    local function append_list(source_name, list)
        if type(list) ~= "table" then return nil end
        for _, boss in ipairs(list) do
            local raw_uuid = safe_get(boss, "uid")
            local uuid = raw_uuid ~= nil and tostring(raw_uuid) or ""
            local monster_id = tonumber(safe_get(boss, "monsterId"))
            local position_index = tonumber(safe_get(boss, "targetPos"))
            if uuid ~= "" and monster_id ~= nil and monster_id > 0 and
               position_index ~= nil and position_index > 0 then
                monster_id = math.floor(monster_id)
                position_index = math.floor(position_index)
                local ok_template, template = call(template_manager, "TryGetMonsterTemplate", monster_id)
                if ok_template and template ~= nil and
                   same_enum_value(scalar_field(template, { "special", "Special" }), super_running_boss) then
                    local tile = index_to_tile(world, position_index)
                    if tile == nil then return "doomsday_target_position_invalid" end
                    local cell_x = math.floor(tile.x / block_size)
                    local cell_y = math.floor(tile.y / block_size)
                    local aoi_index = cell_y * block_count + cell_x
                    if cell_x >= 0 and cell_y >= 0 and cell_x < block_count and cell_y < block_count and
                       selected_lookup[aoi_index] == true and seen[uuid] ~= true then
                        local config_id = integer_field(template, { "id", "Id" })
                        local name_key = scalar_field(template, { "name", "Name" })
                        local level = integer_field(template, { "level", "Level" })
                        if config_id == nil or config_id <= 0 or name_key == nil or tostring(name_key) == "" or level == nil then
                            return "doomsday_template_identity_incomplete"
                        end
                        seen[uuid] = true
                        records[#records + 1] = {
                            uuid = uuid,
                            kind = "monster",
                            runtimeClass = "LWDoomsdayManager.BossVO",
                            serverId = current_server_id(),
                            worldId = 0,
                            x = tile.x, y = tile.y, positionIndex = position_index,
                            distanceFromHome = tile_distance(world, home_tile, tile),
                            monsterId = monster_id,
                            monsterType = integer_field(template, { "type", "Type" }) or 0,
                            monsterSpecialType = tonumber(super_running_boss) or 32,
                            monsterRallyNum = safe_get(boss, "isRally") == true and 1 or 0,
                            configId = config_id,
                            monsterNameKey = tostring(name_key),
                            monsterLevel = level,
                            configType = integer_field(template, { "type", "Type" }),
                            configSpecial = integer_field(template, { "special", "Special" }) or 32,
                            configBoss = integer_field(template, { "boss", "Boss" }),
                            modelName = scalar_field(template, { "model_name", "modelName" }),
                            picture = scalar_field(template, { "pic", "Pic" }),
                            refreshTime = tonumber(safe_get(boss, "refreshTime")),
                            monsterProtectionEligible = false,
                            monsterProtectionKnown = false,
                            monsterProtectionActive = false,
                            isMonster = true,
                            isBoss = false,
                            isActBerserkBoss = false,
                            isOrdinaryBoss = false,
                            isWanderMonster = false,
                            isWanderBoss = false,
                            isZombieRushAltered = false,
                            source = "DataCenter.LWDoomsdayManager." .. source_name,
                        }
                    end
                end
            end
        end
        return nil
    end

    local error_text = append_list("theaterBosses", safe_get(manager, "theaterBosses"))
    if error_text ~= nil then return nil, error_text end
    error_text = append_list("allianceBosses", safe_get(manager, "allianceBosses"))
    if error_text ~= nil then return nil, error_text end
    return records, nil
end

local function request_doomsday_main_info()
    local sfs = rawget(_G, "SFSNetwork")
    local defs = rawget(_G, "MsgDefines")
    local message = defs and safe_get(defs, "ActivityDoomsdayMainInfo") or nil
    local send = sfs and safe_get(sfs, "SendMessage") or nil
    if message == nil or type(send) ~= "function" then return false, "doomsday_transport_unavailable" end
    local ok = pcall(send, message)
    if not ok then ok = pcall(send, sfs, message) end
    if ok then return true, nil end
    return false, "doomsday_send_failed"
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
                            monster_invasion_protection_snapshot(record_uuid, raw_record_uuid)
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
    local completion_error = bounded_enumerator_completion_error(
        collection, enumerator, scanned, MAX_POINTS, "march")
    if completion_error ~= nil then return nil, completion_error end
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
    if bulk_aoi_native_camera ~= nil and bulk_aoi_native_original_fov ~= nil then
        if not select(1, call(bulk_aoi_native_camera, "SetFOV", bulk_aoi_native_original_fov)) then ok = false end
    end
    if bulk_aoi_native_unity_camera ~= nil and bulk_aoi_native_original_aspect ~= nil then
        if not reflected_set_value(bulk_aoi_native_unity_camera, "aspect", bulk_aoi_native_original_aspect) then ok = false end
    end
    if bulk_aoi_native_camera ~= nil and bulk_aoi_native_original_zoom ~= nil then
        if not pcall(function() bulk_aoi_native_camera.Zoom = bulk_aoi_native_original_zoom end) then ok = false end
    end
    if bulk_aoi_native_touch_camera ~= nil and bulk_aoi_native_original_pos ~= nil then
        if not select(1, call(bulk_aoi_native_touch_camera, "SetCameraPos", bulk_aoi_native_original_pos)) then ok = false end
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
    bulk_aoi_native_unity_camera = nil
    bulk_aoi_native_original_aspect = nil
    bulk_aoi_native_original_zoom = nil
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
        matchedDispatchCount = details.matchedDispatchCount,
        matchedGhostCount = details.matchedGhostCount,
        matchedTreasureCount = details.matchedTreasureCount,
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
        doomsdayBossCount = details.doomsdayBossCount or 0,
        monsterInvasionBossCount = details.monsterInvasionBossCount or 0,
        monsterProtectionDetailTargetCount = details.monsterProtectionDetailTargetCount or 0,
        monsterProtectionDetailRequestCount = details.monsterProtectionDetailRequestCount or 0,
        monsterProtectionDetailReadyCount = details.monsterProtectionDetailReadyCount or 0,
        includeCity = request.includeCity == true,
        includeResource = request.includeResource == true,
        includeMonster = request.includeMonster == true,
        includeMonsterProtection = request.includeMonsterProtection == true,
        includeTrain = request.includeTrain == true,
        includeDispatch = request.includeDispatch == true,
        includeGhost = request.includeGhost == true,
        includeTreasure = request.includeTreasure == true,
        includeResourceDetails = request.includeResourceDetails == true,
        requestMethod = details.requestMethod or "WorldPointManager.SendAoiRequest(private-reflection)",
        zoomFinalBlockSize = details.zoomFinalBlockSize,
        zoomFinalBlockCount = details.zoomFinalBlockCount,
        zoomFinalServerLod = details.zoomFinalServerLod,
        zoomWholeWorldCoarse = details.zoomWholeWorldCoarse,
        restoredTileX = details.restoredTileX, restoredTileY = details.restoredTileY,
        restoredBlockSize = details.restoredBlockSize, restoredBlockCount = details.restoredBlockCount, restoredServerLod = details.restoredServerLod,
        zoomRecoveryAttempted = details.zoomRecoveryAttempted,
        zoomRecoveryMethod = details.zoomRecoveryMethod,
        zoomRecoveryIssued = details.zoomRecoveryIssued,
        zoomRecoveryError = details.zoomRecoveryError,
        zoomRecoveryBlockSize = details.zoomRecoveryBlockSize,
        zoomRecoveryBlockCount = details.zoomRecoveryBlockCount,
        zoomRecoveryServerLod = details.zoomRecoveryServerLod,
        registrationMethod = registration_method,
        anchorDebug = details.anchorDebug,
        expandedAnchorCells = details.expandedAnchorCells,
        postInvokeAddListCount = details.postInvokeAddListCount,
        postInvokeSplitPending = details.postInvokeSplitPending,
        postInvokeMsgViewCount = details.postInvokeMsgViewCount,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function try_recover_zoom_aoi_geometry(point_manager, details)
    details = details or {}
    details.zoomRecoveryAttempted = true
    local ok, _, error_text = reflected_call_bool(point_manager, "UpdateLWAoi_Normal", true)
    if ok then
        details.zoomRecoveryMethod = "WorldPointManager.UpdateLWAoi_Normal(true)-reflection"
        details.zoomRecoveryIssued = true
    else
        local direct_ok = select(1, call(point_manager, "UpdateViewRequest", true))
        details.zoomRecoveryMethod = "WorldPointManager.UpdateViewRequest(true)"
        details.zoomRecoveryIssued = direct_ok == true
        details.zoomRecoveryError = direct_ok and nil or tostring(error_text or "zoom_recovery_update_failed")
    end
    details.zoomRecoveryBlockSize = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
    details.zoomRecoveryBlockCount = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
    details.zoomRecoveryServerLod = integer_field(point_manager, { "svLod" })
    return details.zoomRecoveryIssued == true
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
        if request.includeMonster == true and request.includeMonsterProtection ~= true and
           M._doomsdayMainInfoScanRunId ~= request.scanRunId then
            request.doomsdayRequestSent = select(1, request_doomsday_main_info())
            request.doomsdayRequestSentAt = runtime_clock()
            if request.doomsdayRequestSent == true then
                M._doomsdayMainInfoScanRunId = request.scanRunId
            end
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
        bulk_aoi_native_hold_seconds = request.requestMode == "zoom" and nil or (request.holdMilliseconds / 1000.0)
        bulk_aoi_native_position_restored = false
        bulk_aoi_skip_restore_update = request.requestMode == "coverage" or request.requestMode == "anchor" or request.requestMode == "edge"
        local expanded_anchor_cells = nil
        local anchor_debug = nil
        local zoom_debug = nil
        if request.requestMode == "anchor" then
            local anchors = safe_get(camera_manager, "cameraAnchor") or reflected_value(camera_manager, "cameraAnchor")
            local cs = rawget(_G, "CS")
            local vector2_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int or nil
            local scene_utils = rawget(_G, "SceneUtils")
            local tile_to_world = scene_utils and safe_get(scene_utils, "TileToWorld") or nil
            local force_change_scene = rawget(_G, "ForceChangeScene")
            local force_world = force_change_scene and safe_get(force_change_scene, "World") or nil
            if anchors == nil or vector2_type == nil or type(tile_to_world) ~= "function" or force_world == nil then
                fail_bulk_aoi(request, "anchor_rectangle_dependencies_unavailable", nil, point_manager)
                return true
            end
            -- 14x8 visible AOI cells. UpdateLWAoi_Normal adds its recovered EDGE margin;
            -- this is intentionally bounded below the native once_max_request_count=160.
            local width_cells, height_cells = 14, 8
            local target_cell_x = math.floor(request.targetTileX / block_size)
            local target_cell_y = math.floor(request.targetTileY / block_size)
            local start_cell_x = math.max(0, math.min(block_count - width_cells, target_cell_x - math.floor(width_cells / 2)))
            local start_cell_y = math.max(0, math.min(block_count - height_cells, target_cell_y - math.floor(height_cells / 2)))
            local min_tile_x = start_cell_x * block_size
            local min_tile_y = start_cell_y * block_size
            local max_tile_x = math.min(999, ((start_cell_x + width_cells) * block_size) - 1)
            local max_tile_y = math.min(999, ((start_cell_y + height_cells) * block_size) - 1)
            local function anchor_world(tx, ty)
                local ok, value = pcall(tile_to_world, vector2_type(tx, ty), force_world, request.serverId)
                if not ok then return nil end
                return value
            end
            local original_anchor_values = {}
            for index = 0, 3 do
                local value = safe_get(anchors, index)
                original_anchor_values[#original_anchor_values + 1] = {
                    x = tonumber(value and safe_get(value, "x")),
                    y = tonumber(value and safe_get(value, "y")),
                    z = tonumber(value and safe_get(value, "z")),
                }
            end
            local values = {
                anchor_world(min_tile_x, min_tile_y),
                anchor_world(min_tile_x, max_tile_y),
                anchor_world(max_tile_x, max_tile_y),
                anchor_world(max_tile_x, min_tile_y),
            }
            if values[1] == nil or values[2] == nil or values[3] == nil or values[4] == nil then
                fail_bulk_aoi(request, "anchor_rectangle_world_conversion_failed", nil, point_manager)
                return true
            end
            for index = 0, 3 do
                local ok = pcall(function() anchors[index] = values[index + 1] end)
                if not ok then
                    fail_bulk_aoi(request, "anchor_rectangle_assignment_failed", nil, point_manager)
                    return true
                end
            end
            local assigned_anchor_values = {}
            for index = 0, 3 do
                local value = safe_get(anchors, index)
                assigned_anchor_values[#assigned_anchor_values + 1] = {
                    x = tonumber(value and safe_get(value, "x")),
                    y = tonumber(value and safe_get(value, "y")),
                    z = tonumber(value and safe_get(value, "z")),
                }
            end
            expanded_anchor_cells = { start_cell_x, start_cell_y, width_cells, height_cells }
            anchor_debug = {
                minTileX = min_tile_x, minTileY = min_tile_y, maxTileX = max_tile_x, maxTileY = max_tile_y,
                original = original_anchor_values, assigned = assigned_anchor_values,
            }
        end
        if request.requestMode == "zoom" then
            local original_zoom = tonumber(safe_get(camera_manager, "Zoom"))
            local zoom_max = tonumber(safe_get(camera_manager, "ZoomMax"))
            if original_zoom == nil or zoom_max == nil or zoom_max <= 0 then
                fail_bulk_aoi(request, "zoom_state_unavailable", nil, point_manager)
                return true
            end
            bulk_aoi_native_original_zoom = original_zoom
            local ok_zoom = pcall(function() camera_manager.Zoom = zoom_max end)
            if not ok_zoom or not select(1, call(camera_manager, "RefreshCameraAnchor")) then
                fail_bulk_aoi(request, "zoom_override_failed", nil, point_manager)
                return true
            end
            zoom_debug = { originalZoom = original_zoom, zoomMax = zoom_max, appliedZoom = tonumber(safe_get(camera_manager, "Zoom")) }
        end
        if request.requestMode == "expanded" or request.requestMode == "coverage" then
            local unity_camera = safe_get(camera_manager, "__camera") or reflected_value(camera_manager, "camera")
            local original_fov = tonumber(unity_camera and safe_get(unity_camera, "fieldOfView"))
            if original_fov == nil or original_fov <= 0 then
                fail_bulk_aoi(request, "expanded_camera_fov_unavailable", nil, point_manager)
                return true
            end
            local original_aspect = tonumber(unity_camera and safe_get(unity_camera, "aspect"))
            if original_aspect == nil or original_aspect <= 0 then
                fail_bulk_aoi(request, "expanded_camera_aspect_unavailable", nil, point_manager)
                return true
            end
            bulk_aoi_native_original_fov = original_fov
            bulk_aoi_native_unity_camera = unity_camera
            bulk_aoi_native_original_aspect = original_aspect
            if not select(1, call(camera_manager, "SetFOV", 120.0)) then
                fail_bulk_aoi(request, "expanded_camera_fov_set_failed", nil, point_manager)
                return true
            end
            if not reflected_set_value(unity_camera, "aspect", 4.0) then
                fail_bulk_aoi(request, "expanded_camera_aspect_set_failed", nil, point_manager)
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
            if request.requestMode == "anchor" then
                invoked = select(1, reflected_call(point_manager, "UpdateLWAoi_Normal", true))
                request_method = "WorldPointManager.UpdateLWAoi_Normal(true)+synthetic-camera-anchor"
                call(camera_manager, "RefreshCameraAnchor")
            elseif request.requestMode == "edge" then
                local original_edge = tonumber(reflected_value(point_manager, "EDGE"))
                request.details = request.details or {}
                request.details.originalEdge = original_edge
                request.details.testEdge = 8
                if original_edge == nil or not reflected_set_value(point_manager, "EDGE", 8) then
                    fail_bulk_aoi(request, "edge_override_unavailable", nil, point_manager)
                    return true
                end
                invoked = select(1, call(point_manager, "UpdateViewRequest", true))
                reflected_set_value(point_manager, "EDGE", original_edge)
                request_method = "WorldPointManager.UpdateViewRequest(true)+temporary-edge-8"
            else
                invoked = select(1, call(point_manager, "UpdateViewRequest", true))
            end
        end
        if not invoked then
            call(camera_manager, "RefreshCameraAnchor")
            fail_bulk_aoi(request, "native_remote_request_failed", nil, point_manager)
            return true
        end
        -- LWB-R7-014 IMPLEMENTATION POLICY: production coverage uses a zero hold.
        -- Restore the visible camera transform in the same Lua callback that queues the
        -- remote request, instead of waiting for the next 250 ms pump/frame.
        if request.requestMode ~= "zoom" and request.holdMilliseconds == 0 and bulk_aoi_native_touch_camera ~= nil and
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
            if bulk_aoi_native_unity_camera ~= nil and bulk_aoi_native_original_aspect ~= nil and
               not reflected_set_value(bulk_aoi_native_unity_camera, "aspect", bulk_aoi_native_original_aspect) then
                fail_bulk_aoi(request, "native_camera_aspect_same_tick_restore_failed", nil, point_manager)
                return true
            end
            if bulk_aoi_native_camera ~= nil and bulk_aoi_native_original_zoom ~= nil then
                local ok_zoom = pcall(function() bulk_aoi_native_camera.Zoom = bulk_aoi_native_original_zoom end)
                if not ok_zoom then
                    fail_bulk_aoi(request, "native_camera_zoom_same_tick_restore_failed", nil, point_manager)
                    return true
                end
            end
            bulk_aoi_native_position_restored = true
            bulk_aoi_native_hold_seconds = nil
            if request_method == "WorldPointManager.UpdateViewRequest(true)+held-internal-camera-shift" then
                request_method = "WorldPointManager.UpdateViewRequest(true)+same-tick-camera-restore"
            elseif request_method == "WorldPointManager.UpdateViewRequest(true)+synthetic-camera-anchor" then
                request_method = "WorldPointManager.UpdateViewRequest(true)+synthetic-camera-anchor+same-tick-camera-restore"
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
            anchorDebug = anchor_debug,
            zoomDebug = zoom_debug,
            expandedAnchorCells = expanded_anchor_cells,
            postInvokeAddListCount = collection_count(add_list),
            postInvokeSplitPending = reflected_value(point_manager, "_splitLastAOIRequest"),
            postInvokeMsgViewCount = collection_count(reflected_value(point_manager, "_msgViewIndex")),
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
    if bulk_aoi_request.requestMode == "zoom" and details.zoomRestorePending == true then
        local restored_tile = safe_get(world, "CurTilePosClamped")
        details.restoredTileX = tonumber(restored_tile and (safe_get(restored_tile, "x") or safe_get(restored_tile, "X")))
        details.restoredTileY = tonumber(restored_tile and (safe_get(restored_tile, "y") or safe_get(restored_tile, "Y")))
        details.restoredBlockSize = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
        details.restoredBlockCount = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
        details.restoredServerLod = integer_field(point_manager, { "svLod" })
        details.cameraTileStable = details.restoredTileX == details.preTileX and details.restoredTileY == details.preTileY
        if details.cameraTileStable == true and details.restoredBlockSize == details.blockSize and
           details.restoredBlockCount == details.blockCount and details.restoredServerLod == details.serverLod then
            details.zoomRestorePending = false
            details.positionRestoredBeforeResponse = true
            write_bulk_aoi_result(bulk_aoi_request, "proven", nil, details)
            bulk_aoi_request = nil; bulk_aoi_started_at = nil
            return true
        end
        if details.zoomRestoreStartedAt ~= nil and runtime_clock() - details.zoomRestoreStartedAt >= 3 then
            if details.zoomRecoveryAttempted ~= true then
                try_recover_zoom_aoi_geometry(point_manager, details)
                details.zoomRestoreStartedAt = runtime_clock()
                bulk_aoi_request.details = details
                return true
            end
            write_bulk_aoi_result(bulk_aoi_request, "failed", "zoom_restore_confirmation_timeout", details)
            bulk_aoi_request = nil; bulk_aoi_started_at = nil
            return true
        end
        return true
    end
    local elapsed_now = bulk_aoi_started_at and (runtime_clock() - bulk_aoi_started_at) or 0
    if bulk_aoi_request.requestMode ~= "zoom" and not bulk_aoi_native_position_restored and bulk_aoi_native_hold_seconds ~= nil and
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
        if bulk_aoi_native_unity_camera ~= nil and bulk_aoi_native_original_aspect ~= nil and
           not reflected_set_value(bulk_aoi_native_unity_camera, "aspect", bulk_aoi_native_original_aspect) then
            fail_bulk_aoi(bulk_aoi_request, "native_camera_aspect_timed_restore_failed", details, point_manager)
            return true
        end
        bulk_aoi_native_position_restored = true
        details.positionRestoreElapsedSeconds = elapsed_now
    end
    details.positionRestoredBeforeResponse = bulk_aoi_native_position_restored == true
    if bulk_aoi_request.requestMode == "zoom" then
        local split_pending_now = reflected_value(point_manager, "_splitLastAOIRequest") == true
        details.splitPending = split_pending_now
        details.splitAddCount = collection_count(reflected_value(point_manager, "_addViewIndex"))
        details.splitCurrentCount = collection_count(reflected_value(point_manager, "_curViewIndex"))
        details.splitMsgCount = collection_count(reflected_value(point_manager, "_msgViewIndex"))
        local flags_now = bulk_manager_flags(point_manager)
        if split_pending_now then
            if flags_now.isRecvViewPoints == true and world_response_flag(world) == true then
                call(point_manager, "UpdateViewRequest", true)
            end
            if bulk_aoi_started_at ~= nil and runtime_clock() - bulk_aoi_started_at >= 20 then
                fail_bulk_aoi(bulk_aoi_request, "zoom_split_drain_timeout", details, point_manager)
            end
            return true
        end
        if flags_now.isRecvViewPoints ~= true or world_response_flag(world) ~= true then return true end
        local final_block_size = integer_field(point_manager, { "_lwAoiBlockSize", "lwAoiBlockSize" })
        local final_block_count = integer_field(point_manager, { "_lwAoiBlockCount", "lwAoiBlockCount" })
        local final_server_lod = integer_field(point_manager, { "svLod" })
        details.zoomFinalBlockSize = final_block_size
        details.zoomFinalBlockCount = final_block_count
        details.zoomFinalServerLod = final_server_lod
        if final_block_size == 1000 and final_block_count == 1 then
            local full = {}
            for index = 0, 9999 do full[#full + 1] = index end
            details.requestedIndices = full
            details.nativeCurrentSetCount = 1
            details.zoomWholeWorldCoarse = true
        else
            local final_current = reflected_value(point_manager, "_curViewIndex")
            local final_indices = final_current and select(1, collection_int_values(final_current, 10000)) or nil
            if final_indices == nil or #final_indices == 0 then
                fail_bulk_aoi(bulk_aoi_request, "zoom_final_view_indices_unavailable", details, point_manager)
                return true
            end
            details.requestedIndices = final_indices
            details.nativeCurrentSetCount = #final_indices
        end
    end
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
    details.matchedDispatchCount = observed.dispatches
    details.matchedGhostCount = observed.ghosts
    details.matchedTreasureCount = observed.treasures
    details.afterLoadedPointCount = observed.loadedPointCount
    local point_records = {}
    if bulk_aoi_request.includeCity == true then
        local city_records, point_records_error = city_aoi_records(
            world, point_manager, details.blockSize, details.blockCount, lookup)
        if city_records == nil then
            fail_bulk_aoi(bulk_aoi_request, point_records_error, details, point_manager)
            return true
        end
        -- City count follows the retained unique-main-base source, not the current
        -- response delta in _pointInfos.
        details.matchedCityCount = #city_records
        for index = 1, #city_records do point_records[#point_records + 1] = city_records[index] end
    else
        details.matchedCityCount = 0
    end
    if bulk_aoi_request.includeResource == true then
        local resource_records, resource_records_error = resource_aoi_records(
            world, point_manager, details.blockSize, details.blockCount, lookup, bulk_aoi_request)
        if resource_records == nil then
            fail_bulk_aoi(bulk_aoi_request, resource_records_error, details, point_manager)
            return true
        end
        details.matchedResourceCount = #resource_records
        for index = 1, #resource_records do point_records[#point_records + 1] = resource_records[index] end
    else
        details.matchedResourceCount = 0
    end
    if bulk_aoi_request.includeDispatch == true then
        local dispatch_records, dispatch_records_error = dispatch_aoi_records(
            world, point_manager, details.blockSize, details.blockCount, lookup)
        if dispatch_records == nil then
            fail_bulk_aoi(bulk_aoi_request, dispatch_records_error, details, point_manager)
            return true
        end
        for index = 1, #dispatch_records do point_records[#point_records + 1] = dispatch_records[index] end
    end
    if bulk_aoi_request.includeGhost == true then
        local ghost_records, ghost_records_error = ghost_aoi_records(
            world, point_manager, details.blockSize, details.blockCount, lookup)
        if ghost_records == nil then
            fail_bulk_aoi(bulk_aoi_request, ghost_records_error, details, point_manager)
            return true
        end
        for index = 1, #ghost_records do point_records[#point_records + 1] = ghost_records[index] end
    end
    if bulk_aoi_request.includeTreasure == true then
        local treasure_records, treasure_records_error = treasure_aoi_records(
            world, point_manager, details.blockSize, details.blockCount, lookup)
        if treasure_records == nil then
            fail_bulk_aoi(bulk_aoi_request, treasure_records_error, details, point_manager)
            return true
        end
        for index = 1, #treasure_records do point_records[#point_records + 1] = treasure_records[index] end
    end
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
        -- Current-v20 Doom Walker / SuperRunningBoss is owned by the Doomsday
        -- activity manager and may not yet exist in WorldMarchDataManager. Issue
        -- the same read-only main-info request used by UIDoomsday and give that
        -- response a short bounded settle before taking the whole-world snapshot.
        if bulk_aoi_request.includeMonsterProtection ~= true and
           bulk_aoi_request.doomsdayRequestSent == true and
           runtime_clock() - (bulk_aoi_request.doomsdayRequestSentAt or runtime_clock()) < 0.75 then
            return true
        end
        if bulk_aoi_request.requestMode == "coverage" or bulk_aoi_request.requestMode == "anchor" then
            local response_flags = bulk_manager_flags(point_manager)
            if response_flags.isRecvViewPoints ~= true or world_response_flag(world) ~= true then return true end
        end
        local home_tile = bulk_aoi_request.homeTileX >= 0 and
            { x = bulk_aoi_request.homeTileX, y = bulk_aoi_request.homeTileY } or nil
        local monster_march_records, monster_march_records_error = monster_march_aoi_records(
            world, details.blockSize, details.blockCount, lookup, home_tile)
        if monster_march_records == nil then
            fail_bulk_aoi(bulk_aoi_request, monster_march_records_error, details, point_manager)
            return true
        end
        details.doomsdayBossCount = 0
        if bulk_aoi_request.includeMonsterProtection ~= true then
            local doomsday_records, doomsday_error = doomsday_boss_records(
                world, details.blockSize, details.blockCount, lookup, home_tile, monster_march_records)
            if doomsday_records == nil then
                fail_bulk_aoi(bulk_aoi_request, doomsday_error, details, point_manager)
                return true
            end
            details.doomsdayBossCount = #doomsday_records
            for index = 1, #doomsday_records do
                monster_march_records[#monster_march_records + 1] = doomsday_records[index]
            end
        end
        details.monsterMarchRecords = monster_march_records

        local boss_count = 0
        for index = 1, #monster_march_records do
            if monster_march_records[index].monsterProtectionEligible == true then
                boss_count = boss_count + 1
            end
        end
        details.monsterInvasionBossCount = boss_count
        details.monsterProtectionDetailTargetCount = 0
        details.monsterProtectionDetailRequestCount = 0
        details.monsterProtectionDetailReadyCount = 0
        details.monsterProtectionDetailError = nil

        if bulk_aoi_request.includeMonsterProtection == true then
            -- Dedicated Zombie Boss scans reproduce the original popup request
            -- while each boss is still loaded. Generic Monster scans intentionally
            -- skip this network phase so repeated Monster discovery remains fast.
            local targets, total, target_error = monster_invasion_protection_targets(
                world, details.blockSize, details.blockCount, lookup, bulk_aoi_request.serverId)
            if targets == nil then fail_bulk_aoi(bulk_aoi_request, target_error, details, point_manager); return true end
            if total ~= boss_count or #targets ~= boss_count then
                fail_bulk_aoi(bulk_aoi_request, "monster_protection_target_count_mismatch", details, point_manager)
                return true
            end
            local queued, queue_error = queue_monster_invasion_protection_requests(bulk_aoi_request, targets)
            details.monsterProtectionDetailTargetCount = #targets
            details.monsterProtectionDetailRequestCount = queued or 0
            details.monsterProtectionDetailReadyCount = count_ready_monster_invasion_protection_details(targets)
            details.monsterProtectionDetailError = queue_error
        end
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
    if bulk_aoi_request.requestMode == "coverage" or bulk_aoi_request.requestMode == "anchor" or
       bulk_aoi_request.requestMode == "zoom" then
        request_completed = details.responseFlagsTransitioned == true and
            reflected_value(point_manager, "_splitLastAOIRequest") ~= true
    end
    if request_completed then
        local request = bulk_aoi_request
        -- Zoom intentionally holds the temporary camera/LOD state until the coarse
        -- whole-world response has been serialized. Restoration must then run one
        -- normal view update at the original camera state so CurTilePosClamped and
        -- the native LOD0 view state are restored together, not just the transform.
        if request.requestMode == "zoom" then bulk_aoi_skip_restore_update = false end
        local restored = restore_bulk_aoi_state(point_manager)
        if restored and request.requestMode == "zoom" then
            details.zoomRestorePending = true
            details.zoomRestoreStartedAt = runtime_clock()
            request.details = details
            return true
        end
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

function train_list_runtime.cleanup()
    if train_list_runtime.eventManager ~= nil and train_list_runtime.eventId ~= nil and train_list_runtime.listener ~= nil then
        call(train_list_runtime.eventManager, "RemoveListener", train_list_runtime.eventId, train_list_runtime.listener)
    end
    train_list_runtime.request = nil
    train_list_runtime.startedAt = nil
    train_list_runtime.manager = nil
    train_list_runtime.eventManager = nil
    train_list_runtime.eventId = nil
    train_list_runtime.listener = nil
    train_list_runtime.refreshObserved = false
    train_list_runtime.refreshArgument = nil
end

function train_list_runtime.read(now)
    local values = read_kv_file(train_list_runtime.path, 4096)
    if values == nil then return nil end
    pcall(os.remove, train_list_runtime.path)
    local request = {
        requestId = tostring(values.requestId or ""),
        profileId = tostring(values.profileId or ""),
        launchSessionId = tostring(values.launchSessionId or ""),
        challenge = tostring(values.challenge or ""),
        gamePid = tonumber(values.gamePid),
        serverId = tonumber(values.serverId),
        scanRunId = tostring(values.scanRunId or ""),
    }
    if values.schema ~= "1" or values.probeVersion ~= M.VERSION or
       not valid_token(request.requestId) or not valid_token(request.profileId) or
       not valid_token(request.launchSessionId) or not valid_token(request.challenge) or
       not valid_token(request.scanRunId) or request.gamePid == nil or request.gamePid <= 0 or
       request.gamePid ~= math.floor(request.gamePid) or request.serverId == nil or
       request.serverId <= 0 or request.serverId > 99999 or request.serverId ~= math.floor(request.serverId) then
        request.error = "train_list_diagnostic_invalid"
        return request
    end
    request.gamePid = math.floor(request.gamePid)
    request.serverId = math.floor(request.serverId)
    local identity, identity_error = active_overview_identity(now)
    if identity == nil then request.error = identity_error; return request end
    if request.profileId ~= identity.profileId or request.launchSessionId ~= identity.sessionId or
       request.challenge ~= identity.challenge or request.gamePid ~= identity.gamePid then
        request.error = "train_list_diagnostic_identity_mismatch"
        return request
    end
    local live_server = current_server_id()
    if live_server == nil or math.floor(live_server) ~= request.serverId then
        request.error = "train_list_diagnostic_server_mismatch"
    end
    return request
end

function train_list_runtime.worldMarchPositions(world)
    local positions = {}
    if world == nil then return positions end
    local march_manager = safe_get(world, "MarchDataManager") or reflected_value(world, "<MarchDataManager>k__BackingField")
    if march_manager == nil then
        local ok_manager, value = call(world, "get_MarchDataManager")
        if ok_manager then march_manager = value end
    end
    if march_manager == nil then return positions end
    local ok_all, collection = call(march_manager, "GetAllMarchesByCS")
    if not ok_all or collection == nil then collection = reflected_value(march_manager, "allMarches") end
    if collection == nil then return positions end
    each(collection, MAX_POINTS, function(raw)
        local pair = safe_get(raw, "Value") or raw
        local march = pair
        local uuid = march and scalar_field(march, { "uuid", "Uuid", "_uuid" }) or nil
        if uuid == nil then return true end
        local key = tostring(uuid)
        if key == "" then return true end
        local ok_index, position_index = call(march, "GetMarchCurPosIndex")
        local index = ok_index and tonumber(position_index) or nil
        if index == nil or index <= 0 then index = integer_field(march, { "targetPos", "TargetPos" }) end
        local tile = index and index > 0 and index_to_tile(world, index) or nil
        if tile ~= nil then
            positions[key] = {
                x = tile.x, y = tile.y, positionIndex = math.floor(index),
                worldId = integer_field(march, { "worldId", "WorldId" }) or 0,
                ownerUid = scalar_field(march, { "ownerUid", "OwnerUid" }),
                ownerName = scalar_field(march, { "ownerName", "OwnerName" }),
                allianceUid = scalar_field(march, { "allianceUid", "AllianceUid" }),
                allianceName = scalar_field(march, { "allianceName", "AllianceName" }),
                allianceAbbr = scalar_field(march, { "allianceAbbr", "AllianceAbbr" }),
                power = scalar_field(march, { "power", "Power" }),
            }
        end
        return true
    end)
    return positions
end

function train_list_runtime.currentTile(train_data, world, world_positions, march_uuid)
    local from_march = world_positions[march_uuid]
    if from_march ~= nil then return from_march end
    local ui_time = rawget(_G, "UITimeManager")
    local ok_time_manager, time_manager = call(ui_time, "GetInstance")
    if not ok_time_manager or time_manager == nil then return nil, "ui_time_manager_unavailable" end
    local ok_time, server_time = call(time_manager, "GetServerTime")
    if not ok_time or tonumber(server_time) == nil then return nil, "server_time_unavailable" end
    local ok_pos, world_pos = call(train_data, "CalculateTransform", tonumber(server_time))
    if not ok_pos or world_pos == nil then return nil, "train_calculate_transform_failed" end
    local scene_utils = rawget(_G, "SceneUtils")
    if scene_utils == nil then
        local ok_require, value = pcall(require, "Util.SceneUtils")
        if ok_require then scene_utils = value end
    end
    local world_to_tile = scene_utils and safe_get(scene_utils, "WorldToTile") or nil
    if type(world_to_tile) ~= "function" then return nil, "scene_utils_world_to_tile_unavailable" end
    local ok_tile, tile = pcall(world_to_tile, world_pos)
    if not ok_tile or tile == nil then return nil, "scene_utils_world_to_tile_failed" end
    local x = tonumber(safe_get(tile, "x") or safe_get(tile, "X"))
    local y = tonumber(safe_get(tile, "y") or safe_get(tile, "Y"))
    if x == nil or y == nil then return nil, "train_tile_invalid" end
    x = math.floor(x); y = math.floor(y)
    if x < 0 or x >= 1000 or y < 0 or y >= 1000 then return nil, "train_tile_out_of_bounds" end
    local vector_type = rawget(_G, "CS") and CS.UnityEngine and CS.UnityEngine.Vector2Int or nil
    local position_index = nil
    if vector_type ~= nil and world ~= nil then
        local ok_index, raw_index = call(world, "TilePosToIndex", vector_type(x, y))
        if ok_index and tonumber(raw_index) ~= nil then position_index = math.floor(tonumber(raw_index)) end
    end
    return { x = x, y = y, positionIndex = position_index, worldId = 0 }, nil
end

function train_list_runtime.snapshot(request)
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "LWTrainDataManager") or nil
    if manager == nil then return nil, "lw_train_data_manager_unavailable" end
    local enemy_trucks = safe_get(manager, "enemyTrucks")
    local enemy_trains = safe_get(manager, "enemyTrains")
    if type(enemy_trucks) ~= "table" then return nil, "enemy_trucks_unavailable" end
    if type(enemy_trains) ~= "table" then return nil, "enemy_trains_unavailable" end
    local world = select(1, runtime_world())
    if world == nil then return nil, "world_unavailable" end
    local world_positions = train_list_runtime.worldMarchPositions(world)
    local train_type_enum = rawget(_G, "TrainType")
    local official_truck_type = train_type_enum and tonumber(safe_get(train_type_enum, "Truck")) or nil
    local official_train_type = train_type_enum and tonumber(safe_get(train_type_enum, "Train")) or nil
    if official_truck_type == nil or official_train_type == nil then return nil, "train_type_enum_unavailable" end
    local rows = {}
    local summary = {
        truckSourceCount = #enemy_trucks,
        railwaySourceCount = #enemy_trains,
        truckServerIds = {},
        railwayServerIds = {},
        matchServerIds = {},
    }
    local station_manager = data_center and safe_get(data_center, "LWMyStationDataManager") or nil
    local match_servers = station_manager and safe_get(station_manager, "matchServers") or nil
    if type(match_servers) == "table" then
        for server_id, covered in pairs(match_servers) do
            local numeric = tonumber(server_id)
            if covered and numeric ~= nil and numeric > 0 then
                summary.matchServerIds[#summary.matchServerIds + 1] = math.floor(numeric)
            end
        end
        table.sort(summary.matchServerIds)
    end
    local truck_server_set = {}
    local railway_server_set = {}

    local function append_train_rows(source_rows, required_type, source_name, server_set)
        for _, train_data in ipairs(source_rows) do
            local train_type = integer_field(train_data, { "type", "Type" })
            local source_server = integer_field(train_data, { "serverId", "ServerId" })
            if train_type == required_type and source_server ~= nil and source_server > 0 then
                server_set[source_server] = true
            end
            if train_type == required_type and source_server == request.serverId then
                local train_uuid_value = scalar_field(train_data, { "uuid", "Uuid" })
                local march_uuid_value = scalar_field(train_data, { "marchUid", "MarchUid", "marchUuid", "MarchUuid" })
                local train_uuid = train_uuid_value ~= nil and tostring(train_uuid_value) or ""
                local march_uuid = march_uuid_value ~= nil and tostring(march_uuid_value) or ""
                if train_uuid ~= "" and march_uuid ~= "" then
                    local position, position_error = train_list_runtime.currentTile(train_data, world, world_positions, march_uuid)
                    if position == nil then return false, position_error end
                    local quality = integer_field(train_data, { "quality", "Quality" })
                    local cfg_id = integer_field(train_data, { "cfgId", "CfgId" })
                    local carriage_num = integer_field(train_data, { "carriageCount", "CarriageCount" })
                    if quality == nil or quality < 1 or cfg_id == nil or carriage_num == nil then
                        return false, "train_list_required_metadata_unavailable"
                    end
                    local march_info = safe_get(train_data, "marchInfo")
                    local train_data_json = nil
                    if required_type == official_train_type then
                        local ok_json, value = call(train_data, "ToJson")
                        if ok_json and value ~= nil then train_data_json = tostring(value) end
                    end
                    local current_goods, max_loot_count = normalize_train_current_goods(nil, nil, train_data_json, train_data)
                    local from_march = world_positions[march_uuid]
                    local vip_value = march_info and safe_get(march_info, "vipOn") or safe_get(train_data, "vipOn")
                    local row = {
                        uuid = march_uuid,
                        marchUuid = march_uuid,
                        runtimeClass = "TrainData",
                        serverId = request.serverId,
                        worldId = position.worldId or 0,
                        x = position.x, y = position.y, positionIndex = position.positionIndex,
                        ownerUid = (from_march and from_march.ownerUid) or scalar_field(train_data, { "ownerId", "OwnerId" }),
                        ownerName = (from_march and from_march.ownerName) or scalar_field(train_data, { "name", "Name" }),
                        allianceUid = (from_march and from_march.allianceUid) or scalar_field(train_data, { "allianceId", "AllianceId" }),
                        allianceName = (from_march and from_march.allianceName) or scalar_field(train_data, { "allianceName", "AllianceName" }),
                        allianceAbbr = (from_march and from_march.allianceAbbr) or scalar_field(train_data, { "abbr", "Abbr" }),
                        ownerServer = source_server,
                        power = (from_march and from_march.power) or scalar_field(train_data, { "power", "Power", "ownerPower", "OwnerPower" }),
                        startTime = scalar_field(train_data, { "departureTs", "sendTime", "StartTime" }),
                        endTime = scalar_field(train_data, { "arriveTs", "arriveTime", "EndTime" }),
                        trainUuid = train_uuid,
                        trainCfgId = cfg_id,
                        trainType = train_type,
                        trainQuality = quality,
                        carriageNum = carriage_num,
                        arriveTs = scalar_field(train_data, { "arriveTs", "arriveTime", "ArriveTs", "ArriveTime" }),
                        robTimes = march_info and integer_field(march_info, { "robTimes", "RobTimes" }) or nil,
                        protectTime = march_info and scalar_field(march_info, { "protectTime", "ProtectTime" }) or nil,
                        trainDataJson = train_data_json,
                        currentGoods = current_goods,
                        maxLootCount = max_loot_count,
                        source = source_name,
                    }
                    if required_type == official_truck_type then
                        row.truckMetadataKnown = true
                        row.truckCurrentGoodsRaw = current_goods
                        row.truckMaxLootCount = max_loot_count
                        row.truckVipOn = vip_value == true or tonumber(vip_value) == 1
                    end
                    rows[#rows + 1] = row
                end
            end
        end
        return true, nil
    end

    local ok_trucks, truck_error = append_train_rows(
        enemy_trucks, official_truck_type,
        "DataCenter.LWTrainDataManager.TryGetTrainList(true)+OnTrainListGet(ls)",
        truck_server_set)
    if not ok_trucks then return nil, truck_error end
    local ok_trains, train_error = append_train_rows(
        enemy_trains, official_train_type,
        "DataCenter.LWTrainDataManager.TryGetTrainList(true)+OnTrainListGet(allianceTrainList)",
        railway_server_set)
    if not ok_trains then return nil, train_error end
    for server_id in pairs(truck_server_set) do summary.truckServerIds[#summary.truckServerIds + 1] = server_id end
    for server_id in pairs(railway_server_set) do summary.railwayServerIds[#summary.railwayServerIds + 1] = server_id end
    table.sort(summary.truckServerIds)
    table.sort(summary.railwayServerIds)
    return rows, nil, summary
end

function train_list_runtime.write(request, state, error_text, rows, summary)
    summary = summary or {}
    write_json(train_list_runtime.resultPath, {
        schemaVersion = 1, probeVersion = M.VERSION, requestId = request.requestId,
        profileId = request.profileId, launchSessionId = request.launchSessionId,
        challenge = request.challenge, gamePid = request.gamePid, serverId = request.serverId,
        scanRunId = request.scanRunId, state = state, error = error_text,
        refreshObserved = train_list_runtime.refreshObserved == true,
        refreshArgument = train_list_runtime.refreshArgument,
        truckSourceCount = summary.truckSourceCount or 0,
        railwaySourceCount = summary.railwaySourceCount or 0,
        truckServerIds = summary.truckServerIds or {},
        railwayServerIds = summary.railwayServerIds or {},
        matchServerIds = summary.matchServerIds or {},
        train_march_records = rows or {},
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

function train_list_runtime.pump(now)
    if train_list_runtime.request == nil then
        local request = train_list_runtime.read(now)
        if request == nil then return false end
        train_list_runtime.startedAt = runtime_clock()
        if request.error ~= nil then
            train_list_runtime.write(request, "failed", request.error, nil)
            train_list_runtime.cleanup()
            return true
        end
        local data_center = rawget(_G, "DataCenter")
        local manager = data_center and safe_get(data_center, "LWTrainDataManager") or nil
        local event_type = rawget(_G, "EventManager")
        local event_ids = rawget(_G, "EventId")
        local event_id = event_ids and safe_get(event_ids, "RefreshTrainListData") or nil
        local ok_event_manager, event_manager = call(event_type, "GetInstance")
        if manager == nil or not ok_event_manager or event_manager == nil or event_id == nil then
            train_list_runtime.write(request, "failed", "train_list_refresh_dependencies_unavailable", nil)
            train_list_runtime.cleanup()
            return true
        end
        train_list_runtime.request = request
        train_list_runtime.manager = manager
        train_list_runtime.eventManager = event_manager
        train_list_runtime.eventId = event_id
        train_list_runtime.listener = function(argument)
            if train_list_runtime.request ~= request then return end
            train_list_runtime.refreshObserved = true
            local numeric = tonumber(argument)
            train_list_runtime.refreshArgument = numeric ~= nil and math.floor(numeric) or tostring(argument or "")
        end
        local added = select(1, call(event_manager, "AddListener", event_id, train_list_runtime.listener))
        if not added then
            train_list_runtime.write(request, "failed", "train_list_refresh_listener_failed", nil)
            train_list_runtime.cleanup()
            return true
        end
        local sent = select(1, call(manager, "TryGetTrainList", true))
        if not sent then
            train_list_runtime.write(request, "failed", "train_list_refresh_send_failed", nil)
            train_list_runtime.cleanup()
            return true
        end
        return true
    end
    local request = train_list_runtime.request
    if train_list_runtime.refreshObserved == true then
        local rows, snapshot_error, summary = train_list_runtime.snapshot(request)
        if rows == nil then
            train_list_runtime.write(request, "failed", snapshot_error, nil, summary)
        else
            train_list_runtime.write(request, "proven", nil, rows, summary)
        end
        train_list_runtime.cleanup()
        return true
    end
    if runtime_clock() - (train_list_runtime.startedAt or runtime_clock()) >= 8 then
        train_list_runtime.write(request, "failed", "train_list_refresh_timeout", nil)
        train_list_runtime.cleanup()
        return true
    end
    return true
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    -- Read-only asset rendering is an independent lane. It reuses the same
    -- owned Overview session and must not block map acquisition.
    asset_image_runtime.pump(now)
    -- Read-only Treasure status inspection is independent of acquisition and
    -- reuses the same owned Overview identity. It never claims/dispatches.
    M._treasureStateRuntime.pump(now)
    -- Optional Zombie Boss detail runs beside AOI acquisition. Never block map
    -- progress waiting for a protection reply.
    pump_monster_protection_queue()
    if active_request_id == nil then
        if pump_monster_protection_detail(now) then
            write_heartbeat(now)
            return true
        end
        if pump_resource_detail_diagnostic(now) then
            write_heartbeat(now)
            return true
        end
        if resource_scan_detail_runtime.pump(now) then
            write_heartbeat(now)
            return true
        end
        if train_list_runtime.pump(now) then
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
