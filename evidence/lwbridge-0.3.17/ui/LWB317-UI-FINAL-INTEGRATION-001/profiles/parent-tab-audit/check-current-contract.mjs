import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(here,'../../../../../..'),src=path.join(repo,'src/LWBridge.UI-0.3.17/src'),req=createRequire(path.join(src,'../package.json')),{parse}=req('@babel/parser');
const read=n=>fs.readFileSync(path.join(src,n),'utf8'),sha=s=>crypto.createHash('sha256').update(s).digest('hex');
function walk(n,out=[]){if(!n||typeof n!=='object')return out;if(n.type)out.push(n);for(const v of Object.values(n))Array.isArray(v)?v.forEach(x=>walk(x,out)):v&&typeof v==='object'&&walk(v,out);return out;}
function entry(source,name){return walk(parse(source,{sourceType:'module',plugins:['jsx']})).find(n=>n.type==='FunctionDeclaration'&&n.id.name===name);}
const facts=JSON.parse(fs.readFileSync(path.join(here,'source-contract.json'))),groups=JSON.parse(fs.readFileSync(path.join(here,'category-lifetime/source-contract.json')));
for(const original of [facts.original,...Object.values(facts.original.components)])assert.equal(sha(fs.readFileSync(path.join(repo,original.path))).toUpperCase(),original.sha256);
assert.equal(sha(fs.readFileSync(path.join(repo,groups.source.path))).toUpperCase(),groups.source.sha256);
const results=[];
for(const spec of [
  {name:'automation',file:'AutomationPage.jsx',entry:'AutomationPage',selector:'category',provided:'activeCategory',local:'localCategory',change:'selectCategory',visit:'setVisitedCategories',parent:'onActiveCategoryChange',setter:'setLocalCategory',chosen:'trade',initial:'daily'},
  {name:'squads',file:'SquadsPage.jsx',entry:'SquadsPage',selector:'tab',provided:'activeTab',local:'localTab',change:'selectTab',visit:'setVisitedTabs',parent:'onActiveTabChange',setter:'setLocalTab',chosen:'equipment',initial:'afk'},
  {name:'map',file:'MapDataPage.jsx',entry:'MapDataPage',selector:'tab',provided:'activeTab',local:'localTab',change:'changeTab',parent:'onActiveTabChange',setter:'setLocalTab',chosen:'train',initial:'city'},
]){
  const s=read(spec.file),nodes=walk(entry(s,spec.entry)),selector=nodes.find(n=>n.type==='VariableDeclarator'&&n.id.name===spec.selector).init;
  for(const provided of [undefined,null,spec.chosen,'unknown','']){assert.equal(vm.runInNewContext(s.slice(selector.start,selector.end),{[spec.provided]:provided,[spec.local]:spec.initial}),provided??spec.initial);results.push({case:spec.name+'-nullish-'+String(provided),pass:true});}
  const callback=nodes.find(n=>n.type==='VariableDeclarator'&&n.id.name===spec.change)?.init||nodes.find(n=>n.type==='FunctionDeclaration'&&n.id.name===spec.change);
  assert.ok(callback);for(const controlled of [false,true]){
    let visited=new Set([spec.initial]);const events=[],record=tag=>value=>events.push([tag,value]);
    const env={[spec.parent]:controlled?record('parent'):undefined,[spec.setter]:record('local'),[spec.selector]:spec.initial};
    if(spec.visit)env[spec.visit]=fn=>{events.push(['visited']);visited=fn(visited);};
    else Object.assign(env,{searchGeneration:{current:0},tabViewCache:{current:new Map()},page:2,rows:['old'],total:55,setPage:record('page'),setRows:record('rows'),setTotal:record('total'),setLoading:record('loading'),setQueryError:record('queryError'),setActionMessage:record('message')});
    const fn=vm.runInNewContext('('+s.slice(callback.start,callback.end)+')',env);fn(spec.chosen);
    assert.deepEqual(events.map(x=>x[0]),spec.visit?['visited',controlled?'parent':'local']:[controlled?'parent':'local','page','rows','total','loading','queryError','message']);
    if(spec.visit)assert.ok(visited.has(spec.chosen));else{assert.equal(env.searchGeneration.current,1);assert.equal(env.tabViewCache.current.get(spec.initial).page,2);events.length=0;fn(spec.initial);assert.equal(events.length,0);assert.equal(env.searchGeneration.current,1,'active Map no-op');}
    results.push({case:spec.name+'-'+(controlled?'parent':'local')+'-notification-order',pass:true});
  }
}
const a=read('AutomationPage.jsx'),ae=entry(a,'AutomationPage'),nodes=walk(ae),grid=nodes.find(n=>n.type==='JSXElement'&&n.openingElement.attributes.some(p=>p.name?.name==='className'&&p.value?.value==='automation-grid'));
const activityNodes=grid.children.filter(n=>n.type==='JSXExpressionContainer').map(n=>n.expression).filter(n=>n.type==='ConditionalExpression');
assert.equal(activityNodes.length,8);const labels=activityNodes.map(n=>{assert.equal(n.test.callee.property.name,'has');assert.equal(n.test.callee.object.name,'visitedCategories');assert.equal(n.consequent.openingElement.name.name,'Activity');const mode=n.consequent.openingElement.attributes.find(p=>p.name.name==='mode').value.expression;assert.equal(mode.test.left.name,'category');assert.equal(mode.test.right.value,n.test.arguments[0].value);assert.equal(mode.consequent.value,'visible');assert.equal(mode.alternate.value,'hidden');return n.test.arguments[0].value;});
assert.deepEqual(labels,groups.groups.map(g=>g.category));assert.equal((a.match(/className="automation-grid"/g)||[]).length,1);
assert.ok(a.includes('automationCards.daily.slice(0, 4)')&&a.includes('automationCards.daily.slice(4)'));
assert.deepEqual(groups.groups[5].leafTypes.slice(0,2),['he','y']);assert.deepEqual(groups.groups[7].titles,['automation.railway.title','automation.secretTask.title','automation.ghost.title']);
const app=read('App.jsx');assert.match(app,/<Fragment key=\{selectedProfileId\}>/);assert.match(app,/pagePropsByRoute = showProfiles \?/);
assert.match(app,/automation: \{ activeCategory: automationCategory, onActiveCategoryChange: setAutomationCategory \}/);
assert.match(app,/"map-data": \{ activeTab: mapTab, onActiveTabChange: setMapTab \}/);assert.match(app,/march: \{ activeTab: squadTab, onActiveTabChange: setSquadTab \}/);
const baseline=fs.readFileSync(path.join(repo,'evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/route-loading/baseline-Pages.jsx'),'utf8');
// The intentionally changed page wrappers own selection/Activity only. Every
// Automation/Equipment/AFK leaf producer, callback and settings body is unchanged.
for(const file of ['AutomationPage.jsx','SquadsPage.jsx'])for(const node of parse(read(file),{sourceType:'module',plugins:['jsx']}).program.body){const n=node.type==='ExportNamedDeclaration'?node.declaration:node;if(n?.type!=='FunctionDeclaration'||['AutomationPage','SquadsPage','EquipmentContent'].includes(n.id.name))continue;const old=entry(baseline,n.id.name);assert.ok(old,n.id.name);assert.equal(read(file).slice(n.start,n.end),baseline.slice(old.start,old.end),n.id.name);}
results.push({case:'exact-eight-Activity-order-seven-categories-Daily-split-and-parent-route-bindings',pass:true});
const output={marker:'LWB317_PROFILE_TABS_CONTRACT_OK',cases:results.length,results,activityGroups:labels,sourceHashes:Object.fromEntries(['App.jsx','AutomationPage.jsx','SquadsPage.jsx','MapDataPage.jsx'].map(f=>[f,sha(read(f))])),limits:'Actual production selectors/callbacks, JSX boundary contracts and unchanged leaf bodies against exact original contract and immutable pre-migration bodies; actual mounted profile and subscription proof is separate.'};
fs.writeFileSync(path.join(here,'current-contract-results.json'),JSON.stringify(output,null,2)+'\n');console.log(JSON.stringify({marker:output.marker,cases:output.cases,groups:labels.length}));
