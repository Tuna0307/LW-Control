import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)), repo=path.resolve(here,'../../../..');
const base='evidence/lwbridge-0.3.17/ui/';
const scripts=[
 'LWB317-UI-MAP-GOODS-PICKER-001/check-picker.mjs',
 'LWB317-UI-MAP-TREASURE-PICKER-001/check-picker.mjs',
 'LWB317-UI-MAP-GOODS-PICKER-001/replay-parent.mjs',
 'LWB317-UI-MAP-GOODS-PICKER-001/replay-r1.mjs',
 'LWB317-UI-MAP-INTERACTIONS-001/check-request-lifetime.mjs',
 'LWB317-UI-MAP-INTERACTIONS-001/check-navigation-replay.mjs',
 'LWB317-UI-MAP-INTERACTIONS-001/check-interactions.mjs',
 'LWB317-UI-MAP-INTERACTIONS-001/check-integration.mjs',
 'LWB317-UI-MAP-GOODS-PICKER-001/replay-historical.mjs',
];
const results=[];
for(const script of scripts) {
  const run=spawnSync(process.execPath,[base+script],{cwd:repo,encoding:'utf8',timeout:120000});
  results.push({script,exitCode:run.status,output:(run.stdout+run.stderr).trim()});
  console.log(`${run.status===0?'PASS':'FAIL'} ${script} :: ${results.at(-1).output.split(/\r?\n/).at(-1)}`);
}
fs.writeFileSync(path.join(here,'regression-results.json'),JSON.stringify({status:results.every(r=>r.exitCode===0)?'PASS':'FAIL',results},null,2)+'\n');
assert.ok(results.every(r=>r.exitCode===0));
