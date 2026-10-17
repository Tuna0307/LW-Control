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
        value = { frontlineReinforce: true, treasureChestHint: false }; break;
      case "mini_sheep_status": value = managerReply; break;
      case "feature018_sheep_automation_status": value = solverReply; break;
      case "mini_land_status": value = landReply; break;
      case "mini_chest_unmark": value = { markerVisible: false }; break;
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

managerUnavailable = true;
await tick(2000);
assert.match(sheepCard().textContent, /Completed/i,
  "Season manager expiry erased a confirmed completed solver receipt");

solverReply = { ...solverReply, status: "solve_failed", running: false,
  confirmedMoves: 4, error: "Native server Click was not reconciled" };
await tick(2000);
assert.match(sheepCard().querySelector('[role="alert"]')?.textContent || "",
  /Native server Click was not reconciled/,
  "native solver's specific failure was not displayed");
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

landReply = { cellId: 56, state: "unlocked" };
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

await flush(() => reactRoot.unmount());
dom.window.close();
console.log("FEATURE018_MINI_R2_MOUNTED_OFFLINE_PASS completed=visible terminalError=visible landPending=recovered executingProgress=visible");
