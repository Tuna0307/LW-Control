import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),packet=path.resolve(here,'../..'),repo=path.resolve(here,'../../../../../..'),v3=path.join(packet,'current-regressions-v3');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex'),artifact=(f,b)=>hash(/\.jpg$/.test(f)?b:b.toString('utf8').replaceAll('\r\n','\n'));
const walk=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);
const product=['App.jsx','AutomationPage.jsx','SquadsPage.jsx','MapDataPage.jsx'],manifestPath=path.join(here,'current-manifest.json');
if(process.argv.includes('--record')){
  const files=[...product.map(n=>path.join(repo,'src/LWBridge.UI-0.3.17/src',n)),...walk(here),...walk(v3)].filter(f=>f!==manifestPath&&f!==fileURLToPath(import.meta.url));
  fs.writeFileSync(manifestPath,JSON.stringify({scope:'Bounded parent-tab/Automation Activity correction only',hashPolicy:'LF-normalized UTF-8 text; exact JPEG bytes',reference:{path:'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe',sha256:'4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783'},files:files.sort().map(f=>({path:path.relative(repo,f).replaceAll('\\','/'),sha256:artifact(f,fs.readFileSync(f))}))},null,2)+'\n');
}
const manifest=JSON.parse(fs.readFileSync(manifestPath)),i=process.argv.indexOf('--revision'),revision=i<0?null:process.argv[i+1];assert.equal(hash(fs.readFileSync(manifest.reference.path)),manifest.reference.sha256);
for(const item of manifest.files){const b=revision?execFileSync('git',['show',`${revision}:${item.path}`],{cwd:repo,maxBuffer:16*1024*1024}):fs.readFileSync(path.join(repo,item.path));assert.equal(artifact(item.path,b),item.sha256,item.path);if(item.path.endsWith('.json'))JSON.parse(b.toString('utf8'));}
// Always verify explicit evidence conclusions as well as hashes. Later product
// revisions use --revision; this packet must not silently become current proof.
const read=n=>JSON.parse(fs.readFileSync(path.join(here,n)));
assert.equal(read('current-contract-results.json').marker,'LWB317_PROFILE_TABS_CONTRACT_OK');assert.equal(read('current-contract-results.json').cases,22);
const mounted=read('mounted-current-results.json');assert.equal(mounted.cases[0].distinguishingFailures,3);assert.equal(mounted.cases[1].distinguishingFailures,0);
for(const n of ['browser/english.json','browser/japanese.json','browser/category-return.json'])assert.deepEqual(read(n).console,[]);
const regressions=JSON.parse(fs.readFileSync(path.join(v3,'map-regression-results.json')));assert.equal(regressions.results.length,10);assert.ok(regressions.results.every(r=>r.exitCode===0));
for(const n of ['navigation','goods','scheduled'])assert.equal(JSON.parse(fs.readFileSync(path.join(v3,`shape-replay-${n}.json`))).status,'PASS');
assert.equal(manifest.files.filter(f=>f.path.endsWith('.jpg')).length,3);
console.log(`LWB317_PROFILE_TABS_EVIDENCE_OK files=${manifest.files.length} screenshots=3 contract=22 mounted=5 map=10 revision=${revision||'working'}`);
