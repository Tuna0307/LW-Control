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
sys.path.insert(0, str(ROOT.parent / "artifacts" / "map-013-lua-site"))

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

    def test_original_sfs_send_serializes_before_network_submission(self):
        # v26 SFSNetwork.NewMessage calls the concrete OnCreate packet writer;
        # ToBinary is evaluated before SendLuaMessage. The land observer may
        # therefore treat an unobserved OnCreate as uncertain, never accepted.
        send = self.method("Net/SFSNetwork.luac", "SendMessage")
        instructions = list(disassemble(send))
        new = next(i for i, op in enumerate(instructions) if "NewMessage" in op)
        binary = next(i for i, op in enumerate(instructions) if "ToBinary" in op)
        binary_call = next(i for i, op in enumerate(instructions)
                           if i > binary and "CALL" in op and "A=6" in op)
        dispatched = next(i for i, op in enumerate(instructions)
                          if i > binary_call and "CALL" in op and "A=3" in op)
        self.assertLess(new, binary)
        self.assertLess(binary_call, dispatched,
                        "native send must serialize before SendLuaMessage invocation")

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

    def test_season_four_food_house_read_only_availability_has_native_sources(self):
        # Original v26 IsVail requires both the current activity and sheepData;
        # CheckHasGame inspects the native cache/eliminate/stack collections.
        # These are read-only live-precondition observations, not game success.
        module = "DataCenter/LWSheep/LWSheepDataManager.luac"
        activity = self.method(module, "GetActivityData")
        valid = self.method(module, "IsVail")
        playable = self.method(module, "CheckHasGame")
        self.assertTrue({"ActivityListDataManager", "GetActivityDataById", "activityId"}
                        <= {str(c) for c in activity["constants"]})
        self.assertTrue({"GetActivityData", "sheepData"}
                        <= {str(c) for c in valid["constants"]})
        self.assertTrue({"cache", "eliminate", "stack"}
                        <= {str(c) for c in playable["constants"]})
        source = (ROOT / "feature018_mini_game.lua").read_text(encoding="utf-8")
        status = source.split("local function sheep_status()", 1)[1]
        status = status.split("local function sheep_board(view)", 1)[0]
        for original in ("GetActivityData", "IsVail", "GetCurrentLevel", "CheckHasGame"):
            self.assertIn(original, status)
        self.assertNotIn("SendMessage", status)
        self.assertNotIn("OnClickCard", status)

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

    def test_original_chest_both_close_buttons_retire_owned_gui_effect(self):
        path = "UI/LWTreasureChest/UITreasureChestView.luac"
        for name in ("OnBtnCloseClick", "OnBtnBlackClick"):
            method = self.method(path, name)
            self.assertTrue({"ctrl", "CloseSelf"} <=
                            {str(value) for value in method["constants"]})

    def test_frontline_event_must_be_launched_by_original_sunday_activity(self):
        event = self.method("DataCenter/ActFrontBreakSundayManger/ActFrontBreakSundayData.luac",
                            "EnterStage")
        self.assertTrue({"PVEType", "Parkour", "fromActFrontBreakSunday",
                         "frontBreakSundayActId", "activityId"} <=
                        {str(value) for value in event["constants"]})
        result = self.method("DataCenter/LWBattle/Logic/ParkourBattle/ParkourBattleLogic.luac",
                             "OnFrontBreakSundayChallengeResult")
        self.assertTrue({"fromActFrontBreakSunday", "frontBreakSundayActId", "param"} <=
                        {str(value) for value in result["constants"]})


class NativeChestMarkerLua53Checks(unittest.TestCase):
    @staticmethod
    def fixture():
        from lupa.lua53 import LuaRuntime
        runtime = LuaRuntime(unpack_returned_tuples=True)
        runtime.execute(r'''
        Account={source=2289,selfServer=2289,current=2289}
        LuaEntry={Player={uid='3133546747002289',
          GetSourceServerId=function() return Account.source end,
          GetSelfServerId=function() return Account.selfServer end,
          GetCurServerId=function() return Account.current end}}
        local cls = {}
        for _,name in ipairs({'Update','OnDestroy','ChangeSwitchState','OnClickBox',
          'OnTreasureChestEndResult','OnBtnCloseClick','OnBtnBlackClick'}) do
            cls[name] = function(self, ...) return name end
        end
        ChestClass = cls
        OriginalClose, OriginalBlack = cls.OnBtnCloseClick, cls.OnBtnBlackClick
        package.loaded['UI.LWTreasureChest.UITreasureChestView'] = cls
        NativeBox = {IsMoving=function() return false end,
          GetCurPosition=function() return {x=12,y=24,z=36} end}
        local position = {x=0,y=0,z=0}
        Effect = {activeSelf=false,transform={position=position}}
        function Effect:SetActive(value) self.activeSelf=value end
        function Effect.transform:Set_position(x,y,z)
          self.position = {x=x,y=y,z=z}
        end
        Chest = setmetatable({switchState='SwitchEnd',received=false,
          bestRewardIndex=2,boxItems={[2]=NativeBox},switchEffect=Effect},
          {__index=cls})
        function Chest:GetBigRewardBoxIndex() return 2 end
        function Chest:GetBoxItem(i) return self.boxItems[i] end
        function Chest:ShowSwitchEffect(x,y,z)
          self.switchEffect:SetActive(true)
          self.switchEffect.transform:Set_position(x,y,z)
        end
        function Chest:HideSwitchEffect() self.switchEffect:SetActive(false) end
        ''')
        producer = runtime.execute((ROOT / "feature018_mini_game.lua").read_text(
            encoding="utf-8"))
        assert producer.BindTreasureChestView(runtime.globals().Chest)
        return runtime, producer

    def test_marker_shown_only_when_native_effect_reaches_winning_chest(self):
        runtime, mini = self.fixture()
        marked = mini.MarkChest()
        self.assertEqual(marked['state'], 'confirmed')
        self.assertTrue(marked['data']['markerVisible'])
        self.assertTrue(marked['data']['markerPositionVerified'])
        self.assertEqual(runtime.globals().Effect.transform.position.x, 12)
        self.assertEqual(runtime.globals().Effect.transform.position.y, 24)
        self.assertEqual(runtime.globals().Effect.transform.position.z, 36)

    def test_partial_marker_stale_coordinates_are_rolled_back(self):
        runtime, mini = self.fixture()
        runtime.execute('Chest.ShowSwitchEffect=function(self) '
                        'self.switchEffect:SetActive(true) end')
        result = mini.MarkChest()
        self.assertEqual(result['state'], 'failed')
        self.assertFalse(runtime.globals().Effect.activeSelf)

    def test_marker_is_rejected_if_chest_moves_after_effect_was_positioned(self):
        runtime, mini = self.fixture()
        runtime.execute('''local original = Chest.ShowSwitchEffect
            Chest.ShowSwitchEffect = function(self,x,y,z)
              original(self,x,y,z)
              NativeBox.GetCurPosition=function() return {x=88,y=24,z=36} end
            end''')
        self.assertEqual(mini.MarkChest()['state'], 'failed')
        self.assertFalse(runtime.globals().Effect.activeSelf)

    def test_black_market_mark_does_not_activate_on_other_account(self):
        runtime, mini = self.fixture()
        runtime.execute('LuaEntry.Player.uid="3133546747002212"')
        self.assertEqual(mini.MarkChest()['state'], 'failed')
        self.assertFalse(runtime.globals().Effect.activeSelf)
        runtime.execute('LuaEntry.Player.uid="3133546747002289"; Account.current=2212')
        self.assertEqual(mini.MarkChest()['state'], 'failed')
        self.assertFalse(runtime.globals().Effect.activeSelf)

    def test_source_reward_change_or_box_swap_blocks_marker(self):
        runtime, mini = self.fixture()
        runtime.execute('Chest.GetBigRewardBoxIndex=function() return 1 end')
        self.assertEqual(mini.MarkChest()['state'], 'unsupported')
        runtime.execute('Chest.GetBigRewardBoxIndex=function() return 2 end')
        runtime.execute('Chest.GetBoxItem=function() return {} end')
        self.assertEqual(mini.MarkChest()['state'], 'unsupported')
        self.assertFalse(runtime.globals().Effect.activeSelf)

    def test_both_close_buttons_hide_and_restore_exact_native_hook(self):
        for button in ('OnBtnCloseClick', 'OnBtnBlackClick'):
            runtime, mini = self.fixture()
            self.assertEqual(mini.MarkChest()['state'], 'confirmed')
            cls = runtime.globals().ChestClass
            self.assertIsNotNone(cls[button])
            cls[button](runtime.globals().Chest)
            self.assertFalse(runtime.globals().Effect.activeSelf)
            mini.CleanupAfterLeaseLoss()
            self.assertTrue(runtime.eval('ChestClass.' + button +
                                         ' == ' + ('OriginalClose' if button == 'OnBtnCloseClick'
                                                 else 'OriginalBlack')))


class NativeFrontlineLua53Checks(unittest.TestCase):
    @staticmethod
    def fixture():
        from lupa.lua53 import LuaRuntime
        runtime = LuaRuntime(unpack_returned_tuples=True)
        runtime.execute(r'''
        Account = {uid='3133546747002289',source=2289,selfServer=2289,current=2289}
        LuaEntry = {Player = {uid=Account.uid,
          GetSourceServerId=function() return Account.source end,
          GetSelfServerId=function() return Account.selfServer end,
          GetCurServerId=function() return Account.current end}}
        PVEType = {Parkour=4}
        UIWindowNames = {UIParkourBattleMain='Parkour'}
        UIManager = {GetInstance=function() return
          {IsWindowOpen=function(self,name) return name=='Parkour' end} end}
        Team = {defaultHeroIds={203},teamUnits={{guid=1,originalHeroId=203,
          hero={heroId=203,level=12}}}}
        Logic = {team=Team, param={fromActFrontBreakSunday=true,
          frontBreakSundayActId=114}, GetMemberCount=function(self)
          return #self.team.teamUnits end}
        Team.logic = Logic
        Team.GetMemberCount = function(self) return #self.teamUnits end
        Sends = 0
        Logic.AddMember = function(self,heroId,level,fromNative)
          Sends = Sends + 1
          table.insert(self.team.teamUnits, {guid=1+Sends,
            originalHeroId=heroId, hero={heroId=heroId,level=level}})
        end
        DataCenter = {LWBattleManager={logic=Logic,curLogic=Logic,
          gameStart=true,gameOver=false,gamePause=false,
          GetCurBattleType=function() return 4 end,
          GetCurBattleLogic=function(self) return self.logic end,
          IsBattleFinish=function() return false end}}
        ''')
        mini = runtime.execute((ROOT / 'feature018_mini_game.lua').read_text(encoding='utf-8'))
        return runtime, mini

    @staticmethod
    def call(runtime, mini):
        return mini.FrontlineReinforce(runtime.table_from({'action': 'frontlineReinforce',
                                                         'slot': '5'}))

    def test_non_sunday_parkour_and_other_owner_do_not_add_members(self):
        for expression in ("Logic.param.fromActFrontBreakSunday=false",
                           "Logic.param.frontBreakSundayActId=nil",
                           "LuaEntry.Player.uid='3133546747002212'",
                           "Account.current=2212"):
            runtime, mini = self.fixture()
            runtime.execute(expression)
            self.assertEqual(self.call(runtime, mini)['state'], 'unsupported')
            self.assertEqual(runtime.globals().Sends, 0)

    def test_real_sunday_context_adds_exactly_five_distinct_native_guids(self):
        runtime, mini = self.fixture()
        result = self.call(runtime, mini)
        self.assertEqual(result['state'], 'confirmed')
        self.assertEqual(result['data']['addedMembers'], 5)
        self.assertEqual(result['data']['observedCreatedUnits'], 5)
        self.assertEqual(result['data']['frontBreakSundayActId'], 114)
        self.assertFalse(result['data']['serverAckObserved'])
        self.assertEqual(result['data']['observationScope'], 'native_local_battle')
        self.assertEqual(runtime.globals().Sends, 5)
        self.assertEqual(len(runtime.globals().Team.teamUnits), 6)

    def test_owner_change_mid_reinforcement_stops_before_third_addition(self):
        runtime, mini = self.fixture()
        runtime.execute('''local old=Logic.AddMember
            Logic.AddMember=function(...)
              old(...)
              if Sends == 2 then Account.selfServer=2212 end
            end''')
        result = self.call(runtime, mini)
        self.assertEqual(result['state'], 'failed')
        self.assertEqual(result['data']['observedAdded'], 2)
        self.assertEqual(runtime.globals().Sends, 2)


if __name__ == "__main__":
    unittest.main()
