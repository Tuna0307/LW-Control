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
const { createRoot } = requireUi("react-dom/client");

const appPath = path.join(ui, "src/App.jsx");
const homePath = path.join(ui, "src/HomePage.jsx");
const sharedPath = path.join(ui, "src/sharedPageUI.jsx");
const shellPath = path.join(ui, "src/ShellPresentation.jsx");
const helperPath = path.join(ui, "src/profileConfigDraft.js");
const canonicalSource = (value) => value.replace(/\r\n/g, "\n");
const appSource = canonicalSource(fs.readFileSync(appPath, "utf8"));
const homeSource = canonicalSource(fs.readFileSync(homePath, "utf8"));
const sharedSource = canonicalSource(fs.readFileSync(sharedPath, "utf8"));
const shellSource = canonicalSource(fs.readFileSync(shellPath, "utf8"));
const helperSource = canonicalSource(fs.readFileSync(helperPath, "utf8"));
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const deferred = () => {
  let resolve;
  let reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};

const routesModule = await import(pathToFileURL(path.join(ui, "src/routes.js")).href);
const mapBackendModule = await import(pathToFileURL(path.join(ui, "src/mapBackend.js")).href);
const autoScanModule = await import(pathToFileURL(path.join(ui, "src/mapAutoConfig.js")).href);
const preferenceModule = await import(pathToFileURL(path.join(ui, "src/autoLaunchPreference.js")).href);
const profileConfigModule = await import(pathToFileURL(helperPath).href);

const dom = new JSDOM("<!doctype html><html><body></body></html>", { url: "http://127.0.0.1/?previewPage=overview" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "KeyboardEvent", "Node", "MutationObserver", "localStorage"]) {
  globalThis[name] = name === "window" ? dom.window : dom.window[name];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("network access forbidden by Home reconnect proof"); };
window.matchMedia = () => ({ matches: false });

const intervals = new Map();
const timeouts = new Map();
let timerSerial = 0;
window.setInterval = (fn, delay) => { intervals.set(++timerSerial, { fn, delay }); return timerSerial; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = (fn, delay) => { timeouts.set(++timerSerial, { fn, delay }); return timerSerial; };
window.clearTimeout = (id) => timeouts.delete(id);

function walk(node, list = []) {
  if (!node || typeof node !== "object") return list;
  if (node.type) list.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, list));
    else if (value && typeof value === "object") walk(value, list);
  }
  return list;
}

function locator(source, node) {
  assert.ok(node);
  const text = source.slice(node.start, node.end);
  return {
    utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)),
    byteLength: Buffer.byteLength(text),
    sha256: sha256(text),
    text,
  };
}

const appNodes = walk(parse(appSource, { sourceType: "module", plugins: ["jsx"] }));
const homeNodes = walk(parse(homeSource, { sourceType: "module", plugins: ["jsx"] }));
const updateReconnectNode = appNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === "updateAutoReconnect");
const homeReconnectNode = homeNodes.find((node) => node.type === "JSXElement"
  && node.openingElement?.name?.name === "ToggleRow"
  && homeSource.slice(node.start, node.end).includes('label={t("automation.autoReconnect.title")}'));
assert.ok(updateReconnectNode?.init?.arguments?.[0]);
assert.ok(homeReconnectNode);
const updateReconnectLocator = locator(appSource, updateReconnectNode.init.arguments[0]);
const homeReconnectLocator = locator(homeSource, homeReconnectNode);

function compileComponentModule(source, bindings, returnNames) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(statements.map((node) => source.slice(node.start, node.end)).join("\n"), {
    loader: "jsx", jsxFactory: "h", target: "es2022",
  }).code;
  return new Function(...Object.keys(bindings), `${code}\nreturn { ${returnNames.join(", ")} };`)(...Object.values(bindings));
}

const t = (key, vars = {}) => Object.keys(vars).length ? `${key}:${JSON.stringify(vars)}` : key;
const shared = compileComponentModule(sharedSource, { h: React.createElement, useI18n: () => ({ t }) }, ["Switch", "ToggleRow", "PanelTitle"]);
const { HomePage } = compileComponentModule(homeSource, {
  h: React.createElement,
  useI18n: () => ({ t }),
  ToggleRow: shared.ToggleRow,
}, ["HomePage"]);
const shell = compileComponentModule(shellSource, {
  h: React.createElement,
  useSyncExternalStore: React.useSyncExternalStore,
  useI18n: () => ({ t }),
}, ["ShellConfigSaveErrors"]);

function compileApp(bridge, api, registryCapture) {
  const ast = parse(appSource, { sourceType: "module", plugins: ["jsx"] });
  const bindings = { h: React.createElement };
  function Inert() { return null; }
  function Provider({ children }) { return children; }
  function PageForRoute(props) {
    return props.routeKey === "overview" ? React.createElement(HomePage, props) : React.createElement("div", { "data-route": props.routeKey });
  }
  const profileBindings = {
    ...profileConfigModule,
    createProfileConfigDraftRegistry: () => {
      const registry = profileConfigModule.createProfileConfigDraftRegistry();
      registryCapture.push(registry);
      return registry;
    },
  };
  const modules = new Map([
    ["./routes.js", routesModule],
    ["./mapBackend.js", mapBackendModule],
    ["./mapAutoConfig.js", autoScanModule],
    ["./autoLaunchPreference.js", preferenceModule],
    ["./profileConfigDraft.js", profileBindings],
  ]);
  for (const node of ast.program.body.filter((candidate) => candidate.type === "ImportDeclaration")) {
    const from = node.source.value;
    const module = modules.get(from);
    for (const spec of node.specifiers) {
      const imported = spec.imported?.name || "default";
      const local = spec.local.name;
      if (from === "react") bindings[local] = React[imported];
      else if (from === "react-dom" && local === "flushSync") bindings[local] = (fn) => fn();
      else if (local === "backendBridge") bindings[local] = bridge;
      else if (local === "createMapApi") bindings[local] = () => api;
      else if (local === "connectionState") bindings[local] = () => "disconnected";
      else if (local === "PageForRoute") bindings[local] = PageForRoute;
      else if (local === "preloadRoute") bindings[local] = () => {};
      else if (local === "useI18n") bindings[local] = () => ({ language: "en", setLanguage: () => {}, t });
      else if (local === "LANGUAGES") bindings[local] = [{ code: "en", name: "English" }];
      else if (local === "GameAssetImageProvider") bindings[local] = Provider;
      else if (local === "ShellConfigSaveErrors") bindings[local] = shell.ShellConfigSaveErrors;
      else if (["TopVersion", "ProfileSidebar", "AppExitDialog", "AppExitPrompt", "ProfileSwitchState", "NavIcon"].includes(local)) bindings[local] = Inert;
      else if (local === "toggleShellTheme") bindings[local] = () => {};
      else if (local === "initialShellUpdateStatus") bindings[local] = () => null;
      else if (local === "initialShellProfiles") bindings[local] = () => ({ maxProfiles: 1, profiles: [], selectedProfileId: bridge.profileId });
      else if (local === "previewShellFlagStores") bindings[local] = () => [];
      else if (local === "initialProfileFocus") bindings[local] = () => false;
      else if (local === "saveProfileFocus") bindings[local] = () => {};
      else if (from.endsWith(".png")) bindings[local] = "fixture-dot.png";
      else if (module) bindings[local] = module[imported];
      else throw new Error(`unbound App import ${from} ${imported} as ${local}`);
    }
  }
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(statements.map((node) => appSource.slice(node.start, node.end)).join("\n"), {
    loader: "jsx", jsxFactory: "h", target: "es2022",
  }).code;
  return new Function(...Object.keys(bindings), `${code}\nreturn App;`)(...Object.values(bindings));
}

async function flush(fn) {
  await React.act(async () => {
    if (fn) await fn();
    await Promise.resolve();
    await Promise.resolve();
  });
}

function autoReconnectSwitch(host) {
  return [...host.querySelectorAll('.quick-actions-panel .toggle-row[role="switch"]')]
    .find((node) => node.getAttribute("aria-label")?.startsWith("automation.autoReconnect.title:"));
}
function checked(node) { return node?.getAttribute("aria-checked") === "true"; }
function saveError(host) { return host.querySelector(".automation-error"); }
function errorButton(host, text) { return [...host.querySelectorAll(".automation-error button")].find((node) => node.textContent === text); }

function controlledSession(initialReconnect = false, available = true, rootValid = true) {
  const writes = [];
  const profileReads = [];
  const invocations = [];
  const rootSelections = [];
  let nativeReconnect = initialReconnect;
  let nextProfileRead = null;
  const bridge = {
    mode: available ? "native" : "native-unavailable",
    available,
    profileId: "profile-a",
    listen: () => () => {},
    invokeProfileScoped(command, payload = {}) {
      invocations.push({ kind: "profile", command, payload: { ...payload }, profileId: bridge.profileId });
      if (!available) return Promise.reject(new Error("native unavailable"));
      if (command === "get_status") {
        profileReads.push({ profileId: bridge.profileId, value: nativeReconnect });
        if (nextProfileRead) { const gate = nextProfileRead; nextProfileRead = null; return gate.promise; }
        return Promise.resolve({ config: { auto_force_update_reload: nativeReconnect } });
      }
      if (command === "set_automation") {
        const gate = deferred();
        writes.push({ gate, payload: { ...payload }, profileId: bridge.profileId });
        return gate.promise.then((result) => {
          if (typeof result?.enabled === "boolean") nativeReconnect = result.enabled;
          return result;
        });
      }
      throw new Error(`unexpected profile command ${command}`);
    },
    invoke(command, payload = {}) {
      invocations.push({ kind: "global", command, payload: { ...payload }, profileId: bridge.profileId });
      if (!available) return Promise.reject(new Error("native unavailable"));
      if (command === "game_root_status") return Promise.resolve({ valid: rootValid, root: rootValid ? "C:/Fixture/Game" : "" });
      if (command === "game_root_select") { const gate = deferred(); rootSelections.push(gate); return gate.promise; }
      if (command === "update_status") return Promise.resolve(null);
      if (command === "game_recovery_status") return Promise.resolve({ state: "idle" });
      if (command === "local_config_get") return Promise.resolve({ autoLaunchGame: true, autoReconnect: nativeReconnect });
      if (command === "local_config_set") return Promise.resolve({ autoLaunchGame: payload.autoLaunchGame, autoReconnect: nativeReconnect });
      throw new Error(`unexpected bridge command ${command}`);
    },
  };
  return {
    bridge,
    writes,
    profileReads,
    invocations,
    rootSelections,
    nativeReconnect: () => nativeReconnect,
    setNativeReconnect: (value) => { nativeReconnect = value; },
    deferNextProfileRead: () => { assert.equal(nextProfileRead, null); nextProfileRead = deferred(); return nextProfileRead; },
  };
}

function controlledMapApi(control) {
  const scan = { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: 321, isReading: false };
  let deferredStatusRead = null;
  const statusListeners = new Set();
  const retiredStatusListeners = [];
  return {
    readStatus: async () => {
      if (deferredStatusRead) {
        const gate = deferredStatusRead;
        deferredStatusRead = null;
        return gate.promise;
      }
      return { xluaOnline: false, pending: 0, config: { auto_force_update_reload: control.nativeReconnect() } };
    },
    readProxyStatus: async () => ({ gameRunning: false, repairRequired: false }),
    summary: async () => ({ serverId: 321, counts: {}, scanState: scan }),
    listenStatus: (callback) => {
      statusListeners.add(callback);
      return () => { statusListeners.delete(callback); retiredStatusListeners.push(callback); };
    },
    emitStatus: (value) => { for (const listener of statusListeners) listener(value); },
    emitRetiredStatus: (value) => { for (const listener of retiredStatusListeners) listener(value); },
    listenScanStatus: () => () => {},
    importServerJumpHistory: async (history) => history,
    setServerJumpHistory: async (history) => history,
    jumpServer: async (serverId) => ({ changed: false, previousServerId: serverId, serverId }),
    deferNextStatusRead: () => {
      assert.equal(deferredStatusRead, null, "a status read is already deferred");
      deferredStatusRead = deferred();
      return deferredStatusRead;
    },
  };
}

async function mountSession({ initialReconnect = false, available = true, rootValid = true } = {}) {
  intervals.clear();
  timeouts.clear();
  localStorage.clear();
  const control = controlledSession(initialReconnect, available, rootValid);
  const api = controlledMapApi(control);
  const registries = [];
  const App = compileApp(control.bridge, api, registries);
  const host = document.createElement("div");
  document.body.append(host);
  const root = createRoot(host);
  await flush(() => root.render(React.createElement(App)));
  await flush();
  const registry = registries[0];
  assert.ok(registry, "App did not create its profile config draft registry");
  return {
    ...control,
    api,
    App,
    host,
    root,
    registry,
    reconnectStore: () => registry.find("profile-a", "flag:autoForceUpdateReload"),
    reconnect: () => autoReconnectSwitch(host),
    clickReconnect: () => flush(() => autoReconnectSwitch(host).click()),
    clickRetry: () => flush(() => errorButton(host, "common.retry").click()),
    clickDiscard: () => flush(() => errorButton(host, "configSave.discard").click()),
    replaceProfile: async (profileId) => {
      // Inert fixture replaces bootstrap ownership; the real bridge is immutable.
      control.bridge.profileId = profileId;
      await flush(() => root.render(React.createElement(App)));
      await flush();
    },
    runFiveSecondTimers: async () => {
      const timers = [...intervals.values()].filter(({ delay }) => delay === 5000);
      assert.ok(timers.length >= 1, "five-second status poll timer missing");
      await flush(async () => { for (const timer of timers) await timer.fn(); });
    },
    startFiveSecondTimers: () => {
      const timers = [...intervals.values()].filter(({ delay }) => delay === 5000);
      assert.ok(timers.length >= 1, "five-second status poll timer missing");
      for (const timer of timers) void timer.fn();
    },
    close: async () => {
      await flush(() => root.unmount());
      host.remove();
      intervals.clear();
      timeouts.clear();
    },
  };
}

const cases = [];
const pass = (name, observed = {}) => cases.push({ name, status: "PASS", observed });

{
  const s = await mountSession({ initialReconnect: true });
  try {
    assert.equal(checked(s.reconnect()), true, "initial status must flow through the source read adapter");
    assert.equal(s.reconnect().disabled, false);
    assert.ok(s.profileReads.length >= 1, "incoming status change must trigger the draft adapter read");
    assert.equal(s.profileReads[0].profileId, "profile-a");
    pass("initial default and adapter provenance", {
      sourceDefault: false,
      settledVisible: checked(s.reconnect()),
      adapterReads: s.profileReads,
    });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    assert.equal(checked(s.reconnect()), false);
    await s.clickReconnect();
    assert.equal(checked(s.reconnect()), true, "first edit must be visible before acknowledgement");
    assert.equal(s.reconnect().disabled, false, "saving must not disable reconnect");
    assert.equal(s.writes.length, 1);
    assert.deepEqual(s.writes[0].payload, { name: "autoForceUpdateReload", enabled: true });
    assert.deepEqual(s.reconnectStore().getSnapshot(), { draft: true, confirmed: false, dirty: true, saving: true, error: null });

    await s.clickReconnect();
    assert.equal(checked(s.reconnect()), false, "second edit must remain visible during the first save");
    assert.equal(s.reconnect().disabled, false);
    assert.equal(s.writes.length, 1, "source engine must serialize concurrent generations");
    assert.equal(s.reconnectStore().getSnapshot().draft, false);
    assert.equal(s.reconnectStore().getSnapshot().confirmed, false);

    s.setNativeReconnect(true);
    await s.runFiveSecondTimers();
    assert.equal(checked(s.reconnect()), false, "incoming poll must not replace a dirty/saving draft");
    assert.equal(s.profileReads.length, 0, "dirty/saving receive must not start an adapter refresh");

    await flush(() => s.writes[0].gate.resolve({ enabled: true }));
    assert.equal(s.writes.length, 2, "newer generation must dispatch after the first acknowledgement");
    assert.deepEqual(s.writes[1].payload, { name: "autoForceUpdateReload", enabled: false });
    assert.equal(checked(s.reconnect()), false, "older success must not replace the newer visible draft");
    assert.equal(s.reconnectStore().getSnapshot().confirmed, true);
    assert.equal(s.reconnectStore().getSnapshot().draft, false);

    await flush(() => s.writes[1].gate.resolve({ enabled: false }));
    assert.deepEqual(s.reconnectStore().getSnapshot(), { draft: false, confirmed: false, dirty: false, saving: false, error: null });
    pass("mounted off-on-off uses immediate draft, serialized acknowledgements, and poll fencing", {
      writes: s.writes.map(({ payload, profileId }) => ({ payload, profileId })),
      final: s.reconnectStore().getSnapshot(),
    });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    await s.clickReconnect();
    assert.equal(checked(s.reconnect()), true);
    assert.equal(s.writes.length, 1);

    s.setNativeReconnect(true);
    await s.runFiveSecondTimers();
    assert.equal(checked(s.reconnect()), true);
    assert.equal(s.profileReads.length, 0, "dirty receive must only update incoming identity");

    s.setNativeReconnect(false);
    const stalePoll = s.api.deferNextStatusRead();
    s.startFiveSecondTimers();
    await flush(() => s.writes[0].gate.resolve({ enabled: true }));
    assert.equal(checked(s.reconnect()), true);
    assert.equal(s.nativeReconnect(), true);

    await flush(() => stalePoll.resolve({ xluaOnline: false, pending: 0, config: { auto_force_update_reload: false } }));
    assert.equal(checked(s.reconnect()), true, "late stale status reply regressed the acknowledged draft");
    assert.equal(s.profileReads.at(-1)?.value, true, "late changed incoming identity must reconcile through a fresh profile-owned get_status read");
    pass("late status reply after save acknowledgement reconciles through a fresh profile read", {
      staleIncoming: false,
      nativeAfterAcknowledgement: true,
      finalVisible: checked(s.reconnect()),
      profileReadAfterStaleReply: s.profileReads.at(-1),
    });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    await s.clickReconnect();
    await flush(() => s.writes[0].gate.resolve({ enabled: true }));
    assert.equal(checked(s.reconnect()), true);
    await s.clickReconnect();
    assert.equal(checked(s.reconnect()), false);
    await flush(() => s.writes[1].gate.reject(Object.assign(new Error("latest reconnect write failed"), { code: "LATEST_RECONNECT_FAILED" })));
    const failed = s.reconnectStore().getSnapshot();
    assert.equal(failed.confirmed, true);
    assert.equal(failed.draft, false);
    assert.equal(failed.dirty, true);
    assert.equal(failed.saving, false);
    assert.match(String(failed.error), /latest reconnect write failed/);
    assert.equal(checked(s.reconnect()), false, "failed latest draft remains visible like the source store");
    assert.equal(s.reconnect().disabled, false);
    assert.equal(s.host.querySelectorAll(".automation-error").length, 1, "unsupported flag banners must not be fabricated");
    assert.equal(saveError(s.host).querySelector("strong")?.textContent.trim(), "automation.autoReconnect.title");
    assert.equal(errorButton(s.host, "common.retry").disabled, false);
    assert.equal(errorButton(s.host, "configSave.discard").disabled, false);

    await s.clickRetry();
    assert.equal(s.writes.length, 3);
    assert.equal(saveError(s.host), null, "source clears the error banner while Retry is saving");
    assert.deepEqual(s.writes[2].payload, { name: "autoForceUpdateReload", enabled: false });
    await flush(() => s.writes[2].gate.resolve({ enabled: false }));
    assert.equal(saveError(s.host), null);
    assert.deepEqual(s.reconnectStore().getSnapshot(), { draft: false, confirmed: false, dirty: false, saving: false, error: null });
    pass("latest failure presents source banner and actual Retry recovers dirty draft", { writes: s.writes.length, final: false });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    await s.clickReconnect();
    await s.clickReconnect();
    assert.equal(checked(s.reconnect()), false);
    await flush(() => s.writes[0].gate.reject(Object.assign(new Error("older attempted write failed"), { code: "OLDER_RECONNECT_FAILED" })));
    const rejected = s.reconnectStore().getSnapshot();
    assert.equal(rejected.confirmed, false);
    assert.equal(rejected.draft, false);
    assert.equal(rejected.dirty, false, "reverted newer draft equals the last confirmed value");
    assert.match(String(rejected.error), /older attempted write failed/);
    assert.ok(saveError(s.host), "source keeps the write error even when the newer draft reverted to confirmed");
    await s.clickRetry();
    assert.equal(s.writes.length, 1, "Retry has no write when the current draft is already clean");
    assert.ok(saveError(s.host), "clean Retry does not clear the source error");
    s.setNativeReconnect(false);
    await s.clickDiscard();
    assert.equal(saveError(s.host), null, "Discard must force-read and clear the recovered error");
    assert.equal(s.profileReads.at(-1)?.profileId, "profile-a");
    assert.deepEqual(s.reconnectStore().getSnapshot(), { draft: false, confirmed: false, dirty: false, saving: false, error: null });
    pass("older rejection follows source clean-draft error semantics and actual Discard clears it", { writes: s.writes.length, reads: s.profileReads.length });
  } finally { await s.close(); }
}

{
  const calls = [];
  const missing = profileConfigModule.createAutomationFlagAdapter({ profileId: "", invokeProfileScoped: async (...args) => { calls.push(args); } }, "", "autoForceUpdateReload", "auto_force_update_reload", false);
  await assert.rejects(() => missing.write(true), (error) => error.code === "PROFILE_REQUIRED");
  const mismatch = profileConfigModule.createAutomationFlagAdapter({ profileId: "profile-a", invokeProfileScoped: async (...args) => { calls.push(args); } }, "profile-b", "autoForceUpdateReload", "auto_force_update_reload", false);
  await assert.rejects(() => mismatch.write(true), (error) => error.code === "PROFILE_SCOPE_MISMATCH");
  assert.deepEqual(calls, [], "missing/mismatched ownership must reject before dispatch");

  const registry = profileConfigModule.createProfileConfigDraftRegistry();
  const adapter = (value) => ({ read: async () => value, write: async (next) => next });
  const a = registry.get("profile-a", "flag:autoForceUpdateReload", false, adapter(false));
  a.edit(true, false);
  const b = registry.get("profile-b", "flag:autoForceUpdateReload", true, adapter(true));
  b.edit(false, false);
  assert.equal(registry.get("profile-a", "flag:autoForceUpdateReload", false, adapter(false)), a);
  assert.equal(a.getSnapshot().draft, true);
  assert.equal(b.getSnapshot().draft, false);
  pass("profile ownership rejects missing/mismatch and keyed drafts survive replacement/return", { profileA: true, profileB: false, dispatches: calls.length });
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    s.setNativeReconnect(true);
    await flush(() => s.api.emitStatus({ config: { auto_force_update_reload: true } }));
    assert.equal(checked(s.reconnect()), true, "status events must update the source incoming flag and clean draft");
    assert.equal(s.profileReads.at(-1)?.value, true);
    await s.clickReconnect();
    assert.equal(checked(s.reconnect()), false);
    s.setNativeReconnect(false);
    const readsBefore = s.profileReads.length;
    await flush(() => s.api.emitStatus({ config: { auto_force_update_reload: false } }));
    assert.equal(checked(s.reconnect()), false);
    assert.equal(s.profileReads.length, readsBefore, "event receive must not refresh a saving draft");
    await flush(() => s.writes[0].gate.resolve({ enabled: false }));
    pass("status event producer matches poll receive while clean and while saving", { reads: s.profileReads.length, final: checked(s.reconnect()) });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    await s.clickReconnect();
    const oldPoll = s.api.deferNextStatusRead();
    s.startFiveSecondTimers();
    await s.replaceProfile("profile-b");
    assert.equal(checked(s.reconnect()), false);
    await flush(() => oldPoll.resolve({ config: { auto_force_update_reload: true } }));
    assert.equal(checked(s.reconnect()), false, "obsolete profile A poll fed profile B");
    await flush(() => s.writes[0].gate.resolve({ enabled: true }));
    assert.equal(checked(s.reconnect()), false, "profile A save acknowledgement replaced profile B draft");
    assert.equal(saveError(s.host), null);
    await s.replaceProfile("profile-a");
    assert.equal(checked(s.reconnect()), true, "profile A return lost acknowledged keyed draft");
    const reads = s.profileReads.length;
    await flush(() => s.api.emitRetiredStatus({ config: { auto_force_update_reload: false } }));
    assert.equal(s.profileReads.length, reads, "retired listener caused a new profile read");
    assert.equal(checked(s.reconnect()), true);
    pass("mounted App replacement/return fences obsolete polls, save success and retired listeners", { writes: s.writes.map(({ profileId }) => profileId), finalProfileA: checked(s.reconnect()) });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    await s.clickReconnect();
    await s.replaceProfile("profile-b");
    await flush(() => s.writes[0].gate.reject(new Error("profile A retained save error")));
    assert.equal(checked(s.reconnect()), false);
    assert.equal(saveError(s.host), null, "obsolete profile A rejection exposed its banner in profile B");
    await s.replaceProfile("profile-a");
    assert.equal(checked(s.reconnect()), true);
    assert.ok(saveError(s.host), "returning to profile A lost its source-retained error");
    await s.clickRetry();
    assert.equal(s.writes[1].profileId, "profile-a");
    await flush(() => s.writes[1].gate.resolve({ enabled: true }));
    assert.equal(saveError(s.host), null);
    pass("mounted App obsolete rejection stays in its owner store and Retry uses returning owner", { writes: s.writes.map(({ profileId }) => profileId) });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  try {
    const oldRead = s.deferNextProfileRead();
    s.setNativeReconnect(true);
    await flush(() => s.api.emitStatus({ config: { auto_force_update_reload: true } }));
    assert.equal(s.profileReads.at(-1).profileId, "profile-a");
    s.setNativeReconnect(false);
    await s.replaceProfile("profile-b");
    assert.equal(checked(s.reconnect()), false);
    await flush(() => oldRead.resolve({ config: { auto_force_update_reload: true } }));
    assert.equal(checked(s.reconnect()), false, "profile A read acknowledgement replaced profile B draft");
    s.setNativeReconnect(true);
    await s.replaceProfile("profile-a");
    assert.equal(checked(s.reconnect()), true);
    pass("mounted App deferred owner read cannot replace another profile draft", { finalProfileA: checked(s.reconnect()), profileB: s.registry.find("profile-b", "flag:autoForceUpdateReload").getSnapshot().draft });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ rootValid: false });
  try {
    const picker = () => [...s.host.querySelectorAll('.game-root-missing button')][0];
    await flush(() => picker().click());
    await flush(() => s.rootSelections[0].reject(new Error("ROOT_PICKER_FAILED")));
    const rootText = s.host.querySelector('.game-root-missing span').textContent;
    assert.equal(rootText, "common.actionFailed");
    await s.clickReconnect();
    await flush(() => s.writes[0].gate.reject(new Error("RECONNECT_SAVE_FAILED")));
    assert.ok(saveError(s.host));
    assert.equal(s.host.querySelector('.game-root-missing span').textContent, rootText);
    await s.runFiveSecondTimers();
    assert.equal(s.host.querySelector('.game-root-missing span').textContent, rootText);
    await flush(() => picker().click());
    await flush(() => s.rootSelections[1].resolve({ canceled: true, valid: false }));
    assert.equal(s.host.querySelector('.game-root-missing span').textContent, rootText);
    assert.ok(saveError(s.host), "picker cancellation altered independent reconnect error");
    assert.equal(s.invocations.filter(({ command }) => command === 'game_root_status').length, 1, "polling took root acknowledgement ownership");
    pass("actual root picker error/cancellation and five-second polling preserve independent reconnect banner", { rootText, rootStatusRequests: 1 });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false, available: false });
  try {
    assert.equal(checked(s.reconnect()), false);
    assert.equal(s.reconnect().disabled, true, "unavailable provider must keep Home reconnect fenced");
    pass("unavailable-provider fence", { disabled: s.reconnect().disabled });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ initialReconnect: false });
  const originalConsoleError = console.error;
  const errors = [];
  console.error = (...args) => errors.push(args.map(String).join(" "));
  try {
    await s.clickReconnect();
    assert.equal(s.writes.length, 1);
    await s.close();
    s.writes[0].gate.resolve({ enabled: true });
    await Promise.resolve();
    await Promise.resolve();
    assert.deepEqual(errors, [], "pending reconnect completion after unmount emitted a React error");
    pass("pending save completion after unmount has no component-state callback", { consoleErrors: errors.length });
  } finally {
    console.error = originalConsoleError;
    if (s.host.isConnected) await s.close();
  }
}

assert.match(updateReconnectLocator.text, /autoReconnectStore\.edit\(value, false\)/);
assert.match(updateReconnectLocator.text, /autoReconnectStore\.flush\(\)\.catch\(\(\) => \{\}\)/);
assert.doesNotMatch(updateReconnectLocator.text, /setHomeBusy/);
assert.match(homeReconnectLocator.text, /disabled=\{state\.autoReconnect == null \|\| !state\.production\}/);
assert.doesNotMatch(homeReconnectLocator.text, /busy/);

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B",
  date: "2026-10-04",
  result: "LWB317_HOME_RECONNECT_CURRENT_OK",
  production: {
    appPath: path.relative(repo, appPath).replaceAll("\\", "/"),
    appSha256: sha256(appSource),
    homePath: path.relative(repo, homePath).replaceAll("\\", "/"),
    homeSha256: sha256(homeSource),
    shellPath: path.relative(repo, shellPath).replaceAll("\\", "/"),
    shellSha256: sha256(shellSource),
    helperPath: path.relative(repo, helperPath).replaceAll("\\", "/"),
    helperSha256: sha256(helperSource),
    updateAutoReconnect: updateReconnectLocator,
    homeAutoReconnectToggle: homeReconnectLocator,
  },
  cases,
  ordering: {
    visibleEdit: "profile draft changes immediately before set_automation acknowledgement",
    concurrentEdit: "the same source-derived flush serializes the newer generation after an older successful acknowledgement",
    olderRejection: "a failed attempted write retains its error even if a newer boolean edit returned the draft to the confirmed value; clean Retry is a no-op and Discard clears via forced read",
    latestFailure: "last confirmed value and newer failed draft remain distinct; Retry resends the dirty draft",
    incomingPoll: "receive() fences incoming status while dirty/saving and does not issue its adapter read",
  },
  ownership: {
    storeKey: "[profileId, flag:autoForceUpdateReload]",
    missingProfile: "PROFILE_REQUIRED before dispatch",
    mismatchedProfile: "PROFILE_SCOPE_MISMATCH before dispatch",
    nativeBoundary: "existing bridge profile is immutable bootstrap ownership; native SetAutomation persists AutoReconnect in the clone global local config, not a recovered per-profile native store",
  },
  limits: "Controlled inert bridge and jsdom-mounted actual production App/Home/shared ToggleRow/ShellConfigSaveErrors plus the production source-derived draft engine. No Last War process, gameplay, protected original runtime, new command/provider, or per-profile native persistence was exercised.",
};

fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "current-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, cases: cases.length, appSha256: report.production.appSha256, homeSha256: report.production.homeSha256 }, null, 2));
