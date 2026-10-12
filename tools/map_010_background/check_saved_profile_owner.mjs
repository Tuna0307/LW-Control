// Lead distinguishing inverse. Controlled, no game/desktop operations.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";


// Mount the real MapDataPage + native callbacks with controlled locale responses.
// Game binary translation values are tested separately by the Desktop checker.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../src/LWBridge.UI-0.3.17");
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

let owner = 'A';
const savedCalls = [], searches = [];
const api = {
  savedServers: async () => { savedCalls.push(owner); return owner === 'A' ? [2212] : [2213]; },
  dataOptions: async (serverId) => ({serverId, counts:{city:1}, names:{}, alliances:[], dispatchLevels:[], treasureTypes:[], rewardItems:{}}),
  search: async (kind, query) => { searches.push({owner, server:query.serverId}); return {rows:[],total:0}; },
  listPlunderJobs: async () => ({dispatchJobs:[],truckJobs:[]}),
};
const render = () => flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MapRoutePage, {mapApi:api, bridgeMode:'native', backendAvailable:true,
    online:false,currentServerId:0,gameTextOwner:owner, gameTextRoot:'',
    scanState:{serverId:0,isReading:false,phase:'idle',selectedTypes:['city'],scanMode:'normal'},
    summary:{serverId:0,counts:{city:0}},onState(){},onCounts(){} }))));
await render(); await flush(); await flush();
const first=document.querySelector('[data-map-saved-server]')?.value;
owner='B'; await render(); await flush(); await flush();
const second=document.querySelector('[data-map-saved-server]')?.value;
console.log(JSON.stringify({first, second, savedCalls, searches, expectation:'B must load its own saved server 2213', observedDefect:second !== '2213'}));
await flush(() => reactRoot.unmount());dom.window.close();

assert.equal(second, "2213", "same API profile replacement must discover B saved servers instead of retaining A");
