// Rerun the actual mounted worker interaction suite with only output paths/owned port changed.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const target = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-b/browser-interactions.mjs");
const temporary = path.join(path.dirname(target), ".lead-review-mounted.mjs");
assert.ok(!fs.existsSync(temporary), "do not replace another review's file");
const output = path.join(here, "mounted");
fs.mkdirSync(output, { recursive: true });
const original = fs.readFileSync(target, "utf8");
const adaptations = [
  ['const screenshots = path.join(here, "screenshots");', `const screenshots = ${JSON.stringify(output)};`],
  ['const baseUrl = "http://127.0.0.1:4336/";', 'const baseUrl = "http://127.0.0.1:4346/";'],
  ['path.join(here, "browser-interactions.json")', JSON.stringify(path.join(output, "browser-interactions.json"))],
];
let source = original;
for (const [before, after] of adaptations) {
  assert.ok(source.includes(before), `adapter anchor ${before}`);
  source = source.replace(before, after);
}
fs.writeFileSync(temporary, source);
let run;
try {
  run = spawnSync(process.execPath, [temporary], { cwd: repo, windowsHide: true, encoding: "utf8", maxBuffer: 4 * 1024 * 1024 });
} finally {
  fs.unlinkSync(temporary);
}
const result = {
  marker: "LWB317_LEAD_TOOLBAR_MOUNTED_REPLAY",
  sourceSha256: crypto.createHash("sha256").update(original).digest("hex"),
  adaptations: adaptations.map(([before, after]) => ({ before, after })),
  port: 4346,
  exitCode: run.status,
  stdout: run.stdout,
  stderr: run.stderr,
};
fs.writeFileSync(path.join(here, "mounted-replay.json"), JSON.stringify(result, null, 2) + "\n");
console.log(run.stdout);
if (run.stderr) console.error(run.stderr);
assert.equal(run.status, 0, "actual mounted interaction suite");
