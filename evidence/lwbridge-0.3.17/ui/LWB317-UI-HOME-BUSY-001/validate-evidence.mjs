import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../..");
const read = file => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const results = read("busy-results.json"), browser = read("browser-results.json"), verification = read("verification.json");
const bytes = fs.readFileSync(path.join(repo, results.reference.path));
assert.equal(hash(bytes), results.reference.sha256);
for (const locator of Object.values(results.reference.locators)) assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
assert.equal(hash(fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8").replace(/\r\n/g, "\n")), results.currentProducers.appSha256NormalizedLF);
assert.deepEqual(results.counts, { renderComparisons: 13824, predicateCases: 384, preferencesChecks: 27, previewFixtures: 63 });
assert.equal(results.localeResults.length, 9);
for (const locale of results.localeResults) { assert.equal(locale.pass, true); assert.equal(locale.comparisons, 1536); }
assert.equal(browser.syntheticBrowserOnly, true); assert.equal(browser.flows.length, 9); assert.deepEqual(browser.consoleErrors, []);
for (const flow of browser.flows) {
  const actual = flow.observed;
  const expected = results.fixtureResults.find(row => row.language === actual.language && row.fixture === actual.fixture).actual;
  assert.equal(actual.status, expected.status); assert.equal(actual.statusClass, expected.statusClass);
  assert.deepEqual(actual.controls, expected.controls); assert.deepEqual(actual.picker, expected.pickerButton ? [expected.pickerButton] : []);
  assert.equal(actual.repairHint, expected.repairHint);
  assert.ok([...actual.controls, ...actual.picker, ...actual.switches].every(control => control.disabled));
}
assert.deepEqual(browser.flows[1].observed, browser.flows[0].observed);
assert.equal(browser.screenshots.length, 2);
for (const screenshot of browser.screenshots) {
  assert.ok(browser.flows.some(flow => flow.state === screenshot.state && flow.observed.language === screenshot.language));
  const bytes = fs.readFileSync(path.join(here, screenshot.file));
  assert.equal(bytes[0], 255); assert.equal(bytes[1], 216); assert.equal(hash(bytes), screenshot.sha256);
}
assert.equal(verification.state, "AWAITING_REVIEW");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
for (const check of Object.values(verification.checks)) assert.equal(check, "PASS");
console.log("LWB317_HOME_BUSY001_EVIDENCE_OK");
