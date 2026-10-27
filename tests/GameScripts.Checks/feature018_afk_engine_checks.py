"""Inert Lua scheduler checks. No game process, input or network."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


def main():
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.globals().engine = lua.execute((ROOT / 'src/LWBridge.GameScripts/feature018_afk_engine.lua').read_text(encoding='utf-8'))
    lua.execute('''
        local sends, owner, stamina, complete, busy = 0, true, 100, false, false
        local target={uuid='monster',monster=true,player=false,monsterType=0,special=0,
            level=10,nameKey='zombie',name='Zombie',monsterId=10,distance=20}
        local adapter={Owned=function()return owner end, Free=function()return not busy end,
            Stamina=function()return stamina,5 end,Targets=function()return {target} end,
            Send=function()sends=sends+1;return 'native-token' end,
            Observe=function()return {completed=complete,receipt={serverAcknowledgementCorrelated=true}}end,
            MaxLevel=function()return 30 end}
        engine.Bind(adapter)
        local profile={id='p',enabled=true,kind='farm',monsterType=0,monsterNameKey='zombie',
            squadIndexes={1,2},executionLimit=3,levelFilterEnabled=true,minLevel=10,maxLevel=10,
            distanceFilterEnabled=true,maxDistance=25}
        local config={enabled=true,strategies={profile}}
        assert(engine.Start({},config))
        stamina=0;engine.Pump();assert(sends==0 and engine.Status().state=='waiting')
        stamina=100;busy=true;engine.Pump();assert(sends==0)
        busy=false;owner=false;engine.Pump();assert(sends==0 and engine.Status().step=='account_changed')
        owner=true;engine.Pump();assert(sends==2)
        engine.Pump();assert(sends==2,'An unacknowledged send must not be replayed')
        complete=true;engine.Pump();engine.Pump();assert(sends==3,'Count must reserve across assigned squads')
        engine.Pump();engine.Pump();assert(sends==3 and engine.Status().state=='completed')
        assert(engine.Status().processed.p==3)
        assert(engine.Configure(config));engine.Pump()
        assert(sends==3 and engine.Status().state=='completed','Saving completed profile settings must not restart attacks')
        profile.executionLimit=0;assert(engine.Start({},config));complete=false;engine.Pump()
        local before=sends;engine.Stop();for i=1,10 do engine.Pump()end;assert(sends==before)
        assert(not engine.Matches(profile,{monster=true,player=true,level=10}))
        local bad={};for k,v in pairs(target)do bad[k]=v end
        bad.monster=false;assert(not engine.Matches(profile,bad));bad.monster=true
        bad.worldBoss=true;assert(not engine.Matches(profile,bad));bad.worldBoss=false
        bad.rallyOnly=true;assert(not engine.Matches(profile,bad));bad.rallyOnly=false
        bad.distance=26;assert(not engine.Matches(profile,bad));bad.distance=20
        bad.level=9;assert(not engine.Matches(profile,bad));bad.level=10
        bad.nameKey='other';assert(not engine.Matches(profile,bad))
        profile.monsterNameKey=nil;profile.targetNameQuery='zOmB';assert(engine.Matches(profile,target))
        profile.kind='join';profile.rally=true;assert(not engine.Matches(profile,target))
        target.rally=true;target.joinable=true;assert(engine.Matches(profile,target))
        target.leaderUid='leader';target.memberCount=1;target.departureTime=120;target.leaderHeroCount=5
        local r={enabled=true,mode='slot',slotRange={3,3},slotDelaySeconds=1,
            maxWaitMinutes=1,leaderListMode='whitelist',leaders={{uid='leader'}},skipSoloLeader=true,skipKicked=true}
        local wait={};local ready,reason=engine.JoinReady(r,target,wait,100)
        assert(not ready and reason=='waiting_join_delay')
        target.memberCount=2;assert(not engine.JoinReady(r,target,wait,100))
        assert(engine.JoinReady(r,target,wait,101))
        target.kicked=true;assert(not engine.JoinReady(r,target,wait,102));target.kicked=false
        target.leaderHeroCount=1;assert(not engine.JoinReady(r,target,wait,102));target.leaderHeroCount=5
        r.leaderListMode='blacklist';assert(not engine.JoinReady(r,target,wait,102));r.leaderListMode='off'
        target.departureTime=300;assert(not engine.JoinReady(r,target,wait,102));target.departureTime=120
        -- Audit 2026-10-23 AUDIT-05: position 3 already taken (3 members), seats left -> join now, no delay.
        target.memberCount=3;assert(engine.JoinReady(r,target,{},200),'Occupied position joins immediately')
        r.leaderListMode='blacklist';assert(not engine.JoinReady(r,target,{},200),'Restrictions still apply');r.leaderListMode='off'
        target.memberCount=2
        r.mode='delay';r.delaySeconds={2,4};wait={}
        assert(not engine.JoinReady(r,target,wait,100,function()return 0.5 end))
        assert(not engine.JoinReady(r,target,wait,102,function()return 0 end),'Delay must not reroll')
        assert(engine.JoinReady(r,target,wait,103))
        r.enabled=false;target.player=true;assert(not engine.JoinReady(r,target,{},103))
    ''')
    lua.execute('''
        local sends,cancels=0,0
        engine.Bind({Owned=function()return true end,Free=function()return true end,
            Stamina=function()return 100,10 end,Targets=function()return {{uuid='m',monster=true,level=10,monsterType=0,special=0}}end,
            Send=function()sends=sends+1;return 'chooser'end,
            CancelUnsent=function()cancels=cancels+1;return true end,
            Observe=function()error('Disabled unsent profile must not dispatch its chooser')end})
        engine.Stop();local c={enabled=true,strategies={{id='p',enabled=true,kind='farm',monsterType=0,rally=true,squadIndexes={1},executionLimit=0}}}
        assert(engine.Start({},c));engine.Pump();assert(sends==1)
        c.strategies={};assert(engine.Configure(c));engine.Pump()
        assert(sends==1 and cancels==1 and engine.Status().processed.p==0)
    ''')
    lua.execute('''
        local fail=true
        engine.Bind({Owned=function()return true end,Free=function()return true end,
            Stamina=function()return 100,10 end,
            Targets=function()return {{uuid='m2',monster=true,level=10,monsterType=0,special=0}}end,
            Send=function()if fail then fail=false;return nil,'TEMP_SEND_FAILURE' end;return 'native-token'end,
            Observe=function()return {completed=false}end,MaxLevel=function()return 30 end})
        local c={enabled=true,strategies={{id='recover',enabled=true,kind='farm',monsterType=0,
            squadIndexes={1},executionLimit=0,levelFilterEnabled=true,minLevel=10,maxLevel=10}}}
        assert(engine.Start({},c));engine.Pump();assert(engine.Status().lastError=='TEMP_SEND_FAILURE')
        engine.Pump();assert(engine.Status().lastError==nil,'Recovered AFK cycle must clear stale lastError')
        engine.Stop()
    ''')
    # Owner 2026-10-22: per-squad level ranges inside one profile (Squad 1 1-80, Squad 2 1-40).
    lua.execute('''
        local sent={}
        local lvl=60
        engine.Bind({Owned=function()return true end,Free=function()return true end,Stamina=function()return 100,5 end,
            Targets=function()return {{uuid='m'..lvl,monster=true,player=false,monsterType=0,level=lvl,nameKey='z'}}end,
            Send=function(o,slot)sent[#sent+1]=slot;return 'n'..slot end,Observe=function()return {completed=false}end,MaxLevel=function()return 99 end})
        local p={id='j',enabled=true,kind='farm',monsterType=0,squadIndexes={1,2},executionLimit=0,
            squadLevels={['1']={min=1,max=80},['2']={min=1,max=40}}}
        assert(not engine.Start({},{enabled=true,strategies={{id='bad',enabled=true,kind='farm',monsterType=0,squadIndexes={1},executionLimit=0,squadLevels={['1']={min=5,max=2}}}}}))
        assert(engine.Start({},{enabled=true,strategies={p}}))
        engine.Pump();assert(#sent==1 and sent[1]==1,'A level-60 target suits Squad 1 (1-80) but not Squad 2 (1-40)')
        engine.Stop();sent={};lvl=30
        assert(engine.Start({},{enabled=true,strategies={p}}));engine.Pump();assert(#sent==2,'A level-30 target suits both squads')
        engine.Stop()
    ''')
    for name in ['feature018_afk_native.lua', 'feature018_attack_actions.lua']:
        source = (ROOT / 'src/LWBridge.GameScripts' / name).read_text(encoding='utf-8')
        assert lua.eval('function(s) local f,e=load(s);return f~=nil,e end')(source)[0], name
    print('FEATURE018_AFK_ENGINE_CHECKS_OK: lifecycle, shared limits, pause, Stop, typed monster filters, transient error recovery, per-squad level ranges')


if __name__ == '__main__':
    main()
