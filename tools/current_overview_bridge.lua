-- LWBridge Overview-only in-game readiness bridge for the verified current client.
--
-- The loader/install mechanics are reused from the already live-proven v14
-- LuaEntry path.  The control files, challenge correlation, host lease and UI
-- placement below are independent rebuild IMPLEMENTATION POLICY, not claims
-- about the original LWBridge named-pipe protocol.

local M = { VERSION = "lwbridge-overview-bridge-1" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWBridgeRebuild\overview-bridge]]
local control_path = root .. [[\control.txt]]
local lease_path = root .. [[\lease.txt]]
local ready_path = root .. [[\ready.json]]
local heartbeat_path = root .. [[\heartbeat.json]]
local navigation_path = root .. [[\map-navigation.txt]]
local navigation_result_path = root .. [[\map-navigation-result.json]]
local march_follow_path = root .. [[\march-follow.txt]]
local march_follow_result_path = root .. [[\march-follow-result.json]]
local truck_quick_rob_path = root .. [[\truck-quick-rob.txt]]
local truck_quick_rob_result_path = root .. [[\truck-quick-rob-result.json]]
local server_jump_path = root .. [[\server-jump.txt]]
local server_jump_result_path = root .. [[\server-jump-result.json]]
local aoi_diagnostic_path = root .. [[\aoi-diagnostic.txt]]
local aoi_diagnostic_result_path = root .. [[\aoi-diagnostic-result.json]]
local world_ready_path = root .. [[\world-ready.txt]]
local world_ready_result_path = root .. [[\world-ready-result.json]]
local MESSAGE = "LWbridge is running"
local OBJECT_NAME = "LWBridgeOverviewReady"
local LEASE_MAX_AGE_SECONDS = 5

local active = nil
local root_object = nil
local message_text = nil
local registration_method = nil
local timer_handle = nil
local update_callback = nil
local last_heartbeat_clock = -1000
local last_ready_session = nil
local pending_recovery_signal = nil
local pending_recovery_signal_until_clock = nil
local recovery_action_hooks = {}
local pending_navigation = nil
local pending_world_ready = nil
local pending_server_jump = nil
local pending_march_follow = nil
local pending_truck_quick_rob = nil
local NAVIGATION_TIMEOUT_SECONDS = 5
local WORLD_READY_TIMEOUT_SECONDS = 10
local SERVER_JUMP_TIMEOUT_SECONDS = 15
-- IMPLEMENTATION POLICY: setup is safe to abandon because train.attack has not
-- been sent yet.  A response timeout after train.attack is intentionally
-- ambiguous and must never be retried automatically.
local TRUCK_QUICK_ROB_SETUP_TIMEOUT_SECONDS = 15
local TRUCK_QUICK_ROB_RESPONSE_TIMEOUT_SECONDS = 30

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

local function runtime_clock()
    local cs = rawget(_G, "CS")
    local time = cs and cs.UnityEngine and cs.UnityEngine.Time
    return tonumber(time and safe_get(time, "realtimeSinceStartup")) or tonumber(os.clock()) or 0
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

local function read_kv(path)
    local file = io.open(path, "rb")
    if file == nil then return nil end
    local text = file:read("*a") or ""; file:close()
    if #text > 4096 then return nil end
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

local function read_control()
    local values = read_kv(control_path)
    if values == nil or values.schema ~= "1" or values.bridgeVersion ~= M.VERSION then return nil end
    local game_pid = tonumber(values.gamePid)
    if not valid_token(values.profileId) or not valid_token(values.sessionId) or
       not valid_token(values.challenge) or game_pid == nil or game_pid <= 0 or
       game_pid ~= math.floor(game_pid) then return nil end
    return {
        profileId = values.profileId,
        sessionId = values.sessionId,
        challenge = values.challenge,
        gamePid = game_pid,
    }
end

local function lease_is_fresh(control, now)
    local values = read_kv(lease_path)
    if values == nil or values.schema ~= "1" or values.bridgeVersion ~= M.VERSION then return false end
    if values.sessionId ~= control.sessionId or values.challenge ~= control.challenge then return false end
    local updated = tonumber(values.updatedAt)
    if updated == nil or updated > now + 5 then return false end
    return now - updated <= LEASE_MAX_AGE_SECONDS
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

local function reflected_type_name(component)
    if component == nil then return nil end
    local ok_type, reflected_type = pcall(function() return component:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local full_name = safe_get(reflected_type, "FullName") or safe_get(reflected_type, "Name")
    local text = full_name ~= nil and tostring(full_name) or ""
    return text ~= "" and text or nil
end

local function read_navigation(control)
    local values = read_kv(navigation_path)
    if values == nil then return nil end
    pcall(os.remove, navigation_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
    local game_pid = tonumber(values.gamePid)
    local server_id = tonumber(values.serverId)
    local world_id = tonumber(values.worldId)
    local target_x = tonumber(values.targetX)
    local target_y = tonumber(values.targetY)
    local request = {
        requestId = values.requestId,
        serverId = server_id,
        worldId = world_id,
        targetX = target_x,
        targetY = target_y,
    }
    if values.profileId ~= control.profileId or values.sessionId ~= control.sessionId or
       values.challenge ~= control.challenge or game_pid ~= control.gamePid then
        request.error = "navigation_identity_mismatch"
        return request
    end
    if server_id == nil or world_id == nil or server_id ~= math.floor(server_id) or world_id ~= math.floor(world_id) or
       server_id <= 0 or world_id < 0 then
        request.error = "navigation_server_world_invalid"
    elseif target_x == nil or target_y == nil or target_x ~= math.floor(target_x) or target_y ~= math.floor(target_y) or
       target_x < 0 or target_y < 0 or target_x > 2147483647 or target_y > 2147483647 then
        request.error = "navigation_target_invalid"
    end
    return request
end

local function read_march_follow(control)
    local values = read_kv(march_follow_path)
    if values == nil then return nil end
    pcall(os.remove, march_follow_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
    local game_pid = tonumber(values.gamePid)
    local server_id = tonumber(values.serverId)
    local march_uuid = tonumber(values.marchUuid)
    local request = {
        requestId = values.requestId,
        serverId = server_id,
        marchUuid = march_uuid,
    }
    if values.profileId ~= control.profileId or values.sessionId ~= control.sessionId or
       values.challenge ~= control.challenge or game_pid ~= control.gamePid then
        request.error = "march_follow_identity_mismatch"
        return request
    end
    if server_id == nil or server_id ~= math.floor(server_id) or server_id < 1 or server_id > 99999 or
       march_uuid == nil or march_uuid ~= math.floor(march_uuid) or march_uuid <= 0 then
        request.error = "march_follow_target_invalid"
    end
    return request
end

local function read_world_ready(control)
    local values = read_kv(world_ready_path)
    if values == nil then return nil end
    pcall(os.remove, world_ready_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
    local game_pid = tonumber(values.gamePid)
    local request = { requestId = values.requestId }
    if values.profileId ~= control.profileId or values.sessionId ~= control.sessionId or
       values.challenge ~= control.challenge or game_pid ~= control.gamePid then
        request.error = "world_ready_identity_mismatch"
    end
    return request
end

local function read_aoi_diagnostic(control)
    local values = read_kv(aoi_diagnostic_path)
    if values == nil then return nil end
    pcall(os.remove, aoi_diagnostic_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
    local game_pid = tonumber(values.gamePid)
    local cell_x = tonumber(values.cellX)
    local cell_y = tonumber(values.cellY)
    local tile_count = tonumber(values.tileCount)
    local request = { requestId = values.requestId, cellX = cell_x, cellY = cell_y, tileCount = tile_count }
    if values.profileId ~= control.profileId or values.sessionId ~= control.sessionId or
       values.challenge ~= control.challenge or game_pid ~= control.gamePid then
        request.error = "aoi_diagnostic_identity_mismatch"
        return request
    end
    if cell_x == nil or cell_y == nil or tile_count == nil or
       cell_x ~= math.floor(cell_x) or cell_y ~= math.floor(cell_y) or tile_count ~= math.floor(tile_count) or
       cell_x < 0 or cell_y < 0 or tile_count <= 0 then
        request.error = "aoi_diagnostic_arguments_invalid"
    end
    return request
end

local function navigation_world()
    local cs = rawget(_G, "CS")
    local manager = cs and safe_get(cs, "SceneManager")
    local world = manager and safe_get(manager, "World")
    if world == nil then return nil, "world_unavailable" end
    local ok_type, reflected_type = pcall(function() return world:GetType() end)
    local name = ok_type and tostring(safe_get(reflected_type, "Name") or "") or ""
    if name ~= "WorldScene" then return nil, "world_scene_unavailable" end
    return world, nil
end

local function resolve_scene_utils()
    local scene_utils = rawget(_G, "SceneUtils")
    if scene_utils ~= nil then return scene_utils end
    local ok_require, value = pcall(require, "Util.SceneUtils")
    if ok_require and value ~= nil then return value end
    return rawget(_G, "SceneUtils")
end

local function navigation_current_tile(world)
    local tile = safe_get(world, "CurTilePosClamped")
    if tile == nil then return nil, nil end
    local x = tonumber(safe_get(tile, "x") or safe_get(tile, "X"))
    local y = tonumber(safe_get(tile, "y") or safe_get(tile, "Y"))
    if x == nil or y == nil then return nil, nil end
    return math.floor(x), math.floor(y)
end

local function navigation_geometry()
    local details = {}
    local world = select(1, navigation_world())
    details.worldAvailable = world ~= nil
    local point_manager = world and (safe_get(world, "PointManager") or reflected_value(world, "PointManager")) or nil
    if point_manager == nil and world ~= nil then
        local ok_point_manager, value = call(world, "get_PointManager")
        details.pointManagerGetterOk = ok_point_manager == true
        if ok_point_manager then point_manager = value end
    end
    details.pointManagerAvailable = point_manager ~= nil
    details.pointManagerType = reflected_type_name(point_manager)
    if point_manager == nil then return nil, nil, nil, details end

    local lod_value = safe_get(point_manager, "LOD")
    if lod_value == nil then lod_value = reflected_value(point_manager, "LOD") end
    local lod = tonumber(lod_value)
    details.lodAvailable = lod ~= nil

    local server_lod = nil
    if lod ~= nil then
        local ok_server_lod, value = call(point_manager, "GetServerLod", math.floor(lod))
        details.serverLodCallOk = ok_server_lod == true
        if ok_server_lod then server_lod = tonumber(value) end
    end
    details.serverLodAvailable = server_lod ~= nil

    local raw = safe_get(point_manager, "lwAoiBlockSizeArray") or reflected_value(point_manager, "lwAoiBlockSizeArray")
    if raw == nil then
        raw = reflected_static_value(point_manager, "lwAoiBlockSizeArray") or
            reflected_static_value(point_manager, "_lwAoiBlockSizeArray")
        details.aoiGetterOk = raw ~= nil
    end
    if raw == nil then
        local cs = rawget(_G, "CS")
        local point_manager_type = cs and safe_get(cs, "WorldPointManager") or nil
        raw = point_manager_type and safe_get(point_manager_type, "lwAoiBlockSizeArray") or nil
        if raw ~= nil then details.aoiGetterOk = true end
    end
    details.aoiArrayAvailable = raw ~= nil

    local values = nil
    if raw ~= nil then
        local length = tonumber(safe_get(raw, "Length") or safe_get(raw, "Count"))
        details.aoiLength = length
        if length ~= nil and length >= 0 and length <= 32 then
            values = {}
            for index = 0, math.floor(length) - 1 do
                local item = tonumber(safe_get(raw, index))
                if item == nil then item = tonumber(safe_get(raw, index + 1)) end
                if item == nil then values = nil; break end
                values[#values + 1] = item
            end
        end
    end
    details.aoiValuesAvailable = values ~= nil
    return lod, server_lod, values, details
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

local function write_aoi_diagnostic_result(request, state, error_text, details)
    details = details or {}
    write_json(aoi_diagnostic_result_path, {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        requestId = request.requestId,
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
        method = "WorldPointManager.AoiBlockToIndex+GetAoiIndexCenter(read-only)",
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function pump_aoi_diagnostic(control)
    local request = read_aoi_diagnostic(control)
    if request == nil then return end
    if request.error ~= nil then
        write_aoi_diagnostic_result(request, "failed", request.error, nil)
        return
    end
    local world, world_error = navigation_world()
    if world == nil then
        write_aoi_diagnostic_result(request, "failed", world_error, nil)
        return
    end
    local point_manager = safe_get(world, "PointManager") or reflected_value(world, "PointManager")
    if point_manager == nil then
        write_aoi_diagnostic_result(request, "failed", "point_manager_unavailable", nil)
        return
    end
    local lod, server_lod, aoi_sizes = navigation_geometry()
    if lod == nil or server_lod == nil or aoi_sizes == nil or #aoi_sizes == 0 then
        write_aoi_diagnostic_result(request, "failed", "aoi_geometry_unavailable", nil)
        return
    end
    local ok_index, raw_index = call(point_manager, "AoiBlockToIndex", request.cellX, request.cellY)
    local aoi_index = ok_index and tonumber(raw_index) or nil
    if aoi_index == nil then
        write_aoi_diagnostic_result(request, "failed", "aoi_index_unavailable", nil)
        return
    end
    local ok_center, center = call(point_manager, "GetAoiIndexCenter", math.floor(aoi_index))
    local center_x, center_y, center_z = nil, nil, nil
    if ok_center then center_x, center_y, center_z = vector_components(center) end
    write_aoi_diagnostic_result(request, "proven", nil, {
        currentLod = math.floor(lod),
        serverLod = math.floor(server_lod),
        aoiSizes = aoi_sizes,
        blockSize = tonumber(reflected_value(point_manager, "_lwAoiBlockSize")),
        blockCount = tonumber(reflected_value(point_manager, "_lwAoiBlockCount")),
        msgCount = collection_count(reflected_value(point_manager, "_msgViewIndex")),
        addCount = collection_count(reflected_value(point_manager, "_addViewIndex")),
        curCount = collection_count(reflected_value(point_manager, "_curViewIndex")),
        aoiIndex = math.floor(aoi_index),
        centerX = center_x,
        centerY = center_y,
        centerZ = center_z,
    })
end

local function current_map_context()
    local world = select(1, navigation_world())
    if world == nil then return nil end
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    if player == nil then return nil end
    local cs = rawget(_G, "CS")
    local cs_entry = cs and safe_get(cs, "GameEntry") or nil
    local cs_data = cs_entry and safe_get(cs_entry, "Data") or nil
    local cs_player = cs_data and safe_get(cs_data, "Player") or nil
    local ok_server, server_id = call(player, "GetCurServerId")
    local ok_world, world_id = call(player, "GetCurWorldId")
    local tile_count = safe_get(world, "TileCount") or reflected_value(world, "TileCount")
    local tile_width = tonumber(tile_count and (safe_get(tile_count, "x") or safe_get(tile_count, "X")))
    local tile_height = tonumber(tile_count and (safe_get(tile_count, "y") or safe_get(tile_count, "Y")))
    local player_tile_x, player_tile_y = nil, nil
    local point_player = cs_player or player
    local player_point_id = tonumber(safe_get(point_player, "PlayerWorldPointId"))
    if player_point_id == nil then
        local ok_player_point, value = call(point_player, "get_PlayerWorldPointId")
        if ok_player_point then player_point_id = tonumber(value) end
    end
    if player_point_id == nil then
        player_point_id = tonumber(reflected_value(point_player, "<PlayerWorldPointId>k__BackingField"))
    end
    if player_point_id == nil or player_point_id <= 0 then
        local point_manager = safe_get(world, "PointManager") or reflected_value(world, "<PointManager>k__BackingField")
        local ok_my_point, my_point = false, nil
        if point_manager ~= nil then ok_my_point, my_point = call(point_manager, "GetMyPointInfo") end
        if ok_my_point and my_point ~= nil then
            player_point_id = tonumber(safe_get(my_point, "pointIndex") or reflected_value(my_point, "pointIndex"))
        end
    end
    if player_point_id ~= nil and player_point_id > 0 and player_point_id == math.floor(player_point_id) then
        local ok_player_tile, player_tile = call(world, "IndexToTilePos", math.floor(player_point_id))
        if ok_player_tile and player_tile ~= nil then
            player_tile_x = tonumber(safe_get(player_tile, "x") or safe_get(player_tile, "X"))
            player_tile_y = tonumber(safe_get(player_tile, "y") or safe_get(player_tile, "Y"))
            if player_tile_x ~= nil then player_tile_x = math.floor(player_tile_x) end
            if player_tile_y ~= nil then player_tile_y = math.floor(player_tile_y) end
        end
    end
    server_id = ok_server and tonumber(server_id) or nil
    world_id = ok_world and tonumber(world_id) or nil
    if server_id == nil or server_id <= 0 or server_id ~= math.floor(server_id) or
       world_id == nil or world_id < 0 or world_id ~= math.floor(world_id) or
       tile_width == nil or tile_width <= 0 or tile_width ~= math.floor(tile_width) or
       tile_height == nil or tile_height <= 0 or tile_height ~= math.floor(tile_height) then
        return nil
    end
    return {
        serverId = server_id, worldId = world_id, tileWidth = tile_width, tileHeight = tile_height,
        playerTileX = player_tile_x, playerTileY = player_tile_y,
    }
end

local function write_world_ready_result(request, state, error_text, method)
    local context = state == "proven" and current_map_context() or nil
    write_json(world_ready_result_path, {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        requestId = request.requestId,
        state = state,
        method = method,
        error = error_text,
        serverId = context and context.serverId or nil,
        worldId = context and context.worldId or nil,
        tileWidth = context and context.tileWidth or nil,
        tileHeight = context and context.tileHeight or nil,
        playerTileX = context and context.playerTileX or nil,
        playerTileY = context and context.playerTileY or nil,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function begin_world_ready(request)
    if request.error ~= nil then
        write_world_ready_result(request, "failed", request.error, nil)
        return
    end
    if select(1, navigation_world()) ~= nil then
        write_world_ready_result(request, "proven", nil, "already_world_scene")
        return
    end
    local scene_utils = resolve_scene_utils()
    local change_to_world = scene_utils and safe_get(scene_utils, "ChangeToWorld") or nil
    if type(change_to_world) ~= "function" then
        write_world_ready_result(request, "failed", "change_to_world_unavailable", nil)
        return
    end
    local ok_change = pcall(change_to_world, function() end)
    if not ok_change then
        write_world_ready_result(request, "failed", "change_to_world_failed", nil)
        return
    end
    request.transitionMethod = "SceneUtils.ChangeToWorld(callback)"
    request.startedClock = runtime_clock()
    pending_world_ready = request
end

local function pump_world_ready(control)
    if pending_world_ready == nil then
        local request = read_world_ready(control)
        if request ~= nil then begin_world_ready(request) end
    end
    if pending_world_ready == nil then return end
    if select(1, navigation_world()) ~= nil then
        write_world_ready_result(pending_world_ready, "proven", nil, tostring(pending_world_ready.transitionMethod or "world_transition") .. "+WorldScene")
        pending_world_ready = nil
        return
    end
    if runtime_clock() - pending_world_ready.startedClock >= WORLD_READY_TIMEOUT_SECONDS then
        write_world_ready_result(pending_world_ready, "failed", "world_map_failed", pending_world_ready.transitionMethod)
        pending_world_ready = nil
    end
end

local function read_server_jump(control)
    local values = read_kv(server_jump_path)
    if values == nil then return nil end
    pcall(os.remove, server_jump_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
    local game_pid = tonumber(values.gamePid)
    local server_id = tonumber(values.serverId)
    local request = { requestId = values.requestId, serverId = server_id }
    if values.profileId ~= control.profileId or values.sessionId ~= control.sessionId or
       values.challenge ~= control.challenge or game_pid ~= control.gamePid then
        request.error = "server_jump_identity_mismatch"
        return request
    end
    if server_id == nil or server_id ~= math.floor(server_id) or server_id < 1 or server_id > 99999 then
        request.error = "server_jump_server_id_invalid"
    end
    return request
end

local function write_server_jump_result(request, state, error_text)
    write_json(server_jump_result_path, {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        requestId = request.requestId,
        state = state,
        serverId = request.serverId,
        previousServerId = request.previousServerId,
        currentServerId = request.currentServerId,
        changed = request.previousServerId ~= nil and request.previousServerId ~= request.serverId or false,
        method = request.method,
        error = error_text,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function resolve_go_to_util()
    local value = rawget(_G, "GoToUtil")
    if value ~= nil then return value end
    local ok_require, loaded = pcall(require, "Util.GoToUtil")
    if ok_require and loaded ~= nil then return loaded end
    return rawget(_G, "GoToUtil")
end

local function resolve_cross_server_util()
    local value = rawget(_G, "CrossServerUtil")
    if value ~= nil then return value end
    local ok_require, loaded = pcall(require, "Util.CrossServerUtil")
    if ok_require and loaded ~= nil then return loaded end
    return rawget(_G, "CrossServerUtil")
end

local function begin_server_jump(request)
    if request.error ~= nil then
        write_server_jump_result(request, "failed", request.error)
        return
    end
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    if player == nil then
        write_server_jump_result(request, "failed", "live_world_player_unavailable")
        return
    end
    local ok_cur, cur_value = call(player, "GetCurServerId")
    local ok_self, self_value = call(player, "GetSelfServerId")
    local cur_server_id = ok_cur and tonumber(cur_value) or nil
    local self_server_id = ok_self and tonumber(self_value) or nil
    if cur_server_id == nil or cur_server_id <= 0 or cur_server_id ~= math.floor(cur_server_id) or
       self_server_id == nil or self_server_id <= 0 or self_server_id ~= math.floor(self_server_id) then
        write_server_jump_result(request, "failed", "live_server_id_unavailable")
        return
    end
    request.previousServerId = math.floor(cur_server_id)
    request.currentServerId = math.floor(cur_server_id)
    if request.serverId == request.previousServerId then
        request.method = "LuaEntry.Player:GetCurServerId:no-op"
        write_server_jump_result(request, "proven", nil)
        return
    end
    local goto_util = resolve_go_to_util()
    local cross_util = resolve_cross_server_util()
    if goto_util == nil or cross_util == nil then
        write_server_jump_result(request, "failed", "server_jump_util_unavailable")
        return
    end
    local precheck = safe_get(goto_util, "GoToServerPreCheck")
    if type(precheck) ~= "function" then
        write_server_jump_result(request, "failed", "server_jump_precheck_unavailable")
        return
    end
    local ok_precheck, allowed = pcall(precheck, request.serverId)
    if not ok_precheck or allowed ~= true then
        write_server_jump_result(request, "failed", "server_jump_precheck_failed")
        return
    end
    local scene_utils = resolve_scene_utils()
    local change_to_world = scene_utils and safe_get(scene_utils, "ChangeToWorld") or nil
    if type(change_to_world) ~= "function" then
        write_server_jump_result(request, "failed", "scene_utils_change_to_world_unavailable")
        return
    end
    request.startedClock = runtime_clock()
    request.method = request.serverId == self_server_id
        and "SceneUtils.ChangeToWorld+GoToUtil.CheckCrossWar+CrossServerUtil.OnBackSelfServer"
        or "SceneUtils.ChangeToWorld+GoToUtil.CheckCrossWar+CrossServerUtil.OnCrossServer"
    pending_server_jump = request
    local switched = function()
        if pending_server_jump ~= request then return end
        local check_cross_war = safe_get(goto_util, "CheckCrossWar")
        if type(check_cross_war) == "function" and not pcall(check_cross_war, request.previousServerId) then
            request.invokeError = "check_cross_war_failed"
            return
        end
        local fn = request.serverId == self_server_id
            and safe_get(cross_util, "OnBackSelfServer")
            or safe_get(cross_util, "OnCrossServer")
        if type(fn) ~= "function" then
            request.invokeError = "cross_server_function_unavailable"
            return
        end
        local ok_call = request.serverId == self_server_id
            and pcall(fn)
            or pcall(fn, request.serverId)
        if not ok_call then request.invokeError = "cross_server_function_failed" end
    end
    local ok_change = pcall(change_to_world, switched, true)
    if not ok_change then
        pending_server_jump = nil
        write_server_jump_result(request, "failed", "change_to_world_failed")
    end
end

local function pump_server_jump(control)
    if pending_server_jump == nil then
        local request = read_server_jump(control)
        if request ~= nil then
            local ok_begin, begin_error = pcall(begin_server_jump, request)
            if not ok_begin then
                write_server_jump_result(request, "failed", "server_jump_exception:" .. tostring(begin_error))
            end
        end
    end
    if pending_server_jump == nil then return end
    local request = pending_server_jump
    if request.invokeError ~= nil then
        write_server_jump_result(request, "failed", request.invokeError)
        pending_server_jump = nil
        return
    end
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    local ok_cur, cur_value = call(player, "GetCurServerId")
    local cur_server_id = ok_cur and tonumber(cur_value) or nil
    if cur_server_id ~= nil and cur_server_id > 0 and cur_server_id == math.floor(cur_server_id) then
        request.currentServerId = math.floor(cur_server_id)
        if request.currentServerId == request.serverId then
            write_server_jump_result(request, "proven", nil)
            pending_server_jump = nil
            return
        end
    end
    if runtime_clock() - request.startedClock >= SERVER_JUMP_TIMEOUT_SECONDS then
        write_server_jump_result(request, "failed", "server_jump_timeout")
        pending_server_jump = nil
    end
end


local function valid_exact_positive_id(value)
    return type(value) == "string" and #value > 0 and #value <= 20 and
        string.match(value, "^[0-9]+$") ~= nil and string.match(value, "^0+$") == nil
end

local function exact_runtime_id(value)
    if value == nil then return nil end
    local text = tostring(value)
    if valid_exact_positive_id(text) then return text end
    return nil
end

local function read_truck_quick_rob(control)
    local values = read_kv(truck_quick_rob_path)
    if values == nil then return nil end
    pcall(os.remove, truck_quick_rob_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
    local game_pid = tonumber(values.gamePid)
    local server_id = tonumber(values.serverId)
    local request = {
        requestId = values.requestId,
        serverId = server_id,
        marchUuid = values.marchUuid,
        trainUuid = values.trainUuid,
    }
    if values.profileId ~= control.profileId or values.sessionId ~= control.sessionId or
       values.challenge ~= control.challenge or game_pid ~= control.gamePid then
        request.error = "truck_quick_rob_identity_mismatch"
        return request
    end
    if server_id == nil or server_id ~= math.floor(server_id) or server_id < 1 or server_id > 99999 or
       not valid_exact_positive_id(values.marchUuid) or not valid_exact_positive_id(values.trainUuid) then
        request.error = "truck_quick_rob_target_invalid"
    end
    return request
end

local function resolve_truck_railway_util()
    local loaded = package and package.loaded or nil
    local value = loaded and loaded["DataCenter.LWRailway.Util.RailwayUtil"] or nil
    if value ~= nil then return value end
    local ok_require, required = pcall(require, "DataCenter.LWRailway.Util.RailwayUtil")
    if ok_require and required ~= nil then return required end
    return rawget(_G, "RailwayUtil")
end

local function resolve_truck_event_ids()
    local value = rawget(_G, "EventId")
    if value ~= nil then return value end
    local ok_require, required = pcall(require, "Framework.UI.Message.EventId")
    if ok_require and required ~= nil then return required end
    return rawget(_G, "EventId")
end

local function resolve_truck_event_manager()
    local manager_type = rawget(_G, "EventManager")
    if manager_type == nil then return nil end
    local ok_instance, instance = call(manager_type, "GetInstance")
    if ok_instance and instance ~= nil then return instance end
    return manager_type
end

local function find_live_truck_target(request)
    local world, world_error = navigation_world()
    if world == nil then return nil, nil, world_error end
    local march_manager = safe_get(world, "MarchDataManager") or reflected_value(world, "MarchDataManager")
    if march_manager == nil then
        local ok_manager, value = call(world, "get_MarchDataManager")
        if ok_manager then march_manager = value end
    end
    if march_manager == nil then return nil, nil, "world_march_manager_unavailable" end
    local ok_all, collection = call(march_manager, "GetAllMarchesByCS")
    if not ok_all or collection == nil then collection = reflected_value(march_manager, "allMarches") end
    if collection == nil then return nil, nil, "world_march_collection_unavailable" end
    local ok_enum, enumerator = call(collection, "GetEnumerator")
    if not ok_enum or enumerator == nil then return nil, nil, "world_march_enumerator_unavailable" end

    local scanned = 0
    while scanned < 50000 do
        local ok_move, moved = call(enumerator, "MoveNext")
        if not ok_move then return nil, nil, "world_march_enumerator_failed" end
        if moved ~= true then break end
        scanned = scanned + 1
        local pair = safe_get(enumerator, "Current")
        local march = pair and (safe_get(pair, "Value") or pair) or nil
        if march ~= nil and exact_runtime_id(safe_get(march, "uuid") or safe_get(march, "Uuid") or safe_get(march, "_uuid")) == request.marchUuid then
            local train = safe_get(march, "train")
            if train == nil then return march, nil, "truck_train_data_unavailable" end
            local actual_train_uuid = exact_runtime_id(safe_get(train, "uuid") or safe_get(train, "Uuid"))
            if actual_train_uuid ~= request.trainUuid then
                return march, train, "truck_train_identity_mismatch"
            end
            local train_type = tonumber(safe_get(train, "type") or safe_get(train, "Type"))
            if train_type ~= 1 then return march, train, "truck_target_not_truck" end
            local train_server_id = tonumber(safe_get(train, "serverId") or safe_get(train, "ServerId"))
            if train_server_id == nil or train_server_id ~= math.floor(train_server_id) or train_server_id ~= request.serverId then
                return march, train, "truck_train_server_mismatch"
            end
            return march, train, nil
        end
    end
    return nil, nil, "truck_live_target_not_found"
end

local function current_truck_quick_rob_logic(request)
    local data_center = rawget(_G, "DataCenter")
    local battle_manager = data_center and safe_get(data_center, "LWBattleManager") or nil
    if battle_manager == nil then return nil, "truck_battle_manager_unavailable" end
    local ok_logic, logic = call(battle_manager, "GetCurBattleLogic")
    if not ok_logic or logic == nil then return nil, nil end
    local param = safe_get(logic, "param")
    local enter_type = param and safe_get(param, "enterType") or nil
    local pve_enter_type = rawget(_G, "PVEEnterType")
    local truck_rob_type = pve_enter_type and safe_get(pve_enter_type, "TruckRob") or 4
    if tonumber(enter_type) ~= tonumber(truck_rob_type) then return nil, "truck_battle_logic_mismatch" end
    local extra_data = safe_get(param, "extraData")
    local train = extra_data and safe_get(extra_data, "trainData") or nil
    if train == nil or exact_runtime_id(safe_get(train, "uuid") or safe_get(train, "Uuid")) ~= request.trainUuid then
        return nil, "truck_battle_target_mismatch"
    end
    return logic, nil
end


local function normalize_truck_plunder_rewards(rewards)
    if rewards == nil then return {}, true end
    if type(rewards) ~= "table" then return {}, false end

    local data_center = rawget(_G, "DataCenter")
    local item_manager = data_center and safe_get(data_center, "ItemTemplateManager") or nil
    local reward_manager = data_center and safe_get(data_center, "RewardManager") or nil
    local reward_type_enum = rawget(_G, "RewardType")
    local goods_type = reward_type_enum and tonumber(safe_get(reward_type_enum, "GOODS")) or nil
    local load_path = rawget(_G, "LoadPath")
    local item_path = load_path and safe_get(load_path, "ItemPath") or nil

    local by_key, order = {}, {}
    local unresolved = 0
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
                            if ok_format and formatted ~= nil and formatted ~= "" then icon_path = tostring(formatted) end
                        end
                    end
                end
            elseif reward_manager ~= nil then
                local ok_name, resolved_name = call(reward_manager, "GetNameByType", reward_type, item_id)
                if not ok_name or resolved_name == nil or tostring(resolved_name) == "" then
                    ok_name, resolved_name = call(reward_manager, "GetNameByType", reward_type)
                end
                if ok_name and resolved_name ~= nil and tostring(resolved_name) ~= "" then
                    name = tostring(resolved_name)
                end
                local ok_icon, resolved_icon = call(reward_manager, "GetPicByType", reward_type, item_id)
                if not ok_icon or resolved_icon == nil or tostring(resolved_icon) == "" then
                    ok_icon, resolved_icon = call(reward_manager, "GetPicByType", reward_type)
                end
                if ok_icon and resolved_icon ~= nil and tostring(resolved_icon) ~= "" then
                    icon_path = tostring(resolved_icon)
                end
            end

            if name ~= nil and icon_path ~= nil then
                -- Internal rebuild identity only. Current-v19 supplies the
                -- authoritative reward type/item id, name and icon; the exact
                -- original LWBridge plunderRewards key producer is unrecovered.
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
            else
                unresolved = unresolved + 1
            end
        else
            unresolved = unresolved + 1
        end
    end

    local result = {}
    for _, key in ipairs(order) do result[#result + 1] = by_key[key] end
    return result, unresolved == 0
end

local function truck_reward_count(value)
    if value == nil then return 0 end
    local count = tonumber(safe_get(value, "Count") or safe_get(value, "Length"))
    if count ~= nil and count >= 0 then return math.floor(count) end
    if type(value) == "table" then
        local maximum = 0
        for key in pairs(value) do
            if type(key) == "number" and key > maximum and key == math.floor(key) then maximum = key end
        end
        return maximum
    end
    return 0
end

local function write_truck_quick_rob_result(request, state, error_text)
    write_json(truck_quick_rob_result_path, {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        requestId = request.requestId,
        state = state,
        serverId = request.serverId,
        currentServerId = request.currentServerId,
        marchUuid = request.marchUuid,
        trainUuid = request.trainUuid,
        requestSent = request.requestSent == true,
        battleWon = request.battleWon,
        rewardCount = request.rewardCount,
        plunderRewards = request.plunderRewards,
        rewardNormalizationComplete = request.rewardNormalizationComplete,
        dailyRobCount = request.dailyRobCount,
        method = request.method,
        error = error_text,
    })
end

local function detach_truck_quick_rob_listeners(request)
    local event_manager = request and request.eventManager or nil
    if event_manager == nil then return end
    if request.successEventId ~= nil and request.successCallback ~= nil then
        call(event_manager, "RemoveListener", request.successEventId, request.successCallback)
    end
    if request.terminalEventId ~= nil and request.terminalCallback ~= nil then
        call(event_manager, "RemoveListener", request.terminalEventId, request.terminalCallback)
    end
    request.eventManager = nil
    request.successCallback = nil
    request.terminalCallback = nil
end

local function exit_owned_truck_quick_rob_battle(request)
    if request == nil or request.battleOwned ~= true then return end
    local logic = select(1, current_truck_quick_rob_logic(request))
    if logic == nil then return end
    local data_center = rawget(_G, "DataCenter")
    local battle_manager = data_center and safe_get(data_center, "LWBattleManager") or nil
    if battle_manager ~= nil then call(battle_manager, "Exit") end
end

local function finish_truck_quick_rob(request, state, error_text, exit_battle)
    detach_truck_quick_rob_listeners(request)
    write_truck_quick_rob_result(request, state, error_text)
    if exit_battle == true then exit_owned_truck_quick_rob_battle(request) end
    if pending_truck_quick_rob == request then pending_truck_quick_rob = nil end
end

local function abandon_truck_quick_rob()
    local request = pending_truck_quick_rob
    if request == nil then return end
    detach_truck_quick_rob_listeners(request)
    exit_owned_truck_quick_rob_battle(request)
    pending_truck_quick_rob = nil
end

local function begin_truck_quick_rob(request)
    if request.error ~= nil then
        write_truck_quick_rob_result(request, "failed", request.error)
        return
    end
    if pending_navigation ~= nil or pending_world_ready ~= nil or pending_server_jump ~= nil or pending_march_follow ~= nil then
        write_truck_quick_rob_result(request, "failed", "game_operation_in_progress")
        return
    end

    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    local ok_cur, cur_value = call(player, "GetCurServerId")
    local current_server_id = ok_cur and tonumber(cur_value) or nil
    if current_server_id == nil or current_server_id <= 0 or current_server_id ~= math.floor(current_server_id) then
        write_truck_quick_rob_result(request, "failed", "current_server_id_unavailable")
        return
    end
    request.currentServerId = math.floor(current_server_id)
    if request.currentServerId ~= request.serverId then
        write_truck_quick_rob_result(request, "failed", "truck_quick_rob_server_mismatch")
        return
    end

    local data_center = rawget(_G, "DataCenter")
    local battle_manager = data_center and safe_get(data_center, "LWBattleManager") or nil
    local station_manager = data_center and safe_get(data_center, "LWMyStationDataManager") or nil
    if battle_manager == nil or station_manager == nil then
        write_truck_quick_rob_result(request, "failed", "truck_battle_components_unavailable")
        return
    end
    local ok_existing, existing_logic = call(battle_manager, "GetCurBattleLogic")
    if ok_existing and existing_logic ~= nil then
        write_truck_quick_rob_result(request, "failed", "game_operation_in_progress")
        return
    end

    local _, train, target_error = find_live_truck_target(request)
    if target_error ~= nil or train == nil then
        write_truck_quick_rob_result(request, "failed", target_error or "truck_live_target_not_found")
        return
    end

    local railway_util = resolve_truck_railway_util()
    local click_attack = railway_util and safe_get(railway_util, "ClickAttackTrain") or nil
    local event_ids = resolve_truck_event_ids()
    local event_manager = resolve_truck_event_manager()
    local success_event_id = event_ids and safe_get(event_ids, "TrainSkirmishDataReceived") or nil
    local terminal_event_id = event_ids and safe_get(event_ids, "TrainAttackReceived") or nil
    if type(click_attack) ~= "function" or event_manager == nil or success_event_id == nil or terminal_event_id == nil then
        write_truck_quick_rob_result(request, "failed", "truck_quick_rob_components_unavailable")
        return
    end

    request.liveTrain = train
    request.stationManager = station_manager
    request.eventManager = event_manager
    request.successEventId = success_event_id
    request.terminalEventId = terminal_event_id
    request.successCallback = function(message)
        if pending_truck_quick_rob == request and request.requestSent == true then
            request.successMessage = message
            request.successReceived = true
            request.successClock = runtime_clock()
        end
    end
    request.terminalCallback = function()
        if pending_truck_quick_rob == request and request.requestSent == true then
            request.terminalReceived = true
            request.terminalClock = runtime_clock()
        end
    end
    local ok_success_listener = select(1, call(event_manager, "AddListener", success_event_id, request.successCallback))
    local ok_terminal_listener = select(1, call(event_manager, "AddListener", terminal_event_id, request.terminalCallback))
    if not ok_success_listener or not ok_terminal_listener then
        detach_truck_quick_rob_listeners(request)
        write_truck_quick_rob_result(request, "failed", "truck_quick_rob_listener_install_failed")
        return
    end

    request.method = "RailwayUtil.ClickAttackTrain+LWMyStationDataManager.TryAttackTrain"
    request.startedClock = runtime_clock()
    request.phase = "waiting_logic"
    pending_truck_quick_rob = request
    -- Ownership is set before entry so a partial Enter() followed by an
    -- exception can still be cleaned up. exit_owned_truck_quick_rob_battle()
    -- additionally proves the active logic target before calling Exit().
    request.battleOwned = true
    local ok_click = pcall(click_attack, train, true)
    if not ok_click then
        finish_truck_quick_rob(request, "failed", "truck_quick_rob_entry_failed", true)
        return
    end
end

local function pump_truck_quick_rob(control)
    if pending_truck_quick_rob == nil then
        local request = read_truck_quick_rob(control)
        if request ~= nil then
            local ok_begin, begin_error = pcall(begin_truck_quick_rob, request)
            if not ok_begin then
                detach_truck_quick_rob_listeners(request)
                write_truck_quick_rob_result(request, "failed", "truck_quick_rob_exception:" .. tostring(begin_error))
                if pending_truck_quick_rob == request then pending_truck_quick_rob = nil end
            end
        end
    end
    local request = pending_truck_quick_rob
    if request == nil then return end

    if request.phase == "waiting_logic" then
        local logic, logic_error = current_truck_quick_rob_logic(request)
        if logic_error ~= nil and logic_error ~= "truck_battle_manager_unavailable" then
            finish_truck_quick_rob(request, "failed", logic_error, true)
            return
        end
        if logic ~= nil and safe_get(logic, "sceneLoadRequest") ~= nil then
            local station_manager = request.stationManager
            local ok_formation, formation = call(station_manager, "GetRobFormation")
            if not ok_formation or formation == nil then
                finish_truck_quick_rob(request, "failed", "truck_rob_formation_unavailable", true)
                return
            end
            local local_heroes = safe_get(formation, "localHeroes")
            local hero_count = nil
            local table_count = table and safe_get(table, "count") or nil
            if type(table_count) == "function" then
                local ok_count, value = pcall(table_count, local_heroes)
                if ok_count then hero_count = tonumber(value) end
            end
            if hero_count ~= nil and hero_count <= 0 then
                finish_truck_quick_rob(request, "failed", "truck_rob_formation_empty", true)
                return
            end

            local live_train_uuid = safe_get(request.liveTrain, "uuid") or safe_get(request.liveTrain, "Uuid")
            local live_server_id = tonumber(safe_get(request.liveTrain, "serverId") or safe_get(request.liveTrain, "ServerId"))
            if exact_runtime_id(live_train_uuid) ~= request.trainUuid or live_server_id ~= request.serverId then
                finish_truck_quick_rob(request, "failed", "truck_train_identity_changed", true)
                return
            end

            local ok_save = select(1, call(station_manager, "TrySaveTruckFormation", formation, true))
            if not ok_save then
                finish_truck_quick_rob(request, "failed", "truck_rob_formation_save_failed", true)
                return
            end
            request.requestSent = true
            request.sentClock = runtime_clock()
            request.successReceived = false
            request.terminalReceived = false
            local ok_attack, attack_result = call(station_manager, "TryAttackTrain", live_train_uuid, request.serverId, formation)
            if not ok_attack or attack_result == false then
                request.requestSent = false
                finish_truck_quick_rob(request, "failed", "train_attack_send_failed", true)
                return
            end
            request.phase = "waiting_response"
            return
        end
        if runtime_clock() - request.startedClock >= TRUCK_QUICK_ROB_SETUP_TIMEOUT_SECONDS then
            finish_truck_quick_rob(request, "failed", "truck_quick_rob_setup_timeout", true)
        end
        return
    end

    if request.phase == "waiting_response" then
        if request.successReceived == true then
            local logic = select(1, current_truck_quick_rob_logic(request))
            if logic ~= nil then
                local battle_data = safe_get(logic, "battleData")
                if battle_data ~= nil then
                    local ok_top, top_player_win = pcall(function() return battle_data.topPlayerWin end)
                    if ok_top and type(top_player_win) == "boolean" then
                        request.battleWon = not top_player_win
                        local param = safe_get(logic, "param")
                        local raw_rewards = param and safe_get(param, "attackTrainReward") or nil
                        request.rewardCount = truck_reward_count(raw_rewards)
                        request.plunderRewards, request.rewardNormalizationComplete =
                            normalize_truck_plunder_rewards(raw_rewards)
                        local daily_rob_count = tonumber(
                            request.successMessage and safe_get(request.successMessage, "dailyRobCount") or nil)
                        if daily_rob_count ~= nil and daily_rob_count >= 0 and daily_rob_count == math.floor(daily_rob_count) then
                            request.dailyRobCount = math.floor(daily_rob_count)
                        end
                        finish_truck_quick_rob(request, "proven", nil, true)
                        return
                    end
                end
            end
        elseif request.terminalReceived == true then
            finish_truck_quick_rob(request, "failed", "train_attack_rejected", true)
            return
        end

        if runtime_clock() - request.sentClock >= TRUCK_QUICK_ROB_RESPONSE_TIMEOUT_SECONDS then
            if request.successReceived == true then
                finish_truck_quick_rob(request, "ambiguous", "truck_quick_rob_result_unavailable", true)
            else
                finish_truck_quick_rob(request, "ambiguous", "server_response_timeout", true)
            end
        end
    end
end

local function write_march_follow_result(request, state, error_text)
    write_json(march_follow_result_path, {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        requestId = request.requestId,
        state = state,
        serverId = request.serverId,
        marchUuid = request.marchUuid,
        currentServerId = request.currentServerId,
        worldX = request.worldX,
        worldY = request.worldY,
        worldZ = request.worldZ,
        method = request.method,
        error = error_text,
    })
end

local function begin_march_follow(request)
    if request.error ~= nil then
        write_march_follow_result(request, "failed", request.error)
        return
    end

    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    local ok_cur, cur_value = call(player, "GetCurServerId")
    local cur_server_id = ok_cur and tonumber(cur_value) or nil
    if cur_server_id == nil or cur_server_id <= 0 or cur_server_id ~= math.floor(cur_server_id) then
        write_march_follow_result(request, "failed", "current_server_id_unavailable")
        return
    end
    request.currentServerId = math.floor(cur_server_id)
    if request.currentServerId ~= request.serverId then
        write_march_follow_result(request, "failed", "march_follow_server_mismatch")
        return
    end

    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "WorldMarchDataManager") or nil
    if manager == nil then
        write_march_follow_result(request, "failed", "world_march_manager_unavailable")
        return
    end
    local goto_util = rawget(_G, "GoToUtil")
    local jump_march = goto_util and safe_get(goto_util, "JumpToMarchByUuid") or nil
    if type(jump_march) ~= "function" then
        write_march_follow_result(request, "failed", "goto_world_march_unavailable")
        return
    end

    -- Current-v19 GoToUtil.JumpToMarchByUuid is the source-backed shared
    -- moving-march route. It follows an already loaded march immediately;
    -- otherwise it sends MsgDefines.GetMarchPos. GetMarchPosMessage defaults
    -- worldId to 0 and moves/opens the returned march. The original
    -- map_march_follow -> gotoWorldMarch call supplied only serverId/marchUuid
    -- with a 5000 ms native timeout, so do not invent coordinates or march type.
    request.method = "GoToUtil.JumpToMarchByUuid"
    request.startedClock = runtime_clock()
    pending_march_follow = request
    local ok_jump = pcall(jump_march, request.marchUuid, request.serverId, 0)
    if not ok_jump then
        request.invokeError = "goto_world_march_failed"
    end
end

local function pump_march_follow(control)
    if pending_march_follow == nil then
        local request = read_march_follow(control)
        if request ~= nil then
            local ok_begin, begin_error = pcall(begin_march_follow, request)
            if not ok_begin then
                write_march_follow_result(request, "failed", "march_follow_exception:" .. tostring(begin_error))
            end
        end
    end
    if pending_march_follow == nil then return end
    local request = pending_march_follow
    if request.invokeError ~= nil then
        write_march_follow_result(request, "failed", request.invokeError)
        pending_march_follow = nil
        return
    end

    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    local ok_cur, cur_value = call(player, "GetCurServerId")
    local cur_server_id = ok_cur and tonumber(cur_value) or nil
    if cur_server_id == nil or cur_server_id <= 0 or cur_server_id ~= math.floor(cur_server_id) then
        write_march_follow_result(request, "failed", "current_server_id_unavailable")
        pending_march_follow = nil
        return
    end
    request.currentServerId = math.floor(cur_server_id)
    if request.currentServerId ~= request.serverId then
        write_march_follow_result(request, "failed", "march_follow_server_mismatch")
        pending_march_follow = nil
        return
    end

    local observed = false
    local data_center = rawget(_G, "DataCenter")
    local manager = data_center and safe_get(data_center, "WorldMarchDataManager") or nil
    if manager ~= nil then
        local ok_march, march = call(manager, "GetMarch", request.marchUuid)
        if ok_march and march ~= nil then
            local actual_uuid = tonumber(safe_get(march, "uuid") or safe_get(march, "_uuid"))
            if actual_uuid ~= nil and actual_uuid ~= request.marchUuid then
                write_march_follow_result(request, "failed", "march_identity_mismatch")
                pending_march_follow = nil
                return
            end
            local march_server_id = tonumber(safe_get(march, "serverId"))
            if march_server_id ~= nil and march_server_id > 0 and math.floor(march_server_id) ~= request.serverId then
                write_march_follow_result(request, "failed", "march_server_mismatch")
                pending_march_follow = nil
                return
            end
            local ok_pos, world_pos = call(march, "GetMarchCurPos")
            request.worldX = ok_pos and tonumber(world_pos and safe_get(world_pos, "x")) or nil
            request.worldY = ok_pos and tonumber(world_pos and safe_get(world_pos, "y")) or nil
            request.worldZ = ok_pos and tonumber(world_pos and safe_get(world_pos, "z")) or nil
            observed = true
        end
    end

    if not observed then
        local cs = rawget(_G, "CS")
        local scene_manager = cs and safe_get(cs, "SceneManager") or nil
        local world = scene_manager and safe_get(scene_manager, "World") or nil
        local ok_world_march, world_march = call(world, "GetMarch", request.marchUuid)
        if ok_world_march and world_march ~= nil then
            local world_server_id = tonumber(safe_get(world_march, "serverId"))
            if world_server_id ~= nil and world_server_id > 0 and math.floor(world_server_id) ~= request.serverId then
                write_march_follow_result(request, "failed", "march_server_mismatch")
                pending_march_follow = nil
                return
            end
            observed = true
        end
    end

    if observed then
        write_march_follow_result(request, "proven", nil)
        pending_march_follow = nil
        return
    end
    if runtime_clock() - request.startedClock >= NAVIGATION_TIMEOUT_SECONDS then
        write_march_follow_result(request, "failed", "march_follow_completion_timeout")
        pending_march_follow = nil
    end
end

local function write_navigation_result(request, state, error_text, current_x, current_y, lod, server_lod, aoi_sizes)
    if state == "proven" and lod == nil and server_lod == nil and aoi_sizes == nil then
        lod, server_lod, aoi_sizes = navigation_geometry()
    end
    local payload = {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        requestId = request.requestId,
        state = state,
        serverId = request.serverId,
        worldId = request.worldId,
        liveCurServerId = request.liveCurServerId,
        liveSelfServerId = request.liveSelfServerId,
        liveWorldId = request.liveWorldId,
        liveWorldType = request.liveWorldType,
        targetX = request.targetX,
        targetY = request.targetY,
        currentX = current_x,
        currentY = current_y,
        verifiedTargetX = request.verifiedTargetX,
        verifiedTargetY = request.verifiedTargetY,
        expectedUniqueTileX = request.expectedUniqueTileX,
        expectedUniqueTileY = request.expectedUniqueTileY,
        verifiedUniqueTileX = request.verifiedUniqueTileX,
        verifiedUniqueTileY = request.verifiedUniqueTileY,
        verifyCurTargetX = request.verifyCurTargetX,
        verifyCurTargetY = request.verifyCurTargetY,
        verifyCurTargetZ = request.verifyCurTargetZ,
        convertedTileX = request.convertedTileX,
        convertedTileY = request.convertedTileY,
        targetWorldX = request.targetWorldX,
        targetWorldY = request.targetWorldY,
        targetWorldZ = request.targetWorldZ,
        effectiveTargetWorldX = request.effectiveTargetWorldX,
        effectiveTargetWorldY = request.effectiveTargetWorldY,
        effectiveTargetWorldZ = request.effectiveTargetWorldZ,
        expectedGotoWorldX = request.expectedGotoWorldX,
        expectedGotoWorldY = request.expectedGotoWorldY,
        expectedGotoWorldZ = request.expectedGotoWorldZ,
        expectedGotoError = request.expectedGotoError,
        preCanMoving = request.preCanMoving,
        preEnabled = request.preEnabled,
        preCurTargetX = request.preCurTargetX,
        preCurTargetY = request.preCurTargetY,
        preCurTargetZ = request.preCurTargetZ,
        postCanMoving = request.postCanMoving,
        postEnabled = request.postEnabled,
        postCurTargetX = request.postCurTargetX,
        postCurTargetY = request.postCurTargetY,
        postCurTargetZ = request.postCurTargetZ,
        postTargetTileX = request.postTargetTileX,
        postTargetTileY = request.postTargetTileY,
        currentLod = lod,
        serverLod = server_lod,
        lwAoiBlockSizeArray = aoi_sizes,
        geometryWorldAvailable = request.geometryWorldAvailable,
        geometryPointManagerGetterOk = request.geometryPointManagerGetterOk,
        geometryPointManagerAvailable = request.geometryPointManagerAvailable,
        geometryPointManagerType = request.geometryPointManagerType,
        geometryLodAvailable = request.geometryLodAvailable,
        geometryServerLodCallOk = request.geometryServerLodCallOk,
        geometryServerLodAvailable = request.geometryServerLodAvailable,
        geometryAoiGetterOk = request.geometryAoiGetterOk,
        geometryAoiArrayAvailable = request.geometryAoiArrayAvailable,
        geometryAoiLength = request.geometryAoiLength,
        geometryAoiValuesAvailable = request.geometryAoiValuesAvailable,
        method = request.navigationMethod,
        error = error_text,
        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    }
    if not write_json(navigation_result_path, payload) then
        write_json(aoi_diagnostic_result_path, {
            schemaVersion = 1,
            bridgeVersion = M.VERSION,
            profileId = active and active.profileId or nil,
            sessionId = active and active.sessionId or nil,
            gamePid = active and active.gamePid or nil,
            requestId = request.requestId,
            state = "navigation_result_write_failed",
            error = "map_navigation_result_open_failed",
            capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
        })
    end
end

local function expected_normal_world_target(world_pos, server_id)
    local x = tonumber(safe_get(world_pos, "x"))
    local y = tonumber(safe_get(world_pos, "y"))
    local z = tonumber(safe_get(world_pos, "z"))
    if x == nil or y == nil or z == nil then
        return nil, nil, nil, "goto_world_target_invalid"
    end
    local cs = rawget(_G, "CS")
    local scene_manager = cs and safe_get(cs, "SceneManager") or nil
    local ok_in_world, in_world = call(scene_manager, "IsInWorld")
    if server_id ~= nil and server_id > 0 and ok_in_world and in_world == true then
        local season_util = rawget(_G, "SeasonUtil")
        local get_season_info = season_util and safe_get(season_util, "GetSeasonInfo") or nil
        if type(get_season_info) ~= "function" then
            return nil, nil, nil, "season_info_unavailable"
        end
        local ok_info, season_info = pcall(get_season_info, server_id)
        if not ok_info then return nil, nil, nil, "season_info_failed" end
        if season_info ~= nil then
            local ok_type, server_type = call(season_info, "GetServerType", false)
            local season_map_type = rawget(_G, "SeasonMapType")
            local nine_nation = season_map_type and safe_get(season_map_type, "NineNation") or nil
            if not ok_type or nine_nation == nil then
                return nil, nil, nil, "season_server_type_unavailable"
            end
            if server_type == nine_nation then
                local ok_index, map_index = call(season_info, "GetNinePalacesIndex", server_id)
                local scene_utils = resolve_scene_utils()
                local offset_fn = scene_utils and safe_get(scene_utils, "GetNinePalacesOffsetByIndex") or nil
                if not ok_index or map_index == nil or type(offset_fn) ~= "function" then
                    return nil, nil, nil, "nine_palaces_target_unavailable"
                end
                local ok_offset, offset_x, _offset_y, offset_z = pcall(offset_fn, map_index)
                offset_x = ok_offset and tonumber(offset_x) or nil
                offset_z = ok_offset and tonumber(offset_z) or nil
                if offset_x == nil or offset_z == nil then
                    return nil, nil, nil, "nine_palaces_offset_unavailable"
                end
                if offset_x ~= 0 or offset_z ~= 0 then
                    x = (x % 2000) + offset_x
                    z = (z % 2000) + offset_z
                end
            end
        end
    end
    return x, y, z, nil
end

local function begin_navigation(request)
    if request.error ~= nil then
        write_navigation_result(request, "failed", request.error, nil, nil)
        return
    end
    local world, world_error = navigation_world()
    if world == nil then
        write_navigation_result(request, "failed", world_error, nil, nil)
        return
    end
    local cs = rawget(_G, "CS")
    local vector_type = cs and cs.UnityEngine and cs.UnityEngine.Vector2Int
    if vector_type == nil then
        write_navigation_result(request, "failed", "vector2int_unavailable", nil, nil)
        return
    end
    local ok_vector, tile = pcall(function() return vector_type(request.targetX, request.targetY) end)
    if not ok_vector or tile == nil then
        write_navigation_result(request, "failed", "navigation_tile_construct_failed", nil, nil)
        return
    end
    request.navigationTile = tile
    local ok_in_map, in_map = call(world, "IsInMap", tile)
    if not ok_in_map or in_map ~= true then
        write_navigation_result(request, "failed", "navigation_target_out_of_map", nil, nil)
        return
    end
    local scene_utils = resolve_scene_utils()
    local force_change_scene = rawget(_G, "ForceChangeScene")
    local force_world = force_change_scene and safe_get(force_change_scene, "World") or nil
    local tile_to_world = scene_utils and safe_get(scene_utils, "TileToWorld") or nil
    if type(tile_to_world) ~= "function" or force_world == nil then
        write_navigation_result(request, "failed", "scene_utils_tile_to_world_unavailable", nil, nil)
        return
    end
    local ok_world, world_pos = pcall(tile_to_world, tile, force_world, request.serverId)
    if not ok_world or world_pos == nil then
        write_navigation_result(request, "failed", "scene_utils_tile_to_world_failed", nil, nil)
        return
    end
    local world_to_tile = scene_utils and safe_get(scene_utils, "WorldToTile") or nil
    local tile_to_unique = scene_utils and safe_get(scene_utils, "TileToUniqueTile") or nil
    if type(world_to_tile) ~= "function" then
        write_navigation_result(request, "failed", "scene_utils_world_to_tile_unavailable", nil, nil)
        return
    end
    if type(tile_to_unique) ~= "function" then
        write_navigation_result(request, "failed", "scene_utils_tile_to_unique_unavailable", nil, nil)
        return
    end
    local ok_roundtrip, roundtrip_tile = pcall(world_to_tile, world_pos)
    if not ok_roundtrip or roundtrip_tile == nil then
        write_navigation_result(request, "failed", "scene_utils_world_to_tile_failed", nil, nil)
        return
    end
    request.convertedTileX = tonumber(safe_get(roundtrip_tile, "x"))
    request.convertedTileY = tonumber(safe_get(roundtrip_tile, "y"))
    local ok_unique, unique_tile = pcall(tile_to_unique, tile, request.serverId)
    if not ok_unique or unique_tile == nil then
        write_navigation_result(request, "failed", "scene_utils_tile_to_unique_failed", nil, nil)
        return
    end
    request.expectedUniqueTileX = tonumber(safe_get(unique_tile, "x"))
    request.expectedUniqueTileY = tonumber(safe_get(unique_tile, "y"))
    if request.expectedUniqueTileX == nil or request.expectedUniqueTileY == nil then
        write_navigation_result(request, "failed", "scene_utils_unique_tile_invalid", nil, nil)
        return
    end
    request.navigationWorldPos = world_pos
    request.targetWorldX = tonumber(safe_get(world_pos, "x"))
    request.targetWorldY = tonumber(safe_get(world_pos, "y"))
    request.targetWorldZ = tonumber(safe_get(world_pos, "z"))
    request.preCanMoving = safe_get(world, "CanMoving")
    request.preEnabled = safe_get(world, "Enabled")
    local pre_target = safe_get(world, "CurTarget")
    request.preCurTargetX = tonumber(pre_target and safe_get(pre_target, "x"))
    request.preCurTargetY = tonumber(pre_target and safe_get(pre_target, "y"))
    request.preCurTargetZ = tonumber(pre_target and safe_get(pre_target, "z"))
    if request.convertedTileX ~= request.targetX or request.convertedTileY ~= request.targetY then
        write_navigation_result(request, "failed", "scene_utils_tile_roundtrip_mismatch", nil, nil)
        return
    end
    local init_zoom = tonumber(safe_get(world, "InitZoom"))
    if init_zoom == nil then
        write_navigation_result(request, "failed", "world_init_zoom_unavailable", nil, nil)
        return
    end
    local focus_time = tonumber(rawget(_G, "LookAtFocusTime")) or 0.2
    local goto_util = rawget(_G, "GoToUtil")
    if goto_util == nil then
        write_navigation_result(request, "failed", "goto_util_unavailable", nil, nil)
        return
    end
    local lua_entry = rawget(_G, "LuaEntry")
    local player = lua_entry and safe_get(lua_entry, "Player") or nil
    if player == nil then
        write_navigation_result(request, "failed", "live_world_player_unavailable", nil, nil)
        return
    end
    local ok_cur_server_id, cur_server_id_value = call(player, "GetCurServerId")
    local live_cur_server_id = ok_cur_server_id and tonumber(cur_server_id_value) or nil
    local ok_self_server_id, self_server_id_value = call(player, "GetSelfServerId")
    local live_self_server_id = ok_self_server_id and tonumber(self_server_id_value) or nil
    if live_cur_server_id == nil or live_cur_server_id <= 0 or live_cur_server_id ~= math.floor(live_cur_server_id) then
        write_navigation_result(request, "failed", "live_current_server_id_unavailable", nil, nil)
        return
    end
    if live_self_server_id == nil or live_self_server_id <= 0 or live_self_server_id ~= math.floor(live_self_server_id) then
        write_navigation_result(request, "failed", "live_self_server_id_unavailable", nil, nil)
        return
    end
    request.liveCurServerId = live_cur_server_id
    request.liveSelfServerId = live_self_server_id
    if request.serverId ~= live_cur_server_id then
        write_navigation_result(request, "failed", "navigation_server_mismatch", nil, nil)
        return
    end
    local ok_live_world_id, live_world_id_value = call(player, "GetCurWorldId")
    local live_world_id = ok_live_world_id and tonumber(live_world_id_value) or nil
    if live_world_id == nil or live_world_id < 0 or live_world_id ~= math.floor(live_world_id) then
        write_navigation_result(request, "failed", "live_world_id_unavailable", nil, nil)
        return
    end
    request.liveWorldId = live_world_id
    local navigation_fn = nil
    if live_world_id > 0 then
        if request.worldId ~= live_world_id then
            write_navigation_result(request, "failed", "special_world_target_type_unavailable", nil, nil)
            return
        end
        local ok_live_world_type, live_world_type_value = call(player, "GetCurWorldType")
        local live_world_type = ok_live_world_type and tonumber(live_world_type_value) or nil
        if live_world_type == nil and live_world_type_value ~= nil then
            live_world_type = tonumber(reflected_value(live_world_type_value, "value__"))
        end
        if live_world_type == nil or live_world_type < 0 or live_world_type ~= math.floor(live_world_type) then
            write_navigation_result(request, "failed", "live_world_type_unavailable", nil, nil)
            return
        end
        request.liveWorldType = live_world_type
        navigation_fn = safe_get(goto_util, "GotoDragonPos")
        request.navigationMethod = "SceneUtils.TileToWorld(ForceChangeScene.World,serverId)+GoToUtil.GotoDragonPos(serverId,worldId,worldType)+TileToUniqueTile/WorldToUniqueTile"
    else
        local expected_x, expected_y, expected_z, expected_error = expected_normal_world_target(world_pos, request.serverId)
        if expected_error ~= nil then
            write_navigation_result(request, "failed", expected_error, nil, nil)
            return
        end
        request.expectedGotoWorldX = expected_x
        request.expectedGotoWorldY = expected_y
        request.expectedGotoWorldZ = expected_z
        navigation_fn = safe_get(goto_util, "GotoWorldPos")
        request.navigationMethod = "SceneUtils.TileToWorld(ForceChangeScene.World,serverId)+GoToUtil.GotoWorldPos(serverId,worldId)+completionCallbackCurTarget"
    end
    if type(navigation_fn) ~= "function" then
        write_navigation_result(request, "failed", live_world_id > 0 and "goto_dragon_pos_unavailable" or "goto_world_pos_unavailable", nil, nil)
        return
    end
    request.autoLookatCompleted = false
    request.startedClock = runtime_clock()
    pending_navigation = request
    local completed = function()
        if pending_navigation ~= request then return end
        local callback_world = select(1, navigation_world())
        if callback_world ~= nil then
            request.postCanMoving = safe_get(callback_world, "CanMoving")
            request.postEnabled = safe_get(callback_world, "Enabled")
            local post_target = safe_get(callback_world, "CurTarget")
            request.postCurTargetX = tonumber(post_target and safe_get(post_target, "x"))
            request.postCurTargetY = tonumber(post_target and safe_get(post_target, "y"))
            request.postCurTargetZ = tonumber(post_target and safe_get(post_target, "z"))
            local scene_utils = resolve_scene_utils()
            local world_to_tile = scene_utils and safe_get(scene_utils, "WorldToTile") or nil
            if post_target ~= nil and type(world_to_tile) == "function" then
                local ok_tile, post_tile = pcall(world_to_tile, post_target)
                if ok_tile and post_tile ~= nil then
                    request.postTargetTileX = tonumber(safe_get(post_tile, "x"))
                    request.postTargetTileY = tonumber(safe_get(post_tile, "y"))
                end
            end
        end
        request.autoLookatCompleted = true
    end
    local ok_goto
    if request.liveWorldId > 0 then
        ok_goto = pcall(
            navigation_fn,
            world_pos,
            init_zoom,
            focus_time,
            completed,
            request.serverId,
            request.worldId,
            request.liveWorldType)
    else
        ok_goto = pcall(
            navigation_fn,
            world_pos,
            init_zoom,
            focus_time,
            completed,
            request.serverId,
            request.worldId)
    end
    if ok_goto then
        request.effectiveTargetWorldX = tonumber(safe_get(world_pos, "x"))
        request.effectiveTargetWorldY = tonumber(safe_get(world_pos, "y"))
        request.effectiveTargetWorldZ = tonumber(safe_get(world_pos, "z"))
    end
    if not ok_goto then
        if pending_navigation == request then pending_navigation = nil end
        write_navigation_result(request, "failed", request.liveWorldId > 0 and "goto_dragon_pos_failed" or "goto_world_pos_failed", nil, nil)
        return
    end
end

local function pump_navigation(control)
    if pending_navigation == nil then
        local request = read_navigation(control)
        if request ~= nil then
            local ok_begin, begin_error = pcall(begin_navigation, request)
            if not ok_begin then
                write_navigation_result(request, "failed", "navigation_exception:" .. tostring(begin_error), nil, nil)
            end
        end
    end
    if pending_navigation == nil then return end
    local request = pending_navigation
    local ok_verify, verify_error = pcall(function()
        local world, world_error = navigation_world()
        if world == nil then
            write_navigation_result(request, "failed", world_error, nil, nil)
            pending_navigation = nil
            return
        end
        local current_x, current_y = navigation_current_tile(world)
        local verified_unique_matches = false
        local verified_world_matches = false
        local cur_target = safe_get(world, "CurTarget")
        request.verifyCurTargetX = tonumber(cur_target and safe_get(cur_target, "x"))
        request.verifyCurTargetY = tonumber(cur_target and safe_get(cur_target, "y"))
        request.verifyCurTargetZ = tonumber(cur_target and safe_get(cur_target, "z"))
        local scene_utils = resolve_scene_utils()
        local tile_to_unique = scene_utils and safe_get(scene_utils, "TileToUniqueTile") or nil
        local world_to_tile = scene_utils and safe_get(scene_utils, "WorldToTile") or nil
        local world_to_unique = scene_utils and safe_get(scene_utils, "WorldToUniqueTile") or nil
        if request.navigationTile ~= nil and type(tile_to_unique) == "function" then
            local ok_expected_unique, expected_unique = pcall(tile_to_unique, request.navigationTile, request.serverId)
            if ok_expected_unique and expected_unique ~= nil then
                request.expectedUniqueTileX = tonumber(safe_get(expected_unique, "x"))
                request.expectedUniqueTileY = tonumber(safe_get(expected_unique, "y"))
            end
        end
        if request.liveWorldId == 0 and request.navigationWorldPos ~= nil then
            local expected_x, expected_y, expected_z, expected_error = expected_normal_world_target(
                request.navigationWorldPos,
                request.serverId)
            request.expectedGotoError = expected_error
            if expected_error == nil then
                request.expectedGotoWorldX = expected_x
                request.expectedGotoWorldY = expected_y
                request.expectedGotoWorldZ = expected_z
            else
                request.expectedGotoWorldX = nil
                request.expectedGotoWorldY = nil
                request.expectedGotoWorldZ = nil
            end
        end
        if cur_target ~= nil and type(world_to_tile) == "function" then
            local ok_target_tile, target_tile = pcall(world_to_tile, cur_target)
            if ok_target_tile and target_tile ~= nil then
                request.verifiedTargetX = tonumber(safe_get(target_tile, "x"))
                request.verifiedTargetY = tonumber(safe_get(target_tile, "y"))
            end
        end
        if cur_target ~= nil and type(world_to_unique) == "function" then
            local ok_unique_tile, unique_tile = pcall(world_to_unique, cur_target)
            if ok_unique_tile and unique_tile ~= nil then
                request.verifiedUniqueTileX = tonumber(safe_get(unique_tile, "x"))
                request.verifiedUniqueTileY = tonumber(safe_get(unique_tile, "y"))
                verified_unique_matches =
                    request.verifiedUniqueTileX == request.expectedUniqueTileX and
                    request.verifiedUniqueTileY == request.expectedUniqueTileY
            end
        end
        verified_world_matches =
            request.expectedGotoWorldX ~= nil and request.expectedGotoWorldY ~= nil and request.expectedGotoWorldZ ~= nil and
            request.verifyCurTargetX == request.expectedGotoWorldX and
            request.verifyCurTargetY == request.expectedGotoWorldY and
            request.verifyCurTargetZ == request.expectedGotoWorldZ
        local callback_target_matches =
            request.postTargetTileX ~= nil and request.postTargetTileY ~= nil and
            request.postTargetTileX == request.targetX and
            request.postTargetTileY == request.targetY
        local verified_target_matches = request.liveWorldId > 0 and verified_unique_matches or callback_target_matches
        if request.autoLookatCompleted == true and verified_target_matches then
            local lod, server_lod, aoi_sizes, geometry = navigation_geometry()
            request.geometryWorldAvailable = geometry and geometry.worldAvailable or false
            request.geometryPointManagerGetterOk = geometry and geometry.pointManagerGetterOk or false
            request.geometryPointManagerAvailable = geometry and geometry.pointManagerAvailable or false
            request.geometryPointManagerType = geometry and geometry.pointManagerType or nil
            request.geometryLodAvailable = geometry and geometry.lodAvailable or false
            request.geometryServerLodCallOk = geometry and geometry.serverLodCallOk or false
            request.geometryServerLodAvailable = geometry and geometry.serverLodAvailable or false
            request.geometryAoiGetterOk = geometry and geometry.aoiGetterOk or false
            request.geometryAoiArrayAvailable = geometry and geometry.aoiArrayAvailable or false
            request.geometryAoiLength = geometry and geometry.aoiLength or nil
            request.geometryAoiValuesAvailable = geometry and geometry.aoiValuesAvailable or false
            if lod ~= nil and server_lod ~= nil and aoi_sizes ~= nil and #aoi_sizes > 0 then
                write_navigation_result(request, "proven", nil, current_x, current_y, lod, server_lod, aoi_sizes)
                pending_navigation = nil
                return
            end
        end
        if runtime_clock() - request.startedClock >= NAVIGATION_TIMEOUT_SECONDS then
            local error_text = "navigation_target_verification_timeout"
            if verified_target_matches and request.autoLookatCompleted ~= true then
                error_text = "navigation_completion_callback_timeout"
            elseif verified_target_matches then
                error_text = "navigation_geometry_unavailable"
            end
            write_navigation_result(request, "failed", error_text, current_x, current_y)
            pending_navigation = nil
        end
    end)
    if not ok_verify then
        if pending_navigation == request then pending_navigation = nil end
        write_navigation_result(request, "failed", "navigation_exception:" .. tostring(verify_error), nil, nil)
    end
end

local function destroy_message()
    if root_object ~= nil then
        local cs = rawget(_G, "CS")
        local object = cs and cs.UnityEngine and cs.UnityEngine.Object
        if object ~= nil then pcall(function() object.Destroy(root_object) end) end
    end
    root_object = nil
    message_text = nil
    last_ready_session = nil
end

local function find_font_donor(container, text_type)
    local ok, values = pcall(function()
        return container.gameObject:GetComponentsInChildren(text_type, true)
    end)
    if not ok or values == nil then return nil end
    local length = tonumber(safe_get(values, "Length") or safe_get(values, "Count")) or 0
    for index = 0, length - 1 do
        local donor = safe_get(values, index)
        if donor ~= nil and donor ~= message_text then
            local font = safe_get(donor, "font")
            if font ~= nil then return donor end
        end
    end
    return nil
end

local function ensure_message()
    local cs = rawget(_G, "CS")
    if cs == nil or cs.UnityEngine == nil then return false, "unity_unavailable" end
    local game_object = cs.UnityEngine.GameObject
    local ui_root = game_object.Find("GameFramework/UI")
    if ui_root == nil then return false, "ui_root_unavailable" end
    local container = ui_root.transform:Find("UIContainer")
    if container == nil then return false, "ui_container_unavailable" end

    if root_object ~= nil and message_text ~= nil then
        local ok = pcall(function()
            root_object.transform:SetAsLastSibling()
            message_text.text = MESSAGE
            root_object:SetActive(true)
        end)
        if ok then return true, nil end
        destroy_message()
    end

    local rect_type = typeof(cs.UnityEngine.RectTransform)
    local canvas_type = typeof(cs.UnityEngine.Canvas)
    local text_type = typeof(cs.TextMeshProUGUIEx)
    local donor = find_font_donor(container, text_type)
    if donor == nil then return false, "text_font_donor_unavailable" end

    local created = nil
    local ok, err = pcall(function()
        created = game_object(OBJECT_NAME, rect_type)
        local rect = created:GetComponent(rect_type)
        rect:SetParent(container, false)
        rect:Set_anchorMin(0, 0)
        rect:Set_anchorMax(1, 1)
        rect:Set_offsetMin(0, 0)
        rect:Set_offsetMax(0, 0)
        created.layer = cs.UnityEngine.LayerMask.NameToLayer("UI")

        local canvas = created:AddComponent(canvas_type)
        canvas.overrideSorting = true
        canvas.sortingOrder = 32760

        local text_go = game_object("Message", rect_type)
        text_go.layer = created.layer
        local text_rect = text_go:GetComponent(rect_type)
        text_rect:SetParent(created.transform, false)
        text_rect:Set_anchorMin(0.5, 1)
        text_rect:Set_anchorMax(0.5, 1)
        pcall(function() text_rect:Set_pivot(0.5, 1) end)
        text_rect.sizeDelta = cs.UnityEngine.Vector2(520, 44)
        text_rect.anchoredPosition = cs.UnityEngine.Vector2(0, -18)

        local text = text_go:AddComponent(text_type)
        text.font = donor.font
        local donor_material = safe_get(donor, "fontSharedMaterial")
        if donor_material ~= nil then pcall(function() text.fontSharedMaterial = donor_material end) end
        text.text = MESSAGE
        text.fontSize = 22
        pcall(function() text.color = cs.UnityEngine.Color(1, 1, 1, 1) end)
        pcall(function() text.alignment = cs.TMPro.TextAlignmentOptions.Center end)
        pcall(function() text.raycastTarget = false end)
        created.transform:SetAsLastSibling()
        created:SetActive(true)
        root_object = created
        message_text = text
    end)
    if not ok then
        if created ~= nil then
            pcall(function() cs.UnityEngine.Object.Destroy(created) end)
        end
        root_object = nil; message_text = nil
        return false, "message_create_failed:" .. tostring(err)
    end
    return message_text ~= nil and safe_get(message_text, "font") ~= nil, nil
end

local function observe_game_connection()
    local cs = rawget(_G, "CS")
    local entry = rawget(_G, "GameEntry")
    if entry == nil and cs ~= nil then entry = safe_get(cs, "GameEntry") end
    if entry == nil then return { observed = false } end

    local network = safe_get(entry, "Network")
    local data = safe_get(entry, "Data")
    local player = data and safe_get(data, "Player") or nil
    if network == nil or player == nil then return { observed = false } end

    local logged_in = safe_get(network, "Logined")
    if logged_in == nil then logged_in = reflected_value(network, "<Logined>k__BackingField") end
    local connected = safe_get(network, "IsConnected")
    local connecting = safe_get(network, "IsConnecting")
    if connected == nil or connecting == nil then
        local proxy = reflected_value(network, "m_proxy")
        if proxy == nil then
            connected = connected == nil and false or connected
            connecting = connecting == nil and false or connecting
        elseif reflected_type_name(proxy) == "GameKit.Base.NetRawProxy" then
            if connecting == nil then
                local status_value = reflected_value(proxy, "<Status>k__BackingField")
                local status = tonumber(status_value)
                if status == nil and status_value ~= nil then
                    status = tonumber(reflected_value(status_value, "value__"))
                end
                if status ~= nil then connecting = status == 1 end
            end
            if connected == nil then
                local conn = reflected_value(proxy, "conn")
                if conn == nil then
                    connected = false
                else
                    local socket = reflected_value(conn, "m_socket")
                    if socket == nil then
                        connected = false
                    else
                        connected = safe_get(socket, "Connected")
                        if connected == nil then connected = reflected_value(socket, "Connected") end
                    end
                end
            end
        end
    end
    local ok_uid, uid = call(player, "GetUid")
    local ok_server, server_id = call(player, "GetCurServerId")
    local world_pos = safe_get(player, "PlayerWorldPointId")
    if type(logged_in) ~= "boolean" or type(connected) ~= "boolean" or
       type(connecting) ~= "boolean" or not ok_uid or not ok_server then
        return { observed = false }
    end
    return {
        observed = true,
        ready = player ~= nil,
        loggedIn = logged_in,
        connected = connected,
        connecting = connecting,
        gameUid = type(uid) == "string" and uid or nil,
        serverId = tonumber(server_id),
        worldPos = tonumber(world_pos),
    }
end

local RECOVERY_WINDOWS = {
    { windowName = "UIForceUpdateTip", reason = "forceUpdate", updateDetected = true },
    { windowName = "UICrossDisconnect", reason = "crossDisconnect", updateDetected = false },
    { windowName = "UIDisconnect", reason = "disconnect", updateDetected = false },
    { windowName = "UIExitGameTip", reason = "exitPrompt", updateDetected = false },
}

local function observe_recovery_request()
    if pending_recovery_signal ~= nil and pending_recovery_signal_until_clock ~= nil and runtime_clock() > pending_recovery_signal_until_clock then
        pending_recovery_signal = nil
        pending_recovery_signal_until_clock = nil
    end
    if pending_recovery_signal ~= nil then
        return {
            observed = true, confirmed = true, ambiguous = false,
            windowName = pending_recovery_signal.windowName,
            reason = pending_recovery_signal.reason,
            updateDetected = pending_recovery_signal.updateDetected == true,
        }
    end
    local manager = rawget(_G, "UIManager")
    if manager == nil then return { observed = false } end
    local instance = manager
    if type(safe_get(manager, "GetInstance")) == "function" then
        local ok_instance, value = call(manager, "GetInstance")
        if not ok_instance or value == nil then return { observed = false } end
        instance = value
    end
    local match = nil
    local count = 0
    for _, spec in ipairs(RECOVERY_WINDOWS) do
        local ok_open, is_open = call(instance, "IsWindowOpen", spec.windowName)
        if not ok_open or type(is_open) ~= "boolean" then return { observed = false } end
        if is_open then match = spec; count = count + 1 end
    end
    if count ~= 1 then return { observed = true, ambiguous = count > 1 } end
    return {
        observed = true, confirmed = false, ambiguous = false,
        windowName = match.windowName, reason = match.reason,
        updateDetected = match.updateDetected,
    }
end

local function write_heartbeat(now, ready, error)
    local clock = runtime_clock()
    if clock - last_heartbeat_clock < 0.75 and pending_recovery_signal == nil then return end
    last_heartbeat_clock = clock
    local game = observe_game_connection()
    local recovery = observe_recovery_request()
    write_json(heartbeat_path, {
        schemaVersion = 1,
        bridgeVersion = M.VERSION,
        profileId = active and active.profileId or nil,
        sessionId = active and active.sessionId or nil,
        challenge = active and active.challenge or nil,
        gamePid = active and active.gamePid or nil,
        updatedAt = now,
        ready = ready == true,
        messageVisible = ready == true,
        messageText = ready == true and MESSAGE or nil,
        error = error,
        gameStateObserved = game.observed == true,
        gameReady = game.ready,
        loggedIn = game.loggedIn,
        connected = game.connected,
        connecting = game.connecting,
        gameUid = game.gameUid,
        serverId = game.serverId,
        worldPos = game.worldPos,
        recoveryObserved = recovery.observed == true,
        recoveryConfirmed = recovery.confirmed == true,
        recoveryAmbiguous = recovery.ambiguous == true,
        recoveryWindowName = recovery.windowName,
        recoveryReason = recovery.reason,
        recoveryUpdateDetected = recovery.updateDetected == true,
    })
end

local RECOVERY_ACTION_HOOKS = {
    { module = "UI.UIForceUpdateTip.Controller.UIForceUpdateTipCtrl", className = "UIForceUpdateTipCtrl", method = "CloseSelf", windowName = "UIForceUpdateTip", reason = "forceUpdate", updateDetected = true },
    { module = "UI.UIDisconnect.View.UIDisconnectView", className = "UIDisconnectView", method = "GotoLoadingView", windowName = "UIDisconnect", reason = "disconnect", updateDetected = false },
    { module = "UI.UIDisconnect.View.UICrossDisconnectView", className = "UICrossDisconnectView", method = "OnReconnectTimeOut", windowName = "UICrossDisconnect", reason = "crossDisconnect", updateDetected = false },
    { module = "UI.UIExitGameTip.View.UIExitGameTipView", className = "UIExitGameTipView", method = "ExitGame", windowName = "UIExitGameTip", reason = "exitPrompt", updateDetected = false },
}

local function recovery_window_is_open(window_name)
    local manager = rawget(_G, "UIManager")
    if manager == nil then return false end
    local instance = manager
    if type(safe_get(manager, "GetInstance")) == "function" then
        local ok_instance, value = call(manager, "GetInstance")
        if not ok_instance or value == nil then return false end
        instance = value
    end
    local ok_open, is_open = call(instance, "IsWindowOpen", window_name)
    return ok_open and is_open == true
end

-- CURRENT-CLIENT IMPLEMENTATION POLICY: the protected original producer of
-- game.recovery_requested is unavailable.  Confirm only source-backed current
-- client actions that actually enter reload/quit or cross reconnect timeout;
-- mere window visibility remains unconfirmed.
local function confirm_recovery_action(spec)
    local now = tonumber(os.time()) or 0
    if active == nil or not lease_is_fresh(active, now) then return end
    if not recovery_window_is_open(spec.windowName) then return end
    pending_recovery_signal = {
        windowName = spec.windowName,
        reason = spec.reason,
        updateDetected = spec.updateDetected == true,
    }
    -- IMPLEMENTATION POLICY: retain a confirmed action for the host heartbeat
    -- freshness window so the 1-second monitor cannot miss an immediate reload.
    pending_recovery_signal_until_clock = runtime_clock() + 5.0
    local ready = root_object ~= nil and message_text ~= nil and last_ready_session == active.sessionId
    write_heartbeat(now, ready, nil)
end

local function install_recovery_action_hooks()
    local loaded = package and package.loaded or nil
    for _, spec in ipairs(RECOVERY_ACTION_HOOKS) do
        local key = spec.module .. ":" .. spec.method
        if not recovery_action_hooks[key] then
            local target = loaded and loaded[spec.module] or nil
            if target == nil then target = rawget(_G, spec.className) end
            local original = safe_get(target, spec.method)
            if type(original) == "function" then
                local hook_spec, hook_original = spec, original
                local ok = pcall(function()
                    target[hook_spec.method] = function(...)
                        confirm_recovery_action(hook_spec)
                        return hook_original(...)
                    end
                end)
                if ok then recovery_action_hooks[key] = true end
            end
        end
    end
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    local control = read_control()
    if control == nil or not lease_is_fresh(control, now) then
        active = control
        pending_navigation = nil
        pending_world_ready = nil
        pending_server_jump = nil
        pending_march_follow = nil
        abandon_truck_quick_rob()
        destroy_message()
        write_heartbeat(now, false, control == nil and "control_unavailable" or "host_lease_stale")
        return true
    end

    if active == nil or active.sessionId ~= control.sessionId or active.challenge ~= control.challenge or
       active.profileId ~= control.profileId or active.gamePid ~= control.gamePid then
        destroy_message()
        pending_navigation = nil
        pending_world_ready = nil
        pending_server_jump = nil
        pending_march_follow = nil
        abandon_truck_quick_rob()
        active = control
    end

    install_recovery_action_hooks()
    pump_world_ready(control)
    pump_server_jump(control)
    pump_march_follow(control)
    pump_truck_quick_rob(control)
    pump_navigation(control)
    local rendered, render_error = ensure_message()
    if rendered then
        if last_ready_session ~= active.sessionId then
            write_json(ready_path, {
                schemaVersion = 1,
                bridgeVersion = M.VERSION,
                profileId = active.profileId,
                sessionId = active.sessionId,
                challenge = active.challenge,
                gamePid = active.gamePid,
                readyAt = now,
                ready = true,
                messageVisible = true,
                messageText = MESSAGE,
                renderPath = "GameFramework/UI/UIContainer/LWBridgeOverviewReady/Message",
                registrationMethod = registration_method,
            })
            last_ready_session = active.sessionId
        end
        write_heartbeat(now, true, nil)
    else
        write_heartbeat(now, false, render_error)
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
            local register_repeat = timer and safe_get(timer, "RegisterTimerRepeat")
            if type(register_repeat) == "function" then
                local ok_timer, handle = pcall(register_repeat, timer, 0.25, 0.25, update_callback)
                if not ok_timer then ok_timer, handle = pcall(register_repeat, 0.25, 0.25, update_callback) end
                if ok_timer then timer_handle = handle; registration_method = "GameEntry.Timer.RegisterTimerRepeat"; break end
            end
        end
    end
    M.Pump()
    return registration_method ~= nil
end

-- Registration is deliberately lazy. LuaEntry is loaded before all current-client
-- update/timer surfaces are guaranteed to exist; the preserved LuaEntry lifecycle
-- wrapper retries Register after original lifecycle calls without making module load
-- depend on early UpdateManager availability.
return M
