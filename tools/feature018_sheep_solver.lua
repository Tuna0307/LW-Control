-- FEATURE-RECOVERY-018, source-pinned Season 4 Food House auto clear.
-- Original admitted v26 modules, independently inspectable via
-- `python tools/feature018_mini_lua.py --entry <module> --method <method>`:
-- DataCenter/LWSheep/LWSheepCard.luac Bind:43-62, IsVisual:88-95,
-- GetPosToServer:120-122; LWSheep.luac BuildCard:229-254,
-- GetVisualCardByXYZ:539-566, CanClick:570-572, Click:684-725,
-- CheckGameState:485-497, OpCheck:814-960; GSheep.luac Start:99-116,
-- Move:197-203; LWUISheepCardItem.luac OnClickCard:123-141;
-- LWUISheepGameView.luac BuildSheepData:322-376,
-- SeasonSheepOpHandle:165-275, ISCanOp:418-426,
-- SyncServer:842-851, SyncFinish:853-866, Update:691-840.
--
-- Implementation policy: A source-validated board must yield a complete
-- item-free plan before any action. Every UI click is serialized against the
-- game's own confirmed SeasonSheepOp response; timeout never retries a send.
-- Only the entry level captured on Start may execute. Uses no paid props,
-- `NextLevel`, `Again`, `UseItem`, or remote/reconstructed move packet.

local M = { VERSION = 'feature018-sheep-solver-1' }
local run = nil
local observed = nil
local owner_class = nil
-- Stop/timeout may race a native Click already submitted by OnClickCard.
-- Until the original result handler consumes that exact sent syncOps entry,
-- starting a new run could attribute the old reply to the new run's point ID.
local unresolved_move = nil
-- GSheep.Start can send the source-native LWSheepStartGame request when
-- CheckNeedSendStartGameMsgOrOnlyCheck finds no resumable game (v26 GSheep
-- 99-116 / LWSheepDataManager 204-229). Stop/open timeout cannot unsend it.
-- Retain this fence across a stopped run or lease loss until an independent
-- current native Food House board is visible; never repeat an uncertain Start.
local unresolved_start = nil
local MAX_CARDS, MAX_SEARCH_NODES, SEARCH_SLICE = 108, 24000, 160
local MAX_MOVES, MOVE_TIMEOUT, BOARD_TIMEOUT = 108, 12, 18

local function get(obj, key)
    if obj == nil then return nil end
    local ok, value = pcall(function() return obj[key] end)
    return ok and value or nil
end

local function call(obj, key, ...)
    local fn = get(obj, key)
    if type(fn) ~= 'function' then return false end
    local ok, a, b = pcall(fn, obj, ...)
    if ok then return true, a, b end
    return false
end

local function result(state, error, data)
    return {state = state, errorCode = error and 'SHEEP_AUTO_' .. state:upper() or nil,
            error = error, data = data}
end

local function clock()
    return tonumber(os.time()) or 0
end

local function int(value)
    return type(value) == 'number' and value % 1 == 0 and value >= 0
end

local function dictionary_size(obj, max)
    if type(obj) ~= 'table' then return nil end
    local count = 0
    for _ in pairs(obj) do
        count = count + 1
        if count > max then return nil end
    end
    return count
end

local function approved_native_owner()
    -- The host authenticates its session, but a login/server transition can
    -- occur between an accepted Start and a later automated native UI click.
    local player = get(rawget(_G, 'LuaEntry'), 'Player')
    if tostring(get(player, 'uid')) ~= '2553945035002288' then return false end
    for _, getter in ipairs({'GetSourceServerId', 'GetSelfServerId', 'GetCurServerId'}) do
        local ok, server = call(player, getter)
        if not ok or tostring(server) ~= '2288' then return false end
    end
    return true
end

local function context()
    if not approved_native_owner() then
        return nil, 'Food House requires the authorized native UID/server 2288'
    end
    local small = get(rawget(_G, 'SmallGameManager'), 'GSheep')
    local manager = get(rawget(_G, 'DataCenter'), 'LWSheepDataManager')
    local source_enum = get(rawget(_G, 'MiniGameSource'), 'Season')
    if small == nil or manager == nil or source_enum == nil or
       get(small, 'source') ~= source_enum then
        return nil, 'Current small game is not the Season 4 sheep source'
    end
    local valid_ok, valid = call(manager, 'IsVail')
    local end_ok, ended = call(manager, 'IsEnd')
    local level_ok, level = call(manager, 'GetCurrentLevel')
    if not valid_ok or valid ~= true or not end_ok or ended ~= false or
       not level_ok or not int(level) or level < 1 then
        return nil, 'No current unexpired Season 4 Food House level'
    end
    return {small = small, manager = manager, level = level,
            blockId = get(get(manager, 'sheepData'), 'blockId')}
end

local function native_view()
    local view = observed
    local engine = get(view, 'engine')
    local game = get(engine, 'game')
    if type(get(view, 'ISCanOp')) ~= 'function' or
       type(get(view, 'SeasonSheepOpHandle')) ~= 'function' or
       type(get(engine, 'ClickCard')) ~= 'function' or
       type(get(game, 'CanClick')) ~= 'function' then return nil end
    return view, engine, game
end

local function is_sheep_window_open()
    -- The admitted current-client LuaScriptInterface.UIManager exposes
    -- IsWindowOpen(string), and GSheep.Enter uses UILUISheepCloud as its
    -- exact Season 4 game window (GSheep.luac lines 118-151).
    local ui = rawget(_G, 'UIManager')
    local name = get(rawget(_G, 'UIWindowNames'), 'UILUISheepCloud')
    if ui == nil or name == nil then return nil end
    local inst_ok, instance = call(ui, 'GetInstance')
    if not inst_ok or instance == nil then return nil end
    local open_ok, opened = call(instance, 'IsWindowOpen', name)
    if not open_ok or type(opened) ~= 'boolean' then return nil end
    return opened
end

local function board_snapshot(view, game)
    local states = rawget(_G, 'SheepGameState')
    if states == nil or get(game, 'gameState') ~= get(states, 'Doing') then
        return nil, 'Native sheep engine is outside Doing state'
    end
    local show = get(game, 'showCards')
    local visible = get(game, 'visualList')
    local tray = get(game, 'removeList')
    local temporary = get(game, 'temporaryList')
    if type(show) ~= 'table' or type(visible) ~= 'table' or type(tray) ~= 'table' then
        return nil, 'Missing native sheep board, visible list or remove arena'
    end
    -- Original LWSheep.BuildTemporaryList (v26 lines 383-399) holds additional
    -- playable tiles outside showCards/removeList. An item-free plan that omits
    -- these tiles would be incomplete, even when its ordinary card count is
    -- divisible by three. Do not send any move for this unsupported state.
    if dictionary_size(temporary, MAX_CARDS) ~= 0 then
        return nil, 'Native temporary sheep cards are outside the item-free planner'
    end
    local cards, seen, tray_counts, tray_len = {}, {}, {}, 0
    for _, card in ipairs(tray) do
        local picture = get(card, 'pictureId')
        if not int(picture) then return nil, 'Invalid native tray card' end
        tray_counts[picture] = (tray_counts[picture] or 0) + 1
        tray_len = tray_len + 1
        if tray_len >= 7 then return nil, 'Native tray is already at failure capacity' end
    end
    for _, floor in pairs(show) do
        if type(floor) ~= 'table' then return nil, 'Unexpected native sheep floor structure' end
        for id, card in pairs(floor) do
            local pos = get(card, 'pos')
            local x, y, z = get(pos, 'x'), get(pos, 'y'), get(pos, 'z')
            local picture = get(card, 'pictureId')
            if seen[id] or not int(id) or not int(x) or not int(y) or
               not int(z) or z < 1 or not int(picture) or
               get(card, 'gridId') ~= id then
                return nil, 'Unsupported or duplicate native sheep grid metadata'
            end
            local occ = get(card, 'occGridList')
            if type(occ) ~= 'table' or dictionary_size(occ, 4) ~= 4 then
                return nil, 'Native 2-by-2 occupancy footprint mismatch'
            end
            local point_ok, point = call(card, 'GetPosToServer')
            if not point_ok or type(point) ~= 'string' or
               not point:match('^%d+_%d+_%d+$') then
                return nil, 'Unable to preserve source-serialized sheep point ID'
            end
            local click_ok, click = call(game, 'CanClick', id)
            if not click_ok then return nil, 'Native CanClick unavailable' end
            cards[#cards + 1] = {id = id, x = x, y = y, z = z, kind = picture,
                                point = point, native = card, click = click == true}
            seen[id] = true
            if #cards > MAX_CARDS then return nil, 'Current level exceeds bounded solver limit' end
        end
    end
    if #cards == 0 then return nil, 'No current-level sheep cards remain' end
    table.sort(cards, function(a, b) return a.id < b.id end)
    local native_count = dictionary_size(visible, MAX_CARDS)
    if native_count == nil then return nil, 'Invalid native visible list' end
    -- The original game's 2x2 tile footprint is encoded in Bind, and
    -- IsVisual uses that footprint's 4 cover flags. We accept the inferred
    -- z-order only if EVERY inferred playable tile matches native CanClick.
    local playable = 0
    for i, a in ipairs(cards) do
        a.blockers = {}
        for j, b in ipairs(cards) do
            if i ~= j and b.z > a.z and math.abs(b.x - a.x) <= 1 and
               math.abs(b.y - a.y) <= 1 then
                a.blockers[#a.blockers + 1] = j
            end
        end
        if #a.blockers == 0 then playable = playable + 1 end
        local visual = get(visible, a.id) == a.native
        if visual ~= (#a.blockers == 0) or a.click ~= visual then
            return nil, 'Native CanClick disagrees with full-board cover reconstruction'
        end
    end
    if playable ~= native_count or (tray_len + #cards) % 3 ~= 0 then
        return nil, 'Native board visibility or three-match conservation failed'
    end
    return {cards = cards, tray = tray_counts, trayLen = tray_len, visible = playable}
end

local function plan_board(board)
    local cards, total = board.cards, #board.cards
    local removed, tray, path, cache = {}, {}, {}, {}
    for kind, n in pairs(board.tray) do tray[kind] = n end
    local expansions, exhausted = 0, false
    local function search(left, held)
        expansions = expansions + 1
        if expansions > MAX_SEARCH_NODES then exhausted = true; return false end
        if expansions % SEARCH_SLICE == 0 then coroutine.yield('searching', expansions) end
        if left == 0 then return held == 0 end
        local keybits = {}
        for index = 1, total do keybits[index] = removed[index] and '1' or '0' end
        local cache_key = table.concat(keybits)
        if cache[cache_key] then return false end
        local choices = {}
        for index, card in ipairs(cards) do
            if not removed[index] then
                local blocked = false
                for _, blocker in ipairs(card.blockers) do
                    if not removed[blocker] then blocked = true; break end
                end
                if not blocked then
                    local count = tray[card.kind] or 0
                    local next_held = count == 2 and held - 2 or held + 1
                    if next_held < 7 then
                        choices[#choices + 1] = {index = index, score = count * 100 +
                            math.floor(card.z) + #card.blockers, held = next_held}
                    end
                end
            end
        end
        table.sort(choices, function(a, b)
            if a.score ~= b.score then return a.score > b.score end
            return cards[a.index].id < cards[b.index].id
        end)
        for _, pick in ipairs(choices) do
            local index, kind = pick.index, cards[pick.index].kind
            local prior = tray[kind] or 0
            removed[index] = true
            tray[kind] = (prior + 1) % 3
            path[#path + 1] = cards[index].id
            if search(left - 1, pick.held) then return true end
            path[#path] = nil
            tray[kind] = prior
            removed[index] = nil
            if exhausted then return false end
        end
        cache[cache_key] = true
        return false
    end
    local solved = search(total, board.trayLen)
    if not solved then return nil, expansions, exhausted end
    local plan = {}
    local lookup = {}
    for _, card in ipairs(cards) do lookup[card.id] = card end
    for _, id in ipairs(path) do plan[#plan + 1] = lookup[id] end
    return plan, expansions, false
end

local function stop_internal(reason)
    if run == nil then return end
    run.error = reason
    if run.status ~= 'completed' then run.status = reason == 'stopped' and 'stopped' or 'solve_failed' end
    if run.pending ~= nil and not run.pending.ack then
        -- Retain the exact view and original pending record across Stop and
        -- a future lease transition; neither action cancels an in-flight send.
        -- Source LWUISheepGameView.Update may bind a *different* view before
        -- Stop or an asynchronous response. The send belongs to the click's
        -- ORIGINAL view, never whichever view was observed most recently.
        unresolved_move = {view = run.pending.view, pending = run.pending}
    end
    run.pending = nil
    -- The planner is a coroutine. Releasing the actual field on Stop also
    -- releases its captured source-native board while the game stays open.
    run.coroutine = nil
    run.initial = nil
end

local function executing_or_preparing()
    return run ~= nil and (run.status == 'opening' or run.status == 'solving' or
                           run.status == 'executing')
end

local function status()
    if run == nil then return result('confirmed', nil, {
        taskName = 'sheepGame', status = 'idle', running = false,
        awaitingServer = unresolved_move ~= nil or unresolved_start ~= nil,
        restartFenced = unresolved_move ~= nil or unresolved_start ~= nil
    }) end
    local active = run.status == 'opening' or run.status == 'solving' or run.status == 'executing'
    return result('confirmed', nil, {
        taskName = 'sheepGame', status = run.status, running = active,
        currentLevel = run.level, confirmedMoves = run.confirmed or 0,
        totalMoves = run.plan and #run.plan or 0,
        searchNodes = run.nodes or 0, elapsedSeconds = clock() - run.started,
        awaitingServer = run.pending ~= nil or unresolved_move ~= nil or unresolved_start ~= nil,
        restartFenced = unresolved_move ~= nil or unresolved_start ~= nil, error = run.error,
        terminal = not active,
        source = 'native_Season4_LWSheep_server_ack'
    })
end

local function hook_loaded_view()
    local loaded = package and package.loaded
    local module = 'UI.LWSeason4.LWUISheepGame.View.LWUISheepGameView'
    local cls = type(loaded) == 'table' and loaded[module] or nil
    if type(cls) ~= 'table' then return false end
    if owner_class ~= nil then
        -- A changed or replaced native callback cannot observe the original
        -- response's exact SyncFinish. Never send a UI click without the
        -- watcher that fences one sent syncOps item to one server reply.
        return owner_class.class == cls and
               get(cls, 'Update') == owner_class.update and
               get(cls, 'SeasonSheepOpHandle') == owner_class.response and
               get(cls, 'OnDestroy') == owner_class.destroy and
               get(cls, 'OnClickCard') == owner_class.click and
               get(cls, 'OnUseItem') == owner_class.item and
               get(cls, 'NextLevel') == owner_class.nextLevel and
               get(cls, 'Again') == owner_class.again
    end
    local prior_update = get(cls, 'Update')
    local prior_result = get(cls, 'SeasonSheepOpHandle')
    local prior_destroy = get(cls, 'OnDestroy')
    local prior_click = get(cls, 'OnClickCard')
    local prior_item = get(cls, 'OnUseItem')
    local prior_next = get(cls, 'NextLevel')
    local prior_again = get(cls, 'Again')
    if type(prior_update) ~= 'function' or type(prior_result) ~= 'function' or
       type(prior_destroy) ~= 'function' or type(prior_click) ~= 'function' or
       type(prior_item) ~= 'function' or type(prior_next) ~= 'function' or
       type(prior_again) ~= 'function' then return false end
    local function queued_point_count(view, point, require_sent)
        local ops = get(view, 'syncOps')
        if type(ops) ~= 'table' then return nil end
        local total = 0
        for _, op in ipairs(ops) do
            if get(op, 'serverPos') == point then
                if require_sent and get(op, 'isSend') ~= true then return nil end
                total = total + 1
            end
        end
        return total
    end
    local update = function(view, ...)
        if observed ~= view then
            if executing_or_preparing() and run.view ~= nil and run.view ~= view then
                stop_internal('Food House native view was replaced during the current level')
            end
            observed = view
        end
        local mini = rawget(_G, 'LWBridgeFeature018MiniGame')
        if type(get(mini, 'BindSheepView')) == 'function' then
            pcall(mini.BindSheepView, view)
        end
        return prior_update(view, ...)
    end
    local response = function(view, packet, ...)
        local pending = run and run.pending or nil
        local retired = unresolved_move ~= nil and unresolved_move.view == view and
                        unresolved_move.pending or nil
        local event = rawget(_G, 'SheepGameOpType')
        local is_click = pending ~= nil and pending.view == view and
                         pending.engine == get(view, 'engine') and
                         pending.game == get(get(view, 'engine'), 'game') and
                         event ~= nil and
                         get(packet, 'type') == get(event, 'Click')
        local is_retired_click = retired ~= nil and
                                 retired.engine == get(view, 'engine') and
                                 retired.game == get(get(view, 'engine'), 'game') and
                                 event ~= nil and
                                 get(packet, 'type') == get(event, 'Click') and
                                 get(packet, 'opPointId') == retired.point
        local queued_before = is_click and queued_point_count(view, pending.point, true) or nil
        local retired_before = is_retired_click and
                               queued_point_count(view, retired.point, true) or nil
        local ok, value = pcall(prior_result, view, packet, ...)
        if is_retired_click and unresolved_move ~= nil and
           unresolved_move.pending == retired then
            local retired_after = queued_point_count(view, retired.point, false)
            if ok and retired_before == 1 and retired_after == 0 and
               approved_native_owner() then
                unresolved_move = nil
            end
        end
        if pending ~= nil and run ~= nil and run.pending == pending and
           pending.view == view and is_click then
            if get(packet, 'opPointId') == pending.point then
                -- SeasonSheepOpHandle may ignore a packet on OpCheck failure.
                -- Only source SyncFinish draining our exact queued sent point
                -- on the original view establishes a native reply receipt.
                local queued_after = queued_point_count(view, pending.point, false)
                if ok and queued_before == 1 and queued_after == 0 then
                    pending.ack = true
                else
                    stop_internal('Native server Click was not reconciled with its sent move')
                end
            else
                stop_internal('Unrecognized manual or competing sheep move')
            end
        elseif pending ~= nil and run ~= nil and run.pending == pending and
               pending.view == view and event ~= nil and
               get(packet, 'type') == get(event, 'Error') then
            stop_internal('Server rejected sheep action')
        end
        if not ok then error(value) end
        return value
    end
    local destroy = function(view, ...)
        local owns_run = executing_or_preparing() and run.view == view
        if observed == view then
            observed = nil
            local mini = rawget(_G, 'LWBridgeFeature018MiniGame')
            if type(get(mini, 'BindSheepView')) == 'function' then
                pcall(mini.BindSheepView, nil)
            end
        end
        if owns_run then
            local pending = run.pending
            local game = get(get(view, 'engine'), 'game')
            local states = rawget(_G, 'SheepGameState')
            if pending ~= nil and pending.ack and pending.view == view and
               pending.game == game and run.plan ~= nil and
               run.confirmed + 1 == #run.plan and states ~= nil and
               get(game, 'gameState') == get(states, 'Success') and
               get(game, 'rewardsInfo') ~= nil then
                run.pending = nil
                run.confirmed = #run.plan
                run.status = 'completed'
            else
                stop_internal('Food House source view closed before completion')
            end
        end
        return prior_destroy(view, ...)
    end
    local click = function(view, ...)
        if executing_or_preparing() and not run.acting then
            stop_internal('User or another controller clicked the Food House board')
        end
        return prior_click(view, ...)
    end
    local item = function(view, ...)
        if executing_or_preparing() then
            stop_internal('Manual paid/free item use conflicts with item-free plan')
        end
        return prior_item(view, ...)
    end
    local next_level = function(view, ...)
        if executing_or_preparing() then
            stop_internal('Manual entry to next level invalidated current-level fence')
        end
        return prior_next(view, ...)
    end
    local again = function(view, ...)
        if executing_or_preparing() then
            stop_internal('Manual restart invalidated current-level plan')
        end
        return prior_again(view, ...)
    end
    local ok = pcall(function()
        cls.Update = update
        cls.SeasonSheepOpHandle = response
        cls.OnDestroy = destroy
        cls.OnClickCard = click
        cls.OnUseItem = item
        cls.NextLevel = next_level
        cls.Again = again
    end)
    if not ok then
        pcall(function()
            if cls.Update == update then cls.Update = prior_update end
            if cls.SeasonSheepOpHandle == response then cls.SeasonSheepOpHandle = prior_result end
            if cls.OnDestroy == destroy then cls.OnDestroy = prior_destroy end
            if cls.OnClickCard == click then cls.OnClickCard = prior_click end
            if cls.OnUseItem == item then cls.OnUseItem = prior_item end
            if cls.NextLevel == next_level then cls.NextLevel = prior_next end
            if cls.Again == again then cls.Again = prior_again end
        end)
        return false
    end
    owner_class = {class = cls, update = update, response = response,
                   destroy = destroy, click = click, item = item,
                   nextLevel = next_level, again = again, oldUpdate = prior_update,
                   oldResponse = prior_result, oldDestroy = prior_destroy,
                   oldClick = prior_click, oldItem = prior_item,
                   oldNext = prior_next, oldAgain = prior_again}
    return true
end

function M.BindView(view)
    if view == nil then observed = nil; return true end
    if type(get(view, 'SeasonSheepOpHandle')) ~= 'function' or
       type(get(view, 'ISCanOp')) ~= 'function' then return false end
    observed = view
    return true
end

function M.CleanupAfterLeaseLoss()
    stop_internal('stopped')
    local mini = rawget(_G, 'LWBridgeFeature018MiniGame')
    if type(get(mini, 'BindSheepView')) == 'function' then
        pcall(mini.BindSheepView, nil)
    end
    local saved = owner_class
    if saved ~= nil then
        pcall(function()
            if saved.class.Update == saved.update then saved.class.Update = saved.oldUpdate end
            if saved.class.SeasonSheepOpHandle == saved.response then
                saved.class.SeasonSheepOpHandle = saved.oldResponse
            end
            if saved.class.OnDestroy == saved.destroy then saved.class.OnDestroy = saved.oldDestroy end
            if saved.class.OnClickCard == saved.click then saved.class.OnClickCard = saved.oldClick end
            if saved.class.OnUseItem == saved.item then saved.class.OnUseItem = saved.oldItem end
            if saved.class.NextLevel == saved.nextLevel then saved.class.NextLevel = saved.oldNext end
            if saved.class.Again == saved.again then saved.class.Again = saved.oldAgain end
        end)
    end
    owner_class = nil
    observed = nil
    run = nil
end

local function start(request)
    if request == nil or request.taskName ~= 'sheepGame' then
        return result('unsupported', 'Only sheepGame automation is admitted')
    end
    if unresolved_move ~= nil then
        return result('failed', 'Prior Food House Click still awaits source-correlated server reconciliation; no restart')
    end
    if run ~= nil and (run.status == 'solving' or run.status == 'executing' or run.status == 'opening') then
        return result('failed', 'Current Food House level is already being processed')
    end
    local ctx, err = context()
    if ctx == nil then return result('unsupported', err) end
    if request.expectedLevel ~= nil and tostring(ctx.level) ~= request.expectedLevel then
        return result('failed', 'Current Season 4 level changed before Start')
    end
    local challenge_ok, can = call(ctx.manager, 'CanChallenge')
    if not challenge_ok or can ~= true then
        return result('unsupported', 'Current Food House challenge is unavailable or exhausted')
    end
    -- An old Lua view can remain bound when its native window is closed. A
    -- bound view alone does not prove an earlier Start reached the live game.
    local window_open = is_sheep_window_open()
    if window_open == nil then
        return result('unsupported', 'Native UIManager cannot establish Food House window ownership')
    end
    local view, _, game = native_view()
    if unresolved_start ~= nil then
        local pending = unresolved_start
        local snapshot = view ~= nil and window_open and board_snapshot(view, game) or nil
        if pending.manager ~= ctx.manager or pending.small ~= ctx.small or
           pending.level ~= ctx.level or pending.blockId ~= ctx.blockId or
           snapshot == nil then
            return result('failed', 'Prior native Food House Start remains unresolved; no duplicate Start')
        end
        -- Same original manager, current-level board and visible native window
        -- independently establish readiness without another Start send.
        unresolved_start = nil
    end
    local first = {started = clock(), level = ctx.level, blockId = ctx.blockId,
                   status = 'opening', confirmed = 0, nodes = 0}
    run = first
    hook_loaded_view()
    -- Source GSheep.Start may dispatch LWSheepStartGame before the native
    -- window exists, including when an old bound view has survived its close.
    -- Arm the single-attempt fence BEFORE entering that native producer.
    if not window_open then
        if not approved_native_owner() then
            stop_internal('Food House native account changed before Start')
            return result('failed', run.error)
        end
        unresolved_start = {manager = ctx.manager, small = ctx.small,
                            level = ctx.level, blockId = ctx.blockId}
        local call_ok = call(ctx.small, 'Start')
        if not call_ok then
            stop_internal('Native GSheep.Start may have submitted an uncertain start request')
            return result('failed', run.error)
        end
    end
    return result('accepted', nil, {accepted = true, taskName = 'sheepGame',
        currentLevel = first.level, status = 'opening',
        observation = 'source_native_GSheep.Start_or_existing_view'})
end

local function stop(request)
    if request == nil or request.taskName ~= 'sheepGame' then
        return result('unsupported', 'Only sheepGame stop is admitted')
    end
    if run ~= nil and (run.status == 'opening' or run.status == 'solving' or run.status == 'executing') then
        stop_internal('stopped')
    end
    return status()
end

local function ensure_same_stage()
    local ctx, err = context()
    if ctx == nil then return nil, err end
    if run.level ~= ctx.level or run.blockId ~= ctx.blockId then
        return nil, 'The native current-level identity changed'
    end
    return ctx
end

function M.Pump()
    if run == nil then return false end
    local status_now = run.status
    if status_now ~= 'opening' and status_now ~= 'solving' and status_now ~= 'executing' then return false end
    local observed_hooks_intact = hook_loaded_view()
    local view, engine, game = native_view()
    local ctx, err = ensure_same_stage()
    if ctx == nil then
        -- The final server response can advance passMaxBlock/currentLevel.
        -- Completion requires BOTH its correlated Click acknowledgement and
        -- source-native game Success with the server reward payload.
        local pending = run.pending
        local states = rawget(_G, 'SheepGameState')
        if status_now == 'executing' and pending ~= nil and pending.ack and
           pending.view == view and pending.engine == engine and
           pending.game == game and run.view == view and
           run.plan ~= nil and run.confirmed + 1 == #run.plan and
           states ~= nil and get(game, 'gameState') == get(states, 'Success') and
           get(game, 'rewardsInfo') ~= nil then
            run.pending = nil
            run.confirmed = #run.plan
            run.status = 'completed'
            return true
        end
        stop_internal(err)
        return false
    end
    if view == nil then
        if clock() - run.started > BOARD_TIMEOUT then stop_internal('Food House window did not open') end
        return false
    end
    if run.view ~= nil and
       (run.view ~= view or run.engine ~= engine or run.game ~= game) then
        stop_internal('Native Food House view or engine changed during this level')
        return false
    end
    if status_now == 'opening' then
        if is_sheep_window_open() ~= true then
            if clock() - run.started > BOARD_TIMEOUT then
                stop_internal('Food House native window did not open')
            end
            return false
        end
        local data, failure = board_snapshot(view, game)
        if data == nil then
            if clock() - run.started > BOARD_TIMEOUT then stop_internal(failure) end
            return false
        end
        -- Pin the source view *and* its original engine/game. The server reply
        -- handler can be shared by two view instances whose tile point IDs
        -- happen to match; only this exact native board admits our next click.
        run.view, run.engine, run.game = view, engine, game
        if unresolved_start ~= nil and unresolved_start.level == ctx.level and
           unresolved_start.blockId == ctx.blockId and
           unresolved_start.manager == ctx.manager and
           unresolved_start.small == ctx.small then
            unresolved_start = nil
        end
        run.initial = data
        run.coroutine = coroutine.create(function() return plan_board(data) end)
        run.status = 'solving'
        return true
    end
    if status_now == 'solving' then
        if clock() - run.started > 90 then
            stop_internal('Bounded current-level planner exceeded its time horizon')
            return false
        end
        local ok, a, b, c = coroutine.resume(run.coroutine)
        if not ok then stop_internal('Source-constrained planner failed: ' .. tostring(a)); return false end
        if coroutine.status(run.coroutine) ~= 'dead' then
            run.nodes = b or run.nodes
            return true
        end
        run.plan = a
        run.nodes = b
        run.coroutine = nil
        if run.plan == nil or #run.plan == 0 or #run.plan > MAX_MOVES then
            stop_internal(c and 'Bounded planner exhausted its search limit' or 'No complete item-free level plan')
            return false
        end
        run.status = 'executing'
        return true
    end
    if not observed_hooks_intact then
        stop_internal('Native Food House callback observer was replaced or unavailable')
        return false
    end
    if run.pending ~= nil then
        local pending = run.pending
        if clock() > pending.untilTime then
            stop_internal('Timed out awaiting native server response; no move retry')
            return false
        end
        if not pending.ack then return false end
        local sync = get(view, 'syncOps')
        if type(sync) ~= 'table' or #sync ~= 0 then return false end
        local state = get(game, 'gameState')
        local states = rawget(_G, 'SheepGameState')
        if states == nil or (state ~= get(states, 'Doing') and state ~= get(states, 'Success')) then
            stop_internal('Sheep server reconciliation reported a non-progress state')
            return false
        end
        -- Native success may itself close the game interaction gate. In that
        -- phase ISCanOp() need not return true, and the final acknowledgement
        -- must still be recorded, but only with source server rewards.
        if run.confirmed + 1 == #run.plan and state == get(states, 'Success') then
            if get(game, 'rewardsInfo') == nil then
                stop_internal('Native Food House success has no server rewards')
                return false
            end
            run.pending = nil
            run.confirmed = #run.plan
            run.status = 'completed'
            return true
        end
        if get(view, 'lock') == true or get(view, 'isMoveItemAnimating') == true then
            return false
        end
        local check_ok, checked = call(view, 'ISCanOp')
        if not check_ok or checked ~= true then return false end
        run.pending = nil
        run.confirmed = run.confirmed + 1
        if run.confirmed == #run.plan then
            stop_internal('All simulated moves sent but server success was not observed')
        end
        return true
    end
    if run.confirmed >= #run.plan then return false end
    if type(get(view, 'syncOps')) ~= 'table' or #view.syncOps ~= 0 or
       get(view, 'lock') == true or get(view, 'isMoveItemAnimating') == true then return false end
    local allowed_ok, allowed = call(view, 'ISCanOp')
    if not allowed_ok or allowed ~= true then return false end
    local next_card = run.plan[run.confirmed + 1]
    local live = get(game, 'mapCards')
    local card = get(live, next_card.id)
    if card ~= next_card.native or get(card, 'pictureId') ~= next_card.kind then
        stop_internal('Food House native board diverged from planned card identity')
        return false
    end
    local visible = get(game, 'visualList')
    local clickable_ok, clickable = call(game, 'CanClick', next_card.id)
    local item = get(get(view, 'cardViews'), next_card.id)
    if not clickable_ok or clickable ~= true or get(visible, next_card.id) ~= card or
       get(item, 'card') ~= card or type(get(item, 'OnClickCard')) ~= 'function' then
        stop_internal('Planned tile is not the current native clickable UI card')
        return false
    end
    -- Source GSheep.Enter (v26 125,148) opens UILUISheepCloud. An old native
    -- view may remain bound after this window closes and before OnDestroy is
    -- observed. No new UI click is valid without the currently open window.
    if is_sheep_window_open() ~= true then
        stop_internal('Native Food House window closed before next tile click')
        return false
    end
    if not approved_native_owner() then
        stop_internal('Food House native account changed before UI tile click')
        return false
    end
    -- Arm the response fence before invoking the UI producer: even an
    -- immediately delivered callback cannot be mistaken for an unrelated op.
    run.pending = {point = next_card.point, untilTime = clock() + MOVE_TIMEOUT,
                   ack = false, view = view, engine = engine, game = game}
    run.acting = true
    local click_ok = call(item, 'OnClickCard')
    run.acting = false
    if not click_ok then stop_internal('Native UI tile click failed'); return false end
    if run.status == 'completed' then return true end
    local sync = get(view, 'syncOps')
    local found = false
    if type(sync) == 'table' then
        for _, op in ipairs(sync) do
            if get(op, 'serverPos') == next_card.point then found = true end
        end
    end
    -- The original callback can process an immediate matching packet while
    -- OnClickCard is still on the Lua stack. The reply hook proves it saw
    -- exactly one sent queued op and SyncFinish removed that exact point;
    -- in that case absence from syncOps is an ACK, not missing dispatch.
    if not found and not (run.pending ~= nil and run.pending.ack) then
        stop_internal('Native UI did not queue the expected source-serialized move')
        return false
    end
    return true
end

function M.RegisterHandlers(bridge)
    if type(get(bridge, 'RegisterHandler')) ~= 'function' then return false end
    local ok = bridge.RegisterHandler('automation_start', start)
    ok = bridge.RegisterHandler('automation_stop', stop) and ok
    ok = bridge.RegisterHandler('feature018_sheep_automation_status', status) and ok
    return ok
end

M.Start = start
M.Stop = stop
M.Status = status
M.PlanBoard = function(board)
    -- Offline deterministic fixture usage; yield-free on small input.
    local thread = coroutine.create(function() return plan_board(board) end)
    while true do
        local ok, a, b, c = coroutine.resume(thread)
        if not ok then return nil, a end
        if coroutine.status(thread) == 'dead' then return a, b, c end
    end
end
M.Register = M.RegisterHandlers
return M
