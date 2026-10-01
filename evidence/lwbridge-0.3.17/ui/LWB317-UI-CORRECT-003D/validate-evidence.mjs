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

assert.equal(source.task, "LWB317-UI-CORRECT-003D");
assert.equal(browser.task, source.task);
assert.equal(verification.task, source.task);

assert.equal(sha256(path.join(repo, source.reference.automationPanel.path)), source.reference.automationPanel.sha256);
assert.equal(sha256(path.join(repo, source.reference.sharedUi.path)), source.reference.sharedUi.sha256);
assert.equal(sha256(source.reference.executable.path), source.reference.executable.sha256);
assert.deepEqual(source.reference.automationPanel.byteOffsets, {
  defaultConfig: 400,
  immediateSave: 7213,
  crossServerLabel: 8264,
  crossServerSwitch: 8330,
});

assert.equal(browser.flows.length, 4);
assert.ok(browser.flows.every((flow) => flow.result.pass === true));
assert.deepEqual(browser.screenshots, []);

assert.equal(verification.status, "AWAITING_REVIEW");
assert.equal(verification.checks.focusedCrossServerCheck.result, "LWB317_UI_CORRECT003D_TRADE_CROSS_SERVER_OK");
assert.equal(verification.checks.inheritedTradeSelectionCheck.result, "LWB317_UI_CORRECT003B_TRADE_SELECTION_OK");
assert.equal(verification.checks.inheritedTradeHistoryCheck.result, "LWB317_UI_CORRECT003C_TRADE_HISTORY_OK");
assert.equal(verification.checks.browserQa.result, "PASS");

console.log(JSON.stringify({
  result: "LWB317_UI_CORRECT003D_EVIDENCE_OK",
  sourceHash: source.reference.automationPanel.sha256,
  executableHash: source.reference.executable.sha256,
  browserFlows: browser.flows.map((flow) => flow.name),
}, null, 2));
