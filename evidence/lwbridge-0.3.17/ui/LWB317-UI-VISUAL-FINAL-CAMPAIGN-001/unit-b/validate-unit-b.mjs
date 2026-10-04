import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readJson = (file) => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));
const sha = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex").toUpperCase();

const expectedExe = "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783";
assert.equal(sha(path.resolve(repo, "../LW/lwbridge-0.3.17.exe")), expectedExe, "target exe identity");

const scheduled = readJson("results.json");
assert.equal(scheduled.result, "LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK");
assert.equal(scheduled.counts.renders, 10482);
assert.equal(scheduled.mutation.total, 51);
assert.equal(scheduled.mutation.detected, 51);
assert.equal(scheduled.mutation.survivors.length, 0);
assert.equal(scheduled.fixtures.coveredBranches.length, 61);
assert.equal(scheduled.fixtures.requiredBranches, 61);

const integrated = readJson("current-results.json");
assert.equal(integrated.marker, "LWB317_VISUAL_FINAL_UNIT_B_INTEGRATED_MAP_EXECUTED");
assert.equal(integrated.requiredCaseCount, 70);
assert.equal(integrated.supplementalCaseCount, 5);
assert.equal(integrated.caseCount, 75);

const browser = readJson("browser/comparison-results.json");
assert.equal(browser.marker, "LWB317_VISUAL_FINAL_UNIT_B_BROWSER_COMPARED");
assert.equal(browser.pairs.length, 16);
const pixel = readJson("pixel-results.json");
assert.equal(pixel.marker, "LWB317_VISUAL_FINAL_UNIT_B_PIXELS_OK");
assert.equal(pixel.pixelExactPairs, 13);
assert.equal(pixel.acceptedDifferencePairs, 3);
assert.equal(pixel.outsideAcceptedPixels, 0);

const mounted = readJson("mounted/browser-interactions.json");
assert.equal(mounted.marker, "LWB317_VISUAL_FINAL_UNIT_B_MOUNTED_INTERACTIONS_OK");
assert.equal(mounted.cases.length, 25);
assert.equal(mounted.screenshots.length, 2);
assert.equal(mounted.console.filter((entry) => entry.type === "error" || entry.type === "pageerror").length, 0);

const expectedNonExact = new Set(["city-error-en-dark", "scheduled-empty-en-light", "scheduled-conditional-en-dark"]);
assert.deepEqual(new Set(pixel.results.filter((entry) => entry.changedPixels > 0).map((entry) => entry.pairId)), expectedNonExact);
assert.equal(pixel.results.filter((entry) => entry.outsideAcceptedPixels !== 0).length, 0);

console.log(JSON.stringify({
  marker: "LWB317_VISUAL_FINAL_UNIT_B_VALIDATED",
  scheduledRenders: scheduled.counts.renders,
  scheduledMutations: `${scheduled.mutation.detected}/${scheduled.mutation.total}`,
  integratedRendererCases: integrated.caseCount,
  browserPairs: browser.pairs.length,
  pixelExactPairs: pixel.pixelExactPairs,
  acceptedDifferencePairs: pixel.acceptedDifferencePairs,
  outsideAcceptedPixels: pixel.outsideAcceptedPixels,
  mountedCases: mounted.cases.length,
}, null, 2));
