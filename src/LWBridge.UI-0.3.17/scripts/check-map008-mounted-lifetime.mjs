import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

// Actual mounted production component and callbacks with inert held native operations.
// No action or game request is issued. Original lead negatives remain immutable.
const here = path.dirname(fileURLToPath(import.meta.url));
const rootPath = path.resolve(here, "..");
const uiReq = createRequire(path.join(rootPath, "package.json"));
const { JSDOM } = uiReq("jsdom");
const { build } = uiReq("esbuild");
const React = uiReq("react");
const { createRoot } = uiReq("react-dom/client");
const entry = path.join(rootPath, "src");
const component = fs.readFileSync(path.join(entry, "MapDataPage.jsx"));
const sourceSha256 = crypto.createHash("sha256").update(component).digest("hex");
const bundle = await build({
  stdin: { contents: 'export {MapDataPage} from "./MapDataPage.jsx"; export {I18nProvider} from "./i18n.jsx";', resolveDir: entry, loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const mod = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(uiReq, mod, mod.exports);
const { MapDataPage, I18nProvider } = mod.exports;
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 20; i++) await Promise.resolve(); });
function held() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
const now = Date.now();
const ghost = { serverId: 2212, ownerServerId: 2212, uuid: "9090909", ownerUid: "200", ownerName: "Held Ghost",
  completionTime: now - 60000, plunderAt: now - 5000, taskExpireTime: now + 3600000,
  stolenCount: 0, maxStealCount: 2, x: 90, y: 93, quality: "ur" };
const results = [];
for (const [language, theme, narrow] of [["en", "light", false], ["ja", "dark", true]]) {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: "http://127.0.0.1/map008" });
  for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
    globalThis[key] = key === "window" ? dom.window : dom.window[key];
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
  globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  window.matchMedia = () => ({ matches: narrow, addListener() {}, removeListener() {} });
  Object.defineProperty(window, "innerWidth", { configurable: true, value: narrow ? 390 : 1280 });
  localStorage.setItem("lwbridge.language", language);
  localStorage.setItem("lwbridge.theme", theme);
  document.documentElement.dataset.theme = theme;
  const timers = new Set();
  window.setInterval = (fn, delay) => { const id = { fn, delay }; timers.add(id); return id; };
  window.clearInterval = (id) => timers.delete(id);
  window.setTimeout = () => 1;
  window.clearTimeout = () => {};
  let serverId = 2212;
  let previewRequests = [], ghostRequests = [];
  const makeApi = (name) => ({
    dataOptions: async (id) => ({ serverId: id, counts: { ghost: 1, treasure: 1 }, names: { resource: [], monster: [] },
      alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [], rewardItems: {} }),
    search: async (kind, query) => ({ rows: kind === "ghost" ? [{ ...ghost, serverId: query.serverId }] : [], total: kind === "ghost" ? 1 : 0 }),
    listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
    previewTreasureClaim: (id, scope, priority, uuid) => {
      const request = held(); previewRequests.push({ ...request, owner: name, id, scope, priority, uuid }); return request.promise;
    },
    prepareGhostPlunder: (rows, delay) => {
      const request = held(); ghostRequests.push({ ...request, owner: name, rows, delay }); return request.promise;
    },
  });
  let api = makeApi("A");
  const domRoot = createRoot(document.getElementById("root"));
  const container = document.getElementById("root");
  const element = () => React.createElement(I18nProvider, { key: language }, React.createElement(MapDataPage, {
    mapApi: api, bridgeMode: "native", backendAvailable: true, online: true,
    currentServerId: serverId, onState() {}, onCounts() {}, gameTexts: {},
    scanState: { serverId, serverIdSource: "live", isReading: false, phase: "idle", selectedTypes: ["ghost", "treasure"], scanMode: "normal" },
    summary: { serverId, counts: { ghost: 1, treasure: 1 } },
  }));
  const rerender = () => flush(() => domRoot.render(element()));
  const tabs = () => [...container.querySelectorAll(".map-tabs [role=tab]")];
  const click = (element) => flush(() => {
    assert.ok(element, "mounted control missing");
    assert.equal(element.disabled, false, "mounted control unexpectedly disabled: " + element.textContent);
    element.dispatchEvent(new MouseEvent("click", { bubbles: true }));
  });
  const goto = (kind) => click(tabs()[{city:0, ghost:6, treasure:7}[kind]]);
  const actionButtons = () => [...container.querySelectorAll(".map-searchbar .map-schedule-button")];
  const message = () => container.querySelector(".map-claim-result")?.textContent || "";
  await rerender();
  await goto("treasure");
  const beginTreasure = async () => { await click(actionButtons()[0]); return previewRequests.at(-1); };
  const old = await beginTreasure();
  assert.equal(actionButtons()[0].disabled, true, "pending preview must remain busy");
  await goto("city");
  await goto("treasure");
  assert.equal(actionButtons()[0].disabled, false, "LEADMAP007-01: tab retirement must free busy state");
  const newer = await beginTreasure();
  await flush(() => old.resolve({ candidateCount: 99, eligibleCount: 99 }));
  assert.equal(actionButtons()[0].disabled, true, "old completion cleared successor busy flag");
  assert.equal(message(), "", "LEADMAP007-02: old reply resurfaced on replacement view");
  await flush(() => newer.resolve({ candidateCount: 4, eligibleCount: 2 }));
  assert.equal(actionButtons()[0].disabled, false);
  assert.ok(message(), "new preview must report result");
  results.push({ language, theme, case: "treasure leave/return, successor overlap, old success", passed: true });

  const oldApi = await beginTreasure();
  api = makeApi("B");
  await rerender();
  assert.equal(actionButtons()[0].disabled, false, "old API owner busy state survived replacement");
  const nextApi = await beginTreasure();
  await flush(() => oldApi.reject(new Error("OBSOLETE_API")));
  assert.equal(actionButtons()[0].disabled, true);
  assert.ok(!message().includes("OBSOLETE_API"));
  await flush(() => nextApi.resolve({ candidateCount: 3, eligibleCount: 1 }));
  results.push({ language, theme, case: "API/profile replacement, rejection and successor", passed: true });

  const oldServer = await beginTreasure();
  serverId = 2213;
  await rerender();
  for (let i = 0; i < 4; i++) await flush();
  assert.equal(actionButtons()[0].disabled, false, "server replacement did not release busy state");
  const nextServer = await beginTreasure();
  assert.equal(nextServer.id, 2213, "new owner did not use new browse server");
  await flush(() => oldServer.resolve({ candidateCount: 999, eligibleCount: 999 }));
  assert.equal(actionButtons()[0].disabled, true, "server old completion cleared successor");
  await flush(() => nextServer.resolve({ candidateCount: 1, eligibleCount: 1 }));
  results.push({ language, theme, case: "server ownership and stale success", passed: true });

  await goto("ghost");
  const selectGhost = async () => {
    for (let i = 0; i < 3; i++) await flush();
    const selected = container.querySelector(".map-table--ghost tbody input[type=checkbox]");
    assert.ok(selected && !selected.disabled, "mounted Ghost selection missing");
    await flush(() => selected.dispatchEvent(new MouseEvent("click", { bubbles: true })));
  };
  await selectGhost();
  const beginGhost = async () => { await click(actionButtons()[0]); return ghostRequests.at(-1); };
  const oldGhost = await beginGhost();
  await goto("city");
  await flush(() => oldGhost.reject(new Error("OBSOLETE_GHOST")));
  assert.equal(message(), "");
  await goto("ghost");
  await selectGhost();
  const newGhost = await beginGhost();
  assert.equal(newGhost.rows[0].taskKind, "ghost");
  await flush(() => newGhost.resolve([ghost]));
  assert.ok(message(), "current Ghost preparation must report result");
  results.push({ language, theme, case: "Ghost held rejection, tab retirement, owned successor", passed: true });
  await flush(() => domRoot.unmount());
  assert.equal(timers.size, 0, "Map lifetime leaked timers");
  dom.window.close();
}
const report = { status: "PASS", sourceSha256, mounted: "MapDataPage.jsx + I18nProvider / React jsdom, inert held providers", genuineGame: false,
  externalGameActions: 0, cases: results };
const target = process.argv[2];
if (target) fs.writeFileSync(path.resolve(target), JSON.stringify(report, null, 2) + "\n");
console.log(`MAP008_MOUNTED_LIFETIME_PASS cases=${results.length} sha=${sourceSha256}`);
