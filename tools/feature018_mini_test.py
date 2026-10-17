"""Inert execution of selected genuine Mini Games v26 Lua bytecode methods.

No game process, installed-file writes, or actual SFSNetwork calls. Source
prototypes are decoded from the admitted v26 original entry and run with inert
Python callbacks through the existing bounded Lua instruction interpreter.
"""

from __future__ import annotations

import hashlib
import sys
import unittest
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "map_009_offline"))
import run_live_resource_probe as probe  # noqa: E402
import compact_lua53 as lua  # noqa: E402
import execute_selected as runtime  # noqa: E402
from inspect_current import root_methods, disassemble  # noqa: E402
from feature018_mini_lua import ORIGINAL_ENTRY_SHA256  # noqa: E402


class MiniGameNativeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        file_version, content_version, records = probe.read_lwlf(probe.paths()["data"])
        if (file_version, content_version) != (3, 26):
            raise unittest.SkipTest("current original v26 Lua content unavailable")
        cls.entries = dict(records)
        original = cls.entries.get(probe.ORIGINAL_LUA_ENTRY, cls.entries.get(probe.LUA_ENTRY))
        if original is None or hashlib.sha256(original).hexdigest() != ORIGINAL_ENTRY_SHA256:
            raise unittest.SkipTest("original Lua entry does not match source admission")

    def method(self, module, method):
        root = lua.parse_chunk(probe.decode_lenc(self.entries[module]))
        path = root_methods(root).get(method)
        self.assertIsNotNone(path, f"{module} missing source-native method {method}")
        body = root
        for segment in path.split("/")[1:]:
            body = body["children"][int(segment)]
        return body

    def test_land_uses_game_feature_switch_and_real_dict_accessor(self):
        module = "DataCenter/LandLock/LandLockManager.luac"
        observed = []
        enabled = self.method(module, "Enabled")
        globals_ = {"LuaEntry": {"DataConfig": {
            "CheckSwitch": lambda self, name: observed.append(name) or name == "land_lock"
        }}}
        self.assertEqual(runtime.execute(enabled, {}, globals_=globals_), (True,))
        self.assertEqual(observed, ["land_lock"])
        globals_["LuaEntry"]["DataConfig"]["CheckSwitch"] = lambda _, name: False
        self.assertEqual(runtime.execute(enabled, {}, globals_=globals_), (False,))
        get_by_id = self.method(module, "GetLandLockDataById")
        cell = {"state": "Locked", "needPay": False}
        manager = {"landLockDataDict": defaultdict(lambda: None, {7: cell})}
        self.assertEqual(runtime.execute(get_by_id, manager, 7), (cell,))
        self.assertEqual(runtime.execute(get_by_id, manager, 999), (None,))

    def test_sheep_engine_calls_native_game_card_selector(self):
        click = self.method("DataCenter/LWSheep/LWSheepEngine.luac", "ClickCard")
        clicks = []
        engine = {"game": {"Click": lambda self, point: clicks.append(point) or True}}
        self.assertEqual(runtime.execute(click, engine, "7:15"), (True,))
        self.assertEqual(clicks, ["7:15"])
        self.assertEqual(runtime.execute(click, {"game": None}, "7:15"), ())

    def test_sheep_level_checks_game_data_validity(self):
        level_method = self.method("DataCenter/LWSheep/LWSheepDataManager.luac", "GetCurrentLevel")
        game = {"IsVail": lambda self: True, "sheepData": {"passMaxBlock": 7}}
        self.assertEqual(runtime.execute(level_method, game), (8,))
        game["IsVail"] = lambda self: False
        self.assertEqual(runtime.execute(level_method, game), (0,))

    def test_sheep_season_move_selects_native_message_and_city(self):
        move = self.method("DataCenter/SmallGameManager/GSheep.luac", "Move")
        captured = []
        globals_ = {"MiniGameSource": {"Season": 1},
                    "MsgDefines": {"LWSheepMove": "LWSheepMove",
                                   "SeasonCitySheepMove": "SeasonCitySheepMove"},
                    "SFSNetwork": {"SendMessage": lambda *args: captured.append(args)}}
        player = {"source": 1, "serverId": 2172, "cityId": 12}
        self.assertEqual(runtime.execute(move, player, 3, "point:21", globals_=globals_), ())
        self.assertEqual(captured, [("LWSheepMove", 3, "point:21")])
        captured.clear()
        player["source"] = 2
        self.assertEqual(runtime.execute(move, player, 4, "point:22", globals_=globals_), ())
        self.assertEqual(captured, [("SeasonCitySheepMove", 2172, 12, 4, "point:22")])

    def test_season_sheep_message_serializes_exact_fields(self):
        message = self.method("Net/Msgs/MiniGame/SeasonCitySheepMoveMessage.luac", "OnCreate")
        writes = []
        sfs = {"PutInt": lambda self, name, value: writes.append(("int", name, value)),
               "PutUtfString": lambda self, name, value: writes.append(("string", name, value))}
        self.assertEqual(runtime.execute(message, {"sfsObj": sfs}, 2172, 12, 4, "point:22",
                                         globals_={"OnCreate": lambda _self: None}), ())
        self.assertEqual(writes, [("int", "targetServerId", 2172), ("int", "cityId", 12),
                                  ("int", "type", 4), ("string", "pointId", "point:22")])

    def test_chest_item_identity_is_1_based_stable(self):
        get_box = self.method("UI/LWTreasureChest/UITreasureChestView.luac", "GetBoxItem")
        view = {"boxItems": defaultdict(lambda: None, {1: "first", 2: "second", 3: "third"})}
        self.assertEqual(runtime.execute(get_box, view, 1), ("first",))
        self.assertEqual(runtime.execute(get_box, view, 3), ("third",))
        self.assertEqual(runtime.execute(get_box, view, 4), (None,))
        position = self.method("UI/LWTreasureChest/UITreasureChestItem.luac", "GetCurPosition")
        self.assertEqual(runtime.execute(position, {"transform": {"position": (1, 2, 3)}}),
                         ((1, 2, 3),))

    def test_chest_marker_uses_source_native_game_effect(self):
        module = "UI/LWTreasureChest/UITreasureChestView.luac"
        show = self.method(module, "ShowSwitchEffect")
        hide = self.method(module, "HideSwitchEffect")
        changes = []
        effect = {
            "SetActive": lambda self, active: changes.append(("active", active)),
            "transform": {
                "Set_position": lambda self, x, y, z: changes.append(("position", x, y, z))
            },
        }
        view = {"switchEffect": effect}
        self.assertEqual(runtime.execute(show, view, 10.25, 21.5, 32.75), ())
        self.assertEqual(changes, [("active", True), ("position", 10.25, 21.5, 32.75)])
        self.assertEqual(runtime.execute(hide, view), ())
        self.assertEqual(changes[-1], ("active", False))

    def test_frontline_add_member_forwards_hero_level_not_unit_quantity(self):
        logic = self.method("DataCenter/LWBattle/Logic/ParkourBattle/ParkourBattleLogic.luac",
                            "AddMember")
        self.assertEqual(logic["params"], 5)
        calls = []
        native_team = {"AddMember": lambda self, *args: calls.append(args)}
        self.assertEqual(runtime.execute(logic, {"team": native_team}, 203, 12, True, None), ())
        self.assertEqual(calls, [(203, 12, True, None)],
                         "Parkour AddMember is a single-unit method with level as argument 2")
        self.assertEqual(runtime.execute(logic, {"team": None}, 203, 12, True, None), ())
        self.assertEqual(len(calls), 1, "Parkour without a team must not create a unit")

    def test_frontline_real_parkour_team_uses_second_argument_as_level(self):
        source = "Scene/LWBattle/ParkourBattle/Team/ParkourTeam.luac"
        add = self.method(source, "AddMember")
        create = self.method(source, "GenHeroCfg")
        self.assertEqual(add["params"], 5)
        instructions = list(disassemble(add))
        # GenHeroCfg(heroId, heroLevel, true) is called exactly once in the
        # original AddMember, before one RealAddMemberInner.
        self.assertEqual(sum("='GenHeroCfg'" in x for x in instructions), 1)
        self.assertEqual(sum("='RealAddMemberInner'" in x for x in instructions), 1)
        self.assertTrue(any("sourceLine=410" in x and "TESTSET" in x and "B=2" in x
                            for x in instructions), "hero level must be passed through")
        self.assertTrue({"UpdateFromTemplate", "1"} <= {str(x) for x in create["constants"]})
        count = self.method(source, "GetMemberCount")
        self.assertEqual(runtime.execute(count, {"teamUnitCount": 11,
            "teamWorkerCount": 1, "teamInvisiblePetCount": 2}), (8,))

    def test_frontline_native_door_proves_one_unit_and_exact_source_args(self):
        door = self.method("Scene/LWBattle/ParkourBattle/Monster/MonsterImpl/StaticNumberDoorMonster.luac",
                           "OnCollisionViewHandle")
        instructions = list(disassemble(door))
        self.assertTrue(any("sourceLine=166" in x and "='AddMember'" in x for x in instructions))
        self.assertTrue(any("sourceLine=166" in x and "='controlHeroId'" in x for x in instructions))
        self.assertTrue(any("sourceLine=165" in x and "='trigger_para'" in x for x in instructions))
        self.assertTrue(any("sourceLine=166" in x and "LOADBOOL" in x for x in instructions))
        mini = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        self.assertIn('M.FrontlineReinforce = frontline_reinforce', mini)
        self.assertIn('local ok = pcall(member(context.logic, "AddMember"), context.logic,', mini)
        self.assertIn('context.heroId, context.heroLevel, true)', mini)
        self.assertIn('after ~= context.count + confirmed + 1', mini)
        self.assertIn('current.count ~= context.count + confirmed', mini)
        self.assertNotIn('"frontlineReinforce", frontline_reinforce', mini,
                         "G integration is owned by the shared authenticated hotkey dispatcher")

    def test_frontline_member_identity_is_separate_from_local_count(self):
        team = self.method("Scene/LWBattle/ParkourBattle/Team/ParkourTeam.luac", "AddUnitToTeam")
        soldier = self.method("Scene/LWBattle/ParkourBattle/Team/MemberUnit.luac", "Init")
        allot = self.method("DataCenter/LWBattle/Logic/ParkourBattle/ParkourBattleLogic.luac",
                            "AllotUnitGuid")
        team_ops = list(disassemble(team))
        soldier_ops = list(disassemble(soldier))
        self.assertTrue(any("sourceLine=1542" in op and "='teamUnits'" in op
                            for op in team_ops))
        self.assertTrue(any("sourceLine=1543" in op and "='teamUnitCount'" in op
                            for op in team_ops))
        self.assertTrue(any("sourceLine=32" in op and "='AllotUnitGuid'" in op
                            for op in soldier_ops))
        self.assertTrue(any("sourceLine=32" in op and "='guid'" in op
                            for op in soldier_ops))
        identity = {"unitGuid": 0}
        self.assertEqual(runtime.execute(allot, identity), (1,))
        self.assertEqual(runtime.execute(allot, identity), (2,))
        self.assertEqual(identity["unitGuid"], 2,
                         "source game must allocate different unit IDs for distinct members")
        mini = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        for guard in ("frontline_units(context.team)",
                      "after_count == prior_count + 1",
                      "new_units ~= 1", "if after_units[unit] == nil then identities_match = false end",
                      'member(unit, "originalHeroId") ~= context.heroId',
                      'member(hero, "heroId") ~= context.heroId',
                      'member(hero, "level") ~= context.heroLevel',
                      "unitIdentityVerified = true", "observedCreatedUnits = confirmed"):
            self.assertIn(guard, mini)

    def test_land_unlock_ack_routes_through_native_original_server_state(self):
        message = self.method("Net/Msgs/UnlockUserLandMessage.luac", "HandleMessage")
        push = self.method("Net/Msgs/PushUnlockLandMessage.luac", "HandleMessage")
        local_ack = self.method("DataCenter/LandLock/LandLockManager.luac", "UnlockHandle")
        broadcast = self.method("DataCenter/LandLock/LandLockManager.luac", "PushUnlockHandle")
        def names(method):
            return {str(constant) for constant in method["constants"]}
        self.assertTrue({"errorCode", "UnlockHandle"} <= names(message))
        self.assertTrue({"errorCode", "PushUnlockHandle"} <= names(push))
        self.assertTrue({"landId", "RefreshDataByServer", "GF_land_unlock_done"} <= names(local_ack))
        self.assertTrue({"lands", "RefreshDataByServer"} <= names(broadcast))
        mini = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        self.assertIn('invoke(land, "GetLandLockDataById", last_land_request_id)', mini)
        self.assertIn('now == member(states, "Unlocked") or now == member(states, "Finished")', mini)

    def test_chest_owned_marker_retires_on_same_view_native_transitions(self):
        module = "UI/LWTreasureChest/UITreasureChestView.luac"
        state = self.method(module, "ChangeSwitchState")
        selected = self.method(module, "OnClickBox")
        received = self.method(module, "OnTreasureChestEndResult")
        state_ops = list(disassemble(state))
        select_ops = list(disassemble(selected))
        result_ops = list(disassemble(received))
        self.assertTrue(any("sourceLine=189" in op and "='switchState'" in op
                            for op in state_ops))
        self.assertTrue(any("sourceLine=393" in op and "='curSelectIndex'" in op
                            for op in select_ops))
        self.assertTrue(any("sourceLine=261" in op and "='received'" in op
                            for op in result_ops))
        self.assertNotIn("HideSwitchEffect", {str(v) for v in state["constants"]},
                         "Native transition does not remove an added prize marker itself")
        mini = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        self.assertIn('if member(view, "curSelectIndex") ~= nil then', mini)
        self.assertIn('cls.ChangeSwitchState = state', mini)
        self.assertIn('cls.OnClickBox = click', mini)
        self.assertIn('cls.OnTreasureChestEndResult = chest_result', mini)
        self.assertIn('retire_chest_marker_if_ineligible(view)', mini)
        self.assertIn('clear_owned_chest_marker()', mini)
        self.assertIn('saved.class.OnClickBox = saved.originalClick', mini)
        self.assertIn('saved.class.ChangeSwitchState = saved.originalState', mini)
        self.assertIn('saved.class.OnTreasureChestEndResult = saved.originalResult', mini)


if __name__ == "__main__":
    unittest.main()
