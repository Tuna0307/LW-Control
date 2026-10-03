import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {here,repo} from './harness.mjs';
const results=[];
function run(name,exe,args,cwd=repo){
 try {const output=execFileSync(exe,args,{cwd,encoding:'utf8',stdio:['ignore','pipe','pipe'],maxBuffer:16*1024*1024});results.push({name,result:'PASS',output});console.log(`${name}: PASS`);}
 catch(error){results.push({name,result:'FAIL',output:String(error.stdout||'')+'\n'+String(error.stderr||error.message)});fs.writeFileSync(path.join(here,'verification-results.json'),JSON.stringify({results},null,2)+'\n');throw error;}
}
const ui=path.join(repo,'src/LWBridge.UI-0.3.17');
for(const script of ['check','build','check:production-build'])run(script,'cmd.exe',['/d','/s','/c',`npm.cmd run ${script}`],ui);
for(const [name,file] of [
 ['actual source comparison','check-closeout.mjs'],['exact Join renderer','recover-join-renderer.mjs'],['exact Assist renderer','recover-assist-renderer.mjs'],
])run(name,process.execPath,[path.join(here,file)]);
for(const [name,file] of [
 ['accepted weekly','LWB317-UI-CORRECT-003A/check-weekly-state.mjs'],
 ['accepted Trade selection','LWB317-UI-CORRECT-003B/check-trade-selection.mjs'],
 ['accepted Trade presentation','LWB317-UI-CORRECT-003E/check-trade-presentation.mjs'],
 ['protected WIP','LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs'],
])run(name,process.execPath,[path.join(here,'..',file)]);
run('diff check','git',['diff','--check']);
fs.writeFileSync(path.join(here,'verification-results.json'),JSON.stringify({results,limits:'Inert source/local and offline browser only; no Last War or native operations.'},null,2)+'\n');
