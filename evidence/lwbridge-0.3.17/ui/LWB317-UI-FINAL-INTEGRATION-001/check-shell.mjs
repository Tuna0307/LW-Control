import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const ui = path.join(repo, 'src/LWBridge.UI-0.3.17');
const requireUi = createRequire(path.join(ui, 'package.json'));
const { parse } = requireUi('@babel/parser');
const { transformSync } = requireUi('esbuild');
const React = requireUi('react');
const M = requireUi('react/jsx-runtime');
const { renderToStaticMarkup } = requireUi('react-dom/server');
const assetPath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js';
const asset = fs.readFileSync(path.join(repo, assetPath));
const text = asset.toString('utf8');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
assert.equal(hash(asset), '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
const all = parse(text, {sourceType:'module'});
const nodes=[];
function walk(node) {if(!node||typeof node!=='object')return;if(node.type)nodes.push(node);
  for(const value of Object.values(node))Array.isArray(value)?value.forEach(walk):value&&typeof value==='object'&&walk(value);}
walk(all);
const functionAt = (name, byte) => {
  const node=nodes.find(n=>n.type==='FunctionDeclaration'&&n.id?.name===name&&Buffer.byteLength(text.slice(0,n.start))===byte);
  assert.ok(node, name+' locator');return node;
};
const slice = node => text.slice(node.start,node.end);
const locate = node => ({byteOffset:Buffer.byteLength(text.slice(0,node.start)),byteLength:Buffer.byteLength(slice(node)),sha256:hash(slice(node))});
const themeNode=functionAt('Et',364550), headerNode=functionAt('si',354710), errorNode=functionAt('Oe',199047),localeNode=functionAt('Ee',198167);
const headerNodes=[];
function collect(node){if(!node||typeof node!=='object')return;if(node.type)headerNodes.push(node);
for(const value of Object.values(node))Array.isArray(value)?value.forEach(collect):value&&typeof value==='object'&&collect(value);}
collect(headerNode);
const literal=node=>node?.value??(node?.type==='TemplateLiteral'&&node.expressions.length===0?node.quasis[0].value.cooked:undefined);
const versionExpression=headerNodes.find(n=>n.type==='CallExpression'&&literal(n.arguments?.[0])==='div'
  &&n.arguments?.[1]?.properties?.some(p=>p.key?.name==='className'&&literal(p.value)==='top-version-row'));
assert.ok(versionExpression);
let t;
const OriginalVersion = new Function('M','v','_','x','S','C','return '+slice(versionExpression)+';');
const OriginalError = new Function('M','b','De',slice(errorNode)+';return Oe;')(M,
  {useSyncExternalStore:(_subscribe,get)=>get()},()=>({t:(...args)=>t(...args)}));
const currentSource=fs.readFileSync(path.join(ui,'src/ShellPresentation.jsx'),'utf8');
const currentAst=parse(currentSource,{sourceType:'module',plugins:['jsx']});
const currentBody=currentAst.program.body.filter(n=>n.type!=='ImportDeclaration').map(n=>n.type==='ExportNamedDeclaration'?n.declaration:n).map(n=>currentSource.slice(n.start,n.end)).join('\n');
const code=transformSync(currentBody,{loader:'jsx',jsxFactory:'h',target:'es2022'}).code;
const current=new Function('h','useI18n','useSyncExternalStore',code+';return {TopVersion,ShellConfigSaveError};')(
 React.createElement,()=>({t:(...args)=>t(...args)}),(_subscribe,get)=>get());
const results=[];
const pass=(name,cases=1)=>results.push({name,status:'PASS',cases});
const markup=node=>renderToStaticMarkup(node);
let versionCount=0,errorCount=0;
for(const language of ['en','zh-CN','zh-TW','ja','ko','vi','id','ru','pt']){
 const catalog=(await import(pathToFileURL(path.join(ui,'src/locales/'+language+'.js')))).default;
 t=(key,values={})=>(catalog[key]||key).replace(/\{(\w+)\}/g,(match,key)=>String(values[key]??match));
 for(const phase of ['idle','checking','upToDate','available','downloading','opening','error'])
 for(const latestVersion of [null,'0.3.17','0.3.18',''])
 for(const progress of [null,0,42,100]){
  const v={phase,currentVersion:'0.3.17',latestVersion,progress};
  const visible=phase==='available'||phase==='downloading'||phase==='opening'||phase==='error'&&latestVersion!==null&&latestVersion!==v.currentVersion;
  const original=OriginalVersion(M,v,t,visible,phase==='downloading'||phase==='opening',async()=>{});
  assert.equal(markup(current.TopVersion({status:v,onDownload:async()=>{}})),markup(original));versionCount++;
  const fenced=current.TopVersion({status:v});
  if(visible)assert.ok(fenced.props.children[1].props.disabled,'unavailable updater action fenced');
 }
 for(const saving of [false,true])for(const error of [null,new Error('controlled')])for(const disabled of [false,true])for(const label of ['',t('automation.autoReconnect.title')]){
  let called='';const state={getSnapshot:()=>({saving,error}),subscribe:()=>()=>{},
   flush:async()=>{called='retry';},refresh:async force=>{called='discard:'+force;}};
  const original=OriginalError({state,label,disabled});
  const actual=current.ShellConfigSaveError({state,label,disabled});assert.equal(markup(actual),markup(original));errorCount++;
  if(error){const buttons=actual.props.children.filter(n=>n?.type==='button');await buttons[0].props.onClick();assert.equal(called,'retry');await buttons[1].props.onClick();assert.equal(called,'discard:true');}
 }
}
pass('exact original/current header version and conditional updater',versionCount);
pass('exact original/current shared-save error render and Retry/Discard',errorCount);

const {toggleShellTheme}=await import(pathToFileURL(path.join(ui,'src/shellTheme.js')));
function environment(reduced,view){const log=[];let now=0;const timers=[];
 const root={dataset:{},classList:{add:x=>log.push('add:'+x),remove:x=>log.push('remove:'+x)}};
 const document={documentElement:root};
 if(view)document.startViewTransition=fn=>{log.push('view:start');fn();log.push('view:end');};
 const window={matchMedia:query=>{log.push('media:'+query);return{matches:reduced};},setTimeout:(fn,ms)=>{timers.push({fn,ms});log.push('timer:'+ms);}};
 const storage={setItem:(key,value)=>log.push('storage:'+key+':'+value)};
 const commit=x=>log.push('commit:'+x),flushSync=fn=>{log.push('flush');fn();};
 return{log,root,document,window,storage,commit,flushSync,timers};}
let themeCount=0;
for(const theme of ['light','dark'])for(const reduced of [false,true])for(const view of [false,true]){
 const original=environment(reduced,view),actual=environment(reduced,view);
 const fn=new Function('Ue','document','window','vi','yi','Re','ke','We',slice(themeNode)+';return Et;')(
  theme,original.document,original.window,(next,root)=>{root.dataset.theme=next;},next=>original.storage.setItem('lwbridge.theme',next),async()=>{}, {flushSync:original.flushSync},original.commit);
 fn();toggleShellTheme(theme,actual.commit,actual.document,actual.window,actual.storage,actual.flushSync);
 assert.deepEqual(actual.log,original.log);assert.deepEqual(actual.root.dataset,original.root.dataset);
 assert.equal(actual.timers.length,original.timers.length);for(const timer of actual.timers)timer.fn();for(const timer of original.timers)timer.fn();assert.deepEqual(actual.log,original.log);themeCount++;
}
pass('exact original/current reduced-motion/view/fallback theme lifecycle',themeCount);

// Execute actual original and current provider with controlled catalog loaders.
// We deliberately fail after a prior catalog exists and inspect retained state.
const providerSource=fs.readFileSync(path.join(ui,'src/i18n.jsx'),'utf8');
const providerAst=parse(providerSource,{sourceType:'module',plugins:['jsx']});
const providerNode=providerAst.program.body.find(n=>n.type==='ExportNamedDeclaration'&&n.declaration?.id?.name==='I18nProvider').declaration;
async function providerFailure(original,obsolete=false){
 let state={language:'ja',messages:{}},request={current:0},callback,logs=[];let reject;
 const promise=new Promise((_resolve,fail)=>reject=fail);
 const hooks={useState:()=>[state,value=>{state=value;}],useRef:()=>request,useCallback:fn=>{callback=fn;return fn;},useEffect:()=>{},useMemo:fn=>fn()};
 const logger={error:(...args)=>logs.push(args[0])};
 if(original){new Function('b','M','Se','Te','we','console',slice(localeNode)+';return Ee;')(hooks,M,{ja:()=>promise},()=> 'ja',{Provider:'div'},logger)({children:null});}
 else{
  const providerCode=transformSync(providerSource.slice(providerNode.start,providerNode.end),{loader:'jsx',jsxFactory:'h'}).code;
  new Function('useState','useRef','useCallback','useEffect','useMemo','LOADERS','console','I18nContext','ENGLISH_KEY_BY_VALUE','h',providerCode+';return I18nProvider;')(
   hooks.useState,hooks.useRef,hooks.useCallback,hooks.useEffect,hooks.useMemo,{ja:()=>promise},logger,{Provider:'div'},new Map(),React.createElement)({children:null});
 }
 callback('ja');if(obsolete)request.current++;reject(new Error('controlled'));await Promise.resolve();await Promise.resolve();await Promise.resolve();
 return{state,logs};
}
for(const obsolete of [false,true])assert.deepEqual(await providerFailure(false,obsolete),await providerFailure(true,obsolete));
pass('current/obsolete locale load rejection retains prior catalog and log predicate',2);
const app=fs.readFileSync(path.join(ui,'src/App.jsx'),'utf8');
const baseline=execFileSync('git',['show','147e5cf:src/LWBridge.UI-0.3.17/src/App.jsx'],{cwd:repo,encoding:'utf8'});
assert.ok(!baseline.includes('status-online'+String.fromCharCode(36)+'{online'),'baseline connected class omission');
assert.ok(app.includes('status-online'+String.fromCharCode(36)+'{online ? " online" : ""}'));
pass('connected CSS class source-backed correction');
const report={result:'LWB317_FINAL_SHELL_OK',results,original:{asset:assetPath,sha256:hash(asset),locators:{theme:locate(themeNode),header:locate(headerNode),versionExpression:locate(versionExpression),sharedError:locate(errorNode),locale:locate(localeNode)}},
 current:{appSha256:hash(app),presentationSha256:hash(currentSource),i18nSha256:hash(providerSource)},
 limits:'Actual source expressions/functions with inert providers/hooks; native updater actions fenced. Provider state producers and original runtime pixels are separate. Tests use existing dependency versions and do not call native/game/network operations.'};
fs.writeFileSync(path.join(here,'shell-results.json'),JSON.stringify(report,null,2)+'\n');
console.log('LWB317_FINAL_SHELL_OK '+results.map(r=>r.cases).join('/'));
