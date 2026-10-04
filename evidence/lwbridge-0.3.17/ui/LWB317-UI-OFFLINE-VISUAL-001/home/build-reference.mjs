import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=require('@babel/parser'),{transformSync}=require('esbuild'),React=require('react'),{renderToStaticMarkup}=require('react-dom/server');
const domRequire=createRequire('C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json'),{JSDOM}=domRequire('jsdom');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const originalPath='evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js';
const source=fs.readFileSync(path.join(repo,originalPath),'utf8');
assert.equal(hash(source),'44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6');
assert.equal(hash(fs.readFileSync('C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe')),'4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783');
const ast=parse(source,{sourceType:'module'}),names=['Ir','Lr','Kr','zn','Bn','qr'];
const slices=names.map(name=>{const n=ast.program.body.find(n=>n.type==='FunctionDeclaration'&&n.id.name===name);assert.ok(n,name);return{name,utf8ByteOffset:Buffer.byteLength(source.slice(0,n.start)),byteLength:Buffer.byteLength(source.slice(n.start,n.end)),sha256:hash(source.slice(n.start,n.end)),text:source.slice(n.start,n.end)};});
const files=['HomePage.jsx','sharedPageUI.jsx'],currentSources=files.map(n=>fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src',n),'utf8').replaceAll('\r\n','\n'));
const currentFunctions=currentSources.flatMap(s=>parse(s,{sourceType:'module',plugins:['jsx']}).program.body.map(n=>n.declaration||n).filter(n=>n.type==='FunctionDeclaration').map(n=>s.slice(n.start,n.end)));
const currentCode=transformSync(currentFunctions.join('\n'),{loader:'jsx',jsxFactory:'React.createElement'}).code;
const cssPath='src/LWBridge.UI-0.3.17/src/reference.css',css=fs.readFileSync(path.join(repo,cssPath),'utf8');
const recoveredCss=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'),'utf8');
assert.equal(css.replaceAll('\r\n','\n').trim(),recoveredCss.replaceAll('\r\n','\n').trim(),'same exact reference CSS');
const base={rootResolved:true,gameRootStatus:{valid:true,root:'C:\\Games\\Last War-Survival Game'},proxyStatus:{gameRunning:false,repairRequired:false},online:false,gameRecoveryStatus:{state:'idle'},autoLaunchGame:false,autoReconnect:false,busy:'',proxyBusy:false,gameLaunchBusy:false,gameRootError:'',gameActionError:'',production:true};
const cases=[
 ['checking',{rootResolved:false,gameRootStatus:null,proxyStatus:null}],
 ['missing-busy',{gameRootStatus:{valid:false},busy:'gameRoot'}],
 ['missing-error-busy',{gameRootStatus:{valid:false},busy:'gameRoot',gameRootError:'INVALID_GAME_ROOT',gameActionError:'GAME_XLUA_ABI_UNSUPPORTED'}],
 ['launching',{gameLaunchBusy:true}],
 ['proxy-busy-stopped',{proxyBusy:true}],
 ['proxy-busy-running',{proxyBusy:true,proxyStatus:{gameRunning:true}}],
 ['proxy-busy-repair',{proxyBusy:true,proxyStatus:{gameRunning:true,repairRequired:true}}],
 ['busy-overlap',{proxyBusy:true,gameLaunchBusy:true,proxyStatus:{gameRunning:true},autoLaunchGame:true,autoReconnect:true}],
 ['recovery-failed-busy',{proxyBusy:true,gameRecoveryStatus:{state:'failed',error:'QA_UNKNOWN'}}],
 ...['waiting','updating','repairing','launching','verifying','maintenance'].map(state=>[`recovery-${state}-busy`,{proxyBusy:true,proxyStatus:{gameRunning:true},gameRecoveryStatus:{state}}]),
 ['stopped',{}],['running-connected',{online:true,proxyStatus:{gameRunning:true},autoLaunchGame:true,autoReconnect:true}],
 ['repair',{proxyStatus:{gameRunning:true,repairRequired:true}}],
 ['preference-launch-saving',{busy:'autoLaunchGame',proxyBusy:true}],
 ['preference-reconnect-saving',{busy:'autoReconnect',proxyBusy:true}],
 ['unavailable',{production:false,proxyBusy:true}],
];
const htmlDir=path.join(here,'generated');fs.mkdirSync(htmlDir,{recursive:true});
const records=[];let matched=0;
function describe(root){return [root,...root.querySelectorAll('*')].map(e=>({tag:e.tagName,attrs:[...e.attributes].filter(a=>a.name!=='type'||e.tagName!=='BUTTON').sort((a,b)=>a.name.localeCompare(b.name)).map(a=>[a.name,a.value]),text:[...e.childNodes].filter(n=>n.nodeType===3).map(n=>n.textContent).join('')}));}
for(const language of ['en','ja']){
 const {default:catalog}=await import(pathToFileURL(path.join(repo,`src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
 const t=(key,values={})=>(catalog[key]||key).replace(/\{(\w+)\}/g,(m,k)=>String(values[k]??m));
 const Original=new Function('M','De',slices.map(s=>s.text).join('\n')+'\nreturn qr;')(require('react/jsx-runtime'),()=>({t}));
 const Current=new Function('React','useI18n','RECOVERY_ACTIVE_STATES',currentCode+'\nreturn HomePage;')(React,()=>({t}),new Set(['waiting','updating','repairing','launching','verifying','maintenance']));
 for(const [name,patch] of cases){
  const input={...base,...patch};const originalProps={...input,gameRootBusy:input.busy==='gameRoot'};
  const expected=renderToStaticMarkup(React.createElement(Original,originalProps)),actual=renderToStaticMarkup(React.createElement(Current,{homeState:input}));
  const ed=new JSDOM(expected),ad=new JSDOM(actual),er=ed.window.document.querySelector('section'),ar=ad.window.document.querySelector('section');
  const e=describe(er),a=describe(ar);const differences=[];
  assert.equal(a.length,e.length,`${language}/${name}: element count`);
  for(let i=0;i<a.length;i++)if(JSON.stringify(a[i])!==JSON.stringify(e[i]))differences.push({element:i,expected:e[i],actual:a[i]});
  // Every deviation is recorded; no differences are normalized away except the
  // clone's explicit button type. Missing providers and save-busy differences
  // must be assessed separately. We never turn them into a full visual PASS.
  const same=differences.length===0;if(same)matched++;
  for(const theme of ['light','dark'])for(const [side,markup] of [['original',expected],['current',actual]]){
   const filename=`${language}-${theme}-${name}-${side}.html`;
   fs.writeFileSync(path.join(htmlDir,filename),`<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Home ${side} ${name}</title><style>${css}</style></head><body><main class="app-shell"><div class="app-layout single-profile"><nav class="nav-rail"></nav><section class="main-view">${markup}</section></div></main></body></html>`);
  }
  records.push({language,name,input,sameStructureExceptButtonType:same,differences,expectedHtmlSha256:hash(expected),actualHtmlSha256:hash(actual)});
  ed.window.close();ad.window.close();
 }
}
const report={marker:'LWB317_HOME_OFFLINE_REFERENCE_BUILT',reference:{path:originalPath,sha256:hash(source),slices},current:files.map((name,i)=>({path:`src/LWBridge.UI-0.3.17/src/${name}`,sha256:hash(currentSources[i])})),css:{path:cssPath,sha256:hash(css)},cases:records.length,matched,records,limits:'Static actual original/current component rendering with inert callbacks and canonical catalogs. Same isolated shell container and recovered CSS; empty navigation is a fixture, not original shell proof. Button type is the only structural normalization. No native/game actions, mounted effect or protected-original/runtime pixel proof.'};
fs.writeFileSync(path.join(here,'render-results.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({marker:report.marker,cases:report.cases,matched,differences:records.filter(r=>r.differences.length).map(r=>({language:r.language,name:r.name,elements:r.differences.length}))}));
