import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {compile, flatten, read, h, hooks, jsx, Fragment, nodes, raw} from '../LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/harness.mjs';
import * as c from '../../../../src/LWBridge.UI-0.3.17/src/previewRemainingPagesContracts.js';
import en from '../../../../src/LWBridge.UI-0.3.17/src/locales/en.js';
import ja from '../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const pages = read('src/LWBridge.UI-0.3.17/src/Pages.jsx');
const citySource = read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js');
const settingsSource = read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SettingsPanel-DqxIWv_E.js');
const hotkeySource = read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js');
const text = tree => tree == null || typeof tree === 'boolean' ? '' : Array.isArray(tree) ? tree.map(text).join('') : typeof tree === 'object' ? text(tree.props?.children) : String(tree);
const tFor = catalog => (key, vars={}) => String(catalog[key] ?? key).replace(/\{(\w+)\}/g, (m, k) => String(vars[k] ?? m));
const settle = () => new Promise(resolve => setImmediate(resolve));
const stateSet = name => new Function(`return (${raw(pages, nodes(pages).find(n=>n.type==='VariableDeclarator' && n.id.name===name).init)});`)();
const fakeWindow = {getComputedStyle:()=>({paddingLeft:'0',paddingTop:'0'}),setInterval:()=>1,clearInterval(){}};
const results = [];
const add = (name, original, current, proof) => results.push({name, original, current, proof, decision:'CHANGES_REQUIRED'});

// Native/default page mode has no preview state or provider. Invoke actual callbacks inertly.
for (const [mode,online] of [['native',true],['native-unavailable',false]]) {
  const hook = hooks();
  const C = compile(pages,'RecoveredHotkeyPanel',{h,Fragment,...hook,...c,useI18n:()=>({t:tFor(en)}),MINI_GAMES_PREVIEW_STATES:stateSet('MINI_GAMES_PREVIEW_STATES'),HotkeyCard:'Card',ToggleRow:'ToggleRow',PanelTitle:'PanelTitle',window:fakeWindow});
  const render = () => {hook.begin();return C({category:'miniGames',previewState:'',online,bridgeMode:mode,backendAvailable:mode==='native'});};
  let tree=render();
  const land=flatten(tree).find(n=>n.type==='button' && text(n)===en['miniGames.landCellAction']);
  const sheep=flatten(tree).find(n=>n.type==='button' && text(n)===en['common.start']);
  assert.equal(tree.props['data-preview-fixture'],undefined);
  assert.equal(land.props.disabled,!online);
  assert.equal(sheep.props.disabled,!online);
  assert.ok(text(tree).includes(tFor(en)('miniGames.sheep.level',{level:4})));
  land.props.onClick(); await settle(); tree=render();
  assert.equal(text(tree).includes(tFor(en)('miniGames.landCellSent',{id:17})),online);
  sheep.props.onClick(); await settle(); render();
  add(`Mini Games ${mode} has unavailable actions and synthetic state`,{providerAvailable:false,syntheticLevel:null,success:false},{landEnabled:online,sheepEnabled:online,syntheticLevel:4,landSuccessCellId:online?17:null},'Actual current renderer/callback; no provider is invoked. Original z/B await supplied providers and Land uses returned cellId.');
}

for (const mode of ['native','native-unavailable']) {
  const hook=hooks();
  const C=compile(pages,'SettingsPage',{h,Fragment,...hook,...c,useI18n:()=>({language:'en',t:tFor(en)}),SETTINGS_PREVIEW_STATES:stateSet('SETTINGS_PREVIEW_STATES'),initialFeedbackState:compile(pages,'initialFeedbackState'),PanelTitle:'PanelTitle',ToggleRow:'ToggleRow',window:fakeWindow});
  const render=()=>{hook.begin();return C({previewState:'',bridgeMode:mode,backendAvailable:mode==='native',online:false});};
  let tree=render();
  const exportButton=flatten(tree).find(n=>n.type==='button'&&text(n)===en['feedback.export']);
  const checkButton=flatten(tree).find(n=>n.type==='button'&&text(n)===en['update.check']);
  assert.equal(exportButton.props.disabled,false); assert.equal(checkButton.props.disabled,false);
  exportButton.props.onClick();await settle();render();
  assert.equal(hook.values[5].phase,'success');
  assert.equal(hook.values[5].result.path,'Preview/diagnostics.zip');
  checkButton.props.onClick();await settle();render();
  assert.equal(hook.values[6].phase,'upToDate');
  add(`Settings ${mode} fabricated export/check acknowledgement`,{providerAvailable:false,archive:null,checkSuccess:false},{exportEnabled:true,checkEnabled:true,archive:hook.values[5].result,phase:hook.values[6].phase},'Actual current callbacks; original Settings.p and Index.Rn require provider acknowledgements.');
}
const defaultUpdate=c.previewUpdateStatus('');
assert.equal(defaultUpdate.latestVersion,'0.3.17');assert.equal(defaultUpdate.downloadDirectory,'Preview/updates');
add('Settings initial updater metadata',{currentVersion:'',latestVersion:null,downloadDirectory:''},{currentVersion:defaultUpdate.currentVersion,latestVersion:defaultUpdate.latestVersion,downloadDirectory:defaultUpdate.downloadDirectory},'Exact original Ln byte210230 length158 versus current initial helper.');

function cityRunner(original,state='city-layout-populated',placements=[]) {
  const fixture=c.previewCityLayoutFixture();
  const hook=hooks(original?[fixture,{past:[],present:placements,future:[]},24,'',undefined,fixture.buildings[0].uuid,[fixture.buildings[0].uuid],{},6,fixture.layoutRevision,false,false,'',null,null,false]:[]);
  const env={window:fakeWindow,document:{activeElement:null}};
  let C;
  if(original) {
    for(const name of ['te','ne','m','re','ie','ae','oe','le','g'])env[name]=compile(citySource,name,env);
    Object.assign(env,{p:{...hook,useCallback:f=>f},h:jsx,s:()=>({t:key=>key,language:'en'}),se:{past:[],present:[],future:[]},ce:24,ee:()=>null,f:()=>new Promise(()=>{})});
    C=compile(citySource,'ue',env);
  } else {
    Object.assign(env,{h,Fragment,...hook,...c,useI18n:()=>({t:key=>key}),CITY_LAYOUT_PREVIEW_STATES:stateSet('CITY_LAYOUT_PREVIEW_STATES')});
    C=compile(pages,'CityLayoutPage',env);
  }
  const render=()=>{hook.begin();const tree=C(original?{profileId:'inert',online:true,onLog(){}}:{previewState:state,online:false});const grid=flatten(tree).find(n=>/^city-layout-grid(?: dragging)?$/.test(n.props?.className));if(grid)grid.props.ref.current={getBoundingClientRect:()=>({left:0,top:0})};return tree;};
  return {hook,render};
}
for(const state of ['city-layout-loading','city-layout-error']) {
  const original=cityRunner(true);original.hook.values[0]=null;original.hook.values[11]=state.endsWith('loading');original.hook.values[12]=state.endsWith('error')?'controlled-error':'';
  const old=original.render(), current=cityRunner(false,state).render();
  const hasWorkbench=tree=>flatten(tree).some(n=>n.props?.className==='city-layout-workbench');
  assert.equal(hasWorkbench(old),false);assert.equal(hasWorkbench(current),true);
  add(state,{workbench:false},{workbench:true},'Actual original/current null-layout/loading/error renderer.');
}
function drag(original,name,down,move) {
  const runner=cityRunner(original);let tree=runner.render();
  const button=flatten(tree).find(n=>n.type==='button'&&n.props.title?.startsWith(name));
  button.props.onPointerDown({clientX:down[0],clientY:down[1],pointerId:1,ctrlKey:false,shiftKey:false,stopPropagation(){},currentTarget:{setPointerCapture(){}}});
  tree=runner.render();let grid=flatten(tree).find(n=>n.props?.className==='city-layout-grid dragging');
  grid.props.onPointerMove({clientX:move[0],clientY:move[1]});
  tree=runner.render();grid=flatten(tree).find(n=>n.props?.className==='city-layout-grid dragging');grid.props.onPointerUp();runner.render();
  return runner.hook.values[original?1:0].present;
}
const oldOffset=drag(true,'Hospital',[84,108],[84,108]);
const newOffset=drag(false,'Hospital',[84,108],[84,108]);
assert.deepEqual(oldOffset,[]);assert.deepEqual(newOffset,[{uuid:'preview-hospital',targetPointId:404}]);
add('City non-origin grab with zero pointer movement',oldOffset,newOffset,'Actual original/current pointerdown/move/up callbacks.');
const oldFallback=drag(true,'Barracks',[108,36],[180,84]);
const newFallback=drag(false,'Barracks',[108,36],[180,84]);
assert.deepEqual(oldFallback,[{uuid:'preview-barracks',targetPointId:505}]);assert.deepEqual(newFallback,[]);
add('City invalid target valid-axis fallback',oldFallback,newFallback,'Actual original/current pointer callbacks.');

const oldApply=cityRunner(true,'city-layout-populated',[{uuid:'preview-hospital',targetPointId:405}]);
oldApply.hook.values[1].past=[[]];
const currentApply=cityRunner(false,'city-layout-populated-moved');currentApply.render();currentApply.hook.values[0].past=[[]];
for(const runner of [oldApply,currentApply]) {
  const tree=runner.render();flatten(tree).find(n=>n.type==='button'&&text(n)==='cityLayout.apply').props.onClick();
}
const oldApplyTree=oldApply.render(), currentApplyTree=currentApply.render();
const oldUndo=flatten(oldApplyTree).find(n=>n.type==='button'&&text(n)==='cityLayout.undo');
const currentUndo=flatten(currentApplyTree).find(n=>n.type==='button'&&text(n)==='cityLayout.undo');
assert.equal(oldUndo.props.disabled,true);assert.equal(currentUndo.props.disabled,false);
add('City Apply preparation busy gate',{undoDisabled:true},{undoDisabled:false},'Original Apply awaits an inert pending validation; current opens its local confirmation without entering the source busy gate. No native confirmation or apply operation is invoked.');

// Error copy is stored at event time in the original, rather than retranslated on render.
for(const page of ['Settings','Hotkeys']) {
  let locale='en';const currentT=()=>tFor(locale==='en'?en:ja);
  const old=hooks(page==='Settings'?[{showFps:false,showPing:false},false,'']:[c.previewHotkeyConfig(),null,false,'',false,'',Date.now(),'']);
  const now=hooks();let original,current;
  if(page==='Settings') {
    original=compile(settingsSource,'m',{h:jsx,d:jsx,u:old,o:()=>({t:currentT()}),c:'ToggleRow',p:'Feedback',s:'Updater',r:()=>Promise.resolve({}),l:()=>Promise.reject(Error('controlled failure'))});
    current=compile(pages,'SettingsPage',{h,Fragment,...now,...c,useI18n:()=>({language:locale,t:currentT()}),SETTINGS_PREVIEW_STATES:stateSet('SETTINGS_PREVIEW_STATES'),initialFeedbackState:compile(pages,'initialFeedbackState'),PanelTitle:'PanelTitle',ToggleRow:'ToggleRow',window:fakeWindow});
  } else {
    original=compile(hotkeySource,'_',{f:jsx,d:old,i:()=>({t:currentT()}),p:c.HOTKEY_CARDS,m:c.MINI_GAME_HOTKEY_CARDS,l:'Switch',r:()=>Promise.resolve(c.previewHotkeyConfig()),c:()=>Promise.reject(Error('controlled failure')),h:c.sheepStatusKey,g:c.formatSheepElapsed,window:fakeWindow});
    current=compile(pages,'RecoveredHotkeyPanel',{h,Fragment,...now,...c,useI18n:()=>({t:currentT()}),MINI_GAMES_PREVIEW_STATES:stateSet('MINI_GAMES_PREVIEW_STATES'),HotkeyCard:'Card',ToggleRow:'ToggleRow',PanelTitle:'PanelTitle',window:fakeWindow});
  }
  const oldRender=()=>{old.begin();return original({category:'hotkeys',online:false,onLog(){},showProfileFocus:false});};
  const newRender=()=>{now.begin();return current(page==='Settings'?{previewState:'settings-visual-save-error'}:{category:'hotkeys',previewState:'hotkeys-save-error'});};
  let oldTree=oldRender(), newTree=newRender();
  if(page==='Settings') {
    flatten(oldTree).find(n=>n.type==='ToggleRow'&&n.props.label==='settings.visualMetrics.showFps').props.onChange(true);
    flatten(newTree).find(n=>n.type==='ToggleRow'&&n.props.label===en['settings.visualMetrics.showFps']).props.onChange(true);
  } else {
    flatten(oldTree).find(n=>n.type==='button'&&n.props.className==='hotkey-switch').props.onClick();
    const card=flatten(newTree).find(n=>n.type==='Card');card.props.onSaveField({...card.props.config,attack:!card.props.config.attack},'attack');
  }
  await settle();oldRender();newRender();locale='ja';oldTree=oldRender();newTree=newRender();
  const key=page==='Settings'?'settings.visualMetrics.saveFailed':'hotkeys.saveFailed';
  const oldError=flatten(oldTree).find(n=>n.props?.role==='alert');const newError=flatten(newTree).find(n=>n.props?.role==='alert');
  assert.equal(text(oldError),en[key]);assert.equal(text(newError),ja[key]);
  add(`${page} EN failure then JA locale`,text(oldError),text(newError),'Actual rejected-save callbacks followed by same-state locale rerender; original load effect has [] dependencies.');
}
const report={submittedCommit:'afd65b64808e0fd43175dbbaa5afc36e32def9ef',decision:'CHANGES_REQUIRED',results,limits:'Actual inert callbacks/renderers against recovered source. No provider/network/native/gameplay/browser operation. Historical shell retention/pixels remain separate.'};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'independent-results.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
