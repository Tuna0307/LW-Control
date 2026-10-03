import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { nodes } from "../../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const packet = path.dirname(here);
const rel = (file) => path.relative(repo, file).replaceAll("\\", "/");
const read = (file) => fs.readFileSync(path.join(repo, file));
const json = (file) => JSON.parse(read(file));
const hash = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const parent = "evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001";
const historical = "evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-REMAINING-PAGES-001";
const source = json(`${parent}/milestone-a/source-manifest.json`);
const expectedExe = "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783";
assert.equal(source.referenceExecutableSha256, expectedExe);
assert.equal(hash(fs.readFileSync(source.referenceExecutable)), expectedExe);
for (const asset of source.assets) assert.equal(hash(read(asset.path)), asset.sha256, asset.path);
for (const [name, slice] of Object.entries(source.locators)) {
  const bytes = read(`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${slice.asset}`);
  assert.equal(hash(bytes.subarray(slice.utf8ByteOffset, slice.utf8ByteOffset + slice.utf8ByteLength)), slice.sha256, name);
}

// Historical evidence remains a record of its submitted product. Its validators
// intentionally cannot pin the corrected current Pages.jsx. Compare the evidence
// itself to its original committed bytes instead of rewriting those old pins.
const historicalFiles = ["check-review.mjs", "independent-results.json", "verification-results.json", "review-manifest.json", "validate-evidence.mjs", "README.md"];
const normal = (value) => value.replaceAll("\r\n", "\n");
for (const file of historicalFiles) {
  const entry = `${historical}/${file}`;
  const submitted = execFileSync("git", ["show", `8da32be767f05e09361fca02d7723f01ad2d3caa:${entry}`], { cwd: repo, encoding: "utf8" });
  assert.equal(normal(read(entry).toString("utf8")), normal(submitted), entry);
}
const baselinePath = `${parent}/milestone-a/baseline-results.json`;
assert.deepEqual(json(baselinePath), JSON.parse(execFileSync("git", ["show", `afd65b64808e0fd43175dbbaa5afc36e32def9ef:${baselinePath}`], { cwd: repo, encoding: "utf8" })));
assert.equal(json(`${historical}/independent-results.json`).results.length, 12);

const extraSlices = [];
for (const [asset, names] of [
  ["CityLayoutPanel-DoNWkywK.js", ["it", "at", "ot", "ct"]],
  ["HotkeyPanel-XA8idRHB.js", ["L", "z", "B"]],
  ["SettingsPanel-DqxIWv_E.js", ["v"]],
  ["index-BVfnK1wp.js", ["Ln"]],
]) {
  const text = read(`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${asset}`).toString("utf8");
  const ast = nodes(text);
  for (const name of names) {
    const node = ast.find((item) => ["FunctionDeclaration", "VariableDeclarator"].includes(item.type) && item.id?.name === name);
    assert.ok(node, `${asset}:${name}`);
    const bytes = Buffer.from(text.slice(node.start, node.end));
    extraSlices.push({ asset, name, utf8ByteOffset: Buffer.byteLength(text.slice(0, node.start)), utf8ByteLength: bytes.length, sha256: hash(bytes) });
  }
}

const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => entry.isDirectory() ? walk(path.join(dir, entry.name)) : [path.join(dir, entry.name)]);
const manifestPath = path.join(here, "integrity-manifest.json");
const files = [...new Set([
  ...walk(packet).filter((file) => file !== manifestPath).map(rel),
  "src/LWBridge.UI-0.3.17/src/Pages.jsx",
  "src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js",
  "src/LWBridge.UI-0.3.17/src/App.jsx",
  "src/LWBridge.UI-0.3.17/src/locales/en.js",
  "src/LWBridge.UI-0.3.17/src/locales/ja.js",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs",
  `${parent}/milestone-a/source-manifest.json`, baselinePath,
  ...source.assets.map((asset) => asset.path),
  ...historicalFiles.map((file) => `${historical}/${file}`),
])].sort();
for (const file of files.filter((entry) => entry.endsWith(".json"))) json(file);
for (const milestone of ["a", "b", "c"]) assert.equal(json(`${rel(packet)}/milestone-${milestone}/results.json`).result, "PASS");
const browser = json(`${rel(packet)}/milestone-d/browser-results.json`);
assert.equal(browser.result, "PASS");
assert.deepEqual(browser.console.errors, []);
assert.deepEqual(browser.console.warnings, []);
for (const observation of browser.cases.filter((item) => item.screenshot)) assert.ok(fs.statSync(path.join(here, observation.screenshot)).size > 1000, observation.screenshot);
const guard = execFileSync(process.execPath, ["evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs"], { cwd: repo, encoding: "utf8" });
assert.ok(guard.includes("LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7"));

const snapshot = { workItem: "LWB317-UI-REMAINING-PAGES-001-R1", referenceExecutableSha256: expectedExe, parentSlices: 15, extraSlices, files: files.map((file) => ({ path: file, sha256: hash(read(file)) })) };
if (process.argv.includes("--record")) fs.writeFileSync(manifestPath, JSON.stringify(snapshot, null, 2) + "\n");
else assert.deepEqual(snapshot, JSON.parse(fs.readFileSync(manifestPath, "utf8")), "Current R1 identity changed: investigate before recording a superseding checkpoint");
console.log(`LWB317_REMAINING_PAGES_R1_INTEGRITY_OK assets=${source.assets.length} slices=${15 + extraSlices.length} files=${files.length} screenshots=${files.filter((file) => file.endsWith(".jpg")).length} protected=7`);
