import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const read = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();

const results = read("switch-results.json");
assert.equal(results.result, "LWB317_UI_SWITCH_LOCALE001_OK");
assert.equal(results.reference.Bn.utf8ByteOffset, 213332);
assert.deepEqual(results.counts, {
  languages: 9,
  callers: 11,
  uniqueCallerLabels: 10,
  renderComparisons: 360
});
assert.equal(results.localeSummary.length, 9);
assert.ok(results.localeSummary.every((entry) => entry.pass));
assert.ok(results.differential.every((entry) => JSON.stringify(entry.actual) === JSON.stringify(entry.expected)));

const browser = read("browser-results.json");
assert.equal(browser.flows.length, 5);
assert.deepEqual(browser.consoleErrors, []);
assert.deepEqual(
  browser.flows.slice(0, 3).map((flow) => [flow.observed.ariaChecked, flow.observed.ariaLabel]),
  [
    [false, "Show FPS: Disabled"],
    [true, "Show FPS: Enabled"],
    [false, "Show FPS: Disabled"]
  ]
);
const checkedJa = browser.flows.find((flow) => flow.state === "home-connected/ja");
const uncheckedJa = browser.flows.find((flow) => flow.state === "home-running-disconnected/ja");
assert.ok(checkedJa.observed.every((item) => item.disabled && item.ariaChecked && item.ariaLabel.endsWith(": 有効")));
assert.ok(uncheckedJa.observed.every((item) => item.disabled && !item.ariaChecked && item.ariaLabel.endsWith(": 無効")));
assert.match(checkedJa.disabledInteraction, /BROWSER_ELEMENT_DISABLED/);

for (const screenshot of browser.screenshots) {
  const bytes = fs.readFileSync(path.join(here, screenshot.file));
  assert.equal(bytes.subarray(1, 4).toString("ascii"), "PNG");
  assert.equal(hash(bytes), screenshot.sha256);
}

const verification = read("verification.json");
assert.equal(verification.state, "AWAITING_REVIEW");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
for (const value of Object.values(verification.checks)) assert.equal(value, "PASS");
console.log("LWB317_UI_SWITCH_LOCALE001_EVIDENCE_OK");
