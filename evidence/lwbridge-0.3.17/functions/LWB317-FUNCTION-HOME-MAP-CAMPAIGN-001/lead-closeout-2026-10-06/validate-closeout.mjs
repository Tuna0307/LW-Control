import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const json = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const obs = json(path.join(here, 'observations.json'));
const negative = json(path.join(here, 'counterexamples.json'));
assert.equal(negative.head, obs.head);
assert.equal(obs.decision, 'CHANGES_REQUIRED');
assert.equal(obs.nativeFocusedPrograms.ownerIsolationAccepted, false);
assert.equal(obs.postRunLeaseObservation.preRunBaseline, 'UNKNOWN');
assert.equal(negative.nativeCommands, 0);
assert.equal(negative.results.length, 3);
assert.ok(negative.results.every(result => result.defectReproduced));
for (const source of negative.sources) assert.equal(hash(path.join(root, source.path)), source.sha256);
const runs = json(path.join(here, 'runner-results.json'));
assert.equal(runs.length, 7);
for (const run of runs) {
  assert.equal(run.exitCode, 0);
  assert.match(fs.readFileSync(path.resolve(root, run.log), 'utf8'), /"ok"\s*:\s*true/);
}
const release = path.join(root, 'src/LWBridge.Desktop/bin/Release/net10.0-windows10.0.17763.0');
assert.equal(hash(path.join(release, 'LWBridge.Desktop.exe')), obs.releasePackageIdentity.executableSha256);
assert.equal(hash(path.join(release, 'LWBridge.Desktop.dll')), obs.releasePackageIdentity.managedAssemblySha256);
const identity = json(path.join(release, 'ProductionUi/lwbridge-ui-build.json'));
for (const key of ['sourceFingerprint', 'artifactFingerprint']) assert.equal(identity[key], obs.releasePackageIdentity[key]);
for (const [name, language, theme, width] of [
  ['recovery-v3-en-light', 'en', 'light', 1120],
  ['recovery-v3-ja-dark-narrow', 'ja', 'dark', 900],
]) {
  const file = path.resolve(here, '../integration', name + '.json');
  const packet = json(file);
  assert.equal(packet.schemaVersion, 3);
  assert.equal(packet.externalGameActions, 0);
  assert.equal(packet.package.executableSha256, obs.releasePackageIdentity.executableSha256);
  assert.equal(packet.package.managedAssemblySha256, obs.releasePackageIdentity.managedAssemblySha256);
  assert.equal(packet.package.uiIndexSha256, hash(path.join(release, 'ProductionUi/index.html')));
  assert.equal(packet.appearance.final.language, language);
  assert.equal(packet.appearance.final.theme, theme);
  assert.equal(packet.appearance.final.viewport.width, width);
  assert.equal(packet.appearance.final.viewport.scrollWidth, width);
  assert.equal(packet.shutdown.activeRequests, 0);
  assert.equal(packet.shutdown.activeSubscriptions, 0);
  assert.deepEqual(packet.shutdown.cleanupFailures, []);
  const png = fs.readFileSync(path.resolve(here, '../integration', name + '.png'));
  assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
  assert.equal(png.readUInt32BE(16), width);
}
console.log('LWB317_RECOVERY_LEAD_CLOSEOUT_EVIDENCE_OK nativeOutputs=7 isolationAccepted=false reproducedDefects=3 packetIdentities=2');
