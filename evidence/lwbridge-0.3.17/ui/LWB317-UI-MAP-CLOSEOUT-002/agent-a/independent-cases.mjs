import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";

import {
  createOriginalHarness,
  optionsReply,
  treeNodes as originalTreeNodes,
} from "../../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import {
  createHarness,
  deferred,
  nodeText,
  repo,
  treeNodes,
} from "../../LWB317-UI-MAP-REFRESH-FEEDBACK-001/harness.mjs";
import { treasureName } from "../../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const uiSrc = path.join(repo, "src/LWBridge.UI-0.3.17/src");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { transformSync } = require("esbuild");
const gitShow = (revision, file) => execFileSync("git", ["show", `${revision}:${file}`], { cwd: repo, encoding: "utf8" });
const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const mapPath = "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx";

const results = {
  pins: {},
  treasure: [],
  goods: [],
  header: [],
  headerClock: [],
  export: [],
  errorPriority: [],
  refresh: [],
  disposal: [],
  baselines: [],
  inheritedOrSeparate: [],
};

// Pin the exact immutable source files used by this review.
for (const [name, relative] of [
  ["panel", "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js"],
  ["index", "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"],
  ["asset", "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js"],
]) {
  results.pins[name] = sha256(fs.readFileSync(path.join(repo, relative)));
}
assert.equal(results.pins.panel, "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");
assert.equal(results.pins.index, "44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6");
assert.equal(results.pins.asset, "2f92a87c3268497df6425b1175db6140e10aabcbdfae02615f065d00e16458e0");

function compilePicker(source, name, bindings) {
  const code = transformSync(
    source.replace(/^import .*;\r?$/gm, "").replace(`export function ${name}`, `function ${name}`),
    { loader: "jsx", jsxFactory: "h", target: "es2022" },
  ).code;
  const h = (type, attrs, ...children) => ({ type, props: { ...(attrs || {}), children } });
  return new Function(...Object.keys(bindings), "h", `${code}\nreturn ${name};`)(...Object.values(bindings), h);
}

function pickerView(tree) {
  const buttons = treeNodes(tree).filter((node) => node.type === "button");
  const summary = treeNodes(tree).find((node) => node.type === "summary");
  return {
    summary: nodeText(summary),
    active: buttons.map((button, index) => button.props.className === "active" ? index : null).filter((value) => value !== null),
    buttonText: buttons.map(nodeText),
  };
}

async function checkTreasure() {
  const source = gitShow("fbde5d6", "src/LWBridge.UI-0.3.17/src/MapTreasureTypeFilter.jsx");
  const parentPage = gitShow("fbde5d6^", mapPath);
  assert.match(parentPage, /<select aria-label=\{t\("map\.treasureType"\)\}/);
  assert.doesNotMatch(parentPage, /MapTreasureTypeFilter/);
  results.baselines.push({ id: "treasure-parent-native-select", parent: "fbde5d6^", pass: true });
  let currentT = (key) => key;
  const Current = compilePicker(source, "MapTreasureTypeFilter", {
    useRef: () => ({ current: null }),
    useI18n: () => ({ t: currentT }),
    treasureName,
  });
  const items = [
    { key: 2, treasureType: 2, suppliesType: 0, count: 4 },
    { key: "2", treasureType: 999, suppliesType: 0, treasureNameKey: "my_name", count: 5 },
    { key: 0, treasureType: 0, suppliesType: 1, count: 0 },
  ];
  const gameTexts = { my_name: "Recovered" };
  const original = await createOriginalHarness({
    online: false,
    initialTab: "treasure",
    tabMode: "controlled",
    stubs: { dataOptions: { mode: "auto", value: optionsReply({ treasureTypes: items }) } },
  });
  await original.mount();
  const Original = original.findNodes((node) => typeof node.type === "function" && node.type.name === "lt")[0].type;
  currentT = original.translate;
  for (const value of [2, "2", 0, "missing"]) {
    const o = Original({ items, value, gameTexts, onChange: () => {} });
    const c = Current({ items, value, gameTexts, onChange: () => {} });
    assert.deepEqual(pickerView(c), pickerView(o), `treasure strict key ${JSON.stringify(value)}`);
    results.treasure.push({ id: `strict-${JSON.stringify(value)}`, view: pickerView(c), pass: true });
  }
  for (const [label, Component, nodes] of [["original", Original, originalTreeNodes], ["current", Current, treeNodes]]) {
    const trace = [];
    const successful = Component({ items, value: "", gameTexts, onChange: (key) => trace.push(["change", key]) });
    successful.props.ref.current = { removeAttribute: (attr) => trace.push(["remove", attr]) };
    nodes(successful).filter((node) => node.type === "button")[1].props.onClick();
    assert.deepEqual(trace, [["change", 2], ["remove", "open"]], `${label} treasure change/close order`);
    results.treasure.push({ id: `${label}-successful-change-before-close`, trace, pass: true });

    const error = new Error("controlled treasure callback failure");
    let closed = false;
    const tree = Component({ items, value: "", gameTexts, onChange: () => { throw error; } });
    tree.props.ref.current = { removeAttribute: () => { closed = true; } };
    assert.throws(() => nodes(tree).find((node) => node.type === "button").props.onClick(), (caught) => caught === error);
    assert.equal(closed, false, `${label} treasure closes after throwing callback`);
    const noRef = Component({ items, value: "", gameTexts, onChange: () => {} });
    noRef.props.ref.current = null;
    assert.doesNotThrow(() => nodes(noRef).find((node) => node.type === "button").props.onClick());
    results.treasure.push({ id: `${label}-throw-keeps-open-and-null-ref-safe`, pass: true });
  }
  await original.unmount();
}

async function checkGoods() {
  const source = gitShow("315c5a7", "src/LWBridge.UI-0.3.17/src/MapRetainedGoodsFilter.jsx");
  const parentPage = gitShow("315c5a7^", mapPath);
  assert.match(parentPage, /<select aria-label=\{t\("map\.itemFilter"\)\}/);
  assert.doesNotMatch(parentPage, /MapRetainedGoodsFilter/);
  results.baselines.push({ id: "goods-parent-native-select", parent: "315c5a7^", pass: true });
  const Current = compilePicker(source, "MapRetainedGoodsFilter", { useRef: () => ({ current: null }) });
  const items = [
    { key: 2, name: "Numeric", iconPath: "n" },
    { key: "2", name: "String", iconPath: "s" },
    { key: 0, name: "Zero", iconPath: "z" },
  ];
  const original = await createOriginalHarness({
    online: false,
    initialTab: "truck",
    tabMode: "controlled",
    stubs: { dataOptions: { mode: "auto", value: optionsReply({ rewardItems: { truck: items, railway: items } }) } },
  });
  await original.mount();
  const Original = original.findNodes((node) => typeof node.type === "function" && node.type.name === "ct")[0].type;
  for (const value of [2, "2", 0, "missing"]) {
    const props = { items, value, label: "Items", allLabel: "All", onChange: () => {} };
    assert.deepEqual(pickerView(Current(props)), pickerView(Original(props)), `goods strict key ${JSON.stringify(value)}`);
    results.goods.push({ id: `strict-${JSON.stringify(value)}`, view: pickerView(Current(props)), pass: true });
  }
  for (const [label, Component, nodes] of [["original", Original, originalTreeNodes], ["current", Current, treeNodes]]) {
    const trace = [];
    const successful = Component({ items, value: "", label: "Items", allLabel: "All", onChange: (key) => trace.push(["change", key]) });
    successful.props.ref.current = { removeAttribute: (attr) => trace.push(["remove", attr]) };
    nodes(successful).filter((node) => node.type === "button")[1].props.onClick();
    assert.deepEqual(trace, [["change", 2], ["remove", "open"]], `${label} goods change/close order`);
    results.goods.push({ id: `${label}-successful-change-before-close`, trace, pass: true });

    const error = new Error("controlled goods callback failure");
    let closed = false;
    const tree = Component({ items, value: "", label: "Items", allLabel: "All", onChange: () => { throw error; } });
    tree.props.ref.current = { removeAttribute: () => { closed = true; } };
    assert.throws(() => nodes(tree).find((node) => node.type === "button").props.onClick(), (caught) => caught === error);
    assert.equal(closed, false, `${label} goods closes after throwing callback`);
    const noRef = Component({ items, value: "", label: "Items", allLabel: "All", onChange: () => {} });
    noRef.props.ref.current = null;
    assert.doesNotThrow(() => nodes(noRef).find((node) => node.type === "button").props.onClick());
    results.goods.push({ id: `${label}-throw-keeps-open`, pass: true });
  }
  await original.unmount();
}

function summaryView(p) {
  const summary = p.findNodes((node) => node.props?.className === "map-scan-summary")[0];
  const progress = treeNodes(summary).find((node) => node.type === "progress");
  const spans = treeNodes(summary).filter((node) => node.type === "span");
  return {
    text: nodeText(summary),
    progressValue: progress?.props.value,
    progressMax: progress?.props.max,
    statusText: nodeText(spans[0]),
  };
}

async function bootHeaderOriginal(spec) {
  const scan = {
    serverId: 321,
    scanRunId: "run",
    isReading: false,
    phase: "idle",
    selectedTypes: ["city"],
    scanMode: "normal",
    progressPercent: 0,
    startedAt: 0,
    ...spec.scan,
  };
  const p = await createOriginalHarness({
    online: false,
    now: spec.now,
    initialTab: "city",
    tabMode: "controlled",
    scanState: scan,
    translate: (key) => key,
    stubs: { dataOptions: { mode: "auto", value: optionsReply({ scanProgress: spec.stored ?? null }) } },
  });
  await p.mount();
  if (spec.absent) await p.setProps({ scanState: undefined, summary: null });
  return p;
}

async function bootHeaderCurrent(spec, revision = "123459d") {
  const source = gitShow(revision, mapPath);
  let state = {
    serverId: 321,
    scanRunId: "run",
    isReading: false,
    phase: "idle",
    selectedTypes: ["city"],
    scanMode: "normal",
    progressPercent: 0,
    startedAt: 0,
    ...spec.scan,
  };
  const p = await createHarness(source, `agent-a-header-${revision}`, {
    now: spec.now,
    serverId: spec.absent ? 0 : 321,
    translate: (key) => key,
    props: { previewState: "map-city", currentServerId: spec.absent ? 0 : 321 },
    dataOptions: optionsReply({ scanProgress: spec.stored ?? null })(321),
    apiExtensions: () => ({
      summary: () => spec.absent ? new Promise(() => {}) : Promise.resolve({ serverId: state.serverId, counts: {}, scanState: state }),
    }),
  });
  await p.mount();
  p.emit = async (next) => {
    state = { ...state, ...next };
    p.liveStatusListener()(state);
    await p.settle();
  };
  return p;
}

async function checkHeader() {
  const NOW = Date.UTC(2026, 9, 3, 12, 0, 0);
  const START = NOW - 65_000;
  const baseStored = { serverId: 321, id: "run", status: "completed", createdAt: START, updatedAt: NOW - 1000, error: null };
  const cases = [
    { id: "strict-run-id", scan: { scanRunId: 1, startedAt: START }, stored: { ...baseStored, id: "1" } },
    { id: "strict-server-id", scan: { startedAt: START }, stored: { ...baseStored, serverId: "321" } },
    { id: "stored-running-ignored", scan: { startedAt: START }, stored: { ...baseStored, status: "running" } },
    { id: "publishing-stopped-priority", scan: { phase: "publishing", progressPercent: 99.75, startedAt: START, updatedAt: NOW } },
    { id: "provided-zero", scan: {} },
    { id: "optional-absent", absent: true },
  ];
  for (const spec of cases) {
    const full = { ...spec, now: NOW };
    const o = await bootHeaderOriginal(full);
    const c = await bootHeaderCurrent(full);
    assert.deepEqual(summaryView(c), summaryView(o), `header ${spec.id}`);
    results.header.push({ id: spec.id, view: summaryView(c), pass: true });
    await o.unmount();
    await c.unmount();
  }

  // Immutable parent must still exhibit the recovered header mismatch.
  {
    const spec = { now: NOW, scan: { phase: "publishing", progressPercent: 99.75, startedAt: START, updatedAt: NOW } };
    const o = await bootHeaderOriginal(spec);
    const b = await bootHeaderCurrent(spec, "123459d^");
    assert.notDeepEqual(summaryView(b), summaryView(o), "header parent unexpectedly matches recovered publishing view");
    results.baselines.push({ id: "header-parent-publishing-mismatch", parent: "123459d^", original: summaryView(o), parentView: summaryView(b), pass: true });
    await o.unmount(); await b.unmount();
  }

  const o = await bootHeaderOriginal({ now: NOW, scan: { isReading: true, phase: "reading", startedAt: START } });
  const c = await bootHeaderCurrent({ now: NOW, scan: { isReading: true, phase: "reading", startedAt: START } });
  for (const [id, advance] of [["t0", 0], ["t999", 999], ["t1000", 1]]) {
    if (advance) { await o.advance(advance); await c.advance(advance); }
    assert.deepEqual(summaryView(c), summaryView(o), `header clock ${id}`);
    results.headerClock.push({ id, original: o.getState("currentTime"), current: c.getState("currentTime"), pass: true });
  }
  await o.emitScanState({ isReading: false, updatedAt: NOW + 1000 });
  await c.emit({ isReading: false, updatedAt: NOW + 1000 });
  const stopped = c.getState("currentTime");
  await o.advance(1000); await c.advance(1000);
  assert.equal(c.getState("currentTime"), stopped, "current header clock stops after reading");
  const beforeUnmount = c.getState("currentTime");
  await o.unmount(); await c.unmount();
  await o.advance(1000); await c.advance(1000);
  assert.equal(c.getState("currentTime"), beforeUnmount, "current header clock remains disposed");
  results.headerClock.push({ id: "stop-and-unmount-cleanup", pass: true });
}

async function bootFeedbackOriginal(extra = {}) {
  const state = {
    serverId: 321,
    scanRunId: "run",
    isReading: false,
    phase: "idle",
    selectedTypes: ["city"],
    scanMode: "normal",
    readBlocks: 0,
    progressPercent: 0,
    startedAt: 0,
    lastError: "",
    ...extra.scan,
  };
  const p = await createOriginalHarness({
    online: true,
    now: Date.UTC(2026, 9, 3, 12, 0, 0),
    initialTab: "city",
    tabMode: "controlled",
    scanState: state,
    translate: (key, values = {}) => values.count !== undefined || values.path !== undefined ? `${key}:${values.count ?? ""}:${values.path ?? ""}` : key,
    stubs: {
      dataOptions: { mode: "auto", value: optionsReply({ scanProgress: extra.stored ?? null }) },
      cityExport: { mode: "manual" },
      scanStart: { mode: "manual" },
    },
  });
  await p.mount();
  return p;
}

async function bootFeedbackCurrent(extra = {}, revision = "f8f3f4e") {
  const source = gitShow(revision, mapPath);
  let state = {
    serverId: 321,
    scanRunId: "run",
    isReading: false,
    phase: "idle",
    selectedTypes: ["city"],
    scanMode: "normal",
    readBlocks: 0,
    progressPercent: 0,
    startedAt: 0,
    lastError: "",
    ...extra.scan,
  };
  const exports = [];
  const starts = [];
  const p = await createHarness(source, "agent-a-feedback", {
    now: Date.UTC(2026, 9, 3, 12, 0, 0),
    serverId: 321,
    translate: (key, values = {}) => values.count !== undefined || values.path !== undefined ? `${key}:${values.count ?? ""}:${values.path ?? ""}` : key,
    props: { previewState: "map-city", currentServerId: 321, online: true },
    dataOptions: optionsReply({ scanProgress: extra.stored ?? null })(321),
    apiExtensions: () => ({
      summary: async () => ({ serverId: state.serverId, counts: {}, scanState: state }),
      exportCities: (...args) => { const d = deferred(); exports.push({ args, ...d }); return d.promise; },
      start: (...args) => { const d = deferred(); starts.push({ args, ...d }); return d.promise; },
    }),
  });
  await p.mount();
  p.feedbackExports = exports;
  p.feedbackStarts = starts;
  p.emit = async (next) => {
    state = { ...state, ...next };
    p.liveStatusListener()(state);
    await p.settle();
  };
  return p;
}

const actionMessage = (p) => p.findNodes((node) => node.props?.className === "map-claim-result").map(nodeText);
const exportButton = (p) => p.findNodes((node) => node.type === "button" && ["map.exportExcel", "map.exportingExcel"].includes(nodeText(node)))[0];
const startButton = (p) => p.findNodes((node) => node.type === "button" && nodeText(node) === "map.startReading")[0];
const scanErrors = (p) => p.findNodes((node) => node.props?.className === "map-scan-error").map((node) => ({ role: node.props.role, text: nodeText(node) }));

async function singleExport(p, original, reply) {
  const pending = exportButton(p).props.onClick();
  await p.settle();
  assert.equal(nodeText(exportButton(p)), "map.exportingExcel");
  assert.equal(Boolean(exportButton(p).props.disabled), true);
  const call = original ? p.callsNamed("cityExport").at(-1) : p.feedbackExports.at(-1);
  call.resolve(reply);
  await pending;
  await p.settle();
  return { message: actionMessage(p), busy: Boolean(exportButton(p).props.disabled), label: nodeText(exportButton(p)) };
}

async function concurrentExports(p, original) {
  const button = exportButton(p);
  const first = button.props.onClick();
  const second = button.props.onClick();
  await p.settle();
  const calls = original ? p.callsNamed("cityExport").slice(-2) : p.feedbackExports.slice(-2);
  assert.equal(calls.length, 2);
  calls[1].resolve({ canceled: false, rowCount: 2, path: "second.xlsx" });
  await second; await p.settle();
  const afterSecond = { busy: Boolean(exportButton(p).props.disabled), message: actionMessage(p) };
  calls[0].resolve({ canceled: false, rowCount: 1, path: "first.xlsx" });
  await first; await p.settle();
  const final = { busy: Boolean(exportButton(p).props.disabled), message: actionMessage(p) };
  return { afterSecond, final };
}

async function checkFeedback() {
  {
    const o = await bootFeedbackOriginal(), c = await bootFeedbackCurrent();
    const ov = await singleExport(o, true, { canceled: true, rowCount: 0, path: "" });
    const cv = await singleExport(c, false, { canceled: true, rowCount: 0, path: "" });
    assert.deepEqual(cv, ov, "export cancellation/busy");
    results.export.push({ id: "cancellation-and-busy", trace: cv, pass: true });
    await o.unmount(); await c.unmount();
  }

  // A matching non-running stored run owns the derived error over live scanState.lastError.
  {
    const extra = {
      scan: { lastError: "GAME_NOT_CONNECTED" },
      stored: { id: "run", serverId: 321, status: "failed", error: "SCAN_RUNNING", createdAt: 1, updatedAt: 2 },
    };
    const o = await bootFeedbackOriginal(extra), c = await bootFeedbackCurrent(extra);
    assert.deepEqual(scanErrors(c), scanErrors(o), "stored error priority over live error");
    results.errorPriority.push({ id: "matching-stored-error-over-live", view: scanErrors(c), pass: true });
    await o.unmount(); await c.unmount();
  }

  // A local Start rejection is an alert and remains above subsequent stored/live acknowledgements.
  {
    const extra = {
      scan: { lastError: "GAME_NOT_CONNECTED" },
      stored: { id: "run", serverId: 321, status: "failed", error: "SCAN_RUNNING", createdAt: 1, updatedAt: 2 },
    };
    const o = await bootFeedbackOriginal(extra), c = await bootFeedbackCurrent(extra);
    const op = startButton(o).props.onClick(), cp = startButton(c).props.onClick();
    await o.settle(); await c.settle();
    const ocall = o.callsNamed("scanStart").at(-1), ccall = c.feedbackStarts.at(-1);
    ocall.reject(new Error("MAP_SCAN_REJECTED")); ccall.reject(new Error("MAP_SCAN_REJECTED"));
    await op; await cp; await o.settle(); await c.settle();
    await o.emitScanState({ lastError: "GAME_NOT_CONNECTED" }); await c.emit({ lastError: "GAME_NOT_CONNECTED" });
    assert.deepEqual(scanErrors(c), scanErrors(o), "local start error priority");
    assert.equal(scanErrors(c)[0]?.role, "alert");
    results.errorPriority.push({ id: "local-start-error-over-stored-live", view: scanErrors(c), pass: true });
    await o.unmount(); await c.unmount();
  }

  // Immutable parent: successful export lacked the recovered result message.
  {
    const o = await bootFeedbackOriginal(), b = await bootFeedbackCurrent({}, "f8f3f4e^");
    const ov = await singleExport(o, true, { canceled: false, rowCount: 7, path: "baseline.xlsx" });
    const bv = await singleExport(b, false, { canceled: false, rowCount: 7, path: "baseline.xlsx" });
    assert.notDeepEqual(bv, ov, "feedback parent unexpectedly matches export success feedback");
    results.baselines.push({ id: "feedback-parent-export-success-mismatch", parent: "f8f3f4e^", original: ov, parentView: bv, pass: true });
    await o.unmount(); await b.unmount();
  }

  // Immutable parent: reading progress did not schedule the recovered trailing row refresh.
  {
    const o = await bootFeedbackOriginal(), b = await bootFeedbackCurrent({}, "f8f3f4e^");
    const o0 = o.getState("rowsRevision"), b0 = b.getState("searchRevision");
    await o.emitScanState({ isReading: true, readBlocks: 1 }); await b.emit({ isReading: true, readBlocks: 1 });
    await o.advance(1000); await b.advance(1000);
    const originalDelta = o.getState("rowsRevision") - o0;
    const parentDelta = b.getState("searchRevision") - b0;
    assert.equal(originalDelta, 1);
    assert.equal(parentDelta, 0);
    results.baselines.push({ id: "feedback-parent-row-refresh-mismatch", parent: "f8f3f4e^", originalDelta, parentDelta, pass: true });
    await o.unmount(); await b.unmount();
  }
  {
    const o = await bootFeedbackOriginal(), c = await bootFeedbackCurrent();
    const ov = await concurrentExports(o, true), cv = await concurrentExports(c, false);
    assert.deepEqual(cv, ov, "concurrent export completion order");
    results.export.push({ id: "concurrent-second-then-first", trace: cv, pass: true });
    await o.unmount(); await c.unmount();
  }

  // Coalescing boundary: readBlocks changes just before the original 1000 ms deadline do not postpone it.
  {
    const o = await bootFeedbackOriginal(), c = await bootFeedbackCurrent();
    const o0 = o.getState("rowsRevision"), c0 = c.getState("searchRevision");
    await o.emitScanState({ isReading: true, readBlocks: 0 }); await c.emit({ isReading: true, readBlocks: 0 });
    await o.advance(999); await c.advance(999);
    await o.emitScanState({ readBlocks: 1 }); await c.emit({ readBlocks: 1 });
    assert.equal(o.getState("rowsRevision"), o0); assert.equal(c.getState("searchRevision"), c0);
    await o.advance(1); await c.advance(1);
    assert.equal(c.getState("searchRevision") - c0, o.getState("rowsRevision") - o0);
    results.refresh.push({ id: "read-change-at-999ms-keeps-original-deadline", delta: c.getState("searchRevision") - c0, pass: true });
    await o.unmount(); await c.unmount();
  }

  // Completion immediately before the trailing deadline cancels it and emits exactly the completion refresh.
  {
    const o = await bootFeedbackOriginal(), c = await bootFeedbackCurrent();
    const o0 = o.getState("rowsRevision"), c0 = c.getState("searchRevision");
    await o.emitScanState({ isReading: true, readBlocks: 1 }); await c.emit({ isReading: true, readBlocks: 1 });
    await o.advance(999); await c.advance(999);
    await o.emitScanState({ isReading: false }); await c.emit({ isReading: false });
    const afterCompletion = [o.getState("rowsRevision") - o0, c.getState("searchRevision") - c0];
    await o.advance(1); await c.advance(1);
    const afterDeadline = [o.getState("rowsRevision") - o0, c.getState("searchRevision") - c0];
    assert.deepEqual(afterCompletion, [1, 1]);
    assert.deepEqual(afterDeadline, [1, 1]);
    results.refresh.push({ id: "completion-at-999ms-cancels-trailing", afterCompletion, afterDeadline, pass: true });
    await o.unmount(); await c.unmount();
  }

  // Timer-generated row request must be retired by navigation; its late success cannot populate the new tab.
  for (const original of [true, false]) {
    const p = original ? await bootFeedbackOriginal() : await bootFeedbackCurrent();
    const initial = p.currentRequest();
    await p.resolveRequest(initial, { rows: [{ uuid: "initial" }], total: 1 });
    if (original) await p.emitScanState({ isReading: true, readBlocks: 1 }); else await p.emit({ isReading: true, readBlocks: 1 });
    await p.advance(1000);
    const stale = p.currentRequest();
    await p.clickTab("resource");
    const fresh = p.currentRequest();
    assert.notEqual(stale, fresh);
    await p.resolveRequest(stale, { rows: [{ uuid: "STALE" }], total: 1 });
    await p.resolveRequest(fresh, { rows: [{ uuid: "FRESH" }], total: 1 });
    assert.equal(p.getState("rows")[0]?.uuid, "FRESH", `${original ? "original" : "current"} stale timer request mutated rows`);
    results.disposal.push({ id: `${original ? "original" : "current"}-timer-request-navigation-fence`, pass: true });
    await p.unmount();
  }

  // Preserve a separate known ownership gap rather than misattributing it to the Feedback commit:
  // the original can mount already reading, while the f8f3f4e local wrapper learns that state from its first summary.
  // Record completion-side option-call inventory without asserting parity; Milestone B owns that lifecycle.
  {
    const reading = { isReading: true, readBlocks: 3, startedAt: Date.UTC(2026, 9, 3, 11, 0, 0) };
    const o = await bootFeedbackOriginal({ scan: reading });
    const c = await bootFeedbackCurrent({ scan: reading });
    const ob = o.callsNamed("dataOptions").length;
    // Current local summary has settled to reading=true, but its listener-side previousReading ref was initialized false.
    const previousReadingBeforeCompletion = c.getRef("previousReading")?.current;
    const currentOptionsGenerationBefore = c.getRef("optionsGeneration")?.current;
    await o.emitScanState({ isReading: false });
    await c.emit({ isReading: false });
    const currentOptionsGenerationAfter = c.getRef("optionsGeneration")?.current;
    results.inheritedOrSeparate.push({
      id: "mount-reading-completion-options-ownership",
      originalDataOptionsDelta: o.callsNamed("dataOptions").length - ob,
      currentPreviousReadingBeforeCompletion: previousReadingBeforeCompletion,
      currentOptionsGenerationBefore,
      currentOptionsGenerationAfter,
      currentOptionsGenerationDelta: currentOptionsGenerationAfter - currentOptionsGenerationBefore,
      note: "Recorded for Milestone B ownership; not attributed to f8f3f4e without parent comparison.",
    });
    await o.unmount(); await c.unmount();
  }
}

await checkTreasure();
await checkGoods();
await checkHeader();
await checkFeedback();

results.status = "PASS";
results.reviewedCommits = {
  treasure: "fbde5d6",
  goods: "315c5a7",
  header: "123459d",
  feedback: "f8f3f4e",
};
if (process.argv.includes("--record")) {
  fs.writeFileSync(path.join(here, "independent-results.json"), JSON.stringify(results, null, 2) + "\n");
}
console.log(`LWB317_MAP_CLOSEOUT_AGENT_A_OK treasure=${results.treasure.length} goods=${results.goods.length} header=${results.header.length} export=${results.export.length} errorPriority=${results.errorPriority.length} refresh=${results.refresh.length} disposal=${results.disposal.length}`);
