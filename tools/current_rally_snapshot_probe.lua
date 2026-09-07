-- Read-only current-build Rally state adapter.
--
-- The field/method contract is recovered from LWC2AutoJoinRally.lua v49 and
-- checked against the current content-version-12 Lua bytecode. This module has
-- no UI click, march, message-send, or reward-claim path.

local M = {
    VERSION = "lwcontrol-current-rally-snapshot-5",
    SCHEMA_VERSION = 5,
    MODE = "state",
}

local unpack_values = table.unpack or unpack

-- Current content-v12 Global/EnumType.luac, AllianceTeamType (source lines
-- 4715-4730). Value 9 is intentionally absent in the current enum.
local ALLIANCE_TEAM_TYPE_NAMES = {
    [0] = "ATTACK_BOSS",
    [1] = "ATTACK_BUILDING",
    [2] = "ATTACK_CITY",
    [3] = "ATTACK_AL_CITY",
    [4] = "ATTACK_ALLIANCE_THRONE",
    [5] = "ATTACK_DRAGON_BUILDING",
    [6] = "ATTACK_SERVER_THRONE",
    [7] = "ATTACK_AL_CENTER",
    [8] = "ATTACK_CITY_STRONGHOLD",
    [10] = "ATTACK_EPIDEMIC_BUILDING",
    [11] = "ATTACK_EPIDEMIC_CITY",
    [12] = "ATTACK_OUTPOST",
    [13] = "ATTACK_ZWL",
}

-- UIAllianceWarMainTableCtrl.OnJoinClick prototype 0.14, source lines 663-707.
-- These six current AllianceTeamType values receive a Rally target type before
-- the normal JOIN_RALLY UI call.
local JOIN_RALLY_TYPE_BY_WAR_TYPE = {
    ATTACK_BOSS = "RALLY_FOR_BOSS",
    ATTACK_BUILDING = "RALLY_FOR_BUILDING",
    ATTACK_AL_CITY = "RALLY_FOR_ALLIANCE_CITY",
    ATTACK_CITY = "RALLY_FOR_CITY",
    ATTACK_EPIDEMIC_CITY = "RALLY_EPIDEMIC_CITY",
    ATTACK_CITY_STRONGHOLD = "RALLY_CITY_STRONGHOLD",
}

local function fail(message)
    return nil, message
end

local function safe_get(target, key)
    if target == nil then return nil end
    local ok, value = pcall(function() return target[key] end)
    return ok and value or nil
end

local function invoke(target, method, ...)
    local fn = safe_get(target, method)
    if type(fn) ~= "function" then return false, nil, "missing:" .. tostring(method) end
    local packed = table.pack(pcall(fn, target, ...))
    if packed[1] then return true, unpack_values(packed, 2, packed.n) end
    local colon_error = tostring(packed[2])
    packed = table.pack(pcall(fn, ...))
    if packed[1] then return true, unpack_values(packed, 2, packed.n) end
    return false, nil, colon_error .. " | " .. tostring(packed[2])
end

local function text(value)
    if value == nil then return "" end
    local ok, output = pcall(tostring, value)
    return ok and output or ""
end

local function number_or_nil(value)
    local parsed = tonumber(value)
    if parsed == nil then return nil end
    return parsed
end

local function value(target, key)
    return safe_get(target, key)
end

local function each_value(collection, limit, consume)
    if collection == nil then return 0, false end
    local values = safe_get(collection, "Values")
    if values ~= nil and values ~= collection then return each_value(values, limit, consume) end
    local count = 0
    if type(collection) == "table" then
        for key, item in pairs(collection) do
            count = count + 1
            if count > limit then return count - 1, true end
            if consume(item, key) == false then break end
        end
        return count, false
    end
    local ok_enum, enumerator = invoke(collection, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        while count < limit do
            local ok_move, moved = invoke(enumerator, "MoveNext")
            if not ok_move or moved ~= true then return count, false end
            local current = safe_get(enumerator, "Current")
            local key = safe_get(current, "Key")
            local item = safe_get(current, "Value")
            if item == nil then item = current end
            count = count + 1
            if consume(item, key) == false then return count, false end
        end
        local ok_move, moved = invoke(enumerator, "MoveNext")
        return count, ok_move and moved == true
    end
    local length = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if length == nil then return 0, false end
    local truncated = length > limit
    for index = 0, math.min(length - 1, limit - 1) do
        local item = safe_get(collection, index)
        if item == nil then item = safe_get(collection, index + 1) end
        if item ~= nil then
            count = count + 1
            if consume(item, index) == false then break end
        end
    end
    return count, truncated
end

local function each_entry(collection, limit, consume)
    if collection == nil then return 0, false end
    local count = 0
    if type(collection) == "table" then
        for key, item in pairs(collection) do
            count = count + 1
            if count > limit then return count - 1, true end
            if consume(item, key) == false then break end
        end
        return count, false
    end
    local ok_enum, enumerator = invoke(collection, "GetEnumerator")
    if ok_enum and enumerator ~= nil then
        while count < limit do
            local ok_move, moved = invoke(enumerator, "MoveNext")
            if not ok_move or moved ~= true then return count, false end
            local current = safe_get(enumerator, "Current")
            local key = safe_get(current, "Key")
            local item = safe_get(current, "Value")
            if item == nil then item = current end
            count = count + 1
            if consume(item, key) == false then return count, false end
        end
        local ok_move, moved = invoke(enumerator, "MoveNext")
        return count, ok_move and moved == true
    end
    local length = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if length ~= nil then
        local truncated = length > limit
        for index = 0, math.min(length - 1, limit - 1) do
            local item = safe_get(collection, index)
            if item == nil then item = safe_get(collection, index + 1) end
            if item ~= nil then
                count = count + 1
                if consume(item, index) == false then break end
            end
        end
        if count > 0 then return count, truncated end
    end
    local values = safe_get(collection, "Values")
    if values ~= nil and values ~= collection then return each_entry(values, limit, consume) end
    return count, false
end

local function member_names(members)
    local names, seen = {}, {}
    local _, truncated = each_value(members, 64, function(member)
        local info = value(member, "memberInfo") or value(member, "playerInfo")
        local name = text(value(member, "ownerName") or value(member, "name")
            or value(member, "playerName") or value(info, "name")
            or value(info, "playerName") or value(info, "ownerName"))
        if name ~= "" and name ~= "0" and name ~= "nil" and not seen[name] then
            seen[name] = true
            names[#names + 1] = name
        end
        return true
    end)
    if truncated then return nil, "memberList exceeds read-only bound" end
    table.sort(names)
    return names
end

local function collection_count(collection)
    local count = tonumber(safe_get(collection, "Count") or safe_get(collection, "Length"))
    if count ~= nil then return count, "member_list_count" end
    local measured, truncated = each_value(collection, 64, function() return true end)
    if truncated then return nil, "memberList exceeds read-only bound" end
    return measured, "member_list_enumerated"
end

local function war_uuid_candidate(source)
    if source == nil then return nil end
    local source_type = type(source)
    if source_type == "table" or source_type == "userdata" then
        return value(source, "uuid") or value(source, "uuId") or value(source, "warUuid")
            or value(source, "warUUID") or value(source, "allianceWarUuid")
            or value(source, "allianceWarId") or value(source, "id")
    end
    if source_type == "string" or source_type == "number" then return source end
    return nil
end

local function server_time_ms()
    local manager_type = rawget(_G, "UITimeManager")
    if manager_type == nil then return nil, "UITimeManager is unavailable" end
    local ok_instance, instance = invoke(manager_type, "GetInstance")
    if not ok_instance or instance == nil then return nil, "UITimeManager.GetInstance failed" end
    local ok_time, current = invoke(instance, "GetServerTime")
    current = ok_time and tonumber(current) or nil
    if current == nil or current < 0 then return nil, "UITimeManager.GetServerTime failed" end
    return current, nil
end

local function build_war_row(manager, raw_uuid)
    local ok_data, data = invoke(manager, "GetAllianceWarDataByUuid", raw_uuid)
    if not ok_data or data == nil then return fail("GetAllianceWarDataByUuid failed for observed rally identity") end
    local uuid = text(value(data, "uuid") or raw_uuid)
    if uuid == "" or uuid == "0" or uuid == "nil" then return fail("observed rally has no stable uuid") end

    local ok_join, can_join, is_leader, in_team, state = invoke(manager, "CheckJoinAllianceWar", raw_uuid)
    if not ok_join then return fail("CheckJoinAllianceWar failed for rally " .. uuid) end

    local current_time, time_error = server_time_ms()
    if current_time == nil then return fail(time_error) end
    local ok_duration, remaining_seconds = invoke(manager, "GetAllianceWarDurationSec", data, current_time)
    remaining_seconds = ok_duration and number_or_nil(remaining_seconds) or nil
    if remaining_seconds == nil then
        return fail("GetAllianceWarDurationSec failed for rally " .. uuid)
    end

    local leader = value(data, "leaderMarch")
    local raw_war_type = number_or_nil(value(data, "type"))
    if raw_war_type == nil or raw_war_type ~= math.floor(raw_war_type) then
        return fail("observed rally has a non-integral AllianceTeamType")
    end
    local war_type = ALLIANCE_TEAM_TYPE_NAMES[raw_war_type]
    if war_type == nil then
        return fail("observed rally has an unmapped current AllianceTeamType " .. text(raw_war_type))
    end
    local join_rally_type = JOIN_RALLY_TYPE_BY_WAR_TYPE[war_type] or ""
    local leader_start_id = number_or_nil(value(leader, "startId"))
    local server_id = number_or_nil(value(data, "server"))
    local world_id = number_or_nil(value(data, "worldId"))
    local join_monster_special_type = nil
    local join_monster_special_type_source = "UIAllianceWarMainTableCtrl.GetWarItemData: non-boss branch unset"
    local resolved_target_name = text(value(data, "targetName"))
    local resolved_target_level = number_or_nil(value(data, "targetLevel"))
    local resolved_target_metadata_source = "AllianceWarInfo.ParseData: message.targetName/message.targetLevel"
    local resolved_target_display_name = resolved_target_name
    local resolved_target_display_name_source = "AllianceWarInfo.ParseData: message.targetName"
    if war_type == "ATTACK_BOSS" then
        local data_center = rawget(_G, "DataCenter")
        local monster_manager = safe_get(data_center, "MonsterTemplateManager")
        local ok_monster, monster = invoke(monster_manager, "GetMonsterTemplate", value(data, "targetUid"))
        if not ok_monster or monster == nil then
            return fail("MonsterTemplateManager.GetMonsterTemplate failed for boss rally " .. uuid)
        end
        join_monster_special_type = number_or_nil(value(monster, "special"))
        join_monster_special_type_source = "UIAllianceWarMainTableCtrl.GetWarItemData: MonsterTemplateManager.GetMonsterTemplate(targetUid).special"
        resolved_target_name = text(value(monster, "name"))
        resolved_target_level = number_or_nil(value(monster, "level"))
        resolved_target_metadata_source = "UIAllianceWarMainTableCtrl.GetWarItemData: MonsterTemplateManager.GetMonsterTemplate(targetUid).name/level"
        resolved_target_display_name_source = "UIAllianceWarMainTableCtrl.GetWarItemData: CS.GameEntry.Localization.GetString(monster.name)"
        if resolved_target_name == "" or resolved_target_name == "0" or resolved_target_name == "nil"
            or resolved_target_level == nil or resolved_target_level < 0 then
            return fail("MonsterTemplateManager.GetMonsterTemplate returned incomplete boss display metadata for rally " .. uuid)
        end
        local cs = rawget(_G, "CS")
        local localization = safe_get(safe_get(cs, "GameEntry"), "Localization")
        local ok_localized, localized = invoke(localization, "GetString", value(monster, "name"))
        if ok_localized then resolved_target_display_name = text(localized) else resolved_target_display_name = "" end
    end
    local members = value(data, "memberList")
    local names, names_error = member_names(members)
    if names == nil then return nil, names_error end
    local listed_member_count, count_error = collection_count(members)
    if listed_member_count == nil then return fail(count_error) end
    local member_count = listed_member_count + 1
    local member_source = "AllianceWarDataManager.CheckJoinAllianceWarByWarData: table.count(memberList)+1"

    return {
        uuid = uuid,
        rawWarType = raw_war_type,
        warType = war_type,
        warTypeSource = "Global.EnumType.AllianceTeamType",
        server = server_id,
        serverSource = "AllianceWarInfo.ParseData: message.server",
        worldId = world_id,
        worldIdSource = "AllianceWarInfo.ParseData: message.worldId",
        worldType = text(value(data, "worldType")),
        attackPointId = number_or_nil(value(data, "attackPointId")),
        attackUid = text(value(data, "attackUid")),
        attackName = text(value(data, "attackName")),
        targetPointId = number_or_nil(value(data, "targetPointId")),
        targetUuid = text(value(data, "targetUuid")),
        targetUid = text(value(data, "targetUid")),
        targetName = text(value(data, "targetName")),
        targetContentId = text(value(data, "targetContentId")),
        targetBaseSkinId = number_or_nil(value(data, "targetBaseSkinId")),
        targetBaseSkinIdSource = "AllianceWarInfo.ParseData: message.targetBaseSkinId",
        targetLevel = number_or_nil(value(data, "targetLevel")),
        targetLevelSource = "AllianceWarInfo.ParseData: message.targetLevel",
        resolvedTargetName = resolved_target_name,
        resolvedTargetLevel = resolved_target_level,
        resolvedTargetMetadataSource = resolved_target_metadata_source,
        resolvedTargetDisplayName = resolved_target_display_name,
        resolvedTargetDisplayNameSource = resolved_target_display_name_source,
        joinRallyType = join_rally_type,
        joinRallyTypeSource = "UIAllianceWarMainTableCtrl.OnJoinClick",
        joinTargetUuid = uuid,
        joinTargetPointId = leader_start_id,
        joinTargetServerId = server_id,
        joinTargetWorldId = world_id,
        joinMonsterSpecialType = join_monster_special_type,
        joinMonsterSpecialTypeSource = join_monster_special_type_source,
        joinTargetSource = "UIAllianceWarMainTableCtrl.OnJoinClick: leaderMarch.startId + rally uuid + data.server + data.worldId",
        createTime = number_or_nil(value(data, "createTime")),
        waitTime = number_or_nil(value(data, "waitTime")),
        marchTime = number_or_nil(value(data, "marchTime")),
        remainingSeconds = remaining_seconds,
        remainingSecondsSource = "AllianceWarDataManager.GetAllianceWarDurationSec",
        serverTimeMs = current_time,
        currentSoldiers = number_or_nil(value(data, "currentSoldiers")),
        maxSoldiers = number_or_nil(value(data, "maxSoldiers")),
        assemblyMarchMax = number_or_nil(value(data, "assemblyMarchMax")),
        bossHp = number_or_nil(value(data, "bossHp")),
        updateTime = number_or_nil(value(data, "updateTime")),
        memberCount = member_count,
        memberCountSource = member_source,
        memberNames = names,
        canJoin = can_join == true,
        isLeader = is_leader == true,
        inTeam = in_team == true,
        joinState = text(state),
        leader = {
            uuid = text(value(leader, "uuid")),
            ownerUid = text(value(leader, "ownerUid") or value(leader, "uid")),
            ownerName = text(value(leader, "ownerName") or value(leader, "name")),
            status = text(value(leader, "status")),
            startId = leader_start_id,
            startTime = number_or_nil(value(leader, "startTime")),
            endTime = number_or_nil(value(leader, "endTime")),
            teamUuid = text(value(leader, "teamUuid")),
            power = number_or_nil(value(leader, "power")),
            curHp = number_or_nil(value(leader, "curHp")),
            maxHp = number_or_nil(value(leader, "maxHp")),
        },
    }
end

local function build_wars(manager)
    if manager == nil or type(safe_get(manager, "GetAllianceWarIdList")) ~= "function"
        or type(safe_get(manager, "GetAllianceWarDataByUuid")) ~= "function"
        or type(safe_get(manager, "CheckJoinAllianceWar")) ~= "function"
        or type(safe_get(manager, "GetAllianceWarDurationSec")) ~= "function" then
        return fail("AllianceWarDataManager does not expose the recovered current contract")
    end
    local ok_ids, ids = invoke(manager, "GetAllianceWarIdList")
    if not ok_ids or ids == nil then return fail("GetAllianceWarIdList is unavailable") end
    local rows, seen = {}, {}
    local error_value = nil
    local _, truncated = each_entry(ids, 256, function(item, key)
        local raw_uuid = war_uuid_candidate(item) or war_uuid_candidate(key)
        local identity = text(raw_uuid)
        if raw_uuid == nil or identity == "" or identity == "0" or identity == "nil" then
            error_value = "GetAllianceWarIdList returned an entry without a stable identity"
            return false
        end
        if seen[identity] then
            error_value = "GetAllianceWarIdList returned a duplicate rally identity"
            return false
        end
        local row, row_error = build_war_row(manager, raw_uuid)
        if row == nil then error_value = row_error; return false end
        seen[identity] = true
        rows[#rows + 1] = row
        return true
    end)
    if error_value ~= nil then return nil, error_value end
    if truncated then return fail("GetAllianceWarIdList exceeds read-only bound") end
    table.sort(rows, function(left, right) return left.uuid < right.uuid end)
    return rows
end

local function build_formations(manager, world_manager)
    if manager == nil or type(safe_get(manager, "GetCurFormationList")) ~= "function"
        or type(safe_get(manager, "GetCurStaminaByUuid")) ~= "function" then
        return fail("ArmyFormationDataManager does not expose the recovered current contract")
    end
    local ok_list, list = invoke(manager, "GetCurFormationList")
    if not ok_list or list == nil then return fail("GetCurFormationList is unavailable") end
    local rows, seen, error_value = {}, {}, nil
    local _, truncated = each_value(list, 32, function(formation)
        local raw_uuid = value(formation, "uuid") or value(formation, "uuId")
        local uuid = text(raw_uuid)
        if raw_uuid == nil or uuid == "" or uuid == "0" or uuid == "nil" then
            error_value = "formation list contains an entry without a stable uuid"
            return false
        end
        if seen[uuid] then error_value = "formation list contains a duplicate uuid"; return false end
        local ok_free, is_free = invoke(formation, "IsFree")
        if not ok_free then error_value = "formation IsFree failed for " .. uuid; return false end
        local ok_stamina, stamina = invoke(manager, "GetCurStaminaByUuid", raw_uuid)
        if not ok_stamina or tonumber(stamina) == nil then
            error_value = "GetCurStaminaByUuid failed for " .. uuid
            return false
        end
        local current_rally_id = ""
        local owner_march_checked = false
        if world_manager ~= nil and type(safe_get(world_manager, "GetOwnerFormationMarch")) == "function" then
            local ok_owner, owner = invoke(world_manager, "GetOwnerFormationMarch", raw_uuid)
            if ok_owner then
                owner_march_checked = true
                if owner ~= nil then
                    current_rally_id = text(value(owner, "teamUuid") or value(owner, "assemblyUuid")
                        or value(owner, "rallyUuid") or value(owner, "targetUuid"))
                end
            end
        end
        seen[uuid] = true
        rows[#rows + 1] = {
            uuid = uuid,
            index = number_or_nil(value(formation, "index") or value(formation, "formationIndex")),
            state = text(value(formation, "state") or value(formation, "formationState")),
            isFree = is_free == true,
            stamina = tonumber(stamina),
            power = number_or_nil(value(formation, "power") or value(formation, "formationPower")),
            totalSoldierNum = number_or_nil(value(formation, "totalSoldierNum")),
            currentRallyId = current_rally_id,
            ownerMarchChecked = owner_march_checked,
        }
        return true
    end)
    if error_value ~= nil then return nil, error_value end
    if truncated then return fail("GetCurFormationList exceeds read-only bound") end
    if #rows == 0 then return fail("GetCurFormationList is empty; refusing a pre-login/incomplete snapshot") end
    table.sort(rows, function(left, right)
        local left_index = left.index or 999
        local right_index = right.index or 999
        if left_index == right_index then return left.uuid < right.uuid end
        return left_index < right_index
    end)
    return rows
end

function M.Build(war_manager, formation_manager, world_manager, player, capture_id, captured_at, world_source)
    if type(capture_id) ~= "string" or capture_id == "" or type(captured_at) ~= "string" or captured_at == "" then
        return fail("capture metadata is incomplete")
    end
    if player == nil or type(safe_get(player, "GetUid")) ~= "function" then
        return fail("LuaEntry.Player is unavailable")
    end
    local ok_uid, uid = invoke(player, "GetUid")
    uid = ok_uid and text(uid) or ""
    if uid == "" or uid == "0" or uid == "nil" then return fail("player identity is not loaded") end

    local wars, war_error = build_wars(war_manager)
    if wars == nil then return nil, war_error end
    local formations, formation_error = build_formations(formation_manager, world_manager)
    if formations == nil then return nil, formation_error end

    local ok_alliance, in_alliance = invoke(player, "IsInAlliance")
    local ok_alliance_uid, alliance_uid = invoke(player, "GetAllianceUid")
    local ok_stamina, player_stamina = invoke(player, "GetCurStamina")

    local joinable_count, joined_count, free_count = 0, 0, 0
    for _, row in ipairs(wars) do
        if row.canJoin and not row.isLeader and not row.inTeam then joinable_count = joinable_count + 1 end
        if row.inTeam then joined_count = joined_count + 1 end
    end
    for _, row in ipairs(formations) do if row.isFree then free_count = free_count + 1 end end

    return {
        schemaVersion = M.SCHEMA_VERSION,
        mode = M.MODE,
        readOnly = true,
        captureId = capture_id,
        capturedAt = captured_at,
        candidateSource = "DataCenter.AllianceWarDataManager.GetAllianceWarIdList",
        formationSource = "DataCenter.ArmyFormationDataManager.GetCurFormationList",
        worldMarchSource = world_source or "",
        player = {
            uid = uid,
            inAlliance = ok_alliance and in_alliance == true or nil,
            allianceUid = ok_alliance_uid and text(alliance_uid) or "",
            stamina = ok_stamina and number_or_nil(player_stamina) or nil,
        },
        observedRallyCount = #wars,
        joinableRallyCount = joinable_count,
        joinedRallyCount = joined_count,
        formationCount = #formations,
        freeFormationCount = free_count,
        rallies = wars,
        formations = formations,
    }
end

return M
