import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const requireDom = createRequire(process.env.LWB317_JSDOM_PACKAGE || "C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const { JSDOM } = requireDom("jsdom"); const React = requireUi("react"); const jsx = requireUi("react/jsx-runtime"); const { transformSync } = requireUi("esbuild");
const dom = new JSDOM("<!doctype html><html><body><button id='outside'>outside</button></body></html>", { url: "http://127.0.0.1/" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "KeyboardEvent", "Node", "MutationObserver", "localStorage"]) globalThis[name] = name === "window" ? dom.window : dom.window[name];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("No network allowed in app-exit UI proof"); };
const { createRoot } = requireUi("react-dom/client");
dom.window.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
dom.window.HTMLDialogElement.prototype.close = function () { this.open = false; };
dom.window.HTMLElement.prototype.getClientRects = function () { return this.isConnected ? [{ width: 100, height: 20 }] : []; };
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"), "utf8");
const source = fs.readFileSync(path.join(ui, "src/AppExitDialog.jsx"), "utf8");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const slices = {};
function between(name, start, end) {
  const a = asset.indexOf(start); const b = asset.indexOf(end, a + start.length);
  assert.ok(a >= 0 && b > a, name);
  const text = asset.slice(a, b);
  slices[name] = { utf8ByteOffset: Buffer.byteLength(asset.slice(0, a)), utf8ByteLength: Buffer.byteLength(text), sha256: sha(text) };
  return text;
}
const originalModal = between("modal", "function In({", "var Ln=");
const originalRenderer = between("renderer", "function Ji({instanceCount", "var Yi=");
const originalProducer = between("producer", "function qi(){", "function Ji({instanceCount");
between("exitCommand", "function Bt(){return N(`app_exit_confirm`)", "function Vt(");
let language = "en"; const catalogs = {};
for (const code of ["en", "ja"]) catalogs[code] = new Function(fs.readFileSync(path.join(ui, `src/locales/${code}.js`), "utf8").replace("export default", "return"))();
const translate = (key, vars = {}) => (catalogs[language][key] || key).replace(/\{(\w+)\}/g, (all, name) => String(vars[name] ?? all));
const { AppExitDialog, AppExitPrompt } = new Function("React", "h", "useEffect", "useRef", "useState", "warningIcon", "useI18n", `${transformSync(source.replace(/^import[^\n]+\n/gm, "").replace(/export /g, ""), { loader: "jsx", jsxFactory: "h", target: "es2022" }).code}\nreturn {AppExitDialog,AppExitPrompt};`)(React, React.createElement, React.useEffect, React.useRef, React.useState, "exact-warning.png", () => ({ t: translate }));
const original = new Function("M", "b", "De", "Ki", "Wt", "Bt", `${originalModal}\n${originalRenderer}\n${originalProducer}\nreturn {Ji,qi};`)(jsx, React, () => ({ t: translate }), "exact-warning.png", (...args) => environment.subscribe(...args), () => environment.confirm());
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };
let environment;
async function flush(fn) { await React.act(async () => { if (fn) fn(); await Promise.resolve(); await Promise.resolve(); }); }
function reactProps(element) { const key = Object.keys(element).find((value) => value.startsWith("__reactProps$")); assert.ok(key); return element[key]; }
function env() {
  return { events: [], calls: [], subscribers: new Set(), confirm: async () => {}, subscribe(name, callback) {
    if (typeof name === "function") { callback = name; name = "app://close-requested"; }
    assert.equal(name, "app://close-requested"); this.subscribers.add(callback);
    return () => this.subscribers.delete(callback);
  }, emit(instanceCount) { for (const callback of this.subscribers) callback({ instanceCount }); } };
}
async function mount(kind, mode, props = {}) {
  const binding = env(); environment = binding;
  binding.confirm = async () => { binding.calls.push("confirm"); };
  const host = document.createElement("div"); document.body.append(host); const root = createRoot(host);
  const element = () => mode === "renderer" ? React.createElement(kind === "original" ? original.Ji : AppExitDialog, { t: translate, onCancel: () => binding.calls.push("cancel"), onConfirm: () => binding.calls.push("confirm"), ...props })
    : React.createElement(kind === "original" ? original.qi : AppExitPrompt, { subscribeCloseRequests: (callback) => binding.subscribe(callback), confirmExit: () => binding.confirm() });
  await flush(() => root.render(element()));
  return { binding, host, root, rerender: () => flush(() => root.render(element())), dispose: async () => { if (host.contains(document.activeElement)) document.activeElement.blur(); host.remove(); await flush(() => root.unmount()); },
    click: async (selector) => { const control = host.querySelector(selector); assert.ok(control); await flush(() => control.click()); },
  };
}
const cases = [];
async function compare(name, mode, props, action = async () => {}) {
  const output = [];
  for (const kind of ["original", "current"]) {
    const mounted = await mount(kind, mode, props);
    try { await action(mounted); output.push({ html: mounted.host.innerHTML, calls: mounted.binding.calls }); }
    finally { await mounted.dispose(); assert.equal(mounted.binding.subscribers.size, 0); }
  }
  assert.deepEqual(output[1], output[0], name); cases.push({ name, pass: true });
}
for (const code of ["en", "ja"]) for (const count of [null, 0, 1, 4, 99]) for (const busy of [false, true]) {
  language = code; await compare(`${code}-count-${count}-${busy ? "busy" : "idle"}`, "renderer", { instanceCount: count, busy });
}
language = "en";
for (const busy of [false, true]) await compare(`renderer-button-actions-${busy ? "busy" : "idle"}`, "renderer", { instanceCount: 4, busy }, async (mounted) => { await mounted.click(".app-exit-confirm"); await mounted.click(".app-exit-actions button:first-child"); });
for (const busy of [false, true]) await compare(`renderer-Escape-${busy ? "busy" : "idle"}`, "renderer", { instanceCount: 4, busy }, async (mounted) => {
  let prevented = false; await flush(() => reactProps(mounted.host.querySelector("dialog")).onCancel({ preventDefault() { prevented = true; } })); assert.ok(prevented);
});
await compare("renderer-backdrop-does-not-close", "renderer", { instanceCount: 4 }, async (mounted) => { await mounted.click("dialog"); assert.equal(mounted.binding.calls.length, 0); });
for (const busy of [false, true]) await compare(`renderer-Tab-${busy ? "busy" : "idle"}`, "renderer", { instanceCount: 4, busy }, async (mounted) => {
  const dialog = mounted.host.querySelector("dialog"); const controls = [...dialog.querySelectorAll("button")];
  if (!busy) controls.at(-1).focus(); let prevented = false;
  await flush(() => reactProps(dialog).onKeyDown({ currentTarget: dialog, key: "Tab", shiftKey: false, stopPropagation() {}, preventDefault() { prevented = true; } }));
  assert.ok(prevented); if (!busy) assert.equal(document.activeElement, controls[0]);
});
await compare("producer-null-before-event", "producer", {});
await compare("producer-open-Cancel-close", "producer", {}, async (mounted) => {
  await flush(() => mounted.binding.emit(3)); assert.ok(mounted.host.querySelector("dialog"));
  await mounted.click(".app-exit-actions button:first-child"); assert.equal(mounted.host.querySelector("dialog"), null);
});
for (const settle of ["resolve", "reject"]) await compare(`producer-deferred-confirm-${settle}`, "producer", {}, async (mounted) => {
  const response = deferred(); mounted.binding.confirm = () => { mounted.binding.calls.push("confirm"); return response.promise; };
  await flush(() => mounted.binding.emit(3));
  await mounted.click(".app-exit-confirm"); assert.equal(mounted.binding.calls.length, 1);
  assert.equal(mounted.host.querySelector("dialog").getAttribute("aria-busy"), "true");
  await mounted.click(".app-exit-actions button:first-child"); assert.ok(mounted.host.querySelector("dialog"));
  await flush(() => response[settle](settle === "reject" ? new Error("INERT_CONFIRM_FAILED") : undefined));
  assert.equal(mounted.host.querySelector("dialog").getAttribute("aria-busy"), settle === "resolve" ? "true" : "false");
});
await compare("producer-new-close-request-resets-busy-and-count", "producer", {}, async (mounted) => {
  const response = deferred(); mounted.binding.confirm = () => response.promise;
  await flush(() => mounted.binding.emit(3)); await mounted.click(".app-exit-confirm");
  await flush(() => mounted.binding.emit(1)); assert.equal(mounted.host.querySelector("dialog").getAttribute("aria-busy"), "false");
  assert.equal(mounted.host.querySelector(".app-exit-confirm").textContent, translate("appExit.confirm", { count: 1 }));
  await flush(() => response.reject(new Error("INERT_OLD_FAILURE")));
});
const host = document.createElement("div"); document.body.append(host); const root = createRoot(host);
await flush(() => root.render(React.createElement(AppExitDialog, { instanceCount: 2 })));
assert.ok([...host.querySelectorAll("button")].every((control) => control.disabled));
await flush(() => reactProps(host.querySelector(".app-exit-confirm")).onClick());
await flush(() => reactProps(host.querySelector("dialog")).onCancel({ preventDefault() {} }));
assert.ok(host.querySelector("dialog")); host.remove(); await flush(() => root.unmount());
cases.push({ name: "absent-providers-fenced-no-native-close", pass: true });
const result = { marker: "LWB317_FINAL_EXIT_OK", cases: cases.length, originalComparisons: cases.length - 1, details: cases,
  sourceSha256: sha(asset), productionSha256: sha(source), warningSha256: sha(fs.readFileSync(path.join(ui, "src/assets/icon-warning.png"))), locators: slices,
  limits: ["Actual original/current React rendering and callbacks with jsdom modal/client-rect shims", "No native close command or live session control", "No physical browser modal or original pixels observed", "Successful source confirm retains busy; any preview dismissal is external fixture-owned behavior"] };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "exit-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result));
