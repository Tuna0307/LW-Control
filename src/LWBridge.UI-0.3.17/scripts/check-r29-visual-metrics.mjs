import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../src");
const req = createRequire(path.resolve(here, "../package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const { createRoot } = req("react-dom/client");

const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: "http://127.0.0.1/?view=overview",
});
for (const key of [
  "window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent",
  "KeyboardEvent", "Node", "MutationObserver", "localStorage",
]) globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("R29 visual metrics check forbids external network access"); };
window.matchMedia = () => ({ matches: true });
localStorage.setItem("lwbridge.language", "en");
localStorage.setItem("lwbridge.theme", "light");
document.documentElement.dataset.theme = "light";

const intervals = new Map();
const timeouts = new Map();
let timerId = 0;
window.setInterval = (fn, delay) => { intervals.set(++timerId, { fn, delay }); return timerId; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = (fn, delay) => { timeouts.set(++timerId, { fn, delay }); return timerId; };
window.clearTimeout = (id) => timeouts.delete(id);

const calls = [];
const listeners = [];
let generation = 1;
let invokeHandler = async (command) => { throw new Error(`Unexpected R29 visual metrics command: ${command}`); };
const bridge = {
  mode: "native",
  available: true,
  profileId: "profile-a",
  setSelectedProfile(id) {
    if (id !== this.profileId) generation += 1;
    this.profileId = id;
    return this.currentProfileOwner();
  },
  currentProfileOwner() { return { profileId: this.profileId, generation }; },
  isCurrentProfileOwner(owner) {
    return owner?.profileId === this.profileId && owner?.generation === generation;
  },
  listen(eventName, fn) {
    const state = { eventName, fn, active: true };
    listeners.push(state);
    return () => { state.active = false; };
  },
  invokeProfileScoped(command, args = {}) {
    const payload = { ...args, profileId: this.profileId };
    calls.push({ command, args: payload, scoped: true });
    return Promise.resolve().then(() => invokeHandler(command, payload));
  },
  invoke(command, args = {}) {
    calls.push({ command, args, scoped: false });
    return Promise.resolve().then(() => invokeHandler(command, args));
  },
};

globalThis.__r29VisualMetrics = { bridge };
const bundle = await build({
  stdin: {
    contents: 'export { SettingsPage } from "./SettingsPage.jsx"; export { App } from "./App.jsx"; export { I18nProvider } from "./i18n.jsx";',
    resolveDir: src,
    loader: "jsx",
  },
  bundle: true,
  write: false,
  platform: "browser",
  format: "cjs",
  jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" },
  logLevel: "silent",
  plugins: [{
    name: "r29-visual-metrics-native-transport",
    setup(builder) {
      builder.onLoad({ filter: /backendBridge\.js$/ }, () => ({
        contents: "export const backendBridge=globalThis.__r29VisualMetrics.bridge;",
        loader: "js",
      }));
    },
  }],
});
const mod = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);

const container = document.getElementById("root");
const flush = async (fn) => React.act(async () => {
  if (fn) await fn();
  for (let i = 0; i < 20; i++) await Promise.resolve();
});
const wrap = (child) => React.createElement(mod.exports.I18nProvider, null, child);
const switches = () => [...container.querySelectorAll('[role="switch"]')];
const checked = (node) => node?.getAttribute("aria-checked") === "true";
const visualCalls = () => calls.filter(({ command }) =>
  command === "visual_metrics_config_get"
  || command === "visual_metrics_config_save"
  || command === "feature018_visual_metrics_configure");
const clearCalls = () => { calls.length = 0; };

async function mountSettings({ online, handler }) {
  invokeHandler = handler;
  clearCalls();
  const root = createRoot(container);
  await flush(() => root.render(wrap(React.createElement(mod.exports.SettingsPage, { online }))));
  for (let i = 0; i < 3; i++) await flush();
  return root;
}

// Native production load + online apply-before-save.
let persisted = { showFps: false, showPing: true };
let failSave = false;
const settingsHandler = async (command, args) => {
  if (command === "visual_metrics_config_get") return { ...persisted };
  if (command === "feature018_visual_metrics_configure") return { state: "confirmed" };
  if (command === "visual_metrics_config_save") {
    if (failSave) throw new Error("NATIVE_SECRET_SAVE_DETAIL_SHOULD_NOT_RENDER");
    persisted = { showFps: args.showFps === true, showPing: args.showPing === true };
    return { ...persisted };
  }
  throw new Error(`Unexpected Settings visual metrics command: ${command}`);
};

let mounted = await mountSettings({ online: true, handler: settingsHandler });
assert.deepEqual(visualCalls().map(({ command }) => command), ["visual_metrics_config_get"],
  "native Settings must load visual metrics from the profile runtime store");
let toggleNodes = switches();
assert.equal(toggleNodes.length, 2, "visual metrics Settings must expose FPS and Ping switches after load");
assert.equal(checked(toggleNodes[0]), false, "FPS load state mismatch");
assert.equal(checked(toggleNodes[1]), true, "Ping load state mismatch");

clearCalls();
await flush(() => toggleNodes[0].dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.deepEqual(visualCalls().map(({ command }) => command), [
  "feature018_visual_metrics_configure",
  "visual_metrics_config_save",
], "online toggle must apply to the game before persisting");
assert.deepEqual(visualCalls().map(({ args }) => ({ showFps: args.showFps, showPing: args.showPing })), [
  { showFps: true, showPing: true },
  { showFps: true, showPing: true },
], "online configure/save payloads must contain the complete next visual state");
toggleNodes = switches();
assert.equal(checked(toggleNodes[0]), true, "successful online FPS toggle did not remain enabled");
assert.equal(container.querySelector('.update-error[role="alert"]'), null, "successful online save left an error visible");

// If persistence fails after live apply, restore both UI and live game state.
failSave = true;
clearCalls();
await flush(() => toggleNodes[1].dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.deepEqual(visualCalls().map(({ command }) => command), [
  "feature018_visual_metrics_configure",
  "visual_metrics_config_save",
  "feature018_visual_metrics_configure",
], "failed online persistence must re-apply the previous live game state");
assert.deepEqual(visualCalls().map(({ args }) => ({ showFps: args.showFps, showPing: args.showPing })), [
  { showFps: true, showPing: false },
  { showFps: true, showPing: false },
  { showFps: true, showPing: true },
], "save failure rollback must restore the exact previous game state");
toggleNodes = switches();
assert.equal(checked(toggleNodes[1]), true, "failed save did not roll the Ping switch back");
const saveAlert = container.querySelector('.update-error[role="alert"]');
assert.equal(saveAlert?.textContent, "Failed to save in-game display settings.",
  "save failure must surface the localized plain Settings error");
assert.ok(!container.textContent.includes("NATIVE_SECRET_SAVE_DETAIL_SHOULD_NOT_RENDER"),
  "native save details leaked into Settings UI");

// A subsequent edit clears the prior error and follows the normal command order.
failSave = false;
clearCalls();
await flush(() => switches()[1].dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.deepEqual(visualCalls().map(({ command }) => command), [
  "feature018_visual_metrics_configure",
  "visual_metrics_config_save",
], "retry after failure did not return to normal online configure/save order");
assert.equal(container.querySelector('.update-error[role="alert"]'), null,
  "successful retry did not clear the previous save error");
assert.equal(checked(switches()[1]), false, "successful retry did not commit the Ping edit");
await flush(() => mounted.unmount());

// Offline edits persist, but must not dispatch a live game command.
persisted = { showFps: false, showPing: false };
mounted = await mountSettings({ online: false, handler: settingsHandler });
clearCalls();
await flush(() => switches()[0].dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.deepEqual(visualCalls().map(({ command }) => command), ["visual_metrics_config_save"],
  "offline visual toggle must persist without sending a game configure command");
assert.deepEqual({ showFps: persisted.showFps, showPing: persisted.showPing }, { showFps: true, showPing: false },
  "offline visual toggle did not persist the complete next state");
assert.equal(checked(switches()[0]), true, "offline persisted FPS toggle did not remain enabled");
await flush(() => mounted.unmount());

// Load failures stay localized/plain rather than exposing native details.
mounted = await mountSettings({
  online: false,
  handler: async (command) => {
    if (command === "visual_metrics_config_get") throw new Error("NATIVE_SECRET_LOAD_DETAIL_SHOULD_NOT_RENDER");
    throw new Error(`Unexpected load-error command: ${command}`);
  },
});
const loadAlert = container.querySelector('.update-error[role="alert"]');
assert.equal(loadAlert?.textContent, "Failed to load in-game display settings.",
  "load failure must surface the localized plain Settings error");
assert.ok(!container.textContent.includes("NATIVE_SECRET_LOAD_DETAIL_SHOULD_NOT_RENDER"),
  "native load details leaked into Settings UI");
await flush(() => mounted.unmount());

// App reconnect synchronization: offline startup sends no game configuration;
// once the owned status pair becomes connected, persisted visual state is read
// and applied to the game without requiring the Settings route to be opened.
const profile = { id: "profile-a", displayName: "Local 1", enabled: true, serverId: 2212, note: "" };
const snapshot = { selectedProfileId: "profile-a", maxProfiles: 1, profiles: [profile] };
window.__LWBridgeBootstrap = { profiles: snapshot, localRuntime: { profileCapacity: 1 } };
bridge.profileId = "profile-a";
generation += 1;
let appOnline = false;
invokeHandler = async (command, args = {}) => {
  if (command === "profile_instances_reconcile") return { phase: "stopped", connectionState: "offline", results: [], errors: [] };
  if (command === "local_game_launch_status") return { phase: "stopped", gameRunning: false, connectionState: "offline" };
  if (command === "profile_list" || command === "profile_select") return { ...snapshot, selectedProfileId: args.profileId || snapshot.selectedProfileId };
  if (command === "map_summary") return { serverId: 2212, counts: {}, scanState: { serverId: 2212, isReading: false, selectedTypes: [] } };
  if (command === "map_scan_status") return { serverId: 2212, isReading: false, selectedTypes: [] };
  if (command === "map_plunder_jobs_list") return { dispatchJobs: [], truckJobs: [] };
  if (command === "get_status") return { xluaOnline: appOnline, pending: 0 };
  if (command === "proxy_status") return { gameRunning: appOnline };
  if (command === "game_root_status") return { valid: false };
  if (command === "game_recovery_status") return { state: "idle" };
  if (command === "server_jump_history_import") return [];
  if (command === "update_status") return { phase: "idle", currentVersion: "", latestVersion: null };
  if (command === "local_config_get") return { autoLaunchGame: false, autoReconnect: false };
  if (command === "local_map_auto_scan_status") return { running: false };
  if (command === "profile_instance_status") return { phase: "stopped", connectionState: "offline" };
  if (command === "visual_metrics_config_get") return { showFps: true, showPing: false };
  if (command === "feature018_visual_metrics_configure") return { state: "confirmed" };
  throw new Error(`Unexpected App reconnect command: ${command}`);
};
clearCalls();
mounted = createRoot(container);
await flush(() => mounted.render(wrap(React.createElement(mod.exports.App))));
for (let i = 0; i < 6; i++) await flush();
assert.equal(visualCalls().length, 0, "offline App startup must not push visual metrics into the game");

appOnline = true;
clearCalls();
const refresh = [...container.querySelectorAll(".top-actions button")]
  .find((button) => button.textContent.trim() === "Refresh Status");
assert.ok(refresh, "App reconnect regression could not find Refresh Status control");
await flush(() => refresh.dispatchEvent(new MouseEvent("click", { bubbles: true })));
for (let i = 0; i < 4; i++) await flush();
assert.deepEqual(visualCalls().map(({ command }) => command), [
  "visual_metrics_config_get",
  "feature018_visual_metrics_configure",
], "offline-to-connected App transition must reload and apply persisted visual metrics");
const reconnectConfigure = visualCalls()[1];
assert.deepEqual({ showFps: reconnectConfigure.args.showFps, showPing: reconnectConfigure.args.showPing },
  { showFps: true, showPing: false }, "App reconnect sync applied the wrong persisted visual state");

await flush(() => mounted.unmount());
assert.equal(intervals.size, 0, "R29 visual metrics App reconnect mount leaked an interval");
assert.ok(listeners.every((entry) => !entry.active), "R29 visual metrics App reconnect mount leaked a native listener");
dom.window.close();

console.log("FEATURE018_R29_VISUAL_METRICS_UI_PASS");
