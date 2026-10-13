import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parseEscortMillions } from "../src/mapOwnerFilters.js";

assert.deepEqual(parseEscortMillions("25.5"), { valid: true, value: 25.5 });
assert.deepEqual(parseEscortMillions(""), { valid: true, value: null });
for (const invalid of ["-1", "25e1", "Infinity", "NaN", " ", "1000000001"])
  assert.equal(parseEscortMillions(invalid).valid, false, `invalid cap ${invalid}`);

const rootPath = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(rootPath, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
let createRoot;
const uiRoot = path.join(rootPath, "src");
const bundle = await build({ stdin: {
  contents: 'export {MapDataPage} from "./MapDataPage.jsx"; export {I18nProvider} from "./i18n.jsx";',
  resolveDir: uiRoot, loader: "jsx" }, bundle: true, write: false, platform: "browser",
  format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], logLevel: "silent" });
const loaded = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { MapDataPage, I18nProvider } = loaded.exports;
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 24; i++) await Promise.resolve(); });
const outcomes = [];
for (const [language, theme, width] of [["en", "light", 1280], ["ja", "dark", 375]]) {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: "http://127.0.0.1/map014" });
  for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
    globalThis[key] = key === "window" ? dom.window : dom.window[key];
  Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
  globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  // Initialize React's delegated change event plugin with a real JSDOM
  // document, otherwise input fields register only inert early feature tests.
  if (!createRoot) ({ createRoot } = req("react-dom/client"));
  window.matchMedia = () => ({ matches: width < 400, addListener() {}, removeListener() {} });
  Object.defineProperty(window, "innerWidth", { configurable: true, value: width });
  localStorage.setItem("lwbridge.language", language);
  localStorage.setItem("lwbridge.theme", theme);
  document.documentElement.dataset.theme = theme;
  const timers = new Map(); let timerId = 0;
  window.setInterval = (callback, delay) => { const id = ++timerId; timers.set(id, { callback, delay }); return id; };
  window.clearInterval = (id) => timers.delete(id);
  window.setTimeout = () => 1;
  window.clearTimeout = () => {};
  const queries = [], clears = [], follows = [];
  const state = { serverId: 2212, serverIdSource: "live", isReading: false, phase: "idle", selectedTypes: ["resource", "truck"], scanMode: "normal" };
  const rewards = [{ key: "reward:1:100", name: "Genuine Item A", iconPath: null }, { key: "reward:1:500", name: "Genuine Item B", iconPath: null }];
  const api = {
    dataOptions: async (serverId) => ({ serverId, counts: { resource: 2, truck: 1 }, names: { resource: [], monster: [] },
      alliances: [], noAllianceCount: 0, dispatchLevels: [], treasureTypes: [], rewardItems: { truck: rewards, railway: [] } }),
    search: async (kind, query) => { queries.push({ kind, query: structuredClone(query) }); return { rows: kind === "truck"
      ? [{ uuid: "t-1", serverId: 2212, marchUuid: "1234567", arriveTs: Date.now() + 3600000,
        quality: 5, power: 25000000, currentGoods: rewards.map((item) => ({ ...item, count: 2 })) }] : [],
      total: kind === "truck" ? 1 : 0 }; },
    listPlunderJobs: async () => ({ dispatchJobs: [], truckJobs: [] }),
    marchFollow: async (row) => { follows.push(`${row.serverId}:${String(row.marchUuid)}`); },
    scanStatus: async () => state,
    clear: async (serverId) => { clears.push(serverId); return state; },
  };
  let autoRunning = false;
  const root = createRoot(document.getElementById("root"));
  const render = () => flush(() => root.render(React.createElement(I18nProvider, { key: language }, React.createElement(MapDataPage,
    { mapApi: api, bridgeMode: "native", backendAvailable: true, online: true, currentServerId: 2212,
      onState() {}, onCounts() {}, scanState: state, summary: { serverId: 2212, counts: { resource: 2, truck: 1 } },
      autoScanRunning: autoRunning, gameTexts: {} }))));
  const click = (el) => flush(() => { assert.ok(el, "mounted control missing"); assert.equal(el.disabled, false);
    el.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  const change = (el, value) => flush(() => { assert.ok(el, "mounted field missing");
    const proto = el.tagName === "INPUT" ? window.HTMLInputElement.prototype : window.HTMLSelectElement.prototype;
    Object.getOwnPropertyDescriptor(proto, "value").set.call(el, value);
    el.dispatchEvent(new window.Event("input", { bubbles: true })); el.dispatchEvent(new window.Event("change", { bubbles: true })); });
  const goto = (index) => click(document.querySelectorAll(".map-tabs [role=tab]")[index]);
  const search = () => click(document.querySelector(".map-searchbar > button"));
  const field = (name) => [...document.querySelectorAll(".map-searchbar [aria-label]")]
    .find((el) => el.getAttribute("aria-label")?.includes(name));
  await render(); await goto(1);
  const labels = language === "en" ? ["Min level", "Max level", "Resource amount", "Tile terrain"]
    : ["最低レベル", "最高レベル", "資源残量", "タイル地形"];
  await change(field(labels[0]), "7"); await change(field(labels[1]), "9");
  await change(field(labels[2]), "partial"); await change(field(labels[3]), "black"); await search();
  for (const [key, value] of Object.entries({ minLevel: 7, maxLevel: 9, resourceAmountState: "partial", resourceTileState: "black" }))
    assert.equal(queries.at(-1).query[key], value, `mounted Resource ${key}`);
  await goto(3);
  await click(document.querySelectorAll(".map-item-filter-menu button")[1]);
  await click(document.querySelectorAll(".map-item-filter-menu button")[2]);
  assert.match(document.querySelector(".map-item-filter summary").textContent, /\(2\)/);
  await change(field(language === "en" ? "Max escort power" : "護衛戦力"), "25.5"); await search();
  assert.deepEqual(queries.at(-1).query.itemKeys, ["reward:1:100", "reward:1:500"]);
  assert.equal(queries.at(-1).query.escortPowerMaxMillions, 25.5);
  await click(document.querySelector(".map-coordinate-button"));
  assert.equal(follows.at(-1), "2212:1234567", "mounted follow uses stable native identity");
  assert.ok([...timers.values()].some((entry) => entry.delay === 3000), "follow not started");
  await click(document.querySelector(".map-coordinate-button"));
  assert.ok(![...timers.values()].some((entry) => entry.delay === 3000), "Stop Follow retained poll");
  await click(document.querySelectorAll(".map-item-filter-menu button")[1]); await search();
  assert.deepEqual(queries.at(-1).query.itemKeys, ["reward:1:500"]);
  await click(document.querySelectorAll(".map-item-filter-menu button")[0]); await search();
  assert.equal(queries.at(-1).query.itemKeys, undefined, "clear did not remove reward constraints");
  await click(document.querySelectorAll(".map-scan-tabs [role=tab]")[1]);
  await click(document.querySelector(".map-auto-scan-options button:last-of-type"));
  assert.deepEqual(clears, [2212], "Auto Clear did not call canonical command");
  autoRunning = true; await render();
  assert.equal(document.querySelector(".map-auto-scan-options button:last-of-type").disabled, true);
  await flush(() => root.unmount());
  assert.equal(document.documentElement.dataset.theme, theme);
  outcomes.push({ language, theme, width, mountedQueries: queries.length, cleared: clears.length,
    followed: follows.length, remainingTimers: timers.size });
  dom.window.close();
}
console.log("MAP014_MOUNTED_OWNER_FILTER_FOLLOW_CLEAR_OK", JSON.stringify(outcomes));
