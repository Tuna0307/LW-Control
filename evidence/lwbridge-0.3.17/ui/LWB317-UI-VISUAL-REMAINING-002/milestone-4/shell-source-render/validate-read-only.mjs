import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../../../..');
const hash = (value) => crypto.createHash('sha256').update(value).digest('hex').toUpperCase();
const readJson = (file) => JSON.parse(fs.readFileSync(path.join(here, file), 'utf8'));

const locators = readJson('source-locators.json');
assert.equal(locators.assetSha256, '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
assert.deepEqual(locators.functions.Gi, { byteOffset: 361306, byteLength: 14461, sha256: 'AD5ECE3C33F5FB057E250B67ABF8D32B4F482FD4A2414ABF55A79A3B3333BFD5' });
assert.deepEqual(locators.functions.Fn, { byteOffset: 208687, byteLength: 330, sha256: '590CD67C68F8A77B53F82185E6417DE75BA06DE81DC7281D67FF394A47E4941B' });
assert.deepEqual(locators.functions.Yr, { byteOffset: 339285, byteLength: 7154, sha256: 'FE5D0408492E71E9DD74615C269699B8C75B55D346F823BC22B05992C330A490' });
assert.deepEqual(locators.functions.si, { byteOffset: 354710, byteLength: 3232, sha256: '32E9CFF50FC34FCD3B91B2A3256AEC4DC9C605E98713B4F8F0ED536A4B94B405' });
assert.deepEqual(locators.functions.Ji, { byteOffset: 376111, byteLength: 881, sha256: '72F95F7202BC43FD3F9E449DC77133A08C57DE1C8D6F48AB929DCC1D34F48C21' });

const dependencies = readJson('dependencies.json');
for (const dependency of dependencies.files) {
  const file = path.join(repo, dependency.path);
  assert.ok(fs.existsSync(file), `dependency exists: ${dependency.path}`);
  assert.equal(hash(fs.readFileSync(file)), dependency.sha256, `dependency hash: ${dependency.path}`);
}

execFileSync(process.execPath, [path.join(here, 'render-shell-pairs.mjs'), '--verify'], { cwd: repo, stdio: 'pipe' });
execFileSync('python', [path.join(here, 'compare-shell-pairs.py'), '--verify'], { cwd: repo, stdio: 'pipe' });
execFileSync(process.execPath, [path.join(here, 'mutation-check.mjs'), '--verify'], { cwd: repo, stdio: 'pipe' });

const renderer = readJson('renderer-inputs.json');
const requiredCases = [
  'base-single-en-light', 'update-connected-en-light', 'profiles-compact-en-light', 'profile-cached-return-en-light',
  'profiles-expanded-en-light', 'profiles-error-forced-open-en-light', 'profile-note-modal-en-light', 'profile-reorder-dragover-en-light',
  'profile-loading-en-light', 'four-save-errors-en-light', 'cross-server-closed-en-light', 'cross-server-open-en-light',
  'exit-idle-en-light', 'exit-busy-en-light', 'narrow-profiles-ja-dark', 'base-single-ja-dark',
];
const byId = new Map(renderer.cases.map((entry) => [entry.id, entry]));
for (const id of requiredCases) assert.ok(byId.has(id), `required case ${id}`);
assert.equal(renderer.cases.length, requiredCases.length, 'no missing/extra shell cases');
for (const entry of renderer.cases.filter((item) => item.showProfiles)) {
  assert.deepEqual(entry.structuralChecks.profileSidebarWrapper, { original: true, current: true }, `${entry.id} sidebar ancestry`);
}
assert.equal(byId.get('profile-loading-en-light').structuralChecks.originalProfileLoading, true);
assert.equal(byId.get('profile-loading-en-light').structuralChecks.currentProfileLoading, true);
assert.equal(byId.get('profile-cached-return-en-light').structuralChecks.originalProfileLoading, false);
assert.equal(byId.get('profile-cached-return-en-light').structuralChecks.currentProfileLoading, false);
assert.equal(byId.get('four-save-errors-en-light').structuralChecks.originalSaveErrorCount, 4);
assert.equal(byId.get('four-save-errors-en-light').structuralChecks.currentSaveErrorCount, 4);
assert.equal(byId.get('cross-server-open-en-light').structuralChecks.originalCrossServerOpen, true);
assert.equal(byId.get('cross-server-open-en-light').structuralChecks.currentCrossServerOpen, true);
for (const id of ['exit-idle-en-light','exit-busy-en-light']) assert.deepEqual(byId.get(id).structuralChecks.exitDialogSiblingAfterShell, { original: true, current: true });

const errorOriginal = fs.readFileSync(path.join(here, byId.get('profiles-error-forced-open-en-light').originalHtml), 'utf8');
const errorCurrent = fs.readFileSync(path.join(here, byId.get('profiles-error-forced-open-en-light').currentHtml), 'utf8');
assert.match(errorOriginal, /class="profile-list"/); assert.doesNotMatch(errorOriginal, /class="profile-list collapsed"/);
assert.match(errorCurrent, /class="profile-list"/); assert.doesNotMatch(errorCurrent, /class="profile-list collapsed"/);
assert.match(errorOriginal, /class="profile-error"/); assert.match(errorCurrent, /class="profile-error"/);

const save = byId.get('four-save-errors-en-light');
const labels = (html) => [...html.matchAll(/<div class="automation-error" role="alert"><strong>(.*?) <\/strong>/g)].map((match) => match[1]);
const originalLabels = labels(fs.readFileSync(path.join(here, save.originalHtml), 'utf8'));
const currentLabels = labels(fs.readFileSync(path.join(here, save.currentHtml), 'utf8'));
assert.equal(originalLabels.length, 4); assert.deepEqual(currentLabels, originalLabels, 'four save errors preserve recovered order');

const updateCurrent = fs.readFileSync(path.join(here, byId.get('update-connected-en-light').currentHtml), 'utf8');
assert.match(updateCurrent, /class="top-update-button" type="button" disabled=""/);
assert.match(updateCurrent, /class="top-action secondary" type="button" disabled="">Refresh Status<\/button>/);
const compactOriginal = fs.readFileSync(path.join(here, byId.get('profile-cached-return-en-light').originalHtml), 'utf8');
const compactCurrent = fs.readFileSync(path.join(here, byId.get('profile-cached-return-en-light').currentHtml), 'utf8');
assert.match(compactOriginal, /profile-compact-item active/); assert.match(compactCurrent, /profile-compact-item active/);

const pairs = readJson('browser/pairs.json');
assert.equal(pairs.consoleIssues.length, 0, 'browser console/page errors');
assert.equal(pairs.pairs.length, requiredCases.length);
for (const pair of pairs.pairs) {
  for (const side of ['original','current']) for (const kind of ['measurement','screenshot','html']) {
    const file = path.join(here, 'browser', pair[side][kind]);
    assert.equal(hash(fs.readFileSync(file)), pair[side][`${kind}Sha256`], `${pair.id} ${side} ${kind} hash`);
  }
  assert.deepEqual(pair.masks, [], `${pair.id} unmasked`);
}
const cachedPair = pairs.pairs.find((entry) => entry.id === 'profile-cached-return-en-light');
assert.equal(cachedPair.hover, '.profile-compact-item.active'); assert.equal(cachedPair.focus, '.profile-collapse');
for (const side of ['original','current']) {
  const measurement = JSON.parse(fs.readFileSync(path.join(here, 'browser', cachedPair[side].measurement), 'utf8'));
  assert.match(measurement.focused, /profile-collapse/);
}

const comparison = readJson('browser/comparison-results.json');
assert.equal(comparison.pairs, requiredCases.length);
assert.equal(comparison.cases.length, requiredCases.length);
assert.ok(comparison.cases.every((entry) => entry.masks.length === 0));
for (const entry of comparison.cases.filter((item) => byId.get(item.id)?.showProfiles)) {
  assert.deepEqual(entry.originalAncestry, { parent: 'ASIDE', parentClass: 'profile-sidebar', grandparentClass: 'app-layout' });
  assert.deepEqual(entry.currentAncestry, { parent: 'ASIDE', parentClass: 'profile-sidebar', grandparentClass: 'app-layout' });
}
for (const id of ['exit-idle-en-light','exit-busy-en-light']) {
  const entry = comparison.cases.find((item) => item.id === id); assert.equal(entry.originalExitAfterShell, true); assert.equal(entry.currentExitAfterShell, true);
}

const mutation = readJson('mutation-result.json');
assert.equal(mutation.result, 'SHELL_MUTATION_DETECTED');
assert.ok(mutation.mutations.every((entry) => entry.detected));
console.log(JSON.stringify({ result: 'SHELL_SOURCE_RENDER_VALIDATE_OK', cases: requiredCases.length, browserPairs: pairs.pairs.length, consoleIssues: 0, mutationDetections: mutation.mutations.length, rawChangedPixels: comparison.totalChangedPixels }));
