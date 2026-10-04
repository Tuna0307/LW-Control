import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const req=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=req('playwright');
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const results=[],consoleIssues=[];
const record=(name,actual,expected)=>{assert.deepEqual(actual,expected,name);results.push({name,actual,expected});};
const modes=[{language:'en',theme:'light',width:1280},{language:'ja',theme:'dark',width:1280},{language:'en',theme:'dark',width:375},{language:'ja',theme:'light',width:375}];
for(const mode of modes){
 const context=await browser.newContext({viewport:{width:mode.width,height:1000},timezoneId:'Asia/Singapore'});const page=await context.newPage();page.on('console',m=>{if(['warning','error'].includes(m.type()))consoleIssues.push({mode,text:m.text()});});page.on('pageerror',e=>consoleIssues.push({mode,text:e.message}));
 await page.goto(`http://127.0.0.1:4350/?previewPage=automation&previewLanguage=${mode.language}&previewTheme=${mode.theme}`);await page.locator('.automation-categories').waitFor();
 for(const [index,name,count]of [[0,'daily',7],[1,'alliance',8],[2,'gather',1],[3,'resources',2],[4,'chat',3],[5,'trade',1],[6,'system',2]]){
  await page.locator('.automation-categories > button').nth(index).click();const cards=page.locator('.automation-grid article.automation-card:visible');record(`${mode.language}/${mode.theme}/${mode.width} ${name} cards`,await cards.count(),count);
  const triggers=cards.locator('.automation-config-trigger');for(let i=0;i<await triggers.count();i++){
   const trigger=triggers.nth(i);const state=await trigger.getAttribute('aria-expanded');record(`${name}/${i} offline expansion is local`,await trigger.isEnabled(),true);await trigger.click();record(`${name}/${i} expands`,await trigger.getAttribute('aria-expanded'),state==='true'?'false':'true');await trigger.click();record(`${name}/${i} expansion roundtrip`,await trigger.getAttribute('aria-expanded'),state);
  }
  record(`${name} native run actions fenced`,await cards.locator('.automation-run-action').evaluateAll(items=>items.every(e=>e.disabled)),true);
 }
 await context.close();
}
const context=await browser.newContext({viewport:{width:1280,height:1000},timezoneId:'Asia/Singapore'}),page=await context.newPage();page.on('console',m=>{if(['warning','error'].includes(m.type()))consoleIssues.push({text:m.text()});});page.on('pageerror',e=>consoleIssues.push({text:e.message}));
await page.goto('http://127.0.0.1:4350/?previewPage=automation&previewState=automation-trade-positive&previewLanguage=en&previewTheme=light');await page.locator('.automation-categories').waitFor();
await page.locator('.automation-categories > button').nth(5).click();const trade=page.locator('.trade-station-panel');record('Trade starts collapsed',await trade.locator('.automation-config-trigger').getAttribute('aria-expanded'),'false');record('Trade fieldset initially hidden',await trade.locator('.automation-config-body').isVisible(),false);await trade.locator('.automation-config-trigger').click();record('Trade expands actual production state',await trade.locator('.automation-config-body').isVisible(),true);
const goods=trade.locator('.trade-station-good');await goods.nth(1).click();record('Trade edit retained before collapse',await goods.nth(1).locator('input').isChecked(),true);await trade.locator('.automation-config-trigger').click();await trade.locator('.automation-config-trigger').click();record('Trade edit retained across collapse',await goods.nth(1).locator('input').isChecked(),true);
await trade.locator('.trade-station-tabs button').nth(1).click();record('Purchase tab actual retained state',await trade.locator('.trade-station-tabs button').nth(1).getAttribute('aria-selected'),'true');await page.locator('.automation-categories > button').nth(0).click();await page.locator('.automation-categories > button').nth(5).click();record('Trade expanded retained across category',await trade.locator('.automation-config-trigger').getAttribute('aria-expanded'),'true');record('Trade purchase selection retained across category',await trade.locator('.trade-station-tabs button').nth(1).getAttribute('aria-selected'),'true');
await page.locator('.automation-categories > button').nth(4).click();const chat=page.locator('.automation-grid article.automation-card:visible');await chat.first().locator('.automation-config-trigger').click();const reply=chat.first().getByRole('switch',{name:'Auto Reply:'});await reply.click();record('Recovered Reply switch reveals nested form',await chat.first().locator('textarea').isVisible(),true);await chat.first().locator('textarea').fill('Fixture reply');await reply.click();await reply.click();record('Reply text retained across local toggles',await chat.first().locator('textarea').inputValue(),'Fixture reply');
await chat.nth(2).locator('.automation-config-trigger').click();const dispatch=chat.nth(2).getByRole('switch',{name:/Automatically dispatch/});await dispatch.click();record('Recovered Treasure dispatch reveals priority form',await chat.nth(2).locator('.automation-squad-choices').isVisible(),true);
record('All native Run buttons remain disabled',await page.locator('.automation-run-action').evaluateAll(items=>items.every(e=>e.disabled)),true);
fs.mkdirSync(path.join(here,'mounted'),{recursive:true});await page.screenshot({path:path.join(here,'mounted','nested-chat-en-light.png'),fullPage:true});await context.close();await browser.close();
assert.equal(consoleIssues.length,0);fs.writeFileSync(path.join(here,'mounted-results.json'),JSON.stringify({results,consoleIssues,assertions:results.length,limits:'Actual canonical React + local preview config callbacks. No gameplay/native action invoked. Offline expansion checked in four real locale/theme/viewport combinations; inert edit/retention states in English light.'},null,2)+'\n');console.log(JSON.stringify({assertions:results.length,consoleIssues:consoleIssues.length}));
