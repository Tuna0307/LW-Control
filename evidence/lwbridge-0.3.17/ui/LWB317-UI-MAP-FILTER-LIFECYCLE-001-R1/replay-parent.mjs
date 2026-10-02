import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const parentDir = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001");
const checker = path.join(parentDir, "check-filter-lifecycle.mjs");
const historical = path.join(parentDir, "filter-lifecycle-results.json");
const output = path.join(here, "parent-filter-lifecycle-results.json");
const historicalBefore = fs.readFileSync(historical);
const writeFileSync = fs.writeFileSync;

fs.writeFileSync = (file, data, ...args) => {
  if (path.resolve(file) === path.resolve(historical)) return writeFileSync(output, data, ...args);
  return writeFileSync(file, data, ...args);
};

try {
  await import(pathToFileURL(checker).href + "?r1-output-redirect=1");
} finally {
  fs.writeFileSync = writeFileSync;
}

assert.deepEqual(fs.readFileSync(historical), historicalBefore, "parent historical result changed during R1 replay");
assert.ok(fs.existsSync(output), "R1 redirected parent result was not written");
console.log("LWB317_UI_MAP_FILTER_LIFECYCLE_R1_PARENT_REPLAY_OK");
