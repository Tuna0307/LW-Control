// Validates the saved evidence packet of LWB317-UI-MAP-INTERACTIONS-001 against the working tree.
// It does not rerun the behavior checks (run them via the commands in README.md); it proves that the recorded
// results describe the CURRENT production sources, that immutable historical evidence is untouched, and that the
// recorded outcomes carry the required distinguishing baseline failures.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const read = (relative) => fs.readFileSync(path.join(here, relative), "utf8");
const json = (relative) => JSON.parse(read(relative));
const lfHash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n")).digest("hex");
const rawHash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const git = (...args) => execFileSync("git", args, { cwd: repo, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });

// 1. Original asset identity.
assert.equal(rawHash(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js")), "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");

// 2. Recorded results describe the current production page and fail/pass as required.
const page = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const lifetime = json("request-lifetime-results.json");
assert.equal(lifetime.status, "PASS");
assert.equal(lifetime.sources.current.sha256LfNormalized, lfHash(page), "request-lifetime results are stale");
assert.deepEqual(lifetime.failures.baseline, []);
assert.equal(lifetime.failures["delivery-305240e"].length, 4);
assert.deepEqual(lifetime.failures.current, []);
const replay = json("navigation-replay-results.json");
assert.equal(replay.status, "PASS");
assert.equal(replay.baseline.failures.length, 6);
assert.equal(replay.current.sha256LfNormalized, lfHash(page), "navigation replay results are stale");
const interactions = json("interaction-results.json");
assert.equal(interactions.status, "PASS");
assert.equal(interactions.sources.current, lfHash(page), "interaction results are stale");
assert.equal(interactions.sources.baseline, lfHash(path.join(here, "baseline-3e8617c.MapDataPage.jsx")));
assert.equal(interactions.currentMismatches.length, 0);
assert.ok(interactions.baselineMismatches.length >= 20, "baseline must keep distinguishing failures");
assert.equal(interactions.originalErrors.length, 0);
const integration = json("integration-results.json");
assert.equal(integration.status, "PASS");
const integrationBaseline = json("integration-baseline-results.json");
assert.equal(integrationBaseline.status, "FAIL");
assert.ok(integrationBaseline.results.filter((entry) => entry.status === "FAIL").length >= 8);
const mutations = json("mutation-results.json");
assert.equal(mutations.status, "PASS");
assert.equal(mutations.detected, mutations.mutations);
const historical = json("historical-replay-results.json");
assert.equal(historical.status, "PASS");
assert.ok(historical.results.length >= 5 && historical.results.every((entry) => entry.exitCode === 0));
const scheduled = json("scheduled/results.json");
assert.equal(scheduled.result, "LWB317_SCHEDULED_PLUNDER_SOURCE_LOCAL_OK");
assert.equal(scheduled.counts.mutationsDetected, scheduled.counts.mutationsTotal);
const original = json("original/results.json");
assert.equal(original.summary.failedClaims, 0, "original runtime claims all executed");
assert.ok(original.summary.claims >= 100);
const browser = json("browser-results.json");
assert.equal(browser.status, "PASS");
assert.equal(browser.console.freshLoadAndTenTabCycle.errors, 0);
for (const shot of browser.screenshots) assert.ok(fs.statSync(path.join(here, shot.path)).size > 10_000, `screenshot ${shot.path}`);

// 3. Immutable history: PM-025 evidence and NAVIGATION-001 evidence match the assignment commit.
for (const file of [
  "evidence/lwbridge-0.3.17/ui/LWB317-PM-025/availability-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-PM-025/check-availability.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-NAVIGATION-001/check-navigation.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-NAVIGATION-001/navigation-results.json",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-ROWS-001/row-action-results.json",
]) {
  assert.equal(git("diff", "--name-only", "bec0910", "--", file).trim(), "", `${file} must be unchanged since the assignment commit`);
}
const pm025 = json("../LWB317-PM-025/availability-results.json");
assert.ok(pm025.deliveryFailures >= 1, "recorded failing lead evidence preserved");

// 4. Protected pre-existing WIP.
execFileSync(process.execPath, [path.join(here, "check-protected-wip.mjs")], { cwd: repo, stdio: "pipe" });
console.log("LWB317_INTERACTIONS_EVIDENCE_VALID");
