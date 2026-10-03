import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,'../../../../..');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const results=[];
for(const name of ['app-harness.mjs','regression-refresh-ownership.json','regression-results.json']) {
  const relative=`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/${name}`;
  const file=path.join(repo,relative), snapshot=path.join(here,'continuation-snapshots',name);
  const original=execFileSync('git',['show',`081ffc82:${relative}`],{cwd:repo});
  const current=fs.readFileSync(file), preserved=fs.readFileSync(snapshot);
  assert.ok(current.equals(preserved)||current.equals(original),`Unsaved additional edit: ${name}`);
  if(process.argv.includes('--restore')) fs.writeFileSync(file,original);
  results.push({path:relative,preservedSha256:hash(preserved),historicalSha256:hash(original),restored:fs.readFileSync(file).equals(original)});
}
if(process.argv.includes('--restore')) fs.writeFileSync(path.join(here,'continuation-preservation.json'),JSON.stringify({status:'PASS',reason:'Worker current-shell adapter/replays relocated to this packet; historical Auto evidence restored byte-for-byte. Unrelated seven-path WIP is untouched.',results},null,2)+'\n');
console.log(JSON.stringify(results,null,2));
