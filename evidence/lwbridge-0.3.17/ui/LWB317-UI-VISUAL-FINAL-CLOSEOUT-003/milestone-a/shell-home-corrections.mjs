import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { unwrapProfileEvent } from "../../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
const outputPath = path.join(here, "shell-home-corrections-results.json");
const verifyOnly = process.argv.includes("--verify");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const read = (name) => fs.readFileSync(path.join(repo, name), "utf8");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const parser = require("@babel/parser");
const currentPath = "src/LWBridge.UI-0.3.17/src/App.jsx";
const originalPath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
const current = read(currentPath);
const original = read(originalPath);

function nodes(source, jsx = false) {
  const result = [];
  function walk(node) {
    if (!node || typeof node !== "object") return;
    if (node.type) result.push(node);
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(walk);
      else if (value && typeof value === "object") walk(value);
    }
  }
  walk(parser.parse(source, { sourceType: "module", plugins: jsx ? ["jsx"] : [] }));
  return result;
}

function evaluate(source, node, environment, prefix = "") {
  return new Function(
    ...Object.keys(environment),
    `${prefix}return (${source.slice(node.start, node.end)});`,
  )(...Object.values(environment));
}

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((onResolve, onReject) => { resolve = onResolve; reject = onReject; });
  return { promise, resolve, reject };
}

async function settle() {
  await Promise.resolve();
  await new Promise((resolve) => setTimeout(resolve, 0));
}

const cn = nodes(current, true);
const on = nodes(original);

const recoveryEffect = cn.find((node) => node.type === "CallExpression"
  && node.callee.name === "useEffect"
  && current.slice(node.start, node.end).includes("game_recovery_status"))?.arguments[0];
assert.ok(recoveryEffect, "current recovery effect missing");
let recoveryListener;
let recoveryUnsubscribed = 0;
const recoveryAccepted = [];
const selectedProfileIdRef = { current: "A" };
const recoveryCleanup = evaluate(current, recoveryEffect, {
  backendBridge: {
    available: true,
    listen: (_name, callback) => { recoveryListener = callback; return () => { recoveryUnsubscribed += 1; }; },
    invoke: () => new Promise(() => {}),
  },
  selectedProfileId: "A",
  selectedProfileIdRef,
  setGameRecoveryStatus: (value) => recoveryAccepted.push(value),
  unwrapProfileEvent,
})();
recoveryListener({ profileId: "B", payload: { state: "failed", error: "B_ERROR" } });
assert.equal(recoveryAccepted.length, 0, "other-profile recovery event must be rejected");
recoveryListener({ profileId: "A", payload: { state: "ready", owner: "A" } });
recoveryListener({ state: "raw", owner: "A" });
assert.deepEqual(recoveryAccepted, [{ state: "ready", owner: "A" }, { state: "raw", owner: "A" }]);
selectedProfileIdRef.current = "B";
recoveryListener({ profileId: "A", payload: { state: "late" } });
assert.equal(recoveryAccepted.length, 2, "replaced profile must reject late recovery acknowledgement");
selectedProfileIdRef.current = "A";
recoveryCleanup();
recoveryListener({ profileId: "A", payload: { state: "closed" } });
assert.equal(recoveryAccepted.length, 2, "closed recovery listener must not acknowledge");
assert.equal(recoveryUnsubscribed, 1, "recovery cleanup must unsubscribe exactly once");

const originalPe = on.find((node) => node.type === "FunctionDeclaration" && node.id.name === "Pe");
assert.ok(originalPe, "recovered Pe missing");
let originalRecoveryListener;
const originalRecoveryAccepted = [];
evaluate(original, originalPe, {
  Ne: () => true,
  A: () => "A",
  Me: (_name, callback) => { originalRecoveryListener = callback; return Promise.resolve(() => {}); },
})("bridge://game-recovery", (value) => originalRecoveryAccepted.push(value));
originalRecoveryListener({ payload: { profileId: "B", payload: { state: "failed" } } });
assert.equal(originalRecoveryAccepted.length, 0, "recovered Pe must reject other-profile recovery envelope");

const readStatusNode = cn.find((node) => node.type === "VariableDeclarator" && node.id.name === "readStatusSnapshot")?.init?.arguments?.[0];
assert.ok(readStatusNode, "current readStatusSnapshot callback missing");
const periodicEffect = cn.find((node) => node.type === "CallExpression"
  && node.callee.name === "useEffect"
  && current.slice(node.start, node.end).includes("const pollStatus = async () =>"))?.arguments[0];
assert.ok(periodicEffect, "current periodic status effect missing");

function statusHarness({ profileId = "A" } = {}) {
  const proxyRequests = [];
  const runtimeWrites = [];
  const proxyWrites = [];
  const connectionWrites = [];
  const refs = {
    reconnectStatusGeneration: { current: 0 },
    autoLaunchSaveRevisionRef: { current: 0 },
    autoLaunchNativeCommitEpochRef: { current: 0 },
    autoLaunchConfigPollGenerationRef: { current: 0 },
    autoLaunchCommittedRef: { current: false },
  };
  const backendBridge = { available: true, invoke: () => Promise.resolve({ autoLaunchGame: true }) };
  const mapApi = {
    readStatus: () => Promise.resolve({ xluaOnline: true }),
    readProxyStatus: () => { const request = deferred(); proxyRequests.push(request); return request.promise; },
    listenStatus: () => () => {},
    listenScanStatus: () => () => {},
  };
  const acknowledgeRuntimeStatus = (value) => runtimeWrites.push(value);
  const readStatusSnapshot = evaluate(current, readStatusNode, {
    backendBridge,
    ...refs,
    mapApi,
    acknowledgeRuntimeStatus,
    setProxyStatus: (value) => proxyWrites.push(value),
    setConnectionError: (value) => connectionWrites.push(value),
  });
  const selectedProfileIdRef = { current: profileId };
  let intervalCallback;
  let cleared = 0;
  const effect = evaluate(current, periodicEffect, {
    backendBridge,
    selectedProfileId: profileId,
    selectedProfileIdRef,
    mapApi,
    acknowledgeRuntimeStatus,
    acknowledgeMapScan() {},
    readStatusSnapshot,
    window: {
      setInterval: (callback) => { intervalCallback = callback; return 41; },
      clearInterval: (id) => { assert.equal(id, 41); cleared += 1; },
    },
  });
  return { proxyRequests, runtimeWrites, proxyWrites, connectionWrites, selectedProfileIdRef, getInterval: () => intervalCallback, getCleared: () => cleared, effect };
}

const stale = statusHarness();
const staleCleanup = stale.effect();
assert.equal(stale.proxyRequests.length, 1, "initial periodic status read missing");
stale.getInterval()();
assert.equal(stale.proxyRequests.length, 1, "overlapping periodic status read must be suppressed");
stale.selectedProfileIdRef.current = "B";
stale.proxyRequests[0].resolve({ gameRunning: true });
await settle();
assert.deepEqual(stale.runtimeWrites, [], "old-profile periodic runtime status must not acknowledge");
assert.deepEqual(stale.proxyWrites, [], "old-profile periodic proxy status must not acknowledge");
assert.deepEqual(stale.connectionWrites, [], "old-profile periodic connection state must not acknowledge");
staleCleanup();
assert.equal(stale.getCleared(), 1, "periodic cleanup must clear its timer");

const closed = statusHarness();
const closedCleanup = closed.effect();
assert.equal(closed.proxyRequests.length, 1);
closedCleanup();
closed.proxyRequests[0].resolve({ gameRunning: false });
await settle();
assert.deepEqual(closed.runtimeWrites, [], "closed periodic runtime status must not acknowledge");
assert.deepEqual(closed.proxyWrites, [], "closed periodic proxy status must not acknowledge");
assert.deepEqual(closed.connectionWrites, [], "closed periodic connection state must not acknowledge");

const manualRefreshNode = cn.find((node) => node.type === "VariableDeclarator" && node.id.name === "refreshStatus")?.init?.arguments?.[0];
assert.ok(manualRefreshNode, "manual refresh callback missing");
let manualReads = 0;
const manualRefresh = evaluate(current, manualRefreshNode, { readStatusSnapshot: () => { manualReads += 1; return Promise.resolve(); } });
await Promise.all([manualRefresh(), manualRefresh()]);
assert.equal(manualReads, 2, "explicit Refresh Status must remain separate from the periodic in-flight fence");

const originalPoll = on.find((node) => node.type === "VariableDeclarator"
  && node.id.name === "s"
  && node.init
  && original.slice(node.init.start, node.init.end).includes("Promise.all([ot(e).catch"));
assert.ok(originalPoll, "recovered periodic status callback missing");
let originalProxyRequests = 0;
const originalPending = deferred();
const originalPeriodic = evaluate(original, originalPoll.init, {
  ot: () => Promise.resolve({}),
  Vt: () => { originalProxyRequests += 1; return originalPending.promise; },
  A: () => "A",
  Ot() {},
  f() {},
}, "let o=false,t=false,e='A';");
const originalFirst = originalPeriodic();
await originalPeriodic();
assert.equal(originalProxyRequests, 1, "recovered recurring poll must suppress overlap");
originalPending.resolve({ gameRunning: true });
await originalFirst;

const callbacks = cn.find((node) => node.type === "VariableDeclarator" && node.id.name === "profilePreviewCallbacks");
const selectNode = callbacks?.init?.consequent?.properties?.find((node) => node.key.name === "onSelect")?.value;
assert.ok(selectNode, "profile preview selection callback missing");
let selected = "A";
let loading = false;
const loadingWrites = [];
const frames = [];
const cache = new Set(["A"]);
const previewProfileLoadGeneration = { current: 0 };
const select = evaluate(current, selectNode, {
  shellProfiles: { selectedProfileId: "A" },
  previewProfileCache: { current: cache },
  previewProfileLoadGeneration,
  setPreviewProfileLoading: (value) => { loading = value; loadingWrites.push(value); },
  setShellProfiles: (change) => { selected = change({ selectedProfileId: selected }).selectedProfileId; },
  window: { requestAnimationFrame: (callback) => { frames.push(callback); } },
});
const loadB = select("B");
frames.shift()();
const loadC = select("C");
frames.shift()();
await loadB;
assert.equal(selected, "C");
assert.equal(loading, true, "abandoned B completion must not clear C loading");
assert.deepEqual([...cache], ["A", "B"]);
assert.equal(frames.length, 1, "C must retain its pending bootstrap after B completes");
frames.shift()();
frames.shift()();
await loadC;
assert.equal(loading, false, "C completion must clear its own loading state");
assert.deepEqual([...cache], ["A", "B", "C"]);

let cachedSelected = "C";
let cachedLoading = true;
const cachedFrames = [];
const cachedSelect = evaluate(current, selectNode, {
  shellProfiles: { selectedProfileId: "C" },
  previewProfileCache: { current: cache },
  previewProfileLoadGeneration,
  setPreviewProfileLoading: (value) => { cachedLoading = value; },
  setShellProfiles: (change) => { cachedSelected = change({ selectedProfileId: cachedSelected }).selectedProfileId; },
  window: { requestAnimationFrame: (callback) => { cachedFrames.push(callback); } },
});
await cachedSelect("A");
assert.equal(cachedSelected, "A", "cached profile return must still select immediately");
assert.equal(cachedLoading, false, "cached profile return must not remain loading");
assert.equal(cachedFrames.length, 0, "cached profile return must not bootstrap again");

const dependencies = [
  originalPath,
  currentPath,
  "src/LWBridge.UI-0.3.17/src/mapBackend.js",
  "src/LWBridge.UI-0.3.17/scripts/check-home-integration.mjs",
].map((name) => ({ path: name, sha256: sha(fs.readFileSync(path.join(repo, name))) }));
const result = {
  marker: "LWB317_FINAL_CLOSEOUT_SHELL_HOME_CORRECTIONS_OK",
  recovery: {
    currentAccepted: recoveryAccepted,
    otherProfileRejected: true,
    replacementRejected: true,
    cleanupRejected: true,
    unsubscribed: recoveryUnsubscribed,
    originalOtherProfileRejected: originalRecoveryAccepted.length === 0,
  },
  recurringStatus: {
    currentRequestsDuringOverlap: stale.proxyRequests.length,
    staleWrites: { runtime: stale.runtimeWrites.length, proxy: stale.proxyWrites.length, connection: stale.connectionWrites.length },
    closedWrites: { runtime: closed.runtimeWrites.length, proxy: closed.proxyWrites.length, connection: closed.connectionWrites.length },
    manualReads,
    originalRequestsDuringOverlap: originalProxyRequests,
  },
  previewLoading: {
    selected,
    loading,
    cache: [...cache],
    loadingWrites,
    cachedReturn: { selected: cachedSelected, loading: cachedLoading, pendingFrames: cachedFrames.length },
  },
  dependencies,
  scriptSha256: sha(fs.readFileSync(fileURLToPath(import.meta.url))),
};

if (verifyOnly) {
  const recorded = JSON.parse(fs.readFileSync(outputPath, "utf8"));
  assert.deepEqual(result, recorded, "recorded shell/Home correction proof is stale");
} else {
  fs.mkdirSync(here, { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
}
console.log(JSON.stringify({ marker: result.marker, recoveryAccepted: result.recovery.currentAccepted.length, recurring: result.recurringStatus, previewLoading: result.previewLoading, verified: verifyOnly }));
