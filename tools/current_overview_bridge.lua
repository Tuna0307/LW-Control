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
    local connected = safe_get(network, "IsConnected")
    local connecting = safe_get(network, "IsConnecting")
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
        destroy_message()
        write_heartbeat(now, false, control == nil and "control_unavailable" or "host_lease_stale")
        return true
    end

    if active == nil or active.sessionId ~= control.sessionId or active.challenge ~= control.challenge or
       active.profileId ~= control.profileId or active.gamePid ~= control.gamePid then
        destroy_message()
        active = control
    end

    install_recovery_action_hooks()
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
