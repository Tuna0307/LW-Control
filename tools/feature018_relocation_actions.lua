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

local M = {VERSION = "feature018-relocation-actions-3"}
local baseline = {}
local last_owner = nil
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
        and "No source-verified F9 random destination, owned item mapping or warning/confirmation producer"
        or "No source-verified F10 chosen alliance rally target and validated UI confirmation", snapshot)
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
-- Reply observation wraps only existing original handlers during the outstanding
-- transaction, calls them unchanged, and restores them before reporting success.
local pending=nil
local function clear_hook(tx)
    if tx.hook and member(tx.manager,tx.method)==tx.hook then
        tx.manager[tx.method]=tx.original
    end
end
function M.Dispose()
    if pending then clear_hook(pending) end
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
    local item_uid=item and uid(member(item,"uuid"))
    if not ok or not item_uid or not count or count<1 then return nil,"No owned native relocation item: "..name end
    if tostring(member(item,"itemId"))~=tostring(item_id) then return nil,"Owned relocation item identity mismatch" end
    local expired=static_bool(rawget(_G,"UIUtil"),"CheckItemIsExpiredByUuid",member(item,"uuid"))
    if expired~=false then return nil,"Native relocation item expired or expiry is unknown" end
    return {itemId=tostring(item_id),itemUuid=item_uid,nativeUuid=member(item,"uuid"),count=count,nativeItemId=item_id}
end
local function action_receipt(tx,snapshot,confirmed)
    return {state=confirmed and "confirmed" or "accepted",data={
        observation=confirmed and "native_relocation_reply_item_and_world_position" or "native_relocation_pending",
        action=tx.action,slot=0,ownerUid=snapshot.ownerUid,sourceServerId=snapshot.sourceServerId,
        itemId=tx.item.itemId,itemUuid=tx.item.itemUuid,beforeCount=tx.item.count,
        afterCount=tx.afterCount,beforeWorldPointId=tx.before,afterWorldPointId=snapshot.worldPointId,
        worldPointChanged=snapshot.worldPointId~=tx.before,requestDispatched=not confirmed,
        awaitingGameConfirmation=not confirmed,relocationConfirmed=confirmed,
        replyObserved=tx.reply==true,worldMvAcknowledged=confirmed and tx.action=="allianceRelocate",
        warningsClear=true,readOnly=false}}
end
local function perform(fields,input_clear,action)
    if type(fields)~="table" then return failed("GAME_PRECONDITION_FAILED","Invalid relocation fields") end
    if fields.preflightOnly=="1" then return preflight(fields,input_clear,action) end
    local session=key_identity(fields,action)
    if not session then return failed("GAME_PRECONDITION_FAILED","Invalid owned relocation session") end
    if fields.queryOnly~=nil and fields.queryOnly~="1" then return failed("GAME_PRECONDITION_FAILED","Invalid relocation query mode") end
    local snapshot,reason=world_snapshot()
    if not snapshot then return failed("GAME_PRECONDITION_FAILED",reason) end
    local token=session.."/"..snapshot.ownerUid.."/"..snapshot.sourceServerId
    if pending then
        if pending.token~=token or pending.action~=action then
            return failed("GAME_PRECONDITION_FAILED","An owned relocation is unresolved; never repeat or replace it")
        end
        if action=="allianceRelocate" then
            local current_rally=alliance_rally_snapshot(member(rawget(_G,"LuaEntry"),"Player"))
            if not current_rally or current_rally.ownerAllianceUid~=pending.allianceUid or
               not current_rally.homeRally or current_rally.homeRally.worldPointId~=pending.rallyPoint then
                return failed("GAME_ACTION_UNCONFIRMED","Native alliance owner/rally changed during relocation")
            end
        end
        local itemdata=member(rawget(_G,"DataCenter"),"ItemData")
        local ok,item=invoke(itemdata,"GetItemById",pending.item.nativeItemId)
        local count=item and integer(member(item,"count"),0) or 0
        if not ok or count==nil then return failed("GAME_ACTION_UNCONFIRMED","Native relocation inventory unavailable after dispatch") end
        pending.afterCount=count
        if pending.rejected then
            clear_hook(pending)
            return failed("GAME_ACTION_UNCONFIRMED","Original relocation reply rejected the request; no automatic retry")
        end
        local confirmed=pending.reply==true and snapshot.worldPointId~=pending.before and count==pending.item.count-1
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
    local rally,alliance_uid
    if action=="allianceRelocate" then
        if not snapshot.inAlliance then return failed("GAME_PRECONDITION_FAILED","Native alliance membership required") end
        local marks,why=alliance_rally_snapshot(member(rawget(_G,"LuaEntry"),"Player"))
        if not marks or not marks.homeRally or not marks.homeRally.authorizedHomeServer then
            return failed("GAME_PRECONDITION_FAILED",why or "Exact same-alliance home rally missing")
        end
        rally=marks.homeRally;alliance_uid=marks.ownerAllianceUid
        if type(member(rawget(_G,"MoveCityUtil"),"AllianceMoveCityToSelectedRallyPoint"))~="function" then
            return unavailable("Original chosen home-rally relocation method unavailable")
        end
    end
    local center=rawget(_G,"DataCenter")
    local manager=member(center,action=="randomRelocate" and "ItemManager" or "BuildManager")
    local method=action=="randomRelocate" and "ItemUseHandle" or "WorldMvHandle"
    local original=member(manager,method)
    if type(original)~="function" then return unavailable("Original relocation reply handler unavailable") end
    local send=member(rawget(_G,"SFSNetwork"),"SendMessage")
    local message=member(rawget(_G,"MsgDefines"),"ItemUse")
    if action=="randomRelocate" and (type(send)~="function" or message==nil) then return unavailable("Original random relocation ItemUse sender unavailable") end
    local tx={token=token,action=action,item=item,before=snapshot.worldPointId,
        manager=manager,method=method,original=original,reply=false,
        allianceUid=alliance_uid,rallyPoint=rally and rally.worldPointId or nil}
    tx.hook=function(self,response,...)
        local returns=table.pack(original(self,response,...))
        local current=world_snapshot()
        if pending==tx and current and token==session.."/"..current.ownerUid.."/"..current.sourceServerId then
            if action=="randomRelocate" then
                if tostring(member(response,"itemId"))==item.itemId and
                   (member(response,"uuid")==nil or tostring(member(response,"uuid"))==item.itemUuid) then
                    tx.reply=member(response,"errorCode")==nil
                    tx.rejected=not tx.reply
                end
            elseif integer(member(response,"serverId"),1)==HOME_SERVER and
                (member(response,"worldMainPoint")~=nil or member(member(response,"buildInfo"),"pId")~=nil) then
                tx.reply=member(response,"errorCode")==nil
                tx.rejected=not tx.reply
            end
        end
        return table.unpack(returns,1,returns.n)
    end
    local installed=pcall(function()manager[method]=tx.hook end)
    if not installed or member(manager,method)~=tx.hook then return unavailable("Native relocation reply observer could not be attached") end
    pending=tx -- before call, including uncertain exceptions: never retransmit
    local sent
    if action=="randomRelocate" then
        sent=pcall(send,message,{uuid=item.nativeUuid,num=1,useItemFromType=0})
    else
        sent=pcall(member(rawget(_G,"MoveCityUtil"),"AllianceMoveCityToSelectedRallyPoint"),rally.markType,4,false,nil)
    end
    if not sent then return {state="failed",errorCode="GAME_ACTION_UNCONFIRMED",
        error="Relocation outcome unknown; observe only and never repeat",data=action_receipt(tx,snapshot,false).data} end
    return action_receipt(tx,snapshot,false)
end
function M.RandomRelocate(fields,input_clear)
    return perform(fields,input_clear,"randomRelocate")
end
function M.AllianceRelocate(fields,input_clear)
    return perform(fields,input_clear,"allianceRelocate")
end
return M
