import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {createRequire} from "node:module";
import {fileURLToPath} from "node:url";

const here=path.dirname(fileURLToPath(import.meta.url));
const src=path.resolve(here,"../src");
const uiReq=createRequire(path.resolve(here,"../package.json"));
const domReq=createRequire("C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const {JSDOM}=domReq("jsdom");
const {build}=uiReq("esbuild");
const React=uiReq("react");
const {createRoot}=uiReq("react-dom/client");
const all=[];
const errors=[];
const saved=console.error;
console.error=(...args)=>errors.push(args.join(" "));

for (const [language,theme] of [["en","light"],["ja","dark"]]) {
  for (const scenario of ["own-code","other-profile","success"]) {
    const dom=new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>',
        {url:"http://127.0.0.1/?view=overview"});
    for (const key of ["window","document","HTMLElement","HTMLDialogElement",
        "Event","MouseEvent","KeyboardEvent","Node","MutationObserver","localStorage"])
      globalThis[key]=key==="window"?dom.window:dom.window[key];
    Object.defineProperty(globalThis,"navigator",{configurable:true,value:dom.window.navigator});
    globalThis.IS_REACT_ACT_ENVIRONMENT=true;
    globalThis.fetch=()=>{throw Error("HOME009 no network")};
    window.matchMedia=()=>({matches:true});
    localStorage.setItem("lwbridge.language",language);
    localStorage.setItem("lwbridge.theme",theme);
    document.documentElement.dataset.theme=theme;
    const intervals=new Map(),timeouts=new Map(),listeners=[];
    let id=0,reconciles=0;
    window.setInterval=(fn,delay)=>{intervals.set(++id,{fn,delay});return id};
    window.clearInterval=n=>intervals.delete(n);
    window.setTimeout=(fn,delay)=>{timeouts.set(++id,{fn,delay});return id};
    window.clearTimeout=n=>timeouts.delete(n);
    const profile={id:"profile-a",displayName:"Local A",enabled:true,serverId:2212};
    const snapshot={selectedProfileId:"profile-a",maxProfiles:2,profiles:[profile]};
    window.__LWBridgeBootstrap={profiles:snapshot,localRuntime:{profileCapacity:2}};
    const bridge={mode:"native",available:true,profileId:"profile-a",
      setSelectedProfile(x){this.profileId=x},
      currentProfileOwner:()=>({profileId:"profile-a",generation:1}),
      isCurrentProfileOwner:x=>x.profileId==="profile-a"&&x.generation===1,
      listen(name,fn){const state={name,active:true};listeners.push(state);return ()=>{state.active=false}},
      invokeProfileScoped:(cmd,args)=>invoke(cmd,args),
      invoke:(cmd,args)=>invoke(cmd,args)};
    function invoke(cmd,args={}) {
      if(cmd==="profile_instances_reconcile") {
        reconciles++;
        assert.equal(typeof args.autoLaunchAll,"boolean","reconcile request must pass explicit UI intent; inert helper never starts a game");
        const errors=scenario==="success"?[]:[{
          profileId:scenario==="own-code"?"profile-a":"profile-b",
          error:"RECOVERY_RECORD_INVALID",
          message:"Different human-readable detail must not become the error code",
        }];
        return Promise.resolve({errors});
      }
      if(cmd==="local_game_launch_status")return Promise.resolve({phase:"stopped",gameRunning:false,connectionState:"offline"});
      if(cmd==="profile_list"||cmd==="profile_select")return Promise.resolve(snapshot);
      if(cmd==="map_summary")return Promise.resolve({serverId:2212,counts:{},scanState:{isReading:false}});
      if(cmd==="map_scan_status")return Promise.resolve({isReading:false});
      if(cmd==="map_plunder_jobs_list")return Promise.resolve({dispatchJobs:[],truckJobs:[]});
      if(cmd==="get_status")return Promise.resolve({xluaOnline:false,pending:0});
      if(cmd==="proxy_status")return Promise.resolve({gameRunning:false});
      if(cmd==="game_root_status")return Promise.resolve({valid:false});
      if(cmd==="game_recovery_status")return Promise.resolve({state:"idle"});
      if(cmd==="server_jump_history_import")return Promise.resolve([]);
      if(cmd==="update_status")return Promise.resolve({phase:"idle",currentVersion:""});
      if(cmd==="local_config_get")return Promise.resolve({autoLaunchGame:false,autoReconnect:false});
      if(cmd==="local_map_auto_scan_status")return Promise.resolve({running:false});
      if(cmd==="profile_instance_status")return Promise.resolve({phase:"stopped",connectionState:"offline"});
      throw Error("Unexpected production App command: "+cmd);
    }
    globalThis.__home009App={bridge};
    const bundle=await build({stdin:{contents:'export {App} from "./App.jsx";export {I18nProvider} from "./i18n.jsx";',
       resolveDir:src,loader:"jsx"},bundle:true,write:false,platform:"browser",format:"cjs",jsx:"automatic",
       external:["react","react/*","react-dom","react-dom/*"],
       loader:{".png":"dataurl",".svg":"dataurl"},logLevel:"silent",
       plugins:[{name:"held-native-transport",setup(b){
         b.onLoad({filter:/backendBridge\.js$/},()=>({
           contents:"export const backendBridge=globalThis.__home009App.bridge;",loader:"js"}));
       }}]});
    const mod={exports:{}};
    new Function("require","module","exports",bundle.outputFiles[0].text)(uiReq,mod,mod.exports);
    const container=document.getElementById("root"),root=createRoot(container);
    const flush=async fn=>React.act(async()=>{
      if(fn)await fn();for(let i=0;i<18;i++)await Promise.resolve();
    });
    await flush(()=>root.render(React.createElement(mod.exports.I18nProvider,null,
        React.createElement(mod.exports.App))));
    for(let i=0;i<6;i++)await flush();
    assert.ok(container.querySelector(".home-panel")||container.querySelector(".main-view"),
        "real canonical Home was not mounted");
    assert.equal(reconciles,1,"startup reconciled more than once");
    const errorNode=container.querySelector(".game-root-error");
    // An untranslated, source-backed code is preserved on the wire, then
    // localized through HomePage's explicitly generic action-failure fallback.
    const expectedGeneric=language==="en"
        ?"The action could not be completed.":"操作を完了できませんでした。";
    if(scenario==="own-code") {
      assert.equal(errorNode?.textContent,expectedGeneric,
          "own profile original error code was not surfaced through localized Home failure");
      assert.ok(!errorNode.textContent.includes("Different human-readable detail"),
          "Home displayed secondary message rather than source code translation");
    } else {
      assert.ok(!errorNode?.textContent.includes(expectedGeneric),
          "unrelated or absent error leaked into Home");
    }
    all.push({language,theme,scenario,reconciles,localizedFailureShown:
      errorNode?.textContent===expectedGeneric});
    await flush(()=>root.unmount());
    assert.equal(intervals.size,0,"App timer leak");
    assert.ok(listeners.every(x=>!x.active),"App listener leak");
    dom.window.close();
  }
}
console.error=saved;
assert.deepEqual(errors,[],"mounted App emitted React errors");
const result={status:"PASS",cases:all.length,all,externalGameActions:0,
  originalRuntimeExecuted:false,errors};
if(process.argv[2])fs.writeFileSync(path.resolve(process.argv[2]),JSON.stringify(result,null,2)+"\n");
console.log("HOME009_ACTUAL_APP_RECONCILE_ERROR_PASS cases="+all.length);
