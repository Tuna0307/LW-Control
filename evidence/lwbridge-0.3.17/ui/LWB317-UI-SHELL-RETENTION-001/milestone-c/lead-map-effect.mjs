import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { nodes, raw, evaluate, read } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const originalPath = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js";
const currentPath = "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx";
const original = read(originalPath);
const current = read(currentPath);
const sha256 = (s) => crypto.createHash("sha256").update(s).digest("hex").toUpperCase();
const delayNodes = nodes(original).filter((n) => n.type === "VariableDeclarator" && n.id?.name === "Fe");
assert.equal(delayNodes.length, 1);
assert.equal(delayNodes[0].init?.type, "NumericLiteral");
const originalDelay = delayNodes[0].init.value;
assert.equal(originalDelay, 1000);

function effect(source, includes, excludes = "") {
  const matches = nodes(source).filter((n) => n.type === "CallExpression" && raw(source, n.callee).endsWith("useEffect") && n.arguments[0]?.type === "ArrowFunctionExpression")
    .map((n) => n.arguments[0]).filter((n) => includes.every((s) => raw(source, n).includes(s)) && (!excludes || !raw(source, n).includes(excludes)));
  assert.equal(matches.length, 1, `Unique effect: ${includes}`);
  const n = matches[0];
  return { code: raw(source, n), utf8ByteOffset: Buffer.byteLength(source.slice(0, n.start)), utf8ByteLength: Buffer.byteLength(raw(source, n)), sha256: sha256(raw(source, n)) };
}

const callbacks = {
  original: {
    progress: effect(original, ["X.current", "window.setTimeout", "scan-progress"]),
    disposal: effect(original, ["X.current", "window.clearTimeout"], "window.setTimeout"),
  },
  current: {
    progress: effect(current, ["scanProgressTimer.current", "window.setTimeout", "setSearchRevision"]),
    disposal: effect(current, ["scanProgressTimer.current", "window.clearTimeout"], "window.setTimeout"),
  },
};

function run(kind, fireBeforeHide) {
  let id = 0;
  const timers = new Map();
  const ref = { current: null };
  const state = { isReading: true, readBlocks: 1 };
  let revision = 0;
  const setRevision = (updater) => { revision = updater(revision); };
  const window = {
    setTimeout: (fn, delay) => { assert.equal(delay, 1000); timers.set(++id, fn); return id; },
    clearTimeout: (timer) => timers.delete(timer),
  };
  const env = kind === "original"
    ? { window, X: ref, C: state, Fe: originalDelay, _e: (reason) => { assert.equal(reason, "scan-progress"); return { rows: true }; }, _n: setRevision }
    : { window, scanProgressTimer: ref, scanState: state, setSearchRevision: setRevision };
  const reconnect = evaluate(callbacks[kind].progress.code, env);
  const installDisposal = evaluate(callbacks[kind].disposal.code, env);
  const observations = [];
  const snapshot = (stage) => observations.push({ stage, activeTimers: timers.size, timerRef: ref.current, revision });
  const fire = () => { const [timer, fn] = timers.entries().next().value; timers.delete(timer); fn(); };

  reconnect();
  const cleanup = installDisposal();
  snapshot("visible-reading");
  assert.equal(timers.size, 1);
  if (fireBeforeHide) { fire(); snapshot("timer-completed-before-hide"); assert.equal(ref.current, null); }
  cleanup();
  snapshot("hidden-cleanup");
  assert.equal(timers.size, 0);
  reconnect();
  installDisposal();
  snapshot("visible-reconnect");
  assert.equal(timers.size, fireBeforeHide ? 1 : 0);
  if (!fireBeforeHide) {
    assert.notEqual(ref.current, null);
    state.readBlocks++;
    reconnect();
    snapshot("reading-progress-after-return");
    assert.equal(timers.size, 0);
    state.isReading = false;
    reconnect();
    snapshot("reading-stopped");
    assert.equal(ref.current, null);
    state.isReading = true;
    reconnect();
    snapshot("reading-restarted");
    assert.equal(timers.size, 1);
  }
  fire();
  snapshot("returned-timer-fired");
  assert.equal(revision, fireBeforeHide ? 2 : 1);
  return observations;
}

const cases = [];
for (const completed of [false, true]) {
  const originalResult = run("original", completed);
  const currentResult = run("current", completed);
  assert.deepEqual(currentResult, originalResult);
  cases.push({ name: completed ? "completed timer reconnects" : "canceled timer retains sentinel until reading stops", result: "PARITY", original: originalResult, current: currentResult });
}

const report = {
  result: "LWB317_SHELL_LEAD_MAP_EFFECT_PARITY_OK",
  sources: [{ path: originalPath, sha256: sha256(original) }, { path: currentPath, sha256: sha256(current) }],
  callbacks,
  originalDelay: { value: originalDelay, declaration: raw(original, delayNodes[0]), utf8ByteOffset: Buffer.byteLength(original.slice(0, delayNodes[0].start)) },
  cases,
  conclusion: "Both exact source callbacks retain the canceled non-null timer sentinel on hide. Reconnection does not rearm scan-progress refresh until reading stops; a timer completed before hiding reconnects normally. This is original/current parity, not a clone-only defect.",
  limits: "Executes exact extracted effect setup/cleanup callbacks with controlled inert timer ownership and source-declared Fe delay. The original refresh policy is supplied as rows=true to exercise its scan-progress callback. Explicit cleanup/reconnect models Activity effect disconnection; this is not a React mount, browser, native function, or live gameplay proof.",
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "lead-map-effect-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
