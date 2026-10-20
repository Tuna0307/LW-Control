// Inert native-shaped adapter and mounted production Equipment Schemes checks.
import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createLiveEquipmentRuntime, createEquipmentRuntime, equipmentPositionSnapshot, initializeEquipmentPresets, normalizeEquipmentConfig, setEquipmentSlot, swapNativeEquipmentSquads, swapNativeEquipmentTarget } from "../src/equipmentRuntime.js";
import { equipmentPlanTexts } from "../src/locales/feature018-equipment-plan.js";
import { liveEquipmentTexts } from "../src/locales/feature018-equipment-live.js";
for (const language of ["en", "zh-CN", "zh-TW", "ja", "ko", "vi", "id", "ru", "pt"]) {
  assert.equal(Object.keys(equipmentPlanTexts[language]).length, 7);
  assert.equal(Object.keys(liveEquipmentTexts[language]).length, 7);
  for (const value of Object.values(liveEquipmentTexts[language])) assert.ok(value.length > 0);
  for (const text of Object.values(equipmentPlanTexts[language])) assert.ok(text.length > 0);
  assert.ok(equipmentPlanTexts[language]["squad.equipmentPlanOnly"].includes("{number}"));
  assert.ok(equipmentPlanTexts[language]["squad.equipmentUnappliedCount"].includes("{count}"));
}
const baseline = {
  readOnly: true, ownerUid: "10001", serverId: 999,
  squads: [{ squadIndex: 1, positions: [
    { position: 2, heroUuid: "20002", heroItemId: 200, heroNameKey: "hero-native", iconPath: "Assets/Hero/native.png", equips: [{ slot: 1, equipUuid: "501" }] },
    { position: 5, heroUuid: "20005", heroItemId: 201, equips: [] },
  ] }],
  items: [{ equipUuid: "501", slot: 1, itemId: "301", nameKey: "gear-native", iconPath: "Assets/Item/native.png", level: 3 }, { equipUuid: "502", slot: 1, itemId: "302", nameKey: "gear-spare", level: 4 }],
};
function fakeBridge() {
  let generation = 1, saved = { equipmentPresets: [] }, current = structuredClone(baseline), pending;
  const calls = [];
  const bridge = {
    available: true, online: true, profileId: "A", calls,
    currentProfileOwner: () => ({ profileId: bridge.profileId, generation }),
    isCurrentProfileOwner: (owner) => owner?.profileId === bridge.profileId && owner.generation === generation,
    retire() { generation += 1; },
    async invokeProfileScoped(command, payload = {}) {
      calls.push({ command, payload: structuredClone(payload) });
      if (command === "equipment_config_get") return structuredClone(saved);
      if (command === "equipment_config_save") {
        if (bridge.saveLocked) throw Object.assign(new Error("Access to the path is denied"), { code: "CONFIG_FILE_BUSY" });
        saved = structuredClone(payload); return structuredClone(saved);
      }
      if (command === "feature018_equipment_snapshot") {
        if (!bridge.online) throw Object.assign(new Error("There is no authenticated owned game session"), { code: "GAME_CONNECTION_UNAVAILABLE" });
        return structuredClone(current);
      }
      if (command === "lastwar_map_localize") return { language: payload.language, buildId: "26", gameRoot: "C:/native-game", localeSha256: "selected-hash", englishSha256: "english-hash", texts: Object.fromEntries(payload.keys.map((key) => [key, { "hero-native": "Native Hero", "gear-native": "Native Cannon", "gear-spare": "Native Spare" }[key] || key])) };
      if (command === "feature018_hotkey_action" || command === "feature018_equipment_restore") {
        if (!payload.queryOnly) {
          assert.equal(pending, undefined, "Mutation must not be repeated while awaiting confirmation");
          pending = command === "feature018_equipment_restore" ? saved.initialEquipmentConfig.squads : saved.equipmentPresets[payload.slot - 1].squads;
          return { equipmentApplied: false };
        }
        assert.ok(pending);
        for (const squad of current.squads) for (const position of squad.positions)
          position.equips = structuredClone(pending.find((entry) => entry.squadIndex === squad.squadIndex).positions.find((entry) => entry.position === position.position).equips);
        pending = undefined;
        return { equipmentApplied: true, completedSteps: 1, plannedSteps: 1 };
      }
      throw new Error(`Unexpected command ${command}`);
    },
    get saved() { return saved; },
  };
  return bridge;
}
const bridge = fakeBridge(), runtime = createEquipmentRuntime(bridge, "A", { wait: async () => {} });
const fourSlot = equipmentPositionSnapshot({ squads: [{ squadIndex: 4, positions: [{
  position: 3, heroUuid: "90003", equips: [1, 2, 3, 4].map((slot) => ({ slot, equipUuid: String(800 + slot) })),
}] }] });
assert.deepEqual(fourSlot[0].positions[0].equips.map((entry) => entry.slot), [1, 2, 3, 4], "Save current squads must capture all four native slot types");
const dragConfig = normalizeEquipmentConfig({ equipmentPresets: [{ id: "drag", name: "Drag", squads: [
  { squadIndex: 1, positions: [{ position: 1, heroUuid: "h11", equips: [{ slot: 1, equipUuid: "a1" }, { slot: 2, equipUuid: "a2" }] }] },
  { squadIndex: 2, positions: [{ position: 1, heroUuid: "h21", equips: [{ slot: 1, equipUuid: "b1" }, { slot: 2, equipUuid: "b2" }] }] },
] }] });
const dragSnapshot = { squads: [
  { squadIndex: 1, positions: [{ position: 1, heroUuid: "h11", equips: [] }] },
  { squadIndex: 2, positions: [{ position: 1, heroUuid: "h21", equips: [] }] },
] };
const wrongSlot = swapNativeEquipmentTarget(dragConfig, 0,
  { kind: "equip", squadIndex: 1, position: 1, slot: 1 },
  { kind: "equip", squadIndex: 2, position: 1, slot: 2 });
assert.equal(wrongSlot.error, "squad.sameSlotRequired");
assert.deepEqual(wrongSlot.config, dragConfig, "Wrong-slot drag must be a no-op");
const crossHero = swapNativeEquipmentTarget(dragConfig, 0,
  { kind: "equip", squadIndex: 1, position: 1, slot: 1 },
  { kind: "equip", squadIndex: 2, position: 1, slot: 1 });
assert.equal(crossHero.config.equipmentPresets[0].squads[0].positions[0].equips[0].equipUuid, "b1");
assert.equal(crossHero.config.equipmentPresets[0].squads[1].positions[0].equips[0].equipUuid, "a1");
const crossSet = swapNativeEquipmentTarget(dragConfig, 0,
  { kind: "loadout", squadIndex: 1, position: 1 },
  { kind: "loadout", squadIndex: 2, position: 1 });
assert.deepEqual(crossSet.config.equipmentPresets[0].squads[0].positions[0], { position: 1, heroUuid: "h11", equips: [{ slot: 1, equipUuid: "b1" }, { slot: 2, equipUuid: "b2" }] });
assert.deepEqual(crossSet.config.equipmentPresets[0].squads[1].positions[0], { position: 1, heroUuid: "h21", equips: [{ slot: 1, equipUuid: "a1" }, { slot: 2, equipUuid: "a2" }] });
const squadSwap = swapNativeEquipmentSquads(dragConfig, 0, 1, 2, dragSnapshot);
assert.equal(squadSwap.handled, true);
assert.equal(squadSwap.config.equipmentPresets[0].squads[0].positions[0].heroUuid, "h11", "Squad drag must keep source hero fixed");
assert.equal(squadSwap.config.equipmentPresets[0].squads[1].positions[0].heroUuid, "h21", "Squad drag must keep target hero fixed");
assert.deepEqual(squadSwap.config.equipmentPresets[0].squads[0].positions[0].equips.map((entry) => entry.equipUuid), ["b1", "b2"]);
assert.deepEqual(squadSwap.config.equipmentPresets[0].squads[1].positions[0].equips.map((entry) => entry.equipUuid), ["a1", "a2"]);
const fresh = await runtime.snapshot();
const config = normalizeEquipmentConfig(await runtime.readConfig());
const editedFresh = setEquipmentSlot(config, 0, 1, 5, 1, "502", fresh);
assert.deepEqual(editedFresh.equipmentPresets[0].squads[0].positions.map((position) => [position.position, position.heroUuid, position.equips]), [[2, "20002", [{ slot: 1, equipUuid: "501" }]], [5, "20005", [{ slot: 1, equipUuid: "502" }]]]);
const autoFilled = initializeEquipmentPresets(config, fresh);
for (const preset of autoFilled.equipmentPresets) assert.deepEqual(preset.squads, equipmentPositionSnapshot(fresh));
const intentionallyEmpty = setEquipmentSlot(autoFilled, 0, 1, 2, 1, "", fresh);
assert.deepEqual(initializeEquipmentPresets(intentionallyEmpty, fresh), intentionallyEmpty, "Never refill a deliberately emptied initialized plan");
assert.deepEqual(config.equipmentPresets[0].squads, [], "Editing an empty preset must not mutate the source config");
config.equipmentPresets[0].squads = equipmentPositionSnapshot(fresh);
const moved = setEquipmentSlot(config, 0, 1, 5, 1, "501");
assert.deepEqual(moved.equipmentPresets[0].squads[0].positions.map((position) => position.position), [2, 5], "Native position gaps must be retained");
assert.deepEqual(moved.equipmentPresets[0].squads[0].positions[0].equips, [], "A gear UUID cannot be duplicated in two saved positions");
const result = await runtime.apply(1, moved, fresh);
assert.equal(result.result.equipmentApplied, true);
assert.equal(bridge.saved.initialEquipmentConfig.ownerUid, baseline.ownerUid);
assert.equal(bridge.saved.initialEquipmentConfig.serverId, baseline.serverId);
assert.deepEqual(bridge.saved.initialEquipmentConfig.squads, equipmentPositionSnapshot(baseline));
const applyCalls = bridge.calls.filter((call) => call.command === "feature018_hotkey_action");
assert.deepEqual(applyCalls.map((call) => call.payload), [{ action: "equipment", slot: 1 }, { action: "equipment", slot: 1, queryOnly: true }]);
const savedBeforeRestore = structuredClone(bridge.saved.equipmentPresets);
await runtime.restore();
assert.deepEqual(bridge.saved.equipmentPresets, savedBeforeRestore, "Restore must preserve all four user presets");
assert.deepEqual(bridge.calls.filter((call) => call.command === "feature018_equipment_restore").map((call) => call.payload), [{}, { queryOnly: true }]);
assert.deepEqual(equipmentPositionSnapshot(await runtime.snapshot()), equipmentPositionSnapshot(baseline));
const interruptedBridge = fakeBridge(), invokeInterrupted = interruptedBridge.invokeProfileScoped.bind(interruptedBridge);
let queryGap = true;
interruptedBridge.invokeProfileScoped = async (command, payload) => {
  if (command === "feature018_hotkey_action") {
    if (!payload.queryOnly) {
      await invokeInterrupted(command, payload);
      throw Object.assign(new Error("Heartbeat gap after accepted native step"), { code: "GAME_CONNECTION_UNAVAILABLE" });
    }
    if (queryGap) { queryGap = false; throw Object.assign(new Error("Brief heartbeat gap"), { code: "GAME_DISCONNECTED" }); }
  }
  return invokeInterrupted(command, payload);
};
const interrupted = createEquipmentRuntime(interruptedBridge, "A", { wait: async () => {} });
assert.equal((await interrupted.apply(1, moved, fresh)).result.equipmentApplied, true);
assert.equal(interruptedBridge.calls.filter((call) => call.command === "feature018_hotkey_action" && !call.payload.queryOnly).length, 1, "Heartbeat recovery must never resend Apply");
const renamed = structuredClone(bridge.saved); renamed.equipmentPresets[0].name = "Saved name";
await runtime.save(renamed, { ...baseline, ownerUid: "wrong", squads: [] });
assert.deepEqual(bridge.saved.initialEquipmentConfig.squads, equipmentPositionSnapshot(baseline), "Rename must retain the immutable original baseline");
// A save acknowledgement arriving after a profile generation retires must never
// send an apply into the successor profile, even if the visible ID stays the same.
const raceBridge = fakeBridge(), originalInvoke = raceBridge.invokeProfileScoped.bind(raceBridge);
raceBridge.invokeProfileScoped = async (command, payload) => { const value = await originalInvoke(command, payload); if (command === "equipment_config_save") raceBridge.retire(); return value; };
const raced = createEquipmentRuntime(raceBridge, "A", { wait: async () => {} });
await assert.rejects(() => raced.apply(1, config, baseline), (error) => error.code === "PROFILE_GENERATION_RETIRED");
assert.equal(raceBridge.calls.filter((call) => call.command === "feature018_hotkey_action").length, 0);

// An ambiguous local publication must never be replayed. Explicit native
// confirmation of no transaction releases the fence for a later user gesture.
const noTransactionBridge = fakeBridge();
let starts = 0;
noTransactionBridge.invokeProfileScoped = async (command, payload) => {
  if (!payload.queryOnly) { starts += 1; throw Object.assign(new Error("Local publication failed"), { code: "INTERNAL_ERROR" }); }
  throw Object.assign(new Error("No matching live equipment change is pending"), { code: "EQUIPMENT_EDIT_REJECTED" });
};
const noTransaction = createLiveEquipmentRuntime(noTransactionBridge, "A", { wait: async () => {} });
await assert.rejects(() => noTransaction.edit(baseline.squads[0].positions[0], 1, "502", baseline));
assert.equal(noTransaction.pending, true);
await assert.rejects(() => noTransaction.resume());
assert.equal(noTransaction.pending, false);
assert.equal(starts, 1, "Failed publication may only be queried, never automatically replayed");

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."), req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), { build } = req("esbuild"), React = req("react");
const dom = new JSDOM('<div id="root"></div><div id="real-bridge-root"></div>', { url: "http://127.0.0.1/" });
for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
localStorage.setItem("lwbridge.language", "en");
const bundle = await build({ stdin: { contents: 'export { NativeEquipmentContent } from "./NativeEquipmentContent.jsx"; export { GameAssetImageProvider } from "./GameAssetImage.jsx"; export { I18nProvider } from "./i18n.jsx"; export { createBackendBridge } from "./backendBridge.js";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const loaded = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { NativeEquipmentContent, GameAssetImageProvider, I18nProvider, createBackendBridge } = loaded.exports;
const current = structuredClone(baseline);
current.items[0].heroUuid = "20002"; current.items[1].heroUuid = "";
const calls = []; let pending, retired = false;
const liveBridge = {
  available: true, profileId: "A", currentProfileOwner: () => ({ profileId: "A", generation: 1 }),
  isCurrentProfileOwner: () => !retired,
  async invokeProfileScoped(command, payload = {}) {
    calls.push({ command, payload: structuredClone(payload) });
    if (command === "feature018_equipment_snapshot") return structuredClone(current);
    if (command === "lastwar_map_localize") return { language: payload.language, buildId: "26", gameRoot: "native", localeSha256: "locale", englishSha256: "english", texts: Object.fromEntries(payload.keys.map(key => [key, { "hero-native": "Native Hero", "gear-native": "Native Cannon", "gear-spare": "Native Spare" }[key] || key])) };
    if (command !== "feature018_equipment_edit") throw new Error(`Unexpected live command ${command}`);
    if (!payload.queryOnly) {
      assert.equal(pending, undefined, "A second edit may not overtake pending confirmation");
      pending = structuredClone(payload); return { equipmentApplied: false };
    }
    assert.equal(payload.operationId, pending.operationId);
    const target = current.squads.flatMap(s => s.positions).find(p => p.heroUuid === pending.heroUuid);
    for (const position of current.squads.flatMap(s => s.positions))
      position.equips = position.equips.filter(e => !(position === target && e.slot === pending.equipSlot) && (!pending.equipUuid || e.equipUuid !== pending.equipUuid));
    if (pending.equipUuid) target.equips.push({ slot: pending.equipSlot, equipUuid: pending.equipUuid });
    for (const item of current.items) item.heroUuid = current.squads.flatMap(s => s.positions).find(p => p.equips.some(e => e.equipUuid === item.equipUuid))?.heroUuid || "";
    pending = undefined; return { equipmentApplied: true };
  },
};
const { createRoot } = req("react-dom/client"), mounted = createRoot(document.getElementById("root"));
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 50; i++) await Promise.resolve(); });
let tick;
window.setInterval = (fn) => { tick = fn; return 1; }; window.clearInterval = () => { tick = undefined; };
await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(NativeEquipmentContent, { profileId: "A", bridge: liveBridge }))));
assert.equal(document.querySelectorAll('select').length, 8);
assert.equal(document.querySelector('select[aria-label="1:2:1"]').value, "501");
assert.doesNotMatch(document.body.textContent, /Plan only|Save configuration|Restore initial|Save current squads|Preset 1/);
assert.equal(document.querySelector('[data-equipment-sync]').dataset.equipmentSync, "synced");
const selectValue = async (label, value) => flush(() => {
  const select = document.querySelector(`select[aria-label="${label}"]`);
  Object.getOwnPropertyDescriptor(window.HTMLSelectElement.prototype, "value").set.call(select, value);
  select.dispatchEvent(new window.Event("change", { bubbles: true }));
});
const settle = () => flush(() => new Promise(resolve => setTimeout(resolve, 190)));
await selectValue("1:5:1", "502");
assert.ok([...document.querySelectorAll('select')].every(s => s.disabled), "Pending edit must block overlapping gestures");
await settle();
assert.equal(current.squads[0].positions[1].equips[0].equipUuid, "502", "Dropdown must change game immediately without Save");
assert.equal(document.querySelector('select[aria-label="1:5:1"]').value, "502");
const source = document.querySelector('[data-equipment-slot="1:2:1"]'), target = document.querySelector('[data-equipment-slot="1:5:1"]');
await flush(() => source.dispatchEvent(new window.Event("dragstart", { bubbles: true })));
await flush(() => target.dispatchEvent(new window.Event("dragover", { bubbles: true, cancelable: true })));
assert.ok(target.classList.contains("drop-target"));
const beforeInvalid = calls.length;
await flush(() => document.querySelector('[data-equipment-slot="1:5:2"]').dispatchEvent(new window.Event("drop", { bubbles: true, cancelable: true })));
assert.match(document.querySelector('[role="alert"]').textContent, /same slot/i);
assert.equal(calls.length, beforeInvalid, "Wrong-slot drop must not call native commands");
await flush(() => target.dispatchEvent(new window.Event("drop", { bubbles: true, cancelable: true })));
await settle();
assert.equal(current.squads[0].positions[0].equips.length, 0, "Dragging must unequip the source hero");
assert.equal(current.squads[0].positions[1].equips[0].equipUuid, "501");
assert.equal(current.items.find(i => i.equipUuid === "502").heroUuid, "", "Replaced gear returns to inventory instead of swapping");
await selectValue("1:5:1", ""); await settle();
assert.equal(current.squads[0].positions[1].equips.length, 0, "Empty must immediately unequip in game");
assert.equal(document.querySelectorAll('[data-inventory-equip]').length, 2);
current.squads[0].positions[0].equips = [{ slot: 1, equipUuid: "502" }]; current.items[1].heroUuid = "20002";
await flush(() => tick());
assert.equal(document.querySelector('select[aria-label="1:2:1"]').value, "502", "Game-side changes must automatically appear without Refresh");
assert.equal(calls.filter(c => c.command === "equipment_config_get" || c.command === "equipment_config_save" || c.command === "feature018_hotkey_action").length, 0, "Live UI must never load, save or apply plans");
await flush(() => mounted.unmount()); assert.equal(tick, undefined);

let nativeReceiver; const wireCalls = [];
const host = { __LWBridgeBootstrap: { mode: "live", sessionId: "equipment-live", profiles: { selectedProfileId: "A" } }, chrome: { webview: {
 addEventListener(type, callback) { if (type === "message") nativeReceiver = callback; }, removeEventListener() {},
 postMessage(message) {
  if (message.kind !== "invoke") return; wireCalls.push(message);
  liveBridge.invokeProfileScoped(message.command, message.payload).then(result => queueMicrotask(() => nativeReceiver({ data: { kind: "response", sessionId: "equipment-live", id: message.id, ok: true, result } })));
 }
} }, setTimeout, clearTimeout };
const realBridge = createBackendBridge(host), realRoot = createRoot(document.getElementById("real-bridge-root"));
await flush(() => realRoot.render(React.createElement(I18nProvider, {}, React.createElement(NativeEquipmentContent, { profileId: "A", bridge: realBridge }))));
assert.deepEqual(wireCalls.find(c => c.command === "feature018_equipment_snapshot").payload, { profileId: "A" }, "mounted equipment snapshot must carry only the real profile routing field");
await flush(() => realRoot.unmount()); realBridge.dispose(); dom.window.close();
console.log("FEATURE018_R26_LIVE_EQUIPMENT_UI_OK: immediate equip/remove/move, no plan controls or config writes, displaced inventory, automatic game refresh, pending fence, real bridge routing");
