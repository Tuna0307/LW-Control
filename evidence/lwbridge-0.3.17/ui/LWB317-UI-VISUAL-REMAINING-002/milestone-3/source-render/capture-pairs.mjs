import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../../..');
const pw=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=pw('playwright');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const phase=process.argv.includes('--baseline')?'baseline':'current';
const dataRoot=phase==='baseline'?path.join(here,'baseline-render'):here;
const input=JSON.parse(fs.readFileSync(path.join(dataRoot,'renderer-inputs.json'),'utf8'));
const out=path.join(here,phase);fs.mkdirSync(out,{recursive:true});
const styles={original:fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css'),'utf8'),current:fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/reference.css'),'utf8')+'\n'+fs.readFileSync(path.join(repo,'src/LWBridge.UI-0.3.17/src/styles.css'),'utf8')};
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const pairs=[];const errors=[];
const variants=[{language:'en',theme:'light',width:1280,height:900},{language:'ja',theme:'dark',width:1280,height:900},{language:'en',theme:'dark',width:375,height:1000},{language:'ja',theme:'light',width:375,height:1000}];
for(const data of input.cases)for(const variant of variants.filter(v=>v.language===data.language)){
 const id=data.id+'-'+variant.theme+'-'+variant.width;const sides={};
 for(const side of ['original','current']){
  const context=await browser.newContext({viewport:{width:variant.width,height:variant.height},locale:data.language,timezoneId:'Asia/Singapore',colorScheme:variant.theme,deviceScaleFactor:1});
  const page=await context.newPage();page.on('console',message=>{if(['warning','error'].includes(message.type()))errors.push({id,side,type:message.type(),text:message.text()});});page.on('pageerror',e=>errors.push({id,side,text:e.message}));
  const raw=fs.readFileSync(path.join(dataRoot,data[side+'Html']),'utf8');
  await page.setContent(`<!doctype html><html lang="${data.language}" data-theme="${variant.theme}"><head><meta charset="utf-8"><style>${styles[side]}</style></head><body><main class="main-view">${raw}</main></body></html>`);
  await page.evaluate(()=>document.fonts.ready);
  const measurement=await page.evaluate(()=>{
   const root=document.querySelector('.panel');
   const props=['display','position','fontFamily','fontSize','fontWeight','lineHeight','color','backgroundColor','borderColor','borderWidth','borderRadius','padding','margin','gap','gridTemplateColumns','flexDirection','alignItems','opacity','cursor'];
   const rect=e=>{const r=e.getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height};};
   const elements=[root,...root.querySelectorAll('*')].map((e,index)=>{const cs=getComputedStyle(e);return {index,tag:e.tagName,cls:e.className?.baseVal??e.className,text:[...e.childNodes].filter(n=>n.nodeType===3).map(n=>n.textContent).join(''),attrs:[...e.attributes].map(a=>[a.name,a.value]),rect:rect(e),visible:!!(e.getClientRects().length&&cs.display!=='none'),styles:Object.fromEntries(props.map(k=>[k,cs[k]])),pseudo:Object.fromEntries(['::before','::after'].map(p=>{const s=getComputedStyle(e,p);return[p,{content:s.content,color:s.color,width:s.width,height:s.height,display:s.display}]}))};});
   const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT),textRuns=[];let n;while(n=walker.nextNode()){if(!n.textContent.trim())continue;const r=document.createRange();r.selectNodeContents(n);const box=r.getBoundingClientRect();if(!box.width&&!box.height)continue;const s=getComputedStyle(n.parentElement);textRuns.push({text:n.textContent,rect:{x:box.x,y:box.y,width:box.width,height:box.height},color:s.color,fontSize:s.fontSize,fontFamily:s.fontFamily,fontWeight:s.fontWeight});}
   const cards=[...root.querySelectorAll('article.automation-card')].map((e,i)=>({index:i,title:e.querySelector('h3')?.textContent,rect:rect(e),triggerCount:e.querySelectorAll('.automation-config-trigger').length,triggerGlyphs:e.querySelector('.automation-config-trigger')?.querySelectorAll('svg').length??0,bodyVisible:[...e.querySelectorAll('.automation-config-body')].some(b=>!!b.getClientRects().length),controls:[...e.querySelectorAll('input,select,textarea,button')].map(c=>({tag:c.tagName,text:c.textContent,value:c.value,checked:c.checked,disabled:c.disabled,rect:rect(c)}))}));
   return {viewport:{width:innerWidth,height:innerHeight},fonts:document.fonts.status,html:root.outerHTML,elements,textRuns,cards};
  });
  const screenshot=id+'-'+side+'.png';await page.screenshot({path:path.join(out,screenshot),fullPage:true});
  fs.writeFileSync(path.join(out,id+'-'+side+'.json'),JSON.stringify(measurement,null,2)+'\n');
  fs.writeFileSync(path.join(out,id+'-'+side+'.html'),raw);
  sides[side]={measurement:id+'-'+side+'.json',screenshot,sha256:hash(fs.readFileSync(path.join(out,screenshot))),rawSha256:hash(raw),cardCount:measurement.cards.length};await context.close();
 }
 pairs.push({id,category:data.category,expanded:data.expanded,...variant,...sides,masks:[]});
}
await browser.close();
fs.writeFileSync(path.join(out,'pairs.json'),JSON.stringify({pairs,errors,tool:{playwright:pw('playwright/package.json').version,node:process.version},scope:'Actual original/current scoped offline category renderers. Equal declared main-view host. Independent original/current CSS with no compatibility overlay. No pixel masks. Original asset-complete runtime is unavailable.'},null,2)+'\n');
console.log(JSON.stringify({pairs:pairs.length,consoleIssues:errors.length,out}));
