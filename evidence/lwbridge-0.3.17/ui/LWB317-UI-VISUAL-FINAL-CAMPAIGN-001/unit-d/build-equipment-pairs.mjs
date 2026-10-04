import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {require,read,squad,fn,raw,nodes,compile} from '../unit-c/accepted-harness.mjs';
import * as contracts from '../../../../../src/LWBridge.UI-0.3.17/src/previewEquipmentContracts.js';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..');
const React=require('react'),A=require('react/jsx-runtime'),{renderToStaticMarkup}=require('react-dom/server');
const {buildSync}=require('esbuild');
const hash=x=>crypto.createHash('sha256').update(x).digest('hex');
const source=read('src/LWBridge.UI-0.3.17/src/SquadsPage.jsx');
const main=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js');
const image=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js');
const sources=[];
function declaration(asset,text,name){const node=fn(text,name);sources.push({asset,name,offset:Buffer.byteLength(text.slice(0,node.start)),length:Buffer.byteLength(raw(text,node)),sha256:hash(raw(text,node))});return raw(text,node);}
function bundled(file){const out=buildSync({entryPoints:[path.join(repo,file)],bundle:true,platform:'node',format:'cjs',jsx:'automatic',external:['react','react/jsx-runtime'],write:false}).outputFiles[0].text;const m={exports:{}};new Function('require','module','exports',out)(require,m,m.exports);return m.exports;}
const motion=bundled('src/LWBridge.UI-0.3.17/src/EquipmentMotion.jsx');
const currentImage=bundled('src/LWBridge.UI-0.3.17/src/GameAssetImage.jsx');
const imageCode=['b','x'].map(name=>declaration('GameAssetImage-Diy9VTIr.js',image,name)).join('\n');
const OriginalImage=new Function('i','a','l','y',imageCode+';return x;')(React,A,new Map(),()=>{throw Error('SSR image reads prohibited');});
const originalDialog=compile(main,'In',{b:React,M:A});
const currentDialog=compile(source,'EquipmentDialog',{h:React.createElement,useRef:React.useRef,useEffect:React.useEffect});
const helperNames=['ad','od','sd','cd','dd','ld','ud'];
const helperCode=helperNames.map(name=>declaration('SquadPanel-HC3-DJei.js',squad,name)).join('\n');
const fd=declaration('SquadPanel-HC3-DJei.js',squad,'fd');
const cssOriginal=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css');
const cssCurrent=read('src/LWBridge.UI-0.3.17/src/reference.css');
const generated=path.join(here,'paired-generated');fs.mkdirSync(generated,{recursive:true});
const cases=[];
const states=['squads-equipment','squads-equipment-empty','squads-equipment-offline','squads-equipment-progress','squads-equipment-result','squads-equipment-error','squads-equipment-rename','squads-equipment-rename-busy'];
for(const language of ['en','ja']){
 const asset=language==='en'?'en-BisSXcTB.js':'ja-UrbzJu-m.js';
 const localeSource=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/'+asset);
 const localeNode=nodes(localeSource).find(n=>n.type==='VariableDeclarator'&&n.id.name==='t');
 const catalog=new Function('e','return ('+raw(localeSource,localeNode.init)+');')({});
 const originalT=(key,vars={})=>String(catalog[key]??key).replace(/\{(\w+)\}/g,(m,k)=>String(vars[k]??m));
 const currentCatalog=(await import('../../../../../src/LWBridge.UI-0.3.17/src/locales/'+language+'.js')).default;
 const currentT=(key,vars={})=>String(currentCatalog[key]??key).replace(/\{(\w+)\}/g,(m,k)=>String(vars[k]??m));
 for(const state of states){
  const fixture=contracts.previewEquipmentFixture(state,originalT);
  const config={equipmentPresets:fixture.presets,initialEquipmentConfig:fixture.initialEquipmentConfig};
  const confirmed={equipmentPresets:fixture.confirmedPresets};
  const seeds=['',fixture.busyKey,fixture.result,fixture.progress,null,'',[],'',fixture.renameOpen,fixture.presets[0]?.name||''];
  let cursor=0;
  const O={...React,useEffect:()=>{},useState(initial){const i=cursor++;return React.useState(i<seeds.length?seeds[i]:initial);}};
  const draft={draft:config,confirmed,state:{error:null,saving:false,getSnapshot:()=>({draft:config}),edit(){throw Error('SSR edit prohibited');},refresh:async()=>{},flush:async()=>{}}};
  const env={O,A,d:()=>({t:originalT}),c:()=> 'inert-profile',e:()=>draft,ed:()=>true,td:motion.motion,rl:motion.AnimatePresence,ae:OriginalImage,ne:()=>null,y:originalDialog,rd:4,id:contracts.EQUIPMENT_PRESET_IDS||Array.from({length:4},(_,i)=>`equipment-preset-fixed-${i+1}`),nd:[1,2,3,4],structuredClone,window:{},h(){},r(){},E(){},ee(){}};
  const original=new Function(...Object.keys(env),helperCode+'\n'+fd+';return fd;')(...Object.values(env));
  const Current=compile(source,'EquipmentContent',{h:React.createElement,Fragment:React.Fragment,useState:React.useState,useRef:React.useRef,useMemo:React.useMemo,useEffect:()=>{},useI18n:()=>({t:currentT}),...contracts,motion:motion.motion,AnimatePresence:motion.AnimatePresence,useReducedMotion:()=>true,equipmentMotionProps:motion.equipmentMotionProps,GameAssetImage:currentImage.GameAssetImage,EquipmentDialog:currentDialog,PreviewConfigError:()=>null,window:{}});
  cursor=0;const expected=renderToStaticMarkup(React.createElement(original,{online:fixture.online,squads:fixture.squads,reloadSquads:async()=>{},onError(){}}));
  const actual=renderToStaticMarkup(React.createElement(Current,{previewEnabled:true,previewState:state}));
  const id=language+'-'+state;
  for(const theme of ['light','dark'])for(const [side,markup,css] of [['original',expected,cssOriginal],['current',actual,cssCurrent]])fs.writeFileSync(path.join(generated,`${id}-${theme}-${side}.html`),`<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><style>${css}</style></head><body><section class="panel squad-panel">${markup}</section></body></html>`);
  cases.push({id,language,state,originalMarkupSha256:hash(expected),currentMarkupSha256:hash(actual),rawEqual:expected===actual});
 }
}
const report={marker:'LWB317_UNIT_D_EQUIPMENT_PAIRS_BUILT',cases,sources,originalCssSha256:hash(cssOriginal),currentCssSha256:hash(cssCurrent),scope:'Actual complete fd and canonical EquipmentContent SSR, original/canonical independent catalogs, equal disclosed inert state, reduced motion snapshot, original image cache empty. Config-error components inert/null on both sides; native actions never invoked. This is source-rendered comparison, not protected runtime, native assets, or physical drag.'};
fs.writeFileSync(path.join(here,'equipment-pair-render.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({marker:report.marker,cases:cases.length,rawEqual:cases.filter(x=>x.rawEqual).length}));
