import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const read = name => JSON.parse(fs.readFileSync(path.join(here, name), 'utf8').replace(/^\uFEFF/, ''));
const hash = buffer => crypto.createHash('sha256').update(buffer).digest('hex');
const checkpoint = read('worker-checkpoint.json');
assert.equal(checkpoint.head, '540bc53d73053bb1eb70d6a09fea851a9e96d625');
assert.equal(checkpoint.files.length, 29);
for (const entry of checkpoint.files) {
  const file = fs.readFileSync(path.resolve(root, entry.path));
  assert.equal(file.length, entry.bytes, `${entry.path} length changed`);
  assert.equal(hash(file), entry.sha256.toLowerCase(), `${entry.path} worker bytes changed`);
}
const checks = read('runner-results.json');
assert.equal(checks.length, 6);
assert.equal(new Set(checks.map(check => check.flag)).size, 6);
for (const check of checks) {
  assert.equal(check.exitCode, 0, check.flag);
  const log = fs.readFileSync(path.resolve(root, check.log), 'utf8');
  assert.match(log, /"ok"\s*:\s*true/, check.flag);
}
const callbacks = read('auto-callback-races.json');
assert.equal(callbacks.externalActions, 0);
assert.equal(callbacks.results.length, 3);
assert.ok(callbacks.results.every(result => result.defectReproduced));
assert.equal(callbacks.sourceSha256, hash(fs.readFileSync(path.join(root, 'src/LWBridge.UI-0.3.17/src/App.jsx'))));
const owner = read('auto-owner-repro-result.json');
assert.equal(owner.externalActions, 0);
assert.equal(owner.expectedManualStopped, false);
assert.equal(owner.defectReproduced, true);
assert.deepEqual(owner.stoppedRuns, ['manual-M']);
for (const name of ['home-review.md', 'map-review.md', 'frontend-auto-review.md']) {
  assert.ok(fs.statSync(path.join(here, name)).size > 1000);
}
console.log('LWB317_HOME_MAP_LEAD_AUDIT_OK workerFiles=29 focusedChecks=6 callbackDefects=3 nativeCoreDefects=1');
