import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

// Reuse the delivered persistent hook runtime, not its campaign assertions.
// Add one independent lifecycle input: backend availability during a pending search.
const here = path.dirname(fileURLToPath(import.meta.url));
const navigation = path.resolve(here, "../LWB317-UI-MAP-NAVIGATION-001");
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
let runtime = fs.readFileSync(path.join(navigation, "check-navigation.mjs"), "utf8");
runtime = runtime.slice(0, runtime.indexOf("async function campaign("));
assert.ok(runtime.includes("function createHarness(source, label)"));
runtime = runtime.replace(
  'const here = path.dirname(fileURLToPath(import.meta.url));',
  `const here = ${JSON.stringify(navigation)};`,
).replace(
  "    label,\n    requests,",
  "    label,\n    setProps: async (next) => { Object.assign(props, next); await settle(); },\n    requests,",
);
assert.ok(runtime.includes("setProps: async"));
runtime += "\nexport { createHarness };\n";
const temporaryRuntime = path.join(here, ".availability-runtime.mjs");
fs.writeFileSync(temporaryRuntime, runtime);
try {
  const { createHarness } = await import(pathToFileURL(temporaryRuntime).href);
  const repo = path.resolve(here, "../../../..");
  const results = [];
  for (const [label, sourcePath] of [
    ["pre-navigation baseline", path.join(navigation, "baseline.MapDataPage.jsx")],
    ["navigation delivery", path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx")],
  ]) {
    for (const outcome of ["success", "failure"]) {
      const harness = createHarness(fs.readFileSync(sourcePath, "utf8"), label);
      await harness.mount();
      const pending = harness.currentRequest();
      await harness.setProps({ backendAvailable: false });
      const before = {
        rows: structuredClone(harness.getState("rows")),
        total: harness.getState("total"),
        loading: harness.getState("loading"),
        queryError: harness.getState("queryError"),
      };
      if (outcome === "success") {
        await harness.resolveRequest(pending, { rows: [{ recordKey: "obsolete-after-unavailable" }], total: 1 });
      } else {
        await harness.rejectRequest(pending, new Error("obsolete-after-unavailable"));
      }
      const after = {
        rows: harness.getState("rows"),
        total: harness.getState("total"),
        loading: harness.getState("loading"),
        queryError: harness.getState("queryError"),
      };
      results.push({ label, outcome, before, after, staleReplyIgnored: JSON.stringify(before) === JSON.stringify(after) });
    }
  }
  assert.equal(results.filter((r) => r.label === "pre-navigation baseline" && r.staleReplyIgnored).length, 2);
  const deliveryFailures = results.filter((r) => r.label === "navigation delivery" && !r.staleReplyIgnored);
  const output = {
    historicalReviewTarget: "305240e22c963f64fe4f6522696792acbed2db91",
    harnessSha256: sha256(path.join(navigation, "check-navigation.mjs")),
    baselineSha256: sha256(path.join(navigation, "baseline.MapDataPage.jsx")),
    currentSha256: sha256(path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx")),
    scope: "Actual component callbacks/effects in delivered persistent hook runtime; added availability prop transition. Synthetic local responses; no native execution or browser proof.",
    results,
    deliveryFailures: deliveryFailures.length,
  };
  if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "availability-results.json"), JSON.stringify(output, null, 2) + "\n");
  console.log(JSON.stringify(output, null, 2));
} finally {
  fs.unlinkSync(temporaryRuntime);
}
