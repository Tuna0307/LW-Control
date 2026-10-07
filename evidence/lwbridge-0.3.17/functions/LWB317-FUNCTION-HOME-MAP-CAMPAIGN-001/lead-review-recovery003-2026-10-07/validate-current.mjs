import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
let root = here;
while (!fs.existsSync(path.join(root, 'AGENTS.md'))) root = path.dirname(root);
const campaign = 'evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/';
const packetRoot = path.join(root, campaign, 'recovery-003-2026-10-07');
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const json = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const record = { workerCommit: '88591df03cd92f1d28b710a0d107a096266fd360', proofCommit: 'd90f764aa2a721e6bf26952a81fb1a6161bec0d4', packets: [], screenshots: 0 };
for (const [stem, language, theme, width] of [
  ['package-en-light-attempt5', 'en', 'light', 1120],
  ['package-ja-dark-narrow-attempt1', 'ja', 'dark', 900],
]) {
  const packet = json(path.join(packetRoot, stem + '.json'));
  assert.equal(packet.schemaVersion, 5);
  assert.equal(packet.state, 'proven');
  assert.equal(packet.externalGameActions, 0);
  assert.equal(packet.appearance.final.language, language);
  assert.equal(packet.appearance.final.theme, theme);
  assert.equal(packet.appearance.final.viewport.width, width);
  assert.equal(packet.appearance.final.viewport.height, 720);
  const home = packet.home;
  for (const [field, value] of [['divergentAutoLaunchRollback', 'false'], ['inverseAutoLaunchRollback', 'true'], ['concurrentAutoLaunchRollback', 'false']]) {
    assert.equal(home[field].checked, value);
    assert.equal(home[field].globalStorage, value);
  }
  const auto = packet.autoScan;
  assert.deepEqual(auto.beforeHydrationRelease.config.serverIds, [317]);
  assert.deepEqual(auto.mergedHydrationSnapshot.config.serverIds, [317, 10, 11, 9]);
  assert.deepEqual(auto.mergedHydrationSnapshot.config.selectedTypes, ['city', 'resource']);
  assert.deepEqual(auto.pendingArraySnapshot.config.serverIds, [317, 13]);
  assert.deepEqual(auto.pendingArraySnapshot.config.selectedTypes, ['city', 'treasure', 'resource']);
  assert.deepEqual(auto.hydrationRetrySnapshot.config.serverIds, [317, 14]);
  assert.equal(auto.hydrationRetrySnapshot.config.intervalMinutes, 40);
  assert.equal(packet.browserIssues.unexpectedCount, 0);
  assert.equal(packet.browserIssues.preReloadSentinelRetained, true);
  assert.equal(packet.browserIssues.queuedPriorDocumentIssueRetained, true);
  const shutdown = packet.shutdown;
  assert.equal(shutdown.sessionClosed, true);
  assert.equal(shutdown.requestRegistryClosed, true);
  assert.equal(shutdown.activeRequests, 0);
  assert.equal(shutdown.activeSubscriptions, 0);
  assert.equal(shutdown.profileRuntimeEventsDetached, true);
  assert.equal(shutdown.isolatedRootRemoved, true);
  assert.deepEqual(shutdown.cleanupFailures, []);
  for (const suffix of ['', '-home-rollback', '-auto-rebase']) {
    const file = path.join(packetRoot, stem + suffix + '.png');
    const bytes = fs.readFileSync(file);
    assert.equal(bytes.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
    assert.equal(bytes.readUInt32BE(16), width);
    assert.equal(bytes.readUInt32BE(20), 720);
    record.screenshots++;
  }
  for (const [fileField, hashField] of [['executablePath', 'executableSha256'], ['managedAssemblyPath', 'managedAssemblySha256']])
    assert.equal(sha(packet.package[fileField]), packet.package[hashField].toLowerCase(), 'Release artifact must match recorded run');
  record.packets.push({stem, language, theme, width, height: 720, sha256: sha(path.join(packetRoot, stem + '.json'))});
}
const historical = campaign + 'lead-review-2026-10-07';
assert.equal(execFileSync('git', ['diff', '138ea26469b7a35c0758198c98f8c44f352037b0', '--name-only', '--', historical], {cwd: root, encoding:'utf8'}).trim(), '', 'Historical five negative witnesses remain unchanged');
record.status = 'PASS';
record.limits = ['Packet validation and current artifact identity, not a new UI run.', 'PNG headers are checked here; all six images were separately decoded with Pillow.', 'No real game, Lua/xLua host, provider-positive or protected-service execution is established.'];
if (process.argv.includes('--record')) fs.writeFileSync(path.join(here, 'packet-validation.json'), JSON.stringify(record, null, 2) + '\n', {flag:'wx'});
console.log('LWB317_RECOVERY003_LEAD_PACKET_OK packets=2 screenshots=6 historicalNegatives=5 preserved=true');
