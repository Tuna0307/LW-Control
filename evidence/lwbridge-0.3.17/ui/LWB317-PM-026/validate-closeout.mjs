import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const sha = (relative) => crypto.createHash("sha256").update(fs.readFileSync(path.join(repo, relative))).digest("hex");
const json = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const productFiles = ["MapDataPage.jsx", "mapInteractions.js", "ScheduledPlunder.jsx", "mapPlunderPresentation.js", "mapPlunderFixtures.js", "mapPreviewApi.js", "mapTablePresentation.js"].map((name) => `src/LWBridge.UI-0.3.17/src/${name}`);
assert.equal(execFileSync("git", ["diff", "--name-only", "5b76ae8", "--", ...productFiles], { cwd: repo, encoding: "utf8" }).trim(), "", "Lead continuation must not imply a product edit");
const browser = json("browser-recheck.json");
assert.equal(browser.reviewTarget, "5b76ae8111e160ad7673e1162e8a354d1c3cbd45");
assert.deepEqual(browser.clearEnglish.before, [11, 4, 15]);
assert.deepEqual(browser.clearEnglish.after, [11, 4, 15]);
assert.equal(browser.clearEnglish.message, "The action could not be completed.");
assert.deepEqual(browser.clearJapanese.counts, [11, 4, 15]);
assert.equal(browser.clearJapanese.message, "操作を完了できませんでした。");
assert.deepEqual(browser.search.before, browser.search.afterTyping);
assert.equal(browser.search.afterSearch.rows, 1);
assert.match(browser.search.afterSearch.rowText, /Fixture Commander 7/);
assert.equal(browser.dispatch.returnedButton, "Add to schedule (0)");
assert.equal(browser.dispatch.selectedBefore.share, false);
assert.match(browser.dispatch.rejectedSchedule.scheduledTab, /0$/);
assert.equal(browser.truck.afterTrainRoundTrip.selection, "Plunder selected trucks (2)");
assert.equal(browser.truck.afterTrainRoundTrip.page, "Page 2 of 2");
assert.match(browser.truck.afterTrainRoundTrip.row, /Truck Owner 51/);
assert.equal(browser.narrow.viewport.width, 375);
assert.equal(browser.narrow.horizontalPageOverflow, false);
assert.deepEqual(browser.console, []);
assert.deepEqual(browser.cleanup, { ownedTabClosed: true, temporaryViewportReset: true, existingWorkerViteListenerLeftRunning: true });

const scheduled = json("scheduled-replay.txt");
assert.equal(scheduled.result, "LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK");
assert.equal(scheduled.renders, 10482);
assert.equal(scheduled.mutationsDetected, 51);
assert.equal(scheduled.mutationSurvivors, 0);
assert.equal(scheduled.reactWarnings, 0);
const prefix = "evidence/lwbridge-0.3.17/ui/LWB317-PM-026/";
const worker = "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/";
const artifacts = [prefix + "browser-recheck.json", prefix + "scheduled-replay.txt", ...browser.screenshots.map((name) => prefix + name), ...["request-lifetime-results.json", "navigation-replay-results.json", "interaction-results.json", "mutation-results.json", "integration-results.json", "historical-replay-results.json", "scheduled/results.json", "browser-results.json", "original/final-review-results.json"].map((name) => worker + name)];
const manifest = {
  status: "PASS",
  productCheckpoint: browser.reviewTarget,
  decision: "ACCEPTED for focused source/local UI scope only",
  productHashes: Object.fromEntries(productFiles.map((file) => [file, sha(file)])),
  artifactHashes: Object.fromEntries(artifacts.map((file) => [file, sha(file)])),
  proofLimits: browser.scopeLimits,
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "manifest.json"), JSON.stringify(manifest, null, 2) + "\n");
else assert.deepEqual(json("manifest.json"), manifest);
execFileSync(process.execPath, [path.join(repo, worker, "check-protected-wip.mjs")], { cwd: repo, stdio: "pipe" });
console.log("LWB317_PM026_CLOSEOUT_VALID source/local acceptance; production unchanged; final browser and full scheduled replay pinned");
