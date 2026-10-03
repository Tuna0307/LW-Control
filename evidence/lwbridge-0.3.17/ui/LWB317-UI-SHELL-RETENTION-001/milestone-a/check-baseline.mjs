import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const baseline = JSON.parse(fs.readFileSync(path.join(here, "baseline-results.json"), "utf8"));

assert.equal(baseline.productCheckpoint, "35b5cab9ce42eb8514145ce815af0432e48e4c10");
assert.equal(execFileSync("git", ["rev-parse", `${baseline.productCheckpoint}:src/LWBridge.UI-0.3.17/src/App.jsx`], { cwd: repo, encoding: "utf8" }).trim(), baseline.appGitBlob);
assert.equal(baseline.journey.beforeLeave.selectedEquipmentPreset, "Fixture equipment preset 2");
assert.equal(baseline.journey.whileHome.equipmentDomPresent, false);
assert.equal(baseline.journey.afterReturn.selectedEquipmentPreset, "Fixture equipment preset 1");
assert.equal(baseline.observations.stateLossReproduced, true);
assert.equal(baseline.observations.previousPageUnmountedWhileAway, true);
assert.equal(baseline.observations.visibleEffectCleanupObserved, true);
assert.equal(baseline.observations.effectReinstallObservedOnRemount, true);

console.log("LWB317_SHELL_BASELINE_OK expected_state_loss=true expected_unmount=true");
