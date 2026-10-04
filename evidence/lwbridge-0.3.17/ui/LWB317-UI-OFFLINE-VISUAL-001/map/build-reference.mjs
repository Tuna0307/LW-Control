import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
import * as helpers from '../../../../../src/LWBridge.UI-0.3.17/src/mapTablePresentation.js';
import {selectionMembershipKey} from '../../../../../src/LWBridge.UI-0.3.17/src/mapInteractions.js';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=require('@babel/parser'),{transformSync,buildSync}=require('esbuild'),React=require('react'),{renderToStaticMarkup}=require('react-dom/server');
const domRequire=createRequire('C:/Users/chimw/AppData/Local/Temp/lwb317-shell-mount-deps/package.json'),{JSDOM}=domRequire('jsdom');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const assets='evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/';
const originals=[['MapDataPanel-B4GXEND2.js','ce74345518be72e417a510b982c591729e5683f69751e190805598c4c05d3089'],['index-BVfnK1wp.js','44c4e4043825b7db296b64171951b27176f8850ff8d7d337cc991df4765524c6'],['rewardDisplay-eZWrd6iS.js','7f65dd3f5b81c96117af5e83e8310d6c6a3a48787b1f693d387020255c78e73e'],['GameAssetImage-Diy9VTIr.js','2f92a87c3268497df6425b1175db6140e10aabcbdfae02615f065d00e16458e0']].map(([name,sha256])=>{let source=fs.readFileSync(path.join(repo,assets,name),'utf8');assert.equal(hash(source),sha256);return{name,sha256,source,ast:parse(source,{sourceType:'module'})};});
const slices=[];
function pick(asset,name,variable=false){const n=variable?asset.ast.program.body.filter(n=>n.type==='VariableDeclaration').flatMap(n=>n.declarations).find(n=>n.id.name===name):asset.ast.program.body.find(n=>n.type==='FunctionDeclaration'&&n.id.name===name);assert.ok(n,`${asset.name}/${name}`);const text=asset.source.slice(n.start,n.end);slices.push({asset:asset.name,name,utf8ByteOffset:Buffer.byteLength(asset.source.slice(0,n.start)),byteLength:Buffer.byteLength(text),sha256:hash(text),text});return(variable?'const ':'')+text+(variable?';':'');}
const [map,main,reward,image]=originals;
const pureNames=['k','A','j','M','De','Oe','ke','w','T','Ke','qe','Je','N','P','F','Qe','$e','I','L','et','nt'];
const pureCode=pureNames.map(n=>pick(map,n)).join('\n')+'\n'+['O','Ee','Ie'].map(n=>pick(map,n,true)).join('\n');
const truckCode=['Fe','Ie'].map(n=>pick(main,n)).join('\n'),iconCode=pick(main,'Vr'),tableCode=pick(map,'at',true),rewardCode=pick(reward,'e');
const imageCode=['b','x'].map(n=>pick(image,n)).join('\n');
const SourceImage=new Function('i','a','l','y',imageCode+'\nreturn x;')(React,require('react/jsx-runtime'),new Map(),()=>{throw Error('SSR must not read images');});
const SourceIcon=new Function('M',iconCode+'\nreturn Vr;')(require('react/jsx-runtime'));
const rewardCount=new Function(rewardCode+'\nreturn e;')();
const currentPath='src/LWBridge.UI-0.3.17/src/MapDataPage.jsx',current=fs.readFileSync(path.join(repo,currentPath),'utf8').replaceAll('\r\n','\n'),currentAst=parse(current,{sourceType:'module',plugins:['jsx']});
function currentFn(name){const n=currentAst.program.body.map(n=>n.declaration||n).find(n=>n.type==='FunctionDeclaration'&&n.id.name===name);assert.ok(n,name);return current.slice(n.start,n.end);}
const currentCode=transformSync(currentFn('coordinateText')+'\n'+currentFn('MapTable'),{loader:'jsx',jsxFactory:'React.createElement'}).code;
const imageBundle=buildSync({entryPoints:[path.join(repo,'src/LWBridge.UI-0.3.17/src/GameAssetImage.jsx')],bundle:true,platform:'node',format:'cjs',jsx:'automatic',external:['react','react/jsx-runtime'],write:false}).outputFiles[0].text;
const currentImageModule={exports:{}};new Function('require','module','exports',imageBundle)(require,currentImageModule,currentImageModule.exports);
const kinds=['city','resource','monster','truck','railway','dispatch','ghost','treasure'];
const keysNode=currentAst.program.body.filter(n=>n.type==='VariableDeclaration').flatMap(n=>n.declarations).find(n=>n.id.name==='SCAN_TYPE_LABEL_KEYS');
assert.ok(keysNode,'Actual current scan labels');
const keys=new Function('return ('+current.slice(keysNode.init.start,keysNode.init.end)+');')();
const now=1799000000000,RealDate=globalThis.Date;class FixedDate extends RealDate{constructor(...args){super(...(args.length?args:[now]));}static now(){return now;}}
const row={serverId:321,uuid:'12345',x:12,y:34,updatedAt:now-1000,ownerName:'Fixture owner',ownerUid:'',allianceName:'Fixture alliance',level:12,health:99999,protectEndTime:now+60000,resourceNameKey:'resource-key',monsterNameKey:'monster-key',quality:6,power:1000000,robTimes:1,maxLootCount:3,arriveTs:now+100000,protectTime:now+3000,completionTime:now-1000,taskExpireTime:now+10000,plunderAt:now-500,stolenCount:0,maxStealCount:2,treasureType:1,suppliesType:0,remainingBoxes:3,rewardedCount:1,diggingCount:2,worldClaimState:'charging',chargePercent:0.375,playerClaimState:'unclaimed',expireTime:now+10000,rewards:[{key:'reward',name:'Fixture reward',iconPath:'fixture/reward.png',count:1000}],currentGoods:[{key:'goods',name:'Fixture goods',iconPath:'fixture/goods.png',count:1250}]};
const texts={'resource-key':'Fixture resource','monster-key':'Fixture monster','300039':'Fixture occupied','372138':'Fixture idle'};
const css=fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/reference.css'),'utf8'),generated=path.join(here,'generated');fs.mkdirSync(generated,{recursive:true});
const records=[];
function describe(root){return [...root.querySelectorAll('*')].map(e=>({tag:e.tagName,attrs:[...e.attributes].filter(a=>!(e.tagName==='BUTTON'&&a.name==='type')).sort((a,b)=>a.name.localeCompare(b.name)).map(a=>[a.name,a.value]),text:[...e.childNodes].filter(n=>n.nodeType===3).map(n=>n.textContent).join('')}));}
globalThis.Date=FixedDate;
try{for(const language of ['en','ja']){
 const {default:catalog}=await import(pathToFileURL(path.join(repo,`src/LWBridge.UI-0.3.17/src/locales/${language}.js`)));
 const t=(key,values={})=>(catalog[key]||key).replace(/\{(\w+)\}/g,(m,k)=>String(values[k]??m));
 const Original=new Function('Date','y','u','E','v','_','r','l','se',pureCode+'\n'+tableCode+'\nreturn at;')(FixedDate,React,()=>({language,t}),require('react/jsx-runtime'),SourceImage,rewardCount,new Function(truckCode+'\nreturn Ie;')(),new Function(truckCode+'\nreturn Fe;')(),SourceIcon);
 const Current=new Function('React','useI18n','useMemo','useState','useEffect','helpers','selectionMembershipKey','GameAssetImage','SCAN_TYPE_LABEL_KEYS','EMPTY_GAME_TEXTS','const {buildMapColumns,mapNumber,mapResourceStatus,mapRewardCount,mapRewardName,mapTaskLabel,mapTaskSelectable,mapTaskState}=helpers;\n'+currentCode+'\nreturn MapTable;')(React,()=>({language,t}),React.useMemo,React.useState,React.useEffect,helpers,selectionMembershipKey,currentImageModule.exports.GameAssetImage,keys,{});
 for(const kind of kinds)for(const state of ['empty','loading','populated']){
  const shared={kind,rows:state==='populated'?[row]:[],loading:state==='loading',gameTexts:texts,itemKey:'',jumpingKey:''};
  const originalProps={...shared,currentTime:now,sortState:[{sortBy:'updatedAt',sortOrder:'desc'}],selectedDispatchKeys:new Set(),selectedTruckKeys:new Set(),jumpDisabled:true,treasureStatesRefreshing:false,treasureClaimBusy:false,treasureClaimDisabled:true,onSort:()=>{},onToggleDispatch:()=>{},onToggleTruck:()=>{},onCoordinateJump:()=>{},onPlayerMark:()=>{},onClaimTreasure:()=>{}};
  const currentProps={...shared,sorts:originalProps.sortState,selectedKeys:new Set(),actionBusy:false,actionDisabled:true,onSort:()=>{},onSelect:()=>{},onCoordinateJump:()=>{},onPlayerMark:()=>{}};
  const expected=renderToStaticMarkup(React.createElement(Original,originalProps)),actual=renderToStaticMarkup(React.createElement(Current,currentProps));
  const ed=new JSDOM(expected),ad=new JSDOM(actual),e=describe(ed.window.document.body),a=describe(ad.window.document.body),differences=[];
  for(let i=0;i<Math.max(e.length,a.length);i++)if(JSON.stringify(e[i])!==JSON.stringify(a[i]))differences.push({element:i,expected:e[i]??null,actual:a[i]??null});
  for(const theme of ['light','dark'])for(const [side,markup] of [['original',expected],['current',actual]])fs.writeFileSync(path.join(generated,`${language}-${theme}-${kind}-${state}-${side}.html`),`<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Map ${kind} ${state} ${side}</title><style>${css}</style></head><body><main class="app-shell"><div class="app-layout single-profile"><nav class="nav-rail"></nav><section class="main-view"><section class="panel map-data-panel">${markup}</section></section></div></main></body></html>`);
  records.push({language,kind,state,sameStructureExceptButtonType:differences.length===0,elementCount:[e.length,a.length],differences,expectedHtmlSha256:hash(expected),actualHtmlSha256:hash(actual)});ed.window.close();ad.window.close();
 }
}}finally{globalThis.Date=RealDate;}
fs.writeFileSync(path.join(here,'render-results.json'),JSON.stringify({marker:'LWB317_MAP_TABLE_OFFLINE_REFERENCE_BUILT',reference:originals.map(({name,sha256})=>({path:assets+name,sha256})),slices,current:{path:currentPath,sha256:hash(current)},cssSha256:hash(css),cases:records.length,matched:records.filter(r=>r.sameStructureExceptButtonType).length,records,limits:'Actual memoized original table and original pure helpers/icons/reward/image components; actual current table/helpers/image component. Fixed clock, inert callbacks, empty original image cache and no effects during SSR. Isolated panel container; no full Map header/panel shell, loaded images, mounted async/native or protected-runtime pixel proof.'},null,2)+'\n');
console.log(JSON.stringify({marker:'LWB317_MAP_TABLE_OFFLINE_REFERENCE_BUILT',cases:records.length,matched:records.filter(r=>r.sameStructureExceptButtonType).length,differences:records.filter(r=>r.differences.length).map(r=>({language:r.language,kind:r.kind,state:r.state,elements:r.differences.length}))}));
