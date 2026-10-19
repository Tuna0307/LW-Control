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
window.setInterval = () => 1; window.clearInterval = () => {};
localStorage.setItem("lwbridge.language", "en");
let handler;
const calls = [], profiles = new Map();
const empty = () => ({ enabled: false, strategies: [], allianceDrill: { enabled: false, activeRally: false, squadIndexes: [], joinRestrictions: { enabled: false, mode: "delay", slotRange: [2, 2], slotDelaySeconds: 0, delaySeconds: [1, 3], maxWaitMinutes: 5, leaderListMode: "off", leaders: [], skipSoloLeader: false, skipKicked: true } } });
window.__LWBridgeBootstrap = { mode: "live", sessionId: "afk-mounted", profiles: { selectedProfileId: "A" } };
window.chrome = { webview: {
  addEventListener(name, fn) { if (name === "message") handler = fn; }, removeEventListener() {},
  postMessage(message) {
    if (message.kind !== "invoke") return;
    calls.push(message); const p = message.payload.profileId;
    let result = {};
    if (message.command === "get_status") result = { config: { tasks: { monsterSweep: profiles.get(p) || empty() } } };
    if (message.command === "feature018_afk_status") result = { running: false, state: "idle", rows: [], processed: {} };
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
await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(SquadsPage, { profileId: "A" }))));
assert.ok(calls.some((c) => c.command === "get_status") && calls.some((c) => c.command === "monster_catalog_options"));
assert.equal(document.querySelectorAll(".monster-afk-profile-card").length, 0);
assert.doesNotMatch(document.body.textContent, /Fixture Steel|Fixture Gold/);
const add = [...document.querySelectorAll("button")].find((b) => b.textContent === "Add");
assert.ok(add && !add.disabled); await flush(() => add.click());
const farm = document.querySelector('[role="menuitem"]'); assert.ok(farm && !farm.disabled);
await flush(() => farm.click());
assert.match(document.querySelector(".monster-afk-profile-card").textContent, /Native Zombie/);
assert.deepEqual([...document.querySelectorAll(".monster-afk-squad-toggle")].map((b) => b.textContent), ["1"]);
const master = document.querySelector('.monster-afk-toolbar input[type="checkbox"]');
assert.ok(master && !master.disabled); await flush(() => master.click());
assert.equal(profiles.get("A").enabled, true);
assert.equal(profiles.get("A").strategies[0].monsterNameKey, "native-zombie");
assert.ok(calls.some((c) => c.command === "automation_start" && c.payload.taskName === "monsterSweep"));
await flush(() => master.click());
assert.equal(profiles.get("A").enabled, false);
const stop = calls.findLastIndex((c) => c.command === "automation_stop");
assert.ok(stop >= 0 && calls[stop + 1].command === "monster_afk_config_save");
await flush(() => mounted.unmount());
dom.window.close();
console.log("FEATURE018_R20_AFK_MOUNTED_OK: native catalogue/squads, real profile save, master Start/Stop, no fixture fallback");
