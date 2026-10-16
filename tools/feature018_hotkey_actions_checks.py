"""Current-v26 Hotkeys source-path unit checks in an inert Lua 5.3 VM.

No installed game, account, launcher, updater, UI session, network or journal
is started or modified. State objects below are isolated fake game managers.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

SOURCE = Path(__file__).with_name("feature018_hotkey_actions.lua")

GAME = """
state = {
    playerUid='222', focusClear=true, canReturn=true,
    ownerUid='222', formationId=1001, candidateFormationId=1001,
    squadUuid=803001, candidateUid='222', target=17, dispatches=0,
    slot=1, numberOfOwnedShield=2,
}
LuaEntry = { Player={uid=state.playerUid}, DataConfig={
    CheckSwitch=function(self, name)
        assert(name=='slide_left_return')
        return state.canReturn
    end
}}
MarchTargetType = {BACK_HOME=77}
DataCenter = {
    ArmyFormationDataManager={
        GetOneArmyInfoByIndex=function(self,slot)
            if state.slot ~= slot then return nil end
            return {uuid=state.formationId}
        end
    },
    WorldMarchDataManager={
        GetOwnerFormationMarch=function(self, uid,formation,extra)
            assert(type(uid)=='string' and extra=='')
            if uid ~= state.playerUid or formation~=state.formationId then
                return nil
            end
            return {uuid=state.squadUuid,ownerUid=state.candidateUid,
                    ownerFormationUuid=state.candidateFormationId,
                    GetMarchTargetType=function(self)return state.target end}
        end,
        GetMarch=function(self,uuid)
            if uuid ~= state.squadUuid then return nil end
            return {uuid=state.squadUuid,ownerUid=state.ownerUid,
                    ownerFormationUuid=state.formationId,
                    GetMarchTargetType=function(self) return state.target end}
        end,
    },
    ItemData={
        GetItemList=function(self,t,sub)
            assert(t==4 and sub==1)
            return {
                {itemId=8,uuid=555001,count=state.numberOfOwnedShield},
                {itemId=12,uuid=555002,count=0},
                {itemId=99,uuid=555003,count=4},
            }
        end
    },
    ItemTemplateManager={
        GetTypeListByTypes=function(self,t,sub)
            assert(t==4 and sub==1)
            return {{id=8,type=4,type2=1},{id=12,type=4,type2=1}}
        end
    },
}
MarchUtil={OnBackHome=function(uuid)
    assert(uuid==state.squadUuid)
    state.dispatches=state.dispatches+1
end}
frozen={Handle=function(fields)
    return {state='unsupported',errorCode='FALLBACK_USED',
            error='frozen handler',action=fields.action}
end, TextInputClear=function() return state.focusClear end}
"""

def require(condition, message):
    if not condition:
        raise AssertionError("FEATURE018_HOTKEY_ACTIONS: " + message)


def scenario():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute(GAME)
    module = lua.execute(SOURCE.read_text(encoding="utf-8"))
    wrapped = module.Wrap(lua.globals().frozen)
    state = lua.globals().state
    def call(action="recall", slot="1", profile="profile", challenge="challenge"):
        return wrapped(lua.table_from({
            "action": action, "slot": slot,
            "profileId": profile, "launchSessionId": "launch",
            "challenge": challenge, "gamePid": "7020",
        }))
    return lua, module, wrapped, state, call


def main():
    lua, actions, _, state, call = scenario()
    first = call()
    require(first["state"] == "accepted" and first["data"]["requestDispatched"] is True,
            "source-verified owned return did not report send-only")
    require(state["dispatches"] == 1, "native UI-equivalent return not invoked exactly once")
    repeat = call()
    require(repeat["state"] == "accepted" and repeat["data"]["requestDispatched"] is False,
            "pending return was resent before receiving authoritative state")
    require(state["dispatches"] == 1, "duplicate return dispatch")
    state["target"] = 77
    confirmed = call()
    require(confirmed["state"] == "confirmed" and
            confirmed["data"]["observation"] == "native_world_march_return_target",
            "server-updated native march target not recognized")
    require(state["dispatches"] == 1, "poststate polling created another request")
    confirmed_again = call()
    require(confirmed_again["state"] == "confirmed" and state["dispatches"] == 1,
            "already returning march was resent")

    _, _, _, s, call = scenario()
    s["candidateUid"] = "foreignUid"
    require(call()["state"] == "unsupported" and s["dispatches"] == 0,
            "foreign owner march admitted after misleading native GetOwnerFormationMarch")
    s["candidateUid"] = "222"
    s["candidateFormationId"] = 1111
    require(call()["state"] == "unsupported" and s["dispatches"] == 0,
            "wrong formation march admitted")
    s["candidateFormationId"] = 1001
    s["ownerUid"] = "foreignUid"
    require(call()["state"] == "unsupported" and s["dispatches"] == 0,
            "cached march owner mismatch admitted")

    _, _, _, s, call = scenario()
    s["canReturn"] = False
    require(call()["state"] == "failed" and s["dispatches"] == 0,
            "return bypassed current game confirmation setting")
    s["canReturn"] = True
    s["focusClear"] = False
    require(call()["state"] == "failed" and s["dispatches"] == 0,
            "return shortcut executed in active game text editor")
    s["focusClear"] = True
    require(call(profile="bad profile")["state"] == "failed" and s["dispatches"] == 0,
            "invalid game identity admitted")
    require(call(slot="5")["state"] == "failed" and s["dispatches"] == 0,
            "unknown squad slot admitted")
    require(call(slot="2")["state"] == "unsupported" and s["dispatches"] == 0,
            "empty slot triggered return on another squad")

    _, _, _, s, call = scenario()
    for slot in ("1","2","3","4"):
        outcome = call(action="attack",slot=slot)
        require(outcome["state"] == "unsupported" and s["dispatches"] == 0,
                "cursor attack dispatched without typed game cursor target: " + slot)
    for duration in ("8", "12", "24"):
        outcome = call(action="shieldUse",slot=duration)
        require(outcome["state"] == "unsupported" and s["dispatches"] == 0,
                "shield item consumed without source-mapped duration: " + duration)
    require(call(action="shieldUse",slot="99")["state"] == "failed",
            "unexpected shield duration accepted")
    require(call(action="equipment",slot="1")["errorCode"] == "FALLBACK_USED",
            "unrelated frozen Hotkeys handler overridden")

    shield = actions.ShieldOwnedInventory()
    require(shield["state"] == "confirmed", "owned shield inventory unavailable")
    require(shield["data"]["durationMappingVerified"] is False,
            "shield duration mapping falsely claimed")
    items = shield["data"]["items"]
    require(len(items) == 1 and items[1]["itemId"] == "8" and
            items[1]["uuid"] == "555001" and items[1]["count"] == 2,
            "shield inventory leaked unowned/zero/non-type4 item")
    state["numberOfOwnedShield"] = 0
    require(len(actions.ShieldOwnedInventory()["data"]["items"]) == 0,
            "out-of-stock shield item listed")
    lua.globals().DataCenter.ItemTemplateManager = None
    require(actions.ShieldOwnedInventory()["state"] == "unsupported",
            "missing native shield template source admitted")
    print("FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
