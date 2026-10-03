import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const domPackage = process.env.LWB317_JSDOM_PACKAGE || "C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json";
const requireDom = createRequire(domPackage);
const { JSDOM } = requireDom("jsdom");
const { parse } = requireUi("@babel/parser");
const { transformSync } = requireUi("esbuild");
const React = requireUi("react");
const jsxRuntime = requireUi("react/jsx-runtime");
const { createRoot } = requireUi("react-dom/client");

const assetPath = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js");
const asset = fs.readFileSync(assetPath, "utf8");
const currentPath = path.join(ui, "src/App.jsx");
const currentSource = fs.readFileSync(currentPath, "utf8");
const baselinePath = path.join(here, "baseline-55c9ca1-App.jsx");
const baselineSource = fs.readFileSync(baselinePath, "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };

const dom = new JSDOM("<!doctype html><html><body><button id='outside'>outside</button></body></html>", { url: "http://127.0.0.1/?previewPage=overview" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "KeyboardEvent", "Node", "MutationObserver", "localStorage"]) {
  globalThis[name] = name === "window" ? dom.window : dom.window[name];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("network access forbidden in cross-server recovery proof"); };
window.matchMedia = () => ({ matches: false });
const intervals = new Map();
let timerSerial = 0;
window.setInterval = (fn, delay) => { intervals.set(++timerSerial, { fn, delay }); return timerSerial; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = () => ++timerSerial;
window.clearTimeout = () => {};

function sliceBetween(source, startText, endText) {
  const start = source.indexOf(startText);
  assert.ok(start >= 0, `missing ${startText}`);
  const end = source.indexOf(endText, start);
  assert.ok(end > start, `missing ${endText}`);
  return { start, text: source.slice(start, end) };
}

const originalPopover = sliceBetween(asset, "function ti(", "var ni=");
const originalPopoverBlock = sliceBetween(asset, "var $r=`lastwar.serverJumpHistory`", "var ni=");
const originalParent = sliceBetween(asset, "async function Mt(e){", "async function Nt()");
const originalApiStart = asset.indexOf("function Kt(e){return N(`server_jump`");
assert.ok(originalApiStart >= 0);

function locator(source, start, end) {
  const text = source.slice(start, end);
  return { utf8ByteOffset: Buffer.byteLength(source.slice(0, start)), utf8ByteLength: Buffer.byteLength(text), sha256: sha256(text) };
}

const currentAst = parse(currentSource, { sourceType: "module", plugins: ["jsx"] });
const walk = (node, list = []) => {
  if (!node || typeof node !== "object") return list;
  if (node.type) list.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, list));
    else if (value && typeof value === "object") walk(value, list);
  }
  return list;
};
const currentNodes = walk(currentAst);
const legacyNode = currentNodes.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "legacyServerHistory");
const jumpNode = currentNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === "jumpServer");
const importEffectNode = currentNodes.find((node) => node.type === "CallExpression" && node.callee?.name === "useEffect" && currentSource.slice(node.start, node.end).includes("importServerJumpHistory"));
const renderNode = currentNodes.find((node) => node.type === "JSXElement" && node.openingElement?.attributes?.some((attribute) => attribute.name?.name === "className" && attribute.value?.value === "server-jump"));
for (const [name, node] of [["legacyServerHistory", legacyNode], ["jumpServer", jumpNode], ["historyImportEffect", importEffectNode], ["serverJumpRender", renderNode]]) assert.ok(node, `missing current locator ${name}`);

const originalApiLocators = Object.fromEntries([
  ["serverJump", sliceBetween(asset, "function Kt(e){return N(`server_jump`", "function qt(")],
  ["historySet", sliceBetween(asset, "function fn(e,t){return N(`server_jump_history_set`", "function pn(")],
  ["historyImport", sliceBetween(asset, "function pn(e,t){return N(`server_jump_history_import`", "function mn(")],
  ["mapSummary", sliceBetween(asset, "function vn(e){return N(`map_summary`", "function yn(")],
].map(([name, item]) => [name, locator(asset, item.start, item.start + item.text.length)]));

const locators = {
  original: {
    popover: { utf8ByteOffset: Buffer.byteLength(asset.slice(0, originalPopover.start)), utf8ByteLength: Buffer.byteLength(originalPopover.text), sha256: sha256(originalPopover.text) },
    popoverWithLegacyHistoryHelper: { utf8ByteOffset: Buffer.byteLength(asset.slice(0, originalPopoverBlock.start)), utf8ByteLength: Buffer.byteLength(originalPopoverBlock.text), sha256: sha256(originalPopoverBlock.text) },
    parentJump: { utf8ByteOffset: Buffer.byteLength(asset.slice(0, originalParent.start)), utf8ByteLength: Buffer.byteLength(originalParent.text), sha256: sha256(originalParent.text) },
    serverJumpApiMarker: { utf8ByteOffset: Buffer.byteLength(asset.slice(0, originalApiStart)) },
    api: originalApiLocators,
    headerOnJumpBinding: { utf8ByteOffset: Buffer.byteLength(asset.slice(0, asset.indexOf("onJumpServer:Mt"))) },
  },
  baseline: { path: "recovery/baseline-55c9ca1-App.jsx", sha256: sha256(baselineSource), bytes: Buffer.byteLength(baselineSource) },
  current: {
    path: "src/LWBridge.UI-0.3.17/src/App.jsx",
    sha256: sha256(currentSource),
    bytes: Buffer.byteLength(currentSource),
    legacyServerHistory: locator(currentSource, legacyNode.start, legacyNode.end),
    historyImportEffect: locator(currentSource, importEffectNode.start, importEffectNode.end),
    jumpServer: locator(currentSource, jumpNode.start, jumpNode.end),
    serverJumpRender: locator(currentSource, renderNode.start, renderNode.end),
  },
  normalizedDiff: currentSource.replaceAll("\r\n", "\n") === baselineSource.replaceAll("\r\n", "\n") ? "EQUAL" : "DIFFERENT",
};
fs.writeFileSync(path.join(here, "source-locators.json"), `${JSON.stringify(locators, null, 2)}\n`);
assert.equal(locators.normalizedDiff, "EQUAL", "required 55c9ca1 baseline must match current before corrections");

let language = "en";
const t = (key, vars = {}) => `${language}:${key}${Object.keys(vars).length ? `:${JSON.stringify(vars)}` : ""}`;
const originalImports = [];
const originalSets = [];
let originalImportImpl = async (history, profileId) => { originalImports.push({ history, profileId }); return history; };
let originalSetImpl = async (history, profileId) => { originalSets.push({ history, profileId }); return history; };
const OriginalPopover = new Function("M", "b", "De", "pn", "fn", `${originalPopoverBlock.text}\nreturn ti;`)(
  jsxRuntime,
  React,
  () => ({ t }),
  (...args) => originalImportImpl(...args),
  (...args) => originalSetImpl(...args),
);

function originalParentHandler(bindings) {
  return new Function("Kt", "_t", "F", `${originalParent.text}\nreturn Mt;`)(bindings.Kt, bindings._t, bindings.F);
}

async function actFlush(fn) {
  await React.act(async () => {
    if (fn) await fn();
    await Promise.resolve();
    await Promise.resolve();
  });
}

function reactProps(element) {
  const key = Object.keys(element).find((name) => name.startsWith("__reactProps$"));
  assert.ok(key, "rendered element missing React props handle");
  return element[key];
}

function setInput(input, value) {
  reactProps(input).onChange({ target: { value } });
}

function pressEnter(input) {
  reactProps(input).onKeyDown({ key: "Enter" });
}

async function mountOriginal(props) {
  const host = document.createElement("div");
  document.body.append(host);
  const root = createRoot(host);
  await actFlush(() => root.render(React.createElement(OriginalPopover, props)));
  return {
    host,
    root,
    rerender: async (next) => actFlush(() => root.render(React.createElement(OriginalPopover, next))),
    stop: async () => { await actFlush(() => root.unmount()); host.remove(); },
  };
}

function popoverSnapshot(host) {
  const pop = host.querySelector(".server-jump-popover");
  const input = host.querySelector(".server-jump-form input");
  const trigger = host.querySelector(".server-jump > button");
  return {
    open: Boolean(pop),
    inputValue: input?.value ?? null,
    inputDisabled: input?.disabled ?? null,
    triggerDisabled: trigger?.disabled ?? null,
    errors: [...host.querySelectorAll(".server-jump-error")].map((node) => node.textContent),
    buttons: [...host.querySelectorAll(".server-jump-popover button")].map((button) => ({ text: button.textContent, disabled: button.disabled })),
  };
}

const originalCases = [];
async function runOriginalCases() {
  localStorage.clear();
  originalImports.length = 0;
  originalSets.length = 0;
  language = "en";
  localStorage.setItem("lastwar.serverJumpHistory", JSON.stringify([324, "325", 324, 0, 100000, 326, 327, 328]));
  originalImportImpl = async (history, profileId) => { originalImports.push({ history, profileId }); return [330, 324]; };
  const base = { profileId: "profile-a", currentServerId: 321, homeServerId: 320, seasonServerIds: [321, 322], truckMatchServerIds: [323, 321], online: true, scanActive: false, onJump: async (serverId) => ({ changed: false, previousServerId: serverId, serverId }) };
  const mounted = await mountOriginal(base);
  assert.deepEqual(originalImports, [{ history: [324, 325, 326, 327, 328], profileId: "profile-a" }]);
  assert.equal(localStorage.getItem("lastwar.serverJumpHistory"), null);
  await actFlush(() => mounted.host.querySelector(".server-jump > button").click());
  let snap = popoverSnapshot(mounted.host);
  assert.equal(snap.open, true);
  assert.ok(snap.buttons.some((b) => b.text === "321" && b.disabled));
  assert.ok(snap.buttons.some((b) => b.text === "330" && !b.disabled));
  originalCases.push({ name: "migration and source lists", status: "PASS", import: originalImports[0], snapshot: snap });

  await actFlush(() => document.getElementById("outside").dispatchEvent(new Event("pointerdown", { bubbles: true })));
  assert.equal(popoverSnapshot(mounted.host).open, false);
  originalCases.push({ name: "pointerdown outside closes", status: "PASS" });

  await actFlush(() => mounted.host.querySelector(".server-jump > button").click());
  const input = mounted.host.querySelector(".server-jump-form input");
  await actFlush(() => setInput(input, "0"));
  await actFlush(() => pressEnter(input));
  snap = popoverSnapshot(mounted.host);
  assert.deepEqual(snap.errors, ["en:server.invalidId"]);
  originalCases.push({ name: "invalid Enter localized event-time error", status: "PASS", snapshot: snap });

  language = "ja";
  await mounted.rerender(base);
  assert.deepEqual(popoverSnapshot(mounted.host).errors, ["en:server.invalidId"]);
  originalCases.push({ name: "stored action error retains event-time locale", status: "PASS", snapshot: popoverSnapshot(mounted.host) });

  const offline = { ...base, online: false };
  await mounted.rerender(offline);
  const offlineInput = mounted.host.querySelector(".server-jump-form input");
  assert.equal(offlineInput.disabled, false);
  await actFlush(() => pressEnter(offlineInput));
  snap = popoverSnapshot(mounted.host);
  assert.deepEqual(snap.errors, ["ja:status.gameDisconnected", "en:server.invalidId"]);
  originalCases.push({ name: "offline Enter blocked without replacing stored error", status: "PASS", snapshot: snap });

  originalImports.length = 0;
  originalImportImpl = async (history, profileId) => { originalImports.push({ history, profileId }); return [401]; };
  await mounted.rerender({ ...base, profileId: "profile-b" });
  assert.deepEqual(originalImports, [{ history: [], profileId: "profile-b" }]);
  assert.deepEqual(popoverSnapshot(mounted.host).errors, []);
  originalCases.push({ name: "profile change resets history/error and reimports", status: "PASS", import: originalImports[0] });
  await mounted.stop();

  // Execute the exact original parent callback and exact popover together for ordering/deferred acknowledgement.
  for (const changed of [false, true]) {
    const log = [];
    const summaryGate = deferred();
    let jumpResult = { changed, previousServerId: 321, serverId: changed ? 322 : 321 };
    const parent = originalParentHandler({
      Kt: async (serverId) => { log.push(`jump:${serverId}`); return jumpResult; },
      _t: async () => { log.push("summary:start"); return summaryGate.promise.then((value) => { log.push("summary:done"); return value; }); },
      F: (message) => log.push(`log:${message}`),
    });
    originalSetImpl = async (history, profileId) => { log.push(`history:${history.join(",")}:${profileId}`); return history; };
    originalImportImpl = async () => [];
    language = "en";
    const s = await mountOriginal({ ...base, onJump: parent });
    await actFlush(() => s.host.querySelector(".server-jump > button").click());
    const field = s.host.querySelector("input");
    await actFlush(() => setInput(field, changed ? "322" : "321"));
    await actFlush(() => pressEnter(field));
    assert.equal(popoverSnapshot(s.host).open, true);
    assert.deepEqual(log.slice(0, 2), [`jump:${changed ? 322 : 321}`, "summary:start"]);
    const pending = popoverSnapshot(s.host);
    await actFlush(() => summaryGate.resolve({ ok: true }));
    assert.equal(popoverSnapshot(s.host).open, false);
    if (changed) assert.ok(log.some((entry) => entry.startsWith("history:322")));
    else assert.ok(!log.some((entry) => entry.startsWith("history:")));
    const summaryDone = log.indexOf("summary:done");
    const historyIndex = log.findIndex((entry) => entry.startsWith("history:"));
    if (changed) assert.ok(summaryDone < historyIndex);
    originalCases.push({ name: `${changed ? "changed" : "unchanged"} waits for summary before close/history`, status: "PASS", pending, log: [...log] });
    await s.stop();
  }

  // Original summary rejection is swallowed by parent; changed history still follows and the popover closes without action error.
  {
    const log = [];
    const parent = originalParentHandler({
      Kt: async () => { log.push("jump"); return { changed: true, previousServerId: 321, serverId: 322 }; },
      _t: async () => { log.push("summary"); throw new Error("controlled summary failure"); },
      F: (message) => log.push(`log:${message}`),
    });
    originalSetImpl = async (history) => { log.push("history"); return history; };
    const s = await mountOriginal({ ...base, onJump: parent });
    await actFlush(() => s.host.querySelector(".server-jump > button").click());
    const field = s.host.querySelector("input");
    await actFlush(() => setInput(field, "322"));
    await actFlush(() => pressEnter(field));
    assert.equal(popoverSnapshot(s.host).open, false);
    assert.deepEqual(log.filter((entry) => ["jump", "summary", "history"].includes(entry)), ["jump", "summary", "history"]);
    await actFlush(() => s.host.querySelector(".server-jump > button").click());
    assert.deepEqual(popoverSnapshot(s.host).errors, []);
    originalCases.push({ name: "summary rejection swallowed before changed history acknowledgement", status: "PASS", log, reopened: popoverSnapshot(s.host) });
    await s.stop();
  }
}

const routesModule = await import(pathToFileURL(path.join(ui, "src/routes.js")).href);
const mapBackendModule = await import(pathToFileURL(path.join(ui, "src/mapBackend.js")).href);
const autoModule = await import(pathToFileURL(path.join(ui, "src/mapAutoConfig.js")).href);
const currentCases = [];

function compileApp(source, bridge, api, onlineRef) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const bindings = { h: React.createElement };
  const stableSetLanguage = () => {};
  function InertPage(props) { return React.createElement("div", { "data-route": props.routeKey }); }
  function InertIcon() { return null; }
  for (const node of ast.program.body.filter((n) => n.type === "ImportDeclaration")) {
    const from = node.source.value;
    let module = null;
    if (from === "react") module = React;
    else if (from === "./routes.js") module = routesModule;
    else if (from === "./mapBackend.js") module = mapBackendModule;
    else if (from === "./mapAutoConfig.js") module = autoModule;
    for (const spec of node.specifiers) {
      const imported = spec.imported?.name || "default";
      const local = spec.local.name;
      if (local === "backendBridge") bindings[local] = bridge;
      else if (local === "createMapApi") bindings[local] = () => api;
      else if (local === "connectionState") bindings[local] = () => onlineRef.value ? "connected" : "disconnected";
      else if (local === "PageForRoute") bindings[local] = InertPage;
      else if (local === "NavIcon") bindings[local] = InertIcon;
      else if (local === "useI18n") bindings[local] = () => ({ language, setLanguage: stableSetLanguage, t });
      else if (local === "LANGUAGES") bindings[local] = [{ code: "en", name: "English" }, { code: "ja", name: "日本語" }];
      else if (from.endsWith(".png")) bindings[local] = "fixture-dot.png";
      else if (module) bindings[local] = module[imported];
      else throw new Error(`unbound App import ${from} ${imported} as ${local}`);
    }
  }
  const statements = ast.program.body.filter((n) => n.type !== "ImportDeclaration").map((n) => n.type === "ExportNamedDeclaration" ? n.declaration : n).filter(Boolean);
  const code = transformSync(statements.map((n) => source.slice(n.start, n.end)).join("\n"), { loader: "jsx", jsxFactory: "h", target: "es2022" }).code;
  return new Function(...Object.keys(bindings), `${code}\nreturn App;`)(...Object.values(bindings));
}

async function currentSession(source, label, body) {
  localStorage.clear();
  intervals.clear();
  language = "en";
  const actionLog = [];
  const onlineRef = { value: true };
  let scan = { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: 321, homeServerId: 320, seasonServerIds: [321, 322], truckMatchServerIds: [323, 321], isReading: false };
  let summaryImpl = async () => ({ serverId: scan.serverId, counts: {}, scanState: scan });
  let jumpImpl = async (serverId) => ({ changed: serverId !== scan.serverId, previousServerId: scan.serverId, serverId });
  let historyImportImpl = async (history) => history;
  let historySetImpl = async (history) => history;
  const bridge = {
    mode: "native", available: true, profileId: "profile-a",
    listen: () => () => {},
    invoke: async (command) => {
      actionLog.push(`bridge:${command}`);
      if (command === "game_root_status") return { valid: true, root: "C:/Fixture/Game" };
      if (command === "game_recovery_status") return { state: "idle" };
      if (command === "local_config_get") return { autoLaunchGame: false, autoReconnect: false };
      throw new Error(`unexpected bridge command ${command}`);
    },
  };
  const api = {
    readStatus: async () => { actionLog.push("readStatus"); return { xluaOnline: onlineRef.value, pending: 0 }; },
    readProxyStatus: async () => { actionLog.push("readProxyStatus"); return { gameRunning: true, repairRequired: false }; },
    summary: async () => { actionLog.push("summary"); return summaryImpl(); },
    listenStatus: () => () => {}, listenScanStatus: () => () => {},
    importServerJumpHistory: async (history) => { actionLog.push(`import:${bridge.profileId}:${history.join(",")}`); return historyImportImpl(history); },
    setServerJumpHistory: async (history) => { actionLog.push(`history:${history.join(",")}`); return historySetImpl(history); },
    jumpServer: async (serverId) => { actionLog.push(`jump:${serverId}`); const result = await jumpImpl(serverId); if (result.changed) scan = { ...scan, serverId: result.serverId }; return result; },
  };
  const App = compileApp(source, bridge, api, onlineRef);
  const host = document.createElement("div"); document.body.append(host); const root = createRoot(host);
  await actFlush(() => root.render(React.createElement(App)));
  await actFlush();
  const helpers = {
    label,
    host,
    bridge,
    api,
    actionLog,
    onlineRef,
    setScan: (next) => { scan = { ...scan, ...next }; },
    setSummaryImpl: (fn) => { summaryImpl = fn; },
    setJumpImpl: (fn) => { jumpImpl = fn; },
    setHistoryImportImpl: (fn) => { historyImportImpl = fn; },
    setHistorySetImpl: (fn) => { historySetImpl = fn; },
    rerender: async () => actFlush(() => root.render(React.createElement(App))),
    open: async () => actFlush(() => host.querySelector(".server-jump > button").click()),
    enter: async (value) => {
      const input = host.querySelector(".server-jump-form input");
      await actFlush(() => setInput(input, value));
      await actFlush(() => pressEnter(input));
    },
    snapshot: () => popoverSnapshot(host),
  };
  try { return await body(helpers); }
  finally { await actFlush(() => root.unmount()); host.remove(); intervals.clear(); }
}

async function runCurrentCases(source, label) {
  await currentSession(source, label, async (h) => {
    localStorage.setItem("lastwar.serverJumpHistory", JSON.stringify([410]));
    // Import already ran on initial mount, so profile switching exposes the [] dependency mismatch.
    h.actionLog.length = 0;
    h.bridge.profileId = "profile-b";
    await h.rerender();
    assert.ok(!h.actionLog.some((entry) => entry.startsWith("import:")));
    currentCases.push({ implementation: label, name: "profile change does not reimport/reset history", status: "MISMATCH", actionLog: [...h.actionLog] });
  });

  await currentSession(source, label, async (h) => {
    await h.open();
    await actFlush(() => document.getElementById("outside").dispatchEvent(new Event("pointerdown", { bubbles: true })));
    assert.equal(h.snapshot().open, true);
    currentCases.push({ implementation: label, name: "pointerdown outside leaves popover open", status: "MISMATCH", snapshot: h.snapshot() });
  });

  await currentSession(source, label, async (h) => {
    h.onlineRef.value = false;
    await h.rerender();
    await h.open();
    await h.enter("322");
    const snapshot = h.snapshot();
    assert.deepEqual(snapshot.errors, ["en:status.gameDisconnected", "Game disconnected"]);
    currentCases.push({ implementation: label, name: "offline Enter submits into handler and duplicates feedback", status: "MISMATCH", snapshot });
  });

  for (const changed of [false, true]) {
    await currentSession(source, label, async (h) => {
      const gate = deferred();
      h.setJumpImpl(async (serverId) => ({ changed, previousServerId: 321, serverId: changed ? 322 : 321 }));
      h.setSummaryImpl(() => gate.promise);
      h.actionLog.length = 0;
      await h.open();
      await h.enter(changed ? "322" : "321");
      assert.equal(h.snapshot().open, false);
      const beforeSummary = { snapshot: h.snapshot(), actionLog: [...h.actionLog] };
      assert.ok(h.actionLog.includes("summary"));
      if (changed) assert.ok(h.actionLog.indexOf("history:322") < h.actionLog.indexOf("summary"));
      assert.ok(h.actionLog.includes("readStatus"));
      await actFlush(() => gate.resolve({ serverId: changed ? 322 : 321, counts: {}, scanState: { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: changed ? 322 : 321 } }));
      currentCases.push({ implementation: label, name: `${changed ? "changed" : "unchanged"} closes before deferred summary and refreshes status`, status: "MISMATCH", beforeSummary, final: h.snapshot(), actionLog: [...h.actionLog] });
    });
  }

  await currentSession(source, label, async (h) => {
    h.setJumpImpl(async () => ({ changed: true, previousServerId: 321, serverId: 322 }));
    let actionSummary = false;
    h.setSummaryImpl(async () => { if (actionSummary) throw new Error("controlled summary failure"); return { serverId: 321, counts: {}, scanState: { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: 321 } }; });
    h.actionLog.length = 0;
    await h.open();
    actionSummary = true;
    await h.enter("322");
    assert.equal(h.snapshot().open, false);
    await h.open();
    assert.deepEqual(h.snapshot().errors, ["Action failed"]);
    currentCases.push({ implementation: label, name: "summary rejection becomes stored action failure after close", status: "MISMATCH", reopened: h.snapshot(), actionLog: [...h.actionLog] });
  });
}

await runOriginalCases();
await runCurrentCases(baselineSource, "baseline-55c9ca1");
await runCurrentCases(currentSource, "current-dispatch");

const report = {
  result: "LWB317_CROSSSERVER_RECOVERY_DIFFERENTIAL_OK",
  reactVersion: React.version,
  jsdomVersion: requireDom("jsdom/package.json").version,
  identities: locators,
  originalCases,
  currentCases,
  conclusion: "The normalized 55c9ca1 baseline and dispatch App are equal. Exact original component/parent bytes demonstrate click-away, profile-scoped import/reset, Enter availability fencing, localized event-time errors, and summary-before-history/close sequencing. Baseline/current mounted App reproduces the corresponding mismatches with inert APIs.",
  limits: "Exact original minified popover and parent callback bytes are executed with React/i18n/API symbol bindings only. Baseline/current App bodies are compiled from their source with actual React hooks; imported page/icon children and native bridge/provider calls are inert. No Last War, server jump, scan, network, updater, or native gameplay/provider action is invoked.",
};
fs.writeFileSync(path.join(here, "differential-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_CROSSSERVER_RECOVERY_DIFFERENTIAL_OK original=${originalCases.length} current=${currentCases.length}`);
