import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
import en from "../../../../src/LWBridge.UI-0.3.17/src/locales/en.js";
import ja from "../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../..");
const read = file => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const result = read("channel-results.json"), native = read("native-contract.json");
for (const source of [result.source, ...native.sources]) {
  const bytes = fs.readFileSync(path.join(repo, source.path));
  assert.equal(hash(bytes), source.sha256);
  for (const locator of Object.values(source.locators)) {
    assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
  }
}
assert.equal(result.result, "LWB317_HOME_ERROR002_OK");
assert.equal(result.callbackResults.length, 9);
for (const callback of result.callbackResults) assert.equal(callback.pass, true);
assert.equal(result.sourcePickerCases.length, 4);
assert.equal(result.renderResults.length, 72);
const browser = read("browser-results.json");
assert.equal(browser.syntheticBrowserOnly, true);
assert.equal(browser.flows.length, 5);
assert.deepEqual(browser.consoleErrors, []);
const observed = state => browser.flows.find(flow => flow.state === state).observed;
assert.equal(observed("home-errors-both").rootMessage, en["error.INVALID_GAME_ROOT"]);
assert.deepEqual(observed("home-errors-both").actionErrors, [en["error.GAME_XLUA_ABI_UNSUPPORTED"]]);
assert.deepEqual(observed("home-errors-both/navigation-return"), observed("home-errors-both"));
assert.equal(observed("home-error-root").rootMessage, ja["error.INVALID_GAME_ROOT"]);
assert.deepEqual(observed("home-error-root").actionErrors, []);
assert.equal(observed("home-error-action-missing-root").rootMessage, en["setup.gameRootMissing"]);
assert.deepEqual(observed("home-error-action-missing-root").actionErrors, [en["error.GAME_XLUA_ABI_UNSUPPORTED"]]);
assert.deepEqual(observed("home-missing").actionErrors, []);
for (const flow of browser.flows) assert.ok(flow.observed.controls.every(control => control.disabled));
assert.equal(browser.screenshots.length, 2);
for (const screenshot of browser.screenshots) {
  const bytes = fs.readFileSync(path.join(here, screenshot.file));
  assert.equal(bytes[0], 255); assert.equal(bytes[1], 216);
  assert.equal(hash(bytes), screenshot.sha256);
}
const verification = read("verification.json");
assert.equal(verification.state, "AWAITING_REVIEW");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
for (const check of Object.values(verification.checks)) assert.equal(check, "PASS");
console.log("LWB317_HOME_ERROR002_EVIDENCE_OK");
