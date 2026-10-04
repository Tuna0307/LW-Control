import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),packet=path.dirname(here),repo=path.resolve(packet,'../../../..');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const artifactHash=(file,b)=>hash(/\.(jpg|png)$/.test(file)?b:b.toString('utf8').replaceAll('\r\n','\n'));
const walk=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);
const relative=file=>path.relative(repo,file).replaceAll('\\','/');
const file=path.join(here,'loading-checkpoint-manifest.json');
if(process.argv.includes('--record')) {
  const names=['App.jsx','Pages.jsx','sharedPageUI.jsx','HomePage.jsx','AutomationPage.jsx','SquadsPage.jsx','CityLayoutPage.jsx','HotkeyPages.jsx','SettingsPage.jsx','MapRoutePage.jsx','EquipmentMotion.jsx','vendor/equipmentMotion317.js'];
  const files=[...names.map(n=>path.join(repo,'src/LWBridge.UI-0.3.17/src',n)),...['check-static.mjs','check-home-integration.mjs','check-ui-complete.mjs'].map(n=>path.join(repo,'src/LWBridge.UI-0.3.17/scripts',n)),...['route-loading','motion','current-regressions-v2'].flatMap(n=>walk(path.join(packet,n)))].filter(f=>f!==file&&f!==fileURLToPath(import.meta.url));
  const manifest={scope:'Loading/motion checkpoint; no global/native/pixel parity',hashPolicy:'Exact images; LF-normalized UTF-8 text',referenceExe:{path:'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe',sha256:'4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783'},files:files.sort().map(f=>({path:relative(f),sha256:artifactHash(f,fs.readFileSync(f))}))};
  fs.writeFileSync(file,JSON.stringify(manifest,null,2)+'\n');
}
const manifest=JSON.parse(fs.readFileSync(file,'utf8')),i=process.argv.indexOf('--revision'),revision=i<0?null:process.argv[i+1];
assert.equal(hash(fs.readFileSync(manifest.referenceExe.path)),manifest.referenceExe.sha256);
for(const item of manifest.files){const b=revision?execFileSync('git',['show',`${revision}:${item.path}`],{cwd:repo,maxBuffer:8*1024*1024}):fs.readFileSync(path.join(repo,item.path));assert.equal(artifactHash(item.path,b),item.sha256,item.path);if(item.path.endsWith('.json'))JSON.parse(b.toString('utf8'));}
console.log(`LWB317_LOADING_MOTION_CHECKPOINT_OK files=${manifest.files.length} screenshots=${manifest.files.filter(f=>/\.(jpg|png)$/.test(f.path)).length} revision=${revision||'working'}`);
