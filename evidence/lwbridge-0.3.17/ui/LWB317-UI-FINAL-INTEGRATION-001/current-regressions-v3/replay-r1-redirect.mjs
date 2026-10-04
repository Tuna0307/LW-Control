import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const historical = path.resolve(here, "../../LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-results.json");
const checker = path.resolve(here, "../../LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-cases.mjs");
const redirected = path.join(here, "regression-filter-lifecycle-r1.json");
const before = fs.readFileSync(historical);
const originalWrite = fs.writeFileSync;

fs.writeFileSync = (file, data, ...args) => {
  if (path.resolve(file) === historical) return originalWrite(redirected, data, ...args);
  return originalWrite(file, data, ...args);
};
try {
  await import(`${pathToFileURL(checker).href}?auto-config-r1-redirect=1`);
} finally {
  fs.writeFileSync = originalWrite;
}
assert.deepEqual(fs.readFileSync(historical), before, "protected R1 result changed during redirected replay");
assert.ok(fs.existsSync(redirected), "redirected R1 result missing");
console.log("LWB317_AUTO_CONFIG_R1_REDIRECT_OK");
