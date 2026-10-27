"""Actual treatment module + Automation scheduler in inert source-shaped Lua.

No live game, RPC, installed-script writes or processes. Native contracts:
HospitalCureMessage.OnCreate, HospitalManager.HospitalCureHandle/CheckSendFinish,
QueueDataManager.QueueFinishHandle/OnAllianceCallHelp and UIMainCureBtnItem.
"""
from feature018_automation_engine_checks import ROOT, runtime


def treatment():
    vm=runtime()
    vm.globals().native=vm.execute((ROOT/'src/LWBridge.GameScripts/feature018_treatment_native.lua').read_text())
    vm.execute('''
      engine.Register(feature,bridge,native)
      state.config={treatment={enabled=true,amountPerArmy=1}};request.kind='treatment'
      state.callbacks={};state.armies={{armyId=11,dead=10,heal=0}};state.balance=10000;state.soldiers=100
      state.queue={uuid=501,type=7,startTime=0,endTime=0,isHelped=0,itemId='11;1',helpNum=0,lastHelpTime=0,state=0,
        GetQueueState=function(self)return self.state end}
      NewQueueType={Hospital=7};NewQueueState={Free=0,Work=1,Finish=2};AllianceHelpType={Queue=3}
      ResourceType={Metal=1,Food=2,Gold=99};EffectDefine={LW_SOLDIER_MAX_STOCK=301}
      EventId={HospitalUpdate=21,HospitalFinish=22,AllianceQueueHelpNew=23}
      local em={AddListener=function(self,event,fn)state.callbacks[event]=fn end,
        RemoveListener=function(self,event,fn)assert(state.callbacks[event]==fn);state.callbacks[event]=nil end}
      EventManager={GetInstance=function()return em end}
      MsgDefines={HospitalCure=90,AllianceCallHelp=91,QueueFinish=92}
      LuaEntry.Player.IsInAlliance=function()return not state.noAlliance end
      LuaEntry.Resource={GetCntByResType=function()return state.balance end}
      LuaEntry.Effect={GetGameEffect=function()return state.capacity or 1000 end}
      LuaEntry.DataConfig={TryGetNum=function(self,a,b)assert(a=='medical_assist_daily_limit'and b=='k1');return 5 end}
      UITimeManager.GetInstance=function()state.reads=state.reads+1;return {GetServerTime=function()return state.now end,
        IsSameDayForServer=function()return not state.otherDay end}end
      DataCenter.HospitalManager={GetAllHospital=function()state.reads=state.reads+1;if state.bad then error('game error')end;return state.armies end}
      DataCenter.QueueDataManager={GetQueueByType=function(self,t)assert(t==7);return not state.noQueue and state.queue or nil end}
      DataCenter.CityRebuildDataManager={GetCureState=function()return state.rebuilding end}
      DataCenter.SoldierDataManager={GetPlayerSoldiersTotalNum=function()return state.soldiers end,
        GetTemplate=function(self,id)return {cureCost=state.cost or {[1]=10,[2]=20}}end}
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_treatment');state.perf=state.perf+1 end
      SFSNetwork.SendMessage=function(route,...)local args={...};state.sends[#state.sends+1]={route=route,args=args}
        if route==90 then assert(args[1].gold==0 and not args[1].goldForTime and not args[1].goldForResource and not args[1].itemIds)end
        if state.throw then error('uncertain send')end
      end
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services.treatment end
      function cure_result(count)
        state.armies[1].dead=state.armies[1].dead-count;state.armies[1].heal=count
        state.queue.state=1;state.queue.startTime=state.now;state.queue.endTime=state.now+10000
        state.callbacks[21]()
      end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm


def main():
    vm=treatment()
    vm.execute('''
      state.config.treatment.enabled=false;engine.Configure(request);local reads=state.reads
      for _=1,1000 do engine.Pump()end
      assert(reads==state.reads and #state.sends==0 and state.perf==0 and next(state.callbacks)==nil)
      state.config.treatment.enabled=true;engine.Configure(request);step()
      assert(#state.sends==1 and state.sends[1].route==90 and state.sends[1].args[1].arr[11]==1 and status().confirmed==0)
      step();assert(status().confirmed==0,'Submission is not proof')
      cure_result(1);step();assert(status().confirmed==1 and status().receipts[1].after.soldiersQueued==1)
      step();assert(#state.sends==2 and state.sends[2].route==91)
      local a=state.sends[2].args;assert(a[1]==501 and a[2]==3 and a[3]==7 and a[4]=='11;1')
      state.queue.isHelped=1;step();assert(status().confirmed==1,'Queue flag without native response event is not proof')
      state.callbacks[23](99);step();assert(status().confirmed==1,'Other queue help event is not proof')
      state.callbacks[23](7);step();assert(status().confirmed==2 and status().receipts[2].after.helpsRequested==1)
      step();assert(status().state=='success'and #state.sends==2)
      state.now=state.now+16000;state.queue.state=2;step();assert(#state.sends==3 and state.sends[3].route==92)
      assert(state.sends[3].args[1].uuid==501)
      state.armies[1].heal=0;state.queue.state=0;state.queue.startTime=0;state.queue.endTime=0;state.callbacks[22]();step()
      assert(status().confirmed==2,'Collection also needs actual soldier stock increase')
      state.soldiers=101;step();assert(status().confirmed==3 and status().receipts[3].after.collected==1)
      engine.Stop(request);assert(not engine.IsActive()and next(state.callbacks)==nil)
      local reads=state.reads;state.now=state.now+60000;engine.Pump();assert(reads==state.reads)
    ''')
    for case in ['empty','poor','noQueue','rebuilding','invalidAmount','premium','gameError','wrongAccount','wrongLease','uncertain','offPending','partial','wrongQueue','noEvent','throwThenConfirmed']:
        vm=treatment()
        if case=='empty':vm.execute('state.armies={}')
        elif case=='poor':vm.execute('state.balance=0')
        elif case=='noQueue':vm.execute('state.noQueue=true')
        elif case=='rebuilding':vm.execute('state.rebuilding=true')
        elif case=='invalidAmount':vm.execute('state.config.treatment.amountPerArmy=0')
        elif case=='premium':vm.execute('state.cost={[1]=1,[2]=2,[99]=3}')
        elif case=='gameError':vm.execute('state.bad=true')
        elif case=='wrongAccount':vm.execute("LuaEntry.Player.uid='1002'")
        elif case=='wrongLease':vm.execute("state.token='replacement'")
        else:vm.execute('state.throw=true')
        vm.execute('step()')
        if case in ['empty','poor','noQueue','rebuilding']:
            vm.execute("assert(#state.sends==0 and status().state=='nothing_available')")
        elif case in ['invalidAmount','premium','gameError']:
            vm.execute("assert(#state.sends==0 and status().state=='error')")
        elif case in ['wrongAccount','wrongLease']:
            vm.execute('assert(#state.sends==0 and not engine.IsActive())')
        elif case=='throwThenConfirmed':
            vm.execute('cure_result(1);step();assert(status().confirmed==1 and #state.sends==1)')
        else:
            if case=='offPending':
                vm.execute('engine.Stop(request);local reads=state.reads;step();assert(reads==state.reads and next(state.callbacks)==nil)')
            elif case=='partial':vm.execute('state.armies[1].dead=9;state.callbacks[21]()')
            elif case=='wrongQueue':vm.execute('cure_result(1);state.queue.uuid=502')
            elif case=='noEvent':vm.execute('state.armies[1].dead=9;state.armies[1].heal=1;state.queue.state=1;state.queue.startTime=state.now')
            if case!='offPending':
                vm.execute("state.now=state.now+16000;engine.Pump();assert(status().state=='unconfirmed'and status().confirmed==0)")
            vm.execute('''engine.Stop(request);state.config.treatment.enabled=true;engine.Configure(request)
              state.now=state.now+61000;engine.Pump();assert(#state.sends==1,'Never replay uncertain treatment');assert(engine.Run(request).state=='failed')''')
    for case in ['noAlliance','alreadyHelped','dailyLimit','otherDay','fullStock','wrongHospital']:
        vm=treatment()
        vm.execute('state.armies[1].heal=2;state.queue.state=1;state.queue.startTime=1;state.queue.endTime=state.now+60000')
        if case=='noAlliance':vm.execute('state.noAlliance=true')
        elif case=='alreadyHelped':vm.execute('state.queue.isHelped=1')
        elif case in ['dailyLimit','otherDay']:vm.execute('state.queue.helpNum=5;state.queue.lastHelpTime=state.now;state.otherDay='+('true'if case=='otherDay'else'false'))
        elif case=='fullStock':vm.execute('state.queue.state=2;state.capacity=100')
        else:vm.execute('state.queue.type=99')
        vm.execute('step()')
        if case=='otherDay':vm.execute('assert(#state.sends==1 and state.sends[1].route==91)')
        elif case=='wrongHospital':vm.execute("assert(#state.sends==0 and status().state=='error')")
        else:vm.execute("assert(#state.sends==0 and status().state=='nothing_available')")
    vm=treatment()
    vm.execute('''state.config.treatment.amountPerArmy=3;state.armies[2]={armyId=12,dead=2,heal=0};step()
      assert(state.sends[1].args[1].arr[11]==3 and state.sends[1].args[1].arr[12]==2,'Saved amount applies separately per army')''')
    print('FEATURE018_R30_TREATMENT_LUA_PASS cure-help-collect exact-poststate off=zero stop wrong-account wrong-lease empty error ordinary-only uncertain=no-replay')


if __name__=='__main__':main()
