-- FEATURE-RECOVERY-018, isolated v26 F9/F10 relocation source preflight.
-- Exact installed v26 ORIGINAL entries (3/26):
-- Util/MoveCityUtil.luac sha256 d53c624e4dcc87b6228f8ac650704868e321332614d36321b88800b5f8562de4:
--   HasPaidMoveItem root/12 lines 372-383 checks ITEM_MOVE_CITY / SEASON_LIMIT_MOVE_CITY;
--   TryAllianceMoveCity root/2 lines 39-52 opens UIChooseRallyPoint or routes
--   to AllianceMoveCityToSourceRallyPoint root/6 lines 106-139, which may SEND;
--   TryMoveCity root/13 lines 386-483 checks battle, free-move cooldown,
--   special terrain and owned item before EnterMoveCityMode root/14.
-- UI/UIMoveCity/View/UIMoveCityView.luac sha256 20ce35e439aa6c1743ea59652008de4ca1da01bfa74c70f850a91037dda70164:
--   TryChangeUseItem root/30 lines 1415-1538 tests ITEM_ALLIANCE_CITY_MOVE;
--   TryMoveCity root/19 lines 844-1058 requires native warnings, checks blank
--   land, and carries meteorite, black-land and season confirmation branches;
--   DoCityMove root/15 lines 711-722 sends WorldMv only AFTER those gates.
-- Net/Msgs/WorldMvMessage.luac sha256 2f71e8b4fdcb42c07b3b738d4614c456a38fd12dce334b4c157213a2f8d6c4e3:
--   OnCreate root/0 lines 8-31 sends pointId/itemUUid/freeAllianceMove;
--   HandleMessage root/1 lines 33-51 routes reply via BuildManager.WorldMvHandle.
-- Net/Msgs/MoveCityToWorldMessage.luac sha256 d82c7095c19d7de503a17115ee82647a015ede6ac230336bdb9c29673d973594:
--   HandleMessage root/1 lines 21-29 handles tutorial/city-to-world only;
--   NOT a replacement for a WorldMv confirmation.
--
-- There is NO proven mapping from F9 to an original random relocation item
-- and no selected, validated F10 alliance destination. This module NEVER
-- dispatches WorldMv, AllianceMoveCity, ItemUse or opens native UI windows.
-- It exposes a source-derived read-only before/after observation and explicit
-- unsupported result. The prime wraps authenticated Hotkey.Handle with
-- M.RandomRelocate(fields, TextInputClear) and M.AllianceRelocate(...).

local M = {VERSION = "feature018-relocation-actions-1"}
local baseline = {}
local last_owner = nil
local HOME_SERVER = 2212 -- ONLY the authorized owner sub-account's home server.

local function member(object, name)
    if object == nil then return nil end
    local ok, result = pcall(function() return object[name] end)
    if ok then return result end
    return nil
end

local function invoke(object, method, ...)
    local fn = member(object, method)
    if type(fn) ~= "function" then return false, nil end
    local ok, value = pcall(fn, object, ...)
    return ok, value
end

local function integer(value, minimum)
    if type(value) == "boolean" then return nil end
    local n = tonumber(value)
    if n == nil or n ~= n or n == math.huge or n == -math.huge or
       n < minimum or n % 1 ~= 0 or n > 9007199254740991 then return nil end
    return n
end

local function uid(value)
    if type(value) == "boolean" or value == nil then return nil end
    local id = tostring(value)
    if id:match("^[1-9]%d*$") and #id <= 20 then return id end
    return nil
end

local function failed(code, reason)
    return {state="failed", errorCode=code, error=reason}
end

local function unavailable(reason, data)
    return {state="unsupported", errorCode="GAME_METHOD_UNAVAILABLE",
            error=reason, data=data}
end

local function key_identity(fields, expected_action)
    if type(fields) ~= "table" or fields.action ~= expected_action or
       fields.slot ~= "0" then return nil end
    local result = {}
    for _, key in ipairs({"profileId", "launchSessionId", "challenge", "gamePid"}) do
        local value = fields[key]
        if type(value) ~= "string" or #value == 0 or #value > 128 or
           not value:match("^[%w_-]+$") then return nil end
        result[#result + 1] = value
    end
    return table.concat(result, "/")
end

-- Unlike a mere after-position (possibly from manual/other action), this
-- records the exact owner and home server alongside the before observation.
local function world_snapshot()
    local player = member(rawget(_G, "LuaEntry"), "Player")
    local player_uid = uid(member(player, "uid"))
    local ok_source, source_server = invoke(player, "GetSourceServerId")
    local ok_self, self_server = invoke(player, "GetSelfServerId")
    local ok_current, current_server = invoke(player, "GetCurServerId")
    local ok_pos, position = invoke(player, "GetMainWorldPos")
    source_server, self_server = integer(source_server, 1), integer(self_server, 1)
    current_server, position = integer(current_server, 1), integer(position, 0)
    if player_uid == nil or not ok_source or not ok_self or not ok_current or
       not ok_pos or source_server == nil or self_server == nil or
       current_server == nil or position == nil then
        return nil, "Native owner, server and city position snapshot incomplete"
    end
    if source_server ~= HOME_SERVER or self_server ~= HOME_SERVER or
       current_server ~= HOME_SERVER then
        return nil, "Connected world is not the authorized server-2212 home world"
    end

    local item_data = member(rawget(_G, "DataCenter"), "ItemData")
    local ids = rawget(_G, "SpecialItemId")
    local inventory = {}
    for _, item_type in ipairs({"ITEM_MOVE_CITY", "SEASON_LIMIT_MOVE_CITY",
                               "ITEM_ALLIANCE_CITY_MOVE"}) do
        local id = integer(member(ids, item_type), 1)
        if id == nil then return nil, "Unknown source-defined relocation item ID: " .. item_type end
        local ok_item, item = invoke(item_data, "GetItemById", id)
        if not ok_item then return nil, "Relocation inventory unavailable: " .. item_type end
        local count = item == nil and 0 or integer(member(item, "count"), 0)
        if count == nil then return nil, "Relocation item count unreadable: " .. item_type end
        inventory[item_type] = {id=id, count=count}
    end
    local ok_alliance, in_alliance = invoke(player, "IsInAlliance")
    if not ok_alliance or type(in_alliance) ~= "boolean" then
        return nil, "Native alliance membership unavailable"
    end

    return {observation="native_player_world_position_and_owned_move_items",
        ownerUid=player_uid, sourceServerId=source_server,
        selfServerId=self_server, currentServerId=current_server,
        worldPointId=position, inAlliance=in_alliance,
        itemCounts=inventory, noActionDispatched=true}, nil
end

-- This is ONLY a read-only snapshot. It never asserts an F9/F10 action worked.
function M.Snapshot()
    local current, reason = world_snapshot()
    if not current then return unavailable(reason) end
    return {state="confirmed", data=current}
end

local function preflight(fields, input_clear, action)
    local session = key_identity(fields, action)
    if session == nil then
        return failed("GAME_PRECONDITION_FAILED", "Missing exact session identity or invalid F9/F10 slot")
    end
    local query_only = fields.queryOnly == "1"
    if fields.queryOnly ~= nil and not query_only then
        return failed("GAME_PRECONDITION_FAILED", "Unknown relocation observation mode")
    end
    if not query_only and (type(input_clear) ~= "function" or input_clear() ~= true) then
        return failed("GAME_PRECONDITION_FAILED", "Game text-input focus is not verified clear")
    end
    local snapshot, reason = world_snapshot()
    if not snapshot then
        -- A source/world identity change invalidates prior before states.
        baseline = {}
        last_owner = nil
        return unavailable(reason)
    end
    local owner_token = session .. "/" .. snapshot.ownerUid .. "/" .. snapshot.sourceServerId
    if owner_token ~= last_owner then
        baseline = {}
        last_owner = owner_token
    end
    if query_only and baseline[action] == nil then
        return failed("GAME_PRECONDITION_FAILED", "No owned relocation preflight to compare")
    end
    if not query_only then baseline[action] = snapshot.worldPointId end
    local before = baseline[action]
    local after = snapshot.worldPointId
    snapshot.beforeWorldPointId = before
    snapshot.afterWorldPointId = after
    snapshot.worldPointChanged = before ~= after
    snapshot.changeAttributedToHotkey = false
    snapshot.actionRequested = action
    snapshot.requiredUserStep = action == "randomRelocate"
        and "Select an exact native random-relocation item/mode and handle every item warning"
        or "Choose a native alliance rally destination, validate land and confirm any item/cost"
    -- Even if an external/manual relocation changed the city coordinate after
    -- this snapshot, it cannot turn a never-sent hotkey into a success.
    return unavailable(action == "randomRelocate"
        and "No source-verified F9 random destination, owned item mapping or warning/confirmation producer"
        or "No source-verified F10 chosen alliance rally target and validated UI confirmation", snapshot)
end

function M.RandomRelocate(fields, input_clear)
    return preflight(fields, input_clear, "randomRelocate")
end

function M.AllianceRelocate(fields, input_clear)
    return preflight(fields, input_clear, "allianceRelocate")
end

return M
