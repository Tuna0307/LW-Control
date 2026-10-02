import assert from 'node:assert/strict';
import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath,pathToFileURL} from 'node:url';
import {createOriginalHarness,optionsReply} from '../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs';
import {createHarness,repo,nodeText,deferred} from './harness.mjs';
const here=path.dirname(fileURLToPath(import.meta.url)),file=path.join(repo,'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx');
const current=fs.readFileSync(file,'utf8'), baseline=execFileSync('git',['show','123459d:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'],{cwd:repo,encoding:'utf8'});
const NOW=Date.UTC(2026,9,3,12),initial={serverId:321,scanRunId:'run',isReading:false,phase:'idle',selectedTypes:['city'],scanMode:'normal',readBlocks:0,progressPercent:0,startedAt:0,lastError:''};
const result={exportCases:[],scanCases:[],refreshCases:[],baselineDefects:[]};
async function boot(original,source=current,language='en',extras={}){
 const messages=(await import(pathToFileURL(path.join(repo,'src/LWBridge.UI-0.3.17/src/locales/'+language+'.js')).href)).default;
 const t=(key,values={})=>(messages[key]||key).replace(/\{(\w+)\}/g,(_,k)=>String(values[k]??`{${k}}`));let state={...initial,...extras.scan};const options=optionsReply({scanProgress:extras.stored||null});let p;
 if(original)p=await createOriginalHarness({online:true,now:NOW,language,serverId:321,scanState:state,initialTab:'city',tabMode:'controlled',translate:t,stubs:{dataOptions:{mode:'auto',value:options},cityExport:{mode:'manual'},scanStart:{mode:'manual'}}});
 else p=await createHarness(source,'refresh-feedback',{now:NOW,language,serverId:321,translate:t,props:{previewState:'map-city',online:true},dataOptions:options(321),apiExtensions:()=>({summary:async()=>({serverId:state.serverId,counts:{},scanState:state}),exportCities:(...args)=>{const d=deferred();p.exportCall={args,...d};return d.promise;},start:(...args)=>{const d=deferred();p.startCall={args,...d};return d.promise;}})});
 p.t=t;p.emit=async(next)=>{state={...state,...next};if(original)await p.emitScanState(next);else{p.liveStatusListener()(state);await p.settle();}};
 await p.mount();return p;
}
const button=(p,key)=>p.findNodes(n=>n.type==='button'&&nodeText(n)===p.t(key))[0];
const message=p=>p.findNodes(n=>n.props?.className==='map-claim-result').map(nodeText);
const scanErrors=p=>p.findNodes(n=>n.props?.className==='map-scan-error').map(n=>({role:n.props.role,text:nodeText(n)}));
const defined=v=>JSON.parse(JSON.stringify(v));
async function exporter(p,original,outcome){
 const pending=button(p,'map.exportExcel').props.onClick();await p.settle();
 assert.ok(button(p,'map.exportingExcel')?.props.disabled,'export busy');
 const call=original?p.calls.filter(c=>c.name==='cityExport').at(-1):p.exportCall;
 if(outcome.reject)call.reject(outcome.reject);else call.resolve(outcome.reply);
 await pending;await p.settle();assert.equal(Boolean(button(p,'map.exportExcel').props.disabled),false);return {args:defined(call.args),message:message(p),queryErrors:original?[]:p.getState('queryError')};
}
for(const language of ['en','ja','zh-CN','zh-TW','ko','pt','id','vi','ru'])for(const outcome of [
 {id:'success',reply:{canceled:false,rowCount:37,path:'C:/Fixture/東京 export.xlsx'}},
 {id:'canceled',reply:{canceled:true,rowCount:0,path:''}},
 {id:'known-error',reject:new Error('MAP_SCAN_START_FAILED')},
 {id:'generic-error',reject:new Error('synthetic failure')},
 {id:'object-error',reject:{code:'GAME_NOT_CONNECTED',message:'local synthetic'}},
 ]){
 const o=await boot(true,current,language),c=await boot(false,current,language);const ov=await exporter(o,true,outcome),cv=await exporter(c,false,outcome);
 assert.deepEqual(cv.args,ov.args,language+'/'+outcome.id+' export args');assert.deepEqual(cv.message,ov.message,language+'/'+outcome.id+' message');assert.equal(cv.queryErrors,'');
 if(language==='en'||language==='ja'){const b=await boot(false,baseline,language);const bv=await exporter(b,false,outcome);if(JSON.stringify(bv)!==JSON.stringify({...ov,queryErrors:''}))result.baselineDefects.push('export/'+language+'/'+outcome.id);await b.unmount();}
 result.exportCases.push({language,id:outcome.id,pass:true});await o.unmount();await c.unmount();
}
for(const language of ['en','ja','pt'])for(const spec of [
 {id:'live-error',scan:{lastError:'SCAN_RUNNING'}},
 {id:'stored-error',scan:{lastError:'GAME_NOT_CONNECTED'},stored:{id:'run',serverId:321,status:'failed',error:'SCAN_RUNNING'}},
 {id:'stored-empty-hides-live',scan:{lastError:'SCAN_RUNNING'},stored:{id:'run',serverId:321,status:'completed',error:''}},
 {id:'wrong-stored-uses-live',scan:{lastError:'SCAN_RUNNING'},stored:{id:'old',serverId:321,status:'failed',error:'GAME_NOT_CONNECTED'}},
 {id:'local-start-error-persists',scan:{lastError:'GAME_NOT_CONNECTED'},startError:new Error('SCAN_RUNNING')},
 {id:'local-generic-start-error',startError:new Error('synthetic failure')},
 ]){
 const o=await boot(true,current,language,spec),c=await boot(false,current,language,spec);
 if(spec.startError){for(const [p,original] of [[o,true],[c,false]]){const pending=button(p,'map.startReading').props.onClick();await p.settle();const call=original?p.calls.filter(x=>x.name==='scanStart').at(-1):p.startCall;call.reject(spec.startError);await pending;await p.settle();await p.emit({lastError:'GAME_NOT_CONNECTED'});}}
 assert.deepEqual(scanErrors(c),scanErrors(o),language+'/'+spec.id);
 result.scanCases.push({language,id:spec.id,pass:true});await o.unmount();await c.unmount();
}
// Execute actual timer/effect sequences against the original, not a new modeled policy.
for(const sourceName of ['current','baseline']){
 const o=await boot(true),c=await boot(false,sourceName==='current'?current:baseline);const checkpoints=[],initialCounts={original:o.requests.length,current:c.requests.length};
 const mark=id=>{const value={id,originalRevision:o.getState('rowsRevision'),currentRevision:c.getState('searchRevision'),originalRequests:o.requests.length-initialCounts.original,currentRequests:c.requests.length-initialCounts.current};checkpoints.push(value);if(sourceName==='current')assert.equal(value.currentRevision,value.originalRevision,id);};
 mark('mount');await o.emit({isReading:true});await c.emit({isReading:true});mark('reading-no-immediate-search');
 await o.advance(900);await c.advance(900);await o.emit({readBlocks:1});await c.emit({readBlocks:1});mark('coalesced-at-900');
 await o.advance(100);await c.advance(100);mark('one-trailing-search-at-1000');
 await o.advance(1000);await c.advance(1000);mark('no-periodic-search-without-read-change');
 await o.emit({readBlocks:1});await c.emit({readBlocks:1});await o.advance(1000);await c.advance(1000);mark('same-read-count-no-rearm');
 await o.emit({readBlocks:2});await c.emit({readBlocks:2});await o.advance(1000);await c.advance(1000);mark('read-change-rearms');
 await o.emit({readBlocks:3});await c.emit({readBlocks:3});await o.advance(500);await c.advance(500);await o.emit({isReading:false});await c.emit({isReading:false});mark('completion-immediate-search');
 await o.advance(499);await c.advance(499);mark('completion-cancels-trailing');
 await o.emit({isReading:true});await c.emit({isReading:true});const count=c.requests.length;await o.unmount();await c.unmount();await o.advance(2000);await c.advance(2000);assert.equal(c.requests.length,count);
 if(sourceName==='current')result.refreshCases.push(...checkpoints.map(c=>({...c,pass:true})),{id:'unmount-cancels',pass:true});else result.baselineDefects.push(...checkpoints.filter(c=>c.currentRevision!==c.originalRevision).map(c=>'refresh/'+c.id));
}
// A row refresh in Scheduled Plunder must not query a normal table.
const o=await boot(true),c=await boot(false);await o.clickTab('scheduledPlunder');c.findNodes(n=>n.type==='button'&&n.props.role==='tab'&&nodeText(n).includes(c.t('map.scheduledPlunder')))[0].props.onClick();await c.settle();const counts=[o.requests.length,c.requests.length];await o.emit({isReading:true});await c.emit({isReading:true});await o.advance(1000);await c.advance(1000);assert.deepEqual([o.requests.length,c.requests.length],counts);await o.emit({isReading:false});await c.emit({isReading:false});assert.deepEqual([o.requests.length,c.requests.length],counts);await o.unmount();await c.unmount();result.refreshCases.push({id:'scheduled-boundary',pass:true});
// Stored run is retired immediately when Start is requested; a failed start
// remains the local alert even after the five-second summary acknowledgement.
for(const original of [true,false]){
 const p=await boot(original,current,'en',{stored:{id:'run',serverId:321,status:'failed',error:'MAP_SCAN_REJECTED'},scan:{lastError:'MAP_SCAN_START_FAILED'}});
 const pending=button(p,'map.startReading').props.onClick();await p.settle();
 assert.equal(original?p.getState('scanProgress'):p.getState('options').scanProgress,null);
 const call=original?p.calls.filter(x=>x.name==='scanStart').at(-1):p.startCall;
 call.reject(new Error('MAP_SCAN_REJECTED'));await pending;await p.settle();
 await p.advance(5000);assert.deepEqual(scanErrors(p),[{role:'alert',text:p.t('error.MAP_SCAN_REJECTED')}]);await p.unmount();
}
result.scanCases.push({id:'retire-stored-and-keep-local-error-after-poll',pass:true});
result.status='PASS';result.pageSha256=crypto.createHash('sha256').update(current.replace(/\r\n/g,'\n')).digest('hex');
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_MAP_REFRESH_FEEDBACK_OK export=${result.exportCases.length} scan=${result.scanCases.length} refresh=${result.refreshCases.length} baselineDefects=${result.baselineDefects.length}`);
