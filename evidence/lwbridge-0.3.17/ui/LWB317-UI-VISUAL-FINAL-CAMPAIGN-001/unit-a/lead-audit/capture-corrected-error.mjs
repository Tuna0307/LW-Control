import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const readOnly=process.argv.includes('--read-only');
const repo=path.resolve(here,'../../../../../..');
const require=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=require('playwright');
const hash=data=>crypto.createHash('sha256').update(data).digest('hex');
const originalCss=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'),'utf8');
const currentCss=fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/reference.css'),'utf8')+'\n'+fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/styles.css'),'utf8');
const out=path.join(here,'corrected-error-browser');if(!readOnly)fs.mkdirSync(out,{recursive:true});
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const issues=[],pairs=[];
try{
for(const spec of [{language:'en',theme:'dark',width:1280,height:900},{language:'ja',theme:'dark',width:1280,height:900},{language:'ja',theme:'light',width:375,height:1100}]){
 const id=`city-error-${spec.language}-${spec.theme}-${spec.width}`;
 const screenshots={},measurements={};
 for(const side of ['original','current']){
  const raw=fs.readFileSync(path.join(here,'corrected-renderer/raw',`${spec.language}-city-error-${side}.html`),'utf8').trim();
  assert.ok(!raw.includes('fixture integrated query error'),side+' search error is not a visible banner');
  const css=side==='original'?originalCss:currentCss;
  const html=`<!doctype html><html lang="${spec.language}" data-theme="${spec.theme}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><style>${css}</style></head><body><main class="app-shell"><div class="app-layout single-profile"><nav class="nav-rail"></nav><section class="main-view">${raw}</section></div></main></body></html>`;
  if(!readOnly)fs.writeFileSync(path.join(out,`${id}-${side}.html`),html);
  const context=await browser.newContext({viewport:{width:spec.width,height:spec.height},deviceScaleFactor:1,timezoneId:'Asia/Singapore'});
  const page=await context.newPage();page.on('pageerror',e=>issues.push(String(e)));page.on('console',m=>{if(['warning','error'].includes(m.type()))issues.push(m.text());});
  await page.setContent(html);await page.evaluate(()=>document.fonts.ready);
  measurements[side]=await page.evaluate(()=>{let p=document.querySelector('.map-panel'),s=document.querySelector('.map-search'),t=document.querySelector('table');let rect=x=>{let r=x.getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height};};return {fonts:document.fonts.status,panel:rect(p),search:rect(s),table:rect(t),alerts:[...document.querySelectorAll('.map-scan-error')].map(e=>e.textContent)};});
  const png=await page.screenshot({fullPage:true});if(!readOnly)fs.writeFileSync(path.join(out,`${id}-${side}.png`),png);screenshots[side]={file:`${id}-${side}.png`,sha256:hash(png)};
  await context.close();
 }
 assert.deepEqual(measurements.current,measurements.original,id+' fresh measured wholepanel geometry');
 assert.equal(screenshots.current.sha256,screenshots.original.sha256,id+' exact unmasked screenshot');
 pairs.push({id,spec,screenshots,measurements,unmaskedExact:true});
}
assert.equal(issues.length,0);
const report={result:'CORRECTED_SEARCH_REJECTION_EXACT',pairs,issues,browser:browser.version(),proof:'Actual original/current production callback execution from75case renderer; original CSS vs production CSS; zero masks.'};if(!readOnly)fs.writeFileSync(path.join(here,'corrected-error-browser.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({result:report.result,pairs:pairs.length,issues},null,2));
}finally{await browser.close();}
