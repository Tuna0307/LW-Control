"""Inert Alliance Gathering Dispatch through the actual scheduler + adapter; no game or network."""
from feature018_automation_engine_checks import ROOT, runtime


def alliance():
    vm=runtime()
    vm.globals().gatherSource=(ROOT/'tools/feature018_alliance_gather_native.lua').read_text()
    vm.execute('''
      package.preload['DataCenter.Global.LWBridgeFeature018AllianceGatherNative']=function()return load(gatherSource)()end
      state.config={allianceGather={enabled=true,allianceGatherSquads={2,1}}}
      request.kind='allianceGather';state.callbacks={};state.marches={};state.starts={}
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_allianceGather');state.perf=state.perf+1 end
      LuaEntry.Player.allianceId='77';LuaEntry.Player.IsInAlliance=function()return state.noAlliance~=true end
      LuaEntry.Player.GetCurWorldId=function()return 0 end
      EventId={WorldGetAllianceCollectResDetailUpdate=40};MsgDefines.WorldGetAllianceCollectResDetail=41
      MarchTargetType={ALLIANCE_RESOURCE_COLLECT=31};TableName={AllianceMine=9}
      local em={AddListener=function(self,e,cb)state.callbacks[e]=cb end,RemoveListener=function(self,e,cb)if state.callbacks[e]==cb then state.callbacks[e]=nil end end}
      EventManager={GetInstance=function()return em end}
      -- A Doom Elite drop: world point type 26 of alliance 77, capped at 3 gatherers.
      state.drop={PointType=26,pointIndex=5005,uuid=900,allianceId='77',serverId=123,configId=4001}
      state.points={[0]={PointType=1,pointIndex=1},[1]=state.drop}
      state.detail={playerInfoLiset={{uid='3003'}},remainValue=5000,GetCurResourceValue=function(self)return self.remainValue end}
      SFSNetwork.SendMessage=function(route,...)state.sends[#state.sends+1]={route=route,args={...}}end
      CS={SceneManager={World={PointManager={_pointInfos=setmetatable({Count=2},{__index=function(_,i)return state.points[i]end})}}}}
      LocalController={instance=function()return {getLine=function(self,t,cfg)assert(t==9 and cfg==4001);return {guard_num=3}end}end}
      state.forms={}
      for i=1,4 do state.forms[i]={uuid=600+i,index=i,ownerUid='1001',totalSoldierNum=0,ConscriptSoldier=function(self)self.totalSoldierNum=100 end}end
      DataCenter.WorldPointDetailManager={GetAllianceResourceData=function(self,uuid)if uuid==900 and state.detailLoaded then return state.detail end end}
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,i)return state.forms[i]end}
      DataCenter.WorldMarchDataManager={GetOwnerFormationMarch=function(self,uid,f)return state.marches[f]end,
        GetOwnerMarches=function()local out={};for _,m in pairs(state.marches)do out[#out+1]=m end;return out end}
      MarchUtil={GetAutoBackHomeState=function(t)assert(t==31);return true end,
        StartMarch=function(t,point,uuid,timeIndex,zero,formation,back,nilarg,zero2,server)
          assert(t==31 and point==5005 and uuid==900 and timeIndex==-1 and zero==0 and back==true and nilarg==nil and zero2==0 and server==123)
          state.starts[#state.starts+1]=formation
          if state.throwAfter then error('wire outcome unknown')end
          if not state.noReply then state.marches[formation]={uuid=7000+formation,ownerUid='1001',targetUuid=900,targetPos=5005,GetMarchTargetType=function()return 31 end}end
        end}
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services.allianceGather end
      function answer()if state.sends[#state.sends] and state.sends[#state.sends].route==41 then state.detailLoaded=true;state.callbacks[40]()end end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm


def main():
    vm=alliance()
    vm.execute('''
      step();assert(state.sends[1].route==41 and state.sends[1].args[1]==900 and state.sends[1].args[3]==5005,'Fresh drop detail first');answer();step();step()
      assert(#state.starts==1 and state.starts[1]==602,'First idle squad in saved priority order (2 before 1)')
      step();assert(status().confirmed==1 and status().after.mineUuid=='900')
      -- One of your squads per drop (game tip season_tips212): squad 1 must not be sent to the same drop.
      for _=1,70 do step();answer()end
      assert(#state.starts==1,'Never a second own squad at the same drop')
    ''')
    for case in ['off','noAlliance','full','empty','busy','otherAlliance','noDrop','wrongAccount','throwAfter','noReply','refusedBefore','stop']:
        vm=alliance()
        if case=='off':
            vm.execute("state.config.allianceGather.enabled=false;engine.Configure(request);for _=1,30 do step()end;assert(#state.sends==0 and #state.starts==0 and state.perf==0)")
            continue
        if case=='noAlliance':
            vm.execute("state.noAlliance=true;step();assert(#state.sends==0 and status().state=='nothing_available')")
            continue
        vm.execute('step();answer()')
        if case=='full':vm.execute("state.detail.playerInfoLiset={{uid='3'},{uid='4'},{uid='5'}}")
        elif case=='empty':vm.execute('state.detail.remainValue=0')
        elif case=='otherAlliance':vm.execute("state.drop.allianceId='88'")
        elif case=='noDrop':vm.execute('state.points[1]={PointType=1,pointIndex=2}')
        elif case=='busy':vm.execute('for i=1,4 do state.marches[600+i]={uuid=1,targetUuid=1,GetMarchTargetType=function()return 8 end}end')
        elif case=='wrongAccount':vm.execute("LuaEntry.Player.uid='2002'")
        elif case=='throwAfter':vm.execute('state.throwAfter=true')
        elif case=='noReply':vm.execute('state.noReply=true')
        elif case=='refusedBefore':vm.execute('state.forms[2].ConscriptSoldier=function()end;state.forms[1].ConscriptSoldier=function()end')
        elif case=='stop':vm.execute('engine.Stop(request)')
        vm.execute('step();step()')
        if case in ['full','empty','busy','otherAlliance','noDrop']:vm.execute("assert(#state.starts==0 and status().state=='nothing_available')")
        elif case in ['wrongAccount','stop']:vm.execute('assert(#state.starts==0 and not engine.IsActive())')
        elif case=='refusedBefore':vm.execute("assert(#state.starts==0 and status().state=='waiting' and status().submitted==0,'Refused before the game call is not uncertain')")
        else:
            vm.execute("assert(#state.starts==1);state.now=state.now+16000;engine.Pump();assert(status().state=='unconfirmed')")
            vm.execute("for _=1,120 do step();answer()end;assert(#state.starts==1,'Never replay an uncertain march')")
    print('FEATURE018_R30_ALLIANCE_GATHER_LUA_PASS doom-drop fresh-detail priority one-per-drop off no-alliance full empty busy other-alliance no-drop account uncertain no-replay refused-before stop')


if __name__=='__main__':main()
