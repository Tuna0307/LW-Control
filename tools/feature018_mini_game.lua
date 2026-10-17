-- FEATURE-RECOVERY-018 Mini Games native game-specific actions.
--
-- Source: current original Lua v26 (the original LuaEntry SHA-256 is
-- 50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137).
-- DataCenter/LandLock/LandLockManager.luac lines 467-528, 1336-1342
-- DataCenter/LandLock/LandLockBubbleManager.luac lines 114-172
-- Net/Msgs/PushUnlockLandMessage.luac lines 15-25
-- DataCenter/LWSheep/LWSheepDataManager.luac lines 157-162
-- UI/LWTreasureChest/UITreasureChestView.luac lines 106-124, 179-243, 302-371
-- This module does no IO or game work until the owner-lease authenticated
-- feature018 bridge invokes a registered handler. Never report a network
-- dispatch as game-state success. No package/installed-file mutation.

local M = { VERSION = "feature018-mini-game-1" }
local last_land_request_id = nil
local bound_chest_view = nil
local bound_sheep_view = nil
local watched_ui_classes = {}
local marker_owned_view = nil

local function member(obj, name)
    if obj == nil then return nil end
    local ok, value = pcall(function() return obj[name] end)
    if ok then return value end
    return nil
end

local function invoke(obj, name, ...)
    local fn = member(obj, name)
    if type(fn) ~= "function" then return false, nil, nil end
    -- DataCenter managers use colon-style calls. A fallback handles the
    -- native CS static wrappers, but only for source-verified named methods.
    local ok, a, b = pcall(fn, obj, ...)
    if ok then return true, a, b end
    local ok2, c, d = pcall(fn, ...)
    if ok2 then return true, c, d end
    return false, nil, nil
end

local function unsupported(reason, data)
    return { state = "unsupported", errorCode = "GAME_METHOD_UNAVAILABLE",
             error = reason, data = data }
end

local function blocked(reason, data)
    return { state = "failed", errorCode = "GAME_PRECONDITION_FAILED",
             error = reason, data = data }
end

local function is_integer(value)
    return type(value) == "number" and value > 0 and value % 1 == 0
end

local function land_context()
    local dc = rawget(_G, "DataCenter")
    local land = member(dc, "LandLockManager")
    local states = rawget(_G, "LandLockState")
    local conditions = rawget(_G, "LandLockCondition")
    if land == nil or states == nil or conditions == nil then return nil end
    if member(states, "Locked") == nil or member(states, "Unlocked") == nil or
       member(states, "Finished") == nil or member(conditions, "Unknown") == nil then
        return nil
    end
    return land, states, conditions
end

local function next_locked_land()
    local land, states, conditions = land_context()
    if land == nil then return nil, "LandLockManager or source enums unavailable" end
    local enabled_ok, enabled = invoke(land, "Enabled")
    if not enabled_ok or enabled ~= true then
        return nil, "Land-lock game feature is not enabled"
    end
    local ok, id = invoke(land, "GetCurrentToBeUnlockedId")
    if not ok or not is_integer(id) then return nil, "No currently locked land cell" end
    local row_ok, row = invoke(land, "GetLandLockDataById", id)
    if not row_ok or row == nil or member(row, "state") ~= member(states, "Locked") then
        return nil, "Current land cell does not have a verified Locked state"
    end
    return { manager = land, states = states, conditions = conditions, id = id, row = row }
end

local function land_status()
    local land, states = land_context()
    if land == nil then return unsupported("LandLockManager is unavailable") end
    if not is_integer(last_land_request_id) then
        local next_land, reason = next_locked_land()
        if next_land == nil then return unsupported(reason) end
        return { state = "confirmed", data = {
            cellId = next_land.id, cellType = "city_land_lock",
            state = "locked", kind = "city_land_lock",
            observation = "native_land_lock_state"
        } }
    end
    local ok, row = invoke(land, "GetLandLockDataById", last_land_request_id)
    if not ok or row == nil then return unsupported("Requested land state is not currently available") end
    local now = member(row, "state")
    local current_id = last_land_request_id
    if now == member(states, "Unlocked") or now == member(states, "Finished") then
        last_land_request_id = nil
        return { state = "confirmed", data = {
            cellId = current_id, cellType = "city_land_lock",
            state = "unlocked", kind = "city_land_lock",
            observation = "native_land_lock_state"
        } }
    end
    if now == member(states, "Locked") then
        return { state = "confirmed", data = {
            cellId = current_id, cellType = "city_land_lock",
            state = "locked", kind = "city_land_lock",
            observation = "native_land_lock_state", awaitingGameConfirmation = true
        } }
    end
    return unsupported("Requested land has not reached an independently confirmed unlock state")
end

local function land_unlock_one(fields)
    -- Caller has already passed the common bridge's authenticated lease, PID,
    -- challenge, profile/session and request replay gates.
    if last_land_request_id ~= nil then
        return blocked("Earlier land unlock is awaiting a native state transition",
                       { cellId = last_land_request_id })
    end
    local next_land, reason = next_locked_land()
    if next_land == nil then return unsupported(reason) end
    local id, row, land = next_land.id, next_land.row, next_land.manager
    if fields.expectedCellId ~= nil and tostring(id) ~= fields.expectedCellId then
        return blocked("The next locked land ID changed; refresh before acting", { cellId = id })
    end
    local pve_ok, pve = invoke(row, "GetCurPve")
    if not pve_ok or pve ~= 0 then
        return blocked("Native land unlock requires PVE progress", { cellId = id })
    end
    if member(row, "needPay") == true and member(row, "paid") ~= true then
        return blocked("Native land unlock requires a separate payment", { cellId = id })
    end
    local check_ok, condition = invoke(land, "CheckLandLockConditionById", id)
    if not check_ok or condition ~= member(next_land.conditions, "Unknown") then
        return blocked("Native land prerequisites are not satisfied", { cellId = id })
    end
    -- Original LandLockBubbleManager.OnClickBubble: 156-164 issues this exact
    -- message when Locked, no PVE, and no unpaid resources. Fail closed unless
    -- the target methods and source MsgDefines enum exist.
    local send = member(rawget(_G, "SFSNetwork"), "SendMessage")
    local unlock = member(rawget(_G, "MsgDefines"), "UnlockUserLand")
    if type(send) ~= "function" or unlock == nil then
        return unsupported("Native UnlockUserLand game message is unavailable")
    end
    -- Just before sending, reject any land-state change from the original
    -- observed locked record. Source confirmation is server PushUnlockHandle.
    local check_row_ok, check_row = invoke(land, "GetLandLockDataById", id)
    if not check_row_ok or check_row == nil or
       member(check_row, "state") ~= member(next_land.states, "Locked") then
        return blocked("Land changed before the native request could be sent", { cellId = id })
    end
    local ok, send_result = pcall(send, unlock, id)
    if not ok or send_result == false then
        return blocked("Native UnlockUserLand dispatch failed", { cellId = id })
    end
    last_land_request_id = id
    return { state = "accepted", data = {
        cellId = id, cellType = "city_land_lock", kind = "city_land_lock", requestDispatched = true,
        awaitingGameConfirmation = true
    } }
end

local function sheep_status()
    local manager = member(rawget(_G, "DataCenter"), "LWSheepDataManager")
    if manager == nil then return unsupported("Native Food House sheep manager is unavailable") end
    local valid_ok, valid = invoke(manager, "IsVail")
    if not valid_ok or valid ~= true then
        return unsupported("Food House game data is unavailable or expired")
    end
    local level_ok, level = invoke(manager, "GetCurrentLevel")
    local game_ok, has_game = invoke(manager, "CheckHasGame")
    if not level_ok or not game_ok or type(level) ~= "number" then
        return unsupported("Food House game state cannot be verified")
    end
    return { state = "confirmed", data = {
        level = level, currentLevel = level,
        hasGame = has_game == true, game = "LWSheep",
        observation = "native_sheep_data_manager"
    } }
end

local function sheep_board(view)
    if view == nil then return unsupported("No genuine Season 4 Sheep game view was bound") end
    local engine = member(view, "engine")
    local game = member(engine, "game")
    local phase = member(game, "gameState")
    local enums = rawget(_G, "SheepGameState")
    if game == nil or enums == nil or phase ~= member(enums, "Doing") then
        return unsupported("A live native sheep board is not currently in the Doing state")
    end
    local visible = member(game, "visualList")
    local held = member(game, "removeList")
    if type(visible) ~= "table" or type(held) ~= "table" then
        return unsupported("Native sheep board or remove arena is unavailable")
    end
    local type_counts = {}
    local held_count = 0
    for _, card in ipairs(held) do
        held_count = held_count + 1
        local image_id = member(card, "pictureId")
        if type(image_id) == "number" then
            type_counts[image_id] = (type_counts[image_id] or 0) + 1
        end
    end
    local choices = {}
    local inspected = 0
    for grid_id, card in pairs(visible) do
        inspected = inspected + 1
        if inspected > 400 then
            return unsupported("Sheep visible-card count exceeds bounded read-only probe")
        end
        local picture_id = member(card, "pictureId")
        local eligible_ok, eligible = invoke(game, "CanClick", grid_id)
        local point_ok, point_id = invoke(card, "GetPosToServer")
        -- Both source-native CanClick and the card's own point serialization
        -- must admit a candidate. Never generate point IDs independently.
        if eligible_ok and eligible == true and point_ok and
           type(point_id) == "string" and point_id:match("^%-?%d+_%-?%d+_%-?%d+$") and
           type(picture_id) == "number" then
            choices[#choices + 1] = {
                gridId = grid_id, pointId = point_id, pictureId = picture_id,
                matchedInTray = type_counts[picture_id] or 0
            }
        end
    end
    table.sort(choices, function(a, b)
        if a.matchedInTray ~= b.matchedInTray then
            return a.matchedInTray > b.matchedInTray
        end
        if a.pictureId ~= b.pictureId then return a.pictureId < b.pictureId end
        return a.pointId < b.pointId
    end)
    local count = #choices
    while #choices > 20 do table.remove(choices) end
    return { state = "confirmed", data = {
        game = "LWSheep", phase = "doing", heldCount = held_count,
        playableCount = count, playable = choices,
        -- This is a conservative immediate-match hint; a complete solver
        -- must model hidden cards, undo items and server-confirmed steps.
        suggestionIsUnsolved = true,
        observation = "native_sheep_visual_list_and_can_click"
    } }
end

local function chest_snapshot(view)
    if view == nil then return unsupported("No genuine active TreasureChest view was bound") end
    -- UITreasureChestView.OnClickBox (source lines 382-397) only accepts a
    -- reward selection during the literal SwitchEnd phase, before received.
    if member(view, "switchState") ~= "SwitchEnd" or member(view, "received") == true then
        return unsupported("Chest shuffle has not completed or reward was received")
    end
    local index = member(view, "bestRewardIndex")
    if not is_integer(index) or index > 3 then
        local ok, value = invoke(view, "GetBigRewardBoxIndex")
        if not ok then return unsupported("Best-reward chest is not identified by game data") end
        index = value
    end
    if not is_integer(index) or index > 3 then
        return unsupported("No best reward is identified in native chest data")
    end
    local boxes = member(view, "boxItems")
    local item = member(boxes, index)
    if item == nil then return unsupported("Native chest item is not available") end
    local moving_ok, moving = invoke(item, "IsMoving")
    local pos_ok, pos = invoke(item, "GetCurPosition")
    if not moving_ok or not pos_ok or pos == nil then
        return unsupported("Native chest coordinates or movement phase not readable")
    end
    local x, y, z = member(pos, "x"), member(pos, "y"), member(pos, "z")
    if type(x) ~= "number" or type(y) ~= "number" or type(z) ~= "number" then
        return unsupported("Chest position is not a valid Unity Vector3")
    end
    if moving == true then return unsupported("Winning chest is still moving") end
    -- The Lua view provides 1-based identity and physical position; it does
    -- NOT provide a promise that the chest is clickable at this instant.
    return { state = "confirmed", data = {
        bestRewardBoxIndex = index, x = x, y = y, z = z,
        moving = false, nativeSwitchState = "SwitchEnd", markerEligible = true,
        observation = "native_treasure_chest_view"
    } }
end

-- Only the effect activated by this module may be hidden. A new view can
-- become active while the previous chest is still alive; retaining that
-- effect would leave a stale winning-chest marker on the previous shuffle.
local function clear_owned_chest_marker()
    local view = marker_owned_view
    if view == nil then return true end
    local effect = member(view, "switchEffect")
    if effect == nil or type(member(view, "HideSwitchEffect")) ~= "function" then
        return false
    end
    if member(effect, "activeSelf") ~= false then
        local ok = invoke(view, "HideSwitchEffect")
        if not ok or member(effect, "activeSelf") ~= false then return false end
    end
    marker_owned_view = nil
    return true
end

local function mark_chest()
    M.TryObserveUiClasses()
    local game_state = chest_snapshot(bound_chest_view)
    if game_state.state ~= "confirmed" then return game_state end
    local view = bound_chest_view
    if marker_owned_view ~= nil and marker_owned_view ~= view and
       not clear_owned_chest_marker() then
        return blocked("Previous owned chest marker could not be cleared")
    end
    local effect = member(view, "switchEffect")
    local show = member(view, "ShowSwitchEffect")
    if effect == nil or type(show) ~= "function" then
        return unsupported("Source-native TreasureChest switch effect is unavailable")
    end
    -- Preserve an effect that the original game currently owns (e.g. while
    -- the shuffle animation is finishing). Only restore our own addition.
    if marker_owned_view ~= view and member(effect, "activeSelf") == true then
        return blocked("The game's own chest switch effect is already active")
    end
    local data = game_state.data
    -- UITreasureChestView.ShowSwitchEffect (v26 lines 373-376) activates the
    -- already present effect and sets its position via Set_position(x,y,z).
    -- Reuse that native effect to mark the source-identified prize chest.
    local ok = invoke(view, "ShowSwitchEffect", data.x, data.y, data.z)
    if not ok then return blocked("Could not position native prize marker") end
    if member(effect, "activeSelf") ~= true then
        return blocked("Native prize marker activation was not observed")
    end
    marker_owned_view = view
    data.markerVisible = true
    data.markerSource = "UITreasureChestView.ShowSwitchEffect"
    return { state = "confirmed", data = data }
end

local function unmark_chest()
    M.TryObserveUiClasses()
    if marker_owned_view == nil then
        return unsupported("No owned chest marker is currently active")
    end
    -- Clean up the actual owned effect, even if another chest view has since
    -- been bound. Never hide the new view's native effect by mistake.
    if not clear_owned_chest_marker() then
        return blocked("Native prize marker cleanup was not observed")
    end
    return { state = "confirmed", data = { markerVisible = false,
        observation = "native_treasure_chest_switch_effect" } }
end

-- Only an actual view object from UI/LWTreasureChest/UITreasureChestView may
-- be passed by the host's validated UI hook. No speculative scene traversal.
function M.BindTreasureChestView(view)
    if view == nil then
        if not clear_owned_chest_marker() then return false end
        bound_chest_view = nil
        return true
    end
    local ok = type(member(view, "GetBigRewardBoxIndex")) == "function" and
               type(member(view, "GetBoxItem")) == "function" and
               member(view, "boxItems") ~= nil
    if not ok then return false end
    if bound_chest_view ~= view and not clear_owned_chest_marker() then
        return false
    end
    bound_chest_view = view
    return true
end

function M.BindSheepView(view)
    if view == nil then bound_sheep_view = nil; return true end
    local engine = member(view, "engine")
    local game = member(engine, "game")
    if type(member(view, "OnClickCard")) ~= "function" or
       type(member(game, "CanClick")) ~= "function" or
       type(member(engine, "ClickCard")) ~= "function" then
        return false
    end
    bound_sheep_view = view
    return true
end

-- Chest's view may be observed here; Season 4 Sheep is owned solely by the
-- dedicated solver's Update/OnDestroy wrappers and calls BindSheepView.
-- This prevents two independent modules from restoring overlapping wrappers.
local watched_view_paths = {
    { path = "UI.LWTreasureChest.UITreasureChestView", kind = "chest" },
}

function M.TryObserveUiClasses()
    local loaded = package and package.loaded
    if type(loaded) ~= "table" then return 0 end
    local attached = 0
    for _, entry in ipairs(watched_view_paths) do
        if watched_ui_classes[entry.path] ~= nil then
            attached = attached + 1
        else
            local cls = loaded[entry.path]
            local original_update = member(cls, "Update")
            local original_destroy = member(cls, "OnDestroy")
            if type(cls) == "table" and type(original_update) == "function" and
               type(original_destroy) == "function" then
                local update = function(view, ...)
                    if bound_chest_view ~= view then M.BindTreasureChestView(view) end
                    return original_update(view, ...)
                end
                local destroy = function(view, ...)
                    if marker_owned_view == view then
                        -- The original view is being destroyed. Release the
                        -- effect before its native objects become invalid.
                        clear_owned_chest_marker()
                        marker_owned_view = nil
                    end
                    if bound_chest_view == view then bound_chest_view = nil end
                    return original_destroy(view, ...)
                end
                local ok = pcall(function()
                    cls.Update = update
                    cls.OnDestroy = destroy
                end)
                if ok then
                    watched_ui_classes[entry.path] = {
                        class = cls, originalUpdate = original_update,
                        originalDestroy = original_destroy,
                        updateWrapper = update, destroyWrapper = destroy,
                    }
                    attached = attached + 1
                else
                    -- A class whose methods cannot be safely wrapped cannot
                    -- be observed; do not leave a half-installed replacement.
                    pcall(function()
                        if member(cls, "Update") == update then cls.Update = original_update end
                        if member(cls, "OnDestroy") == destroy then cls.OnDestroy = original_destroy end
                    end)
                end
            end
        end
    end
    return attached
end

function M.RemoveUiObservation()
    clear_owned_chest_marker()
    marker_owned_view = nil
    for path, saved in pairs(watched_ui_classes) do
        -- Never overwrite a different mod's subsequently installed method.
        pcall(function()
            if member(saved.class, "Update") == saved.updateWrapper then
                saved.class.Update = saved.originalUpdate
            end
            if member(saved.class, "OnDestroy") == saved.destroyWrapper then
                saved.class.OnDestroy = saved.originalDestroy
            end
        end)
        watched_ui_classes[path] = nil
    end
    bound_chest_view = nil
    bound_sheep_view = nil
end

function M.CleanupAfterLeaseLoss()
    -- Owner's Overview Pump should call this on stale/absent lease AND on
    -- owner session transitions. Once the owner lease is gone, only undo
    -- our previous UI effect and restore our exact in-memory UI wrappers.
    M.RemoveUiObservation()
    last_land_request_id = nil
end

M.ProbeChest = chest_snapshot
M.MarkChest = mark_chest
M.UnmarkChest = unmark_chest
M.LandStatus = land_status
M.LandUnlockOne = land_unlock_one
M.SheepStatus = sheep_status
M.ProbeSheepBoard = sheep_board

function M.RegisterHandlers(bridge)
    if bridge == nil or type(bridge.RegisterHandler) ~= "function" then
        return false
    end
    local success = true
    success = bridge.RegisterHandler("monopoly_cell_open", land_unlock_one) and success
    success = bridge.RegisterHandler("mini_land_status", land_status) and success
    success = bridge.RegisterHandler("mini_sheep_status", sheep_status) and success
    success = bridge.RegisterHandler("mini_sheep_board", function()
        M.TryObserveUiClasses()
        return sheep_board(bound_sheep_view)
    end) and success
    success = bridge.RegisterHandler("mini_chest_position", function()
        M.TryObserveUiClasses()
        return chest_snapshot(bound_chest_view)
    end) and success
    success = bridge.RegisterHandler("mini_chest_mark", mark_chest) and success
    success = bridge.RegisterHandler("mini_chest_unmark", unmark_chest) and success
    return success
end

M.Register = M.RegisterHandlers

return M
