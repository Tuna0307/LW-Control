/* Browser/visual checks against the unmodified extracted frontend.
 * Usage: NODE_PATH=<directory containing playwright> node tools/check_lwbridge_frontend.cjs
 * Reference authentication is synthetic test data in an isolated browser, never
 * a login to the executable or a service. No native commands leave this process.
 */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const {chromium} = require('playwright');
const root = path.resolve(__dirname, '..');
const output = path.join(root, '.codex-live/lwbridge-ui-verification');
const source = path.join(root, 'evidence/lwbridge-0.3.1/frontend');
const candidate = path.join(root, 'src/LWBridge.Desktop/WebUi');
const preview = fs.readFileSync(path.join(candidate, 'preview-host.js'), 'utf8');
const views = ['overview', 'automation', 'map-data', 'march', 'city-layout', 'hotkeys', 'mini-games', 'settings'];
const referenceShim = `
${preview}
let callbackId=0;
window.__TAURI_EVENT_PLUGIN_INTERNALS__={unregisterListener(){}};
window.__TAURI_INTERNALS__={
 transformCallback(){return ++callbackId},
 async invoke(command,args={}) {
  if(command==='plugin:event|listen') return ++callbackId;
  if(command==='plugin:event|unlisten') return null;
  if(command==='auth_state') return {phase:'authorized',username:'Local reference fixture',accessRole:'normal',watermarkTraceCode:'',expiresAt:null,lastHeartbeatAt:null,lockedUntil:null,errorCode:null};
  if(command==='multi_entitlement_get') return {phase:'single',maxProfiles:1};
  if(command==='profile_list') return window.LWBridgePreview.profiles;
  if(command==='profile_instances_reconcile') return {errors:[]};
  return window.LWBridgePreview.invoke(command,args.payload);
 }
};`;

async function main() {
 fs.mkdirSync(output,{recursive:true});
 const server=http.createServer((req,res)=>{
  const url=new URL(req.url,'http://localhost');
  const isReference=url.pathname.startsWith('/reference/');
  const rel=url.pathname.replace(/^\/(reference|candidate)\//,'') || 'index.html';
  const base=isReference?source:candidate;
  const file=path.resolve(base,rel);
  if(!file.startsWith(base+path.sep)){res.writeHead(403);res.end();return;}
  try {
   let data;
   if(isReference && rel==='reference-shim.js') data=referenceShim;
   else {data=fs.readFileSync(file);if(isReference && rel==='index.html') data=data.toString().replace('<script type="module"','<script src="./reference-shim.js"></script><script type="module"');}
   res.setHeader('Content-Type',rel.endsWith('.js')?'text/javascript':rel.endsWith('.css')?'text/css':rel.endsWith('.png')?'image/png':'text/html');
   res.end(data);
  } catch {res.writeHead(404);res.end();}
 }).listen(0,'127.0.0.1');
 await new Promise(resolve=>server.once('listening',resolve));
 const origin=`http://127.0.0.1:${server.address().port}`;
 const browser=await chromium.launch({channel:process.env.LWBRIDGE_BROWSER || 'msedge',headless:true});
 const results=[];
 const errors=[];
 async function open(kind,view,theme,language,width=1120) {
  const page=await browser.newPage({viewport:{width,height:720},deviceScaleFactor:1});
  page.on('pageerror',error=>errors.push(`${kind}/${view}: ${error.stack}`));
  await page.route('**/*',route=>route.request().url().startsWith(origin+'/')?route.continue():route.abort());
  await page.goto(`${origin}/${kind}/index.html?view=${view}&theme=${theme}&language=${language}`);
  await page.locator('.main-view .panel').first().waitFor();
  if(kind==='reference' && view!=='overview') {
   await page.locator('.side-nav button').nth(views.indexOf(view)).click();
   await page.waitForTimeout(150);
  }
  await page.waitForTimeout(350);
  assert.equal(await page.locator('.auth-screen').count(),0);
  assert.equal(await page.locator('.side-nav button').count(),8);
  return page;
 }
 try {
  for(const theme of (process.env.LWBRIDGE_INTERACTIONS_ONLY ? [] : ['light','dark'])) for(const language of ['zh-CN','en']) for(const view of views) {
   const reference=await open('reference',view,theme,language);
   const rebuilt=await open('candidate',view,theme,language);
   const id=`${view}-${theme}-${language}`;
   // Compare the whole feature viewport, excluding the deliberately removed
   // account button in the top bar. This includes original scrolling/clipping.
   const region=await reference.locator('.main-view').boundingBox();
   const actualRegion=await rebuilt.locator('.main-view').boundingBox();
   assert.deepEqual(actualRegion,region,`${id}: feature viewport geometry`);
   await reference.screenshot({path:path.join(output,id+'-reference.png'),clip:region,animations:'disabled'});
   await rebuilt.screenshot({path:path.join(output,id+'-rebuilt.png'),clip:region,animations:'disabled'});
   const rebuiltText=await rebuilt.locator('.main-view').innerText();
   const referenceText=await reference.locator('.main-view').innerText();
   if(view==='map-data') {
    // Maintained differences from immutable 0.3.1: user-owned Normal/Fast
    // controls and Scheduled Plunder remain retired for separate parity work,
    // while Zombie Boss is a dedicated rebuild-only Map Data kind. City Excel
    // is original behavior and must remain visible alongside Search.
    // Remove only the still-documented differences from these disposable
    // comparison pages and compare every remaining visible line strictly.
    assert.equal(await rebuilt.locator('.map-speed-toggle').count(),0,`${id}: retired Manual speed control`);
    assert.equal(await rebuilt.locator('.map-searchbar button').count(),2,`${id}: restored City export remains beside Search on the default City tab`);
    assert.equal((rebuiltText.match(/Zombie Boss/g)||[]).length,2,`${id}: dedicated Zombie Boss scan/result labels`);
    assert.equal(await rebuilt.locator('.map-scheduled-group').count(),0,`${id}: retired Scheduled Plunder result groups`);
    const normalizedReference=await reference.locator('.main-view').evaluate(root=>{
     root.querySelector('.map-speed-toggle')?.remove();
     const resultTabs=root.querySelectorAll('.map-tabs button');
     if(resultTabs.length)resultTabs[resultTabs.length-1].remove();
     return root.innerText;
    });
    const normalizedRebuilt=await rebuilt.locator('.main-view').evaluate(root=>{
     for(const label of root.querySelectorAll('.map-types label'))
      if(label.textContent.trim()==='Zombie Boss')label.remove();
     for(const button of root.querySelectorAll('.map-tabs button'))
      if(button.textContent.includes('Zombie Boss'))button.remove();
     return root.innerText;
    });
    assert.equal(normalizedRebuilt,normalizedReference,`${id}: feature text excluding documented Map Data owner overrides`);
   } else {
    assert.equal(rebuiltText,referenceText,`${id}: feature text`);
   }
   const failures=await rebuilt.evaluate(()=>window.LWBridgePreview.failures);
   assert.deepEqual(failures,[],`${id}: local display state`);
   assert.equal(await rebuilt.locator('.auth-account-button').count(),0);
   results.push({id,region,errors:failures});
   await reference.close();await rebuilt.close();
   console.log(`Compared ${id}`);
  }
  const page=await open('candidate','overview','light','en');
  await page.locator('.theme-toggle').click();
  await page.waitForFunction(()=>document.documentElement.dataset.theme==='dark');
  assert.equal(await page.locator('html').getAttribute('data-theme'),'dark');
  await page.reload();
  await page.locator('.main-view .panel').waitFor();
  // Query theme intentionally controls capture; interactive saves are checked
  // without that override on the next navigation.
  await page.goto(`${origin}/candidate/index.html?view=hotkeys&language=en`);
  await page.locator('.hotkey-switch').first().waitFor();
  const before=await page.locator('.hotkey-switch').first().getAttribute('aria-label');
  await page.locator('.hotkey-switch').first().click();
  await page.waitForTimeout(450);
  assert.notEqual(await page.locator('.hotkey-switch').first().getAttribute('aria-label'),before);
  await page.reload();await page.locator('.hotkey-switch').first().waitFor();
  assert.notEqual(await page.locator('.hotkey-switch').first().getAttribute('aria-label'),before);
  await page.locator('.language-select select').selectOption('zh-CN');
  await page.waitForTimeout(200);
  assert.match(await page.locator('.main-view').innerText(),/游戏快捷键/);
  // Exercise all nested feature tabs by clicking their actual recovered DOM.
  for(const [index,selector] of [[1,'.automation-categories button'],[2,'.map-tabs button'],[3,'.squad-tabs button']]) {
   await page.locator('.side-nav button').nth(index).click();await page.waitForTimeout(350);
   const tabs=page.locator(selector);await tabs.first().waitFor();const count=await tabs.count();
   assert(count>0,`Missing recovered tabs: ${selector}`);
   for(let i=0;i<count;i++) {await tabs.nth(i).click();await page.waitForTimeout(200);assert(await page.locator('.main-view').innerText());}
   results.push({interaction:selector,count});
  }
  assert.deepEqual(await page.evaluate(()=>window.LWBridgePreview.failures),[], 'Nested pages must have complete display state');
  for(const language of ['en','zh-CN','zh-TW','ja','ko','vi','id','ru','pt']) {
   await page.locator('.language-select select').selectOption(language);
   await page.waitForTimeout(150);
   assert(await page.locator('.main-view').innerText());
  }
  const monsterPage=await open('candidate','map-data','dark','en');
  await monsterPage.goto(`${origin}/candidate/index.html?view=map-data&theme=dark&language=en&fixture=monster-locale`);
  await monsterPage.locator('.main-view .panel').first().waitFor();
  const monsterTabs=monsterPage.locator('.map-tabs button');
  await monsterTabs.nth(2).click();
  await monsterPage.waitForFunction(()=>document.body.innerText.includes('Fixture Monster Alpha'));
  const monsterTable=monsterPage.locator('.map-table');
  assert.match(await monsterTable.innerText(),/Fixture Monster Alpha/);
  assert.match(await monsterTable.innerText(),/Fixture Monster Beta/);
  const alphaRow=monsterTable.locator('tbody tr').filter({hasText:'Fixture Monster Alpha'});
  const firstRemaining=await alphaRow.locator('td').nth(3).innerText();
  assert.match(firstRemaining,/^00:0[23]:\d{2}$/,'Monster Remaining must render a live HH:MM:SS zombie shield remainder');
  await monsterPage.waitForTimeout(1200);
  const nextRemaining=await alphaRow.locator('td').nth(3).innerText();
  assert.notEqual(nextRemaining,firstRemaining,'Monster shield countdown must continue ticking locally after capture');
  const levelSelect=monsterPage.locator('.map-searchbar select[aria-label="Level"]');
  assert.deepEqual(await levelSelect.locator('option').evaluateAll(options=>options.map(option=>option.value)),['','5','10'],'Monster level choices must advance in increments of five up to the source-backed observed maximum bucket');
  await levelSelect.selectOption('10');
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===2);
  assert.match(await monsterTable.innerText(),/Fixture Monster Alpha/);
  assert.match(await monsterTable.innerText(),/Fixture Monster Beta/);
  await levelSelect.selectOption('5');
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===1);
  assert.match(await monsterTable.innerText(),/Fixture Monster Alpha/);
  assert.doesNotMatch(await monsterTable.innerText(),/Fixture Monster Beta/);
  assert.equal(await monsterTable.locator('tbody tr').first().locator('td').nth(3).innerText()==='-',false,'level-5 shield row must keep its source-backed Remaining value');
  await levelSelect.selectOption('');
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===2);
  const levelHeader=monsterTable.locator('thead th').filter({hasText:'Level'});
  await levelHeader.click();
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===2);
  assert.equal(await monsterTable.locator('tbody tr').count(),2,'Monster Level sort must not clear the table');
  await levelHeader.click();
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===2);
  const distanceHeader=monsterTable.locator('thead th').filter({hasText:'Distance'});
  await distanceHeader.click();
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===2);
  assert.equal(await monsterTable.locator('tbody tr').count(),2,'Monster Distance sort must not clear the table');
  const searchInput=monsterPage.locator('.map-searchbar input');
  await searchInput.fill('Alpha');
  await monsterPage.locator('.map-searchbar button').filter({hasText:'Search'}).click();
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===1);
  assert.match(await monsterTable.innerText(),/Fixture Monster Alpha/);
  assert.doesNotMatch(await monsterTable.innerText(),/Fixture Monster Beta/);
  await searchInput.fill('');
  await monsterPage.locator('.map-searchbar button').filter({hasText:'Search'}).click();
  await monsterPage.waitForFunction(()=>document.querySelectorAll('.map-table tbody tr').length===2);
  const englishLocaleCalls=await monsterPage.evaluate(()=>window.LWBridgePreview.calls.filter(name=>name==='lastwar_localize').length);
  assert(englishLocaleCalls>0,'Monster table must call lastwar_localize in English');
  await monsterPage.screenshot({path:path.join(output,'monster-locale-en.png'),animations:'disabled'});
  await monsterPage.locator('.language-select select').selectOption('zh-CN');
  await monsterPage.waitForFunction(()=>document.body.innerText.includes('\u624b\u52a8\u626b\u63cf'));
  await monsterPage.locator('.map-tabs button').nth(2).click();
  await monsterPage.waitForFunction(()=>document.body.innerText.includes('测试怪物甲'));
  assert.match(await monsterTable.innerText(),/测试怪物甲/);
  assert.doesNotMatch(await monsterTable.innerText(),/Fixture Monster Alpha/);
  const chineseLocaleCalls=await monsterPage.evaluate(()=>window.LWBridgePreview.calls.filter(name=>name==='lastwar_localize').length);
  assert(chineseLocaleCalls>englishLocaleCalls,'Language change must rerun lastwar_localize');
  assert.deepEqual(await monsterPage.evaluate(()=>window.LWBridgePreview.failures),[], 'Monster locale fixture must stay error-free');
  await monsterPage.screenshot({path:path.join(output,'monster-locale-zh-CN.png'),animations:'disabled'});
  results.push({interaction:'monster-locale-switch-level-search-countdown',englishLocaleCalls,chineseLocaleCalls});
  await monsterPage.close();
  const denied=await page.evaluate(async()=>{
   const result=[];
   for(const name of ['auth_login','auth_activate','auth_logout','profile_instance_start','map_scan_start','call_lua']) {
    try {await window.LWBridgePreview.invoke(name);result.push(false);}catch{result.push(true);}
   }
   return result;
  });
  assert(denied.every(Boolean),'Unimplemented native actions must reject');
  await page.close();
  for(const width of [900,1440]) {
   const p=await open('candidate','map-data','dark','en',width);
   await p.screenshot({path:path.join(output,`responsive-${width}.png`)});
   assert(await p.locator('.main-view').isVisible());await p.close();
  }
  assert.deepEqual(errors,[]);
 } finally {
  fs.writeFileSync(path.join(output,process.env.LWBRIDGE_INTERACTIONS_ONLY?'interactions.json':'report.json'),JSON.stringify({results,errors},null,2));
  await browser.close();server.close();
 }
 console.log(`Passed ${results.length} browser checks. Screenshot pairs: ${output}`);
}
main().catch(error=>{console.error(error);process.exitCode=1;});
