import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const here = path.dirname(fileURLToPath(import.meta.url));
const parent = path.resolve(here, '../LWB317-UI-MAP-FILTER-LIFECYCLE-001');
const checker = path.join(parent, 'check-filter-lifecycle.mjs');
const historical = path.join(parent, 'filter-lifecycle-results.json');
const before = fs.readFileSync(historical);
let code = fs.readFileSync(checker, 'utf8');
code = code.replace('from "./harness.mjs"', 'from ' + JSON.stringify(pathToFileURL(path.join(here, 'picker-harness.mjs')).href));
code = code.replace('from "../LWB317-UI-MAP-INTERACTIONS-001/driver.mjs"', 'from ' + JSON.stringify(pathToFileURL(path.resolve(parent, '../LWB317-UI-MAP-INTERACTIONS-001/driver.mjs')).href));
code = code.replace('const here = path.dirname(fileURLToPath(import.meta.url));', 'const here = ' + JSON.stringify(parent) + ';');
const writeFileSync = fs.writeFileSync;
fs.writeFileSync = (file, data, ...args) => path.resolve(file) === historical
  ? writeFileSync(path.join(here, 'parent-replay-results.json'), data, ...args)
  : writeFileSync(file, data, ...args);
try { await import('data:text/javascript;base64,' + Buffer.from(code).toString('base64')); }
finally { fs.writeFileSync = writeFileSync; }
assert.deepEqual(fs.readFileSync(historical), before);
console.log('LWB317_PICKER_PARENT_REPLAY_OK actual menu parent callbacks; original assertions unchanged');
