// Controlled mounted controls and config persistence. Genuine packaged proof is separate.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { applyAutoScanConfigIntent, autoScanShouldRun, loadAutoScanConfig, normalizeAutoScanConfig, saveAutoScanConfig } from "../src/mapAutoConfig.js";
import { withMapScanControlTexts } from "../src/locales/map017-scan-controls.js";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), { build } = req("esbuild"), React = req("react");
const bundle = await build({stdin:{contents:'export { MapRoutePage } from "./MapRoutePage.jsx"; export { I18nProvider } from "./i18n.jsx";',resolveDir:path.join(root,"src"),loader:"jsx"},bundle:true,write:false,platform:"browser",format:"cjs",jsx:"automatic",external:["react","react/*","react-dom","react-dom/*"],loader:{".png":"dataurl",".svg":"dataurl"},logLevel:"silent"});
const mod = {exports:{}};
new Function("require","module","exports",bundle.outputFiles[0].text)(req,mod,mod.exports);
const { MapRoutePage, I18nProvider } = mod.exports;
const dom = new JSDOM('<div id="root"></div>',{url:"http://127.0.0.1/"});
for(const key of ["window","document","HTMLElement","HTMLDialogElement","Event","MouseEvent","Node","MutationObserver","localStorage"]) globalThis[key]=key==="window"?dom.window:dom.window[key];
Object.defineProperty(globalThis,"navigator",{configurable:true,value:dom.window.navigator});
globalThis.IS_REACT_ACT_ENVIRONMENT=true;
const {createRoot}=req("react-dom/client");
window.matchMedia=()=>({matches:false,addListener(){},removeListener(){}});
window.setInterval=()=>1;window.clearInterval=()=>{};
localStorage.setItem("lwbridge.language","en");
const flush=async(fn)=>React.act(async()=>{if(fn)await fn();for(let i=0;i<20;i++)await Promise.resolve();});
const reactRoot=createRoot(document.getElementById("root"));
let config=normalizeAutoScanConfig({enabled:false,serverIds:[2211,2213],selectedTypes:["railway"],returnToOriginalServer:true});
let state={serverId:2212,isReading:false,phase:"idle",selectedTypes:["city"],scanMode:"fast"};
let runs=0, starts=0, online=true, autoRunning=false;
const api={savedServers:async()=>[],dataOptions:async()=>({}),search:async()=>({rows:[],total:0}),start:async()=>{starts++;return state;}};
const render=()=>flush(()=>reactRoot.render(React.createElement(I18nProvider,{},React.createElement(MapRoutePage,{
  mapApi:api,mapOwner:"map017-owner",bridgeMode:"native",backendAvailable:true,online,currentServerId:2212,
  scanState:state,summary:{serverId:2212,counts:{}},onState(){},autoScanConfig:config,autoScanRunning:autoRunning,
  onAutoScanConfig:(patch,operation)=>{config=applyAutoScanConfigIntent(config,{patch,operation,editedAt:1_800_000_000_000});},
  onAutoScanRunNow:()=>runs++,
}))));
const button=text=>[...document.querySelectorAll("button")].find(node=>node.textContent===text);
const checkboxes=()=>[...document.querySelectorAll(".map-category-menu input")];
await render();await flush();
const manual=button("Start Scan");
assert.ok(manual&&!manual.disabled);
await flush(()=>checkboxes()[0].click());
assert.ok(manual.disabled,"Manual Start must disable when its final choice is unchecked");
assert.match(document.querySelector('[data-map-empty-selection="manual"]').textContent,/at least one/);
await flush(()=>manual.click());assert.equal(starts,0);
state={...state,selectedTypes:["city","truck"]};await render();
assert.equal(checkboxes().filter(input=>input.checked).length,0,"native status refresh must not erase a deliberate empty Manual choice");
await flush(()=>checkboxes()[3].click());
assert.equal(manual.disabled,false);
assert.equal(document.querySelector('[data-map-empty-selection="manual"]'),null);
await flush(()=>button("Auto Scan").click());
let run=button("Run now");
assert.ok(run&&!run.disabled,"Run now must work with schedule disabled and selected kinds");
await flush(()=>run.click());assert.equal(runs,1);assert.equal(config.enabled,false);
assert.equal(checkboxes()[4].disabled,false,"the final selected Auto category must be uncheckable");
await flush(()=>checkboxes()[4].click());await render();
assert.deepEqual(config.selectedTypes,[]);
run=button("Run now");assert.equal(run.disabled,true);
assert.match(document.querySelector('[data-map-empty-selection="auto"]').textContent,/at least one/);
await flush(()=>run.click());assert.equal(runs,1);
saveAutoScanConfig("map017",config,localStorage);
assert.deepEqual(loadAutoScanConfig("map017",localStorage).selectedTypes,[],"stored explicit empty Auto choice must survive reload");
assert.equal(autoScanShouldRun({...config,enabled:true,nextRunAt:0},Date.now(),true,false,false),false);
await flush(()=>checkboxes()[4].click());await render();
assert.equal(button("Run now").disabled,false);
assert.equal(document.querySelector('[data-map-empty-selection="auto"]'),null);
await flush(()=>document.querySelector(".map-auto-scan-master input").click());await render();
assert.equal(config.enabled,true);assert.equal(runs,1,"enabling the schedule must not invoke Run now");
assert.equal(config.nextRunAt,1_800_000_000_000+60*60_000);
online=false;await render();assert.equal(button("Run now").disabled,true,"offline execution must remain disabled");
online=true;autoRunning=true;await render();assert.equal(button("Run now").disabled,true,"an existing cycle must prevent a second Run now");
for(const language of ["en","zh-CN","zh-TW","ja","ko","vi","id","ru","pt"])
  assert.ok(withMapScanControlTexts(language,{})["map.scanCategoryRequired"],`missing empty-selection hint: ${language}`);
await flush(()=>reactRoot.unmount());dom.window.close();
console.log("MAP017_MOUNTED_SCAN_CONTROLS_OK",JSON.stringify({oneOffRunActions:runs,nativeStarts:starts}));
