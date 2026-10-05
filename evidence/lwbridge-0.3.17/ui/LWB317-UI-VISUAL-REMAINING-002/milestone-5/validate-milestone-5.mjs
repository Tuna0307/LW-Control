import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { collectAppDependencyClosure } from "./collect-app-dependency-closure.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const manifest = JSON.parse(fs.readFileSync(path.join(here, "manifest.json"), "utf8"));
const result = JSON.parse(fs.readFileSync(path.join(here, "full-app-current-results.json"), "utf8"));
const sha256 = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
const fileHash = (filePath) => sha256(fs.readFileSync(filePath));
const resolveRecorded = (recordedPath) => /^[A-Za-z]:\//.test(recordedPath) ? recordedPath : path.join(repo, recordedPath);
const run = (command, args) => {
  const completed = spawnSync(command, args, { cwd: repo, encoding: "utf8", maxBuffer: 32 * 1024 * 1024 });
  assert.equal(completed.status, 0, `${command} ${args.join(" ")} failed\n${completed.stdout}\n${completed.stderr}`);
  return `${completed.stdout || ""}${completed.stderr || ""}`;
};

assert.equal(manifest.schema, 1);
assert.equal(manifest.task, "LWB317-UI-VISUAL-REMAINING-002");
assert.equal(manifest.milestone, 5);
assert.equal(manifest.branch, "research/offline-controller");
assert.equal(manifest.productionBaseline, "1a7d863b536f8da67c2abc1b7dfbec483ec36d81");

for (const record of manifest.referenceFiles) {
  const filePath = resolveRecorded(record.path);
  const bytes = fs.readFileSync(filePath);
  assert.equal(bytes.length, record.bytes, `reference byte mismatch: ${record.path}`);
  assert.equal(sha256(bytes), record.sha256, `reference hash mismatch: ${record.path}`);
}
assert.equal(manifest.referenceFiles[0].sha256, "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783");
assert.equal(manifest.referenceFiles[1].sha256, "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6");
assert.equal(manifest.referenceFiles[2].sha256, "3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545");
assert.equal(manifest.referenceFiles[3].sha256, "CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089");

for (const [relativePath, expectedHash] of Object.entries(manifest.productionSources)) {
  assert.equal(fileHash(path.join(repo, relativePath)), expectedHash, `production source changed: ${relativePath}`);
}
const currentClosure = collectAppDependencyClosure(repo);
assert.deepEqual(Object.keys(manifest.productionSources), currentClosure, "served App dependency closure changed");
for (const record of manifest.packageFiles) {
  const filePath = resolveRecorded(record.path);
  const bytes = fs.readFileSync(filePath);
  assert.equal(bytes.length, record.bytes, `package/tool input byte mismatch: ${record.path}`);
  assert.equal(sha256(bytes), record.sha256, `package/tool input hash mismatch: ${record.path}`);
}
for (const record of manifest.evidenceFiles) {
  const filePath = resolveRecorded(record.path);
  const bytes = fs.readFileSync(filePath);
  assert.equal(bytes.length, record.bytes, `evidence byte mismatch: ${record.path}`);
  assert.equal(sha256(bytes), record.sha256, `evidence hash mismatch: ${record.path}`);
}

assert.equal(result.marker, manifest.expected.marker);
assert.equal(result.assertions.length, manifest.expected.assertions);
assert.equal(result.screenshots.length, manifest.expected.screenshots);
assert.equal(result.consoleIssues.length, manifest.expected.consoleIssues);
assert.deepEqual(result.sourceFiles, manifest.productionSources);
assert.deepEqual(result.screenshots, manifest.screenshots);

const assertionNames = new Set(result.assertions.map((entry) => entry.name));
for (const mode of manifest.expected.modes) {
  const routeAssertions = result.assertions.filter((entry) => entry.name.startsWith(`${mode}/`) && (entry.name.endsWith(" selected nav owns aria-current") || entry.name.endsWith(" return preserves retained DOM identity")));
  assert.equal(routeAssertions.length, manifest.expected.routeTransitionAssertionsPerMode, `${mode} route transition proof incomplete`);
}
for (const required of [
  "uncached profile replacement crossed loading boundary",
  "cached Local 1 return bypasses loading",
  "cached Local 2 return bypasses loading",
  "App-owned Map tab persists across profile replacement",
  "page-local Map keyword resets across profile replacement",
  "page-local Map scan mode resets across profile replacement",
  "page-local Map quality filter resets across profile replacement",
  "page-local Map plunderable filter resets across profile replacement",
  "profile-scoped Map Auto interval resets for replacement profile",
  "App-owned Automation category persists across profile replacement",
  "App-owned Squads subtab persists across profile replacement",
  "Map pagination advances to second page",
  "Map pagination survives ordinary route hiding",
  "Map result count survives ordinary route hiding",
  "City hidden Activity cleans exactly one window keydown owner",
  "City return restores exactly one window keydown owner",
  "Automation local draft survives ordinary route hiding",
  "Squads AFK local editor draft survives ordinary route hiding",
  "Cross-server unavailable path is source-blocked",
  "retained Map survives live locale change",
  "retained Map survives live theme change",
  "shell-exit exit dialog is sibling after shell",
  "shell-exit-busy exit dialog is sibling after shell",
  "busy exit remains mounted after Escape",
]) assert.ok(assertionNames.has(required), `missing final-App assertion: ${required}`);

const routesSource = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/routes.js"), "utf8");
const routeOrder = [...routesSource.matchAll(/\{ key: "([^"]+)"/g)].map((match) => match[1]);
assert.deepEqual(routeOrder, manifest.expected.routes);

const app = fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/src/App.jsx"), "utf8");
assert.ok(app.includes("<Fragment key={selectedProfileId}>"), "selected-profile retained subtree key missing");
assert.ok(app.includes('<Activity key={route.key} mode={route.key === activeRoute ? "visible" : "hidden"}>'), "route Activity ownership missing");
assert.ok(app.includes("automation: { activeCategory: automationCategory, onActiveCategoryChange: setAutomationCategory }"), "Automation parent category ownership missing");
assert.ok(app.includes('"map-data": { activeTab: mapTab, onActiveTabChange: setMapTab }'), "Map parent tab ownership missing");
assert.ok(app.includes("march: { activeTab: squadTab, onActiveTabChange: setSquadTab }"), "Squads parent tab ownership missing");
const selectStart = app.indexOf("const selectRoute = useCallback");
const selectEnd = app.indexOf("const acknowledgeMapScan", selectStart);
const selectBody = app.slice(selectStart, selectEnd);
const mapRefresh = selectBody.indexOf('if (routeKey === "map-data") refreshMapSummary().catch(() => {});');
const transition = selectBody.indexOf("startRouteTransition(() => {");
assert.ok(mapRefresh >= 0 && transition > mapRefresh, "Map summary dispatch must precede route transition");

for (const screenshot of manifest.screenshots) {
  const filePath = path.join(here, "browser-current", screenshot.file);
  const bytes = fs.readFileSync(filePath);
  assert.equal(bytes.length, screenshot.bytes, `screenshot byte mismatch: ${screenshot.file}`);
  assert.equal(sha256(bytes), screenshot.sha256, `screenshot hash mismatch: ${screenshot.file}`);
}
const decodeScript = [
  "import json, os, sys",
  "from PIL import Image",
  "m=json.load(open(sys.argv[1], encoding='utf-8'))",
  "base=os.path.join(os.path.dirname(sys.argv[1]), 'browser-current')",
  "out=[]",
  "for s in m['screenshots']:",
  " p=os.path.join(base,s['file'])",
  " im=Image.open(p); im.load()",
  " assert im.format == 'PNG'",
  " assert im.width > 0 and im.height > 0",
  " out.append([s['file'], im.width, im.height])",
  "print(json.dumps(out))",
].join("\n");
const decoded = JSON.parse(run("python", ["-c", decodeScript, path.join(here, "manifest.json")]).trim());
assert.equal(decoded.length, manifest.expected.screenshots);

const uiNodeModules = path.join(repo, "src/LWBridge.UI-0.3.17/node_modules");
const installedPackageVersion = (relativePackageJson) => JSON.parse(fs.readFileSync(path.join(uiNodeModules, relativePackageJson, "package.json"), "utf8")).version;
const packageJson = JSON.parse(fs.readFileSync(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"), "utf8"));
const playwrightPackage = "C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/node_modules/playwright/package.json";
const chromeExe = "C:/Program Files/Google/Chrome/Application/chrome.exe";
const runtimeTools = {
  node: process.version,
  viteRequested: packageJson.devDependencies?.vite || packageJson.dependencies?.vite || "unlisted",
  vite: installedPackageVersion("vite"),
  react: installedPackageVersion("react"),
  reactDom: installedPackageVersion("react-dom"),
  viteReactPlugin: installedPackageVersion("@vitejs/plugin-react"),
  playwright: fs.existsSync(playwrightPackage) ? JSON.parse(fs.readFileSync(playwrightPackage, "utf8")).version : "unavailable",
  chrome: run("powershell.exe", ["-NoProfile", "-Command", `(Get-Item -LiteralPath '${chromeExe.replaceAll("'", "''")}').VersionInfo.FileVersion`]).trim(),
  python: run("python", ["--version"]).trim(),
  pillow: run("python", ["-c", "import PIL; print(PIL.__version__)"]).trim(),
  git: run("git", ["--version"]).trim(),
};
assert.deepEqual(runtimeTools, manifest.tools, "material render/tool versions changed");

const unitB = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/validate-unit-b.mjs"]);
assert.ok(unitB.includes(manifest.inheritedReplayExpectations.mapUnitB), "Map Unit B replay marker missing");
const mapPixels = run("python", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/replay-map-pixels-read-only.py"]);
assert.ok(mapPixels.includes(manifest.inheritedReplayExpectations.mapPixels), "Map pixel replay marker missing");
assert.ok(mapPixels.includes('"outsideAcceptedPixels": 0'), "Map pixel replay has pixels outside accepted fence masks");
assert.ok(mapPixels.includes('"acceptedDifferencePairs": 2'), "Map pixel replay must retain only the two Start Scan availability fences");
const correctedCity = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a/lead-audit/capture-corrected-error.mjs", "--read-only"]);
assert.ok(correctedCity.includes(manifest.inheritedReplayExpectations.correctedCityError), "corrected City search-rejection exact replay missing");
assert.ok(correctedCity.includes('"pairs": 3'), "corrected City search-rejection pair count changed");
const mapEntryCurrent = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/replay-map-entry-current.mjs"]);
assert.ok(mapEntryCurrent.includes(manifest.inheritedReplayExpectations.mapEntryCurrent), "current Map entry runtime replay missing");
const mapEntryResult = JSON.parse(mapEntryCurrent.trim());
assert.equal(mapEntryResult.cases.length, 4, "current Map entry runtime case count changed");
assert.equal(mapEntryResult.appSha256, manifest.productionSources["src/LWBridge.UI-0.3.17/src/App.jsx"], "Map entry runtime App identity mismatch");
const unitsEtoH = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-e/recover-pages.mjs", "--verify"]);
for (const marker of manifest.inheritedReplayExpectations.unitsEtoH) assert.ok(unitsEtoH.includes(marker), `E-H replay missing: ${marker}`);
const shellM4 = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-4/shell-source-render/validate-read-only.mjs"]);
assert.ok(shellM4.includes(manifest.inheritedReplayExpectations.shellM4), "M4 whole-shell replay marker missing");
const archive = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs"]);
assert.ok(archive.includes(manifest.inheritedReplayExpectations.archive), "archive replay marker missing");
const mutation = run("node", ["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-5/mutation-check.mjs"]);
assert.ok(mutation.includes("LWB317_REMAINING_M5_MUTATIONS_DETECTED"), "M5 mutation sensitivity marker missing");

console.log(JSON.stringify({
  marker: "LWB317_REMAINING_M5_VALIDATION_OK",
  productionBaseline: manifest.productionBaseline,
  sources: Object.keys(manifest.productionSources).length,
  assertions: result.assertions.length,
  screenshots: result.screenshots.length,
  decodedPngs: decoded.length,
  consoleIssues: result.consoleIssues.length,
  routeTransitionAssertions: manifest.expected.modes.length * manifest.expected.routeTransitionAssertionsPerMode,
  inherited: { mapUnitB: true, mapPixels: true, correctedCityError: true, mapEntryCurrent: true, unitsEtoH: true, shellM4: true, archive: true },
  mutationDetections: 3,
}));
