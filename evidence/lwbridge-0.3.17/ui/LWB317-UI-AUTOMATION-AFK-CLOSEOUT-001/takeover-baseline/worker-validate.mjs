import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const fixtureModule = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js")));

const activeAssist = new Set(["scheduled", "waiting_connection", "retry_wait", "running"]);
for (const [state, status] of Object.entries({
  "automation-assist-waiting": "waiting_connection",
  "automation-assist-retry-wait": "retry_wait",
  "automation-assist-running": "running",
  "automation-assist-failed": "failed",
  "automation-assist-expired": "expired",
})) {
  const fixture = fixtureModule.previewAssistFixture(state);
  assert.equal(fixture.jobs[0].scheduleStatus, status, state);
  assert.equal(activeAssist.has(status), !["failed", "expired"].includes(status), `${state}: task-row active predicate`);
}
const scheduled = fixtureModule.previewAssistFixture("automation-assist-schedule");
assert.equal(scheduled.jobs.find((job) => job.scheduleStatus === "failed")?.uuid, "fixture-assist-2");
assert.equal(scheduled.tasks.some((task) => task.uuid === "fixture-assist-2"), true);
assert.equal(fixtureModule.previewAssistFixture("automation-assist-empty").tasks.length, 0);
assert.equal(fixtureModule.previewAssistFixture("automation-assist-busy").busy, true);

for (const state of ["manual_wait", "shield_paused", "runtime_wait", "recalling", "recall_failed", "state_unconfirmed"]) {
  const previewState = `automation-gather-${state}`;
  assert.equal(fixtureModule.previewResourceGatherConfig(previewState).enabled, true, previewState);
}
assert.deepEqual(fixtureModule.previewResourceGatherRuntime("automation-gather-no-squads").gatherSquadIndexes, []);
assert.equal(fixtureModule.previewResourceGatherRuntime("automation-gather-runtime_wait").step, "runtime_wait");
assert.equal(fixtureModule.previewResourceGatherRuntime("automation-gather-manual_wait").gatherSquads[0].pauseReason, "manual_wait");
assert.equal(fixtureModule.previewResourceGatherRuntime("automation-gather-shield_paused").gatherSquads[0].step, "shield_paused");
assert.equal(fixtureModule.previewResourceGatherRuntime("automation-gather-view-change").gatherSquadIndexes.includes(3), true);

assert.equal(fixtureModule.previewAutomationRuntime("Trucks").capabilities.batchDeparture, true);
assert.equal(fixtureModule.previewAutomationRuntime("Secret Task").capabilities.superRefresh, false);

const pages = readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/Pages.jsx"), "utf8");
assert.match(pages, /runtime\.capabilities\?\.batchDeparture === true/);
assert.match(pages, /runtime\.capabilities\?\.superRefresh === true/);
assert.match(pages, /\["recalling", "recall_failed", "state_unconfirmed"\]\.includes\(squadRuntime\?\.step\)/);
assert.match(pages, /squadRuntime\?\.pauseReason === "manual_wait"/);
assert.match(pages, /squadRuntime\?\.shieldEndAt/);

const sha256 = (file) => createHash("sha256").update(readFileSync(file)).digest("hex");
const protectedFiles = new Map([
  ["src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js", "a41e07a3487fc71b78376ba99609c030ddad6b30e71daac5c428b42eaa6bc447"],
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/filter-lifecycle-results.json", "d7b32b2fbce9f8f4c3945f82c80506d9b22536604058b6228cadf921169afd04"],
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-results.json", "4e1f78a93e8d5a5e3995ba137c08089c48d900b7915f6c896e20fc555a94d2fc"],
  [".scratch-lwb317/AutomationPanel.pretty.js", "8e7173c71afe650cf1839e7eaa78e67bfa066ff6289ac2141410441e49da2d79"],
  [".scratch-lwb317/baseline-check.mjs", "9c308b07b2a79632b92eec959e2d1108d61b5809c58f33f3fab69e42403275c9"],
  [".scratch-lwb317/SquadPanel.pretty.js", "dc973a3adac1f9bc06230229c4a292e8659d0aaa82cdbb6812e97cbc4fc3d3d5"],
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/weekly-save-error-light-en.png", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"],
]);
for (const [relative, expected] of protectedFiles) assert.equal(sha256(path.join(repo, relative)), expected, `protected ${relative}`);

console.log("LWB317_AUTOMATION_AFK_CLOSEOUT_VALIDATOR_OK");
