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
-- Net/Msgs/Alliance/AllianceMoveCityMessage.luac sha256
--   dfed59efaad7ec93cb0b89be67ff5d5e6720443b7ac3332f1ef5f86bb5a84579:
--   OnCreate lines 14-23 serializes costType/byInvite and optional pin UUID;
--   HandleMessage lines 39-42 forwards only result=1 to WorldMvHandle(reply,240).
-- Net/Msgs/ItemUseMessage.luac sha256
--   9313f37b8183fd7338fcfc82cfd3a0d211db1d9cc5ac67fc5d0692de78262a1c:
--   OnCreate line 16 serializes item UUID with PutUtfString.
-- DataCenter/AllianceData/AllianceRallyPointDataManager.luac sha256
--   8d970b439bfb0acf3033b4375da1731607a3879b063f24b652010ef5652d73f6:
--   GetMyAllianceRallyPoint root/25 lines 274-277 only reads markDataDic[type];
--   GetRecommendType root/27 lines 286-288 reads recommendType.
-- DataCenter/WorldFavoData/AllianceMarkData.luac sha256
--   b08cc1e15a9fd57d3cc7132000cd081e2ac5ea4473e6243c0ff8d36f63b00578:
--   ParseData root/3 lines 51-106 maps markType->type, point->pos,
--   serverId->server and allianceId->allianceId; GetPointIndex root/18
--   lines 185-188 calculates pos // 10.
-- UI/UILWAlliance/UIChooseRallyPoint/UIChooseRallyPointView.luac sha256
--   2c66d46152e1843245b9f45db3b3f028437134fb186da34395ee53dfe0bce8c5:
--   DataDefine root/4 lines 65-68 reads GetAllAllianceRallyPoints;
--   OnBtnMoveClick root/9 lines 106-110 calls AllianceMoveCityToSelectedRallyPoint.
--
-- Public RandomRelocate/AllianceRelocate perform one original client action
-- after exact-owner and current native warning/item/rally checks. queryOnly=1
-- observes that request without retransmission; preflightOnly=1 preserves the
-- earlier source observation. No action occurs when this module is loaded.

local M = {VERSION = "feature018-relocation-actions-10"}
local baseline = {}
local last_owner = nil
local inspect_relocation_readiness
local HOME_SERVER = 2289 -- Authorized sub-account only. 2212 is the read-only main.
local APPROVED_UID = "3133546747002289"

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
    if type(value) == "number" and
       (value ~= value or value < 1 or value % 1 ~= 0 or
        (math.type and math.type(value) == "float" and
         value > 9007199254740991)) then return nil end
    local id = tostring(value)
    if id:match("^[1-9]%d*$") and #id <= 20 then return id end
    return nil
end

-- ItemUseMessage.OnCreate serializes the bag stack UUID through PutUtfString.
-- Unlike Player.uid or allianceId it is an opaque native item token; decimal-
-- only account validation rejects legitimate alphanumeric item UUIDs.
local function native_item_uuid(value)
    if type(value) ~= "string" and type(value) ~= "number" then return nil end
    local token = tostring(value)
    if #token < 1 or #token > 128 or token == "0" or
       token:find("[^%w_-]") then return nil end
    return token
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
    for _,raw_server in ipairs({source_server,self_server,current_server})do
        if raw_server~=HOME_SERVER and raw_server~=tostring(HOME_SERVER) then
            return nil,"Connected world is not the exact authorized server-2289 home world"
        end
    end
    source_server, self_server = integer(source_server, 1), integer(self_server, 1)
    current_server, position = integer(current_server, 1), integer(position, 0)
    if player_uid == nil or not ok_source or not ok_self or not ok_current or
       not ok_pos or source_server == nil or self_server == nil or
       current_server == nil or position == nil then
        return nil, "Native owner, server and city position snapshot incomplete"
    end
    if player_uid ~= APPROVED_UID then
        return nil, "Connected UID does not match the exact authorized 2289 sub-account"
    end
    if source_server ~= HOME_SERVER or self_server ~= HOME_SERVER or
       current_server ~= HOME_SERVER then
        return nil, "Connected world is not the authorized server-2289 home world"
    end

    local item_data = member(rawget(_G, "DataCenter"), "ItemData")
    local ids = rawget(_G, "SpecialItemId")
    local inventory = {}
    local inventory_complete = true
    -- These are the ordinary City move and season consumables. The ORIGINAL
    -- F9/F10 bag OnItemUse route instead uses ITEM_MOVE_RANDOM and
    -- LW_ITEM_ALLY_MOVE_CITY. An absent/temporarily unreadable unrelated
    -- class must not suppress exact-owner world state or a valid bag action.
    -- Retain explicit incompleteness rather than fabricating zero stock.
    for _, item_type in ipairs({"ITEM_MOVE_CITY", "SEASON_LIMIT_MOVE_CITY",
                               "ITEM_ALLIANCE_CITY_MOVE"}) do
        local id = integer(member(ids, item_type), 1)
        if id == nil then
            inventory[item_type] = {available=false, reason="source item ID unknown"}
            inventory_complete = false
        else
            local ok_item, item = invoke(item_data, "GetItemById", id)
            local count = ok_item and (item == nil and 0 or
                integer(member(item, "count"), 0)) or nil
            if count == nil then
                inventory[item_type] = {id=id, available=false,
                    reason=ok_item and "count unreadable" or "reader unavailable"}
                inventory_complete = false
            else
                inventory[item_type] = {id=id, count=count, available=true}
            end
        end
    end
    local ok_alliance, in_alliance = invoke(player, "IsInAlliance")
    if not ok_alliance or type(in_alliance) ~= "boolean" then
        return nil, "Native alliance membership unavailable"
    end

    return {observation="native_player_world_position_and_owned_move_items",
        ownerUid=player_uid, sourceServerId=source_server,
        selfServerId=self_server, currentServerId=current_server,
        worldPointId=position, inAlliance=in_alliance,
        itemCounts=inventory, standardItemInventoryComplete=inventory_complete,
        noActionDispatched=true}, nil
end

-- The original F10 TryAllianceMoveCity first reads the OTHER-server rally.
-- If one exists it opens a choice dialog, but WITHOUT one it immediately
-- calls AllianceMoveCityToSourceRallyPoint, whose native path can SEND
-- MsgDefines.AllianceMoveCity. Opening the handler as a "safe dialog" would
-- therefore be unsafe. Only inspect getters proven read-only in original v26.
local function alliance_rally_snapshot(player)
    local owner_alliance_ok, owner_alliance = invoke(player, "GetAllianceUid")
    if not owner_alliance_ok then
        owner_alliance=member(player,"allianceId") -- original bag Ctrl 286 compares this native field
        owner_alliance_ok=owner_alliance~=nil
    end
    owner_alliance = uid(owner_alliance)
    if not owner_alliance_ok or not owner_alliance then
        return nil, "Native owner alliance UID not verifiable"
    end
    local marks = member(rawget(_G, "DataCenter"), "AllianceRallyPointDataManager")
    local types = rawget(_G, "MarkType")
    local home_type = integer(member(types, "Alliance_rally"), 1)
    local remote_type = integer(member(types, "Alliance_OtherServerRally"), 1)
    if home_type == nil or remote_type == nil or home_type == remote_type then
        return nil, "Native F10 rally types not verifiable"
    end
    if type(member(marks, "GetMyAllianceRallyPoint")) ~= "function" or
       type(member(marks, "GetRecommendType")) ~= "function" then
        return nil, "Native F10 rally getters unavailable"
    end
    local ok_recommended, recommended = invoke(marks, "GetRecommendType")
    recommended = integer(recommended, 1)
    if not ok_recommended or (recommended ~= home_type and recommended ~= remote_type) then
        return nil, "Native F10 recommended rally type unknown"
    end

    local function inspect_one(mark_type)
        local ok, mark = invoke(marks, "GetMyAllianceRallyPoint", mark_type)
        if not ok then return nil, "Native F10 rally read failed" end
        if mark == nil then return false, nil end
        local mark_alliance = uid(member(mark, "allianceId"))
        local mark_server = integer(member(mark, "server"), 1)
        local mark_pos = integer(member(mark, "pos"), 0)
        local seen_type = integer(member(mark, "type"), 1)
        local ok_point, point = invoke(mark, "GetPointIndex")
        point = integer(point, 0)
        if mark_alliance == nil or mark_alliance ~= owner_alliance or
           mark_server == nil or mark_pos == nil or seen_type ~= mark_type or
           not ok_point or point == nil or point ~= math.floor(mark_pos / 10) then
            return nil, "Native F10 rally mark owner/type/server/point mismatch"
        end
        return {markType=mark_type, serverId=mark_server, worldPointId=point,
                authorizedHomeServer=mark_server == HOME_SERVER,
                sameOwnerAlliance=true}, nil
    end

    local home, home_error = inspect_one(home_type)
    if home_error then return nil, home_error end
    local remote, remote_error = inspect_one(remote_type)
    if remote_error then return nil, remote_error end
    local native_chooser = remote ~= false
    return {
        observation="original_v26_alliance_rally_mark_data",
        ownerAllianceUid=owner_alliance,
        homeRally=home ~= false and home or nil,
        otherServerRally=remote ~= false and remote or nil,
        recommendedMarkType=recommended,
        nativePath=native_chooser and "choose_rally_point_ui" or "direct_alliance_move_send_risk",
        nativeChooserWouldOpen=native_chooser,
        nativeDirectSendRisk=not native_chooser,
        selectedTargetValidated=false,
        confirmationCompleted=false,
        worldMvAcknowledged=false,
        noActionDispatched=true,
    }, nil
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
    local focus_ok, focus_clear = false, false
    if not query_only and type(input_clear) == "function" then
        focus_ok, focus_clear = pcall(input_clear)
    end
    if not query_only and (not focus_ok or focus_clear ~= true) then
        return failed("GAME_PRECONDITION_FAILED", "Game text-input focus is not verified clear")
    end
    local snapshot, reason = world_snapshot()
    if not snapshot then
        -- A source/world identity change invalidates prior before states.
        baseline = {}
        last_owner = nil
        return unavailable(reason)
    end
    -- The three conventional move consumables in world_snapshot are not the
    -- original F9/F10 bag actions. Probe the exact action item/expiry now,
    -- even when subsequent F10 rally inspection cannot resolve an owner.
    snapshot.actionReadiness = inspect_relocation_readiness(action)
    if action == "allianceRelocate" then
        if not snapshot.inAlliance then
            baseline = {}
            last_owner = nil
            return unavailable("Native F10 alliance membership required", snapshot)
        end
        local player = member(rawget(_G, "LuaEntry"), "Player")
        local marks, marks_error = alliance_rally_snapshot(player)
        if not marks then
            baseline = {}
            last_owner = nil
            return unavailable(marks_error, snapshot)
        end
        snapshot.allianceRally = marks
        snapshot.actionReadiness.originalNativeRoute =
            marks.nativeChooserWouldOpen and "UIChooseRallyPoint" or
            "MoveCrossServer_BackToSrcServer"
        snapshot.actionReadiness.chooserRequestEligible =
            marks.nativeChooserWouldOpen and marks.otherServerRally ~= nil
        snapshot.actionReadiness.homeDirectRelocationEnabled = false
    end
    local owner_token = session .. "/" .. snapshot.ownerUid .. "/" .. snapshot.sourceServerId
    if owner_token ~= last_owner then
        baseline = {}
        last_owner = owner_token
    end
    if query_only and baseline[action] == nil then
        return failed("GAME_PRECONDITION_FAILED", "No owned relocation preflight to compare")
    end
    if query_only and snapshot.allianceRally and
       baseline[action].ownerAllianceUid ~= snapshot.allianceRally.ownerAllianceUid then
        -- The session/player can join a different alliance without changing
        -- its city position. Do not compare a new alliance's targets to an
        -- earlier alliance's observation, even when marker points coincide.
        baseline[action] = nil
        return failed("GAME_PRECONDITION_FAILED", "Native alliance owner changed since relocation preflight")
    end
    local rally_fingerprint = ""
    if snapshot.allianceRally then
        local marks = snapshot.allianceRally
        local function mark_key(value)
            return value and table.concat({value.markType, value.serverId,
                                           value.worldPointId}, ":") or "absent"
        end
        rally_fingerprint = table.concat({mark_key(marks.homeRally),
            mark_key(marks.otherServerRally), marks.recommendedMarkType}, "/")
    end
    if not query_only then
        baseline[action] = {worldPointId=snapshot.worldPointId,
                            rallyFingerprint=rally_fingerprint,
                            ownerAllianceUid=snapshot.allianceRally and
                                snapshot.allianceRally.ownerAllianceUid or nil}
    end
    local before = baseline[action].worldPointId
    local after = snapshot.worldPointId
    snapshot.beforeWorldPointId = before
    snapshot.afterWorldPointId = after
    snapshot.worldPointChanged = before ~= after
    snapshot.rallyObservationChanged = baseline[action].rallyFingerprint ~= rally_fingerprint
    snapshot.changeAttributedToHotkey = false
    snapshot.worldMvAcknowledged = false
    snapshot.serverAckAttributable = false
    snapshot.actionRequested = action
    snapshot.requiredUserStep = action == "randomRelocate"
        and "Select an exact native random-relocation item/mode and handle every item warning"
        or "Use native rally choice/confirmation; the no-other-server-rally path may send immediately"
    -- Even if an external/manual relocation changed the city coordinate after
    -- this snapshot, it cannot turn a never-sent hotkey into a success.
    return unavailable(action == "randomRelocate"
        and "Original F9 bag item is source identified but random destination/server ACK are not established by read-only preflight"
        or (snapshot.allianceRally and snapshot.allianceRally.nativeChooserWouldOpen
            and "Original F10 native rally chooser requires explicit user selection; no relocation or server ACK observed"
            or "Original F10 source-home route sends MoveCrossServer, not AllianceMoveCity; direct send suppressed"), snapshot)
end

-- Recovery of the actual random/alliance item branch:
-- ORIGINAL UI/UILWBag/UILWBagMain/Controller/UILWBagMainCtrl.luac
-- sha256 aa7f2ebbc31ed8c0ef600ecc55659183d125368d3f572b3ad6f3414c0a91a48a
-- OnItemUse root/6 lines 906-1041 selects ITEM_MOVE_RANDOM / LW_ITEM_ALLY_MOVE_CITY.
-- root/6/12 924-930 sends ItemUse({uuid,num,useItemFromType=0});
-- root/6/11 920 calls MoveCityUtil.TryAllianceMoveCity(4,false).
-- We reject every currently active native warning rather than silently accepting
-- its callback: battlefield, season hunter, meteorite, zombie challenge, virus.
-- Thus a sent request has traversed the same no-warning native conditions.
-- F9 ItemUse reply observation wraps the original ItemUseHandle during its
-- outstanding request and restores it before reporting success. Without a
-- matching native ItemUse UUID and independent inventory+position it remains
-- unconfirmed; the server's random destination is never predicted.
-- Important v26 correction: AllianceMoveCityToSourceRallyPoint on *this*
-- source=self=2289 home world invokes upvalue MoveCityFromOtherToSource.
-- That upvalue instead sends MsgDefines.MoveCrossServer (BackToSrcServer,
-- dstPoint=0, useCrossItem=not CanFreeAllianceMove). Its line-136
-- AllianceMoveCity sender runs only on the OTHER branch and is not the 2289
-- direct F10 packet. Never invoke this mismatched direct path or claim its
-- WorldMvHandle(reason=240) could acknowledge the attempted request.
local pending=nil
local chooser_pending=nil
local function clear_hook(tx)
    if tx.hook and member(tx.manager,tx.method)==tx.hook then
        tx.manager[tx.method]=tx.original
    end
end
function M.Dispose()
    if pending then
        clear_hook(pending)
        -- Disposal can race a reply already sent by the server. Preserve
        -- the one-send tombstone, but never imply its detached observer can
        -- still correlate a future ItemUseHandle invocation.
        pending.observerDetached=true
    end
    chooser_pending=nil
end
local function static_bool(object,name,...)
    local fn=member(object,name)
    if type(fn)~="function" then return nil end
    local ok,value=pcall(fn,...)
    if not ok or type(value)~="boolean" then return nil end
    return value
end
local function warning_clear(action)
    if static_bool(rawget(_G,"BattleFieldUtil"),"InBattleField")~=false then
        return nil,"Relocation is unavailable in a battlefield or native battlefield state is unknown"
    end
    local center=rawget(_G,"DataCenter")
    local checks={{"SeasonHunterManager","IsInBattle"},{"LWZombieRushManager","IsChallenging"},
        {"ActMeteoriteBattleManager",action=="randomRelocate" and "NeedNoticeRandomMoveCity" or "NeedNoticeAllianceMoveCityDropMine"}}
    if action=="allianceRelocate" then checks[#checks+1]={"ActMeteoriteBattleManager","NeedNoticeAllianceMoveCityPosition"} end
    for _,check in ipairs(checks)do
        local ok,value=invoke(member(center,check[1]),check[2])
        if not ok or value~=false then return nil,"Native relocation warning requires game confirmation: "..check[2] end
    end
    local entry=rawget(_G,"LuaEntry")
    local effect=member(entry,"Effect")
    local effect_id=member(rawget(_G,"EffectDefine"),"SEASON_VIRUS_MAX_EXPLODE")
    local status_id=rawget(_G,"SELF_EXPLOSION_STATUS_ID")
    if effect_id==nil or status_id==nil then return nil,"Native relocation virus warning classification unavailable" end
    local ok_effect,value=invoke(effect,"GetGameEffect",effect_id)
    local ok_status,status=invoke(effect,"HasStatus",status_id)
    if not ok_effect or value~=0 or not ok_status or status~=false then
        return nil,"Native relocation virus/explosion warning requires game confirmation"
    end
    return true
end
local function owned_item(action)
    local name=action=="randomRelocate" and "ITEM_MOVE_RANDOM" or "LW_ITEM_ALLY_MOVE_CITY"
    local item_id=member(rawget(_G,"SpecialItemId"),name)
    if item_id==nil then return nil,"Original relocation item classification unavailable: "..name end
    local itemdata=member(rawget(_G,"DataCenter"),"ItemData")
    local ok,item=invoke(itemdata,"GetItemById",item_id)
    local count=item and integer(member(item,"count"),0) or 0
    local item_uid=item and native_item_uuid(member(item,"uuid"))
    if not ok or not item_uid or not count or count<1 then return nil,"No owned native relocation item: "..name end
    if tostring(member(item,"itemId"))~=tostring(item_id) then return nil,"Owned relocation item identity mismatch" end
    local expired=static_bool(rawget(_G,"UIUtil"),"CheckItemIsExpiredByUuid",member(item,"uuid"))
    if expired~=false then return nil,"Native relocation item expired or expiry is unknown" end
    -- Original ItemUseMessage.OnCreate line 16 uses PutUtfString("uuid").
    -- Preserve exact 64-bit game UUID digits as a Lua string, not a number.
    return {itemId=tostring(item_id),itemUuid=item_uid,nativeUuid=item_uid,count=count,nativeItemId=item_id}
end
-- No mutation: exact v26 bag item + original native warning and scene
-- prerequisites. The random destination is server-chosen, not derivable
-- from this read-only observation; the home rally mark is not sent as pointId.
inspect_relocation_readiness=function(action)
    local item,why=owned_item(action)
    local clear,warning=warning_clear(action)
    local readiness={
        observation="original_v26_relocation_owned_item_and_warning_preflight",
        action=action,ownerItemPresent=item~=nil,
        itemType=action=="randomRelocate" and "ITEM_MOVE_RANDOM" or "LW_ITEM_ALLY_MOVE_CITY",
        itemId=item and item.itemId or nil,
        itemUuid=item and item.itemUuid or nil,
        itemCount=item and item.count or 0,
        itemBlocker=item and nil or why,
        warningsClear=clear==true,
        warningBlocker=clear and nil or warning,
        readOnly=true,requestDispatched=false,
        destinationVerified=false,serverReplyObserved=false,
    }
    if action=="allianceRelocate" then
        local scene=static_bool(rawget(_G,"SceneUtils"),"GetIsInCity")
        local ok,source_login=invoke(member(rawget(_G,"LuaEntry"),"Player"),"IsLoginSourceServer")
        readiness.worldSceneReady=scene==false
        readiness.sourceServerLogin=ok and source_login==true
    end
    return readiness
end
-- R8 read-only endpoint for authenticated feature018_relocation_preflight.
-- Unlike the historical preflightOnly before/after tracker, this obtains an
-- independent CURRENT original-v26 readiness observation; it never creates
-- a pending action, opens a chooser, touches network or attributes a reply.
-- "actionable" here means only that an original F9 ItemUse REQUEST can be
-- attempted under the native owner+stock+warning+focus guards. It does not
-- mean the server will choose a valid destination or acknowledge the move.
-- F10's source=self=2289 native direct route is MoveCrossServer; it is
-- NEVER actionable as relocation even if a UI chooser is available.
function M.Readiness(fields,input_clear,action)
    if action==nil and type(fields)=="table" then action=fields.action end
    if action~="randomRelocate" and action~="allianceRelocate" then
        return failed("GAME_PRECONDITION_FAILED",
            "Original relocation readiness requires exact F9 or F10 action")
    end
    if not key_identity(fields,action) then
        return failed("GAME_PRECONDITION_FAILED",
            "Original relocation readiness requires an exact owner session and slot zero")
    end
    local snapshot,why=world_snapshot()
    if not snapshot then return unavailable(why) end
    local focus_ok,focus_clear=false,false
    if type(input_clear)=="function" then
        focus_ok,focus_clear=pcall(input_clear)
    end
    local input_ready=focus_ok and focus_clear==true
    local item=inspect_relocation_readiness(action)
    local rally, rally_error
    if action=="allianceRelocate" and snapshot.inAlliance then
        rally,rally_error=alliance_rally_snapshot(
            member(rawget(_G,"LuaEntry"),"Player"))
    end
    local ui=rawget(_G,"UIManager")
    local chooser_components_present,chooser_already_open=false,false
    if action=="allianceRelocate" then
        local window=member(rawget(_G,"UIWindowNames"),"UIChooseRallyPoint")
        local ok,manager=invoke(ui,"GetInstance")
        if ok and manager and window~=nil and
           type(member(manager,"OpenWindow"))=="function" and
           type(member(manager,"IsWindowOpen"))=="function" then
            local observed,visible=invoke(manager,"IsWindowOpen",window)
            if observed and type(visible)=="boolean" then
                chooser_components_present=true
                chooser_already_open=visible
            end
        end
    end
    local chooser_eligible=action=="allianceRelocate" and
        snapshot.inAlliance and rally~=nil and
        rally.nativeChooserWouldOpen and rally.otherServerRally~=nil and
        chooser_components_present and input_ready and item.ownerItemPresent and
        item.warningsClear and not chooser_already_open
    local sender_ready=type(member(rawget(_G,"SFSNetwork"),"SendMessage"))=="function" and
        member(rawget(_G,"MsgDefines"),"ItemUse")~=nil and
        type(member(member(rawget(_G,"DataCenter"),"ItemManager"),
            "ItemUseHandle"))=="function"
    local f9_ready=action=="randomRelocate" and input_ready and
        item.ownerItemPresent and item.warningsClear and sender_ready
    -- An owner-confirmed stock/focus snapshot is NOT fresh action readiness
    -- while an earlier send (including an uncertain send) or chooser request
    -- is outstanding. No new local send may be attempted until that lifetime
    -- is explicitly disposed; do not advertise another actionable F9 key.
    local outstanding=pending~=nil or chooser_pending~=nil
    if outstanding then
        f9_ready=false
        chooser_eligible=false
    end
    local route=action=="randomRelocate" and "ItemUse" or
        (not snapshot.inAlliance and "AllianceMembershipRequired" or
        (rally and rally.nativeChooserWouldOpen and "UIChooseRallyPoint" or
         "MoveCrossServer_BackToSrcServer"))
    local blocker
    if outstanding then
        blocker=pending and "Previous original F9 item-use is unresolved; never resend" or
            "Previous original F10 chooser request is unresolved; never reopen or send"
    elseif not input_ready then blocker="Native game text input is active or unverifiable"
    elseif not item.ownerItemPresent then blocker=item.itemBlocker
    elseif not item.warningsClear then blocker=item.warningBlocker
    elseif action=="randomRelocate" and not sender_ready then
        blocker="Original ItemUse sender or reply observer unavailable"
    elseif action=="allianceRelocate" and not snapshot.inAlliance then
        blocker="Native F10 alliance membership unavailable"
    elseif action=="allianceRelocate" and rally_error then blocker=rally_error
    elseif action=="allianceRelocate" and not (rally and rally.nativeChooserWouldOpen) then
        blocker="Original source-home F10 would send MoveCrossServer, not AllianceMoveCity"
    elseif action=="allianceRelocate" and not chooser_components_present then
        blocker="Original F10 chooser UI not available"
    elseif action=="allianceRelocate" and chooser_already_open then
        blocker="Original F10 chooser already open; no attributed new request"
    end
    return {state="confirmed",data={
        observation="original_v26_authenticated_relocation_readiness",
        readOnly=true,action=action,slot=0,
        ownerUid=snapshot.ownerUid,sourceServerId=snapshot.sourceServerId,
        selfServerId=snapshot.selfServerId,currentServerId=snapshot.currentServerId,
        worldPointId=snapshot.worldPointId,inAlliance=snapshot.inAlliance,
        inputFocusClear=input_ready,
        ownerItemPresent=item.ownerItemPresent,itemType=item.itemType,
        itemId=item.itemId,itemUuid=item.itemUuid,itemCount=item.itemCount,
        warningsClear=item.warningsClear,warningBlocker=item.warningBlocker,
        nativeItemBlocker=item.itemBlocker,
        originalNativeRoute=route,originalPacketSend=false,
        requestDispatched=false,serverReplyObserved=false,
        serverAckAttributable=false,relocationConfirmed=false,
        destinationVerified=false,actionable=f9_ready==true,
        nativeItemUseRequestReady=f9_ready==true,
        oneSendFenceActive=outstanding,
        originalItemUseOutstanding=pending~=nil,
        nativeChooserRequestOutstanding=chooser_pending~=nil,
        chooserRequestEligible=chooser_eligible==true,
        chooserVisibleVerified=chooser_already_open,
        chooserAlreadyOpen=chooser_already_open,
        ownerSelectionRequired=(rally~=nil and rally.nativeChooserWouldOpen)==true,
        homeDirectRelocationEnabled=false,
        currentBlocker=blocker}}
end
local function action_receipt(tx,snapshot,confirmed)
    return {state=confirmed and "confirmed" or "accepted",data={
        observation=confirmed and "native_relocation_reply_item_and_world_position" or "native_relocation_pending",
        action=tx.action,slot=0,ownerUid=snapshot.ownerUid,sourceServerId=snapshot.sourceServerId,
        itemId=tx.item.itemId,itemUuid=tx.item.itemUuid,beforeCount=tx.item.count,
        afterCount=tx.afterCount,beforeWorldPointId=tx.before,afterWorldPointId=snapshot.worldPointId,
        acknowledgedWorldPointId=tx.replyPoint,
        worldPointChanged=snapshot.worldPointId~=tx.before,
        requestDispatched=tx.sendOutcomeConfirmed==true,
        nativeSendAttempted=true,
        nativeSendOutcomeUncertain=tx.sendOutcomeUncertain==true,
        oneSendNoRetry=true,
        awaitingGameConfirmation=not confirmed,relocationConfirmed=confirmed,
        replyObserved=tx.reply==true,worldMvAcknowledged=false,
        replyExactItemUuidObserved=tx.reply==true,
        replyUseOperationCompatible=tx.replyUseOperationCompatible==true,
        independentWorldAndInventoryPoststateVerified=confirmed,
        clientRequestNonceAvailable=false,serverAckAttributable=false,
        observerDetached=tx.observerDetached==true,
        warningsClear=true,readOnly=false}}
end
-- Original TryAllianceMoveCity lines 40-47: when a genuine same-alliance
-- OtherServerRally exists, open the original chooser UI only. The owner must
-- make a separate native destination/confirmation choice. Opening the dialog
-- is not a MoveCrossServer/AllianceMoveCity request or evidence of relocation.
-- V26 Framework/UI/UIManager:IsWindowOpen(name) accepts window states
-- Create/Loading/Open, not necessarily a visually rendered screen. A
-- protected OpenWindow call can return normally without opening anything
-- (guide/PVE guards); inspect this native registered-window lifecycle but
-- NEVER infer visual visibility or owner selection from it.
local function native_chooser_state()
    local ui=rawget(_G,"UIManager")
    local ok_manager,manager=invoke(ui,"GetInstance")
    local window=member(rawget(_G,"UIWindowNames"),"UIChooseRallyPoint")
    if not ok_manager or manager==nil or
       window==nil or type(member(manager,"IsWindowOpen"))~="function" then
        return nil,"Native F10 chooser visibility reader unavailable"
    end
    local ok_open,is_open=invoke(manager,"IsWindowOpen",window)
    if not ok_open or type(is_open)~="boolean" then
        return nil,"Native F10 chooser visibility could not be established"
    end
    return {manager=manager,window=window,visible=is_open},nil
end
local function chooser_receipt(snapshot,tx,visible)
    return {state="accepted",data={
        observation="original_v26_F10_rally_chooser_native_window_lifecycle",
        action="allianceRelocate",ownerUid=snapshot.ownerUid,
        sourceServerId=snapshot.sourceServerId,
        beforeWorldPointId=tx.before,afterWorldPointId=snapshot.worldPointId,
        otherServerRally=tx.remote,homeRally=tx.home,
        chooserRequestMade=true,chooserVisibleVerified=false,
        chooserWindowRegistered=visible,
        chooserWindowStateObserved=true,
        chooserWindowStateScope="create_loading_or_open",
        chooserStillOpen=visible,
        ownerSelectionRequired=true,ownerSelectedRallyObserved=false,
        destinationAuthorized=false,requestDispatched=false,
        gameRequestAcknowledged=false,serverAckAttributable=false,
        relocationConfirmed=false,itemDebitedVerified=false,
        originalPacketSend=false,readOnly=tx.observationOnly,sourceVersion=26}}
end
local function observe_original_f10_chooser(snapshot,token,query)
    local tx=chooser_pending
    if not tx or tx.token~=token then
        return failed("GAME_PRECONDITION_FAILED",
            "No exact owner/session native F10 chooser request to observe")
    end
    if snapshot.worldPointId~=tx.before or not snapshot.inAlliance then
        return failed("GAME_ACTION_UNCONFIRMED",
            "Native owner alliance or city position changed after chooser request; no attributed relocation")
    end
    local current,why=alliance_rally_snapshot(
        member(rawget(_G,"LuaEntry"),"Player"))
    local remote=current and current.otherServerRally
    if not current or current.ownerAllianceUid~=tx.allianceUid or
       not remote or remote.serverId~=tx.remote.serverId or
       remote.markType~=tx.remote.markType or
       remote.worldPointId~=tx.remote.worldPointId then
        return failed("GAME_ACTION_UNCONFIRMED",
            why or "Native F10 chooser rally/owner changed after open request")
    end
    local observed,err=native_chooser_state()
    if not observed then return unavailable(err) end
    tx.observationOnly=query
    return chooser_receipt(snapshot,tx,observed.visible)
end
local function request_original_f10_chooser(snapshot,marks,token)
    local observed,err=native_chooser_state()
    if not observed then return unavailable(err,snapshot) end
    if type(member(observed.manager,"OpenWindow"))~="function" then
        return unavailable("Original F10 rally chooser open method unavailable",snapshot)
    end
    if observed.visible then
        return unavailable("Original native rally chooser was already open; cannot attribute it to this F10 request",snapshot)
    end
    local tx={token=token,before=snapshot.worldPointId,
        allianceUid=marks.ownerAllianceUid,
        remote=marks.otherServerRally,home=marks.homeRally,
        observationOnly=false}
    -- Tombstone BEFORE the GUI request. If native OpenWindow throws after
    -- partially opening, repeating would spuriously reset its user selection.
    chooser_pending=tx
    local ok_open,open_result=pcall(member(observed.manager,"OpenWindow"),observed.manager,
        observed.window,{anim=true},{costType=4,isInviteMove=false})
    if not ok_open or open_result==false then
        return failed("GAME_ACTION_UNCONFIRMED",
            "Native F10 chooser open outcome unknown; no relocation ACK or automatic reopen")
    end
    return observe_original_f10_chooser(snapshot,token,false)
end
local function perform(fields,input_clear,action)
    if type(fields)~="table" then return failed("GAME_PRECONDITION_FAILED","Invalid relocation fields") end
    if fields.preflightOnly~=nil and fields.preflightOnly~="1" then
        return failed("GAME_PRECONDITION_FAILED","Unknown relocation preflight mode")
    end
    if fields.preflightOnly=="1" then return preflight(fields,input_clear,action) end
    local session=key_identity(fields,action)
    if not session then return failed("GAME_PRECONDITION_FAILED","Invalid owned relocation session") end
    if fields.queryOnly~=nil and fields.queryOnly~="1" then return failed("GAME_PRECONDITION_FAILED","Invalid relocation query mode") end
    local snapshot,reason=world_snapshot()
    if not snapshot then return failed("GAME_PRECONDITION_FAILED",reason) end
    local token=session.."/"..snapshot.ownerUid.."/"..snapshot.sourceServerId
    if chooser_pending then
        if action~="allianceRelocate" then
            return failed("GAME_PRECONDITION_FAILED",
                "Original F10 chooser request unresolved; cannot dispatch another relocation")
        end
        return observe_original_f10_chooser(snapshot,token,true)
    end
    if pending then
        if pending.token~=token or pending.action~=action then
            return failed("GAME_PRECONDITION_FAILED","An owned relocation is unresolved; never repeat or replace it")
        end
        if pending.observerDetached then
            -- Dispose explicitly detaches our original handler. Preserve the
            -- prior accepted but causally unresolved status without retrying.
            return action_receipt(pending,snapshot,false)
        end
        -- A reply for the same numeric itemId after a native manager or
        -- session swap cannot authenticate the earlier ItemUse. Retain the
        -- tombstone even when the original handler was displaced.
        if member(rawget(_G,"LuaEntry"),"Player")~=pending.player or
           rawget(_G,"DataCenter")~=pending.center or
           member(pending.center,"ItemManager")~=pending.manager or
           member(pending.manager,pending.method)~=pending.hook then
            pending.observerDetached=true
            return failed("GAME_ACTION_UNCONFIRMED",
                "Original relocation reply handler or player session changed after dispatch; never resend")
        end
        local itemdata=member(rawget(_G,"DataCenter"),"ItemData")
        local ok,item=invoke(itemdata,"GetItemById",pending.item.nativeItemId)
        local count=item and integer(member(item,"count"),0) or 0
        if not ok or count==nil then return failed("GAME_ACTION_UNCONFIRMED","Native relocation inventory unavailable after dispatch") end
        if item~=nil and native_item_uuid(member(item,"uuid"))~=pending.item.itemUuid then
            return failed("GAME_ACTION_UNCONFIRMED","Native relocation item stack identity changed after dispatch")
        end
        pending.afterCount=count
        if pending.rejected then
            clear_hook(pending)
            return failed("GAME_ACTION_UNCONFIRMED","Original relocation reply rejected the request; no automatic retry")
        end
        local confirmed=pending.observerDetached~=true and
            pending.reply==true and pending.replyUseOperationCompatible==true and
            snapshot.worldPointId~=pending.before and count==pending.item.count-1
        local result=action_receipt(pending,snapshot,confirmed)
        if confirmed then clear_hook(pending);pending=nil end
        return result
    end
    if fields.queryOnly=="1" then return failed("GAME_PRECONDITION_FAILED","No dispatched relocation to observe") end
    local focus_ok,focus=false,false
    if type(input_clear)=="function" then focus_ok,focus=pcall(input_clear) end
    if not focus_ok or focus~=true then return failed("GAME_PRECONDITION_FAILED","Game input focus is active or unverified") end
    local clear,why=warning_clear(action)
    if not clear then return failed("GAME_PRECONDITION_FAILED",why) end
    local item,item_reason=owned_item(action)
    if not item then return unavailable(item_reason,snapshot) end
    local alliance_uid,chooser_marks
    if action=="allianceRelocate" then
        if not snapshot.inAlliance then return failed("GAME_PRECONDITION_FAILED","Native alliance membership required") end
        local marks,why=alliance_rally_snapshot(member(rawget(_G,"LuaEntry"),"Player"))
        if not marks then
            return failed("GAME_PRECONDITION_FAILED",why or "Original F10 native alliance marks unavailable")
        end
        -- Original TryAllianceMoveCity first checks OtherServerRally. If it
        -- exists, the original F10 route opens UIChooseRallyPoint, leaving the
        -- actual choice to the owner. Directly invoking home-rally here would
        -- silently bypass that native chooser and select another outcome.
        if marks.nativeChooserWouldOpen then
            if not marks.otherServerRally then
                return unavailable("Original F10 chooser mark is not owner-verified",snapshot)
            end
            chooser_marks=marks
        else
            -- Exact source=self=current=2289 owner fence makes this native
            -- original request MoveCrossServer, NOT AllianceMoveCity. It
            -- cannot be paired with the old WorldMvHandle(reason=240)
            -- confirmation; reject rather than spending a cross-server item.
            return unavailable("Original F10 same-home rally route sends MoveCrossServer, not AllianceMoveCity; no authorized direct relocation ACK",snapshot)
        end
        alliance_uid=marks.ownerAllianceUid
    end
    -- Native focus checks and item/rally readers can cross a game-session
    -- transition. Revalidate the exact owner, three servers and before
    -- position at the last no-send boundary, ahead of installing an observer.
    local final_owner,final_reason=world_snapshot()
    if not final_owner or final_owner.ownerUid~=snapshot.ownerUid or
       final_owner.sourceServerId~=snapshot.sourceServerId or
       final_owner.worldPointId~=snapshot.worldPointId then
        return failed("GAME_PRECONDITION_FAILED",
            final_reason or "Native relocation owner or starting point changed before dispatch")
    end
    -- Reconfirm the same bag consumable stack and native rally after the
    -- last focus/scene observations; it must be the original v26 path we
    -- checked, not a new item/mark appearing mid-command.
    local final_item=owned_item(action)
    if not final_item or final_item.nativeItemId~=item.nativeItemId or
       final_item.itemUuid~=item.itemUuid or final_item.count~=item.count then
        return failed("GAME_PRECONDITION_FAILED",
            "Original relocation consumable changed before native dispatch")
    end
    local final_warning,final_warning_reason=warning_clear(action)
    if not final_warning then
        return failed("GAME_PRECONDITION_FAILED",
            final_warning_reason or "Native relocation warning changed before dispatch")
    end
    if action=="allianceRelocate" then
        if not final_owner.inAlliance then
            return failed("GAME_PRECONDITION_FAILED",
                "Native F10 alliance membership changed before chooser")
        end
        local fresh=alliance_rally_snapshot(member(rawget(_G,"LuaEntry"),"Player"))
        local old_remote=chooser_marks and chooser_marks.otherServerRally
        local now_remote=fresh and fresh.otherServerRally
        if not fresh or fresh.ownerAllianceUid~=alliance_uid or
           not fresh.nativeChooserWouldOpen or not old_remote or not now_remote or
           old_remote.serverId~=now_remote.serverId or
           old_remote.markType~=now_remote.markType or
           old_remote.worldPointId~=now_remote.worldPointId then
            return failed("GAME_PRECONDITION_FAILED",
                "Original native alliance chooser mark changed before opening")
        end
        return request_original_f10_chooser(final_owner,fresh,token)
    end
    if action~="randomRelocate" then
        return failed("GAME_PRECONDITION_FAILED",
            "Original relocation sender is unavailable for this action")
    end
    local center=rawget(_G,"DataCenter")
    local manager=member(center,"ItemManager")
    local method="ItemUseHandle"
    local original=member(manager,method)
    if type(original)~="function" then return unavailable("Original relocation reply handler unavailable") end
    local send=member(rawget(_G,"SFSNetwork"),"SendMessage")
    local message=member(rawget(_G,"MsgDefines"),"ItemUse")
    if type(send)~="function" or message==nil then
        return unavailable("Original random relocation ItemUse sender unavailable")
    end
    local tx={token=token,action=action,item=item,before=snapshot.worldPointId,
        center=center,player=member(rawget(_G,"LuaEntry"),"Player"),
        manager=manager,method=method,original=original,reply=false}
    tx.hook=function(self,response,...)
        local returns=table.pack(original(self,response,...))
        local current=world_snapshot()
        if pending==tx and current and self==manager and
           rawget(_G,"DataCenter")==tx.center and
           member(tx.center,"ItemManager")==tx.manager and
           member(rawget(_G,"LuaEntry"),"Player")==tx.player and
           member(manager,method)==tx.hook and
           token==session.."/"..current.ownerUid.."/"..current.sourceServerId then
            if tostring(member(response,"itemId"))==item.itemId and
               native_item_uuid(member(response,"uuid"))==item.itemUuid then
                -- ItemUseHandle can handle itemId-only responses; those
                -- cannot independently acknowledge our exact owned stack.
                -- Original v26 ItemManager.ItemUseHandle lines 49-52 marks
                -- opType=1 as a non-normal-use operation. Never credit that
                -- event (or an unrecognized opType) as a sent ItemUse reply
                -- even if some separate world movement+stock debit occurs.
                local op=member(response,"opType")
                local use_operation=(op==nil or op==0)
                if use_operation then
                    tx.reply=member(response,"errorCode")==nil
                    tx.replyUseOperationCompatible=tx.reply
                    tx.rejected=not tx.reply
                else
                    tx.incompatibleOperationObserved=true
                end
            end
        end
        return table.unpack(returns,1,returns.n)
    end
    local installed=pcall(function()manager[method]=tx.hook end)
    if not installed or member(manager,method)~=tx.hook then return unavailable("Native relocation reply observer could not be attached") end
    pending=tx -- before call, including uncertain exceptions: never retransmit
    local sent,send_result=pcall(send,message,{uuid=item.nativeUuid,num=1,useItemFromType=0})
    if not sent or send_result==false then
        tx.sendOutcomeUncertain=true
        return {state="failed",errorCode="GAME_ACTION_UNCONFIRMED",
            error="Relocation outcome unknown; observe only and never repeat",data=action_receipt(tx,snapshot,false).data}
    end
    tx.sendOutcomeConfirmed=true
    return action_receipt(tx,snapshot,false)
end
function M.RandomRelocate(fields,input_clear)
    return perform(fields,input_clear,"randomRelocate")
end
function M.AllianceRelocate(fields,input_clear)
    return perform(fields,input_clear,"allianceRelocate")
end
return M
