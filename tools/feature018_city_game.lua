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
--   Net/Msgs/BuildWorldMoveNewMessage.luac: OnCreate, HandleMessage
--   DataCenter/BuildManager/BuildManager.luac: BuildWorldMoveNewHandle
--
-- An EXISTING building move uses the placement UI's PlaceBuildType.Move,
-- NOT PlaceBuildType.Replace (which is used for inventory/rolled-up buildings).
-- Only a later authoritative BuildManager update counts as completion. A
-- dispatch response is always state='accepted', never a server ACK.

local M = { VERSION = 'feature018-city-v26-1', active = nil }
local CITY_REGION = 'main-city'
local MAX_BUILDINGS = 512
local MAX_PLACEMENTS = 64
local MAX_STEPS = 192
local MAX_CANDIDATE_BUILDINGS = 16
local MAX_CANDIDATES_PER_BUILDING = 3
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
    local scene_utils = rawget(_G, 'SceneUtils')
    local bm = data and data.BuildManager
    local zone = data and data.CityZoneManager
    local land = data and data.LandLockManager
    local templates = data and data.BuildTemplateManager
    local city_points = data and data.CityPointManager
    local board = data and data.BoardManager
    local point_data = data and data.CityPointDataManager
    local resources = data and data.CollectResourceManager
    local building_utils = rawget(_G, 'BuildingUtils')
    local building_types = rawget(_G, 'BuildingTypes')
    local point_types = rawget(_G, 'CityPointType')
    if not (bm and zone and land and templates and city_points and board and
        point_data and resources and scene_utils and
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
        type(city_points.GetPointType) ~= 'function' or point_types.Road == nil or
        type(board.GetBoardDataByPointId) ~= 'function' or
        type(point_data.GetPointDataByPointId) ~= 'function' or
        type(resources.GetResourcePointInfoByIndex) ~= 'function' or
        type(resources.GetCollectRangeInfoByIndex) ~= 'function' then
        return nil, 'Current city snapshot source is not ready'
    end
    local width = tonumber(rawget(_G, 'CityTileCount'))
    if not width or width ~= math.floor(width) or width < 1 or width > 128 then
        return nil, 'Current city grid dimensions are unavailable'
    end
    return {
        bm = bm, zone = zone, land = land, templates = templates,
        board = board, pointData = point_data, resources = resources,
        scene = scene_utils, width = width,
        buildingUtils = building_utils, flagItemId = building_types.LW_BUILD_FLAG,
        pointTypes = point_types, roadType = point_types.Road,
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
        -- v26 IsCanPutDownByPoint (513-542) uses CityPointManager.GetPointType.
        -- Its implementation checks every building via a *linear* scan before
        -- querying BoardManager, CityPointDataManager and CollectResourceManager.
        -- Avoid 10,000 times that building scan: classify the static terrain
        -- through those exact underlying managers, all keyed by point ID.
        -- Building occupancy itself comes from the authoritative footprints
        -- below and is verified by the planner, not by terrain classification.
        -- IsInFlagPosition's four special cells are exclusively for LW_BUILD_FLAG.
        local point_types = ctx.pointTypes
        local road = ctx.board:GetBoardDataByPointId(pid) ~= nil
        local other = not road and ctx.pointData:GetPointDataByPointId(pid) or nil
        local point_type = other and other.type or nil
        -- The native placement method also refuses garbage, monster and
        -- collect/resource cells (v26 IsCanPutDownByPoint 521-542).
        -- Keep these distinct from roads/land locks for accurate errors.
        local obstructed = (point_types.Garbage ~= nil and point_type == point_types.Garbage)
            or (point_types.Monster ~= nil and point_type == point_types.Monster)
            or (point_types.MonsterReward ~= nil and point_type == point_types.MonsterReward)
            or (point_types.Collect ~= nil and point_type == point_types.Collect)
            or (point_types.CollectRange ~= nil and point_type == point_types.CollectRange)
        if not road and not other then
            obstructed = obstructed or
                ctx.resources:GetResourcePointInfoByIndex(pid) ~= nil or
                ctx.resources:GetCollectRangeInfoByIndex(pid) ~= nil
        end
        local flag_only = ctx.buildingUtils.IsInFlagPosition(pid) == true
        local cell = {
            pointId = pid, x = x, y = y, regionId = CITY_REGION,
            unlocked = unlocked, road = road, flagOnly = flag_only,
            obstructed = obstructed == true,
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
            local x, y = point_xy(point_id, width)
            -- The source's BuildingDate.GetRangedPoints (487-492) enumerates
            -- every (negative x, negative y) tile offset from the anchor,
            -- skipping points that fall outside the city grid. Such a clipped
            -- footprint cannot be moved as a complete building. Fail closed
            -- rather than planning with only its surviving partial tiles.
            local tile_x, tile_y = tonumber(template.tileX), tonumber(template.tileY)
            if not tile_x or not tile_y or tile_x ~= math.floor(tile_x) or
                tile_y ~= math.floor(tile_y) or tile_x < 1 or tile_y < 1 or
                tile_x > width or tile_y > width or #occupied ~= tile_x * tile_y then
                return nil, 'City building native footprint and template size differ'
            end
            for ox = 0, tile_x - 1 do
                for oy = 0, tile_y - 1 do
                    local expected = at_xy(x - ox, y - oy, width)
                    if not expected or footprints[expected] ~= true then
                        return nil, 'City building native footprint is truncated or irregular'
                    end
                end
            end
            sorted(occupied)
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
                tileX = tile_x,
                tileY = tile_y,
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
        local template = by_uuid[building.uuid].template
        signature[#signature + 1] = building.uuid .. ':' .. building.pointId .. ':' ..
            table.concat(building.occupiedPoints, ',') .. ':' ..
            tostring(building.itemId) .. ':' .. tostring(building.tileX) .. ':' ..
            tostring(building.tileY) .. ':' .. tostring(building.level) .. ':' ..
            (building.movable and '1' or '0') .. ':' ..
            (building.isFlag and '1' or '0') .. ':' ..
            tostring(template.zoneType) .. ':' .. tostring(template.build_type) .. ':' ..
            tostring(template.sup_main_build_id)
    end
    -- Original IsCanPutDownByPoint -> GetBuildMaxRadius -> DataConfig and
    -- ADD_CAN_BUILD_NUM effect; radius can change while all building anchors
    -- and city zone points remain fixed. A pre-change client revision must
    -- not silently authorize a now-unreachable destination.
    local radius_ok, radius = pcall(ctx.board.GetBuildMaxRadius, ctx.board)
    signature[#signature + 1] = 'placement-radius:' ..
        (radius_ok and tostring(radius) or 'unavailable')
    -- Preserve a revision on *placement permissions*, not only building
    -- coordinates: newly allowed/restricted zones and the military gate's
    -- CanMoveBuild result can change without moving a single building.
    for _, cell in ipairs(cells) do
        signature[#signature + 1] = (cell.unlocked and '1' or '0') ..
            (cell.road and '1' or '0') .. (cell.flagOnly and '1' or '0') ..
            (cell.obstructed and '1' or '0') ..
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

local function second_range_ok(ctx, source, pid)
    -- v26 BuildingUtils.IsCanPutDownByPoint 477-480: a Second building
    -- must fit an actual placed main building's current offer_range. The
    -- corresponding IsInMainSubRange (1103-1119) checks current main
    -- buildings and GetBuildingLevelTemplate, so hard-coded radii or zones
    -- would bypass the game's real placement restriction.
    local template = source.template
    if template.build_type == nil then return true end
    local build_type = rawget(_G, 'BuildType')
    if not build_type or build_type.Second == nil then return false end
    if template.build_type ~= build_type.Second then return true end
    if template.sup_main_build_id == nil or
        type(ctx.buildingUtils.IsInMainSubRange) ~= 'function' then
        return false
    end
    local ok, admitted = pcall(ctx.buildingUtils.IsInMainSubRange,
        pid, template.sup_main_build_id)
    return ok and admitted == true
end

local function native_radius_ok(ctx, pid)
    -- IsCanPutDownByPoint (496-498) applies this extra original-game gate
    -- to ordinary city buildings. Checking it conservatively for all moved
    -- buildings may exclude special exceptions, but cannot permit an unsafe
    -- placement. Absence of either source method never means "allowed".
    if type(ctx.buildingUtils.IsInMyBaseCircleRange) ~= 'function' or
        type(ctx.board.GetBuildMaxRadius) ~= 'function' then return false end
    local radius_ok, radius = pcall(ctx.board.GetBuildMaxRadius, ctx.board)
    if not radius_ok or type(radius) ~= 'number' then return false end
    local admitted, result = pcall(
        ctx.buildingUtils.IsInMyBaseCircleRange, pid, radius)
    return admitted and result == true
end

local function snapshot_move_candidates(state, meta)
    -- The visible city grid is not a placement-permission map. In v26,
    -- CityZoneManager.CanPutBuildByPoint (70-75) delegates to each point's
    -- CityZoneTemplate.CanPutByZoneType (49-60); a geometrically free tile can
    -- still be prohibited. Locate a few *current* legal empty destinations
    -- using the game's real zone and main-range predicates. This is read-only,
    -- and a later validate/apply must independently revalidate every point.
    local ctx = meta.ctx
    local candidates, occupancy = {}, {}
    local movable_count, searched = 0, 0
    for _, building in ipairs(state.buildings) do
        if building.movable then movable_count = movable_count + 1 end
        for _, pid in ipairs(building.occupiedPoints) do
            -- An inconsistent native occupancy is not a trustworthy planner.
            if occupancy[pid] ~= nil then
                return {}, { movableBuildings = movable_count,
                    searchedBuildings = 0, inconsistentOccupancy = true }
            end
            occupancy[pid] = building.uuid
        end
    end
    -- IsCanPutDownByPoint (496-498) additionally gates ordinary inner-city
    -- placements via IsInMyBaseCircleRange(pointId, GetBuildMaxRadius()).
    -- Missing native helpers must never produce a supposedly legal target.
    local can_range = type(ctx.buildingUtils.IsInMyBaseCircleRange) == 'function'
        and type(ctx.board.GetBuildMaxRadius) == 'function'
    if can_range then
        local ok, value = pcall(ctx.board.GetBuildMaxRadius, ctx.board)
        can_range = ok and type(value) == 'number'
    end
    for _, building in ipairs(state.buildings) do
        if building.movable and searched < MAX_CANDIDATE_BUILDINGS then
            searched = searched + 1
            local source = meta.by_uuid[building.uuid]
            local zone_type = source.template.zoneType
            if zone_type ~= nil and can_range then
                local count = 0
                -- Search by Manhattan distance from the current anchor;
                -- unlike an arbitrary 35-point probe, each result is checked
                -- against the native zone policy for its *entire* footprint.
                for distance = 1, 2 * (ctx.width - 1) do
                    if count >= MAX_CANDIDATES_PER_BUILDING then break end
                    for dx = -distance, distance do
                        if count >= MAX_CANDIDATES_PER_BUILDING then break end
                        local delta_y = distance - math.abs(dx)
                        for sign = -1, 1, 2 do
                            local dy = delta_y * sign
                            if delta_y ~= 0 or sign == -1 then
                                local x, y = building.x + dx, building.y + dy
                                local anchor = at_xy(x, y, ctx.width)
                                local target = anchor and translate_footprint(
                                    source, anchor, meta.by_id, ctx.width)
                                if target then
                                    local legal = true
                                    for _, pid in ipairs(target) do
                                        local cell = meta.by_id[pid]
                                        if not cell.unlocked or cell.road or
                                            cell.obstructed or occupancy[pid] ~= nil or
                                            (cell.flagOnly and not building.isFlag) or
                                            not ctx.zone:CanPutBuildByPoint(pid, zone_type) or
                                            not second_range_ok(ctx, source, pid) then
                                            legal = false
                                            break
                                        end
                                        if not native_radius_ok(ctx, pid) then
                                            legal = false
                                            break
                                        end
                                    end
                                    if legal then
                                        candidates[#candidates + 1] = {
                                            uuid = building.uuid,
                                            fromPointId = building.pointId,
                                            targetPointId = anchor,
                                            distance = distance,
                                        }
                                        count = count + 1
                                    end
                                end
                            end
                        end
                    end
                end
            end
        end
    end
    return candidates, {
        movableBuildings = movable_count,
        searchedBuildings = searched,
        truncated = searched < movable_count,
        nativeRangeAvailable = can_range == true,
    }
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
                        elseif cell.obstructed then
                            issue(placement.uuid, 'CITY_LAYOUT_OBSTRUCTED', pid)
                            break
                        elseif cell.flagOnly and not source.public.isFlag then
                            issue(placement.uuid, 'CITY_LAYOUT_FLAG_ONLY', pid)
                            break
                        elseif not meta.ctx.zone:CanPutBuildByPoint(pid, zone_type) then
                            issue(placement.uuid, 'CITY_LAYOUT_ZONE', pid)
                            break
                        elseif not native_radius_ok(meta.ctx, pid) then
                            issue(placement.uuid, 'CITY_LAYOUT_RANGE', pid)
                            break
                        elseif not second_range_ok(meta.ctx, source, pid) then
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
                                    cell.obstructed or
                                    (cell.flagOnly and not job.source.public.isFlag) or
                                    not meta.ctx.zone:CanPutBuildByPoint(pid, job.zoneType) or
                                    not native_radius_ok(meta.ctx, pid) or
                                    not second_range_ok(meta.ctx, job.source, pid) then
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
        inFlight = job.waiting ~= nil,
        pendingUuid = job.waiting and job.waiting.uuid or nil,
        pendingFromPointId = job.waiting and job.waiting.from or nil,
        pendingPointId = job.waiting and job.waiting.pointId or nil,
        cancelRequested = job.cancelRequested == true,
        cancelRequestedWhileInFlight = job.cancelRequestedWhileInFlight,
        cancelRequestedAtCompletedMoves = job.cancelRequestedAtCompletedMoves,
        dispatchedMoves = job.dispatched or 0,
        matchingNativeReplies = job.matchingNativeReplies or 0,
        pendingNativeReplyObserved = job.waiting ~= nil and
            job.replyForStep == job.waiting or false,
    }
end

local function remove_reply_observer(job)
    if not job or not job.replyObserver then return end
    if job.replyManager and
        job.replyManager.BuildWorldMoveNewHandle == job.replyObserver then
        job.replyManager.BuildWorldMoveNewHandle = job.originalReplyHandler
    end
    job.replyObserver = nil
end

local function observe_native_move_replies(job, bm)
    -- v26 BuildWorldMoveNewMessage.HandleMessage delegates the exact server
    -- payload to BuildManager:BuildWorldMoveNewHandle. Its success branch
    -- reads buildInfo.uuid, applies buildInfo via AddBuilding, and broadcasts
    -- BuildingMove{uuid,old,new}. Unlike a coordinate echo or local placement
    -- animation, this is an actual game reply with a building identity.
    if job.replyObserver then
        return job.replyManager == bm and
            bm.BuildWorldMoveNewHandle == job.replyObserver
    end
    local previous = bm.BuildWorldMoveNewHandle
    if type(previous) ~= 'function' then return false end
    local function observer(self, response, ...)
        local step = job.waiting
        local info = type(response) == 'table' and response.buildInfo or nil
        local matching = M.active == job and step ~= nil and
            type(info) == 'table' and response.errorCode == nil and
            tostring(info.uuid) == step.uuid and
            tonumber(info.pointId) == step.pointId and
            tonumber(info.bId) == job.sources[step.uuid].public.itemId
        local before = matching and self:GetBuildingDataByUuid(step.nativeUuid) or nil
        local from = before and tonumber(before.pointId)
        -- Always invoke the original handler once with its original receiver
        -- and arguments, preserving the game-owned reply and error handling.
        previous(self, response, ...)
        if matching and from == step.from and job.replyForStep ~= step then
            local after = self:GetBuildingDataByUuid(step.nativeUuid)
            if after and tonumber(after.pointId) == step.pointId and
                tonumber(after.itemId) == job.sources[step.uuid].public.itemId then
                job.replyForStep = step
                job.matchingNativeReplies = (job.matchingNativeReplies or 0) + 1
            end
        end
    end
    bm.BuildWorldMoveNewHandle = observer
    if bm.BuildWorldMoveNewHandle ~= observer then return false end
    job.replyManager, job.originalReplyHandler, job.replyObserver =
        bm, previous, observer
    return true
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
        if not record or tonumber(record.pointId) ~= point_id or
            tonumber(record.itemId) ~= source.public.itemId or
            type(record.GetRangedPoints) ~= 'function' then return false end
        -- v26 GetRangedPoints derives its tiles from pointId and itemId,
        -- caching only while both match (BuildingDate 478-505). A pointId
        -- match alone is not a complete game poststate: another update may
        -- have replaced an item's shape while an ACK was pending. Compare
        -- the entire source-equivalent translated footprint before any next
        -- send and before the final 'completed' status.
        local current_footprint = record:GetRangedPoints()
        if type(current_footprint) ~= 'table' then return false end
        local x, y = point_xy(point_id, job.width)
        local expected_footprint = {}
        for _, original_id in ipairs(source.public.occupiedPoints) do
            local old_x, old_y = point_xy(original_id, job.width)
            local next_id = at_xy(x + old_x - source.public.x,
                y + old_y - source.public.y, job.width)
            if not next_id or current_footprint[next_id] ~= true then return false end
            expected_footprint[next_id] = true
        end
        for footprint_id, occupied in pairs(current_footprint) do
            if occupied and not expected_footprint[tonumber(footprint_id)] then
                return false
            end
        end
    end
    return true
end

local function dispatch_step(job)
    local step = job.steps[job.completed + 1]
    if step == nil then
        job.state = 'completed'
        remove_reply_observer(job)
        return true
    end
    local observed, meta_or_reason = snapshot()
    if not observed or not observed.isInCity then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_NOT_IN_CITY'
        job.error = meta_or_reason or 'The inner-city game scene is no longer active.'
        return false
    end
    if meta_or_reason.ctx.width ~= job.width or not check_positions(job) then
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
        if not cell.unlocked or cell.road or cell.obstructed or
            (cell.flagOnly and not building.public.isFlag) or
            not meta.ctx.zone:CanPutBuildByPoint(pid, zone_type) or
            not native_radius_ok(meta.ctx, pid) or
            not second_range_ok(meta.ctx, building, pid) then
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
    if not net or not message or not message.BuildWorldMoveNew or
        type(net.SendMessage) ~= 'function' then
        job.state, job.errorCode = 'failed', 'GAME_METHOD_UNAVAILABLE'
        job.error = 'The official existing-building move sender is unavailable.'
        return false
    end
    if not observe_native_move_replies(job, meta.ctx.bm) then
        job.state, job.errorCode = 'failed', 'GAME_METHOD_UNAVAILABLE'
        job.error = 'The native Move reply handler cannot be observed safely.'
        return false
    end
    -- v26 UIPlaceBuildView.OnConfirmBtnClick:436-459, PlaceBuildType.Move.
    -- OnCreate encodes uuid as PutLong and pointId as PutInt; lastIndex is
    -- NOT transmitted, but is required by the original reply handler to
    -- restore a rejected movement's previous visual location. Preserve the
    -- original server-owned building.uuid object (never parse it via double).
    -- Replace (line 430) is a different action and packet: it may legitimately
    -- leave an already placed building unchanged even when SendMessage returns.
    job.waiting = step
    job.replyForStep = nil
    job.sentAt = os.time()
    job.dispatched = (job.dispatched or 0) + 1
    local sent, failure = pcall(net.SendMessage, message.BuildWorldMoveNew,
        { uuid = step.nativeUuid, pointId = step.pointId, lastIndex = step.from })
    if not sent or failure == false then
        -- The native sender is void-returning. A bridge exception/false value
        -- cannot prove the packet did not escape, so retain the pending fence.
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_RESULT_UNCERTAIN'
        job.error = sent and 'The native move sender returned an unexpected failure.'
            or ('The native move sender raised: ' .. tostring(failure))
        return false
    end
    return true
end

local function progress_job(job)
    if job.state ~= 'running' or not job.waiting then return end
    local current = job.waiting
    local bm = rawget(_G, 'DataCenter') and _G.DataCenter.BuildManager
    local building = bm and bm:GetBuildingDataByUuid(current.nativeUuid)
    local observed = building and tonumber(building.pointId)
    if observed == current.pointId and job.replyForStep == current then
        -- BuildWorldMoveNewHandle invokes AddBuilding on the server's
        -- buildInfo then broadcasts BuildingMove({uuid,old,new}); inspecting
        -- native BuildingDate plus full translated footprint avoids mistaking
        -- the sender's local visual UIChangeBuilding for a server ACK.
        job.expected[current.uuid] = current.pointId
        -- Confirm the complete city snapshot before reporting completion, even
        -- on the last step. A manual/foreign relocation during the ACK wait
        -- invalidates the plan regardless of whether this one move succeeded.
        if not check_positions(job) then
            job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
            job.error = 'Another building moved during this layout request.'
            job.waiting = nil -- Exact reply arrived; further state needs a new plan.
            remove_reply_observer(job)
            return
        end
        job.waiting = nil
        job.completed = job.completed + 1
        if job.cancelRequested then job.state = 'cancelled'
        elseif job.completed == #job.steps then job.state = 'completed'
        else dispatch_step(job) end
        if job.state ~= 'running' and not job.waiting then
            remove_reply_observer(job)
        end
    elseif observed == current.pointId then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_RESULT_UNCERTAIN'
        job.error = 'The target position changed without a matching native Move reply; an unconfirmed packet remains outstanding.'
    elseif observed ~= current.from then
        job.state, job.errorCode = 'failed', 'CITY_LAYOUT_STALE'
        job.error = 'The game moved a building to a different position.'
    elseif os.time() - job.sentAt > MOVE_RESPONSE_TIMEOUT_SECONDS then
        job.state, job.errorCode = 'failed', 'GAME_MOVE_RESULT_TIMEOUT'
        job.error = 'The game did not confirm the building movement.'
    end
end

local function reconcile_timed_out(job)
    -- The original BuildWorldMoveNew sender has no synchronous result.
    -- A timeout is not server rejection: its in-flight packet can still be
    -- applied later. Keep that job fenced until the *exact* official game
    -- BuildManager destination and all other expected footprints are seen.
    -- The same rule applies if the periodic owner token vanishes after the
    -- packet was sent. Do not resume the rest of an abandoned layout plan.
    if job.state ~= 'failed' or
        (job.errorCode ~= 'GAME_MOVE_RESULT_TIMEOUT' and
         job.errorCode ~= 'CITY_LAYOUT_OWNER_LOSS_UNCERTAIN' and
         job.errorCode ~= 'CITY_LAYOUT_STALE' and
         job.errorCode ~= 'CITY_LAYOUT_RESULT_UNCERTAIN') or
        not job.waiting then return end
    local owner_lost = job.errorCode == 'CITY_LAYOUT_OWNER_LOSS_UNCERTAIN'
    local pending = job.waiting
    local bm = rawget(_G, 'DataCenter') and _G.DataCenter.BuildManager
    local record = bm and bm:GetBuildingDataByUuid(pending.nativeUuid)
    if job.replyForStep ~= pending or not record or
        tonumber(record.pointId) ~= pending.pointId then return end
    job.expected[pending.uuid] = pending.pointId
    if check_positions(job) then
        job.completed = job.completed + 1
        if owner_lost then
            job.errorCode = 'CITY_LAYOUT_OWNER_LOSS_RECONCILED'
            job.error = 'The late native building result was observed after ownership was interrupted. Remaining layout moves were cancelled.'
        end
    else
        job.errorCode = 'CITY_LAYOUT_STALE'
        job.error = 'The late server result changed other city building state.'
    end
    job.waiting = nil
    job.cancelRequested = true
    remove_reply_observer(job)
end

-- Advance server-confirmed moves on the owned game tick, including when the
-- City UI is hidden and its React status poll is suspended. The caller must
-- invoke Pump only while the original authenticated game owner is lease-fresh.
-- No new send occurs until the official BuildManager observes the previous
-- server response; CleanupAfterLeaseLoss prevents successor-owner sends.
function M.Pump()
    local job = M.active
    if job == nil then return false end
    if job.state == 'running' then
        progress_job(job)
        return true
    end
    if job.state == 'failed' and
        (job.errorCode == 'GAME_MOVE_RESULT_TIMEOUT' or
         job.errorCode == 'CITY_LAYOUT_OWNER_LOSS_UNCERTAIN' or
         job.errorCode == 'CITY_LAYOUT_STALE' or
         job.errorCode == 'CITY_LAYOUT_RESULT_UNCERTAIN') and
        job.waiting then
        reconcile_timed_out(job)
        return true
    end
    return false
end

function M.Register(feature)
    if not feature or type(feature.RegisterHandler) ~= 'function' then return false end
    local ok = feature.RegisterHandler('city_layout_snapshot_get', function()
        local current, meta_or_reason = snapshot()
        if not current then return unsupported('CITY_LAYOUT_UNAVAILABLE', meta_or_reason) end
        local candidates, search = snapshot_move_candidates(current, meta_or_reason)
        current.moveCandidates = feature.Array(candidates)
        current.moveCandidateSearch = search
        return confirmed(current)
    end)
    ok = feature.RegisterHandler('city_layout_validate', function(values)
        local validation, state_or_reason, _, steps = validate_state(values)
        if not validation then return unsupported('CITY_LAYOUT_UNAVAILABLE', state_or_reason) end
        -- The client must refresh its snapshot before retrying a stale layout;
        -- exposing the observed revision diagnoses legitimate async city drift
        -- without treating an old draft as safe to apply.
        validation.currentRevision = state_or_reason.layoutRevision
        validation.issues = feature.Array(validation.issues)
        local public_steps = {}
        for index, step in ipairs(steps or {}) do
            public_steps[#public_steps + 1] = { index = index, uuid = step.uuid,
                fromPointId = step.from, pointId = step.pointId,
                temporary = step.temporary == true }
        end
        validation.plannedMoves = feature.Array(public_steps)
        return confirmed(validation)
    end) and ok
    ok = feature.RegisterHandler('city_layout_apply_start', function(values)
        if M.active then reconcile_timed_out(M.active) end
        if M.active and M.active.state == 'running' then
            return failed('CITY_LAYOUT_BUSY', 'Another city layout job is running.')
        end
        if M.active and M.active.waiting then
            return failed('CITY_LAYOUT_RESULT_UNCERTAIN',
                'A previous native building replacement may still receive a server result; no other move can be safely sent.')
        end
        if M.active then remove_reply_observer(M.active) end
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
            width = meta.ctx.width,
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
        reconcile_timed_out(job)
        return confirmed(status_data(job))
    end) and ok
    ok = feature.RegisterHandler('city_layout_apply_cancel', function(values)
        local job = M.active
        if not job or not same_owner(job, values) or values.jobId ~= job.jobId then
            return failed('CITY_LAYOUT_JOB_MISSING', 'The requested layout job is unavailable.')
        end
        job.cancelRequested = true
        -- This receipt describes the moment the actual Cancel command was
        -- consumed, before status polling or an ACK can advance the job.
        -- Repeated Cancel must not overwrite an earlier in-flight observation.
        if job.cancelRequestedWhileInFlight == nil then
            job.cancelRequestedWhileInFlight = job.waiting ~= nil
            job.cancelRequestedAtCompletedMoves = job.completed
        end
        if job.state == 'running' and not job.waiting then job.state = 'cancelled' end
        -- An already sent network move cannot be revoked; cancellation stops
        -- subsequent move dispatch after the in-flight result is observed.
        return confirmed({ accepted = true, status = status_data(job) })
    end) and ok
    return ok
end

-- A transient Feature018OwnerToken() nil can occur while the native host
-- lease/account observer is briefly unavailable. The previously dispatched
-- message cannot be revoked, so wiping its in-memory job would lose the only
-- duplicate-send fence. Retain a terminal, cancelled, owner-bound tombstone
-- until the exact native BuildManager result can be reconciled. A successor
-- cannot read the prior owner's status or resume its pending layout steps,
-- and cannot send a new move while this message remains uncertain.
function M.CleanupAfterLeaseLoss()
    if M.active ~= nil then
        M.active.cancelRequested = true
        if M.active.waiting then
            M.active.state = 'failed'
            if M.active.errorCode ~= 'CITY_LAYOUT_OWNER_LOSS_RECONCILED' then
                M.active.errorCode = 'CITY_LAYOUT_OWNER_LOSS_UNCERTAIN'
                M.active.error = 'Native owner admission was interrupted after a building replacement was sent. Its server outcome is unknown; another move is blocked.'
            end
            return
        end
        M.active.state = 'cancelled'
        remove_reply_observer(M.active)
    end
    M.active = nil
end

return M
