import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createAppHarness } from '../LWB317-UI-MAP-AUTO-CONFIG-001/app-harness.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const source = fs.readFileSync(path.join(repo, 'src/LWBridge.UI-0.3.17/src/App.jsx'), 'utf8');
const now = 1_800_001_000_000;
const h = await createAppHarness(source, 'lead-auto-transitions', { profileId: 'lead-ui-proof', now });
const results = [];
const pass = name => results.push({ name, pass: true });
await h.mount();
assert.equal(h.storageWrites.filter(([k]) => k.startsWith('lwbridge.mapAutoScan.')).length, 0);
pass('missing-storage-load-does-not-write');

let p = h.pageProps();
p.onAutoScanConfig({ ...p.autoScanConfig, enabled: true, nextRunAt: 42 });
await h.settle();
assert.equal(h.pageProps().autoScanConfig.nextRunAt, now);
assert.equal(JSON.parse(h.storage.get('lwbridge.mapAutoScan.lead-ui-proof')).nextRunAt, now);
pass('enable-overrides-supplied-deadline-with-clock-in-state-and-storage');

p = h.pageProps();
p.onAutoScanConfig({ ...p.autoScanConfig, scanMode: 'normal', nextRunAt: now + 12345 });
await h.settle();
assert.equal(h.pageProps().autoScanConfig.nextRunAt, now + 12345);
pass('enabled-edit-preserves-supplied-future-deadline');

p = h.pageProps();
p.onAutoScanConfig({ ...p.autoScanConfig, intervalMinutes: 'bad' });
await h.settle();
assert.ok(Number.isNaN(h.pageProps().autoScanConfig.intervalMinutes));
assert.equal(JSON.parse(h.storage.get('lwbridge.mapAutoScan.lead-ui-proof')).intervalMinutes, null);
pass('source-NaN-state-is-serialized-to-null');

const saved = Object.fromEntries(h.storage);
await h.unmount();
const loaded = await createAppHarness(source, 'lead-auto-reload', { profileId: 'lead-ui-proof', storage: saved, now });
await loaded.mount();
assert.equal(loaded.pageProps().autoScanConfig.intervalMinutes, 60);
assert.equal(loaded.pageProps().autoScanConfig.nextRunAt, now + 12345);
pass('reload-null-interval-restores-default-without-losing-deadline');

p = loaded.pageProps();
p.onAutoScanConfig({ ...p.autoScanConfig, enabled: false, nextRunAt: now + 54321 });
await loaded.settle();
assert.equal(loaded.pageProps().autoScanConfig.nextRunAt, 0);
assert.equal(JSON.parse(loaded.storage.get('lwbridge.mapAutoScan.lead-ui-proof')).nextRunAt, 0);
pass('disable-clears-positive-deadline-in-state-and-storage');

p = loaded.pageProps();
p.onAutoScanConfig({ ...p.autoScanConfig, enabled: true });
p.onAutoScanConfig({ ...p.autoScanConfig, enabled: false });
await loaded.settle();
assert.equal(loaded.pageProps().autoScanConfig.enabled, false);
assert.equal(loaded.pageProps().autoScanConfig.nextRunAt, 0);
assert.equal(JSON.parse(loaded.storage.get('lwbridge.mapAutoScan.lead-ui-proof')).enabled, false);
pass('same-turn-enable-disable-keeps-last-edit-without-stale-deadline');
await loaded.unmount();

const record = {
  scope: 'Independent lead cases executing actual App through inert hook/storage adapter; no native operation. Source contracts: index Ct byte 364217, Ei 359327, Ai/ji 360013/360170 in the pinned Auto locator packet.',
  appSha256: crypto.createHash('sha256').update(source).digest('hex'),
  cases: results,
};
fs.writeFileSync(path.join(here, 'auto-independent-results.json'), JSON.stringify(record, null, 2) + '\n');
console.log(`LWB317_PM_UI_FINISH_AUTO_INDEPENDENT_OK cases=${results.length}`);
