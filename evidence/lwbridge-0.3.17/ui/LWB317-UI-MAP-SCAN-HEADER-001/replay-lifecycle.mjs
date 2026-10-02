import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const prior=path.resolve(here,'../LWB317-UI-MAP-GOODS-PICKER-001');
const outputs=['parent-replay-results.json','r1-replay-results.json'];
const saved=Object.fromEntries(outputs.map(p=>[p,fs.readFileSync(path.join(prior,p))]));
const write=fs.writeFileSync;
fs.writeFileSync=(file,data,...args)=>outputs.some(p=>path.resolve(file)===path.join(prior,p))?write(path.join(here,path.basename(file)),data,...args):write(file,data,...args);
try {await import('../LWB317-UI-MAP-GOODS-PICKER-001/replay-parent.mjs');await import('../LWB317-UI-MAP-GOODS-PICKER-001/replay-r1.mjs');}
finally {fs.writeFileSync=write;}
for(const p of outputs)assert.deepEqual(fs.readFileSync(path.join(prior,p)),saved[p]);
console.log('LWB317_SCAN_HEADER_LIFECYCLE_REPLAY_OK all historical outputs preserved');
