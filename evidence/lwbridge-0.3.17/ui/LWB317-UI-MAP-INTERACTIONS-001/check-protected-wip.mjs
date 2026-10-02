// Compares the pinned protected-WIP manifest (LWB317-UI-LEAD-TABLES-001/protected-wip.json) with the
// working tree and verifies that no protected path is staged or tracked-modified by a commit.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const manifest = JSON.parse(fs.readFileSync(path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/protected-wip.json"), "utf8"));
const changed = [];
for (const entry of manifest) {
  const file = path.join(repo, entry.path);
  const bytes = fs.readFileSync(file);
  const sha = crypto.createHash("sha256").update(bytes).digest("hex");
  if (sha !== entry.sha256 || bytes.length !== entry.bytes) changed.push({ path: entry.path, expected: entry.sha256, actual: sha });
}
assert.deepEqual(changed, [], "protected WIP bytes must match the pinned manifest");
const staged = execFileSync("git", ["diff", "--cached", "--name-only"], { cwd: repo, encoding: "utf8" }).split(/\r?\n/).filter(Boolean);
const protectedPrefixes = ["src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js", ".scratch-lwb317/", "evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/"];
const leaked = staged.filter((file) => protectedPrefixes.some((prefix) => file === prefix || file.startsWith(prefix)));
assert.deepEqual(leaked, [], "protected WIP must not be staged");
console.log(`LWB317_PROTECTED_WIP_OK files=${manifest.length} staged=${staged.length}`);
