import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const appPath = path.join(root, 'src/LWBridge.UI-0.3.17/src/App.jsx');
const app = fs.readFileSync(appPath, 'utf8');
const helpers = await import(pathToFileURL(path.join(root, 'src/LWBridge.UI-0.3.17/src/mapAutoConfig.js')));
const { createAutoScanNativeCoordinator } = await import(pathToFileURL(path.join(root, 'src/LWBridge.UI-0.3.17/src/autoScanNativeCoordinator.js')));
const { connectionState } = await import(pathToFileURL(path.join(root, 'src/LWBridge.UI-0.3.17/src/mapBackend.js')));
const deferred = () => {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
};
const drain = async () => { for (let i = 0; i < 8; i++) await Promise.resolve(); };
const between = (left, right) => {
  const start = app.indexOf(left), end = app.indexOf(right, start);
  assert.ok(start >= 0 && end > start, 'Current-source extraction anchor missing');
  return app.slice(start, end);
};
const initialSource = between('function initialAutoScanConfig(', 'function RetainedPages(');
const initializer = new Function('backendBridge', 'normalizeAutoScanConfig', 'loadAutoScanConfig',
  'AUTO_SCAN_DEFAULT_TYPES', 'window', initialSource + '\nreturn initialAutoScanConfig;')(
  { mode: 'native' }, helpers.normalizeAutoScanConfig, helpers.loadAutoScanConfig,
  helpers.AUTO_SCAN_DEFAULT_TYPES, {});
const results = [];
{
  // Current App supplies native defaults before its asynchronous status read.
  const persisted = helpers.normalizeAutoScanConfig({ enabled: true, intervalMinutes: 60,
    serverIds: [321, 322], selectedTypes: ['city'], scanMode: 'normal',
    returnToOriginalServer: false, nextRunAt: 1_900_000_000_000 });
  let visible = initializer('A', '');
  const write = deferred();
  let submitted;
  const coordinator = createAutoScanNativeCoordinator({
    saveConfig: config => { submitted = config; return write.promise; },
    readStatus: async () => ({ revision: 5, config: persisted }),
    runNow: async () => { throw new Error('Not exercised'); },
    onConfigSnapshot: snapshot => { visible = helpers.normalizeAutoScanConfig(snapshot.config); },
    onRuntimeSnapshot: () => {}, onWriteError: () => {}, onActionError: () => {},
  });
  visible = helpers.applyAutoScanConfigEdit(visible, { ...visible, intervalMinutes: 45 }, 1_800_000_000_000);
  const saving = coordinator.save(visible);
  await drain();
  coordinator.receive({ revision: 5, config: persisted });
  write.resolve({ revision: 6, config: submitted });
  await saving;
  results.push({ case: 'unrelated-edit-before-first-native-hydration-loses-persisted-fields',
    expected: { enabled: true, serverIds: [321, 322], selectedTypes: ['city'], intervalMinutes: 45 },
    actual: { enabled: visible.enabled, serverIds: visible.serverIds,
      selectedTypes: visible.selectedTypes, intervalMinutes: visible.intervalMinutes },
    defectReproduced: !visible.enabled && visible.serverIds.length === 0 });
  coordinator.retire();
}
{
  // Execute the exact current paired-status callback with deferred native results.
  const callback = between('  const readStatusSnapshot = useCallback(', '  const refreshStatus = useCallback(');
  const calls = [0, 1].map(() => ({ status: deferred(), proxy: deferred() }));
  let statusIndex = 0, proxyIndex = 0;
  const state = { runtime: null, proxy: null, ready: false, error: '' };
  const refs = () => ({ current: 0 });
  const make = new Function('useCallback', 'backendBridge', 'selectedProfileOwnerRef',
    'isCurrentProfileOwner', 'setStatusPairReady', 'setConnectionError', 'reconnectStatusGeneration',
    'autoLaunchSaveRevisionRef', 'autoLaunchNativeCommitEpochRef', 'autoLaunchConfigPollGenerationRef',
    'mapApi', 'acknowledgeRuntimeStatus', 'setProxyStatus', 'autoLaunchCommittedRef',
    'writeAutoLaunchGamePreference', 'localStorage', 'setAutoLaunchGame', callback + '\nreturn readStatusSnapshot;');
  const read = make(f => f, { available: true, invoke: async () => ({}) },
    { current: { profileId: 'A', generation: 1 } }, () => true,
    ready => { state.ready = ready; }, error => { state.error = error; }, refs(), refs(), refs(), refs(),
    { readStatus: () => calls[statusIndex++].status.promise, readProxyStatus: () => calls[proxyIndex++].proxy.promise },
    runtime => { state.runtime = runtime; }, proxy => { state.proxy = proxy; },
    { current: false }, () => {}, {}, () => {});
  const oldRead = read(), newRead = read();
  calls[1].status.reject(new Error('Newer native failure'));
  calls[1].proxy.resolve({ gameRunning: false });
  await newRead;
  const afterNewer = connectionState(state.runtime, state.proxy, 'native', state.ready, state.error);
  calls[0].status.resolve({ xluaOnline: true });
  calls[0].proxy.resolve({ gameRunning: true });
  await oldRead;
  const afterOlder = connectionState(state.runtime, state.proxy, 'native', state.ready, state.error);
  results.push({ case: 'older-connected-pair-revives-after-newer-failure', afterNewer,
    afterOlder, defectReproduced: afterNewer === 'unavailable' && afterOlder === 'connected' });
}
{
  let error = '';
  const runtimeSource = between('  const applyAutoScanRuntimeSnapshot = useCallback(', '  useLayoutEffect(() => {\n    autoScanCoordinatorRef');
  const applyRuntime = new Function('useCallback', 'setAutoScanRunning', 'setAutoScanError',
    runtimeSource + '\nreturn applyAutoScanRuntimeSnapshot;')(f => f, () => {}, value => { error = value; });
  const coordinator = createAutoScanNativeCoordinator({ saveConfig: async () => null,
    readStatus: async () => null, runNow: async () => { throw new Error('Run now rejected'); },
    onConfigSnapshot: () => {}, onRuntimeSnapshot: applyRuntime,
    onWriteError: () => {}, onActionError: value => { error = value; },
  });
  await coordinator.run();
  const afterRejection = error;
  coordinator.receive({ revision: 9, running: false, lastError: null });
  results.push({ case: 'clean-runtime-event-clears-independent-run-now-error', afterRejection,
    afterCleanRuntime: error, defectReproduced: afterRejection === 'Run now rejected' && error === '' });
  coordinator.retire();
}
assert.ok(results.every(result => result.defectReproduced), 'Finding no longer reproduces; reassess source');
const sources = ['App.jsx', 'autoScanNativeCoordinator.js', 'mapAutoConfig.js', 'mapBackend.js'].map(name => ({
  path: 'src/LWBridge.UI-0.3.17/src/' + name,
  sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(root, 'src/LWBridge.UI-0.3.17/src', name))).digest('hex'),
}));
fs.writeFileSync(path.join(here, 'counterexamples.json'), JSON.stringify({
  head: '99d8f7b513ae56a0aa3b4d92a202f81586a32924',
  proofType: 'exact-current-initializer/callbacks/coordinator-with-controlled-promises',
  externalActions: 0, nativeCommands: 0, sources, results,
}, null, 2) + '\n');
console.log(`LWB317_RECOVERY_CLOSEOUT_DEFECTS_REPRODUCED ${results.length}/${results.length}`);
