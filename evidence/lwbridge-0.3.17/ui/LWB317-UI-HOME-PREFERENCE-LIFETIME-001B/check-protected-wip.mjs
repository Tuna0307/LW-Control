import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const baseline = JSON.parse(fs.readFileSync(path.join(here, "protected-wip-start.json"), "utf8"));
const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const checked = [];

for (const item of baseline.paths) {
  const absolute = path.join(repo, item.path);
  assert.ok(fs.existsSync(absolute), `starting WIP path disappeared: ${item.path}`);
  const bytes = fs.readFileSync(absolute);
  assert.equal(bytes.length, item.length, `starting WIP length changed: ${item.path}`);
  assert.equal(sha256(bytes), item.sha256, `starting WIP hash changed: ${item.path}`);
  checked.push({ path: item.path, sha256: item.sha256, length: item.length, protected: item.protected });
}

const report = {
  task: "LWB317-UI-HOME-PREFERENCE-LIFETIME-001B",
  date: "2026-10-04",
  result: "LWB317_HOME_RECONNECT_WIP_GUARD_OK",
  dispatchHead: baseline.dispatchHead,
  checked,
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "protected-wip-final.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ result: report.result, checked: checked.length }, null, 2));
