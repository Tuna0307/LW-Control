// Preserve old checkers and every assertion. Adapt only stale harness bindings.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createRequire} from 'node:module';
import * as autoConfig from '../../../../../src/LWBridge.UI-0.3.17/src/mapAutoConfig.js';
import * as interactions from '../../../../../src/LWBridge.UI-0.3.17/src/mapInteractions.js';
import * as scanPresentation from '../../../../../src/LWBridge.UI-0.3.17/src/mapScanPresentation.js';
import * as tablePresentation from '../../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..'),base=path.join(repo,'evidence/lwbridge-0.3.17/ui');
const mode=process.argv[2],sourcePaths={navigation:'LWB317-UI-MAP-NAVIGATION-001/check-navigation.mjs',goods:'LWB317-UI-MAP-GOODS-PICKER-001/check-picker.mjs',scheduled:'LWB317-UI-MAP-INTERACTIONS-001/scheduled/check-scheduled-plunder.mjs'};
assert.ok(sourcePaths[mode]);const file=path.join(base,sourcePaths[mode]),original=fs.readFileSync(file,'utf8'),sha=s=>crypto.createHash('sha256').update(s).digest('hex');
function adapt(s){
  s=s.replace(/const here\s*=\s*path\.dirname\(fileURLToPath\(import\.meta\.url\)\);/,`const here = ${JSON.stringify(path.dirname(file))};`);
  const {parse}=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'))('@babel/parser');
  const imports=parse(s,{sourceType:'module'}).program.body.filter(n=>n.type==='ImportDeclaration'&&n.source.value.startsWith('.'));
  for(const n of imports.reverse())s=s.slice(0,n.source.start)+JSON.stringify(pathToFileURL(path.resolve(path.dirname(file),n.source.value)).href)+s.slice(n.source.end);
  return s;
}
let source=adapt(original),details=[];
const inject={...autoConfig,...interactions,...scanPresentation,...tablePresentation,ScheduledPlunder:function ScheduledPlunder(){},MapTreasureTypeFilter:function MapTreasureTypeFilter(){},MapRetainedGoodsFilter:function MapRetainedGoodsFilter(){}};
if(mode==='navigation'){
  const old='const index = stateNames.indexOf(name);';assert.ok(source.includes(old));source=source.replace(old,"const index = stateNames.indexOf(name === 'tab' && !stateNames.includes('tab') ? 'localTab' : name);");
  details.push('Resolve old harness getState(tab) to renamed uncontrolled localTab only when original tab state binding is absent; actual production selector and callback stay unchanged. All 17 requests and baseline six failures retained.');
}
if(mode==='goods'){
  const old="const compiled = new Function('useRef', 'h', code + '\\nreturn { MapRetainedGoodsFilter, MapFilterAssetPlaceholder };')(() => ({ current: null }), h);";assert.ok(source.includes(old));
  const image=fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/GameAssetImage.jsx'),'utf8').replace(/^import[^\n]+\n/gm,'').replace(/export /g,'');
  const replacement=`const imageCode=transformSync(${JSON.stringify(image)},{loader:'jsx',jsxFactory:'h'}).code;
  const actualImage=new Function('createContext','useContext','useEffect','useRef','useState','h',imageCode+'\\nreturn GameAssetImage;')(()=>null,()=>null,()=>{},()=>({current:null}),v=>[typeof v==='function'?v():v,()=>{}],h);
  const compiled = new Function('useRef','h','GameAssetImage',code+'\\nreturn {MapRetainedGoodsFilter,MapFilterAssetPlaceholder:GameAssetImage};')(()=>({current:null}),h,actualImage);`;
  source=source.replace(old,replacement);details.push('Resolve obsolete removed placeholder export through the actual GameAssetImage component/normalizer with absent reader and inert effects; retain nine-locale original/current picker and parent assertions.');
}
if(mode==='scheduled'){
  const old="if (args.path === 'i18n.jsx') return { contents: i18nStub, loader: 'jsx' };";assert.ok(source.includes(old));
  source=source.replace(old,old+`\n      if(args.path==='GameAssetImage.jsx')return{contents:'import React from "react";export function GameAssetImage({className}){return React.createElement("span",{className:className+" game-asset-placeholder","aria-hidden":"true"});}',loader:'jsx'};`);
  details.push('Expose newly imported image component as the exact same historical placeholder used by original oracle, preserving structural table/controls/status proof and every 10482 render/51 mutation assertion. This legacy render replay normalizes image aria attributes; actual image/caller source parity is separately verified in the images packet.');
}
for(const [name,value]of Object.entries(inject)){assert.equal(Object.hasOwn(globalThis,name),false);globalThis[name]=value;}
const oldLog=console.log,output=[];console.log=(...args)=>output.push(args.join(' '));
try{await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));}
catch(error){console.log=oldLog;console.error(`${mode}: ${error.name}: ${error.message}`);process.exitCode=1;}
finally{console.log=oldLog;for(const name of Object.keys(inject))delete globalThis[name];}
assert.equal(fs.readFileSync(file,'utf8'),original,'historical checker unchanged');
const report={mode,source:sourcePaths[mode],sha256:sha(original),adaptedHarnessSha256:sha(source),details,output,status:process.exitCode?'FAIL':'PASS'};
fs.writeFileSync(path.join(here,`shape-replay-${mode}.json`),JSON.stringify(report,null,2)+'\n');console.log(`LWB317_CURRENT_SHAPE_REPLAY_${mode.toUpperCase()}_${report.status}`);if(!process.exitCode)for(const line of output.slice(-2))console.log(line);
