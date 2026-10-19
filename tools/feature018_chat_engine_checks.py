"""Inert tests of actual Chat Lua: no game, installed files, or network."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'artifacts/map-013-lua-site'))
from lupa.lua53 import LuaRuntime


def main():
    vm = LuaRuntime(unpack_returned_tuples=True)
    vm.execute('''
      state={now=1000000,uid='1001',server=123,token='owner',sent={},messages={},callbacks={},replyCount=0}
      os.time=function()return math.floor(state.now/1000)end
      math.random=function(a,b)if a then return a end return 0.5 end
      LuaEntry={Player={uid='1001',GetSourceServerId=function()return state.server end,
        GetSelfServerId=function()return state.server end,GetCurServerId=function()return state.server end},
        DataConfig={TryGetNum=function()return 120 end}}
      UITimeManager={GetInstance=function()return {GetServerTime=function()return state.now end}end}
      PostType={Text_Normal=0,RedPackge_New=611,FIREWORK_GIFT_REWARD=693,
        Detect_Treasure_Fin_Info=600,Detect_Treasure_Reward_Fin_Info=599,DetectEventGetDoubleTreasure=610}
      ChatGroupType={GROUP_COUNTRY=1,GROUP_ALLIANCE=2,GROUP_SEASON_ROOM=3,GROUP_ALLIANCE_FRIEND_ROOM=4}
      MsgDefines={OpenRedPacket=10,GetFireworksGift=11,DetectEventClaimTreasure=12}
      TableName={Firework='Firework'}
      GetTableData=function(t,id,key,default)assert(t=='Firework' and key=='type');return 2 end
      DataCenter={RedPacketTemplateManager={GetTemplate=function()return {Condition=function()return not state.refuse end}end},
        RedPacketManager={ChannelType={World=1,Alliance=2,Season=3,AliFriend=4}},
        LWFireworkGiftManager={IsThisGiftUuidGot=function(self,uuid)return state.got==uuid end}}
      local room={GetUnblockedChatDatas=function()return state.messages end}
      local mgr={Room={GetRoomDatas=function()return {room}end},CanNotPickRedPacket=function()return state.blocked or false end,
        CheckRoomSend=function()return true end,SendChatMsg=function(self,p)assert(p.msg=='thanks');state.replyCount=state.replyCount+1 end}
      ChatManager2={GetInstance=function()return mgr end}
      EventId={RedPacketOpen=2,FireworkGiftGotUpdate=3}
      ChatEventEnum={CHAT_RECIEVE_ROOM_MSG=1,CHAT_ROOM_ONEMSG_UPDATA=4}
      local em={AddListener=function(self,event,cb)state.callbacks[event]=cb end,
        RemoveListener=function(self,event,cb)if state.callbacks[event]==cb then state.callbacks[event]=nil end end}
      EventManager={GetInstance=function()return em end}
      SFSNetwork={SendMessage=function(route,...)state.sent[#state.sent+1]={route,...};if state.throw then error('uncertain send')end end}
      CS={SceneManager={World={GetPointInfoByUuid=function(self,uuid)return {uuid=uuid,IsHaveGetReward=function()return state.treasureGot end}end}}}
      package.preload.rapidjson=function()return {decode=function(s)if s=='config'then return state.config end;error('invalid json')end}end
      state.config={redPacket={enabled=true,claimDelaySeconds={2,2},replyEnabled=true,replyDelaySeconds={2,2},replies={'thanks'}},
        fireworks={enabled=true,claimDelaySeconds={0,0}},treasure={enabled=true,claimDelaySeconds={0,0}}}
      request={authorizedUid='1001',confirmedChatConfig='config'}
      overview={Feature018OwnerToken=function()return state.token end}
      feature={RegisterHandler=function()end,Array=function(x)return x end}
      function msg(post,uuid,seq,extra)
        extra=extra or {};extra.uuid=uuid;extra.expiredTime=state.now+120000;extra.packetId=1;extra.serverId=123;extra.targetServer=123
        extra.configId=1;extra.ownerUid='2001'
        return {post=post,seqId=seq,roomId='room',group=1,serverTime=state.now,extra={customJsonParam=extra}}
      end
    ''')
    vm.globals().engine = vm.execute((ROOT / 'tools/feature018_chat_engine.lua').read_text(encoding='utf-8'))
    vm.execute('''
      engine.Register(feature,overview);assert(#state.sent==0,'Require must be inert')
      assert(engine.Configure(request).state=='confirmed')
      local ordinary=msg(0,'999',1);state.callbacks[1](ordinary)
      assert(engine.Status().data.listener.observedMessages==1 and #state.sent==0)
      state.messages={msg(611,'red',2),msg(693,'3001',3),msg(600,'4001',4)}
      state.now=state.now+1000;engine.Pump()
      assert(#state.sent==2 and state.sent[1][1]~=10 and state.sent[2][1]~=10,'Independent service queues')
      assert(engine.Status().data.services.fireworks.claimed==0,'Submission is not confirmation')
      state.callbacks[3]({uuid='not-matching'});assert(engine.Status().data.services.fireworks.claimed==0)
      state.callbacks[3]({uuid=3001});assert(engine.Status().data.services.fireworks.claimed==1)
      state.now=state.now+2000;engine.Pump();assert(#state.sent==3 and state.sent[3][1]==10)
      assert(state.sent[3][2]=='red'and state.sent[3][3]==1 and state.sent[3][4]==1 and state.sent[3][5]==123)
      state.callbacks[2]({redPacket={uuid='red'}});assert(state.replyCount==0)
      state.treasureGot=true;engine.Pump();assert(engine.Status().data.services.treasure.claimed==1)
      state.now=state.now+2000;engine.Pump();assert(state.replyCount==1)
      engine.Run({kind='redPacket',authorizedUid='1001',confirmedChatConfig='config'});engine.Pump();assert(#state.sent==3,'No duplicate UUID claims')
      -- Disable while a delayed event is queued: no later automatic send/reply.
      state.callbacks[1](msg(611,'disabled-queued',5));state.config.redPacket.enabled=false
      engine.Configure(request);state.now=state.now+10000;engine.Pump();assert(#state.sent==3)
      -- Explicit Run now remains useful with the switch off.
      state.messages={msg(611,'manual',6)};request.kind='redPacket';engine.Run(request)
      state.now=state.now+3000;engine.Pump();assert(#state.sent==4)
      -- A send that throws must never be retried, even by Run now.
      state.messages={msg(611,'uncertain',7)};engine.Run(request);state.throw=true
      state.now=state.now+3000;engine.Pump();assert(#state.sent==5);state.throw=false
      engine.Run(request);state.now=state.now+3000;engine.Pump();assert(#state.sent==5)
      -- An exact account or launch-lease mismatch stops all delayed dispatch.
      state.config.redPacket.enabled=true;state.messages={msg(611,'foreign',8)};engine.Configure(request)
      LuaEntry.Player.uid='2002';state.now=state.now+3000;engine.Pump();assert(#state.sent==5)
      assert(engine.Configure(request).state=='failed')
      LuaEntry.Player.uid='1001';state.token=nil;engine.Pump();assert(#state.sent==5)
      engine.CleanupAfterLeaseLoss();assert(state.callbacks[1]==nil and state.callbacks[2]==nil)
      -- A different account/session must not inherit any queued targets.
      LuaEntry.Player.uid='2002';request.authorizedUid='2002';state.token='other';state.messages={}
      assert(engine.Configure(request).state=='confirmed');state.now=state.now+3000;engine.Pump();assert(#state.sent==5)
    ''')
    print('FEATURE018_CHAT_ENGINE_OK native-shaped fixtures: listener, three claim routes, delays, stop, reply, UUID fencing, foreign UID/session')


if __name__ == '__main__':
    main()
