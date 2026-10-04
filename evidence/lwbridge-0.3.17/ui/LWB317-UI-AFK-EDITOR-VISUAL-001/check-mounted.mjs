import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {here,hash} from './harness.mjs';
const req=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json');
const {chromium}=req('playwright');
const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const result={cases:[],consoleIssues:[],screenshots:[],scope:'Mounted canonical offline preview controls only. No native/gameplay actions.'};
function equal(id,actual,expected){assert.deepEqual(actual,expected,id);result.cases.push({id,actual,expected});}
for(const v of [{language:'en',theme:'light',width:1280,height:900},{language:'ja',theme:'dark',width:375,height:1000}]){
 const context=await browser.newContext({viewport:{width:v.width,height:v.height}});const page=await context.newPage();
 page.on('console',m=>{if(['error','warning'].includes(m.type()))result.consoleIssues.push({language:v.language,text:m.text()});});page.on('pageerror',e=>result.consoleIssues.push({language:v.language,text:e.message}));
 await page.goto(`http://127.0.0.1:4370/?previewPage=march&previewState=squads-profile-positive&previewLanguage=${v.language}&previewTheme=${v.theme}`,{waitUntil:'networkidle'});
 const editor=page.locator('.monster-afk-editor:visible');await editor.waitFor();
 equal('section headings '+v.language,await editor.locator(':scope > section > h3').count(),3);
 const name=editor.locator('.monster-afk-basic-grid').first().locator('input');
 await name.fill('Lead editor checkpoint');await name.press('Enter');equal('name edit '+v.language,await name.inputValue(),'Lead editor checkpoint');
 const level=editor.locator('.monster-afk-filter').nth(0),distance=editor.locator('.monster-afk-filter').nth(1);
 await level.locator('input[type=checkbox]').first().check();
 await level.locator('input[type=number]').nth(0).fill('11');await level.locator('input[type=number]').nth(1).fill('12');
 equal('range warning '+v.language,await editor.locator('.monster-afk-attackable-range.is-invalid strong').count(),1);
 await level.locator('input[type=checkbox]').nth(1).check();equal('progressive hides max '+v.language,await level.locator('input[type=number]').count(),1);
 await level.locator('input[type=number]').fill('4');equal('valid progressive range '+v.language,await editor.locator('.monster-afk-attackable-range.is-invalid').count(),0);
 await distance.locator('input[type=checkbox]').check();await distance.locator('input[type=number]').fill('77');
 await editor.locator('.monster-afk-squad-toggle').nth(2).click();equal('squad 3 '+v.language,await editor.locator('.monster-afk-squad-toggle').nth(2).getAttribute('aria-pressed'),'true');
 await editor.locator('.monster-afk-custom-target input[type=checkbox]').check();equal('empty custom validation '+v.language,await editor.locator('.monster-afk-custom-name input').getAttribute('aria-invalid'),'true');
 await editor.locator('.monster-afk-custom-name input').fill('Fixture target');equal('valid custom name '+v.language,await editor.locator('.monster-afk-custom-name input').getAttribute('aria-invalid'),'false');
 await editor.locator('.monster-afk-custom-target input[type=checkbox]').uncheck();equal('target restored '+v.language,await editor.locator('select').first().inputValue(),'steel');
 await page.locator('.squad-tabs [role=tab]').nth(1).click();equal('hidden editor '+v.language,await page.locator('.monster-afk-editor:visible').count(),0);
 await page.locator('.squad-tabs [role=tab]').nth(0).click();await editor.waitFor();
 equal('retained name '+v.language,await name.inputValue(),'Lead editor checkpoint');
 equal('retained distance '+v.language,await distance.locator('input[type=number]').inputValue(),'77');
 equal('retained squad '+v.language,await editor.locator('.monster-afk-squad-toggle').nth(2).getAttribute('aria-pressed'),'true');
 equal('retained progressive '+v.language,await level.locator('input[type=checkbox]').nth(1).isChecked(),true);
 const screenshot=`mounted-${v.language}-${v.width}.png`;await page.screenshot({path:path.join(here,screenshot),fullPage:true});result.screenshots.push({screenshot,sha256:hash(fs.readFileSync(path.join(here,screenshot))),viewport:await page.evaluate(()=>[innerWidth,innerHeight]),documentWidth:await page.evaluate(()=>document.documentElement.scrollWidth)});
 await context.close();
}
await browser.close();assert.equal(result.consoleIssues.length,0);fs.writeFileSync(path.join(here,'mounted-results.json'),JSON.stringify(result,null,2)+'\n');console.log('LWB317_AFK_EDITOR_MOUNTED_OK '+JSON.stringify({cases:result.cases.length,consoleIssues:result.consoleIssues.length}));
