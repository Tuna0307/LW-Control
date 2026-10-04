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
const canonicalSource = (value) => value.replace(/\r\n/g, "\n");
const appSource = canonicalSource(fs.readFileSync(appPath, "utf8"));
const homeSource = canonicalSource(fs.readFileSync(homePath, "utf8"));
const sharedSource = canonicalSource(fs.readFileSync(sharedPath, "utf8"));
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

const dom = new JSDOM("<!doctype html><html><body></body></html>", { url: "http://127.0.0.1/?previewPage=overview" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "KeyboardEvent", "Node", "MutationObserver", "localStorage"]) {
  globalThis[name] = name === "window" ? dom.window : dom.window[name];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("network access forbidden by Home preference proof"); };
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
const updateAutoLaunchNode = appNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === "updateAutoLaunch");
const autoLaunchStateNode = appNodes.find((node) => node.type === "VariableDeclarator" && node.id?.type === "ArrayPattern"
  && node.id.elements?.[0]?.name === "autoLaunchGame" && node.id.elements?.[1]?.name === "setAutoLaunchGame");
const homeAutoLaunchNode = homeNodes.find((node) => node.type === "JSXElement"
  && node.openingElement?.name?.name === "ToggleRow"
  && homeSource.slice(node.start, node.end).includes('label={t("auth.autoLaunchGame")}'));
assert.ok(updateAutoLaunchNode?.init?.arguments?.[0]);
assert.ok(autoLaunchStateNode);
assert.ok(homeAutoLaunchNode);
const updateAutoLaunchLocator = locator(appSource, updateAutoLaunchNode.init.arguments[0]);
const autoLaunchStateLocator = locator(appSource, autoLaunchStateNode);
const homeAutoLaunchLocator = locator(homeSource, homeAutoLaunchNode);

function compileComponentModule(source, bindings, returnNames) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(statements.map((node) => source.slice(node.start, node.end)).join("\n"), {
    loader: "jsx",
    jsxFactory: "h",
    target: "es2022",
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

function compileApp(bridge, api) {
  const ast = parse(appSource, { sourceType: "module", plugins: ["jsx"] });
  const bindings = { h: React.createElement };
  function Inert() { return null; }
  function Provider({ children }) { return children; }
  function PageForRoute(props) {
    return props.routeKey === "overview" ? React.createElement(HomePage, props) : React.createElement("div", { "data-route": props.routeKey });
  }
  const modules = new Map([
    ["./routes.js", routesModule],
    ["./mapBackend.js", mapBackendModule],
    ["./mapAutoConfig.js", autoScanModule],
    ["./autoLaunchPreference.js", preferenceModule],
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
      else if (["TopVersion", "ShellConfigSaveErrors", "ProfileSidebar", "AppExitDialog", "AppExitPrompt", "ProfileSwitchState", "NavIcon"].includes(local)) bindings[local] = Inert;
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
    loader: "jsx",
    jsxFactory: "h",
    target: "es2022",
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

function autoLaunchSwitch(host) {
  return [...host.querySelectorAll('.quick-actions-panel .toggle-row[role="switch"]')]
    .find((node) => node.getAttribute("aria-label")?.startsWith("auth.autoLaunchGame:"));
}

function autoReconnectSwitch(host) {
  return [...host.querySelectorAll('.quick-actions-panel .toggle-row[role="switch"]')]
    .find((node) => node.getAttribute("aria-label")?.startsWith("automation.autoReconnect.title:"));
}

function checked(node) { return node?.getAttribute("aria-checked") === "true"; }

function controlledBridge(initialConfig = { autoLaunchGame: true, autoReconnect: false }) {
  const saves = [];
  const invocations = [];
  const configReads = [];
  let config = { ...initialConfig };
  let deferredConfigRead = null;
  const bridge = {
    mode: "native",
    available: true,
    profileId: "profile-a",
    listen: () => () => {},
    invokeProfileScoped: async () => ({ enabled: false }),
    invoke(command, payload = {}) {
      invocations.push({ command, payload, profileId: bridge.profileId });
      if (command === "game_root_status") return Promise.resolve({ valid: true, root: "C:/Fixture/Game" });
      if (command === "update_status") return Promise.resolve(null);
      if (command === "game_recovery_status") return Promise.resolve({ state: "idle" });
      if (command === "local_config_get") {
        if (deferredConfigRead) {
          const gate = deferredConfigRead;
          deferredConfigRead = null;
          configReads.push(gate);
          return gate.promise;
        }
        return Promise.resolve({ ...config });
      }
      if (command === "local_config_set") {
        const gate = deferred();
        saves.push({ gate, payload: { ...payload }, profileId: bridge.profileId });
        return gate.promise;
      }
      throw new Error(`unexpected bridge command ${command}`);
    },
  };
  return {
    bridge,
    saves,
    invocations,
    configReads,
    setConfig: (next) => { config = { ...next }; },
    deferNextConfigRead: () => {
      assert.equal(deferredConfigRead, null, "a config read is already deferred");
      deferredConfigRead = deferred();
      return deferredConfigRead;
    },
  };
}

function controlledMapApi() {
  const scan = { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: 321, isReading: false };
  return {
    readStatus: async () => ({ xluaOnline: false, pending: 0 }),
    readProxyStatus: async () => ({ gameRunning: false, repairRequired: false }),
    summary: async () => ({ serverId: 321, counts: {}, scanState: scan }),
    listenStatus: () => () => {},
    listenScanStatus: () => () => {},
    importServerJumpHistory: async (history) => history,
    setServerJumpHistory: async (history) => history,
    jumpServer: async (serverId) => ({ changed: false, previousServerId: serverId, serverId }),
  };
}

async function mountSession({ storageValue = "false", initialConfig } = {}) {
  intervals.clear();
  timeouts.clear();
  localStorage.clear();
  if (storageValue !== null) localStorage.setItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY, storageValue);
  const control = controlledBridge(initialConfig);
  const api = controlledMapApi();
  const App = compileApp(control.bridge, api);
  const host = document.createElement("div");
  document.body.append(host);
  const root = createRoot(host);
  await flush(() => root.render(React.createElement(App)));
  await flush();
  return {
    ...control,
    api,
    App,
    host,
    root,
    autoLaunch: () => autoLaunchSwitch(host),
    autoReconnect: () => autoReconnectSwitch(host),
    clickAutoLaunch: () => flush(() => autoLaunchSwitch(host).click()),
    clickRefreshStatus: () => flush(() => {
      const button = [...host.querySelectorAll(".top-action.secondary")].find((node) => node.textContent === "top.refreshStatus");
      assert.ok(button, "refresh status button missing");
      button.click();
    }),
    rerender: () => flush(() => root.render(React.createElement(App))),
    runFiveSecondTimers: async () => {
      const timers = [...intervals.values()].filter(({ delay }) => delay === 5000);
      assert.ok(timers.length >= 1, "five-second config/status poll timer missing");
      await flush(async () => { for (const timer of timers) await timer.fn(); });
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
  const memory = new Map();
  const storage = {
    getItem: (key) => memory.has(key) ? memory.get(key) : null,
    setItem: (key, value) => memory.set(key, String(value)),
  };
  assert.equal(preferenceModule.readAutoLaunchGamePreference(storage), true);
  storage.setItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY, "false");
  assert.equal(preferenceModule.readAutoLaunchGamePreference(storage), false);
  storage.setItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY, "true");
  assert.equal(preferenceModule.readAutoLaunchGamePreference(storage), true);
  storage.setItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY, "other");
  assert.equal(preferenceModule.readAutoLaunchGamePreference(storage), true);
  preferenceModule.writeAutoLaunchGamePreference(storage, false);
  assert.equal(memory.get(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "false");
  preferenceModule.writeAutoLaunchGamePreference(storage, true);
  assert.equal(memory.get(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "true");
  pass("exact local preference default, encoding and reload rule", { key: preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY, missing: true, literalFalse: false, literalTrue: true, other: true });
}

{
  const s = await mountSession({ storageValue: "false", initialConfig: { autoLaunchGame: true, autoReconnect: false, unrelated: "POLL-A" } });
  try {
    assert.equal(checked(s.autoLaunch()), false, "local storage must own initial visible Auto Launch value");
    assert.equal(s.autoLaunch().disabled, false, "Auto Launch must be editable when native provider is available");
    assert.equal(checked(s.autoReconnect()), false);

    await s.clickAutoLaunch();
    assert.equal(checked(s.autoLaunch()), true, "first edit must be visible immediately");
    assert.equal(s.autoLaunch().disabled, false, "first pending save must not disable the switch");
    assert.equal(localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "true");
    assert.equal(s.saves.length, 1);
    assert.deepEqual(s.saves[0].payload, { autoLaunchGame: true });

    await s.clickAutoLaunch();
    assert.equal(checked(s.autoLaunch()), false, "second edit must be visible while first save is pending");
    assert.equal(s.autoLaunch().disabled, false);
    assert.equal(localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "false");
    assert.equal(s.saves.length, 1, "native adapter serializes writes, preventing response reordering at transport level");

    s.setConfig({ autoLaunchGame: true, autoReconnect: true, unrelated: "POLL-B" });
    await s.runFiveSecondTimers();
    assert.equal(checked(s.autoLaunch()), false, "five-second native config poll must not replace the local preference draft");
    assert.equal(checked(s.autoReconnect()), true, "unrelated native config must still refresh normally");

    s.bridge.profileId = "profile-b";
    await s.rerender();
    await flush();
    assert.equal(checked(s.autoLaunch()), false, "profile replacement must preserve the global local preference draft");
    assert.equal(checked(s.autoReconnect()), true);

    s.saves[0].gate.resolve({ autoLaunchGame: true, autoReconnect: false });
    await flush();
    assert.equal(checked(s.autoLaunch()), false, "late first acknowledgement must not overwrite the newer edit");
    assert.equal(checked(s.autoReconnect()), true, "Auto Launch acknowledgement must not replace unrelated polled config");
    assert.equal(s.saves.length, 2, "second native write starts only after the first settles");
    assert.deepEqual(s.saves[1].payload, { autoLaunchGame: false });
    s.saves[1].gate.resolve({ autoLaunchGame: false, autoReconnect: false });
    await flush();
    assert.equal(checked(s.autoLaunch()), false);
    assert.equal(checked(s.autoReconnect()), true);
    assert.equal(localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "false");
    pass("mounted off-on-off survives deferred first save, poll, profile replacement and stale acknowledgement", {
      saves: s.saves.map(({ payload, profileId }) => ({ payload, profileId })),
      finalAutoLaunch: checked(s.autoLaunch()),
      finalAutoReconnect: checked(s.autoReconnect()),
      stored: localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY),
    });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ storageValue: "false", initialConfig: { autoLaunchGame: false, autoReconnect: false } });
  try {
    await s.clickAutoLaunch();
    await s.clickAutoLaunch();
    assert.equal(checked(s.autoLaunch()), false);
    s.saves[0].gate.reject(Object.assign(new Error("first failed"), { code: "FIRST_FAILED" }));
    await flush();
    assert.equal(checked(s.autoLaunch()), false, "stale first rejection must not roll back the newer edit");
    assert.equal(s.host.querySelector(".game-root-error"), null, "stale first rejection must not surface as the latest edit's error");
    assert.equal(s.saves.length, 2);
    s.saves[1].gate.resolve({ autoLaunchGame: false, autoReconnect: false });
    await flush();
    assert.equal(checked(s.autoLaunch()), false);
    pass("stale rejection is fenced and newer queued save succeeds", { final: false, visibleError: false });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ storageValue: "false", initialConfig: { autoLaunchGame: false, autoReconnect: true, unrelated: "KEEP" } });
  try {
    await s.runFiveSecondTimers();
    assert.equal(checked(s.autoReconnect()), true);
    await s.clickAutoLaunch();
    await s.clickAutoLaunch();
    s.saves[0].gate.resolve({ autoLaunchGame: true, autoReconnect: false });
    await flush();
    assert.equal(checked(s.autoLaunch()), false);
    assert.equal(checked(s.autoReconnect()), true);
    s.saves[1].gate.reject(Object.assign(new Error("latest persistence failed"), { code: "LATEST_FAILED" }));
    await flush();
    assert.equal(checked(s.autoLaunch()), true, "latest rejection must reconcile to last native-confirmed value");
    assert.equal(localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "true");
    assert.equal(checked(s.autoReconnect()), true, "rollback must preserve unrelated config");
    assert.ok(s.host.querySelector(".game-root-error"), "latest persistence failure must remain reported");
    pass("latest rejection rolls back to last confirmed value and reports failure", {
      finalAutoLaunch: true,
      finalAutoReconnect: true,
      stored: localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY),
      errorText: s.host.querySelector(".game-root-error")?.textContent || "",
    });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ storageValue: "false", initialConfig: { autoLaunchGame: true, autoReconnect: false, unrelated: "NATIVE-CONFIRMED" } });
  try {
    assert.equal(checked(s.autoLaunch()), false, "native read must not replace the source-local visible preference");
    await s.clickAutoLaunch();
    assert.equal(checked(s.autoLaunch()), true);
    s.saves[0].gate.reject(Object.assign(new Error("native mirror rejected"), { code: "MIRROR_REJECTED" }));
    await flush();
    assert.equal(checked(s.autoLaunch()), true, "failed mirror must reconcile to the last native-confirmed value");
    assert.equal(localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "true");
    assert.ok(s.host.querySelector(".game-root-error"));
    pass("poll establishes native rollback reference without owning the visible local value", {
      beforeEdit: false,
      rollbackNativeConfirmed: true,
      stored: localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY),
    });
  } finally { await s.close(); }
}

{
  const s = await mountSession({ storageValue: "false", initialConfig: { autoLaunchGame: false, autoReconnect: false } });
  try {
    await s.clickAutoLaunch();
    assert.equal(s.saves.length, 1);
    const stalePoll = s.deferNextConfigRead();
    await s.clickRefreshStatus();
    assert.equal(s.configReads.length, 1, "manual refresh must start the deferred config read");
    s.saves[0].gate.resolve({ autoLaunchGame: true, autoReconnect: false });
    await flush();
    assert.equal(checked(s.autoLaunch()), true);
    stalePoll.resolve({ autoLaunchGame: false, autoReconnect: false, unrelated: "STALE-POLL" });
    await flush();
    await s.clickAutoLaunch();
    assert.equal(checked(s.autoLaunch()), false);
    assert.equal(s.saves.length, 2);
    s.saves[1].gate.reject(Object.assign(new Error("second save rejected"), { code: "SECOND_REJECTED" }));
    await flush();
    assert.equal(checked(s.autoLaunch()), true, "late stale poll must not replace the native-confirmed rollback reference");
    assert.equal(localStorage.getItem(preferenceModule.AUTO_LAUNCH_GAME_STORAGE_KEY), "true");
    pass("late config poll cannot regress rollback reference after native save acknowledgement", {
      nativeConfirmedBeforeStalePoll: true,
      stalePollValue: false,
      rollbackAfterLaterFailure: true,
    });
  } finally { await s.close(); }
}

{
  localStorage.clear();
  const first = await mountSession({ storageValue: null, initialConfig: { autoLaunchGame: false, autoReconnect: false } });
  try {
    assert.equal(checked(first.autoLaunch()), true, "missing local key must mount checked even if native poll says false");
    preferenceModule.writeAutoLaunchGamePreference(localStorage, false);
  } finally { await first.close(); }
  const second = await mountSession({ storageValue: "false", initialConfig: { autoLaunchGame: true, autoReconnect: false } });
  try {
    assert.equal(checked(second.autoLaunch()), false, "literal false must survive remount/reload");
    pass("mounted default and reload follow exact local storage rule", { missingMount: true, falseReloadMount: false });
  } finally { await second.close(); }
}

assert.ok(!updateAutoLaunchLocator.text.includes("setHomeBusy"), "Auto Launch callback must not create save-busy UI state");
assert.ok(!updateAutoLaunchLocator.text.includes("setLocalConfig"), "Auto Launch acknowledgement must not replace unrelated native config state");
assert.match(homeAutoLaunchLocator.text, /disabled=\{state\.autoLaunchGame == null \|\| !state\.production\}/);
assert.doesNotMatch(homeAutoLaunchLocator.text, /busy/);

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001A",
  date: "2026-10-04",
  result: "LWB317_HOME_PREFERENCE_CURRENT_OK",
  production: {
    appPath: path.relative(repo, appPath).replaceAll("\\", "/"),
    appSha256: sha256(appSource),
    homePath: path.relative(repo, homePath).replaceAll("\\", "/"),
    homeSha256: sha256(homeSource),
    helperPath: "src/LWBridge.UI-0.3.17/src/autoLaunchPreference.js",
    helperSha256: sha256(canonicalSource(fs.readFileSync(path.join(ui, "src/autoLaunchPreference.js"), "utf8"))),
    updateAutoLaunch: updateAutoLaunchLocator,
    autoLaunchState: autoLaunchStateLocator,
    homeAutoLaunchToggle: homeAutoLaunchLocator,
  },
  cases,
  ordering: {
    localEdits: "immediate and independently editable",
    nativeMirror: "serialized through the existing local_config_set command; a later local revision fences a late/stale earlier acknowledgement or rejection",
    outOfOrderNativeAcknowledgements: "structurally prevented because request N+1 is not dispatched until request N settles",
    latestFailure: "reconciles local storage and visible state to the last native-confirmed value and reports the failure",
  },
  preserved: {
    autoReconnect: "unchanged acknowledged/profile-scoped callback and Home disabled predicate",
    unrelatedConfig: "Auto Launch callback never calls setLocalConfig; polled Auto Reconnect survives Auto Launch acknowledgements and rollback",
    fiveSecondPoll: "still executes; it refreshes native config without replacing the source-local Auto Launch value",
    profileReplacement: "global local Auto Launch draft persists while selected profile effects refresh independently",
  },
  limits: "Controlled inert frontend bridge and jsdom-mounted actual App/Home/shared switch. No Last War process, gameplay, protected original runtime, or new provider/command was exercised.",
};

fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "current-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, cases: cases.length, appSha256: report.production.appSha256, homeSha256: report.production.homeSha256 }, null, 2));
