import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { fn, raw, read, squad } from "../../LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-c/accepted-harness.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../..");
const hash = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
const fileHash = (file) => hash(fs.readFileSync(file));
const currentPath = path.join(repo, "src/LWBridge.UI-0.3.17/src/SquadsPage.jsx");
const fixturePath = path.join(repo, "src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js");
const current = fs.readFileSync(currentPath, "utf8");
const preFix = fs.readFileSync(path.join(here, "baseline/pre-fix-SquadsPage.jsx"), "utf8");

const expectedSource = {
  I: { offset: 28070, length: 22554, sha256: "0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E" },
  pe: { offset: 4586, length: 11256, sha256: "4DD8A8770C7726AA3CCE0657BC88C6B0FB2146FA161BCF2C3017A231D0953C85" },
  me: { offset: 15842, length: 687, sha256: "7232700BC71C76F9E5B0DA56A2DCC43D369191701C45CE28A87D5A966058DAB8" },
  he: { offset: 16529, length: 1957, sha256: "37F600C70C0F2F1A2F5722DC33C6E62075A7976C86ECA48001E97F1FA39BC32B" },
};
for (const [name, expected] of Object.entries(expectedSource)) {
  const node = fn(squad, name);
  const source = raw(squad, node);
  assert.equal(Buffer.byteLength(squad.slice(0, node.start)), expected.offset, `${name} UTF-8 offset`);
  assert.equal(Buffer.byteLength(source), expected.length, `${name} UTF-8 length`);
  assert.equal(hash(source), expected.sha256, `${name} source hash`);
}

for (const name of ["AfkProfileEditor", "EquipmentContent", "CompactAfkCard"]) {
  const before = raw(preFix, fn(preFix, name));
  const after = raw(current, fn(current, name));
  assert.equal(after, before, `${name} accepted/pre-existing slice changed`);
}

const baselineHashes = {
  "profile-failing-baseline.json": "52CE90805182ECC94B711EB375286D399A9F78CE133D891DCD6852959725F280",
  "garrison-zombie-failing-baseline.json": "542A589A15A3E7D6AE9762D248C8AC0E04713F395604ABA7A06D487456B9911E",
};
for (const [name, expected] of Object.entries(baselineHashes)) assert.equal(fileHash(path.join(here, name)), expected, `${name} immutable hash`);

const sourceContracts = [
  ["four source-owned config stores", (current.match(/usePreviewConfig\(/g) || []).length === 4],
  ["Master/profiles/Drill shared store", current.includes("afkConfig.draft.strategies") && current.includes("afkConfig.draft.allianceDrill") && current.includes("afkConfig.draft.enabled")],
  ["Potion independent store", current.includes("potionConfig.draft.minStamina") && current.includes("updatePotion")],
  ["Garrison independent store", current.includes("garrisonConfig.draft.targets") && current.includes("updateGarrison")],
  ["Zombie independent store", current.includes("zombieConfig.draft.enabled") && current.includes("updateZombie")],
  ["Master stop busy", current.includes('setProfileAction("stop")') && current.includes("previewAfkMasterStopAck(previewState)")],
  ["reorder busy before edit", current.indexOf('setProfileAction("reorder")') < current.indexOf("let changed = false; setProfiles")],
  ["source-valid available squads", current.includes("previewAfkAvailableSquads(previewState)") && current.includes("availableSquads={availableSquads}")],
  ["runtime game-text preference", current.includes('runtimeNames[row.joinTargetNameKey || ""] || row.joinTargetName')],
  ["runtime error translation", current.includes("translatedAfkError(t, row.lastError)")],
  ["native Garrison dialog", current.includes('<dialog ref={ref} className="app-dialog garrison-modal-backdrop"') && current.includes("dialog.showModal()")],
  ["Garrison name localization", current.includes("runtime.gameTexts?.[building.nameKey]") && current.includes("building?.allianceAbbr")],
  ["Garrison pending summary fallback", current.includes("garrisonDiscoveryReady ?") && current.includes(": garrisonConfig.draft.targets.length")],
  ["Garrison native action fence", current.includes('data-preview-action="presentation-only"')],
  ["final composition error surface", current.includes('automation-error monster-afk-error')],
];
for (const [label, pass] of sourceContracts) assert.equal(pass, true, label);

const browserPath = path.join(here, "browser-current-results.json");
const browser = JSON.parse(fs.readFileSync(browserPath, "utf8"));
assert.equal(browser.marker, "LWB317_REMAINING_M2_BROWSER_CURRENT_OK");
assert.equal(browser.cases.length, 70);
assert.equal(browser.cases.every((entry) => entry.pass), true);
assert.equal(browser.screenshots.length, 9);
assert.equal(browser.console.filter((entry) => ["error", "pageerror"].includes(entry.type)).length, 0);
assert.equal(browser.current.squadsPage.sha256.toUpperCase(), fileHash(currentPath), "browser packet stale: SquadsPage");
assert.equal(browser.current.fixtures.sha256.toUpperCase(), fileHash(fixturePath), "browser packet stale: fixtures");

const report = {
  marker: "LWB317_REMAINING_M2_VALIDATION_OK",
  sourceFunctions: expectedSource,
  immutableBaselines: baselineHashes,
  preservedSlices: ["AfkProfileEditor", "EquipmentContent", "CompactAfkCard"],
  sourceContracts: sourceContracts.map(([label]) => label),
  browserAssertions: browser.cases.length,
  screenshots: browser.screenshots.length,
  consoleErrors: 0,
  current: { squadsPageSha256: fileHash(currentPath), fixturesSha256: fileHash(fixturePath) },
  legacyNote: "The historical AFK-editor validator pins the pre-milestone whole SquadsPage file size and Unit-D check-afk-source-replay freezes the pre-milestone two-store/article harness. They are preserved unchanged; this validator proves their accepted editor/Equipment/compact slices remain byte-identical while the authorized surrounding full composition changed.",
};
fs.writeFileSync(path.join(here, "validation-results.json"), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
