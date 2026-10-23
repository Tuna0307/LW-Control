// Mounted R29 Map/City error regressions. Browser/native calls are controlled;
// no desktop input, launcher, game process, or live game action is used.
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath } from "node:url";

const thisFile = fileURLToPath(import.meta.url);
const scenario = process.argv[2] || "";
const scenarios = ["status-detail", "status-code-zh", "save", "autosave", "discard", "cancel", "restore"];

if (!scenario) {
  for (const name of scenarios) {
    const run = spawnSync(process.execPath, [thisFile, name], { encoding: "utf8" });
    if (run.status !== 0) {
      process.stdout.write(run.stdout || "");
      process.stderr.write(run.stderr || "");
      process.exit(run.status || 1);
    }
  }
  console.log("R29_MAP_CITY_VISIBLE_ERROR_MOUNTED_OK", scenarios.join(","));
  process.exit(0);
}
assert.ok(scenarios.includes(scenario), `unknown scenario ${scenario}`);

const uiRoot = path.resolve(path.dirname(thisFile), "..");
const req = createRequire(path.join(uiRoot, "package.json"));
const { JSDOM } = req("jsdom");
const { build } = req("esbuild");
const React = req("react");
const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', {
  url: `http://127.0.0.1/r29-city-errors/${scenario}`,
});
for (const key of ["window", "document", "HTMLElement", "Element", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", scenario === "status-code-zh" ? "zh-CN" : "en");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
window.setInterval = () => 1;
window.clearInterval = () => {};

let receive = null;
let snapshot = null;
let draft = { revision: 0, value: null };
let applyStatus = { state: "idle" };
let restorePreflight = null;
const failOnce = new Map();
window.__LWBridgeBootstrap = {
  mode: "live", sessionId: `r29-city-${scenario}`, profiles: { selectedProfileId: "r29-city-profile" },
};
const send = (message, ok, result, error) => queueMicrotask(() => receive?.({ data: {
  kind: "response", sessionId: window.__LWBridgeBootstrap.sessionId,
  id: message.id, ok, ...(ok ? { result } : { error }),
} }));
window.chrome = { webview: {
  addEventListener(name, callback) { if (name === "message") receive = callback; },
  removeEventListener() { receive = null; },
  postMessage(message) {
    if (message.kind !== "invoke") return;
    const failure = failOnce.get(message.command);
    if (failure) {
      failOnce.delete(message.command);
      send(message, false, null, failure);
      return;
    }
    let result;
    switch (message.command) {
      case "city_layout_snapshot_get": result = snapshot; break;
      case "city_layout_draft_get": result = draft; break;
      case "city_layout_apply_status": result = applyStatus; break;
      case "city_layout_draft_save":
        draft = { revision: draft.revision + 1, value: message.payload.value };
        result = draft;
        break;
      case "city_layout_draft_clear": draft = { revision: 0, value: null }; result = draft; break;
      case "city_layout_apply_cancel":
        applyStatus = { state: "cancelled", jobId: message.payload.jobId, completedMoves: 1, totalMoves: 3 };
        result = { accepted: true, status: applyStatus };
        break;
      case "city_layout_restore_preflight": result = restorePreflight; break;
      case "lastwar_map_localize":
        result = {
          language: message.payload.language, buildId: "r29", gameRoot: "C:/controlled/game",
          localeSha256: "r29-locale", englishSha256: "r29-en",
          texts: Object.fromEntries(message.payload.keys.map((key) => [key, key])),
        };
        break;
      default: throw new Error(`Unexpected City command: ${message.command}`);
    }
    send(message, true, result, null);
  },
} };

const bundle = await build({
  stdin: { contents: [
    'export { CityLayoutPage } from "./CityLayoutPage.jsx";',
    'export { I18nProvider } from "./i18n.jsx";',
    'export { previewCityLayoutFixture } from "./previewRemainingPagesContracts.js";',
  ].join("\n"), resolveDir: path.join(uiRoot, "src"), loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic",
  external: ["react", "react/*", "react-dom", "react-dom/*"],
  loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent",
});
const loaded = { exports: {} };
new Function("require", "module", "exports", bundle.outputFiles[0].text)(req, loaded, loaded.exports);
const { CityLayoutPage, I18nProvider, previewCityLayoutFixture } = loaded.exports;
snapshot = previewCityLayoutFixture("city-layout-populated");

if (scenario === "status-detail") {
  applyStatus = { state: "failed", jobId: "status-job", completedMoves: 0, totalMoves: 1,
    errorCode: "GAME_MOVE_RESULT_TIMEOUT", error: "GAME_MOVE_RESULT_TIMEOUT: Native move response was never confirmed." };
} else if (scenario === "status-code-zh") {
  applyStatus = { state: "failed", jobId: "status-job", completedMoves: 0, totalMoves: 1,
    errorCode: "GAME_MOVE_RESULT_TIMEOUT" };
} else if (scenario === "discard") {
  draft = { revision: 2, value: { version: 1, baseRevision: "stale-revision", placements: [], updatedAt: Date.now() } };
} else if (scenario === "cancel") {
  applyStatus = { state: "running", jobId: "cancel-job", completedMoves: 1, totalMoves: 3 };
} else if (scenario === "restore") {
  applyStatus = { state: "completed", jobId: "restore-job", completedMoves: 1, totalMoves: 1 };
  restorePreflight = {
    jobId: "restore-job", preflightOnly: true, requestDispatched: false,
    historicalRequestsReconciled: false, ready: false, restorationRequired: true,
    pendingNativeMove: false, plannedMoves: [], issueCode: "CITY_LAYOUT_RESTORE_NOT_VALID",
    issues: [{ code: "CITY_LAYOUT_OVERLAP" }], historicalUncertaintyInPlan: false,
  };
}

const { createRoot } = req("react-dom/client");
const root = createRoot(document.getElementById("root"));
const flush = (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 48; i += 1) await Promise.resolve(); });
const waitForAutosave = () => flush(() => new Promise((resolve) => setTimeout(resolve, 550)));
const pointer = (target, kind, x, y) => {
  const event = new window.Event(kind, { bubbles: true });
  for (const [key, value] of Object.entries({ clientX: x, clientY: y, pointerId: 1, ctrlKey: false, shiftKey: false }))
    Object.defineProperty(event, key, { value });
  target.dispatchEvent(event);
};
await flush(() => root.render(React.createElement(I18nProvider, {}, React.createElement(CityLayoutPage, { online: true }))));
await flush();

const visibleError = () => document.querySelector(".city-layout-error")?.textContent || "";
if (scenario === "status-detail") {
  assert.match(visibleError(), /Native move response was never confirmed/);
  assert.doesNotMatch(visibleError(), /GAME_MOVE_RESULT_TIMEOUT/);
} else if (scenario === "status-code-zh") {
  assert.match(visibleError(), /操作未能完成/);
  assert.doesNotMatch(visibleError(), /GAME_MOVE_RESULT_TIMEOUT/);
} else if (scenario === "save") {
  const button = () => document.querySelector(".city-layout-actions button:nth-of-type(5)");
  failOnce.set("city_layout_draft_save", { code: "CITY_DRAFT_SAVE_FAILED", message: "CITY_DRAFT_SAVE_FAILED: Draft storage unavailable." });
  await flush(() => button().click());
  assert.match(visibleError(), /Draft storage unavailable/);
  assert.doesNotMatch(visibleError(), /CITY_DRAFT_SAVE_FAILED/);
  await flush(() => button().click());
  assert.equal(visibleError(), "", "successful Save Draft retained its prior transient error");
} else if (scenario === "autosave") {
  failOnce.set("city_layout_draft_save", { code: "CITY_AUTOSAVE_FAILED", message: "CITY_AUTOSAVE_FAILED: Automatic draft save failed." });
  const grid = document.querySelector(".city-layout-grid");
  grid.getBoundingClientRect = () => ({ left: 0, top: 0, width: 192, height: 192 });
  const hospital = document.querySelector('[data-city-building-id="preview-hospital"]');
  await flush(() => pointer(hospital, "pointerdown", 60, 108));
  await flush(() => pointer(hospital, "pointermove", 108, 108));
  await flush(() => pointer(hospital, "pointerup", 108, 108));
  assert.ok(document.querySelector('[data-city-building-id="preview-hospital"]').classList.contains("changed"));
  await waitForAutosave();
  assert.match(visibleError(), /Automatic draft save failed/);
  assert.doesNotMatch(visibleError(), /CITY_AUTOSAVE_FAILED/);
  const movedHospital = document.querySelector('[data-city-building-id="preview-hospital"]');
  await flush(() => pointer(movedHospital, "pointerdown", 108, 108));
  await flush(() => pointer(movedHospital, "pointermove", 84, 108));
  await flush(() => pointer(movedHospital, "pointerup", 84, 108));
  await waitForAutosave();
  assert.equal(visibleError(), "", "successful autosave retained its prior transient error");
} else if (scenario === "discard") {
  const button = () => document.querySelector(".city-layout-warning button");
  assert.ok(button(), "stale draft discard control missing");
  failOnce.set("city_layout_draft_clear", { code: "CITY_DRAFT_CLEAR_FAILED", message: "CITY_DRAFT_CLEAR_FAILED: Draft could not be discarded." });
  await flush(() => button().click());
  assert.match(visibleError(), /Draft could not be discarded/);
  assert.doesNotMatch(visibleError(), /CITY_DRAFT_CLEAR_FAILED/);
  await flush(() => button().click());
  assert.equal(visibleError(), "", "successful stale-draft discard retained its prior transient error");
  assert.equal(button(), null, "successful stale-draft discard retained stale state");
} else if (scenario === "cancel") {
  const button = () => document.querySelector(".city-layout-progress:not([data-feature018-restore-review]) button");
  assert.ok(button(), "running Apply cancel control missing");
  failOnce.set("city_layout_apply_cancel", { code: "CITY_CANCEL_FAILED", message: "CITY_CANCEL_FAILED: Cancellation was not acknowledged." });
  await flush(() => button().click());
  assert.match(visibleError(), /Cancellation was not acknowledged/);
  assert.doesNotMatch(visibleError(), /CITY_CANCEL_FAILED/);
  await flush(() => button().click());
  assert.equal(visibleError(), "", "successful Apply cancellation retained its prior transient error");
  assert.equal(button(), null, "terminal cancelled status retained a running cancel control");
} else if (scenario === "restore") {
  const button = document.querySelector("[data-feature018-restore-review] button");
  assert.ok(button, "restore preflight control missing");
  await flush(() => button.click());
  const text = document.querySelector("[data-feature018-restore-review] [role=status]")?.textContent || "";
  assert.match(text, /overlaps another building/i);
  assert.doesNotMatch(text, /CITY_LAYOUT_(?:RESTORE_NOT_VALID|OVERLAP)/);
}

await flush(() => root.unmount());
dom.window.close();
console.log(`R29_CITY_VISIBLE_ERROR_CASE_OK ${scenario}`);
