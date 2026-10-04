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
const routesModule = await import(pathToFileURL(path.join(ui, "src/routes.js")).href);
const mapBackendModule = await import(pathToFileURL(path.join(ui, "src/mapBackend.js")).href);
const autoModule = await import(pathToFileURL(path.join(ui, "src/mapAutoConfig.js")).href);

const shellStateModule = await import(pathToFileURL(path.join(ui, "src/shellState.js")));
const shellThemeModule = await import(pathToFileURL(path.join(ui, "src/shellTheme.js")));
const ReactDom = requireUi("react-dom");
const sourcePath = path.join(ui, "src/App.jsx");
const source = fs.readFileSync(sourcePath, "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return { promise, resolve, reject }; };
const sourceAst = parse(source, { sourceType: "module", plugins: ["jsx"] });
const walk = (node, list = []) => {
  if (!node || typeof node !== "object") return list;
  if (node.type) list.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, list));
    else if (value && typeof value === "object") walk(value, list);
  }
  return list;
};
const sourceNodes = walk(sourceAst);
const locate = (node) => {
  assert.ok(node);
  const text = source.slice(node.start, node.end);
  return { utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)), utf8ByteLength: Buffer.byteLength(text), sha256: sha256(text) };
};
const sourceLocators = {
  legacyServerHistory: locate(sourceNodes.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "legacyServerHistory")),
  historyImportEffect: locate(sourceNodes.find((node) => node.type === "CallExpression" && node.callee?.name === "useEffect" && source.slice(node.start, node.end).includes("importServerJumpHistory"))),
  outsidePointerEffect: locate(sourceNodes.find((node) => node.type === "CallExpression" && node.callee?.name === "useEffect" && source.slice(node.start, node.end).includes('addEventListener("pointerdown"'))),
  requestServerJump: locate(sourceNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === "requestServerJump")),
  jumpServer: locate(sourceNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === "jumpServer")),
  serverJumpRender: locate(sourceNodes.find((node) => node.type === "JSXElement" && node.openingElement?.attributes?.some((attribute) => attribute.name?.name === "className" && attribute.value?.value === "server-jump"))),
};

const dom = new JSDOM("<!doctype html><html><body><button id='outside'>outside</button></body></html>", { url: "http://127.0.0.1/?previewPage=overview" });
for (const name of ["window", "document", "HTMLElement", "Event", "MouseEvent", "KeyboardEvent", "Node", "MutationObserver", "localStorage"]) {
  globalThis[name] = name === "window" ? dom.window : dom.window[name];
}
Object.defineProperty(globalThis, "navigator", { configurable: true, value: dom.window.navigator });
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.fetch = () => { throw new Error("network access forbidden by focused Cross-server proof"); };
window.matchMedia = () => ({ matches: false });
const intervals = new Map();
const timeouts = new Map();
let timerSerial = 0;
window.setInterval = (fn, delay) => { intervals.set(++timerSerial, { fn, delay }); return timerSerial; };
window.clearInterval = (id) => intervals.delete(id);
window.setTimeout = (fn, delay) => { timeouts.set(++timerSerial, { fn, delay }); return timerSerial; };
window.clearTimeout = (id) => timeouts.delete(id);

let language = "en";
const t = (key, vars = {}) => `${language}:${key}${Object.keys(vars).length ? `:${JSON.stringify(vars)}` : ""}`;

function compileApp(bridge, api, onlineRef) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const bindings = { h: React.createElement };
  function InertPage(props) { return React.createElement("div", { "data-route": props.routeKey }); }
  function InertIcon() { return null; }
  for (const node of ast.program.body.filter((candidate) => candidate.type === "ImportDeclaration")) {
    const from = node.source.value;
    let module = null;
    if (from === "react") module = React;
    else if (from === "react-dom") module = ReactDom;
    else if (from === "./shellState.js") module = shellStateModule;
    else if (from === "./shellTheme.js") module = shellThemeModule;
    else if (["./ShellPresentation.jsx", "./ProfileSidebar.jsx", "./AppExitDialog.jsx", "./ProfileSwitchState.jsx"].includes(from)) module = new Proxy({}, {get: () => InertIcon});
    else if (from === "./GameAssetImage.jsx") module = {GameAssetImageProvider: ({children}) => children};
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
      else if (local === "useI18n") bindings[local] = () => ({ language, setLanguage: () => {}, t });
      else if (local === "LANGUAGES") bindings[local] = [{ code: "en", name: "English" }, { code: "ja", name: "æ—¥æœ¬èªž" }];
      else if (from.endsWith(".png")) bindings[local] = "fixture-dot.png";
      else if (module) bindings[local] = module[imported];
      else throw new Error(`unbound App import ${from} ${imported} as ${local}`);
    }
  }
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(statements.map((node) => source.slice(node.start, node.end)).join("\n"), { loader: "jsx", jsxFactory: "h", target: "es2022" }).code;
  return new Function(...Object.keys(bindings), `${code}\nreturn App;`)(...Object.values(bindings));
}

async function flush(fn) {
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

async function inputValue(host, value) {
  const input = host.querySelector(".server-jump-form input");
  assert.ok(input);
  await flush(() => reactProps(input).onChange({ target: { value } }));
  return host.querySelector(".server-jump-form input");
}

async function pressEnter(host) {
  const input = host.querySelector(".server-jump-form input");
  assert.ok(input);
  await flush(() => reactProps(input).onKeyDown({ key: "Enter" }));
}

function snapshot(host) {
  const popover = host.querySelector(".server-jump-popover");
  const input = host.querySelector(".server-jump-form input");
  const trigger = host.querySelector(".server-jump > button");
  const sections = [...host.querySelectorAll(".server-jump-history")].map((section) => ({
    label: section.querySelector("span")?.textContent || "",
    buttons: [...section.querySelectorAll("button")].map((button) => ({ text: button.textContent, disabled: button.disabled })),
  }));
  return {
    open: Boolean(popover),
    inputValue: input?.value ?? null,
    inputDisabled: input?.disabled ?? null,
    triggerDisabled: trigger?.disabled ?? null,
    errors: [...host.querySelectorAll(".server-jump-error")].map((node) => node.textContent),
    submit: host.querySelector(".server-jump-form button") ? { text: host.querySelector(".server-jump-form button").textContent, disabled: host.querySelector(".server-jump-form button").disabled } : null,
    home: host.querySelector(".server-jump-home")?.textContent || null,
    sections,
  };
}

async function session(name, configure, body) {
  localStorage.clear();
  intervals.clear();
  timeouts.clear();
  language = "en";
  const log = [];
  const onlineRef = { value: true };
  let scan = {
    ...mapBackendModule.DEFAULT_SCAN_STATE,
    serverId: 321,
    homeServerId: 320,
    seasonServerIds: [321, 322],
    truckMatchServerIds: [323, 321],
    isReading: false,
  };
  let summaryImpl = async () => ({ serverId: scan.serverId, counts: {}, scanState: scan });
  let jumpImpl = async (serverId) => ({ changed: serverId !== scan.serverId, previousServerId: scan.serverId, serverId });
  let importImpl = async (history) => history;
  let historyImpl = async (history) => history;
  const bridge = {
    mode: "native", available: true, profileId: "profile-a",
    listen: () => () => {},
    invoke: async (command) => {
      log.push(`bridge:${command}`);
      if (command === "game_root_status") return { valid: true, root: "C:/Fixture/Game" };
      if (command === "game_recovery_status") return { state: "idle" };
      if (command === "local_config_get") return { autoLaunchGame: false, autoReconnect: false };
      throw new Error(`unexpected bridge command ${command}`);
    },
  };
  const api = {
    readStatus: async () => { log.push("readStatus"); return { xluaOnline: onlineRef.value, pending: 0 }; },
    readProxyStatus: async () => { log.push("readProxyStatus"); return { gameRunning: true, repairRequired: false }; },
    summary: async () => { log.push("summary"); return summaryImpl(); },
    listenStatus: () => () => {}, listenScanStatus: () => () => {},
    importServerJumpHistory: async (history) => { log.push(`import:${bridge.profileId}:${history.join(",")}`); return importImpl(history); },
    setServerJumpHistory: async (history) => { log.push(`history:${history.join(",")}`); return historyImpl(history); },
    jumpServer: async (serverId) => { log.push(`jump:${serverId}`); const result = await jumpImpl(serverId); if (result?.changed) scan = { ...scan, serverId: result.serverId }; return result; },
  };
  const control = {
    name, log, bridge, api, onlineRef,
    setScan: (patch) => { scan = { ...scan, ...patch }; },
    setSummary: (fn) => { summaryImpl = fn; },
    setJump: (fn) => { jumpImpl = fn; },
    setImport: (fn) => { importImpl = fn; },
    setHistory: (fn) => { historyImpl = fn; },
  };
  await configure?.(control);
  const App = compileApp(bridge, api, onlineRef);
  const host = document.createElement("div"); document.body.append(host);
  const root = createRoot(host);
  await flush(() => root.render(React.createElement(App)));
  await flush();
  const helper = {
    ...control, host,
    rerender: async () => flush(() => root.render(React.createElement(App))),
    open: async () => flush(() => host.querySelector(".server-jump > button").click()),
    closeToggle: async () => flush(() => host.querySelector(".server-jump > button").click()),
    setInput: (value) => inputValue(host, value),
    enter: () => pressEnter(host),
    snapshot: () => snapshot(host),
  };
  try { return await body(helper); }
  finally {
    await flush(() => root.unmount());
    host.remove();
    intervals.clear();
    timeouts.clear();
  }
}

const cases = [];
const pass = (name, details = {}) => cases.push({ name, status: "PASS", ...details });

await session("lists/history", async (h) => {
  localStorage.setItem("lastwar.serverJumpHistory", JSON.stringify([330, "331", 330, 0, 100000, 332, 333, 334]));
  h.setImport(async (history) => {
    assert.deepEqual(history, [330, 331, 332, 333, 334]);
    return [330, 321];
  });
}, async (h) => {
  assert.equal(localStorage.getItem("lastwar.serverJumpHistory"), null);
  await h.open();
  const observed = h.snapshot();
  assert.equal(observed.inputDisabled, false);
  assert.equal(observed.triggerDisabled, false);
  const season = observed.sections.find((section) => section.label === "en:server.seasonServers");
  const plunder = observed.sections.find((section) => section.label === "en:server.plunderableServers");
  const recent = observed.sections.find((section) => section.label === "en:server.recent");
  assert.deepEqual(season.buttons, [{ text: "321", disabled: true }, { text: "322", disabled: false }]);
  assert.deepEqual(plunder.buttons, [{ text: "323", disabled: false }, { text: "321", disabled: true }]);
  assert.deepEqual(recent.buttons, [{ text: "330", disabled: false }, { text: "321", disabled: false }]);
  pass("legacy normalization and current/home/season/plunderable/recent rendering", { observed });
});

await session("outside/escape", null, async (h) => {
  await h.open();
  assert.equal(h.snapshot().open, true);
  await flush(() => document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true })));
  assert.equal(h.snapshot().open, true);
  await flush(() => document.getElementById("outside").dispatchEvent(new Event("pointerdown", { bubbles: true })));
  assert.equal(h.snapshot().open, false);
  await h.open();
  await h.closeToggle();
  assert.equal(h.snapshot().open, false);
  pass("trigger toggle, no Escape close, outside pointerdown close");
});

await session("validation locale", null, async (h) => {
  await h.open();
  await h.setInput("0");
  await h.enter();
  assert.deepEqual(h.snapshot().errors, ["en:server.invalidId"]);
  language = "ja";
  await h.rerender();
  assert.deepEqual(h.snapshot().errors, ["en:server.invalidId"]);
  assert.equal(h.snapshot().submit.text, "ja:server.switchAction");
  pass("validation uses event-time localization and stored error retains that locale", { observed: h.snapshot() });
});

await session("offline Enter", async (h) => { h.onlineRef.value = false; }, async (h) => {
  await h.open();
  await h.setInput("322");
  h.log.length = 0;
  await h.enter();
  const observed = h.snapshot();
  assert.deepEqual(observed.errors, ["en:status.gameDisconnected"]);
  assert.equal(observed.inputDisabled, false);
  assert.equal(observed.submit.disabled, true);
  assert.deepEqual(h.log, []);
  pass("offline presentation blocks Enter without duplicate stored error", { observed });
});

await session("scan Enter", async (h) => { h.setScan({ isReading: true }); }, async (h) => {
  await h.open();
  await h.setInput("322");
  h.log.length = 0;
  await h.enter();
  const observed = h.snapshot();
  assert.deepEqual(observed.errors, ["en:server.stopScanFirst"]);
  assert.equal(observed.inputDisabled, false);
  assert.equal(observed.submit.disabled, true);
  assert.deepEqual(h.log, []);
  pass("active scan presentation blocks Enter", { observed });
});

await session("profile reset", null, async (h) => {
  await h.open();
  await h.setInput("0");
  await h.enter();
  assert.deepEqual(h.snapshot().errors, ["en:server.invalidId"]);
  localStorage.setItem("lastwar.serverJumpHistory", JSON.stringify([401, 402]));
  h.log.length = 0;
  h.setImport(async () => [410]);
  h.bridge.profileId = "profile-b";
  await h.rerender();
  await flush();
  assert.ok(h.log.includes("import:profile-b:401,402"));
  assert.equal(localStorage.getItem("lastwar.serverJumpHistory"), null);
  const observed = h.snapshot();
  assert.deepEqual(observed.errors, []);
  const recent = observed.sections.find((section) => section.label === "en:server.recent");
  assert.deepEqual(recent?.buttons, [{ text: "410", disabled: false }]);
  pass("profile identity resets error/history and reimports legacy history", { actionLog: [...h.log], observed });
});

for (const changed of [false, true]) {
  await session(`${changed ? "changed" : "unchanged"} deferred summary`, null, async (h) => {
    const gate = deferred();
    h.setJump(async (serverId) => ({ changed, previousServerId: 321, serverId: changed ? 322 : 321 }));
    h.setSummary(() => gate.promise);
    await h.open();
    await h.setInput(changed ? "322" : "321");
    h.log.length = 0;
    const action = h.enter();
    await flush();
    const pending = h.snapshot();
    assert.equal(pending.open, true);
    assert.equal(pending.inputDisabled, false);
    assert.equal(pending.triggerDisabled, false);
    assert.equal(pending.submit.disabled, true);
    assert.deepEqual(h.log, [`jump:${changed ? 322 : 321}`, "summary"]);
    await flush(() => gate.resolve({ serverId: changed ? 322 : 321, counts: {}, scanState: { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: changed ? 322 : 321 } }));
    await action;
    const final = h.snapshot();
    assert.equal(final.open, false);
    if (changed) assert.deepEqual(h.log, ["jump:322", "summary", "history:322"]);
    else assert.deepEqual(h.log, ["jump:321", "summary"]);
    assert.ok(!h.log.some((entry) => entry === "readStatus" || entry === "readProxyStatus" || entry.startsWith("bridge:")));
    pass(`${changed ? "changed" : "unchanged"} waits for summary before close${changed ? "/history" : ""}`, { pending, final, actionLog: [...h.log] });
  });
}

await session("summary failure", null, async (h) => {
  h.setJump(async () => ({ changed: true, previousServerId: 321, serverId: 322 }));
  h.setSummary(async () => { throw new Error("controlled summary failure"); });
  await h.open();
  await h.setInput("322");
  h.log.length = 0;
  await h.enter();
  assert.deepEqual(h.log, ["jump:322", "summary", "history:322"]);
  assert.equal(h.snapshot().open, false);
  await h.open();
  assert.deepEqual(h.snapshot().errors, []);
  pass("summary failure is swallowed before changed history acknowledgement", { actionLog: [...h.log], reopened: h.snapshot() });
});

await session("jump failure", null, async (h) => {
  h.setJump(async () => { throw new Error("controlled jump failure"); });
  await h.open();
  await h.setInput("322");
  h.log.length = 0;
  await h.enter();
  const observed = h.snapshot();
  assert.equal(observed.open, true);
  assert.equal(observed.inputValue, "322");
  assert.deepEqual(observed.errors, ["en:common.actionFailed"]);
  assert.deepEqual(h.log, ["jump:322"]);
  pass("jump failure keeps input/popover and shows localized action failure", { observed, actionLog: [...h.log] });
});

await session("history failure", null, async (h) => {
  h.setJump(async () => ({ changed: true, previousServerId: 321, serverId: 322 }));
  h.setHistory(async () => { throw new Error("controlled history failure"); });
  await h.open();
  await h.setInput("322");
  h.log.length = 0;
  await h.enter();
  const observed = h.snapshot();
  assert.equal(observed.open, true);
  assert.equal(observed.inputValue, "322");
  assert.deepEqual(observed.errors, ["en:common.actionFailed"]);
  assert.deepEqual(h.log, ["jump:322", "summary", "history:322"]);
  pass("history failure occurs after summary and keeps input/popover", { observed, actionLog: [...h.log] });
});

await session("busy trigger", null, async (h) => {
  const gate = deferred();
  h.setJump(async () => ({ changed: false, previousServerId: 321, serverId: 321 }));
  h.setSummary(() => gate.promise);
  await h.open();
  await h.setInput("321");
  const action = h.enter();
  await flush();
  const pending = h.snapshot();
  assert.equal(pending.submit.text, 'en:server.switching:{"server":321}');
  assert.equal(pending.inputDisabled, false);
  assert.equal(pending.triggerDisabled, false);
  await h.closeToggle();
  assert.equal(h.snapshot().open, false);
  await flush(() => gate.resolve({ serverId: 321, counts: {}, scanState: { ...mapBackendModule.DEFAULT_SCAN_STATE, serverId: 321 } }));
  await action;
  pass("busy presentation keeps input/trigger enabled and trigger can close", { pending });
});

const originalLocators = JSON.parse(fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/recovery/source-locators.json"), "utf8"));
const report = {
  result: "LWB317_CROSSSERVER_FOCUSED_OK",
  source: { path: "src/LWBridge.UI-0.3.17/src/App.jsx", sha256: sha256(source), bytes: Buffer.byteLength(source) },
  sourceLocators,
  originalLocators: originalLocators.original,
  reactVersion: React.version,
  jsdomVersion: requireDom("jsdom/package.json").version,
  cases,
  limits: "Actual current App body and React hooks mounted with controlled bridge/map API bindings and inert page/icon children. Input onChange/onKeyDown closures are invoked from the React props attached to rendered production inputs because jsdom 27 + React 19 input-event polyfill is unstable in this host; pointerdown and document Escape use DOM dispatch. No native provider, Last War, server jump, scan, network, update, or gameplay action is invoked.",
};
fs.writeFileSync(path.join(here, "crossserver-focused-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_CROSSSERVER_FOCUSED_OK cases=${cases.length}`);
