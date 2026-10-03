import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '../../../..');
const workerPath = 'evidence/lwbridge-0.3.17/ui/LWB317-UI-EQUIPMENT-CLOSEOUT-001';
const worker = path.join(repo, workerPath);
const reference = 'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe';
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const source = readJson(path.join(worker, 'source-manifest.json'));
assert.equal(sha(fs.readFileSync(reference)), source.referenceExecutableSha256);
for (const asset of source.assets) assert.equal(sha(fs.readFileSync(path.join(repo, asset.path))), asset.sha256, asset.path);
for (const [name, locator] of Object.entries(source.locators)) {
  const bytes = fs.readFileSync(path.join(repo, 'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets', locator.asset));
  assert.equal(sha(bytes.subarray(locator.utf8ByteOffset, locator.utf8ByteOffset + locator.utf8ByteLength)), locator.sha256, name);
}
for (const [file, hash] of Object.entries(source.current)) assert.equal(sha(fs.readFileSync(path.join(repo, file))), hash, file);
assert.deepEqual(readJson(path.join(worker, 'baseline-results.json')), JSON.parse(execFileSync('git', ['show', `4bafcd437929f2f905b557ca700657467afbdcbc:${workerPath}/baseline-results.json`], { cwd: repo, encoding: 'utf8' })));
const entries = [];
function collect(directory) {
  for (const item of fs.readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, item.name);
    if (item.isDirectory()) collect(file);
    else {
      if (file.endsWith('.json')) readJson(file);
      entries.push({ path: path.relative(repo, file).replaceAll('\\', '/'), sha256: sha(fs.readFileSync(file)) });
    }
  }
}
collect(worker);
for (const file of Object.keys(source.current)) entries.push({ path: file, sha256: sha(fs.readFileSync(path.join(repo, file))) });
for (const file of ['independent-results.json', 'check-review.mjs', 'verification-results.json']) {
  const full = path.join(here, file);
  if (file.endsWith('.json')) readJson(full);
  entries.push({ path: path.relative(repo, full).replaceAll('\\', '/'), sha256: sha(fs.readFileSync(full)) });
}
const pinPath = path.join(here, 'review-manifest.json');
if (process.argv.includes('--record')) fs.writeFileSync(pinPath, JSON.stringify({ submittedCommit: '4bafcd437929f2f905b557ca700657467afbdcbc', referenceExecutableSha256: source.referenceExecutableSha256, files: entries }, null, 2) + '\n');
else assert.deepEqual(entries, readJson(pinPath).files, 'Evidence changed since lead review; preserve this historical manifest and create a new revision packet');
const report = readJson(path.join(here, 'independent-results.json'));
assert.equal(report.decision, 'CHANGES_REQUIRED');
assert.equal(report.hiddenTab.currentHiddenAltAction, 'apply-all:equipment-preset-fixed-2');
const screenshots = entries.filter(file => /\.(jpg|png)$/.test(file.path));
assert.equal(screenshots.length, 3);
console.log(`LWB317_EQUIPMENT_LEAD_EVIDENCE_OK assets=${source.assets.length} locators=${Object.keys(source.locators).length} screenshots=${screenshots.length} files=${entries.length} decision=CHANGES_REQUIRED`);
