import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';import {execFileSync} from 'node:child_process';import {root,repo,hash} from './page-harness.mjs';
const out=path.join(root,'accepted-replays');fs.mkdirSync(out,{recursive:true});const sources=[
'LWB317-UI-REMAINING-PAGES-001/milestone-b/check-city-layout.mjs',
'LWB317-UI-REMAINING-PAGES-001/milestone-c/check-hotkeys.mjs',
'LWB317-UI-REMAINING-PAGES-001/milestone-d/check-mini-games.mjs',
'LWB317-UI-REMAINING-PAGES-001/milestone-e/check-settings.mjs',
'LWB317-UI-REMAINING-PAGES-001-R1/milestone-a/check-boundaries.mjs',
'LWB317-UI-REMAINING-PAGES-001-R1/milestone-b/check-city-r1.mjs',
'LWB317-UI-REMAINING-PAGES-001-R1/milestone-c/check-feedback-lifetime.mjs',
'LWB317-UI-REMAINING-PAGES-001-R1/milestone-d/lead-city-review.mjs'];
const records=[];
for(const original of sources){
 const sourceFile=path.join(repo,'evidence/lwbridge-0.3.17/ui',original);
 let source=fs.readFileSync(sourceFile,'utf8');const originalHash=hash(source);
 source=source.replace(/from\s+(["'])(\.[^"']+)\1/g,(m,q,rel)=>'from '+JSON.stringify(new URL(rel,pathToFileURL(sourceFile)).href));
 source=source.replaceAll('read("src/LWBridge.UI-0.3.17/src/Pages.jsx")',`["CityLayoutPage.jsx","HotkeyPages.jsx","SettingsPage.jsx","Pages.jsx"].map(file=>read("src/LWBridge.UI-0.3.17/src/"+file).replace(/^import .*?;\\r?\\n/gm,"" )).join("\\n")`);
 source=source.replace('  HomePage: Stub,','  HomePage: Stub, LazyHotkeyPanel: Stub, LazySettingsPage: Stub,');
 source=`globalThis.GameAssetImage = ()=>null; // Actual image nodes separately source/pixel-verified; historical predicates do not inspect asset rendering.\n`+source;
 const file=path.join(out,path.basename(original));fs.writeFileSync(file,source);let stdout;
 try{stdout=execFileSync(process.execPath,[file,'--record'],{cwd:repo,encoding:'utf8',maxBuffer:10*1024*1024});records.push({original,originalSha256:originalHash,adapterSha256:hash(source),result:'PASS',output:stdout});}
 catch(e){records.push({original,originalSha256:originalHash,adapterSha256:hash(source),result:'FAIL',output:e.stdout?.toString()??'',error:e.stderr?.toString()??e.message});}
 console.log(original,records.at(-1).result);
}
fs.writeFileSync(path.join(out,'replay-results.json'),JSON.stringify({records,substitutions:'Read canonical split modules instead of historical inline Pages.jsx; imports resolved to historical original locations; outputs confined to task adapter folder; asset node stub only for historical behavioral predicates'},null,2)+'\n');if(records.some(r=>r.result==='FAIL'))process.exitCode=1;
