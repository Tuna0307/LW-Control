"""Reproduce the documented inert comparison from the repository root.

Requires the exact retained private core and existing Lupa fixture runtime.
Never downloads, launches, alters installed files, contacts a server or sends
real game commands. Only two hash-locked, reviewed pure core functions execute.
"""
import ast,json,hashlib,sys
from pathlib import Path
ROOT=Path.cwd();OUT=ROOT/'artifacts/lwtool-chat-comparison-20261025';sys.stdout.reconfigure(encoding='utf-8')
OUT.mkdir(parents=True,exist_ok=True)
core=ROOT/'artifacts/lwtool-remaining-tabs-20261025/current-lwtool-observation/recovered-core.py'
assert hashlib.sha256(core.read_bytes()).hexdigest()=='f9f910e2f42ea5215fce1391ac7c6685ff4f16b7926200aa203265875c4b0ec3','Recovered input changed; review it before replay'
tree=ast.parse(core.read_text('utf-8'))
post_node=next(n.value for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='_CN_POST_LUA' for t in n.targets))
scope={'_CN_POST_LUA':ast.literal_eval(post_node),'_vip_tat_path':lambda _:False}
selected=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('_chat_nhanh_bat','_cn_post_theo_cfg')]
assert len(selected)==2
exec(compile(ast.Module(body=selected,type_ignores=[]),str(core),'exec'),scope)
gates=[]
for egg,cross,vip,trade in [(True,False,True,False),(True,True,False,False),(True,True,True,False),(False,True,True,False),(False,False,True,True)]:
 cfg=dict(goldEgg=egg,goldEggCross=cross,vipCheck=vip,crossServerScan=trade)
 gates.append(dict(config=cfg,fastRequested=scope['_chat_nhanh_bat'](cfg),selectedPostTypes=sorted(scope['_cn_post_theo_cfg'](cfg))))
assert gates[2]['fastRequested'] and gates[2]['selectedPostTypes']==[611,693,695,710]
assert not gates[0]['fastRequested'] and not gates[1]['fastRequested'] and not gates[3]['fastRequested']
assert gates[4]['selectedPostTypes']==[662]
# Reuse only the inert fixture setup from the existing regression; no test main,
# installed-file changes, network or real game process is invoked.
fixture=ast.parse((ROOT/'tools/feature018_chat_engine_checks.py').read_text('utf-8'))
main=next(n for n in fixture.body if isinstance(n,ast.FunctionDef) and n.name=='main')
setup=next(n for n in main.body if isinstance(n,ast.Expr) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='execute').value.args[0].value
sys.path.insert(0,str(ROOT/'artifacts/map-013-lua-site'));from lupa.lua53 import LuaRuntime
rows=[]
for post in [611,693,694,695,710]:
 vm=LuaRuntime(unpack_returned_tuples=True);vm.execute(setup)
 vm.execute('PostType.FIREWORK_GIFT_REWARD_DOUBLE=694;PostType.S0_ALLIANCE_BOSS_GIFT_REWARD=695;PostType.EasterEggChat=710')
 vm.globals().engine=vm.execute((ROOT/'tools/feature018_chat_engine.lua').read_text('utf-8'))
 vm.execute("engine.Register(feature,overview);engine.Configure(request);state.messages={msg("+str(post)+",'5001',1)};state.now=state.now+16001;engine.Pump()")
 row={'post':post,'mockPackets':len(vm.globals().state.sent),'redPacketSubmitted':vm.eval('engine.Status().data.services.redPacket.submitted'),'fireworksSubmitted':vm.eval('engine.Status().data.services.fireworks.submitted')}
 # Red Packet has a 2 s claim delay in the fixture.
 if post==611:
  vm.execute('state.now=state.now+2001;engine.Pump()');row.update(mockPackets=len(vm.globals().state.sent),redPacketSubmitted=vm.eval('engine.Status().data.services.redPacket.submitted'))
 rows.append(row)
assert rows[0]['redPacketSubmitted']==1 and rows[1]['fireworksSubmitted']==1
assert all(r['mockPackets']==0 for r in rows[2:])
vm=LuaRuntime(unpack_returned_tuples=True);vm.execute(setup)
vm.globals().engine=vm.execute((ROOT/'tools/feature018_chat_engine.lua').read_text('utf-8'))
vm.execute("engine.Register(feature,overview);engine.Configure(request);state.messages={msg(693,'6001',1)};state.now=state.now+16001;engine.Pump();assert(engine.Status().data.services.fireworks.claimed==0);state.got=6001;engine.Pump()")
counter={'mockPackets':len(vm.globals().state.sent),'newRewardEventDelivered':False,'cacheGotAfterSend':True,'reportedClaimed':vm.eval('engine.Status().data.services.fireworks.claimed')}
assert counter['reportedClaimed']==1
v={'scope':'controlled replay of exact recovered pure host functions and current production Lua with mocked game; not live gameplay or speed measurement','lwtCoreSha256':hashlib.sha256(core.read_bytes()).hexdigest(),'lwControlChatEngineSha256':hashlib.sha256((ROOT/'tools/feature018_chat_engine.lua').read_bytes()).hexdigest(),'lwtFastChatGates':gates,'ourMessageCoverage':rows,'alreadyClaimedCacheCounterexample':counter,'liveNetworkCalls':0,'liveGameActions':0}
(OUT/'controlled-comparison.json').write_text(json.dumps(v,indent=2)+'\n');print('CONTROLLED_CHAT_COMPARISON_PASS',json.dumps(rows))
