"""Inert Lua 5.3 exercise of the actual FEATURE018 City Layout producer.

Uses fake tables, a captured network sender and the bundled isolated Lua VM.
No game, updater, installed scripts, active journal, or live network I/O.
"""
from __future__ import annotations

import sys
import hashlib
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parents[0] / 'artifacts/map-013-lua-site'))
sys.path.insert(0, str(ROOT / 'map_009_offline'))
sys.path.insert(0, str(ROOT))
from lupa.lua53 import LuaRuntime  # noqa: E402
import compact_lua53 as bytecode  # noqa: E402
import execute_selected as selected  # noqa: E402
import run_live_resource_probe as probe  # noqa: E402
from inspect_current import root_methods, disassemble  # noqa: E402
from feature018_mini_lua import ORIGINAL_ENTRY_SHA256  # noqa: E402


MODULE = Path(__file__).with_name('feature018_city_game.lua')

GAME = r'''
CityTileCount = 5
BuildingStateType = {Normal = 1, FoldUp = 2}
BuildingTypes = {FUN_BUILD_MAIN = 700, LW_BUILD_GATE = 701, LW_BUILD_FLAG = 777}
CityPointType = {Empty = 0, Road = 42, Building = 43, Garbage = 44,
    Monster = 45, MonsterReward = 46, Collect = 47, CollectRange = 48,
    Other = 49}
NativeCityPointKinds = {[25] = CityPointType.Road}
NativeCityPointScans = 0
GateLevel = 1
local flagPositions = {[5049]=true, [5050]=true, [4950]=true, [4949]=true}
BuildingUtils = {
    IsInFlagPosition = function(id) return flagPositions[id] == true end,
    IsInMyBaseCircleRange = function(pointId, radius)
        return radius == 50
    end,
    CanMoveBuild = function(itemId)
        if itemId == BuildingTypes.FUN_BUILD_MAIN then return false end
        local gates = DataCenter.BuildManager:GetBuildingDatasByBuildingId(BuildingTypes.LW_BUILD_GATE)
        local gate = gates[1]
        if gate == nil or gate.level < 1 then return false end
        if itemId ~= nil then
            local template = DataCenter.BuildTemplateManager:GetBuildingDesTemplate(itemId)
            if template ~= nil and not template:CanMove() then return false end
        end
        return true
    end,
}
SceneUtils = {GetIsInCity = function() return true end}
-- Deliberately disagree with the placement manager: World is not the v26
-- placement rule's authoritative CityPointManager tile classifier.
CS = {SceneManager = {World = {GetPointType = function(self, pointId)
    if pointId == 23 then return CityPointType.Road end
    return CityPointType.Empty
end}}}
local moves = {}
ObservedMoves = moves
SFSNetwork = {SendMessage = function(message, args)
    moves[#moves+1] = {message = message, uuid = args.uuid,
                      pointId = args.pointId, lastIndex = args.lastIndex}
end}
MsgDefines = {FreeBuildingReplaceNew = 'native_free_building_replace_new',
              BuildWorldMoveNew = 'native_build_world_move_new'}
local templates = {
  [11] = {tileX=1, tileY=1, zoneType=1, CanMove=function(self) return true end},
  [12] = {tileX=1, tileY=1, zoneType=1, CanMove=function(self) return true end},
  [13] = {tileX=1, tileY=1, zoneType=1, CanMove=function(self) return false end},
  [700] = {tileX=1, tileY=1, zoneType=1, CanMove=function(self) return true end},
  [777] = {tileX=1, tileY=1, zoneType=1, CanMove=function(self) return true end},
}
function FixtureBuilding(uuid, itemId, pointId)
    return {uuid=uuid, itemId=itemId, pointId=pointId, level=6, isWorldBuild=false,
            state=BuildingStateType.Normal,
            GetRangedPoints=function(self) return {[self.pointId]=true} end}
end
local all = { [101] = FixtureBuilding(101,11,1), [102] = FixtureBuilding(102,12,2),
              [103] = FixtureBuilding(103,13,6)}
DataCenter = {
    BuildManager = {allBuilding=all,
       GetBuildingDataByUuid=function(self, uuid) return self.allBuilding[uuid] end,
       GetBuildingDatasByBuildingId=function(self, itemId)
           if itemId ~= BuildingTypes.LW_BUILD_GATE or GateLevel < 0 then return {} end
           return {{level=GateLevel}}
       end},
    BuildTemplateManager = {GetBuildingDesTemplate = function(self, id) return templates[id] end},
    CityPointManager = {GetPointType = function(self, id)
        NativeCityPointScans = NativeCityPointScans + 1
        return NativeCityPointKinds[id] or CityPointType.Other
    end},
    BoardManager = {GetBuildMaxRadius = function(self) return 50 end,
       GetBoardDataByPointId = function(self, id)
        if NativeCityPointKinds[id] == CityPointType.Road then return {} end
        return nil
    end},
    CityPointDataManager = {GetPointDataByPointId = function(self, id)
        local kind = NativeCityPointKinds[id]
        if kind == CityPointType.Garbage or kind == CityPointType.Monster or
            kind == CityPointType.MonsterReward then return {type=kind} end
        return nil
    end},
    CollectResourceManager = {
        GetResourcePointInfoByIndex = function(self, id)
            if NativeCityPointKinds[id] == CityPointType.Collect then return {} end
        end,
        GetCollectRangeInfoByIndex = function(self, id)
            if NativeCityPointKinds[id] == CityPointType.CollectRange then return {} end
        end,
    },
    CityZoneManager = {allZonePoint = {},
        CanPutBuildByPoint = function(self, pointId, zoneType)
            return self.allZonePoint[pointId] ~= nil and zoneType == 1
        end},
    LandLockManager = {CanBuildByPointId = function(self, pointId)
        return pointId ~= 24
    end},
}
for pointId=1,CityTileCount*CityTileCount do
    DataCenter.CityZoneManager.allZonePoint[pointId] = {zone_type=1}
end
handlers = {}
Feature = {RegisterHandler = function(command, fn)
    if handlers[command] ~= nil then return false end
    handlers[command] = fn
    return true
end, Array = function(value) return setmetatable(value,{__feature018_array=true}) end}
function Act(name, props) return handlers[name](props or {}) end
function Fields(baseRevision, placements)
    return {baseRevision=baseRevision, placements=placements or '',
       requestId='abc', profileId='p1',launchSessionId='session1',challenge='nonce'}
end
'''


def fake():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute(GAME)
    module = lua.execute(MODULE.read_text(encoding='utf-8'))
    assert module.Register(lua.globals().Feature) is True
    return lua, module


def check(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError('FEATURE018_CITY_GAME: ' + message)


def check_original_game_wire() -> None:
    """Execute the shipped native message serializer without a game/network."""
    file_version, content_version, records = probe.read_lwlf(probe.paths()['data'])
    files = dict(records)
    original = files.get(probe.ORIGINAL_LUA_ENTRY, files.get(probe.LUA_ENTRY))
    check((file_version, content_version) == (3, 26) and
          hashlib.sha256(original).hexdigest() == ORIGINAL_ENTRY_SHA256,
          'not the admitted original v26 game package')

    def native_method(module, method):
        root = bytecode.parse_chunk(probe.decode_lenc(files[module]))
        path = root_methods(root)[method]
        for child in path.split('/')[1:]:
            root = root['children'][int(child)]
        return root

    ui = native_method('UI/UIPlaceBuild/View/UIPlaceBuildView.luac', 'OnConfirmBtnClick')
    source = list(disassemble(ui))
    check(any('sourceLine=430' in line and 'FreeBuildingReplaceNew' in line for line in source) and
          any('sourceLine=430' in line and 'SendMessage' in line for line in source) and
          any('sourceLine=426' in line and 'buildUuid' in line for line in source) and
          any('sourceLine=427' in line and 'pointId' in line for line in source),
          'current UI does not send the verified replacement message/fields')

    message = native_method('Net/Msgs/FreeBuildingReplaceNewMessage.luac', 'OnCreate')
    captured = []
    sfs = {'PutLong': lambda _, key, value: captured.append(('long', key, value)),
           'PutInt': lambda _, key, value: captured.append(('int', key, value))}
    # The real R3 building UUID is above IEEE-754's exact double range. Feed
    # it as the native game integer; no decimal-text-to-float round trip may
    # occur between the original BuildManager and PutLong serializer.
    native_uuid = 1392040865799139725
    selected.execute(message, {'sfsObj': sfs},
                     {'buildUuid': native_uuid, 'pointId': 4144},
                     globals_={'OnCreate': lambda _: None})
    check(captured == [('long', 'buildUuid', native_uuid),
                       ('int', 'pointId', 4144)],
          'native current-client move serializer differs from adapter request')

    # Critical R3 correction: the original UI selects two different message
    # classes. PlaceBuildType.Replace places an inventory/rolled-up building;
    # PlaceBuildType.Move relocates an existing in-city UUID. The successful
    # local FreeBuildingReplaceNew call in receipts 062112/063021 did NOT
    # exercise the original existing-building move path.
    check(any('sourceLine=424' in line and "'Replace'" in line for line in source) and
          any('sourceLine=436' in line and "'Move'" in line for line in source) and
          any('sourceLine=455' in line and "'uuid'" in line for line in source) and
          any('sourceLine=456' in line and "'pointId'" in line for line in source) and
          any('sourceLine=457' in line and "'lastIndex'" in line for line in source) and
          any('sourceLine=458' in line and "'BuildWorldMoveNew'" in line for line in source),
          'v26 existing-building Move path no longer has its distinct message')
    select_native = list(disassemble(native_method(
        'UI/UIMoveCity/View/UIMoveCityView.luac', 'ChangeSelectBuild')))
    check(any('sourceLine=1785' in line and "'Move'" in line for line in select_native) and
          any('sourceLine=1786' in line and "'buildUuid'" in line for line in select_native) and
          any('sourceLine=1787' in line and "'Replace'" in line for line in select_native) and
          any('sourceLine=1788' in line and "'FakeBuildUuid'" in line
              for line in select_native),
          'v26 City move no longer distinguishes real existing UUID from fake inventory placeholder')
    native_move = native_method('Net/Msgs/BuildWorldMoveNewMessage.luac', 'OnCreate')
    native_wire = []
    native_data = {'BuildManager': {
        'AddOneChangeMoveBuild': lambda _, point: native_wire.append(('rollback', point)),
        'SetCurrentBuildMoveState': lambda _, state: native_wire.append(('state', state)),
    }}
    native_send = {'PutLong': lambda _, key, value: native_wire.append(('long', key, value)),
                   'PutInt': lambda _, key, value: native_wire.append(('int', key, value))}
    selected.execute(native_move, {'sfsObj': native_send},
                     {'uuid': native_uuid, 'pointId': 4144, 'lastIndex': 4242},
                     globals_={'OnCreate': lambda _: None},
                     upvalues={1: {'DataCenter': native_data,
                                   'BuildMoveState': {'None': 'reset'}}})
    check(native_wire == [('long', 'uuid', native_uuid),
                          ('int', 'pointId', 4144), ('rollback', 4242),
                          ('state', 'reset')],
          'original Move serializer and rollback anchor differ from producer')
    reply = list(disassemble(native_method(
        'Net/Msgs/BuildWorldMoveNewMessage.luac', 'HandleMessage')))
    check(any('sourceLine=22' in line and 'errorCode' in line for line in reply) and
          any('sourceLine=23' in line and "'Success'" in line for line in reply) and
          any('sourceLine=23' in line and "'Fail'" in line for line in reply) and
          any('sourceLine=24' in line and 'BuildWorldMoveNewHandle' in line for line in reply),
          'original Move ACK/error handler changed')
    move_poststate = list(disassemble(native_method(
        'DataCenter/BuildManager/BuildManager.luac', 'BuildWorldMoveNewHandle')))
    check(any('sourceLine=4113' in line and 'errorCode' in line for line in move_poststate) and
          any('sourceLine=4120' in line and 'AddBuilding' in line for line in move_poststate) and
          any('sourceLine=4123' in line and "'uuid'" in line for line in move_poststate) and
          any('sourceLine=4124' in line and 'BuildingMove' in line for line in move_poststate),
          'original Move authoritative UUID-bound poststate changed')

    # R3 native 2289 receipt 063021: accepting a SendMessage invocation and
    # subsequently timing out does not establish server acceptance or a
    # uniquely attributable rejection. The v26 sender returns no result;
    # its message-specific handler forwards the reply to BuildManager, where
    # both success and error paths broadcast the same payload-free UI event.
    send_native = list(disassemble(native_method(
        'Net/SFSNetwork.luac', 'SendMessage')))
    check(any('sourceLine=28' in line and 'SendLuaMessage' in line
              for line in send_native) and
          any('sourceLine=34 RETURN' in line and 'B=1' in line
              for line in send_native),
          'original v26 SendMessage no longer has unconfirmed void-return contract')
    handle_native = list(disassemble(native_method(
        'Net/Msgs/FreeBuildingReplaceNewMessage.luac', 'HandleMessage')))
    check(any('sourceLine=18' in line and 'FreeBuildingReplaceNewHandle' in line
              for line in handle_native),
          'v26 replacement message reply no longer delegates to BuildManager')
    build_reply = list(disassemble(native_method(
        'DataCenter/BuildManager/BuildManager.luac', 'FreeBuildingReplaceNewHandle')))
    check(any('sourceLine=4062' in line and 'UIPlaceBuildSendMessageBack' in line
              for line in build_reply) and
          any('sourceLine=4063' in line and 'errorCode' in line
              for line in build_reply) and
          any('sourceLine=4071' in line and 'AddBuilding' in line
              for line in build_reply) and
          any('sourceLine=4080' in line and 'AddBuilding' in line
              for line in build_reply) and
          any('sourceLine=4089' in line and 'ShowBuildErrorCode' in line
              for line in build_reply),
          'v26 response event, server error and authoritative update paths changed')
    event_pc = next(i for i, line in enumerate(build_reply)
                    if 'sourceLine=4062' in line and 'UIPlaceBuildSendMessageBack' in line)
    error_pc = next(i for i, line in enumerate(build_reply)
                    if 'sourceLine=4063' in line and 'errorCode' in line)
    check(event_pc < error_pc and
          not any('buildUuid' in line or 'pointId' in line for line in build_reply),
          'native generic UI callback acquired an exact per-request reply identifier')

    native_can_move = native_method('Util/BuildingUtils.luac', 'CanMoveBuild')
    can_move_source = list(disassemble(native_can_move))
    for expected in ('FUN_BUILD_MAIN', 'LW_BUILD_GATE', 'GetBuildingDatasByBuildingId',
                     'level', 'GetBuildingDesTemplate', 'CanMove'):
        check(any(expected in instruction for instruction in can_move_source),
              'current game movement gate missing: ' + expected)
    native_flag = native_method('Util/BuildingUtils.luac', 'IsInFlagPosition')
    flag_ids = {value for value in native_flag['constants'] if type(value) is int and
                4900 <= value <= 5100}
    check(flag_ids == {5049, 5050, 4950, 4949},
          'native military-flag-only cell IDs differ from verified four')
    for pid in sorted(flag_ids | {4948, 5051, 5000}):
        observed = selected.execute(native_flag, pid,
                                    upvalues={0: defaultdict(lambda: None,
                                                         {item: True for item in flag_ids})})
        check(observed == (pid in flag_ids,),
              f'native flag-only lookup did not classify point {pid}')

    placement = native_method('Util/BuildingUtils.luac', 'IsCanPutDownByPoint')
    place_source = list(disassemble(placement))
    for expected in ('LW_BUILD_FLAG', 'IsInFlagPosition',
                     'CityPointManager', 'GetPointType', 'CityPointType', 'Road',
                     'Garbage', 'Monster', 'MonsterReward', 'Collect', 'CollectRange'):
        check(any(expected in instruction for instruction in place_source),
              'native point-placement flag/road rule missing: ' + expected)
    check(any('sourceLine=477' in line and "'Second'" in line for line in place_source) and
          any('sourceLine=479' in line and 'IsInMainSubRange' in line for line in place_source) and
          any('sourceLine=479' in line and 'sup_main_build_id' in line for line in place_source),
          'v26 Second-building main-range restriction is not source-proven')
    main_range = list(disassemble(native_method(
        'Util/BuildingUtils.luac', 'IsInMainSubRange')))
    for term in ('GetAllBuildingByItemIdWithoutPickUp', 'GetBuildingLevelTemplate',
                 'offer_range', 'IsInRangeBySquare', 'destroyStartTime'):
        check(any(term in line for line in main_range),
              'native main-sub range proof lost ' + term)
    # v26 does have a typed CollectRange exception, but only in the resource
    # placement branch carrying a ResourceType and template para4. Generic
    # FreeBuildingReplaceNew city rearrangement supplies no such positive
    # resource-placement evidence; keep this path read-only/fail-closed.
    check(any('sourceLine=530' in line and "'None'" in line for line in place_source) and
          any('sourceLine=532' in line and "'CollectRange'" in line for line in place_source) and
          any('sourceLine=538' in line and "'IsResourceByType'" in line for line in place_source) and
          any('sourceLine=537' in line and "'para4'" in line for line in place_source),
          'native CollectRange denied/typed resource exception no longer source-grounded')

    def permitted(item_id, gate_level, template_can_move):
        gate = defaultdict(lambda: None,
                           {1: {'level': gate_level}} if gate_level is not None else {})
        manager = {'GetBuildingDatasByBuildingId': lambda _self, _id: gate}
        template = {'CanMove': lambda _self: template_can_move}
        templates = {'GetBuildingDesTemplate': lambda _self, _id: template}
        game_globals = {
            'BuildingTypes': {'FUN_BUILD_MAIN': 700, 'LW_BUILD_GATE': 701},
            'DataCenter': {'BuildManager': manager, 'BuildTemplateManager': templates},
        }
        return selected.execute(native_can_move, item_id, globals_=game_globals)

    check(permitted(700, 1, True) == (False,),
          'native moving logic did not block FUN_BUILD_MAIN')
    check(permitted(11, None, True) == (False,),
          'native moving logic permitted missing military gate')
    check(permitted(11, 0, True) == (False,),
          'native moving logic permitted a level-zero military gate')
    check(permitted(13, 1, False) == (False,),
          'native moving logic ignored the building template CanMove rule')
    check(permitted(11, 1, True) == (True,),
          'native moving logic did not permit normal post-gate building')


def main():
    check_original_game_wire()
    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    snap = get('city_layout_snapshot_get')
    check(snap['state'] == 'confirmed', 'city snapshot unavailable')
    baseline = snap['data']
    check(len(baseline['cells']) == 25 and len(baseline['buildings']) == 3,
          'snapshot silently lost a building or a grid cell')
    check(baseline['isInCity'] is True and baseline['cells'][25]['road'] is True,
          'native city/road state not observed')
    check(baseline['cells'][24]['unlocked'] is False,
          'native locked land not observed')
    check(baseline['buildings'][1]['occupiedPoints'][1] == 1,
          'source native building footprint missing')
    revision = baseline['layoutRevision']
    check(get('city_layout_snapshot_get')['data']['layoutRevision'] == revision,
          'snapshot revision changed without a game-state change')

    fields = lua.globals().Fields(revision, '101:3')
    good = get('city_layout_validate', fields)
    check(good['state'] == 'confirmed' and good['data']['valid'] is True and
          good['data']['totalMoves'] == 1 and good['data']['temporaryMoves'] == 0,
          'disjoint move was not source validated')
    check(len(good['data']['issues']) == 0 and
          lua.eval('function(tab) return getmetatable(tab) and getmetatable(tab).__feature018_array end')(good['data']['issues']) is True,
          'empty validation issues must encode as []')
    stale = get('city_layout_validate', lua.globals().Fields('old-revision', '101:3'))
    check(stale['data']['valid'] is False and
          stale['data']['issues'][1]['code'] == 'CITY_LAYOUT_STALE',
          'stale snapshot was accepted')
    immovable = get('city_layout_validate', lua.globals().Fields(revision, '103:7'))
    check(immovable['data']['issues'][1]['code'] == 'CITY_LAYOUT_IMMOVABLE',
          'fixed building allowed to move')
    locked = get('city_layout_validate', lua.globals().Fields(revision, '101:24'))
    check(locked['data']['issues'][1]['code'] == 'CITY_LAYOUT_LOCKED',
          'locked land allowed')
    road = get('city_layout_validate', lua.globals().Fields(revision, '101:25'))
    check(road['data']['issues'][1]['code'] == 'CITY_LAYOUT_ROAD',
          'road placement allowed')
    check(get('city_layout_snapshot_get')['data']['cells'][23]['road'] is False,
          'world-only road false positive leaked into city placement snapshot')
    check(lua.globals().NativeCityPointScans == 0,
          'full-grid snapshot triggered v26 O(cells x buildings) CityPointManager scans')
    for point_type in ('Garbage', 'Monster', 'MonsterReward', 'Collect', 'CollectRange'):
        lua.execute(f'NativeCityPointKinds[3] = CityPointType.{point_type}')
        obstructed = get('city_layout_snapshot_get')['data']
        check(obstructed['cells'][3]['obstructed'] is True and
              obstructed['layoutRevision'] != revision,
              f'native {point_type} restriction missing from snapshot/revision')
        outcome = get('city_layout_validate', lua.globals().Fields(
            obstructed['layoutRevision'], '101:3'))
        check(outcome['data']['valid'] is False and
              outcome['data']['issues'][1]['code'] == 'CITY_LAYOUT_OBSTRUCTED',
              f'native {point_type} occupied tile was accepted')
    lua.execute('NativeCityPointKinds[3] = nil')
    check(get('city_layout_snapshot_get')['data']['layoutRevision'] == revision,
          'cleared native obstacle did not restore original revision')
    invalid = get('city_layout_validate', lua.globals().Fields(revision, '101:3;101:4'))
    check(invalid['state'] == 'unsupported', 'duplicate UUID admitted')

    lua.execute('GateLevel = 0')
    without_gate = get('city_layout_snapshot_get')['data']
    check(all(without_gate['buildings'][i]['movable'] is False for i in range(1, 4)),
          'current game military-gate restriction not reflected in city snapshot')
    rejected_gate = get('city_layout_validate', lua.globals().Fields(
        without_gate['layoutRevision'], '101:3'))
    check(rejected_gate['data']['issues'][1]['code'] == 'CITY_LAYOUT_IMMOVABLE',
          'locked military-gate state allowed city action')
    lua.execute('GateLevel = 1')
    lua.execute('DataCenter.BuildManager.allBuilding[104] = FixtureBuilding(104, 700, 7)')
    main = get('city_layout_snapshot_get')['data']
    check(next(main['buildings'][i] for i in range(1, 5)
               if main['buildings'][i]['uuid'] == '104')['movable'] is False,
          'FUN_BUILD_MAIN was marked movable despite game rule')
    lua.execute('DataCenter.BuildManager.allBuilding[104] = nil')
    check(get('city_layout_snapshot_get')['data']['layoutRevision'] == revision,
          'game-state cleanup did not restore the validation revision')

    # Exact four IsInFlagPosition entries are expressed in the 100-wide game
    # coordinate space. The snapshot reflects these only when they are in range.
    # Validate both regular building rejection and LW_BUILD_FLAG exemption.
    lua_flags, _ = fake()
    lua_flags.execute('''
      CityTileCount = 100
      for pid=26,10000 do DataCenter.CityZoneManager.allZonePoint[pid]={zone_type=1} end
      DataCenter.BuildManager.allBuilding[200] = FixtureBuilding(200, BuildingTypes.LW_BUILD_FLAG, 7)
    ''')
    action = lambda name, fields=None: lua_flags.globals().Act(name, fields)
    flag_snap_result = action('city_layout_snapshot_get')
    check(flag_snap_result['state'] == 'confirmed', '100-cell-wide source grid unavailable')
    flag_snap = flag_snap_result['data']
    check(len(flag_snap['cells']) == 10000, 'city grid wrongly constrained to 80 cells')
    check(lua_flags.globals().NativeCityPointScans == 0,
          '10,000-cell native snapshot used expensive per-cell building census')
    for pid in (5049, 5050, 4950, 4949):
        check(flag_snap['cells'][pid]['flagOnly'] is True and
              flag_snap['cells'][pid]['road'] is False,
              f'military flag-only point {pid} was misclassified')
    for pid in (4948, 5000, 5051):
        check(flag_snap['cells'][pid]['flagOnly'] is False,
              f'ordinary point {pid} was classified military flag-only')
    check(flag_snap['cells'][25]['road'] is True,
          'road cell not derived from GetPointType enum')
    flag = next(flag_snap['buildings'][i] for i in range(1, 5)
                if flag_snap['buildings'][i]['uuid'] == '200')
    check(flag['isFlag'] is True, 'LW_BUILD_FLAG was not identified by itemId')
    non_flag = action('city_layout_validate', lua_flags.globals().Fields(
        flag_snap['layoutRevision'], '101:5049'))
    check(non_flag['data']['valid'] is False and
          non_flag['data']['issues'][1]['code'] == 'CITY_LAYOUT_FLAG_ONLY',
          'non-flag building admitted into military flag-only cell')
    allowed_flag = action('city_layout_validate', lua_flags.globals().Fields(
        flag_snap['layoutRevision'], '200:5049'))
    check(allowed_flag['data']['valid'] is True,
          'military flag rejected from its designated unlocked non-road cell')
    ordinary_flag = action('city_layout_validate', lua_flags.globals().Fields(
        flag_snap['layoutRevision'], '200:8'))
    check(ordinary_flag['data']['valid'] is True,
          'military flag rejected from ordinary unlocked non-road cell')

    # v26 BuildingDate.GetRangedPoints skips offsets outside the native grid.
    # A 2x1 building at x=0 therefore yields only one occupied point; that
    # *partial* native shape must never be accepted as a 1x1 movable building.
    lua_footprint, _ = fake()
    get_footprint = lambda name, fields=None: lua_footprint.globals().Act(name, fields)
    lua_footprint.execute('''
      local original = DataCenter.BuildTemplateManager.GetBuildingDesTemplate
      DataCenter.BuildTemplateManager.GetBuildingDesTemplate = function(self, id)
        if id == 11 then return {tileX=2, tileY=1, zoneType=1,
          CanMove=function(self) return true end} end
        return original(self, id)
      end
      local one = DataCenter.BuildManager.allBuilding[101]
      one.pointId = 4
      one.GetRangedPoints = function(self)
        return {[self.pointId]=true, [self.pointId - 1]=true}
      end
    ''')
    intact = get_footprint('city_layout_snapshot_get')
    check(intact['state'] == 'confirmed' and
          next(intact['data']['buildings'][i] for i in range(1, 4)
               if intact['data']['buildings'][i]['uuid'] == '101')['tileX'] == 2,
          'legal two-tile native source footprint was rejected')
    lua_footprint.execute('''
      local one = DataCenter.BuildManager.allBuilding[101]
      one.pointId = 6 -- first column (x=0) has no left neighbour
      one.GetRangedPoints = function(self) return {[self.pointId]=true} end
    ''')
    truncated = get_footprint('city_layout_snapshot_get')
    check(truncated['state'] == 'unsupported' and
          'footprint' in truncated['error'],
          'clipped two-tile native footprint was accepted as a movable layout')
    lua_footprint.execute('''
      local one = DataCenter.BuildManager.allBuilding[101]
      one.pointId = 4
      one.GetRangedPoints = function(self)
        return {[self.pointId]=true, [self.pointId - 2]=true}
      end
    ''')
    malformed = get_footprint('city_layout_snapshot_get')
    check(malformed['state'] == 'unsupported' and
          'footprint' in malformed['error'],
          'same-count but irregular native footprint passed template geometry')

    start = get('city_layout_apply_start', fields)
    check(start['state'] == 'accepted' and start['data']['requestDispatched'] is True,
          'dispatch was falsely reported as completed')
    dispatched = lua.globals().ObservedMoves
    check(len(dispatched) == 1 and dispatched[1]['message'] == 'native_build_world_move_new' and
          dispatched[1]['uuid'] == 101 and dispatched[1]['pointId'] == 3 and
          dispatched[1]['lastIndex'] == 1,
          'official existing-building Move message/args incorrect')
    pending = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(pending['state'] == 'running' and pending['completedMoves'] == 0,
          'unconfirmed native move reported complete')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    complete = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(complete['state'] == 'completed' and complete['completedMoves'] == 1,
          'server-observed building update did not complete job')

    # A real 2289 building UUID is > 2^53. Preserve the exact source-native
    # integer through the public decimal ID lookup and original Move packet;
    # converting its decimal ID through a Lua double would silently retarget.
    lua_large, _ = fake()
    get_large = lambda name, fields=None: lua_large.globals().Act(name, fields)
    native_uuid = 1392040865799139725
    lua_large.execute('''
      local building = DataCenter.BuildManager.allBuilding[101]
      DataCenter.BuildManager.allBuilding[101] = nil
      building.uuid = 1392040865799139725
      DataCenter.BuildManager.allBuilding[1392040865799139725] = building
    ''')
    large_revision = get_large('city_layout_snapshot_get')['data']['layoutRevision']
    large_start = get_large('city_layout_apply_start',
                           lua_large.globals().Fields(large_revision,
                                                       str(native_uuid) + ':3'))
    large_packet = lua_large.globals().ObservedMoves
    check(large_start['state'] == 'accepted' and len(large_packet) == 1 and
          large_packet[1]['message'] == 'native_build_world_move_new' and
          large_packet[1]['uuid'] == native_uuid and
          large_packet[1]['pointId'] == 3 and
          large_packet[1]['lastIndex'] == 1,
          'real high owner UUID lost precision or wrong Move command was sent')

    lua_no_move, _ = fake()
    get_no_move = lambda name, fields=None: lua_no_move.globals().Act(name, fields)
    no_move_revision = get_no_move('city_layout_snapshot_get')['data']['layoutRevision']
    lua_no_move.execute('MsgDefines.BuildWorldMoveNew = nil')
    missing_move = get_no_move('city_layout_apply_start',
                               lua_no_move.globals().Fields(no_move_revision, '101:3'))
    check(missing_move['state'] == 'failed' and
          missing_move['errorCode'] == 'GAME_METHOD_UNAVAILABLE' and
          len(lua_no_move.globals().ObservedMoves) == 0,
          'missing original Move wire silently fell back to unsafe Replace')

    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    swap = lua.globals().Fields(revision, '101:2;102:1')
    plan = get('city_layout_validate', swap)['data']
    check(plan['valid'] is True and plan['totalMoves'] == 3 and
          plan['temporaryMoves'] == 1, 'occupied swap lacked a real empty buffer')
    accepted = get('city_layout_apply_start', swap)
    check(accepted['state'] == 'accepted', 'safe swap job not dispatched')
    expected = [(101, 3, 1), (102, 1, 2), (101, 2, 3)]
    for i, (uid, to_point, old_point) in enumerate(expected, start=1):
        observed = lua.globals().ObservedMoves[i]
        check(observed['uuid'] == uid and observed['pointId'] == to_point,
              'swap order did not preserve occupied cells')
        check(observed['lastIndex'] == old_point and
              observed['message'] == 'native_build_world_move_new',
              'swap Move sender/rollback anchor mismatched original City action')
        check(get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']['completedMoves'] == i-1,
              'step confirmed without authoritative update')
        lua.execute(f'DataCenter.BuildManager.allBuilding[{uid}].pointId={to_point}')
        updated = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
        check(updated['completedMoves'] == i, 'game step ACK not observed')
    check(updated['state'] == 'completed', 'server-observed swap not completed')

    # Current v26 IsCanPutDownByPoint only permits BuildType.Second placements
    # within a *live main building's* IsInMainSubRange(pointId, sup_main_build_id)
    # offer_range. A valid region/zone alone cannot bypass this restriction.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    lua.execute('''
      BuildType = {Second=2}
      local second = DataCenter.BuildTemplateManager:GetBuildingDesTemplate(11)
      second.build_type = BuildType.Second
      second.sup_main_build_id = 700
      AllowedMainSub = {[1]=true,[2]=true,[4]=true}
      BuildingUtils.IsInMainSubRange = function(pointId, mainId)
        assert(mainId == 700)
        return AllowedMainSub[pointId] == true
      end
    ''')
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    source_ok = get('city_layout_validate', lua.globals().Fields(revision,'101:4'))['data']
    check(source_ok['valid'] is True, 'valid native Second main-range placement rejected')
    outside = get('city_layout_validate', lua.globals().Fields(revision,'101:3'))['data']
    check(outside['valid'] is False and outside['issues'][1]['code'] == 'CITY_LAYOUT_ZONE',
          'Second building incorrectly accepted outside source main-building range')
    denied = get('city_layout_apply_start', lua.globals().Fields(revision,'101:3'))
    check(denied['state'] == 'confirmed' and denied['data']['accepted'] is False and
          len(lua.globals().ObservedMoves) == 0,
          'Second range denied placement dispatched official move')
    # During a swap, the first candidate buffer is 3; only the main-supported
    # buffer 4 may be used, and every ACK must gate the next source-checked send.
    plan = get('city_layout_validate',lua.globals().Fields(revision,'101:2;102:1'))['data']
    check(plan['valid'] is True and plan['totalMoves'] == 3,
          'main-supported alternate swap buffer was not found')
    swap = get('city_layout_apply_start',lua.globals().Fields(revision,'101:2;102:1'))
    check(swap['state'] == 'accepted' and lua.globals().ObservedMoves[1]['pointId'] == 4,
          'buffer planner selected prohibited secondary buffer instead of 4')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=4')
    producer.Pump()
    check(lua.globals().ObservedMoves[2]['uuid'] == 102 and
          lua.globals().ObservedMoves[2]['pointId'] == 1,
          'secondary range swap did not advance after original native ACK')
    lua.execute('DataCenter.BuildManager.allBuilding[102].pointId=1')
    lua.execute('AllowedMainSub[2] = nil')
    producer.Pump()
    stopped = get('city_layout_apply_status',lua.globals().Fields(revision,''))['data']
    check(stopped['state'] == 'failed' and stopped['errorCode'] == 'CITY_LAYOUT_STALE' and
          len(lua.globals().ObservedMoves) == 2,
          'secondary main range changed but last swap packet was still sent')
    lua.execute('AllowedMainSub[2] = true')
    snap_recovered = get('city_layout_snapshot_get')['data']
    check(snap_recovered['layoutRevision'] != revision,
          'authoritative revision did not update after an acknowledged secondary move')
    lua.execute('DataCenter.BuildManager.allBuilding[103].level = 7')
    check(get('city_layout_snapshot_get')['data']['layoutRevision'] !=
          snap_recovered['layoutRevision'],
          'native building level/range dependency changed without City revision')

    # A destination occupied by a stationary building is a final-layout
    # collision. The planner must reject it as OVERLAP before considering any
    # temporary buffer, and must never dispatch an official replacement.
    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    stationary_overlap = lua.globals().Fields(revision, '101:6')
    collision = get('city_layout_validate', stationary_overlap)
    check(collision['data']['valid'] is False and
          any(collision['data']['issues'][i]['code'] == 'CITY_LAYOUT_OVERLAP'
              for i in range(1, len(collision['data']['issues']) + 1)),
          'moving onto unchanged building did not return final-layout OVERLAP')
    denied = get('city_layout_apply_start', stationary_overlap)
    check(denied['state'] == 'confirmed' and denied['data']['accepted'] is False and
          len(lua.globals().ObservedMoves) == 0,
          'stationary occupancy conflict triggered a native move')

    # One acknowledged point change is insufficient to claim a successful
    # whole-layout result when another building moved during that ACK wait.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    check(get('city_layout_apply_start', lua.globals().Fields(revision, '101:3'))
          ['state'] == 'accepted', 'third-party drift scenario was not dispatched')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    lua.execute('DataCenter.BuildManager.allBuilding[102].pointId=4')
    producer.Pump()
    drift = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(drift['state'] == 'failed' and drift['errorCode'] == 'CITY_LAYOUT_STALE' and
          drift['completedMoves'] == 0 and len(lua.globals().ObservedMoves) == 1,
          'whole-layout completion ignored unrelated authoritative building drift')

    # Original BuildingDate.GetRangedPoints is cached on both itemId and
    # pointId. A point-only ACK can be a false success if an independent
    # server update changed the item/footprint during the in-flight move.
    for kind in ('item', 'footprint'):
        lua, producer = fake()
        get = lambda name, fields=None: lua.globals().Act(name, fields)
        revision = get('city_layout_snapshot_get')['data']['layoutRevision']
        check(get('city_layout_apply_start', lua.globals().Fields(
            revision, '101:3'))['state'] == 'accepted',
              f'{kind} ACK-drift fixture failed to start')
        lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
        if kind == 'item':
            lua.execute('DataCenter.BuildManager.allBuilding[101].itemId=12')
        else:
            lua.execute('''DataCenter.BuildManager.allBuilding[101].GetRangedPoints =
                function(self) return {[self.pointId]=true, [self.pointId+1]=true} end''')
        producer.Pump()
        stale = get('city_layout_apply_status',
                    lua.globals().Fields(revision, ''))['data']
        check(stale['state'] == 'failed' and stale['errorCode'] == 'CITY_LAYOUT_STALE' and
              stale['completedMoves'] == 0 and len(lua.globals().ObservedMoves) == 1,
              f'{kind} changed but pointId-only ACK was called completed')

    # The game pump must continue an owner-authorized move plan even when
    # the City panel is hidden and never issues another status request.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    pending_swap = get('city_layout_apply_start',
                       lua.globals().Fields(revision, '101:2;102:1'))
    check(pending_swap['state'] == 'accepted', 'tick-only swap did not begin')
    for index, (uid, target) in enumerate([(101, 3), (102, 1), (101, 2)], start=1):
        check(len(lua.globals().ObservedMoves) == index,
              'game tick dispatched a future move before server confirmation')
        producer.Pump()
        check(len(lua.globals().ObservedMoves) == index,
              'game tick treated an unconfirmed request as a completed move')
        lua.execute(f'DataCenter.BuildManager.allBuilding[{uid}].pointId={target}')
        producer.Pump()
    finished = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(finished['state'] == 'completed' and finished['completedMoves'] == 3,
          'game tick did not finish a multi-step job without UI polling')

    # Cancellation cannot undo an already sent UI-equivalent packet. It must
    # wait for that one ACK but *must not* dispatch steps 2 or 3, and must
    # faithfully report the one authoritative partial move, not completion.
    for cancel_after in (1, 2):
        lua, producer = fake()
        get = lambda name, fields=None: lua.globals().Act(name, fields)
        revision = get('city_layout_snapshot_get')['data']['layoutRevision']
        started = get('city_layout_apply_start', lua.globals().Fields(
            revision, '101:2;102:1'))
        check(started['state'] == 'accepted', 'multistep cancel fixture failed to start')
        if cancel_after == 2:
            lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
            producer.Pump()
            check(len(lua.globals().ObservedMoves) == 2,
                  'second swap packet was not sent before controlled cancellation')
        pending = get('city_layout_apply_cancel', lua.table_from({
            'profileId': 'p1', 'launchSessionId': 'session1', 'challenge': 'nonce',
            'jobId': started['data']['status']['jobId'],
        }))
        check(pending['data']['status']['state'] == 'cancelling',
              'in-flight multistep cancel was incorrectly called completed')
        pending_count = len(lua.globals().ObservedMoves)
        producer.Pump()
        check(len(lua.globals().ObservedMoves) == pending_count,
              'cancel dispatched later swap step without receiving ACK')
        if cancel_after == 1:
            lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
        else:
            lua.execute('DataCenter.BuildManager.allBuilding[102].pointId=1')
        producer.Pump()
        stopped = get('city_layout_apply_status',
                      lua.globals().Fields(revision, ''))['data']
        check(stopped['state'] == 'cancelled' and
              stopped['completedMoves'] == cancel_after and
              stopped['totalMoves'] == 3 and
              len(lua.globals().ObservedMoves) == pending_count,
              f'cancel after ACK {cancel_after} dispatched a future move or hid partial poststate')
        expected_state = {101: 3, 102: 2 if cancel_after == 1 else 1}
        for uid, actual in expected_state.items():
            check(lua.globals().DataCenter.BuildManager.allBuilding[uid]['pointId'] == actual,
                  f'cancel after ACK {cancel_after} lost authoritative partial layout')

    # Each future send still re-reads the native placement conditions.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    start = get('city_layout_apply_start',
                lua.globals().Fields(revision, '101:2;102:1'))
    check(start['state'] == 'accepted', 'zone-drift swap did not begin')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    lua.execute('DataCenter.CityZoneManager.allZonePoint[1] = nil')
    producer.Pump()
    stale = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(stale['state'] == 'failed' and stale['errorCode'] == 'CITY_LAYOUT_STALE' and
          len(lua.globals().ObservedMoves) == 1,
          'game tick dispatched move despite changed native zone permission')

    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    start = get('city_layout_apply_start', lua.globals().Fields(revision, '101:3'))
    cancelled = get('city_layout_apply_cancel', lua.table_from({
        'profileId':'p1','launchSessionId':'session1','challenge':'nonce',
        'jobId':start['data']['status']['jobId']}))
    check(cancelled['state'] == 'confirmed' and
          cancelled['data']['status']['state'] == 'cancelling',
          'inflight cancel did not visibly acknowledge pending server result')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    outcome = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(outcome['state'] == 'cancelled', 'cancel did not stop additional dispatch')
    check(len(lua.globals().ObservedMoves) == 1, 'cancel dispatched an extra move')

    # v26 BuildWorldMoveNew OnConfirm sends an asynchronous packet and the
    # only authoritative reply is the later BuildManager handler. A 15-second
    # timeout does not mean the server rejected it: without a retry fence, a
    # user can dispatch another replacement against a still-in-flight packet.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    lua.execute('Clock = 100; os.time = function() return Clock end')
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    plan = lua.globals().Fields(revision, '101:2;102:1')
    started = get('city_layout_apply_start', plan)
    check(started['state'] == 'accepted' and len(lua.globals().ObservedMoves) == 1,
          'ambiguous timeout fixture did not dispatch its original packet')
    lua.execute('Clock = 116')
    timed_out = get('city_layout_apply_status',
                    lua.globals().Fields(revision, ''))['data']
    check(timed_out['state'] == 'failed' and
          timed_out['errorCode'] == 'GAME_MOVE_RESULT_TIMEOUT' and
          timed_out['completedMoves'] == 0,
          'unconfirmed native packet was not classified as timed out')
    rejected_retry = get('city_layout_apply_start', plan)
    check(rejected_retry['state'] == 'failed' and
          rejected_retry['errorCode'] == 'CITY_LAYOUT_RESULT_UNCERTAIN' and
          len(lua.globals().ObservedMoves) == 1,
          'timed-out but unresolved native packet allowed a conflicting retry')
    producer.Pump()
    check(len(lua.globals().ObservedMoves) == 1,
          'timed-out job dispatched a future swap packet without proof')
    # Even a terminal-status cancellation must not unlock uncertain sends.
    cancelled = get('city_layout_apply_cancel', lua.table_from({
        'profileId':'p1','launchSessionId':'session1','challenge':'nonce',
        'jobId':started['data']['status']['jobId'],
    }))
    check(cancelled['state'] == 'confirmed' and
          get('city_layout_apply_start', plan)['errorCode'] ==
          'CITY_LAYOUT_RESULT_UNCERTAIN',
          'cancellation of timed-out native packet wrongly unlocked retry')
    # A late official BuildManager result releases only the ambiguity fence.
    # It is partial completion, never permission to continue the old swap.
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId = 3')
    producer.Pump()
    recovered = get('city_layout_apply_status',
                    lua.globals().Fields(revision, ''))['data']
    check(recovered['state'] == 'failed' and
          recovered['completedMoves'] == 1 and
          len(lua.globals().ObservedMoves) == 1,
          'late server ACK either vanished or resumed an expired swap')
    new_revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    safe_retry = get('city_layout_apply_start',
                     lua.globals().Fields(new_revision, '101:4'))
    check(safe_retry['state'] == 'accepted' and
          len(lua.globals().ObservedMoves) == 2 and
          lua.globals().ObservedMoves[2]['pointId'] == 4,
          'reconciled late ACK never released the retry fence')

    # A third-party/manual destination while our request is unresolved is
    # also ambiguous: do not treat the observed foreign point as proof that
    # this original pending server packet was rejected/cannot arrive later.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    check(get('city_layout_apply_start', lua.globals().Fields(
        revision, '101:3'))['state'] == 'accepted',
        'foreign-position timeout fixture could not start')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId = 4')
    producer.Pump()
    stale = get('city_layout_apply_status',
                lua.globals().Fields(revision, ''))['data']
    check(stale['state'] == 'failed' and stale['errorCode'] == 'CITY_LAYOUT_STALE',
          'third-party move was not diagnosed as stale')
    fresh = get('city_layout_snapshot_get')['data']['layoutRevision']
    retry = get('city_layout_apply_start',lua.globals().Fields(fresh,'101:5'))
    check(retry['state'] == 'failed' and
          retry['errorCode'] == 'CITY_LAYOUT_RESULT_UNCERTAIN' and
          len(lua.globals().ObservedMoves) == 1,
          'foreign-position state allowed a new move while old packet remains uncertain')

    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    lua.execute('SFSNetwork.SendMessage = function() return false end')
    refused = get('city_layout_apply_start', lua.globals().Fields(revision, '101:3'))
    check(refused['state'] == 'failed' and
          refused['errorCode'] == 'GAME_MOVE_DISPATCH_FAILED',
          'native SendMessage false return was mistaken for a dispatched request')
    check(get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']['state'] == 'failed',
          'declined native send left a false-positive running job')

    # R3 live 2289 receipt 062112: start accepted/requestDispatched, but a
    # subsequent status was idle with no BuildManager point change. The
    # periodic host gate can briefly return nil, causing CleanupAfterLeaseLoss.
    # A missing owner token does NOT prove that the in-flight server packet
    # was rejected; preserve its uncertainty fence and never send step two.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    plan = lua.globals().Fields(revision, '101:2;102:1')
    started = get('city_layout_apply_start', plan)
    check(started['state'] == 'accepted', 'owner-loss fixture never sent first native move')
    check(len(lua.globals().ObservedMoves) == 1 and
          lua.globals().ObservedMoves[1]['pointId'] == 3,
          'fixture did not actually have a sent first swap packet')
    producer.CleanupAfterLeaseLoss()
    tombstone = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(tombstone['state'] == 'failed' and
          tombstone['errorCode'] == 'CITY_LAYOUT_OWNER_LOSS_UNCERTAIN' and
          tombstone['completedMoves'] == 0 and
          tombstone['totalMoves'] == 3,
          'transient owner loss erased pending native packet or falsely confirmed it')
    reject = get('city_layout_apply_start', plan)
    check(reject['state'] == 'failed' and
          reject['errorCode'] == 'CITY_LAYOUT_RESULT_UNCERTAIN' and
          len(lua.globals().ObservedMoves) == 1,
          'missing owner token allowed a second potentially conflicting send')
    # A newly admitted owner may not read the old job's private details, but
    # cannot send while the old packet has an unknown server outcome.
    different_owner = lua.globals().Fields(revision, '101:4')
    different_owner['profileId'] = 'another-owner'
    check(get('city_layout_apply_status', different_owner)['data']['state'] == 'idle',
          'successor read private job details from previous owner')
    check(get('city_layout_apply_start', different_owner)['errorCode'] ==
          'CITY_LAYOUT_RESULT_UNCERTAIN',
          'successor sent while prior owner had unresolved native packet')
    producer.CleanupAfterLeaseLoss()
    producer.Pump()
    check(len(lua.globals().ObservedMoves) == 1,
          'second transient nil owner token advanced an abandoned swap')
    # Native point confirmation can release the fence without resuming stale
    # steps 2 and 3. It remains a partial failed result for the old owner.
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    producer.Pump()
    recovered = get('city_layout_apply_status',
                    lua.globals().Fields(revision, ''))['data']
    check(recovered['state'] == 'failed' and
          recovered['errorCode'] == 'CITY_LAYOUT_OWNER_LOSS_RECONCILED' and
          recovered['completedMoves'] == 1 and
          len(lua.globals().ObservedMoves) == 1,
          'late ACK after owner loss was lost or sent remainder of stale swap')
    fresh_revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    new_plan = lua.globals().Fields(fresh_revision, '101:4')
    allowed = get('city_layout_apply_start', new_plan)
    check(allowed['state'] == 'accepted' and
          len(lua.globals().ObservedMoves) == 2 and
          lua.globals().ObservedMoves[2]['pointId'] == 4,
          'confirmed late ACK never permitted a separately revalidated move')

    # Owner loss without any outstanding network packet may fully retire
    # the completed job; there is no ambiguity to preserve.
    lua, producer = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    producer.CleanupAfterLeaseLoss()
    check(get('city_layout_apply_status', lua.globals().Fields('', ''))['data']['state'] ==
          'idle', 'cleanup of idle module created an erroneous job tombstone')
    print('FEATURE018_CITY_GAME_LUA53_OFFLINE_PASS')


if __name__ == '__main__':
    main()
