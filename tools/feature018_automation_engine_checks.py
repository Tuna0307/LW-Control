"""Actual R30 scheduler + native adapters in inert Lua; no game, network or installation access."""
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime

def runtime():
    vm=LuaRuntime(unpack_returned_tuples=True)
    vm.execute('''
      state={uid='1001',server=123,token='owner',now=1000000,reads=0,sends={},perf=0}
      LuaEntry={Player={uid='1001',GetSourceServerId=function()state.reads=state.reads+1;return state.server end,
        GetSelfServerId=function()return state.server end,GetCurServerId=function()return state.server end}}
      UITimeManager={GetInstance=function()state.reads=state.reads+1;return {GetServerTime=function()return state.now end}end}
      DataCenter={AllianceHelpDataManager={GetOtherHelpList=function()state.reads=state.reads+1;if state.bad then error('game error')end;return state.list or {}end}}
      MsgDefines={AlHelpAll=71}
      SFSNetwork={SendMessage=function(route,at)
        state.sends[#state.sends+1]={route=route,at=at}
        if state.throw then error('uncertain send')end
      end}
      package.preload.rapidjson=function()return {decode=function(s)assert(s=='config');return state.config end}end
      state.config={allianceHelp={enabled=false}}
      request={authorizedUid='1001',confirmedAutomationConfig='config',kind='allianceHelp'}
      bridge={Feature018OwnerToken=function()return state.token end,
        PerformanceBegin=function()return 1 end,PerformanceEnd=function(kind)assert(kind=='automation_allianceHelp');state.perf=state.perf+1 end}
      feature={RegisterHandler=function()end,Array=function(x)return x end}
    ''')
    vm.globals().native=vm.execute((ROOT/'tools/feature018_automation_native.lua').read_text())
    vm.globals().engine=vm.execute((ROOT/'tools/feature018_automation_engine.lua').read_text())
    vm.execute('engine.Register(feature,bridge,native);assert(state.reads==0 and #state.sends==0)')
    return vm

def main():
    vm=runtime()
    vm.execute('''
      assert(engine.Configure(request).state=='confirmed');local n=state.reads
      for i=1,1000 do engine.Pump()end
      assert(state.reads==n and #state.sends==0 and state.perf==0,'Off must have zero game reads/work')
      state.config.allianceHelp.enabled=true;assert(engine.Configure(request).state=='confirmed')
      state.list={a={uuid=1},b={uuid=2}};engine.Pump()
      assert(#state.sends==1 and state.sends[1].route==71 and state.sends[1].at==state.now)
      assert(engine.Status().data.services.allianceHelp.confirmed==0,'Send is not success')
      engine.Pump();assert(#state.sends==1,'Slow timer prevents a per-tick scan')
      state.list={};state.now=state.now+1000;engine.Pump()
      local r=engine.Status().data.services.allianceHelp
      assert(r.state=='success'and r.confirmed==1 and r.receipts[1].before.available==2 and r.receipts[1].after.available==0)
      state.config.allianceHelp.enabled=false;engine.Configure(request);local n=state.reads
      state.now=state.now+60000;engine.Pump();assert(state.reads==n and not engine.IsActive())
    ''')
    for case in ['empty','error','wrongAccount','wrongLease','uncertain','offPending','runNow']:
        vm=runtime();vm.execute("state.config.allianceHelp.enabled=true;engine.Configure(request);state.list={a={uuid=1}}")
        if case=='empty':
            vm.execute("state.list={};engine.Pump();assert(#state.sends==0 and engine.Status().data.services.allianceHelp.state=='nothing_available')")
        elif case=='error':
            vm.execute("state.bad=true;engine.Pump();assert(#state.sends==0 and engine.Status().data.services.allianceHelp.state=='error')")
        elif case=='wrongAccount':
            vm.execute("LuaEntry.Player.uid='1002';engine.Pump();assert(#state.sends==0 and not engine.IsActive());assert(engine.Configure(request).state=='failed')")
        elif case=='wrongLease':
            vm.execute("state.token='replacement';engine.Pump();assert(#state.sends==0 and not engine.IsActive())")
        elif case in ['uncertain','offPending']:
            vm.execute("state.throw=true;engine.Pump();assert(#state.sends==1)")
            if case=='offPending':vm.execute("engine.Stop(request);local n=state.reads;state.now=state.now+30000;engine.Pump();assert(state.reads==n)")
            else:vm.execute("state.now=state.now+16000;engine.Pump();assert(engine.Status().data.services.allianceHelp.state=='unconfirmed')")
            vm.execute("state.config.allianceHelp.enabled=false;engine.Configure(request);state.config.allianceHelp.enabled=true;engine.Configure(request);state.now=state.now+61000;engine.Pump();assert(#state.sends==1,'Never replay uncertain send after off/on');assert(engine.Run(request).state=='failed')")
        else:
            vm.execute("state.config.allianceHelp.enabled=false;engine.Configure(request);assert(engine.Run(request).state=='accepted');engine.Pump();assert(#state.sends==1);state.list={};state.now=state.now+1000;engine.Pump();assert(not engine.IsActive())")
    vm=runtime()
    vm.execute('''state.config.allianceHelp.enabled=true;engine.Configure(request);engine.Stop(request)
      assert(engine.Status(request).state=='confirmed'and not engine.IsActive(),'Failed disk save/status must never restart stopped engine')
      engine.Configure(request);assert(engine.IsActive(),'Explicit successful configure can re-enable')''')
    # Owner direction 2026-10-21: switching Help on checks at once, then every 3 s (not every minute).
    vm=runtime()
    vm.execute('''
      state.config.allianceHelp.enabled=true;assert(engine.Configure(request).state=='confirmed')
      engine.Pump();local first=state.reads;assert(first>0,'Switch-on checks immediately')
      state.now=state.now+1000;engine.Pump();state.now=state.now+1000;engine.Pump();local mid=state.reads
      state.now=state.now+1500;engine.Pump();assert(state.reads>mid,'Help re-checks within about 3 s')
      assert(engine.Status().data.services.allianceHelp.nextRunAt-state.now<=3000)
    ''')
    print('FEATURE018_R30_ALLIANCE_HELP_LUA_PASS off=zero reads happy=poststate stop uncertain=no-replay wrong-account wrong-lease empty game-error run-now perf')

if __name__=='__main__':main()
