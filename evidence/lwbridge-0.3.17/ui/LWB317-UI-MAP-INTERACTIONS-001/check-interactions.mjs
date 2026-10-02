// Milestone B differential: the same user-level scenarios (scenarios.mjs + shared driver) run against
//   original  the ACTUAL original component R executed from the asset bytes (original/original-runtime.mjs)
//   current   production src/LWBridge.UI-0.3.17/src/MapDataPage.jsx
//   baseline  immutable pre-campaign canonical page (baseline-3e8617c.MapDataPage.jsx)
// Parity = current observation deep-equals the original observation. The baseline is expected to differ and
// every difference is preserved as distinguishing evidence. Synthetic local responses; not browser/native proof.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { scenarios } from "./scenarios.mjs";
import { bootCanonicalLike, bootOriginal, SOURCES } from "./flavors.mjs";
import { ORIGINAL_SHA256 } from "./original/original-runtime.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const stable = (value) => Array.isArray(value) ? value.map(stable)
  : value && typeof value === "object" ? Object.fromEntries(Object.keys(value).sort().map((key) => [key, stable(value[key])])) : value;
const sha256 = (file) => crypto.createHash("sha256").update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n")).digest("hex");
assert.equal(ORIGINAL_SHA256.toLowerCase(), "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");

const flavors = {
  original: bootOriginal,
  current: (options) => bootCanonicalLike(SOURCES.current, "current", options),
  baseline: (options) => bootCanonicalLike(SOURCES.baseline, "baseline", options),
};
const observed = {};
for (const [name, boot] of Object.entries(flavors)) {
  observed[name] = {};
  for (const scenario of scenarios) {
    try { observed[name][scenario.id] = { value: stable(await scenario.run({ boot })) }; }
    catch (error) { observed[name][scenario.id] = { error: error.message.split("\n")[0] }; }
  }
}
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const rows = scenarios.map((scenario) => ({
  id: scenario.id,
  currentMatchesOriginal: same(observed.current[scenario.id], observed.original[scenario.id]),
  baselineMatchesOriginal: same(observed.baseline[scenario.id], observed.original[scenario.id]),
  original: observed.original[scenario.id],
  current: observed.current[scenario.id],
  baseline: observed.baseline[scenario.id],
}));
const originalErrors = rows.filter((row) => row.original.error);
const currentMismatch = rows.filter((row) => !row.currentMatchesOriginal);
const baselineMismatch = rows.filter((row) => !row.baselineMatchesOriginal);
const output = {
  status: originalErrors.length === 0 && currentMismatch.length === 0 ? "PASS" : "FAIL",
  scope: "User-level scenarios through the persistent hook adapters: ACTUAL original component (asset bytes), production page and the immutable pre-campaign baseline; synthetic deferred local replies and controlled clock. Not browser/native evidence.",
  originalAssetSha256: ORIGINAL_SHA256,
  sources: { current: sha256(SOURCES.current), baseline: sha256(SOURCES.baseline) },
  scenarioCount: scenarios.length,
  currentMismatches: currentMismatch.map((row) => row.id),
  baselineMismatches: baselineMismatch.map((row) => row.id),
  originalErrors: originalErrors.map((row) => ({ id: row.id, error: row.original.error })),
  documentedDifferences: [
    "Original issues one extra search after mount (options reply replaces the name-selection object, a search-effect dependency). Requests are counted relative to the post-mount base. Not reproduced: the canonical page polls options every 5 s, so copying it would refetch continuously.",
  ],
  rows,
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "interaction-results.json"), JSON.stringify(output, null, 2) + "\n");
for (const row of rows) console.log(`${row.currentMatchesOriginal ? "PARITY" : "DIFF  "} ${row.baselineMatchesOriginal ? "(baseline same)" : "(baseline differs)"} ${row.id}`);
console.log(`LWB317_INTERACTIONS ${output.status} scenarios=${rows.length} currentMismatches=${currentMismatch.length} baselineMismatches=${baselineMismatch.length} originalErrors=${originalErrors.length}`);
if (output.status !== "PASS") process.exitCode = 1;
