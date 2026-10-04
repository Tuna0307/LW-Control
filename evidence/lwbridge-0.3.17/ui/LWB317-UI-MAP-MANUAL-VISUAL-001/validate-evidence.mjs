import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const read = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const manifest = read("integrity-manifest.json");
for (const item of [...manifest.dependencies, ...manifest.evidence]) {
  const bytes = fs.readFileSync(path.join(repo, item.path));
  assert.equal(bytes.length, item.bytes, `${item.path}: length`);
  assert.equal(hash(bytes), item.sha256, `${item.path}: hash`);
}
const inputs = read("pinned-inputs.json");
assert.equal(hash(fs.readFileSync(inputs.reference.exe.path)), inputs.reference.exe.sha256);
for (const entry of [inputs.reference.panel, inputs.reference.css, ...inputs.current.files, ...Object.values(inputs.current.locales)]) {
  assert.equal(hash(fs.readFileSync(path.join(repo, entry.path))), entry.sha256, entry.path);
}
let locatorCount = 0;
for (const locator of [...inputs.reference.locators, ...inputs.current.locators]) {
  const bytes = fs.readFileSync(path.join(repo, locator.file)).subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.byteLength);
  assert.equal(bytes.length, locator.byteLength);
  assert.equal(hash(bytes), locator.sha256, locator.id || locator.name);
  if (locator.text) assert.equal(bytes.toString("utf8"), locator.text);
  locatorCount++;
}
const results = read("render-results.json");
assert.equal(results.coreResults.length, 12);
assert.equal(results.providerFenceResults.length, 3);
function differences(left, right, at = "$", output = []) {
  if (Object.is(left, right)) return output;
  if (Array.isArray(left) && Array.isArray(right)) {
    for (let index = 0; index < Math.max(left.length, right.length); index++) differences(left[index], right[index], `${at}[${index}]`, output);
  } else if (left && right && typeof left === "object" && typeof right === "object" && !Array.isArray(left) && !Array.isArray(right)) {
    for (const key of new Set([...Object.keys(left), ...Object.keys(right)])) differences(left[key], right[key], `${at}.${key}`, output);
  } else output.push({ path: at, original: left, current: right });
  return output;
}
const families = ["idle-unavailable", "idle-server", "reading-low", "reading-high", "completed-timing", "error"];
assert.deepEqual(results.coreResults.map((result) => `${result.language}/${result.caseId}`).sort(), ["en", "ja"].flatMap((language) => families.map((id) => `${language}/${id}`)).sort());
for (const result of [...results.coreResults, ...results.providerFenceResults]) {
  const raw = (side) => fs.readFileSync(path.join(repo, result.rawFiles[side]), "utf8").replace(/\n$/, "");
  assert.equal(hash(Buffer.from(raw("original"))), result.originalMarkupSha256);
  assert.equal(hash(Buffer.from(raw("current"))), result.currentMarkupSha256);
  assert.equal(raw("original") === raw("current"), result.exactMarkupMatch);
  assert.equal(result.differences.length === 0, result.exactViewMatch);
  // Match the recorded JSON representation: missing array positions produce
  // undefined values, which JSON omits from difference objects. Keep their path.
  assert.deepEqual(result.differences, JSON.parse(JSON.stringify(differences(result.originalView, result.currentView))));
}
const measurements = read("browser/measurements.json");
const comparisons = read("browser/comparison-results.json");
const inspection = read("browser/inspection.json");
const consoleRecords = read("browser/console.json");
assert.equal(measurements.records.length, 8);
assert.equal(comparisons.pairs.length, 4);
assert.equal(consoleRecords.length, 8);
assert.ok(consoleRecords.every((record) => record.issues.length === 0));
for (const pair of comparisons.pairs) {
  const records = measurements.records.filter((record) => record.pairId === pair.pairId);
  assert.equal(records.length, 2);
  for (const record of records) {
    const image = fs.readFileSync(path.join(repo, record.screenshot));
    assert.equal(hash(image), record.screenshotSha256);
    assert.equal(image.length, record.screenshotBytes);
    assert.equal(image.readUInt32BE(16), record.viewport.width);
    assert.equal(image.readUInt32BE(20), record.viewport.height);
    assert.equal(record.measurement.title, `${record.pairId} ${record.side}`);
    assert.equal(record.measurement.language, pair.language);
    assert.equal(record.measurement.theme, pair.theme);
    assert.equal(record.measurement.viewport.innerWidth, pair.viewport.width);
    assert.equal(record.measurement.viewport.innerHeight, pair.viewport.height);
    assert.equal(record.measurement.viewport.devicePixelRatio, 1);
    assert.equal(record.measurement.timezone, "Asia/Singapore");
    assert.equal(record.consoleIssueCount, 0);
  }
  const original = records.find((record) => record.side === "original");
  const current = records.find((record) => record.side === "current");
  assert.equal(pair.exactScreenshotBytes, original.screenshotSha256 === current.screenshotSha256);
  assert.equal(pair.domMatch, JSON.stringify(original.measurement.dom) === JSON.stringify(current.measurement.dom));
  const geometry = [];
  const styles = [];
  for (const name of new Set([...Object.keys(original.measurement.anchors), ...Object.keys(current.measurement.anchors)])) {
    const left = original.measurement.anchors[name];
    const right = current.measurement.anchors[name];
    if (!left || !right) {
      if (left || right) geometry.push({ anchor: name, original: left ? "present" : null, current: right ? "present" : null });
      continue;
    }
    for (const key of ["x", "y", "width", "height"]) if (left.rect[key] !== right.rect[key]) geometry.push({ anchor: name, property: key, original: left.rect[key], current: right.rect[key] });
    for (const key of Object.keys(left.styles)) if (left.styles[key] !== right.styles[key]) styles.push({ anchor: name, property: key, original: left.styles[key], current: right.styles[key] });
    for (const key of Object.keys(left.before)) if (left.before[key] !== right.before[key]) styles.push({ anchor: `${name}::before`, property: key, original: left.before[key], current: right.before[key] });
  }
  assert.deepEqual(pair.geometryDifferences, geometry);
  assert.deepEqual(pair.styleDifferences, styles);
  assert.ok(inspection.pairs.some((record) => record.pairId === pair.pairId && record.status === "INSPECTED"));
}
const findings = read("findings.json");
assert.equal(findings.browserSummary.pairs, comparisons.pairs.length);
assert.equal(findings.browserSummary.exactScreenshotPairs, comparisons.pairs.filter((pair) => pair.exactScreenshotBytes).length);
assert.equal(findings.browserSummary.geometryDifferences, comparisons.pairs.reduce((count, pair) => count + pair.geometryDifferences.length, 0));
assert.equal(findings.browserSummary.computedStyleDifferences, comparisons.pairs.reduce((count, pair) => count + pair.styleDifferences.length, 0));
const counter = read("selection-counter-evidence.json");
assert.deepEqual(counter.initial, { originalDisabled: false, currentDisabled: true });
assert.deepEqual(counter.after, { original: [], current: ["city"] });
assert.deepEqual(counter.nativeActionHandlersInvoked, []);
for (const locator of counter.sourceLocators) {
  const bytes = fs.readFileSync(path.join(repo, locator.file)).subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.byteLength);
  assert.equal(hash(bytes), locator.sha256, locator.name);
  assert.equal(bytes.toString("utf8"), locator.text);
  locatorCount++;
}
console.log(JSON.stringify({ marker: "LWB317_MAP_MANUAL_VISUAL_EVIDENCE_OK", dependencies: manifest.dependencies.length, evidence: manifest.evidence.length, locators: locatorCount, coreCases: results.coreResults.length, providerCases: results.providerFenceResults.length, screenshotPairs: comparisons.pairs.length, exactScreenshotPairs: findings.browserSummary.exactScreenshotPairs, parityClaim: "Comparison packet complete; recorded implementation differences remain." }));
