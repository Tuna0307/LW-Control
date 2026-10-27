-- RECOVERED v26 SeasonTradeShopDataManager / native TradeShop UI contracts.
-- OWN_DESIGN serial scheduler and reconciliation. No UI purchase dialogs/payment APIs.
local M={}
local ctx,inspection
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' unavailable')end;return f(o,...)end
local function static(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' unavailable')end;return f(...)end
local function dc(k)return get(rawget(_G,'DataCenter'),k)end
local function route(k)local r=get(rawget(_G,'MsgDefines'),k);if r==nil then error('Native '..k..' route unavailable')end;return r end
local function account()
 local p=get(rawget(_G,'LuaEntry'),'Player');local u=get(p,'uid');local s=tonumber(call(p,'GetSourceServerId'))
 if not u or not s or s<=0 or tonumber(call(p,'GetCurServerId'))~=s then error('Home game account unavailable')end
 return tostring(u)..'/'..s,s
end
local function listen(c,name,fn)
 local event=get(rawget(_G,'EventId'),name);if not event then error('Native '..name..' event unavailable')end
 call(c.manager,'AddListener',event,fn);c.listeners[#c.listeners+1]={event,fn}
end
local function context()
 local owner,home=account();if ctx and ctx.owner~=owner then M.Stop('tradeStation');error('The game account changed')end
 if not ctx then
  local c={owner=owner,home=home,listVersions={},lists={},fresh={},listeners={},detailVersion=0,goodsVersion=0,actionVersions={},
   manager=call(rawget(_G,'EventManager'),'GetInstance'),processed={},details={}}
  ctx=c
  listen(c,'UpdateAllServerTradeInfoWithServer',function(data)
   local server=tonumber(get(data,'serverId'));local list=get(data,'list')
   if server and type(list)=='table'then c.listVersions[server]=(c.listVersions[server]or 0)+1;c.lists[server]=list end
  end)
  listen(c,'GetTradeDetail',function()c.detailVersion=c.detailVersion+1 end)
  -- Full goods reads have no configId; mutation/push events carry an ID.
  listen(c,'GetTradeShopGoodsInfo',function(id)
   if id==nil then c.goodsVersion=c.goodsVersion+1
   else id=tonumber(id);if id then c.actionVersions[id]=(c.actionVersions[id]or 0)+1 end end
  end)
 end
 return ctx
end
local function selected(list,id)for _,v in ipairs(list or {})do if tonumber(v)==id then return true end end;return false end
local function servers(c,config)
 local list={c.home};local seen={[c.home]=true}
 if config.crossServerEnabled==true then
  local info=call(dc('SeasonDataManager'),'GetUserSeasonInfo');local native=call(info,'GetServerListInt')
  if type(native)~='table'then error('Native seasonal server list unavailable')end
  for _,id in pairs(native)do id=tonumber(id);if id and id>0 and not seen[id]then seen[id]=true;list[#list+1]=id end end
 end
 table.sort(list);return list
end
local function identity(station,server)return tostring(server)..'/'..tostring(get(station,'tradeId'))end
local function query(c,phase,server,id,shop)
 local name,args,version
 if phase=='stations'then name='GetAllServerTradeMessage';args={server};version=c.listVersions[server]or 0
 elseif phase=='detail'then name='GetTradeDetailMessage';args={id,server};version=c.detailVersion
 else name='GetTradeShopGoodsInfoMessage';args={id,server,shop};version=c.goodsVersion end
 return {readOnly=true,phase=phase,route=route(name),args=args,owner=c.owner,serverId=server,tradeId=id,shopType=shop,version=version}
end
local function sameTrade(c,server,id)
 local t=get(dc('SeasonTradeShopDataManager'),'curTrade')
 return tonumber(get(t,'tradeId'))==id and tonumber(get(t,'serverId'))==server,t
end
local function currencyBalance(id)return tonumber(static(rawget(_G,'CommonUtil'),'GetResOrItemCount',id))end
function M.Supports(kind)return kind=='tradeStation'end
function M.Interval()return 60000 end
function M.Plan(kind,config,at)
 if kind~='tradeStation'then error('Unsupported trade service')end
 if not static(rawget(_G,'SeasonUtil'),'SeasonHasTradingStation')then
  return nil,{available=0,waitingForEvent=true,seasonal=true,reason='Trade stations are unavailable in this account season'}
 end
 if #(config.selectedItemIds or {})==0 or #(config.selectedCurrencyIds or {})==0 then
  return nil,{available=0,reason='Select goods and in-game purchase currencies first'}
 end
 local c=context();local shop=call(dc('SeasonTradeShopDataManager'),'GetShopType')
 if c.finished or c.shop~=shop then c.fresh={};c.processed={};c.details={};c.finished=false;c.shop=shop end
 local serverList=servers(c,config)
 for _,server in ipairs(serverList)do if not c.fresh[server]or at-c.fresh[server]>60000 then return query(c,'stations',server),{fresh=false,serverId=server}end end
 local stations={}
 for _,server in ipairs(serverList)do
  for _,t in pairs(c.lists[server])do
   local id=tonumber(get(t,'tradeId'))
   if id and id>0 and call(t,'GetShopState')==1 and not c.processed[identity(t,server)]then stations[#stations+1]={trade=t,server=server,id=id}end
  end
 end
 table.sort(stations,function(a,b)if a.server~=b.server then return a.server<b.server end;return a.id<b.id end)
 for _,s in ipairs(stations)do
  local key=identity(s.trade,s.server);local same,t=sameTrade(c,s.server,s.id)
  if not c.details[key]or not same then return query(c,'detail',s.server,s.id,shop),{fresh=false,serverId=s.server,tradeId=s.id}end
  if not c.goodsKey or c.goodsKey~=key or c.goodsShop~=shop or not c.goodsFresh or at-c.goodsFresh>15000 then
   return query(c,'goods',s.server,s.id,shop),{fresh=false,serverId=s.server,tradeId=s.id}
  end
  local manager=dc('SeasonTradeShopDataManager');local template=call(dc('AllianceCityTemplateManager'),'GetTemplate',s.id,s.server)
  local items=call(manager,'GetItemsByShopId',call(template,'GetShopId',shop))
  if type(items)~='table'then error('Native trade goods template unavailable')end
  local max=tonumber(call(manager,'GetTradePostShopValue','k1',0));local bought=tonumber(get(manager,'buyNumber'))
  local level=tonumber(call(dc('BuildManager'),'GetMainLevel'))
  if not max or not bought or not level then error('Fresh native trade limits unavailable')end
  if tostring(get(t,'uid')or '')~=''and bought<max then
   for _,item in ipairs(items)do
    local id,currency=tonumber(get(item,'id')),tonumber(get(item,'currency_id'))
    local count,selfCount=call(manager,'GetShopGoodsExchangeNum',id);count=tonumber(count);selfCount=tonumber(selfCount)
    local cap,minLevel,base=tonumber(get(item,'cycle_times')),tonumber(get(item,'level_low')),tonumber(get(item,'currency_num'))
    -- Original UI explicitly skips city-owner-exclusive items even if shown.
    if selected(config.selectedItemIds,tonumber(get(item,'itemId')))and selected(config.selectedCurrencyIds,currency)and
     get(item,'exclusive_flag')==false and count and count>=0 and selfCount and selfCount<1 and cap and count<cap and
     minLevel and level>=minLevel and currency and currency>0 and base and base>=0 then
     local discount=1
     if tostring(get(t,'allianceId'))==tostring(call(get(rawget(_G,'LuaEntry'),'Player'),'GetAllianceUid'))then
      discount=tonumber(get(template,'alliance_discount'));if not discount or discount<=0 or discount>1 then error('Native trade discount unavailable')end
     end
     local effect=tonumber(call(get(rawget(_G,'LuaEntry'),'Effect'),'GetGameEffect',get(rawget(_G,'EffectDefine'),'TradeShopDiscount')))
     if not effect or effect<0 or effect>=discount then error('Native trade discount effect unavailable')end
     discount=discount-effect
     local cost=math.ceil(base*discount);local balance=currencyBalance(currency)
     if not balance then error('In-game currency balance unavailable')end
     if balance>=cost then
      -- Native response increments buyNumber using this local field; it is never accepted as proof.
      if not inspection then manager.preExchangeNum=1 end
      return {route=route('TradeShopGoodsExchangeMessage'),args={s.id,id,1,s.server,shop},owner=c.owner,
       serverId=s.server,tradeId=s.id,shopType=shop,configId=id,actionVersion=c.actionVersions[id]or 0,continue=true},
       {available=1,serverId=s.server,tradeId=s.id,configId=id,itemId=tonumber(get(item,'itemId')),currencyId=currency,
        cost=cost,balance=balance,selfBuyNum=selfCount,exchangeNum=count,buyNumber=bought}
     end
    end
   end
  end
  c.processed[key]=true
 end
 c.finished=true;return nil,{available=0,seasonal=true,reason='No selected goods are currently available with the selected currencies'}
end
function M.Verify(kind,job)
 local c=ctx;local owner=account();if not c or owner~=c.owner or job.owner~=owner then return false end
 if job.readOnly then
  if job.phase=='stations'then
   if (c.listVersions[job.serverId]or 0)<=job.version then return false end
   c.fresh[job.serverId]=job.sentAt;return true,{fresh=true,serverId=job.serverId}
  elseif job.phase=='detail'then
   local same=sameTrade(c,job.serverId,job.tradeId)
   if c.detailVersion<=job.version or not same then return false end
   c.details[tostring(job.serverId)..'/'..job.tradeId]=true;return true,{fresh=true,tradeId=job.tradeId}
  elseif c.goodsVersion>job.version and sameTrade(c,job.serverId,job.tradeId)then
   if get(dc('SeasonTradeShopDataManager'),'shopType')~=job.shopType then return false end
   c.goodsKey=tostring(job.serverId)..'/'..job.tradeId;c.goodsShop=job.shopType;c.goodsFresh=job.sentAt;return true,{fresh=true}
  end
  return false
 end
 if not sameTrade(c,job.serverId,job.tradeId)then return false end
 if (c.actionVersions[job.configId]or 0)<=job.actionVersion then return false end
 if not job.postRead then
  job.postRead=true;job.postVersion=c.goodsVersion;job.followupReads=1
  local q=query(c,'goods',job.serverId,job.tradeId,job.shopType)
  get(rawget(_G,'SFSNetwork'),'SendMessage')(q.route,table.unpack(q.args));return false
 end
 if c.goodsVersion<=job.postVersion then return false end
 local manager=dc('SeasonTradeShopDataManager');if get(manager,'shopType')~=job.shopType then return false end
 local count,selfCount=call(manager,'GetShopGoodsExchangeNum',job.configId)
 local after={serverId=job.serverId,tradeId=job.tradeId,configId=job.configId,selfBuyNum=tonumber(selfCount),
  exchangeNum=tonumber(count),buyNumber=tonumber(get(manager,'buyNumber')),balance=currencyBalance(job.before.currencyId)}
 local yes=after.selfBuyNum==job.before.selfBuyNum+1 and after.exchangeNum and after.exchangeNum>=job.before.exchangeNum+1 and
  after.buyNumber==job.before.buyNumber+1 and after.balance and after.balance<=job.before.balance-job.before.cost
 if yes then c.goodsFresh=job.sentAt end
 return yes,after
end
function M.Stop(kind)
 if kind~='tradeStation'then return end
 if ctx then for _,r in ipairs(ctx.listeners)do pcall(call,ctx.manager,'RemoveListener',r[1],r[2])end end
 ctx=nil
end
local function catalog()
 local manager=dc('SeasonTradeShopDataManager');call(manager,'InitTemplate')
 local templates=get(manager,'templates');if type(templates)~='table'then error('Current season trade templates unavailable')end
 local items,currencies,byId,byCurrency={},{},{},{}
 local function name(id)
  local types=rawget(_G,'ResourceType');local lo,hi=tonumber(get(types,'None')),tonumber(get(types,'Max'))
  local ok,value
  if lo and hi and id>lo and id<hi then ok,value=pcall(call,dc('ResourceManager'),'GetResourceNameByType',id)
  else local yes,t=pcall(call,dc('ItemTemplateManager'),'GetItemTemplate',id);if yes and t then ok,value=pcall(call,t,'GetName')end end
  return ok and type(value)=='string'and value or '#'..tostring(id)
 end
 for shop,list in pairs(templates)do for _,offer in ipairs(list)do
  local id,currency=tonumber(get(offer,'itemId')),tonumber(get(offer,'currency_id'))
  if id and currency and currency>0 then
   local item=byId[id]
   if not item then item={itemId=id,name=get(offer,'itemName')or name(id),offers={}};byId[id]=item;items[#items+1]=item end
   item.offers[#item.offers+1]={configId=tonumber(get(offer,'id')),shopId=tonumber(shop),currencyId=currency,
    basePrice=tonumber(get(offer,'currency_num')),exclusive=get(offer,'exclusive_flag')==true}
   if not byCurrency[currency]then
    local balance=currencyBalance(currency);local row={id=currency,name=name(currency),balance=balance,
     premium=tonumber(get(rawget(_G,'ResourceType'),'Gold'))==currency}
    byCurrency[currency]=row;currencies[#currencies+1]=row
   end
  end
 end end
 table.sort(items,function(a,b)return a.itemId<b.itemId end);table.sort(currencies,function(a,b)return a.id<b.id end)
 return {items=items,currencies=currencies,nativeTemplate=true}
end
function M.Observe(kind,config,at,scope)
 if kind~='tradeStation'then return {state='failed',error='Unsupported trade observation'}end
 if config.enabled==true or ctx and not ctx.observationScope then return {state='failed',error='Turn Trade Station off before inspecting its catalog'}end
 local yes,available=pcall(static,rawget(_G,'SeasonUtil'),'SeasonHasTradingStation')
 if not yes then return {state='failed',error=tostring(available)}end
 if not available then M.Stop(kind);return {state='confirmed',data={readOnly=true,seasonal=true,available=0,waitingForEvent=true,
  reason='Trade stations are unavailable in this account season',catalog={items={},currencies={},nativeTemplate=true}}}end
 local ok,c=pcall(context);if not ok then return {state='failed',error=tostring(c)}end
 if c.observationScope and c.observationScope~=scope then M.Stop(kind);c=context()end
 c.observationScope=scope
 if not c.catalog then local done,cat=pcall(catalog);if not done then M.Stop(kind);return {state='failed',error=tostring(cat)}end;c.catalog=cat end
 if c.observation then
  local job=c.observation;local checked,done=pcall(M.Verify,kind,job)
  if not checked then M.Stop(kind);return {state='failed',error=tostring(done)}end
  if not done then
   if at-job.sentAt>=15000 then M.Stop(kind);return {state='failed',error='Fresh trade data was not received; no purchase was sent'}end
   return {state='accepted',data={readOnly=true,waiting=true,catalog=c.catalog}}
  end
  c.observation=nil
 end
 -- Inspect availability even before the owner selects goods; every plan is read-only here.
 local inspectConfig={crossServerEnabled=config.crossServerEnabled,selectedItemIds={},selectedCurrencyIds={}}
 for _,r in ipairs(c.catalog.items)do inspectConfig.selectedItemIds[#inspectConfig.selectedItemIds+1]=r.itemId end
 for _,r in ipairs(c.catalog.currencies)do inspectConfig.selectedCurrencyIds[#inspectConfig.selectedCurrencyIds+1]=r.id end
 inspection=true;local planned,plan,before=pcall(M.Plan,kind,inspectConfig,at);inspection=nil
 if not planned then M.Stop(kind);return {state='failed',error=tostring(plan)}end
 if plan and plan.readOnly then
  plan.sentAt=at;plan.before=before;c.observation=plan
  local sent,err=pcall(get(rawget(_G,'SFSNetwork'),'SendMessage'),plan.route,table.unpack(plan.args))
  if not sent then M.Stop(kind);return {state='failed',error=tostring(err)}end
  return {state='accepted',data={readOnly=true,waiting=true,catalog=c.catalog}}
 end
 before=before or {};before.readOnly=true;before.seasonal=true;before.catalog=c.catalog;before.purchased=0
 M.Stop(kind);return {state='confirmed',data=before}
end
return M
