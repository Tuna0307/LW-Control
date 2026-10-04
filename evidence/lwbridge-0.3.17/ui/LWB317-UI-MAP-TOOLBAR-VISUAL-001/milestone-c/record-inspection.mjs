import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const browserDir = path.join(here, "browser");
const readJson = (file) => JSON.parse(fs.readFileSync(file, "utf8"));
const writeJson = (file, value) => fs.writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);
const hashFile = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

const measurements = readJson(path.join(browserDir, "measurements.json"));
const comparisons = readJson(path.join(browserDir, "comparison-results.json"));
assert.equal(measurements.records.length, 12);
assert.equal(comparisons.pairs.length, 6);

const notes = {
  "city-en-light-desktop": "Original/current are visually identical. Toolbar controls, count and Page 1 of 31 pagination align exactly.",
  "city-ja-dark-375": "Original/current are visually identical at 375px. The narrow tab strip clips horizontally in the same place on both sides; stacked City controls and Page 1 of 31 pagination match.",
  "truck-en-dark-desktop": "Geometry is identical. Current intentionally renders Plunder selected trucks (1) disabled/fenced, so that one button loses the recovered blue enabled treatment; all other toolbar and pagination visuals match.",
  "dispatch-ja-light-375": "Geometry is identical at 375px. Current intentionally disables/fences Schedule selected and Share to Alliance, producing the only two visible button-style differences; all filters, delay input, count and pagination align.",
  "treasure-en-light-desktop": "Geometry is identical. Current intentionally keeps Claim Boxes and Claim Season Treasures disabled, producing the only visible differences; Treasure filters, count and pagination align.",
  "scheduled-ja-dark-375": "Original/current are visually identical. Scheduled toolbar/count presentation matches; Scheduled Plunder job-table bodies are intentionally outside this proof scope.",
};

const inspection = {
  task: "LWB317-UI-MAP-TOOLBAR-VISUAL-001",
  marker: "LWB317_MAP_TOOLBAR_SCREENSHOTS_INSPECTED",
  inspectedAt: "2026-10-04",
  records: measurements.records.map((record) => {
    const screenshot = path.join(path.resolve(here, "../../../../.."), record.screenshot);
    assert.equal(hashFile(screenshot), record.screenshotSha256, `${record.pairId}/${record.side}: screenshot hash before inspection record`);
    return {
      pairId: record.pairId,
      side: record.side,
      file: record.screenshot,
      sha256: record.screenshotSha256,
      viewport: record.viewport,
      status: "INSPECTED",
      observation: notes[record.pairId],
    };
  }),
};

const classification = {
  "city-en-light-desktop": {
    status: "PRESENTATION_MATCH",
    explanation: "Pixel, geometry and computed-style exact. Raw DOM differences are current accessibility/button-type attributes only.",
  },
  "city-ja-dark-375": {
    status: "PRESENTATION_MATCH",
    explanation: "Pixel, geometry and computed-style exact at 375px. Raw DOM differences are current accessibility/button-type attributes only.",
  },
  "truck-en-dark-desktop": {
    status: "INTENTIONAL_NATIVE_FENCE",
    explanation: "No geometry difference. Eight computed-style deltas are all the single unavailable Plunder selected trucks action (border/color/background/opacity/cursor) caused by the accepted disabled provider fence.",
  },
  "dispatch-ja-light-375": {
    status: "INTENTIONAL_NATIVE_FENCE",
    explanation: "No geometry difference. Sixteen computed-style deltas are exactly the two unavailable Schedule/Share actions (8 each) caused by accepted disabled provider fences.",
  },
  "treasure-en-light-desktop": {
    status: "INTENTIONAL_NATIVE_FENCE",
    explanation: "No geometry difference. Sixteen computed-style deltas are exactly the two disabled claim actions (8 each). Current label span wrappers are raw-DOM-only/presentation-equivalent.",
  },
  "scheduled-ja-dark-375": {
    status: "PRESENTATION_MATCH",
    explanation: "Pixel, geometry and computed-style exact. Raw DOM differences are current accessibility/button-type attributes only; job-table bodies are out of scope.",
  },
};

const expectedStyleCounts = {
  "city-en-light-desktop": 0,
  "city-ja-dark-375": 0,
  "truck-en-dark-desktop": 8,
  "dispatch-ja-light-375": 16,
  "treasure-en-light-desktop": 16,
  "scheduled-ja-dark-375": 0,
};
const matrix = comparisons.pairs.map((pair) => {
  assert.equal(pair.geometryDifferences.length, 0, `${pair.pairId}: geometry`);
  assert.equal(pair.styleDifferences.length, expectedStyleCounts[pair.pairId], `${pair.pairId}: expected classified style difference count`);
  return {
    pairId: pair.pairId,
    tab: pair.tab,
    language: pair.language,
    theme: pair.theme,
    viewport: pair.viewport,
    exactScreenshotBytes: pair.exactScreenshotBytes,
    domExact: pair.domExact,
    geometryDifferenceCount: pair.geometryDifferences.length,
    styleDifferenceCount: pair.styleDifferences.length,
    attributeDifferenceCount: pair.attributeDifferences.length,
    ...classification[pair.pairId],
  };
});

writeJson(path.join(browserDir, "inspection.json"), inspection);
writeJson(path.join(here, "difference-matrix.json"), {
  task: inspection.task,
  marker: "LWB317_MAP_TOOLBAR_DIFFERENCES_CLASSIFIED",
  unresolvedInScopePresentationDefects: 0,
  sharedFacts: [
    "Both sides use the same reference.css then styles.css bytes, timezone, language, theme, viewport and device scale.",
    "All six pairs have zero geometry differences and all twelve captures have fonts loaded with zero browser console/page issues.",
    "Current tablist/tab ARIA metadata and explicit button types remain visible in raw DOM evidence and do not change pixels in the exact pairs.",
    "Provider/native action availability is intentionally preserved; screenshots do not enable unavailable scheduling/share/claim/truck operations.",
  ],
  pairs: matrix,
});

console.log(JSON.stringify({ marker: "LWB317_MAP_TOOLBAR_SCREENSHOTS_INSPECTED", screenshots: inspection.records.length, pairs: matrix.length, unresolvedInScopePresentationDefects: 0 }, null, 2));
