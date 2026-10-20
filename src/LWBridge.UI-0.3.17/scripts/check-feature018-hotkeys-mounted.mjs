// FEATURE-018 Hotkeys UI: inert mounted React regression.
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
  url: "http://127.0.0.1/feature018-hotkeys-inert",
});
for (const key of ["window", "document", "HTMLElement", "Element", "Event", "Node",
  "MutationObserver", "localStorage"]) {
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "zh-CN");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });

let receiver;
const commands = [];
const config = {
  attack: true,
  recall: true,
  shieldUse: false,
  equipment: true,
  randomRelocate: false,
  allianceRelocate: false,
  frontlineReinforce: true,
  frontlineReinforceCount: 5,
  treasureChestHint: false,
};
window.__LWBridgeBootstrap = {
  mode: "live", sessionId: "hotkeys-inert-session",
  profiles: { selectedProfileId: "hotkeys-approved-profile" },
};
window.chrome = { webview: {
  addEventListener(type, callback) { if (type === "message") receiver = callback; },
  removeEventListener() { receiver = undefined; },
  postMessage(request) {
    if (request.kind !== "invoke") return;
    commands.push(request);
    if (request.command !== "hotkey_config_get")
      throw Error("Unexpected Hotkeys diagnostic/native command: " + request.command);
    queueMicrotask(() => receiver?.({ data: {
      kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
      id: request.id, ok: true, result: { ...config },
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

const workerPanelSelectors = [
  "[data-feature018-attack-readiness]",
  "[data-feature018-shield-inventory]",
  "[data-feature018-shield-shop]",
  "[data-feature018-equipment-preflight]",
  "[data-feature018-relocation-readiness]",
  "[data-feature018-frontline-native]",
];
const expectedBindings = ["Q / W / E / R", "A / S / D / F", "F6 / F7 / F8",
  "Alt + 1～4", "F9", "F10"];

function assertOriginalHotkeysOnly() {
  const page = document.querySelector('[data-hotkey-category="hotkeys"]');
  assert.ok(page, "Hotkeys page did not mount");
  const cards = [...page.querySelectorAll(".hotkey-grid > article.hotkey-card")];
  assert.equal(cards.length, 6, "Hotkeys must contain exactly the six owner-retained cards");
  assert.deepEqual(cards.map((card) => card.querySelector("kbd.hotkey-binding")?.textContent), expectedBindings,
    "Owner-retained Hotkeys card order or binding changed");
  assert.equal(cards.some((card) => card.querySelector("kbd.hotkey-binding")?.textContent === "Space"), false,
    "Removed Space shield countdown card remained visible");
  for (const selector of workerPanelSelectors)
    assert.equal(page.querySelector(selector), null, `Worker diagnostic panel remained: ${selector}`);
  assert.equal(page.querySelector(".hotkey-attack-settings"), null,
    "Retired attack march speedup controls rendered in Hotkeys");
  assert.equal(page.querySelector("[data-feature018-rapid]"), null,
    "Retired current-march speedup panel rendered in Hotkeys");
  assert.doesNotMatch(page.textContent, /当前行军道具加速|Current-march item speedup/i,
    "Retired current-march speedup copy remained visible");
}

await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(HotkeysPage, { online: true }))));
assertOriginalHotkeysOnly();
assert.deepEqual(commands.map((request) => request.command), ["hotkey_config_get"],
  "Mounting Hotkeys invoked a removed worker diagnostic command");

backendBridge.setSelectedProfile("hotkeys-second-profile");
window.__LWBridgeBootstrap.profiles.selectedProfileId = "hotkeys-second-profile";
await flush(() => mounted.render(React.createElement(I18nProvider, {},
  React.createElement(HotkeysPage, { online: true }))));
assertOriginalHotkeysOnly();
assert.ok(commands.length >= 2, "Profile switch did not reload original Hotkeys settings");
assert.equal(commands.every((request) => request.command === "hotkey_config_get"), true,
  "Profile switch invoked a removed worker diagnostic command");

await flush(() => mounted.unmount());
backendBridge.dispose();
console.log("FEATURE018_HOTKEYS_MOUNTED_OFFLINE_PASS cards=r24-six space=removed diagnostics=removed rapid=retired profileSwitch=clean");
