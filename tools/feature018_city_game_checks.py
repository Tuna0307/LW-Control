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
CityPointType = {Empty = 0, Road = 42}
GateLevel = 1
local flagPositions = {[5049]=true, [5050]=true, [4950]=true, [4949]=true}
BuildingUtils = {
    IsInFlagPosition = function(id) return flagPositions[id] == true end,
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
CS = {SceneManager = {World = {GetPointType = function(self, pointId)
    if pointId == 25 then return CityPointType.Road end
    return CityPointType.Empty
end}}}
local moves = {}
ObservedMoves = moves
SFSNetwork = {SendMessage = function(message, args)
    moves[#moves+1] = {message = message, uuid = args.buildUuid, pointId = args.pointId}
end}
MsgDefines = {FreeBuildingReplaceNew = 'native_free_building_replace_new'}
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
    selected.execute(message, {'sfsObj': sfs},
                     {'buildUuid': 9007199254740991, 'pointId': 15},
                     globals_={'OnCreate': lambda _: None})
    check(captured == [('long', 'buildUuid', 9007199254740991),
                       ('int', 'pointId', 15)],
          'native current-client move serializer differs from adapter request')

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
                     'GetPointType', 'CityPointType', 'Road'):
        check(any(expected in instruction for instruction in place_source),
              'native point-placement flag/road rule missing: ' + expected)

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

    start = get('city_layout_apply_start', fields)
    check(start['state'] == 'accepted' and start['data']['requestDispatched'] is True,
          'dispatch was falsely reported as completed')
    dispatched = lua.globals().ObservedMoves
    check(len(dispatched) == 1 and dispatched[1]['message'] == 'native_free_building_replace_new' and
          dispatched[1]['uuid'] == 101 and dispatched[1]['pointId'] == 3,
          'official FreeBuildingReplaceNew message/args incorrect')
    pending = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(pending['state'] == 'running' and pending['completedMoves'] == 0,
          'unconfirmed native move reported complete')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    complete = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(complete['state'] == 'completed' and complete['completedMoves'] == 1,
          'server-observed building update did not complete job')

    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    swap = lua.globals().Fields(revision, '101:2;102:1')
    plan = get('city_layout_validate', swap)['data']
    check(plan['valid'] is True and plan['totalMoves'] == 3 and
          plan['temporaryMoves'] == 1, 'occupied swap lacked a real empty buffer')
    accepted = get('city_layout_apply_start', swap)
    check(accepted['state'] == 'accepted', 'safe swap job not dispatched')
    expected = [(101, 3), (102, 1), (101, 2)]
    for i, (uid, to_point) in enumerate(expected, start=1):
        observed = lua.globals().ObservedMoves[i]
        check(observed['uuid'] == uid and observed['pointId'] == to_point,
              'swap order did not preserve occupied cells')
        check(get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']['completedMoves'] == i-1,
              'step confirmed without authoritative update')
        lua.execute(f'DataCenter.BuildManager.allBuilding[{uid}].pointId={to_point}')
        updated = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
        check(updated['completedMoves'] == i, 'game step ACK not observed')
    check(updated['state'] == 'completed', 'server-observed swap not completed')

    lua, _ = fake()
    get = lambda name, fields=None: lua.globals().Act(name, fields)
    revision = get('city_layout_snapshot_get')['data']['layoutRevision']
    start = get('city_layout_apply_start', lua.globals().Fields(revision, '101:3'))
    cancelled = get('city_layout_apply_cancel', lua.table_from({
        'profileId':'p1','launchSessionId':'session1','challenge':'nonce',
        'jobId':start['data']['status']['jobId']}))
    check(cancelled['state'] == 'confirmed', 'inflight cancel was rejected')
    lua.execute('DataCenter.BuildManager.allBuilding[101].pointId=3')
    outcome = get('city_layout_apply_status', lua.globals().Fields(revision, ''))['data']
    check(outcome['state'] == 'cancelled', 'cancel did not stop additional dispatch')
    check(len(lua.globals().ObservedMoves) == 1, 'cancel dispatched an extra move')

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
    print('FEATURE018_CITY_GAME_LUA53_OFFLINE_PASS')


if __name__ == '__main__':
    main()
