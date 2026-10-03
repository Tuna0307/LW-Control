import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {here,repo} from './harness.mjs';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
const file=relative=>({path:relative,sha256LF:hash(fs.readFileSync(path.join(repo,relative),'utf8').replace(/\r\n/g,'\n'))});
const report=JSON.parse(fs.readFileSync(path.join(here,'actual-source-results.json')));
const prefix=path.relative(repo,here).replaceAll('\\','/');
const target='C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe';
const screenshots=fs.readdirSync(path.join(here,'screenshots')).filter(n=>n.endsWith('.png')).map(name=>({path:`${prefix}/screenshots/${name}`,sha256:hash(fs.readFileSync(path.join(here,'screenshots',name))),visuallyInspected:true}));
const files=[
 'src/LWBridge.UI-0.3.17/src/Pages.jsx','src/LWBridge.UI-0.3.17/src/previewAfkContracts.js','src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js','src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js',
 'src/LWBridge.UI-0.3.17/src/AutomationMeta.jsx','src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx','src/LWBridge.UI-0.3.17/src/DispatchAssistManual.jsx','src/LWBridge.UI-0.3.17/scripts/check-ui-complete.mjs',
 ...['check-closeout.mjs','harness.mjs','recover-join-renderer.mjs','recover-assist-renderer.mjs','validate.mjs','record-verification.mjs','record-manifest.mjs','actual-source-results.json','browser-results.json','verification-results.json','takeover-baseline/Pages.jsx','takeover-baseline/previewAfkContracts.js','takeover-baseline/previewAfkCloseoutFixtures.js','takeover-baseline/worker-source-locators.json','takeover-baseline/worker-validate.mjs'].map(p=>`${prefix}/${p}`),
].map(file);
const manifest={targetExecutable:{path:target,sha256:hash(fs.readFileSync(target))},assets:report.assets,locators:report.locators,files,screenshots,method:'Exact source asset bytes and UTF-8 slices; executed original/current comparisons; real local browser controls. Text delivery hashes normalize CRLF to LF. Supersedes incorrect worker EXE hash; worker packet retained under takeover-baseline.'};
fs.writeFileSync(path.join(here,'source-locators.json'),JSON.stringify(manifest,null,2)+'\n');
console.log(`RECORDED assets=${manifest.assets.length} locators=${Object.keys(manifest.locators).length} screenshots=${screenshots.length}`);
