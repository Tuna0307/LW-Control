import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const { parse } = requireUi("@babel/parser");
const { transformSync } = requireUi("esbuild");
const React = requireUi("react");
const { renderToStaticMarkup } = requireUi("react-dom/server");
const dispatch = "1f117a4e61c35798f98c5d62a275eac163a12be6";
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const homePath = "src/LWBridge.UI-0.3.17/src/HomePage.jsx";
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const fromDispatch = (relativePath) => execFileSync("git", ["show", `${dispatch}:${relativePath}`], { cwd: repo, encoding: "utf8" });

const app = fromDispatch(appPath);
const home = fromDispatch(homePath);

function walk(node, list = []) {
  if (!node || typeof node !== "object") return list;
  if (node.type) list.push(node);
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach((child) => walk(child, list));
    else if (value && typeof value === "object") walk(value, list);
  }
  return list;
}

function callback(source, name) {
  const nodes = walk(parse(source, { sourceType: "module", plugins: ["jsx"] }));
  const variable = nodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === name);
  assert.ok(variable?.init?.arguments?.[0], `missing ${name}`);
  const node = variable.init.arguments[0];
  return source.slice(node.start, node.end);
}

function compileHome(source) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  const statements = ast.program.body
    .filter((node) => node.type !== "ImportDeclaration")
    .map((node) => node.type === "ExportNamedDeclaration" ? node.declaration : node)
    .filter(Boolean);
  const code = transformSync(statements.map((node) => source.slice(node.start, node.end)).join("\n"), {
    loader: "jsx", jsxFactory: "h", target: "es2022",
  }).code;
  const h = React.createElement;
  const useI18n = () => ({ t: (key) => key });
  const ToggleRow = ({ label, checked, disabled }) => React.createElement("button", {
    type: "button", role: "switch", "data-label": label, "aria-checked": checked, disabled,
  });
  return new Function("h", "useI18n", "ToggleRow", `${code}\nreturn HomePage;`)(h, useI18n, ToggleRow);
}

const updateSource = callback(app, "updateAutoReconnect");
const state = { busy: "", error: "", config: { autoLaunchGame: true, autoReconnect: false, unrelated: "KEEP" } };
const trace = [];
let resolveSave;
const save = new Promise((resolve) => { resolveSave = resolve; });
const backendBridge = {
  available: true,
  invokeProfileScoped(command, payload) {
    trace.push({ kind: "invokeProfileScoped", command, payload });
    assert.equal(command, "set_automation");
    return save;
  },
};
const setter = (key) => (value) => {
  state[key] = typeof value === "function" ? value(state[key]) : value;
  trace.push({ kind: "state", key, value: state[key] });
};
const update = new Function(
  "backendBridge", "setHomeBusy", "setGameActionError", "setLocalConfig",
  `return (${updateSource});`,
)(backendBridge, setter("busy"), setter("error"), setter("config"));

const pending = update(true);
await Promise.resolve();
assert.equal(state.config.autoReconnect, false, "dispatch must wait for acknowledgement before visible state changes");
assert.equal(state.busy, "autoReconnect");

const HomePage = compileHome(home);
const markup = renderToStaticMarkup(React.createElement(HomePage, {
  homeState: {
    rootResolved: true,
    gameRootStatus: { valid: true, root: "C:/Fixture/Game" },
    proxyStatus: { gameRunning: false, repairRequired: false },
    gameRecoveryStatus: { state: "idle" },
    autoLaunchGame: true,
    autoReconnect: state.config.autoReconnect,
    busy: state.busy,
    production: true,
  },
}));
assert.match(markup, /data-label="automation\.autoReconnect\.title"[^>]*disabled=""/);

resolveSave({ enabled: true });
await pending;
assert.equal(state.config.autoReconnect, true);
assert.equal(state.busy, "");

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B",
  capturedAt: "2026-10-04",
  immutableDispatch: dispatch,
  source: {
    appPath, appSha256: sha256(app), homePath, homeSha256: sha256(home), updateAutoReconnect: updateSource,
  },
  observed: {
    beforeAcknowledgement: { visibleAutoReconnect: false, busy: "autoReconnect", mountedSwitchDisabled: true },
    afterAcknowledgement: { visibleAutoReconnect: true, busy: "" },
    trace,
  },
  parityFailures: [
    "visible Automatic Reconnection value waits for set_automation acknowledgement instead of changing the profile draft immediately",
    "mounted Automatic Reconnection switch is disabled while saving, preventing the source-supported concurrent second edit",
  ],
  classification: "IMMUTABLE FAILING BASELINE",
};

fs.mkdirSync(here, { recursive: true });
fs.writeFileSync(path.join(here, "baseline-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log("LWB317_HOME_RECONNECT_BASELINE_CAPTURED failures=2");
