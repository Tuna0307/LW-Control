import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";
import { fileURLToPath } from "node:url";
import { runMutationCheck } from "./mutation-check.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../");
if (!process.argv.includes("--verify")) throw new Error("read-only validation requires --verify");
const manifestPath = path.join(here, "final-current-manifest.json");
const resultPath = path.join(here, "final-current-results.json");
assert.ok(fs.existsSync(manifestPath), "final-current-manifest.json is not recorded yet");
assert.ok(fs.existsSync(resultPath), "final-current-results.json is not recorded yet");
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
const result = JSON.parse(fs.readFileSync(resultPath, "utf8"));
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const hashFile = (file) => ({ bytes: fs.statSync(file).size, sha256: sha256(fs.readFileSync(file)) });
function treeDigest(root) {
  const entries = [];
  const walk = (folder) => {
    for (const item of fs.readdirSync(folder, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      const full = path.join(folder, item.name);
      if (item.isDirectory()) walk(full);
      else if (item.isFile()) entries.push({ path: path.relative(root, full).replaceAll("\\", "/"), ...hashFile(full) });
    }
  };
  walk(root);
  return { path: root.replaceAll("\\", "/"), fileCount: entries.length, bytes: entries.reduce((sum, entry) => sum + entry.bytes, 0), sha256: sha256(Buffer.from(JSON.stringify(entries))) };
}

const LOCAL_EXTENSIONS = ["", ".js", ".jsx", ".mjs", ".css", ".json", ".png", ".svg", ".webp", ".jpg", ".jpeg", ".gif"];
function resolveLocalSpecifier(importer, specifier) {
  if (!specifier.startsWith(".")) return null;
  const base = path.resolve(path.dirname(importer), specifier.split(/[?#]/, 1)[0]);
  for (const extension of LOCAL_EXTENSIONS) { const file = `${base}${extension}`; if (fs.existsSync(file) && fs.statSync(file).isFile()) return file; }
  for (const extension of [".js", ".jsx", ".mjs", ".css", ".json"]) { const file = path.join(base, `index${extension}`); if (fs.existsSync(file) && fs.statSync(file).isFile()) return file; }
  throw new Error(`unresolved local dependency ${specifier} from ${importer}`);
}
function collectClosure() {
  const uiRoot = path.join(repo, "src/LWBridge.UI-0.3.17");
  const visited = new Set([path.join(uiRoot, "index.html")]);
  const queue = [path.join(uiRoot, "src/main.jsx")];
  while (queue.length) {
    const file = queue.shift();
    if (visited.has(file)) continue;
    visited.add(file);
    const extension = path.extname(file).toLowerCase();
    if (![".js", ".jsx", ".mjs", ".css"].includes(extension)) continue;
    const source = fs.readFileSync(file, "utf8");
    const found = new Set();
    if (extension === ".css") {
      for (const match of source.matchAll(/url\(\s*["']?([^"')]+)["']?\s*\)/g)) if (!/^(?:data:|https?:|#|\/)/.test(match[1].trim())) found.add(match[1].trim());
    } else {
      for (const pattern of [/\b(?:import|export)\s+[\s\S]*?\bfrom\s*["']([^"']+)["']/g, /\bimport\s*["']([^"']+)["']/g, /\bimport\s*\(\s*["']([^"']+)["']\s*\)/g]) {
        for (const match of source.matchAll(pattern)) found.add(match[1]);
      }
    }
    for (const specifier of found) { const resolved = resolveLocalSpecifier(file, specifier); if (resolved && !visited.has(resolved)) queue.push(resolved); }
  }
  return [...visited].map((file) => path.relative(repo, file).replaceAll("\\", "/")).sort();
}
function closureHashes() { return Object.fromEntries(collectClosure().map((name) => [name, sha256(fs.readFileSync(path.join(repo, name)))])); }
const closureDigest = (closure) => sha256(Buffer.from(JSON.stringify(closure)));

function decodePng(bytes) {
  assert.equal(bytes.subarray(0, 8).toString("hex"), "89504e470d0a1a0a", "PNG signature");
  let offset = 8, width = 0, height = 0, bitDepth = 0, colorType = 0;
  const idat = [];
  while (offset < bytes.length) {
    const length = bytes.readUInt32BE(offset), type = bytes.subarray(offset + 4, offset + 8).toString("ascii"), data = bytes.subarray(offset + 8, offset + 8 + length);
    if (type === "IHDR") { width = data.readUInt32BE(0); height = data.readUInt32BE(4); bitDepth = data[8]; colorType = data[9]; assert.equal(data[12], 0); }
    else if (type === "IDAT") idat.push(data); else if (type === "IEND") break;
    offset += 12 + length;
  }
  assert.equal(bitDepth, 8);
  const channels = ({ 0: 1, 2: 3, 4: 2, 6: 4 })[colorType];
  assert.ok(channels);
  const rowBytes = width * channels, inflated = zlib.inflateSync(Buffer.concat(idat)), pixels = Buffer.alloc(height * rowBytes);
  assert.equal(inflated.length, height * (rowBytes + 1));
  const paeth = (a, b, c) => { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; };
  for (let y = 0; y < height; y += 1) {
    const input = y * (rowBytes + 1), filter = inflated[input], out = y * rowBytes;
    for (let x = 0; x < rowBytes; x += 1) {
      const raw = inflated[input + 1 + x], left = x >= channels ? pixels[out + x - channels] : 0, up = y ? pixels[out - rowBytes + x] : 0, upperLeft = y && x >= channels ? pixels[out - rowBytes + x - channels] : 0;
      const value = filter === 0 ? raw : filter === 1 ? raw + left : filter === 2 ? raw + up : filter === 3 ? raw + Math.floor((left + up) / 2) : filter === 4 ? raw + paeth(left, up, upperLeft) : (() => { throw new Error(`unsupported PNG filter ${filter}`); })();
      pixels[out + x] = value & 0xff;
    }
  }
  return { width, height, channels, pixelSha256: sha256(pixels) };
}

assert.equal(manifest.schema, 1);
assert.equal(manifest.task, "LWB317-UI-VISUAL-FINAL-CLOSEOUT-003-R2");
assert.equal(manifest.marker, "LWB317_FINAL_CLOSEOUT_R2_MANIFEST");
assert.equal(result.marker, "LWB317_FINAL_CLOSEOUT_R2_CURRENT_OK");
assert.deepEqual(result.sourceStart, result.sourceEnd, "recorded capture changed source during execution");
assert.equal(result.sourceClosureSha256Start, result.sourceClosureSha256End, "recorded source closure digest changed during capture");
const currentClosure = closureHashes();
assert.deepEqual(currentClosure, manifest.sourceFiles, "served App source closure drifted");
assert.equal(closureDigest(currentClosure), manifest.sourceClosureSha256, "served App source closure digest drifted");
assert.equal(result.sourceClosureSha256End, manifest.sourceClosureSha256, "result/manifest source closure digest differs");
assert.deepEqual(result.sourceEnd, manifest.sourceFiles, "result/manifest source closure differs");

for (const record of [...manifest.evidenceFiles, ...manifest.supportFiles, ...manifest.acceptanceFiles]) {
  const file = /^[A-Za-z]:\//.test(record.path) ? record.path : path.join(repo, record.path);
  assert.deepEqual(hashFile(file), { bytes: record.bytes, sha256: record.sha256 }, `evidence/support/acceptance drift: ${record.path}`);
}
for (const record of manifest.tools.files) {
  assert.deepEqual(hashFile(record.path), { bytes: record.bytes, sha256: record.sha256 }, `tool drift: ${record.path}`);
}
for (const record of manifest.tools.trees) assert.deepEqual(treeDigest(record.path), record, `tool implementation tree drift: ${record.path}`);
assert.equal(process.version, manifest.tools.node, "Node version drift");
assert.deepEqual(result.tools, manifest.tools, "result/manifest tool identity differs");
assert.deepEqual(hashFile(resultPath), { bytes: manifest.result.bytes, sha256: manifest.result.sha256 }, "recorded result drift");
assert.deepEqual(result.acceptanceChain, manifest.acceptanceChain, "result/manifest closeout acceptance chain differs");
assert.deepEqual(result.endpointProvenance, manifest.endpointProvenance, "result/manifest endpoint provenance differs");
assert.deepEqual(result.endpointProvenance.sourceFiles, manifest.sourceFiles, "recorded owned Vite endpoint did not serve the frozen source closure");
assert.equal(result.endpointProvenance.sourceClosureSha256, manifest.sourceClosureSha256, "endpoint/source closure digest differs");
assert.equal(result.endpointProvenance.matchedFiles, Object.keys(manifest.sourceFiles).length, "endpoint provenance coverage is incomplete");
const acceptanceByPath = Object.fromEntries(manifest.acceptanceFiles.map((entry) => [entry.path, entry]));
const intentionalSourcePath = "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx";
assert.deepEqual(result.acceptanceChain.intentionalSourceDelta.changedFiles, [intentionalSourcePath], "R2 source evolution must remain a one-file SquadsPage delta");
assert.equal(result.acceptanceChain.intentionalSourceDelta.path, intentionalSourcePath, "R2 intentional source path");
assert.equal(result.acceptanceChain.intentionalSourceDelta.beforeSha256, "920892FCC3069D4F98B0662E4502910DDF5901EE707C8A663B751BBB3A2E4623", "frozen R1 SquadsPage anchor");
assert.equal(result.acceptanceChain.intentionalSourceDelta.afterSha256, manifest.sourceFiles[intentionalSourcePath], "R2 intentional source delta must end at current SquadsPage");
assert.equal(result.acceptanceChain.intentionalSourceDelta.afterSha256, "5D0AE58CBE199441A046E326D699808CA31AEB96D6CC7DAA0E7EC0A8C3B231D7", "reviewed R2 SquadsPage anchor");
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/host-reconciliation-results.json"].sha256, result.acceptanceChain.r1HostBaseline.resultSha256, "R1 host result chain hash differs");
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/conditional-composition-results.json"].sha256, result.acceptanceChain.milestoneC.conditionalSha256, "Milestone C conditional chain hash differs");
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/config-store-ownership-results.json"].sha256, result.acceptanceChain.milestoneC.configStoreSha256, "Milestone C config-store chain hash differs");
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/profile-ownership-results.json"].sha256, result.acceptanceChain.r1Ownership.resultSha256, "R1 focused ownership chain hash differs");
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r1/equipment-regression-results.json"].sha256, result.acceptanceChain.r1Ownership.historicalEquipmentSha256, "historical R1 Equipment chain hash differs");
assert.equal(result.acceptanceChain.r1Ownership.status, "PASS");
assert.equal(result.acceptanceChain.r1Ownership.leadCounterexample, true);
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/equipment-save-contract-results.json"].sha256, result.acceptanceChain.r2Equipment.contractSha256, "R2 Equipment save-contract chain hash differs");
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/r2/equipment-regression-results.json"].sha256, result.acceptanceChain.r2Equipment.regressionSha256, "R2 Equipment regression chain hash differs");
assert.equal(result.acceptanceChain.r2Equipment.contractMarker, "LWB317_FINAL_CLOSEOUT_R2_EQUIPMENT_SAVE_CONTRACT_OK");
assert.equal(result.acceptanceChain.r2Equipment.contractAssertions, 38);
assert.equal(result.acceptanceChain.r2Equipment.regressionMarker, "LWB317_FINAL_CLOSEOUT_R2_EQUIPMENT_REGRESSION_OK");
assert.equal(result.acceptanceChain.r2Equipment.regressionAssertions, 17);
assert.equal(acceptanceByPath["evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/locale-inventory.json"].sha256, result.acceptanceChain.inheritedLocaleFallback.sha256, "Unit C locale fallback chain hash differs");
assert.equal(result.acceptanceChain.inheritedLocaleFallback.marker, "LWB317_VISUAL_FINAL_UNIT_C_LOCALES_OK");
assert.deepEqual(result.acceptanceChain.inheritedLocaleFallback.keys, ["common.loading"], "source-proven fallback set changed");

const r2EquipmentContract = JSON.parse(fs.readFileSync(path.join(here, "equipment-save-contract-results.json"), "utf8"));
const r2EquipmentRegression = JSON.parse(fs.readFileSync(path.join(here, "equipment-regression-results.json"), "utf8"));
assert.equal(r2EquipmentContract.cases.noSecondSave.afterFirstAck.dirty, true, "R2 distinguishing first acknowledgement must leave later move dirty");
assert.equal(r2EquipmentContract.cases.noSecondSave.afterExplicitSave.dirty, false, "R2 later explicit Save must confirm later move");
assert.equal(r2EquipmentContract.cases.queuedSave.settled.dirty, false, "R2 explicit queued second Save must drain");
assert.equal(r2EquipmentContract.issues.length, 0, "R2 Equipment save-contract browser issues");
assert.equal(r2EquipmentRegression.issues.length, 0, "R2 Equipment regression browser issues");
for (const focused of [r2EquipmentContract, r2EquipmentRegression]) {
  for (const screenshot of focused.screenshots) {
    const file = path.join(repo, screenshot.file);
    const bytes = fs.readFileSync(file);
    assert.equal(bytes.length, screenshot.bytes, `${screenshot.id} focused screenshot byte count drift`);
    assert.equal(sha256(bytes), screenshot.sha256, `${screenshot.id} focused screenshot hash drift`);
  }
}

assert.equal(result.consoleIssues.length, 0, "recorded browser console/page issues");
assert.ok(result.assertions.length >= 100, `final integrated assertion count unexpectedly small: ${result.assertions.length}`);
assert.ok(result.screenshots.length >= 10, `final settled screenshot count unexpectedly small: ${result.screenshots.length}`);
assert.deepEqual(result.localeAudit.codes, ["en", "id", "ja", "ko", "pt", "ru", "vi", "zh-CN", "zh-TW"]);
assert.equal(result.localeAudit.requiredKeys.length, result.localeAudit.summary.en.catalogKeys, "locale audit must cover the complete recovered English catalog");
for (const key of ["hotkeys.title", "hotkeys.description", "miniGames.title", "miniGames.description"]) assert.ok(result.localeAudit.dynamicAssignedKeys.includes(key), `dynamic assigned locale key missing from audit: ${key}`);
assert.deepEqual(result.localeAudit.sourceProvenFallbackKeys, ["common.loading"], "recorded source-proven fallback keys changed");
assert.deepEqual(result.localeAudit.literalMissing, ["common.loading"], "literal assigned keys outside the current catalogs changed");
for (const code of result.localeAudit.codes) {
  assert.deepEqual(result.localeAudit.summary[code].missing, [], `${code} locale audit recorded missing keys`);
  assert.deepEqual(result.localeAudit.summary[code].extra, [], `${code} locale audit recorded extra keys`);
  assert.equal(result.localeAudit.summary[code].assignedKeysPresent, result.localeAudit.requiredKeys.length, `${code} locale audit is incomplete`);
}
assert.equal(result.raceProofs.recovery.crossProfileRejected, true);
assert.equal(result.raceProofs.recovery.replacedAndClosedRetired, true);
assert.equal(result.raceProofs.recovery.accepted, 2);
assert.equal(result.raceProofs.periodicStatus.requestsDuringOverlap, 1);
assert.equal(result.raceProofs.periodicStatus.obsoleteWrites, 0);
assert.equal(result.raceProofs.periodicStatus.timerClears, 1);
assert.equal(result.raceProofs.previewLoading.selected, "C");
assert.equal(result.raceProofs.previewLoading.loading, false);
assert.equal(result.raceProofs.mapRequestRetirement.authoritativeServer, 422);

const assertionNames = new Set(result.assertions.map((entry) => entry.name));
for (const required of [
  "Automation exposes seven categories",
  "Map exposes eight data tabs plus Scheduled",
  "Squads exposes AFK and Equipment",
  "uncached profile crosses loading boundary",
  "Automation profile A child draft is dirty before switch",
  "Automation child draft resets at profile boundary",
  "Automation profile A child draft returns only with profile A",
  "Automation profile B remains isolated after cached return",
  "AFK profile A Potion draft is dirty before switch",
  "AFK profile A Potion draft records local edit",
  "AFK profile B starts with clean Potion draft",
  "AFK profile B starts with clean AFK store",
  "AFK profile A Potion draft returns only with profile A",
  "AFK profile A store remains dirty on return",
  "AFK profile B remains clean after cached return",
  "AFK profile B remains isolated after cached return",
  "Equipment R2 integrated contract executes all focused assertions",
  "Equipment pending acknowledgement leaves later unrequested move dirty",
  "Equipment later explicit Save confirms retained move",
  "Equipment explicitly queued second Save confirms later move",
  "Equipment rejection Retry confirms retained owning draft",
  "Equipment rejection Discard restores owning confirmed draft",
  "Equipment R2 integrated contract has no browser issues",
  "cached first profile return bypasses loading",
  "cached second profile return bypasses loading",
  "App-owned Map tab persists across profile replacement",
  "Map keyword resets at profile boundary",
  "Map page 2 survives route hiding",
  "Automation conditional edit is dirty before hide",
  "Automation conditional draft survives category and route hiding",
  "AFK editor is dirty before hide",
  "AFK draft survives Equipment return",
  "AFK Join cleanup restores trigger focus",
  "City hide removes exactly one keydown owner",
  "City return restores exactly one keydown owner",
  "Mini Games hide suspends at least one interval owner",
  "Mini Games return restores interval ownership",
  "Cross-server provider remains fenced",
  "Profile note dialog stays outside routed page",
  "retained Map survives live locale/theme transition",
  "shell-exit dialog is sibling after shell",
  "shell-exit-busy dialog is sibling after shell",
  "busy exit ignores Escape",
]) assert.ok(assertionNames.has(required), `missing final integrated assertion: ${required}`);
for (const mode of ["en-light-desktop", "ja-dark-desktop", "en-light-narrow", "ja-dark-narrow", "en-dark-desktop", "ja-light-narrow"]) {
  assert.ok(assertionNames.has(`${mode} exact navigation count`), `missing route mode: ${mode}`);
  for (const route of ["overview", "automation", "map-data", "march", "city-layout", "hotkeys", "mini-games", "settings"]) {
    assert.ok(assertionNames.has(`${mode}/${route} return keeps DOM identity`), `missing route-return proof: ${mode}/${route}`);
  }
}

assert.equal(result.styleSnapshots.length, result.screenshots.length, "every screenshot must have a settled style/text inspection");
for (const snapshot of result.styleSnapshots) {
  assert.equal(snapshot.loadingPlaceholders, 0, `${snapshot.id} recorded a loading placeholder`);
  assert.ok(snapshot.textLength > 0 && snapshot.textSha256, `${snapshot.id} missing settled text inspection`);
  assert.ok(snapshot.shell && snapshot.topBar && snapshot.sideNav && snapshot.mainView, `${snapshot.id} missing host geometry/style inspection`);
}

const manifestScreenshots = new Map(manifest.screenshots.map((entry) => [entry.file, entry]));
assert.equal(manifestScreenshots.size, result.screenshots.length, "manifest screenshot set differs from result");
for (const file of [
  "en-light-desktop-eight-route-return.png", "ja-dark-desktop-eight-route-return.png",
  "en-light-narrow-eight-route-return.png", "ja-dark-narrow-eight-route-return.png",
  "en-dark-desktop-eight-route-return.png", "ja-light-narrow-eight-route-return.png",
  "en-light-profile-subtabs-reset.png", "en-light-map-page2-return.png",
  "en-light-automation-conditional-return.png", "en-light-squads-dialog-retention.png",
  "en-light-afk-join-dialog.png", "en-light-cross-server-popover.png", "en-light-profile-note-dialog.png",
  "en-light-shell-exit-dialog.png", "ja-dark-shell-exit-busy-dialog.png", "ja-dark-live-locale-theme-map.png",
]) assert.ok(manifestScreenshots.has(file), `missing required settled capture: ${file}`);
for (const screenshot of result.screenshots) {
  const expected = manifestScreenshots.get(screenshot.file);
  assert.ok(expected, `manifest missing screenshot ${screenshot.file}`);
  const file = path.join(here, "browser-current", screenshot.file);
  const bytes = fs.readFileSync(file);
  assert.equal(bytes.length, expected.bytes, `${screenshot.file} byte count drift`);
  assert.equal(sha256(bytes), expected.sha256, `${screenshot.file} PNG drift`);
  const decoded = decodePng(bytes);
  assert.deepEqual(decoded, { width: expected.width, height: expected.height, channels: expected.channels, pixelSha256: expected.pixelSha256 }, `${screenshot.file} decoded pixel drift`);
  assert.equal(screenshot.pixelSha256, expected.pixelSha256, `${screenshot.file} result pixel digest differs`);
}

const mutation = await runMutationCheck();
assert.equal(mutation.marker, "LWB317_FINAL_CLOSEOUT_R2_MUTATIONS_DETECTED");
assert.ok(mutation.mutations.every((entry) => entry.detected), JSON.stringify(mutation, null, 2));
assert.ok(mutation.mutations.length >= 12, "expected inherited A/B/C mutations plus the R2 Equipment false-flush mutation");
assert.deepEqual(new Set(mutation.mutations.map((entry) => entry.milestone)), new Set(["A", "B", "C", "R2"]), "semantic mutations must cover A/B/C and R2");
assert.ok(mutation.mutations.some((entry) => entry.name === "restore Equipment default true flush mode" && entry.detected), "R2 false-flush mutation must be detected");

console.log(JSON.stringify({
  marker: "LWB317_FINAL_CLOSEOUT_R2_VALIDATED",
  sources: Object.keys(manifest.sourceFiles).length,
  assertions: result.assertions.length,
  screenshots: result.screenshots.length,
  decodedPngs: result.screenshots.length,
  localeCatalogs: result.localeAudit.codes.length,
  mutationDetections: mutation.mutations.length,
  consoleIssues: result.consoleIssues.length,
}));
