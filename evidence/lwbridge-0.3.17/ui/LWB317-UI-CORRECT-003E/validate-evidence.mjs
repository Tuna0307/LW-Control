import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const read = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const locators = read("source-locators.json");
for (const reference of [locators.reference, locators.locale]) {
  const bytes = fs.readFileSync(path.join(repo, reference.path));
  assert.equal(hash(bytes), reference.sha256);
  for (const locator of Object.values(reference.locators || locators.sourceLocators)) {
    assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
  }
}
const render = read("render-results.json");
assert.equal(render.results.length, 11);
assert.equal(render.missingStatusCases, 4);
const browser = read("browser-results.json");
assert.equal(browser.flows.length, 12);
assert.deepEqual(browser.consoleErrors, []);
const flow = (state) => { const entry = browser.flows.find((entry) => entry.state === state); assert.ok(entry); return entry.observed || entry; };
assert.match(flow("automation-trade-loading").muted.join(""), /Loading goods/);
assert.equal(flow("automation-trade-loading-retained").goods, 2);
assert.equal(flow("automation-trade-error-retained").goods, 2);
assert.equal(flow("automation-trade-error").error, "Error: Fixture Trade goods request failed");
assert.match(flow("automation-trade-error/history").stats, /Success/);
assert.match(browser.flows.find((entry) => entry.state.endsWith("/history")).history, /Fixture Resource Crate/);
assert.match(flow("automation-trade-status-absent").stats, /Detected: 0[\s\S]*Last result: -/);
assert.equal(flow("automation-trade-save-error/after-toggle").sharedConfigError, "Changes have not been saved.");
assert.match(flow("automation-trade-save-error/after-toggle").stats, /Success/);
const verification = read("verification.json");
assert.equal(verification.state, "AWAITING_REVIEW");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
for (const value of Object.values(verification.checks)) assert.equal(value, "PASS");
for (const screenshot of browser.screenshots) {
  const bytes = fs.readFileSync(path.join(here, screenshot.file));
  assert.equal(bytes[0], 0xff); assert.equal(bytes[1], 0xd8);
  assert.equal(hash(bytes), screenshot.sha256);
}
for (const file of fs.readdirSync(here).filter((file) => file.endsWith(".json"))) read(file);
console.log("LWB317_UI_CORRECT003E_EVIDENCE_OK");
