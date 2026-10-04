import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const hashBytes = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const hashFile = (file) => hashBytes(fs.readFileSync(file));
const fromRepo = (relative) => path.join(repo, relative);

const pinned = readJson("pinned-inputs.json");
assert.equal(pinned.task, "LWB317-UI-MAP-AUTO-VISUAL-001");
assert.equal(pinned.evidenceState, "EXACT_BYTES / EXACT_CONTRACT");
assert.equal(hashFile(pinned.reference.exe.path), pinned.reference.exe.sha256, "EXE pin");
for (const key of ["panel", "index", "css"]) {
  const item = pinned.reference[key];
  assert.equal(hashFile(fromRepo(item.path)), item.sha256, `reference ${key} pin`);
}
assert.equal(hashFile(fromRepo(pinned.reference.acceptedConfigLocators.path)), pinned.reference.acceptedConfigLocators.sha256, "accepted config locator pin");
for (const item of [...pinned.current.files, ...Object.values(pinned.current.locales), ...pinned.dependencies]) {
  assert.equal(hashFile(fromRepo(item.path)), item.sha256, `current/dependency pin ${item.path}`);
}
assert.equal(pinned.current.referenceCssExactlyMatchesOriginal, true);
assert.deepEqual(pinned.current.cssImportOrder, ["./reference.css", "./styles.css"]);

const panelBytes = fs.readFileSync(fromRepo(pinned.reference.autoCard.asset));
const originalCard = panelBytes.subarray(pinned.reference.autoCard.byteStart, pinned.reference.autoCard.byteEnd);
assert.equal(hashBytes(originalCard), pinned.reference.autoCard.sliceSha256, "original card slice hash");
assert.equal(originalCard.toString("utf8"), pinned.reference.autoCard.source, "original card slice bytes");

function verifyUtf8Locator(locator) {
  const bytes = fs.readFileSync(fromRepo(locator.file));
  const slice = bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.byteLength);
  assert.equal(hashBytes(slice), locator.sha256, `source locator ${locator.id}`);
  if (locator.text !== undefined) assert.equal(slice.toString("utf8"), locator.text, `source locator text ${locator.id}`);
}
verifyUtf8Locator(pinned.reference.removeIconUse);
verifyUtf8Locator(pinned.reference.removeIconComponent);
verifyUtf8Locator(pinned.current.autoCard);
verifyUtf8Locator(pinned.current.removeIconChild);

assert.equal(pinned.configValidation.currentDefaultEqualsRecoveredDefault, true);
assert.equal(pinned.configValidation.configuredNormalizesUnchanged, true);
assert.deepEqual(pinned.configs.default, {
  enabled: false,
  intervalMinutes: 60,
  serverIds: [],
  selectedTypes: ["truck", "railway", "dispatch", "ghost", "treasure"],
  scanMode: "fast",
  returnToOriginalServer: true,
  nextRunAt: 0,
});
assert.deepEqual(pinned.configs.configured, {
  enabled: true,
  intervalMinutes: 90,
  serverIds: [8, 15, 120],
  selectedTypes: ["city", "truck", "treasure"],
  scanMode: "normal",
  returnToOriginalServer: false,
  nextRunAt: 1791110700000,
});

const render = readJson("render-results.json");
assert.equal(render.marker, "LWB317_MAP_AUTO_VISUAL_RENDER_COMPARED");
assert.equal(render.pairs.length, 2);
assert.deepEqual(render.browserPairs.map((pair) => pair.id), ["en-light-default", "ja-dark-configured"]);
const defaultPair = render.pairs.find((pair) => pair.id === "default");
const configuredPair = render.pairs.find((pair) => pair.id === "configured");
assert.ok(defaultPair && configuredPair);
assert.equal(defaultPair.enteredAutoViaActualTabCallback, true);
assert.equal(configuredPair.enteredAutoViaActualTabCallback, true);
assert.equal(defaultPair.exactViewMatch, true);
assert.equal(defaultPair.exactMarkupMatch, true);
assert.deepEqual(defaultPair.differences, []);
assert.equal(configuredPair.exactViewMatch, false);
assert.equal(configuredPair.exactMarkupMatch, false);
const expectedDifferencePaths = [
  ...[0, 1, 2].flatMap((index) => [
    `$.server.chips[${index}].removeChildTag`,
    `$.server.chips[${index}].removeChildClassName`,
    `$.server.chips[${index}].removeChildText`,
    `$.server.chips[${index}].removePathD`,
  ]),
  "$.nativeTagCount",
];
assert.deepEqual(configuredPair.differences.map((item) => item.path), expectedDifferencePaths);
assert.ok(configuredPair.originalView.server.chips.every((chip) => chip.removeChildTag === "svg" && chip.removeChildClassName === "ui-icon" && chip.removePathD === "m4 4 8 8M12 4l-8 8"));
assert.ok(configuredPair.currentView.server.chips.every((chip) => chip.removeChildTag === "span" && chip.removeChildText === "×" && chip.removePathD === null));
for (const pair of render.pairs) {
  for (const side of ["original", "current"]) {
    const raw = fs.readFileSync(fromRepo(pair.rawFiles[side]), "utf8").replace(/\r?\n$/, "");
    assert.equal(hashBytes(Buffer.from(raw, "utf8")), pair[`${side}MarkupSha256`], `${pair.id}/${side} raw markup`);
  }
}

const measurements = readJson("browser/measurements.json");
const browser = readJson("browser/comparison-results.json");
const consoleEntries = readJson("browser/console.json");
const inspection = readJson("browser/inspection.json");
assert.equal(measurements.marker, "LWB317_MAP_AUTO_VISUAL_BROWSER_MEASURED");
assert.equal(browser.marker, "LWB317_MAP_AUTO_VISUAL_BROWSER_COMPARED");
assert.equal(measurements.records.length, 4);
assert.equal(browser.pairs.length, 2);
assert.equal(consoleEntries.length, 4);
assert.ok(consoleEntries.every((entry) => entry.issues.length === 0));
assert.equal(inspection.marker, "LWB317_MAP_AUTO_VISUAL_SCREENSHOTS_INSPECTED");
assert.equal(inspection.screenshots.length, 4);
assert.ok(inspection.screenshots.every((entry) => entry.status === "INSPECTED"));

for (const record of measurements.records) {
  assert.equal(record.consoleIssueCount, 0, `${record.pairId}/${record.side} console issues`);
  assert.equal(hashFile(fromRepo(record.screenshot)), record.screenshotSha256, `${record.pairId}/${record.side} screenshot hash`);
  const inspected = inspection.screenshots.find((entry) => fromRepo(`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-VISUAL-001/${entry.file}`) === fromRepo(record.screenshot));
  assert.ok(inspected, `${record.pairId}/${record.side} inspected screenshot`);
  assert.equal(inspected.sha256, record.screenshotSha256);
}

const defaultBrowser = browser.pairs.find((pair) => pair.pairId === "en-light-default");
const configuredBrowser = browser.pairs.find((pair) => pair.pairId === "ja-dark-configured");
assert.ok(defaultBrowser && configuredBrowser);
assert.equal(defaultBrowser.exactScreenshotBytes, true);
assert.equal(defaultBrowser.domMatch, true);
assert.deepEqual(defaultBrowser.geometryDifferences, []);
assert.deepEqual(defaultBrowser.styleDifferences, []);
assert.equal(configuredBrowser.exactScreenshotBytes, false);
assert.equal(configuredBrowser.domMatch, false);
assert.equal(configuredBrowser.geometryDifferences.length, 12);
assert.equal(configuredBrowser.styleDifferences.length, 9);
assert.ok(configuredBrowser.geometryDifferences.every((item) => /^serverChipChild[0-2]$/.test(item.anchor)));
assert.ok(configuredBrowser.styleDifferences.every((item) => /^serverChipChild[0-2]$/.test(item.anchor)));

function record(pairId, side) {
  const found = measurements.records.find((item) => item.pairId === pairId && item.side === side);
  assert.ok(found, `${pairId}/${side} measurement record`);
  return found;
}
const defaultOriginal = record("en-light-default", "original");
const defaultCurrent = record("en-light-default", "current");
assert.deepEqual(defaultOriginal.measurement.anchors, defaultCurrent.measurement.anchors, "default measured anchors");
const configuredOriginal = record("ja-dark-configured", "original");
const configuredCurrent = record("ja-dark-configured", "current");
for (const [name, originalAnchor] of Object.entries(configuredOriginal.measurement.anchors)) {
  if (/^serverChipChild[0-2]$/.test(name)) continue;
  const currentAnchor = configuredCurrent.measurement.anchors[name];
  assert.ok(currentAnchor, `configured current anchor ${name}`);
  assert.equal(originalAnchor.tag, currentAnchor.tag, `configured non-glyph tag ${name}`);
  assert.deepEqual(originalAnchor.attrs, currentAnchor.attrs, `configured non-glyph attrs ${name}`);
  assert.deepEqual(originalAnchor.rect, currentAnchor.rect, `configured non-glyph rect ${name}`);
  assert.deepEqual(originalAnchor.styles, currentAnchor.styles, `configured non-glyph styles ${name}`);
  assert.deepEqual(originalAnchor.before, currentAnchor.before, `configured non-glyph pseudo-style ${name}`);
}
assert.deepEqual(configuredOriginal.measurement.anchors.card.rect, { x: 17, y: 17, width: 341, height: 650 });
assert.deepEqual(configuredCurrent.measurement.anchors.card.rect, { x: 17, y: 17, width: 341, height: 650 });
assert.deepEqual(defaultOriginal.measurement.anchors.card.rect, { x: 17, y: 17, width: 1246, height: 314 });

console.log("LWB317_MAP_AUTO_VISUAL_EVIDENCE_OK pairs=2 screenshots=4 defaultPixelExact=1 configuredGlyphGeometry=12 configuredGlyphStyles=9 consoleIssues=0");
