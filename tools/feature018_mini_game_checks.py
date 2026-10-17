"""Inert native v26 land ACK and exact-cell proof contract checks.

These checks never send game packets. The game bytecode is read only from the
admitted original v26 entry; the new status command is inspected as Lua text.
"""

from __future__ import annotations

import hashlib
import sys
import unittest
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path[:0] = [str(ROOT), str(ROOT / "map_009_offline")]

import compact_lua53 as lua  # noqa: E402
import execute_selected as vm  # noqa: E402
import run_live_resource_probe as probe  # noqa: E402
from feature018_mini_lua import ORIGINAL_ENTRY_SHA256  # noqa: E402
from inspect_current import disassemble, root_methods  # noqa: E402


class NativeLandAckChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        version, content, items = probe.read_lwlf(probe.paths()["data"])
        cls.items = dict(items)
        original = cls.items.get(probe.ORIGINAL_LUA_ENTRY, cls.items.get(probe.LUA_ENTRY))
        if (version, content) != (3, 26) or original is None or \
           hashlib.sha256(original).hexdigest() != ORIGINAL_ENTRY_SHA256:
            raise unittest.SkipTest("Admitted original v26 Lua entry unavailable")

    @classmethod
    def method(cls, name, method):
        proto = lua.parse_chunk(probe.decode_lenc(cls.items[name]))
        path = root_methods(proto)[method]
        for segment in path.split("/")[1:]:
            proto = proto["children"][int(segment)]
        return proto

    def test_server_reply_applies_only_named_land_and_preserves_next_locked(self):
        # Execute the real LandLockManager.RefreshDataByServer (v26 168-211)
        # on inert records. A changed next-locked ID is a consequence of a
        # specific row changing, not a receipt for the prior send by itself.
        refresh = self.method("DataCenter/LandLock/LandLockManager.luac",
                              "RefreshDataByServer")
        land5 = defaultdict(lambda: None, {"id": 5, "state": "locked", "pveList": []})
        land6 = defaultdict(lambda: None, {"id": 6, "state": "locked", "pveList": []})
        applied = []
        manager = {
            "landLockDataDict": defaultdict(lambda: None, {5: land5, 6: land6}),
            "GetTemplate": lambda self, ident: defaultdict(lambda: None),
            "SetLandLockState": lambda self, row, state, _effect: (
                applied.append((row["id"], state)), row.__setitem__("state", state)
            ),
            "RefreshReward": lambda *args: None,
        }
        states = {"Unlocked": "unlocked", "Finished": "finished", "Hide": "hidden"}
        for server_state, expected in [(2, "unlocked"), (3, "finished")]:
            land5["state"] = "locked"
            applied.clear()
            reply = defaultdict(lambda: None, {"landId": 5, "state": server_state})
            self.assertEqual(vm.execute(refresh, manager, reply,
                                        False, globals_={"LandLockState": states}), ())
            self.assertEqual(applied, [(5, expected)])
            self.assertEqual(land5["state"], expected)
            self.assertEqual(land6["state"], "locked")

    def test_server_error_is_not_a_land_state_confirmation(self):
        local = self.method("Net/Msgs/UnlockUserLandMessage.luac", "HandleMessage")
        broadcast = self.method("Net/Msgs/PushUnlockLandMessage.luac", "HandleMessage")
        for handler, native in [(local, "UnlockHandle"), (broadcast, "PushUnlockHandle")]:
            ops = list(disassemble(handler))
            names = {str(c) for c in handler["constants"]}
            self.assertTrue({"errorCode", "ShowTips", native} <= names)
            self.assertTrue(any("sourceLine=19" in op and "errorCode" in op
                                for op in ops))
        # The original local callback can be an error with no LandLockManager
        # update, so submitted alone must never be treated as completion.

    def test_native_unlock_reply_error_does_not_update_manager(self):
        # Execute the two original incoming network callbacks using inert
        # manager and UI fixtures. This tests their conditional dispatch,
        # beyond searching for named methods in the bytecode constants.
        for module, action in [
            ("Net/Msgs/UnlockUserLandMessage.luac", "UnlockHandle"),
            ("Net/Msgs/PushUnlockLandMessage.luac", "PushUnlockHandle"),
        ]:
            handler = self.method(module, "HandleMessage")
            changes, tips = [], []
            manager = {action: lambda _self, packet: changes.append(packet)}
            source_globals = {
                "HandleMessage": lambda *_: None,
                "DataCenter": {"LandLockManager": manager,
                               "RewardManager": {"AddRewardsAndRes": lambda *_: None}},
                "UIUtil": {"ShowTips": lambda value: tips.append(value)},
            }
            upvalues = {
                1: source_globals,
                2: {"GetString": lambda _self, code: f"error:{code}"},
            }
            error_packet = defaultdict(lambda: None, {"errorCode": 4321})
            self.assertEqual(vm.execute(handler, {}, error_packet, globals_=source_globals,
                                        upvalues=upvalues), ())
            self.assertEqual(changes, [], f"{action} updated on an error packet")
            self.assertEqual(tips, ["error:4321"])
            ok_packet = defaultdict(lambda: None,
                                    {"landId": 5, "lands": [{"landId": 5, "state": 2}]})
            self.assertEqual(vm.execute(handler, {}, ok_packet, globals_=source_globals,
                                        upvalues=upvalues), ())
            self.assertEqual(changes, [ok_packet])

    def test_status_supports_explicit_read_only_id_without_implicit_resend(self):
        source = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        exact = source.split("if type(fields) == \"table\" and fields.expectedCellId ~= nil then",
                             1)[1].split("if not is_integer(last_land_request_id)", 1)[0]
        self.assertIn('invoke(land, "GetLandLockDataById", id)', exact)
        self.assertIn('exactCellLookup = true', exact)
        self.assertIn('observation = "native_land_lock_state"', exact)
        self.assertIn('last_land_request_id = nil', exact)
        self.assertIn('id > 10000', exact)
        self.assertIn('not raw_id:match("^[1-9]%d*$")', exact)
        self.assertNotIn("SendMessage", exact, "read-only verification cannot send")
        self.assertNotIn("GetCurrentToBeUnlockedId", exact,
                         "the exact-ID path must not query a different locked cell")
        self.assertRegex(source, r'local function land_status\(fields\)')
        self.assertIn('bridge.RegisterHandler("mini_land_status", land_status)', source)
        self.assertIn("local land, states = land_state_context()", source)
        read_context = source.split("local function land_state_context()", 1)[1]
        read_context = read_context.split("local function land_context()", 1)[0]
        self.assertNotIn("LandLockCondition", read_context,
                         "Exact native land read must not need send-only conditions")

    def test_next_locked_status_carries_only_reverified_previous_unlock(self):
        source = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        between = source.split("if not is_integer(last_land_request_id) then", 1)[1]
        between = between.split('local ok, row = invoke(land, "GetLandLockDataById", last_land_request_id)', 1)[0]
        self.assertIn('lastObservedUnlockedCellId = observed', between)
        self.assertIn('invoke(land, "GetLandLockDataById",', between)
        self.assertIn('previous_state == member(states, "Unlocked")', between)
        self.assertIn('previous_state == member(states, "Finished")', between)
        self.assertIn('last_observed_unlocked_id = nil', between,
                      "a vanished/relocked old row must retire the receipt")
        self.assertNotIn("SendMessage", between)
        self.assertIn('last_observed_unlocked_id = current_id', source)
        cleanup = source.split('function M.CleanupAfterLeaseLoss()', 1)[1]
        cleanup = cleanup.split('\nend', 1)[0]
        self.assertIn('last_observed_unlocked_id = nil', cleanup)

    def test_original_chest_partial_activation_requires_single_call_cleanup(self):
        native = "UI/LWTreasureChest/UITreasureChestView.luac"
        show = self.method(native, "ShowSwitchEffect")
        hide = self.method(native, "HideSwitchEffect")
        events = []
        effect = {"activeSelf": False}

        def activate(_effect, value):
            effect["activeSelf"] = value
            events.append(("active", value))

        def fail_position(_transform, *_coordinates):
            raise RuntimeError("native transform position failed after activation")

        effect["SetActive"] = activate
        effect["transform"] = {"Set_position": fail_position}
        with self.assertRaisesRegex(RuntimeError, "native transform position"):
            vm.execute(show, {"switchEffect": effect}, 1, 2, 3)
        self.assertTrue(effect["activeSelf"],
                        "Original v26 activates the effect before Set_position")
        self.assertEqual(events, [("active", True)])
        self.assertEqual(vm.execute(hide, {"switchEffect": effect}), ())
        self.assertEqual(events, [("active", True), ("active", False)])
        self.assertFalse(effect["activeSelf"])
        source = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        mark = source.split("local function mark_chest()", 1)[1]
        mark = mark.split("local function unmark_chest()", 1)[0]
        self.assertIn('member(effect, "activeSelf") ~= false', mark)
        self.assertIn("marker_owned_view = view", mark)
        self.assertIn('local ok = pcall(show, view, data.x, data.y, data.z)', mark)
        self.assertIn('if not clear_owned_chest_marker() then', mark)
        self.assertNotIn('invoke(view, "ShowSwitchEffect"', mark,
                         "Retrying a partially successful native marker is unsafe")


if __name__ == "__main__":
    unittest.main()
