import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url));
const evidence = path.resolve(here, "..");
const root = path.resolve(here, "../../../../..");
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const sources = JSON.parse(fs.readFileSync(path.join(evidence, "source-locators.json")));
const anchorIssues = [];
for (const source of sources.assets) {
  const bytes = fs.readFileSync(path.join(root, source.path));
  assert.equal(hash(bytes), source.sha256);
  for (const locator of source.locators) {
    assert.ok(bytes.includes(Buffer.from(locator.excerpt)));
    const text = Buffer.from(locator.anchor ?? locator.excerpt);
    if (locator.utf8ByteOffset !== undefined && !bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + text.length).equals(text)) anchorIssues.push({ file: source.path, meaning: locator.meaning, recordedByte: locator.utf8ByteOffset, excerptStartByte: bytes.indexOf(text) });
    else assert.ok(bytes.includes(text));
  }
}
const browser = JSON.parse(fs.readFileSync(path.join(evidence, "browser-results.json")));
for (const shot of browser.screenshots) assert.equal(hash(fs.readFileSync(path.join(evidence, shot.path))), shot.sha256);
console.log(JSON.stringify({ sourceHashesMatch: true, screenshotHashesMatch: true, anchorIssues }, null, 2));
if (anchorIssues.length) process.exitCode = 1;
