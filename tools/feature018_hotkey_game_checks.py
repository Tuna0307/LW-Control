"""Offline Lua 5.3 checks for FEATURE018 Hotkeys native-safe game adapter.

Runs only fake manager tables in a separate Lua VM. No game, launcher, update,
network, active session, installed package or foreign files are accessed.
"""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime


SOURCE = Path(__file__).with_name("feature018_hotkey_game.lua")


def create_runtime(managers: str = ""):
    runtime = LuaRuntime(unpack_returned_tuples=True)
    runtime.execute(managers)
    module = runtime.execute(SOURCE.read_text(encoding="utf-8"))
    return runtime, module


def require(condition, message):
    if not condition:
        raise AssertionError("FEATURE018_HOTKEY_GAME: " + message)


BASE = """
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


def main():
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
    lookup = module.Handle(runtime.table_from({"action": "shieldOverlay", "slot": "0"}))
    require(lookup["state"] == "failed", "Space bypassed unknown game text focus")
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
    clear, reason = focused_module.TextInputClear()
    require(clear is True and reason == "no_selected_text_input",
            "real event system with no selected object rejected")
    require(focused_module.InputFocusSnapshot()["data"]["inputClear"] is True,
            "authenticated focus status was not exposed to game bridge")
    require(focused_module.Handle(focus_runtime.table_from(
        {"action": "shieldOverlay", "slot": "0"}))["state"] == "confirmed",
            "Space did not show native shield status when game input was clear")
    for action, slot in [("attack", "1"), ("shieldUse", "8"),
                         ("equipment", "4"), ("frontlineReinforce", "5"),
                         ("randomRelocate", "0"), ("allianceRelocate", "0"),
                         ("recall", "2")]:
        outcome = focused_module.Handle(focus_runtime.table_from(
            {"action": action, "slot": slot}))
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
    require(focused_module.Handle(focus_runtime.table_from(
        {"action": "shieldOverlay", "slot": "0"}))["state"] == "failed",
            "Space action bypassed newly selected input field")
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
