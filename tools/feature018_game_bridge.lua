-- FEATURE-RECOVERY-018 implementation-policy transport.
-- A command is delivered only for the authenticated, lease-fresh Overview owner.
-- Handlers are registered by separate source-proven game feature modules. A
-- missing handler is reported as unavailable, never as successful gameplay.
local M = { VERSION = "feature018-game-bridge-1", handlers = {} }
local root = (os.getenv("LWBRIDGE_REBUILD_DATA_ROOT") or "")
if root == "" then root = (os.getenv("LOCALAPPDATA") or ".") .. [[\LWBridgeRebuild]] end
root = root .. [[\overview-bridge]]
local command_path = root .. [[\feature018-request.txt]]
local result_path = root .. [[\feature018-response.json]]
local last_request = nil
local last_session = nil

local function encode_string(value)
    return '"' .. tostring(value):gsub('\\', '\\\\'):gsub('"', '\\"'):
        gsub('\r', '\\r'):gsub('\n', '\\n'):gsub('[\0-\31]', '?') .. '"'
end
local function json_value(value, depth)
    if depth > 10 then return 'null' end
    if value == nil then return 'null' end
    local kind = type(value)
    if kind == 'boolean' then return value and 'true' or 'false' end
    if kind == 'number' then
        if value ~= value or value == math.huge or value == -math.huge then return 'null' end
        return tostring(value)
    end
    if kind ~= 'table' then return encode_string(value) end
    local marker = getmetatable(value)
    local array = #value > 0 or (marker ~= nil and marker.__feature018_array == true)
    local out = {}
    if array then
        for _, item in ipairs(value) do out[#out+1] = json_value(item, depth+1) end
    else
        for key, item in pairs(value) do
            if type(key) == 'string' then
                out[#out+1] = encode_string(key) .. ':' .. json_value(item, depth+1)
            end
        end
    end
    return (array and '[' or '{') .. table.concat(out, ',') .. (array and ']' or '}')
end
local function read_request()
    local file = io.open(command_path, 'rb')
    if file == nil then return nil end
    local content = file:read(32769) or ''
    file:close()
    if #content > 32768 then return nil end
    local values = {}
    for line in content:gmatch('[^\r\n]+') do
        local key, value = line:match('^([%w_]+)=(.*)$')
        if key == nil or values[key] ~= nil then return nil end
        values[key] = value
    end
    return values
end
local function valid_token(value)
    return type(value) == 'string' and #value > 0 and #value <= 128 and
        value:match('^[%w_-]+$') ~= nil
end
local function current_owner(values)
    if type(values) ~= 'table' or values.schema ~= '1' or
        not valid_token(values.requestId) or not valid_token(values.command) or
        not valid_token(values.profileId) or not valid_token(values.launchSessionId) or
        not valid_token(values.challenge) then return false end
    local bridge = rawget(_G, 'LWBridgeOverviewBridge')
    local gate = bridge and bridge.IsAuthorizedFeature018Request
    return type(gate) == 'function' and gate(values) == true
end
local function respond(values, state, error_code, error, data)
    -- Observation time is recorded by the in-game runtime, never provided by
    -- the request. A dispatch acknowledgement must not use state=confirmed.
    local packet = {
        schemaVersion = 1, requestId = values.requestId,
        command = values.command, profileId = values.profileId,
        launchSessionId = values.launchSessionId, challenge = values.challenge,
        gamePid = tonumber(values.gamePid),
        observedAt = (tonumber(os.time()) or 0) * 1000,
        state = state, errorCode = error_code, error = error, data = data,
    }
    local encoded = json_value(packet, 0)
    local file = io.open(result_path, 'wb')
    if file then file:write(encoded); file:close() end
end
function M.RegisterHandler(command, handler)
    if not valid_token(command) or type(handler) ~= 'function' then return false end
    if M.handlers[command] ~= nil then return false end
    M.handlers[command] = handler
    return true
end
-- Empty Lua tables default to JSON objects. A game handler must tag an empty
-- original array (buildings, issues, placements...) to preserve exact UI JSON.
function M.Array(values)
    if type(values) ~= 'table' then return nil end
    return setmetatable(values, { __feature018_array = true })
end
function M.Pump()
    local values = read_request()
    if not current_owner(values) then return false end
    if values.launchSessionId == last_session and values.requestId == last_request then return false end
    local issued = tonumber(values.issuedAt)
    local now = (tonumber(os.time()) or 0) * 1000
    if issued == nil or issued < now - 30000 or issued > now + 5000 then return false end
    last_session, last_request = values.launchSessionId, values.requestId
    local handler = M.handlers[values.command]
    if type(handler) ~= 'function' then
        respond(values, 'unsupported', 'GAME_METHOD_UNAVAILABLE',
            'No source-verified current game producer is registered for ' .. values.command, nil)
        return true
    end
    local ok, result = pcall(handler, values)
    if not ok then
        respond(values, 'failed', 'GAME_ACTION_FAILED', tostring(result), nil)
    elseif type(result) ~= 'table' or
        (result.state ~= 'confirmed' and result.state ~= 'accepted' and
         result.state ~= 'unsupported' and result.state ~= 'failed') then
        respond(values, 'failed', 'GAME_RESULT_INVALID', 'The current game result was not confirmed.', nil)
    else
        respond(values, result.state, result.errorCode, result.error, result.data)
    end
    return true
end
return M
