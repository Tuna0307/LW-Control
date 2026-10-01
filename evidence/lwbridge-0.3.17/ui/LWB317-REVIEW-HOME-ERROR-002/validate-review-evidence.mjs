import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");

function sha256(relativePath) {
  return crypto.createHash("sha256").update(fs.readFileSync(path.join(repo, relativePath))).digest("hex").toUpperCase();
}

const results = JSON.parse(fs.readFileSync(path.join(here, "review-results.json"), "utf8"));
const browser = JSON.parse(fs.readFileSync(path.join(here, "browser-review.json"), "utf8"));

assert.equal(results.task, "LWB317-REVIEW-HOME-ERROR-002");
assert.equal(results.recommendation, "CHANGES_REQUIRED");
assert.equal(results.source.sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(results.source.anchors.qr.utf8ByteOffset, 336694);
assert.equal(results.source.anchors.Jt.utf8ByteOffset, 367489);
assert.equal(results.source.anchors.Xt.utf8ByteOffset, 367703);
assert.equal(results.scenarios.length, 8);
assert.ok(results.clearingDifference.finding.includes("five-second loop"));

assert.deepEqual(browser.observations.map(({ fixture }) => fixture), [
  "home-errors-both",
  "home-error-action-missing-root",
]);
assert.deepEqual(browser.consoleErrors, []);
assert.equal(
  sha256("evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-HOME-ERROR-002/both-errors-review.jpg"),
  browser.screenshot.sha256,
);

assert.equal(
  sha256("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"),
  "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6",
);
assert.equal(
  sha256("src/LWBridge.Desktop/GameInstallationService.cs"),
  "31BC28DAF0C0C6879347958A1D052052024FE8602D82695CC2550FC2E3E96911",
);
assert.equal(
  sha256("src/LWBridge.Desktop/LWBridgeWindow.cs"),
  "15397E9857A1EAB8E6D39CE5707F8C6ECC5B66BA63E2169EF7DA330A130238F2",
);
assert.equal(
  sha256("src/LWBridge.Desktop/LocalConfigStore.cs"),
  "A3154052C85D58C7D9E8713227D9EF49745DFC0C3B8EDD75DC32DBAF68E66C9C",
);

console.log("LWB317_REVIEW_HOME_ERROR_002_EVIDENCE_OK");
