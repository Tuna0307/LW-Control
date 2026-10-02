import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const historical = path.resolve(here, '../LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-results.json');
const before = fs.readFileSync(historical);
const writeFileSync = fs.writeFileSync;
fs.writeFileSync = (file, data, ...args) => path.resolve(file) === historical
  ? writeFileSync(path.join(here, 'r1-replay-results.json'), data, ...args)
  : writeFileSync(file, data, ...args);
try { await import('../LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-cases.mjs'); }
finally { fs.writeFileSync = writeFileSync; }
assert.deepEqual(fs.readFileSync(historical), before);
console.log('LWB317_PICKER_R1_REPLAY_OK historical record preserved');
