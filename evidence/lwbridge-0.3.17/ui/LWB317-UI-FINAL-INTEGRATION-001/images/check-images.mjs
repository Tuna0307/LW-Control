import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, "../../../../.."); const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json")); const React = requireUi("react"); const jsx = requireUi("react/jsx-runtime"); const { transformSync } = requireUi("esbuild");
const requireDom = createRequire(process.env.LWB317_JSDOM_PACKAGE || "C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json"); const { JSDOM } = requireDom("jsdom");
const dom = new JSDOM("<!doctype html><html><body></body></html>", { url: "http://127.0.0.1/" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "Node", "MutationObserver"]) globalThis[name] = name === "window" ? dom.window : dom.window[name];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator }); globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("network forbidden in inert image proof"); };
const { createRoot } = requireUi("react-dom/client");
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js"), "utf8");
const source = fs.readFileSync(path.join(ui, "src/GameAssetImage.jsx"), "utf8");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };
async function flush(fn) { await React.act(async () => { if (fn) fn(); await Promise.resolve(); await Promise.resolve(); }); }
let now = 100000; const originalDateNow = Date.now; Date.now = () => now;
function machine(kind) {
  const tasks = []; const calls = []; const reads = [];
  const reader = (request) => { const response = deferred(); calls.push(request); reads.push(response); return response.promise; };
  const schedule = (fn, delay) => { tasks.push({ fn, delay }); return tasks.length; };
  let Component, Provider, normalize, subscribe, lookup;
  if (kind === "original") {
    const originalCode = asset.slice(asset.indexOf("var i=t(r()),a=e(),")).replace("var i=t(r()),a=e(),", "var ").replace(/export\{x as t\};?/, "");
    const value = new Function("i", "a", "n", "window", `${originalCode}\nreturn {Component:x,normalize:b,subscribe:y,lookup:key=>l.get(key)};`)(React, jsx, reader, { setTimeout: schedule });
    ({ Component, normalize, lookup } = value);
    subscribe = (source, listener) => value.subscribe(source.sourceKey, source.assetPath, source.spriteName, listener);
  } else {
    const value = new Function("h", "createContext", "useContext", "useEffect", "useRef", "useState", `${transformSync(source.replace(/^import[^\n]+\n/gm, "").replace(/export /g, ""), { loader: "jsx", jsxFactory: "h", target: "es2022" }).code}\nreturn {GameAssetImage,GameAssetImageProvider,normalizeGameAssetSource,createGameAssetImageCache};`)(React.createElement, React.createContext, React.useContext, React.useEffect, React.useRef, React.useState);
    const cache = value.createGameAssetImageCache(reader, { schedule, now: () => now });
    Component = value.GameAssetImage; Provider = value.GameAssetImageProvider; normalize = value.normalizeGameAssetSource; subscribe = cache.subscribe; lookup = cache.lookup;
  }
  window.setTimeout = schedule;
  return { kind, Component, Provider, normalize, subscribe, lookup, reader, tasks, calls, reads,
    drain: async () => flush(() => { while (tasks.length) { const task = tasks.shift(); assert.equal(task.delay ?? 0, 0); task.fn(); } }),
  };
}
async function mount(m, props = {}) {
  const container = document.createElement("div"); document.body.append(container); const root = createRoot(container);
  const element = () => React.createElement(m.Component, { assetPath: "icons/a", alt: "Icon A", className: "map-reward-icon", readImage: m.reader, ...props });
  await flush(() => root.render(element()));
  return { container, props, rerender: async (patch) => { Object.assign(props, patch); await flush(() => root.render(element())); },
    dispose: async () => { container.remove(); await flush(() => root.unmount()); },
  };
}
function snapshot(mount) {
  return [...mount.container.children].map((element) => ({ tag: element.tagName, class: element.className,
    alt: element.getAttribute("alt"), label: element.getAttribute("aria-label"), loading: element.getAttribute("loading"), decoding: element.getAttribute("decoding"),
    src: element.getAttribute("src") ? sha(element.getAttribute("src")) : null }));
}
const cases = []; let normalizationComparisons = 0;
async function compare(name, action) {
  const output = [];
  for (const kind of ["original", "current"]) { now = 100000; delete globalThis.IntersectionObserver; const m = machine(kind); output.push(await action(m)); }
  assert.deepEqual(output[1], output[0], name); cases.push({ name, pass: true });
}
await compare("source-normalization-exactly-one-trimmed-input", async (m) => {
  const outputs = [];
  for (const assetPath of [undefined, null, "", " ", "icons/a", "  icons/a  "]) for (const spriteName of [undefined, null, "", " ", "SpriteA", "  SpriteA  "]) outputs.push(m.normalize(assetPath, spriteName));
  normalizationComparisons = outputs.length; return outputs;
});
await compare("placeholder-to-image-attributes", async (m) => {
  const mounted = await mount(m); const states = [snapshot(mounted)];
  await m.drain(); assert.equal(m.calls.length, 1); assert.deepEqual(m.calls[0], { assetPath: "icons/a", spriteName: undefined });
  await flush(() => m.reads[0].resolve({ dataUrl: "data:image/png;base64,INERT_A" })); states.push(snapshot(mounted));
  assert.equal(mounted.container.firstChild.tagName, "IMG"); await mounted.dispose(); return { states, calls: m.calls };
});
await compare("sprite-request-and-invalid-dual-source-placeholder", async (m) => {
  const mounted = await mount(m, { assetPath: undefined, spriteName: " SpriteA " });
  await m.drain(); assert.deepEqual(m.calls[0], { assetPath: undefined, spriteName: "SpriteA" });
  await flush(() => m.reads[0].resolve({ dataUrl: "data:image/png;base64,INERT_SPRITE" }));
  await mounted.rerender({ assetPath: "icons/a" }); await m.drain();
  assert.equal(m.calls.length, 1); const state = snapshot(mounted); await mounted.dispose(); return state;
});
await compare("visibility-observer-160px-and-intersection", async (m) => {
  const observers = [];
  globalThis.IntersectionObserver = class { constructor(callback, options) { this.callback = callback; this.options = options; this.disconnected = 0; observers.push(this); } observe(element) { this.element = element; } disconnect() { this.disconnected++; } };
  const mounted = await mount(m, { deferUntilVisible: true }); await m.drain(); assert.equal(m.calls.length, 0);
  assert.equal(observers[0].options.rootMargin, "160px 0px");
  await flush(() => observers[0].callback([{ isIntersecting: false }])); await m.drain(); assert.equal(m.calls.length, 0);
  await flush(() => observers[0].callback([{ isIntersecting: true }])); await m.drain(); assert.equal(m.calls.length, 1);
  await flush(() => m.reads[0].resolve({ dataUrl: "data:image/png;base64,INERT_VISIBLE" })); const state = snapshot(mounted);
  await mounted.dispose(); return { state, disconnected: observers[0].disconnected };
});
await compare("visibility-api-absent-loads-immediately", async (m) => {
  const mounted = await mount(m, { deferUntilVisible: true }); await m.drain(); assert.equal(m.calls.length, 1);
  await mounted.dispose(); return m.calls;
});
await compare("source-change-fences-obsolete-listener", async (m) => {
  const mounted = await mount(m); await m.drain();
  await mounted.rerender({ assetPath: "icons/b" }); await m.drain(); assert.equal(m.calls.length, 2);
  await flush(() => m.reads[0].resolve({ dataUrl: "data:image/png;base64,OLD" })); assert.equal(mounted.container.firstChild.tagName, "SPAN");
  await flush(() => m.reads[1].resolve({ dataUrl: "data:image/png;base64,NEW" })); const states = [snapshot(mounted)];
  await mounted.rerender({ assetPath: "icons/a" }); await m.drain(); states.push(snapshot(mounted)); assert.equal(m.calls.length, 2);
  await mounted.dispose(); return states;
});
await compare("queued-unmount-never-dispatches", async (m) => { const mounted = await mount(m); await mounted.dispose(); await m.drain(); assert.equal(m.calls.length, 0); return m.calls; });
await compare("running-unmount-caches-without-state-write", async (m) => {
  const mounted = await mount(m); await m.drain(); await mounted.dispose();
  await flush(() => m.reads[0].resolve({ dataUrl: "data:image/png;base64,CACHED_AFTER_UNMOUNT" })); await m.drain();
  const again = await mount(m, { deferUntilVisible: true }); await m.drain(); const state = snapshot(again); assert.equal(m.calls.length, 1); await again.dispose(); return state;
});
await compare("coalesced-duplicate-read", async (m) => {
  const a = await mount(m); const b = await mount(m); await m.drain(); assert.equal(m.calls.length, 1);
  await flush(() => m.reads[0].resolve({ dataUrl: "data:image/png;base64,COALESCED" })); const states = [snapshot(a), snapshot(b)]; await a.dispose(); await b.dispose(); return states;
});
await compare("two-concurrent-reads-and-queued-disposal", async (m) => {
  const loads = []; const source = (key) => ({ sourceKey: `asset:${key}`, assetPath: key });
  const listener = (key) => ({ load: (value) => loads.push([key, value]) });
  m.subscribe(source("a"), listener("a")); m.subscribe(source("b"), listener("b")); m.subscribe(source("c"), listener("c"));
  const disposeD = m.subscribe(source("d"), listener("d")); disposeD();
  await m.drain(); assert.deepEqual(m.calls.map((call) => call.assetPath), ["a", "b"]);
  await flush(() => m.reads[0].resolve({ dataUrl: "A" })); await m.drain(); assert.deepEqual(m.calls.map((call) => call.assetPath), ["a", "b", "c"]);
  await flush(() => { m.reads[1].resolve({ dataUrl: "B" }); m.reads[2].resolve({ dataUrl: "C" }); }); await m.drain(); return { calls: m.calls, loads };
});
await compare("failure-suppression-60s-no-automatic-retry", async (m) => {
  const source = { sourceKey: "asset:a", assetPath: "a" }; const load = () => {};
  m.subscribe(source, { load }); await m.drain();
  await flush(() => m.reads[0].reject(new Error("INERT_IMAGE_READ_FAILED"))); await m.drain();
  now += 59999; m.subscribe(source, { load }); await m.drain(); assert.equal(m.calls.length, 1);
  now += 1; await m.drain(); assert.equal(m.calls.length, 1); // expiry alone never retries
  m.subscribe(source, { load }); await m.drain(); assert.equal(m.calls.length, 2);
  await flush(() => m.reads[1].resolve({ dataUrl: "SUCCESS" })); await m.drain(); return m.calls;
});
await compare("cache-insertion-order-eviction-hits-do-not-promote", async (m) => {
  const chunk = "A".repeat(16 * 1024 * 1024); const source = (key) => ({ sourceKey: `asset:${key}`, assetPath: key });
  m.subscribe(source("a"), { load() {} }); m.subscribe(source("b"), { load() {} }); await m.drain();
  await flush(() => { m.reads[0].resolve({ dataUrl: chunk }); m.reads[1].resolve({ dataUrl: chunk }); }); await m.drain();
  assert.ok(m.lookup("asset:a")); m.subscribe(source("a"), { load() {} });
  m.subscribe(source("c"), { load() {} }); await m.drain(); await flush(() => m.reads[2].resolve({ dataUrl: "C" })); await m.drain();
  assert.equal(m.lookup("asset:a"), undefined); assert.ok(m.lookup("asset:b")); assert.equal(m.lookup("asset:c"), "C");
  return { keys: ["a", "b", "c"].map((key) => [key, !!m.lookup(`asset:${key}`)]), calls: m.calls.length };
});
await compare("oversize-image-delivered-not-cached", async (m) => {
  const source = { sourceKey: "asset:large", assetPath: "large" }; let length = 0;
  m.subscribe(source, { load: (value) => { length = value.length; } }); await m.drain();
  await flush(() => m.reads[0].resolve({ dataUrl: "X".repeat(32 * 1024 * 1024 + 1) })); await m.drain();
  assert.equal(m.lookup("asset:large"), undefined); assert.equal(length, 32 * 1024 * 1024 + 1); return { length };
});

const m = machine("current"); const absent = await mount(m, { readImage: null }); await m.drain(); assert.equal(m.calls.length, 0); assert.equal(absent.container.firstChild.tagName, "SPAN"); await absent.dispose();
cases.push({ name: "absent-reader-fenced", pass: true });
const a = machine("current"); const mounted = await mount(a); await a.drain(); await flush(() => a.reads[0].resolve({ dataUrl: "data:image/png;base64,INERT_OLD_READER" }));
const replacement = deferred(); const replacementCalls = [];
await mounted.rerender({ readImage: (request) => { replacementCalls.push(request); return replacement.promise; } });
assert.equal(mounted.container.firstChild.tagName, "SPAN"); await a.drain(); assert.equal(replacementCalls.length, 1);
await flush(() => replacement.resolve({ dataUrl: "data:image/png;base64,INERT_NEW_READER" })); assert.ok(mounted.container.firstChild.src.includes("INERT_NEW_READER")); await mounted.dispose();
cases.push({ name: "reader-identity-isolates-preview-native-cache-and-visible-source", pass: true });
const contextMachine = machine("current"); const contextHost = document.createElement("div"); document.body.append(contextHost); const contextRoot = createRoot(contextHost);
const contextElement = (override = undefined) => React.createElement(contextMachine.Provider, { readImage: contextMachine.reader }, React.createElement(contextMachine.Component, { assetPath: "icons/context", alt: "Context", className: "map-reward-icon", readImage: override }));
await flush(() => contextRoot.render(contextElement())); await contextMachine.drain(); assert.equal(contextMachine.calls.length, 1);
await flush(() => contextMachine.reads[0].resolve({ dataUrl: "data:image/png;base64,INERT_CONTEXT" })); assert.equal(contextHost.firstChild.tagName, "IMG");
await flush(() => contextRoot.render(contextElement(null))); await contextMachine.drain(); assert.equal(contextMachine.calls.length, 1); assert.equal(contextHost.firstChild.tagName, "SPAN");
contextHost.remove(); await flush(() => contextRoot.unmount());
cases.push({ name: "context-provider-inherits-reader-and-explicit-null-fences", pass: true });
Date.now = originalDateNow; delete globalThis.IntersectionObserver;
const result = { marker: "LWB317_FINAL_IMAGES_OK", cases: cases.length, originalComparisons: cases.length - 3, normalizationComparisons, details: cases,
  sourceSha256: sha(asset), productionSha256: sha(source), sourceBytes: Buffer.byteLength(asset),
  locators: Object.fromEntries(["function g(", "function v(", "function y(", "function b(", "function x("].map((marker) => { const offset = asset.indexOf(marker); return [marker, { utf8ByteOffset: Buffer.byteLength(asset.slice(0, offset)) }]; })),
  limits: ["Controlled inert read promises and actual original/current React handlers", "No real native image reader invoked or assets resolved", "DOM images use inert strings; no physical browser geometry/pixel claim", "Original cache reads do not update eviction order; retry expiry alone does not schedule a new read"] };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "image-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result));
