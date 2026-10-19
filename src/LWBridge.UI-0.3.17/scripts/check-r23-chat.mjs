import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { chatConfig, chatDraft, createChatAdapter } from "../src/chatAutomationRuntime.js";
const calls = [], saved = { enabled: false, claimDelaySeconds: [1, 2], replies: ["Thanks"], replyEnabled: false };
let configReadMode = "ready";
const heldConfigReads = [];
const bridge = { profileId: "A", async invokeProfileScoped(command, payload) {
  calls.push({ command, payload: structuredClone(payload) });
  if (command === "feature018_chat_config_get") {
    if (configReadMode === "held") return new Promise(resolve => heldConfigReads.push(resolve));
    if (configReadMode === "failed") throw new Error("Confirmed Chat settings could not be loaded");
    return structuredClone(saved);
  }
  if (command === "feature018_chat_config_save") { Object.assign(saved, payload.config); return structuredClone(saved); }
  if (command === "feature018_chat_status") return { listener: { ready: true, observedMessages: 4 }, services: { redPacket: { state: "listening" } } };
  if (["feature018_chat_configure", "feature018_chat_run"].includes(command)) return { state: "confirmed" };
  throw new Error(command);
} };
assert.deepEqual(chatConfig(chatDraft(saved)).claimDelaySeconds, [1, 2]);
const adapter = createChatAdapter(bridge, "A", "redPacket", () => true, true);
await adapter.write({ ...chatDraft(saved), enabled: true });
assert.deepEqual(calls.map(c => c.command), ["feature018_chat_config_save", "feature018_chat_configure"]);
bridge.profileId = "B";
await assert.rejects(() => adapter.read(), e => e.code === "PROFILE_SCOPE_MISMATCH");
bridge.profileId = "A";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."), req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), { build } = req("esbuild"), React = req("react");
const dom = new JSDOM('<div id="root"></div>', { url: "http://127.0.0.1/" });
for (const key of ["window", "document", "HTMLElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
localStorage.setItem("lwbridge.language", "en");
const bundle = await build({ stdin: { contents: 'export { NativeChatAutomationCard } from "./NativeChatAutomationCard.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const loaded = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { NativeChatAutomationCard, I18nProvider } = loaded.exports;
const { createRoot } = req("react-dom/client"), mounted = createRoot(document.getElementById("root"));
const flush = async fn => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 70; i++) await Promise.resolve(); });
const nativeSetInterval = globalThis.setInterval, nativeClearInterval = globalThis.clearInterval;
const pollTimers = new Map(); let nextPollTimer = 0;
globalThis.setInterval = callback => { const id = ++nextPollTimer; pollTimers.set(id, callback); return id; };
globalThis.clearInterval = id => pollTimers.delete(id);
const poll = () => flush(() => Promise.all([...pollTimers.values()].map(callback => callback())));
const render = (online, profileId = "A") => mounted.render(React.createElement(I18nProvider, {}, React.createElement(NativeChatAutomationCard, { key: profileId, title: "Red Packet", description: "Listen for chat", online, profileId, availableSquads: [1], bridge })));
const mutations = () => calls.filter(call => ["feature018_chat_config_save", "feature018_chat_configure", "feature018_chat_run"].includes(call.command));

// A live status reply must not unlock default-valued controls while the
// independent persisted settings read is unresolved. Otherwise the first
// switch click can erase the owner's saved delays and reply phrases.
configReadMode = "held";
saved.replyEnabled = true; saved.replies = ["Keep this confirmed reply"];
const beforeHydration = mutations().length;
await flush(() => render(true));
assert.equal(heldConfigReads.length, 1);
assert.match(document.body.textContent, /listening/, "Native status is available independently of config hydration");
assert.equal(document.querySelector('button[role="switch"]').disabled, true);
assert.equal(document.querySelector('.automation-run-action').disabled, true);
assert.equal(document.querySelector('fieldset').disabled, true);
await flush(() => {
  document.querySelector('button[role="switch"]').click();
  document.querySelector('.automation-run-action').click();
});
await poll();
assert.equal(mutations().length, beforeHydration, "Held settings must never be replaced by the initial default draft");
assert.equal(document.querySelector('.automation-run-action').disabled, true);
configReadMode = "ready";
await flush(() => heldConfigReads.shift()(structuredClone(saved)));
assert.equal(document.querySelector('button[role="switch"]').disabled, false);
assert.equal(document.querySelector('button[role="switch"]').getAttribute('aria-checked'), "true");
assert.equal(document.querySelector('.automation-run-action').disabled, false);
assert.deepEqual([...document.querySelectorAll('input[type="number"]')].slice(0, 2).map(input => input.value), ["1", "2"]);
assert.equal(document.querySelector('textarea').value, "Keep this confirmed reply");
assert.equal(mutations().length, beforeHydration, "Completing hydration only reads settings");

// A failed settings read must remain visible and locked even while later
// native status polls succeed. Only successful explicit Retry may unlock it.
bridge.profileId = "failed-hydration"; configReadMode = "failed";
const beforeFailure = mutations().length;
await flush(() => render(true, bridge.profileId));
assert.match(document.querySelector('[role="alert"]').textContent, /Confirmed Chat settings could not be loaded/);
assert.equal(document.querySelector('button[role="switch"]').disabled, true);
assert.equal(document.querySelector('.automation-run-action').disabled, true);
assert.equal(document.querySelector('fieldset').disabled, true);
const statusBefore = calls.filter(call => call.command === "feature018_chat_status").length;
await poll(); await poll();
assert.equal(calls.filter(call => call.command === "feature018_chat_status").length, statusBefore + 2);
assert.match(document.querySelector('[role="alert"]').textContent, /Confirmed Chat settings could not be loaded/);
assert.equal(document.querySelector('.automation-run-action').disabled, true);
assert.equal(mutations().length, beforeFailure);
configReadMode = "ready";
await flush(() => document.querySelector('[role="alert"] button').click());
assert.equal(document.querySelector('[role="alert"]'), null);
assert.equal(document.querySelector('.automation-run-action').disabled, false);
assert.equal(document.querySelector('textarea').value, "Keep this confirmed reply");
assert.equal(mutations().length, beforeFailure);

bridge.profileId = "A";
await flush(() => render(false));
assert.equal(document.querySelector('.automation-run-action').disabled, true);
await flush(() => render(true));
assert.equal(document.querySelector('.automation-run-action').disabled, false);
assert.match(document.body.textContent, /listening/);
await flush(() => document.querySelector('.automation-run-action').click());
assert.equal(calls.filter(c => c.command === "feature018_chat_run").length, 1);
await flush(() => mounted.unmount());
assert.equal(pollTimers.size, 0, "Unmount must stop native status polling");
globalThis.setInterval = nativeSetInterval; globalThis.clearInterval = nativeClearInterval;
dom.window.close();
console.log("R23_CHAT_ADAPTER_AND_MOUNTED_PASS: held/failed settings hydration gates, preserved native config, successful Retry, reconnect, Run once, polling cleanup");
