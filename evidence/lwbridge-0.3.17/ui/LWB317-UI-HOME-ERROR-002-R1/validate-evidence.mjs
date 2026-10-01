import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const results = JSON.parse(fs.readFileSync(path.join(here, "results.json"), "utf8"));
const browser = JSON.parse(fs.readFileSync(path.join(here, "browser-results.json"), "utf8"));
const verification = JSON.parse(fs.readFileSync(path.join(here, "verification.json"), "utf8"));
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

assert.equal(results.task, "LWB317-UI-HOME-ERROR-002-R1");
assert.equal(results.result, "LWB317_HOME_ERROR_002_R1_OK");
assert.equal(results.scenarios.length, 14);
assert.equal(results.source.sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(results.source.Jt.utf8ByteOffset, 367489);
assert.equal(results.source.initialRootAcknowledgement.utf8ByteOffset, 369540);
assert.equal(results.source.periodicStatusPoll.utf8ByteOffset, 369979);
assert.equal(results.source.selectedProfileEffect.utf8ByteOffset, 369239);
assert.equal(results.source.selectedProfileEffect.dependencies, "[r.selectedProfileId]");
assert.equal(results.production.selectedProfileExpression, "backendBridge.profileId");
assert.equal(results.production.rootEffect.dependencies, "[acknowledgeGameRootStatus, selectedProfileId]");
assert.ok(!results.production.callbacks.refreshStatus.includes("game_root_status"));
assert.deepEqual(
  results.scenarios.find(({ name }) => name === "five-second refresh inventory excludes root status").requests.map(({ command }) => command),
  ["game_recovery_status", "local_config_get"],
);
assert.equal(results.originalJtAfterSuccess.rootError, "");

const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8").replace(/\r\n/g, "\n");
assert.equal(sha256(app), verification.appSha256NormalizedLF);
assert.equal(verification.state, "AWAITING_REVIEW");
assert.ok(Object.values(verification.checks).every((value) => value === "PASS"));
assert.equal(verification.counts.r1SyntheticScenarios, 14);
assert.equal(verification.buildFingerprint, "8055b198949dcac217b15c7b8dac40e6f965847ef472906cf27b1a68388aeb0b");
assert.equal(verification.packageFingerprint, "3a01e609bf7f5281757cf843181bc2c2a1fdf5f544a62a1fae50b87a12616cb4");

assert.deepEqual(browser.observations.map(({ fixture }) => fixture), ["home-errors-both", "home-error-action-missing-root"]);
assert.deepEqual(browser.consoleErrors, []);
assert.ok(browser.observations.every(({ chooseFolderDisabled, preferenceTogglesDisabled }) => chooseFolderDisabled && preferenceTogglesDisabled));
assert.equal(browser.nativeControlsInvoked, false);

console.log("LWB317_UI_HOME_ERROR_002_R1_EVIDENCE_OK");
