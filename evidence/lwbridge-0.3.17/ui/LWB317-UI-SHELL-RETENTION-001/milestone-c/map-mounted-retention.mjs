// Actual React DOM/Activity mount of the production Map page and exact shell boundary.
// Native API responses are deferred/inert. Imported JSX child controls are inert;
// MapDataPage and its inline MapTable/Pagination execute their production bodies.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { routes } from '../../../../../src/LWBridge.UI-0.3.17/src/routes.js';
import { DEFAULT_SCAN_STATE, EMPTY_COUNTS } from '../../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js';
import { DEFAULT_AUTO_SCAN_CONFIG } from '../../../../../src/LWBridge.UI-0.3.17/src/mapAutoConfig.js';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../..');
const ui = path.join(repo, 'src/LWBridge.UI-0.3.17');
const requireUi = createRequire(path.join(ui, 'package.json'));
const domPackage = process.env.LWB317_JSDOM_PACKAGE || 'C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json';
const requireDom = createRequire(domPackage);
const { JSDOM } = requireDom('jsdom');
const { parse } = requireUi('@babel/parser');
const { transformSync } = requireUi('esbuild');
const React = requireUi('react');
const { createRoot } = requireUi('react-dom/client');
const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'http://localhost/' });
globalThis.window = dom.window;
globalThis.document = dom.window.document;
Object.defineProperty(globalThis, 'navigator', { value: dom.window.navigator, configurable: true });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
const errors = [];
const savedError = console.error;
console.error = (...args) => errors.push(args.map(String).join(' '));
const sourcePath = path.join(ui, 'src/MapDataPage.jsx');
const source = fs.readFileSync(sourcePath, 'utf8');
const appSource = fs.readFileSync(path.join(ui, 'src/App.jsx'), 'utf8');
const ast = parse(source, { sourceType: 'module', plugins: ['jsx'] });
const walk = (node, list = []) => { if (!node || typeof node !== 'object') return list; if (node.type) list.push(node); for (const v of Object.values(node)) { if (Array.isArray(v)) v.forEach(c => walk(c, list)); else if (v && typeof v === 'object') walk(v, list); } return list; };
const pageNode = walk(ast).find(n => n.type === 'FunctionDeclaration' && n.id.name === 'MapDataPage');
const stateNames = walk(pageNode).filter(n => n.type === 'VariableDeclarator' && n.id?.type === 'ArrayPattern' && n.init?.callee?.name === 'useState').map(n => n.id.elements[0].name);
const refNames = walk(pageNode).filter(n => n.type === 'VariableDeclarator' && n.init?.callee?.name === 'useRef').map(n => n.id.name);
let capture;
let stateCursor = 0;
let refCursor = 0;
let stateValues = [];
let refValues = [];
const translate = key => key;
const bindings = { h: React.createElement, Fragment: React.Fragment };
for (const node of ast.program.body.filter(n => n.type === 'ImportDeclaration')) {
  const from = node.source.value;
  const module = from === 'react' ? React : from.endsWith('.js') ? await import(pathToFileURL(path.resolve(ui, 'src', from)).href) : null;
  for (const spec of node.specifiers) {
    const imported = spec.imported?.name || 'default';
    bindings[spec.local.name] = module ? module[imported] : imported === 'useI18n' ? () => ({language:'en',t:translate}) : function InertChild() { return null; };
  }
}
bindings.useState = initial => { const pair = React.useState(initial); stateValues[stateCursor++] = pair[0]; return pair; };
bindings.useRef = initial => { const ref = React.useRef(initial); refValues[refCursor++] = ref; return ref; };
const statements = ast.program.body.filter(n => n.type !== 'ImportDeclaration').map(n => n.type === 'ExportNamedDeclaration' ? n.declaration : n).filter(Boolean);
const code = transformSync(statements.map(n => source.slice(n.start,n.end)).join('\n'), {loader:'jsx',jsxFactory:'h',jsxFragment:'Fragment',target:'es2022'}).code;
const RawMap = new Function(...Object.keys(bindings),code+'\nreturn MapDataPage;')(...Object.values(bindings));
function MountedMap(props) { stateCursor=0; refCursor=0; stateValues=[]; refValues=[]; const tree=RawMap(props); capture={state:Object.fromEntries(stateNames.map((n,i)=>[n,stateValues[i]])),refs:Object.fromEntries(refNames.map((n,i)=>[n,refValues[i]]))}; return tree; }
function PageForRoute(props) { return props.routeKey === 'map-data' ? React.createElement(MountedMap, props) : React.createElement('div', {'data-other-route':props.routeKey}); }
const shellAst = parse(appSource,{sourceType:'module',plugins:['jsx']});
const shellNode = shellAst.program.body.find(n => n.type === 'FunctionDeclaration' && n.id.name === 'RetainedPages');
const shellCode = transformSync(appSource.slice(shellNode.start,shellNode.end),{loader:'jsx',jsxFactory:'h',target:'es2022'}).code;
const RetainedPages = new Function('h','Fragment','Activity','routes','PageForRoute',shellCode+'\nreturn RetainedPages;')(React.createElement,React.Fragment,React.Activity,routes,PageForRoute);
const deferred = () => { let resolve,reject; const promise=new Promise((a,b)=>{resolve=a;reject=b;});return {promise,resolve,reject}; };
const scan = (serverId=321,patch={}) => ({...DEFAULT_SCAN_STATE,serverId,selectedTypes:[...DEFAULT_SCAN_STATE.selectedTypes],...patch});
const options = (serverId,count=2) => ({serverId,counts:{...EMPTY_COUNTS,city:count,resource:count},names:{resource:[],monster:[]},alliances:[],dispatchLevels:[],treasureTypes:[],rewardItems:{truck:[],railway:[]},noAllianceCount:0});
const digest = s => crypto.createHash('sha256').update(s).digest('hex');
const originalPath = 'evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/reference/MapDataPanel.pretty.js';
const original = fs.readFileSync(path.join(repo,originalPath),'utf8');
const originalAst = parse(original,{sourceType:'module',plugins:['jsx']});
function originalFact(name,marker,interpretation) {
  const nodes = walk(originalAst).filter(n=>n.type==='CallExpression'&&(n.callee?.property?.name||n.callee?.expressions?.at(-1)?.property?.name)==='useEffect'&&original.slice(n.start,n.end).includes(marker));
  assert.equal(nodes.length,1,`exact original effect ${name}`);const n=nodes[0];const bytes=original.slice(n.start,n.end);
  return {name,source:originalPath,utf8ByteOffset:Buffer.byteLength(original.slice(0,n.start)),utf8ByteLength:Buffer.byteLength(bytes),sha256:digest(bytes),interpretation};
}
const originalFacts = [originalFact('options have no cleanup','ge(R2).then','Options effect returns no cleanup; its current generation may settle and acknowledge counts while Activity is hidden.'),originalFact('Dispatch selection reconnect reset','on({})','Tab-dependent effect resets Dispatch selection when its setup reconnects; retained state does not override source effect behavior.'),originalFact('progress cleanup preserves sentinel','X.current !== null && window.clearTimeout(X.current)','Cleanup clears timeout without clearing its ref. Exact callback parity is separately recorded in lead-map-effect-results.json.')];
const cases = [];
async function session(name, body) {
  const host=document.createElement('div');document.body.append(host);const root=createRoot(host);
  const searches=[],optionRequests=[],countAcks=[],intervals=new Map(),timeouts=new Map();let serial=0;let jobsAdds=0,jobsRemoves=0,childSummaries=0,childScanListeners=0;
  window.setInterval=(fn,delay)=>{intervals.set(++serial,{fn,delay});return serial;};window.clearInterval=id=>intervals.delete(id);
  window.setTimeout=(fn,delay)=>{timeouts.set(++serial,{fn,delay});return serial;};window.clearTimeout=id=>timeouts.delete(id);
  const api={search:(kind,query)=>{const d=deferred();searches.push({kind,query,...d});return d.promise;},dataOptions:serverId=>{const d=deferred();optionRequests.push({serverId,...d});return d.promise;},summary:async()=>{childSummaries++;throw Error('unexpected child summary');},listenScanStatus:()=>{childScanListeners++;return()=>{};},listenPlunderJobsChanged:()=>{jobsAdds++;return()=>{jobsRemoves++;};}};
  let props={mapApi:api,bridgeMode:'native',backendAvailable:true,online:true,currentServerId:321,scanState:scan(),summary:{serverId:321,counts:{...EMPTY_COUNTS,resource:80},scanState:scan()},onState:()=>{},onCounts:(serverId,counts)=>countAcks.push({serverId,counts}),autoScanConfig:DEFAULT_AUTO_SCAN_CONFIG,autoScanRunning:false,onAutoScanConfig:()=>{},onAutoScanRunningChange:()=>{}};
  const render = async (activeRoute='map-data',patch={})=>{props={...props,...patch};await React.act(async()=>{root.render(React.createElement(RetainedPages,{activeRoute,visitedRoutes:new Set(['overview','map-data']),selectedProfileId:'fixture-a',pageProps:props}));});};
  const settle = async fn=>{await React.act(async()=>{fn?.();await Promise.resolve();});};
  const snapshot = stage=>({stage,tab:capture.state.tab,page:capture.state.page,loading:capture.state.loading,total:capture.state.total,queryError:capture.state.queryError,scanServer:capture.state.scanState.serverId,dataServer:capture.state.browseServerId,searchServers:searches.map(r=>r.query.serverId),optionServers:optionRequests.map(r=>r.serverId),jobsActive:jobsAdds-jobsRemoves,intervals:intervals.size,timeouts:timeouts.size,timerRef:capture.refs.scanProgressTimer.current});
  const click = async text=>{const button=[...host.querySelectorAll('button')].find(b=>(b.querySelector('.map-tab-label')?.textContent||b.textContent)===text);assert.ok(button,`button ${text}`);await settle(()=>button.click());};
  await render();
  const result=await body({render,settle,click,snapshot,searches,optionRequests,countAcks,host,props:()=>props,timeouts});
  assert.equal(childSummaries,0);assert.equal(childScanListeners,0);
  await React.act(async()=>root.unmount());host.remove();assert.equal(jobsAdds,jobsRemoves);assert.equal(intervals.size,0);assert.equal(timeouts.size,0);
  cases.push({name,status:'PASS',childSummaries,childScanListeners,...result});
}
try {
  for (const reject of [false,true]) await session(`pending search ${reject?'rejection':'success'} hidden and fresh return`,async h=>{
    await h.settle(()=>h.optionRequests[0].resolve(options(321)));
    const obsolete=h.searches.at(-1);
    await h.render('overview');const before=h.snapshot('hidden-before-settlement');assert.equal(before.jobsActive,0);
    await h.settle(()=>reject?obsolete.reject(Error('obsolete hidden rejection')):obsolete.resolve({rows:[],total:77}));const after=h.snapshot('hidden-after-settlement');
    assert.equal(after.total,before.total);assert.equal(after.loading,before.loading);assert.equal(after.queryError,before.queryError);
    const requestsBefore=h.searches.length;await h.render();assert.equal(h.searches.length,requestsBefore+1);assert.equal(h.snapshot('returned').jobsActive,1);
    await h.settle(()=>h.searches.at(-1).resolve({rows:[],total:88}));assert.equal(capture.state.total,88);assert.equal(capture.state.loading,false);
    return {observations:[before,after,h.snapshot('fresh-return-settled')]};
  });
  await session('pending options may acknowledge while hidden; return refreshes once',async h=>{
    const pending=h.optionRequests[0];await h.render('overview');await h.settle(()=>pending.resolve(options(321,9)));
    assert.equal(h.countAcks.length,1);assert.equal(h.countAcks[0].counts.city,9);assert.equal(capture.state.counts.city,9);
    const hidden=h.snapshot('hidden-options-settled');await h.render();assert.deepEqual(h.optionRequests.map(r=>r.serverId),[321,321]);
    await h.settle(()=>h.optionRequests.at(-1).resolve(options(321,10)));assert.equal(capture.state.counts.city,10);
    return {observations:[hidden,h.snapshot('returned-options-refreshed')],countAcknowledgements:h.countAcks};
  });
  await session('normal resource tab and page survive return; hidden server transition settles current server',async h=>{
    await h.settle(()=>{h.optionRequests[0].resolve(options(321));h.searches[0].resolve({rows:[],total:120});});
    await h.click('map.resource');await h.settle(()=>h.searches.at(-1).resolve({rows:[],total:120}));
    await h.click('map.nextPage');await h.settle(()=>h.searches.at(-1).resolve({rows:[],total:120}));
    assert.equal(capture.state.tab,'resource');assert.equal(capture.state.page,2);
    await h.render('overview');const retained=h.snapshot('hidden-page-two');
    await h.render();assert.equal(capture.state.tab,'resource');assert.equal(capture.state.page,2);assert.equal(h.searches.at(-1).query.page,2);
    const returned=h.snapshot('returned-page-two');
    await h.render('overview');await h.render('overview',{currentServerId:322,scanState:scan(322),summary:{serverId:322,counts:{...EMPTY_COUNTS,resource:11},scanState:scan(322)}});
    const hiddenServer=h.snapshot('parent-server-updated-hidden');const optionsBefore=h.optionRequests.length;await h.render();
    assert.equal(capture.state.scanState.serverId,322);assert.equal(capture.state.browseServerId,322);assert.equal(capture.state.page,1);
    assert.equal(h.optionRequests.at(-1).serverId,322);assert.equal(h.searches.at(-1).query.serverId,322);
    await h.settle(()=>{h.optionRequests.at(-1).resolve(options(322,22));h.searches.at(-1).resolve({rows:[],total:55});});
    const ackBefore=h.countAcks.length;
    await h.settle(()=>h.optionRequests.slice(optionsBefore,-1).forEach(r=>r.resolve(options(r.serverId,99))));
    assert.equal(h.countAcks.length,ackBefore);assert.equal(capture.state.counts.city,22);assert.equal(capture.state.total,55);
    return {observations:[retained,returned,hiddenServer,h.snapshot('current-server-settled')],returnOptions:h.optionRequests.slice(optionsBefore).map(r=>r.serverId),note:'Return may bootstrap retained data server before scan-prop synchronization; generation fencing preserves the newer current-server owner.'};
  });
  await session('reading timeout canceled on hide retains original non-null sentinel',async h=>{
    await h.render('map-data',{scanState:scan(321,{isReading:true,readBlocks:1})});assert.equal(h.timeouts.size,1);const before=h.snapshot('reading-visible');
    await h.render('overview');const hidden=h.snapshot('reading-hidden');assert.equal(h.timeouts.size,0);assert.notEqual(hidden.timerRef,null);
    await h.render();assert.equal(h.timeouts.size,0);const returned=h.snapshot('reading-returned');
    await h.render('map-data',{scanState:scan(321,{isReading:false,readBlocks:2})});assert.equal(capture.refs.scanProgressTimer.current,null);
    await h.render('map-data',{scanState:scan(321,{isReading:true,readBlocks:3})});assert.equal(h.timeouts.size,1);
    return {observations:[before,hidden,returned,h.snapshot('reading-restarted')],originalParity:'Exact original/current effect callback parity recorded in lead-map-effect-results.json; no timer-ref reset was invented.'};
  });
  // Mount the actual parent App, not only the controlled prop supplier used above.
  {
    const host=document.createElement('div');document.body.append(host);const root=createRoot(host);
    const intervals=new Map(),timeouts=new Map();let serial=0;
    window.setInterval=(fn,delay)=>{intervals.set(++serial,{fn,delay});return serial;};window.clearInterval=id=>intervals.delete(id);
    window.setTimeout=(fn,delay)=>{timeouts.set(++serial,{fn,delay});return serial;};window.clearTimeout=id=>timeouts.delete(id);
    window.matchMedia=()=>({matches:false});globalThis.localStorage=window.localStorage;
    let summaryCalls=0,statusCalls=0,proxyCalls=0,statusAdds=0,statusRemoves=0,scanAdds=0,scanRemoves=0,jobsAdds=0,jobsRemoves=0;
    let statusListener,scanListener;let currentScan=scan();let providerStatus={xluaOnline:true,pending:0};
    const bridge={mode:'native',available:true,profileId:'fixture-a',listen:()=>()=>{},invoke:async command=>{
      if(command==='game_root_status')return {valid:true,root:'C:/Fixture/Game'};
      if(command==='game_recovery_status')return {state:'idle'};
      if(command==='local_config_get')return {autoLaunchGame:false,autoReconnect:false};
      throw Error(`unexpected mounted App native binding ${command}`);
    }};
    const api={
      readStatus:async()=>{statusCalls++;return providerStatus;},readProxyStatus:async()=>{proxyCalls++;return {gameRunning:true,repairRequired:false};},
      summary:async()=>{summaryCalls++;return {serverId:currentScan.serverId,counts:{...EMPTY_COUNTS,resource:120},scanState:currentScan};},
      listenStatus:fn=>{statusAdds++;statusListener=fn;return()=>{statusRemoves++;if(statusListener===fn)statusListener=null;};},
      listenScanStatus:fn=>{scanAdds++;scanListener=fn;return()=>{scanRemoves++;if(scanListener===fn)scanListener=null;};},
      dataOptions:async serverId=>options(serverId,120),search:async()=>({rows:[],total:120}),importServerJumpHistory:async()=>[],
      listenPlunderJobsChanged:()=>{jobsAdds++;return()=>{jobsRemoves++;};},
    };
    const parentBindings={h:React.createElement};
    const stableSetLanguage=()=>{};
    for(const node of shellAst.program.body.filter(n=>n.type==='ImportDeclaration')){
      const from=node.source.value;
      const module=from==='react'?React:from.endsWith('.js')?await import(pathToFileURL(path.resolve(ui,'src',from)).href):null;
      for(const spec of node.specifiers){const imported=spec.imported?.name||'default';const local=spec.local.name;
        if(local==='backendBridge')parentBindings[local]=bridge;
        else if(local==='createMapApi')parentBindings[local]=()=>api;
        else if(local==='PageForRoute')parentBindings[local]=PageForRoute;
        else if(local==='useI18n')parentBindings[local]=()=>({language:'en',setLanguage:stableSetLanguage,t:translate});
        else if(local==='LANGUAGES')parentBindings[local]=[{code:'en',name:'English'}];
        else if(from.endsWith('.png'))parentBindings[local]='fixture-dot.png';
        else if(module)parentBindings[local]=module[imported];
        else parentBindings[local]=function InertIcon(){return null;};
      }
    }
    const appStatements=shellAst.program.body.filter(n=>n.type!=='ImportDeclaration').map(n=>n.type==='ExportNamedDeclaration'?n.declaration:n).filter(Boolean);
    const appCode=transformSync(appStatements.map(n=>appSource.slice(n.start,n.end)).join('\n'),{loader:'jsx',jsxFactory:'h',target:'es2022'}).code;
    const MountedApp=new Function(...Object.keys(parentBindings),appCode+'\nreturn App;')(...Object.values(parentBindings));
    const settle=async fn=>{await React.act(async()=>{fn?.();await Promise.resolve();});};
    const clickNav=async text=>{const button=[...host.querySelectorAll('.side-nav button')].find(b=>b.textContent===text);assert.ok(button,`mounted App navigation ${text}`);await settle(()=>button.click());};
    const observed=[];const snapshot=stage=>({stage,summaryCalls,statusCalls,proxyCalls,statusActive:statusAdds-statusRemoves,scanActive:scanAdds-scanRemoves,jobsActive:jobsAdds-jobsRemoves,intervals:intervals.size,mapServer:capture.state.scanState.serverId,mapTab:capture.state.tab,mapPage:capture.state.page,pendingText:host.querySelectorAll('.status-card strong')[1]?.textContent});
    await settle(()=>root.render(React.createElement(MountedApp)));
    assert.equal(statusAdds-statusRemoves,1);assert.equal(scanAdds-scanRemoves,1);assert.equal(summaryCalls,2);
    await clickNav('nav.mapData');assert.equal(jobsAdds-jobsRemoves,1);assert.equal(capture.state.scanState.serverId,321);observed.push(snapshot('Map-visible'));
    await clickNav('nav.overview');assert.equal(jobsAdds-jobsRemoves,0);observed.push(snapshot('Map-hidden'));
    const statusBefore=statusCalls,summaryBefore=summaryCalls;
    currentScan=scan(322);providerStatus={xluaOnline:true,pending:7};
    await settle(()=>{statusListener(providerStatus);scanListener(currentScan);});
    assert.equal(host.querySelectorAll('.status-card strong')[1].textContent,'7');
    await settle(()=>{for(const timer of [...intervals.values()]){assert.equal(timer.delay,5000);timer.fn();}});
    assert.equal(statusCalls,statusBefore+1);assert.equal(summaryCalls,summaryBefore+1);assert.equal(statusAdds,1);assert.equal(scanAdds,1);assert.equal(jobsAdds-jobsRemoves,0);observed.push(snapshot('five-second-parent-work-while-Map-hidden'));
    await clickNav('nav.mapData');assert.equal(jobsAdds-jobsRemoves,1);assert.equal(capture.state.scanState.serverId,322);assert.equal(capture.state.browseServerId,322);assert.equal(statusAdds,1);assert.equal(scanAdds,1);observed.push(snapshot('Map-returned-current-props'));
    await settle(()=>root.unmount());host.remove();assert.equal(statusAdds,statusRemoves);assert.equal(scanAdds,scanRemoves);assert.equal(jobsAdds,jobsRemoves);assert.equal(intervals.size,0);assert.equal(timeouts.size,0);
    cases.push({name:'actual mounted App retains parent five-second polling/status/scan listeners while Map Activity hides',status:'PASS',observations:observed,listenerOwnership:{statusAdds,statusRemoves,scanAdds,scanRemoves,jobsAdds,jobsRemoves},limits:'Actual App body and React hooks with inert bridge/map API bindings; non-Map routes and NavIcon are inert. This proves parent lifetime and current prop delivery, not native provider behavior.'});
  }
} catch (error) { console.error=savedError;throw error; }
finally { console.error=savedError; }
const report={result:'LWB317_SHELL_MAP_MOUNTED_OK',reactVersion:React.version,jsdomVersion:requireDom('jsdom/package.json').version,domDependencyPackage:domPackage,sources:[{path:'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx',sha256:digest(source)},{path:'src/LWBridge.UI-0.3.17/src/App.jsx',sha256:digest(appSource)},{path:originalPath,sha256:digest(original)}],originalFacts,cases,consoleErrors:errors,limits:'Actual React DOM Activity, exact RetainedPages and production MapDataPage hooks/effects. JSX imports are inert child components; inline MapTable/Pagination remain actual. Translation is stable key identity. First five cases use a controlled prop supplier; the sixth mounts the actual App body with inert bridge/map API bindings. Deferred local APIs and controlled timers; no browser pixels, native function, gameplay or protected reference runtime. Exact original request ordering is not mounted-proven. Original effect quirks are pinned above and timer callbacks compared separately by lead-map-effect.mjs.'};
assert.deepEqual(errors,[],'unexpected React console errors');
fs.writeFileSync(path.join(here,'map-mounted-retention-results.json'),JSON.stringify(report,null,2)+'\n');
console.log(`LWB317_SHELL_MAP_MOUNTED_OK cases=${cases.length}`);
