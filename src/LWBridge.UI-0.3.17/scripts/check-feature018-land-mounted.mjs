// Inert mounted frontend regression of the real app's native Land Cell gate.
// No Last War process, bridge, game files, credentials or gameplay is accessed.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const require = createRequire(path.join(root, "package.json"));
const { JSDOM } = require("jsdom");
const { build } = require("esbuild");
const React = require("react");
const { createRoot } = require("react-dom/client");
const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: "http://127.0.0.1/feature018-land-inert",
});
for (const key of ["window", "document", "HTMLElement", "Element", "Event", "Node",
  "MutationObserver", "localStorage"]) {
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "zh-CN");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
let nextInterval = 1;
const intervals = new Map();
window.setInterval = (fn, delay) => {
  const id = nextInterval++;
  intervals.set(id, { fn, delay });
  return id;
};
window.clearInterval = (id) => intervals.delete(id);

let receiver;
let landReply = "locked";
let sendingFailure = false;
let frontlineMembers = 1;
let frontlineGuid = 1;
let frontlineActions = 0;
let frontlineUncertain = false;
const requests = [];
window.__LWBridgeBootstrap = {
  mode: "live", sessionId: "approved-inert-session",
  profiles: { selectedProfileId: "approved-2288-profile" },
};
window.chrome = { webview: {
  addEventListener(type, callback) { if (type === "message") receiver = callback; },
  removeEventListener() { receiver = undefined; },
  postMessage(request) {
    if (request.kind !== "invoke") return;
    requests.push(request);
    let response;
    if (request.command === "hotkey_config_get") {
      response = { frontlineReinforce: true, treasureChestHint: false };
    } else if (request.command === "mini_land_status") {
      const targeted = request.payload.expectedCellId !== undefined;
      if (targeted) assert.equal(request.payload.expectedCellId, 6);
      response = {
        cellId: 6, state: targeted && landReply !== "locked" ? "unlocked" : "locked",
        kind: "city_land_lock", ...(targeted ? { exactCellLookup: true,
          requestProof: landReply === "matched" ? {
            requestCausalityConfirmed: true, replyLandId: 6, targetRequestCount: 1,
            replyCount: 1, accountFenceIntact: true,
          } : { requestCausalityConfirmed: false, replyLandId: 6,
            targetRequestCount: 0, replyCount: 1, accountFenceIntact: true } } : {}),
      };
    } else if (request.command === "monopoly_cell_open") {
      assert.deepEqual(Object.keys(request.payload).sort(), ["expectedCellId", "profileId"]);
      assert.equal(request.payload.expectedCellId, 6);
      if (!sendingFailure) response = {
        cellId: 6, requestDispatched: true, awaitingGameConfirmation: true,
      };
    } else if (request.command === "mini_chest_unmark") {
      response = { markerVisible: false };
    } else if (request.command === "mini_frontline_status") {
      assert.deepEqual(request.payload, {
        profileId: window.__LWBridgeBootstrap.profiles.selectedProfileId,
      });
      response = { observation: "native_parkour_battle_readonly",
        ownerUid: "2553945035002288", sourceServerId: 2288,
        stageMode: "normal", frontBreakSundayActId: 0, heroId: 203, heroLevel: 12,
        memberCount: frontlineMembers, guidAllocator: frontlineGuid,
        actionEligible: true, uncertainPriorAction: false,
        readOnly: true, requestDispatched: false,
        serverAckObserved: false, observationScope: "native_local_battle" };
    } else if (request.command === "feature018_frontline_action") {
      assert.deepEqual(request.payload, {
        frontlineOptIn: true,
        profileId: window.__LWBridgeBootstrap.profiles.selectedProfileId,
      });
      const before = frontlineMembers;
      const previousGuid = frontlineGuid;
      frontlineActions++;
      frontlineMembers += 5;
      frontlineGuid += 5;
      response = { observation: "native_ParkourBattleLogic_GetMemberCount",
        action: "frontlineReinforce", slot: 5,
        stageMode: "normal", frontBreakSundayActId: 0, heroId: 203,
        beforeCount: before, afterCount: frontlineMembers,
        guidAllocatorBefore: previousGuid, guidAllocatorAfter: frontlineGuid,
        addedMembers: 5, observedCreatedUnits: 5,
        unitIdentityVerified: true, serverAckObserved: false,
        observationScope: "native_local_battle" };
    } else if (request.command === "mini_sheep_status") {
      response = { game: "LWSheep", level: 1 };
    } else if (request.command === "feature018_sheep_automation_status") {
      response = { status: "idle", running: false };
    } else throw Error("Unexpected Mini command " + request.command);
    queueMicrotask(() => receiver?.({ data: {
      kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
      id: request.id, ok: (!sendingFailure || request.command !== "monopoly_cell_open") &&
        (!frontlineUncertain || request.command !== "feature018_frontline_action"),
      result: response, error: { code: "GAME_ACTION_UNCONFIRMED", message: "Uncertain send" },
    } }));
  },
} };

const bundle = await build({
  stdin: { contents: [
    'export { MiniGamesPage } from "./HotkeyPages.jsx";',
    'export { I18nProvider } from "./i18n.jsx";',
    'export { backendBridge } from "./backendBridge.js";',
  ].join("\n"), resolveDir: path.join(root, "src"), loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const module = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(
  require, module, module.exports);
const { MiniGamesPage, I18nProvider, backendBridge } = module.exports;
const mounted = createRoot(document.getElementById("root"));
const flush = (callback) => React.act(async () => {
  if (callback) await callback();
  for (let i = 0; i < 35; i++) await Promise.resolve();
});
const tick = (delay) => flush(() => {
  for (const item of [...intervals.values()]) if (item.delay === delay) item.fn();
});
const landAction = () => document.querySelector(
  '[data-hotkey-category="miniGames"] .mini-game-actions button.primary');

await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: true }))));
const frontline = () => document.querySelector("[data-feature018-frontline-native]");
assert.ok(frontline(), "Original Frontline local action was not mounted in Mini Games");
assert.equal(frontlineActions, 0, "Mini Games mount automatically created native soldiers");
assert.equal(requests.filter((r) => r.command === "mini_frontline_status").length, 0,
  "Frontline automatically inspected or sent while user had not opted in");
await flush(() => frontline().querySelector(".mini-game-actions button").click());
assert.match(frontline().textContent, /当前士兵.*1/,
  "Read-only original Frontline battle readiness missing");
assert.equal(frontlineActions, 0, "Frontline readiness unexpectedly dispatched AddMember");
let nativeG = [...frontline().querySelectorAll("button")].find((button) =>
  button.textContent.includes("原生增援"));
assert.ok(nativeG && nativeG.disabled, "Five-unit action admitted before owner consent");
await flush(() => frontline().querySelector("input[type=checkbox]").click());
nativeG = [...frontline().querySelectorAll("button")].find((button) =>
  button.textContent.includes("原生增援"));
assert.equal(nativeG.disabled, false, "Explicitly confirmed original Frontline action cannot be invoked");
await flush(() => nativeG.click());
assert.equal(frontlineActions, 1, "Original Frontline did not invoke exactly one G action");
assert.match(frontline().textContent, /五名士兵/, "Local Frontline poststate not shown");
assert.match(frontline().textContent, /未收到独立服务器计分回复/,
  "Frontline local units were misrepresented as server scoring");
assert.equal(frontline().querySelector("input[type=checkbox]"), null,
  "Previously dispatched original Frontline action became replayable");
await flush(() => frontline().querySelector(".mini-game-actions button").click());
assert.equal(frontlineActions, 1,
  "Read-only follow-up rearmed or retried the same native Frontline action");
assert.ok(landAction(), "Native one-cell Mini Games control failed to mount");
await flush(() => landAction().click());
const sends = () => requests.filter((r) => r.command === "monopoly_cell_open");
assert.equal(sends().length, 1, "Land action dispatched anything other than one packet");
assert.equal(landAction().disabled, true, "One-shot Land button did not latch after send");
landReply = "pushOnly";
await tick(1000);
assert.equal(landAction().disabled, true, "Uncorrelated Land push reopened a duplicate send");
assert.doesNotMatch(document.querySelector('[data-hotkey-category="miniGames"]')?.textContent || "",
  /#6[^#]*完成/, "Uncorrelated Land push was falsely reported completed");
assert.equal(sends().length, 1, "Uncorrelated Land push triggered a resend");
landReply = "matched";
await tick(1000);
assert.equal(landAction().disabled, false, "Exact matched Land reply did not release pending latch");
assert.equal(sends().length, 1, "Read-only Land proof poll dispatched a duplicate");
assert.ok(requests.some((r) => r.command === "mini_land_status" &&
  r.payload.expectedCellId === 6), "Land status polled next cell instead of exactly ID 6");

backendBridge.setSelectedProfile("different-read-only-profile");
window.__LWBridgeBootstrap.profiles.selectedProfileId = "different-read-only-profile";
landReply = "locked";
await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: true }))));
await flush(() => frontline().querySelector(".mini-game-actions button").click());
await flush(() => frontline().querySelector("input[type=checkbox]").click());
frontlineUncertain = true;
nativeG = [...frontline().querySelectorAll("button")].find((button) =>
  button.textContent.includes("原生增援"));
await flush(() => nativeG.click());
assert.equal(frontlineActions, 2,
  "Second owner original Frontline intent was not sent once before timeout");
assert.equal(frontline().querySelector("input[type=checkbox]"), null,
  "Native Frontline uncertain timeout rearmed a second mutating send");
await flush(() => frontline().querySelector(".mini-game-actions button").click());
assert.equal(frontlineActions, 2,
  "Frontline read-only verification resent a timeout-uncertain five-unit mutation");
sendingFailure = true;
await flush(() => landAction().click());
const attempted = sends().length;
assert.equal(landAction().disabled, true, "Uncertain native Land send permitted another packet");
await tick(1000);
assert.equal(sends().length, attempted, "Uncertain Land send retried in the same owner session");

await flush(() => mounted.unmount());
assert.equal(intervals.size, 0, "Native Mini Games polling timers leaked on unmount");
backendBridge.dispose();
console.log("FEATURE018_LAND_MOUNTED_OFFLINE_PASS exactCell=6 unmatchedPush=noSuccess timeout=noResend matchedReply=confirmed");
