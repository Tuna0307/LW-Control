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
    uid='2553945035002288', source=2288, selfServer=2288, current=2288,
    position=501234, alliance=true, focusClear=true, itemCalls=0,
    sends=0, dialogs=0, relocations=0,
    standard=2, season=0, allianceItem=1, allianceUid='7070000123456789',
    rallyReads=0, recommendType=701,
}
MarkType={Alliance_rally=701, Alliance_OtherServerRally=702}
local function fake_mark(kind,server,point)
    return {allianceId=world.allianceUid, type=kind, server=server,
            pos=point*10+3,
            GetPointIndex=function(self) return math.floor(self.pos / 10) end}
end
world.homeRally=fake_mark(MarkType.Alliance_rally,2288,514999)
world.otherRally=nil
LuaEntry = {Player={
    uid=world.uid,
    GetSourceServerId=function() return world.source end,
    GetSelfServerId=function() return world.selfServer end,
    GetCurServerId=function() return world.current end,
    GetMainWorldPos=function() return world.position end,
    IsInAlliance=function() return world.alliance end,
    GetAllianceUid=function() return world.allianceUid end,
}}
SpecialItemId={ITEM_MOVE_CITY=5001, SEASON_LIMIT_MOVE_CITY=5002,
               ITEM_ALLIANCE_CITY_MOVE=5003}
DataCenter={ItemData={GetItemById=function(self,id)
    world.itemCalls=world.itemCalls+1
    if id==5001 and world.standard>0 then return {count=world.standard,uuid=100001} end
    if id==5002 and world.season>0 then return {count=world.season,uuid=100002} end
    if id==5003 and world.allianceItem>0 then return {count=world.allianceItem,uuid=100003} end
    return nil
end}, AllianceRallyPointDataManager={
    GetMyAllianceRallyPoint=function(self,kind)
        world.rallyReads=world.rallyReads+1
        if kind==MarkType.Alliance_rally then return world.homeRally end
        if kind==MarkType.Alliance_OtherServerRally then return world.otherRally end
    end,
    GetRecommendType=function(self) return world.recommendType end,
}}
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
                  "launchSessionId": "launch", "challenge": "challenge", "gamePid": "8123",
                  "authorizedUid": "2553945035002288", "preflightOnly":"1"}
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
        self.assertEqual(data["sourceServerId"], 2288)
        self.assertEqual(data["worldPointId"], 501234)
        self.assertEqual(data["itemCounts"]["ITEM_MOVE_CITY"]["count"], 2)
        self.assertEqual(data["itemCounts"]["SEASON_LIMIT_MOVE_CITY"]["count"], 0)
        self.assertEqual(data["itemCounts"]["ITEM_ALLIANCE_CITY_MOVE"]["count"], 1)
        self.assertTrue(data["noActionDispatched"])
        self.assertTrue(data["standardItemInventoryComplete"])
        self.assert_no_side_effects(state)

    def test_unrelated_city_item_id_missing_does_not_hide_owner_snapshot(self):
        runtime, actions, state, call = scenario()
        runtime.execute("SpecialItemId.SEASON_LIMIT_MOVE_CITY=nil")
        result = actions.Snapshot()
        self.assertEqual(result["state"], "confirmed")
        self.assertFalse(result["data"]["standardItemInventoryComplete"])
        self.assertFalse(result["data"]["itemCounts"]["SEASON_LIMIT_MOVE_CITY"]["available"])
        self.assertEqual(result["data"]["worldPointId"], 501234)
        self.assertEqual(call("randomRelocate")["state"], "unsupported")
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
            self.assertFalse(result["data"]["worldMvAcknowledged"])
            self.assertFalse(result["data"]["serverAckAttributable"])
        self.assert_no_side_effects(state)

    def test_f10_native_local_rally_owner_bound_with_direct_send_warning(self):
        _, _, state, call = scenario()
        self.assertEqual(state["rallyReads"], 0)
        result = call("allianceRelocate")
        self.assertEqual(result["state"], "unsupported")
        rally = result["data"]["allianceRally"]
        self.assertEqual(rally["nativePath"], "direct_alliance_move_send_risk")
        self.assertTrue(rally["nativeDirectSendRisk"])
        self.assertFalse(rally["nativeChooserWouldOpen"])
        self.assertFalse(rally["selectedTargetValidated"])
        self.assertFalse(rally["worldMvAcknowledged"])
        self.assertEqual(rally["homeRally"]["worldPointId"], 514999)
        self.assertEqual(rally["homeRally"]["serverId"], 2288)
        self.assertTrue(rally["homeRally"]["authorizedHomeServer"])
        self.assertIsNone(rally["otherServerRally"])
        self.assertEqual(state["rallyReads"], 2)
        self.assert_no_side_effects(state)

    def test_f10_other_server_rally_ui_path_unsafe_without_selected_target(self):
        runtime, _, state, call = scenario()
        runtime.execute("""world.otherRally={allianceId=world.allianceUid,
            type=MarkType.Alliance_OtherServerRally,server=2212,pos=300021,
            GetPointIndex=function(self) return math.floor(self.pos/10) end}""")
        result = call("allianceRelocate")
        rally = result["data"]["allianceRally"]
        self.assertEqual(result["state"], "unsupported")
        self.assertEqual(rally["nativePath"], "choose_rally_point_ui")
        self.assertTrue(rally["nativeChooserWouldOpen"])
        self.assertFalse(rally["nativeDirectSendRisk"])
        self.assertFalse(rally["otherServerRally"]["authorizedHomeServer"])
        self.assertFalse(rally["selectedTargetValidated"])
        self.assert_no_side_effects(state)

    def test_f10_native_rally_change_observed_unattributed_and_owner_mismatch_rejected(self):
        _, _, state, call = scenario()
        self.assertEqual(call("allianceRelocate")["state"], "unsupported")
        state["homeRally"]["pos"] = 888881
        later = call("allianceRelocate", query=True)
        self.assertTrue(later["data"]["rallyObservationChanged"])
        self.assertEqual(later["data"]["beforeWorldPointId"], 501234)
        self.assertEqual(later["data"]["afterWorldPointId"], 501234)
        self.assertFalse(later["data"]["changeAttributedToHotkey"])
        self.assertEqual(later["data"]["allianceRally"]["homeRally"]["worldPointId"], 88888)
        state["homeRally"]["allianceId"] = "999999"
        bad = call("allianceRelocate", query=True)
        self.assertEqual(bad["state"], "unsupported")
        self.assertIn("owner/type/server/point mismatch", bad["error"])
        self.assertEqual(call("allianceRelocate", query=True)["state"], "unsupported")
        self.assert_no_side_effects(state)

    def test_f10_invalid_marks_must_not_claim_target_or_ack(self):
        runtime, _, state, call = scenario()
        state["alliance"] = False
        self.assertIn("membership required", call("allianceRelocate")["error"])
        state["alliance"] = True
        state["homeRally"]["server"] = None
        self.assertIn("rally mark owner/type/server/point mismatch", call("allianceRelocate")["error"])
        state["homeRally"]["server"] = 2288
        state["homeRally"]["type"] = 702
        self.assertIn("rally mark owner/type/server/point mismatch", call("allianceRelocate")["error"])
        state["homeRally"]["type"] = 701
        state["homeRally"]["GetPointIndex"] = runtime.eval("function() return 42 end")
        self.assertIn("rally mark owner/type/server/point mismatch", call("allianceRelocate")["error"])
        self.assert_no_side_effects(state)

    def test_alliance_owner_switch_invalidates_target_baseline_even_same_point(self):
        _, _, state, call = scenario()
        self.assertEqual(call("allianceRelocate")["state"], "unsupported")
        state["allianceUid"] = "8080000123456789"
        state["homeRally"]["allianceId"] = state["allianceUid"]
        result = call("allianceRelocate", query=True)
        self.assertEqual(result["state"], "failed")
        self.assertIn("alliance owner changed", result["error"])
        self.assertEqual(call("allianceRelocate", query=True)["state"], "failed")
        new = call("allianceRelocate")
        self.assertEqual(new["state"], "unsupported")
        self.assertEqual(new["data"]["allianceRally"]["ownerAllianceUid"], state["allianceUid"])
        self.assertFalse(new["data"]["rallyObservationChanged"])
        self.assert_no_side_effects(state)

    def test_f9_focus_callback_error_fails_closed(self):
        runtime, actions, state, _ = scenario()
        data = runtime.table_from({"action":"randomRelocate", "slot":"0",
            "profileId":"owner", "launchSessionId":"launch",
            "challenge":"challenge", "gamePid":"8123",
            "authorizedUid":"2553945035002288"})
        exploding = runtime.eval("function() error('focus query failed') end")
        result = actions.RandomRelocate(data, exploding)
        self.assertEqual(result["state"], "failed")
        self.assertEqual(result["errorCode"], "GAME_PRECONDITION_FAILED")
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
        state["current"] = 2212
        reject = call("allianceRelocate")
        self.assertEqual(reject["state"], "unsupported")
        self.assertIn("server identity disagrees", reject["error"])
        self.assertEqual(actions.Snapshot()["state"], "unsupported")
        state["current"] = 2288
        state["source"] = 2212
        self.assertEqual(call("allianceRelocate")["state"], "unsupported")
        state["source"] = 2288
        runtime.globals().LuaEntry.Player.uid = "3133546747002212"
        self.assertIn("request UID does not match native owner", call("allianceRelocate")["error"])
        runtime.globals().LuaEntry.Player.uid = "2553945035002288"
        # An unparseable native UID is rejected; the fixture's native Player
        # property must change, not just the backing state used by methods.
        runtime.globals().LuaEntry.Player.uid = "not-an-uid"
        self.assertIn("owner", call("allianceRelocate")["error"])
        self.assert_no_side_effects(state)

    def test_missing_inventory_and_invalid_requests_fail_closed(self):
        runtime, actions, state, call = scenario()
        self.assertEqual(call("randomRelocate", profile="bad profile")["state"], "failed")
        self.assertEqual(call("allianceRelocate", focus=False)["state"], "failed")
        runtime.execute("SpecialItemId.ITEM_MOVE_RANDOM=5004")
        runtime.globals().DataCenter.ItemData.GetItemById = runtime.eval(
            "function(self,id) return {count=-1,uuid=30001} end")
        bad = call("randomRelocate")
        self.assertEqual(bad["state"], "unsupported")
        self.assertFalse(bad["data"]["standardItemInventoryComplete"])
        self.assertIn("No owned native relocation item",
                      bad["data"]["actionReadiness"]["itemBlocker"])
        runtime.globals().DataCenter.ItemData.GetItemById = None
        missing = call("randomRelocate")
        self.assertEqual(missing["state"], "unsupported")
        self.assertFalse(missing["data"]["standardItemInventoryComplete"])
        self.assertIn("No owned native relocation item",
                      missing["data"]["actionReadiness"]["itemBlocker"])
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

    def test_source_verified_rally_readers_mark_fields_and_choice_dispatch(self):
        manager = "DataCenter/AllianceData/AllianceRallyPointDataManager.luac"
        digest = "8d970b439bfb0acf3033b4375da1731607a3879b063f24b652010ef5652d73f6"
        self.check_method(manager, "GetMyAllianceRallyPoint", digest,
                          ("MarkType", "Alliance_rally", "markDataDic"))
        self.check_method(manager, "GetRecommendType", digest,
                          ("recommendType", "MarkType", "Alliance_rally"))
        self.check_method("DataCenter/WorldFavoData/AllianceMarkData.luac", "ParseData",
                          "b08cc1e15a9fd57d3cc7132000cd081e2ac5ea4473e6243c0ff8d36f63b00578",
                          ("allianceId", "markType", "type", "point", "pos", "serverId", "server"))
        self.check_method("DataCenter/WorldFavoData/AllianceMarkData.luac", "GetPointIndex",
                          "b08cc1e15a9fd57d3cc7132000cd081e2ac5ea4473e6243c0ff8d36f63b00578",
                          ("pos",))
        self.check_method("UI/UILWAlliance/UIChooseRallyPoint/UIChooseRallyPointView.luac",
                          "OnBtnMoveClick", "2c66d46152e1843245b9f45db3b3f028437134fb186da34395ee53dfe0bce8c5",
                          ("AllianceMoveCityToSelectedRallyPoint", "curType", "costType", "pinMsgUUid"))

    def test_original_ui_manager_open_window_can_silently_skip_and_native_visibility_is_separate(self):
        digest="0161c2f07e6d6da3abdeb224e6b8d95139791b55fb32f6d57f6a516043927c5d"
        opener=self.check_method('Framework/UI/UIManager.luac','OpenWindow',digest,
            ('IsCanOpenUI','windows','OpenWindow, GuideManager can not open : '))
        self.assertTrue(any('sourceLine=892' in line and 'RETURN' in line
                            for line in disassemble(opener)))
        visibility=self.check_method('Framework/UI/UIManager.luac','IsWindowOpen',
            digest,('windows','State'))
        lines=disassemble(visibility)
        self.assertTrue(any('sourceLine=1488' in line for line in lines))
        view=self.check_method('UI/UILWAlliance/UIChooseRallyPoint/UIChooseRallyPointView.luac',
            'OnChangeSelectRallyPoint',
            '2c66d46152e1843245b9f45db3b3f028437134fb186da34395ee53dfe0bce8c5',
            ('curType',))
        self.assertIn('curType',view['constants'])

    def test_original_reply_handlers_update_inventory_and_city_from_server_data(self):
        self.check_method('DataCenter/ItemManager/ItemManager.luac','ItemUseHandle',
            'febee83973a019ce47dc154190a7bae182b6ea5d5546b63c7f412ec60222847c',
            ('errorCode','itemId','uuid','count','UpdateOneItem','OnUseRet'))
        self.check_method('DataCenter/BuildManager/BuildManager.luac','WorldMvHandle',
            '537c896000bc29d07758dcc7e6f75255fb7c06fce5a6e01afdb20773fbcc37c3',
            ('errorCode','itemCostArr','UpdateOneItem','worldMainPoint','SetMainWorldPointId','buildInfo','pId'))

    def test_original_f9_item_reply_may_omit_stack_uuid_and_fallback_to_item_id(self):
        # Original ItemUseHandle may update an inventory stack from itemId
        # alone; such a reply is not an identifiable ACK for our owned UUID.
        body=self.check_method('DataCenter/ItemManager/ItemManager.luac','ItemUseHandle',
            'febee83973a019ce47dc154190a7bae182b6ea5d5546b63c7f412ec60222847c',
            ('GetItemByUuid','GetItemById','itemId','uuid','UpdateOneItem'))
        trace=disassemble(body)
        self.assertTrue(any('sourceLine=58' in line and 'GetItemByUuid' in line for line in trace))
        self.assertTrue(any('sourceLine=63' in line and 'GetItemById' in line for line in trace))
        # An item-change response can name the very same stack UUID without
        # representing our normal F9 ItemUse. The original distinguishes
        # opType=1 from the normal-use path before UpdateOneItem/OnUseRet.
        self.assertTrue(any('sourceLine=51' in line and "'opType'" in line
                            for line in trace))
        self.assertTrue(any('sourceLine=51' in line and '=1' in line
                            for line in trace))

    def test_original_random_and_alliance_owned_item_senders_and_warning_conditions(self):
        # Full original artifact identity plus exact source closures; no guessed item IDs.
        name='UI/UILWBag/UILWBagMain/Controller/UILWBagMainCtrl.luac'
        self.assertEqual(hashlib.sha256(self.members[name]).hexdigest(),
                         'aa7f2ebbc31ed8c0ef600ecc55659183d125368d3f572b3ad6f3414c0a91a48a')
        root=lua53.parse_chunk(probe.decode_lenc(self.members[name]))
        body=root['children'][6]
        for token in ('ITEM_MOVE_RANDOM','LW_ITEM_ALLY_MOVE_CITY','SeasonHunterManager',
                      'LWZombieRushManager','SEASON_VIRUS_MAX_EXPLODE','SELF_EXPLOSION_STATUS_ID'):
            self.assertIn(token,body['constants'])
        random=body['children'][12]
        self.assertIn('ItemUse',random['constants']);self.assertIn('uuid',random['constants'])
        self.assertIn('num',random['constants']);self.assertIn('useItemFromType',random['constants'])
        alliance=body['children'][11]
        self.assertIn('TryAllianceMoveCity',alliance['constants']);self.assertIn(4,alliance['constants'])
        for index,warning in ((13,'NeedNoticeRandomMoveCity'),(15,'NeedNoticeAllianceMoveCityDropMine'),
                              (17,'NeedNoticeAllianceMoveCityPosition')):
            self.assertIn(warning,body['children'][index]['constants'])

    def test_original_f10_source_home_uses_move_cross_server_not_alliance_ack(self):
        # MoveCityUtil's source==self branch on the authorized 2288 home world
        # invokes MoveCityFromOtherToSource, not the line-136 AllianceMoveCity.
        # The latter branch and its reason-240 reply are never a valid
        # matching ACK for the source==self path.
        move=self.check_method('Util/MoveCityUtil.luac','AllianceMoveCityToSourceRallyPoint',
            'd53c624e4dcc87b6228f8ac650704868e321332614d36321b88800b5f8562de4',
            ('AllianceMoveCity','SendMessage','GetSourceServerId','GetSelfServerId',
             'GetIsInCity','ChangeToWorld'))
        self.assertEqual(move['upvalueNames'][1],'MoveCityFromOtherToSource')
        self.assertTrue(any('sourceLine=133' in row and 'GETUPVAL' in row
                            for row in disassemble(move)))
        self.assertTrue(any("sourceLine=136" in row and "AllianceMoveCity" in row
                            for row in disassemble(move)))
        raw=self.members['Util/MoveCityUtil.luac']
        root=lua53.parse_chunk(probe.decode_lenc(raw))
        cross=root['children'][0]
        for token in ('MoveCrossServer','BackToSrcServer','BackToSrcServerBigMap',
                      'CanFreeAllianceMove','useCrossItem','dstPoint'):
            self.assertIn(token,cross['constants'])
        self.assertTrue(any('sourceLine=16' in row and 'MoveCrossServer' in row
                            for row in disassemble(cross)))
        other=root['children'][5]
        for token in ('AllianceMoveCity','GetSelfServerId',
                      'Alliance_OtherServerRally'):
            self.assertIn(token,other['constants'])
        self.assertEqual(other['upvalueNames'][1],'MoveCityFromOtherToRally')
        self.assertTrue(any('sourceLine=99' in row and 'AllianceMoveCity' in row
                            for row in disassemble(other)))
        # The selected foreign rally route has a *conditional* native
        # MoveCrossServer path when its mark server == owner self server,
        # but otherwise sends AllianceMoveCity (still a potentially
        # cross-server gameplay action). Neither branch is authorized here.
        cross_foreign=root['children'][1]
        for token in ('MoveCrossServer','OtherAllianceRallyPoint','dstPoint',
                      'useCrossItem','CanFreeAllianceMove'):
            self.assertIn(token,cross_foreign['constants'])
        self.assertTrue(any('sourceLine=30' in row and 'MoveCrossServer' in row
                            for row in disassemble(cross_foreign)))
        chooser=root['children'][2]
        for token in ('UIChooseRallyPoint','OpenWindow','costType','isInviteMove',
                      'pinMsgUUid','Alliance_OtherServerRally'):
            self.assertIn(token,chooser['constants'])
        self.assertTrue(any('sourceLine=47' in row and 'OpenWindow' in row
                            for row in disassemble(chooser)))
        reply=self.check_method('Net/Msgs/Alliance/AllianceMoveCityMessage.luac',
            'HandleMessage','dfed59efaad7ec93cb0b89be67ff5d5e6720443b7ac3332f1ef5f86bb5a84579',
            ('WorldMvHandle','result','errorCode'))
        lines=disassemble(reply)
        self.assertTrue(any('sourceLine=42' in line and 'WorldMvHandle' in line
                            for line in lines))
        self.assertTrue(any('sourceLine=42' in line and '240' in line
                            for line in lines))
        serializer=self.check_method('Net/Msgs/ItemUseMessage.luac','OnCreate',
            '9313f37b8183fd7338fcfc82cfd3a0d211db1d9cc5ac67fc5d0692de78262a1c',
            ('PutUtfString','uuid','num','useItemFromType'))
        self.assertTrue(any('sourceLine=16' in line and 'PutUtfString' in line
                            for line in disassemble(serializer)))



class RelocationActionChecks(unittest.TestCase):
    def fixture(self):
        lua,actions,state,_=scenario()
        lua.execute("""
        SpecialItemId.ITEM_MOVE_RANDOM=5004
        SpecialItemId.LW_ITEM_ALLY_MOVE_CITY=5005
        world.randomCount=2;world.allyCount=2
        local oldGet=DataCenter.ItemData.GetItemById
        function DataCenter.ItemData:GetItemById(id)
          if id==5004 then
            if world.randomCount>0 then return {uuid=100004,itemId=5004,count=world.randomCount} end
            return nil
          end
          if id==5005 then
            if world.allyCount>0 then return {uuid=100005,itemId=5005,count=world.allyCount} end
            return nil
          end
          return oldGet(self,id)
        end
        BattleFieldUtil={InBattleField=function()return false end}
        UIUtil={CheckItemIsExpiredByUuid=function()return false end}
        DataCenter.SeasonHunterManager={IsInBattle=function()return false end}
        DataCenter.LWZombieRushManager={IsChallenging=function()return false end}
        DataCenter.ActMeteoriteBattleManager={
          NeedNoticeRandomMoveCity=function()return false end,
          NeedNoticeAllianceMoveCityDropMine=function()return false end,
          NeedNoticeAllianceMoveCityPosition=function()return false end}
        EffectDefine={SEASON_VIRUS_MAX_EXPLODE=15};SELF_EXPLOSION_STATUS_ID=16
        LuaEntry.Effect={GetGameEffect=function()return 0 end,HasStatus=function()return false end}
        SceneUtils={GetIsInCity=function()return false end}
        function LuaEntry.Player:IsLoginSourceServer()return true end
        DataCenter.ItemManager={ItemUseHandle=function(self,response)
          if not response.errorCode and response.itemId==5004 then world.randomCount=world.randomCount-1 end
        end}
        DataCenter.BuildManager={WorldMvHandle=function(self,response)
          if not response.errorCode then world.allyCount=world.allyCount-1;world.position=response.worldMainPoint end
        end}
        world.itemOriginal=DataCenter.ItemManager.ItemUseHandle
        world.moveOriginal=DataCenter.BuildManager.WorldMvHandle
        MsgDefines={ItemUse='itemuse',AllianceMoveCity='allianceMoveCity'}
        SFSNetwork.SendMessage=function(msg,args)
          assert(msg=='itemuse' and args.uuid=='100004' and args.num==1)
          world.sends=world.sends+1
        end
        MoveCityUtil.AllianceMoveCityToSelectedRallyPoint=function(kind,cost,invite,pin)
          assert(kind==701 and cost==4 and invite==false and pin==nil)
          world.sends=world.sends+1
        end
        UIWindowNames={UIChooseRallyPoint='native-choose-rally'}
        UIManager={GetInstance=function()return {
          IsWindowOpen=function(self,window)
            assert(window=='native-choose-rally')
            return world.chooserOpen==true
          end,
          OpenWindow=function(self,window,animation,parameters)
            assert(window=='native-choose-rally')
            assert(animation.anim==true and parameters.costType==4)
            assert(parameters.isInviteMove==false and parameters.pinMsgUUid==nil)
            world.dialogs=world.dialogs+1
            world.chooserOpen=true
          end
        }end}
        """)
        def call(action='randomRelocate',query=False):
            fields={'action':action,'slot':'0','profileId':'owner','launchSessionId':'launch',
                    'challenge':'challenge','gamePid':'8123',
                    'authorizedUid':'2553945035002288'}
            if query: fields['queryOnly']='1'
            func=actions.RandomRelocate if action=='randomRelocate' else actions.AllianceRelocate
            return func(lua.table_from(fields),lua.eval('function()return world.focusClear end'))
        return lua,actions,state,call

    def test_r8_readiness_confirms_exact_owner_f9_native_bag_packet_without_sending(self):
        lua,module,state,_=self.fixture()
        fields=lua.table_from({'action':'randomRelocate','slot':'0',
            'profileId':'owner','launchSessionId':'launch','challenge':'challenge',
            'gamePid':'8123','authorizedUid':'2553945035002288'})
        result=module.Readiness(fields,lua.eval('function()return true end'))
        self.assertEqual(result['state'],'confirmed')
        data=result['data']
        self.assertEqual(data['observation'],'original_v26_authenticated_relocation_readiness')
        self.assertEqual(data['action'],'randomRelocate')
        self.assertEqual(data['ownerUid'],'2553945035002288')
        self.assertEqual(data['sourceServerId'],2288)
        self.assertEqual(data['originalNativeRoute'],'ItemUse')
        self.assertEqual(data['itemType'],'ITEM_MOVE_RANDOM')
        self.assertEqual(data['itemUuid'],'100004')
        self.assertTrue(data['nativeItemUseRequestReady'])
        self.assertTrue(data['actionable'])
        self.assertTrue(data['readOnly'])
        for prop in ('originalPacketSend','requestDispatched',
                     'serverReplyObserved','relocationConfirmed',
                     'serverAckAttributable','destinationVerified'):
            self.assertFalse(data[prop],prop)
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],0)

    def test_r8_readiness_f10_same_home_route_is_never_misidentified_as_alliance_move(self):
        lua,module,state,_=self.fixture()
        fields=lua.table_from({'action':'allianceRelocate','slot':'0',
            'profileId':'owner','launchSessionId':'launch','challenge':'challenge',
            'gamePid':'8123','authorizedUid':'2553945035002288'})
        def query():
            return module.Readiness(fields,lua.eval('function()return true end'),
                                    'allianceRelocate')
        result=query()
        self.assertEqual(result['state'],'confirmed')
        data=result['data']
        self.assertEqual(data['action'],'allianceRelocate')
        self.assertEqual(data['originalNativeRoute'],'MoveCrossServer_BackToSrcServer')
        self.assertFalse(data['actionable'])
        self.assertFalse(data['chooserRequestEligible'])
        self.assertFalse(data['requestDispatched'])
        self.assertFalse(data['originalPacketSend'])
        self.assertIn('MoveCrossServer',data['currentBlocker'])
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,
          server=2213,pos=6610023,
          GetPointIndex=function(self)return math.floor(self.pos/10)end}
        """)
        chosen=query()
        self.assertEqual(chosen['state'],'confirmed')
        view=chosen['data']
        self.assertEqual(view['originalNativeRoute'],'UIChooseRallyPoint')
        self.assertTrue(view['chooserRequestEligible'])
        self.assertTrue(view['ownerSelectionRequired'])
        self.assertFalse(view['actionable'])
        self.assertFalse(view['originalPacketSend'])
        self.assertFalse(view['chooserVisibleVerified'])
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],0)
        state['chooserOpen']=True
        preexisting=query()
        self.assertEqual(preexisting['state'],'confirmed')
        self.assertFalse(preexisting['data']['chooserRequestEligible'])
        self.assertTrue(preexisting['data']['chooserVisibleVerified'])
        self.assertIn('already open',preexisting['data']['currentBlocker'])
        state['chooserOpen']=False
        state['allyCount']=0
        no_stock=query()
        self.assertEqual(no_stock['state'],'confirmed')
        self.assertFalse(no_stock['data']['chooserRequestEligible'])
        self.assertFalse(no_stock['data']['actionable'])
        self.assertEqual(state['dialogs'],0)

    def test_r8_readiness_missing_stock_focus_warning_and_foreign_account_fail_closed(self):
        for variable,change in (
            ('inventory','world.randomCount=0'),
            ('focus','world.focusClear=false'),
            ('warning','DataCenter.SeasonHunterManager.IsInBattle=function()return true end'),
            ('sender','SFSNetwork.SendMessage=nil'),
        ):
            with self.subTest(variable=variable):
                lua,module,state,_=self.fixture()
                lua.execute(change)
                fields=lua.table_from({'action':'randomRelocate','slot':'0',
                    'profileId':'owner','launchSessionId':'launch',
                    'challenge':'challenge','gamePid':'8123',
                    'authorizedUid':'2553945035002288'})
                result=module.Readiness(fields,
                    lua.eval('function()return world.focusClear end'))
                self.assertEqual(result['state'],'confirmed')
                self.assertFalse(result['data']['actionable'])
                self.assertFalse(result['data']['requestDispatched'])
                self.assertTrue(result['data']['currentBlocker'])
                self.assertEqual(state['sends'],0)
        lua,module,state,_=self.fixture()
        fields=lua.table_from({'action':'randomRelocate','slot':'0',
            'profileId':'owner','launchSessionId':'launch','challenge':'challenge',
            'gamePid':'8123','authorizedUid':'2553945035002288'})
        lua.execute('world.current=2212')
        self.assertEqual(module.Readiness(
            fields,lua.eval('function()return true end'))['state'],'unsupported')
        lua.execute('world.current=2288;LuaEntry.Player.uid="3133546747002212"')
        self.assertEqual(module.Readiness(
            fields,lua.eval('function()return true end'))['state'],'failed')
        self.assertEqual(state['sends'],0)

    def test_any_authenticated_uid_and_server_can_request_f9_with_matching_identity(self):
        lua,module,state,_=self.fixture()
        lua.execute("""
          LuaEntry.Player.uid='3133546747002212'
          world.source=2212;world.selfServer=2212;world.current=2212
        """)
        request=lua.table_from({'action':'randomRelocate','slot':'0',
            'profileId':'owner','launchSessionId':'launch','challenge':'challenge',
            'gamePid':'8123','authorizedUid':'3133546747002212'})
        readiness=module.Readiness(request,lua.eval('function()return true end'))
        self.assertEqual(readiness['state'],'confirmed')
        self.assertTrue(readiness['data']['actionable'])
        self.assertEqual(readiness['data']['sourceServerId'],2212)
        self.assertEqual(module.RandomRelocate(request,lua.eval('function()return true end'))['state'],'accepted')
        self.assertEqual(state['sends'],1)

    def test_mismatched_request_uid_never_uses_another_players_item_or_chooser(self):
        for action in ('randomRelocate','allianceRelocate'):
            lua,module,state,_=self.fixture()
            forged=lua.table_from({'action':action,'slot':'0',
                'profileId':'owner','launchSessionId':'launch','challenge':'challenge',
                'gamePid':'8123','authorizedUid':'3133546747002212'})
            method=module.RandomRelocate if action=='randomRelocate' else module.AllianceRelocate
            self.assertEqual(method(forged,lua.eval('function()return true end'))['state'],'failed')
            self.assertEqual(module.Readiness(forged,lua.eval('function()return true end'))['state'],'failed')
            self.assertEqual(state['sends'],0)
            self.assertEqual(state['dialogs'],0)

    def test_readiness_during_coherent_server_transition_never_reports_actionable(self):
        lua,module,state,_=self.fixture()
        request=lua.table_from({'action':'randomRelocate','slot':'0',
            'profileId':'owner','launchSessionId':'launch','challenge':'challenge',
            'gamePid':'8123','authorizedUid':'2553945035002288'})
        changed=lua.eval("""
          function()
            world.source=2212;world.selfServer=2212;world.current=2212
            return true
          end
        """)
        result=module.Readiness(request,changed)
        self.assertEqual(result['state'],'failed')
        self.assertIn('changed during readiness',result['error'])
        self.assertEqual(state['sends'],0)

    def test_f9_send_return_after_server_transition_keeps_no_retry_tombstone(self):
        lua,module,state,call=self.fixture()
        lua.execute("""
          SFSNetwork.SendMessage=function(...)
            world.sends=world.sends+1
            world.source=2212;world.selfServer=2212;world.current=2212
          end
        """)
        first=call()
        self.assertEqual(first['state'],'failed')
        self.assertIn('changed during F9',first['error'])
        self.assertEqual(state['sends'],1)
        lua.execute('world.source=2288;world.selfServer=2288;world.current=2288')
        self.assertEqual(call()['state'],'accepted')
        self.assertEqual(state['sends'],1)

    def test_r8_readiness_rejects_forged_action_and_session_without_side_effects(self):
        lua,module,state,_=self.fixture()
        base={'action':'randomRelocate','slot':'0','profileId':'owner',
              'launchSessionId':'launch','challenge':'challenge','gamePid':'8123',
              'authorizedUid':'2553945035002288'}
        for name,override in (
            ('wrong action',{'action':'allianceRelocate'}),
            ('bad slot',{'slot':'9'}),
            ('forged profile',{'profileId':'other/profile'}),
        ):
            with self.subTest(name=name):
                req={**base,**override}
                result=module.Readiness(lua.table_from(req),
                    lua.eval('function()return true end'),'randomRelocate')
                self.assertEqual(result['state'],'failed')
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],0)

    def test_f9_owned_random_item_single_send_reply_and_fresh_position(self):
        lua,_,state,call=self.fixture()
        result=call();self.assertEqual(result['state'],'accepted');self.assertEqual(state['sends'],1)
        self.assertEqual(call(query=True)['state'],'accepted');self.assertEqual(state['sends'],1)
        lua.execute('world.position=600123')
        self.assertEqual(call(query=True)['state'],'accepted')
        lua.execute('DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004})')
        final=call(query=True);self.assertEqual(final['state'],'confirmed')
        self.assertTrue(final['data']['relocationConfirmed']);self.assertTrue(final['data']['replyObserved'])
        self.assertTrue(final['data']['requestDispatched'])
        self.assertTrue(final['data']['replyExactItemUuidObserved'])
        self.assertTrue(final['data']['replyUseOperationCompatible'])
        self.assertTrue(final['data']['independentWorldAndInventoryPoststateVerified'])
        self.assertTrue(final['data']['oneSendNoRetry'])
        self.assertFalse(final['data']['clientRequestNonceAvailable'])
        self.assertFalse(final['data']['serverAckAttributable'])
        self.assertEqual(state['randomCount'],1)
        self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle==world.itemOriginal'))

    def test_f9_original_item_action_not_blocked_by_unrelated_city_item_types(self):
        lua,_,state,call=self.fixture()
        lua.execute('SpecialItemId.ITEM_MOVE_CITY=nil;SpecialItemId.SEASON_LIMIT_MOVE_CITY=nil')
        result=call()
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(state['sends'],1)

    def test_f10_original_alliance_action_not_blocked_by_unrelated_city_items(self):
        lua,_,state,call=self.fixture()
        lua.execute('SpecialItemId.ITEM_ALLIANCE_CITY_MOVE=nil;world.otherRally={allianceId=world.allianceUid,type=702,server=2213,pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}')
        result=call('allianceRelocate')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],1)

    def test_f9_exact_item_stack_changed_during_preflight_never_dispatches(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local original=DataCenter.ItemData.GetItemById
        local seen=0
        function DataCenter.ItemData:GetItemById(id)
          local item=original(self,id)
          if id==5004 then
            seen=seen+1
            if seen==2 then return {uuid=100006,itemId=5004,count=2} end
          end
          return item
        end
        """)
        result=call()
        self.assertEqual(result['state'],'failed')
        self.assertIn('consumable changed',result['error'])
        self.assertEqual(state['sends'],0)

    def test_f9_native_warning_becomes_active_after_initial_item_check(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local calls=0
        DataCenter.SeasonHunterManager.IsInBattle=function()
          calls=calls+1
          return calls>1
        end
        """)
        result=call()
        self.assertEqual(result['state'],'failed')
        self.assertIn('native relocation warning',result['error'].lower())
        self.assertEqual(state['sends'],0)

    def test_f10_chooser_mark_move_between_first_check_and_open_never_opens(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local original=DataCenter.AllianceRallyPointDataManager.GetMyAllianceRallyPoint
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,pos=6610023,
          GetPointIndex=function(self)return math.floor(self.pos/10)end}
        local remoteReads=0
        function DataCenter.AllianceRallyPointDataManager:GetMyAllianceRallyPoint(kind)
          if kind==702 then
            remoteReads=remoteReads+1
            if remoteReads==2 then world.otherRally.pos=5310023 end
          end
          return original(self,kind)
        end
        """)
        result=call('allianceRelocate')
        self.assertEqual(result['state'],'failed')
        self.assertIn('chooser mark changed',result['error'])
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],0)

    def test_f10_chooser_request_never_sends_even_if_native_city_scene(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,pos=6610023,
          GetPointIndex=function(self)return math.floor(self.pos/10)end}
        local sceneReads=0
        SceneUtils.GetIsInCity=function()
          sceneReads=sceneReads+1
          return true
        end
        """)
        result=call('allianceRelocate')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],1)

    def test_f10_same_home_native_move_cross_server_not_falsely_called_alliance_move(self):
        lua,_,state,call=self.fixture()
        response=call('allianceRelocate')
        self.assertEqual(response['state'],'unsupported')
        self.assertIn('MoveCrossServer',response['error'])
        self.assertEqual(state['sends'],0)
        lua.execute('DataCenter.BuildManager:WorldMvHandle({serverId=2288,result=1,worldMainPoint=514998},240)')
        result=call('allianceRelocate',True)
        self.assertEqual(result['state'],'failed')
        self.assertEqual(state['sends'],0)
        self.assertTrue(lua.eval('DataCenter.BuildManager.WorldMvHandle==world.moveOriginal'))

    def test_f10_other_server_native_choice_is_not_silently_bypassed(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={
          allianceId=world.allianceUid,type=702,server=2213,pos=6610023,
          GetPointIndex=function(self)return math.floor(self.pos/10)end,
        }
        """)
        response=call('allianceRelocate')
        self.assertEqual(response['state'],'accepted')
        self.assertTrue(response['data']['chooserRequestMade'])
        self.assertFalse(response['data']['chooserVisibleVerified'])
        self.assertTrue(response['data']['chooserWindowRegistered'])
        self.assertTrue(response['data']['chooserStillOpen'])
        self.assertFalse(response['data']['requestDispatched'])
        self.assertFalse(response['data']['relocationConfirmed'])
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(state['sends'],0)
        observed=call('allianceRelocate',query=True)
        self.assertEqual(observed['state'],'accepted')
        self.assertFalse(observed['data']['chooserVisibleVerified'])
        self.assertTrue(observed['data']['chooserWindowRegistered'])
        self.assertTrue(observed['data']['readOnly'])
        self.assertFalse(observed['data']['originalPacketSend'])
        self.assertFalse(observed['data']['ownerSelectedRallyObserved'])
        again=call('allianceRelocate')
        self.assertEqual(again['state'],'accepted')
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(state['sends'],0)

    def test_f10_chooser_open_accepted_without_actual_window_remains_unverified(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        UIManager.GetInstance=function()return {
          IsWindowOpen=function(self,name)return world.chooserOpen==true end,
          OpenWindow=function(self)world.dialogs=world.dialogs+1 end
        }end
        """)
        first=call('allianceRelocate')
        self.assertEqual(first['state'],'accepted')
        self.assertFalse(first['data']['chooserVisibleVerified'])
        self.assertFalse(first['data']['chooserWindowRegistered'])
        self.assertFalse(first['data']['requestDispatched'])
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(call('allianceRelocate',query=True)['state'],'accepted')
        self.assertEqual(state['dialogs'],1)
        state['chooserOpen']=True
        later=call('allianceRelocate',query=True)
        self.assertTrue(later['data']['chooserWindowRegistered'])
        self.assertFalse(later['data']['chooserVisibleVerified'])
        self.assertFalse(later['data']['relocationConfirmed'])
        state['chooserOpen']=False
        closed=call('allianceRelocate',query=True)
        self.assertFalse(closed['data']['chooserStillOpen'])
        self.assertFalse(closed['data']['ownerSelectedRallyObserved'])
        self.assertFalse(closed['data']['relocationConfirmed'])
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(state['sends'],0)

    def test_f10_chooser_pending_blocks_new_F9_action_and_foreign_owner_observation(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        """)
        self.assertEqual(call('allianceRelocate')['state'],'accepted')
        wrong=call('randomRelocate')
        self.assertEqual(wrong['state'],'failed')
        self.assertIn('chooser request unresolved',wrong['error'])
        state['current']=2212
        denied=call('allianceRelocate',query=True)
        self.assertEqual(denied['state'],'failed')
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(state['sends'],0)

    def test_f10_chooser_pending_detects_native_rally_change_without_game_send(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        """)
        self.assertEqual(call('allianceRelocate')['state'],'accepted')
        state['otherRally']['pos']=7788993
        changed=call('allianceRelocate',query=True)
        self.assertEqual(changed['state'],'failed')
        self.assertIn('rally/owner changed',changed['error'])
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(state['sends'],0)

    def test_f10_chooser_requires_real_native_ui_method_and_does_not_fallback_to_send(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        UIManager.GetInstance=function()return {} end
        """)
        response=call('allianceRelocate')
        self.assertEqual(response['state'],'unsupported')
        self.assertIn('visibility reader unavailable',response['error'])
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],0)

    def test_f10_chooser_open_exception_is_uncertain_and_never_dispatches_rpc(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        UIManager.GetInstance=function()return {
          IsWindowOpen=function()return world.chooserOpen==true end,
          OpenWindow=function()
          world.dialogs=world.dialogs+1
          error('Window presentation uncertain')
        end} end
        """)
        response=call('allianceRelocate')
        self.assertEqual(response['state'],'failed')
        self.assertEqual(response['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertIn('chooser open outcome unknown',response['error'])
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],1)

    def test_final_native_owner_revalidation_after_focus_blocks_both_sends(self):
        for action in ('randomRelocate','allianceRelocate'):
            lua,module,state,_=self.fixture()
            if action=='allianceRelocate':
                lua.execute('world.otherRally={allianceId=world.allianceUid,type=702,server=2213,pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}')
            fn=module.RandomRelocate if action=='randomRelocate' else module.AllianceRelocate
            values={'action':action,'slot':'0','profileId':'owner',
                    'launchSessionId':'launch','challenge':'challenge','gamePid':'8123',
                    'authorizedUid':'2553945035002288'}
            focus=lua.eval("""
                function()
                  world.current=2212
                  return true
                end
            """)
            result=fn(lua.table_from(values),focus)
            self.assertEqual(result['state'],'failed')
            self.assertIn('server',result['error'])
            self.assertEqual(state['sends'],0)
            self.assertEqual(state['dialogs'],0)

    def test_f9_item_reply_without_exact_uuid_never_acknowledges(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        DataCenter.ItemManager.ItemUseHandle=function(self,response)
          if response.itemId==5004 and world.randomCount==2 then
            world.randomCount=1
          end
        end
        """)
        self.assertEqual(call()['state'],'accepted')
        lua.execute("world.position=600123;DataCenter.ItemManager:ItemUseHandle({itemId=5004})")
        pending=call(query=True)
        self.assertEqual(pending['state'],'accepted')
        self.assertFalse(pending['data']['replyObserved'])
        self.assertFalse(pending['data']['relocationConfirmed'])
        self.assertEqual(state['sends'],1)
        lua.execute('DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004})')
        # Missing UUID must not have burned the observer; a subsequent exact
        # matching response and observed poststate may be confirmed without
        # dispatching a second ItemUse.
        final=call(query=True)
        self.assertEqual(final['state'],'confirmed')
        self.assertEqual(state['sends'],1)

    def test_f9_same_uuid_but_nonuse_optype_cannot_confirm_or_trigger_second_send(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
          DataCenter.ItemManager.ItemUseHandle=function(self,response)
            if response.itemId==5004 and response.opType==0 and
               world.randomCount==2 then
              world.randomCount=1
            end
          end
        """)
        self.assertEqual(call()['state'],'accepted')
        lua.execute("""
          world.position=600123
          DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004,opType=1})
        """)
        denied=call(query=True)
        self.assertEqual(denied['state'],'accepted')
        self.assertFalse(denied['data']['replyObserved'])
        self.assertFalse(denied['data']['relocationConfirmed'])
        self.assertFalse(denied['data']['replyUseOperationCompatible'])
        self.assertEqual(state['sends'],1)
        for request_op in ("'1'",'2'):
            lua.execute("DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004,opType="+request_op+"})")
            pending=call(query=True)
            self.assertEqual(pending['state'],'accepted')
            self.assertFalse(pending['data']['replyObserved'])
            self.assertEqual(state['sends'],1)
        lua.execute("DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004,opType=0})")
        final=call(query=True)
        self.assertEqual(final['state'],'confirmed')
        self.assertEqual(state['sends'],1)

    def test_r10_readiness_during_unresolved_F9_never_offers_second_item_use(self):
        lua,module,state,call=self.fixture()
        values={'action':'randomRelocate','slot':'0',
                'profileId':'owner','launchSessionId':'launch',
                'challenge':'challenge','gamePid':'8123',
                'authorizedUid':'2553945035002288'}
        identity=lua.table_from(values)
        pre=module.Readiness(identity,lua.eval('function()return true end'))
        self.assertTrue(pre['data']['actionable'])
        self.assertEqual(call()['state'],'accepted')
        during=module.Readiness(identity,lua.eval('function()return true end'))
        self.assertEqual(during['state'],'confirmed')
        self.assertFalse(during['data']['actionable'])
        self.assertTrue(during['data']['oneSendFenceActive'])
        self.assertTrue(during['data']['originalItemUseOutstanding'])
        self.assertFalse(during['data']['nativeItemUseRequestReady'])
        self.assertIn('never resend',during['data']['currentBlocker'])
        self.assertEqual(call()['state'],'accepted')
        self.assertEqual(state['sends'],1)

    def test_r10_dispose_during_outstanding_F9_preserves_no_retry_tombstone(self):
        lua,module,state,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        module.Dispose()
        self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle==world.itemOriginal'))
        lua.execute('world.position=600123;DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004,opType=0})')
        result=call(query=True)
        self.assertEqual(result['state'],'accepted')
        self.assertTrue(result['data']['observerDetached'])
        self.assertFalse(result['data']['replyObserved'])
        self.assertFalse(result['data']['relocationConfirmed'])
        self.assertEqual(call()['state'],'accepted')
        self.assertEqual(state['sends'],1)

    def test_r10_readiness_during_native_F10_chooser_is_always_nonactionable(self):
        lua,module,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        """)
        self.assertEqual(call('allianceRelocate')['state'],'accepted')
        values={'action':'allianceRelocate','slot':'0',
                'profileId':'owner','launchSessionId':'launch',
                'challenge':'challenge','gamePid':'8123',
                'authorizedUid':'2553945035002288'}
        result=module.Readiness(lua.table_from(values),lua.eval('function()return true end'))
        self.assertEqual(result['state'],'confirmed')
        self.assertTrue(result['data']['oneSendFenceActive'])
        self.assertTrue(result['data']['nativeChooserRequestOutstanding'])
        self.assertFalse(result['data']['chooserRequestEligible'])
        self.assertFalse(result['data']['actionable'])
        self.assertIn('never reopen',result['data']['currentBlocker'])
        self.assertEqual(state['sends'],0)
        self.assertEqual(state['dialogs'],1)

    def test_readonly_readiness_exposes_exact_f9_and_f10_bag_items(self):
        lua,module,state,_=self.fixture()
        def probe(action):
            vals={'action':action,'slot':'0','profileId':'owner','launchSessionId':'launch',
                  'challenge':'challenge','gamePid':'8123',
                  'authorizedUid':'2553945035002288','preflightOnly':'1'}
            func=module.RandomRelocate if action=='randomRelocate' else module.AllianceRelocate
            return func(lua.table_from(vals),lua.eval('function()return true end'))
        random=probe('randomRelocate')
        self.assertEqual(random['state'],'unsupported')
        own=random['data']['actionReadiness']
        self.assertEqual(own['itemType'],'ITEM_MOVE_RANDOM')
        self.assertEqual(own['itemUuid'],'100004')
        self.assertEqual(own['itemId'],'5004')
        self.assertEqual(own['itemCount'],2)
        self.assertTrue(own['ownerItemPresent'])
        self.assertTrue(own['warningsClear'])
        self.assertFalse(own['requestDispatched'])
        ally=probe('allianceRelocate')
        self.assertEqual(ally['state'],'unsupported')
        data=ally['data']['actionReadiness']
        self.assertEqual(data['itemType'],'LW_ITEM_ALLY_MOVE_CITY')
        self.assertEqual(data['itemUuid'],'100005')
        self.assertTrue(data['worldSceneReady'])
        self.assertTrue(data['sourceServerLogin'])
        self.assertTrue(data['ownerItemPresent'])
        self.assertFalse(data['destinationVerified'])
        self.assertEqual(data['originalNativeRoute'],'MoveCrossServer_BackToSrcServer')
        self.assertFalse(data['chooserRequestEligible'])
        self.assertFalse(data['homeDirectRelocationEnabled'])
        self.assertEqual(state['sends'],0)

    def test_readonly_readiness_reports_missing_owned_item_without_send(self):
        lua,module,state,_=self.fixture()
        lua.execute('world.randomCount=0')
        vals={'action':'randomRelocate','slot':'0','profileId':'owner',
              'launchSessionId':'launch','challenge':'challenge','gamePid':'8123',
              'authorizedUid':'2553945035002288','preflightOnly':'1'}
        result=module.RandomRelocate(lua.table_from(vals),
                                     lua.eval('function()return true end'))
        self.assertEqual(result['state'],'unsupported')
        self.assertFalse(result['data']['actionReadiness']['ownerItemPresent'])
        self.assertIn('No owned native relocation item',
                      result['data']['actionReadiness']['itemBlocker'])
        self.assertEqual(state['sends'],0)

    def test_invalid_preflight_mode_does_not_dispatch_f9_or_f10(self):
        lua,module,state,_=self.fixture()
        for action in ('randomRelocate','allianceRelocate'):
            vals={'action':action,'slot':'0','profileId':'owner',
                  'launchSessionId':'launch','challenge':'challenge','gamePid':'8123',
                  'authorizedUid':'2553945035002288','preflightOnly':'0'}
            fn=module.RandomRelocate if action=='randomRelocate' else module.AllianceRelocate
            response=fn(lua.table_from(vals),lua.eval('function()return true end'))
            self.assertEqual(response['state'],'failed')
            self.assertIn('preflight mode',response['error'])
        self.assertEqual(state['sends'],0)

    def test_item_stack_replacement_cannot_confirm_same_item_id_count_delta(self):
        lua,_,state,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.execute("""
        local old=DataCenter.ItemData.GetItemById
        function DataCenter.ItemData:GetItemById(id)
          if id==5004 then return {uuid=999994,itemId=5004,count=1} end
          return old(self,id)
        end
        world.position=600123
        DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004})
        """)
        reply=call(query=True)
        self.assertEqual(reply['state'],'failed')
        self.assertIn('stack identity changed',reply['error'])
        self.assertEqual(state['sends'],1)

    def test_f10_unrelated_alliance_reply_is_not_an_ack_on_home_world(self):
        for reply in ('{serverId=2288,result=2,worldMainPoint=514998}',
                      '{serverId=2288,result=1,worldMainPoint=514998,buildInfo={pId=514999}}',
                      '{serverId=2288,result=1,worldMainPoint=514998}'):
            lua,_,state,call=self.fixture()
            self.assertEqual(call('allianceRelocate')['state'],'unsupported')
            lua.execute('DataCenter.BuildManager:WorldMvHandle('+reply+',240)')
            observed=call('allianceRelocate',True)
            self.assertEqual(observed['state'],'failed',reply)
            self.assertEqual(state['sends'],0,reply)

    def test_missing_warning_or_item_never_dispatches(self):
        lua,_,state,call=self.fixture()
        lua.execute('DataCenter.ActMeteoriteBattleManager.NeedNoticeRandomMoveCity=function()return true end')
        self.assertEqual(call()['state'],'failed');self.assertEqual(state['sends'],0)
        lua.execute('DataCenter.ActMeteoriteBattleManager.NeedNoticeRandomMoveCity=function()return false end;world.randomCount=0')
        self.assertEqual(call()['state'],'unsupported');self.assertEqual(state['sends'],0)

    def test_f10_no_source_home_rpc_even_if_external_world_move_occurs(self):
        lua,_,state,call=self.fixture()
        self.assertEqual(call('allianceRelocate')['state'],'unsupported')
        lua.execute('DataCenter.BuildManager:WorldMvHandle({serverId=2288,worldMainPoint=514998})')
        result=call('allianceRelocate',True)
        self.assertEqual(result['state'],'failed')
        self.assertEqual(state['sends'],0)

    def test_f10_deferred_city_or_missing_source_sender_rejected_without_dispatch(self):
        lua,_,state,call=self.fixture()
        lua.execute('SceneUtils.GetIsInCity=function()return true end')
        self.assertEqual(call('allianceRelocate')['state'],'unsupported')
        lua.execute('SceneUtils.GetIsInCity=function()return false end')
        lua.execute('function LuaEntry.Player:IsLoginSourceServer()return false end')
        self.assertEqual(call('allianceRelocate')['state'],'unsupported')
        lua.execute('function LuaEntry.Player:IsLoginSourceServer()return true end')
        lua.execute('MsgDefines.AllianceMoveCity=nil')
        self.assertEqual(call('allianceRelocate')['state'],'unsupported')
        self.assertEqual(state['sends'],0)

    def test_f9_item_uuid_preserves_exact_64_bit_digits_as_utf_string(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local prev=DataCenter.ItemData.GetItemById
        function DataCenter.ItemData:GetItemById(id)
          if id==5004 then return {uuid=1392040870727446886,itemId=5004,count=2} end
          return prev(self,id)
        end
        SFSNetwork.SendMessage=function(msg,args)
          assert(msg=='itemuse' and type(args.uuid)=='string')
          assert(args.uuid=='1392040870727446886' and args.num==1)
          world.sends=world.sends+1
        end
        """)
        result=call()
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(result['data']['itemUuid'],'1392040870727446886')
        self.assertEqual(state['sends'],1)

    def test_f9_opaque_native_stack_uuid_sends_and_matches_exact_original_reply(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local prev=DataCenter.ItemData.GetItemById
        function DataCenter.ItemData:GetItemById(id)
          if id==5004 then return {uuid='A97D-42ef_099',itemId=5004,count=world.randomCount} end
          return prev(self,id)
        end
        SFSNetwork.SendMessage=function(msg,args)
          assert(msg=='itemuse' and type(args.uuid)=='string')
          assert(args.uuid=='A97D-42ef_099' and args.num==1)
          world.sends=world.sends+1
        end
        """)
        first=call()
        self.assertEqual(first['state'],'accepted')
        self.assertEqual(first['data']['itemUuid'],'A97D-42ef_099')
        self.assertEqual(state['sends'],1)
        lua.execute("world.position=600123;DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid='A97D-42ef_099',opType=0})")
        final=call(query=True)
        self.assertEqual(final['state'],'confirmed')
        self.assertTrue(final['data']['replyExactItemUuidObserved'])
        self.assertFalse(final['data']['serverAckAttributable'])
        self.assertEqual(state['sends'],1)

    def test_f9_wrong_opaque_reply_uuid_cannot_confirm_changed_world_and_item(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local prev=DataCenter.ItemData.GetItemById
        function DataCenter.ItemData:GetItemById(id)
          if id==5004 then return {uuid='A97D-42ef_099',itemId=5004,count=world.randomCount} end
          return prev(self,id)
        end
        SFSNetwork.SendMessage=function()world.sends=world.sends+1 end
        """)
        self.assertEqual(call()['state'],'accepted')
        lua.execute("world.position=600123;DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid='A97D-42ef_098',opType=0})")
        query=call(query=True)
        self.assertEqual(query['state'],'accepted')
        self.assertFalse(query['data']['replyObserved'])
        self.assertEqual(state['sends'],1)

    def test_f9_observer_displaced_after_reply_blocks_confirmation_and_replay(self):
        lua,_,state,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.execute("""
        world.position=600123
        DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004,opType=0})
        DataCenter.ItemManager.ItemUseHandle=function()end
        """)
        final=call(query=True)
        self.assertEqual(final['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertIn('handler',final['error'])
        subsequent=call()
        self.assertEqual(subsequent['state'],'accepted')
        self.assertTrue(subsequent['data']['observerDetached'])
        self.assertFalse(subsequent['data']['relocationConfirmed'])
        self.assertEqual(state['sends'],1)

    def test_wrong_account_or_server_after_send_never_confirms_or_resends(self):
        lua,_,state,call=self.fixture();call()
        lua.execute("LuaEntry.Player.uid='different'")
        self.assertEqual(call(query=True)['state'],'failed');self.assertEqual(state['sends'],1)
        self.assertEqual(call()['state'],'failed');self.assertEqual(state['sends'],1)

    def test_unknown_send_exception_preserves_tombstone(self):
        lua,_,state,call=self.fixture()
        lua.execute("SFSNetwork.SendMessage=function()world.sends=world.sends+1;error('uncertain')end")
        first=call()
        self.assertEqual(first['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertFalse(first['data']['requestDispatched'])
        self.assertTrue(first['data']['nativeSendAttempted'])
        self.assertTrue(first['data']['nativeSendOutcomeUncertain'])
        query=call(query=True)
        self.assertEqual(query['state'],'accepted')
        self.assertFalse(query['data']['requestDispatched'])
        self.assertEqual(state['sends'],1)

    def test_sender_returned_false_preserves_f9_one_send_and_pending_observer(self):
        lua,_,state,call=self.fixture()
        lua.execute("SFSNetwork.SendMessage=function()world.sends=world.sends+1;return false end")
        first=call()
        self.assertEqual(first['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertEqual(state['sends'],1)
        again=call()
        self.assertEqual(again['state'],'accepted')
        self.assertTrue(again['data']['oneSendNoRetry'])
        self.assertFalse(again['data']['requestDispatched'])
        self.assertTrue(again['data']['nativeSendAttempted'])
        self.assertTrue(again['data']['nativeSendOutcomeUncertain'])
        self.assertFalse(again['data']['serverAckAttributable'])
        self.assertEqual(state['sends'],1)

    def test_f10_open_window_returning_false_cannot_reopen_or_dispatch_game_rpc(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        world.otherRally={allianceId=world.allianceUid,type=702,server=2213,
          pos=6610023,GetPointIndex=function(self)return math.floor(self.pos/10)end}
        UIManager.GetInstance=function()return {
          IsWindowOpen=function(self) return world.chooserOpen==true end,
          OpenWindow=function(self) world.dialogs=world.dialogs+1;return false end,
        }end
        """)
        first=call('allianceRelocate')
        self.assertEqual(first['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertEqual(state['dialogs'],1)
        again=call('allianceRelocate')
        self.assertEqual(again['state'],'accepted')
        self.assertFalse(again['data']['chooserWindowRegistered'])
        self.assertFalse(again['data']['requestDispatched'])
        self.assertEqual(state['dialogs'],1)
        self.assertEqual(state['sends'],0)

    def test_reply_for_unrelated_item_cannot_confirm_external_position_change(self):
        lua,_,_,call=self.fixture();call()
        lua.execute('world.randomCount=1;world.position=600123;DataCenter.ItemManager:ItemUseHandle({itemId=999,uuid=999})')
        self.assertEqual(call(query=True)['state'],'accepted')

    def test_dispose_restores_only_own_reply_wrapper(self):
        lua,module,_,call=self.fixture();call();module.Dispose()
        self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle==world.itemOriginal'))


if __name__ == "__main__":
    unittest.main(verbosity=2)
