import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const packet=process.argv[2]||'equipment';
assert.ok(['equipment','compact-baseline','compact-current'].includes(packet));
const req=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=req('playwright');
const report={marker:'LWB317_UNIT_D_EQUIPMENT_PAIRED_BROWSER',pairs:[],console:[]};
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const cases=JSON.parse(fs.readFileSync(path.join(here,`${packet}-pair-render.json`),'utf8')).cases;
const shots=path.join(here,packet==='equipment'?'paired-screenshots':`${packet}-paired-screenshots`);fs.mkdirSync(shots,{recursive:true});
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
report.browser=await browser.version();
try {
for(const c of cases)for(const mode of [
 {theme:c.language==='en'?'light':'dark',width:1280,height:900},
 ...((c.narrow||['squads-equipment','squads-equipment-rename'].includes(c.state))?[{theme:c.language==='en'?'dark':'light',width:375,height:1000}]:[])
]) {
 const id=`${c.id}-${mode.theme}-${mode.width}`,pair={id,input:c,viewport:{width:mode.width,height:mode.height},sides:{}};
 for(const side of ['original','current']){
  const context=await browser.newContext({viewport:pair.viewport,reducedMotion:'reduce'}),page=await context.newPage();
  page.on('console',m=>{if(['warning','error'].includes(m.type()))report.console.push({id,side,type:m.type(),text:m.text()});});page.on('pageerror',e=>report.console.push({id,side,type:'pageerror',text:String(e)}));
  try {
   await page.goto(pathToFileURL(path.join(here,packet==='equipment'?'paired-generated':`${packet}-generated`,`${c.id}-${mode.theme}-${side}.html`)).href);await page.evaluate(()=>document.fonts.ready);
   const measured=await page.locator('.squad-panel').evaluate(root=>[root,...root.querySelectorAll('*')].map(e=>{const r=e.getBoundingClientRect(),s=getComputedStyle(e);return {tag:e.tagName,rect:[r.x,r.y,r.width,r.height],text:[...e.childNodes].filter(n=>n.nodeType===3).map(n=>n.textContent).join(''),style:Object.fromEntries(['color','backgroundColor','fontFamily','fontSize','fontWeight','lineHeight','padding','margin','border','boxShadow','display','opacity','transform','gap'].map(k=>[k,s[k]])),before:getComputedStyle(e,'::before').content,after:getComputedStyle(e,'::after').content};}));
   const file=path.join(shots,id+'-'+side+'.png');await page.locator('.squad-panel').screenshot({path:file});
   pair.sides[side]={measured,screenshot:{path:path.relative(here,file).replaceAll('\\','/'),sha256:hash(fs.readFileSync(file))},fonts:await page.evaluate(()=>document.fonts.status)};
  } finally {await context.close();}
 }
 const a=pair.sides.original.measured,b=pair.sides.current.measured;const diffs=[];
 for(let i=0;i<Math.max(a.length,b.length);i++)if(JSON.stringify(a[i])!==JSON.stringify(b[i]))diffs.push({index:i,original:a[i]??null,current:b[i]??null});
 pair.differenceCount=diffs.length;pair.firstDifferences=diffs.slice(0,12);pair.pngBytesEqual=pair.sides.original.screenshot.sha256===pair.sides.current.screenshot.sha256;
 report.pairs.push(pair);
}
} finally {await browser.close();}
assert.equal(report.console.length,0,JSON.stringify(report.console));
fs.writeFileSync(path.join(here,`${packet}-paired-browser.json`),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({marker:report.marker,pairs:report.pairs.length,equalMeasurements:report.pairs.filter(x=>!x.differenceCount).length,equalPng:report.pairs.filter(x=>x.pngBytesEqual).length,console:report.console.length,unmatched:report.pairs.filter(x=>x.differenceCount).map(x=>({id:x.id,differences:x.differenceCount,first:x.firstDifferences[0]}))},null,2));
