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
const api = {
  dataOptions: async (serverId) => ({ serverId, counts: { resource: 1, monster: 1 },
    names: { resource: [{ key: keys[0], count: 1 }], monster: [{ key: keys[1], count: 1 }] },
    alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [], rewardItems: {} }),
  search: async (kind) => ({ rows: kind === "resource" ? [{ recordKey: "r", resourceNameKey: keys[0], x: 1, y: 2 }] :
    kind === "monster" ? [{ recordKey: "m", monsterNameKey: keys[1], x: 3, y: 4 }] : [],
    total: ["resource", "monster"].includes(kind) ? 1 : 0 }),
  listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
};
let activeOwner = "profile-A", language = "en", source = "23555";
let selectedRootStatus = { valid: true, root: rootPath, source: "saved", candidates: [] };
let delayBatch = false;
const requests = [];
const loadGameTexts = (payload) => {
  const request = { ...delayed(), payload, owner: activeOwner, source };
  requests.push(request);
  if (!delayBatch || payload.keys.length === 0) {
    request.resolve({ language: payload.language, gameRoot: rootPath, buildId: source,
      localeSha256: source + payload.language, englishSha256: source + "en",
      texts: Object.fromEntries(payload.keys.map((key) => [key, names[payload.language][keys.indexOf(key)] || key])) });
  }
  return request.promise;
};
const render = async () => flush(() => reactRoot.render(React.createElement(I18nProvider, { key: language },
  React.createElement(MapRoutePage, { mapApi: api, bridgeMode: "native", backendAvailable: true,
    online: true, currentServerId: 2212, gameTextLoader: loadGameTexts, gameTextOwner: activeOwner,
    gameTextRoot: selectedNativeGameTextRoot(selectedRootStatus), onState() {}, onCounts() {},
    scanState: { serverId: 2212, isReading: false, phase: "idle", selectedTypes: ["resource", "monster"], scanMode: "normal" },
    summary: { serverId: 2212, counts: { resource: 1, monster: 1 } },
  }))));
const tab = (index) => flush(() => element.querySelectorAll(".map-tabs [role=tab]")[index].dispatchEvent(new MouseEvent("click", { bubbles: true })));
const filter = () => element.querySelector(".map-searchbar select[aria-label]")?.textContent || "";
const rowText = () => element.querySelector(".map-table tbody")?.textContent || "";
await render();
await tab(1);
for (let i = 0; i < 3; i++) await flush();
assert.ok(filter().includes(names.en[0]), "production filter should use English localized name");
assert.ok(rowText().includes(names.en[0]), "production Resource column should resolve name");
await tab(2);
assert.ok(filter().includes(names.en[1]) && rowText().includes(names.en[1]), "production Monster table and filter");
assert.ok(requests.every((request) => request.payload.keys.length <= 200), "native request bound");
results.push("EN resource/monster tables and filters");

delayBatch = true;
activeOwner = "profile-B";
await render();
await flush();
const pending = requests.filter((request) => request.owner === "profile-B" && request.payload.keys.length);
assert.ok(pending.length > 0, "replacement profile must request its own names");
activeOwner = "profile-C";
delayBatch = false;
await render();
assert.ok(rowText().includes(names.en[1]), "successor names must appear");
await flush(() => pending.forEach((request) => request.resolve({ language: "en", gameRoot: rootPath,
  buildId: "invalid-old", localeSha256: "old", englishSha256: "old", texts: { [keys[1]]: "OBSOLETE" } })));
assert.ok(!element.textContent.includes("OBSOLETE"), "retired profile response leaked into current view");
results.push("stale profile reply rejected after successor");

language = "ja";
localStorage.setItem("lwbridge.language", "ja");
await render();
await tab(2);
for (let i = 0; i < 3; i++) await flush();
assert.ok(filter().includes(names.ja[1]) && rowText().includes(names.ja[1]),
  `Japanese name after language change: filter=${filter()} row=${rowText()} locale=${localStorage.getItem("lwbridge.language")} requests=${JSON.stringify(requests.slice(-5).map((r) => r.payload))}`);
results.push("JA mounted table and filter after language change");
selectedRootStatus = { valid: false, root: rootPath };
await render();
assert.ok(element.textContent.includes("Game names unavailable: current game installation is not resolved"),
  "Missing native selected game root must show an explicit Map localization failure");
selectedRootStatus = { valid: true, root: rootPath };
await render();
assert.ok(rowText().includes(names.ja[1]), "restored game root must recover current localized Monster rows");
results.push("native root status through actual Map route, invalid root rejection");

await flush(() => reactRoot.unmount());
dom.window.close();
const report = { status: "PASS", mounted: "MapRoutePage + MapDataPage + I18nProvider with native root-status mapping", controlled: true,
  calls: requests.length, maxKeys: Math.max(...requests.map((request) => request.payload.keys.length)), cases: results,
  externalGameActions: 0 };
if (process.argv[2]) fs.writeFileSync(path.resolve(process.argv[2]), JSON.stringify(report, null, 2) + "\n");
console.log("MAP008_MOUNTED_NATIVE_NAMES_PASS " + JSON.stringify(report));
