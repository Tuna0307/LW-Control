import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createRequire } from "node:module";
import { createAppHarness, mapSummary } from "./app-ownership-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const ui = path.join(repo, "src/LWBridge.UI-0.3.17");
const requireUi = createRequire(path.join(ui, "package.json"));
const { parse } = requireUi("@babel/parser");
const currentPath = path.join(ui, "src/App.jsx");
const current = fs.readFileSync(currentPath, "utf8");
const dispatch = execFileSync("git", ["cat-file", "blob", "7c0c0b8bcc3f5c036824607004293bb29d4e2dc8:src/LWBridge.UI-0.3.17/src/App.jsx"], { cwd: repo, encoding: "utf8" });
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex");
const normalizedHash = (value) => sha256(value.replaceAll("\r\n", "\n"));

function declaration(source, name) {
  const ast = parse(source, { sourceType: "module", plugins: ["jsx"] });
  for (const entry of ast.program.body) {
    const node = entry.type === "ExportNamedDeclaration" ? entry.declaration : entry;
    if (node?.type === "FunctionDeclaration" && node.id?.name === name) return source.slice(node.start, node.end);
  }
  throw new Error(`missing function ${name}`);
}

const cases = [];
const pass = (name, details = {}) => cases.push({ name, status: "PASS", ...details });

// The accepted first-visit Activity wrapper is outside this task. Pin it against
// the actual dispatch bytes so changes to the header do not silently alter route
// retention/profile identity.
const dispatchRetained = declaration(dispatch, "RetainedPages");
const currentRetained = declaration(current, "RetainedPages");
assert.ok(currentRetained.includes('pagePropsByRoute'));
const currentBoundary = currentRetained.replace(', pagePropsByRoute })', ' })').replace(' {...pagePropsByRoute?.[route.key]}', '');
assert.equal(currentBoundary.replaceAll("\r\n", "\n"), dispatchRetained.replaceAll("\r\n", "\n"));
pass("accepted Activity/profile boundary unchanged except reviewed per-route controlled props", {
  dispatchSha256: normalizedHash(dispatchRetained),
  currentSha256: normalizedHash(currentRetained),
});

// Reuse the accepted App ownership harness, but write only this task's new result.
// This exercises current App callbacks/effects and proves Map summary/status/scan
// ownership remains at the parent around the Cross-server edit.
const app = await createAppHarness(current, "crossserver-current-owner");
await app.mount();
assert.equal(app.summaryRequests.length, 2, "profile bootstrap + connected immediate summary poll");
const bootstrap = app.summaryRequests[0];
const poll = app.summaryRequests[1];
await app.resolveSummary(poll, mapSummary(321, 2));
await app.resolveSummary(bootstrap, mapSummary(321, 1));
assert.equal(app.getState("mapSummary").counts.city, 2, "obsolete bootstrap cannot replace newer parent summary");
await app.advance(5000);
assert.equal(app.summaryRequests.length, 3, "parent owns five-second summary cadence");
const pending = app.summaryRequests[2];
await app.emitStatus({ xluaOnline: false });
await app.resolveSummary(pending, mapSummary(321, 3));
assert.equal(app.getState("mapSummary").counts.city, 3, "same-profile in-flight parent summary may settle after offline transition");
await app.advance(5000);
assert.equal(app.summaryRequests.length, 3, "offline parent does not poll summary");
await app.emitStatus({ xluaOnline: true });
assert.equal(app.summaryRequests.length, 4, "reconnection starts one parent summary poll");
await app.resolveSummary(app.summaryRequests[3], mapSummary(321, 4));
const reading = { ...mapSummary(321, 4).scanState, isReading: true, scanRunId: "crossserver-regression", readBlocks: 1 };
await app.emitScan(reading);
assert.equal(app.summaryRequests.length, 4, "scan progress acknowledgement does not create a second summary owner");
const stopped = { ...reading, isReading: false, readBlocks: 2 };
await app.emitScan(stopped);
assert.equal(app.summaryRequests.length, 5, "reading-to-stopped edge requests one parent completion summary");
await app.resolveSummary(app.summaryRequests[4], { ...mapSummary(321, 5), scanState: stopped });
assert.equal(app.getState("mapSummary").counts.city, 5);
const listenerCountsBeforeUnmount = app.listenerCounts();
await app.unmount();
const listenerCounts = app.listenerCounts();
assert.equal(listenerCounts.statusSubscribes, listenerCounts.statusUnsubscribes);
assert.equal(listenerCounts.scanSubscribes, listenerCounts.scanUnsubscribes);
pass("current parent retains Map summary/status/scan ownership", {
  mountSummaryRequests: 2,
  fiveSecondCadence: true,
  offlineCadenceStopped: true,
  completionSummaryRefresh: true,
  listenerCountsBeforeUnmount,
  listenerCounts,
});

// Re-execute the current Home auto-reconnect callback with the real backendBridge
// transport and controlled WebView responses. This is the Home acknowledgement
// channel nearest the edited App callbacks and proves its owner/ordering survived.
const currentAst = parse(current, { sourceType: "module", plugins: ["jsx"] });
let homeCallbackNode = null;
const findHomeCallback = (node) => {
  if (!node || typeof node !== "object" || homeCallbackNode) return;
  if (node.type === "VariableDeclarator" && node.id?.name === "updateAutoReconnect") homeCallbackNode = node.init?.arguments?.[0];
  for (const value of Object.values(node)) {
    if (Array.isArray(value)) value.forEach(findHomeCallback);
    else if (value && typeof value === "object") findHomeCallback(value);
  }
};
findHomeCallback(currentAst);
assert.equal(homeCallbackNode?.type, "ArrowFunctionExpression", "current Home autoReconnect callback extraction");
const body = current.slice(homeCallbackNode.body.start + 1, homeCallbackNode.body.end - 1);
const { createBackendBridge } = await import(pathToFileURL(path.join(ui, "src/backendBridge.js")).href);
const homeOutcomes = [];
for (const outcome of ["acknowledged", "rejected", "missing-profile"]) {
  let handler;
  const posted = [];
  const events = [];
  let config = { autoReconnect: false };
  const host = {
    __LWBridgeBootstrap: { mode: "live", sessionId: `crossserver-${outcome}`, profiles: { selectedProfileId: outcome === "missing-profile" ? "" : "review-profile" } },
    setTimeout,
    clearTimeout,
    chrome: {
      webview: {
        addEventListener(_event, callback) { handler = callback; },
        removeEventListener() {},
        postMessage(message) { posted.push(message); },
      },
    },
  };
  const bridge = createBackendBridge(host);
  const callback = new Function("backendBridge", "setHomeBusy", "setGameActionError", "setLocalConfig", `return async function(value) {${body}}`)(
    bridge,
    (value) => events.push(["busy", value]),
    (value) => events.push(["error", value]),
    (update) => { config = update(config); events.push(["config", config.autoReconnect]); },
  );
  const action = callback(true);
  assert.deepEqual(events[0], ["busy", "autoReconnect"]);
  assert.equal(config.autoReconnect, false, "Home does not acknowledge checked state before native result");
  if (outcome === "missing-profile") {
    assert.equal(posted.length, 0);
  } else {
    const invokeMessage = posted.find((message) => message.kind === "invoke");
    assert.ok(invokeMessage, `${outcome}: Home callback posts invoke`);
    assert.equal(invokeMessage.payload.profileId, "review-profile");
    assert.equal(invokeMessage.command, "set_automation");
    handler({ data: {
      kind: "response",
      sessionId: `crossserver-${outcome}`,
      id: invokeMessage.id,
      ok: outcome === "acknowledged",
      result: {},
      error: { code: "CONTROLLED_REJECTION", message: "controlled rejection" },
    } });
  }
  await action;
  assert.equal(config.autoReconnect, outcome === "acknowledged");
  assert.deepEqual(events.at(-1), ["busy", ""]);
  if (outcome !== "acknowledged") assert.ok(events.some(([kind, value]) => kind === "error" && value));
  bridge.dispose();
  homeOutcomes.push({ outcome, posted: posted.length, finalAutoReconnect: config.autoReconnect, events });
}
pass("Home auto-reconnect acknowledgement owner remains intact", { outcomes: homeOutcomes });

const report = {
  result: "LWB317_CROSSSERVER_REGRESSIONS_OK",
  currentApp: { path: "src/LWBridge.UI-0.3.17/src/App.jsx", normalizedSha256: normalizedHash(current) },
  dispatchApp: { commit: "7c0c0b8bcc3f5c036824607004293bb29d4e2dc8", normalizedSha256: normalizedHash(dispatch) },
  cases,
  limits: "Current App ownership is executed through the accepted shell-retention hook harness with presentation children inert. Home uses the real backendBridge against a controlled in-memory WebView transport. No historical result file is rewritten and no native/game/provider/network action occurs.",
};
fs.writeFileSync(path.join(here, "crossserver-regression-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(`LWB317_CROSSSERVER_REGRESSIONS_OK cases=${cases.length}`);
