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
 soldierCount=110, heroCount=1, createCounter=0,
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
   return {uuid=state.formation,ownerUid=state.formationOwner,
           soldiers={[101]=state.soldierCount},
           heroes=state.heroCount>0 and {[1]=777} or {}}
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
 if route==99 then
  assert(uuid==state.formation and itemid==17 and buy==990123)
  state.attackSends=(state.attackSends or 0)+1
  require('Net.Msgs.WorldMarchFormationNewMessage').OnCreate({},uuid,itemid,buy)
  return
 end
 assert(route==173 and uuid==state.uuid and ((itemid==3108 and buy==false) or (itemid==9999 and buy==true)))
 state.rpcCount=state.rpcCount+1
 state.rpcArgs={route,uuid,itemid,buy}
 if state.failRpc then error("simulated_rpc_reject") end
 if state.failRpcFalse then return false end
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
             "CheckFormation": ["GetOneArmyInfoByUuid", "heroes", "GetCanAddHeroNum"],
             "GetMaxCanAddSoldierNum": ["building_base", "APS_FORMATION_SIZE",
                                        "APS_FORMATION_SIZE_ENHANCE",
                                        "GetFormationMaxNumByFormationIndex"],
             "SendCreateMarchToServer": ["WorldMarchFormationNew", "SendMessage"],
             "SendChangeMarchToServer": ["WorldMarchChange", "SendMessage"],
             "TryStartMarch": ["GetCostStaminaByTargetType", "CheckFormation",
                               "SendCreateMarchMessage", "StartMarch"]}),
        "Util/UIUtil.luac": (
            "73c1da103424ae1f7156f9988ef3498925418890fec8b3e2837a2a885d1cbf3f",
            {"OpenFormationSelectUI": ["formation_v2_switch",
                                        "UIFormationSelectListV2",
                                        "UIFormationSelectListNew", "OpenWindow"]}),
        "DataCenter/MonsterManager/MonsterTemplateManager.luac": (
            "2b6d67baea00cf54e99df638e2f175cf4504ebc08e27a1e9150fbfbe5fa54f96",
            {"GetMonsterTemplate": [], "TryGetMonsterTemplate": []}),
        "DataCenter/ArmyFormationData/ArmyFormationDataManager.luac": (
            "0df480a74c338b9830f16b7b455ae0f1a77877a78896f0fedde381c28d7cbce7",
            {"GetOneArmyInfoByIndex": [],
             "GetArmyUnFormationList": ["ArmyManager", "GetArmyFreeList", "walk"],
             "AutoAddSoldierByForm": ["soldiers", "AutoAddSoldier"]}),
        "DataCenter/ArmyManager/ArmyManager.luac": (
            "e090fc4c93abcc7e22d97838171945332955165d45768e63587b5bf269835485",
            {"GetArmyFreeList": ["allArmy", "walk"]}),
        "UI/UIFormation/UIFormationSelectListV2/Controller/UIFormationSelectListV2Ctrl.luac": (
            "25db92ba9a39791ff8fb500e2b00034f894dc5cf1d98200454e582bb24daa84c",
            {"InitData": ["targetType", "targetPoint", "targetUuid"],
             "OnAtkClick": ["GetOwnerFormationMarch", "ChangeMarchByType"],
             "GetCurSoldierNum": ["GetOneArmyInfoByUuid", "soldiers", "walk"],
             "CheckCanBattle": ["GetOneArmyInfoByUuid", "GetCurStaminaByUuid"],
             "ChangeMarchByType": ["GetOwnerFormationMarch", "OnAttackArmy",
                                   "OnAttackOtherCity"]}),
        "UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac": (
            "7298256e4a5181a335cfd2ce8c89c9454452211b2feab68eac57827e0465af6d",
            {"InitData": ["curTouchPoint", "GetPointInfoByUuid",
                          "GetCurServerId", "pointId"]}),
        "UI/UIMainMapPointToSelect/View/UIMainMapPointToSelectView.luac": (
            "8178cafadb703205a6e8ac259337fe28ed4ff90e535370ac4a8d8eead30452a1",
            {"OnContentClick": ["touchCamera", "ScreenPointToRay",
                                "position", "GetIntersectionPoint",
                                "WorldPointSelectViewDataManager",
                                "ChangeSelectZone"]}),
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
            {"OnCreate": ["SaveCreateMarchRecordTime", "PutUtfString", "clientCreateUuid"],
             "GetTestData": ["clientCreateUuid", "ownerUid", "uuid"],
             "HandleMessage": ["errorCode", None, "SceneManager", "World",
                               "HandleFormationMarch", "FetchFormationSoldier"]}),
        "Net/Msgs/WorldMarchChangeMessage.luac": (
            "71453f2d7287ddb1f012e05cea71755f6bc704971a59425b6c54834c96fe8160",
            {"OnCreate": ["uuid", "target", "targetUid", "PutLong"],
             "HandleMessage": ["errorCode", None, "HandleFormationMarchChange"]}),
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
            if name.endswith("WorldMarchFormationNewMessage.luac") and name_ == "OnCreate":
                instructions=disassemble(proto)
                saved=next(i for i,x in enumerate(instructions)
                           if "sourceLine=46 " in x and "'SaveCreateMarchRecordTime'" in x)
                written=next(i for i,x in enumerate(instructions)
                             if "sourceLine=47 " in x and "'clientCreateUuid'" in x)
                require(saved<written, "original source create nonce generator/serializer drifted")
            if name.endswith("ArmyFormationDataManager.luac") and name_ == "AutoAddSoldierByForm":
                instr=disassemble(proto)
                require(any("sourceLine=920 " in x and "'soldiers'" in x for x in instr) and
                        any("sourceLine=956 " in x and "'AutoAddSoldier'" in x for x in instr),
                        "native formation auto-assignment stopped mutating the formation")
            if name.endswith("UIWorldPointCtrl.luac") and name_ == "InitData":
                require("ScreenPointToRay" not in constants_seen and
                        "mousePosition" not in constants_seen,
                        "original current main-world point unexpectedly uses fresh cursor raycast")
            if name.endswith("MarchUtil.luac") and name_ in (
                    "SendCreateMarchToServer", "SendChangeMarchToServer"):
                route = ("WorldMarchFormationNew" if name_ == "SendCreateMarchToServer"
                         else "WorldMarchChange")
                line = ("sourceLine=2012 " if name_ == "SendCreateMarchToServer"
                        else "sourceLine=2064 ")
                instructions = disassemble(proto)
                require(any(line in x and f"'{route}'" in x for x in instructions) and
                        any(line in x and "'SendMessage'" in x for x in instructions),
                        f"native {name_} does not dispatch the source-backed route")


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
        SFSNetwork.SendMessage(99,formation,17,990123,'current-path')
      end
      nativeCtrl.OnAtkClick=nativeCtrl.OnCreateClick
      nativeCtrl.OnCheckTime=nativeCtrl.OnCreateClick
      MsgDefines.WorldMarchFormationNew=99
      package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()
        return {OnCreate=function(self,formation,kind,target)
          state.createCounter=state.createCounter+1
          local token='nativeCreate-'..state.createCounter
          self.sfsObj={GetUtfString=function(obj,key)
            assert(key=='clientCreateUuid');return token
          end}
        end,HandleMessage=function(self,response) end}
      end
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
    require(first['state']=='accepted' and state['attackDelegateCount']==1 and
            first['data']['nativeTransaction']['sendCount']==1 and
            first['data']['nativeTransaction']['nonceStatus']=='awaiting_same_nonce_reply' and
            state['attackSends']==1,
            'native selector did not dispatch fixed own squad exactly once')
    require(call(queryOnly='1')['state']=='accepted' and state['attackDelegateCount']==1,
            'pending attack was resent or falsely confirmed without native march')
    state['existingAbsent']=False
    state['targetUuid']=990124
    require(call(queryOnly='1')['state']=='accepted','different target march falsely confirmed')
    state['targetUuid']=990123
    require(call(queryOnly='1')['state']=='accepted',
            'local-only native march was misreported as a server ACK')
    vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
        {}, {clientCreateUuid='nativeCreate-1',ownerUid=state.uid,uuid=state.uuid})""")
    ack=call(queryOnly='1')
    require(ack['state']=='confirmed' and ack['data']['serverAcknowledgementCorrelated'] is True and
            ack['data']['nativeMarchPostStateVerified'] is True and
            ack['data']['transaction']['nonceStatus']==
                'same_nonce_owner_march_reply_observed_requires_fresh_poststate' and
            ack['data']['targetUuid']=='990123' and
            ack['data']['currentCursorVerified'] is False and state['attackDelegateCount']==1,
            'exact authoritative owned squad/target transition not recognized')
    require(call()['state']=='unsupported' and state['attackSends']==1,
            'an existing active march was retargeted without a correlatable native change ACK')
    for opts in ({}, {'diamondOptIn':'1'}, {'diamondOptIn':'1','userConfirmed':'1'},
                 {'diamondOptIn':'1','queryOnly':'1'}):
        require(call(action='speedupDiamond',itemId='9999',**opts)['state']=='unsupported' and
                state['rpcCount']==0,
                'direct diamond purchase bypassed original native confirmation callback')
    state['existingAbsent']=True
    state['soldierCount']=0
    require(call()['state']=='failed' and state['attackSends']==1,
            'unassigned original squad soldiers dispatched an attack')
    state['soldierCount']=110
    state['heroCount']=0
    require(call()['state']=='failed' and state['attackSends']==1,
            'unassigned original squad heroes dispatched an attack')
    state['heroCount']=1
    require(call(allowRapidDiamond='1')['state']=='unsupported' and
            state['attackSends']==1 and state['rpcCount']==0,
            'attack-linked automatic diamond buy bypassed native modal confirmation')
    require(call(allowRapidItem='1')['state']=='accepted',
            'opt-in rapid attack failed to start native selection')
    state['existingAbsent']=False
    state['startTime']=1100
    vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
        {}, {clientCreateUuid='nativeCreate-2',ownerUid=state.uid,uuid=state.uuid})""")
    chain=call(queryOnly='1')
    require(chain['state']=='accepted' and state['rpcCount']==1 and
            state['rpcArgs'][4] is False,
            'confirmed attack did not apply exactly one opted-in owned rapid item')
    require(call(queryOnly='1')['state']=='accepted' and state['rpcCount']==1,
            'rapid followup resubmitted an item or accepted absent inventory/time change')
    state['count']=1
    state['endTime']=2800
    finished=call(queryOnly='1')
    require(finished['state']=='confirmed' and finished['data']['speedup']['currentCount']==1 and
            state['rpcCount']==1,
            'composed attack rapid did not require independent owned-item and march-time poststate')
    state['serverId']=2212
    require(call()['state']=='failed' and state['attackDelegateCount']==2,
            'main-account server entered mutable attack controller')


def test_attack_sender_correlation_negatives():
    # A controller call alone is not a network receipt. The source-backed
    # formation/march route must match slot, native kind and target UUID,
    # and a matching owned march transition must follow independently.
    for mode, immediate, later, sends in (
            ('no_send', 'accepted', 'accepted', 0),
            ('wrong_target', 'accepted', 'accepted', 1),
            ('double', 'failed', 'failed', 2),
            ('sender_false', 'failed', 'failed', 1),
            ('sender_throw', 'failed', 'failed', 1),
            ('reply_error', 'accepted', 'failed', 1),
            ('nonce_missing', 'accepted', 'accepted', 1),
            ('reply_wrong_nonce', 'accepted', 'accepted', 1),
            ('reply_wrong_owner', 'accepted', 'accepted', 1),
            ('reply_wrong_march', 'accepted', 'accepted', 1),
            ('reply_duplicate', 'accepted', 'accepted', 1),
            # A negative controller return can still follow a real send. A
            # subsequent independent matching native march can settle it.
            ('controller_false', 'failed', 'confirmed', 1)):
        vm=LuaRuntime(unpack_returned_tuples=True)
        vm.execute(GAME)
        vm.execute("""
          state.existingAbsent=true; state.startTime=1000; state.targetUuid=990123
          state.sendMode='""" + mode + """'
          package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()
            return {OnCreate=function(self,formation,kind,target)
              self.sfsObj=state.sendMode=='nonce_missing' and {} or
                {GetUtfString=function(self,key) return 'mock-create-nonce' end}
            end,HandleMessage=function(self,response) end}
          end
          package.preload['Net.Msgs.WorldMarchChangeMessage']=function()
            return {HandleMessage=function(self,response) end}
          end
          MsgDefines.WorldMarchFormationNew=99
          UIWindowNames={UIFormationSelectListV2='formation'}
          nativeCtrl={targetType=17,targetUuid=990123,targetPoint=state.touchedTile}
          SFSNetwork.SendMessage=function(route,form,kind,target,path)
            assert(route==99 and form==state.formation and kind==17)
            state.attackSends=(state.attackSends or 0)+1
            require('Net.Msgs.WorldMarchFormationNewMessage').OnCreate({},form,kind,target)
            if state.sendMode=='sender_false' then return false end
            if state.sendMode=='sender_throw' then error('simulated native sender failure') end
          end
          nativeCtrl.OnCheckTime=function(self,formation)
            state.attackDelegateCount=state.attackDelegateCount+1
            if state.sendMode=='no_send' then return end
            local target=state.sendMode=='wrong_target' and 990124 or 990123
            SFSNetwork.SendMessage(99,formation,17,target,'path')
            if state.sendMode=='double' then
              SFSNetwork.SendMessage(99,formation,17,target,'path')
            end
            if state.sendMode=='controller_false' then return false end
          end
          nativeWindow={Ctrl=nativeCtrl}
          UIManager={GetInstance=function() return {
            IsWindowOpen=function(self,name) return name=='formation' end,
            GetWindow=function(self,name) return name=='formation' and nativeWindow end
          } end}
        """)
        module=vm.execute(SOURCE.read_text(encoding='utf-8'))
        def call(**extra):
            fields=dict(action='attack',slot='1',authorizedUid='3133546747002289',
                        profileId='profile',launchSessionId='launch',
                        challenge='request',gamePid='7020')
            fields.update(extra)
            return module.Handle(vm.table_from(fields),vm.globals().input_clear)
        dispatched=call()
        require(dispatched['state']==immediate and
                vm.globals().state.attackDelegateCount==1 and
                (vm.globals().state.attackSends or 0)==sends,
                'unexpected native attack dispatch outcome: '+mode)
        if mode in ('double','sender_false','sender_throw'):
            require(dispatched['data']['nativeTransaction']['sendCount']>=1,
                    'failed outbound request lost no-retry transaction fence: '+mode)
        if mode=='reply_error':
            vm.execute("require('Net.Msgs.WorldMarchChangeMessage').HandleMessage({}, {errorCode='unrelated_recall'})")
            unrelated=call(queryOnly='1')
            require(unrelated['state']=='accepted' and
                    unrelated['data']['nativeTransaction']['replyObserved'] is None,
                    'unrelated recall/change ACK was attributed to a create attack')
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='mock-create-nonce',ownerUid=state.uid,
                   uuid=state.uuid,errorCode='native_reject'})""")
        if mode=='controller_false':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='mock-create-nonce',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='nonce_missing':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='mock-create-nonce',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='reply_wrong_nonce':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='unrelated-create-id',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='reply_wrong_owner':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='mock-create-nonce',ownerUid=123,uuid=state.uuid})""")
        if mode=='reply_wrong_march':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='mock-create-nonce',ownerUid=state.uid,uuid=123})""")
        if mode=='reply_duplicate':
            vm.execute("""for i=1,2 do
              require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
                {}, {clientCreateUuid='mock-create-nonce',ownerUid=state.uid,uuid=state.uuid})
              end""")
        vm.globals().state.existingAbsent=False
        polled=call(queryOnly='1')
        require(polled['state']==later and
                vm.globals().state.attackDelegateCount==1 and
                (vm.globals().state.attackSends or 0)==sends,
                'unproven native attack was confirmed or resent: '+mode)


def test_native_prepare_helper():
    vm=LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    vm.execute("""
      MarchTargetType.ATTACK_MONSTER=19
      DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
        if slot~=state.slot then return nil end
        return {uuid=state.formation,ownerUid=state.uid,index=slot,
          heroes=state.heroCount>0 and {[1]=777} or {},
          soldiers={[101]=state.soldierCount},
          GetLocalHeroesCount=function()return state.localHeroCount end}
      end
      DataCenter.ArmyManager={GetArmyFreeList=function()
        return {[101]=state.freeTroops or 70}
      end}
      DataCenter.ArmyFormationDataManager.GetArmyUnFormationList=function()
        if state.badFreeType then return {[-1]=5} end
        return {[101]=state.freeTroops or 70}
      end
      MarchUtil.GetMaxCanAddSoldierNum=function(heroes,index)
        assert(index==1 and type(heroes)=='table')
        if state.badCapacity then return nil end
        return state.capacity or 150
      end
      DataCenter.ArmyFormationDataManager.AutoAddSoldierByForm=function()
        error('read-only inventory must not mutate formation')
      end
      DataCenter.WorldMarchDataManager.GetAllMarches=function() return {
        {uuid=990123,monsterId=100,ownerUid=999},
        {uuid=state.uuid,ownerUid=state.uid,ownerFormationUuid=state.formation,target=17}}
      end
      DataCenter.MonsterTemplateManager={GetMonsterTemplate=function(self,id)
        assert(id==100);return {level=1,special=0} end}
      CS.SceneManager.World.GetMarch=function(self,id)
        if id==990123 then return {uuid=id,monsterId=100,ownerUid=999} end end
      CS.SceneManager.World.GetTroop=function(self,id)
        if id==990123 then return {GetPosition=function()return CS.SceneManager.World.curTouchPoint end} end end
      UIUtil={OpenFormationSelectUI=function(...)
        local a=table.pack(...)
        assert(a.n==9 and a[1]==1 and a[2]==19 and a[3]==35723 and a[4]==990123)
        assert(a[5]==nil and a[6]==nil and a[7]==nil and a[8]==2289 and a[9]==0)
        state.prepared=true
      end}
    """)
    module=vm.execute(SOURCE.read_text(encoding='utf-8'))
    fields=dict(mode='inventory',authorizedUid='3133546747002289',profileId='profile',
                launchSessionId='launch',challenge='request',gamePid='7020')
    observed=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(observed['state']=='confirmed' and observed['data']['candidates'][1]['uuid']=='990123' and
            observed['data']['ownedMarches']['555110']['formationUuid']=='1001' and
            observed['data']['squads']['1']['soldierCount']==110 and
            observed['data']['squads']['1']['staffedForAttack'] is True and
            observed['data']['unassignedSoldiers']['observed'] is True and
            observed['data']['unassignedSoldiers']['total']==70 and
            observed['data']['squads']['1']['formationCapacity']==150 and
            observed['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is True and
            observed['data']['staffingPreparationPerformed'] is False,
            'native prepare lost current ordinary-monster or independently owned march identity')
    vm.globals().state.soldierCount=0
    no_soldiers=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(no_soldiers['state']=='confirmed' and
            no_soldiers['data']['squads']['1']['soldierCount']==0 and
            no_soldiers['data']['squads']['1']['staffedForAttack'] is False and
            no_soldiers['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is True,
            'missing soldier staffing was advertised as dispatch-ready')
    vm.globals().state.badFreeType=True
    bad_free=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(bad_free['state']=='confirmed' and
            bad_free['data']['unassignedSoldiers']['observed'] is False and
            bad_free['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is False,
            'malformed native free soldier record reported as actionable')
    vm.globals().state.badFreeType=False
    vm.globals().state.badCapacity=True
    no_capacity=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(no_capacity['state']=='confirmed' and
            no_capacity['data']['squads']['1']['formationCapacity'] is None and
            no_capacity['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is False,
            'unknown native formation capacity advertised a viable staffing path')
    vm.globals().state.badCapacity=False
    vm.globals().state.freeTroops=0
    no_free=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(no_free['state']=='confirmed' and
            no_free['data']['unassignedSoldiers']['total']==0 and
            no_free['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is False,
            'depleted native free soldier inventory advertised a viable staffing path')
    vm.globals().state.freeTroops=None
    vm.globals().state.capacity=0
    no_headroom=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(no_headroom['state']=='confirmed' and
            no_headroom['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is False,
            'zero native formation capacity advertised a viable staffing path')
    vm.globals().state.capacity=None
    vm.globals().state.heroCount=0
    no_hero=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(no_hero['state']=='confirmed' and
            no_hero['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is False,
            'no assigned heroes advertised a viable staffing path')
    vm.globals().state.heroCount=1
    vm.globals().state.localHeroCount=0
    local_heroes_absent=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(local_heroes_absent['state']=='confirmed' and
            local_heroes_absent['data']['squads']['1']['staffedForAttack'] is False and
            local_heroes_absent['data']['squads']['1']['canPrepareWithUnassignedSoldiers'] is False,
            'native local-zero heroes bypassed game formation staffing gate')
    vm.globals().state.localHeroCount=None
    vm.globals().state.soldierCount=110
    fields.update(mode='open',targetUuid='990123')
    opened=module.Prepare(vm.table_from(fields),vm.globals().input_clear)
    require(opened['state']=='confirmed' and vm.globals().state.prepared is True and
            vm.globals().state.rpcCount==0,
            'native configured formation helper was not used or prepare sent gameplay RPC')


def main():
    check_original()
    test_selected_attack_and_diamond()
    test_attack_sender_correlation_negatives()
    test_native_prepare_helper()
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
    require(diamond["state"] == "unsupported" and state["rpcCount"] == 0,
            "diamond rapid requires source-native modal confirmation callback")
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

    for failure in ('failRpc','failRpcFalse'):
        state,call,inspect=mock()
        state[failure]=True
        uncertain=call()
        require(uncertain['state']=='failed' and state['rpcCount']==1,
                'native sender failure not reported after one attempted spend: '+failure)
        state[failure]=False
        blocked=call()
        require(blocked['state']=='accepted' and state['rpcCount']==1 and
                blocked['data']['requestDispatched'] is False,
                'native sender failure permitted second item spend: '+failure)
        state['endTime']=2700
        state['count']=1
        receipt=call(queryOnly='1')
        require(receipt['state']=='confirmed' and state['rpcCount']==1,
                'failed/uncertain native sender lost eventual independent poststate: '+failure)

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
            valid["data"]["safeToDispatch"] is False and
            valid["data"]["formationStaffing"]["staffedForAttack"] is True,
            "read-only native clicked-world point provenance missing")
    require(prestate["freshLookups"] == 1,
            "existing march must be independently re-fetched once")
    vm.globals().Input=vm.table_from({"mousePosition": vm.table_from({"x":33,"y":45})})
    pointer_spoof=preflight()
    require(pointer_spoof['state']=='confirmed' and
            pointer_spoof['data']['currentCursorVerified'] is False and
            pointer_spoof['data']['freshTouchVerified'] is False and
            pointer_spoof['data']['safeToDispatch'] is False and
            pointer_spoof['data']['targetSource']=='SceneManager.World.curTouchPoint_last_touch' and
            prestate['attackDelegateCount']==0,
            'unverified physical pointer spoofed original game current-world touch')
    require(prestate["freshLookups"] == 2,
            "cursor spoof read-only preflight must revalidate the existing march independently")
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
