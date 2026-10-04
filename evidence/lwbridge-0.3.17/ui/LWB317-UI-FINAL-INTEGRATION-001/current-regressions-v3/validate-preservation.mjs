import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../../../..');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const records = JSON.parse(fs.readFileSync(path.join(here, 'historical-inputs.json'), 'utf8'));
for (const record of records) assert.equal(hash(fs.readFileSync(path.join(root, record.path))), record.sha256, record.path);
for (const name of ['map-entry-focused-results.json', 'crossserver-focused-results.json', 'crossserver-regression-results.json', 'map-ownership-results.json', 'affected-results.json']) {
  const result = JSON.parse(fs.readFileSync(path.join(here, name), 'utf8'));
  assert.ok(result, name);
}
const appHash = hash(fs.readFileSync(path.join(root, 'src/LWBridge.UI-0.3.17/src/App.jsx')));
const entry = JSON.parse(fs.readFileSync(path.join(here, 'map-entry-focused-results.json'), 'utf8'));
assert.equal(entry.source.sha256.toLowerCase(), appHash, 'Map-entry result current App identity');
const cross = JSON.parse(fs.readFileSync(path.join(here, 'crossserver-focused-results.json'), 'utf8'));
assert.equal(cross.source.sha256.toLowerCase(), appHash, 'Cross-server result current App identity');
const affected = JSON.parse(fs.readFileSync(path.join(here, 'affected-results.json'), 'utf8'));
assert.equal(affected.currentApp.sha256.toLowerCase(), appHash, 'Aggregate current App identity');
console.log(`LWB317_FINAL_CURRENT_REGRESSION_PRESERVATION_OK historical=${records.length} outputs=5 currentApp=${appHash}`);
