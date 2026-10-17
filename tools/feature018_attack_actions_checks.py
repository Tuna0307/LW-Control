"""Inert v26 attack / march rapid source-backed regression checks.

Reads admitted original Lua bytecode only; Lua game APIs are mock tables.
No client, account, game process, bridge, journal, installed file or RPC.
"""

from __future__ import annotations

import hashlib
import sys
from pathlib import Path

SOURCE = Path(__file__).with_name("feature018_attack_actions.lua")
TOOLS = SOURCE.parent
sys.path.insert(0, str(TOOLS / "map_009_offline"))
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(TOOLS.parent / "artifacts/map-013-lua-site"))

import compact_lua53 as lua53  # noqa: E402
from inspect_current import root_methods, disassemble  # noqa: E402
from lupa.lua53 import LuaRuntime  # noqa: E402
import feature018_mini_lua as original  # noqa: E402


GAME = """
state={
 uid=222, expectedUid="222", formation=1001, owner=222, formOwner=1001,
 targetType=17,
 uuid=555110, slot=1, endTime=3000, now=2000, count=2,
 inputClear=true, itemOptIn="1", rpcCount=0, rpcArgs={}, failRpc=false,
 category=108, stale=false, ownsItem=true,
}
LuaEntry={Player={uid=state.uid,allianceId=700}}
MarchTargetType={ATTACK_CITY=17,ATTACK_ARMY=18}
UITimeManager={GetInstance=function(self) return {
 GetServerTime=function(self) return state.now end
} end}
GOODS_TYPE={GOODS_TYPE_108=108}
DataCenter={
 ArmyFormationDataManager={
  GetOneArmyInfoByIndex=function(self,s)
   if s~=state.slot then return nil end
   return {uuid=state.formation}
  end
 },
 WorldMarchDataManager={
  GetOwnerFormationMarch=function(self,uid,form,extra)
   assert(extra==700)
   if uid~=tostring(state.uid) or form~=state.formation then return nil end
   return {uuid=state.uuid,ownerUid=state.owner,
           ownerFormationUuid=state.formOwner}
  end,
  GetMarch=function(self,uuid)
   if uuid~=state.uuid then return nil end
   return {uuid=state.uuid,ownerUid=state.owner,
           ownerFormationUuid=state.formOwner,
           endTime=state.endTime,
           GetMarchTargetType=function(self) return state.targetType end}
  end
 },
 ItemData={
  GetItemsByType=function(self,category)
   assert(category==state.category)
   if not state.ownsItem then return {} end
   return {{itemId=3108,count=state.count},
           {itemId=666,count=1}}
  end,
  GetItemById=function(self,id)
   if id==3108 and state.ownsItem then
     return {itemId=id,count=(state.stale and state.count+1 or state.count)}
   end
   return nil
  end
 },
 ItemTemplateManager={
  GetTypeListByType=function(self,category)
   assert(category==state.category)
   return {{id=3108,para1="1;5",para3="100",price=0},
           {id=9999,para1="1;10",para3="100",price=10}}
  end
 }
}
MsgDefines={WorldMarchRapid=173}
SFSNetwork={SendMessage=function(route,uuid,itemid,buy)
 assert(route==173 and uuid==state.uuid and itemid==3108 and buy==false)
 state.rpcCount=state.rpcCount+1
 state.rpcArgs={route,uuid,itemid,buy}
 if state.failRpc then error("simulated_rpc_reject") end
end}
input_clear=function() return state.inputClear end
"""


def require(condition: bool, explanation: str) -> None:
    if not condition:
        raise AssertionError("FEATURE018_ATTACK_ACTIONS: " + explanation)


def mock():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    module = vm.execute(SOURCE.read_text(encoding="utf-8"))
    state = vm.globals().state

    def call(action="speedupItem", slot="1", item_id="3108", **overrides):
        fields = {
            "action": action, "slot": slot, "itemId": item_id,
            "itemOptIn": "1", "authorizedUid": "222",
            "profileId": "profile", "launchSessionId": "launch",
            "challenge": "request", "gamePid": "7020",
        }
        fields.update(overrides)
        return module.Handle(vm.table_from(fields), vm.globals().input_clear)

    def inspect(**overrides):
        fields = {
            "slot": "1", "authorizedUid": "222",
            "profileId": "profile", "launchSessionId": "launch",
            "challenge": "request", "gamePid": "7020",
        }
        fields.update(overrides)
        return module.OwnedSpeedupInventory(vm.table_from(fields),
                                             vm.globals().input_clear)

    return state, call, inspect


def check_original() -> None:
    fv, cv, records = original.probe.read_lwlf(original.probe.paths()["data"])
    entries = dict(records)
    raw_original = entries.get(original.probe.ORIGINAL_LUA_ENTRY,
                               entries.get(original.probe.LUA_ENTRY))
    require((fv, cv) == (3, 26) and
            hashlib.sha256(raw_original).hexdigest() ==
            original.ORIGINAL_ENTRY_SHA256,
            "installed source original identity changed")
    expectations = {
        "Util/MarchUtil.luac": (
            "d3c3bb47b11e555ccd646510628c2e428000436ffa8bc8bac290a1320094e245",
            {"OnAttackOtherCity": ["GetPointInfo", "TryStartMarch"],
             "OnAttackArmy": ["GetTroop", "TryStartMarch"]}),
        "UI/UILWRapid/View/UILWRapidView.luac": (
            "19bd32d6cedc24586089e19e12c070b3b73fac7e170dd4917ca8041a11720da3",
            {"GetAllItems": ["GOODS_TYPE_108", "GetItemsByType", "GetTypeListByType"],
             "ReInit": ["GetMarch", "endTime", "ItemSpdMenu_Troop"],
             "ConfirmUse": ["SendMessage", "WorldMarchRapid", "UseTool", "ReduceTime"],
             "ConfirmBuy": ["WorldMarchRapid", "UpdateGold"],
             "CellsCallBack": ["ShowUseDiamondConfirm", "ConfirmUse"]}),
        "Net/Msgs/WorldMarchRapidMessage.luac": (
            "1ba6497b62d041729d2e7b5a7dd907e5067fec8b92893e5e63ecd9cc2e00a8f5",
            {"HandleMessage": ["errorCode", "remainGold"]}),
    }
    for name, (digest, methods) in expectations.items():
        require(hashlib.sha256(entries[name]).hexdigest() == digest,
                f"unrecognized original v26 module {name}")
        root = lua53.parse_chunk(original.probe.decode_lenc(entries[name]))
        paths = root_methods(root)
        for name_, constants in methods.items():
            proto_path = paths[name_]
            proto = next(p for path, p in original.walk(root) if path == proto_path)
            constants_seen = set(proto["constants"])
            require(all(x in constants_seen for x in constants),
                    f"source method {name}.{name_} missing critical delegate {constants}")
            require(len(disassemble(proto)) > 0,
                    f"source method {name}.{name_} has no instructions")


def main():
    check_original()
    state, call, inspect = mock()
    inv = inspect()
    require(inv["state"] == "confirmed" and
            inv["data"]["observation"] == "native_owned_rapid_item_candidates" and
            len(inv["data"]["items"]) == 1 and
            inv["data"]["items"][1]["itemId"] == "3108",
            "native inventory must exclude absent templates and unowned items")
    require(state["rpcCount"] == 0, "inventory introspection dispatched an RPC")
    dispatched = call()
    require(dispatched["state"] == "accepted" and
            dispatched["data"]["requestDispatched"] is True and
            state["rpcCount"] == 1,
            "owned march/item sender not invoked exactly once")
    require(state["rpcArgs"][2] == 555110 and
            state["rpcArgs"][3] == 3108 and state["rpcArgs"][4] is False,
            "item route did not preserve real v26 sender arguments")
    in_flight = call(queryOnly="1")
    require(in_flight["state"] == "accepted" and
            in_flight["data"]["requestDispatched"] is False and
            state["rpcCount"] == 1,
            "duplicate item was sent before authoritative poststate")
    state["inputClear"] = False
    require(call(queryOnly="1")["state"] == "accepted" and
            state["rpcCount"] == 1,
            "read-only query wrongly required keyboard focus or resent item")
    require(inspect()["state"] == "confirmed" and state["rpcCount"] == 1,
            "read-only inventory snapshot incorrectly required keyboard focus")
    require(call()["state"] == "failed" and state["rpcCount"] == 1,
            "pending request with active text input bypassed send focus gate")
    state["inputClear"] = True
    reconnected = call(challenge="newverifiedchallenge")
    require(reconnected["state"] == "accepted" and
            reconnected["data"]["requestDispatched"] is False and
            state["rpcCount"] == 1,
            "session change resent unresolved consumable")
    state["endTime"] = 2800
    require(call(queryOnly="1")["state"] == "accepted",
            "march-time-only optimistic change falsely confirmed")
    state["count"] = 1
    confirm = call(queryOnly="1")
    require(confirm["state"] == "confirmed" and
            confirm["data"]["currentEndTime"] == 2800 and
            confirm["data"]["currentCount"] == 1,
            "both independent native observations required for confirmation")
    require(state["rpcCount"] == 1, "querying confirmation resent RPC")

    state, call, inspect = mock()
    cases = [
        ({"queryOnly": "1"}, "failed", "query without sent request"),
        ({"itemOptIn": "0"}, "failed", "missing spend opt-in"),
        ({"itemId": "9999"}, "failed", "template never owned"),
        ({"itemId": "666"}, "failed", "owned item has no rapid template"),
        ({"authorizedUid": "300"}, "failed", "wrong admitted account"),
        ({"profileId": "unsafe session"}, "failed", "forged bridge identity"),
        ({"slot": "5"}, "failed", "unknown formation"),
        ({"action": "attack"}, "unsupported", "cursor-selected target unresolved"),
        ({"action": "speedupDiamond"}, "unsupported", "diamond confirmation UI unresolved"),
    ]
    for fields, expected, label in cases:
        require(call(**fields)["state"] == expected, label)
        require(state["rpcCount"] == 0, f"unverified path sent request: {label}")
    state["inputClear"] = False
    require(call()["state"] == "failed" and state["rpcCount"] == 0,
            "editing Unity game text bypassed focus guard")
    state["inputClear"] = True
    state["formation"] = float(9007199254740994)
    require(call()["state"] == "unsupported" and state["rpcCount"] == 0,
            "unsafe or no-longer-canonical formation was accepted")
    state["formation"] = 1001
    state["owner"] = 444
    require(call()["state"] == "unsupported" and state["rpcCount"] == 0,
            "foreign march owner passed selection")
    state["owner"] = 222
    state["formOwner"] = 2020
    require(call()["state"] == "unsupported" and state["rpcCount"] == 0,
            "foreign formation passed selection")
    state["formOwner"] = 1001
    state["targetType"] = 94
    require(call()["state"] == "unsupported" and state["rpcCount"] == 0,
            "nonattack world march incorrectly admitted for attack speedup")
    state["targetType"] = 17
    state["stale"] = True
    require(call()["state"] == "failed" and state["rpcCount"] == 0,
            "stale mismatched item manager admitted")
    state["stale"] = False
    state["count"] = 0
    require(call()["state"] == "failed" and state["rpcCount"] == 0,
            "depleted item consumed")
    state["count"] = 2
    state["endTime"] = state["now"]
    require(call()["state"] == "failed" and state["rpcCount"] == 0,
            "already completed march speedup sent")
    state["endTime"] = 3000
    state["failRpc"] = True
    require(call()["state"] == "failed" and state["rpcCount"] == 1,
            "sender exception incorrectly accepted as dispatch success")

    print("FEATURE018_ATTACK_ACTIONS_SOURCE_AND_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
