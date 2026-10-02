// Replay the preserved table campaign read-only. Its old scalar setter harness
// needs the new per-kind setter dependency; assertions/source stay unchanged.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../..');
assert.equal(process.argv.length,2,'Read-only replay takes no record/verify flags');
const originalPath=path.join(repo,'evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/check-map-tables.mjs');
let script=fs.readFileSync(originalPath,'utf8');
const before="const actual = new Function('tab','setItemKey','setSortsByKind','setPage',pages.slice(currentFilter.start,currentFilter.end)+';return changeItemFilter;')";
const after="const actual = new Function('tab','setItemKey','setSortsByKind','setPage','const activeSorts='+JSON.stringify(actualSorts[kind])+';function setItemKeyByKind(update){setItemKey(update({[tab]:\"old\"})[tab]);}'+pages.slice(currentFilter.start,currentFilter.end)+';return changeItemFilter;')";
assert.ok(script.includes(before));
script=script.replace(before,after).replace('fileURLToPath(import.meta.url)',JSON.stringify(originalPath));
for(const relative of ['mapTablePresentation.js','mapPreviewApi.js']) script=script.replace(`../../../../src/LWBridge.UI-0.3.17/src/${relative}`,pathToFileURL(path.join(repo,'src/LWBridge.UI-0.3.17/src',relative)).href);
script=script.split('\n').filter(line=>!line.startsWith("if(process.argv.includes('--record'))")&&!line.startsWith("if(process.argv.includes('--verify-record'))")).join('\n');
try { await import('data:text/javascript;base64,'+Buffer.from(script).toString('base64')); }
catch(error) { console.error(error.message); process.exitCode=1; }
assert.equal(process.exitCode,undefined);
console.log('LWB317_MAP_TABLE_REGRESSION_OK historical evidence unchanged; only test setter dependency adapted');
