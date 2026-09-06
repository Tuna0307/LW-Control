-- One-request current-build Rally list refresh and post-refresh snapshot.
--
-- This runtime template is embedded into a temporary encrypted script package.
-- It uses the exact GetAllianceWarList call recovered from current content v12:
--   SFSNetwork.SendMessage(MsgDefines.GetAllianceWarList,
--       LuaEntry.Player:GetCurServerId())
-- It contains only the list-refresh request plus read-only manager observation.

local M = {
    VERSION = "lwcontrol-current-rally-sync-snapshot-1",
    MODE = "sync_state",
}

local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
local heartbeat_path = root .. [[\rally-sync-loader-probe.json]]
local snapshot_path = root .. [[\rally-sync-snapshot.json]]
local status_path = root .. [[\rally-sync-snapshot-status.json]]

local builder = (function()
-- __SNAPSHOT_BUILDER__
end)()

local registration_method = nil
local update_callback = nil
local timer_handle = nil
local last_heartbeat = 0
local stage = "waiting"
local pre_snapshot = nil
local pre_signature = nil
local pre_stable_since = nil
local sync_started_at = nil
local sync_protocol = nil
local target_server = nil
local current_world_id = nil
local owned_send_count = 0
local foreign_sync_send_count = 0
local handler_count = 0
local response_error_code = nil
local response_teams_present = false
local response_observed_at = nil
local issuing = false
local hooks_installed = false
local original_send = nil
local original_handle = nil
local message_module = nil
local network = nil
local capture_complete = false

local function safe_get(target, key)
    if target == nil then return nil end
    local ok, value = pcall(function() return target[key] end)
    return ok and value or nil
end

local function invoke(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil, "missing:" .. tostring(method) end
    local packed = table.pack(pcall(fn, target, ...))
    if packed[1] then return true, table.unpack(packed, 2, packed.n) end
    local first_error = tostring(packed[2])
    packed = table.pack(pcall(fn, ...))
    if packed[1] then return true, table.unpack(packed, 2, packed.n) end
    return false, nil, first_error .. " | " .. tostring(packed[2])
end

local function invoke_static(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, ...)
    return ok, ok and value or nil
end

local function text(value)
    if value == nil then return "" end
    local ok, output = pcall(tostring, value)
    return ok and output or ""
end

local function each_value(collection, limit, consume)
    if collection == nil then return 0 end
    local values = safe_get(collection, "Values")
    if values ~= nil and values ~= collection then return each_value(values, limit, consume) end
    local count = 0
    if type(collection) == "table" then
        for key, item in pairs(collection) do
            count = count + 1
            if consume(item, key) == false or count >= limit then break end
        end
        return count
    end
    local ok_enum, enumerator = invoke(collection, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        while count < limit do
            local ok_move, moved = invoke(enumerator, "MoveNext")
            if not ok_move or moved ~= true then break end
            local current = safe_get(enumerator, "Current")
            local item = safe_get(current, "Value")
            if item == nil then item = current end
            count = count + 1
            if consume(item, safe_get(current, "Key")) == false then break end
        end
        return count
    end
    local length = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if length ~= nil then
        for index = 0, math.min(length - 1, limit - 1) do
            local item = safe_get(collection, index)
            if item == nil then item = safe_get(collection, index + 1) end
            if item ~= nil then
                count = count + 1
                if consume(item, index) == false then break end
            end
        end
    end
    return count
end

local function world_march_manager(world)
    if world == nil then return nil end
    local manager = safe_get(world, "MarchDataManager")
    if manager == nil then
        local ok_manager, output = invoke(world, "get_MarchDataManager")
        if ok_manager then manager = output end
    end
    return manager
end

local function find_active_world_context()
    local direct = safe_get(rawget(_G, "SceneManager"), "World")
    if direct ~= nil and safe_get(direct, "isActiveAndEnabled") ~= false
        and type(safe_get(direct, "HandleFormationMarch")) == "function" then
        local manager = world_march_manager(direct)
        if manager ~= nil then return manager, "SceneManager.World" end
    end
    local cs = rawget(_G, "CS")
    local resources = safe_get(safe_get(cs, "UnityEngine"), "Resources")
    local scene_type = safe_get(cs, "WorldScene")
    local typeof_fn = rawget(_G, "typeof")
    if resources == nil or scene_type == nil or type(typeof_fn) ~= "function" then return nil, "" end
    local ok_type, reflected_type = pcall(typeof_fn, scene_type)
    if not ok_type then return nil, "" end
    local ok_objects, objects = invoke_static(resources, "FindObjectsOfTypeAll", reflected_type)
    if not ok_objects or objects == nil then return nil, "" end
    local found = nil
    each_value(objects, 32, function(candidate)
        if safe_get(candidate, "isActiveAndEnabled") == true
            and type(safe_get(candidate, "HandleFormationMarch")) == "function" then
            local manager = world_march_manager(candidate)
            if manager ~= nil then found = manager; return false end
        end
        return true
    end)
    return found, found ~= nil and "Resources.FindObjectsOfTypeAll" or ""
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

local ARRAY_KEYS = { rallies = true, formations = true, memberNames = true }

local function json_encode(value, force_array)
    local kind = type(value)
    if value == nil then return "null" end
    if kind == "boolean" then return value and "true" or "false" end
    if kind == "number" then
        if value ~= value or value == math.huge or value == -math.huge then return "null" end
        return tostring(value)
    end
    if kind == "string" then return json_string(value) end
    if kind ~= "table" then return json_string(tostring(value)) end
    if force_array then
        local parts = { "[" }
        for index, item in ipairs(value) do
            if index > 1 then parts[#parts + 1] = "," end
            parts[#parts + 1] = json_encode(item, false)
        end
        parts[#parts + 1] = "]"
        return table.concat(parts)
    end
    local keys = {}
    for key in pairs(value) do keys[#keys + 1] = tostring(key) end
    table.sort(keys)
    local parts = { "{" }
    for index, key in ipairs(keys) do
        if index > 1 then parts[#parts + 1] = "," end
        parts[#parts + 1] = json_string(key)
        parts[#parts + 1] = ":"
        parts[#parts + 1] = json_encode(value[key], ARRAY_KEYS[key] == true)
    end
    parts[#parts + 1] = "}"
    return table.concat(parts)
end

local function write_json(path, value)
    local file = io.open(path, "wb")
    if not file then return false end
    file:write(json_encode(value, false))
    file:close()
    return true
end

local function sync_evidence()
    return {
        protocol = text(sync_protocol),
        targetServer = target_server,
        currentWorldId = current_world_id,
        ownedSendCount = owned_send_count,
        foreignSyncSendCount = foreign_sync_send_count,
        handlerCount = handler_count,
        responseErrorCode = response_error_code,
        responseTeamsPresent = response_teams_present,
        responseObservedAt = response_observed_at,
        exactlyOneOwnedSend = owned_send_count == 1,
        noForeignSameProtocolSend = foreign_sync_send_count == 0,
        noRetry = true,
    }
end

local function write_status(now, state_value, err)
    return write_json(status_path, {
        probeVersion = M.VERSION,
        builderVersion = builder.VERSION,
        state = state_value,
        updatedAt = now,
        error = err,
        registrationMethod = registration_method or "",
        explicitNetworkSends = owned_send_count,
        joinActions = 0,
        claimActions = 0,
        sync = sync_evidence(),
    })
end

local function write_heartbeat(now)
    if now == last_heartbeat then return true end
    local ok = write_json(heartbeat_path, { version = M.VERSION, loaded = true, updatedAt = now })
    if ok then last_heartbeat = now end
    return ok
end

local function snapshot_signature(snapshot)
    local parts = {
        tostring(snapshot.player and snapshot.player.uid or ""),
        tostring(snapshot.observedRallyCount or 0),
        tostring(snapshot.formationCount or 0),
        tostring(snapshot.freeFormationCount or 0),
    }
    for _, rally in ipairs(snapshot.rallies or {}) do parts[#parts + 1] = tostring(rally.uuid or "") end
    for _, formation in ipairs(snapshot.formations or {}) do
        parts[#parts + 1] = tostring(formation.uuid or "") .. ":" .. tostring(formation.isFree == true)
    end
    return table.concat(parts, "|")
end

local function restore_hooks()
    if not hooks_installed then return true end
    local ok_network = pcall(function()
        if network ~= nil and original_send ~= nil then network["SendMessage"] = original_send end
    end)
    local ok_message = pcall(function()
        if message_module ~= nil and original_handle ~= nil then message_module["HandleMessage"] = original_handle end
    end)
    local restored = ok_network and ok_message
        and safe_get(network, "SendMessage") == original_send
        and safe_get(message_module, "HandleMessage") == original_handle
    hooks_installed = not restored
    return restored
end

local function install_hooks(protocol)
    network = rawget(_G, "SFSNetwork")
    original_send = safe_get(network, "SendMessage")
    if type(network) ~= "table" or type(original_send) ~= "function" then
        return false, "SFSNetwork.SendMessage hook unavailable"
    end
    local ok_module, module = pcall(require, "Net.Msgs.Alliance.GetAllianceWarListMessage")
    if not ok_module or type(module) ~= "table" then
        return false, "GetAllianceWarListMessage module unavailable"
    end
    message_module = module
    original_handle = safe_get(message_module, "HandleMessage")
    if type(original_handle) ~= "function" then return false, "GetAllianceWarListMessage.HandleMessage unavailable" end

    local send_wrapper = function(...)
        local args = table.pack(...)
        local arg_offset = args[1] == network and 1 or 0
        local sent_protocol = args[1 + arg_offset]
        if text(sent_protocol) == text(protocol) then
            if issuing then owned_send_count = owned_send_count + 1
            else foreign_sync_send_count = foreign_sync_send_count + 1 end
        end
        return original_send(...)
    end
    local handle_wrapper = function(...)
        local args = table.pack(...)
        local payload = args[args.n]
        local packed = table.pack(pcall(original_handle, ...))
        if sync_started_at ~= nil then
            handler_count = handler_count + 1
            response_error_code = safe_get(payload, "errorCode")
            response_teams_present = safe_get(payload, "teams") ~= nil
            response_observed_at = os.time()
        end
        if not packed[1] then error(packed[2]) end
        return table.unpack(packed, 2, packed.n)
    end
    local ok_send = pcall(function() network["SendMessage"] = send_wrapper end)
        and safe_get(network, "SendMessage") == send_wrapper
    if not ok_send then return false, "SFSNetwork.SendMessage hook install failed" end
    local ok_handle = pcall(function() message_module["HandleMessage"] = handle_wrapper end)
        and safe_get(message_module, "HandleMessage") == handle_wrapper
    if not ok_handle then
        pcall(function() network["SendMessage"] = original_send end)
        return false, "GetAllianceWarListMessage.HandleMessage hook install failed"
    end
    hooks_installed = true
    return true
end

local function build_snapshot(now, suffix)
    local data_center = rawget(_G, "DataCenter")
    if type(data_center) ~= "table" then return nil, "DataCenter unavailable" end
    local war_manager = safe_get(data_center, "AllianceWarDataManager")
    local formation_manager = safe_get(data_center, "ArmyFormationDataManager")
    local player = safe_get(rawget(_G, "LuaEntry"), "Player")
    local world_manager, world_source = find_active_world_context()
    local captured_at = os.date("!%Y-%m-%dT%H:%M:%SZ", now)
    return builder.Build(
        war_manager, formation_manager, world_manager, player,
        "live-sync-" .. tostring(now) .. "-" .. suffix, captured_at, world_source)
end

local function begin_sync(now)
    local msg_defines = rawget(_G, "MsgDefines")
    sync_protocol = safe_get(msg_defines, "GetAllianceWarList")
    if sync_protocol == nil or text(sync_protocol) == "" then return false, "MsgDefines.GetAllianceWarList unavailable" end
    local player = safe_get(rawget(_G, "LuaEntry"), "Player")
    local ok_server, server = invoke(player, "GetCurServerId")
    target_server = ok_server and tonumber(server) or nil
    if target_server == nil or target_server < 0 or target_server ~= math.floor(target_server) then
        return false, "LuaEntry.Player.GetCurServerId invalid"
    end
    local ok_world, world_id = invoke(player, "GetCurWorldId")
    current_world_id = ok_world and tonumber(world_id) or nil
    if current_world_id == nil then return false, "LuaEntry.Player.GetCurWorldId invalid" end
    local hook_ok, hook_error = install_hooks(sync_protocol)
    if not hook_ok then return false, hook_error end
    issuing = true
    local send = safe_get(network, "SendMessage")
    local packed = table.pack(pcall(send, sync_protocol, target_server))
    issuing = false
    if not packed[1] then restore_hooks(); return false, "GetAllianceWarList send failed:" .. text(packed[2]) end
    if owned_send_count ~= 1 then restore_hooks(); return false, "expected exactly one owned GetAllianceWarList send" end
    sync_started_at = now
    stage = "waiting_response"
    return true
end

local function finish_capture(now)
    if owned_send_count ~= 1 or foreign_sync_send_count ~= 0 or handler_count ~= 1 then
        restore_hooks()
        return false, "sync correlation counts are not exact"
    end
    if response_error_code ~= nil then
        restore_hooks()
        return false, "GetAllianceWarList response error:" .. text(response_error_code)
    end
    local snapshot, snapshot_error = build_snapshot(now, "post")
    if snapshot == nil then restore_hooks(); return false, snapshot_error end
    local hooks_restored = restore_hooks()
    if not hooks_restored then return false, "sync hooks did not restore" end
    snapshot.sync = sync_evidence()
    snapshot.preSyncObservedRallyCount = pre_snapshot and pre_snapshot.observedRallyCount or nil
    snapshot.preSyncJoinableRallyCount = pre_snapshot and pre_snapshot.joinableRallyCount or nil
    snapshot.listRefreshCorrelated = response_teams_present == true
        and owned_send_count == 1 and foreign_sync_send_count == 0 and handler_count == 1
    if not write_json(snapshot_path, snapshot) then return false, "rally sync snapshot file could not be opened" end
    capture_complete = true
    stage = "captured"
    write_status(now, "captured", nil)
    return true
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    write_heartbeat(now)
    if capture_complete then return true end

    if stage == "waiting" or stage == "stabilizing" then
        local ok, snapshot, err = pcall(build_snapshot, now, "pre")
        if not ok then write_status(now, "error", snapshot); return true end
        if snapshot == nil then write_status(now, "waiting", err); return true end
        local signature = snapshot_signature(snapshot)
        if signature ~= pre_signature then
            pre_snapshot = snapshot
            pre_signature = signature
            pre_stable_since = now
            stage = "stabilizing"
            write_status(now, "stabilizing", nil)
            return true
        end
        pre_snapshot = snapshot
        if pre_stable_since == nil or now - pre_stable_since < 3 then
            write_status(now, "stabilizing", nil)
            return true
        end
        local sync_ok, sync_error = begin_sync(now)
        if not sync_ok then write_status(now, "error", sync_error); return true end
        write_status(now, "waiting_response", nil)
        return true
    end

    if stage == "waiting_response" then
        if sync_started_at ~= nil and now - sync_started_at > 8 then
            restore_hooks()
            stage = "error"
            write_status(now, "error", "GetAllianceWarList response timeout")
            return true
        end
        if handler_count > 1 or owned_send_count > 1 or foreign_sync_send_count > 0 then
            restore_hooks()
            stage = "error"
            write_status(now, "error", "sync correlation became ambiguous")
            return true
        end
        if handler_count == 1 and response_observed_at ~= nil and now - response_observed_at >= 1 then
            local ok, err = finish_capture(now)
            if not ok then stage = "error"; write_status(now, "error", err) end
        else
            write_status(now, "waiting_response", nil)
        end
        return true
    end
    return true
end

function M.Register()
    if registration_method ~= nil then return true end
    update_callback = function() M.Pump() end
    local manager = rawget(_G, "UpdateManager")
    local instance = manager
    if manager ~= nil and type(safe_get(manager, "GetInstance")) == "function" then
        local ok_instance, value = invoke(manager, "GetInstance")
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
