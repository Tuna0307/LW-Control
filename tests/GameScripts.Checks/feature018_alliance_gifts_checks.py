"""Inert normal/advanced gift list/claim cycles using the actual R30 native adapter."""
from feature018_automation_engine_checks import runtime

def gifts():
    vm=runtime()
    vm.execute('''
      state.callbacks={};state.config={allianceHelp={enabled=false},allianceGift={enabled=true,intervalMinutes=120}}
      request.kind='allianceGift';MsgDefines.AllianceGiftList=72;MsgDefines.AllianceReceiveAllGift=73
      DataCenter.AllianceGiftDataManager={giftInfoList={[1]={},[2]={}}}
      EventId={RefreshAllianceGift=11}
      local em={AddListener=function(self,event,cb)state.callbacks[event]=cb end,
        RemoveListener=function(self,event,cb)if state.callbacks[event]==cb then state.callbacks[event]=nil end end}
      EventManager={GetInstance=function()return em end}
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_allianceGift');state.perf=state.perf+1 end
      SFSNetwork.SendMessage=function(route,...)state.sends[#state.sends+1]={route=route,args={...}};if state.throw and route==73 then error('uncertain gift send')end end
      function step()state.now=state.now+1000;engine.Pump()end
      function gift(uuid)return {uuid=uuid,receiveState=0,receiveTime=0,expirationTime=state.now+300000}end
      function reply(lists)DataCenter.AllianceGiftDataManager.giftInfoList=lists;state.callbacks[11]()end
      function status()return engine.Status().data.services.allianceGift end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm

def main():
    vm=gifts()
    vm.execute('''
      state.config.allianceGift.enabled=false;engine.Configure(request);local n=state.reads
      for _=1,100 do engine.Pump()end;assert(state.reads==n and not state.callbacks[11]and #state.sends==0)
      state.config.allianceGift.enabled=true;engine.Configure(request);step()
      assert(#state.sends==1 and state.sends[1].route==72 and state.sends[1].args[1]==0 and state.sends[1].args[2]==1000)
      assert(status().submitted==0 and status().confirmed==0,'Read query is not a claim')
      step();assert(#state.sends==1,'Do not read cached empty lists as a fresh empty result')
      local normal={a=gift('a'),b=gift('b')};local advanced={c=gift('c')};reply({[1]=normal,[2]=advanced})
      step();step();assert(state.sends[2].route==73 and state.sends[2].args[1]==1)
      step();assert(status().confirmed==0,'Submitting/optimistic fields are not confirmation')
      for _,g in pairs(normal)do g.receiveState=1;g.receiveTime=state.now end
      step();step();assert(state.sends[3].route==73 and state.sends[3].args[1]==2)
      advanced.c.receiveState=1;advanced.c.receiveTime=state.now;step();step()
      assert(status().state=='success'and status().confirmed==2 and status().submitted==2)
      assert(status().normalClaimed==2 and status().advancedClaimed==1)
      assert(status().receipts[1].after.claimed==2 and status().receipts[2].after.claimed==1)
      assert(status().nextRunAt==state.now+120*60000,'Recovered 120-minute interval')
      engine.Stop(request);assert(not engine.IsActive()and not state.callbacks[11])
    ''')
    for case in ['nothing','uncertain','wrongAccount','offQuery','gameError','partial']:
        vm=gifts()
        if case=='gameError':
            vm.execute("EventId.RefreshAllianceGift=nil;step();assert(#state.sends==0 and status().state=='error')")
            continue
        vm.execute("step();assert(#state.sends==1)")
        if case=='offQuery':
            vm.execute("engine.Stop(request);local n=state.reads;step();assert(state.reads==n and not state.callbacks[11]);state.config.allianceGift.enabled=true;engine.Configure(request);step();assert(#state.sends==2,'A cancelled read-only inspection may be started afresh')")
            continue
        if case=='nothing':
            vm.execute("reply({[1]={},[2]={}});step();step();assert(status().state=='nothing_available'and status().submitted==0)")
            continue
        vm.execute("local normal={a=gift('a'),b=gift('b')};reply({[1]=normal,[2]={}});step()")
        if case=='wrongAccount':
            vm.execute("LuaEntry.Player.uid='1002';step();assert(#state.sends==1 and not engine.IsActive())")
            continue
        vm.execute("state.throw=true;step();assert(#state.sends==2)")
        if case=='partial':vm.execute("local a=DataCenter.AllianceGiftDataManager.giftInfoList[1].a;a.receiveState=1;a.receiveTime=state.now")
        vm.execute("state.now=state.now+16000;engine.Pump();assert(status().state=='unconfirmed'and status().confirmed==0);engine.Stop(request);state.config.allianceGift.enabled=true;engine.Configure(request);step();assert(#state.sends==2,'No replay after uncertain/partial gift send')")
    print('FEATURE018_R30_GIFTS_LUA_PASS fresh-query normal advanced poststate interval zero-off stop partial uncertain no-replay account empty error')

if __name__=='__main__':main()
