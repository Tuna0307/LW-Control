"""Inert native seasonal Trade Station reads/purchases and strict post-read proof."""
from pathlib import Path
from feature018_automation_engine_checks import runtime


def trade():
    vm=runtime()
    vm.globals().native=vm.execute(Path(__file__).with_name('feature018_trade_native.lua').read_text())
    vm.execute('''
      state.config={tradeStation={enabled=true,crossServerEnabled=false,selectedItemIds={501},selectedCurrencyIds={15}}}
      request.kind='tradeStation';engine.Register(feature,bridge,native)
      bridge.PerformanceEnd=function(kind)assert(kind=='automation_tradeStation');state.perf=state.perf+1 end
      state.callbacks={};state.available=true;state.balance=1000;state.effect=0
      LuaEntry.Player.GetAllianceUid=function()return 'ally' end
      LuaEntry.Effect={GetGameEffect=function()return state.effect end};EffectDefine={TradeShopDiscount=19}
      CommonUtil={GetResOrItemCount=function(id)assert(id==15);return state.balance end}
      SeasonUtil={SeasonHasTradingStation=function()return state.available end}
      EventId={UpdateAllServerTradeInfoWithServer=1,GetTradeDetail=2,GetTradeShopGoodsInfo=3}
      EventManager={GetInstance=function()return {
        AddListener=function(self,id,f)state.callbacks[id]=f end,
        RemoveListener=function(self,id,f)if state.callbacks[id]==f then state.callbacks[id]=nil end end}end}
      MsgDefines={GetAllServerTradeMessage=91,GetTradeDetailMessage=92,GetTradeShopGoodsInfoMessage=93,TradeShopGoodsExchangeMessage=94}
      state.item={id=700,itemId=501,currency_id=15,currency_num=100,exclusive_flag=false,cycle_times=20,level_low=10}
      state.template={GetShopId=function()return 6 end,alliance_discount=0.8}
      state.station={tradeId=5,serverId=123,uid='town-owner',allianceId='ally',GetShopState=function()return 1 end}
      DataCenter.AllianceCityTemplateManager={GetTemplate=function(self,id,server)assert(id==5 and server==123);return state.template end}
      DataCenter.BuildManager={GetMainLevel=function()return 30 end}
      DataCenter.SeasonDataManager={GetUserSeasonInfo=function()return {GetServerListInt=function()return {123,124}end}end}
      DataCenter.SeasonTradeShopDataManager={curTrade={},shopType=1,buyNumber=0,shopGoodsList={},
        GetShopType=function()return 1 end,GetItemsByShopId=function()return {state.item}end,
        GetTradePostShopValue=function()return 10 end,
        GetShopGoodsExchangeNum=function(self,id)local r=self.shopGoodsList[id];return r and r.exchangeNum or -1,r and r.selfBuyNum or 0 end}
      SFSNetwork.SendMessage=function(route,...)state.sends[#state.sends+1]={route=route,args={...}};if state.throw and route==94 then error('uncertain exchange')end end
      function step()state.now=state.now+1000;engine.Pump()end
      function status()return engine.Status().data.services.tradeStation end
      function initial()
       step();assert(state.sends[1].route==91 and state.sends[1].args[1]==123)
       state.callbacks[1]({serverId=123,list={state.station}});step();step()
       assert(state.sends[2].route==92)
       DataCenter.SeasonTradeShopDataManager.curTrade=state.station;state.callbacks[2]();step();step()
       assert(state.sends[3].route==93)
       DataCenter.SeasonTradeShopDataManager.shopGoodsList={[700]={exchangeNum=2,selfBuyNum=0}};state.callbacks[3]();step();step()
      end
      assert(engine.Configure(request).state=='confirmed')
    ''')
    return vm


def main():
    vm=trade()
    vm.execute('''
      state.config.tradeStation.enabled=false;engine.Configure(request);local n=state.reads
      for _=1,100 do engine.Pump()end;assert(state.reads==n and #state.sends==0 and next(state.callbacks)==nil)
      state.config.tradeStation.enabled=true;engine.Configure(request);initial()
      local send=state.sends[4];assert(send.route==94 and send.args[1]==5 and send.args[2]==700 and send.args[3]==1 and send.args[4]==123 and send.args[5]==1)
      assert(status().confirmed==0 and status().before.cost==80)
      local manager=DataCenter.SeasonTradeShopDataManager
      manager.buyNumber=1;manager.shopGoodsList[700]={exchangeNum=3,selfBuyNum=1};state.balance=920
      step();assert(status().confirmed==0 and #state.sends==4,'Unsolicited local fields are not proof')
      state.callbacks[3](700);step();assert(#state.sends==5 and state.sends[5].route==93 and status().confirmed==0)
      state.callbacks[3]();step();assert(status().confirmed==1 and status().receipts[1].after.selfBuyNum==1)
      step();assert(status().state=='success');engine.Stop(request);assert(not engine.IsActive()and next(state.callbacks)==nil)
    ''')
    for case in ['unavailable','noneSelected','empty','crossServer','stale','wrongAccount','wrongIdentity','uncertain','offPending','gameError','exclusive','wrongCurrency','unaffordable','effect']:
        vm=trade()
        if case=='unavailable':
            vm.execute("state.available=false;step();assert(status().state=='nothing_available'and status().before.waitingForEvent and #state.sends==0)")
        elif case=='noneSelected':
            vm.execute("state.config.tradeStation.selectedItemIds={};engine.Configure(request);step();assert(status().state=='nothing_available'and #state.sends==0)")
        elif case=='empty':
            vm.execute("step();state.callbacks[1]({serverId=123,list={}});step();step();assert(status().state=='nothing_available'and status().submitted==0)")
        elif case=='crossServer':
            vm.execute("state.config.tradeStation.crossServerEnabled=true;engine.Configure(request);step();state.callbacks[1]({serverId=123,list={}});step();step();assert(state.sends[2].route==91 and state.sends[2].args[1]==124);state.callbacks[1]({serverId=124,list={}});step();step();assert(status().state=='nothing_available'and status().submitted==0)")
        elif case=='gameError':
            vm.execute("EventId.GetTradeDetail=nil;step();assert(status().state=='error'and #state.sends==0)")
        elif case=='stale':
            vm.execute("step();state.callbacks[1]({serverId=999,list={state.station}});step();assert(#state.sends==1);state.now=state.now+16000;engine.Pump();assert(status().state=='error')")
        elif case=='wrongAccount':
            vm.execute("step();LuaEntry.Player.uid='1002';step();assert(not engine.IsActive()and #state.sends==1 and next(state.callbacks)==nil)")
        elif case in ['exclusive','wrongCurrency','unaffordable']:
            if case=='exclusive':vm.execute('state.item.exclusive_flag=true')
            if case=='wrongCurrency':vm.execute('state.item.currency_id=999')
            if case=='unaffordable':vm.execute('state.balance=79')
            vm.execute("initial();assert(status().submitted==0 and status().state=='nothing_available')")
        elif case=='effect':
            vm.execute("state.effect=0.1;initial();assert(status().before.cost==math.ceil((0.8-0.1)*100))")
        else:
            vm.execute('state.throw=true;initial();assert(status().submitted==1)')
            if case=='offPending':vm.execute('engine.Stop(request)')
            elif case=='wrongIdentity':
                vm.execute("DataCenter.SeasonTradeShopDataManager.curTrade={tradeId=99,serverId=123};state.callbacks[3](700);step();assert(status().confirmed==0 and #state.sends==4)")
            vm.execute("local n=#state.sends;state.now=state.now+16000;engine.Pump();engine.Stop(request);state.config.tradeStation.enabled=true;engine.Configure(request);step();assert(#state.sends==n,'Uncertain exchange cannot replay after off/on')")
    vm=trade()
    vm.execute('''
      assert(native.Observe('tradeStation',state.config.tradeStation,state.now,'scope').state=='failed')
      state.config.tradeStation.enabled=false;engine.Configure(request);state.config.tradeStation.selectedItemIds={}
      ResourceType={None=0,Max=100,Gold=15}
      DataCenter.ResourceManager={GetResourceNameByType=function(self,id)assert(id==15);return 'Native premium currency' end}
      DataCenter.SeasonTradeShopDataManager.InitTemplate=function(self)self.templates={[6]={state.item}}end
      SFSNetwork.SendMessage=function(route,...)
       assert(route~=94,'Read-only observation cannot purchase, regardless of selections')
       state.sends[#state.sends+1]={route=route}
       if route==91 then state.callbacks[1]({serverId=123,list={state.station}})
       elseif route==92 then DataCenter.SeasonTradeShopDataManager.curTrade=state.station;state.callbacks[2]()
       elseif route==93 then DataCenter.SeasonTradeShopDataManager.shopGoodsList={[700]={exchangeNum=2,selfBuyNum=0}};state.callbacks[3]()end
      end
      local result
      for _=1,8 do state.now=state.now+1000;result=native.Observe('tradeStation',state.config.tradeStation,state.now,'scope');if result.state=='confirmed'then break end end
      assert(result.state=='confirmed'and result.data.readOnly and result.data.purchased==0 and result.data.available==1)
      assert(result.data.catalog.nativeTemplate and #result.data.catalog.items==1 and #result.data.catalog.currencies==1)
      assert(result.data.catalog.currencies[1].premium==true and result.data.catalog.currencies[1].balance==1000)
      assert(DataCenter.SeasonTradeShopDataManager.preExchangeNum==nil,'Inspection cannot prime a purchase')
      assert(next(state.callbacks)==nil)
      state.available=false;result=native.Observe('tradeStation',state.config.tradeStation,state.now,'scope')
      assert(result.state=='confirmed'and result.data.waitingForEvent and #result.data.catalog.items==0)
    ''')
    print('FEATURE018_R30_TRADE_LUA_PASS seasonal selected-currency serial fresh-query exact-poststate off stop uncertain no-replay account empty error cross-server')


if __name__=='__main__':main()
