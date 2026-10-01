import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex").toUpperCase();

const source = readJson("source-locators.json");
const browser = readJson("browser-results.json");
const verification = readJson("verification.json");

assert.equal(source.task, "LWB317-UI-CORRECT-003C");
assert.equal(browser.task, source.task);
assert.equal(verification.task, source.task);

const sourcePath = path.join(repo, source.reference.automationPanel.path);
assert.equal(sha256(sourcePath), source.reference.automationPanel.sha256);
assert.equal(sha256(source.reference.executable.path), source.reference.executable.sha256);

assert.equal(browser.screenshots.length, 2);
for (const screenshot of browser.screenshots) {
  assert.equal(sha256(path.join(here, screenshot.path)), screenshot.sha256);
}
const jpgs = fs.readdirSync(here).filter((entry) => entry.toLowerCase().endsWith(".jpg")).sort();
assert.deepEqual(jpgs, browser.screenshots.map((entry) => entry.path).sort());

assert.equal(verification.status, "AWAITING_REVIEW");
assert.equal(verification.checks.focusedHistoryCheck.result, "LWB317_UI_CORRECT003C_TRADE_HISTORY_OK");
assert.equal(verification.checks.inheritedTradeSelectionCheck.result, "LWB317_UI_CORRECT003B_TRADE_SELECTION_OK");

console.log(JSON.stringify({
  result: "LWB317_UI_CORRECT003C_EVIDENCE_OK",
  sourceHash: source.reference.automationPanel.sha256,
  executableHash: source.reference.executable.sha256,
  screenshotHashes: Object.fromEntries(browser.screenshots.map((entry) => [entry.path, entry.sha256])),
}, null, 2));
