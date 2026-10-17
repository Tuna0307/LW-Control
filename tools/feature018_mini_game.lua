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
local last_observed_unlocked_id = nil
local bound_chest_view = nil
local bound_sheep_view = nil
local watched_ui_classes = {}
local marker_owned_view = nil
local marker_owned_target = nil
local marker_initializing_view = nil
local land_observer = nil
local land_proof = nil

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

-- The bridge fences its owner lease, but the native account can change while
-- an asynchronous packet is in flight. Re-read the original Player identity
-- and every native server getter at dispatch, serialization, reply and proof.
-- Never trust a request-supplied UID or the currently selected UI profile.
local function native_owner_verified()
    local player = member(rawget(_G, "LuaEntry"), "Player")
    if tostring(member(player, "uid")) ~= "3133546747002289" then return false end
    for _, getter in ipairs({ "GetSourceServerId", "GetSelfServerId", "GetCurServerId" }) do
        local ok, server = invoke(player, getter)
        if not ok or tostring(server) ~= "2289" then return false end
    end
    return true
end

-- v26 SFSNetwork creates a NEW EMPTY message for incoming replies, so the
-- outbound and inbound message objects cannot be used as a request nonce.
-- Observe the real serializer and the exact UnlockUserLand reply landId;
-- never promote PushUnlockLand or a changed next-cell index into this ACK.
local function install_land_observer()
    if land_observer ~= nil then
        return package ~= nil and package.loaded ~= nil and
               package.loaded["Net.Msgs.UnlockUserLandMessage"] == land_observer.class and
               land_observer.class.OnCreate == land_observer.create and
               land_observer.class.HandleMessage == land_observer.reply
    end
    local path = "Net.Msgs.UnlockUserLandMessage"
    local cls = package and package.loaded and package.loaded[path]
    if cls == nil then
        local ok, loaded = pcall(require, path)
        if ok then cls = loaded end
    end
    if type(cls) ~= "table" or type(cls.OnCreate) ~= "function" or
       type(cls.HandleMessage) ~= "function" then return false end
    local original_create, original_reply = cls.OnCreate, cls.HandleMessage
    local create = function(self, id, ...)
        local proof = land_proof
        if proof ~= nil and tonumber(id) == proof.cellId then
            proof.targetRequestCount = proof.targetRequestCount + 1
            if proof.dispatching then proof.ownRequestObserved = true end
            if not native_owner_verified() then proof.ownerMismatchSeen = true end
        end
        return original_create(self, id, ...)
    end
    local reply = function(self, packet, ...)
        local proof = land_proof
        local id = tonumber(member(packet, "landId"))
        -- Preserve the exact original handler, including errors, rewards and
        -- manager update ordering. Observe poststate only AFTER it returns.
        local result = original_reply(self, packet, ...)
        if proof ~= nil and land_proof == proof and id == proof.cellId then
            proof.replyCount = proof.replyCount + 1
            proof.replyObserved = true
            if proof.targetRequestCount ~= 1 or not proof.ownRequestObserved then
                proof.replyBeforeSoleRequest = true
            end
            if not native_owner_verified() then proof.ownerMismatchSeen = true end
            proof.replyLandId = id
            proof.replyErrorCode = member(packet, "errorCode")
            proof.replyNativeState = member(packet, "state")
            local dc = rawget(_G, "DataCenter")
            local manager = member(dc, "LandLockManager")
            local ok, row = invoke(manager, "GetLandLockDataById", id)
            local states = rawget(_G, "LandLockState")
            local state = ok and member(row, "state") or nil
            proof.replyAfterUnlocked = not proof.ownerMismatchSeen and state ~= nil and
                (state == member(states, "Unlocked") or state == member(states, "Finished"))
        end
        return result
    end
    cls.OnCreate, cls.HandleMessage = create, reply
    land_observer = { class = cls, originalCreate = original_create,
        originalReply = original_reply, create = create, reply = reply }
    return true
end

local function land_receipt(id, fresh_unlocked)
    local proof = land_proof
    if proof == nil or proof.cellId ~= id then return nil end
    local intact = land_observer ~= nil and
        package ~= nil and package.loaded ~= nil and
        package.loaded["Net.Msgs.UnlockUserLandMessage"] == land_observer.class and
        land_observer.class.OnCreate == land_observer.create and
        land_observer.class.HandleMessage == land_observer.reply
    local owner_intact = native_owner_verified() and not proof.ownerMismatchSeen
    return {
        requestId = proof.requestId, beforeState = "locked",
        targetRequestCount = proof.targetRequestCount,
        ownRequestObserved = proof.ownRequestObserved == true,
        replyCount = proof.replyCount, replyObserved = proof.replyObserved == true,
        replyLandId = proof.replyLandId, replyErrorCode = proof.replyErrorCode,
        replyNativeState = proof.replyNativeState,
        replyAfterUnlocked = proof.replyAfterUnlocked == true,
        observerIntact = intact, accountFenceIntact = owner_intact,
        freshPostStateVerified = fresh_unlocked == true,
        requestCausalityConfirmed = intact and owner_intact and fresh_unlocked == true and
            not proof.replyBeforeSoleRequest and proof.dispatched == true and
            proof.targetRequestCount == 1 and proof.ownRequestObserved == true and
            proof.replyCount == 1 and proof.replyErrorCode == nil and
            proof.replyAfterUnlocked == true,
        observation = "UnlockUserLandMessage.OnCreate+HandleMessage",
    }
end

local function land_confirmed(data)
    data.requestProof = land_receipt(data.cellId, data.state == "unlocked")
    return { state = "confirmed", data = data }
end

local function land_state_context()
    local dc = rawget(_G, "DataCenter")
    local land = member(dc, "LandLockManager")
    local states = rawget(_G, "LandLockState")
    if land == nil or states == nil then return nil end
    if member(states, "Locked") == nil or member(states, "Unlocked") == nil or
       member(states, "Finished") == nil then
        return nil
    end
    return land, states
end

local function land_context()
    local land, states = land_state_context()
    local conditions = rawget(_G, "LandLockCondition")
    if land == nil or conditions == nil or member(conditions, "Unknown") == nil then
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

local function land_status(fields)
    -- Read-only exact-state observation depends only on the original manager
    -- and state enum. LandLockCondition is required for a send preflight, not
    -- for reading a previously addressed server-updated land row.
    local land, states = land_state_context()
    if land == nil then return unsupported("LandLockManager is unavailable") end
    -- A successful v26 UnlockUserLandMessage passes its response to
    -- LandLockManager.UnlockHandle, whose RefreshDataByServer updates this
    -- *specific* land ID. GetCurrentToBeUnlockedId instead iterates pairs
    -- over Locked rows; a later next-locked ID cannot prove the prior ACK.
    -- Permit a read-only exact-ID query even after the pending latch cleared
    -- or the bridge module was reloaded. Never re-dispatch from status.
    if type(fields) == "table" and fields.expectedCellId ~= nil then
        local raw_id = fields.expectedCellId
        if type(raw_id) ~= "string" or #raw_id > 10 or
           not raw_id:match("^[1-9]%d*$") then
            return blocked("Exact land status requires a positive integer cell ID")
        end
        local id = tonumber(raw_id)
        if not is_integer(id) or id > 10000 then
            return blocked("Exact land status cell ID is out of native range")
        end
        local ok, row = invoke(land, "GetLandLockDataById", id)
        if not ok or row == nil then
            return unsupported("Specified native land cell does not exist", { cellId = id })
        end
        local now = member(row, "state")
        local resolved = now == member(states, "Unlocked") or
                         now == member(states, "Finished")
        if not resolved and now ~= member(states, "Locked") then
            return unsupported("Specified native land cell has no supported lock state",
                               { cellId = id })
        end
        local matching_pending = id == last_land_request_id
        local proof = land_receipt(id, resolved)
        if resolved and matching_pending and proof ~= nil and
           proof.requestCausalityConfirmed then
            last_land_request_id = nil
            last_observed_unlocked_id = id
        end
        return land_confirmed({
            cellId = id, cellType = "city_land_lock", kind = "city_land_lock",
            state = resolved and "unlocked" or "locked",
            nativeLandState = now == member(states, "Finished") and "finished" or
                              (resolved and "unlocked" or "locked"),
            exactCellLookup = true,
            observation = "native_land_lock_state",
            awaitingGameConfirmation = not resolved and matching_pending or nil,
            awaitingCorrelatedReply = matching_pending and
                (proof == nil or not proof.requestCausalityConfirmed) or nil
        })
    end
    if not is_integer(last_land_request_id) then
        -- Preserve a prior accepted request's verified transition on later
        -- polls after the unlocked cell has ceased being the first Locked
        -- row. Re-read the exact ID each time; never replay a stale receipt
        -- after the native row disappears or returns to Locked.
        local observed = nil
        if is_integer(last_observed_unlocked_id) then
            local previous_ok, previous = invoke(land, "GetLandLockDataById",
                                                 last_observed_unlocked_id)
            local previous_state = previous_ok and member(previous, "state") or nil
            if previous_state == member(states, "Unlocked") or
               previous_state == member(states, "Finished") then
                observed = last_observed_unlocked_id
            else
                last_observed_unlocked_id = nil
            end
        end
        local next_land, reason = next_locked_land()
        if next_land == nil then
            if observed ~= nil then
                return land_confirmed({
                    cellId = observed, cellType = "city_land_lock",
                    state = "unlocked", kind = "city_land_lock",
                    observation = "native_land_lock_state",
                    lastObservedUnlockedCellId = observed
                })
            end
            return unsupported(reason)
        end
        return land_confirmed({
            cellId = next_land.id, cellType = "city_land_lock",
            state = "locked", kind = "city_land_lock",
            observation = "native_land_lock_state",
            lastObservedUnlockedCellId = observed
        })
    end
    local ok, row = invoke(land, "GetLandLockDataById", last_land_request_id)
    if not ok or row == nil then return unsupported("Requested land state is not currently available") end
    local now = member(row, "state")
    local current_id = last_land_request_id
    if now == member(states, "Unlocked") or now == member(states, "Finished") then
        local proof = land_receipt(current_id, true)
        if proof ~= nil and proof.requestCausalityConfirmed then
            last_land_request_id = nil
            last_observed_unlocked_id = current_id
        end
        return land_confirmed({
            cellId = current_id, cellType = "city_land_lock",
            state = "unlocked", kind = "city_land_lock",
            observation = "native_land_lock_state",
            awaitingCorrelatedReply = proof == nil or
                not proof.requestCausalityConfirmed
        })
    end
    if now == member(states, "Locked") then
        return land_confirmed({
            cellId = current_id, cellType = "city_land_lock",
            state = "locked", kind = "city_land_lock",
            observation = "native_land_lock_state", awaitingGameConfirmation = true
        })
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
    if not native_owner_verified() then
        return blocked("Land unlock requires UID 3133546747002289 on native server 2289")
    end
    if type(fields) ~= "table" then
        return blocked("Land unlock requires a valid native request")
    end
    local next_land, reason = next_locked_land()
    if next_land == nil then return unsupported(reason) end
    local id, row, land = next_land.id, next_land.row, next_land.manager
    if fields.expectedCellId ~= nil and
       (type(fields.expectedCellId) ~= "string" or
        tostring(id) ~= fields.expectedCellId) then
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
    if not install_land_observer() then
        return unsupported("Native UnlockUserLand packet observer is unavailable; no unlock sent")
    end
    -- Just before sending, reject any land-state change from the original
    -- observed locked record. A local request ACK is UnlockHandle; a push
    -- alone can update the row but cannot prove this request caused it.
    local check_row_ok, check_row = invoke(land, "GetLandLockDataById", id)
    if not check_row_ok or check_row == nil or
       member(check_row, "state") ~= member(next_land.states, "Locked") then
        return blocked("Land changed before the native request could be sent", { cellId = id })
    end
    if not native_owner_verified() then
        return blocked("Native land owner changed before send", { cellId = id })
    end
    land_proof = { cellId = id, requestId = fields.requestId,
        targetRequestCount = 0, replyCount = 0, dispatching = true }
    -- Latch BEFORE native code: a throw can follow a packet being sent.
    -- No exception or timeout permits an automatic second unlock request.
    last_land_request_id = id
    local ok, send_result = pcall(send, unlock, id)
    land_proof.dispatching = false
    -- Original v26 SFSNetwork.SendMessage: NewMessage(...) serializes via
    -- OnCreate before SendLuaMessage. If our exact serializer was not called
    -- once, packet submission is uncertain even if SendMessage returned.
    land_proof.dispatched = ok and send_result ~= false and
        land_proof.ownRequestObserved == true and
        land_proof.targetRequestCount == 1 and not land_proof.ownerMismatchSeen and
        native_owner_verified() and install_land_observer()
    if not ok or send_result == false then
        return blocked("Native UnlockUserLand dispatch failed", { cellId = id })
    end
    if not land_proof.dispatched then
        return blocked("Native UnlockUserLand serializer or account changed; send uncertain",
                       { cellId = id })
    end
    return { state = "accepted", data = {
        cellId = id, cellType = "city_land_lock", kind = "city_land_lock", requestDispatched = true,
        awaitingGameConfirmation = true, requestProof = land_receipt(id)
    } }
end

local function sheep_status()
    local manager = member(rawget(_G, "DataCenter"), "LWSheepDataManager")
    if manager == nil then return unsupported("Native Food House sheep manager is unavailable") end
    local activity_ok, activity = invoke(manager, "GetActivityData")
    local valid_ok, valid = invoke(manager, "IsVail")
    if not valid_ok or valid ~= true then
        return unsupported("Food House game data is unavailable or expired", {
            availability = activity_ok and activity == nil and "LIVE_EVENT_UNAVAILABLE" or "LIVE_PRECONDITION_FAILED",
            activityObserved = activity_ok, activityPresent = activity ~= nil,
            nativeIsVail = valid_ok and valid == true,
            sheepDataPresent = member(manager, "sheepData") ~= nil,
            observation = "LWSheepDataManager.GetActivityData+IsVail",
        })
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

-- Frontline Breakthrough G: reproduce the existing Parkour game's *single*
-- reinforcement producer five times, not AddMember(heroId, 5). Original v26:
-- ParkourBattleLogic.AddMember (3769-3773) forwards four arguments to team;
-- ParkourTeam.AddMember (396-417) passes its second argument to GenHeroCfg as
-- the HERO LEVEL, and creates only ONE unit per invocation. The genuine
-- StaticNumberDoorMonster.OnCollisionViewHandle (165-166) calls
-- logic:AddMember(controlHeroId, heroLevel, true). Never derive IDs/levels from
-- arbitrary caller input; take both from an already living native team hero.
local function frontline_context()
    if not native_owner_verified() then
        return nil, "Native Frontline requires authorized UID/server 2289"
    end
    local battle = member(rawget(_G, "DataCenter"), "LWBattleManager")
    local pve = rawget(_G, "PVEType")
    local ui = rawget(_G, "UIManager")
    local window_name = member(rawget(_G, "UIWindowNames"), "UIParkourBattleMain")
    if battle == nil or pve == nil or member(pve, "Parkour") == nil or
       ui == nil or window_name == nil then
        return nil, "Parkour battle manager, type or native window is unavailable"
    end
    local type_ok, battle_type = invoke(battle, "GetCurBattleType")
    local logic_ok, logic = invoke(battle, "GetCurBattleLogic")
    if not type_ok or battle_type ~= member(pve, "Parkour") or not logic_ok or
       logic == nil or member(battle, "curLogic") ~= logic or
       member(battle, "logic") ~= logic then
        return nil, "The current native battle is not this Parkour logic"
    end
    -- Original ActFrontBreakSundayData.EnterStage (v26 lines 85-90) marks its
    -- exact Parkour launch parameter. Generic Parkour battles also pass the
    -- type/window gate, but may not receive Frontline's G reinforcement.
    local event = member(logic, "param")
    if member(event, "fromActFrontBreakSunday") ~= true or
       not is_integer(member(event, "frontBreakSundayActId")) then
        return nil, "Current Parkour battle is not a Frontline Breakthrough event"
    end
    local ui_ok, instance = invoke(ui, "GetInstance")
    local open_ok, is_open = invoke(instance, "IsWindowOpen", window_name)
    if not ui_ok or instance == nil or not open_ok or is_open ~= true then
        return nil, "Parkour main battle window is not open"
    end
    local end_ok, ended = invoke(battle, "IsBattleFinish")
    if not end_ok or ended ~= false or member(battle, "gameStart") ~= true or
       member(battle, "gameOver") == true or member(battle, "gamePause") == true then
        return nil, "Parkour has not started or battle already finished"
    end
    local team = member(logic, "team")
    if team == nil or member(team, "logic") ~= logic or
       type(member(logic, "AddMember")) ~= "function" or
       type(member(logic, "GetMemberCount")) ~= "function" or
       type(member(team, "GetMemberCount")) ~= "function" then
        return nil, "The source-native Parkour team or reinforcement API is unavailable"
    end
    local defaults = member(team, "defaultHeroIds")
    local slots = member(team, "teamUnits")
    local primary_id = member(defaults, 1)
    if not is_integer(primary_id) or type(slots) ~= "table" then
        return nil, "No source-native primary hero identity is available"
    end
    local level = nil
    local found = false
    for _, unit in pairs(slots) do
        local hero = member(unit, "hero")
        if member(unit, "originalHeroId") == primary_id and
           member(hero, "heroId") == primary_id then
            local candidate_level = member(hero, "level")
            if not is_integer(candidate_level) then
                return nil, "Primary live hero level cannot be verified"
            end
            if level ~= nil and candidate_level ~= level then
                return nil, "Primary hero levels disagree in the native team"
            end
            found = true
            level = candidate_level
        end
    end
    if not found then return nil, "The native primary hero is no longer alive" end
    local count_ok, count = invoke(logic, "GetMemberCount")
    local team_ok, team_count = invoke(team, "GetMemberCount")
    local allocated = member(logic, "unitGuid")
    if not count_ok or not team_ok or type(count) ~= "number" or
       count % 1 ~= 0 or count < 1 or team_count ~= count or
       not is_integer(allocated) then
        return nil, "Native Parkour team count or GUID allocator is unreadable"
    end
    return { battle = battle, logic = logic, team = team, heroId = primary_id,
             heroLevel = level, count = count,
             eventId = member(event, "frontBreakSundayActId"),
             lastGuid = allocated }
end

local function frontline_units(team)
    -- Original ParkourTeam.AddUnitToTeam stores each member in teamUnits;
    -- MemberUnit.Init (v26 lines 28-44) assigns a per-unit guid from
    -- ParkourBattleLogic.AllotUnitGuid. A +1 counter by itself is ambiguous
    -- if another gameplay event adds/removes a unit in the same frame.
    local slots = member(team, "teamUnits")
    if type(slots) ~= "table" then return nil end
    local refs, ids = {}, {}
    local count = 0
    for _, unit in pairs(slots) do
        if unit ~= nil then
            local guid = member(unit, "guid")
            -- Source MemberUnit.Init:32 obtains a numeric GUID solely through
            -- ParkourBattleLogic.AllotUnitGuid (+1). String GUIDs are not
            -- genuine source-created soldier identities.
            if not is_integer(guid) then return nil end
            local id = tostring(guid)
            if ids[id] then return nil end
            ids[id] = true
            refs[unit] = id
            count = count + 1
            if count > 2000 then return nil end
        end
    end
    return refs, ids, count
end

local frontline_active = false
local frontline_reentry_seen = false

local function frontline_reinforce_once(fields)
    -- Source-native producer invoked solely through the authenticated owner
    -- hotkey command. No stand-alone network sender or fabricated game state.
    if type(fields) ~= "table" or fields.action ~= "frontlineReinforce" or
       tostring(fields.slot) ~= "5" then
        return blocked("Only the native G / Frontline reinforcement intent is admitted")
    end
    local context, reason = frontline_context()
    if context == nil then return unsupported(reason) end
    local confirmed = 0
    for _ = 1, 5 do
        if frontline_reentry_seen then
            return blocked("Overlapping native Frontline G intent interrupted reinforcement", {
                beforeCount = context.count, observedAdded = confirmed,
                serverAckObserved = false,
                observationScope = "native_local_battle" })
        end
        -- Recheck the current battle, window and source player hero before
        -- every irreversible unit creation. Any missing receipt stops further
        -- additions and reports partial failure, never a claimed +5 success.
        local current, changed = frontline_context()
        if current == nil or current.logic ~= context.logic or
           current.team ~= context.team or current.heroId ~= context.heroId or
           current.heroLevel ~= context.heroLevel or
           current.eventId ~= context.eventId or
           current.lastGuid ~= context.lastGuid + confirmed or
           current.count ~= context.count + confirmed then
            return blocked(changed or "Native Parkour team changed during reinforcement", {
                beforeCount = context.count, observedAdded = confirmed,
                observation = "native_ParkourBattleLogic_GetMemberCount" })
        end
        local prior_units, prior_ids, prior_count = frontline_units(context.team)
        if prior_units == nil then
            return blocked("Parkour unit identities are unavailable before reinforcement", {
                beforeCount = context.count, observedAdded = confirmed })
        end
        -- Original StaticNumberDoorMonster's source UI-equivalent producer:
        -- exactly one soldier, same hero and level as the live primary hero.
        -- Call exactly once. invoke() has a fallback for mixed static/colon
        -- accessors; retrying a mutating method after an exception could
        -- duplicate a unit even though the first call already took effect.
        local ok = pcall(member(context.logic, "AddMember"), context.logic,
                         context.heroId, context.heroLevel, true)
        local count_ok, after = invoke(context.logic, "GetMemberCount")
        local after_units, _, after_count = frontline_units(context.team)
        local new_units = 0
        local identities_match = after_units ~= nil and after_count == prior_count + 1
        if identities_match then
            for unit, guid in pairs(after_units) do
                if prior_units[unit] ~= nil then
                    if prior_units[unit] ~= guid then identities_match = false end
                else
                    local hero = member(unit, "hero")
                    new_units = new_units + 1
                    if prior_ids[guid] or
                       member(unit, "guid") ~= member(context.logic, "unitGuid") or
                       member(unit, "originalHeroId") ~= context.heroId or
                       member(hero, "heroId") ~= context.heroId or
                       member(hero, "level") ~= context.heroLevel then
                        identities_match = false
                    end
                end
            end
            for unit in pairs(prior_units) do
                if after_units[unit] == nil then identities_match = false end
            end
        end
        if not ok or not count_ok or after ~= context.count + confirmed + 1 or
           member(context.logic, "unitGuid") ~= context.lastGuid + confirmed + 1 or
           not identities_match or new_units ~= 1 then
            return blocked("Native Parkour member addition was not confirmed", {
                beforeCount = context.count, observedAdded = confirmed,
                observedAfterCount = count_ok and after or nil,
                unitIdentityVerified = identities_match and new_units == 1 or false,
                observation = "native_ParkourBattleLogic_GetMemberCount" })
        end
        confirmed = confirmed + 1
    end
    return { state = "confirmed", data = {
        action = "frontlineReinforce", slot = 5, heroId = context.heroId,
        frontBreakSundayActId = context.eventId,
        beforeCount = context.count, afterCount = context.count + confirmed,
        addedMembers = confirmed, unitIdentityVerified = true,
        observedCreatedUnits = confirmed,
        guidAllocatorBefore = context.lastGuid,
        guidAllocatorAfter = context.lastGuid + confirmed,
        -- Original AddMember has no per-key server request or response. This
        -- confirms five distinct client battle units, not a server score ACK.
        serverAckObserved = false, observationScope = "native_local_battle",
        unitIdentitySource = "native_ParkourTeam_teamUnits_guid",
        observation = "native_ParkourBattleLogic_GetMemberCount"
    } }
end

local function frontline_reinforce(fields)
    -- Original ParkourBattleLogic.AddMember can invoke game callbacks while
    -- creating a MemberUnit. A nested hotkey intent within that callback must
    -- never start another five-add loop or turn the first loop into +10.
    if frontline_active then
        frontline_reentry_seen = true
        return blocked("Native Frontline G reinforcement is already in progress", {
            serverAckObserved = false,
            observationScope = "native_local_battle" })
    end
    frontline_active = true
    frontline_reentry_seen = false
    local ok, outcome = pcall(frontline_reinforce_once, fields)
    frontline_active = false
    if not ok then
        -- Do not retry a native producer after an exception; AddMember may
        -- have inserted a soldier before the source method failed.
        return blocked("Native Frontline G producer aborted; no automatic retry", {
            serverAckObserved = false,
            observationScope = "native_local_battle" })
    end
    if frontline_reentry_seen and outcome.state == "confirmed" then
        return blocked("Overlapping Frontline G intent was observed", {
            beforeCount = outcome.data.beforeCount,
            observedAdded = outcome.data.addedMembers,
            serverAckObserved = false,
            observationScope = "native_local_battle" })
    end
    return outcome
end

-- Original UITreasureChestCtrl.CloseSelf (v26 9-11) destroys the exact
-- UIWindowNames.UITreasureChest window. A bound Lua view can outlive the
-- visible window; only the original UIManager.IsWindowOpen can admit a mark.
local function native_chest_window_open()
    local name = member(rawget(_G, "UIWindowNames"), "UITreasureChest")
    local ui = rawget(_G, "UIManager")
    if name == nil or ui == nil then return false end
    local ok, instance = invoke(ui, "GetInstance")
    if not ok or instance == nil then return false end
    local open_ok, opened = invoke(instance, "IsWindowOpen", name)
    return open_ok and opened == true
end

local function chest_snapshot(view)
    if view == nil then return unsupported("No genuine active TreasureChest view was bound") end
    if not native_chest_window_open() then
        return unsupported("The original TreasureChest UI window is not currently open")
    end
    -- UITreasureChestView.OnClickBox (source lines 382-397) only accepts a
    -- reward selection during the literal SwitchEnd phase, before received.
    if member(view, "switchState") ~= "SwitchEnd" or member(view, "received") == true then
        return unsupported("Chest shuffle has not completed or reward was received")
    end
    if member(view, "curSelectIndex") ~= nil then
        return unsupported("A treasure chest has already been selected in this shuffle")
    end
    -- DataDefine caches this rewardDatas-derived result in bestRewardIndex.
    -- It must still agree with GetBigRewardBoxIndex on the current shuffle:
    -- an old cached index can point to a different physical chest.
    local ok_index, index = invoke(view, "GetBigRewardBoxIndex")
    if not ok_index or member(view, "bestRewardIndex") ~= index then
        return unsupported("Native best reward identity changed in this shuffle")
    end
    if not is_integer(index) or index > 3 then
        return unsupported("No best reward is identified in native chest data")
    end
    -- Source TreasureChestDataManager.GetBoxRewardDatas (v26 lines 45-55)
    -- constructs each reward row with IsBest. GetBigRewardBoxIndex returns
    -- the FIRST truthy IsBest (v26 lines 116-124), which is ambiguous if two
    -- rows are marked; a stale UI cached index alone is not winner proof.
    local rewards = member(view, "rewardDatas")
    if type(rewards) ~= "table" or #rewards ~= 3 then
        return unsupported("Source-native three-chest reward rows are unavailable")
    end
    local unique_best = nil
    for i = 1, 3 do
        local row = member(rewards, i)
        if row == nil then return unsupported("Original reward row was removed") end
        if member(row, "IsBest") == true then
            if unique_best ~= nil then
                return unsupported("Ambiguous multiple native grand-prize rows")
            end
            unique_best = i
        end
    end
    if unique_best ~= index then
        return unsupported("Native grand-prize row no longer matches the cached box index")
    end
    local boxes = member(view, "boxItems")
    local item = member(boxes, index)
    if item == nil then return unsupported("Native chest item is not available") end
    local item_ok, genuine = invoke(view, "GetBoxItem", index)
    if not item_ok or genuine ~= item then
        return unsupported("Native chest item no longer matches the reward index")
    end
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
    marker_owned_target = nil
    marker_initializing_view = nil
    return true
end

local function chest_effect_at(view, x, y, z)
    -- Original UITreasureChestView.ShowSwitchEffect writes the Unity transform
    -- position, and UITreasureChestItem.GetCurPosition reads the same property.
    -- An active effect alone does not establish where the GUI marker appeared.
    local effect = member(view, "switchEffect")
    if member(effect, "activeSelf") ~= true then return false end
    local pos = member(member(effect, "transform"), "position")
    local px, py, pz = member(pos, "x"), member(pos, "y"), member(pos, "z")
    local function near(a, b)
        return type(a) == "number" and type(b) == "number" and
               a == a and b == b and math.abs(a - b) <= 0.001
    end
    return near(px, x) and near(py, y) and near(pz, z)
end

local function retire_chest_marker_if_ineligible(view)
    if marker_owned_view ~= view then return true end
    if marker_initializing_view == view then return true end
    -- Same-view state transitions are not view teardown. The source game can
    -- change SwitchEnd -> ShowBigReward, set curSelectIndex on OnClickBox, or
    -- set received on OnTreasureChestEndResult without hiding switchEffect.
    -- Also catch reward identity / physical-position drift on an unchanged
    -- SwitchEnd view: a previously valid marker must never point elsewhere.
    local target = marker_owned_target
    if target ~= nil then
        local current = chest_snapshot(view)
        if current.state == "confirmed" and
           current.data.bestRewardBoxIndex == target.index and
           member(view, "treasureChestId") == target.chestId and
           member(view, "rewardDatas") == target.rewards and
           member(target.rewards, target.index) == target.reward and
           member(view, "boxItems") == target.boxes and
           member(target.boxes, target.index) == target.box and
           chest_effect_at(view, target.x, target.y, target.z) and
           chest_effect_at(view, current.data.x, current.data.y, current.data.z) then
            return true
        end
    end
    return clear_owned_chest_marker()
end

local function mark_chest()
    if not native_owner_verified() then
        return blocked("Black Market marker requires authorized UID/server 2289")
    end
    M.TryObserveUiClasses()
    local watched = watched_ui_classes["UI.LWTreasureChest.UITreasureChestView"]
    if watched == nil or package == nil or package.loaded == nil or
       package.loaded["UI.LWTreasureChest.UITreasureChestView"] ~= watched.class or
       member(watched.class, "OnCreate") ~= watched.createWrapper or
       member(watched.class, "Update") ~= watched.updateWrapper or
       member(watched.class, "OnDestroy") ~= watched.destroyWrapper or
       member(watched.class, "ChangeSwitchState") ~= watched.stateWrapper or
       member(watched.class, "OnClickBox") ~= watched.clickWrapper or
       member(watched.class, "OnTreasureChestEndResult") ~= watched.resultWrapper or
       member(watched.class, "OnBtnCloseClick") ~= watched.closeWrapper or
       member(watched.class, "OnBtnBlackClick") ~= watched.blackWrapper then
        -- A different hook owner may have replaced our lifecycle observer.
        -- Do not leave an already-owned highlight stranded on the old view.
        if marker_owned_view ~= nil then clear_owned_chest_marker() end
        return blocked("Native TreasureChest lifecycle observer changed; marker cannot be maintained")
    end
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
    if marker_owned_view ~= view and member(effect, "activeSelf") ~= false then
        return blocked("The native chest switch effect is not confirmed inactive")
    end
    local data = game_state.data
    -- UITreasureChestView.ShowSwitchEffect (v26 lines 373-376) activates the
    -- already present effect and sets its position via Set_position(x,y,z).
    -- Reuse that native effect to mark the source-identified prize chest.
    -- Original v26 ShowSwitchEffect first calls SetActive(true), then
    -- transform:Set_position(x,y,z) (lines 374-375). A late positioning error
    -- can therefore leave an active marker behind. Own the previously
    -- verified inactive effect before the *single* native call and undo any
    -- partial activation. Retain ownership if cleanup itself is unsuccessful.
    marker_owned_view = view
    marker_owned_target = {index = data.bestRewardBoxIndex,
                           x = data.x, y = data.y, z = data.z,
                           chestId = member(view, "treasureChestId"),
                           rewards = member(view, "rewardDatas"),
                           reward = member(member(view, "rewardDatas"), data.bestRewardBoxIndex),
                           boxes = member(view, "boxItems"),
                           box = member(member(view, "boxItems"), data.bestRewardBoxIndex)}
    marker_initializing_view = view
    local ok = pcall(show, view, data.x, data.y, data.z)
    marker_initializing_view = nil
    local still_eligible = chest_snapshot(view)
    local target = marker_owned_target
    if not ok or not native_owner_verified() or still_eligible.state ~= "confirmed" or
       still_eligible.data.bestRewardBoxIndex ~= data.bestRewardBoxIndex or
       target == nil or member(view, "treasureChestId") ~= target.chestId or
       member(view, "rewardDatas") ~= target.rewards or
       member(target.rewards, target.index) ~= target.reward or
       member(view, "boxItems") ~= target.boxes or
       member(target.boxes, target.index) ~= target.box or
       not chest_effect_at(view, data.x, data.y, data.z) or
       not chest_effect_at(view, still_eligible.data.x,
                           still_eligible.data.y, still_eligible.data.z) then
        if not clear_owned_chest_marker() then
            return blocked("Native prize marker failed and owned effect cleanup is incomplete")
        end
        return blocked("Native prize marker position, activation or winning chest changed")
    end
    data.markerVisible = true
    data.markerPositionVerified = true
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
            local original_create = member(cls, "OnCreate")
            local original_destroy = member(cls, "OnDestroy")
            local original_state = member(cls, "ChangeSwitchState")
            local original_click = member(cls, "OnClickBox")
            local original_result = member(cls, "OnTreasureChestEndResult")
            local original_close = member(cls, "OnBtnCloseClick")
            local original_black = member(cls, "OnBtnBlackClick")
            if type(cls) == "table" and type(original_update) == "function" and
               type(original_create) == "function" and
               type(original_destroy) == "function" and
               type(original_state) == "function" and
               type(original_click) == "function" and
               type(original_result) == "function" and
               type(original_close) == "function" and
               type(original_black) == "function" then
                local update = function(view, ...)
                    if bound_chest_view ~= view then M.BindTreasureChestView(view) end
                    retire_chest_marker_if_ineligible(view)
                    local value = original_update(view, ...)
                    retire_chest_marker_if_ineligible(view)
                    return value
                end
                local create = function(view, ...)
                    -- Original OnCreate runs ComponentDefine and DataDefine,
                    -- which populate rewardDatas, boxItems and the best index.
                    -- Bind only after that original initialization succeeded.
                    local value = original_create(view, ...)
                    M.BindTreasureChestView(view)
                    return value
                end
                local state = function(view, next_state, ...)
                    if marker_owned_view == view and next_state ~= "SwitchEnd" then
                        clear_owned_chest_marker()
                    end
                    return original_state(view, next_state, ...)
                end
                local click = function(view, index, ...)
                    -- Clicking any box while SwitchEnd is a real selection,
                    -- not a reason to leave a winning-box hint visible.
                    if marker_owned_view == view and
                       member(view, "switchState") == "SwitchEnd" and
                       member(view, "received") ~= true and
                       is_integer(index) and index <= 3 then
                        clear_owned_chest_marker()
                    end
                    return original_click(view, index, ...)
                end
                local chest_result = function(view, ...)
                    local value = original_result(view, ...)
                    retire_chest_marker_if_ineligible(view)
                    return value
                end
                local close = function(view, ...)
                    if marker_owned_view == view then clear_owned_chest_marker() end
                    return original_close(view, ...)
                end
                local black = function(view, ...)
                    if marker_owned_view == view then clear_owned_chest_marker() end
                    return original_black(view, ...)
                end
                local destroy = function(view, ...)
                    if marker_owned_view == view then
                        -- The original view is being destroyed. Release the
                        -- effect before its native objects become invalid.
                        clear_owned_chest_marker()
                        marker_owned_view = nil
                        marker_owned_target = nil
                        marker_initializing_view = nil
                    end
                    if bound_chest_view == view then bound_chest_view = nil end
                    return original_destroy(view, ...)
                end
                local ok = pcall(function()
                    cls.OnCreate = create
                    cls.Update = update
                    cls.OnDestroy = destroy
                    cls.ChangeSwitchState = state
                    cls.OnClickBox = click
                    cls.OnTreasureChestEndResult = chest_result
                    cls.OnBtnCloseClick = close
                    cls.OnBtnBlackClick = black
                end)
                if ok then
                    watched_ui_classes[entry.path] = {
                        class = cls, originalUpdate = original_update,
                        originalCreate = original_create,
                        originalDestroy = original_destroy,
                        originalState = original_state, originalClick = original_click,
                        originalResult = original_result,
                        originalClose = original_close, originalBlack = original_black,
                        updateWrapper = update, createWrapper = create,
                        destroyWrapper = destroy,
                        stateWrapper = state, clickWrapper = click,
                        resultWrapper = chest_result,
                        closeWrapper = close, blackWrapper = black,
                    }
                    attached = attached + 1
                else
                    -- A class whose methods cannot be safely wrapped cannot
                    -- be observed; do not leave a half-installed replacement.
                    pcall(function()
                        if member(cls, "OnCreate") == create then cls.OnCreate = original_create end
                        if member(cls, "Update") == update then cls.Update = original_update end
                        if member(cls, "OnDestroy") == destroy then cls.OnDestroy = original_destroy end
                        if member(cls, "ChangeSwitchState") == state then cls.ChangeSwitchState = original_state end
                        if member(cls, "OnClickBox") == click then cls.OnClickBox = original_click end
                        if member(cls, "OnTreasureChestEndResult") == chest_result then cls.OnTreasureChestEndResult = original_result end
                        if member(cls, "OnBtnCloseClick") == close then cls.OnBtnCloseClick = original_close end
                        if member(cls, "OnBtnBlackClick") == black then cls.OnBtnBlackClick = original_black end
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
    marker_owned_target = nil
    marker_initializing_view = nil
    for path, saved in pairs(watched_ui_classes) do
        -- Never overwrite a different mod's subsequently installed method.
        pcall(function()
            if member(saved.class, "OnCreate") == saved.createWrapper then
                saved.class.OnCreate = saved.originalCreate
            end
            if member(saved.class, "Update") == saved.updateWrapper then
                saved.class.Update = saved.originalUpdate
            end
            if member(saved.class, "OnDestroy") == saved.destroyWrapper then
                saved.class.OnDestroy = saved.originalDestroy
            end
            if member(saved.class, "ChangeSwitchState") == saved.stateWrapper then
                saved.class.ChangeSwitchState = saved.originalState
            end
            if member(saved.class, "OnClickBox") == saved.clickWrapper then
                saved.class.OnClickBox = saved.originalClick
            end
            if member(saved.class, "OnTreasureChestEndResult") == saved.resultWrapper then
                saved.class.OnTreasureChestEndResult = saved.originalResult
            end
            if member(saved.class, "OnBtnCloseClick") == saved.closeWrapper then
                saved.class.OnBtnCloseClick = saved.originalClose
            end
            if member(saved.class, "OnBtnBlackClick") == saved.blackWrapper then
                saved.class.OnBtnBlackClick = saved.originalBlack
            end
        end)
        watched_ui_classes[path] = nil
    end
    bound_chest_view = nil
    bound_sheep_view = nil
    if land_observer ~= nil then
        local saved = land_observer
        if saved.class.OnCreate == saved.create then saved.class.OnCreate = saved.originalCreate end
        if saved.class.HandleMessage == saved.reply then saved.class.HandleMessage = saved.originalReply end
        land_observer = nil
    end
end

function M.CleanupAfterLeaseLoss()
    -- Owner's Overview Pump should call this on stale/absent lease AND on
    -- owner session transitions. Once the owner lease is gone, only undo
    -- our previous UI effect and restore our exact in-memory UI wrappers.
    M.RemoveUiObservation()
    -- Keep an uncertain one-shot request fenced across a transient owner
    -- lease loss. Read-only exact status can resolve it; cleanup is not ACK.
    last_observed_unlocked_id = nil
    land_proof = nil
end

M.ProbeChest = chest_snapshot
M.MarkChest = mark_chest
M.UnmarkChest = unmark_chest
M.LandStatus = land_status
M.LandUnlockOne = land_unlock_one
M.SheepStatus = sheep_status
M.ProbeSheepBoard = sheep_board
M.FrontlineReinforce = frontline_reinforce

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
