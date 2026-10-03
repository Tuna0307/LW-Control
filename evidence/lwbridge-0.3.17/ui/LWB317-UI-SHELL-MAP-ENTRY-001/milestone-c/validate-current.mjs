import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex").toUpperCase();
const load = (file) => JSON.parse(fs.readFileSync(file, "utf8"));

const expectedHashes = new Map([
  ["C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe", "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783"],
  ["evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js", "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6"],
  ["src/LWBridge.UI-0.3.17/src/App.jsx", "210842F74C49E0195C7684A7B1CFBEA24D22B516DF87C3082038559304836131"],
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-c/browser/en-light-crossserver.jpg", "2603B8DC010E2E985399251D4B64AAF4E6A23DC44C9DCD0850A1F43345925F0C"],
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-c/browser/ja-dark-map-truck.jpg", "098A8A3443D582001B01D2F311092B2AA1555A56DCD62DE64B9D23921ED0ED1E"],
  ["evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-c/browser/ja-dark-narrow-crossserver.jpg", "A61CD1448E14314B7A64802C104AD99B1BF225A1C69B75B4D7103E9947E8E211"],
]);

for (const [name, expected] of expectedHashes) {
  const file = path.isAbsolute(name) ? name : path.join(repo, name);
  assert.equal(sha256(file), expected, `hash ${name}`);
}

const manifest = load(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/recovery/source-manifest.json"));
assert.equal(manifest.referenceExe.sha256, expectedHashes.get("C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe"));
assert.equal(manifest.originalAsset.sha256, expectedHashes.get("evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js"));
assert.equal(manifest.originalLocators.mapEntryFunction.byteStart, 364377);
assert.equal(manifest.originalLocators.mapEntryFunction.byteEndExclusive, 364550);
assert.match(manifest.originalLocators.mapEntryFunction.utf8, /map-data.*_t\(\)\.catch.*Wi\(e\).*c\(\(\)=>/s);

const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");
const selectStart = app.indexOf("const selectRoute = useCallback");
const summaryStart = app.indexOf('if (routeKey === "map-data") refreshMapSummary().catch(() => {});', selectStart);
const transitionStart = app.indexOf("startRouteTransition(() => {", selectStart);
assert.ok(selectStart >= 0 && summaryStart > selectStart && transitionStart > summaryStart, "current Map entry starts summary before transition");
assert.ok(!app.includes('if (activeRoute === "map-data" && backendBridge.available) refreshMapSummary().catch(() => {});'), "old post-route entry effect removed");

const focused = load(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-b/focused-results.json"));
assert.equal(focused.result, "LWB317_MAP_ENTRY_FOCUSED_OK");
assert.equal(focused.cases.length, 10);
const affected = load(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-b/affected-results.json"));
assert.equal(affected.result, "LWB317_MAP_ENTRY_AFFECTED_OK");
for (const check of affected.acceptedChecks) assert.equal(check.historicalSha256Before, check.historicalSha256After, `${check.name} historical output preserved`);
const browser = load(path.join(here, "browser/observations.json"));
assert.equal(browser.result, "LWB317_MAP_ENTRY_BROWSER_OK");
assert.equal(browser.previewLanguageQueryUsed, false);
assert.equal(browser.japaneseDarkNarrow.cssViewport.width, 800);
assert.equal(browser.japaneseDarkNarrow.cssViewport.height, 543);
assert.equal(browser.englishLight.consoleErrors, 0);
assert.equal(browser.japaneseDarkNarrow.consoleErrors, 0);
const checks = load(path.join(here, "current-checks.json"));
assert.equal(checks.result, "LWB317_MAP_ENTRY_CURRENT_CHECKS_OK");
assert.ok(checks.checks.every((check) => check.status === "PASS"));

console.log(`LWB317_MAP_ENTRY_DELIVERY_INTEGRITY_OK hashes=${expectedHashes.size} checks=${checks.checks.length}`);
