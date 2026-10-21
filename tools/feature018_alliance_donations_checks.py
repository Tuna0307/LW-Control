"""Actual R30 donation adapter, entirely inert source-shaped Lua objects."""
from feature018_automation_engine_checks import runtime

def donations():
    vm=runtime()
    vm.execute('''
      state.config={allianceHelp={enabled=false},allianceDonate={enabled=true,threshold=15}}
      request.kind='allianceDonate';state.left=20;state.balance=10000;state.callbacks={}
      state.science={scienceId=101,curLevel=1,currentPro=10,res=100,resNum=100}
      ResourceType={Wood=100,Gold=999};state.resource=100
      LuaEntry.Resource={GetCntByResType=function(self,resource)assert(resource==state.science.res);return state.balance end}
      DataCenter.AllianceScienceDataManager={alScienceList={[101]=state.science},
        GetResDonateRestCount=function()return state.left end,
        GetCurRecommendScience=function()return state.science end,
        GetOneAllianceScienceById=function(self,id)assert(id==101);return state.science end}
      package.preload['UI.UIAlliance.UIAllianceScienceInfo.Controller.UIAllianceScienceInfoCtrl']=function()
        return {GetScienceDataById=function(self,id)if state.bad then error('game read failed')end;return {canDonate=not state.locked,resType=state.science.res,resNum=state.science.resNum}end}
      end
      EventId={OnGetAllianceTechMessage=12,AllianceTechnology=13}
      local em={AddListener=function(self,event,cb)state.callbacks[event]=cb end,
        RemoveListener=function(self,event,cb)if state.callbacks[event]==cb then state.callbacks[event]=nil end end}
      EventManager={GetInstance=function()return em end}
      MsgDefines.AllScienceRefresh=80;MsgDefines.AlScienceDonate=81;MsgDefines.AlScienceGoldDonate=82;MsgDefines.AlScienceFresh=83
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_allianceDonate');state.perf=state.perf+1 end
      SFSNetwork.SendMessage=function(route,...)assert(route~=82,'Never use premium donation');state.sends[#state.sends+1]={route=route,args={...}};if state.throw and route==81 then error('uncertain donation')end end
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services.allianceDonate end
      function hydrate()step();state.callbacks[12]();step();step();assert(state.sends[#state.sends].route==83);state.callbacks[13]();step()end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm

def main():
    vm=donations()
    vm.execute('''
      state.config.allianceDonate.enabled=false;engine.Configure(request);local n=state.reads
      for _=1,100 do engine.Pump()end;assert(state.reads==n and #state.sends==0)
      state.config.allianceDonate.enabled=true;engine.Configure(request);step()
      assert(#state.sends==1 and state.sends[1].route==80 and status().submitted==0)
      state.callbacks[12]();step();step();assert(#state.sends==2 and state.sends[2].route==83 and status().submitted==0)
      state.callbacks[13]();step();step()
      assert(#state.sends==3 and state.sends[3].route==81 and state.sends[3].args[1]==101 and state.sends[3].args[2]==1)
      assert(status().confirmed==0);state.left=19;state.science.currentPro=20;state.balance=9900;step()
      assert(status().state=='success'and status().donated==1 and status().remaining==19 and status().scienceId==101)
      engine.Stop(request);assert(not engine.IsActive()and not state.callbacks[12])
    ''')
    for case in ['belowThreshold','poor','premium','locked','wrongAccount','off','error','uncertain','partial','throwThenConfirmed','otherOrdinary']:
        vm=donations();vm.execute('hydrate()')
        if case=='belowThreshold':vm.execute('state.left=14')
        elif case=='poor':vm.execute('state.balance=99')
        elif case=='premium':vm.execute('state.science.res=ResourceType.Gold')
        elif case=='otherOrdinary':vm.execute('state.science.res=101')
        elif case=='locked':vm.execute('state.locked=true')
        elif case=='wrongAccount':vm.execute("LuaEntry.Player.uid='1002'")
        elif case=='off':vm.execute('engine.Stop(request)')
        elif case=='error':vm.execute('state.bad=true')
        else:vm.execute('state.throw=true')
        vm.execute('step()')
        if case in ['belowThreshold','poor','premium','locked']:
            vm.execute("assert(#state.sends==2 and status().state=='nothing_available')")
        elif case in ['off','wrongAccount']:vm.execute('assert(#state.sends==2 and not engine.IsActive())')
        elif case=='error':vm.execute("assert(#state.sends==2 and status().state=='error')")
        elif case in ['throwThenConfirmed','otherOrdinary']:
            vm.execute("state.left=19;state.science.currentPro=20;state.balance=9900;step();assert(status().state=='success'and #state.sends==3)")
        else:
            if case=='partial':vm.execute('state.science.currentPro=20')
            vm.execute("state.now=state.now+16000;engine.Pump();assert(status().state=='unconfirmed'and status().confirmed==0);engine.Stop(request);state.config.allianceDonate.enabled=true;engine.Configure(request);state.now=state.now+61000;engine.Pump();assert(#state.sends==3,'Never replay uncertain currency spend')")
    vm=donations()
    vm.execute('''state.science.res=0;state.science.resNum=0;step();state.callbacks[12]();step();step()
      state.callbacks[13]();step();assert(#state.sends==2 and status().submitted==0 and status().state=='awaiting_confirmation',
        'Unloaded zero cost is not nothing available or permission to donate')
      state.science.res=100;state.science.resNum=100;state.callbacks[13]();step();step();assert(#state.sends==3)''')
    print('FEATURE018_R30_DONATIONS_LUA_PASS fresh-query standard-only threshold balance poststate zero-off stop account error partial uncertain no-replay')

if __name__=='__main__':main()
