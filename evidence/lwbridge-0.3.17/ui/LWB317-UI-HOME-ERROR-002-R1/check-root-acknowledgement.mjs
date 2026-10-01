import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { createBackendBridge } from "../../../../src/LWBridge.UI-0.3.17/src/backendBridge.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");

const sourcePath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const appPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const source = fs.readFileSync(path.join(repo, sourcePath), "utf8");
const app = fs.readFileSync(path.join(repo, appPath), "utf8").replace(/\r\n/g, "\n");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

assert.equal(sha256(source), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

function walk(node, output = []) {
  if (!node || typeof node !== "object") return output;
  if (node.type) output.push(node);
  for (const child of Object.values(node)) {
    if (Array.isArray(child)) child.forEach((value) => walk(value, output));
    else if (child && typeof child === "object") walk(child, output);
  }
  return output;
}

function declaration(ast, name) {
  const node = ast.program.body.map((entry) => entry.declaration || entry)
    .find((entry) => entry.type === "FunctionDeclaration" && entry.id?.name === name);
  assert.ok(node, `missing declaration ${name}`);
  return node;
}

function utf8Offset(text, charOffset) {
  return Buffer.byteLength(text.slice(0, charOffset));
}

function locator(text, node) {
  return {
    utf8ByteOffset: utf8Offset(text, node.start),
    expression: text.slice(node.start, node.end),
  };
}

const sourceAst = parse(source, { sourceType: "module" });
const sourceParent = declaration(sourceAst, "Gi");
const sourceNodes = walk(sourceParent);
const sourceJt = sourceNodes.find((node) =>
  node.type === "FunctionDeclaration"
  && node.id?.name === "Jt"
  && utf8Offset(source, node.start) === 367489);
assert.ok(sourceJt, "missing exact original Jt");

const initialRootAck = "Ht().then(Jt).catch(e=>x(String(e)));";
const originalPoll = "Promise.all([ot(e).catch(()=>null),Vt(e).catch(()=>null)])";
const initialRootAckIndex = source.indexOf(initialRootAck);
const originalPollIndex = source.indexOf(originalPoll);
assert.equal(utf8Offset(source, initialRootAckIndex), 369540);
assert.equal(utf8Offset(source, originalPollIndex), 369979);

const sourceRootEffect = sourceNodes.find((node) =>
  node.type === "CallExpression"
  && node.arguments?.[0]
  && source.slice(node.arguments[0].start, node.arguments[0].end).includes(initialRootAck));
assert.ok(sourceRootEffect, "missing original selected-profile root effect");
const sourceRootEffectBody = source.slice(sourceRootEffect.arguments[0].start, sourceRootEffect.arguments[0].end);
const sourceRootEffectDependencies = source.slice(sourceRootEffect.arguments[1].start, sourceRootEffect.arguments[1].end);
assert.ok(sourceRootEffectBody.includes("if(!r.selectedProfileId)return;"), "original root effect must gate on selected profile");
assert.equal(sourceRootEffectDependencies, "[r.selectedProfileId]");

const appAst = parse(app, { sourceType: "module", plugins: ["jsx"] });
const appFn = declaration(appAst, "App");
const appNodes = walk(appFn);

function callback(name) {
  const variable = appNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === name);
  assert.ok(variable?.init?.arguments?.[0], `missing useCallback ${name}`);
  const node = variable.init.arguments[0];
  return { node, expression: app.slice(node.start, node.end) };
}

const callbacks = Object.fromEntries(
  ["acknowledgeGameRootStatus", "refreshStatus", "updateAutoLaunch", "updateAutoReconnect", "selectGameRoot", "jumpServer"]
    .map((name) => [name, callback(name).expression]),
);

const selectedProfile = appNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === "selectedProfileId");
assert.equal(app.slice(selectedProfile.init.start, selectedProfile.init.end), "backendBridge.profileId");

const currentRootEffect = appNodes.find((node) => {
  if (node.type !== "CallExpression" || node.callee?.name !== "useEffect" || !node.arguments?.[0]) return false;
  return app.slice(node.arguments[0].start, node.arguments[0].end).includes('backendBridge.invoke("game_root_status", {})');
});
assert.ok(currentRootEffect, "missing production root-status effect");
const currentRootEffectBody = app.slice(currentRootEffect.arguments[0].start, currentRootEffect.arguments[0].end);
const currentRootEffectDependencies = app.slice(currentRootEffect.arguments[1].start, currentRootEffect.arguments[1].end);
assert.ok(currentRootEffectBody.includes("!selectedProfileId"), "production root effect must gate on selected profile");
assert.equal(currentRootEffectDependencies, "[acknowledgeGameRootStatus, selectedProfileId]");
assert.ok(!callbacks.refreshStatus.includes("game_root_status"), "periodic refresh must not request game_root_status");
assert.ok(app.includes("window.setInterval(refreshStatus, 5000)"), "five-second refresh timer must remain");
assert.ok(callbacks.jumpServer.includes("await refreshStatus()"), "manual server refresh path must remain wired");
assert.ok(app.includes('backendBridge.listen("bridge://game-recovery"'), "game-recovery listener must remain");
assert.ok(app.includes("mapApi.listenStatus"), "status listener must remain");
assert.ok(app.includes("mapApi.listenScanStatus"), "scan listener must remain");

function transport(profileId = "r1-profile", nativeAvailable = true) {
  let messageHandler;
  let seq = 0;
  const requests = [];
  const webview = nativeAvailable ? {
    addEventListener: (_, handler) => { messageHandler = handler; },
    removeEventListener() {},
    postMessage: (request) => requests.push(request),
  } : undefined;
  const host = {
    __LWBridgeBootstrap: {
      mode: "live",
      sessionId: "home-error-002-r1",
      profiles: profileId ? { selectedProfileId: profileId } : {},
    },
    ...(webview ? { chrome: { webview } } : {}),
    crypto: { randomUUID: () => `r1-${++seq}` },
    setTimeout,
    clearTimeout,
  };
  const bridge = createBackendBridge(host);
  const respond = (index, result, error) => {
    const request = requests[index];
    assert.ok(request, `missing request ${index}`);
    assert.ok(messageHandler, "missing bridge message handler");
    messageHandler({
      data: {
        kind: "response",
        sessionId: request.sessionId,
        id: request.id,
        ok: !error,
        ...(error ? { error } : { result }),
      },
    });
  };
  return { bridge, requests, respond, dispose: () => bridge.dispose() };
}

function stateFixture(profileId = "r1-profile", nativeAvailable = true) {
  const io = transport(profileId, nativeAvailable);
  const state = {
    busy: "",
    rootError: "ROOT_PRIOR",
    actionError: "ACTION_PRIOR",
    root: { valid: false, root: "" },
    config: { autoLaunchGame: false, autoReconnect: false },
    recovery: null,
    runtime: null,
    proxy: null,
    scan: null,
    serverId: 0,
    connectionError: "prior connection error",
  };
  const trace = [];
  const setter = (key) => (value) => {
    state[key] = typeof value === "function" ? value(state[key]) : value;
    trace.push({ key, value: state[key] });
  };

  const acknowledge = new Function(
    "setGameRootStatus",
    "setGameRootError",
    `return (${callbacks.acknowledgeGameRootStatus});`,
  )(setter("root"), setter("rootError"));

  const make = (name) => new Function(
    "backendBridge",
    "setHomeBusy",
    "setGameRootError",
    "setGameActionError",
    "setGameRootStatus",
    "setLocalConfig",
    "acknowledgeGameRootStatus",
    `return (${callbacks[name]});`,
  )(
    io.bridge,
    setter("busy"),
    setter("rootError"),
    setter("actionError"),
    setter("root"),
    setter("config"),
    acknowledge,
  );

  const initialEffect = new Function(
    "backendBridge",
    "selectedProfileId",
    "acknowledgeGameRootStatus",
    "setGameRootError",
    `return (${currentRootEffectBody});`,
  )(io.bridge, io.bridge.profileId, acknowledge, setter("rootError"));

  return {
    ...io,
    state,
    trace,
    acknowledge,
    initialEffect,
    callbacks: {
      updateAutoLaunch: make("updateAutoLaunch"),
      updateAutoReconnect: make("updateAutoReconnect"),
      selectGameRoot: make("selectGameRoot"),
    },
    setter,
  };
}

function refreshCallback(fixture) {
  const mapApi = {
    readStatus: async () => ({ connected: true, pending: 2 }),
    readProxyStatus: async () => ({ gameRunning: false, repairRequired: false }),
    scanStatus: async () => ({ serverId: 77, isReading: false }),
  };
  return new Function(
    "backendBridge",
    "mapApi",
    "setRuntimeStatus",
    "setProxyStatus",
    "setCurrentServerId",
    "setMapRuntime",
    "setGameRecoveryStatus",
    "setLocalConfig",
    "setConnectionError",
    `return (${callbacks.refreshStatus});`,
  )(
    fixture.bridge,
    mapApi,
    fixture.setter("runtime"),
    fixture.setter("proxy"),
    fixture.setter("serverId"),
    fixture.setter("scan"),
    fixture.setter("recovery"),
    fixture.setter("config"),
    fixture.setter("connectionError"),
  );
}

async function flush() {
  await Promise.resolve();
  await Promise.resolve();
}

const scenarios = [];
function saveScenario(name, fixture, requestStart = 0) {
  scenarios.push({
    name,
    state: fixture.state,
    trace: fixture.trace,
    requests: fixture.requests.slice(requestStart).map(({ command, payload }) => ({ command, payload })),
  });
  fixture.dispose();
}

{
  const f = stateFixture();
  f.initialEffect();
  assert.deepEqual(f.requests.map(({ command }) => command), ["game_root_status"]);
  assert.equal(f.state.rootError, "ROOT_PRIOR", "initial root error clears only after acknowledgement");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  assert.equal(f.state.root.valid, false);
  f.respond(0, { valid: true, root: "C:\\LastWar" });
  await flush();
  assert.equal(f.state.root.valid, true);
  assert.equal(f.state.rootError, "");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  const rootTrace = f.trace.filter(({ key }) => key === "root" || key === "rootError");
  assert.deepEqual(rootTrace.map(({ key }) => key), ["root", "rootError"]);
  saveScenario("initial profile root acknowledgement succeeds", f);
}

{
  const f = stateFixture();
  f.initialEffect();
  f.respond(0, null, { code: "ROOT_STATUS_FAILED", message: "initial status failed" });
  await flush();
  assert.equal(f.state.root.valid, false);
  assert.equal(f.state.rootError, "initial status failed");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  saveScenario("initial profile root acknowledgement fails into root channel", f);
}

{
  const f = stateFixture();
  f.initialEffect();
  await flush();
  assert.equal(f.requests.length, 1);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.root.valid, false);
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  f.respond(0, { valid: false, root: "" });
  await flush();
  assert.equal(f.state.root.valid, false);
  assert.equal(f.state.rootError, "");
  saveScenario("initial profile root acknowledgement remains deferred until response", f);
}

{
  const f = stateFixture("");
  f.initialEffect();
  await flush();
  assert.deepEqual(f.requests, []);
  assert.deepEqual(f.trace, []);
  saveScenario("initial root request is gated without selected profile", f);
}

{
  const f = stateFixture("r1-profile", false);
  f.initialEffect();
  for (const callback of Object.values(f.callbacks)) await callback(true);
  assert.deepEqual(f.requests, []);
  assert.deepEqual(f.trace, []);
  saveScenario("native unavailable fences initial and action callbacks", f);
}

{
  const f = stateFixture();
  const refreshStatus = refreshCallback(f);
  const pending = refreshStatus();
  assert.deepEqual(f.requests.map(({ command }) => command), ["game_recovery_status", "local_config_get"]);
  assert.deepEqual(f.requests[0].payload, { profileId: "r1-profile" });
  f.respond(0, { state: "idle" });
  f.respond(1, { autoLaunchGame: false, autoReconnect: false });
  await pending;
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  assert.equal(f.state.serverId, 77);
  assert.equal(f.state.connectionError, "");
  saveScenario("five-second refresh inventory excludes root status", f);
}

{
  const f = stateFixture();
  const selection = f.callbacks.selectGameRoot();
  f.respond(0, { canceled: true, path: null, valid: false });
  await selection;
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  const requestStart = f.requests.length;
  const refreshStatus = refreshCallback(f);
  const poll = refreshStatus();
  assert.deepEqual(f.requests.slice(requestStart).map(({ command }) => command), ["game_recovery_status", "local_config_get"]);
  f.respond(requestStart, { state: "idle" });
  f.respond(requestStart + 1, { autoLaunchGame: false, autoReconnect: false });
  await poll;
  assert.equal(f.state.rootError, "ROOT_PRIOR", "periodic refresh must not erase canceled picker root error");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  saveScenario("picker cancellation survives later periodic refresh", f);
}

for (const outcome of ["invalid", "selection-error", "status-error", "valid"]) {
  const f = stateFixture();
  const pending = f.callbacks.selectGameRoot();
  assert.equal(f.state.busy, "gameRoot");
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  if (outcome === "selection-error") {
    f.respond(0, null, { code: "SELECT_FAILED", message: "selection failed" });
  } else {
    f.respond(0, {
      canceled: false,
      path: outcome === "invalid" ? "C:\\invalid" : "C:\\LastWar",
      valid: outcome !== "invalid",
    });
  }
  await flush();
  if (outcome === "status-error" || outcome === "valid") {
    assert.equal(f.requests[1]?.command, "game_root_status");
    assert.equal(f.state.rootError, "ROOT_PRIOR", "valid selection must wait for root-status acknowledgement");
    if (outcome === "status-error") f.respond(1, null, { code: "ROOT_STATUS_FAILED", message: "status failed" });
    else f.respond(1, { valid: true, root: "C:\\LastWar" });
  }
  await pending;
  assert.equal(f.state.busy, "");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  assert.equal(
    f.state.rootError,
    outcome === "valid" ? "" : outcome === "invalid" ? "INVALID_GAME_ROOT" : outcome === "selection-error" ? "selection failed" : "status failed",
  );
  assert.equal(f.state.root.valid, outcome === "valid");
  saveScenario(`picker ${outcome} preserves action channel and acknowledgement ordering`, f);
}

{
  const f = stateFixture();
  const pending = f.callbacks.updateAutoReconnect(true);
  assert.equal(f.state.busy, "autoReconnect");
  assert.equal(f.state.actionError, "");
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.config.autoReconnect, false);
  assert.equal(f.requests[0].command, "set_automation");
  assert.equal(f.requests[0].payload.profileId, "r1-profile");
  f.respond(0, { enabled: true });
  await pending;
  assert.equal(f.state.config.autoReconnect, true);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.busy, "");
  saveScenario("reconnect remains profile scoped and updates after acknowledgement", f);
}

{
  const f = stateFixture("");
  await f.callbacks.updateAutoReconnect(true);
  assert.equal(f.requests.length, 0);
  assert.equal(f.state.config.autoReconnect, false);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "Select a profile before changing profile-scoped automation settings.");
  assert.equal(f.state.busy, "");
  saveScenario("reconnect still rejects missing profile before dispatch", f);
}

{
  const f = stateFixture();
  const pending = f.callbacks.updateAutoLaunch(true);
  assert.equal(f.state.busy, "autoLaunchGame");
  assert.equal(f.state.actionError, "");
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  f.respond(0, null, { code: "PREFERENCE_FAILED", message: "preference failed" });
  await pending;
  assert.equal(f.state.config.autoLaunchGame, false);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "preference failed");
  assert.equal(f.state.busy, "");
  saveScenario("preference failure remains independent from root channel", f);
}

const originalState = { root: { valid: false }, rootError: "ROOT_PRIOR" };
const originalJtFn = new Function(
  "g",
  "x",
  `${source.slice(sourceJt.start, sourceJt.end)}; return Jt;`,
)(
  (value) => { originalState.root = value; },
  (value) => { originalState.rootError = value; },
);
originalJtFn({ valid: true, root: "C:\\LastWar" });
assert.equal(originalState.root.valid, true);
assert.equal(originalState.rootError, "");

const report = {
  task: "LWB317-UI-HOME-ERROR-002-R1",
  date: "2026-10-02",
  result: "LWB317_HOME_ERROR_002_R1_OK",
  source: {
    path: sourcePath,
    sha256: sha256(source),
    Jt: locator(source, sourceJt),
    initialRootAcknowledgement: {
      utf8ByteOffset: utf8Offset(source, initialRootAckIndex),
      expression: initialRootAck,
    },
    periodicStatusPoll: {
      utf8ByteOffset: utf8Offset(source, originalPollIndex),
      expression: originalPoll,
    },
    selectedProfileEffect: {
      utf8ByteOffset: utf8Offset(source, sourceRootEffect.start),
      callback: sourceRootEffectBody,
      dependencies: sourceRootEffectDependencies,
    },
  },
  production: {
    appPath,
    appSha256NormalizedLF: sha256(app),
    selectedProfileExpression: app.slice(selectedProfile.init.start, selectedProfile.init.end),
    rootEffect: {
      callback: currentRootEffectBody,
      dependencies: currentRootEffectDependencies,
    },
    callbacks,
    timerExpression: "window.setInterval(refreshStatus, 5000)",
    listenersPreserved: ["bridge://game-recovery", "mapApi.listenStatus", "mapApi.listenScanStatus"],
    manualRefreshConsequence: "jumpServer still awaits refreshStatus, but refreshStatus no longer retrieves game_root_status; root retrieval is profile-effect/post-selection only.",
  },
  originalJtAfterSuccess: originalState,
  scenarios,
  limits: "Synthetic local response envelopes through the real frontend bridge and extracted current callbacks/effect only. No native picker, Last War process, persistence, protected original runtime, or original pixel comparison executed.",
};

if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "results.json"), JSON.stringify(report, null, 2) + "\n");
}
if (process.argv.includes("--verify-record")) {
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "results.json"), "utf8")), report);
}

console.log(JSON.stringify({
  result: report.result,
  scenarios: report.scenarios.length,
  appSha256NormalizedLF: report.production.appSha256NormalizedLF,
  timerNativeRequests: report.scenarios.find(({ name }) => name === "five-second refresh inventory excludes root status")?.requests.map(({ command }) => command),
}, null, 2));
