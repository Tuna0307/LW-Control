import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { MAP_KIND_KEYS, buildStartPayload } from "../../../../src/LWBridge.UI-0.3.17/src/mapBackend.js";
import { createOriginalHarness, optionsReply } from "../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs";
import { loadPanel, parse, rawOf, walkAll } from "../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";
import { createHarness, treeNodes } from "../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const currentPageFile = "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx";
const currentHelperFile = "src/LWBridge.UI-0.3.17/src/mapBackend.js";
const panel = loadPanel();
const currentPageSource = fs.readFileSync(path.join(repo, currentPageFile), "utf8");
const currentHelperSource = fs.readFileSync(path.join(repo, currentHelperFile), "utf8");
const hash = (text) => crypto.createHash("sha256").update(text).digest("hex");

function locator(source, file, node, name) {
  const text = source.slice(node.start, node.end);
  return {
    name,
    file,
    utf8ByteOffset: Buffer.byteLength(source.slice(0, node.start)),
    byteLength: Buffer.byteLength(text),
    sha256: hash(text),
    text,
  };
}

function needleLocator(source, file, needle, name) {
  const start = source.indexOf(needle);
  assert.ok(start >= 0, `${name}: source needle`);
  return {
    name,
    file,
    utf8ByteOffset: Buffer.byteLength(source.slice(0, start)),
    byteLength: Buffer.byteLength(needle),
    sha256: hash(needle),
    text: needle,
  };
}

const sourceLocators = [];
let originalOnChange = null;
walkAll(panel.R, (node) => {
  if (node.type !== "ArrowFunctionExpression" || node.body.type !== "BlockStatement") return;
  if (!rawOf(panel.entry, node).includes("P.current=!0,qe(")) return;
  originalOnChange = node;
});
assert.ok(originalOnChange, "original Manual checkbox onChange");
sourceLocators.push(locator(panel.entry.source, panel.entry.relative, originalOnChange, "original Manual checkbox onChange"));

const originalAst = parse(panel.entry.source, { sourceType: "module" });
let originalNormalizer = null;
walkAll(originalAst.program, (node) => {
  if (node.type === "FunctionDeclaration" && node.id?.name === "rt") originalNormalizer = node;
});
assert.ok(originalNormalizer, "original rt selected-type normalizer");
const originalNormalizerSource = rawOf(panel.entry, originalNormalizer);
sourceLocators.push(locator(panel.entry.source, panel.entry.relative, originalNormalizer, "original rt selected-type normalizer"));

for (const entry of parse(currentHelperSource, { sourceType: "module" }).program.body) {
  const node = entry.declaration;
  if (["normalizeSelectedTypes", "updateSelectedTypes", "buildStartPayload"].includes(node?.id?.name)) {
    sourceLocators.push(locator(currentHelperSource, currentHelperFile, node, `current ${node.id.name}`));
  }
}

const manualInputNeedle = '<input type="checkbox" checked={selectedTypes.includes(key)} onChange={(event) => toggleType(key, event.target.checked)} />';
const autoInputNeedle = '<input type="checkbox" checked={autoConfig.selectedTypes.includes(key)} disabled={autoConfig.selectedTypes.length === 1 && autoConfig.selectedTypes[0] === key} onChange={(event) => toggleAutoType(key, event.target.checked)} />';
sourceLocators.push(needleLocator(currentPageSource, currentPageFile, manualInputNeedle, "current Manual checkbox"));
sourceLocators.push(needleLocator(currentPageSource, currentPageFile, autoInputNeedle, "current Auto checkbox last-type protection"));

const originalNormalize = new Function(
  "Le",
  "D",
  `${originalNormalizerSource}\nreturn rt;`,
)(new Set(MAP_KIND_KEYS), [...MAP_KIND_KEYS]);
const startNormalization = {
  originalEmpty: originalNormalize([]),
  currentEmpty: buildStartPayload([], "fast").selectedTypes,
  currentScanMode: buildStartPayload([], "fast").scanMode,
};
assert.deepEqual(startNormalization.originalEmpty, MAP_KIND_KEYS, "original Start normalizes empty selection to all recovered types");
assert.deepEqual(startNormalization.currentEmpty, MAP_KIND_KEYS, "current request boundary must preserve empty-to-all normalization");
assert.equal(startNormalization.currentScanMode, "fast");

const now = Date.UTC(2026, 9, 4, 9, 30);
const scan = {
  serverId: 321,
  isReading: false,
  phase: "idle",
  selectedTypes: ["city"],
  scanMode: "fast",
  progressPercent: 0,
  startedAt: 0,
  updatedAt: 0,
};
const summary = { serverId: 321, counts: {}, scanState: scan };
const autoConfigEdits = [];
const original = await createOriginalHarness({
  online: true,
  now,
  scanState: scan,
  summary,
  stubs: { dataOptions: { mode: "auto", value: optionsReply() } },
});
const current = await createHarness(currentPageSource, "manual-types-corrected", {
  now,
  serverId: 321,
  dataOptions: optionsReply()(321),
  apiExtensions: () => ({
    start: async () => { throw new Error("Manual selection check must not invoke Start"); },
    stop: async () => { throw new Error("Manual selection check must not invoke Stop"); },
    clear: async () => { throw new Error("Manual selection check must not invoke Clear"); },
  }),
  props: {
    bridgeMode: "native",
    backendAvailable: true,
    online: true,
    currentServerId: 321,
    scanState: scan,
    summary,
    onState: () => {},
    onCounts: () => {},
    onAutoScanConfig: (next) => autoConfigEdits.push(structuredClone(next)),
    activeTab: "city",
    onActiveTabChange: () => {},
    previewState: "",
  },
});

function inputs(harness) {
  const group = treeNodes(harness.tree()).find((node) => node.props?.className === "map-types map-types--compact");
  assert.ok(group, `${harness.label || "original"}: Manual type group`);
  const values = treeNodes(group).filter((node) => node.type === "input" && node.props?.type === "checkbox");
  assert.equal(values.length, MAP_KIND_KEYS.length, `${harness.label || "original"}: eight Manual inputs`);
  return values;
}

function selected(harness, stateName) {
  return [...harness.getState(stateName)];
}

async function toggle(harness, key, checked) {
  const index = MAP_KIND_KEYS.indexOf(key);
  assert.ok(index >= 0, `known kind ${key}`);
  const input = inputs(harness)[index];
  assert.equal(Boolean(input.props.disabled), false, `${harness.label || "original"}: ${key} must be user-toggleable`);
  input.props.onChange({ target: { checked } });
  await harness.settle();
}

const snapshots = [];
function compareState(label) {
  const left = selected(original, "Ke");
  const right = selected(current, "selectedTypes");
  assert.deepEqual(right, left, `${label}: current selection matches original callback state`);
  snapshots.push({ label, original: left, current: right });
}

try {
  await original.mount();
  await current.mount();
  assert.equal(Boolean(inputs(original)[0].props.disabled), false, "original final Manual city type is enabled");
  assert.equal(Boolean(inputs(current)[0].props.disabled), false, "current final Manual city type is enabled");
  assert.equal(autoConfigEdits.length, 0, "mount must not mutate Auto config");
  compareState("single-city");

  await toggle(original, "city", false);
  await toggle(current, "city", false);
  compareState("single-to-empty");
  assert.deepEqual(snapshots.at(-1).current, []);

  await toggle(original, "resource", true);
  await toggle(current, "resource", true);
  compareState("empty-to-resource");
  assert.deepEqual(snapshots.at(-1).current, ["resource"]);

  await toggle(original, "city", true);
  await toggle(current, "city", true);
  compareState("append-city");
  assert.deepEqual(snapshots.at(-1).current, ["resource", "city"]);

  await toggle(original, "monster", true);
  await toggle(current, "monster", true);
  compareState("append-monster");
  assert.deepEqual(snapshots.at(-1).current, ["resource", "city", "monster"]);

  await toggle(original, "city", false);
  await toggle(current, "city", false);
  compareState("remove-middle-city");
  assert.deepEqual(snapshots.at(-1).current, ["resource", "monster"]);

  await toggle(original, "city", true);
  await toggle(current, "city", true);
  compareState("reappend-city-at-end");
  assert.deepEqual(snapshots.at(-1).current, ["resource", "monster", "city"]);

  assert.equal(autoConfigEdits.length, 0, "Manual edits must not mutate Auto config");

  const report = {
    marker: "LWB317_MAP_MANUAL_TYPES_CALLBACK_OK",
    baseline: "baseline-selection-counter.json preserves the accepted pre-fix original [] versus current [city] discrepancy",
    startNormalization,
    snapshots,
    auto: {
      lastTypePredicatePreserved: true,
      manualEditCallbackCount: autoConfigEdits.length,
    },
    sourceLocators,
    nativeActionHandlersInvoked: [],
    limitation: "Inert persistent-hook callback execution plus pure original/current start normalization; no scan or native action was invoked.",
  };
  fs.writeFileSync(path.join(here, "callback-results.json"), `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify({ marker: report.marker, snapshots: snapshots.length, autoConfigEdits: autoConfigEdits.length }));
} finally {
  await original.unmount();
  await current.unmount();
}
