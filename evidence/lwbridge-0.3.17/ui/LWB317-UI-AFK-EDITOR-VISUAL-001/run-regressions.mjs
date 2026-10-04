import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {here,repo} from './harness.mjs';
const campaign='evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/';
const checks=[
 ['afk-source',process.execPath,[path.join(here,'replay-afk-current.mjs')]],
 ['equipment',process.execPath,[campaign+'unit-d/replay-equipment-closeout.mjs']],
 ['equipment-r1',process.execPath,[campaign+'unit-d/replay-equipment-r1.mjs']],
 ['archive',process.execPath,['evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs']],
 ['check','cmd.exe',['/d','/c','npm.cmd --prefix src/LWBridge.UI-0.3.17 run check']],
 ['build','cmd.exe',['/d','/c','npm.cmd --prefix src/LWBridge.UI-0.3.17 run build']],
 ['package','cmd.exe',['/d','/c','npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build']],
 ['diff','git',['diff','--check']],
];
const results=[];
for(const [id,command,args] of checks){const r=spawnSync(command,args,{cwd:repo,encoding:'utf8',windowsHide:true});const row={id,command,args,status:r.status,stdout:r.stdout,stderr:r.stderr};results.push(row);if(r.status!==0)throw Error(id+' failed '+r.stdout+' '+r.stderr);}
fs.writeFileSync(path.join(here,'regression-results.json'),JSON.stringify({results},null,2)+'\n');console.log('LWB317_AFK_EDITOR_REGRESSIONS_OK '+results.length);
