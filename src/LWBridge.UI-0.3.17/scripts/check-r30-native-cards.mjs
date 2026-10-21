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
const bundle = await build({ stdin: { contents: 'export { NativeAutomationCard } from "./NativeAutomationCard.jsx"; export { NativeGatherCard } from "./NativeGatherCard.jsx"; export { NativeTradeCard } from "./NativeTradeCard.jsx"; export { AutomationCardBoundary } from "./AutomationCardBoundary.jsx"; export { NativeChatAutomationCard } from "./NativeChatAutomationCard.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const mod = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);
const { NativeAutomationCard, NativeGatherCard, NativeTradeCard, AutomationCardBoundary, NativeChatAutomationCard, I18nProvider } = mod.exports;
const defaults = {
  dispatch: { enabled: false, autoExecute: false, collectRewards: false, delayMinutes: 3, weeklyQualities: ["none", "ur", "none", "none", "none", "ur", "none"] },
  dispatchAssist: { enabled: false, autoHelp: false, qualities: [], delaySeconds: [0, 0], intervalSeconds: 30 },
  allianceGather: { enabled: false, allianceGatherSquads: [1, 2] },
  tradeStation: { enabled: false, crossServerEnabled: false, selectedItemIds: [], selectedCurrencyIds: [15, 650053] },
  resourceGather: { enabled: false, scanRadius: 200, manualResumeDelaySeconds: 120, recallOnDisable: false, squads: [1, 2, 3, 4].map(i => ({ squadIndex: i, enabled: false, resource: "metal", level: 10 })) },
};
const store = structuredClone(defaults);
const calls = [];
const bridge = { profileId: "a", invokeProfileScoped: async (command, payload = {}) => {
  calls.push(`${command}:${payload.kind ?? ""}`);
  if (command === "feature018_automation_config_get") return structuredClone(store[payload.kind]);
  if (command === "feature018_automation_config_save") { store[payload.kind] = structuredClone(payload.config);return structuredClone(store[payload.kind]); }
  if (command === "feature018_automation_status") return { services: {} };
  // Live v27 seasonal reply: empty native Lua tables arrive as {} (objects), not arrays.
  if (command === "feature018_automation_observe") return { readOnly: true, seasonal: true, available: 0, reason: "Trade stations are unavailable in this account season", catalog: { nativeTemplate: true, items: {}, currencies: {} } };
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

// Original layout: multi-option cards collapse their settings, closed by default and remembered.
const secret = document.querySelector('[data-native-automation=dispatch]');
const secretTrigger = secret.querySelector('.automation-config-trigger');
assert.equal(secretTrigger.getAttribute("aria-expanded"), "false");
assert.equal(secret.querySelector('.automation-config-body').style.display, "none");
await flush(() => secretTrigger.click());
assert.equal(secret.querySelector('.automation-config-body').style.display, "");
assert.equal(localStorage.getItem("automation-settings:a:dispatch"), "true");
const allianceTrigger = document.querySelector('[data-native-automation=allianceGather] .automation-config-trigger');
await flush(() => allianceTrigger.click());
// Secret Task: the switch turns on automatic dispatch; Rewards is an extra option.
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
assert.ok(!document.querySelector(".automation-run-action"), "Owner 2026-10-21: no Run now button on Automation cards");
await flush(() => page.unmount());
// Owner 2026-10-21: Red Packet "Min 10, Max 0" then switch on = a real 10-10 s delay, never a switch that looks on but did not save.
let chatSaved = { enabled: false, claimDelaySeconds: [0, 0], replyEnabled: false, replyDelaySeconds: [2, 5], replies: [] };
const chatBridge = { profileId: "a", invokeProfileScoped: async (command, payload = {}) => {
  if (command === "feature018_chat_config_get") return structuredClone(chatSaved);
  if (command === "feature018_chat_config_save") { chatSaved = structuredClone(payload.config);return structuredClone(chatSaved); }
  if (command === "feature018_chat_status") return { services: { redPacket: { state: chatSaved.enabled ? "waiting" : "disabled" } } };
  return {};
} };
const chat = createRoot(document.getElementById("root"));
await flush(() => chat.render(React.createElement(I18nProvider, null, React.createElement(NativeChatAutomationCard, { title: "Red Packet", description: "", online: true, profileId: "a", availableSquads: [1], bridge: chatBridge }))));
await flush();await flush();
const minBox = document.querySelectorAll('[data-native-chat=redPacket] input[type=number]')[0];
const setNumber = Object.getOwnPropertyDescriptor(dom.window.HTMLInputElement.prototype, "value").set;
await flush(() => { setNumber.call(minBox, "10");minBox.dispatchEvent(new Event("input", { bubbles: true })); });
await flush(() => minBox.dispatchEvent(new dom.window.FocusEvent("focusout", { bubbles: true })));
assert.equal(document.querySelectorAll('[data-native-chat=redPacket] input[type=number]')[1].value, "10", "Max follows a higher Min");
await flush(() => document.querySelector('[data-native-chat=redPacket] button[role=switch]').click());
await flush();
assert.deepEqual([chatSaved.enabled, chatSaved.claimDelaySeconds], [true, [10, 10]], "Switch-on saves the 10 s delay");
assert.ok(!document.body.textContent.includes("CONFIG_DRAFT_INVALID"), "No raw internal code is shown");
// Auto Reply game emoji picker: read-only list, click appends the in-text tag to the last reply line.
chatSaved = { ...chatSaved, replyEnabled: true, replies: ["thanks"] };
chatBridge.invokeProfileScoped = (inner => async (command, payload) => command === "feature018_chat_emoji_list"
  ? { readOnly: true, emojis: { 1: { id: 7, name: "flower", tag: "<lwEmoji:7:>" } } } : inner(command, payload))(chatBridge.invokeProfileScoped);
await flush(() => chat.unmount());
const chat2 = createRoot(document.getElementById("root"));
await flush(() => chat2.render(React.createElement(I18nProvider, null, React.createElement(NativeChatAutomationCard, { title: "Red Packet", description: "", online: true, profileId: "a", availableSquads: [1], bridge: chatBridge }))));
await flush();await flush();
await flush(() => document.querySelector('.game-emoji-picker > button').click());await flush();
await flush(() => document.querySelector('.game-emoji-list [data-emoji-id="7"]').click());await flush();
assert.deepEqual(chatSaved.replies, ["thanks <lwEmoji:7:>"], "Picked game emoji is saved as its in-text tag");
await flush(() => chat2.unmount());
// Trade Station with the real "not this season" reply must render, not crash the app.
const trade = createRoot(document.getElementById("root"));
await flush(() => trade.render(React.createElement(I18nProvider, null, React.createElement(NativeTradeCard, { online: true, profileId: "a", bridge }))));
await flush();await flush();
assert.ok(document.querySelector('[data-native-automation=tradeStation]'), "Seasonal empty catalog renders the Trade card");
await flush(() => trade.unmount());
// One failing card is contained; the rest of the page stays.
const Broken = () => { throw new Error("inert card failure"); };
const consoleError = console.error;console.error = () => {};
const guarded = createRoot(document.getElementById("root"));
await flush(() => guarded.render(React.createElement(I18nProvider, null, React.createElement("div", null,
  React.createElement(AutomationCardBoundary, { name: "x", title: "X", message: "failed", retryLabel: "Retry" }, React.createElement(Broken)),
  React.createElement("p", { id: "survivor" }, "still here")))));
console.error = consoleError;
assert.ok(document.querySelector('[data-automation-card-failed=x]') && document.getElementById("survivor"), "A crashing card never blanks the page");
await flush(() => guarded.unmount());
dom.window.close();
console.log("FEATURE018_R30_NATIVE_CARDS_PASS collapse trade-empty-catalog boundary shared-status secret+assist alliance-priority gathering-squads stop-first");
