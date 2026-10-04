import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../..');
const src = path.join(repo, 'src/LWBridge.UI-0.3.17/src');
const requireUi = createRequire(path.join(src, '../package.json'));
const requireDom = createRequire('C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json');
const {JSDOM} = requireDom('jsdom');
const {build} = requireUi('esbuild');
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const trackedSources = ['App.jsx','Pages.jsx','HotkeyPages.jsx','MapRoutePage.jsx','shellTheme.js'];
const sourceHashesBefore = Object.fromEntries(trackedSources.map(name=>[name,sha(fs.readFileSync(path.join(src,name)))]));
const deferred = () => {let resolve, reject; const promise = new Promise((a,b) => {resolve=a;reject=b;}); return {promise,resolve,reject};};
const observations = [];
let React;
let createRoot;
async function flush(action) {await React.act(async () => {if(action) await action(); for(let i=0;i<8;i++) await Promise.resolve();});}

async function boot({search='?view=overview', native=false, reduced=true, viewTransition=false}={}) {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {url:`http://127.0.0.1/${search}`});
  for(const key of ['window','document','HTMLElement','HTMLDialogElement','Event','MouseEvent','KeyboardEvent','localStorage','Node','MutationObserver']) globalThis[key]=key==='window'?dom.window:dom.window[key];
  Object.defineProperty(globalThis,'navigator',{configurable:true,value:dom.window.navigator});
  globalThis.IS_REACT_ACT_ENVIRONMENT=true;
  globalThis.fetch=()=>{throw Error('Network forbidden in route loading proof');};
  window.matchMedia=()=>({matches:reduced});
  localStorage.setItem('lwbridge.language','en');
  const intervals=new Map(), timeouts=new Map();let serial=0;
  window.setInterval=(fn,delay)=>{intervals.set(++serial,{fn,delay});return serial;};
  window.clearInterval=id=>intervals.delete(id);
  window.setTimeout=(fn,delay)=>{timeouts.set(++serial,{fn,delay});return serial;};
  window.clearTimeout=id=>timeouts.delete(id);
  const gates=new Map(), events=[], summaries=[];
  const gate=name=>{if(!gates.has(name))gates.set(name,deferred());return gates.get(name);};
  const invoke=(command,payload)=>{
    if(command==='map_summary'){const request=deferred();summaries.push(request);events.push({type:'summary',active:document.querySelector('.side-nav [aria-current="page"] .nav-label')?.textContent||null});return request.promise;}
    if(command==='get_status')return Promise.resolve({xluaOnline:false,pending:0});
    if(command==='proxy_status')return Promise.resolve({gameRunning:false});
    if(command==='game_root_status')return Promise.resolve({valid:false});
    if(command==='game_recovery_status')return Promise.resolve({state:'idle'});
    if(command==='server_jump_history_import')return Promise.resolve([]);
    if(command==='game_asset_image')throw Error('Native image request forbidden');
    if(command==='map_search')return Promise.resolve({rows:[],total:0});
    if(command==='map_data_options')return Promise.resolve({serverId:321,counts:{}});
    if(command==='update_status')return Promise.resolve({phase:'idle',currentVersion:'',latestVersion:null});
    if(command==='local_config_get')return Promise.resolve({autoLaunchGame:false,autoReconnect:false});
    throw Error(`Unexpected bridge command ${command}`);
  };
  const bridge={mode:native?'native':'preview',available:native,profileId:'profile-a',invoke,listen:()=>()=>{},invokeProfileScoped:invoke};
  globalThis.__routeProof={bridge,wait(name){events.push({type:'load',module:name,active:document.querySelector('.side-nav [aria-current="page"] .nav-label')?.textContent||null});return gate(name).promise;}};
  const transitions=[];
  if(viewTransition) document.startViewTransition=callback=>{transitions.push(document.documentElement.dataset.theme);callback();return {finished:Promise.resolve()};};
  React=requireUi('react');({createRoot}=requireUi('react-dom/client'));
  const bundle=await build({stdin:{contents:'export {App,RetainedPages} from "./App.jsx";export {I18nProvider} from "./i18n.jsx";export {PageForRoute,preloadRoute} from "./Pages.jsx";',resolveDir:src,loader:'jsx'},bundle:true,write:false,platform:'browser',format:'cjs',jsx:'automatic',external:['react','react/*','react-dom','react-dom/*'],loader:{'.png':'dataurl'},logLevel:'silent',plugins:[{
    name:'inert-transport-and-deferred-production-loaders',setup(build){
      build.onLoad({filter:/backendBridge\.js$/},()=>({contents:'export const backendBridge=globalThis.__routeProof.bridge;',loader:'js'}));
      build.onLoad({filter:/[/\\]App\.jsx$/},args=>({contents:fs.readFileSync(args.path,'utf8')+'\nexport {RetainedPages};',loader:'jsx',resolveDir:src}));
      build.onLoad({filter:/[/\\]Pages\.jsx$/},args=>({contents:fs.readFileSync(args.path,'utf8').replace(/import\(("\.\/[^"\n]+\.jsx")\)/g,(_,name)=>`globalThis.__routeProof.wait(${name}).then(()=>import(${name}))`),loader:'jsx',resolveDir:src}));
    }
  }]});
  const module={exports:{}};new Function('require','module','exports',bundle.outputFiles[0].text)(requireUi,module,module.exports);
  const production=module.exports, container=document.getElementById('root'), root=createRoot(container);
  const h=React.createElement;
  const renderApp=()=>flush(()=>root.render(h(production.I18nProvider,null,h(production.App))));
  const renderRetained=(route,visited,profile='profile-a')=>flush(()=>root.render(h(production.I18nProvider,null,h(React.Suspense,{fallback:h('div',{className:'panel','data-proof-fallback':true},'processing')},h(production.RetainedPages,{activeRoute:route,visitedRoutes:new Set(visited),selectedProfileId:profile,pageProps:{previewState:'mini-games-active',bridgeMode:'preview',backendAvailable:false,online:false}})))));
  const click=element=>{assert.ok(element,'click target');return flush(()=>element.dispatchEvent(new MouseEvent('click',{bubbles:true})));};
  const nav=label=>[...container.querySelectorAll('.side-nav button')].find(node=>node.querySelector('.nav-label')?.textContent===label);
  const settle=name=>flush(()=>gate(name).resolve());
  const stop=async()=>{await flush(()=>root.unmount());assert.equal(intervals.size,0,'interval cleanup');dom.window.close();};
  return {production,container,root,events,summaries,intervals,timeouts,transitions,gate,renderApp,renderRetained,click,nav,settle,stop};
}

// Actual initial App render suspends on its real lazy Hotkey module and shows the
// production Suspense fallback until the transport-only import gate resolves.
{
  const p=await boot({search:'?view=hotkeys&previewState=hotkeys-connected'});
  await p.renderApp();
  assert.ok(p.container.querySelector('.main-view .panel .muted'),'initial source-shaped fallback');
  assert.equal(p.container.querySelector('.hotkey-panel'),null);
  assert.ok(p.events.some(e=>e.module==='./HotkeyPages.jsx'));
  await p.settle('./HotkeyPages.jsx');
  assert.ok(p.container.querySelector('.hotkey-panel'));
  observations.push({id:'initial-suspend',status:'PASS',events:p.events});
  await p.stop();
}

// Actual native-mode App is bound to an inert bridge. Its summary request is
// deliberately unresolved while the real Map lazy module is loading.
{
  const p=await boot({native:true});await p.renderApp();
  assert.equal(p.summaries.length,1,'profile bootstrap');
  await flush(()=>p.summaries[0].resolve({serverId:321,counts:{},scanState:{serverId:321,isReading:false}}));
  const home=p.container.querySelector('.home-panel')||p.container.querySelector('.main-view .panel');assert.ok(home);
  p.events.length=0;await p.click(p.nav('Map Data'));
  assert.equal(p.summaries.length,2,'Map entry summary starts');
  assert.equal(p.events[0].type,'summary');assert.equal(p.events[1].type,'load');
  assert.equal(p.events[0].active,'Home');assert.equal(p.events[1].active,'Home');
  assert.equal(p.container.querySelector('.side-nav [aria-current="page"] .nav-label')?.textContent,'Home','pending transition retains old route');
  assert.equal(home.style.display,'','old content stays visible');
  assert.equal(p.container.querySelector('.map-panel'),null);
  await p.settle('./MapRoutePage.jsx');
  assert.ok(p.container.querySelector('.map-panel'),'Map becomes visible before summary acknowledgement');
  assert.equal(p.container.querySelector('.side-nav [aria-current="page"] .nav-label')?.textContent,'Map Data');
  const before=p.events.length;await p.click(p.nav('Map Data'));assert.equal(p.events.length,before,'active route is no-op');
  assert.equal(p.summaries.length,2);
  observations.push({id:'map-before-preload-without-await-and-pending-retention',status:'PASS',events:p.events,summaryRequests:p.summaries.length});
  await flush(()=>p.summaries[1].resolve({serverId:321,counts:{},scanState:{serverId:321,isReading:false}}));
  await p.stop();
}

// Preload is best effort; rejecting the actual production preload promise must
// leave the mounted current route usable and produce no unhandled rejection.
{
  const p=await boot();await p.renderApp();const unhandled=[];const listener=reason=>unhandled.push(String(reason));process.on('unhandledRejection',listener);
  const city=p.nav('City Layout');assert.ok(city);
  await flush(()=>city.dispatchEvent(new MouseEvent('mouseover',{bubbles:true,relatedTarget:null})));
  await flush(()=>city.focus());
  assert.equal(p.events.filter(event=>event.module==='./CityLayoutPage.jsx').length,2,'actual hover and focus both preload');
  await flush(()=>p.gate('./CityLayoutPage.jsx').reject(Error('CONTROLLED_IMPORT_REJECTED')));
  await new Promise(resolve=>setImmediate(resolve));process.off('unhandledRejection',listener);
  assert.deepEqual(unhandled,[]);assert.equal(p.container.querySelector('.side-nav [aria-current="page"] .nav-label')?.textContent,'Home');
  observations.push({id:'actual-hover-focus-preload-rejection-is-best-effort',status:'PASS',unhandled,events:p.events});await p.stop();
}

// Actual Mini Games state/effect retention, using the recovered explicit offline
// fixture rather than a probe component. Profile identity remounts the panel.
{
  const p=await boot();await p.renderRetained('mini-games',['mini-games']);await p.settle('./HotkeyPages.jsx');
  const panel=p.container.querySelector('.hotkey-panel');assert.ok(panel);
  const switches=[...panel.querySelectorAll('[role="switch"]')];assert.ok(switches.length>0);
  const toggle=switches.at(-1);const initial=toggle.getAttribute('aria-checked');await p.click(toggle);
  assert.notEqual(toggle.getAttribute('aria-checked'),initial,'meaningful production local edit');
  const edited=toggle.getAttribute('aria-checked');assert.equal(p.intervals.size,1);
  await p.renderRetained('overview',['mini-games','overview']);assert.equal(p.intervals.size,0);assert.equal(panel.style.display,'none');
  await p.renderRetained('mini-games',['mini-games','overview']);assert.equal(p.container.querySelector('.hotkey-panel'),panel);assert.equal(toggle.getAttribute('aria-checked'),edited);assert.equal(p.intervals.size,1);
  await p.renderRetained('mini-games',['mini-games','overview'],'profile-b');const remounted=p.container.querySelector('.hotkey-panel');assert.notEqual(remounted,panel);assert.equal([...remounted.querySelectorAll('[role="switch"]')].at(-1).getAttribute('aria-checked'),initial);assert.equal(p.intervals.size,1);
  observations.push({id:'actual-mini-games-activity-state-effect-retention-and-profile-key-reset',status:'PASS',initial,edited,visibleHiddenReturnedIntervals:[1,0,1],profileReset:true});await p.stop();
}

// Real App theme control drives the canonical reduced-motion, 240-ms fallback
// and view-transition branches. Native preference persistence remains absent.
for(const spec of [{id:'theme-reduced',reduced:true},{id:'theme-fallback',reduced:false},{id:'theme-view-transition',reduced:false,viewTransition:true}]) {
  const p=await boot(spec);await p.renderApp();await p.click(p.container.querySelector('.theme-toggle'));
  assert.equal(document.documentElement.dataset.theme,'dark');assert.equal(localStorage.getItem('lwbridge.theme'),'dark');assert.equal(p.container.querySelector('.theme-toggle').getAttribute('aria-pressed'),'true');
  if(spec.id==='theme-fallback') {assert.ok(document.documentElement.classList.contains('theme-transitioning'));const timers=[...p.timeouts.values()].filter(timer=>timer.delay===240);assert.equal(timers.length,1);await flush(()=>timers[0].fn());assert.equal(document.documentElement.classList.contains('theme-transitioning'),false);}
  else {assert.equal(document.documentElement.classList.contains('theme-transitioning'),false);assert.equal([...p.timeouts.values()].filter(timer=>timer.delay===240).length,0);}
  if(spec.viewTransition)assert.deepEqual(p.transitions,['light']);
  observations.push({id:spec.id,status:'PASS',transitionCalls:p.transitions.length});await p.stop();
}

const sourceHashesAfter=Object.fromEntries(trackedSources.map(name=>[name,sha(fs.readFileSync(path.join(src,name)))]));
assert.deepEqual(sourceHashesAfter,sourceHashesBefore,'production files remain unchanged throughout actual mounted run');
const result={status:'PASS',cases:observations.length,reactVersion:React.version,jsdomVersion:requireDom('jsdom/package.json').version,sourceHashes:sourceHashesAfter,observations,limits:'Actual canonical App/Pages/components/React Activity and Suspense mounted in jsdom. Only dynamic module promises are gated; resolved modules are actual bundled production components. Bridge responses are controlled inert inputs. No native/service/gameplay/updater/image/exit operation or original-runtime pixel proof.'};
fs.writeFileSync(path.join(here,'mounted-deferred-results.json'),JSON.stringify(result,null,2)+'\n');
console.log(`LWB317_ROUTE_LOADING_MOUNTED_OK cases=${observations.length}`);
