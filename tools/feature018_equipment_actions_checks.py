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
  uid='3133546747002289',source=2289,selfServer=2289,current=2289,
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
        "authorizedUid": "3133546747002289",
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

    def test_readonly_preflight_owner_requires_exact_uid_and_all_native_servers(self):
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


class EquipmentApplyChecks(unittest.TestCase):
    def fixture(self):
        lua,module,state,presets=setup()
        lua.execute("""
        LuaEntry.Player.uid='3133546747002289'
        function LuaEntry.Player:GetSourceServerId()return 2289 end
        function LuaEntry.Player:GetSelfServerId()return 2289 end
        function LuaEntry.Player:GetCurServerId()return 2289 end
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
            extra={'authorizedUid':'3133546747002289'}
            if query: extra['queryOnly']='1'
            return module.Handle(fields(lua,slot,**extra),presets,
                                 lua.eval('function()return state.focus end'))
        return lua,module,state,presets,call

    def test_native_snapshot_supplies_legitimate_persistable_loadout_and_inventory(self):
        lua,module,state,_,_=self.fixture()
        lua.execute('function DataCenter.EquipDataManager:GetAllEquipList()return state.items end')
        result=module.Snapshot(fields(lua,authorizedUid='3133546747002289'))
        self.assertEqual(result['state'],'confirmed');data=result['data']
        self.assertEqual(data['squads'][1]['positions'][1]['equips'][1]['equipUuid'],'501')
        self.assertEqual(data['squads'][2]['positions'][1]['heroUuid'],'202')
        self.assertEqual(len(data['items']),4);self.assertEqual(state['sent'],0)
        self.assertTrue(data['readOnly']);self.assertFalse(data['requestDispatched'])

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
        self.assertFalse(result['data']['perSlotReplyCorrelated'])
        self.assertEqual(result['data']['completedSteps'],4)
        self.assertEqual(state['hero101']['equips'][1]['uuid'],502)
        self.assertEqual(state['hero202']['equips'][1]['uuid'],501)

    def test_exact_2289_uid_and_all_three_native_servers_before_every_step(self):
        lua,_,state,_,call=self.fixture();call();lua.globals().apply_push()
        lua.execute('function LuaEntry.Player:GetCurServerId()return 2212 end')
        self.assertEqual(call(True)['state'],'failed');self.assertEqual(state['sent'],1)

    def test_throw_after_send_never_retransmits(self):
        lua,_,state,_,call=self.fixture()
        lua.execute("SFSNetwork.SendMessage=function(...)state.sent=state.sent+1;error('uncertain')end")
        self.assertEqual(call()['errorCode'],'GAME_ACTION_UNCONFIRMED')
        self.assertEqual(call(True)['state'],'accepted');self.assertEqual(state['sent'],1)

    def test_preexisting_matching_preset_is_observed_without_send(self):
        _,_,state,_,call=self.fixture()
        result=call(slot=1);self.assertEqual(result['state'],'confirmed')
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
        result=module.Handle(fields(lua,2,authorizedUid='3133546747002289',queryOnly='1'),
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
        result=module.Handle(fields(lua,2,authorizedUid='3133546747002289',queryOnly='1'),
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
        result=module.Handle(fields(lua,2,authorizedUid='3133546747002289',queryOnly='1'),
            presets,lua.eval('''
              function()
                state.items[501].uuid=999
                return true
              end
            '''))
        self.assertEqual(result['state'],'failed')
        self.assertIn('target changed immediately before dispatch',result['error'])
        self.assertEqual(state['sent'],3)


if __name__ == "__main__":
    unittest.main(verbosity=2)
