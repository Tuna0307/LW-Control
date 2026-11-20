"""Controlled real-producer regression; no game processes or gameplay sends."""
import unittest
from feature018_city_game_checks import fake


class CityMovementLevelChecks(unittest.TestCase):
    def test_current_level_reaches_native_rule_and_fixed_buildings_stay_fixed(self):
        lua, _ = fake()
        lua.execute('''
          EligibilityCalls = {}
          BuildingUtils.CanMoveBuild = function(id)
            EligibilityCalls[#EligibilityCalls+1] = id
            return GateLevel >= 1 and (id == 17 or id == 18)
          end
        ''')
        snapshot = lua.globals().Act('city_layout_snapshot_get')['data']
        buildings = {snapshot['buildings'][i]['uuid']: snapshot['buildings'][i]
                     for i in range(1, 4)}
        self.assertTrue(buildings['101']['movable'])
        self.assertTrue(buildings['102']['movable'])
        self.assertFalse(buildings['103']['movable'])
        self.assertEqual(set(lua.globals().EligibilityCalls.values()), {17, 18, 19})
        lua.execute('GateLevel = 0')
        gated = lua.globals().Act('city_layout_snapshot_get')['data']
        self.assertTrue(all(not gated['buildings'][i]['movable'] for i in range(1, 4)))
        self.assertEqual(len(lua.globals().ObservedMoves), 0)

    def test_invalid_level_fails_without_querying_guessed_template(self):
        lua, _ = fake()
        lua.execute('DataCenter.BuildManager.allBuilding[101].level = -1')
        result = lua.globals().Act('city_layout_snapshot_get')
        self.assertNotEqual(result['state'], 'confirmed')
        self.assertEqual(len(lua.globals().ObservedMoves), 0)


if __name__ == '__main__':
    unittest.main()
