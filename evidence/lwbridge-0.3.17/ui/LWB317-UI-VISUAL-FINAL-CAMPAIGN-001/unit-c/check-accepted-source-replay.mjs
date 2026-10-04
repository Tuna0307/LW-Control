import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {here,repo,read,squad,automation,pages,fn,raw,nodes,evaluate,compile,h,jsx,Fragment,flatten,text,stable,hooks} from './accepted-harness.mjs';
import * as afk from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkContracts.js';
import * as fixtures from '../../../../../src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js';
import * as autoFixtures from '../../../../../src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js';
import {createConfigDraft} from '../../../../../src/LWBridge.UI-0.3.17/src/previewConfig.js';
import en from '../../../../../src/LWBridge.UI-0.3.17/src/locales/en.js';
import ja from '../../../../../src/LWBridge.UI-0.3.17/src/locales/ja.js';
const results=[]; const locators={};
const automationPage=read('src/LWBridge.UI-0.3.17/src/AutomationPage.jsx');
const hash=data=>crypto.createHash('sha256').update(data).digest('hex').toUpperCase();
const original=(name)=>compile(squad,name,{oe:afk.normalizeJoinRestrictions,se:afk.validJoinRestrictions,k:p=>!p.distanceFilterEnabled||p.maxDistance>0});
const record=(name,count)=>results.push({name,count,result:'PASS'});
const tFor=catalog=>(key,vars={})=>String(catalog[key]||key||'').replace(/\{(\w+)\}/g,(m,k)=>vars[k]??m);
for(const name of ['oe','se','ue','Ce','we','Te','ye','pe','me','I','N','de']){
 const n=fn(squad,name); locators[`Squad.${name}`]={asset:'SquadPanel-HC3-DJei.js',utf8ByteOffset:Buffer.byteLength(squad.slice(0,n.start)),utf8ByteLength:Buffer.byteLength(raw(squad,n)),sha256:hash(raw(squad,n))};
}
for(const name of ['me','O','E','T','Ce']){
 const n=fn(automation,name); locators[`Automation.${name}`]={asset:'AutomationPanel-BJ0gIqFh.js',utf8ByteOffset:Buffer.byteLength(automation.slice(0,n.start)),utf8ByteLength:Buffer.byteLength(raw(automation,n)),sha256:hash(raw(automation,n))};
}
// Execute original helpers, not reimplemented expected-value formulas.
let helperCount=0;
for(const kind of ['farm','join'])for(const target of afk.previewAfkTargets){
 const p=afk.makePreviewAfkProfile('a','A',kind,target.key);
 assert.equal(afk.previewAfkProfileValid(p),original('Te')(p)); helperCount++;
 for(const patch of [{name:' '},{targetKey:'query:',targetNameQuery:''},{minLevel:11,maxLevel:12,levelFilterEnabled:true},{progressiveLevels:true,levelFilterEnabled:true,minLevel:11},{squadIndexes:[]},{executionLimit:NaN},{maxDistance:0,distanceFilterEnabled:true},{monsterType:-1}]){
  assert.equal(afk.previewAfkProfileValid({...p,...patch}),original('Te')({...p,...patch}),`${kind}/${target.key}/${JSON.stringify(patch)}`);
  assert.equal(afk.previewAfkLevelOutOfRange({...p,...patch},target),!!original('we')({...p,...patch},target)); helperCount+=2;
 }
 for(const patch of [{},{targetKey:'obsolete'},{targetKey:'preset:x',source:'undiscovered'},{targetKey:'name:x',monsterType:99},{targetKey:'query:x'},{targetNameQuery:'x'},{monsterNameKey:'absent'}]){
  assert.deepEqual(afk.resolvePreviewAfkTarget(afk.previewAfkTargets,{...p,...patch})??null,original('ue')(afk.previewAfkTargets,{...p,...patch})??null);helperCount++;
 }
 const fresh=original('Ce')(target,[1,2],kind);
 for(const key of ['enabled','kind','executionLimit','minLevel','maxLevel','maxDistance','distanceFilterEnabled','levelFilterEnabled','progressiveLevels','attackEnabled','continuousAttack','joinEnabled','continuousJoin','squadIndexes','joinRestrictions'])assert.deepEqual(p[key],fresh[key],`new ${kind}: ${key}`);
 helperCount++;
}
for(const v of [undefined,{mode:'slotRange',slotRange:[3,5]}, {delaySeconds:[0.001,3]}, {leaders:[{uid:'0'}]}, {leaders:[{uid:'10001'},{uid:'10001'}]}])for(const n of [true,false]){
 const current=afk.normalizeJoinRestrictions(v,1,n);assert.deepEqual(current,original('oe')(v,1,n));assert.equal(afk.validJoinRestrictions(current),original('se')(current));helperCount+=2;
}
record('actual original/current AFK helpers and new profile defaults',helperCount);

// Entire recovered join renderer, including enabled/disabled/negative/member variants.
const joinSource=read('src/LWBridge.UI-0.3.17/src/RallyJoinSettings.jsx');
const ret=fn(squad,'ye').body.body.find(n=>n.type==='ReturnStatement').argument;
const scenarios=['squads-profile-members-self','squads-profile-members-left','squads-profile-members-empty','squads-profile-members-loading','squads-profile-members-failed','squads-profile-members-offline','squads-profile-members-positive'];
let renderCount=0;
function joinRunner(state,value,disabled,catalog){
 const hook=hooks();const t=tFor(catalog);let changed;
 const component=compile(joinSource,'RallyJoinSettings',{...hook,A:jsx,useI18n:()=>({t}),previewMemberFixture:fixtures.previewMemberFixture,se:afk.validJoinRestrictions,JoinModal:'Modal'});
 const ToggleRow=props=>({type:'Toggle',props});
 const render=()=>{hook.begin();return component({value,onChange:next=>changed=next,disabled,previewState:state,ToggleRow});};
 return {render,hook,getChanged:()=>changed};
}
for(const catalog of [en,ja])for(const state of scenarios)for(const enabled of [true,false])for(const disabled of [true,false])for(const mode of ['slot','delay']){
 const value={...afk.normalizeJoinRestrictions(undefined,1,true),enabled,mode,leaderListMode:'whitelist',leaders:[{uid:'10000',name:'Saved self'},{uid:'19999',name:'Saved departed'},{uid:'10002',name:'Saved Blair'}]};
 const runner=joinRunner(state,value,disabled,catalog);const fixture=fixtures.previewMemberFixture(state);const t=tFor(catalog);
 const x=fixture.ready?{membersReady:true,members:fixture.members,selfUid:fixture.selfUid}:null;
 const S=x?x.members.filter(m=>m.uid!==x.selfUid):[], C=new Map((x?.members||[]).map(m=>[m.uid,m]));
 const expected=evaluate(raw(squad,ret),{A:jsx,e:value,t:()=>{},n:fixture.online,r:disabled,i:'squad.afkJoinConditions',o:t,x,v:fixture.failed,S,C,T:value.leaders.filter(m=>!C.has(m.uid)),ee:S,f:false,m:'',g:new Set(),_:()=>{},h:()=>{},p:()=>{},w:props=>({type:'Toggle',props:{...props,label:t(props.label)}}),y:'Modal',se:afk.validJoinRestrictions});
 assert.deepEqual(stable(runner.render()),stable(expected),state);renderCount++;
}
record('whole original/current join renderer in English and Japanese',renderCount);
let handlerCount=0;
for(const state of ['squads-profile-members-self','squads-profile-members-left','squads-profile-members-positive']){
 const value={...afk.normalizeJoinRestrictions(undefined,1,true),enabled:true,leaderListMode:'whitelist',leaders:[{uid:'10000',name:'Saved self'},{uid:'19999',name:'Saved departed'},{uid:'10002',name:'Saved Blair'}]};
 const runner=joinRunner(state,value,false,en);
 const button=flatten(runner.render()).find(n=>n.type==='button'&&text(n)==='Select alliance members');assert.ok(button);button.props.onClick();
 let tree=runner.render(); const checkboxes=flatten(tree).filter(n=>n.type==='input'&&n.props.type==='checkbox');
 assert.equal(checkboxes.length,3);assert.ok(!text(flatten(tree).find(n=>n.props?.className==='garrison-ally-list')).includes('Fixture Self10000')); // self remains saved but not in picker
 checkboxes[0].props.onChange({target:{checked:true}});checkboxes[1].props.onChange({target:{checked:false}});
 tree=runner.render();const confirm=flatten(tree).find(n=>n.type==='button'&&text(n)==='Confirm selection');assert.ok(confirm);confirm.props.onClick();
 assert.deepEqual(runner.getChanged().leaders,[{uid:'10000',name:'Saved self'},{uid:'19999',name:'Saved departed'},{uid:'10001',name:'Fixture Avery'}]);handlerCount++;
}
record('actual member modal confirmation preserves self/departed and source order',handlerCount);

// Compare the original shared card renderer to the actual current meta renderer.
const cardSource=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js');
const metaSource=read('src/LWBridge.UI-0.3.17/src/AutomationMeta.jsx');
const meta=compile(metaSource,'AutomationMeta',{h,Fragment,useI18n:()=>({t:tFor(en)})});
const future=compile(metaSource,'previewFutureTime',{h});
const originalFuture=compile(automation,'E',{S:jsx});
const stateFn=compile(automationPage,'sourceAutomationState');
const presentation=compile(automationPage,'automationRuntimePresentation',{previewTime:compile(automationPage,'previewTime'),previewFutureTime:future});
const originalCard=compile(cardSource,'c',{r:()=>({t:tFor(en)}),o:{useState:()=>[false,()=>{}],useId:()=>''},s:jsx,a:'Switch',i:'Icon'});
const cardNode=fn(cardSource,'c');locators['AutomationCard.c']={asset:'AutomationCard-LCx_jIi7.js',utf8ByteOffset:Buffer.byteLength(cardSource.slice(0,cardNode.start)),utf8ByteLength:Buffer.byteLength(raw(cardSource,cardNode)),sha256:hash(raw(cardSource,cardNode))};
const slices=tree=>flatten(tree).filter(n=>['automation-card-meta-row','automation-card-summary'].includes(n.props?.className)).map(stable);
let metaCount=0;
for(const online of [false,true])for(const active of [false,true])for(const state of ['common.waiting','common.success','common.failed','automation.running'])for(const actionBusy of [false,true]){
 const data={status:[['automation.lastRun','old'],['automation.nextRun','tomorrow'],['automation.nextClaim','later']],summary:[['automation.departed',4],['automation.latestResult',state],['automation.pendingClaims',null]]};
 const expected=originalCard({title:'Title',state,active,online,actionBusy,statusRows:data.status,summaryRows:data.summary});
 assert.deepEqual(slices(meta({online,active,state,actionBusy,presentation:data})),slices(expected));metaCount++;
}
const now=Date.UTC(2026,9,3,10);
for(const language of ['en','ja'])for(const value of [null,0,NaN,Infinity,now-1,now,now+3600000,now+86400000,now+172800000,Date.UTC(2027,1,1)]){
 assert.deepEqual(stable(future(value,language,now)),stable(originalFuture(value,language,now)));metaCount++;
}
record('actual original/current Automation meta and future-time renderer',metaCount);

const cardProps=nodes(automation).filter(n=>n.type==='CallExpression'&&n.arguments[1]?.type==='ObjectExpression').map(n=>n.arguments[1]);
const defs=[['Trucks','automation.railway.title','X','cr'],['Secret Task','automation.secretTask.title','Z','fr'],['Alliance Help','automation.allianceHelp.title','wn','er'],['Alliance Gifts','automation.allianceGift.title','Tn','tr'],['Excavation Stronghold Resources','automation.strongholdResource.title','En','nr'],['Alliance Center Resources','automation.allianceCenterResource.title','Dn','rr'],['Weekend Shield','automation.weekendShield.title','Q','o'],['Attack Shield','automation.attackShield.title','Q','c']];
let summaryCount=0;
const realNow=Date.now; Date.now=()=>now;
for(const [title,key,runtimeName,enabledName] of defs){
 const object=cardProps.find(n=>n.properties.some(p=>p.key?.name==='title'&&raw(automation,p.value)===`\`${key}\``));assert.ok(object,title);
 locators[key]={asset:'AutomationPanel-BJ0gIqFh.js',utf8ByteOffset:Buffer.byteLength(automation.slice(0,object.start)),utf8ByteLength:Buffer.byteLength(raw(automation,object)),sha256:hash(raw(automation,object))};
 for(const enabled of [false,true])for(const runtime of [{},{departed:4,dispatched:7,pendingClaims:2,nextScheduledAt:now+3600000,nextContinuationAt:now+86400000,nextRunAt:now+3600000,shielded:true,inWeekendWindow:true,shieldEndAt:now+86400000,weekendStartAt:now,weekendEndAt:now+86400000,capabilities:{batchDeparture:true,superRefresh:false}},{capabilities:{},details:{qualified:3,available:7}}]){
  const env={S:jsx,j:'en',M:tFor(en),D:compile(automation,'D'),T:compile(automation,'T'),E:(v,l)=>originalFuture(v,l,now),Ce:compile(automation,'Ce'),Fr:runtime.details,[runtimeName]:runtime,[enabledName]:enabled};
  const props={};for(const p of object.properties)if(['statusRows','summaryRows'].includes(p.key?.name))props[p.key.name]=evaluate(raw(automation,p.value),env);
  const current=presentation(title,runtime,enabled,{},tFor(en),'en');
  // Compare drawn fields, not unused status inputs. Use deterministic time for current helper.
  const normalizeRows=rows=>rows?.map(([key,value])=>[key,typeof value==='string'?tFor(en)(value):stable(value)]);
  assert.deepEqual(normalizeRows(current.status),normalizeRows(props.statusRows),`${title} complete status inputs`);
  const expectedMeta=originalCard({state:stateFn('',enabled),active:enabled,online:true,...props});
  const actualMeta=meta({state:stateFn('',enabled),active:enabled,online:true,presentation:current});
  // Replace now-dependent future date labels with identical time props before comparison.
   const expectedSummaries=flatten(expectedMeta).filter(n=>n.props?.className==='automation-card-summary').map(n=>text(n));
   const currentSummaries=flatten(actualMeta).filter(n=>n.props?.className==='automation-card-summary').map(n=>text(n));
   assert.deepEqual(currentSummaries,expectedSummaries,title);
  summaryCount++;
 }
}
Date.now=realNow;
record('named Automation runtime branches through the original shared card',summaryCount);

// Execute the exact manual Assist branch and actual recovered production component.
const manualNode=nodes(automation).find(n=>n.type==='LogicalExpression'&&raw(automation,n).startsWith('!vr&&')&&raw(automation,n).includes('automation.allySecretTasks'));
locators['Automation.manualAssist']={asset:'AutomationPanel-BJ0gIqFh.js',utf8ByteOffset:Buffer.byteLength(automation.slice(0,manualNode.start)),utf8ByteLength:Buffer.byteLength(raw(automation,manualNode)),sha256:hash(raw(automation,manualNode))};
const manualSource=read('src/LWBridge.UI-0.3.17/src/DispatchAssistManual.jsx');
const formatSource=read('evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/rewardDisplay-eZWrd6iS.js');
let assistCount=0;
for(const catalog of [en,ja])for(const state of ['automation-assist-empty','automation-assist-schedule','automation-assist-waiting','automation-assist-retry-wait','automation-assist-running','automation-assist-failed','automation-assist-expired','automation-assist-busy'])for(const selected of [[],['fixture-assist-2']]){
 const fixture=autoFixtures.previewAssistFixture(state);const t=tFor(catalog);const image='GameAssetImage';const format=compile(formatSource,'e');
 const env={S:jsx,M:t,j:catalog===en?'en':'ja',D:compile(automation,'D'),T:compile(automation,'T'),b:image,se:format};
 env.Kr=compile(automation,'Kr',env);env.Yr=compile(automation,'Yr',env);env.Xr=compile(automation,'Xr',env);
 const expected=evaluate(raw(automation,manualNode),{...env,vr:false,Gt:fixture,Sr:new Map(fixture.jobs.map(job=>[job.uuid,job])),tn:selected,rn:fixture.busy?'busy':'',nn:()=>{},qr:()=>{},Jr:()=>{}});
 const component=compile(manualSource,'DispatchAssistManual',{S:jsx,useI18n:()=>({t,language:env.j}),b:image,se:format});
 const current=component({fixture,selected,onSelectionChange:()=>{},onSchedule:()=>{},onJobAction:()=>{}});
 assert.deepEqual(stable(current),stable(expected),state);assistCount++;
 const currentControls=flatten(current).filter(n=>n.type==='input');const sourceControls=flatten(expected).filter(n=>n.type==='input');
 for(let i=0;i<currentControls.length;i++)assert.equal(currentControls[i].props.disabled,sourceControls[i].props.disabled);
}
record('actual original/current manual Assist rows queue states and controls',assistCount);

// Gather handlers are invoked on the actual original and current render trees.
let gatherCount=0;
for(const state of ['automation-gather-no-squads','automation-gather-view-change','automation-gather-runtime_wait','automation-gather-manual_wait','automation-gather-shield_paused','automation-gather-recalling','automation-gather-recall_failed','automation-gather-state_unconfirmed']){
 const draft=autoFixtures.previewResourceGatherConfig(state), runtime=autoFixtures.previewResourceGatherRuntime(state);const t=tFor(en);const hook=hooks();let saved;
 const source=compile(automation,'me',{te:()=>({t,language:'en'}),x:hook,S:jsx,y:'Card',ee:()=>Promise.resolve({})});hook.begin();
 const expected=source({config:draft,status:runtime,squadIndexes:runtime.gatherSquadIndexes,onSave:v=>saved=v,online:true});
 const config={draft,store:{getSnapshot:()=>({draft}),edit:v=>saved=typeof v==='function'?v(draft):v,flush:()=>Promise.resolve()}};
 const currentHook=hooks(); const component=compile(automationPage,'ResourceGatherCard',{h,Fragment,...currentHook,useI18n:()=>({t,language:'en'}),usePreviewConfig:()=>config,previewResourceGatherConfig:autoFixtures.previewResourceGatherConfig,validPreviewResourceGatherConfig:autoFixtures.validPreviewResourceGatherConfig,previewResourceGatherRuntime:()=>runtime,Switch:'Switch',PreviewConfigError:'Error'});
 currentHook.begin();const current=component({previewEnabled:true,previewState:state});
 const currentSelects=flatten(current).filter(n=>n.type==='select'), originalSelects=flatten(expected).filter(n=>n.type==='select');assert.equal(currentSelects.length,originalSelects.length,state);
 for(let i=0;i<currentSelects.length;i++){
  const event={target:{value:i===0?'300':i===1?'5':i%2===0?'food':'7'}};
  currentSelects[i].props.onChange(event);const actual=saved;originalSelects[i].props.onChange(event);assert.deepEqual(actual,saved,`${state} select ${i}`);gatherCount++;
 }
 const states=tree=>flatten(tree).filter(n=>n.props?.className==='automation-resource-gather-state muted').map(text);
 assert.deepEqual(states(current),states(expected),`${state} runtime precedence`);gatherCount++;
}
record('actual original/current Gather callbacks and runtime precedence',gatherCount);

const automationReplay={
 result:'LWB317_VISUAL_FINAL_UNIT_C_ACCEPTED_SOURCE_REPLAY_OK',
 results:results.filter(entry=>/Automation|Assist|Gather|runtime|meta|future-time/i.test(entry.name)),
 locators:Object.fromEntries(Object.entries(locators).filter(([key])=>key.startsWith('Automation.'))),
 assets:[
  {path:'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js',sha256:hash(automation)},
  {path:'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js',sha256:hash(cardSource)},
 ],
 limits:'Fresh campaign-owned execution of the accepted recovered-source Automation overlap only. Squads/AFK continuation is deferred to Unit D; no native/gameplay action or protected original runtime is executed.',
};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'accepted-source-replay.json'),JSON.stringify(automationReplay,null,2)+'\n');
console.log(JSON.stringify(automationReplay,null,2));
process.exit(0);

let garrisonCount=0;
const garrisonValue={enabled:true,recallOnDisable:true,squadPriority:[1,2],targets:[{kind:'allianceBuilding',buildId:1,nameSnapshot:'Building'},{kind:'allyCity',uid:'10001',nameSnapshot:'Saved Avery'}]};
const suppliedAllies=[{uid:'10001',name:'Current Avery',serverId:321,pointId:12,uuid:'1122',uuidUpdatedAt:99}];
assert.deepEqual(afk.refreshGarrisonTargets(garrisonValue,suppliedAllies),compile(squad,'N')(garrisonValue,suppliedAllies));garrisonCount++;
for(const state of ['squads-profile-garrison-running','squads-profile-garrison-unavailable','squads-profile-garrison-error']){
 let saved;const hook=hooks();const component=compile(pages,'GarrisonPreviewSettings',{h,Fragment,...hook,useI18n:()=>({t:tFor(en)}),...fixtures,refreshGarrisonTargets:afk.refreshGarrisonTargets,ToggleRow:'Toggle'});
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
 const currentTable=compile(pages,'ZombieBusPreviewSettings',{h,useI18n:()=>({t}),previewZombieBusRuntime:()=>({assignments:rows,gameTexts:names,lastError:''})})({previewState:'squads-profile-zombie-running'});
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
 const component=compile(pages,'AfkContent',{h,Fragment,...hook,useI18n:()=>({t:tFor(en)}),usePreviewConfig:()=>calls++%2===0?config:toolbar,...afk,...fixtures,
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
 const component=compile(pages,'AllianceDrillPreviewSettings',{h,Fragment,...hook,useI18n:()=>({t}),...afk,...fixtures,ToggleRow:'Toggle',RallyJoinSettings:'Join'});
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
 const component=compile(pages,'AfkContent',{h,Fragment,...hook,useI18n:()=>({t}),usePreviewConfig:()=>calls++%2===0?config:toolbar,...afk,...fixtures,initialAfkProfiles:()=>[],validAfkToolbarConfig:()=>true,PreviewConfigError:'Error',CompactAfkCard:'Card',AfkProfileEditor:'Editor',AllianceDrillPreviewSettings:'Drill',GarrisonPreviewSettings:'Garrison',ZombieBusPreviewSettings:'Zombie'});
 hook.begin();const current=component({previewEnabled:enabled,previewState:'squads-profile-potion-positive'});
 const potion=flatten(current).find(n=>n.type==='section'&&n.props.className==='automation-card monster-afk-toolbar-settings');
 const original=evaluate(raw(originalI,sourceToolbar('potion')),{k:'potion',A:jsx,h:t,te:stamina,re:true,B:enabled?'':'busy',ae:()=>{},ce:()=>{}});
 const controls=tree=>flatten(tree).filter(n=>n.type==='input').map(n=>Object.fromEntries(['type','min','max','step','value','checked','disabled'].filter(k=>n.props[k]!==undefined).map(k=>[k,['min','max','step'].includes(k)?Number(n.props[k]):n.props[k]])));
 assert.deepEqual(controls(potion),controls(original));assert.equal(text(potion),text(original));toolbarCases+=2;
}
for(const state of ['','native','native-unavailable','map-truck'])assert.deepEqual(fixtures.previewDrillRuntime(state),[]);
record('actual Potion form Drill source wait-detail priority and drag predicates',toolbarCases+4);

// Distinguishing immutable worker checkpoint: test the actual previous imports/functions.
const baseline=read('evidence/lwbridge-0.3.17/ui/LWB317-UI-AUTOMATION-AFK-CLOSEOUT-001/takeover-baseline/Pages.jsx');
const before=[];
const oldPresentation=compile(baseline,'automationRuntimePresentation',{previewTime:compile(baseline,'previewTime')});
for(const title of ['Trucks','Secret Task']){
 const data=oldPresentation(title,{},true,{},tFor(en),'en');
 before.push({case:`${title} unknown capability`,failed:data.status.at(-1)[1]!==tFor(en)('common.notChecked')});
}
for(const key of ['squad.join.hint','squad.join.hint.${e.mode}'])before.push({case:`join recovered hint ${key}`,failed:!baseline.includes(key)});
before.push({case:'unrecovered full status grid',failed:baseline.includes('className="automation-status-grid"')});
assert.equal(before.filter(c=>c.failed).length,5);
record('immutable baseline distinguishes five corrected render omissions',5);

const assets=['AutomationPanel-BJ0gIqFh.js','SquadPanel-HC3-DJei.js','index-BVfnK1wp.js','GameAssetImage-Diy9VTIr.js','AutomationCard-LCx_jIi7.js','rewardDisplay-eZWrd6iS.js'].map(name=>({path:`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${name}`,sha256:hash(read(`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/${name}`))}));
const report={result:'LWB317_AUTOMATION_AFK_ACTUAL_SOURCE_OK',results,baseline:before,locators,assets,limits:'Inert actual original/current render and local payload proof. No live game, native provider or original post-auth pixels.'};
if(process.argv.includes('--record'))fs.writeFileSync(path.join(here,'actual-source-results.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({result:report.result,results},null,2));
