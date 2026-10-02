import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const read = (file) => JSON.parse(fs.readFileSync(path.join(here, file), "utf8"));
const hash = (data) => crypto.createHash("sha256").update(data).digest("hex").toUpperCase();
const result = read("independent-results.json");
assert.equal(result.task, "LWB317-REVIEW-TRADE-STATUS-001");
assert.equal(result.recommendation, "AWAITING_REVIEW");
assert.equal(result.renderComparisons.length, 68);
for (const entry of result.renderComparisons) {
  if (entry.original) assert.deepEqual(entry.current, entry.original);
  else assert.equal(entry.pass, true);
}
assert.equal(result.fetchEffectCases.length, 6);
for (const entry of result.fetchEffectCases) assert.equal(entry.pass, true);
assert.equal(result.nativeFenceComparisons.length, 2);
assert.deepEqual(result.nativeFenceComparisons[0].current, result.nativeFenceComparisons[0].original);
assert.equal(result.nativeFenceComparisons[1].pass, true);
const baseline = read("native-fence-baseline.json");
assert.equal(baseline.matches, false);
assert.notDeepEqual(baseline.current, baseline.original);
for (const boundary of [result.producerBoundary, result.fixtures]) {
  const bytes = fs.readFileSync(path.join(repo, boundary.path));
  assert.equal(hash(bytes), boundary.sha256);
  if (boundary.expression) assert.equal(bytes.subarray(boundary.utf8ByteOffset, boundary.utf8ByteOffset + Buffer.byteLength(boundary.expression)).toString("utf8"), boundary.expression);
}
const original = fs.readFileSync(path.join(repo, result.reference.path));
assert.equal(hash(original), result.reference.sha256);
for (const locator of Object.values(result.sourceLocators)) assert.equal(original.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
for (const locale of result.locales) {
  const bytes = fs.readFileSync(path.join(repo, locale.path));
  assert.equal(hash(bytes), locale.sha256);
  assert.equal(locale.entries.length, 12);
  for (const locator of locale.entries) assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
}
// Scope hash permits independent Map integration but does not silently permit
// a change to the reviewed Trade function.
const pages = fs.readFileSync(path.join(repo, result.current.path), "utf8");
const require = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = require("@babel/parser");
const trade = parse(pages, { sourceType: "module", plugins: ["jsx"] }).program.body.find((n) => n.type === "FunctionDeclaration" && n.id?.name === "TradeStationCard");
assert.equal(hash(pages.slice(trade.start, trade.end)), result.current.scopeSha256);
const verification = read("verification.json");
assert.equal(verification.referenceExeHash, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
assert.equal(verification.productEdits, true);
for (const [name, status] of Object.entries(verification.focusedChecks)) assert.equal(status, "PASS", name);
if (!process.argv.includes("--source-only")) {
  const browser = read("browser-results.json");
  assert.deepEqual(browser.consoleErrors, []);
  assert.equal(browser.flows.length, 8);
  const flow = (state, language, tab) => {
    const entry = browser.flows.find((item) => item.state === state && item.language === language && item.tab === tab);
    assert.ok(entry, `${state}/${language}/${tab}`); return entry.observed;
  };
  const loading = flow("automation-trade-loading-retained", "en", "goods");
  assert.match(loading.loadingText, /Loading goods/);
  assert.equal(loading.goodsCount, 2);
  assert.match(loading.stats, /Success/);
  const loadingPurchases = flow("automation-trade-loading-retained", "en", "purchases");
  assert.equal(loadingPurchases.purchaseCount, 2);
  assert.equal(loadingPurchases.loadingText, "");
  const loadingReturn = flow("automation-trade-loading-retained", "en", "goods-return");
  assert.equal(loadingReturn.goodsCount, 2);
  assert.equal(loadingReturn.loadingText, loading.loadingText);
  assert.equal(loadingReturn.stats, loading.stats);
  const failed = flow("automation-trade-error-retained", "ja", "goods");
  assert.equal(failed.errorText, "Error: Fixture Trade goods request failed");
  assert.equal(failed.goodsCount, 2);
  assert.match(failed.stats, /成功/);
  const failedPurchases = flow("automation-trade-error-retained", "ja", "purchases");
  assert.equal(failedPurchases.errorText, failed.errorText);
  assert.equal(failedPurchases.purchaseCount, 2);
  const failedReturn = flow("automation-trade-error-retained", "ja", "goods-return");
  assert.equal(failedReturn.goodsCount, 2);
  assert.equal(failedReturn.errorText, failed.errorText);
  assert.equal(failedReturn.stats, failed.stats);
  const absent = flow("automation-trade-status-absent", "en", "goods");
  assert.match(absent.stats, /Detected: 0[\s\S]*Attempted: 0[\s\S]*Succeeded: 0[\s\S]*Last result: -/);
  const inactive = flow("", "en", "goods");
  assert.match(inactive.stats, /Detected: 0[\s\S]*Attempted: 0[\s\S]*Succeeded: 0[\s\S]*Last result: -/);
  assert.equal(inactive.goodsCount, 0);
  assert.equal(inactive.purchaseCount, 0);
  assert.ok(inactive.controls.length > 0);
  assert.ok(inactive.controls.every((control) => control.disabled));
  assert.equal(browser.inactivePurchaseTabDisabled, true);
  assert.equal(browser.blockedInactiveTabClick, true);
  assert.ok(browser.screenshots.length > 0 && browser.screenshots.length <= 2);
  for (const image of browser.screenshots) {
    assert.equal(image.inspected, true);
    assert.equal(hash(fs.readFileSync(path.join(here, image.file))), image.sha256.toUpperCase());
  }
}
console.log(process.argv.includes("--source-only") ? "LWB317_REVIEW_TRADE_STATUS_SOURCE_EVIDENCE_OK" : "LWB317_REVIEW_TRADE_STATUS_EVIDENCE_OK");
