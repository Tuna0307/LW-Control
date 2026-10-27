"""Distinguishing inert Chat outcome/correlation/cleanup tests; no live game."""
import ast
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "artifacts/map-013-lua-site"))
from lupa.lua53 import LuaRuntime

fixture = ast.parse((ROOT / "tests/GameScripts.Checks/feature018_chat_engine_checks.py").read_text("utf-8"))
main = next(n for n in fixture.body if isinstance(n, ast.FunctionDef) and n.name == "main")
setup = next(n.value.args[0].value for n in main.body if isinstance(n, ast.Expr)
             and isinstance(n.value, ast.Call) and isinstance(n.value.func, ast.Attribute)
             and n.value.func.attr == "execute")


def runtime(source=None):
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute(setup)
    vm.execute("""
      fw={HandleMessage=function(self,data)
        state.originalCalls=(state.originalCalls or 0)+1
        if state.handlerThrows then error('original error')end
        if data.errorCode=='firework_tips_1016'then state.got=tonumber(data.uuid)end
        return 'original',nil,7
      end}
      rp={HandleMessage=function()return 'red original'end}
      originalFw=fw.HandleMessage;originalRp=rp.HandleMessage
      package.preload['Net.Msgs.GetFireworksGiftMessage']=function()return fw end
      package.preload['Net.Msgs.OpenRedPacketMessage']=function()return rp end
      state.config.fireworks.replyEnabled=true
      state.config.fireworks.replyDelaySeconds={2,2};state.config.fireworks.replies={'thanks'}
    """)
    vm.globals().engine = vm.execute(source or (ROOT / "src/LWBridge.GameScripts/feature018_chat_engine.lua").read_text("utf-8"))
    vm.execute("""
      engine.Register(feature,overview);engine.Configure(request)
      state.messages={msg(693,'7001',1)};state.now=state.now+16001;engine.Pump()
      assert(#state.sent==1 and engine.Status().data.services.fireworks.claimed==0)
    """)
    return vm


def main():
    vm = runtime()
    vm.execute("""
      state.got=7001;engine.Pump()
      local s=engine.Status().data.services.fireworks
      assert(s.claimed==0 and s.unconfirmed==1,'Got-cache alone must not credit a new reward')
      state.now=state.now+20000;engine.Pump();assert(state.replyCount==0)
      engine.Run({kind='fireworks',authorizedUid='1001',confirmedChatConfig='config'})
      assert(#state.sent==1,'Unknown/cache-only outcome must never replay')
      local a,b,c=fw:HandleMessage({uuid=7001})
      assert(a=='original'and b==nil and c==7,'Observer preserves original return arity')
      s=engine.Status().data.services.fireworks
      assert(s.claimed==1 and s.unconfirmed==0,'Late identified success resolves unknown once')
      fw:HandleMessage({uuid=7001});state.callbacks[3]({uuid=7001})
      assert(engine.Status().data.services.fireworks.claimed==1)
      state.now=state.now+2001;engine.Pump();assert(state.replyCount==1)
    """)
    for code, field in [("firework_tips_1016", "alreadyClaimed"), ("firework_tips_1015", "exhausted"),
                        ("firework_tips_1014", "expired"), ("other-error", "refused")]:
        vm = runtime()
        vm.globals().responseCode = code
        vm.globals().counterField = field
        vm.execute("""
          fw:HandleMessage({uuid=8001,errorCode=responseCode})
          fw:HandleMessage({errorCode=responseCode})
          fw:HandleMessage({uuid=7001,serverId=999,errorCode=responseCode})
          local s=engine.Status().data.services.fireworks;assert((s[counterField]or 0)==0)
          fw:HandleMessage({uuid=7001,serverId=123,errorCode=responseCode})
          s=engine.Status().data.services.fireworks
          assert(s[counterField]==1 and s.claimed==0)
          fw:HandleMessage({uuid=7001,errorCode=responseCode});state.callbacks[3]({uuid=7001})
          state.now=state.now+20000;engine.Pump();assert(state.replyCount==0)
          engine.Run({kind='fireworks',authorizedUid='1001',confirmedChatConfig='config'})
          assert(#state.sent==1 and s[counterField]==1,'Terminal refusals retain one-send tombstone')
        """)
    vm = runtime()
    vm.execute("""
      local a,b=msg(693,'8002',2),msg(693,'8002',3)
      b.extra.customJsonParam.targetServer=124;b.extra.customJsonParam.serverId=124
      state.messages={a,b};state.now=state.now+16001;engine.Pump()
      assert(#state.sent==3)
      fw:HandleMessage({uuid=8002})
      assert(engine.Status().data.services.fireworks.claimed==0,'UUID-only response cannot credit two servers')
      fw:HandleMessage({uuid=8002,serverId=124})
      assert(engine.Status().data.services.fireworks.claimed==1,'Exact server resolves one target')
    """)
    vm = runtime()
    vm.execute("""
      engine.Configure(request);local before=state.originalCalls or 0
      fw:HandleMessage({uuid=8001});assert(state.originalCalls==before+1,'Configure must not stack observers')
      state.handlerThrows=true;assert(not pcall(fw.HandleMessage,fw,{uuid=7001}))
      assert(engine.Status().data.services.fireworks.claimed==0);state.handlerThrows=false
      LuaEntry.Player.uid='another-owner';fw:HandleMessage({uuid=7001})
      assert(engine.Status().data.services.fireworks.claimed==0,'Retired owner cannot credit a response')
      engine.CleanupAfterLeaseLoss()
      assert(fw.HandleMessage==originalFw and rp.HandleMessage==originalRp)
    """)
    vm = runtime()
    vm.execute("""
      local successor=function()return 'new wrapper'end;fw.HandleMessage=successor
      engine.CleanupAfterLeaseLoss();assert(fw.HandleMessage==successor,'Cleanup preserves a successor observer')
    """)
    print("CHAT_OUTCOME_CHECKS_PASS cache-only/no-replay late-success exact-UUID/server refusal states return arity original errors owner loss successor-safe cleanup")


if __name__ == "__main__":
    main()
