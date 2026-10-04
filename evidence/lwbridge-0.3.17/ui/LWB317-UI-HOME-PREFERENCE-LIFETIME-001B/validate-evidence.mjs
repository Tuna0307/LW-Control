import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const read = (relative) => JSON.parse(fs.readFileSync(path.join(here, relative), "utf8"));
const normalized = (bytes) => bytes.toString().replace(/\r\n/g, "\n");
const source = read("recovery/source-recovery.json");
const asset = fs.readFileSync(path.join(repo, source.source.path));
assert.equal(hash(asset), source.source.sha256);
assert.equal(source.source.sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(hash(fs.readFileSync("C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe")),
  "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
let locators = 0;
for (const entry of [...Object.values(source.source), ...Object.values(read("lead/source-additions.json").locators)]) {
  if (!entry || typeof entry !== "object" || !Number.isInteger(entry.utf8ByteOffset)) continue;
  assert.equal(hash(asset.subarray(entry.utf8ByteOffset, entry.utf8ByteOffset + entry.byteLength)), entry.sha256);
  locators++;
}
const baseline = read("recovery/baseline-results.json");
assert.equal(baseline.immutableDispatch, "1f117a4e61c35798f98c5d62a275eac163a12be6");
assert.equal(baseline.parityFailures.length, 2);
for (const name of ["app", "home"]) {
  const bytes = execFileSync("git", ["show", `${baseline.immutableDispatch}:${baseline.source[`${name}Path`]}`], { cwd: repo });
  assert.equal(hash(bytes), baseline.source[`${name}Sha256`]);
}
const current = read("milestone-b/current-results.json");
assert.equal(current.result, "LWB317_HOME_RECONNECT_CURRENT_OK");
assert.equal(current.cases.length, 13);
assert.ok(current.cases.every(({ status }) => status === "PASS"));
for (const name of ["app", "home", "shell", "helper"]) {
  assert.equal(hash(normalized(fs.readFileSync(path.join(repo, current.production[`${name}Path`])))), current.production[`${name}Sha256`]);
}
const unitA = read("regression/current-results.json");
assert.equal(unitA.result, "LWB317_UNIT_A_CURRENT_REGRESSION_OK");
assert.equal(unitA.cases.length, 7);
assert.ok(unitA.cases.every(({ status }) => status === "PASS"));
assert.equal(unitA.production.appSha256, current.production.appSha256);
assert.equal(read("regression/preservation-results.json").unchangedCallbacks.length, 9);
assert.deepEqual(read("lead/receive-identity-results.json").current, read("lead/receive-identity-results.json").original);
const native = read("native-boundary/native-boundary-results.json");
assert.equal(native.result, "LWB317_HOME_RECONNECT_NATIVE_BOUNDARY_OK");
for (const entry of Object.values(native.files)) assert.equal(hash(fs.readFileSync(path.join(repo, entry.path))), entry.sha256);
const workerBrowser = read("browser-results.json"), browser = read("lead/browser-results.json");
assert.equal(workerBrowser.result, "PASS");
for (const screenshot of workerBrowser.settledScreenshots) {
  const bytes = fs.readFileSync(path.join(here, screenshot.path));
  assert.equal(hash(bytes), screenshot.sha256); assert.equal(bytes.length, screenshot.length);
}
assert.equal(browser.result, "PASS");
assert.equal(browser.console.en.length, 0); assert.equal(browser.console.ja.length, 0);
assert.equal(browser.steps.length, 7);
assert.equal(browser.steps.find(({ case: name }) => name === "EN clean changed-identity status event").checked, "true");
for (const entry of browser.steps.filter((item) => /clear/.test(item.case))) assert.equal(entry.errorCount, 0);
for (const entry of read("protected-wip-start.json").paths) {
  const bytes = fs.readFileSync(path.join(repo, entry.path));
  assert.equal(hash(bytes), entry.sha256); assert.equal(bytes.length, entry.length);
}
const manifest = read("evidence-manifest.json");
for (const entry of manifest.files) {
  const raw = fs.readFileSync(path.join(here, entry.path));
  const bytes = entry.normalization === "LF" ? Buffer.from(normalized(raw)) : raw;
  assert.equal(hash(bytes), entry.sha256, `evidence changed: ${entry.path}`);
  assert.equal(bytes.length, entry.length);
  if (entry.path.endsWith(".json")) JSON.parse(bytes);
}
console.log(`LWB317_HOME_RECONNECT_EVIDENCE_OK locators=${locators} cases=13 regression=7 pinned=${manifest.files.length} protected=10`);
