"""Inert exact-position/UID application confirmation; never opens a game or sends network traffic."""
from feature018_automation_engine_checks import runtime

def official():
    vm=runtime()
    vm.execute('''
      state.config={allianceHelp={enabled=false},officialPosition={enabled=true,positionId=10005}}
      request.kind='officialPosition';state.applicants={};state.appointments={};state.callbacks={};state.ownApply=0;state.held=0
      DataCenter.OfficialApplyManager={GetApplyList=function(self,pos)assert(pos=='10005');return state.applicants end,
        GetAppointmentList=function(self,pos)assert(pos=='10005');return state.appointments end,
        GetOwnAppointIdName=function()return state.held,'position' end,GetOwnApplyPositionId=function()return state.ownApply end,
        CheckCanApply=function(self,pos)return not state.unavailable end,GetOwnApplicantData=function()return state.reason end}
      EventId={OfficialApplyListInit=14,OfficialAppointmentListRefresh=15}
      local em={AddListener=function(self,event,cb)state.callbacks[event]=cb end,
        RemoveListener=function(self,event,cb)if state.callbacks[event]==cb then state.callbacks[event]=nil end end}
      EventManager={GetInstance=function()return em end}
      MsgDefines.KingdomPositionApply=90;MsgDefines.KingdomPositionApplyList=91;MsgDefines.KingdomPositionAppointmentList=92
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_officialPosition');state.perf=state.perf+1 end
      SFSNetwork.SendMessage=function(route,pos)assert(type(pos)=='string'and pos=='10005');state.sends[#state.sends+1]={route=route,pos=pos};if state.throw and route==90 then error('uncertain application')end end
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services.officialPosition end
      function prepare()step();assert(state.sends[1].route==91);state.callbacks[14](10005);step();assert(state.sends[2].route==92);state.callbacks[15](10005);step()end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm

def main():
    vm=official()
    vm.execute('''
      state.config.officialPosition.enabled=false;engine.Configure(request);local n=state.reads
      for _=1,100 do engine.Pump()end;assert(state.reads==n and #state.sends==0)
      state.config.officialPosition.enabled=true;engine.Configure(request);prepare();step()
      assert(#state.sends==3 and state.sends[3].route==90 and status().confirmed==0)
      state.ownApply=10005;step()
      assert(#state.sends==5 and state.sends[4].route==91 and status().confirmed==0,'Optimistic ownApply ID is not proof')
      state.applicants={{uid='1002'}};state.callbacks[14](10005);state.callbacks[15](10005);step();assert(status().confirmed==0,'Another applicant is not our UID')
      state.applicants={{uid='1001'}};state.callbacks[14](10005);step()
      assert(status().state=='success'and status().confirmed==1 and status().positionId==10005 and status().readRequests==4)
      engine.Stop(request);assert(not state.callbacks[14]and not engine.IsActive())
    ''')
    for case in ['none','alreadyQueued','held','otherPosition','unavailable','reason','error','wrongAccount','uncertain','stop']:
        vm=official()
        if case=='none':
            vm.execute("state.config.officialPosition.positionId=0;engine.Configure(request);step();assert(#state.sends==0 and status().state=='nothing_available')")
            continue
        vm.execute('prepare()')
        if case=='alreadyQueued':vm.execute("state.applicants={{uid='1001'}}")
        elif case=='held':vm.execute('state.held=10005')
        elif case=='otherPosition':vm.execute('state.ownApply=10006')
        elif case=='unavailable':vm.execute('state.unavailable=true')
        elif case=='reason':vm.execute("state.reason='Headquarters level too low'")
        elif case=='error':vm.execute("DataCenter.OfficialApplyManager.CheckCanApply=function()error('game error')end")
        elif case=='wrongAccount':vm.execute("LuaEntry.Player.uid='1002'")
        elif case=='stop':vm.execute('engine.Stop(request)')
        else:vm.execute('state.throw=true')
        vm.execute('step()')
        if case in ['alreadyQueued','held','otherPosition','unavailable','reason']:
            vm.execute("assert(#state.sends==2 and status().state=='nothing_available')")
        elif case in ['wrongAccount','stop']:vm.execute('assert(#state.sends==2 and not engine.IsActive())')
        elif case=='error':vm.execute("assert(#state.sends==2 and status().state=='error')")
        else:
            vm.execute("step();state.now=state.now+16000;engine.Pump();assert(status().state=='unconfirmed');engine.Stop(request);state.config.officialPosition.enabled=true;engine.Configure(request);state.now=state.now+61000;engine.Pump();local count=0;for _,s in ipairs(state.sends)do if s.route==90 then count=count+1 end end;assert(count==1,'Never repeat an uncertain application')")
    vm=official()
    vm.execute('''state.config.officialPosition.enabled=false;engine.Configure(request)
      assert(engine.Observe(request).state=='accepted'and #state.sends==2 and not engine.IsActive())
      state.appointments={{uid='1001'}};state.callbacks[14]('10005');state.callbacks[15]('10005')
      local r=engine.Observe(request);assert(r.state=='confirmed'and r.data.appointed and r.data.readOnly and #state.sends==2)
      assert(not state.callbacks[14]and not state.callbacks[15])''')
    vm=official()
    vm.execute('''prepare();step();step();state.appointments={{uid='1001'}};state.callbacks[14](10005);state.callbacks[15](10005);step()
      assert(status().state=='success'and status().confirmed==1,'Automatically accepted appointment is also an exact native post-state')''')
    # Owner 2026-10-21: full list -> keep checking (apply list only, every 3 s) and apply when a place opens.
    vm=official()
    vm.execute('''
      DataConfig={TryGetNum=function(self,t,k)assert(t=='auto_wonder_config'and k=='k3');return 2 end}
      local reply={HandleMessage=function(self,r)state.handled=(state.handled or 0)+1 end}
      local original=reply.HandleMessage
      package.preload['Net.Msgs.Government.OfficialApply.KingdomPositionApplyMessage']=function()return reply end
      function applies()local n=0;for _,s in ipairs(state.sends)do if s.route==90 then n=n+1 end end;return n end
      state.applicants={{uid='2001'},{uid='2002'}}
      prepare();step()
      assert(applies()==0 and status().state=='waiting'and status().before.full==true,'Full list must wait, not apply')
      local reads=#state.sends
      local answered=#state.sends
      for _=1,30 do step();if #state.sends>answered then answered=#state.sends;state.callbacks[state.sends[answered].route==91 and 14 or 15](10005)end end
      local listReads,appointmentReads=0,0;for i=reads+1,#state.sends do local r=state.sends[i].route
        assert(r==91 or r==92,'Only lists are read while full');if r==91 then listReads=listReads+1 else appointmentReads=appointmentReads+1 end end
      assert(appointmentReads<=2,'Appointment list only on the 30 s full refresh')
      assert(applies()==0 and listReads>=8 and listReads<=11,'Full list re-read about every 3 s: '..listReads)
      -- A place opens: the very next check applies.
      state.applicants={{uid='2001'}}
      for _=1,4 do step();if #state.sends>answered and state.sends[#state.sends].route~=90 then answered=#state.sends;state.callbacks[state.sends[answered].route==91 and 14 or 15](10005)end end
      assert(applies()==1,'Apply within one 3 s check after the list has room')
      -- The server refuses (another player took the place): retry is allowed, not a replay.
      reply:HandleMessage({errorCode='E_FULL'});step()
      assert(status().state=='waiting'and status().refused==1 and status().confirmed==0)
      assert(state.handled==1,'Game reply handler still runs')
      state.applicants={{uid='2001'}};for _=1,8 do step();if state.sends[#state.sends].route==91 then state.callbacks[14](10005)end;if state.sends[#state.sends].route==92 then state.callbacks[15](10005)end end
      assert(applies()==2,'Refused application is retried')
      -- Second attempt is accepted and our UID is in the fresh list.
      reply:HandleMessage({});step();state.applicants={{uid='2001'},{uid='1001'}};state.callbacks[14](10005);state.callbacks[15](10005);step()
      assert(status().state=='success'and status().confirmed==1 and applies()==2)
      engine.Stop(request);assert(reply.HandleMessage==original,'Reply observer removed on Stop')
    ''')
    # Server answered OK but the lists do not show us: accepted, never resent.
    vm=official()
    vm.execute('''
      local reply={HandleMessage=function()end}
      package.preload['Net.Msgs.Government.OfficialApply.KingdomPositionApplyMessage']=function()return reply end
      prepare();step();assert(state.sends[3].route==90)
      reply:HandleMessage({});step();state.callbacks[14](10005);state.callbacks[15](10005);step()
      assert(status().state=='success'and status().after.serverAccepted==true)
    ''')
    print('FEATURE018_R30_OFFICIAL_LUA_PASS exact-position exact-UID fresh-list optimistic-rejection off stop account nothing error uncertain no-replay full-retry refusal-retry')

if __name__=='__main__':main()
