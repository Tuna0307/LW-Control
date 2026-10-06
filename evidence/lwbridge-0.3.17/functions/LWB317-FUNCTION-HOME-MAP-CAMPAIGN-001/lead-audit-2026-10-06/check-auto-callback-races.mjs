import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Lead-only audit: execute exact current callback bodies with controlled promises.
// No React mount, WebView, native command, storage write, or game provider.
const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const appPath = path.join(root, 'src/LWBridge.UI-0.3.17/src/App.jsx');
const app = fs.readFileSync(appPath, 'utf8');
const helpers = await import(pathToFileURL(path.join(root, 'src/LWBridge.UI-0.3.17/src/mapAutoConfig.js')));
const begin = app.indexOf('  const acknowledgeAutoScanSnapshot = useCallback(');
const end = app.indexOf('  const runAutoScanNow = useCallback(', begin);
if (begin < 0 || end < 0) throw new Error('Current callback extraction anchor missing');
const callbacks = app.slice(begin, end);
const defer = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};
const drain = async () => { for (let i = 0; i < 8; i++) await Promise.resolve(); };
function fixture() {
  const profileRef = { current: 'A' };
  const configRef = { current: helpers.normalizeAutoScanConfig(null) };
  const writes = [], statuses = [];
  let visible = configRef.current;
  const api = {
    updateAutoScanConfig(config) { const d = defer(); writes.push({ ...d, config }); return d.promise; },
    autoScanStatus() { const d = defer(); statuses.push(d); return d.promise; },
  };
  const create = new Function('useCallback', 'normalizeAutoScanConfig', 'applyAutoScanConfigEdit',
    'autoScanConfigRef', 'setAutoScanConfig', 'setAutoScanRunning', 'setAutoScanError',
    'backendBridge', 'selectedProfileId', 'selectedProfileIdRef', 'mapApi',
    'saveAutoScanConfig', 'window', `${callbacks}\nreturn { updateAutoScanConfig, acknowledgeAutoScanSnapshot };`);
  const bound = create(f => f, helpers.normalizeAutoScanConfig, helpers.applyAutoScanConfigEdit,
    configRef, next => { visible = next; }, () => {}, () => {}, { mode: 'native' },
    'A', profileRef, api, () => { throw new Error('Preview storage must not run'); }, {});
  return { ...bound, profileRef, configRef, writes, statuses, get visible() { return visible; } };
}
const results = [];
{
  const f = fixture();
  f.updateAutoScanConfig({ ...f.visible, intervalMinutes: 45 });
  f.updateAutoScanConfig({ ...f.visible, serverIds: [321] });
  f.writes[1].resolve({ config: f.writes[1].config }); await drain();
  f.writes[0].resolve({ config: f.writes[0].config }); await drain();
  results.push({ case: 'older-save-reply-overwrites-newer-edit', expectedServers: [321],
    actualServers: f.visible.serverIds, defectReproduced: f.visible.serverIds.length === 0 });
}
{
  const f = fixture();
  const oldSnapshot = { config: f.visible };
  f.updateAutoScanConfig({ ...f.visible, intervalMinutes: 45 });
  f.acknowledgeAutoScanSnapshot(oldSnapshot);
  f.updateAutoScanConfig({ ...f.visible, returnToOriginalServer: false });
  results.push({ case: 'old-status-or-event-loses-new-edit-on-next-save', expectedInterval: 45,
    actualSubmittedInterval: f.writes[1].config.intervalMinutes,
    defectReproduced: f.writes[1].config.intervalMinutes === 60 });
}
{
  const f = fixture();
  f.updateAutoScanConfig({ ...f.visible, intervalMinutes: 45 });
  f.writes[0].reject(new Error('Controlled rejection')); await drain();
  if (f.statuses.length !== 1) throw new Error('Expected actual failed-save recovery request');
  f.profileRef.current = 'B';
  f.configRef.current = helpers.normalizeAutoScanConfig({ intervalMinutes: 90, serverIds: [322] });
  f.statuses[0].resolve({ config: helpers.normalizeAutoScanConfig({ intervalMinutes: 20, serverIds: [321] }) });
  await drain();
  results.push({ case: 'A-failed-save-status-applied-after-selecting-B', expectedProfileBInterval: 90,
    actualInterval: f.configRef.current.intervalMinutes,
    defectReproduced: f.configRef.current.intervalMinutes === 20 });
}
const output = { schemaVersion: 1, proofType: 'exact-current-callback-execution-with-controlled-promises',
  sourceSha256: crypto.createHash('sha256').update(fs.readFileSync(appPath)).digest('hex'),
  extractedCallbacksSha256: crypto.createHash('sha256').update(callbacks).digest('hex'),
  externalActions: 0, results };
fs.writeFileSync(path.join(here, 'auto-callback-races.json'), JSON.stringify(output, null, 2) + '\n');
if (!results.every(r => r.defectReproduced)) throw new Error('Lead finding no longer reproduces; reassess current source');
console.log(`LEAD_AUTO_CALLBACK_DEFECTS_REPRODUCED ${results.length}/${results.length}`);
