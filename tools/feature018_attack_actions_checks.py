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
sys.path.insert(0, str(TOOLS / "map_014_il_decode"))
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(TOOLS.parent / "artifacts/map-013-lua-site"))

import compact_lua53 as lua53  # noqa: E402
from inspect_current import root_methods, disassemble  # noqa: E402
from lupa.lua53 import LuaRuntime  # noqa: E402
import feature018_mini_lua as original  # noqa: E402
from rgmd_il import MetadataImage  # noqa: E402
from rgmd_disasm import Resolver, disasm as managed_disasm  # noqa: E402


GAME = """
state={
 uid=1183884595002296, expectedUid="1183884595002296", formation=1001, owner=1183884595002296, formOwner=1001,
 freshOwner=1183884595002296, freshFormOwner=1001, freshUuid=555110, freshPresent=true,
 existingAbsent=false, freshLookups=0,
 targetType=17, serverId=2296, formationOwner=1183884595002296, touchedTile=35723,
 formationMarchBroken=false,
 touchedX=12, touchedZ=37, worldUuid=990123, worldOwner=999,
 worldServer=2296, worldMain=35723, reverseValid=true, worldAlliance=701,
 reverseMain=35723, worldPresent=true,
 uuid=555110, slot=1, endTime=3000, now=2000, count=2,
 soldierCount=110, heroCount=1, createCounter=0,
 inputClear=true, itemOptIn="1", rpcCount=0, rpcArgs={}, failRpc=false,
 attackDelegateCount=0,
 category=108, stale=false, ownsItem=true,
}
LuaEntry={Player={uid=state.uid,allianceId=700}}
LuaEntry.Player.IsInAlliance=function(self)
 if state.forceNoAlliance then return false end
 return self.allianceId~=0
end
LuaEntry.Player.GetAllianceUid=function(self) return self.allianceId end
LuaEntry.Player.GetCurServerId=function(self) return state.serverId end
LuaEntry.Player.GetSourceServerId=LuaEntry.Player.GetCurServerId
LuaEntry.Player.GetSelfServerId=LuaEntry.Player.GetCurServerId
MarchTargetType={ATTACK_CITY=17,ATTACK_ARMY=18}
CS={SceneManager={World={
 curTouchPoint={x=state.touchedX,y=0,z=state.touchedZ},
 GetPointInfo=function(self,pointId)
   if not state.worldPresent or pointId~=state.touchedTile then return nil end
   return {uuid=state.worldUuid,mainIndex=state.worldMain,
           pointIndex=pointId,isMainPoint=true,PointType=state.worldPointType or 4,
           ownerUid=state.worldOwner,serverId=state.worldServer,allianceId=state.worldAlliance}
 end,
 GetPointInfoByUuid=function(self,uuid)
   if uuid~=state.worldUuid then return nil end
   if not state.reverseValid then return {uuid=17} end
   return {uuid=state.worldUuid, mainIndex=state.reverseMain,
           serverId=state.worldServer, ownerUid=state.worldOwner,allianceId=state.worldAlliance}
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
           clientCreateGuid=state.marchClientCreateGuid,
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


def check_installed_managed_nonce_source() -> None:
    """Static SHA-pinned installed C# contract, never a claim of a live reply."""
    root = Path.home() / ("AppData/Local/FunFly/Last War-Survival Game/Game/"
                          "LastWar_Data/Assemblies")
    assemblies = {
        "Assembly-CSharp.rdl": "bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e",
        "SmartFox2X.rdl": "bdb893b29cd5c1ffe4e2ebecf504209c47d775de5dd08e34d09ad5fb6107a96b",
        "Smartfox2xLw.rdl": "126d1f34c0f13b91ae1a7d37a9f4a7b523441671e421c7e2cdad78edc28f692a",
    }
    for name, expected in assemblies.items():
        path = root / name
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        require(digest == expected, "installed managed nonce source changed: " + name)
        if name != "Assembly-CSharp.rdl":
            img = MetadataImage.load(path)
            typ = ("Sfs2X" if name == "SmartFox2X.rdl" else "Sfs2XLw")
            for typename in (f"{typ}.Entities.Data.SFSObject",
                             f"{typ}.Entities.Data.ISFSObject"):
                matching = [
                    row for i, row in enumerate(img.tables.TypeDef.rows, 1)
                    if img.type_name("TypeDef", i) == typename
                ]
                require(len(matching) == 1, "managed SFS object type missing: " + typename)
                methods = [ref.row for ref in matching[0].MethodList
                           if str(ref.row.Name) == "GetUtfString"]
                require(len(methods) == 1 and
                        img.decode_method_signature(bytes(methods[0].Signature.value)) ==
                            ("string", ["string"]),
                        "installed SmartFox GetUtfString(string) signature unavailable")
            continue
        img = MetadataImage.load(path)
        def verify(token: int, label: str, fragments: tuple[str, ...]) -> None:
            row = token & 0xFFFFFF
            lines = managed_disasm(img, Resolver(img), row)
            require(img.method_label(row) == label and
                    all(any(part in ins for ins in lines) for part in fragments),
                    "original managed token or IL contract changed: " + label)

        verify(0x060032de, "WorldMarchDataManager::SaveCreateMarchRecordTime",
               ("System.Guid::NewGuid", "System.Object::ToString",
                "GetServerTime", "_cacheClientCreateGuidAndTimeDict"))
        verify(0x060031bc, "WorldMarch::get_clientCreateGuid",
               ("WorldMarch::_clientCreateGuid",))
        verify(0x060031e6, "WorldMarch::UpdateFromSFS",
               ("'clientCreateUuid'", "MessageExtension::TryGetString",
                "WorldMarch::_clientCreateGuid"))
        verify(0x060032df, "WorldMarchDataManager::RemoveCreateMarchRecordTime",
               ("WorldMarch::get_clientCreateGuid",
                "_cacheClientCreateGuidAndTimeDict", "ContainsKey",
                "CreateMarchDeltaTime"))
        verify(0x06003285, "WorldMarchDataManager::HandleFormationMarch",
               ("StepProcessQueue", "Push", "UpdateLastMarchMessage"))
        verify(0x06003286, "WorldMarchDataManager::HandleFormationMarchImpl",
               ("ParseFormationUpdate",))
        verify(0x06003289, "WorldMarchDataManager::ParseFormationUpdate",
               ("'uuid'", "GetLong", "ParsePushWorldMarchAdd"))
        # This method is a registered Lua property getter; the native GUID
        # can be used as an independent poststate witness IF actually visible
        # on a fresh owner march. Source membership alone is not live proof.
        wrapped = img.tables.MethodDef.rows[(0x06008a4b & 0xFFFFFF) - 1]
        require(img.method_label(0x06008a4b & 0xFFFFFF) ==
                "XLua.CSObjectWrap.WorldMarchWrap::_g_get_clientCreateGuid" and
                img.decode_method_signature(bytes(wrapped.Signature.value)) ==
                    ("int32", ["native int"]),
                "original XLua WorldMarch clientCreateGuid getter is missing")


def mock():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    module = vm.execute(SOURCE.read_text(encoding="utf-8"))
    state = vm.globals().state

    def call(action="speedupItem", slot="1", item_id="3108", **overrides):
        fields = {
            "action": action, "slot": slot, "itemId": item_id,
            "itemOptIn": "1", "authorizedUid": "1183884595002296",
            "profileId": "profile", "launchSessionId": "launch",
            "challenge": "request", "gamePid": "7020",
        }
        fields.update(overrides)
        return module.Handle(vm.table_from(fields), vm.globals().input_clear)

    def inspect(**overrides):
        fields = {
            "slot": "1", "authorizedUid": "1183884595002296",
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
        "Net/SFSNetwork.luac": (
            "e093de00dbb63ce2f985b541ba00f4252963816ba65a10d3335a1820fd7f2225",
            {"SendMessage": ["NewMessage", "ToBinary", "SendLuaMessage"]}),
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
            {"InitData": ["targetType", "targetPoint", "targetUuid", "targetServerId",
                          "currentFormationUuid"],
             "OnEditClick": ["UIHeroPVPFormation", "OpenWindow",
                             "EnterHeroSquadPanelWay", "ToMarch"],
             "OnAtkClick": ["GetOwnerFormationMarch", "ChangeMarchByType"],
             "OnCheckTime": ["OnCreateClick", "ShowMessage"],
             "GetCurSoldierNum": ["GetOneArmyInfoByUuid", "soldiers", "walk"],
             "CheckCanBattle": ["GetOneArmyInfoByUuid", "GetCurStaminaByUuid"],
             "ChangeMarchByType": ["GetOwnerFormationMarch", "OnAttackArmy",
                                   "OnAttackOtherCity"]}),
        "UI/UIFormation/UIFormationSelectListNew/Controller/UIFormationSelectListNewCtrl.luac": (
            "c7081eed53c9d2cc34707b580efcf17287316988dfa5d5696fbb93db2d737fc9",
            {"OnEditClick": ["UIHeroPVPFormation", "OpenWindow",
                             "EnterHeroSquadPanelWay", "ToMarch"]}),
        "UI/UILWHero/UIHeroPVPFormationPanel/Controller/UIHeroPVPFormationPanelCtrl.luac": (
            "de23c994f10914de2f8a268d27f085aa2d61993efcb48d3253cd851d230b4c35",
            {"SaveSquad": ["FormationSave", "NormalFormationInfoSave", "SendMessage"]}),
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
            if name == "Net/SFSNetwork.luac" and name_ == "SendMessage":
                ins=disassemble(proto)
                new_message=next(i for i,x in enumerate(ins) if "'NewMessage'" in x)
                serialize=next(i for i,x in enumerate(ins) if "'ToBinary'" in x)
                raw_send=next(i for i,x in enumerate(ins) if "'SendLuaMessage'" in x)
                send_call=next(i for i,x in enumerate(ins)
                               if "pc=0011 " in x and " CALL " in x)
                require(new_message<raw_send<serialize<send_call and
                        any(" RETURN " in x for x in ins[send_call+1:]),
                        "original native SendMessage is a serialization/send path, not a server ACK")
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
            if name.endswith("UIFormationSelectListV2Ctrl.luac") and name_ == "OnEditClick" or (
                name.endswith("UIFormationSelectListNewCtrl.luac") and name_ == "OnEditClick"
            ):
                require(len(proto["children"]) == 1 and
                        "OnCheckTime" in proto["children"][0]["constants"],
                        "native editor completion callback no longer starts a march")
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
          local token=string.format('12345678-1234-4abc-8def-%012d',state.createCounter)
          state.marchClientCreateGuid=token
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
        fields=dict(action=action,slot='1',authorizedUid='1183884595002296',
                    profileId='profile',launchSessionId='launch',challenge='request',gamePid='7020')
        fields.update(extra)
        return module.Handle(vm.table_from(fields),vm.globals().input_clear)
    first=call()
    require(first['state']=='accepted' and state['attackDelegateCount']==1 and
            first['data']['requestDispatched'] is True and
            first['data']['nativeTransaction']['sendCount']==1 and
            first['data']['nativeTransaction']['nonceStatus']=='awaiting_same_nonce_reply' and
            state['attackSends']==1,
            'native selector did not dispatch fixed own squad exactly once')
    editor_attempt=module.Prepare(vm.table_from(dict(
        mode='staffingEditor',slot='1',editorOptIn='1',
        authorizedUid='1183884595002296',
        profileId='profile',launchSessionId='launch',
        challenge='request',gamePid='7020')),vm.globals().input_clear)
    require(editor_attempt['state']=='failed' and
            'unresolved attack or rapid transaction' in editor_attempt['error'] and
            state['attackDelegateCount']==1,
            'native staffing editor shortcut bypassed a pending attack fence')
    pending_view=module.Prepare(vm.table_from(dict(
        mode='attackReadiness',slot='1',
        authorizedUid='1183884595002296',
        profileId='profile',launchSessionId='launch',
        challenge='request',gamePid='7020')),vm.globals().input_clear)
    require(pending_view['state']=='confirmed' and
            pending_view['data']['sourcePrerequisitesObserved'] is False and
            'native_attack_or_speedup_transaction_pending' in
              list(pending_view['data']['outstandingReasons'].values()) and
            pending_view['data']['requestDispatched'] is False and
            state['attackDelegateCount']==1 and state['attackSends']==1,
            'read-only R8 readiness missed in-flight one-shot attack fence')
    require(call(queryOnly='1')['state']=='accepted' and state['attackDelegateCount']==1,
            'pending attack was resent or falsely confirmed without native march')
    state['existingAbsent']=False
    state['targetUuid']=990124
    require(call(queryOnly='1')['state']=='accepted','different target march falsely confirmed')
    state['targetUuid']=990123
    require(call(queryOnly='1')['state']=='accepted',
            'local-only native march was misreported as a server ACK')
    vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
        {}, {clientCreateUuid='12345678-1234-4abc-8def-000000000001',ownerUid=state.uid,uuid=state.uuid})""")
    state['marchClientCreateGuid']=None
    absent_guid=call(queryOnly='1')
    require(absent_guid['state']=='accepted' and
            absent_guid['data']['nativeMarchPostStateVerified'] is False and
            absent_guid['data']['nativeTransaction']['nativeMarchGuidObserved'] is False,
            'matching reply/target without native march create GUID claimed success')
    state['marchClientCreateGuid']='abcdef01-1234-4abc-8def-000000000001'
    bad_native_guid=call(queryOnly='1')
    require(bad_native_guid['state']=='accepted' and
            bad_native_guid['data']['nativeMarchPostStateVerified'] is False and
            bad_native_guid['data']['nativeTransaction']['nativeMarchGuidMatchesRequest'] is False,
            'matching reply and target falsely confirmed a native march with another original create GUID')
    state['marchClientCreateGuid']= '12345678-1234-4abc-8def-000000000001'
    ack=call(queryOnly='1')
    require(ack['state']=='confirmed' and ack['data']['serverAcknowledgementCorrelated'] is True and
            ack['data']['nativeMarchPostStateVerified'] is True and
            ack['data']['transaction']['nonceStatus']==
                'same_nonce_owner_march_reply_observed_requires_fresh_poststate' and
            ack['data']['nativeMarchGuidMatchesRequest'] is True and
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
        {}, {clientCreateUuid='12345678-1234-4abc-8def-000000000002',ownerUid=state.uid,uuid=state.uuid})""")
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


def test_attack_final_action_boundary_and_sender_truth():
    # The native v26 OnAtkClick branches on GetOwnerFormationMarch; the
    # adapter must not enter a create-march controller after that slot gains
    # an existing march, nor act on a foreign/stale formation. A witnessed
    # original packet send enables the desktop's query-only ACK polling.
    for mode in ('foreign_initial', 'foreign_refresh', 'wrong_slot',
                 'late_march', 'owner_midflight', 'server_midflight',
                 'deferred_dialog', 'original_send'):
        vm=LuaRuntime(unpack_returned_tuples=True)
        vm.execute(GAME)
        vm.execute("""
          state.existingAbsent=true;state.startTime=1000;state.targetUuid=990123
          state.formReads=0;state.marchReads=0
          MsgDefines.WorldMarchFormationNew=99
          local getForm=DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex
          DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
            state.formReads=state.formReads+1
            local form=getForm(self,slot)
            if state.checkMode=='foreign_initial' or
                (state.checkMode=='foreign_refresh' and state.formReads==2) then
              form.ownerUid=111
            end
            if state.checkMode=='wrong_slot' then form.index=2 end
            if state.formReads==2 then
              if state.checkMode=='owner_midflight' then LuaEntry.Player.uid=222 end
              if state.checkMode=='server_midflight' then state.serverId=2212 end
            end
            return form
          end
          local getMarch=DataCenter.WorldMarchDataManager.GetOwnerFormationMarch
          DataCenter.WorldMarchDataManager.GetOwnerFormationMarch=function(self,uid,form,alliance)
            state.marchReads=state.marchReads+1
            if state.checkMode=='late_march' and state.marchReads==2 then
              return {uuid=555110,ownerUid=state.uid,ownerFormationUuid=state.formation}
            end
            return getMarch(self,uid,form,alliance)
          end
          package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()
            return {OnCreate=function(self,formation,kind,target)
              local token='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'
              self.sfsObj={GetUtfString=function(_,key)
                assert(key=='clientCreateUuid'); return token
              end}
            end,HandleMessage=function(self,response) end}
          end
          UIWindowNames={UIFormationSelectListV2='formation'}
          nativeCtrl={targetType=17,targetUuid=990123,targetPoint=state.touchedTile}
          nativeCtrl.OnCheckTime=function(self,formation)
            state.attackDelegateCount=state.attackDelegateCount+1
            if state.checkMode~='deferred_dialog' then
              SFSNetwork.SendMessage(99,formation,17,990123,'native-route')
            end
          end
          nativeWindow={Ctrl=nativeCtrl}
          UIManager={GetInstance=function()return {
            IsWindowOpen=function(_,name)return name=='formation' end,
            GetWindow=function(_,name)return name=='formation' and nativeWindow end,
          }end}
        """)
        vm.globals().state.checkMode=mode
        module=vm.execute(SOURCE.read_text(encoding='utf-8'))
        def call(**extra):
            fields=dict(action='attack',slot='1',authorizedUid='1183884595002296',
                        profileId='profile',launchSessionId='launch',
                        challenge='request',gamePid='7020')
            fields.update(extra)
            return module.Handle(vm.table_from(fields),vm.globals().input_clear)
        result=call()
        state=vm.globals().state
        if mode in ('foreign_initial','foreign_refresh','wrong_slot','late_march',
                    'owner_midflight','server_midflight'):
            require(result['state']=='failed' and
                    state.attackDelegateCount==0 and (state.attackSends or 0)==0,
                    'native owner/index or pre-send march race sent a request: '+mode)
        else:
            expected_send=0 if mode=='deferred_dialog' else 1
            require(result['state']=='accepted' and
                    result['data']['requestDispatched'] is (expected_send==1) and
                    result['data']['awaitingGameConfirmation'] is True and
                    state.attackDelegateCount==1 and
                    (state.attackSends or 0)==expected_send,
                    'native action reported a packet that was not actually observed: '+mode)
            polled=call(queryOnly='1')
            require(polled['state']=='accepted' and
                    polled['data']['requestDispatched'] is False and
                    (state.attackSends or 0)==expected_send,
                    'queryOnly status attempted a duplicate attack send: '+mode)
        if mode=='late_march':
            require(state.marchReads==2,
                    'original native formation was not revalidated at send boundary')


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
            ('nonce_malformed', 'accepted', 'accepted', 1),
            ('late_manual', 'accepted', 'failed', 0),
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
              state.marchClientCreateGuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'
              self.sfsObj=state.sendMode=='nonce_missing' and {} or
                {GetUtfString=function(self,key)
                  if state.sendMode=='nonce_malformed' then return 'not-a-guid' end
                  return 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'
                end}
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
            if state.sendMode=='no_send' or state.sendMode=='late_manual' then return end
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
            fields=dict(action='attack',slot='1',authorizedUid='1183884595002296',
                        profileId='profile',launchSessionId='launch',
                        challenge='request',gamePid='7020')
            fields.update(extra)
            return module.Handle(vm.table_from(fields),vm.globals().input_clear)
        dispatched=call()
        require(dispatched['state']==immediate and
                vm.globals().state.attackDelegateCount==1 and
                (vm.globals().state.attackSends or 0)==sends,
                'unexpected native attack dispatch outcome: '+mode)
        if immediate=='accepted':
            require(dispatched['data']['requestDispatched'] is
                    (mode not in ('no_send','wrong_target','late_manual')),
                    'native packet dispatch receipt does not match original sender: '+mode)
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
              {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',ownerUid=state.uid,
                   uuid=state.uuid,errorCode='native_reject'})""")
        if mode=='controller_false':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='nonce_missing':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='nonce_malformed':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='not-a-guid',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='late_manual':
            vm.execute("""SFSNetwork.SendMessage(99,state.formation,17,990123,'path')
              require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
                {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
                     ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='reply_wrong_nonce':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='unrelated-create-id',ownerUid=state.uid,uuid=state.uuid})""")
        if mode=='reply_wrong_owner':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',ownerUid=123,uuid=state.uuid})""")
        if mode=='reply_wrong_march':
            vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
              {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',ownerUid=state.uid,uuid=123})""")
        if mode=='reply_duplicate':
            vm.execute("""for i=1,2 do
              require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage(
                {}, {clientCreateUuid='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',ownerUid=state.uid,uuid=state.uuid})
              end""")
        vm.globals().state.existingAbsent=False
        polled=call(queryOnly='1')
        require(polled['state']==later and
                vm.globals().state.attackDelegateCount==1 and
                (vm.globals().state.attackSends or 0)==
                    sends+(1 if mode=='late_manual' else 0),
                'unproven native attack was confirmed or resent: '+mode)
        if mode=='late_manual':
            require(polled['data']['nativeTransaction']['sendCount']==0 and
                    polled['data']['nativeTransaction']['unattributedSendCount']==1 and
                    polled['data']['nativeTransaction']['nonceStatus']==
                      'matching_send_outside_original_controller_causal_scope',
                    'late native user/dialog send acquired attack-origin nonce or ACK')


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
        assert(a[5]==nil and a[6]==nil and a[7]==nil and a[8]==2296 and a[9]==0)
        state.prepared=true
      end}
    """)
    module=vm.execute(SOURCE.read_text(encoding='utf-8'))
    fields=dict(mode='inventory',authorizedUid='1183884595002296',profileId='profile',
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


def test_manual_native_formation_editor_no_automatic_march():
    # BOTH native V2/New OnEditClick callbacks call OnCheckTime after editor
    # confirmation. Opening the exact original editor with an inert callback
    # is the only bounded way to offer owner-driven staffing without turning
    # editor completion into an unconfirmed attack send.
    vm=LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    vm.execute("""
      state.existingAbsent=true
      state.editOpens=0
      UIWindowNames={UIFormationSelectListV2='native-selector',
                     UIFormationSelectListNew='native-selector-new',
                     UIHeroPVPFormation='native-editor'}
      EnterHeroSquadPanelWay={ToMarch=9}
      nativeCtrl={OnEditClick=function()error('unsafe original callback')end,
                  OnCheckTime=function()
                    state.attackSends=(state.attackSends or 0)+1
                  end}
      nativeWindow={Ctrl=nativeCtrl}
      manager={
        IsWindowOpen=function(self,name)
          if name=='native-selector' then return not state.selectorClosed end
          if name=='native-editor' then return state.editorOpen or false end
          return false
        end,
        GetWindow=function(self,name)
          if name=='native-selector' then return nativeWindow end
          return nil
        end,
        OpenWindow=function(self,name,options,way,uuid,callback)
          assert(name=='native-editor' and options.anim==true
            and way==9 and uuid==state.formation and type(callback)=='function')
          state.editOpens=state.editOpens+1
          state.editorCallback=callback
          state.editorOpen=true
        end
      }
      UIManager={GetInstance=function()return manager end}
    """)
    mod=vm.execute(SOURCE.read_text(encoding="utf-8"))
    def edit(**extra):
        fields=dict(mode='staffingEditor',slot='1',editorOptIn='1',
                    authorizedUid='1183884595002296',profileId='profile',
                    launchSessionId='launch',challenge='request',gamePid='7020')
        fields.update(extra)
        return mod.Prepare(vm.table_from(fields),vm.globals().input_clear)
    state=vm.globals().state
    for overrides,expected in (({'editorOptIn':'0'},'failed'),
                               ({'slot':'4'},'failed'),
                               ({'authorizedUid':'1000'},'failed')):
        require(edit(**overrides)['state']==expected and state.editOpens==0,
                'unadmitted native editor request opened a window')
    state.existingAbsent=False
    require(edit()['state']=='failed' and state.editOpens==0,
            'currently active squad allowed manual editor with unknown subsequent march')
    state.existingAbsent=True
    state.selectorClosed=True
    require(edit()['state']=='failed' and state.editOpens==0,
            'no original native selector allowed editor launch')
    state.selectorClosed=False
    state.inputClear=False
    require(edit()['state']=='failed' and state.editOpens==0,
            'native formation editor bypassed game text input guard')
    state.inputClear=True
    success=edit()
    require(success['state']=='confirmed' and
            success['data']['observation']=='native_manual_formation_editor_open' and
            success['data']['formationSaveSent'] is False and
            success['data']['attackDispatched'] is False and
            success['data']['operatorMustSaveInOriginalGame'] is True and
            state.editOpens==1 and state['attackSends'] is None and
            state.rpcCount==0,
            'native editor not opened once with safe manual-only source contract')
    vm.execute("state.editorCallback()")
    require(state['attackSends'] is None and state.rpcCount==0,
            'original OnEditClick automatic OnCheckTime march callback was reused')
    require(edit()['state']=='failed' and state.editOpens==1,
            'second editor launch occurred while native editor already open')
    state.editorOpen=False
    state.serverId=2212
    require(edit()['state']=='confirmed' and state.editOpens==2,
            'another authenticated native server was incorrectly denied by shipped product')
    state.editorOpen=False
    state.serverId=0
    require(edit()['state']=='failed' and state.editOpens==2,
            'unknown server identity entered native formation editor')


def test_original_selector_typed_target_preflight_and_dispatch_revalidation():
    vm=LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    vm.execute("""
      state.existingAbsent=true
      state.targetUuid=990123
      state.targetPoint=state.touchedTile
      state.targetServerId=2296
      state.targetType=17
      state.revalidationCase='stable'
      state.targetLookups=0
      state.formationLookups=0
      MarchTargetType.ATTACK_MONSTER=19
      UIWindowNames={UIFormationSelectListV2='native-selector'}
      nativeCtrl={targetType=17,targetUuid=state.worldUuid,targetPoint=state.touchedTile,
                  targetServerId=2296}
      nativeCtrl.OnCheckTime=function()
        state.attackDelegateCount=state.attackDelegateCount+1
        error('unexpected send in isolated source revalidation test')
      end
      nativeWindow={Ctrl=nativeCtrl}
      UIManager={GetInstance=function() return {
        IsWindowOpen=function(self,name) return name=='native-selector' end,
        GetWindow=function(self,name) return name=='native-selector' and nativeWindow end
      } end}
      local originalForm=DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex
      DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
        state.formationLookups=state.formationLookups+1
        if state.formationLookups==2 then
          if state.revalidationCase=='point' then nativeCtrl.targetPoint=35724 end
          if state.revalidationCase=='selectorServer' then nativeCtrl.targetServerId=2212 end
          if state.revalidationCase=='worldOwner' then state.worldOwner=state.uid end
          if state.revalidationCase=='sameAlliance' then state.worldAlliance=700 end
          if state.revalidationCase=='unknownAlliance' then state.worldAlliance=nil end
          if state.revalidationCase=='otherAlliance' then state.worldAlliance=702 end
          if state.revalidationCase=='ownerAlliance' then LuaEntry.Player.allianceId=701 end
          if state.revalidationCase=='worldUnavailable' then state.worldPresent=false end
          if state.revalidationCase=='kind' then nativeCtrl.targetType=18 end
        end
        return originalForm(self,slot)
      end
      MsgDefines.WorldMarchFormationNew=99
      package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()
        return {OnCreate=function(self)end,HandleMessage=function(self,response)end}
      end
      package.preload['Net.Msgs.WorldMarchChangeMessage']=function()
        return {HandleMessage=function(self,response)end}
      end
    """)
    mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
    state=vm.globals().state
    basic=dict(slot='1',authorizedUid='1183884595002296',profileId='profile',
               launchSessionId='launch',challenge='request',gamePid='7020')
    def prep(**extra):
        fields=dict(basic,mode='selectorTarget')
        fields.update(extra)
        return mod.Prepare(vm.table_from(fields),vm.globals().input_clear)
    witness=prep()
    require(witness['state']=='confirmed' and
            witness['data']['target']['uuid']=='990123' and
            witness['data']['target']['point']==35723 and
            witness['data']['target']['targetOwnerUid']=='999' and
            witness['data']['target']['selectorServer']==2296 and
            witness['data']['target']['currentCursorVerified'] is False and
            witness['data']['safeToDispatchFromPhysicalCursor'] is False and
            state.attackDelegateCount==0,
            'typed selector read-only native source preflight incorrectly claimed physical cursor')
    vm.execute("Input={mousePosition={x=123,y=456}}")
    require(prep()['data']['target']['physicalCursorTargetVerified'] is False,
            'untrusted mousePosition changed typed native selector provenance')
    state.worldOwner=state.uid
    require(prep()['state']=='failed', 'owned native city selector passed target check')
    state.worldOwner=999
    state.worldServer=2212
    require(prep()['state']=='failed','foreign native city point server passed target check')
    state.worldServer=2296
    vm.execute("nativeCtrl.targetServerId=2212")
    require(prep()['state']=='failed','foreign selector server passed target check')
    vm.execute("nativeCtrl.targetServerId=2296")
    for alliance in (700, None, -1, True, 'invalid', ''):
        state.worldAlliance=alliance
        require(prep()['state']=='failed' and state.attackDelegateCount==0,
                'same or unknown city alliance accepted: '+repr(alliance))
    state.worldAlliance=0
    require(prep()['state']=='confirmed' and
            prep()['data']['target']['targetAllianceUid']=='0',
            'explicit unaffiliated city was not observed as neutral')
    state.worldAlliance=701
    for case in ('point','selectorServer','worldOwner','worldUnavailable','kind',
                 'sameAlliance','unknownAlliance','otherAlliance','ownerAlliance'):
        vm.execute("""nativeCtrl.targetPoint=35723
          nativeCtrl.targetServerId=2296; nativeCtrl.targetType=17""")
        state.worldPresent=True
        state.worldOwner=999
        state.worldAlliance=701
        vm.execute('LuaEntry.Player.allianceId=700')
        state.revalidationCase=case
        state.formationLookups=0
        fields=dict(basic,action='attack')
        rejected=mod.Handle(vm.table_from(fields),vm.globals().input_clear)
        require(rejected['state']=='failed' and
                state.attackDelegateCount==0 and
                (state.attackSends is None or state.attackSends==0),
                'changed selector/native target dispatched attack: '+case)
        require(mod.Handle(vm.table_from(dict(fields,queryOnly='1')),
                           vm.globals().input_clear)['state']=='failed',
                'rejected target drift created a phantom pending attack: '+case)


def test_native_alliance_field_selection_for_typed_targets():
    # Target identities come from different native classes. An allianceId
    # field on a march may not substitute for WorldMarch.allianceUid.
    for category, kind in (('city',17),('army',18),('monster',19),('resource',20)):
        vm=LuaRuntime(unpack_returned_tuples=True)
        vm.execute(GAME)
        vm.execute("""
          state.targetAlliance=701
          state.resourceGather=0
          MarchTargetType.ATTACK_MONSTER=19
          MarchTargetType.COLLECT=20
          CS.SceneManager.World.GetMarch=function(self,id)
            return {uuid=id,ownerUid=999,serverId=state.serverId,
              allianceUid=state.targetAlliance,allianceId=701}
          end
          CS.SceneManager.World.GetResourcePointInfoByIndex=function(self,id)
            return {id=301,mainIndex=id,serverId=state.serverId,
              gatherMarchUuid=state.resourceGather}
          end
          UIWindowNames={UIFormationSelectListV2='formation'}
          nativeCtrl={targetType=17,targetPoint=state.touchedTile,
            targetUuid=state.worldUuid,targetServerId=state.serverId}
          nativeWindow={Ctrl=nativeCtrl}
          UIManager={GetInstance=function() return {
            IsWindowOpen=function(self,name)return name=='formation' end,
            GetWindow=function(self,name)return name=='formation' and nativeWindow end,
          } end}
        """)
        state=vm.globals().state
        vm.globals().nativeCtrl.targetType=kind
        if category=='resource':
            vm.globals().nativeCtrl.targetUuid=None
        mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
        fields=vm.table_from(dict(mode='selectorTarget',authorizedUid='1183884595002296',
            profileId='profile',launchSessionId='launch',challenge='request',gamePid='7020'))
        def observe():
            return mod.Prepare(fields,vm.globals().input_clear)
        require(observe()['state']=='confirmed',
                'source-backed native target alliance did not resolve: '+category)
        for own in (None, -1, True, 'unreadable', ''):
            vm.globals().LuaEntry.Player.allianceId=own
            require(observe()['state']=='failed',
                    'unknown owner alliance accepted for '+category+': '+repr(own))
        vm.globals().LuaEntry.Player.allianceId=700
        if category=='resource':
            for occupied in (None, -1, True, 555110, 'invalid'):
                state.resourceGather=occupied
                require(observe()['state']=='failed',
                        'unknown/occupied typed resource accepted: '+repr(occupied))
        else:
            for alliance in (700, None, -1, True, 'invalid', ''):
                state.worldAlliance=alliance
                state.targetAlliance=alliance
                require(observe()['state']=='failed',
                        'same/unknown native alliance accepted for '+category+': '+repr(alliance))
            state.worldAlliance=0
            state.targetAlliance=0
            require(observe()['state']=='confirmed' and
                    observe()['data']['target']['targetAllianceUid']=='0',
                    'explicit zero native alliance rejected for '+category)
        require(state.attackDelegateCount==0 and state.rpcCount==0,
                'alliance preflight issued gameplay: '+category)


def test_original_reply_observer_required_before_native_controller():
    for mode in ('missing','displaced','serializerDisplaced','senderDisplaced','netReplaced'):
        vm=LuaRuntime(unpack_returned_tuples=True)
        vm.execute(GAME)
        vm.execute("""
          state.existingAbsent=true
          UIWindowNames={UIFormationSelectListV2='native-selector'}
          nativeCtrl={targetType=17,targetPoint=state.touchedTile,
                      targetUuid=state.worldUuid}
          nativeCtrl.OnCheckTime=function(self,form)
            state.attackDelegateCount=state.attackDelegateCount+1
          end
          nativeWindow={Ctrl=nativeCtrl}
          UIManager={GetInstance=function() return {
            IsWindowOpen=function(self,name)return name=='native-selector' end,
            GetWindow=function(self,name)return name=='native-selector' and nativeWindow end,
          } end}
          MsgDefines.WorldMarchFormationNew=99
          package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()
            local class={OnCreate=function(self,...)end}
            if state.observerMode~='missing' then
              class.HandleMessage=function(self,response)end
            end
            return class
          end
          package.preload['Net.Msgs.WorldMarchChangeMessage']=function()
            return {HandleMessage=function(self,response)end}
          end
          local original=DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex
          DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
            if slot==2 then
              return {uuid=1002,soldiers={[101]=110},heroes={[1]=777}}
            end
            return original(self,slot)
          end
        """)
        state=vm.globals().state
        state.observerMode=mode
        mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
        def attack(slot):
            fields=dict(action='attack',slot=str(slot),authorizedUid='1183884595002296',
                        profileId='profile',launchSessionId='launch',challenge='request',gamePid='7020')
            return mod.Handle(vm.table_from(fields),vm.globals().input_clear)
        if mode!='missing':
            first=attack(1)
            require(first['state']=='accepted' and state.attackDelegateCount==1,
                    'mock did not install all original march observers')
            if mode=='displaced':
                vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').HandleMessage=
                    function(self,response)end""")
            elif mode=='serializerDisplaced':
                vm.execute("""require('Net.Msgs.WorldMarchFormationNewMessage').OnCreate=
                    function(self,...)end""")
            elif mode=='senderDisplaced':
                vm.execute("SFSNetwork.SendMessage=function(...)end")
            else:
                vm.execute("SFSNetwork={SendMessage=function(...)end}")
            second=attack(2)
            require(second['state']=='unsupported' and state.attackDelegateCount==1,
                    'missing/replaced original sender/nonce/reply observer allowed attack: '+mode)
        else:
            denied=attack(1)
            require(denied['state']=='unsupported' and state.attackDelegateCount==0,
                    'missing v26 native create-march HandleMessage permitted dispatch')
        require(state.rpcCount==0 and
                (state.attackSends is None or state.attackSends==0),
                'unobservable attack reply admission emitted network RPC: '+mode)


def test_readonly_end_to_end_source_prerequisites():
    vm=LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    vm.execute("""
      state.existingAbsent=true
      UIWindowNames={UIFormationSelectListV2='formation'}
      nativeCtrl={targetType=17,targetUuid=state.worldUuid,
                  targetPoint=state.touchedTile,targetServerId=2296}
      nativeWindow={Ctrl=nativeCtrl}
      UIManager={GetInstance=function()return {
        IsWindowOpen=function(self,name)
          return name=='formation' and state.selectorOpen~=false
        end,
        GetWindow=function(self,name)
          if name=='formation' then return nativeWindow end
        end
      }end}
      DataCenter.ArmyFormationDataManager.GetArmyUnFormationList=function()
        return {[101]=state.freeTroops or 50}
      end
      local savedGetForm=DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex
      DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
        local form=savedGetForm(self,slot)
        if form then form.index=slot end
        return form
      end
      MarchUtil.GetMaxCanAddSoldierNum=function(heroes,index)
        return 150
      end
      state.noReadinessSend=true
      MarchUtil.TryStartMarch=function(...)error('readiness invoked action')end
      SFSNetwork.SendMessage=function(...)error('readiness sent network request')end
    """)
    state=vm.globals().state
    mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
    fields=dict(mode='attackReadiness',slot='1',
                authorizedUid='1183884595002296',profileId='profile',
                launchSessionId='launch',challenge='request',gamePid='7020')
    def read(**overrides):
        return mod.Prepare(vm.table_from(dict(fields,**overrides)),vm.globals().input_clear)
    ready=read()
    data=ready['data']
    require(ready['state']=='confirmed' and
            data['sourcePrerequisitesObserved'] is True and
            len(data['outstandingReasons'])==0 and
            data['target']['uuid']=='990123' and
            data['formationStaffing']['soldierCount']==110 and
            data['nativeMarchLookupSucceeded'] is True and
            data['physicalCursorTargetVerified'] is False and
            data['keyboardInputFocusAtActionVerified'] is False and
            data['serverAcknowledgementCorrelated'] is False and
            data['safeToDispatchFromPhysicalCursor'] is False and
            data['attackDispatched'] is False and
            state['attackDelegateCount']==0 and state['rpcCount']==0,
            'source readiness fabricated a cursor, attack, nonce, or server receipt')
    state.inputClear=False
    require(read()['data']['keyboardInputFocusAtActionVerified'] is False,
            'read-only readiness pretended native text focus was checked at send time')
    state.inputClear=True
    for flag,value,reason in (
        ('existingAbsent',False,'native_formation_has_existing_march'),
        ('soldierCount',0,'native_formation_soldiers_or_heroes_unstaffed'),
        ('heroCount',0,'native_formation_soldiers_or_heroes_unstaffed'),
        ('formationOwner',123,'native_formation_owner_mismatch'),
        ('worldPresent',False,'native_typed_selector_target_invalid'),
        ('worldServer',2212,'native_typed_selector_target_invalid'),
        ('selectorOpen',False,'native_typed_selector_not_open')):
        old=state[flag]
        state[flag]=value
        rejected=read()
        reasons=list(rejected['data']['outstandingReasons'].values())
        require(rejected['state']=='confirmed' and
                rejected['data']['sourcePrerequisitesObserved'] is False and
                reason in reasons and state.attackDelegateCount==0,
                'source readiness missed real native blocker '+flag)
        state[flag]=old
    state.soldierCount=0
    shortage=read()
    require(shortage['data']['originalManualEditorMayHelp'] is True and
            shortage['data']['staffingMutationPerformed'] is False,
            'unstaffed slot failed to report original manual editor possibility')
    state.soldierCount=110
    require(read(slot='5')['state']=='failed' and
            read(authorizedUid='999')['state']=='failed',
            'readiness ignored fixed slot or approved account identity')


def test_original_world_scene_cursor_projection_is_read_only():
    vm=LuaRuntime(unpack_returned_tuples=True)
    vm.execute(GAME)
    vm.execute("""
      -- The original managed WorldScene.GetTouchPoint requires an explicit
      -- Vector3 screen point. Physical Input.mousePosition is passed intact.
      CS.UnityEngine={Screen={width=1280,height=1134},
        Input={mousePosition={x=700,y=400}},
        Vector3={New=function(x,y,z) return {x=x,y=y,z=z} end}}
      Vector3=CS.UnityEngine.Vector3
      state.touchPointCalls=0
      state.worldTileCalls=0
      state.hover=9007199254740993
      CS.SceneManager.World.GetRaycastHitMarch=function(self,p)
        assert(p.x==700 and p.y==400)
        return state.hover
      end
      CS.SceneManager.World.GetTroop=function(self,id)
        if id~=state.hover then return nil end
        return {GetMarchUUID=function() return state.hover end,
          IsMonsterTroop=function()return true end,
          IsWorldBossTroop=function()return state.hoverIsBoss==true end}
      end
      CS.SceneManager.World.GetTouchPoint=function(self,p)
        state.touchPointCalls=state.touchPointCalls+1
        local screen=p
        if not screen or screen.x~=700 or screen.y~=400 then return nil end
        return {x=state.touchedX,y=0,z=state.touchedZ}
      end
      CS.SceneManager.World.WorldToTile=function(self,p)
        state.worldTileCalls=state.worldTileCalls+1
        return {x=p.x,z=p.z}
      end
      CS.SceneManager.World.TilePosToIndex=function(self,tile)
        return tile.x==state.touchedX and tile.z==state.touchedZ
            and state.touchedTile or nil
      end
    """)
    state=vm.globals().state
    mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
    base=dict(mode='cursorPoint',authorizedUid='1183884595002296',
              profileId='profile',launchSessionId='launch',
              challenge='request',gamePid='7020')
    def probe(**changes):
        return mod.Prepare(vm.table_from(dict(base,**changes)),vm.globals().input_clear)
    sampled=probe(samplePixelX='700',samplePixelY='400')
    require(sampled['state']=='confirmed' and
            sampled['data']['cursor']['pointIndex']=='35723' and
            sampled['data']['cursor']['worldArmy']['marchUuid']=='9007199254740993' and
            sampled['data']['cursor']['worldArmy']['monsterVerified'] is True and
            sampled['data']['cursor']['target']['category']=='ordinary_monster' and
            sampled['data']['cursor']['target']['safeToDispatchFromPhysicalCursor'] is False and
            sampled['data']['cursor']['readOnly'] is True and
            sampled['data']['cursor']['physicalCursorRead'] is False and
            sampled['data']['requestDispatched'] is False and
            state.touchPointCalls==1 and state.worldTileCalls>=1 and
            state.attackDelegateCount==0 and state.rpcCount==0,
            'sampled screen-to-world projection did not match native tile without sending')
    state.worldPointType=5
    boss=probe(samplePixelX='700',samplePixelY='400')
    require(boss['state']=='confirmed' and
            boss['data']['cursor']['target']['category']=='world_boss_excluded' and
            boss['data']['cursor']['target']['safeToDispatchFromPhysicalCursor'] is False,
            'hovered world boss was treated as actionable target')
    vm.execute('state.hoverIsBoss=true')
    army_boss=probe(samplePixelX='700',samplePixelY='400')
    require(army_boss['data']['cursor']['worldArmy']['worldBoss'] is True and
            army_boss['data']['cursor']['worldArmy']['monsterVerified'] is False and
            army_boss['data']['cursor']['requestDispatched'] is False,
            'read-only WorldArmy diagnostic treated excluded world boss as ordinary')
    vm.execute('state.hoverIsBoss=false;state.hover=0')
    state.worldPointType=7
    resource=probe(samplePixelX='700',samplePixelY='400')
    require(resource['state']=='confirmed' and
            resource['data']['cursor']['target']['category']=='resource_node' and
            state.attackDelegateCount==0 and state.rpcCount==0,
            'resource observation incorrectly sent a march')
    state.worldPointType=4
    state.inputClear=False
    physical=probe()
    require(physical['state']=='confirmed' and
            physical['data']['cursor']['pixelX']==700 and
            physical['data']['cursor']['physicalCursorRead'] is True and
            physical['data']['cursor']['worldArmy']['raycastSucceeded'] is True and
            physical['data']['cursor']['worldArmy']['hitFound'] is False and
            state.touchPointCalls==5 and
            state.attackDelegateCount==0 and state.rpcCount==0,
            'read-only native cursor projection incorrectly required focus or sent gameplay')
    require(probe(samplePixelX='-1',samplePixelY='400')['state']=='failed' and
            probe(samplePixelX='800')['state']=='failed',
            'invalid viewport sample was accepted')
    vm.execute("""
      CS.SceneManager.World.GetTouchPoint=function(self,p)
        state.serverId=2212
        return {x=state.touchedX,y=0,z=state.touchedZ}
      end
    """)
    require(probe(samplePixelX='700',samplePixelY='400')['state']=='failed' and
            state.attackDelegateCount==0 and state.rpcCount==0,
            'server transition during native WorldScene cursor projection was accepted')


def test_original_hover_monster_and_resource_one_shot_chooser():
    # Inert Lua53 fixture uses exact Int64 above the binary64 integer limit.
    # Native raycast, native world march, troop and manager must all agree.
    for target in ('monster', 'monster_no_ground', 'monster_no_soldiers',
                   'monster_same_alliance', 'monster_unknown_alliance',
                   'monster_alliance_drift', 'owner_alliance_unknown',
                   'resource', 'resource_unknown_occupancy', 'resource_occupancy_drift',
                   'boss', 'occupied_resource', 'unknown_ground_point'):
        vm=LuaRuntime(unpack_returned_tuples=True)
        vm.execute(GAME)
        vm.execute("""
          state.existingAbsent=true
          state.hover=9007199254740993
          state.openCalls=0
          state.hoverType=7
          state.resourceGather=0
          state.worldPointType=7
          state.worldMain=35723
          state.touchCalls=0
          state.hoverAlliance=0
          state.raycastCalls=0
          state.vectorNewCalls=0
          MarchTargetType.ATTACK_MONSTER=19
          MarchTargetType.COLLECT=20
          NewMarchType={MONSTER=7}
          CS.UnityEngine={Screen={width=1280,height=1134},
            Input={mousePosition={x=700,y=400}},
            Vector3={New=function(x,y,z)
              state.vectorNewCalls=state.vectorNewCalls+1
              return {x=x,y=y,z=z,synthetic=true}
            end}}
          Vector3=CS.UnityEngine.Vector3
          CS.SceneManager.World.GetTouchPoint=function(self,p)
            state.touchCalls=state.touchCalls+1
            assert(p~=nil and p.x==700 and p.y==400)
            if state.groundMissing then return nil end
            return {x=state.touchedX,y=0,z=state.touchedZ}
          end
          CS.SceneManager.World.WorldToTile=function(self,p)
            return {x=p.x,z=p.z}
          end
          CS.SceneManager.World.TilePosToIndex=function(self,p)
            return state.touchedTile
          end
          CS.SceneManager.World.GetRaycastHitMarch=function(self,p)
            assert(p==CS.UnityEngine.Input.mousePosition and p.x==700 and p.y==400)
            state.raycastCalls=state.raycastCalls+1
            if state.raycastCalls==2 and state.hoverAllianceDrift then state.hoverAlliance=700 end
            if state.raycastCalls==3 and state.resourceOccupancyDrift then state.resourceGather=555110 end
            return state.hover
          end
          CS.SceneManager.World.GetResourcePointInfoByIndex=function(self,id)
            assert(id==35723)
            return {id=301,pointIndex=35723,mainIndex=35723,
              gatherMarchUuid=state.resourceGather,serverId=state.serverId}
          end
          CS.SceneManager.World.GetMarch=function(self,id)
            if id~=state.hover or id==0 then return nil end
            return {uuid=state.hover,monsterId=100,ownerUid=999,serverId=state.serverId,
              allianceUid=state.hoverAlliance}
          end
          CS.SceneManager.World.GetTroop=function(self,id)
            if id~=state.hover or id==0 then return nil end
            return {GetMarchUUID=function() return state.hover end,
              IsMonsterTroop=function() return true end,
              IsWorldBossTroop=function() return state.boss==true end,
              GetPosition=function() return CS.SceneManager.World.curTouchPoint end}
          end
          local oldGetMarch=DataCenter.WorldMarchDataManager.GetMarch
          DataCenter.WorldMarchDataManager.GetMarch=function(self,id)
            if id==state.hover and id~=0 then
              return {uuid=state.hover,monsterId=100,ownerUid=999,
                allianceUid=state.hoverAlliance,
                serverId=state.serverId,GetMarchType=function()return 7 end}
            end
            return oldGetMarch(self,id)
          end
          DataCenter.MonsterTemplateManager={GetMonsterTemplate=function(self,id)
            assert(id==100)
            return {special=0,level=1}
          end}
          MarchUtil.OnClickStartMarch=function(...)
            state.openCalls=state.openCalls+1
            state.openArgs=table.pack(...)
          end
        """)
        state=vm.globals().state
        if target in ('resource','occupied_resource','unknown_ground_point',
                      'resource_unknown_occupancy','resource_occupancy_drift'):
            vm.execute('state.hover=0')
        if target=='monster_same_alliance':
            vm.execute('state.hoverAlliance=700')
        if target=='monster_unknown_alliance':
            vm.execute('state.hoverAlliance=nil')
        if target=='monster_alliance_drift':
            vm.execute('state.hoverAllianceDrift=true')
        if target=='resource_unknown_occupancy':
            vm.execute('state.resourceGather=nil')
        if target=='resource_occupancy_drift':
            vm.execute('state.resourceOccupancyDrift=true')
        if target=='owner_alliance_unknown':
            vm.execute('LuaEntry.Player.allianceId=nil')
        if target=='monster_no_ground':
            vm.execute('state.groundMissing=true')
        if target=='monster_no_soldiers':
            vm.execute('''state.soldierCount=0;state.heroCount=2
              state.uid=1183884595002296
              state.serverId=2296
              state.formationOwner=state.uid
              LuaEntry.Player.uid=state.uid''')
        if target=='unknown_ground_point':
            vm.execute('state.worldPointType=17')
        if target=='boss':
            vm.execute('state.boss=true')
        if target=='occupied_resource':
            vm.execute('state.resourceGather=555110')
        mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
        payload=dict(action='attack',slot='1',
                     authorizedUid='1183884595002296',
                     profileId='profile',launchSessionId='launch',
                     challenge='request',gamePid='7020')
        def execute(**changes):
            return mod.Handle(vm.table_from(dict(payload,**changes)),
                              vm.globals().input_clear)
        first=execute()
        require(state.vectorNewCalls==0,
                'physical native hover reconstructed Unity Input.mousePosition')
        if target in ('boss','occupied_resource',
                      'unknown_ground_point','monster_same_alliance','monster_unknown_alliance',
                      'monster_alliance_drift','owner_alliance_unknown',
                      'resource_unknown_occupancy','resource_occupancy_drift'):
            require(first['state']=='failed' and state.openCalls==0 and
                    state.rpcCount==0,
                    'unsafe or unstaffed native hover entered original march UI')
            continue
        require(first['state']=='accepted' and
                first['data']['awaitingNativeChooser'] is True and
                first['data']['requestDispatched'] is False and
                first['data']['ownerUid']=='1183884595002296' and
                first['data']['sourceServerId']==2296 and
                first['data']['chooserRequestMade'] is True and
                first['data']['formationUuid']=='1001' and
                state.openCalls==1 and state.rpcCount==0,
                'physical hover did not invoke one source-native chooser without sending')
        args=state.openArgs
        if target in ('monster','monster_no_ground','monster_no_soldiers'):
            require(args[1]==19 and args[2]==35723 and
                    str(args[3])=='9007199254740993' and
                    args[4]==-1 and args[5]==1 and args['n']==5 and
                    first['data']['targetUuid']=='9007199254740993',
                    'native WorldArmy Int64 march UUID was rounded or wrong formation args')
            if target=='monster_no_ground':
                require(state.touchCalls==0 and state.rpcCount==0,
                        'moving monster mistakenly required a successful terrain raycast')
        else:
            require(args[1]==20 and args[2]==35723 and args[3] is None and
                    args[7]==2296 and args['n']==7 and
                    first['data']['targetUuid']=='35723',
                    'original resource gather selector/mainIndex/send namespace was lost')
        pending=execute(queryOnly='1')
        require(pending['state']=='accepted' and
                pending['data']['awaitingNativeChooser'] is True and
                pending['data']['requestDispatched'] is False and
                state.openCalls==1 and state.rpcCount==0,
                'queryOnly chooser poll repeated a physical hover or sent a march')
        repeat=execute()
        require(repeat['state']=='accepted' and state.openCalls==1 and
                state.rpcCount==0,
                'repeated physical hotkey reopened already pending native chooser')
        state.serverId=2212
        changed=execute(queryOnly='1')
        require(changed['state']=='failed' and state.openCalls==1 and
                state.rpcCount==0,
                'account/server switch continued frozen hover intent')


def test_current_nested_staffing_and_native_membership():
    for case in ('ready','zero','denied','unknown_permission','bad_supply','bad_total',
                 'changed_at_send','alliance_contradiction','target_unknown'):
        vm=LuaRuntime(unpack_returned_tuples=True)
        vm.execute(GAME)
        vm.execute("""
          state.existingAbsent=true
          state.currentCount=170
          state.currentSupply=170
          state.currentTotal=170
          state.currentCanMarch=true
          state.formReads=0
          state.forceNoAlliance=true
          LuaEntry.Player.allianceId=''
          DataCenter.WorldMarchDataManager.GetOwnerFormationMarch=function()return nil end
          DataCenter.SoldierDataManager={
            GetInsideSoldiers=function()return {{id=101,count=480,lv=1}} end,
            GetPlayerSoldiersTotalNum=function()return 480 end,
          }
          DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
            if slot~=1 then return nil end
            state.formReads=state.formReads+1
            if state.changeAtSend and state.formReads==2 then state.currentCanMarch=false end
            return {uuid=state.formation,ownerUid=state.uid,index=1,
              heroes={[1]=777,[2]=778},
              soldiers={[1]={[101]=state.currentCount,supply=state.currentSupply,heroMaxCapacity=170}},
              totalSoldierNum=state.currentTotal,heroTotalSoldierCapacity=170,
              canMarch=state.currentCanMarch,
              GetLocalHeroesCount=function()return 2 end}
          end
          UIWindowNames={UIFormationSelectListV2='formation'}
          nativeCtrl={targetType=17,targetPoint=state.touchedTile,targetUuid=state.worldUuid}
          nativeCtrl.OnCheckTime=function(self,formation)
            state.attackDelegateCount=state.attackDelegateCount+1
            SFSNetwork.SendMessage(99,formation,17,state.worldUuid,'current-path')
          end
          nativeWindow={Ctrl=nativeCtrl}
          UIManager={GetInstance=function()return {
            IsWindowOpen=function(self,name)return name=='formation' end,
            GetWindow=function(self,name)return name=='formation' and nativeWindow end}end}
          MsgDefines.WorldMarchFormationNew=99
          package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()
            return {OnCreate=function(self)end,HandleMessage=function(self,response)end} end
          package.preload['Net.Msgs.WorldMarchChangeMessage']=function()
            return {HandleMessage=function(self,response)end} end
        """)
        state=vm.globals().state
        if case=='zero': state.currentCount=0;state.currentSupply=0;state.currentTotal=0
        if case=='denied': state.currentCanMarch=False
        if case=='unknown_permission': state.currentCanMarch=None
        if case=='bad_supply': state.currentSupply=171
        if case=='bad_total': state.currentTotal=171
        if case=='changed_at_send': state.changeAtSend=True
        if case=='alliance_contradiction':
            vm.execute('LuaEntry.Player.allianceId=0;LuaEntry.Player.IsInAlliance=function()return true end')
        if case=='target_unknown': state.worldAlliance=None
        mod=vm.execute(SOURCE.read_text(encoding='utf-8'))
        fields=dict(slot='1',authorizedUid='1183884595002296',profileId='profile',
                    launchSessionId='launch',challenge='request',gamePid='7020')
        inventory=mod.Prepare(vm.table_from(dict(fields,mode='inventory')),vm.globals().input_clear)
        require(inventory['state']=='confirmed' and
                inventory['data']['currentSoldiers']['total']==480 and
                inventory['data']['squads']['1']['formationStaffing']['staffingShape']=='current_nested_hero_slots',
                'current native troop diagnostics were reduced to legacy ArmyManager inventory')
        state.formReads=0
        sent=mod.Handle(vm.table_from(dict(fields,action='attack')),vm.globals().input_clear)
        if case=='ready':
            require(sent['state']=='accepted' and state.attackDelegateCount==1 and state.attackSends==1,
                    'source-backed nested170/170 ready formation or explicit unaffiliated owner was rejected')
        else:
            require(sent['state'] in ('failed','unsupported') and state.attackDelegateCount==0 and
                    state.attackSends is None,
                    'unready/malformed/drifted current nested staffing dispatched: '+case)


def main():
    check_original()
    check_installed_managed_nonce_source()
    test_selected_attack_and_diamond()
    test_current_nested_staffing_and_native_membership()
    test_original_world_scene_cursor_projection_is_read_only()
    test_original_hover_monster_and_resource_one_shot_chooser()
    test_attack_final_action_boundary_and_sender_truth()
    test_attack_sender_correlation_negatives()
    test_native_prepare_helper()
    test_manual_native_formation_editor_no_automatic_march()
    test_original_selector_typed_target_preflight_and_dispatch_revalidation()
    test_native_alliance_field_selection_for_typed_targets()
    test_original_reply_observer_required_before_native_controller()
    test_readonly_end_to_end_source_prerequisites()
    state, call, inspect = mock()
    inv = inspect()
    require(inv["state"] == "confirmed" and
            inv["data"]["observation"] == "native_owned_rapid_item_candidates" and
            len(inv["data"]["items"]) == 1 and
            inv["data"]["items"][1]["itemId"] == "3108",
            "native inventory must exclude absent templates and unowned items")
    require(state["rpcCount"] == 0, "inventory introspection dispatched an RPC")
    preflight = call(action="attack")
    require(preflight["state"] in ("failed", "unsupported") and state["rpcCount"] == 0,
            "attack without a verifiable live native hovered target must not dispatch")
    require(call(action="attack", targetUuid="990123", targetType="ATTACK_CITY")["state"] in
            ("failed", "unsupported") and state["rpcCount"] == 0,
            "caller target fields may not substitute a physical native hovered target")
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
                  "challenge": "request", "gamePid": "7020", "authorizedUid": "1183884595002296"}
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
        ("worldOwner", 1183884595002296, "failed"),
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
    state["owner"] = 1183884595002296
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
