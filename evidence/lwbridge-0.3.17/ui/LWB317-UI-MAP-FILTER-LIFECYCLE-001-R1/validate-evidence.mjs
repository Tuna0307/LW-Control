import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const json = (name) => JSON.parse(fs.readFileSync(path.join(here, name), "utf8"));
const rawHash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const lfHash = (file) => crypto.createHash("sha256").update(fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n")).digest("hex");
const git = (...args) => execFileSync("git", args, { cwd: repo, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 }).trim();

const original = path.join(repo, "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js");
assert.equal(rawHash(original), "ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089");

const page = path.join(repo, "src/LWBridge.UI-0.3.17/src/MapDataPage.jsx");
const independent = json("independent-results.json");
assert.equal(independent.source.currentSha256, lfHash(page), "R1 independent results are stale");
assert.equal(independent.cases.length, 5);
assert.ok(independent.cases.every((entry) => entry.pass === true));
const redirect = independent.cases.find((entry) => entry.case.startsWith("redirected options"));
assert.deepEqual(redirect.current.optionServers, [321, 322, 321]);
assert.equal(redirect.current.serverId, 321);
const delayed = json("parent-filter-lifecycle-results.json").corrected.delayedClearAckAfterServerChange;
assert.deepEqual(delayed.optionCalls, [322, 321]);
assert.equal(delayed.browseServerId, 321);

const parentHistorical = "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001/filter-lifecycle-results.json";
assert.equal(git("hash-object", parentHistorical), git("rev-parse", `HEAD:${parentHistorical}`), "parent historical filter result changed");

const browser = json("browser-results.json");
assert.equal(browser.status, "PASS");
assert.equal(browser.serverId, 321);
assert.equal(browser.cityFilterSmoke, true);
assert.equal(browser.treasurePreferenceSmoke, true);
assert.deepEqual(browser.consoleErrors, []);

execFileSync(process.execPath, [
  path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/check-protected-wip.mjs"),
], { cwd: repo, stdio: "pipe" });

console.log("LWB317_UI_MAP_FILTER_LIFECYCLE_R1_EVIDENCE_VALID");
