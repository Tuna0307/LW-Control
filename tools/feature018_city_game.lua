-- Source-backed FEATURE-RECOVERY-018 City Layout game producer (v26).
--
-- Read sources, packed original Lua (3/26):
--   DataCenter/BuildManager/BuildManager.luac
--     GetBuildingDataByUuid, GetAllBuildUuid, FreeBuildingReplaceNewHandle
--   DataCenter/BuildManager/BuildingDate.luac: GetRangedPoints
--   Util/BuildingUtils.luac: CanMoveBuild (2031-2067),
--     IsInFlagPosition (3423-3433), IsCanPutDownByPoint (509-520)
--   DataCenter/CityZoneManager/CityZoneManager.luac: CanPutBuildByPoint
--   DataCenter/LandLock/LandLockManager.luac: CanBuildByPointId
--   Util/SceneUtils.luac: GetIsInCity, IndexToTilePos, TileXYToIndex
--   UI/UIPlaceBuild/View/UIPlaceBuildView.luac: OnConfirmBtnClick, 425-435
--   Net/Msgs/FreeBuildingReplaceNewMessage.luac: OnCreate, HandleMessage
--
-- A move is dispatched via precisely the same SFSNetwork/MsgDefines request as
-- the official placement UI. Only a later authoritative BuildManager update
-- counts as completion. A dispatch response is always state='accepted'.

local M = { VERSION = 'feature018-city-v26-1', active = nil }
local CITY_REGION = 'main-city'
local MAX_BUILDINGS = 512
local MAX_PLACEMENTS = 64
local MAX_STEPS = 192
local MOVE_RESPONSE_TIMEOUT_SECONDS = 15

local function unsupported(code, message)
    return { state = 'unsupported', errorCode = code, error = message }
end

local function failed(code, message)
    return { state = 'failed', errorCode = code, error = message }
end

local function confirmed(value)
    return { state = 'confirmed', data = value }
end

local function sorted(list)
    table.sort(list)
    return list
end

local function point_xy(point_id, width)
    local idx = point_id - 1
    return idx % width, math.floor(idx / width)
end

local function at_xy(x, y, width)
    if x < 0 or y < 0 or x >= width or y >= width then return nil end
    return y * width + x + 1
end

local function digest(text)
    -- Lua 5.3 exact-integer-safe rolling checksum of observed game state.
    local value = 131071
    for i = 1, #text do value = (value * 131 + text:byte(i)) % 2147483647 end
    return 'v26-city-' .. tostring(value)
end

local function context()
    local data = rawget(_G, 'DataCenter')
    local cs = rawget(_G, 'CS')
    local scene_utils = rawget(_G, 'SceneUtils')
    local bm = data and data.BuildManager
    local zone = data and data.CityZoneManager
    local land = data and data.LandLockManager
    local templates = data and data.BuildTemplateManager
    local building_utils = rawget(_G, 'BuildingUtils')
    local building_types = rawget(_G, 'BuildingTypes')
    local point_types = rawget(_G, 'CityPointType')
    local world = cs and cs.SceneManager and cs.SceneManager.World
    if not (bm and zone and land and templates and scene_utils and world and
        building_utils and building_types and point_types) then
        return nil, 'Current city managers are not loaded'
    end
    if type(bm.allBuilding) ~= 'table' or type(zone.allZonePoint) ~= 'table' or
        type(land.CanBuildByPointId) ~= 'function' or
        type(zone.CanPutBuildByPoint) ~= 'function' or
        type(templates.GetBuildingDesTemplate) ~= 'function' or
        type(scene_utils.GetIsInCity) ~= 'function' or
        type(building_utils.CanMoveBuild) ~= 'function' or
        type(building_utils.IsInFlagPosition) ~= 'function' or
        building_types.LW_BUILD_FLAG == nil or
        type(world.GetPointType) ~= 'function' or point_types.Road == nil then
        return nil, 'Current city snapshot source is not ready'
    end
    local width = tonumber(rawget(_G, 'CityTileCount'))
    if not width or width ~= math.floor(width) or width < 1 or width > 128 then
        return nil, 'Current city grid dimensions are unavailable'
    end
    return {
        bm = bm, zone = zone, land = land, templates = templates,
        world = world, scene = scene_utils, width = width,
        buildingUtils = building_utils, flagItemId = building_types.LW_BUILD_FLAG,
        roadType = point_types.Road,
        isInCity = scene_utils.GetIsInCity() == true,
    }
end

local function snapshot()
    local ctx, reason = context()
    if not ctx then return nil, reason end
    local width = ctx.width
    local cells = {}
    local by_id = {}
    local zone_points = ctx.zone.allZonePoint
    local zone_signature = {}
    for pid = 1, width * width do
        local x, y = point_xy(pid, width)
        local template = zone_points[pid]
        -- LandLockManager.CanBuildByPointId (362-369) represents the separate
        -- land restriction. A missing CityZoneTemplate must be rejected by
        -- CanPutBuildByPoint (70-75) as a ZONE issue, not mislabeled LOCKED.
        local unlocked = ctx.land:CanBuildByPointId(pid) == true
        zone_signature[#zone_signature + 1] = template == nil and 'missing'
            or tostring(template.zone_type)
        -- IsCanPutDownByPoint (513-520) uses GetPointType == CityPointType.Road.
        -- IsInFlagPosition's four special cells are exclusively for LW_BUILD_FLAG.
        local road = ctx.world:GetPointType(pid) == ctx.roadType
        local flag_only = ctx.buildingUtils.IsInFlagPosition(pid) == true
        local cell = {
            pointId = pid, x = x, y = y, regionId = CITY_REGION,
            unlocked = unlocked, road = road, flagOnly = flag_only,
        }
        cells[#cells + 1] = cell
        by_id[pid] = cell
    end

    local buildings = {}
    local by_uuid = {}
    for key, building in pairs(ctx.bm.allBuilding) do
        if #buildings >= MAX_BUILDINGS then return nil, 'City building count exceeds snapshot limit' end
        if building and building.isWorldBuild ~= true and
            (rawget(_G, 'BuildingStateType') == nil or
             building.state ~= _G.BuildingStateType.FoldUp) then
            local uuid = tostring(building.uuid or key)
            local point_id = tonumber(building.pointId)
            if uuid:match('^%d+$') == nil or
                point_id == nil or point_id < 1 or point_id > width * width or
                point_id ~= math.floor(point_id) or by_uuid[uuid] then
                return nil, 'City building identities or positions are invalid'
            end
            local template = ctx.templates:GetBuildingDesTemplate(building.itemId)
            if not template or type(building.GetRangedPoints) ~= 'function' then
                return nil, 'City building footprint/eligibility is unavailable'
            end
            local footprints = building:GetRangedPoints()
            if type(footprints) ~= 'table' then
                return nil, 'City building occupied points were unavailable'
            end
            local occupied = {}
            for pid, is_occupied in pairs(footprints) do
                if is_occupied then
                    local p = tonumber(pid)
                    if not p or not by_id[p] then
                        return nil, 'City building footprint exceeds the authoritative grid'
                    end
                    occupied[#occupied + 1] = p
                end
            end
            if #occupied == 0 then return nil, 'City building has no occupied points' end
            sorted(occupied)
            local x, y = point_xy(point_id, width)
            -- Source CanMoveBuild rejects FUN_BUILD_MAIN, requires a built
            -- LW_BUILD_GATE (level >= 1), then uses template:CanMove().
            -- Call the game's complete rule so these conditions stay together.
            local eligible, move_result = pcall(
                ctx.buildingUtils.CanMoveBuild, building.itemId)
            if not eligible or type(move_result) ~= 'boolean' then
                return nil, 'Current game building movement rule is unavailable'
            end
            local movable = move_result
            local is_flag = building.itemId == ctx.flagItemId
            local entry = {
                uuid = uuid, pointId = point_id, x = x, y = y,
                occupiedPoints = occupied, regionId = CITY_REGION,
                movable = movable, isFlag = is_flag,
                tileX = tonumber(template.tileX) or 1,
                tileY = tonumber(template.tileY) or 1,
                level = tonumber(building.level) or 0,
                itemId = tonumber(building.itemId) or 0,
            }
            by_uuid[uuid] = { public = entry, nativeUuid = building.uuid,
                native = building, template = template }
            buildings[#buildings + 1] = entry
        end
    end
    if #buildings == 0 then return nil, 'No authoritative city building snapshot is loaded' end
    table.sort(buildings, function(a, b) return a.uuid < b.uuid end)
    local signature = {}
    for _, building in ipairs(buildings) do
        signature[#signature + 1] = building.uuid .. ':' .. building.pointId .. ':' ..
            table.concat(building.occupiedPoints, ',') .. ':' ..
            tostring(building.itemId) .. ':' .. tostring(building.tileX) .. ':' ..
            tostring(building.tileY) .. ':' .. (building.movable and '1' or '0') .. ':' ..
            (building.isFlag and '1' or '0')
    end
    -- Preserve a revision on *placement permissions*, not only building
    -- coordinates: newly allowed/restricted zones and the military gate's
    -- CanMoveBuild result can change without moving a single building.
    for _, cell in ipairs(cells) do
        signature[#signature + 1] = (cell.unlocked and '1' or '0') ..
            (cell.road and '1' or '0') .. (cell.flagOnly and '1' or '0') ..
            ':' .. zone_signature[cell.pointId]
    end
    local point_ids = {}
    for i = 1, width * width do point_ids[i] = i end
    return {
        layoutRevision = digest(table.concat(signature, '|')),
        isInCity = ctx.isInCity,
        cells = cells, buildings = buildings,
        regions = {{
            id = CITY_REGION, pointIds = point_ids,
            bounds = { minX = 0, minY = 0, maxX = width - 1, maxY = width - 1 },
        }},
    }, { ctx = ctx, by_uuid = by_uuid, by_id = by_id }
end

local function parse_placements(encoded)
    if type(encoded) ~= 'string' or #encoded > 16000 then return nil end
    if encoded == '' then return {} end
    if encoded:sub(-1) == ';' or encoded:find(';;', 1, true) then return nil end
    local out, seen = {}, {}
    for part in encoded:gmatch('[^;]+') do
        local uuid, position = part:match('^(%d+):(%d+)$')
        local point_id = position and tonumber(position)
        if not uuid or seen[uuid] or not point_id or
            point_id < 1 or point_id ~= math.floor(point_id) or
            #out >= MAX_PLACEMENTS then return nil end
        seen[uuid] = true
        out[#out + 1] = { uuid = uuid, targetPointId = point_id }
    end
    table.sort(out, function(a, b) return a.uuid < b.uuid end)
    return out
end

local function translate_footprint(src, point_id, map, width)
    local origin = src.public
    local x, y = point_xy(point_id, width)
    if map[point_id] == nil then return nil end
    local result = {}
    for _, old_pid in ipairs(origin.occupiedPoints) do
        local ox, oy = point_xy(old_pid, width)
        local next_pid = at_xy(x + ox - origin.x, y + oy - origin.y, width)
        if next_pid == nil or map[next_pid] == nil then return nil end
        result[#result + 1] = next_pid
    end
    return sorted(result)
end

local function validate_state(values)
    local state, meta_or_err = snapshot()
    if not state then return nil, meta_or_err end
    local meta = meta_or_err
    local placements = parse_placements(values.placements or '')
    if not placements then return nil, 'Invalid city placements argument' end
    local issues, jobs, final_occupied = {}, {}, {}
    local function issue(uuid, code, point_id)
        local problem = { uuid = uuid, code = code }
        if point_id ~= nil then problem.pointId = point_id end
        issues[#issues + 1] = problem
    end
    if tostring(values.baseRevision or '') ~= state.layoutRevision then
        issue('', 'CITY_LAYOUT_STALE')
    end
    for _, placement in ipairs(placements) do
        local source = meta.by_uuid[placement.uuid]
        if source == nil then
            issue(placement.uuid, 'CITY_LAYOUT_BUILDING_MISSING')
        elseif not source.public.movable then
            issue(placement.uuid, 'CITY_LAYOUT_IMMOVABLE')
        elseif source.public.pointId ~= placement.targetPointId then
            local target = translate_footprint(source, placement.targetPointId,
                meta.by_id, meta.ctx.width)
            if not target then
                issue(placement.uuid, 'CITY_LAYOUT_OUTSIDE', placement.targetPointId)
            else
                local zone_type = source.template.zoneType
                if zone_type == nil then
                    issue(placement.uuid, 'CITY_LAYOUT_ZONE', placement.targetPointId)
                else
                    for _, pid in ipairs(target) do
                        local cell = meta.by_id[pid]
                        if not cell.unlocked then
                            issue(placement.uuid, 'CITY_LAYOUT_LOCKED', pid)
                            break
                        elseif cell.road then
                            issue(placement.uuid, 'CITY_LAYOUT_ROAD', pid)
                            break
                        elseif cell.flagOnly and not source.public.isFlag then
                            issue(placement.uuid, 'CITY_LAYOUT_FLAG_ONLY', pid)
                            break
                        elseif not meta.ctx.zone:CanPutBuildByPoint(pid, zone_type) then
                            issue(placement.uuid, 'CITY_LAYOUT_ZONE', pid)
                            break
                        end
                    end
                end
                jobs[#jobs + 1] = {
                    uuid = placement.uuid, source = source,
                    currentPointId = source.public.pointId,
                    destination = placement.targetPointId,
                    footprints = source.public.occupiedPoints,
                    desiredFootprints = target,
                    zoneType = source.template.zoneType,
                }
                for _, pid in ipairs(target) do
                    if final_occupied[pid] ~= nil then
                        issue(placement.uuid, 'CITY_LAYOUT_OVERLAP', pid)
                    end
                    final_occupied[pid] = placement.uuid
                end
            end
        end
    end
    if #issues > 0 then
        return { valid = false, issues = issues,
            totalMoves = 0, temporaryMoves = 0 }, state, meta, {}
    end

    local occupancy = {}
    for _, building in ipairs(state.buildings) do
        for _, pid in ipairs(building.occupiedPoints) do
            if occupancy[pid] and occupancy[pid] ~= building.uuid then
                issue(building.uuid, 'CITY_LAYOUT_OVERLAP', pid)
            end
            occupancy[pid] = building.uuid
        end
    end
    if #issues > 0 then
        return { valid = false, issues = issues,
            totalMoves = 0, temporaryMoves = 0 }, state, meta, {}
    end

    -- Validate the whole *resulting* occupancy, including buildings the user
    -- did not move. A requested destination overlapping a stationary building
    -- cannot be solved by a temporary buffer, and must report the actual
    -- conflict instead of searching until CITY_LAYOUT_NO_BUFFER.
    local job_by_uuid, eventual_occupancy = {}, {}
    for _, job in ipairs(jobs) do job_by_uuid[job.uuid] = job end
    for _, building in ipairs(state.buildings) do
        local job = job_by_uuid[building.uuid]
        local eventual = job and job.desiredFootprints or building.occupiedPoints
        for _, pid in ipairs(eventual) do
            local previous = eventual_occupancy[pid]
            if previous and previous ~= building.uuid then
                issue(building.uuid, 'CITY_LAYOUT_OVERLAP', pid)
            else
                eventual_occupancy[pid] = building.uuid
            end
        end
    end
    if #issues > 0 then
        return { valid = false, issues = issues,
            totalMoves = 0, temporaryMoves = 0 }, state, meta, {}
    end

    -- Source-backed per-building moves. Plan a safe sequential order. In a
    -- cycle, choose a *currently free* valid buffer footprint, then replan.
    -- The server confirms each step before the next is ever dispatched.
    local steps, temporary = {}, 0
    local function free_for(job, dest)
        for _, pid in ipairs(dest) do
            if occupancy[pid] and occupancy[pid] ~= job.uuid then return false end
        end
        return true
    end
    local function append_step(job, destination, positions, is_temporary)
        steps[#steps + 1] = {
            uuid = job.uuid, nativeUuid = job.source.nativeUuid,
            from = job.currentPointId, pointId = destination,
            temporary = is_temporary == true,
        }
        for _, pid in ipairs(job.footprints) do
            if occupancy[pid] == job.uuid then occupancy[pid] = nil end
        end
        for _, pid in ipairs(positions) do occupancy[pid] = job.uuid end
        job.currentPointId, job.footprints = destination, positions
        if is_temporary then temporary = temporary + 1 end
    end
    local finished = {}
    while #steps < MAX_STEPS do
        local left = 0
        for _, job in ipairs(jobs) do
            if not finished[job.uuid] then left = left + 1 end
        end
        if left == 0 then break end
        local advanced = false
        for _, job in ipairs(jobs) do
            if not finished[job.uuid] and free_for(job, job.desiredFootprints) then
                append_step(job, job.destination, job.desiredFootprints, false)
                finished[job.uuid] = true
                advanced = true
                break
            end
        end
        if not advanced then
            local found = false
            for _, job in ipairs(jobs) do
                if not finished[job.uuid] then
                    for point_id = 1, #state.cells do
                        local target = translate_footprint(job.source, point_id,
                            meta.by_id, meta.ctx.width)
                        local allowed = target and point_id ~= job.currentPointId
                        if allowed then
                            for _, pid in ipairs(target) do
                                local cell = meta.by_id[pid]
                                if final_occupied[pid] or not cell.unlocked or cell.road or
                                    (cell.flagOnly and not job.source.public.isFlag) or
                                    not meta.ctx.zone:CanPutBuildByPoint(pid, job.zoneType) then
                                    allowed = false
                                    break
                                end
                            end
                        end
                        if allowed and free_for(job, target) then
                            append_step(job, point_id, target, true)
                            found = true
                            break
                        end
                    end
                end
                if found then break end
            end
            if not found then issue('', 'CITY_LAYOUT_NO_BUFFER'); break end
        end
    end
    if #steps >= MAX_STEPS then issue('', 'CITY_LAYOUT_NO_BUFFER') end
    if #issues > 0 then
        return { valid = false, issues = issues,
            totalMoves = 0, temporaryMoves = 0 }, state, meta, {}
    end
    return { valid = true, issues = {}, totalMoves = #steps,
        temporaryMoves = temporary }, state, meta, steps
end

local function status_data(job)
    if job == nil then return { state = 'idle' } end
    return {
        jobId = job.jobId,
        state = job.state == 'running' and job.cancelRequested and 'cancelling' or job.state,
        totalMoves = #job.steps, completedMoves = job.completed,
        temporaryMoves = job.temporaryMoves,
        errorCode = job.errorCode, error = job.error,
    }
end

local function same_owner(job, values)
    return job.profileId == values.profileId and
        job.launchSessionId == values.launchSessionId and
        job.challenge == values.challenge
end

local function check_positions(job)
    local bm = rawget(_G, 'DataCenter') and _G.DataCenter.BuildManager
    if not bm then return false end
    for uuid, point_id in pairs(job.expected) do
        local source = job.sources[uuid]
        local record = bm:GetBuildingDataByUuid(source.nativeUuid)
        if not record or tonumber(record.pointId) ~= point_id then return false end
    end
    return true
end

local function dispatch_step(job)
    local step = job.steps[job.completed + 1]
    if step == nil then job.state = 'completed'; return true end
    local observed, meta_or_reason = snapshot()
    if not observed or not observed.isInCity then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_NOT_IN_CITY'
        job.error = meta_or_reason or 'The inner-city game scene is no longer active.'
        return false
    end
    if not check_positions(job) then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
        job.error = 'A game building changed independently of this layout job.'
        return false
    end
    -- Revalidate *against the latest game managers* before every server send.
    -- Zone, road, lock and occupancy could have changed while awaiting an ACK.
    local meta = meta_or_reason
    local building = meta.by_uuid[step.uuid]
    local target = building and translate_footprint(building, step.pointId,
        meta.by_id, meta.ctx.width)
    local zone_type = building and building.template.zoneType
    if not building or not building.public.movable or not zone_type or not target then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
        job.error = 'Building placement eligibility changed during layout execution.'
        return false
    end
    for _, pid in ipairs(target) do
        local cell = meta.by_id[pid]
        if not cell.unlocked or cell.road or
            (cell.flagOnly and not building.public.isFlag) or
            not meta.ctx.zone:CanPutBuildByPoint(pid, zone_type) then
            job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
            job.error = 'The next building destination is no longer available.'
            return false
        end
        for _, other in ipairs(observed.buildings) do
            if other.uuid ~= step.uuid then
                for _, occupied_id in ipairs(other.occupiedPoints) do
                    if occupied_id == pid then
                        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
                        job.error = 'The next building destination became occupied.'
                        return false
                    end
                end
            end
        end
    end
    local net = rawget(_G, 'SFSNetwork')
    local message = rawget(_G, 'MsgDefines')
    if not net or not message or not message.FreeBuildingReplaceNew or
        type(net.SendMessage) ~= 'function' then
        job.state, job.errorCode = 'failed', 'GAME_METHOD_UNAVAILABLE'
        job.error = 'The official building replacement sender is unavailable.'
        return false
    end
    -- Exactly UIPlaceBuildView.OnConfirmBtnClick:430, replacement branch.
    local sent, failure = pcall(net.SendMessage, message.FreeBuildingReplaceNew,
        { buildUuid = step.nativeUuid, pointId = step.pointId })
    if not sent or failure == false then
        job.state, job.errorCode = 'failed', 'GAME_MOVE_DISPATCH_FAILED'
        job.error = sent and 'The native building replacement sender declined the move.'
            or tostring(failure)
        return false
    end
    job.waiting = step
    job.sentAt = os.time()
    return true
end

local function progress_job(job)
    if job.state ~= 'running' or not job.waiting then return end
    local current = job.waiting
    local bm = rawget(_G, 'DataCenter') and _G.DataCenter.BuildManager
    local building = bm and bm:GetBuildingDataByUuid(current.nativeUuid)
    local observed = building and tonumber(building.pointId)
    if observed == current.pointId then
        -- BuildingDate is changed by the *server reply* handler, not the
        -- visual UIChangeBuilding call. This is an observed game result.
        job.expected[current.uuid] = current.pointId
        -- Confirm the complete city snapshot before reporting completion, even
        -- on the last step. A manual/foreign relocation during the ACK wait
        -- invalidates the plan regardless of whether this one move succeeded.
        if not check_positions(job) then
            job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
            job.error = 'Another building moved during this layout request.'
            return
        end
        job.waiting = nil
        job.completed = job.completed + 1
        if job.cancelRequested then job.state = 'cancelled'
        elseif job.completed == #job.steps then job.state = 'completed'
        else dispatch_step(job) end
    elseif observed ~= current.from then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
        job.error = 'The game moved a building to a different position.'
    elseif os.time() - job.sentAt > MOVE_RESPONSE_TIMEOUT_SECONDS then
        job.state, job.errorCode = 'failed', 'GAME_MOVE_RESULT_TIMEOUT'
        job.error = 'The game did not confirm the building movement.'
    end
end

-- Advance server-confirmed moves on the owned game tick, including when the
-- City UI is hidden and its React status poll is suspended. The caller must
-- invoke Pump only while the original authenticated game owner is lease-fresh.
-- No new send occurs until the official BuildManager observes the previous
-- server response; CleanupAfterLeaseLoss prevents successor-owner sends.
function M.Pump()
    local job = M.active
    if job == nil or job.state ~= 'running' then return false end
    progress_job(job)
    return true
end

function M.Register(feature)
    if not feature or type(feature.RegisterHandler) ~= 'function' then return false end
    local ok = feature.RegisterHandler('city_layout_snapshot_get', function()
        local current, reason = snapshot()
        if not current then return unsupported('CITY_LAYOUT_UNAVAILABLE', reason) end
        return confirmed(current)
    end)
    ok = feature.RegisterHandler('city_layout_validate', function(values)
        local validation, reason = validate_state(values)
        if not validation then return unsupported('CITY_LAYOUT_UNAVAILABLE', reason) end
        validation.issues = feature.Array(validation.issues)
        return confirmed(validation)
    end) and ok
    ok = feature.RegisterHandler('city_layout_apply_start', function(values)
        if M.active and M.active.state == 'running' then
            return failed('CITY_LAYOUT_BUSY', 'Another city layout job is running.')
        end
        local validation, state, meta, steps = validate_state(values)
        if not validation then return unsupported('CITY_LAYOUT_UNAVAILABLE', state) end
        if not state.isInCity then
            return failed('CITY_LAYOUT_NOT_IN_CITY', 'Return to the inner city before applying.')
        end
        if not validation.valid then
            return confirmed({ accepted = false, issues = feature.Array(validation.issues),
                status = { state = 'failed', totalMoves = 0, completedMoves = 0 } })
        end
        if #steps == 0 then
            return confirmed({ accepted = false, issues = {{ code = 'CITY_LAYOUT_NO_CHANGES', uuid = '' }} })
        end
        local sources, expected = {}, {}
        for uuid, source in pairs(meta.by_uuid) do
            sources[uuid] = source
            expected[uuid] = source.public.pointId
        end
        local job = {
            jobId = 'f018city' .. values.requestId,
            profileId = values.profileId,
            launchSessionId = values.launchSessionId,
            challenge = values.challenge,
            state = 'running', completed = 0, steps = steps,
            temporaryMoves = validation.temporaryMoves,
            expected = expected, sources = sources,
        }
        M.active = job
        if not dispatch_step(job) then
            return failed(job.errorCode or 'GAME_ACTION_FAILED',
                job.error or 'The first building move could not be dispatched.')
        end
        -- A queued ACK is not the game-server confirmation. Status observes
        -- the official BuildManager updates before marking a move completed.
        return { state = 'accepted', data = {
            accepted = true, requestDispatched = true, status = status_data(job),
        }}
    end) and ok
    ok = feature.RegisterHandler('city_layout_apply_status', function(values)
        local job = M.active
        if not job or not same_owner(job, values) then return confirmed({ state = 'idle' }) end
        progress_job(job)
        return confirmed(status_data(job))
    end) and ok
    ok = feature.RegisterHandler('city_layout_apply_cancel', function(values)
        local job = M.active
        if not job or not same_owner(job, values) or values.jobId ~= job.jobId then
            return failed('CITY_LAYOUT_JOB_MISSING', 'The requested layout job is unavailable.')
        end
        job.cancelRequested = true
        if job.state == 'running' and not job.waiting then job.state = 'cancelled' end
        -- An already sent network move cannot be revoked; cancellation stops
        -- subsequent move dispatch after the in-flight result is observed.
        return confirmed({ accepted = true, status = status_data(job) })
    end) and ok
    return ok
end

-- Once the exact Overview owner or its lease disappears, no successor may
-- inherit a pending city relocation job. An in-flight server message cannot
-- be revoked, but subsequent planned moves must never be sent under a new
-- game session. Preserve no editable job state across that ownership change.
function M.CleanupAfterLeaseLoss()
    if M.active ~= nil then
        M.active.cancelRequested = true
        M.active.state = 'cancelled'
    end
    M.active = nil
end

return M
