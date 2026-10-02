import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHarness, deferred, repo, tick } from '../LWB317-UI-MAP-FILTER-LIFECYCLE-001/harness.mjs';
import { createOriginalHarness, optionsReply } from '../LWB317-UI-MAP-INTERACTIONS-001/original/common.mjs';

// Lead-authored boundary checks. Actual canonical callbacks/effects and original
// component bytes; all provider replies are controlled, with no native actions.
const here = path.dirname(fileURLToPath(import.meta.url));
const source = fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'), 'utf8');
const results = [];
const reply = (sid, name) => optionsReply({ alliances: [{ name, count: 1 }], noAllianceCount: 1 })(sid);

async function canonical(label) {
  const pending = [];
  const h = await createHarness(source, label, {
    props: { previewState: 'map-city' },
    apiExtensions: () => ({ dataOptions: (sid) => {
      const call = { sid, ...deferred() };
      pending.push(call);
      return call.promise;
    } }),
  });
  await h.mount();
  return { h, pending };
}

// An options server redirect must not apply the redirected payload's lists.
{
  const { h, pending } = await canonical('lead-server-redirect');
  assert.equal(pending[0].sid, 321);
  pending[0].resolve(reply(322, 'WRONG-PAYLOAD'));
  await h.settle();
  const current = { serverId: h.getState('browseServerId'), optionServers: pending.map(c => c.sid) };
  assert.equal(h.getState('options'), null);
  const currentCall = pending.findLast(c => c.sid === current.serverId);
  assert.ok(currentCall);
  currentCall.resolve(reply(current.serverId, 'CURRENT'));
  await h.settle();
  assert.equal(h.getState('options').alliances[0].name, 'CURRENT');
  await h.unmount();

  const o = await createOriginalHarness({ online: false, stubs: { dataOptions: { mode: 'manual' } } });
  await o.mount();
  const first = o.callsNamed('dataOptions').findLast(c => c.pending);
  await o.resolveCall(first, reply(322, 'WRONG-PAYLOAD'));
  const original = { serverId: o.getState('dataServerId'), optionServers: o.callsNamed('dataOptions').map(c => c.args[0]) };
  assert.equal(original.serverId, 321);
  assert.deepEqual(original.optionServers, [321, 322, 321]);
  assert.deepEqual(o.getState('alliances'), []);
  const next = o.callsNamed('dataOptions').findLast(c => c.pending && c.args[0] === 321);
  await o.resolveCall(next, reply(321, 'CURRENT'));
  assert.equal(o.getState('alliances')[0].name, 'CURRENT');
  await o.unmount();
  results.push({ case: 'mismatched options response follows original server synchronization', proof: 'original and production callbacks', original, current,
    pass: JSON.stringify(current) === JSON.stringify(original) });
}

// Retiring options ownership must suppress both success and error writes.
for (const boundary of ['backend-loss', 'unmount']) {
  for (const outcome of ['success', 'failure']) {
    const { h, pending } = await canonical(`lead-${boundary}-${outcome}`);
    if (boundary === 'backend-loss') await h.setProps({ backendAvailable: false });
    else await h.unmount();
    if (outcome === 'success') pending[0].resolve(reply(321, 'OBSOLETE'));
    else pending[0].reject(new Error('OBSOLETE-ERROR'));
    await tick();
    await h.settle();
    assert.equal(h.getState('options'), null);
    assert.equal(h.getState('queryError'), '');
    if (boundary === 'backend-loss') await h.unmount();
    results.push({ case: `${boundary} ignores pending options ${outcome}`, proof: 'production callback; canonical availability boundary', pass: true });
  }
}

// A superseded rejection must not overwrite a current successful options reply.
{
  const { h, pending } = await canonical('lead-stale-rejection');
  await h.emitServer(322);
  const newer = pending.find(c => c.sid === 322);
  newer.resolve(reply(322, 'FRESH'));
  await h.settle();
  pending[0].reject(new Error('OLD-SERVER-ERROR'));
  await tick(); await h.settle();
  assert.equal(h.getState('options').alliances[0].name, 'FRESH');
  assert.equal(h.getState('queryError'), '');
  await h.unmount();
  results.push({ case: 'old-server options rejection cannot replace current success', proof: 'production callbacks', pass: true });
}

const failures = results.filter(r => !r.pass).length;
console.log('LWB317_PM027_INDEPENDENT cases=' + results.length + ' failures=' + failures);
console.log(JSON.stringify(results, null, 2));
if (process.argv.includes('--record')) fs.writeFileSync(path.join(here, 'independent-results.json'), JSON.stringify(results, null, 2) + '\n');
if (!process.argv.includes('--audit')) assert.equal(failures, 0, 'Map filter lifecycle differs from original; see independent results');
