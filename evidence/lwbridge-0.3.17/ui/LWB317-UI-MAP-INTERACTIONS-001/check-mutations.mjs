// Negative-control proof for check-interactions.mjs: each deliberate defect in a COPY of the production page must
// be detected by at least one scenario (observation != the recorded observation of the ACTUAL original component).
// Expected observations are read from interaction-results.json (original column), so no production logic is repeated.
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { scenarios } from "./scenarios.mjs";
import { bootCanonicalLike, SOURCES } from "./flavors.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const recorded = JSON.parse(fs.readFileSync(path.join(here, "interaction-results.json"), "utf8"));
const original = Object.fromEntries(recorded.rows.map((row) => [row.id, row.original]));
const stable = (value) => Array.isArray(value) ? value.map(stable)
  : value && typeof value === "object" ? Object.fromEntries(Object.keys(value).sort().map((key) => [key, stable(value[key])])) : value;
const source = fs.readFileSync(SOURCES.current, "utf8").replace(/\r\n/g, "\n");

const mutations = [
  ["tab change keeps the action message (defect found by the independent original-runtime comparison)", "    setQueryError(\"\");\n    setActionMessage(\"\");\n  }", "    setQueryError(\"\");\n  }"],
  ["keyword is a search-effect dependency (typing searches)", "    searchRevision, tab, treasureType,\n", "    searchRevision, tab, treasureType, keyword,\n"],
  ["tab change resets the Truck selection", "    setDispatchSelection((current) => selectionCount(current) === 0 ? current : {});\n", "    setDispatchSelection((current) => selectionCount(current) === 0 ? current : {});\n    setTruckSelection({});\n"],
  ["tab change keeps the Dispatch selection", "    setDispatchSelection((current) => selectionCount(current) === 0 ? current : {});\n", ""],
  ["name select keeps the keyword text", "    else setMonsterNameKey(value);\n    setKeyword(\"\");\n", "    else setMonsterNameKey(value);\n"],
  ["typing keeps the selected name", "    if (tab === \"resource\" && resourceNameKey) setResourceNameKey(\"\");\n", ""],
  ["Search button disabled while loading", "disabled={!backendAvailable || !dataServerId} onClick={submitSearch}", "disabled={!backendAvailable || !dataServerId || loading} onClick={submitSearch}"],
  ["Search on page 1 only resets the page (no direct request)", "    if (page === 1) runSearch(1);\n    else setPage(1);", "    setPage(1);"],
  ["online is not a search-effect dependency", "luckyFirst, mapApi, markedOnly, minLevel, monsterNameKey, online, page,", "luckyFirst, mapApi, markedOnly, minLevel, monsterNameKey, page,"],
  ["server change clears the Dispatch selection", "    setQueryError(\"\");\n  }, [dataServerId]);", "    setQueryError(\"\");\n    setDispatchSelection({});\n  }, [dataServerId]);"],
  ["name options show raw keys (no game-text lookup)", "{lookupMapText(gameTexts, item.key, item.key)} ({item.count})", "{item.key} ({item.count})"],
  ["selection shares one collection between Dispatch and Truck", "selectedKeys={tab === \"truck\" ? truckKeys : dispatchKeys}", "selectedKeys={dispatchKeys}"],
];

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "lwb317-mutants-"));
const results = [];
try {
  for (const [name, from, to] of mutations) {
    assert.equal(source.split(from).length - 1, 1, `mutation anchor must be unique: ${name}`);
    const file = path.join(tmp, `${results.length}.MapDataPage.jsx`);
    fs.writeFileSync(file, source.replace(from, to));
    const detectedBy = [];
    for (const scenario of scenarios) {
      let observation;
      try { observation = { value: stable(await scenario.run({ boot: (options) => bootCanonicalLike(file, "mutant", options) })) }; }
      catch (error) { observation = { error: error.message.split("\n")[0] }; }
      if (JSON.stringify(observation) !== JSON.stringify(original[scenario.id])) detectedBy.push(scenario.id);
    }
    results.push({ mutation: name, detected: detectedBy.length > 0, detectedBy });
  }
} finally {
  fs.rmSync(tmp, { recursive: true, force: true });
}
const missed = results.filter((entry) => !entry.detected);
const output = { status: missed.length ? "FAIL" : "PASS", mutations: results.length, detected: results.length - missed.length, results };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "mutation-results.json"), JSON.stringify(output, null, 2) + "\n");
for (const entry of results) console.log(`${entry.detected ? "DETECTED" : "MISSED  "} ${entry.mutation} (${entry.detectedBy.length} scenarios)`);
console.log(`LWB317_MUTATIONS ${output.status} detected=${output.detected}/${output.mutations}`);
if (missed.length) process.exitCode = 1;
