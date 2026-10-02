// Read-only replay adapter for the historical Map regression checks.
//
// Why: MapDataPage.jsx now imports mapInteractions.js and ScheduledPlunder.jsx. The preserved checks evaluate
// the production source text inside `new Function(<fixed binding names>, code)`, so the new free identifiers
// (selection helpers, the Scheduled Plunder component) are unresolved, and one check locates the search effect by
// the text `mapApi.search`, which now lives in the extracted `runSearch` function the effect calls.
//
// What this adapter does: (1) defines the new module exports as globals in a child process so the evaluated
// production code resolves them (real mapInteractions.js exports; the JSX component is an inert stub because these
// checks never render it); (2) for check-filters.mjs, only widens the effect locator from `mapApi.search` to also accept
// `runSearch(`; (3) for check-row-actions.mjs, strips the unconditional results write so the immutable
// row-action-results.json is not rewritten. Every historical assertion, reference expression and source file is
// otherwise unchanged and still evaluates the ACTUAL production MapDataPage.jsx. Historical scripts, results and
// manifests are never modified.
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const ui = path.join(repo, "evidence/lwbridge-0.3.17/ui");

if (process.argv[2] === "--child") {
  const [, , , target, mode] = process.argv;
  const interactions = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/mapInteractions.js")).href);
  const presentation = await import(pathToFileURL(path.join(repo, "src/LWBridge.UI-0.3.17/src/mapTablePresentation.js")).href);
  Object.assign(globalThis, interactions, { lookupMapText: presentation.lookupMapText, ScheduledPlunder: function ScheduledPlunder() { return null; } });
  const file = path.join(ui, target);
  process.argv.splice(2); // historical scripts assert they were started without flags
  if (mode === "unchanged") {
    await import(pathToFileURL(file).href);
  } else {
    let text = fs.readFileSync(file, "utf8");
    if (mode === "filters") {
      const before = "entry.fn.toString().includes('mapApi.search')";
      assert.ok(text.includes(before), "filters effect locator present");
      // The campaign also evaluates the preserved baseline, whose effect still contains the request inline.
      text = text.replace(before, "(entry.fn.toString().includes('mapApi.search') || entry.fn.toString().includes('runSearch('))");
    } else if (mode === "rows") {
      const lines = text.split("\n");
      const writes = lines.filter((line) => line.includes("fs.writeFileSync(path.join(here,'row-action-results.json')"));
      assert.equal(writes.length, 1, "exactly one results write");
      text = lines.filter((line) => !writes.includes(line)).join("\n");
    }
    text = text.replace("fileURLToPath(import.meta.url)", JSON.stringify(file));
    text = text.replace(/from '\.\.\/\.\.\/\.\.\/\.\.\/([^']+)'/g, (_, rest) => `from '${pathToFileURL(path.join(repo, rest)).href}'`);
    await import(`data:text/javascript;base64,${Buffer.from(text).toString("base64")}`);
  }
} else {
  const targets = [
    ["LWB317-UI-MAP-FILTERS-001/check-filters.mjs", "filters"],
    ["LWB317-UI-MAP-FILTERS-001/check-table-regression.mjs", "unchanged"],
    ["LWB317-UI-MAP-FILTERS-001/check-treasure-checking.mjs", "unchanged"],
    ["LWB317-UI-MAP-ROWS-001/check-row-actions.mjs", "rows"],
    ["LWB317-REVIEW-MAP-STATES-001/check-review-map-states.mjs", "unchanged"],
  ];
  const results = [];
  for (const [target, mode] of targets) {
    const run = spawnSync(process.execPath, [fileURLToPath(import.meta.url), "--child", target, mode], { cwd: path.join(ui, path.dirname(target)), encoding: "utf8", timeout: 600000 });
    const output = `${run.stdout}${run.stderr}`.trim().split(/\r?\n/).filter(Boolean);
    results.push({ target, mode, exitCode: run.status, lastLine: output.at(-1) || "" });
    console.log(`${run.status === 0 ? "PASS" : "FAIL"} ${target} (${mode}) :: ${output.at(-1) || ""}`);
  }
  const failed = results.filter((entry) => entry.exitCode !== 0);
  if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "historical-replay-results.json"), JSON.stringify({ status: failed.length ? "FAIL" : "PASS", results }, null, 2) + "\n");
  assert.equal(failed.length, 0, `historical replays failed: ${failed.map((entry) => entry.target).join(", ")}`);
}
