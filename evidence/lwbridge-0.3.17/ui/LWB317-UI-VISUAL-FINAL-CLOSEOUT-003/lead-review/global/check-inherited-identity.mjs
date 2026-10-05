import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// Independent read-only audit. No production, historical evidence or manifests are written.
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../../../');
const root = 'evidence/lwbridge-0.3.17/ui/';
const hash = b => crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const json = p => JSON.parse(fs.readFileSync(path.join(repo,p),'utf8'));
const bytes = p => fs.readFileSync(/^[A-Za-z]:\//.test(p) ? p : path.join(repo,p));
const d = json(root+'LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-d/host-reconciliation-manifest.json');
let pins=0, slices=0, inheritedProofs=0;
for (const [p,h] of Object.entries(d.inheritedLivePins)) {
  assert.equal(hash(bytes(p)), h.toUpperCase(), p); pins++;
}
for (const [p,h] of Object.entries(d.inheritedProofFiles)) {
  assert.equal(hash(bytes(p)), h.toUpperCase(), p); inheritedProofs++;
}
const older = json(root+'LWB317-UI-VISUAL-REMAINING-002/milestone-5/manifest.json');
for(const r of older.referenceFiles) {
  const b=bytes(r.path); assert.equal(b.length,r.bytes,r.path); assert.equal(hash(b),r.sha256.toUpperCase(),r.path);
}
const expectedCounts = {e:30,f:18,g:78,h:63};
for (const unit of ['e','f','g','h']) {
  const inv=json(root+`LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-${unit}/current/inventory.json`);
  assert.equal(hash(bytes(inv.currentSource.path)),inv.currentSource.sha256.toUpperCase());
  assert.equal(hash(bytes(inv.originalCSS.path)),inv.originalCSS.sha256.toUpperCase());
  assert.equal(inv.statesRendered.length,expectedCounts[unit]);
  for(const r of inv.functions) {
    const b=bytes(r.path).subarray(r.utf8Offset,r.utf8Offset+r.bytes);
    assert.equal(b.length,r.bytes); assert.equal(hash(b),r.sha256.toUpperCase(),r.name); slices++;
  }
}
const panel=fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/sharedPageUI.jsx'),'utf8');
assert.ok(panel.includes('subtitleClassName = "muted"'));
assert.ok(panel.includes('<span className={subtitleClassName}>'));
const changed = ['App.jsx','AutomationPage.jsx','RallyJoinSettings.jsx','SquadsPage.jsx','sharedPageUI.jsx'];
console.log(JSON.stringify({marker:'LWB317_LEAD_GLOBAL_INHERITED_IDENTITY_OK',livePins:pins,inheritedProofs,referenceFiles:older.referenceFiles.length,exactOriginalSlices:slices,acceptedEHStateCounts:expectedCounts,sharedPanelTitleDefaultPreserved:true,changedProductionBoundary:changed}));
