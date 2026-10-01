import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import en from "../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, "../../../..");
const read = (file) => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const results = read("helper-render-results.json");
for (const reference of [results.reference, ...Object.values(results.localeEvidence)]) {
  const bytes = fs.readFileSync(path.join(repo, reference.path)); assert.equal(hash(bytes), reference.sha256);
  for (const locator of Object.values(reference.locators)) assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
}
assert.equal(results.syntheticResults.length, 60); assert.equal(results.localeResults.length, 9);
for (const row of results.syntheticResults) assert.equal(row.actual, row.expected);
for (const row of results.localeResults) { assert.equal(row.noErrorGuard, "PASS"); assert.equal(row.missingRootErrorGuards, "PASS"); for (const state of row.renderResults) assert.deepEqual(state.actual, state.expected); }
const browser = read("browser-results.json"); assert.equal(browser.flows.length, 5); assert.deepEqual(browser.consoleErrors, []);
const observed = (state) => browser.flows.find((flow) => flow.state === state).observed;
assert.deepEqual(observed("home-error-unknown").errors, [en["common.actionFailed"]]);
assert.deepEqual(observed("home-error-embedded").errors, [en["error.GAME_XLUA_ABI_UNSUPPORTED"]]);
assert.deepEqual(observed("home-error-embedded/navigation-return").errors, observed("home-error-embedded").errors);
assert.deepEqual(observed("home-recovery-error-unknown").errors, [ja["recovery.failedDetail"].replace("{error}", ja["common.actionFailed"])]);
assert.deepEqual(observed("home-connected").errors, []);
for (const flow of browser.flows) assert.ok(flow.observed.controls.every((control) => control.disabled));
for (const screenshot of browser.screenshots) { const bytes = fs.readFileSync(path.join(here, screenshot.file)); assert.equal(bytes[0], 255); assert.equal(bytes[1], 216); assert.equal(hash(bytes), screenshot.sha256); }
const verification = read("verification.json"); assert.equal(verification.state, "AWAITING_REVIEW");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
for (const check of Object.values(verification.checks)) assert.equal(check, "PASS");
console.log("LWB317_HOME_ERROR001_EVIDENCE_OK");
