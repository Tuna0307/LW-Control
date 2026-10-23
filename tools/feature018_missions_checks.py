"""Actual R30 mission adapter + scheduler, inert source-shaped native objects only."""
from pathlib import Path
from feature018_automation_engine_checks import runtime as base_runtime

ROOT = Path(__file__).resolve().parents[1]


def runtime(kind='dispatch'):
    vm = base_runtime()
    vm.execute('''
      state.callbacks={};state.reads=0;state.config={};state.heroEligible=true;state.weekday=3
      state.config.dispatch={enabled=true,autoExecute=true,collectRewards=true,delayMinutes=3,
        weeklyQualities={'none','ur','none','none','none','ur','none'}}
      state.config.dispatchAssist={enabled=true,autoHelp=true,qualities={'ur','special'},delaySeconds={0,0},intervalSeconds=30}
      state.config.ghostRecon={enabled=true,autoStartOwn=true,autoJoinAlliance=true,autoClaimRewards=true,allianceFilter='special'}
      LuaEntry.Player.allianceId='al';LuaEntry.DataConfig={TryGetNum=function(self,key,n,default)return default end}
      state.clock={GetServerTime=function()return state.now end,GetNowWeekdayIndex=function()return state.weekday end,
        GetTodayZeroServerTime=function(self,at)return 0 end}
      UITimeManager.GetInstance=function()state.reads=state.reads+1;return state.clock end
      EventId={DispatchTaskUpdateAll=101,DispatchTaskUpdateAlliance=102,GhostreconTaskRefreshAll=103,GhostreconAllianceTaskRefresh=104}
      state.em={AddListener=function(self,event,cb)state.callbacks[event]=cb end,
        RemoveListener=function(self,event,cb)if state.callbacks[event]==cb then state.callbacks[event]=nil end end}
      EventManager={GetInstance=function()state.reads=state.reads+1;return state.em end}
      for _,r in ipairs({'DispatchGetTasks','DispatchGetAllianceTasks','DispatchStart','DispatchReward','DispatchTaskRefresh','DispatchAssist',
        'GhostreconGetTaskList','GhostReconGetAllianceTaskList','GhostReconJoinTeam','GhostReconStartTeam','GhostReconReward','GhostReconHandleSendChat','GhostReconPutPointInWorld'})do MsgDefines[r]=r end
      state.own={};state.allies={};state.assists=0;state.tokens={count=4};state.ghosts={};state.ghostAllies={}
      DataCenter.ActDispatchTaskDataManager={GetAllSingleTasks=function()state.reads=state.reads+1;return state.own end,
        GetAllAllianceTasks=function()state.reads=state.reads+1;return state.allies end,
        GetTodayAssistNum=function()return state.assists end,GetDispatchSetting=function()return 5 end,
        GetMaxMarch=function()return state.max or 3 end,GetSingleTaskIngCount=function()return state.running or 0 end,
        GetTaskRefreshSetting=function()return 100,9001,1 end}
      DataCenter.ItemData={GetItemById=function(self,id)assert(id==9001);return state.tokens end}
      DataCenter.ActGhostreconManager={taskList=state.ghosts,dispatchBeginTime=1,dispatchEndTime=999999999,
        IsOpenDay=function()return state.open~=false end,GetTaskTemplate=function(self,id)return state.cfg end,
        GetTeamworkRewardTimesFull=function()return state.ghostFull==true end,GetTeamMaxMemberNum=function()return 5 end}
      DataCenter.ActGhostreconAllianceManager={allianceTaskList=state.ghostAllies}
      state.cfg={color=5,special=true,showTime=300000,superCondions={},GetSuperCondionNumsByMemberList=function()return {}end}
      SeasonUtil={IsInLandlordActAndOnCenterServer=function()return state.landlord==true end}
      local recommendation={GetRecommendHeroList=function()if state.heroError then error('native hero error')end;return state.heroes or {'h1','h2','h3'},state.heroEligible end}
      package.preload['UI.UIFormation.UIFormationDispatchTask.Controller.UIFormationDispatchTaskCtrl']=function()return recommendation end
      package.preload['UI.UIDispatchTask.Ghostrecon.Formation.Controller.UIGhostreconFormationCtrl']=function()return recommendation end
      replyModules={}
      for _,name in ipairs({'GhostReconPutPointInWorldMessage','GhostReconJoinTeamMessage','GhostReconHandleSendChatMessage','GhostReconStartTeamMessage','GhostReconRewardMessage'})do
        replyModules[name]={HandleMessage=function()return 'original-'..name end}
        package.preload['Net.Msgs.Ghostrecon.'..name]=function()return replyModules[name]end
      end
      package.preload['UI.UIFormation.UIFormationDispatchTask.View.UIFormationDispatchTaskView']=function()return {CalculateMarchTime=function()return 2500 end}end
      SFSNetwork.SendMessage=function(route,...)
        state.sends[#state.sends+1]={route=route,args={...}}
        if state.throw and route~='DispatchGetTasks' and route~='DispatchGetAllianceTasks' and route~='GhostreconGetTaskList' and route~='GhostReconGetAllianceTaskList'then error('uncertain wire send')end
      end
      bridge.PerformanceEnd=function(kind)assert(kind:match('^automation_'));state.perf=state.perf+1 end
      function emit(kind)local map={dispatch=101,dispatchAssist=102,ghostRecon=103,ghostAlliance=104};local cb=state.callbacks[map[kind]];if cb then cb()end end
      function task(id,color,owner)return {uuid=id,cfgId=1,ownerInfo={uid=owner or '1001'},cfg={color=color or 5,is_special=0},pointId=50,
        targetServer=123,completionTime=0,rewarded=0}end
      function ghost(id,owner)
        return {uuid=id,cfgId=1,ownerId=owner or '1001',ownerServer=123,targetServer=123,pointId=777,
          completionTime=0,teamStartTime=0,sendChatTime=0,taskExpireTime=state.now+600000,actEndTime=state.now+600000,
          memberList={},MemberIsFull=function(self)return #self.memberList>=5 end}
      end
    ''')
    vm.globals().native=vm.execute((ROOT/'tools/feature018_missions_native.lua').read_text(encoding='utf-8'))
    vm.globals().engine=vm.execute((ROOT/'tools/feature018_automation_engine.lua').read_text(encoding='utf-8'))
    vm.globals().kind=kind
    vm.execute('''
      for k,c in pairs(state.config)do if k~=kind then c.enabled=false end end
      request.kind=kind;engine.Register(feature,bridge,native)
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services[kind]end
      function configure()assert(engine.Configure(request).state=='confirmed')end
      function acquire()configure();step();emit(kind);step();step()end
      function reconcile()step();emit(kind);step()end
    ''')
    return vm


def main():
    # Off is a gate before subscriptions, list reads and clocks; Stop also fences
    # old callbacks/Verify continuations, independent of the scheduler's off gate.
    for kind in ['dispatch','dispatchAssist']:
        vm=runtime(kind)
        vm.execute('''
          local n=state.reads;assert(native.Supports(kind));assert(native.Plan(kind,{enabled=false},state.now)==nil)
          assert(state.reads==n and next(state.callbacks)==nil and #state.sends==0)
          state.config[kind].enabled=false;configure();n=state.reads
          for _=1,1000 do engine.Pump()end;assert(state.reads==n and #state.sends==0 and state.perf==0)
          state.config[kind].enabled=true;configure();step();local n=#state.sends
          assert(n==1 and status().submitted==0,'A list read is not an action')
          engine.Stop(request);assert(next(state.callbacks)==nil);local reads=state.reads;step()
          assert(state.reads==reads and #state.sends==n)
          assert(not native.Verify(kind,{uid='1001',generation=1,phase='missionList',version=-1,sentAt=0}))
          assert(#state.sends==n,'A stopped native continuation must not send')
        ''')
        for failure in ['empty','wrongAccount','wrongLease','error']:
            vm=runtime(kind)
            if failure=='error':
                vm.execute("EventId=nil;configure();step();assert(status().state=='error'and #state.sends==0 and next(state.callbacks)==nil)")
            elif failure=='empty':
                vm.execute("acquire();assert(status().state=='nothing_available'and status().submitted==0);engine.Stop(request)")
            else:
                vm.execute("configure();step();state.savedSends=#state.sends")
                vm.execute("LuaEntry.Player.uid='1002'" if failure=='wrongAccount' else "state.token='other'")
                vm.execute("step();assert(not engine.IsActive()and #state.sends==state.savedSends and next(state.callbacks)==nil)")

    vm=runtime()
    vm.execute('''
      local t=task('d');state.own={t};acquire()
      assert(state.sends[2].route=='DispatchStart'and state.sends[2].args[1]=='d'and #state.sends[2].args[2]==3 and state.sends[2].args[3]==2500)
      t.completionTime=state.now+100000;step();assert(status().confirmed==0,'Optimistic cache is not success')
      emit(kind);step();assert(status().confirmed==1 and status().receipts[1].after.uuid=='d')
      step();assert(status().state=='success');engine.Stop(request)
    ''')
    vm=runtime()
    vm.execute('''
      local t=task('claim');t.completionTime=state.now-1;state.own={t};acquire()
      assert(state.sends[2].route=='DispatchReward');t.rewarded=1;reconcile();assert(status().confirmed==1)
    ''')
    # Live v27: the claimed task is removed from the fresh own list.
    vm=runtime()
    vm.execute('''
      local t=task('gone');t.completionTime=state.now-1;state.own={t};acquire()
      assert(state.sends[2].route=='DispatchReward');state.own={};reconcile();assert(status().confirmed==1,'Removal after a fresh list confirms the claim')
    ''')
    vm=runtime()
    vm.execute('''
      local t=task('stale');t.completionTime=state.now-1;state.own={t};acquire()
      state.own={};step();step();assert(status().confirmed==0,'Without a fresh list reply nothing is confirmed')
    ''')
    for guard in ['capacity','heroes','season','foreign','delay','oversizedHeroes','heroError']:
        vm=runtime()
        vm.execute("state.own={task('d')}")
        vm.execute({'capacity':'state.running=3','heroes':'state.heroEligible=false','season':'state.landlord=true',
            'foreign':"state.own[1].ownerInfo.uid='1002'",'delay':'state.config.dispatch.delayMinutes=30',
            'oversizedHeroes':"state.heroes={'h1','h2','h3','h4'}",'heroError':'state.heroError=true'}[guard])
        vm.execute("acquire();assert(status().submitted==0 and #state.sends==1)")
    for refresh in ['tokens','premium','preserveGood']:
        vm=runtime()
        vm.execute("state.weekday=2;state.own={task('bad',3)}")
        if refresh=='premium':vm.execute('state.tokens.count=0')
        if refresh=='preserveGood':vm.execute("state.own[2]=task('good',5);state.heroEligible=false")
        vm.execute('acquire()')
        if refresh=='tokens':
            vm.execute("assert(state.sends[2].route=='DispatchTaskRefresh'and state.sends[2].args[1]==0);state.tokens.count=3;state.own[1].cfg.color=5;reconcile();assert(status().confirmed==1)")
        else:vm.execute("assert(status().submitted==0 and #state.sends==1,'Never spend gems or reroll a blocked qualified mission')")

    vm=runtime('dispatchAssist')
    vm.execute('''
      local t=task('ally',5,'1002');t.completionTime=state.now-1;state.allies={t};acquire()
      assert(state.sends[2].route=='DispatchAssist'and state.sends[2].args[1]=='ally'and state.sends[2].args[2]==123)
      step();state.assists=1;emit(kind);step();assert(status().confirmed==0,'Attempt count alone is not exact task confirmation')
      state.allies={};emit(kind);step();assert(status().confirmed==1)
    ''')
    for guard in ['self','running','rewarded','limit','quality','delay','noAlliance']:
        vm=runtime('dispatchAssist');vm.execute("local t=task('ally',5,'1002');t.completionTime=state.now-1;state.allies={t}")
        vm.execute({'self':"state.allies[1].ownerInfo.uid='1001'",'running':'state.allies[1].completionTime=state.now+99999',
            'rewarded':'state.allies[1].rewarded=1','limit':'state.assists=5','quality':"state.config.dispatchAssist.qualities={'r'}",
            'delay':'state.config.dispatchAssist.delaySeconds={60,60}','noAlliance':"LuaEntry.Player.allianceId=''"}[guard])
        vm.execute("acquire();assert(status().submitted==0)")

    # A thrown/uncertain write is never retried after timeout or off/on. The
    # scheduler owns the persistent tombstone; the adapter only sends follow-up reads.
    for kind in ['dispatch','dispatchAssist']:
        vm=runtime(kind)
        if kind=='dispatch':vm.execute("state.own={task('d')}")
        else:vm.execute("local t=task('a',5,'1002');t.completionTime=state.now-1;state.allies={t}")
        vm.execute('''
          state.throw=true;acquire();assert(status().submitted==1 and status().confirmed==0)
          state.now=state.now+16000;engine.Pump();assert(status().state=='unconfirmed')
          local n=status().submitted;engine.Stop(request);state.config[kind].enabled=true;configure();step()
          assert(status().submitted==n and status().state=='unconfirmed','Uncertain action must never replay')
        ''')
    print('FEATURE018_R30_MISSIONS_LUA_PASS dispatch claim token-refresh weekly hero-capacity assist qualities delay exact-UID ghost-removed zero-off stop fresh-state uncertain no-replay account lease empty error')


if __name__=='__main__':
    main()
