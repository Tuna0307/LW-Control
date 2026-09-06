"""Build/verify a separate encrypted loaded-world snapshot probe package.

The generated candidate preserves the current official LuaEntry, installs the
already-proven loader wrapper, and replaces only LWControlProbe with a bounded
World-scene transition plus WorldPointManager snapshot writer. Preparation
never changes installed files.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

try:
    from .extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from .install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        PROBE_ENTRY,
        _entry_map,
        _entry_source,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )
except ImportError:
    from extract_lenc_v3 import decode_lenc_bytes, derive_xlua_key_nonce, encode_lenc_bytes
    from install_loader_probe import (
        InstallRefused,
        LUA_ENTRY,
        ORIGINAL_LUA_ENTRY,
        PROBE_ENTRY,
        _entry_map,
        _entry_source,
        _xlua_path,
        crc32_file,
        discover_paths,
        preflight,
        read_lwlf,
        write_lwlf,
    )


PROBE_VERSION = "lwcontrol-world-loader-probe-2"


def _builder_source() -> str:
    return Path(__file__).with_name("current_world_map_snapshot_probe.lua").read_text(
        encoding="utf-8"
    )


def _probe_source() -> bytes:
    source = r'''local M = { VERSION = "__PROBE_VERSION__" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
local heartbeat_path = root .. [[\world-map-loader-probe.json]]
local snapshot_path = root .. [[\world-map-snapshot.json]]
local status_path = root .. [[\world-map-snapshot-status.json]]
local last_write = 0
local transition_requested = false
local transition_requested_at = 0
local transition_invocations = 0
local transition_button_path = nil
local transition_method = nil
local transition_state = "idle"
local stable_point_count = nil
local stable_since = 0
local STABLE_SECONDS = 2
local MAX_GAME_OBJECTS = 20000
local pump_count = 0
local registration_method = nil
local capture_complete = false
local update_callback = nil
local timer_handle = nil
local observed_world_source = nil
local observed_manager_source = nil
local observed_world_type = nil
local observed_world_members = nil
local observed_manager_candidates = nil
local manager_diagnostics_at = 0
local fallback_all_main_count = nil
local fallback_screen_count = nil
local fallback_sample = nil
local city_visible = false
local world_visible = false
local scene_scanned = 0
local scene_state = "unknown"
local current_scene_id = nil
local city_scene_id = nil
local world_scene_id = nil
local aoi_block_size = nil
local aoi_block_count = nil
local aoi_lod = nil
local aoi_server_lod = nil
local aoi_req_pos = nil
local aoi_last_lb = nil
local aoi_last_rt = nil
local aoi_once_max_request_count = nil
local aoi_first_time_request = nil
local aoi_battle_field_first = nil
local transform_path = nil

local builder = (function()
__SNAPSHOT_BUILDER__
end)()

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

local function update_scene_identity()
    local cs = rawget(_G, "CS")
    local scene_manager = cs and safe_get(cs, "SceneManager")
    local scene_ids = rawget(_G, "SceneManagerSceneID")
        or (cs and safe_get(cs, "SceneManagerSceneID"))
    local current = scene_manager and safe_get(scene_manager, "CurrSceneID")
    local city = scene_ids and safe_get(scene_ids, "City")
    local world = scene_ids and safe_get(scene_ids, "World")
    current_scene_id = current ~= nil and tostring(current) or nil
    city_scene_id = city ~= nil and tostring(city) or nil
    world_scene_id = world ~= nil and tostring(world) or nil
    if current ~= nil and world ~= nil and current == world then
        scene_state = "world"
    elseif current ~= nil and city ~= nil and current == city then
        scene_state = "city"
    else
        scene_state = "unknown"
    end
    return scene_state
end

local function json_string(value)
    if value == nil then return "null" end
    value = tostring(value)
    value = string.gsub(value, '[%z\1-\31\\"]', function(ch)
        if ch == '"' then return '\\"' end
        if ch == '\\' then return '\\\\' end
        if ch == '\b' then return '\\b' end
        if ch == '\f' then return '\\f' end
        if ch == '\n' then return '\\n' end
        if ch == '\r' then return '\\r' end
        if ch == '\t' then return '\\t' end
        return string.format('\\u%04x', string.byte(ch))
    end)
    return '"' .. value .. '"'
end

local function write_heartbeat(now)
    if now == last_write then return true end
    local file = io.open(heartbeat_path, "wb")
    if not file then return false end
    file:write('{"version":', json_string(M.VERSION), ',"loaded":true,"updated_at":',
        tostring(now), '}')
    file:close()
    last_write = now
    return true
end

local function write_status(now, state, err)
    local file = io.open(status_path, "wb")
    if not file then return false end
    file:write('{"probeVersion":', json_string(builder.VERSION),
        ',"state":', json_string(state), ',"updatedAt":', tostring(now),
        ',"error":', json_string(err),
        ',"transition_requested":', tostring(transition_requested),
        ',"transition_requested_at":', tostring(transition_requested_at),
        ',"transition_invocations":', tostring(transition_invocations),
        ',"transition_button_path":', json_string(transition_button_path),
        ',"transition_method":', json_string(transition_method),
        ',"transition_state":', json_string(transition_state),
        ',"stable_point_count":', tostring(stable_point_count or -1),
        ',"stable_since":', tostring(stable_since),
        ',"pump_count":', tostring(pump_count),
        ',"registration_method":', json_string(registration_method),
        ',"world_source":', json_string(observed_world_source),
        ',"manager_source":', json_string(observed_manager_source),
        ',"world_type":', json_string(observed_world_type),
        ',"world_members":', json_string(observed_world_members),
        ',"manager_candidates":', json_string(observed_manager_candidates),
        ',"fallback_all_main_count":', tostring(fallback_all_main_count or -1),
        ',"fallback_screen_count":', tostring(fallback_screen_count or -1),
        ',"fallback_sample":', json_string(fallback_sample),
        ',"city_visible":', tostring(city_visible),
        ',"world_visible":', tostring(world_visible),
        ',"scene_scanned":', tostring(scene_scanned),
        ',"scene_state":', json_string(scene_state),
        ',"current_scene_id":', json_string(current_scene_id),
        ',"city_scene_id":', json_string(city_scene_id),
        ',"world_scene_id":', json_string(world_scene_id),
        ',"aoi_block_size":', tostring(aoi_block_size or -1),
        ',"aoi_block_count":', tostring(aoi_block_count or -1),
        ',"aoi_lod":', tostring(aoi_lod or -1),
        ',"aoi_server_lod":', tostring(aoi_server_lod or -1),
        ',"aoi_req_pos":', json_string(aoi_req_pos),
        ',"aoi_last_lb":', json_string(aoi_last_lb),
        ',"aoi_last_rt":', json_string(aoi_last_rt),
        ',"aoi_once_max_request_count":', tostring(aoi_once_max_request_count or -1),
        ',"aoi_first_time_request":', tostring(aoi_first_time_request == true),
        ',"aoi_battle_field_first":', tostring(aoi_battle_field_first == true), '}')
    file:close()
    return true
end

local function serialize(snapshot)
    local parts = {
        '{"schemaVersion":', tostring(snapshot.schemaVersion),
        ',"mode":', json_string(snapshot.mode),
        ',"source":', json_string(snapshot.source),
        ',"captureId":', json_string(snapshot.captureId),
        ',"capturedAt":', json_string(snapshot.capturedAt),
        ',"heartbeat":{"probeVersion":', json_string(snapshot.heartbeat.probeVersion),
        ',"observedAt":', json_string(snapshot.heartbeat.observedAt), '}',
        ',"points":['
    }
    for index, point in ipairs(snapshot.points) do
        if index > 1 then parts[#parts + 1] = ',' end
        parts[#parts + 1] = '{"id":' .. tostring(point.id)
        parts[#parts + 1] = ',"pointType":' .. tostring(point.pointType)
        parts[#parts + 1] = ',"uuid":' .. tostring(point.uuid)
        parts[#parts + 1] = ',"serverId":' .. tostring(point.serverId)
        parts[#parts + 1] = ',"srcServerId":' .. tostring(point.srcServerId)
        parts[#parts + 1] = ',"worldId":' .. tostring(point.worldId) .. '}'
    end
    parts[#parts + 1] = ']}'
    return table.concat(parts)
end

local function is_runtime_world_scene(candidate)
    if candidate == nil then return false end
    local ok_type, reflected_type = pcall(function() return candidate:GetType() end)
    if not ok_type or reflected_type == nil then return false end
    local name = tostring(safe_get(reflected_type, "Name") or "")
    local full_name = tostring(safe_get(reflected_type, "FullName") or name)
    return name == "WorldScene" or string.sub(full_name, -10) == "WorldScene"
end

local function find_active_world_scene()
    local cs = rawget(_G, "CS")
    local cs_scene_manager = cs and safe_get(cs, "SceneManager")
    local world = cs_scene_manager and safe_get(cs_scene_manager, "World")
    if is_runtime_world_scene(world) then return world, "CS.SceneManager.World" end

    local scene_manager = rawget(_G, "SceneManager")
    world = scene_manager and safe_get(scene_manager, "World")
    if is_runtime_world_scene(world) then return world, "SceneManager.World" end

    local resources = safe_get(safe_get(cs, "UnityEngine"), "Resources")
    local world_type = safe_get(cs, "WorldScene")
    local typeof_fn = rawget(_G, "typeof")
    if resources == nil or world_type == nil or type(typeof_fn) ~= "function" then
        return nil, "world_scene_reflection_unavailable"
    end
    local ok_type, reflected_type = pcall(typeof_fn, world_type)
    if not ok_type or reflected_type == nil then return nil, "world_scene_type_unavailable" end
    local ok_objects, objects = static_call(resources, "FindObjectsOfTypeAll", reflected_type)
    if not ok_objects or objects == nil then return nil, "world_scene_objects_unavailable" end
    local count = tonumber(safe_get(objects, "Length") or safe_get(objects, "Count")) or 0
    for index = 0, math.min(count - 1, 31) do
        local candidate = safe_get(objects, index) or safe_get(objects, index + 1)
        if candidate ~= nil and safe_get(candidate, "isActiveAndEnabled") == true
            and is_runtime_world_scene(candidate) then
            return candidate, "Resources.FindObjectsOfTypeAll"
        end
    end
    return nil, "world_scene_not_found"
end

local function reflected_private_collection(target, name)
    if target == nil then return nil end
    local direct = safe_get(target, name)
    if direct ~= nil then return direct end
    local ok_type, reflected_type = pcall(function() return target:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local ok_field, field = pcall(function() return reflected_type:GetField(name, 52) end)
    if not ok_field or field == nil then return nil end
    local ok_value, value = pcall(function() return field:GetValue(target) end)
    return ok_value and value or nil
end

local function recovered_manager_candidate()
    local seen = {}
    local function accept(label, value)
        if value == nil then return nil, nil end
        local key = tostring(value)
        if seen[key] then return nil, nil end
        seen[key] = true
        if reflected_private_collection(value, "_pointInfos") ~= nil then
            return value, label
        end
        return nil, nil
    end
    local function inspect(label, holder)
        local manager, source = accept(label, holder)
        if manager ~= nil then return manager, source end
        for _, name in ipairs({
            "WorldPointManager", "worldPointManager", "PointManager", "pointManager",
            "Scene", "scene", "CurScene", "curScene", "CurrentScene", "currentScene",
        }) do
            manager, source = accept(label .. "." .. name, safe_get(holder, name))
            if manager ~= nil then return manager, source end
        end
        return nil, nil
    end

    for _, name in ipairs({ "CurScene", "SceneInterface", "WorldPointManager", "WorldScene", "SceneManager", "GameEntry" }) do
        local manager, source = inspect("global:" .. name, rawget(_G, name))
        if manager ~= nil then return manager, source end
    end
    local scene_manager = rawget(_G, "SceneManager")
    local manager, source = inspect(
        "global:SceneManager.World", scene_manager and safe_get(scene_manager, "World"))
    if manager ~= nil then return manager, source end

    local active_world, active_world_source = find_active_world_scene()
    manager, source = inspect("active:" .. tostring(active_world_source), active_world)
    if manager ~= nil then return manager, source end

    local cs = rawget(_G, "CS")
    if cs ~= nil then
        local game_entry = safe_get(cs, "GameEntry")
        for _, item in ipairs({
            { "CS.GameEntry", game_entry },
            { "CS.GameEntry.Scene", safe_get(game_entry, "Scene") },
            { "CS.GameEntry.SceneComponent", safe_get(game_entry, "SceneComponent") },
        }) do
            manager, source = inspect(item[1], item[2])
            if manager ~= nil then return manager, source end
        end
    end
    return nil, nil
end

local function runtime_type_name(value)
    if value == nil then return nil end
    local ok_type, reflected_type = pcall(function() return value:GetType() end)
    if not ok_type or reflected_type == nil then return tostring(value) end
    local ok_name, name = pcall(function() return reflected_type.FullName or reflected_type.Name end)
    return ok_name and tostring(name or reflected_type) or tostring(reflected_type)
end

local function reflected_world_member_summary(world)
    if world == nil then return nil end
    local ok_type, reflected_type = pcall(function() return world:GetType() end)
    if not ok_type or reflected_type == nil then return nil end
    local ok_members, members = pcall(function() return reflected_type:GetMembers(52) end)
    if not ok_members or members == nil then return nil end
    local names, seen = {}, {}
    local count = tonumber(safe_get(members, "Length") or safe_get(members, "Count")) or 0
    for index = 0, math.min(count - 1, 511) do
        local member = safe_get(members, index) or safe_get(members, index + 1)
        local name = tostring(safe_get(member, "Name") or "")
        local lower = string.lower(name)
        if name ~= "" and (string.find(lower, "point", 1, true)
            or string.find(lower, "manager", 1, true)
            or string.find(lower, "world", 1, true)) and not seen[name] then
            seen[name] = true
            names[#names + 1] = name
            if #names >= 96 then break end
        end
    end
    table.sort(names)
    return table.concat(names, "|")
end

local function recovered_manager_summary()
    local entries, seen = {}, {}
    local function add(label, value)
        if value == nil then return end
        local key = tostring(value)
        if seen[key] then return end
        local has_main = type(safe_get(value, "GetAllMainBaseList")) == "function"
        local has_screen = type(safe_get(value, "GetMainByScreen")) == "function"
        local has_alliance = safe_get(value, "alliancePointsInfos") ~= nil
        local points = reflected_private_collection(value, "_pointInfos")
        if not has_main and not has_screen and not has_alliance and points == nil then return end
        seen[key] = true
        local point_count = points and tonumber(safe_get(points, "Count") or safe_get(points, "Length")) or -1
        entries[#entries + 1] = label .. "@" .. tostring(runtime_type_name(value) or "?")
            .. ":main=" .. tostring(has_main) .. ":screen=" .. tostring(has_screen)
            .. ":alliance=" .. tostring(has_alliance) .. ":points=" .. tostring(point_count)
    end
    local function inspect(label, holder)
        add(label, holder)
        for _, name in ipairs({
            "WorldPointManager", "worldPointManager", "PointManager", "pointManager",
            "Scene", "scene", "CurScene", "curScene", "CurrentScene", "currentScene",
        }) do
            add(label .. "." .. name, safe_get(holder, name))
        end
    end
    for _, name in ipairs({ "CurScene", "SceneInterface", "WorldPointManager", "WorldScene", "SceneManager", "GameEntry" }) do
        inspect("global:" .. name, rawget(_G, name))
    end
    local scene_manager = rawget(_G, "SceneManager")
    inspect("global:SceneManager.World", scene_manager and safe_get(scene_manager, "World"))
    local active_world, active_world_source = find_active_world_scene()
    inspect("active:" .. tostring(active_world_source), active_world)
    local cs = rawget(_G, "CS")
    if cs ~= nil then
        local game_entry = safe_get(cs, "GameEntry")
        inspect("CS.GameEntry", game_entry)
        inspect("CS.GameEntry.Scene", safe_get(game_entry, "Scene"))
        inspect("CS.GameEntry.SceneComponent", safe_get(game_entry, "SceneComponent"))
    end
    table.sort(entries)
    return table.concat(entries, " || ")
end

local function collection_probe(collection)
    if collection == nil then return -1, nil end
    local count = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    local sample = nil
    if count ~= nil and count > 0 then
        sample = safe_get(collection, 0) or safe_get(collection, 1)
    else
        local ok_enum, enumerator = call(collection, "GetEnumerator")
        if ok_enum and enumerator ~= nil then
            local ok_move, moved = call(enumerator, "MoveNext")
            if ok_move and moved == true then sample = safe_get(enumerator, "Current") end
        end
    end
    sample = safe_get(sample, "Value") or sample
    if sample == nil then return count or 0, nil end
    local id = safe_get(sample, "pointIndex") or safe_get(sample, "PointIndex")
        or safe_get(sample, "mainIndex") or safe_get(sample, "MainIndex")
        or safe_get(sample, "pointId") or safe_get(sample, "PointId")
    local point_type = safe_get(sample, "pointType") or safe_get(sample, "PointType")
    local uuid = safe_get(sample, "uuid") or safe_get(sample, "Uuid")
    return count or -2, "id=" .. tostring(id) .. ":type=" .. tostring(point_type)
        .. ":uuid=" .. tostring(uuid) .. ":runtime=" .. tostring(runtime_type_name(sample))
end

local function root_game_object(game_object)
    local transform = safe_get(game_object, "transform")
    if transform == nil then return game_object end
    for _ = 1, 32 do
        local parent = safe_get(transform, "parent")
        if parent == nil then break end
        if string.lower(tostring(safe_get(parent, "name") or "")) == "dynamicobj" then break end
        transform = parent
    end
    return safe_get(transform, "gameObject") or game_object
end

local function unity_kind(blob)
    local lower = string.lower(tostring(blob or ""))
    if string.find(lower, "gameframework/ui/", 1, true)
        or string.find(lower, "uicontainer", 1, true)
        or string.find(lower, "/canvas", 1, true)
        or string.find(lower, "soundcomponent", 1, true)
        or string.find(lower, "manager", 1, true)
        or string.find(lower, "effect", 1, true)
        or string.find(lower, "eff_", 1, true) then
        return nil
    end
    if (string.find(lower, "building_", 1, true) and string.find(lower, "_world(clone)", 1, true))
        or string.find(lower, "playerbuilding", 1, true)
        or string.find(lower, "worldbuilding", 1, true)
        or string.find(lower, "mainbase(clone)", 1, true) then return "player_base" end
    if string.find(lower, "alliancecity", 1, true)
        or string.find(lower, "alliancebuild", 1, true) then return "alliance_building" end
    if string.find(lower, "kill_zombie_world_box", 1, true) then return nil end
    if string.find(lower, "zombie", 1, true)
        or string.find(lower, "worldmonster", 1, true)
        or string.find(lower, "worldboss", 1, true) then return "monster" end
    if string.find(lower, "goodsfood(clone)", 1, true)
        or string.find(lower, "goodsiron(clone)", 1, true)
        or string.find(lower, "goodswood(clone)", 1, true)
        or string.find(lower, "goodsgold(clone)", 1, true)
        or string.find(lower, "collectresource", 1, true)
        or string.find(lower, "worldresource", 1, true) then return "resource_point" end
    return nil
end

local function update_scene_root_diagnostics()
    city_visible = false
    world_visible = false
    scene_scanned = 0
    local cs = rawget(_G, "CS")
    local unity = cs and safe_get(cs, "UnityEngine")
    local resources = unity and safe_get(unity, "Resources")
    local game_object_type = unity and safe_get(unity, "GameObject")
    local typeof_fn = rawget(_G, "typeof")
    if resources == nil or game_object_type == nil or type(typeof_fn) ~= "function" then return end
    local ok_type, reflected_type = pcall(typeof_fn, game_object_type)
    if not ok_type or reflected_type == nil then return end
    local ok_objects, objects = static_call(resources, "FindObjectsOfTypeAll", reflected_type)
    if not ok_objects or objects == nil then return end
    local count = tonumber(safe_get(objects, "Length") or safe_get(objects, "Count")) or 0
    count = math.min(count, MAX_GAME_OBJECTS)
    for index = 0, count - 1 do
        local candidate = safe_get(objects, index) or safe_get(objects, index + 1)
        if candidate ~= nil and safe_get(candidate, "activeInHierarchy") == true then
            scene_scanned = scene_scanned + 1
            local path = transform_path(candidate)
            local lower_path = string.lower(path)
            if lower_path == "city" or string.find(lower_path, "city/", 1, true) == 1 then
                city_visible = true
            end
            local root_object = root_game_object(candidate)
            local root_name = tostring(safe_get(root_object, "name") or "")
            if string.lower(root_name) == "world" or unity_kind(root_name) ~= nil then
                world_visible = true
            end
        end
    end
end

local function update_manager_diagnostics(now)
    if manager_diagnostics_at ~= 0 and now - manager_diagnostics_at < 5 then return end
    manager_diagnostics_at = now
    local world, world_source = find_active_world_scene()
    observed_world_source = world_source
    observed_world_type = runtime_type_name(world)
    observed_world_members = reflected_world_member_summary(world)
    observed_manager_candidates = recovered_manager_summary()
    update_scene_root_diagnostics()
    if world ~= nil then
        local ok_main, main = call(world, "GetAllMainBaseList")
        local ok_screen, screen = call(world, "GetMainByScreen")
        local main_sample, screen_sample = nil, nil
        fallback_all_main_count, main_sample = collection_probe(ok_main and main or nil)
        fallback_screen_count, screen_sample = collection_probe(ok_screen and screen or nil)
        fallback_sample = main_sample or screen_sample
    end
end

local function point_manager()
    local world, world_source = find_active_world_scene()
    if world ~= nil then
        local manager = safe_get(world, "PointManager") or safe_get(world, "WorldPointManager")
            or safe_get(world, "pointManager")
        if manager == nil then
            local ok, value = call(world, "get_PointManager")
            if ok then manager = value end
        end
        if manager ~= nil and reflected_private_collection(manager, "_pointInfos") ~= nil then
            return manager, nil, world_source, tostring(world_source) .. ".PointManager"
        end
    end
    local recovered, recovered_source = recovered_manager_candidate()
    if recovered ~= nil then
        return recovered, nil, world_source, recovered_source
    end
    return nil, world ~= nil and "world_point_manager_unavailable" or world_source,
        world_source, nil
end

transform_path = function(game_object)
    local transform = safe_get(game_object, "transform")
    if transform == nil then return tostring(safe_get(game_object, "name") or "") end
    local parts = {}
    local current = transform
    for _ = 1, 64 do
        if current == nil then break end
        local current_object = safe_get(current, "gameObject")
        local name = safe_get(current_object, "name") or safe_get(current, "name")
        if name ~= nil then table.insert(parts, 1, tostring(name)) end
        current = safe_get(current, "parent")
    end
    return table.concat(parts, "/")
end

local function is_world_button_candidate(game_object, path)
    local lower = string.lower(path or "")
    if lower == "gameframework/ui/uicontainer/uiresource/uimain/safearea/bottomlayer/worldbtn/btn" then
        return true
    end
    if string.sub(lower, -13) == "/worldbtn/btn" then return true end
    local name = string.lower(tostring(safe_get(game_object, "name") or ""))
    if name ~= "btn" then return false end
    local transform = safe_get(game_object, "transform")
    local parent = safe_get(transform, "parent")
    local parent_object = safe_get(parent, "gameObject")
    local parent_name = string.lower(tostring(safe_get(parent_object, "name") or safe_get(parent, "name") or ""))
    return parent_name == "worldbtn"
end

local function component(game_object, component_type)
    if game_object == nil or component_type == nil then return nil end
    local typeof_fn = rawget(_G, "typeof")
    if type(typeof_fn) ~= "function" then return nil end
    local ok_type, reflected_type = pcall(typeof_fn, component_type)
    if not ok_type or reflected_type == nil then return nil end
    local ok_component, value = call(game_object, "GetComponent", reflected_type)
    return ok_component and value or nil
end

local function find_world_button()
    local cs = rawget(_G, "CS")
    local resources = safe_get(safe_get(cs, "UnityEngine"), "Resources")
    local game_object_type = safe_get(safe_get(cs, "UnityEngine"), "GameObject")
    local ui = safe_get(safe_get(cs, "UnityEngine"), "UI")
    local button_type = safe_get(ui, "Button")
    local new_button_type = safe_get(cs, "NewButton")
    local typeof_fn = rawget(_G, "typeof")
    if resources == nil or game_object_type == nil or type(typeof_fn) ~= "function" then
        return nil, nil, "world button discovery unavailable"
    end
    local ok_type, reflected_type = pcall(typeof_fn, game_object_type)
    if not ok_type or reflected_type == nil then return nil, nil, "GameObject type unavailable" end
    local ok_objects, objects = static_call(resources, "FindObjectsOfTypeAll", reflected_type)
    if not ok_objects or objects == nil then return nil, nil, "GameObject enumeration unavailable" end
    local count = tonumber(safe_get(objects, "Length") or safe_get(objects, "Count")) or 0
    if count > MAX_GAME_OBJECTS then count = MAX_GAME_OBJECTS end
    for index = 0, count - 1 do
        local game_object = safe_get(objects, index) or safe_get(objects, index + 1)
        if game_object ~= nil and safe_get(game_object, "activeInHierarchy") ~= false then
            local path = transform_path(game_object)
            if is_world_button_candidate(game_object, path) then
                local button = component(game_object, button_type) or component(game_object, new_button_type)
                if button ~= nil then return button, path, nil end
            end
        end
    end
    return nil, nil, "world button unavailable"
end

local function ensure_world(now)
    if transition_requested then
        transition_state = "waiting_for_world"
        return false, "world transition already requested"
    end
    local scene_utils = rawget(_G, "SceneUtils")
    local change_to_world = safe_get(scene_utils, "ChangeToWorld")
    if type(change_to_world) ~= "function" then
        transition_state = "change_to_world_unavailable"
        return false, "SceneUtils.ChangeToWorld is unavailable"
    end
    local _, path = find_world_button()
    transition_button_path = path
    transition_method = "SceneUtils.ChangeToWorld"
    local ok, invoke_error = pcall(change_to_world)
    if not ok then
        transition_state = "change_to_world_failed"
        return false, tostring(invoke_error)
    end
    transition_requested = true
    transition_requested_at = now
    transition_invocations = 1
    transition_state = "requested"
    return true, nil
end

local function loaded_point_count(manager)
    local collection = safe_get(manager, "_pointInfos")
    if collection == nil then
        local ok_type, reflected_type = pcall(function() return manager:GetType() end)
        if ok_type and reflected_type ~= nil then
            local ok_field, field = pcall(function() return reflected_type:GetField("_pointInfos", 52) end)
            if ok_field and field ~= nil then
                local ok_value, value = pcall(function() return field:GetValue(manager) end)
                if ok_value then collection = value end
            end
        end
    end
    if collection == nil then return nil end
    return tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
end

local function vector_text(value)
    if value == nil then return nil end
    local x = tonumber(safe_get(value, "x") or safe_get(value, "X"))
    local y = tonumber(safe_get(value, "y") or safe_get(value, "Y"))
    if x == nil or y == nil then return nil end
    return tostring(math.floor(x)) .. "," .. tostring(math.floor(y))
end

local function update_aoi_diagnostics(manager)
    aoi_block_size = tonumber(reflected_private_collection(manager, "_lwAoiBlockSize"))
    aoi_block_count = tonumber(reflected_private_collection(manager, "_lwAoiBlockCount"))
    aoi_lod = tonumber(reflected_private_collection(manager, "LOD"))
    aoi_server_lod = tonumber(reflected_private_collection(manager, "svLod"))
    aoi_req_pos = vector_text(reflected_private_collection(manager, "reqPos"))
    aoi_last_lb = vector_text(reflected_private_collection(manager, "_lastViewLBBlock"))
    aoi_last_rt = vector_text(reflected_private_collection(manager, "_lastViewRTBlock"))
    aoi_once_max_request_count = tonumber(
        reflected_private_collection(manager, "once_max_request_count"))
    aoi_first_time_request = reflected_private_collection(manager, "firstTimeReqAoi") == true
    aoi_battle_field_first = reflected_private_collection(manager, "battleFieldFirst") == true
end

local function world_is_stable(manager, now)
    local count = loaded_point_count(manager)
    if count == nil then
        transition_state = "world_count_unavailable"
        return false, "WorldPointManager._pointInfos count unavailable"
    end
    if stable_point_count ~= count then
        stable_point_count = count
        stable_since = now
        transition_state = "stabilizing"
        return false, "loaded world points are stabilizing"
    end
    if now - stable_since < STABLE_SECONDS then
        transition_state = "stabilizing"
        return false, "loaded world points are stabilizing"
    end
    transition_state = "world_stable"
    return true, nil
end

local function capture(now, manager)
    local captured_at = os.date("!%Y-%m-%dT%H:%M:%SZ", now)
    local snapshot, snapshot_error = builder.Build(
        manager, "live-world-" .. tostring(now), captured_at)
    if snapshot == nil then return nil, snapshot_error end
    local file = io.open(snapshot_path, "wb")
    if not file then return nil, "world-map snapshot file could not be opened" end
    file:write(serialize(snapshot))
    file:close()
    return true
end

function M.Pump()
    pump_count = pump_count + 1
    local now = tonumber(os.time()) or 0
    local heartbeat_ok = write_heartbeat(now)
    if capture_complete then return heartbeat_ok end
    local active_scene_state = update_scene_identity()
    local manager, manager_error, world_source, manager_source = point_manager()
    observed_world_source = world_source
    observed_manager_source = manager_source
    if active_scene_state ~= "world" then
        if manager == nil then update_manager_diagnostics(now) end
        if active_scene_state == "city" then
            local requested, transition_error = ensure_world(now)
            if requested then
                write_status(now, "transition_requested", nil)
            else
                write_status(now, "waiting", transition_error or manager_error)
            end
        else
            transition_state = "scene_identity_unavailable"
            write_status(now, "waiting", "current scene is neither authoritative City nor World")
        end
        return heartbeat_ok
    end
    if transition_requested then transition_state = "world_scene_id_confirmed" end
    if manager == nil then
        update_manager_diagnostics(now)
        write_status(now, "waiting", manager_error or "World scene confirmed; WorldPointManager unavailable")
        return heartbeat_ok
    end
    update_aoi_diagnostics(manager)
    local stable, stable_error = world_is_stable(manager, now)
    if not stable then
        write_status(now, "waiting", stable_error)
        return heartbeat_ok
    end
    local ok, captured, err = pcall(capture, now, manager)
    if not ok then
        write_status(now, "error", captured)
    elseif captured then
        transition_state = "captured"
        capture_complete = true
        write_status(now, "captured", nil)
    else
        write_status(now, "waiting", err)
    end
    return heartbeat_ok
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
                local ok_timer, handle = pcall(register_repeat, timer, 0.25, 0.25, update_callback)
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
'''
    source = source.replace("__PROBE_VERSION__", PROBE_VERSION)
    source = source.replace("__SNAPSHOT_BUILDER__", _builder_source())
    return source.encode("utf-8")


def _encoded_entries(paths: dict[str, Path], entries: list[tuple[str, bytes]]):
    mapped = _entry_map(entries)
    official = mapped.get(LUA_ENTRY)
    if official is None:
        raise InstallRefused(f"{LUA_ENTRY} is missing from LWScripts.data")
    native = derive_xlua_key_nonce(_xlua_path(paths))
    wrapper_source = _entry_source()
    probe_source = _probe_source()
    wrapper = encode_lenc_bytes(wrapper_source, native["key"], native["nonce"])
    probe = encode_lenc_bytes(probe_source, native["key"], native["nonce"])
    if decode_lenc_bytes(wrapper, native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("world-map wrapper LENC round-trip failed")
    if decode_lenc_bytes(probe, native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("world-map probe LENC round-trip failed")

    mapped[ORIGINAL_LUA_ENTRY] = bytes(official)
    mapped[LUA_ENTRY] = wrapper
    mapped[PROBE_ENTRY] = probe
    output: list[tuple[str, bytes]] = []
    seen: set[str] = set()
    for name, _ in entries:
        output.append((name, mapped[name]))
        seen.add(name)
    for name in (ORIGINAL_LUA_ENTRY, PROBE_ENTRY):
        if name not in seen:
            output.append((name, mapped[name]))
    return output, native, bytes(official), wrapper_source, probe_source


def prepare(paths: dict[str, Path], output_dir: Path) -> dict[str, object]:
    before = preflight(paths)
    if before["file_version"] != 3:
        raise InstallRefused("world-map probe requires current LWLF version 3")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = [output_dir / "LWScripts.data", output_dir / "LWScripts.txt", output_dir / "version.txt"]
    if any(path.exists() for path in outputs):
        raise InstallRefused("world-map candidate output already exists")

    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared, native, official, wrapper_source, probe_source = _encoded_entries(paths, entries)
    write_lwlf(outputs[0], file_version, content_version, prepared)
    verify_version, verify_content, verify_entries = read_lwlf(outputs[0])
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("world-map candidate header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official:
        raise InstallRefused("world-map candidate did not preserve official LuaEntry")
    if decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("world-map serialized wrapper verification failed")
    if decode_lenc_bytes(verify_map[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("world-map serialized probe verification failed")

    length = outputs[0].stat().st_size
    crc = crc32_file(outputs[0])
    outputs[1].write_text(f"{length}|{crc}", encoding="utf-8")
    outputs[2].write_text(str(content_version), encoding="utf-8")
    return {
        "changed_installed_files": False,
        "output_dir": str(output_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(verify_entries),
        "package_length": length,
        "package_crc32": crc,
        "package_sha256": hashlib.sha256(outputs[0].read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(probe_source).hexdigest(),
        "xlua_sha256": native["sha256"],
        "serialized_round_trip_verified": True,
        "preflight": before,
    }


def verify(paths: dict[str, Path], candidate_dir: Path) -> dict[str, object]:
    candidate_dir = candidate_dir.resolve()
    data = candidate_dir / "LWScripts.data"
    metadata = candidate_dir / "LWScripts.txt"
    version = candidate_dir / "version.txt"
    if not data.is_file() or not metadata.is_file() or not version.is_file():
        raise InstallRefused("world-map candidate is incomplete")
    file_version, content_version, entries = read_lwlf(data)
    mapped = _entry_map(entries)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    official_file_version, official_content_version, official_entries = read_lwlf(paths["data"])
    official_map = _entry_map(official_entries)
    if (file_version, content_version) != (official_file_version, official_content_version):
        raise InstallRefused("world-map candidate LWLF version does not match installed build")
    if mapped.get(ORIGINAL_LUA_ENTRY) != official_map.get(LUA_ENTRY):
        raise InstallRefused("world-map candidate original LuaEntry does not match installed build")
    if decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != _entry_source():
        raise InstallRefused("world-map candidate wrapper does not match expected source")
    if decode_lenc_bytes(mapped[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != _probe_source():
        raise InstallRefused("world-map candidate probe does not match expected source")
    expected_length, expected_crc = metadata.read_text(encoding="utf-8").strip().split("|", 1)
    if int(expected_length) != data.stat().st_size or int(expected_crc) != crc32_file(data):
        raise InstallRefused("world-map candidate metadata length/CRC mismatch")
    if int(version.read_text(encoding="utf-8").strip()) != content_version:
        raise InstallRefused("world-map candidate version.txt mismatch")
    return {
        "candidate_dir": str(candidate_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "package_sha256": hashlib.sha256(data.read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(_probe_source()).hexdigest(),
        "verified": True,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--prepare-dir", type=Path)
    group.add_argument("--verify-dir", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        paths = discover_paths()
        result = prepare(paths, args.prepare_dir) if args.prepare_dir else verify(paths, args.verify_dir)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
