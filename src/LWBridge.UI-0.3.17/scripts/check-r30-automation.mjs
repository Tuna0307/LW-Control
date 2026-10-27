import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { chatRewardOutcomes } from "../src/locales/chat-reward-outcomes.js";
import { fileURLToPath } from "node:url";
import { retiredAutomationKey } from "./r30-retired-automation.mjs";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), React = req("react"), { build } = req("esbuild");
const dom = new JSDOM('<div id="root"></div>', { url: "http://localhost/" });
for (const key of ["window", "document", "HTMLElement", "Node", "MutationObserver", "localStorage", "Event", "MouseEvent"]) globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "en");
const { createRoot } = req("react-dom/client");
const retired = ["Auto Training", "Automatic Construction", "Free Stamina", "Trucks", "Automatic Alliance Train Boarding", "Building Resource Collection", "Armed Truck", "Weekend Shield", "Attack Shield"];
for (const file of ["AutomationPage.jsx", "previewAutomationContracts.js", "previewAutomationFixtures.js"]) {
  const source = fs.readFileSync(path.join(root, "src", file), "utf8");
  for (const title of retired) assert.ok(!source.includes(title), `${file}: ${title} retired`);
}
for (const code of ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"]) {
  const messages = (await import(`../src/locales/${code}.js`)).default;
  for (const [key, value] of Object.entries(messages)) {
    assert.ok(!retiredAutomationKey(key), `${code}: ${key} retired`);
    assert.ok(!retired.includes(value), `${code}: ${value} retired`);
  }
}
const bundle = await build({ stdin: { contents: 'export { NativeAutomationCard } from "./NativeAutomationCard.jsx"; export { NativeChatAutomationCard } from "./NativeChatAutomationCard.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const mod = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);
const { NativeChatAutomationCard, NativeAutomationCard, I18nProvider } = mod.exports;
const bridge = { profileId: "a", currentProfileOwner: () => ({ profileId: "a", generation: 1 }), isCurrentProfileOwner: o => o.profileId === "a", invokeProfileScoped: async command => {
  if (command === "feature018_chat_config_get") return { enabled: false };
  if (command === "feature018_chat_status") return { services: {} };
  throw new Error(`Unexpected action: ${command}`);
} };
const flush = async fn => React.act(async () => { if (fn) await fn(); await new Promise(resolve => setTimeout(resolve, 0)); });
for (const title of ["Red Packet", "Fireworks / Egg", "Gold Egg", "Treasure"]) {
  let mounted;
  const mount = async () => { mounted = createRoot(document.getElementById("root")); await flush(() => mounted.render(React.createElement(I18nProvider, null, React.createElement(NativeChatAutomationCard, { title, description: "", online: false, profileId: "a", availableSquads: [], bridge })))); await flush(); };
  await mount();
  let trigger = document.querySelector(".automation-config-trigger");
  assert.equal(trigger.getAttribute("aria-expanded"), "false");
  assert.equal(document.querySelector("fieldset").style.display, "none");
  await flush(() => trigger.click());
  assert.equal(trigger.getAttribute("aria-expanded"), "true");
  await flush(() => mounted.unmount());
  await mount();
  trigger = document.querySelector(".automation-config-trigger");
  assert.equal(trigger.getAttribute("aria-expanded"), "true", `${title} remembers expansion`);
  await flush(() => trigger.click());
  await flush(() => mounted.unmount());
}
let saved = { enabled: false }, failSave = false;
const calls = [];
const automationBridge = { profileId: "a", invokeProfileScoped: async (command, payload) => {
  calls.push(command);
  if (command === "feature018_automation_config_get") return structuredClone(saved);
  if (command === "feature018_automation_config_save") {
    if (failSave) throw new Error("Inert disk failure");
    saved = structuredClone(payload.config);return structuredClone(saved);
  }
  if (command === "feature018_automation_status") return { services: { allianceHelp: { enabled: saved.enabled, state: "disabled" } } };
  if (["feature018_automation_run", "feature018_automation_stop", "feature018_automation_configure"].includes(command)) return {};
  throw new Error(command);
} };
const mounted = createRoot(document.getElementById("root"));
await flush(() => mounted.render(React.createElement(I18nProvider, null, React.createElement(NativeAutomationCard, { title: "Alliance Help", description: "", online: true, profileId: "a", bridge: automationBridge }))));
await flush();
let toggle = document.querySelector('[data-native-automation=allianceHelp] button[role=switch]');
assert.equal(toggle.disabled, false);
await flush(() => toggle.click());
assert.deepEqual(calls.slice(-2), ["feature018_automation_config_save", "feature018_automation_configure"]);
assert.equal(saved.enabled, true);
await flush(() => toggle.click());
assert.deepEqual(calls.slice(-3), ["feature018_automation_stop", "feature018_automation_config_save", "feature018_automation_configure"]);
assert.equal(saved.enabled, false);
await flush(() => toggle.click());
failSave = true;
await flush(() => toggle.click());
assert.deepEqual(calls.slice(-2), ["feature018_automation_stop", "feature018_automation_config_save"], "Stop still precedes a failed disk save");
assert.ok(document.querySelector('[role=alert]'));
assert.ok(!document.querySelector('[data-preview-fixture]'), "Native card must never render fixtures");
await flush(() => mounted.unmount());
saved = { enabled: false, intervalMinutes: 120 };failSave = false;
const giftRoot = createRoot(document.getElementById("root"));
await flush(() => giftRoot.render(React.createElement(I18nProvider, null, React.createElement(NativeAutomationCard, { title: "Alliance Gifts", description: "", online: true, profileId: "a", bridge: automationBridge }))));
await flush();
const giftInput = document.querySelector('[data-native-automation=allianceGift] input[type=number]');
assert.equal(giftInput.value, "120");
const setValue = Object.getOwnPropertyDescriptor(dom.window.HTMLInputElement.prototype, "value").set;
await flush(() => { setValue.call(giftInput, "0");giftInput.dispatchEvent(new Event("input", { bubbles: true })); });
assert.equal(giftInput.getAttribute("aria-invalid"), "true", "Invalid gift interval remains an unsaved draft");
await flush(() => { setValue.call(giftInput, "37");giftInput.dispatchEvent(new Event("input", { bubbles: true })); });
await flush(() => giftInput.dispatchEvent(new dom.window.FocusEvent("focusout", { bubbles: true })));
assert.equal(saved.intervalMinutes, 37, "Gift settings persist as native numbers");
const giftToggle = document.querySelector('[data-native-automation=allianceGift] button[role=switch]');
await flush(() => giftToggle.click());
assert.equal(saved.enabled, true);
await flush(() => { setValue.call(giftInput, "0");giftInput.dispatchEvent(new Event("input", { bubbles: true })); });
const beforeStop = calls.length;
await flush(() => giftToggle.click());
assert.equal(calls[beforeStop], "feature018_automation_stop", "Off stops immediately even with an invalid unsaved interval");
assert.ok(document.querySelector('[role=alert]'));
await flush(() => giftRoot.unmount());
saved = { enabled: false, positionId: 0 };failSave = false;
const officialRoot = createRoot(document.getElementById("root"));
await flush(() => officialRoot.render(React.createElement(I18nProvider, null, React.createElement(NativeAutomationCard, { title: "Automatic Official Application", description: "", online: true, profileId: "a", bridge: automationBridge }))));
await flush();
const officialToggle = document.querySelector('[data-native-automation=officialPosition] button[role=switch]');
assert.equal(officialToggle.disabled, true, "Choose a position before enabling");
const position = document.querySelector('[data-native-automation=officialPosition] select');
assert.equal(position.options.length, 7);
await flush(() => { position.value = "10005";position.dispatchEvent(new Event("change", { bubbles: true })); });
assert.equal(saved.positionId, 10005);
assert.equal(officialToggle.disabled, false);
assert.ok(!document.querySelector('[data-native-automation=officialPosition] .automation-run-action'), "Original official card has no Run now");
await flush(() => officialRoot.unmount());
saved = { enabled: false, amountPerArmy: 1 };failSave = false;
const treatmentRoot = createRoot(document.getElementById("root"));
await flush(() => treatmentRoot.render(React.createElement(I18nProvider, null, React.createElement(NativeAutomationCard, { title: "Automatic Treatment", description: "", online: true, profileId: "a", bridge: automationBridge }))));
await flush();
const amount = document.querySelector('[data-native-automation=treatment] input[type=number]');
assert.equal(amount.value, "1");
await flush(() => { setValue.call(amount, "3");amount.dispatchEvent(new Event("input", { bubbles: true })); });
await flush(() => amount.dispatchEvent(new dom.window.FocusEvent("focusout", { bubbles: true })));
assert.equal(saved.amountPerArmy, 3);
const treatmentToggle = document.querySelector('[data-native-automation=treatment] button[role=switch]');
await flush(() => treatmentToggle.click());
assert.equal(saved.enabled, true);
await flush(() => { setValue.call(amount, "0");amount.dispatchEvent(new Event("input", { bubbles: true })); });
assert.equal(amount.getAttribute("aria-invalid"), "true");
const treatmentStop = calls.length;
await flush(() => treatmentToggle.click());
assert.equal(calls[treatmentStop], "feature018_automation_stop");
assert.ok(!document.querySelector('[data-native-automation=treatment] .automation-run-action'));
await flush(() => treatmentRoot.unmount());
for (const [state, field, label] of [["already_claimed", "alreadyClaimed", "Already claimed"],
  ["exhausted", "exhausted", "No rewards left"], ["expired", "expired", "Expired"],
  ["refused", "refused", "Refused"], ["unconfirmed", "unconfirmed", "Unconfirmed"]]) {
  const profileId = `outcome-${state}`;
  const outcomeBridge = { profileId, currentProfileOwner: () => ({ profileId, generation: 1 }),
    isCurrentProfileOwner: o => o.profileId === profileId, invokeProfileScoped: async command => {
      if (command === "feature018_chat_config_get") return { enabled: true };
      if (command === "feature018_chat_status") return { services: { fireworks: { state, claimed: 0, [field]: 1 } } };
      throw new Error(`Outcome display must not send a mutation: ${command}`);
    } };
  const outcomeRoot = createRoot(document.getElementById("root"));
  await flush(() => outcomeRoot.render(React.createElement(I18nProvider, null,
    React.createElement(NativeChatAutomationCard, { title: "Fireworks / Egg", description: "", online: true, profileId, bridge: outcomeBridge }))));
  await flush();
  assert.equal(document.querySelector(".automation-state").textContent, label);
  const rewardRow = [...document.querySelectorAll(".automation-status-row")].find(r => r.textContent.includes("New rewards confirmed"));
  assert.equal(rewardRow.querySelector("strong").textContent, "0", "Refusal/unknown must not look like a new reward");
  assert.equal(document.querySelector(".state-success"), null);
  await flush(() => outcomeRoot.unmount());
}
let goldSaved={enabled:true,legacyCombined:true};const goldCalls=[];
const goldBridge={profileId:'gold',currentProfileOwner:()=>({profileId:'gold',generation:1}),
  isCurrentProfileOwner:o=>o.profileId==='gold',invokeProfileScoped:async(command,payload)=>{
    goldCalls.push(command);
    if(command==='feature018_chat_config_get')return structuredClone(goldSaved);
    if(command==='feature018_chat_status')return {services:{goldEgg:{state:'waiting',claimed:0,crossServer:{enabled:true,eligibleRooms:0}}}};
    if(command==='feature018_chat_config_save'){assert.equal(payload.kind,'goldEgg');assert.equal('legacyCombined' in payload.config,false);
      goldSaved=structuredClone(payload.config);return structuredClone(goldSaved);}
    if(command==='feature018_chat_configure')return {};
    throw new Error(command);
  }};
const goldRoot=createRoot(document.getElementById('root'));
await flush(()=>goldRoot.render(React.createElement(I18nProvider,null,
  React.createElement(NativeChatAutomationCard,{title:'Gold Egg',description:'',online:true,profileId:'gold',bridge:goldBridge}))));
await flush();
await flush(()=>document.querySelector('.automation-config-trigger').click());
const goldOption=text=>[...document.querySelectorAll('[data-native-chat=goldEgg] button[role=switch]')].find(b=>b.textContent.includes(text));
assert.equal(goldOption('Cross-server Gold Egg').disabled,true,'Migration requires explicit separation');
assert.equal(goldOption('Skip birthday eggs').disabled,true);
assert.ok(document.body.textContent.includes(chatRewardOutcomes.en['chatRewards.eventUnavailable']));
const remoteRow=[...document.querySelectorAll('.automation-status-row')].find(r=>r.textContent.includes('Available cross-server rooms'));
assert.equal(remoteRow.querySelector('strong').textContent,'0','Zero live room coverage stays visible');
assert.equal(goldCalls.filter(c=>c==='feature018_chat_config_save').length,0,'Mount never saves migration');
await flush(()=>document.querySelector('[data-chat-separate-gold]').click());
await flush();
assert.equal(goldOption('Cross-server Gold Egg').disabled,false);
assert.equal(goldSaved.crossServerEnabled,false,'Separation does not enable remote discovery');
await flush(()=>goldOption('Cross-server Gold Egg').click());
await flush(()=>goldOption('Skip birthday eggs').click());
assert.equal(goldSaved.crossServerEnabled,true);assert.equal(goldSaved.ignoreBirthday,true);
assert.deepEqual(goldCalls.slice(-2),['feature018_chat_config_save','feature018_chat_configure']);
await flush(()=>goldRoot.unmount());
let retiredCalls=0;
const retiredBridge={profileId:'successor',invokeProfileScoped:async()=>{retiredCalls++;throw new Error('Retired card invoked successor');}};
const retiredRoot=createRoot(document.getElementById('root'));
await flush(()=>retiredRoot.render(React.createElement(I18nProvider,null,
  React.createElement(NativeChatAutomationCard,{title:'Gold Egg',description:'',online:true,profileId:'retired',bridge:retiredBridge}))));
await flush();assert.equal(retiredCalls,0);assert.ok(document.querySelector('[role=alert]'),'Retired card stays mounted with an error instead of throwing');
await flush(()=>retiredRoot.unmount());
for (const [language, labels] of Object.entries(chatRewardOutcomes)) {
  assert.equal(Object.keys(labels).length, 17, language);
  for (const text of Object.values(labels)) assert.ok(text.trim(), language);
}
dom.window.close();
console.log("FEATURE018_R30_REMOVALS_CHAT_COLLAPSE_PASS");
