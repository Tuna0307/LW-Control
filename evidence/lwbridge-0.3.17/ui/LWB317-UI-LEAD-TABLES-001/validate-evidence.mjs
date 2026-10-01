import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const here=path.dirname(fileURLToPath(import.meta.url)), repo=path.resolve(here,'../../../..');
const read=name=>JSON.parse(fs.readFileSync(path.join(here,name),'utf8'));
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const normalized=file=>fs.readFileSync(path.join(repo,file),'utf8').replace(/\r\n/g,'\n');
const source=read('map-table-results.json'), browser=read('browser-results.json');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=require('@babel/parser');
function walk(node,out=[]){if(!node||typeof node!=='object')return out;if(node.type)out.push(node);for(const value of Object.values(node)){if(Array.isArray(value))value.forEach(n=>walk(n,out));else if(value&&typeof value==='object')walk(value,out);}return out;}
const referenceExe='C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe';
const exeHash='4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783';
assert.equal(hash(fs.readFileSync(referenceExe)),exeHash);
function anchor(bytes,entry){assert.equal(bytes.subarray(entry.utf8ByteOffset,entry.utf8ByteOffset+Buffer.byteLength(entry.expression)).toString('utf8'),entry.expression);}
for(const entry of [source.source,source.truckSource,source.rewardSource]){
  const bytes=fs.readFileSync(path.join(repo,entry.path));assert.equal(hash(bytes),entry.sha256);
  for(const loc of Object.values(entry.locators||{}))anchor(bytes,loc);
  if(entry.locator)anchor(bytes,entry.locator);
  for(const loc of Object.values(entry.tableFunctions||{}))anchor(bytes,loc);
  if(entry.itemFilter)anchor(bytes,entry.itemFilter);
  if(entry.tableWidths)anchor(bytes,entry.tableWidths);
}
const reference=fs.readFileSync(path.join(repo,source.source.path),'utf8');
const clock=walk(parse(reference,{sourceType:'module'})).find(n=>n.type==='ArrowFunctionExpression'&&n.end-n.start<500&&reference.slice(n.start,n.end).includes('window.setInterval'));
assert.ok(clock);
const clockAnchor={path:source.source.path,utf8ByteOffset:Buffer.byteLength(reference.slice(0,clock.start)),expression:reference.slice(clock.start,clock.end)};
const index=fs.readFileSync(path.join(repo,source.truckSource.path),'utf8');
const arrowPaths=['M8 13.5v-11M3.5 7 8 2.5 12.5 7','M8 2.5v11M3.5 9 8 13.5 12.5 9'].map(expression=>{
  assert.ok(index.includes(expression));return {path:source.truckSource.path,utf8ByteOffset:Buffer.byteLength(index.slice(0,index.indexOf(expression))),expression};
});
assert.deepEqual(source.counts,{columnCases:288,valueComparisons:15264,selectCases:243,taskCases:81,treasureCases:3150,filterCases:8,renderCases:72,clockCleanupCases:27,compactCases:11});
assert.equal(browser.mode,'browser-preview-only');assert.deepEqual(browser.consoleErrors,[]);
assert.equal(new Set(browser.cases.filter(c=>c.surface==='Map').map(c=>c.tab)).size,8);
assert.ok(browser.cases.filter(c=>c.surface==='Map').every(c=>c.rowActionButtonsDisabled));
const find=name=>{const value=browser.cases.find(c=>c.case===name);assert.ok(value,name);return value.result;};
assert.equal(find('map-resource-unknown').unknown,true);
assert.ok(find('map-item-sort-selected-final').some(c=>c.label==='Items: descending, priority 1'));
assert.ok(find('map-item-sort-cleared-final').headers.some(c=>c.label==='Updated At: descending, priority 1'));
assert.equal(find('map-treasure-ja-dark').theme,'dark');assert.ok(find('map-treasure-ja-dark').firstRows[0].includes('充電中 38%'));
assert.ok(find('trade-error-retained-dom-final').error.includes('Error: Fixture Trade goods request failed'));
assert.ok(find('trade-error-purchased-visible').history[0].text.includes('Purchased today: 7'));
const productionFiles=['src/LWBridge.UI-0.3.17/src/mapTablePresentation.js','src/LWBridge.UI-0.3.17/src/MapDataPage.jsx','src/LWBridge.UI-0.3.17/src/mapPreviewApi.js','src/LWBridge.UI-0.3.17/scripts/check-ui-complete.mjs'];
const screenshots=fs.readdirSync(here).filter(n=>n.endsWith('.jpg')).sort().map(file=>{const bytes=fs.readFileSync(path.join(here,file));assert.ok(bytes.length>10000);assert.equal(bytes.subarray(0,3).toString('hex'),'ffd8ff');return {file,sha256:hash(bytes),bytes:bytes.length};});
assert.equal(screenshots.length,3);
assert.equal(new Set(screenshots.map(s=>s.sha256)).size,3);
const importerPath='src/LWBridge.Desktop/FirstLiveResultImporter.cs';
const importer=fs.readFileSync(path.join(repo,importerPath),'utf8');
const importerAnchor='normalized["rebuildGatherOccupancyKnown"] = gatherOccupancyKnown;';
assert.ok(importer.includes(importerAnchor));
const manifest={task:'LWB317-UI-LEAD-TABLES-001',referenceExe,referenceExeSha256:exeHash,clockAnchor,arrowPaths,
  currentOccupancyProducer:{path:importerPath,sha256NormalizedLF:hash(normalized(importerPath)),utf8ByteOffset:Buffer.byteLength(importer.slice(0,importer.indexOf(importerAnchor))),expression:importerAnchor},
  productionFiles:productionFiles.map(file=>({file,sha256NormalizedLF:hash(normalized(file))})),screenshots,
  evidence:['map-table-results.json','browser-results.json'].map(file=>({file,sha256NormalizedLF:hash(fs.readFileSync(path.join(here,file),'utf8').replace(/\r\n/g,'\n'))})),
  tools:{node:process.version,babelParser:require('@babel/parser/package.json').version,esbuild:require('esbuild/package.json').version},
  limits:'Source/local table scope; independent review pending. Synthetic data and placeholder native images are not original pixels, current game text integration or live gameplay proof.'};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
assert.deepEqual(read('manifest.json'),manifest);
for(const item of read('protected-wip.json')){const bytes=fs.readFileSync(path.join(repo,item.path));assert.equal(hash(bytes).toLowerCase(),item.sha256);assert.equal(bytes.length,item.bytes);}
const verification=read('verification.json');assert.equal(verification.state,'AWAITING_REVIEW');
for(const check of Object.values(verification.checks))assert.equal(check,'PASS');
assert.equal(verification.package.sourceFingerprint,'1280d8a4df7aec2f81260a8081c0dda7626bad06e8771d25cea4e3ae0c49daf2');
assert.equal(verification.package.packageFingerprint,'b2a903176188b57cd02f281ffaded42aa05a0d6503595e25ee3e5fcfa09127ae');
console.log('LWB317_LEAD_TABLES_EVIDENCE_OK');
