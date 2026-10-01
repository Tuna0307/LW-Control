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
const source = fs.readFileSync(path.join(repo, sourcePath), "utf8");
const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8").replace(/\r\n/g, "\n");
const sourceHash = crypto.createHash("sha256").update(source).digest("hex").toUpperCase();
assert.equal(sourceHash, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

function walk(node, output = []) {
  if (!node || typeof node !== "object") return output;
  if (node.type) output.push(node);
  for (const child of Object.values(node)) {
    if (Array.isArray(child)) child.forEach((value) => walk(value, output));
    else if (child && typeof child === "object") walk(child, output);
  }
  return output;
}

function utf8Offset(text, charOffset) {
  return Buffer.byteLength(text.slice(0, charOffset));
}

const sourceAst = parse(source, { sourceType: "module" });
const sourceNodes = walk(sourceAst);
const anchors = { qr: 336694, Jt: 367489, Xt: 367703 };
const original = {};
for (const [name, expectedOffset] of Object.entries(anchors)) {
  const node = sourceNodes.find((candidate) =>
    candidate.type === "FunctionDeclaration"
    && candidate.id?.name === name
    && utf8Offset(source, candidate.start) === expectedOffset);
  assert.ok(node, `missing original ${name} at byte ${expectedOffset}`);
  original[name] = {
    utf8ByteOffset: expectedOffset,
    expression: source.slice(node.start, node.end),
  };
}

const initialRootAck = "Ht().then(Jt).catch(e=>x(String(e)));";
const originalPoll = "Promise.all([ot(e).catch(()=>null),Vt(e).catch(()=>null)])";
const initialRootAckIndex = source.indexOf(initialRootAck);
const originalPollIndex = source.indexOf(originalPoll);
assert.ok(initialRootAckIndex >= 0);
assert.ok(originalPollIndex >= 0);

const appAst = parse(app, { sourceType: "module", plugins: ["jsx"] });
const appFn = appAst.program.body.find((node) => node.type === "ExportNamedDeclaration" && node.declaration?.id?.name === "App")?.declaration;
assert.ok(appFn, "App function not found");
const appNodes = walk(appFn);
function callback(name) {
  const variable = appNodes.find((node) => node.type === "VariableDeclarator" && node.id?.name === name);
  assert.ok(variable?.init?.arguments?.[0], `callback not found: ${name}`);
  const node = variable.init.arguments[0];
  return { node, expression: app.slice(node.start, node.end) };
}

const callbackExpressions = Object.fromEntries(
  ["updateAutoLaunch", "updateAutoReconnect", "selectGameRoot", "refreshStatus"]
    .map((name) => [name, callback(name).expression]),
);
assert.ok(app.includes("window.setInterval(refreshStatus, 5000)"), "current five-second refresh loop not found");

function transport(profileId = "review-home-profile") {
  let messageHandler;
  let seq = 0;
  const requests = [];
  const host = {
    __LWBridgeBootstrap: {
      mode: "live",
      sessionId: "review-home-error-002",
      profiles: profileId ? { selectedProfileId: profileId } : {},
    },
    chrome: {
      webview: {
        addEventListener: (_, handler) => { messageHandler = handler; },
        removeEventListener() {},
        postMessage: (request) => requests.push(request),
      },
    },
    crypto: { randomUUID: () => `review-${++seq}` },
    setTimeout,
    clearTimeout,
  };
  const bridge = createBackendBridge(host);
  const respond = (index, result, error) => {
    const request = requests[index];
    assert.ok(request, `missing request at index ${index}`);
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

function callbackFixture(profileId = "review-home-profile") {
  const io = transport(profileId);
  const state = {
    busy: "",
    rootError: "ROOT_PRIOR",
    actionError: "ACTION_PRIOR",
    root: { valid: false, root: "" },
    config: { autoLaunchGame: false, autoReconnect: false },
  };
  const trace = [];
  const setter = (key) => (value) => {
    state[key] = typeof value === "function" ? value(state[key]) : value;
    trace.push({ key, value: state[key] });
  };
  const make = (name) => new Function(
    "backendBridge",
    "setHomeBusy",
    "setGameRootError",
    "setGameActionError",
    "setGameRootStatus",
    "setLocalConfig",
    `return (${callbackExpressions[name]});`,
  )(
    io.bridge,
    setter("busy"),
    setter("rootError"),
    setter("actionError"),
    setter("root"),
    setter("config"),
  );
  return {
    ...io,
    state,
    trace,
    callbacks: {
      updateAutoLaunch: make("updateAutoLaunch"),
      updateAutoReconnect: make("updateAutoReconnect"),
      selectGameRoot: make("selectGameRoot"),
    },
  };
}

const scenarios = [];
function saveScenario(name, fixture) {
  scenarios.push({
    name,
    state: fixture.state,
    trace: fixture.trace,
    requests: fixture.requests.map(({ command, payload }) => ({ command, payload })),
  });
  fixture.dispose();
}

{
  const f = callbackFixture();
  const pending = f.callbacks.selectGameRoot();
  assert.equal(f.state.busy, "gameRoot");
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  f.respond(0, { canceled: true, path: null, valid: false });
  await pending;
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  assert.equal(f.state.busy, "");
  saveScenario("picker cancel preserves both prior channels", f);
}

{
  const f = callbackFixture();
  const pending = f.callbacks.selectGameRoot();
  f.respond(0, { canceled: false, path: "C:\\invalid", valid: false });
  await pending;
  assert.equal(f.state.rootError, "INVALID_GAME_ROOT");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  assert.equal(f.requests.length, 1);
  saveScenario("picker invalid replaces only root error", f);
}

{
  const f = callbackFixture();
  const pending = f.callbacks.selectGameRoot();
  f.respond(0, null, { code: "SELECT_FAILED", message: "selection failed" });
  await pending;
  assert.equal(f.state.rootError, "selection failed");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  saveScenario("picker failure replaces only root error", f);
}

{
  const f = callbackFixture();
  const pending = f.callbacks.selectGameRoot();
  f.respond(0, { canceled: false, path: "C:\\LastWar", valid: true });
  await Promise.resolve();
  await Promise.resolve();
  assert.equal(f.requests[1]?.command, "game_root_status");
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  f.respond(1, { valid: true, root: "C:\\LastWar" });
  await pending;
  assert.equal(f.state.root.valid, true);
  assert.equal(f.state.rootError, "");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  saveScenario("valid picker waits for deferred status acknowledgement before clearing root error", f);
}

{
  const f = callbackFixture();
  const pending = f.callbacks.selectGameRoot();
  f.respond(0, { canceled: false, path: "C:\\LastWar", valid: true });
  await Promise.resolve();
  await Promise.resolve();
  f.respond(1, null, { code: "ROOT_STATUS_FAILED", message: "status failed" });
  await pending;
  assert.equal(f.state.rootError, "status failed");
  assert.equal(f.state.actionError, "ACTION_PRIOR");
  assert.equal(f.state.root.valid, false);
  saveScenario("status failure replaces only root error", f);
}

{
  const f = callbackFixture();
  const pending = f.callbacks.updateAutoReconnect(true);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "");
  assert.equal(f.state.config.autoReconnect, false);
  assert.equal(f.requests[0].command, "set_automation");
  assert.equal(f.requests[0].payload.profileId, "review-home-profile");
  f.respond(0, { enabled: true });
  await pending;
  assert.equal(f.state.config.autoReconnect, true);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  saveScenario("deferred reconnect acknowledgement preserves root error and updates only after ack", f);
}

{
  const f = callbackFixture();
  const pending = f.callbacks.updateAutoLaunch(true);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "");
  assert.equal(f.state.config.autoLaunchGame, false);
  f.respond(0, null, { code: "PREFERENCE_FAILED", message: "preference failed" });
  await pending;
  assert.equal(f.state.config.autoLaunchGame, false);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.actionError, "preference failed");
  saveScenario("preference failure preserves root error and acknowledged config", f);
}

{
  const f = callbackFixture("");
  await f.callbacks.updateAutoReconnect(true);
  assert.equal(f.requests.length, 0);
  assert.equal(f.state.rootError, "ROOT_PRIOR");
  assert.equal(f.state.config.autoReconnect, false);
  assert.equal(
    f.state.actionError,
    "Select a profile before changing profile-scoped automation settings.",
  );
  saveScenario("missing profile rejects before reconnect dispatch", f);
}

const pollTransport = transport();
const pollState = {
  rootError: "ROOT_PRIOR",
  root: { valid: false, root: "" },
  recovery: null,
  config: null,
  runtime: null,
  proxy: null,
  scan: null,
  serverId: 0,
  connectionError: "prior connection error",
};
const pollTrace = [];
const pollSetter = (key) => (value) => {
  pollState[key] = typeof value === "function" ? value(pollState[key]) : value;
  pollTrace.push({ key, value: pollState[key] });
};
const mapApi = {
  readStatus: async () => ({ connected: true }),
  readProxyStatus: async () => ({ gameRunning: false }),
  scanStatus: async () => ({ serverId: 77, isReading: false }),
};
const refreshStatus = new Function(
  "backendBridge",
  "mapApi",
  "setRuntimeStatus",
  "setProxyStatus",
  "setCurrentServerId",
  "setMapRuntime",
  "setGameRootStatus",
  "setGameRecoveryStatus",
  "setLocalConfig",
  "setConnectionError",
  `return (${callbackExpressions.refreshStatus});`,
)(
  pollTransport.bridge,
  mapApi,
  pollSetter("runtime"),
  pollSetter("proxy"),
  pollSetter("serverId"),
  pollSetter("scan"),
  pollSetter("root"),
  pollSetter("recovery"),
  pollSetter("config"),
  pollSetter("connectionError"),
);

const refreshPending = refreshStatus();
assert.deepEqual(
  pollTransport.requests.map(({ command }) => command),
  ["game_root_status", "game_recovery_status", "local_config_get"],
);
pollTransport.respond(0, { valid: true, root: "C:\\LastWar" });
pollTransport.respond(1, { state: "idle" });
pollTransport.respond(2, { autoLaunchGame: false, autoReconnect: false });
await refreshPending;
assert.equal(pollState.root.valid, true);
assert.equal(
  pollState.rootError,
  "ROOT_PRIOR",
  "current refreshStatus leaves a prior root error in state after a successful root-status reply",
);
pollTransport.dispose();

const sourceJtState = { root: { valid: false }, rootError: "ROOT_PRIOR" };
const sourceJt = new Function(
  "g",
  "x",
  `${original.Jt.expression}; return Jt;`,
)(
  (value) => { sourceJtState.root = value; },
  (value) => { sourceJtState.rootError = value; },
);
sourceJt({ valid: true, root: "C:\\LastWar" });
assert.equal(sourceJtState.root.valid, true);
assert.equal(sourceJtState.rootError, "");

const report = {
  task: "LWB317-REVIEW-HOME-ERROR-002",
  date: "2026-10-02",
  recommendation: "CHANGES_REQUIRED",
  source: {
    path: sourcePath,
    sha256: sourceHash,
    anchors: original,
    rootStatusInitialAck: {
      utf8ByteOffset: utf8Offset(source, initialRootAckIndex),
      expression: initialRootAck,
    },
    periodicStatusPoll: {
      utf8ByteOffset: utf8Offset(source, originalPollIndex),
      expression: originalPoll,
      observation: "Original five-second poll refreshes status and proxy status only; game_root_status is fetched separately and routed through Jt.",
    },
  },
  currentCallbacks: callbackExpressions,
  scenarios,
  clearingDifference: {
    currentPollingExpression: "window.setInterval(refreshStatus, 5000)",
    currentRefreshRequests: ["game_root_status", "game_recovery_status", "local_config_get"],
    currentAfterSuccessfulRootStatus: {
      gameRootStatus: pollState.root,
      gameRootError: pollState.rootError,
    },
    originalJtAfterSuccessfulRootStatus: sourceJtState,
    finding: "Current refreshStatus repeatedly asks game_root_status in the five-second loop but handles a successful reply with setGameRootStatus only. Original fetches game_root_status separately through Jt, which sets status and clears the root error, while its five-second loop polls status/proxy only. The retained polling therefore prevents source-correct Jt clearing without also changing Xt cancellation persistence.",
    smallestSuggestedCorrection: "Move game_root_status out of the repeated refreshStatus poll into a source-like one-time root-status acknowledgement path, apply Jt semantics there (set gameRootStatus and clear only gameRootError), and reuse the same acknowledgement behavior for the explicit post-selection status reply. Preserve gameActionError and the existing five-second status/proxy polling.",
    scopeNote: "Do not merely clear gameRootError on every current five-second root-status reply: after Xt-style cancellation, that periodic clear would erase the preserved root error on the next tick even though the original periodic poll never asks for game_root_status.",
  },
};

fs.writeFileSync(path.join(here, "review-results.json"), JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({
  result: "LWB317_REVIEW_HOME_ERROR_002_REPRODUCED",
  recommendation: report.recommendation,
  scenarios: scenarios.length,
  clearingDifference: report.clearingDifference.finding,
}, null, 2));
