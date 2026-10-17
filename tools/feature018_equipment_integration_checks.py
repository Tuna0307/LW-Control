"""Inert game-bridge registration of trusted persisted equipment preflight.

No installed game/client/account is touched; spoofed UI fields cannot create
equipment item installs. Only the exact 2288 sub-account may be inspected.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

SOURCE = Path(__file__).with_name("feature018_hotkey_actions.lua")
GAME = r"""
state={uid="2553945035002288",source=2288,selfServer=2288,current=2288,
focus=true,inspections=0,sends=0}
LuaEntry={Player={uid=state.uid,
GetSourceServerId=function(self)return state.source end,
GetSelfServerId=function(self)return state.selfServer end,
GetCurServerId=function(self)return state.current end}}
frozen={TextInputClear=function()return state.focus end,
Handle=function()error("Never call mutable preset sender")end}
SFSNetwork={SendMessage=function()state.sends=state.sends+1 end}
LWBridgeFeature018EquipmentActions={
 Preflight=function(fields,presets,focus)
  assert(fields.action=="equipment" and fields.slot=="2")
  assert(fields.authorizedUid==state.uid and fields.presetId=="preset-two")
  assert(fields.profileId=="profile" and fields.gamePid=="9012")
  assert(type(presets)=="table" and #presets==2)
  assert(presets[2].id=="preset-two")
  if not focus() then
   return {state="failed",errorCode="GAME_PRECONDITION_FAILED"}
  end
  state.inspections=state.inspections+1
  return {state="confirmed",data={
   observation="native_HeroEquip_slot_inventory_preflight",
   readOnly=true,readyToApply=false,requestDispatched=false,
   changedSlots=1,slot=2,presetId="preset-two",slots={{heroUuid="202",slot=1}}
  }}
 end
}
json_decoder={decode=function(text)
 assert(text=='[{"id":"preset-one","name":"First","squads":[]},{"id":"preset-two","name":"Second","squads":[]}]')
 return {{id="preset-one",name="First",squads={}},
         {id="preset-two",name="Second",squads={}}}
end}
package.loaded["rapidjson"]=json_decoder
bridge={handlers={},RegisterHandler=function(name,func)
 assert((name=="feature018_equipment_snapshot" or
         name=="feature018_equipment_preflight") and type(func)=="function")
 if bridge.handlers[name]~=nil then return false end
 bridge.handlers[name]=func
 return true
end,Array=function(value)return value end}
"""

def ensure(condition, description):
    if not condition:
        raise AssertionError("FEATURE018_EQUIPMENT_INTEGRATION: " + description)

def main():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    bridge = vm.globals().bridge
    action = vm.execute(SOURCE.read_text(encoding="utf-8"))
    ensure(action.RegisterEquipment(bridge, vm.globals().frozen),
           "native equipment read-only producer not registered")
    config = '[{"id":"preset-one","name":"First","squads":[]},{"id":"preset-two","name":"Second","squads":[]}]'

    def call(**override):
        fields = {
            "slot":"2", "profileId":"profile","launchSessionId":"launch",
            "challenge":"nonce","gamePid":"9012",
            "confirmedEquipmentPresets":config,
        }
        fields.update(override)
        return bridge.handlers["feature018_equipment_preflight"](vm.table_from(fields))

    state = vm.globals().state
    ensure(bridge.handlers["feature018_equipment_snapshot"] is not None and
           bridge.handlers["feature018_equipment_preflight"] is not None,
           "equipment snapshot and preflight must each have a native handler")
    snapshot = bridge.handlers["feature018_equipment_snapshot"](vm.table_from({}))
    ensure(snapshot["state"]=="unsupported" and state["sends"]==0,
           "unavailable native snapshot must not fall back to equipment installation")
    good = call(authorizedUid="spoofed-uid",presetId="forged")
    ensure(good["state"]=="confirmed" and
           good["data"]["observation"]=="native_HeroEquip_slot_inventory_preflight" and
           good["data"]["readyToApply"] is False and
           good["data"]["requestDispatched"] is False and
           state["inspections"]==1 and state["sends"]==0,
           "owner-derived persisted preset preflight mutated game state")
    for server in ("source","selfServer","current"):
        for incorrect in (2212,2213,2289,None):
            state[server]=incorrect
            denied=call()
            ensure(denied["state"]=="failed" and
                   "server-2288" in denied["error"] and state["inspections"]==1,
                   "unapproved server was admitted for equipment: "+server)
        state[server]=2288

    for incorrect in ("3133546747002212","3133546747002289","3133546747002299","invalid",None):
        state["uid"]=incorrect
        vm.globals().LuaEntry.Player.uid=incorrect
        denied=call()
        ensure(denied["state"]=="failed" and "UID" in denied["error"] and
               state["inspections"]==1,
               "main-account, other-account or invalid native UID passed equipment")
    state["uid"]="2553945035002288"
    vm.globals().LuaEntry.Player.uid=state["uid"]
    ensure(call(slot="5")["state"]=="failed", "invalid ordinal passed equipment")
    ensure(call(confirmedEquipmentPresets="not-json")["state"]=="failed",
           "malformed persisted config admitted")
    ensure(call(confirmedEquipmentPresets="")["state"]=="failed",
           "missing confirmed persisted config admitted")
    state["focus"]=False
    ensure(call()["state"]=="failed", "game text-input editor admitted preflight")
    ensure(state["sends"]==0,"equipment preflight called an item-sending method")
    print("FEATURE018_EQUIPMENT_NATIVE_PREFLIGHT_BRIDGE_LUA53_OFFLINE_PASS")

if __name__=="__main__":
    main()
