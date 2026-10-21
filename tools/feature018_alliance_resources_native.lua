-- RECOVERED v26 routes/settings; OWN_DESIGN scheduling and strict reconciliation.
-- Source: artifacts/feature018-r30/research-later/{center-click,stronghold-read-response}.txt.
-- Requiring this module does no game work. The scheduler owns UID/lease admission.
local M={}
local contexts={}
local function get(o,k)local ok,v=pcall(function()return o and o[k]end);if ok then return v end end
local function call(o,k,...)local f=get(o,k);if type(f)~='function'then error('Native '..k..' unavailable')end;return f(o,...)end
local function dc(k)return get(rawget(_G,'DataCenter'),k)end
local function route(k)local r=get(rawget(_G,'MsgDefines'),k);if not r then error('Native '..k..' route unavailable')end;return r end
local function account()
 local p=get(rawget(_G,'LuaEntry'),'Player');local uid=get(p,'uid');local server=tonumber(call(p,'GetSourceServerId'))
 if not uid or not server or server<=0 or tonumber(call(p,'GetCurServerId'))~=server then error('Home game account unavailable')end
 return tostring(uid)..'/'..server,server
end
local function seasonal(name)
 local u=rawget(_G,'SeasonUtil');local f=get(u,name)
 if type(f)~='function'then error('Native season availability unavailable')end
 return f()==true
end
local function listen(c,name,key)
 local event=get(rawget(_G,'EventId'),name);if not event then error('Native '..name..' event unavailable')end
 local fn=function(data)c.versions[key]=(c.versions[key]or 0)+1;c.payloads[key]=data end
 call(c.manager,'AddListener',event,fn);c.listeners[#c.listeners+1]={event,fn}
end
local function context(kind)
 local owner,server=account();local c=contexts[kind]
 if c and c.owner~=owner then M.Stop(kind);error('The game account changed')end
 if not c then
  c={owner=owner,server=server,versions={},payloads={},listeners={},manager=call(rawget(_G,'EventManager'),'GetInstance')}
  contexts[kind]=c
  if kind=='strongholdResource'then
   listen(c,'CrossOccupyStrongholdListUpdate','list');listen(c,'CollectStrongholdResourceSuccess','claim')
  else listen(c,'UpdateAllAllianceMineList','mines');listen(c,'AllianceStoveCenterUpdate','list')end
 end
 return c
end
local function rows(kind,c)
 local result={available=0,rows={}}
 if kind=='strongholdResource'then
  local list=get(dc('SeasonDataManager'),'CrossOccupyStrongholdRewardInfo')
  if type(list)~='table'then error('Fresh stronghold rewards unavailable')end
  for _,r in pairs(list)do
   local id,server,n=tonumber(get(r,'id')),tonumber(get(r,'serverId')),tonumber(get(r,'leftNum'))
   -- Never infer a foreign/missing server or operate on bank records.
   if id and id>0 and server==c.server and n and n>=0 then
    result.rows[id]={id=id,serverId=server,amount=n};if n>0 then result.available=result.available+1 end
   end
  end
 else
  local mine=dc('AllianceMineManager');local list=get(get(mine,'militaryCenterBuildInfos'),'info')
  local buildings=get(mine,'allianceMilitaryAttachmentList')
  if type(list)~='table'or type(buildings)~='table'then error('Fresh alliance production unavailable')end
  local normal=get(rawget(_G,'AllianceMineStatus'),'Normal');if normal==nil then error('Native alliance building status unavailable')end
  local eligible={}
  for _,b in pairs(buildings)do
   local id,level=tonumber(get(b,'buildId')),tonumber(get(b,'level'))
   if id and id>0 and level and level>0 and get(b,'status')==normal and call(b,'Injuried')==false then eligible[id]=true end
  end
  for _,r in pairs(list)do
   local id,n=tonumber(get(r,'buildId')),tonumber(get(r,'num'))
   if id and eligible[id]and n and n>=0 then
    result.rows[id]={id=id,serverId=c.server,amount=n};if n>0 then result.available=result.available+1 end
   end
  end
 end
 return result
end
local function query(kind,c,phase)
 local name=kind=='strongholdResource'and 'GetCrossOccupyStrongholdList'or phase=='mines'and 'GetAllAllianceMineList'or 'FetchMilitaryCenterBuildInfo'
 local key=phase=='mines'and 'mines'or 'list'
 return {readOnly=true,phase=phase or 'list',route=route(name),args={},version=c.versions[key]or 0,owner=c.owner}
end
function M.Supports(kind)return kind=='strongholdResource'or kind=='allianceCenterResource'end
function M.Interval(kind,config)
 local n=tonumber(config.intervalMinutes)or 60
 if n~=math.floor(n)or n<1 or n>1440 then n=60 end
 return n*60000
end
function M.Plan(kind,config,at)
 if not M.Supports(kind)then error('Unsupported alliance resource service')end
 local interval=tonumber(config.intervalMinutes)or 60
 if interval~=math.floor(interval)or interval<1 or interval>1440 then error('Collection interval must be 1 to 1440 minutes')end
 if not seasonal(kind=='strongholdResource'and 'SeasonHasCityStronghold'or 'SeasonHasMilitaryCenterAttachment')then
  return nil,{available=0,waitingForEvent=true,reason='This seasonal production feature is unavailable'}
 end
 if call(get(rawget(_G,'LuaEntry'),'Player'),'IsInAlliance')~=true then return nil,{available=0,reason='Join an alliance first'}end
 local c=context(kind)
 if kind=='allianceCenterResource'and(not c.minesFresh or at-c.minesFresh>30000)then return query(kind,c,'mines'),{fresh=false}end
 if not c.fresh or at-c.fresh>30000 then return query(kind,c,'list'),{fresh=false}end
 local before=rows(kind,c);local ids={};for id,r in pairs(before.rows)do if r.amount>0 then ids[#ids+1]=id end end;table.sort(ids)
 if not ids[1]then before.reason='No production resources are ready';return nil,before end
 local r=before.rows[ids[1]];before.id=r.id;before.amount=r.amount;before.serverId=r.serverId
 return {route=route(kind=='strongholdResource'and 'CollectStrongholdResource'or 'FetchMilitaryCenterBuildResult'),
  args=kind=='strongholdResource'and {r.serverId,r.id}or {r.id},id=r.id,owner=c.owner,
  claimVersion=c.versions.claim or 0,listVersion=c.versions.list or 0,continue=true},before
end
function M.Verify(kind,job)
 local c=contexts[kind];local owner=account();if not c or c.owner~=owner or job.owner~=owner then return false end
 if job.readOnly then
  local key=job.phase=='mines'and 'mines'or 'list'
  if (c.versions[key]or 0)<=job.version then return false end
  if key=='mines'then c.minesFresh=job.sentAt;return true,{fresh=true,phase='mines'}end
  local after=rows(kind,c);c.fresh=job.sentAt;return true,after
 end
 if kind=='strongholdResource'then
  local ack=c.payloads.claim
  if (c.versions.claim or 0)<=job.claimVersion or get(ack,'errorCode')~=nil or
   tonumber(get(ack,'serverId'))~=job.before.serverId or tonumber(get(ack,'strongholdId'))~=job.id then return false end
 else
  if (c.versions.list or 0)<=job.listVersion then return false end
 end
 if not job.postRead then
  job.postRead=true;job.postVersion=c.versions.list or 0;job.followupReads=1
  local q=query(kind,c,'list');get(rawget(_G,'SFSNetwork'),'SendMessage')(q.route,table.unpack(q.args));return false
 end
 if (c.versions.list or 0)<=job.postVersion then return false end
 local after=rows(kind,c);local r=after.rows[job.id]
 if not r then return false,after end
 after.id=job.id;after.amount=r.amount;after.serverId=r.serverId
 if r.amount<job.before.amount then c.fresh=job.sentAt;return true,after end
 return false,after
end
function M.Stop(kind)
 local c=contexts[kind];if c then for _,r in ipairs(c.listeners)do pcall(call,c.manager,'RemoveListener',r[1],r[2])end end
 contexts[kind]=nil
end
function M.Observe(kind,config,at,scope)
 local c=contexts[kind]
 if config.enabled==true or c and not c.observationScope then return {state='failed',error='Turn this service off before inspecting its resources'}end
 if c and c.observationScope and c.observationScope~=scope then M.Stop(kind);c=nil end
 if c and c.observation then
  local job=c.observation;local yes,done=pcall(M.Verify,kind,job)
  if not yes then M.Stop(kind);return {state='failed',error=tostring(done)}end
  if not done then
   if at-job.sentAt>=15000 then M.Stop(kind);return {state='failed',error='Fresh production data was not received; no resources were claimed'}end
   return {state='accepted',data={readOnly=true,waiting=true}}
  end
  c.observation=nil
 end
 local yes,plan,before=pcall(M.Plan,kind,config,at)
 if not yes then M.Stop(kind);return {state='failed',error=tostring(plan)}end
 if plan and plan.readOnly then
  c=contexts[kind];c.observationScope=scope;plan.sentAt=at;plan.before=before;c.observation=plan
  local sent,err=pcall(get(rawget(_G,'SFSNetwork'),'SendMessage'),plan.route,table.unpack(plan.args))
  if not sent then M.Stop(kind);return {state='failed',error=tostring(err)}end
  return {state='accepted',data={readOnly=true,waiting=true}}
 end
 before=before or {};before.readOnly=true;before.seasonal=true;before.claimed=0;M.Stop(kind)
 return {state='confirmed',data=before}
end
return M
