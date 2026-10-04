import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const readJson = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const source = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/AutomationPage.jsx"), "utf8");
const recoveredCard = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js"), "utf8");

const accepted = readJson("accepted-source-replay.json");
assert.equal(accepted.result, "LWB317_VISUAL_FINAL_UNIT_C_ACCEPTED_SOURCE_REPLAY_OK");
assert.deepEqual(accepted.results.map((entry) => entry.count), [52, 48, 32, 54]);
assert.equal(accepted.results.reduce((sum, entry) => sum + entry.count, 0), 186);

const browser = readJson("browser-current-results.json");
assert.equal(browser.marker, "LWB317_VISUAL_FINAL_UNIT_C_BROWSER_CURRENT_OK");
assert.equal(browser.cases.length, 121);
assert.ok(browser.cases.every((entry) => entry.pass === true));
assert.equal(browser.screenshots.length, 20);
assert.equal(browser.console.filter((entry) => entry.type === "error" || entry.type === "pageerror").length, 0);
for (const shot of browser.screenshots) {
  assert.equal(sha256(shot.path), shot.sha256, `browser screenshot hash ${shot.id}`);
}

const baseline = readJson("enabled-class-baseline.json");
assert.equal(baseline.result, "EXPECTED_FAIL_SOURCE_ENABLED_CLASS_MISSING");
assert.equal(baseline.cases.length, 3);
for (const entry of baseline.cases) {
  assert.equal(entry.current.className.includes("is-enabled"), false);
  assert.equal(entry.sourceClassControl.className.includes("is-enabled"), true);
  assert.equal(sha256(entry.currentScreenshot.path), entry.currentScreenshot.sha256);
  assert.equal(sha256(entry.sourceClassControlScreenshot.path), entry.sourceClassControlScreenshot.sha256);
}

const fixed = readJson("enabled-class-current.json");
assert.equal(fixed.result, "LWB317_VISUAL_FINAL_UNIT_C_ENABLED_CLASS_FIXED");
assert.equal(fixed.cases.length, 3);
assert.equal(fixed.css.recoveredSha256, fixed.css.currentSha256);
for (const entry of fixed.cases) {
  assert.equal(entry.style.className.includes("is-enabled"), true);
  assert.equal(entry.mutationStyle.className.includes("is-enabled"), false);
  assert.notEqual(entry.style.boxShadow, entry.mutationStyle.boxShadow);
  assert.equal(sha256(entry.screenshot.path), entry.screenshot.sha256);
  assert.equal(sha256(entry.mutationScreenshot.path), entry.mutationScreenshot.sha256);
}
const enabledExpression = 'className={`automation-card${enabled ? " is-enabled" : ""}`}';
assert.equal(source.split(enabledExpression).length - 1, 3, "generic/Gather/Trade must all use recovered enabled class");
assert.ok(recoveredCard.includes('className:`automation-card${i?` is-enabled`:``}`'), "recovered shared card enabled class expression missing");

const pixels = readJson("enabled-class-pixels.json");
assert.equal(pixels.result, "LWB317_VISUAL_FINAL_UNIT_C_ENABLED_CLASS_PIXELS_OK");
assert.equal(pixels.caseCount, 3);
assert.equal(pixels.allMutationsDetected, true);
assert.ok(pixels.cases.every((entry) => entry.changedPixels > 0));

const locales = readJson("locale-inventory.json");
assert.equal(locales.result, "LWB317_VISUAL_FINAL_UNIT_C_LOCALES_OK");
assert.equal(locales.locales.length, 9);
assert.equal(locales.keyCount, 363);
assert.deepEqual(locales.fallbackKeys, ["common.loading"]);
assert.equal(locales.partialMissing.length, 0);
for (const item of locales.localeFiles) assert.equal(sha256(path.join(repo, item.path)), item.sha256);

// Campaign-owned replay scripts are re-executed here rather than trusting stale PASS text.
for (const [script, marker] of [
  ["replay-003A.mjs", "LWB317_UI_CORRECT003A_WEEKLY_OK"],
  ["replay-003B.mjs", "LWB317_UI_CORRECT003B_TRADE_SELECTION_OK"],
  ["replay-003C.mjs", "LWB317_UI_CORRECT003C_TRADE_HISTORY_OK"],
  ["replay-003D.mjs", "LWB317_UI_CORRECT003D_TRADE_CROSS_SERVER_OK"],
  ["replay-003E.mjs", "LWB317_UI_CORRECT003E_TRADE_PRESENTATION_OK"],
]) {
  const output = execFileSync(process.execPath, [path.join(here, script)], { cwd: repo, encoding: "utf8" });
  assert.ok(output.includes(marker), `${script} marker`);
}

console.log(JSON.stringify({
  result: "LWB317_VISUAL_FINAL_UNIT_C_VALIDATED",
  sourceComparisons: 186,
  browserAssertions: browser.cases.length,
  screenshots: browser.screenshots.length,
  enabledClassScopes: fixed.cases.length,
  enabledClassChangedPixels: pixels.cases.map((entry) => entry.changedPixels),
  locales: locales.locales.length,
  localeKeys: locales.keyCount,
  consoleErrors: 0,
}, null, 2));
