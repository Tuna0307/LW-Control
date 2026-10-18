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
      response = { frontlineReinforce: true, frontlineReinforceCount: 5,
        treasureChestHint: false };
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
    } else if (request.command === "mini_sheep_status") {
      response = { game: "LWSheep", level: 1 };
    } else if (request.command === "feature018_sheep_automation_status") {
      response = { status: "idle", running: false };
    } else throw Error("Unexpected Mini command " + request.command);
    queueMicrotask(() => receiver?.({ data: {
      kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
      id: request.id, ok: !sendingFailure || request.command !== "monopoly_cell_open",
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
assert.equal(document.querySelector("[data-feature018-frontline-native]"), null,
  "Worker Frontline diagnostic panel remained in Mini Games");
const miniPage = document.querySelector('[data-hotkey-category="miniGames"]');
assert.ok(miniPage, "Mini Games page did not mount");
const gCard = [...miniPage.querySelectorAll(".hotkey-grid > article.hotkey-card")]
  .find((card) => card.querySelector("kbd.hotkey-binding")?.textContent === "G");
assert.ok(gCard, "Original 0.3.17 G feature card was removed");
const gCount = gCard.querySelector('.hotkey-attack-toggle input[type="number"]');
assert.ok(gCount, "Original G soldiers-per-press setting was removed");
assert.equal(gCount.value, "5", "Saved G soldiers-per-press count was not rendered");
assert.equal(requests.some((r) => r.command === "mini_frontline_status" ||
  r.command === "feature018_frontline_action"), false,
  "Removed Frontline diagnostic command ran from the Mini Games page");
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
assert.equal(document.querySelector("[data-feature018-frontline-native]"), null,
  "Worker Frontline diagnostic panel returned after profile switch");
sendingFailure = true;
await flush(() => landAction().click());
const attempted = sends().length;
assert.equal(landAction().disabled, true, "Uncertain native Land send permitted another packet");
await tick(1000);
assert.equal(sends().length, attempted, "Uncertain Land send retried in the same owner session");

await flush(() => mounted.unmount());
assert.equal(intervals.size, 0, "Native Mini Games polling timers leaked on unmount");
backendBridge.dispose();
console.log("FEATURE018_LAND_MOUNTED_OFFLINE_PASS diagnostics=removed originalG=count-preserved exactCell=6 unmatchedPush=noSuccess timeout=noResend matchedReply=confirmed");
