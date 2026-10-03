import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const repo = path.resolve(here, "../../../../..");
const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const readJson = (relative) => JSON.parse(fs.readFileSync(path.join(root, relative), "utf8"));
const fileHash = (relative) => sha256(fs.readFileSync(path.join(repo, relative)));
function jpegDimensions(bytes) {
  assert.equal(bytes[0], 0xff);
  assert.equal(bytes[1], 0xd8);
  let offset = 2;
  while (offset + 9 < bytes.length) {
    if (bytes[offset] !== 0xff) { offset += 1; continue; }
    const marker = bytes[offset + 1];
    if ([0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf].includes(marker)) {
      return { height: bytes.readUInt16BE(offset + 5), width: bytes.readUInt16BE(offset + 7) };
    }
    if (marker === 0xd8 || marker === 0xd9 || marker === 0x01 || (marker >= 0xd0 && marker <= 0xd7)) {
      offset += 2;
      continue;
    }
    const length = bytes.readUInt16BE(offset + 2);
    assert.ok(length >= 2, `invalid JPEG segment length at ${offset}`);
    offset += 2 + length;
  }
  throw new Error("JPEG dimensions not found");
}

const exe = "C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe";
assert.equal(sha256(fs.readFileSync(exe)), "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
assert.equal(fileHash("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");

const baselinePath = path.join(root, "recovery/baseline-55c9ca1-App.jsx");
const baseline = fs.readFileSync(baselinePath);
const gitBaseline = execFileSync("git", ["cat-file", "blob", "55c9ca118d31a9fcdbc0ec0cf86930474303930e:src/LWBridge.UI-0.3.17/src/App.jsx"], { cwd: repo, encoding: null });
assert.deepEqual(baseline, gitBaseline);
assert.equal(sha256(baseline), "F201E28E4DD678E735AE91B5DFA52D86DF32FCDACB0C0F10C03FE4C767046CDF");

const currentApp = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"));
assert.equal(sha256(currentApp), "7FD95D74D66A762F19DC202E88944C0FD450155104BE33E80B4B3F1A26797F5C");

const recovery = readJson("recovery/differential-results.json");
assert.equal(recovery.result, "LWB317_CROSSSERVER_RECOVERY_DIFFERENTIAL_OK");
assert.equal(recovery.originalCases.length, 9);
assert.equal(recovery.currentCases.length, 12);
const focused = readJson("milestone-b/focused-results.json");
assert.equal(focused.result, "LWB317_CROSSSERVER_FOCUSED_OK");
assert.equal(focused.cases.length, 12);
assert.ok(focused.cases.every((entry) => entry.status === "PASS"));
assert.equal(focused.source.sha256, sha256(currentApp));
const regressions = readJson("milestone-c/regression-results.json");
assert.equal(regressions.result, "LWB317_CROSSSERVER_REGRESSIONS_OK");
assert.equal(regressions.cases.length, 3);
assert.ok(regressions.cases.every((entry) => entry.status === "PASS"));
const checks = readJson("milestone-c/current-checks.json");
assert.equal(checks.result, "LWB317_CROSSSERVER_CURRENT_CHECKS_OK");
assert.equal(checks.checks.length, 7);
assert.ok(checks.checks.every((entry) => entry.status === "PASS"));

const browser = readJson("milestone-c/browser/observations.json");
assert.equal(browser.result, "LWB317_CROSSSERVER_BROWSER_QA_OK");
assert.equal(browser.mainWindow.console.errors, 0);
assert.equal(browser.mainWindow.console.warnings, 0);
assert.equal(browser.narrowWindow.console.errors, 0);
assert.equal(browser.narrowWindow.console.warnings, 0);
assert.deepEqual(browser.narrowWindow.cssViewport, { width: 800, height: 543, devicePixelRatio: 1 });
assert.equal(browser.narrowWindow.fullyInsideMeasuredViewport, true);
for (const shot of [browser.mainWindow.englishLight.screenshot, browser.mainWindow.japaneseDark.screenshot, browser.narrowWindow.screenshot]) {
  const absolute = path.join(here, shot.path.replace(/^browser\//, "browser/"));
  const bytes = fs.readFileSync(absolute);
  assert.equal(sha256(bytes), shot.sha256);
  assert.equal(`${bytes[0].toString(16)}-${bytes[1].toString(16)}-${bytes[2].toString(16)}`.toUpperCase(), "FF-D8-FF");
  assert.deepEqual(jpegDimensions(bytes), { width: shot.width, height: shot.height });
}

for (const commit of ["a74aba9b3a5abaf7a12979c7938e8bcc2ca465c7", "5318467c43598993b8c9cad499643e037fbc7dde"]) {
  execFileSync("git", ["merge-base", "--is-ancestor", commit, "HEAD"], { cwd: repo, stdio: "ignore" });
}
const guard = execFileSync("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs"], { cwd: repo, encoding: "utf8" });
assert.match(guard, /LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7/);

console.log("LWB317_CROSSSERVER_CURRENT_INTEGRITY_OK focused=12 regressions=3 screenshots=3 protected=7");
