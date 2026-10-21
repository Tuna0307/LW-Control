import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
// R30 mounted checks for the native Secret Task (+ Dispatch Assist), Alliance Gathering
// Dispatch, Resource gathering and shared status polling. Inert fake bridge only.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), React = req("react"), { build } = req("esbuild");
const dom = new JSDOM('<div id="root"></div>', { url: "http://localhost/" });
for (const key of ["window", "document", "HTMLElement", "Node", "MutationObserver", "localStorage", "Event", "MouseEvent"]) globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "en");
const { createRoot } = req("react-dom/client");
const bundle = await build({ stdin: { contents: 'export { NativeAutomationCard } from "./NativeAutomationCard.jsx"; export { NativeGatherCard } from "./NativeGatherCard.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const mod = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);
const { NativeAutomationCard, NativeGatherCard, I18nProvider } = mod.exports;
const defaults = {
  dispatch: { enabled: false, autoExecute: false, collectRewards: false, delayMinutes: 3, weeklyQualities: ["none", "ur", "none", "none", "none", "ur", "none"] },
  dispatchAssist: { enabled: false, autoHelp: false, qualities: [], delaySeconds: [0, 0], intervalSeconds: 30 },
  allianceGather: { enabled: false, allianceGatherSquads: [1, 2] },
  resourceGather: { enabled: false, scanRadius: 200, manualResumeDelaySeconds: 120, recallOnDisable: false, squads: [1, 2, 3, 4].map(i => ({ squadIndex: i, enabled: false, resource: "metal", level: 10 })) },
};
const store = structuredClone(defaults);
const calls = [];
const bridge = { profileId: "a", invokeProfileScoped: async (command, payload = {}) => {
  calls.push(`${command}:${payload.kind ?? ""}`);
  if (command === "feature018_automation_config_get") return structuredClone(store[payload.kind]);
  if (command === "feature018_automation_config_save") { store[payload.kind] = structuredClone(payload.config);return structuredClone(store[payload.kind]); }
  if (command === "feature018_automation_status") return { services: {} };
  if (["feature018_automation_stop", "feature018_automation_configure", "feature018_automation_run"].includes(command)) return {};
  throw new Error(command);
} };
const flush = async fn => React.act(async () => { if (fn) await fn(); await new Promise(resolve => setTimeout(resolve, 0)); });
const statusCalls = () => calls.filter(c => c.startsWith("feature018_automation_status")).length;

// Three cards mounted together share one status read.
const page = createRoot(document.getElementById("root"));
await flush(() => page.render(React.createElement(I18nProvider, null,
  React.createElement(NativeAutomationCard, { title: "Secret Task", description: "", online: true, profileId: "a", bridge }),
  React.createElement(NativeAutomationCard, { title: "Alliance Gathering Dispatch", description: "", online: true, profileId: "a", bridge }),
  React.createElement(NativeGatherCard, { online: true, profileId: "a", bridge }))));
await flush();
assert.equal(statusCalls(), 1, "Visible native Automation cards share one status read");

// Secret Task: the switch turns on automatic dispatch; Rewards is an extra option.
const secret = document.querySelector('[data-native-automation=dispatch]');
await flush(() => secret.querySelector('button[role=switch]').click());
assert.equal(store.dispatch.enabled, true);assert.equal(store.dispatch.autoExecute, true);
await flush(() => secret.querySelectorAll('[data-native-mission-fields=dispatch] .automation-inline-checkbox input')[1].click());
assert.equal(store.dispatch.collectRewards, true);
// Dispatch Assist lives inside the Secret Task card as its own native service.
const assist = document.querySelector('[data-native-mission-fields=dispatchAssist]');
assert.ok(assist, "Dispatch Assist settings are inside the Secret Task card");
await flush(() => assist.querySelectorAll('.automation-squad-choices input')[4].click());
await flush(() => assist.querySelector('.automation-inline-checkbox input').click());
assert.deepEqual([store.dispatchAssist.enabled, store.dispatchAssist.autoHelp, store.dispatchAssist.qualities], [true, true, ["ur"]]);
let before = calls.length;
await flush(() => secret.querySelector('button[role=switch]').click());
assert.equal(calls[before], "feature018_automation_stop:dispatch", "Off stops Secret Task first");
assert.deepEqual([store.dispatch.enabled, store.dispatch.autoExecute, store.dispatch.collectRewards], [false, false, false]);

// Alliance Gathering Dispatch: recovered default squads 1 and 2; add 3 and raise it.
const alliance = document.querySelector('[data-native-automation=allianceGather]');
const squadBoxes = () => [...alliance.querySelectorAll('.automation-squad-priority-item input')];
await flush(() => squadBoxes()[2].click());
assert.deepEqual(store.allianceGather.allianceGatherSquads, [1, 2, 3]);
await flush(() => alliance.querySelectorAll('.automation-squad-move')[1].click());
assert.deepEqual(store.allianceGather.allianceGatherSquads, [1, 3, 2], "Up moves a squad earlier in priority");
await flush(() => alliance.querySelector('button[role=switch]').click());
assert.equal(store.allianceGather.enabled, true);
for (const box of squadBoxes().filter(b => b.checked).slice(0, 2)) await flush(() => box.click());
await flush(() => squadBoxes().find(b => b.checked).click());
assert.ok(store.allianceGather.allianceGatherSquads.length > 0, "Enabled card keeps at least one squad");
assert.ok(alliance.querySelector('[role=alert]'));

// Resource gathering: cannot switch on without a squad; settings save as native values.
const gather = document.querySelector('[data-native-automation=resourceGather]');
await flush(() => gather.querySelector('button[role=switch]').click());
assert.equal(store.resourceGather.enabled, false, "No squad selected: stays off");
const row = gather.querySelectorAll('.automation-resource-gather-squad')[0];
await flush(() => row.querySelector('input[type=checkbox]').click());
await flush(() => { const s = row.querySelectorAll('select')[0];s.value = "food";s.dispatchEvent(new Event("change", { bubbles: true })); });
await flush(() => { const s = row.querySelectorAll('select')[1];s.value = "2";s.dispatchEvent(new Event("change", { bubbles: true })); });
assert.deepEqual(store.resourceGather.squads[0], { squadIndex: 1, enabled: true, resource: "food", level: 2 });
await flush(() => gather.querySelector('button[role=switch]').click());
assert.equal(store.resourceGather.enabled, true);
before = calls.length;
await flush(() => gather.querySelector('button[role=switch]').click());
assert.equal(calls[before], "feature018_automation_stop:resourceGather", "Off stops gathering before saving");
assert.equal(store.resourceGather.enabled, false);
assert.ok(!document.querySelector('[data-preview-fixture]'), "Native cards never render fixtures");
await flush(() => page.unmount());
dom.window.close();
console.log("FEATURE018_R30_NATIVE_CARDS_PASS shared-status secret+assist alliance-priority gathering-squads stop-first");
