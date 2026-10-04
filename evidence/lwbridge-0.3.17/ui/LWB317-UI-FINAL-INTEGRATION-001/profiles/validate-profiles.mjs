import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const sha = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const result = JSON.parse(fs.readFileSync(path.join(here, "results.json"), "utf8"));
const asset = fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"));
assert.equal(sha(asset), "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(result.sourceSha256, sha(asset));
assert.equal(sha(fs.readFileSync("C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe")), "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
assert.equal(sha(fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx"))), result.productionSha256);
assert.equal(result.cases, result.details.length);
assert.equal(result.cases, 98);
assert.equal(result.helperComparisons, 32);
assert.ok(result.details.every((item) => item.pass));
for (const [name, locator] of Object.entries(result.locators)) {
  assert.equal(sha(asset.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.utf8ByteLength)), locator.sha256, name);
}
const source = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/ProfileSidebar.jsx"), "utf8");
assert.ok(!source.includes("backendBridge") && !source.includes("fetch(") && !source.includes("profile-upgrade"));
console.log(`LWB317_FINAL_PROFILES_EVIDENCE_OK cases=${result.cases} slices=${Object.keys(result.locators).length}`);
