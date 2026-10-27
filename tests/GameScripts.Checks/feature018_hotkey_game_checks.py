"""Offline Lua 5.3 checks for FEATURE018 Hotkeys native-safe game adapter.

Runs only fake manager tables in a separate Lua VM. No game, launcher, update,
network, active session, installed package or foreign files are accessed.
"""

from pathlib import Path
_REPO = __import__('pathlib').Path(__file__).resolve().parents[2]
_SCRIPTS = _REPO / 'src' / 'LWBridge.GameScripts'
_RESEARCH = _REPO / 'tools' / 'research'
__import__('sys').path.append(str(_SCRIPTS))
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime
import feature018_hotkey_shield_checks as shield_checks


SOURCE = (_SCRIPTS / "feature018_hotkey_game.lua")


def create_runtime(managers: str = ""):
    runtime = LuaRuntime(unpack_returned_tuples=True)
    runtime.execute(managers)
    module = runtime.execute(SOURCE.read_text(encoding="utf-8"))
    return runtime, module


def require(condition, message):
    if not condition:
        raise AssertionError("FEATURE018_HOTKEY_GAME: " + message)


BASE = """
LuaEntry = {Player = {
    uid = '3133546747002289',
    GetSourceServerId = function() return 2289 end,
    GetSelfServerId = function() return 2289 end,
    GetCurServerId = function() return 2289 end,
}}
DataCenter = {StatusManager = {
    GetShieldStatusIds = function(self) return {[1001] = true, [1002] = true} end,
    GetAllStatusItem = function(self) return {[1001] = 130000, [1002] = 100000} end,
    GetBuffTimeInfo = function(self, id)
        if id == 1001 then return {endTime = 130000, totalTime = 7200000, intKey = id} end
        return {endTime = 100000, totalTime = 0, intKey = id}
    end,
}}
UITimeManager = {GetInstance = function(self)
    return {GetServerTime = function(self) return 100000 end}
end}
"""

def test_shield_request_and_pending_account_fences():
    def switched(lua, uid, server):
        player = lua.globals().LuaEntry.Player
        player.uid = uid
        for name in ("GetSourceServerId", "GetSelfServerId", "GetCurServerId"):
            player[name] = lua.eval(f"function() return {server} end")

    def request(lua, module, uid="2553945035002288", query=False):
        fields = {
            "action": "shieldUse", "slot": "8",
            "profileId": "profile", "launchSessionId": "launch",
            "challenge": "challenge", "gamePid": "7020",
        }
        if uid is not None:
            fields["authorizedUid"] = uid
        if query:
            fields["queryOnly"] = "1"
        return module.Handle(lua.table_from(fields))

    lua, module, state = shield_checks.run()
    require(request(lua, module, None)["state"] == "failed" and
            request(lua, module, "3133546747002212")["state"] == "failed" and
            state["sent"] == 0 and state["cooldownSets"] == 0,
            "missing or mismatched request UID reached native ItemUse")
    lua.execute("""
      LuaEntry.Player.GetCurServerId=function(self)
        self.uid='3133546747002212'
        return 2288
      end
    """)
    require(request(lua, module)["state"] == "failed" and state["sent"] == 0,
            "UID switched within server getter but original ItemUse was sent")
    switched(lua, "2553945035002288", 2288)
    switched(lua, "3133546747002212", 2212)
    require(request(lua, module, "3133546747002212")["state"] == "accepted" and
            state["sent"] == 1, "valid different account could not send native ItemUse")
    switched(lua, "2553945035002288", 2288)
    require(request(lua, module, query=True)["state"] == "failed" and
            state["sent"] == 1, "pending shield crossed to a different UID/server")
    state["items"][1]["count"] = 1
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600000
    require(request(lua, module, query=True)["state"] == "failed",
            "new account falsely confirmed another account's completed shield")
    switched(lua, "3133546747002212", 2212)
    require(request(lua, module, "3133546747002212", query=True)["state"] == "confirmed" and
            state["sent"] == 1, "original account could not read its independent shield poststate")

    # The original ItemUseHandle runs before our observer evaluates its reply.
    # Its handler may still be installed when the user changes accounts.
    lua, module, state = shield_checks.run()
    require(request(lua, module)["state"] == "accepted", "baseline shield send missing")
    switched(lua, "3133546747002212", 2212)
    manager = lua.globals().DataCenter.ItemManager
    manager.ItemUseHandle(manager, lua.table_from({
        "uuid": 101001, "itemId": 9, "count": 1
    }))
    switched(lua, "2553945035002288", 2288)
    state["items"][1]["count"] = 1
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600000
    result = request(lua, module, query=True)
    require(result["state"] == "accepted" and
            result["data"]["nativeReplyAmbiguous"] is True and state["sent"] == 1,
            "late original ItemUse reply from another account was credited")

    # SetUseShieldCD is a game method and may change native identity before
    # the network call even when the LuaEntry.Player table itself is stable.
    lua, module, state = shield_checks.run()
    lua.execute("""
      local original=DataCenter.StatusManager.SetUseShieldCD
      DataCenter.StatusManager.SetUseShieldCD=function(self)
        original(self)
        LuaEntry.Player.uid='3133546747002212'
        LuaEntry.Player.GetSourceServerId=function() return 2212 end
        LuaEntry.Player.GetSelfServerId=function() return 2212 end
        LuaEntry.Player.GetCurServerId=function() return 2212 end
      end
    """)
    require(request(lua, module)["state"] == "failed" and state["sent"] == 0,
            "same player table changed its UID/server during cooldown and sent ItemUse")

    lua, module, state = shield_checks.run()
    lua.execute("""
      local send=SFSNetwork.SendMessage
      SFSNetwork.SendMessage=function(...)
        send(...)
        LuaEntry.Player.uid='3133546747002212'
        LuaEntry.Player.GetSourceServerId=function() return 2212 end
        LuaEntry.Player.GetSelfServerId=function() return 2212 end
        LuaEntry.Player.GetCurServerId=function() return 2212 end
      end
    """)
    require(request(lua, module)["state"] == "failed" and state["sent"] == 1,
            "native ItemUse account changed during posted request but was credited")
    switched(lua, "2553945035002288", 2288)
    require(request(lua, module)["state"] == "failed" and state["sent"] == 1,
            "uncertain sent shield was retransmitted after identity restoration")


def test_shield_shop_account_fence():
    lua, module, state = shield_checks.run()
    lua.execute("""
      LuaEntry.Player.gold=1000
      state.items={}
      state.templates[1].price=500
      MsgDefines.UIShopBuy='UIShopBuy'
      DataCenter.ItemManager.ItemBuyHandle=function(self,reply)
        state.originalBuyReplyHandled=(state.originalBuyReplyHandled or 0)+1
      end
      SFSNetwork.SendMessage=function(name,data)
        assert(name=='UIShopBuy' and data.itemId=='9' and data.num==1)
        state.sent=state.sent+1
      end
    """)
    def call(mode, uid="2553945035002288", **kwargs):
        return module.ShieldShop(lua.table_from({
            "mode": mode, "slot": "8", "profileId": "profile",
            "launchSessionId": "launch", "challenge": "challenge",
            "gamePid": "7020", "authorizedUid": uid, **kwargs,
        }))
    require(call("buy", uid="3133546747002212", gemOptIn="1")["state"] == "failed" and
            state["sent"] == 0, "unbound shop request UID spent gems")
    require(call("buy", gemOptIn="1")["state"] == "accepted" and state["sent"] == 1,
            "bound shop request did not retain original action")
    player = lua.globals().LuaEntry.Player
    player.uid = "3133546747002212"
    for getter in ("GetSourceServerId", "GetSelfServerId", "GetCurServerId"):
        player[getter] = lua.eval("function() return 2212 end")
    manager = lua.globals().DataCenter.ItemManager
    manager.ItemBuyHandle(manager, lua.table_from({
        "item": {"id": 9, "uuid": 101900, "count": 1},
        "costGold": 500, "remainGold": 500,
    }))
    player.uid = "2553945035002288"
    for getter in ("GetSourceServerId", "GetSelfServerId", "GetCurServerId"):
        player[getter] = lua.eval("function() return 2288 end")
    lua.execute("state.items={{itemId=9,uuid=101900,count=1}}; LuaEntry.Player.gold=500")
    observed = call("status")
    require(observed["state"] == "accepted" and
            observed["data"]["nativeReplyAmbiguous"] is True and state["sent"] == 1,
            "purchase callback from different account confirmed native shop spend")


def main():
    test_shield_request_and_pending_account_fences()
    test_shield_shop_account_fence()
    runtime, module = create_runtime(BASE)
    result = module.ShieldStatusSnapshot()
    require(result["state"] == "confirmed", "verified shield was rejected")
    data = result["data"]
    require(data["shieldStatusCount"] == 1 and data["active"] is True,
            "expired shield was included or active status lost")
    require(data["remainingMilliseconds"] == 30000 and data["endTime"] == 130000,
            "game server remaining time was not used")
    require(data["statuses"][1]["statusId"] == 1001, "wrong shield ID")

    exposed = runtime.eval("{RegisterHandler = function(name, handler) return (name == 'feature018_hotkey_action' or name == 'feature018_input_focus') and type(handler) == 'function' end}")
    require(module.Register(exposed) is True, "plugin failed registration contract")
    require(module.InputFocusSnapshot()["state"] == "unsupported",
            "unknown input-focus status falsely reported safe")

    for action, slot in [("attack", "1"), ("shieldUse", "8"),
                         ("equipment", "4"), ("frontlineReinforce", "5"),
                         ("randomRelocate", "0"), ("allianceRelocate", "0"),
                         ("recall", "2")]:
        outcome = module.Handle(runtime.table_from({"action": action, "slot": slot}))
        require(outcome["state"] == "failed", "unknown game input-focus bypassed: " + action)
    require(module.Handle(runtime.table_from({"action": "shieldUse", "slot": "0"}))["state"] == "failed",
            "wrong duration accepted")
    require(module.Handle(runtime.table_from({"action": "arbitrary", "slot": "5"}))["state"] == "failed",
            "arbitrary gameplay calls admitted")

    _, missing = create_runtime()
    require(missing.ShieldStatusSnapshot()["state"] == "unsupported",
            "missing native state falsely succeeded")

    _, mismatch = create_runtime(BASE.replace("endTime = 130000", "endTime = 140000"))
    require(mismatch.ShieldStatusSnapshot()["state"] == "unsupported",
            "different status manager and Effect status values falsely succeeded")

    no_shield_runtime, none = create_runtime(BASE.replace("[1001] = 130000", "[1001] = 100000"))
    no_shield_bridge = no_shield_runtime.eval("""
        { RegisterHandler=function(name, handler) return type(handler)=='function' end,
          Array=function(values)
              return setmetatable(values, {__feature018_array=true})
          end }
    """)
    require(none.Register(no_shield_bridge) is True,
            "shield status array encoder was not registered")
    status = none.ShieldStatusSnapshot()
    require(status["state"] == "confirmed" and status["data"]["active"] is False and
            status["data"]["remainingMilliseconds"] == 0,
            "expired shield did not report inactive")
    status_array = status["data"]["statuses"]
    require(len(status_array) == 0 and
            no_shield_runtime.eval("function(values) return getmetatable(values).__feature018_array end")(status_array),
            "inactive shield statuses would serialize as an object rather than []")

    _, multiple = create_runtime(BASE.replace("[1002] = 100000", "[1002] = 120000")
                                  .replace("endTime = 100000", "endTime = 120000"))
    many = multiple.ShieldStatusSnapshot()
    require(many["state"] == "confirmed" and many["data"]["shieldStatusCount"] == 2,
            "multiple native buffs lost")
    require(many["data"]["remainingMilliseconds"] is None,
            "ambiguous overlay chose an arbitrary active shield")

    clear, reason = module.TextInputClear()
    require(clear is False and reason == "game_input_focus_types_unavailable",
            "missing Unity event system was admitted")
    focus_game = """
typeof = function(t) return t end
IsNull = function(v) return v == nil end
selectedObject = nil
CS = {
    UnityEngine = {
        UI = {InputField = 'InputField'},
        EventSystems = {EventSystem = {current = {currentSelectedGameObject = nil}}},
    },
    TMPro = {TMP_InputField = 'TMP_InputField', TMP_InputFieldEx = 'TMP_InputFieldEx'},
}
function SetFocus(obj)
    CS.UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject = obj
end
"""
    focus_runtime, focused_module = create_runtime(BASE + focus_game)
    def focused_request(action, slot, **extra):
        return focus_runtime.table_from({
            "action": action, "slot": slot,
            "authorizedUid": "3133546747002289", **extra
        })
    clear, reason = focused_module.TextInputClear()
    require(clear is True and reason == "no_selected_text_input",
            "real event system with no selected object rejected")
    require(focused_module.InputFocusSnapshot()["data"]["inputClear"] is True,
            "authenticated focus status was not exposed to game bridge")
    for action, slot in [("attack", "1"), ("shieldUse", "8"),
                         ("equipment", "4"), ("frontlineReinforce", "5"),
                         ("randomRelocate", "0"), ("allianceRelocate", "0"),
                         ("recall", "2")]:
        outcome = focused_module.Handle(focused_request(action, slot))
        require(outcome["state"] == "unsupported",
                "unproven action claimed game execution when text focus clear: " + action)
    focus_runtime.execute("""
        SetFocus({GetComponentInParent=function(self, typ)
            if typ == 'TMP_InputField' then return {} end
            return nil
        end})
    """)
    clear, reason = focused_module.TextInputClear()
    require(clear is False and reason == "native_game_text_input_selected",
            "focused native TMP field did not suppress hotkey")
    require(focused_module.InputFocusSnapshot()["data"]["inputClear"] is False,
            "input-focus response failed to disarm native capture")
    focus_runtime.execute("SetFocus({GetComponentInParent=function(self,typ) return nil end})")
    clear, _ = focused_module.TextInputClear()
    require(clear is True, "ordinary selected game object blocked despite all input classes checked")
    focus_runtime.execute("SetFocus({})")
    clear, _ = focused_module.TextInputClear()
    require(clear is False, "non-inspectable focused control was admitted")
    focus_runtime.execute("CS.TMPro.TMP_InputFieldEx = nil")
    clear, _ = focused_module.TextInputClear()
    require(clear is False, "missing current-client TMP variant was admitted")
    require(focused_module.InputFocusSnapshot()["state"] == "unsupported",
            "missing game input type was reported safe by authenticated bridge")

    print("FEATURE018_HOTKEY_GAME_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
