"""Read-only City candidate enumeration checks against pinned v26 predicates.

The producer's moveCandidates are *suggestions* tied to the snapshot revision,
not an ACK or authorization to dispatch the native FreeBuildingReplaceNew wire.
"""
from __future__ import annotations

from feature018_city_game_checks import fake, check_original_game_wire


def check(value, message):
    assert value, 'FEATURE018_CITY_CANDIDATE: ' + message


def install_radius(lua, allowed='true'):
    lua.execute(f'''
      BuildingUtils.IsInMyBaseCircleRange =
          function(pointId, radius) return radius == 9 and ({allowed}) end
      DataCenter.BoardManager.GetBuildMaxRadius =
          function(self) return 9 end
    ''')


def snapshot(lua):
    result = lua.globals().Act('city_layout_snapshot_get')
    check(result['state'] == 'confirmed', 'native snapshot not confirmed')
    return result['data']


def candidates(state):
    return [state['moveCandidates'][i]
            for i in range(1, len(state['moveCandidates']) + 1)]


def main():
    check_original_game_wire()
    lua, _ = fake()
    install_radius(lua)
    baseline = snapshot(lua)
    recommendations = candidates(baseline)
    check(len(recommendations) >= 2, 'free source-admitted cells missing')
    check(baseline['moveCandidateSearch']['movableBuildings'] == 2 and
          baseline['moveCandidateSearch']['searchedBuildings'] == 2 and
          baseline['moveCandidateSearch']['nativeRangeAvailable'] is True,
          'candidate search did not expose bounded real native availability')
    check(len(lua.globals().ObservedMoves) == 0,
          'read-only candidate search sent a game move')
    frozen = [(a['uuid'], a['fromPointId'], a['targetPointId'], a['distance'])
              for a in recommendations]
    check(frozen == [(a['uuid'], a['fromPointId'], a['targetPointId'], a['distance'])
                     for a in candidates(snapshot(lua))],
          'unchanged current city generated nondeterministic candidate order')
    check(snapshot(lua)['layoutRevision'] == baseline['layoutRevision'],
          'candidate search changed native layout revision')
    occupied = {1, 2, 6}
    check(all(a['targetPointId'] not in occupied and
              a['targetPointId'] != a['fromPointId']
              for a in recommendations), 'occupied/current cell suggested')

    # These cells are geometrically valid, but the exact native zone manager
    # permits only point 3. The snapshot must yield that *source-checked* target
    # rather than wasting a series of speculative validation requests.
    lua.execute('''
      DataCenter.CityZoneManager.CanPutBuildByPoint = function(self, id, zone)
          return id == 3 and self.allZonePoint[id] ~= nil and zone == 1
      end
    ''')
    zoned = snapshot(lua)
    check({a['targetPointId'] for a in candidates(zoned)} == {3},
          'native zoning was ignored in suggested city placements')
    validate = lua.globals().Act('city_layout_validate',
                                 lua.globals().Fields(zoned['layoutRevision'], '101:3'))
    check(validate['data']['valid'] is True,
          'current native-valid zoned candidate failed validation')

    lua.execute('''
      BuildingUtils.IsInMyBaseCircleRange = function(pid, radius)
          return pid == 4 and radius == 9
      end
    ''')
    check(len(candidates(snapshot(lua))) == 0,
          'native placement radius restriction ignored')

    lua.execute('''
      DataCenter.CityZoneManager.CanPutBuildByPoint = function(self, id, zone)
          return self.allZonePoint[id] ~= nil and zone == 1
      end
    ''')
    checked = candidates(snapshot(lua))
    check(len(checked) >= 1 and
          all(a['targetPointId'] == 4 for a in checked),
          'native radius did not restrict candidate target to cell 4')
    radius_revision = snapshot(lua)['layoutRevision']
    lua.execute('''
      DataCenter.BoardManager.GetBuildMaxRadius = function(self) return 10 end
    ''')
    changed_radius = snapshot(lua)
    check(changed_radius['layoutRevision'] != radius_revision,
          'original build radius changed without invalidating the snapshot')
    stale = lua.globals().Act(
        'city_layout_validate',
        lua.globals().Fields(radius_revision, '101:3'))['data']
    check(stale['valid'] is False and
          any(stale['issues'][i]['code'] == 'CITY_LAYOUT_STALE'
              for i in range(1, len(stale['issues'])+1)) and
          stale['currentRevision'] == changed_radius['layoutRevision'],
          'radius drift was accepted without returning current revision')
    lua.execute('''
      DataCenter.BoardManager.GetBuildMaxRadius = function(self) return 9 end
    ''')
    restricted = lua.globals().Act(
        'city_layout_validate',
        lua.globals().Fields(snapshot(lua)['layoutRevision'], '101:3'))['data']
    check(any(restricted['issues'][i]['code'] == 'CITY_LAYOUT_RANGE'
              for i in range(1, len(restricted['issues'])+1)),
          'validation allowed a zone-valid placement outside native radius')
    refused = lua.globals().Act(
        'city_layout_apply_start',
        lua.globals().Fields(snapshot(lua)['layoutRevision'], '101:3'))
    check(refused['data']['accepted'] is False and
          len(lua.globals().ObservedMoves) == 0,
          'native radius invalid move dispatched to server')

    lua.execute('''
      BuildingUtils.IsInMyBaseCircleRange = nil
    ''')
    missing = snapshot(lua)
    check(len(candidates(missing)) == 0 and
          missing['moveCandidateSearch']['nativeRangeAvailable'] is False,
          'missing original radius helper produced an unsafe suggestion')
    check(len(lua.globals().ObservedMoves) == 0,
          'candidate search dispatched native network messages')

    # A two-tile native high-corner footprint must validate every translated
    # tile's permission and refuse positions that cross a grid row.
    lua, _ = fake()
    install_radius(lua)
    lua.execute('''
      local native = DataCenter.BuildTemplateManager.GetBuildingDesTemplate
      DataCenter.BuildTemplateManager.GetBuildingDesTemplate =
          function(self, itemId)
              if itemId == 11 then
                  return {tileX=2, tileY=1, zoneType=1,
                          CanMove=function() return true end}
              end
              return native(self,itemId)
          end
      DataCenter.BuildManager.allBuilding[101].pointId = 4
      DataCenter.BuildManager.allBuilding[101].GetRangedPoints =
          function(self) return {[4]=true,[3]=true} end
      DataCenter.CityZoneManager.CanPutBuildByPoint = function(self, id, zone)
          if id == 4 or id == 5 then return false end
          return self.allZonePoint[id] ~= nil and zone == 1
      end
    ''')
    state = snapshot(lua)
    wide = [a for a in candidates(state) if a['uuid'] == '101']
    check(len(wide) >= 1 and all(
          a['targetPointId'] % 5 != 1 and a['targetPointId'] > 2
          for a in wide), 'multi-cell native footprint crossed row boundary')
    check(all(a['targetPointId'] != 5 for a in wide),
          'rejected footprint cell leaked into move candidates')
    print('FEATURE018_CITY_CANDIDATE: PASS pinned v26 +'
          ' deterministic source-zone/footprint/radius/no-send checks')


if __name__ == '__main__':
    main()
