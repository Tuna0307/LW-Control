import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const scripts = [
  path.relative(repo, path.join(here, "replay-navigation-current.mjs")),
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-SCAN-HEADER-001/check-header.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-GOODS-PICKER-001/check-picker.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TREASURE-PICKER-001/check-picker.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-REFRESH-FEEDBACK-001/check-feedback.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/scheduled/check-scheduled-plunder.mjs",
  path.relative(repo, path.join(here, "replay-r1-redirect.mjs")),
  path.relative(repo, path.join(here, "check-refresh-ownership-current.mjs")),
];

const results = scripts.map((script) => {
  const run = spawnSync(process.execPath, [script], { cwd: repo, encoding: "utf8" });
  return {
    script: script.replaceAll("\\", "/"),
    exitCode: run.status,
    stdout: run.stdout.trim(),
    stderr: run.stderr.trim(),
  };
});
for (const result of results) {
  assert.equal(result.exitCode, 0, `${result.script}\n${result.stdout}\n${result.stderr}`);
}
const output = {
  status: "PASS",
  scope: "Focused accepted Map regressions affected by App/Map ownership and Auto-card edits. Historical writers are either invoked without record mode or redirected into this packet. Synthetic/offline only; no native gameplay execution.",
  count: results.length,
  results,
};
fs.writeFileSync(path.join(here, "regression-results.json"), `${JSON.stringify(output, null, 2)}\n`);
for (const result of results) console.log(`PASS ${result.script}`);
console.log(`LWB317_AUTO_CONFIG_REGRESSIONS_OK count=${results.length}`);
