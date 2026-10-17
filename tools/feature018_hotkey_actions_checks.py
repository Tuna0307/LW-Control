"""Current-v26 Hotkeys source-path unit checks in an inert Lua 5.3 VM.

No installed game, account, launcher, updater, UI session, network or journal
is started or modified. State objects below are isolated fake game managers.
"""

import hashlib
import sys
from pathlib import Path

import feature018_hotkey_shield_checks as shield_checks

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "artifacts/map-013-lua-site"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parent / "map_009_offline"))
from lupa.lua53 import LuaRuntime
import run_live_resource_probe as original_probe
import compact_lua53 as original_bytecode
from inspect_current import root_methods

SOURCE = Path(__file__).with_name("feature018_hotkey_actions.lua")
ARCHIVED_V26 = SOURCE.parents[1] / "artifacts/feature018/offline-candidate-continuation/LWScripts.data"
ORIGINAL_V26_LUA_ENTRY_SHA = (
    "50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137"
)

GAME = """
state = {
    playerUid='3133546747002289', focusClear=true, canReturn=true,
    ownerUid='3133546747002289', formationId=1001, candidateFormationId=1001,
    squadUuid=803001, candidateUid='3133546747002289', target=17, dispatches=0,
    slot=1, numberOfOwnedShield=2, numberOfOwnedShield12=1,
    numberOfOwnedShield24=1, fallbackCalls=0, inventoryReads=0,
    itemUseCalls=0,
    sourceServer=2289, selfServer=2289, currentServer=2289,
}
LuaEntry = { Player={uid=state.playerUid,allianceId=700,
    GetSourceServerId=function() return state.sourceServer end,
    GetSelfServerId=function() return state.selfServer end,
    GetCurServerId=function() return state.currentServer end,
}, DataConfig={
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
            assert(type(uid)=='string' and extra==700)
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
            state.inventoryReads=state.inventoryReads+1
            return {
                {itemId=8,uuid=555001,count=state.numberOfOwnedShield},
                {itemId=12,uuid=555002,count=state.numberOfOwnedShield12},
                {itemId=24,uuid=555004,count=state.numberOfOwnedShield24},
                {itemId=99,uuid=555003,count=4},
            }
        end
    },
    ItemTemplateManager={
        GetTypeListByTypes=function(self,t,sub)
            assert(t==4 and sub==1)
            return {{id=8,para1=801,type=4,type2=1},
                    {id=12,para1=802,type=4,type2=1},
                    {id=24,para1=803,type=4,type2=1}}
        end
    },
    StatusManager={GetShieldStatusIds=function(self)
        return {[801]=true,[802]=true,[803]=true}
    end},
}
TableName={StatusTab='StatusTab'}
LocalController={instance=function()
  return {getLine=function(self,name,id)
    assert(name=='StatusTab')
    local seconds={[801]=28800,[802]=43200,[803]=86400}
    return {time=seconds[id]}
  end}
end}
MarchUtil={OnBackHome=function(uuid)
    assert(uuid==state.squadUuid)
    state.dispatches=state.dispatches+1
end}
frozen={Handle=function(fields)
    state.fallbackCalls=state.fallbackCalls+1
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
    def call(action="recall", slot="1", profile="profile", challenge="challenge",
                 query_only=False):
        fields = {
            "action": action, "slot": slot,
            "profileId": profile, "launchSessionId": "launch",
            "challenge": challenge, "gamePid": "7020",
        }
        if query_only:
            fields["queryOnly"] = "1"
        return wrapped(lua.table_from(fields))
    return lua, module, wrapped, state, call


def test_registered_shield_inventory():
    lua, actions, _, state, _ = scenario()
    lua.execute('''
    SFSNetwork={SendMessage=function()
      state.itemUseCalls=state.itemUseCalls+1
      error('read-only shield inventory sent ItemUse')
    end}
    feature={handlers={}, RegisterHandler=function(command, handler)
      assert(command=='feature018_shield_inventory_get')
      if feature.handlers[command]~=nil then return false end
      feature.handlers[command]=handler
      return true
    end, Array=function(values)
      assert(type(values)=='table' and #values==0)
      return setmetatable(values,{__feature018_array=true})
    end}
    ''')
    feature = lua.globals().feature
    require(actions.RegisterShieldInventory(feature) is True,
            "read-only shield inventory native handler did not register")
    require(actions.RegisterShieldInventory(feature) is False,
            "duplicate shield inventory registration overwrote handler")
    handler = feature.handlers["feature018_shield_inventory_get"]
    require(handler is not None, "registered native shield handler missing")

    def call(**fields):
        return handler(lua.table_from(fields))

    require(handler(None)["state"] == "failed" and state["inventoryReads"] == 0,
            "malformed native inventory request accessed game state")
    for malformed_uid in (None, "invalid", "3133546747002212", "31335467470022890"):
        lua.globals().LuaEntry.Player.uid = malformed_uid
        bad = call(authorizedUid="3133546747002289")
        require(bad["state"] == "failed" and state["inventoryReads"] == 0 and
                state["itemUseCalls"] == 0,
                "wrong/malformed native UID reached shield stock reader")
    lua.globals().LuaEntry.Player.uid = "3133546747002289"
    for getter in ("sourceServer", "selfServer", "currentServer"):
        for invalid in (2212, 2213, None, "not-a-server", "02289", "2289.0", 2289.5):
            state[getter] = invalid
            denied = call(authorizedUid="3133546747002289")
            require(denied["state"] == "failed" and
                    state["inventoryReads"] == 0 and state["itemUseCalls"] == 0,
                    "invalid native server getter accessed owned shield stock: " + getter)
        state[getter] = 2289

    # Client fields cannot select an item or authorize a spend. Data comes
    # exclusively from v26 native ItemData, Goods templates and StatusTab.
    good = call(authorizedUid="3133546747002212", slot="999", itemId="99999",
                itemOptIn="1", action="ItemUse")
    require(good["state"] == "confirmed" and
            good["data"]["readOnly"] is True and
            good["data"]["requestDispatched"] is False and
            good["data"]["durationMappingVerified"] is True and
            state["itemUseCalls"] == 0 and state["dispatches"] == 0 and
            state["fallbackCalls"] == 0,
            "registered read-only inventory accepted an action or forged owner")
    shield_items = good["data"]["items"]
    require(len(shield_items) == 3 and
            [shield_items[i]["durationHours"] for i in (1,2,3)] == [8,12,24] and
            [shield_items[i]["statusId"] for i in (1,2,3)] == ["801","802","803"] and
            [shield_items[i]["itemId"] for i in (1,2,3)] == ["8","12","24"] and
            [shield_items[i]["uuid"] for i in (1,2,3)] == ["555001","555002","555004"] and
            [shield_items[i]["count"] for i in (1,2,3)] == [2,1,1] and
            all(shield_items[i]["uniqueDuration"] is True for i in (1,2,3)),
            "registered native inventory did not classify exact 8/12/24h owned shields")

    state["numberOfOwnedShield"] = 0
    state["numberOfOwnedShield12"] = 0
    state["numberOfOwnedShield24"] = 0
    empty = call()
    require(empty["state"] == "confirmed" and len(empty["data"]["items"]) == 0 and
            lua.eval("function(t) return getmetatable(t).__feature018_array end")(
                empty["data"]["items"]) is True and
            state["itemUseCalls"] == 0,
            "no-stock native shield inventory was not marked as JSON []")
    original_marker = feature.Array
    feature.Array = lua.eval("function(values) return values end")
    require(call()["state"] == "unsupported",
            "unmarked empty item table could be serialized as JSON object")
    feature.Array = lua.eval("function(values) error('tagging failed') end")
    require(call()["state"] == "unsupported",
            "array encoder failure escaped native handler instead of failing closed")
    feature.Array = original_marker
    lua.execute("DataCenter.StatusManager.GetShieldStatusIds=function() return {} end")
    require(call()["state"] == "unsupported" and state["itemUseCalls"] == 0,
            "missing source status IDs were represented as confirmed empty inventory")
    require(actions.RegisterShieldInventory(lua.table_from({})) is False,
            "missing native register and array methods accepted")
    require(actions.RegisterShieldInventory(lua.table_from({
        "RegisterHandler": feature.RegisterHandler,
    })) is False, "missing JSON array marker accepted")


def test_original_v26_item_use_ack_limits():
    """The native success response updates inventory/status but has no client request nonce."""
    fv, cv, records = original_probe.read_lwlf(ARCHIVED_V26)
    require((fv, cv) == (3, 26), "archived item-use source was not current v26")
    entries = dict(records)
    require(hashlib.sha256(entries[original_probe.ORIGINAL_LUA_ENTRY]).hexdigest() ==
            ORIGINAL_V26_LUA_ENTRY_SHA, "v26 bytecode identity changed")

    def method(module, function):
        root = original_bytecode.parse_chunk(original_probe.decode_lenc(entries[module]))
        path = root_methods(root)[function]
        for child in path.split("/")[1:]:
            root = root["children"][int(child)]
        return root

    on_create = method("Net/Msgs/ItemUseMessage.luac", "OnCreate")
    require({"uuid", "num", "PutUtfString", "PutInt"}.issubset(
                set(on_create["constants"])),
            "original ItemUse sender lost item UUID / quantity serialization")
    response = method("Net/Msgs/ItemUseMessage.luac", "HandleMessage")
    require({"ItemUseHandle", "errorCode"}.issubset(set(response["constants"])),
            "original ItemUse response no longer delegates to inventory update")
    require("requestId" not in response["constants"] and
            "challenge" not in response["constants"],
            "ItemUse response gained a request correlation that must be reviewed")
    item_manager = method("DataCenter/ItemManager/ItemManager.luac", "ItemUseHandle")
    require({"errorCode", "itemId", "uuid", "UpdateOneItem", "OnUseRet"}.issubset(
                set(item_manager["constants"])),
            "original ItemUse inventory/status observer contract changed")
    observed_status = method("DataCenter/ItemData/ItemData.luac", "OnUseRet")
    require({"itemEffectObj", "status", "effectState", "AddStatus"}.issubset(
                set(observed_status["constants"])),
            "original ItemUse response stopped applying native status effects")
    # An uncorrelated generic response must never become an invented per-request
    # ACK. The already implemented frozen shieldUse only reports confirmed on
    # independently observed stock decrement plus matching MAIN_SHIELD effect.


def test_wrapped_original_shield_sender():
    """Integrate this owner gate with the actual v26-shape ItemUse producer."""
    lua, frozen, state = shield_checks.run()
    lua.execute('''
    liveAccount={uid='3133546747002212',source=2212,selfServer=2212,cur=2212}
    LuaEntry={Player={
      uid=liveAccount.uid,
      GetSourceServerId=function(self) return liveAccount.source end,
      GetSelfServerId=function(self) return liveAccount.selfServer end,
      GetCurServerId=function(self) return liveAccount.cur end,
    }}
    ''')
    actions = lua.execute(SOURCE.read_text(encoding="utf-8"))
    wrapped = actions.Wrap(frozen)
    account = lua.globals().liveAccount

    def call(*, status=False):
        fields = {
            "action": "shieldUse", "slot": "8", "profileId": "profile",
            "launchSessionId": "launch", "challenge": "challenge", "gamePid": "7020",
        }
        if status:
            fields["queryOnly"] = "1"
        return wrapped(lua.table_from(fields))

    require(call()["state"] == "failed" and state["sent"] == 0 and
            state["cooldownSets"] == 0,
            "main 2212 account reached a real ItemUse sender")
    account["uid"] = "3133546747002289"
    lua.globals().LuaEntry.Player.uid = account["uid"]
    for key in ("source", "selfServer", "cur"):
        require(call()["state"] == "failed" and state["sent"] == 0,
                "2289 UID with stale main-account server was admitted")
        account[key] = 2289
    sent = call()
    require(sent["state"] == "accepted" and
            sent["data"]["requestDispatched"] is True and
            state["sent"] == 1 and state["cooldownSets"] == 1,
            "proper 2289 owner was blocked from the original source-proven ItemUse")
    account["cur"] = 2212
    require(call(status=True)["state"] == "failed" and state["sent"] == 1,
            "server switched before pending shield observation")
    account["cur"] = 2289
    require(call(status=True)["state"] == "accepted" and state["sent"] == 1,
            "unconfirmed ItemUse query initiated a duplicate send")
    state["items"][1]["count"] = 1
    state["effectEnd"][501] = state["serverTime"] + 8 * 3600 * 1000
    confirmed = call(status=True)
    require(confirmed["state"] == "confirmed" and
            confirmed["data"]["observation"] ==
            "native_owned_shield_consumption_and_MAIN_SHIELD" and state["sent"] == 1,
            "exact game-owned consumption/effect poststate could not confirm")


def test_registered_relocation_readonly():
    lua, actions, _, state, _ = scenario()
    lua.execute('''
      state.relocationReads=0
      LWBridgeFeature018RelocationActions={
        Readiness=function(fields,clear,action)
          state.relocationReads=state.relocationReads+1
          assert(fields.authorizedUid=='3133546747002289')
          assert(action=='randomRelocate' or action=='allianceRelocate')
          return {state='confirmed',data={
            action=action,ownerUid=fields.authorizedUid,readOnly=true,
            requestDispatched=false,relocationConfirmed=false,
            observation='native_relocation_readonly_preflight',
          }}
        end}
      feature={handlers={}, RegisterHandler=function(command, handler)
        if feature.handlers[command]~=nil then return false end
        feature.handlers[command]=handler
        return true
      end}
    ''')
    feature=lua.globals().feature
    require(actions.RegisterRelocation(feature,lua.globals().frozen) is True,
            "original F9/F10 safe preflight handler missing")
    require(actions.RegisterRelocation(feature,lua.globals().frozen) is False,
            "duplicate relocation preflight registration replaced owner handler")
    handler=feature.handlers["feature018_relocation_preflight"]
    require(handler(lua.table_from({"action":"unknown"}))["state"]=="failed",
            "untrusted browser action reached relocation readiness")
    for expected_count,action in enumerate(("randomRelocate","allianceRelocate"),start=1):
        result=handler(lua.table_from({"action":action,"authorizedUid":"3133546747002212"}))
        require(result["state"]=="confirmed" and
                result["data"]["requestDispatched"] is False and
                result["data"]["relocationConfirmed"] is False and
                state["dispatches"]==0 and state["relocationReads"]==expected_count,
                "read-only relocation handler allowed spoofed owner or an actual move")
    state["sourceServer"]=2212
    require(handler(lua.table_from({"action":"randomRelocate"}))["state"]=="failed" and
            state["relocationReads"]==2,
            "main server reached original relocation preflight")
    state["sourceServer"]=2289
    lua.execute("LWBridgeFeature018RelocationActions.Readiness=function() return "
                "{state='confirmed',data={action='randomRelocate',ownerUid='3133546747002289',"
                "readOnly=true,requestDispatched=true,relocationConfirmed=true}} end")
    require(handler(lua.table_from({"action":"randomRelocate"}))["state"]=="failed",
            "native preflight forged a relocation dispatch or confirmation")

def test_consent_bound_source_frontline_action():
    lua, actions, _, state, _ = scenario()
    lua.execute('''
      state.frontlineCalls=0
      LWBridgeFeature018MiniGame={
        FrontlineStatus=function()
          return {state='confirmed',data={
            observation='native_frontline_sunday_battle_readonly',
            actionEligible=true,uncertainPriorAction=false,
            requestDispatched=false,serverAckObserved=false}}
        end,
        FrontlineReinforce=function(fields)
          state.frontlineCalls=state.frontlineCalls+1
          assert(fields.action=='frontlineReinforce' and fields.slot=='5')
          return {state='confirmed',data={
            observation='native_ParkourBattleLogic_GetMemberCount',
            action='frontlineReinforce',slot=5,beforeCount=7,afterCount=12,
            addedMembers=5,observedCreatedUnits=5,unitIdentityVerified=true,
            observationScope='native_local_battle',serverAckObserved=false
          }}
        end
      }
      frontlineFeature={handlers={},RegisterHandler=function(name,callback)
        if frontlineFeature.handlers[name]~=nil then return false end
        frontlineFeature.handlers[name]=callback
        return true
      end}
    ''')
    feature=lua.globals().frontlineFeature
    require(actions.RegisterFrontline(feature,lua.globals().frozen) is True and
            actions.RegisterFrontline(feature,lua.globals().frozen) is False,
            "Original Frontline native local producer not registered uniquely")
    call=feature.handlers["feature018_frontline_action"]
    def request(**changes):
        return call(lua.table_from({"action":"frontlineReinforce","slot":"5",
            "profileId":"profile","launchSessionId":"launch",
            "challenge":"challenge","gamePid":"7020",**changes}))
    for opt in (None,"0",True):
        require(request(frontlineOptIn=opt)["state"]=="failed" and
                state["frontlineCalls"]==0,
                "Frontline local game creation was admitted without fresh consent")
    state["sourceServer"]=2212
    require(request(frontlineOptIn="1")["state"]=="failed" and
            state["frontlineCalls"]==0,
            "Main server2212 was admitted to mutate a Frontline battle")
    state["sourceServer"]=2289
    state["focusClear"]=False
    require(request(frontlineOptIn="1")["state"]=="failed" and
            state["frontlineCalls"]==0,
            "Active game editor admitted original Parkour soldier production")
    state["focusClear"]=True
    result=request(frontlineOptIn="1")
    require(result["state"]=="confirmed" and
            result["data"]["observationScope"]=="native_local_battle" and
            result["data"]["serverAckObserved"] is False and
            state["frontlineCalls"]==1 and state["dispatches"]==0,
            "Original local Frontline five distinct soldiers were mislabeled as server scoring")
    require(request(frontlineOptIn="1")["state"]=="failed" and
            state["frontlineCalls"]==1,
            "Native UI remount/another fresh consent repeated an earlier original five-unit action")
    lua.execute('''LWBridgeFeature018MiniGame.FrontlineReinforce=function(fields)
      state.frontlineCalls=state.frontlineCalls+1
      return {state='confirmed',data={observation='native_ParkourBattleLogic_GetMemberCount',
        action='frontlineReinforce',slot=5,beforeCount=7,afterCount=12,
        addedMembers=5,observedCreatedUnits=5,unitIdentityVerified=true,
        observationScope='native_local_battle',serverAckObserved=true}}
    end''')
    require(request(frontlineOptIn="1",launchSessionId="newlaunch")["state"]=="failed" and
            state["frontlineCalls"]==2,
            "Forged local soldier response was promoted to invented server acknowledgement")


def test_original_recall_uncertain_native_send_is_one_intent_across_challenge_rotation():
    lua,_,_,state,call=scenario()
    lua.execute("""
        MarchUtil.OnBackHome=function(uuid)
          assert(uuid==state.squadUuid)
          state.dispatches=state.dispatches+1
          error('original method threw after native send')
        end
    """)
    first=call()
    require(first['state']=='failed' and state['dispatches']==1 and
            'uncertain' in first['error'],
            'throwing native recall did not preserve an uncertain one-send fence')
    for challenge in ('challenge','rotatedChallenge'):
        repeat=call(challenge=challenge)
        require(repeat['state']=='accepted' and
                repeat['data']['requestDispatched'] is False and
                state['dispatches']==1,
                'new challenge replayed uncertain original recall sender')
    state['target']=77
    post=call(challenge='rotatedChallenge',query_only=True)
    require(post['state']=='confirmed' and
            post['data']['nativeReplyCausalityConfirmed'] is False and
            post['data']['priorDispatchOutcomeUncertain'] is True and
            post['data']['observedAfterPreviousDispatch'] is False and
            state['dispatches']==1,
            'uncertain native send was misreported as an acknowledged dispatch')

    lua,_,_,state,call=scenario()
    lua.execute("""
        MarchUtil.OnBackHome=function(uuid)
          state.dispatches=state.dispatches+1
          return false
        end
    """)
    require(call()['state']=='failed' and state['dispatches']==1,
            'original return false was treated as proven request dispatch')
    require(call(challenge='newChallenge')['state']=='accepted' and
            state['dispatches']==1,
            'false native return retried a potentially sent recall')

    lua,_,_,state,call=scenario()
    require(call()['state']=='accepted' and state['dispatches']==1,
            'baseline return sender did not dispatch')
    require(call(challenge='newChallenge')['state']=='accepted' and
            state['dispatches']==1,
            'challenge rotation sent a second recall against the same march')
    lua.execute("""
        local original=DataCenter.WorldMarchDataManager
        DataCenter.WorldMarchDataManager=setmetatable({}, {__index=original})
    """)
    result=call(challenge='newChallenge',query_only=True)
    require(result['state']=='failed' and 'manager' in result['error'] and
            state['dispatches']==1,
            'new native march manager reused an earlier session return receipt')

    for change in ('server','manager'):
        lua,actions,_,state,_=scenario()
        lua.execute("""
            local reads=0
            frozen.TextInputClear=function()
              reads=reads+1
              if reads==2 then
                if '%s'=='server' then
                  state.currentServer=2212
                else
                  local original=DataCenter.WorldMarchDataManager
                  DataCenter.WorldMarchDataManager=setmetatable({}, {__index=original})
                end
              end
              return true
            end
        """ % change)
        wrapped=actions.Wrap(lua.globals().frozen)
        fields=lua.table_from({'action':'recall','slot':'1',
            'profileId':'profile','launchSessionId':'launch',
            'challenge':'challenge','gamePid':'7020'})
        result=wrapped(fields)
        require(result['state']=='failed' and state['dispatches']==0,
                'native '+change+' changed in late focus callback but recall was sent')


def main():
    test_original_recall_uncertain_native_send_is_one_intent_across_challenge_rotation()
    test_consent_bound_source_frontline_action()
    test_registered_relocation_readonly()
    test_original_v26_item_use_ack_limits()
    test_registered_shield_inventory()
    test_wrapped_original_shield_sender()
    lua, actions, _, state, call = scenario()
    first = call()
    require(first["state"] == "accepted" and first["data"]["requestDispatched"] is True,
            "source-verified owned return did not report send-only")
    require(state["dispatches"] == 1, "native UI-equivalent return not invoked exactly once")
    repeat = call(query_only=True)
    require(repeat["state"] == "accepted" and repeat["data"]["requestDispatched"] is False,
            "pending return was resent before receiving authoritative state")
    require(state["dispatches"] == 1, "duplicate return dispatch")
    state["target"] = 77
    confirmed = call(query_only=True)
    require(confirmed["state"] == "confirmed" and
            confirmed["data"]["observation"] == "native_world_march_return_target" and
            confirmed["data"]["nativePostStateObserved"] is True and
            confirmed["data"]["observedAfterPreviousDispatch"] is True and
            confirmed["data"]["priorTarget"] == 17 and
            confirmed["data"]["originalResponseObserved"] is False and
            confirmed["data"]["nativeReplyCausalityConfirmed"] is False and
            confirmed["data"]["serverAcknowledgementCorrelated"] is False and
            confirmed["data"]["requestDispatched"] is False,
            "cached post-send native return was promoted to uniquely correlated server ACK")
    require(state["dispatches"] == 1, "poststate polling created another request")
    require(call(query_only=True)["state"] == "failed" and state["dispatches"] == 1,
            "recall query-only started or confirmed without matching pending send")
    confirmed_again = call()
    require(confirmed_again["state"] == "confirmed" and
            confirmed_again["data"]["observation"] ==
            "native_world_march_already_returning_readonly" and
            confirmed_again["data"]["observedAfterPreviousDispatch"] is False and
            confirmed_again["data"]["requestDispatched"] is False and
            confirmed_again["data"]["actionPerformed"] is False and
            state["dispatches"] == 1,
            "already-returning march was resent or counted as this hotkey's causal action")

    _, _, _, s, call = scenario()
    require(call(query_only=True)["state"] == "failed" and s["dispatches"] == 0,
            "query-only triggered a previously unsent native recall")
    s["candidateUid"] = "foreignUid"
    require(call()["state"] == "unsupported" and s["dispatches"] == 0,
            "foreign owner march admitted after misleading native GetOwnerFormationMarch")
    s["candidateUid"] = "3133546747002289"
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

    lua_second, _, _, s, call = scenario()
    for slot in ("1","2","3","4"):
        outcome = call(action="attack",slot=slot)
        require(outcome["state"] == "unsupported" and s["dispatches"] == 0,
                "cursor attack dispatched without typed game cursor target: " + slot)
    lua_second.globals().LuaEntry.Player.uid = "222"
    for duration in ("8", "12", "24"):
        outcome = call(action="shieldUse",slot=duration)
        require(outcome["state"] == "failed" and "UID" in outcome["error"] and
                s["dispatches"] == 0,
                "main/unknown game owner reached mutable shield handler: " + duration)
    s["playerUid"] = "3133546747002289"
    lua_second.globals().LuaEntry.Player.uid = s["playerUid"]
    s["sourceServer"] = 2212
    require(call(action="shieldUse",slot="8")["state"] == "failed" and
            call(action="shieldUse",slot="8",query_only=True)["state"] == "failed" and
            s["dispatches"] == 0 and s["fallbackCalls"] == 0,
            "wrong-server owner reached shield sender")
    s["sourceServer"] = 2289
    for duration in ("8", "12", "24"):
        outcome = call(action="shieldUse",slot=duration)
        require(outcome["state"] == "unsupported" and s["dispatches"] == 0 and
                s["fallbackCalls"] == {"8":1,"12":2,"24":3}[duration],
                "shield item consumed without source-mapped duration: " + duration)
    require(call(action="shieldUse",slot="99")["state"] == "failed",
            "unexpected shield duration accepted")
    require(call(action="frontlineReinforce",slot="5")["state"] == "unsupported"
            and s["dispatches"] == 0,
            "Frontline shortcut claimed unsupported native battle as working")
    lua_second.execute('''LWBridgeFeature018MiniGame = { FrontlineReinforce = function(fields)
        assert(fields.slot == '5' and fields.action == 'frontlineReinforce')
        return {state='confirmed',data={
          observation='native_ParkourBattleLogic_GetMemberCount',
          beforeCount=20,afterCount=25,addedMembers=5,
          slot=5,action='frontlineReinforce'}}
    end }''')
    require(call(action="frontlineReinforce",slot="5")["state"] == "confirmed"
            and s["dispatches"] == 0,
            "authenticated native +5 producer was not routed to the hotkey")
    s["focusClear"] = False
    require(call(action="frontlineReinforce",slot="5")["state"] == "failed"
            and s["dispatches"] == 0,
            "Frontline native action bypassed text-input gate")
    s["focusClear"] = True
    require(call(action="equipment",slot="1")["state"] == "failed",
            "equipment without trusted persisted presets reached a mutable sender")

    state["playerUid"] = "3133546747002289"
    lua.globals().LuaEntry.Player.uid = state["playerUid"]
    shield = actions.ShieldOwnedInventory()
    require(shield["state"] == "confirmed", "owned shield inventory unavailable")
    require(shield["data"]["durationMappingVerified"] is True and
            shield["data"]["readOnly"] is True and
            shield["data"]["requestDispatched"] is False,
            "read-only source duration provenance missing")
    items = shield["data"]["items"]
    require(len(items) == 3 and
            [items[i]["durationHours"] for i in (1,2,3)] == [8,12,24] and
            items[1]["itemId"] == "8" and items[1]["statusId"] == "801" and
            items[1]["uuid"] == "555001" and items[1]["count"] == 2 and
            items[3]["uuid"] == "555004" and
            all(items[i]["uniqueDuration"] is True for i in (1,2,3)),
            "native owned shields did not join exact StatusTab hours and status IDs")
    state["numberOfOwnedShield"] = 0
    require(len(actions.ShieldOwnedInventory()["data"]["items"]) == 2,
            "out-of-stock shield item listed or other owned durations lost")
    lua.globals().DataCenter.ItemTemplateManager = None
    require(actions.ShieldOwnedInventory()["state"] == "unsupported",
            "missing native shield template source admitted")
    lua, actions, _, state, call = scenario()
    state["playerUid"] = "3133546747002289"
    lua.globals().LuaEntry.Player.uid = state["playerUid"]
    lua.execute("DataCenter.ItemTemplateManager.GetTypeListByTypes=function() "
                "return {{id=8,para1=801},{id=12,para1=801},{id=24,para1=803}} end")
    duplicate = actions.ShieldOwnedInventory()
    require(duplicate["state"] == "unsupported" or
            (duplicate["state"] == "confirmed" and
             duplicate["data"]["items"][1]["uniqueDuration"] is False),
            "duplicate matching duration was incorrectly called a unique usable item")
    lua.execute("LocalController.instance=function() "
                "return {getLine=function() return {time=-1} end} end")
    require(actions.ShieldOwnedInventory()["state"] == "unsupported",
            "invalid original StatusTab hours were accepted")
    lua, actions, _, state, call = scenario()
    lua.globals().LuaEntry.Player.uid = "222"
    require(actions.ShieldOwnedInventory()["state"] == "failed",
            "main-account shield inventory disclosed without native UID admission")
    state["playerUid"] = "3133546747002289"
    lua.globals().LuaEntry.Player.uid = state["playerUid"]
    for server in ("sourceServer", "selfServer", "currentServer"):
        state[server] = 2212
        require(actions.ShieldOwnedInventory()["state"] == "failed",
                "shield inventory read through unapproved native server: " + server)
        state[server] = 2289
    lua.execute("DataCenter.StatusManager.GetShieldStatusIds=function() return {} end")
    require(actions.ShieldOwnedInventory()["state"] == "unsupported",
            "missing MAIN_SHIELD status classification was accepted")
    print("FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS")


if __name__ == "__main__":
    main()
