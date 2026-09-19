/* PM13-01b browser checks for rebuild-only Map Data feedback. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const {chromium} = require('playwright');

const root = path.resolve(__dirname, '..');
const candidate = path.join(root, 'src/LWBridge.Desktop/WebUi');
const types = ['city','resource','monster','truck','railway','dispatch','ghost','treasure'];
const counts = Object.fromEntries(types.map(key => [key, 0]));
const scanState = {
  serverId:2212, serverIdSource:'pm13-test', scanRunId:'', isReading:false,
  phase:'idle', selectedTypes:types, totalBlocks:0, readBlocks:0, unreadBlocks:0,
  failedBlocks:0, inflightBlocks:0, scanMode:'normal', concurrency:8, retryCount:2,
  scanRate:0, progressPercent:0, nativeCaptureReady:false, nativePendingRecords:0,
  nativeDroppedRecords:0, homeServerId:0, seasonServerIds:[], truckMatchServerIds:[]
};
const injection = `(()=>{
 const mode=new URLSearchParams(location.search).get('pm13Case');
 const original=window.LWBridgePreview.invoke.bind(window.LWBridgePreview);
 const types=${JSON.stringify(types)},counts=${JSON.stringify(counts)},scan=${JSON.stringify(scanState)};
 const status={xluaOnline:true,pending:0,config:{auto_weekend_shield:true,auto_attack_shield:true,auto_force_update_reload:false,auto_close_popup:false,tasks:{}}};
 window.__pm13Commands=[];
 window.LWBridgePreview.invoke=async(command,payload={})=>{
   window.__pm13Commands.push(command);
   if(command==='get_status') return status;
   if(command==='map_scan_status') return mode==='missing'?{...scan,serverId:0,serverIdSource:'none'}:scan;
   if(command==='map_summary') {
     if(mode==='missing') {const e=new Error('no saved context');e.code='MAP_SAVED_CONTEXT_UNAVAILABLE';throw e;}
     if(mode==='ambiguous') {const e=new Error('multiple saved servers');e.code='MAP_SAVED_CONTEXT_AMBIGUOUS';throw e;}
     return {serverId:2212,counts,scanState:scan};
   }
   if(command==='map_data_options') return {serverId:2212,alliances:[],names:{resource:[],monster:[]},dispatchLevels:[],counts,rewardItems:{truck:[],railway:[]},treasureTypes:[],noAllianceCount:0,scanProgress:null};
   if(command==='map_search') {
     if(mode==='query') {const e=new Error('synthetic storage fault');e.code='SQLITE_IOERR';throw e;}
     return {rows:[],total:0};
   }
   if(command==='map_scan_start') {
     const e=new Error('bounded adapter resource only');e.code='LIVE_RESOURCE_TYPES_UNSUPPORTED';throw e;
   }
   if(command==='append_log'||command==='set_window_theme') return null;
   if(command==='lastwar_localize') return {};
   return original(command,payload);
 };
})();`;

async function main(){
 const baseHtml=fs.readFileSync(path.join(candidate,'index.html'),'utf8');
 const injectedHtml=baseHtml.replace('<script type="module"','<script src="./pm13-injection.js"></script>\n    <script type="module"');
 const server=http.createServer((req,res)=>{
   const url=new URL(req.url,'http://localhost');
   const rel=url.pathname.slice(1)||'index.html';
   try {
     if(rel==='pm13-injection.js') {res.setHeader('Content-Type','text/javascript');res.end(injection);return;}
     const file=path.resolve(candidate,rel);
     if(!file.startsWith(candidate+path.sep)&&file!==path.join(candidate,'index.html')) throw new Error('outside root');
     const data=rel==='index.html'?injectedHtml:fs.readFileSync(file);
     res.setHeader('Content-Type',rel.endsWith('.js')?'text/javascript':rel.endsWith('.css')?'text/css':'text/html');
     res.end(data);
   } catch {res.writeHead(404);res.end();}
 }).listen(0,'127.0.0.1');
 await new Promise(r=>server.once('listening',r));
 const origin=`http://127.0.0.1:${server.address().port}`;
 const browser=await chromium.launch({channel:process.env.LWBRIDGE_BROWSER||'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1280,height:900}});
 async function open(mode){
   await page.goto(`${origin}/index.html?view=map-data&language=en&pm13Case=${mode}`);
   await page.locator('.panel.map-panel').waitFor();
   await page.waitForTimeout(250);
 }
 try {
   await open('unsupported');
   const localeCoverage=await page.evaluate(async()=>{
     const m=await import('./assets/index-sfL2sT3K.js');
     const table=m.s;
     const locales=['en','zh-CN','zh-TW','ja','ko','vi','id','ru','pt'];
     const codes=['LIVE_RESOURCE_TYPES_UNSUPPORTED','MAP_SAVED_CONTEXT_UNAVAILABLE','MAP_SAVED_CONTEXT_AMBIGUOUS','MAP_QUERY_FAILED'];
     return locales.every(locale=>codes.every(code=>typeof table?.[locale]?.[`error.${code}`]==='string'&&table[locale][`error.${code}`].length>0));
   });
   assert.equal(localeCoverage,true,'all four PM13 feedback codes must exist in all nine recovered locales');
   const start=page.locator('.map-header .map-actions button.primary').first();
   assert.equal(await start.isDisabled(),false,'Start Scan should be actionable in test state');
   await start.click();
   const unsupported=page.locator('.map-scan-error[role="alert"]');
   await unsupported.waitFor();
   assert.match(await unsupported.innerText(),/supports Resource Point only/i);
   assert(!/could not be completed/i.test(await unsupported.innerText()),'must not use generic action failure');
   assert((await page.evaluate(()=>window.__pm13Commands)).includes('map_scan_start'));

   await open('empty');
   await page.getByRole('button',{name:/Resource/}).first().click();
   await page.getByRole('button',{name:'Search'}).click();
   await page.waitForTimeout(150);
   assert.equal(await page.locator('.map-empty').count(),1,'empty saved data keeps recovered empty state');
   assert.equal(await page.locator('.map-scan-error[role="alert"]').count(),0,'empty data is not a query error');

   await open('missing');
   await page.getByRole('button',{name:/Resource/}).first().click();
   await page.getByRole('button',{name:'Search'}).click();
   const missing=page.locator('.map-scan-error[role="alert"]');
   await missing.waitFor();
   assert.match(await missing.innerText(),/No saved map server is available/i);
   assert(!(await page.evaluate(()=>window.__pm13Commands)).includes('map_search'),'missing context must fail before query');

   await open('query');
   await page.getByRole('button',{name:/Resource/}).first().click();
   await page.getByRole('button',{name:'Search'}).click();
   const query=page.locator('.map-scan-error[role="alert"]');
   await query.waitFor();
   assert.match(await query.innerText(),/Saved map search failed/i);
   assert(!/could not be completed/i.test(await query.innerText()),'query fault must not use generic action failure');
   assert((await page.evaluate(()=>window.__pm13Commands)).includes('map_search'),'query case must reach map_search');

   console.log('PM13-01b resource feedback browser checks passed.');
 } finally {
   await page.close();
   await browser.close();
   server.close();
 }
}

main().catch(error=>{
 console.error(error);
 process.exitCode=1;
});
