import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const historical = path.resolve(here, "../LWB317-UI-MAP-CLOSEOUT-002/milestone-b/refresh-ownership-results.json");
const checker = path.resolve(here, "../LWB317-UI-MAP-CLOSEOUT-002/milestone-b/check-refresh-ownership.mjs");
const redirected = path.join(here, "regression-refresh-ownership.json");
const before = fs.readFileSync(historical);
const originalWrite = fs.writeFileSync;

fs.writeFileSync = (file, data, ...args) => {
  if (path.resolve(file) === historical) return originalWrite(redirected, data, ...args);
  return originalWrite(file, data, ...args);
};
try {
  await import(`${pathToFileURL(checker).href}?auto-config-refresh-redirect=1`);
} finally {
  fs.writeFileSync = originalWrite;
}
assert.deepEqual(fs.readFileSync(historical), before, "historical ownership result changed during redirected replay");
assert.ok(fs.existsSync(redirected), "redirected ownership result missing");
console.log("LWB317_AUTO_CONFIG_REFRESH_OWNERSHIP_REDIRECT_OK");
