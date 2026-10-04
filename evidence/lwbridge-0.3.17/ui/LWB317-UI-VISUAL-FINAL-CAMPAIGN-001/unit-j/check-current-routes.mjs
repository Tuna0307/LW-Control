// Current-app integration smoke, not a reference visual acceptance test.
import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';import {createRequire} from 'node:module';import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../..');
const require=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');const {chromium}=require('playwright');const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const routes=[['nav.overview','.quick-actions-panel'],['nav.automation','.automation-categories'],['nav.mapData','.map-panel'],['nav.squads','.squad-panel'],['nav.cityLayout','.city-layout-empty'],['nav.hotkeys','.hotkey-panel'],['nav.miniGames','.hotkey-panel'],['nav.settings','.settings-panel']];
const results=[];
try{for(const [language,theme,width]of [['en','light',1280],['ja','dark',1280],['en','dark',375],['ja','light',375]]){
 const catalog=(await import(pathToFileURL(path.join(repo,`src/LWBridge.UI-0.3.17/src/locales/${language}.js`)))).default;
 const context=await browser.newContext({viewport:{width,height:1000},locale:language,timezoneId:'Asia/Singapore'});const page=await context.newPage(),issues=[];page.on('console',m=>{if(['warning','error'].includes(m.type()))issues.push({type:m.type(),text:m.text()});});page.on('pageerror',e=>issues.push({type:'pageerror',text:e.message}));
 await page.goto(`http://127.0.0.1:4350/?previewPage=overview&previewLanguage=${language}&previewTheme=${theme}`);await page.locator('.quick-actions-panel').waitFor();const steps=[];
 for(const [key,selector]of [...routes,...routes.slice().reverse()]){
  const label=catalog[key];assert.ok(label,key);await page.locator('.side-nav button').filter({hasText:label}).click();await page.locator(selector+':visible').waitFor();await page.evaluate(()=>document.fonts.ready);
  const metric=await page.evaluate(()=>({width:innerWidth,fonts:document.fonts.status,theme:document.documentElement.dataset.theme,language:document.documentElement.lang}));assert.equal(metric.width,width);assert.equal(metric.fonts,'loaded');assert.equal(metric.theme,theme);
  steps.push({key,selector,metric});
 }
 assert.deepEqual(issues,[]);results.push({language,theme,width,steps,issues});await context.close();
}}
finally{await browser.close();}
fs.writeFileSync(path.join(here,'current-routes.json'),JSON.stringify({routes:8,transitions:64,modes:results,limits:'Canonical current-app navigation smoke only. No game/native/provider/updater actions, no original visual oracle, no profile-replacement or modal-boundary blanket acceptance.'},null,2)+'\n');console.log('CURRENT_APP_ROUTE_SMOKE_OK routes=8 transitions=64 console=0');
