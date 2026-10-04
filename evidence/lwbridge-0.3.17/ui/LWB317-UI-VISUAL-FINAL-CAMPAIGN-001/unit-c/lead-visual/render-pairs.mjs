import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath,pathToFileURL} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const repo=path.resolve(here,'../../../../../..');
const src=path.join(repo,'src/LWBridge.UI-0.3.17/src');
const outputRoot=process.argv.includes('--baseline')?path.join(here,'baseline-render'):here;
const assets=path.join(repo,'evidence/lwbridge-0.3.17/ui/frontend-package/web/assets');
const require=createRequire(path.join(repo,'src/LWBridge.UI-0.3.17/package.json'));
const {parse}=require('@babel/parser'),{transformSync}=require('esbuild');
const React=require('react'),{renderToStaticMarkup}=require('react-dom/server');
const verifyOnly=process.argv.includes('--verify');
const persist=(file,data)=>{if(verifyOnly)assert.equal(fs.readFileSync(file,'utf8'),String(data),'Fresh actual renderer differs from pinned output: '+file);else fs.writeFileSync(file,data);};
const read=f=>fs.readFileSync(f,'utf8');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const dependencies=new Set();
function load(file){dependencies.add(file);return read(file);}
function moduleOf(file,env,names){
 const source=load(file),ast=parse(source,{sourceType:'module',plugins:['jsx']});
 const pieces=ast.program.body.filter(n=>!['ImportDeclaration','ExportAllDeclaration'].includes(n.type)).map(n=>n.type==='ExportNamedDeclaration'?n.declaration?source.slice(n.declaration.start,n.declaration.end):'':source.slice(n.start,n.end));
 const code=transformSync(pieces.join('\n'),{loader:'jsx',jsxFactory:'h',jsxFragment:'Fragment'}).code;
 return new Function(...Object.keys(env),code+`\nreturn {${names.join(',')}};`)(...Object.values(env));
}
function fnOf(file,name,env){const source=load(file),ast=parse(source,{sourceType:'module',plugins:['jsx']});const node=ast.program.body.map(n=>n.declaration||n).find(n=>n.type==='FunctionDeclaration'&&n.id.name===name);if(!node)throw Error(name);return new Function(...Object.keys(env),`return (${source.slice(node.start,node.end)});`)(...Object.values(env));}
const element=(type,props,...children)=>({type,props:{...props,...(children.length?{children}: {})}});
const Fragment='@fragment';const Activity='@activity';
const jsx={jsx:(type,props,key)=>element(type,{...props,key}),jsxs:(type,props,key)=>element(type,{...props,key}),Fragment};
let context={},caseData={};
// Disclosed post-effect state: the recovered Ae effect copies configured reward
// preferences and the available squad-index reply into these local hook values.
const originalPanelText=load(path.join(assets,'AutomationPanel-BJ0gIqFh.js'));
const originalPanelAst=parse(originalPanelText,{sourceType:'module'});
const originalPanelNode=originalPanelAst.program.body.find(n=>n.type==='FunctionDeclaration'&&n.id.name==='Ae');
const hookBindings=[];
function visitBindings(n){if(!n||typeof n!=='object')return;
 if(n.type==='VariableDeclarator'&&n.id?.type==='ArrayPattern'&&n.init?.type==='CallExpression'){
  const callee=n.init.callee.type==='SequenceExpression'?n.init.callee.expressions.at(-1):n.init.callee;
  if(callee.type==='MemberExpression'&&callee.property.name==='useState')hookBindings.push({offset:n.start,name:n.id.elements[0].name});
 }
 for(const v of Object.values(n))if(Array.isArray(v))v.forEach(visitBindings);else if(v&&typeof v==='object')visitBindings(v);
}
visitBindings(originalPanelNode);hookBindings.sort((a,b)=>a.offset-b.offset);

const fakeReact={Activity,useState(initial){const index=context.index++;let value=typeof initial==='function'?initial():initial;
 if(context.name==='Ae'){
  const binding=hookBindings[index]?.name;
  if(binding==='H')value=['fixture-medal'];
  if(binding==='Ut')value=caseData.category==='alliance'?[1,2,3,4]:[];
 }
 if(caseData.expanded&&((context.name==='c'&&index===0)||(context.name==='AutomationCard'&&index===1)||(context.name==='ResourceGatherCard'&&index===0)||(context.name==='TradeStationCard'&&index===2)))value=true;
 return [value,()=>{}];},useRef(value){return {current:value};},useId(){return 'proof-id';},useEffect(){},useMemo(f){return f();},useSyncExternalStore(sub,get){return get?get():undefined;}};
function materialize(node){
 if(node==null||typeof node==='boolean')return null;if(Array.isArray(node))return node.map((item,index)=>React.createElement(React.Fragment,{key:item?.props?.key??index},materialize(item)));if(typeof node!=='object')return node;
 if(node.type===Fragment)return materialize(node.props.children);if(node.type===Activity)return node.props.mode==='hidden'?null:materialize(node.props.children);
 if(typeof node.type==='function'){const old=context;context={name:node.type.name,index:0,props:node.props};const tree=node.type(node.props||{});context=old;return materialize(tree);}
 const props={};for(const [k,v]of Object.entries(node.props||{}))if(k!=='children'&&k!=='ref'&&v!==undefined)props[k]=v;
 return React.createElement(node.type,props,...[materialize(node.props?.children)].flat());
}
function hook(initial){const draft=structuredClone(typeof initial==='function'?initial():initial);const store={getSnapshot:()=>({draft,error:null,saving:false,dirty:false}),flush:()=>Promise.resolve(),receive(){},edit(){},pause(){}};return {...store.getSnapshot(),store};}
const contracts=await import(pathToFileURL(path.join(src,'previewAutomationContracts.js')).href);
const fixtures=await import(pathToFileURL(path.join(src,'previewAutomationFixtures.js')).href);
const history=await import(pathToFileURL(path.join(src,'tradePurchaseHistory.js')).href);
for(const f of ['previewAutomationContracts.js','previewAutomationFixtures.js','tradePurchaseHistory.js'])dependencies.add(path.join(src,f));
const indexSource=load(path.join(assets,'index-BVfnK1wp.js'));
const allCatalogs={};const currentCatalogs={};
for(const language of ['en','ja']){
 const localeFile=fs.readdirSync(assets).find(f=>f.startsWith(language+'-')&&f.endsWith('.js'));
 const ast=parse(load(path.join(assets,localeFile)),{sourceType:'module'}),messages={};
 const walk=n=>{if(!n||typeof n!=='object')return;if(n.type==='ObjectProperty'&&n.key?.type==='StringLiteral'&&(n.value.type==='TemplateLiteral'||n.value.type==='StringLiteral'))messages[n.key.value]=n.value.type==='StringLiteral'?n.value.value:n.value.quasis[0].value.cooked;for(const v of Object.values(n))if(Array.isArray(v))v.forEach(walk);else if(v&&typeof v==='object')walk(v);};walk(ast);allCatalogs[language]=messages;
 currentCatalogs[language]=(await import(pathToFileURL(path.join(src,`locales/${language}.js`)).href)).default;dependencies.add(path.join(src,`locales/${language}.js`));
}
const tFor=catalog=>(key,vars={})=>String(catalog[key]||key||'').replace(/\{(\w+)\}/g,(m,k)=>String(vars[k]??m));
function setup(language){
 const originalT=tFor(allCatalogs[language]),currentT=tFor(currentCatalogs[language]);
 const englishKeys=new Map();for(const[k,v]of Object.entries(currentCatalogs.en))if(!englishKeys.has(v))englishKeys.set(v,k);
 const currentI18n=()=>({t:currentT,language,english:text=>currentCatalogs[language][englishKeys.get(text)]||text});
 const currentEnv={h:element,Fragment,...fakeReact,useContext:()=>null,createContext:()=>({}),useI18n:currentI18n};
 const imageCurrent=moduleOf(path.join(src,'GameAssetImage.jsx'),currentEnv,['GameAssetImage']);
 const imageOriginal=moduleOf(path.join(assets,'GameAssetImage-Diy9VTIr.js'),{e:()=>jsx,t:a=>a,r:()=>fakeReact,n:()=>Promise.reject(Error('INERT_NO_READER'))},['x']);
 const shared=moduleOf(path.join(src,'sharedPageUI.jsx'),currentEnv,['Switch','ToggleRow','PanelTitle']);
 const originalSwitch=fnOf(path.join(assets,'index-BVfnK1wp.js'),'zn',{M:jsx});
 const originalIcon=fnOf(path.join(assets,'index-BVfnK1wp.js'),'Vr',{M:jsx});
 const originalToggle=fnOf(path.join(assets,'index-BVfnK1wp.js'),'Bn',{M:jsx,De:()=>({t:originalT}),zn:originalSwitch});
 const originalCard=moduleOf(path.join(assets,'AutomationCard-LCx_jIi7.js'),{e:()=>jsx,t:a=>a,n:()=>fakeReact,r:()=>({t:originalT}),i:()=>null,a:originalSwitch},['c']);
 const meta=moduleOf(path.join(src,'AutomationMeta.jsx'),currentEnv,['AutomationMeta','previewFutureTime']);
 const assist=moduleOf(path.join(src,'DispatchAssistManual.jsx'),{...currentEnv,S:jsx,b:imageCurrent.GameAssetImage},['DispatchAssistManual']);
 const error=moduleOf(path.join(src,'previewConfigHook.jsx'),{...currentEnv,createConfigDraft:()=>{},useSyncExternalStore:fakeReact.useSyncExternalStore},['PreviewConfigError']);
 const current=moduleOf(process.argv.includes('--baseline')?path.join(here,'baseline-AutomationPage.jsx'):path.join(src,'AutomationPage.jsx'),{...currentEnv,...fixtures,...contracts,...history,...meta,...shared,...error,...assist,...imageCurrent,usePreviewConfig:hook},['AutomationPage','automationCategories','automationCards','initialAutomationDraft','previewConstructionBuildingTypes','previewSoldierCamps','previewTrainRewards']);
 const noop=()=>{};const store=(profile,name,draft)=>hook(draft).store;
 const original=moduleOf(path.join(assets,'AutomationPanel-BJ0gIqFh.js'),{
  e:()=>0,t:noop,n:(name,draft)=>({...hook(draft),state:hook(draft).store}),r:noop,i:()=>jsx,a:noop,o:noop,s:noop,c:a=>a,l:()=>fakeReact,u:()=> 'proof-profile',ee:noop,d:noop,te:()=>({t:originalT,language}),ne:noop,f:noop,p:noop,m:noop,h:store,g:originalToggle,_:noop,v:originalIcon,re:noop,ie:noop,ae:noop,oe:()=>null,se:(value)=>String(value),y:originalCard.c,b:imageOriginal.x
 },['Ae']);
 return {current,original};
}
function inputFor(current,category){
 const taskMap={'Auto Training':'soldierTraining','Automatic Construction':'construction','Free Stamina':'stamina','Automatic Treatment':'treatment','Trucks':'railway','Secret Task':'dispatch','Ghost Ops':'ghostRecon','Alliance Tech Donations':'allianceDonate','Automatic Official Application':'officialPosition','Automatic Alliance Train Boarding':'allianceTrainRide','Alliance Help':'allianceHelp','Alliance Gifts':'allianceGift','Excavation Stronghold Resources':'strongholdResource','Alliance Center Resources':'allianceCenterResource','Alliance Gathering Dispatch':'allianceGather'};
 const config={};for(const[title,key]of Object.entries(taskMap)){
  const c=current.initialAutomationDraft(title,'');config[key]={...c};
  if(title==='Auto Training')config[key]={enabled:false,totalCount:0,targetLevel:0};
  if(title==='Automatic Construction')config[key]={enabled:false};
  if(title==='Automatic Alliance Train Boarding')config[key]={enabled:false,selectionMode:'reward',vipSelectionMode:'reward',preferredRewardKeys:['fixture-medal'],thanksMode:'like',ticketCount:1};
  if(title==='Alliance Gathering Dispatch')config[key]={enabled:false,squadPriority:[1,2]};
  if(title==='Ghost Ops')config[key]={enabled:false,autoJoinAlliance:false,autoClaimRewards:false,allianceFilter:'special'};
 }
 config.resourceGather=fixtures.previewResourceGatherConfig('automation-gather-no-squads');
 const tasks=Object.fromEntries(Object.entries(taskMap).map(([title,key])=>[key,{...fixtures.previewAutomationRuntime(title,''),enabled:false}]));
 tasks.soldierTraining={...tasks.soldierTraining,camps:current.previewSoldierCamps,trained:420,promoted:180,collected:360,order:contracts.previewTrainingOrder('')};
 tasks.construction={...tasks.construction,buildingTypes:current.previewConstructionBuildingTypes};
 tasks.allianceTrainRide={...tasks.allianceTrainRide,rewardOptions:current.previewTrainRewards};
 tasks.resourceGather=fixtures.previewResourceGatherRuntime('');
 const resourceStatus={tasks:{buildingResources:{enabled:false,intervalMinutes:60},armedTruckReward:{enabled:false,intervalMinutes:60}}};
 return {activeCategory:category,profileId:'proof-profile',autoWeekendShield:false,autoAttackShield:false,online:false,config,status:{tasks,services:{}},resourceStatus,redPacketConfig:{},treasureConfig:{},fireworksConfig:{},tradeStationConfig:{},onToggle:()=>{},onBackgroundToggle:()=>{},onBackgroundRun:()=>{},onConstructionClaimAll:()=>{},onInspect:()=>{},onRunTask:()=>{},onResourceRun:()=>{},onChatRun:()=>{}};
}
fs.mkdirSync(path.join(outputRoot,'raw'),{recursive:true});
const results=[];
for(const language of ['en','ja']){
 const {original,current}=setup(language);
 for(const category of ['daily','alliance','resourceGather','resources','chat','trade','system'])for(const expanded of [false,true]){
  caseData={expanded,category};const inputs=inputFor(current,category);
  const expected=renderToStaticMarkup(materialize(element(original.Ae,inputs)));
  const actual=renderToStaticMarkup(materialize(element(current.AutomationPage,{activeCategory:category,previewState:''})));
  const id=`${category}-${expanded?'expanded':'collapsed'}-${language}`;
  persist(path.join(outputRoot,'raw',id+'-original.html'),expected);persist(path.join(outputRoot,'raw',id+'-current.html'),actual);
  results.push({id,language,category,expanded,originalHtml:`raw/${id}-original.html`,currentHtml:`raw/${id}-current.html`,originalSha256:hash(expected),currentSha256:hash(actual),inputs});
 }
}
for(const f of ['reference.css','styles.css'])dependencies.add(path.join(src,f));dependencies.add(path.join(assets,'index-rIL9Fpht.css'));
const json=(file,data)=>persist(path.join(outputRoot,file),JSON.stringify(data,null,2)+'\n');
json('renderer-inputs.json',{originalHookBindings:hookBindings,cases:results,limits:['Actual recovered and canonical functions; effects omitted, inert stores snapshot, explicit expansion seed and shared synthetic runtime inputs. Original H and Ut hooks are seeded with configured preferredRewardKeys and known Alliance squad indexes [1,2,3,4] (empty for Resource Gather) to match the current post-effect snapshot. Original icon Vr is executed, not stubbed.','Original uses actual recovered locales independently from current catalogs.','Image components executed independently with empty cache/no readers. Asset-complete visuals remain BLOCKED.','Offline source-valid component presentation only; no gameplay or protected original runtime.']});
if(!verifyOnly)json('dependencies.json',{files:[...dependencies].map(file=>({path:path.relative(repo,file).replaceAll('\\','/'),sha256:hash(fs.readFileSync(file))})),tools:{node:process.version,react:require('react/package.json').version,esbuild:require('esbuild/package.json').version,parser:require('@babel/parser/package.json').version},scriptSha256:hash(fs.readFileSync(fileURLToPath(import.meta.url)))});
console.log(JSON.stringify({rendererPairs:results.length,root:here}));
