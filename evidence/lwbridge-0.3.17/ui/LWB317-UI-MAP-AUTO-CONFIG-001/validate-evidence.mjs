import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

const locators = readJson("original-source-locators.json");
for (const asset of Object.values(locators.assets)) {
  assert.equal(sha256(path.join(repo, asset.asset)), asset.sha256, "original asset hash mismatch: " + asset.asset);
}

const baseline = readJson("baseline-results.json");
assert.equal(baseline.baseline.commit, "cf75b4e3cd8b602c17cef9a244385e6db7c02daf");
assert.equal(baseline.passed, 3);
assert.equal(baseline.failed, 9, "distinguishing baseline failures must remain visible");

const helpers = readJson("current-helper-results.json");
assert.equal(helpers.passed, 29);
assert.equal(helpers.failed, 0);

const profiles = readJson("profile-ownership-results.json");
assert.equal(profiles.status, "PASS");
assert.equal(profiles.cases.length, 7);
assert.ok(profiles.cases.every((entry) => entry.status === "PASS"));

const controls = readJson("control-results.json");
assert.equal(controls.status, "PASS");
assert.equal(controls.originalAgentB.status, "PASS");
assert.equal(controls.originalAgentB.cases, 12);
assert.equal(controls.currentCases.length, 11);
assert.ok(controls.currentCases.every((entry) => entry.status === "PASS"));

const regressions = readJson("regression-results.json");
assert.equal(regressions.status, "PASS");
assert.equal(regressions.count, 10);
assert.ok(regressions.results.every((entry) => entry.exitCode === 0));
assert.match(regressions.results[0].stdout, /baseline=6 current=0 requests=17/);
assert.match(regressions.results.at(-1).stdout, /childSummary=0 childListeners=0 options=321,321/);

const r1 = readJson("regression-filter-lifecycle-r1.json");
assert.equal(r1.cases.length, 5);
assert.ok(r1.cases.every((entry) => entry.pass === true));

const ownership = readJson("regression-refresh-ownership.json");
assert.equal(ownership.status, "PASS");
assert.equal(ownership.baselineCounterexample.status, "EXPECTED_FAIL_OWNERSHIP");
assert.equal(ownership.controlledBoundary.summaryCalls, 0);
assert.equal(ownership.controlledBoundary.scanListenerCalls, 0);
assert.deepEqual(ownership.controlledBoundary.optionServers, [321, 321]);

const browser = readJson("browser-results.json");
assert.equal(browser.status, "PASS");
assert.equal(browser.console.warnings, 0);
assert.equal(browser.console.errors, 0);
assert.equal(browser.nativeFence.runNowDisabledOffline, true);
assert.equal(browser.nativeFence.nativeActionsDispatched, 0);
assert.ok(browser.liveChecks.every((entry) => entry.status === "PASS"));

const hashes = readJson("current-source-hashes.json");
for (const entry of hashes.files) {
  assert.equal(sha256(path.join(repo, entry.path)), entry.sha256, "current file hash mismatch: " + entry.path);
}
for (const shot of browser.savedScreenshots) {
  assert.equal(sha256(path.join(here, shot.path)), shot.sha256, "screenshot hash mismatch: " + shot.path);
}

const protectedBefore = readJson("protected-wip-before.json");
const protectedAfter = readJson("protected-wip-after.json");
assert.equal(protectedAfter.status, "PASS");
assert.equal(protectedAfter.paths.length, protectedBefore.paths.length);
for (const expected of protectedBefore.paths) {
  const recorded = protectedAfter.paths.find((entry) => entry.path === expected.path);
  assert.ok(recorded, "missing protected record: " + expected.path);
  assert.equal(recorded.sha256, expected.sha256);
  assert.equal(sha256(path.join(repo, expected.path)), expected.sha256, "protected WIP changed: " + expected.path);
}

console.log("LWB317_AUTO_CONFIG_EVIDENCE_OK helpers=29 profiles=7 controls=11 regressions=10 protected=7 browser=PASS");
