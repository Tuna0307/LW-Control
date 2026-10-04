import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readJson = (relative) => JSON.parse(fs.readFileSync(path.join(here, relative), "utf8"));
const fromRepo = (relative) => path.join(repo, relative);
const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));

function verifyLocator(locator) {
  const bytes = fs.readFileSync(fromRepo(locator.file));
  const slice = bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.byteLength);
  assert.equal(hashBytes(slice), locator.sha256, `locator hash ${locator.id}`);
  if (locator.text !== undefined) assert.equal(slice.toString("utf8"), locator.text, `locator text ${locator.id}`);
}

const pinned = readJson("pinned-inputs.json");
assert.equal(pinned.task, "LWB317-UI-MAP-AUTO-REMOVE-001");
assert.equal(pinned.milestone, "C");
assert.equal(hashFile(pinned.reference.exe.path), pinned.reference.exe.sha256, "EXE pin");
for (const key of ["panel", "index", "css"]) assert.equal(hashFile(fromRepo(pinned.reference[key].path)), pinned.reference[key].sha256, `reference ${key}`);
for (const item of [...pinned.current.files, ...Object.values(pinned.current.locales), ...pinned.dependencies]) {
  assert.equal(hashFile(fromRepo(item.path)), item.sha256, `current/dependency pin ${item.path}`);
}
for (const parent of Object.values(pinned.parentEvidence)) assert.equal(hashFile(fromRepo(parent.path)), parent.sha256, `parent evidence ${parent.path}`);
for (const locator of [
  pinned.reference.removeIconUse,
  pinned.reference.removeIconComponent,
  pinned.reference.typeList,
  pinned.reference.lastTypePredicate,
  pinned.reference.runNowPredicate,
  pinned.current.autoCard,
  pinned.current.removeIconChild,
  pinned.current.emitAutoConfig,
  pinned.current.addAutoServers,
  pinned.current.toggleAutoType,
  pinned.current.lastTypePredicate,
  pinned.current.runNowPredicate,
]) verifyLocator(locator);
assert.equal(pinned.css.referenceCssExactlyMatchesOriginal, true);
assert.equal(hashFile(fromRepo(pinned.css.original.path)), pinned.css.original.sha256);

const expected = [
  ["en-light-default", "default", "en", "light", 1280, 720],
  ["ja-dark-configured", "configured-waiting", "ja", "dark", 375, 1000],
  ["ja-light-running", "auto-running", "ja", "light", 375, 1000],
  ["en-dark-offline", "offline", "en", "dark", 1280, 720],
];
assert.equal(pinned.pairs.length, 4);
for (const [id, caseId, language, theme, width, height] of expected) {
  const pair = pinned.pairs.find((item) => item.id === id);
  assert.ok(pair, `${id}: pinned pair`);
  assert.equal(pair.caseId, caseId);
  assert.equal(pair.language, language);
  assert.equal(pair.theme, theme);
  assert.deepEqual(pair.viewport, { width, height, deviceScaleFactor: 1 });
  assert.equal(pair.raw.exactBytes, true);
  assert.deepEqual(pair.raw.differences, []);
  const originalRaw = fs.readFileSync(fromRepo(pair.rawFiles.original), "utf8").replace(/\r?\n$/, "");
  const currentRaw = fs.readFileSync(fromRepo(pair.rawFiles.current), "utf8").replace(/\r?\n$/, "");
  assert.equal(hashBytes(Buffer.from(originalRaw, "utf8")), pair.raw.originalSha256, `${id}: original raw hash`);
  assert.equal(hashBytes(Buffer.from(currentRaw, "utf8")), pair.raw.currentSha256, `${id}: current raw hash`);
  assert.equal(originalRaw, currentRaw, `${id}: raw DOM/markup exact`);
  assert.ok(fs.existsSync(fromRepo(pair.generatedFiles.original)), `${id}: original browser document`);
  assert.ok(fs.existsSync(fromRepo(pair.generatedFiles.current)), `${id}: current browser document`);
}

const raw = readJson("raw-comparison.json");
const manifest = readJson("browser-pairs.json");
assert.equal(raw.marker, "LWB317_MAP_AUTO_REMOVE_C_RAW_COMPARED");
assert.equal(raw.pairs.length, 4);
assert.ok(raw.pairs.every((pair) => pair.raw.exactBytes && pair.raw.differences.length === 0));
assert.equal(manifest.marker, "LWB317_MAP_AUTO_REMOVE_C_BROWSER_PAIRS_PREPARED");
assert.equal(manifest.pairs.length, 4);

const measurements = readJson("browser/measurements.json");
const comparisons = readJson("browser/comparison-results.json");
const consoleEntries = readJson("browser/console.json");
const inspection = readJson("browser/inspection.json");
assert.equal(measurements.marker, "LWB317_MAP_AUTO_REMOVE_C_BROWSER_MEASURED");
assert.equal(comparisons.marker, "LWB317_MAP_AUTO_REMOVE_C_BROWSER_COMPARED");
assert.equal(measurements.environment.serverPort, 4344);
assert.equal(measurements.environment.debugPort, 4345);
assert.notEqual(measurements.environment.serverPort, 4335, "owner port preserved");
assert.notEqual(measurements.environment.debugPort, 4335, "owner port preserved");
assert.equal(measurements.records.length, 8);
assert.equal(comparisons.pairs.length, 4);
assert.equal(consoleEntries.length, 8);
assert.ok(consoleEntries.every((entry) => entry.issues.length === 0), "browser console clean");

for (const pair of comparisons.pairs) {
  assert.equal(pair.exactScreenshotBytes, true, `${pair.pairId}: screenshot bytes`);
  assert.equal(pair.domMatch, true, `${pair.pairId}: browser DOM`);
  assert.deepEqual(pair.geometryDifferences, [], `${pair.pairId}: geometry`);
  assert.deepEqual(pair.styleDifferences, [], `${pair.pairId}: styles`);
  assert.equal(pair.originalScreenshotSha256, pair.currentScreenshotSha256, `${pair.pairId}: screenshot hash equality`);
  const records = measurements.records.filter((record) => record.pairId === pair.pairId);
  assert.equal(records.length, 2, `${pair.pairId}: two browser sides`);
  for (const record of records) {
    assert.equal(record.consoleIssueCount, 0, `${pair.pairId}/${record.side}: console`);
    assert.equal(hashFile(fromRepo(record.screenshot)), record.screenshotSha256, `${pair.pairId}/${record.side}: PNG hash`);
    assert.ok(record.measurement?.anchors?.card?.rect?.width > 0, `${pair.pairId}/${record.side}: card rectangle measured`);
    assert.ok(Object.keys(record.measurement?.anchors?.card?.styles || {}).length > 0, `${pair.pairId}/${record.side}: card styles measured`);
  }
}

assert.equal(inspection.marker, "LWB317_MAP_AUTO_REMOVE_C_SCREENSHOTS_INSPECTED");
assert.equal(inspection.records.length, 8);
for (const entry of inspection.records) {
  assert.equal(entry.status, "INSPECTED");
  assert.equal(hashFile(path.join(here, entry.file)), entry.sha256, `${entry.pairId}/${entry.side}: inspected PNG hash`);
  const pair = comparisons.pairs.find((item) => item.pairId === entry.pairId);
  assert.ok(pair, `${entry.pairId}: inspection pair exists`);
  assert.equal(entry.sha256, pair[`${entry.side}ScreenshotSha256`]);
}

console.log("LWB317_MAP_AUTO_REMOVE_C_EVIDENCE_OK pairs=4 screenshots=8 exactScreenshots=4 rawDifferences=0 geometry=0 styles=0 consoleIssues=0 inspected=8");
