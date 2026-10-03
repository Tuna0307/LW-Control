import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../..");
const referenceExe = "C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe";
let assertions = 0;

function check(value, message) {
  assert.ok(value, message);
  assertions += 1;
}

function sha256Bytes(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex").toUpperCase();
}

function sha256File(file) {
  return sha256Bytes(fs.readFileSync(file));
}

function repoPath(relative) {
  return path.join(repo, ...relative.split("/"));
}

function expectHash(relative, expected) {
  check(fs.existsSync(repoPath(relative)), `Missing evidence file: ${relative}`);
  check(sha256File(repoPath(relative)) === expected, `Hash mismatch: ${relative}`);
}

function expectSlice(relative, offset, length, expected) {
  const bytes = fs.readFileSync(repoPath(relative));
  check(bytes.length >= offset + length, `Slice is outside ${relative}`);
  check(sha256Bytes(bytes.subarray(offset, offset + length)) === expected, `Slice hash mismatch: ${relative}@${offset}+${length}`);
}

check(fs.existsSync(referenceExe), "Reference LWBridge 0.3.17 executable is missing");
check(sha256File(referenceExe) === "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783", "Reference executable hash mismatch");

const squadAsset = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js";
const indexAsset = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js";
for (const [relative, expected] of Object.entries({
  [squadAsset]: "ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7",
  [indexAsset]: "44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js": "2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css": "3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js": "0BF43D180EB93692A93B830B5D984E9D01DDEEA524340F2334FC63CBA527A731",
  "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/ja-UrbzJu-m.js": "35969183AB623B6701BC850FDF28BCF268C93127FF6C9693F77E588EE7BDBE38"
})) expectHash(relative, expected);

expectSlice(squadAsset, 176937, 14376, "64968D742C5AAE80AAC5A87C90EEF4E2FCCD75115F6844C35372660B0D4912B0");
expectSlice(squadAsset, 191313, 1517, "F4644E68B880678194C13EAC0FF9F7D395061EF7EB63FC505A85DD3E2C1419E8");
expectSlice(squadAsset, 180434, 234, "CA912650CA34356BF94AB56FC6CCFDF6024C65EA7A715DB294BB8DD3E4E6D879");
expectSlice(squadAsset, 179906, 400, "E5C03C3FE849E892B0CCF8B816701D42FDBB4A38BE8A3A7D9E0B254DDC6D713A");
expectSlice(indexAsset, 209017, 1209, "E42C17AAC67AFE7A73D525902C9E757644E40838422730CEB32EC47830C4F8A2");

for (const [relative, expected] of Object.entries({
  "src/LWBridge.UI-0.3.17/src/Pages.jsx": "D873F1DC608342ADB8618A345CC9E2238FB5C54CF34C020753DEBD21370BF3F2",
  "src/LWBridge.UI-0.3.17/src/previewConfigHook.jsx": "643875718C4DA1B443215F8E6B9417C840F372882AA81BA286080977D1ED827E",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001/check-closeout.mjs": "6E03800DA2BF57C313EA7A9FB9EA7D32915F7620BCBFEA685330B93E4405A0B7",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/check-r1.mjs": "2F4E4C88E118437DEE2649F0C5C35E6D724A5ACB6251D04E5D038C028E021ED7",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/r1-results.json": "D05DCAC044BCD0855C11AF44CCA1CFF2BD87CE9690DFAC4F9955B8D2021EA47C",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/browser-results.json": "BE314B4E4BB583F8D298CC58AF54DF4AB11CC67C44AE865AC75EABA16A28E68A",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/screenshots/rename-pending-en-light.jpg": "6A9ED4045DB4C99361EBE3B88B843053A55FE13DC40F43BDECCD8083592D8725",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/screenshots/rename-error-ja-dark.jpg": "AA6B732369947D9491A7C3303BB2D5E95CFF5BC77141ED5F5C204D11FDA60067"
})) expectHash(relative, expected);

const r1 = JSON.parse(fs.readFileSync(path.join(here, "r1-results.json"), "utf8"));
check(r1.result === "LWB317_EQUIPMENT_R1_ACTUAL_OK", "R1 executable result marker changed");
check(Array.isArray(r1.results) && r1.results.length === 10, "R1 executable result set changed");
check(r1.results.every((entry) => entry.result === "PASS"), "R1 executable case failure recorded");
check(r1.results.reduce((sum, entry) => sum + entry.count, 0) === 61, "R1 assertion count changed");

const browser = JSON.parse(fs.readFileSync(path.join(here, "browser-results.json"), "utf8"));
check(browser.result === "LWB317_EQUIPMENT_R1_BROWSER_OK", "Browser result marker changed");
check(browser.enLight.pending.dialogOpen && browser.enLight.pending.busyKey === "rename", "EN/light pending acknowledgement proof changed");
check(browser.enLight.error.dialogOpen && browser.enLight.error.buttons.join("|") === "Retry|Discard unsaved changes", "EN/light error proof changed");
check(browser.enLight.success.dialogClosed && browser.enLight.success.movedItemStateRetained, "EN/light success proof changed");
check(browser.jaDark.pending.dialogOpen && browser.jaDark.pending.busyKey === "rename", "JA/dark pending acknowledgement proof changed");
check(browser.jaDark.error.retryKeptDialogOpen && browser.jaDark.error.retryClearedError && browser.jaDark.error.saveAfterRetryClosedDialog, "JA/dark retry proof changed");
check(browser.jaDark.success.dialogClosed && browser.jaDark.success.renamedTo === "成功 R1", "JA/dark success proof changed");
check(browser.activityLifecycle.hiddenEquipmentAlt3.defaultPrevented === false && browser.activityLifecycle.hiddenEquipmentAlt3.netEquipmentKeydownListeners === 0, "Hidden Equipment effect proof changed");
check(browser.activityLifecycle.returnedEquipment.netEquipmentKeydownListeners === 1 && browser.activityLifecycle.returnedEquipment.selectedName === "Retained R1", "Returned Equipment lifecycle proof changed");
check(browser.activityLifecycle.afkEffect.pickerRetainedAfterEscapeWhileHidden && browser.activityLifecycle.afkEffect.pickerClosedAfterEscapeWhenVisibleAgain, "AFK Activity lifecycle proof changed");
check(browser.console.warnings.length === 0 && browser.console.errors.length === 0, "Browser console diagnostics are not clean");

for (const relative of [
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/screenshots/rename-pending-en-light.jpg",
  "evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/screenshots/rename-error-ja-dark.jpg"
]) {
  const bytes = fs.readFileSync(repoPath(relative));
  check(bytes.length > 50000, `Screenshot is unexpectedly small: ${relative}`);
  check(bytes[0] === 0xff && bytes[1] === 0xd8 && bytes.at(-2) === 0xff && bytes.at(-1) === 0xd9, `Screenshot JPEG framing changed: ${relative}`);
}

expectHash("evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/check-review.mjs", "D064C67A5DA0DE739A6B471D3FF20BD9E6AAC203AFC303B6BDE18C91BA1360EF");
expectHash("evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/independent-results.json", "70BD9FDE1900E6C10D1AA107E5A8EFE2F84D8E0E0B039084E6661424BA77CCA5");
const failing = JSON.parse(fs.readFileSync(repoPath("evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-EQUIPMENT-CLOSEOUT-001/independent-results.json"), "utf8"));
check(failing.submittedCommit === "4bafcd437929f2f905b557ca700657467afbdcbc", "Submitted failing commit record changed");
check(failing.decision === "CHANGES_REQUIRED", "Submitted failing decision record changed");
check(failing.hiddenTab.currentHiddenAltPrevented === true && failing.hiddenTab.currentHiddenAltAction === "apply-all:equipment-preset-fixed-2", "Submitted hidden-effect defect record changed");
check(failing.renameProofGap.currentPreviewSaveSynchronous === true && failing.renameProofGap.currentDialogClosesImmediately === true, "Submitted rename proof-gap record changed");

const historicalManifest = JSON.parse(fs.readFileSync(repoPath("evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001/source-manifest.json"), "utf8"));
check(historicalManifest.current["src/LWBridge.UI-0.3.17/src/Pages.jsx"] === "A07D08E1546824B5D7342B3F473C0569ED4775C36DF07784CD59529A309D130C", "Historical parent current-product hash was rewritten");
check(historicalManifest.referenceExecutableSha256 === "4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783", "Historical reference identity changed");

const protectedGuard = spawnSync(process.execPath, [repoPath("evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs")], {
  cwd: repo,
  encoding: "utf8"
});
check(protectedGuard.status === 0, `Protected-WIP guard failed: ${protectedGuard.stdout}${protectedGuard.stderr}`);
check(protectedGuard.stdout.includes("LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7"), "Protected-WIP guard marker changed");

console.log(`LWB317_EQUIPMENT_R1_EVIDENCE_OK assertions=${assertions}`);
