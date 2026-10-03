import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { compile, read, require as productRequire, repo } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";
import { routes } from "../../../../../src/LWBridge.UI-0.3.17/src/routes.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const dependencyRoot = process.env.LWB317_SHELL_MOUNT_DEPS || path.join(os.tmpdir(), "lwb317-shell-mount-deps");
const dependencyRequire = createRequire(path.join(dependencyRoot, "package.json"));
const { JSDOM } = dependencyRequire("jsdom");
const dom = new JSDOM("<!doctype html><html><body><button id='outside'>outside focus</button><div id='root'></div></body></html>", { url: "http://127.0.0.1/?previewPage=overview" });
for (const name of ["window", "document", "HTMLElement", "HTMLDialogElement", "Event", "MouseEvent", "KeyboardEvent", "localStorage", "Node", "MutationObserver"]) globalThis[name] = name === "window" ? dom.window : dom.window[name];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("No network access permitted by mounted shell proof"); };
window.__LWBridgeBootstrap = { mode: "preview" };
localStorage.setItem("lwbridge.language", "en");

const React = productRequire("react");
const { createRoot } = productRequire("react-dom/client");
const { act, createElement: h } = React;
assert.equal(React.version, "19.3.0");
const container = document.getElementById("root");
const outside = document.getElementById("outside");
const intervals = new Map();
const timeouts = new Map();
let timerId = 0;
window.setInterval = (fn, delay) => { intervals.set(++timerId, { fn, delay }); return timerId; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = (fn, delay) => { timeouts.set(++timerId, { fn, delay }); return timerId; };
window.clearTimeout = (id) => timeouts.delete(id);
const keydownListeners = new Set();
const add = window.addEventListener.bind(window);
const remove = window.removeEventListener.bind(window);
window.addEventListener = (name, callback, options) => { if (name === "keydown") keydownListeners.add(callback); return add(name, callback, options); };
window.removeEventListener = (name, callback, options) => { if (name === "keydown") keydownListeners.delete(callback); return remove(name, callback, options); };
let modalOpenCalls = 0;
let modalCloseCalls = 0;
// jsdom lacks native dialog top-layer behavior. This controlled shim only proves
// production EquipmentDialog invokes show/close and restores connected focus.
HTMLDialogElement.prototype.showModal = function () { this.open = true; modalOpenCalls++; this.querySelector("input,button")?.focus(); };
HTMLDialogElement.prototype.close = function () { this.open = false; modalCloseCalls++; };

const app = read("src/LWBridge.UI-0.3.17/src/App.jsx");
const pagesSource = read("src/LWBridge.UI-0.3.17/src/Pages.jsx");
const { buildSync } = productRequire("esbuild");
const bundled = buildSync({
  stdin: { contents: 'export * from "./Pages.jsx"; export { I18nProvider } from "./i18n.jsx";', resolveDir: path.join(repo, "src/LWBridge.UI-0.3.17/src"), sourcefile: "lead-mounted-entry.jsx", loader: "jsx" },
  bundle: true, write: false, platform: "browser", format: "cjs", jsx: "automatic", external: ["react", "react/*"], logLevel: "silent",
});
const module = { exports: {} };
new Function("require", "module", "exports", bundled.outputFiles[0].text)(productRequire, module, module.exports);
const production = module.exports;
const makeRetained = (PageForRoute) => compile(app, "RetainedPages", { h, Fragment: React.Fragment, Activity: React.Activity, routes, PageForRoute });
const MountedProductionPages = makeRetained(production.PageForRoute);
const observations = [];
const capture = (label, extra = {}) => observations.push({ label, activeIntervals: intervals.size, activeTimeouts: timeouts.size, activeKeydownListeners: keydownListeners.size, ...extra });
let root;
async function start() { root = createRoot(container); }
async function render(component) { if (process.env.LWB317_MOUNT_DEBUG) console.log("render begin", component.props?.activeRoute || "provider"); await act(async () => { root.render(component); }); if (process.env.LWB317_MOUNT_DEBUG) console.log("render complete"); }
async function stop() { await act(async () => root.unmount()); assert.equal(intervals.size, 0); assert.equal(keydownListeners.size, 0); }
async function click(element) { assert.ok(element); await act(async () => element.dispatchEvent(new MouseEvent("click", { bubbles: true }))); }
function visible(node) { for (let n = node; n && n !== container; n = n.parentElement) if (n.style.display === "none" || n.hidden) return false; return true; }
function shell(Component, route, visited, profile = "profile-a", pageProps = {}) { return h(Component, { activeRoute: route, visitedRoutes: new Set(visited), selectedProfileId: profile, pageProps }); }
function productionShell(route, visited, previewState, profile = "profile-a", extra = {}) { return h(production.I18nProvider, null, shell(MountedProductionPages, route, visited, profile, { previewState, bridgeMode: "preview", backendAvailable: false, online: false, ...extra })); }

// Mount actual production wrapper around a state/effect probe, using real React.
let serial = 0;
const counts = new Map();
function Probe({ routeKey, marker, online }) {
  const [identity] = React.useState(() => ++serial);
  const [value, setValue] = React.useState(0);
  const entry = counts.get(identity) || { routeKey, setup: 0, cleanup: 0, layoutSetup: 0, layoutCleanup: 0 };
  counts.set(identity, entry);
  React.useLayoutEffect(() => { entry.layoutSetup++; return () => { entry.layoutCleanup++; }; }, []);
  React.useEffect(() => { entry.setup++; return () => { entry.cleanup++; }; }, []);
  return h("div", { "data-route": routeKey, "data-identity": identity, "data-marker": marker, "data-online": String(online) }, h("button", { onClick: () => setValue((n) => n + 1) }, String(value)));
}
const ProbedPages = makeRetained(Probe);
if (process.env.LWB317_MOUNT_DEBUG) console.log("bundle and compile complete");
await start();
await render(shell(ProbedPages, "overview", ["overview"], "profile-a", { marker: "initial", online: false }));
assert.equal(container.querySelectorAll("[data-route]").length, 1);
assert.equal(counts.size, 1);
const home = container.querySelector("[data-route='overview']");
const homeIdentity = Number(home.dataset.identity);
await click(home.querySelector("button"));
assert.equal(home.textContent, "1");
const beforeSame = JSON.stringify(counts.get(homeIdentity));
await render(shell(ProbedPages, "overview", ["overview"], "profile-a", { marker: "initial", online: false }));
assert.equal(JSON.stringify(counts.get(homeIdentity)), beforeSame);
await render(shell(ProbedPages, "march", ["overview", "march"], "profile-a", { marker: "new-props", online: true }));
assert.equal(visible(home), false);
assert.equal(home.textContent, "1");
assert.equal(counts.get(homeIdentity).cleanup, 1);
assert.equal(counts.get(homeIdentity).layoutCleanup, 1);
capture("probe-hidden-home", { identity: homeIdentity, counts: { ...counts.get(homeIdentity) } });
await render(shell(ProbedPages, "overview", ["overview", "march"], "profile-a", { marker: "returned-props", online: true }));
assert.equal(container.querySelector("[data-route='overview']"), home);
assert.equal(visible(home), true);
assert.equal(home.textContent, "1");
assert.equal(home.dataset.marker, "returned-props");
assert.equal(home.dataset.online, "true");
assert.equal(counts.get(homeIdentity).setup, 2);
assert.equal(counts.get(homeIdentity).layoutSetup, 2);
capture("probe-returned-home", { identity: homeIdentity, counts: { ...counts.get(homeIdentity) }, latestProps: { marker: home.dataset.marker, online: home.dataset.online } });
for (let repeat = 0; repeat < 2; repeat++) {
  await render(shell(ProbedPages, "march", ["overview", "march"]));
  await render(shell(ProbedPages, "overview", ["overview", "march"]));
}
assert.equal(counts.get(homeIdentity).setup - counts.get(homeIdentity).cleanup, 1);
await render(shell(ProbedPages, "settings", routes.map((r) => r.key)));
assert.equal(container.querySelectorAll("[data-route]").length, 8);
assert.equal([...container.querySelectorAll("[data-route]")].filter(visible).length, 1);
await render(shell(ProbedPages, "overview", routes.map((r) => r.key), "profile-b"));
const profileHome = container.querySelector("[data-route='overview']");
assert.notEqual(profileHome, home);
assert.notEqual(Number(profileHome.dataset.identity), homeIdentity);
assert.equal(profileHome.textContent, "0");
capture("profile-key-remount", { oldIdentity: homeIdentity, newIdentity: Number(profileHome.dataset.identity), retainedRouteDomCount: container.querySelectorAll("[data-route]").length });
await stop();

// Real page timer owners; no manually invoking effect setup/cleanup.
for (const [route, fixture, selector] of [["mini-games", "mini-games-active", ".hotkey-panel"], ["settings", "settings-update-cooldown", ".settings-panel"]]) {
  await start();
  await render(productionShell(route, [route], fixture));
  const pageNode = container.querySelector(selector);
  assert.ok(pageNode);
  assert.equal(intervals.size, 1);
  assert.equal([...intervals.values()][0].delay, 1000);
  capture(`${route}-visible`);
  await render(productionShell("overview", [route, "overview"], fixture));
  assert.equal(visible(pageNode), false);
  assert.equal(intervals.size, 0);
  capture(`${route}-hidden`);
  await render(productionShell(route, [route, "overview"], fixture));
  assert.equal(container.querySelector(selector), pageNode);
  assert.equal(intervals.size, 1);
  capture(`${route}-returned`);
  await stop();
}

// Real City listener and draft timer owner, using the recognized moved fixture.
await start();
await render(productionShell("city-layout", ["city-layout"], "city-layout-populated-moved"));
const city = container.querySelector(".city-layout-panel") || container.querySelector(".city-layout");
assert.ok(city);
assert.equal(keydownListeners.size, 1);
const restoreCity = [...city.querySelectorAll("button")].find((button) => /Restore initial/i.test(button.textContent));
await click(restoreCity);
assert.equal([...timeouts.values()].filter((timer) => timer.delay === 500).length, 1);
const cityEditedText = city.textContent;
capture("city-visible-pending-draft", { draftTimers: [...timeouts.values()].filter((timer) => timer.delay === 500).length });
await render(productionShell("overview", ["city-layout", "overview"], "city-layout-populated-moved"));
assert.equal(keydownListeners.size, 0);
assert.equal([...timeouts.values()].filter((timer) => timer.delay === 500).length, 0);
capture("city-hidden", { draftTimers: 0 });
await render(productionShell("city-layout", ["city-layout", "overview"], "city-layout-populated-moved"));
assert.equal(keydownListeners.size, 1);
assert.equal(city.textContent, cityEditedText);
assert.equal([...timeouts.values()].filter((timer) => timer.delay === 500).length, 1);
capture("city-returned", { draftTimers: 1, editedStateRetained: true });
await act(async () => { for (const [id, timer] of [...timeouts]) if (timer.delay === 500) { timeouts.delete(id); timer.fn(); } });
assert.equal([...timeouts.values()].filter((timer) => timer.delay === 500).length, 0);
capture("city-returned-draft-fired", { draftTimers: 0 });
await stop();

// Real EquipmentContent keyboard listener and EquipmentDialog show/close calls.
await start();
await render(productionShell("march", ["march"], "squads-equipment"));
assert.equal(keydownListeners.size, 1);
const rename = [...container.querySelectorAll("button")].find((button) => button.textContent.trim() === "Rename");
outside.focus();
await click(rename);
const dialog = container.querySelector("dialog");
assert.ok(dialog?.open);
const dialogInput = dialog.querySelector("input");
assert.equal(document.activeElement, dialogInput);
capture("equipment-dialog-visible", { modalOpenCalls, modalCloseCalls, open: dialog.open });
await render(productionShell("overview", ["march", "overview"], "squads-equipment"));
assert.equal(keydownListeners.size, 0);
assert.equal(dialog.open, false);
assert.equal(modalCloseCalls, 1);
capture("equipment-dialog-hidden", { modalOpenCalls, modalCloseCalls, open: dialog.open, activeElement: document.activeElement.tagName, focusLimit: "React autoFocus occurs before the passive dialog effect captures previousFocus; jsdom does not implement hidden-element focus or modal inertness." });
await render(productionShell("march", ["march", "overview"], "squads-equipment"));
assert.equal(container.querySelector("dialog"), dialog);
assert.equal(dialog.open, true);
assert.equal(keydownListeners.size, 1);
assert.equal(document.activeElement, dialogInput);
assert.equal(modalOpenCalls, 2);
assert.equal(modalCloseCalls, 1);
capture("equipment-dialog-returned", { modalOpenCalls, modalCloseCalls, open: dialog.open });
await click([...dialog.querySelectorAll("button")].find((button) => button.textContent.trim() === "Cancel"));
assert.equal(container.querySelector("dialog"), null);
const sourceLoadout = container.querySelector(".equipment-loadout-handle");
const targetPosition = container.querySelectorAll(".equipment-position-card")[1];
assert.ok(sourceLoadout && targetPosition);
const beforeDrag = container.querySelector(".equipment-preset-layout").textContent;
await act(async () => sourceLoadout.dispatchEvent(new Event("dragstart", { bubbles: true, cancelable: true })));
await act(async () => targetPosition.dispatchEvent(new Event("drop", { bubbles: true, cancelable: true })));
await act(async () => sourceLoadout.dispatchEvent(new Event("dragend", { bubbles: true, cancelable: true })));
const equipment = container.querySelector(".equipment-preset-layout");
assert.notEqual(equipment.textContent, beforeDrag);
assert.ok(equipment.textContent.includes("Unsaved"));
const dirtyText = equipment.textContent;
capture("equipment-moved-dirty", { retainedDirty: equipment.textContent.includes("Unsaved"), dropFeedbackTimeouts: [...timeouts.values()].filter((timer) => timer.delay === 450).length });
await render(productionShell("overview", ["march", "overview"], "squads-equipment"));
assert.equal(keydownListeners.size, 0);
await render(productionShell("march", ["march", "overview"], "squads-equipment"));
assert.equal(container.querySelector(".equipment-preset-layout"), equipment);
assert.equal(equipment.textContent, dirtyText);
assert.equal(keydownListeners.size, 1);
capture("equipment-moved-dirty-returned", { retainedDirty: equipment.textContent.includes("Unsaved") });
await click(container.querySelectorAll(".squad-tabs [role='tab']")[0]);
assert.equal(keydownListeners.size, 0);
assert.equal(visible(equipment), false);
await render(productionShell("overview", ["march", "overview"], "squads-equipment"));
await render(productionShell("march", ["march", "overview"], "squads-equipment"));
assert.equal(container.querySelectorAll(".squad-tabs [role='tab']")[0].getAttribute("aria-selected"), "true");
assert.equal(visible(equipment), false);
assert.equal(keydownListeners.size, 0);
capture("equipment-inner-afk-retained-through-outer-return", { selectedInnerTab: "afk", equipmentHidden: true });
await click(container.querySelectorAll(".squad-tabs [role='tab']")[1]);
assert.equal(visible(equipment), true);
assert.equal(equipment.textContent, dirtyText);
assert.equal(keydownListeners.size, 1);
capture("equipment-inner-return-retains-dirty", { retainedDirty: true });
// Action-owned drop feedback has identical original/current 450ms behavior.
// Its callback can survive hidden effects, so flush this inert feedback before teardown.
await act(async () => { for (const [id, timer] of [...timeouts]) { timeouts.delete(id); timer.fn(); } });
await stop();

const hash = (text) => crypto.createHash("sha256").update(text).digest("hex").toUpperCase();
const report = {
  result: "LWB317_SHELL_LEAD_MOUNTED_RETENTION_OK",
  toolVersions: { node: process.version, react: React.version, reactDom: productRequire("react-dom/package.json").version, jsdom: dependencyRequire("jsdom/package.json").version, esbuild: productRequire("esbuild/package.json").version },
  toolSource: "Isolated npm registry jsdom@27.0.0 installation with --ignore-scripts; product React/react-dom/esbuild resolve from canonical UI node_modules.",
  sources: [{ path: "src/LWBridge.UI-0.3.17/src/App.jsx", sha256: hash(app) }, { path: "src/LWBridge.UI-0.3.17/src/Pages.jsx", sha256: hash(pagesSource) }],
  observations,
  verified: ["production RetainedPages actual React DOM mount", "never-visited probe page not rendered", "same-route rerender preserves state/effect identity", "hidden passive/layout effects disconnect", "return preserves DOM/state identity and reconnects once", "two repeated leave/returns remain balanced", "current runtime props on return", "all eight probe routes have single visibility", "profile fragment key remount resets local state", "actual Mini/Settings interval owners 1/0/1", "actual City keyboard listener 1/0/1", "actual City edited state retained and pending draft timer 1/0/1 then fires", "actual Equipment keyboard listener 1/0/1", "actual EquipmentDialog close/reopen effect calls", "actual Equipment HTML drag handlers move a loadout and preserve dirty state through outer leave/return", "Equipment dirty state survives inner AFK -> outer Home -> Squads AFK -> Equipment with balanced listener ownership"],
  limits: "Real React DOM + jsdom. The shell state/effect/current-props/profile tests use an instrumented PageForRoute probe in the unchanged production wrapper; Mini/Settings/City/Equipment tests mount the actual bundled production Pages and I18nProvider with recognized inert preview fixtures. Timers are controlled maps. HTML drag events invoke actual React production handlers but do not prove physical browser drag behavior. Native dialog top-layer/Tab focus behavior uses a documented showModal/close shim; React autoFocus may become the passive effect's previousFocus, and jsdom does not enforce hidden-element focus or modal inertness, so browser focus/inertness remains unverified. Does not mount App-owned bootstrap/polling, exercise App selectRoute callbacks, prove original pixels/native functions, or invoke gameplay/updater/export/global hotkeys.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "lead-mounted-retention-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
dom.window.close();
