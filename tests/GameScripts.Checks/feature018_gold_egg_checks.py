"""Inert source-shaped Gold Egg classification, migration and one-send checks."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests' / 'GameScripts.Checks'))
from feature018_chat_outcome_checks import setup, LuaRuntime


def runtime(legacy=False):
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(setup)
    vm.execute("""
      PostType.S0_ALLIANCE_BOSS_GIFT_REWARD=695;PostType.EasterEggChat=710
      LuaEntry.Player.IsInSelfServer=function()return state.atHome~=false end
      templates={
        [1]={type=1,special_type=1,goodsId=653052},[100]={type=1,special_type=2},
        [201]={type=3,special_type=3},[301]={type=2,special_type=4},
        [401]={type=5,special_type=5,goodsId=670001},[2]={type=1,goodsId=653053}}
      for _,t in pairs(templates)do
        t.Condition=function()return not state.refuse end
        t.IsCanCrossServer=function()return state.nativeCross~=false end
      end
      DataCenter.RedPacketTemplateManager.GetTemplate=function(self,id)return templates[id]end
      GetTableData=function(table,id,key,default)return id==671001 and 1 or 0 end
      state.config={redPacket={enabled=false,claimDelaySeconds={2,2}},fireworks={enabled=false,claimDelaySeconds={3,3}},
        goldEgg={enabled=true,claimDelaySeconds={0,0},crossServerEnabled=false},treasure={enabled=false}}
      fw={HandleMessage=function(self,data)return 'original'end}
      rp={HandleMessage=function(self,data)return 'original'end}
      package.preload['Net.Msgs.GetFireworksGiftMessage']=function()return fw end
      package.preload['Net.Msgs.OpenRedPacketMessage']=function()return rp end
      function packet(template,uuid,seq,server)
        local m=msg(611,uuid,seq);m.extra.customJsonParam.packetId=template
        m.extra.customJsonParam.targetServer=server or 123;return m
      end
      function boss(uuid,post,seq)
        local m=msg(post or 695,uuid,seq);m.extra.customJsonParam.configId=671001;return m
      end
      nativeGift={uuid=8001,ownerUid='2001',configId=671001,type=1,
        CheckIsAvailable=function(self)self.isAvailable=state.nativeEligible~=false end,
        IsReachLimit=function()return state.nativeFull==true end}
      DataCenter.LWFireworkGiftManager.GetGiftQueueByPlayerUid=function(self,uid)
        assert(uid=='2001');return {ToArray=function()return state.noGift and {}or {nativeGift}end}
      end
    """)
    vm.globals().engine = vm.execute((ROOT / 'src/LWBridge.GameScripts/feature018_chat_engine.lua').read_text('utf-8'))
    if legacy:
        vm.execute("state.config.goldEgg={enabled=true,legacyCombined=true};state.config.redPacket.enabled=true")
    vm.execute("engine.Register(feature,overview);engine.Configure(request);state.now=state.now+16001")
    return vm


def main():
    for template in (100, 201, 301, 401, 1):
        vm = runtime();vm.globals().templateId = template
        vm.execute("""
          state.messages={packet(templateId,'gold-one',1)};engine.Pump()
          assert(#state.sent==1 and state.sent[1][1]==10,'Known egg uses original native packet route')
          assert(engine.Status().data.services.goldEgg.submitted==1)
          rp:HandleMessage({redPacket={uuid='gold-one'}})
          assert(engine.Status().data.services.goldEgg.claimed==1)
          assert(engine.Status().data.services.redPacket.claimed==0)
        """)
    vm = runtime()
    vm.execute("""
      state.messages={packet(2,'normal',1),msg(693,'7001',2),packet(100,'remote',3,124)};engine.Pump()
      assert(#state.sent==0,'Gold alone must not enable ordinary packets/fireworks or remote eggs')
      state.config.goldEgg.ignoreBirthday=true;engine.Configure(request)
      state.callbacks[1](packet(401,'birthday',4));engine.Pump();assert(#state.sent==0)
      state.config.goldEgg.crossServerEnabled=true;state.nativeCross=false;engine.Configure(request)
      state.callbacks[1](packet(201,'cross-refused',5,124));engine.Pump();assert(#state.sent==0)
      state.nativeCross=true;state.callbacks[1](packet(201,'cross-allowed',6,124));engine.Pump();assert(#state.sent==1)
      state.config.goldEgg.claimDelaySeconds={1,1};engine.Configure(request)
      state.callbacks[1](packet(100,'cross-off-before-send',7,124))
      state.config.goldEgg.crossServerEnabled=false;engine.Configure(request);state.now=state.now+2000;engine.Pump()
      assert(#state.sent==1,'Cross-server opt-out is rechecked at actual send')
    """)
    vm = runtime(legacy=True)
    vm.execute("""
      state.messages={packet(100,'legacy-tax',1),boss('8001',693,2),boss('8002',695,3)};engine.Pump()
      assert(#state.sent==0,'Legacy Gold keeps original per-family switch/delay and no new 695 sends')
      state.now=state.now+2000;engine.Pump();assert(#state.sent==1 and state.sent[1][1]==10)
      state.config.goldEgg={enabled=false};engine.Configure(request)
      state.callbacks[1](packet(201,'explicit-off',4));state.now=state.now+10000;engine.Pump();assert(#state.sent==1)
    """)
    vm = runtime()
    vm.execute("""
      state.messages={boss('8001',695,1),boss('8001',693,2)};engine.Pump()
      assert(#state.sent==1 and state.sent[1][1]==11,'Dual announcement is one native gift request')
      state.got=8001;engine.Pump();assert(engine.Status().data.services.goldEgg.claimed==0)
      fw:HandleMessage({uuid=8001});fw:HandleMessage({uuid=8001})
      assert(engine.Status().data.services.goldEgg.claimed==1 and engine.Status().data.services.fireworks.claimed==0)
      engine.Run({authorizedUid='1001',confirmedChatConfig='config',kind='goldEgg'});assert(#state.sent==1)
    """)
    for reason in ('noGift', 'nativeFull', 'nativeEligible', 'atHome'):
        vm = runtime();vm.globals().reason = reason
        vm.execute("""
          if reason=='nativeEligible'or reason=='atHome'then state[reason]=false else state[reason]=true end
          state.messages={boss('8001',695,1)};engine.Pump()
          for i=1,30 do state.now=state.now+100;engine.Pump()end
          assert(#state.sent==0,'Native eligibility must refuse '..reason)
        """)
    vm = runtime()
    vm.execute("state.messages={msg(710,'9001',1)};engine.Pump();assert(#state.sent==0,'An event announcement must not invent a generic reward claim')")
    print('GOLD_EGG_CHECKS_PASS native packet families filters independent toggles legacy rules cross opt-out exact gift eligibility route dedupe native outcomes')


if __name__ == '__main__':main()
