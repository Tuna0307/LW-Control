import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here,'../../../..');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const read = name => JSON.parse(fs.readFileSync(path.join(here,name),'utf8'));
const result = read('row-action-results.json');
assert.equal(result.status,'PASS');
assert.equal(result.actualOriginalCurrentComparisons,160);
assert.equal(result.previewIsolation,true);
assert.equal(result.nativeTrackingUnavailableFence,true);
assert.equal(result.blockedNativeActions,7);
const reference = fs.readFileSync(path.join(repo,result.source.path));
assert.equal(hash(reference),result.source.sha256);
const anchor = result.source.de;
assert.equal(reference.subarray(anchor.byte,anchor.byte+Buffer.byteLength(anchor.expression)).toString(),anchor.expression);
assert.equal(hash(fs.readFileSync(path.join(repo,result.current.path))),result.current.sha256);
assert.equal(read('transport-current-summary.json').mismatchCount,0);
const browser = read('browser-results.json');
assert.equal(browser.online,false);
assert.equal(browser.synthetic,true);
assert.deepEqual(browser.consoleErrors,[]);
assert.equal(browser.flows.length,8);
for (const language of ['en','ja']) for (const kind of ['truck','railway']) {
  const flow = browser.flows.find(f=>f.language===language && f.kind===kind);
  assert.equal(flow.rows.length,4);
  const column = kind==='truck'?1:0;
  assert.equal(flow.rows[0].cells[column],'-');
  assert.equal(flow.rows[3].cells[column],'-');
  const following = flow.rows[2].actions[0];
  assert.notEqual(following.text,flow.rows[1].actions[0].text);
  assert.equal(following.disabled,true);
  assert.equal(flow.rows[1].actions[0].disabled,true);
}
const truck = browser.flows.find(f=>f.language==='en' && f.kind==='truck');
assert.equal(truck.rows[3].label,'Select , server 0');
const city = browser.flows.find(f=>f.language==='en' && f.kind==='city');
assert.match(city.rows[0].className,/is-marked.*is-missing/);
assert.match(city.rows[1].className,/is-replaced/);
assert.match(city.rows[0].actions[0].title,/Original position is empty/);
assert.equal(city.rows[3].cells[1],'-');
assert.equal(city.rows[4].cells[1],'-');
assert.equal(city.rows[3].actions.length,1);
assert.equal(city.rows[4].actions.length,1);
assert.ok(city.rows.every(r=>r.actions.every(a=>a.disabled)));
for (const item of read('image-manifest.json')) assert.equal(hash(fs.readFileSync(path.join(here,item.file))),item.sha256);
// Preserve historical current-source hashes: validate them against the original
// checkpoint instead of replacing old records with corrected-source results.
const oldRoot = path.join(repo,'evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001');
const oldManifest = JSON.parse(fs.readFileSync(path.join(oldRoot,'manifest.json'),'utf8'));
for (const item of oldManifest.productionFiles) {
  const old = execFileSync('git',['show',`6a03a32:${item.file}`],{cwd:repo,encoding:'utf8'}).replace(/\r\n/g,'\n');
  assert.equal(hash(old),item.sha256NormalizedLF);
}
for (const item of JSON.parse(fs.readFileSync(path.join(oldRoot,'protected-wip.json'),'utf8'))) {
  const bytes = fs.readFileSync(path.join(repo,item.path));
  assert.equal(hash(bytes).toLowerCase(),item.sha256);
  assert.equal(bytes.length,item.bytes);
}
const verification = read('verification.json');
for (const [name,entry] of Object.entries(verification.checks)) assert.equal(entry,'PASS',name);
const identity = JSON.parse(fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/dist/lwbridge-ui-build.json'),'utf8'));
assert.equal(identity.sourceFingerprint,verification.package.sourceFingerprint);
assert.equal(identity.artifactFingerprint,verification.package.artifactFingerprint);
console.log('LWB317_MAP_ROWS_EVIDENCE_OK current/source/browser/history/protected-WIP');
