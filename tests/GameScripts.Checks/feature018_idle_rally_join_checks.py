"""Source-shaped idle/create vs existing-march join and observer cooperation tests."""
import ast
from feature018_afk_native_checks import ROOT, LuaRuntime

tree=ast.parse((ROOT/'tests/GameScripts.Checks/feature018_afk_native_checks.py').read_text('utf-8'))
main=next(n for n in tree.body if isinstance(n,ast.FunctionDef)and n.name=='main')
setup=next(n.value.args[0].value for n in main.body if isinstance(n,ast.Expr)and isinstance(n.value,ast.Call)
           and isinstance(n.value.func,ast.Attribute)and n.value.func.attr=='execute')


def runtime():
    vm=LuaRuntime(unpack_returned_tuples=True);vm.execute(setup)
    vm.globals().attack=vm.execute((ROOT/'src/LWBridge.GameScripts/feature018_attack_actions.lua').read_text('utf-8'))
    vm.execute('''
      AllianceTeamType={ATTACK_BOSS=9,ATTACK_CITY=10}
      state.war={uuid=700,leaderMarch={uuid=701,ownerUid='2002',ownerName='ally',startId=100101,endTime=150000,
        heroes={1,2,3},status=0},attackAllianceId=7,type=9,targetUuid=501,targetUid=10,memberList={}}
      DataCenter.AllianceWarDataManager={GetAllianceWarDataByUuid=function(self,uuid)if uuid==700 then return state.war end end,
        CheckJoinAllianceWarByWarData=function()return state.joinable~=false end}
      MarchUtil.OnJoinRally=function()error('Idle formations cannot be passed as march UUIDs')end
      MarchUtil.TryStartMarch=function(formation,kind,energy,isFormation,uuid,point,back,a,b,server)
        assert(formation==101 and kind==4 and isFormation==true and uuid==700 and point==100101 and back==1 and server==123)
        if state.drift then LuaEntry.Player.uid='other'end
        if state.fallback then SFSNetwork.SendMessage(100,formation,kind,uuid);return end
        SFSNetwork.SendMessage(99,formation,kind,uuid,'100100;100101')
      end
      rally=attack.AutomationRallyInfo('700',true);assert(rally)
      function joinedMarch(target,owner,formation,guid)
        state.march={uuid=605,ownerUid=owner or '1001',ownerFormationUuid=formation or 101,targetUuid=target or 700,serverId=123,
          GetMarchTargetType=function()return 4 end,clientCreateGuid=guid or '12345678-1234-4abc-8def-000000000001'}
      end
      function cooperate(object,key)
        local old=object[key];local wrapper=function(...)return old(...)end;object[key]=wrapper
        assert(attack.RegisterObserverLink(object,key,wrapper,old));return wrapper
      end
    ''')
    return vm


def main():
    vm=runtime();vm.execute('''
      assert(attack.AutomationJoinIdle(fields,input,rally).data.requestDispatched and state.sends==1)
      assert(attack.AutomationObserve(fields,input).state=='accepted')
      joinedMarch(999);assert(attack.AutomationObserve(fields,input).state~='confirmed')
      joinedMarch(700,'other');assert(attack.AutomationObserve(fields,input).state~='confirmed')
      joinedMarch(700,'1001',999);assert(attack.AutomationObserve(fields,input).state~='confirmed')
      joinedMarch(700,'1001',101,'wrong');assert(attack.AutomationObserve(fields,input).state~='confirmed')
      joinedMarch();assert(attack.AutomationObserve(fields,input).state=='confirmed'and state.sends==1)
      state.march=nil
      cooperate(SFSNetwork,'SendMessage')
      cooperate(require('Net.Msgs.WorldMarchFormationNewMessage'),'OnCreate')
      cooperate(require('Net.Msgs.WorldMarchFormationNewMessage'),'HandleMessage')
      assert(attack.AutomationJoinIdle(fields,input,rally).data.requestDispatched and state.sends==2,
        'Known gathering observer chain must preserve attack observers and exact nonce capture')
      joinedMarch();assert(attack.AutomationObserve(fields,input).state=='confirmed')
      state.march=nil;SFSNetwork.SendMessage=function()error('unknown replacement')end
      assert(attack.AutomationJoinIdle(fields,input,rally).state~='accepted'and state.sends==2,
        'An unknown replacement still refuses before dispatch')
    ''')
    for mode in ['drift','fallback','busy','notJoinable']:
        vm=runtime();vm.globals().mode=mode;vm.execute('''
          if mode=='busy'then joinedMarch()
          elseif mode=='notJoinable'then state.joinable=false
          else state[mode]=true end
          local result=attack.AutomationJoinIdle(fields,input,rally)
          assert(result.state~='accepted'and state.sends==0 and state.changed==0,'Unsafe idle join: '..mode)
        ''')
    vm=runtime();vm.execute('''
      assert(attack.AutomationJoinIdle(fields,input,rally).data.requestDispatched)
      local again=attack.AutomationJoinIdle(fields,input,rally)
      assert(not(again.data and again.data.requestDispatched)and state.sends==1,'An unconfirmed join cannot be sent twice')
    ''')
    print('IDLE_RALLY_JOIN_CHECKS_PASS create-not-change exact-GUID-target-owner-formation no-replay final-wire-fences known-observer-cooperation unknown-replacement-refusal')


if __name__=='__main__':main()
