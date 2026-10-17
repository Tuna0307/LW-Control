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
    uid='3133546747002289', source=2289, selfServer=2289, current=2289,
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
world.homeRally=fake_mark(MarkType.Alliance_rally,2289,514999)
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
                  "launchSessionId": "launch", "challenge": "challenge", "gamePid": "8123", "preflightOnly":"1"}
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
        self.assertEqual(data["sourceServerId"], 2289)
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
        self.assertEqual(rally["homeRally"]["serverId"], 2289)
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
        state["homeRally"]["server"] = 2289
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
            "challenge":"challenge", "gamePid":"8123"})
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
        self.assertIn("server-2289", reject["error"])
        self.assertEqual(actions.Snapshot()["state"], "unsupported")
        state["current"] = 2289
        state["source"] = 2212
        self.assertEqual(call("allianceRelocate")["state"], "unsupported")
        state["source"] = 2289
        runtime.globals().LuaEntry.Player.uid = "3133546747002212"
        self.assertIn("exact authorized 2289", call("allianceRelocate")["error"])
        runtime.globals().LuaEntry.Player.uid = "3133546747002289"
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

    def test_original_f10_alliance_message_not_world_mv_and_success_reason_240(self):
        # The selected-rally call is a costType=4 AllianceMoveCity RPC on this
        # owner home world. Its result=1 forwards to WorldMvHandle(response,240).
        # A different WorldMvHandle invocation cannot confirm this request.
        move=self.check_method('Util/MoveCityUtil.luac','AllianceMoveCityToSourceRallyPoint',
            'd53c624e4dcc87b6228f8ac650704868e321332614d36321b88800b5f8562de4',
            ('AllianceMoveCity','SendMessage','GetSourceServerId','GetSelfServerId',
             'GetIsInCity','ChangeToWorld'))
        self.assertTrue(any("sourceLine=136" in row and "AllianceMoveCity" in row
                            for row in disassemble(move)))
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
        """)
        def call(action='randomRelocate',query=False):
            fields={'action':action,'slot':'0','profileId':'owner','launchSessionId':'launch',
                    'challenge':'challenge','gamePid':'8123'}
            if query: fields['queryOnly']='1'
            func=actions.RandomRelocate if action=='randomRelocate' else actions.AllianceRelocate
            return func(lua.table_from(fields),lua.eval('function()return world.focusClear end'))
        return lua,actions,state,call

    def test_f9_owned_random_item_single_send_reply_and_fresh_position(self):
        lua,_,state,call=self.fixture()
        result=call();self.assertEqual(result['state'],'accepted');self.assertEqual(state['sends'],1)
        self.assertEqual(call(query=True)['state'],'accepted');self.assertEqual(state['sends'],1)
        lua.execute('world.position=600123')
        self.assertEqual(call(query=True)['state'],'accepted')
        lua.execute('DataCenter.ItemManager:ItemUseHandle({itemId=5004,uuid=100004})')
        final=call(query=True);self.assertEqual(final['state'],'confirmed')
        self.assertTrue(final['data']['relocationConfirmed']);self.assertTrue(final['data']['replyObserved'])
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
        lua.execute('SpecialItemId.ITEM_ALLIANCE_CITY_MOVE=nil')
        result=call('allianceRelocate')
        self.assertEqual(result['state'],'accepted')
        self.assertEqual(state['sends'],1)

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

    def test_f10_rally_move_between_first_check_and_send_never_dispatches(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local original=DataCenter.AllianceRallyPointDataManager.GetMyAllianceRallyPoint
        local homeReads=0
        function DataCenter.AllianceRallyPointDataManager:GetMyAllianceRallyPoint(kind)
          if kind==701 then
            homeReads=homeReads+1
            if homeReads==2 then world.homeRally.pos=5310023 end
          end
          return original(self,kind)
        end
        """)
        result=call('allianceRelocate')
        self.assertEqual(result['state'],'failed')
        self.assertIn('rally target changed',result['error'])
        self.assertEqual(state['sends'],0)

    def test_f10_native_scene_changes_after_initial_world_check_no_send(self):
        lua,_,state,call=self.fixture()
        lua.execute("""
        local sceneReads=0
        SceneUtils.GetIsInCity=function()
          sceneReads=sceneReads+1
          return sceneReads>1
        end
        """)
        result=call('allianceRelocate')
        self.assertEqual(result['state'],'failed')
        self.assertIn('world scene changed',result['error'])
        self.assertEqual(state['sends'],0)

    def test_f10_exact_home_alliance_native_cost4_and_reply_poststate(self):
        lua,_,state,call=self.fixture()
        self.assertEqual(call('allianceRelocate')['state'],'accepted')
        self.assertEqual(state['sends'],1)
        lua.execute('DataCenter.BuildManager:WorldMvHandle({serverId=2289,result=1,worldMainPoint=514998},240)')
        result=call('allianceRelocate',True)
        self.assertEqual(result['state'],'confirmed');self.assertTrue(result['data']['worldMvAcknowledged'])
        self.assertEqual(result['data']['afterWorldPointId'],514998)
        self.assertEqual(result['data']['acknowledgedWorldPointId'],514998)
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
        self.assertEqual(response['state'],'unsupported')
        self.assertIn('native rally chooser',response['error'])
        self.assertEqual(state['sends'],0)

    def test_final_native_owner_revalidation_after_focus_blocks_both_sends(self):
        for action in ('randomRelocate','allianceRelocate'):
            lua,module,state,_=self.fixture()
            fn=module.RandomRelocate if action=='randomRelocate' else module.AllianceRelocate
            values={'action':action,'slot':'0','profileId':'owner',
                    'launchSessionId':'launch','challenge':'challenge','gamePid':'8123'}
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

    def test_readonly_readiness_exposes_exact_f9_and_f10_bag_items(self):
        lua,module,state,_=self.fixture()
        def probe(action):
            vals={'action':action,'slot':'0','profileId':'owner','launchSessionId':'launch',
                  'challenge':'challenge','gamePid':'8123','preflightOnly':'1'}
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
        self.assertEqual(state['sends'],0)

    def test_readonly_readiness_reports_missing_owned_item_without_send(self):
        lua,module,state,_=self.fixture()
        lua.execute('world.randomCount=0')
        vals={'action':'randomRelocate','slot':'0','profileId':'owner',
              'launchSessionId':'launch','challenge':'challenge','gamePid':'8123',
              'preflightOnly':'1'}
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
                  'preflightOnly':'0'}
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

    def test_f10_reason_240_requires_matching_server_reply_point_and_success(self):
        for reply in ('{serverId=2289,result=2,worldMainPoint=514998}',
                      '{serverId=2289,result=1,worldMainPoint=514998,buildInfo={pId=514999}}',
                      '{serverId=2289,result=1,worldMainPoint=514998}'):
            lua,_,state,call=self.fixture()
            # Independent position after does not match one/both reply fields
            # in the final case; still no ACK despite the owned item debit.
            if reply.endswith('worldMainPoint=514998}'):
                lua.execute('DataCenter.BuildManager.WorldMvHandle=function(self,r) world.allyCount=world.allyCount-1 end')
            self.assertEqual(call('allianceRelocate')['state'],'accepted')
            lua.execute('DataCenter.BuildManager:WorldMvHandle('+reply+',240)')
            observed=call('allianceRelocate',True)
            self.assertEqual(observed['state'],'accepted',reply)
            self.assertFalse(observed['data']['replyObserved'],reply)
            self.assertFalse(observed['data']['worldMvAcknowledged'],reply)
            self.assertEqual(state['sends'],1,reply)

    def test_missing_warning_or_item_never_dispatches(self):
        lua,_,state,call=self.fixture()
        lua.execute('DataCenter.ActMeteoriteBattleManager.NeedNoticeRandomMoveCity=function()return true end')
        self.assertEqual(call()['state'],'failed');self.assertEqual(state['sends'],0)
        lua.execute('DataCenter.ActMeteoriteBattleManager.NeedNoticeRandomMoveCity=function()return false end;world.randomCount=0')
        self.assertEqual(call()['state'],'unsupported');self.assertEqual(state['sends'],0)

    def test_f10_wrong_reply_reason_cannot_confirm_even_with_real_position_and_debit(self):
        lua,_,state,call=self.fixture()
        self.assertEqual(call('allianceRelocate')['state'],'accepted')
        lua.execute('DataCenter.BuildManager:WorldMvHandle({serverId=2289,worldMainPoint=514998})')
        result=call('allianceRelocate',True)
        self.assertEqual(result['state'],'accepted')
        self.assertFalse(result['data']['worldMvAcknowledged'])
        self.assertFalse(result['data']['replyObserved'])
        self.assertEqual(state['sends'],1)

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

    def test_wrong_account_or_server_after_send_never_confirms_or_resends(self):
        lua,_,state,call=self.fixture();call()
        lua.execute("LuaEntry.Player.uid='different'")
        self.assertEqual(call(query=True)['state'],'failed');self.assertEqual(state['sends'],1)
        self.assertEqual(call()['state'],'failed');self.assertEqual(state['sends'],1)

    def test_unknown_send_exception_preserves_tombstone(self):
        lua,_,state,call=self.fixture()
        lua.execute("SFSNetwork.SendMessage=function()world.sends=world.sends+1;error('uncertain')end")
        self.assertEqual(call()['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertEqual(call(query=True)['state'],'accepted');self.assertEqual(state['sends'],1)

    def test_reply_for_unrelated_item_cannot_confirm_external_position_change(self):
        lua,_,_,call=self.fixture();call()
        lua.execute('world.randomCount=1;world.position=600123;DataCenter.ItemManager:ItemUseHandle({itemId=999,uuid=999})')
        self.assertEqual(call(query=True)['state'],'accepted')

    def test_dispose_restores_only_own_reply_wrapper(self):
        lua,module,_,call=self.fixture();call();module.Dispose()
        self.assertTrue(lua.eval('DataCenter.ItemManager.ItemUseHandle==world.itemOriginal'))


if __name__ == "__main__":
    unittest.main(verbosity=2)
