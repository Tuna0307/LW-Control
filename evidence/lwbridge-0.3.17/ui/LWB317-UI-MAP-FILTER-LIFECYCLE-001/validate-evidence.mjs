import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const json = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const rawHash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const lfHash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n")).digest("hex");
const git = (...args) => execFileSync("git", args, { cwd: repo, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 }).trim();

const originalAsset = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js");
assert.equal(rawHash(originalAsset), "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");

const baseline = path.join(here, "baseline-c72aae6.MapDataPage.jsx");
assert.equal(git("hash-object", baseline), git("rev-parse", "c72aae6:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx"));

const results = json("filter-lifecycle-results.json");
const page = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
assert.equal(results.sources.current?.sha256 ?? results.sources.corrected.sha256, lfHash(page), "filter lifecycle results are stale");
assert.deepEqual(results.corrected.refresh, { alliance: "all", level: "", treasureType: "" });
assert.deepEqual(results.corrected.validRefresh, { alliancePreserved: true, levelPreserved: true, treasurePreserved: true });
assert.deepEqual(results.corrected.delayedClearAckAfterServerChange.optionCalls, [322, 321]);
assert.equal(results.corrected.staleOptionsAfterClear.staleApplied, false);
assert.equal(results.corrected.staleSearchAfterClear.staleRowApplied, false);
assert.deepEqual(results.corrected.treasureMissing.initial.query, { includeForeignRadarTreasures: false, luckyFirst: true });
assert.deepEqual(results.corrected.treasureSeeded.initial.query, { includeForeignRadarTreasures: true, luckyFirst: false });

const browser = json("browser-results.json");
assert.equal(browser.status, "PASS");
assert.deepEqual(browser.interactive.consoleErrors, []);
assert.deepEqual(browser.interactive.taskStorageAfterInteractiveCleanup, {
  "lwbridge.mapIncludeForeignRadarTreasures": null,
  "lwbridge.mapLuckyTreasurePriority": null,
});
assert.equal(browser.narrowViewport.width, 375);
assert.equal(browser.narrowViewport.language, "ja");
assert.equal(browser.narrowViewport.theme, "dark");
const screenshot = fs.readFileSync(path.join(here, browser.narrowViewport.screenshot));
assert.equal(rawHash(path.join(here, browser.narrowViewport.screenshot)), browser.narrowViewport.sha256);
assert.equal(screenshot.readUInt32BE(16), 375);
assert.equal(screenshot.readUInt32BE(20), 3000);

const finalReview = fs.readFileSync(path.join(here, "subagent-final-review.md"), "utf8");
assert.match(finalReview, /PASS \/ ACCEPT/);
assert.match(finalReview, /\[322,\s*321\]/);

for (const oldEvidence of [
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-NAVIGATION-001",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001",
]) {
  assert.equal(git("diff", "--name-only", "c72aae6", "--", oldEvidence), "", oldEvidence + " historical evidence changed");
}

execFileSync(process.execPath, [
  path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-protected-wip.mjs"),
], { cwd: repo, stdio: "pipe" });

console.log("LWB317_UI_MAP_FILTER_LIFECYCLE_EVIDENCE_VALID");
