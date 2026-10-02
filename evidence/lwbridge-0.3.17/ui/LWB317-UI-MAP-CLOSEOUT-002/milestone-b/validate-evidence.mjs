import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readJson = (relative) => JSON.parse(fs.readFileSync(path.join(here, relative), "utf8"));
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const normalizedSourceHash = (file) => crypto.createHash("sha256")
  .update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n"))
  .digest("hex");

const locators = readJson("source-locators.json");
const ownership = readJson("refresh-ownership-results.json");
const delayedClear = readJson("delayed-clear-current-results.json");
const browser = readJson("browser-smoke-results.json");

assert.equal(ownership.status, "PASS");
assert.equal(ownership.baselineCounterexample.status, "EXPECTED_FAIL_OWNERSHIP");
assert.equal(ownership.controlledBoundary.summaryCalls, 0);
assert.equal(ownership.controlledBoundary.scanListenerCalls, 0);
assert.deepEqual(ownership.controlledBoundary.optionServers, [321, 321]);
assert.deepEqual(ownership.controlledBoundary.mountAlreadyReadingOptions, [321, 321]);
assert.equal(ownership.controlledBoundary.progressTimeoutRowsOnly, true);
assert.equal(ownership.controlledBoundary.completionOptionNameInvalidation, true);
assert.equal(ownership.parentLifecycle.pollOverlapSuppressed, true);
assert.equal(ownership.parentLifecycle.profileReplacementFenced, true);
assert.equal(ownership.parentLifecycle.deferredRejectionPreservedState, true);
assert.equal(ownership.parentLifecycle.deferredUnmountSuccessFenced, true);
assert.deepEqual(delayedClear.trace.optionCalls, [322, 321]);

const pagePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const appPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx");
assert.equal(normalizedSourceHash(pagePath), ownership.source.currentPageSha256);
assert.equal(normalizedSourceHash(appPath), ownership.source.currentAppSha256);

for (const asset of Object.values(locators.assets)) {
  assert.equal(sha256(path.join(repo, asset.path)), asset.sha256);
}
for (const locator of locators.locators) {
  const asset = locators.assets[locator.asset];
  const source = fs.readFileSync(path.join(repo, asset.path), "utf8");
  assert.equal(Buffer.byteLength(source.slice(0, source.indexOf(locator.anchor)), "utf8"), locator.utf8Byte);
  assert.ok(source.includes(locator.anchor), `missing source locator ${locator.anchor}`);
}

assert.equal(browser.status, "PASS");
assert.equal(browser.states.length, 2);
for (const state of browser.states) {
  assert.equal(state.consoleErrors, 0);
  assert.equal(state.consoleWarnings, 0);
  assert.equal(state.renderedRows, 50);
  assert.deepEqual(state.counts, ["52", "53", "54", "55", "56", "57", "58", "59", "0"]);
  assert.equal(sha256(path.join(here, state.screenshot)), state.screenshotSha256);
}
assert.equal(browser.server, 321);

const protectedManifest = JSON.parse(fs.readFileSync(
  path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/protected-wip.json"),
  "utf8",
));
for (const [relative, expected] of Object.entries(protectedManifest)) {
  assert.equal(sha256(path.join(repo, relative)), expected, `protected WIP changed: ${relative}`);
}

console.log(
  `LWB317_MAP_REFRESH_OWNERSHIP_EVIDENCE_OK locators=${locators.locators.length} browser=${browser.states.length} protected=${Object.keys(protectedManifest).length}`,
);
