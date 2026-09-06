"""Build or verify one bounded current-build daily-task reward-claim candidate.

The probe derives exactly one claimable target from a fresh game-owned snapshot,
sends at most one explicit reward request, records any matching response/push
evidence, then performs one fresh daily-task list refresh and accepts success only
when that authoritative post-state proves the exact target transition. Stage -1
is excluded.
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


PROBE_VERSION = "lwcontrol-daily-claim-probe-3"


def _builder_source() -> str:
    return Path(__file__).with_name("current_daily_task_snapshot_probe.lua").read_text(encoding="utf-8")


def _probe_source() -> bytes:
    source = r'''local M = { VERSION = "__PROBE_VERSION__" }
local root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWControl\runtime]]
local heartbeat_path = root .. [[\daily-task-claim-heartbeat.json]]
local status_path = root .. [[\daily-task-claim-status.json]]
local before_path = root .. [[\daily-task-claim-before.json]]
local after_path = root .. [[\daily-task-claim-after.json]]
local result_path = root .. [[\daily-task-claim-result.json]]
local wire_path = root .. [[\daily-task-claim-wire.json]]
local last_heartbeat = 0
local refresh_requested = false
local refresh_requested_at = nil
local post_refresh_requested = false
local post_refresh_requested_at = nil
local update_wrapped = false
local handlers_wrapped = false
local push_handler_wrapped = false
local wire_events = {}
local send_count = 0
local completed = false
local target = nil
local before_snapshot = nil
local response_observed = false
local response_handler = nil
local response_correlated = false
local response_had_error = false
local response_error = nil
local response_handler_ok = nil
local response_handler_error = nil
local unpack_values = table.unpack or unpack
local attempt_claim = nil

local builder = (function()
__SNAPSHOT_BUILDER__
end)()

local function safe_get(value, key)
    if value == nil then return nil end
    local ok, output = pcall(function() return value[key] end)
    return ok and output or nil
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

local ARRAY_KEYS = { tasks = true, receivedStages = true, boxes = true, events = true, taskUpdates = true, stages = true }

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

local function write_heartbeat(now)
    if last_heartbeat == now then return true end
    local ok = write_json(heartbeat_path, { version = M.VERSION, loaded = true, updatedAt = now })
    if ok then last_heartbeat = now end
    return ok
end

local function write_status(now, state, err)
    return write_json(status_path, {
        probeVersion = M.VERSION,
        builderVersion = builder.VERSION,
        state = state,
        updatedAt = now,
        error = err,
        refreshRequestedAt = refresh_requested_at,
        postRefreshRequestedAt = post_refresh_requested_at,
        sendCount = send_count,
        target = target,
    })
end

local function build_snapshot(manager, template_manager, task_state, prefix, now)
    local captured_at = os.date("!%Y-%m-%dT%H:%M:%SZ", now)
    return builder.Build(manager, template_manager, task_state,
        prefix .. "-" .. tostring(now), captured_at)
end

local function select_one(snapshot)
    for _, box in ipairs(snapshot.boxes or {}) do
        if box.state == "CanReceive" and box.index >= 1 and box.index <= 5 then
            return {
                kind = "DailyQuestStage",
                stage = box.index,
                captureId = snapshot.captureId,
            }
        end
    end
    for _, task in ipairs(snapshot.tasks or {}) do
        if task.state == "CanReceive" and task.taskId ~= nil and tostring(task.taskId) ~= "" then
            return {
                kind = "DailyTask",
                taskId = tostring(task.taskId),
                captureId = snapshot.captureId,
            }
        end
    end
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

local function contains_received_stage(snapshot, stage)
    for _, value in ipairs(snapshot.receivedStages or {}) do
        if tonumber(value) == tonumber(stage) then return true end
    end
    return false
end

local function response_contains_task_field(message, field, task_id)
    local info = safe_get(message, field)
    if type(info) ~= "table" then return false end
    for _, item in pairs(info) do
        if tostring(safe_get(item, "id")) == tostring(task_id) then return true end
    end
    return false
end

local function response_contains_task(message, task_id)
    return response_contains_task_field(message, "taskInfo", task_id)
end

local function response_contains_push_task(message, task_id)
    return response_contains_task_field(message, "dailyQuest", task_id)
end

local function state_name(value, task_state)
    if value == task_state.NoComplete then return "NoComplete" end
    if value == task_state.CanReceive then return "CanReceive" end
    if value == task_state.Received then return "Received" end
    return value == nil and nil or tostring(value)
end

local function summarize_task_updates(message, field, task_state)
    local output = {}
    local info = safe_get(message, field)
    if type(info) ~= "table" then return output end
    for _, item in pairs(info) do
        local id = safe_get(item, "id")
        output[#output + 1] = {
            taskId = id == nil and nil or tostring(id),
            state = state_name(safe_get(item, "state"), task_state),
        }
    end
    table.sort(output, function(left, right) return tostring(left.taskId) < tostring(right.taskId) end)
    return output
end

local function summarize_stages(message)
    local output = {}
    local stages = safe_get(message, "stageArr")
    if type(stages) ~= "table" then return output end
    for _, value in pairs(stages) do
        if tonumber(value) ~= nil then output[#output + 1] = tonumber(value) end
    end
    table.sort(output)
    return output
end

local function capture_wire(kind, message, task_state)
    if send_count < 1 then return end
    local row = {
        kind = kind,
        observedAt = tonumber(os.time()) or 0,
        errorCode = safe_get(message, "errorCode"),
    }
    if kind == "DailyTaskReward" then
        row.taskUpdates = summarize_task_updates(message, "taskInfo", task_state)
    elseif kind == "PushDailyQuest" then
        row.taskUpdates = summarize_task_updates(message, "dailyQuest", task_state)
    elseif kind == "DailyQuestReward" then
        row.stages = summarize_stages(message)
    end
    wire_events[#wire_events + 1] = row
    write_json(wire_path, { probeVersion = M.VERSION, events = wire_events })
end

local function response_contains_stage(message, stage)
    local stages = safe_get(message, "stageArr")
    if type(stages) ~= "table" then return false end
    for _, value in pairs(stages) do
        if tonumber(value) == tonumber(stage) then return true end
    end
    return false
end

local function record_response_from_handler(handler_name, message, handler_ok, handler_error)
    if completed or target == nil or before_snapshot == nil then return end
    response_observed = true
    response_handler = handler_name
    if response_handler_ok == nil then
        response_handler_ok = handler_ok
    else
        response_handler_ok = response_handler_ok and handler_ok
    end
    if not handler_ok and response_handler_error == nil then
        response_handler_error = handler_error
    end
    local observed_error = safe_get(message, "errorCode")
    if observed_error ~= nil then
        response_had_error = true
        if response_error == nil then response_error = observed_error end
    end
    local correlated = false
    if target.kind == "DailyTask" then
        if handler_name == "PushDailyQuestMessage.HandleMessage" then
            correlated = response_contains_push_task(message, target.taskId)
        else
            correlated = response_contains_task(message, target.taskId)
        end
    elseif target.kind == "DailyQuestStage" then
        correlated = response_contains_stage(message, target.stage)
    end
    response_correlated = response_correlated or correlated
end

local function effect_confirmed_from_snapshots(after_snapshot)
    if after_snapshot == nil or before_snapshot == nil or target == nil then return false end
    if target.kind == "DailyTask" then
        return task_state(before_snapshot, target.taskId) == "CanReceive"
            and task_state(after_snapshot, target.taskId) == "Received"
    elseif target.kind == "DailyQuestStage" then
        return box_state(before_snapshot, target.stage) == "CanReceive"
            and not contains_received_stage(before_snapshot, target.stage)
            and box_state(after_snapshot, target.stage) == "Received"
            and contains_received_stage(after_snapshot, target.stage)
    end
    return false
end

local function finish_from_post_refresh(manager, template_manager, task_state, now)
    if completed or target == nil or before_snapshot == nil then return end
    local after_snapshot, snapshot_error = build_snapshot(manager, template_manager, task_state, "claim-after", now)
    local effect_confirmed = effect_confirmed_from_snapshots(after_snapshot)

    if after_snapshot ~= nil then write_json(after_path, after_snapshot) end
    local state = "verification_failed"
    if response_had_error then state = "response_error"
    elseif response_handler_ok == false then state = "handler_error"
    elseif effect_confirmed then state = "claimed" end
    write_json(result_path, {
        probeVersion = M.VERSION,
        state = state,
        target = target,
        sendCount = send_count,
        verificationMode = "fresh_daily_quest_list_state",
        postRefreshRequested = post_refresh_requested,
        postRefreshRequestedAt = post_refresh_requested_at,
        postRefreshObserved = true,
        postRefreshObservedAt = now,
        responseObserved = response_observed,
        responseHandler = response_handler,
        responseCorrelated = response_correlated,
        responseHadError = response_had_error,
        responseError = response_error,
        handlerOk = response_handler_ok,
        handlerError = response_handler_error,
        effectConfirmed = effect_confirmed,
        beforeCaptureId = before_snapshot.captureId,
        afterCaptureId = after_snapshot and after_snapshot.captureId or nil,
        snapshotError = snapshot_error,
        wireEventCount = #wire_events,
    })
    completed = true
    write_status(now, state, snapshot_error or response_handler_error)
end

local function install_handlers(manager, template_manager, task_state)
    if handlers_wrapped then return true end
    if type(manager.DailyTaskRewardMessageHandle) ~= "function"
        or type(manager.DailyQuestRewardMessageHandle) ~= "function" then
        return false
    end

    local previous_task = manager.DailyTaskRewardMessageHandle
    manager.DailyTaskRewardMessageHandle = function(...)
        local message = select(2, ...)
        capture_wire("DailyTaskReward", message, task_state)
        local values = { pcall(previous_task, ...) }
        local ok = table.remove(values, 1)
        record_response_from_handler(
            "DailyTaskRewardMessageHandle", message, ok, ok and nil or values[1])
        if not ok then error(values[1]) end
        return unpack_values(values)
    end

    local previous_stage = manager.DailyQuestRewardMessageHandle
    manager.DailyQuestRewardMessageHandle = function(...)
        local message = select(2, ...)
        capture_wire("DailyQuestReward", message, task_state)
        local values = { pcall(previous_stage, ...) }
        local ok = table.remove(values, 1)
        record_response_from_handler(
            "DailyQuestRewardMessageHandle", message, ok, ok and nil or values[1])
        if not ok then error(values[1]) end
        return unpack_values(values)
    end
    handlers_wrapped = true
    return true
end

local function install_push_handler(manager, template_manager, task_state)
    if push_handler_wrapped then return true end
    local ok, message_type = pcall(require, "Net.Msgs.Alliance.PushDailyQuestMessage")
    if not ok or type(message_type) ~= "table" or type(message_type.HandleMessage) ~= "function" then
        return false
    end
    local previous = message_type.HandleMessage
    message_type.HandleMessage = function(...)
        local message = select(2, ...)
        capture_wire("PushDailyQuest", message, task_state)
        local values = { pcall(previous, ...) }
        local handler_ok = table.remove(values, 1)
        if target ~= nil and target.kind == "DailyTask" and not completed then
            record_response_from_handler(
                "PushDailyQuestMessage.HandleMessage", message, handler_ok, handler_ok and nil or values[1])
        end
        if not handler_ok then error(values[1]) end
        return unpack_values(values)
    end
    push_handler_wrapped = true
    return true
end

local function request_refresh_once(manager, now)
    if refresh_requested then return false end
    if type(manager.TryReqUpdateData) ~= "function" then return false end
    refresh_requested = true
    refresh_requested_at = now
    write_status(now, "refresh_requested", nil)
    local ok, err = pcall(manager.TryReqUpdateData, manager)
    if not ok then write_status(now, "refresh_error", err) end
    return ok
end

local function request_post_claim_refresh_once(manager, now)
    if post_refresh_requested then return false end
    if type(manager.TryReqUpdateData) ~= "function" then return false end
    post_refresh_requested = true
    post_refresh_requested_at = now
    write_status(now, "post_refresh_requested", nil)
    local ok, err = pcall(manager.TryReqUpdateData, manager)
    if not ok then write_status(now, "post_refresh_error", err) end
    return ok, err
end

local function install_update_observer(manager, template_manager, task_state)
    if update_wrapped then return true end
    if type(manager.UpdateDailyTask) ~= "function" then return false end
    local previous = manager.UpdateDailyTask
    manager.UpdateDailyTask = function(...)
        local values = { pcall(previous, ...) }
        local ok = table.remove(values, 1)
        local now = tonumber(os.time()) or 0
        if ok and send_count > 0 and post_refresh_requested and not completed then
            local finish_ok, finish_error = pcall(
                finish_from_post_refresh, manager, template_manager, task_state, now)
            if not finish_ok then write_status(now, "error", finish_error) end
        elseif ok and type(attempt_claim) == "function" then
            local claim_ok, claim_error = pcall(attempt_claim, now)
            if not claim_ok then write_status(now, "error", claim_error) end
        end
        if not ok then error(values[1]) end
        return unpack_values(values)
    end
    update_wrapped = true
    return true
end

attempt_claim = function(now)
    if completed or send_count > 0 then return true end
    local data_center = rawget(_G, "DataCenter")
    local task_state_enum = rawget(_G, "TaskState")
    if type(data_center) ~= "table" then return nil, "DataCenter unavailable" end
    if task_state_enum == nil then return nil, "TaskState unavailable" end
    local manager = data_center.DailyTaskManager
    local template_manager = data_center.DailyTaskTemplateManager
    if manager == nil or template_manager == nil then return nil, "daily-task managers unavailable" end
    install_update_observer(manager, template_manager, task_state_enum)

    local snapshot, err = build_snapshot(manager, template_manager, task_state_enum, "claim-before", now)
    if snapshot == nil then
        if type(manager.dailyBoxActive) == "table" and manager.dailyBoxActive[1] == nil then
            request_refresh_once(manager, now)
        end
        return nil, err or "pre-claim snapshot unavailable"
    end

    before_snapshot = snapshot
    target = select_one(snapshot)
    write_json(before_path, snapshot)
    if target == nil then
        write_json(result_path, {
            probeVersion = M.VERSION,
            state = "no_eligible_target",
            sendCount = 0,
            beforeCaptureId = snapshot.captureId,
        })
        completed = true
        write_status(now, "no_eligible_target", nil)
        return true
    end

    if not install_handlers(manager, template_manager, task_state_enum) then
        completed = true
        write_status(now, "handler_unavailable", "required reward response handler unavailable")
        return nil, "required reward response handler unavailable"
    end
    if target.kind == "DailyTask" and not install_push_handler(manager, template_manager, task_state_enum) then
        completed = true
        write_status(now, "handler_unavailable", "PushDailyQuest response path unavailable")
        return nil, "PushDailyQuest response path unavailable"
    end

    local network = rawget(_G, "SFSNetwork")
    local defines = rawget(_G, "MsgDefines")
    if type(network) ~= "table" or type(network.SendMessage) ~= "function" or type(defines) ~= "table" then
        completed = true
        write_status(now, "send_unavailable", "SFSNetwork.SendMessage or MsgDefines unavailable")
        return nil, "claim send dependencies unavailable"
    end

    local command = nil
    local argument = nil
    if target.kind == "DailyTask" then
        command = defines.DailyTaskReward
        argument = target.taskId
    elseif target.kind == "DailyQuestStage" and target.stage >= 1 and target.stage <= 5 then
        command = defines.DailyQuestReward
        argument = target.stage
    end
    if command == nil or argument == nil then
        completed = true
        write_status(now, "send_unavailable", "explicit claim command unavailable")
        return nil, "explicit claim command unavailable"
    end

    send_count = 1
    write_status(now, "sending", nil)
    local ok, send_error = pcall(function()
        network.SendMessage(command, argument)
    end)
    if not ok then
        write_json(result_path, {
            probeVersion = M.VERSION,
            state = "send_error",
            target = target,
            sendCount = send_count,
            responseObserved = false,
            effectConfirmed = false,
            error = send_error,
        })
        completed = true
        write_status(now, "send_error", send_error)
        return nil, send_error
    end
    if not completed then
        local refresh_ok, refresh_error = request_post_claim_refresh_once(manager, now)
        if not refresh_ok then
            write_json(result_path, {
                probeVersion = M.VERSION,
                state = "post_refresh_error",
                target = target,
                sendCount = send_count,
                verificationMode = "fresh_daily_quest_list_state",
                postRefreshRequested = post_refresh_requested,
                postRefreshRequestedAt = post_refresh_requested_at,
                postRefreshObserved = false,
                responseObserved = response_observed,
                responseCorrelated = response_correlated,
                responseHadError = response_had_error,
                responseError = response_error,
                effectConfirmed = false,
                error = refresh_error or "DailyTaskManager.TryReqUpdateData unavailable",
            })
            completed = true
            write_status(now, "post_refresh_error", refresh_error)
            return nil, refresh_error or "post-claim refresh unavailable"
        end
        write_status(now, "sent_waiting_post_refresh", nil)
    end
    return true
end

function M.Pump()
    local now = tonumber(os.time()) or 0
    write_heartbeat(now)
    if completed then return true end
    local ok, result, err = pcall(attempt_claim, now)
    if not ok then write_status(now, "error", result)
    elseif result == nil and not completed then write_status(now, "waiting", err) end
    return true
end

M.Pump()
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
        raise InstallRefused("Daily claim wrapper LENC round-trip failed")
    if decode_lenc_bytes(probe, native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("Daily claim probe LENC round-trip failed")

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
        raise InstallRefused("Daily claim probe requires current LWLF version 3")
    output_dir = output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = [output_dir / "LWScripts.data", output_dir / "LWScripts.txt", output_dir / "version.txt"]
    if any(path.exists() for path in outputs):
        raise InstallRefused("Daily claim candidate output already exists")

    file_version, content_version, entries = read_lwlf(paths["data"])
    prepared, native, official, wrapper_source, probe_source = _encoded_entries(paths, entries)
    write_lwlf(outputs[0], file_version, content_version, prepared)
    verify_version, verify_content, verify_entries = read_lwlf(outputs[0])
    verify_map = _entry_map(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise InstallRefused("Daily claim candidate header did not round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official:
        raise InstallRefused("Daily claim candidate did not preserve official LuaEntry")
    if decode_lenc_bytes(verify_map[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != wrapper_source:
        raise InstallRefused("Daily claim serialized wrapper verification failed")
    if decode_lenc_bytes(verify_map[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != probe_source:
        raise InstallRefused("Daily claim serialized probe verification failed")

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
        "builder_source_sha256": hashlib.sha256(_builder_source().encode("utf-8")).hexdigest(),
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
        raise InstallRefused("Daily claim candidate is incomplete")
    file_version, content_version, entries = read_lwlf(data)
    mapped = _entry_map(entries)
    native = derive_xlua_key_nonce(_xlua_path(paths))
    official_file_version, official_content_version, official_entries = read_lwlf(paths["data"])
    official_map = _entry_map(official_entries)
    if (file_version, content_version) != (official_file_version, official_content_version):
        raise InstallRefused("Daily claim candidate LWLF version does not match installed build")
    if mapped.get(ORIGINAL_LUA_ENTRY) != official_map.get(LUA_ENTRY):
        raise InstallRefused("Daily claim candidate original LuaEntry does not match installed build")
    if decode_lenc_bytes(mapped[LUA_ENTRY], native["key"], native["nonce"])["decoded"] != _entry_source():
        raise InstallRefused("Daily claim candidate wrapper does not match expected source")
    if decode_lenc_bytes(mapped[PROBE_ENTRY], native["key"], native["nonce"])["decoded"] != _probe_source():
        raise InstallRefused("Daily claim candidate probe does not match expected source")
    expected_length, expected_crc = metadata.read_text(encoding="utf-8").strip().split("|", 1)
    if int(expected_length) != data.stat().st_size or int(expected_crc) != crc32_file(data):
        raise InstallRefused("Daily claim candidate metadata length/CRC mismatch")
    if int(version.read_text(encoding="utf-8").strip()) != content_version:
        raise InstallRefused("Daily claim candidate version.txt mismatch")
    return {
        "candidate_dir": str(candidate_dir),
        "file_version": file_version,
        "content_version": content_version,
        "entry_count": len(entries),
        "package_sha256": hashlib.sha256(data.read_bytes()).hexdigest(),
        "probe_source_sha256": hashlib.sha256(_probe_source()).hexdigest(),
        "builder_source_sha256": hashlib.sha256(_builder_source().encode("utf-8")).hexdigest(),
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
