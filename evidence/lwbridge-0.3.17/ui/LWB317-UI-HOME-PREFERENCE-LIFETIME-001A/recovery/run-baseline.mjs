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
const dispatch = "52dd38a6c5f003ef6f9caebf681d3fbd1ed824b5";
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

const updateAutoLaunchSource = callback(app, "updateAutoLaunch");
const state = { busy: "", error: "", config: { autoLaunchGame: false, autoReconnect: true, unrelated: "KEEP" } };
const trace = [];
let resolveSave;
const save = new Promise((resolve) => { resolveSave = resolve; });
const backendBridge = {
  available: true,
  invoke(command, payload) {
    trace.push({ kind: "invoke", command, payload });
    assert.equal(command, "local_config_set");
    return save;
  },
};
const setter = (key) => (value) => {
  state[key] = typeof value === "function" ? value(state[key]) : value;
  trace.push({ kind: "state", key, value: state[key] });
};
const updateAutoLaunch = new Function(
  "backendBridge",
  "setHomeBusy",
  "setGameActionError",
  "setLocalConfig",
  `return (${updateAutoLaunchSource});`,
)(backendBridge, setter("busy"), setter("error"), setter("config"));

const pending = updateAutoLaunch(true);
await Promise.resolve();
assert.equal(state.config.autoLaunchGame, false, "dispatch baseline must still wait for acknowledgement before visible config changes");
assert.equal(state.busy, "autoLaunchGame", "dispatch baseline must mark Auto Launch busy while saving");

function compileHome(source) {
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
  const h = React.createElement;
  const useI18n = () => ({ t: (key) => key });
  const ToggleRow = ({ label, checked, disabled }) => React.createElement("button", {
    type: "button",
    role: "switch",
    "data-label": label,
    "aria-checked": checked,
    disabled,
  });
  return new Function("h", "useI18n", "ToggleRow", `${code}\nreturn HomePage;`)(h, useI18n, ToggleRow);
}

const HomePage = compileHome(home);
const markup = renderToStaticMarkup(React.createElement(HomePage, {
  homeState: {
    rootResolved: true,
    gameRootStatus: { valid: true, root: "C:/Fixture/Game" },
    proxyStatus: { gameRunning: false, repairRequired: false },
    gameRecoveryStatus: { state: "idle" },
    autoLaunchGame: state.config.autoLaunchGame,
    autoReconnect: state.config.autoReconnect,
    busy: state.busy,
    production: true,
  },
}));
assert.match(markup, /data-label="auth\.autoLaunchGame"[^>]*disabled=""/);

resolveSave({ autoLaunchGame: true, autoReconnect: true, unrelated: "KEEP" });
await pending;
assert.equal(state.config.autoLaunchGame, true);
assert.equal(state.busy, "");

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001A",
  capturedAt: "2026-10-04",
  immutableDispatch: dispatch,
  source: {
    appPath,
    appSha256: sha256(app),
    homePath,
    homeSha256: sha256(home),
    updateAutoLaunch: updateAutoLaunchSource,
  },
  observed: {
    beforeAcknowledgement: {
      visibleAutoLaunchGame: false,
      busy: "autoLaunchGame",
      mountedSwitchDisabled: true,
    },
    afterAcknowledgement: {
      visibleAutoLaunchGame: true,
      busy: "",
    },
    trace,
  },
  parityFailures: [
    "visible value waits for local_config_set acknowledgement instead of changing immediately",
    "mounted Auto Launch switch is disabled while the save is pending, preventing a second user edit",
  ],
  classification: "IMMUTABLE FAILING BASELINE",
};

fs.writeFileSync(path.join(here, "baseline-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log("LWB317_HOME_PREFERENCE_BASELINE_CAPTURED failures=2");
