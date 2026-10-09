import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import {createRequire} from "node:module";
import {fileURLToPath} from "node:url";
const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,"../../..");
const src=path.join(repo,"src/LWBridge.UI-0.3.17/src");
const uiReq=createRequire(path.join(src,"../package.json"));
const domReq=createRequire("C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const {JSDOM}=domReq("jsdom");
const {build}=uiReq("esbuild");
const React=uiReq("react");
const {createRoot}=uiReq("react-dom/client");
assert.ok(process.argv[2] && process.argv[3],"Expected actual native replay JSON and report path");
const native=JSON.parse(fs.readFileSync(path.resolve(process.argv[2]),"utf8"));
assert.ok(native.proofType.includes("REAL_V22_CAPTURED"));
const corpus=native.profileA;
const rendered=[];
const errors=[];
const requests=[];
const originalConsoleError=console.error;
console.error=(...parts)=>{errors.push(parts.map(String).join(" "));originalConsoleError(...parts)};
const bundle=await build({stdin:{contents:'export {MapDataPage} from "./MapDataPage.jsx";export {I18nProvider} from "./i18n.jsx";export {createMapApi} from "./mapBackend.js";',resolveDir:src,loader:"jsx"},bundle:true,write:false,platform:"browser",format:"cjs",jsx:"automatic",external:["react","react/*","react-dom","react-dom/*"],loader:{".png":"dataurl",".svg":"dataurl"},logLevel:"silent"});
const m={exports:{}};
new Function("require","module","exports",bundle.outputFiles[0].text)(uiReq,m,m.exports);
const {MapDataPage,I18nProvider,createMapApi}=m.exports;
const flush=async (fn)=>{await React.act(async()=>{if(fn)await fn();for(let i=0;i<15;i++)await Promise.resolve();});};
async function run(language,theme){
  const dom=new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>',{url:"http://127.0.0.1/campaign007"});
  for (const key of ["window","document","HTMLElement","HTMLDialogElement","Event","MouseEvent","Node","MutationObserver","localStorage"])globalThis[key]=key==="window"?dom.window:dom.window[key];
  Object.defineProperty(globalThis,"navigator",{configurable:true,value:dom.window.navigator});
  globalThis.IS_REACT_ACT_ENVIRONMENT=true;
  window.matchMedia=()=>({matches:true});
  window.localStorage.setItem("lwbridge.language",language);
  window.localStorage.setItem("lwbridge.theme",theme);
  document.documentElement.dataset.theme=theme;
  const timers=new Set(), listenerTokens=[];
  window.setInterval=(f,n)=>{const id={f,n};timers.add(id);return id};
  window.clearInterval=id=>timers.delete(id);
  window.setTimeout=(f,n)=>{const id={f,n};listenerTokens.push(id);return id};
  window.clearTimeout=id=>{};
  let profile="A",generation=1,staleDeferred=null;
  const bridge={
    get profileId(){return profile},
    currentProfileOwner:()=>({profileId:profile,generation}),
    isCurrentProfileOwner:owner=>owner.profileId===profile&&owner.generation===generation,
    listen:()=>()=>{},
    invokeProfileScoped:async(cmd,args)=>{
      requests.push({language,profile,cmd,query:args?.query||null});
      if(cmd==="map_search"){
        if(args.kind!=="resource"||args.query.serverId!==2212)throw Error("unexpected Map search route");
        if(profile==="B")return native.profileB.firstPage;
        if(args.query.resourceNameKey==="129027")return corpus.positiveResourceFilter;
        if(args.query.resourceNameKey==="__campaign007_no_such_resource_key__")return corpus.impossibleResourceFilter;
        if(args.query.page===2)return corpus.secondPage;
        return corpus.firstPage;
      }
      if(cmd==="map_data_options")return profile==="A"?corpus.options:{serverId:2212,counts:{resource:0},names:{resource:[],monster:[]}};
      if(cmd==="map_summary")return {serverId:2212,counts:{resource:profile==="A"?8008:0},scanState:{serverId:2212,serverIdSource:"live",isReading:false}};
      if(cmd==="map_scan_status")return {serverId:2212,serverIdSource:"live",isReading:false};
      if(cmd==="get_status")return {xluaOnline:false};
      if(cmd==="proxy_status")return {gameRunning:false};
      if(cmd==="map_plunder_jobs_list")return {dispatchJobs:[],truckJobs:[]};
      throw Error("disallowed command "+cmd);
    }
  };
  const container=document.getElementById("root"),root=createRoot(container);
  let api=createMapApi(bridge);
  const state={serverId:2212,serverIdSource:"live",isReading:false,phase:"idle",selectedTypes:["resource"],scanMode:"normal"};
  const element=()=>React.createElement(I18nProvider,{key:language},React.createElement(MapDataPage,{
    key:profile+":"+generation,mapApi:api,bridgeMode:"native",backendAvailable:true,
    online:false,activeTab:"resource",currentServerId:2212,
    scanState:state,summary:{serverId:2212,counts:{resource:profile==="A"?8008:0},scanState:state},
    onState:()=>{},onCounts:()=>{},gameTexts:{}
  }));
  await flush(()=>root.render(element()));
  const wait=async()=>{for(let i=0;i<5;i++)await flush()};
  await wait();
  const countText=()=>container.querySelector(".map-result-count")?.textContent||"";
  const rows=()=>container.querySelectorAll("tbody tr").length;
  rendered.push({language,theme,phase:"A-initial",countText:countText(),tableRows:rows(),bodyTextLength:container.textContent.length});
  assert.match(countText(),/8[,.]?008/,"real A count in mounted canonical Map panel");
  assert.ok(rows()>=50,"real mounted table first page rows");
  // Exercise the real production Pagination control, not merely API.query(page2).
  const page2Button=container.querySelector(".map-pagination button:last-of-type");
  assert.ok(page2Button && !page2Button.disabled,"page2 pagination control absent");
  await flush(()=>page2Button.dispatchEvent(new MouseEvent("click",{bubbles:true})));
  await wait();
  assert.ok(requests.some(x=>x.language===language && x.profile==="A" &&
    x.cmd==="map_search" && x.query?.page===2),"mounted next-page failed to dispatch page2");
  assert.ok(rows()===50 && container.querySelector(".map-pagination")?.textContent,
    "mounted second page not shown");
  rendered.push({language,theme,phase:"A-page2",countText:countText(),
    tableRows:rows(),paginationText:container.querySelector(".map-pagination")?.textContent});
  // Exercise actual Resource filter select and dynamic refreshed results.
  const nameSelect=[...container.querySelectorAll(".map-searchbar select")].find(
    node=>[...node.options].some(opt=>opt.value==="129027"));
  assert.ok(nameSelect,"real Resource name option from native response absent");
  await flush(()=>{
    const descriptor=Object.getOwnPropertyDescriptor(window.HTMLSelectElement.prototype,"value");
    descriptor.set.call(nameSelect,"129027");
    nameSelect.dispatchEvent(new Event("change",{bubbles:true}));
  });
  await wait();
  rendered.push({language,theme,phase:"A-name-filter",countText:countText(),tableRows:rows(),
    selectValue:nameSelect.value});
  assert.ok(nameSelect.value==="129027" && /2608/.test(countText()),
    "mounted real positive Resource filter did not display 2608");
  profile="B";generation++;api=createMapApi(bridge);
  await flush(()=>root.render(element()));await wait();
  rendered.push({language,theme,phase:"B",countText:countText(),tableRows:rows()});
  assert.ok(!/8[,.]?008/.test(countText()),"B must not inherit 8008");
  profile="A";generation++;api=createMapApi(bridge);
  await flush(()=>root.render(element()));await wait();
  rendered.push({language,theme,phase:"A-return",countText:countText(),tableRows:rows()});
  assert.match(countText(),/8[,.]?008/,"A return retains completed real rows");
  await flush(()=>root.unmount());
  assert.equal(timers.size,0,"Map page timer leak at unmount");
  dom.window.close();
}
for(const [language,theme] of [["en","light"],["ja","dark"]])await run(language,theme);
console.error=originalConsoleError;
assert.deepEqual(errors,[],"unexpected actual mounted production frontend issues");
const output={status:"PASS",proofType:"actual production MapDataPage.jsx + I18nProvider.jsx + mapBackend.js mounted in isolated jsdom with REAL current-v22 Resource native command payload and controlled external transport; not current LastWar or original protected WebView",languageTheme:["en/light","ja/dark"],cases:rendered,requestCount:requests.length,commands:[...new Set(requests.map(x=>x.cmd))],errors,externalGameActions:0,originalParity:"UNKNOWN"};
fs.writeFileSync(path.resolve(process.argv[3]),JSON.stringify(output,null,2)+"\n");
console.log("CAMPAIGN007_CANONICAL_MOUNTED_REAL_RESOURCE_PASS cases="+rendered.length+" no leaked timers no native game actions");
