import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {require,read,fn,raw,nodes,compile} from '../unit-c/accepted-harness.mjs';
const here=path.dirname(fileURLToPath(import.meta.url)),React=require('react'),A=require('react/jsx-runtime'),{renderToStaticMarkup}=require('react-dom/server');
const packet=process.argv.includes('--baseline')?'compact-baseline':'compact-current';
const currentPath='src/LWBridge.UI-0.3.17/src/SquadsPage.jsx',source=read(currentPath),main=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'),card=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js');
const hash=x=>crypto.createHash('sha256').update(x).digest('hex');
const Icon=compile(main,'Vr',{M:A}),Original=compile(card,'l',{s:A,i:Icon});
const originalCSS=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'),currentCSS=read('src/LWBridge.UI-0.3.17/src/reference.css');
const cases=[];const out=path.join(here,packet+'-generated');fs.mkdirSync(out,{recursive:true});
for(const language of ['en','ja']){
 const localeAsset=language==='en'?'en-BisSXcTB.js':'ja-UrbzJu-m.js';const lc=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/'+localeAsset),node=nodes(lc).find(n=>n.type==='VariableDeclarator'&&n.id.name==='t');const catalog=new Function('e','return ('+raw(lc,node.init)+');')({});
 const t=k=>catalog[k]??k;const env={h:React.createElement,useI18n:()=>({t})};const Glyph=compile(source,'SettingsGlyph',env),Current=compile(source,'CompactAfkCard',{...env,SettingsGlyph:Glyph});
 for(const enabled of [false,true])for(const settingsOpen of [false,true]){
  const state=`${enabled?'enabled':'disabled'}-${settingsOpen?'open':'closed'}`,id=language+'-'+state;
  const props={title:t('squad.afkMaster'),description:t('squad.afkMasterDescription'),summary:t(enabled?'common.enabled':'common.disabled'),enabled,disabled:false,settingsOpen,onToggle(){},onSettings(){},settingsLabel:t('nav.settings'),cardSelectable:true};
  const original=renderToStaticMarkup(React.createElement(Original,props)),current=renderToStaticMarkup(React.createElement(Current,props));
  for(const theme of ['light','dark'])for(const [side,markup,css] of [['original',original,originalCSS],['current',current,currentCSS]])fs.writeFileSync(path.join(out,`${id}-${theme}-${side}.html`),`<!doctype html><html lang="${language}" data-theme="${theme}"><head><meta charset="utf-8"><style>${css}</style></head><body><section class="panel squad-panel"><div class="monster-afk-toolbar">${markup}</div></section></body></html>`);
  cases.push({id,language,state,narrow:true,originalHtmlSha256:hash(original),currentHtmlSha256:hash(current),rawEqual:original===current});
 }
}
const slices=[['AutomationCard-LCx_jIi7.js',card,'l'],['index-BVfnK1wp.js',main,'Vr']].map(([asset,s,name])=>{const n=fn(s,name);return {asset,name,offset:Buffer.byteLength(s.slice(0,n.start)),length:Buffer.byteLength(raw(s,n)),sha256:hash(raw(s,n))};});
if(packet==='compact-baseline')fs.writeFileSync(path.join(here,'SquadsPage.compact-baseline.jsx'),source);
fs.writeFileSync(path.join(here,packet+'-pair-render.json'),JSON.stringify({marker:'LWB317_UNIT_D_COMPACT_PAIRS',cases,slices,current:{path:currentPath,sha256:hash(source)},scope:'Executed exact original l/Vr and actual canonical compact card with independent recovered locale values, source CSS/current CSS and inert local callbacks.'},null,2)+'\n');console.log(JSON.stringify({packet,cases:cases.length,rawEqual:cases.filter(c=>c.rawEqual).length}));
