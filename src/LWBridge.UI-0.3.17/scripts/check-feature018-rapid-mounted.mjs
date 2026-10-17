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
let equipmentActions = 0;
let equipmentQuerySteps = 0;
let shieldGold = 176;
let shieldCount = 0;
let shieldShopSends = 0;
let shieldUseSends = 0;
let shieldShopConfirmed = false;
let shieldUseConfirmed = false;
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
    } else if (request.command === "feature018_attack_prepare") {
      assert.deepEqual(request.payload,
        { mode: "inventory", profileId: window.__LWBridgeBootstrap.profiles.selectedProfileId });
      response = { observation: "native_ordinary_monsters_and_owned_squads",
        requestDispatched: false, staffingPreparationPerformed: false,
        candidates: [], unassignedSoldiers: { observed: true, total: 120 },
        squads: { "1": { unlocked: true, heroCount: 5, soldierCount: 0,
          formationCapacity: 280, staffedForAttack: false,
          canPrepareWithUnassignedSoldiers: true },
          "2": { unlocked: true, heroCount: 5, soldierCount: 0, formationCapacity: 300,
            staffedForAttack: false, canPrepareWithUnassignedSoldiers: true },
          "3": { unlocked: false }, "4": { unlocked: false } },
      };
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
        // Current v26 ItemUse PutUtfString UUID, independently observed on
        // the 2289 account; it is not constrained to a 20-digit number.
        items: [{ itemId: "81008", uuid: "e43623c3ec5e4cd78ae1b3f904094fd7", count: shieldCount || 2,
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
    } else if (request.command === "feature018_shield_shop") {
      assert.equal(request.payload.slot === 8 || request.payload.slot === 12, true);
      const duration = request.payload.slot;
      const price = duration === 8 ? 1500 : 2500;
      const itemId = duration === 8 ? "81008" : "81012";
      response = { observation: "native_shield_shop_inventory",
        itemId, durationHours: duration, nativePrice: price, gold: shieldGold,
        count: shieldCount, readOnly: true, requestDispatched: false,
        canAffordNativePrice: shieldGold >= price,
        additionalGoldNeeded: Math.max(0, price-shieldGold),
        nativeGoldClaimRoutes: { readOnly: true, requestDispatched: false,
          goldEarned: false, goldAmountVerified: false,
          availability: "native_observed", claimableDailyTaskCount: 2,
          goldTypeMatchedDailyTaskCount: 1,
          signIn: { availability: "native_cached", claimableEventCount: 1 } },
      };
      if (request.payload.mode === "buy") {
        assert.equal(request.payload.gemOptIn, true);
        assert.equal(duration, 8);
        shieldShopSends++;
        response = { ...response, readOnly: false, requestDispatched: true,
          awaitingGameConfirmation: true };
      } else if (request.payload.mode === "status") {
        assert.equal(request.payload.gemOptIn, undefined);
        response = shieldShopConfirmed ? {
          ...response, observation: "native_shield_purchase_inventory_and_gold",
          beforeCount: 0, beforeGold: 3000, spentGems: 1500, purchasedCount: 1,
        } : { ...response, awaitingGameConfirmation: true };
      } else assert.equal(request.payload.mode, "inspect");
    } else if (request.command === "feature018_hotkey_action" &&
        request.payload.action === "shieldUse") {
      assert.equal(request.payload.slot, 8);
      if (request.payload.queryOnly === true) {
        response = shieldUseConfirmed ? {
          observation: "native_owned_shield_consumption_and_MAIN_SHIELD",
          slot: 8, itemId: "81008", consumedCount: 1,
          durationMilliseconds: 28800000, remainingMilliseconds: 28799000,
        } : { awaitingGameConfirmation: true, requestDispatched: false, slot: 8 };
      } else {
        shieldUseSends++;
        response = { requestDispatched: true, awaitingGameConfirmation: true, slot: 8 };
      }
    } else if (request.command === "feature018_hotkey_action") {
      assert.equal(request.payload.action, "equipment");
      assert.equal(request.payload.slot, 1);
      assert.equal(request.payload.presetId, undefined,
        "Equipment UI must never provide a forged persisted preset ID");
      assert.equal(request.payload.authorizedUid, undefined,
        "Equipment UI must never select the native game account");
      assert.equal(request.payload.confirmedEquipmentPresets, undefined,
        "Equipment UI must never supply equipment UUIDs");
      if (request.payload.queryOnly === true) {
        equipmentQuerySteps++;
        response = equipmentQuerySteps >= 2 ? {
          observation: "native_HeroEquip_preset_slots_applied", equipmentApplied: true,
          action: "equipment", slot: 1, presetId: "confirmed-native-preset",
          completedSteps: 2, plannedSteps: 2,
          slots: [{ currentEquipUuid: "991", targetEquipUuid: "991" }],
        } : { observation: "native_HeroEquip_preset_pending", equipmentApplied: false,
          awaitingGameConfirmation: true, action: "equipment", slot: 1,
          presetId: "confirmed-native-preset", completedSteps: 1, plannedSteps: 2 };
      } else {
        equipmentActions++;
        response = { observation: "native_HeroEquip_preset_pending", equipmentApplied: false,
          awaitingGameConfirmation: true, action: "equipment", slot: 1,
          presetId: "confirmed-native-preset", completedSteps: 0, plannedSteps: 2 };
      }
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
const attackPanel = () => document.querySelector("[data-feature018-attack-readiness]");
const shopPanel = () => document.querySelector("[data-feature018-shield-shop]");
const buttons = () => [...panel().querySelectorAll("button")];
const chooser = () => panel().querySelector('select[aria-label*="拥有的道具"]');
const consent = () => panel().querySelector('input[type="checkbox"]');

await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(HotkeysPage, { online: true }))));
assert.ok(panel(), "New rapid item workflow did not mount in Hotkeys");
assert.ok(equipPanel(), "Read-only original equipment preset inspection did not mount");
assert.ok(shieldPanel(), "Read-only native shield inventory control did not mount");
assert.ok(attackPanel(), "Native QWER owner staffing panel missing");
assert.equal(commands.filter((r) => r.command === "feature018_attack_prepare").length, 0,
  "Native attack readiness automatically ran or changed a formation on mount");
await flush(() => attackPanel().querySelector("button").click());
assert.match(attackPanel().textContent, /空闲士兵: 120/, "Native available soldier count not visible");
assert.match(attackPanel().textContent, /已编入士兵: 0/, "Native zero-staffed slot not shown");
assert.match(attackPanel().textContent, /还没有可核实的出征编队/, "Zero soldiers reported as attack-ready");
assert.equal(commands.filter((r) => r.command === "feature018_attack_prepare").length, 1,
  "QWER read-only readiness initiated more than one source observation");
assert.ok(shopPanel(), "Owner-gated shield purchase/use control did not mount");
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
assert.equal(shieldShopSends, 0, "Native shield shop auto-purchased on mount/inventory");
const shopButtons = () => [...shopPanel().querySelectorAll("button")];
const selectDuration = (value) => {
  const combo = shopPanel().querySelector("select");
  combo.value = String(value);
  combo.dispatchEvent(new window.Event("change", { bubbles: true }));
};
await flush(() => selectDuration(12));
await flush(() => shopButtons()[0].click());
assert.match(shopPanel().textContent, /2324/,
  "Read-only 12h purchase did not report native gold deficit");
assert.match(shopPanel().textContent, /奖励类型含金币的任务: 1/,
  "Read-only native Gold reward class was hidden despite source eligibility");
assert.match(shopPanel().textContent, /金币数量、实际领取及服务器入账均未核实/,
  "Cached reward eligibility was promoted to a real gold amount or claim");
assert.equal(shopPanel().querySelector('input[type="checkbox"]'), null,
  "Insufficient gold exposed a purchase consent action");
assert.equal(shieldShopSends, 0);
shieldGold = 3000;
await flush(() => selectDuration(8));
await flush(() => shopButtons()[0].click());
const shopConsent = shopPanel().querySelector('input[type="checkbox"]');
assert.ok(shopConsent, "Affordable shield inventory preflight had no explicit consent");
assert.equal(shopButtons()[1].disabled, true, "Gold spend opened without consent");
await flush(() => shopConsent.click());
await flush(() => shopButtons()[1].click());
assert.equal(shieldShopSends, 1, "Native purchase did not send exactly once");
assert.ok(shopPanel().textContent.includes("不得重复发送"),
  "Pending native purchase did not display no-retry warning");
shieldShopConfirmed = true;
shieldCount = 1; shieldGold = 1500;
await flush(() => shopButtons().at(-1).click());
assert.equal(shieldShopSends, 1, "Read-only purchase status retriggered shop");
assert.match(shopPanel().textContent, /游戏库存\+1及金币扣费确认/,
  "Gold and item count native poststate did not confirm purchase");
await flush(() => shopButtons()[0].click());
assert.ok(shopPanel().querySelector('input[type="checkbox"]'),
  "Owned shield missing separate consumption consent");
assert.equal(shopButtons()[1].disabled, true, "ItemUse enabled without separate consent");
await flush(() => shopPanel().querySelector('input[type="checkbox"]').click());
await flush(() => shopButtons()[1].click());
assert.equal(shieldUseSends, 1, "Owned shield ItemUse must dispatch only once");
shieldUseConfirmed = true; shieldCount = 0;
await flush(() => shopButtons().at(-1).click());
assert.equal(shieldUseSends, 1, "Owned shield poststate check repeated item use");
assert.match(shopPanel().textContent, /道具减少及 MAIN_SHIELD 生效均已确认/,
  "Native ItemUse did not require item and MAIN_SHIELD proof");
assert.equal(commands.filter((r) => r.command === "feature018_rapid_action").length, 0,
  "Hotkeys mounting automatically used or inspected consumables");
assert.equal(commands.filter((r) => r.command === "feature018_equipment_preflight").length, 0,
  "Hotkeys mount must not claim to inspect or apply saved equipment");
await flush(() => equipPanel().querySelector("button").click());
assert.match(equipPanel().textContent, /已核对当前装备；尚未执行预设应用/,
  "Native equipment preflight was confused with applying the preset");
assert.equal(commands.filter((r) => r.command === "feature018_equipment_preflight").length, 1,
  "Explicit equipment inspection did not invoke its separate read-only native command");
const equipConsent = equipPanel().querySelector('input[type="checkbox"]');
assert.ok(equipConsent, "Source-confirmed equipment preflight exposed no explicit apply consent");
assert.equal(equipmentActions, 0, "Inspecting native equipment sent an install");
await flush(() => equipConsent.click());
await flush(() => [...equipPanel().querySelectorAll("button")].at(-1).click());
assert.equal(equipmentActions, 1, "Explicit apply must begin exactly one native transaction");
assert.match(equipPanel().textContent, /正在等待游戏确认/, "Pending equipment install falsely showed success");
for (let i = 0; i < 2; i++) {
  await flush(() => [...equipPanel().querySelectorAll("button")].at(-1).click());
}
assert.equal(equipmentActions, 1, "Native verification resent the initial install");
assert.equal(equipmentQuerySteps, 2, "Native equipment sequence did not progress in exact status order");
assert.match(equipPanel().textContent, /已核对所有英雄装备槽位/, "Fully confirmed gear state did not complete preset");
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
