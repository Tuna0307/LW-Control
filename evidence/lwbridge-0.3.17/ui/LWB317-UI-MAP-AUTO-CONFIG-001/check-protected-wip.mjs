import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const before = JSON.parse(fs.readFileSync(path.join(here, "protected-wip-before.json"), "utf8"));
const after = JSON.parse(fs.readFileSync(path.join(here, "protected-wip-after.json"), "utf8"));
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");

assert.equal(after.status, "PASS");
assert.equal(after.paths.length, before.paths.length);
for (const expected of before.paths) {
  const recorded = after.paths.find((entry) => entry.path === expected.path);
  assert.ok(recorded, "missing after-hash record: " + expected.path);
  assert.equal(recorded.sha256, expected.sha256, "recorded hash drift: " + expected.path);
  assert.equal(sha256(path.join(repo, expected.path)), expected.sha256, "protected WIP changed: " + expected.path);
}

console.log("LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=" + before.paths.length);
