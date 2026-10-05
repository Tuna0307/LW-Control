import assert from "node:assert/strict";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, "../../../../../..");
const req = createRequire(path.join(repo, "src/LWBridge.UI-0.3.17/package.json"));
const { parse } = req("@babel/parser");
const read = (file) => fs.readFileSync(path.join(repo, file), "utf8");
const sha256 = (value) => crypto.createHash("sha256").update(value).digest("hex").toUpperCase();

const sourceFile = "evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js";
const source = read(sourceFile);
const snapshotFile = "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/baseline/pre-fix-AutomationPage.jsx";
const snapshot = read(snapshotFile);
const contractsFile = "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/baseline/pre-fix-previewAutomationContracts.js";
const fixturesFile = "evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/baseline/pre-fix-previewAutomationFixtures.js";

const ast = parse(source, { sourceType: "module" });
let ae;
for (const node of ast.program.body) {
  const declaration = node.type === "ExportNamedDeclaration" ? node.declaration : node;
  if (declaration?.type === "FunctionDeclaration" && declaration.id?.name === "Ae") ae = declaration;
}
assert.ok(ae, "recovered Automation Ae function missing");
const aeRaw = source.slice(ae.start, ae.end);
const locator = {
  asset: "AutomationPanel-BJ0gIqFh.js",
  utf8ByteOffset: Buffer.byteLength(source.slice(0, ae.start)),
  utf8ByteLength: Buffer.byteLength(aeRaw),
  sha256: sha256(aeRaw),
};
assert.deepEqual(locator, {
  asset: "AutomationPanel-BJ0gIqFh.js",
  utf8ByteOffset: 21817,
  utf8ByteLength: 53453,
  sha256: "61F181D014CFA82BD35CF3BBAE1DB3096CBE502826DDCD4680AD4C46C11A4B68",
});

const checks = [
  {
    id: "source-treasure-uses-discovered-squads",
    sourceEvidence: "d=[...u,...Ut.filter(e=>!ee.has(e))]",
    pass: /dispatchSquads\?\?\[\][\s\S]{0,300}Ut\.filter/.test(aeRaw),
  },
  {
    id: "baseline-treasure-hardcodes-four-squads",
    currentEvidence: "[1,2,3,4].map(...dispatchSquads...)",
    pass: /\[1,2,3,4\]\.map\(\(index\)\s*=>\s*<label[^>]*key=\{index\}>/.test(snapshot),
  },
  {
    id: "source-treasure-priority-is-draggable",
    sourceEvidence: "automation-squad-priority-item + draggable + drag handle",
    pass: /automation-squad-priority-item[\s\S]{0,1400}draggable:[\s\S]{0,1800}automation-squad-drag-handle/.test(aeRaw),
  },
  {
    id: "baseline-treasure-lacks-priority-row",
    currentEvidence: "Treasure branch uses automation-squad-choices labels rather than priority rows",
    pass: /treasureDispatchEnabled[\s\S]{0,6000}automation-squad-choices/.test(snapshot)
      && !/treasureDispatchEnabled[\s\S]{0,6000}automation-squad-priority-item/.test(snapshot),
  },
  {
    id: "source-alliance-gather-uses-discovered-squads",
    sourceEvidence: "sr=[...ar,...Ut.filter(e=>!or.has(e))]",
    pass: /squadPriority\?\?\[\][\s\S]{0,300}Ut\.filter/.test(aeRaw),
  },
  {
    id: "baseline-alliance-gather-hardcodes-four-squads",
    currentEvidence: "ordered=[...selected,...[1,2,3,4].filter(...)]",
    pass: /const ordered = \[\.\.\.selected, \.\.\.\[1, 2, 3, 4\]\.filter/.test(snapshot),
  },
  {
    id: "source-resource-gather-error-owned-by-card",
    sourceEvidence: "me returns AutomationCard with error:l||void 0",
    pass: /function me\([\s\S]{0,5500}error:l\|\|void 0/.test(source),
  },
  {
    id: "baseline-resource-gather-error-inside-fieldset",
    currentEvidence: "ResourceGatherCard renders error after squad rows inside automation-config-body",
    pass: /<fieldset[^>]+automation-config-body[\s\S]{0,7000}\{error \? <p className="automation-error"/.test(snapshot),
  },
  {
    id: "source-config-save-errors-precede-panel-title",
    sourceEvidence: "Ae maps P/I/L through recovered Oe before panel-title",
    pass: /Object\.entries\(P\)\.map[\s\S]{0,1400}panel-title/.test(aeRaw),
  },
  {
    id: "baseline-generic-config-save-errors-live-inside-card-body",
    currentEvidence: "AutomationConfigPreview renders PreviewConfigError inside AutomationCard fieldset",
    pass: /function AutomationConfigPreview[\s\S]{0,550}<PreviewConfigError/.test(snapshot)
      && /<fieldset className="automation-config-body"[\s\S]{0,700}AutomationConfigPreview/.test(snapshot),
  },
];

for (const check of checks) assert.equal(check.pass, true, check.id);

const result = {
  marker: "LWB317_REMAINING_M3_FAILING_BASELINE_CAPTURED",
  recoveredAe: locator,
  immutableSnapshots: {
    AutomationPage: { path: snapshotFile, sha256: sha256(snapshot) },
    previewAutomationContracts: { path: contractsFile, sha256: sha256(read(contractsFile)) },
    previewAutomationFixtures: { path: fixturesFile, sha256: sha256(read(fixturesFile)) },
  },
  mismatches: checks.map(({ pass, ...check }) => ({ ...check, observed: pass })),
};
const out = path.join(here, "failing-baseline.json");
fs.writeFileSync(out, `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify({ marker: result.marker, mismatches: result.mismatches.length, recoveredAe: locator, snapshots: result.immutableSnapshots }, null, 2));
