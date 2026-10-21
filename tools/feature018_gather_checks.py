"""Inert actual gathering adapter with source-shaped native world/message objects.

Direct Plan/BeforeSend/execute/Verify exercises the adapter; the main Automation
suite separately owns the scheduler's off/account/uncertain-send behavior.
"""
from feature018_automation_engine_checks import ROOT, runtime


def gathering():
    vm=runtime()
    vm.globals().gather=vm.execute((ROOT/'tools/feature018_gather_native.lua').read_text())
    vm.execute('''
      state.forms={};state.marches={};state.createToken='01234567-89ab-cdef-0123-456789abcdef'
      state.config={resourceGather={enabled=true,scanRadius=200,manualResumeDelaySeconds=120,recallOnDisable=false,
        squads={{squadIndex=1,enabled=true,resource='metal',level=10}}}}
      request.kind='resourceGather';state.sends={};state.reads=0
      state.point={PointType=7,mainIndex=1011,pointIndex=1011,serverId=123,ownerUid='0'}
      state.resource={id=401,mainIndex=1011,serverId=123,gatherMarchUuid='0',gatherUid='0',ownerUid='0'}
      state.forms[1]={uuid=41,index=1,ownerUid='1001',totalSoldierNum=2,heroes={1},
        ConscriptSoldier=function(self)state.staffed=(state.staffed or 0)+1 end}
      LuaEntry.Player.GetCurWorldId=function()return 0 end
      LuaEntry.Player.GetMainWorldPos=function()return 1001 end
      LuaEntry.Player.allianceId='12'
      LuaEntry.DataConfig={CheckSwitch=function(self,k)assert(k=='slide_left_return');return not state.confirmRecall end}
      ResourceType={Metal=1,Food=2,Gold=3};TableName={GatherResource=4}
      GetTableData=function(tab,cfg,k)assert(tab==4 and cfg==401);return ({resource_type=state.resType or 1,level=state.resLevel or 10})[k]end
      SceneUtils={IsInBlackRange=function(point,server)assert(point==1011 and server==123);return state.black==true end}
      DataCenter.ArmyFormationDataManager={GetOneArmyInfoByIndex=function(self,slot)state.reads=state.reads+1;return state.forms[slot]end}
      DataCenter.WorldMarchDataManager={GetOwnerFormationMarch=function(self,uid,formation,alliance)
        assert(uid==LuaEntry.Player.uid);state.reads=state.reads+1
        local m=state.marches[formation];return m and m.targetType~=9 and m or nil end,
        GetMarch=function(self,uuid)state.reads=state.reads+1;for _,m in pairs(state.marches)do if m.uuid==uuid then return m end end end}
      DataCenter.WorldPointDetailManager={GetDetailByPointId=function(self,p)assert(p==1011);return state.detail end}
      CS={SceneManager={World={GetPointInfo=function(self,p)state.reads=state.reads+1;if state.bad then error('native read failed')end;return p==1011 and state.point or nil end,
        GetResourcePointInfoByIndex=function(self,p)state.reads=state.reads+1;return p==1011 and state.resource or nil end}}}
      MsgDefines={FindResourcePoint=80,WorldGetDetail=81,WorldMarchFormationNew=82,WorldMarchChange=83}
      MarchTargetType={COLLECT=8,BACK_HOME=9}
      classes={}
      classes.FindResourcePointMessage={HandleMessage=function(self,response)state.searchApplied=(state.searchApplied or 0)+1;return 'original-search-result' end}
      classes.WorldMarchFormationNewMessage={OnCreate=function(self,formation,kind,target,path)
        self.sfsObj={GetUtfString=function(self,k)assert(k=='clientCreateUuid');return state.createToken end}
        return 'original-create-result'
      end,HandleMessage=function(self,response)state.createApplied=(state.createApplied or 0)+1;return 'original-reply-result'end}
      classes.WorldMarchChangeMessage={HandleMessage=function(self,response)state.recallApplied=true;return 'original-change-result'end}
      for key,value in pairs(classes)do package.preload['Net.Msgs.'..key]=function()return value end end
      local function rawSend(route,...)
        local args={...};state.sends[#state.sends+1]={route=route,args=args}
        if route==82 then
          local packet={};assert(classes.WorldMarchFormationNewMessage.OnCreate(packet,table.unpack(args))=='original-create-result')
        end
        if state.throw and (route==82 or route==83)then error('uncertain native wire outcome')end
      end
      state.rawSend=rawSend;SFSNetwork.SendMessage=rawSend
      MarchUtil={OnGotoCollect=function(formation,resource,targetUuid,isFormation)
        assert(formation==41 and resource==state.resource and targetUuid==0 and isFormation==true,'Source-causal targetUid, not serverId')
        if state.changeAtNative then state.resource.gatherMarchUuid='999'end
        if state.fallbackRoute then SFSNetwork.SendMessage('move.city.to.world')end
        SFSNetwork.SendMessage(82,formation,8,0,'1001;1011')
      end,OnBackHome=function(uuid)
        assert(uuid==61);SFSNetwork.SendMessage(83,uuid,9,0,'1011;1001')
      end}
      gather.SetBridge(bridge)
      function plan()local j,b=gather.Plan('resourceGather',state.config.resourceGather,state.now);if j then j.before=b;j.sentAt=state.now end;return j,b end
      function dispatch(j)
        local ok,why=gather.BeforeSend('resourceGather',j);if not ok then return false,why end
        if j.execute then return pcall(j.execute)end
        return pcall(SFSNetwork.SendMessage,j.route,table.unpack(j.args or {}))
      end
      function verify(j)return gather.Verify('resourceGather',j)end
      function find_result(point)
        assert(classes.FindResourcePointMessage.HandleMessage({}, {pointId=point or 1011})=='original-search-result')
      end
      function hydrate()
        searchJob=plan();assert(searchJob.phase=='search'and searchJob.readOnly);assert(dispatch(searchJob));assert(not verify(searchJob))
        find_result();assert(verify(searchJob));detailJob=plan();assert(detailJob.phase=='detail'and detailJob.readOnly)
        assert(dispatch(detailJob));assert(not verify(detailJob));state.detail={pointId=1011,resourceId=401,remainRes=100}
        assert(verify(detailJob));job=plan();assert(job.phase=='gather'and type(job.execute)=='function')
      end
      function native_result()
        state.marches[41]={uuid=61,ownerUid='1001',ownerFormationUuid=41,serverId=123,clientCreateGuid=state.createToken,targetPos=1011,
          GetMarchTargetType=function(self)return self.targetType or 8 end}
      end
    ''')
    return vm


def main():
    # Live v27: CS.SceneManager.World returns a fresh Lua wrapper on every read.
    vm=gathering()
    vm.execute('''
      local world=CS.SceneManager.World;CS.SceneManager=setmetatable({},{__index=function(_,k)if k=='World'then return setmetatable({},{__index=world})end end})
      hydrate()
      assert(#state.sends==2 and state.sends[2].route==81,'Search reply is accepted although the World wrapper differs per read')
    ''')
    # Live v27 empty tile: gatherUid is empty text. nil stays unknown (refused).
    vm=gathering()
    vm.execute('''state.resource.gatherUid='';state.point.ownerUid='';state.resource.ownerUid='';hydrate();assert(#state.sends==2 and state.sends[2].route==81,'Empty-text gatherer/owner is an empty tile')''')
    vm=gathering()
    vm.execute('''state.point.ownerUid='2002';searchJob=plan();assert(dispatch(searchJob));find_result();assert(verify(searchJob))
      local j,b=plan();assert(j==nil and b.gatherSquads[1].reason:find('player owner')and #state.sends==1,'A real owner is never targeted')''')
    vm=gathering()
    vm.execute('''state.resource.gatherUid=nil;searchJob=plan();assert(dispatch(searchJob));find_result();assert(verify(searchJob))
      local j,b=plan();assert(j==nil and b.gatherSquads[1].reason:find('unknown')and #state.sends==1,'Missing gatherer is unknown and never sent')''')
    vm=gathering()
    vm.execute('''
      assert(#state.sends==0 and state.reads==0,'Module load and SetBridge do zero work')
      hydrate()
      assert(#state.sends==2 and state.sends[1].route==80 and state.sends[1].args[1]==1 and state.sends[1].args[2]==10)
      local a=state.sends[2].args;assert(#a==5 and a[1]==1011 and a[2]==123 and a[3]==0 and a[4]==7 and a[5]=='0','Source-native five-argument detail')
      assert(dispatch(job));assert(#state.sends==3 and not verify(job),'Submission is not success')
      native_result();state.marches[41].clientCreateGuid='unrelated';assert(not verify(job))
      state.marches[41].clientCreateGuid=state.createToken
      local ok,receipt=verify(job);assert(ok and receipt.nativeGuidPostState and receipt.marchUuid=='61')
      local j,b=plan();assert(j==nil and b.gatherSquads[1].step=='busy')
      state.config.resourceGather.recallOnDisable=true
      local stop=gather.Stop('resourceGather');assert(#stop.cleanupJobs==1 and #state.sends==3,'Stop plans but does not send recall')
      local recall=stop.cleanupJobs[1];assert(dispatch(recall));assert(#state.sends==4 and not verify(recall))
      assert(#gather.Stop('resourceGather').cleanupJobs==0,'Save disabled after Stop must not resend')
      state.marches[41].targetType=9;state.marches[41].clientCreateGuid=''
      state.marches[41].ownerUid='2002';assert(not verify(recall),'Wrong owner cannot confirm return')
      state.marches[41].ownerUid='1001';state.marches[41].clientCreateGuid='replacement'
      assert(not verify(recall),'Different nonempty creation GUID cannot confirm return')
      state.marches[41].clientCreateGuid=''
      -- Native replies expose SFS getters, and BACK_HOME is no longer in the formation lookup.
      assert(DataCenter.WorldMarchDataManager:GetOwnerFormationMarch('1001',41,'12')==nil)
      local response={GetLong=function(self,k)return ({uuid=61,ownerFormationUuid=41})[k] end,
        GetUtfString=function(self,k)return k=='ownerUid'and '1001'or nil end,
        GetInt=function(self,k)assert(k=='target');return 9 end}
      assert(classes.WorldMarchChangeMessage.HandleMessage({}, response)=='original-change-result')
      assert(verify(recall));gather.FinishStop('resourceGather')
      assert(SFSNetwork.SendMessage==state.rawSend,'Restore exact owned observer chain')
      assert(classes.WorldMarchFormationNewMessage.OnCreate({},41,8,0,'1001;1011')=='original-create-result')
    ''')
    vm=gathering()
    vm.execute('''
      hydrate();assert(dispatch(job));native_result();assert(verify(job));state.config.resourceGather.recallOnDisable=true
      state.marches[41].clientCreateGuid=''
      local j,b=plan();assert(j==nil and b.gatherSquads[1].pauseReason==nil,'Arrival update clearing GUID retains exact engine-owned march')
      local recall=gather.Stop('resourceGather').cleanupJobs[1];assert(dispatch(recall))
      gather.Stop('resourceGather');state.marches[41].targetType=9;state.marches[41].clientCreateGuid=''
      local yes,receipt=verify(recall)
      assert(yes and receipt.nativePostState and not receipt.replyObserved and not receipt.exactRequestCausality,
        'Exact owned native return transition confirms without inventing a direct reply')
    ''')
    for case in ['occupied','unknownOccupancy','playerOwner','city','alliancePoint','server','identity','level','resource','radius','black','gameError']:
        vm=gathering();vm.execute('local j=plan();assert(dispatch(j));find_result();assert(verify(j))')
        if case=='occupied':vm.execute("state.resource.gatherMarchUuid='7'")
        elif case=='unknownOccupancy':vm.execute('state.resource.gatherUid=nil')
        elif case=='playerOwner':vm.execute("state.point.ownerUid='42'")
        elif case=='city':vm.execute('state.point.PointType=6')
        elif case=='alliancePoint':vm.execute('state.point.PointType=26')
        elif case=='server':vm.execute('state.resource.serverId=222')
        elif case=='identity':vm.execute('state.resource.mainIndex=1012')
        elif case=='level':vm.execute('state.resLevel=9')
        elif case=='resource':vm.execute('state.resType=2')
        elif case=='radius':vm.execute('LuaEntry.Player.GetMainWorldPos=function()return 900000 end')
        elif case=='black':vm.execute('state.black=true')
        else:vm.execute('state.bad=true')
        if case=='gameError':vm.execute('assert(not pcall(plan))')
        else:vm.execute("local j,b=plan();assert(j==nil and b.gatherSquads[1].step=='nothing_available')")
        vm.execute('assert(#state.sends==1,"Unsafe candidate may not cause details or a march")')
    for case in ['off','wrongAccount','wrongLease','occupiedBeforeSend','occupiedInsideNative','detailChanged','busyBeforeSend','throwThenProof','uncertain','badGuid','noStaff','nativeFallback']:
        vm=gathering();vm.execute('hydrate()')
        if case=='off':vm.execute("gather.Stop('resourceGather')")
        elif case=='wrongAccount':vm.execute("LuaEntry.Player.uid='1002'")
        elif case=='wrongLease':vm.execute("state.token='replacement'")
        elif case=='occupiedBeforeSend':vm.execute("state.resource.gatherUid='1002'")
        elif case=='occupiedInsideNative':vm.execute('state.changeAtNative=true')
        elif case=='detailChanged':vm.execute('state.detail={pointId=1011,resourceId=401,remainRes=0}')
        elif case=='busyBeforeSend':vm.execute('native_result()')
        elif case=='noStaff':vm.execute('state.forms[1].totalSoldierNum=0')
        elif case=='nativeFallback':vm.execute('state.fallbackRoute=true')
        else:vm.execute('state.throw=true')
        vm.execute('sent,reason=dispatch(job)')
        if case in ['throwThenProof','uncertain','badGuid']:
            vm.execute('assert(not sent and #state.sends==3)')
            if case=='throwThenProof':vm.execute('native_result();assert(verify(job))')
            elif case=='badGuid':vm.execute("native_result();state.marches[41].clientCreateGuid='stale';assert(not verify(job))")
            else:vm.execute('assert(not verify(job));assert(#state.sends==3)')
        else:vm.execute('assert(not sent and #state.sends==2,"Final fence refuses unsafe or stopped dispatch")')
    vm=gathering()
    vm.execute('''
      local j=plan();assert(dispatch(j));find_result(0);assert(verify(j));local j,b=plan()
      assert(j==nil and b.gatherSquads[1].step=='nothing_available'and #state.sends==1)
      gather.Stop('resourceGather');assert(SFSNetwork.SendMessage==state.rawSend)
    ''')
    vm=gathering()
    vm.execute('''
      hydrate();assert(dispatch(job));native_result();assert(verify(job))
      -- Another actor's same-formation march with a different GUID is never recallable.
      state.marches[41].clientCreateGuid='another-guid';state.config.resourceGather.recallOnDisable=true
      assert(#gather.Stop('resourceGather').cleanupJobs==0 and #state.sends==3)
    ''')
    vm=gathering()
    vm.execute('''
      hydrate();assert(dispatch(job));native_result();assert(verify(job));state.config.resourceGather.recallOnDisable=true
      LuaEntry.Player.uid='1002';assert(#gather.Stop('resourceGather').cleanupJobs==0 and #state.sends==3)
    ''')
    vm=gathering()
    vm.execute('''
      hydrate();assert(dispatch(job));native_result();assert(verify(job))
      state.marches[41].clientCreateGuid='manual';local j,b=plan();assert(j==nil and b.gatherSquads[1].step=='busy')
      state.marches[41]=nil;state.now=state.now+1000;j,b=plan()
      assert(j==nil and b.gatherSquads[1].step=='paused'and b.gatherSquads[1].manualResumeAt>state.now)
      state.now=state.now+120000;j,b=plan();assert(j.phase=='search','Resume only after saved manual delay')
    ''')
    vm=gathering()
    vm.execute('''
      hydrate();local replacement=function()end;SFSNetwork.SendMessage=replacement
      assert(not gather.BeforeSend('resourceGather',job));gather.Stop('resourceGather')
      assert(SFSNetwork.SendMessage==replacement,'Never overwrite another observer owner')
    ''')
    vm=gathering()
    vm.execute('''
      state.forms[2]={uuid=42,index=2,ownerUid='1001'}
      state.config.resourceGather.squads[2]={squadIndex=2,enabled=true,resource='food',level=8}
      local j=plan();assert(j.slot==1 and dispatch(j));find_result(0);assert(verify(j));assert(plan()==nil)
      j=plan();assert(j.slot==2 and j.args[1]==2 and j.args[2]==8,'Empty squad one must not starve squad two')
    ''')
    vm=gathering()
    vm.execute('''
      hydrate();assert(dispatch(job));native_result();assert(verify(job));assert(not dispatch(job),'Exact gathering job cannot replay')
      state.config.resourceGather.recallOnDisable=true;local r=gather.Stop('resourceGather').cleanupJobs[1]
      state.throw=true;assert(not dispatch(r));assert(not dispatch(r),'Exact recall job cannot replay')
      assert(#gather.Stop('resourceGather').cleanupJobs==0 and #state.sends==4,'Repeated Stop cannot replay an uncertain recall')
    ''')
    print('FEATURE018_R30_GATHER_LUA_PASS actual-module search-detail-guid occupied-never twice-fenced manual-delay owned-recall stop account lease uncertain')


if __name__=='__main__':main()
