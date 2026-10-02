import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const hash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const json = (file) => JSON.parse(fs.readFileSync(file, "utf8"));

const contract = json(path.join(here, "source-contract.json"));
const results = json(path.join(here, "navigation-results.json"));
const browser = json(path.join(here, "browser-results.json"));
const consoleErrors = json(path.join(here, "console-errors.json"));

assert.equal(contract.evidenceState, "EXACT_BYTES");
assert.equal(contract.cacheHelper.byte, 554);
assert.equal(contract.serverTransitionEffect.byte, 33502);
assert.equal(contract.tabSelectionEffect.byte, 34677);
assert.equal(contract.search.byte, 38513);
assert.equal(contract.tabHandler.byte, 39238);
assert.equal(hash(path.join(repo, contract.source.path)), contract.source.sha256);

assert.equal(results.status, "PASS");
assert.equal(results.baseline.sha256, "2457d4a801385a74165de69c4e4961882529ee7cf9c36a050fcad0100a3da51b");
assert.equal(hash(path.join(repo, results.baseline.path)), results.baseline.sha256);
assert.ok(results.baseline.failures.length >= 1);
assert.deepEqual(results.current.failures, []);
assert.equal(hash(path.join(repo, results.current.path)), results.current.sha256);

assert.equal(browser.status, "PASS");
assert.equal(browser.fixture, "map-truck");
assert.equal(browser.online, false);
assert.equal(browser.serverId, 321);
assert.equal(browser.truckTotal, 55);
assert.equal(browser.initialRow, "Truck Owner 1");
assert.ok(browser.navigation.some((entry) => entry.observedPage === "Page 2 of 2" && entry.observedRow === "Truck Owner 51"));
assert.ok(browser.navigation.some((entry) => entry.observedTab === "Scheduled Plunder0" && entry.normalPaginationPresent === false));
assert.equal(browser.consoleErrors, 0);
assert.deepEqual(consoleErrors.entries, []);
for (const screenshot of browser.screenshots) {
  assert.equal(hash(path.join(here, screenshot.path)), screenshot.sha256);
}

const protectedManifest = json(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/protected-wip.json"));
for (const entry of protectedManifest) {
  const target = path.join(repo, entry.path);
  assert.equal(fs.statSync(target).size, entry.bytes, entry.path);
  assert.equal(hash(target), entry.sha256, entry.path);
}

console.log("LWB317_MAP_NAVIGATION_EVIDENCE_OK");
