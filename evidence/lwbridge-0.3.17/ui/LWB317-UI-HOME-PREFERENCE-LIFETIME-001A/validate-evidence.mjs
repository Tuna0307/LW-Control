import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const read = (relative) => JSON.parse(fs.readFileSync(path.join(here, relative), "utf8"));
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const canonicalSource = (value) => value.replace(/\r\n/g, "\n");

const source = read("recovery/source-recovery.json");
const baseline = read("recovery/baseline-results.json");
const current = read("milestone-b/current-results.json");
const preservation = read("milestone-b/preservation-results.json");
const browser = read("browser-results.json");
const start = read("protected-wip-start.json");
const final = read("protected-wip-final.json");

for (const report of [source, baseline, current, preservation, browser, start, final]) {
  assert.equal(report.task || "LWB317-UI-HOME-PREFERENCE-LIFETIME-001A", "LWB317-UI-HOME-PREFERENCE-LIFETIME-001A");
}
assert.equal(source.classification, "EXACT_BYTES / EXACT_CONTRACT");
assert.equal(source.source.sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(source.contract.defaultWhenMissing, true);
assert.equal(source.source.key.value, "lwbridge.autoLaunchGame");
assert.equal(source.source.key.utf8ByteOffset, 246758);
assert.equal(source.source.setter.utf8ByteOffset, 248381);
assert.equal(source.source.homeBinding.utf8ByteOffset, 338639);
assert.equal(source.source.switchComponent.utf8ByteOffset, 213332);

assert.equal(baseline.immutableDispatch, "52dd38a6c5f003ef6f9caebf681d3fbd1ed824b5");
assert.equal(baseline.parityFailures.length, 2);
assert.equal(baseline.observed.beforeAcknowledgement.mountedSwitchDisabled, true);

assert.equal(current.result, "LWB317_HOME_PREFERENCE_CURRENT_OK");
assert.equal(current.cases.length, 7);
assert.ok(current.cases.every((item) => item.status === "PASS"));
assert.equal(preservation.result, "LWB317_HOME_PREFERENCE_PRESERVATION_OK");
assert.equal(preservation.exactCallbacks.length, 10);
assert.equal(browser.result, "PASS");
assert.equal(browser.console.cleanRunPageErrors, 0);
assert.equal(final.result, "LWB317_HOME_PREFERENCE_WIP_GUARD_OK");
assert.equal(final.checked.length, start.paths.length);
assert.equal(final.checked.length, 10);

const currentFiles = [
  [current.production.appPath, current.production.appSha256],
  [current.production.homePath, current.production.homeSha256],
  [current.production.helperPath, current.production.helperSha256],
];
for (const [relative, expected] of currentFiles) {
  assert.equal(sha256(canonicalSource(fs.readFileSync(path.join(repo, relative), "utf8"))), expected, `${relative} changed after current evidence`);
}

const dispatch = baseline.immutableDispatch;
for (const [relative, expected] of [[baseline.source.appPath, baseline.source.appSha256], [baseline.source.homePath, baseline.source.homeSha256]]) {
  const content = execFileSync("git", ["show", `${dispatch}:${relative}`], { cwd: repo });
  assert.equal(sha256(content), expected, `${relative} immutable dispatch hash mismatch`);
}

console.log("LWB317_HOME_PREFERENCE_EVIDENCE_OK");
