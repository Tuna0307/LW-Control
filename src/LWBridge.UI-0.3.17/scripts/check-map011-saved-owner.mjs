// MAP-011 controlled mounted inversion. No game/desktop operations.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";


// Mount the real MapDataPage + native callbacks with controlled locale responses.
// Game binary translation values are tested separately by the Desktop checker.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const { createRoot } = req("react-dom/client");
const entry = path.join(root, "src");
const bundle = await build({
  stdin: { contents: 'export { MapRoutePage } from "./MapRoutePage.jsx"; export { I18nProvider } from "./i18n.jsx"; export { selectedNativeGameTextRoot } from "./gameTextSource.js";', resolveDir: entry, loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const mod = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);
const { MapRoutePage, I18nProvider, selectedNativeGameTextRoot } = mod.exports;
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 20; i++) await Promise.resolve(); });
const delayed = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};

const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: "http://127.0.0.1/" });
for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
window.setInterval = () => 1;
window.clearInterval = () => {};
window.setTimeout = () => 1;
window.clearTimeout = () => {};
localStorage.setItem("lwbridge.language", "en");
const reactRoot = createRoot(document.getElementById("root"));
const element = document.getElementById("root");
const rootPath = "C:\\admitted-game";
assert.equal(selectedNativeGameTextRoot({ valid: true, root: rootPath, source: "saved", candidates: [] }), rootPath,
  "actual native game_root_status.Root must feed production Map name-loading identity");
assert.equal(selectedNativeGameTextRoot({ valid: false, root: rootPath }), "", "invalid native root must be rejected");
assert.equal(selectedNativeGameTextRoot({ valid: true, path: rootPath }), "", "launch-admission Path is a different native contract");
const keys = ["fixture.resource.locale", "fixture.monster.locale"];
const names = { en: ["Fixture Resource EN", "Fixture Monster EN"], ja: ["Fixture Resource JA", "Fixture Monster JA"] };
const results = [];

let owner = 'A', generation = 1, online = false, scanServer = 0;
const savedCalls = [], searches = [], optionsReads = [], deferred = [], clearCalls = [];
const savedValues = new Map([['A', [2212]], ['B', [2213]]]);
const saveDeferred = new Map(), optionsDeferred = new Map(), searchDeferred = new Map();
const ownerKey = () => owner + ':' + generation;
const api = {
  savedServers: () => {
    const key = ownerKey();
    savedCalls.push(key);
    return saveDeferred.has(key) ? saveDeferred.get(key).promise : Promise.resolve(savedValues.get(owner) || []);
  },
  dataOptions: (serverId) => {
    const key = ownerKey();
    optionsReads.push({ key, serverId });
    return optionsDeferred.has(key) ? optionsDeferred.get(key).promise : Promise.resolve({
      serverId, counts:{city:serverId===2213?3:1}, names:{resource:[],monster:[]}, alliances:[], dispatchLevels:[], treasureTypes:[], rewardItems:{},
    });
  },
  search: (kind, query) => {
    const key = ownerKey();
    searches.push({key, server:query.serverId});
    return searchDeferred.has(key) ? searchDeferred.get(key).promise : Promise.resolve({rows:[],total:0});
  },
  clear: async (serverId, clearSavedServer) => {
    clearCalls.push({serverId,clearSavedServer});
    savedValues.set(owner, (savedValues.get(owner) || []).filter((id) => id !== serverId));
    return {serverId:scanServer,isReading:false,phase:'idle',selectedTypes:['city'],scanMode:'normal'};
  },
  listPlunderJobs: async () => ({dispatchJobs:[],truckJobs:[]}),
};
const render = () => flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MapRoutePage, {mapApi:api, mapOwner:ownerKey(),
    bridgeMode:'native', backendAvailable:true,
    online,currentServerId:scanServer,gameTextOwner:ownerKey(), gameTextRoot:'',
    scanState:{serverId:scanServer,isReading:false,phase:'idle',selectedTypes:['city'],scanMode:'normal'},
    summary:{serverId:scanServer,counts:{city:0}},onState(){},
    onCounts:(serverId,counts)=>deferred.push({owner:ownerKey(),serverId,counts}) }))));
const selected = () => document.querySelector('[data-map-saved-server]')?.value;
const selections = () => [...document.querySelectorAll('[data-map-saved-server] option')].map((option) => option.value);
const switchOwner = async (id) => { owner = id; generation += 1; await render(); };

await render(); await flush();
assert.equal(selected(), '2212');
assert.deepEqual(selections(), ['2212']);
await switchOwner('B'); await flush();
assert.equal(selected(), '2213');
assert.deepEqual(selections(), ['2213']);
results.push('same API A/B stopped owners isolate servers');

await switchOwner('A'); await flush();
assert.equal(selected(), '2212');
assert.ok(savedCalls.includes('A:3'));
results.push('A/B/A rereads profile A without old cache');

// Reuse the same positive server on both profiles: no data-options from the
// retired owner may publish counts into App or mutate successor rows.
savedValues.set('B', [2212]);
const heldBOptions = delayed(), heldBSearch = delayed();
optionsDeferred.set('B:4', heldBOptions);
searchDeferred.set('B:4', heldBSearch);
await switchOwner('B'); // B:4 requests are held from their first invocation
await flush();
assert.equal(selected(), '2212');
assert.ok(optionsReads.some((call) => call.key === 'B:4' && call.serverId === 2212));
assert.ok(searches.some((call) => call.key === 'B:4' && call.server === 2212));
// Stage a pending request for the current B owner and retire it.
await switchOwner('A');
assert.equal(selected(), '2212');
const beforeLateCounts = deferred.length;
await flush(() => heldBOptions.resolve({serverId:2212, counts:{city:999},
  names:{resource:[],monster:[]},alliances:[],dispatchLevels:[],treasureTypes:[]}));
await flush(() => heldBSearch.resolve({rows:[{recordKey:'obsolete',x:1,y:1}],total:1}));
assert.equal(deferred.length, beforeLateCounts, 'retired options must not call App onCounts');
assert.ok(!document.body.textContent.includes('obsolete'));
results.push('same-server old options/search responses cannot publish to successor');

// Held savedServers requests must clear the previous selection immediately,
// and both eventual completion and rejection are retired by generation.
const heldSavedB = delayed();
saveDeferred.set('B:6', heldSavedB);
await switchOwner('B'); // B:6
assert.equal(selected(), undefined, 'pending B read must not display A server');
assert.deepEqual(selections(), []);
await switchOwner('A'); // A:7
await flush(() => heldSavedB.resolve([9932]));
assert.equal(selected(), '2212');
assert.ok(!document.body.textContent.includes('9932'));
results.push('held retired saved list success fenced');

const heldRejectedB = delayed();
saveDeferred.set('B:8', heldRejectedB);
await switchOwner('B'); // B:8
assert.equal(selected(), undefined);
await switchOwner('A'); // A:9
await flush(() => heldRejectedB.reject(new Error('obsolete unavailable')));
assert.equal(selected(), '2212');
results.push('held rejected saved list fenced');

savedValues.set('B', []);
await switchOwner('B'); // B:10
await flush();
assert.equal(selected(), undefined);
assert.equal(document.querySelector('[data-map-saved-server]'), null);
results.push('empty B store has no phantom server');

const failed = delayed();
saveDeferred.set('A:11', failed);
await switchOwner('A'); // A:11
assert.equal(selected(), undefined);
await flush(() => failed.reject(new Error('current owner store unavailable')));
assert.equal(selected(), undefined);
results.push('current owner failed read stays empty, no silent fallback');

saveDeferred.delete('A:11');
await switchOwner('B'); // B:12
await switchOwner('A'); // A:13
assert.equal(selected(), '2212');
online=true;scanServer=2212;
await render();await flush();
assert.equal(selected(), '2212',
  'MAP014 preserves the current owner saved selector while connected');
savedValues.set('A',[2212,2213]);
scanServer=2213; await render(); await flush();
assert.deepEqual(selections(),['2212','2213']);
const serverSelect=document.querySelector('[data-map-saved-server]');
await flush(()=>{serverSelect.value='2213';serverSelect.dispatchEvent(new window.Event('change',{bubbles:true}));});
scanServer=2212; await render(); await flush();
assert.equal(selected(),'2213','Auto returning home erased the remote dataset selection');
assert.equal(searches.at(-1)?.server,2213,
  'Auto returned home but the selected remote server did not reach the real row search');
assert.ok(optionsReads.some((call)=>call.key==='A:13'&&call.serverId===2213),
  'Remote saved rows must load their own source-server options');
const remoteCityCount = () => document.querySelector('.map-tabs button .map-tab-count')?.textContent;
assert.equal(remoteCityCount(),'3','retained remote dataset must display its own true counts');
await render();await flush();
assert.equal(remoteCityCount(),'3','another home-server summary poll must not blank remote server counts');
const autoTab = document.querySelectorAll('.map-scan-tabs button')[1];
await flush(()=>autoTab.click());
const clearButton = document.querySelector('.map-auto-scan-options > button:last-of-type');
assert.ok(clearButton && !clearButton.disabled, 'Auto Clear must remain usable for the selected remote data');
await flush(()=>clearButton.click());await flush();
assert.ok(deferred.some((write)=>write.owner==='A:13'&&write.serverId===2213&&write.counts?.city===0),
  'Auto Clear must report zero for the cleared remote server, not the current home server');
assert.deepEqual(clearCalls.at(-1),{serverId:2213,clearSavedServer:true},
  'Auto must explicitly authorize stored-server Clear at the native bridge boundary');
assert.equal(selected(),'2212','Auto Clear must switch to the preserved home dataset');
assert.deepEqual(selections(),['2212'],'Auto Clear must refresh saved-server options after deletion');
results.push('Auto Clear deletes only selected remote source, refreshes saved selector and preserves home');
savedValues.set('A',[2212]);
online=false;scanServer=0;
await render();await flush();
assert.equal(selected(), '2212');
results.push('offline / online / offline transition retains current owner only');

await flush(() => reactRoot.unmount());dom.window.close();
console.log('MAP011_SAVED_OWNER_MOUNTED_PASS', JSON.stringify({results,savedCalls,optionsReads:optionsReads.length,searches:searches.length,oldAppCountWritesBlocked:true}));
