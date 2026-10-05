import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
const repo=process.cwd();
const here=path.dirname(fileURLToPath(import.meta.url));
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const parser=require('@babel/parser');
const source=fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/App.jsx'),'utf8');
const original=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'),'utf8');
const {unwrapProfileEvent}=await import(pathToFileURL(path.join(repo,'src/LWBridge.UI-0.3.17/src/mapBackend.js')).href);
function nodes(s,jsx){const out=[];function walk(n){if(!n||typeof n!=='object')return;if(n.type)out.push(n);for(const x of Object.values(n))Array.isArray(x)?x.forEach(walk):walk(x);}walk(parser.parse(s,{sourceType:'module',plugins:jsx?['jsx']:[]}));return out;}
const currentNodes=nodes(source,true), originalNodes=nodes(original,false);
function evaluate(s,n,env,prefix=''){return new Function(...Object.keys(env),`${prefix}return (${s.slice(n.start,n.end)});`)(...Object.values(env));}
function deferred(){let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b;});return{promise,resolve,reject};}
const settle=()=>new Promise(r=>setTimeout(r,0));
const readNode=currentNodes.find(n=>n.type==='VariableDeclarator'&&n.id?.name==='readStatusSnapshot').init.arguments[0];
const pollNode=currentNodes.find(n=>n.type==='CallExpression'&&n.callee?.name==='useEffect'&&source.slice(n.start,n.end).includes('const pollStatus = async () =>')).arguments[0];
const recoveryNode=currentNodes.find(n=>n.type==='CallExpression'&&n.callee?.name==='useEffect'&&source.slice(n.start,n.end).includes('game_recovery_status')).arguments[0];
const originalPe=originalNodes.find(n=>n.type==='FunctionDeclaration'&&n.id?.name==='Pe');
const assertions=[];
function check(id,actual,expected){assert.deepEqual(actual,expected,id);assertions.push({id,actual});}
function poll(){const requests=[],writes=[];let tick,clear=0,unlisten=0;const profile={current:'A'};const refs=Object.fromEntries(['reconnectStatusGeneration','autoLaunchSaveRevisionRef','autoLaunchNativeCommitEpochRef','autoLaunchConfigPollGenerationRef','autoLaunchCommittedRef'].map(k=>[k,{current:0}]));const bridge={available:true,invoke:()=>Promise.resolve({autoLaunchGame:false})};const api={readStatus:()=>Promise.resolve({xluaOnline:true}),readProxyStatus:()=>{const d=deferred();requests.push(d);return d.promise;},listenStatus:()=>()=>unlisten++,listenScanStatus:()=>()=>unlisten++};const read=evaluate(source,readNode,{backendBridge:bridge,mapApi:api,...refs,acknowledgeRuntimeStatus:v=>writes.push(['status',v]),setProxyStatus:v=>writes.push(['proxy',v]),setConnectionError:v=>writes.push(['error',v])});const stop=evaluate(source,pollNode,{backendBridge:bridge,mapApi:api,selectedProfileId:'A',selectedProfileIdRef:profile,acknowledgeRuntimeStatus(){},acknowledgeMapScan(){},readStatusSnapshot:read,window:{setInterval:(cb,ms)=>{assert.equal(ms,5000);tick=cb;return 9;},clearInterval:id=>{assert.equal(id,9);clear++;}}})();return{requests,writes,profile,tick:()=>tick(),stop,cleanup:()=>[clear,unlisten]};}
const retry=poll();retry.tick();check('initial and overlapping timer produce one request',retry.requests.length,1);retry.requests[0].reject(new Error('CURRENT_ERROR'));await settle();check('current failure is visible',retry.writes.at(-1),['error','CURRENT_ERROR']);retry.tick();check('finally releases failed poll',retry.requests.length,2);retry.requests[1].resolve({gameRunning:false});await settle();check('later success clears current error',retry.writes.at(-1),['error','']);retry.stop();check('cleanup clears one timer and both listeners',retry.cleanup(),[1,2]);
const replaced=poll();replaced.profile.current='B';replaced.requests[0].reject(new Error('A_STALE_ERROR'));await settle();check('replaced profile rejects failure writes',replaced.writes,[]);replaced.stop();
const closed=poll();closed.stop();closed.requests[0].resolve({gameRunning:true});await settle();check('unmounted success rejects every write',closed.writes,[]);
for(const mode of ['same','other','raw']){let originalListener,currentListener;const sourceWrites=[],currentWrites=[];const payload={state:'ready',mode};const event=mode==='raw'?payload:{profileId:mode==='same'?'A':'B',payload};evaluate(original,originalPe,{Ne:()=>true,A:()=> 'A',Me:(_n,cb)=>{originalListener=cb;return Promise.resolve(()=>{});}})('bridge://game-recovery',v=>sourceWrites.push(v));let initial=deferred();const ref={current:'A'};const stop=evaluate(source,recoveryNode,{backendBridge:{available:true,listen:(_n,cb)=>{currentListener=cb;return()=>{};},invoke:()=>initial.promise},selectedProfileId:'A',selectedProfileIdRef:ref,setGameRecoveryStatus:v=>currentWrites.push(v),unwrapProfileEvent})();originalListener({payload:event});currentListener(event);check(`original/current ${mode} recovery event`,currentWrites,sourceWrites);ref.current='B';initial.resolve({state:'late-initial'});await settle();check(`${mode} old initial status retired`,currentWrites,sourceWrites);stop();}
const locator=n=>({line:source.slice(0,n.start).split('\n').length,utf8Byte:Buffer.byteLength(source.slice(0,n.start)),length:Buffer.byteLength(source.slice(n.start,n.end))});
const result={verdict:'ACCEPT',marker:'LEAD_SHELL_AFK_INDEPENDENT_SEMANTICS_OK',assertions,inputs:{appSha256:crypto.createHash('sha256').update(source).digest('hex'),originalSha256:crypto.createHash('sha256').update(original).digest('hex')},locators:{readStatusSnapshot:locator(readNode),periodicEffect:locator(pollNode),recoveryEffect:locator(recoveryNode),originalPe:{utf8Byte:Buffer.byteLength(original.slice(0,originalPe.start)),length:Buffer.byteLength(original.slice(originalPe.start,originalPe.end))}}};
fs.writeFileSync(path.join(here,'poll-recovery-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({marker:result.marker,assertions:assertions.length}));
