import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import assert from 'node:assert/strict';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..'),campaign=path.dirname(here);
const manifest=JSON.parse(fs.readFileSync(path.join(here,'frozen-inputs.json'),'utf8'));
assert.equal(process.version,manifest.node);
assert.equal(execFileSync('python',['-c','import sys,PIL,numpy;print(sys.version.split()[0]+" / "+PIL.__version__+" / "+numpy.__version__)'],{encoding:'utf8'}).trim(),manifest.python);
for(const e of manifest.files){const f=path.isAbsolute(e.path)?e.path:path.join(repo,e.path);assert.equal(fs.statSync(f).size,e.bytes,e.path+' length');assert.equal(crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex'),e.sha256,e.path+' bytes');}
function node(relative,args=[]){return execFileSync(process.execPath,[path.join(campaign,relative),...args],{encoding:'utf8',maxBuffer:64*1024*1024});}
// These modes execute actual source renderers and compare their fresh output to
// frozen output; they do not rewrite the evidence to manufacture a pass.
node('unit-c/lead-visual/render-pairs.mjs',['--verify']);
node('unit-e/recover-pages.mjs',['--verify']);
const pixels=JSON.parse(execFileSync('python',[path.join(here,'check-decoded-pixels.py')],{encoding:'utf8',maxBuffer:64*1024*1024}));
const {createRequire}=await import('node:module');const require=createRequire('C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json');const {JSDOM}=require('jsdom');
function normalized(html){const d=new JSDOM(html).window.document;for(const e of d.querySelectorAll('*')){for(const a of [...e.attributes])if(a.name.startsWith('data-preview')||['data-hotkey-category','data-city-building-id'].includes(a.name))e.removeAttribute(a.name);if(e.tagName==='BUTTON'&&e.getAttribute('type')==='button')e.removeAttribute('type');}const sub=d.querySelector('.settings-panel .panel-title > span');if(sub?.className==='muted')sub.removeAttribute('class');return d.body.innerHTML;}
let states=0;for(const unit of ['e','f','g','h']){const dir=path.join(campaign,'unit-'+unit,'current'),inventory=JSON.parse(fs.readFileSync(path.join(dir,'inventory.json'),'utf8'));for(const row of inventory.statesRendered){const a=fs.readFileSync(path.join(dir,'raw',row.id+'-original.html'),'utf8'),b=fs.readFileSync(path.join(dir,'raw',row.id+'-current.html'),'utf8');assert.equal(normalized(a),normalized(b),unit+'/'+row.id);states++;}}
assert.equal(states,189);assert.equal(pixels.decodedPairs,112);
console.log(JSON.stringify({result:'LEAD_TAKEOVER_CHECKPOINT_OK',frozenFiles:manifest.files.length,freshAutomationRendererPairs:28,freshRemainingPageRendererStates:189,decodedZeroTolerancePairs:112,compactBaselineFailuresDetected:16,limits:'Finite sampled/source-local gates only. Broader Automation positive/conditional forms, full AFK/Garrison/Profile, shell/Home whole compositions and final acceptance remain open.'}));
