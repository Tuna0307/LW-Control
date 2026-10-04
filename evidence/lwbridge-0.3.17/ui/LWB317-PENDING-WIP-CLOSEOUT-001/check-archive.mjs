import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const manifest = JSON.parse(fs.readFileSync(path.join(here, "archive-manifest.json"), "utf8"));
const baseline = JSON.parse(fs.readFileSync(path.join(repo, manifest.legacyBaseline), "utf8"));
assert.equal(manifest.items.length, baseline.paths.length);
for (const item of manifest.items) {
  const original = baseline.paths.find((row) => row.path === item.originalPath);
  assert.ok(original, item.originalPath);
  assert.equal(item.sha256, original.sha256);
  assert.equal(item.length, original.length);
  const archive = path.resolve(repo, item.archivePath);
  assert.ok(archive.startsWith(path.resolve(here, "archive") + path.sep));
  const data = fs.readFileSync(archive);
  assert.equal(data.length, item.length);
  assert.equal(crypto.createHash("sha256").update(data).digest("hex").toUpperCase(), item.sha256);
}
console.log(`LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=${manifest.items.length}`);
