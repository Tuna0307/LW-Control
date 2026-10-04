// Fresh read-only browser measurement of pinned source-rendered HTML, including every descendant.
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const campaign=path.resolve(here,'../..');
const repo=path.resolve(campaign,'../../../..');
const require=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=require('playwright');
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const issues=[];
const results=[];
try {
 for(const [unit,folder] of [['unit-a','browser'],['unit-a','action-message-browser/browser'],['unit-b','browser']]){
  const submitted=JSON.parse(fs.readFileSync(path.join(campaign,unit,folder,'measurements.json'),'utf8'));
  const pairs=Map.groupBy(submitted.records,r=>r.pairId);
  for(const [pairId,records] of pairs){
   const measures={};
   for(const record of records){
    const context=await browser.newContext({viewport:{width:record.viewport.width,height:record.viewport.height},deviceScaleFactor:1,timezoneId:'Asia/Singapore'});
    const page=await context.newPage();
    page.on('pageerror',e=>issues.push({unit,pairId,side:record.side,error:String(e)}));
    page.on('console',msg=>{if(['error','warning'].includes(msg.type()))issues.push({unit,pairId,side:record.side,type:msg.type(),text:msg.text()});});
    await page.setContent(fs.readFileSync(path.join(repo,record.generatedFile),'utf8'));
    await page.evaluate(()=>document.fonts.ready);
    measures[record.side]=await page.evaluate(()=>{
     const root=document.querySelector('.map-panel');
     const rect=element=>{const r=element.getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height};};
     const css=(element,pseudo)=>{const c=getComputedStyle(element,pseudo);return Object.fromEntries(['display','color','backgroundColor','fontFamily','fontSize','fontWeight','lineHeight','opacity','borderColor','borderWidth','boxShadow','padding','margin','gap','content','width','height','position','transform','visibility'].map(k=>[k,c[k]]));};
     const elements=[root,...root.querySelectorAll('*')].map((element,index)=>({index,tag:element.tagName,className:element.getAttribute('class')||'',text:[...element.childNodes].filter(n=>n.nodeType===3).map(n=>n.textContent).join(''),rect:rect(element),style:css(element),before:css(element,'::before'),after:css(element,'::after'),value:element.value,checked:element.checked,disabled:element.disabled}));
     return {fonts:document.fonts.status,elements};
    });
    await context.close();
   }
   const diffs=[];
   const left=measures.original.elements,right=measures.current.elements;
   for(let i=0;i<Math.max(left.length,right.length);i++){
    const a=left[i],b=right[i];
    if(!a||!b){diffs.push({index:i,original:a||null,current:b||null});continue;}
    for(const key of Object.keys(a))if(JSON.stringify(a[key])!==JSON.stringify(b[key]))diffs.push({index:i,tag:a.tag,className:a.className,key,original:a[key],current:b[key]});
   }
   results.push({unit,pairId,originalDescendants:left.length,currentDescendants:right.length,differences:diffs});
  }
 }
 console.log(JSON.stringify({result:'DESCENDANTS_REMEASURED',browserVersion:browser.version(),pairs:results.length,issues,results,limit:'Fresh geometry/computed-style comparison of submitted source-rendered HTML; does not prove live original runtime or independent translation recovery.'},null,2));
}finally{await browser.close();}
