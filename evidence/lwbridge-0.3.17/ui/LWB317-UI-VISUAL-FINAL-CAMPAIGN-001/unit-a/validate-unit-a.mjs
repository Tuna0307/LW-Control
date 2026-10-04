import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readText = (file) => fs.readFileSync(file, "utf8").replace(/^\uFEFF/, "");
const readJson = (file) => JSON.parse(readText(file));
const hashFile = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");

const pageFile = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const page = readText(pageFile);
assert.ok(page.includes('/>{t("map.showForeignRadarTreasures")}</label>'), "foreign Treasure label uses direct text");
assert.ok(page.includes('/>{t("map.prioritizeLuckyTreasures")}</label>'), "lucky Treasure label uses direct text");
assert.ok(!page.includes('<span>{t("map.showForeignRadarTreasures")}</span>'), "foreign Treasure label span removed");
assert.ok(!page.includes('<span>{t("map.prioritizeLuckyTreasures")}</span>'), "lucky Treasure label span removed");

const renderer = readJson(path.join(here, "current-results.json"));
assert.equal(renderer.requiredCaseCount, 34, "34 required toolbar renderer cases");
assert.equal(renderer.supplementalCaseCount, 12, "12 supplemental toolbar renderer cases");
const corrections = readJson(path.join(here, "correction-results.json"));
assert.equal(corrections.marker, "LWB317_VISUAL_FINAL_UNIT_A_CORRECTION_OK");

const measurements = readJson(path.join(here, "browser/measurements.json"));
assert.equal(measurements.marker, "LWB317_VISUAL_FINAL_UNIT_A_BROWSER_MEASURED");
assert.equal(measurements.records.length, 8, "four original/current Treasure browser pairs");
const byPair = Map.groupBy(measurements.records, (record) => record.pairId);
for (const [pairId, records] of byPair) {
  assert.equal(records.length, 2, `${pairId}: original/current records`);
  const original = records.find((record) => record.side === "original");
  const current = records.find((record) => record.side === "current");
  assert.ok(original && current, `${pairId}: both sides`);
  const filterRuns = (record) => record.measurement.textRuns.filter((run) =>
    run.parentTag === "LABEL" && run.parentClass.split(/\s+/).includes("map-filter-field"));
  const left = filterRuns(original);
  const right = filterRuns(current);
  assert.equal(left.length, 2, `${pairId}: original has two Treasure label text runs`);
  assert.equal(right.length, 2, `${pairId}: current has two Treasure label text runs`);
  for (let index = 0; index < 2; index += 1) {
    assert.equal(right[index].text, left[index].text, `${pairId}: label ${index} text`);
    assert.equal(right[index].styles.color, left[index].styles.color, `${pairId}: label ${index} effective color`);
    assert.deepEqual(right[index].rect, left[index].rect, `${pairId}: label ${index} text geometry`);
  }
}

const pixel = readJson(path.join(here, "pixel-fences-r1.json"));
assert.equal(pixel.marker, "LWB317_INDEPENDENT_TOOLBAR_PIXEL_MASK");
assert.equal(pixel.pairs.length, 4);
for (const pair of pixel.pairs) assert.equal(pair.changedPixelsOutsideDisabledActionBoxes, 0, `${pair.pairId}: no pixels outside accepted action fences`);
const mutation = readJson(path.join(here, "pixel-mutation-control.json"));
const mutatedTreasure = mutation.pairs.find((pair) => pair.pairId === "treasure-en-light-desktop");
assert.ok(mutatedTreasure, "historical Treasure mutation control");
assert.equal(mutatedTreasure.changedPixelsOutsideDisabledActionBoxes, 1734, "exact submitted Treasure color defect detected");

const actionRenderer = readJson(path.join(here, "action-messages/current-results.json"));
assert.equal(actionRenderer.marker, "LWB317_VISUAL_FINAL_UNIT_A_ACTION_MESSAGES_EXECUTED");
assert.equal(actionRenderer.requiredCaseCount, 8, "Dispatch/Ghost empty/populated EN/JA renderer states");
const actionBrowser = readJson(path.join(here, "action-message-browser/browser/comparison-results.json"));
assert.equal(actionBrowser.marker, "LWB317_VISUAL_FINAL_UNIT_A_ACTION_MESSAGE_BROWSER_COMPARED");
assert.equal(actionBrowser.pairs.length, 8);
for (const pair of actionBrowser.pairs) {
  assert.equal(pair.exactScreenshotBytes, true, `${pair.pairId}: exact browser pixels`);
  assert.equal(pair.geometryDifferences.length, 0, `${pair.pairId}: geometry`);
  assert.equal(pair.styleDifferences.length, 0, `${pair.pairId}: computed styles`);
  assert.equal(pair.pseudoDifferences.length, 0, `${pair.pairId}: pseudo-elements`);
  assert.equal(pair.textRunDifferences.length, 0, `${pair.pairId}: descendant text`);
}

for (const language of ["en", "ja"]) {
  for (const tab of ["dispatch", "ghost"]) {
    const emptyOriginal = readText(path.join(here, `action-messages/raw/${language}-${tab}-empty-original.html`));
    const emptyCurrent = readText(path.join(here, `action-messages/raw/${language}-${tab}-empty-current.html`));
    const populatedOriginal = readText(path.join(here, `action-messages/raw/${language}-${tab}-populated-original.html`));
    const populatedCurrent = readText(path.join(here, `action-messages/raw/${language}-${tab}-populated-current.html`));
    assert.ok(!emptyOriginal.includes("map-claim-result") && !emptyCurrent.includes("map-claim-result"), `${language}/${tab}: empty message state`);
    assert.ok(populatedOriginal.includes('class="map-claim-result" role="status"'), `${language}/${tab}: original populated message`);
    assert.ok(populatedCurrent.includes('class="map-claim-result" role="status"'), `${language}/${tab}: current populated message`);
  }
}

const mounted = readJson(path.join(here, "mounted/browser-interactions.json"));
assert.equal(mounted.marker, "LWB317_VISUAL_FINAL_UNIT_A_MOUNTED_INTERACTIONS_OK");
assert.equal(mounted.cases.length, 34);
assert.equal(mounted.cases.every((entry) => entry.pass), true, "mounted interaction assertions");
assert.equal(mounted.console.filter((entry) => ["error", "pageerror"].includes(entry.type)).length, 0, "mounted console/page errors");

const externalInputs = [
  "evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TOOLBAR-VISUAL-001/check-pixel-fences.py",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/harness.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/lib.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/original/semantics.mjs",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/ja-UrbzJu-m.js",
  "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx",
  "src/LWBridge.UI-0.3.17/src/reference.css",
  "src/LWBridge.UI-0.3.17/src/styles.css",
  "src/LWBridge.UI-0.3.17/src/locales/en.js",
  "src/LWBridge.UI-0.3.17/src/locales/ja.js",
];
const dependencyManifest = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a",
  marker: "LWB317_VISUAL_FINAL_UNIT_A_DEPENDENCIES",
  generatedAt: "2026-10-04",
  toolchain: {
    node: process.version,
    npm: "11.16.0",
    python: "3.12.10",
    pillow: "12.3.0",
    chrome: mounted.browser.version,
    timezone: "Asia/Singapore",
    browserDeviceScaleFactor: 1
  },
  referenceExecutable: {
    path: "C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe",
    sha256: hashFile("C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe")
  },
  files: externalInputs.map((entry) => {
    const file = path.join(repo, entry);
    return { path: entry, sha256: hashFile(file), bytes: fs.statSync(file).size };
  })
};
fs.writeFileSync(path.join(here, "dependencies.json"), `${JSON.stringify(dependencyManifest, null, 2)}\n`);

const result = {
  task: "LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a",
  marker: "LWB317_VISUAL_FINAL_UNIT_A_VALIDATED",
  source: { path: rel(pageFile), sha256: hashFile(pageFile) },
  rendererCases: renderer.requiredCaseCount + renderer.supplementalCaseCount,
  treasureBrowserPairs: pixel.pairs.length,
  outsideFencePixels: pixel.pairs.reduce((sum, pair) => sum + pair.changedPixelsOutsideDisabledActionBoxes, 0),
  mutationOutsideFencePixels: mutatedTreasure.changedPixelsOutsideDisabledActionBoxes,
  actionMessagePairs: actionBrowser.pairs.length,
  exactActionMessagePixelPairs: actionBrowser.pairs.filter((pair) => pair.exactScreenshotBytes).length,
  mountedCases: mounted.cases.length
};
fs.writeFileSync(path.join(here, "validation-results.json"), `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify(result, null, 2));
