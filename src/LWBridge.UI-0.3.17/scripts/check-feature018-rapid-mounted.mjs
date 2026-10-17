// FEATURE-018 native owned-march rapid item UI: inert mounted React regression.
// No actual game, app, account, network calls, scripts, or spending.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const require = createRequire(path.join(root, "package.json"));
const { JSDOM } = require("jsdom");
const { build } = require("esbuild");
const React = require("react");
const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: "http://127.0.0.1/feature018-rapid-inert",
});
for (const key of ["window", "document", "HTMLElement", "Element", "Event", "Node",
  "MutationObserver", "localStorage"]) {
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "zh-CN");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
const intervals = new Map();
let nextInterval = 1;
window.setInterval = (callback, delay) => {
  const id = nextInterval++;
  intervals.set(id, { callback, delay });
  return id;
};
window.clearInterval = (id) => intervals.delete(id);

let receiver;
const commands = [];
let dispatches = 0;
let statusReady = false;
let forceInventoryFailure = false;
let shieldReads = 0;
window.__LWBridgeBootstrap = {
  mode: "live", sessionId: "rapid-inert-session",
  profiles: { selectedProfileId: "rapid-approved-profile" },
};
window.chrome = { webview: {
  addEventListener(type, callback) { if (type === "message") receiver = callback; },
  removeEventListener() { receiver = undefined; },
  postMessage(request) {
    if (request.kind !== "invoke") return;
    commands.push(request);
    let response;
    if (request.command === "hotkey_config_get") {
      response = { attack: true, attackMarchSpeedupItem: false };
    } else if (request.command === "feature018_rapid_action") {
      switch (request.payload.mode) {
        case "inventory":
          assert.equal(request.payload.itemOptIn, undefined);
          assert.equal(request.payload.itemId, undefined);
          assert.equal(request.payload.slot, 1);
          response = { observation: "native_owned_rapid_item_candidates", slot: "1",
            marchUuid: "991102", items: [{ itemId: "30108", count: 2 }] };
          break;
        case "itemUse":
          assert.equal(request.payload.slot, 1);
          assert.equal(request.payload.itemId, "30108");
          assert.equal(request.payload.itemOptIn, true);
          dispatches++;
          response = { requestDispatched: true, awaitingGameConfirmation: true,
            slot: 1, marchUuid: "991102", itemId: "30108" };
          break;
        case "status":
          assert.equal(request.payload.itemOptIn, undefined);
          assert.equal(request.payload.itemId, undefined);
          response = statusReady ? {
            state: "confirmed", observation: "native_march_endTime_and_item_count_transition",
            slot: "1", marchUuid: "991102",
            priorEndTime: 3000, currentEndTime: 2700, priorCount: 2, currentCount: 1,
          } : {
            state: "pending", requestDispatched: false,
            observation: "native_rapid_item_request_unconfirmed", marchUuid: "991102",
          };
          break;
        default: throw Error("Unexpected rapid mode: " + request.payload.mode);
      }
    } else if (request.command === "feature018_shield_inventory_get") {
      assert.deepEqual(request.payload, { profileId: window.__LWBridgeBootstrap.profiles.selectedProfileId },
        "Read-only shield inventory may carry only the profile-scoped routing field");
      shieldReads++;
      response = { observation: "native_MAIN_SHIELD_StatusTab_owned_item_duration_join",
        durationMappingVerified: true, readOnly: true, requestDispatched: false,
        items: [{ itemId: "81008", uuid: "882013", count: 2,
          statusId: "7801", durationHours: 8, uniqueDuration: true },
          { itemId: "81024", uuid: "882024", count: 1,
            statusId: "7803", durationHours: 24, uniqueDuration: false }] };
    } else if (request.command === "feature018_equipment_preflight") {
      assert.equal(request.payload.slot, 1);
      assert.equal(request.payload.presetId, undefined);
      assert.equal(request.payload.authorizedUid, undefined);
      assert.equal(request.payload.confirmedEquipmentPresets, undefined);
      response = { observation: "native_HeroEquip_slot_inventory_preflight",
        readOnly: true, readyToApply: false, requestDispatched: false, slot: 1,
        presetId: "confirmed-native-preset", changedSlots: 1,
        wornByOtherHero: 0, uninstallSlots: 1,
        slots: [{ heroUuid: "12112", slot: 1, state: "replaceRequired" }],
      };
    } else throw Error("Unexpected native command: " + request.command);
    queueMicrotask(() => receiver?.({ data: {
      kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
      id: request.id, ok: !(request.payload?.mode === "inventory" && forceInventoryFailure),
      result: response, error: { code: "GAME_METHOD_UNAVAILABLE",
        message: "Native approved rapid inventory unavailable" },
    } }));
  },
} };

const bundle = await build({
  stdin: { contents: [
    'export { HotkeysPage } from "./HotkeyPages.jsx";',
    'export { I18nProvider } from "./i18n.jsx";',
    'export { backendBridge } from "./backendBridge.js";',
  ].join("\n"), resolveDir: path.join(root, "src"), loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs",
  jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const module = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(
  require, module, module.exports);
const { HotkeysPage, I18nProvider, backendBridge } = module.exports;
const { createRoot } = require("react-dom/client");
const mounted = createRoot(document.getElementById("root"));
const flush = (action) => React.act(async () => {
  if (action) await action();
  for (let i = 0; i < 30; i++) await Promise.resolve();
});
const tick = (delay) => flush(() => {
  for (const entry of [...intervals.values()]) {
    if (entry.delay === delay) entry.callback();
  }
});
const panel = () => document.querySelector("[data-feature018-rapid]");
const equipPanel = () => document.querySelector("[data-feature018-equipment-preflight]");
const shieldPanel = () => document.querySelector("[data-feature018-shield-inventory]");
const buttons = () => [...panel().querySelectorAll("button")];
const chooser = () => panel().querySelector('select[aria-label*="拥有的道具"]');
const consent = () => panel().querySelector('input[type="checkbox"]');

await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(HotkeysPage, { online: true }))));
assert.ok(panel(), "New rapid item workflow did not mount in Hotkeys");
assert.ok(equipPanel(), "Read-only original equipment preset inspection did not mount");
assert.ok(shieldPanel(), "Read-only native shield inventory control did not mount");
assert.equal(shieldReads, 0, "Mounting the Hotkeys page unexpectedly read native shield items");
await flush(() => shieldPanel().querySelector("button").click());
assert.equal(shieldReads, 1, "Explicit shield inventory inspect did not query game exactly once");
assert.match(shieldPanel().textContent, /8 小时/,
  "Native shield 8-hour duration missing from Chinese live panel");
assert.match(shieldPanel().textContent, /24 小时/,
  "Native shield 24-hour duration missing from Chinese live panel");
assert.match(shieldPanel().textContent, /有多个同持续时间道具/,
  "Ambiguous item duration was silently represented as safe to use");
assert.equal(dispatches, 0, "Inspecting a shield item spent another consumable");
assert.equal(commands.filter((r) => r.command === "feature018_rapid_action").length, 0,
  "Hotkeys mounting automatically used or inspected consumables");
assert.equal(commands.filter((r) => r.command === "feature018_equipment_preflight").length, 0,
  "Hotkeys mount must not claim to inspect or apply saved equipment");
await flush(() => equipPanel().querySelector("button").click());
assert.match(equipPanel().textContent, /已核对当前装备；尚未执行预设应用/,
  "Native equipment preflight was confused with applying the preset");
assert.equal(commands.filter((r) => r.command === "feature018_equipment_preflight").length, 1,
  "Explicit equipment inspection did not invoke its separate read-only native command");
assert.ok(!chooser(), "No item should be selectable before an authoritative inventory read");
await flush(() => buttons()[0].click());
assert.ok(chooser(), "Native owned items did not render in live Hotkeys");
assert.equal(consent().checked, false, "Native inventory silently authorized spending");
assert.equal(buttons()[1].disabled, true, "One-item send enabled without consent");
assert.equal(dispatches, 0, "Read-only inventory sent a WorldMarchRapid request");
await flush(() => consent().click());
assert.equal(buttons()[1].disabled, false, "Explicit one-item consent not honored");
await flush(() => buttons()[1].click());
assert.equal(dispatches, 1, "Approved item action must send exactly once");
assert.match(panel().textContent, /等待游戏行军时间和库存确认/,
  "Game accepted-only reply appeared as completed");
assert.doesNotMatch(panel().textContent, /已由游戏行军时间及道具库存共同确认/,
  "A dispatched item was reported as completed before the native poststate");
await tick(1500);
assert.equal(dispatches, 1, "Read-only native status re-sent a consumable");
statusReady = true;
await tick(1500);
assert.match(panel().textContent, /已由游戏行军时间及道具库存共同确认/,
  "Two source-based native poststates did not produce confirmation");
assert.equal(dispatches, 1, "Confirmation polling dispatched a second item");

backendBridge.setSelectedProfile("rapid-unapproved-second-profile");
await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(HotkeysPage, { online: true }))));
assert.doesNotMatch(shieldPanel().textContent, /8 小时/,
  "Other profile inherited previously inspected native shield inventory");
assert.ok(!chooser(), "Old owner's owned rapid item survived profile retirement");
assert.equal(dispatches, 1, "Profile retirement sent an item");
forceInventoryFailure = true;
await flush(() => buttons()[0].click());
assert.match(panel().querySelector('[role="alert"]')?.textContent || "",
  /GAME_METHOD_UNAVAILABLE/, "Native inventory error was hidden or represented as owned items");
assert.ok(!chooser(), "Failed live inventory reopened another owner's items");
await flush(() => mounted.unmount());
assert.equal(intervals.size, 0, "Owned march rapid status timers leaked on unmount");
backendBridge.dispose();
console.log("FEATURE018_RAPID_MOUNTED_OFFLINE_PASS consent=required send=1 poll=noResend sourcePoststate=confirmed profileRetired=fenced");
