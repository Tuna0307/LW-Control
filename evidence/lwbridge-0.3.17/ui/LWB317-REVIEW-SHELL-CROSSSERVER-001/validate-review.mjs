import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const read = relative => fs.readFileSync(path.join(repo, relative));
const packet = 'evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001';
const independent = JSON.parse(fs.readFileSync(path.join(here, 'independent-results.json')));
assert.equal(independent.result, 'LWB317_CROSSSERVER_INDEPENDENT_OK');
assert.equal(independent.cases.length, 11);
assert.ok(independent.cases.every(entry => entry.status === 'PASS'));
assert.equal(independent.current.appSha256, hash(read('src/LWBridge.UI-0.3.17/src/App.jsx')));
assert.equal(independent.current.appSha256, '7FD95D74D66A762F19DC202E88944C0FD450155104BE33E80B4B3F1A26797F5C');
assert.equal(independent.original.assetSha256, '44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6');
const currentChecks = JSON.parse(read(`${packet}/milestone-c/current-checks.json`));
assert.equal(currentChecks.result, 'LWB317_CROSSSERVER_CURRENT_CHECKS_OK');
assert.equal(currentChecks.checks.length, 7);
assert.ok(currentChecks.checks.every(entry => entry.status === 'PASS'));
const integrity = execFileSync('node', [`${packet}/milestone-c/validate-current.mjs`], {
  cwd: repo, encoding: 'utf8', windowsHide: true,
}).trim();
assert.match(integrity, /LWB317_CROSSSERVER_CURRENT_INTEGRITY_OK/);
const paths = [
  `${packet}/milestone-b/run-focused.mjs`, `${packet}/milestone-b/focused-results.json`,
  `${packet}/milestone-c/run-regressions.mjs`, `${packet}/milestone-c/regression-results.json`,
  `${packet}/milestone-c/run-current-checks.mjs`, `${packet}/milestone-c/current-checks.json`,
  `${packet}/milestone-c/validate-current.mjs`, `${packet}/milestone-c/browser/observations.json`,
];
// Normalize text hashes so Git's existing CRLF checkout policy does not invalidate
// this new text manifest. Original asset and App exact-byte checks above stay exact.
const textHash = relative => hash(read(relative).toString('utf8').replaceAll('\r\n', '\n'));
const inputs = Object.fromEntries(paths.map(relative => [relative, textHash(relative)]));
const report = {
  result: 'LWB317_CROSSSERVER_LEAD_REVIEW_OK',
  reviewedDelivery: '52710d3d1950cceccee2439c4979d3b99c797782',
  decision: 'COMPLETE / ACCEPTED for assigned source/local UI scope',
  independentCases: 11, currentChecks: currentChecks.checks, integrity, inputs,
  browser: 'Three worker screenshots inspected; worker interactions retained as worker observations, no new lead browser session.',
  productionCorrection: false,
};
const recordPath = path.join(here, 'verification.json');
if (process.argv.includes('--record')) {
  fs.writeFileSync(recordPath, JSON.stringify(report, null, 2) + '\n');
} else {
  assert.deepEqual(JSON.parse(fs.readFileSync(recordPath, 'utf8')), report);
}
console.log('LWB317_CROSSSERVER_LEAD_REVIEW_OK independent=11 focused=12 affected=3 protected=7');
