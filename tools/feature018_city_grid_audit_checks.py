"""Independent v26 city-grid/zone/road/flag audit, entirely offline.

Loads the same original-package hash-pinned Lua bytecode as the City harness,
and exercises the bridge module only in isolated Lua 5.3 fake managers.
No game sessions, installed mutations, or network activity.
"""
from __future__ import annotations

import hashlib
from pathlib import Path

from feature018_city_game_checks import (  # noqa: E402
    ORIGINAL_ENTRY_SHA256, bytecode, fake, probe, root_methods, disassemble,
    selected,
)


def expect(condition, why):
    assert condition, 'FEATURE018_CITY_V26_GRID_AUDIT: ' + why


def exact_native_methods():
    version, content, records = probe.read_lwlf(probe.paths()['data'])
    files = dict(records)
    original = files.get(probe.ORIGINAL_LUA_ENTRY, files.get(probe.LUA_ENTRY))
    expect((version, content) == (3, 26) and
           hashlib.sha256(original).hexdigest() == ORIGINAL_ENTRY_SHA256,
           'original-game v26 provenance changed')

    def native(module, method):
        root = bytecode.parse_chunk(probe.decode_lenc(files[module]))
        path = root_methods(root)[method]
        for child in path.split('/')[1:]:
            root = root['children'][int(child)]
        return root

    return native


def bytecode_grid_tests(native):
    scene = 'Util/SceneUtils.luac'
    to_xy = native(scene, 'IndexToTilePos')
    to_id = native(scene, 'TileXYToIndex')
    expect(to_xy['line'] == 244 and to_id['line'] == 303,
           'native point-coordinate method provenance changed')
    source_xy = list(disassemble(to_xy))
    source_id = list(disassemble(to_id))
    for term in ('MOD', 'floor', 'CityTileCount'):
        expect(any(term in line for line in source_xy), 'IndexToTilePos lost ' + term)
    for term in ('MUL', 'ADD', 'CityTileCount'):
        expect(any(term in line for line in source_id), 'TileXYToIndex lost ' + term)

    global_vars = {
        'WorldTileCount': 1000,
        'CityTileCount': 100,
        'ForceChangeScene': {'World': 'world', 'City': 'city'},
    }
    # The game computes x/y from the 1-based pointId, not from a zero-based
    # incoming message ID. ForceChangeScene.City removes scene ambiguity.
    for x, y, point_id in [
        (0, 0, 1), (99, 0, 100), (0, 1, 101),
        (48, 49, 4949), (49, 49, 4950),
        (48, 50, 5049), (49, 50, 5050),
        (99, 99, 10000),
    ]:
        result = selected.execute(to_id, x, y, 'city', globals_=global_vars,
                                  upvalues={1: True})
        expect(result == (point_id,), f'v26 TileXYToIndex ({x},{y}) differed')
    for x, y in [(-1, 0), (100, 0), (0, 100), (0, -1)]:
        expect(selected.execute(to_id, x, y, 'city', globals_=global_vars,
                                upvalues={1: True}) == (0,),
               f'v26 accepted out-of-grid ({x},{y})')

    footprint = native('DataCenter/BuildManager/BuildingDate.luac', 'GetRangedPoints')
    source = list(disassemble(footprint))
    expect(any('sourceLine=491' in line and 'GetIndexByOffset' in line for line in source),
           'native building footprint stopped using GetIndexByOffset')
    expect(sum('sourceLine=491' in line and 'UNM' in line for line in source) == 2,
           'native footprint no longer uses negative X/Y offsets')

    flags = native('Util/BuildingUtils.luac', 'IsInFlagPosition')
    flag_ids = {v for v in flags['constants'] if isinstance(v, int) and
                not isinstance(v, bool) and v >= 4900 and v < 5100}
    expect(flag_ids == {4949, 4950, 5049, 5050}, 'native flag IDs changed')

    placement = native('Util/BuildingUtils.luac', 'IsCanPutDownByPoint')
    placement_source = list(disassemble(placement))
    for term in ('CanPutBuildByPoint', 'CanBuildByPointId', 'IsInFlagPosition',
                 'LW_BUILD_FLAG', 'GetPointType', 'CityPointType', 'Road'):
        expect(any(term in line for line in placement_source),
               'native placement restriction missing ' + term)

    city_zone = native('DataCenter/CityZoneManager/CityZoneManager.luac',
                       'CanPutBuildByPoint')
    land_lock = native('DataCenter/LandLock/LandLockManager.luac', 'CanBuildByPointId')
    zone_template = native('DataCenter/CityZoneManager/CityZoneTemplate.luac',
                           'CanPutByZoneType')
    expect('CanPutByZoneType' in str(city_zone['constants']),
           'CityZoneManager does not delegate to per-point zone templates')
    expect('GetNoPutCountByXY' in str(land_lock['constants']),
           'LandLockManager lost independent no-placement count')
    expect({'No', 'All', 'SeasonBuild'} <= set(zone_template['constants']),
           'native zone no/all/season policy changed')

    types = {'No': 0, 'All': 1, 'SeasonBuild': 2, 'House': 3}
    for available, building, expected in [
        (0, 0, False), (0, 1, False), (1, 1, True),
        (1, 2, False), (1, 3, True), (3, 3, True),
        (3, 1, True), (3, 2, False),
    ]:
        result = selected.execute(zone_template, {'zone_type': available},
                                  building, globals_={'BuildZoneType': types})
        expect(result == (expected,),
               f'native zone {available} against building {building} differed')


def module_grid_tests():
    lua, _ = fake()
    act = lambda cmd, arg=None: lua.globals().Act(cmd, arg)
    snapshot = lambda: act('city_layout_snapshot_get')['data']
    fields = lambda revision, placement: lua.globals().Fields(revision, placement)
    base = snapshot()

    # Zone membership and land-lock status come from different native sources.
    lua.execute('DataCenter.CityZoneManager.allZonePoint[3] = nil')
    missing_zone = snapshot()
    expect(missing_zone['cells'][3]['unlocked'] is True,
           'cell without zone was falsely classified as locked land')
    expect(missing_zone['layoutRevision'] != base['layoutRevision'],
           'zone removal left original layoutRevision valid')
    denied = act('city_layout_validate', fields(missing_zone['layoutRevision'], '101:3'))
    expect(denied['data']['issues'][1]['code'] == 'CITY_LAYOUT_ZONE',
           'source-denied zone mislabeled LOCKED')
    expect(act('city_layout_apply_start', fields(missing_zone['layoutRevision'], '101:3'))
           ['data']['accepted'] is False and len(lua.globals().ObservedMoves) == 0,
           'zone-denied target dispatched a server move')

    lua.execute('DataCenter.CityZoneManager.allZonePoint[3] = {zone_type=1}')
    restored = snapshot()
    expect(restored['layoutRevision'] == base['layoutRevision'],
           'reinstated zone did not restore deterministic revision')
    lua.execute('DataCenter.CityZoneManager.allZonePoint[3].zone_type = 2')
    rezone = snapshot()
    expect(rezone['layoutRevision'] != base['layoutRevision'],
           'zone type change did not invalidate a queued layout revision')
    lua.execute('''DataCenter.CityZoneManager.CanPutBuildByPoint = function(self, pid, desired)
        local point = self.allZonePoint[pid]
        return point ~= nil and (desired == 1 or point.zone_type == desired)
    end''')
    # The source All zone semantics explicitly permit build zone type All on
    # non-No template zones. The original fake template uses zone type 1.
    expect(act('city_layout_validate', fields(rezone['layoutRevision'], '101:3'))
           ['data']['valid'] is True,
           'source-compatible All zone was disallowed')
    lua.execute('DataCenter.CityZoneManager.allZonePoint[3].zone_type = 0')
    lua.execute('''DataCenter.CityZoneManager.CanPutBuildByPoint = function(self, pid, desired)
        local point = self.allZonePoint[pid]
        return point ~= nil and point.zone_type ~= 0 and
           (desired == 1 or point.zone_type == desired)
    end''')
    no_zone = snapshot()
    expect(act('city_layout_validate', fields(no_zone['layoutRevision'], '101:3'))
           ['data']['issues'][1]['code'] == 'CITY_LAYOUT_ZONE',
           'native No zone accepted a building')

    lua.execute('GateLevel = 0')
    gate_off = snapshot()
    expect(gate_off['layoutRevision'] != no_zone['layoutRevision'],
           'movement permission changed but layoutRevision did not')
    expect(act('city_layout_validate', fields(gate_off['layoutRevision'], '101:3'))
           ['data']['issues'][1]['code'] == 'CITY_LAYOUT_IMMOVABLE',
           'gate-locked building was movable')
    lua.execute('GateLevel = 1')
    expect(snapshot()['layoutRevision'] == no_zone['layoutRevision'],
           'restored game movement permission changed same-state revision')

    lua.execute('DataCenter.CityZoneManager.allZonePoint[3] = {zone_type=1}')
    lua.execute('''CS.SceneManager.World.GetPointType = function(self, id)
                 if id == 3 then return CityPointType.Road end
                 return CityPointType.Empty end''')
    road = snapshot()
    expect(road['cells'][3]['road'] is True and
           act('city_layout_validate', fields(road['layoutRevision'], '101:3'))
           ['data']['issues'][1]['code'] == 'CITY_LAYOUT_ROAD',
           'v26 road enum not consulted for target placement')

    # BuildingDate.GetRangedPoints starts at the building's high-corner
    # anchor, subtracting offsets. Test the bridge against a 2x1 real shape.
    lua, _ = fake()
    lua.execute('''
        DataCenter.BuildManager.allBuilding[101].pointId = 4
        DataCenter.BuildManager.allBuilding[101].GetRangedPoints = function(self)
            return {[self.pointId] = true, [self.pointId - 1] = true}
        end
    ''')
    act = lambda cmd, arg=None: lua.globals().Act(cmd, arg)
    wide = act('city_layout_snapshot_get')['data']
    wide_101 = next(wide['buildings'][i] for i in range(1, 4)
                    if wide['buildings'][i]['uuid'] == '101')
    expect([wide_101['occupiedPoints'][i] for i in range(1, 3)] == [3, 4],
           'native high-corner building anchor lost its negative-X footprint')
    allowed = act('city_layout_validate', lua.globals().Fields(
        wide['layoutRevision'], '101:5'))
    expect(allowed['data']['valid'] is True,
           '2x1 high-corner building could not translate inside its row')
    row_wrap = act('city_layout_validate', lua.globals().Fields(
        wide['layoutRevision'], '101:6'))
    expect(row_wrap['data']['issues'][1]['code'] == 'CITY_LAYOUT_OUTSIDE',
           'native footprint illegally wrapped from first column to prior row')

    lua, _ = fake()
    lua.execute('''CityTileCount=100
        for pid=26,10000 do
          DataCenter.CityZoneManager.allZonePoint[pid]={zone_type=1}
        end
        DataCenter.BuildManager.allBuilding[200] = FixtureBuilding(200,
          BuildingTypes.LW_BUILD_FLAG, 7)
    ''')
    act = lambda cmd, arg=None: lua.globals().Act(cmd, arg)
    state = act('city_layout_snapshot_get')['data']
    expect(len(state['cells']) == 10000, '100-wide native grid not represented')
    for pid in (4949, 4950, 5049, 5050):
        expect(state['cells'][pid]['flagOnly'] is True,
               f'exact v26 special flag point {pid} not recognized')
        denied = act('city_layout_validate', lua.globals().Fields(
            state['layoutRevision'], '101:' + str(pid)))
        expect(denied['data']['issues'][1]['code'] == 'CITY_LAYOUT_FLAG_ONLY',
               f'non-flag allowed at special point {pid}')
        admitted = act('city_layout_validate', lua.globals().Fields(
            state['layoutRevision'], '200:' + str(pid)))
        expect(admitted['data']['valid'] is True,
               f'native flag blocked from its special point {pid}')


if __name__ == '__main__':
    native = exact_native_methods()
    bytecode_grid_tests(native)
    module_grid_tests()
    print('FEATURE018_CITY_V26_GRID_AUDIT_PASS')
