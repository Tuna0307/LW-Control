"""Inert Alliance Drill event scheduler checks; no game/input/network."""
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


def main():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.globals().drill = vm.execute((ROOT / 'tools/feature018_drill_engine.lua').read_text(encoding='utf-8'))
    vm.execute('''
      local owner,energy,busy=true,100,{[3]=true}
      local sends,observes,cancelled={},0,0
      local event={active=false,target={uuid='monster',monster=true,player=false},rallies={}}
      local rally={uuid='rally',monsterUuid='monster',monster=true,player=false,joinable=true}
      local stages={};local join_ready=true
      drill.Bind({Owned=function()return owner end,Snapshot=function()return event end,
        Free=function(slot)return not busy[slot]end,Stamina=function()return energy,10 end,
        JoinReady=function()return join_ready end,
        Send=function(o,slot,role,target)
          sends[#sends+1]={slot=slot,role=role,target=target};stages[slot]='chooser';return slot
        end,
        CancelUnsent=function(job)
          if stages[job.slot]=='chooser'then stages[job.slot]=nil;cancelled=cancelled+1;return true end
          return false
        end,
        Observe=function(job,allow)
          observes=observes+1
          if stages[job.slot]=='chooser' then assert(allow);stages[job.slot]='sent';return {step='attacking'}end
          return {acknowledged=true,completed=stages[job.slot]=='returned',receipt={serverAcknowledgementCorrelated=true}}
        end})
      local config={enabled=true,activeRally=true,squadIndexes={3,2,1}}
      assert(not drill.Start({token='x'},{enabled=true,activeRally=true,squadIndexes={2,2}}))
      assert(drill.Start({token='x'},config));drill.Pump();assert(#sends==0 and drill.Status().step=='waiting_event')
      event.active=true;energy=0;drill.Pump();assert(#sends==0 and drill.Status().step=='waiting_stamina')
      energy=100;owner=false;drill.Pump();assert(#sends==0 and drill.Status().step=='account_changed')
      owner=true;event.target.player=true;drill.Pump();assert(#sends==0)
      event.target.player=false;drill.Pump();assert(#sends==1 and sends[1].slot==2 and sends[1].role=='leader')
      event.active=false;drill.Pump();assert(cancelled==1 and observes==0,'Event end must cancel a chooser before dispatch')
      event.active=true;drill.Pump();assert(#sends==2)
      drill.Pump();assert(#sends==2,'One pending leader reservation must suppress duplicate rallies')
      event.rallies={rally};rally.player=true;drill.Pump();assert(#sends==2,'Player rally must never be joined')
      rally.player=false;rally.monsterUuid='other';drill.Pump();assert(#sends==2)
      rally.monsterUuid='monster';join_ready=false;drill.Pump();assert(#sends==2)
      join_ready=true;drill.Pump();assert(#sends==3 and sends[3].role=='member' and sends[3].slot==1)
      assert(drill.Status().confirmedSends==1,'Only native acknowledgement counts')
      drill.Stop();assert(cancelled==2);local before=#sends
      for i=1,5 do drill.Pump()end;assert(#sends==before,'Stop must suppress all new sends')
      stages[2]='returned';drill.Pump()
      assert(drill.Start({token='x'},config));event.target.uuid='new';event.rallies={};drill.Pump()
      event.target.uuid='changed';drill.Pump();assert(cancelled==3,'Changing event target cancels stale unsent chooser')
      drill.Stop();config.activeRally=false;assert(drill.Start({token='x'},config));before=#sends
      drill.Pump();assert(#sends==before,'Join-only option must not launch a rally')
      event.rallies={{uuid='r2',monsterUuid='changed',monster=true,player=false,joinable=true}}
      drill.Pump();assert(#sends==before+1 and sends[#sends].role=='member')
      drill.Stop();drill.Fail('fixture');assert(drill.Status().state=='failed' and not drill.Status().running)
    ''')
    # Owner 2026-10-22: Squad 4 launch only, Squad 1 join only.
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.globals().drill = vm.execute((ROOT / 'tools/feature018_drill_engine.lua').read_text(encoding='utf-8'))
    vm.execute('''
      local sends,stages,busy={},{},{}
      local event={active=true,target={uuid='m',monster=true,player=false},rallies={},ownRallyExists=false}
      drill.Bind({Owned=function()return true end,Snapshot=function()return event end,Free=function(slot)return not busy[slot]end,
        Stamina=function()return 100,10 end,JoinReady=function()return true end,
        Send=function(o,slot,role,target)sends[#sends+1]={slot=slot,role=role};busy[slot]=true;return slot end,
        CancelUnsent=function()return false end,Observe=function()return {step='joining'}end})
      assert(not drill.Start({token='y'},{enabled=true,activeRally=false,squadIndexes={1,4},squadRoles={['4']='both',['9']='join'}}))
      assert(not drill.Start({token='y'},{enabled=true,activeRally=false,squadIndexes={1,4},squadRoles={['4']='lead'}}))
      local config={enabled=true,activeRally=false,squadIndexes={1,4},squadRoles={['1']='join',['4']='launch'}}
      assert(drill.Start({token='y'},config))
      drill.Pump();assert(#sends==1 and sends[1].slot==4 and sends[1].role=='leader','Squad 4 launches even with Launch rallies off; Squad 1 never launches')
      event.rallies={{uuid='r',monsterUuid='m',monster=true,player=false,joinable=true}}
      drill.Pump();assert(#sends==2 and sends[2].slot==1 and sends[2].role=='member','Squad 1 joins')
      busy={};event.ownRallyExists=true;sends={}
      drill.Pump();drill.Pump()
      for _,s in ipairs(sends)do assert(not(s.slot==4 and s.role=='member'),'Launch-only squad never joins')end
    ''')
    print('FEATURE018_DRILL_ENGINE_CHECKS_OK: event lifecycle, priority, Stop, limits, stamina/account pause, monster-only joins, per-squad launch/join roles')


if __name__ == '__main__':
    main()
