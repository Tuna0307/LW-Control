import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';

// Only this evidence script/result is writable. This runs extracted production
// callbacks in inert dependency closures; it never mounts React or invokes native UI.
const scriptPath = fileURLToPath(import.meta.url);
let root = path.dirname(scriptPath);
while (!fs.existsSync(path.join(root, 'AGENTS.md'))) {
  const parent = path.dirname(root);
  if (parent === root) throw new Error('Repository root not found');
  root = parent;
}
const baselineCommit = '138ea26469b7a35c0758198c98f8c44f352037b0';
const ui = 'src/LWBridge.UI-0.3.17/src/';
const sourcePaths = ['App.jsx', 'MapDataPage.jsx', 'mapAutoConfig.js', 'autoScanNativeCoordinator.js', 'autoLaunchPreference.js', 'mapBackend.js'];
const sha = value => crypto.createHash('sha256').update(value).digest('hex');
const currentSources = Object.fromEntries(sourcePaths.map(name => [name, fs.readFileSync(path.join(root, ui, name), 'utf8')]));
const baselineSources = Object.fromEntries(sourcePaths.map(name => [name, execFileSync('git', ['show', `${baselineCommit}:${ui}${name}`], { cwd: root, encoding: 'utf8', maxBuffer: 2 ** 22 })]));
const helpers = await import(pathToFileURL(path.join(root, ui, 'mapAutoConfig.js')));
const { MAP_KIND_KEYS } = await import(pathToFileURL(path.join(root, ui, 'mapBackend.js')));
const { createAutoScanNativeCoordinator } = await import(pathToFileURL(path.join(root, ui, 'autoScanNativeCoordinator.js')));
const preference = await import(pathToFileURL(path.join(root, ui, 'autoLaunchPreference.js')));
const clone = value => structuredClone(value);
let assertionCount = 0;
const equal = (actual, expected, message) => { assertionCount++; assert.deepEqual(actual, expected, message); };
const truth = (actual, message) => { assertionCount++; assert.ok(actual, message); };

function closingBrace(source, start) {
  let depth = 0, quote = '', lineComment = false, blockComment = false;
  for (let index = start; index < source.length; index++) {
    const ch = source[index], next = source[index + 1];
    if (lineComment) { if (ch === '\n') lineComment = false; continue; }
    if (blockComment) { if (ch === '*' && next === '/') { blockComment = false; index++; } continue; }
    if (quote) { if (ch === '\\') index++; else if (ch === quote) quote = ''; continue; }
    if (ch === '/' && next === '/') { lineComment = true; index++; continue; }
    if (ch === '/' && next === '*') { blockComment = true; index++; continue; }
    if (ch === '"' || ch === "'" || ch === '`') { quote = ch; continue; }
    if (ch === '{') depth++;
    if (ch === '}' && --depth === 0) return index;
  }
  throw new Error('Unbalanced extracted function');
}
function extractCallback(source, name) {
  const marker = `const ${name} = useCallback(`;
  const markerAt = source.indexOf(marker);
  assert.ok(markerAt >= 0, `Missing callback ${name}`);
  const start = markerAt + marker.length;
  const bodyAt = source.indexOf('=> {', start) + 3;
  const end = closingBrace(source, bodyAt) + 1;
  return { text: source.slice(start, end), byteStart: Buffer.byteLength(source.slice(0, start)), byteEnd: Buffer.byteLength(source.slice(0, end)), line: source.slice(0, start).split('\n').length };
}
function extractFunction(source, name) {
  const start = source.indexOf(`function ${name}(`);
  assert.ok(start >= 0, `Missing function ${name}`);
  const end = closingBrace(source, source.indexOf('{', start)) + 1;
  return { text: source.slice(start, end), byteStart: Buffer.byteLength(source.slice(0, start)), byteEnd: Buffer.byteLength(source.slice(0, end)), line: source.slice(0, start).split('\n').length };
}
function compile(text, environment) {
  return new Function(...Object.keys(environment), `return (${text});`)(...Object.values(environment));
}
function baselineModule(source, names, environment = {}) {
  const body = source.replace(/^import .*;\r?\n/gm, '').replace(/^export /gm, '');
  return new Function(...Object.keys(environment), `${body}\nreturn {${names.join(',')}};`)(...Object.values(environment));
}
const baselineHelpers = baselineModule(baselineSources['mapAutoConfig.js'], Object.keys(helpers).filter(name => name !== 'applyAutoScanConfigIntent'), { MAP_KIND_KEYS });
const baselineCoordinator = baselineModule(baselineSources['autoScanNativeCoordinator.js'], ['createAutoScanNativeCoordinator']).createAutoScanNativeCoordinator;
// The baseline lacks the new intent helper; never silently substitute current code.
delete baselineHelpers.applyAutoScanConfigIntent;

const locators = {};
for (const [version, sources] of Object.entries({ current: currentSources, baseline: baselineSources })) {
  locators[version] = {};
  for (const name of ['updateAutoLaunch', 'updateAutoScanConfig']) locators[version][name] = extractCallback(sources['App.jsx'], name);
  locators[version].emitAutoConfig = extractCallback(sources['MapDataPage.jsx'], 'emitAutoConfig');
  for (const name of ['addAutoServers', 'toggleAutoType']) locators[version][name] = extractFunction(sources['MapDataPage.jsx'], name);
}

function memoryStorage(initial = {}) {
  const data = new Map(Object.entries(initial));
  return { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, String(value)) };
}
const ref = current => ({ current });
function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
async function until(predicate) {
  for (let index = 0; index < 100; index++) { if (predicate()) return; await new Promise(resolve => setImmediate(resolve)); }
  throw new Error('Inert callback queue did not reach expected barrier');
}
function globalHarness(version, initial, native) {
  const storage = memoryStorage({ 'lwbridge.autoLaunchGame': String(initial) });
  const owner = ref({ profileId: 'A', generation: 1 });
  const nativeByProfile = new Map([['A', native], ['B', !native]]);
  const writes = [], refs = {
    autoLaunchNativeCommittedByOwnerRef: ref(new Map([['A:1', native]])),
    autoLaunchGlobalCommittedRef: ref(initial), autoLaunchGlobalOwnerRef: ref(null),
    autoLaunchSaveRevisionRef: ref(0), autoLaunchSaveChainRef: ref(Promise.resolve()),
    autoLaunchNativeCommitEpochRef: ref(0),
  };
  let visible = initial, error = '';
  const backendBridge = { available: true, invoke(command, payload) {
    assert.equal(command, 'local_config_set');
    const gate = deferred();
    writes.push({ payload: clone(payload), succeed(value = payload.autoLaunchGame) { nativeByProfile.set(payload.profileId, value); gate.resolve({ autoLaunchGame: value }); }, fail() { gate.reject(new Error('inert global rejection')); } });
    return gate.promise;
  } };
  // Use the actual extracted owner-key implementation too.
  const profileOwnerKey = compile(extractFunction(currentSources['App.jsx'], 'profileOwnerKey').text, {});
  // Seed mirror with the exact production key rather than our readable placeholder.
  refs.autoLaunchNativeCommittedByOwnerRef.current = new Map([[profileOwnerKey(owner.current), native]]);
  const edit = value => compile(locators[version].updateAutoLaunch.text, {
    backendBridge, selectedProfileOwnerRef: owner, profileOwnerKey,
    isCurrentProfileOwner: candidate => candidate.profileId === owner.current.profileId && candidate.generation === owner.current.generation,
    ...refs, autoLaunchGame: visible, localStorage: storage,
    writeAutoLaunchGamePreference: preference.writeAutoLaunchGamePreference,
    setAutoLaunchGame: value => { visible = value; }, setGameActionError: value => { error = value; },
  })(value);
  return { edit, writes, refs, switch(profileId) { owner.current = { profileId, generation: owner.current.generation + 1 }; refs.autoLaunchNativeCommittedByOwnerRef.current.set(profileOwnerKey(owner.current), nativeByProfile.get(profileId)); }, get visible() { return visible; }, get stored() { return storage.getItem('lwbridge.autoLaunchGame'); }, get error() { return error; }, nativeByProfile };
}

function mapHarness(version, base, { hydration = 'deferred', mode = 'native', manualSaves = false, failReads = 0, failSaves = 0 } = {}) {
  const sources = version === 'current' ? currentSources : baselineSources;
  const hp = version === 'current' ? helpers : baselineHelpers;
  const factory = version === 'current' ? createAutoScanNativeCoordinator : baselineCoordinator;
  const storage = memoryStorage();
  let native = clone(base), nativeRevision = 1, visible = hp.freshAutoScanDefaults(), error = '', input = '', reads = 0;
  const hydrationGate = deferred(), writes = [], tasks = [], runtime = [];
  const configRef = ref(visible), coordinatorRef = ref(null);
  const mergeStart = sources['App.jsx'].indexOf('mergeConfig:');
  const mergeEnd = sources['App.jsx'].indexOf('onConfigSnapshot:', mergeStart);
  const mergeText = sources['App.jsx'].slice(mergeStart + 'mergeConfig:'.length, mergeEnd).trim().replace(/,$/, '');
  const mergeConfig = compile(mergeText, hp);
  const snapshot = () => ({ config: clone(native), revision: nativeRevision, running: false, lastError: '' });
  const coordinator = factory({
    mergeConfig,
    readStatus: async () => {
      reads++;
      if (failReads-- > 0) throw new Error('inert hydration rejection');
      if (hydration === 'deferred' && reads === 1) return hydrationGate.promise;
      return snapshot();
    },
    saveConfig: config => {
      const gate = deferred();
      const write = { config: clone(config), succeed() { native = clone(config); nativeRevision++; gate.resolve(snapshot()); }, fail() { gate.reject(new Error('inert config rejection')); } };
      writes.push(write);
      if (failSaves-- > 0) write.fail(); else if (!manualSaves) write.succeed();
      return gate.promise;
    },
    runNow: async () => snapshot(),
    onConfigSnapshot: next => { visible = hp.normalizeAutoScanConfig(next.config); configRef.current = visible; },
    onRuntimeSnapshot: next => runtime.push(clone(next)),
    onWriteError: value => { error = value; }, onActionError: () => {},
  });
  coordinatorRef.current = { save(...args) { const task = coordinator.save(...args); tasks.push(task); return task; } };
  const update = (...args) => compile(locators[version].updateAutoScanConfig.text, {
    ...hp, backendBridge: { mode }, autoScanConfigRef: configRef, autoScanCoordinatorRef: coordinatorRef,
    setAutoScanConfig: next => { visible = next; }, selectedProfileId: 'A', window: { localStorage: storage },
  })(...args);
  const emit = (...args) => compile(locators[version].emitAutoConfig.text, { onAutoScanConfig: update })(...args);
  const add = draft => {
    input = draft;
    compile(locators[version].addAutoServers.text, { ...hp, autoServerInput: input, autoConfig: visible, emitAutoConfig: emit, setAutoServerInput: next => { input = next; } })();
  };
  const type = (kind, checked) => compile(locators[version].toggleAutoType.text, { autoConfig: visible, emitAutoConfig: emit })(kind, checked);
  const removeStart = sources['MapDataPage.jsx'].indexOf('onClick={() => emitAutoConfig({ serverIds: removeAutoServerId');
  assert.ok(removeStart >= 0, 'Missing actual chip remove handler');
  const removeBrace = sources['MapDataPage.jsx'].indexOf('{', removeStart);
  const removeText = sources['MapDataPage.jsx'].slice(removeBrace + 1, closingBrace(sources['MapDataPage.jsx'], removeBrace));
  const remove = id => compile(removeText, { id, autoConfig: visible, emitAutoConfig: emit, removeAutoServerId: hp.removeAutoServerId })();
  return {
    add, type, remove, update, writes, tasks, coordinator, runtime, storage,
    hydrate() { hydrationGate.resolve(snapshot()); }, receive() { coordinator.receive(snapshot()); },
    async settle() { await Promise.all(tasks); },
    get visible() { return clone(visible); }, get native() { return clone(native); }, get error() { return error; }, get input() { return input; }, get reads() { return reads; },
  };
}

const results = [];
async function scenario(name, run) {
  const before = assertionCount;
  const observations = await run();
  results.push({ name, status: 'PASS', assertions: assertionCount - before, observations });
}
const nativeBase = overrides => ({ ...helpers.freshAutoScanDefaults(), enabled: true, serverIds: [317, 8], selectedTypes: ['city', 'treasure'], nextRunAt: 123456, ...overrides });

for (const initial of [false, true]) await scenario(`global divergent ${initial} / native ${!initial}`, async () => {
  const current = globalHarness('current', initial, !initial);
  const task = current.edit(!initial);
  equal(current.visible, !initial, 'Immediate global UI'); equal(current.stored, String(!initial), 'Immediate global storage');
  await until(() => current.writes.length === 1); current.writes[0].fail(); await task;
  equal(current.visible, initial, 'Current rollback uses global baseline'); equal(current.stored, String(initial), 'Stored rollback'); equal(current.nativeByProfile.get('A'), !initial, 'Rejected native gate unchanged'); truth(current.error.includes('rejection'), 'Truthful rejection');
  const old = globalHarness('baseline', initial, !initial), oldTask = old.edit(!initial);
  await until(() => old.writes.length === 1); old.writes[0].fail(); await oldTask;
  equal(old.visible, !initial, 'Immutable baseline reproduces native-first defect');
  return { currentGlobal: current.visible, baselineGlobal: old.visible, native: current.nativeByProfile.get('A') };
});
await scenario('global earlier success / later rejection preserves confirmation', async () => {
  const h = globalHarness('current', false, false), first = h.edit(true);
  await until(() => h.writes.length === 1); const second = h.edit(false);
  h.writes[0].succeed(); await until(() => h.writes.length === 2);
  equal(h.visible, false, 'Earlier acknowledgement cannot overwrite newer optimistic UI');
  equal(h.refs.autoLaunchGlobalCommittedRef.current, true, 'Earlier success advances confirmation');
  h.writes[1].fail(); await Promise.all([first, second]);
  equal(h.visible, true, 'Later rejection returns earlier successful global value'); equal(h.stored, 'true', 'Rollback storage');
  return { global: h.visible, native: h.nativeByProfile.get('A'), writes: h.writes.length };
});
await scenario('global stale A/B/A acknowledgement and queued retirement', async () => {
  const h = globalHarness('current', true, true), first = h.edit(false);
  await until(() => h.writes.length === 1); const queued = h.edit(true);
  h.switch('B'); h.switch('A'); const newA = h.edit(false);
  h.writes[0].succeed(); await until(() => h.writes.length === 2);
  equal(h.writes[1].payload.autoLaunchGame, false, 'Retired queued write was not dispatched');
  equal(h.refs.autoLaunchGlobalCommittedRef.current, true, 'Retired acknowledgement cannot confirm');
  h.writes[1].fail(); await Promise.all([first, queued, newA]);
  equal(h.visible, true, 'Replacement owner first edit rollback baseline'); equal(h.writes.length, 2, 'Only admitted requests dispatched');
  return { global: h.visible, native: h.nativeByProfile.get('A'), writes: h.writes.length };
});
await scenario('global preserved retired optimistic false becomes new-owner baseline', async () => {
  const h = globalHarness('current', true, true), first = h.edit(false);
  await until(() => h.writes.length === 1); h.switch('B'); h.switch('A'); const replacement = h.edit(true);
  h.writes[0].succeed(); await until(() => h.writes.length === 2); h.writes[1].fail();
  await Promise.all([first, replacement]); equal(h.visible, false, 'New owner preserves immediate global intent'); equal(h.stored, 'false', 'Preserved local authority');
  return { global: h.visible, confirmed: h.refs.autoLaunchGlobalCommittedRef.current };
});
await scenario('actual Add/type callbacks rebase ordered semantic edits before hydration', async () => {
  const h = mapHarness('current', nativeBase());
  h.update({ intervalMinutes: 35 }); h.add('9,317,10,9'); equal(h.input, '', 'Valid Add clears input');
  h.add('10;11'); h.remove(9); h.add('9'); h.type('resource', true); h.type('treasure', false); h.type('truck', false);
  equal(h.native.serverIds, [317, 8], 'Hydration barrier keeps native base');
  h.hydrate(); await h.settle();
  equal(h.native.serverIds, [317, 8, 10, 11, 9], 'Append/dedupe/remove-readd order'); equal(h.native.selectedTypes, ['city', 'resource'], 'Unknown saved City retained'); equal(h.native.intervalMinutes, 35, 'Scalar patch retained'); equal(h.native.nextRunAt, 123456, 'Native deadline retained'); equal(h.visible, h.native, 'Latest acknowledgement converges UI');
  h.add('invalid'); equal(h.input, 'invalid', 'Invalid-only Add preserves input'); equal(h.tasks.length, 8, 'Invalid Add creates no save');
  return { config: h.native, saves: h.writes.length };
});
await scenario('immutable baseline reproduces pre-hydration array replacement', async () => {
  const h = mapHarness('baseline', nativeBase()); h.add('9'); h.hydrate(); await h.settle();
  equal(h.native.serverIds, [9], 'Baseline Add overwrites saved targets');
  const current = mapHarness('current', nativeBase()); current.add('9'); current.hydrate(); await current.settle();
  equal(current.native.serverIds, [317, 8, 9], 'Current actual Add preserves native base');
  return { baseline: h.native.serverIds, current: current.native.serverIds };
});
await scenario('hydration rejection retains operations through later retry', async () => {
  const h = mapHarness('current', nativeBase(), { hydration: 'immediate', failReads: 1 });
  h.add('9'); await h.settle(); truth(h.error.includes('hydration rejection'), 'Hydration failure visible'); equal(h.writes.length, 0, 'Failed hydrate writes nothing');
  h.type('resource', true); h.add('10'); await h.settle();
  equal(h.native.serverIds, [317, 8, 9, 10], 'Failed Add retained during retry'); equal(h.native.selectedTypes, ['city', 'treasure', 'resource'], 'Type retry merges unrelated choices'); equal(h.error, '', 'Successful retry clears write error'); equal(h.reads, 2, 'Hydration retried');
  return { config: h.native, reads: h.reads };
});
await scenario('save rejection retains operations through later retry', async () => {
  const h = mapHarness('current', nativeBase(), { hydration: 'immediate', failSaves: 1 });
  h.add('9'); await h.settle(); truth(h.error.includes('config rejection'), 'Save failure visible'); equal(h.native.serverIds, [317, 8], 'Rejected save leaves native base');
  h.remove(9); h.add('10'); await h.settle();
  equal(h.native.serverIds, [317, 8, 10], 'Failed append/remove and new append compose'); equal(h.error, '', 'Successful retry clears error');
  return { config: h.native, saves: h.writes.length };
});
await scenario('pending save retains latest draft then rebases exact next operation', async () => {
  const h = mapHarness('current', nativeBase(), { hydration: 'immediate', manualSaves: true }); h.receive();
  h.add('9'); await until(() => h.writes.length === 1); h.remove(317); h.add('10');
  const latestDraft = h.visible; h.writes[0].succeed(); await until(() => h.writes.length === 2);
  equal(h.visible, latestDraft, 'Older success cannot overwrite pending draft'); equal(h.writes[1].config.serverIds, [8, 9], 'Second save rebases remove over first acknowledged append');
  h.writes[1].succeed(); await until(() => h.writes.length === 3); h.writes[2].succeed(); await h.settle();
  equal(h.native.serverIds, [8, 9, 10], 'Final ordered pending intents'); equal(h.visible, h.native, 'Final UI converges');
  return { config: h.native, saves: h.writes.length };
});
await scenario('repaired capped successful no-op is not resurrected after removal', async () => {
  const ids = Array.from({ length: 20 }, (_, index) => index + 1);
  const h = mapHarness('current', nativeBase({ serverIds: ids }), { hydration: 'immediate', manualSaves: true }); h.receive();
  h.add('21'); await until(() => h.writes.length === 1); h.remove(1); h.add('22');
  equal(h.writes[0].config.serverIds, ids, 'First Add at cap is no-op'); h.writes[0].succeed();
  await until(() => h.writes.length === 2); h.writes[1].succeed(); await until(() => h.writes.length === 3); h.writes[2].succeed(); await h.settle();
  equal(h.native.serverIds, [...ids.slice(1), 22], 'Successful Add21 no-op remains no-op after remove1'); equal(h.native.serverIds.length, 20, 'Combined limit preserved');
  return { config: h.native, saves: h.writes.length };
});
await scenario('last-type no-op remains retired after later type additions', async () => {
  const h = mapHarness('current', nativeBase({ selectedTypes: ['truck'] }), { hydration: 'deferred', manualSaves: true });
  // Visible recovered defaults contain other types, allowing the pre-hydration
  // Truck uncheck. On the native base it must be guarded as the final choice.
  h.type('truck', false); h.type('city', true); h.type('resource', true); h.hydrate();
  await until(() => h.writes.length === 1); equal(h.writes[0].config.selectedTypes, ['truck'], 'Acknowledged-base last-type guard'); h.writes[0].succeed();
  await until(() => h.writes.length === 2); h.writes[1].succeed(); await until(() => h.writes.length === 3); h.writes[2].succeed(); await h.settle();
  equal(h.native.selectedTypes, ['truck', 'city', 'resource'], 'Successful guarded no-op cannot later remove Truck');
  return { config: h.native, saves: h.writes.length };
});
await scenario('profile retirement rejects callbacks and undispatched queued operations', async () => {
  const old = mapHarness('current', nativeBase(), { hydration: 'immediate', manualSaves: true }); old.receive(); old.add('9');
  await until(() => old.writes.length === 1); old.add('10'); const draft = old.visible; old.coordinator.retire(); old.writes[0].succeed(); await old.settle();
  equal(old.writes.length, 1, 'Retired queued save not dispatched'); equal(old.visible, draft, 'Retired acknowledgement cannot rewrite visible draft'); equal(old.runtime.length, 1, 'Retired acknowledgement emits no runtime callback');
  const replacement = mapHarness('current', nativeBase({ serverIds: [77] }), { hydration: 'immediate' }); replacement.add('11'); await replacement.settle();
  equal(replacement.native.serverIds, [77, 11], 'Replacement owner progresses independently');
  return { retiredWrites: old.writes.length, replacement: replacement.native.serverIds };
});
await scenario('preview uses actual same callbacks and exact profile local persistence', async () => {
  const h = mapHarness('current', nativeBase(), { mode: 'preview' });
  h.add('9,10,9'); h.add('10,11'); h.remove(9); h.add('9'); h.type('city', true);
  const saved = helpers.loadAutoScanConfig('A', h.storage);
  equal(saved.serverIds, [10, 11, 9], 'Preview semantic ordered persistence'); equal(saved.selectedTypes, [...helpers.AUTO_SCAN_DEFAULT_TYPES, 'city'], 'Preview type intent'); equal(h.writes.length, 0, 'Preview invokes no native save'); equal(saved, h.visible, 'Preview reload contract');
  return { saved };
});

const output = {
  schemaVersion: 1, status: 'PASS', baselineCommit, nodeVersion: process.version,
  scriptSha256: sha(fs.readFileSync(scriptPath)),
  sources: Object.fromEntries(sourcePaths.map(name => [ui + name, { currentSha256: sha(fs.readFileSync(path.join(root, ui, name))), baselineGitBlobSha256: sha(Buffer.from(baselineSources[name])) }])),
  // Baseline git text is the immutable blob; current UTF-8 bytes include checkout CRLF.
  extractedCallbacks: Object.fromEntries(Object.entries(locators).map(([version, entries]) => [version, Object.fromEntries(Object.entries(entries).map(([name, item]) => [name, { line: item.line, byteStart: item.byteStart, byteEnd: item.byteEnd, bodySha256: sha(item.text) }]))])),
  scenarios: results, scenarioCount: results.length, assertionCount,
  proofLimits: [
    'Node-only inert closures execute exact extracted App/Map handlers plus actual helpers/coordinator; no React DOM mount, Enter event dispatch or checkbox disabled-state proof.',
    'Injected native/status/storage dependencies deterministically model acknowledgements and failures; no native service, build/package/browser/game/process action occurs.',
    'Current owner fencing is actual callback logic; owner selection/generation changes are inert harness inputs, not mounted profile controls.',
    'Immutable starting baseline R3-04 divergent rollback and R3-05 Add replacement negatives are reproduced without modifying historical evidence.',
    'Mounted Window proofs and broader native/session ownership are coordinator-owned and remain separate.',
  ],
};
const resultPath = path.join(path.dirname(scriptPath), 'check-frontend-inverses.results.json');
if (process.argv.includes('--verify')) {
  const previous = JSON.parse(fs.readFileSync(resultPath, 'utf8'));
  assert.deepEqual(output, previous, 'Write-safe verification must reproduce exact source-bound results');
  console.log(JSON.stringify({ status: 'PASS', mode: 'verify-no-write', scenarioCount: results.length, assertionCount }));
} else {
  fs.writeFileSync(resultPath, JSON.stringify(output, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ status: 'PASS', mode: 'create-results', scenarioCount: results.length, assertionCount, resultPath }));
}
