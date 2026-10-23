import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import english from "../src/locales/en.js";
import zhCN from "../src/locales/zh-CN.js";
import { withHomeLaunchFeedback } from "../src/locales/home-launch-feedback.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../src");
const req = createRequire(path.resolve(here, "../package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const { createRoot } = req("react-dom/client");

const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: "http://127.0.0.1/?view=overview",
});
for (const key of ["window", "document", "HTMLElement", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
window.matchMedia = () => ({ matches: true });
window.confirm = () => { throw new Error("Home Close must use the in-app DOM confirmation, not window.confirm"); };
localStorage.setItem("lwbridge.language", "en");
localStorage.setItem("lwbridge.theme", "light");

const bundle = await build({
  stdin: {
    contents: 'export { HomePage } from "./HomePage.jsx"; export { I18nProvider, useI18n } from "./i18n.jsx";',
    resolveDir: src,
    loader: "jsx",
  },
  bundle: true,
  write: false,
  platform: "browser",
  format: "cjs",
  jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" },
  logLevel: "silent",
});
const mod = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, mod, mod.exports);

const container = document.getElementById("root");
const flush = async (fn) => React.act(async () => {
  if (fn) await fn();
  for (let i = 0; i < 20; i++) await Promise.resolve();
});

const runningState = {
  rootResolved: true,
  gameRootStatus: { valid: true, root: "C:/Games/Last War" },
  proxyStatus: { gameRunning: true, repairRequired: false },
  online: true,
  gameRecoveryStatus: { state: "idle" },
  autoLaunchGame: false,
  autoReconnect: true,
  busy: "",
  proxyBusy: false,
  gameLaunchBusy: false,
  gameRootError: "",
  gameActionError: "",
  production: true,
};
let stopCalls = 0;
let updateCalls = 0;
let startCalls = 0;
let homeState = runningState;

function Harness() {
  const { language, setLanguage } = mod.exports.useI18n();
  return React.createElement(React.Fragment, null,
    React.createElement("select", {
      id: "home-test-language",
      value: language,
      onChange: (event) => setLanguage(event.target.value),
    }, React.createElement("option", { value: "en" }, "English"), React.createElement("option", { value: "zh-CN" }, "Chinese")),
    React.createElement(mod.exports.HomePage, {
      homeState,
      onAutoLaunchGameChange: () => {},
      onAutoReconnectChange: () => {},
      onGameRootSelect: () => {},
      onStartGame: () => { startCalls += 1; },
      onStopGame: () => { stopCalls += 1; },
      onUpdateAndRestart: () => { updateCalls += 1; },
    }),
  );
}

const root = createRoot(container);
await flush(() => root.render(React.createElement(mod.exports.I18nProvider, null, React.createElement(Harness))));
for (let i = 0; i < 3; i++) await flush();

const closeButton = [...container.querySelectorAll(".game-controls button")]
  .find((button) => button.textContent.trim() === english["setup.closeGameAction"]);
assert.ok(closeButton, "running Home must expose Close game");
await flush(() => closeButton.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(stopCalls, 0, "opening Close confirmation must not stop the game");
let dialog = container.querySelector('.home-close-confirm[role="dialog"]');
assert.ok(dialog, "Close game must open an in-app DOM confirmation dialog");
assert.ok(dialog.textContent.includes(english["setup.closeGameConfirm"]), "English Close confirmation text missing");
assert.ok(dialog.textContent.includes(english["setup.closeGameAutoRestartWarning"]),
  "auto-reconnect Close confirmation must explain that the game will restart");

const cancel = [...dialog.querySelectorAll("button")].find((button) => button.textContent.trim() === english["common.cancel"]);
await flush(() => cancel.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(container.querySelector(".home-close-confirm"), null, "Cancel must dismiss Close confirmation");
assert.equal(stopCalls, 0, "Cancel must not dispatch Stop");

await flush(() => closeButton.dispatchEvent(new MouseEvent("click", { bubbles: true })));
const language = document.getElementById("home-test-language");
await flush(() => {
  language.value = "zh-CN";
  language.dispatchEvent(new Event("change", { bubbles: true }));
});
for (let i = 0; i < 3; i++) await flush();
dialog = container.querySelector('.home-close-confirm[role="dialog"]');
assert.ok(dialog.textContent.includes(zhCN["setup.closeGameConfirm"]),
  "open Close confirmation must re-localize after switching to zh-CN");
assert.ok(dialog.textContent.includes(zhCN["setup.closeGameAutoRestartWarning"]),
  "open auto-reconnect warning must re-localize after switching to zh-CN");
const confirm = dialog.querySelector("button.danger");
assert.equal(confirm?.textContent.trim(), zhCN["setup.closeGameAction"], "zh-CN Close confirmation action mismatch");
await flush(() => confirm.dispatchEvent(new MouseEvent("click", { bubbles: true })));
assert.equal(stopCalls, 1, "confirming Close must dispatch exactly one Stop action");
assert.equal(updateCalls, 0, "normal Close confirmation must not dispatch repair/update");
assert.equal(container.querySelector(".home-close-confirm"), null, "confirmed Close must dismiss its dialog");

// An exited game can retain exact native ownership and an unrestored journal.
// Exercise the actual mounted controls, not just a recreated gating predicate.
const renderState = async (next) => {
  homeState = next;
  await flush(() => root.render(React.createElement(mod.exports.I18nProvider, null, React.createElement(Harness))));
};
const control = (label) => [...container.querySelectorAll(".game-controls button")]
  .find((button) => button.textContent.trim() === label);
const click = (element) => flush(() => element.dispatchEvent(new MouseEvent("click", { bubbles: true })));
for (const [code, baseCatalog] of [["en", english], ["zh-CN", zhCN]]) {
  const catalog = withHomeLaunchFeedback(code, baseCatalog);
  await flush(() => {
    const selector = document.getElementById("home-test-language");
    selector.value = code;
    selector.dispatchEvent(new Event("change", { bubbles: true }));
  });
  const exitedOwner = {
    ...runningState,
    proxyStatus: { gameRunning: false, runtimeManaged: true, repairRequired: false },
    online: false,
    autoReconnect: false,
  };
  await renderState(exitedOwner);
  assert.equal(container.querySelector(".panel-title span").textContent,
    catalog["error.GAME_EXITED_RESTORE_REQUIRED"], `${code}: exited owner needs truthful cleanup feedback`);
  assert.equal(control(catalog["top.launchGame"])?.disabled, true,
    `${code}: native retained ownership blocks successor Start while the process is stopped`);
  const cleanup = control(catalog["setup.closeGameAction"]);
  assert.equal(cleanup?.disabled, false, `${code}: native retained owner still permits exact Stop/restoration`);
  const stopsBefore = stopCalls;
  await click(cleanup);
  dialog = container.querySelector('.home-close-confirm[role="dialog"]');
  assert.ok(dialog, `${code}: cleanup uses the existing Close confirmation`);
  assert.ok(!dialog.textContent.includes(catalog["setup.closeGameAutoRestartWarning"]),
    `${code}: auto-reconnect OFF must not warn that cleanup restarts the game`);
  assert.equal(stopCalls, stopsBefore, "opening cleanup confirmation cannot dispatch native Stop");
  await click([...dialog.querySelectorAll("button")].find((button) => button.textContent.trim() === catalog["common.cancel"]));
  assert.equal(stopCalls, stopsBefore, "cancelled cleanup must preserve exact retained ownership");
  await click(cleanup);
  await click(container.querySelector(".home-close-confirm button.danger"));
  assert.equal(stopCalls, stopsBefore + 1, "confirmed cleanup dispatches exactly one existing Stop callback");
  assert.equal(updateCalls, 0, "exited-owner cleanup cannot dispatch repair/update/restart");

  await renderState({ ...exitedOwner, proxyBusy: true });
  assert.equal(container.querySelector(".game-controls button.primary")?.disabled, true);
  assert.equal(control(catalog["setup.closeGameAction"])?.disabled, true,
    "an in-flight native cleanup cannot admit another Close");
  assert.equal(container.querySelector(".panel-title span").textContent, catalog["common.processing"]);
  await renderState({ ...exitedOwner, gameRecoveryStatus: { state: "waiting" } });
  assert.equal(container.querySelector(".panel-title span").textContent, catalog["recovery.state.waiting"],
    "active automatic recovery keeps precedence over retained-owner cleanup feedback");

  await renderState({ ...exitedOwner, proxyStatus: { ...exitedOwner.proxyStatus, runtimeManaged: false } });
  assert.equal(container.querySelector(".panel-title span").textContent, catalog["setup.gameStopped"]);
  assert.equal(control(catalog["top.launchGame"])?.disabled, false,
    "acknowledged native ownership retirement restores Start");
  assert.equal(control(catalog["setup.closeGameAction"])?.disabled, true,
    "a stopped game without retained ownership has no cleanup target");
  const startsBefore = startCalls;
  await click(control(catalog["top.launchGame"]));
  assert.equal(startCalls, startsBefore + 1, "post-cleanup Start dispatches its original callback");
}
await renderState({
  ...runningState,
  proxyStatus: { gameRunning: true, runtimeManaged: false, repairRequired: true },
});
assert.equal(control(zhCN["setup.closeGameAction"]), undefined,
  "running unmanaged repair retains the Update and launch action");
await click(control(zhCN["setup.updateAndLaunch"]));
assert.equal(updateCalls, 1, "existing running-game repair dispatches update/restart directly");
assert.equal(container.querySelector(".home-close-confirm"), null,
  "running-game repair must not turn into retained-owner Close confirmation");

await flush(() => root.unmount());
dom.window.close();

console.log("FEATURE018_R29_HOME_CLOSE_CONFIRM_PASS");
