import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../..');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const record=relative=>{const bytes=fs.readFileSync(path.join(repo,relative));return {path:relative,sha256:hash(bytes),bytes:bytes.length};};
const prefix=path.relative(repo,here).replaceAll('\\','/')+'/';
const baseline=fs.readFileSync(path.join(here,'baseline.MapDataPage.jsx'),'utf8');
const committed=execFileSync('git',['show','c7c3a3271c6b3b433a2295e79b56feb074b24f67:src/LWBridge.UI-0.3.17/src/MapDataPage.jsx'],{cwd:repo,encoding:'utf8'});
assert.equal(baseline.replaceAll('\r\n','\n'),committed.replaceAll('\r\n','\n'));
const protectedWip=JSON.parse(fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/protected-wip.json')));
for(const expected of protectedWip)assert.deepEqual(record(expected.path),expected);
const images=['train-filters.png','treasure-checking-en.png','treasure-checking-ja.png'].map(name=>{
  const bytes=fs.readFileSync(path.join(here,name));return {...record(prefix+name),width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20),visuallyInspected:true};
});
const files=[
  'src/LWBridge.UI-0.3.17/src/MapDataPage.jsx','src/LWBridge.UI-0.3.17/src/mapPreviewApi.js',
  ...['source-contract.json','filter-results.json','treasure-results.json','browser-results.json','baseline.MapDataPage.jsx','recover-source.mjs','check-filters.mjs','check-treasure-checking.mjs','check-table-regression.mjs'].map(name=>prefix+name),
  'evidence/lwbridge-0.3.17/ui/LWB317-UI-LEAD-TABLES-001/check-map-tables.mjs',
].map(record);
const build=JSON.parse(fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/dist/lwbridge-ui-build.json')));
const manifest={workItem:'LWB317-UI-MAP-FILTERS-001',status:'AWAITING_REVIEW',baseline:'c7c3a3271c6b3b433a2295e79b56feb074b24f67',target:{path:'C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe',sha256:'4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783'},tools:{node:process.version,babelParser:require('@babel/parser/package.json').version,esbuild:require('esbuild/package.json').version,vite:require('vite/package.json').version},files,images,protectedWip,build};
fs.writeFileSync(path.join(here,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
console.log('LWB317_MAP_FILTER_MANIFEST_WRITTEN');
