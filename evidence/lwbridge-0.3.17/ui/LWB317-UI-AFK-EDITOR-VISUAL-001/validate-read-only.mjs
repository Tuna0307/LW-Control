import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {here,repo,hash,sourceLocator} from './harness.mjs';
const manifest=JSON.parse(fs.readFileSync(path.join(here,'frozen-inputs.json'),'utf8'));
for(const file of manifest.files){const data=fs.readFileSync(file.path);assert.equal(data.length,file.bytes,file.path);assert.equal(hash(data),file.sha256,file.path);}
assert.deepEqual(JSON.parse(fs.readFileSync(path.join(here,'source-locators.json'),'utf8')),sourceLocator);
const json=name=>JSON.parse(fs.readFileSync(path.join(here,name),'utf8'));
assert.equal(json('baseline-results.json').mismatches,24);assert.equal(json('renderer-results.json').mismatches,0);
assert.equal(json('behavior-results.json').cases,267);assert.equal(json('behavior-results.json').preservedCallbacks,16);
const mounted=json('mounted-results.json');assert.equal(mounted.cases.length,28);assert.equal(mounted.consoleIssues.length,0);assert.ok(mounted.screenshots.every(s=>s.documentWidth===s.viewport[0]));
for(const s of mounted.screenshots)assert.equal(hash(fs.readFileSync(path.join(here,s.screenshot))),s.sha256);
for(const phase of ['baseline','current']){
 const evidence=json(phase+'/pairs.json');assert.equal(evidence.pairs.length,12);assert.equal(evidence.issues.length,0);
 for(const p of evidence.pairs){assert.deepEqual(p.masks,[]);for(const side of ['original','current'])assert.equal(hash(fs.readFileSync(path.join(here,phase,p[side].screenshot))),p[side].sha256);if(phase==='current'){assert.ok(p.geometryAndStylesEqual);assert.ok(p.browserDomEqual);assert.ok(p.encodedPngEqual);}}
}
assert.ok(json('regression-results.json').results.every(r=>r.status===0));
// Execute fresh assertions without recording/replacing historical or current outputs.
for(const [command,args] of [
 [process.execPath,[path.join(here,'check-renderer.mjs')]],
 [process.execPath,[path.join(here,'check-behavior.mjs')]],
 ['python',[path.join(here,'check-pixels.py')]],
 [process.execPath,['evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs']],
]){const r=spawnSync(command,args,{cwd:repo,encoding:'utf8',windowsHide:true});assert.equal(r.status,0,r.stdout+'\n'+r.stderr);console.log(r.stdout.trim());}
// Detect accidental output writes as well as input mutation during validation.
for(const file of manifest.files)assert.equal(hash(fs.readFileSync(file.path)),file.sha256,file.path);
console.log('LWB317_AFK_EDITOR_EVIDENCE_OK files='+manifest.files.length+' renderer=24 pairs=12 masks=0 mounted=28');
