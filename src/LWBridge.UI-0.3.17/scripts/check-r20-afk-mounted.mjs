// Inert mounted production screen with a native-shaped transport. No game.
import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), { build } = req("esbuild"), React = req("react");
const dom = new JSDOM('<div id="root"></div>', { url: "http://127.0.0.1/" });
for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
window.HTMLElement.prototype.scrollIntoView = () => {};
let tick;
window.setInterval = (fn) => { tick = fn; return 1; }; window.clearInterval = () => { tick = undefined; };
localStorage.setItem("lwbridge.language", "en");
let handler;
const calls = [], profiles = new Map();
const realNow = Date.now; let clock = realNow(); Date.now = () => clock;
let getStatusFailures = 1, catalogFailures = 1;
let runtimeResult = { running: false, state: "idle", rows: [], processed: {} };
const empty = () => ({ enabled: false, strategies: [], allianceDrill: { enabled: false, activeRally: false, squadIndexes: [], joinRestrictions: { enabled: false, mode: "delay", slotRange: [2, 2], slotDelaySeconds: 0, delaySeconds: [1, 3], maxWaitMinutes: 5, leaderListMode: "off", leaders: [], skipSoloLeader: false, skipKicked: true } } });
window.__LWBridgeBootstrap = { mode: "live", sessionId: "afk-mounted", profiles: { selectedProfileId: "A" } };
window.chrome = { webview: {
  addEventListener(name, fn) { if (name === "message") handler = fn; }, removeEventListener() {},
  postMessage(message) {
    if (message.kind !== "invoke") return;
    calls.push(message); const p = message.payload.profileId;
    if (message.command === "get_status" && getStatusFailures > 0) {
      getStatusFailures -= 1;
      queueMicrotask(() => handler({ data: { kind: "response", sessionId: "afk-mounted", id: message.id, ok: false, error: { code: "GAME_CONNECTION_UNAVAILABLE", message: "RAW_CONFIG_FAILURE" } } }));
      return;
    }
    if (message.command === "monster_catalog_options" && catalogFailures > 0) {
      catalogFailures -= 1;
      queueMicrotask(() => handler({ data: { kind: "response", sessionId: "afk-mounted", id: message.id, ok: false, error: { code: "GAME_RESPONSE_TIMEOUT", message: "RAW_CATALOG_FAILURE" } } }));
      return;
    }
    let result = {};
    if (message.command === "get_status") result = { config: { tasks: { monsterSweep: profiles.get(p) || empty(), staminaPotion: { enabled: false, minStamina: 10, preferFifty: false } } } };
    if (message.command === "feature018_afk_status") result = structuredClone(runtimeResult);
    if (message.command === "monster_catalog_options") result = { unlockedSquads: [1], options: [{ key: "native-zombie", name: "Native Zombie", monsterNameKey: "native-zombie", monsterType: 2, monsterSpecial: 0, monsterIds: [], group: "normal", source: "search", action: "attack", rally: false, searchable: true, minLevel: 1, maxLevel: 30 }], allianceMembers: { ready: true, members: [], selfUid: "1001", online: true } };
    if (message.command === "monster_afk_config_save") { const { profileId, ...draft } = message.payload; profiles.set(p, structuredClone(draft)); result = draft; }
    queueMicrotask(() => handler({ data: { kind: "response", sessionId: "afk-mounted", id: message.id, ok: true, result } }));
  },
} };
const bundle = await build({ stdin: { contents: 'export { SquadsPage } from "./SquadsPage.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const loaded = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { SquadsPage, I18nProvider } = loaded.exports;
const { createRoot } = req("react-dom/client"); const mounted = createRoot(document.getElementById("root"));
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 40; i++) await Promise.resolve(); });
await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(SquadsPage, { profileId: "A", online: false, activeTab: "afk", key: "offline-afk" }))));
assert.equal(calls.length, 0, "Offline AFK must not poll native status/config/catalog");
let master = document.querySelector('.monster-afk-toolbar input[type="checkbox"]');
assert.ok(master?.disabled, "Offline AFK mutations must be disabled");
assert.match(document.body.textContent, /Game disconnected/);
await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(SquadsPage, { profileId: "A", online: false, activeTab: "equipment", key: "offline-equipment" }))));
const refresh = [...document.querySelectorAll("button")].find((button) => button.textContent === "Refresh");
assert.ok(refresh?.disabled, "Equipment page Refresh must use the real game connection state");
assert.equal(calls.length, 0, "Offline Equipment shell must not invoke native commands");

await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(SquadsPage, { profileId: "A", online: true, activeTab: "afk", key: "online-afk" }))));
assert.equal(document.querySelectorAll("[data-preview-fixture]").length, 0, "Production AFK must not expose preview fixture markers");
master = document.querySelector('.monster-afk-toolbar input[type="checkbox"]');
assert.ok(master?.disabled, "AFK mutations must stay disabled until persisted config hydrates");
const savesBeforeHydrate = calls.filter((c) => c.command === "monster_afk_config_save").length;
await flush(() => master.click());
assert.equal(calls.filter((c) => c.command === "monster_afk_config_save").length, savesBeforeHydrate, "A failed initial read must never overwrite persisted AFK config with defaults");
assert.doesNotMatch(document.body.textContent, /RAW_CONFIG_FAILURE|RAW_CATALOG_FAILURE/, "Native failure details must not leak into AFK UI text");
assert.equal(calls.filter((c) => c.command === "monster_catalog_options").length, 1);
await flush(() => tick());
assert.equal(calls.filter((c) => c.command === "monster_catalog_options").length, 1, "Failed catalogue reads must respect the 30s retry backoff");
clock += 30_001;
await flush(() => tick());
assert.ok(calls.some((c) => c.command === "get_status") && calls.some((c) => c.command === "monster_catalog_options"));
assert.equal(calls.filter((c) => c.command === "monster_catalog_options").length, 2, "Catalogue must retry after its backoff window");
assert.equal(document.querySelector(".monster-afk-layout").dataset.nativeConfigReady, "true");
assert.equal(document.querySelectorAll(".monster-afk-profile-card").length, 0);
assert.doesNotMatch(document.body.textContent, /Fixture Steel|Fixture Gold/);
const add = [...document.querySelectorAll("button")].find((b) => b.textContent === "Add");
assert.ok(add && !add.disabled); await flush(() => add.click());
const farm = document.querySelector('[role="menuitem"]'); assert.ok(farm && !farm.disabled);
await flush(() => farm.click());
assert.equal(document.querySelectorAll("[data-preview-fixture]").length, 0, "Production AFK editor must not expose preview fixture markers");
assert.match(document.querySelector(".monster-afk-profile-card").textContent, /Native Zombie/);
assert.deepEqual([...document.querySelectorAll(".monster-afk-squad-toggle")].map((b) => b.textContent), ["1"]);
master = document.querySelector('.monster-afk-toolbar input[type="checkbox"]');
assert.ok(master && !master.disabled); await flush(() => master.click());
assert.equal(profiles.get("A").enabled, true);
assert.equal(profiles.get("A").strategies[0].monsterNameKey, "native-zombie");
assert.ok(calls.some((c) => c.command === "feature018_afk_configure"));
await flush(() => master.click());
assert.equal(profiles.get("A").enabled, false);
const stop = calls.findLastIndex((c) => c.command === "automation_stop");
assert.ok(stop >= 0 && calls[stop + 1].command === "monster_afk_config_save");
const firstId = profiles.get("A").strategies[0].id;
await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(SquadsPage, { profileId: "A", online: true, activeTab: "afk", key: "fresh-mount" }))));
await flush(() => [...document.querySelectorAll("button")].find((b) => b.textContent === "Add").click());
await flush(() => document.querySelector('[role="menuitem"]').click());
await flush(() => document.querySelector('.monster-afk-toolbar input[type="checkbox"]').click());
const ids = profiles.get("A").strategies.map((p) => p.id);
assert.equal(ids.length, 2); assert.equal(new Set(ids).size, 2, "A remount must not reuse a saved AFK profile ID");
assert.ok(ids.includes(firstId));
await flush(() => document.querySelector('.monster-afk-toolbar input[type="checkbox"]').click());
await flush(() => mounted.unmount());
assert.equal(tick, undefined, "AFK polling must stop on unmount");
const failedConfig=profiles.get('A');failedConfig.allianceDrill={...failedConfig.allianceDrill,enabled:true,squadIndexes:[1]};
runtimeResult={running:false,state:'idle',rows:[],processed:{},allianceDrill:{state:'failed',running:false,
  step:'joining',lastError:'Rally join was not confirmed by a native march',rows:[{squadIndex:1,step:'joining',activityRole:'member'}]}};
const failedRoot=req('react-dom/client').createRoot(document.getElementById('root'));
await flush(()=>failedRoot.render(React.createElement(I18nProvider,{},React.createElement(SquadsPage,{profileId:'A',online:true,activeTab:'afk',key:'failed-drill'}))));
assert.ok(document.querySelector('[data-drill-runtime=failed]'),'Failed Drill is visible');
assert.doesNotMatch(document.body.textContent,/Joining a rally|Leading a rally/,'Stale pending rows must not describe a failed engine as joining');
await flush(()=>failedRoot.unmount());
Date.now = realNow;
dom.window.close();
console.log("FEATURE018_R29_AFK_MOUNTED_OK: offline gate, hydrate recovery, catalogue backoff, localized failures, native save/Start/Stop");
