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
const { JSDOM } = requireDom("jsdom");
const React = requireUi("react");
const jsx = requireUi("react/jsx-runtime");
let createRoot;
const { transformSync } = requireUi("esbuild");
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"), "utf8");
const current = fs.readFileSync(path.join(ui, "src/ProfileSidebar.jsx"), "utf8");
const sha = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
function between(start, end) {
  const a = asset.indexOf(start);
  assert.ok(a >= 0, start);
  const b = asset.indexOf(end, a + start.length);
  assert.ok(b > a, end);
  return { utf8ByteOffset: Buffer.byteLength(asset.slice(0, a)), utf8ByteLength: Buffer.byteLength(asset.slice(a, b)), sha256: sha(asset.slice(a, b)), text: asset.slice(a, b) };
}
const slices = {
  helpers: between("function ce(e,t)", "function A()"),
  errors: between("function Ir(e)", "var Rr="),
  icons: between("function Vr({", "function Hr("),
  connections: between("var Jr=", "function Yr("),
  sidebar: between("function Yr(", "function Xr("),
  dialog: between("function In({", "var Ln="),
  visibility: between("function se(e){return(e?.maxProfiles", "function O(e,t,n"),
  parent: between("rt=se(r.entitlement)", "function dt(e)"),
};
const { profileDisplay, reorderProfileIds, profileBatchIds, ProfileSidebar } = new Function("React", "h", "useEffect", "useRef", "useState", "useI18n", `${transformSync(current.replace(/^import[^\n]+\n/gm, "").replace(/export /g, ""), { loader: "jsx", jsxFactory: "h", target: "es2022" }).code}\nreturn {profileDisplay,reorderProfileIds,profileBatchIds,ProfileSidebar};`)(React, React.createElement, React.useEffect, React.useRef, React.useState, () => ({ t: translate }));
const originalHelpers = new Function(`${slices.helpers.text}\nreturn {ce,le,ue};`)();
const dom = new JSDOM("<!doctype html><html><body><button id='outside'>outside</button></body></html>", { url: "http://127.0.0.1/" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "KeyboardEvent", "Node", "MutationObserver", "localStorage"]) globalThis[name] = name === "window" ? dom.window : dom.window[name];
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
({ createRoot } = requireUi("react-dom/client"));
globalThis.fetch = () => { throw new Error("network forbidden in profile UI proof"); };
dom.window.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
dom.window.HTMLDialogElement.prototype.close = function () { this.open = false; };
dom.window.HTMLElement.prototype.getClientRects = function () { return this.isConnected ? [{ width: 100, height: 20 }] : []; };
const intervals = new Map(); let timer = 0;
window.setInterval = (fn, delay) => { intervals.set(++timer, { fn, delay }); return timer; };
window.clearInterval = (id) => intervals.delete(id);
let language = "en";
const catalogs = {};
for (const code of ["en", "ja"]) {
  const text = fs.readFileSync(path.join(ui, `src/locales/${code}.js`), "utf8");
  catalogs[code] = new Function(text.replace("export default", "return"))();
}
function translate(key, vars = {}) { return (catalogs[language][key] || key).replace(/\{(\w+)\}/g, (all, name) => String(vars[name] ?? all)); }
let environment;
const OriginalDialog = new Function("M", "b", `${slices.dialog.text}\nreturn In;`)(jsx, React);
const Original = new Function("M", "b", "Br", "Ar", "De", "Ze", "Qe", "tt", "In", `${slices.helpers.text}\n${slices.errors.text}\n${slices.icons.text}\n${slices.connections.text}\n${slices.sidebar.text}\nreturn Yr;`)(jsx, React, () => environment.provider, () => ({ profileLaunchErrors: environment.errors, clearProfileLaunchErrors: environment.clear }), () => ({ t: translate }), (...args) => environment.read(...args), (...args) => environment.start(...args), (...args) => environment.stop(...args), OriginalDialog);
const profiles = [
  { id: "a", roleName: "Avery", displayName: "Fallback A", serverId: 321, note: "", enabled: true },
  { id: "b", roleName: "", displayName: "Blake", serverId: 0, note: "東京", enabled: true },
  { id: "c", roleName: "Casey", displayName: "Fallback C", serverId: 322, note: "Note", enabled: false },
];
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };
const cases = []; let helperComparisons = 0;
function record(name, details = {}) { cases.push({ name, pass: true, ...details }); }
async function flush(fn) { await React.act(async () => { if (fn) fn(); await Promise.resolve(); await Promise.resolve(); }); }
function props(element) { const key = Object.keys(element).find((value) => value.startsWith("__reactProps$")); assert.ok(key); return element[key]; }
function normalized(container) {
  const clone = container.cloneNode(true);
  clone.querySelectorAll(".profile-upgrade").forEach((element) => element.remove());
  // React's boolean unknown focusable serialization is irrelevant to icon geometry.
  clone.querySelectorAll("svg").forEach((element) => element.removeAttribute("focusable"));
  return clone.innerHTML;
}
function createEnvironment(options = {}) {
  const calls = [];
  const instanceValues = options.instances || {};
  const state = { profiles: profiles.map((profile) => ({ ...profile })), selectedProfileId: "a", maxProfiles: 4, ...options.state };
  const provider = { state, busy: options.busy || false, error: options.error || "", select: (...args) => { calls.push(["select", ...args]); }, create: () => { calls.push(["create"]); }, reorder: (...args) => { calls.push(["reorder", ...args]); }, remove: async (...args) => { calls.push(["remove", ...args]); }, updateNote: async (...args) => { calls.push(["note", ...args]); } };
  return { calls, provider, errors: options.errors || [], read: options.read || (async (id) => instanceValues[id] || null),
    start: async (id) => { calls.push(["start", id]); return { instanceId: `instance-${id}`, connectionState: "connected" }; },
    stop: async (...args) => { calls.push(["stop", ...args]); }, clear: () => { calls.push(["clear"]); },
    restart: async () => { calls.push(["restart"]); },
  };
}
async function mount(kind, env) {
  environment = env;
  const container = document.createElement("div"); document.body.append(container);
  const root = createRoot(container);
  function element() {
    return kind === "original" ? React.createElement(Original, { focusGameOnProfileSelect: false, onOpenAccount: () => {}, onRestartAll: env.restart })
      : React.createElement(ProfileSidebar, { state: env.provider.state, busy: env.provider.busy, error: env.provider.error, profileLaunchErrors: env.errors, focusGameOnProfileSelect: false,
        onSelect: env.provider.select, onCreate: env.provider.create, onRemove: env.provider.remove, onReorder: env.provider.reorder, onUpdateNote: env.provider.updateNote,
        readInstance: env.read, onStartProfile: env.start, onStopProfile: env.stop, onRestartAll: env.restart, onClearProfileLaunchErrors: env.clear });
  }
  await flush(() => root.render(element()));
  return { container, env, rerender: () => flush(() => root.render(element())), dispose: async () => { if (container.contains(document.activeElement)) document.activeElement.blur(); container.remove(); await flush(() => root.unmount()); },
    click: async (selector) => { const control = container.querySelector(selector); assert.ok(control, selector); await flush(() => control.click()); return control; },
  };
}
async function compareScenario(name, options, actions = async () => {}) {
  if (process.argv.includes("--trace")) console.error(name);
  const snapshots = [];
  for (const kind of ["original", "current"]) {
    if (process.argv.includes("--trace")) console.error("kind",kind);
    const env = createEnvironment(options);
    const mounted = await mount(kind, env);
    try { await actions(mounted, kind); snapshots.push({ html: normalized(mounted.container), calls: env.calls }); }
    finally { await mounted.dispose(); }
  }
  assert.deepEqual(snapshots[1], snapshots[0], name);
  record(name);
}

for (const code of ["en", "ja"]) {
  language = code;
  for (const profile of profiles.concat([{ id: "x", displayName: "Fallback", note: null, serverId: null }])) {
    assert.deepEqual(profileDisplay(profile, translate), originalHelpers.ce(profile, translate)); helperComparisons++;
  }
}
for (const source of ["a", "b", "c", "missing"]) for (const target of ["a", "b", "c", "missing"]) {
  assert.deepEqual(reorderProfileIds(["a", "b", "c"], source, target), originalHelpers.le(["a", "b", "c"], source, target)); helperComparisons++;
}
for (const instance of [null, { phase: "error", pid: null }, { phase: "error", pid: 123 }, { phase: "running", pid: 123 }]) for (const action of ["start", "stop"]) {
  assert.deepEqual(profileBatchIds(action, profiles, { a: instance, c: instance }), originalHelpers.ue(action, profiles, { a: instance, c: instance })); helperComparisons++;
}
record("exact-helper-comparisons", { comparisons: helperComparisons });

for (const code of ["en", "ja"]) for (const connection of ["offline", "starting", "recovering", "awaitingLogin", "connected", "reconnecting", "grace", "locked", "error"]) for (const busy of [false, true]) for (const expanded of [false, true]) {
  language = code;
  await compareScenario(`${code}-${connection}-${busy ? "busy" : "idle"}-${expanded ? "expanded" : "compact"}`, { busy, instances: { a: { phase: "running", pid: 12, instanceId: "i-a", connectionState: connection } } }, async (mounted) => {
    if (expanded) await mounted.click(".profile-collapse");
  });
}
language = "en";
await compareScenario("compact-profile-selection-passes-focus-flag", {}, async (mounted) => { await mounted.click(".profile-compact-item:nth-child(2)"); });
await compareScenario("expanded-profile-selection", {}, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click(".profile-row:nth-child(2) .profile-item"); });
await compareScenario("locked-disabled-and-capacity", { state: { maxProfiles: 3, profiles: profiles.map((profile) => ({ ...profile, lockedReason: profile.id === "b" ? "LOCKED" : "" })) } }, async (mounted) => { await mounted.click(".profile-collapse"); });
await compareScenario("external-error-forces-expanded", { error: Object.assign(new Error("INVALID_GAME_ROOT"), { code: "INVALID_GAME_ROOT" }) });
await compareScenario("launch-errors-force-expanded-in-source-order", { errors: [{ profileId: "b", error: "INVALID_GAME_ROOT" }, { profileId: "missing", error: "UNKNOWN_ERROR_CODE" }] });
await compareScenario("create-callback", {}, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click("button.profile-add"); });
await compareScenario("batch-start-sequential-enabled-only", {}, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click(".profile-batch-actions button:first-child"); });
await compareScenario("batch-stop-retains-disabled-instances", { instances: { a: { instanceId: "i-a", phase: "running", pid: 123 }, c: { instanceId: "i-c", phase: "running", pid: 456 } } }, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click(".profile-batch-actions button:last-child"); });
await compareScenario("single-start", {}, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click(".profile-row:first-child .profile-run"); });
await compareScenario("single-stop", { instances: { a: { instanceId: "i-a", phase: "running", pid: 123 } } }, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click(".profile-row:first-child .profile-run"); });
await compareScenario("failed-instance-with-no-pid-is-startable", { instances: { a: { instanceId: "i-a", phase: "error", pid: null } } }, async (mounted) => { await mounted.click(".profile-collapse"); await mounted.click(".profile-row:first-child .profile-run"); });
await compareScenario("drag-insert-before-target", {}, async (mounted) => {
  await mounted.click(".profile-collapse");
  const handle = mounted.container.querySelector(".profile-row:last-child .profile-drag-handle");
  const transfer = { setData: () => {}, effectAllowed: "" };
  await flush(() => props(handle).onDragStart({ dataTransfer: transfer }));
  assert.equal(transfer.effectAllowed, "move");
  const target = mounted.container.querySelector(".profile-row:first-child");
  await flush(() => props(target).onDragOver({ preventDefault() {} }));
  await flush(() => props(target).onDrop({ preventDefault() {} }));
});
for (const accept of [false, true]) await compareScenario(`delete-confirm-${accept}`, {}, async (mounted) => {
  window.confirm = () => accept;
  await mounted.click(".profile-collapse"); await mounted.click(".profile-row:nth-child(2) .profile-delete");
});
await compareScenario("delete-synchronous-provider-failure-translates", {}, async (mounted) => {
  window.confirm = () => true;
  mounted.env.provider.remove = () => { throw new Error("INVALID_GAME_ROOT"); };
  await mounted.rerender(); await mounted.click(".profile-collapse"); await mounted.click(".profile-row:nth-child(2) .profile-delete");
});
await compareScenario("note-modal-shape-save-raw-value", {}, async (mounted) => {
  await mounted.click(".profile-collapse"); await mounted.click(".profile-row:nth-child(2) .profile-note-edit");
  const input = mounted.container.querySelector("dialog input"); assert.equal(input.maxLength, 80); assert.equal(input.value, "東京");
  await flush(() => props(input).onChange({ target: { value: "  retained  " } }));
  await flush(() => props(mounted.container.querySelector("dialog form")).onSubmit({ preventDefault() {} }));
});
for (const result of ["success", "failure"]) await compareScenario(`note-deferred-${result}-busy-and-cancel`, {}, async (mounted) => {
  const response = deferred();
  mounted.env.provider.updateNote = (...args) => { mounted.env.calls.push(["note", ...args]); return response.promise; };
  await mounted.rerender(); await mounted.click(".profile-collapse"); await mounted.click(".profile-row:first-child .profile-note-edit");
  await flush(() => props(mounted.container.querySelector("dialog form")).onSubmit({ preventDefault() {} }));
  mounted.env.provider.busy = true; await mounted.rerender();
  const dialog = mounted.container.querySelector("dialog"); assert.ok(dialog.open);
  await flush(() => props(dialog).onCancel({ preventDefault() {} })); assert.ok(mounted.container.querySelector("dialog"));
  mounted.env.provider.busy = false; await mounted.rerender();
  await flush(() => result === "success" ? response.resolve() : response.reject(new Error("INVALID_GAME_ROOT")));
  if (result === "success") assert.equal(mounted.container.querySelector("dialog"), null);
  else assert.ok(mounted.container.querySelector("dialog"));
});
await compareScenario("note-escape-cancel-source-focus-lifetime", {}, async (mounted) => {
  await mounted.click(".profile-collapse");
  const trigger = mounted.container.querySelector(".profile-row:first-child .profile-note-edit"); trigger.focus();
  await mounted.click(".profile-row:first-child .profile-note-edit");
  await flush(() => props(mounted.container.querySelector("dialog")).onCancel({ preventDefault() {} }));
  mounted.env.calls.push(["focus-after-cancel", document.activeElement.tagName, document.activeElement.className]);
});
await compareScenario("native-start-restart-required-and-retry-presentation", {}, async (mounted) => {
  mounted.env.start = async () => { throw Object.assign(new Error("LAUNCH_TICKET_RESTART_REQUIRED"), { code: "LAUNCH_TICKET_RESTART_REQUIRED" }); };
  await mounted.rerender(); await mounted.click(".profile-collapse"); await mounted.click(".profile-row:first-child .profile-run");
  const retry = [...mounted.container.querySelectorAll("button.profile-add")].at(-1); assert.equal(retry.textContent, translate("profile.restartAll"));
  await flush(() => retry.click());
});

for (const kind of ["original", "current"]) {
  const pending = deferred(); const reads = [];
  const env = createEnvironment({ read: (id) => { reads.push(id); return pending.promise; } });
  const mounted = await mount(kind, env);
  const timerEntry = [...intervals.values()]; assert.equal(timerEntry.length, 1); assert.equal(timerEntry[0].delay, 3000);
  await flush(() => { timerEntry[0].fn(); timerEntry[0].fn(); });
  assert.deepEqual(reads, ["a", "b", "c"], `${kind} poll overlap`);
  await flush(() => pending.resolve({ phase: "running", pid: 12, connectionState: "connected", instanceId: "i-a" }));
  assert.ok(mounted.container.querySelector(".profile-dot.connected"));
  await flush(() => timerEntry[0].fn());
  assert.equal(reads.length, 6);
  await mounted.dispose(); assert.equal(intervals.size, 0);
}
record("original-current-3000ms-poll-overlap-and-settlement", { comparisons: 2 });
for (const kind of ["original", "current"]) {
  const pending = deferred(); const env = createEnvironment({ read: () => pending.promise });
  const mounted = await mount(kind, env);
  await mounted.dispose(); assert.equal(intervals.size, 0);
  await flush(() => pending.resolve({ phase: "running", pid: 12, connectionState: "connected" }));
  assert.equal(mounted.container.childNodes.length, 0);
}
record("original-current-obsolete-poll-after-unmount", { comparisons: 2 });
await compareScenario("read-failure-clears-instance-like-original", { read: async () => { throw new Error("INERT_READ_FAILED"); } });

await compareScenario("note-modal-Tab-loop", {}, async (mounted) => {
  await mounted.click(".profile-collapse"); await mounted.click(".profile-row:first-child .profile-note-edit");
  const dialog = mounted.container.querySelector("dialog");
  const controls = [...dialog.querySelectorAll("input,button")];
  controls.at(-1).focus(); let prevented = false;
  await flush(() => props(dialog).onKeyDown({ currentTarget: dialog, stopPropagation() {}, key: "Tab", shiftKey: false, preventDefault() { prevented = true; } }));
  assert.ok(prevented); assert.equal(document.activeElement, controls[0]);
  prevented = false;
  await flush(() => props(dialog).onKeyDown({ currentTarget: dialog, stopPropagation() {}, key: "Tab", shiftKey: true, preventDefault() { prevented = true; } }));
  assert.ok(prevented); assert.equal(document.activeElement, controls.at(-1));
});

// Missing local/native providers are deliberate fail-closed differences from the original.
const container = document.createElement("div"); document.body.append(container); const root = createRoot(container);
await flush(() => root.render(React.createElement(ProfileSidebar, { state: createEnvironment().provider.state })));
assert.ok([...container.querySelectorAll(".profile-compact-item")].every((button) => button.disabled));
await flush(() => container.querySelector(".profile-collapse").click());
assert.ok([...container.querySelectorAll(".profile-run,.profile-add,.profile-delete,.profile-note-edit,.profile-batch-actions button")].every((button) => button.disabled));
assert.equal(container.querySelector(".profile-upgrade"), null);
await flush(() => root.unmount()); container.remove();
assert.equal(intervals.size, 0);
record("provider-absence-fences-and-no-commercial-account-controls");

const result = { marker: "LWB317_FINAL_PROFILES_OK", cases: cases.length, helperComparisons, sourceSha256: sha(asset), productionSha256: sha(current), originalComparisons: cases.filter((item) => item.name !== "provider-absence-fences-and-no-commercial-account-controls").length, details: cases,
  limits: ["Controlled mounted React and jsdom dialog lifecycle; no physical HTML5 drag claim", "No local profile producer/persistence integrated by this isolated unit", "No original service entitlement or gameplay/native start/stop", "Upgrade/account controls excluded intentionally; absent-provider controls disabled intentionally"],
  locators: Object.fromEntries(Object.entries(slices).map(([key, { text, ...locator }]) => [key, locator])),
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result));
