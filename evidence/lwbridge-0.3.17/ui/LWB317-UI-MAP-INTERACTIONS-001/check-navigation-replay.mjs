// Replay adapter for the historical NAVIGATION-001 scenarios.
//
// The immutable check-navigation.mjs evaluates MapDataPage through its own hard-wired import
// surface and cannot evaluate a page that imports further local modules. This script keeps the
// historical `campaign()` text byte-for-byte (sliced from that file at run time), supplies it the
// harness.mjs hook runtime instead of the stale createHarness, and replays it against the
// pre-navigation baseline (must reproduce the six recorded defects) and current production
// (must have none). The component under test is the unmodified production source.
import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHarness as createReplayHarness, repo } from "./harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const navigation = path.resolve(here, "../LWB317-UI-MAP-NAVIGATION-001");
const historical = fs.readFileSync(path.join(navigation, "check-navigation.mjs"), "utf8");
const start = historical.indexOf("async function campaign(");
const end = historical.indexOf("const originalPath = path.join(repo, contract.source.path);");
assert.ok(start > 0 && end > start, "historical campaign() located");
const campaignText = historical.slice(start, end).replace("const h = createHarness(source, label);", "const h = await createHarness(source, label);");
assert.ok(campaignText.includes("await createHarness(source, label)"));
const sha256 = (value) => crypto.createHash("sha256").update(value.replace(/\r\n/g, "\n")).digest("hex");
const campaign = new Function("assert", "createHarness", `${campaignText}\nreturn campaign;`)(assert, createReplayHarness);

const recorded = JSON.parse(fs.readFileSync(path.join(navigation, "navigation-results.json"), "utf8"));
const baseline = fs.readFileSync(path.join(navigation, "baseline.MapDataPage.jsx"), "utf8");
const currentFile = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const current = fs.readFileSync(currentFile, "utf8");
const baselineResult = await campaign(baseline, "baseline", false);
const currentResult = await campaign(current, "current", true);
assert.deepEqual(currentResult.failures, [], "current production passes every historical navigation scenario");
assert.deepEqual(
  baselineResult.failures.map((failure) => failure.name),
  recorded.baseline.failures.map((failure) => failure.name),
  "baseline reproduces exactly the six recorded NAVIGATION-001 defects through the replay adapter",
);
const output = {
  status: "PASS",
  adapter: "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/harness.mjs",
  historicalCampaignSha256: sha256(campaignText),
  scope: "Historical NAVIGATION-001 campaign() text replayed through the generic hook adapter against the actual production MapDataPage callbacks/effects. Synthetic deferred local responses; not browser/native evidence.",
  baseline: { failures: baselineResult.failures.map((failure) => failure.name), requests: baselineResult.requests.length },
  current: { path: path.relative(repo, currentFile).replaceAll("\\", "/"), sha256LfNormalized: sha256(current), failures: [], requests: currentResult.requests.length },
};
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "navigation-replay-results.json"), JSON.stringify(output, null, 2) + "\n");
console.log(`LWB317_NAVIGATION_REPLAY_OK baseline=${baselineResult.failures.length} current=0 requests=${currentResult.requests.length}`);
