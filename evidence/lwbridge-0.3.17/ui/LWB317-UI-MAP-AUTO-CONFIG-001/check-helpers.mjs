import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  loadIndex,
  loadPanel,
  rawOf,
  walkAll,
} from "../LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs";
import * as current from "../../../../src/LWBridge.UI-0.3.17/src/mapAutoConfig.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const baselinePath = path.join(here, "baseline-cf75b4e.MapDataPage.jsx");
const baselineSource = fs.readFileSync(baselinePath, "utf8");

function sha256(value) {
  return crypto.createHash("sha256").update(value).digest("hex");
}

function storageRuntime(initial = {}) {
  const values = new Map(Object.entries(initial).map(([key, value]) => [key, String(value)]));
  const writes = [];
  return {
    values,
    writes,
    api: {
      getItem(key) { return values.has(key) ? values.get(key) : null; },
      setItem(key, value) {
        values.set(key, String(value));
        writes.push([key, String(value)]);
      },
    },
  };
}

function originalRuntime(initial = {}) {
  const index = loadIndex();
  const rawIndex = (node) => rawOf(index.entry, node);
  const storage = storageRuntime(initial);
  const helperNames = ["Ci", "wi", "Ti", "Ei", "Di", "Oi", "ki", "Ai", "ji"];
  const body = [
    `var xi=${rawIndex(index.variables.xi.declarator.init)};`,
    `var Si=${rawIndex(index.variables.Si.declarator.init)};`,
    ...helperNames.map((name) => rawIndex(index.functions[name])),
    `return {xi,Si,${helperNames.join(",")}};`,
  ].join("\n");
  const helpers = new Function("localStorage", body)(storage.api);
  return { index, rawIndex, storage, ...helpers };
}

function originalParentApply(runtime, previous, candidate, now) {
  const gi = runtime.index.functions.Gi;
  const nodes = [];
  walkAll(gi, (node) => nodes.push(node));
  const ctNode = nodes.find((node) => node.type === "FunctionDeclaration" && node.id?.name === "Ct");
  assert.ok(ctNode, "original parent Ct");
  const Ce = { current: previous };
  let nextState = previous;
  const ye = (next) => { nextState = next; };
  const r = { selectedProfileId: "profile-a" };
  const fakeDate = { now: () => now };
  const Ct = new Function("Ce", "ye", "r", "Ei", "ji", "Date", `${runtime.rawIndex(ctNode)}\nreturn Ct;`)(
    Ce, ye, r, runtime.Ei, runtime.ji, fakeDate,
  );
  Ct(candidate);
  return nextState;
}

function originalDateFormatter() {
  const panel = loadPanel();
  return new Function(`${rawOf(panel.entry, panel.topLevel.N)}\nreturn N;`)();
}

function extractFunction(source, name) {
  const marker = `function ${name}(`;
  const start = source.indexOf(marker);
  assert.ok(start >= 0, `baseline function ${name}`);
  const brace = source.indexOf("{", start);
  let depth = 0;
  for (let index = brace; index < source.length; index += 1) {
    if (source[index] === "{") depth += 1;
    if (source[index] === "}") {
      depth -= 1;
      if (depth === 0) return source.slice(start, index + 1);
    }
  }
  throw new Error(`unterminated baseline function ${name}`);
}

const baselineNormalize = new Function("MAP_KIND_KEYS", "AUTO_DEFAULT_TYPES", `${extractFunction(baselineSource, "normalizeAutoConfig")}\nreturn normalizeAutoConfig;`)(
  ["city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"],
  ["truck", "railway", "dispatch", "ghost", "treasure"],
);
const baselineParserExpression = baselineSource.match(/const added = (autoServerInput\.split\([^;]+);/)?.[1];
assert.ok(baselineParserExpression, "baseline inline server parser expression");
const baselineParser = new Function("autoServerInput", `return ${baselineParserExpression}`);
const baselineDate = new Function(
  "document",
  `${extractFunction(baselineSource, "uiLocale")}\n${extractFunction(baselineSource, "dateText")}\nreturn dateText;`,
)({ documentElement: { lang: "en" } });

const oracle = originalRuntime();
const formatOriginalDate = originalDateFormatter();

const mixedInput = "8 15，120;121；122\n123,1e2 0x10 001 1.0 8 bad 0 -1 100000 1.5";
const capInput = Array.from({ length: 25 }, (_, index) => index + 1).join("；");
const stored = {
  enabled: "true",
  intervalMinutes: "1440.9",
  serverIds: ["8", "8", "1e2", "bad", 16],
  selectedTypes: ["treasure", "city", "treasure", "bad"],
  scanMode: "unexpected",
  returnToOriginalServer: 0,
  nextRunAt: "1234.9",
};

function sameValue(left, right) {
  if (Number.isNaN(left) && Number.isNaN(right)) return true;
  try { assert.deepEqual(left, right); return true; } catch { return false; }
}

function capture(label, expected, actual) {
  return { label, pass: sameValue(actual, expected), expected, actual };
}

function captureThrows(label, expectedThrows, fn) {
  let threw = false;
  let message = "";
  try { fn(); } catch (error) { threw = true; message = error?.message || String(error); }
  return { label, pass: threw === expectedThrows, expectedThrows, actualThrows: threw, message };
}

const originalDefaults = oracle.Ei(null);
const originalMixed = oracle.Ci(mixedInput);
const originalCap = oracle.Ci(capInput);
const originalAppend = oracle.wi([8, 15, 120], "15；121 0x10");
const originalRemove = oracle.Ti([8, 15, 120], 15);
const originalStored = oracle.Ei(stored);
const originalBadInterval = oracle.Ei({ intervalMinutes: "bad" });

const currentCases = [
  capture("defaults", originalDefaults, current.normalizeAutoScanConfig(null)),
  capture("parser-mixed-separators-and-number-forms", originalMixed, current.parseAutoServerIds(mixedInput)),
  capture("parser-cap-20", originalCap, current.parseAutoServerIds(capInput)),
  capture("append-first-occurrence-order", originalAppend, current.appendAutoServerIds([8, 15, 120], "15；121 0x10")),
  capture("remove-order", originalRemove, current.removeAutoServerId([8, 15, 120], 15)),
  capture("normalization-full-shape", originalStored, current.normalizeAutoScanConfig(stored)),
  capture("interval-low-clamp", oracle.Ei({ intervalMinutes: 19 }), current.normalizeAutoScanConfig({ intervalMinutes: 19 })),
  capture("interval-empty-clamp", oracle.Ei({ intervalMinutes: "" }), current.normalizeAutoScanConfig({ intervalMinutes: "" })),
  capture("interval-null-default", oracle.Ei({ intervalMinutes: null }), current.normalizeAutoScanConfig({ intervalMinutes: null })),
  { label: "interval-bad-preserves-nan", pass: Number.isNaN(current.normalizeAutoScanConfig({ intervalMinutes: "bad" }).intervalMinutes) && Number.isNaN(originalBadInterval.intervalMinutes), expected: "NaN", actual: Number.isNaN(current.normalizeAutoScanConfig({ intervalMinutes: "bad" }).intervalMinutes) ? "NaN" : current.normalizeAutoScanConfig({ intervalMinutes: "bad" }).intervalMinutes },
  captureThrows("structural-serverIds-throws", true, () => current.normalizeAutoScanConfig({ serverIds: "truthy-non-array" })),
  captureThrows("structural-selectedTypes-throws", true, () => current.normalizeAutoScanConfig({ selectedTypes: "truthy-non-array" })),
  capture("target-current-server-fallback", oracle.Di([], 321), current.autoScanTargetServers([], 321)),
  capture("target-empty-without-current", oracle.Di([], 0), current.autoScanTargetServers([], 0)),
  capture("deadline-advance", oracle.Oi({ ...originalDefaults, intervalMinutes: 20 }, 1_800_000_000_000), current.advanceAutoScanDeadline({ ...originalDefaults, intervalMinutes: 20 }, 1_800_000_000_000)),
  capture("admission-true", oracle.ki({ ...originalDefaults, enabled: true, nextRunAt: 100 }, 100, true, false, false), current.autoScanShouldRun({ ...originalDefaults, enabled: true, nextRunAt: 100 }, 100, true, false, false)),
  capture("admission-reading-false", oracle.ki({ ...originalDefaults, enabled: true, nextRunAt: 100 }, 100, true, false, true), current.autoScanShouldRun({ ...originalDefaults, enabled: true, nextRunAt: 100 }, 100, true, false, true)),
  capture("date-seconds", formatOriginalDate(1_234_567_890, "en"), current.formatAutoScanDate(1_234_567_890, "en")),
  capture("date-milliseconds", formatOriginalDate(1_800_000_000_000, "en"), current.formatAutoScanDate(1_800_000_000_000, "en")),
];

for (const [name, previous, candidate, now] of [
  ["transition-disabled-to-enabled", originalDefaults, { ...originalDefaults, enabled: true, nextRunAt: 777 }, 1_800_000_000_111],
  ["transition-enabled-edit-preserves-deadline", { ...originalDefaults, enabled: true, nextRunAt: 777 }, { ...originalDefaults, enabled: true, intervalMinutes: 2000, nextRunAt: 12345 }, 1_800_000_000_222],
  ["transition-disable-clears-deadline", { ...originalDefaults, enabled: true, nextRunAt: 777 }, { ...originalDefaults, enabled: false, nextRunAt: 99999 }, 1_800_000_000_333],
]) {
  currentCases.push(capture(name, originalParentApply(originalRuntime(), previous, candidate, now), current.applyAutoScanConfigEdit(previous, candidate, now)));
}

const loadInitial = {
  "lwbridge.mapAutoScan.p1": JSON.stringify({ ...originalDefaults, intervalMinutes: 19, serverIds: ["8", "8", "1e2"] }),
  "lwbridge.mapAutoScan.bad-json": "{",
  "lwbridge.mapAutoScan.bad-structure": JSON.stringify({ serverIds: "oops" }),
};
const originalLoad = originalRuntime(loadInitial);
const currentLoad = storageRuntime(loadInitial);
for (const profileId of ["p1", "missing", "bad-json", "bad-structure"]) {
  currentCases.push(capture(`load-${profileId}`, originalLoad.Ai(profileId), current.loadAutoScanConfig(profileId, currentLoad.api)));
}
currentCases.push(capture("load-does-not-write", [], currentLoad.writes));

const originalSave = originalRuntime();
const currentSave = storageRuntime();
originalSave.ji("profile-save", { ...originalDefaults, intervalMinutes: 19, serverIds: ["001", "1e2"] });
current.saveAutoScanConfig("profile-save", { ...originalDefaults, intervalMinutes: 19, serverIds: ["001", "1e2"] }, currentSave.api);
currentCases.push(capture("save-key-and-normalized-json", originalSave.storage.writes, currentSave.writes));
currentCases.push(capture("storage-key-no-default-suffix", "lwbridge.mapAutoScan.", current.autoScanStorageKey("")));

const baselineCases = [
  capture("defaults", originalDefaults, baselineNormalize(null)),
  capture("parser-mixed-separators-and-number-forms", originalMixed, baselineParser(mixedInput)),
  capture("parser-cap-20", originalCap, baselineParser(capInput).slice(0, 20)),
  capture("normalization-full-shape", originalStored, baselineNormalize(stored)),
  capture("interval-low-clamp", oracle.Ei({ intervalMinutes: 19 }), baselineNormalize({ intervalMinutes: 19 })),
  capture("interval-empty-clamp", oracle.Ei({ intervalMinutes: "" }), baselineNormalize({ intervalMinutes: "" })),
  { label: "interval-bad-preserves-nan", pass: Number.isNaN(baselineNormalize({ intervalMinutes: "bad" }).intervalMinutes) && Number.isNaN(originalBadInterval.intervalMinutes), expected: "NaN", actual: baselineNormalize({ intervalMinutes: "bad" }).intervalMinutes },
  captureThrows("structural-serverIds-throws", true, () => baselineNormalize({ serverIds: "truthy-non-array" })),
  captureThrows("structural-selectedTypes-throws", true, () => baselineNormalize({ selectedTypes: "truthy-non-array" })),
  capture("date-seconds", formatOriginalDate(1_234_567_890, "en"), baselineDate(1_234_567_890)),
  { label: "profile-key-has-no-default-fallback", pass: !baselineSource.includes('mapApi.profileId || "default"'), expected: "exact selected profile suffix", actual: baselineSource.includes('mapApi.profileId || "default"') ? "default fallback present" : "no fallback" },
  { label: "profile-load-precedes-save", pass: !baselineSource.includes("window.localStorage.setItem(autoStorageKey, JSON.stringify(autoConfig))"), expected: "parent layout load before user-save path", actual: "Map page save effect writes current in-memory config whenever storage key changes" },
];

const originalMeta = {
  indexSha256: oracle.index.entry.sha256,
  panelSha256: loadPanel().entry.sha256,
};
const baselineMeta = {
  commit: "cf75b4e3cd8b602c17cef9a244385e6db7c02daf",
  gitBlob: "e429778ed2d7555676ed2844a5e204f8c6723f47",
  sha256: sha256(fs.readFileSync(baselinePath)),
};

function report(kind, cases) {
  return {
    kind,
    original: originalMeta,
    baseline: baselineMeta,
    passed: cases.filter((entry) => entry.pass).length,
    failed: cases.filter((entry) => !entry.pass).length,
    cases,
  };
}

const baselineReport = report("immutable-cf75b4e-baseline-vs-original", baselineCases);
const currentReport = report("current-canonical-helpers-vs-original", currentCases);
fs.writeFileSync(path.join(here, "baseline-results.json"), `${JSON.stringify(baselineReport, null, 2)}\n`);
fs.writeFileSync(path.join(here, "current-helper-results.json"), `${JSON.stringify(currentReport, null, 2)}\n`);

assert.ok(baselineReport.failed > 0, "baseline must retain distinguishing failures");
assert.equal(currentReport.failed, 0, `current helper mismatches: ${currentReport.cases.filter((entry) => !entry.pass).map((entry) => entry.label).join(", ")}`);
console.log(`PASS current ${currentReport.passed}/${currentReport.cases.length}; retained baseline failures ${baselineReport.failed}/${baselineReport.cases.length}`);
