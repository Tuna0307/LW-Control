import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createBackendBridge } from "../../../../src/LWBridge.UI-0.3.17/src/backendBridge.js";
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser"); const { transformSync } = require("esbuild");
const sourcePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const source = fs.readFileSync(path.join(repo, sourcePath), "utf8");
// Normalize implementation line endings for replay after Git's Windows checkout conversion.
// Original asset bytes and byte locators remain unmodified.
const pages = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx"), "utf8").replace(/\r\n/g, "\n");
const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8").replace(/\r\n/g, "\n");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
assert.equal(hash(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
const ast = parse(source, { sourceType: "module" }); const pageAst = parse(pages, { sourceType: "module", plugins: ["jsx"] }); const appAst = parse(app, { sourceType: "module", plugins: ["jsx"] });
const fn = (ast, name) => { const node = ast.program.body.map((node) => node.declaration || node).find((node) => node.type === "FunctionDeclaration" && node.id.name === name); assert.ok(node, name); return node; };
function walk(node, result = []) { if (!node || typeof node !== "object") return result; if (node.type) result.push(node); for (const child of Object.values(node)) { if (Array.isArray(child)) child.forEach((value) => walk(value, result)); else if (child && typeof child === "object") walk(child, result); } return result; }
const parent = fn(ast, "Gi"); const parentNodes = walk(parent);
const originalNodes = ["Ir", "Lr", "Kr", "qr"].map((name) => fn(ast, name));
const originalPickerNodes = ["Xt", "Jt"].map((name) => { const node = parentNodes.find((node) => node.type === "FunctionDeclaration" && node.id.name === name); assert.ok(node); return node; });
const appNodes = walk(fn(appAst, "App"));
const callbackNodes = Object.fromEntries(["updateAutoLaunch", "updateAutoReconnect", "selectGameRoot"].map((name) => {
  const variable = appNodes.find((node) => node.type === "VariableDeclarator" && node.id.name === name); assert.ok(variable); return [name, variable.init.arguments[0]];
}));
const propNode = appNodes.find((node) => node.type === "JSXAttribute" && node.name.name === "homeState").value.expression;
assert.ok(propNode.properties.some((node) => node.key.name === "gameRootError")); assert.ok(propNode.properties.some((node) => node.key.name === "gameActionError"));
for (const name of ["gameRootError", "gameActionError"]) assert.equal(propNode.properties.find((node) => node.key.name === name).value.name, name, "actual App/Home channel wiring");
assert.ok(!propNode.properties.some((node) => node.key.name === "error"));
const callbackResults = [];
function fixture(profileId = "home-channel-profile", available = true) {
  let message; let seq = 0; const requests = [];
  const host = { __LWBridgeBootstrap: { mode: "live", sessionId: "home-channels", profiles: profileId ? { selectedProfileId: profileId } : {} },
    chrome: { webview: { addEventListener: (_, handler) => { message = handler; }, removeEventListener() {}, postMessage: (request) => requests.push(request) } },
    crypto: { randomUUID: () => `channel-${++seq}` }, setTimeout, clearTimeout };
  const realBridge = createBackendBridge(host);
  const bridge = available ? realBridge : { available: false };
  const state = { rootError: "INVALID_GAME_ROOT", actionError: "ACTION_QA_PREVIOUS", busy: "", root: { valid: false }, config: { autoLaunchGame: false, autoReconnect: false } };
  const trace = [];
  const setter = (name) => (value) => { state[name] = typeof value === "function" ? value(state[name]) : value; trace.push({ name, value: state[name] }); };
  const callbacks = Object.fromEntries(Object.entries(callbackNodes).map(([name, node]) => [name, new Function("backendBridge", "setHomeBusy", "setGameRootError", "setGameActionError", "setGameRootStatus", "setLocalConfig", `return (${app.slice(node.start, node.end)});`)(bridge, setter("busy"), setter("rootError"), setter("actionError"), setter("root"), setter("config"))]));
  const respond = (index, result, error) => { const request = requests[index]; assert.ok(request); message({ data: { kind: "response", sessionId: request.sessionId, id: request.id, ok: !error, ...(error ? { error } : { result }) } }); };
  return { state, trace, requests, callbacks, respond, dispose: () => realBridge.dispose() };
}
function record(name, fixture) { callbackResults.push({ name, pass: true, trace: fixture.trace, requests: fixture.requests.map(({ command, payload }) => ({ command, payload })) }); fixture.dispose(); }
for (const [name, expectedCommand, busy] of [["updateAutoLaunch", "local_config_set", "autoLaunchGame"], ["updateAutoReconnect", "set_automation", "autoReconnect"]]) {
  const f = fixture(); const pending = f.callbacks[name](true);
  assert.equal(f.state.busy, busy); assert.equal(f.state.actionError, ""); assert.equal(f.state.rootError, "INVALID_GAME_ROOT"); assert.equal(f.state.config[busy], false);
  assert.equal(f.requests[0].command, expectedCommand);
  if (name === "updateAutoReconnect") assert.equal(f.requests[0].payload.profileId, "home-channel-profile");
  f.respond(0, name === "updateAutoLaunch" ? { autoLaunchGame: true, autoReconnect: false } : { enabled: true }); await pending;
  assert.equal(f.state.config[busy], true); assert.equal(f.state.busy, ""); assert.equal(f.state.rootError, "INVALID_GAME_ROOT");
  const failed = f.callbacks[name](false); assert.equal(f.state.config[busy], true);
  f.respond(1, null, { code: "ACTION_QA_FAILED", message: "Action QA failed" }); await failed;
  assert.equal(f.state.actionError, "Action QA failed"); assert.equal(f.state.rootError, "INVALID_GAME_ROOT"); assert.equal(f.state.config[busy], true); assert.equal(f.state.busy, "");
  record(`${name}: deferred success and failure`, f);
}
{
  const f = fixture(""); await f.callbacks.updateAutoReconnect(true); assert.equal(f.requests.length, 0); assert.ok(f.state.actionError); assert.equal(f.state.rootError, "INVALID_GAME_ROOT"); assert.equal(f.state.config.autoReconnect, false); assert.equal(f.state.busy, ""); record("missing profile before dispatch", f);
}
for (const outcome of ["cancel", "invalid", "valid", "selection-error", "status-error"]) {
  const f = fixture(); f.state.rootError = "ROOT_QA_PREVIOUS"; const pending = f.callbacks.selectGameRoot();
  assert.equal(f.state.busy, "gameRoot"); assert.equal(f.state.rootError, "ROOT_QA_PREVIOUS"); assert.equal(f.state.actionError, "ACTION_QA_PREVIOUS");
  if (outcome === "selection-error") f.respond(0, null, { code: "ROOT_QA_FAILED", message: "Root QA failed" });
  else f.respond(0, { canceled: outcome === "cancel", valid: ["valid", "status-error"].includes(outcome), path: null });
  await Promise.resolve(); await Promise.resolve();
  if (["valid", "status-error"].includes(outcome)) {
    assert.equal(f.requests[1].command, "game_root_status");
    assert.equal(f.state.rootError, "ROOT_QA_PREVIOUS", "clear only after successful root status acknowledgement");
    if (outcome === "status-error") f.respond(1, null, { code: "ROOT_QA_FAILED", message: "Root status QA failed" });
    else f.respond(1, { valid: true, root: "QA fixture root" });
  }
  await pending; assert.equal(f.state.busy, ""); assert.equal(f.state.actionError, "ACTION_QA_PREVIOUS");
  assert.equal(f.state.rootError, outcome === "valid" ? "" : outcome === "selection-error" ? "Root QA failed" : outcome === "status-error" ? "Root status QA failed" : outcome === "invalid" ? "INVALID_GAME_ROOT" : "ROOT_QA_PREVIOUS");
  assert.equal(f.state.root.valid, outcome === "valid");
  assert.equal(f.requests.length, ["valid", "status-error"].includes(outcome) ? 2 : 1);
  record(`picker ${outcome}`, f);
}
{
  const f = fixture("home-channel-profile", false); for (const callback of Object.values(f.callbacks)) await callback(true); assert.deepEqual(f.trace, []); assert.deepEqual(f.requests, []); record("unavailable host no dispatch", f);
}
const sourcePickerCode = originalPickerNodes.map((node) => source.slice(node.start, node.end)).join("\n");
const sourcePickerCases = [];
for (const outcome of ["cancel", "invalid", "failure", "root-update"]) {
  const state = { rootError: "ROOT_QA_PREVIOUS", actionError: "ACTION_QA_PREVIOUS", busy: false, root: null };
  const { Xt, Jt } = new Function("Ut", "v", "x", "g", sourcePickerCode + "\nreturn {Xt,Jt};")(
    async () => { if (outcome === "failure") throw "ROOT_QA_FAILED"; return { canceled: outcome === "cancel", valid: outcome !== "invalid" }; },
    (value) => { state.busy = value; }, (value) => { state.rootError = value; }, (value) => { state.root = value; });
  if (outcome === "root-update") Jt({ valid: true }); else await Xt();
  assert.equal(state.rootError, outcome === "invalid" ? "INVALID_GAME_ROOT" : outcome === "failure" ? "ROOT_QA_FAILED" : outcome === "root-update" ? "" : "ROOT_QA_PREVIOUS");
  assert.equal(state.actionError, "ACTION_QA_PREVIOUS"); assert.equal(state.busy, false);
  sourcePickerCases.push({ outcome, state });
}
const h = (type, props, ...children) => ({ type, props: { ...props, ...(children.length ? { children } : {}) } }); const M = { jsx: h, jsxs: h };
const currentNames = ["translatedError", "previewHomeState", "HomePage"].map((name) => fn(pageAst, name));
const cloneCode = transformSync(currentNames.map((node) => pages.slice(node.start, node.end)).join("\n"), { loader: "jsx", jsxFactory: "h" }).code;
const originalCode = originalNodes.map((node) => source.slice(node.start, node.end)).join("\n");
function nodes(tree, output = []) { if (Array.isArray(tree)) tree.forEach((node) => nodes(node, output)); else if (tree && typeof tree === "object") { output.push(tree); nodes(tree.props.children, output); } return output; }
function text(tree) { if (tree == null || typeof tree === "boolean") return ""; if (Array.isArray(tree)) return tree.map(text).join(""); if (typeof tree === "object") return text(tree.props.children); return String(tree); }
const display = (tree) => ({ root: text(nodes(tree).find((node) => node.props.className === "game-root-missing")), actionAndRecovery: nodes(tree).filter((node) => node.props.className === "game-root-error").map(text) });
const renderResults = [];
for (const language of ["en", "ja", "zh-CN"]) {
  const { default: catalog } = await import(pathToFileURL(path.join(repo, `src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
  const t = (key, values = {}) => (catalog[key] || key).replace(/\{(\w+)\}/g, (match, name) => String(values[name] ?? match));
  const originalHome = new Function("M", "De", "Bn", originalCode + "\nreturn qr;")(M, () => ({ t }), "original-toggle");
  const cloneHome = new Function("h", "useI18n", "ToggleRow", "RECOVERY_ACTIVE_STATES", cloneCode + "\nreturn HomePage;")(h, () => ({ t }), "clone-toggle", new Set(["waiting", "updating", "repairing", "launching", "verifying", "maintenance"]));
  for (const root of [null, { valid: false }, { valid: true }]) for (const rootError of ["", "INVALID_GAME_ROOT"]) for (const actionError of ["", "GAME_XLUA_ABI_UNSUPPORTED"]) for (const recovery of [null, { state: "failed", error: "QA_RECOVERY_UNKNOWN" }]) {
    const expected = display(originalHome({ gameRootStatus: root, gameRootError: rootError, gameActionError: actionError, gameRecoveryStatus: recovery }));
    const actual = display(cloneHome({ homeState: { rootResolved: root !== null, gameRootStatus: root, gameRootError: rootError, gameActionError: actionError, gameRecoveryStatus: recovery } }));
    assert.deepEqual(actual, expected);
    renderResults.push({ language, root: root?.valid ?? null, rootError, actionError, recovery: recovery?.state || "idle", actual });
  }
}
const locator = (text, node) => ({ utf8ByteOffset: Buffer.byteLength(text.slice(0, node.start)), expression: text.slice(node.start, node.end) });
const report = { task: "LWB317-UI-HOME-ERROR-002", result: "LWB317_HOME_ERROR002_OK", source: { path: sourcePath, sha256: hash(source), locators: Object.fromEntries([...originalNodes, ...originalPickerNodes].map((node) => [node.id.name, locator(source, node)])) }, callbacks: Object.fromEntries(Object.entries(callbackNodes).map(([name, node]) => [name, app.slice(node.start, node.end)])), homeStateExpression: app.slice(propNode.start, propNode.end), callbackResults, sourcePickerCases, renderResults, limits: "Actual frontend callbacks + real bridge transport with synthetic local response envelopes. No host/game process or native folder picker launched. Preference error string reduction and polling retained; lifecycle/state reachability unproved." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "channel-results.json"), JSON.stringify(report, null, 2) + "\n");
if (process.argv.includes("--verify-record")) assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "channel-results.json"), "utf8")), report);
console.log(JSON.stringify({ result: report.result, callbackScenarios: callbackResults.length, sourcePickerCases: sourcePickerCases.length, renderComparisons: renderResults.length }, null, 2));
