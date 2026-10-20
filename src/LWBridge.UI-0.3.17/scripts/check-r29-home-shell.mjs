import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import english from "../src/locales/en.js";
import zhCN from "../src/locales/zh-CN.js";

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

function Harness() {
  const { language, setLanguage } = mod.exports.useI18n();
  return React.createElement(React.Fragment, null,
    React.createElement("select", {
      id: "home-test-language",
      value: language,
      onChange: (event) => setLanguage(event.target.value),
    }, React.createElement("option", { value: "en" }, "English"), React.createElement("option", { value: "zh-CN" }, "Chinese")),
    React.createElement(mod.exports.HomePage, {
      homeState: runningState,
      onAutoLaunchGameChange: () => {},
      onAutoReconnectChange: () => {},
      onGameRootSelect: () => {},
      onStartGame: () => {},
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

await flush(() => root.unmount());
dom.window.close();

console.log("FEATURE018_R29_HOME_CLOSE_CONFIRM_PASS");
