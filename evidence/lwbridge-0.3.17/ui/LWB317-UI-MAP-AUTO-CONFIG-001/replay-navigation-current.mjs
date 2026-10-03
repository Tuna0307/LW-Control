import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import * as autoConfig from "../../../../src/LWBridge.UI-0.3.17/src/mapAutoConfig.js";
import * as interactions from "../../../../src/LWBridge.UI-0.3.17/src/mapInteractions.js";
import * as scanPresentation from "../../../../src/LWBridge.UI-0.3.17/src/mapScanPresentation.js";
import * as tablePresentation from "../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const checker = path.resolve(here, "../LWB317-UI-MAP-NAVIGATION-001/check-navigation.mjs");
const historicalResult = path.resolve(here, "../LWB317-UI-MAP-NAVIGATION-001/navigation-results.json");
const before = fs.existsSync(historicalResult) ? fs.readFileSync(historicalResult) : null;

// NAVIGATION-001 predates several later Map imports. Its harness strips imports
// before evaluating the actual production MapDataPage body, so expose the real
// production JS helper exports for that evaluation without changing the
// accepted historical checker or copying helper formulas into this adapter.
//
// These component values are identities only: the navigation harness creates
// JSX nodes for them but never invokes their component bodies. Their behavior
// remains covered by the maintained focused component regressions that the
// milestone-D runner executes separately.
const componentIdentities = {
  ScheduledPlunder: function ScheduledPlunder() {},
  MapTreasureTypeFilter: function MapTreasureTypeFilter() {},
  MapRetainedGoodsFilter: function MapRetainedGoodsFilter() {},
};
const injected = Object.entries({
  ...autoConfig,
  ...interactions,
  ...scanPresentation,
  ...tablePresentation,
  ...componentIdentities,
});
for (const [name, value] of injected) {
  assert.equal(Object.prototype.hasOwnProperty.call(globalThis, name), false, `unexpected global collision: ${name}`);
  globalThis[name] = value;
}

try {
  await import(`${pathToFileURL(checker).href}?auto-config-current-navigation=1`);
} finally {
  for (const [name] of injected) delete globalThis[name];
}

if (before !== null) {
  assert.deepEqual(
    fs.readFileSync(historicalResult),
    before,
    "historical navigation result changed during current adapter replay",
  );
}

console.log("LWB317_AUTO_CONFIG_NAVIGATION_CURRENT_OK");
