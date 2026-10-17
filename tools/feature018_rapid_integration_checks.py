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
  uid="2553945035002288", source=2288, selfServer=2288, current=2288,
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
  if name=="feature018_attack_prepare" then return true end
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

    return runtime, state, call, module


def main():
    lua, state, call, module = mock()
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
    # Every inventory/read and spending command must re-fetch all three
    # authenticated server signals; server 2212 remains the owner's main.
    for key in ("source", "selfServer", "current"):
        for incorrect in (2212, 2213, 2289, None):
            state[key] = incorrect
            for params in ({}, {"mode": "itemUse", "itemOptIn": "1", "itemId": "30108"},
                           {"mode": "status"}):
                before = (state["probes"], state["sends"], state["status"])
                rejected = call(**params)
                check(rejected["state"] == "failed" and "server-2288" in rejected["error"]
                      and before == (state["probes"], state["sends"], state["status"]),
                      "unauthorized server reached rapid preflight: " + key)
            state[key] = 2288

    def change_native_uid(uid):
        state["uid"] = uid
        lua.globals().LuaEntry.Player.uid = uid

    for incorrect in ("3133546747002212", "3133546747002289", "3133546747002299",
                      "not-an-authenticated-uid", None):
        change_native_uid(incorrect)
        for params in ({}, {"mode": "itemUse", "itemOptIn": "1", "itemId": "30108"},
                       {"mode": "status"}):
            before = (state["probes"], state["sends"], state["status"])
            rejected = call(**params)
            check(rejected["state"] == "failed" and "UID" in rejected["error"]
                  and before == (state["probes"], state["sends"], state["status"]),
                  "different/missing native UID reached rapid preflight")
    change_native_uid("2553945035002288")
    state["focused"] = False
    check(call(mode="itemUse", itemOptIn="1", itemId="30108")["state"] == "failed"
          and state["sends"] == 0, "sender ignored game text input")
    state["focused"] = True
    sent = call(mode="itemUse", itemOptIn="1", itemId="30108")
    check(sent["state"] == "accepted" and
          sent["data"]["requestDispatched"] is True and state["sends"] == 1,
          "item sender did not distinguish accepted submission")
    # A previously accepted spend must not let subsequent read/status calls
    # cross into the main account or another native UID.
    change_native_uid("3133546747002212")
    denied = call(mode="status")
    check(denied["state"] == "failed" and "UID" in denied["error"] and
          state["sends"] == 1 and state["status"] == 1,
          "post-dispatch status observed a different account")
    change_native_uid("2553945035002288")
    state["current"] = 2212
    denied = call(mode="status")
    check(denied["state"] == "failed" and "server-2288" in denied["error"] and
          state["sends"] == 1 and state["status"] == 1,
          "post-dispatch status observed the protected main server")
    state["current"] = 2288
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

    # The older wrapped hotkey route also exposes speedupItem/speedupDiamond:
    # it must enforce the same identity gate and replace forged UI UID fields.
    lua.execute('''
      state.wrapCalls=0
      LWBridgeFeature018AttackActions.Handle=function(fields)
        state.wrapCalls=state.wrapCalls+1
        assert(fields.authorizedUid==state.uid)
        return {state="confirmed",data={observation="inert_wrap_test"}}
      end
    ''')
    wrapped = module.Wrap(lua.globals().frozen)
    for action in ("speedupItem", "speedupDiamond"):
        reply = wrapped(lua.table_from({"action": action, "authorizedUid": "forged-main-uid"}))
        check(reply["state"] == "confirmed", "wrapped speedup did not derive native UID")
    check(state["wrapCalls"] == 2, "wrapped test producer invocation missing")
    for action in ("speedupItem", "speedupDiamond"):
        change_native_uid("3133546747002212")
        denied = wrapped(lua.table_from({"action": action}))
        check(denied["state"] == "failed" and state["wrapCalls"] == 2,
              "wrapped speedup admitted main-account UID")
        change_native_uid("2553945035002288")
        state["current"] = 2212
        denied = wrapped(lua.table_from({"action": action}))
        check(denied["state"] == "failed" and state["wrapCalls"] == 2,
              "wrapped speedup admitted main-account server")
        state["current"] = 2288
    print("FEATURE018_RAPID_NATIVE_BRIDGE_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
