-- FEATURE-RECOVERY-018: native game-window Hotkeys action adapter.
--
-- Source-verified read-only shield-status path in ORIGINAL installed v26 Lua:
-- DataCenter/ItemManager/StatusManager.luac SHA-256
-- 8546a11d87b66719c3f05917312d353119701cd78bbe55b2b87123df3722631c
--   GetShieldStatusIds (lines 26-36): iterates StatusTab and retains IDs whose
--      effect == EffectDefine.MAIN_SHIELD, indexed [statusId] = true.
--   GetAllStatusItem (lines 38-48): LuaEntry.Effect:GetStatusMap().
--   GetBuffTimeInfo (lines 154-165): {endTime,totalTime,intKey} for active buff.
--   CheckHasStatusByType2 (lines 374-388): UITimeManager:GetInstance()
--      :GetServerTime() and active status end times share the same time base.
-- Source-verified shield-item request: UI/UILWCityShield/Component/CityShieldPage.luac ConfirmUse
-- lines 243-249 sends SFSNetwork.SendMessage(MsgDefines.ItemUse,
-- {uuid=item.uuid,num=1}); BuyOrUse lines 202-240 performs additional
-- meteorite-warning, inventory and diamond-purchase checks.
-- Offline v26 3/26 original-entry SHA
-- 50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137:
-- CityShieldPage.CellsCallBack 153-200 resolves each template.para1 through
-- StatusTab and explicitly compares status.time * 1000 with shield remaining;
-- its confirmation formats status.time / 3600 hours. The adapter supports
-- owned duration-matched items only when every UI warning/confirmation gate
-- can be satisfied without a user dialog or paid purchase.
--
-- Only Feature018GameBridge.Pump may invoke this registered module after the
-- exact game PID/profile/session/challenge/lease admission. Never register a
-- global keyboard hook, modify a game file or claim a network send succeeded.

local M = { VERSION = "feature018-hotkey-game-1" }
local mark_array = nil
local pending_shield = nil
local SHIELD_PENDING_SECONDS = 45
local shield_use_observer = nil
local pending_purchase = nil
local purchase_observer = nil
local PURCHASE_OBSERVER_SECONDS = 90
local armed_shield_replace = nil

local function member(value, key)
    if value == nil then return nil end
    local ok, item = pcall(function() return value[key] end)
    if ok then return item end
    return nil
end

local function invoke(value, key, ...)
    local method = member(value, key)
    if type(method) ~= "function" then return false, nil end
    local ok, result = pcall(method, value, ...)
    -- A native eligibility gate can legitimately return false. Converting
    -- that value to nil would make safe, negative checks unverifiable.
    return ok, result
end

local function unavailable(reason)
    return {state = "unsupported", errorCode = "GAME_METHOD_UNAVAILABLE",
            error = reason}
end

local function invalid(reason)
    return {state = "failed", errorCode = "GAME_PRECONDITION_FAILED",
            error = reason}
end

local function number(value)
    local result = tonumber(value)
    if result == nil or result ~= result or
       result == math.huge or result == -math.huge then return nil end
    return result
end

local function shield_snapshot()
    local center = rawget(_G, "DataCenter")
    local status = member(center, "StatusManager")
    if status == nil then return unavailable("Current-game StatusManager unavailable") end
    local ok_ids, shield_ids = invoke(status, "GetShieldStatusIds")
    local ok_map, all_statuses = invoke(status, "GetAllStatusItem")
    if not ok_ids or type(shield_ids) ~= "table" or
       not ok_map or type(all_statuses) ~= "table" then
        return unavailable("Native shield status IDs or authoritative Effect status map unavailable")
    end
    local time_type = rawget(_G, "UITimeManager")
    local ok_clock, clock = invoke(time_type, "GetInstance")
    local ok_time, server_time = invoke(clock, "GetServerTime")
    local now = number(server_time)
    if not ok_clock or not ok_time or now == nil or now < 0 then
        return unavailable("Native game server clock unavailable")
    end

    local count_ids = 0
    local statuses = {}
    for key, is_shield in pairs(shield_ids) do
        if is_shield == true then
            local id = number(key)
            if id ~= nil and id > 0 and id % 1 == 0 then
                count_ids = count_ids + 1
                local end_value = member(all_statuses, key)
                if end_value == nil then end_value = member(all_statuses, id) end
                if end_value == nil then end_value = member(all_statuses, tostring(id)) end
                local ended = number(end_value)
                if ended ~= nil and ended > now then
                    local ok_info, info = invoke(status, "GetBuffTimeInfo", id)
                    local total = ok_info and number(member(info, "totalTime")) or nil
                    local end_time = ok_info and number(member(info, "endTime")) or nil
                    -- The independent StatusManager's own lookup must agree
                    -- with the native Effect status map before a time is shown.
                    if end_time ~= nil and end_time == ended and
                       total ~= nil and total >= 0 then
                        statuses[#statuses + 1] = {
                            statusId = id,
                            endTime = ended,
                            totalTime = total,
                            remainingMilliseconds = ended - now,
                        }
                    else
                        return unavailable("Active shield status could not be independently checked")
                    end
                end
            end
        end
    end
    if count_ids == 0 then
        return unavailable("Current game has no source-classified MAIN_SHIELD status IDs")
    end
    if #statuses > 1 then
        table.sort(statuses, function(a, b) return a.endTime < b.endTime end)
    end
    -- FEATURE018's JSON encoder treats an empty Lua table as an object.
    -- A shield-free game must still return statuses=[] to the frontend.
    if type(mark_array) == "function" then
        statuses = mark_array(statuses)
    end
    local result = {
        active = #statuses > 0,
        shieldStatusCount = #statuses,
        serverTimeMilliseconds = now,
        statuses = statuses,
        observation = "native_MAIN_SHIELD_effect_status_map",
    }
    if #statuses == 0 then
        result.remainingMilliseconds = 0
    elseif #statuses == 1 then
        result.remainingMilliseconds = statuses[1].remainingMilliseconds
        result.endTime = statuses[1].endTime
    end
    return {state = "confirmed", data = result}
end

function M.ShieldStatusSnapshot()
    return shield_snapshot()
end

-- R18: visible cities, not the local player's Effect status map.
-- Current hash-gated RGMD: WorldPointManager.GetMainByScreen RID13723
-- filters BuildPointInfo to the screen; protectEndTime is field 0x0400274e.
-- WorldScene.TileIndexToWorld RID14789 uses the same native point index.
function M.VisibleCityShieldSnapshot()
    local cs = rawget(_G, "CS")
    local manager = member(cs, "SceneManager")
    local ids = rawget(_G, "SceneManagerSceneID") or member(cs, "SceneManagerSceneID")
    if member(manager, "CurrSceneID") == nil or member(ids, "World") == nil or
       member(manager, "CurrSceneID") ~= member(ids, "World") then
        return unavailable("Visible city shields require the world map")
    end
    local world = member(manager, "World")
    local points = member(world, "PointManager")
    local camera = member(member(world, "Camera"), "__camera")
    local ok_list, cities = invoke(points, "GetMainByScreen")
    local count = number(member(cities, "Count"))
    local _, clock = invoke(rawget(_G, "UITimeManager"), "GetInstance")
    local ok_time, now = invoke(clock, "GetServerTime")
    now = number(now)
    if not ok_list or count == nil or count < 0 or count > 512 or
       count % 1 ~= 0 or camera == nil or not ok_time or now == nil then
        return unavailable("Native visible-city list, camera or server clock unavailable")
    end
    local rows = {}
    for index = 0, count - 1 do
        local city = member(cities, index)
        local point = number(member(city, "pointIndex"))
        local ending = number(member(city, "protectEndTime"))
        local uuid = member(city, "uuid")
        if point == nil or ending == nil or uuid == nil then
            return unavailable("Visible BuildPointInfo shield fields unavailable")
        end
        local ok_pos, pos = invoke(world, "TileIndexToWorld", point)
        local ok_view, viewport = invoke(camera, "WorldToViewportPoint", pos)
        local x, y, z = number(member(viewport, "x")), number(member(viewport, "y")), number(member(viewport, "z"))
        if not ok_pos or not ok_view or x == nil or y == nil or z == nil then
            return unavailable("Native city screen projection unavailable")
        end
        -- The Map City Shield Ends formatter accepts seconds or milliseconds.
        local end_ms = ending < 1000000000000 and ending * 1000 or ending
        rows[#rows + 1] = {uuid=tostring(uuid), ownerUid=tostring(member(city, "ownerUid") or ""),
            ownerName=tostring(member(city, "playerName") or ""), pointId=point,
            protectEndTime=ending, shieldEndTimeMilliseconds=end_ms,
            remainingMilliseconds=math.max(0, end_ms-now),
            active=end_ms > now, viewportX=x, viewportY=y, viewportZ=z}
    end
    if mark_array then rows = mark_array(rows) end
    return {state="confirmed", data={observation="native_visible_city_shields",
        readOnly=true, source="WorldPointManager.GetMainByScreen+BuildPointInfo.protectEndTime",
        serverTimeMilliseconds=now, cities=rows}}
end

-- Native game-side input focus gate for the window-only key sampler.
-- The v26 `Framework/UI/Component/UIInput.luac` (SHA-256
-- ed42a0d5ca46d2712d67a078a7f8fb597bbc36b00acd82366ec9ef4fbd15bbce)
-- OnCreate lines 19-29 and Select lines 184-192 prove this game uses
-- UnityEngine.UI.InputField, TMPro.TMP_InputField and TMP_InputFieldEx to edit
-- text, with Select()/ActivateInputField(). A Win32 HWND focus check alone
-- cannot distinguish these objects, which share the main Unity game HWND.
--
-- This is a pure game-thread observation, NOT an assertion that the desktop
-- has a fresh authenticated input-focus heartbeat. The caller must bind a
-- periodic result to the current game owner and must fail closed if stale.
function M.TextInputClear()
    local cs = rawget(_G, "CS")
    local unity = member(cs, "UnityEngine")
    local ui = member(unity, "UI")
    local events = member(unity, "EventSystems")
    local tmpro = member(cs, "TMPro")
    local event_type = member(events, "EventSystem")
    local field_type = member(ui, "InputField")
    local tmp_type = member(tmpro, "TMP_InputField")
    local extra_type = member(tmpro, "TMP_InputFieldEx")
    local type_of = rawget(_G, "typeof")
    local is_null = rawget(_G, "IsNull")
    if event_type == nil or field_type == nil or tmp_type == nil or
       extra_type == nil or type(type_of) ~= "function" or
       type(is_null) ~= "function" then
        return false, "game_input_focus_types_unavailable"
    end

    local current = member(event_type, "current")
    if current == nil then return false, "unity_event_system_unavailable" end
    local ok_selected, selected = pcall(function()
        return current.currentSelectedGameObject
    end)
    if not ok_selected then return false, "game_selected_object_unreadable" end
    if selected == nil or is_null(selected) then return true, "no_selected_text_input" end

    -- A focused child of an input component is still an edit target. Query
    -- the selected object's ancestors as well, never its siblings or scene.
    local get_component = member(selected, "GetComponentInParent")
    if type(get_component) ~= "function" then
        return false, "game_selection_component_reader_unavailable"
    end
    for _, native_type in ipairs({field_type, tmp_type, extra_type}) do
        local ok_kind, component_type = pcall(type_of, native_type)
        if not ok_kind or component_type == nil then
            return false, "game_input_component_type_unavailable"
        end
        local ok_field, field = pcall(get_component, selected, component_type)
        if not ok_field then return false, "game_input_component_lookup_failed" end
        if field ~= nil and not is_null(field) then
            return false, "native_game_text_input_selected"
        end
    end
    return true, "native_selected_object_has_no_text_input_component"
end

-- Called only through the authenticated game-bridge request/response gate.
-- The bridge itself stamps the request identity and in-game observation time
-- into a correlated envelope; the desktop must also bound receipt freshness.
function M.InputFocusSnapshot()
    local clear, reason = M.TextInputClear()
    if reason == "native_game_text_input_selected" then
        return {state = "confirmed", data = {
            inputClear = false, observation = "native_Unity_UI_input_focus",
            focusReason = reason,
        }}
    end
    if not clear then return unavailable("Native game text-input focus cannot be verified: " .. reason) end
    return {state = "confirmed", data = {
        inputClear = true, observation = "native_Unity_UI_input_focus",
        focusReason = reason,
    }}
end

local function digit_id(value)
    if value == nil or type(value) == "boolean" then return nil end
    local str = tostring(value)
    if #str < 1 or #str > 20 or str == "0" or
       str:find("[^0-9]") then return nil end
    return str
end

-- v26 Net.Msgs.ItemUseMessage.OnCreate uses PutUtfString("uuid"), while
-- Goods.itemId remains numeric. A real owned item-stack UUID may therefore
-- be an opaque game-assigned token longer than the 20-digit item/template ID.
-- Only the exact current owner's ItemData may supply this token; callers
-- cannot pass a UUID to shieldUse. Never transform the native token.
local function native_item_uuid(value)
    if type(value) ~= "string" and type(value) ~= "number" then return nil end
    local token = tostring(value)
    if #token < 1 or #token > 128 or token == "0" or
       token:find("[^%w_-]") then return nil end
    return token
end

local function shield_owner(fields)
    local tokens = {}
    for _, key in ipairs({"profileId", "launchSessionId", "challenge", "gamePid"}) do
        local value = fields[key]
        if type(value) ~= "string" or #value == 0 or #value > 128 or
           value:find("[^%w_-]") then return nil end
        tokens[#tokens + 1] = value
    end
    return table.concat(tokens, "/")
end

-- Snapshot the currently authenticated native account. Shipped actions use
-- this identity, not a hardcoded player/server. A pending operation retains
-- its originating player and UID/server when later replies are processed.
local function native_account()
    local player = member(rawget(_G, "LuaEntry"), "Player")
    local uid=digit_id(member(player, "uid"))
    if not uid then return nil end
    local expected_server
    for _, getter in ipairs({"GetSourceServerId", "GetSelfServerId", "GetCurServerId"}) do
        local ok, server = invoke(player, getter)
        local id=digit_id(server)
        if not ok or not id or (expected_server and expected_server~=id) then return nil end
        expected_server=id
    end
    if member(rawget(_G,"LuaEntry"),"Player")~=player or
       digit_id(member(player,"uid"))~=uid then return nil end
    return {uid=uid,server=expected_server,player=player}
end
local function native_sub_account()
    return native_account()~=nil
end

-- A pending native RPC keeps the exact account seen before sending. The
-- player object alone can survive a UID/server switch, so neither identity
-- nor a later reply may be inferred from object equality.
local function same_native_account(expected)
    local current=native_account()
    return expected~=nil and current~=nil and
        expected.player==current.player and expected.uid==current.uid and
        expected.server==current.server
end

local function request_native_account(fields)
    local account=native_account()
    if account==nil or type(fields)~="table" or
       type(fields.authorizedUid)~="string" or
       fields.authorizedUid~=account.uid then return nil end
    return account
end

-- Original v26 DailyTaskManager.GetAllDailyTask (root/16) selects visible
-- unreceived DailyQuest tasks. DailyTaskInfo.UpdateInfo (root/2) stores the
-- *server supplied* state and RewardManager.ReturnRewardParamForView result.
-- Only TaskState.CanReceive establishes a currently claimable DAILY task.
-- The native Gold reward view uses Player.sm_addGoldCount (RewardManager
-- root/13), which is NOT a reliable per-task promised gold amount. Even a
-- matching ResourceType.Gold must not be summed into an earning forecast.
-- No TryReqUpdateData, DailyTaskReward, sign-in, gift or pay RPC is called.
local function sign_in_claim_observation()
    -- v26 LWActSignInManager.OnGetEventInfo fills infos[eventId] from server
    -- events. ClaimActReward(eventId, 0) gates IsEnd and
    -- GetCanClaimRewardDay()>0 before sending ClaimActSignInReward(eventId,0).
    -- LWActSignInInfo.GetCanClaimRewardDay returns a COUNT of claimable days,
    -- not a day index. Never pass the count as a CanClaimAtDay index.
    local result = {availability="source_unavailable", source="native_sign_in_infos",
        route="ClaimActSignInReward", goldRewardVerified=false,
        readOnly=true, requestDispatched=false}
    local manager = member(rawget(_G, "DataCenter"), "LWActSignInManager")
    local infos = member(manager, "infos")
    if type(infos) ~= "table" then return result end
    local candidates, scanned, qualifying = {}, 0, 0
    for key, info in pairs(infos) do
        scanned = scanned + 1
        if scanned > 64 then
            result.availability="too_many_unverified_sign_in_events"
            return result
        end
        local id = digit_id(key)
        local ok_end, ended = invoke(info, "IsEnd")
        local ok_day, day = invoke(info, "GetCanClaimRewardDay")
        local parsed_day = number(day)
        if id ~= nil and ok_end and ended == false and
           ok_day and parsed_day ~= nil and parsed_day > 0 and
           parsed_day % 1 == 0 then
            qualifying = qualifying + 1
            if #candidates < 16 then
                candidates[#candidates+1] = {eventId=id,originalClaimAnyDay=0,
                    nativeAvailableRewardCount=parsed_day,
                    claimability="native_cached_IsEnd_GetCanClaimRewardDay_count",
                    goldRewardVerified=false,requestDispatched=false}
            end
        end
    end
    result.availability="native_cached"
    result.loadedEventCount=scanned
    result.claimableEventCount=qualifying
    result.claimableEventsTruncated=qualifying > #candidates
    table.sort(candidates, function(a,b) return a.eventId < b.eventId end)
    result.claimableEvents=type(mark_array)=="function" and mark_array(candidates) or candidates
    return result
end

local function daily_gold_claim_observation()
    if not native_sub_account() then return nil end
    local data = member(rawget(_G, "DataCenter"), "DailyTaskManager")
    local task_states = rawget(_G, "TaskState")
    local claim_state = member(task_states, "CanReceive")
    local gold_type = member(rawget(_G, "ResourceType"), "Gold")
    local report = {observation="native_visible_daily_tasks_read_only",
        readOnly=true, requestDispatched=false, goldEarned=false,
        goldAmountVerified=false, route="DailyTaskReward",
        rewardClass="unverified", availability="source_unavailable",
        signInRoute="ClaimActSignInReward", signInClaimabilityVerified=false,
        allianceGiftRoute="AllianceGiftGetReward", allianceGiftClaimabilityVerified=false}
    report.signIn = sign_in_claim_observation()
    if type(member(data, "GetAllDailyTask")) ~= "function" or
       claim_state == nil or number(claim_state) == nil or
       member(data, "dailyQuestTasks") == nil then
        return report
    end
    local ok, tasks = invoke(data, "GetAllDailyTask")
    if not ok or type(tasks) ~= "table" then return report end
    local results, scanned, eligible, gold_candidates = {}, 0, 0, 0
    local unidentified = 0
    for _, task in pairs(tasks) do
        scanned = scanned + 1
        if scanned > 256 then
            report.availability="too_many_unverified_tasks"
            return report
        end
        if number(member(task, "state")) == number(claim_state) then
            local id = digit_id(member(task, "id"))
            if id == nil then
                unidentified = unidentified + 1
            else
                eligible = eligible + 1
                local reward = member(task, "reward")
                local has_gold, readable = false, type(reward) == "table"
                if readable then
                    local scanned_rewards = 0
                    for _, item in pairs(reward) do
                        scanned_rewards = scanned_rewards + 1
                        if scanned_rewards > 64 then readable = false; break end
                        local reward_type = number(member(item, "rewardType"))
                        if number(gold_type) == nil or reward_type == nil then
                            readable = false
                        elseif reward_type == number(gold_type) then
                            has_gold = true
                        end
                    end
                end
                if has_gold and readable then gold_candidates = gold_candidates + 1 end
                if #results < 16 then
                    results[#results+1] = {taskId=id, route="DailyTaskReward",
                        sourceState="TaskState.CanReceive",
                        goldRewardTypeMatched=has_gold and readable,
                        currencyClassificationComplete=readable,
                        goldAmountVerified=false, requestDispatched=false}
                end
            end
        end
    end
    if not native_sub_account() then return nil end
    table.sort(results, function(a,b) return tostring(a.taskId or "") < tostring(b.taskId or "") end)
    report.availability="native_observed"
    report.freshServerStateVerified=false
    report.visibleTaskCount=scanned
    report.claimableDailyTaskCount=eligible
    report.unidentifiedClaimableTaskCount=unidentified
    report.goldTypeMatchedDailyTaskCount=gold_candidates
    report.claimableTasksTruncated=eligible > #results
    report.claimableDailyTasks=type(mark_array)=="function" and mark_array(results) or results
    return report
end

-- Optional authenticated inspection entry for the existing shop read path.
-- Returns no user-requested claim action, even when a reward is claimable.
function M.GoldClaimableSnapshot(fields)
    if type(fields) ~= "table" or shield_owner(fields) == nil or
       not native_sub_account() then
        return invalid("Current authorized native player/session required for reward inspection")
    end
    -- The bridge owns the authenticated launch token. Within that launch,
    -- reject a native Player or claim manager/cache swap during observation:
    -- a different session can have the same UID but different cached claims.
    local player = member(rawget(_G, "LuaEntry"), "Player")
    local center = rawget(_G, "DataCenter")
    local daily = member(center, "DailyTaskManager")
    local sign_in = member(center, "LWActSignInManager")
    local tasks = member(daily, "dailyQuestTasks")
    local events = member(sign_in, "infos")
    local observation = daily_gold_claim_observation()
    if observation == nil or not native_sub_account() or
       member(rawget(_G, "LuaEntry"), "Player") ~= player or
       rawget(_G, "DataCenter") ~= center or
       member(center, "DailyTaskManager") ~= daily or
       member(center, "LWActSignInManager") ~= sign_in or
       member(daily, "dailyQuestTasks") ~= tasks or
       member(sign_in, "infos") ~= events then
        return invalid("Native reward inspector owner/session/cache changed")
    end
    return {state="confirmed", data=observation}
end

local function owned_item_count(uuid, item_id)
    local center = rawget(_G, "DataCenter")
    local items = member(center, "ItemData")
    local ok, list = invoke(items, "GetItemList", 4, 1)
    if not ok or type(list) ~= "table" then return nil end
    local count = 0
    for _, item in pairs(list) do
        if native_item_uuid(member(item, "uuid")) == uuid then
            if digit_id(member(item, "itemId")) ~= item_id then return nil end
            local available = number(member(item, "count"))
            if available == nil or available < 0 or available % 1 ~= 0 then
                return nil
            end
            count = count + available
        end
    end
    return count
end

-- v26 ItemData.UpdateOneItem (DataCenter/ItemData/ItemData.luac root/16)
-- indexes ItemInfos by the server-returned item.uuid. Aggregate per-itemId
-- stock is insufficient to prove the exact returned shop stack exists.
-- Fail closed on duplicate UUID entries, a changed itemId or malformed count.
local function owned_exact_stack_count(items, uuid, item_id)
    -- Reuse the SAME ItemData:GetItemList snapshot already used for total
    -- stock; do not trigger a second native list fetch while correlating.
    if type(items) ~= "table" then return nil end
    local count, found = nil, 0
    for _, item in pairs(items) do
        if native_item_uuid(member(item, "uuid")) == uuid then
            found = found + 1
            if found > 1 or digit_id(member(item, "itemId")) ~= item_id then
                return nil
            end
            local amount = number(member(item, "count"))
            if amount == nil or amount < 0 or amount % 1 ~= 0 then return nil end
            count = amount
        end
    end
    return count
end

local function source_matched_shield_row(row, hours)
    -- In v26 StatusManager.GetShieldStatusIds root/3/0 uses
    -- tonumber(StatusTab.effect) == EffectDefine.MAIN_SHIELD. Its result is
    -- cached in shieldStatusIds; a re-read of that cache alone cannot detect
    -- a changed StatusTab classification. Verify the actual row as well.
    local required_effect = number(member(rawget(_G,"EffectDefine"),"MAIN_SHIELD"))
    return required_effect ~= nil and
           number(member(row,"effect")) == required_effect and
           number(member(row,"time")) == hours * 3600
end

local function shield_item_for_hours(hours)
    local center = rawget(_G, "DataCenter")
    local item_data = member(center, "ItemData")
    local templates = member(center, "ItemTemplateManager")
    local status = member(center, "StatusManager")
    local controller = rawget(_G, "LocalController")
    local tables = rawget(_G, "TableName")
    local status_table = member(tables, "StatusTab")
    local ok_owned, owned = invoke(item_data, "GetItemList", 4, 1)
    local ok_templates, available = invoke(templates, "GetTypeListByTypes", 4, 1)
    local ok_ids, ids = invoke(status, "GetShieldStatusIds")
    local ok_controller, local_instance = invoke(controller, "instance")
    if not ok_owned or type(owned) ~= "table" or
       not ok_templates or type(available) ~= "table" or
       not ok_ids or type(ids) ~= "table" or
       not ok_controller or local_instance == nil or status_table == nil then
        return nil, unavailable("Native owned items, status IDs, or Goods/Status templates unavailable")
    end
    local chosen = nil
    for _, template in pairs(available) do
        local status_id = digit_id(member(template, "para1"))
        local template_id = digit_id(member(template, "id"))
        if template_id ~= nil and status_id ~= nil and
           (ids[tonumber(status_id)] == true or ids[status_id] == true) then
            local ok_row, row = invoke(local_instance, "getLine",
                status_table, member(template, "para1"))
            if not ok_row or row == nil then
                return nil, unavailable("Native StatusTab shield duration is unreadable")
            end
            local seconds = number(member(row, "time"))
            if seconds == nil or seconds <= 0 or seconds % 1 ~= 0 then
                return nil, unavailable("Native StatusTab shield duration is invalid")
            end
            if seconds == hours * 3600 and
               not source_matched_shield_row(row, hours) then
                return nil, unavailable("Native StatusTab MAIN_SHIELD classification changed")
            end
            if seconds == hours * 3600 then
                for _, item in pairs(owned) do
                    local count = number(member(item, "count"))
                    local uuid = native_item_uuid(member(item, "uuid"))
                    if digit_id(member(item, "itemId")) == template_id and
                       uuid ~= nil and count ~= nil and count >= 1 and count % 1 == 0 then
                        if chosen ~= nil then
                            return nil, unavailable("Multiple owned stacks map to this shield duration")
                        end
                        chosen = {uuid=uuid, itemId=template_id,
                            statusId=tonumber(status_id), count=count, durationMs=seconds*1000}
                    end
                end
            end
        end
    end
    if chosen == nil then
        return nil, unavailable("No uniquely owned 8/12/24h shield item with source-matched StatusTab duration")
    end
    return chosen
end

local function shield_shop_pre_send_unchanged(center, player, chosen, before_gold, before_count)
    local ok_items, items = invoke(member(center, "ItemData"), "GetItemList", 4, 1)
    local ok_templates, templates = invoke(member(center, "ItemTemplateManager"), "GetTypeListByTypes", 4, 1)
    local ok_ids, ids = invoke(member(center, "StatusManager"), "GetShieldStatusIds")
    local ok_controller, controller = invoke(rawget(_G, "LocalController"), "instance")
    local status_tab = member(rawget(_G, "TableName"), "StatusTab")
    local gold = number(member(player, "gold"))
    if not ok_items or type(items) ~= "table" or not ok_templates or
       type(templates) ~= "table" or not ok_ids or type(ids) ~= "table" or
       not ok_controller or controller == nil or status_tab == nil or
       gold ~= before_gold then return false end
    -- Original v26 CityShieldPage.CellsCallBack maps Goods.para1 to the
    -- classified MAIN_SHIELD StatusTab.time. A changed row/classification
    -- makes the formerly approved F6/F7/F8 duration a different purchase.
    if ids[chosen.statusId] ~= true and ids[tostring(chosen.statusId)] ~= true then
        return false
    end
    local count = 0
    for _, item in pairs(items) do
        if digit_id(member(item, "itemId")) == chosen.itemId then
            local amount = number(member(item, "count"))
            if amount == nil or amount < 0 or amount % 1 ~= 0 then return false end
            count = count + amount
        end
    end
    if count ~= before_count then return false end
    local matching = 0
    for _, template in pairs(templates) do
        if digit_id(member(template, "id")) == chosen.itemId then
            matching = matching + 1
            if digit_id(member(template, "para1")) ~= tostring(chosen.statusId) or
               number(member(template, "price")) ~= chosen.price then return false end
            local ok_row, row = invoke(controller, "getLine", status_tab, member(template, "para1"))
            if not ok_row or not source_matched_shield_row(row, chosen.durationHours) then
                return false
            end
        end
    end
    return matching == 1
end

-- ORIGINAL v26 Net/Msgs/UIShopBuyMessage.HandleMessage (f7bcd809...) routes
-- the reply through DataCenter.ItemManager.ItemBuyHandle (febee839..., root/2).
-- ItemBuyHandle reads errorCode, reply.item.id/itemId, costGold, remainGold
-- and updates the native ItemData/Player caches. A matched reply is useful
-- evidence, but there is NO request nonce: even an exact match is not a
-- causally unique proof that this adapter originated the server reply.
-- Observe only our one in-flight purchase and ALWAYS run the original first.
local function restore_purchase_observer()
    local observer = purchase_observer
    if observer == nil then return true end
    purchase_observer = nil
    if member(observer.manager, "ItemBuyHandle") ~= observer.wrapper then
        return false  -- A different owner replaced the handler; never clobber it.
    end
    local ok = pcall(function()
        observer.manager.ItemBuyHandle = observer.rawOriginal
    end)
    return ok and member(observer.manager, "ItemBuyHandle") == observer.original
end

local function install_purchase_observer(pending, center, player)
    if purchase_observer ~= nil then return false end
    local manager = member(center, "ItemManager")
    if type(manager) ~= "table" then return false end
    local original = member(manager, "ItemBuyHandle")
    if type(original) ~= "function" then return false end
    local raw_original = rawget(manager, "ItemBuyHandle")
    local observer = {manager=manager, original=original, rawOriginal=raw_original}
    observer.wrapper = function(self, reply)
        local account_before=same_native_account(pending.account)
        if pending_purchase == pending and type(pending.sentAt) == "number" and
           (os.time() - pending.sentAt > PURCHASE_OBSERVER_SECONDS or
            os.time() < pending.sentAt) then
            pending.observerExpired = true
            restore_purchase_observer() -- Leave the no-resend tombstone intact.
        end
        local ok_original, original_result = pcall(original, self, reply)
        local active = pending_purchase
        if active == pending and purchase_observer == observer then
            active.replyCount = active.replyCount + 1
            local source_item = member(reply, "item")
            local raw_id = member(source_item, "id")
            local raw_item_id = member(source_item, "itemId")
            local response_id = digit_id(raw_id ~= nil and raw_id or raw_item_id)
            local raw_uuid = member(source_item, "uuid")
            local response_uuid = native_item_uuid(raw_uuid)
            local response_count = number(member(source_item, "count"))
            -- The original ItemBuyHandle hands this entire item to
            -- ItemData.UpdateOneItem, whose primary identity is item.uuid.
            -- If the reply exposes the new stack, verify its exact identity
            -- and purchased count rather than attributing any new same-id
            -- stack to this request. A reply without a UUID remains weaker
            -- handler evidence, never proof of a particular stack.
            local inconsistent_stack = raw_uuid ~= nil and
                (response_uuid == nil or response_count ~= 1)
            -- ItemManager.ItemBuyHandle overwrites itemId from item.id when
            -- present. Refuse inconsistent dual IDs instead of accepting the
            -- other field before the original cache applies its update.
            local conflicting_ids = raw_id ~= nil and raw_item_id ~= nil and
                digit_id(raw_id) ~= digit_id(raw_item_id)
            local cost = number(member(reply, "costGold"))
            local remaining = number(member(reply, "remainGold"))
            local source_error = member(reply, "errorCode")
            local owned = ok_original and self == manager and
                rawget(_G, "DataCenter") == center and
                member(rawget(_G, "LuaEntry"), "Player") == player and
                account_before and same_native_account(pending.account)
            if not conflicting_ids and not inconsistent_stack and
               response_id == pending.itemId and
               source_error == nil and
               cost == pending.price and remaining == pending.gold - pending.price and
               owned and active.replyCount == 1 then
                active.replyMatched = true
                active.replyStackUuid = response_uuid
            else
                -- An unrelated/malformed/error reply or an original-handler
                -- failure makes source attribution ambiguous for this lease.
                -- A later reply may not clear this tombstone.
                active.replyAmbiguous = true
                active.replyMatched = false
            end
            active.replyError = source_error ~= nil
            if not owned then active.replyOwnerOrHandlerUncertain = true end
        end
        if not ok_original then error(original_result) end
        return original_result
    end
    local ok_install = pcall(function()
        manager.ItemBuyHandle = observer.wrapper
    end)
    if not ok_install or member(manager, "ItemBuyHandle") ~= observer.wrapper then
        if ok_install then
            pcall(function()
                if rawget(manager, "ItemBuyHandle") == observer.wrapper then
                    manager.ItemBuyHandle = raw_original
                end
            end)
        end
        return false
    end
    purchase_observer = observer
    return true
end

-- Original v26 UIShopBuyMessage (f7bcd809...) serializes itemId as UtfString
-- and num as Int; CityShieldPage.Refresh/BuyOrUse sources Goods.price and
-- Player.gold. This explicit provisioning operation buys inventory only.
local function shield_shop(fields)
    local owner = shield_owner(fields)
    local hours = tonumber(fields.slot)
    if owner == nil or not (hours == 8 or hours == 12 or hours == 24) then
        return invalid("Exact owned shield purchase session/duration required")
    end
    local account=request_native_account(fields)
    if account==nil then
        return invalid("Shield shop requires exact native authorized UID and server")
    end
    owner=owner.."/"..account.uid.."/"..account.server
    local player = member(rawget(_G,"LuaEntry"),"Player")
    local center = rawget(_G,"DataCenter")
    local ok_templates, templates = invoke(member(center,"ItemTemplateManager"),"GetTypeListByTypes",4,1)
    local ok_ids, ids = invoke(member(center,"StatusManager"),"GetShieldStatusIds")
    local ok_tables, tables = invoke(rawget(_G,"LocalController"),"instance")
    local ok_owned, owned = invoke(member(center,"ItemData"),"GetItemList",4,1)
    if not ok_templates or not ok_ids or not ok_tables or not ok_owned or
       type(templates) ~= "table" or type(ids) ~= "table" or type(owned) ~= "table" then
        return unavailable("Native shield Goods/status/inventory unavailable")
    end
    local chosen, count = nil, 0
    for _, template in pairs(templates) do
        local status_id = member(template,"para1")
        if ids[status_id] == true or ids[tonumber(status_id)] == true then
            local ok_row, row = invoke(tables,"getLine",member(rawget(_G,"TableName"),"StatusTab"),status_id)
            if ok_row and source_matched_shield_row(row, hours) then
                if chosen ~= nil then return invalid("Ambiguous native shield template duration") end
                chosen = {itemId=digit_id(member(template,"id")),statusId=tonumber(status_id),
                    price=number(member(template,"price")),durationHours=hours}
            end
        end
    end
    if chosen == nil or chosen.itemId == nil then return unavailable("Native shield duration template absent") end
    local observed_stacks = {}
    for _, item in pairs(owned) do
        if digit_id(member(item,"itemId")) == chosen.itemId then
            local amount = number(member(item,"count"))
            if amount == nil or amount < 0 or amount % 1 ~= 0 then return invalid("Invalid owned shield count") end
            count = count + amount
            -- Read-only, source-bound inspection for an inventory identity
            -- discrepancy. The UI's original ItemUse serializes item.uuid;
            -- zero/missing/non-numeric UUID must never become an ItemUse.
            local raw_uuid = member(item,"uuid")
            local uuid_text = raw_uuid ~= nil and tostring(raw_uuid) or nil
            if uuid_text ~= nil and (#uuid_text > 128 or
                uuid_text:find("[^%w_-]")) then uuid_text = nil end
            observed_stacks[#observed_stacks+1] = {
                uuidType = type(raw_uuid), uuidText = uuid_text,
                validItemUseUuid = native_item_uuid(raw_uuid) ~= nil,
                count = amount,
            }
        end
    end
    local gold = number(member(player,"gold"))
    if gold == nil or gold < 0 then return unavailable("Native gem balance unavailable") end
    local data = {observation="native_shield_shop_inventory",readOnly=true,
        itemId=chosen.itemId,statusId=chosen.statusId,durationHours=hours,
        nativePrice=chosen.price,gold=gold,count=count,requestDispatched=false}
    if fields.mode == "inspect" then
        -- v26 CityShieldPage.ConfirmBuy uses UIShopBuy's Goods.price and
        -- LuaEntry.Player.gold. Affordability is an observation only; the
        -- user must separately consent to the exact native item purchase.
        if chosen.price ~= nil and chosen.price > 0 and
           chosen.price % 1 == 0 and gold % 1 == 0 then
            data.canAffordNativePrice = gold >= chosen.price
            data.additionalGoldNeeded = math.max(0, chosen.price - gold)
        end
        data.currencySource = "native_Player_gold"
        data.purchaseRoute = "native_UIShopBuy_one_item_with_consent"
        data.nativeMatchedShieldStacks = observed_stacks
        local claim = M.GoldClaimableSnapshot(fields)
        if claim.state ~= "confirmed" or not same_native_account(account) or
           rawget(_G,"DataCenter") ~= center or
           member(rawget(_G,"LuaEntry"),"Player") ~= player or
           number(member(player,"gold")) ~= gold then
            return invalid("Native shield/gold inspection owner changed")
        end
        data.nativeGoldClaimRoutes = claim.data
        return {state="confirmed",data=data}
    end
    if fields.mode == "armUse" then
        if fields.shieldReplaceOptIn ~= "1" then return invalid("Shield replacement confirmation required") end
        local snapshot=shield_snapshot()
        if snapshot.state ~= "confirmed" then return snapshot end
        if snapshot.data.shieldStatusCount > 1 or snapshot.data.remainingMilliseconds == nil then
            return invalid("Native active shield state is ambiguous; replacement not armed")
        end
        if snapshot.data.remainingMilliseconds >= hours*3600000 then
            return invalid("Native shield cannot be replaced with a shorter duration")
        end
        if not same_native_account(account) or
           rawget(_G,"DataCenter") ~= center or
           member(rawget(_G,"LuaEntry"),"Player") ~= player then
            return invalid("Native shield replacement owner changed before arming")
        end
        armed_shield_replace={owner=owner,account=account,hours=hours,expires=os.time()+60}
        data.observation="explicit_next_shield_replacement_confirmed"
        data.replacementArmed=true
        return {state="confirmed",data=data}
    end
    if pending_purchase ~= nil and pending_purchase.owner ~= owner then
        return invalid("Unresolved purchase belongs to a previous owner; no resend")
    end
    if fields.mode == "status" then
        local pending = pending_purchase
        if pending == nil or pending.itemId ~= chosen.itemId or
           pending.hours ~= hours or pending.statusId ~= chosen.statusId then
            return invalid("No matching shield purchase")
        end
        if not same_native_account(pending.account) or
           rawget(_G,"DataCenter") ~= center or
           member(rawget(_G,"LuaEntry"),"Player") ~= player or
           pending.player ~= player or pending.center ~= center or
           member(center,"ItemManager") ~= pending.manager then
            return invalid("Native shield purchase owner changed during observation")
        end
        local observed_reply = pending.replyCount > 0
        local observer_intact = purchase_observer ~= nil and
            purchase_observer.manager == pending.manager and
            member(purchase_observer.manager,"ItemBuyHandle") == purchase_observer.wrapper
        data.nativeShopReplyObserved = observed_reply
        data.nativeShopReplyMatched = pending.replyMatched and not pending.replyAmbiguous
        data.nativeShopReplyCount = pending.replyCount
        data.nativeReplyCausalityConfirmed = false -- No native request nonce.
        data.nativeShopReplyOrigin = "original_ItemManager_ItemBuyHandle_no_nonce"
        if pending.replyAmbiguous then
            if type(pending.sentAt) == "number" and
               (os.time() - pending.sentAt > PURCHASE_OBSERVER_SECONDS or
                os.time() < pending.sentAt) then
                restore_purchase_observer()
                pending.observerExpired = true
            end
            data.nativeReplyAmbiguous = true
            data.awaitingGameConfirmation = true
            return {state="accepted", data=data}
        end
        if not observer_intact and not pending.observerExpired then
            -- A newly installed different handler can hide an error/reply.
            -- Do not declare a matching poststate while observation was lost.
            if type(pending.sentAt) == "number" and
               (os.time() - pending.sentAt > PURCHASE_OBSERVER_SECONDS or
                os.time() < pending.sentAt) then
                restore_purchase_observer()
                pending.observerExpired = true
            end
            data.nativeReplyAmbiguous = true
            data.awaitingGameConfirmation = true
            return {state="accepted",data=data}
        end
        -- The original v26 source updates ItemData by reply.item.uuid. If a
        -- reply supplied that identity, enforce *that exact new stack* is in
        -- the native inventory. Equal total stock + Gold is not enough when
        -- the reply's own stack went missing or a different stack appeared.
        if pending.replyStackUuid ~= nil then
            local stack_count = owned_exact_stack_count(
                owned, pending.replyStackUuid, pending.itemId)
            if stack_count ~= 1 then
                pending.replyAmbiguous = true
                pending.replyMatched = false
                data.nativeShopReplyMatched = false
                data.nativeReplyStackMismatch = true
                data.nativeReplyAmbiguous = true
                data.awaitingGameConfirmation = true
                return {state="accepted",data=data}
            end
            data.nativeShopReplyStackMatched = true
        end
        if count == pending.count + 1 and gold == pending.gold - pending.price then
            if pending.replyMatched and not observer_intact then
                -- If our observer was overwritten, the previous reply
                -- cannot be treated as intact handler evidence.
                data.nativeShopReplyMatched = false
                data.nativeReplyAmbiguous = true
            end
            data.purchaseProofKind = data.nativeShopReplyMatched and
                (data.nativeShopReplyStackMatched and
                "matching_original_reply_owned_stack_and_cached_poststate_no_nonce" or
                "matching_original_reply_and_cached_poststate_no_nonce") or
                "native_cached_inventory_and_gold_only"
            -- The mounted original Hotkeys view gates its completion on
            -- this exact stable observation. Preserve that contract; put
            -- additional R7 reply evidence in purchaseProofKind instead.
            data.observation = "native_shield_purchase_inventory_and_gold"
            data.nativeReplyHandlerEvidenceOnly = data.nativeShopReplyMatched
            if not restore_purchase_observer() then
                data.nativeReplyAmbiguous = true
                data.nativeShopReplyMatched = false
                data.purchaseProofKind = "native_cached_inventory_and_gold_only"
                data.observation = "native_shield_purchase_inventory_and_gold"
            end
            pending_purchase = nil
            data.beforeCount=pending.count; data.beforeGold=pending.gold
            data.spentGems=pending.price; data.purchasedCount=1
            return {state="confirmed",data=data}
        end
        if type(pending.sentAt) == "number" and
           (os.time() - pending.sentAt > PURCHASE_OBSERVER_SECONDS or
            os.time() < pending.sentAt) then
            -- A timeout retires only the temporary game-handler observer.
            -- The no-resend pending request remains unresolved indefinitely.
            restore_purchase_observer()
            pending.observerExpired = true
            data.nativeReplyObserverExpired = true
        elseif not observer_intact then
            data.nativeReplyAmbiguous = true
        end
        data.awaitingGameConfirmation=true
        return {state="accepted",data=data}
    end
    if fields.mode ~= "buy" or fields.gemOptIn ~= "1" then return invalid("Explicit gem purchase consent required") end
    if pending_purchase ~= nil then return invalid("Earlier shield purchase unresolved; never resend") end
    if count ~= 0 then return invalid("Owned duration shield already available; use inventory") end
    if chosen.price == nil or chosen.price <= 0 or chosen.price % 1 ~= 0 or gold < chosen.price then
        return invalid("Native shield price invalid or insufficient gems")
    end
    local clear, reason = M.TextInputClear()
    if not clear then return invalid("Game input not clear: " .. reason) end
    local message = member(rawget(_G,"MsgDefines"),"UIShopBuy")
    local send = member(rawget(_G,"SFSNetwork"),"SendMessage")
    if message == nil or type(send) ~= "function" then return unavailable("Native shop purchase sender unavailable") end
    if not same_native_account(account) then return invalid("Native shield shop owner changed before send") end
    if not shield_shop_pre_send_unchanged(center,player,chosen,gold,count) or
       rawget(_G,"DataCenter") ~= center or
       member(rawget(_G,"LuaEntry"),"Player") ~= player or
       not same_native_account(account) then
        return invalid("Native shield item price, stock, balance or owner changed before purchase")
    end
    pending_purchase={owner=owner,account=account,itemId=chosen.itemId,hours=hours,
        statusId=chosen.statusId,count=count,gold=gold,price=chosen.price,
        center=center,player=player,manager=member(center,"ItemManager"),
        sentAt=os.time(),replyCount=0,replyMatched=false,replyAmbiguous=false}
    if not install_purchase_observer(pending_purchase,center,player) then
        pending_purchase=nil  -- No packet has been sent; safe to decline.
        return unavailable("Original ItemBuyHandle reply observer cannot be safely installed")
    end
    if not same_native_account(account) then
        restore_purchase_observer()
        pending_purchase=nil  -- No network attempt occurred.
        return invalid("Shield shop native account changed before posting purchase")
    end
    local sent, send_result = pcall(send,message,{itemId=chosen.itemId,num=1})
    if not sent or send_result == false then
        return invalid("Native shop sender result uncertain; state must be observed, no retry")
    end
    if not same_native_account(account) then
        -- The original sender may have posted before switching accounts.
        -- Retain the pending tombstone and refuse to credit this request.
        return invalid("Native shield purchase account changed during send; outcome uncertain, no retry")
    end
    data.readOnly=false; data.requestDispatched=true; data.awaitingGameConfirmation=true
    return {state="accepted",data=data}
end

function M.ShieldShop(fields) return shield_shop(fields) end

local function shield_safe_to_consume(hours, replacement_confirmed)
    local center = rawget(_G, "DataCenter")
    local status = member(center, "StatusManager")
    local ok_cd, cooling = invoke(status, "IsUseShieldCD")
    local ok_war, war_fever = invoke(status, "WarFeverStatu")
    if not ok_cd or type(cooling) ~= "boolean" or not ok_war then
        return unavailable("Native shield cooldown/war-fever eligibility cannot be verified")
    end
    if cooling then return invalid("Game shield-use cooldown remains active") end
    -- CityShieldPage.CellsCallBack 159–161 explicitly warns and returns when
    -- WarFeverStatu is NON-nil; nil is the game's safe state.
    if war_fever ~= nil then
        return invalid("Native war-fever shield warning blocks ItemUse")
    end
    local season = rawget(_G, "SeasonUtil")
    local season_reader = member(season, "IsInSeason")
    if type(season_reader) ~= "function" then
        return unavailable("Current season source reader unavailable")
    end
    -- The original CityShieldPage invokes SeasonUtil.IsInSeason() statically.
    -- Calling with an injected receiver can make a native C# static delegate
    -- fail even if an inert Lua table ignores the extra argument.
    local ok_season, seasonal = pcall(season_reader)
    if not ok_season or type(seasonal) ~= "boolean" then
        return unavailable("Current season and alliance-field shield eligibility unavailable")
    end
    -- The current UI needs seasonal alliance-field, danger-area and missile
    -- checks (CellsCallBack 163-181). Do not infer these from city coordinates.
    if seasonal then return invalid("Native seasonal shield-placement checks require game UI") end
    local wall = member(center, "DefenceWallDataManager")
    local ok_wall, defence = invoke(wall, "GetDefenceWallData")
    local clock_type = rawget(_G, "UITimeManager")
    local ok_clock, clock = invoke(clock_type, "GetInstance")
    local ok_time, current_time = invoke(clock, "GetServerTime")
    local now = number(current_time)
    local ends = number(member(defence, "protectEndTime"))
    if not ok_wall or not ok_clock or not ok_time or now == nil or ends == nil then
        return unavailable("Current native wall protection or game server time unavailable")
    end
    if ends > now and not (replacement_confirmed and hours ~= nil and
       ends-now < hours*3600000) then
        return invalid("Active shield replacement requires native user confirmation")
    end
    local meteorite = member(center, "ActMeteoriteBattleManager")
    local ok_notice, warn = invoke(meteorite, "NeedNoticeShield")
    if not ok_notice or type(warn) ~= "boolean" then
        return unavailable("Native meteorite shield warning eligibility unavailable")
    end
    if warn then return invalid("Native meteorite-drop warning requires user confirmation") end
    return nil
end

-- Original v26 Net/Msgs/ItemUseMessage.HandleMessage (SHA 9313f37b...)
-- invokes ItemManager.ItemUseHandle (SHA febee839..., root/3), which reads
-- errorCode, uuid, itemId and remaining count, and updates ItemData. This
-- watcher is NOT an independent server ACK: native replies lack a request
-- nonce and a matching manual/parallel use cannot be uniquely attributed.
-- Never replace the original handler permanently or suppress its exceptions.
local function restore_shield_use_observer()
    local observer = shield_use_observer
    if observer == nil then return true end
    shield_use_observer = nil
    if member(observer.manager, "ItemUseHandle") ~= observer.wrapper then
        return false -- A different code owner replaced this handler.
    end
    local ok = pcall(function()
        observer.manager.ItemUseHandle = observer.rawOriginal
    end)
    return ok and member(observer.manager, "ItemUseHandle") == observer.original
end

local function install_shield_use_observer(pending, center, player)
    if shield_use_observer ~= nil then return false end
    local manager = member(center, "ItemManager")
    if type(manager) ~= "table" then return false end
    local original = member(manager, "ItemUseHandle")
    if type(original) ~= "function" then return false end
    local raw_original = rawget(manager, "ItemUseHandle")
    local observer = {manager=manager,original=original,rawOriginal=raw_original}
    observer.wrapper = function(self, reply)
        local account_before=same_native_account(pending.account)
        local active = pending_shield
        if active == pending and (os.time() < pending.sentAt or
           os.time() - pending.sentAt > SHIELD_PENDING_SECONDS) then
            pending.observerExpired = true
            restore_shield_use_observer()
        end
        local ok_original, result = pcall(original, self, reply)
        if pending_shield == pending and shield_use_observer == observer then
            pending.replyCount = pending.replyCount + 1
            local item_uuid = native_item_uuid(member(reply, "uuid"))
            local item_id = digit_id(member(reply, "itemId"))
            local remaining = number(member(reply, "count"))
            local reply_error = member(reply, "errorCode")
            local op_type = number(member(reply, "opType"))
            local same_owner = ok_original and self == manager and
                rawget(_G, "DataCenter") == center and
                member(center, "ItemManager") == manager and
                member(rawget(_G, "LuaEntry"), "Player") == player and
                account_before and same_native_account(pending.account)
            if same_owner and reply_error == nil and
               (op_type == nil or op_type == 0) and
               item_uuid == pending.uuid and item_id == pending.itemId and
               remaining == pending.count - 1 and pending.replyCount == 1 then
                pending.replyMatched = true
            else
                pending.replyAmbiguous = true
                pending.replyMatched = false
            end
            if reply_error ~= nil then pending.replyError = true end
            if not same_owner then pending.replyOwnerOrHandlerUncertain = true end
        end
        if not ok_original then error(result) end
        return result
    end
    local ok = pcall(function() manager.ItemUseHandle = observer.wrapper end)
    if not ok or member(manager, "ItemUseHandle") ~= observer.wrapper then
        if ok then
            pcall(function()
                if rawget(manager, "ItemUseHandle") == observer.wrapper then
                    manager.ItemUseHandle = raw_original
                end
            end)
        end
        return false
    end
    shield_use_observer = observer
    return true
end

local function shield_use(fields)
    local owner = shield_owner(fields)
    local hours = tonumber(fields.slot)
    if owner == nil then
        return unavailable("Exact authenticated shield session identity unavailable")
    end
    local account=request_native_account(fields)
    if account==nil then
        return invalid("Shield ItemUse requires exact native authorized UID and server")
    end
    owner=owner.."/"..account.uid.."/"..account.server
    local player = member(rawget(_G, "LuaEntry"), "Player")
    local center = rawget(_G, "DataCenter")
    if not (hours == 8 or hours == 12 or hours == 24) then
        return invalid("Shield duration must be 8, 12 or 24 hours")
    end
    if pending_shield ~= nil and pending_shield.owner ~= owner then
        return invalid("A previous owner's shield request is unresolved; never replace its pending fence")
    end
    if fields.queryOnly ~= nil and fields.queryOnly ~= "1" then
        return invalid("Only an exact read-only shield query is accepted")
    end
    local pending = pending_shield
    if fields.queryOnly == "1" then
        if pending == nil or pending.hours ~= hours then
            return invalid("No matching sent shield to verify")
        end
        local elapsed = os.time() - pending.sentAt
        if elapsed < 0 or elapsed > SHIELD_PENDING_SECONDS then
            pending.observerExpired = true
            restore_shield_use_observer()
            return invalid("Shield ItemUse remains unconfirmed; manual state refresh required")
        end
        if pending.player ~= player or pending.center ~= center or
           not same_native_account(pending.account) then
            return invalid("Native shield pending ItemUse belongs to another native player/session")
        end
        if member(center, "ItemManager") ~= pending.manager then
            return invalid("Original ItemUse handler owner/session changed")
        end
        local snapshot = shield_snapshot()
        if snapshot.state ~= "confirmed" then return snapshot end
        local count = owned_item_count(pending.uuid, pending.itemId)
        if count == nil then return unavailable("Owned shield count after ItemUse is unreadable") end
        if not same_native_account(pending.account) or
           member(rawget(_G,"LuaEntry"),"Player") ~= player or
           rawget(_G,"DataCenter") ~= center then
            return invalid("Native shield status owner changed during observation")
        end
        local observer_intact = shield_use_observer ~= nil and
            shield_use_observer.manager == pending.manager and
            member(pending.manager, "ItemUseHandle") == shield_use_observer.wrapper
        local reply_info = {
            nativeItemUseReplyObserved = pending.replyCount > 0,
            nativeItemUseReplyMatched = pending.replyMatched and
                not pending.replyAmbiguous and observer_intact,
            nativeItemUseReplyCount = pending.replyCount,
            nativeReplyCausalityConfirmed = false,
            itemUseReplyOrigin = "original_ItemManager_ItemUseHandle_no_nonce",
        }
        if pending.replyAmbiguous or (not observer_intact and not pending.observerExpired) then
            reply_info.nativeReplyAmbiguous = true
            reply_info.awaitingGameConfirmation = true
            reply_info.requestDispatched = false
            reply_info.slot = hours
            reply_info.itemUuid = pending.uuid
            return {state="accepted",data=reply_info}
        end
        local match = false
        for _, effect in pairs(snapshot.data.statuses) do
            if effect.statusId == pending.statusId and
               effect.remainingMilliseconds > 0 and
               effect.remainingMilliseconds <= pending.durationMs + 15000 and
               effect.remainingMilliseconds >= pending.durationMs - 120000 and
               snapshot.data.serverTimeMilliseconds >= pending.beforeClock then
                match = true
            end
        end
        local end_advanced = pending.beforeEndTime == nil or
            (snapshot.data.endTime ~= nil and snapshot.data.endTime > pending.beforeEndTime)
        if count == pending.count - 1 and match and end_advanced and
           snapshot.data.shieldStatusCount == 1 then
            reply_info.itemUseProofKind = reply_info.nativeItemUseReplyMatched and
                "matching_original_itemuse_reply_and_shield_poststate_no_nonce" or
                "native_cached_shield_inventory_and_MAIN_SHIELD_only"
            if not restore_shield_use_observer() then
                reply_info.nativeItemUseReplyMatched = false
                reply_info.nativeReplyAmbiguous = true
                reply_info.itemUseProofKind = "native_cached_shield_inventory_and_MAIN_SHIELD_only"
            end
            pending_shield = nil
            return {state="confirmed",data={
                observation="native_owned_shield_consumption_and_MAIN_SHIELD",
                slot=hours, itemUuid=pending.uuid, itemId=pending.itemId,
                statusId=pending.statusId, durationMilliseconds=pending.durationMs,
                consumedCount=1, remainingMilliseconds=snapshot.data.remainingMilliseconds,
                serverTimeMilliseconds=snapshot.data.serverTimeMilliseconds,
                nativeItemUseReplyObserved=reply_info.nativeItemUseReplyObserved,
                nativeItemUseReplyMatched=reply_info.nativeItemUseReplyMatched,
                nativeItemUseReplyCount=reply_info.nativeItemUseReplyCount,
                nativeReplyCausalityConfirmed=false,
                itemUseProofKind=reply_info.itemUseProofKind,
                nativeReplyAmbiguous=reply_info.nativeReplyAmbiguous,
            }}
        end
        reply_info.requestDispatched = false
        reply_info.awaitingGameConfirmation = true
        reply_info.slot = hours
        reply_info.itemUuid = pending.uuid
        return {state="accepted",data=reply_info}
    end
    if pending ~= nil then
        return invalid("Prior ItemUse is unconfirmed; never consume again in this session")
    end
    local arm=armed_shield_replace
    local replace = fields.shieldReplaceOptIn == "1" or
        (arm ~= nil and arm.owner == owner and
         same_native_account(arm.account) and arm.hours == hours and arm.expires >= os.time())
    local safety = shield_safe_to_consume(hours,replace)
    if safety ~= nil then return safety end
    local before = shield_snapshot()
    if before.state ~= "confirmed" then return before end
    if before.data.shieldStatusCount > 1 or before.data.remainingMilliseconds == nil then
        return invalid("Native active shield state is ambiguous; ItemUse blocked")
    end
    if before.data.active and not (replace and before.data.remainingMilliseconds < hours*3600000) then
        return invalid("Active MAIN_SHIELD requires native replacement confirmation")
    end
    local item, reason = shield_item_for_hours(hours)
    if item == nil then return reason end
    local count = owned_item_count(item.uuid, item.itemId)
    if count == nil or count ~= item.count then
        return invalid("Owned shield stack changed during native eligibility check")
    end
    -- The original ConfirmUse sends ItemUse only after user-facing warning
    -- routes have been passed. It sets the five-second cooldown before send.
    local network = rawget(_G, "SFSNetwork")
    local message = member(rawget(_G, "MsgDefines"), "ItemUse")
    local status = member(rawget(_G, "DataCenter"), "StatusManager")
    local item_manager = member(center, "ItemManager")
    if type(member(network, "SendMessage")) ~= "function" or message == nil or
       type(member(status, "SetUseShieldCD")) ~= "function" or
       type(member(item_manager, "ItemUseHandle")) ~= "function" then
        return unavailable("Native shield ItemUse sender/cooldown/reply handler unavailable")
    end
    -- Re-evaluate volatile warning and cooldown immediately before sending.
    local final_safety = shield_safe_to_consume(hours,replace)
    if final_safety ~= nil then return final_safety end
    if not same_native_account(account) or
       member(rawget(_G,"LuaEntry"),"Player") ~= player or
       rawget(_G,"DataCenter") ~= center then
        return invalid("Native shield owner/session changed before ItemUse")
    end
    local ok_cd, cd_result = invoke(status, "SetUseShieldCD")
    if not ok_cd or cd_result == false then
        return invalid("Native shield cooldown could not be set")
    end
    if not same_native_account(account) or
       member(rawget(_G,"LuaEntry"),"Player") ~= player or
       rawget(_G,"DataCenter") ~= center then
        return invalid("Native shield owner/session changed before ItemUse")
    end
    -- SetUseShieldCD executes game code and may synchronously process a
    -- changed inventory/effect/template. Before committing the one-shot
    -- ItemUse tombstone and network call, require the exact same source-owned
    -- stack, duration and MAIN_SHIELD endTime as the original preflight.
    local current_item = shield_item_for_hours(hours)
    local fresh = shield_snapshot()
    if current_item == nil or current_item.uuid ~= item.uuid or
       current_item.itemId ~= item.itemId or
       current_item.statusId ~= item.statusId or
       current_item.durationMs ~= item.durationMs or
       current_item.count ~= item.count or
       owned_item_count(item.uuid, item.itemId) ~= item.count or
       fresh.state ~= "confirmed" or
       fresh.data.shieldStatusCount ~= before.data.shieldStatusCount or
       fresh.data.active ~= before.data.active or
       fresh.data.endTime ~= before.data.endTime or
       fresh.data.serverTimeMilliseconds < before.data.serverTimeMilliseconds or
       not same_native_account(account) or
       member(rawget(_G,"LuaEntry"),"Player") ~= player or
       rawget(_G,"DataCenter") ~= center then
        return invalid("Native shield state, inventory, duration or owner changed before ItemUse")
    end
    armed_shield_replace=nil
    pending_shield = {
        owner=owner, account=account, player=player, center=center,
        manager=item_manager,
        hours=hours, uuid=item.uuid, itemId=item.itemId,
        statusId=item.statusId, durationMs=item.durationMs, count=item.count,
        beforeClock=before.data.serverTimeMilliseconds,
        beforeEndTime=before.data.endTime, sentAt=os.time(),
        replyCount=0, replyMatched=false, replyAmbiguous=false
    }
    if not install_shield_use_observer(pending_shield, center, player) then
        pending_shield = nil -- The network request has not yet been sent.
        return unavailable("Original ItemUseHandle observer could not be installed")
    end
    if not same_native_account(account) then
        restore_shield_use_observer()
        pending_shield=nil -- No packet sent; account changed while observer installed.
        return invalid("Native shield account changed before posting ItemUse")
    end
    -- A native exception/false return can follow a transmitted request.
    -- Install the tombstone before calling and retain it on uncertainty.
    local ok_send, send_result = pcall(member(network, "SendMessage"), message,
        {uuid=item.uuid, num=1})
    if not ok_send or send_result == false then
        return invalid("Native ItemUse result is uncertain; observe without resending")
    end
    if not same_native_account(account) then
        -- Keep the no-resend record: an outbound RPC may already have escaped.
        return invalid("Native ItemUse account changed during send; outcome uncertain, no retry")
    end
    return {state="accepted",data={
        requestDispatched=true,awaitingGameConfirmation=true,
        slot=hours,itemUuid=item.uuid,itemId=item.itemId
    }}
end

local slots = {
    attack = {"1", "2", "3", "4"},
    recall = {"1", "2", "3", "4"},
    equipment = {"1", "2", "3", "4"},
    shieldUse = {"8", "12", "24"},
    frontlineReinforce = {"5"},
    shieldOverlay = {"0"},
    randomRelocate = {"0"},
    allianceRelocate = {"0"},
}

function M.Handle(fields)
    if type(fields) ~= "table" or type(fields.action) ~= "string" or
       type(fields.slot) ~= "string" or slots[fields.action] == nil then
        return invalid("Invalid feature018 hotkey kind or slot")
    end
    local valid = false
    for _, slot in ipairs(slots[fields.action]) do
        if fields.slot == slot then valid = true; break end
    end
    if not valid then return invalid("Hotkey slot is not bound to this game action") end
    local account=request_native_account(fields)
    if not account then
        return invalid("Hotkey request UID differs from current native game account")
    end
    -- Always revalidate game-side text focus when dispatch happens, even if
    -- the WinForms sampler saw a fresh earlier input-clear observation.
    if not (fields.action == "shieldUse" and fields.queryOnly == "1") then
        local clear, reason = M.TextInputClear()
        if not clear then
            return invalid("Game text input is active or unverifiable: " .. reason)
        end
    end
    if fields.action == "shieldOverlay" then
        local status=M.VisibleCityShieldSnapshot()
        if not same_native_account(account) then
            return invalid("Native shield overlay account changed during observation")
        end
        return status
    end
    if fields.action == "shieldUse" then
        return shield_use(fields)
    end
    if fields.action == "equipment" then
        -- Original Alt+1–4 references per-profile saved *equipment* loadouts.
        -- The v26 PVPFormationPresetManager.SendPresetApplyMsg uses game
        -- formation preset UUIDs and is not an equivalent equipment-slot
        -- mapping. A source-backed gear-owner/slot-ACK producer is required.
        return unavailable("Saved equipment preset cannot be mapped to a native v26 equipped-gear apply action")
    end
    return unavailable("Current-client gameplay operation has no proven native action producer: " .. fields.action)
end

function M.Register(feature)
    if type(feature) ~= "table" or type(feature.RegisterHandler) ~= "function" then return false end
    local actions = feature.RegisterHandler("feature018_hotkey_action", M.Handle)
    local input = feature.RegisterHandler("feature018_input_focus", function(_)
        return M.InputFocusSnapshot()
    end)
    if actions ~= true or input ~= true then return false end
    feature.RegisterHandler("feature018_shield_shop", shield_shop)
    feature.RegisterHandler("feature018_visible_city_shields", function(fields)
        local account = request_native_account(fields)
        if not account then return invalid("Visible city shields account unavailable") end
        local result = M.VisibleCityShieldSnapshot()
        if not same_native_account(account) then return invalid("Visible city shields account changed") end
        return result
    end)
    mark_array = type(feature.Array) == "function" and feature.Array or nil
    return true
end

return M
