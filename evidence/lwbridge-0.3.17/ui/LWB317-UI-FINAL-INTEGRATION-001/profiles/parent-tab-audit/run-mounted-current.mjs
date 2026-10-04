import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../../..'),src=path.join(repo,'src/LWBridge.UI-0.3.17/src');
const req=createRequire(path.join(src,'../package.json')),domReq=createRequire('C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json');
const {JSDOM}=domReq('jsdom'),{build}=req('esbuild'),React=req('react'),{createRoot}=req('react-dom/client');
const h=React.createElement,hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const baselineRevision='307b13ca97840077ebf649eceb1caefc670fb052';
const owned=['App.jsx','AutomationPage.jsx','SquadsPage.jsx','MapDataPage.jsx'];
const observed=[];
async function flush(fn){await React.act(async()=>{if(fn)await fn();await new Promise(r=>setTimeout(r,30));});}
async function boot({baseline=false,search='?view=overview&previewState=shell-profiles',categoryProof=false}={}){
  const dom=new JSDOM('<!doctype html><div id="root"></div>',{url:'http://127.0.0.1/'+search});
  for(const key of ['window','document','HTMLElement','HTMLDialogElement','Event','MouseEvent','KeyboardEvent','localStorage','Node','MutationObserver'])globalThis[key]=key==='window'?dom.window:dom.window[key];
  Object.defineProperty(globalThis,'navigator',{configurable:true,value:dom.window.navigator});globalThis.IS_REACT_ACT_ENVIRONMENT=true;
  HTMLDialogElement.prototype.showModal=function(){this.open=true;};HTMLDialogElement.prototype.close=function(){this.open=false;};
  window.matchMedia=()=>({matches:true,addListener(){},removeListener(){},addEventListener(){},removeEventListener(){}});localStorage.setItem('lwbridge.language','en');
  globalThis.fetch=()=>{throw Error('Network forbidden');};const intervals=new Map();let serial=0;
  window.setInterval=(fn,delay)=>{intervals.set(++serial,{fn,delay});return serial;};window.clearInterval=id=>intervals.delete(id);
  globalThis.__profileProof={stores:new Map(),bridge:{mode:'preview',available:false,profileId:'inert-local',listen:()=>()=>{},invoke:()=>Promise.reject(Error('INERT_NO_PROVIDER')),invokeProfileScoped:()=>Promise.reject(Error('INERT_NO_PROVIDER'))}};
  const bundle=await build({stdin:{contents:'export {App,RetainedPages} from "./App.jsx";export {AutomationPage} from "./AutomationPage.jsx";export {SquadsPage} from "./SquadsPage.jsx";export {MapRoutePage} from "./MapRoutePage.jsx";export {I18nProvider} from "./i18n.jsx";',resolveDir:src,loader:'jsx'},bundle:true,write:false,platform:'browser',format:'cjs',jsx:'automatic',external:['react','react/*','react-dom','react-dom/*'],loader:{'.png':'dataurl'},logLevel:'silent',plugins:[{name:'actual-modules-inert-bridge-and-subscription-observer',setup(b){
    b.onLoad({filter:/backendBridge\.js$/},()=>({contents:'export const backendBridge=globalThis.__profileProof.bridge;',loader:'js'}));
    b.onLoad({filter:/[/\\]App\.jsx$/},args=>({contents:(baseline?execFileSync('git',['show',`${baselineRevision}:src/LWBridge.UI-0.3.17/src/App.jsx`],{cwd:repo}).toString():fs.readFileSync(args.path,'utf8'))+'\nexport {RetainedPages};',loader:'jsx',resolveDir:src}));
    if(baseline)b.onLoad({filter:/[/\\](AutomationPage|SquadsPage|MapDataPage)\.jsx$/},args=>({contents:execFileSync('git',['show',`${baselineRevision}:src/LWBridge.UI-0.3.17/src/${path.basename(args.path)}`],{cwd:repo}).toString(),loader:'jsx',resolveDir:src}));
    if(categoryProof)b.onLoad({filter:/previewConfigHook\.jsx$/},args=>{
      let contents=fs.readFileSync(args.path,'utf8');const anchor='  const state = useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);';assert.ok(contents.includes(anchor));
      contents=contents.replace(anchor,`  if (!globalThis.__profileProof.stores.has(store)) {
        const record={scope,active:0};globalThis.__profileProof.stores.set(store,record);
        const subscribe=store.subscribe;store.subscribe=(listener)=>{record.active++;const stop=subscribe(listener);return()=>{record.active--;stop();};};
      }\n${anchor}`);
      return{contents,loader:'jsx',resolveDir:src};
    });
  }}]});
  const mod={exports:{}};new Function('require','module','exports',bundle.outputFiles[0].text)(req,mod,mod.exports);const p=mod.exports,container=document.getElementById('root'),root=createRoot(container);
  const render=element=>flush(()=>root.render(h(p.I18nProvider,null,element)));
  const click=element=>{assert.ok(element,'click target');return flush(()=>element.dispatchEvent(new MouseEvent('click',{bubbles:true})));};
  const nav=label=>[...container.querySelectorAll('.side-nav button')].find(n=>n.querySelector('.nav-label')?.textContent===label);
  const tab=(selector,label)=>[...container.querySelectorAll(selector+' button[role="tab"]')].find(n=>n.textContent.includes(label));
  const chosen=selector=>container.querySelector(selector+' button[aria-selected="true"]')?.textContent.trim();
  const profile=id=>container.querySelector(`.profile-card[data-profile-id="${id}"] .profile-item`)||[...container.querySelectorAll('.profile-item,.profile-compact-item')].find(n=>(n.getAttribute('aria-label')||n.textContent).includes(id==='preview-local-1'?'Local 1':'Local 2'));
  const stop=async()=>{await flush(()=>root.unmount());assert.equal(intervals.size,0,'all actual window intervals cleaned');dom.window.close();};
  return {p,root,container,render,click,nav,tab,chosen,profile,stop,stores:globalThis.__profileProof.stores};
}

// Same actual App, lazy modules, local profile controls and profile key are run
// at the immutable pre-correction revision and in current production.
for(const baseline of [true,false]){
  const p=await boot({baseline});await p.render(h(p.p.App));
  await p.click(p.nav('Automation'));assert.equal(p.chosen('.automation-categories'),'Daily Tasks');await p.click(p.tab('.automation-categories','Trade Station'));
  const oldAutomation=p.container.querySelector('.automation-categories');
  await p.click(p.nav('Map Data'));await p.click(p.tab('.map-tabs','Train'));
  const input=p.container.querySelector('.map-searchbar input');assert.ok(input);const setter=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,'value').set;await flush(()=>{setter.call(input,'old profile query');input.dispatchEvent(new Event('input',{bubbles:true}));});
  await p.click(p.nav('Squads / AFK'));await p.click(p.tab('.squad-tabs','Equipment'));const oldSquad=p.container.querySelector('.squad-panel');
  await p.click(p.profile('preview-local-2'));
  const squadAfter=p.chosen('.squad-tabs');assert.notEqual(p.container.querySelector('.squad-panel'),oldSquad,'profile key remount retained');
  await p.click(p.nav('Map Data'));const mapAfter=p.chosen('.map-tabs');assert.equal(p.container.querySelector('.map-searchbar input').value,'','child query resets');
  await p.click(p.nav('Automation'));const automationAfter=p.chosen('.automation-categories');assert.notEqual(p.container.querySelector('.automation-categories'),oldAutomation);
  const expected=['Trade Station','Train','Equipment Schemes'],actual=[automationAfter,mapAfter?.replace(/\s*[—\d]+$/u,''),squadAfter];
  const failures=actual.filter((value,i)=>value!==expected[i]).length;assert.equal(failures,baseline?3:0);
  if(!baseline){
    await p.click(p.profile('preview-local-1'));assert.equal(p.chosen('.automation-categories'),'Trade Station');
    await p.click(p.tab('.automation-categories','Chat'));await p.click(p.nav('Map Data'));assert.ok(p.chosen('.map-tabs').startsWith('Train'));await p.click(p.nav('Squads / AFK'));assert.equal(p.chosen('.squad-tabs'),'Equipment Schemes');
    await p.click(p.profile('preview-local-2'));await p.click(p.nav('Automation'));assert.equal(p.chosen('.automation-categories'),'Chat');
  }
  observed.push({case:baseline?'immutable-baseline-profile-A-to-B':'current-A-to-B-to-A-and-independent-selections',pass:true,actual,expected,distinguishingFailures:failures,childProfileKeyRemount:true,childQueryReset:true});await p.stop();
}

// Actual uncontrolled single-profile fallback, including explicit preview starts.
{
  const p=await boot({search:'?view=overview'});await p.render(h(p.p.App));assert.equal(p.container.querySelector('.profile-sidebar'),null);
  await p.click(p.nav('Automation'));await p.click(p.tab('.automation-categories','Chat'));await p.click(p.nav('Home'));await p.click(p.nav('Automation'));assert.equal(p.chosen('.automation-categories'),'Chat');
  await p.render(h(p.p.SquadsPage,{previewState:'squads-equipment-offline'}));assert.equal(p.chosen('.squad-tabs'),'Equipment Schemes');
  await p.render(h(p.p.SquadsPage,{previewState:'squads-equipment-offline',activeTab:null}));assert.equal(p.chosen('.squad-tabs'),'Equipment Schemes');
  await p.render(h(p.p.SquadsPage,{key:'fresh',previewState:'squads-equipment-offline',activeTab:'afk'}));assert.equal(p.chosen('.squad-tabs'),'AFK Tasks');
  observed.push({case:'single-profile-uncontrolled-retention-preview-start-nullish-and-controlled-precedence',pass:true});await p.stop();
}

// Actual card subscriptions, DOM and local collapse state. Only subscribe is
// observed; actual stores, writes, callbacks and leaf components are unchanged.
for(const baseline of [true,false]){
  const p=await boot({baseline,categoryProof:true});await p.render(h(p.p.AutomationPage,{previewState:'automation-trade-positive'}));
  const createdBefore=p.stores.size,active=()=>[...p.stores.values()].filter(r=>r.active>0).map(r=>r.scope);
  const activeBefore=active();const card=p.container.querySelector('.automation-card'),trigger=card?.querySelector('.automation-config-trigger');assert.ok(trigger);await p.click(trigger);const expanded=trigger.getAttribute('aria-expanded');
  await p.click(p.tab('.automation-categories','Trade Station'));const during=active();
  if(!baseline){assert.equal(during.length,1,'only Trade store subscribed');assert.ok(during[0].includes('automation:trade:'));assert.equal(activeBefore.length,7,'only seven Daily cards subscribed');assert.equal(createdBefore,7,'unvisited categories not mounted');}
  else {assert.ok(activeBefore.length>7,'baseline mounts unvisited stores');assert.equal(during.length,activeBefore.length,'CSS hiding leaves subscriptions live');}
  await p.click(p.tab('.automation-categories','Daily Tasks'));assert.ok(p.container.contains(card),'same actual Daily DOM retained');assert.equal(card.style.display,'','Daily is visible again');assert.equal(trigger.getAttribute('aria-expanded'),expanded,'local expanded state retained');
  if(!baseline)assert.deepEqual(active().sort(),activeBefore.sort());
  const beforeStop={createdBefore,activeBefore,during};await p.stop();assert.equal([...p.stores.values()].filter(r=>r.active>0).length,0);
  observed.push({case:baseline?'baseline-all-categories-mount-and-hidden-subscriptions':'current-visited-only-hidden-suspend-state-retain-and-unmount',pass:true,...beforeStop});
}
const result={marker:'LWB317_PROFILE_TABS_MOUNTED_OK',baselineRevision,sourceHashes:Object.fromEntries(owned.map(n=>[n,hash(fs.readFileSync(path.join(src,n)))])),cases:observed,react:React.version,jsdom:domReq('jsdom/package.json').version,limits:'Actual canonical App/panels/React lazy/Activity mounted in jsdom, actual local profile controls, inert absent bridge. Subscription-only observer retains actual config stores and leaf callbacks. No native/game/service action, original protected runtime or pixel proof.'};
fs.writeFileSync(path.join(here,'mounted-current-results.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({marker:result.marker,groups:observed.length,baselineFailures:3,currentFailures:0}));
