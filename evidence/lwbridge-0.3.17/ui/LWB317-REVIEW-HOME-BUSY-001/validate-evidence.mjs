import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const hash = value => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const readJson = file => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));

const review = readJson("review-results.json");
const browser = readJson("browser-results.json");
const verification = readJson("verification.json");

assert.equal(review.task, "LWB317-REVIEW-HOME-BUSY-001");
assert.equal(review.result, "LWB317_REVIEW_HOME_BUSY001_OK");
assert.deepEqual(review.counts, { distinguishingCases: 17, sourceLocatorChecks: 6 });
assert.equal(review.reference.sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
const source = fs.readFileSync(path.join(repo, review.reference.path));
assert.equal(hash(source), review.reference.sha256);
for (const locator of Object.values(review.reference.locators)) {
  const expression = Buffer.from(locator.expression, "utf8");
  assert.equal(source.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + expression.length).toString("utf8"), locator.expression);
}

const currentApp = fs.readFileSync(path.join(repo, review.current.appPath), "utf8").replace(/\r\n/g, "\n");
assert.equal(hash(currentApp), review.current.appSha256NormalizedLF);
assert.equal(review.current.appSha256NormalizedLF, "BA4A300D525A271C61A8061406CA81DE2414AACC15DF10A728B0E1A8641B2701");
assert.deepEqual([...new Set(review.current.appInputMapping.setHomeBusyWrites)].sort(), ["", "autoLaunchGame", "autoReconnect", "gameRoot"]);
assert.equal(review.current.appInputMapping.productionProxyBusy, false);
assert.equal(review.current.appInputMapping.productionGameLaunchBusy, false);
assert.deepEqual(review.current.appInputMapping.previewOnlyBusyInputs, ["proxyBusy", "gameLaunchBusy"]);
assert.equal(review.historicalEvidence.appHashMatchesSavedReport, false);
assert.equal(review.historicalEvidence.savedAppSha256NormalizedLF, "136C6361B8C8871ECB33ADFB249A6334BD874F94C604E63C85EABC8C0D46CBE7");
assert.equal(review.historicalEvidence.sourceLocatorsVerified, 8);

assert.equal(browser.syntheticBrowserOnly, true);
assert.equal(browser.flows.length, 5);
assert.deepEqual(browser.consoleErrors, []);
const byState = new Map(browser.flows.map(flow => [`${flow.state}/${flow.language}`, flow.observed]));
assert.deepEqual(byState.get("home-root-busy-valid/en"), {
  status: "Game stopped", statusClass: "muted",
  controls: [{ text: "Launch Game", disabled: true }, { text: "Close game", disabled: true }],
  picker: [], switchesDisabled: [true, true],
});
assert.deepEqual(byState.get("home-proxy-busy-stopped/en"), {
  status: "Processing", statusClass: "muted",
  controls: [{ text: "Processing", disabled: true }, { text: "Close game", disabled: true }],
  picker: [], switchesDisabled: [true, true],
});
assert.deepEqual(byState.get("home-proxy-busy-running/en"), {
  status: "Processing", statusClass: "muted",
  controls: [{ text: "Processing", disabled: true }, { text: "Processing", disabled: true }],
  picker: [], switchesDisabled: [true, true],
});
assert.deepEqual(byState.get("home-busy-overlap/en"), {
  status: "Launching game…", statusClass: "muted",
  controls: [{ text: "Launching game…", disabled: true }, { text: "Processing", disabled: true }],
  picker: [], switchesDisabled: [true, true],
});
assert.deepEqual(byState.get("home-launching/ja"), {
  status: "ゲームを起動中…", statusClass: "muted",
  controls: [{ text: "ゲームを起動中…", disabled: true }, { text: "ゲームを閉じる", disabled: true }],
  picker: [], switchesDisabled: [true, true],
});
assert.equal(browser.screenshots.length, 1);
for (const screenshot of browser.screenshots) {
  const bytes = fs.readFileSync(path.join(here, screenshot.file));
  assert.equal(bytes[0], 0xff);
  assert.equal(bytes[1], 0xd8);
  assert.equal(hash(bytes), screenshot.sha256);
}

for (const historical of review.historicalEvidence.screenshots) {
  const bytes = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-BUSY-001", historical.file));
  assert.equal(hash(bytes), historical.sha256);
  assert.equal(historical.verified, true);
}

assert.equal(verification.task, "LWB317-REVIEW-HOME-BUSY-001");
assert.equal(verification.state, "REVIEW_COMPLETE");
assert.equal(verification.verdict, "ACCEPT");
for (const result of Object.values(verification.checks)) assert.equal(result, "PASS");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");

console.log("LWB317_REVIEW_HOME_BUSY001_EVIDENCE_OK");
