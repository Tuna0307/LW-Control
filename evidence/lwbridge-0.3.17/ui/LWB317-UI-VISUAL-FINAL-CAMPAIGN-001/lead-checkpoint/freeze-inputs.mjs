// Explicit evidence checkpoint. NEVER invoked by the verifier.
import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';import {execFileSync} from 'node:child_process';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..'),campaign=path.dirname(here),files=new Set();
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
function tree(folder,filter=()=>true){for(const d of fs.readdirSync(folder,{withFileTypes:true})){const f=path.join(folder,d.name);if(d.isDirectory())tree(f,filter);else if(filter(f)&&f!==path.join(here,'frozen-inputs.json')&&!['validation-results.json','lead-validation.log'].includes(d.name))files.add(f);}}
tree(campaign);tree(path.join(repo,'src/LWBridge.UI-0.3.17/src'));tree(path.join(repo,'src/LWBridge.UI-0.3.17/scripts'));tree(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets'));
// Conservative superset for research harness imports/dynamic compile adapters.
tree(path.join(repo,'evidence/lwbridge-0.3.17/ui'),f=>/\.(?:mjs|js|jsx|py)$/.test(f));
for(const f of ['src/LWBridge.UI-0.3.17/package.json','src/LWBridge.UI-0.3.17/package-lock.json'])files.add(path.join(repo,f));
for(const f of ['C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe','C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package-lock.json','C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package-lock.json','C:/Program Files/Google/Chrome/Application/chrome.exe'])files.add(f);
const manifest={node:process.version,python:execFileSync('python',['-c','import sys,PIL,numpy;print(sys.version.split()[0]+" / "+PIL.__version__+" / "+numpy.__version__)'],{encoding:'utf8'}).trim(),scope:'All campaign evidence, canonical UI/modules/scripts, original assets, all historical UI code harnesses, lockfiles, target EXE and material external browser tools. Preservation does not imply acceptance.',files:[...files].sort().map(f=>({path:path.relative(repo,f).startsWith('..')?f:path.relative(repo,f).replaceAll('\\','/'),bytes:fs.statSync(f).size,sha256:hash(f)}))};
fs.writeFileSync(path.join(here,'frozen-inputs.json'),JSON.stringify(manifest,null,2)+'\n');console.log('LEAD_CHECKPOINT_INPUTS_FROZEN '+manifest.files.length);
