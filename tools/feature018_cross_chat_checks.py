"""Inert native-room history permission, bounds and ownership regressions."""
from feature018_gold_egg_checks import runtime


def cross_runtime():
    vm = runtime()
    vm.execute("""
      ChatEventEnum.CHAT_ROOM_HISTORYMSG_UPDATA=5
      manager=ChatManager2:GetInstance();roomManager=manager.Room
      state.nativeRooms={};state.nativeRoomData={};state.history={};state.season={123,124,125}
      DataCenter.SeasonDataManager={GetServerListInt=function()return state.season end}
      roomManager.GetGameServerRoomList=function()return state.nativeRooms end
      roomManager.GetRoomData=function(self,id)return state.nativeRoomData[id]end
      roomManager.requestMultiRoomLatestMsg=function(self,ids)
        if state.nativePending then for i=#ids,1,-1 do table.remove(ids,i)end;return true end
        local copy={};for _,id in ipairs(ids)do copy[#copy+1]=id end
        state.history[#state.history+1]=copy;return true
      end
      function addRoom(id)
        state.nativeRooms[#state.nativeRooms+1]=id
        state.nativeRoomData[id]={roomId=id,GetUnblockedChatDatas=function()
          state.historyReads=(state.historyReads or 0)+1;return state.historyMessages or {} end}
      end
      addRoom('game_country_123');addRoom('game_country_124');addRoom('game_country_999')
      addRoom('game_alliance_124');state.nativeRooms[#state.nativeRooms+1]='game_country_125'
      engine.CleanupAfterLeaseLoss()
      state.config.goldEgg.crossServerEnabled=true;engine.Configure(request)
    """)
    return vm


def main():
    vm=cross_runtime()
    vm.execute("""
      local catalog=engine.Catalog({authorizedUid='1001'}).data
      assert(catalog.crossServer.eligibleKnownRemoteRooms==1 and #state.history==0 and #state.sent==0)
      engine.Pump();assert(#state.history==1 and #state.history[1]==1 and state.history[1][1]=='game_country_124')
      assert(#state.nativeRooms==5,'Native permission list is never mutated')
      for i=1,14 do state.now=state.now+1000;engine.Pump()end
      assert(#state.history==1,'At most one history batch every 15 seconds')
      state.now=state.now+1000;engine.Pump();assert(#state.history==2)
      state.historyMessages={packet(100,'known-cross',1,124)}
      state.callbacks[5]({roomId='game_country_999'});engine.Pump();assert(not state.historyReads)
      state.callbacks[5]({roomId='game_country_124'});state.callbacks[5]({roomId='game_country_124'})
      engine.Pump();assert(state.historyReads==1 and #state.sent==1)
      engine.Pump();assert(state.historyReads==1 and #state.sent==1,'Callbacks coalesce; rewards never replay')
      state.callbacks[5]({roomId='game_country_124'});state.nativeRooms={};engine.Pump()
      assert(state.historyReads==1,'Revoked native room permission discards pending inspection')
      engine.CleanupAfterLeaseLoss();assert(state.callbacks[5]==nil)
    """)
    vm=cross_runtime()
    vm.execute("""
      state.nativePending=true;engine.Pump()
      assert(#state.history==0 and engine.Status().data.services.goldEgg.crossServer.historyBatches==0,
        'Native pending-request dedup is not counted as a submitted batch')
      state.nativePending=false;state.nativeRooms={};state.nativeRoomData={}
      for i=1,40 do local sid=200+i;state.season[#state.season+1]=sid;addRoom('game_country_'..sid)end
      state.now=state.now+15000;engine.Pump()
      assert(#state.history[1]==8 and engine.Status().data.services.goldEgg.crossServer.eligibleRooms==32)
      local seen={};for _,id in ipairs(state.history[1])do seen[id]=true end
      state.now=state.now+15000;engine.Pump();for _,id in ipairs(state.history[2])do assert(not seen[id])end
      state.historyMessages={};for i=1,250 do state.historyMessages[i]=packet(100,'bound-'..i,i,201)end
      local before=engine.Status().data.listener.eventMessages
      state.callbacks[5]({roomId=state.history[1][1]});engine.Pump()
      assert(engine.Status().data.listener.eventMessages-before<=200,'History parse work is bounded per tick')
    """)
    for mode in ('off','legacy','offHours','ownerLoss'):
        vm=cross_runtime();vm.globals().mode=mode
        vm.execute("""
          if mode=='off'then state.config.goldEgg.crossServerEnabled=false
          elseif mode=='legacy'then state.config.goldEgg.legacyCombined=true
          elseif mode=='offHours'then
            os.date=function()return {hour=12,min=0}end
            state.config.goldEgg.activeHours={enabled=true,start='01:00',['end']='02:00'}
          end
          engine.Configure(request);if mode=='ownerLoss'then state.token='new owner'end
          engine.Pump();assert(#state.history==0 and #state.sent==0,mode..' must not request or claim')
        """)
    print('CROSS_CHAT_CHECKS_PASS native permissions no guessed rooms bounds coalescing opt-out hours owner loss cleanup')


if __name__=='__main__':main()
