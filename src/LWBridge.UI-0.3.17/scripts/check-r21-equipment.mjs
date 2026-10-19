// Inert native-shaped adapter and mounted production Equipment Schemes checks.
import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createEquipmentRuntime, equipmentPositionSnapshot, normalizeEquipmentConfig, setEquipmentSlot } from "../src/equipmentRuntime.js";
const baseline = {
  readOnly: true, ownerUid: "10001", serverId: 999,
  squads: [{ squadIndex: 1, positions: [
    { position: 2, heroUuid: "20002", heroItemId: 200, heroNameKey: "hero-native", equips: [{ slot: 1, equipUuid: "501" }] },
    { position: 5, heroUuid: "20005", heroItemId: 201, equips: [] },
  ] }],
  items: [{ equipUuid: "501", slot: 1, itemId: "301", nameKey: "gear-native", level: 3 }, { equipUuid: "502", slot: 1, itemId: "302", nameKey: "gear-spare", level: 4 }],
};
function fakeBridge() {
  let generation = 1, saved = { equipmentPresets: [] }, current = structuredClone(baseline), pending;
  const calls = [];
  const bridge = {
    available: true, profileId: "A", calls,
    currentProfileOwner: () => ({ profileId: bridge.profileId, generation }),
    isCurrentProfileOwner: (owner) => owner?.profileId === bridge.profileId && owner.generation === generation,
    retire() { generation += 1; },
    async invokeProfileScoped(command, payload = {}) {
      calls.push({ command, payload: structuredClone(payload) });
      if (command === "equipment_config_get") return structuredClone(saved);
      if (command === "equipment_config_save") { saved = structuredClone(payload); return structuredClone(saved); }
      if (command === "feature018_equipment_snapshot") return structuredClone(current);
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
const fresh = await runtime.snapshot();
const config = normalizeEquipmentConfig(await runtime.readConfig());
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

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."), req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), { build } = req("esbuild"), React = req("react");
const dom = new JSDOM('<div id="root"></div><div id="real-bridge-root"></div>', { url: "http://127.0.0.1/" });
for (const key of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
localStorage.setItem("lwbridge.language", "en");
const bundle = await build({ stdin: { contents: 'export { NativeEquipmentContent } from "./NativeEquipmentContent.jsx"; export { I18nProvider } from "./i18n.jsx"; export { createBackendBridge } from "./backendBridge.js";', resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const loaded = { exports: {} }; new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { NativeEquipmentContent, I18nProvider, createBackendBridge } = loaded.exports, mountedBridge = fakeBridge();
const { createRoot } = req("react-dom/client"), mounted = createRoot(document.getElementById("root"));
const flush = async (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 50; i++) await Promise.resolve(); });
await flush(() => mounted.render(React.createElement(I18nProvider, {}, React.createElement(NativeEquipmentContent, { profileId: "A", bridge: mountedBridge }))));
assert.equal(document.querySelectorAll(".equipment-preset-list button").length, 4);
assert.deepEqual([...document.querySelectorAll("[data-equipment-position]")].map((entry) => entry.dataset.equipmentPosition), ["1:2", "1:5"]);
assert.match(document.body.textContent, /Native Hero/);
assert.match(document.body.textContent, /Native Cannon/);
assert.doesNotMatch(document.body.textContent, /Fixture|Preview|Ember|Steel Vanguard/);
await flush(() => document.querySelector('[data-equipment-action="capture"]').click());
assert.equal(mountedBridge.saved.equipmentPresets[0].squads[0].positions[0].position, 2);
assert.equal(mountedBridge.saved.initialEquipmentConfig.ownerUid, baseline.ownerUid);
const select = document.querySelector('select[aria-label="1:2:1"]');
await flush(() => { Object.getOwnPropertyDescriptor(window.HTMLSelectElement.prototype, "value").set.call(select, "502"); select.dispatchEvent(new window.Event("change", { bubbles: true })); });
await flush(() => { document.querySelector('[data-equipment-action="apply"]').click(); });
await flush(() => new Promise((resolve) => setTimeout(resolve, 180)));
assert.equal(mountedBridge.saved.equipmentPresets[0].squads[0].positions[0].equips[0].equipUuid, "502");
assert.ok(mountedBridge.calls.some((call) => call.command === "feature018_hotkey_action" && call.payload.queryOnly));
await flush(() => document.querySelector('[data-equipment-action="restore"]').click());
await flush(() => new Promise((resolve) => setTimeout(resolve, 180)));
assert.deepEqual(equipmentPositionSnapshot(await mountedBridge.invokeProfileScoped("feature018_equipment_snapshot")), equipmentPositionSnapshot(baseline));
assert.equal(document.querySelector('[role="alert"]'), null);
await flush(() => mounted.unmount());

// Regression for the real WebView payload shape that exposed R22: the
// production bridge appends profileId to every profile-scoped call. Mount the
// actual component through createBackendBridge and require that exact wire
// payload for the equipment snapshot rather than an empty fake-adapter call.
let nativeReceiver;
const wireCalls = [];
const nativeHost = {
  __LWBridgeBootstrap: { mode: "live", sessionId: "r22-equipment-mounted", profiles: { selectedProfileId: "A" } },
  chrome: { webview: {
    addEventListener(type, callback) { if (type === "message") nativeReceiver = callback; },
    removeEventListener() {},
    postMessage(message) {
      if (message.kind !== "invoke") return;
      wireCalls.push(structuredClone(message));
      let result;
      if (message.command === "equipment_config_get") result = { equipmentPresets: [] };
      else if (message.command === "feature018_equipment_snapshot") result = structuredClone(baseline);
      else if (message.command === "lastwar_map_localize") result = { language: message.payload.language, buildId: "26", gameRoot: "C:/native-game", localeSha256: "selected-hash", englishSha256: "english-hash", texts: Object.fromEntries(message.payload.keys.map((key) => [key, key])) };
      else throw new Error(`Unexpected real-bridge command ${message.command}`);
      queueMicrotask(() => nativeReceiver({ data: { kind: "response", sessionId: "r22-equipment-mounted", id: message.id, ok: true, result } }));
    },
  } },
  setTimeout,
  clearTimeout,
};
const realBridge = createBackendBridge(nativeHost);
const realMounted = createRoot(document.getElementById("real-bridge-root"));
await flush(() => realMounted.render(React.createElement(I18nProvider, {}, React.createElement(NativeEquipmentContent, { profileId: "A", bridge: realBridge }))));
const wireSnapshot = wireCalls.find((call) => call.command === "feature018_equipment_snapshot");
assert.ok(wireSnapshot, "real bridge did not issue mounted equipment snapshot");
assert.deepEqual(wireSnapshot.payload, { profileId: "A" }, "mounted equipment snapshot must carry only the real profile routing field");
assert.equal(document.querySelector('#real-bridge-root [role="alert"]'), null);
assert.equal(document.querySelectorAll('#real-bridge-root [data-equipment-position]').length, 2);
await flush(() => realMounted.unmount()); realBridge.dispose(); dom.window.close();
console.log("FEATURE018_R21_EQUIPMENT_UI_OK: native gear/current positions, four saved ordinals, apply/poll, immutable owner baseline/restore, localization, profile-generation fence, real bridge profileId payload");
