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
      local mgr={Room={GetRoomDatas=function()state.roomScans=(state.roomScans or 0)+1;return {room}end},CanNotPickRedPacket=function()return state.blocked or false end,
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
      assert(engine.Status().data.services.treasure.capabilities.claims==true)
      local ordinary=msg(0,'999',1);state.callbacks[1](ordinary)
      assert(engine.Status().data.listener.observedMessages==1 and #state.sent==0)
      state.messages={msg(611,'red',2),msg(693,'3001',3),msg(600,'4001',4)}
      state.now=state.now+1000;engine.Pump()
      assert(#state.sent==1 and state.sent[1][1]==11,'Fireworks claim route must remain real')
      local treasure=engine.Status().data.services.treasure
      assert(treasure.failed==0 and treasure.submitted==0 and treasure.notJoined==1,
        'A finished dig our squad did not join is never claimed (owner 2026-10-22)')
      assert(engine.Status().data.services.fireworks.claimed==0,'Submission is not confirmation')
      state.callbacks[3]({uuid='not-matching'});assert(engine.Status().data.services.fireworks.claimed==0)
      state.callbacks[3]({uuid=3001});assert(engine.Status().data.services.fireworks.claimed==1)
      state.now=state.now+2000;engine.Pump();assert(#state.sent==2 and state.sent[2][1]==10)
      assert(state.sent[2][2]=='red'and state.sent[2][3]==1 and state.sent[2][4]==1 and state.sent[2][5]==123)
      state.callbacks[2]({redPacket={uuid='red'}});assert(state.replyCount==0)
      state.now=state.now+2000;engine.Pump();assert(state.replyCount==1)
      engine.Run({kind='redPacket',authorizedUid='1001',confirmedChatConfig='config'});engine.Pump();assert(#state.sent==2,'No duplicate UUID claims')
      -- Disable while a delayed event is queued: no later automatic send/reply.
      state.callbacks[1](msg(611,'disabled-queued',5));state.config.redPacket.enabled=false
      engine.Configure(request);state.now=state.now+10000;engine.Pump();assert(#state.sent==2)
      -- Explicit Run now remains useful with the switch off.
      state.messages={msg(611,'manual',6)};request.kind='redPacket';engine.Run(request)
      state.now=state.now+3000;engine.Pump();assert(#state.sent==3)
      -- A send that throws must never be retried, even by Run now.
      state.messages={msg(611,'uncertain',7)};engine.Run(request);state.throw=true
      state.now=state.now+3000;engine.Pump();assert(#state.sent==4);state.throw=false
      engine.Run(request);state.now=state.now+3000;engine.Pump();assert(#state.sent==4)
      -- An exact account or launch-lease mismatch stops all delayed dispatch.
      state.config.redPacket.enabled=true;state.messages={msg(611,'foreign',8)};engine.Configure(request)
      LuaEntry.Player.uid='2002';state.now=state.now+3000;engine.Pump();assert(#state.sent==4)
      assert(engine.Configure(request).state=='failed')
      LuaEntry.Player.uid='1001';state.token=nil;engine.Pump();assert(#state.sent==4)
      engine.CleanupAfterLeaseLoss();assert(state.callbacks[1]==nil and state.callbacks[2]==nil)
      -- A different account/session must not inherit any queued targets.
      LuaEntry.Player.uid='2002';request.authorizedUid='2002';state.token='other';state.messages={}
      assert(engine.Configure(request).state=='confirmed');state.now=state.now+3000;engine.Pump();assert(#state.sent==4)
      -- R29 performance: idle owners never rescan rooms; enabled owners rescan at most every 15 s.
      state.config={redPacket={enabled=false},fireworks={enabled=false},treasure={enabled=false}}
      assert(engine.Configure(request).state=='confirmed');state.roomScans=0
      for _=1,60 do state.now=state.now+1000;engine.Pump()end
      for _=1,20 do engine.Read(request)end
      assert(state.roomScans==0,'Idle Chat must not rescan rooms every tick')
      state.config={redPacket={enabled=true,claimDelaySeconds={0,0}},fireworks={enabled=false},treasure={enabled=false}}
      assert(engine.Configure(request).state=='confirmed');state.roomScans=0
      for _=1,30 do state.now=state.now+1000;engine.Pump()end
      assert(state.roomScans>=1 and state.roomScans<=2,'Enabled Chat rescans at most every 15 s: '..state.roomScans)
      -- A live chat event is still handled immediately between rescans.
      local before=#state.sent;state.callbacks[1](msg(611,'live-between',9));state.now=state.now+1000;engine.Pump()
      assert(#state.sent==before+1,'Live event claim must not wait for the backup rescan')
    ''')
    vm.execute('''
      -- Read-only emoji list: tags use the game's in-text form, nothing is sent.
      local sends=#state.sent
      DataCenter.ChatEmojiTemplateManager={GetShowEmojiList=function()return {{id=7,name='flower'},{id=8}}end}
      local r=engine.EmojiList();assert(r.state=='confirmed'and r.data.readOnly and #r.data.emojis==2)
      assert(r.data.emojis[1].tag=='<lwEmoji:7:>'and r.data.emojis[1].name=='flower'and #state.sent==sends)
      DataCenter.ChatEmojiTemplateManager=nil;assert(engine.EmojiList().state=='failed')
      -- Picture reads: invalid names refused; a still-loading sheet reports not ready (nothing sent).
      assert(engine.EmojiImage({name='../x'}).state=='failed')
      DataCenter.CacheData={LoadEmojiAsset=function(self,cb)end}
      assert(engine.EmojiImage({name='e006'}).errorCode=='GAME_NOT_READY'and #state.sent==sends)
    ''')
    # Owner 2026-10-22 Treasure: an alliance share of an EMPTY tile next to the dig, the real dig found
    # within 3 tiles, our squad dispatched, then the claim only after it joined, at the scheduled delay.
    vm.execute('''
      engine.CleanupAfterLeaseLoss()
      LuaEntry.Player.uid='1001';LuaEntry.Player.allianceId='77';request.authorizedUid='1001';state.token='owner';state.sent={};state.messages={}
      PostType.Text_PointShare_Alliance=50;MarchTargetType={DETECT_TREASURE=40}
      local share=500*1000+300+1;local dig=share+2
      local digInfo={pointType=21,mainIndex=dig,serverId=123,uuid=9001,allianceId='77',expireTime=0,
        GetType=function()return {FullName='TreasurePointInfo'}end,IsComplete=function()return false end,
        IsHaveGetReward=function()return false end,IsHaveWorking=function()return false end,IsReceiveAllReward=function()return false end}
      CS.SceneManager.World.GetPointInfo=function(self,index)if index==dig then return digInfo end end
      state.marches={}
      local forms={};for i=1,4 do forms[i]={uuid=500+i,index=i,ownerUid='1001'}end
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,i)return forms[i]end}
      DataCenter.WorldMarchDataManager={GetOwnerFormationMarch=function(self,uid,f)return state.marches[f]end}
      MarchUtil={GetAutoBackHomeState=function()return true end,CheckFormation=function()return true,true end,
        SendCreateMarchMessage=function(f,kind,point,uuid,a,back,b,server)
          assert(kind==40 and point==dig and uuid==9001 and server==123);state.dispatchedSquad=f
          state.marches[f]={targetUuid=9001,GetMarchTargetType=function()return 40 end}end}
      local claimReply={HandleMessage=function()return 'original-claim'end}
      package.preload['Net.Msgs.RadarCenter.DetectEventClaimTreasureMessage']=function()return claimReply end
      state.config={redPacket={enabled=false},fireworks={enabled=false},treasure={enabled=true,claimDelaySeconds={0,0},
        dispatchEnabled=true,dispatchDelaySeconds={0,0},dispatchSquadPriority={2,1},dispatchRetrySeconds=5,
        claimSchedule={{start='00:00',['end']='12:00',min=1,max=1},{start='12:00',['end']='00:00',min=1,max=1}}}}
      -- The game's ping (400 ms here) is subtracted from the scheduled 1 s delay.
      CS.GameEntry={Network={m_proxy={GetPing=function()return 400 end}}}
      assert(engine.Configure(request).state=='confirmed')
      -- World chat shares are ignored; the alliance share points at the empty tile beside the dig.
      state.callbacks[1]({post=50,seqId=90,roomId='w',group=1,attachmentId={pos=share,sid=123}})
      state.callbacks[1]({post=50,seqId=91,roomId='a',group=2,attachmentId={pos=share,sid=123}})
      state.now=state.now+100;engine.Pump();state.now=state.now+100;engine.Pump()
      assert(state.dispatchedSquad==502,'First idle squad in priority order is sent to the real dig 2 tiles away')
      state.now=state.now+100;engine.Pump()
      local t=engine.Status().data.services.treasure
      assert(t.sharesSeen==1 and t.digsFound==1 and t.dispatched==1)
      -- A different finished dig (not joined) is never claimed; ours is claimed after the 1 s schedule delay.
      local before=#state.sent
      state.callbacks[1](msg(600,'8888',92));state.callbacks[1](msg(600,'9001',93))
      state.now=state.now+500;engine.Pump();assert(#state.sent==before,'Waits for the scheduled delay minus ping')
      state.now=state.now+150;engine.Pump()
      assert(#state.sent==before+1 and state.sent[#state.sent][1]==12 and state.sent[#state.sent][2]==9001 and state.sent[#state.sent][3]==123)
      assert(claimReply.HandleMessage({}, {})=='original-claim')
      t=engine.Status().data.services.treasure;assert(t.claimed==1 and t.notJoined>=1)
    ''')
    print('FEATURE018_CHAT_ENGINE_OK native-shaped fixtures: listener, Red Packet/Fireworks claims, Treasure share->dig->claim (alliance only, 3-tile search, joined-only, schedule), delays, stop, reply, UUID fencing, foreign UID/session, idle no-rescan')


if __name__ == '__main__':
    main()
