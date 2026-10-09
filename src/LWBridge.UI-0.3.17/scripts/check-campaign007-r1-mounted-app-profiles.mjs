import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {createRequire} from "node:module";
import {fileURLToPath} from "node:url";
const here=path.dirname(fileURLToPath(import.meta.url)), repo=path.resolve(here,"../../..");
const src=path.join(repo,"src/LWBridge.UI-0.3.17/src");
const uiReq=createRequire(path.join(src,"../package.json"));
const domReq=createRequire("C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const {JSDOM}=domReq("jsdom");
const {build}=uiReq("esbuild");
const React=uiReq("react");
const {createRoot}=uiReq("react-dom/client");
const native=JSON.parse(fs.readFileSync(path.resolve(process.argv[2]),"utf8"));
assert.ok(native.proofType.includes("REAL_V22_CAPTURED"));
const rendered=[],commands=[],errors=[];
const savedError=console.error;
console.error=(...parts)=>{errors.push(parts.map(String).join(" "));savedError(...parts)};
for(const [language,theme] of [["en","light"],["ja","dark"]]){
  const dom=new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>',{url:"http://127.0.0.1/?view=overview"});
  for (const key of ["window","document","HTMLElement","HTMLDialogElement","Event","MouseEvent","KeyboardEvent","Node","MutationObserver","localStorage"])globalThis[key]=key==="window"?dom.window:dom.window[key];
  Object.defineProperty(globalThis,"navigator",{configurable:true,value:dom.window.navigator});
  globalThis.IS_REACT_ACT_ENVIRONMENT=true;
  globalThis.fetch=()=>{throw Error("External network forbidden")};
  window.matchMedia=()=>({matches:true});
  localStorage.setItem("lwbridge.language",language);
  localStorage.setItem("lwbridge.theme",theme);
  document.documentElement.dataset.theme=theme;
  const intervals=new Map(),timeouts=new Map(),listeners=[];let id=0;
  window.setInterval=(fn,delay)=>{intervals.set(++id,{fn,delay});return id};
  window.clearInterval=n=>intervals.delete(n);
  window.setTimeout=(fn,delay)=>{timeouts.set(++id,{fn,delay});return id};
  window.clearTimeout=n=>timeouts.delete(n);
  let profileGeneration=1, delayedA=null, delayedAReject=null, deferNextA=false;
  const profiles=[{id:"profile-a",displayName:"Local 1",enabled:true,serverId:2212},{id:"profile-b",displayName:"Local 2",enabled:true,serverId:2212}];
  const snapshot=selectedProfileId=>({selectedProfileId,maxProfiles:2,profiles});
  window.__LWBridgeBootstrap={profiles:snapshot("profile-a"),localRuntime:{profileCapacity:2}};
  const bridge={mode:"native",available:true,profileId:"profile-a",
    setSelectedProfile(id){this.profileId=id;profileGeneration++;},
    currentProfileOwner:()=>({profileId:bridge.profileId,generation:profileGeneration}),
    isCurrentProfileOwner:x=>x.profileId===bridge.profileId&&x.generation===profileGeneration,
    listen:(eventName,fn)=>{const state={eventName,fn,active:true};listeners.push(state);return ()=>{state.active=false}},
    invokeProfileScoped: (cmd,args)=>invoke(cmd,args),
    invoke:(cmd,args)=>invoke(cmd,args)
  };
  function invoke(cmd,args={}){
    commands.push({language,cmd,kind:args?.kind||null,page:args?.query?.page||null});
    if(cmd==="profile_instances_reconcile")return Promise.resolve({phase:"stopped",connectionState:"offline",results:[]});
    if(cmd==="local_game_launch_status")return Promise.resolve({phase:"stopped",gameRunning:false,connectionState:"offline"});
    if(cmd==="profile_list")return Promise.resolve(snapshot("profile-a"));
    if(cmd==="profile_select")return Promise.resolve(snapshot(args.profileId));
    if(cmd==="map_search"){
      if(args.kind==="resource"){
        assert.equal(args.query.serverId,2212);
        if(bridge.profileId==="profile-b")return Promise.resolve({rows:[],total:0});
        if(deferNextA && args.query.page===2){deferNextA=false;return new Promise((resolve,reject)=>{delayedA=resolve;delayedAReject=reject;});}
        return Promise.resolve(args.query.page===2?native.profileA.secondPage:
          args.query.resourceNameKey==="129027"?native.profileA.positiveResourceFilter:
          native.profileA.firstPage);
      }
      return Promise.resolve({rows:[],total:0});
    }
    if(cmd==="map_data_options")return Promise.resolve(bridge.profileId==="profile-a"?native.profileA.options:{serverId:2212,counts:{resource:0},names:{resource:[]}});
    if(cmd==="map_summary")return Promise.resolve({serverId:2212,counts:{resource:bridge.profileId==="profile-a"?8008:0},scanState:{serverId:2212,serverIdSource:"live",isReading:false,selectedTypes:["resource"],scanMode:"normal"}});
    if(cmd==="map_scan_status")return Promise.resolve({serverId:2212,serverIdSource:"live",isReading:false,selectedTypes:["resource"]});
    if(cmd==="map_plunder_jobs_list")return Promise.resolve({dispatchJobs:[],truckJobs:[]});
    if(cmd==="get_status")return Promise.resolve({xluaOnline:false,pending:0});
    if(cmd==="proxy_status")return Promise.resolve({gameRunning:false});
    if(cmd==="game_root_status")return Promise.resolve({valid:false});
    if(cmd==="game_recovery_status")return Promise.resolve({state:"idle"});
    if(cmd==="server_jump_history_import")return Promise.resolve([]);
    if(cmd==="update_status")return Promise.resolve({phase:"idle",currentVersion:"",latestVersion:null});
    if(cmd==="local_config_get")return Promise.resolve({autoLaunchGame:false,autoReconnect:false});
    if(cmd==="local_map_auto_scan_status")return Promise.resolve({running:false});
    if(cmd==="profile_instance_status")return Promise.resolve({phase:"stopped",connectionState:"offline"});
    throw Error("CAMPAIGN007 forbidden or unexpected App command "+cmd);
  }
  globalThis.__campaignApp={bridge};
  const bundle=await build({stdin:{contents:'export {App} from "./App.jsx";export {I18nProvider} from "./i18n.jsx";',resolveDir:src,loader:"jsx"},bundle:true,write:false,platform:"browser",format:"cjs",jsx:"automatic",external:["react","react/*","react-dom","react-dom/*"],loader:{".png":"dataurl",".svg":"dataurl"},logLevel:"silent",plugins:[{
    name:"inert-native-transport",setup(b){
      b.onLoad({filter:/backendBridge\.js$/},()=>({contents:"export const backendBridge=globalThis.__campaignApp.bridge;",loader:"js"}));
    }
  }]});
  const m={exports:{}};
  new Function("require","module","exports",bundle.outputFiles[0].text)(uiReq,m,m.exports);
  const root=createRoot(document.getElementById("root")),container=document.getElementById("root");
  const flush=async fn=>React.act(async()=>{if(fn)await fn();for(let i=0;i<15;i++)await Promise.resolve();});
  await flush(()=>root.render(React.createElement(m.exports.I18nProvider,null,React.createElement(m.exports.App))));
  for(let i=0;i<3;i++)await flush();
  assert.ok(container.querySelector(".home-panel")||container.querySelector(".main-view"),"actual App Home not mounted");
  rendered.push({language,theme,phase:"home",textLength:container.textContent.length});
  const buttons=[...container.querySelectorAll(".side-nav button")];
  assert.equal(buttons.length,8,"original eight navigation buttons");
  await flush(()=>buttons[2].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<8;i++)await flush();
  assert.ok(container.querySelector(".map-panel"),"real Map page not loaded from App");
  const tabButtons=[...container.querySelectorAll(".map-tabs [role=tab]")];
  assert.ok(tabButtons.length>=8,"actual Map tab list missing");
  await flush(()=>tabButtons[1].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<7;i++)await flush();
  const count=()=>container.querySelector(".map-result-count")?.textContent||"";
  assert.ok(/8008/.test(count()),"actual App Map data did not consume 8008 native real rows");
  assert.equal(container.querySelectorAll(".map-table tbody tr").length,50);
  rendered.push({language,theme,phase:"actual-App-Resource",count:count(),rows:50});
  const page2=container.querySelector(".map-pagination button:last-of-type");
  assert.ok(page2,"page2 control missing in actual App");
  await flush(()=>page2.dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<5;i++)await flush();
  assert.ok(commands.some(x=>x.language===language&&x.cmd==="map_search"&&x.kind==="resource"&&x.page===2));
  rendered.push({language,theme,phase:"actual-App-page2",count:count(),rows:container.querySelectorAll(".map-table tbody tr").length});
  await flush(()=>buttons[0].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<4;i++)await flush();
  assert.ok(container.querySelector(".home-panel")||container.querySelector(".main-view"));
  await flush(()=>buttons[2].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<5;i++)await flush();
  assert.ok(/8008/.test(count()),"actual App return lost Map rows");
  rendered.push({language,theme,phase:"Home-Map-retained-return",count:count()});
  const profileButtons=[...container.querySelectorAll(".profile-compact-item")];
  assert.equal(profileButtons.length,2,"actual App must expose both profile controls");
  const prev=container.querySelector(".map-pagination button:first-of-type");
  if(prev) {await flush(()=>prev.dispatchEvent(new MouseEvent("click",{bubbles:true})));for(let i=0;i<5;i++)await flush();}
  deferNextA=true;
  const delayedNext=container.querySelector(".map-pagination button:last-of-type");
  assert.ok(delayedNext,"page2 deferred control unavailable");
  await flush(()=>delayedNext.dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<3;i++)await flush();
  assert.ok(delayedA,"old profile page2 request not deferred");
  await flush(()=>profileButtons[1].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<9;i++)await flush();
  assert.equal(bridge.profileId,"profile-b","profile B not selected in actual App");
  assert.ok(!/8008/.test(count()),"B must not show A published rows");
  rendered.push({language,theme,phase:"mounted-App-profile-B",count:count()});
  delayedA(native.profileA.secondPage);
  for(let i=0;i<6;i++)await flush();
  assert.ok(!/8008/.test(count()),"late A response contaminated B");
  rendered.push({language,theme,phase:"retired-A-deferred-page2",count:count()});
  await flush(()=>profileButtons[0].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<10;i++)await flush();
  assert.equal(bridge.profileId,"profile-a");
  assert.ok(/8008/.test(count()),"A return did not restore A real rows");
  rendered.push({language,theme,phase:"mounted-App-profile-A-return",count:count()});
  deferNextA=true;delayedA=null;delayedAReject=null;
  const rejectNext=container.querySelector(".map-pagination button:last-of-type");
  assert.ok(rejectNext,"deferred rejection trigger requires page2");
  await flush(()=>rejectNext.dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<3;i++)await flush();
  assert.ok(delayedAReject,"A in-flight request for retired-error case must be pending");
  rendered.push({language,theme,phase:"pending-A-request-before-switch"});
  await flush(()=>profileButtons[1].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<8;i++)await flush();
  assert.equal(bridge.profileId,"profile-b");
  delayedAReject(Object.assign(new Error("R1_RETIRED_A_PROVIDER_ERROR"),{code:"R1_RETIRED_A_PROVIDER_ERROR"}));
  for(let i=0;i<7;i++)await flush();
  assert.ok(!/8008/.test(count()),"rejected old A request contaminated B");
  rendered.push({language,theme,phase:"retired-A-rejected-response-no-B-leak",count:count()});
  await flush(()=>profileButtons[0].dispatchEvent(new MouseEvent("click",{bubbles:true})));
  for(let i=0;i<9;i++)await flush();
  assert.ok(/8008/.test(count()),"A did not recover after retired error");
  rendered.push({language,theme,phase:"A-recovery-after-retired-error",count:count()});
  await flush(()=>root.unmount());
  assert.equal(intervals.size,0,"App leaked interval after unmount");
  assert.ok(listeners.every(x=>!x.active),"App listener leak");
  dom.window.close();
}
console.error=savedError;
assert.deepEqual(errors,[],"Unexpected production mounted errors");
const result={status:"PASS",evidenceClass:"Actual App.jsx/HomePage.jsx/MapRoutePage.jsx/MapDataPage.jsx with real 004 Current-v22 Resource command payload, isolated jsdom/react transport; NOT original 0.3.17 runtime or foreground WebView",cases:rendered.length,rendered,commands:[...new Set(commands.map(x=>x.cmd))],externalGameActions:0,originalParity:"UNPROVEN",errors};
fs.writeFileSync(path.resolve(process.argv[3]),JSON.stringify(result,null,2)+"\n");
console.log("CAMPAIGN007_R1_ACTUAL_APP_PROFILE_A_B_A_DEFERRED_PASS cases="+rendered.length+" commands="+result.commands.join(","));
