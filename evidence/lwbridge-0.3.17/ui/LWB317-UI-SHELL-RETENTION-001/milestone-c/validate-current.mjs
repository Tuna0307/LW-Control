import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),packet=path.dirname(here),repo=path.resolve(here,'../../../../..');
const relative=file=>path.relative(repo,file).replaceAll('\\','/');
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const read=file=>fs.readFileSync(path.join(repo,file));
const json=file=>JSON.parse(read(file));
const source=JSON.parse(fs.readFileSync(path.join(packet,'milestone-a/source-manifest.json')));
assert.equal(hash(fs.readFileSync(source.referenceExecutable)),source.referenceExecutableSha256);
const original=read(source.originalShellAsset);
assert.equal(hash(original),source.originalShellAssetSha256);
for(const locator of source.locators) assert.equal(hash(original.subarray(locator.utf8ByteOffset,locator.utf8ByteOffset+locator.utf8ByteLength)),locator.sha256,locator.name);
assert.equal(hash(execFileSync('git',['show',`${source.baseline.commit}:${source.baseline.appPath}`],{cwd:repo})),source.baseline.appSha256FromGitBlobBytes);
const historicalRoot='evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001-R1';
const historicalFiles=execFileSync('git',['ls-tree','-r','--name-only','35b5cab','--',historicalRoot],{cwd:repo,encoding:'utf8'}).trim().split('\n');
const normalized=bytes=>bytes.toString('utf8').replaceAll('\r\n','\n');
for(const file of historicalFiles) {
 const prior=execFileSync('git',['show',`35b5cab:${file}`],{cwd:repo});
 const current=read(file);
 if(/\.(mjs|js|jsx|json|md)$/.test(file)) assert.equal(normalized(current),normalized(prior),file);
 else assert.ok(current.equals(prior),file);
}
for(const name of ['app-harness.mjs','regression-refresh-ownership.json','regression-results.json']) {
 const file=`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/${name}`;
 assert.equal(normalized(read(file)),normalized(execFileSync('git',['show',`081ffc82:${file}`],{cwd:repo})),`Historical Auto ${name}`);
}
const scoped=name=>JSON.parse(fs.readFileSync(path.join(here,name)));
const mapEffects=scoped('lead-map-effect-results.json');
assert.equal(mapEffects.result,'LWB317_SHELL_LEAD_MAP_EFFECT_PARITY_OK');
const mapOriginal=read(mapEffects.sources[0].path);
assert.equal(hash(mapOriginal),mapEffects.sources[0].sha256);
for(const callback of Object.values(mapEffects.callbacks.original))assert.equal(hash(mapOriginal.subarray(callback.utf8ByteOffset,callback.utf8ByteOffset+callback.utf8ByteLength)),callback.sha256);
for(const name of ['current-checks.json','lead-mounted-rerun.json','map-replay-results.json','browser-results.json','native-dialog-browser-results.json','continuation-preservation.json'])assert.equal(scoped(name).status,'PASS',name);
assert.equal(scoped('lead-mounted-retention-results.json').result,'LWB317_SHELL_LEAD_MOUNTED_RETENTION_OK');
const map=scoped('map-mounted-retention-results.json');
assert.equal(map.result,'LWB317_SHELL_MAP_MOUNTED_OK'); assert.equal(map.cases.length,6); assert.deepEqual(map.consoleErrors,[]);
assert.deepEqual(scoped('browser-results.json').console,[]);
const dialog=scoped('native-dialog-browser-results.json');assert.deepEqual(dialog.console,[]);
assert.equal(dialog.beforeHide.modal,true);assert.equal(dialog.hidden.modal,false);assert.equal(dialog.returned.modal,true);
const smoke=scoped('browser-results.json').cases.find(c=>c.id==='eight-navigation-smoke').observed;
assert.equal(smoke.length,8); for(const row of smoke)assert.equal(row.visible.length,1,row.name);
const walk=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(entry=>entry.isDirectory()?walk(path.join(dir,entry.name)):[path.join(dir,entry.name)]);
const manifestPath=path.join(here,'integrity-manifest.json');
const images=walk(path.join(here,'screenshots'));
assert.equal(images.length,7);
for(const file of images) {const bytes=fs.readFileSync(file);assert.ok(bytes.length>1000,file);assert.equal(bytes.readUInt16BE(0),0xffd8,'JPEG screenshot');}
function jpegSize(bytes) {
 let offset=2;
 while(offset<bytes.length) {
  assert.equal(bytes[offset++],0xff);
  while(bytes[offset]===0xff)offset++;
  const marker=bytes[offset++],length=bytes.readUInt16BE(offset);
  if([0xc0,0xc1,0xc2].includes(marker))return {height:bytes.readUInt16BE(offset+3),width:bytes.readUInt16BE(offset+5)};
  assert.ok(length>=2);offset+=length;
 }
 throw Error('JPEG dimensions unavailable');
}
const narrow=fs.readFileSync(path.join(here,'screenshots/settings-return-ja-dark-narrow.jpg'));
// The connector exports a scaled JPEG; actual CSS viewport was measured in DOM.
assert.deepEqual(jpegSize(narrow),{width:790,height:593});
assert.deepEqual(scoped('browser-results.json').cases.find(c=>c.id==='narrow-japanese-return').viewport,{width:800,height:600});
const additional=['src/LWBridge.UI-0.3.17/src/App.jsx','src/LWBridge.UI-0.3.17/src/Pages.jsx','src/LWBridge.UI-0.3.17/src/MapDataPage.jsx','src/LWBridge.UI-0.3.17/src/routes.js','src/LWBridge.UI-0.3.17/src/backendBridge.js'];
const files=[...new Set([...walk(packet).filter(f=>f!==manifestPath).map(relative),...additional,source.originalShellAsset,...map.sources.map(entry=>entry.path),...mapEffects.sources.map(entry=>entry.path)])].sort();
for(const file of files.filter(f=>f.endsWith('.json')))json(file);
const entries=files.map(file=>({path:file,sha256:hash(read(file)),bytes:read(file).length}));
if(process.argv.includes('--record'))fs.writeFileSync(manifestPath,JSON.stringify({scope:'Current shell identity and executed evidence; historical four-page R1 preserved, not re-pinned to changed App.',files:entries},null,2)+'\n');
const recorded=JSON.parse(fs.readFileSync(manifestPath));
assert.deepEqual(recorded.files,entries);
const guard=execFileSync(process.execPath,['evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs'],{cwd:repo,encoding:'utf8'});
assert.match(guard,/PROTECTED_WIP_OK count=7/);
console.log(`LWB317_SHELL_RETENTION_INTEGRITY_OK slices=${source.locators.length} files=${entries.length} images=${images.length} historicalR1=${historicalFiles.length} protected=7`);
