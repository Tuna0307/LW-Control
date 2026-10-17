"""Inert current-v26 Hotkeys rapid command bridge integration checks.

Only an in-memory Lua 5.3 fixture is used. No game, installed script, account,
network, hotkey, native window, or recovery journal is accessed or changed.
"""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "feature018_hotkey_actions.lua").read_text(encoding="utf-8")

GAME = r"""
state = {
  uid="3133546747002212", source=2212, selfServer=2212, current=2212,
  focused=true, sends=0, probes=0, status=0, items=2, pending=false,
}
LuaEntry={Player={
 uid=state.uid,
 GetSourceServerId=function(self)return state.source end,
 GetSelfServerId=function(self)return state.selfServer end,
 GetCurServerId=function(self)return state.current end,
}}
local rapid={
  OwnedSpeedupInventory=function(fields, focus)
    assert(fields.authorizedUid==state.uid and fields.action=="speedupItem")
    assert(fields.slot=="2" and fields.itemId==nil and fields.itemOptIn==nil)
    state.probes=state.probes+1
    local items = {}
    if state.items>0 then items={{itemId="30108",count=state.items}} end
    return {state="confirmed", data={
       observation="native_owned_rapid_item_candidates",
       slot=fields.slot,marchUuid="901234",items=items
    }}
  end,
  Handle=function(fields, focus)
    assert(fields.authorizedUid==state.uid and fields.action=="speedupItem")
    assert(fields.slot=="2")
    if fields.queryOnly=="1" then
      assert(fields.itemOptIn==nil and fields.itemId==nil)
      state.status=state.status+1
      if not state.pending then return {state="failed",error="No pending item"} end
      if state.items>1 then
        return {state="accepted",data={requestDispatched=false,
          awaitingGameConfirmation=true,marchUuid="901234"}}
      end
      state.pending=false
      return {state="confirmed",data={observation=
        "native_march_endTime_and_item_count_transition",
        marchUuid="901234",slot="2",priorEndTime=3000,
        currentEndTime=2600,priorCount=2,currentCount=1}}
    end
    assert(fields.itemOptIn=="1" and fields.itemId=="30108")
    assert(focus() and not state.pending)
    state.sends=state.sends+1
    state.pending=true
    return {state="accepted",data={requestDispatched=true,
      awaitingGameConfirmation=true,slot="2",marchUuid="901234",
      itemId="30108"}}
  end
}
LWBridgeFeature018AttackActions=rapid
frozen={TextInputClear=function()return state.focused end,
 Handle=function()return {state="unsupported"} end}
bridge={
 handler=nil,
 RegisterHandler=function(name, fn)
  assert(name=="feature018_rapid_action" and type(fn)=="function")
  assert(bridge.handler==nil)
  bridge.handler=fn
  return true
 end,
 Array=function(value)setmetatable(value,{__feature018_array=true}) end
}
"""


def check(value, message):
    if not value:
        raise AssertionError("FEATURE018_RAPID_INTEGRATION: " + message)


def mock():
    runtime = LuaRuntime(unpack_returned_tuples=True)
    runtime.execute(GAME)
    module = runtime.execute(SOURCE)
    bridge = runtime.globals().bridge
    check(module.RegisterRapid(bridge, runtime.globals().frozen),
          "rapid native command registration failed")
    state = runtime.globals().state

    def call(mode="inventory", slot="2", **kwargs):
        fields = {"mode": mode, "slot": slot, "profileId": "profile",
                  "launchSessionId": "launch", "challenge": "nonce",
                  "gamePid": "9123"}
        fields.update(kwargs)
        return bridge.handler(runtime.table_from(fields))

    return runtime, state, call


def main():
    lua, state, call = mock()
    inventory = call()
    check(inventory["state"] == "confirmed" and
          inventory["data"]["observation"] == "native_owned_rapid_item_candidates" and
          inventory["data"]["items"][1]["itemId"] == "30108" and
          state["sends"] == 0 and state["probes"] == 1,
          "native verified inventory was not accessible without a send")
    state["items"] = 0
    empty = call()
    check(empty["state"] == "confirmed" and
          empty["data"]["items"][1] is None and
          lua.eval("getmetatable")(empty["data"]["items"])["__feature018_array"],
          "empty owned item array not tagged to serialize as JSON []")
    state["items"] = 2
    check(call(mode="status")["state"] == "failed" and state["sends"] == 0,
          "status started a rapid item request")
    for fields in (
        {"mode": "itemUse", "itemId": "30108"},
        {"mode": "itemUse", "itemOptIn": "0", "itemId": "30108"},
        {"mode": "itemUse", "itemOptIn": "1", "itemId": "30108", "slot": "9"},
        {"mode": "inventory", "itemId": "30108"},
    ):
        check(call(**fields)["state"] == "failed" and state["sends"] == 0,
              "invalid or nonconsensual rapid action reached sender")
    for key in ("source", "selfServer", "current"):
        state[key] = 2289
        rejected = call(mode="itemUse", itemOptIn="1", itemId="30108")
        check(rejected["state"] == "failed" and state["sends"] == 0,
              "unapproved game server reached native item sender: " + key)
        state[key] = 2212
    state["uid"] = "not-an-authenticated-uid"
    check(call(mode="itemUse", itemOptIn="1", itemId="30108")["state"] == "failed"
          and state["sends"] == 0, "unverified player UID reached sender")
    state["uid"] = "3133546747002212"
    state["focused"] = False
    check(call(mode="itemUse", itemOptIn="1", itemId="30108")["state"] == "failed"
          and state["sends"] == 0, "sender ignored game text input")
    state["focused"] = True
    sent = call(mode="itemUse", itemOptIn="1", itemId="30108")
    check(sent["state"] == "accepted" and
          sent["data"]["requestDispatched"] is True and state["sends"] == 1,
          "item sender did not distinguish accepted submission")
    waiting = call(mode="status")
    check(waiting["state"] == "confirmed" and
          waiting["data"]["state"] == "pending" and
          waiting["data"]["requestDispatched"] is False and
          state["sends"] == 1, "read-only pending status resent item or claimed success")
    state["items"] = 1
    observed = call(mode="status")
    check(observed["state"] == "confirmed" and
          observed["data"]["state"] == "confirmed" and
          observed["data"]["observation"] ==
          "native_march_endTime_and_item_count_transition" and
          state["sends"] == 1, "native item/march poststate not independently verified")
    print("FEATURE018_RAPID_NATIVE_BRIDGE_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
