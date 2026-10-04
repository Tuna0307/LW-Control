import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const taskRoot = path.dirname(here);
const repo = path.resolve(here, "../../../../..");
const json = (file) => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));
const taskJson = (file) => JSON.parse(fs.readFileSync(path.join(taskRoot, file), "utf8"));
const sha = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const shaFile = (file) => sha(fs.readFileSync(file));

const pairs = json("browser-pairs.json");
const measurements = json("browser/measurements.json");
const comparisons = json("browser/comparison-results.json");
const consoleRows = json("browser/console.json");
const inspection = json("browser/inspection.json");
const matrix = json("difference-matrix.json");
const regressions = json("regression-results.json");
const current = taskJson("milestone-b/current-results.json");
const correction = taskJson("milestone-b/correction-results.json");
const interactions = taskJson("milestone-b/browser-interactions.json");
const locales = taskJson("milestone-b/locale-inventory.json");

assert.equal(pairs.marker, "LWB317_MAP_TOOLBAR_BROWSER_PAIRS_PREPARED");
assert.equal(current.marker, "LWB317_MAP_TOOLBAR_VISUAL_CURRENT_EXECUTED");
assert.equal(current.requiredCaseCount, 34);
assert.equal(current.supplementalCaseCount, 12);
assert.equal(correction.marker, "LWB317_MAP_TOOLBAR_CORRECTIONS_OK");
assert.equal(interactions.marker, "LWB317_MAP_TOOLBAR_BROWSER_INTERACTIONS_OK");
assert.equal(interactions.cases.length, 34);
assert.equal(interactions.console.filter((entry) => entry.type === "error" || entry.type === "pageerror").length, 0);
assert.equal(locales.marker, "LWB317_MAP_TOOLBAR_LOCALE_INVENTORY_OK");
assert.equal(locales.assignedKeyCount, 45);
assert.equal(locales.locales.length, 9);
for (const locale of locales.locales) {
  assert.equal(locale.keyCount, 45, `${locale.language}: toolbar key count`);
  assert.equal(locale.ownCount, 45, `${locale.language}: direct toolbar keys`);
  assert.equal(locale.fallbackCount, 0, `${locale.language}: fallback toolbar keys`);
}

const expectedPairs = [
  ["city-en-light-desktop", "city", "en", "light", 1280, 720, true, 0],
  ["city-ja-dark-375", "city", "ja", "dark", 375, 1000, true, 0],
  ["truck-en-dark-desktop", "truck", "en", "dark", 1280, 720, false, 8],
  ["dispatch-ja-light-375", "dispatch", "ja", "light", 375, 1000, false, 16],
  ["treasure-en-light-desktop", "treasure", "en", "light", 1280, 720, false, 16],
  ["scheduled-ja-dark-375", "scheduledPlunder", "ja", "dark", 375, 1000, true, 0],
];
assert.equal(pairs.pairs.length, 6);
assert.deepEqual(pairs.pairs.map((row) => [row.id, row.tab, row.language, row.theme, row.width, row.height]), expectedPairs.map((row) => row.slice(0, 6)));

for (const [label, pin] of Object.entries(pairs.production)) {
  const file = path.join(repo, pin.path);
  assert.equal(shaFile(file), pin.sha256, `production pin ${label}`);
}
for (const pair of pairs.pairs) {
  for (const side of ["original", "current"]) {
    for (const group of ["sourceFiles", "rawFiles", "generatedFiles"]) {
      const pin = pair[group][side];
      assert.equal(shaFile(path.join(repo, pin.path)), pin.sha256, `${pair.id}/${side}/${group}`);
    }
  }
}

assert.equal(measurements.marker, "LWB317_MAP_TOOLBAR_BROWSER_MEASURED");
assert.equal(measurements.records.length, 12);
assert.equal(measurements.environment.combinedCssSha256, pairs.fixedEnvironment.combinedCssSha256);
for (const record of measurements.records) {
  const screenshot = fs.readFileSync(path.join(repo, record.screenshot));
  assert.equal(sha(screenshot), record.screenshotSha256, `${record.pairId}/${record.side}: screenshot hash`);
  assert.equal(screenshot.length, record.screenshotBytes, `${record.pairId}/${record.side}: screenshot size`);
  assert.equal(screenshot.readUInt32BE(16), record.viewport.width, `${record.pairId}/${record.side}: PNG width`);
  assert.equal(screenshot.readUInt32BE(20), record.viewport.height, `${record.pairId}/${record.side}: PNG height`);
  assert.equal(record.measurement.fontsStatus, "loaded", `${record.pairId}/${record.side}: fonts`);
  assert.equal(record.measurement.timezone, "Asia/Singapore", `${record.pairId}/${record.side}: timezone`);
  assert.equal(record.measurement.viewport.devicePixelRatio, 1, `${record.pairId}/${record.side}: DPR`);
  assert.equal(record.consoleIssueCount, 0, `${record.pairId}/${record.side}: console`);
}

assert.equal(comparisons.marker, "LWB317_MAP_TOOLBAR_BROWSER_COMPARED");
assert.equal(comparisons.pairs.length, 6);
for (const expected of expectedPairs) {
  const [id, , , , , , exactScreenshot, styleCount] = expected;
  const row = comparisons.pairs.find((item) => item.pairId === id);
  assert.ok(row, id);
  assert.equal(row.geometryDifferences.length, 0, `${id}: geometry`);
  assert.equal(row.styleDifferences.length, styleCount, `${id}: classified styles`);
  assert.equal(row.exactScreenshotBytes, exactScreenshot, `${id}: screenshot equivalence`);
}
assert.equal(consoleRows.length, 12);
assert.ok(consoleRows.every((row) => row.issues.length === 0), "zero browser console/page issues");

assert.equal(inspection.marker, "LWB317_MAP_TOOLBAR_SCREENSHOTS_INSPECTED");
assert.equal(inspection.records.length, 12);
assert.ok(inspection.records.every((row) => row.status === "INSPECTED"));
for (const row of inspection.records) assert.equal(shaFile(path.join(repo, row.file)), row.sha256, `${row.pairId}/${row.side}: inspected image unchanged`);

assert.equal(matrix.marker, "LWB317_MAP_TOOLBAR_DIFFERENCES_CLASSIFIED");
assert.equal(matrix.unresolvedInScopePresentationDefects, 0);
assert.equal(matrix.pairs.length, 6);
assert.equal(regressions.marker, "LWB317_MAP_TOOLBAR_REGRESSIONS_REPLAYED");
assert.equal(regressions.results.length, 9);
assert.ok(regressions.results.every((row) => row.exitCode === 0), "all affected regressions pass");

const tableReplay = JSON.parse(fs.readFileSync(path.join(here, "replays/table-current/render-results.json"), "utf8"));
assert.equal(tableReplay.marker, "LWB317_MAP_TABLE_OFFLINE_REFERENCE_BUILT");
assert.equal(tableReplay.cases, 48);
assert.equal(tableReplay.matched, 48);
assert.ok(tableReplay.records.every((row) => row.differences.length === 0));

console.log(JSON.stringify({
  marker: "LWB317_MAP_TOOLBAR_VISUAL_EVIDENCE_OK",
  rendererStates: current.requiredCaseCount,
  supplementalStates: current.supplementalCaseCount,
  localInteractions: interactions.cases.length,
  locales: locales.locales.length,
  browserPairs: comparisons.pairs.length,
  screenshotsInspected: inspection.records.length,
  affectedRegressions: regressions.results.length,
  unresolvedInScopePresentationDefects: matrix.unresolvedInScopePresentationDefects,
}));
