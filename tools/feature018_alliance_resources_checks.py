"""Inert real Lua scheduler + stronghold/center adapters; no game access."""
from pathlib import Path
from feature018_automation_engine_checks import runtime


def resources(kind):
    vm=runtime()
    vm.globals().native=vm.execute(Path(__file__).with_name('feature018_alliance_resources_native.lua').read_text())
    vm.globals().testKind=kind
    vm.execute('''
      state.config={[testKind]={enabled=true,intervalMinutes=60}};request.kind=testKind
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_'..testKind);state.perf=state.perf+1 end
      engine.Register(feature,bridge,native)
      state.callbacks={};state.available=true;state.alliance=true
      LuaEntry.Player.IsInAlliance=function()return state.alliance end
      SeasonUtil={SeasonHasCityStronghold=function()return state.available end,SeasonHasMilitaryCenterAttachment=function()return state.available end}
      EventId={CrossOccupyStrongholdListUpdate=1,CollectStrongholdResourceSuccess=2,UpdateAllAllianceMineList=3,AllianceStoveCenterUpdate=4}
      EventManager={GetInstance=function()return {
        AddListener=function(self,id,f)state.callbacks[id]=f end,
        RemoveListener=function(self,id,f)if state.callbacks[id]==f then state.callbacks[id]=nil end end}end}
      MsgDefines={GetCrossOccupyStrongholdList=81,CollectStrongholdResource=82,GetAllAllianceMineList=83,FetchMilitaryCenterBuildInfo=84,FetchMilitaryCenterBuildResult=85}
      AllianceMineStatus={Normal=2};DataCenter.SeasonDataManager={CrossOccupyStrongholdRewardInfo={}}
      DataCenter.AllianceMineManager={allianceMilitaryAttachmentList={},militaryCenterBuildInfos={info={}}}
      SFSNetwork.SendMessage=function(route,...)state.sends[#state.sends+1]={route=route,args={...}};if state.throw and(route==82 or route==85)then error('uncertain send')end end
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services[testKind]end
      function produce(n)
       DataCenter.SeasonDataManager.CrossOccupyStrongholdRewardInfo={{id=9,serverId=123,leftNum=n}}
       DataCenter.AllianceMineManager.allianceMilitaryAttachmentList={{buildId=9,level=1,status=2,Injuried=function()return state.injured or false end}}
       DataCenter.AllianceMineManager.militaryCenterBuildInfos={info={{buildId=9,num=n}},now=state.now}
      end
      function initial(n)
       step();produce(n)
       if testKind=='allianceCenterResource'then state.callbacks[3]();step();step();state.callbacks[4]()
       else state.callbacks[1]()end
       step();step()
      end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm


def main():
    for kind in ['strongholdResource','allianceCenterResource']:
        vm=resources(kind)
        vm.execute('''
          state.config[testKind].enabled=false;engine.Configure(request);local n=state.reads
          for _=1,100 do engine.Pump()end;assert(n==state.reads and #state.sends==0 and next(state.callbacks)==nil)
          state.config[testKind].enabled=true;engine.Configure(request);initial(25)
          local action=state.sends[#state.sends];assert(action.route==(testKind=='strongholdResource'and 82 or 85));assert(status().confirmed==0)
          step();assert(status().confirmed==0,'Cached local decrement is not proof')
          if testKind=='strongholdResource'then state.callbacks[2]({serverId=123,strongholdId=9})else state.callbacks[4]()end
          step();assert(status().confirmed==0,'Post-send server read is required')
          produce(0);state.callbacks[testKind=='strongholdResource'and 1 or 4]();step()
          assert(status().confirmed==1 and status().receipts[1].after.amount==0)
          step();assert(status().state=='success'and status().nextRunAt==state.now+3600000)
          engine.Stop(request);assert(not engine.IsActive()and next(state.callbacks)==nil)
        ''')
        if kind=='strongholdResource':
            # Live v27 reply: the success broadcast carries the reward only, no stronghold IDs.
            vm=resources(kind)
            vm.execute('''
              initial(17);state.callbacks[2]({reward={{type=1,value=17}}});step()
              produce(0);state.callbacks[1]();step()
              assert(status().confirmed==1 and status().receipts[1].after.amount==0,'Real reward-only reply confirms after the fresh list')
            ''')
            vm=resources(kind)
            vm.execute('''
              initial(17);state.callbacks[2]({reward={{type=1,value=17}}});step()
              produce(17);state.callbacks[1]();step();step()
              assert(status().confirmed==0,'An unchanged fresh amount is never success')
            ''')
        if kind=='strongholdResource':
            # Live v27: a refusal reply (e.g. too little to collect) is not uncertain; retried at the interval.
            vm=resources(kind)
            vm.execute('''
              local reply={HandleMessage=function()end};local original=reply.HandleMessage
              package.preload['Net.Msgs.Season.CollectStrongholdResourceMessage']=function()return reply end
              initial(1);local sends=#state.sends;reply:HandleMessage({errorCode='season_s2_tips_004'});step()
              assert(status().state=='waiting'and status().refused==1 and status().confirmed==0)
              assert(status().nextRunAt==state.now+3600000,'Refusal waits for the saved interval, not a tight loop')
              for _=1,30 do step()end;assert(#state.sends==sends,'No resend before the interval')
              engine.Stop(request);assert(reply.HandleMessage==original,'Observer removed on Stop')
            ''')
        for case in ['nothing','unavailable','wrongAccount','uncertain','gameError','offPending','stale','wrongIdentity','invalid','injured','foreign']:
            vm=resources(kind)
            if case=='unavailable':
                vm.execute("state.available=false;step();assert(status().state=='nothing_available'and status().before.waitingForEvent and #state.sends==0)")
            elif case=='invalid':
                vm.execute("state.config[testKind].intervalMinutes=0;engine.Configure(request);step();assert(status().state=='error'and #state.sends==0)")
            elif case=='gameError':
                vm.execute("EventId.CrossOccupyStrongholdListUpdate=nil;EventId.UpdateAllAllianceMineList=nil;step();assert(status().state=='error'and #state.sends==0)")
            elif case=='stale':
                vm.execute("step();produce(25);step();assert(#state.sends==1 and status().confirmed==0);state.now=state.now+16000;engine.Pump();assert(status().state=='error')")
            elif case=='injured' and kind=='allianceCenterResource':
                vm.execute("state.injured=true;initial(25);assert(status().state=='nothing_available'and status().submitted==0)")
            elif case=='foreign' and kind=='strongholdResource':
                vm.execute("step();DataCenter.SeasonDataManager.CrossOccupyStrongholdRewardInfo={{id=9,serverId=999,leftNum=25}};state.callbacks[1]();step();step();assert(status().state=='nothing_available'and status().submitted==0)")
            elif case=='nothing':
                vm.execute("initial(0);assert(status().state=='nothing_available'and status().submitted==0)")
            elif case=='wrongAccount':
                vm.execute("step();LuaEntry.Player.uid='1002';step();assert(not engine.IsActive()and #state.sends==1 and next(state.callbacks)==nil)")
            elif case in ['uncertain','offPending','wrongIdentity']:
                vm.execute("state.throw=true;initial(25);assert(status().submitted==1)")
                if case=='offPending':vm.execute('engine.Stop(request)')
                elif case=='wrongIdentity' and kind=='strongholdResource':
                    vm.execute("state.callbacks[2]({serverId=999,strongholdId=9});step();assert(status().confirmed==0)")
                vm.execute("local n=#state.sends;state.now=state.now+16000;engine.Pump();engine.Stop(request);state.config[testKind].enabled=true;engine.Configure(request);step();assert(#state.sends==n,'Uncertain gameplay must never replay')")
        vm=resources(kind)
        vm.execute('''
          assert(native.Observe(testKind,state.config[testKind],state.now,'scope').state=='failed','Do not disrupt an enabled service')
          state.config[testKind].enabled=false;engine.Configure(request)
          SFSNetwork.SendMessage=function(route,...)
           assert(route==81 or route==83 or route==84,'Observation cannot claim anything')
           state.sends[#state.sends+1]={route=route};produce(25)
           state.callbacks[route==81 and 1 or route==83 and 3 or 4]()
          end
          local result
          for _=1,6 do state.now=state.now+1000;result=native.Observe(testKind,state.config[testKind],state.now,'scope');if result.state=='confirmed'then break end end
          assert(result.state=='confirmed'and result.data.readOnly and result.data.available==1 and result.data.claimed==0)
          assert(next(state.callbacks)==nil,'Completed observation removes all listeners')
          state.available=false;result=native.Observe(testKind,state.config[testKind],state.now,'scope')
          assert(result.state=='confirmed'and result.data.waitingForEvent and result.data.readOnly)
        ''')
    print('FEATURE018_R30_ALLIANCE_RESOURCES_LUA_PASS fresh-query exact-poststate seasonal empty off stop uncertain no-replay account error eligibility')


if __name__=='__main__':main()
