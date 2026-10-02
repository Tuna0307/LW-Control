import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createOriginalHarness,optionsReply} from '../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs';
import {createHarness,repo} from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';
const here=path.dirname(fileURLToPath(import.meta.url)), src=path.join(repo,'src/LWBridge.UI-0.3.17/src');
const page=fs.readFileSync(path.join(src,'MapDataPage.jsx'),'utf8');
const baseline=execFileSync('git',['show','315c5a7:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'],{cwd:repo,encoding:'utf8'});
const NOW=Date.UTC(2026,9,3,12,0,0), START=NOW-65000;
const state={serverId:321,scanRunId:'run-1',isReading:false,phase:'idle',selectedTypes:['city'],scanMode:'normal',totalBlocks:0,progressPercent:0,startedAt:0};
const stored={serverId:321,id:'run-1',status:'completed',createdAt:START,updatedAt:NOW-1000,error:null};
const specs=[
 {id:'no-state',absent:true},
 {id:'provided-zero',scan:{}},
 {id:'phase-completed-without-stored',scan:{phase:'completed',progressPercent:100}},
 {id:'publishing-stopped',scan:{phase:'publishing',progressPercent:99.75,startedAt:START,updatedAt:NOW}},
 {id:'publishing-reading',scan:{phase:'publishing',isReading:true,progressPercent:99.75,startedAt:START}},
 {id:'reading-fraction',scan:{isReading:true,phase:'reading',progressPercent:37.75,startedAt:START}},
 {id:'reading-ignores-stored',scan:{isReading:true,startedAt:START},stored:{...stored,createdAt:START-10000}},
 {id:'stored-completed',scan:{progressPercent:100},stored},
 {id:'stored-failed',scan:{startedAt:START-20000},stored:{...stored,status:'failed'}},
 {id:'stored-stopped',stored:{...stored,status:'stopped'}},
 {id:'stored-running-ignored',scan:{startedAt:START},stored:{...stored,status:'running'}},
 {id:'stored-run-id-mismatch',scan:{startedAt:START},stored:{...stored,id:'old'}},
 {id:'stored-server-mismatch',scan:{startedAt:START},stored:{...stored,serverId:322}},
 {id:'strict-stored-server-key',scan:{startedAt:START},stored:{...stored,serverId:'321'}},
 {id:'strict-stored-run-key',scan:{scanRunId:1,startedAt:START},stored:{...stored,id:'1'}},
 {id:'stopped-end-missing',scan:{startedAt:START}},
 {id:'end-before-start',scan:{startedAt:START,updatedAt:START-1000}},
 {id:'cross-day',stored:{...stored,createdAt:NOW-86400000}},
 {id:'second-timestamps',stored:{...stored,createdAt:Math.floor(START/1000),updatedAt:Math.floor(NOW/1000)}},
 {id:'string-timestamps',stored:{...stored,createdAt:String(START),updatedAt:String(NOW)}},
 ...[-1,0,49.99,50,100,200,Infinity,'NaN',null,'37.125'].map((progressPercent,i)=>({id:'progress-'+i,scan:{progressPercent}})),
];
function shape(node){
 if(Array.isArray(node))return node.filter(n=>n!=null && typeof n!=='boolean').map(shape).flat();
 if(node==null || typeof node==='boolean')return [];
 if(typeof node!=='object')return String(node);
 const children=shape(node.props.children);
 return {type:node.type,attrs:Object.fromEntries(['className','max','value','aria-label','dateTime','title'].filter(k=>node.props[k]!==undefined).map(k=>[k,k==='max'?Number(node.props[k]):node.props[k]])),children:Array.isArray(children)?children:[children]};
}
function view(p){return Object.fromEntries(['map-scan-timing','map-scan-summary'].map(css=>[css,shape(p.findNodes(n=>n.props?.className===css)[0])]));}
async function boot(spec,original,translate,source=page,language='en'){
 let current={...state,...spec.scan};
 const reply=optionsReply({scanProgress:spec.stored || null});
 let p;
 if(original){p=await createOriginalHarness({online:false,now:NOW,language,serverId:spec.absent?0:321,scanState:current,initialTab:'city',tabMode:'controlled',translate,stubs:{dataOptions:{mode:'auto',value:reply}}});if(spec.absent)await p.setProps({scanState:undefined,summary:null});}
 else {p=await createHarness(source,'scan-header',{now:NOW,language,serverId:spec.absent?0:321,translate,props:{previewState:'map-city',currentServerId:spec.absent?0:321},dataOptions:reply(321),apiExtensions:()=>({summary:()=>spec.absent?new Promise(()=>{}):Promise.resolve({serverId:current.serverId,counts:{},scanState:current})})});}
 await p.mount();
 p.headerEmit=async(next)=>{current={...current,...next};if(original)await p.emitScanState(next);else {p.liveStatusListener()(current);await p.settle();}};
 return p;
}
const cases=[],baselineMismatches=[];
for(const language of ['en','ja','zh-CN','zh-TW','ko','pt','id','vi','ru']){
 const messages=(await import(pathToFileURL(path.join(src,'locales/'+language+'.js')).href)).default;
 const t=(key,values={})=>(messages[key] || key).replace(/\{(\w+)\}/g,(_,k)=>String(values[k] ?? `{${k}}`));
 for(const spec of specs){
   const o=await boot(spec,true,t,page,language), c=await boot(spec,false,t,page,language), b=language==='en'?await boot(spec,false,t,baseline,language):null;
   assert.deepEqual(view(c),view(o),language+'/'+spec.id);
   assert.equal(c.findNodes(n=>n.props?.className==='map-counters').length,0);
   if(b){if(JSON.stringify(view(b))!==JSON.stringify(view(o)))baselineMismatches.push(spec.id);await b.unmount();}
   cases.push({language,id:spec.id,pass:true});await o.unmount();await c.unmount();
 }
}
// Actual page effects: clock ticks while reading City, stops on completion,
// restarts when reading resumes, and is removed on unmount.
const o=await boot({scan:{isReading:true,startedAt:START}},true,k=>k), c=await boot({scan:{isReading:true,startedAt:START}},false,k=>k);
const clock=[];
for(const phase of ['initial','tick','stop','stopped-tick','resume','resumed-tick']){
 if(phase==='tick'||phase==='stopped-tick'||phase==='resumed-tick'){await o.advance(1000);await c.advance(1000);}
 if(phase==='stop'){await o.headerEmit({isReading:false,updatedAt:NOW+1000});await c.headerEmit({isReading:false,updatedAt:NOW+1000});}
 if(phase==='resume'){await o.headerEmit({isReading:true});await c.headerEmit({isReading:true});}
 assert.deepEqual(view(c),view(o),'clock '+phase);clock.push({phase,pass:true,currentTime:c.getState('currentTime')});
}
const before=c.getState('currentTime');await c.unmount();await c.advance(1000);assert.equal(c.getState('currentTime'),before);await o.unmount();
assert.ok(baselineMismatches.includes('reading-fraction'));assert.ok(baselineMismatches.includes('stored-completed'));assert.ok(baselineMismatches.includes('publishing-stopped'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n')).digest('hex');
const result={comparisons:cases.length,cases,baselineMismatches,clock,unmount:'PASS',pageSha256:sha(path.join(src,'MapDataPage.jsx')),helperSha256:sha(path.join(src,'mapScanPresentation.js'))};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'header-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_MAP_SCAN_HEADER_OK comparisons=${cases.length} baselineMismatches=${baselineMismatches.length} clock=${clock.length} unmount=PASS`);
