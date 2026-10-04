// Explicit checkpoint creation only. Verification never updates this manifest.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {execFileSync} from 'node:child_process';
const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,'../../../../../..');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const files=new Set();
function tree(folder){for(const d of fs.readdirSync(folder,{withFileTypes:true})){const file=path.join(folder,d.name);if(d.name==='lead-audit')continue;if(d.isDirectory())tree(file);else files.add(file);}}
for(const folder of ['src/LWBridge.UI-0.3.17/src','evidence/lwbridge-0.3.17/ui/frontend-package/web/assets','evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001','evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001','evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-a','evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b'])tree(path.join(repo,folder));
for(const relative of ['src/LWBridge.UI-0.3.17/package.json','src/LWBridge.UI-0.3.17/package-lock.json','evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TOOLBAR-VISUAL-001/check-pixel-fences.py'])files.add(path.join(repo,relative));
const external=['C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe','C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package-lock.json','C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package-lock.json','C:/Program Files/Google/Chrome/Application/chrome.exe'];
external.forEach(file=>files.add(file));
function ownTree(folder){for(const d of fs.readdirSync(folder,{withFileTypes:true})){const file=path.join(folder,d.name);if(['frozen-inputs.json','validation-results.json'].includes(d.name))continue;if(d.isDirectory())ownTree(file);else files.add(file);}}
ownTree(here);
const manifest={scope:'Conservative superset of actual UI/module imports, all original frontend assets, historical Map oracle/harness trees, submitted A/B scripts/HTML/measurements/PNGs, package locks and material tool executables. No old pass marker is an assertion.',node:process.version,pythonPillow:execFileSync('python',['-c','import sys,PIL;print(sys.version.split()[0]+" / "+PIL.__version__)'],{encoding:'utf8'}).trim(),files:[...files].sort().map(file=>({path:path.relative(repo,file).startsWith('..')?file:path.relative(repo,file).replaceAll('\\','/'),sha256:hash(file),bytes:fs.statSync(file).size}))};
fs.writeFileSync(path.join(here,'frozen-inputs.json'),JSON.stringify(manifest,null,2)+'\n');
console.log(`INPUTS_FROZEN files=${manifest.files.length}`);
