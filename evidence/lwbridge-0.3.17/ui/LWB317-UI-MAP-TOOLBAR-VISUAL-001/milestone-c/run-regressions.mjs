import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const redirect = path.relative(repo, path.join(here, "redirect-replay.mjs"));
const specs = [
  ["navigation-current-shape", redirect, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v3/replay-shape-adapters.mjs", "navigation-current-shape", "navigation"],
  ["request-lifetime", "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs"],
  ["interactions", "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs"],
  ["filter-lifecycle-r1", redirect, "evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v3/replay-r1-redirect.mjs", "filter-lifecycle-r1"],
  ["auto-controls", redirect, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-b/replay-controls.mjs", "auto-controls"],
  ["manual-empty-selection", redirect, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-TYPES-001/check-manual-selection.mjs", "manual-empty-selection"],
  ["table-current-renderers", redirect, "evidence/lwbridge-0.3.17/ui/LWB317-UI-OFFLINE-VISUAL-001/map/build-reference.mjs", "table-current"],
  ["treasure-table-state", "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTERS-001/check-treasure-checking.mjs"],
  ["row-actions", "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/replay-historical.mjs", "--child", "LWB317-UI-MAP-ROWS-001/check-row-actions.mjs", "rows"],
];

const results = [];
for (const [name, script, ...args] of specs) {
  const commandArgs = [script, ...args];
  const run = spawnSync(process.execPath, commandArgs, { cwd: repo, encoding: "utf8", windowsHide: true, maxBuffer: 20 * 1024 * 1024 });
  results.push({
    name,
    command: `node ${commandArgs.join(" ")}`,
    exitCode: run.status,
    stdout: String(run.stdout || "").trim(),
    stderr: String(run.stderr || "").trim(),
  });
  if (run.status !== 0) {
    console.error(`FAIL ${name}\n${run.stdout || ""}\n${run.stderr || ""}`);
    break;
  }
  console.log(`PASS ${name}: ${String(run.stdout || "").trim().split(/\r?\n/).slice(-1)[0] || "exit 0"}`);
}

const report = {
  task: "LWB317-UI-MAP-TOOLBAR-VISUAL-001",
  marker: "LWB317_MAP_TOOLBAR_REGRESSIONS_REPLAYED",
  generatedAt: "2026-10-04",
  historicalWritePolicy: "Scripts that write unconditionally are executed through redirect-replay.mjs; all writes are captured under this task's milestone-c/replays directory.",
  results,
};
fs.writeFileSync(path.join(here, "regression-results.json"), `${JSON.stringify(report, null, 2)}\n`);
assert.equal(results.length, specs.length, "all regression specs executed");
assert.ok(results.every((result) => result.exitCode === 0), "all regression specs pass");
console.log(`LWB317_MAP_TOOLBAR_REGRESSIONS_OK checks=${results.length}`);
