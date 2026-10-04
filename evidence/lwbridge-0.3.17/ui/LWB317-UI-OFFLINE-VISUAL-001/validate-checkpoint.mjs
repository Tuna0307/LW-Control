import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../..');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const canonical=(f,b)=>/\.jpg$/.test(f)?b:b.toString('utf8').replaceAll('\r\n','\n');
const walk=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.name==='generated'?[]:e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
const manifestPath=path.join(here,'manifest.json');
const currentFiles=['HomePage.jsx','sharedPageUI.jsx','MapDataPage.jsx','GameAssetImage.jsx','mapTablePresentation.js','mapInteractions.js','reference.css','locales/en.js','locales/ja.js'].map(n=>path.join(repo,'src/LWBridge.UI-0.3.17/src',n));
if(process.argv.includes('--record')){const files=[...walk(here),...currentFiles].filter(f=>f!==manifestPath&&f!==fileURLToPath(import.meta.url));fs.writeFileSync(manifestPath,JSON.stringify({scope:'Named offline Home/Map TABLE visual checkpoint only; broader campaign PARTIAL',hashPolicy:'LF-normalized text, exact JPEG bytes',files:files.sort().map(f=>({path:path.relative(repo,f).replaceAll('\\','/'),sha256:hash(canonical(f,fs.readFileSync(f)))}))},null,2)+'\n');}
const manifest=JSON.parse(fs.readFileSync(manifestPath)),i=process.argv.indexOf('--revision'),revision=i<0?null:process.argv[i+1];
for(const item of manifest.files){const b=revision?execFileSync('git',['show',`${revision}:${item.path}`],{cwd:repo,maxBuffer:16*1024*1024}):fs.readFileSync(path.join(repo,item.path));assert.equal(hash(canonical(item.path,b)),item.sha256,item.path);if(item.path.endsWith('.json'))JSON.parse(b);}
assert.equal(hash(fs.readFileSync('C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe')),'4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783');
const read=n=>JSON.parse(fs.readFileSync(path.join(here,n)));
const home=read('home/render-results.json'),map=read('map/render-results.json'),counter=read('home/preference-counter-evidence.json');
assert.equal(home.cases,42);assert.equal(home.matched,30);assert.equal(map.cases,48);assert.equal(map.matched,48);assert.ok(map.records.every(r=>r.differences.length===0));
for(const r of home.records)for(const d of r.differences){const removeDisabled=x=>({...x,attrs:x.attrs.filter(([k])=>k!=='disabled')});assert.deepEqual(removeDisabled(d.expected),removeDisabled(d.actual));assert.ok(d.actual.attrs.some(([k])=>k==='disabled'));assert.ok(!d.expected.attrs.some(([k])=>k==='disabled'));}
for(const [record,slices] of [[home.reference,home.reference.slices],[null,map.slices],[counter,counter.slices]])for(const slice of slices){const f=record?.path||record?.sourcePath||`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${slice.asset}`;const b=fs.readFileSync(path.join(repo,f)).subarray(slice.utf8ByteOffset,slice.utf8ByteOffset+slice.byteLength);assert.equal(hash(b),slice.sha256);assert.equal(b.toString('utf8'),slice.text);}
for(const asset of [home.reference,...map.reference])assert.equal(hash(fs.readFileSync(path.join(repo,asset.path))),asset.sha256);
for(const scope of ['home','map']){const measures=read(`${scope}/browser/measurements.json`),comparisons=read(`${scope}/browser/comparison-results.json`);assert.equal(measures.length,4);assert.equal(comparisons.pairs.length,4);for(let i=0;i<4;i++){const [e,a]=measures[i].pair;assert.deepEqual(e.elements,a.elements);assert.deepEqual(e.viewport,a.viewport);assert.deepEqual(e.document,a.document);const hashes=measures[i].pair.map(p=>hash(fs.readFileSync(path.join(here,scope,'browser',p.screenshot))));assert.equal(hashes[0],hashes[1]);assert.deepEqual(hashes,comparisons.pairs[i].jpegHashes);assert.equal(comparisons.pairs[i].jpegBytesMatch,true);}}
assert.deepEqual(read('map/browser/console.json'),[]);assert.equal(manifest.files.filter(f=>f.path.endsWith('.jpg')).length,16);
console.log(`LWB317_OFFLINE_VISUAL_CHECKPOINT_OK files=${manifest.files.length} Home=30/42 Map=48/48 identicalBrowserPairs=8 revision=${revision||'working'}`);
