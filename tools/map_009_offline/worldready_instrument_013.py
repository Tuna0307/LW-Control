"""An exact-source, opt-in WorldReady lifecycle tracer (diagnostic only).

Source starts as the existing unchanged current_overview_bridge.lua body.
Each anchor is an actual live-boundary in that file; missing/multiple anchors
refuse packaging. No handler is replaced or re-ordered. Offline tests parse
the entire resulting Lua body and compare the default binary unchanged.
"""
from __future__ import annotations


class WorldReadyInstrumentationError(ValueError):
    pass


def instrument_worldready(body: bytes) -> bytes:
    # Windows checkout is CRLF. Only the private opt-in copy is normalized;
    # normal bridge_source() still returns the original byte stream.
    text = body.decode("utf-8").replace("\r\n", "\n")

    def replace_once(needle: str, replacement: str) -> None:
        nonlocal text
        if text.count(needle) != 1:
            raise WorldReadyInstrumentationError(
                "expected exactly one source-bound WorldReady anchor: " + repr(needle[:90])
            )
        text = text.replace(needle, replacement, 1)

    replace_once(
        "local pending_world_ready = nil\n",
        """local pending_world_ready = nil
-- MAP013 exact-task-owned, opt-in only. Never written to product/game data.
local _map013_world_log = root .. [[\\map013-worldready-lifecycle.txt]]
local _map013_world_last, _map013_world_count = {}, 0
local function _map013_world_emit(stage, request, detail)
    -- Observing any stage must never alter Pump/callback behavior on I/O error.
    pcall(function()
        local now = os.time()
        local id = request and request.requestId or "-"
        -- Prevent malformed incoming IDs and errors from injecting log rows.
        if type(id) ~= "string" or #id > 128 or
            string.match(id, "^[%w_-]+$") == nil then id = "invalid_token" end
        local key = stage .. ":" .. id
        if _map013_world_count >= 200 or _map013_world_last[key] == now then return end
        local f = io.open(_map013_world_log, "ab")
        if f ~= nil then
            local safe_detail = string.sub(string.gsub(tostring(detail or "-"), "[^%w_+-]", "_"), 1, 96)
            local written = f:write(tostring(now), "|", stage, "|", tostring(id), "|",
                safe_detail, "\\n")
            local closed = f:close()
            if written and closed then
                _map013_world_last[key] = now
                _map013_world_count = _map013_world_count + 1
            end
        end
    end)
end
""",
    )
    replace_once(
        """local function read_world_ready(control)
    local values = read_kv(world_ready_path)
    if values == nil then return nil end
    pcall(os.remove, world_ready_path)
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then return nil end
""",
        """local function read_world_ready(control)
    local values = read_kv(world_ready_path)
    if values == nil then return nil end
    _map013_world_emit("consumption_read", {requestId=values.requestId}, "file_seen")
    local _map013_removed, _map013_remove_result = pcall(os.remove, world_ready_path)
    _map013_world_emit(_map013_removed and _map013_remove_result and
        "command_consumed" or "command_remove_failed", {requestId=values.requestId},
        _map013_removed and _map013_remove_result and "removed" or "not_removed")
    if values.schema ~= "1" or values.bridgeVersion ~= M.VERSION or not valid_token(values.requestId) then
        _map013_world_emit("schema_or_token_rejected", {requestId=values.requestId}, "no_result_emitted")
        return nil
    end
""",
    )
    replace_once(
        """        request.error = "world_ready_identity_mismatch"
    end
    return request
end
""",
        """        request.error = "world_ready_identity_mismatch"
        _map013_world_emit("owner_rejected", request, request.error)
    end
    if request.error == nil then _map013_world_emit("owner_admitted", request, "ok") end
    return request
end
""",
    )
    replace_once(
        """    write_json(world_ready_result_path, {
""",
        """    local _map013_written = write_json(world_ready_result_path, {
""",
    )
    replace_once(
        """        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
end

local function begin_world_ready(request)
""",
        """        capturedAt = os.date("!%Y-%m-%dT%H:%M:%SZ", tonumber(os.time()) or 0),
    })
    _map013_world_emit(_map013_written and "result_written" or "result_write_failed", request, state)
end

local function begin_world_ready(request)
    _map013_world_emit("begin_transition", request, request.error or "admitted")
""",
    )
    replace_once(
        """    if select(1, navigation_world()) ~= nil then
        write_world_ready_result(request, "proven", nil, "already_world_scene")
""",
        """    if select(1, navigation_world()) ~= nil then
        _map013_world_emit("already_world_scene", request, "WorldScene")
        write_world_ready_result(request, "proven", nil, "already_world_scene")
""",
    )
    replace_once(
        """    if type(change_to_world) ~= "function" then
        write_world_ready_result(request, "failed", "change_to_world_unavailable", nil)
""",
        """    if type(change_to_world) ~= "function" then
        _map013_world_emit("transition_unavailable", request, "SceneUtils")
        write_world_ready_result(request, "failed", "change_to_world_unavailable", nil)
""",
    )
    replace_once(
        """    local ok_change = pcall(change_to_world, function() end)
    if not ok_change then
""",
        """    _map013_world_emit("transition_invoked", request, "ChangeToWorld")
    local ok_change = pcall(change_to_world, function()
        _map013_world_emit("transition_callback_invoked", request, "callback_returned")
    end)
    if not ok_change then
        _map013_world_emit("transition_invocation_failed", request, "pcall")
""",
    )
    replace_once(
        """    pending_world_ready = request
end

local function pump_world_ready(control)
""",
        """    pending_world_ready = request
    _map013_world_emit("pending_transition", request, "await_WorldScene")
end

local function pump_world_ready(control)
""",
    )
    replace_once(
        """    if select(1, navigation_world()) ~= nil then
        write_world_ready_result(pending_world_ready, "proven", nil, tostring(pending_world_ready.transitionMethod or "world_transition") .. "+WorldScene")
""",
        """    if select(1, navigation_world()) ~= nil then
        _map013_world_emit("scene_world_confirmed", pending_world_ready, "WorldScene")
        write_world_ready_result(pending_world_ready, "proven", nil, tostring(pending_world_ready.transitionMethod or "world_transition") .. "+WorldScene")
""",
    )
    replace_once(
        """    if runtime_clock() - pending_world_ready.startedClock >= WORLD_READY_TIMEOUT_SECONDS then
        write_world_ready_result(pending_world_ready, "failed", "world_map_failed", pending_world_ready.transitionMethod)
""",
        """    if runtime_clock() - pending_world_ready.startedClock >= WORLD_READY_TIMEOUT_SECONDS then
        _map013_world_emit("scene_transition_timeout", pending_world_ready, "world_map_failed")
        write_world_ready_result(pending_world_ready, "failed", "world_map_failed", pending_world_ready.transitionMethod)
""",
    )
    replace_once(
        """    if ((control == nil and control_error == "busy") or
        (control ~= nil and not fresh and lease_error == "busy")) and
        can_defer_shared_read(control, now) then return false end
""",
        """    -- A task-owned live WorldReady request was silently abandoned when
    -- the shared reader intermittently returned 'unavailable', despite a
    -- fresh previously verified exact owner/lease. Use the *existing* bounded
    -- 5-second owner fence only for transient read failure, never for malformed,
    -- stale or mismatched data. Normal production remains unchanged.
    if ((control == nil and (control_error == "busy" or control_error == "unavailable")) or
        (control ~= nil and not fresh and
            (lease_error == "busy" or lease_error == "unavailable"))) and
        can_defer_shared_read(control, now) then
        _map013_world_emit("shared_read_deferred", nil,
            control == nil and control_error or lease_error)
        return false
    end
""",
    )
    replace_once(
        """    if control == nil or not fresh then
        verified_lease = nil
""",
        """    if control == nil or not fresh then
        _map013_world_emit("control_or_lease_rejected", nil,
            control == nil and (control_error or "control_absent") or (lease_error or "lease_invalid"))
        if pending_world_ready ~= nil then
            _map013_world_emit("pending_abandoned", pending_world_ready, "owner_lease_lost")
        end
        verified_lease = nil
""",
    )
    replace_once(
        """    install_recovery_action_hooks()
    pump_world_state(control)
    pump_world_ready(control)
""",
        """    install_recovery_action_hooks()
    _map013_world_emit("owner_lease_admitted", nil, control.sessionId and "verified" or "invalid")
    pump_world_state(control)
    pump_world_ready(control)
""",
    )
    return text.encode("utf-8")
