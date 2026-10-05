import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';

// Read-only execution of the audited actual Ae/current adapter. Its recorded
// files are only read; all extra cases and output belong to this lead review.
const repo=process.cwd();
const sourcePath=path.join(repo,'evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CLOSEOUT-003/milestone-c/conditional-composition.mjs');
const source=fs.readFileSync(sourcePath,'utf8');
const contracts=await import(pathToFileURL(path.join(repo,'src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js')));
const fixtures=await import(pathToFileURL(path.join(repo,'src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js')));
let code=source.replace(/^import .*;\r?\n/gm,'').replaceAll('fileURLToPath(import.meta.url)',`fileURLToPath(${JSON.stringify(pathToFileURL(sourcePath).href)})`);
code=code.replace('const verifyOnly = process.argv.includes("--verify");','const verifyOnly = true;');
const AsyncFunction=Object.getPrototypeOf(async function(){}).constructor;
const api=await new AsyncFunction('fs','path','crypto','assert','createRequire','fileURLToPath','pathToFileURL','fixtures','contracts',code+'\nreturn {trainSource,trainCurrent,card,carriageGroups};')(fs,path,crypto,assert,createRequire,fileURLToPath,pathToFileURL,fixtures,contracts);
const cases=[];
for(const normal of [[],[4],[1,2],[99],[4,99]])for(const vip of [[],[3,4],[4,99]])for(const explicit of [true,false]){
  const sourceConfig={fixedCarriageIds:normal,vipFixedCarriageIds:vip,preferredRewardKeys:[]};
  const currentConfig={enabled:false,normalFixedCarriageIds:normal,vipFixedCarriageIds:vip,preferredRewardKeys:[],thanksMode:'like'};
  // The source helper inherits explicit reward modes. `undefined` restores the
  // original nullish-mode fallback while leaving matched arrays unchanged.
  sourceConfig.selectionMode=explicit?'fixed':undefined;
  sourceConfig.vipSelectionMode=explicit?'fixed':undefined;
  if(explicit){currentConfig.trainMode='fixed';currentConfig.vipTrainMode='fixed';}
  const original=api.card(api.trainSource(sourceConfig),'Automatic Alliance Train Boarding');
  const current=api.trainCurrent(currentConfig);
  const om=api.carriageGroups(original),cm=api.carriageGroups(current);
  assert.deepEqual(cm,om,`normal=${normal} vip=${vip} explicit=${explicit}`);
  const radios=html=>[...html.matchAll(/<fieldset class="automation-compact-choice-group automation-selection-mode-choices"[^>]*>([\s\S]*?)<\/fieldset>/g)].map(m=>[...m[1].matchAll(/<input[^>]*>/g)].map(([tag])=>/checked=""/.test(tag)));
  assert.deepEqual(radios(current),radios(original),'Train inferred mode mismatch');
  cases.push({normal,vip,explicit,carriages:cm,modeRadios:radios(current),pass:true});
}
console.log(JSON.stringify({marker:'LWB317_LEAD_AUTOMATION_TRAIN_INVERSE_OK',cases:cases.length,actualRecoveredCurrent:true,results:cases},null,2));
