-- Clean-room Daily Task Claim runtime for current Last War builds.
-- Behavior is limited to the reverse-engineered DailyQuestLs, DailyTaskReward,
-- DailyQuestReward, and PushDailyQuest paths documented in this repository.

local M = { VERSION = "lwcontrol-daily-task-runtime-1" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
local command_path = root .. [[\daily-task-command.txt]]
local heartbeat_path = root .. [[\daily-task-runtime-heartbeat.json]]
local status_path = root .. [[\daily-task-runtime-status.json]]
local last_heartbeat = 0
local last_command_poll = 0
local registration_method = nil
local update_callback = nil
local handlers_installed = false
local unpack_values = table.unpack or unpack

local active = nil
local response_error = nil
local response_handler_error = nil
local SETTLE_SECONDS = 1

local builder = (function()
__SNAPSHOT_BUILDER__
end)()

local function safe_get(value, key)
    if value == nil then return nil end
    local ok, output = pcall(function() return value[key] end)
    return ok and output or nil
end

local function invoke(value, name, ...)
    local fn = safe_get(value, name)
    if type(fn) ~= "function" then return false, nil end
    local ok, output = pcall(fn, value, ...)
    if ok then return true, output end
    return pcall(fn, ...)
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

local ARRAY_KEYS = { claimedTargets = true, receivedStages = true, boxes = true, tasks = true }

local function json_encode(value, force_array)
    local kind = type(value)
    if value == nil then return "null" end
    if kind == "boolean" then return value and "true" or "false" end
    if kind == "number" then return tostring(value) end
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

local function file_exists(path)
    local file = io.open(path, "rb")
    if not file then return false end
    file:close()
    return true
end

local function safe_command_id(value)
    if type(value) ~= "string" or #value < 1 or #value > 128 then return false end
    return string.match(value, "^[A-Za-z0-9][A-Za-z0-9_-]*$") ~= nil
end

local function result_path(command_id)
    return root .. [[\daily-task-result-]] .. command_id .. ".json"
end

local function write_heartbeat(now)
    if last_heartbeat == now then return end
    if write_json(heartbeat_path, {
        version = M.VERSION,
        loaded = true,
        updatedAt = now,
        registrationMethod = registration_method,
    }) then last_heartbeat = now end
end

local function write_status(now, state, err)
    write_json(status_path, {
        version = M.VERSION,
        state = state,
        updatedAt = now,
        commandId = active and active.commandId or nil,
        phase = active and active.phase or nil,
        confirmedClaims = active and active.confirmedClaims or 0,
        rewardSendCount = active and active.rewardSendCount or 0,
        refreshSendCount = active and active.refreshSendCount or 0,
        error = err,
    })
end

local function parse_command()
    local file = io.open(command_path, "rb")
    if not file then return nil end
    local text = file:read("*a") or ""
    file:close()
    if #text > 4096 then return nil, "command_too_large" end
    local values = {}
    for line in string.gmatch(text, "[^\r\n]+") do
        local key, value = string.match(line, "^([A-Za-z][A-Za-z0-9_]*)=(.*)$")
        if key ~= nil then values[key] = value end
    end
    if values.schema ~= "1" then return nil, "unsupported_schema" end
    if not safe_command_id(values.commandId) then return nil, "invalid_command_id" end
    if values.mode ~= "run_once" then return nil, "unsupported_mode" end
    local maximum = tonumber(values.maximumClaims or "")
    if maximum == nil or maximum < 1 or maximum > 20 or maximum ~= math.floor(maximum) then
        return nil, "invalid_maximum_claims"
    end
    return { commandId = values.commandId, maximumClaims = maximum }
end

local function build_snapshot(manager, template_manager, task_state, prefix, now)
    local captured_at = os.date("!%Y-%m-%dT%H:%M:%SZ", now)
    return builder.Build(manager, template_manager, task_state,
        prefix .. "-" .. tostring(now), captured_at)
end

local function select_one(snapshot)
    for _, box in ipairs(snapshot.boxes or {}) do
        if box.state == "CanReceive" and box.index >= 1 and box.index <= 5 then
            return { kind = "DailyQuestStage", stage = box.index }
        end
    end
    local ids = {}
    local states = {}
    for _, task in ipairs(snapshot.tasks or {}) do
        if task.state == "CanReceive" and task.taskId ~= nil and tostring(task.taskId) ~= "" then
            local id = tostring(task.taskId)
            ids[#ids + 1] = id
            states[id] = task.state
        end
    end
    table.sort(ids)
    if #ids > 0 then return { kind = "DailyTask", taskId = ids[1] } end
    return nil
end

local function task_state(snapshot, task_id)
    for _, task in ipairs(snapshot.tasks or {}) do
        if tostring(task.taskId) == tostring(task_id) then return task.state end
    end
    return nil
end

local function box_state(snapshot, stage)
    for _, box in ipairs(snapshot.boxes or {}) do
        if tonumber(box.index) == tonumber(stage) then return box.state end
    end
    return nil
end

local function contains_stage(snapshot, stage)
    for _, value in ipairs(snapshot.receivedStages or {}) do
        if tonumber(value) == tonumber(stage) then return true end
    end
    return false
end

local function target_received(before, after, target)
    if target.kind == "DailyTask" then
        return task_state(before, target.taskId) == "CanReceive"
            and task_state(after, target.taskId) == "Received"
    end
    if target.kind == "DailyQuestStage" then
        return box_state(before, target.stage) == "CanReceive"
            and not contains_stage(before, target.stage)
            and box_state(after, target.stage) == "Received"
            and contains_stage(after, target.stage)
    end
    return false
end

local function finish_command(now, state, message, snapshot)
    if active == nil then return end
    local path = result_path(active.commandId)
    write_json(path, {
        schemaVersion = 1,
        runtimeVersion = M.VERSION,
        commandId = active.commandId,
        state = state,
        message = message,
        confirmedClaims = active.confirmedClaims,
        rewardSendCount = active.rewardSendCount,
        refreshSendCount = active.refreshSendCount,
        claimedTargets = active.claimedTargets,
        finalSnapshot = snapshot,
        completedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", now),
    })
    write_status(now, state, state == "completed" and nil or message)
    active = nil
    response_error = nil
    response_handler_error = nil
end

local function current_managers()
    local data_center = rawget(_G, "DataCenter")
    local task_state = rawget(_G, "TaskState")
    if type(data_center) ~= "table" or task_state == nil then return nil end
    local manager = data_center.DailyTaskManager
    local template_manager = data_center.DailyTaskTemplateManager
    if manager == nil or template_manager == nil then return nil end
    return manager, template_manager, task_state
end

local function send_list_refresh(now)
    if active == nil then return false, "no_active_command" end
    local network = rawget(_G, "SFSNetwork")
    local defines = rawget(_G, "MsgDefines")
    if type(network) ~= "table" or type(network.SendMessage) ~= "function"
        or type(defines) ~= "table" or defines.DailyQuestLs == nil then
        return false, "daily_quest_list_send_unavailable"
    end
    active.refreshSendCount = active.refreshSendCount + 1
    local ok, err = pcall(network.SendMessage, defines.DailyQuestLs)
    if not ok then return false, tostring(err) end
    active.lastActionAt = now
    return true
end

local function send_claim(now, target)
    if active == nil then return false, "no_active_command" end
    local network = rawget(_G, "SFSNetwork")
    local defines = rawget(_G, "MsgDefines")
    if type(network) ~= "table" or type(network.SendMessage) ~= "function" or type(defines) ~= "table" then
        return false, "claim_send_unavailable"
    end
    local command, argument
    if target.kind == "DailyTask" then
        command, argument = defines.DailyTaskReward, target.taskId
    elseif target.kind == "DailyQuestStage" and target.stage >= 1 and target.stage <= 5 then
        command, argument = defines.DailyQuestReward, target.stage
    end
    if command == nil or argument == nil then return false, "claim_target_unavailable" end
    active.rewardSendCount = active.rewardSendCount + 1
    local ok, err = pcall(network.SendMessage, command, argument)
    if not ok then return false, tostring(err) end
    active.target = target
    active.lastActionAt = now
    return true
end

local function after_fresh_state(now, snapshot)
    if active == nil then return end
    if active.phase == "waiting_pre_refresh" then
        local target = select_one(snapshot)
        if target == nil then
            finish_command(now, "completed", "no_eligible_target", snapshot)
            return
        end
        active.beforeSnapshot = snapshot
        response_error = nil
        response_handler_error = nil
        local sent, send_error = send_claim(now, target)
        if not sent then finish_command(now, "failed", send_error, snapshot); return end
        active.phase = "waiting_post_refresh"
        local refreshed, refresh_error = send_list_refresh(now)
        if not refreshed then finish_command(now, "failed", refresh_error, snapshot) end
        return
    end
    if active.phase == "waiting_post_refresh" then
        if response_error ~= nil then
            finish_command(now, "failed", "claim_response_error:" .. tostring(response_error), snapshot)
            return
        end
        if response_handler_error ~= nil then
            finish_command(now, "failed", "claim_handler_error:" .. tostring(response_handler_error), snapshot)
            return
        end
        if not target_received(active.beforeSnapshot, snapshot, active.target) then
            finish_command(now, "unknown", "claim_effect_unconfirmed", snapshot)
            return
        end
        active.confirmedClaims = active.confirmedClaims + 1
        active.claimedTargets[#active.claimedTargets + 1] = active.target
        active.target = nil
        active.beforeSnapshot = nil
        if active.confirmedClaims >= active.maximumClaims then
            finish_command(now, "completed", "maximum_claims_reached", snapshot)
            return
        end
        active.phase = "settling_confirmed_claim"
        active.settleSnapshot = snapshot
        active.settleUntil = now + SETTLE_SECONDS
    end
end

local function continue_after_settle(now)
    if active == nil or active.phase ~= "settling_confirmed_claim" then return end
    if now < (active.settleUntil or now) then return end
    local snapshot = active.settleSnapshot
    active.settleSnapshot = nil
    active.settleUntil = nil
    if response_error ~= nil then
        finish_command(now, "failed", "claim_response_error:" .. tostring(response_error), snapshot)
        return
    end
    if response_handler_error ~= nil then
        finish_command(now, "failed", "claim_handler_error:" .. tostring(response_handler_error), snapshot)
        return
    end
    local next_target = select_one(snapshot)
    if next_target == nil then
        finish_command(now, "completed", "no_more_eligible_targets", snapshot)
        return
    end
    active.beforeSnapshot = snapshot
    response_error = nil
    response_handler_error = nil
    local sent, send_error = send_claim(now, next_target)
    if not sent then finish_command(now, "failed", send_error, snapshot); return end
    active.phase = "waiting_post_refresh"
    local refreshed, refresh_error = send_list_refresh(now)
    if not refreshed then finish_command(now, "failed", refresh_error, snapshot) end
end

local function install_handlers()
    if handlers_installed then return true end
    local manager, template_manager, task_state = current_managers()
    if manager == nil then return false end
    if type(manager.DailyQuestLsMessageHandle) ~= "function"
        or type(manager.DailyTaskRewardMessageHandle) ~= "function"
        or type(manager.DailyQuestRewardMessageHandle) ~= "function" then
        return false
    end

    local previous_list = manager.DailyQuestLsMessageHandle
    manager.DailyQuestLsMessageHandle = function(...)
        local message = select(2, ...)
        local values = { pcall(previous_list, ...) }
        local ok = table.remove(values, 1)
        local now = tonumber(os.time()) or 0
        if not ok then
            if active ~= nil then finish_command(now, "failed", tostring(values[1]), nil) end
            error(values[1])
        end
        if active ~= nil and (active.phase == "waiting_pre_refresh" or active.phase == "waiting_post_refresh") then
            local error_code = safe_get(message, "errorCode")
            if error_code ~= nil then
                finish_command(now, "failed", "daily_quest_list_error:" .. tostring(error_code), nil)
            else
                local snapshot, snapshot_error = build_snapshot(
                    manager, template_manager, task_state, "daily-runtime", now)
                if snapshot == nil then
                    finish_command(now, "unknown", snapshot_error or "snapshot_unavailable", nil)
                else
                    after_fresh_state(now, snapshot)
                end
            end
        end
        return unpack_values(values)
    end

    local previous_task = manager.DailyTaskRewardMessageHandle
    manager.DailyTaskRewardMessageHandle = function(...)
        local message = select(2, ...)
        local values = { pcall(previous_task, ...) }
        local ok = table.remove(values, 1)
        if active ~= nil and (active.phase == "waiting_post_refresh" or active.phase == "settling_confirmed_claim") then
            local error_code = safe_get(message, "errorCode")
            if error_code ~= nil then response_error = error_code end
            if not ok then response_handler_error = values[1] end
        end
        if not ok then error(values[1]) end
        return unpack_values(values)
    end

    local previous_chest = manager.DailyQuestRewardMessageHandle
    manager.DailyQuestRewardMessageHandle = function(...)
        local message = select(2, ...)
        local values = { pcall(previous_chest, ...) }
        local ok = table.remove(values, 1)
        if active ~= nil and (active.phase == "waiting_post_refresh" or active.phase == "settling_confirmed_claim") then
            local error_code = safe_get(message, "errorCode")
            if error_code ~= nil then response_error = error_code end
            if not ok then response_handler_error = values[1] end
        end
        if not ok then error(values[1]) end
        return unpack_values(values)
    end

    local push_ok, push_type = pcall(require, "Net.Msgs.Alliance.PushDailyQuestMessage")
    if push_ok and type(push_type) == "table" and type(push_type.HandleMessage) == "function" then
        local previous_push = push_type.HandleMessage
        push_type.HandleMessage = function(...)
            local message = select(2, ...)
            local values = { pcall(previous_push, ...) }
            local ok = table.remove(values, 1)
            if active ~= nil and (active.phase == "waiting_post_refresh" or active.phase == "settling_confirmed_claim") then
                local error_code = safe_get(message, "errorCode")
                if error_code ~= nil then response_error = error_code end
                if not ok then response_handler_error = values[1] end
            end
            if not ok then error(values[1]) end
            return unpack_values(values)
        end
    end

    handlers_installed = true
    return true
end

local function start_command(command, now)
    local result = result_path(command.commandId)
    if file_exists(result) then
        write_status(now, "replay_rejected", "command_id_already_completed")
        return
    end
    if active ~= nil then
        write_status(now, "busy", "another_command_is_active")
        return
    end
    active = {
        commandId = command.commandId,
        maximumClaims = command.maximumClaims,
        confirmedClaims = 0,
        rewardSendCount = 0,
        refreshSendCount = 0,
        claimedTargets = {},
        phase = "waiting_managers",
        startedAt = now,
        lastActionAt = now,
    }
    write_status(now, "waiting_managers", nil)
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    write_heartbeat(now)
    if active ~= nil and active.phase == "waiting_managers" then
        if install_handlers() then
            active.phase = "waiting_pre_refresh"
            local ok, err = send_list_refresh(now)
            if not ok then finish_command(now, "failed", err, nil)
            else write_status(now, "running", nil) end
        elseif now - active.startedAt > 60 then
            finish_command(now, "unknown", "daily_task_managers_timeout", nil)
        end
    elseif active ~= nil and active.phase == "settling_confirmed_claim" then
        continue_after_settle(now)
    elseif active ~= nil and now - (active.lastActionAt or now) > 15 then
        finish_command(now, "unknown", "state_refresh_timeout", nil)
    end
    if now == last_command_poll then return true end
    last_command_poll = now
    local command, command_error = parse_command()
    if command_error ~= nil then
        write_status(now, "command_rejected", command_error)
        return true
    end
    if command ~= nil and (active == nil or command.commandId ~= active.commandId) then
        start_command(command, now)
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
            local repeat_fn = safe_get(timer, "RegisterTimerRepeat")
            if type(repeat_fn) == "function" then
                local ok_timer = pcall(repeat_fn, timer, 0.25, 0.25, update_callback)
                if not ok_timer then ok_timer = pcall(repeat_fn, 0.25, 0.25, update_callback) end
                if ok_timer then
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
