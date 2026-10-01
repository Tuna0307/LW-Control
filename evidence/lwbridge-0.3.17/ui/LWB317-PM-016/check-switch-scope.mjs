import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)), repo = path.resolve(here, "../../../..");
const submitted = "47c243af1a311e331b6ac0f51d84e16782f32c47", baseline = "1cfce34523b19c7fe0e40cc00466a8f3ba7ffe75";
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const normalized = text => text.replace(/\r\n/g, "\n");
const pagesPath = "src/LWBridge.UI-0.3.17/src/Pages.jsx";
const committed = normalized(execFileSync("git", ["show", `${submitted}:${pagesPath}`], { cwd: repo, encoding: "utf8" }));
const previous = normalized(execFileSync("git", ["show", `${baseline}:${pagesPath}`], { cwd: repo, encoding: "utf8" }));
const current = fs.readFileSync(path.join(repo, pagesPath), "utf8");
assert.equal(normalized(current), committed, "reviewed product file must match submitted commit");
const additions = [
  ['function ToggleRow({ label, checked = false, disabled = false, onChange }) {\n  const { t } = useI18n();', 'function ToggleRow({ label, checked = false, disabled = false, onChange }) {'],
  ['aria-label={`${label}: ${t(checked ? "common.enabled" : "common.disabled")}`}', 'aria-label={`${label}: ${checked ? "Enabled" : "Disabled"}`}'],
];
let reverted = committed;
for (const [after, before] of additions) { assert.equal(reverted.split(after).length - 1, 1); reverted = reverted.replace(after, before); }
assert.equal(reverted, previous, "every caller, handler, predicate and all other Pages bytes are unchanged");
const changedProductFiles = execFileSync("git", ["diff", "--name-only", baseline, submitted, "--", "src"], { cwd: repo, encoding: "utf8" }).trim().split(/\r?\n/);
assert.deepEqual(changedProductFiles, [pagesPath]);
const evidence = path.join(repo, "evidence/lwbridge-0.3.17/ui/LWB317-UI-SWITCH-LOCALE-001");
const results = JSON.parse(fs.readFileSync(path.join(evidence, "switch-results.json"), "utf8"));
for (const [file, locators] of [[results.reference, [results.reference.Bn]], [results.production, [results.production.ToggleRow, ...results.callerInventory]]]) {
  const bytes = fs.readFileSync(path.join(repo, file.path)); assert.equal(hash(bytes), file.sha256);
  for (const locator of locators) assert.equal(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + Buffer.byteLength(locator.expression)).toString("utf8"), locator.expression);
}
const report = { task: "LWB317-PM-016", reviewedCommit: submitted, baseline, result: "LWB317_PM016_SWITCH_SCOPE_OK", changedProductFiles, helperOnlyDiff: "PASS", originalSourceAndProductionHashesAndByteLocators: "PASS", callerByteLocators: results.callerInventory.length, counts: results.counts, limits: "Focused switch acceptance only. Browser observations retained as worker evidence; no new original/native/gameplay session. Existing unstaged AFK fixtures remain outside acceptance." };
if (process.argv.includes("--record")) fs.writeFileSync(path.join(here, "lead-results.json"), JSON.stringify(report, null, 2) + "\n");
if (process.argv.includes("--verify-record")) assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here, "lead-results.json"), "utf8")), report);
console.log(report.result);
