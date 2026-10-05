import fs from 'node:fs';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const {chromium}=createRequire('C:/Users/chimw/AppData/Local/Temp/lwbridge-playwright/package.json')('playwright');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
const context=await browser.newContext({viewport:{width:1280,height:1000},reducedMotion:'reduce'});
const page=await context.newPage();const issues=[];page.on('pageerror',e=>issues.push(String(e)));page.on('console',m=>{if(['error','warning'].includes(m.type()))issues.push(m.text())});
try{
await page.goto('http://127.0.0.1:4441/?previewPage=march&previewState=squads-equipment&previewLanguage=en&previewTheme=light');
await page.locator('.equipment-preset-layout').waitFor();
await page.evaluate(async()=>{
const src=await(await fetch('/src/main.jsx')).text(); const reactURL=src.match(/from "([^\"]+react\.js[^\"]*)"/)[1];const domURL=src.match(/from "([^\"]+react-dom_client\.js[^\"]*)"/)[1];
const rm=await import(reactURL),dm=await import(domURL);const R=rm.default,D=dm.default;const {I18nProvider}=await import('/src/i18n.jsx'),{SquadsPage}=await import('/src/SquadsPage.jsx'),{usePreviewConfigAdapter}=await import('/src/previewConfigHook.jsx');
document.getElementById('root').style.display='none';const host=document.createElement('div');host.id='independent-equipment';document.body.append(host);const root=D.createRoot(host);
window.renderOwner=(owner,state='squads-equipment')=>root.render(R.createElement(I18nProvider,null,R.createElement(SquadsPage,{key:owner,profileId:owner,previewState:state,activeTab:'equipment'})));
window.renderOwner('A');
window.captureEquipmentStore=async()=>{
 const probeHost=document.createElement('div');document.body.append(probeHost);const probeRoot=D.createRoot(probeHost);
 function Probe(){const state=usePreviewConfigAdapter([],{valid:()=>true,read:async()=>[],write:async v=>v},JSON.stringify(['A','equipment:squads-equipment']));window.equipmentStore=state.store;return null;}
 probeRoot.render(R.createElement(Probe));await new Promise(r=>setTimeout(r,50));probeRoot.unmount();probeHost.remove();
 let release;const gate=new Promise(r=>release=r);window.releaseEquipmentSave=()=>release();
 window.equipmentStore.setAdapter({valid:()=>true,read:async()=>window.equipmentStore.getSnapshot().confirmed,write:async draft=>{await gate;return draft}});
};
});
const panel=page.locator('#independent-equipment');await panel.locator('.equipment-preset-layout').waitFor();
await page.evaluate(()=>window.captureEquipmentStore());
await panel.getByRole('button',{name:/^Rename$/}).click();const dialog=panel.locator('dialog');await dialog.locator('input').fill('Pending A owned');await dialog.locator('button.primary').click();
await page.waitForFunction(()=>window.equipmentStore.getSnapshot().saving);
const pending=await page.evaluate(()=>structuredClone(window.equipmentStore.getSnapshot()));
await page.evaluate(()=>window.renderOwner('B'));await panel.locator('.equipment-preset-layout').waitFor();assert.equal((await panel.locator('.equipment-preset-list button').first().innerText()).includes('Pending A owned'),false);
await page.evaluate(()=>window.renderOwner('A'));await panel.locator('.equipment-preset-layout').waitFor();assert.equal((await panel.locator('.equipment-preset-list button').first().innerText()).includes('Pending A owned'),true);
// Physical UI dragging is not claimed; execute actual rendered React HTML5 handlers.
await panel.locator('.equipment-loadout-handle').first().evaluate(node=>{const props=node[Object.keys(node).find(k=>k.startsWith('__reactProps$'))];props.onDragStart();});
await panel.locator('.equipment-position-card').nth(1).evaluate(node=>{const props=node[Object.keys(node).find(k=>k.startsWith('__reactProps$'))];props.onDrop({preventDefault(){}});});
const edited=await page.evaluate(()=>structuredClone(window.equipmentStore.getSnapshot()));
assert.notDeepEqual(edited.draft,pending.draft,'actual drop must produce a second edit while retained store saving');
await page.evaluate(()=>window.releaseEquipmentSave());await page.waitForFunction(()=>!window.equipmentStore.getSnapshot().saving);
const settled=await page.evaluate(()=>structuredClone(window.equipmentStore.getSnapshot()));
const repo=path.resolve(here,'../../../../../..');
const requireUi=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));const parser=requireUi('@babel/parser');
const main=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js'),'utf8');const squad=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js'),'utf8');
const Tnode=parser.parse(main,{sourceType:'module'}).program.body.find(n=>n.type==='FunctionDeclaration'&&n.id.name==='T');
const fd=parser.parse(squad,{sourceType:'module'}).program.body.find(n=>n.type==='FunctionDeclaration'&&n.id.name==='fd');const Pnode=fd.body.body.find(n=>n.type==='FunctionDeclaration'&&n.id.name==='P');
const OriginalT=new Function('w',`return (${main.slice(Tnode.start,Tnode.end)});`)(JSON.stringify);
let originalRelease;const originalGate=new Promise(r=>originalRelease=r);const originalStore=OriginalT(structuredClone(pending.confirmed),{valid:()=>true,read:async()=>pending.confirmed,write:async draft=>{await originalGate;return draft}});
const OriginalP=new Function('u',`return (${squad.slice(Pnode.start,Pnode.end)});`)({state:originalStore});
const originalPending=OriginalP(pending.draft);originalStore.edit(edited.draft,false);originalRelease();await originalPending;
const originalSettled=originalStore.getSnapshot();assert.equal(originalSettled.dirty,true);assert.equal(settled.dirty,false);
const original={PByte:Buffer.byteLength(squad.slice(0,Pnode.start)),PSource:squad.slice(Pnode.start,Pnode.end),dirty:originalSettled.dirty,confirmedMatchesFirstDraft:JSON.stringify(originalSettled.confirmed)===JSON.stringify(pending.draft)};
assert.deepEqual(issues,[]);
const result={pending,edited,settled,original,issues,limits:'Isolated mounted actual SquadsPage and actual rendered rename/drop callbacks. Local controlled deferred store adapter; no native/gameplay or physical HTML5 drag claim.'};fs.writeFileSync(path.join(here,'equipment-owner-adversarial-results.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({beforeDirty:edited.dirty,afterDirty:settled.dirty,confirmedMatchesSecondDraft:JSON.stringify(settled.confirmed)===JSON.stringify(edited.draft),original}));
}finally{await context.close();await browser.close();}
