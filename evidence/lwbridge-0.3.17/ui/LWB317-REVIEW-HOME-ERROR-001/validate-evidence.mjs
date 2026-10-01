import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));

const cases = readJson("independent-cases.json");
assert.equal(cases.task, "LWB317-REVIEW-HOME-ERROR-001");
assert.equal(cases.result, "REVIEW_HOME_ERROR_MATCH");
assert.equal(cases.reference.sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(cases.reference.locators.Ir.utf8ByteOffset, 328453);
assert.equal(cases.reference.locators.Lr.utf8ByteOffset, 328684);
assert.equal(cases.reference.locators.qr.utf8ByteOffset, 336694);
assert.equal(cases.cases.length, 13);
for (const row of cases.cases) assert.equal(row.actual, row.expected, row.name);
assert.deepEqual(cases.recoveryComposition.actual, cases.recoveryComposition.expected);

const browser = readJson("browser-results.json");
assert.equal(browser.flows.length, 2);
assert.deepEqual(browser.consoleErrors, []);
assert.deepEqual(browser.flows[0].observed.errors, ["The action could not be completed."]);
assert.deepEqual(browser.flows[1].observed.errors, ["原因：操作を完了できませんでした。"]) ;
for (const flow of browser.flows) assert.ok(flow.observed.controls.every((control) => control.disabled));
const screenshot = browser.screenshots[0];
const screenshotBytes = fs.readFileSync(path.join(here, screenshot.file));
assert.equal(screenshotBytes[0], 0xff);
assert.equal(screenshotBytes[1], 0xd8);
assert.equal(hash(screenshotBytes), screenshot.sha256);

const verification = readJson("verification.json");
assert.equal(verification.state, "REVIEW_COMPLETE");
assert.equal(verification.recommendation, "ACCEPT for focused source/local scope");
assert.equal(verification.browserScreenshot.sha256, screenshot.sha256);
for (const result of Object.values(verification.checks)) assert.match(result, /^PASS/);

console.log("LWB317_REVIEW_HOME_ERROR001_EVIDENCE_OK");
