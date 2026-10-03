import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)), repo=path.resolve(here,'../../../../..');
const ui=path.join(repo,'src/LWBridge.UI-0.3.17');
const prefix='evidence/lwbridge-0.3.17/ui/';
const scripts=[
 'LWB317-UI-SHELL-RETENTION-001/milestone-a/check-baseline.mjs',
 'LWB317-UI-SHELL-RETENTION-001/milestone-b/check-retention.mjs',
 'LWB317-UI-REMAINING-PAGES-001-R1/milestone-a/check-boundaries.mjs',
 'LWB317-UI-REMAINING-PAGES-001-R1/milestone-b/check-city-r1.mjs',
 'LWB317-UI-REMAINING-PAGES-001-R1/milestone-c/check-feedback-lifetime.mjs',
 'LWB317-UI-REMAINING-PAGES-001-R1/milestone-d/lead-city-review.mjs',
 'LWB317-UI-EQUIPMENT-CLOSEOUT-001/check-closeout.mjs',
 'LWB317-UI-EQUIPMENT-CLOSEOUT-001-R1/check-r1.mjs',
 'LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs',
 'LWB317-UI-SHELL-RETENTION-001/milestone-c/run-map-replays.mjs',
 'LWB317-UI-SHELL-RETENTION-001/milestone-c/lead-map-effect.mjs',
];
const checks=scripts.map(s=>({command:process.execPath,args:[prefix+s],cwd:repo}));
for(const script of ['check','build','check:production-build']) checks.push({command:'cmd.exe',args:['/d','/c',`npm.cmd run ${script}`],cwd:ui});
checks.push({command:'git',args:['diff','--check'],cwd:repo});
const results=[];
for(const check of checks) {
 const out=spawnSync(check.command,check.args,{cwd:check.cwd,encoding:'utf8',maxBuffer:10*1024*1024});
 const result={command:[path.basename(check.command),...check.args].join(' '),exitCode:out.status,stdout:out.stdout?.trim(),stderr:out.stderr?.trim()};
 results.push(result); console.log(`${out.status===0?'PASS':'FAIL'} ${result.command}`);
 if(out.status!==0) {fs.writeFileSync(path.join(here,'current-checks.json'),JSON.stringify({status:'FAIL',results},null,2)+'\n'); process.exit(1);}
}
fs.writeFileSync(path.join(here,'current-checks.json'),JSON.stringify({status:'PASS',scope:'Focused changed-shell and affected accepted page/Map checks; historical record flags omitted. Unchanged costly Scheduled Plunder proof reused.',results},null,2)+'\n');
