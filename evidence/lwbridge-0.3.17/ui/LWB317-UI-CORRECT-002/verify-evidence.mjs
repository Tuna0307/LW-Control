import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../../..");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const manifest = JSON.parse(fs.readFileSync(path.join(here, "source-manifest.json")));
const contractFile = JSON.parse(fs.readFileSync(path.join(here, "source-contracts.json")));
for (const source of manifest.source_files) assert.equal(hash(fs.readFileSync(path.join(root, source.path))), source.sha256);
for (const contract of contractFile.contracts) {
  const source = path.join(root, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets", contract.source);
  const bytes = fs.readFileSync(source);
  assert.equal(hash(bytes), contract.sha256);
  for (const locator of contract.locators) {
    const anchor = Buffer.from(locator.anchor);
    assert.deepEqual(bytes.subarray(locator.utf8_byte, locator.utf8_byte + anchor.length), anchor, `${contract.id} byte locator drift`);
  }
}
for (const shot of manifest.screenshots) assert.equal(hash(fs.readFileSync(path.join(here, "screenshots", shot.file))), shot.sha256);
const qa = JSON.parse(fs.readFileSync(path.join(here, "browser-qa.json")));
assert.equal(qa.checks.length, 50);
assert.ok(qa.checks.every((check) => check.passed === true));
assert.deepEqual(qa.consoleErrors, []);
console.log(`LWB317_UI_CORRECT002_EVIDENCE_OK ${contractFile.contracts.length} contracts ${manifest.screenshots.length} screenshots ${qa.checks.length} browser assertions/observations`);
