"""Inert v26 Hotkeys shield-item duration, warning and ACK inverse checks.

No game/launcher/network/journal/installation is accessed. All managers below
are fake Lua tables. Source-backed status.time and ItemUse shapes are audited
against the archived 3/26 Lua package separately, before integration.
"""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

MODULE = Path(__file__).with_name("feature018_hotkey_game.lua")

GAME = """
state = {
  items = {
    {itemId=9, uuid=101001, count=2},
    {itemId=10, uuid=101002, count=1},
    {itemId=11, uuid=101003, count=1},
  },
  templates = {{id=9,para1=501},{id=10,para1=502},{id=11,para1=503}},
  seconds = {[501]=28800,[502]=43200,[503]=86400},
  effectEnd = {},
  sent = 0, cooldownSets = 0, cd = false, war = nil,
  season = false, meteorite = false, protectEnd = 0,
  serverTime = 1000000, focus = true, failCD = false,
}
LuaEntry = {Player = {uid='3133546747002289',
  GetSourceServerId=function(self) return 2289 end,
  GetSelfServerId=function(self) return 2289 end,
  GetCurServerId=function(self) return 2289 end}}
DataCenter = {
  ItemData = {GetItemList=function(self, t, u)
    assert(t==4 and u==1)
    return state.items
  end},
  ItemTemplateManager = {GetTypeListByTypes=function(self, t, u)
    assert(t==4 and u==1)
    return state.templates
  end},
  StatusManager = {
    GetShieldStatusIds=function(self)
      return {[501]=true, [502]=true, [503]=true}
    end,
    GetAllStatusItem=function(self) return state.effectEnd end,
    GetBuffTimeInfo=function(self, id)
      return {endTime=state.effectEnd[id], totalTime=state.seconds[id]*1000}
    end,
    IsUseShieldCD=function(self) return state.cd end,
    WarFeverStatu=function(self) return state.war end,
    SetUseShieldCD=function(self)
      if state.failCD then error('native cooldown failed') end
      state.cooldownSets = state.cooldownSets + 1
    end
  },
  DefenceWallDataManager={GetDefenceWallData=function(self)
    return {protectEndTime=state.protectEnd}
  end},
  ActMeteoriteBattleManager={NeedNoticeShield=function(self)
    return state.meteorite
  end},
}
TableName = {StatusTab='StatusTab'}
LocalController = {instance=function(self)
  return {getLine=function(self, tableName, id)
    assert(tableName=='StatusTab')
    if state.seconds[id] then return {time=state.seconds[id]} end
    return nil
  end}
end}
UITimeManager = {GetInstance=function(self)
  return {GetServerTime=function(self) return state.serverTime end}
end}
SeasonUtil = {IsInSeason=function() return state.season end}
MsgDefines = {ItemUse='ItemUse'}
SFSNetwork = {SendMessage=function(name, data)
  assert(name=='ItemUse' and data.num==1)
  state.sent = state.sent+1
  state.lastUuid = tostring(data.uuid)
end}
typeof=function(t) return t end
IsNull=function(v) return v==nil end
CS = {UnityEngine = {
  UI = {InputField='InputField'},
  EventSystems={EventSystem={current={currentSelectedGameObject=nil}}}
}, TMPro={TMP_InputField='TMP_InputField', TMP_InputFieldEx='TMP_InputFieldEx'}}
"""


def require(value, detail):
    if not value:
        raise AssertionError("FEATURE018_SHIELD: " + detail)


def run():
    runtime = LuaRuntime(unpack_returned_tuples=True)
    runtime.execute(GAME)
    module = runtime.execute(MODULE.read_text(encoding="utf-8"))
    return runtime, module, runtime.globals().state


def command(runtime, module, hours, *, query=False, challenge="challenge"):
    values = {
        "action": "shieldUse",
        "slot": str(hours),
        "profileId": "profile",
        "launchSessionId": "launch",
        "challenge": challenge,
        "gamePid": "7020",
    }
    if query:
        values["queryOnly"] = "1"
    return module.Handle(runtime.table_from(values))


def main():
    # Current v26 ItemUseMessage.OnCreate PutUtfString("uuid") accepts a
    # game-issued opaque stack UUID: numeric itemId and stack UUID are separate.
    runtime, module, state = run()
    opaque_uuid = "shield-stack-" + "a" * 48
    state["items"][1]["uuid"] = opaque_uuid
    sent_opaque = command(runtime, module, 8)
    require(sent_opaque["state"] == "accepted" and
            sent_opaque["data"]["itemUuid"] == opaque_uuid and
            state["lastUuid"] == opaque_uuid and state["sent"] == 1,
            "source PutUtfString rejected genuine long owned stack UUID")
    state["items"][1]["count"] = 1
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600000
    require(command(runtime, module, 8, query=True)["state"] == "confirmed",
            "opaque game-owned ItemUse UUID lost poststate correlation")
    for bad in ("", "bad space", "bad\nline", "z" * 129):
        runtime, module, state = run()
        state["items"][1]["uuid"] = bad
        require(command(runtime, module, 8)["state"] == "unsupported" and state["sent"] == 0,
                "invalid native item UUID was forwarded to ItemUse")
    for hours, uuid, status in ((8, "101001", 501), (12, "101002", 502), (24, "101003", 503)):
        runtime, module, state = run()
        require(command(runtime, module, hours, query=True)["state"] == "failed",
                "read-only query accepted without a prior ItemUse")
        sent = command(runtime, module, hours)
        require(sent["state"] == "accepted" and
                sent["data"]["requestDispatched"] is True and
                sent["data"]["itemUuid"] == uuid and state["sent"] == 1 and
                state["lastUuid"] == uuid and state["cooldownSets"] == 1,
                f"exact {hours}h owned StatusTab item was not sent once")
        require(command(runtime, module, hours)["state"] == "failed" and state["sent"] == 1,
                "second hotkey consumed the same pending shield again")
        pending = command(runtime, module, hours, query=True)
        require(pending["state"] == "accepted" and
                pending["data"]["requestDispatched"] is False and state["sent"] == 1,
                "pre-ACK shield query dispatched a duplicate ItemUse")
        state["items"][{8: 1, 12: 2, 24: 3}[hours]]["count"] -= 1
        state["effectEnd"][status] = state["serverTime"] + hours * 3600 * 1000
        confirmed = command(runtime, module, hours, query=True)
        require(confirmed["state"] == "confirmed" and
                confirmed["data"]["observation"] == "native_owned_shield_consumption_and_MAIN_SHIELD" and
                confirmed["data"]["consumedCount"] == 1 and
                confirmed["data"]["durationMilliseconds"] == hours * 3600 * 1000 and
                confirmed["data"]["itemUuid"] == uuid and state["sent"] == 1,
                f"{hours}h poststate did not require inventory decrease and matching MAIN_SHIELD")
        require(command(runtime, module, hours, query=True)["state"] == "failed",
                "completed shield query unexpectedly reused former pending record")

    runtime, module, state = run()
    require(command(runtime, module, 8)["state"] == "accepted", "baseline 8h send")
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600 * 1000
    require(command(runtime, module, 8, query=True)["state"] == "accepted",
            "status-only update falsely confirmed shield consumption")
    state["effectEnd"] = runtime.table_from({})
    state["items"][1]["count"] = 1
    require(command(runtime, module, 8, query=True)["state"] == "accepted",
            "inventory-only decrement falsely confirmed active matching shield")
    require(command(runtime, module, 8, query=True, challenge="foreign")["state"] == "failed",
            "foreign owner was allowed to reattach an earlier pending ItemUse")

    for field, value, reason in (
        ("cd", True, "cooldown"),
        ("war", 1, "war fever"),
        ("season", True, "seasonal danger"),
        ("meteorite", True, "meteorite notice"),
        ("protectEnd", 1000001, "replacement confirmation"),
        ("failCD", True, "cooldown send failure"),
    ):
        runtime, module, state = run()
        state[field] = value
        require(command(runtime, module, 24)["state"] != "accepted" and state["sent"] == 0,
                f"{reason} bypassed native shield precondition")

    runtime, module, state = run()
    state["items"][1]["count"] = 0
    require(command(runtime, module, 8)["state"] == "unsupported" and state["sent"] == 0,
            "empty 8h stock triggered purchased item/diamonds")
    runtime, module, state = run()
    state["templates"][1]["para1"] = 503
    require(command(runtime, module, 8)["state"] == "unsupported" and state["sent"] == 0,
            "uncertain duration consumed guessed item ID")
    runtime, module, state = run()
    state["items"][2]["itemId"] = 9
    require(command(runtime, module, 8)["state"] == "unsupported" and state["sent"] == 0,
            "ambiguous identical-duration owned item stacks consumed arbitrarily")

    # The game bridge admits a profile, but the mutating Lua producer must
    # also independently bind exact native player identity and all servers.
    for expression in (
        "LuaEntry.Player.uid='3133546747002212'",
        'LuaEntry.Player.GetSourceServerId=function() return 2212 end',
        'LuaEntry.Player.GetSelfServerId=function() return 2212 end',
        'LuaEntry.Player.GetCurServerId=function() return 2212 end',
        'LuaEntry.Player.GetSelfServerId=nil',
        'LuaEntry.Player.uid=nil',
    ):
        runtime, module, state = run()
        runtime.execute(expression)
        require(command(runtime, module, 8)["state"] == "failed" and state["sent"] == 0
                and state["cooldownSets"] == 0,
                "foreign/missing native owner reached ItemUse: " + expression)

    runtime, module, state = run()
    require(command(runtime, module, 8)["state"] == "accepted", "baseline owned ItemUse")
    runtime.execute("LuaEntry.Player.GetCurServerId=function() return 2212 end")
    require(command(runtime, module, 8, query=True)["state"] == "failed",
            "read-only ItemUse status used across a main-account transition")
    require(command(runtime, module, 8)["state"] == "failed" and state["sent"] == 1,
            "unresolved ItemUse resent on the main account")
    runtime.execute("LuaEntry.Player.GetCurServerId=function() return 2289 end")
    state["items"][1]["count"] = 1
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600000
    require(command(runtime, module, 8, query=True)["state"] == "confirmed" and state["sent"] == 1,
            "original owner could not safely read independent ItemUse poststate")

    runtime, module, state = run()
    runtime.execute('''
      local prior=DataCenter.StatusManager.SetUseShieldCD
      DataCenter.StatusManager.SetUseShieldCD=function(self)
        prior(self)
        LuaEntry.Player.uid='3133546747002212'
      end
    ''')
    require(command(runtime, module, 8)["state"] == "failed" and state["sent"] == 0,
            "game owner changed during cooldown and reached mutable ItemUse")

    runtime, module, state = run()
    runtime.execute('''
      local original=SFSNetwork.SendMessage
      SFSNetwork.SendMessage=function(...) original(...); return false end
    ''')
    require(command(runtime, module, 8)["state"] == "failed" and state["sent"] == 1,
            "uncertain native ItemUse boolean result was reported as dispatched")
    require(command(runtime, module, 8)["state"] == "failed" and state["sent"] == 1,
            "uncertain false ItemUse result allowed a duplicate")
    state["items"][1]["count"] = 1
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600000
    require(command(runtime, module, 8, query=True)["state"] == "confirmed" and
            state["sent"] == 1,
            "uncertain transmitted ItemUse could not be independently verified")

    print("FEATURE018_HOTKEY_SHIELD_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
