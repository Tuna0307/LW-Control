"""FEATURE-RECOVERY-018 native equipment read-only preflight and bytecode checks.

Uses an archived admitted original v26 Lua package and inert Lua tables only;
does not access the installed game, account, journal, or network.
"""

from __future__ import annotations

import hashlib
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path[:0] = [
    str(ROOT.parent / "artifacts/map-013-lua-site"),
    str(ROOT),
    str(ROOT / "map_009_offline"),
]
from lupa.lua53 import LuaRuntime  # noqa: E402
import run_live_resource_probe as probe  # noqa: E402
import compact_lua53 as bytecode  # noqa: E402
from inspect_current import disassemble, root_methods  # noqa: E402
from feature018_mini_lua import ORIGINAL_ENTRY_SHA256  # noqa: E402

ARCHIVE = ROOT.parent / "artifacts/feature018/offline-candidate-continuation/LWScripts.data"
ADAPTER = ROOT / "feature018_equipment_actions.lua"

FAKE_GAME = """
state = {
  sent=0,focus=true,
  uid='2553945035002288',source=2288,selfServer=2288,current=2288,
  hero101={uuid=101,heroType=7,equips={}},
  hero202={uuid=202,heroType=7,equips={}},
  formation1={uuid=3001,localIndexToHeroDic={[1]=101}},
  formation2={uuid=3002,localIndexToHeroDic={[1]=202}},
  items={
    [501]={uuid=501,slot=1,heroUuid=101,config={heroType=7}},
    [502]={uuid=502,slot=1,heroUuid=0,config={heroType=7}},
    [503]={uuid=503,slot=1,heroUuid=202,config={heroType=7}},
    [504]={uuid=504,slot=2,heroUuid=0,config={heroType=7}},
  }
}
function state.hero101:GetEquipBySlotType(slot) return self.equips[slot] end
function state.hero202:GetEquipBySlotType(slot) return self.equips[slot] end
function state.hero101:IsUnlockEquipFunction() return true end
function state.hero202:IsUnlockEquipFunction() return true end
state.hero101.equips[1] = state.items[501]
state.hero202.equips[1] = state.items[503]
function state.formation1:GetLocalHeroAtSlotIndex(position)
  return self.localIndexToHeroDic[position]
end
function state.formation2:GetLocalHeroAtSlotIndex(position)
  return self.localIndexToHeroDic[position]
end
LuaEntry={Player={
  uid=state.uid,
  GetSourceServerId=function(self)return state.source end,
  GetSelfServerId=function(self)return state.selfServer end,
  GetCurServerId=function(self)return state.current end,
}}
HeroType={All=0}
DataCenter={
  ArmyFormationDataManager={GetOneArmyInfoByIndex=function(_,slot)
    if slot==1 then return state.formation1 end
    if slot==2 then return state.formation2 end
  end},
  HeroDataManager={GetHeroByUuid=function(_,id)
    if id==101 then return state.hero101 end
    if id==202 then return state.hero202 end
  end},
  EquipDataManager={GetEquipByUuid=function(_,id)
    return state.items[id]
  end},
}
SFSNetwork={SendMessage=function(...)
  state.sent=state.sent+1
  error('equipment read-only preflight must never send a game request')
end}
presets={
 {id="preset1",name="Current",squads={
   {squadIndex=1,positions={{position=1,equips={{slot=1,equipUuid="501"}}}}},
   {squadIndex=2,positions={{position=1,equips={{slot=1,equipUuid="503"}}}}},
 }},
 {id="preset2",name="Move",squads={
   {squadIndex=1,positions={{position=1,equips={{slot=1,equipUuid="502"}}}}},
   {squadIndex=2,positions={{position=1,equips={{slot=1,equipUuid="501"}}}}},
 }},
 {id="preset3",name="Empty",squads={
   {squadIndex=1,positions={{position=1,equips={}}}},
 }},
}
"""


def setup():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute(FAKE_GAME)
    adapter = lua.execute(ADAPTER.read_text(encoding="utf-8"))
    return lua, adapter, lua.globals().state, lua.globals().presets


def fields(lua, slot=1, **kwargs):
    values = {
        "action": "equipment",
        "slot": str(slot),
        "profileId": "profile",
        "launchSessionId": "session",
        "challenge": "challenge",
        "gamePid": "9245",
        "authorizedUid": "2553945035002288",
        "presetId": f"preset{slot}",
    }
    values.update(kwargs)
    return lua.table_from(values)


def status(lu, adapter, presets, slot=1, **kwargs):
    return adapter.Preflight(
        fields(lu, slot, **kwargs), presets,
        lu.eval("function() return true end"),
    )


class OriginalV26Evidence(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        f, c, records = probe.read_lwlf(ARCHIVE)
        cls.entries = dict(records)
        original = cls.entries.get(probe.ORIGINAL_LUA_ENTRY)
        assert (f, c) == (3, 26)
        assert hashlib.sha256(original).hexdigest() == ORIGINAL_ENTRY_SHA256

    def body(self, module, method):
        root = bytecode.parse_chunk(probe.decode_lenc(self.entries[module]))
        path = root_methods(root).get(method, method)
        for part in path.split("/")[1:]:
            root = root["children"][int(part)]
        return root

    def test_original_ui_hero_uid_equipment_uuid_sender(self):
        ui = self.body(
            "UI/UILWHero/UIHeroEquipListPanel/View/UIHeroEquipListPanelView.luac",
            "root/1",
        )
        lines = "\n".join(disassemble(ui))
        for key in ("HeroEquipInstall", "curHeroData", "slotType", "uuid", "SendMessage"):
            self.assertIn(key, lines)
        self.assertTrue(any("sourceLine=33" in line and "HeroEquipInstall" in line
                            for line in lines.splitlines()))
        self.assertTrue(any("sourceLine=33" in line and "slotType" in line
                            for line in lines.splitlines()))

    def test_original_hero_and_equipment_picture_sources(self):
        hero = "\n".join(disassemble(self.body(
            "UI/UIHero2/Common/UIHeroCellSmall.luac", "root/13")))
        for key in ("GetHeroIconPath", "modelId", "GetSkinId"):
            self.assertIn(key, hero)
        hero_resolver = "\n".join(disassemble(self.body(
            "UI/UIHero2/HeroUtils.luac", "GetHeroIconPath")))
        self.assertIn("small_icon", hero_resolver)
        gear = "\n".join(disassemble(self.body(
            "UI/UILWHero/UIHeroEquipListPanel/Component/BaseUIEquipItem.luac", "root/9")))
        for key in ("sourceLine=105", "LoadSpriteAsync", "ItemPath", "icon", "format"):
            self.assertIn(key, gear)

    def test_original_install_serialization_and_uncorrelated_reply(self):
        module = "Net/Msgs/HeroEquipInstallMessage.luac"
        sender = self.body(module, "OnCreate")
        dis = "\n".join(disassemble(sender))
        for key in ("heroUid", "slot2Uid", "PutLong", "PutInt", "PutSFSArray", "AddSFSObject"):
            self.assertIn(key, dis)
        reply = self.body(module, "HandleMessage")
        self.assertIn("errorCode", reply["constants"])
        self.assertIn("HeroEquipUninstall", reply["constants"])
        self.assertNotIn("slot2Uid", reply["constants"])
        self.assertNotIn("equipUuid", reply["constants"])
        uninstall_reply=self.body("Net/Msgs/HeroEquipUninstallMessage.luac",
                                  "HandleMessage")
        self.assertIn("errorCode",uninstall_reply["constants"])
        self.assertIn("heroUid",uninstall_reply["constants"])
        self.assertNotIn("slot2Uid",uninstall_reply["constants"])
        self.assertNotIn("requestId",uninstall_reply["constants"])
        push = self.body("Net/Msgs/PushHeroEquipChangeMessage.luac", "HandleMessage")
        self.assertIn("UpdateManyEquip", push["constants"])
        self.assertIn("list", push["constants"])
        self.assertNotIn("requestId", push["constants"])

    def test_native_owner_hero_slot_and_inventory_accessors(self):
        formation = self.body(
            "DataCenter/ArmyFormationData/ArmyFormationInfo.luac",
            "GetLocalHeroAtSlotIndex",
        )
        self.assertIn("localIndexToHeroDic", formation["constants"])
        lookup = self.body("DataCenter/HeroData/HeroDataManager.luac", "GetHeroByUuid")
        self.assertIn("allHero", lookup["constants"])
        gear = self.body("DataCenter/EquipData/EquipDataManager.luac", "GetEquipByUuid")
        self.assertIn("allEquip", gear["constants"])
        ui = self.body(
            "UI/UILWHero/UIHeroEquipListPanel/Controller/UIHeroEquipListPanelCtrl.luac",
            "GetAllEquipDataList",
        )
        self.assertIn("GetAllEquipListBySlotTypeAndHeroType", ui["constants"])


class EquipmentPreflight(unittest.TestCase):
    def test_transient_wearer_mismatch_refreshes_exact_native_slot_before_blocking(self):
        lua, module, state, confirmed = setup()
        lua.execute("""
          state.inventoryReads=0
          function DataCenter.EquipDataManager:GetEquipByUuid(id)
            if id==501 then
              state.inventoryReads=state.inventoryReads+1
              if state.inventoryReads==1 then
                return {uuid=501,slot=1,heroUuid=0,config={heroType=7}}
              end
            end
            return state.items[id]
          end
        """)
        result=status(lua,module,confirmed,1)
        self.assertEqual(result["state"],"confirmed")
        self.assertGreaterEqual(state["inventoryReads"],2)
        self.assertEqual(result["data"]["slots"][1]["currentEquipUuid"],"501")
        self.assertEqual(state["sent"],0)

    def test_fresh_slot_read_accepts_real_item_move_between_heroes(self):
        lua,module,state,confirmed=setup()
        lua.execute("""
          state.slotReads=0
          state.items[501].heroUuid=202
          state.items[502].heroUuid=101
          function state.hero101:GetEquipBySlotType(slot)
            if slot~=1 then return nil end
            state.slotReads=state.slotReads+1
            if state.slotReads==1 then return state.items[501] end
            return state.items[502]
          end
        """)
        result=status(lua,module,confirmed,1)
        self.assertEqual(result["state"],"confirmed")
        self.assertGreaterEqual(state["slotReads"],2)
        self.assertEqual(result["data"]["slots"][1]["currentEquipUuid"],"502")
        self.assertEqual(result["data"]["slots"][1]["targetEquipUuid"],"501")
        self.assertEqual(result["data"]["slots"][1]["state"],"wornByOtherHero")
        self.assertEqual(state["sent"],0)

    def test_verified_equipped_inventory_preflight_has_no_side_effect(self):
        lua, module, state, confirmed = setup()
        result = status(lua, module, confirmed, 1)
        self.assertEqual(result["state"], "confirmed")
        data = result["data"]
        self.assertEqual(data["observation"], "native_HeroEquip_slot_inventory_preflight")
        self.assertTrue(data["readOnly"])
        self.assertFalse(data["readyToApply"])
        self.assertFalse(data["requestDispatched"])
        self.assertEqual(data["plannedPositions"], 2)
        self.assertEqual(data["plannedItems"], 2)
        self.assertEqual(data["changedSlots"], 0)
        self.assertEqual(len(data["slots"]), 8)
        self.assertEqual(data["slots"][1]["heroUuid"], "101")
        self.assertEqual(data["slots"][1]["currentEquipUuid"], "501")
        self.assertEqual(data["slots"][1]["targetEquipUuid"], "501")
        self.assertEqual(data["slots"][1]["state"], "alreadyEquipped")
        self.assertEqual(state["sent"], 0)
        action = module.Handle(
            fields(lua,authorizedUid="12345678"), confirmed,
            lua.eval("function() return true end"))
        self.assertEqual(action["state"], "failed")
        self.assertEqual(state["sent"], 0)

    def test_free_item_and_cross_hero_swap_identified_without_sending(self):
        lua, module, state, confirmed = setup()
        result = status(lua, module, confirmed, 2)
        self.assertEqual(result["state"], "confirmed")
        info = result["data"]
        self.assertEqual(info["plannedItems"], 2)
        self.assertEqual(info["wornByOtherHero"], 1)
        self.assertTrue(info["changedSlots"] >= 2)
        self.assertEqual(info["slots"][1]["state"], "replaceRequired")
        self.assertEqual(info["slots"][5]["state"], "wornByOtherHero")
        self.assertEqual(state["sent"], 0)
        empty = status(lua, module, confirmed, 3)
        self.assertEqual(empty["state"], "confirmed")
        self.assertEqual(empty["data"]["uninstallSlots"], 1)
        self.assertEqual(empty["data"]["slots"][1]["state"], "uninstallRequired")

    def test_identity_focus_and_confirmed_profile_preset_guards(self):
        lua, module, state, presets = setup()
        for case in [
            dict(authorizedUid="999999"),
            dict(authorizedUid="012345678"),
            dict(profileId=""),
            dict(profileId="other/profile"),
            dict(challenge=""),
            dict(presetId="preset2"),
            dict(slot="5"),
            dict(slot="0"),
            dict(slot="01"),
            dict(action="attack"),
        ]:
            with self.subTest(case=case):
                receipt = module.Preflight(
                    fields(lua, **case), presets,
                    lua.eval("function() return true end"))
                self.assertNotEqual(receipt["state"], "confirmed")
                self.assertEqual(state["sent"], 0)
        self.assertEqual(module.Preflight(
            fields(lua), presets, lua.eval("function() return false end"))["state"],
                         "failed")
        self.assertEqual(module.Preflight(
            fields(lua), presets, lua.eval("function() error('focus unavailable') end"))["state"],
                         "failed")
        self.assertEqual(module.Preflight(
            fields(lua), None, lua.eval("function() return true end"))["state"],
                         "unsupported")
        presets[1]["id"] = "modified-unconfirmed"
        self.assertEqual(status(lua, module, presets)["state"], "failed")
        self.assertEqual(state["sent"], 0)

    def test_readonly_preflight_request_uid_must_match_and_native_servers_agree(self):
        for native_server in ("source","selfServer","current"):
            for rejected in (2212,2213,None):
                lua, module, state, presets=setup()
                state[native_server]=rejected
                result=status(lua,module,presets)
                self.assertEqual(result["state"],"failed",(native_server,rejected))
                self.assertEqual(state["sent"],0)
        lua,module,state,presets=setup()
        lua.globals().LuaEntry.Player.uid='3133546747002212'
        result=status(lua,module,presets)
        self.assertEqual(result["state"],"failed")
        self.assertEqual(state["sent"],0)
        # Every authenticated UID/server is valid when the request and all
        # three native server getters agree. No fixed proof-account allowlist.
        lua,module,state,presets=setup()
        lua.execute("""
          LuaEntry.Player.uid='3133546747002212'
          state.source=2212;state.selfServer=2212;state.current=2212
        """)
        fresh=status(lua,module,presets,authorizedUid='3133546747002212')
        self.assertEqual(fresh["state"],"confirmed")
        self.assertEqual(state["sent"],0)

    def test_inventory_disagreement_duplicates_and_wrong_slot_fail_closed(self):
        def check_failure(transform, expected="failed", slot=1):
            lua, module, state, confirmed = setup()
            transform(state, confirmed)
            result = status(lua, module, confirmed, slot)
            self.assertEqual(result["state"], expected,
                             result["error"] if result["state"] != "confirmed" else "unexpected success")
            self.assertEqual(state["sent"], 0)

        check_failure(lambda s, p: s["items"][501].__setitem__("slot", 2))
        check_failure(lambda s, p: s["items"][501].__setitem__("heroUuid", 0))
        check_failure(lambda s, p: s["items"][501]["config"].__setitem__("heroType", 9))
        check_failure(lambda s, p: s["hero101"].__setitem__("heroType", None),
                      "unsupported")
        check_failure(lambda s, p: s["items"].__setitem__(501, None))
        check_failure(lambda s, p: s["hero101"]["equips"].__setitem__(1, None))
        check_failure(lambda s, p: p[1]["squads"][2]["positions"][1]["equips"][1].__setitem__("equipUuid", "501"))
        check_failure(lambda s, p: p[1]["squads"][1]["positions"][1]["equips"][1].__setitem__("equipUuid", "504"))
        check_failure(lambda s, p: p[1]["squads"][1].__setitem__("squadIndex", 2))
        check_failure(lambda s, p: p[1]["squads"][1]["positions"][1].__setitem__("position", 6))
        check_failure(lambda s, p: s["formation1"].__setitem__("localIndexToHeroDic", {}), "unsupported")
        check_failure(lambda s,p: p[2]["squads"][1]["positions"][1].__setitem__("heroUuid","202"),slot=2)
        check_failure(lambda s,p: p[2]["squads"][1]["positions"][1].__setitem__("heroUuid","not-native"),slot=2)

    def test_persisted_native_snapshot_hero_identity_fails_closed_after_formation_rearrangement(self):
        lua,module,state,presets=setup()
        presets[2]["squads"][1]["positions"][1]["heroUuid"]="101"
        presets[2]["squads"][2]["positions"][1]["heroUuid"]="202"
        self.assertEqual(status(lua,module,presets,2)["state"],"confirmed")
        state["formation1"]["localIndexToHeroDic"][1]=202
        result=status(lua,module,presets,2)
        self.assertEqual(result["state"],"failed")
        self.assertIn("hero UUID no longer matches",result["error"])
        self.assertEqual(state["sent"],0)


class EquipmentApplyChecks(unittest.TestCase):
    def fixture(self):
        lua,module,state,presets=setup()
        lua.execute("""
        LuaEntry.Player.uid='2553945035002288'
        function LuaEntry.Player:GetSourceServerId()return 2288 end
        function LuaEntry.Player:GetSelfServerId()return 2288 end
        function LuaEntry.Player:GetCurServerId()return 2288 end
        MsgDefines={HeroEquipInstall='install',HeroEquipUninstall='remove'}
        state.requests={}
        SFSNetwork.SendMessage=function(message,hero,slots)
          state.sent=state.sent+1
          state.requests[state.sent]={message=message,hero=hero,slots=slots}
        end
        function apply_push()
          local req=state.requests[state.sent]
          local hero=req.hero==101 and state.hero101 or state.hero202
          if req.message=='remove' then
            for _,slot in ipairs(req.slots)do
              local old=hero.equips[slot]
              if old then old.heroUuid=0 end
              hero.equips[slot]=nil
            end
          else
            for slot,id in pairs(req.slots)do
              hero.equips[slot]=state.items[id]
              state.items[id].heroUuid=hero.uuid
            end
          end
        end
        """)
        def call(query=False,slot=2):
            extra={'authorizedUid':'2553945035002288'}
            if query: extra['queryOnly']='1'
            return module.Handle(fields(lua,slot,**extra),presets,
                                 lua.eval('function()return state.focus end'))
        return lua,module,state,presets,call

    def test_native_snapshot_supplies_legitimate_persistable_loadout_and_inventory(self):
        lua,module,state,_,_=self.fixture()
        lua.execute('function DataCenter.EquipDataManager:GetAllEquipList()return state.items end')
        lua.execute('state.hero101.firstName="hero_native_name"; state.items[501].configId=101; state.items[501].config.name="equip_native_name"; state.items[501].level=7')
        result=module.Snapshot(fields(lua,authorizedUid='2553945035002288'))
        self.assertEqual(result['state'],'confirmed');data=result['data']
        self.assertEqual(data['squads'][1]['positions'][1]['equips'][1]['equipUuid'],'501')
        self.assertEqual(data['squads'][2]['positions'][1]['heroUuid'],'202')
        self.assertEqual(data['squads'][1]['positions'][1]['heroNameKey'],'hero_native_name')
        stock=next(item for item in data['items'].values() if item['equipUuid']=='501')
        self.assertEqual(stock['itemId'],'101');self.assertEqual(stock['nameKey'],'equip_native_name')
        self.assertEqual(stock['level'],7)
        self.assertEqual(len(data['items']),4);self.assertEqual(state['sent'],0)
        self.assertTrue(data['readOnly']);self.assertFalse(data['requestDispatched'])

    def test_native_snapshot_refreshes_transient_wearer_mismatch_once(self):
        lua,module,state,_,_=self.fixture()
        lua.execute('''
        state.inventoryReads=0
        function DataCenter.EquipDataManager:GetEquipByUuid(id)
          if id==501 then
            state.inventoryReads=state.inventoryReads+1
            if state.inventoryReads==1 then
              return {uuid=501,slot=1,heroUuid=0,config={heroType=7}}
            end
          end
          return state.items[id]
        end
        function DataCenter.EquipDataManager:GetAllEquipList()return state.items end
        ''')
        result=module.Snapshot(fields(lua,authorizedUid='2553945035002288'))
        self.assertEqual(result['state'],'confirmed')
        self.assertGreaterEqual(state['inventoryReads'],2)
        self.assertEqual(result['data']['squads'][1]['positions'][1]['equips'][1]['equipUuid'],'501')
        self.assertEqual(state['sent'],0)

    def test_native_snapshot_pictures_use_native_skin_and_item_icon(self):
        lua,module,state,_,_=self.fixture()
        lua.execute('''
        function DataCenter.EquipDataManager:GetAllEquipList()return state.items end
        HeroIconType={small_icon=17}
        HeroUtils={GetHeroIconPath=function(model,kind,skin)
          assert(model==77 and kind==17 and skin==88)
          return "Assets/Hero/native-skin.png"
        end}
        state.hero101.modelId=77
        function state.hero101:GetSkinId()return 88 end
        LoadPath={ItemPath="Assets/Item/%s.png"}
        state.items[501].config.icon="native-cannon"
        ''')
        data=module.Snapshot(fields(lua,authorizedUid='2553945035002288'))['data']
        self.assertEqual(data['squads'][1]['positions'][1]['iconPath'], 'Assets/Hero/native-skin.png')
        stock=next(item for item in data['items'].values() if item['equipUuid']=='501')
        self.assertEqual(stock['iconPath'], 'Assets/Item/native-cannon.png')
        self.assertIsNone(data['squads'][2]['positions'][1]['iconPath'])
        self.assertEqual(state['sent'],0)

    def test_sequential_real_install_uninstall_waits_for_native_push(self):
        lua,_,state,_,call=self.fixture()
        first=call();self.assertEqual(first['state'],'accepted');self.assertEqual(state['sent'],1)
        self.assertEqual(state['requests'][1]['message'],'remove')
        waiting=call(True);self.assertEqual(waiting['state'],'accepted');self.assertEqual(state['sent'],1)
        for _ in range(8):
            lua.globals().apply_push()
            result=call(True)
            if result['state']=='confirmed':break
        self.assertEqual(result['state'],'confirmed');self.assertTrue(result['data']['equipmentApplied'])
        self.assertTrue(result['data']['nativeSlotPoststateVerified'])
        self.assertTrue(result['data']['requestDispatched'])
        self.assertFalse(result['data']['clientStepNonceAvailable'])
        self.assertFalse(result['data']['perSlotServerAckAttributable'])
        self.assertFalse(result['data']['perSlotReplyCorrelated'])
        self.assertEqual(result['data']['completedSteps'],4)
        self.assertEqual(state['hero101']['equips'][1]['uuid'],502)
        self.assertEqual(state['hero202']['equips'][1]['uuid'],501)

    def test_split_native_push_across_frames_waits_without_replay_or_next_send(self):
        lua,_,state,_,call=self.fixture()
        self.assertEqual(call()['state'],'accepted');self.assertEqual(state['sent'],1)
        # Inventory got the uninstall push, but the hero slot still exposes
        # its old object for multiple frames, including both immediate reads.
        lua.execute('state.items[501].heroUuid=0')
        for _ in range(3):
            pending=call(True)
            self.assertEqual(pending['state'],'accepted')
            self.assertTrue(pending['data']['awaitingNativeEquipmentSync'])
            self.assertTrue(pending['data']['nextStepNotDispatched'])
            self.assertEqual(state['sent'],1)
        for _ in range(8):
            lua.globals().apply_push()
            result=call(True)
            if result['state']=='confirmed':break
        self.assertTrue(result['data']['equipmentApplied'])
        self.assertEqual(state['sent'],4, 'Only original four install/uninstall steps may send')
        self.assertEqual(state['hero101']['equips'][1]['uuid'],502)
        self.assertEqual(state['hero202']['equips'][1]['uuid'],501)

    def test_saved_preset_2_apply_then_saved_preset_1_restore_exact_gear_and_owner(self):
        lua,module,state,presets,call=self.fixture()
        self.assertEqual(state['hero101']['equips'][1]['uuid'],501)
        self.assertEqual(state['hero202']['equips'][1]['uuid'],503)
        total=0
        for selected in (2,1):
            initial=call(slot=selected)
            self.assertEqual(initial['state'],'accepted')
            for _ in range(10):
                lua.globals().apply_push()
                next_state=call(query=True,slot=selected)
                if next_state['state']=='confirmed':
                    break
            self.assertEqual(next_state['state'],'confirmed')
            self.assertTrue(next_state['data']['nativeSlotPoststateVerified'])
            self.assertTrue(next_state['data']['requestDispatched'])
            self.assertFalse(next_state['data']['perSlotReplyCorrelated'])
            self.assertEqual(next_state['data']['completedSteps'],4)
            self.assertEqual(next_state['data']['dispatchedSteps'],4)
            total+=4
            self.assertEqual(state['sent'],total)
            if selected==2:
                self.assertEqual(state['hero101']['equips'][1]['uuid'],502)
                self.assertEqual(state['hero202']['equips'][1]['uuid'],501)
            else:
                self.assertEqual(state['hero101']['equips'][1]['uuid'],501)
                self.assertEqual(state['hero202']['equips'][1]['uuid'],503)
                self.assertEqual(state['items'][502]['heroUuid'],0)
                self.assertEqual(state['items'][501]['heroUuid'],101)
                self.assertEqual(state['items'][503]['heroUuid'],202)
        self.assertEqual(call(query=True,slot=1)['state'],'failed')

    def test_unrelated_equipment_change_while_waiting_cannot_advance_or_send(self):
        lua,module,state,presets,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.execute("""
          state.hero101.equips[1]=state.items[503]
          state.items[501].heroUuid=0
          state.items[503].heroUuid=101
          state.hero202.equips[1]=nil
        """)
        response=call(query=True)
        self.assertEqual(response['state'],'failed')
        self.assertIn('unexpected equipment UUID',response['error'])
        self.assertEqual(state['sent'],1)

    def test_same_uid_and_native_server_required_before_every_step(self):
        lua,_,state,_,call=self.fixture();call();lua.globals().apply_push()
        lua.execute('function LuaEntry.Player:GetCurServerId()return 2212 end')
        self.assertEqual(call(True)['state'],'failed');self.assertEqual(state['sent'],1)

    def test_full_server_hop_with_same_uid_blocks_pending_gear_transaction(self):
        lua,_,state,_,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.globals().apply_push()
        lua.execute("""
          function LuaEntry.Player:GetSourceServerId()return 2212 end
          function LuaEntry.Player:GetSelfServerId()return 2212 end
          function LuaEntry.Player:GetCurServerId()return 2212 end
        """)
        observed=call(True)
        self.assertEqual(observed['state'],'failed')
        self.assertIn('another native server',observed['error'])
        self.assertEqual(state['sent'],1)
        # A fresh request on the new server must not start another series while
        # the original transaction is unresolved, even with matching UID.
        self.assertEqual(call()['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_native_sender_server_hop_marks_uncertain_without_second_rpc(self):
        lua,_,state,_,call=self.fixture()
        lua.execute("""
          function LuaEntry.Player:GetSourceServerId()return state.source end
          function LuaEntry.Player:GetSelfServerId()return state.selfServer end
          function LuaEntry.Player:GetCurServerId()return state.current end
          SFSNetwork.SendMessage=function(...)
            state.sent=state.sent+1
            state.source=2212;state.selfServer=2212;state.current=2212
          end
        """)
        first=call()
        self.assertEqual(first['state'],'failed')
        self.assertEqual(first['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertIn('changed during send',first['error'])
        self.assertTrue(first['data']['uncertainSendOutcome'])
        self.assertEqual(state['sent'],1)
        state['source']=2288;state['selfServer']=2288;state['current']=2288
        self.assertEqual(call(query=True)['state'],'failed')
        self.assertEqual(call()['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_preflight_native_focus_switch_to_another_coherent_server_is_rejected(self):
        lua,module,state,presets=setup()
        callback=lua.eval("""
          function()
            state.source=2212;state.selfServer=2212;state.current=2212
            return true
          end
        """)
        outcome=module.Preflight(fields(lua,2),presets,callback)
        self.assertEqual(outcome['state'],'failed')
        self.assertIn('changed during preflight',outcome['error'])
        self.assertEqual(state['sent'],0)

    def test_different_authenticated_account_can_apply_preset(self):
        lua,module,state,presets,call=self.fixture()
        lua.execute("""
          LuaEntry.Player.uid='3133546747002212'
          function LuaEntry.Player:GetSourceServerId()return 2212 end
          function LuaEntry.Player:GetSelfServerId()return 2212 end
          function LuaEntry.Player:GetCurServerId()return 2212 end
        """)
        approved=module.Handle(fields(lua,2,authorizedUid='3133546747002212'),
            presets,lua.eval('function()return true end'))
        self.assertEqual(approved['state'],'accepted')
        self.assertEqual(state['sent'],1)
        # The queued old UID cannot observe or advance the new owner's steps.
        self.assertEqual(call(query=True)['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_throw_after_send_never_retransmits(self):
        lua,_,state,_,call=self.fixture()
        lua.execute("SFSNetwork.SendMessage=function(...)state.sent=state.sent+1;error('uncertain')end")
        self.assertEqual(call()['errorCode'],'GAME_ACTION_UNCONFIRMED')
        observed=call(True)
        self.assertEqual(observed['state'],'failed')
        self.assertEqual(observed['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertTrue(observed['data']['uncertainSendOutcome'])
        self.assertFalse(observed['data']['uncertainStepPoststateVerified'])
        self.assertTrue(observed['data']['nextStepNotDispatched'])
        self.assertEqual(state['sent'],1)
        # The native gear change may have succeeded after an exception.
        # The original reply cannot uniquely ACK this saved slot operation;
        # observing it must NEVER automatically advance to gear step two.
        lua.execute("state.hero101.equips[1]=nil;state.items[501].heroUuid=0")
        later=call(True)
        self.assertEqual(later['state'],'failed')
        self.assertTrue(later['data']['uncertainStepPoststateVerified'])
        self.assertTrue(later['data']['awaitingOwnerReview'])
        self.assertEqual(state['sent'],1)
        self.assertEqual(call()['state'],'failed')
        self.assertEqual(state['sent'],1)

    def test_original_equipment_sender_returning_false_is_uncertain_not_retryable(self):
        lua,_,state,_,call=self.fixture()
        lua.execute("SFSNetwork.SendMessage=function(...)state.sent=state.sent+1;return false end")
        first=call()
        self.assertEqual(first['state'],'failed')
        self.assertEqual(first['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertTrue(first['data']['uncertainSendOutcome'])
        lua.execute("state.hero101.equips[1]=nil;state.items[501].heroUuid=0")
        later=call(True)
        self.assertEqual(later['state'],'failed')
        self.assertTrue(later['data']['uncertainStepPoststateVerified'])
        self.assertEqual(state['sent'],1)

    def test_preexisting_matching_preset_is_observed_without_send(self):
        _,_,state,_,call=self.fixture()
        result=call(slot=1);self.assertEqual(result['state'],'confirmed')
        self.assertFalse(result['data']['requestDispatched'])
        self.assertEqual(result['data']['dispatchedSteps'],0);self.assertEqual(state['sent'],0)

    def test_changed_preset_or_hero_rejects_pending_transaction(self):
        lua,_,state,presets,call=self.fixture();call()
        presets[2]['squads'][1]['positions'][1]['equips'][1]['equipUuid']='504'
        self.assertEqual(call(True)['state'],'failed');self.assertEqual(state['sent'],1)

    def test_query_only_observes_gear_push_but_does_not_dispatch_next_step_in_text_focus(self):
        lua,_,state,_,call=self.fixture()
        first=call()
        self.assertEqual(first['state'],'accepted')
        self.assertEqual(state['sent'],1)
        lua.globals().apply_push()
        state['focus']=False
        paused=call(True)
        self.assertEqual(paused['state'],'accepted')
        self.assertEqual(paused['data']['completedSteps'],1)
        self.assertTrue(paused['data']['awaitingInputClear'])
        self.assertTrue(paused['data']['nextStepNotDispatched'])
        self.assertEqual(state['sent'],1)
        again=call(True)
        self.assertEqual(again['state'],'accepted')
        self.assertEqual(state['sent'],1)
        state['focus']=True
        resumed=call(True)
        self.assertEqual(resumed['state'],'accepted')
        self.assertEqual(resumed['data']['dispatchedSteps'],2)
        self.assertEqual(state['sent'],2)
        for _ in range(6):
            lua.globals().apply_push()
            result=call(True)
            if result['state']=='confirmed':
                break
        self.assertEqual(result['state'],'confirmed')
        self.assertEqual(state['sent'],4)

    def test_query_only_final_state_completes_without_focus_and_no_fresh_send(self):
        lua,_,state,_,call=self.fixture()
        self.assertEqual(call(slot=2)['state'],'accepted')
        for index in range(4):
            lua.globals().apply_push()
            if index==3:
                state['focus']=False
            response=call(query=True)
        self.assertEqual(response['state'],'confirmed')
        self.assertEqual(state['sent'],4)
        self.assertFalse(response['data']['awaitingGameConfirmation'])

    def test_query_only_owner_transition_during_focus_check_blocks_next_step(self):
        lua,module,state,presets,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.globals().apply_push()
        lua.execute('function LuaEntry.Player:GetCurServerId()return state.current end')
        result=module.Handle(fields(lua,2,authorizedUid='2553945035002288',queryOnly='1'),
            presets,lua.eval("""
              function()
                state.current=2212
                return true
              end
            """))
        self.assertEqual(result['state'],'failed')
        self.assertIn('owner/server changed',result['error'])
        self.assertEqual(state['sent'],1)

    def test_uninstall_requires_old_native_gear_to_be_unworn_before_next_step(self):
        lua,_,state,_,call=self.fixture()
        self.assertEqual(call(slot=3)['state'],'accepted')
        self.assertEqual(state['sent'],1)
        lua.globals().apply_push()
        state['items'][501]['heroUuid']=202
        result=call(query=True,slot=3)
        self.assertEqual(result['state'],'failed')
        self.assertIn('reassigned to another hero',result['error'])
        self.assertEqual(state['sent'],1)

    def test_focus_callback_changed_next_native_slot_blocks_automatic_send(self):
        lua,module,state,presets,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.globals().apply_push()
        result=module.Handle(fields(lua,2,authorizedUid='2553945035002288',queryOnly='1'),
            presets,lua.eval('''
              function()
                state.hero202.equips[1]=nil
                return true
              end
            '''))
        self.assertEqual(result['state'],'failed')
        self.assertIn('slot changed immediately before dispatch',result['error'])
        self.assertEqual(state['sent'],1)

    def test_focus_callback_changed_next_gear_uuid_blocks_original_install(self):
        lua,module,state,presets,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        for _ in range(2):
            lua.globals().apply_push()
            self.assertEqual(call(query=True)['state'],'accepted')
        self.assertEqual(state['sent'],3)
        lua.globals().apply_push()
        result=module.Handle(fields(lua,2,authorizedUid='2553945035002288',queryOnly='1'),
            presets,lua.eval('''
              function()
                state.items[501].uuid=999
                return true
              end
            '''))
        self.assertEqual(result['state'],'failed')
        self.assertIn('target changed immediately before dispatch',result['error'])
        self.assertEqual(state['sent'],3)

    def test_native_focus_check_mutating_saved_owner_preset_blocks_next_equipment_rpc(self):
        lua,module,state,presets,call=self.fixture()
        self.assertEqual(call()['state'],'accepted')
        lua.globals().apply_push()
        result=module.Handle(fields(lua,2,authorizedUid='2553945035002288',queryOnly='1'),
            presets,lua.eval('''
              function()
                presets[2].squads[1].positions[1].equips[1].equipUuid="503"
                return true
              end
            '''))
        self.assertEqual(result['state'],'failed')
        self.assertIn('preset targets changed before dispatch',result['error'])
        self.assertEqual(state['sent'],1)

    def test_native_focus_check_editing_saved_hero_binding_blocks_preset_rpc(self):
        lua,module,state,presets,call=self.fixture()
        presets[2]['squads'][1]['positions'][1]['heroUuid']='101'
        presets[2]['squads'][2]['positions'][1]['heroUuid']='202'
        self.assertEqual(call()['state'],'accepted')
        lua.globals().apply_push()
        result=module.Handle(fields(lua,2,authorizedUid='2553945035002288',queryOnly='1'),
            presets,lua.eval('''
              function()
                presets[2].squads[2].positions[1].heroUuid="101"
                return true
              end
            '''))
        self.assertEqual(result['state'],'failed')
        self.assertIn('saved hero changed before dispatch',result['error'])
        self.assertEqual(state['sent'],1)


class LiveEquipmentChecks(unittest.TestCase):
    def live_fixture(self):
        lua,module,state,_,_=EquipmentApplyChecks.fixture(self)
        lua.execute('function DataCenter.EquipDataManager:GetAllEquipList()return state.items end')
        def edit(hero='202', slot='1', gear='501', expected='503', wearer='101', identity='a'*32, query=False):
            values={'operationId':identity,'heroUuid':hero,'equipSlot':slot,'equipUuid':gear,
                    'expectedEquipUuid':expected,'expectedWearerUuid':wearer,'authorizedUid':'2553945035002288'}
            if query: values['queryOnly']='1'
            return module.Edit(fields(lua,**values),lua.eval('function()return state.focus end'))
        def finish(result, **arguments):
            sent=0
            for _ in range(12):
                if result['state']=='confirmed' and result['data']['equipmentApplied']: return result
                self.assertEqual(result['state'],'accepted')
                if state['sent']>sent:
                    lua.globals().apply_push();sent=state['sent']
                result=edit(query=True,**arguments)
            self.fail('Live edit failed to settle')
        return lua,module,state,edit,finish

    def test_live_move_replaces_target_without_swapping_and_duplicate_never_sends(self):
        lua,module,state,edit,finish=self.live_fixture()
        finish(edit())
        self.assertIsNone(state['hero101']['equips'][1])
        self.assertEqual(state['hero202']['equips'][1]['uuid'],501)
        self.assertEqual(state['items'][503]['heroUuid'],0)
        sent=state['sent'];self.assertEqual(edit()['state'],'confirmed')
        self.assertEqual(state['sent'],sent)

    def test_live_unequip_then_equip_empty_slot(self):
        lua,module,state,edit,finish=self.live_fixture()
        finish(edit(hero='101',gear='',expected='501',wearer=''),hero='101',gear='',expected='501',wearer='')
        self.assertEqual(state['items'][501]['heroUuid'],0)
        args=dict(hero='101',gear='502',expected='',wearer='',identity='b'*32)
        finish(edit(**args),**args)
        self.assertEqual(state['hero101']['equips'][1]['uuid'],502)

    def test_live_stale_slot_wearer_wrong_type_and_foreign_owner_send_nothing(self):
        lua,module,state,edit,finish=self.live_fixture()
        for args in [dict(expected=''),dict(wearer=''),dict(gear='504',wearer='')]:
            self.assertEqual(edit(**args)['state'],'failed')
        self.assertEqual(state['sent'],0)
        lua.execute("LuaEntry.Player.uid='999'")
        self.assertEqual(edit()['state'],'failed');self.assertEqual(state['sent'],0)

    def test_live_pending_refuses_another_edit_and_never_replays_waiting_send(self):
        lua,module,state,edit,finish=self.live_fixture()
        self.assertEqual(edit()['state'],'accepted');sent=state['sent']
        self.assertEqual(edit(identity='b'*32)['state'],'failed')
        self.assertEqual(edit(query=True)['state'],'accepted')
        self.assertEqual(state['sent'],sent)


if __name__ == "__main__":
    unittest.main(verbosity=2)
