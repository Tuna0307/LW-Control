import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../../.."), ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const req = createRequire(path.join(ui, "package.json")), React = req("react"), jsx = req("react/jsx-runtime"), parse = req("@babel/parser").parse, traverse = req("@babel/traverse").default;
const domReq = createRequire(process.env.LWB317_JSDOM_PACKAGE || "C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json");
const { JSDOM } = domReq("jsdom"), dom = new JSDOM("<!doctype html><html><body></body></html>", { url: "http://127.0.0.1/", pretendToBeVisual: true });
for (const name of ["window", "document", "HTMLElement", "HTMLButtonElement", "Element", "Event", "MouseEvent", "EventTarget", "AbortController", "Node", "MutationObserver"]) globalThis[name] = name === "window" ? dom.window : dom.window[name];
globalThis.getComputedStyle = window.getComputedStyle.bind(window); globalThis.PointerEvent = dom.window.MouseEvent;
Object.defineProperty(globalThis, "navigator", { configurable: true, value: window.navigator }); globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("network forbidden in motion proof"); };
const { createRoot } = req("react-dom/client");
const original = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js"), "utf8");
const main = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"), "utf8");
const vendor = fs.readFileSync(path.join(ui, "src/vendor/equipmentMotion317.js"), "utf8");
const wrapper = fs.readFileSync(path.join(ui, "src/EquipmentMotion.jsx"), "utf8");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const closure = original.slice(original.indexOf("var Ee=(0,O.createContext)({});"), original.indexOf("var td=$u,nd="));
const helper = main.slice(main.indexOf("var e=Object.create,"), main.indexOf("(function(){"));
const currentProps = new Function(wrapper.replace(/^import[^\n]+\n/gm, "").replace(/^export \{[^\n]+\n/gm, "").replace(/export /g, "") + "\nreturn equipmentMotionProps;")();
const classes = { presets: "equipment-preset-squads", squad: "equipment-preset-squad ", slot: "preset-equipment-slot ", position: "equipment-position-card ", rename: "equipment-preset-dialog", toast: "equipment-toast", progress: "equipment-apply-progress" };
const propKeys = new Set(["initial", "animate", "exit", "transition", "layout", "layoutId", "whileHover", "whileTap"]);
const recovered = {}, locators = {};
traverse(parse(original, { sourceType: "module" }), { CallExpression(p) {
  const object = p.node.arguments[1]; if (object?.type !== "ObjectExpression") return;
  const classProperty = object.properties.find((property) => property.key?.name === "className");
  const value = classProperty?.value; const className = value?.type === "TemplateLiteral" ? value.quasis[0].value.raw : value?.value;
  const kind = Object.keys(classes).find((key) => className === classes[key] || key === "slot" && className?.startsWith(classes[key]) || key === "position" && className?.startsWith(classes[key]));
  if (!kind || !object.properties.some((property) => propKeys.has(property.key?.name))) return;
  const properties = object.properties.filter((property) => propKeys.has(property.key?.name));
  recovered[kind] = new Function("s", "r", "N", "e", "return ({" + properties.map((property) => original.slice(property.start, property.end)).join(",") + "});");
  locators[kind] = properties.map((property) => ({ key: property.key.name, utf8ByteOffset: Buffer.byteLength(original.slice(0, property.start)), expression: original.slice(property.start, property.end) }));
} });
assert.deepEqual(Object.keys(recovered).sort(), Object.keys(classes).sort());
const originalProps = (kind, reduced, { index = 0, presetId, equipUuid, hasSavedEquip = false } = {}) => recovered[kind](reduced, index, { id: presetId }, hasSavedEquip ? { equipUuid } : undefined);
let propComparisons = 0;
for (const kind of Object.keys(classes)) for (const reduced of [false, true]) for (const index of [0, 1, 3]) for (const hasSavedEquip of [false, true]) {
  const args = { index, hasSavedEquip, presetId: "preset-A", equipUuid: 42 };
  assert.deepEqual(currentProps(kind, reduced, args), originalProps(kind, reduced, args), kind); propComparisons++;
}
let clock = 1000, frameId = 0, frames = new Map(), media, mediaListeners = [];
const realPerformance = globalThis.performance;
Object.defineProperty(globalThis, "performance", { configurable: true, value: new Proxy(realPerformance, { get(target, key) { const value = target[key]; return key === "now" ? () => clock : typeof value === "function" ? value.bind(target) : value; } }) });
globalThis.requestAnimationFrame = window.requestAnimationFrame = (callback) => { const id = ++frameId; frames.set(id, callback); return id; };
globalThis.cancelAnimationFrame = window.cancelAnimationFrame = (id) => frames.delete(id);
window.matchMedia = (query) => { assert.equal(query, "(prefers-reduced-motion)"); return media; };
async function flush(action) { await React.act(async () => { action?.(); await Promise.resolve(); await Promise.resolve(); }); }
async function advance(milliseconds) { const end = clock + milliseconds; while (clock < end) { clock = Math.min(end, clock + 16); await flush(() => { const callbacks = [...frames.values()]; frames.clear(); callbacks.forEach((fn) => fn(clock)); }); } }
function engine(kind, reduced = false) {
  clock = 1000; frames = new Map(); mediaListeners = [];
  media = { matches: reduced, addEventListener(type, listener) { assert.equal(type, "change"); mediaListeners.push(listener); }, removeEventListener() {} };
  const code = kind === "original" ? `const {d:n,o:s,c:l}=(()=>{${helper} return{d,o,c}})();\n${closure}\nreturn{motion:$u,AnimatePresence:rl,useReducedMotion:ed};`
    : vendor.replace(/^import[^\n]+\n/gm, "").replace(/export \{[^\n]+\};?/, "") + "\nreturn{motion:$u,AnimatePresence:rl,useReducedMotion:ed};";
  return new Function("O", "A", code)(React, jsx);
}
async function mount(factory, initial = {}) {
  const host = document.createElement("div"); document.body.append(host); const root = createRoot(host); let state = initial;
  await flush(() => root.render(factory(state)));
  return { host, render: async (patch) => { state = { ...state, ...patch }; await flush(() => root.render(factory(state))); }, unmount: async () => { await flush(() => root.unmount()); host.remove(); } };
}
const snapshot = (host) => [...host.querySelectorAll("[data-motion]")].map((element) => ({ key: element.getAttribute("data-motion"), opacity: element.style.opacity, transform: element.style.transform }));
const cases = [];
async function compare(name, run, reduced = false) {
  const outcomes = [];
  for (const kind of ["original", "current"]) { const runtime = engine(kind, reduced); outcomes.push(await run(runtime, kind)); }
  assert.deepEqual(outcomes[1], outcomes[0], name); cases.push({ name, pass: true });
}
for (const kind of ["presets", "squad", "rename", "toast", "progress"]) await compare(`mounted-${kind}-entry`, async (runtime, implementation) => {
  const props = implementation === "original" ? originalProps : currentProps;
  const mounted = await mount(() => React.createElement(runtime.motion.div, { ...props(kind, false, { index: 2 }), "data-motion": kind }, kind));
  const samples = [snapshot(mounted.host)]; await advance(64); samples.push(snapshot(mounted.host)); await advance(600); samples.push(snapshot(mounted.host));
  assert.equal(samples[0][0].opacity, "0"); assert.equal(samples.at(-1)[0].opacity, "1"); assert.equal(samples.at(-1)[0].transform, "none");
  await mounted.unmount(); return samples;
});
await compare("presence-wait-retires-old-key-before-next-entry", async (runtime, implementation) => {
  const props = implementation === "original" ? originalProps : currentProps;
  const mounted = await mount((state) => React.createElement(runtime.AnimatePresence, { mode: "wait" }, React.createElement(runtime.motion.div, { ...props("presets", false), key: state.name, "data-motion": state.name }, state.name)), { name: "A" });
  await advance(300); await mounted.render({ name: "B" }); const samples = [snapshot(mounted.host)];
  assert.equal(samples[0][0].key, "A"); await advance(64); samples.push(snapshot(mounted.host)); assert.equal(samples[1][0].key, "A");
  await advance(650); samples.push(snapshot(mounted.host)); assert.equal(samples.at(-1)[0].key, "B");
  await mounted.unmount(); return samples;
});
for (const kind of ["rename", "toast", "progress"]) await compare(`presence-${kind}-exit-and-effect-cleanup`, async (runtime, implementation) => {
  const props = implementation === "original" ? originalProps : currentProps; let mounts = 0, cleanups = 0;
  function Content() { React.useEffect(() => { mounts++; return () => { cleanups++; }; }, []); return React.createElement(runtime.motion.div, { ...props(kind, false), "data-motion": kind }, kind); }
  const mounted = await mount((state) => React.createElement(runtime.AnimatePresence, null, state.show ? React.createElement(Content, { key: kind }) : null), { show: true });
  await advance(500); await mounted.render({ show: false }); const samples = [snapshot(mounted.host)]; assert.equal(cleanups, 0); assert.equal(samples[0].length, 1);
  await advance(64); samples.push(snapshot(mounted.host)); await advance(650); samples.push(snapshot(mounted.host));
  assert.equal(cleanups, 1); assert.equal(samples.at(-1).length, 0); await mounted.unmount(); assert.equal(mounts, 1); return { samples, cleanups };
});
await compare("slot-hover-tap-and-listener-cleanup", async (runtime, implementation) => {
  const props = implementation === "original" ? originalProps : currentProps;
  const mounted = await mount(() => React.createElement(runtime.motion.div, { ...props("slot", false, { hasSavedEquip: true, presetId: "A", equipUuid: 42 }), "data-motion": "slot" }, "slot"));
  const element = mounted.host.firstElementChild, pointer = (name, target = element) => { const event = new window.MouseEvent(name, { bubbles: true, button: 0, buttons: name === "pointerup" ? 0 : 1 }); Object.defineProperty(event, "pointerType", { value: "mouse" }); target.dispatchEvent(event); };
  const samples = [snapshot(mounted.host)]; await flush(() => pointer("pointerenter")); await advance(500); samples.push(snapshot(mounted.host));
  assert.notEqual(samples[1][0].transform, "none"); await flush(() => pointer("pointerdown")); await advance(500); samples.push(snapshot(mounted.host));
  await flush(() => { pointer("pointerup", window); pointer("pointerleave"); }); await advance(500); samples.push(snapshot(mounted.host));
  await mounted.unmount(); const frameCount = frames.size; await flush(() => { pointer("pointerenter"); pointer("pointerdown"); }); await advance(64);
  return { samples, noDetachedTree: !element.isConnected, pendingAfterUnmount: frames.size <= frameCount };
});
await compare("reduced-motion-source-snapshot-and-no-explicit-transforms", async (runtime, implementation) => {
  const props = implementation === "original" ? originalProps : currentProps; let hookValue;
  function Reduced() { hookValue = runtime.useReducedMotion(); return React.createElement(runtime.motion.div, { ...props("rename", hookValue), "data-motion": "reduced" }, "reduced"); }
  const mounted = await mount(() => React.createElement(Reduced)); const start = snapshot(mounted.host);
  assert.equal(hookValue, true); assert.equal(start[0].opacity, "1"); assert.equal(start[0].transform, "none");
  media.matches = false; mediaListeners.forEach((listener) => listener()); await flush(); assert.equal(hookValue, true);
  await mounted.unmount(); return { start, sourceSnapshotStaysUntilRemount: hookValue, globalListenerCount: mediaListeners.length };
}, true);
await compare("unmount-during-entry-retires-rendered-tree", async (runtime, implementation) => {
  const props = implementation === "original" ? originalProps : currentProps;
  const mounted = await mount(() => React.createElement(runtime.motion.div, { ...props("rename", false), "data-motion": "rename" }, "rename"));
  await advance(48); await mounted.unmount(); await advance(600); return { retainedNodes: document.querySelectorAll("[data-motion]").length, frames: frames.size };
});
Object.defineProperty(globalThis, "performance", { configurable: true, value: realPerformance }); dom.window.close();
const result = { marker: "LWB317_EQUIPMENT_MOTION_OK", propComparisons, cases: cases.length, details: cases, locators, sourceSha256: sha(original), vendorSha256: sha(vendor), wrapperSha256: sha(wrapper), proof: "Actual source/current engines, exact recovered caller props, mounted React/jsdom with controlled frame clock; physical browser/layout evidence separate" };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "motion-results.json"), JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify(result));
