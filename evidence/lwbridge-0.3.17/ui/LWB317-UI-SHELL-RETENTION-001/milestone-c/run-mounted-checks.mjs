import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..');
const results=[];
for(const file of ['lead-mounted-retention.mjs','map-mounted-retention.mjs']) {
 const out=spawnSync(process.execPath,[path.join(here,file)],{cwd:repo,encoding:'utf8',maxBuffer:1024*1024});
 results.push({file,exitCode:out.status,stdout:out.stdout?.trim(),stderr:out.stderr?.trim()});
 console.log(`${out.status===0?'PASS':'FAIL'} ${file}`);
 if(out.status!==0) break;
}
const status=results.length===2&&results.every(r=>r.exitCode===0)?'PASS':'FAIL';
fs.writeFileSync(path.join(here,'lead-mounted-rerun.json'),JSON.stringify({status,results},null,2)+'\n');
if(status!=='PASS')process.exit(1);
