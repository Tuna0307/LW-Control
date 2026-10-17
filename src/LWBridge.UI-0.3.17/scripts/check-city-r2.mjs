// Isolated mounted City tests. No real browser, bridge, launcher, or game.
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
  url: "http://127.0.0.1/city-r2",
});
for (const key of ["window", "document", "HTMLElement", "Element", "Event",
  "MouseEvent", "Node", "MutationObserver", "localStorage"]) {
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "en");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
window.setInterval = () => 42;
window.clearInterval = () => {};

let receive = null;
let nativeStatus = { state: "failed", jobId: "native-fixture-job",
  completedMoves: 0, totalMoves: 1, errorCode: "GAME_MOVE_RESULT_TIMEOUT",
  error: "Native move response was never confirmed." };
let nativeSnapshot;
const calls = [];
let holdDraftSaves = false;
const heldDraftSaves = [];
window.__LWBridgeBootstrap = {
  mode: "live", sessionId: "city-r2-offline-session", profiles: { selectedProfileId: "city-r2-profile" },
};
window.chrome = { webview: {
  addEventListener(event, callback) { if (event === "message") receive = callback; },
  removeEventListener() { receive = null; },
  postMessage(message) {
    if (message.kind !== "invoke") return;
    calls.push(message.command);
    if (message.command === "city_layout_draft_save" && holdDraftSaves) {
      heldDraftSaves.push(message);
      return;
    }
    let result;
    switch (message.command) {
      case "city_layout_snapshot_get": result = nativeSnapshot; break;
      case "city_layout_draft_get": result = { revision: 0, value: null }; break;
      case "city_layout_apply_status": result = nativeStatus; break;
      case "city_layout_draft_save": result = { revision: 1, value: message.payload.value }; break;
      default: throw new Error("Unexpected live City command: " + message.command);
    }
    queueMicrotask(() => receive?.({ data: {
      kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
      id: message.id, ok: true, result,
    } }));
  },
} };

const bundle = await build({
  stdin: { contents: [
    'export { CityLayoutPage } from "./CityLayoutPage.jsx";',
    'export { I18nProvider } from "./i18n.jsx";',
    'export { previewCityLayoutFixture } from "./previewRemainingPagesContracts.js";',
    'export { backendBridge } from "./backendBridge.js";',
  ].join("\n"), resolveDir: path.join(uiRoot, "src"), loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const module = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, module, module.exports);
const { CityLayoutPage, I18nProvider, previewCityLayoutFixture, backendBridge } = module.exports;
nativeSnapshot = previewCityLayoutFixture("city-layout-populated");
const { createRoot } = req("react-dom/client");
const reactRoot = createRoot(document.getElementById("root"));
const flush = (fn) => React.act(async () => {
  if (fn) await fn();
  for (let i = 0; i < 32; i += 1) await Promise.resolve();
});
const render = (previewState, instanceKey = previewState) => flush(() => reactRoot.render(
  React.createElement(I18nProvider, {},
    React.createElement(CityLayoutPage, { key: instanceKey, previewState, online: true })),
));
const pointer = (target, kind, x, y) => {
  const event = new window.Event(kind, { bubbles: true });
  for (const [key, value] of Object.entries({
    clientX: x, clientY: y, pointerId: 1, ctrlKey: false, shiftKey: false,
  })) Object.defineProperty(event, key, { value });
  target.dispatchEvent(event);
};

await render("city-layout-populated");
const grid = document.querySelector(".city-layout-grid");
assert.ok(grid, "City grid fixture did not mount");
grid.getBoundingClientRect = () => ({ left: 0, top: 0, width: 192, height: 192 });
const emptyCell = [...grid.querySelectorAll(".city-layout-cell")]
  .find((cell) => cell.style.gridColumn === "2" && cell.style.gridRow === "4");
assert.ok(emptyCell, "expected empty real grid span missing");
assert.equal(document.querySelector('[data-city-building-id="preview-barracks"]').getAttribute("aria-pressed"), "false");
await flush(() => pointer(emptyCell, "pointerdown", 36, 84));
assert.ok(grid.querySelector(".city-layout-lasso"),
  "pointer down on child grid span did not start the advertised box selection");
await flush(() => pointer(grid, "pointermove", 108, 60));
await flush(() => pointer(grid, "pointerup", 108, 60));
assert.equal(document.querySelector('[data-city-building-id="preview-barracks"]').getAttribute("aria-pressed"),
  "true", "box selection failed to select the intersected movable building");
assert.equal(grid.querySelector(".city-layout-lasso"), null, "selection lasso was left active");

await render("");
await flush();
assert.deepEqual(calls.slice(0, 3),
  ["city_layout_snapshot_get", "city_layout_draft_get", "city_layout_apply_status"],
  "live UI did not load native snapshot/draft/apply status in sequence");
assert.match(document.querySelector(".city-layout-error")?.textContent || "",
  /Native move response was never confirmed/,
  "terminal native City failure disappeared during snapshot refresh");

nativeStatus = { state: "cancelling", jobId: "native-fixture-job",
  completedMoves: 1, totalMoves: 3 };
await flush(() => document.querySelector(".city-layout-actions button").click());
await flush();
const cancel = document.querySelector(".city-layout-progress button");
assert.ok(cancel?.disabled, "native cancellation was not visually pending/disabled");

// A queued save must keep its original owner while waiting for a previous
// write's reply. The native bridge selects a profile at *invoke time*, so a
// second write after a profile switch would otherwise write A's draft to B.
nativeStatus = { state: "idle" };
await render("", "owner-retirement-case");
await flush();
holdDraftSaves = true;
const saveButton = document.querySelector(".city-layout-actions button:nth-of-type(5)");
assert.ok(saveButton && !saveButton.disabled, "City Save Draft action unavailable");
await flush(() => { saveButton.click(); saveButton.click(); });
assert.equal(heldDraftSaves.length, 1, "City draft writes were not serialized");
backendBridge.setSelectedProfile("other-profile");
await flush(() => receive?.({ data: {
  kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
  id: heldDraftSaves[0].id, ok: true,
  result: { revision: 1, value: heldDraftSaves[0].payload.value },
} }));
assert.equal(calls.filter((command) => command === "city_layout_draft_save").length, 1,
  "deferred City save from profile A was dispatched under successor profile B");

await flush(() => reactRoot.unmount());
dom.window.close();
console.log("FEATURE018_CITY_R2_MOUNTED_OFFLINE_PASS boxSelect=span nativeError=visible cancelling=disabled retiredSave=fenced");
