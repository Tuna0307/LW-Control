import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { buildMapColumns } from '../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
import { MAP_KIND_KEYS, createMapApi } from '../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js';
import { getMapPreviewProvider } from '../../../../src/LWBridge.UI-0.3.17/src/mapPreviewApi.js';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../..');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=require('@babel/parser');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const contract=JSON.parse(fs.readFileSync(path.join(here,'source-contract.json')));
const source=fs.readFileSync(path.join(repo,contract.source.path),'utf8');
assert.equal(hash(source),contract.source.sha256);
function walk(node,list=[]) { if(!node||typeof node!=='object')return list;if(node.type)list.push(node);for(const value of Object.values(node))if(Array.isArray(value))value.forEach(child=>walk(child,list));else if(value&&typeof value==='object')walk(value,list);return list; }
const ast=parse(source,{sourceType:'module'}),nodes=walk(ast);
const decl=name=>nodes.find(node=>node.type==='FunctionDeclaration'&&node.id?.name===name);
const extract=node=>source.slice(node.start,node.end);
const names=['k','A','j','M','De','Oe','ke','w','T','Ke','qe','Je','N','P','F','Qe','$e','I','L','et','nt'];
const originalColumns=new Function(names.map(name=>extract(decl(name))).join('\n')+'\nconst '+contract.sets.O.expression+';const '+extract(nodes.find(node=>node.type==='VariableDeclarator'&&node.id?.name==='Ee'))+';return nt;')();
let comparisons=0;
const cases=[{}, {worldClaimState:'claimable',playerClaimState:'claimed'}, {worldClaimState:'',playerClaimState:'',claimBlockReason:'other_alliance'}, {worldClaimState:'charging',chargePercent:.375,playerClaimState:'unclaimed'}, {worldClaimState:'new-state',playerClaimState:'new-state'}, {playerClaimState:'claimed',claimBlockReason:'other_alliance'}];
for(const language of ['en','zh-CN','zh-TW','ja','ko','vi','id','ru','pt']) {
  const catalog=(await import(pathToFileURL(path.join(repo,`src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
  const t=(key,values={})=>{assert.ok(key in catalog,key);return catalog[key].replace(/\{(\w+)\}/g,(match,key)=>String(values[key]??match));};
  for(const refreshing of [false,true]) {
    const expected=originalColumns('treasure',t,language,{},'',refreshing),actual=buildMapColumns('treasure',t,language,{},'',refreshing);
    for(const row of cases) for(const column of [3,4]) {assert.equal(actual[column].value(row),expected[column].value(row));comparisons++;}
  }
}
// Exercise the actual recovered search producer with local, deferred responses.
async function sourceProducer({online=true,reading=false,sameServer=true,empty=false,queryFails=false,refreshFails=false,suppressedError=false}={}) {
  const flags=[],logs=[];let rows=[],refreshCalls=0,resolveRefresh,rejectRefresh;
  const pending=new Promise((resolve,reject)=>{resolveRefresh=resolve;rejectRefresh=reject;});
  const setRows=value=>rows=typeof value==='function'?value(rows):value;
  const merge=new Function('k',contract.helpers.He.expression+';return He;')((row,key)=>row[key]);
  const run=new Function('L','B','T','Zt','nr','ce','je','V','U','W','l','C','R','Le','Mn','g','He','w','b',contract.helpers.rr.expression+';return rr;')(
    'treasure',1,{current:0},()=>{},()=>({}),async()=>{if(queryFails)throw new Error('query failed');return {rows:empty?[]:[{uuid:'a',worldClaimState:''}],total:empty?0:1};},50,()=>{},setRows,()=>{},online,{isReading:reading,serverId:sameServer?321:322},321,{current:0},value=>flags.push(value),()=>{refreshCalls++;return pending;},merge,{current:message=>logs.push(message)},message=>logs.push(message),
  );
  await run();
  const whilePending={flags:[...flags],rows:structuredClone(rows),refreshCalls};
  if(refreshCalls) { if(refreshFails)rejectRefresh(new Error(suppressedError?'SCAN_RUNNING':'refresh failed'));else resolveRefresh({states:[{uuid:'a',worldClaimState:'claimable',playerClaimState:'unclaimed'}]}); }
  await new Promise(resolve=>setImmediate(resolve));
  return {whilePending,flags,rows,refreshCalls,logs};
}
const producerResults=[];
for(const [name,input] of Object.entries({offline:{online:false},scanRunning:{reading:true},wrongServer:{sameServer:false},empty:{empty:true},queryFailure:{queryFails:true},deferredSuccess:{},refreshFailure:{refreshFails:true},scanFailureSuppressed:{refreshFails:true,suppressedError:true}})) {
  const result=await sourceProducer(input);
  if(['offline','scanRunning','wrongServer','empty','queryFailure'].includes(name)) assert.equal(result.refreshCalls,0);
  else {assert.deepEqual(result.whilePending.flags,[true]);assert.equal(result.flags.at(-1),false);assert.equal(result.refreshCalls,1);}
  if(name==='deferredSuccess')assert.equal(result.rows[0].worldClaimState,'claimable');
  if(name==='refreshFailure')assert.ok(result.logs.some(message=>message.includes('refresh failed')));
  if(name==='scanFailureSuppressed')assert.deepEqual(result.logs,[]);
  producerResults.push({name,...result});
}
const pagePath='src/LWBridge.UI-0.3.17/src/MapDataPage.jsx',page=fs.readFileSync(path.join(repo,pagePath),'utf8');
const pageNodes=walk(parse(page,{sourceType:'module',plugins:['jsx']}));
const call=pageNodes.find(node=>node.type==='JSXOpeningElement'&&node.name.name==='MapTable');
const expression=call.attributes.find(node=>node.name?.name==='treasureStatesRefreshing').value.expression;
const flag=new Function('previewFixture','previewState','tab','previewTreasureStatesRefreshing','return '+page.slice(expression.start,expression.end));
let fenceCases=0;
for(const mode of ['preview','native','native-unavailable'])for(const state of ['map-treasure-checking','map-treasure','map-table-states',''])for(const kind of MAP_KIND_KEYS)for(const input of [true,false,'true',undefined]) {
  assert.equal(flag(mode==='preview'&&state.startsWith('map-'),state,kind,input),mode==='preview'&&state==='map-treasure-checking'&&kind==='treasure'&&input===true);fenceCases++;
}
for(const mode of ['native','native-unavailable'])assert.equal(getMapPreviewProvider(mode,'map-treasure-checking'),null);
const provider=getMapPreviewProvider('preview','map-treasure-checking');
assert.equal(provider.online,false);assert.equal(provider.previewTreasureStatesRefreshing,true);
assert.equal(getMapPreviewProvider('preview','map-treasure').previewTreasureStatesRefreshing,false);
for(const action of ['start','stop','clear','jumpServer','coordinateJump','setPlayerMark','exportCities']) await assert.rejects(provider.mapApi[action](),{code:'PREVIEW_NATIVE_ACTION_BLOCKED'});
const fixture=await provider.mapApi.search('treasure',{serverId:321,page:1,sorts:[{sortBy:'updatedAt',sortOrder:'desc'}]});
assert.equal(fixture.rows[0].worldClaimState,'');assert.equal(fixture.rows[0].playerClaimState,'');
assert.equal(fixture.rows[1].playerClaimState,'claimed');assert.equal(fixture.rows[2].claimBlockReason,'other_alliance');
// Current host commands exist, but no canonical mapApi methods expose them.
const nativeApi=createMapApi({profileId:'test',invoke:()=>{throw new Error('native invocation forbidden in this harness');},listen:()=>()=>{}});
const backendPath='src/LWBridge.UI-0.3.17/src/mapBackend.js',backendText=fs.readFileSync(path.join(repo,backendPath),'utf8');
for(const name of ['map_treasure_state_refresh','map_treasure_state_refresh_all','map_treasure_claim_status'])assert.ok(!backendText.includes(name));
assert.equal(nativeApi.refreshTreasureStates,undefined);
const hostPaths=['src/LWBridge.Desktop/Map317CommandService.cs','src/LWBridge.Desktop/ManualMapScanCommandService.cs'];
const hosts=hostPaths.map(relative=>{const value=fs.readFileSync(path.join(repo,relative),'utf8');const lines=value.split(/\r?\n/);return {path:relative,sha256:hash(value),locators:lines.flatMap((line,index)=>/map_treasure_(state_refresh|claim_status)/.test(line)?[{line:index+1,text:line.trim()}]:[])};});
for(const host of hosts)assert.ok(host.locators.length>=3);
const output={status:'PASS',scope:'Original/current display and isolated preview reachability; recovered producer exercised using synthetic responses only. Native host not invoked.',comparisons,fenceCases,producerResults,source:contract.source,current:{path:pagePath,sha256:hash(page)},frontend:{path:backendPath,sha256:hash(backendText),treasureMethodsExposed:false},hosts,nativeReachability:'BLOCKED within this UI-only scope: frontend methods, viewer context and operation lifecycle still require a separate connection assignment; host handlers already exist, usability is not established here.'};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'treasure-results.json'),JSON.stringify(output,null,2)+'\n');
console.log(`LWB317_TREASURE_CHECKING_OK ${comparisons} display comparisons; ${fenceCases} input fences; ${producerResults.length} original producer cases`);
