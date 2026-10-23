"""Inert current-client AFK adapter simulation, including real attack adapter.

These Lua tables are protocol-shaped fixtures, never live proof. Native create
and retarget sends must wait for independently advanced server post-state.
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


def main():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute('''
      state={uid='1001',server=123,now=100,sends=0,changed=0,stamina=100,form=101}
      os.time=function()return state.now end
      LuaEntry={Player={uid='1001',allianceId=7,
        GetSourceServerId=function()return state.server end,GetSelfServerId=function()return state.server end,
        GetCurServerId=function()return state.server end,GetMainWorldPos=function()return 100100 end}}
      MarchTargetType={ATTACK_MONSTER=1,BACK_HOME=3,STATE=0,JOIN_RALLY=4,RALLY_FOR_BOSS=5}
      MarchAutoBackType={Back=1,NoBack=0};NewMarchType={MONSTER=7,PLAYER=1,BOSS=8}
      ForceChangeScene={World=3};SceneUtils={WorldToTileIndex=function()return 100105 end}
      local target={uuid=501,monsterId=10,GetMarchType=function()return state.playerTarget and 1 or 7 end,
        IsMonsterOrBoss=function()return not state.playerTarget end}
      local formation={uuid=101,ownerUid='1001',index=1,heroes={99},soldiers={[1]=100},
        ConscriptSoldier=function()end}
      state.target=target;state.formation=formation
      CS={SceneManager={World={GetMarch=function(self,uuid)if uuid==501 then return target end;return state.march end,
        GetTroop=function()return {GetPosition=function()return {x=105,z=100}end}end}}}
      DataCenter={ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,slot)if slot==1 then return formation end end,
        GetCurStaminaByUuid=function()return state.stamina end},
        MonsterTemplateManager={GetMonsterTemplate=function(self,id)if id~=10 then return nil end;return {level=10,special=state.special or 0,type=2,name='zombie'}end,
          GetMonsterMaxLevelbyType=function()return 30 end},
        WorldMarchDataManager={GetOwnerFormationMarch=function()return state.march end,
          GetMarch=function(self,uuid)if state.march and state.march.uuid==uuid then return state.march end end,
          GetAllMarches=function()return {target}end}}
      MsgDefines={WorldMarchFormationNew=99,WorldMarchChange=100}
      package.preload['Net.Msgs.WorldMarchFormationNewMessage']=function()return {
        OnCreate=function(self)
          self.sfsObj={GetUtfString=function()return '12345678-1234-4abc-8def-000000000001'end}
        end,HandleMessage=function()end}end
      package.preload['Net.Msgs.WorldMarchChangeMessage']=function()return {HandleMessage=function()end}end
      SFSNetwork={SendMessage=function(route,formation,kind,target)
        if route==99 then
          state.sends=state.sends+1;require('Net.Msgs.WorldMarchFormationNewMessage').OnCreate({},formation,kind,target)
        elseif route==100 then state.changed=state.changed+1
        elseif route==101 then state.finds=(state.finds or 0)+1
        elseif route==102 then
          assert(formation.uuid=='stamina-owned' and formation.num==1)
          state.itemUses=state.itemUses+1;state.item.count=state.item.count-1;state.stamina=state.stamina+50
        else error('unexpected native route')end
      end}
      MarchUtil={GetCostStaminaByTargetType=function()return 10 end,
        OnAttackMonster=function(formation,t,stamina,isFormation,auto)
          assert(t==target and auto==0)
          SFSNetwork.SendMessage(isFormation and 99 or 100,formation,1,t.uuid,'100100;100105')
        end}
      package.preload['DataCenter.Global.LWBridgeFeature018HotkeyGame']=function()return {TextInputClear=function()return true end}end
      fields={slot='1',authorizedUid='1001',profileId='profile',launchSessionId='session',challenge='challenge',gamePid='123'}
      input=function()return true end
    ''')
    attack = vm.execute((ROOT / 'tools/feature018_attack_actions.lua').read_text(encoding='utf-8'))
    vm.globals().attack = attack
    vm.execute('''
      local info=attack.AutomationMonsterInfo('501');assert(info.monster and not info.player and info.level==10)
      state.playerTarget=true;assert(attack.AutomationMonsterInfo('501')==nil)
      assert(attack.AutomationSend(fields,input,info,true).state=='failed' and state.sends==0)
      state.playerTarget=false;state.special=8
      assert(attack.AutomationSend(fields,input,info,true).state=='failed' and state.sends==0)
      state.special=0;LuaEntry.Player.uid='other'
      assert(attack.AutomationSend(fields,input,info,true).state=='failed' and state.sends==0)
      LuaEntry.Player.uid='1001'
      assert(attack.AutomationSend(fields,input,info,true).data.requestDispatched and state.sends==1)
      assert(attack.AutomationObserve(fields,input).state=='accepted','A send is not an acknowledgement')
      state.march={uuid=601,ownerUid='1001',ownerFormationUuid=101,targetUuid=501,startTime=100,
        GetMarchTargetType=function()return state.kind or 1 end,
        clientCreateGuid='wrong-guid'}
      assert(attack.AutomationObserve(fields,input).state=='accepted')
      state.march.clientCreateGuid='12345678-1234-4abc-8def-000000000001'
      local r=attack.AutomationObserve(fields,input);assert(r.state=='confirmed' and state.sends==1, r.error or (r.data and r.data.nativeTransaction and r.data.nativeTransaction.nonceStatus) or r.state)
      state.kind=0
      local cont=attack.AutomationContinue(fields,input,info,'601')
      assert(cont.data.requestDispatched and state.changed==1)
      assert(attack.AutomationObserve(fields,input).state=='accepted')
      state.kind=1;assert(attack.AutomationObserve(fields,input).state=='accepted','Old startTime must not confirm a retarget')
      state.march.startTime=200
      local confirmed=attack.AutomationObserve(fields,input)
      assert(confirmed.state=='confirmed' and confirmed.data.serverAppliedChangePostState)
      assert(state.changed==1 and state.sends==1)
      AllianceTeamType={ATTACK_BOSS=9,ATTACK_CITY=10}
      local leader={uuid=701,ownerUid='2002',ownerName='ally',startId=100101,endTime=150000,
        heroes={1,2,3},status=0}
      local war={uuid=700,leaderMarch=leader,attackAllianceId=7,type=9,targetUuid=501,targetUid=10,memberList={}}
      DataCenter.AllianceWarDataManager={GetAllianceWarDataByUuid=function(self,uuid)if uuid==700 then return war end end,
        CheckJoinAllianceWarByWarData=function()return true end}
      local rally=attack.AutomationRallyInfo('700',true)
      assert(rally and rally.monster and not rally.player and rally.monsterId==10 and rally.memberCount==1)
      assert(rally.uuid=='700' and rally.monsterUuid=='501' and rally.pointIndex==100101)
      war.type=10;assert(attack.AutomationRallyInfo('700',true)==nil,'Player rally must be excluded')
      war.type=9;MarchStatus={MOVING=1};leader.status=1;assert(attack.AutomationRallyInfo('700',true)==nil)
      leader.status=0;war.attackAllianceId=8;assert(attack.AutomationRallyInfo('700',true)==nil)
      war.attackAllianceId=7;war.targetUid=11;assert(attack.AutomationRallyInfo('700',true)==nil)
      war.targetUid=10;state.march=nil
      UIWindowNames={UIFormationSelectListV2='formation'}
      local window
      UIManager={GetInstance=function()return {IsWindowOpen=function()return window~=nil end,
        GetWindow=function()return window end}end}
      UIUtil={OpenFormationSelectUI=function(_,kind,point,uuid)
        assert(kind==4 and point==100101 and uuid==700)
        window={Ctrl={targetType=kind,targetPoint=point,targetUuid=uuid,targetServerId=123,
          OnCheckTime=function(self,formation)SFSNetwork.SendMessage(99,formation,kind,uuid)end}}
      end}
      assert(attack.AutomationRallyOpen(fields,input,rally,true).data.awaitingNativeChooser)
      assert(state.sends==1,'Opening a rally chooser must not send')
      assert(attack.AutomationRallyDispatch(fields,input,rally,true).data.requestDispatched)
      assert(state.sends==2)
      assert(attack.AutomationObserve(fields,input).state=='accepted')
      state.kind=4;state.march={uuid=801,ownerUid='1001',ownerFormationUuid=101,targetUuid=700,startTime=300,
        GetMarchTargetType=function()return state.kind end,clientCreateGuid='12345678-1234-4abc-8def-000000000001'}
      assert(attack.AutomationObserve(fields,input).state=='confirmed')
    ''')
    vm.globals().engine_source = (ROOT / 'tools/feature018_afk_engine.lua').read_text(encoding='utf-8')
    vm.globals().drill_source = (ROOT / 'tools/feature018_drill_engine.lua').read_text(encoding='utf-8')
    vm.globals().stamina_source = (ROOT / 'tools/feature018_stamina_engine.lua').read_text(encoding='utf-8')
    vm.execute('''
      package.loaded['DataCenter.Global.LWBridgeFeature018AttackActions']=attack
      package.preload['DataCenter.Global.LWBridgeFeature018AfkEngine']=function()return assert(load(engine_source))()end
      package.preload['DataCenter.Global.LWBridgeFeature018DrillEngine']=function()return assert(load(drill_source))()end
      package.preload['DataCenter.Global.LWBridgeFeature018StaminaEngine']=function()return assert(load(stamina_source))()end
      package.preload['rapidjson']=function()return {decode=function(text)
        if text=='{}'then return {}end
        if text=='drill-fixture'then return {allianceDrill={enabled=true,activeRally=true,squadIndexes={1}}}end
        if text=='stamina-fixture'then return {enabled=true,minStamina=101,preferFifty=false}end
        return {enabled=true,strategies={}}
      end}end
      overview={Feature018OwnerToken=function()return state.lease and 'owned' or nil end,Feature018SessionToken=function()return state.lease end}
      transport={handlers={},RegisterHandler=function(name,fn)transport.handlers[name]=fn end,
        Array=function(t)return setmetatable(t,{__feature018_array=true})end}
      state.march=nil;state.lease='session'
      local scheduler=require('DataCenter.Global.LWBridgeFeature018AfkEngine')
      local bind=scheduler.Bind
      scheduler.Bind=function(adapter)native_adapter=adapter;return bind(adapter)end
    ''')
    native = vm.execute((ROOT / 'tools/feature018_afk_native.lua').read_text(encoding='utf-8'))
    vm.globals().native = native
    vm.execute('''
      native.Register(transport,overview)
      local s=native.Status().data
      assert(getmetatable(s.rows).__feature018_array and getmetatable(s.receipts).__feature018_array,
        'The feature transport, not the Overview owner, must tag empty JSON arrays')
      local c=native.Catalog().data
      assert(getmetatable(c.options).__feature018_array and getmetatable(c.allianceMembers.members).__feature018_array)
      fields.confirmedAfkConfig='fixture';assert(native.Start(fields).state=='confirmed')
      LuaEntry.Player.uid='2002';native.CleanupAfterLeaseLoss()
      assert(native.Status().data.running and native.Status().data.step=='account_changed')
      state.now=state.now+1;native.Pump();assert(native.Status().data.state=='waiting')
      LuaEntry.Player.uid='1001';state.now=state.now+1;native.Pump()
      assert(native.Status().data.running)
      state.lease=nil;native.CleanupAfterLeaseLoss();assert(not native.Status().data.running)
      state.lease='session';assert(native.Start(fields).state=='confirmed')
      package.preload['Net.Msgs.FindMonsterMessage']=function()return {HandleMessage=function()end}end
      MsgDefines.FindMonster=101
      state.visible=nil;state.moves=0
      CS.SceneManager.World.GetMarch=function(self,uuid)
        if uuid~=state.visible then return nil end
        return {uuid=uuid,monsterId=10,GetMarchType=function()return 7 end,IsMonsterOrBoss=function()return true end}
      end
      DataCenter.WorldMarchDataManager.GetAllMarches=function()
        return state.visible and {{uuid=state.visible,monsterId=10}} or {}
      end
      GoToUtil={MoveToWorldMarchAndOpen=function(point,uuid)
        state.moves=state.moves+1;state.visible=uuid
      end}
      local p={id='search',kind='farm',searchable=true,monsterType=2,monsterNameKey='zombie',
        minLevel=10,maxLevel=10,levelFilterEnabled=true}
      native_adapter.Targets(p,10);assert(state.finds==1)
      require('Net.Msgs.FindMonsterMessage').HandleMessage({}, {uuid=502,pointId=100105})
      local first=native_adapter.Targets(p,10);assert(#first==1 and state.moves==1)
      state.visible=nil;state.now=state.now+11
      native_adapter.Targets(p,10);assert(state.finds==2)
      require('Net.Msgs.FindMonsterMessage').HandleMessage({}, {uuid=503,pointId=100106})
      local second=native_adapter.Targets(p,10);assert(#second==1 and state.moves==2,
        'Each new native search result must acquire its own AOI, not inherit a prior moved flag')
      -- Regression: the retired category returns EMPTY, while current
      -- Energy/UseItem routes name real owned stock. Ignore buy routes.
      ResLackContextType={Energy=3};LWResourceLackGetWay={UseItem=1}
      state.item={itemId=5,uuid='stamina-owned',count=2};state.itemUses=0
      DataCenter.LWResourceLackManager={GetSpecialResWay=function(self,kind)
        assert(kind==3);return {{tips=1,para1='5'},{tips=9,para1='6'},{tips=1,para1='5'}}
      end}
      GoodsUtil={GetGoodsReturnItemCount=function(id)assert(id==5);return 50 end}
      DataCenter.ItemData={GetItemList=function()return {}end,GetItemById=function(self,id)
        assert(id==5,'Purchase routes must never be inspected as consumable stock')
        if state.driftAfterItemRead then LuaEntry.Player.uid='2002'end
        return state.item
      end}
      MsgDefines.ItemUse=102
      fields.confirmedStaminaConfig='stamina-fixture'
      assert(native.StartStamina(fields).state=='confirmed')
      state.now=state.now+1;native.Pump();assert(state.itemUses==1)
      state.now=state.now+1;native.Pump()
      local used=native.StaminaStatus().data
      assert(used.used==1 and used.receipts[1].countBefore==2 and used.receipts[1].countAfter==1)
      assert(used.receipts[1].staminaAfter>used.receipts[1].staminaBefore)
      native.StopStamina();state.stamina=50;assert(native.StartStamina(fields).state=='confirmed')
      state.driftAfterItemRead=true;state.now=state.now+1;native.Pump()
      assert(state.itemUses==1,'Account change during inventory read must block item send')
      state.driftAfterItemRead=false;LuaEntry.Player.uid='1001';native.StopStamina()
      AllyDrillStage={AttackStage=8};WorldMonsterSpecialType={AllyDrill=5}
      local event={stage=1,deadline=300000,point=100105,server=123}
      state.drillMoves=0
      UITimeManager={GetInstance=function()return {GetServerTime=function()return state.now*1000 end}end}
      DataCenter.AllyDrillDataManager={GetCurStageAndCountDown=function()return event.stage,event.deadline end,
        GetActInfo=function()return {data={bossPointId=event.point,bossServerId=event.server}}end,
        JumpToDrill=function()state.drillMoves=state.drillMoves+1 end}
      fields.confirmedAfkConfig='drill-fixture';assert(native.StartDrill(fields).state=='confirmed')
      state.now=state.now+1;native.Pump();assert(state.drillMoves==0 and not native.DrillStatus().data.active)
      event.stage=8;native.DrillStatus();assert(state.drillMoves==0,'Read-only status cannot move the game camera')
      state.now=state.now+1;native.Pump();assert(state.drillMoves==1)
      state.now=state.now+1;native.Pump();assert(state.drillMoves==1,'Target acquisition must be rate limited')
      event.server=124;state.now=state.now+31;native.Pump();assert(state.drillMoves==1,'Cross-server event must not move or send')
      native.StopDrill();event.server=123;state.now=state.now+31;native.Pump();assert(state.drillMoves==1)
      assert(native.Status().data.allianceDrill.running==false)
      native.Stop();state.special=5;state.march=nil;event.stage=8;event.deadline=999999
      state.visible=503
      local opens=0
      UIManager={GetInstance=function()return {IsWindowOpen=function()return window~=nil end,GetWindow=function()return window end}end}
      UIUtil.OpenFormationSelectUI=function(_,kind,point,uuid)
        assert(kind==5 and uuid==503 and point==100105)
        opens=opens+1
        window={Ctrl={targetType=kind,targetPoint=point,targetUuid=uuid,targetServerId=123,
          OnCheckTime=function(self,formation)SFSNetwork.SendMessage(99,formation,kind,uuid)end}}
      end
      assert(native.StartDrill(fields).state=='confirmed')
      local before_sends=state.sends;state.now=state.now+1;native.Pump()
      assert(opens==1 and state.sends==before_sends,'Drill opens its native chooser before any send')
      event.stage=1;state.now=state.now+1;native.Pump()
      assert(state.sends==before_sends,'Event ending must cancel the actual native adapter chooser')
      event.stage=8;state.now=state.now+1;native.Pump();assert(opens==2)
      state.now=state.now+1;native.Pump();assert(state.sends==before_sends+1,'Active Drill must dispatch its native rally route: '..tostring(native.DrillStatus().data.lastError))
      native.StopDrill();state.now=state.now+1;native.Pump()
      assert(state.sends==before_sends+1,'Drill Stop cannot send again')
      local normal_form=DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex
      DataCenter.ArmyFormationDataManager.GetOneArmyInfoByIndex=function(self,slot)
        if slot==2 then return {uuid=102,ownerUid='1001',index=2}end
        return normal_form(self,slot)
      end
      attack.AutomationRallyOpen=function()return {state='accepted',data={awaitingNativeChooser=true}}end
      attack.AutomationRallyDispatch=function()return {state='failed',error='ambiguous dispatch fixture'}end
      local captured={uid='1001',server='123',token='owned',fields=fields}
      assert(native_adapter.Send(captured,2,{kind='farm',rally=true},{uuid='503'}))
      assert(native_adapter.Observe({slot=2}).failed)
      native.Stop()
      assert(not native_adapter.Free(2,{}),'Stop must retain uncertain rally dispatch reservation')



    ''')
    print('FEATURE018_AFK_NATIVE_CHECKS_OK: monster-only/owner guards, create GUID, continuous server post-state, no replay')


if __name__ == '__main__':
    main()
