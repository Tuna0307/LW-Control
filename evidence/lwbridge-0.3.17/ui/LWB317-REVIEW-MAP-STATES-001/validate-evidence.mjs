import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const readJson = name => JSON.parse(fs.readFileSync(path.join(here, name), 'utf8'));
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const normalized = file => fs.readFileSync(path.join(repo, file), 'utf8').replace(/\r\n/g, '\n');
const normalizedHere = file => fs.readFileSync(path.join(here, file), 'utf8').replace(/\r\n/g, '\n');
const sourceResults = readJson('source-local-results.json');
const browser = readJson('browser-results.json');

const referenceExe = 'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe';
const referenceExeSha256 = '4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783';
const sourcePath = 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js';
const sourceSha256 = 'CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089';

assert.equal(hash(fs.readFileSync(referenceExe)), referenceExeSha256);
const sourceBytes = fs.readFileSync(path.join(repo, sourcePath));
assert.equal(hash(sourceBytes), sourceSha256);
assert.equal(sourceResults.task, 'LWB317-REVIEW-MAP-STATES-001');
assert.equal(sourceResults.result, 'LWB317_REVIEW_MAP_STATES_SOURCE_LOCAL_OK');
assert.equal(sourceResults.source.path, sourcePath);
assert.equal(sourceResults.source.sha256, sourceSha256);
assert.deepEqual(sourceResults.counts, {
  timestampCases: 7,
  taskCasesPerKindPerLocale: 14,
  taskComparisons: 56,
  treasureWorldCasesPerLocale: 9,
  treasurePlayerCasesPerLocale: 11,
  treasureNameCasesPerLocale: 9,
  renderedTables: 6,
});

function anchor(bytes, entry) {
  const length = Buffer.byteLength(entry.expression);
  assert.equal(bytes.subarray(entry.utf8ByteOffset, entry.utf8ByteOffset + length).toString('utf8'), entry.expression);
}

for (const entry of Object.values(sourceResults.source.locators)) anchor(sourceBytes, entry);
for (const entry of Object.values(sourceResults.source.tableFunctions)) anchor(sourceBytes, entry);
anchor(sourceBytes, sourceResults.source.treasureTypes);
for (const entry of Object.values(sourceResults.source.treasureRefreshing)) anchor(sourceBytes, entry);

const helperBytes = Buffer.from(normalized(sourceResults.production.helperPath));
const pageBytes = Buffer.from(normalized(sourceResults.production.pagePath));
assert.equal(hash(helperBytes), sourceResults.production.helperSha256NormalizedLF);
assert.equal(hash(pageBytes), sourceResults.production.pageSha256NormalizedLF);
for (const name of ['mapTaskState', 'mapTaskSelectable', 'treasureName']) anchor(helperBytes, sourceResults.production.locators[name]);
for (const name of ['MapTable', 'MapTableCall']) anchor(pageBytes, sourceResults.production.locators[name]);
assert.equal(sourceResults.production.treasureRefreshingPropPassedByPage, false);

const statusTexts = ['In progress', 'Fully plundered', 'Protected', 'Expired', 'Available'];
const statusClasses = ['pending', 'full', 'protected', 'expired', 'ready'];
const taskCase = name => {
  const value = browser.cases.find(entry => entry.case === name);
  assert.ok(value, name);
  assert.deepEqual(value.statuses.map(entry => entry.text), statusTexts);
  assert.deepEqual(value.statuses.map(entry => entry.className), statusClasses.map(name => `map-task-status ${name}`));
  assert.deepEqual(value.disabledSelections, [false, true, false, true, false]);
  assert.ok(value.coordinateButtonsDisabled.every(Boolean));
  return value;
};

taskCase('secret-task-en-fresh');
taskCase('ghost-ops-en-fresh');
const treasureEn = browser.cases.find(entry => entry.case === 'treasure-en');
const treasureJa = browser.cases.find(entry => entry.case === 'treasure-ja');
assert.ok(treasureEn && treasureJa);
assert.deepEqual(treasureEn.firstRows.map(row => [row[3], row[4]]), [
  ['Charging 38%', 'Not claimed'],
  ['Claimable', 'Claimed'],
  ['Depleted', 'Other alliance'],
]);
assert.deepEqual(treasureJa.firstRows.map(row => [row[3], row[4]]), [
  ['充電中 38%', '未受取'],
  ['受取可能', '受取済み'],
  ['受取終了', '他同盟'],
]);
for (const value of [treasureEn, treasureJa]) {
  assert.ok(value.coordinateButtonsDisabled.every(Boolean));
  assert.ok(value.claimButtonsDisabled.every(Boolean));
}
assert.deepEqual(browser.consoleErrors, []);
assert.equal(browser.cleanup.reviewerTabClosed, true);
assert.equal(browser.cleanup.reviewerViteServerStopped, true);
assert.deepEqual(browser.deadlineObservation.freshGhostPass, statusTexts);

const screenshotFiles = browser.screenshots.map(entry => entry.file).sort();
assert.deepEqual(screenshotFiles, ['secret-task-states.jpg', 'treasure-ja.jpg']);
const screenshots = browser.screenshots.map(entry => {
  const bytes = fs.readFileSync(path.join(here, entry.file));
  assert.equal(bytes.subarray(0, 3).toString('hex'), 'ffd8ff');
  assert.equal(bytes.length, entry.bytes);
  assert.equal(hash(bytes), entry.sha256);
  assert.equal(entry.visuallyInspected, true);
  assert.ok(bytes.length > 10_000);
  return { file: entry.file, sha256: hash(bytes), bytes: bytes.length };
});
assert.equal(new Set(screenshots.map(entry => entry.sha256)).size, screenshots.length);

const productionFiles = [
  sourceResults.production.helperPath,
  sourceResults.production.pagePath,
  'src/LWBridge.UI-0.3.17/src/mapPreviewApi.js',
];
const evidenceFiles = ['check-review-map-states.mjs', 'source-local-results.json', 'browser-results.json'];
const require = createRequire(path.join(repo, 'src/LWBridge.UI-0.3.17/package.json'));
const manifest = {
  task: 'LWB317-REVIEW-MAP-STATES-001',
  recommendation: 'ACCEPT',
  reviewInputs: {
    implementation: 'bf84bbdca8a86c1a45e9cbb3bd0170c8fbfcdd7f',
    deliveryBaseline: '3939ac045ef9c01ac454e06f8a35c72a220cea4b',
    startingHead: 'ceffe100f896af7dc8ce6ea5f443c9ace31ea6fd',
  },
  referenceExe,
  referenceExeSha256,
  source: { path: sourcePath, sha256: sourceSha256 },
  productionFiles: productionFiles.map(file => ({ file, sha256NormalizedLF: hash(normalized(file)) })),
  evidence: evidenceFiles.map(file => ({ file, sha256NormalizedLF: hash(normalizedHere(file)) })),
  screenshots,
  browser: {
    previewState: browser.previewState,
    cases: browser.cases.map(entry => entry.case),
    consoleErrors: browser.consoleErrors,
  },
  tools: {
    node: process.version,
    babelParser: require('@babel/parser/package.json').version,
    esbuild: require('esbuild/package.json').version,
  },
  limits: 'Recovered-source/local UI presentation for Secret Task, Ghost Ops and Treasure only. Synthetic preview data does not establish gameplay/native actions or original post-auth pixel parity. The recovered Treasure refreshing formatter matches when supplied, but current MapDataPage does not expose the original async refreshing producer flag to MapTable.',
};

const manifestPath = path.join(here, 'manifest.json');
if (process.argv.includes('--record')) fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
assert.deepEqual(readJson('manifest.json'), manifest);
console.log('LWB317_REVIEW_MAP_STATES_EVIDENCE_OK');
