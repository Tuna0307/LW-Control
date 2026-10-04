import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {repo,read,squad,fn,raw,nodes,evaluate,compile,h,jsx,Fragment,flatten,text,stable,hooks} from '../unit-c/accepted-harness.mjs';
import * as afk from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js';
import * as fixtures from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js';
import * as autoFixtures from '../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js';
import {createConfigDraft} from '../../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js';
import en from '../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js';
import ja from '../../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js';
const here=path.dirname(fileURLToPath(import.meta.url));
const results=[]; const locators={};
const squadPage=read('src/LWBridge.UI-0.3.17/src/SquadsPage.jsx');
const hash=data=>crypto.createHash('sha256').update(data).digest('hex').toUpperCase();
const record=(name,count)=>results.push({name,count,result:'PASS'});
const tFor=catalog=>(key,vars={})=>String(catalog[key]||key||'').replace(/\{(\w+)\}/g,(m,k)=>vars[k]??m);let garrisonCount=0;
const garrisonValue={enabled:true,recallOnDisable:true,squadPriority:[1,2],targets:[{kind:'allianceBuilding',buildId:1,nameSnapshot:'Building'},{kind:'allyCity',uid:'10001',nameSnapshot:'Saved Avery'}]};
const suppliedAllies=[{uid:'10001',name:'Current Avery',serverId:321,pointId:12,uuid:'1122',uuidUpdatedAt:99}];
assert.deepEqual(afk.refreshGarrisonTargets(garrisonValue,suppliedAllies),compile(squad,'N')(garrisonValue,suppliedAllies));garrisonCount++;
for(const state of ['squads-profile-garrison-running','squads-profile-garrison-unavailable','squads-profile-garrison-error']){
 let saved;const hook=hooks();const component=compile(squadPage,'GarrisonPreviewSettings',{h,Fragment,...hook,useI18n:()=>({t:tFor(en)}),...fixtures,refreshGarrisonTargets:afk.refreshGarrisonTargets,ToggleRow:'Toggle'});
 const render=()=>{hook.begin();return component({disabled:false,value:garrisonValue,onChange:v=>saved=v,previewState:state});};
 const tree=render();const priorities=flatten(tree).filter(n=>n.props?.className==='garrison-priority-item');priorities[0].props.onDragStart();
 const drop=flatten(render()).filter(n=>n.props?.className==='garrison-priority-item')[1];drop.props.onDrop({preventDefault(){}});
 const reordered=compile(squad,'de')(garrisonValue.targets,0,1);assert.deepEqual(saved.targets,afk.refreshGarrisonTargets({...garrisonValue,targets:reordered},fixtures.previewGarrisonMemberFixture(state).members).targets);garrisonCount++;
 const assignments=flatten(tree).filter(n=>n.props?.className==='garrison-assignment');assert.equal(assignments.length,state.endsWith('running')?1:0);garrisonCount++;
 const buildings=flatten(tree).filter(n=>n.type==='label'&&n.props?.className?.startsWith('garrison-building'));
 assert.ok(buildings.every(n=>flatten(n).find(n=>n.type==='input').props.disabled===state.endsWith('unavailable')));garrisonCount++;
}
record('Garrison actual priority handlers availability runtime and source snapshot normalization',garrisonCount);
let zombieCount=0;
for(const catalog of [en,ja])for(const rows of [[],[{squadIndex:1,uid:'x',name:'',gold:true,state:'guarding'},{squadIndex:2,uid:'y',name:'Named ally',gold:false,state:'marching'},{squadIndex:3,uid:'z',nameKey:'localized.bus',gold:null,state:'returning'}]]){
 const names={'localized.bus':'Recovered supplied name'};const t=tFor(catalog);
 const originalTable=compile(squad,'me',{d:()=>({t}),A:jsx})({rows,names});
 const currentTable=compile(squadPage,'ZombieBusPreviewSettings',{h,useI18n:()=>({t}),previewZombieBusRuntime:()=>({assignments:rows,gameTexts:names,lastError:''})})({previewState:'squads-profile-zombie-running'});
 const tables=flatten(currentTable).filter(n=>n.type==='table');
 if(rows.length)assert.deepEqual(stable(tables[0]),stable(flatten(originalTable).find(n=>n.type==='table')));else assert.equal(tables.length,0);zombieCount++;
}
record('actual original/current Zombie table empty gold normal unknown and supplied names',zombieCount);

// Positive fixture data must be unreachable from inactive/native mode inputs.
let fenceCount=0;
for(const state of ['', 'native', 'native-unavailable','home-connected','map-truck','squads-equipment-positive']){
 assert.equal(fixtures.previewMemberFixture(state).members.length,0);
 assert.equal(fixtures.previewGarrisonRuntime(state).buildings.length,0);
 assert.equal(fixtures.previewZombieBusRuntime(state).assignments.length,0);
 assert.equal(fixtures.initialAfkToolbarConfig(state).garrison.targets.length,0);
 assert.equal(autoFixtures.previewAssistFixture(state).tasks.length,0);fenceCount+=5;
}
record('explicit AFK/Assist fixture mode fencing',fenceCount);

// Store proof uses two profile IDs, failed write Retry/Discard, and real callback payloads.
const a=afk.makePreviewAfkProfile('profile-a','A','join'), b=afk.makePreviewAfkProfile('profile-b','B','join');
let confirmed=[a,b],fail=true,writes=0;
const store=createConfigDraft(confirmed,{valid:v=>v.every(afk.previewAfkProfileValid),read:async()=>structuredClone(confirmed),write:async v=>{writes++;if(fail){fail=false;throw Error('inert failure');}return confirmed=structuredClone(v);}});
const draft=structuredClone(confirmed);draft[0].joinRestrictions.delaySeconds=[2,3];store.edit(draft,false);await assert.rejects(store.flush());
assert.deepEqual(store.getSnapshot().draft[1],b);await store.flush();assert.deepEqual(store.getSnapshot().confirmed[0].joinRestrictions.delaySeconds,[2,3]);
store.edit(v=>v.map(p=>p.id==='profile-b'?{...p,name:'B changed'}:p),false);await store.refresh(true);assert.equal(store.getSnapshot().draft[1].name,'B');store.dispose();
record('two profile IDs and real draft error Retry/Discard',5);

// JS confirmation is inert here; execute the actual production Delete callback
// and the original nested I.lt (the top-level lt is an unrelated animation helper).
const originalI=raw(squad,fn(squad,'I'));
const originalDeleteNode=nodes(originalI).find(n=>n.type==='FunctionDeclaration'&&n.id.name==='lt'&&raw(originalI,n).includes('window.confirm'));
const originalDeleteSource=raw(originalI,originalDeleteNode);
const originalDeleteOffset=fn(squad,'I').start+originalDeleteNode.start;
locators['Squad.I.lt']={asset:'SquadPanel-HC3-DJei.js',utf8ByteOffset:Buffer.byteLength(squad.slice(0,originalDeleteOffset)),utf8ByteLength:Buffer.byteLength(originalDeleteSource),sha256:hash(originalDeleteSource)};
let deletionCases=0;
for(const accept of [false,true]){
 const start=[a,b];let current=structuredClone(start),edits=0,flushes=0,currentPrompt,originalPrompt,originalSaved,originalSelection;
 const hook=hooks();
 const config={confirmed:start,get draft(){return current;},store:{getSnapshot:()=>({draft:current}),edit:(v,delay)=>{assert.equal(delay,false);current=typeof v==='function'?v(current):v;edits++;},flush:async()=>{flushes++;}}};
 const toolbar={draft:fixtures.initialAfkToolbarConfig('squads-profile-positive')};
 let calls=0;
 const component=compile(squadPage,'AfkContent',{h,Fragment,...hook,useI18n:()=>({t:tFor(en)}),usePreviewConfig:()=>calls++%2===0?config:toolbar,...afk,...fixtures,
   initialAfkProfiles:()=>start,validAfkToolbarConfig:()=>true,PreviewConfigError:'Error',CompactAfkCard:'Card',AfkProfileEditor:'Editor',AllianceDrillPreviewSettings:'Drill',GarrisonPreviewSettings:'Garrison',ZombieBusPreviewSettings:'Zombie',
   window:{confirm:message=>{currentPrompt=message;return accept;}}});
 const render=()=>{hook.begin();return component({previewEnabled:true,previewState:'squads-profile-positive'});};
 const tree=render();const card=flatten(tree).find(n=>n.type==='article'&&n.props.key==='profile-a');
 flatten(card).find(n=>n.type==='button'&&text(n)==='Delete').props.onClick();
 const originalDelete=compile(originalDeleteSource,'lt',{window:{confirm:message=>{originalPrompt=message;return accept;}},h:tFor(en),S:start,De:{current:'profile-a'},Ee:id=>originalSelection=id,Ze:async value=>originalSaved=value,V:()=>{},Fe:()=>{}});
 await originalDelete(a);
 assert.equal(currentPrompt,originalPrompt);
 assert.deepEqual(current,accept?originalSaved:start);assert.equal(edits,accept?1:0);assert.equal(flushes,accept?1:0);
 if(accept){assert.equal(hook.values[1],originalSelection);assert.equal(originalSelection,'profile-b');}
 deletionCases++;
}
record('actual original/current profile Delete confirmation cancel and accept',deletionCases);

let toolbarCases=0;
const sourceToolbar=name=>nodes(originalI).find(n=>n.type==='LogicalExpression'&&raw(originalI,n.left)===`k===\`${name}\``);
for(const name of ['potion','drill']){
 const node=sourceToolbar(name),offset=fn(squad,'I').start+node.start;
 locators[`Squad.I.${name}`]={asset:'SquadPanel-HC3-DJei.js',utf8ByteOffset:Buffer.byteLength(squad.slice(0,offset)),utf8ByteLength:Buffer.byteLength(raw(originalI,node)),sha256:hash(raw(originalI,node))};
}
for(const catalog of [en,ja])for(const disabled of [false,true]){
 const t=tFor(catalog),value={enabled:true,squadIndexes:[2,1],activeRally:true,joinRestrictions:afk.normalizeJoinRestrictions(undefined,1,true)};
 let changed;const hook=hooks();
 const component=compile(squadPage,'AllianceDrillPreviewSettings',{h,Fragment,...hook,useI18n:()=>({t}),...afk,...fixtures,ToggleRow:'Toggle',RallyJoinSettings:'Join'});
 const render=()=>{hook.begin();return component({disabled,value,onChange:next=>changed=next,previewState:'squads-profile-drill-waiting'});};
 const current=render(),runtime=fixtures.previewDrillRuntime('squads-profile-drill-waiting');
 const original=evaluate(raw(originalI,sourceToolbar('drill')),{k:'drill',A:jsx,h:t,ht:'2 → 1',E:value,B:disabled?'save':'',w:props=>({type:'Toggle',props}),tt:()=>{},_t:[2,1,3,4],gt:new Set([2,1]),Re:null,Be:null,ze:()=>{},Ve:()=>{},it:()=>{},rt:()=>{},T:'Icon',ye:'Join',t:false,y:{state:{edit(){}}},K:runtime,Ae:{}});
 const currentGroup=flatten(current).find(n=>n.props?.className==='automation-compact-choice-group automation-squad-priority');
 const originalGroup=flatten(original).find(n=>n.props?.className==='automation-compact-choice-group automation-squad-priority');
 assert.equal(currentGroup.props['aria-label'],originalGroup.props['aria-label']);
 const controls=tree=>flatten(tree).filter(n=>n.type==='input').map(n=>({checked:n.props.checked,disabled:n.props.disabled}));
 assert.deepEqual(controls(currentGroup),controls(originalGroup));
 const detail=tree=>flatten(tree).filter(n=>n.type==='span'&&n.props.className==='muted').map(text);
 assert.deepEqual(detail(current),detail(original));toolbarCases+=3;
 if(!disabled){const priorities=flatten(currentGroup).filter(n=>n.props.className?.startsWith('automation-squad-priority-item'));
 priorities[0].props.onDragStart();const dragging=render();const target=flatten(dragging).find(n=>n.props.key===1&&n.props.className?.startsWith('automation-squad-priority-item'));
 target.props.onDragOver({preventDefault(){}});assert.ok(flatten(render()).some(n=>n.props.className?.includes('drag-over')));target.props.onDrop({preventDefault(){}});assert.deepEqual(changed.squadIndexes,[1,2]);toolbarCases+=2;}
}
for(const catalog of [en,ja])for(const enabled of [false,true])for(const stamina of [0,50,9999,NaN]){
 const t=tFor(catalog),hook=hooks(),toolbar={draft:{...fixtures.initialAfkToolbarConfig('squads-profile-potion-positive'),minStamina:stamina,preferFifty:true},store:{getSnapshot(){return {draft:this.value};},value:null,edit(){},flush:async()=>{}}};
 let calls=0;const config={draft:[],confirmed:[]};
 const component=compile(squadPage,'AfkContent',{h,Fragment,...hook,useI18n:()=>({t}),usePreviewConfig:()=>calls++%2===0?config:toolbar,...afk,...fixtures,initialAfkProfiles:()=>[],validAfkToolbarConfig:()=>true,PreviewConfigError:'Error',CompactAfkCard:'Card',AfkProfileEditor:'Editor',AllianceDrillPreviewSettings:'Drill',GarrisonPreviewSettings:'Garrison',ZombieBusPreviewSettings:'Zombie'});
 hook.begin();const current=component({previewEnabled:enabled,previewState:'squads-profile-potion-positive'});
 const potion=flatten(current).find(n=>n.type==='section'&&n.props.className==='automation-card monster-afk-toolbar-settings');
 const original=evaluate(raw(originalI,sourceToolbar('potion')),{k:'potion',A:jsx,h:t,te:stamina,re:true,B:enabled?'':'busy',ae:()=>{},ce:()=>{}});
 const controls=tree=>flatten(tree).filter(n=>n.type==='input').map(n=>Object.fromEntries(['type','min','max','step','value','checked','disabled'].filter(k=>n.props[k]!==undefined).map(k=>[k,['min','max','step'].includes(k)?Number(n.props[k]):n.props[k]])));
 assert.deepEqual(controls(potion),controls(original));assert.equal(text(potion),text(original));toolbarCases+=2;
}
for(const state of ['','native','native-unavailable','map-truck'])assert.deepEqual(fixtures.previewDrillRuntime(state),[]);
record('actual Potion form Drill source wait-detail priority and drag predicates',toolbarCases+4);

const assets=[
 {path:'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js',sha256:hash(read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js'))},
 {path:'src/LWBridge.UI-0.3.17/src/SquadsPage.jsx',sha256:hash(squadPage)},
];
const report={result:'LWB317_VISUAL_FINAL_UNIT_D_AFK_SOURCE_OK',results,locators,assets,limits:'Campaign-owned recovered/current AFK/Garrison source replay. Handler drag proof is not physical browser drag. No native/gameplay action or protected original runtime is executed.'};
fs.writeFileSync(path.join(here,'afk-source-replay.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({result:report.result,results},null,2));