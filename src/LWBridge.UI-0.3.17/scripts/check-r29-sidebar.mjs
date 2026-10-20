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
globalThis.fetch = () => { throw new Error("R29 sidebar check forbids external network access"); };
window.matchMedia = () => ({ matches: true });
localStorage.setItem("lwbridge.language", "en");
localStorage.setItem("lwbridge.theme", "light");
document.documentElement.dataset.theme = "light";

const intervals = new Map();
const timeouts = new Map();
const listeners = [];
let timerId = 0;
let failStatusRead = false;
let statusOnline = false;
window.setInterval = (fn, delay) => { intervals.set(++timerId, { fn, delay }); return timerId; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = (fn, delay) => { timeouts.set(++timerId, { fn, delay }); return timerId; };
window.clearTimeout = (id) => timeouts.delete(id);

const profiles = [
  { id: "profile-a", displayName: "Local 1", enabled: true, serverId: 2212, note: "" },
  { id: "profile-b", displayName: "Local 2", enabled: true, serverId: 2212, note: "" },
];
const snapshot = { selectedProfileId: "profile-a", maxProfiles: 2, profiles };
window.__LWBridgeBootstrap = { profiles: snapshot, localRuntime: { profileCapacity: 2 } };

const bridge = {
  mode: "native",
  available: true,
  profileId: "profile-a",
  setSelectedProfile(id) { this.profileId = id; },
  currentProfileOwner: () => ({ profileId: bridge.profileId, generation: 1 }),
  isCurrentProfileOwner: (owner) => owner.profileId === bridge.profileId && owner.generation === 1,
  listen(eventName, fn) {
    const state = { eventName, fn, active: true };
    listeners.push(state);
    return () => { state.active = false; };
  },
  invokeProfileScoped: (command, args) => invoke(command, args),
  invoke: (command, args) => invoke(command, args),
};

function invoke(command, args = {}) {
  if (command === "profile_instances_reconcile") return Promise.resolve({ phase: "stopped", connectionState: "offline", results: [], errors: [] });
  if (command === "local_game_launch_status") return Promise.resolve({ phase: "stopped", gameRunning: false, connectionState: "offline" });
  if (command === "profile_list" || command === "profile_select") return Promise.resolve({ ...snapshot, selectedProfileId: args.profileId || snapshot.selectedProfileId });
  if (command === "map_summary") return Promise.resolve({ serverId: 2212, counts: {}, scanState: { serverId: 2212, isReading: false, selectedTypes: [] } });
  if (command === "map_scan_status") return Promise.resolve({ serverId: 2212, isReading: false, selectedTypes: [] });
  if (command === "map_plunder_jobs_list") return Promise.resolve({ dispatchJobs: [], truckJobs: [] });
  if (command === "get_status") {
    if (failStatusRead) return Promise.reject(new Error("NATIVE_STATUS_READ_DETAIL_SHOULD_NOT_RENDER"));
    return Promise.resolve({ xluaOnline: statusOnline, pending: 0 });
  }
  if (command === "proxy_status") return Promise.resolve({ gameRunning: statusOnline });
  if (command === "game_root_status") return Promise.resolve({ valid: false });
  if (command === "game_recovery_status") return Promise.resolve({ state: "idle" });
  if (command === "server_jump_history_import") return Promise.resolve([]);
  if (command === "update_status") return Promise.resolve({ phase: "idle", currentVersion: "", latestVersion: null });
  if (command === "local_config_get") return Promise.resolve({ autoLaunchGame: false, autoReconnect: false });
  if (command === "local_map_auto_scan_status") return Promise.resolve({ running: false });
  if (command === "profile_instance_status") return Promise.resolve({ phase: "stopped", connectionState: "offline" });
  if (command === "visual_metrics_config_get") return Promise.resolve({ showFps: false, showPing: false });
  if (command === "feature018_visual_metrics_configure") return Promise.resolve({ state: "confirmed" });
  throw new Error(`Unexpected R29 sidebar App command: ${command}`);
}

globalThis.__r29Sidebar = { bridge };
const bundle = await build({
  stdin: {
    contents: 'export { App } from "./App.jsx"; export { ProfileSidebar } from "./ProfileSidebar.jsx"; export { I18nProvider, useI18n } from "./i18n.jsx";',
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
    name: "r29-inert-native-transport",
    setup(builder) {
      builder.onLoad({ filter: /backendBridge\.js$/ }, () => ({
        contents: "export const backendBridge=globalThis.__r29Sidebar.bridge;",
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
  for (let i = 0; i < 18; i++) await Promise.resolve();
});
const element = () => React.createElement(mod.exports.I18nProvider, null, React.createElement(mod.exports.App));
const storageKey = "lwbridge.profileSidebarHidden";

let mounted = createRoot(container);
await flush(() => mounted.render(element()));
for (let i = 0; i < 6; i++) await flush();

assert.ok(container.querySelector(".profile-sidebar"), "multi-profile sidebar must be visible by default");
assert.equal(container.querySelectorAll(".profile-item").length, 2, "visible sidebar must retain both profile switcher controls");
assert.equal(container.querySelectorAll(".profile-compact-item").length, 0, "R29 must not leave a compact account strip");
assert.ok(!container.querySelector(".app-layout").classList.contains("single-profile"), "visible sidebar must retain its account column");
assert.equal(container.querySelector(".profile-sidebar-restore"), null, "restore control is only needed while the sidebar is hidden");

// A real native pair failure must use the locale catalog, including after a
// live language switch, and a successful retry must clear the failure state.
const refreshButton = [...container.querySelectorAll(".top-actions button")]
  .find((button) => button.textContent.trim() === "Refresh Status");
assert.ok(refreshButton, "R29 shell check could not find Refresh Status");
failStatusRead = true;
await flush(() => refreshButton.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(container.querySelector(".status-online strong")?.textContent.trim(), "Required component unavailable",
  "native status failure must render the localized English unavailable label");
const languageSelect = container.querySelector(".language-select select");
await flush(() => {
  languageSelect.value = "zh-CN";
  languageSelect.dispatchEvent(new Event("change", { bubbles: true }));
});
for (let i = 0; i < 3; i++) await flush();
assert.equal(container.querySelector(".status-online strong")?.textContent.trim(), "所需游戏组件不可用",
  "live zh-CN switch must re-localize the native unavailable state");
failStatusRead = false;
await flush(() => refreshButton.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.notEqual(container.querySelector(".status-online strong")?.textContent.trim(), "所需游戏组件不可用",
  "successful status retry must clear the previous unavailable state");

// App-owned transient popover errors store translation keys, so the same live
// language switch must also re-render an already visible validation error.
statusOnline = true;
await flush(() => refreshButton.dispatchEvent(new MouseEvent("click", { bubbles: true })));
await flush(() => {
  languageSelect.value = "en";
  languageSelect.dispatchEvent(new Event("change", { bubbles: true }));
});
for (let i = 0; i < 3; i++) await flush();
const serverJumpToggle = container.querySelector(".server-jump > button");
await flush(() => serverJumpToggle.dispatchEvent(new MouseEvent("click", { bubbles: true })));
const serverJumpAction = container.querySelector(".server-jump-form button");
await flush(() => serverJumpAction.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(container.querySelector(".server-jump-error")?.textContent, "Server ID must be an integer from 1 to 99999.",
  "server-jump validation must start in English");
await flush(() => {
  languageSelect.value = "zh-CN";
  languageSelect.dispatchEvent(new Event("change", { bubbles: true }));
});
for (let i = 0; i < 3; i++) await flush();
assert.equal(container.querySelector(".server-jump-error")?.textContent, "区服 ID 必须为 1–99999 的整数。",
  "stored server-jump validation must re-localize after switching to zh-CN");
await flush(() => {
  languageSelect.value = "en";
  languageSelect.dispatchEvent(new Event("change", { bubbles: true }));
});
for (let i = 0; i < 3; i++) await flush();

const hide = container.querySelector(".profile-collapse");
assert.ok(hide, "visible sidebar needs a full-hide control");
await flush(() => hide.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(localStorage.getItem(storageKey), "true", "full-hide choice must persist immediately");
assert.equal(container.querySelector(".profile-sidebar"), null, "full-hide must remove the entire aside");
assert.ok(container.querySelector(".app-layout").classList.contains("single-profile"), "hidden sidebar must leave no account grid column");
const restore = container.querySelector(".profile-sidebar-restore");
assert.ok(restore, "hidden sidebar must leave an always-visible restore control outside the aside");
assert.equal(restore.closest(".profile-sidebar"), null, "restore control cannot live inside the hidden sidebar subtree");

await flush(() => mounted.unmount());
assert.equal(intervals.size, 0, "first App mount leaked an interval");

mounted = createRoot(container);
await flush(() => mounted.render(element()));
for (let i = 0; i < 6; i++) await flush();
assert.equal(container.querySelector(".profile-sidebar"), null, "persisted hidden choice must survive a full App remount");
const persistedRestore = container.querySelector(".profile-sidebar-restore");
assert.ok(persistedRestore, "persisted hidden state must still expose restore after remount");

await flush(() => persistedRestore.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(localStorage.getItem(storageKey), "false", "restoring the sidebar must persist the visible choice");
assert.ok(container.querySelector(".profile-sidebar"), "restore control must bring the full sidebar back");
assert.equal(container.querySelectorAll(".profile-item").length, 2, "restored sidebar must preserve profile switcher semantics");
assert.equal(container.querySelectorAll(".profile-compact-item").length, 0, "restore must not recreate the retired compact rail");
assert.ok(!container.querySelector(".app-layout").classList.contains("single-profile"), "restored sidebar must reclaim its grid column");

await flush(() => mounted.unmount());
assert.equal(intervals.size, 0, "remounted App leaked an interval");
assert.ok(listeners.every((entry) => !entry.active), "App leaked a native listener across sidebar persistence test");

// Owner direction is one game at a time. Start All is gone and, while A has an
// owned stop target, B's individual Start is disabled. Store the native failure
// itself so an already-visible profile action error re-localizes in place.
function ProfileHarness() {
  const { setLanguage } = mod.exports.useI18n();
  return React.createElement(React.Fragment, null,
    React.createElement("button", { id: "profile-language-zh", onClick: () => setLanguage("zh-CN") }, "zh-CN"),
    React.createElement(mod.exports.ProfileSidebar, {
      state: snapshot,
      instances: {
        "profile-a": { phase: "running", connectionState: "connected", instanceId: "instance-a" },
        "profile-b": { phase: "stopped", connectionState: "offline" },
      },
      onStartProfile: async () => ({ phase: "running", connectionState: "connected", instanceId: "unexpected" }),
      onStopProfile: async () => {
        const failure = new Error("PROFILE_RUNNING");
        failure.code = "PROFILE_RUNNING";
        throw failure;
      },
    }),
  );
}
localStorage.setItem("lwbridge.language", "en");
mounted = createRoot(container);
await flush(() => mounted.render(React.createElement(mod.exports.I18nProvider, null, React.createElement(ProfileHarness))));
for (let i = 0; i < 3; i++) await flush();
const batchButtons = [...container.querySelectorAll(".profile-batch-actions button")];
assert.equal(batchButtons.length, 1, "one-game sidebar must expose only the safe Stop All batch action");
assert.equal(batchButtons[0].textContent.trim(), "Stop all", "remaining batch action must be Stop all");
const runButtons = [...container.querySelectorAll(".profile-run")];
assert.equal(runButtons.length, 2, "direct sidebar regression needs both account run controls");
assert.equal(runButtons[0].disabled, false, "running account must retain its Stop action");
assert.equal(runButtons[1].disabled, true, "idle account Start must be disabled while another game is owned");
await flush(() => runButtons[0].dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(container.querySelector(".profile-error")?.textContent, "Stop this account before changing it.",
  "profile lifecycle failure must render localized English text without raw codes");
await flush(() => document.getElementById("profile-language-zh").dispatchEvent(new MouseEvent("click", { bubbles: true })));
for (let i = 0; i < 3; i++) await flush();
assert.equal(container.querySelector(".profile-error")?.textContent, "请先关闭该账号。",
  "visible profile lifecycle failure must re-localize after the language changes");
assert.ok(!container.textContent.includes("PROFILE_RUNNING"), "raw profile error code leaked into the sidebar");
await flush(() => mounted.unmount());
dom.window.close();

console.log("FEATURE018_R29_SHELL_SIDEBAR_PASS");
