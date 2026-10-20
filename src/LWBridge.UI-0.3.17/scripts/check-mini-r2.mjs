// FEATURE-018 R2 mounted Mini Games checks with inert native responses.
// No real browser, game process, network transport, or account actions.
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const uiRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const req = createRequire(path.join(uiRoot, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: "http://127.0.0.1/mini-r2-offline",
});
for (const key of ["window", "document", "HTMLElement", "Element", "Event", "Node",
  "MutationObserver", "localStorage"]) {
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "en");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
const intervals = new Map();
let nextInterval = 1;
window.setInterval = (callback, delay) => {
  const id = nextInterval++;
  intervals.set(id, { callback, delay });
  return id;
};
window.clearInterval = (id) => { intervals.delete(id); };

let receiver;
const calls = [];
let managerUnavailable = false;
let managerReply = { game: "LWSheep", level: 5 };
let solverReply = { taskName: "sheepGame", running: false, status: "completed",
  currentLevel: 4, totalMoves: 9, confirmedMoves: 9, elapsedSeconds: 31 };
let landReply = { cellId: 56, state: "locked", awaitingGameConfirmation: true };
let configReply = {
  attack: true, recall: true, shieldUse: false, equipment: true,
  randomRelocate: false, allianceRelocate: false,
  frontlineReinforce: true, frontlineReinforceCount: 5, treasureChestHint: false,
};
let heldAutomation = null;
let holdAutomationStart = false;
window.__LWBridgeBootstrap = {
  mode: "live", sessionId: "mini-r2-offline-session",
  profiles: { selectedProfileId: "mini-r2-offline-profile" },
};
window.chrome = { webview: {
  addEventListener(type, callback) { if (type === "message") receiver = callback; },
  removeEventListener() { receiver = null; },
  postMessage(message) {
    if (message.kind !== "invoke") return;
    calls.push(message.command);
    let value;
    switch (message.command) {
      case "hotkey_config_get":
        value = { ...configReply }; break;
      case "hotkey_config_save":
        configReply = { ...message.payload };
        value = { ...configReply };
        break;
      case "mini_sheep_status": value = managerReply; break;
      case "feature018_sheep_automation_status": value = solverReply; break;
      case "mini_land_status": value = landReply; break;
      case "mini_chest_position": value = { markerEligible: false }; break;
      case "automation_start":
        if (holdAutomationStart) {
          holdAutomationStart = false;
          heldAutomation = message;
          return;
        }
        value = { accepted: true, taskName: "sheepGame" };
        break;
      case "automation_stop": value = { accepted: true, taskName: "sheepGame" }; break;
      default: throw new Error("Unexpected Mini native command: " + message.command);
    }
    queueMicrotask(() => receiver?.({ data: {
      kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
      id: message.id, ok: !(message.command === "mini_sheep_status" && managerUnavailable),
      result: value, error: { code: "GAME_METHOD_UNAVAILABLE", message: "Season manager expired" },
    } }));
  },
} };

const bundle = await build({
  stdin: { contents: [
    'export { MiniGamesPage } from "./HotkeyPages.jsx";',
    'export { I18nProvider } from "./i18n.jsx";',
    'export { backendBridge } from "./backendBridge.js";',
  ].join("\n"), resolveDir: path.join(uiRoot, "src"), loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const module = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, module, module.exports);
const { MiniGamesPage, I18nProvider, backendBridge } = module.exports;
const { createRoot } = req("react-dom/client");
const reactRoot = createRoot(document.getElementById("root"));
const flush = (fn) => React.act(async () => {
  if (fn) await fn();
  for (let i = 0; i < 32; i += 1) await Promise.resolve();
});
const tick = (delay) => flush(() => {
  for (const { callback, delay: ms } of [...intervals.values()]) {
    if (ms === delay) callback();
  }
});
const sheepCard = () => [...document.querySelectorAll(".hotkey-card")].at(-1);
const landCard = () => [...document.querySelectorAll(".hotkey-card")].at(-2);
const gCard = () => [...document.querySelectorAll(".hotkey-card")]
  .find((card) => card.querySelector("kbd")?.textContent === "G");
const typeInputValue = (input, value) => {
  Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value").set.call(input, value);
  input.dispatchEvent(new window.Event("input", { bubbles: true }));
};

await flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: true }))));
assert.match(sheepCard().textContent, /Completed/i,
  "server-confirmed completed state was hidden behind the planned move count");
assert.match(sheepCard().textContent, /Current level:\s*4/,
  "terminal run showed the manager's next level instead of the completed original level");
assert.doesNotMatch(sheepCard().textContent, /9\s*\/\s*9/,
  "completed run still renders the executing progress label");
assert.ok(landCard().querySelector("button").disabled,
  "remounted Mini tab lost its game-side in-flight land unlock state");
assert.ok(!calls.includes("monopoly_cell_open"), "recovery should only read native land state");
assert.ok(!calls.includes("mini_chest_unmark") && !calls.includes("mini_chest_mark"),
  "Opening Mini Games with Black Market disabled must be passive");

const countInput = gCard().querySelector('input[type="number"]');
const savesBeforeCount = calls.filter((command) => command === "hotkey_config_save").length;
await flush(() => {
  countInput.focus();
  typeInputValue(countInput, "2");
  typeInputValue(countInput, "25");
});
assert.equal(countInput.disabled, false, "Typing a multi-digit G count disabled the input before commit");
assert.equal(calls.filter((command) => command === "hotkey_config_save").length, savesBeforeCount,
  "G count saved before the user committed the multi-digit edit");
await flush(() => countInput.blur());
assert.equal(configReply.frontlineReinforceCount, 25, "Committed multi-digit G count was not persisted");
assert.equal(gCard().querySelector('input[type="number"]').value, "25",
  "Saved multi-digit G count did not remain visible");

managerUnavailable = true;
await tick(2000);
assert.match(sheepCard().textContent, /Completed/i,
  "Season manager expiry erased a confirmed completed solver receipt");

solverReply = { ...solverReply, status: "solve_failed", running: false,
  confirmedMoves: 4, error: "Native server Click was not reconciled" };
await tick(2000);
assert.match(sheepCard().querySelector('[role="alert"]')?.textContent || "",
  /Food House stopped because the game did not confirm the automated move/,
  "native solver failure was not converted to localized owner-facing copy");
assert.doesNotMatch(sheepCard().textContent, /Native server Click|reconciled|GAME_/,
  "native solver details leaked into Mini Games");
assert.doesNotMatch(sheepCard().textContent, /4\s*\/\s*9/,
  "terminal solver failure was hidden behind an executing progress label");

managerUnavailable = false;
solverReply = { ...solverReply, status: "executing", running: true, confirmedMoves: 4,
  error: "" };
await tick(2000);
assert.match(sheepCard().textContent, /4\s*\/\s*9/,
  "currently executing solver lost its confirmed-progress label");
assert.match(sheepCard().textContent, /Current level:\s*5/,
  "running game should show the manager's current level");

landReply = { cellId: 56, state: "unlocked", exactCellLookup: true,
  requestProof: { requestCausalityConfirmed: true, replyLandId: 56,
    targetRequestCount: 1, replyCount: 1, accountFenceIntact: true } };
await tick(1000);
assert.equal(landCard().querySelector("button").disabled, false,
  "source-confirmed Land unlock did not retire its pending tab action");
assert.match(landCard().textContent, /Completed/i);

landReply = { cellId: 84, state: "locked", awaitingGameConfirmation: true };
backendBridge.setSelectedProfile("mini-r2-second-profile");
await flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: true }))));
assert.ok(landCard().querySelector("button").disabled,
  "new profile's own pending cell was not rehydrated");
assert.match(landCard().textContent, /84/);
assert.doesNotMatch(landCard().textContent, /56/,
  "previous owner's land confirmation leaked after profile switch");

solverReply = { ...solverReply, status: "idle", running: false, confirmedMoves: 0, error: "" };
await tick(2000);
holdAutomationStart = true;
await flush(() => sheepCard().querySelector(".mini-game-actions button").click());
assert.ok(heldAutomation, "Food House start did not enter the in-flight owner-race fixture");
assert.equal(sheepCard().querySelector(".mini-game-actions button").disabled, true,
  "Food House start was not marked busy while pending");
backendBridge.setSelectedProfile("mini-r2-third-profile");
window.__LWBridgeBootstrap.profiles.selectedProfileId = "mini-r2-third-profile";
landReply = { cellId: 91, state: "locked" };
await flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: true }))));
assert.equal(sheepCard().querySelector(".mini-game-actions button").disabled, false,
  "Retired Food House request left the new owner busy");
await flush(() => receiver?.({ data: {
  kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
  id: heldAutomation.id, ok: false,
  error: { code: "GAME_ACTION_FAILED", message: "raw old-owner failure" },
} }));
heldAutomation = null;
assert.doesNotMatch(document.body.textContent, /raw old-owner failure|PROFILE_GENERATION_RETIRED|GAME_ACTION_FAILED/,
  "Retired Food House request leaked raw failure into the new owner");

await flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: false }))));
assert.match(sheepCard().textContent, /Game disconnected\. Food House status is unavailable\./,
  "Disconnected Mini Games did not show unknown Food House status");
assert.doesNotMatch(sheepCard().textContent, /Stopped/,
  "Disconnected Mini Games falsely claimed Food House was stopped");

const chestReadsBefore = calls.filter((command) => command === "mini_chest_position").length;
const chestMarksBefore = calls.filter((command) => command === "mini_chest_mark").length;
configReply = { ...configReply, treasureChestHint: true };
backendBridge.setSelectedProfile("mini-r2-screen-only-profile");
window.__LWBridgeBootstrap.profiles.selectedProfileId = "mini-r2-screen-only-profile";
landReply = { cellId: 92, state: "locked" };
await flush(() => reactRoot.render(React.createElement(I18nProvider, {},
  React.createElement(MiniGamesPage, { online: true }))));
assert.ok(calls.filter((command) => command === "mini_chest_position").length > chestReadsBefore,
  "Enabled Black Market screen did not use the read-only eligibility probe");
assert.equal(calls.filter((command) => command === "mini_chest_mark").length, chestMarksBefore,
  "Screen-only Black Market state sent a marker mutation without read-only eligibility");
assert.match(document.body.textContent, /chest marker is not available/i,
  "Ineligible Black Market screen did not show localized passive-state feedback");

await flush(() => reactRoot.unmount());
dom.window.close();
console.log("FEATURE018_MINI_R2_MOUNTED_OFFLINE_PASS completed=visible terminalError=localized landPending=recovered executingProgress=visible gCount=multi-digit passiveChest=read-only ownerRace=retired offline=unknown");
