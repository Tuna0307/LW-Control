"""Inert v26 relocation preflight contract and original-bytecode checks.

Reads the installed original v26 LWLF package only for source signatures and
executes the new Lua module against fake managers. No game, account, network,
updater, script replacement, installer, credentials or journal operations.
"""
from __future__ import annotations

import hashlib
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path[:0] = [str(ROOT), str(ROOT / "map_009_offline"),
                str(ROOT.parent / "artifacts/map-013-lua-site")]
from lupa.lua53 import LuaRuntime  # noqa: E402
import compact_lua53 as lua53  # noqa: E402
import run_live_resource_probe as probe  # noqa: E402
from feature018_mini_lua import ORIGINAL_ENTRY_SHA256  # noqa: E402
from inspect_current import disassemble, root_methods  # noqa: E402

SCRIPT = ROOT / "feature018_relocation_actions.lua"

FIXTURE = r"""
world = {
    uid='3133546747000001', source=2212, selfServer=2212, current=2212,
    position=501234, alliance=true, focusClear=true, itemCalls=0,
    sends=0, dialogs=0, relocations=0,
    standard=2, season=0, allianceItem=1,
}
LuaEntry = {Player={
    uid=world.uid,
    GetSourceServerId=function() return world.source end,
    GetSelfServerId=function() return world.selfServer end,
    GetCurServerId=function() return world.current end,
    GetMainWorldPos=function() return world.position end,
    IsInAlliance=function() return world.alliance end,
}}
SpecialItemId={ITEM_MOVE_CITY=5001, SEASON_LIMIT_MOVE_CITY=5002,
               ITEM_ALLIANCE_CITY_MOVE=5003}
DataCenter={ItemData={GetItemById=function(self,id)
    world.itemCalls=world.itemCalls+1
    if id==5001 and world.standard>0 then return {count=world.standard,uuid=100001} end
    if id==5002 and world.season>0 then return {count=world.season,uuid=100002} end
    if id==5003 and world.allianceItem>0 then return {count=world.allianceItem,uuid=100003} end
    return nil
end}}
SFSNetwork={SendMessage=function(...) world.sends=world.sends+1 end}
UIManager={OpenWindow=function(...) world.dialogs=world.dialogs+1 end}
MoveCityUtil={TryMoveCity=function(...) world.relocations=world.relocations+1 end,
              TryAllianceMoveCity=function(...) world.relocations=world.relocations+1 end}
"""


def scenario():
    runtime = LuaRuntime(unpack_returned_tuples=True)
    runtime.execute(FIXTURE)
    actions = runtime.execute(SCRIPT.read_text(encoding="utf-8"))
    world = runtime.globals().world
    def call(action, query=False, profile="owner", focus=None):
        values = {"action": action, "slot": "0", "profileId": profile,
                  "launchSessionId": "launch", "challenge": "challenge", "gamePid": "8123"}
        if query:
            values["queryOnly"] = "1"
        allow_focus = runtime.eval("function() return world.focusClear end")
        if focus is not None:
            allow_focus = runtime.eval("function() return " +
                                       ("true" if focus else "false") + " end")
        return (actions.RandomRelocate if action == "randomRelocate" else actions.AllianceRelocate)(
            runtime.table_from(values), allow_focus)
    return runtime, actions, world, call


class InertRelocationChecks(unittest.TestCase):
    def assert_no_side_effects(self, state):
        self.assertEqual(state["sends"], 0)
        self.assertEqual(state["dialogs"], 0)
        self.assertEqual(state["relocations"], 0)

    def test_readonly_snapshot_reports_actual_game_items_without_guessing_mapping(self):
        _, actions, state, _ = scenario()
        snapshot = actions.Snapshot()
        self.assertEqual(snapshot["state"], "confirmed")
        data = snapshot["data"]
        self.assertEqual(data["observation"], "native_player_world_position_and_owned_move_items")
        self.assertEqual(data["sourceServerId"], 2212)
        self.assertEqual(data["worldPointId"], 501234)
        self.assertEqual(data["itemCounts"]["ITEM_MOVE_CITY"]["count"], 2)
        self.assertEqual(data["itemCounts"]["SEASON_LIMIT_MOVE_CITY"]["count"], 0)
        self.assertEqual(data["itemCounts"]["ITEM_ALLIANCE_CITY_MOVE"]["count"], 1)
        self.assertTrue(data["noActionDispatched"])
        self.assert_no_side_effects(state)

    def test_f9_and_f10_fail_explicitly_even_with_owned_items_and_alliance(self):
        _, _, state, call = scenario()
        for kind in ("randomRelocate", "allianceRelocate"):
            result = call(kind)
            self.assertEqual(result["state"], "unsupported")
            self.assertEqual(result["errorCode"], "GAME_METHOD_UNAVAILABLE")
            self.assertFalse(result["data"]["worldPointChanged"])
            self.assertFalse(result["data"]["changeAttributedToHotkey"])
            self.assertEqual(result["data"]["beforeWorldPointId"], 501234)
            self.assertEqual(result["data"]["afterWorldPointId"], 501234)
            self.assertTrue(result["data"]["requiredUserStep"])
        self.assert_no_side_effects(state)

    def test_poststate_is_unattributed_and_ownership_change_invalidates_before(self):
        _, _, state, call = scenario()
        self.assertEqual(call("allianceRelocate", query=True)["state"], "failed")
        self.assertEqual(call("allianceRelocate")["state"], "unsupported")
        state["position"] = 505000  # independent/manual change; hotkey never sent
        after = call("allianceRelocate", query=True)
        self.assertEqual(after["state"], "unsupported")
        self.assertEqual(after["data"]["beforeWorldPointId"], 501234)
        self.assertEqual(after["data"]["afterWorldPointId"], 505000)
        self.assertTrue(after["data"]["worldPointChanged"])
        self.assertFalse(after["data"]["changeAttributedToHotkey"])
        self.assertEqual(call("allianceRelocate", query=True, profile="different")["state"], "failed")
        self.assertEqual(call("allianceRelocate", query=True)["state"], "failed")
        self.assert_no_side_effects(state)

    def test_focus_and_current_owner_world_rejected_even_with_inventory(self):
        runtime, actions, state, call = scenario()
        state["focusClear"] = False
        self.assertEqual(call("randomRelocate")["state"], "failed")
        self.assert_no_side_effects(state)
        state["focusClear"] = True
        state["current"] = 2289
        reject = call("allianceRelocate")
        self.assertEqual(reject["state"], "unsupported")
        self.assertIn("server-2212", reject["error"])
        self.assertEqual(actions.Snapshot()["state"], "unsupported")
        state["current"] = 2212
        state["source"] = 2289
        self.assertEqual(call("allianceRelocate")["state"], "unsupported")
        state["source"] = 2212
        # An unparseable native UID is rejected; the fixture's native Player
        # property must change, not just the backing state used by methods.
        runtime.globals().LuaEntry.Player.uid = "not-an-uid"
        self.assertIn("owner", call("allianceRelocate")["error"])
        self.assert_no_side_effects(state)

    def test_missing_inventory_and_invalid_requests_fail_closed(self):
        runtime, actions, state, call = scenario()
        self.assertEqual(call("randomRelocate", profile="bad profile")["state"], "failed")
        self.assertEqual(call("allianceRelocate", focus=False)["state"], "failed")
        runtime.globals().DataCenter.ItemData.GetItemById = runtime.eval(
            "function(self,id) return {count=-1,uuid=30001} end")
        self.assertIn("item count unreadable", call("randomRelocate")["error"])
        runtime.globals().DataCenter.ItemData.GetItemById = None
        self.assertIn("inventory unavailable", call("randomRelocate")["error"])
        self.assert_no_side_effects(state)


class OriginalV26Sources(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        fver, cver, members = probe.read_lwlf(probe.paths()["data"])
        cls.members = dict(members)
        original = cls.members.get(probe.ORIGINAL_LUA_ENTRY,
                                   cls.members.get(probe.LUA_ENTRY))
        if (fver, cver) != (3, 26) or original is None or \
                hashlib.sha256(original).hexdigest() != ORIGINAL_ENTRY_SHA256:
            raise unittest.SkipTest("Admitted original v26 Lua resource unavailable")

    def check_method(self, name, method, digest, required):
        encoded = self.members[name]
        self.assertEqual(hashlib.sha256(encoded).hexdigest(), digest)
        proto = lua53.parse_chunk(probe.decode_lenc(encoded))
        mapped = root_methods(proto)
        self.assertIn(method, mapped)
        for component in mapped[method].split("/")[1:]:
            proto = proto["children"][int(component)]
        constants = set(str(x) for x in proto["constants"])
        for value in required:
            self.assertIn(value, constants, f"{name}:{method} lacks {value}")
        return proto

    def test_owned_items_vs_unproven_random_mapping(self):
        self.check_method("Util/MoveCityUtil.luac", "HasPaidMoveItem",
                          "d53c624e4dcc87b6228f8ac650704868e321332614d36321b88800b5f8562de4",
                          ("ITEM_MOVE_CITY", "SEASON_LIMIT_MOVE_CITY", "GetItemById", "count"))
        self.check_method("UI/UIMoveCity/View/UIMoveCityView.luac", "TryChangeUseItem",
                          "20ce35e439aa6c1743ea59652008de4ca1da01bfa74c70f850a91037dda70164",
                          ("ITEM_ALLIANCE_CITY_MOVE", "MoveCity_Al", "costType"))

    def test_rally_selection_and_game_native_warning_door(self):
        self.check_method("Util/MoveCityUtil.luac", "TryAllianceMoveCity",
                          "d53c624e4dcc87b6228f8ac650704868e321332614d36321b88800b5f8562de4",
                          ("UIChooseRallyPoint", "GetMyAllianceRallyPoint",
                           "AllianceMoveCityToSourceRallyPoint"))
        self.check_method("UI/UIMoveCity/View/UIMoveCityView.luac", "TryMoveCity",
                          "20ce35e439aa6c1743ea59652008de4ca1da01bfa74c70f850a91037dda70164",
                          ("CheckBlankLand", "MoveCityInBlackTip", "TryShowConfirm"))

    def test_world_movement_send_and_server_ack_are_separate(self):
        self.check_method("UI/UIMoveCity/View/UIMoveCityView.luac", "DoCityMove",
                          "20ce35e439aa6c1743ea59652008de4ca1da01bfa74c70f850a91037dda70164",
                          ("WorldMv", "itemUUid", "freeAllianceMove", "pointId"))
        self.check_method("Net/Msgs/WorldMvMessage.luac", "OnCreate",
                          "2f71e8b4fdcb42c07b3b738d4614c456a38fd12dce334b4c157213a2f8d6c4e3",
                          ("PutInt", "PutUtfString", "PutBool", "itemUUid", "pointId"))
        self.check_method("Net/Msgs/WorldMvMessage.luac", "HandleMessage",
                          "2f71e8b4fdcb42c07b3b738d4614c456a38fd12dce334b4c157213a2f8d6c4e3",
                          ("WorldMvHandle", "lastFreeMoveTime"))
        self.check_method("Net/Msgs/MoveCityToWorldMessage.luac", "HandleMessage",
                          "d82c7095c19d7de503a17115ee82647a015ede6ac230336bdb9c29673d973594",
                          ("MoveCityToWorldHandle", "GetMainWorldPos"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
