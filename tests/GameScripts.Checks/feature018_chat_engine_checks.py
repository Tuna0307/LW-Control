"""Inert tests of actual Chat Lua: no game, installed files, or network."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
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
        extra.configId=1;extra.ownerUid=extra.ownerUid or '2001'
        return {post=post,seqId=seq,roomId='room',group=1,serverTime=state.now,extra={customJsonParam=extra}}
      end
    ''')
    vm.globals().engine = vm.execute((ROOT / 'src/LWBridge.GameScripts/feature018_chat_engine.lua').read_text(encoding='utf-8'))
    vm.execute('''
      engine.Register(feature,overview);assert(#state.sent==0,'Require must be inert')
      assert(engine.Configure(request).state=='confirmed')
      assert(engine.Status().data.services.treasure.capabilities.claims==true)
      local ordinary=msg(0,'999',1);state.callbacks[1](ordinary)
      assert(engine.Status().data.listener.observedMessages==1 and #state.sent==0)
      state.messages={msg(611,'red',2),msg(693,'3001',3),msg(600,'4001',4,{ownerUid='1001'})}
      state.now=state.now+1000;engine.Pump()
      assert(#state.sent==1 and state.sent[1][1]==11,'Fireworks claim route must remain real')
      local treasure=engine.Status().data.services.treasure
      assert(treasure.failed==0 and treasure.submitted==0,
        'LwTool default: a treasure from my own radar is not auto-opened')
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
    # Treasure (LwTool "Dig Treasure" first, owner 2026-10-26): an alliance share of an EMPTY tile next to
    # the dig finds it (Treasure Hunt), one squad is dispatched, and claims go out when a dig finishes,
    # whether or not our squad joined; world-chat shares are ignored.
    vm.execute('''
      engine.CleanupAfterLeaseLoss()
      LuaEntry.Player.uid='1001';LuaEntry.Player.allianceId='77';request.authorizedUid='1001';state.token='owner';state.sent={};state.messages={}
      PostType.Text_PointShare_Alliance=50;MarchTargetType={DETECT_TREASURE=40,COLLECT=1}
      local share=500*1000+300+1;local dig=share+2
      digInfo={pointType=21,mainIndex=dig,serverId=123,uuid=9001,allianceId='77',ownerUid='3001',expireTime=0,
        GetType=function()return {FullName='TreasurePointInfo'}end,IsComplete=function()return false end,
        IsHaveGetReward=function()return false end,IsHaveWorking=function()return false end,IsReceiveAllReward=function()return false end}
      CS.SceneManager.World.GetPointInfo=function(self,index)if index==dig then return digInfo end end
      state.marches={}
      local forms={};for i=1,4 do forms[i]={uuid=500+i,index=i,ownerUid='1001'}end
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,i)return forms[i]end}
      DataCenter.WorldMarchDataManager={GetOwnerFormationMarch=function(self,uid,f)return state.marches[f]end}
      MarchUtil={GetAutoBackHomeState=function()return 1 end,CheckFormation=function()return true end,
        SendCreateMarchMessage=function(f,kind,point,uuid,a,back,b,server)
          assert(kind==40 and point==dig and uuid==9001 and server==123);state.dispatchedSquad=f
          state.marches[f]={targetUuid=9001,ownerUid='1001',GetMarchTargetType=function()return 40 end}end}
      local claimReply={HandleMessage=function()return 'original-claim'end}
      package.preload['Net.Msgs.RadarCenter.DetectEventClaimTreasureMessage']=function()return claimReply end
      state.config={redPacket={enabled=false},fireworks={enabled=false},treasure={enabled=true,
        dispatchEnabled=true,dispatchDelayMs=0,dispatchSquadPriority={2,1},dispatchRetrySeconds=5,clickBeforeMs=0}}
      CS.GameEntry={Network={m_proxy={GetPing=function()return 400 end}}}
      assert(engine.Configure(request).state=='confirmed')
      state.callbacks[1]({post=50,seqId=90,roomId='w',group=1,attachmentId={pos=share,sid=123}})
      state.callbacks[1]({post=50,seqId=91,roomId='a',group=2,attachmentId={pos=share,sid=123}})
      state.now=state.now+100;engine.Pump();state.now=state.now+100;engine.Pump()
      assert(state.dispatchedSquad==502,'First idle squad in the saved order is sent to the real dig 2 tiles away')
      state.now=state.now+100;engine.Pump()
      local t=engine.Status().data.services.treasure
      assert(t.sharesSeen==1 and t.digsFound==1 and t.dispatched==1,'World chat is ignored; one squad per dig')
      -- A finish announcement for a dig we never joined is claimed right away.
      local before=#state.sent
      state.callbacks[1](msg(600,'8888',92));state.now=state.now+10;engine.Pump()
      assert(#state.sent==before+1 and state.sent[#state.sent][1]==12 and state.sent[#state.sent][2]==8888,
        'LwTool claims a finished alliance dig whether or not our squad joined')
      assert(claimReply.HandleMessage({}, {})=='original-claim')
      -- Our dig gets a completion time: the claim waits for it (Click before 0) and goes out on time.
      digInfo.completionTime=state.now+1000
      state.now=state.now+900;engine.Pump();assert(#state.sent==before+1,'Waits for the completion time')
      state.now=state.now+150;engine.Pump()
      assert(#state.sent==before+2 and state.sent[#state.sent][2]==9001 and state.sent[#state.sent][3]==123)
      claimReply.HandleMessage({}, {})
      t=engine.Status().data.services.treasure;assert(t.claimed==2 and t.claimsSent==2)
    ''')
    # Audit 2026-10-23 AUDIT-02: two joined digs finish together. The claim reply names no dig, so only
    # one claim is in flight; the second goes out after the first reply and both are counted.
    vm.execute('''
      local T=engine._Treasure;local claimReply=require('Net.Msgs.RadarCenter.DetectEventClaimTreasureMessage')
      local function claims()local n=0;for _,x in ipairs(state.sent)do if x[1]==12 then n=n+1 end end;return n end
      state.marches[503]={targetUuid=9102,GetMarchTargetType=function()return 40 end}
      state.marches[504]={targetUuid=9103,GetMarchTargetType=function()return 40 end}
      local base=claims();local claimedBefore=T.stats.claimed
      state.callbacks[1](msg(600,'9102',401));state.callbacks[1](msg(600,'9103',402))
      state.now=state.now+5000;engine.Pump();engine.Pump()
      assert(claims()==base+1,'Only one Treasure claim waits for a reply at a time')
      claimReply.HandleMessage({}, {});state.now=state.now+100;engine.Pump()
      assert(claims()==base+2,'The second claim follows the first reply')
      claimReply.HandleMessage({}, {});state.now=state.now+100;engine.Pump()
      assert(T.stats.claimed==claimedBefore+2 and T.claiming==nil,'Both claims are counted, none left pending')
    ''')
    # AUDIT-03: switching Treasure off releases an in-flight map request and the shared lock.
    vm.execute('''
      local T=engine._Treasure;local removed=0
      T.aoi={manager={},current={Remove=function()removed=removed+1 end},added={7,8},int32={Parse=function(x)return x end},started=state.now}
      rawset(_G,'__lwbridgeAoiOwner',T.aoi)
      state.config.treasure.enabled=false;assert(engine.Configure(request).state=='confirmed')
      state.config.redPacket={enabled=true,claimDelaySeconds={0,0}}
      assert(engine.Configure(request).state=='confirmed')
      state.now=state.now+1000;engine.Pump()
      assert(T.aoi==nil and rawget(_G,'__lwbridgeAoiOwner')==nil and removed==2,'Off Treasure releases its map request')
      state.config.treasure.enabled=true;state.config.redPacket={enabled=false};assert(engine.Configure(request).state=='confirmed')
    ''')
    # AUDIT-01: a dig found by one account is never dispatched by the next account.
    vm.execute('''
      local T=engine._Treasure
      state.config.treasure.dispatchDelayMs=10000;assert(engine.Configure(request).state=='confirmed')
      for k in pairs(state.marches)do state.marches[k]=nil end;state.dispatchedSquad=nil
      T.digs={};T.lookups={};T.shareSeen={}
      state.callbacks[1]({post=50,seqId=500,roomId='a',group=2,attachmentId={pos=500*1000+300+1,sid=123}})
      state.now=state.now+100;engine.Pump()
      assert(T.digs['9001']and T.digs['9001'].state=='queued','Dig found and waiting for its dispatch delay')
      LuaEntry.Player.uid='2002';LuaEntry.Player.allianceId='99';request.authorizedUid='2002';state.token='owner-2'
      local forms={};for i=1,4 do forms[i]={uuid=700+i,index=i,ownerUid='2002'}end
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,i)return forms[i]end}
      assert(engine.Configure(request).state=='confirmed')
      for _=1,20 do state.now=state.now+1000;engine.Pump()end
      assert(state.dispatchedSquad==nil,'The next account never marches to the previous account dig')
    ''')
    # AUDIT-04: a Red Packet queued at 01:59 is not claimed after the 02:00 stop.
    vm.execute('''
      local realDate=os.date;local hm={1,59}
      os.date=function(f,t)if f=='*t'then return {hour=hm[1],min=hm[2]}end;return realDate(f,t)end
      state.config={redPacket={enabled=true,claimDelaySeconds={2,2},activeHours={enabled=true,start='08:00',['end']='02:00'}},
        fireworks={enabled=false},treasure={enabled=false}}
      assert(engine.Configure(request).state=='confirmed')
      local before=#state.sent
      state.callbacks[1](msg(611,'late-packet',600))
      hm={2,0};state.now=state.now+3000;engine.Pump()
      assert(#state.sent==before,'Queued before the stop time, not sent after it')
      os.date=realDate
    ''')
    # Owner 2026-10-22: active hours 08:00-02:00 cross midnight; outside them a new claim does not start.
    vm.execute('''
      local realDate=os.date;local hour=3
      os.date=function(f,t)if f=='*t'then return {hour=hour,min=0}end;return realDate(f,t)end
      local w={activeHours={enabled=true,start='08:00',['end']='02:00'}}
      hour=8;assert(engine._InHours(w));hour=23;assert(engine._InHours(w));hour=1;assert(engine._InHours(w))
      hour=2;assert(not engine._InHours(w));hour=7;assert(not engine._InHours(w))
      assert(engine._InHours({activeHours={enabled=false,start='08:00',['end']='02:00'}}))
      hour=3;state.config.fireworks={enabled=true,claimDelaySeconds={0,0},activeHours=w.activeHours}
      assert(engine.Configure(request).state=='confirmed')
      local before=#state.sent;state.callbacks[1](msg(693,'7777',300));state.now=state.now+1000;engine.Pump()
      assert(#state.sent==before,'No claim outside active hours')
      local f=engine.Status().data.services.fireworks;assert(f.offHours==true and f.activeFrom=='08:00')
      os.date=realDate
    ''')
    # Owner 2026-10-26 (LwTool parity): both point-share post types in alliance or world chat, x/y or
    # pos payloads, every alliance dig within 15 tiles queued nearest first, a 4x4-block area request,
    # and a busy shared map lock waits up to 15 s before using what is already loaded.
    vm.execute('''
      local T=engine._Treasure;engine.CleanupAfterLeaseLoss()
      LuaEntry.Player.uid='1001';LuaEntry.Player.allianceId='77';request.authorizedUid='1001';state.token='owner'
      PostType.Text_PointShare=13;PostType.Text_PointShare_Alliance=50
      local forms={};for i=1,4 do forms[i]={uuid=800+i,index=i,ownerUid='1001'}end
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,i)return forms[i]end}
      state.marches={};state.sentTo={}
      MarchUtil.SendCreateMarchMessage=function(f,kind,point,uuid)
        state.sentTo[#state.sentTo+1]=uuid;state.marches[f]={targetUuid=uuid,GetMarchTargetType=function()return 40 end}end
      local function dig(uuid,index,alliance)
        return {pointType=21,mainIndex=index,serverId=123,uuid=uuid,allianceId=alliance or '77',expireTime=0,
          GetType=function()return {FullName='TreasurePointInfo'}end,IsComplete=function()return false end,
          IsHaveGetReward=function()return false end,IsHaveWorking=function()return false end,IsReceiveAllReward=function()return false end}
      end
      local x,y=400,600;local spot=y*1000+x+1
      local points={[spot+14]=dig(7001,spot+14),[spot+3000]=dig(7002,spot+3000),[spot+16]=dig(7003,spot+16),
        [spot-5]=dig(7004,spot-5,'88')}
      CS.SceneManager.World.GetPointInfo=function(self,index)return points[index]end
      CS.SceneManager.World.PointManager=nil
      state.config={redPacket={enabled=false},fireworks={enabled=false},treasure={enabled=true,claimDelaySeconds={0,0},
        dispatchEnabled=true,dispatchDelayMs=0,dispatchSquadPriority={1,2,3,4},dispatchRetrySeconds=5,unsharedEnabled=true}}
      assert(engine.Configure(request).state=='confirmed')
      T.digs={};T.lookups={};T.shareSeen={};T.stats.sharesSeen=0;T.stats.digsFound=0
      -- Ignored: an ordinary text post, a world-chat share, a season-room share, another server's share.
      state.callbacks[1]({post=0,seqId=700,roomId='a',group=2,attachmentId={x=x,y=y,sid=123}})
      state.callbacks[1]({post=13,seqId=7000,roomId='w',group=1,attachmentId={x=x,y=y,sid=123}})
      state.callbacks[1]({post=13,seqId=701,roomId='s',group=3,attachmentId={x=x,y=y,sid=123}})
      state.callbacks[1]({post=13,seqId=702,roomId='a',group=2,attachmentId={x=x,y=y,sid=999}})
      state.now=state.now+100;engine.Pump()
      assert(T.stats.sharesSeen==0 and #state.sentTo==0,'Only point shares of this server in alliance chat count')
      -- An alliance Text_PointShare carrying x/y (as ShareDecode reads it), Unshared Treasure on.
      state.callbacks[1]({post=13,seqId=703,roomId='a',group=2,attachmentId={x=x,y=y,sid=123}})
      state.now=state.now+100;engine.Pump()
      assert(T.stats.sharesSeen==1 and T.digs['7001']and T.digs['7002'],'Digs 14 tiles east and 3 tiles north are found')
      assert(not T.digs['7003'],'A dig 16 tiles away is outside the search')
      assert(not T.digs['7004'],'Another alliance dig is never queued')
      local near=T.Nearby(spot,15);assert(#near==2 and near[1].uuid=='7002'and near[2].uuid=='7001','Nearest first')
      for _=1,3 do state.now=state.now+100;engine.Pump()end
      assert(#state.sentTo==2,'An idle squad is sent to each found dig: '..#state.sentTo)
      -- A nested param payload with pos is also read; a share with no dig nearby queues nothing.
      state.callbacks[1]({post=50,seqId=704,roomId='a',group=2,attachmentId={param={pos=100*1000+100+1,sid=123}}})
      state.now=state.now+100;engine.Pump()
      assert(T.stats.sharesSeen==2 and #T.lookups==0 and #state.sentTo==2)
      -- Area request covers 15 tiles each way: up to 4x4 blocks, clipped at the map edge.
      local a,b,c,d=T.AoiBlocks(500*1000+505+1,15);assert(a==49 and b==52 and c==48 and d==51)
      a,b,c,d=T.AoiBlocks(3*1000+3+1,15);assert(a==0 and b==1 and c==0 and d==1)
      local view={}
      local current={Contains=function(self,n)return view[n]==true end,Add=function(self,n)view[n]=true;return true end,
        Remove=function(self,n)view[n]=nil end}
      CS.SceneManager.World.PointManager={_curViewIndex=current,isRecvViewPoints=true}
      CS.System={Int32={Parse=function(s)return tonumber(s)end},Array={CreateInstance=function(_,n)
        local arr={n=n,values={}};arr.SetValue=function(self,v,k)self.values[k+1]=v end;return arr end}}
      typeof=function(t)return t end
      CS.WorldGetBlockMessage={Request=function()return {}end,Instance={Send=function(self,r)state.block=r end}}
      -- The shared map lock is held by another service: wait, then after 15 s use what is loaded.
      rawset(_G,'__lwbridgeAoiOwner',{})
      state.callbacks[1]({post=13,seqId=705,roomId='a',group=2,attachmentId={x=x,y=y,sid=123}})
      state.now=state.now+1000;engine.Pump()
      assert(T.lookups[1]and T.lookups[1].state=='new'and state.block==nil,'A busy lock waits')
      state.now=state.now+15000;engine.Pump();assert(#T.lookups==0,'After 15 s the loaded area is used')
      rawset(_G,'__lwbridgeAoiOwner',nil)
      state.callbacks[1]({post=13,seqId=706,roomId='a',group=2,attachmentId={x=x,y=y,sid=123}})
      state.now=state.now+100;engine.Pump()
      assert(state.block and state.block.index.n==16 and #state.block.index.values==16,'4x4 blocks requested')
      assert(state.block.x==x and state.block.y==y and state.block.leftBottom==580*1000+380+1 and state.block.rightTop==620*1000+420+1)
      assert(T.aoi and T.aoi.lookup.state=='loading')
      state.now=state.now+100;engine.Pump()
      assert(T.aoi==nil and rawget(_G,'__lwbridgeAoiOwner')==nil and next(view)==nil,'Reply consumed, view restored')
      CS.SceneManager.World.PointManager=nil
    ''')
    # LwTool Dig Dispatch order and owner redirect: weakest/strongest by the game's formation power,
    # idle squads first, then a gathering squad redirected with SendChangeMarchToServer (borrow marked
    # for Gathering), never one in black tiles or doing anything else; Hunt alone looks 3 tiles out.
    vm.execute('''
      local T=engine._Treasure;engine.CleanupAfterLeaseLoss()
      LuaEntry.Player.uid='1001';LuaEntry.Player.allianceId='77';request.authorizedUid='1001';state.token='owner'
      MarchTargetType={DETECT_TREASURE=40,COLLECT=1,ATTACK_MONSTER=2}
      local power={[901]=500,[902]=100,[903]=300,[904]=900}
      local forms={};for i=1,4 do forms[i]={uuid=900+i,index=i,ownerUid='1001'}end
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,i)return forms[i]end,
        GetFormationPowerByUuid=function(self,uuid)return power[uuid]end}
      state.marches={};state.created={};state.changed={}
      DataCenter.WorldMarchDataManager={GetOwnerFormationMarch=function(self,uid,f)return state.marches[f]end}
      local function busy(f,kind,extra)
        local m={uuid=7700+f,ownerUid='1001',targetUuid=0,GetMarchTargetType=function()return kind end,
          GetIsBroken=function()return false end,GetMarchCurPos=function()return {tile=f}end}
        for k,v in pairs(extra or {})do m[k]=v end
        state.marches[f]=m;return m
      end
      SceneUtils={WorldToTileIndex=function(pos)return pos.tile end,IsInBlackRange=function(index)return state.black==index end}
      ForceChangeScene={World=1}
      MarchUtil={GetAutoBackHomeState=function()return 1 end,CheckFormation=function()return true end,
        SendCreateMarchMessage=function(f,kind,point,uuid)state.created[#state.created+1]=f
          busy(f,40).targetUuid=uuid end,
        SendChangeMarchToServer=function(march,kind,point,uuid,back,server,extra)
          state.changed[#state.changed+1]={march=march,kind=kind,point=point,uuid=uuid,back=back,server=server}
          for f,m in pairs(state.marches)do if m.uuid==march then busy(f,40).targetUuid=uuid end end end}
      local function dig(uuid,index)
        return {pointType=21,mainIndex=index,serverId=123,uuid=uuid,allianceId='77',ownerUid='3001',expireTime=0,
          GetType=function()return {FullName='TreasurePointInfo'}end,IsComplete=function()return false end,
          IsHaveGetReward=function()return false end,IsHaveWorking=function()return false end,IsReceiveAllReward=function()return false end}
      end
      local points={};CS.SceneManager.World.GetPointInfo=function(self,index)return points[index]end
      CS.SceneManager.World.PointManager=nil
      local function treasure(extra)
        local c={enabled=true,dispatchEnabled=true,dispatchDelayMs=0,dispatchSquadPriority={1,2,3,4},dispatchRetrySeconds=5}
        for k,v in pairs(extra or {})do c[k]=v end
        state.config={redPacket={enabled=false},fireworks={enabled=false},treasure=c}
        assert(engine.Configure(request).state=='confirmed')
        T.digs={};T.lookups={};T.shareSeen={}
      end
      local seq=800
      local function share(index)seq=seq+1
        state.callbacks[1]({post=50,seqId=seq,roomId='a',group=2,attachmentId={pos=index,sid=123}})
        for _=1,3 do state.now=state.now+100;engine.Pump()end
      end
      -- Weakest first (default): squad 2 (power 100); strongest first: squad 4 (power 900).
      treasure();points[100001]=dig(6001,100001);share(100001)
      assert(state.created[1]==902,'Weakest squad first: '..tostring(state.created[1]))
      state.marches={};state.created={};treasure({dispatchOrder='strong'});share(100001)
      assert(state.created[1]==904,'Strongest squad first')
      -- All checked squads out: one gathering (COLLECT) is redirected, one attacking is never touched.
      state.marches={};state.created={};rawset(_G,'__lwbridgeTreasureBorrow',nil)
      treasure({dispatchSquadPriority={1,3}})
      busy(901,2);busy(903,1)
      share(100001)
      assert(#state.created==0 and #state.changed==1,'Gathering squad redirected instead of creating a march')
      local c=state.changed[1]
      assert(c.march==8603 and c.kind==40 and c.point==100001 and c.uuid==6001 and c.back==true and c.server==123,
        'redirect args '..tostring(c.march)..' '..tostring(c.kind)..' '..tostring(c.point)..' '..tostring(c.uuid)..' '..tostring(c.back)..' '..tostring(c.server))
      assert(rawget(_G,'__lwbridgeTreasureBorrow')['903'],'Gathering is told the squad was borrowed')
      assert(T.digs['6001'].state=='joined'and T.digs['6001'].redirected==true and T.stats.redirected>=1,'Redirected squad confirmed on the dig')
      -- A gathering squad inside black tiles is not redirected; neither is any squad with redirect off.
      state.marches={};state.changed={};treasure({dispatchSquadPriority={3}});busy(903,1);state.black=903;share(100001)
      assert(#state.changed==0 and T.digs['6001'].state=='queued','Black-tile squad is left alone, retried later')
      state.black=nil;state.marches={};treasure({dispatchSquadPriority={3},redirectGathering=false});busy(903,1);share(100001)
      assert(#state.changed==0,'Redirect off never touches a gathering squad')
      -- Treasure Hunt alone looks 3 tiles out; a dig 4 tiles away needs Unshared Treasure.
      state.marches={};state.created={};treasure();points={[200001+4]=dig(6002,200001+4)};share(200001)
      assert(not T.digs['6002'],'Hunt stays within 3 tiles')
      treasure({unsharedEnabled=true});share(200001)
      assert(T.digs['6002'],'Unshared Treasure searches 15 tiles')
    ''')
    # LwTool claim timing: Click before sends early, a "not finished" refusal is retried, "all slots
    # taken" counts as late, Random waits up to 10 s, and my own radar treasure is skipped by default.
    vm.execute('''
      local T=engine._Treasure;local claimReply=require('Net.Msgs.RadarCenter.DetectEventClaimTreasureMessage')
      local function claims(uuid)local n=0;for _,x in ipairs(state.sent)do if x[1]==12 and (uuid==nil or x[2]==uuid)then n=n+1 end end;return n end
      local function dig(uuid,index,owner,finish)
        return {pointType=21,mainIndex=index,serverId=123,uuid=uuid,allianceId='77',ownerUid=owner or '3001',expireTime=0,completionTime=finish,
          GetType=function()return {FullName='TreasurePointInfo'}end,IsComplete=function()return false end,
          IsHaveGetReward=function()return false end,IsHaveWorking=function()return true end,IsReceiveAllReward=function()return false end}
      end
      local points={};CS.SceneManager.World.GetPointInfo=function(self,index)return points[index]end
      local function treasure(extra)
        local c={enabled=true,dispatchEnabled=false,dispatchDelayMs=0,dispatchSquadPriority={1},dispatchRetrySeconds=5}
        for k,v in pairs(extra or {})do c[k]=v end
        state.config={redPacket={enabled=false},fireworks={enabled=false},treasure=c}
        assert(engine.Configure(request).state=='confirmed');T.digs={};T.lookups={};T.shareSeen={};T.claiming=nil
      end
      local seq=900
      local function share(index)seq=seq+1
        state.callbacks[1]({post=50,seqId=seq,roomId='a',group=2,attachmentId={pos=index,sid=123}})
        state.now=state.now+10;engine.Pump()
      end
      -- Click before 200 ms: sent 200 ms before completion; "not finished" is retried 300 ms later.
      treasure({clickBeforeMs=200});points[300001]=dig(5001,300001,nil,state.now+1000);share(300001)
      state.now=state.now+700;engine.Pump();assert(claims(5001)==0)
      state.now=state.now+100;engine.Pump();assert(claims(5001)==1,'Sent at completion minus Click before')
      claimReply.HandleMessage({}, {errorCode='detect_dig_err_01'})
      state.now=state.now+200;engine.Pump();assert(claims(5001)==1)
      state.now=state.now+150;engine.Pump();assert(claims(5001)==2,'Early claim retried after the dig finishes')
      local late=T.stats.late;claimReply.HandleMessage({}, {errorCode='radar_tips_4'})
      assert(T.stats.late==late+1 and T.claiming==nil,'All reward slots taken is counted as late, not retried')
      state.now=state.now+1000;engine.Pump();assert(claims(5001)==2)
      -- Negative Click before waits after completion; Random adds up to 10 s (fixture: half).
      treasure({clickBeforeMs=-500,randomClaimEnabled=true});points[300101]=dig(5002,300101,nil,state.now+1000);share(300101)
      state.now=state.now+1000+500+4900;engine.Pump();assert(claims(5002)==0,'Click later + random not yet due')
      state.now=state.now+200;engine.Pump();assert(claims(5002)==1)
      claimReply.HandleMessage({}, {})
      -- My own radar treasure is not auto-opened unless that option is off.
      treasure();points[300201]=dig(5003,300201,'1001',state.now+100);share(300201)
      state.now=state.now+1000;engine.Pump();assert(claims(5003)==0,'Own radar treasure skipped by default')
      treasure({skipOwnRadar=false});share(300201);state.now=state.now+100;engine.Pump()
      assert(claims(5003)==1,'Own radar treasure claimed when the option is off')
      claimReply.HandleMessage({}, {})
    ''')
    print('FEATURE018_CHAT_ENGINE_OK native-shaped fixtures: listener, Red Packet/Fireworks claims, Treasure LwTool logic (alliance point shares, hunt 3 / unshared 15 tiles, weak/strong order, gathering redirect, completion-time claim with click-before/random/retry/late, own-radar skip), delays, stop, reply, UUID fencing, foreign UID/session, idle no-rescan, active hours, audit 01-04')


if __name__ == '__main__':
    main()
