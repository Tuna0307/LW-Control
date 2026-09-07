-- Current-game loaded WorldPointManager read-only snapshot builder.
--
-- This module never sends a network request, moves the camera, or mutates the
-- point manager. The private/protected _pointInfos read follows the reflection
-- pattern recovered from original LWC2MapScanner.lua v108.

local M = {
    VERSION = "lwcontrol-world-state-probe-1",
    SCHEMA_VERSION = 1,
    MODE = "state",
    SOURCE = "WorldPointManager",
}

local MAX_POINTS = 50000
local REFLECTION_FLAGS = 52 -- Instance | Public | NonPublic

local function fail(message)
    return nil, message
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

local function reflected_collection(target, name)
    local direct = safe_get(target, name)
    if direct ~= nil then return direct end
    local ok_type, reflected_type = pcall(function() return target:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local ok_field, field = pcall(function()
        return reflected_type:GetField(name, REFLECTION_FLAGS)
    end)
    if not ok_field or field == nil then return nil end
    local ok_value, value = pcall(function() return field:GetValue(target) end)
    return ok_value and value or nil
end

local function each_value(collection, limit, consume)
    if collection == nil then return 0 end
    local count = 0
    local function accept(item)
        if item == nil or count >= limit then return count < limit end
        local value = safe_get(item, "Value") or item
        count = count + 1
        return consume(value) ~= false and count < limit
    end

    local values = safe_get(collection, "Values")
    if values ~= nil and values ~= collection then
        return each_value(values, limit, consume)
    end

    local length = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if length ~= nil then
        if length > limit then return -1 end
        for index = 0, length - 1 do
            local item = safe_get(collection, index)
            if item == nil then item = safe_get(collection, index + 1) end
            if accept(item) == false then break end
        end
        if count > 0 or length == 0 then return count end
    end

    local ok_enum, enumerator = call(collection, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        while count < limit do
            local ok_move, moved = call(enumerator, "MoveNext")
            if not ok_move or moved ~= true then break end
            if accept(safe_get(enumerator, "Current")) == false then break end
        end
        return count
    end

    if type(collection) == "table" then
        for _, item in pairs(collection) do
            if accept(item) == false then break end
        end
    end
    return count
end

local function integer_field(target, names, required)
    for _, name in ipairs(names) do
        local value = safe_get(target, name)
        local numeric = tonumber(value)
        if numeric ~= nil and numeric == math.floor(numeric) then
            return numeric
        end
    end
    if required then return nil end
    return 0
end

local function point_snapshot(info)
    if info == nil then return fail("_pointInfos contains a nil point") end
    local id = integer_field(info, {
        "pointIndex", "PointIndex", "mainIndex", "MainIndex", "pointId", "PointId",
    }, true)
    local point_type = integer_field(info, { "pointType", "PointType" }, true)
    if id == nil or id < 0 then return fail("world point is missing a non-negative point identity") end
    if point_type == nil or point_type < 0 then return fail("world point is missing a non-negative point type") end

    local uuid = integer_field(info, { "uuid", "Uuid" }, false)
    local server_id = integer_field(info, { "serverId", "ServerId" }, false)
    local src_server_id = integer_field(info, { "srcServerId", "SrcServerId" }, false)
    local world_id = integer_field(info, { "worldId", "WorldId" }, false)
    if uuid < 0 or server_id < 0 or src_server_id < 0 or world_id < 0 then
        return fail("world point contains a negative routing or UUID value")
    end
    return {
        id = id,
        pointType = point_type,
        uuid = uuid,
        serverId = server_id,
        srcServerId = src_server_id,
        worldId = world_id,
    }
end

function M.Build(point_manager, capture_id, captured_at)
    if point_manager == nil then return fail("WorldPointManager unavailable") end
    if type(capture_id) ~= "string" or capture_id == ""
        or type(captured_at) ~= "string" or captured_at == "" then
        return fail("world-map capture metadata is incomplete")
    end

    local collection = reflected_collection(point_manager, "_pointInfos")
    if collection == nil then
        return fail("WorldPointManager._pointInfos reflection unavailable")
    end
    local expected_count = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if expected_count ~= nil and (expected_count < 0 or expected_count > MAX_POINTS) then
        return fail("WorldPointManager._pointInfos count is outside the bounded snapshot limit")
    end

    local points = {}
    local identities = {}
    local enumeration_error = nil
    local scanned = each_value(collection, MAX_POINTS + 1, function(info)
        local point, point_error = point_snapshot(info)
        if point == nil then enumeration_error = point_error; return false end
        local identity = tostring(point.worldId) .. ":" .. tostring(point.serverId)
            .. ":" .. tostring(point.id)
        if identities[identity] then
            enumeration_error = "duplicate world/server/point identity"
            return false
        end
        identities[identity] = true
        points[#points + 1] = point
        return true
    end)
    if enumeration_error ~= nil then return fail(enumeration_error) end
    if scanned < 0 or scanned > MAX_POINTS or #points > MAX_POINTS then
        return fail("loaded world-point enumeration exceeded the bounded snapshot limit")
    end
    if expected_count ~= nil and scanned ~= expected_count then
        return fail("loaded world-point enumeration did not match _pointInfos.Count")
    end

    table.sort(points, function(left, right)
        if left.worldId ~= right.worldId then return left.worldId < right.worldId end
        if left.serverId ~= right.serverId then return left.serverId < right.serverId end
        return left.id < right.id
    end)

    return {
        schemaVersion = M.SCHEMA_VERSION,
        mode = M.MODE,
        source = M.SOURCE,
        captureId = capture_id,
        capturedAt = captured_at,
        heartbeat = {
            probeVersion = M.VERSION,
            observedAt = captured_at,
        },
        points = points,
    }
end

return M
