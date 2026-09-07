-- Current-build bounded full-world World AOI scan proof.
--
-- This probe mirrors the recovered LWC2MapScanner v108 full-grid policy on the
-- proven normal 100x100 AOI grid. The recovered planner produces 65 batches
-- using an 8x20 native batch shape. Batch 1 is sent and confirmed serially as
-- the capability probe; remaining batches are scheduled with the recovered
-- default concurrency of 8. Correlated response coverage and point records are
-- accumulated across the full session. Current-build WorldPointInfo records are
-- used first because they cover persistent map objects directly. When the full
-- point sweep contains no monsters, the recovered 500-view camera traversal is
-- used with the current game's normal AOI request. Monster/march data is then
-- captured directly from push.world.march.world.get.new at the current-build
-- serverMarchArr[*].marchInfos boundary before WorldMarchDataManager can retire
-- entries from its cache. Current-build WorldMarch.IsMonsterOrBoss type logic
-- decides which spatially correlated march records are monster/boss candidates.
-- No per-view rect-march request is sent. There are no retries.

local M = { VERSION = "lwcontrol-world-full-scan-probe-9" }
M.PERSISTENT = rawget(_G, "LWControlWorldScanPersistentRuntime") == true
M.active_command_id = nil
M.power_enrichment = {}
M.power_target_limit = nil
M.resource_enrichment = {}
M.resource_target_limit = nil

local MAX_CONCURRENCY = 8
local EXPECTED_MONSTER_CAMERA_VIEWS = 500
local PATHS = {}
do
    local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
    PATHS.heartbeat = root .. [[\world-map-full-scan-heartbeat.json]]
    PATHS.status = root .. [[\world-map-full-scan-status.json]]
    PATHS.result = root .. [[\world-map-full-scan-result.json]]
    PATHS.command = root .. [[\world-map-scan-command.txt]]
    PATHS.monster_diagnostics = root .. [[\world-map-full-scan-monster-diagnostics.json]]
    PATHS.direct_diagnostics = root .. [[\world-map-full-scan-direct-diagnostics.json]]
    PATHS.focus_command = root .. [[\world-map-focus-command.txt]]
    PATHS.focus_result = root .. [[\world-map-focus-result.json]]
end
local CFG = {
    STABLE_SECONDS = 2,
    RESPONSE_TIMEOUT_SECONDS = 30,
    MAX_POINTS = 50000,
    MAX_RESPONSE_ENVELOPES = 24,
    MAX_SCAN_RECORDS = 30000,
    MAX_DIAGNOSTIC_EVENTS = 64,
    MAX_MONSTER_CAMERA_VIEWS = EXPECTED_MONSTER_CAMERA_VIEWS,
    MAX_MARCH_PUSHES_PER_VIEW = 16,
    MONSTER_VIEW_RESPONSE_TIMEOUT_SECONDS = 4,
    -- Bounded timeout for the separately proven current-build march AOI push.
    -- This is an operational guard, not a recovered packet-radius/timing fact.
    MONSTER_MARCH_RESPONSE_TIMEOUT_SECONDS = 4,
    MONSTER_CAMERA_RESTORE_TIMEOUT_SECONDS = 1,
    POWER_DETAIL_BATCH_SIZE = 48,
    POWER_DETAIL_WAIT_SECONDS = 0.5,
    POWER_DETAIL_MAX_RETRIES = 1,
}

local phase = M.PERSISTENT and "idle" or "waiting_scene"
local completed = M.PERSISTENT
local transition_requested = false
local transition_count = 0
local stable_point_count = nil
local stable_since = 0
local request_sent_count = 0
local request_sent_at = nil
local target_command = nil
local response_hook = nil
local request = nil
local scan_request = nil
local batch_requests = {}
local active_batch_index = 0
local completed_batch_count = 0
local batch_results = {}
local full_scan_entries = {}
local next_batch_index = 2
local full_scan_active = false
local full_scan_send_events = {}
local full_scan_response_events = {}
local event_ordinal = 0
local full_scan_peak_inflight = 0
local full_scan_wave_count = 0
local scan_records = {}
local scan_record_keys = {}
local scan_duplicate_record_count = 0
local scan_capture_count = 0
local scan_capture_error = nil
local before_capture = nil
local matched_responses = {}
local requested_block_keys = {}
local covered_block_keys = {}
local requested_block_count = 0
local covered_block_count = 0
local target_response_count = 0
local rejected_response_count = 0
local observed_response_envelopes = {}
local last_error = nil
local registration_method = nil
local update_callback = nil
local monster_views = {}
local monster_view_index = 0
local monster_view_started_at = nil
local monster_view_response_started_at = nil
local monster_camera_move_count = 0
local monster_capture_count = 0
local monster_views_with_marches = 0
local monster_views_with_bosses = 0
local monster_max_march_count = 0
local monster_original_view = nil
local monster_original_zoom = nil
local monster_restore_started_at = nil
local monster_camera_restored = false
local monster_current_view = nil
local monster_current_world_position = nil
local monster_official_request_count = 0
local monster_view_response_received = false
local monster_view_response_route = nil
local monster_view_response_envelope = nil
local monster_march_hook = nil
local monster_march_hook_restored = nil
local monster_march_protocol = nil
local monster_march_request_count = 0
local monster_march_response_count = 0
local monster_march_foreign_send_count = 0
local monster_march_response_received = false
local monster_march_response_error_code = nil
local monster_march_response_handler_error = nil
local monster_march_response_started_at = nil
local monster_current_pushes = {}
local march_payload_stats = {
    push_count = 0,
    entry_count = 0,
    spatial_match_count = 0,
    empty_correlated_count = 0,
    uncorrelated_push_count = 0,
}
local monster_view_diagnostics = {}
local monster_current_diagnostic = nil
local restore_state = {
    manager_flag_original = nil,
    manager_flag_touched = false,
    timer_handle = nil,
    transport_hook_restored = nil,
    transport_flag_restored = nil,
    monster_manager_flag_original = nil,
    monster_manager_flag_touched = false,
    monster_manager_flag_restored = nil,
    monster_world_flag_original = nil,
    monster_world_flag_touched = false,
    monster_world_flag_restored = nil,
}
local monster_terminal_state = nil
local monster_terminal_error = nil
local monster_restore_distance = nil
local monster_restore_zoom_delta = nil

local function next_event()
    event_ordinal = event_ordinal + 1
    return event_ordinal
end

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

local function static_call(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, ...)
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
    local ok_field, field = pcall(function()
        return reflected_type:GetField(tostring(key), flags)
    end)
    if ok_field and field ~= nil then
        local ok_value, value = pcall(function() return field:GetValue(component) end)
        if ok_value then return value end
    end
    local ok_property, property = pcall(function()
        return reflected_type:GetProperty(tostring(key), flags)
    end)
    if ok_property and property ~= nil then
        local ok_value, value = pcall(function() return property:GetValue(component, nil) end)
        if ok_value then return value end
    end
    return nil
end

local function reflected_set_value(component, key, value)
    if component == nil then return false, "target_unavailable" end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return false, "target_type_unavailable" end
    local flags = reflection_flags()
    local ok_field, field = pcall(function()
        return reflected_type:GetField(tostring(key), flags)
    end)
    if ok_field and field ~= nil then
        local ok_set, set_error = pcall(function() field:SetValue(component, value) end)
        return ok_set, ok_set and nil or tostring(set_error)
    end
    local ok_property, property = pcall(function()
        return reflected_type:GetProperty(tostring(key), flags)
    end)
    if ok_property and property ~= nil and safe_get(property, "CanWrite") == true then
        local ok_set, set_error = pcall(function() property:SetValue(component, value, nil) end)
        return ok_set, ok_set and nil or tostring(set_error)
    end
    return false, "member_unavailable:" .. tostring(key)
end

local function reflected_method(target, name, parameter_count)
    if target == nil then return nil, "target_unavailable" end
    local ok_type, reflected_type = pcall(function() return target:GetType() end)
    if not ok_type or reflected_type == nil then return nil, "target_type_unavailable" end
    local ok_methods, methods = pcall(function()
        return reflected_type:GetMethods(reflection_flags())
    end)
    if not ok_methods or methods == nil then return nil, "method_list_unavailable" end
    local selected = nil
    each(methods, 2400, function(method)
        if tostring(safe_get(method, "Name") or "") ~= tostring(name) then return true end
        local ok_parameters, values = pcall(function() return method:GetParameters() end)
        local count = ok_parameters and tonumber(
            safe_get(values, "Length") or safe_get(values, "Count")) or nil
        if parameter_count == nil or count == tonumber(parameter_count) then
            selected = method
            return false
        end
        return true
    end)
    return selected, selected ~= nil and nil or "method_unavailable:" .. tostring(name)
end

local function reflected_arguments(values)
    local cs = rawget(_G, "CS")
    local typeof_fn = rawget(_G, "typeof")
    if cs == nil or cs.System == nil or type(typeof_fn) ~= "function" then
        return nil, "system_reflection_unavailable"
    end
    local ok, arguments = pcall(function()
        return cs.System.Array.CreateInstance(typeof_fn(cs.System.Object), #values)
    end)
    if not ok or arguments == nil then return nil, tostring(arguments) end
    for index, value in ipairs(values) do
        local set_ok, set_error = pcall(function() arguments:SetValue(value, index - 1) end)
        if not set_ok then return nil, tostring(set_error) end
    end
    return arguments, nil
end

local function invoke_private(target, name, values)
    values = values or {}
    local method, method_error = reflected_method(target, name, #values)
    if method == nil then return false, nil, method_error end
    local arguments, arguments_error = reflected_arguments(values)
    if arguments == nil then return false, nil, arguments_error end
    local ok, result = pcall(function() return method:Invoke(target, arguments) end)
    return ok, ok and result or nil, ok and nil or tostring(result)
end

local function boxed_int(value)
    local cs = rawget(_G, "CS")
    local convert = cs and cs.System and cs.System.Convert
    local ok, boxed = pcall(function() return convert.ToInt32(tonumber(value) or 0) end)
    return ok and boxed or tonumber(value) or 0
end

local function boxed_bool(value)
    local cs = rawget(_G, "CS")
    local convert = cs and cs.System and cs.System.Convert
    local ok, boxed = pcall(function() return convert.ToBoolean(value == true) end)
    return ok and boxed or value == true
end

local function vector2_int(x, y)
    local cs = rawget(_G, "CS")
    local vector = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int
    if vector == nil then return nil, "vector2int_unavailable" end
    local ok, value = pcall(function() return vector(boxed_int(x), boxed_int(y)) end)
    return ok and value or nil, ok and nil or tostring(value)
end

local function int_array(values)
    local cs = rawget(_G, "CS")
    local typeof_fn = rawget(_G, "typeof")
    if cs == nil or cs.System == nil or type(typeof_fn) ~= "function" then
        return nil, "int_array_runtime_unavailable"
    end
    local ok, array = pcall(function()
        return cs.System.Array.CreateInstance(typeof_fn(cs.System.Int32), #values)
    end)
    if not ok or array == nil then return nil, tostring(array) end
    for index, value in ipairs(values) do
        local set_ok, set_error = pcall(function()
            array:SetValue(boxed_int(value), index - 1)
        end)
        if not set_ok then return nil, tostring(set_error) end
    end
    return array, nil
end

local function block_vector(value)
    if value == nil then return nil end
    local x = tonumber(safe_get(value, "x") or safe_get(value, "X"))
    local y = tonumber(safe_get(value, "y") or safe_get(value, "Y"))
    if x == nil or y == nil then return nil end
    return { x = math.floor(x), y = math.floor(y) }
end

local function world_size(world)
    local value = tonumber(safe_get(world, "WorldSize"))
    if value == nil then
        local ok, observed = call(world, "get_WorldSize")
        value = ok and tonumber(observed) or nil
    end
    return value and math.floor(value) or nil
end

local function world_tile_index(world, x, y)
    local tile, vector_error = vector2_int(x, y)
    if tile == nil then return nil, vector_error end
    local ok, value = call(world, "TilePosToIndex", tile)
    local numeric = ok and tonumber(value) or nil
    if numeric ~= nil then return math.floor(numeric), "WorldScene.TilePosToIndex" end
    local size = world_size(world)
    if size ~= nil and size > 0 then
        return math.floor(y) * size + math.floor(x) + 1, "formula:y*world_size+x+1"
    end
    return nil, "world_tile_index_unavailable"
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

local function runtime_clock()
    local cs = rawget(_G, "CS")
    local time = cs and cs.UnityEngine and cs.UnityEngine.Time
    local value = time and tonumber(safe_get(time, "realtimeSinceStartup")) or nil
    if value ~= nil then return value end
    return tonumber(os.clock()) or 0
end

local function current_view(world)
    local tile = safe_get(world, "CurTilePosClamped") or safe_get(world, "CurTilePos")
    local x = tonumber(safe_get(tile, "x") or safe_get(tile, "X") or safe_get(tile, 1))
    local y = tonumber(safe_get(tile, "y") or safe_get(tile, "Y") or safe_get(tile, 2))
    return { x = x, y = y }
end

local function view_distance(view, target)
    if view == nil or target == nil or view.x == nil or view.y == nil
        or target.x == nil or target.y == nil then return math.huge end
    return math.max(math.abs(view.x - tonumber(target.x)), math.abs(view.y - tonumber(target.y)))
end

local function camera_zoom(world)
    local camera = safe_get(world, "Camera")
    return tonumber(safe_get(camera, "Zoom") or safe_get(camera, "zoom"))
end

local function move_official_monster_view(world, target)
    local ok_position, world_position = call(world, "TileToWorld", target.x, target.y)
    if not ok_position or world_position == nil then return false, "scan_tile_to_world_failed", nil end
    local ok_lookat = select(1, call(world, "Lookat", world_position))
    if ok_lookat then return true, nil, "SceneManager.World.Lookat", world_position end
    local zoom = camera_zoom(world) or -1
    local ok_move = select(1, call(world, "AutoLookat", world_position, zoom, 0.01, nil))
    return ok_move, ok_move and nil or "scan_world_lookat_failed",
        ok_move and "SceneManager.World.AutoLookat" or nil,
        ok_move and world_position or nil
end

local function restore_monster_camera(world, target, zoom)
    local ok_position, world_position = call(world, "TileToWorld", target.x, target.y)
    if not ok_position or world_position == nil then return false, "restore_tile_to_world_failed" end
    local requested_zoom = tonumber(zoom) or camera_zoom(world) or -1
    local ok_move = select(1, call(world, "AutoLookat", world_position, requested_zoom, 0.08, nil))
    return ok_move, ok_move and nil or "restore_auto_lookat_failed"
end

local function world_response_flag(world)
    local value = safe_get(world, "hasReceiveViewPointsReply")
    if value == nil then value = reflected_value(world, "hasReceiveViewPointsReply") end
    if value == nil then return nil end
    return value == true
end

local function set_world_response_flag(world, value)
    if world == nil then return false, "world_unavailable" end
    local direct, direct_error = pcall(function()
        world.hasReceiveViewPointsReply = value == true
    end)
    if direct then return true, nil end
    local reflected, reflected_error = reflected_set_value(
        world, "hasReceiveViewPointsReply", boxed_bool(value == true))
    return reflected, reflected and nil or tostring(reflected_error or direct_error)
end

local function scene_identity()
    local cs = rawget(_G, "CS")
    local manager = cs and safe_get(cs, "SceneManager")
    local ids = rawget(_G, "SceneManagerSceneID") or (cs and safe_get(cs, "SceneManagerSceneID"))
    local current = manager and safe_get(manager, "CurrSceneID")
    local city = ids and safe_get(ids, "City")
    local world = ids and safe_get(ids, "World")
    if current ~= nil and world ~= nil and current == world then return "world", current, city, world end
    if current ~= nil and city ~= nil and current == city then return "city", current, city, world end
    return "unknown", current, city, world
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

local function player_identity()
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    if player == nil then return nil, nil, "LuaEntry.Player unavailable" end
    local server_id = nil
    for _, method in ipairs({ "GetCurServerId", "GetSelfServerId", "GetSourceServerId" }) do
        local ok, value = call(player, method)
        local numeric = ok and tonumber(value) or nil
        if numeric ~= nil and numeric > 0 then server_id = math.floor(numeric); break end
    end
    local ok_world, world_id_value = call(player, "GetCurWorldId")
    local world_id = ok_world and tonumber(world_id_value) or nil
    world_id = world_id and world_id >= 0 and math.floor(world_id) or nil
    if server_id == nil or world_id == nil then return nil, nil, "player server/world identity unavailable" end
    return server_id, world_id, nil
end

local function choose_scan_window(point_manager)
    local block_count = tonumber(reflected_value(point_manager, "_lwAoiBlockCount"))
    local block_size = tonumber(reflected_value(point_manager, "_lwAoiBlockSize"))
    local lb = block_vector(reflected_value(point_manager, "_lastViewLBBlock"))
    local rt = block_vector(reflected_value(point_manager, "_lastViewRTBlock"))
    if block_count == nil or block_size == nil or block_count ~= 100 or block_size ~= 10
        or lb == nil or rt == nil then
        return nil, "full scan requires the proven normal 100x100 grid with block size 10"
    end
    block_count, block_size = math.floor(block_count), math.floor(block_size)
    local blocks = {}
    for y = 0, block_count - 1 do
        for x = 0, block_count - 1 do
            blocks[#blocks + 1] = {
                x = x,
                y = y,
                block_index = y * block_count + x,
            }
        end
    end
    return {
        blocks = blocks,
        requested_blocks = blocks,
        requested_block_count = #blocks,
        coverage = {
            left = 0,
            right = block_count - 1,
            bottom = 0,
            top = block_count - 1,
        },
        requested_coverage = {
            left = 0,
            right = block_count - 1,
            bottom = 0,
            top = block_count - 1,
        },
        side = "full",
        visible_left_bottom = lb,
        visible_right_top = rt,
        block_count = block_count,
        block_size = block_size,
        left_bottom = { x = 0, y = 0 },
        right_top = { x = block_count * block_size, y = block_count * block_size },
    }, nil
end

local function build_monster_camera_views(target)
    local block_count = math.floor(tonumber(target and target.block_count) or 0)
    local block_size = math.floor(tonumber(target and target.block_size) or 0)
    local lb = target and target.visible_left_bottom or nil
    local rt = target and target.visible_right_top or nil
    local view_width = lb and rt and math.floor(tonumber(rt.x) - tonumber(lb.x) + 1) or 0
    local view_height = lb and rt and math.floor(tonumber(rt.y) - tonumber(lb.y) + 1) or 0
    if block_count ~= 100 or block_size ~= 10 or view_width ~= 5 or view_height ~= 4 then
        return nil, "monster camera queue requires proven 100x100/10 geometry and observed 5x4 viewport"
    end
    local rows = {}
    local block_y = 0
    while block_y < block_count do
        local row = {}
        local row_end = math.min(block_count - 1, block_y + view_height - 1)
        local block_x = 0
        while block_x < block_count do
            local column_end = math.min(block_count - 1, block_x + view_width - 1)
            local center_block_x = math.floor((block_x + column_end) / 2)
            local center_block_y = math.floor((block_y + row_end) / 2)
            row[#row + 1] = {
                block_x = center_block_x,
                block_y = center_block_y,
                x = center_block_x * block_size + math.floor(block_size / 2),
                y = center_block_y * block_size + math.floor(block_size / 2),
                coverage = {
                    left = block_x,
                    bottom = block_y,
                    right = column_end,
                    top = row_end,
                },
            }
            block_x = column_end + 1
        end
        if #rows % 2 == 1 then
            local reversed = {}
            for index = #row, 1, -1 do reversed[#reversed + 1] = row[index] end
            row = reversed
        end
        rows[#rows + 1] = row
        block_y = row_end + 1
    end
    local views = {}
    for _, row in ipairs(rows) do
        for _, view in ipairs(row) do
            if #views >= CFG.MAX_MONSTER_CAMERA_VIEWS then
                return nil, "monster camera queue exceeded bounded view cap"
            end
            views[#views + 1] = view
        end
    end
    if #views ~= EXPECTED_MONSTER_CAMERA_VIEWS then
        return nil, "monster camera queue did not produce the recovered 500-view traversal"
    end
    return views, nil
end

local function pad_axis(low, high, minimum, block_count)
    local missing = math.max(0, minimum - (high - low + 1))
    low = low - math.floor(missing / 2)
    high = high + math.ceil(missing / 2)
    if low < 0 then high, low = high - low, 0 end
    if high >= block_count then
        low, high = low - (high - block_count + 1), block_count - 1
    end
    return math.max(0, low), math.min(block_count - 1, high)
end

local function transport_coverage_for_batch(coverage, block_count)
    local left, right = pad_axis(coverage.left, coverage.right, 5, block_count)
    local bottom, top = pad_axis(coverage.bottom, coverage.top, 4, block_count)
    return { left = left, bottom = bottom, right = right, top = top }
end

local function build_native_batch_queue(target)
    local coverage = target.coverage
    local width = coverage.right - coverage.left + 1
    local height = coverage.top - coverage.bottom + 1
    local best_width, best_height, best_count, best_area = 1, 1, math.huge, 0
    for batch_width = 1, math.min(width, 160) do
        local batch_height = math.min(height, math.floor(160 / batch_width))
        if batch_height >= 1 then
            local count = math.ceil(width / batch_width) * math.ceil(height / batch_height)
            local area = batch_width * batch_height
            if count < best_count or (count == best_count and area > best_area) then
                best_width, best_height, best_count, best_area = batch_width, batch_height, count, area
            end
        end
    end
    local batches = {}
    local y = coverage.bottom
    while y <= coverage.top do
        local batch_top = math.min(coverage.top, y + best_height - 1)
        local row = {}
        local x = coverage.left
        while x <= coverage.right do
            local batch_right = math.min(coverage.right, x + best_width - 1)
            local blocks = {}
            for block_y = y, batch_top do
                for block_x = x, batch_right do
                    blocks[#blocks + 1] = {
                        x = block_x, y = block_y,
                        block_index = block_y * target.block_count + block_x,
                    }
                end
            end
            row[#row + 1] = {
                blocks = blocks,
                coverage = { left = x, bottom = y, right = batch_right, top = batch_top },
            }
            x = batch_right + 1
        end
        if #batches % 2 == 1 then
            local reversed = {}
            for index = #row, 1, -1 do reversed[#reversed + 1] = row[index] end
            row = reversed
        end
        for _, batch in ipairs(row) do batches[#batches + 1] = batch end
        y = batch_top + 1
    end
    return batches, best_area
end

local function build_request_for_batch(world, point_manager, target, batch, sequence)
    local size = world_size(world)
    if size ~= 1000 then return nil, "current world size is not proven normal-map size 1000" end
    local coverage = transport_coverage_for_batch(batch.coverage, target.block_count)
    local block_indexes = {}
    for block_y = coverage.bottom, coverage.top do
        for block_x = coverage.left, coverage.right do
            block_indexes[#block_indexes + 1] = block_y * target.block_count + block_x
        end
    end
    if #block_indexes < 1 or #block_indexes > 160 then
        return nil, "native transport index count invalid"
    end
    local left = {
        x = coverage.left * target.block_size,
        y = coverage.bottom * target.block_size,
    }
    local right = {
        x = math.min(size - 1, (coverage.right + 1) * target.block_size),
        y = math.min(size - 1, (coverage.top + 1) * target.block_size),
    }
    local left_index, left_source = world_tile_index(world, left.x, left.y)
    local right_index, right_source = world_tile_index(world, right.x, right.y)
    if left_index == nil or right_index == nil then return nil, "request tile-index geometry unavailable" end
    local server_id, world_id, identity_error = player_identity()
    if server_id == nil or world_id == nil then return nil, identity_error end
    local server_lod = tonumber(reflected_value(point_manager, "svLod"))
    if server_lod == nil then return nil, "WorldPointManager.svLod unavailable" end
    server_lod = math.max(0, math.min(2, math.floor(server_lod)))
    return {
        big_map = 0,
        server_id = server_id,
        world_id = world_id,
        server_lod = server_lod,
        x = math.floor((left.x + right.x) / 2),
        y = math.floor((left.y + right.y) / 2),
        sequence = sequence,
        block_side = target.side,
        requested_blocks = batch.blocks,
        requested_block_count = #batch.blocks,
        requested_coverage = batch.coverage,
        block_indexes = block_indexes,
        transport_index_count = #block_indexes,
        transport_coverage = coverage,
        block_count = target.block_count,
        block_size = target.block_size,
        visible_left_bottom = target.visible_left_bottom,
        visible_right_top = target.visible_right_top,
        left_bottom = left,
        right_top = right,
        left_bottom_index = left_index,
        right_top_index = right_index,
        left_bottom_index_source = left_source,
        right_top_index_source = right_source,
        target_left_bottom = {
            x = batch.coverage.left * target.block_size,
            y = batch.coverage.bottom * target.block_size,
        },
        target_right_top = {
            x = math.min(size - 1, (batch.coverage.right + 1) * target.block_size),
            y = math.min(size - 1, (batch.coverage.top + 1) * target.block_size),
        },
    }, nil
end

local function build_requests(world, point_manager)
    local target, target_error = choose_scan_window(point_manager)
    if target == nil then return nil, nil, target_error end
    local batches, batch_size = build_native_batch_queue(target)
    if #target.blocks ~= 10000 or #batches ~= 65 or batch_size ~= 160 then
        return nil, nil, "recovered full-grid batch plan did not produce expected 65 batches"
    end
    local output = {}
    for index, batch in ipairs(batches) do
        local built, build_error = build_request_for_batch(world, point_manager, target, batch, index)
        if built == nil then return nil, nil, build_error end
        output[#output + 1] = built
    end
    local full_batches, narrow_batches = 0, 0
    for _, value in ipairs(output) do
        if value.requested_block_count == 160 then full_batches = full_batches + 1
        elseif value.requested_block_count == 80 then narrow_batches = narrow_batches + 1
        else return nil, nil, "unexpected full-grid batch logical size" end
    end
    if full_batches ~= 60 or narrow_batches ~= 5 then
        return nil, nil, "full-grid batch size distribution did not match recovered 60x160 + 5x80 plan"
    end
    return target, output, nil
end

local function send_aoi_with_bridge(point_manager, request)
    local cs = rawget(_G, "CS")
    local system = cs and cs.System or nil
    if system == nil then return false, "sender_bridge_runtime_unavailable" end
    local indexes, index_error = int_array(request.block_indexes)
    if indexes == nil then return false, index_error end
    local args, args_error = reflected_arguments({
        point_manager,
        boxed_int(request.big_map),
        boxed_int(request.server_lod),
        boxed_int(request.x),
        boxed_int(request.y),
        indexes,
        boxed_int(request.block_size),
        boxed_int(request.left_bottom_index),
        boxed_int(request.right_top_index),
    })
    if args == nil then return false, args_error end
    local ok_load, sender = pcall(function()
        local root = system.Environment.GetFolderPath(
            system.Environment.SpecialFolder.LocalApplicationData)
        local path = system.IO.Path.Combine(root, "LWControl", "runtime", "WorldBlockSender.dll")
        if system.IO.File.Exists(path) ~= true then error("sender_bridge_assembly_unavailable") end
        local assembly = system.Reflection.Assembly.LoadFrom(path)
        local sender_type = assembly:GetType("LWControl.Diagnostics.WorldBlockSender", true)
        local field = sender_type:GetField("SendAoi", 24)
        if field == nil then error("sender_bridge_delegate_unavailable") end
        return field:GetValue(nil)
    end)
    if not ok_load or sender == nil then
        return false, tostring(sender or "sender_bridge_load_failed")
    end
    local ok_send, send_error = pcall(function()
        if type(sender) == "function" then return sender(args) end
        local invoke = safe_get(sender, "Invoke")
        if type(invoke) == "function" then return invoke(sender, args) end
        return sender(args)
    end)
    return ok_send, ok_send and nil or tostring(send_error)
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
            if type(value) == "string" or type(value) == "boolean" or type(value) == "number" then
                return value
            end
            local numeric = tonumber(value)
            if numeric ~= nil then return numeric end
            local text = tostring(value)
            if text ~= nil and text ~= "" then return text end
        end
    end
    return nil
end

local function object_field(target, names)
    for _, name in ipairs(names) do
        local value = safe_get(target, name)
        if value == nil then value = reflected_value(target, name) end
        if value ~= nil then return value end
    end
    return nil
end

local function populated(value)
    if type(value) ~= "table" then return value end
    for _, member in pairs(value) do
        if member ~= nil then return value end
    end
    return nil
end

-- These payload names and members come from the current generated
-- Protobuf.WorldPointInfo contract. They are kept separate from the historical
-- LWControl aliases below so a recovered-original fallback cannot be mistaken
-- for a current-build protobuf field.
local function current_build_info(info)
    local value = object_field(info, { "buildInfo", "BuildInfo" })
    if value == nil then return nil end
    return populated({
        ownerUid = scalar_field(value, { "ownerUid", "OwnerUid" }),
        uuid = scalar_field(value, { "uuid", "Uuid" }),
        buildId = scalar_field(value, { "buildId", "BuildId" }),
        level = scalar_field(value, { "level", "Level" }),
        buildState = scalar_field(value, { "buildState", "BuildState" }),
        queueState = scalar_field(value, { "queueState", "QueueState" }),
        allianceId = scalar_field(value, { "allianceId", "AllianceId" }),
        updateEndTime = scalar_field(value, { "updateEndTime", "UpdateEndTime" }),
        updateStartTime = scalar_field(value, { "updateStartTime", "UpdateStartTime" }),
        lastHpTime = scalar_field(value, { "lastHpTime", "LastHpTime" }),
        protectEndTime = scalar_field(value, { "protectEndTime", "ProtectEndTime" }),
        inside = scalar_field(value, { "inside", "Inside" }),
        currentHp = scalar_field(value, { "currentHp", "CurrentHp" }),
        name = scalar_field(value, { "name", "Name" }),
        allianceAbbreviation = scalar_field(value, {
            "allianceAbbreviation", "AllianceAbbreviation", "alAbbr", "AlAbbr",
        }),
        lastCollectTime = scalar_field(value, { "lastCollectTime", "LastCollectTime" }),
        unavailableTime = scalar_field(value, { "unavailableTime", "UnavailableTime" }),
        monthCardEndTime = scalar_field(value, { "monthCardEndTime", "MonthCardEndTime" }),
        queueItemId = scalar_field(value, { "queueItemId", "QueueItemId" }),
        queueStartTime = scalar_field(value, { "queueStartTime", "QueueStartTime" }),
        queueUpdateTime = scalar_field(value, { "queueUpdateTime", "QueueUpdateTime" }),
        destroyStartTime = scalar_field(value, { "destroyStartTime", "DestroyStartTime" }),
        appearanceId = scalar_field(value, { "appearanceId", "AppearanceId" }),
        specialType = scalar_field(value, { "specialType", "SpecialType" }),
        positionId = scalar_field(value, { "positionId", "PositionId" }),
    })
end

local function current_road_info(info)
    local value = object_field(info, { "roadInfo", "RoadInfo" })
    if value == nil then return nil end
    return populated({
        ownerUid = scalar_field(value, { "ownerUid", "OwnerUid" }),
        uuid = scalar_field(value, { "uuid", "Uuid" }),
        roadState = scalar_field(value, { "roadState", "RoadState" }),
        inside = scalar_field(value, { "inside", "Inside" }),
        currentHp = scalar_field(value, { "currentHp", "CurrentHp" }),
        allianceId = scalar_field(value, { "allianceId", "AllianceId" }),
    })
end

local function current_collect_resource_info(info)
    local value = object_field(info, { "collectResourceInfo", "CollectResourceInfo" })
    if value == nil then return nil end
    return populated({
        resourceType = scalar_field(value, { "resourceType", "ResourceType" }),
        level = scalar_field(value, { "level", "Level" }),
        type = scalar_field(value, { "type", "Type" }),
        attachId = scalar_field(value, { "attachId", "AttachId" }),
    })
end

local function current_resource_info(info)
    local value = object_field(info, { "resourceInfo", "ResourceInfo" })
    if value == nil then return nil end
    return populated({
        resourceId = scalar_field(value, { "resourceId", "ResourceId" }),
        state = scalar_field(value, { "state", "State" }),
        gatherUuid = scalar_field(value, { "gatherUuid", "GatherUuid" }),
    })
end

local function current_event_info(info, names)
    local value = object_field(info, names)
    if value == nil then return nil end
    return populated({
        ownerUid = scalar_field(value, { "ownerUid", "OwnerUid" }),
        uuid = scalar_field(value, { "uuid", "Uuid" }),
        eventId = scalar_field(value, { "eventId", "EventId" }),
    })
end

local function current_garbage_info(info)
    local value = object_field(info, { "garbagePointInfo", "GarbagePointInfo" })
    if value == nil then return nil end
    return populated({
        ownerUid = scalar_field(value, { "ownerUid", "OwnerUid" }),
        uuid = scalar_field(value, { "uuid", "Uuid" }),
        eventId = scalar_field(value, { "eventId", "EventId" }),
        endTime = scalar_field(value, { "endTime", "EndTime" }),
    })
end

-- Source-level recovery from the supplied LWControl scanner v108. Unknown
-- current-build point codes deliberately remain world_point.
local function classify_point(point_type, player_name, owner_uid, alliance_id, alliance_abbr)
    local code = tonumber(point_type)
    if code == 6 then return "player_base" end
    if code == 11 or code == 15 or code == 25 or code == 35 then return "alliance_building" end
    if code == 4 or code == 5 or code == 22 or code == 1003 then return "monster" end
    if code == 1 or code == 7 or code == 26 then return "resource_point" end
    if code == nil and (player_name ~= nil or owner_uid ~= nil) then return "player_base" end
    if code == nil and (alliance_id ~= nil or alliance_abbr ~= nil) then return "alliance_building" end
    return "world_point"
end

local function optional_resource_values(info)
    local output = {}
    local ok_level, level = call(info, "GetResLevel")
    if ok_level and tonumber(level) ~= nil then output.level = math.floor(tonumber(level)) end
    local ok_type, resource_type = call(info, "GetResType")
    if ok_type and resource_type ~= nil then
        output.resource_type_id = tonumber(resource_type) or tostring(resource_type)
    end
    local ok_point_type, resource_point_type = call(info, "GetResPointType")
    if ok_point_type and resource_point_type ~= nil then
        output.resource_point_type = tonumber(resource_point_type) or tostring(resource_point_type)
    end
    output.getters_observed = ok_level or ok_type or ok_point_type
    return output
end

local function resource_type_name(value)
    if value == nil then return nil end
    local resource_types = rawget(_G, "ResourceType")
    local requested_number = tonumber(value)
    local requested_text = tostring(value)
    local names = {
        Gold = "gold", Food = "food", Wood = "wood",
        Metal = "iron", Iron = "iron", Stone = "iron",
        Oil = "oil", Petroleum = "oil",
        SeasonResource = "season", Season = "season", Special = "season",
    }
    local resolved = nil
    for enum_name, output_name in pairs(names) do
        local enum_value = safe_get(resource_types, enum_name)
        if enum_value ~= nil and ((requested_number ~= nil and tonumber(enum_value) == requested_number)
            or tostring(enum_value) == requested_text) then
            if resolved ~= nil and resolved ~= output_name then return "unknown" end
            resolved = output_name
        end
    end
    return resolved or "unknown"
end

-- Recovered verbatim in behavior from original LWC2MapScanner.lua v108: boss
-- rows obtain level/recommended power from MonsterTemplateManager rather than
-- assuming those values live on the WorldMarch object.
local function monster_template_values(monster_id, observed_level)
    if monster_id == nil then return {} end
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "MonsterTemplateManager")
    if manager == nil then return {} end
    local numeric_id = tonumber(monster_id)
    local candidates = { numeric_id or monster_id }
    if numeric_id ~= nil and numeric_id >= 1000000 then
        local numeric_level = tonumber(observed_level) or (numeric_id % 100)
        local season_family = math.floor(numeric_id / 10000) * 1000
        local canonical_id = season_family + 100 + math.floor(numeric_level)
        if canonical_id ~= numeric_id then candidates[#candidates + 1] = canonical_id end
    end
    for index, candidate in ipairs(candidates) do
        local ok_template, template = call(manager, "GetMonsterTemplate", candidate)
        if ok_template and template ~= nil then
            local output = {
                level = scalar_field(template, { "level", "Level" }),
                recommended_power = scalar_field(template, {
                    "recommendPower", "RecommendPower", "recommendedPower", "RecommendedPower",
                    "recommend_power", "power", "Power",
                }),
                template_id = candidate,
                source = index == 1 and "MonsterTemplateManager.GetMonsterTemplate"
                    or "MonsterTemplateManager.GetMonsterTemplate.season_canonical",
            }
            if output.recommended_power ~= nil then return output end
            if output.level ~= nil then return output end
        end
    end
    return {}
end

local function recovered_point_record(point_manager, info, id, server_id, world_id, tile)
    local point_type = integer_field(info, { "pointType", "PointType" })
    local build_info = current_build_info(info)
    local road_info = current_road_info(info)
    local collect_resource_info = current_collect_resource_info(info)
    local resource_info = current_resource_info(info)
    local explore_point_info = current_event_info(info, { "explorePointInfo", "ExplorePointInfo" })
    local sample_point_info = current_event_info(info, { "samplePointInfo", "SamplePointInfo" })
    local garbage_point_info = current_garbage_info(info)
    local player_name = build_info and build_info.name
        or scalar_field(info, { "playerName", "PlayerName", "name", "Name" })
    local owner_uid = build_info and build_info.ownerUid
        or scalar_field(info, { "ownerUid", "OwnerUid", "uid", "Uid" })
    local alliance_id = build_info and build_info.allianceId
        or scalar_field(info, { "allianceId", "AllianceId" })
    local alliance_abbr = build_info and build_info.allianceAbbreviation
        or scalar_field(info, { "alAbbr", "AlAbbr", "abbr", "Abbr" })
    local kind = classify_point(point_type, player_name, owner_uid, alliance_id, alliance_abbr)
    local source = info
    local resource_values = {}
    if kind == "resource_point" then
        local ok_resource, resource_info = call(point_manager, "GetResourcePointInfoByIndex", id)
        if ok_resource and resource_info ~= nil then source = resource_info end
        resource_values = optional_resource_values(source)
    end
    local protect_end = tonumber(build_info and build_info.protectEndTime or scalar_field(source, {
        "protectEndTime", "ProtectEndTime", "shieldEndTime", "ShieldEndTime",
    }))
    if protect_end ~= nil and protect_end > 100000000000 then protect_end = math.floor(protect_end / 1000) end
    local now = tonumber(os.time()) or 0
    local shield_active = protect_end ~= nil and protect_end > now
    local gather_end = tonumber(scalar_field(source, {
        "gatherEndTime", "GatherEndTime", "collectEndTime", "CollectEndTime",
        "endTime", "EndTime", "remainEndTime", "RemainEndTime",
    }))
    if gather_end ~= nil and gather_end > 100000000000 then gather_end = math.floor(gather_end / 1000) end
    local resource_owner = resource_info and resource_info.gatherUuid or scalar_field(source, {
        "collectorUid", "CollectorUid", "gatherUid", "GatherUid", "occupyUid", "OccupyUid",
    })
    local exact_resource_type = collect_resource_info and collect_resource_info.resourceType or nil
    local exact_resource_level = collect_resource_info and collect_resource_info.level or nil
    local resource_type_id = resource_values.resource_type_id or exact_resource_type
        or scalar_field(source, { "resourceType", "ResourceType", "resType", "ResType" })
    local gather_status = nil
    if kind == "resource_point" then
        if gather_end ~= nil then
            gather_status = "gathering"
        else
            gather_status = resource_owner == nil and "not_gathering" or "occupied_time_unavailable"
        end
    end
    return {
        id = id,
        pointId = id,
        pointType = point_type,
        kind = kind,
        uuid = integer_field(source, { "uuid", "Uuid" }) or integer_field(info, { "uuid", "Uuid" }) or 0,
        serverId = server_id,
        srcServerId = integer_field(source, { "srcServerId", "SrcServerId" }) or integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
        worldId = world_id,
        x = tile.x,
        y = tile.y,
        name = player_name or alliance_abbr or owner_uid,
        playerName = player_name,
        playerId = owner_uid,
        alliance = kind == "player_base" and (alliance_abbr or alliance_id or "") or alliance_abbr,
        allianceId = alliance_id,
        level = exact_resource_level or resource_values.level
            or (build_info and build_info.level)
            or scalar_field(source, { "level", "Level" }) or scalar_field(info, { "level", "Level" }),
        power = scalar_field(source, { "power", "Power", "fightPower", "FightPower", "combatPower", "CombatPower", "totalPower", "TotalPower", "battlePower", "BattlePower" }),
        monsterId = scalar_field(source, { "monsterId", "MonsterId", "monsterID", "templateId", "TemplateId" }),
        resourceTypeId = resource_type_id,
        resourceType = kind == "resource_point" and resource_type_name(resource_type_id) or nil,
        resourcePointType = resource_values.resource_point_type,
        resourceGettersObserved = resource_values.getters_observed == true,
        resourceRemaining = scalar_field(source, { "remain", "Remain", "remaining", "Remaining", "resourceRemain", "ResourceRemain" }),
        resourceCapacity = scalar_field(source, { "max", "Max", "capacity", "Capacity", "resourceMax", "ResourceMax" }),
        gatherSeconds = kind == "resource_point" and gather_end ~= nil and math.max(0, gather_end - now) or nil,
        gatherTimeStatus = gather_status,
        shield = {
            known = protect_end ~= nil,
            active = shield_active,
            expiresAt = protect_end,
            remainingSeconds = shield_active and (protect_end - now) or 0,
            source = build_info ~= nil and "WorldPointInfo.BuildInfo.protectEndTime"
                or "recovered_original_point_alias",
        },
        buildInfo = build_info,
        roadInfo = road_info,
        collectResourceInfo = collect_resource_info,
        resourceInfo = resource_info,
        explorePointInfo = explore_point_info,
        samplePointInfo = sample_point_info,
        garbagePointInfo = garbage_point_info,
        source = "WorldPointManager._pointInfos",
    }
end

-- PROVEN current-build producer chain:
-- UIWorldPointCtrl.RequestWorldPointDetail -> MsgDefines.WorldGetDetail ->
-- WorldGetDetailMessage -> WorldPointDetailManager.UpdateDetail ->
-- WorldPointDetailManager.GetDetailByPointId. WorldPointDetailData exposes
-- the parsed player `power` field. Zero/missing values remain unresolved.
function M.CachedWorldDetailPower(point_id)
    local numeric_point = tonumber(point_id)
    if numeric_point == nil then return nil, nil end
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "WorldPointDetailManager")
    if manager == nil then return nil, nil end
    local ok, detail = call(manager, "GetDetailByPointId", math.floor(numeric_point))
    if not ok or detail == nil then return nil, nil end
    local power = tonumber(safe_get(detail, "power") or safe_get(detail, "Power"))
    if power == nil or power <= 0 then return nil, nil end
    return power, "DataCenter.WorldPointDetailManager.GetDetailByPointId"
end

-- PROVEN current-build ParseData normalization:
-- message.remainRes falls back to message.reserve -> detail.remainRes;
-- message.initRes falls back to message.initReserve -> detail.initRes.
-- A zero remaining amount is valid when a positive initial capacity proves that
-- the detail object represents a parsed resource point.
function M.CachedWorldDetailResource(point_id)
    local numeric_point = tonumber(point_id)
    if numeric_point == nil then return nil, nil, nil end
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "WorldPointDetailManager")
    if manager == nil then return nil, nil, nil end
    local ok, detail = call(manager, "GetDetailByPointId", math.floor(numeric_point))
    if not ok or detail == nil then return nil, nil, nil end
    local remaining = tonumber(safe_get(detail, "remainRes") or safe_get(detail, "RemainRes"))
    local capacity = tonumber(safe_get(detail, "initRes") or safe_get(detail, "InitRes"))
    if remaining == nil or remaining < 0 or capacity == nil or capacity <= 0 then
        return nil, nil, nil
    end
    return remaining, capacity, "DataCenter.WorldPointDetailManager.GetDetailByPointId"
end

-- RECOVERED original request shape, constrained by the current-build
-- RequestWorldPointDetail(self) implementation. The controller derives the
-- active world/server and sends MsgDefines.WorldGetDetail using pointId,
-- PointType from SceneManager.World:GetPointInfo(pointId), and ownerUid.
function M.RequestWorldPointDetail(record)
    if record == nil then return false, "player_record_unavailable" end
    local numeric_point = tonumber(record.pointId or record.id)
    if numeric_point == nil then return false, "player_point_id_unavailable" end
    local ok_module, controller = pcall(require, "UI.UIWorldPoint.Controller.UIWorldPointCtrl")
    local request_detail = ok_module and safe_get(controller, "RequestWorldPointDetail") or nil
    if type(request_detail) ~= "function" then
        return false, "world_detail_request_unavailable"
    end
    local requester = {
        pointId = math.floor(numeric_point),
        serverId = tonumber(record.serverId),
        ownerUid = record.playerId,
        uuid = record.uuid,
    }
    if type(controller) == "table" then setmetatable(requester, { __index = controller }) end
    local ok, result = pcall(request_detail, requester)
    if not ok then return false, tostring(result) end
    return result ~= false, result == false and "world_detail_request_rejected" or nil
end

function M.BeginPowerEnrichment()
    local state = {
        started = true,
        complete = false,
        targets = {},
        initial_target_count = 0,
        index = 1,
        retry = 0,
        requests = 0,
        request_failures = 0,
        resolved = 0,
        cached_resolved = 0,
        unresolved = 0,
        skipped_target_count = 0,
        wait_at = nil,
    }
    M.power_enrichment = state
    for _, record in ipairs(scan_records) do
        if record.kind == "player_base" and (tonumber(record.power) or 0) <= 0
            and tonumber(record.pointId or record.id) ~= nil then
            local power, source = M.CachedWorldDetailPower(record.pointId or record.id)
            if power ~= nil then
                record.power = power
                record.powerSource = source
                state.resolved = state.resolved + 1
                state.cached_resolved = state.cached_resolved + 1
            elseif M.power_target_limit == nil or #state.targets < M.power_target_limit then
                state.targets[#state.targets + 1] = record
            else
                state.skipped_target_count = state.skipped_target_count + 1
            end
        end
    end
    state.initial_target_count = #state.targets
    state.unresolved = #state.targets
    if #state.targets == 0 then
        state.complete = true
        return false
    end
    phase = "player_power_request"
    return true
end

function M.AdvancePowerEnrichment()
    local state = M.power_enrichment or {}
    if phase == "player_power_request" then
        local stop = math.min(#(state.targets or {}),
            (tonumber(state.index) or 1) + CFG.POWER_DETAIL_BATCH_SIZE - 1)
        for index = tonumber(state.index) or 1, stop do
            local requested = M.RequestWorldPointDetail(state.targets[index])
            state.requests = (tonumber(state.requests) or 0) + 1
            if not requested then
                state.request_failures = (tonumber(state.request_failures) or 0) + 1
            end
        end
        state.index = stop + 1
        if state.index <= #(state.targets or {}) then return false end
        state.wait_at = runtime_clock()
        phase = "player_power_wait"
        return false
    end
    if phase ~= "player_power_wait" then return true end
    if runtime_clock() - (tonumber(state.wait_at) or runtime_clock()) < CFG.POWER_DETAIL_WAIT_SECONDS then
        return false
    end
    local unresolved = {}
    local resolved = 0
    for _, record in ipairs(state.targets or {}) do
        local power, source = M.CachedWorldDetailPower(record.pointId or record.id)
        if power ~= nil then
            record.power = power
            record.powerSource = source
            resolved = resolved + 1
        else
            unresolved[#unresolved + 1] = record
        end
    end
    state.resolved = (tonumber(state.resolved) or 0) + resolved
    state.unresolved = #unresolved
    if #unresolved > 0 and (tonumber(state.retry) or 0) < CFG.POWER_DETAIL_MAX_RETRIES then
        state.targets = unresolved
        state.index = 1
        state.retry = (tonumber(state.retry) or 0) + 1
        state.wait_at = nil
        phase = "player_power_request"
        return false
    end
    state.targets = unresolved
    state.complete = true
    return true
end

function M.BeginResourceEnrichment()
    local state = {
        started = true,
        complete = false,
        targets = {},
        initial_target_count = 0,
        index = 1,
        retry = 0,
        requests = 0,
        request_failures = 0,
        resolved = 0,
        cached_resolved = 0,
        unresolved = 0,
        skipped_target_count = 0,
        wait_at = nil,
    }
    M.resource_enrichment = state
    for _, record in ipairs(scan_records) do
        if record.kind == "resource_point"
            and (record.resourceRemaining == nil or record.resourceCapacity == nil)
            and tonumber(record.pointId or record.id) ~= nil then
            local remaining, capacity, source = M.CachedWorldDetailResource(record.pointId or record.id)
            if remaining ~= nil and capacity ~= nil then
                record.resourceRemaining = remaining
                record.resourceCapacity = capacity
                record.resourceAmountSource = source
                state.resolved = state.resolved + 1
                state.cached_resolved = state.cached_resolved + 1
            elseif M.resource_target_limit == nil or #state.targets < M.resource_target_limit then
                state.targets[#state.targets + 1] = record
            else
                state.skipped_target_count = state.skipped_target_count + 1
            end
        end
    end
    state.initial_target_count = #state.targets
    state.unresolved = #state.targets
    if #state.targets == 0 then
        state.complete = true
        return false
    end
    phase = "resource_detail_request"
    return true
end

function M.AdvanceResourceEnrichment()
    local state = M.resource_enrichment or {}
    if phase == "resource_detail_request" then
        local stop = math.min(#(state.targets or {}),
            (tonumber(state.index) or 1) + CFG.POWER_DETAIL_BATCH_SIZE - 1)
        for index = tonumber(state.index) or 1, stop do
            local requested = M.RequestWorldPointDetail(state.targets[index])
            state.requests = (tonumber(state.requests) or 0) + 1
            if not requested then
                state.request_failures = (tonumber(state.request_failures) or 0) + 1
            end
        end
        state.index = stop + 1
        if state.index <= #(state.targets or {}) then return false end
        state.wait_at = runtime_clock()
        phase = "resource_detail_wait"
        return false
    end
    if phase ~= "resource_detail_wait" then return true end
    if runtime_clock() - (tonumber(state.wait_at) or runtime_clock()) < CFG.POWER_DETAIL_WAIT_SECONDS then
        return false
    end
    local unresolved = {}
    local resolved = 0
    for _, record in ipairs(state.targets or {}) do
        local remaining, capacity, source = M.CachedWorldDetailResource(record.pointId or record.id)
        if remaining ~= nil and capacity ~= nil then
            record.resourceRemaining = remaining
            record.resourceCapacity = capacity
            record.resourceAmountSource = source
            resolved = resolved + 1
        else
            unresolved[#unresolved + 1] = record
        end
    end
    state.resolved = (tonumber(state.resolved) or 0) + resolved
    state.unresolved = #unresolved
    if #unresolved > 0 and (tonumber(state.retry) or 0) < CFG.POWER_DETAIL_MAX_RETRIES then
        state.targets = unresolved
        state.index = 1
        state.retry = (tonumber(state.retry) or 0) + 1
        state.wait_at = nil
        phase = "resource_detail_request"
        return false
    end
    state.targets = unresolved
    state.complete = true
    return true
end


local function capture_world_monsters(world, bounds)
    local records = {}
    local diagnostics = {
        ok = false,
        observed = 0,
        resolved = 0,
        selected = 0,
        monsters = 0,
        bosses = 0,
        ordinary_bosses = 0,
    }
    local manager = safe_get(world, "MarchDataManager")
    if manager == nil then
        local ok_manager, observed_manager = call(world, "get_MarchDataManager")
        if ok_manager then manager = observed_manager end
    end
    if manager == nil then
        diagnostics.error = "march_manager_unavailable"
        return records, diagnostics
    end
    local uuids = safe_get(manager, "allMarchUuids")
        or safe_get(manager, "AllMarchUuids")
        or reflected_value(manager, "allMarchUuids")
    if uuids == nil then
        diagnostics.error = "all_march_uuids_unavailable"
        return records, diagnostics
    end
    diagnostics.ok = true
    each(uuids, CFG.MAX_POINTS, function(raw_uuid)
        diagnostics.observed = diagnostics.observed + 1
        local ok_march, info = call(manager, "GetMarch", raw_uuid)
        if not ok_march or info == nil then return true end
        diagnostics.resolved = diagnostics.resolved + 1
        local ok_monster_or_boss, is_monster_or_boss = call(info, "IsMonsterOrBoss")
        if not ok_monster_or_boss or is_monster_or_boss ~= true then return true end
        local ok_monster, is_monster = call(info, "IsMonster")
        local ok_boss, is_boss = call(info, "IsBoss")
        local ok_ordinary, is_ordinary = call(info, "IsOrdinaryBoss")
        if ok_monster and is_monster == true then diagnostics.monsters = diagnostics.monsters + 1 end
        if ok_boss and is_boss == true then diagnostics.bosses = diagnostics.bosses + 1 end
        if ok_ordinary and is_ordinary == true then
            diagnostics.ordinary_bosses = diagnostics.ordinary_bosses + 1
        end
        local point_id = integer_field(info, {
            "startPos", "StartPos", "endPos", "EndPos", "pointIndex", "PointIndex",
            "targetPoint", "TargetPoint",
        })
        local uuid = scalar_field(info, {
            "uuid", "Uuid", "UUID", "_uuid", "targetUuid", "TargetUuid", "marchUuid", "MarchUuid",
        })
        if point_id ~= nil and point_id >= 0 and uuid ~= nil and tostring(uuid) ~= "" and tostring(uuid) ~= "0" then
            local tile = index_to_tile(world, point_id)
            if tile ~= nil and tile.x >= bounds.left_bottom.x and tile.x < bounds.right_top.x
                and tile.y >= bounds.left_bottom.y and tile.y < bounds.right_top.y then
                local observed_level = scalar_field(info, { "level", "Level", "monsterLevel", "MonsterLevel" })
                local monster_id = scalar_field(info, { "monsterId", "MonsterId", "monsterID" })
                local template = monster_template_values(monster_id, observed_level)
                records[#records + 1] = {
                    id = point_id,
                    pointId = point_id,
                    kind = "monster",
                    uuid = uuid,
                    serverId = integer_field(info, { "serverId", "ServerId", "targetServerId", "TargetServerId" }) or 0,
                    srcServerId = integer_field(info, { "srcServerId", "SrcServerId" }) or 0,
                    worldId = integer_field(info, { "worldId", "WorldId" }) or 0,
                    x = tile.x,
                    y = tile.y,
                    level = template.level or observed_level,
                    monsterId = monster_id,
                    marchType = scalar_field(info, { "type", "Type" }),
                    monsterType = scalar_field(info, { "monsterType", "MonsterType" }),
                    monsterSpecialType = scalar_field(info, {
                        "monsterSpecialType", "MonsterSpecialType", "specialType", "SpecialType", "special",
                    }),
                    isMonster = ok_monster and is_monster == true,
                    isBoss = ok_boss and is_boss == true,
                    isOrdinaryBoss = ok_ordinary and is_ordinary == true,
                    invasionBossInfoPresent = object_field(info, {
                        "invasionBossInfo", "InvasionBossInfo",
                    }) ~= nil,
                    zMBossInfoPresent = object_field(info, { "zMBossInfo", "ZMBossInfo" }) ~= nil,
                    detectZombieBusTrainPresent = object_field(info, {
                        "detectZombieBusTrain", "DetectZombieBusTrain",
                    }) ~= nil,
                    darknessMonsterDataPresent = object_field(info, {
                        "darknessMonsterData", "DarknessMonsterData",
                    }) ~= nil,
                    recommendedPower = template.recommended_power,
                    recommendedPowerSource = template.source,
                    recommendedPowerTemplateId = template.template_id,
                    source = "World.MarchDataManager.GetMarch",
                }
                diagnostics.selected = diagnostics.selected + 1
            end
        end
        return true
    end)
    return records, diagnostics
end

local function capture_points(world, point_manager, bounds)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if expected == nil or expected < 0 or expected > CFG.MAX_POINTS then
        return nil, "loaded point count is outside bounded limit"
    end
    local values = safe_get(collection, "Values")
    local enumerable = values ~= nil and values or collection
    local identities, in_bounds = {}, {}
    local scanned = each(enumerable, CFG.MAX_POINTS + 1, function(raw)
        local info = safe_get(raw, "Value") or raw
        local id = integer_field(info, {
            "pointIndex", "PointIndex", "mainIndex", "MainIndex", "pointId", "PointId",
        })
        local server_id = integer_field(info, { "serverId", "ServerId" }) or 0
        local world_id = integer_field(info, { "worldId", "WorldId" }) or 0
        if id == nil or id < 0 then return true end
        local key = tostring(world_id) .. ":" .. tostring(server_id) .. ":" .. tostring(id)
        identities[key] = { id = id, serverId = server_id, worldId = world_id }
        local tile = bounds and index_to_tile(world, id) or nil
        if tile ~= nil and tile.x >= bounds.left_bottom.x and tile.x < bounds.right_top.x
            and tile.y >= bounds.left_bottom.y and tile.y < bounds.right_top.y then
            in_bounds[#in_bounds + 1] = recovered_point_record(
                point_manager, info, id, server_id, world_id, tile)
        end
        return true
    end)
    if scanned ~= expected then return nil, "loaded point enumeration did not match _pointInfos.Count" end
    local monsters = capture_world_monsters(world, bounds)
    for _, monster in ipairs(monsters) do
        in_bounds[#in_bounds + 1] = monster
    end
    return { count = expected, identities = identities, in_bounds = in_bounds }, nil
end

local function append_scan_records(records)
    for _, point in ipairs(records or {}) do
        local monster_uuid = tostring(point.uuid or "")
        local use_monster_uuid = point.kind == "monster"
            and point.source ~= "WorldPointManager._pointInfos"
            and monster_uuid ~= "" and monster_uuid ~= "0"
            and monster_uuid ~= "nil" and monster_uuid ~= "null"
        local key = use_monster_uuid and ("monster:" .. monster_uuid) or table.concat({
                tostring(point.worldId or 0),
                tostring(point.serverId or 0),
                tostring(point.id or -1),
            }, ":")
        if scan_record_keys[key] ~= true then
            if #scan_records >= CFG.MAX_SCAN_RECORDS then
                scan_capture_error = "accumulated point records exceeded bounded limit"
                return false
            end
            scan_record_keys[key] = true
            scan_records[#scan_records + 1] = point
        else
            scan_duplicate_record_count = scan_duplicate_record_count + 1
        end
    end
    return true
end

local function accumulate_scan_records(world, point_manager)
    local capture, capture_error = capture_points(world, point_manager, scan_request)
    scan_capture_count = scan_capture_count + 1
    if capture == nil then
        scan_capture_error = capture_error or "post-response point capture failed"
        return false
    end
    return append_scan_records(capture.in_bounds)
end

local function invoke_dispatch_delegate(delegate, ...)
    if type(delegate) == "function" then return delegate(...) end
    local invoke = safe_get(delegate, "Invoke")
    if type(invoke) == "function" then return invoke(delegate, ...) end
    return delegate(...)
end

local function same_delegate(left, right)
    if left == right then return true end
    local cs = rawget(_G, "CS")
    local system_object = cs and cs.System and cs.System.Object
    local ok, same = static_call(system_object, "ReferenceEquals", left, right)
    return ok and same == true
end

local function event_parameter(event, key)
    local parameters = safe_get(event, "Params")
    if parameters == nil then
        local ok, observed = call(event, "get_Params")
        if ok then parameters = observed end
    end
    if parameters == nil then return nil end
    local direct = safe_get(parameters, key)
    if direct ~= nil and type(direct) ~= "function" then return direct end
    for _, getter in ipairs({ "get_Item", "Get", "GetValue" }) do
        local ok, value = call(parameters, getter, key)
        if ok and value ~= nil then return value end
    end
    return nil
end

local function response_sfs_has_key(target, key)
    if target == nil then return false end
    local ok_contains, contains = call(target, "ContainsKey", key)
    if ok_contains then
        return contains == true or string.lower(tostring(contains or "")) == "true"
    end
    local direct = safe_get(target, key)
    if direct ~= nil and type(direct) ~= "function" then return true end
    local ok_keys, keys = call(target, "GetKeys")
    if not ok_keys or keys == nil then return nil end
    local found = false
    each(keys, 256, function(candidate)
        if tostring(candidate or "") == tostring(key) then
            found = true
            return false
        end
        return true
    end)
    return found
end

local function response_number(target, key)
    if target == nil then return nil end
    local present = response_sfs_has_key(target, key)
    if present == false then return nil end
    for _, getter in ipairs({ "GetInt", "GetLong", "GetShort", "GetByte" }) do
        local ok, value = call(target, getter, key)
        local numeric = ok and tonumber(value) or nil
        if numeric ~= nil then return math.floor(numeric) end
    end
    local numeric = tonumber(safe_get(target, key))
    return numeric and math.floor(numeric) or nil
end

local function response_array_count(value)
    if value == nil then return 0 end
    for _, property in ipairs({ "Count", "Length", "Size" }) do
        local numeric = tonumber(safe_get(value, property))
        if numeric ~= nil then return math.max(0, math.floor(numeric)) end
    end
    for _, method in ipairs({ "get_Count", "get_Length", "Size", "size" }) do
        local ok, observed = call(value, method)
        local numeric = ok and tonumber(observed) or nil
        if numeric ~= nil then return math.max(0, math.floor(numeric)) end
    end
    return 0
end

local march_payload = {}

function march_payload.response_text(target, key)
    if target == nil then return nil end
    local present = response_sfs_has_key(target, key)
    if present == false then return nil end
    for _, getter in ipairs({ "GetUtfString", "GetText", "GetString" }) do
        local ok, value = call(target, getter, key)
        if ok and value ~= nil then
            local text = tostring(value)
            if text ~= "" then return text end
        end
    end
    local direct = safe_get(target, key)
    if direct ~= nil and type(direct) ~= "function" then
        local text = tostring(direct)
        if text ~= "" then return text end
    end
    return nil
end

function march_payload.response_scalar(target, key)
    local numeric = response_number(target, key)
    if numeric ~= nil then return numeric end
    return march_payload.response_text(target, key)
end

-- PROVEN current build: WorldMarchDataManager.HandleWorldMarchGet reads
-- serverMarchArr[*].serverId, uuidSet and marchInfos. ParseDataWorldMarchGet
-- iterates marchInfos, reads uuid, and passes each ISFSObject to
-- WorldMarch.UpdateWorldMarch -> UpdateFromSFS. These field names therefore come
-- from the current client rather than the recovered bot.
march_payload.CURRENT_BOSS_MARCH_TYPES = {
    [3] = true, [9] = true, [10] = true, [12] = true, [15] = true,
    [20] = true, [21] = true, [22] = true, [25] = true, [26] = true,
    [28] = true, [29] = true, [39] = true, [45] = true, [46] = true,
}

function march_payload.current_march_classification(march_type)
    local value = tonumber(march_type)
    if value == nil then return false, nil, nil, nil end
    value = math.floor(value)
    if value == 2 then
        return true, true, false, "WorldMarch.IsMonster:type=2"
    end
    -- Current IsMonsterOrBoss explicitly accepts type 33. IsMonster/IsBoss then
    -- split type 33 by monsterType, which is not a raw UpdateFromSFS SFS key.
    if value == 33 then
        return true, nil, nil, "WorldMarch.IsMonsterOrBoss:type=33"
    end
    if march_payload.CURRENT_BOSS_MARCH_TYPES[value] then
        return true, false, true, "WorldMarch.IsBoss:type=" .. tostring(value)
    end
    return false, false, false, nil
end

function march_payload.raw_march_entry(info, server_id, source)
    local uuid = march_payload.response_scalar(info, "uuid")
    local entry = {
        source = source,
        server_id = server_id,
        uuid = uuid,
        start_pos = response_number(info, "startPos"),
        target_pos = response_number(info, "targetPos"),
        main_point_id = response_number(info, "mainPointId"),
        march_type = response_number(info, "type"),
        monster_id = response_number(info, "monsterId"),
        world_id = response_number(info, "worldId"),
        server = response_number(info, "server"),
        target_server = response_number(info, "targetServer"),
        src_server = response_number(info, "srcServer"),
        target_uuid = march_payload.response_scalar(info, "targetUuid"),
        event_id = march_payload.response_scalar(info, "eventId"),
        event_uuid = march_payload.response_scalar(info, "eventUuid"),
        boss_id = response_number(info, "bossId"),
        cur_hp = response_number(info, "curHp"),
        max_hp = response_number(info, "maxHp"),
        running_hp = response_number(info, "running_hp"),
        invasion_boss_info_present = response_sfs_has_key(info, "invasionBossInfo") == true,
        zm_boss_info_present = response_sfs_has_key(info, "zMBossInfo") == true,
        alliance_boss_info_present = response_sfs_has_key(info, "allianceBossInfo") == true,
        stronghold_boss_present = response_sfs_has_key(info, "strongholdBoss") == true,
        alliance_challenge_info_present = response_sfs_has_key(info, "allianceChallengeInfo") == true,
        city_battle_s1_monster_info_present = response_sfs_has_key(info, "cityBattleS1MonsterInfo") == true,
        bloody_queen_monster_present = response_sfs_has_key(info, "bloodyQueenMonster") == true,
        train_present = response_sfs_has_key(info, "train") == true,
        bus_list_present = response_sfs_has_key(info, "busList") == true,
    }
    entry.is_monster_or_boss, entry.is_monster, entry.is_boss, entry.classification_source =
        march_payload.current_march_classification(entry.march_type)
    return entry
end

function march_payload.snapshot_march_push(payload, route)
    local snapshot = {
        observed_at = runtime_clock(),
        route = route,
        servers = {},
        total_entries = 0,
        parser_source = "PushWorldMarchWorldGet.serverMarchArr[*].marchInfos",
    }
    local function add_server(server_value, source)
        if server_value == nil then return end
        local ok_marches, marches = call(server_value, "GetSFSArray", "marchInfos")
        if not ok_marches or marches == nil then return end
        local count = response_array_count(marches)
        local server = {
            server_id = response_number(server_value, "serverId"),
            source = source,
            march_info_count = count,
            uuid_set_present = response_sfs_has_key(server_value, "uuidSet") == true,
            entries = {},
        }
        local bounded = math.min(count, CFG.MAX_POINTS)
        for index = 0, bounded - 1 do
            local ok_info, info = call(marches, "GetSFSObject", index)
            if ok_info and info ~= nil then
                server.entries[#server.entries + 1] = march_payload.raw_march_entry(
                    info, server.server_id, source .. ".marchInfos[" .. tostring(index) .. "]")
            end
        end
        snapshot.total_entries = snapshot.total_entries + #server.entries
        snapshot.servers[#snapshot.servers + 1] = server
    end

    local ok_servers, servers = call(payload, "GetSFSArray", "serverMarchArr")
    if ok_servers and servers ~= nil then
        local count = math.min(response_array_count(servers), CFG.MAX_RESPONSE_ENVELOPES)
        for index = 0, count - 1 do
            local ok_server, server = call(servers, "GetSFSObject", index)
            if ok_server and server ~= nil then
                add_server(server, "$.serverMarchArr[" .. tostring(index) .. "]")
            end
        end
    else
        -- ParseDataWorldMarchGet itself accepts one server object containing
        -- marchInfos, so retain that exact shape if this dispatch route exposes
        -- the nested object instead of the outer HandleWorldMarchGet payload.
        add_server(payload, "$")
    end
    return snapshot
end

function march_payload.tile_in_envelope(tile, envelope)
    return tile ~= nil and envelope ~= nil
        and tile.x >= envelope.tile_left and tile.x < envelope.tile_right_exclusive
        and tile.y >= envelope.tile_bottom and tile.y < envelope.tile_top_exclusive
end

function march_payload.correlated_march_record(world, entry, envelope)
    if entry == nil or entry.is_monster_or_boss ~= true then return nil, false end
    local point_id, tile, point_source = nil, nil, nil
    for _, candidate in ipairs({
        { value = entry.start_pos, source = "startPos" },
        { value = entry.target_pos, source = "targetPos" },
        { value = entry.main_point_id, source = "mainPointId" },
    }) do
        if candidate.value ~= nil then
            local observed = index_to_tile(world, candidate.value)
            if march_payload.tile_in_envelope(observed, envelope) then
                point_id, tile, point_source = candidate.value, observed, candidate.source
                break
            end
        end
    end
    if point_id == nil then return nil, false end
    local uuid = entry.uuid
    if uuid == nil or tostring(uuid) == "" or tostring(uuid) == "0" then return nil, true end
    local template = monster_template_values(entry.monster_id, nil)
    return {
        id = point_id,
        pointId = point_id,
        kind = "monster",
        uuid = uuid,
        serverId = entry.target_server or entry.server or entry.server_id or scan_request.server_id or 0,
        srcServerId = entry.src_server or 0,
        worldId = entry.world_id or scan_request.world_id or 0,
        x = tile.x,
        y = tile.y,
        pointIdSource = "push.world.march.world.get.new." .. point_source,
        level = template.level,
        monsterId = entry.monster_id,
        marchType = entry.march_type,
        monsterType = nil,
        monsterTypeStatus = "not_a_raw_WorldMarch.UpdateFromSFS_field",
        isMonsterOrBoss = true,
        isMonster = entry.is_monster,
        isBoss = entry.is_boss,
        classificationSource = entry.classification_source,
        eventId = entry.event_id,
        eventUuid = entry.event_uuid,
        bossId = entry.boss_id,
        curHp = entry.cur_hp,
        maxHp = entry.max_hp,
        runningHp = entry.running_hp,
        invasionBossInfoPresent = entry.invasion_boss_info_present,
        zMBossInfoPresent = entry.zm_boss_info_present,
        allianceBossInfoPresent = entry.alliance_boss_info_present,
        strongholdBossPresent = entry.stronghold_boss_present,
        allianceChallengeInfoPresent = entry.alliance_challenge_info_present,
        cityBattleS1MonsterInfoPresent = entry.city_battle_s1_monster_info_present,
        bloodyQueenMonsterPresent = entry.bloody_queen_monster_present,
        trainPresent = entry.train_present,
        busListPresent = entry.bus_list_present,
        recommendedPower = template.recommended_power,
        recommendedPowerSource = template.source,
        recommendedPowerTemplateId = template.template_id,
        source = "PushWorldMarchWorldGet.serverMarchArr.marchInfos",
    }, true
end

function march_payload.correlate_march_snapshot(world, snapshot, envelope)
    if snapshot == nil or envelope == nil then return nil end
    local records = {}
    local spatial_entries = 0
    local server_entries = 0
    local matching_server_count = 0
    local expected_server = scan_request and tonumber(scan_request.server_id) or nil
    for _, server in ipairs(snapshot.servers or {}) do
        local observed_server = tonumber(server.server_id)
        local server_matches = expected_server == nil or observed_server == nil
            or observed_server == expected_server
        if server_matches then
            matching_server_count = matching_server_count + 1
            server_entries = server_entries + #(server.entries or {})
            for _, entry in ipairs(server.entries or {}) do
                local matched_position = false
                for _, point_id in ipairs({ entry.start_pos, entry.target_pos, entry.main_point_id }) do
                    if point_id ~= nil and march_payload.tile_in_envelope(index_to_tile(world, point_id), envelope) then
                        matched_position = true
                        break
                    end
                end
                if matched_position then
                    spatial_entries = spatial_entries + 1
                    local record = select(1, march_payload.correlated_march_record(world, entry, envelope))
                    if record ~= nil then records[#records + 1] = record end
                end
            end
        end
    end
    local correlated = spatial_entries > 0
    local method = correlated and "spatial_index_in_correlated_world_get_block_envelope" or nil
    -- Empty AOI snapshots contain no march coordinate to compare. The current
    -- handler's serverMarchArr grouping plus this probe's one-request-at-a-time
    -- window still lets an empty snapshot prove that the matching server replied,
    -- while keeping that weaker correlation method explicit in diagnostics.
    if not correlated and matching_server_count > 0 and server_entries == 0
        and snapshot.observed_at >= (monster_march_response_started_at or 0) then
        correlated = true
        method = "request_window_matching_server_empty_marchInfos"
    end
    return {
        correlated = correlated,
        method = method,
        records = records,
        spatial_entries = spatial_entries,
        server_entries = server_entries,
        matching_server_count = matching_server_count,
        total_entries = snapshot.total_entries or 0,
        route = snapshot.route,
    }
end

function march_payload.try_correlate_current_march_pushes(world)
    if monster_march_response_received == true or monster_view_response_envelope == nil then return false end
    for _, snapshot in ipairs(monster_current_pushes or {}) do
        if snapshot.processed ~= true then
            snapshot.processed = true
            local result = march_payload.correlate_march_snapshot(world, snapshot, monster_view_response_envelope)
            if result ~= nil and result.correlated then
                monster_march_response_received = true
                monster_march_response_count = monster_march_response_count + 1
                march_payload_stats.spatial_match_count = march_payload_stats.spatial_match_count
                    + (tonumber(result.spatial_entries) or 0)
                if result.method == "request_window_matching_server_empty_marchInfos" then
                    march_payload_stats.empty_correlated_count = march_payload_stats.empty_correlated_count + 1
                end
                if monster_current_diagnostic ~= nil then
                    monster_current_diagnostic.march_response_observed = true
                    monster_current_diagnostic.march_response_success = true
                    monster_current_diagnostic.march_response_route = result.route
                    monster_current_diagnostic.march_correlation_method = result.method
                    monster_current_diagnostic.march_payload_entry_count = result.total_entries
                    monster_current_diagnostic.march_count_after_response = result.spatial_entries
                    monster_current_diagnostic.boss_count_after_response = #result.records
                    monster_current_diagnostic.monster_or_boss_count_after_response = #result.records
                    monster_current_diagnostic.raw_march_records = result.records
                end
                return true
            end
            march_payload_stats.uncorrelated_push_count = march_payload_stats.uncorrelated_push_count + 1
        end
    end
    return false
end

local function response_envelopes(payload)
    local output, seen = {}, {}
    local function visit(value, source, depth)
        if value == nil or depth > 4 or #output >= CFG.MAX_RESPONSE_ENVELOPES then return end
        local left_bottom = response_number(value, "leftBottom")
        local right_top = response_number(value, "rightTop")
        if left_bottom ~= nil and right_top ~= nil then
            local server_id = response_number(value, "serverId")
            local world_id = response_number(value, "worldId")
            local identity = table.concat({
                tostring(server_id or ""), tostring(world_id or ""),
                tostring(left_bottom), tostring(right_top),
            }, ":")
            if not seen[identity] then
                seen[identity] = true
                output[#output + 1] = {
                    server_id = server_id,
                    world_id = world_id,
                    left_bottom = left_bottom,
                    right_top = right_top,
                    source = source,
                }
            end
        end

        local function visit_array(array, array_source)
            if array == nil or #output >= CFG.MAX_RESPONSE_ENVELOPES then return end
            local observed_count = response_array_count(array)
            local count = observed_count > 0
                and math.min(observed_count, CFG.MAX_RESPONSE_ENVELOPES)
                or CFG.MAX_RESPONSE_ENVELOPES
            for index = 0, count - 1 do
                local ok_item, item = call(array, "GetSFSObject", index)
                if not ok_item or item == nil then
                    if observed_count == 0 then break end
                else
                    visit(item, array_source .. "[" .. tostring(index) .. "]", depth + 1)
                end
            end
        end

        local ok_server_points, server_points = call(value, "GetSFSArray", "serverPointArr")
        if ok_server_points and server_points ~= nil then
            visit_array(server_points, source .. ".array[serverPointArr]")
        end

        local ok_keys, keys = call(value, "GetKeys")
        if not ok_keys or keys == nil then return end
        each(keys, CFG.MAX_RESPONSE_ENVELOPES, function(key)
            local key_text = tostring(key or "")
            local ok_object, child = call(value, "GetSFSObject", key)
            if ok_object and child ~= nil then
                visit(child, source .. ".object[" .. key_text .. "]", depth + 1)
            end
            if key_text ~= "serverPointArr" then
                local ok_array, array = call(value, "GetSFSArray", key)
                if ok_array and array ~= nil then
                    visit_array(array, source .. ".array[" .. key_text .. "]")
                end
            end
            return #output < CFG.MAX_RESPONSE_ENVELOPES
        end)
    end
    visit(payload, "$", 0)
    return output
end

local function normalize_response_envelope(world, envelope)
    if envelope == nil or scan_request == nil then return nil end
    local left_bottom = index_to_tile(world, envelope.left_bottom)
    local right_top = index_to_tile(world, envelope.right_top)
    if left_bottom == nil or right_top == nil then return nil end
    local tile_left = math.min(left_bottom.x, right_top.x)
    local tile_bottom = math.min(left_bottom.y, right_top.y)
    local tile_right_exclusive = math.max(left_bottom.x, right_top.x)
    local tile_top_exclusive = math.max(left_bottom.y, right_top.y)
    if tile_right_exclusive <= tile_left or tile_top_exclusive <= tile_bottom then return nil end
    local tile_right = tile_right_exclusive - 1
    local tile_top = tile_top_exclusive - 1
    local block_size = math.max(1, tonumber(scan_request.block_size) or 1)
    return {
        server_id = envelope.server_id,
        world_id = envelope.world_id,
        left_bottom = envelope.left_bottom,
        right_top = envelope.right_top,
        source = envelope.source,
        tile_left = tile_left,
        tile_bottom = tile_bottom,
        tile_right = tile_right,
        tile_top = tile_top,
        tile_right_exclusive = tile_right_exclusive,
        tile_top_exclusive = tile_top_exclusive,
        block_left = math.floor(tile_left / block_size),
        block_bottom = math.floor(tile_bottom / block_size),
        block_right = math.floor(tile_right / block_size),
        block_top = math.floor(tile_top / block_size),
    }
end

local function restore_response_hook()
    if response_hook == nil or response_hook.proxy == nil then return true end
    local restored = 0
    for index = #(response_hook.hooks or {}), 1, -1 do
        local hook = response_hook.hooks[index]
        local current = safe_get(response_hook.proxy, hook.field)
        if same_delegate(current, hook.installed) then
            local ok = pcall(function() response_hook.proxy[hook.field] = hook.original end)
            if ok then restored = restored + 1 end
        end
    end
    local expected = #(response_hook.hooks or {})
    response_hook.restored_count = restored
    response_hook.restore_complete = restored == expected
    return response_hook.restore_complete
end

local function restore_manager_flag(point_manager)
    if not restore_state.manager_flag_touched then return true end
    local restored = select(1, reflected_set_value(
        point_manager, "isRecvViewPoints", boxed_bool(restore_state.manager_flag_original == true)))
    restore_state.manager_flag_touched = false
    return restored == true
end

local function block_key(x, y)
    return tostring(math.floor(tonumber(x) or -1)) .. ":" .. tostring(math.floor(tonumber(y) or -1))
end

local function initialize_requested_coverage(value)
    requested_block_keys = {}
    covered_block_keys = {}
    requested_block_count = 0
    covered_block_count = 0
    for _, block in ipairs(value and (value.requested_blocks or value.blocks) or {}) do
        local key = block_key(block.x, block.y)
        if requested_block_keys[key] ~= true then
            requested_block_keys[key] = true
            requested_block_count = requested_block_count + 1
        end
    end
    return requested_block_count > 0
end

local function active_batch_covered_count()
    local count = 0
    for _, block in ipairs(request and request.requested_blocks or {}) do
        if covered_block_keys[block_key(block.x, block.y)] == true then count = count + 1 end
    end
    return count
end

local function mark_response_coverage(normalized)
    if normalized == nil then return 0 end
    local added = 0
    for _, block in ipairs(request and request.requested_blocks or {}) do
        if block.x >= normalized.block_left and block.x <= normalized.block_right
            and block.y >= normalized.block_bottom and block.y <= normalized.block_top then
            local key = block_key(block.x, block.y)
            if requested_block_keys[key] and not covered_block_keys[key] then
                covered_block_keys[key] = true
                covered_block_count = covered_block_count + 1
                added = added + 1
            end
        end
    end
    return added
end

local function new_full_scan_entry(value)
    local expected = {}
    for _, block in ipairs(value and value.requested_blocks or {}) do
        expected[block_key(block.x, block.y)] = true
    end
    return {
        request = value,
        expected_keys = expected,
        covered_keys = {},
        expected_count = value and value.requested_block_count or 0,
        covered_count = 0,
        complete = false,
        recorded = false,
        sent_at = nil,
        response_count = 0,
        accepted_envelope_count = 0,
        rejected_envelope_count = 0,
    }
end

local function mark_full_scan_coverage(entry, normalized)
    if entry == nil or normalized == nil then return 0 end
    local added = 0
    for _, block in ipairs(entry.request and entry.request.requested_blocks or {}) do
        if block.x >= normalized.block_left and block.x <= normalized.block_right
            and block.y >= normalized.block_bottom and block.y <= normalized.block_top then
            local key = block_key(block.x, block.y)
            if entry.expected_keys[key] and not entry.covered_keys[key] then
                entry.covered_keys[key] = true
                entry.covered_count = entry.covered_count + 1
                added = added + 1
                if requested_block_keys[key] and not covered_block_keys[key] then
                    covered_block_keys[key] = true
                    covered_block_count = covered_block_count + 1
                end
            end
        end
    end
    entry.complete = entry.expected_count > 0 and entry.covered_count == entry.expected_count
    return added
end

local function match_full_scan_entry(normalized)
    local matched, match_count = nil, 0
    for _, entry in pairs(full_scan_entries) do
        if entry.complete ~= true then
            local value = entry.request
            local identity_matches = normalized ~= nil
                and (normalized.server_id == nil
                    or tonumber(normalized.server_id) == tonumber(value.server_id))
                and (normalized.world_id == nil
                    or tonumber(normalized.world_id) == tonumber(value.world_id))
            local bounds_match = identity_matches
                and tonumber(normalized.left_bottom) == tonumber(value.left_bottom_index)
                and tonumber(normalized.right_top) == tonumber(value.right_top_index)
            if bounds_match then
                matched = entry
                match_count = match_count + 1
            end
        end
    end
    if match_count == 1 then return matched, nil end
    return nil, match_count == 0 and "full_scan_response_unmatched"
        or "full_scan_response_ambiguous"
end

local function public_covered_blocks()
    local output = {}
    for _, block in ipairs(scan_request and (scan_request.requested_blocks or scan_request.blocks) or {}) do
        local key = block_key(block.x, block.y)
        output[#output + 1] = {
            x = block.x,
            y = block.y,
            block_index = block.block_index,
            covered = covered_block_keys[key] == true,
        }
    end
    return output
end

local function install_response_hook(world, point_manager)
    local cs = rawget(_G, "CS")
    local proxy_type = cs and safe_get(cs, "BaseUtils")
        and safe_get(safe_get(cs, "BaseUtils"), "MessageFactoryProxy")
    local proxy = proxy_type and safe_get(proxy_type, "Instance")
    if proxy == nil then return false, "message_factory_proxy_unavailable" end
    local message_type = cs and safe_get(cs, "WorldGetBlockMessage")
    local message = message_type and safe_get(message_type, "Instance")
    if message == nil then return false, "world_get_block_message_unavailable" end
    local ok_command, command = call(message, "GetMsgId")
    command = ok_command and tostring(command or "") or ""
    if command == "" then return false, "world_get_block_command_unavailable" end
    target_command = command
    response_hook = { proxy = proxy, hooks = {}, target_command = command }

    local function observe(command_value, payload, route)
        local command_text = tostring(command_value or "")
        if monster_march_protocol ~= nil
            and command_text == tostring(monster_march_protocol) then
            if (phase == "monster_waiting_response" or phase == "monster_waiting_march_push")
                and monster_current_view ~= nil then
                if monster_march_response_received == true then
                    if monster_march_hook ~= nil then
                        monster_march_hook.duplicate_push_count =
                            (tonumber(monster_march_hook.duplicate_push_count) or 0) + 1
                    end
                elseif #monster_current_pushes >= CFG.MAX_MARCH_PUSHES_PER_VIEW then
                    monster_march_response_handler_error = "bounded march push buffer exceeded"
                else
                    local snapshot = march_payload.snapshot_march_push(payload, route)
                    march_payload_stats.push_count = march_payload_stats.push_count + 1
                    march_payload_stats.entry_count = march_payload_stats.entry_count
                        + (tonumber(snapshot.total_entries) or 0)
                    monster_current_pushes[#monster_current_pushes + 1] = snapshot
                    monster_march_response_error_code = scalar_field(payload, { "errorCode" })
                    if monster_current_diagnostic ~= nil then
                        monster_current_diagnostic.march_push_count = #monster_current_pushes
                        monster_current_diagnostic.latest_march_payload_entry_count = snapshot.total_entries
                        monster_current_diagnostic.march_response_error_code = monster_march_response_error_code
                    end
                    march_payload.try_correlate_current_march_pushes(world)
                end
            end
            return
        end
        if command_text ~= target_command then return end
        target_response_count = target_response_count + 1
        local envelopes = response_envelopes(payload)
        local accepted_any = false
        if phase == "monster_waiting_response" and monster_current_view ~= nil then
            for _, envelope in ipairs(envelopes) do
                local normalized = normalize_response_envelope(world, envelope)
                if normalized ~= nil
                    and monster_current_view.x >= normalized.tile_left
                    and monster_current_view.x < normalized.tile_right_exclusive
                    and monster_current_view.y >= normalized.tile_bottom
                    and monster_current_view.y < normalized.tile_top_exclusive then
                    accepted_any = true
                    monster_view_response_received = true
                    monster_view_response_route = route
                    monster_view_response_envelope = normalized
                    march_payload.try_correlate_current_march_pushes(world)
                    break
                end
            end
            if not accepted_any then rejected_response_count = rejected_response_count + 1 end
            return
        end
        -- Once the recovered monster phase has started, target-command replies
        -- outside its explicit waiting state are late/duplicate official-view
        -- traffic. Never allow them to re-enter the completed serial/direct
        -- batch state machine.
        if #monster_views > 0 then
            return
        end
        for _, envelope in ipairs(envelopes) do
            local normalized = normalize_response_envelope(world, envelope)
            if #observed_response_envelopes < CFG.MAX_DIAGNOSTIC_EVENTS then
                observed_response_envelopes[#observed_response_envelopes + 1] = normalized or envelope
            end
            if full_scan_active or phase == "waiting_full_scan" then
                local entry, match_error = match_full_scan_entry(normalized)
                if entry ~= nil then
                    accepted_any = true
                    local response_event = next_event()
                    if #full_scan_response_events < CFG.MAX_DIAGNOSTIC_EVENTS then
                        full_scan_response_events[#full_scan_response_events + 1] = {
                            sequence = entry.request.sequence,
                            event = response_event,
                        }
                    end
                    entry.response_count = entry.response_count + 1
                    entry.accepted_envelope_count = entry.accepted_envelope_count + 1
                    local newly_covered = mark_full_scan_coverage(entry, normalized)
                    matched_responses[#matched_responses + 1] = {
                        batch_sequence = entry.request.sequence,
                        route = route,
                        command = target_command,
                        server_id = envelope.server_id,
                        world_id = envelope.world_id,
                        left_bottom = envelope.left_bottom,
                        right_top = envelope.right_top,
                        source = envelope.source,
                        normalized = normalized,
                        observed_at = os.time(),
                        event = response_event,
                        newly_covered_blocks = newly_covered,
                        covered_block_count = covered_block_count,
                        requested_block_count = requested_block_count,
                        active_batch_covered_count = entry.covered_count,
                        active_batch_requested_count = entry.expected_count,
                    }
                else
                    if match_error == "full_scan_response_ambiguous" then
                        rejected_response_count = rejected_response_count + 1
                    end
                end
            else
                local identity_matches = normalized ~= nil
                    and request ~= nil
                    and (normalized.server_id == nil
                        or tonumber(normalized.server_id) == tonumber(request.server_id))
                    and (normalized.world_id == nil
                        or tonumber(normalized.world_id) == tonumber(request.world_id))
                local requested_coverage = request and request.requested_coverage or nil
                local overlaps_target = identity_matches and requested_coverage ~= nil
                    and normalized.block_right >= requested_coverage.left
                    and normalized.block_top >= requested_coverage.bottom
                    and normalized.block_left <= requested_coverage.right
                    and normalized.block_bottom <= requested_coverage.top
                if overlaps_target then
                    accepted_any = true
                    local newly_covered = mark_response_coverage(normalized)
                    matched_responses[#matched_responses + 1] = {
                        batch_sequence = request.sequence,
                        route = route,
                        command = target_command,
                        server_id = envelope.server_id,
                        world_id = envelope.world_id,
                        left_bottom = envelope.left_bottom,
                        right_top = envelope.right_top,
                        source = envelope.source,
                        normalized = normalized,
                        observed_at = os.time(),
                        newly_covered_blocks = newly_covered,
                        covered_block_count = covered_block_count,
                        requested_block_count = requested_block_count,
                        active_batch_covered_count = active_batch_covered_count(),
                        active_batch_requested_count = request.requested_block_count,
                    }
                    if request.requested_block_count > 0
                        and active_batch_covered_count() == request.requested_block_count then
                        phase = "batch_response_matched"
                    end
                end
            end
        end
        if accepted_any then
            accumulate_scan_records(world, point_manager)
        else
            rejected_response_count = rejected_response_count + 1
        end
    end

    local function assign(field, wrapper)
        local original = safe_get(proxy, field)
        if original == nil then return false, field .. "_delegate_unavailable" end
        local ok, assign_error = pcall(function() proxy[field] = wrapper end)
        if not ok then return false, tostring(assign_error) end
        local installed = safe_get(proxy, field)
        if installed == nil then
            pcall(function() proxy[field] = original end)
            return false, field .. "_hook_readback_failed"
        end
        response_hook.hooks[#response_hook.hooks + 1] = {
            field = field, original = original, installed = installed,
        }
        return true, nil
    end

    local original1 = safe_get(proxy, "DispatchResponse1")
    if original1 == nil then return false, "dispatch_response1_delegate_unavailable" end
    local wrapper1 = function(event)
        local command_value = event_parameter(event, "cmd")
        local payload = event_parameter(event, "params")
        local result = invoke_dispatch_delegate(original1, event)
        pcall(observe, command_value, payload, "DispatchResponse1")
        return result
    end
    local installed1, error1 = assign("DispatchResponse1", wrapper1)
    if not installed1 then restore_response_hook(); return false, error1 end

    local original2 = safe_get(proxy, "DispatchResponse2")
    if original2 ~= nil then
        local wrapper2 = function(command_value, payload)
            local result = invoke_dispatch_delegate(original2, command_value, payload)
            pcall(observe, command_value, payload, "DispatchResponse2")
            return result
        end
        local installed2, error2 = assign("DispatchResponse2", wrapper2)
        if not installed2 then response_hook.secondary_error = error2 end
    end
    return true, nil
end

local function restore_monster_march_hooks()
    if monster_march_hook == nil then
        monster_march_hook_restored = true
        return true
    end
    -- The current-build path is managed BaseMessage.Send -> CSHandleResponse.
    -- No Lua SFSNetwork function is replaced, so cleanup only records that the
    -- managed-request observation state has been retired.
    monster_march_hook.restore_complete = true
    monster_march_hook_restored = true
    return true
end

local function install_monster_march_hooks()
    local cs = rawget(_G, "CS")
    local push_type = cs and safe_get(cs, "PushWorldMarchWorldGet")
    local push = push_type and safe_get(push_type, "Instance")
    if push == nil then return false, "PushWorldMarchWorldGet.Instance unavailable" end
    local ok_protocol, protocol = call(push, "GetMsgId")
    protocol = ok_protocol and tostring(protocol or "") or ""
    if protocol == "" then return false, "PushWorldMarchWorldGet.GetMsgId unavailable" end
    if protocol ~= "push.world.march.world.get.new" then
        return false, "unexpected current march AOI push id: " .. tostring(protocol)
    end

    monster_march_protocol = protocol
    monster_march_hook = {
        transport = "PushWorldMarchWorldGet -> WorldMarchDataManager.HandleWorldMarchGet",
        duplicate_push_count = 0,
    }
    monster_march_hook_restored = false
    return true, nil
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
    local array = true
    local maximum, count = 0, 0
    for key in pairs(value) do
        if type(key) ~= "number" or key < 1 or key ~= math.floor(key) then array = false; break end
        maximum = math.max(maximum, key)
        count = count + 1
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
    file:write(json_encode(value))
    file:close()
    return true
end

function M.ResetScan(command_id)
    local timer_handle = restore_state and restore_state.timer_handle or nil
    M.active_command_id = command_id
    M.focus_pending = nil
    M.power_enrichment = {}
    M.resource_enrichment = {}
    phase = "waiting_scene"
    completed = false
    transition_requested = false
    transition_count = 0
    stable_point_count = nil
    stable_since = 0
    request_sent_count = 0
    request_sent_at = nil
    target_command = nil
    response_hook = nil
    request = nil
    scan_request = nil
    batch_requests = {}
    active_batch_index = 0
    completed_batch_count = 0
    batch_results = {}
    full_scan_entries = {}
    next_batch_index = 2
    full_scan_active = false
    full_scan_send_events = {}
    full_scan_response_events = {}
    event_ordinal = 0
    full_scan_peak_inflight = 0
    full_scan_wave_count = 0
    scan_records = {}
    scan_record_keys = {}
    scan_duplicate_record_count = 0
    scan_capture_count = 0
    scan_capture_error = nil
    before_capture = nil
    matched_responses = {}
    requested_block_keys = {}
    covered_block_keys = {}
    requested_block_count = 0
    covered_block_count = 0
    target_response_count = 0
    rejected_response_count = 0
    observed_response_envelopes = {}
    last_error = nil
    monster_views = {}
    monster_view_index = 0
    monster_view_started_at = nil
    monster_view_response_started_at = nil
    monster_camera_move_count = 0
    monster_capture_count = 0
    monster_views_with_marches = 0
    monster_views_with_bosses = 0
    monster_max_march_count = 0
    monster_original_view = nil
    monster_original_zoom = nil
    monster_restore_started_at = nil
    monster_camera_restored = false
    monster_current_view = nil
    monster_current_world_position = nil
    monster_official_request_count = 0
    monster_view_response_received = false
    monster_view_response_route = nil
    monster_view_response_envelope = nil
    monster_march_hook = nil
    monster_march_hook_restored = nil
    monster_march_protocol = nil
    monster_march_request_count = 0
    monster_march_response_count = 0
    monster_march_foreign_send_count = 0
    monster_march_response_received = false
    monster_march_response_error_code = nil
    monster_march_response_handler_error = nil
    monster_march_response_started_at = nil
    monster_current_pushes = {}
    march_payload_stats = {
        push_count = 0,
        entry_count = 0,
        spatial_match_count = 0,
        empty_correlated_count = 0,
        uncorrelated_push_count = 0,
    }
    monster_view_diagnostics = {}
    monster_current_diagnostic = nil
    restore_state = {
        manager_flag_original = nil,
        manager_flag_touched = false,
        timer_handle = timer_handle,
        transport_hook_restored = nil,
        transport_flag_restored = nil,
        monster_manager_flag_original = nil,
        monster_manager_flag_touched = false,
        monster_manager_flag_restored = nil,
        monster_world_flag_original = nil,
        monster_world_flag_touched = false,
        monster_world_flag_restored = nil,
    }
    monster_terminal_state = nil
    monster_terminal_error = nil
    monster_restore_distance = nil
    monster_restore_zoom_delta = nil
end

function M.ProcessScanCommand(now)
    if not M.PERSISTENT or not completed then return false end
    local file = io.open(PATHS.command, "rb")
    if file == nil then return false end
    local text = file:read("*a") or ""
    file:close()
    if #text > 4096 then
        os.remove(PATHS.command)
        last_error = "scan_command_too_large"
        write_json(PATHS.status, status_payload and status_payload(now) or {
            probeVersion = M.VERSION, state = phase, updatedAt = now, error = last_error,
        })
        return false
    end
    local values = {}
    for line in string.gmatch(text, "[^\r\n]+") do
        local key, value = string.match(line, "^([%w_]+)=(.*)$")
        if key ~= nil then values[key] = value end
    end
    local command_id = tostring(values.commandId or "")
    local power_target_limit = values.powerTargetLimit ~= nil and tonumber(values.powerTargetLimit) or nil
    local resource_target_limit = values.resourceTargetLimit ~= nil and tonumber(values.resourceTargetLimit) or nil
    if values.schema ~= "1" or values.mode ~= "run_once"
        or not string.match(command_id, "^[%w_-]+$") or #command_id > 128
        or (values.powerTargetLimit ~= nil and (power_target_limit == nil
            or power_target_limit ~= math.floor(power_target_limit)
            or power_target_limit < 1 or power_target_limit > CFG.MAX_SCAN_RECORDS))
        or (values.resourceTargetLimit ~= nil and (resource_target_limit == nil
            or resource_target_limit ~= math.floor(resource_target_limit)
            or resource_target_limit < 1 or resource_target_limit > CFG.MAX_SCAN_RECORDS)) then
        os.remove(PATHS.command)
        last_error = "invalid_scan_command"
        return false
    end
    os.remove(PATHS.command)
    os.remove(PATHS.result)
    os.remove(PATHS.monster_diagnostics)
    os.remove(PATHS.direct_diagnostics)
    os.remove(PATHS.focus_command)
    os.remove(PATHS.focus_result)
    M.ResetScan(command_id)
    M.power_target_limit = power_target_limit and math.floor(power_target_limit) or nil
    M.resource_target_limit = resource_target_limit and math.floor(resource_target_limit) or nil
    return true
end

function M.ProcessFocus(now)
    if M.focus_pending == nil then
        local file = io.open(PATHS.focus_command, "rb")
        if file == nil then return true end
        local text = file:read("*a") or ""
        file:close()
        os.remove(PATHS.focus_command)
        local values = {}
        for line in string.gmatch(text, "[^\r\n]+") do
            local key, value = string.match(line, "^([%w_]+)=(.*)$")
            if key ~= nil then values[key] = value end
        end
        local command_id = tostring(values.commandId or "")
        local x = tonumber(values.x)
        local y = tonumber(values.y)
        local server_id = tonumber(values.serverId)
        if values.schema ~= "1" or not string.match(command_id, "^[%w_-]+$")
            or #command_id > 128 or x == nil or y == nil
            or x < 0 or x > 999 or y < 0 or y > 999 then
            write_json(PATHS.focus_result, {
                schemaVersion = 1, commandId = command_id, state = "failed",
                error = "invalid_focus_command", completedAt = now,
            })
            return false
        end
        if server_id ~= nil and server_id > 0 and scan_request ~= nil
            and tonumber(scan_request.server_id) ~= nil
            and server_id ~= tonumber(scan_request.server_id) then
            write_json(PATHS.focus_result, {
                schemaVersion = 1, commandId = command_id, state = "failed",
                error = "focus_server_mismatch", x = x, y = y, completedAt = now,
            })
            return false
        end
        M.focus_pending = {
            command_id = command_id, x = x, y = y, server_id = server_id,
            point_id = tostring(values.pointId or ""), started_at = runtime_clock(),
            transition_requested = false, move_requested = false, route = nil,
        }
    end

    local pending = M.focus_pending
    local scene = scene_identity()
    if scene ~= "world" then
        if scene == "city" and pending.transition_requested ~= true then
            local scene_utils = rawget(_G, "SceneUtils")
            local change = safe_get(scene_utils, "ChangeToWorld")
            if type(change) == "function" then
                local ok = pcall(change)
                if not ok then ok = pcall(change, scene_utils) end
                pending.transition_requested = ok == true
            end
        end
        if runtime_clock() - pending.started_at < 6 then return true end
        write_json(PATHS.focus_result, {
            schemaVersion = 1, commandId = pending.command_id, state = "failed",
            error = "focus_world_scene_timeout", x = pending.x, y = pending.y,
            completedAt = now,
        })
        M.focus_pending = nil
        return false
    end

    local world = select(1, runtime_world())
    if world == nil then return true end
    if pending.move_requested ~= true then
        local moved, move_error, route = move_official_monster_view(world, pending)
        if not moved then
            write_json(PATHS.focus_result, {
                schemaVersion = 1, commandId = pending.command_id, state = "failed",
                error = move_error or "focus_camera_move_failed",
                x = pending.x, y = pending.y, completedAt = now,
            })
            M.focus_pending = nil
            return false
        end
        pending.move_requested = true
        pending.route = route
        pending.move_started_at = runtime_clock()
    end
    local observed = current_view(world)
    if view_distance(observed, pending) <= 3 then
        write_json(PATHS.focus_result, {
            schemaVersion = 1, commandId = pending.command_id, state = "completed",
            x = pending.x, y = pending.y, observedX = observed.x, observedY = observed.y,
            route = pending.route, completedAt = now,
        })
        M.focus_pending = nil
        return true
    end
    if runtime_clock() - (pending.move_started_at or pending.started_at) >= 3 then
        write_json(PATHS.focus_result, {
            schemaVersion = 1, commandId = pending.command_id, state = "failed",
            error = "focus_camera_evidence_timeout", x = pending.x, y = pending.y,
            observedX = observed.x, observedY = observed.y, route = pending.route,
            completedAt = now,
        })
        M.focus_pending = nil
        return false
    end
    return true
end

local function public_request(value)
    if value == nil then return nil end
    return {
        big_map = value.big_map,
        server_id = value.server_id,
        world_id = value.world_id,
        server_lod = value.server_lod,
        x = value.x,
        y = value.y,
        block_side = value.block_side,
        requested_blocks = value.requested_blocks,
        requested_block_count = value.requested_block_count,
        requested_coverage = value.requested_coverage,
        block_indexes = value.block_indexes,
        transport_index_count = value.transport_index_count,
        transport_coverage = value.transport_coverage,
        block_count = value.block_count,
        block_size = value.block_size,
        visible_left_bottom = value.visible_left_bottom,
        visible_right_top = value.visible_right_top,
        left_bottom = value.left_bottom,
        right_top = value.right_top,
        left_bottom_index = value.left_bottom_index,
        right_top_index = value.right_top_index,
        left_bottom_index_source = value.left_bottom_index_source,
        right_top_index_source = value.right_top_index_source,
        target_left_bottom = value.target_left_bottom,
        target_right_top = value.target_right_top,
    }
end

local function public_requests(values)
    local output = {}
    for _, value in ipairs(values or {}) do output[#output + 1] = public_request(value) end
    return output
end

local function public_scan(value)
    if value == nil then return nil end
    return {
        side = value.side,
        requested_blocks = value.requested_blocks or value.blocks,
        requested_block_count = value.requested_block_count,
        requested_coverage = value.requested_coverage or value.coverage,
        block_count = value.block_count,
        block_size = value.block_size,
        visible_left_bottom = value.visible_left_bottom,
        visible_right_top = value.visible_right_top,
        left_bottom = value.left_bottom,
        right_top = value.right_top,
    }
end

local function public_scan_summary(value)
    if value == nil then return nil end
    return {
        side = value.side,
        requested_block_count = requested_block_count,
        requested_coverage = value.requested_coverage or value.coverage,
        block_count = value.block_count,
        block_size = value.block_size,
        visible_left_bottom = value.visible_left_bottom,
        visible_right_top = value.visible_right_top,
        left_bottom = value.left_bottom,
        right_top = value.right_top,
    }
end

local function full_scan_inflight_count()
    local count = 0
    for index = 2, #batch_requests do
        local entry = full_scan_entries[index]
        if entry ~= nil and entry.complete ~= true then count = count + 1 end
    end
    return count
end

local function full_scan_active_sequences()
    local output = {}
    for index = 2, #batch_requests do
        local entry = full_scan_entries[index]
        if entry ~= nil and entry.complete ~= true then
            output[#output + 1] = entry.request.sequence
        end
    end
    return output
end

local function status_payload(now)
    local scene, current, city, world = scene_identity()
    local power_state = M.power_enrichment or {}
    local resource_state = M.resource_enrichment or {}
    return {
        probeVersion = M.VERSION,
        commandId = M.active_command_id,
        persistent = M.PERSISTENT,
        state = phase,
        updatedAt = now,
        error = last_error,
        completed = completed,
        scene = scene,
        current_scene_id = tonumber(current),
        city_scene_id = tonumber(city),
        world_scene_id = tonumber(world),
        transition_requested = transition_requested,
        transition_count = transition_count,
        stable_point_count = stable_point_count,
        request_sent_count = request_sent_count,
        request_sent_at = request_sent_at,
        active_batch_index = active_batch_index,
        completed_batch_count = completed_batch_count,
        planned_batch_count = #batch_requests,
        next_batch_index = next_batch_index,
        full_scan_active = full_scan_active,
        full_scan_inflight_count = full_scan_inflight_count(),
        full_scan_active_sequences = full_scan_active_sequences(),
        full_scan_send_events = full_scan_send_events,
        full_scan_response_events = full_scan_response_events,
        full_scan_peak_inflight = full_scan_peak_inflight,
        full_scan_wave_count = full_scan_wave_count,
        maximum_concurrency = MAX_CONCURRENCY,
        target_command = target_command,
        target_response_count = target_response_count,
        rejected_response_count = rejected_response_count,
        observed_response_envelopes = observed_response_envelopes,
        scan = public_scan_summary(scan_request),
        active_request_sequence = request and request.sequence or nil,
        requested_block_count = requested_block_count,
        covered_block_count = covered_block_count,
        accumulated_record_count = #scan_records,
        duplicate_record_count = scan_duplicate_record_count,
        post_response_capture_count = scan_capture_count,
        post_response_capture_error = scan_capture_error,
        monster_view_index = monster_view_index,
        monster_view_count = #monster_views,
        monster_camera_move_count = monster_camera_move_count,
        monster_official_request_count = monster_official_request_count,
        monster_march_request_count = monster_march_request_count,
        monster_march_response_count = monster_march_response_count,
        monster_march_payload_push_count = march_payload_stats.push_count,
        monster_march_payload_entry_count = march_payload_stats.entry_count,
        monster_march_spatial_match_count = march_payload_stats.spatial_match_count,
        monster_march_empty_correlated_count = march_payload_stats.empty_correlated_count,
        monster_march_uncorrelated_push_count = march_payload_stats.uncorrelated_push_count,
        monster_march_foreign_send_count = monster_march_foreign_send_count,
        monster_march_duplicate_push_count = monster_march_hook
            and (tonumber(monster_march_hook.duplicate_push_count) or 0) or 0,
        monster_view_diagnostic_count = #monster_view_diagnostics,
        monster_capture_count = monster_capture_count,
        monster_views_with_marches = monster_views_with_marches,
        monster_views_with_bosses = monster_views_with_bosses,
        monster_max_march_count = monster_max_march_count,
        monster_camera_restored = monster_camera_restored,
        monster_restore_distance = monster_restore_distance,
        monster_restore_zoom_delta = monster_restore_zoom_delta,
        response_hook_count = response_hook and #(response_hook.hooks or {}) or 0,
        monster_march_hook_restored = monster_march_hook_restored,
        power_enrichment_started = power_state.started == true,
        power_enrichment_complete = power_state.complete == true,
        power_initial_target_count = tonumber(power_state.initial_target_count) or 0,
        power_request_count = tonumber(power_state.requests) or 0,
        power_request_failure_count = tonumber(power_state.request_failures) or 0,
        power_cached_resolved_count = tonumber(power_state.cached_resolved) or 0,
        power_resolved_count = tonumber(power_state.resolved) or 0,
        power_unresolved_count = tonumber(power_state.unresolved) or 0,
        power_skipped_target_count = tonumber(power_state.skipped_target_count) or 0,
        power_target_limit = M.power_target_limit,
        power_retry_count = tonumber(power_state.retry) or 0,
        resource_enrichment_started = resource_state.started == true,
        resource_enrichment_complete = resource_state.complete == true,
        resource_initial_target_count = tonumber(resource_state.initial_target_count) or 0,
        resource_request_count = tonumber(resource_state.requests) or 0,
        resource_request_failure_count = tonumber(resource_state.request_failures) or 0,
        resource_cached_resolved_count = tonumber(resource_state.cached_resolved) or 0,
        resource_resolved_count = tonumber(resource_state.resolved) or 0,
        resource_unresolved_count = tonumber(resource_state.unresolved) or 0,
        resource_skipped_target_count = tonumber(resource_state.skipped_target_count) or 0,
        resource_target_limit = M.resource_target_limit,
        resource_retry_count = tonumber(resource_state.retry) or 0,
        registration_method = registration_method,
    }
end

local function cleanup(point_manager)
    full_scan_active = false
    local block_hook_restored = restore_response_hook()
    local march_hook_restored = restore_monster_march_hooks()
    local hook_restored = block_hook_restored and march_hook_restored
    local flag_restored = restore_manager_flag(point_manager)
    return hook_restored, flag_restored
end

local function direct_transport_complete()
    if requested_block_count ~= 10000 or covered_block_count ~= requested_block_count
        or request_sent_count ~= 65 or completed_batch_count ~= 65
        or next_batch_index ~= 66 or full_scan_inflight_count() ~= 0
        or full_scan_peak_inflight < 1 or full_scan_peak_inflight > MAX_CONCURRENCY
        or #full_scan_send_events ~= 64 or scan_capture_error ~= nil then
        return false
    end
    return true
end

local function write_monster_diagnostics(now, terminal_state, terminal_error)
    return write_json(PATHS.monster_diagnostics, {
        probeVersion = M.VERSION,
        updatedAt = now,
        state = terminal_state,
        error = terminal_error,
        view_count = #monster_view_diagnostics,
        march_request_count = monster_march_request_count,
        march_response_count = monster_march_response_count,
        march_payload_push_count = march_payload_stats.push_count,
        march_payload_entry_count = march_payload_stats.entry_count,
        march_spatial_match_count = march_payload_stats.spatial_match_count,
        march_empty_correlated_count = march_payload_stats.empty_correlated_count,
        march_uncorrelated_push_count = march_payload_stats.uncorrelated_push_count,
        foreign_send_count = monster_march_foreign_send_count,
        duplicate_push_count = monster_march_hook
            and (tonumber(monster_march_hook.duplicate_push_count) or 0) or 0,
        views = monster_view_diagnostics,
    })
end

local function restore_monster_world_flag(world)
    if not restore_state.monster_world_flag_touched then
        restore_state.monster_world_flag_restored = true
        return true
    end
    local restored = select(1, set_world_response_flag(world, restore_state.monster_world_flag_original == true))
    restore_state.monster_world_flag_touched = false
    restore_state.monster_world_flag_restored = restored == true
    return restore_state.monster_world_flag_restored
end

local function camera_restore_observation(world)
    local distance = view_distance(current_view(world), monster_original_view)
    local current_zoom = camera_zoom(world)
    local original_zoom = tonumber(monster_original_zoom)
    local zoom_delta = nil
    local zoom_ok = original_zoom == nil
    if original_zoom ~= nil and current_zoom ~= nil then
        zoom_delta = math.abs(current_zoom - original_zoom)
        zoom_ok = zoom_delta <= 0.05
    end
    local position_ok = distance <= 3
    monster_restore_distance = distance
    monster_restore_zoom_delta = zoom_delta
    return {
        distance = distance,
        current_zoom = current_zoom,
        zoom_delta = zoom_delta,
        position_ok = position_ok,
        zoom_ok = zoom_ok,
        restored = position_ok and zoom_ok,
    }
end

local function finish_monster_failure(now, world, point_manager, message, state)
    last_error = message
    write_monster_diagnostics(now, state or "monster_scan_failed", message)
    local world_flag_restored = restore_monster_world_flag(world)
    local hook_restored, flag_restored = cleanup(point_manager)
    restore_state.transport_hook_restored = hook_restored
    restore_state.transport_flag_restored = flag_restored
    restore_state.monster_manager_flag_restored = flag_restored
    if not world_flag_restored or not hook_restored or not flag_restored then
        last_error = tostring(message) .. "; cleanup restoration failed"
        state = "cleanup_restore_failed"
    end
    phase = state or "monster_scan_failed"
    completed = true
    write_json(PATHS.status, status_payload(now))
end

local function finish_success(now, world, point_manager)
    local kind_counts = {
        player_base = 0,
        resource_point = 0,
        monster = 0,
        alliance_building = 0,
        world_point = 0,
    }
    local point_type_counts = {}
    local direct_monster_count = 0
    for _, point in ipairs(scan_records) do
        local kind = tostring(point.kind or "world_point")
        kind_counts[kind] = (tonumber(kind_counts[kind]) or 0) + 1
        local point_type = tonumber(point.pointType)
        if point_type ~= nil then
            local point_type_key = tostring(math.floor(point_type))
            point_type_counts[point_type_key] = (tonumber(point_type_counts[point_type_key]) or 0) + 1
        end
        if kind == "monster" and point.source == "WorldPointManager._pointInfos" then
            direct_monster_count = direct_monster_count + 1
        end
    end
    local direct_world_point_mode = #monster_views == 0
    if direct_world_point_mode then
        if not direct_transport_complete()
            or direct_monster_count < 1
            or kind_counts.player_base < 1
            or kind_counts.resource_point < 1
            or kind_counts.alliance_building < 1 then
            finish_monster_failure(now, world, point_manager,
                "direct WorldPointInfo completion invariants were not satisfied",
                "monster_coverage_incomplete")
            return
        end
        monster_camera_restored = true
        monster_march_hook_restored = true
    elseif not direct_transport_complete()
        or #monster_views ~= EXPECTED_MONSTER_CAMERA_VIEWS
        or monster_view_index ~= EXPECTED_MONSTER_CAMERA_VIEWS + 1
        or monster_camera_move_count ~= EXPECTED_MONSTER_CAMERA_VIEWS
        or monster_official_request_count ~= EXPECTED_MONSTER_CAMERA_VIEWS
        or monster_march_request_count ~= 0
        or monster_march_response_count ~= EXPECTED_MONSTER_CAMERA_VIEWS
        or monster_march_foreign_send_count ~= 0
        or #monster_view_diagnostics ~= EXPECTED_MONSTER_CAMERA_VIEWS
        or monster_capture_count ~= EXPECTED_MONSTER_CAMERA_VIEWS
        or monster_views_with_bosses < 1
        or monster_camera_restored ~= true then
        finish_monster_failure(now, world, point_manager,
            "full_scan monster completion invariants were not satisfied", "monster_coverage_incomplete")
        return
    end
    table.sort(scan_records, function(left, right)
        if tonumber(left.worldId) ~= tonumber(right.worldId) then
            return tonumber(left.worldId) < tonumber(right.worldId)
        end
        if tonumber(left.serverId) ~= tonumber(right.serverId) then
            return tonumber(left.serverId) < tonumber(right.serverId)
        end
        return tonumber(left.id) < tonumber(right.id)
    end)
    local after_capture, capture_error = capture_points(world, point_manager, scan_request)
    if after_capture == nil then
        finish_monster_failure(now, world, point_manager,
            capture_error or "final point capture failed", "matched_response_capture_failed")
        return
    end
    local world_flag_restored = direct_world_point_mode and true or restore_monster_world_flag(world)
    local hook_restored, flag_restored = cleanup(point_manager)
    restore_state.transport_hook_restored = hook_restored
    restore_state.transport_flag_restored = flag_restored
    restore_state.monster_manager_flag_restored = flag_restored
    if not world_flag_restored or not hook_restored or not flag_restored then
        last_error = "full_scan cleanup restoration failed"
        phase = "cleanup_restore_failed"
        completed = true
        write_json(PATHS.status, status_payload(now))
        return
    end
    local added = {}
    for key, point in pairs(after_capture.identities) do
        if before_capture == nil or before_capture.identities[key] == nil then
            added[#added + 1] = point
        end
    end
    table.sort(added, function(a, b) return a.id < b.id end)
    local result = {
        schemaVersion = 2,
        probeVersion = M.VERSION,
        commandId = M.active_command_id,
        state = "proven",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
        scan = public_scan(scan_request),
        requests = public_requests(batch_requests),
        request = public_request(request),
        batch_results = batch_results,
        responses = matched_responses,
        requested_block_count = requested_block_count,
        covered_block_count = covered_block_count,
        request_sent_count = request_sent_count,
        completed_batch_count = completed_batch_count,
        serial_capability_probe_first = true,
        maximum_concurrency = MAX_CONCURRENCY,
        full_scan_wave_count = full_scan_wave_count,
        full_scan_send_events = full_scan_send_events,
        full_scan_response_events = full_scan_response_events,
        full_scan_peak_inflight = full_scan_peak_inflight,
        target_response_count = target_response_count,
        rejected_response_count = rejected_response_count,
        accumulated_record_count = #scan_records,
        duplicate_record_count = scan_duplicate_record_count,
        post_response_capture_count = scan_capture_count,
        monster_discovery_source = direct_world_point_mode
            and "WorldGetBlock.WorldPointInfo"
            or "PushWorldMarchWorldGet.serverMarchArr.marchInfos",
        direct_monster_record_count = direct_monster_count,
        kind_counts = kind_counts,
        point_type_counts = point_type_counts,
        point_records = scan_records,
        before_point_count = before_capture and before_capture.count or nil,
        after_point_count = after_capture.count,
        added_points = added,
        points_in_requested_bounds_after = after_capture.in_bounds,
        response_hook_restored = hook_restored,
        manager_flag_restored = flag_restored,
        world_response_flag_restored = world_flag_restored,
        camera_move_count = monster_camera_move_count,
        camera_restored = monster_camera_restored,
        camera_restore_distance = monster_restore_distance,
        camera_restore_zoom_delta = monster_restore_zoom_delta,
        monster_camera_view_count = #monster_views,
        monster_official_request_count = monster_official_request_count,
        monster_march_request_count = monster_march_request_count,
        monster_march_response_count = monster_march_response_count,
        monster_march_payload_push_count = march_payload_stats.push_count,
        monster_march_payload_entry_count = march_payload_stats.entry_count,
        monster_march_spatial_match_count = march_payload_stats.spatial_match_count,
        monster_march_empty_correlated_count = march_payload_stats.empty_correlated_count,
        monster_march_uncorrelated_push_count = march_payload_stats.uncorrelated_push_count,
        monster_march_foreign_send_count = monster_march_foreign_send_count,
        monster_march_duplicate_push_count = monster_march_hook
            and (tonumber(monster_march_hook.duplicate_push_count) or 0) or 0,
        monster_march_hook_restored = monster_march_hook_restored,
        monster_view_diagnostics = monster_view_diagnostics,
        monster_capture_count = monster_capture_count,
        monster_views_with_marches = monster_views_with_marches,
        monster_views_with_bosses = monster_views_with_bosses,
        monster_max_march_count = monster_max_march_count,
        player_power_enrichment = {
            source = "DataCenter.WorldPointDetailManager.GetDetailByPointId",
            request_route = "UI.UIWorldPoint.Controller.UIWorldPointCtrl.RequestWorldPointDetail",
            batch_size = CFG.POWER_DETAIL_BATCH_SIZE,
            wait_seconds = CFG.POWER_DETAIL_WAIT_SECONDS,
            max_retries = CFG.POWER_DETAIL_MAX_RETRIES,
            initial_target_count = tonumber(M.power_enrichment.initial_target_count) or 0,
            request_count = tonumber(M.power_enrichment.requests) or 0,
            request_failure_count = tonumber(M.power_enrichment.request_failures) or 0,
            cached_resolved_count = tonumber(M.power_enrichment.cached_resolved) or 0,
            resolved_count = tonumber(M.power_enrichment.resolved) or 0,
            unresolved_count = tonumber(M.power_enrichment.unresolved) or 0,
            skipped_target_count = tonumber(M.power_enrichment.skipped_target_count) or 0,
            target_limit = M.power_target_limit,
            capped = (tonumber(M.power_enrichment.skipped_target_count) or 0) > 0,
            all_resolved = (tonumber(M.power_enrichment.unresolved) or 0) == 0
                and (tonumber(M.power_enrichment.skipped_target_count) or 0) == 0,
            retry_count = tonumber(M.power_enrichment.retry) or 0,
            complete = M.power_enrichment.complete == true,
        },
        resource_detail_enrichment = {
            source = "DataCenter.WorldPointDetailManager.GetDetailByPointId",
            request_route = "UI.UIWorldPoint.Controller.UIWorldPointCtrl.RequestWorldPointDetail",
            parse_contract = "WorldPointDetailData.ParseData: remainRes|reserve -> remainRes; initRes|initReserve -> initRes",
            batch_size = CFG.POWER_DETAIL_BATCH_SIZE,
            wait_seconds = CFG.POWER_DETAIL_WAIT_SECONDS,
            max_retries = CFG.POWER_DETAIL_MAX_RETRIES,
            initial_target_count = tonumber(M.resource_enrichment.initial_target_count) or 0,
            request_count = tonumber(M.resource_enrichment.requests) or 0,
            request_failure_count = tonumber(M.resource_enrichment.request_failures) or 0,
            cached_resolved_count = tonumber(M.resource_enrichment.cached_resolved) or 0,
            resolved_count = tonumber(M.resource_enrichment.resolved) or 0,
            unresolved_count = tonumber(M.resource_enrichment.unresolved) or 0,
            skipped_target_count = tonumber(M.resource_enrichment.skipped_target_count) or 0,
            target_limit = M.resource_target_limit,
            capped = (tonumber(M.resource_enrichment.skipped_target_count) or 0) > 0,
            all_resolved = (tonumber(M.resource_enrichment.unresolved) or 0) == 0
                and (tonumber(M.resource_enrichment.skipped_target_count) or 0) == 0,
            retry_count = tonumber(M.resource_enrichment.retry) or 0,
            complete = M.resource_enrichment.complete == true,
        },
        retry_count = 0,
    }
    write_monster_diagnostics(now, "proven", nil)
    phase = "captured"
    completed = true
    write_json(PATHS.result, result)
    write_json(PATHS.status, status_payload(now))
end

local function fail_after_setup(now, point_manager, message, state)
    last_error = message
    cleanup(point_manager)
    phase = state or "error"
    completed = true
    write_json(PATHS.status, status_payload(now))
end

local function start_monster_phase(now, world, point_manager)
    if not direct_transport_complete() then
        return false, "full_scan completion invariants were not satisfied before monster refresh"
    end
    local views, view_error = build_monster_camera_views(scan_request)
    if views == nil then return false, view_error end
    local original_view = current_view(world)
    local original_zoom = camera_zoom(world)
    if original_view == nil or original_view.x == nil or original_view.y == nil then
        return false, "original camera tile could not be captured"
    end
    if original_zoom == nil then return false, "original camera zoom could not be captured" end
    local world_flag = world_response_flag(world)
    if world_flag == nil then return false, "world response flag is unavailable" end
    if monster_march_request_count ~= 0 or monster_march_response_count ~= 0
        or monster_march_foreign_send_count ~= 0 then
        return false, "monster march counters were not clean at phase start"
    end
    local march_hooked, march_hook_error = install_monster_march_hooks()
    if not march_hooked then return false, march_hook_error end

    monster_views = views
    monster_view_diagnostics = {}
    monster_view_index = 1
    monster_original_view = original_view
    monster_original_zoom = original_zoom
    restore_state.monster_world_flag_original = world_flag
    restore_state.monster_manager_flag_original = reflected_value(point_manager, "isRecvViewPoints") == true
    monster_terminal_state = nil
    monster_terminal_error = nil
    monster_camera_restored = false
    full_scan_active = false
    phase = "monster_camera_move"
    write_json(PATHS.status, status_payload(now))
    return true, nil
end

local function begin_monster_restore(now, world, terminal_state, terminal_error)
    monster_terminal_state = terminal_state
    monster_terminal_error = terminal_error
    if monster_original_view == nil then
        return false, "original camera view is unavailable"
    end
    local requested, restore_error = restore_monster_camera(
        world, monster_original_view, monster_original_zoom)
    if not requested then return false, restore_error or "camera restore request failed" end
    monster_restore_started_at = runtime_clock()
    phase = "monster_camera_restore_wait"
    write_json(PATHS.status, status_payload(now))
    return true, nil
end

local function fail_monster_with_restore(now, world, point_manager, message, state)
    last_error = message
    local started, restore_error = begin_monster_restore(now, world, state or "monster_scan_failed", message)
    if started then return end
    monster_camera_restored = false
    finish_monster_failure(now, world, point_manager,
        tostring(message) .. "; camera restore request failed: " .. tostring(restore_error),
        "camera_restore_failed")
end

local function send_batch_request(now, point_manager, index)
    if index < 1 or index > #batch_requests then return false, "batch index is outside plan" end
    if request_sent_count >= #batch_requests then return false, "bounded full-scan send cap reached" end
    if index ~= 1 then return false, "serial send helper is reserved for capability batch 1" end
    request = batch_requests[index]
    active_batch_index = index
    local reset, reset_error = reflected_set_value(
        point_manager, "isRecvViewPoints", boxed_bool(false))
    if not reset then
        return false, "response_flag_reset_failed:" .. tostring(reset_error)
    end
    restore_state.manager_flag_touched = true
    request_sent_count = request_sent_count + 1
    request_sent_at = now
    local invoked, invoke_error = send_aoi_with_bridge(point_manager, request)
    if not invoked then return false, invoke_error or "SendAoiRequest invocation failed" end
    phase = "waiting_response"
    return true, nil
end

local function launch_full_scan_batches(now, point_manager)
    if request_sent_count < 1 or completed_batch_count < 1 then
        return false, "full_scan scheduler requires one completed serial capability batch"
    end
    if #batch_requests ~= 65 then return false, "full_scan scheduler requires the recovered 65-batch plan" end
    if next_batch_index < 2 or next_batch_index > #batch_requests + 1 then
        return false, "full_scan next batch index is invalid"
    end
    full_scan_active = true
    local launched = 0
    while next_batch_index <= #batch_requests
        and full_scan_inflight_count() < MAX_CONCURRENCY
        and launched < MAX_CONCURRENCY do
        local index = next_batch_index
        local value = batch_requests[index]
        local entry = new_full_scan_entry(value)
        entry.sent_at = now
        full_scan_entries[index] = entry
        local started_event = next_event()
        request_sent_count = request_sent_count + 1
        request_sent_at = now
        local invoked, invoke_error = send_aoi_with_bridge(point_manager, value)
        if not invoked then
            return false, invoke_error or "full_scan SendAoiRequest invocation failed"
        end
        local completed_event = next_event()
        if #full_scan_send_events < CFG.MAX_DIAGNOSTIC_EVENTS then
            full_scan_send_events[#full_scan_send_events + 1] = {
                sequence = value.sequence,
                started_event = started_event,
                completed_event = completed_event,
            }
        end
        next_batch_index = index + 1
        launched = launched + 1
        full_scan_peak_inflight = math.max(full_scan_peak_inflight, full_scan_inflight_count())
    end
    if launched > 0 then full_scan_wave_count = full_scan_wave_count + 1 end
    phase = "waiting_full_scan"
    return true, nil
end

local function start_full_scan_scan(now, world, point_manager)
    if request_sent_count ~= 0 then return false, "full_scan scan already attempted" end
    local target, requests, build_error = build_requests(world, point_manager)
    if target == nil or requests == nil then return false, build_error end
    if #requests ~= 65 then
        return false, "bounded proof requires recovered 65-batch full-grid plan"
    end
    local full_batches, narrow_batches = 0, 0
    for _, value in ipairs(requests) do
        if value.requested_block_count == 160 then full_batches = full_batches + 1
        elseif value.requested_block_count == 80 then narrow_batches = narrow_batches + 1 end
    end
    if full_batches ~= 60 or narrow_batches ~= 5 then
        return false, "bounded proof requires recovered 60x160 + 5x80 batch distribution"
    end
    scan_request = target
    batch_requests = requests
    if not initialize_requested_coverage(scan_request) or requested_block_count ~= 10000 then
        return false, "full_scan scan did not contain exactly 10000 logical target blocks"
    end
    before_capture, build_error = capture_points(world, point_manager, scan_request)
    if before_capture == nil then return false, build_error end
    local hooked, hook_error = install_response_hook(world, point_manager)
    if not hooked then return false, hook_error end
    restore_state.manager_flag_original = reflected_value(point_manager, "isRecvViewPoints") == true
    local sent, send_error = send_batch_request(now, point_manager, 1)
    if not sent then return false, send_error end
    return true, nil
end

local function record_completed_batch(now)
    local covered = active_batch_covered_count()
    local response_count = 0
    for _, response in ipairs(matched_responses) do
        if tonumber(response.batch_sequence) == tonumber(request.sequence) then
            response_count = response_count + 1
        end
    end
    batch_results[#batch_results + 1] = {
        sequence = request.sequence,
        completed_at = now,
        requested_block_count = request.requested_block_count,
        covered_block_count = covered,
        matched_response_count = response_count,
        request = public_request(request),
    }
    completed_batch_count = completed_batch_count + 1
end

local function record_completed_full_scan_entries(now)
    for index = 2, #batch_requests do
        local entry = full_scan_entries[index]
        if entry ~= nil and entry.complete == true and entry.recorded ~= true then
            if entry.covered_count ~= entry.expected_count then
                return false, "full_scan completed batch coverage mismatch"
            end
            batch_results[#batch_results + 1] = {
                sequence = entry.request.sequence,
                completed_at = now,
                requested_block_count = entry.expected_count,
                covered_block_count = entry.covered_count,
                matched_response_count = entry.response_count,
                accepted_envelope_count = entry.accepted_envelope_count,
                request = public_request(entry.request),
            }
            entry.recorded = true
            completed_batch_count = completed_batch_count + 1
        end
    end
    return true, nil
end

local function timed_out_full_scan_sequence(now)
    for index = 2, #batch_requests do
        local entry = full_scan_entries[index]
        if entry ~= nil and entry.complete ~= true and entry.sent_at ~= nil
            and now - entry.sent_at >= CFG.RESPONSE_TIMEOUT_SECONDS then
            return entry.request.sequence
        end
    end
    return nil
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    write_json(PATHS.heartbeat, {
        version = M.VERSION,
        loaded = true,
        persistent = M.PERSISTENT,
        updated_at = now,
        registrationMethod = registration_method,
    })
    if M.PERSISTENT then M.ProcessScanCommand(now) end
    if completed then
        M.ProcessFocus(now)
        return true
    end
    local scene = scene_identity()
    if scene ~= "world" then
        if scene == "city" and not transition_requested then
            local scene_utils = rawget(_G, "SceneUtils")
            local change = safe_get(scene_utils, "ChangeToWorld")
            if type(change) ~= "function" then
                last_error = "SceneUtils.ChangeToWorld unavailable"
                phase = "waiting_scene"
            else
                local ok, transition_error = pcall(change)
                if not ok then ok, transition_error = pcall(change, scene_utils) end
                if ok then
                    transition_requested = true
                    transition_count = transition_count + 1
                    phase = "transition_requested"
                else
                    last_error = tostring(transition_error)
                end
            end
        end
        write_json(PATHS.status, status_payload(now))
        return true
    end

    local world, point_manager, manager_error = runtime_world()
    if point_manager == nil then
        last_error = manager_error
        phase = "waiting_world_manager"
        write_json(PATHS.status, status_payload(now))
        return true
    end

    if phase == "player_power_request" or phase == "player_power_wait" then
        if M.AdvancePowerEnrichment() then
            if M.BeginResourceEnrichment() then
                write_json(PATHS.status, status_payload(now))
            else
                finish_success(now, world, point_manager)
            end
        else
            write_json(PATHS.status, status_payload(now))
        end
        return true
    end

    if phase == "resource_detail_request" or phase == "resource_detail_wait" then
        if M.AdvanceResourceEnrichment() then
            finish_success(now, world, point_manager)
        else
            write_json(PATHS.status, status_payload(now))
        end
        return true
    end

    if phase == "monster_camera_move" then
        if monster_view_index > #monster_views then
            local started, restore_error = begin_monster_restore(now, world, "captured", nil)
            if not started then
                monster_camera_restored = false
                finish_monster_failure(now, world, point_manager,
                    "camera restore request failed: " .. tostring(restore_error), "camera_restore_failed")
            end
            return true
        end
        local view = monster_views[monster_view_index]
        monster_current_view = { x = view.x, y = view.y }
        local moved, move_error, move_route, world_position =
            move_official_monster_view(world, monster_current_view)
        if not moved then
            fail_monster_with_restore(now, world, point_manager,
                move_error or "monster camera move failed", "monster_camera_move_failed")
            return true
        end
        local world_x = tonumber(safe_get(world_position, "x"))
        local world_z = tonumber(safe_get(world_position, "z"))
        if world_x == nil or world_z == nil then
            fail_monster_with_restore(now, world, point_manager,
                "TileToWorld result did not expose current-build worldPos.x/worldPos.z",
                "monster_world_position_unavailable")
            return true
        end
        monster_current_world_position = world_position
        monster_current_diagnostic = {
            view_index = monster_view_index,
            tile_x = monster_current_view.x,
            tile_y = monster_current_view.y,
            world_position_x = world_x,
            world_position_z = world_z,
            camera_move_route = move_route,
            official_request_sent = false,
            official_response_observed = false,
            march_request_sent = false,
            march_response_observed = false,
            march_response_success = false,
        }
        monster_view_diagnostics[#monster_view_diagnostics + 1] = monster_current_diagnostic
        monster_camera_move_count = monster_camera_move_count + 1
        monster_view_started_at = runtime_clock()
        phase = "monster_camera_wait"
        write_json(PATHS.status, status_payload(now))
        return true
    end

    if phase == "monster_camera_wait" then
        local elapsed = runtime_clock() - (monster_view_started_at or runtime_clock())
        local distance = view_distance(current_view(world), monster_current_view)
        if elapsed < 0.02 or (distance > 3 and elapsed < 0.18) then
            write_json(PATHS.status, status_payload(now))
            return true
        end
        local reset, reset_error = reflected_set_value(
            point_manager, "isRecvViewPoints", boxed_bool(false))
        if not reset then
            fail_monster_with_restore(now, world, point_manager,
                "response_flag_reset_failed:" .. tostring(reset_error), "monster_official_request_failed")
            return true
        end
        restore_state.manager_flag_touched = true
        restore_state.monster_manager_flag_touched = true
        local world_reset, world_reset_error = set_world_response_flag(world, false)
        if not world_reset then
            fail_monster_with_restore(now, world, point_manager,
                "world_response_flag_reset_failed:" .. tostring(world_reset_error),
                "monster_official_request_failed")
            return true
        end
        restore_state.monster_world_flag_touched = true
        monster_view_response_received = false
        monster_view_response_route = nil
        monster_view_response_envelope = nil
        monster_march_response_received = false
        monster_march_response_error_code = nil
        monster_march_response_handler_error = nil
        monster_current_pushes = {}
        monster_march_response_started_at = runtime_clock()
        monster_view_response_started_at = runtime_clock()
        phase = "monster_waiting_response"
        local started = select(1, call(point_manager, "StartViewRequest"))
        local updated = select(1, call(point_manager, "UpdateViewRequest", true))
        if not started or not updated then
            fail_monster_with_restore(now, world, point_manager,
                not started and "start_view_request_failed" or "update_view_request_failed",
                "monster_official_request_failed")
            return true
        end
        monster_official_request_count = monster_official_request_count + 1
        if monster_current_diagnostic ~= nil then
            monster_current_diagnostic.official_request_sent = true
        end
        write_json(PATHS.status, status_payload(now))
        return true
    end

    if phase == "monster_waiting_response" then
        if monster_view_response_received then
            if monster_current_diagnostic ~= nil then
                monster_current_diagnostic.official_response_observed = true
                monster_current_diagnostic.official_response_route = monster_view_response_route
            end
            phase = "monster_waiting_march_push"
            write_json(PATHS.status, status_payload(now))
        elseif monster_view_response_started_at ~= nil
            and runtime_clock() - monster_view_response_started_at >= CFG.MONSTER_VIEW_RESPONSE_TIMEOUT_SECONDS then
            fail_monster_with_restore(now, world, point_manager,
                "official monster-view response timeout", "monster_view_timeout")
        else
            write_json(PATHS.status, status_payload(now))
        end
        return true
    end

    if phase == "monster_waiting_march_push" then
        if monster_march_response_received then
            if monster_march_response_handler_error ~= nil then
                fail_monster_with_restore(now, world, point_manager,
                    "march AOI push handler failed:" .. tostring(monster_march_response_handler_error),
                    "monster_march_response_handler_failed")
                return true
            end
            if monster_march_response_error_code ~= nil then
                fail_monster_with_restore(now, world, point_manager,
                    "march AOI push error:" .. tostring(monster_march_response_error_code),
                    "monster_march_response_error")
                return true
            end
            local monsters = monster_current_diagnostic
                and monster_current_diagnostic.raw_march_records or {}
            monster_capture_count = monster_capture_count + 1
            local observed = tonumber(monster_current_diagnostic
                and monster_current_diagnostic.march_count_after_response) or 0
            local selected = #monsters
            if monster_current_diagnostic ~= nil then
                monster_current_diagnostic.boss_count_after_response = selected
                monster_current_diagnostic.monster_or_boss_count_after_response = selected
                monster_current_diagnostic.resolved_march_count_after_response = observed
                monster_current_diagnostic.capture_completed = true
                monster_current_diagnostic.raw_march_records = nil
            end
            if observed > 0 then monster_views_with_marches = monster_views_with_marches + 1 end
            if selected > 0 then monster_views_with_bosses = monster_views_with_bosses + 1 end
            monster_max_march_count = math.max(monster_max_march_count, observed)
            if not append_scan_records(monsters) then
                fail_monster_with_restore(now, world, point_manager,
                    scan_capture_error or "monster record accumulation failed", "monster_capture_failed")
                return true
            end
            monster_view_index = monster_view_index + 1
            monster_current_view = nil
            monster_current_world_position = nil
            monster_current_diagnostic = nil
            phase = "monster_camera_move"
            write_json(PATHS.status, status_payload(now))
        elseif monster_march_response_started_at ~= nil
            and runtime_clock() - monster_march_response_started_at >= CFG.MONSTER_MARCH_RESPONSE_TIMEOUT_SECONDS then
            fail_monster_with_restore(now, world, point_manager,
                "push.world.march.world.get.new timeout after correlated AOI view response",
                "monster_march_response_timeout")
        else
            write_json(PATHS.status, status_payload(now))
        end
        return true
    end

    if phase == "monster_camera_restore_wait" then
        local observation = camera_restore_observation(world)
        if observation.restored then
            monster_camera_restored = true
            if monster_terminal_state == "captured" then
                if M.BeginPowerEnrichment() then
                    write_json(PATHS.status, status_payload(now))
                elseif M.BeginResourceEnrichment() then
                    write_json(PATHS.status, status_payload(now))
                else
                    finish_success(now, world, point_manager)
                end
            else
                finish_monster_failure(now, world, point_manager,
                    monster_terminal_error or "monster scan failed",
                    monster_terminal_state or "monster_scan_failed")
            end
        elseif monster_restore_started_at ~= nil
            and runtime_clock() - monster_restore_started_at >= CFG.MONSTER_CAMERA_RESTORE_TIMEOUT_SECONDS then
            monster_camera_restored = false
            finish_monster_failure(now, world, point_manager,
                tostring(monster_terminal_error or "monster scan completed") .. "; camera restore timeout",
                "camera_restore_failed")
        else
            write_json(PATHS.status, status_payload(now))
        end
        return true
    end

    if phase == "waiting_response" then
        if scan_capture_error ~= nil then
            fail_after_setup(now, point_manager,
                scan_capture_error, "matched_response_capture_failed")
        elseif request ~= nil and request.requested_block_count > 0
            and active_batch_covered_count() == request.requested_block_count then
            phase = "batch_response_matched"
            write_json(PATHS.status, status_payload(now))
        elseif request_sent_at ~= nil and now - request_sent_at >= CFG.RESPONSE_TIMEOUT_SECONDS then
            fail_after_setup(now, point_manager,
                "no correlated WorldGetBlock response within bounded timeout", "unknown_timeout")
        else
            write_json(PATHS.status, status_payload(now))
        end
        return true
    end
    if phase == "batch_response_matched" then
        if active_batch_covered_count() ~= request.requested_block_count then
            fail_after_setup(now, point_manager,
                "active batch coverage changed before completion", "coverage_incomplete")
            return true
        end
        record_completed_batch(now)
        if active_batch_index == 1 then
            local sent, send_error = launch_full_scan_batches(now, point_manager)
            if not sent then
                fail_after_setup(now, point_manager, send_error, "full_scan_launch_failed")
            else
                write_json(PATHS.status, status_payload(now))
            end
        else
            fail_after_setup(now, point_manager,
                "unexpected serial batch state", "coverage_incomplete")
        end
        return true
    end
    if phase == "waiting_full_scan" then
        if scan_capture_error ~= nil then
            fail_after_setup(now, point_manager,
                scan_capture_error, "matched_response_capture_failed")
        else
            local recorded, record_error = record_completed_full_scan_entries(now)
            if not recorded then
                fail_after_setup(now, point_manager, record_error, "coverage_incomplete")
            elseif request_sent_count == #batch_requests
                and completed_batch_count == #batch_requests then
                if covered_block_count == requested_block_count then
                    local direct_monster_count = 0
                    local direct_kind_counts = {}
                    local direct_point_type_counts = {}
                    for _, point in ipairs(scan_records) do
                        local direct_kind = tostring(point.kind or "world_point")
                        direct_kind_counts[direct_kind] = (tonumber(direct_kind_counts[direct_kind]) or 0) + 1
                        local direct_point_type = tonumber(point.pointType)
                        if direct_point_type ~= nil then
                            local point_type_key = tostring(math.floor(direct_point_type))
                            direct_point_type_counts[point_type_key] =
                                (tonumber(direct_point_type_counts[point_type_key]) or 0) + 1
                        end
                        if point.kind == "monster"
                            and point.source == "WorldPointManager._pointInfos" then
                            direct_monster_count = direct_monster_count + 1
                        end
                    end
                    write_json(PATHS.direct_diagnostics, {
                        probeVersion = M.VERSION,
                        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
                        requested_block_count = requested_block_count,
                        covered_block_count = covered_block_count,
                        request_sent_count = request_sent_count,
                        completed_batch_count = completed_batch_count,
                        accumulated_record_count = #scan_records,
                        direct_monster_record_count = direct_monster_count,
                        kind_counts = direct_kind_counts,
                        point_type_counts = direct_point_type_counts,
                        point_records = scan_records,
                    })
                    local started, monster_error = start_monster_phase(now, world, point_manager)
                    if not started then
                        finish_monster_failure(now, world, point_manager,
                            monster_error or "passive AOI march phase could not start",
                            "monster_phase_start_failed")
                    end
                else
                    fail_after_setup(now, point_manager,
                        "all full_scan batches completed without full logical coverage",
                        "coverage_incomplete")
                end
            else
                local timed_out_sequence = timed_out_full_scan_sequence(now)
                if timed_out_sequence ~= nil then
                    fail_after_setup(now, point_manager,
                        "full_scan WorldGetBlock response timeout for batch " .. tostring(timed_out_sequence),
                        "unknown_timeout")
                else
                    local sent, send_error = launch_full_scan_batches(now, point_manager)
                    if not sent then
                        fail_after_setup(now, point_manager, send_error, "full_scan_launch_failed")
                    else
                        write_json(PATHS.status, status_payload(now))
                    end
                end
            end
        end
        return true
    end

    local collection = reflected_value(point_manager, "_pointInfos")
    local count = collection and tonumber(
        safe_get(collection, "Count") or safe_get(collection, "Length")) or nil
    if count == nil then
        last_error = "WorldPointManager._pointInfos count unavailable"
        phase = "stabilizing"
        write_json(PATHS.status, status_payload(now))
        return true
    end
    if stable_point_count ~= count then
        stable_point_count = count
        stable_since = now
        phase = "stabilizing"
        write_json(PATHS.status, status_payload(now))
        return true
    end
    if now - stable_since < CFG.STABLE_SECONDS then
        phase = "stabilizing"
        write_json(PATHS.status, status_payload(now))
        return true
    end

    local sent, send_error = start_full_scan_scan(now, world, point_manager)
    if not sent then
        fail_after_setup(now, point_manager, send_error, "request_failed")
    else
        write_json(PATHS.status, status_payload(now))
    end
    return true
end

function M.Register()
    if registration_method ~= nil then return true end
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
            local register_repeat = safe_get(timer, "RegisterTimerRepeat")
            if type(register_repeat) == "function" then
                local ok_timer, handle = pcall(
                    register_repeat, timer, 0.25, 0.25, update_callback)
                if not ok_timer then
                    ok_timer, handle = pcall(register_repeat, 0.25, 0.25, update_callback)
                end
                if ok_timer then
                    restore_state.timer_handle = handle
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
