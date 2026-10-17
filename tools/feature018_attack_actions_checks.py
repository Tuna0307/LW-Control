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
 uid=3133546747002289, expectedUid="3133546747002289", formation=1001, owner=3133546747002289, formOwner=1001,
 freshOwner=3133546747002289, freshFormOwner=1001, freshUuid=555110, freshPresent=true,
 existingAbsent=false, freshLookups=0,
 targetType=17, serverId=2289, formationOwner=3133546747002289, touchedTile=35723,
 formationMarchBroken=false,
 touchedX=12, touchedZ=37, worldUuid=990123, worldOwner=999,
 worldServer=2289, worldMain=35723, reverseValid=true,
 reverseMain=35723, worldPresent=true,
 uuid=555110, slot=1, endTime=3000, now=2000, count=2,
 inputClear=true, itemOptIn="1", rpcCount=0, rpcArgs={}, failRpc=false,
 attackDelegateCount=0,
 category=108, stale=false, ownsItem=true,
}
LuaEntry={Player={uid=state.uid,allianceId=700}}
LuaEntry.Player.GetCurServerId=function(self) return state.serverId end
LuaEntry.Player.GetSourceServerId=LuaEntry.Player.GetCurServerId
LuaEntry.Player.GetSelfServerId=LuaEntry.Player.GetCurServerId
MarchTargetType={ATTACK_CITY=17,ATTACK_ARMY=18}
CS={SceneManager={World={
 curTouchPoint={x=state.touchedX,y=0,z=state.touchedZ},
 GetPointInfo=function(self,pointId)
   if not state.worldPresent or pointId~=state.touchedTile then return nil end
   return {uuid=state.worldUuid,mainIndex=state.worldMain,
           ownerUid=state.worldOwner,serverId=state.worldServer}
 end,
 GetPointInfoByUuid=function(self,uuid)
   if uuid~=state.worldUuid then return nil end
   if not state.reverseValid then return {uuid=17} end
   return {uuid=state.worldUuid, mainIndex=state.reverseMain,
           serverId=state.worldServer, ownerUid=state.worldOwner}
 end
}}}
ForceChangeScene={World=3}
SceneUtils={WorldToTileIndex=function(worldPos, scene)
 assert(scene==3)
 if worldPos.x~=state.touchedX or worldPos.z~=state.touchedZ then return nil end
 return state.touchedTile
end}
UITimeManager={GetInstance=function(self) return {
 GetServerTime=function(self) return state.now end
} end}
GOODS_TYPE={GOODS_TYPE_108=108}
DataCenter={
 ArmyFormationDataManager={
  GetOneArmyInfoByIndex=function(self,s)
   if s~=state.slot then return nil end
   return {uuid=state.formation,ownerUid=state.formationOwner}
  end
 },
 WorldMarchDataManager={
  GetOwnerFormationMarch=function(self,uid,form,extra)
   assert(extra==700)
   assert(type(uid)==type(LuaEntry.Player.uid), "native lookup must preserve original Player.uid type")
   if state.existingAbsent then return nil end
   if tostring(uid)~=tostring(state.uid) or form~=state.formation then return nil end
   return {uuid=state.uuid,ownerUid=state.owner,
           ownerFormationUuid=state.formOwner,
           GetIsBroken=function(self) return state.formationMarchBroken end}
  end,
  GetMarch=function(self,uuid)
   state.freshLookups=state.freshLookups+1
   if uuid~=state.uuid or not state.freshPresent then return nil end
   return {uuid=state.freshUuid,ownerUid=state.freshOwner,
           ownerFormationUuid=state.freshFormOwner,
           endTime=state.endTime,startTime=state.startTime,targetUuid=state.targetUuid,
           GetWorldType=function() return state.worldType end,
           GetIsBroken=function(self) return state.formationMarchBroken end,
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
 assert(route==173 and uuid==state.uuid and ((itemid==3108 and buy==false) or (itemid==9999 and buy==true)))
 state.rpcCount=state.rpcCount+1
 state.rpcArgs={route,uuid,itemid,buy}
 if state.failRpc then error("simulated_rpc_reject") end
end}
MarchUtil={
 TryStartMarch=function(...) state.attackDelegateCount=state.attackDelegateCount+1 end,
 StartMarch=function(...) state.attackDelegateCount=state.attackDelegateCount+1 end,
 OnAttackOtherCity=function(...) state.attackDelegateCount=state.attackDelegateCount+1 end,
 OnAttackArmy=function(...) state.attackDelegateCount=state.attackDelegateCount+1 end,
}
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
            "itemOptIn": "1", "authorizedUid": "3133546747002289",
            "profileId": "profile", "launchSessionId": "launch",
            "challenge": "request", "gamePid": "7020",
        }
        fields.update(overrides)
        return module.Handle(vm.table_from(fields), vm.globals().input_clear)

    def inspect(**overrides):
        fields = {
            "slot": "1", "authorizedUid": "3133546747002289",
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
             "OnAttackArmy": ["GetTroop", "TryStartMarch"],
             "TryStartMarch": ["GetCostStaminaByTargetType", "CheckFormation",
                               "SendCreateMarchMessage", "StartMarch"]}),
        "UI/UIFormation/UIFormationSelectListV2/Controller/UIFormationSelectListV2Ctrl.luac": (
            "25db92ba9a39791ff8fb500e2b00034f894dc5cf1d98200454e582bb24daa84c",
            {"InitData": ["targetType", "targetPoint", "targetUuid"],
             "OnAtkClick": ["GetOwnerFormationMarch", "ChangeMarchByType"],
             "ChangeMarchByType": ["GetOwnerFormationMarch", "OnAttackArmy",
                                   "OnAttackOtherCity"]}),
        "UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac": (
            "7298256e4a5181a335cfd2ce8c89c9454452211b2feab68eac57827e0465af6d",
            {"InitData": ["curTouchPoint", "GetPointInfoByUuid",
                          "GetCurServerId", "pointId"]}),
        "UI/UIWorldPoint/View/UIWorldPointView.luac": (
            "e49f0a80b37ae2ecc3abf808c4fa2d2ed5eb146f4224cdf1cf348a1f461636ff",
            {"ReInit": ["GetPointData", "pointId", "ctrl"]}),
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
        "Net/Msgs/WorldMarchFormationNewMessage.luac": (
            "69dec0f3e14286770c728a2b42d8542f8f3b9e0f493bc2bcb75c9cf4f69a8dd9",
            {"HandleMessage": ["errorCode", None, "SceneManager", "World",
                               "HandleFormationMarch", "FetchFormationSoldier"]}),
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
            if name.endswith("WorldMarchFormationNewMessage.luac") and name_ == "HandleMessage":
                # A successful native response goes to World.HandleFormationMarch,
                # but that callback does not, by itself, prove an exact
                # cursor-target, formation/owner or resulting march poststate.
                instructions = disassemble(proto)
                source_guard = next(i for i, line in enumerate(instructions)
                                    if "sourceLine=94 " in line and " EQ " in line)
                source_apply = next(i for i, line in enumerate(instructions)
                                    if "HandleFormationMarch" in line)
                require(source_guard < source_apply and
                        "sourceLine=126 " in instructions[source_apply],
                        "native march response success guard/apply route changed")


def test_selected_attack_and_diamond():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    vm.execute("""
      state.existingAbsent=true; state.startTime=1000; state.targetUuid=990123
      state.worldType=0; LuaEntry.Player.gold=100
      UIWindowNames={UIFormationSelectListV2='formation'}
      nativeCtrl={targetType=17,targetUuid=990123,targetPoint=state.touchedTile}
      nativeCtrl.OnCreateClick=function(self,formation)
        assert(formation==state.formation)
        state.attackDelegateCount=state.attackDelegateCount+1
      end
      nativeCtrl.OnAtkClick=nativeCtrl.OnCreateClick
      nativeCtrl.OnCheckTime=nativeCtrl.OnCreateClick
      nativeWindow={Ctrl=nativeCtrl}
      UIManager={GetInstance=function() return {
        IsWindowOpen=function() return true end,
        GetWindow=function() return nativeWindow end
      } end}
    """)
    module = vm.execute(SOURCE.read_text(encoding='utf-8'))
    state = vm.globals().state
    def call(action='attack', **extra):
        fields=dict(action=action,slot='1',authorizedUid='3133546747002289',
                    profileId='profile',launchSessionId='launch',challenge='request',gamePid='7020')
        fields.update(extra)
        return module.Handle(vm.table_from(fields),vm.globals().input_clear)
    first=call()
    require(first['state']=='accepted' and state['attackDelegateCount']==1,
            'native selector did not dispatch fixed own squad exactly once')
    require(call(queryOnly='1')['state']=='accepted' and state['attackDelegateCount']==1,
            'pending attack was resent or falsely confirmed without native march')
    state['existingAbsent']=False
    state['targetUuid']=990124
    require(call(queryOnly='1')['state']=='accepted','different target march falsely confirmed')
    state['targetUuid']=990123
    ack=call(queryOnly='1')
    require(ack['state']=='confirmed' and ack['data']['targetUuid']=='990123' and
            ack['data']['currentCursorVerified'] is False and state['attackDelegateCount']==1,
            'exact authoritative owned squad/target transition not recognized')
    require(call(action='speedupDiamond',itemId='9999')['state']=='failed' and state['rpcCount']==0,
            'diamond purchase bypassed explicit app consent')
    sent=call(action='speedupDiamond',itemId='9999',diamondOptIn='1')
    require(sent['state']=='accepted' and state['rpcArgs'][4] is True and state['rpcCount']==1,
            'native gem rapid send arguments or one-shot result wrong')
    state['endTime']=2900
    require(call(action='speedupDiamond',queryOnly='1')['state']=='accepted',
            'time-only gem rapid change falsely confirmed')
    vm.globals().LuaEntry.Player.gold=91
    require(call(action='speedupDiamond',queryOnly='1')['state']=='accepted',
            'unrelated gem debit falsely matched the exact native price')
    vm.globals().LuaEntry.Player.gold=90
    ack=call(action='speedupDiamond',queryOnly='1')
    require(ack['state']=='confirmed' and ack['data']['price']==10 and state['rpcCount']==1,
            'source-native gem debit plus earlier march arrival did not confirm')
    state['existingAbsent']=True
    require(call(allowRapidItem='1')['state']=='accepted',
            'opt-in rapid attack failed to start native selection')
    state['existingAbsent']=False
    state['startTime']=1100
    chain=call(queryOnly='1')
    require(chain['state']=='accepted' and state['rpcCount']==2 and
            state['rpcArgs'][4] is False,
            'confirmed attack did not apply exactly one opted-in owned rapid item')
    require(call(queryOnly='1')['state']=='accepted' and state['rpcCount']==2,
            'rapid followup resubmitted an item or accepted absent inventory/time change')
    state['count']=1
    state['endTime']=2800
    finished=call(queryOnly='1')
    require(finished['state']=='confirmed' and finished['data']['speedup']['currentCount']==1 and
            state['rpcCount']==2,
            'composed attack rapid did not require independent owned-item and march-time poststate')
    state['serverId']=2212
    require(call()['state']=='failed' and state['attackDelegateCount']==2,
            'main-account server entered mutable attack controller')


def main():
    check_original()
    test_selected_attack_and_diamond()
    state, call, inspect = mock()
    inv = inspect()
    require(inv["state"] == "confirmed" and
            inv["data"]["observation"] == "native_owned_rapid_item_candidates" and
            len(inv["data"]["items"]) == 1 and
            inv["data"]["items"][1]["itemId"] == "3108",
            "native inventory must exclude absent templates and unowned items")
    require(state["rpcCount"] == 0, "inventory introspection dispatched an RPC")
    preflight = call(action="attack")
    require(preflight["state"] == "failed" and state["rpcCount"] == 0,
            "attack without native selector must fail without dispatch")
    require(call(action="attack", targetUuid="990123", targetType="ATTACK_CITY")["state"] == "failed",
            "caller target fields may not substitute a native selector")
    readonly_target = inspect()
    require(readonly_target["state"] == "confirmed",
            "owned speedup inventory regressed after target preflight")
    diamond = call(action="speedupDiamond", diamondOptIn="1",
                   userConfirmed="1", itemId="9999", price="10")
    require(diamond["state"] == "failed" and state["rpcCount"] == 0,
            "diamond rapid requires source-native world type and real balance")
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
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    preflight_module = vm.execute(SOURCE.read_text(encoding="utf-8"))
    prestate = vm.globals().state
    def preflight(**overrides):
        fields = {"slot": "1", "profileId": "profile", "launchSessionId": "launch",
                  "challenge": "request", "gamePid": "7020", "authorizedUid": "3133546747002289"}
        fields.update(overrides)
        return preflight_module.AttackTargetPreflight(
            vm.table_from(fields), vm.globals().input_clear)
    valid = preflight()
    require(valid["state"] == "confirmed" and
            valid["data"]["worldObjectReverseLookupVerified"] is True and
            valid["data"]["worldPointUuid"] == "990123" and
            valid["data"]["existingOwnedMarchUuid"] == "555110" and
            valid["data"]["existingOwnedMarchFreshlyVerified"] is True and
            valid["data"]["existingOwnedMarchBroken"] is False and
            valid["data"]["safeToDispatch"] is False,
            "read-only native clicked-world point provenance missing")
    require(prestate["freshLookups"] == 1,
            "existing march must be independently re-fetched once")
    prestate["existingAbsent"] = True
    lookups_before = prestate["freshLookups"]
    idle = preflight()
    require(idle["state"] == "confirmed" and
            idle["data"]["existingOwnedMarchUuid"] is None and
            idle["data"]["existingOwnedMarchFreshlyVerified"] is False and
            idle["data"]["safeToDispatch"] is False and
            prestate["freshLookups"] == lookups_before,
            "idle formation must allow read-only diagnosis without pretending a march exists")
    prestate["existingAbsent"] = False
    prestate["inputClear"] = False
    require(preflight()["state"] == "confirmed", "read-only attack snapshot requires keyboard focus")
    prestate["inputClear"] = True
    for kwargs in ({"authorizedUid": "999"}, {"slot": "5"},
                   {"pointIndex": "99999"}):
        require(preflight(**kwargs)["state"] == "failed",
                "unsafe attack preflight request accepted")
    require(preflight(pointIndex="35723")["state"] == "confirmed",
            "matching game-derived last touch was rejected")
    for key, wrong, expected in (
        ("formationOwner", 777, "failed"),
        ("owner", 777, "failed"),
        ("formOwner", 777, "failed"),
        ("worldOwner", 3133546747002289, "failed"),
        ("worldServer", 777, "failed"),
        ("reverseValid", False, "unsupported"),
        ("reverseMain", 44000, "unsupported"),
        ("worldPresent", False, "unsupported"),
    ):
        original_value = prestate[key]
        prestate[key] = wrong
        require(preflight()["state"] == expected,
                "native attack last-touch preflight accepted corrupted " + key)
        prestate[key] = original_value
    for key, wrong in (("freshOwner", 777), ("freshFormOwner", 778),
                       ("freshUuid", 900011), ("freshPresent", False)):
        original_value = prestate[key]
        prestate[key] = wrong
        rejected = preflight()
        require(rejected["state"] == "unsupported" and
                prestate["rpcCount"] == 0 and
                prestate["attackDelegateCount"] == 0,
                "stale independent march lookup was trusted: " + key)
        prestate[key] = original_value
    prestate["formationMarchBroken"] = True
    require(preflight()["state"] == "confirmed" and
            preflight()["data"]["existingOwnedMarchBroken"] is True and
            preflight()["data"]["safeToDispatch"] is False,
            "broken formation march misreported as ready to launch")
    prestate["formationMarchBroken"] = False
    require(prestate["rpcCount"] == 0 and prestate["attackDelegateCount"] == 0,
            "read-only target preflight emitted any native attack or RPC")

    state, call, inspect = mock()
    cases = [
        ({"queryOnly": "1"}, "failed", "query without sent request"),
        ({"itemOptIn": "0"}, "failed", "missing spend opt-in"),
        ({"itemId": "9999"}, "failed", "template never owned"),
        ({"itemId": "666"}, "failed", "owned item has no rapid template"),
        ({"authorizedUid": "300"}, "failed", "wrong admitted account"),
        ({"profileId": "unsafe session"}, "failed", "forged bridge identity"),
        ({"slot": "5"}, "failed", "unknown formation"),
        ({"action": "attack"}, "failed", "cursor-selected target unresolved"),
        ({"action": "speedupDiamond"}, "failed", "diamond confirmation UI unresolved"),
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
    state["owner"] = 3133546747002289
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
