// Explicit checkpoint writer. Validation never imports or calls this file.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {here,repo,hash,sourceLocator,raw,squad,editor} from './harness.mjs';
const prior='evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/lead-checkpoint/frozen-inputs.json';
const inherited=JSON.parse(fs.readFileSync(path.join(repo,prior),'utf8'));
const permitted=new Set([
 'src/LWBridge.UI-0.3.17/src/SquadsPage.jsx',
 'evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/progress.md',
 'evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/continuation.md',
].map(p=>path.resolve(repo,p)));
const all=new Set(),changed=[];
for(const file of inherited.files){
 const p=path.isAbsolute(file.path)?file.path:path.join(repo,file.path);
 const data=fs.readFileSync(p);all.add(path.resolve(p));
 if(hash(data)!==file.sha256.toLowerCase()){
  assert.ok(permitted.has(path.resolve(p)),'Unrelated inherited input changed: '+p);
  if(p.endsWith('SquadsPage.jsx'))assert.equal(hash(fs.readFileSync(path.join(here,'SquadsPage.baseline.jsx'))),file.sha256.toLowerCase(),'Baseline must equal prior frozen source');
  changed.push({path:file.path,previousSha256:file.sha256,sha256:hash(data)});
 }
}
all.add(path.join(repo,prior));
for(const name of ['docs/lwbridge-ui.md','docs/UI_FINISH_CHECKLIST.md','docs/implementation-handoff.md','docs/lwbridge-parity-matrix.md','docs/lwbridge-feature-ledger.md','task.md','docs/work-items/LWB317-UI-AFK-EDITOR-VISUAL-001.md','docs/reviews/2026-10-05-LWB317-UI-AFK-EDITOR-VISUAL-001.md'])all.add(path.resolve(repo,name));
fs.writeFileSync(path.join(here,'source-locators.json'),JSON.stringify(sourceLocator,null,2)+'\n');
fs.writeFileSync(path.join(here,'original-editor.js'),raw(squad,editor)+'\n');
function walk(root){for(const e of fs.readdirSync(root,{withFileTypes:true})){const p=path.join(root,e.name);if(e.isDirectory())walk(p);else if(e.name!=='frozen-inputs.json')all.add(p);}}
walk(here);
const files=[...all].sort().map(p=>{const data=fs.readFileSync(p);return {path:p.replaceAll('\\','/'),bytes:data.length,sha256:hash(data)};});
fs.writeFileSync(path.join(here,'frozen-inputs.json'),JSON.stringify({prior,priorSha256:hash(fs.readFileSync(path.join(repo,prior))),permittedChanges:changed,files,scope:'Inherited campaign dependency closure preserved except explicit editor/status delta, plus all bounded task evidence and current master docs. Preservation does not imply global acceptance.'},null,2)+'\n');
console.log('LWB317_AFK_EDITOR_INPUTS_FROZEN '+files.length+' inheritedChanges='+changed.length);
