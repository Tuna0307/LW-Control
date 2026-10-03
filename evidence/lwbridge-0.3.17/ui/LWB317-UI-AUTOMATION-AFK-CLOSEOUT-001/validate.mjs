import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {here,repo} from './harness.mjs';
const json=name=>JSON.parse(fs.readFileSync(path.join(here,name),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const manifest=json('source-locators.json');
assert.equal(manifest.targetExecutable.sha256,'4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783');
assert.equal(hash(fs.readFileSync(manifest.targetExecutable.path)),manifest.targetExecutable.sha256,'actual reference EXE');
for(const asset of manifest.assets)assert.equal(hash(fs.readFileSync(path.join(repo,asset.path))),asset.sha256,asset.path);
for(const [name,locator] of Object.entries(manifest.locators)){
 const bytes=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets',locator.asset));
 assert.equal(hash(bytes.subarray(locator.utf8ByteOffset,locator.utf8ByteOffset+locator.utf8ByteLength)),locator.sha256,name);
}
for(const file of manifest.files)assert.equal(hash(fs.readFileSync(path.join(repo,file.path),'utf8').replace(/\r\n/g,'\n')),file.sha256LF,file.path);
assert.ok(manifest.screenshots.length>=6);
for(const file of manifest.screenshots){const bytes=fs.readFileSync(path.join(repo,file.path));assert.equal(hash(bytes),file.sha256,file.path);assert.ok(file.visuallyInspected);assert.ok(bytes.length>1000);}
const browser=json('browser-results.json');assert.equal(browser.console.length,0);assert.ok(browser.records.length>=15);
for(const id of ['join-self-confirm','two-id-discard-navigation','departed-member-modal-escape','range-custom-restore-ja','garrison-availability-member-last-squad','assist-preview-select-schedule-cancel','assist-empty-quality-validation','gather-new-squad-radius-retention-ja','inactive-afk-data-fence','drill-wait-detail-ja','potion-range-master-local-toggle','zombie-runtime-table'])assert.equal(browser.records.find(r=>(r.id||r.case)===id)?.result,'PASS',id);
const verification=json('verification-results.json');assert.ok(verification.results.length>=10);assert.ok(verification.results.every(r=>r.result==='PASS'));
const report=json('actual-source-results.json');assert.equal(report.result,'LWB317_AUTOMATION_AFK_ACTUAL_SOURCE_OK');assert.ok(report.baseline.every(r=>r.failed));
const actual=JSON.parse(execFileSync(process.execPath,[path.join(here,'check-closeout.mjs')],{cwd:repo,encoding:'utf8',maxBuffer:8e6}));assert.deepEqual(actual.results,report.results);
for(const script of ['recover-join-renderer.mjs','recover-assist-renderer.mjs'])execFileSync(process.execPath,[path.join(here,script)],{cwd:repo,stdio:'pipe'});
const protectedGuard=path.join(here,'../LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs');execFileSync(process.execPath,[protectedGuard],{cwd:repo,stdio:'pipe'});
console.log(`LWB317_AUTOMATION_AFK_CLOSEOUT_EVIDENCE_OK cases=${report.results.reduce((s,r)=>s+r.count,0)} locators=${Object.keys(manifest.locators).length} screenshots=${manifest.screenshots.length} browser=${browser.records.length} protected=7`);
