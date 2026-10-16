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
from inspect_current import root_methods  # noqa: E402
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


if __name__ == "__main__":
    unittest.main()
