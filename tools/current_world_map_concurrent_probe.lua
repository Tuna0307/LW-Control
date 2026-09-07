-- Current-build bounded concurrent World AOI proof.
--
-- This probe mirrors the recovered LWC2MapScanner v108 batch builder at the
-- smallest odd square that yields three native batches: 19x19 = 361 logical
-- blocks. The recovered planner deterministically produces 152 + 152 + 57.
-- Batch 1 is sent and confirmed first as the recovered capability-probe ordering
-- requires. Batches 2 and 3 are then issued as one bounded two-request
-- concurrent wave. It does not move the camera, retry, or use the camera-driven
-- fallback path.

local M = { VERSION = "lwcontrol-world-concurrent-probe-1" }

local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
local heartbeat_path = root .. [[\world-map-concurrent-heartbeat.json]]
local status_path = root .. [[\world-map-concurrent-status.json]]
local result_path = root .. [[\world-map-concurrent-result.json]]

local STABLE_SECONDS = 2
local RESPONSE_TIMEOUT_SECONDS = 30
local MAX_POINTS = 50000
local MAX_RESPONSE_ENVELOPES = 24

local phase = "waiting_scene"
local completed = false
local transition_requested = false
local transition_count = 0
local stable_point_count = nil
local stable_since = 0
local request_sent_count = 0
local request_sent_at = nil
local target_command = nil
local response_hook = nil
local manager_flag_original = nil
local manager_flag_touched = false
local request = nil
local scan_request = nil
local batch_requests = {}
local active_batch_index = 0
local completed_batch_count = 0
local batch_results = {}
local concurrent_entries = {}
local concurrent_wave_launching = false
local concurrent_wave_launched = false
local concurrent_wave_sent_at = nil
local concurrent_wave_launch_completed_event = nil
local concurrent_response_before_wave_complete = false
local concurrent_first_response_event = nil
local concurrent_send_events = {}
local concurrent_response_events = {}
local event_ordinal = 0
local concurrent_peak_inflight = 0
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
local timer_handle = nil

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
    if block_count == nil or block_size == nil or block_count < 19 or block_size < 1
        or lb == nil or rt == nil then
        return nil, "current AOI geometry unavailable"
    end
    block_count, block_size = math.floor(block_count), math.floor(block_size)
    local middle_y = math.floor((lb.y + rt.y) / 2)
    local start_x = rt.x + 1
    if start_x < 0 or start_x + 18 >= block_count then
        return nil, "no 19x19 right-side target fits current AOI"
    end
    local start_y = math.max(0, math.min(block_count - 19, middle_y - 9))
    local blocks = {}
    for y = start_y, start_y + 18 do
        for x = start_x, start_x + 18 do
            local outside = x < lb.x or x > rt.x or y < lb.y or y > rt.y
            if not outside then return nil, "concurrent target overlaps current visible AOI" end
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
            left = start_x,
            right = start_x + 18,
            bottom = start_y,
            top = start_y + 18,
        },
        requested_coverage = {
            left = start_x,
            right = start_x + 18,
            bottom = start_y,
            top = start_y + 18,
        },
        side = "right",
        visible_left_bottom = lb,
        visible_right_top = rt,
        block_count = block_count,
        block_size = block_size,
        left_bottom = { x = start_x * block_size, y = start_y * block_size },
        right_top = {
            x = (start_x + 19) * block_size,
            y = (start_y + 19) * block_size,
        },
    }, nil
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
    if #target.blocks ~= 361 or #batches ~= 3 or batch_size ~= 152 then
        return nil, nil, "recovered 19x19 batch plan did not produce expected three-batch split"
    end
    local output = {}
    for index, batch in ipairs(batches) do
        local built, build_error = build_request_for_batch(world, point_manager, target, batch, index)
        if built == nil then return nil, nil, build_error end
        output[#output + 1] = built
    end
    if output[1].requested_block_count ~= 152
        or output[2].requested_block_count ~= 152
        or output[3].requested_block_count ~= 57 then
        return nil, nil, "recovered 19x19 batch plan did not produce expected 152+152+57 split"
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

local function capture_points(world, point_manager, bounds)
    local collection = reflected_value(point_manager, "_pointInfos")
    if collection == nil then return nil, "WorldPointManager._pointInfos unavailable" end
    local expected = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if expected == nil or expected < 0 or expected > MAX_POINTS then
        return nil, "loaded point count is outside bounded limit"
    end
    local values = safe_get(collection, "Values")
    local enumerable = values ~= nil and values or collection
    local identities, in_bounds = {}, {}
    local scanned = each(enumerable, MAX_POINTS + 1, function(raw)
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
            in_bounds[#in_bounds + 1] = {
                id = id, serverId = server_id, worldId = world_id, x = tile.x, y = tile.y,
            }
        end
        return true
    end)
    if scanned ~= expected then return nil, "loaded point enumeration did not match _pointInfos.Count" end
    return { count = expected, identities = identities, in_bounds = in_bounds }, nil
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

local function response_envelopes(payload)
    local output, seen = {}, {}
    local function visit(value, source, depth)
        if value == nil or depth > 4 or #output >= MAX_RESPONSE_ENVELOPES then return end
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
            if array == nil or #output >= MAX_RESPONSE_ENVELOPES then return end
            local observed_count = response_array_count(array)
            local count = observed_count > 0
                and math.min(observed_count, MAX_RESPONSE_ENVELOPES)
                or MAX_RESPONSE_ENVELOPES
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
        each(keys, MAX_RESPONSE_ENVELOPES, function(key)
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
            return #output < MAX_RESPONSE_ENVELOPES
        end)
    end
    visit(payload, "$", 0)
    return output
end

local function normalize_response_envelope(world, envelope)
    if envelope == nil or request == nil then return nil end
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
    local block_size = math.max(1, tonumber(request.block_size) or 1)
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
    if not manager_flag_touched then return true end
    local restored = select(1, reflected_set_value(
        point_manager, "isRecvViewPoints", boxed_bool(manager_flag_original == true)))
    manager_flag_touched = false
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

local function new_concurrent_entry(value)
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
        response_count = 0,
        accepted_envelope_count = 0,
        rejected_envelope_count = 0,
    }
end

local function mark_concurrent_coverage(entry, normalized)
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

local function match_concurrent_entry(normalized)
    local matched, match_count = nil, 0
    for _, entry in pairs(concurrent_entries) do
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
    return nil, match_count == 0 and "concurrent_response_unmatched"
        or "concurrent_response_ambiguous"
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

local function install_response_hook(world)
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
        if tostring(command_value or "") ~= target_command then return end
        target_response_count = target_response_count + 1
        local envelopes = response_envelopes(payload)
        local accepted_any = false
        for _, envelope in ipairs(envelopes) do
            local normalized = normalize_response_envelope(world, envelope)
            if #observed_response_envelopes < 12 then
                observed_response_envelopes[#observed_response_envelopes + 1] = normalized or envelope
            end
            if concurrent_wave_launching or phase == "waiting_concurrent" then
                local entry, match_error = match_concurrent_entry(normalized)
                if entry ~= nil then
                    accepted_any = true
                    local response_event = next_event()
                    if concurrent_wave_launched ~= true then
                        concurrent_response_before_wave_complete = true
                    end
                    if concurrent_first_response_event == nil then
                        concurrent_first_response_event = response_event
                    end
                    concurrent_response_events[#concurrent_response_events + 1] = {
                        sequence = entry.request.sequence,
                        event = response_event,
                    }
                    entry.response_count = entry.response_count + 1
                    entry.accepted_envelope_count = entry.accepted_envelope_count + 1
                    local newly_covered = mark_concurrent_coverage(entry, normalized)
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
                    if match_error == "concurrent_response_ambiguous" then
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
                        return
                    end
                end
            end
        end
        if not accepted_any then rejected_response_count = rejected_response_count + 1 end
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

local function status_payload(now)
    local scene, current, city, world = scene_identity()
    return {
        probeVersion = M.VERSION,
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
        concurrent_wave_launching = concurrent_wave_launching,
        concurrent_wave_launched = concurrent_wave_launched,
        concurrent_wave_sent_at = concurrent_wave_sent_at,
        concurrent_wave_launch_completed_event = concurrent_wave_launch_completed_event,
        concurrent_response_before_wave_complete = concurrent_response_before_wave_complete,
        concurrent_first_response_event = concurrent_first_response_event,
        concurrent_send_events = concurrent_send_events,
        concurrent_response_events = concurrent_response_events,
        concurrent_peak_inflight = concurrent_peak_inflight,
        target_command = target_command,
        target_response_count = target_response_count,
        rejected_response_count = rejected_response_count,
        observed_response_envelopes = observed_response_envelopes,
        scan = public_scan(scan_request),
        requests = public_requests(batch_requests),
        request = public_request(request),
        batch_results = batch_results,
        matched_responses = matched_responses,
        requested_block_count = requested_block_count,
        covered_block_count = covered_block_count,
        requested_blocks = public_covered_blocks(),
        response_hook_count = response_hook and #(response_hook.hooks or {}) or 0,
        registration_method = registration_method,
    }
end

local function cleanup(point_manager)
    local hook_restored = restore_response_hook()
    local flag_restored = restore_manager_flag(point_manager)
    return hook_restored, flag_restored
end

local function finish_success(now, world, point_manager)
    local send_events_complete = #concurrent_send_events == 2
    local sends_before_response = send_events_complete and concurrent_first_response_event ~= nil
    if sends_before_response then
        for _, item in ipairs(concurrent_send_events) do
            if tonumber(item.completed_event) == nil
                or item.completed_event >= concurrent_first_response_event then
                sends_before_response = false
                break
            end
        end
    end
    if requested_block_count ~= 361 or covered_block_count ~= requested_block_count
        or request_sent_count ~= 3 or completed_batch_count ~= 3
        or concurrent_wave_launched ~= true or concurrent_peak_inflight ~= 2
        or concurrent_response_before_wave_complete == true
        or sends_before_response ~= true then
        last_error = "concurrent completion invariants were not satisfied"
        cleanup(point_manager)
        phase = "coverage_incomplete"
        completed = true
        write_json(status_path, status_payload(now))
        return
    end
    local after_capture, capture_error = capture_points(world, point_manager, scan_request)
    local hook_restored, flag_restored = cleanup(point_manager)
    if after_capture == nil then
        last_error = capture_error
        phase = "matched_response_capture_failed"
        completed = true
        write_json(status_path, status_payload(now))
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
        schemaVersion = 1,
        probeVersion = M.VERSION,
        state = "proven",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
        scan = public_scan(scan_request),
        requests = public_requests(batch_requests),
        request = public_request(request),
        batch_results = batch_results,
        responses = matched_responses,
        requested_block_count = requested_block_count,
        covered_block_count = covered_block_count,
        requested_blocks = public_covered_blocks(),
        request_sent_count = request_sent_count,
        completed_batch_count = completed_batch_count,
        concurrent_wave_launched = concurrent_wave_launched,
        concurrent_wave_sent_at = concurrent_wave_sent_at,
        concurrent_wave_launch_completed_event = concurrent_wave_launch_completed_event,
        concurrent_response_before_wave_complete = concurrent_response_before_wave_complete,
        concurrent_first_response_event = concurrent_first_response_event,
        concurrent_send_events = concurrent_send_events,
        concurrent_response_events = concurrent_response_events,
        concurrent_peak_inflight = concurrent_peak_inflight,
        target_response_count = target_response_count,
        rejected_response_count = rejected_response_count,
        before_point_count = before_capture and before_capture.count or nil,
        after_point_count = after_capture.count,
        added_points = added,
        points_in_requested_bounds_after = after_capture.in_bounds,
        response_hook_restored = hook_restored,
        manager_flag_restored = flag_restored,
        camera_move_count = 0,
        retry_count = 0,
    }
    phase = "captured"
    completed = true
    write_json(result_path, result)
    write_json(status_path, status_payload(now))
end

local function fail_after_setup(now, point_manager, message, state)
    last_error = message
    cleanup(point_manager)
    phase = state or "error"
    completed = true
    write_json(status_path, status_payload(now))
end

local function send_batch_request(now, point_manager, index)
    if index < 1 or index > #batch_requests then return false, "batch index is outside plan" end
    if request_sent_count >= 3 then return false, "bounded three-request send cap reached" end
    if index ~= 1 then return false, "serial send helper is reserved for capability batch 1" end
    request = batch_requests[index]
    active_batch_index = index
    local reset, reset_error = reflected_set_value(
        point_manager, "isRecvViewPoints", boxed_bool(false))
    if not reset then
        return false, "response_flag_reset_failed:" .. tostring(reset_error)
    end
    manager_flag_touched = true
    request_sent_count = request_sent_count + 1
    request_sent_at = now
    local invoked, invoke_error = send_aoi_with_bridge(point_manager, request)
    if not invoked then return false, invoke_error or "SendAoiRequest invocation failed" end
    phase = "waiting_response"
    return true, nil
end

local function launch_concurrent_wave(now, point_manager)
    if request_sent_count ~= 1 or completed_batch_count ~= 1 then
        return false, "concurrent wave requires one completed serial capability batch"
    end
    if #batch_requests ~= 3 then return false, "concurrent wave requires exactly two remaining batches" end
    if concurrent_wave_launched or concurrent_wave_launching then
        return false, "concurrent wave already launched"
    end
    concurrent_entries = {
        [2] = new_concurrent_entry(batch_requests[2]),
        [3] = new_concurrent_entry(batch_requests[3]),
    }
    concurrent_wave_launching = true
    for index = 2, 3 do
        if request_sent_count >= 3 then
            concurrent_wave_launching = false
            return false, "bounded three-request send cap reached"
        end
        local value = batch_requests[index]
        local started_event = next_event()
        request_sent_count = request_sent_count + 1
        request_sent_at = now
        local invoked, invoke_error = send_aoi_with_bridge(point_manager, value)
        if not invoked then
            concurrent_wave_launching = false
            return false, invoke_error or "concurrent SendAoiRequest invocation failed"
        end
        local completed_event = next_event()
        concurrent_send_events[#concurrent_send_events + 1] = {
            sequence = value.sequence,
            started_event = started_event,
            completed_event = completed_event,
        }
    end
    concurrent_peak_inflight = 2
    concurrent_wave_launching = false
    concurrent_wave_launched = true
    concurrent_wave_sent_at = now
    concurrent_wave_launch_completed_event = next_event()
    phase = "waiting_concurrent"
    return true, nil
end

local function start_concurrent_scan(now, world, point_manager)
    if request_sent_count ~= 0 then return false, "concurrent scan already attempted" end
    local target, requests, build_error = build_requests(world, point_manager)
    if target == nil or requests == nil then return false, build_error end
    if #requests ~= 3 or requests[1].requested_block_count ~= 152
        or requests[2].requested_block_count ~= 152
        or requests[3].requested_block_count ~= 57 then
        return false, "bounded proof requires recovered 152+152+57 three-batch plan"
    end
    scan_request = target
    batch_requests = requests
    if not initialize_requested_coverage(scan_request) or requested_block_count ~= 361 then
        return false, "concurrent scan did not contain exactly 361 logical target blocks"
    end
    before_capture, build_error = capture_points(world, point_manager, scan_request)
    if before_capture == nil then return false, build_error end
    local hooked, hook_error = install_response_hook(world)
    if not hooked then return false, hook_error end
    manager_flag_original = reflected_value(point_manager, "isRecvViewPoints") == true
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

local function record_concurrent_batches(now)
    for index = 2, 3 do
        local entry = concurrent_entries[index]
        if entry == nil or entry.complete ~= true
            or entry.covered_count ~= entry.expected_count then
            return false, "concurrent batch coverage incomplete"
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
        completed_batch_count = completed_batch_count + 1
    end
    return true, nil
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    write_json(heartbeat_path, { version = M.VERSION, loaded = true, updated_at = now })
    if completed then return true end
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
        write_json(status_path, status_payload(now))
        return true
    end

    local world, point_manager, manager_error = runtime_world()
    if point_manager == nil then
        last_error = manager_error
        phase = "waiting_world_manager"
        write_json(status_path, status_payload(now))
        return true
    end

    if phase == "waiting_response" then
        if request ~= nil and request.requested_block_count > 0
            and active_batch_covered_count() == request.requested_block_count then
            phase = "batch_response_matched"
            write_json(status_path, status_payload(now))
        elseif request_sent_at ~= nil and now - request_sent_at >= RESPONSE_TIMEOUT_SECONDS then
            fail_after_setup(now, point_manager,
                "no correlated WorldGetBlock response within bounded timeout", "unknown_timeout")
        else
            write_json(status_path, status_payload(now))
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
            local sent, send_error = launch_concurrent_wave(now, point_manager)
            if not sent then
                fail_after_setup(now, point_manager, send_error, "concurrent_launch_failed")
            else
                write_json(status_path, status_payload(now))
            end
        else
            fail_after_setup(now, point_manager,
                "unexpected serial batch state", "coverage_incomplete")
        end
        return true
    end
    if phase == "waiting_concurrent" then
        local second = concurrent_entries[2]
        local third = concurrent_entries[3]
        if second and third and second.complete == true and third.complete == true then
            local recorded, record_error = record_concurrent_batches(now)
            if not recorded then
                fail_after_setup(now, point_manager, record_error, "coverage_incomplete")
            elseif covered_block_count == requested_block_count then
                finish_success(now, world, point_manager)
            else
                fail_after_setup(now, point_manager,
                    "all concurrent batches completed without full logical coverage",
                    "coverage_incomplete")
            end
        elseif concurrent_wave_sent_at ~= nil
            and now - concurrent_wave_sent_at >= RESPONSE_TIMEOUT_SECONDS then
            fail_after_setup(now, point_manager,
                "concurrent WorldGetBlock responses did not complete within bounded timeout",
                "unknown_timeout")
        else
            write_json(status_path, status_payload(now))
        end
        return true
    end

    local collection = reflected_value(point_manager, "_pointInfos")
    local count = collection and tonumber(
        safe_get(collection, "Count") or safe_get(collection, "Length")) or nil
    if count == nil then
        last_error = "WorldPointManager._pointInfos count unavailable"
        phase = "stabilizing"
        write_json(status_path, status_payload(now))
        return true
    end
    if stable_point_count ~= count then
        stable_point_count = count
        stable_since = now
        phase = "stabilizing"
        write_json(status_path, status_payload(now))
        return true
    end
    if now - stable_since < STABLE_SECONDS then
        phase = "stabilizing"
        write_json(status_path, status_payload(now))
        return true
    end

    local sent, send_error = start_concurrent_scan(now, world, point_manager)
    if not sent then
        fail_after_setup(now, point_manager, send_error, "request_failed")
    else
        write_json(status_path, status_payload(now))
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
