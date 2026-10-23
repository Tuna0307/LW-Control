// Read-only native-shaped City name loading; no game, desktop input or RPC.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."), req = createRequire(path.join(root, "package.json"));
const { JSDOM } = req("jsdom"), { build } = req("esbuild"), React = req("react");
const dom = new JSDOM('<div id="root"></div>', { url: "http://127.0.0.1/city-names" });
for (const key of ["window", "document", "HTMLElement", "Element", "Event", "MouseEvent", "Node", "MutationObserver", "localStorage"])
  globalThis[key] = key === "window" ? dom.window : dom.window[key];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
localStorage.setItem("lwbridge.language", "en");
window.matchMedia = () => ({ matches: false, addListener() {}, removeListener() {} });
window.setInterval = () => 1; window.clearInterval = () => {};
let receive, snapshot, held = null, holdNames = false, badHash = false;
const calls = [];
window.__LWBridgeBootstrap = { mode: "live", sessionId: "city-names", profiles: { selectedProfileId: "A" } };
const respond = (message, result) => queueMicrotask(() => receive({ data: { kind: "response", sessionId: "city-names", id: message.id, ok: true, result } }));
const locale = (payload) => ({ language: payload.language, buildId: "26", gameRoot: "C:/native-game", localeSha256: badHash && payload.keys.length ? "changed" : "native-hash", englishSha256: "native-en", texts: Object.fromEntries(payload.keys.map((key) => [key, `${payload.language === "zh-CN" ? "建築" : "Native Building"} ${key.split("_").at(-1)}`])) });
window.chrome = { webview: {
  addEventListener(name, callback) { if (name === "message") receive = callback; }, removeEventListener() {},
  postMessage(message) {
    if (message.kind !== "invoke") return;
    calls.push(message);
    let result;
    if (message.command === "city_layout_snapshot_get") result = snapshot;
    else if (message.command === "game_asset_image") result = { dataUrl: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWlQAAAAASUVORK5CYII=" };
    else if (message.command === "city_layout_draft_get") result = { revision: 0, value: null };
    else if (message.command === "city_layout_apply_status") result = { state: "idle" };
    else if (message.command === "lastwar_map_localize") {
      if (holdNames && message.payload.keys.length) { held = message; return; }
      result = locale(message.payload);
    } else throw new Error(`Unexpected City command ${message.command}`);
    respond(message, result);
  },
} };
const bundled = await build({ stdin: { contents: [
  'export { CityLayoutPage } from "./CityLayoutPage.jsx";',
  'export { I18nProvider, useI18n } from "./i18n.jsx";',
  'export { previewCityLayoutFixture } from "./previewRemainingPagesContracts.js";',
  'export { backendBridge } from "./backendBridge.js";',
  'export { GameAssetImageProvider, createProfileGameAssetReader } from "./GameAssetImage.jsx";',
].join("\n"), resolveDir: path.join(root, "src"), loader: "jsx" }, bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*", "react-dom", "react-dom/*"], loader: { ".png": "dataurl", ".svg": "dataurl" }, logLevel: "silent" });
const loaded = { exports: {} }; new Function("require", "module", "exports", bundled.outputFiles[0].text)(req, loaded, loaded.exports);
const { CityLayoutPage, I18nProvider, useI18n, previewCityLayoutFixture, backendBridge, GameAssetImageProvider, createProfileGameAssetReader } = loaded.exports;
snapshot = previewCityLayoutFixture("city-layout-populated");
// 203 distinct native text keys exercise the real 200-key request boundary.
const original = snapshot.buildings[0];
snapshot.buildings = Array.from({ length: 203 }, (_, index) => ({ ...original, uuid: `building-${index}`, itemId: 10113000 + index, name: undefined, nameKey: `native_building_${index}`, level: 6, iconPath: "Assets/Building/repeated-native-farm.png" }));
function LanguageControl() {
  const { setLanguage } = useI18n();
  return React.createElement("button", { id: "language", onClick: () => setLanguage("zh-CN") }, "Language");
}
const { createRoot } = req("react-dom/client"), mounted = createRoot(document.getElementById("root"));
const flush = (fn) => React.act(async () => { if (fn) await fn(); for (let i = 0; i < 100; i++) await Promise.resolve(); });
const readImage = createProfileGameAssetReader(backendBridge);
const render = (key) => flush(() => mounted.render(React.createElement(I18nProvider, { key }, React.createElement(GameAssetImageProvider, { readImage }, React.createElement(LanguageControl), React.createElement(CityLayoutPage, { online: true })))));
await render("first");
await flush(() => new Promise((resolve) => setTimeout(resolve, 30)));
assert.equal(document.querySelectorAll('img.city-layout-grid-icon').length, 203, "Every repeated building subscribes to its shared native image");
assert.ok([...document.querySelectorAll('img.city-layout-grid-icon')].every((image) => image.getAttribute("loading") === "eager"), "Every grid cell must decode eagerly inside the scrolled/zoomed city grid");
assert.equal(calls.filter((call) => call.command === "game_asset_image").length, 1, "Repeated native paths must share one image read");
const nameCalls = calls.filter((call) => call.command === "lastwar_map_localize");
assert.deepEqual(nameCalls.map((call) => call.payload.keys.length), [0, 200, 3]);
assert.ok(nameCalls.every((call) => call.payload.language === "en" && call.payload.profileId === "A"));
const building = document.querySelector('[data-city-building-id="building-0"]');
assert.match(building.title, /Native Building 0.*Level 6/);
assert.match(building.getAttribute("aria-label"), /Native Building 0.*Level 6/);
assert.equal(building.querySelector(".city-layout-building-level").textContent, "6", "Compact level number must fit a single 24px cell");
await flush(() => building.click());
assert.equal(document.querySelector('img.city-layout-selected-icon').getAttribute("loading"), "eager");
assert.equal(calls.filter((call) => call.command === "game_asset_image").length, 1, "Properties must reuse the decoded building source");
assert.match(document.querySelector(".city-layout-selected").textContent, /Native Building 0/);
assert.doesNotMatch(document.querySelector(".city-layout-selected").textContent, /#10113000|native_building_/);
await flush(() => document.getElementById("language").click());
assert.match(document.querySelector('[data-city-building-id="building-0"]').title, /建築 0/);
assert.equal(calls.filter((call) => /apply_start|draft_save|unlock/.test(call.command)).length, 0, "Localization must never change a movement plan or send a game action");
const css = fs.readFileSync(path.join(root, "src/styles.css"), "utf8");
assert.match(css, /\.city-layout-building-level\{font-size:12px;.*line-height:16px/);
// A mismatched source identity must display an explicit failure and never use
// the unverified replacement text.
badHash = true;
await flush(() => document.querySelector(".city-layout-actions button").click());
assert.match(document.querySelector('.city-layout-error[role="alert"]').textContent, /locale|语言|語言|names|名称/i);
assert.doesNotMatch(document.querySelector('[data-city-building-id="building-0"]').title, /Native Building|建築/);
badHash = false; holdNames = true;
await flush(() => document.querySelector(".city-layout-actions button").click());
assert.ok(held);
const old = held, before = calls.length;
backendBridge.setSelectedProfile("B");
await flush(() => respond(old, locale(old.payload)));
assert.equal(calls.slice(before).filter((call) => call.command === "lastwar_map_localize" && call.payload.profileId === "B" && call.payload.keys.length === 3).length, 0, "Retired profile must not dispatch the remaining localization batch into its successor");
assert.doesNotMatch(document.querySelector('[data-city-building-id="building-0"]').title, /Native Building|建築/);
await flush(() => mounted.unmount()); dom.window.close();
console.log("FEATURE018_R21_CITY_NAMES_UI_OK: 200-key batches, native names, app language, source identity, profile retirement, compact readable level and full tooltips; no game actions");
