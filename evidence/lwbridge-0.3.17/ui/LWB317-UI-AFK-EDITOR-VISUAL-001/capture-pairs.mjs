import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {here,repo,hash} from './harness.mjs';
const pw=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=pw('playwright');
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const baseline=process.argv.includes('--baseline');
const phase=baseline?'baseline':'current',root=path.join(here,phase);fs.mkdirSync(root,{recursive:true});
const input=path.join(here,baseline?'baseline-render':'render');
const css={original:fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'),'utf8'),current:fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/reference.css'),'utf8')+'\n'+fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/styles.css'),'utf8')};
const variants=[{language:'en',theme:'light',width:1280,height:900},{language:'ja',theme:'dark',width:1280,height:900},{language:'en',theme:'dark',width:375,height:1000},{language:'ja',theme:'light',width:375,height:1000}];
const pairs=[],issues=[];
for(const v of variants)for(const id of ['filters','custom-empty',...(v.width===1280?['warning','join-delay']:[])]){
 const name=`${v.language}-${id}-${v.theme}-${v.width}`,sides={};
 for(const side of ['original','current']){
  const context=await browser.newContext({viewport:{width:v.width,height:v.height},deviceScaleFactor:1,locale:v.language,timezoneId:'Asia/Singapore',colorScheme:v.theme});
  const page=await context.newPage();page.on('console',m=>{if(['error','warning'].includes(m.type()))issues.push({name,side,text:m.text()});});page.on('pageerror',e=>issues.push({name,side,text:e.message}));
  const html=fs.readFileSync(path.join(input,`${v.language}-${id}-${side}.html`),'utf8');
  await page.setContent(`<!doctype html><html lang="${v.language}" data-theme="${v.theme}"><head><meta charset="utf-8"><style>${css[side]}</style></head><body><main class="main-view"><section class="panel squad-panel">${html}</section></main></body></html>`);
  await page.evaluate(()=>document.fonts.ready);
  const measurement=await page.evaluate(()=>{
   const root=document.querySelector('.monster-afk-editor');
   const styles=['display','fontSize','fontWeight','lineHeight','color','backgroundColor','padding','margin','gap','border','gridTemplateColumns'];
   return {viewport:[innerWidth,innerHeight],documentWidth:document.documentElement.scrollWidth,html:root.outerHTML,elements:[root,...root.querySelectorAll('*')].map(e=>{const r=e.getBoundingClientRect(),s=getComputedStyle(e);return {tag:e.tagName,class:e.getAttribute('class'),rect:[r.x,r.y,r.width,r.height],styles:Object.fromEntries(styles.map(p=>[p,s[p]]))};})};
  });
  const screenshot=`${name}-${side}.png`,json=`${name}-${side}.json`;
  await page.screenshot({path:path.join(root,screenshot),fullPage:true});fs.writeFileSync(path.join(root,json),JSON.stringify(measurement,null,2)+'\n');
  sides[side]={screenshot,measurement:json,sha256:hash(fs.readFileSync(path.join(root,screenshot)))};await context.close();
 }
 const a=JSON.parse(fs.readFileSync(path.join(root,sides.original.measurement))),b=JSON.parse(fs.readFileSync(path.join(root,sides.current.measurement)));
 pairs.push({id:name,...v,...sides,geometryAndStylesEqual:JSON.stringify(a.elements)===JSON.stringify(b.elements),browserDomEqual:a.html===b.html,encodedPngEqual:sides.original.sha256===sides.current.sha256,masks:[]});
}
await browser.close();fs.writeFileSync(path.join(root,'pairs.json'),JSON.stringify({pairs,issues,playwright:pw('playwright/package.json').version,scope:'Exact original subtree and canonical editor, independently recovered catalogs/CSS, identical declared host, synthetic targets, no protected runtime access, no pixel masks.'},null,2)+'\n');
console.log(JSON.stringify({phase,pairs:pairs.length,equal:pairs.filter(p=>p.geometryAndStylesEqual&&p.encodedPngEqual).length,issues:issues.length}));
