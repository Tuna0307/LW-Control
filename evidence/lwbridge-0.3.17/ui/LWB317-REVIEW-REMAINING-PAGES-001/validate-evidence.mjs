import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,'../../../..');
const worker='evidence/lwbridge-0.3.17/ui/LWB317-UI-REMAINING-PAGES-001';
const json=file=>JSON.parse(fs.readFileSync(path.join(repo,file),'utf8'));
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();
const source=json(`${worker}/milestone-a/source-manifest.json`);
const expectedExe='4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783';
assert.equal(source.referenceExecutableSha256,expectedExe);
assert.equal(sha(fs.readFileSync(source.referenceExecutable)),expectedExe);
for(const asset of source.assets)assert.equal(sha(fs.readFileSync(path.join(repo,asset.path))),asset.sha256,asset.path);
for(const [name,locator] of Object.entries(source.locators)) {
  const bytes=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets',locator.asset));
  assert.equal(sha(bytes.subarray(locator.utf8ByteOffset,locator.utf8ByteOffset+locator.utf8ByteLength)),locator.sha256,name);
}
assert.deepEqual(json(`${worker}/milestone-a/baseline-results.json`),JSON.parse(execFileSync('git',['show',`afd65b64808e0fd43175dbbaa5afc36e32def9ef:${worker}/milestone-a/baseline-results.json`],{cwd:repo,encoding:'utf8'})));
const report=JSON.parse(fs.readFileSync(path.join(here,'independent-results.json'),'utf8'));
assert.equal(report.decision,'CHANGES_REQUIRED');assert.equal(report.results.length,12);
const imageManifest=json(`${worker}/milestone-f/screenshot-manifest.json`);
assert.equal(imageManifest.screenshots.length,12);
const files=[
  'src/LWBridge.UI-0.3.17/src/Pages.jsx',
  'src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js',
  'src/LWBridge.UI-0.3.17/src/App.jsx',
  `${worker}/milestone-a/source-manifest.json`,`${worker}/milestone-a/baseline-results.json`,
  ...source.assets.map(a=>a.path),
  ...imageManifest.screenshots.map(image=>`${worker}/milestone-f/${image.file}`),
  ...['check-review.mjs','independent-results.json','verification-results.json'].map(name=>path.relative(repo,path.join(here,name)).replaceAll('\\','/'))
];
for(const image of imageManifest.screenshots)assert.equal(sha(fs.readFileSync(path.join(repo,worker,'milestone-f',image.file))),image.sha256,image.file);
const entries=files.map(file=>({path:file,sha256:sha(fs.readFileSync(path.join(repo,file)))}));
const manifestPath=path.join(here,'review-manifest.json');
if(process.argv.includes('--record'))fs.writeFileSync(manifestPath,JSON.stringify({submittedCommit:report.submittedCommit,referenceExecutableSha256:expectedExe,files:entries},null,2)+'\n');
else assert.deepEqual(entries,JSON.parse(fs.readFileSync(manifestPath,'utf8')).files,'Historical review snapshot differs; create a new R1 packet instead of overwriting pins');
const protectedResult=execFileSync(process.execPath,['evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs'],{cwd:repo,encoding:'utf8'});
assert.ok(protectedResult.includes('LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7'));
console.log(`LWB317_REMAINING_PAGES_LEAD_REVIEW_OK decision=CHANGES_REQUIRED cases=${report.results.length} assets=${source.assets.length} slices=${Object.keys(source.locators).length} images=12 protected=7`);
