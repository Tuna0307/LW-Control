"""Inert stamina engine checks; no game, item use or purchases."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


def main():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.globals().potion = vm.execute((ROOT / 'tools/feature018_stamina_engine.lua').read_text(encoding='utf-8'))
    vm.execute('''
      local owned,stamina,clock,sends=true,9,0,0
      local items={{id=2,uuid='50',count=2,recovery=50,staminaItem=true},
        {id=1,uuid='10',count=3,recovery=10,staminaItem=true},
        {id=3,uuid='not-stamina',count=8,recovery=1,staminaItem=false}}
      local selected
      potion.Bind({Owned=function()return owned end,Now=function()return clock end,
        Snapshot=function()return {stamina=stamina,items=items}end,
        Use=function(owner,item)sends=sends+1;selected=item;return true end})
      local owner={token='session'}
      assert(potion.Start(owner,{enabled=true,minStamina=10,preferFifty=false}))
      owned=false;potion.Pump();assert(sends==0 and potion.Status().step=='account_changed')
      owned=true;potion.Pump();assert(sends==1 and selected.id==1 and potion.IsPending())
      potion.Pump();assert(sends==1 and potion.Status().used==0,'Send alone must not confirm item use')
      items[2].count=2;potion.Pump();assert(potion.Status().used==0,'Count decrease alone must not confirm stamina')
      stamina=19;potion.Pump();assert(potion.Status().used==1 and not potion.IsPending())
      potion.Pump();assert(sends==1 and potion.Status().step=='above_limit')
      potion.Stop();stamina=1;potion.Pump();assert(sends==1)
      assert(potion.Start(owner,{enabled=true,minStamina=10,preferFifty=true}))
      potion.Pump();assert(sends==2 and selected.id==2)
      potion.Stop();potion.Pump();assert(sends==2,'Stop cannot repeat pending item use')
      clock=31;potion.Pump();assert(potion.Status().state=='failed' and potion.IsPending())
      assert(not potion.Start(owner,{enabled=true,minStamina=10,preferFifty=false}),'Uncertain item must remain fenced')
      items[1].count=1;stamina=51;potion.Pump();assert(not potion.IsPending() and not potion.Status().running)
      items={};assert(potion.Start(owner,{enabled=true,minStamina=100,preferFifty=false}))
      potion.Pump();assert(sends==2 and potion.Status().step=='no_owned_items','Empty stock must never buy with gems')
      local snapshotFail=true
      potion.Bind({Owned=function()return true end,Now=function()return clock end,
        Snapshot=function()if snapshotFail then return nil,'TEMP_SNAPSHOT_FAILURE'end;return {stamina=100,items={}}end,
        Use=function()error('Recovered high stamina must not use an item')end})
      local recoveredOwner={token='recovered-session'}
      assert(potion.Start(recoveredOwner,{enabled=true,minStamina=10,preferFifty=false}))
      potion.Pump();assert(potion.Status().lastError=='TEMP_SNAPSHOT_FAILURE')
      snapshotFail=false;potion.Pump();assert(potion.Status().lastError==nil and potion.Status().step=='above_limit','Recovered stamina read must clear stale lastError')
      potion.Stop()
    ''')
    print('FEATURE018_STAMINA_ENGINE_CHECKS_OK: threshold, cheapest/prefer-50, exact count plus stamina, Stop/account/uncertainty fences, no purchase, transient error recovery')


if __name__ == '__main__':
    main()
